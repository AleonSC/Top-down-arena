using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Orbital Laser (OrbitalLaser.cs): the DPS Soldier's key 2 on the lobby's Ultimate row (the pick reaching the run on F, and a
// Wildcard's pick), nothing hit during the 1.9 s lock-on, then every regular and heavy enemy in the 260 px circle killed however
// tough, bosses losing 20% once, enemy shots erased, allies and what's outside untouched; the scorch burning and slowing what walks
// in; mastered, 340 px and the beam ploughing 200 px on, away from the caster.
static partial class Program
{
    static void OrbitalLaserTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);

        var def = Upgrades.Ability(AbilityId.OrbitalLaser);
        Check(def.Name == "Orbital Laser" && def.Cooldown == 90f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Kit == Kit.Soldier && def.Role == Role.Dps && def.Capstone != null,
            "Orbital Laser is the DPS Soldier's ultimate on key 2, 90 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Soldier) == AbilityId.ArtilleryAssault && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 1) == AbilityId.AttackHelicopter
            && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 2) == AbilityId.OrbitalLaser,
            "Artillery Assault stays the default and the Attack Helicopter key 1; key 2 is Orbital Laser");
        int wild = Upgrades.WildUlt(Kit.Soldier, 2);
        Check(Upgrades.ClampUlt(Role.Wildcard, Kit.Soldier, wild) == wild && Upgrades.Ultimate(Role.Wildcard, Kit.Soldier, wild) == AbilityId.OrbitalLaser,
            "a Wildcard can pick it too");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 3 }, port, false, "Gunner", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].Ult == 2 && client.Lobby[1].Ult == 2 && host.Lobby[1].UltId == AbilityId.OrbitalLaser, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        int U = Upgrades.UltSlot;
        Check(me.Slots[U].Id == AbilityId.OrbitalLaser && client.Game.Players[1].Slots[U].Id == AbilityId.OrbitalLaser, "in the match the host's F is Orbital Laser, on every game");
        g.DevSetGod(1, true);

        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, float, Enemy> pin = (at, type, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = hp; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at; return e;
        };
        V2 mePos = new V2(200f, 450f), target = new V2(900f, 450f), allyPos = target + new V2(0f, 120f);
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var kv in spots) if (g.Enemies.ContainsKey(kv.Key.Id)) { kv.Key.Pos = kv.Value; kv.Key.FireT = 99f; }
                foreach (var id in g.Enemies.Keys.Where(k => !spots.Keys.Any(e => e.Id == k)).ToList()) g.Enemies.Remove(id);
                me.Pos = mePos; ally.Pos = allyPos;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = target });
            }
        };
        Action reset = () =>
        {
            g.Enemies.Clear(); spots.Clear(); g.Effects.Clear(); g.Bullets.Clear();
            me.Slots[U].Timer = 0f; me.Slots[U].Charges = 1; me.Pos = mePos;
            ally.Hp = ally.MaxHp; ally.Downed = false; ally.InvulnT = 0f;
        };
        Func<Enemy, bool> dead = e => e.Hp <= 0f || !g.Enemies.ContainsKey(e.Id);

        // --- The strike ---------------------------------------------------------------------------------------------------
        reset();
        var grunt = pin(target + new V2(60f, 0f), EnemyType.Grunt, 5000f);
        var heavy = pin(target + new V2(-200f, 80f), EnemyType.Brute, 1e5f);
        var rim = pin(target + new V2(0f, -270f), EnemyType.Grunt, 5000f);   // its centre outside, its body touching the rim
        var outside = pin(target + new V2(330f, 0f), EnemyType.Grunt, 5000f);
        var boss = pin(target + new V2(-60f, -60f), EnemyType.Grunt, 3000f);
        boss.Def = Defs.Get(EnemyType.Colossus);
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, U, target });
        var fx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.OrbitalLaser);
        Check(fx != null && (int)fx.Radius == me.Id && fx.Pos.DistanceTo(target) < 1f && fx.Pos2.DistanceTo(target) < 1f
            && Math.Abs(fx.Color.A * 1000f - Game.OrbRadius) < 0.5f && Math.Abs(fx.Dur - Game.OrbFxDur) < 0.01f,
            "casting it sends the look to every screen (one effect on the cursor, the caster's id, the circle's radius)");
        Check(g.Sounds.Any(c => c.Sfx == Sfx.OrbitalLaser), "the strike sounds (Sfx.OrbitalLaser, sent with the cast)");
        step(0.1f);
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.OrbitalLaser && e.Pos.DistanceTo(target) < 1f), "the other screen draws it");

        step(Game.OrbHitT - 0.2f);
        Check(grunt.Hp == grunt.MaxHp && heavy.Hp == heavy.MaxHp && boss.Hp == boss.MaxHp, "nothing is hit during the lock-on");
        var shots = new List<Bullet>();
        for (int i = 0; i < 4; i++)
            shots.Add((Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, target + new V2(-100f + i * 60f, 30f), new V2(0f, 0.01f), 0f, 6f, 5f, 0, false, 0f }));
        var farShot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, target + new V2(500f, 0f), new V2(0f, 0.01f), 0f, 6f, 5f, 0, false, 0f });
        step(0.4f);
        Console.WriteLine("  orbital: grunt {0}, heavy {1}, rim {2}, outside {3:0}, boss lost {4:0}, ally {5:0}/{6:0}", dead(grunt) ? "dead" : "alive", dead(heavy) ? "dead" : "alive",
            dead(rim) ? "dead" : "alive", outside.Hp, boss.MaxHp - boss.Hp, ally.Hp, ally.MaxHp);
        Check(dead(grunt) && dead(heavy) && dead(rim), "the beam kills every regular and heavy enemy it touches, however tough (5,000 and 100,000 health)");
        Check(outside.Hp == outside.MaxHp, "an enemy outside the circle is untouched");
        Check(Math.Abs(boss.MaxHp - boss.Hp - boss.MaxHp * Game.OrbBossShare) < 1f, "a boss loses 20% of its health");
        Check(shots.All(b => !g.Bullets.ContainsKey(b.Id)) && g.Bullets.ContainsKey(farShot.Id), "enemy shots in the circle are erased, the ones outside fly on");
        Check(ally.Hp == ally.MaxHp && !ally.Downed, "an ally standing in it is safe");
        step(Game.OrbBeamT - 0.6f);  // (to just before the beam ends: the scorch burns it after)
        Check(Math.Abs(boss.MaxHp - boss.Hp - boss.MaxHp * Game.OrbBossShare) < 1f, "...only once, however long the beam stays on it");
        step(0.6f);

        // --- The scorch --------------------------------------------------------------------------------------------------
        var walker = pin(target + new V2(40f, 0f), EnemyType.Grunt, 1e5f);
        step(1f);
        float burnt = walker.MaxHp - walker.Hp, want = Game.OrbBurnDps * me.Stats.DmgMult;
        Check(burnt > want * 0.7f && burnt < want * 1.3f && Math.Abs(walker.SlowMult - (1f - Game.OrbSlow)) < 0.01f && walker.SlowT > 0f,
            "the scorch burns what walks in, 25 a second times your damage (" + burnt.ToString("0.0") + " in 1 s), and slows it 30%");
        step(Game.OrbScorchT);
        float after = walker.Hp;
        step(1f);
        Check(walker.Hp == after, "it stops burning after 4 s");

        // --- Mastered ----------------------------------------------------------------------------------------------------
        reset();
        var wide = pin(target + new V2(0f, -310f), EnemyType.Grunt, 5000f);  // outside 260 px, inside 340
        var ahead = pin(target + new V2(470f, 0f), EnemyType.Grunt, 5000f);  // reached only once the beam has ploughed 200 px on
        var behind = pin(target + new V2(-370f, 0f), EnemyType.Grunt, 5000f);
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, U, target });
        masteredField.SetValue(g, false);
        var mfx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.OrbitalLaser);
        Check(mfx != null && Math.Abs(mfx.Color.A * 1000f - Game.OrbRadiusMastered) < 0.5f && mfx.Pos2.DistanceTo(target + new V2(Game.OrbSweep, 0f)) < 1f,
            "mastered, the look knows (340 px, and the beam's end 200 px on, away from the caster)");
        step(Game.OrbHitT + 0.1f);
        Check(dead(wide) && !dead(ahead), "mastered, the circle is 340 px");
        step(Game.OrbBeamT);
        Check(dead(ahead) && !dead(behind), "...and the beam ploughs on away from you, reaching what stood ahead (not behind)");
        step(Game.OrbScorchT + 0.5f);

    }
}

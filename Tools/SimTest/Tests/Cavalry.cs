using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Cavalry Call (CavalryCall.cs): the lancers' ride, damage, shove and slow once per enemy, what they miss, Heavy and boss handling,
// the 20% guard, and the mastered four-lancer lane.
static partial class Program
{
    static void CavalryTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Tank", Role.Tank, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        me.Slots[0].Id = AbilityId.CavalryCall;
        Check(Upgrades.Ability(AbilityId.CavalryCall).Name == "Cavalry Call" && Upgrades.Ability(AbilityId.CavalryCall).Cooldown == 18f, "the Tank Knight's second ability is Cavalry Call, 18 s");
        g.Enemies.Clear(); g.Bullets.Clear();
        Func<V2, EnemyType, Enemy> make = (at, type) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; return e; };
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) => { var e = make(at, type); spots[e] = at; return e; };
        V2 mePos = new V2(200f, 450f);
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        me.Pos = mePos; me.Aim = 0f;
        var inLane = pin(new V2(560f, 450f), EnemyType.Brute); var farLane = pin(new V2(1150f, 470f), EnemyType.Brute);
        var wide = pin(new V2(600f, 700f), EnemyType.Brute); var behind = pin(new V2(100f, 450f), EnemyType.Brute);
        var shoved = make(new V2(480f, 430f), EnemyType.Brute); var shovedFrom = shoved.Pos;
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        Check(g.Effects.Any(e => e.Kind == FxKind.CavalryCall && Math.Abs(e.Radius - 2f) < 0.01f), "casting it sends the look to every screen (two lancers)");
        Check(me.UnbreakT > 2.9f && me.UnbreakT <= Game.CavGuardTime, "the guard is up for 3 s");
        float t0 = 0f;
        for (int f = 0; f < 0.7f / dt; f++) { step(dt); t0 += dt; }
        Check(inLane.Hp == inLane.MaxHp, "the lancers haven't started riding at 0.7 s");
        step(3.2f);
        float dmg = Game.CavDamage * me.Stats.DmgMult;
        float hitNear = inLane.MaxHp - inLane.Hp, hitFar = farLane.MaxHp - farLane.Hp;
        Console.WriteLine("  cavalry: near {0:0.0}, far {1:0.0}, wide {2:0.0}, behind {3:0.0}", hitNear, hitFar, wide.MaxHp - wide.Hp, behind.MaxHp - behind.Hp);
        Check(Math.Abs(hitNear - dmg) < 0.5f && Math.Abs(hitFar - dmg) < 0.5f, "enemies in the lane take 35, once each, near or 900 px out");
        Check(wide.Hp == wide.MaxHp && behind.Hp == behind.MaxHp, "what the lane misses (off to the side, behind the knight) is untouched");
        Check(shoved.Pos.DistanceTo(shovedFrom) > 60f && Math.Abs(shoved.Pos.Y - shovedFrom.Y) > 40f, "an enemy in the lane is shoved out of it (sideways, not just ahead)");
        Check(shoved.Hp < shoved.MaxHp, "...and hurt");

        // The slow, measured right after a hit.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        var slowed = pin(new V2(560f, 450f), EnemyType.Brute);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        for (int f = 0; f < 3f / dt && slowed.Hp == slowed.MaxHp; f++) step(dt);
        Check(slowed.Hp < slowed.MaxHp && Math.Abs(slowed.SlowMult - (1f - Game.CavSlow)) < 0.01f && slowed.SlowT > 2f, "a hit enemy is slowed 45% for 3 s");
        step(4f);
        Check(slowed.SlowMult == 1f, "...and the slow wears off");

        // A Heavy enemy takes the hit, a short shove and a lighter slow.
        g.Enemies.Clear(); spots.Clear();
        var big = make(new V2(560f, 450f), EnemyType.Brute);
        big.Def = Defs.Get(EnemyType.Juggernaut);
        bool bigHeavy = big.Def.Heavy || big.Def.IsBoss;
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        for (int f = 0; f < 3f / dt && big.Hp == big.MaxHp; f++) step(dt);
        Check(bigHeavy && big.Hp < big.MaxHp && Math.Abs(big.SlowMult - (1f - Game.CavSlowHeavy)) < 0.01f, "a Heavy enemy is hit and only slowed 20%");
        Check(big.KnockLeft.Length > 20f && big.KnockLeft.Length <= Game.CavKnockHeavy + 1f, "...and is shoved only 45 px, not 150");

        // A boss is hit and slowed but never shoved.
        g.Enemies.Clear(); spots.Clear();
        var boss = make(new V2(560f, 450f), EnemyType.Brute);
        boss.Def = Defs.Get(EnemyType.Colossus);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        for (int f = 0; f < 3f / dt && boss.Hp == boss.MaxHp; f++) step(dt);
        Check(boss.Def.IsBoss && boss.Hp < boss.MaxHp && boss.KnockLeft.Length < 1f, "a boss is hit and slowed but not shoved");

        // Mastered: four lancers, the outer pair covers the lane 114 px off the line; the guard lasts 5 s.
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        var outer = pin(new V2(700f, 450f + 114f), EnemyType.Brute);
        var farOut = pin(new V2(700f, 450f + 260f), EnemyType.Brute);
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, 0, new V2(900f, 450f) });
        Check(g.Effects.Any(e => e.Kind == FxKind.CavalryCall && Math.Abs(e.Radius - 4f) < 0.01f) && me.UnbreakT > 4.9f, "mastered, four lancers ride and the guard lasts 5 s");
        step(3.2f);
        masteredField.SetValue(g, false);
        Check(outer.Hp < outer.MaxHp && farOut.Hp == farOut.MaxHp, "...the outer pair reaches 114 px off the line, not 260");

        // The guard takes 20% off.
        me.UnbreakT = 0f; me.InvulnT = 0f; g.DevSetGod(1, false);
        float a0 = me.Run.DamageTaken; damage.Invoke(g, new object[] { me, 40f, "test" }); float plain = me.Run.DamageTaken - a0;
        me.UnbreakT = 3f; me.InvulnT = 0f; a0 = me.Run.DamageTaken; damage.Invoke(g, new object[] { me, 40f, "test" }); float guarded = me.Run.DamageTaken - a0;
        Check(plain > 1f && Math.Abs(guarded - plain * 0.8f) < 0.5f, "the guard is 20% less damage");
    }
}

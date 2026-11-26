using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Rocket Drop (RocketDrop.cs): the DPS Soldier's key 3 on the lobby's Ultimate row. A pod called at the cursor (up to 220 px away)
// slams down after 1.1 s (120 to enemies within 90 px), opens, and waits; only its owner takes the launcher by stepping on it. Then
// 10 s (mastered 13) of rockets in place of the weapon, one every 0.6 s: each bursts on the first enemy, a wall or the cursor for
// 150 at the middle falling to 60 at the 190 px rim, and leaves a fire pool (35 a second, 118 px, 2.6 s). Mastered blasts throw
// 3 bomblets. It ends on time or when you go down; it's seen on every screen.
static partial class Program
{
    static void RocketDropTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);

        var def = Upgrades.Ability(AbilityId.RocketDrop);
        Check(def.Name == "Rocket Drop" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Dps && def.Kit == Kit.Soldier && def.Capstone != null,
            "Rocket Drop is the DPS Soldier's ultimate on key 3, 80 s");
        Check(Upgrades.Ultimate(Role.Dps, Kit.Soldier) == AbilityId.DeployableTurret && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 1) == AbilityId.AttackHelicopter
            && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 2) == AbilityId.OrbitalLaser && Upgrades.Ultimate(Role.Dps, Kit.Soldier, 3) == AbilityId.RocketDrop
            && Upgrades.ClampUlt(Role.Dps, Kit.Soldier, 3) == 3 && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 3) != AbilityId.RocketDrop,
            "the Deployable Turret is the default, then the Attack Helicopter and Orbital Laser; key 3 is Rocket Drop, only for the DPS Soldier");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 6 }, port, false, "Grenadier", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].UltId == AbilityId.RocketDrop && client.Lobby[1].Ult == 3, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, noPicks);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.RocketDrop, "in the match the host's ultimate is Rocket Drop");
        g.DevSetGod(client.MyId, true);

        V2 meAt = new V2(500f, 500f), drop = new V2(650f, 500f), allyAt = new V2(300f, 250f), aim = new V2(900f, 500f);
        bool fire = false;
        var mine = new Dictionary<int, V2>();
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.ContainsKey(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { e.Pos = mine[e.Id]; e.FireT = 99f; }
                g.State = MatchState.Fighting;
                me.Pos = meAt; me.InvulnT = 1f; ally.Pos = allyAt;
                noPicks();
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = aim, Fire = fire });
            }
        };
        step(0.2f);
        Func<V2, float, Enemy> enemy = (at, hp) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = hp; mine[e.Id] = at;
            return e;
        };
        Action clear = () =>
        {
            foreach (var e in g.Enemies.Values.ToList()) g.Enemies.Remove(e.Id);
            mine.Clear();
            enemy(new V2(120f, 120f), 1e7f);  // a keeper far off, so the wave isn't over
        };
        Action charge = () => { me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f; };

        // the call and the slam
        var near = enemy(drop + new V2(40f, 0f), 1000f);
        var far = enemy(drop + new V2(0f, 200f), 1000f);
        float speedBefore = me.Speed;
        charge();
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, drop, false });
        var z = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.RocketDrop);
        Check(z != null && z.Pos.CloserThan(drop, 1f), "the pod is called onto the cursor");
        step(0.9f);
        Check(near.Hp == 1000f && !Game.RocketLanded(z), "nothing lands for the first 1.1 s");
        step(0.3f);
        Check(Math.Abs(1000f - near.Hp - Game.RocketSlamDmg) < 1f, "the pod slams the enemy under it for 120 (" + (1000f - near.Hp).ToString("0") + ")");
        Check(far.Hp == 1000f, "an enemy 200 px off isn't touched");
        clear();
        settle();
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.RocketDrop), "the other screen has the pod");

        // only its owner takes it, by stepping on it
        allyAt = drop;
        step(1f);
        Check(!Game.RocketOn(me) && !Game.RocketOn(ally) && g.Zones.ContainsKey(z.Id) && Game.RocketReady(z), "it opens and waits; an ally standing on it doesn't take it");
        allyAt = new V2(300f, 250f);
        meAt = drop + new V2(20f, 0f);
        step(0.1f);
        Check(Game.RocketOn(me) && me.RocketT > Game.RocketTime - 0.3f && me.UltT > 0f, "you step on it and shoulder the launcher: 10 s");
        Check(Math.Abs(me.Speed - speedBefore * Game.RocketSlow) < 0.5f, "carrying it you move 85% as fast (" + me.Speed.ToString("0") + " of " + speedBefore.ToString("0") + ")");
        step(Game.RocketFold + 0.1f);
        Check(!g.Zones.ContainsKey(z.Id), "the empty pod folds away");
        settle();
        Check(Game.RocketOn(cg.Players[me.Id]), "the other screen sees the launcher");

        // rockets: a direct hit, the falloff, out of reach, the fire
        meAt = new V2(400f, 500f);
        var target = enemy(new V2(800f, 500f), 10000f);
        var side = enemy(new V2(800f, 640f), 10000f);
        var outside = enemy(new V2(800f, 760f), 10000f);
        aim = target.Pos;
        int shots0 = me.RocketShots;
        fire = true;
        step(0.05f);
        fire = false;
        Check(me.RocketShots == shots0 + 1 && g.Bullets.Values.Count(b => b.Kind == Defs.RocketShell) == 1, "Fire launches a rocket");
        Check(g.Bullets.Values.All(b => b.Kind == Defs.RocketShell || b.Owner != me.Id), "and not the weapon");
        step(0.6f);
        float hitT = 10000f - target.Hp, hitS = 10000f - side.Hp;
        Check(hitT >= Game.RocketMid * me.Stats.DmgMult * 0.95f, "it bursts on the enemy for the full 150 (" + hitT.ToString("0") + ")");
        Check(hitS > Game.RocketEdge * me.Stats.DmgMult * 0.9f && hitS < hitT, "one 140 px off takes less (" + hitS.ToString("0") + ")");
        Check(outside.Hp == 10000f, "one 260 px off is out of reach");
        var pool = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.RocketFire);
        Check(pool != null && pool.Pos.CloserThan(target.Pos, 40f), "it leaves a fire pool");
        float before = target.Hp;
        step(1f);
        Check(before - target.Hp > Game.RocketFireDps * 0.6f && before - target.Hp < Game.RocketFireDps * 2f, "the fire burns what stands in it (" + (before - target.Hp).ToString("0") + " in 1 s)");
        settle();
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.RocketFire), "the other screen has the fire");
        step(Game.RocketFireTime);
        Check(!g.Zones.Values.Any(q => q.Kind == ZoneKind.RocketFire), "and it burns out");

        // infinite ammo at the rate; a miss bursts at the cursor
        clear();
        aim = meAt + new V2(300f, 0f);
        shots0 = me.RocketShots;
        fire = true;
        step(1.25f);
        fire = false;
        Check(me.RocketShots - shots0 == 3, "one rocket every 0.6 s, no reload (" + (me.RocketShots - shots0) + " in 1.25 s)");
        step(0.5f);
        Check(g.Zones.Values.Count(q => q.Kind == ZoneKind.RocketFire && q.Pos.CloserThan(aim, 20f)) >= 3, "with nothing in the way they burst at the cursor");

        // it ends on time
        step(me.RocketT + 0.2f);
        Check(!Game.RocketOn(me) && me.RocketT == 0f && me.UltT == 0f, "after 10 s you drop the tube");
        settle();
        Check(!Game.RocketOn(cg.Players[me.Id]), "on the other screen too");

        // mastered: 13 s and bomblets; going down drops it
        me.MasteryXp[AbilityId.RocketDrop] = 1e6f;
        charge();
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, meAt + new V2(1500f, 0f), false });
        var z2 = g.Zones.Values.First(q => q.Kind == ZoneKind.RocketDrop);
        Check(z2.Pos.DistanceTo(meAt) <= Game.RocketReach + 1f, "a pod called too far lands 220 px out");
        step(Game.RocketCallIn + Game.RocketOpen + 0.1f);
        meAt = z2.Pos;
        step(0.1f);
        Check(me.RocketT > Game.RocketTime + 2f, "mastered, the launcher lasts 13 s (" + me.RocketT.ToString("0.0") + ")");
        aim = meAt + new V2(300f, 0f);
        step(0.3f);  // (the launcher's first rocket is ready 0.3 s after you take it)
        fire = true;
        step(0.05f);
        fire = false;
        int bomblets = 0;
        for (int f = 0; f < 0.5f / dt && bomblets == 0; f++)
        {
            step(dt);
            bomblets = g.Bullets.Values.Count(b => b.Bomblet && b.Owner == me.Id);
        }
        Check(bomblets == Game.RocketBomblets, "mastered, each blast throws 3 bomblets (" + bomblets + ")");
        me.InvulnT = 0f;
        me.Downed = true;
        g.FixedStep(dt, new PlayerInput { AimAt = aim });
        Check(me.RocketT == 0f, "going down drops the launcher");
        me.Downed = false; me.Hp = me.MaxHp;

        host.Leave(""); client.Leave("");
    }
}

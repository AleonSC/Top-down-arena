using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Soldier DPS: the Twin Rifle's grenade tubes (TwinGrenades.cs) and Airstrike (Airstrike.cs).
static partial class Program
{
    /// The tubes: rifle hits load them (crossfire twice as fast), two at most; the weapon-action key fires one a press, left then
    /// right, and the turn order holds when a tube is refilled in between; the grenade bursts where the streams cross; the
    /// tubes empty when the rifle is put away, and the screens see all of it.
    static void TwinGrenadeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gunner", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.TwinRifle, "the Soldier DPS's first weapon is the Twin Rifle");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), aimAt = new V2(700f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) e.Pos = aimAt;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aimAt, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Bullet> NewGrenade = () => g.Bullets.Values.Where(b => b.Kind == Defs.GrenadeShot && b.Owner == 1).OrderByDescending(b => b.Life).FirstOrDefault();
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Check(me.TwinNades == 0, "the tubes start empty");
        Step(0.3f, false, true);
        Check(NewGrenade() == null, "the weapon-action key does nothing with the tubes empty");

        var dummy = Dummy(aimAt);
        Step(1.5f, true, false);
        Console.WriteLine("  twin tubes: after 1.5 s of crossfire, {0} loaded, charge {1}", me.TwinNades, me.TwinCharge);
        Check(me.TwinNades >= 1, "crossfire hits load a grenade in about a second (40 hits, each counting twice)");
        Step(3f, true, false);
        Check(me.TwinNades == Game.TwinNadeMax && me.TwinCharge == 0, "the tubes hold two at most (" + me.TwinNades + ", charge " + me.TwinCharge + ")");
        Step(0.3f, false, false);
        Check(client.Game.Players[1].TwinNades == 2 && client.Game.Players[1].TwinNext == 0, "the other screen sees two loaded, the left one next");

        // Fire: the left tube first (arena y runs down the screen, so the left of an aim to the right is up).
        g.Bullets.Clear();
        Step(dt * 2f, false, true);
        var g1 = NewGrenade();
        Check(g1 != null && g1.Pos.Y < from.Y - 10f, "the first press fires the left tube (" + (g1 == null ? "none" : g1.Pos.ToString()) + ")");
        Check(me.TwinNades == 1 && me.TwinNext == 1, "one left, and the right tube is next");
        Step(0.1f, false, true);
        Check(me.TwinNades == 1, "a second press within 0.25 s does nothing");
        Step(0.3f, false, false);
        var before2 = g.Bullets.Values.Where(b => b.Kind == Defs.GrenadeShot).Select(b => b.Id).ToList();
        Step(dt * 2f, false, true);
        var g2 = g.Bullets.Values.FirstOrDefault(b => b.Kind == Defs.GrenadeShot && !before2.Contains(b.Id));
        Check(me.TwinNades == 0 && me.TwinNext == 0 && g2 != null && g2.Pos.Y > from.Y + 10f, "the next press fires the right one");

        // Fire the left, refill, and the right still goes next.
        g.Bullets.Clear();
        Step(3f, true, false);
        Check(me.TwinNades == 2 && me.TwinNext == 0, "reloaded: two, the left next");
        Step(0.3f, false, false);
        Step(0.05f, false, true);
        Check(me.TwinNades == 1 && me.TwinNext == 1, "the left fired");
        Step(1.5f, true, false);
        Check(me.TwinNades == 2, "the left tube is refilled");
        g.Bullets.Clear();
        Step(0.3f, false, false);
        Step(dt * 2f, false, true);
        var g3 = NewGrenade();
        Check(g3 != null && g3.Pos.Y > from.Y + 10f && me.TwinNext == 0, "after a refill the next press still fires the right tube");

        // The blast lands where the streams cross.
        Step(1f, false, false);
        float hp0 = dummy.Hp;
        g.Bullets.Clear();
        Step(dt * 2f, false, true);
        Step(1f, false, false);
        Console.WriteLine("  twin tubes: a grenade at the crossing took {0:0}", hp0 - dummy.Hp);
        Check(hp0 - dummy.Hp >= Game.TwinNadeDamage * me.Stats.DmgMult - 1f, "the grenade bursts at the crossing for at least 60");

        // Put the rifle away and the tubes empty.
        me.Weapon = Defs.Weapon(Role.Dps, Kit.Soldier, 1);
        Step(0.1f, false, false);
        Check(me.TwinNades == 0 && me.TwinCharge == 0, "putting the rifle away empties the tubes");
        host.Leave(""); client.Leave("");
    }

    /// Airstrike: replaces the Cluster Grenade; a lane across the aim through the flare, wall to wall; nothing before the jet; each
    /// bomb hits what is near it once; the lane burns for 3 s; what is off the lane is untouched; mastered, a second wall 160 px out;
    /// Bigger Boom widens the bombs; the client gets the look.
    static void AirstrikeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gunner", Role.Dps, 0, 1, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var def = Upgrades.Ability(AbilityId.Airstrike);
        Check(def.Name == "Airstrike" && def.Kit == Kit.Soldier && def.Role == Role.Dps && def.Choice == 1 && def.Cooldown == 16f, "the Soldier DPS's second ability is Airstrike, 16 s");
        Check(me.Slots[0].Id == AbilityId.Airstrike, "a Soldier DPS who picks it has it on Space");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; spots[e] = at;
            return e;
        };
        V2 mePos = new V2(400f, 450f), flare = new V2(900f, 450f);
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; queue.Clear(); foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = flare } : new PlayerInput()); } } };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear(); queue.Clear();
        me.Pos = mePos; me.Aim = 0f;
        // The lane runs top to bottom through x = 900 (across an aim to the right); bombs fall every 140 px from the end the jet comes in.
        V2 laneFrom = new V2(900f, Game.Height - Game.AirEdgeInset);
        V2 onBomb = laneFrom + new V2(0f, -Game.AirBombAlong(4));
        var hitA = pin(onBomb);
        var beside = pin(onBomb + new V2(60f, 0f));   // inside the burning strip and the bomb's blast
        var offLane = pin(new V2(1250f, 450f));
        var behind = pin(new V2(300f, 300f));
        var second = pin(new V2(900f + Game.AirSecondGap, onBomb.Y));  // only the mastered second wall reaches it
        masteredField.SetValue(g, false);
        cast.Invoke(g, new object[] { me, 0, flare });
        step(0.05f);
        var fx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.Airstrike);
        Check(fx != null && fx.Color.A > 0.75f && Math.Abs(fx.Pos.X - 900f) < 1f && Math.Abs(fx.Pos2.X - 900f) < 1f
            && Math.Abs(fx.Pos.Y - fx.Pos2.Y) > Game.Height - 2f * Game.AirEdgeInset - 2f, "the lane runs across the aim, through the flare, wall to wall");
        step(1.0f);
        Check(spots.Keys.All(e => e.Hp == e.MaxHp), "nothing is hit before the jet comes in");
        step(0.3f);
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.Airstrike), "the other screen gets the Airstrike look");
        int bombs = Game.AirBombCount(fx.Pos.DistanceTo(fx.Pos2));
        float lastBomb = Game.AirBombTime(bombs - 1);
        step(lastBomb - 1.35f - 0.03f);  // (just before the last bomb, which also lights the lane)
        float bomb = Game.AirBombDamage * me.Stats.DmgMult;
        Console.WriteLine("  airstrike: {0} bombs, on a bomb took {1:0}, beside it {2:0}, off the lane {3:0}, behind you {4:0}", bombs, took(hitA), took(beside), took(offLane), took(behind));
        Check(bombs >= 6 && Math.Abs(took(hitA) - bomb) < 0.5f, "an enemy under a bomb takes 55, once");
        Check(took(beside) > 0f && took(beside) <= 2f * bomb + 0.5f, "one 60 px off the line is caught by the blast too");
        float before = took(hitA);
        step(Game.AirFireTime + 0.2f);
        float fire = took(hitA) - before;  // (the strip starts as the last bomb falls)
        Console.WriteLine("  airstrike: the burning strip did {0:0.0}", fire);
        Check(Math.Abs(fire - Game.AirFireDps * Game.AirFireTime * me.Stats.DmgMult) < 4f, "the lane burns for 3 s at 15 a second");
        Check(offLane.Hp == offLane.MaxHp && behind.Hp == behind.MaxHp && second.Hp == second.MaxHp, "what is off the lane is untouched");
        step(1f);
        float after = took(hitA);
        step(1f);
        Check(took(hitA) == after, "and then it is over");

        // Mastered: a second wall 160 px further out, half a second later, without a flare.
        g.Effects.Clear();
        foreach (var e in spots.Keys) e.Hp = e.MaxHp;
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, 0, flare });
        masteredField.SetValue(g, false);
        step(0.3f);
        Check(g.Effects.Count(e => e.Kind == FxKind.Airstrike) == 1, "mastered: the second wall waits half a second");
        step(0.4f);
        var walls = g.Effects.Where(e => e.Kind == FxKind.Airstrike).ToList();
        Check(walls.Count == 2 && walls.Any(w => w.Color.A < 0.75f && Math.Abs(w.Pos.X - (900f + Game.AirSecondGap)) < 1f), "...then lands 160 px further out (no flare)");
        step(lastBomb - 0.7f + Game.AirSecondDelay + 0.05f);
        Func<Enemy, bool> bombed = e => took(e) > bomb - 0.5f && took(e) < bomb + 4f * Game.AirFireDps * Game.AirFireTick + 0.5f;  // one bomb and a few ticks of fire
        Check(bombed(second) && bombed(hitA), "both walls bomb their own lane (" + took(hitA) + ", " + took(second) + ")");
        step(Game.AirFireTime + 1f);

        // Bigger Boom: bombs blast 50% wider.
        g.Effects.Clear();
        foreach (var e in spots.Keys) e.Hp = e.MaxHp;
        var wideOne = pin(onBomb + new V2(115f, 0f));
        Check(Upgrades.All.Any(u => u.Name == "Bigger Boom" && u.RequiresAbility == AbilityId.Airstrike), "Bigger Boom goes with Airstrike");
        step(0.1f);
        float w0 = took(wideOne);
        cast.Invoke(g, new object[] { me, 0, flare });
        step(lastBomb + 0.05f);
        Check(took(wideOne) == w0, "115 px off the line is outside a normal bomb");
        step(Game.AirFireTime + 1f);
        me.Stats.GrenadeRadiusMult = 1.5f;
        cast.Invoke(g, new object[] { me, 0, flare });
        step(lastBomb + 0.05f);
        Check(took(wideOne) > w0, "...but inside one Bigger Boom widened");
        me.Stats.GrenadeRadiusMult = 1f;
        host.Leave(""); client.Leave("");
    }
}

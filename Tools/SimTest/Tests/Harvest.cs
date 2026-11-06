using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Crimson Harvest (CrimsonHarvest.cs): the rip (damage and blood from every enemy), the hold, the throw (a second press, or the timer),
// the blast's size and falloff, and the mastered hold.
static partial class Program
{
    static void HarvestTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cast = typeof(Game).GetMethod("CastAbility", flags);
        var masteredField = typeof(Game).GetField("castMastered", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Blood", Role.Dps, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        me.Slots[Upgrades.UltSlot].Id = AbilityId.CrimsonApotheosis;
        var def = Upgrades.Ability(AbilityId.CrimsonApotheosis);
        Check(def.Name == "Crimson Harvest" && def.Cooldown == 65f && Upgrades.Ultimate(Role.Dps, Kit.Hemomancer) == AbilityId.CrimsonApotheosis, "the DPS Hemomancer's ultimate is Crimson Harvest, 65 s");

        V2 mePos = new V2(300f, 450f), aim = new V2(900f, 450f);
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; spots[e] = at; return e;
        };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; me.Hp = me.MaxHp; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); } };
        Action reset = () => { g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.Pos = mePos; me.Slots[Upgrades.UltSlot].Charges = 1; };

        // The rip: every enemy, near or far, takes 20 and gives its blood.
        reset();
        var near = pin(new V2(500f, 450f), EnemyType.Grunt); var far = pin(new V2(1500f, 800f), EnemyType.Grunt);
        var big = pin(new V2(700f, 300f), EnemyType.Brute); var back = pin(new V2(100f, 100f), EnemyType.Grunt);
        float expect = new[] { near, far, big, back }.Sum(e => Game.HarvestWeight(e));
        Check(Game.HarvestWeight(near) == 1f && Game.HarvestWeight(big) >= 1f, "a Grunt gives 1 blood and a bigger enemy at least as much");
        masteredField.SetValue(g, false);
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, false });
        Check(me.HarvestHold == Game.HarvestHoldTime && me.HarvestBlood == 0f && !Game.HarvestHolding(me), "casting it starts the rip with a 5 s hold waiting");
        step(0.5f);
        Check(Game.HarvestActive(me) && !Game.HarvestHolding(me), "the rip is still running at 0.5 s");
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, true });  // pressed early: nothing happens, nothing is spent
        Check(Game.HarvestActive(me) && !g.Effects.Any(e => e.Kind == FxKind.HarvestThrow), "pressing it during the rip doesn't throw");
        step(Game.HarvestRip - 0.5f + 0.1f);
        float ripped = near.MaxHp - near.Hp, ripFar = far.MaxHp - far.Hp;
        Console.WriteLine("  harvest: blood {0:0.0} (expected {1:0.0}); near took {2:0.0}, far {3:0.0}", me.HarvestBlood, expect, ripped, ripFar);
        Check(Math.Abs(me.HarvestBlood - expect) < 0.01f, "the ball holds the blood of every enemy in the arena");
        Check(Math.Abs(ripped - Game.HarvestRipDmg * me.Stats.DmgMult) < 0.5f && Math.Abs(ripFar - ripped) < 0.5f, "each enemy, near or far, takes 20");
        Check(Game.HarvestHolding(me) && Game.HarvestLeft(me) > 4.5f, "the hold begins when the rip ends");
        Check(Math.Abs(me.Speed - me.Def.Speed * me.Stats.SpeedMult * Defs.KitSpeedMult(me.Kit) * Game.HarvestSlow) < 0.5f, "you move at 60% speed while the ball is up");
        for (int f = 0; f < 0.5f / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; me.Hp = me.MaxHp; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = aim }); }
        Check(client.Game.Players[1].HarvestBlood > expect - 0.5f && client.Game.Players[1].HarvestHold > 0f, "other screens see the ball and its blood");
        Check(Game.HarvestRadius(46f) > Game.HarvestRadius(4f) && Game.HarvestBallR(46f) > Game.HarvestBallR(4f) && Game.HarvestDamage(46f) > Game.HarvestDamage(4f), "more blood: a bigger ball and a bigger blast");

        // The throw: a second press lands it at the cursor. Core takes full damage, the edge half, outside nothing.
        float R = Game.HarvestRadius(me.HarvestBlood), D = Game.HarvestDamage(me.HarvestBlood) * me.Stats.DmgMult;
        g.Enemies.Clear(); spots.Clear(); g.Effects.Clear();
        V2 land = aim;
        var core = pin(land, EnemyType.Grunt); var edge = pin(land + new V2(R * 0.9f, 0f), EnemyType.Grunt); var outside = pin(land + new V2(0f, R + 120f), EnemyType.Grunt);
        var edgeStart = edge.Pos;
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, true });
        Check(!Game.HarvestActive(me) && g.Effects.Any(e => e.Kind == FxKind.HarvestThrow && Math.Abs(e.Radius - expect) < 0.01f && e.Pos2.DistanceTo(land) < 1f), "the second press throws the ball, with its look sent to every screen");
        step(Game.HarvestThrowT - 0.2f);
        Check(core.Hp == core.MaxHp, "it is still in the air 0.2 s before landing");
        for (int f = 0; f < 1f / dt && core.Hp == core.MaxHp; f++) step(dt);
        Check(edge.KnockLeft.Length > 1f || edge.Pos.DistanceTo(edgeStart) > 1f, "enemies are flung outward");
        step(0.5f);
        float hitCore = core.MaxHp - core.Hp, hitEdge = edge.MaxHp - edge.Hp;
        Console.WriteLine("  harvest blast: R {0:0}, core {1:0.0} of {2:0.0}, edge {3:0.0}", R, hitCore, D, hitEdge);
        Check(Math.Abs(hitCore - D) < 1f, "an enemy at the centre takes the full blast");
        Check(hitEdge > D * 0.45f && hitEdge < D * 0.75f, "one near the edge takes about half");
        Check(outside.Hp == outside.MaxHp, "one outside the ring is untouched");
        Check(core.SlowT > 3f && Math.Abs(core.SlowMult - Game.HarvestSlowMult) < 0.01f, "the blast slows what it hits");

        // The timer: left alone, it throws itself at your cursor.
        reset();
        var tgt = pin(aim, EnemyType.Grunt);
        hostAbility.Invoke(g, new object[] { 1, Upgrades.UltSlot, aim, false });
        step(Game.HarvestRip + Game.HarvestHoldTime - 0.3f);
        Check(Game.HarvestActive(me), "still held 0.3 s before the time runs out");
        step(0.6f);
        Check(!Game.HarvestActive(me) && g.Effects.Any(e => e.Kind == FxKind.HarvestThrow), "after 5 s of holding it throws itself");
        step(1f);
        Check(tgt.Hp < tgt.MaxHp - Game.HarvestRipDmg, "...and lands on your cursor");

        // Mastered: a longer hold and 25% more blood.
        reset();
        var m1 = pin(new V2(500f, 450f), EnemyType.Grunt);
        masteredField.SetValue(g, true);
        cast.Invoke(g, new object[] { me, Upgrades.UltSlot, aim });
        masteredField.SetValue(g, false);
        step(Game.HarvestRip + 0.1f);
        Check(me.HarvestHold == Game.HarvestHoldMastered && Math.Abs(me.HarvestBlood - Game.HarvestWeight(m1) * Game.HarvestMasteredBlood) < 0.01f, "mastered: an 8 s hold and 25% more blood");

        // Going down puts the ball away.
        me.Downed = true;
        step(0.1f);
        me.Downed = false;
        Check(!Game.HarvestActive(me) && me.HarvestBlood == 0f, "going down ends the harvest");
        s[0].Leave(""); s[1].Leave("");
    }
}

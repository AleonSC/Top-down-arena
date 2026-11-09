using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Blood Freeze (BloodFreeze.cs): the cost and numbers, the charge before the wave, the wave reaching near enemies before far ones, every
// enemy frozen for 5 s and a boss for 3 s, nothing damaged, allies untouched, the thaw, Hungry Earth's extra time and the mastered +25% damage.
static partial class Program
{
    static void BloodFreezeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);

        var def = Upgrades.Ability(AbilityId.SiphonField);
        Check(def.Name == "Blood Freeze" && def.Cooldown == 14f && def.HealthCost == 0.1f && def.Kit == Kit.Hemomancer && def.Role == Role.Healer,
            "the Healer Hemomancer's second ability is Blood Freeze: 14 s, 10% of max health");
        Check(Game.FreezeWaveR(0.3f) == 0f && Game.FreezeWaveR(Game.FreezeCharge + 0.2f) > 300f && Math.Abs(Game.FreezeWaveR(Game.FreezeCharge + Game.FreezeSweep) - Game.FreezeMaxR) < 0.5f
            && Game.FreezeWaveAt(Game.FreezeMaxR) - (Game.FreezeCharge + Game.FreezeSweep) < 0.01f && Math.Abs(Game.FreezeWaveR(Game.FreezeWaveAt(700f)) - 700f) < 1f,
            "the wave waits for the charge, then rushes out and slows, and its timing inverts cleanly");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Healer", Role.Healer, 0, 0, Kit.Hemomancer);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var friend = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 mePos = new V2(300f, 450f);
        bool keepFull = false;
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, EnemyType, Enemy> pin = (at, type) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; spots[e] = at; return e;
        };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = mePos; if (keepFull) me.Hp = me.MaxHp; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); } };
        Action<bool> castIt = mastered => { g.ShowcaseSetAbility(AbilityId.SiphonField); me.Slots[0].Charges = 1; hostAbility.Invoke(g, new object[] { 1, 0, mePos, true }); };
        Action reset = () => { g.Enemies.Clear(); spots.Clear(); g.Bullets.Clear(); g.Effects.Clear(); me.Pos = mePos; me.Hp = me.MaxHp; };

        reset();
        var near = pin(new V2(500f, 450f), EnemyType.Grunt);
        var far = pin(new V2(1500f, 800f), EnemyType.Grunt);
        var boss = pin(new V2(900f, 200f), EnemyType.Boss);
        var spawning = pin(new V2(700f, 600f), EnemyType.Grunt); spawning.SpawnT = 5f;
        friend.Pos = new V2(600f, 450f);
        float hp0 = me.Hp, nearHp = near.Hp, friendHp = friend.Hp;
        castIt(false);
        float paid = hp0 - me.Hp;
        Check(Math.Abs(paid - me.MaxHp * 0.1f) < 0.5f, "it costs 10% of max health");
        var fx = g.Effects.FirstOrDefault(e => e.Kind == FxKind.BloodFreeze);
        Check(fx != null && fx.Radius == Game.FreezeMaxR && Math.Abs(fx.Dur - Game.FreezeFxTime(0f)) < 0.01f && fx.Pos2.Y == Game.FreezeTime, "one Blood Freeze effect carries the whole show");
        step(0.4f);
        Check(near.FrozenT <= 0f && far.FrozenT <= 0f, "nothing freezes while the rune circle charges");
        step(Game.FreezeCharge - 0.4f + 0.12f);
        Check(near.FrozenT > 0f && far.FrozenT <= 0f, "the wave reaches the near enemy before the far one");
        step(Game.FreezeSweep - 0.2f);
        Console.WriteLine("  blood freeze: near {0:0.00}, far {1:0.00}, boss {2:0.00} s left", near.FrozenT, far.FrozenT, boss.FrozenT);
        Check(far.FrozenT > 0f && near.FrozenT > 0f, "the wave reaches every enemy in the arena");
        Check(far.FrozenT > 3.9f && far.FrozenT <= Game.FreezeTime, "an enemy is frozen for 5 s from the moment the wave reaches it");
        Check(boss.FrozenT > 1.9f && boss.FrozenT <= Game.FreezeBossTime, "a boss is frozen for 3 s");
        Check(spawning.FrozenT <= 0f, "an enemy still spawning is skipped");
        Check(near.Hp == nearHp && friend.Hp == friendHp, "it does no damage and allies are untouched");
        Check(near.Frozen == true || g.Enemies.Values.Any(e => e.FrozenT > 0f), "frozen enemies are replicated as frozen");
        var cl = client.Game.Enemies.Values.Where(e => e.Frozen).Count();
        Console.WriteLine("  blood freeze: the client sees {0} frozen", cl);
        Check(cl >= 2, "the client sees the enemies frozen");
        Vector2Check(near, spots[near]);

        // Frozen, it neither moves nor shoots; and the ice runs out.
        step(Game.FreezeBossTime - 0.2f);
        Check(boss.FrozenT <= 0.3f, "the boss thaws first");
        step(2.5f);
        Check(far.FrozenT <= 0f && near.FrozenT <= 0f && near.IceT <= 0f, "everything thaws after 5 s");
        keepFull = true;

        // The damage bonus is the mastered Blood Freeze's only.
        reset();
        var a = pin(new V2(500f, 450f), EnemyType.Grunt);
        float plain0 = a.Hp;
        hurt.Invoke(g, new object[] { a, 100f, 1 });
        float plain = plain0 - a.Hp;
        a.Hp = a.MaxHp;
        castIt(false); step(Game.FreezeCharge + 0.5f);
        a.Hp = a.MaxHp; float h1 = a.Hp;
        hurt.Invoke(g, new object[] { a, 100f, 1 });
        float frozenHit = h1 - a.Hp;
        Console.WriteLine("  blood freeze: plain hit {0:0.0}, frozen hit {1:0.0}", plain, frozenHit);
        Check(Math.Abs(frozenHit - plain) < 0.5f, "ordinary Blood Freeze does not make enemies take more damage");

        // Mastered: +25% while frozen, and not after.
        reset();
        var m = pin(new V2(500f, 450f), EnemyType.Grunt);
        g.ShowcaseSetAbility(AbilityId.SiphonField); me.Slots[0].Charges = 1;
        me.MasteryXp[AbilityId.SiphonField] = 1e9f;
        hostAbility.Invoke(g, new object[] { 1, 0, mePos, true });
        step(Game.FreezeCharge + 0.5f);
        m.Hp = m.MaxHp; float h2 = m.Hp;
        hurt.Invoke(g, new object[] { m, 100f, 1 });
        float hit = h2 - m.Hp;
        Console.WriteLine("  blood freeze: mastered frozen hit {0:0.0} (plain {1:0.0})", hit, plain);
        Check(m.IceMult == Game.FreezeVuln && Math.Abs(hit - plain * Game.FreezeVuln) < 0.5f, "mastered, frozen enemies take 25% more damage");
        step(Game.FreezeTime + 0.3f);
        m.Hp = m.MaxHp; float h3 = m.Hp;
        hurt.Invoke(g, new object[] { m, 100f, 1 });
        Check(Math.Abs((h3 - m.Hp) - plain) < 0.5f, "and normal damage once the ice has thawed");

        // Hungry Earth: +1.5 s a stack.
        reset();
        var up = Upgrades.All.First(u => u.Name == "Hungry Earth");
        me.Taken.Add(Array.IndexOf(Upgrades.All.ToArray(), up));
        var h = pin(new V2(500f, 450f), EnemyType.Grunt);
        castIt(false); step(Game.FreezeCharge + 0.5f);
        Console.WriteLine("  blood freeze: Hungry Earth holds {0:0.00} s", h.FrozenT);
        Check(h.FrozenT > 5.8f && h.FrozenT <= Game.FreezeTime + 1.5f, "Hungry Earth holds everything 1.5 s longer");
        host.Leave(""); client.Leave("");
    }

    static void Vector2Check(Enemy e, V2 at) => Check(e.Pos.DistanceTo(at) < 1f, "a frozen enemy stays where it was");
}

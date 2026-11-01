using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Bosses that keep up with strong builds (BuildPower.cs).
static partial class Program
{
    static void BossPowerTest(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var host = new Session { AutoStart = 1, StartWave = 20 };
        host.Host(nextPort++, "A", Role.Dps, 0, 0, Kit.Hemomancer);
        host.StartMatch();
        var g = host.Game;
        for (int i = 0; i < 300 && !g.Running; i++) { host.Poll(); g.FixedStep(dt, new PlayerInput()); }
        var me = g.Players[1];
        Func<float> bossScale = () =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Singularity, new V2(800f, 300f), 0f, 0, false });
            float k = e.MaxHp / g.SpawnHp(EnemyType.Singularity, g.Wave);
            e.Hp = 0f;
            return k;
        };
        me.Taken.Clear(); g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, u => u.Name == "Vitality"));
        float plain = bossScale();
        // The uploaded Hemomancer run's picks before wave 20.
        string[] picks = { "Magnum Rounds", "Hair Trigger", "Bloodrush", "Warden's Guard", "Homing Rounds", "Magnum Rounds", "Hair Trigger", "Light Boots", "Sprinter", "Reinforced Guard", "Hair Trigger", "Warden's Guard", "Sharpened Rounds", "Hive Drones", "Homing Rounds", "Sharpened Rounds", "Multishot", "Blink", "Magnum Rounds", "Barrage", "Overcharge", "Fortitude", "Executioner", "Homing Rounds", "Barrage", "Hair Trigger", "Nova", "Thick Skin", "Hemo Nova", "Sharpened Rounds", "Thick Skin", "Hair Trigger", "Magnum Rounds", "Twin Fang", "Second Charge", "Multishot" };
        me.Taken.Clear();
        foreach (var n in picks) me.Taken.Add(Array.FindIndex(Upgrades.All, u => u.Name == n));
        g.GrantUpgrade(1, Array.FindIndex(Upgrades.All, u => u.Name == "Vitality"));
        float strong = bossScale();
        float expect = Game.BossScaleFor(Game.BuildPower(me.Stats) / Game.TypicalPower(me.Role, me.RoleAbility, g.Wave));
        g.ScaleBosses = false;
        float off = bossScale();
        Console.WriteLine("  boss health at wave 20: no upgrades x{0:0.00}; the Hemomancer run's build x{1:0.00} (formula x{2:0.00}); scaling off x{3:0.00}", plain, strong, expect, off);
        Check(Math.Abs(plain - 1f) < 0.001f && strong > 2f && Math.Abs(strong - expect) < 0.01f && strong <= Game.BossPowerCap + 0.001f && Math.Abs(off - 1f) < 0.001f,
            "a boss gets more health (up to x3) for a party far above a typical build, and the usual health otherwise");
        host.Leave("");
    }
}

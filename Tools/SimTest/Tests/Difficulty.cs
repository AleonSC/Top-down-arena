using System;
using System.Threading;
using Arena;

// Difficulty (Difficulty.cs): presets and Custom picked in the lobby, shared, saved, and what each number changes.
static partial class Program
{
    static void DifficultyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var regular = typeof(Game).GetMethod("RegularShotBudget", flags);

        // The numbers on their own: presets, typing, saving.
        var normal = new Difficulty();
        Check(normal.Level == DifficultyLevel.Normal && normal[DiffKnob.Regen] == 0.25f && normal[DiffKnob.OrbChance] == 0.12f && normal[DiffKnob.ReviveTime] == 4f
              && normal[DiffKnob.HitGrace] == 0.25f && normal[DiffKnob.BossBullets] == 0.6f && normal[DiffKnob.SoloHp] == 0.85f && normal[DiffKnob.DamagePerWave] == 0.03f,
            "Normal: 0.25 HP/s regen, 12% orbs, 4 s revives, 0.25 s hit grace, adds get 60% in boss fights, solo enemies 85%, +3% damage a wave");
        var orb = Difficulty.Knobs[(int)DiffKnob.OrbChance];
        float typed;
        Check(orb.TryParse("15", out typed) && Math.Abs(typed - 0.15f) < 1e-5f && orb.TryParse("9%", out typed) && Math.Abs(typed - 0.09f) < 1e-5f
              && !orb.TryParse("lots", out typed) && Math.Abs(orb.Clamp(5f) - orb.Max) < 1e-5f,
            "a typed number reads in the unit shown (12 = 12%), and is kept inside the knob's range");
        var hp = Difficulty.Knobs[(int)DiffKnob.EnemyHp];
        Check(Math.Abs(hp.FromSlider(hp.ToSlider(1.5f)) - 1.5f) < 1e-4f, "a slider maps to its number and back");
        var custom = Difficulty.Of(DifficultyLevel.Hard);
        custom.Set(DiffKnob.EnemyDamage, 2.35f);
        custom.Set(DiffKnob.Regen, 0f);
        var back = Difficulty.Parse(custom.ToText());
        Check(custom.Level == DifficultyLevel.Custom && back.SameAs(custom) && Difficulty.Parse("Nightmare").SameAs(Difficulty.Of(DifficultyLevel.Nightmare))
              && Difficulty.Parse("nonsense").Level == DifficultyLevel.Normal,
            "changing a number makes it Custom, and a difficulty writes to text and reads back the same");
        var save = new RunSave { Diff = custom };
        save.Seats.Add(new SeatSave { Name = "Host" });
        Check(RunSave.Parse(save.ToText()).Diff.SameAs(custom) && RunSave.Parse("arena-run 1\nwave 3\nseat\nname Host\n").Diff.Level == DifficultyLevel.Normal,
            "a run save keeps its difficulty (older saves load as Normal)");

        // The lobby: the host picks, the friend sees it, and only the host can change it.
        int port = nextPort++;
        var host = HostAs(new Session(), port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        Action Sync = () => PollBoth(host, client);
        WaitForLobby(host, client);
        Sync();
        host.SetDifficulty(DifficultyLevel.Hard);
        Sync();
        Check(client.Diff.Level == DifficultyLevel.Hard && client.Diff.SameAs(host.Diff), "the friend sees the host's difficulty");
        client.RequestReady(true); Sync();
        host.SetDifficultyKnob(DiffKnob.EnemyHp, 2f);
        Sync();
        Check(client.Diff.Level == DifficultyLevel.Custom && client.Diff[DiffKnob.EnemyHp] == 2f && client.Diff[DiffKnob.EnemyDamage] == 1.25f && !host.Lobby[2].Ready,
            "a changed number makes it Custom (starting from Hard), everyone sees it, and Ready is taken back");
        client.SetDifficultyKnob(DiffKnob.EnemyHp, 0.5f);
        client.SetDifficulty(DifficultyLevel.Easy);
        Sync();
        Check(host.Diff[DiffKnob.EnemyHp] == 2f && host.Diff.Level == DifficultyLevel.Custom, "only the host can change the difficulty");
        host.SetDifficulty(DifficultyLevel.Normal);
        host.SetDifficultyKnob(DiffKnob.EnemyHp, 1.5f);
        host.SetDifficultyKnob(DiffKnob.EnemyCount, 1.5f);
        host.SetDifficultyKnob(DiffKnob.FireRate, 1.4f);
        Sync();
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        Check(g.Diff.SameAs(host.Diff) && client.Game.Diff.SameAs(host.Diff), "both games play the lobby's difficulty");

        // What the numbers do.
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        float baseHp = Defs.Get(EnemyType.Grunt).Hp * (1f + 0.15f * (g.Wave - 1));  // two players: 100%
        Check(Math.Abs(grunt.MaxHp - baseHp * 1.5f) < 0.01f, "Enemy health: 1.5x on a grunt");
        g.Diff = new Difficulty();
        float bossN = g.SpawnHp(EnemyType.Boss, 5);
        g.Diff = Difficulty.Of(DifficultyLevel.Hard);
        float bossH = g.SpawnHp(EnemyType.Boss, 5);
        Check(Math.Abs(bossH / bossN - 1.25f * 1.2f) < 0.001f, "Hard: bosses have 1.25 x 1.2 = 1.5x health");

        g.Diff = new Difficulty();
        var me = g.Players[1];
        Func<int, float> hit = wave =>
        {
            g.Wave = wave; g.BossSlot = (wave) % 5 == 0 && (wave) > 0;
            me.Hp = me.MaxHp; me.ShieldHp = 0f; me.InvulnT = 0f;
            float t0 = me.Run.DamageTaken;
            damage.Invoke(g, new object[] { me, 10f, "test" });
            return me.Run.DamageTaken - t0;
        };
        float d1 = hit(1), d11 = hit(11), d50 = hit(50);
        float grace = me.InvulnT;
        g.Diff = Difficulty.Of(DifficultyLevel.Nightmare);
        float n11 = hit(11);
        Console.WriteLine("  difficulty: a 10 hit takes {0:0.##} at wave 1, {1:0.##} at 11, {2:0.##} at 50 (Normal); {3:0.##} at 11 on Nightmare; grace {4:0.##} s",
            d1, d11, d50, n11, grace);
        Check(Math.Abs(d11 / d1 - 1.3f) < 0.01f && Math.Abs(d50 / d1 - 2.47f) < 0.01f, "Normal: enemy damage grows 3% a wave (x1.3 at wave 11, x2.47 at 50)");
        Check(Math.Abs(n11 / d1 - 1.5f * 1.5f) < 0.01f, "Nightmare: 1.5x damage, growing 5% a wave");
        Check(Math.Abs(grace - 0.25f) < 1e-4f, "Normal: 0.25 s of hit grace after a hit");

        g.Wave = 10; g.BossSlot = true;
        g.Diff = new Difficulty();
        int budgetN = g.ShotBudget;
        g.Diff = Difficulty.Of(DifficultyLevel.Nightmare);
        int budgetM = g.ShotBudget;
        g.Diff = new Difficulty();
        var boss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, new V2(800f, 450f), 0f, 0, false });
        int duringBoss = (int)regular.Invoke(g, null);
        g.Enemies.Remove(boss.Id);
        Console.WriteLine("  bullet budget at wave 10: {0} (Normal), {1} (Nightmare), {2} for the others while a boss is up", budgetN, budgetM, duringBoss);
        Check(Math.Abs(budgetM - budgetN * 1.3f) <= 2f && duringBoss == (int)(budgetN * 0.6f),
            "the bullet budget scales with the difficulty, and other enemies get 60% of it while a boss is up");
        Check(Math.Abs(g.EliteChance(20) - 0.35) < 1e-6 && Math.Abs(WithDifficulty(g, DifficultyLevel.Nightmare).EliteChance(20) - 0.7) < 1e-6 && g.EliteChance(11) == 0.0,
            "elites: up to 35% on Normal, twice that on Nightmare, none before wave 12");
        host.Leave(""); client.Leave("");
    }

    static Game WithDifficulty(Game g, DifficultyLevel level)
    {
        g.Diff = Arena.Difficulty.Of(level);
        return g;
    }
}

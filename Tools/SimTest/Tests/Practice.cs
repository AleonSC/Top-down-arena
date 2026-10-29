using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Practice Range, the lobby's practice setup, and run modifiers.
static partial class Program
{
    static void PracticeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var cast = typeof(Game).GetMethod("HostAbility", flags);

        // The picker lists what each EnemyDef says, and every listed enemy has a blurb.
        Check(PracticePlan.Choices.Length > 20 && PracticePlan.Choices.All(t => PracticePlan.BlurbOf(t).Length > 0)
            && PracticePlan.Choices.Distinct().Count() == PracticePlan.Choices.Length, "every enemy in the Practice picker has a blurb");

        // Text format: what's saved loads back the same; typos are reported, not fatal.
        var plan = new PracticePlan { Loop = false };
        var w1 = new PracticeWave { Level = 2 }; w1.Counts[EnemyType.Grunt] = 3; w1.Counts[EnemyType.Sniper] = 2;
        var w2 = new PracticeWave { Level = 9 }; w2.Counts[EnemyType.Boss] = 1; w2.Counts[EnemyType.Swarmer] = 2;
        var w3 = new PracticeWave { Level = 4 }; w3.Counts[EnemyType.TwinA] = 1;
        plan.Waves.Add(w1); plan.Waves.Add(w2); plan.Waves.Add(w3);
        string text = plan.ToText("test");
        List<string> problems;
        var back = PracticePlan.Parse(text, out problems);
        Console.WriteLine("  practice file:\n    " + text.Trim().Replace("\n", "\n    "));
        Check(problems.Count == 0 && back.ToText("test") == text && back.Waves.Count == 3 && back.Waves[1].Level == 9 && back.Waves[1].Counts[EnemyType.Boss] == 1
            && back.Waves[2].Counts[EnemyType.TwinA] == 1 && !back.Loop, "a practice setup saves to text and loads back the same");
        var messy = PracticePlan.Parse("loop: yes\r\nwave 3: grunt x2, Blorp x4, warden\r\nnonsense\r\nwave 999: Swarmer x500\r\n", out problems);
        Check(messy.Loop && messy.Waves.Count == 2 && messy.Waves[0].Counts[EnemyType.Grunt] == 2 && messy.Waves[0].Counts[EnemyType.Boss] == 1
            && messy.Waves[1].Level == Game.MaxWave && messy.Waves[1].Counts[EnemyType.Swarmer] == 99 && problems.Count == 2,
            "hand-edited setups load forgivingly (case, missing counts, limits) and list what was skipped");

        // A practice run: exactly the chosen enemies, at the chosen level, then it ends after the last wave.
        int port = nextPort++;
        var host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Tank, 0, 0, Kit.Knight);
        var g = host.Game;
        host.PracticeMode = true;
        host.Plan = PracticePlan.Parse(text, out problems);
        host.StartMatch();
        g.DevSetGod(1, true);
        Action<float, Func<bool>> Solo = (secs, stop) =>
        {
            for (int f = 0; f < secs * 60; f++)
            {
                foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.Offer = null; }
                Step(host, dt);
                if (stop != null && stop()) break;
            }
        };
        var seen = new List<Dictionary<EnemyType, int>>();
        var levels = new List<int>();
        var hp = new Dictionary<int, float>();
        for (int wave = 0; wave < 3; wave++)
        {
            Solo(15f, () => g.State == MatchState.Fighting && g.PracticeIndex == wave);
            Solo(12f, () => g.Remaining == g.Enemies.Count);  // all spawned
            var counts = new Dictionary<EnemyType, int>();
            foreach (var e in g.Enemies.Values)
            {
                counts[e.Type] = counts.TryGetValue(e.Type, out var n) ? n + 1 : 1;
                if (e.Type == EnemyType.Grunt || e.Type == EnemyType.Swarmer)
                    hp[wave] = e.MaxHp;
            }
            seen.Add(counts);
            levels.Add(g.Wave);
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            Solo(3f, () => g.State != MatchState.Fighting);
            for (int k = 0; k < 20 && g.Enemies.Count > 0; k++) { foreach (var e in g.Enemies.Values) e.Hp = 0f; Solo(0.5f, () => g.State != MatchState.Fighting); }
        }
        Func<Dictionary<EnemyType, int>, EnemyType, int> C = (d, t) => d.TryGetValue(t, out var n) ? n : 0;
        Console.WriteLine("  practice waves: " + string.Join(" | ", seen.Select((d, i) => "lv " + levels[i] + ": " + string.Join(", ", d.Select(kv => kv.Key + " x" + kv.Value)))) + "  -> " + g.State);
        Check(seen.Count == 3 && C(seen[0], EnemyType.Grunt) == 3 && C(seen[0], EnemyType.Sniper) == 2 && seen[0].Count == 2
            && C(seen[1], EnemyType.Boss) == 1 && C(seen[1], EnemyType.Swarmer) >= 2 && C(seen[2], EnemyType.TwinA) == 1 && C(seen[2], EnemyType.TwinB) == 1,
            "practice waves spawn exactly the enemies chosen (the Twin Reapers as a pair)");
        Check(levels.SequenceEqual(new[] { 2, 9, 4 }), "each practice wave is fought at its own level");
        Check(g.State == MatchState.Victory, "a practice run ends after its last wave");

        // Looping: it goes back to wave 1 instead of ending.
        port = nextPort++;
        host = new Session { AutoStart = 1 };
        host.Host(port, "Solo", Role.Dps, 0, 0, Kit.Soldier);
        g = host.Game;
        var loop = PracticePlan.Parse("loop: yes\nwave 1: Grunt x1\n", out problems);
        host.PracticeMode = true;
        host.Plan = loop;
        host.StartMatch();
        g.DevSetGod(1, true);
        int fights = 0;
        bool was = false;
        Solo(40f, () =>
        {
            bool fighting = g.State == MatchState.Fighting;
            if (fighting && !was) fights++;
            was = fighting;
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            return fights >= 3 || g.State == MatchState.Victory;
        });
        Check(fights >= 3 && g.State != MatchState.Victory && g.PracticeIndex == 0, "a looping practice plan keeps going round");
        host.Leave("");
        Check(host.Game.Practice == null && !host.PracticeMode, "leaving clears the practice plan, so the next run is a normal one");
        PracticeLobbyTest(dt);
        ModifiersTest(dt);
        PathsTest(dt);
        PathHazardsTest(dt);
        PathTwistsTest(dt);
        PathArenaRulesTest(dt);
        BazaarTest(dt);

        // Shield Wall now stops red aimed shots too.
        port = nextPort++;
        host = new Session { AutoStart = 1, StartWave = 3 };
        host.Host(port, "Tank", Role.Tank, 0, 0, Kit.Knight);
        g = host.Game;
        host.StartMatch();
        Solo(8f, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f; };
        Clear(); Solo(0.3f, null);
        me.Pos = new V2(400f, 450f);
        me.Slots[0].Id = AbilityId.ShieldWall;
        cast.Invoke(g, new object[] { 1, 0, me.Pos, true });
        Action<float> Hold = secs => { for (int f = 0; f < secs * 60; f++) { host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(600f, 450f) }); g.Update(dt); Clear(); } };
        Hold(0.05f);
        float took = me.Run.DamageTaken;
        var red = (Bullet)shoot.Invoke(g, new object[] { Defs.PierceShot, new V2(480f, 450f), new V2(-320f, 0f), 0f, 6f, 15f, 0, false, 0f });
        Hold(0.5f);
        Check(!g.Bullets.ContainsKey(red.Id) && me.Run.DamageTaken == took, "Shield Wall stops a red aimed shot from the front");

        // Touching a boss: one hit, a shove back out, and no more hits for a second.
        me.WallT = 0f;
        g.DevSetGod(1, false);
        me.InvulnT = 0f;
        var boss = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Boss, new V2(800f, 450f), 0f, 0, false });
        boss.Hp = boss.MaxHp = 1e6f;
        Solo(1.5f, null);  // let it finish spawning
        int hits = 0;
        float maxSep = 0f, minSep = 1e9f;
        for (int f = 0; f < 120; f++)
        {
            boss.FireT = 10f; boss.Pos = new V2(800f, 450f);  // held in place (frozen enemies don't touch-damage at all)
            foreach (var b in g.Bullets.Values.ToList()) g.Bullets.Remove(b.Id);
            if (f == 0 || f == 30) me.Pos = new V2(800f - boss.Radius * 0.5f, 450f);  // shoved into it twice
            float before = me.Run.DamageTaken;
            me.InvulnT = 0f;
            host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = boss.Pos }); g.Update(dt);
            if (me.Run.DamageTaken > before) hits++;
            float sep = me.Pos.DistanceTo(boss.Pos) - boss.Radius;
            if (f == 12) maxSep = sep;
            minSep = Math.Min(minSep, sep);
        }
        Console.WriteLine("  boss contact: {0} hits in 2s (two touches), {1:0} px clear of the boss 0.2s after touching", hits, maxSep);
        Check(maxSep > 40f, "touching a boss shoves you back out");
        Check(hits >= 1 && hits <= 2, "a boss's body hurts at most once a second");
        host.Leave("");
    }

    static void PracticeLobbyTest(float dt)
    {
        // The Practice Range lives in the lobby: everyone sees the setup, and the host decides who can edit it.
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        List<string> problems;
        host.SetPracticeMode(true);
        host.Plan = PracticePlan.Parse("wave 4: Grunt x3\nwave 10: The Warden x1", out problems);
        host.PracticeEdited(1);
        Sync();
        Check(client.PracticeMode && client.Plan.ToText() == host.Plan.ToText() && client.PlanEditor == "Host" && client.PlanEditorSel == 1 && !client.CanEditPractice,
            "the lobby shows everyone the host's practice waves as they change (and which wave they're on)");

        // A friend's edit is undone unless the host lets them edit.
        string before = host.Plan.ToText();
        client.Plan.Waves[0].Counts[EnemyType.Sniper] = 5;
        typeof(Session).GetMethod("PracticeEdited").Invoke(client, new object[] { 0 });  // the UI wouldn't even offer it
        var w = Proto.Begin(Msg.PracticeEdit); w.Write(client.Plan.ToText()); w.Write(0); client.SendToHost(Proto.End(w));
        Sync();
        Check(host.Plan.ToText() == before && client.Plan.ToText() == before, "without the host's OK, a friend's edits are put back");

        host.SetOthersCanEdit(true);
        Sync();
        client.Plan.Waves[0].Counts[EnemyType.Sniper] = 5;
        client.Plan.Waves.Add(new PracticeWave { Level = 7 });
        client.PracticeEdited(2);
        Sync();
        Check(client.CanEditPractice && host.Plan.Waves.Count == 3 && host.Plan.Waves[0].Counts[EnemyType.Sniper] == 5 && host.PlanEditor == "Friend" && host.PlanEditorSel == 2,
            "with Let others edit on, a friend's changes reach the host");

        // Starting plays the plan for everyone; the HUD knows which practice wave it is.
        host.Plan = PracticePlan.Parse("wave 2: Grunt x1\nwave 3: Swarmer x2", out problems);
        host.PracticeEdited(0);
        Sync();
        host.RequestReady(true);
        client.RequestReady(true);
        Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        host.Game.DevSetGod(1, true); host.Game.DevSetGod(2, true);
        Run(s, dt, 15f, () => host.Game.State == MatchState.Fighting && host.Game.PracticeIndex == 1 && client.Game.PracticeIndex == 1,
            () => { foreach (var e in host.Game.Enemies.Values) e.Hp = 0f; foreach (var p in host.Game.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.Offer = null; } });
        Check(client.Game.Practice != null && client.Game.Practice.Waves.Count == 2 && client.Game.PracticeIndex == 1 && host.Game.PracticeIndex == 1,
            "a practice match plays the lobby's waves, and everyone's HUD follows which one it is");
        host.Leave("");
        client.Leave("");
    }

    static void ModifiersTest(float dt)
    {
        // Run modifiers: the host picks them in the lobby, everyone sees them, and they change the run.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        Action Sync = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); } };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
        Sync();
        host.SetModifier(RunMod.ToughCrowd, true);
        host.SetModifier(RunMod.GlassCannon, true);
        host.SetModifier(RunMod.Lucky, true);
        host.SetModifier(RunMod.Famine, true);
        Sync();
        Check(client.Modifiers == host.Modifiers && Modifiers.Count(client.Modifiers) == 4, "the friend sees the host's run modifiers");
        client.RequestReady(true); host.RequestReady(true); Sync();
        host.StartMatch();
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        Check(g.Has(RunMod.ToughCrowd) && client.Game.Has(RunMod.Lucky), "both games know the run's modifiers");
        Check(g.Players[1].Rerolls == Game.StartingRerolls + Modifiers.LuckyRerolls && client.Game.Players[2].Rerolls == Game.StartingRerolls + Modifiers.LuckyRerolls,
            "Lucky: everyone starts with 3 extra rerolls");
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        float baseHp = Defs.Get(EnemyType.Grunt).Hp * (1f + 0.15f * (g.Wave - 1));
        Check(Math.Abs(grunt.MaxHp - baseHp * Modifiers.ToughCrowdHp * (grunt.MaxHp / (baseHp * Modifiers.ToughCrowdHp))) < 0.01f && grunt.MaxHp >= baseHp * 1.49f,
            "Tough Crowd: enemies have 50% more health");
        float hp0 = grunt.Hp;
        hurt.Invoke(g, new object[] { grunt, 10f, 1 });
        var me = g.Players[1];
        me.InvulnT = 0f;
        float t0 = me.Run.DamageTaken;
        damage.Invoke(g, new object[] { me, 10f, "test" });
        Console.WriteLine("  modifiers: grunt hp {0:0} (base {1:0}), a 10 hit dealt {2:0.#}, a 10 hit taken {3:0.#}", grunt.MaxHp, baseHp, hp0 - grunt.Hp, me.Run.DamageTaken - t0);
        Check(Math.Abs((hp0 - grunt.Hp) - 15f) < 0.01f && Math.Abs((me.Run.DamageTaken - t0) - 15f * me.Stats.DamageTaken) < 0.5f, "Glass Cannon: everyone deals and takes 50% more");
        int orbs = g.Orbs.Count;
        for (int k = 0; k < 40; k++)
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(700f + k, 400f), 0f, 0, false });
            e.Hp = 0f;
        }
        Step(host, dt);
        Check(g.Orbs.Count == orbs, "Famine: enemies drop no health orbs");
        host.Leave(""); client.Leave("");
        Check(host.Modifiers == RunMod.None && client.Modifiers == RunMod.None, "leaving clears the modifiers");
    }
}

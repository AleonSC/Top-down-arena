using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The game's test suite: real Sessions (host and friends over localhost TCP, driven by bots) and offline games,
// checking that everything works and stays in sync. The tests live in Tests/, one file per system; this file has
// the shared helpers and the table of suites.
//   run.sh [wave] [seconds] [suite]      e.g. run.sh 1 30 (core), run.sh 1 30 features, run.sh 1 30 all
//   run.sh list                          every suite and report
// Random rolls are repeatable: each test seeds the games it makes (Game.SeedForTests); set SEED=random for fresh
// rolls, or SEED=<n> to try another fixed set.
static partial class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine((ok ? "PASS " : "FAIL ") + what);
        if (!ok)
            failures++;
    }

    static readonly List<Sfx> clientHeard = new List<Sfx>();  // every sound the client games played

    static void Step(Session s, float dt)
    {
        s.Poll();
        s.Game.FixedStep(dt, new PlayerInput { AimAt = new V2(640, 360) });
        s.Game.Update(dt);
        if (s.IsHost)
            foreach (var cue in s.Game.Sounds)
                if (cue.Sfx == Sfx.Pickup || cue.Sfx == Sfx.PickupBig)
                    pickups++;
        if (!s.IsHost && s.Game.Orbs.Count > 0)
            clientSawOrbs = true;
        if (!s.IsHost)
            clientHeard.AddRange(s.Game.Sounds.Select(c => c.Sfx));
        s.Game.Sounds.Clear();
    }

    static int pickups;
    static bool clientSawOrbs;

    static int nextPort = 7810;

    // Start a bot match at a given wave with both players buffed so they survive a while.
    static Session[] StartBuffed(float dt, int wave, int damageStacks)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = wave };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        host.Host(port, "Host", Role.Tank, 0);
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        foreach (int id in new[] { 1, 2 })
        {
            for (int k = 0; k < 20; k++) host.Game.GrantUpgrade(id, U("Fortitude"));
            for (int k = 0; k < 10; k++) host.Game.GrantUpgrade(id, U("Regeneration"));
            for (int k = 0; k < damageStacks; k++) host.Game.GrantUpgrade(id, U("Hollow Points"));
        }
        return new[] { host, client };
    }

    static void Run(Session[] s, float dt, float seconds, Func<bool> stop = null, Action each = null)
    {
        for (int i = 0; i < seconds * 60; i++)
        {
            Step(s[0], dt);
            Step(s[1], dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            if (each != null)
                each();
            if (stop != null && stop())
                break;
        }
        for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
    }

    static int Ghosts(Session[] s)
    {
        int ghosts = 0;
        foreach (var b in s[1].Game.Bullets.Values)
            if (!s[0].Game.Bullets.ContainsKey(b.Id))
                ghosts++;
        return ghosts;
    }

    static bool BossAlive(Session s)
    {
        foreach (var e in s.Game.Enemies.Values)
            if (e.Def.IsBoss)
                return true;
        return false;
    }

    static Enemy Find(Session s, EnemyType t)
    {
        foreach (var e in s.Game.Enemies.Values)
            if (e.Type == t)
                return e;
        return null;
    }

    // --- Suites -------------------------------------------------------------------------------------

    const float Dt = 1f / 60f;

    class Suite
    {
        public string Name, What;
        public bool Report;              // a balance report or debug tool: prints numbers, has no pass/fail
        public Action<string[]> Run;     // gets the whole command line (wave, seconds, suite, extras)
    }

    /// Run one test with its own repeatable random rolls (the seed comes from the test's name).
    static void Seeded(string name, Action test)
    {
        string mode = Environment.GetEnvironmentVariable("SEED");
        if (mode == "random")
            Game.SeedForTests(Environment.TickCount);
        else
        {
            int salt;
            int.TryParse(mode, out salt);
            uint h = 2166136261u;
            foreach (char c in name)
                h = (h ^ c) * 16777619u;
            Game.SeedForTests((int)(h & 0x7fffffff) ^ salt);
        }
        test();
    }

    static Suite Test(string name, string what, params Action[] tests)
    {
        return new Suite { Name = name, What = what, Run = args => { for (int i = 0; i < tests.Length; i++) Seeded(name + "#" + i, tests[i]); } };
    }

    static Suite Report(string name, string what, Action<string[]> run) => new Suite { Name = name, What = what, Report = true, Run = run };

    static int Arg(string[] args, int i, int fallback) => args.Length > i ? int.Parse(args[i]) : fallback;

    // The feature suite, in its long-standing order.
    static readonly Action[] FeatureTests =
    {
        MusicTest, () => ParryTest(Dt), () => SessionTest(Dt), () => ParryUpgradeTest(Dt), () => AimTest(Dt), () => SpinnerTest(Dt), () => ShielderTest(Dt), () => DevToolsTest(Dt),
        () => MasteryTest(Dt), () => BudgetHomingTest(Dt), () => BossLabTest(Dt), () => SingularityTest(Dt), () => KnightTest(Dt), () => BearTotemTest(Dt),
        () => WardenTest(Dt), () => QueenTest(Dt), () => ReapersTest(Dt), () => BossSoundTest(Dt), () => WeaponExtraTest(Dt), () => KeybindTest(Dt), () => RevolverReloadTest(Dt), () => BowTest(Dt),
        () => PracticeTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt), () => ShowcaseTest(Dt), () => ClassAbilityTest(Dt), () => NinjaTest(Dt),
        () => BloodMageTest(Dt), TextOverridesTest, () => VeinBeamTest(Dt),
    };

    static void RunFeatures()
    {
        for (int i = 0; i < FeatureTests.Length; i++)
            Seeded("features#" + i, FeatureTests[i]);
    }

    static void RunCore(string[] args) => Seeded("core", () => CoreMatchTest(Arg(args, 0, 1), Arg(args, 1, 60)));

    static readonly Suite[] Suites =
    {
        new Suite { Name = "core", What = "the core match over TCP (sync, joining, restarts) and the core sweeps; the default", Run = RunCore },
        new Suite { Name = "features", What = "every feature test (parry, aim, classes, bosses, practice, paths...); run with core before committing", Run = args => RunFeatures() },
        new Suite { Name = "all", What = "core, then features", Run = args => { RunCore(args); RunFeatures(); } },
        // Single systems.
        Test("parry", "the parry and its upgrades", () => ParryTest(Dt), () => ParryUpgradeTest(Dt)),
        Test("aim", "aim precision, weapon range, the Frost Lance", () => AimTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt)),
        Test("extra", "extra projectiles upgrade every weapon its own way", () => WeaponExtraTest(Dt)),
        Test("revolver", "the Revolver", () => RevolverReloadTest(Dt)),
        Test("bow", "the Mender's Bow", () => BowTest(Dt)),
        Test("bear", "the Bear Totem", () => BearTotemTest(Dt)),
        Test("budget", "the enemy bullet budget and Homing Rounds", () => BudgetHomingTest(Dt)),
        Test("keys", "bonus abilities on Q/E/Shift", () => KeybindTest(Dt)),
        Test("mastery", "ability mastery", () => MasteryTest(Dt)),
        Test("classcards", "class ability cards", () => ClassAbilityTest(Dt)),
        Test("mods", "weapon mods and boss rerolls", () => WeaponModTest(Dt), () => BossRerollScenario(Dt)),
        Test("ults", "every ultimate over the network", () => UltimateSweep(Dt)),
        Test("showcase", "the lobby's loadout preview", () => ShowcaseTest(Dt)),
        Test("knight", "the Knight", () => KnightTest(Dt)),
        Test("ninja", "the Ninja", () => NinjaTest(Dt)),
        Test("blood", "the Blood Mage", () => VeinBeamTest(Dt), () => BloodMageTest(Dt)),
        Test("melee", "early melee rushers", () => MeleeTest(Dt)),
        Test("late", "waves 11-13: new enemies and elites", () => LateGameEnemies(Dt)),
        Test("spinner", "Spinners", () => SpinnerTest(Dt)),
        Test("shielder", "Shielders' shields wearing down", () => ShielderTest(Dt)),
        Test("warden", "the Warden", () => WardenTest(Dt)),
        Test("queen", "the Hive Queen", () => QueenTest(Dt)),
        Test("reapers", "the Twin Reapers", () => ReapersTest(Dt)),
        Test("singularity", "the Singularity", () => SingularityTest(Dt)),
        Test("bosssound", "boss sound cues", () => BossSoundTest(Dt)),
        Test("bosses", "every late boss's mechanics, victory and endless", () => BossMechanics(Dt)),
        Test("exploit", "parking a blocker on a boss", () => ExploitTest(Dt)),
        Test("practice", "the Practice Range (with the lobby, modifiers, paths and the Bazaar)", () => PracticeTest(Dt)),
        Test("paths", "divergent paths", () => PathsTest(Dt), () => PathHazardsTest(Dt), () => PathTwistsTest(Dt), () => PathArenaRulesTest(Dt)),
        Test("bazaar", "the Bazaar", () => BazaarTest(Dt)),
        Test("three", "three players", () => ThreePlayerTest(Dt)),
        Test("wrap", "wrapping round the arena's edges", () => WrapTest(Dt)),
        Test("ready", "ready-up in the lobby", () => ReadyTest(Dt)),
        Test("dev", "the F1 dev tools", () => DevToolsTest(Dt)),
        Test("textedit", "the dev text editor's overrides", TextOverridesTest),
        Test("labtest", "the Boss Lab", () => BossLabTest(Dt)),
        new Suite { Name = "session", What = "session recording (run.sh 1 30 session [copy-to])", Run = args =>
            Seeded("session", () => { var path = SessionTest(Dt); if (args.Length > 3 && path != null) File.Copy(path, args[3], true); }) },
        // Reports and debug tools (no pass/fail).
        Report("ttk", "boss time-to-kill over a full run (run.sh 1 1 ttk [last wave])", args => BossTtk(Dt, Arg(args, 3, 50))),
        Report("lab", "Boss Lab batch (run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat])",
            args => LabRun(int.Parse(args[3]), (BotSkill)int.Parse(args[4]), int.Parse(args[5]), args.Skip(6).ToArray())),
        Report("levels", "the level a team of Good bots has at each boss", args => LevelReport(Dt)),
        Report("bulletcount", "enemy bullets on screen through a wave", args => BulletCountReport(Dt)),
        Report("dps", "every weapon's damage per second", args => WeaponDpsReport(Dt)),
        Report("laserdebug", "the Singularity's lasers, step by step", args => LaserDebug()),
    };

    static int Main(string[] args)
    {
        string name = args.Length > 2 ? args[2] : args.Length == 1 && args[0] == "list" ? "list" : "core";
        if (name == "music")
            name = "features";  // its old name (it began as the music test)
        if (name == "list")
        {
            foreach (var s in Suites)
                Console.WriteLine("  {0,-12} {1}{2}", s.Name, s.Report ? "(report) " : "", s.What);
            return 0;
        }
        var suite = Suites.FirstOrDefault(s => s.Name == name);
        if (suite == null)
        {
            Console.WriteLine("No suite called \"" + name + "\". Try: run.sh list");
            return 2;
        }
        suite.Run(args);
        if (suite.Report)
            return 0;
        Console.WriteLine(failures == 0 ? "ALL PASSED" : failures + " FAILED");
        return failures == 0 ? 0 : 1;
    }
}

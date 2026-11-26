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
        if (!domesAllowed && s.IsHost)  // a bot's Fortress would wall off the boss and make everyone untouchable in tests that count on being hit
            foreach (var q in s.Game.Players.Values)
            {
                q.DomeT = 0f;
                var ult = q.Slots[Upgrades.UltSlot];
                if (ult.Id == AbilityId.Fortress)
                {
                    ult.Charges = 0;
                    ult.Timer = 99f;
                }
            }
        s.Game.FixedStep(dt, new PlayerInput { AimAt = aimOverride ?? new V2(640, 360) });
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

    static V2? aimOverride;  // where every player's input aims, when a test needs a facing (null: the usual point)
    static bool clientSawOrbs;

    static int nextPort = Environment.GetEnvironmentVariable("PORTBASE") != null ? int.Parse(Environment.GetEnvironmentVariable("PORTBASE")) : 7810;  // PORTBASE=n: parallel runs (BalanceGrid) each take their own ports

    // Shared setup for host + friend tests over localhost.
    // HostAs: host a run on `port` with `s` (pass `new Session { AutoStart = 2, StartWave = 3 }` etc.).
    static Session HostAs(Session s, int port, bool bot, string name, Role role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
    {
        if (bot)
            s.Game.Bot = true;
        if (!s.Host(port, name, role, weapon, ability, kit))
            Console.WriteLine("  HOST FAILED on port " + port + ": " + s.Status + " (another SimTest run using the same ports? give it PORTBASE=n)");
        s.Diff.Set(DiffKnob.Campaign, 0f);  // (the tests expect the usual random waves; CampaignTest turns it on itself)
        return s;
    }

    // JoinAs: a friend joining the host on `port`.
    static Session JoinAs(int port, bool bot, string name, Role? role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
    {
        var s = new Session();
        if (bot)
            s.Game.Bot = true;
        s.Join("127.0.0.1", port, name, role, weapon, ability, kit);
        return s;
    }

    // Step both games until the client is in the run (at most 300 steps).
    static void WaitForGame(Session host, Session client, float dt)
    {
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Nap(2); }
    }

    // Step both sessions until Practice is up on the client (it plays behind the lobby: the screen stays the lobby).
    static void WaitForPractice(Session host, Session client, float dt)
    {
        for (int i = 0; i < 300 && !client.Game.Practicing; i++) { Step(host, dt); Step(client, dt); Nap(2); }
    }

    // Poll both sessions until the client is in the lobby (at most 300 polls).
    static void WaitForLobby(Session host, Session client)
    {
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Nap(2); }
    }

    // Finish every push still sliding (Knock.cs: pushes travel over Game.KnockTime), for checks made right after one.
    static void SettleKnocks(Game g)
    {
        var tick = typeof(Game).GetMethod("TickKnock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        foreach (var e in g.Enemies.Values)
            tick.Invoke(g, new object[] { e, 1f });
    }

    // Poll both sessions `times` times, letting messages cross.
    static void PollBoth(Session host, Session client, int times = 40)
    {
        for (int i = 0; i < times; i++) { host.Poll(); client.Poll(); Nap(2); }
    }

    // Start a bot match at a given wave with both players buffed so they survive a while.
    static Session[] StartBuffed(float dt, int wave, int damageStacks, Action<Session> setup = null)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = wave % 5 == 0 ? -wave : wave };  // (a multiple of 5 starts at its boss)
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        host.Host(port, "Host", Role.Tank, 0);
        host.Diff.Set(DiffKnob.Campaign, 0f);  // (the usual random waves; CampaignTest turns it on itself)
        host.Diff.Set(DiffKnob.EnemySpeed, 1f);  // (the fights are time-limited; keep their pace independent of the default speed preset)
        setup?.Invoke(host);  // (e.g. another mechanics tier: BossStagesTest)
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", port, "Friend", Role.Dps, 0);
        WaitForGame(host, client, dt);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        foreach (int id in new[] { 1, 2 })
        {
            for (int k = 0; k < 20; k++) host.Game.GrantUpgrade(id, U("Fortitude"));
            for (int k = 0; k < 10; k++) host.Game.GrantUpgrade(id, U("Regeneration"));
            for (int k = 0; k < damageStacks; k++) host.Game.GrantUpgrade(id, U("Hollow Points"));
            // (Repeat copies diminish now, Upgrades.CopyStrength: the test buff sets the stats those copies used to give.)
            Action<string, float> set = (field, v) => host.Game.DevSetStat(id, Array.FindIndex(Game.StatFields, f => f.Name == field), v);
            set("BonusHp", 20 * 40f);
            set("Regen", 10 * 3f);
            if (damageStacks > 0) set("DmgMult", 1f + 0.2f * damageStacks);
        }
        return new[] { host, client };
    }

    /// Wait a moment: over real TCP (SIMTEST_TCP=1) a real sleep, for the network; over loopback (which delivers on the next
    /// Poll) the session's test clock moves on instead (Session.TestClock: the Hangout room's send rate reads it).
    static double napClock;
    static void Nap(int ms) { if (NetPeer.Loopback) napClock += ms / 1000.0; else Thread.Sleep(ms); }

    static bool domesAllowed;  // only the dome test leaves Fortress's dome standing
    static void Run(Session[] s, float dt, float seconds, Func<bool> stop = null, Action each = null)
    {
        for (int i = 0; i < seconds * 60; i++)
        {
            foreach (var x in s)
                Step(x, dt);
            if (i % 2 == 0)
                Nap(1);
            if (each != null)
                each();
            if (stop != null && stop())
                break;
        }
        for (int k = 0; s.Length > 1 && k < 5; k++) { Nap(2); s[1].Poll(); }
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

    /// Tests that empty the spawn queue to get a quiet arena empty the squads waiting to come in too (Variety.cs).
    static void ClearSquads(Game g) =>
        ((List<SquadDef>)typeof(Game).GetField("squadQueue", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g)).Clear();

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

    /// The random rolls a test named `name` starts from (SEED=random or SEED=<salt> change them).
    static void SeedAs(string name)
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
    }

    /// Run one test with its own repeatable random rolls (the seed comes from the test's name).
    static void Seeded(string name, Action test)
    {
        SeedAs(name);
        Game.PropsByDefault = false;  // props only where a test asks for them (the core match, the props test)
        Game.LayoutsByDefault = false;  // floor plans too (the layouts test)
        Game.ChallengesByDefault = false;  // challenge rooms too (the challenges test)
        Game.LongRunDefault = false;  // the 50-wave track (the longrun suite turns the 100-wave Campaign on)
        Session.QuickDefault = false;  // hosts open on the Campaign (Quick Play has its own suite, quick)
        var watch = System.Diagnostics.Stopwatch.StartNew();
        test();
        if (Environment.GetEnvironmentVariable("TIMES") == "1")  // TIMES=1 run.sh ...: how long each test took
            Console.WriteLine("  [time] {0}: {1:0.0} s", name, watch.Elapsed.TotalSeconds);
    }

    static Suite Test(string name, string what, params Action[] tests)
    {
        // ONLY=n run.sh ...: just the suite's n-th test (from 0), with the same seed it gets in a full run.
        return new Suite { Name = name, What = what, Run = args =>
        {
            string only = Environment.GetEnvironmentVariable("ONLY");
            for (int i = 0; i < tests.Length; i++)
                if (string.IsNullOrEmpty(only) || only == i.ToString())
                    Seeded(name + "#" + i, tests[i]);
        } };
    }

    static Suite Report(string name, string what, Action<string[]> run) => new Suite { Name = name, What = what, Report = true, Run = run };

    static int Arg(string[] args, int i, int fallback) => args.Length > i ? int.Parse(args[i]) : fallback;

    // The feature suite, in its long-standing order.
    static readonly Action[] FeatureTests =
    {
        MusicTest, () => ParryTest(Dt), () => { } /* (was the session recording) */, () => ParryUpgradeTest(Dt), () => AimTest(Dt), () => SpinnerTest(Dt), () => ShielderTest(Dt), () => DevToolsTest(Dt),
        () => MasteryTest(Dt), () => BudgetHomingTest(Dt), () => BossLabTest(Dt), () => SingularityTest(Dt), () => ColossusTest(Dt), () => LichTest(Dt), () => KnightTest(Dt), () => BearTotemTest(Dt),
        () => WardenTest(Dt), () => QueenTest(Dt), () => ReapersTest(Dt), () => BossSoundTest(Dt), () => WeaponExtraTest(Dt), () => RepulsionTest(Dt), () => QuarterstaffTest(Dt), () => TankKnightTest(Dt), () => KeybindTest(Dt), () => RevolverReloadTest(Dt), () => BowTest(Dt),
        () => MoreModifiersTest(Dt), () => DuelistTest(Dt), () => HealerSoloTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt), () => WeaponSoundsTest(Dt), () => ShowcaseTest(Dt), () => ClassAbilityTest(Dt), () => NinjaTest(Dt),
        () => HemomancerTest(Dt), () => BloodLashTest(Dt), () => BloodBlastTest(Dt), () => { } /* (was the text editor) */, () => VeinBeamTest(Dt), () => WardenLootTest(Dt), () => LeechGraspTest(Dt), () => F1BotTest(Dt), () => FlamethrowerTest(Dt), () => HealerKitTest(Dt), () => RadiantOrbTest(Dt), () => StormHammerTest(Dt), () => GolemStaffTest(Dt), () => BoneGuardTest(Dt), () => LightConeTest(Dt), () => SanguineOrbTest(Dt), () => FireballArcTest(Dt), () => ScatterFlakBloodTest(Dt), () => TwinSmgTest(Dt), () => PrismWolfRailTest(Dt), () => MissilesRiotBladesTest(Dt), () => FiveMoreTest(Dt), () => FourMoreTest(Dt), () => LightbringerTest(Dt), () => LanceTest(Dt), () => HealerFiveTest(Dt), () => LastFourTest(Dt), () => HealerWardsTest(Dt), () => HeraldTest(Dt), () => DevourerTest(Dt), () => EclipseTest(Dt), () => VarietyTest(Dt), () => SavesTest(Dt), () => ProfileTest(Dt), DiagTest, () => UpgradeFixesTest(Dt), () => QueenLootTest(Dt), () => ReaperLootTest(Dt), () => BossPowerTest(Dt), () => EnemyNumbersTest(Dt), () => DifficultyTest(Dt), () => RuleModifiersTest(Dt), () => ReadabilityTest(Dt), () => RunStatsLiveTest(Dt), () => WaveReadyTest(Dt), () => PropsTest(Dt), () => ChallengesTest(Dt), ClassCompletenessTest, () => ApproachesTest(Dt), CampaignTest, () => HuntTest(Dt), () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt), () => ZombieTest(Dt), () => ClearGhostsTest(Dt), () => HealingRainTest(Dt), () => GustGateTest(Dt), () => LumenWardTest(Dt), () => FireflyRootTest(Dt), () => NimbusChalkTest(Dt), () => TsunamiTest(Dt), () => ChargedTest(Dt), () => SteamTest(Dt), () => WindTest(Dt), () => ChillTest(Dt), () => ElementGunsTest(Dt), () => HolyCrossTest(Dt), () => CavalryTest(Dt), () => WarhornVolleyTest(Dt), () => TwinGrenadeTest(Dt), () => AirstrikeTest(Dt), MusicLibraryTest, CustomMusicTest, ProjectClipsTest, () => DomeTest(Dt), () => AlphaCallTest(Dt), () => WolfPackTest(Dt), () => AegisTest(Dt), () => GolemWallTest(Dt), () => SanctumTest(Dt), () => HydraTest(Dt), () => BurrowerTest(Dt), () => WorldTreeTest(Dt), () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); PartyBotCampaignTest(Dt); }, () => BotTeamplayTest(Dt), () => BotParryTest(Dt), () => BotAltTest(Dt), () => BotPicksTest(Dt), () => BotShopTest(Dt), () => DodgeTest(Dt), () => HarvestTest(Dt), () => TitanTest(Dt), () => RainTest(Dt), () => TitheTest(Dt), () => RodTest(Dt), () => FloodTest(Dt), () => BloodbathTest(Dt), () => JuggernautTest(Dt), () => StoneColossusTest(Dt), () => BloodGolemTest(Dt), () => SpiritAvatarTest(Dt), () => VortexTest(Dt), () => BloodFreezeTest(Dt), () => ForcePactTest(Dt), () => QuarterstaffStacksTest(Dt), () => StormHammerSlamTest(Dt), () => MomentumTest(Dt),
        () => PhalanxTest(Dt), () => PoisonDartTest(Dt), () => LagChartTest(Dt), () => BossSlotTest(Dt), () => TankAltTest(Dt), () => { } /* (was the Dev Arena) */, () => { } /* (was the session sources) */, () => AuditFixesTest(Dt), () => FavoritesTest(Dt), () => GadgetTest(Dt), () => CauterizeTest(Dt), () => EngineerTest(Dt), () => PickupsTest(Dt), () => RemoteChestsTest(Dt), () => ShowcaseAltsTest(Dt), () => DevSpawnerTest(Dt), () => NetAuditFixesTest(Dt),  // (new tests go last: each test's seed comes from its place, so inserting one reseeds every test after it)
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
        new Suite { Name = "features", What = "every feature test (parry, aim, classes, bosses, paths...); run with core before committing", Run = args => RunFeatures() },
        new Suite { Name = "all", What = "core, then features", Run = args => { RunCore(args); RunFeatures(); } },
        // Single systems.
        Test("audit", "Nov 2026 audit fixes: slows end on time, piercing shots hit overlapping enemies once, boss coins carry all their gold, Stop clears per-run lists, bad client input and resent dodges are ignored, cut-short run saves are refused", () => AuditFixesTest(Dt)),
        Test("showcasealts", "the lobby preview presses every weapon's Alt (ShowcaseAlt.cs): a run with it shows something a run without never does", () => ShowcaseAltsTest(Dt)),
        Test("classes", "every class is complete: name, blurb, icon, look, weapons that fire, abilities that cast (run first when adding a class)", ClassCompletenessTest),
        Test("approaches", "boss approaches: the themed share, waves built with it, the Riot Shield (bash, knockback, shield), the Gas Grenadier, the Warden's pair", () => ApproachesTest(Dt)),
        Test("campaign", "the campaign buildup: every wave themed, the last boss fading out, an older pool, the next boss building up", CampaignTest),
        Test("parry", "the parry and its upgrades", () => ParryTest(Dt), () => ParryUpgradeTest(Dt)),
        Test("golemwall", "Golem Wall: three big golems, no timer, worn by every shot, broken at zero, replaced by a recast", () => GolemWallTest(Dt)),
        Test("aegis", "Aeroaegis: the vortex goes to the player nearest the cursor, enemies stay out", () => AegisTest(Dt)),
        Test("sanctum", "Sanctum: seven healing pulses, the last bigger, each ally healed as the ring reaches them", () => SanctumTest(Dt)),
        Test("alphacall", "Alpha Call: Blood Scent stacks build, lunge at 5, replicate, and clear", () => AlphaCallTest(Dt)),
        Test("wolfpack", "Wolf Whistle pack: grows with kills to 5, soaks 10% of your damage, Alt merges an alpha", () => WolfPackTest(Dt)),
        Test("dome", "Fortress's dome: enemies and their shots stay out, player shots cross", () => DomeTest(Dt)),
        Test("aim", "aim precision, weapon range, the Frost Lance", () => AimTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt)),
        Test("weaponsounds", "the Repulsion Cannon's blast and Chain Light's cone and arcs make their own sounds", () => WeaponSoundsTest(Dt)),
        Test("extra", "extra projectiles upgrade every weapon its own way", () => WeaponExtraTest(Dt)),
        Test("storm", "the Storm Hammer", () => StormHammerTest(Dt)),
        Test("stormslam", "the Storm Hammer's Alt slam", () => StormHammerSlamTest(Dt)),
        Test("golem", "the Golem Staff's boulder", () => GolemStaffTest(Dt)),
        Test("boneguard", "the Bone Guard's cages", () => BoneGuardTest(Dt)),
        Test("lightcone", "Chain Light's cone", () => LightConeTest(Dt)),
        Test("sanguine", "the Sanguine Orb", () => SanguineOrbTest(Dt)),
        Test("fireball", "the Fireball and Arc Lightning", () => FireballArcTest(Dt)),
        Test("scatterflak", "the Scattergun, Flak Cannon and the Needle Swarm's bleed", () => ScatterFlakBloodTest(Dt)),
        Test("twinsmg", "the Twin Rifle and Burst SMG", () => TwinSmgTest(Dt)),
        Test("prismwolfrail", "the Prism Beam, Wolf Whistle and Railgun", () => PrismWolfRailTest(Dt)),
        Test("missilesriot", "Ice Shards, the Riot Repeater and Twin Blades", () => MissilesRiotBladesTest(Dt)),
        Test("bosspowertest", "bosses get more health for a party far above a typical build", () => BossPowerTest(Dt)),
        Test("reaperloot", "the Twin Reapers' legendaries: Twin Scythes, Final Reap, Scythe Throw", () => ReaperLootTest(Dt)),
        Test("queenloot", "the Hive Queen's legendaries: Hive Drones, Royal Dive, Bomber Escort", () => QueenLootTest(Dt)),
        Test("captain", "the Riot Captain: its shield, the charge into a wall, the whistle, its attacks, its room over the network", () => RiotCaptainTest(Dt)),
        Test("guardswing", "the riot Guard swings its baton fast, forehand then backhand, only when you're close and without stopping", () => GuardSwingTest(Dt)),
        Test("thugpunch", "the Thug plants and winds up a short punch when you're close; it lands in its wedge and a sidestep dodges it", () => ThugPunchTest(Dt)),
        Test("guardrally", "a Rioter near a Guard runs faster than one alone", () => GuardRallyTest(Dt)),
        Test("riotercrowd", "Rioters touching you slow your walk, compounding: barely with one, a lot with a pile", () => RioterCrowdTest(Dt)),
        Test("rioterbottle", "from wave 2 some Rioters carry one bottle and throw it at a player 110-300 px away", () => RioterBottleTest(Dt)),
        Test("security", "the Security Guard walks while it locks on, keeps its strafe side and aims straight at you", () => SecurityTest(Dt)),
        Test("reapercult", "the Twin Reapers' cult: Bound Wraiths' tether and revive, Scythe Wraith's reap, Lamplighter, Mourner", () => ReaperCultTest(Dt)),
        Test("turnkey", "the Warden faction's Turnkey: its wave-3 debut, running, traps, Cell Doors and their Rioters", () => TurnkeyTest(Dt)),
        Test("hitfx", "hit feedback: fire alone doesn't flash an enemy, steady fire softens the flash, Explosive Rounds' blasts are thinned", () => HitFxTest(Dt)),
        Test("upgradefix", "Velocity Rounds land airbursts at the cursor; Incendiary Rounds add up", () => UpgradeFixesTest(Dt)),
        Test("fivemore", "Acupuncture Needles, Greatsword, Kunai, Wisp Lantern and Imp Caller", () => FiveMoreTest(Dt)),
        Test("fourmore", "Shuriken, Blood Brawler and Treant Rod", () => FourMoreTest(Dt)),
        Test("lightbringer", "the Lightbringer: Dawn, Mercy, Noon, the light gauge, a Radiant Noon and the Sunlance", () => LightbringerTest(Dt)),
        Test("bulwark", "the Bulwark Barrier: the half guard, Rim Cut, Backhand, Boss Slam, the Shield Throw, the parry reset, the Brace soaking shots and melee into Ram, the Shield Bash and its shards", () => BulwarkTest(Dt)),
        Test("lance", "the Lance: Jab, Second Jab, Wide Sweep, Breaching Thrust, Impact and Hook & Haul", () => LanceTest(Dt)),
        Test("healerfive", "Censer, Spirit Fan, Ofuda, Sprite Staff and Totem Staff", () => HealerFiveTest(Dt)),
        Test("gustgate", "Gust Gate: crossing the ring boosts speed 80% for 3 s, enemies inside pushed out, ends on time, mastered wider", () => GustGateTest(Dt)),
        Test("healingrain", "Healing Rain: heals allies inside, slows enemies, ends on time, mastered lasts 10 s", () => HealingRainTest(Dt)),
        Test("lumenward", "Lumen Ward: a cone of light that follows your aim, heals inside and burns bullets in it or touching it", () => LumenWardTest(Dt)),
        Test("wards", "Spirit Ward and Healing Mist", () => HealerWardsTest(Dt)),
        Test("lastfour", "Gore Mortar, Transfusion Darts, Rapier (Tempo, Flourish) and the Katana (stance combos, Momentum Burst, caltrops)", () => LastFourTest(Dt)),
        Test("repulsion", "the Repulsion Cannon", () => RepulsionTest(Dt)),
        Test("quarterstaff", "the Quarterstaff", () => { QuarterstaffTest(Dt); QuarterstaffStacksTest(Dt); }),
        Test("greatsword", "the Greatsword's ten-step chain and Moonwheel", () => GreatswordTest(Dt)),
        Test("knuckles", "the Arc Knuckles: the Brawl stance and its click wheel, Wall Slam, parry reset, Counter Hammer, the Rocket stance", () => ArcKnucklesTest(Dt)),
        Test("tankknight", "the Warhammer's charged slam, the Halberd's Reaper's Wheel and Set the Pike", () => TankKnightTest(Dt)),
        Test("revolver", "the Revolver's Gunslinger stance: clicks alternate fire and hammer pull", () => RevolverReloadTest(Dt)),
        Test("bow", "the Mender's Bow", () => BowTest(Dt)),
        Test("bear", "the Bear Totem", () => BearTotemTest(Dt)),
        Test("budget", "the enemy bullet budget and Homing Rounds", () => BudgetHomingTest(Dt)),
        Test("keys", "2nd–4th abilities on 2nd, 3rd or 4th", () => KeybindTest(Dt)),
        Test("mastery", "ability mastery", () => MasteryTest(Dt)),
        Test("classcards", "class ability cards", () => ClassAbilityTest(Dt)),
        Test("milestone", "milestone upgrades: the pool, every 10 levels, the Milestone Compass", () => MilestoneTest(Dt)),
        Test("mods", "weapon mods and boss rerolls", () => WeaponModTest(Dt), () => BossRerollScenario(Dt)),
        Test("offers", "every card offer reads the weapon: weapon-only mods, legendaries and milestone cards", () => OfferWeaponTest(Dt)),
        Test("cardfilters", "class (Kit) and weapon-list cards: who is offered them, the Wildcard's classes", () => CardFilterTest()),
        Test("classcardlists", "each class's own upgrade card list: only its cards, all offered, unique names", () => ClassCardListsTest()),
        Test("abilitycards", "the Rare cards for the Elementalist and Summoner 1st abilities and the Medkit (Docs/UpgradePools.md step 5)", () => AbilityCardsTest(Dt)),
        Test("cardids", "card ids over the network: 2 bytes, ids past 255 and empty offer slots", () => CardIdWireTest(Dt)),
        Test("ultimates", "every ultimate over the network", () => UltimateSweep(Dt)),
        Test("showcase", "the lobby's loadout preview", () => ShowcaseTest(Dt)),
        Test("knight", "the Knight", () => KnightTest(Dt)),
        Test("twinblades", "the Twin Blades: each step's shove", () => TwinBladesTest(Dt)),
        Test("knightdps", "the DPS Knight's Gauntlet, Bladed Dive and Drawbridge", () => KnightDpsTest(Dt)),
        Test("caltroprun", "Caltrop Run, the DPS Knight's 3rd choice: the 20% sprint, a caltrop per 34 px moved (none standing), 16 at most, landing behind you, the bite (12, bleed, 40% slow; heavies 20%; once per enemy), breaking after two, gone after 6 s, the cooldown waiting, the other screen, Barbed Iron and mastery", () => CaltropRunTest(Dt)),
        Test("basicfaction", "the basic faction: the eight plain enemies left, Quick Play's build-up for a boss with no faction, nothing retired in any wave", BasicFactionTest),
        Test("court", "the Duelist's faction (all four steps): the Fencer's hop, the Blade Dancer's blades, the Parrier's parries, the Second's daggers, the Marshal's circle, the Gallant's salute, the Harlequin's crossing, the Mummer's copies, the Ring of Honour, the ten lessons", () => CourtTest(Dt)),
        Test("quarry", "the Colossus's faction, step 1: Rubble, the Quarryman and the Stomper's push ring", () => QuarryTest(Dt)),
        Test("larder", "the Devourer's faction: all eight Larder enemies and their tricks, the Feast, the Long Campaign's ten lessons", () => LarderTest(Dt)),
        Test("hive", "the Hive Queen's faction: eight enemies that play like the Warden's, honey instead of gas, the venom dart, and the chart in waves 11-20", () => HiveTest(Dt)),
        Test("infall", "the Singularity's faction, steps 1-3: all eight enemies (the Graviton's well, the Collapsar's implosion, the Lens's face, the White Dwarf's pulse), waves 16-19", () => InfallTest(Dt)),
        Test("penumbral", "the Eclipse's faction (all four steps): Ember sparks, the Corona Priest's gap, the Heliograph's lane, the Moonward's moon, the Gnomon's shadow, the Dusk Lobber's pool, the Sickle's crescent, the Nova's fuse, Last Light, the lessons, waves 46-49", () => PenumbralTest(Dt)),
        Test("squall", "the Storm Herald's faction: all eight (the Dust Devil's shove, the Gunner's curve, strikes, the Grounder's clap, the Pylon arc, the Lineman's mark, the Thunderhead's row, a wave's Ball Lightning), the Storm front set piece and the lessons, waves 36-39", () => SquallTest(Dt)),
        Test("aitrace", "the enemy behaviour refactor's safety net (Docs/EnemyBehaviours.md): every non-boss enemy alone against a god-mode host for 12 s, each frame hashed and compared with Golden/aitrace.txt (AITRACE=update rewrites it)", () => AiTraceTest()),
        Test("spawndump", "the spawner refactor's safety net: the first waves of a few modes played headless, every arrival compared with Golden/spawndump.txt (WAVEDUMP=update rewrites it)", () => SpawnDumpTest()),
        Test("wavedump", "the refactor safety net: every mode's waves built on fixed seeds, compared with Golden/wavedump.txt (WAVEDUMP=update rewrites it)", () => WaveDumpTest()),
        Test("bossslot", "the boss between waves: it follows every fifth regular wave and keeps its number; paths, saves and the Lab start", () => BossSlotTest(Dt)),
        Test("phalanx", "Bone Phalanx (the Tank Summoner's second ultimate) and the lobby's Ultimate pick", () => PhalanxTest(Dt)),
        Test("ninja", "the Ninja", () => NinjaTest(Dt)),
        Test("blood", "the Hemomancer", () => VeinBeamTest(Dt), () => LeechGraspTest(Dt), () => HemomancerTest(Dt)),
        Test("bloodblast", "the Blood Blast: the six-step chain, its health costs, blood stacks and the kill heal", () => BloodBlastTest(Dt)),
        Test("bloodlash", "the Blood Lash chain combo: Crack, Backhand, Snap, Reap, the chains and Martyr's Leash", () => BloodLashTest(Dt)),
        Test("melee", "early melee rushers", () => MeleeTest(Dt)),
        Test("snapshot", "run snapshots (F6 / F7): a loaded snapshot puts the run back and plays on tick for tick", () => RunSnapshotTest(Dt)),
        Test("brute", "Brute enrage", () => BruteEnrageTest()),
        Test("trickle", "Campaign handover: fade and trickle", () => TrickleTest()),
        Test("meleesounds", "melee rushers' sounds: quieter with distance, distinct deaths that merge", () => MeleeSoundsTest(Dt)),
        Test("late", "waves 11-13: new enemies and elites", () => LateGameEnemies(Dt)),
        Test("netaudit", "network fixes from the late Nov 2026 audit: 255+ zones, shots keep their look through a pause, a leaver vanishes on every screen", () => NetAuditFixesTest(Dt)),
        Test("spinner", "Spinners", () => SpinnerTest(Dt)),
        Test("shielder", "Shielders' shields wearing down", () => ShielderTest(Dt)),
        Test("warden", "the Warden", () => WardenTest(Dt)),
        Test("bossrules", "boss numbers by mechanics tier (BossRules.cs): summons, escorts, rest pace", () => BossRulesTest(Dt)),
        Test("stages", "boss stage tables (BossStages.cs): Normal as balanced, Easy without the last stands, fights following their tier", () => BossStagesTest(Dt)),
        Test("holdingcell", "the Warden's net and Holding Cells: guards, the padlock, abilities, the fight pausing, next door, downed inside", () => HoldingCellTest(Dt)),
        Test("botdodge", "smart bots dodge walls of shots and sprint to cross the arena, without overusing either", () => BotDodgeTest(Dt)),
        Test("botgate", "smart bots walk through the Warden's gates' doors", () => BotWardenGateTest(Dt)),
        Test("wardenloot", "the Warden's legendaries: Stun Gun, Warden's Guard and Searchlight", () => WardenLootTest(Dt)),
        Test("flame", "the Flamethrower", () => FlamethrowerTest(Dt)),
        Test("radiant", "the Radiant Orb", () => RadiantOrbTest(Dt)),
        Test("pact", "The Tank Summoner Force Pact ability", () => ForcePactTest(Dt)),
        Test("hydra", "The Summoner DPS Hydra ability", () => HydraTest(Dt)),
        Test("burrower", "Earthquake: a slam, then a Burrower that tunnels through the pack biting each enemy once", () => BurrowerTest(Dt)),
        Test("titan", "Titan: a colossus that rises, winds up and slams, blocks bullets, then crumbles", () => TitanTest(Dt)),
        Test("worldtree", "World Tree: a 100 px tree for 15 s that sends healing butterflies to each ally", () => WorldTreeTest(Dt)),
        Test("healerkit", "Soul Lantern and the Deployable Medkit", () => HealerKitTest(Dt)),
        Test("queen", "the Hive Queen", () => QueenTest(Dt)),
        Test("reapers", "the Twin Reapers", () => ReapersTest(Dt)),
        Test("duelist", "the Duelist's three phases and last stand", () => DuelistTest(Dt)),
        Test("lich", "the Lich's three phases and last stand", () => LichTest(Dt)),
        Test("singularity", "the Singularity's three phases and last stand", () => SingularityTest(Dt), () => SingularityCageTest(Dt)),
        Test("herald", "the Storm Herald's three phases and last stand", () => HeraldTest(Dt)),
        Test("eclipse", "the Eclipse's five phases and Totality", () => EclipseTest(Dt)),
        Test("variety", "elite traits and squads", () => VarietyTest(Dt)),
        Test("devourer", "the Devourer's three phases and last meal", () => DevourerTest(Dt)),
        Test("recentruns", "Records > Recent runs: the run's curve, its file, the profile's run log", () => RecentRunsTest(Dt)),
        Test("pacman", "the Pac-Man maze game: maze, steering, tunnel, dots and pellets, the four role ghosts, lives, levels, the ghost house, co-op", () => PacmanTest(Dt)),
        Test("corelayouts", "core layouts (Docs/Mockups/core-layouts.html): ten floor plans for the Grid core, door lanes and the gate floor clear, one a run, a new one each Campaign map, synced and saved", () => CoreLayoutsTest(Dt)),
        Test("mapreset", "the Campaign map resets after each boss: kits kept, half the spent vectors back, saved", () => MapResetTest(Dt)),
        Test("longrun", "the 100-wave Campaign: two legs a track wave, the boss after every tenth, half XP, saves and continuing, the party's level at the first boss", () => LongRunTest(Dt), () => LongRunPaceTest(Dt)),
        Test("wardencycle", "the Campaign's Warden cycle: one lesson a wave (Rioters, guards, shields, gas, tasers, squads, the Riot line) (one guaranteed star enemy each)", () => WardenCycleTest(Dt)),
        Test("checkpoints", "checkpoints and lives: a boss leaves a checkpoint, a wipe with a life left retries from it (score cut), Play again or no lives end the run", () => CheckpointsTest(Dt)),
        Test("variants", "enemy variants: the Rioter's Ball and Chain spawns in its place, slower, and the client sees it", () => EnemyVariantsTest(Dt)),
        Test("coredrops", "weapon variants: what runs and hunts pay in Cores, the glowing Core Carrier", () => CoreDropsTest(Dt)),
        Test("saves", "saved runs (continuing them, in co-op) and the profile; recovered save and file failures reach the log (Diag.cs)", () => SavesTest(Dt), () => ProfileTest(Dt), DiagTest),
        Test("favorites", "saved favourite loadouts: slots, names, the profile's fav lines, loading one over the network, perks", () => FavoritesTest(Dt)),
        Test("colossus", "the Colossus's three phases, stomp rings, plates and last stand", () => ColossusTest(Dt)),
        Test("bosssound", "boss sound cues", () => BossSoundTest(Dt)),
        Test("bosses", "every late boss's mechanics, victory and endless", () => BossMechanics(Dt)),
        Test("latewaves", "waves 25-50: each late boss beaten, victory and endless", () => LateWaves(Dt)),
        Test("exploit", "parking a blocker on a boss", () => ExploitTest(Dt)),
        Test("nimbuschalk", "the Nimbus (cloud, rain, strikes) and Spirit Chalk (ink, loops that pen enemies in)", () => NimbusChalkTest(Dt)),
        Test("tsunami", "Tsunami: the wave, wet, the slow, lightning arcing to every wet enemy, wearing off, the mastered return", () => TsunamiTest(Dt)),
        Test("charged", "Charged: lightning charges, charged enemies conduct, wet and charged ones short out", () => ChargedTest(Dt)),
        Test("steam", "Steam: wet and burning turns to steam, the cloud scalds and jams, refreshes, ends", () => SteamTest(Dt)),
        Test("wind", "Wind reactions: Wind Chill on wet enemies, Fan the Flames on burning ones, once a second", () => WindTest(Dt)),
        Test("chill", "Slowed vs Chilled: plain slows never freeze, ice slows and chills, the third freezes and lifts the slows", () => ChillTest(Dt)),
        Test("elementguns", "The Flamethrower's variants: the Water Gun splashes wet, the Gale Gun slices and slows", () => ElementGunsTest(Dt)),
        Test("holycross", "Consecrate's Holy Cross: it grows as it flies, heals and hastens allies, hurts, slows and knocks back enemies, hits each once, the mastered fan", () => HolyCrossTest(Dt)),
        Test("engineer", "the Engineer: one robot weapon a role (the forges gone), Drag Pylon (slows, one at a time, 320 px reach) and Breach (the blast; a room behind the Expanding arena's wall)", () => EngineerTest(Dt)),
        Test("arcanist", "the Arcanist: Dancing Blades (launch, dance, recall), Runic Array, Telekinetic Crush, Kinetic Field; Orbit Shards (cut, Shard Guard), Kinetic Grasp (hold, throw), Grand Sigil (rim freeze, release); Glyph Stylus heals, Arcane Tether, Sanctum Sigil", () => ArcanistTest(Dt)),
        Test("engbots", "the Engineer's robot weapons: a click summons the role's robot (sent at the enemy clicked), it shoots and draws enemies, rebuilt-able after 8 s; the Vector Blade's swipe, the Iron Fist's shove, the Stasis Gun's held field; the Medic Bot heals", () => EngineerBotsTest(Dt)),
        Test("cover", "Deployable Cover: the Tank Soldier's 4th choice, two charges, planted 64 px ahead, solid after 0.4 s, stops enemy shots on its plates and wears, keeps enemies off, three at most (a fourth folds the oldest), breaks, replicated, folds at wave end, mastered", () => DeployableCoverTest(Dt)),
        Test("decoy", "Decoy Dummy: the Healer Soldier's 4th choice, three charges, thrown to the cursor (280 px at most), up after 0.7 s, draws regular enemies within 240 px (bosses glance), shots and bites wear it, three at most, the pop's confetti blast, deflating, replicated, mastered, Double-Ply", () => DecoyDummyTest(Dt)),
        Test("graze", "Graze: an enemy shot skimming past a player (within 16 px of the hit circle) counts once and takes 0.3 s off their ultimate; far misses, hits, invulnerability and a second shot in the 0.2 s gap don't; a client's own game takes it off (Msg.Graze)", () => GrazeTest(Dt)),
        Test("sanctumglass", "Sanctum Glass: the Healer Knight's 4th choice; the pane turns enemy shots (red aimed too) into motes that heal the most hurt player, your shots pass, cap 120 then it shatters, Rose Window, mastered, replicated, going down", () => SanctumGlassTest(Dt)),
        Test("galewall", "Gale Wall: the Elementalist DPS's 4th choice; the lens catches enemy shots as charges (cap 40), a second press or the timer unleashes a cone of 40 + 6 per charge, mastered, replicated, going down", () => GaleWallTest(Dt)),
        Test("waterward", "Water Ward: the Healer Elementalist's 4th choice; 2 drops a cast fall then circle (max 10, no cast at 10), even spacing, splash (12 + wet 52 px), burst on a shot (wet 72 px, not red shots), mastered, replicated, going down", () => WaterWardTest(Dt)),
        Test("harvest", "Crimson Harvest: rips 20 and the blood of every enemy into a ball, a 5 s hold, a second press throws it for a blast that scales with the blood", () => HarvestTest(Dt)),
        Test("bloodfreeze", "Blood Freeze: 25% health, a charge then a 260 px wave that freezes every enemy it reaches for 5 s (bosses 3 s), no damage, allies untouched, Hungry Earth, mastered +25% damage", () => BloodFreezeTest(Dt)),
        Test("flood", "Flood, the Tank Elementalist's 4th choice: the 4th lobby key (only that class, clamped elsewhere), the stamp (10 damage, 60 px shove, heavies stay), a 190 px pool for 6 s, 40/20% slow, wet while in and 3 s after, lightning arcs, draining does nothing, Deluge and High Tide", () => FloodTest(Dt)),
        Test("deployturret", "Deployable Turret, the DPS Soldier's 4th choice: the 4th lobby key, a drop that roots you, no rounds while unfolding or without Fire, a lane that pierces 2, the spool-up, the overheat and the 160 px blast, folding quietly when you go down, mastery (14 s, pierce 4)", () => DeployTurretTest(Dt)),
        Test("cannoncall", "Cannon Call, the DPS Knight's 4th choice: the 4th lobby key, the 420 px mark, one effect a ball on both screens, 70 in the core and 35 to the edge after the fall, the shove and stun, half the cooldown back for 3 kills, mastery (three balls) and Siege Powder", () => CannonCallTest(Dt)),
        Test("thornbloom", "Thorn Bloom, the DPS Hemomancer's 4th choice: the 4th lobby key, the 12% cost, twelve needles on both screens landing on arrival (45 and a bleed stack, the first enemy only), the tendril chain (30, 20% less a jump, 3 jumps, never the same enemy twice), mastery (5 jumps) and Thornwork", () => ThornBloomTest(Dt)),
        Test("chakra", "Chakra Reserves, the Healer Ninja's 4th choice: the 4th lobby key, the heal (40 + 15% missing), the Chakra Surge (faster moving and attacks, both screens), the teammate's cooldowns refreshed on their own game but not the ultimate, nobody in reach (half on you, no refresh), going down ending it, Deep Reserves and mastery", () => ChakraReservesTest(Dt)),
        Test("bamboo", "Bamboo Grove, the Tank Ninja's 4th choice: the 4th lobby key, the wind-up, the wave reaching near before far (40 and a stun, heavy enemies less), the bleed once the stun ends, nothing past the radius, the fence holding enemies out and stopping shots, mastery (6 s fence) and Rhizome", () => BambooGroveTest(Dt)),
        Test("siegebell", "Siege Bell, the Tank Knight's 4th choice: the 4th lobby key, the sound, braced (rooted, 25% less damage) while it rings, tolls 1 and 2 (8 and a flinch within 120 px), the shockwave reaching near before far (20, a shove, a 2.5 s stun, heavy enemies less), nothing past 260 px, mastery (3.5 s, shots erased) and Great Bell", () => SiegeBellTest(Dt)),
        Test("flashfire", "Flash Fire, the DPS Elementalist's third ultimate (key 2): the kindle, the ring reaching near before far (100, slowed 40%, heavy enemies less), nothing past the radius, the ground hurting 25 a second for 8 s, going out and leaving what is in it burning, mastery (11 s) and Backdraft", () => FlashFireTest(Dt)),
        Test("stampede", "Stampede, the Tank Summoner's 4th choice: the 4th lobby key (that class only), a 7-beast line charging 320 px, trample 20 once (heavies hurt, not shoved), the shove, the wall slam (+15, stun), the herd fading on every screen, Thundering Herd, the mastered herd charging back", () => StampedeTest(Dt)),
        Test("claymore", "Claymore, the DPS Soldier's 3rd choice: the 3rd lobby key, two charges, the mine at the cursor (220 px reach) facing away, the arming, the trigger cone, the wide blast (140, less with distance, nothing behind), the shove, three at most, Shaped Charge and mastery (a second blast)", () => ClaymoreTest(Dt)),
        Test("senbon", "Senbon Seekers, the DPS Ninja's 4th choice: the 4th lobby key, three needles a cast up to 9 (nothing at 9), no timer, launching only within 200 px, the hit (20 and a poison stack, used up), two per enemy at most, poison to 5 stacks, flying back when its enemy dies, other screens, going down, Hidden Needles, mastery (12)", () => SenbonTest(Dt)),
        Test("landshark", "Land Shark, the DPS Summoner's 4th choice: the 4th lobby key, the puddle at the cursor (420 px reach), the bite (120 once and a stun, heavies too), only what is in the puddle, the drain, Great White and mastery", () => LandSharkTest(Dt)),
        Test("rod", "Lightning Rod and the lobby's ultimate choice: the pick reaching the run and saves, 80% cut, taunt, a bolt per hit with 2 arcs, the per-enemy cap, wet arcs, ends on time or on going down", () => RodTest(Dt)),
        Test("stonecolossus", "Stone Colossus: key 2 of the Tank Elementalist's Ultimate row, the rise, 70% cut, taunt, stomps, the slam, the crumble's boulders, ending on time or going down, mastered and Bedrock", () => StoneColossusTest(Dt)),
        Test("bloodgolem", "Blood Golem: key 2 of the Tank Hemomancer's Ultimate row, the gather (stacks torn off, the rest paid), coats soaking and shedding, Golem Fist and the Slam, Hurl Clot, kills growing a coat, the collapse, ending, mastered", () => BloodGolemTest(Dt)),
        Test("vortex", "Vortex: key 3 of the Tank Elementalist's Ultimate row, the wind-up, swirl and pull, the fringe, heavies, 45 a second, shots swept away, the fling, going down, mastered", () => VortexTest(Dt)),
        Test("ravager", "Blood Ravager: key 1 of the DPS Hemomancer's Ultimate row, the cast (9 s, 1.8x speed, the weapon away), a click lashing the free leg nearest an enemy in reach, up to 3 at once on different enemies, the walk's stomps and the ram, a client's click, ending on time and going down, mastered", () => BloodRavagerTest(Dt)),
        Test("ballista", "Ballista: key 2 of the DPS Knight's Ultimate row, the drop and landing slam, a teammate mounting and leaving over the wire, one rider at a time, the seat, bolts on Fire every 0.9 s through two in line (off-line untouched, knocked along, enemy shots knocked down), 30% of the rider's hits on it, breaking after the last bolt, folding away, mastered and Windlass", () => BallistaTest(Dt)),
        Test("boil", "Blood Boil: key 2 of the DPS Hemomancer's Ultimate row, the cast (10 s), the 7% a second drain and its 5% floor, speed, fire rate and damage growing as health falls, back to normal at the end with the health kept, pressing again, going down, mastered, the client", () => BloodBoilTest(Dt)),
        Test("livingbolt", "Living Bolt: key 1 of the DPS Elementalist's Ultimate row, the channel, dashes that shock and leave residue, the lockout, the weapon away, a client's dash, the discharge, going down, mastered", () => LivingBoltTest(Dt)),
        Test("heartbeat", "Heartbeat: key 1 of the Healer Hemomancer's Ultimate row; three beats each paying 8% of max health down to 15%, each wave hitting an enemy in reach once (not past 320 px), a wave's kill always dropping a blood orb, the end on time and going down, mastered, the client", () => HeartbeatTest(Dt)),
        Test("staticbubble", "Static Bubble: key 1 of the Healer Elementalist's Ultimate row; enemies put out, shoved, hurt and feeding it (heavies too), shots eaten, allies inside safe, the growth, the full burst and Static Rush for those in it, the smaller pop on time, going down, mastered, the client's view", () => StaticBubbleTest(Dt)),
        Test("astral", "Astral Projection: key 1 of the Healer Ninja's Ultimate row; the body can't be hurt, can't move and enemies drop it, the host healed and sped up, the spirit's lean and astral bolts (the weapon away), right click switching round the team, a host going down, riding your own body alone, the end, mastered and Astral Bond", () => AstralProjectionTest(Dt)),
        Test("spiritavatar", "Spirit Avatar: key 2 of the Tank Ninja's Ultimate row, the rise, 60% cut, taunt, shots swallowed, the Moon Cleave (wind-up, 200° sweep, 80, stun, push, heavies half, bosses unpushed), the release, going down, mastered and Kami Vessel", () => SpiritAvatarTest(Dt)),
        Test("juggernaut", "the Juggernaut Suit and the lobby's Ultimate row: the pick reaching everyone and clamped, armour before health, a flak shard per bullet, the slow and taunt, ending on time, armour 0, going down and between waves, saved runs", () => JuggernautTest(Dt)),
        Test("hospital", "the Field Hospital: the Healer Soldier's key 1, the pick's free and taken door spots, the cell opened as a room on every game, its shrine (once each, 25% past max), orbs that stay inside and come back, the gate keeping enemies and shots out, nothing built off it, a full minute through the wave's end, gone with a map reset, everyone put out and the room taken off with parents kept, the tent outside a Grid run", () => FieldHospitalTest(Dt)),
        Test("supplydrop", "the Supply Drop: the Healer Soldier's key 2, the flare at the cursor clamped to 520 px, nothing for 1.5 s, then the landing's damage and stagger within 90 px only, the open zone speeding movement, fire and cooldowns (not the ultimate) for everyone inside on every screen, health packs every 2 s (4 at most), the buff ending on stepping out and the zone after 12 s", () => SupplyDropTest(Dt)),
        Test("armordrop", "the Armor Drop: the Tank Soldier's key 3, the flare at the cursor clamped to 520 px, nothing for 1.6 s, then the landing's damage and stagger within 110 px only, 3 vests (4 mastered) 70-110 px out on every screen, a vest setting armor to 50 (no stacking), armor before health and after the Juggernaut Suit's, no regeneration, a refill, the crate folding once the vests are taken or after 20 s", () => ArmorDropTest(Dt)),
        Test("helicopter", "the Attack Helicopter: the DPS Soldier's key 1, the pick reaching the run, a fly-in with no fire, then one target at a time nearest you within 560 px at 20 rounds of 8, the next once it dies, twin guns on two targets mastered, the other screen sees it, ends on time, going down and between waves", () => HelicopterTest(Dt)),
        Test("literaltank", "A Literal Tank: the Tank Soldier's key 2, the pick reaching the run, 400 armour before health, 75% speed, abilities locked, a shell every 0.9 s (75 on a hit, 45 round it, a burst at the cursor), small enemies crushed and heavies shoved, the wreck blowing up on time, mastered 550, ending on armour 0, going down and between waves", () => LiteralTankTest(Dt)),
        Test("trojanhorse", "Trojan Horse: the Tank Knight's key 1, the pick reaching the run, the roll at 90 px/s shoving enemies aside and parking at the edge, climbing in (Interact and Msg.HorseBoard), unhurt and abilities locked inside, an arrow every 0.35 s for 16, everyone in: enemies go for the horse and wear it, shots stopping on it, breaking (thrown out, untouchable), Sally Forth on time (+25% damage, enemies shoved), the cooldown waiting, mastered 40% health, ending between waves", () => TrojanHorseTest(Dt)),
        Test("ironarmadillo", "Iron Armadillo: the Tank Summoner's key 2, the pick reaching the run, the curl (rooted), rolling toward the cursor with speed building, no damage taken, abilities locked, hits by speed that fling enemies and stun Heavies, shots ricocheting off the shell, a fast wall hit bashing, the uncurl slam on time, mastered 13 s, ending quietly on going down and between waves", () => IronArmadilloTest(Dt)),
        Test("dogma", "Dragon's Dogma: the Tank Ninja's key 1, the pick reaching the run, regular enemies devoured whole, heavies bitten for 150 once per 1.2 s, shots swallowed, home and ended with its body drained, 7 s and 3% heals mastered, ends on going down and between waves", () => DogmaTest(Dt)),
        Test("tithe", "Vein Tithe: a tendril to every enemy on screen, held enemies frozen for 15 s, a per-enemy health drain, a slow, 40% less damage, ends on time or on death", () => TitheTest(Dt)),
        Test("bloodbath", "Bloodbath and the lobby's ultimate pick: 15% cost, a 170 px pool for 10 s, 50/35/20% slow, a bleed only while in it, 25% back with no extra 3%, 20% guard, Blood Price, ends on time or between waves (not on going down), the pick reaching the run", () => BloodbathTest(Dt)),
        Test("rain", "Sanguine Rain: a 1.8 s siphon costing 12%, rings that heal every ally 3% a tick for 10 s, the healer's 3% drain, cancel by pressing again, the 15% floor", () => RainTest(Dt)),
        Test("tankalts", "the Tank Soldier's Alt fires: Shock Grenade (three grenades, lobbed, a burst that stuns), Proximity Shell (lies at the cursor, bursts under an enemy), Tear Gas (suppresses to the cap and slows)", () => TankAltTest(Dt)),
        Test("twinnades", "the Twin Rifle's grenade tubes: hits load two, Alt fires left then right, the order holds through a refill", () => TwinGrenadeTest(Dt)),
        Test("grenadelauncher", "the Grenade Launcher: HE bursts at the cursor (less at the rim), six to a drum then a reload, Alt swaps to smoke, the cloud jams a regular enemy and slows it, a mini-boss is only slowed, three canisters back one per 6 s, the other screen sees it", () => GrenadeLauncherTest(Dt)),
        Test("pistolwhip", "the Revolver's Fan stance: Alt switches to it, a click whips and stuns the pack in front, a click after fans the hammer, the parry resets the combo, parry + click fans", () => PistolWhipTest(Dt)),
        Test("railgun", "the Railgun: hold to charge a beam (tap to full, perfect), pierce, the ricochet off the edge, heat from every shot seen by the other screen, Heat Dump's cone burns, shoves and empties the meter", () => RailgunTest(Dt)),
        Test("needles", "Acupuncture Needles: Fire flies through enemies and sticks in a hurt teammate, healing over time (seen on the other screen); the fifth opens the meridian (a burst, the needles out); they fall out after 4 s; Alt's Bloodletting: three needles that hit, stick and bleed, a 0.9 s cooldown; the fifth in one enemy bursts at the pressure point; a dying enemy's needles fly back to heal the most hurt player", () => NeedlesTest(Dt)),
        Test("ofuda", "Ofuda: Fire's Healing Talisman bends onto a hurt teammate late in its flight and heals it; on an enemy it does no damage but shoves it and erases small shots near; Alt's Blast Ofuda sticks to the enemy it hits, rides it (seen on the other screen) and blows for the weapon's damage, or lands and blows later; 1.1 s cooldown", () => OfudaTest(Dt)),
        Test("hivecenser", "the Hive Censer: held Fire pours insects from the cursor that settle into swarms (up to four, 9 s each, 900 kept), a swarm wraps an enemy for damage scaled by its size and a slow, shells teammates in chitin that soaks hits and wears off, Alt sends every swarm to the cursor (6 s cooldown)", () => HiveCenserTest(Dt)),
        Test("staticshock", "Static Shock: the held cone wires up to six enemies into a circuit and shocks them harder the longer they stay, a teammate in it gets Overcharge (faster moving, firing and shots for 10 s, seen by the other screen); Alt's Arc Cannon rolls through enemies and teammates and bursts at the cursor with a stun, 5 s cooldown", () => StaticShockTest(Dt)),
        Test("twinpistols", "the Twin Pistols: the guns take turns, hits knock tiny health orbs loose, Alt's Laser Sights fire both together down beams that meet at the cursor, slower walking, the cooldown, the other screen sees it", () => TwinPistolsTest(Dt)),
        Test("doublebarrel", "the Double Barrel: left then right barrel, 9 pellets each that pierce, two shells then a break-open reload, Alt fires both, damage falls off with range, the other screen sees the reload", () => DoubleBarrelTest(Dt)),
        Test("arclock", "Arc Lightning's lock-on chain: holding tags the enemy in the cone nearest the cursor, up to ten, tags stick outside the cone, letting go chains through them in order for 16 + 4 a tag and a stun, nothing tagged fizzles; Forked Volley hits the three nearest the cursor, then cools down", () => ArcLockTest(Dt)),
        Test("honeybee", "the Honeybee Staff: a bee hatches at the cursor, buzzes, stings the nearest enemy once and dies (or fizzles), the cap, Swarm, held Alt sprays honey puddles that slow and drain the meter, an enemy dying in honey bursts into free bees, the other screen sees it, Bigger Hive", () => HoneybeeTest(Dt)),
        Test("stonespear", "the Stonespear: Fire slams (wind-up, damage round you, six spears up), then throws the risen spear nearest the cursor (pierces one), Alt sends the rest curving in on the enemy nearest the cursor, the next slam waits, the other screen sees the spears and the lock, Tectonic adds a spear a side", () => StonespearTest(Dt)),
        Test("bloodbrawler", "the Blood Brawler: Fire on an enemy marks it and lands Pulse, Reel, Heart Lift and Marrow Burst (costs, shove, pull, airborne, shards), the string starts over, nothing fires without a target, Alt's Sympathy echoes moves onto bound enemies at half, Marrow", () => BloodBrawlerTest(Dt)),
        Test("needleswarm", "the Needle Swarm: full and free when it comes out, siphoned for health (not under 15%), shots break needles on the ring, held Fire grows a tendril to the enemy nearest the cursor (pulled back if let go) that wraps, cuts, slows and bleeds it until it dies, three at once, Alt's fan of needles and its cooldown, the other screen, putting it away", () => NeedleSwarmTest(Dt)),
        Test("embolus", "the Embolus Spears: Fire throws the left then the right spear for 20% health each, they pierce and stick in the wall, a third Fire recalls both (piercing again) and the catch heals it back, Alt bursts them for half back and new ones harden, holding Fire cycles, the other screen sees them, Burst Vessels, putting it away crumbles them", () => EmbolusTest(Dt)),
        Test("vowguard", "the Vowguard: chains packed, saved and sent to both screens, each stance plays its own chain in order, Faith lit by Valor links and spent by finishers, the chain window, Alt and Sanctuary's slower walk and front guard, Rally Tap, the ward, the dome, Cover Ally, the parry starts both over", () => VowguardTest(Dt)),
        Test("nodachi", "the Nodachi: Arashi's five sweeps land and go round with no timer, Alt switches to Mamori and both stances keep their place, Mamori's jabs and slower walk and its cone blocking shots, armoured Storm wind-ups, Tenchi stuns, the parry starts both over, the other screen sees it, Tamahagane", () => NodachiTest(Dt)),
        Test("nunchaku", "the Nunchaku (Twin Storm): Fire runs Snap, Backhand, Double Figure-8 and the Twin Crush (a stunning shockwave) and starts over; Alt raises the Twin Windmill guard (slower, seen by the other screen), its discs swat shots back and count them, Fire in it is the Twin Counter (20 + 6 a swat), the parry resets the combo, Iron Links", () => NunchakuTest(Dt)),
        Test("frostlance", "the Frost Lance: the five-step chain (crescent ahead not behind, backhand, waltz all round, fan, slam), chill freezes, the slam shatters, the parry breaks a step and keeps the chain, the Rime Counter, Absolute Zero freezes then shatters", () => FrostLanceTest(Dt)),
        Test("twintessen", "Twin Tessen: the Crane Dance hits ahead and heals an ally it brushes, chains to the Crane Bloom (the only step reaching far), petals heal the hurt ally, Tailwind, Breeze on both screens, a pause starts it over; the Kamikaze Gust slows you while held, throws enemies back, turns shots round, heals and cools down; a full Breeze makes a stunning Typhoon; Divine Wind", () => TwinTessenTest(Dt)),
        Test("heartflail", "Heart Flail: a click swings through the enemy ahead (bleed, a quicker beat, blood drops healing the hurt ally), held spins all round and heals the ally near you each beat, 180 beats make a Cardiac Burst, Alt throws the heart where it roots and heals through its veins, then reels back; Pacemaker", () => HeartFlailTest(Dt)),
        Test("windclash", "Wind Clash: the crescent hits ahead not behind, the back crescent, the gale lance reaches far down the aim, the clash ring hits all round, each wave shoves, the combo starts over; Tempest hurts, catches shots, slows you, stops attacks, ends at 3 s and cools down; the other screen sees it; Gale Force", () => WindClashTest(Dt)),
        Test("bumbleremote", "Bumble Remote: the Bumble Bot follows the cursor within its leash, you never shoot, it shoots the enemy near it on its own and twice as fast with Fire held, enemies still come for you, Alt dives into a Swarm Burst (blast, shove, bees, cooldown); the other screen sees it", () => BumbleRemoteTest(Dt)),
        Test("wyvernstaff", "Wyvern Staff: the Wyvern hovers over you; its gale slows, shoves and slows enemy shots; Fire held breathes fire for as long as the Cinder lasts (15 to start) and the Wyvern drifts back slowly; Alt dives and slams (damage, stun, cleared shots, Cinder back, cooldown); the other screen sees it; Stormwing", () => WyvernStaffTest(Dt)),
        Test("twinauto", "the Twin Rifle Auto: each gun picks the nearest enemy on its side and fires untouched, Alt locks both on the enemy by the cursor (crossfire) until it dies or Alt is pressed again", () => TwinAutoTest(Dt)),
        Test("elementballs", "the Fireball's variants: the Waterball rolls through and soaks (no burn), the Earthball stops on the first enemy, stuns it (less on heavies) and its rubble hurts the one beside it", () => ElementBallsTest(Dt)),
        Test("outofammo", "Out of Ammo: no shots, the five-move knife chain on a close enemy, one swat per rifle, Alt's spin cuts and swats everything then cools down", () => OutOfAmmoTest(Dt)),
        Test("cauterize", "Cauterize: fire on a bleeding enemy lands the bleed left at once, times 1.25, split bleeder/burner credit, ends the bleed, the look, a 1 s gap", () => CauterizeTest(Dt)),
        Test("poisondart", "Poison Dart: the dart at the enemy nearest the cursor, the poison, the spread on death and the chain, mastered, Virulence", () => PoisonDartTest(Dt)),
        Test("airstrike", "Airstrike: a carpet of bombs across the aim, wall to wall, the burning lane, the mastered second wall, Bigger Boom", () => AirstrikeTest(Dt)),
        Test("volley", "Warhorn Volley: the DPS Knight's key 1 ultimate; burning arrows sweep the lane once per enemy (120, burn 20/s for 5 s, pinned 40% for 3 s), knock ordinary shots down, mastered wider and longer fire", () => WarhornVolleyTest(Dt)),
        Test("honeyraid", "Honey Raid: the Healer Summoner's key 1 ultimate; the bee coming in from behind, drips on the way, the 40 splash, a 240 px pool slowing 55/35/20%, burning 12/s and healing allies 10/s, setting to stick grunts (not bosses), mastered 300 px", () => HoneyRaidTest(Dt)),
        Test("tusker", "Tidal Tusker: the Tank Summoner's key 3 ultimate; the rise, following behind you, the hose (40 a second, wet, the first enemy in line, nothing out of reach, grunts pushed, heavies not), the client seeing it, the taunt (not bosses), shots stopped, the stomp when broken or on time or when you go down, mastered two streams, Deep Trunk", () => TidalTuskerTest(Dt)),
        Test("mammoth", "Mammoth Fall: the DPS Summoner's key 2 ultimate; the ring following the aim within 520 px, the client seeing it, the slam (120 under it, 25 and a 1.5 s stun in 200 px, heavies not shoved), the rampage charging and trampling enemies near the crater, the end on time, going down while it forms, mastered, Ice Age", () => MammothFallTest(Dt)),
        Test("rodent", "Rodent Minigun: the DPS Summoner's key 1 ultimate; the portal's timeline (about 207 rodents), the weapon away, 75% speed, the barrel dragging at 240°/s, the mix of mice, rats and hamsters, the rat's shove, no weapon mods, Plague Rats, going down, mastered", () => RodentMinigunTest(Dt)),
        Test("forbidden", "Forbidden Path: the DPS Ninja's key 1 ultimate; the shed, the lock-on, Fire held running all five stages (Pounce, Rake, Spiral's pull, Rend, Gate and the brands bursting), the weapon away, the drain, a kill's heal, the client's view, the end, going down, mastered, Forbidden Hunger", () => ForbiddenPathTest(Dt)),
        Test("orbital", "Orbital Laser: the DPS Soldier's key 2 ultimate; nothing hit in the 1.9 s lock-on, then every regular and heavy enemy in 260 px killed, bosses lose 20% once, shots erased, allies safe; the scorch burns 25/s and slows 30% for 4 s; mastered 340 px and the beam ploughs 200 px on", () => OrbitalLaserTest(Dt)),
        Test("cavalry", "Cavalry Call: two lancers (four mastered) ride the aim, hit each enemy once, shove, slow and hurt, Heavy ones barely move, the 20% guard", () => CavalryTest(Dt)),
        Test("fireflyroot", "the Sprite Staff's firefly hops and the Treant Rod's root lash", () => FireflyRootTest(Dt)),
        Test("healersolo", "Healers alone: aura self-heal, overheal shields, Martyr's ward, Phoenix, Adrenaline Shot", () => HealerSoloTest(Dt)),
        Test("ironhide", "the Tank role passive Ironhide: shield before health, the wait and refill, breaking, downed, snapshot, Tanks only; the Healer aura's falloff", () => IronhideTest(Dt)),
        Test("momentum", "the DPS role passive Momentum: kill stacks, speed and fire rate, ageing, downed, snapshot, DPS only", () => MomentumTest(Dt)),
        Test("adrenaline", "the Adrenaline Shot: cursor targeting, speed, fire rate, damage cut, expiry, Long Dose, capstone", () => AdrenalineTest(Dt)),
        Test("difficulty", "difficulty presets and Custom: lobby, sync, saves, and what each number changes", () => DifficultyTest(Dt)),
        Test("readable", "regular waves: shooters take turns, a wind-up, bullet shapes, thinner rings", () => ReadabilityTest(Dt)),
        Test("rulemods", "Hunted, Death Throes and Second Wind", () => RuleModifiersTest(Dt)),
        Test("runmods", "Head Start, Practiced, Bloodlust, Tiny Arena and Nightfall", () => MoreModifiersTest(Dt)),
        Test("paths", "divergent paths", () => PathsTest(Dt), () => PathHazardsTest(Dt), () => PathTwistsTest(Dt), () => PathArenaRulesTest(Dt)),
        Test("bazaar", "the Bazaar", () => BazaarTest(Dt)),
        Test("props", "the arena's props: crates as cover, barrels, the treasure chest and its items", () => PropsTest(Dt)),
        Test("timed", "timed waves: enemies arrive over a window (ramp, plateau, taper) and the wave clears when all are dead", () => TimedWavesTest(Dt)),
        Test("portals", "spawn portals and surges: bursts through portals, sealing one cancels the rest, flanks behind the party, the surge rhythm", () => SpawnPortalsTest(Dt)),
        Test("layouts", "arena layouts: the six floor plans, walls and pillars, edge gates, enemy gates, steering round walls, the walls by cell (SolidGrid)", () => LayoutsTest(Dt), () => SolidGridTest()),
        Test("expand", "the Expanding arena: seeded room offers, no overlaps, connected maps, open doors, the size cap", () => ExpansionTest(), () => ExpandingGameTest(Dt), () => ExpandingEconomyTest(Dt), () => ExpandingFeaturesTest(Dt), () => ExpandingVectorsTest(Dt), () => ExpandingGatekeeperTest(Dt), () => ExpandingKitsTest(Dt), () => ExpandingBreachesTest(Dt), () => ExpandingLinksTest(Dt), () => ExpandingGatesTest(Dt), () => ExpandingSurpriseTest(), () => ExpandingSurpriseGameTest(Dt), () => ExpandingBreachEventTest(Dt), () => ExpandingStayInRoomsTest(Dt), () => ExpandingNodeCentreTest(), () => ExpandingVectorChestTest(Dt), () => ChestKindsTest(Dt)),
        Test("stayin", "enemies stay inside the rooms", () => ExpandingStayInRoomsTest(Dt)),
        Test("roomfog", "room fog (Docs/FogOfWar.md): new rooms start unseen, the line-of-sight cone explores and shows them for everyone, saves, bots scout; dormant groups", () => RoomFogTest(), () => RoomFogGameTest(Dt), () => RoomFogBotTest(Dt), () => DormantGroupsTest(Dt)),
        Test("celllayouts", "cell layouts (Docs/Mockups/cell-layouts.html): hand-made walls per grid cell, open door lanes and middle, clear sleeper slots, the same on every build", () => CellLayoutsTest()),
        Test("pits", "cell variants and pits (Docs/CellVariants.md): the pit variants keep the middle and doors clear, rolled per cell; an enemy shoved in dies, a player climbs out with 15% less health", () => PitsTest(Dt)),
        Test("layouttest", "the Cell Editor's hub edits and TEST LAYOUT (HubLayoutEdits.cs, LayoutTest.cs): a hub plan's walls and crates change and revert, a tested piece is built with the layouts asked for", () => LayoutTestTest(Dt)),
        Test("gridrooms", "grid rooms (Docs/GridRooms.md): the core, door spots on the lattice, nodes and chests at a cell's middle; pieces, cards and mini-boss rooms", () => GridRoomsTest(), () => GridRoomsGameTest(Dt), () => PieceShapesTest(), () => PieceCardsTest(), () => GridMiniTest(), () => GridSurpriseTest(), () => PieceShapesGameTest(Dt)),
        Test("lighting", "dynamic lighting's shadows (Core/LightCast.cs): walls and bodies stop a light, sharp corners, a light's own body ignored, casters out of reach skipped", () => LightingTest()),
        Test("turret", "the Black Market's Turret (Turret.cs): placed from the stash, it shoots enemies in range for 5 waves, 3 each", () => TurretTest(Dt)),
        Test("armsdealer", "the Arms Dealer and the Medic (Docs/ArmsDealer.md): service rooms selling every weapon mod for gold, priced by your stacks, and heals and revives; a boss's pick of 5 room cards and kits", () => ArmsDealerTest(Dt), () => BossCardsTest(Dt)),
        Test("mysterydoor", "mystery doors (Docs/MysteryDoors.md): sealed doors rolled on the outer wall, paid to open any time, one save per player, the rest expire", () => MysteryDoorsTest(Dt), () => MysteryContentsTest(Dt), () => MysteryBotsTest(Dt)),
        Test("factionrelics", "faction relics: 20 over 10 bosses, node relic, stats, saves", () => FactionRelicsTest(Dt)),
        Test("oneway", "One-way pieces (Docs/PieceMods.md): all doors but one one way (in or out), the first both ways; players and enemies held, routes the open way", () => OneWayDoorsTest(Dt)),
        Test("shrines", "health shrines (the only node: one use each, health past max) and room chests (pieces and mystery rooms roll one by rarity, vendors never)", () => ShrinesTest(Dt)),
        Test("chests", "chest kinds (Props.cs ChestKind): the node roll, each kind's reward, the kind in the snapshot", () => ChestKindsTest(Dt)),
        Test("challenges", "challenge rooms: a portal every five waves, the five challenges, the altar's prize, failing safely", () => ChallengesTest(Dt)),
        Test("bossgate", "the boss gate (BossGate.cs): opens before the boss, holds the wave until everyone stands in it, bots walk in", () => BossGateTest(Dt)),
        Test("quick", "Quick Play (QuickPlay.cs): the default mode, ten faction waves, a random boss after wave 10, its records, endless", () => QuickPlayTest(Dt), () => QuickBuildTest(Dt), () => TutorialTest(Dt), () => QuickBossHpReport()),
        Test("bossschedule", "boss schedules (BossSchedule.cs): the Campaign's and a hunt's match the old formulas", () => BossScheduleTest()),
        Test("huntmap", "the hunt map (HuntMap.cs): rows and lanes, links that never cross, node types, walking and restoring it; played over the network: the lobby switch, the vote, Elite waves", () => HuntMapTest(), () => HuntMapNetTest(Dt)),
        Test("huntsave", "a Boss Hunt saves once, at its step-10 checkpoint; Continue picks it up there (HuntMapGame.cs, SaveData.cs): the text, the lobby, the resumed map and build, the save going when the hunt ends", () => { }, () => HuntSaveTest(Dt)),
        Test("recycle", "enemy recycling (Recycling.cs): a straggler far from every player comes back in near the party", () => RecyclingTest(Dt)),
        Test("survival", "Survival (Survival.cs): the pressure curve, faction phases, the cap, squads, Elites, the same seed gives the same run", () => SurvivalTest(), () => SurvivalNetTest(Dt), () => SurvivalModsTest(Dt)),
        Test("hunts", "Boss Hunts: locked until the boss is beaten, the hunt's waves, winning it, its records", () => HuntTest(Dt)),
        Test("wiki", "the in-game wiki: an entry with text for every class, weapon, ability, card, perk and enemy; search; references both ways", WikiTest),
        Test("perks", "perks: unlock rules, slots, equipping (one per boss), the profile line; Riot Gear: block, grace, 30 s recharge, burns go through; Contraband: the cycle chest and rare-or-better items", () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt)),
        Test("rank", "the account rank: the curve, XP for a run, modifier multiplier, profile, lobby sharing, a run ending", () => RankTest(Dt)),
        Test("remotechests", "the Chests tab: collect a chest from afar for a fee (between waves only), everyone's share straight to them", () => RemoteChestsTest(Dt)),
        Test("pickups", "per-player pickups: each player's own coins, orbs and Magnets, taken only by them, the owner on both screens", () => PickupsTest(Dt)),
        Test("spawners", "the F1 dock's enemy spawners: placed, spawning on a timer up to the cap, seen by a client, removed", () => DevSpawnerTest(Dt)),
        Test("gadgets", "gadgets: the Magnet picked up, switched on (pulls every coin), thrown and caught over the network, used up, dropped when downed", () => GadgetTest(Dt)),
        Test("pings", "pings: tap/wheel marks, host pause and cap, Focus following an enemy, lapsing", () => PingTest(Dt)),
        Test("foequery", "the shared attack queries (FoeQuery.cs): circle, strip, cone and every foe match the plain loops", () => FoeQueryTest()),
        Test("pvp", "PvP in the Hangout (Pvp.cs): attacks hurt other players at PvpDamage, never yourself; every weapon and ability works on a player", () => PvpTest(Dt)),
        Test("training", "Training (Training.cs): the drills, their medals and best times, the lobby mode that never starts a run from START", () => TrainingTest(Dt)),
        Test("hangoutmode", "the Hangout mode (HangoutGame.cs): opened by a client, held between waves, dummies, real weapon, no XP, back to lobby", () => HangoutModeTest(Dt)),
        Test("hangout", "the lobby's Hangout room: everyone's character, moves over the network, the walls", () => HangoutTest(Dt)),
        Test("zombies", "Zombie Infection: melee hordes, the cap, the dead rising", () => ZombieTest(Dt)),
        Test("scratch", "V2 distance tests without the square root, the bullet table, and the reusable lists", ScratchTest),
        Test("refdoc", "Docs/Reference.md (weapon, ability and enemy tables) matches the definitions (run.sh 1 1 refdocwrite)", RefDocTest),
        Test("clearghosts", "a shot fired in the tick enemy bullets are cleared doesn't linger on the client", () => ClearGhostsTest(Dt)),
        Test("three", "three players", () => ThreePlayerTest(Dt)),
        Test("wrap", "wrapping round the arena's edges", () => WrapTest(Dt)),
        Test("ready", "ready-up in the lobby", () => ReadyTest(Dt)),
        Test("dev", "the F1 dev tools", () => DevToolsTest(Dt)),
        Test("runstats", "the Run stats screen's live numbers reach every player", () => RunStatsLiveTest(Dt)),
        Test("waveready", "the between-waves summary, awards, Ready and Auto-ready", () => WaveReadyTest(Dt)),
        Test("wavekinds", "a recipe wave shows at most 6 distinct enemy types (8 from wave 25)", () => WaveKindsTest()),
        Test("landmarks", "the Shade's blink and the Mortar's bomb mark where they land", () => LandingMarksTest()),
        Test("newenemies", "the new-enemy chips: which types a wave brings and a note for each", () => NewEnemiesTest()),
        Test("enemynumbers", "every weapon has a damage a second, spawn health at your wave, the fire-rate ramp", () => EnemyNumbersTest(Dt)),
        Test("labtest", "the Boss Lab", () => BossLabTest(Dt)),
        Test("botweapon", "the smart bot's parry and weapon Alts", () => BotParryTest(Dt), () => BotAltTest(Dt), () => BotKatanaTest(Dt)),
        Test("pickheld", "level-up cards held while an awake enemy is near between waves (Game.PickHeld)", () => PickHeldTest(Dt)),
        Test("botpicks", "the bots' level-up picks and shopping", () => BotPicksTest(Dt), () => BotShopTest(Dt)),
        Test("botteam", "the smart bot's team play: Roar, Last Stand, Mark, healing orbs and zones", () => BotTeamplayTest(Dt)),
        Test("partybots", "bots in the lobby's open slots", () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); PartyBotCampaignTest(Dt); }),
        Test("wildcard", "Wildcard: DPS gear from any class, a Wildcard ability on a chosen key, the damage and cooldown price, the unlock", () => WildcardTest(Dt)),
        Test("lag", "the lag chart: LagStats (stalls, p95, 1% low, freeze, summary, CSV) and pings shared over the network", () => LagChartTest(Dt)),
        Test("stacking", "power stacking: diminishing returns on unlimited cards, wounds (a share of each hit no heal brings back, carried across waves up to a cap)", () => StackingTest(Dt)),
        Test("dodge", "Dodge: the tiny dash with invulnerability (host and client), its cooldown, the step back, the hold-to-sprint", () => DodgeTest(Dt)),
        Test("f1bot", "F1 \"Bot plays for you\" (the smart bot) through waves and the Bazaar", () => F1BotTest(Dt)),
        // Reports and debug tools (no pass/fail).
        Report("chartcheck", "the Campaign's shown waves 1-20 next to the chart in Docs/Approaches.md", args => ChartCheckReport()),
        Report("ttk", "boss time-to-kill over a full run (run.sh 1 1 ttk [last wave])", args => BossTtk(Dt, Arg(args, 3, 50))),
        Report("lab", "Boss Lab batch (run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat])",
            args => LabRun(int.Parse(args[3]), (BotSkill)int.Parse(args[4]), int.Parse(args[5]), args.Skip(6).ToArray())),
        Report("quickfights", "Quick Play boss fights with bots: every boss after waves 10/20/30, its fight length (run.sh 1 1 quickfights [runs] [waves] [skill] [seats])", args => QuickFightsReport(args)),
        Report("levels", "the level a team of Good bots has at each boss", args => LevelReport(Dt)),
        Report("arenabal", "the Expanding arena vs Classic: a duo of Good bots, each regular wave's length, damage taken, threats in view (run.sh 1 1 arenabal [last wave] [seed] [Expanding damage x])", args => ArenaBalanceReport(Dt, Arg(args, 3, 20), Arg(args, 4, 1), args.Length > 5 ? float.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 0f)),
        Report("vecbal", "the Expanding arena's vector economy: a duo of Good bots on Classic vs Expanding, each round's vectors, proposals and rooms built (run.sh 1 1 vecbal [last wave] [seed])", args => VectorBalanceReport(Dt, Arg(args, 3, 25), Arg(args, 4, 1), Arg(args, 5, 0) == 1)),
        Report("rooms", "a duo of Good bots plays with challenge rooms on: how each room went (run.sh 1 1 rooms [last wave] [seed])", args => RoomsReport(Dt, Arg(args, 3, 30), Arg(args, 4, 1))),
        Report("huntbal", "the hunt map's balance: a duo of Good bots plays the same maps on a safe, an Elite and a random route (run.sh 1 1 huntbal [maps] [first seed] [bot skill 1-3] [routes, e.g. safe,elite,random,plain])", args => HuntMapBalanceReport(Dt, Arg(args, 3, 4), Arg(args, 4, 1), (BotSkill)Arg(args, 5, 2), args.Length > 6 ? args[6] : null)),
        Report("survbal", "Survival's balance: bots play Survival to the end or a cap: time survived, phase, level (run.sh 1 1 survbal [runs] [first seed] [bot skill 1-3] [players 1-3] [cap minutes])", args => SurvivalBalanceReport(Dt, Arg(args, 3, 4), Arg(args, 4, 1), (BotSkill)Arg(args, 5, 2), Arg(args, 6, 1), Arg(args, 7, 20))),
        Report("refdocwrite", "rewrite Docs/Reference.md from the definitions", args => RefDocReport(true)),
        Report("wavemix", "what a regular wave is made of: shooters, melee and support, wave by wave", args => WaveMixReport()),
        Report("survperf", "Survival frame cost: two kept-alive bots, clock started late (run.sh 1 1 survperf [seconds] [start min])", args => SurvivalPerfReport(Dt, Arg(args, 3, 60), Arg(args, 4, 12))),
        Report("perf", "frame cost of a heavy wave: Quick Play from a breach wave with a boss's approach (run.sh 1 1 perf [seconds] [boss] [wave])", args => PerfReport(Dt, Arg(args, 3, 60), args.Length > 4 ? args[4] : "TwinA", Arg(args, 5, 7))),
        Report("breachcrowd", "a breach wave's crowd as played: timed spawning, the Warden's approach, six breaches, bots fighting (run.sh 1 1 breachcrowd [seconds] [wave])", args => BreachCrowdReport(Dt, Arg(args, 3, 90), Arg(args, 4, 3))),
        Report("bulletcount", "enemy bullets on screen through a wave", args => BulletCountReport(Dt)),
        Report("grid", "the balance grid: every role, class and weapon vs each listed boss (run.sh 1 1 grid <waves> [runs] [roles DTH] [skill] [classes]; Tools/BalanceGrid/grid.sh draws it)", args => BalanceGrid(args)),
        Report("dps", "every weapon's damage per second", args => WeaponDpsReport(Dt)),
        Report("weapondps", "every weapon's damage a second as the lobby shows it; \"write\" saves Defs.WeaponDps's table, \"only=<WeaponId>\" measures one weapon", args => MeasuredWeaponDps(Dt, args.Contains("write"), (args.FirstOrDefault(a => a.StartsWith("only=")) ?? "").Replace("only=", ""))),
        Report("upgradecov", "which weapon upgrades each weapon ignores", args => UpgradeCoverageReport(Dt)),
        Report("bosspower", "a typical build's power at each boss wave, and the boss health a strong build would bring", args => BossPowerReport(Dt)),
        Report("balance", "what one pick and a full stack of each damage upgrade is worth: boss, pack, spread wave", args => UpgradeBalanceReport(Dt)),
        Report("ultdps", "the damage each DPS ultimate adds against one big target", args => UltimateDpsReport(Dt)),
        Report("laserdebug", "the Singularity's lasers, step by step", args => LaserDebug()),
    };

    /// The pass/fail suites that neither `core` nor `features` runs: CI runs them in its own job (Tools/SimTest/extras.sh),
    /// alongside the other two. A new suite whose tests aren't in FeatureTests goes here. (`singularity` is here for its cage
    /// test; its other test is also in features.)
    static readonly string[] CiExtras =
    {
        "snapshot", "turnkey", "holdingcell", "hitfx", "greatsword", "knuckles", "milestone", "mods", "knightdps", "twinblades", "melee", "brute",
        "trickle", "meleesounds", "recentruns", "pacman", "longrun", "wardencycle", "checkpoints", "coredrops", "variants",
        "exploit", "doublebarrel", "railgun", "twinpistols", "pistolwhip", "stonespear", "honeybee", "nunchaku", "nodachi", "vowguard", "embolus", "needleswarm", "bloodbrawler", "arclock", "twinauto", "outofammo", "elementballs", "adrenaline", "paths", "bazaar", "timed", "layouts",
        "expand", "recycle", "armsdealer", "turret", "mysterydoor", "mapreset", "quick", "bossgate", "bossschedule", "rank", "pings", "hangout", "hangoutmode", "training", "foequery", "pvp", "three", "wrap", "ready", "wavekinds", "landmarks",
        "arcanist", "stages", "bossrules", "newenemies", "guardrally", "riotercrowd", "rioterbottle", "wildcard", "stacking", "singularity", "security", "guardswing", "thugpunch", "captain", "wavedump", "spawndump", "aitrace", "hive", "reapercult", "infall", "quarry", "court", "larder", "basicfaction", "penumbral", "squall",
        // (these were missing until Nov 2026, so nothing caught them breaking; ci_coverage.py now fails CI on a suite left out)
        "abilitycards", "armordrop", "astral", "ballista", "bamboo", "boil", "bosses", "botdodge", "botgate", "botweapon", "bulwark",
        "bumbleremote", "cannoncall", "cardfilters", "cardids", "celllayouts", "chests", "classcardlists", "claymore", "corelayouts",
        "cover", "decoy", "deployturret", "dogma", "engbots", "factionrelics", "flashfire", "forbidden", "galewall", "graze",
        "grenadelauncher", "gridrooms", "helicopter", "hivecenser", "hospital", "huntmap", "huntsave", "ironarmadillo", "ironhide",
        "landshark", "late", "latewaves", "layouttest", "lighting", "literaltank", "livingbolt", "mammoth", "needles", "offers", "ofuda",
        "oneway", "orbital", "pickheld", "pits", "portals", "ravager", "refdoc", "rodent", "roomfog", "scratch", "senbon", "shrines",
        "stampede", "staticbubble", "staticshock", "stayin", "supplydrop", "survival", "thornbloom", "trojanhorse", "twintessen",
        "ultimates", "waterward", "wiki", "windclash", "wyvernstaff",
        "heartflail", "sanctumglass", "siegebell", "chakra", "honeyraid", "heartbeat", "tusker",
    };

    static int Main(string[] args)
    {
        NetPeer.PeerTimeout = 1e9;  // the games here are stepped slower than real time: a 30 s silence isn't a dropped link
        Game.IronhideOff = true;  // tests read a Tank's health loss exactly; IronhideTest turns it on (Ironhide.cs)
        NetPeer.Loopback = Environment.GetEnvironmentVariable("SIMTEST_TCP") == null;  // hosts and joins meet in memory: every message is there on the next Poll, however busy the machine (SIMTEST_TCP=1: real localhost TCP)
        if (NetPeer.Loopback)
            Session.TestClock = () => napClock;
        if (args.Length == 1 && args[0] == "extras-list")
        {
            foreach (var n in CiExtras)
                Console.WriteLine(n);
            return 0;
        }
        string name = args.Length > 2 ? args[2] : args.Length == 1 && args[0] == "list" ? "list" : "core";
        if (name == "music")
            name = "features";  // its old name (it began as the music test)
        if (name == "list")
        {
            foreach (var s in Suites)
                Console.WriteLine("  {0,-12} {1}{2}", s.Name, s.Report ? "(report) " : "", s.What);
            return 0;
        }
        TimedWaves.Enabled = name == "timed";
        Game.GatesOn = name == "bossgate";  // (the boss gate waits for players to walk in; only its own suite plays it)  // (the other suites' waves are short; the timed suite turns them on)
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

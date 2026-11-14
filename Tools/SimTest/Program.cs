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

    static int nextPort = Environment.GetEnvironmentVariable("PORTBASE") != null ? int.Parse(Environment.GetEnvironmentVariable("PORTBASE")) : 7810;  // PORTBASE=n: parallel runs (BalanceGrid) each take their own ports

    // Shared setup for host + friend tests over localhost.
    // HostAs: host a match on `port` with `s` (pass `new Session { AutoStart = 2, StartWave = 3 }` etc.).
    static Session HostAs(Session s, int port, bool bot, string name, Role role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
    {
        if (bot)
            s.Game.Bot = true;
        s.Host(port, name, role, weapon, ability, kit);
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

    // Step both games until the client is in the match (at most 300 steps).
    static void WaitForGame(Session host, Session client, float dt)
    {
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
    }

    // Poll both sessions until the client is in the lobby (at most 300 polls).
    static void WaitForLobby(Session host, Session client)
    {
        for (int i = 0; i < 300 && client.Screen != AppScreen.Lobby; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
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
        for (int i = 0; i < times; i++) { host.Poll(); client.Poll(); Thread.Sleep(2); }
    }

    // Start a bot match at a given wave with both players buffed so they survive a while.
    static Session[] StartBuffed(float dt, int wave, int damageStacks)
    {
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = wave % 5 == 0 ? -wave : wave };  // (a multiple of 5 starts at its boss)
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        host.Host(port, "Host", Role.Tank, 0);
        host.Diff.Set(DiffKnob.Campaign, 0f);  // (the usual random waves; CampaignTest turns it on itself)
        host.Diff.Set(DiffKnob.EnemySpeed, 1f);  // (the fights are time-limited; keep their pace independent of the default speed preset)
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

    static bool domesAllowed;  // only the dome test leaves Fortress's dome standing
    static void Run(Session[] s, float dt, float seconds, Func<bool> stop = null, Action each = null)
    {
        for (int i = 0; i < seconds * 60; i++)
        {
            foreach (var x in s)
                Step(x, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            if (each != null)
                each();
            if (stop != null && stop())
                break;
        }
        for (int k = 0; s.Length > 1 && k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
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
        return new Suite { Name = name, What = what, Run = args => { for (int i = 0; i < tests.Length; i++) Seeded(name + "#" + i, tests[i]); } };
    }

    static Suite Report(string name, string what, Action<string[]> run) => new Suite { Name = name, What = what, Report = true, Run = run };

    static int Arg(string[] args, int i, int fallback) => args.Length > i ? int.Parse(args[i]) : fallback;

    // The feature suite, in its long-standing order.
    static readonly Action[] FeatureTests =
    {
        MusicTest, () => ParryTest(Dt), () => SessionTest(Dt), () => ParryUpgradeTest(Dt), () => AimTest(Dt), () => SpinnerTest(Dt), () => ShielderTest(Dt), () => DevToolsTest(Dt),
        () => MasteryTest(Dt), () => BudgetHomingTest(Dt), () => BossLabTest(Dt), () => SingularityTest(Dt), () => ColossusTest(Dt), () => LichTest(Dt), () => KnightTest(Dt), () => BearTotemTest(Dt),
        () => WardenTest(Dt), () => QueenTest(Dt), () => ReapersTest(Dt), () => BossSoundTest(Dt), () => WeaponExtraTest(Dt), () => RepulsionTest(Dt), () => QuarterstaffTest(Dt), () => TankKnightTest(Dt), () => KeybindTest(Dt), () => RevolverReloadTest(Dt), () => BowTest(Dt),
        () => PracticeTest(Dt), () => MoreModifiersTest(Dt), () => DuelistTest(Dt), () => HealerSoloTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt), () => WeaponSoundsTest(Dt), () => ShowcaseTest(Dt), () => ClassAbilityTest(Dt), () => NinjaTest(Dt),
        () => BloodMageTest(Dt), TextOverridesTest, () => VeinBeamTest(Dt), () => WardenLootTest(Dt), () => LeechGraspTest(Dt), () => F1BotTest(Dt), () => FlamethrowerTest(Dt), () => HealerKitTest(Dt), () => RadiantOrbTest(Dt), () => StormHammerTest(Dt), () => GolemStaffTest(Dt), () => BoneGuardTest(Dt), () => LightConeTest(Dt), () => SanguineOrbTest(Dt), () => FireballArcTest(Dt), () => ScatterFlakBloodTest(Dt), () => TwinSmgTest(Dt), () => PrismWolfRailTest(Dt), () => MissilesRiotBladesTest(Dt), () => FiveMoreTest(Dt), () => FourMoreTest(Dt), () => LightbringerTest(Dt), () => LanceTest(Dt), () => HealerFiveTest(Dt), () => LastFourTest(Dt), () => HealerWardsTest(Dt), () => HeraldTest(Dt), () => DevourerTest(Dt), () => EclipseTest(Dt), () => VarietyTest(Dt), () => SavesTest(Dt), () => ProfileTest(Dt), () => UpgradeFixesTest(Dt), () => QueenLootTest(Dt), () => ReaperLootTest(Dt), () => BossPowerTest(Dt), () => FieldGuideTest(Dt), () => DifficultyTest(Dt), () => PatternsTest(Dt), () => RuleModifiersTest(Dt), () => ReadabilityTest(Dt), () => RunStatsLiveTest(Dt), () => WaveReadyTest(Dt), () => PropsTest(Dt), () => ChallengesTest(Dt), ClassCompletenessTest, () => ApproachesTest(Dt), CampaignTest, () => HuntTest(Dt), () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt), () => ZombieTest(Dt), () => ClearGhostsTest(Dt), () => WaveDesignTest(Dt), WaveGenTest, () => DpsToolsTest(Dt), () => BalanceTestRun(Dt), () => HealingRainTest(Dt), () => LumenWardTest(Dt), () => FireflyRootTest(Dt), () => NimbusChalkTest(Dt), () => TsunamiTest(Dt), () => HolyCrossTest(Dt), () => CavalryTest(Dt), () => TwinGrenadeTest(Dt), () => AirstrikeTest(Dt), MusicLibraryTest, CustomMusicTest, ProjectClipsTest, () => DomeTest(Dt), () => AlphaCallTest(Dt), () => AegisTest(Dt), () => GolemWallTest(Dt), () => SanctumTest(Dt), () => ReplayTest(Dt), () => HydraTest(Dt), () => BurrowerTest(Dt), () => WorldTreeTest(Dt), () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); }, () => BotTeamplayTest(Dt), () => DodgeTest(Dt), () => HarvestTest(Dt), () => TitanTest(Dt), () => RainTest(Dt), () => TitheTest(Dt), () => RodTest(Dt), () => BloodbathTest(Dt), () => JuggernautTest(Dt), () => BloodFreezeTest(Dt), () => ForcePactTest(Dt), () => QuarterstaffStacksTest(Dt), () => StormHammerSlamTest(Dt), () => MomentumTest(Dt),
        () => PhalanxTest(Dt), () => PoisonDartTest(Dt), () => LagChartTest(Dt), () => PracticeRespawnTest(Dt), () => BossSlotTest(Dt), () => TankAltTest(Dt), () => DevArenaTest(Dt), () => SessionSourcesTest(Dt), () => AuditFixesTest(Dt), () => FavoritesTest(Dt), () => GadgetTest(Dt),  // (new tests go last: each test's seed comes from its place, so inserting one reseeds every test after it)
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
        Test("audit", "Nov 2026 audit fixes: slows end on time, piercing shots hit overlapping enemies once, boss coins carry all their gold, Stop clears per-run lists, bad client input and resent dodges are ignored, cut-short run saves are refused", () => AuditFixesTest(Dt)),
        Test("classes", "every class is complete: name, blurb, icon, look, weapons that fire, abilities that cast (run first when adding a class)", ClassCompletenessTest),
        Test("approaches", "boss approaches: the themed share, waves built with it, the Riot Shield (bash, knockback, shield), the Gas Grenadier, the Warden's pair", () => ApproachesTest(Dt)),
        Test("campaign", "the campaign buildup: every wave themed, the last boss fading out, an older pool, the next boss building up", CampaignTest),
        Test("parry", "the parry and its upgrades", () => ParryTest(Dt), () => ParryUpgradeTest(Dt)),
        Test("replay", "run replays: recording, the file, rename and delete, pause, step, any speed either way, seeking", () => ReplayTest(Dt)),
        Test("golemwall", "Golem Wall: three big golems, no timer, worn by every shot, broken at zero, replaced by a recast", () => GolemWallTest(Dt)),
        Test("aegis", "Aeroaegis: the vortex goes to the player nearest the cursor, enemies stay out", () => AegisTest(Dt)),
        Test("sanctum", "Sanctum: seven healing pulses, the last bigger, each ally healed as the ring reaches them", () => SanctumTest(Dt)),
        Test("alphacall", "Alpha Call: Blood Scent stacks build, lunge at 5, replicate, and clear", () => AlphaCallTest(Dt)),
        Test("dome", "Fortress's dome: enemies and their shots stay out, player shots cross", () => DomeTest(Dt)),
        Test("sessionsrc", "the session recording credits melee, beam, lobbed and companion weapon hits to the weapon, and blood costs as health spent", () => SessionSourcesTest(Dt)),
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
        Test("scatterflak", "the Scattergun, Flak Cannon and Blood Bolts", () => ScatterFlakBloodTest(Dt)),
        Test("twinsmg", "the Twin Rifle and Burst SMG", () => TwinSmgTest(Dt)),
        Test("prismwolfrail", "the Prism Beam, Wolf Whistle and Railgun", () => PrismWolfRailTest(Dt)),
        Test("missilesriot", "Ice Shards, the Riot Repeater and Twin Blades", () => MissilesRiotBladesTest(Dt)),
        Test("bosspowertest", "bosses get more health for a party far above a typical build", () => BossPowerTest(Dt)),
        Test("reaperloot", "the Twin Reapers' legendaries: Twin Scythes, Final Reap, Scythe Throw", () => ReaperLootTest(Dt)),
        Test("queenloot", "the Hive Queen's legendaries: Hive Drones, Royal Dive, Bomber Escort", () => QueenLootTest(Dt)),
        Test("swordsman", "the Swordsman's cone, cut and dodge, the Shield Swordsman's guard and block flash, the wave unlocks", () => SwordsmanTest(Dt)),
        Test("captain", "the Riot Captain: its shield, the charge into a wall, the whistle, its attacks, its room over the network", () => RiotCaptainTest(Dt)),
        Test("security", "the Security Guard walks while it locks on, keeps its strafe side and aims straight at you", () => SecurityTest(Dt)),
        Test("reapercult", "the Twin Reapers' cult: Bound Wraiths' tether and revive, Scythe Wraith's reap, Lamplighter, Mourner", () => ReaperCultTest(Dt)),
        Test("nonshooters", "the Fuse Runner's fuse and blast, the Hookman's yank and open window, the Sapper's Tripwire Pods", () => NonShootersTest(Dt)),
        Test("hitfx", "hit feedback: fire alone doesn't flash an enemy, steady fire softens the flash, Explosive Rounds' blasts are thinned", () => HitFxTest(Dt)),
        Test("upgradefix", "Velocity Rounds land airbursts at the cursor; Incendiary Rounds add up", () => UpgradeFixesTest(Dt)),
        Test("fivemore", "Medicine Darts, Greatsword, Kunai, Wisp Lantern and Imp Caller", () => FiveMoreTest(Dt)),
        Test("fourmore", "Shuriken, Bone Spear and Treant Rod", () => FourMoreTest(Dt)),
        Test("lightbringer", "the Lightbringer: Dawn, Mercy, Noon, the light gauge, a Radiant Noon and the Sunlance", () => LightbringerTest(Dt)),
        Test("lance", "the Lance: Jab, Second Jab, Wide Sweep, Breaching Thrust, Impact and Hook & Haul", () => LanceTest(Dt)),
        Test("healerfive", "Censer, Spirit Fan, Ofuda, Sprite Staff and Totem Staff", () => HealerFiveTest(Dt)),
        Test("healingrain", "Healing Rain: heals allies inside, slows enemies, ends on time, mastered lasts 10 s", () => HealingRainTest(Dt)),
        Test("lumenward", "Lumen Ward: a cone of light that heals inside, burns bullets in it and holds the ones outside", () => LumenWardTest(Dt)),
        Test("wards", "Spirit Ward, Healing Mist and Energy Ward", () => HealerWardsTest(Dt)),
        Test("lastfour", "Clot Cannon, Transfusion Darts, Rapier (Tempo, Flourish) and the Katana (stance combos, Momentum Burst, caltrops)", () => LastFourTest(Dt)),
        Test("repulsion", "the Repulsion Cannon", () => RepulsionTest(Dt)),
        Test("quarterstaff", "the Quarterstaff", () => { QuarterstaffTest(Dt); QuarterstaffStacksTest(Dt); }),
        Test("greatsword", "the Greatsword's chain, guard and Rend", () => GreatswordTest(Dt)),
        Test("knuckles", "the Arc Knuckles: the Knight DPS's fourth weapon, its chain, parry cancel and Counter Hammer", () => ArcKnucklesTest(Dt)),
        Test("tankknight", "the Warhammer's charged slam and the Halberd's sweep", () => TankKnightTest(Dt)),
        Test("revolver", "the Revolver", () => RevolverReloadTest(Dt)),
        Test("bow", "the Mender's Bow", () => BowTest(Dt)),
        Test("bear", "the Bear Totem", () => BearTotemTest(Dt)),
        Test("budget", "the enemy bullet budget and Homing Rounds", () => BudgetHomingTest(Dt)),
        Test("keys", "bonus abilities on Q/E/Shift", () => KeybindTest(Dt)),
        Test("mastery", "ability mastery", () => MasteryTest(Dt)),
        Test("classcards", "class ability cards", () => ClassAbilityTest(Dt)),
        Test("milestone", "milestone upgrades: the pool, every 10 levels, the Milestone Compass", () => MilestoneTest(Dt)),
        Test("mods", "weapon mods and boss rerolls", () => WeaponModTest(Dt), () => BossRerollScenario(Dt)),
        Test("offers", "every card offer reads the weapon: weapon-only mods, legendaries and milestone cards", () => OfferWeaponTest(Dt)),
        Test("cardfilters", "class (Kit) and weapon-list cards: who is offered them, the Wildcard's classes", () => CardFilterTest()),
        Test("classcardlists", "each class's own upgrade card list: only its cards, all offered, unique names", () => ClassCardListsTest()),
        Test("abilitycards", "the Rare cards for the Elementalist and Summoner Space abilities and the Medkit (Docs/UpgradePools.md step 5)", () => AbilityCardsTest(Dt)),
        Test("cardids", "card ids over the network: 2 bytes, ids past 255 and empty offer slots", () => CardIdWireTest(Dt)),
        Test("ults", "every ultimate over the network", () => UltimateSweep(Dt)),
        Test("showcase", "the lobby's loadout preview", () => ShowcaseTest(Dt)),
        Test("knight", "the Knight", () => KnightTest(Dt)),
        Test("knightdps", "the DPS Knight's Caught Steel, Gauntlet, Misericorde and Drawbridge", () => KnightDpsTest(Dt)),
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
        Test("blood", "the Blood Mage", () => VeinBeamTest(Dt), () => LeechGraspTest(Dt), () => BloodMageTest(Dt)),
        Test("melee", "early melee rushers", () => MeleeTest(Dt)),
        Test("devarena", "the F1 Dev Arena: no waves, god mode, playing as an enemy (move, attack, attack now, let go)", () => DevArenaTest(Dt)),
        Test("brute", "Brute enrage", () => BruteEnrageTest()),
        Test("trickle", "Campaign handover: fade and trickle", () => TrickleTest()),
        Test("meleesounds", "melee rushers' sounds: quieter with distance, Buzzsaw contact whirr, distinct deaths that merge", () => MeleeSoundsTest(Dt)),
        Test("grunts", "melee grunts: Buzzsaw and Spike Block from wave 5, the zig-zagging Spiker", () => MeleeGruntsTest(Dt)),
        Test("late", "waves 11-13: new enemies and elites", () => LateGameEnemies(Dt)),
        Test("spinner", "Spinners", () => SpinnerTest(Dt)),
        Test("shielder", "Shielders' shields wearing down", () => ShielderTest(Dt)),
        Test("warden", "the Warden", () => WardenTest(Dt)),
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
        Test("duelist", "the Duelist's three stages and last stand", () => DuelistTest(Dt)),
        Test("lich", "the Lich's three stages and last stand", () => LichTest(Dt)),
        Test("singularity", "the Singularity's three stages and last stand", () => SingularityTest(Dt), () => SingularityCageTest(Dt)),
        Test("herald", "the Storm Herald's three stages and last stand", () => HeraldTest(Dt)),
        Test("eclipse", "the Eclipse's five stages and Totality", () => EclipseTest(Dt)),
        Test("variety", "the new late enemies, elite traits and squads", () => VarietyTest(Dt)),
        Test("devourer", "the Devourer's three stages and last meal", () => DevourerTest(Dt)),
        Test("recentruns", "Records > Recent runs: the run's curve, its file, the profile's run log", () => RecentRunsTest(Dt)),
        Test("pacman", "the Pac-Man maze game: maze, steering, tunnel, dots and pellets, the four role ghosts, lives, levels, the ghost house, co-op", () => PacmanTest(Dt)),
        Test("longrun", "the 100-wave Campaign: two legs a track wave, the boss after every tenth, half XP, saves and continuing, the party's level at the first boss", () => LongRunTest(Dt), () => LongRunPaceTest(Dt)),
        Test("wardencycle", "the Campaign's Warden cycle: one lesson a wave (Rioters, guards, shields, gas, tasers, squads, the Riot line) (one guaranteed star enemy each)", () => WardenCycleTest(Dt)),
        Test("checkpoints", "checkpoints and lives: a boss leaves a checkpoint, a wipe with a life left retries from it (score cut), Play again or no lives end the run", () => CheckpointsTest(Dt)),
        Test("coredrops", "weapon variants: what runs and hunts pay in Cores, the glowing Core Carrier", () => CoreDropsTest(Dt)),
        Test("saves", "saved runs (continuing them, in co-op) and the profile", () => SavesTest(Dt), () => ProfileTest(Dt)),
        Test("favorites", "saved favourite loadouts: slots, names, the profile's fav lines, loading one over the network, perks", () => FavoritesTest(Dt)),
        Test("colossus", "the Colossus's three stages, stomp rings, plates and last stand", () => ColossusTest(Dt)),
        Test("bosssound", "boss sound cues", () => BossSoundTest(Dt)),
        Test("bosses", "every late boss's mechanics, victory and endless", () => BossMechanics(Dt)),
        Test("latewaves", "waves 25-50: late enemies, each late boss beaten, victory and endless", () => LateWaves(Dt)),
        Test("exploit", "parking a blocker on a boss", () => ExploitTest(Dt)),
        Test("dpstools", "the DPS meter and target dummies", () => DpsToolsTest(Dt)),
        Test("balancetest", "the F1 Balance tab: five test waves, the log and the report", () => BalanceTestRun(Dt)),
        Test("nimbuschalk", "the Nimbus (cloud, rain, strikes) and Spirit Chalk (ink, loops that pen enemies in)", () => NimbusChalkTest(Dt)),
        Test("tsunami", "Tsunami: the wave, wet, the slow, lightning arcing to every wet enemy, wearing off, the mastered return", () => TsunamiTest(Dt)),
        Test("holycross", "Consecrate's Holy Cross: it grows as it flies, heals and hastens allies, hurts, slows and knocks back enemies, hits each once, the mastered fan", () => HolyCrossTest(Dt)),
        Test("harvest", "Crimson Harvest: rips 20 and the blood of every enemy into a ball, a 5 s hold, a second press throws it for a blast that scales with the blood", () => HarvestTest(Dt)),
        Test("bloodfreeze", "Blood Freeze: 25% health, a charge then a 260 px wave that freezes every enemy it reaches for 5 s (bosses 3 s), no damage, allies untouched, Hungry Earth, mastered +25% damage", () => BloodFreezeTest(Dt)),
        Test("rod", "Lightning Rod and the lobby's ultimate choice: the pick reaching the match and saves, 80% cut, taunt, a bolt per hit with 2 arcs, the per-enemy cap, wet arcs, ends on time or on going down", () => RodTest(Dt)),
        Test("juggernaut", "the Juggernaut Suit and the lobby's Ultimate row: the pick reaching everyone and clamped, armour before health, a flak shard per bullet, the slow and taunt, ending on time, armour 0, going down and between waves, saved runs", () => JuggernautTest(Dt)),
        Test("tithe", "Vein Tithe: a tendril to every enemy on screen, held enemies frozen for 15 s, a per-enemy health drain, a slow, 40% less damage, ends on time or on death", () => TitheTest(Dt)),
        Test("bloodbath", "Bloodbath and the lobby's ultimate pick: 15% cost, a 170 px pool for 10 s, 50/35/20% slow, a bleed only while in it, 25% back with no extra 3%, 20% guard, Blood Price, ends on time or between waves (not on going down), the pick reaching the match", () => BloodbathTest(Dt)),
        Test("rain", "Sanguine Rain: a 1.8 s siphon costing 12%, rings that heal every ally 3% a tick for 10 s, the healer's 3% drain, cancel by pressing again, the 15% floor", () => RainTest(Dt)),
        Test("tankalts", "the Tank Soldier's Alt fires: Double Barrel (shells from point-blank hits, both at once, the kick), Proximity Shell (lies at the cursor, bursts under an enemy), Tear Gas (suppresses to the cap and slows)", () => TankAltTest(Dt)),
        Test("twinnades", "the Twin Rifle's grenade tubes: hits load two, Alt fires left then right, the order holds through a refill", () => TwinGrenadeTest(Dt)),
        Test("doublebarrel", "the Double Barrel: left then right barrel, 9 pellets each that pierce, two shells then a break-open reload, Alt fires both, damage falls off with range, the other screen sees the reload", () => DoubleBarrelTest(Dt)),
        Test("twinauto", "the Twin Rifle Auto: each gun picks the nearest enemy on its side and fires untouched, Alt locks both on the enemy by the cursor (crossfire) until it dies or Alt is pressed again", () => TwinAutoTest(Dt)),
        Test("outofammo", "Out of Ammo: no shots, the five-move knife chain on a close enemy, one swat per rifle, Alt's spin cuts and swats everything then cools down", () => OutOfAmmoTest(Dt)),
        Test("poisondart", "Poison Dart: the dart at the enemy nearest the cursor, the poison, the spread on death and the chain, mastered, Virulence", () => PoisonDartTest(Dt)),
        Test("airstrike", "Airstrike: a carpet of bombs across the aim, wall to wall, the burning lane, the mastered second wall, Bigger Boom", () => AirstrikeTest(Dt)),
        Test("cavalry", "Cavalry Call: two lancers (four mastered) ride the aim, hit each enemy once, shove, slow and hurt, Heavy ones barely move, the 20% guard", () => CavalryTest(Dt)),
        Test("fireflyroot", "the Sprite Staff's firefly hops and the Treant Rod's root lash", () => FireflyRootTest(Dt)),
        Test("practice", "the Practice Range (with the lobby, modifiers, paths and the Bazaar)", () => PracticeTest(Dt)),
        Test("healersolo", "Healers alone: aura self-heal, overheal shields, Martyr's ward, Phoenix, Adrenaline Shot", () => HealerSoloTest(Dt)),
        Test("momentum", "the DPS role passive Momentum: kill stacks, speed and fire rate, ageing, downed, snapshot, DPS only", () => MomentumTest(Dt)),
        Test("adrenaline", "the Adrenaline Shot: cursor targeting, speed, fire rate, damage cut, expiry, Long Dose, capstone", () => AdrenalineTest(Dt)),
        Test("patterns", "the Pauser, Leader and Stitcher: their shots, threads, sync and bot rules", () => PatternsTest(Dt)),
        Test("difficulty", "difficulty presets and Custom: lobby, sync, saves, and what each number changes", () => DifficultyTest(Dt)),
        Test("readable", "regular waves: shooters take turns, a wind-up, bullet shapes, thinner rings", () => ReadabilityTest(Dt)),
        Test("rulemods", "Hunted, Death Throes, Solid Walls and Second Wind", () => RuleModifiersTest(Dt)),
        Test("runmods", "Head Start, Practiced, Bloodlust, Gold Rush, Tiny Arena and Nightfall", () => MoreModifiersTest(Dt)),
        Test("paths", "divergent paths", () => PathsTest(Dt), () => PathHazardsTest(Dt), () => PathTwistsTest(Dt), () => PathArenaRulesTest(Dt)),
        Test("bazaar", "the Bazaar", () => BazaarTest(Dt)),
        Test("props", "the arena's props: crates as cover, pots, barrels, the treasure chest and its items", () => PropsTest(Dt)),
        Test("timed", "timed waves: enemies arrive over a window (ramp, plateau, taper) and the wave clears when all are dead", () => TimedWavesTest(Dt)),
        Test("portals", "spawn portals and surges: bursts through portals, sealing one cancels the rest, flanks behind the party, the surge rhythm", () => SpawnPortalsTest(Dt)),
        Test("layouts", "arena layouts: the six floor plans, walls and pillars, edge gates, enemy gates, steering round walls", () => LayoutsTest(Dt)),
        Test("wings", "the Rooms arena (phase 1): the Arena setting, the Wings rooms and hallways, doors, walls, spawns, steering, boss waves in the hub", () => RoomsArenaTest(Dt)),
        Test("expand", "the Expanding arena: seeded room offers, no overlaps, connected maps, open doors, the size cap", () => ExpansionTest(), () => ExpandingOutlineTest(Dt), () => ExpandingGameTest(Dt), () => ExpandingEconomyTest(Dt), () => ExpandingFeaturesTest(Dt), () => ExpandingVectorsTest(Dt), () => ExpandingGatekeeperTest(Dt), () => ExpandingKitsTest(Dt), () => ExpandingThemesTest(Dt), () => ExpandingWallsTest(Dt), () => ExpandingFreeBuildTest(Dt), () => ExpandingPurseTest(Dt), () => ExpandingBreachesTest(Dt), () => ExpandingLinksTest(Dt), () => ExpandingGatesTest(Dt), () => ExpandingRoomSizesTest(Dt), () => ExpandingFloatCostTest(), () => ExpandingSurpriseTest(), () => ExpandingSurpriseGameTest(Dt), () => ExpandingBreachEventTest(Dt), () => ExpandingStayInRoomsTest(Dt)),
        Test("stayin", "enemies stay inside the rooms", () => ExpandingStayInRoomsTest(Dt)),
        Test("challenges", "challenge rooms: a portal every five waves, the five challenges, the altar's prize, failing safely", () => ChallengesTest(Dt)),
        Test("bossgate", "the boss gate (BossGate.cs): opens before the boss, holds the wave until everyone stands in it, bots walk in", () => BossGateTest(Dt)),
        Test("quick", "Quick Play (QuickPlay.cs): the default mode, ten faction waves, a random boss after wave 10, its records, endless", () => QuickPlayTest(Dt), () => QuickBuildTest(Dt), () => TutorialTest(Dt), () => QuickRecordTest(Dt), () => QuickBossHpReport()),
        Test("bossschedule", "boss schedules (BossSchedule.cs): the Campaign's and a hunt's match the old formulas", () => BossScheduleTest()),
        Test("huntmap", "the hunt map (HuntMap.cs): rows and lanes, links that never cross, node types, walking and restoring it; played over the network: the lobby switch, the vote, Elite waves", () => HuntMapTest(), () => HuntMapNetTest(Dt)),
        Test("survival", "Survival (Survival.cs): the pressure curve, faction phases, the cap, squads, Elites, the same seed gives the same run", () => SurvivalTest(), () => SurvivalNetTest(Dt)),
        Test("hunts", "Boss Hunts: locked until the boss is beaten, the hunt's waves, winning it, its records", () => HuntTest(Dt)),
        Test("perks", "perks: unlock rules, slots, equipping (one per boss), the profile line; Riot Gear: block, grace, 30 s recharge, burns go through; Contraband: the cycle chest and rare-or-better items", () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt)),
        Test("rank", "the account rank: the curve, XP for a run, modifier multiplier, profile, lobby sharing, a run ending", () => RankTest(Dt)),
        Test("gadgets", "gadgets: the Magnet picked up, switched on (pulls every coin), thrown and caught over the network, used up, dropped when downed", () => GadgetTest(Dt)),
        Test("pings", "pings: tap/wheel marks, host pause and cap, Focus following an enemy, lapsing", () => PingTest(Dt)),
        Test("hangout", "the lobby's Hangout room: everyone's character, moves over the network, the walls", () => HangoutTest(Dt)),
        Test("zombies", "Zombie Infection: melee hordes, the cap, the dead rising", () => ZombieTest(Dt)),
        Test("wavegen", "the built-in designed waves match their files in Data/Waves (Tools/WaveGen/build.sh)", WaveGenTest),
        Test("wavedesign", "the wave editor's designs: the .waves file, and playing them in the Practice Range", () => WaveDesignTest(Dt)),
        Test("clearghosts", "a shot fired in the tick enemy bullets are cleared doesn't linger on the client", () => ClearGhostsTest(Dt)),
        Test("three", "three players", () => ThreePlayerTest(Dt)),
        Test("wrap", "wrapping round the arena's edges", () => WrapTest(Dt)),
        Test("ready", "ready-up in the lobby", () => ReadyTest(Dt)),
        Test("dev", "the F1 dev tools", () => DevToolsTest(Dt)),
        Test("textedit", "the dev text editor's overrides", TextOverridesTest),
        Test("runstats", "the Run stats screen's live numbers reach every player", () => RunStatsLiveTest(Dt)),
        Test("waveready", "the between-waves summary, awards, Ready and Auto-ready", () => WaveReadyTest(Dt)),
        Test("wavekinds", "a recipe wave shows at most 6 distinct enemy types (8 from wave 25)", () => WaveKindsTest()),
        Test("landmarks", "the Phantom's blink, the Mortar's bomb and the Charger's dash end mark where they land", () => LandingMarksTest()),
        Test("newenemies", "the new-enemy chips: which types a wave brings and a note for each", () => NewEnemiesTest()),
        Test("fieldguide", "the Field guide's Bestiary: an entry per enemy, health at your wave", () => FieldGuideTest(Dt)),
        Test("labtest", "the Boss Lab", () => BossLabTest(Dt)),
        Test("botteam", "the smart bot's team play: Roar, Last Stand, Mark, healing orbs and zones", () => BotTeamplayTest(Dt)),
        Test("partybots", "bots in the lobby's open slots", () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); }),
        Test("wildcard", "Wildcard: DPS gear from any class, a second ability on a chosen key, the damage and cooldown price, the unlock", () => WildcardTest(Dt)),
        Test("respawn", "the Practice Range: no game over, Respawn, Restart wave, Training drill text format", () => PracticeRespawnTest(Dt)),
        Test("lag", "the lag chart: LagStats (stalls, p95, 1% low, freeze, summary, CSV) and pings shared over the network", () => LagChartTest(Dt)),
        Test("stacking", "power stacking: diminishing returns on unlimited cards, wounds (a share of each hit no heal brings back this wave)", () => StackingTest(Dt)),
        Test("dodge", "Dodge: the tiny dash with invulnerability (host and client), its cooldown, the step back", () => DodgeTest(Dt)),
        Test("f1bot", "F1 \"Bot plays for you\" (the smart bot) through waves and the Bazaar", () => F1BotTest(Dt)),
        new Suite { Name = "session", What = "session recording (run.sh 1 30 session [copy-to])", Run = args =>
            Seeded("session", () => { var path = SessionTest(Dt); if (args.Length > 3 && path != null) File.Copy(path, args[3], true); }) },
        // Reports and debug tools (no pass/fail).
        Report("chartcheck", "the Campaign's shown waves 1-20 next to the chart in Docs/Approaches.md", args => ChartCheckReport()),
        Report("ttk", "boss time-to-kill over a full run (run.sh 1 1 ttk [last wave])", args => BossTtk(Dt, Arg(args, 3, 50))),
        Report("lab", "Boss Lab batch (run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat])",
            args => LabRun(int.Parse(args[3]), (BotSkill)int.Parse(args[4]), int.Parse(args[5]), args.Skip(6).ToArray())),
        Report("quickfights", "Quick Play boss fights with bots: every boss after waves 10/20/30, its fight length (run.sh 1 1 quickfights [runs] [waves] [skill] [seats])", args => QuickFightsReport(args)),
        Report("levels", "the level a team of Good bots has at each boss", args => LevelReport(Dt)),
        Report("arenabal", "the Rooms arena vs Classic: a duo of Good bots, each regular wave's length, damage taken, threats in view (run.sh 1 1 arenabal [last wave] [seed] [Rooms damage x])", args => ArenaBalanceReport(Dt, Arg(args, 3, 20), Arg(args, 4, 1), args.Length > 5 ? float.Parse(args[5], System.Globalization.CultureInfo.InvariantCulture) : 0f)),
        Report("vecbal", "the Expanding arena's vector economy: a duo of Good bots on Classic vs Expanding, each round's vectors, proposals and rooms built (run.sh 1 1 vecbal [last wave] [seed])", args => VectorBalanceReport(Dt, Arg(args, 3, 25), Arg(args, 4, 1), Arg(args, 5, 0) == 1)),
        Report("rooms", "a duo of Good bots plays with challenge rooms on: how each room went (run.sh 1 1 rooms [last wave] [seed])", args => RoomsReport(Dt, Arg(args, 3, 30), Arg(args, 4, 1))),
        Report("huntbal", "the hunt map's balance: a duo of Good bots plays the same maps on a safe, an Elite and a random route (run.sh 1 1 huntbal [maps] [first seed] [bot skill 1-3] [routes, e.g. safe,elite,random,plain])", args => HuntMapBalanceReport(Dt, Arg(args, 3, 4), Arg(args, 4, 1), (BotSkill)Arg(args, 5, 2), args.Length > 6 ? args[6] : null)),
        Report("survbal", "Survival's balance: bots play Survival to the end or a cap: time survived, phase, level (run.sh 1 1 survbal [runs] [first seed] [bot skill 1-3] [players 1-3] [cap minutes])", args => SurvivalBalanceReport(Dt, Arg(args, 3, 4), Arg(args, 4, 1), (BotSkill)Arg(args, 5, 2), Arg(args, 6, 1), Arg(args, 7, 20))),
        Report("wavemix", "what a regular wave is made of: shooters, melee and support, wave by wave", args => WaveMixReport()),
        Report("perf", "frame cost of a heavy wave: Quick Play from a breach wave with a boss's approach (run.sh 1 1 perf [seconds] [boss] [wave])", args => PerfReport(Dt, Arg(args, 3, 60), args.Length > 4 ? args[4] : "TwinA", Arg(args, 5, 7))),
        Report("bulletcount", "enemy bullets on screen through a wave", args => BulletCountReport(Dt)),
        Report("grid", "the balance grid: every role, class and weapon vs each listed boss (run.sh 1 1 grid <waves> [runs] [roles DTH] [skill] [classes]; Tools/BalanceGrid/grid.sh draws it)", args => BalanceGrid(args)),
        Report("dps", "every weapon's damage per second", args => WeaponDpsReport(Dt)),
        Report("weapondps", "every weapon's damage a second as the lobby shows it; \"write\" saves Defs.WeaponDps's table", args => MeasuredWeaponDps(Dt, args.Contains("write"))),
        Report("weaponsrc", "where each weapon's damage lands in the session recording (weapon vs Other), lowest weapon share first", args => WeaponSourceReport(Dt)),
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
        "swordsman", "nonshooters", "hitfx", "greatsword", "knuckles", "milestone", "mods", "knightdps", "melee", "brute",
        "trickle", "meleesounds", "grunts", "recentruns", "pacman", "longrun", "wardencycle", "checkpoints", "coredrops",
        "exploit", "doublebarrel", "twinauto", "outofammo", "adrenaline", "paths", "bazaar", "timed", "layouts", "wings",
        "expand", "quick", "bossgate", "bossschedule", "rank", "pings", "hangout", "three", "wrap", "ready", "wavekinds", "landmarks",
        "newenemies", "wildcard", "stacking", "singularity", "security", "captain", "wavedump", "spawndump", "aitrace", "hive", "reapercult", "infall", "quarry", "court", "larder", "basicfaction", "penumbral", "squall",
    };

    static int Main(string[] args)
    {
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

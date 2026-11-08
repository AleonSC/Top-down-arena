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

    static int nextPort = 7810;

    // Shared setup for host + friend tests over localhost.
    // HostAs: host a match on `port` with `s` (pass `new Session { AutoStart = 2, StartWave = 3 }` etc.).
    static Session HostAs(Session s, int port, bool bot, string name, Role role, int weapon = 0, int ability = 0, Kit kit = Kit.Soldier)
    {
        if (bot)
            s.Game.Bot = true;
        s.Host(port, name, role, weapon, ability, kit);
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
        var host = new Session { AutoStart = 2, StartWave = wave };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        host.Host(port, "Host", Role.Tank, 0);
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
        }
        return new[] { host, client };
    }

    static bool domesAllowed;  // only the dome test leaves Fortress's dome standing
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
        () => MasteryTest(Dt), () => BudgetHomingTest(Dt), () => BossLabTest(Dt), () => SingularityTest(Dt), () => ColossusTest(Dt), () => LichTest(Dt), () => KnightTest(Dt), () => BearTotemTest(Dt),
        () => WardenTest(Dt), () => QueenTest(Dt), () => ReapersTest(Dt), () => BossSoundTest(Dt), () => WeaponExtraTest(Dt), () => RepulsionTest(Dt), () => QuarterstaffTest(Dt), () => TankKnightTest(Dt), () => KeybindTest(Dt), () => RevolverReloadTest(Dt), () => BowTest(Dt),
        () => PracticeTest(Dt), () => MoreModifiersTest(Dt), () => DuelistTest(Dt), () => HealerSoloTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt), () => WeaponSoundsTest(Dt), () => ShowcaseTest(Dt), () => ClassAbilityTest(Dt), () => NinjaTest(Dt),
        () => BloodMageTest(Dt), TextOverridesTest, () => VeinBeamTest(Dt), () => WardenLootTest(Dt), () => LeechGraspTest(Dt), () => F1BotTest(Dt), () => FlamethrowerTest(Dt), () => HealerKitTest(Dt), () => RadiantOrbTest(Dt), () => StormHammerTest(Dt), () => GolemStaffTest(Dt), () => BoneGuardTest(Dt), () => LightConeTest(Dt), () => SanguineOrbTest(Dt), () => FireballArcTest(Dt), () => ScatterFlakBloodTest(Dt), () => TwinSmgTest(Dt), () => PrismWolfRailTest(Dt), () => MissilesRiotBladesTest(Dt), () => FiveMoreTest(Dt), () => FourMoreTest(Dt), () => HealerFiveTest(Dt), () => LastFourTest(Dt), () => HealerWardsTest(Dt), () => HeraldTest(Dt), () => DevourerTest(Dt), () => EclipseTest(Dt), () => VarietyTest(Dt), () => SavesTest(Dt), () => ProfileTest(Dt), () => UpgradeFixesTest(Dt), () => QueenLootTest(Dt), () => ReaperLootTest(Dt), () => BossPowerTest(Dt), () => FieldGuideTest(Dt), () => DifficultyTest(Dt), () => PatternsTest(Dt), () => RuleModifiersTest(Dt), () => ReadabilityTest(Dt), () => RunStatsLiveTest(Dt), () => WaveReadyTest(Dt), () => PropsTest(Dt), () => ChallengesTest(Dt), ClassCompletenessTest, () => ApproachesTest(Dt), () => HuntTest(Dt), () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt), () => ZombieTest(Dt), () => ClearGhostsTest(Dt), () => WaveDesignTest(Dt), WaveGenTest, () => DpsToolsTest(Dt), () => HealingRainTest(Dt), () => LumenWardTest(Dt), () => FireflyRootTest(Dt), () => NimbusChalkTest(Dt), () => TsunamiTest(Dt), () => HolyCrossTest(Dt), () => CavalryTest(Dt), MusicLibraryTest, CustomMusicTest, ProjectClipsTest, () => DomeTest(Dt), () => AlphaCallTest(Dt), () => AegisTest(Dt), () => GolemWallTest(Dt), () => SanctumTest(Dt), () => ReplayTest(Dt), () => HydraTest(Dt), () => BurrowerTest(Dt), () => WorldTreeTest(Dt), () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); }, () => BotTeamplayTest(Dt), () => DodgeTest(Dt), () => HarvestTest(Dt), () => TitanTest(Dt), () => RainTest(Dt), () => TitheTest(Dt), () => ForcePactTest(Dt),
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
        Test("classes", "every class is complete: name, blurb, icon, look, weapons that fire, abilities that cast (run first when adding a class)", ClassCompletenessTest),
        Test("approaches", "boss approaches: the themed share, waves built with it, the Riot Shield (bash, knockback, shield), the Gas Grenadier, the Warden's pair", () => ApproachesTest(Dt)),
        Test("parry", "the parry and its upgrades", () => ParryTest(Dt), () => ParryUpgradeTest(Dt)),
        Test("replay", "run replays: recording, the file, rename and delete, pause, step, any speed either way, seeking", () => ReplayTest(Dt)),
        Test("golemwall", "Golem Wall: three big golems, no timer, worn by every shot, broken at zero, replaced by a recast", () => GolemWallTest(Dt)),
        Test("aegis", "Aeroaegis: the vortex goes to the player nearest the cursor, enemies stay out", () => AegisTest(Dt)),
        Test("sanctum", "Sanctum: seven healing pulses, the last bigger, each ally healed as the ring reaches them", () => SanctumTest(Dt)),
        Test("alphacall", "Alpha Call: Blood Scent stacks build, lunge at 5, replicate, and clear", () => AlphaCallTest(Dt)),
        Test("dome", "Fortress's dome: enemies and their shots stay out, player shots cross", () => DomeTest(Dt)),
        Test("aim", "aim precision, weapon range, the Frost Lance", () => AimTest(Dt), () => WeaponRangeTest(Dt), () => FrostLanceTest(Dt)),
        Test("weaponsounds", "the Repulsion Cannon's blast and Chain Light's cone and arcs make their own sounds", () => WeaponSoundsTest(Dt)),
        Test("extra", "extra projectiles upgrade every weapon its own way", () => WeaponExtraTest(Dt)),
        Test("storm", "the Storm Hammer", () => StormHammerTest(Dt)),
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
        Test("hitfx", "hit feedback: fire alone doesn't flash an enemy, steady fire softens the flash, Explosive Rounds' blasts are thinned", () => HitFxTest(Dt)),
        Test("upgradefix", "Velocity Rounds land airbursts at the cursor; Incendiary Rounds add up", () => UpgradeFixesTest(Dt)),
        Test("fivemore", "Medicine Darts, Greatsword, Kunai, Wisp Lantern and Imp Caller", () => FiveMoreTest(Dt)),
        Test("fourmore", "Shuriken, Bone Spear, Lightbringer and Treant Rod", () => FourMoreTest(Dt)),
        Test("healerfive", "Censer, Spirit Fan, Ofuda, Sprite Staff and Totem Staff", () => HealerFiveTest(Dt)),
        Test("healingrain", "Healing Rain: heals allies inside, slows enemies, ends on time, mastered lasts 10 s", () => HealingRainTest(Dt)),
        Test("lumenward", "Lumen Ward: a cone of light that heals inside, burns bullets in it and holds the ones outside", () => LumenWardTest(Dt)),
        Test("wards", "Spirit Ward, Healing Mist and Energy Ward", () => HealerWardsTest(Dt)),
        Test("lastfour", "Clot Bolt, Transfusion Darts, Rapier and the Katana (chain, meter, Alt strike)", () => LastFourTest(Dt)),
        Test("repulsion", "the Repulsion Cannon", () => RepulsionTest(Dt)),
        Test("quarterstaff", "the Quarterstaff", () => QuarterstaffTest(Dt)),
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
        Test("ults", "every ultimate over the network", () => UltimateSweep(Dt)),
        Test("showcase", "the lobby's loadout preview", () => ShowcaseTest(Dt)),
        Test("knight", "the Knight", () => KnightTest(Dt)),
        Test("ninja", "the Ninja", () => NinjaTest(Dt)),
        Test("blood", "the Blood Mage", () => VeinBeamTest(Dt), () => LeechGraspTest(Dt), () => BloodMageTest(Dt)),
        Test("melee", "early melee rushers", () => MeleeTest(Dt)),
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
        Test("saves", "saved runs (continuing them, in co-op) and the profile", () => SavesTest(Dt), () => ProfileTest(Dt)),
        Test("colossus", "the Colossus's three stages, stomp rings, plates and last stand", () => ColossusTest(Dt)),
        Test("bosssound", "boss sound cues", () => BossSoundTest(Dt)),
        Test("bosses", "every late boss's mechanics, victory and endless", () => BossMechanics(Dt)),
        Test("latewaves", "waves 25-50: late enemies, each late boss beaten, victory and endless", () => LateWaves(Dt)),
        Test("exploit", "parking a blocker on a boss", () => ExploitTest(Dt)),
        Test("dpstools", "the DPS meter and target dummies", () => DpsToolsTest(Dt)),
        Test("nimbuschalk", "the Nimbus (cloud, rain, strikes) and Spirit Chalk (ink, loops that pen enemies in)", () => NimbusChalkTest(Dt)),
        Test("tsunami", "Tsunami: the wave, wet, the slow, lightning arcing to every wet enemy, wearing off, the mastered return", () => TsunamiTest(Dt)),
        Test("holycross", "Consecrate's Holy Cross: it grows as it flies, heals and hastens allies, hurts, slows and knocks back enemies, hits each once, the mastered fan", () => HolyCrossTest(Dt)),
        Test("harvest", "Crimson Harvest: rips 20 and the blood of every enemy into a ball, a 5 s hold, a second press throws it for a blast that scales with the blood", () => HarvestTest(Dt)),
        Test("tithe", "Vein Tithe: a tendril to every enemy on screen, held enemies frozen for 30 s, a per-enemy health drain, 40% less damage, ends on time or on death", () => TitheTest(Dt)),
        Test("rain", "Sanguine Rain: a 1.8 s siphon costing 12%, rings that heal every ally 3% a tick for 10 s, the healer's 3% drain, cancel by pressing again, the 15% floor", () => RainTest(Dt)),
        Test("cavalry", "Cavalry Call: two lancers (four mastered) ride the aim, hit each enemy once, shove, slow and hurt, Heavy ones barely move, the 20% guard", () => CavalryTest(Dt)),
        Test("fireflyroot", "the Sprite Staff's firefly hops and the Treant Rod's root lash", () => FireflyRootTest(Dt)),
        Test("practice", "the Practice Range (with the lobby, modifiers, paths and the Bazaar)", () => PracticeTest(Dt)),
        Test("healersolo", "Healers alone: aura self-heal, overheal shields, Martyr's ward, Phoenix, Adrenaline Shot", () => HealerSoloTest(Dt)),
        Test("adrenaline", "the Adrenaline Shot: cursor targeting, speed, fire rate, damage cut, expiry, Long Dose, capstone", () => AdrenalineTest(Dt)),
        Test("patterns", "the Pauser, Leader and Stitcher: their shots, threads, sync and bot rules", () => PatternsTest(Dt)),
        Test("difficulty", "difficulty presets and Custom: lobby, sync, saves, and what each number changes", () => DifficultyTest(Dt)),
        Test("readable", "regular waves: shooters take turns, a wind-up, bullet shapes, thinner rings", () => ReadabilityTest(Dt)),
        Test("rulemods", "Hunted, Death Throes, Solid Walls and Second Wind", () => RuleModifiersTest(Dt)),
        Test("runmods", "Head Start, Practiced, Bloodlust, Gold Rush, Tiny Arena and Nightfall", () => MoreModifiersTest(Dt)),
        Test("paths", "divergent paths", () => PathsTest(Dt), () => PathHazardsTest(Dt), () => PathTwistsTest(Dt), () => PathArenaRulesTest(Dt)),
        Test("bazaar", "the Bazaar", () => BazaarTest(Dt)),
        Test("props", "the arena's props: crates as cover, pots, barrels, the treasure chest and its items", () => PropsTest(Dt)),
        Test("layouts", "arena layouts: the six floor plans, walls and pillars, edge gates, enemy gates, steering round walls", () => LayoutsTest(Dt)),
        Test("challenges", "challenge rooms: a portal every five waves, the five challenges, the altar's prize, failing safely", () => ChallengesTest(Dt)),
        Test("hunts", "Boss Hunts: locked until the boss is beaten, the hunt's waves, winning it, its records", () => HuntTest(Dt)),
        Test("perks", "perks: unlock rules, slots, equipping (one per boss), the profile line; Riot Gear: block, grace, 30 s recharge, burns go through; Contraband: the cycle chest and rare-or-better items", () => PerksTest(Dt), () => RiotGearTest(Dt), () => ContrabandTest(Dt)),
        Test("rank", "the account rank: the curve, XP for a run, modifier multiplier, profile, lobby sharing, a run ending", () => RankTest(Dt)),
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
        Test("fieldguide", "the Field guide's Bestiary: an entry per enemy, health at your wave", () => FieldGuideTest(Dt)),
        Test("labtest", "the Boss Lab", () => BossLabTest(Dt)),
        Test("botteam", "the smart bot's team play: Roar, Last Stand, Mark, healing orbs and zones", () => BotTeamplayTest(Dt)),
        Test("partybots", "bots in the lobby's open slots", () => { PartyBotsTest(Dt); PartyBotShelterTest(Dt); }),
        Test("wildcard", "Wildcard: DPS gear from any class, a second ability on a chosen key, the damage and cooldown price, the unlock", () => WildcardTest(Dt)),
        Test("dodge", "Dodge: the tiny dash with invulnerability (host and client), its cooldown, the step back", () => DodgeTest(Dt)),
        Test("f1bot", "F1 \"Bot plays for you\" (the smart bot) through waves and the Bazaar", () => F1BotTest(Dt)),
        new Suite { Name = "session", What = "session recording (run.sh 1 30 session [copy-to])", Run = args =>
            Seeded("session", () => { var path = SessionTest(Dt); if (args.Length > 3 && path != null) File.Copy(path, args[3], true); }) },
        // Reports and debug tools (no pass/fail).
        Report("ttk", "boss time-to-kill over a full run (run.sh 1 1 ttk [last wave])", args => BossTtk(Dt, Arg(args, 3, 50))),
        Report("lab", "Boss Lab batch (run.sh 1 1 lab <wave> <skill 1-3> <runs> <seat> [seat] [seat])",
            args => LabRun(int.Parse(args[3]), (BotSkill)int.Parse(args[4]), int.Parse(args[5]), args.Skip(6).ToArray())),
        Report("levels", "the level a team of Good bots has at each boss", args => LevelReport(Dt)),
        Report("rooms", "a duo of Good bots plays with challenge rooms on: how each room went (run.sh 1 1 rooms [last wave] [seed])", args => RoomsReport(Dt, Arg(args, 3, 30), Arg(args, 4, 1))),
        Report("bulletcount", "enemy bullets on screen through a wave", args => BulletCountReport(Dt)),
        Report("dps", "every weapon's damage per second", args => WeaponDpsReport(Dt)),
        Report("weapondps", "every weapon's damage a second as the lobby shows it; \"write\" saves Defs.WeaponDps's table", args => MeasuredWeaponDps(Dt, args.Contains("write"))),
        Report("upgradecov", "which weapon upgrades each weapon ignores", args => UpgradeCoverageReport(Dt)),
        Report("bosspower", "a typical build's power at each boss wave, and the boss health a strong build would bring", args => BossPowerReport(Dt)),
        Report("balance", "what one pick and a full stack of each damage upgrade is worth: boss, pack, spread wave", args => UpgradeBalanceReport(Dt)),
        Report("ultdps", "the damage each DPS ultimate adds against one big target", args => UltimateDpsReport(Dt)),
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

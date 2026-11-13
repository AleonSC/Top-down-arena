using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Arena;

// Quick Play (Core/QuickPlay.cs, Docs/QuickPlay.md step 1): the lobby's default mode, ten waves of the coming boss's
// faction, a random boss after wave 10 (the Warden on a first Quick Play), its own records, then endless.
static partial class Program
{
    static void QuickPlayTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // The boss order: bags of all ten, no boss twice in a row, the Warden first when asked.
        bool bagsOk = true, wardenFirst = true, othersFirst = false;
        for (int seed = 0; seed < 40; seed++)
        {
            var order = QuickPlay.MakeOrder(new Random(seed), seed % 2 == 0);
            bagsOk &= order.Length == QuickPlay.OrderLength;
            for (int b = 0; b + 10 <= order.Length; b += 10)
                bagsOk &= order.Skip(b).Take(10).Distinct().Count() == 10;
            for (int i = 1; i < order.Length; i++)
                bagsOk &= order[i] != order[i - 1];
            if (seed % 2 == 0)
                wardenFirst &= order[0] == EnemyType.Boss;
            else
                othersFirst |= order[0] != EnemyType.Boss;
        }
        Check(bagsOk && wardenFirst && othersFirst, "the boss order: every boss once per ten, never twice in a row; the Warden first only on a first Quick Play");

        var sched = new QuickBosses(QuickPlay.MakeOrder(new Random(1), true));
        Check(sched.Slotted && !sched.IsBossWave(5) && sched.IsBossWave(10) && sched.IsBossWave(20) && sched.NextBossWaveFrom(1) == 10 && sched.NextBossWaveFrom(11) == 20
            && sched.BossAt(10) == EnemyType.Boss && sched.BossAt(20) == sched.Order[1] && sched.FinaleWave(5) && sched.FinaleWave(10) && sched.FinaleWave(15) && !sched.FinaleWave(9)
            && sched.ApproachStep(1) == 1 && sched.ApproachStep(4) == 2 && sched.ApproachStep(10) == 4 && sched.ApproachStep(11) == 1,
            "Quick Play's schedule: a boss after every tenth wave, the faction's set piece on the fifth and tenth");

        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-quick-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        Session.QuickDefault = true;
        try
        {
            // The lobby: a new profile opens on Quick Play; picking another mode is remembered.
            int port = nextPort++;
            var solo = new Session();
            var sp = solo.Game.ProfileOverride = new Profile();
            solo.Host(port, "Host", Role.Tank);
            bool openedQuick = solo.QuickMode && !solo.HuntMode && !solo.PracticeMode;
            solo.SetMode(false, false, false);
            solo.Leave("");
            solo.Host(port, "Host", Role.Tank);
            Check(openedQuick && sp.LastMode == "campaign" && !solo.QuickMode && Profile.Parse(sp.ToText()).LastMode == "campaign",
                "a new profile's lobby opens on Quick Play; another mode picked is where it opens next time");
            solo.Leave("");

            // The run, in co-op.
            port = nextPort++;
            var host = new Session();
            var hp = host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            var cp = client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            var s = new[] { host, client };
            Action Sync = () => PollBoth(host, client);
            WaitForLobby(host, client);
            Sync();
            Check(host.QuickMode && client.QuickMode, "the friend's lobby shows Quick Play too");
            client.RequestReady(true); host.RequestReady(true); Sync();
            host.StartMatch();
            WaitForGame(host, client, dt);
            var g = host.Game;
            var c = client.Game;
            g.ChallengesOn = true;  // (on, to show Quick Play opens no portal)
            Check(g.Quick && c.Quick && c.QuickOrder != null && c.QuickOrder.SequenceEqual(g.QuickOrder) && g.BossAt(10) == EnemyType.Boss && c.BossAt(10) == EnemyType.Boss
                && !g.LongLegs && g.LastWave == 10 && c.ShownLastWave == 10 && hp.RunsStarted == 0 && g.LivesMax == 0,
                "Quick Play starts on every game with the same boss order (a first Quick Play: the Warden), ten waves, no lives, not a run started");
            var saveable = (bool)typeof(Game).GetProperty("Saveable", flags).GetValue(g, null);
            Check(!saveable, "a Quick Play isn't saved to continue later");

            Action keep = () =>
            {
                foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
                foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && g.Enemies.Count > 0) e.Hp = 0f;
            };
            bool portal = false, pathOffered = false, bossEarly = false;
            var faction = new HashSet<EnemyType>(Approaches.For(EnemyType.Boss).Members.Select(m => m.Type));
            var seen = new HashSet<EnemyType>();
            var others = new HashSet<EnemyType>();
            Run(s, dt, 500f, () => g.Wave == 10 && g.BossSlot && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), () =>
            {
                keep();
                portal |= g.Room != RoomPhase.None;
                pathOffered |= g.PathOffer != null;
                bossEarly |= g.Wave < 10 && g.Enemies.Values.Any(e => e.Def.IsBoss);
                if (!g.BossSlot && g.State == MatchState.Fighting)
                    foreach (var e in g.Enemies.Values)
                        (faction.Contains(e.Type) ? seen : others).Add(e.Type);
            });
            var warden = Find(host, EnemyType.Boss);
            Console.WriteLine("  waves 1-10: the Warden's own {0}; anything else: {1}", string.Join(", ", seen), others.Count == 0 ? "none" : string.Join(", ", others));
            Check(warden != null && g.Wave == 10 && g.BossSlot && c.BossNow && !bossEarly && !portal && !pathOffered,
                "ten regular waves (no boss on wave 5), then the Warden as its own fight after wave 10; no path offer or Data Gate on the way");
            Check(others.Count == 0 && seen.Count >= faction.Count - 1, "the waves before it bring only the Warden's own enemies");
            float expectHp = g.QuickBossHp(EnemyType.Boss, 10) * warden.PowerScale;
            Console.WriteLine("  the Warden after wave 10: {0:0} HP (its Campaign wave 5: {1:0}), power scale {2:0.00}", warden.MaxHp, g.SpawnHp(EnemyType.Boss, 5), warden.PowerScale);
            Check(Math.Abs(warden.MaxHp - expectHp) < 1f && warden.MaxHp > g.SpawnHp(EnemyType.Boss, 5), "its health is set for a two-minute fight at this wave (QuickBossHp)");

            warden.Hp = 0f;
            Run(s, dt, 8f, () => c.State == MatchState.Victory && cp.QuickPlays > 0, keep);
            Console.WriteLine("  won: host quick {0} plays / {1} wins / best wave {2} / {3} bosses; runs won {4}, best run wave {5}; Warden beaten {6}",
                hp.QuickPlays, hp.QuickWins, hp.QuickBestWave, hp.QuickBosses, hp.RunsWon, hp.BestWave, hp.Beaten(EnemyType.Boss));
            Check(g.State == MatchState.Victory && c.State == MatchState.Victory, "beating the boss after wave 10 wins the Quick Play");
            Check(hp.QuickPlays == 1 && hp.QuickWins == 1 && hp.QuickBestWave == 10 && hp.QuickBosses == 1 && cp.QuickWins == 1 && hp.RunsWon == 0 && hp.BestWave == 0
                && hp.Beaten(EnemyType.Boss) == 1 && hp.Recent.Count > 0 && hp.Recent[0].Mode == "Quick Play" && hp.Recent[0].Xp > 0,
                "every profile records it as a Quick Play (XP, the boss beaten), not as a run");
            Check(hp.TutorialDone && cp.TutorialDone && !g.Tutorial.Active, "a first Quick Play recorded ends the tutorial for good");
            var back = Profile.Parse(hp.ToText());
            Check(back.QuickPlays == 1 && back.QuickWins == 1 && back.QuickBestWave == 10 && back.QuickBosses == 1, "Quick Play records read back from the profile's text");

            // Endless: a new random boss after wave 20.
            g.ContinueEndless();
            var second = g.BossAt(20);
            Run(s, dt, 600f, () => g.Wave == 20 && g.BossSlot && g.Enemies.Values.Any(e => e.Def.IsBoss && !g.IsSpawning(e)), () =>
            {
                keep();
                pathOffered |= g.PathOffer != null;
            });
            Console.WriteLine("  endless: wave {0}, boss {1} (order says {2}), client sees {3}", g.Wave, string.Join(",", g.Enemies.Values.Where(e => e.Def.IsBoss).Select(e => e.Type)), second, c.BossAt(20));
            Check(g.Endless && g.Wave == 20 && g.Enemies.Values.Any(e => e.Def.IsBoss && (e.Type == second || Game.IsTwin(e.Type) && Game.IsTwin(second))) && second != EnemyType.Boss
                && c.BossAt(20) == second && !pathOffered, "endless goes on: ten more waves, then the order's next boss (not the Warden again)");

            // The party falls: the endless stretch is added to the records.
            int xpBefore = (int)hp.Xp;
            foreach (var p in g.Players.Values) { p.InvulnT = 0f; }
            g.DevSetGod(1, false); g.DevSetGod(2, false);
            foreach (var p in g.Players.Values) { p.Hp = 0f; p.Downed = true; p.ReviveToken = false; p.SecondWind = false; }
            Run(s, dt, 5f, () => g.State == MatchState.GameOver && cp.QuickBestWave >= 19);
            Console.WriteLine("  fell in endless: host best wave {0}, plays {1}, XP +{2}, mode '{3}'", hp.QuickBestWave, hp.QuickPlays, hp.Xp - xpBefore, hp.Recent[0].Mode);
            Check(g.State == MatchState.GameOver && hp.QuickPlays == 1 && hp.QuickBestWave >= 19 && hp.Xp > xpBefore && hp.Recent[0].Mode == "Quick Play · Endless",
                "falling in endless adds the stretch after the win (best wave, XP), not a second play");
            Check(hp.Recent.Count >= 2 && RunTimelineStore.Load(hp.Recent[1].File) != null && RunTimelineStore.Load(hp.Recent[0].File) != null,
                "the win and the endless stretch after it each keep a curve for Recent runs");
            host.Leave(""); client.Leave("");
        }
        finally
        {
            Session.QuickDefault = false;
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// Quick Play's room building (step 2): the Expanding arena, a build round after every wave, and what bots build in ten waves.
    static void QuickBuildTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session(), port, true, "Host", Role.Tank);
        host.Game.ProfileOverride = new Profile();
        var client = JoinAs(port, true, "Friend", Role.Dps);
        client.Game.ProfileOverride = new Profile();
        var s = new[] { host, client };
        WaitForLobby(host, client);
        PollBoth(host, client);
        host.SetMode(false, false, true);
        PollBoth(host, client);
        client.RequestReady(true); host.RequestReady(true); PollBoth(host, client);
        host.StartMatch();
        WaitForGame(host, client, dt);
        var g = host.Game;
        var c = client.Game;
        Check(g.Quick && g.ExpandingOn && c.ExpandingOn, "Quick Play is played on the Expanding arena (every game knows)");
        Action keep = () =>
        {
            foreach (var p in g.Players.Values) { p.InvulnT = 1f; p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PrizePicks = p.PendingMilestones = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
            foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss && g.Enemies.Count > 0 && g.State == MatchState.Fighting && e.Hp > 0f && g.RunTime % 3f < 0.02f) e.Hp = 0f;
        };
        int lastWave = 0, rounds = 0;
        bool firstRound = false;
        var line = new List<string>();
        Run(s, dt, 900f, () => g.Wave == 10 && g.BossSlot && g.State == MatchState.Fighting, () =>
        {
            keep();
            if (g.State == MatchState.Intermission && g.Wave != lastWave && g.Wave < 10)
            {
                lastWave = g.Wave;
                if (g.ExpandOpen) rounds++;
                if (g.Wave == 1) firstRound = g.ExpandOpen && c.ExpandOpen;
                line.Add(g.Wave + ":" + string.Join("/", g.Players.Values.Select(p => p.Vectors)) + " r" + Expansion.BoughtCount(g.Expanded));
            }
        });
        int built = Expansion.BoughtCount(g.Expanded);
        Console.WriteLine("  vectors (host/friend) and rooms built after each wave: {0}", string.Join("  ", line));
        Console.WriteLine("  by the boss after wave 10: {0} build rounds, {1} rooms built", rounds, built);
        Check(firstRound && rounds >= 8, "a build round opens after wave 1 and after every wave on the way to the boss");
        Check(built >= 3, "two bots build several rooms in a Quick Play's ten waves (vectors keep up)");
        host.Leave(""); client.Leave("");
    }

    /// The first-run tutorial (Core/Tutorial.cs, step 3): on for a profile's first Quick Play, steps tick off from your own
    /// input and the game, skipping turns it off for good.
    static void TutorialTest(float dt)
    {
        string wasFolder = Profile.Folder, dir = Path.Combine(Path.GetTempPath(), "arena-tut-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = dir;
        try
        {
            Func<Profile, bool, Session> start = (prof, quick) =>
            {
                var h = new Session { AutoStart = 2 };
                h.Game.ProfileOverride = prof;
                h.Host(nextPort++, "Host", Role.Tank);
                h.Diff.Set(DiffKnob.Campaign, 0f);
                h.SetMode(false, false, quick);
                h.StartMatch();
                for (int i = 0; i < 400 && h.Game.State != MatchState.Fighting; i++) Step(h, dt);
                return h;
            };
            var pf = new Profile();
            var host = start(pf, true);
            var g = host.Game;
            var tut = g.Tutorial;
            Check(g.Quick && tut.Active && tut.Current == 0, "a profile's first Quick Play turns the tutorial on, at THE BASICS");
            Action<PlayerInput, int> play = (input, frames) =>
            {
                for (int f = 0; f < frames; f++)
                {
                    foreach (var p in g.Players.Values) p.InvulnT = 1f;
                    input.AimAt = new V2(640f + 200f * (float)Math.Sin(f * 0.1f), 360f);
                    host.Poll();
                    g.FixedStep(dt, input);
                    g.Update(dt);
                }
            };
            play(new PlayerInput { Move = new V2(1f, 0f) }, 30);
            bool notYet = !tut.Done(TutorialStep.Move);
            play(new PlayerInput { Move = new V2(1f, 0f), Fire = true }, 60);
            play(new PlayerInput { Dodge = true }, 1);
            Check(notYet && tut.Done(TutorialStep.Move) && tut.Done(TutorialStep.Aim) && tut.Done(TutorialStep.Shoot) && tut.Done(TutorialStep.Dodge) && tut.Current == 1,
                "moving (for a moment), aiming, shooting and dodging tick off THE BASICS; the prompt moves on to LEVEL UP");
            play(new PlayerInput { Ability = true }, 2);
            play(new PlayerInput { Parry = true }, 30);
            Check(tut.Done(TutorialStep.Ability) && tut.Done(TutorialStep.Parry) && tut.Current == 1, "the ability and the parry tick off too (YOUR KIT), but LEVEL UP stays on the prompt until a card is picked");
            g.State = MatchState.Intermission;
            g.ExpandOpen = true;
            tut.ShowAt(2, 0);  // (the basics and LEVEL UP done, a build round open after wave 1)
            bool buildNow = tut.CurrentFor(g) == Tutorial.BuildLesson;
            g.ExpandOpen = false;
            g.State = MatchState.Fighting;
            bool kitInFight = tut.CurrentFor(g) == Tutorial.KitLesson && tut.Current == Tutorial.BuildLesson;
            Check(buildNow && kitInFight, "right after the first card the build round shows BUILD A ROOM; in the next wave the prompt shows YOUR KIT (building comes back with the next round)");
            tut.Note(TutorialStep.OpenBuild);
            tut.Skip(g);
            Check(!tut.Active && pf.TutorialDone && Profile.Parse(pf.ToText()).TutorialDone, "SKIP turns it off, and the profile remembers");
            host.Leave("");
            host = start(pf, true);
            Check(!host.Game.Tutorial.Active, "a profile that has done it doesn't see it again");
            host.Leave("");
            host = start(new Profile(), false);
            Check(!host.Game.Tutorial.Active, "the Campaign never shows it");
            host.Leave("");
        }
        finally
        {
            Profile.Folder = wasFolder;
            try { Directory.Delete(dir, true); } catch (IOException) { }
        }
    }

    /// Each boss's Quick Play health after waves 10, 20 and 30 (a Tank and a DPS), against its Campaign health at its own wave.
    static void QuickBossHpReport()
    {
        var host = new Session();
        var g = host.Game;
        g.Players[1] = new Player { Id = 1, Role = Role.Tank, RoleAbility = Upgrades.RoleAbility(Role.Tank, Kit.Soldier, 0) };
        g.Players[2] = new Player { Id = 2, Role = Role.Dps, RoleAbility = Upgrades.RoleAbility(Role.Dps, Kit.Soldier, 0) };
        Console.WriteLine("  boss          home  HP@home   after 10  after 20  after 30   (x of its home health)");
        foreach (var b in new[] { EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
                                  EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse })
        {
            int home = QuickPlay.HomeWave(b);
            float h0 = g.SpawnHp(b, home);
            Console.WriteLine("  {0,-13} {1,4} {2,8:0}   {3,6:0.00}x  {4,6:0.00}x  {5,6:0.00}x", Defs.Get(b).Name, home, h0,
                g.QuickBossHp(b, 10) / h0, g.QuickBossHp(b, 20) / h0, g.QuickBossHp(b, 30) / h0);
        }
        Check(g.QuickBossHp(EnemyType.Eclipse, 10) < g.SpawnHp(EnemyType.Eclipse, 50) && g.QuickBossHp(EnemyType.Boss, 30) > g.SpawnHp(EnemyType.Boss, 5),
            "a late boss after wave 10 has less health than at its own wave; an early one in endless has more");
    }
}

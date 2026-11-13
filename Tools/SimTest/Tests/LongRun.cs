using System;
using System.Collections;
using System.Linq;
using Arena;

// The 100-wave Campaign (Docs/RunLength.md step 2, Hunts.cs): each track wave is fought twice (two legs), the shown wave
// counts 1-100 with a boss after every tenth, per-wave rewards are halved, and saves keep the leg.
static partial class Program
{
    static void LongRunTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Game.LongRunDefault = true;
        try
        {
            // A save keeps the Campaign and its leg; one from before (no "campaign" line) is the old 50 waves.
            var made = new RunSave { Id = "c1", Wave = 7, LongRun = true, Leg = 0 };
            made.Seats.Add(new SeatSave { Name = "A" });
            var back = RunSave.Parse(made.ToText(), "c1");
            var old = RunSave.Parse(made.ToText().Replace("campaign 0\n", ""), "c0");
            Console.WriteLine("  save: campaign {0} leg {1} shown {2} ({3}); old save campaign {4} shown {5}", back?.LongRun, back?.Leg, back?.ShownWave, back?.Summary, old?.LongRun, old?.ShownWave);
            Check(back != null && back.LongRun && back.Leg == 0 && back.ShownWave == 13 && back.Summary.StartsWith("Wave 14") && old != null && !old.LongRun && old.ShownWave == 7,
                "a Campaign save keeps its leg and counts waves to 100; an older save stays a 50-wave run");

            // A Campaign from wave 1: two legs a track wave, the boss after shown wave 10 (track wave 5), on both games.
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, false, "Host", Role.Tank);
            var client = JoinAs(port, false, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game; var cg = client.Game;
            g.DevSetGod(1, true); g.DevSetGod(2, true);
            Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
            Action Clear = () =>
            {
                NoPicks();
                ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
                ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
            };
            Check(g.LongRun && cg.LongRun && g.LongLegs && g.Pace == 0.5f, "a new run is a 100-wave Campaign on every game");
            Check(g.ExpandingOn && cg.ExpandingOn, "a new Campaign is the Expanding arena on every game");
            var seen = new System.Collections.Generic.List<string>();
            bool clientAgrees = true;
            int lastKey = -1;
            Run(s, dt, 400f, () => g.BossSlot && g.State == MatchState.Fighting, () =>
            {
                Clear();
                if (g.State == MatchState.Fighting)
                {
                    int key = g.Wave * 4 + g.Leg * 2 + (g.BossSlot ? 1 : 0);
                    if (key != lastKey)
                    {
                        lastKey = key;
                        seen.Add(g.BossSlot ? "B" : g.ShownWave.ToString());
                    }
                }
            });
            Run(s, dt, 0.5f, null, NoPicks);
            clientAgrees = cg.Wave == g.Wave && cg.Leg == g.Leg && cg.BossSlot && cg.ShownWave == g.ShownWave;
            Console.WriteLine("  campaign fights: {0}; boss after track wave {1} (shown {2}); client shows {3}", string.Join(" ", seen), g.Wave, g.ShownWave, cg.ShownWave);
            Check(string.Join(" ", seen) == "1 2 3 4 5 6 7 8 9 10 B" && g.Wave == 5 && g.ShownWave == 10, "shown waves 1-10 are track waves 1-5, two legs each, then the boss");
            Check(clientAgrees, "the client sees the same wave, leg and shown number");
            Check(g.RecordWave == 10 && cg.RecordWave == 10, "records count the Campaign's waves (the boss after wave 10 is wave 10)");
            Check(g.ShownLastWave == 100 && g.ShownEnd(50) == 100 && g.ShownStart(6) == 11, "the Campaign is 100 waves, a boss after every tenth");

            // After the boss: shown wave 11 is track wave 6's first leg.
            string trail = "";
            Run(s, dt, 60f, () => g.State == MatchState.Fighting && !g.BossSlot, () => { Clear(); if (g.PathOffer != null) { g.ChoosePath(0); cg.ChoosePath(0); } string k = g.State + "/" + g.Wave + "/" + g.Leg + "/" + g.BossSlot; if (!trail.EndsWith(k)) trail += " " + k; });
            Console.WriteLine("  trail:" + trail);
            Console.WriteLine("  after the boss: track wave {0} leg {1}, shown {2}", g.Wave, g.Leg, g.ShownWave);
            Check(g.Wave == 6 && g.Leg == 0 && g.ShownWave == 11, "after the boss comes shown wave 11, the next track wave's first leg");

            // XP is halved (twice the waves).
            var grant = typeof(Game).GetMethod("GrantXp", flags);
            var me = g.Players[1];
            float before = me.Xp; int lvl = me.Level;
            grant.Invoke(g, new object[] { 20f });
            float got = me.Level == lvl ? me.Xp - before : -1f;
            Console.WriteLine("  20 XP granted in a Campaign gives {0:0.#}", got);
            Check(Math.Abs(got - 10f * me.Stats.XpMult) < 0.01f, "a Campaign gives half the XP");

            // Clear the first leg of wave 6 and quit between waves: the save says leg 0, and continuing plays its second leg.
            Run(s, dt, 60f, () => g.State == MatchState.Intermission && g.Wave == 6 && g.Leg == 0, Clear);
            NoPicks();
            if (g.ShopOpen)  // a path's Bazaar can open on this break (it depends on the random path and card draws): a run saves only once it's shut
            {
                g.Shop(ShopAction.Done, 1);
                cg.Shop(ShopAction.Done, 1);
                Run(s, dt, 0.5f, () => !g.ShopOpen);
            }
            string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arena-longrun-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            SaveStore.Folder = folder;
            try
            {
                g.RunId = RunSave.NewId();
                host.Leave(""); client.Leave("");
                var sv = SaveStore.List().FirstOrDefault();
                Console.WriteLine("  saved: track wave {0} leg {1} campaign {2} ({3})", sv?.Wave, sv?.Leg, sv?.LongRun, sv?.Summary);
                Check(sv != null && sv.LongRun && sv.Wave == 6 && sv.Leg == 0 && sv.Summary.StartsWith("Wave 12"), "leaving after a first leg saves the Campaign there");
                port = nextPort++;
                host = new Session { AutoStart = 2 };
                host.HostResume(sv, port, "Host");
                client = new Session();
                client.Join("127.0.0.1", port, "Pal", Role.Healer);
                s = new[] { host, client };
                WaitForGame(host, client, dt);
                g = host.Game; cg = client.Game;
                g.DevSetGod(1, true); g.DevSetGod(2, true);
                Run(s, dt, 30f, () => g.State == MatchState.Fighting, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
                Run(s, dt, 0.5f);
                Console.WriteLine("  continued: track wave {0} leg {1}, shown {2} (client {3}), campaign {4}/{5}", g.Wave, g.Leg, g.ShownWave, cg.ShownWave, g.LongRun, cg.LongRun);
                Check(g.LongRun && cg.LongRun && g.Wave == 6 && g.Leg == 1 && g.ShownWave == 12 && cg.ShownWave == 12, "continuing plays the second leg, shown wave 12, on every game");
                host.Leave(""); client.Leave("");
            }
            finally
            {
                SaveStore.Folder = null;
                try { System.IO.Directory.Delete(folder, true); } catch (System.IO.IOException) { }
            }

            // Boss Hunts stay ten waves.
            var hunt = new Game(new Session()) { LongRun = true, Hunt = Hunts.All[0] };
            Check(!hunt.LongLegs && hunt.Pace == 1f, "a Boss Hunt isn't a Campaign (ten waves, full rewards)");

            // Records: a profile from before the Campaign has its waves doubled once (50-wave wave n ~ Campaign wave 2n); Boss Hunt lines stay.
            var oldProfile = new Profile { BestWave = 37, BestEndlessWave = 55 };
            oldProfile.Recent.Add(new RankLog { Wave = 30, Result = 1, Mode = "Campaign" });
            oldProfile.Recent.Add(new RankLog { Wave = 10, Result = 3, Mode = "Hunt" });
            string text = oldProfile.ToText();
            var migrated = Profile.Parse(text.Replace("arena-profile " + Profile.FormatVersion, "arena-profile 6"));
            var again = Profile.Parse(migrated.ToText());
            Console.WriteLine("  profile from before: best {0} (endless {1}), run log {2} and hunt {3}; read again: best {4}",
                migrated.BestWave, migrated.BestEndlessWave, migrated.Recent[0].Wave, migrated.Recent[1].Wave, again.BestWave);
            Check(migrated.BestWave == 74 && migrated.BestEndlessWave == 110 && migrated.Recent[0].Wave == 60 && migrated.Recent[1].Wave == 10 && again.BestWave == 74,
                "an older profile's best waves and run log move to the Campaign's scale once (Boss Hunts keep theirs)");

            // The arena (Rooms.cs ArenaKind): a Campaign is the Expanding arena; Simple Arena keeps the open one; a Boss Hunt
            // and an older 50-wave run use the plain arena; the dev dock's Rooms prototype still works.
            var camp = new Game(new Session()) { LongRun = true };
            var simple = new Game(new Session()) { LongRun = true, Mods = RunMod.SimpleArena };
            var oldRun = new Game(new Session()) { LongRun = false };
            var wings = new Game(new Session()) { LongRun = true };
            wings.Diff.Set(DiffKnob.ArenaRooms, 1f);
            Console.WriteLine("  arenas: campaign {0}, simple {1}, hunt {2}, old run {3}, rooms prototype {4}", camp.ArenaKind, simple.ArenaKind, hunt.ArenaKind, oldRun.ArenaKind, wings.ArenaKind);
            Check(camp.ArenaKind == ArenaLayout.Expanding && simple.ArenaKind == ArenaLayout.Single && hunt.ArenaKind == ArenaLayout.Single
                && oldRun.ArenaKind == ArenaLayout.Single && wings.ArenaKind == ArenaLayout.Wings,
                "the Campaign is the Expanding arena; Simple Arena, a Boss Hunt and an older run are the open arena; the Rooms prototype still works");
        }
        finally
        {
            Game.LongRunDefault = false;
        }
    }

    /// Report + check: the party's level at the first boss, in a 50-wave run and in a Campaign (bots, everything killed
    /// at once so only the XP pace differs). Within about 10%.
    static void LongRunPaceTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var levels = new float[2];
        for (int mode = 0; mode < 2; mode++)
        {
            Game.LongRunDefault = mode == 1;
            try
            {
                int port = nextPort++;
                var host = HostAs(new Session { AutoStart = 2, StartWave = 1 }, port, false, "Host", Role.Tank);
                var client = JoinAs(port, false, "Friend", Role.Dps);
                var s = new[] { host, client };
                WaitForGame(host, client, dt);
                var g = host.Game;
                g.DevSetGod(1, true); g.DevSetGod(2, true);
                Run(s, dt, 900f, () => g.BossSlot && g.State == MatchState.Fighting, () =>
                {
                    foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; }
                    foreach (var e in g.Enemies.Values) if (!e.Def.IsBoss) e.Hp = 0f;
                });
                levels[mode] = (float)g.Players.Values.Average(p => p.Level + p.Xp / Defs.XpToNext(p.Level));
                Console.WriteLine("  {0}: at the first boss (shown wave {1}) the party is level {2:0.00}", mode == 1 ? "Campaign (100)" : "track (50)", g.ShownWave, levels[mode]);
                host.Leave(""); client.Leave("");
            }
            finally
            {
                Game.LongRunDefault = false;
            }
        }
        Check(Math.Abs(levels[1] - levels[0]) <= 0.1f * levels[0], "the party reaches the first boss at about the same level in both (within 10%)");
    }
}

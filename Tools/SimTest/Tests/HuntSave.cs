using System;
using System.IO;
using System.Linq;
using Arena;

// A Boss Hunt saves once, at its step-10 checkpoint (HuntMapGame.cs, SaveData.cs, Docs/LongHunt.md), and Continue picks it up there.
static partial class Program
{
    static void HuntSaveTest(float dt)
    {
        string folder = Path.Combine(Path.GetTempPath(), "arena-huntsave-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        SaveStore.Folder = folder;
        try
        {
            // The save's text reads back the same.
            var made = new RunSave { Id = "h1", Wave = 10, IsHunt = true, HuntBoss = EnemyType.Warden, HuntLength = 20, HuntSeed = 4242, HuntClues = 3, RunTime = 301f };
            made.HuntPath.AddRange(new[] { 0, 1, 1, 0, 2, 1, 0, 0, 1, 0 });
            made.Seats.Add(new SeatSave { Name = "A", Role = Role.Tank, Kit = Kit.Knight });
            var back = RunSave.Parse(made.ToText(), "h1");
            Check(back != null && back.IsHunt && back.HuntBoss == EnemyType.Warden && back.HuntLength == 20 && back.HuntSeed == 4242 && back.HuntClues == 3 && back.HuntPath.SequenceEqual(made.HuntPath) && back.Summary.Contains("step 10 of 20"),
                "a hunt's save reads back (boss, length, map seed, clues, cleared path)");
            Check(!RunSave.Parse(new RunSave { Id = "c1", Wave = 4, Seats = { new SeatSave() } }.ToText()).IsHunt, "a Campaign save isn't a hunt");

            // The 20-step hunt keeps its own records beside the ten-step one's; a perk's unlock counts both.
            var rp = new Profile();
            rp.HuntEnded(EnemyType.Warden, true, 500f, false, true);
            rp.HuntEnded(EnemyType.Warden, false, 90f, false, true);
            var rb = Profile.Parse(rp.ToText());
            Check(rp.HuntWon(EnemyType.Warden, true) == 1 && rp.HuntPlayed(EnemyType.Warden, true) == 2 && rp.HuntWon(EnemyType.Warden) == 0 && rp.HuntPlayed(EnemyType.Warden) == 0 && Math.Abs(rp.HuntBestTime(EnemyType.Warden, true) - 500f) < 0.01f && rp.HuntBestTime(EnemyType.Warden) == 0f,
                "a 20-step hunt counts in its own records, not the ten-step ones");
            Check(rp.HuntWins(EnemyType.Warden) == 1 && rp.HuntsWonCount == 1 && Perks.Unlocked(rp, PerkId.RiotGear), "and still unlocks the boss's perk");
            rp.HuntEnded(EnemyType.Warden, true, 300f);
            Check(rp.HuntsWonCount == 1 && rp.HuntWins(EnemyType.Warden) == 2, "a boss's two hunt lengths count as one hunt won");
            Check(rb.Valid && rb.HuntWon(EnemyType.Warden, true) == 1 && rb.HuntPlayed(EnemyType.Warden, true) == 2 && Math.Abs(rb.HuntBestTime(EnemyType.Warden, true) - 500f) < 0.01f, "the long records read back from the profile text");

            // Play a long hunt with a duo of bots until the miniboss falls.
            Game.SeedForTests(3);
            var host = new Session { AutoStart = 2, StartWave = 1 };
            var hp = host.Game.ProfileOverride = new Profile();
            hp.BossBeaten(EnemyType.Warden, 5);
            host.Game.Bot = true; host.Game.BotLevel = (BotSkill)2; host.Game.BotRerolls = false;
            int port = nextPort++;
            host.Host(port, "A", Role.Tank, 0);
            host.SetMode(GameMode.Hunt);
            host.Game.HuntMapSeed = 3;
            var c = new Session(); c.Game.Bot = true; c.Game.BotLevel = (BotSkill)2; c.Game.BotRerolls = false;
            c.Game.ProfileOverride = new Profile();
            c.Join("127.0.0.1", port, "B", Role.Dps, 0);
            WaitForGame(host, c, dt);
            var g = host.Game;
            Check(host.HuntLong && g.HuntLength == Hunts.LongLength && g.Hunt != null && g.HuntMapOn, "a Boss Hunt is the 20-step map");
            bool savedEarly = false;
            float idle = 0f;
            var s = new[] { host, c };
            for (int f = 0; f < 60 * 60 * 40 && !g.HuntCheckpointed && g.State != MatchState.GameOver && g.State != MatchState.Victory; f++)
            {
                Step(host, dt); Step(c, dt);
                idle = g.State == MatchState.Fighting && g.Foes() == 0 ? idle + dt : 0f;
                if (idle > 20f) { foreach (var pr in g.Props.Values.Where(pr => pr.Kind == PropKind.Chest).ToList()) g.Props.Remove(pr.Id); g.Loots.Clear(); idle = 0f; }
                if (g.HuntRoute != null && g.HuntRoute.Step < 10 && SaveStore.List().Count > 0) savedEarly = true;
            }
            Check(g.HuntCheckpointed && !savedEarly, "the hunt is saved at its checkpoint and not before (ended " + g.State + ", step " + (g.HuntRoute == null ? -1 : g.HuntRoute.Step) + ")");
            if (!g.HuntCheckpointed) { host.Leave(""); c.Leave(""); return; }
            Run(s, dt, 1f);
            var sv = SaveStore.List().FirstOrDefault();
            int wantLevel = g.Players.Values.First(p => p.Name == "A").Level, wantClues = g.HuntRoute.Clues, wantGold = g.Players.Values.First(p => p.Name == "A").Gold;
            Console.WriteLine("  checkpoint: {0}", sv == null ? "none" : sv.Summary);
            Check(sv != null && sv.IsHunt && sv.Wave == 10 && sv.HuntLength == 20 && sv.HuntPath.Count == 10 && sv.HuntClues == wantClues && sv.Seats.Count == 2 && sv.HuntBoss == EnemyType.Warden,
                "the checkpoint is a hunt save at step 10 with both seats");
            // Quitting later doesn't overwrite it.
            Run(s, dt, 40f);
            host.Leave(""); c.Leave("");
            var after = SaveStore.List().FirstOrDefault();
            Check(after != null && after.Wave == 10 && after.HuntPath.Count == 10, "leaving later in the hunt keeps the step-10 save as it was");

            // Continue it.
            port = nextPort++;
            Game.SeedForTests(9);
            var h2 = new Session { AutoStart = 2 };
            var hp2 = h2.Game.ProfileOverride = new Profile(); hp2.BossBeaten(EnemyType.Warden, 5);
            h2.Game.Bot = true; h2.Game.BotLevel = (BotSkill)2; h2.Game.BotRerolls = false;
            Check(h2.HostResume(after, port, "A") && h2.Mode == GameMode.Hunt && h2.PlayMode == GameMode.Hunt && h2.HuntLong && h2.HuntBoss == EnemyType.Warden, "continuing a hunt save opens the Boss Hunt lobby");
            var c2 = new Session(); c2.Game.Bot = true; c2.Game.BotLevel = (BotSkill)2; c2.Game.BotRerolls = false;
            c2.Game.ProfileOverride = new Profile();
            c2.Join("127.0.0.1", port, "B", Role.Dps, 0);
            WaitForGame(h2, c2, dt);
            Check(c2.PlayMode == GameMode.Hunt && c2.HuntMode, "the friend's lobby plays the hunt too");
            var g2 = h2.Game; var cg = c2.Game;
            var s2 = new[] { h2, c2 };
            Run(s2, dt, 0.5f);
            var a2 = g2.Players.Values.First(p => p.Name == "A");
            Check(g2.Running && g2.Resumed && g2.Hunt != null && g2.HuntLength == 20 && g2.HuntRoute != null && g2.HuntRoute.Step == 10 && g2.HuntRoute.Clues == wantClues && g2.Wave == 10 && cg.HuntRoute != null && cg.HuntRoute.Step == 10 && cg.HuntRoute.Clues == wantClues,
                "the hunt resumes at the vote for step 11, with its map and clues, on both games");
            Check(a2.Level == wantLevel && a2.Gold == wantGold, "the build and gold come back");
            Run(s2, dt, 60f * 40f, () => g2.State == MatchState.GameOver || g2.State == MatchState.Victory, () =>
            {
                if (g2.State == MatchState.Fighting && g2.Foes() == 0) { foreach (var pr in g2.Props.Values.Where(pr => pr.Kind == PropKind.Chest).ToList()) g2.Props.Remove(pr.Id); g2.Loots.Clear(); }
            });
            Console.WriteLine("  resumed hunt ended: {0}, step {1}", g2.State, g2.HuntRoute == null ? -1 : g2.HuntRoute.Step);
            Check((g2.State == MatchState.GameOver || g2.State == MatchState.Victory) && SaveStore.List().Count == 0, "a hunt that ends (won or lost) loses its save");
            h2.Leave(""); c2.Leave("");
        }
        finally
        {
            SaveStore.Folder = null;
            try { Directory.Delete(folder, true); } catch { }
        }
    }
}

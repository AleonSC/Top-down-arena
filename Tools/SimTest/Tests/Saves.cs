using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Saved runs (continue later, in co-op too) and the player's profile (RunSaves.cs, SaveData.cs, Profile.cs).
static partial class Program
{
    static void SavesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        string folder = Path.Combine(Path.GetTempPath(), "arena-saves-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        SaveStore.Folder = folder;
        try
        {
            // A save reads back the same, with upgrades by name; unknown lines and names are skipped.
            var made = new RunSave { Id = "t1", Wave = 12, Score = 3456, Mods = RunMod.Lucky | RunMod.Frenzy, Endless = false, RunTime = 754.5f,
                CurrentPath = PathId.Bazaar, PathFrom = 11, PathTo = 14, LastPath = PathId.Bazaar };
            made.Route.Add(PathId.SwarmFields); made.Route.Add(PathId.Bazaar);
            var seat = new SeatSave { Name = "Anthony", Role = Role.Healer, Kit = Kit.Ninja, Weapon = 2, Ability = 1, Level = 9, Xp = 41.5f, Rerolls = 3, Gold = 120, ReviveToken = true, HpFrac = 0.75f };
            seat.Taken.Add(Upgrades.All[0].Name); seat.Taken.Add(Upgrades.All[5].Name);
            seat.BonusKeys[1] = AbilityId.Dash.ToString();
            seat.Mastery["Dash"] = 123.5f;
            seat.Run.Kills = 77; seat.Run.DamageDealt = 9876.5f;
            made.Seats.Add(seat);
            var back = RunSave.Parse(made.ToText() + "\nsomething-new 42\n", "t1");
            var bs = back?.Seats.FirstOrDefault();
            Console.WriteLine("  save text: {0} lines, read back wave {1}, seat {2} ({3}), {4} upgrades, mastery {5}", made.ToText().Split('\n').Length, back?.Wave, bs?.Name, bs?.Summary, bs?.Taken.Count, bs?.Mastery.Values.FirstOrDefault());
            Check(back != null && back.Wave == 12 && back.Score == 3456 && back.Mods == made.Mods && back.CurrentPath == PathId.Bazaar && back.PathTo == 14 && back.Route.Count == 2
                && bs.Role == Role.Healer && bs.Kit == Kit.Ninja && bs.Weapon == 2 && bs.Level == 9 && bs.Gold == 120 && bs.ReviveToken && Math.Abs(bs.HpFrac - 0.75f) < 0.01f
                && bs.Taken.SequenceEqual(seat.Taken) && bs.BonusKeys[1] == "Dash" && Math.Abs(bs.Mastery["Dash"] - 123.5f) < 0.01f && bs.Run.Kills == 77,
                "a saved run's text reads back the same (upgrades and abilities by name)");
            Check(RunSave.Parse("not a save") == null && RunSave.Parse("") == null, "a file that isn't a save isn't read as one");

            // The host saves as each wave starts; the save has every seat and their picks.
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 3 };
            host.Game.Bot = true;
            host.Host(port, "Host", Role.Tank, 0, 0, Kit.Knight);
            var client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Friend", Role.Dps, 1, 0, Kit.Mage);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            var g = host.Game;
            int fortitude = Array.FindIndex(Upgrades.All, u => u.Name == "Fortitude");
            g.GrantUpgrade(2, fortitude); g.GrantUpgrade(2, fortitude);
            Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 3);
            var saves = SaveStore.List();
            var sv = saves.FirstOrDefault();
            var friendSeat = sv?.Seats.FirstOrDefault(x => x.Name == "Friend");
            Console.WriteLine("  autosave: {0} file(s); wave {1}, seats {2}, the friend's upgrades {3} (in game {4})", saves.Count, sv?.Wave, sv == null ? "" : string.Join(", ", sv.Seats.Select(x => x.Name + " " + x.Summary)), friendSeat?.Taken.Count, g.Players[2].Taken.Count);
            Check(saves.Count == 1 && sv.Wave == 2 && sv.Seats.Count == 2 && sv.Id == g.RunId, "the host saves the run as a wave starts (one slot per run)");
            Check(friendSeat != null && friendSeat.Kit == Kit.Mage && friendSeat.Weapon == 1 && friendSeat.Taken.Count == g.Players[2].Taken.Count && friendSeat.Taken.Count(t => t == "Fortitude") >= 2,
                "each seat keeps its class, weapon and picks");

            // Play on a little, give the friend some mastery and gold, then everyone leaves between waves: saved there.
            Run(s, dt, 40f, () => g.State == MatchState.Intermission && g.Wave == 3 && !g.AnyonePicking());
            var me2 = g.Players[2];
            me2.MasteryXp[me2.RoleAbility] = 75f;
            me2.Gold = 55;
            int wantLevel = me2.Level, wantTaken = me2.Taken.Count, wantScore = g.Score;
            host.Leave("");  // the host first, so the save still has both seats
            client.Leave("");
            sv = SaveStore.List().FirstOrDefault();
            Console.WriteLine("  quit between waves: saved wave {0}, the friend level {1} with {2} upgrades, gold {3}, mastery {4}", sv?.Wave, sv?.Seats[1].Level, sv?.Seats[1].Taken.Count, sv?.Seats[1].Gold, sv?.Seats[1].Mastery.Values.FirstOrDefault());
            Check(sv != null && sv.Wave == 3 && sv.Seats[1].Taken.Count == wantTaken && sv.Seats[1].Gold == 55 && sv.Score == wantScore, "leaving between waves saves the run there");

            // Continue it: the host opens the saved run; the friend joins under another name and gets the free seat.
            port = nextPort++;
            host = new Session { AutoStart = 2 };
            host.Game.Bot = true;
            host.HostResume(sv, port, "Host");
            client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Pal", Role.Healer, 0, 0, Kit.Summoner);
            s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            Run(s, dt, 0.5f);
            g = host.Game;
            var c = client.Game;
            var pal = g.Players.Values.FirstOrDefault(p => p.Name == "Pal");
            var palC = c.Players.Values.FirstOrDefault(p => p.Name == "Pal");
            Console.WriteLine("  continued: wave {0} (client {1}), score {2} (client {3}); Pal is a {4} {5}, level {6} (client {7}), {8} upgrades (client {9}), gold {10}, mastery {11:0}",
                g.Wave, c.Wave, g.Score, c.Score, pal?.Role, pal == null ? "" : Defs.KitName(pal.Kit), pal?.Level, palC?.Level, pal?.Taken.Count, palC?.Taken.Count, pal?.Gold, pal == null ? 0f : pal.MasteryXp.Values.FirstOrDefault());
            Check(g.Running && g.Resumed && c.Resumed && g.Wave == 3 && c.Wave == 3 && g.Score == wantScore && c.Score == wantScore, "continuing starts the next wave with the run's score, on both games");
            Check(pal != null && pal.Role == Role.Dps && pal.Kit == Kit.Mage && pal.Level == wantLevel && palC?.Level == wantLevel && pal.Taken.Count == wantTaken && palC?.Taken.Count == wantTaken
                && pal.Gold == 55 && pal.MasteryXp.Values.Any(v => v >= 74f), "a player in a seat gets back its class, level, picks, gold and mastery (seen on their own game)");
            Run(s, dt, 15f, () => g.State == MatchState.Fighting);
            Check(g.Wave == 4 && SaveStore.List().Count == 1 && SaveStore.List()[0].Id == sv.Id, "the continued run plays on and keeps saving to its slot");

            // A run that ends loses its save.
            foreach (var p in g.Players.Values) { p.Downed = true; p.ReviveToken = false; }
            typeof(Game).GetMethod("CheckGameOver", flags).Invoke(g, null);
            Check(g.State == MatchState.GameOver && SaveStore.List().Count == 0, "a run that's over is no longer saved");
            host.Leave(""); client.Leave("");

            // A saved run's seats: names first, then whatever's free; a full run turns more players away.
            port = nextPort++;
            host = new Session();
            var two = RunSave.Parse(made.ToText());
            two.Seats.Add(new SeatSave { Name = "Friend", Role = Role.Tank, Kit = Kit.Knight });
            two.Id = "seats";
            host.HostResume(two, port, "Friend");
            var c2 = new Session();
            c2.Join("127.0.0.1", port, "Late", Role.Dps);  // no seat has this name: it takes the free one...
            for (int i = 0; i < 100; i++) { Step(host, dt); Step(c2, dt); Thread.Sleep(1); }
            bool lateSat = host.Lobby.Values.Any(e => e.Name == "Late" && e.Seat == 0);
            var c1 = new Session();
            c1.Join("127.0.0.1", port, "Anthony", Role.Dps);  // ...until the seat's owner arrives
            for (int i = 0; i < 200; i++) { Step(host, dt); Step(c1, dt); Step(c2, dt); Thread.Sleep(1); }
            var hostSeat = host.Lobby[1].Seat;
            var anthony = host.Lobby.Values.FirstOrDefault(e => e.Name == "Anthony");
            Console.WriteLine("  seats: host 'Friend' took seat {0}; 'Late' sat in the free seat {1}; 'Anthony' took it back, seat {2} as a {3}; 'Late' was {4} ({5})", hostSeat, lateSat, anthony?.Seat, anthony?.Role, c2.Screen, c2.Status);
            Check(hostSeat == 1 && anthony?.Seat == 0 && anthony.Role == Role.Healer && anthony.Kit == Kit.Ninja && c1.Resume != null && c1.Resume.Seats.Count == 2, "players take back the seats with their names (and see the run's seats)");
            Check(lateSat && c2.Screen == AppScreen.Menu && c2.Status.Contains("belongs to Anthony"), "someone without a seat of their own sits in a free one, and makes way for its owner");
            c1.RequestRole(Role.Tank);
            for (int i = 0; i < 30; i++) { Step(host, dt); Step(c1, dt); Thread.Sleep(1); }
            Check(host.Lobby.Values.First(e => e.Name == "Anthony").Role == Role.Healer, "a seat's class can't be changed in the lobby");
            host.Leave(""); c1.Leave(""); c2.Leave("");
        }
        finally
        {
            SaveStore.Folder = null;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    static void ProfileTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        // The profile's text reads back the same.
        var pr = new Profile { RunsStarted = 5, RunsWon = 1, RunsLost = 3, BestWave = 37, BestScore = 9000, BestEndlessWave = 0, PlayTime = 4000f, Kills = 812 };
        pr.BossBeaten(EnemyType.HiveQueen, 10); pr.BossBeaten(EnemyType.HiveQueen, 10);
        pr.Unlocked.Add("something");
        var pb = Profile.Parse(pr.ToText());
        Check(pb.RunsStarted == 5 && pb.RunsWon == 1 && pb.RunsLost == 3 && pb.BestWave == 37 && pb.Kills == 812 && pb.Beaten(EnemyType.HiveQueen) == 2 && pb.IsUnlocked("something"),
            "a profile's text reads back the same");

        // Each player's own game keeps their own profile: a boss beaten and the run's end count for both.
        var test = new UnlockDef { Id = "test-loss", Name = "Test", Desc = "Lose a run", Earned = p => p.RunsLost > 0 };
        Unlocks.All.Add(test);
        try
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 5 };
            var hp = host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            var cp = client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            var g = host.Game;
            Run(s, dt, 20f, () => g.State == MatchState.Fighting && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; });
            Find(host, EnemyType.Boss).Hp = 0f;
            Run(s, dt, 3f, () => cp.Beaten(EnemyType.Boss) > 0, () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; });
            Console.WriteLine("  profiles: runs started host {0} client {1}; the Warden beaten host {2} client {3}; best wave {4}/{5}", hp.RunsStarted, cp.RunsStarted, hp.Beaten(EnemyType.Boss), cp.Beaten(EnemyType.Boss), hp.BestWave, cp.BestWave);
            Check(hp.RunsStarted == 1 && cp.RunsStarted == 1 && hp.Beaten(EnemyType.Boss) == 1 && cp.Beaten(EnemyType.Boss) == 1 && cp.BestWave == 5, "every player's profile counts the run and the boss");
            foreach (var p in g.Players.Values) { p.Downed = true; p.ReviveToken = false; }
            typeof(Game).GetMethod("CheckGameOver", flags).Invoke(g, null);
            Run(s, dt, 1f, () => cp.RunsLost > 0);
            Console.WriteLine("  run lost: host {0} lost, client {1} lost as {2}; unlock earned host {3} client {4}, toast on the client {5}",
                hp.RunsLost, cp.RunsLost, string.Join(",", cp.RunsAs.Keys), hp.IsUnlocked("test-loss"), cp.IsUnlocked("test-loss"), client.Game.UnlockToasts.Count);
            Check(hp.RunsLost == 1 && cp.RunsLost == 1 && cp.RunsAs.ContainsKey(Profile.ClassKey(Role.Dps, Kit.Soldier)), "the run's end counts for every player, as the class they played");
            Check(hp.IsUnlocked("test-loss") && cp.IsUnlocked("test-loss") && client.Game.UnlockToasts.Any(t => t.Unlock.Id == "test-loss"), "an unlock's condition met: it's earned, with a toast");
            host.Leave(""); client.Leave("");

            // A run with dev tools used, or the bot playing, doesn't count.
            port = nextPort++;
            host = new Session { StartWave = 5, AutoStart = 1 };
            var dp = host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            host.StartMatch();
            host.Game.DevSetGod(1, true);
            for (int i = 0; i < 20 * 60 && !(Find(host, EnemyType.Boss) != null && !host.Game.IsSpawning(Find(host, EnemyType.Boss))); i++) Step(host, dt);
            Find(host, EnemyType.Boss).Hp = 0f;
            for (int i = 0; i < 60; i++) Step(host, dt);
            Check(dp.RunsStarted == 1 && dp.Beaten(EnemyType.Boss) == 0, "a run where dev tools were used doesn't count towards records");
            host.Leave("");
        }
        finally
        {
            Unlocks.All.Remove(test);
        }
    }
}

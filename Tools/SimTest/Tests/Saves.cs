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
            seat.BonusKeys[1] = AbilityId.PoisonDart.ToString();
            seat.Mastery["PoisonDart"] = 123.5f;
            seat.Run.Kills = 77; seat.Run.DamageDealt = 9876.5f;
            made.Seats.Add(seat);
            var back = RunSave.Parse(made.ToText() + "\nsomething-new 42\n", "t1");
            var bs = back?.Seats.FirstOrDefault();
            Console.WriteLine("  save text: {0} lines, read back wave {1}, seat {2} ({3}), {4} upgrades, mastery {5}", made.ToText().Split('\n').Length, back?.Wave, bs?.Name, bs?.Summary, bs?.Taken.Count, bs?.Mastery.Values.FirstOrDefault());
            Check(back != null && back.Wave == 12 && back.Score == 3456 && back.Mods == made.Mods && back.CurrentPath == PathId.Bazaar && back.PathTo == 14 && back.Route.Count == 2
                && bs.Role == Role.Healer && bs.Kit == Kit.Ninja && bs.Weapon == 2 && bs.Level == 9 && bs.Gold == 120 && bs.ReviveToken && Math.Abs(bs.HpFrac - 0.75f) < 0.01f
                && bs.Taken.SequenceEqual(seat.Taken) && bs.BonusKeys[1] == "PoisonDart" && Math.Abs(bs.Mastery["PoisonDart"] - 123.5f) < 0.01f && bs.Run.Kills == 77,
                "a saved run's text reads back the same (upgrades and abilities by name)");
            // Saves from before the classes were renamed in the code (Kit.Mage, Kit.BloodMage) still load.
            seat.Kit = Kit.Elementalist;
            var oldMage = RunSave.Parse(made.ToText().Replace("Healer Elementalist ", "Healer Mage "));
            seat.Kit = Kit.Hemomancer;
            var oldBlood = RunSave.Parse(made.ToText().Replace("Healer Hemomancer ", "Healer BloodMage "));
            seat.Kit = Kit.Ninja;
            Check(made.ToText().Contains("Healer Ninja ") && oldMage?.Seats[0].Kit == Kit.Elementalist && oldBlood?.Seats[0].Kit == Kit.Hemomancer,
                "saves that call the classes Mage and BloodMage (before the rename) load as the Elementalist and Hemomancer");
            Check(RunSave.Parse("not a save") == null && RunSave.Parse("") == null, "a file that isn't a save isn't read as one");
            {
                var t = new RunSave { Id = "trashme", Wave = 5 };
                t.Seats.Add(new SeatSave { Name = "A" });
                SaveStore.Write(t);
                bool moved = SaveStore.Trash("trashme");
                Check(moved && SaveStore.List().All(x => x.Id != "trashme") && System.IO.File.Exists(System.IO.Path.Combine(folder, "deleted", "trashme.run")), "DEL moves a save to deleted/ instead of erasing it");
                System.IO.File.Delete(System.IO.Path.Combine(folder, "deleted", "trashme.run"));
            }

            // The host saves as each wave starts; the save has every seat and their picks.
            int port = nextPort++;
            var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, true, "Host", Role.Tank, 0, 0, Kit.Knight);
            var client = JoinAs(port, true, "Friend", Role.Dps, 1, 0, Kit.Elementalist);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
            var g = host.Game;
            int fortitude = Array.FindIndex(Upgrades.All, u => u.Name == "Fortitude");
            g.GrantUpgrade(2, fortitude); g.GrantUpgrade(2, fortitude);
            Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 3);
            var saves = SaveStore.List();
            var sv = saves.FirstOrDefault();
            var friendSeat = sv?.Seats.FirstOrDefault(x => x.Name == "Friend");
            Console.WriteLine("  autosave: {0} file(s); wave {1}, seats {2}, the friend's upgrades {3} (in game {4})", saves.Count, sv?.Wave, sv == null ? "" : string.Join(", ", sv.Seats.Select(x => x.Name + " " + x.Summary)), friendSeat?.Taken.Count, g.Players[2].Taken.Count);
            Check(saves.Count == 1 && sv.Wave == 2 && sv.Seats.Count == 2 && sv.Id == g.RunId, "the host saves the run as a wave starts (one slot per run)");
            Check(friendSeat != null && friendSeat.Kit == Kit.Elementalist && friendSeat.Weapon == 1 && friendSeat.Taken.Count == g.Players[2].Taken.Count && friendSeat.Taken.Count(t => t == "Fortitude") >= 2,
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
            WaitForGame(host, client, dt);
            Run(s, dt, 0.5f);
            g = host.Game;
            var c = client.Game;
            var pal = g.Players.Values.FirstOrDefault(p => p.Name == "Pal");
            var palC = c.Players.Values.FirstOrDefault(p => p.Name == "Pal");
            Console.WriteLine("  continued: wave {0} (client {1}), score {2} (client {3}); Pal is a {4} {5}, level {6} (client {7}), {8} upgrades (client {9}), gold {10}, mastery {11:0}",
                g.Wave, c.Wave, g.Score, c.Score, pal?.Role, pal == null ? "" : Defs.KitName(pal.Kit), pal?.Level, palC?.Level, pal?.Taken.Count, palC?.Taken.Count, pal?.Gold, pal == null ? 0f : pal.MasteryXp.Values.FirstOrDefault());
            Check(g.Running && g.Resumed && c.Resumed && g.Wave == 3 && c.Wave == 3 && g.Score == wantScore && c.Score == wantScore, "continuing starts the next wave with the run's score, on both games");
            Check(pal != null && pal.Role == Role.Dps && pal.Kit == Kit.Elementalist && pal.Level == wantLevel && palC?.Level == wantLevel && pal.Taken.Count == wantTaken && palC?.Taken.Count == wantTaken
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

            // The host is never put out of their own lobby: sitting in someone else's seat with none free, the
            // seat's owner is turned away instead.
            port = nextPort++;
            host = new Session();
            two.Id = "seats2";
            host.HostResume(two, port, "Nobody");  // no seat has this name: the host sits in seat 0 (Anthony's)
            var friend = new Session();
            friend.Join("127.0.0.1", port, "Friend", Role.Dps);  // takes back seat 1, so none is free
            for (int i = 0; i < 100; i++) { Step(host, dt); Step(friend, dt); Thread.Sleep(1); }
            var owner = JoinAs(port, false, "Anthony", Role.Dps);
            for (int i = 0; i < 200; i++) { Step(host, dt); Step(friend, dt); Step(owner, dt); Thread.Sleep(1); }
            Console.WriteLine("  host in someone's seat: host still in the lobby {0} (seat {1}); 'Anthony' was {2} ({3})", host.Lobby.ContainsKey(1), host.Lobby.ContainsKey(1) ? host.Lobby[1].Seat : -1, owner.Screen, owner.Status);
            Check(host.Lobby.ContainsKey(1) && host.Lobby[1].Seat == 0 && host.Lobby.Count == 2 && owner.Screen == AppScreen.Menu && owner.Status.Contains("seats are taken"),
                "the host keeps their seat in a full saved run; its owner is turned away instead");
            host.Leave(""); friend.Leave(""); owner.Leave("");

            // Saving over a save swaps the new file in (no leftover temp file, the old one replaced).
            var over = RunSave.Parse(made.ToText());
            over.Id = "over";
            over.Wave = 6; over.BossSlot = false;
            bool w1 = SaveStore.Write(over);
            over.Wave = 9; over.BossSlot = false;
            bool w2 = SaveStore.Write(over);
            var listed = SaveStore.List().Where(x => x.Id == "over").ToList();
            Check(w1 && w2 && listed.Count == 1 && listed[0].Wave == 9 && Directory.GetFiles(folder, "*.tmp").Length == 0,
                "saving over a saved run replaces it, leaving no temp file");
            SaveStore.Delete("over");

            ExpandingSaveTest(dt);
        }
        finally
        {
            SaveStore.Folder = null;
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    /// The Expanding arena's map, kits, vectors and shop shelf go in the save and come back on Continue (ExpansionGame.cs).
    static void ExpandingSaveTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Friend", Role.Dps, 1, 0, Kit.Elementalist);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 40f, () => g.State == MatchState.Intermission && g.Wave == 6 && !g.AnyonePicking(), () => { NoPicks(); foreach (var e in g.Enemies.Values) e.Hp = 0f; });

        // A map: a drawn room (the friend's), a shop room off it, a kit held; vectors and a shelf for the friend.
        string why;
        var east = Expansion.Custom(g.Expanded, 1600, 300, 600, 300, out why).Value;
        east.Owner = 2;
        g.Expanded.Add(east);
        var shop = Expansion.Custom(g.Expanded, 2200, 300, 400, 300, out why);
        if (shop.HasValue) { var sh = shop.Value; sh.Kind = RoomKind.Shop; g.Expanded.Add(sh); }
        g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Floor, Floor = PathId.FrozenLake });
        var friend = g.Players[2];
        friend.Vectors = 31;
        int fortitude = Array.FindIndex(Upgrades.All, u => u.Name == "Fortitude");
        friend.Shelf[0] = fortitude; friend.Shelf[1] = -1; friend.ShelfSold = 1; friend.Restocks = 1;
        int rooms = g.Expanded.Count;
        host.Leave(""); client.Leave("");
        var sv = SaveStore.List().FirstOrDefault(x => x.HasExpansion);
        var back = sv == null ? null : RunSave.Parse(sv.ToText(), sv.Id);
        Console.WriteLine("  expanding save: {0} rooms (in game {1}), kits {2}, friend vectors {3}, shelf '{4}'", back?.Rooms.Count, rooms, back?.Kits.Count, back?.Seats[1].Vectors, back == null ? "" : string.Join(",", back.Seats[1].Shelf.Take(2)));
        Check(back != null && back.Rooms.Count == rooms && back.Rooms.Any(r => r.X == 1600 && r.Y == 300 && r.Owner == 2) && back.Kits.Count == 1 && back.Kits[0].Floor == PathId.FrozenLake && back.Seats[1].Vectors == 31
            && back.Seats[1].Shelf[0] == "Fortitude" && back.Seats[1].ShelfSold == 1 && back.Seats[1].Restocks == 1, "an Expanding run saves its map (drawer by seat), kits, vectors and shelf");

        // Continue it: the friend takes their seat under another name; the map, kit, vectors and shelf come back on both games.
        port = nextPort++;
        host = new Session { AutoStart = 2 };
        host.HostResume(back, port, "Host");
        client = new Session();
        client.Join("127.0.0.1", port, "Pal", Role.Healer, 0, 0, Kit.Summoner);
        s = new[] { host, client };
        WaitForGame(host, client, dt);
        Run(s, dt, 0.5f);
        g = host.Game; cg = client.Game;
        var pal = g.Players.Values.FirstOrDefault(p => p.Name == "Pal");
        var palC = cg.Players.Values.FirstOrDefault(p => p.Name == "Pal");
        Console.WriteLine("  expanding continued: arena expanding {0}/{1}, rooms {2} (client {3}), owner {4} (pal {5}), kits {6} (client {7}), pal vectors {8} (client {9}), shelf {10} sold {11} restocks {12} areas {13}",
            g.ExpandingOn, cg.ExpandingOn, g.Expanded.Count, cg.Expanded.Count, g.Expanded.FirstOrDefault(r => r.X == 1600 && r.Y == 300).Owner, pal?.Id, g.RoomTokens.Count, cg.RoomTokens.Count, pal?.Vectors, palC?.Vectors, pal?.Shelf[0], pal?.ShelfSold, pal?.Restocks, g.CurrentWorld.Areas.Length);
        Check(g.ExpandingOn && cg.ExpandingOn && g.Expanded.Count == rooms && cg.Expanded.Count == rooms && pal != null && g.Expanded.Any(r => r.X == 1600 && r.Y == 300 && r.Owner == pal.Id) && g.RoomTokens.Count == 1 && cg.RoomTokens.Count == 1,
            "continuing an Expanding run rebuilds its map and kits on every game, the drawn room going to whoever took its seat");
        Check(pal != null && pal.Vectors == 31 && palC?.Vectors == 31 && pal.Shelf[0] == fortitude && pal.ShelfSold == 1 && pal.Restocks == 1,
            "each seat gets back its vectors and shop shelf");
        Run(s, dt, 15f, () => g.State == MatchState.Fighting);
        Run(s, dt, 1f);
        Console.WriteLine("  expanding next wave: wave {0}, rooms up {1}/{4}, areas {2} (client {3}), client rooms {5}", g.Wave, g.RoomsOn, g.CurrentWorld.Areas.Length, cg.CurrentWorld.Areas.Length, cg.RoomsOn, cg.Expanded.Count);
        Check(g.RoomsOn && g.CurrentWorld.Areas.Length >= 2 && cg.CurrentWorld.Areas.Length == g.CurrentWorld.Areas.Length, "the next wave is fought on the saved map");
        host.Leave(""); client.Leave("");
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

        // The menu's "your profile" nudge: shown for the first 5 launches, gone for good once the profile is opened, and kept in the file.
        var hint = new Profile();
        for (int i = 1; i <= Profile.HintMaxLaunches; i++)
        {
            hint.NoteMenuLaunch();
            Check(hint.ShowProfileHint, "the profile hint shows on launch " + i);
        }
        hint.NoteMenuLaunch();
        Check(!hint.ShowProfileHint, "the profile hint stops after " + Profile.HintMaxLaunches + " launches");
        var hint2 = new Profile();
        hint2.NoteMenuLaunch();
        hint2.ProfileOpened = true;
        var hintBack = Profile.Parse(hint2.ToText());
        Check(hintBack.Valid && hintBack.HintLaunches == 1 && hintBack.ProfileOpened && !hintBack.ShowProfileHint, "opening the profile turns the hint off for good, and the file remembers it");
        Check(Profile.Parse("arena-profile 4\nruns 1 0 0\nend 1\n").HintLaunches == 0 && Profile.Parse(new Profile().ToText()).LoadedVersion == Profile.FormatVersion, "an older profile file loads with no hint count");

        var tagP = new Profile();
        tagP.TagSeen["Sniper"] = 2; tagP.TagSeen["Grunt"] = Profile.TagShows;
        var tagBack = Profile.Parse(tagP.ToText());
        Check(tagBack.Valid && tagBack.TagSeen.Count == 2 && tagBack.TagSeen["Sniper"] == 2 && tagBack.TagSeen["Grunt"] == Profile.TagShows, "the name-tag counts read back from the profile file");
        Check(Profile.Parse("arena-profile 5\nruns 1 0 0\nend 1\n").TagSeen.Count == 0, "an older profile file loads with no name tags shown yet");

        ProfileFileTest();
        ProfileStoreTest();

        // Each player's own game keeps their own profile: a boss beaten and the run's end count for both.
        var test = new UnlockDef { Id = "test-loss", Name = "Test", Desc = "Lose a run", Earned = p => p.RunsLost > 0 };
        Unlocks.All.Add(test);
        try
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = -5 };
            var hp = host.Game.ProfileOverride = new Profile();
            host.Host(port, "Host", Role.Tank);
            var client = new Session();
            var cp = client.Game.ProfileOverride = new Profile();
            client.Join("127.0.0.1", port, "Friend", Role.Dps);
            var s = new[] { host, client };
            WaitForGame(host, client, dt);
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
            host = new Session { StartWave = -5, AutoStart = 1 };
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

    /// profile.txt on disk: backup, cut-off and damaged files, version 1 files, save failures.
    static void ProfileFileTest()
    {
        string folder = Path.Combine(Path.GetTempPath(), "arena-profile-test-" + Guid.NewGuid().ToString("N"));
        Profile.Folder = folder;
        try
        {
            string main = Path.Combine(folder, "profile.txt"), bak = main + ".bak";
            var a = new Profile { RunsWon = 1, Xp = 100 };
            Check(a.Save() && File.Exists(main) && !File.Exists(bak), "first save writes profile.txt, no backup yet");
            a.RunsWon = 2; a.Xp = 250;
            a.Save();
            Check(File.Exists(bak) && Profile.Parse(File.ReadAllText(bak)).RunsWon == 1, "the second save keeps the one before as profile.txt.bak");
            Profile.Reload();
            Check(Profile.Current.RunsWon == 2 && Profile.LoadNote == "", "a good file loads with no note");

            // Cut off mid-write: no "end" line, so it falls back to the backup and the bad file is set aside.
            string text = File.ReadAllText(main);
            File.WriteAllText(main, text.Substring(0, text.Length / 2));
            Profile.Reload();
            Check(Profile.Current.RunsWon == 1 && Profile.LoadNote.Contains("bak"), "a cut-off profile restores from the backup");
            Check(Directory.GetFiles(folder, "profile.corrupt-*.txt").Length == 1, "the cut-off file is kept, not overwritten");

            // Garbage with no backup: a blank profile, the garbage kept.
            File.Delete(bak);
            File.WriteAllText(main, "not a profile at all");
            Profile.Reload();
            Check(Profile.Current.RunsWon == 0 && Profile.LoadNote.Contains("no backup"), "garbage and no backup: new profile, with a note");
            Check(Directory.GetFiles(folder, "profile.corrupt-*.txt").Length >= 1, "the garbage file is kept too");

            // A damaged line is skipped, the rest loads, and a copy of the file is kept.
            File.WriteAllText(main, text.Replace("best ", "best x "));
            Profile.Reload();
            Check(Profile.Current.RunsWon == 2 && Directory.GetFiles(folder, "profile.damaged-*.txt").Length == 1 && Profile.LoadNote.Contains("damaged line"),
                "a damaged line is skipped, the rest loads, a copy is kept");

            // A version 1 file (no "end" line) still loads and is brought up to date.
            string v1 = text.Replace("arena-profile " + Profile.FormatVersion, "arena-profile 1").Replace("end 1\n", "");
            var old = Profile.Parse(v1);
            Check(old.Valid && old.RunsWon == 2 && old.LoadedVersion == Profile.FormatVersion, "a version 1 profile loads and migrates");
            Check(!Profile.Parse("runs 1 1 0\n").Valid, "text without the header isn't a profile");

            // Failing to write: Save says so, and the next good save clears it.
            File.WriteAllText(Path.Combine(folder, "blocker"), "x");
            Profile.Folder = Path.Combine(folder, "blocker", "sub");
            Check(!a.Save() && Profile.LastSaveFailed, "a save that can't write reports the failure");
            Profile.Folder = folder;
            Check(a.Save() && !Profile.LastSaveFailed, "the next good save clears it");
        }
        finally
        {
            Profile.Folder = null;
            Profile.Reload();
            try { Directory.Delete(folder, true); } catch (IOException) { }
        }
    }

    /// Several profiles: migration, create/rename/delete, switching without mixing records or saved runs.
    static void ProfileStoreTest()
    {
        string data = Path.Combine(Path.GetTempPath(), "arena-profiles-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            // A pre-profiles install: profile.txt and saves/*.run in the data folder.
            Directory.CreateDirectory(Path.Combine(data, "saves"));
            var old = new Profile { RunsWon = 4, RunsLost = 2, Xp = 900 };
            File.WriteAllText(Path.Combine(data, "profile.txt"), old.ToText());
            File.WriteAllText(Path.Combine(data, "saves", "abc.run"), "x");
            Check(ProfileStore.Init(data, "Anthony"), "first launch makes a profile from the old files and opens it");
            var list = ProfileStore.List();
            Check(list.Count == 1 && list[0].Name == "Anthony" && list[0].Wins == 4 && list[0].Runs == 6, "the migrated profile keeps its records and takes the old player name");
            Check(Profile.Current.Xp == 900 && Profile.Current.Name == "Anthony", "the active profile is the migrated one");
            Check(File.Exists(Path.Combine(data, "profile.txt")) && File.Exists(Path.Combine(data, "saves", "abc.run")) && File.Exists(Path.Combine(data, "migrated.txt")), "the old files are left in place");
            Check(File.Exists(Path.Combine(SaveStore.Folder, "abc.run")), "the old saved runs went with it");

            // A second profile: its own records and saved runs.
            string sam = ProfileStore.Create("Sam");
            Check(sam != null && ProfileStore.List().Count == 2, "a new profile is made");
            Check(ProfileStore.Create("sam") == null && ProfileStore.NameProblem("SAM").Length > 0, "a name already taken (any case) is refused");
            Check(ProfileStore.Create("   ") == null && ProfileStore.NameProblem("").Length > 0, "an empty name is refused");
            string anthony = ProfileStore.ActiveId;
            Profile.Current.RunsWon = 5; Profile.Current.Save();
            ProfileStore.SetActive(sam);
            Check(Profile.Current.Name == "Sam" && Profile.Current.RunsWon == 0 && Profile.Current.Xp == 0, "switching loads the other profile's records, not the old ones");
            Check(Directory.GetFiles(SaveStore.Folder, "*.run").Length == 0, "the other profile has no saved runs");
            Profile.Current.RunsWon = 1; Profile.Current.Save();
            ProfileStore.SetActive(anthony);
            Check(Profile.Current.RunsWon == 5 && Directory.GetFiles(SaveStore.Folder, "*.run").Length == 1, "switching back finds the first profile untouched");

            // Rename, and the choice survives a restart.
            Check(ProfileStore.Rename(sam, "Samantha") && ProfileStore.Rename(sam, "samantha") && !ProfileStore.Rename(sam, "Anthony"), "rename works, except onto another profile's name");
            ProfileStore.SetActive(sam);
            ProfileStore.Init(data, "ignored");
            Check(ProfileStore.ActiveId == sam && Profile.Current.Name == "samantha" && Profile.Current.RunsWon == 1, "a restart opens the profile used last, with its name");

            // Easter-egg disk looks: earned by a name, kept when the profile is renamed, kept across a restart.
            Check(ProfileStore.DiskLookFor("San Andreas") == "sa" && ProfileStore.DiskLookFor("GTA:SA") == "sa" && ProfileStore.DiskLookFor("SA") == "sa" && ProfileStore.DiskLookFor("doom 2") == "doom"
                && ProfileStore.DiskLookFor("Mission Impossible") == "mi" && ProfileStore.DiskLookFor("Windows 95") == "win95" && ProfileStore.DiskLookFor("Win95") == "win95" && ProfileStore.DiskLookFor("Antaleon") == "holo" && ProfileStore.DiskLookFor("antaleon_2") == "holo" && ProfileStore.DiskLookFor("Pac-Man") == "pac" && ProfileStore.DiskLookFor("Ms Pacman") == "pac" && ProfileStore.DiskLookFor("Chomper") == "pac" && ProfileStore.DiskLookFor("waka waka") == "pac" && ProfileStore.DiskLookFor("Sam") == "", "disk looks: the names that earn one");
            ProfileStore.Root = Path.Combine(data, "profiles");
            Check(ProfileStore.Rename(sam, "Doomguy") && ProfileStore.List().Find(i => i.Id == sam).Disk == "doom", "disk looks: renaming to a Doom name gives the Doom disk");
            Check(ProfileStore.Rename(sam, "Plain Sam") && ProfileStore.List().Find(i => i.Id == sam).Disk == "doom", "disk looks: renaming to a plain name keeps it");
            ProfileStore.Init(data, "ignored");
            Check(ProfileStore.List().Find(i => i.Id == sam).Disk == "doom", "disk looks: kept after a restart");
            Check(ProfileStore.Rename(sam, "SA") && ProfileStore.List().Find(i => i.Id == sam).Disk == "sa", "disk looks: another earning name swaps it");

            // Hostile names make safe folder ids.
            ProfileStore.Root = Path.Combine(data, "profiles");
            bool safe = true;
            foreach (var bad in new[] { "..", "con", "a/b\\c", "COM1", "\u00e9\u00e8", new string('x', 200) })
            {
                string id = ProfileStore.NewId(bad);
                safe &= id.Length > 0 && id.Length <= 28 && id.IndexOfAny(new[] { '/', '\\', '.', ':' }) < 0 && id != "con" && id != "com1";
            }
            Check(safe, "hostile names make safe folder ids");
            Check(ProfileStore.Clean(new string('y', 40)).Length == ProfileStore.MaxNameLength, "names are cut to " + ProfileStore.MaxNameLength + " characters");

            // The profile limit, and deleting.
            while (ProfileStore.List().Count < ProfileStore.MaxProfiles) ProfileStore.Create("P" + ProfileStore.List().Count);
            Check(ProfileStore.MaxProfiles == 3, "three profile slots, so the picker always has four boxes");
            Check(ProfileStore.Create("One too many") == null, "no more than " + ProfileStore.MaxProfiles + " profiles");
            ProfileStore.SetActive(anthony);
            Check(ProfileStore.Delete(anthony) && ProfileStore.ActiveId != anthony && ProfileStore.ActiveId.Length > 0 && !Directory.Exists(Path.Combine(data, "profiles", anthony)), "deleting the active profile makes another active and removes its folder");
            foreach (var p in ProfileStore.List().Skip(1).ToList()) ProfileStore.Delete(p.Id);
            Check(ProfileStore.List().Count == 1 && !ProfileStore.Delete(ProfileStore.List()[0].Id), "the last profile can't be deleted");

            // A damaged profile in one folder doesn't touch another, and still shows in the list.
            string other = ProfileStore.Create("Other");
            File.WriteAllText(Path.Combine(data, "profiles", other, "profile.txt"), "garbage");
            File.Delete(Path.Combine(data, "profiles", other, "profile.txt.bak"));
            var ls = ProfileStore.List();
            Check(ls.Count == 2 && ls.Any(p => p.Id == other), "a damaged profile is still listed");
        }
        finally
        {
            ProfileStore.Root = null; ProfileStore.ActiveId = "";
            Profile.Folder = null; SaveStore.Folder = null; Profile.Reload();
            try { Directory.Delete(data, true); } catch (IOException) { }
        }
    }
}

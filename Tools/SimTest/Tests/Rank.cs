using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The account rank (Rank.cs, Docs/Rank.md): the curve, XP for a run, the profile, the lobby, and a run ending.
static partial class Program
{
    static void RankTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;

        // The curve and the tiers.
        var r1 = Rank.Of(0);
        Check(r1.Rank == 1 && r1.Into == 0 && r1.Need == 200, "rank 1 at 0 XP, 200 XP to the next");
        var r2 = Rank.Of(199);
        var r3 = Rank.Of(200);
        Check(r2.Rank == 1 && r2.Into == 199 && r3.Rank == 2 && r3.Into == 0 && r3.Need == 400, "200 XP is exactly rank 2");
        Check(Rank.Of(Rank.XpAt(8) + 115).Rank == 8 && Rank.Of(Rank.XpAt(8) + 115).Into == 115 && Rank.Of(Rank.XpAt(8)).Need == 1600, "rank 8 is 1,600 XP long (200 a rank), with 115 into it");
        Check(Rank.Of(-50).Rank == 1 && Rank.Of(long.MaxValue / 4).Rank == Rank.MaxRank, "negative XP is rank 1 and rank stops at the cap");
        Check(Rank.Title(4) == "Recruit" && Rank.Title(5) == "Challenger" && Rank.Title(10) == "Veteran" && Rank.Title(20) == "Champion" && Rank.Title(30) == "Legend" && Rank.Tier(99) == 4, "tiers start at 5, 10, 20 and 30");

        // Cosmetics: owned from their rank, one frame number each.
        Check(!Rank.Owns(7, "trail-ember") && Rank.Owns(8, "trail-ember") && !Rank.Owns(99, "nonsense") && Rank.Owns(30, "frame-gold"), "a cosmetic is yours from its rank, and unknown ids never are");
        Check(Rank.FrameIndex("") == 0 && Rank.FrameIndex("frame-steel") == 1 && Rank.FrameIndex("frame-gold") == 2 && Rank.FrameId(2) == "frame-gold" && Rank.FrameId(9) == "" && Rank.FrameId(0) == "", "frames are numbered 1, 2 for the lobby (0 none)");
        Check(Rank.RewardsAt(8).Count == 1 && Rank.RewardsAt(8)[0].Id == "trail-ember" && Rank.RewardsAt(9).Count == 0, "rank 8 brings the Ember trail, rank 9 nothing");

        // XP for a run.
        var a = Rank.Award(false, 10, 2, 1, RunMod.None, DifficultyLevel.Normal);
        Check(a.Subtotal == 540 && a.ModMult == 1f && a.Total == 540 && a.Lines.Count == 3, "wave 10 cleared, two bosses, one revive: 200 + 300 + 40 = 540");
        var b = Rank.Award(false, 10, 2, 1, RunMod.ToughCrowd | RunMod.Nightfall, DifficultyLevel.Normal);
        Check(Math.Abs(b.ModMult - 1.5f) < 1e-4f && b.Total == 810 && b.ModNotes.Count == 2 && b.Mods == "Tough Crowd|Nightfall", "two Harder modifiers: x1.5, 810 XP");
        var c = Rank.Award(true, 50, 10, 0, RunMod.GlassCannon | RunMod.Hunted, DifficultyLevel.Normal);
        Check(Math.Abs(c.ModMult - 1.3f) < 1e-4f && c.Lines.Any(l => l.Label == "Victory" && l.Xp == 500) && c.Total == (int)Math.Round((1000 + 1500 + 500) * 1.3f), "a win adds 500, and two Risky modifiers give +15% each");
        var easy = Rank.Award(false, 10, 2, 0, RunMod.Lucky | RunMod.HeadStart | RunMod.SecondWind, DifficultyLevel.Normal);
        Check(easy.ModMult == 1f && easy.ModNotes.Count == 0 && easy.Total == 500, "Easier modifiers add nothing and cost nothing");
        var every = Rank.Award(false, 10, 2, 0, (RunMod)0x1FFFF, DifficultyLevel.Normal);
        Check(every.ModMult == Rank.ModCap, "every modifier at once is capped at x3");
        var nm = Rank.Award(false, 10, 2, 0, RunMod.None, DifficultyLevel.Nightmare);
        var ez = Rank.Award(false, 10, 2, 0, RunMod.None, DifficultyLevel.Easy);
        var cu = Rank.Award(false, 10, 2, 0, RunMod.None, DifficultyLevel.Custom);
        Check(nm.Total == 750 && ez.Total == 375 && cu.Total == 500, "difficulty steps the XP: Nightmare x1.5, Easy x0.75, Custom x1");
        Check(Rank.Award(false, 0, 0, 0, RunMod.None, DifficultyLevel.Normal).Total == 0 && Rank.BossesIn(11) == 2 && Rank.BossesIn(4) == 0, "a run that cleared nothing earns nothing; a boss every fifth wave");

        // The profile: XP, rank-ups and what they unlock, equipping, and saving it.
        var p = new Profile();
        var g1 = p.AwardXp(Rank.Award(false, 10, 2, 1, RunMod.None, DifficultyLevel.Normal), 11, 0);
        Check(p.Xp == 540 && g1.Before == 0 && g1.After == 540 && g1.RankBefore == 1 && g1.RankAfter == 2 && g1.RankedUp && p.RankNow == 2 && g1.Unlocked.Count == 0, "540 XP takes a new profile to rank 2, with nothing to unlock yet");
        p.Xp = Rank.XpAt(7) + 900;
        var g2 = p.AwardXp(Rank.Award(false, 10, 2, 1, RunMod.ToughCrowd | RunMod.Nightfall, DifficultyLevel.Normal), 11, 0);
        Check(g2.RankBefore == 7 && g2.RankAfter == 8 && g2.Unlocked.Count == 1 && g2.Unlocked[0].Id == "trail-ember" && Rank.Of(p.Xp).Into == 310, "rank 7 + 810 XP is rank 8, which unlocks the Ember trail");
        Check(p.Recent.Count == 2 && p.Recent[0].Xp == 810 && p.Recent[0].MultPct == 150 && p.Recent[0].Mods == "Tough Crowd|Nightfall", "the run is logged, newest first");
        for (int i = 0; i < 9; i++) p.AwardXp(Rank.Award(false, 1, 0, 0, RunMod.None, DifficultyLevel.Normal), 2, 0);
        Check(p.Recent.Count == Profile.RecentKept, "only the last few runs are kept");
        p.Xp = Rank.XpAt(12);
        Check(!p.Equip(CosmeticKind.Frame, "frame-gold") && p.Frame == "" && !p.Equip(CosmeticKind.Trail, "frame-steel") && p.Equip(CosmeticKind.Frame, "frame-steel") && p.Frame == "frame-steel" && p.Equip(CosmeticKind.Trail, "trail-ember"), "you can equip what your rank has and the right kind, not more");
        Check(p.Equip(CosmeticKind.Frame, "") && p.Frame == "", "an empty id takes the slot off");
        p.Equip(CosmeticKind.Frame, "frame-steel");
        var back = Profile.Parse(p.ToText());
        Check(back.Xp == p.Xp && back.Frame == "frame-steel" && back.Trail == "trail-ember" && back.Background == "" && back.Recent.Count == p.Recent.Count && back.Recent[0].Wave == p.Recent[0].Wave, "XP, what's equipped and the recent runs read back the same");
        p.Recent.Insert(0, new RankLog { Wave = 23, Result = 1, Xp = 1120, MultPct = 175, Mods = "Death Throes|Frenzy|Tough Crowd" });
        Check(Profile.Parse(p.ToText()).Recent[0].Mods == "Death Throes|Frenzy|Tough Crowd", "modifier names with spaces survive saving");
        Check(Profile.Parse("arena-profile 1\nxp banana\nequip - -\nrlog 1 2\n").Xp == 0 && Profile.Parse("arena-profile 1\nruns 1 0 0\n").Xp == 0, "a damaged or old profile loads with no XP");

        // The lobby: a friend's rank and frame reach the host and everyone, and the host only believes frames the rank has.
        int port = nextPort++;
        var host = new Session();
        var hp = host.Game.ProfileOverride = new Profile { Xp = Rank.XpAt(30) };
        hp.Equip(CosmeticKind.Frame, "frame-gold");
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        var cp = client.Game.ProfileOverride = new Profile { Xp = Rank.XpAt(12) };
        cp.Equip(CosmeticKind.Frame, "frame-steel");
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        WaitForLobby(host, client);
        PollBoth(host, client);
        Func<Session, int, LobbyEntry> entry = (sess, id) => sess.Lobby.ContainsKey(id) ? sess.Lobby[id] : null;
        int cid = client.MyId;
        Check(entry(host, 1).Rank == 30 && entry(host, 1).Frame == 2 && entry(client, 1).Rank == 30 && entry(client, 1).Frame == 2, "the host's rank 30 and gold frame show in everyone's lobby");
        Check(entry(host, cid).Rank == 12 && entry(host, cid).Frame == 1 && entry(client, cid).Rank == 12 && entry(client, cid).Frame == 1, "the friend's rank 12 and steel frame reach the host and the lobby");
        cp.Xp = Rank.XpAt(13);
        cp.Frame = "frame-gold";  // not theirs yet: a tampered profile
        client.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).Rank == 13 && entry(host, cid).Frame == 0 && entry(client, cid).Frame == 0, "a new rank is shared, and a frame the rank doesn't have is dropped by the host");
        cp.Frame = "";
        cp.Equip(CosmeticKind.Frame, "frame-steel");
        client.ShareCosmetics();
        hp.Xp = Rank.XpAt(31);
        host.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(client, cid).Frame == 1 && entry(client, 1).Rank == 31, "equipping a frame, and the host's own rank-up, update the lobby");
        cp.Xp = Rank.XpAt(25);
        cp.Equip(CosmeticKind.Trail, "trail-gold");
        hp.Equip(CosmeticKind.Trail, "trail-ember");
        client.ShareCosmetics(); host.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).Trail == 2 && entry(client, cid).Trail == 2 && entry(client, 1).Trail == 1 && Rank.TrailId(2) == "trail-gold" && Rank.TrailIndex("trail-ember") == 1, "everyone's lobby knows each player's trail, so it can be drawn in the match");
        cp.Trail = "trail-gold"; cp.Xp = Rank.XpAt(24);  // not theirs at rank 24
        client.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).Trail == 0, "a trail the rank doesn't have is dropped by the host");
        host.Leave(""); client.Leave("");

        // A run ending pays out in every profile; a run with dev tools doesn't.
        port = nextPort++;
        host = new Session { AutoStart = 2, StartWave = 5 };
        hp = host.Game.ProfileOverride = new Profile();
        host.Host(port, "Host", Role.Tank);
        client = new Session();
        cp = client.Game.ProfileOverride = new Profile();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Check(g.LastGain == null && client.Game.LastGain == null && hp.Xp == 0, "no XP before the run ends");
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && Find(host, EnemyType.Boss) != null && !g.IsSpawning(Find(host, EnemyType.Boss)), () => { foreach (var pl in g.Players.Values) pl.InvulnT = 1f; });
        Find(host, EnemyType.Boss).Hp = 0f;
        Run(s, dt, 3f, () => cp.Beaten(EnemyType.Boss) > 0, () => { foreach (var pl in g.Players.Values) pl.InvulnT = 1f; });
        foreach (var pl in g.Players.Values) { pl.Downed = true; pl.ReviveToken = false; }
        typeof(Game).GetMethod("CheckGameOver", flags).Invoke(g, null);
        Run(s, dt, 1f, () => cp.RunsLost > 0);
        var cg = client.Game.LastGain;
        Console.WriteLine("  run ended: host {0} XP (rank {1}), client {2} XP (rank {3}); client breakdown: {4}", hp.Xp, hp.RankNow, cp.Xp, cp.RankNow,
            cg == null ? "none" : string.Join(", ", cg.Award.Lines.Select(l => l.Label + " " + l.Xp).ToArray()));
        Check(cg != null && g.LastGain != null && cp.Xp > 0 && cp.Xp == cg.After && cg.Award.Total == cp.Xp && hp.Xp == g.LastGain.After && cp.Recent.Count == 1 && cp.Recent[0].Xp == cp.Xp,
            "when the run ends every player's profile gets its XP, with the breakdown for the results screen");
        Check(cg != null && cg.Award.Lines.Any(l => l.Label == "Waves cleared") && cg.Award.Lines.Any(l => l.Label == "Bosses defeated"), "the breakdown lists the waves cleared and the boss beaten");
        host.Leave(""); client.Leave("");

        port = nextPort++;
        host = new Session { StartWave = 5, AutoStart = 1 };
        var dp = host.Game.ProfileOverride = new Profile();
        host.Host(port, "Host", Role.Tank);
        host.StartMatch();
        host.Game.DevSetGod(1, true);
        for (int i = 0; i < 20 * 60 && !(Find(host, EnemyType.Boss) != null && !host.Game.IsSpawning(Find(host, EnemyType.Boss))); i++) Step(host, dt);
        Find(host, EnemyType.Boss).Hp = 0f;
        for (int i = 0; i < 60; i++) Step(host, dt);
        foreach (var pl in host.Game.Players.Values) { pl.Downed = true; pl.ReviveToken = false; }
        typeof(Game).GetMethod("CheckGameOver", flags).Invoke(host.Game, null);
        for (int i = 0; i < 60; i++) Step(host, dt);
        Check(dp.Xp == 0 && host.Game.LastGain == null, "a run where dev tools were used earns no XP");
        host.Leave("");
    }
}

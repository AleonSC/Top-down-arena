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
        Check(Rank.FrameIndex("") == 0 && Rank.FrameIndex("frame-steel") == 1 && Rank.FrameIndex("frame-gold") == 5 && Rank.FrameId(5) == "frame-gold" && Rank.FrameId(9) == "" && Rank.FrameId(0) == "", "frames are numbered in rank order for the lobby (0 none), so Gold is 5");
        Check(Rank.RewardsAt(16)[0].Id == "frame-circuit" && Rank.RewardsAt(20).Exists(rw => rw.Id == "frame-ember") && Rank.RewardsAt(24)[0].Id == "frame-void" && Rank.RewardsAt(40)[0].Id == "frame-foil" && Rank.RewardsAt(50)[0].Id == "frame-prism" && Rank.OfKind(CosmeticKind.Frame).Count == 7 && Rank.FrameIndex("frame-prism") == 7 && Rank.FrameId(8) == "", "Circuit, Ember Edge, Void Pulse, Legend Foil and Prism unlock at 16, 20, 24, 40 and 50");
        bool distinct = true;
        for (int i = 1; i <= 7; i++) for (int j = i + 1; j <= 7; j++) distinct &= Rank.FrameId(i) != Rank.FrameId(j);
        Check(distinct && Rank.FrameColor(2).G > 0.8f && Rank.FrameColor(3).R > 0.9f, "every frame has its own number and a base colour");
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
        for (int i = 0; i < Profile.RecentKept + 3; i++) p.AwardXp(Rank.Award(false, 1, 0, 0, RunMod.None, DifficultyLevel.Normal), 2, 0);
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
        Check(entry(host, 1).Rank == 30 && entry(host, 1).Frame == 5 && entry(client, 1).Rank == 30 && entry(client, 1).Frame == 5, "the host's rank 30 and gold frame show in everyone's lobby");
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
        Check(entry(host, cid).Trail == 5 && entry(client, cid).Trail == 5 && entry(client, 1).Trail == 1 && Rank.TrailId(5) == "trail-gold" && Rank.TrailIndex("trail-ember") == 1, "everyone's lobby knows each player's trail, so it can be drawn in the match");
        Check(Rank.RewardsAt(14)[0].Id == "trail-frost" && Rank.RewardsAt(18)[0].Id == "trail-storm" && Rank.RewardsAt(22)[0].Id == "trail-void" && Rank.RewardsAt(35)[0].Id == "trail-prism"
              && Rank.OfKind(CosmeticKind.Trail).Count == 7 && Rank.TrailIndex("trail-prism") == 6 && Rank.TrailId(6) == "trail-prism" && Rank.TrailId(8) == "", "Frost, Storm, Void and Prism unlock at 14, 18, 22 and 35, and every trail has its own lobby number");
        Check(Rank.RewardsAt(45).Count == 1 && Rank.RewardsAt(45)[0].Id == "trail-lightcycle" && Rank.TrailIndex("trail-lightcycle") == 7 && Rank.TrailId(7) == "trail-lightcycle" && !Rank.Owns(44, "trail-lightcycle") && Rank.Owns(45, "trail-lightcycle") && Rank.TrailIndex("trail-gold") == 5, "the Light Cycle trail unlocks at rank 45 as the last trail, and no earlier trail's number moves");
        // Card backgrounds: ten of them, numbered in rank order, shared through the lobby like frames and trails.
        Check(Rank.OfKind(CosmeticKind.CardBackground).Count == 10 && Rank.CardBgIndex("card-dusk") == 1 && Rank.CardBgIndex("card-prism") == 10 && Rank.CardBgId(10) == "card-prism" && Rank.CardBgId(11) == "" && Rank.Owns(55, "card-prism") && !Rank.Owns(54, "card-prism") && Rank.RewardsAt(6)[0].Id == "card-dusk",
            "ten card backgrounds, Dusk Grid at rank 6 to Prism Shards at 55, numbered for the lobby");
        cp.Xp = Rank.XpAt(30); hp.Xp = Rank.XpAt(30);
        Check(cp.Equip(CosmeticKind.CardBackground, "card-rain") == false && cp.Equip(CosmeticKind.CardBackground, "card-circuit") && cp.CardBg == "card-circuit" && !cp.Equip(CosmeticKind.Frame, "card-dusk") && cp.Equipped(CosmeticKind.CardBackground) == "card-circuit", "you equip the card backgrounds your rank has, and only in their slot");
        hp.Equip(CosmeticKind.CardBackground, "card-dusk");
        client.ShareCosmetics(); host.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).CardBg == 6 && entry(client, cid).CardBg == 6 && entry(client, 1).CardBg == 1, "everyone's lobby knows each player's card background");
        cp.CardBg = "card-prism";  // not theirs at rank 30
        client.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).CardBg == 0, "a card background the rank doesn't have is dropped by the host");
        // Name effects: twelve, numbered in rank order, shared through the lobby; the host drops one the rank doesn't have.
        Check(Rank.OfKind(CosmeticKind.NameEffect).Count == 12 && Rank.NameFxIndex("name-outline") == 1 && Rank.NameFxIndex("name-glitchy") == 8 && Rank.NameFxId(12) == "name-rainbow" && Rank.NameFxId(13) == "" && Rank.Owns(36, "name-glitchy") && !Rank.Owns(35, "name-glitchy") && Rank.RewardsAt(36).Exists(rw => rw.Id == "name-glitchy"),
            "twelve name effects, Steel Outline at rank 4 to Rainbow at 56, with Glitchy at 36");
        cp.Xp = Rank.XpAt(40); hp.Xp = Rank.XpAt(40);
        Check(cp.Equip(CosmeticKind.NameEffect, "name-glitchy") && cp.NameFx == "name-glitchy" && !cp.Equip(CosmeticKind.NameEffect, "name-gold") && !cp.Equip(CosmeticKind.Frame, "name-fire") && cp.Equipped(CosmeticKind.NameEffect) == "name-glitchy", "you equip the name effects your rank has, and only in their slot");
        hp.Equip(CosmeticKind.NameEffect, "name-outline");
        client.ShareCosmetics(); host.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).NameFx == 8 && entry(client, cid).NameFx == 8 && entry(client, 1).NameFx == 1, "everyone's lobby knows each player's name effect");
        cp.NameFx = "name-rainbow";  // not theirs at rank 40
        client.ShareCosmetics();
        PollBoth(host, client);
        Check(entry(host, cid).NameFx == 0, "a name effect the rank doesn't have is dropped by the host");
        // Aim lines: nine unlocks beside the free Classic line; local only (nothing on the wire), saved with the profile.
        Check(Rank.OfKind(CosmeticKind.AimLine).Count == 9 && Rank.AimLineIndex("aim-dotted") == 1 && Rank.AimLineIndex("aim-prism") == 9 && Rank.AimLineId(9) == "aim-prism" && Rank.AimLineId(10) == "" && Rank.AimLineIndex("") == 0
              && Rank.Owns(46, "aim-prism") && !Rank.Owns(45, "aim-prism") && Rank.Owns(5, "aim-dotted") && !Rank.Owns(4, "aim-dotted"), "nine aim lines, Dotted Tracer at rank 5 to Prism at 46, numbered in rank order, Classic is 0");
        cp.Xp = Rank.XpAt(20);
        Check(cp.Equip(CosmeticKind.AimLine, "aim-ember") && cp.AimLine == "aim-ember" && cp.Equipped(CosmeticKind.AimLine) == "aim-ember" && !cp.Equip(CosmeticKind.AimLine, "aim-storm") && !cp.Equip(CosmeticKind.Trail, "aim-dotted") && cp.Equip(CosmeticKind.AimLine, "") && cp.AimLine == "",
            "you equip the aim lines your rank has, only in their slot, and \"\" goes back to Classic");
        // Cursors: ten unlocks beside the free Classic crosshair; local only, saved as the seventh field of the equip line.
        Check(Rank.OfKind(CosmeticKind.Cursor).Count == 10 && Rank.CursorIndex("cursor-fine") == 1 && Rank.CursorIndex("cursor-cycle") == 10 && Rank.CursorId(10) == "cursor-cycle" && Rank.CursorId(11) == "" && Rank.CursorIndex("") == 0
              && Rank.Owns(46, "cursor-cycle") && !Rank.Owns(45, "cursor-cycle") && Rank.Owns(3, "cursor-fine") && !Rank.Owns(2, "cursor-fine"), "ten cursors, Fine Cross at rank 3 to Light Cycle at 46, numbered in rank order, Classic is 0");
        cp.Xp = Rank.XpAt(20);
        Check(cp.Equip(CosmeticKind.Cursor, "cursor-ember") && cp.Cursor == "cursor-ember" && cp.Equipped(CosmeticKind.Cursor) == "cursor-ember" && !cp.Equip(CosmeticKind.Cursor, "cursor-storm") && !cp.Equip(CosmeticKind.AimLine, "cursor-fine") && cp.Equip(CosmeticKind.Cursor, "cursor-fine") && cp.Equip(CosmeticKind.Cursor, "") && cp.Cursor == "",
            "you equip the cursors your rank has, only in their slot, and \"\" goes back to Classic");
        cp.Xp = Rank.XpAt(30);
        // Emotes: thirteen, on the bar (nothing to equip); a player sends one, the host checks rank and pause, everyone sees it.
        Check(Rank.EmoteCount == 13 && Rank.EmoteIndex("emote-wave") == 1 && Rank.EmoteId(13) == "emote-steam" && Rank.Owns(1, "emote-wave") && !Rank.Owns(24, "emote-spin") && Rank.Owns(25, "emote-spin") && !cp.Equip(CosmeticKind.Emote, "emote-wave") && cp.Equipped(CosmeticKind.Emote) == "",
            "thirteen emotes from rank 1 to 52, numbered in order, and nothing to equip");
        cp.Xp = Rank.XpAt(30);
        client.ShareCosmetics();
        PollBoth(host, client);
        int shown; float age;
        Check(!client.SendEmote(11) && client.EmoteWait == 0f, "an emote your rank hasn't reached isn't sent");
        Check(client.SendEmote(5), "you can play an emote you've unlocked");
        PollBoth(host, client);
        Check(host.EmoteOf(cid, out shown, out age) && shown == 5 && client.EmoteOf(cid, out shown, out age) && shown == 5 && age < 1f, "the host and the player's own game both show it");
        Check(!client.SendEmote(6) && client.EmoteWait > 0f && client.EmoteWait <= Session.EmoteHangoutCooldown && Session.EmoteCooldown == 2f, "there's a pause before the next one (shorter in the lobby, 2 s in a match)");
        Check(host.SendEmote(1), "the host plays one too");
        PollBoth(host, client);
        Check(client.EmoteOf(1, out shown, out age) && shown == 1, "and the client sees the host's");
        // Chat: a client's line is cleaned and trimmed by the host and shown to everyone; one a second per player.
        Check(Session.CleanChat("  hi\u0007  there  ") == "hi there" && Session.CleanChat(new string('x', 300)).Length == Session.ChatMaxLength && Session.CleanChat("\n\t") == "", "chat lines lose control characters and extra spaces, and are cut to 120");
        Check(client.SendChat("on my way") && !client.SendChat("   "), "a client sends a line, and an empty one isn't sent");
        PollBoth(host, client);
        Check(host.Chat.Count == 1 && client.Chat.Count == 1 && client.Chat[0].Text == "on my way" && client.Chat[0].Id == cid, "the host and the sender both get the line");
        client.SendChat("too fast");
        PollBoth(host, client);
        Check(host.Chat.Count == 1, "a second line inside the gap is dropped by the host");
        Check(host.SendChat("hello") && host.Chat.Count == 2, "the host chats too");
        PollBoth(host, client);
        Check(client.Chat.Count == 2 && client.Chat[1].Id == 1 && client.Chat[1].Text == "hello", "and the client sees the host's");
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

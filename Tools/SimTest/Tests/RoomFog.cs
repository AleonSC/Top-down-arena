using System;
using System.Linq;
using Arena;

// Room fog (Docs/FogOfWar.md, Core/RoomFog.cs): a room the map gets after the core starts unseen for everyone; a standing
// player walking in reveals the whole piece for the whole team; it's on the wire and in saves; bots scout unseen rooms.
static partial class Program
{
    static void RoomFogTest()
    {
        var core = Expansion.GridCore();
        Check(core.All(r => !RoomFog.StartsUnseen(r)), "the core never starts unseen");
        var room = new ExpansionRoom { X = 2000, Y = 0, W = 600, H = 600, Kind = Arena.RoomKind.Combat, Unseen = true };
        Check(RoomFog.StartsUnseen(room), "a built room starts unseen");
        var list = core.ToList(); list.Add(room);
        Check(RoomFog.UnseenCount(list) == 1, "the unseen count counts it");

        // Saves keep it; an old save's room (no field) is seen.
        var save = new RunSave { Wave = 6, HasExpansion = true };
        save.Seats.Add(new SeatSave());
        save.Rooms.AddRange(list);
        var back = RunSave.Parse(save.ToText());
        Check(back != null && back.Rooms.Count == list.Count && back.Rooms.Last().Unseen && back.Rooms.Take(4).All(r => !r.Unseen), "a save keeps which rooms are unseen");
        var line = save.ToText().Split('\n').Last(l => l.StartsWith("room "));
        var old = RunSave.Parse(save.ToText().Replace(line, line.Substring(0, line.LastIndexOf(' '))));
        Check(old != null && !old.Rooms.Last().Unseen, "an old save's rooms are seen");
    }

    static void RoomFogGameTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.Expanded.Count == 4 && g.Expanded.All(r => !r.Unseen) && cg.Expanded.All(r => !r.Unseen), "the core starts seen");

        // Two pieces: both go up unseen on both screens.
        var friend = g.Players[2];
        friend.Hand[0] = (byte)Expansion.MakePieceCard(PieceShape.Domino);
        friend.Hand[1] = (byte)Expansion.MakePieceCard(PieceShape.Mono);
        var a = Expansion.PlacementsFor(g.Expanded, PieceShape.Domino).First();
        cg.PlayPieceCard(0, a.Orient, a.X, a.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == 4 + a.Rooms.Count, noPicks);
        int aHead = 4, bHead = g.Expanded.Count;
        var b = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First(o => !o.Rooms.Any(r => r.X < g.Expanded[aHead].X1 + 100 && r.X1 > g.Expanded[aHead].X - 100 && r.Y < g.Expanded[aHead].Y1 + 100 && r.Y1 > g.Expanded[aHead].Y - 100));
        int slot = Array.FindIndex(friend.Hand, h => h == (byte)Expansion.MakePieceCard(PieceShape.Mono));
        cg.PlayPieceCard(slot, b.Orient, b.X, b.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == bHead + b.Rooms.Count, noPicks);
        Check(g.Expanded.Count == bHead + b.Rooms.Count && g.Expanded.Skip(4).All(r => r.Unseen) && cg.Expanded.Skip(4).All(r => r.Unseen), "new pieces go up unseen, every box, on both screens");
        Check(RoomFog.UnseenCount(g.Expanded) == 2, "two unseen rooms");
        Run(s, dt, 0.3f, null, noPicks);

        // Fog hides what's inside unless a player is close; bots head for the nearest one.
        var head = g.Expanded[aHead];
        var mid = new V2((head.X + head.X1) / 2f, (head.Y + head.Y1) / 2f);
        bool far = g.Players.Values.All(p => p.Pos.FartherThan(mid, RoomFog.PeekReach));
        Check(cg.UnseenAt(mid) && (!far || cg.FogHides(mid)), "inside an unseen room is hidden from afar");
        Check(cg.NearestUnseen(g.Players[1].Pos).HasValue, "a bot has an unseen room to scout");

        // The host's player walks into the first piece: all of it is seen, on both screens; the second stays unseen.
        Run(s, dt, 1.5f, () => cg.Expanded.Count > aHead && !cg.Expanded[aHead].Unseen, () => { noPicks(); g.Players[1].Pos = mid; });
        var group = Expansion.GroupOf(g.Expanded, aHead);
        Check(group.All(i => !g.Expanded[i].Unseen) && group.All(i => !cg.Expanded[i].Unseen), "walking in reveals the whole piece for everyone (" + group.Count + " boxes)");
        Check(g.Expanded[bHead].Unseen && cg.Expanded[bHead].Unseen, "the other room stays unseen");
        Check(!cg.UnseenAt(mid) && !cg.FogHides(mid), "inside a seen room shows");
        Check(g.Expanded.Count == bHead + b.Rooms.Count && cg.Expanded.Count == g.Expanded.Count, "a reveal doesn't rebuild the map");
        host.Leave(""); client.Leave("");
    }

    static void RoomFogBotTest(float dt)
    {
        // A bot with nothing to fight walks into the unseen room and reveals it.
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1, StartWave = 6 }, port, true, "Bot", Role.Dps);
        var s = new[] { host };
        var g = host.Game;
        host.StartMatch();
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        g.DevSetGod(1, true);
        g.BotLevel = BotSkill.Good;
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Expanded.Clear(); g.Nodes.Clear(); g.ExpandOpen = true;
        Run(s, dt, 0.6f, () => g.Expanded.Count == 4, noPicks);
        var me = g.Players.Values.First();
        me.Hand[0] = (byte)Expansion.MakePieceCard(PieceShape.Mono);
        var opt = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First();
        g.PlayPieceCard(0, opt.Orient, opt.X, opt.Y);
        Run(s, dt, 1f, () => g.Expanded.Count == 5, noPicks);
        Check(g.Expanded.Count == 5 && g.Expanded[4].Unseen, "the bot's room goes up unseen");
        Action calm = () => { noPicks(); g.Enemies.Clear(); };
        Run(s, dt, 25f, () => !g.Expanded[4].Unseen, calm);
        Check(!g.Expanded[4].Unseen, "with nothing to fight the bot scouts it");
        host.Leave("");
    }
}

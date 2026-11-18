using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Grid pieces (Docs/GridPieces.md step 1): polyominoes of 1 to 4 cells placed on the lattice at door spots, each an open room.
static partial class Program
{
    static void GridPiecesTest()
    {
        var expect = new Dictionary<GridPiece, int> { { GridPiece.Mono, 1 }, { GridPiece.Domino, 2 }, { GridPiece.TriI, 2 }, { GridPiece.TriL, 4 }, { GridPiece.TetI, 2 }, { GridPiece.TetO, 1 }, { GridPiece.TetT, 4 }, { GridPiece.TetS, 4 }, { GridPiece.TetL, 8 } };
        foreach (var kv in expect)
            Check(Expansion.GridPieceOrients(kv.Key).Count == kv.Value, Expansion.GridPieceName(kv.Key) + " has " + kv.Value + " distinct turns and mirrors (" + Expansion.GridPieceOrients(kv.Key).Count + ")");
        Check(expect.Keys.Sum(k => Expansion.GridPieceOrients(k).Count) == 28 && expect.Keys.All(k => Expansion.GridPieceOrients(k).All(o => o.Length == Expansion.GridPieceCells(k))), "28 orientations in all, each with the piece's cells");

        var core = Expansion.GridCore();
        string why;
        foreach (var kv in expect)
        {
            var opts = Expansion.GridPieceOptions(core, kv.Key);
            bool aligned = opts.All(o => o.Rooms.All(r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0));
            bool free = opts.All(o => o.Rooms.All(r => r.Price == 0));
            bool openRoom = opts.All(o => { var m = new List<ExpansionRoom>(core); m.AddRange(o.Rooms); return Expansion.GroupOf(m, core.Count).Count == o.Rooms.Count && Expansion.BoughtCount(m) == 0 + 1; });
            Check(opts.Count > 0 && aligned && free && openRoom, Expansion.GridPieceName(kv.Key) + ": " + opts.Count + " places round the core, every box on the lattice, free, one open room");
        }
        var mono = Expansion.GridPieceOptions(core, GridPiece.Mono).Select(o => o.X + "," + o.Y).OrderBy(k => k).ToList();
        var cell = Expansion.GridOptions(core, GridShape.Room).Select(o => o.Room.X + "," + o.Room.Y).OrderBy(k => k).ToList();
        Check(mono.SequenceEqual(cell), "a single cell goes exactly where a Room goes (" + mono.Count + " spots)");

        Check(Expansion.GridPieceRooms(core, GridPiece.Domino, 0, 2100, 0, out why) == null && why == "Off the grid", "a piece off the lattice is refused (" + why + ")");
        Check(Expansion.GridPieceRooms(core, GridPiece.Mono, 0, 5000 + 600 - 200 - 0, -400, out why) == null, "far from the map is refused (" + why + ")");
        Check(Expansion.GridPieceRooms(core, GridPiece.Mono, 0, 200, -400, out why) == null && why.StartsWith("Overlaps"), "on top of the core is refused (" + why + ")");
        Check(Expansion.GridPieceRooms(core, GridPiece.Mono, 99, 2000, -400, out why) == null && why == "No such turn", "no such turn is refused");

        // Gaps: two singles on the core's right wall leave one cell between them; a piece fits in it if it fits, and nothing overlaps what is built.
        var map = new List<ExpansionRoom>(core);
        foreach (int y in new[] { -400, 800 })
        {
            var r = Expansion.GridPieceRooms(map, GridPiece.Mono, 0, 2000, y, out why);
            Check(r != null, "a single at (2000, " + y + ") (" + why + ")");
            if (r != null) map.AddRange(r);
        }
        Func<GridPiece, bool> coversGap = p => Expansion.GridPieceOptions(map, p).Any(o => o.Rooms.Any(r => 2300 > r.X && 2300 < r.X1 && 500 > r.Y && 500 < r.Y1));
        Check(coversGap(GridPiece.Mono) && coversGap(GridPiece.Domino) && coversGap(GridPiece.TriI) && coversGap(GridPiece.TetI) && coversGap(GridPiece.TetL), "the gap takes a single, a domino, a line of 3, a line of 4 and an L");
        bool clean = true;
        foreach (GridPiece p in Enum.GetValues(typeof(GridPiece)))
            foreach (var o in Expansion.GridPieceOptions(map, p))
                foreach (var r in o.Rooms)
                    foreach (var b in map)
                        clean &= !(r.X < b.X1 && b.X < r.X1 && r.Y < b.Y1 && b.Y < r.Y1);
        Check(clean, "no place for any piece overlaps a built room");
        Check(!Expansion.GridPieceOptions(map, GridPiece.TetI).Any(o => o.Rooms.Any(r => r.X <= 2300 && 2300 < r.X1 && r.Y < 500 && 500 < r.Y1 && r.W == 600 && r.H > 600)), "and a line of 4 standing up can't squeeze into the one-cell gap");
    }

    static void GridPiecesGameTest(float dt)
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
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.RoomCards, 0f); gg.Diff.Set(DiffKnob.BuildVote, 0f); gg.Diff.Set(DiffKnob.GridRooms, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4, "the core is up first");
        var opt = Expansion.GridPieceOptions(g.Expanded, GridPiece.TriL).First();
        int vec = g.Players[2].Vectors;
        cg.PlayGridPiece(GridPiece.TriL, opt.Orient, opt.X, opt.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == 4 + opt.Rooms.Count, noPicks);
        Check(g.Expanded.Count == 4 + opt.Rooms.Count && cg.Expanded.Count == g.Expanded.Count && g.Expanded.Skip(4).All(r => r.Owner == 2), "a friend's corner piece is built at once on both screens, all its boxes theirs (" + opt.Rooms.Count + " boxes)");
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Players[2].Vectors == vec && Expansion.BoughtCount(g.Expanded) == 1, "playing it costs nothing, and the open room counts as one room");
        int n = g.Expanded.Count;
        cg.PlayGridPiece(GridPiece.Domino, 0, opt.X + 100, opt.Y);
        cg.PlayGridPiece(GridPiece.Mono, 0, opt.X, opt.Y);
        Run(s, dt, 0.6f, null, noPicks);
        Check(g.Expanded.Count == n && cg.Expanded.Count == n, "a piece off the lattice and one on top of a room are refused");
        g.Diff.Set(DiffKnob.GridRooms, 0f);
        var again = Expansion.GridPieceOptions(g.Expanded, GridPiece.Mono).First();
        g.PlayGridPiece(GridPiece.Mono, again.Orient, again.X, again.Y);
        Run(s, dt, 0.3f, null, noPicks);
        Check(g.Expanded.Count == n, "with Grid off, no piece");
    }
}

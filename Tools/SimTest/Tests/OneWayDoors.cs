using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// One-way pieces (Docs/PieceMods.md, Core/OneWayDoors.cs): all a One-way piece's doors but one let you through one way only (in or out); the door it was
// built through stays both ways, a piece with a single door is both ways; players and enemies are held, enemy routes only cross the open way.
static partial class Program
{
    static void OneWayDoorsTest(float dt)
    {
        // The map: the Grid core and a One-way piece beside it with more than one door.
        var core = Expansion.GridCore();
        List<ExpansionRoom> rooms = null;
        World w = null;
        int first = core.Count + 1;
        foreach (var shape in new[] { PieceShape.Domino, PieceShape.TetL, PieceShape.TetI })
        {
            foreach (var o in Expansion.PlacementsFor(core, shape))
            {
                var l = new List<ExpansionRoom>(core);
                l.AddRange(o.Rooms.Select(q => PieceMods.ApplyRoom(q, PieceMods.OneWay.Set(0, 1))));
                var tw = Expansion.Build(l, Expansion.GridCoreCuts());
                int outs = tw.Doors.Count(d => (d.A >= first) != (d.B >= first));
                if (outs >= 3) { rooms = l; w = tw; break; }
            }
            if (w != null) break;
        }
        Check(w != null, "a piece beside the core has three or more doors");
        if (w == null) return;
        Func<World, int, List<RoomDoor>> outsOf = (wd, from) => wd.Doors.Where(d => (d.A >= from) != (d.B >= from)).ToList();
        var outs0 = outsOf(w, first);
        Check(outs0.Count(d => d.Pass == 0) == 1 && outs0[0].Pass == 0 && outs0.Skip(1).All(d => d.Pass != 0),
            "a One-way piece keeps its first door both ways, every other door one way (" + outs0.Count + " doors)");
        var plain = new List<ExpansionRoom>(core);
        plain.AddRange(rooms.Skip(core.Count).Select(q => { q.OneWay = false; return q; }));
        Check(Expansion.Build(plain, Expansion.GridCoreCuts()).Doors.All(d => d.Pass == 0), "the same piece without the modifier has no one-way doors");
        Check(outs0.Skip(1).All(d => (d.Pass > 0 ? d.B >= first : d.A >= first) == Expansion.OneWayInward(d.Pos)), "each one-way door lets you in or out by its seeded coin");
        int ins = 0, outsN = 0;
        for (int x = 0; x < 400; x++) { if (Expansion.OneWayInward(new V2(x * 600 + 300, 1200))) ins++; else outsN++; }
        Check(ins > 120 && outsN > 120, "the coin lands both ways (" + ins + " in, " + outsN + " out)");
        var mono = Expansion.PlacementsFor(core, PieceShape.Mono).Select(o =>
        {
            var l = new List<ExpansionRoom>(core);
            l.AddRange(o.Rooms.Select(q => PieceMods.ApplyRoom(q, PieceMods.OneWay.Set(0, 1))));
            return Expansion.Build(l, Expansion.GridCoreCuts());
        }).FirstOrDefault(tw => outsOf(tw, first).Count == 1);
        Check(mono != null && mono.Doors.All(d => d.Pass == 0), "a One-way piece with a single door: it works both ways");

        // Two One-way pieces side by side: each keeps its both-ways door.
        var second = Expansion.PlacementsFor(rooms, PieceShape.Mono).Select(o =>
        {
            var l = new List<ExpansionRoom>(rooms);
            l.AddRange(o.Rooms.Select(q => PieceMods.ApplyRoom(q, PieceMods.OneWay.Set(0, 1))));
            return l;
        }).FirstOrDefault(l => { var tw = Expansion.Build(l, Expansion.GridCoreCuts()); int a = l.Count; return tw.Doors.Count(d => d.Joins(a)) == 1 && tw.Doors.Any(d => d.Joins(a) && d.Other(a) >= first); });
        if (second != null)
        {
            var tw = Expansion.Build(second, Expansion.GridCoreCuts());
            Check(tw.Doors.Where(d => d.Joins(second.Count)).All(d => d.Pass == 0), "a One-way piece built only onto another one: its single door stays both ways");
        }

        // The geometry: held on the side it can't leave by, through the open way, routes only the open way.
        int k = Array.FindIndex(w.Doors, d => d.Pass != 0);
        var door = w.Doors[k];
        int into = door.Pass > 0 ? door.B : door.A;
        V2 inSide = (w.Areas[into].Mid - door.Pos).Dot(door.Across) >= 0f ? door.Across : -door.Across;
        V2 farIn = door.Pos + inSide * 120f, farOut = door.Pos - inSide * 120f;
        Check(!w.OneWayBlocks(farOut, farIn) && w.OneWayBlocks(farIn, farOut), "a line through the door the open way is clear, the other way blocked");
        V2 dashed = w.OneWayStop(door.Pos + inSide * 30f, door.Pos - inSide * 60f, 20f);
        Check((dashed - door.Pos).Dot(inSide) >= 19.9f, "a dash the wrong way stops on its own side (" + (dashed - door.Pos).Dot(inSide).ToString("0") + " px in)");
        V2 walked = w.OneWayStop(door.Pos - inSide * 10f, door.Pos + inSide * 5f, 20f);
        Check((walked - door.Pos).Dot(inSide) >= 5f, "the open way walks through (nudged clear of the line)");
        V2 leaning = w.OneWayHold(door.Pos + inSide * 8f, 20f);
        Check(Math.Abs((leaning - door.Pos).Dot(inSide) - 20f) < 0.1f, "leaning on it from the wrong side holds a body's width off the line");
        V2 beside = door.Pos + door.Along * (door.W / 2f + 60f) + inSide * 5f;
        Check(w.OneWayHold(beside, 20f).Equals(beside), "only the doorway holds (the wall beside it is a wall)");

        // In a match: the card, the client's map, and enemies' routes.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true; }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        var card = Expansion.MakePieceCardMods(PieceShape.TetL, PieceMods.OneWay.Set(0, 1));
        Check(Expansion.CardName(card).StartsWith("One-way") && PieceMods.Blurb(PieceMods.OneWay.Set(0, 1)).Contains("one-way") && Expansion.CardPrice(card) == 4 * 6 + 8, "a One-way card names it, says it, and costs 8 more (" + Expansion.CardName(card) + ")");
        var me = g.Players[1];
        var play = typeof(Game).GetMethod("HostPlayPieceCard", flags);
        var opts = Expansion.PlacementsForCard(g.Expanded, g.Nodes, card);
        int built = 0;
        foreach (var o in opts)  // (one with at least three doors)
        {
            var l = new List<ExpansionRoom>(g.Expanded); l.AddRange(o.Rooms.Select(q => { q.OneWay = true; return q; }));
            if (Expansion.Build(l, g.Openings).Doors.Count(d => d.Pass != 0) == 0) continue;
            Array.Clear(me.Hand, 0, me.Hand.Length);
            me.Hand[0] = (int)card;
            int before = g.Expanded.Count;
            play.Invoke(g, new object[] { me.Id, 0, o.Orient, o.X, o.Y });
            built = g.Expanded.Count - before;
            break;
        }
        Check(built > 0 && g.Expanded.Skip(g.Expanded.Count - built).All(q => q.OneWay), "a played One-way card builds its rooms one-way");
        g.ExpandOpen = false;
        Run(s, dt, 1f, () => cg.Expanded.Count == g.Expanded.Count && cg.Expanded.Count(q => q.OneWay) == built, noPicks);
        var hw = g.ExpansionWorld; var cw = cg.ExpansionWorld;
        Check(hw.OneWays.Length > 0 && hw.Doors.Select(d => d.Pass).SequenceEqual(cw.Doors.Select(d => d.Pass)), "the client builds the same one-way doors (" + hw.OneWays.Length + ")");
        Check(g.RoomsOn && g.LayoutOn, "the match is in the Expanding arena with walls up");

        var gd = hw.Doors[hw.OneWays[0]];
        int gInto = gd.Pass > 0 ? gd.B : gd.A;
        V2 gIn = (hw.Areas[gInto].Mid - gd.Pos).Dot(gd.Across) >= 0f ? gd.Across : -gd.Across;
        V2 pushed = g.PushOutOfSolids(gd.Pos + gIn * 4f, 18f);
        Check((pushed - gd.Pos).Dot(gIn) >= 17.9f, "the game's push-out holds bodies on the side they can't leave by");
        var step = typeof(Game).GetMethod("OneWayStep", flags);
        V2 stepped = (V2)step.Invoke(g, new object[] { gd.Pos + gIn * 25f, gd.Pos - gIn * 40f, 18f });
        Check((stepped - gd.Pos).Dot(gIn) > 0f, "a player's or enemy's step can't cross the wrong way");

        var steer = typeof(Game).GetMethod("SteerDir", flags);
        V2 a0 = gd.Pos + gIn * 90f, b0 = gd.Pos - gIn * 90f;
        V2 straight = (b0 - a0).Normalized();
        V2 wrong = (V2)steer.Invoke(g, new object[] { 991, a0, 0f, 16f, b0, straight });
        V2 right = (V2)steer.Invoke(g, new object[] { 992, b0, 0f, 16f, a0, -straight });
        Check(right.Equals(-straight), "an enemy walks straight through the open way");
        Check(wrong.Dot(straight) < 0.9f, "the wrong way, an enemy's route goes round by another door (dir " + wrong.X.ToString("0.00") + "," + wrong.Y.ToString("0.00") + ")");
    }
}

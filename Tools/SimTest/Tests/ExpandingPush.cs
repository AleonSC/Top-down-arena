using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Wall push (Docs/WallPush.md step 1): a section is one box joined open onto the wall it was pushed from; pushing its far wall grows it.
static partial class Program
{
    static void ExpandingPushRulesTest()
    {
        string why;
        int idx, cost;
        var list = new List<ExpansionRoom>();
        Func<int, int, int, int, WallPush> wp = (ar, sd, st, wd) => new WallPush { Area = (short)ar, Side = (byte)sd, Start = (short)st, Width = (byte)wd };

        // A first push off the hub's east wall: a 1-tile box, joined to the hub, costing its width.
        var a = Expansion.Push(list, wp(0, 1, 300, 4), out idx, out cost, out why);
        Check(a.HasValue && idx == -1 && cost == 6, "a push off the hub's east wall is a new section costing its new wall: width + 2 (" + why + ")");
        var b = a.Value;
        Check(b.X == 1600 && b.Y == 300 && b.W == 100 && b.H == 400 && b.Parent == 0 && Expansion.IsPush(b) && b.Kind == RoomKind.Combat, "it is a 1-tile box on the wall, its parent the hub");
        list.Add(b);
        Check(Expansion.Grown(list) && Expansion.BoughtCount(list) == 0 && Expansion.PushCount(list) == 1, "the map has grown, but no room was bought (sections don't count towards the room cap)");
        Check(Expansion.PushAway(list, 0) == 1 && Expansion.PushDepth(list[0], 1) == 1, "it grows east, 1 tile out");

        // Pushing its far wall over its full width grows the same box.
        var g2 = Expansion.Push(list, wp(1, 1, 300, 4), out idx, out cost, out why);
        Check(g2.HasValue && idx == 0 && cost == 2 && g2.Value.X == 1600 && g2.Value.W == 200 && g2.Value.H == 400 && g2.Value.Price == 8, "pushing the section's far wall grows it a tile, for 2 vectors (" + why + ")");
        list[0] = g2.Value;

        // The other three sides of the hub, each 1 tile out.
        var w = Expansion.Push(list, wp(0, 0, 200, 3), out idx, out cost, out why).Value;
        var n = Expansion.Push(list, wp(0, 2, 700, 2), out idx, out cost, out why).Value;
        var s = Expansion.Push(list, wp(0, 3, 600, 10), out idx, out cost, out why).Value;
        Check(w.X == -100 && w.Y == 200 && w.W == 100 && w.H == 300, "west: a box to the left of the hub");
        Check(n.X == 700 && n.Y == -100 && n.W == 200 && n.H == 100, "north: a box above the hub");
        Check(s.X == 600 && s.Y == 900 && s.W == 1000 && s.H == 100 && cost == 12, "south: a 10-wide box below it, costing 12");

        // Growing west and north moves the box's corner out.
        list.Add(w);
        var w2 = Expansion.Push(list, wp(2, 0, 200, 3), out idx, out cost, out why).Value;
        Check(idx == 1 && w2.X == -200 && w2.W == 200, "a west section grows west");

        // A narrower stretch of the section's far wall, or its side wall, starts a new section from it.
        var sub = Expansion.Push(list, wp(1, 1, 400, 2), out idx, out cost, out why);
        Check(sub.HasValue && idx == -1 && sub.Value.Parent == 1 && sub.Value.X == 1800, "part of its far wall: a new section off it (" + why + ")");
        var side = Expansion.Push(list, wp(1, 2, 1600, 2), out idx, out cost, out why);
        Check(side.HasValue && idx == -1 && side.Value.Parent == 1 && side.Value.Y == 200 && side.Value.X == 1600, "its side wall: a new section off it (" + why + ")");

        // Refusals.
        Check(!Expansion.Push(list, wp(0, 1, 300, 1), out idx, out cost, out why).HasValue, "1 tile wide is too narrow (" + why + ")");
        Check(!Expansion.Push(list, wp(0, 1, 0, 11), out idx, out cost, out why).HasValue, "11 tiles is too wide");
        Check(!Expansion.Push(list, wp(0, 1, 350, 2), out idx, out cost, out why).HasValue, "off the grid (" + why + ")");
        Check(!Expansion.Push(list, wp(0, 1, 800, 2), out idx, out cost, out why).HasValue, "off the end of the wall (" + why + ")");
        Check(!Expansion.Push(list, wp(0, 1, 200, 4), out idx, out cost, out why).HasValue && why == "Overlaps a room", "over a section already there: overlaps (" + why + ")");
        Check(!Expansion.Push(list, wp(9, 1, 200, 4), out idx, out cost, out why).HasValue, "no such area");

        // The 12-tile limit.
        var deep = new List<ExpansionRoom> { list[0] };
        for (int i = 0; i < 20; i++)
        {
            var d = Expansion.Push(deep, wp(1, 1, 300, 4), out idx, out cost, out why);
            if (!d.HasValue) break;
            deep[0] = d.Value;
        }
        Check(Expansion.PushDepth(deep[0], 1) == Expansion.PushMaxDepth && why == Expansion.PushMaxDepth + " tiles out is the limit", "a section stops at " + Expansion.PushMaxDepth + " tiles out (" + why + ")");

        // Breaking through: a push whose face reaches another room opens into it, full width (owner Nov 14: no more "a tile off").
        var near = new List<ExpansionRoom> { list[0], new ExpansionRoom { Parent = 0, X = 1900, Y = 0, W = 600, H = 900 } };
        var bt = Expansion.Push(near, wp(1, 1, 300, 4), out idx, out cost, out why);
        Check(bt.HasValue && idx == 0 && Expansion.PushBreaksInto(near, wp(1, 1, 300, 4)) == 2, "a room a tile past the face: the push reaches it, and the label knows (" + why + ")");
        near[0] = bt.Value;
        var bw = Expansion.Build(near);
        var bd = bw.Doors.FirstOrDefault(dd => dd.Joins(1) && dd.Joins(2));
        Check(bd.Width == 400f && bd.Vertical && Math.Abs(bd.Pos.X - 1900f) < 0.1f && Expansion.PushMeets(near, 1, 2), "the section's face opens into the room its full width (" + bd.Width + ")");
        Check(!Expansion.Push(near, wp(1, 1, 300, 4), out idx, out cost, out why).HasValue, "and it can't go further (" + why + ")");
        Check(!Expansion.PushPick(near, new V2(1890f, 500f), new V2(1800f, 500f), 4).HasValue || Expansion.PushPick(near, new V2(1890f, 500f), new V2(1800f, 500f), 4).Value.Area != 1 || Expansion.PushPick(near, new V2(1890f, 500f), new V2(1800f, 500f), 4).Value.Side != 1, "an opened face isn't offered for pushing");
        var shop = new List<ExpansionRoom> { list[0], new ExpansionRoom { X = 1900, Y = 300, W = 400, H = 400, Kind = RoomKind.Shop, Floating = true } };
        Check(Expansion.Build(shop).Areas[2].Sealed, "(a floating shop room off the map is sealed)");
        var sb = Expansion.Push(shop, wp(1, 1, 300, 4), out idx, out cost, out why);
        Check(sb.HasValue, "a push can reach a floating room (" + why + ")");
        shop[0] = sb.Value;
        Check(!Expansion.Build(shop).Areas[2].Sealed && Expansion.ConnectedAreas(shop)[2], "and opens it: the shop room joins the map");
        var along = new List<ExpansionRoom> { list[0], new ExpansionRoom { Parent = 0, X = 1700, Y = 700, W = 600, H = 400 } };
        var asd = Expansion.Push(along, wp(1, 1, 300, 4), out idx, out cost, out why);
        Check(asd.HasValue && Expansion.PushBreaksInto(along, wp(1, 1, 300, 4)) == -1, "running alongside a room isn't breaking through (" + why + ")");

        // A floating room's walls can't be pushed; the map's size cap holds.
        var fl = new List<ExpansionRoom> { new ExpansionRoom { X = 3000, Y = 0, W = 400, H = 400, Kind = RoomKind.Shop, Floating = true } };
        Check(!Expansion.Push(fl, wp(1, 1, 0, 2), out idx, out cost, out why).HasValue, "a floating room's wall can't be pushed (" + why + ")");
        var wide = new List<ExpansionRoom> { new ExpansionRoom { Parent = 0, X = 1600, Y = 0, W = Expansion.MaxWidth - 1600, H = 900 } };
        Check(!Expansion.Push(wide, wp(0, 0, 200, 2), out idx, out cost, out why).HasValue && why == "The map would get too big", "the map's size cap (" + why + ")");

        // A room kit can't retype a section alone.
        Check(!Expansion.CanRetype(list, 0, new RoomToken { Kind = RoomKind.Shop }, out why), "a section isn't a room for a kit (" + why + ")");

        // Picking (step 2): the stretch of outer wall under the mouse, within reach of the player.
        var one = new List<ExpansionRoom> { list[0] };  // the hub with a section 2 tiles out of its east wall at y 300-700
        var pk = Expansion.PushPick(new List<ExpansionRoom>(), new V2(1580f, 500f), new V2(1450f, 500f), 4);
        Check(pk.HasValue && pk.Value.Area == 0 && pk.Value.Side == 1 && pk.Value.Start == 300 && pk.Value.Width == 4, "the mouse by the hub's east wall picks 4 tiles centred on it");
        var pk2 = Expansion.PushPick(one, new V2(1580f, 760f), new V2(1450f, 700f), 4);
        Check(pk2.HasValue && pk2.Value.Area == 0 && pk2.Value.Start == 700 && pk2.Value.Width == 2, "beside a section only the outer wall left is picked (2 tiles, 700-900): " + (pk2.HasValue ? pk2.Value.Start + "+" + pk2.Value.Width : "none"));
        var pk3 = Expansion.PushPick(one, new V2(1790f, 500f), new V2(1700f, 500f), 6);
        Check(pk3.HasValue && pk3.Value.Area == 1 && pk3.Value.Side == 1 && pk3.Value.Start == 300 && pk3.Value.Width == 4, "the section's far wall: all of it (a wider pick is cut to its 4 tiles)");
        var pg = Expansion.Push(one, pk3.Value, out idx, out cost, out why);
        Check(pg.HasValue && idx == 0, "and pushing that grows the section");
        Check(!Expansion.PushPick(one, new V2(1580f, 500f), new V2(200f, 450f), 4).HasValue, "a wall out of reach isn't picked");
        Check(!Expansion.PushPick(one, new V2(800f, 450f), new V2(800f, 450f), 4).HasValue, "the middle of the room is far from every wall: nothing");
        Check(Expansion.PushAreaAt(one, 1, 1800, 300, 4) == 1 && Expansion.PushAreaAt(one, 1, 1600, 700, 2) == 0 && Expansion.PushAreaAt(one, 1, 1900, 300, 4) == -1, "a locked face is found by its line (the section's, the hub's; not one that isn't there yet): " + Expansion.PushAreaAt(one, 1, 1800, 300, 4) + " " + Expansion.PushAreaAt(one, 1, 1600, 700, 2) + " " + Expansion.PushAreaAt(one, 1, 1900, 300, 4));

        // The world: the section opens full width onto the hub, and has no random cover.
        Expansion.PushStyleOverride = (int)PushStyle.None;
        var world = Expansion.Build(new List<ExpansionRoom> { list[0] });
        Expansion.PushStyleOverride = -1;
        var door = world.Doors.FirstOrDefault(dd => dd.Joins(0) && dd.Joins(1));
        Check(world.Areas.Length == 2 && door.Width == 400f && door.Vertical && Math.Abs(door.Pos.X - 1600f) < 0.1f, "the section opens onto the hub its full width (" + door.Width + ")");
        Check(world.Cover.Length == 0, "and gets no random cover (only its own features)");
        Check(world.Areas.Any(ar => ar.Contains(new V2(1700f, 500f))) && !world.Areas.Any(ar => ar.Contains(new V2(1700f, 250f))), "its floor is in the world, the wall beside it isn't");
    }

    /// Step 3: what grows in a section as it's pushed, built from its style and depth.
    static void ExpandingPushFeaturesTest()
    {
        Func<int, int, ExpansionRoom> section = (w, d) => new ExpansionRoom { Parent = 0, Leg = Expansion.PushLeg, X = 1600, Y = 100, W = d * 100, H = w * 100 };
        Check(Expansion.PushStyleFor(0, 1, 100, 3) == PushStyle.Stubs && Expansion.PushStyleFor(0, 1, 100, 2) == PushStyle.Stubs, "a 2-3 wide corridor gets stubs");
        var four = Expansion.PushStyleFor(0, 1, 100, 4);
        Check(four == PushStyle.Fins || four == PushStyle.TFins || four == PushStyle.Cover, "4 wide: fins, T-fins or cover (" + four + ")");
        var rolled = new HashSet<PushStyle>();
        for (int st = 0; st < 900; st += 100) rolled.Add(Expansion.PushStyleFor(0, 1, st, 6));
        for (int st = 0; st < 900; st += 100) rolled.Add(Expansion.PushStyleFor(0, 3, st, 7));
        Check(rolled.Count >= 4 && !rolled.Contains(PushStyle.None), "6+ wide sections roll a spread of styles (" + string.Join(", ", rolled) + ")");
        var grow1 = new List<ExpansionRoom> { section(6, 1) };
        var grow12 = new List<ExpansionRoom> { section(6, 12) };
        Check(Expansion.PushStyleOf(grow1, 0) == Expansion.PushStyleOf(grow12, 0) && Expansion.PushStyleOf(grow12, 0) == Expansion.PushStyleFor(0, 1, 100, 6), "a section keeps its style as it grows");
        Check(Expansion.PushStyleOfPush(grow1, new WallPush { Area = 1, Side = 1, Start = 100, Width = 6 }) == Expansion.PushStyleOf(grow1, 0), "the label's style for growing it is its own");

        foreach (PushStyle st in new[] { PushStyle.Fins, PushStyle.Baffles, PushStyle.Pincers, PushStyle.TFins, PushStyle.Divider, PushStyle.Cover })
        {
            Expansion.PushStyleOverride = (int)st;
            int d = 12, w = 6;
            var rooms = new List<ExpansionRoom> { section(w, d) };
            var spans = new List<WallSpan>(); var cover = new List<Solid>();
            Expansion.PushFeatures(rooms, 0, spans, cover);
            var wd = Expansion.Build(rooms);
            int inSpans = wd.Spans.Count(sp => sp.Vertical ? sp.At > 1600 && sp.At < 1600 + d * 100 && sp.From > 100 - 1 && sp.Till < 700 + 1 && sp.Till - sp.From < 600 : sp.At > 100 && sp.At < 700 && sp.From >= 1600 && sp.Till <= 1600 + d * 100 && sp.Till - sp.From < d * 100);
            Check((st == PushStyle.Cover ? cover.Count : spans.Count) > 0 && inSpans >= spans.Count && wd.Cover.Length == cover.Count, st + ": pieces built into the world (" + spans.Count + " walls, " + cover.Count + " cover)");
            float far = 1600 + (d - 1.5f) * 100 + 1;
            bool clearFace = spans.All(sp => sp.Vertical ? sp.At <= far : sp.Till <= far) && cover.All(c => c.Pos.X + Math.Max(c.HW, c.R) <= far);
            Check(clearFace, st + ": the two rows by the face stay clear");
            bool lane = spans.All(sp => !sp.Vertical || sp.Till - sp.From <= (w - 2) * 100 + 1);
            Check(lane, st + ": no wall across the section is longer than its width less 2 tiles (a lane is always left)");
            var spans8 = new List<WallSpan>(); var cover8 = new List<Solid>();
            Expansion.PushFeatures(new List<ExpansionRoom> { section(w, 8) }, 0, spans8, cover8);
            if (st != PushStyle.Divider)
                Check(spans8.All(a8 => spans.Any(b8 => b8.Vertical == a8.Vertical && b8.At == a8.At && b8.From == a8.From && b8.Till == a8.Till)) && cover8.All(c8 => cover.Any(c12 => c12.Pos.X == c8.Pos.X && c12.Pos.Y == c8.Pos.Y)), st + ": pushing further only adds pieces (what was there stays)");
            if (st == PushStyle.Divider)
                Check(spans.Count == 2 && spans.All(sp => !sp.Vertical && sp.At == 400), "the divider runs down the middle with a doorway (" + spans.Count + " pieces)");
        }
        // A 2-wide corridor: stubs from alternate sides, a 1.5-tile lane past each.
        Expansion.PushStyleOverride = -1;
        foreach (int nw in new[] { 2, 3 })
        {
            var stub = new List<WallSpan>(); var sc = new List<Solid>();
            Expansion.PushFeatures(new List<ExpansionRoom> { section(nw, 12) }, 0, stub, sc);
            Check(stub.Count >= 3 && stub.All(sp => sp.Vertical && Math.Abs(sp.Till - sp.From - (nw - 1.5f) * 100) < 1f), nw + "-wide: " + stub.Count + " stubs, each leaving a 1.5-tile lane");
        }
        // Not on a narrow section; and a section off this one's side wall keeps its opening clear.
        Expansion.PushStyleOverride = (int)PushStyle.Baffles;
        var narrow = new List<WallSpan>(); var nc = new List<Solid>();
        Expansion.PushFeatures(new List<ExpansionRoom> { section(4, 12) }, 0, narrow, nc);
        Check(narrow.Count == 0, "baffles need 5 tiles: a 4-wide section gets none");
        var withChild = new List<ExpansionRoom> { section(6, 12), new ExpansionRoom { Parent = 1, Leg = Expansion.PushLeg, X = 1700, Y = 0, W = 300, H = 100 } };
        var cw = Expansion.Build(withChild);
        bool blocked = cw.Spans.Any(sp => sp.Vertical && sp.At > 1700 && sp.At < 2000 && sp.From <= 120 && sp.Till > 120);
        Check(!blocked, "no baffle stands across the opening to a section pushed off its side wall");
        Expansion.PushStyleOverride = -1;
    }

    /// Wall push in a game: between waves only, with free building, near the wall, paid by the pusher, sent to everyone.
    static void ExpandingPushTest(float dt)
    {
        Game.PushShelved = false;  // the feature is shelved in the game; the tests run it anyway
        ExpandingPushRulesTest();
        ExpandingPushFeaturesTest();
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var ss = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Run(ss, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        var east = new WallPush { Area = 0, Side = 1, Start = 300, Width = 4 };
        V2 nearEast = new V2(1450f, 500f), far = new V2(200f, 450f);
        Action stand = () => { foreach (var p in g.Players.Values) { p.Hp = p.MaxHp; p.Downed = false; } g.Players[2].Pos = nearEast; cg.Players[2].Pos = nearEast; };
        g.Players[2].Vectors = 50;

        Check(!g.CanPush && !cg.CanPush, "mid-wave, no pushing");
        cg.PushWall(east);
        Run(ss, dt, 0.4f, null, stand);
        Check(g.Expanded.Count == 0, "a push mid-wave is refused");

        foreach (var gg in new[] { g, cg }) gg.ExpandOpen = true;
        Check(g.CanPush && cg.CanPush, "between waves (a building round), pushing is on");
        cg.PushWall(east);
        Run(ss, dt, 0.6f, () => cg.Expanded.Count == 1, stand);
        Check(g.Expanded.Count == 1 && cg.Expanded.Count == 1 && Expansion.IsPush(cg.Expanded[0]) && cg.Expanded[0].Owner == 2, "a friend's push is built and sent to everyone");
        Run(ss, dt, 1f, () => cg.Players[2].Vectors == 44, stand);
        Check(g.Players[2].Vectors == 44 && cg.Players[2].Vectors == 44, "and they pay its new wall (4 + 2 vectors): " + g.Players[2].Vectors);
        Check(g.RoomsUp && g.CurrentWorld.Areas.Length == 2, "the map goes up with just a pushed wall (" + g.CurrentWorld.Areas.Length + " areas)");

        // Pushing again (after the rate gap) grows the same section.
        var grow = new WallPush { Area = 1, Side = 1, Start = 300, Width = 4 };
        Run(ss, dt, 0.3f, null, stand);
        cg.PushWall(grow);
        Run(ss, dt, 0.6f, () => cg.Expanded.Count == 1 && cg.Expanded[0].W == 200, stand);
        Check(g.Expanded.Count == 1 && g.Expanded[0].W == 200 && cg.Expanded[0].W == 200, "a second push grows the section a tile");

        // Too far from the wall, too fast, too poor.
        Action standFar = () => { stand(); g.Players[2].Pos = far; cg.Players[2].Pos = far; };
        Run(ss, dt, 0.3f, null, standFar);
        cg.PushWall(grow);
        Run(ss, dt, 0.6f, null, standFar);
        Check(g.Expanded[0].W == 200, "a push from across the room is refused");
        Run(ss, dt, 0.3f, null, stand);
        cg.PushWall(grow); cg.PushWall(grow); cg.PushWall(grow);
        Run(ss, dt, 0.5f, null, stand);
        Check(g.Expanded[0].W == 300, "three pushes sent at once count once (the rate gap): " + g.Expanded[0].W);
        g.Players[2].Vectors = 1;
        Run(ss, dt, 0.3f, null, stand);
        cg.PushWall(grow);
        Run(ss, dt, 0.5f, null, stand);
        Check(g.Expanded[0].W == 300 && g.Players[2].Vectors == 1, "a push you can't pay for is refused");
        g.Players[2].Vectors = 40;

        // A save keeps the section.
        var save = RunSave.Parse(new RunSave { HasExpansion = true, Rooms = { g.Expanded[0] }, Seats = { new SeatSave { Name = "Host" } } }.ToText());
        Check(save != null && save.Rooms.Count == 1 && Expansion.IsPush(save.Rooms[0]) && save.Rooms[0].W == 300, "a saved run keeps its pushed sections");

        // Team vote: off.
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.BuildVote, 1f);
        Check(!g.CanPush && !cg.CanPush, "with Team vote building, pushing is off");
        Run(ss, dt, 0.3f, null, stand);
        cg.PushWall(grow);
        Run(ss, dt, 0.5f, null, stand);
        Check(g.Expanded[0].W == 300, "and a push is refused");
    }
}

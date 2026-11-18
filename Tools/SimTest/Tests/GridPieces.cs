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

    static void GridCardsTest()
    {
        bool ok = true;
        foreach (GridPiece p in Enum.GetValues(typeof(GridPiece)))
            foreach (var f in new[] { PathId.None, PathId.SwarmFields, PathId.Bazaar })
            {
                var c = Expansion.PieceCard(p, f);
                ok &= Expansion.IsPieceCard(c) && Expansion.CardPiece(c) == p && Expansion.CardFloor(c) == f && Expansion.IsGridCard(c) && (int)c < BossCards.KitBase && (int)c <= 255;
            }
        Check(ok, "every piece, plain or on a floor, is a hand code that reads back (32 up, below the kit codes)");
        Check(!Expansion.IsPieceCard(RoomCard.Box) && !Expansion.IsGridCard(RoomCard.Box) && !Expansion.IsGridCard(RoomCard.MiniBoss) && Expansion.IsGridCard(RoomCard.Shop) && Expansion.IsGridCard(RoomCard.BuilderShop), "the old shape cards and the Mini-boss card aren't played in a Grid run; service cards are");
        Check(Expansion.GridCardShape(RoomCard.Market) == GridPiece.Mono && Expansion.GridCardShape(Expansion.PieceCard(GridPiece.TetL)) == GridPiece.TetL, "a service card is a single cell");
        Check(Expansion.CardName(Expansion.PieceCard(GridPiece.TriL)) == "Corner" && Expansion.CardName(Expansion.PieceCard(GridPiece.TetT, PathId.FrozenLake)).EndsWith(" T") && Expansion.CardPrice(Expansion.PieceCard(GridPiece.TetO)) == 24 && Expansion.CardPrice(Expansion.PieceCard(GridPiece.Mono, PathId.Sanctuary)) == 14,
            "names and prices: a cell is 6 vectors, a floor 8 more");
        Check(Expansion.PieceModMax == 13 && Expansion.FirstFreeMod == (int)PathId.Bazaar + 1 && Expansion.ModKind(0) == PieceModKind.None && Expansion.ModKind((int)PathId.FrozenLake) == PieceModKind.Floor && Expansion.ModKind(Expansion.FirstFreeMod) == PieceModKind.Unknown
            && Expansion.PieceCardMod(GridPiece.TetS, (int)PathId.BloodMoon) == Expansion.PieceCard(GridPiece.TetS, PathId.BloodMoon) && (int)Expansion.PieceCardMod(GridPiece.TetL, Expansion.PieceModMax) <= 255,
            "the modifier slot: 0 plain, 1-10 the floors (same codes as before), 11-13 kept for modifiers to come, all inside a byte");
        var free = Expansion.PieceCardMod(GridPiece.Domino, Expansion.FirstFreeMod);
        Check(Expansion.IsPieceCard(free) && Expansion.CardPiece(free) == GridPiece.Domino && Expansion.CardMod(free) == Expansion.FirstFreeMod && Expansion.CardFloor(free) == PathId.None && !Expansion.IsGridCard(free), "a card with a modifier not defined yet reads back but isn't playable");
        var modded = Expansion.ApplyPieceMod(new ExpansionRoom { Kind = Arena.RoomKind.Combat }, (int)PathId.Sanctuary);
        var plainRoom = Expansion.ApplyPieceMod(new ExpansionRoom { Kind = Arena.RoomKind.Combat }, 0);
        Check(modded.Kind == Arena.RoomKind.Floor && modded.Floor == PathId.Sanctuary && plainRoom.Kind == Arena.RoomKind.Combat && Expansion.CardMod(RoomCard.Shop) == 0, "a floor modifier makes the room a floor room; no modifier leaves it a combat room");
        var allPieces = new[] { MysteryRoom.Small, MysteryRoom.Medium, MysteryRoom.Hallway }.SelectMany(Mystery.PiecesOf).Distinct().Count();
        Check(allPieces == Enum.GetValues(typeof(GridPiece)).Length, "every piece shape can come from a mystery door (" + allPieces + " shapes)");
        var hand = Expansion.GridStartHand(5);
        Check(hand.Length == 3 && hand[0] == Expansion.PieceCard(GridPiece.Mono) && hand[1] == Expansion.PieceCard(GridPiece.Domino) && Expansion.GridPieceCells(Expansion.CardPiece(hand[2])) == 3 && hand.SequenceEqual(Expansion.GridStartHand(5)), "the starting hand: a single, a domino and a tromino, the same each time");
        Check(Expansion.GridRoundCard(1) == Expansion.PieceCard(GridPiece.Domino) && Expansion.GridRoundCard(2) == Expansion.PieceCard(GridPiece.Mono), "the round card: a domino after odd waves, a single after even");
        int service = 0, themed = 0, tet = 0, pieces = 0, bad = 0;
        for (int i = 0; i < 600; i++)
        {
            var c = Expansion.GridDropCard(new DetRandom(i * 37 + 1), 30);
            if (!Expansion.IsGridCard(c)) bad++;
            if (!Expansion.IsPieceCard(c)) { service++; continue; }
            pieces++;
            if (Expansion.CardFloor(c) != PathId.None) themed++;
            if (Expansion.GridPieceCells(Expansion.CardPiece(c)) == 4) tet++;
        }
        Check(bad == 0 && service >= 80 && service <= 160 && themed >= 60 && themed <= 200 && tet >= 120, "drops: about 1 in 5 a service card, a quarter of the pieces on a floor, a third tetrominoes (" + service + " service, " + themed + " floor, " + tet + " of " + pieces + " pieces)");
        var offers = Expansion.GridCardOffers(7, 0);
        Check(offers.Length == Expansion.CardOfferCount && offers.All(Expansion.IsGridCard) && offers.SequenceEqual(Expansion.GridCardOffers(7, 0)) && Expansion.GridCardOffers(7, 1).Contains(RoomCard.BuilderShop), "the vendor's three cards are grid cards, the same each time, a Builder Shop one cycle in three");
        var shelf = Expansion.GridBuilderOffers(7, 3);
        Check(shelf.Length == Expansion.BuilderOfferCount && shelf.All(Expansion.IsGridCard) && Expansion.BuilderPrice(shelf[0]) < Expansion.CardPrice(shelf[0]), "the Builder Shop's five are grid cards at a quarter off");
        var spoils = BossCards.Offer(new DetRandom(3), Expansion.GridCore(), new List<RoomToken>(), 12);
        Check(spoils.Length == BossCards.OfferSize && spoils.Distinct().Count() == spoils.Length && spoils.All(c => BossCards.IsKit(c) || Expansion.IsGridCard((RoomCard)c)) && !spoils.Contains((int)RoomCard.MiniBoss), "a Grid boss pick: pieces, service cards and floor kits, no Mini-boss card");
        Check(new Difficulty().ToText() == "Normal" && Difficulty.Parse("Normal").SameAs(new Difficulty()), "a preset saves as its name alone (the retired building knobs aren't written)");
        var save = new RunSave();
        var seat = new SeatSave { Vectors = 30, VectorCap = 70 };
        seat.Hand.AddRange(new[] { (int)Expansion.PieceCard(GridPiece.TetL, PathId.Fogbound), (int)Expansion.PieceCard(GridPiece.Mono), (int)RoomCard.Shop });
        save.Seats.Add(seat);
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats.Count == 1 && back.Seats[0].Hand.SequenceEqual(seat.Hand), "a hand of pieces is saved and read back");
    }

    // Step 4: a mini-boss room waits sealed on the lattice, two cells, one cell out from the map; a piece into the cell between opens it.
    static void GridMiniTest()
    {
        var core = Expansion.GridCore();
        var mini = Expansion.GridMiniFor(9, 0, core, new List<MapNode>());
        Check(mini.HasValue, "a mini-boss room finds a spot off the core");
        if (!mini.HasValue) return;
        var m = mini.Value;
        bool lattice = (m.X - Expansion.GridX0) % 600 == 0 && (m.Y - Expansion.GridY0) % 600 == 0 && (m.W == 1200 && m.H == 600 || m.W == 600 && m.H == 1200);
        Check(m.Floating && m.Kind == RoomKind.Gatekeeper && lattice && Expansion.GridMiniFor(9, 0, core, new List<MapNode>()).Value.Equals(m), "it is two cells on the lattice, a floating mini-boss room, the same each time (" + m.X + "," + m.Y + " " + m.W + "x" + m.H + ")");
        var map = new List<ExpansionRoom>(core) { m };
        Check(Expansion.GridMinisWaiting(map) == 1 && Expansion.BoughtCount(map) == 0 && !core.Any(c => c.X < m.X1 + 1 && m.X - 1 < c.X1 && c.Y < m.Y1 + 1 && m.Y - 1 < c.Y1), "it waits sealed, apart from the map, and isn't a bought room");
        Check(!Expansion.GridSpots(map).Any(sp => sp.X >= m.X - 600 && sp.X < m.X1 && sp.Y >= m.Y - 600 && sp.Y < m.Y1 && (sp.SX == m.X || sp.SX == m.X1 || sp.SY == m.Y || sp.SY == m.Y1)), "a sealed room has no door spots of its own");
        var bridge = Expansion.GridPieceOptions(map, GridPiece.Mono).FirstOrDefault(o => o.Rooms.Any(r => (r.X1 == m.X || r.X == m.X1) && r.Y < m.Y1 && m.Y < r.Y1 || (r.Y1 == m.Y || r.Y == m.Y1) && r.X < m.X1 && m.X < r.X1));
        Check(bridge.Rooms != null, "a single fits in the cell between the map and the mini-boss room");
        if (bridge.Rooms == null) return;
        map.AddRange(bridge.Rooms);
        var w = Expansion.Build(map);
        int area = core.Count + 1;
        Check(Expansion.GridMinisWaiting(map) == 0 && !w.Areas[area].Sealed && w.Areas[area].Kind == RoomKind.Gatekeeper, "built, it joins the map: the mini-boss room is open");
        Check(Expansion.GridSpots(map).Any(sp => sp.SX == m.X || sp.SX == m.X1 || sp.SY == m.Y || sp.SY == m.Y1), "and its walls get door spots");
    }

    // Owner Nov 18: a Grid run's boss surprise rooms and breach rooms (Stashes, the gold breach's room, the Engineer's Breach) sit on the lattice.
    static void GridSurpriseTest()
    {
        var core = Expansion.GridCore();
        Func<ExpansionRoom, bool> onLattice = r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0;
        var stash = Expansion.SurpriseOptionsFor(true, core, SurpriseShape.Stash, new V2(2100f, 500f));
        Check(stash.Count > 0 && stash.All(o => o.Rooms.Count == 1 && o.Rooms[0].W == 600 && o.Rooms[0].H == 600 && onLattice(o.Rooms[0]) && o.Rooms[0].X < 2100 && 2100 < o.Rooms[0].X1 && o.Rooms[0].Surprise == 1 + (int)SurpriseShape.Stash && o.Rooms[0].Price == 0),
            "a Stash behind a breach on the core's right wall is the cell behind it, free, marked a Stash (" + stash.Count + ")");
        foreach (var shape in new[] { SurpriseShape.Ring, SurpriseShape.Cross, SurpriseShape.LHall, SurpriseShape.THall, SurpriseShape.Chamfer })
        {
            string why;
            var pick = Expansion.SurpriseFor(true, 5, 11, core, shape, out why);
            int area = pick.HasValue ? pick.Value.Rooms.Sum(r => r.W * r.H) : 0;
            Check(pick.HasValue && pick.Value.Rooms.All(onLattice) && area == 4 * 600 * 600 && pick.Value.Rooms.All(r => r.Surprise == 1 + (int)shape) && Expansion.SurpriseFor(true, 5, 11, core, shape, out why).Value.X == pick.Value.X,
                "a boss's " + Expansion.SurpriseName(shape) + " gift is a 4-cell piece on the lattice, the same each time");
        }
    }

    static void GridPiecesGameTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
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
        Check(g.Expanded.Count == 4 && cg.Expanded.Count == 4, "the core is up first");

        // The first build round deals the hand: the starting pieces and the round's card, and the friend's game has them.
        typeof(Game).GetMethod("OpenBuildRound", flags).Invoke(g, null);
        var friend = g.Players[2];
        Check(Game.HandCount(friend) == 4 && friend.Hand.Take(4).All(h => Expansion.IsPieceCard((RoomCard)h)), "the first round deals three pieces and the round's card (" + string.Join(",", friend.Hand.Take(4)) + ")");
        Run(s, dt, 1f, () => cg.ExpandOpen && Game.HandCount(cg.Players[2]) == 4, noPicks);
        Check(cg.Players[2].Hand.SequenceEqual(friend.Hand), "the friend's game sees the same hand");

        // Playing a card: built at once as its piece, free, the card leaves the hand.
        int vec = friend.Vectors;
        var card = (RoomCard)friend.Hand[2];
        var opt = Expansion.GridPieceOptions(g.Expanded, Expansion.CardPiece(card)).First();
        cg.PlayGridCard(2, opt.Orient, opt.X, opt.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == 4 + opt.Rooms.Count, noPicks);
        Check(g.Expanded.Count == 4 + opt.Rooms.Count && cg.Expanded.Count == g.Expanded.Count && g.Expanded.Skip(4).All(r => r.Owner == 2 && r.Kind == Arena.RoomKind.Combat), "the friend's piece is built at once on both screens, all its boxes theirs");
        Run(s, dt, 0.5f, () => Game.HandCount(cg.Players[2]) == 3, noPicks);
        Check(Game.HandCount(friend) == 3 && Game.HandCount(cg.Players[2]) == 3 && friend.Vectors == vec && Expansion.BoughtCount(g.Expanded) == 1, "the card leaves the hand, it cost nothing, and the open room counts as one room");

        // Refusals keep the card: a spot off the lattice, an empty slot, a card that isn't a grid card.
        int n = g.Expanded.Count;
        var held = friend.Hand.ToArray();
        cg.PlayGridCard(0, 0, opt.X + 100, opt.Y);
        cg.PlayGridCard(5, 0, opt.X, opt.Y);
        friend.Hand[1] = (byte)RoomCard.Box;
        cg.PlayGridCard(1, 0, opt.X, opt.Y);
        Run(s, dt, 0.6f, null, noPicks);
        Check(g.Expanded.Count == n && friend.Hand[0] == held[0] && friend.Hand[1] == (byte)RoomCard.Box, "an off-lattice spot, an empty slot and an old Box card are refused, the cards stay");

        // A floor piece is a floor room; a service card is one cell with its job.
        friend.Hand[0] = (byte)Expansion.PieceCard(GridPiece.Domino, PathId.BurningGrounds);
        var fo = Expansion.GridPieceOptions(g.Expanded, GridPiece.Domino).First();
        cg.PlayGridCard(0, fo.Orient, fo.X, fo.Y);
        Run(s, dt, 1f, () => g.Expanded.Count > n, noPicks);
        var made = g.Expanded.Skip(n).ToList();
        Check(made.Count > 0 && made.All(r => r.Kind == Arena.RoomKind.Floor && r.Floor == PathId.BurningGrounds), "a Burning Grounds domino builds a floor room");
        friend.Hand[0] = (byte)RoomCard.Shop;
        int n2 = g.Expanded.Count;
        var so = Expansion.GridPieceOptions(g.Expanded, GridPiece.Mono).First();
        cg.PlayGridCard(0, 0, so.X, so.Y);
        Run(s, dt, 1f, () => g.Expanded.Count > n2, noPicks);
        var shop = g.Expanded.Last();
        Check(g.Expanded.Count == n2 + 1 && shop.Kind == Arena.RoomKind.Shop && shop.W == 600 && shop.H == 600, "a Shop card builds one cell as a shop");

        // Sources: a chest gives everyone a grid card; the vendor sells grid cards for vectors; the Builder Shop has five.
        var chest = typeof(Game).GetMethod("ChestCards", flags);
        var before = g.Players.Values.ToDictionary(p => p.Id, p => Game.HandCount(p));
        for (int i = 0; i < Expansion.HandMax; i++) { foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length); }
        chest.Invoke(g, new object[] { 3 });
        Check(g.Players.Values.All(p => Game.HandCount(p) == 1 && Expansion.IsGridCard((RoomCard)p.Hand[0])), "a chest gives everyone a grid card");
        var buy = typeof(Game).GetMethod("BuyRoomCard", flags);
        friend.Vectors = 100;
        var offer0 = g.VendorCards[0];
        buy.Invoke(g, new object[] { friend, 0 });
        Check(Game.HandCount(friend) == 2 && friend.Vectors == 100 - Expansion.CardPrice(offer0) && Expansion.IsGridCard(offer0), "the vendor sells a grid card for its price in vectors (" + Expansion.CardName(offer0) + ", " + Expansion.CardPrice(offer0) + ")");
        Check(g.BuilderCards.Length == 5 && g.BuilderCards.All(Expansion.IsGridCard), "the Builder Shop sells five grid cards");

        // A bot plays a piece from its hand.
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        g.Players[1].Hand[0] = (byte)Expansion.PieceCard(GridPiece.TriL);
        int n3 = g.Expanded.Count;
        bool played = (bool)typeof(Game).GetMethod("BotPlayGridCard", flags).Invoke(g, null);
        Check(played && g.Expanded.Count > n3 && Game.HandCount(g.Players[1]) == 0, "a bot plays its piece at a door spot");
        // Mystery doors: the room behind each is a piece on the lattice; a small room (a shop or vendor) is one cell.
        var roll = typeof(Game).GetMethod("RollMysteryDoors", flags);
        var plans = (System.Collections.IList)typeof(Game).GetField("mysteryPlans", flags).GetValue(g);
        bool lattice = true, cellSmall = true; int seen = 0;
        for (int w = 0; w < 6; w++)
        {
            g.Wave = 7 + w;
            roll.Invoke(g, null);
            foreach (SurpriseOption pl in plans)
            {
                seen++;
                lattice &= pl.Rooms.All(r => (r.X - Expansion.GridX0) % 600 == 0 && (r.Y - Expansion.GridY0) % 600 == 0 && r.W % 600 == 0 && r.H % 600 == 0) && pl.Price > 0;
                if (pl.Rooms.Count == 1 && pl.Rooms[0].W == pl.Rooms[0].H) cellSmall &= pl.Rooms[0].W == 600;
            }
        }
        Check(seen > 0 && lattice && cellSmall, "mystery door rooms are pieces on the lattice, priced, a small one exactly a cell (" + seen + " plans)");
        var place = typeof(Game).GetMethod("PlaceGridMini", flags);
        int before4 = g.Expanded.Count;
        bool first = (bool)place.Invoke(g, new object[] { 1 }), second = (bool)place.Invoke(g, new object[] { 1 });
        var waiting = g.Expanded.Skip(before4).ToList();
        Check(first && !second && waiting.Count == 1 && waiting[0].Floating && waiting[0].Kind == Arena.RoomKind.Gatekeeper && waiting[0].Mini == (byte)g.MiniBossNow(), "a boss cycle places one mini-boss room on the lattice, holding the coming boss's faction's mini-boss; one waits at a time");
    }

    /// Test helper: `who` plays a piece (put in their last hand slot on the host) at its first place round the map (or the first `where` accepts),
    /// and the run waits until both screens have it. Returns the place, or null if none fits.
    static GridPieceOption? PlayPiece(Session[] s, float dt, Game host, Game who, GridPiece piece, PathId floor = PathId.None, Func<GridPieceOption, bool> where = null)
    {
        var hand = host.Players[who.MyId].Hand;
        hand[hand.Length - 1] = (byte)Expansion.PieceCard(piece, floor);
        var opts = Expansion.GridPieceOptions(host.Expanded, piece).Where(o => where == null || where(o)).ToList();
        if (opts.Count == 0)
            return null;
        int n = host.Expanded.Count;
        who.PlayGridCard(hand.Length - 1, opts[0].Orient, opts[0].X, opts[0].Y);
        Run(s, dt, 1f, () => host.Expanded.Count > n && s.All(x => x.Game.Expanded.Count == host.Expanded.Count));
        return opts[0];
    }


    /// Test helper: the Grid core and these pieces, each at the first place round the map on the core's east side (else anywhere).
    static List<ExpansionRoom> GridMap(params GridPiece[] pieces)
    {
        var map = Expansion.GridCore();
        foreach (var p in pieces)
        {
            var opts = Expansion.GridPieceOptions(map, p);
            var o = opts.FirstOrDefault(q => q.Side == 3);
            if (o.Rooms == null && opts.Count > 0) o = opts[0];
            if (o.Rooms != null) map.AddRange(o.Rooms);
        }
        return map;
    }

    /// Test helper: these games' maps are this one, with the core's inner walls open (as TickGridCore builds it).
    static void UseMap(IEnumerable<Game> games, List<ExpansionRoom> map)
    {
        foreach (var g in games)
        {
            g.Expanded.Clear(); g.Expanded.AddRange(map);
            g.Openings.Clear(); g.Openings.AddRange(Expansion.GridCoreCuts());
            typeof(Game).GetField("expWorld", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(g, null);
        }
    }

}

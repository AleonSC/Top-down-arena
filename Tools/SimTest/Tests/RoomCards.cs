using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Room cards (Docs/RoomCards.md): every card's room fits the starting map and rebuilds the same from its spot; a player's hand is dealt,
// synced, played (the host builds it at once), refused where it doesn't fit; drawing is off; boss drops, the mini-boss card, the vendor and saves.
static partial class Program
{
    static void RoomCardsTest()
    {
        var empty = new List<ExpansionRoom>();
        foreach (RoomCard c in Enum.GetValues(typeof(RoomCard)))
        {
            if (c == RoomCard.None) continue;
            var opts = Expansion.CardOptions(empty, c);
            Check(opts.Count > 0, "the " + Expansion.CardName(c) + " card fits the starting map (" + opts.Count + " spots)");
            if (opts.Count == 0) continue;
            string why;
            var o = opts[0];
            var again = Expansion.CardRooms(empty, c, o.X, o.Y, o.Turn, o.Mirror, out why);
            Check(again != null && again.Count == o.Rooms.Count && again.Zip(o.Rooms, (a, b) => a.X == b.X && a.Y == b.Y && a.W == b.W && a.H == b.H).All(x => x)
                  && again.All(q => q.Price == 0 && q.Surprise == 0),
                "  its best spot rebuilds the same from (x, y, turn, mirror), free and unmarked" + (why != null ? " (" + why + ")" : ""));
            var placed = new List<ExpansionRoom>(again ?? new List<ExpansionRoom>());
            Check(Expansion.CardRooms(placed, c, o.X, o.Y, o.Turn, o.Mirror, out why) == null, "  and can't go on top of itself (" + why + ")");
            Check(Expansion.CardRooms(empty, c, o.X + 50, o.Y, o.Turn, o.Mirror, out why) == null, "  and must sit on the grid");
        }
        // Inside the rooms: each card's room gets its designed obstacles (not random cover or side walls), every one on the room's floor and
        // clear of its door, in whichever turn it was placed.
        foreach (RoomCard c in Enum.GetValues(typeof(RoomCard)))
        {
            if (c == RoomCard.None || Expansion.IsServiceCard(c)) continue;  // (service rooms stay clear: checked below)
            var opts = Expansion.CardOptions(empty, c);
            int okSpots = 0, tried = 0, fewest = 99;
            for (int i = 0; i < opts.Count; i += Math.Max(1, opts.Count / 10))
            {
                tried++;
                var o = opts[i];
                var made = Expansion.CardRooms(empty, c, o.X, o.Y, o.Turn, o.Mirror, out _);
                var w = Expansion.Build(made);
                var inRoom = w.Cover.Where(s => made.Any(q => s.Pos.X >= q.X && s.Pos.X <= q.X1 && s.Pos.Y >= q.Y && s.Pos.Y <= q.Y1)).ToList();
                bool onFloor = inRoom.All(s =>
                {
                    float hw = s.Round ? s.R : s.HW, hh = s.Round ? s.R : s.HH;
                    return new[] { new V2(s.Pos.X - hw + 1, s.Pos.Y - hh + 1), new V2(s.Pos.X + hw - 1, s.Pos.Y - hh + 1), new V2(s.Pos.X - hw + 1, s.Pos.Y + hh - 1), new V2(s.Pos.X + hw - 1, s.Pos.Y + hh - 1) }
                        .All(pt => made.Any(q => pt.X >= q.X && pt.X <= q.X1 && pt.Y >= q.Y && pt.Y <= q.Y1));
                });
                var door = w.Doors.Where(d => (d.A == 0) != (d.B == 0)).ToList();
                bool doorClear = inRoom.All(s => door.All(d => s.Pos.DistanceTo(d.Pos) > d.W / 2f + 40f));
                bool noFins = w.Spans.All(sp => { var m = sp.Vertical ? new V2(sp.At, (sp.From + sp.Till) / 2f) : new V2((sp.From + sp.Till) / 2f, sp.At); return !made.Any(q => m.X > q.X + 1 && m.X < q.X1 - 1 && m.Y > q.Y + 1 && m.Y < q.Y1 - 1); });
                fewest = Math.Min(fewest, inRoom.Count);
                if (inRoom.Count >= 2 && onFloor && doorClear && noFins) okSpots++;
            }
            Check(tried > 0 && okSpots == tried, "the " + Expansion.CardName(c) + " room has its obstacles on its floor, clear of the door, no side walls (" + okSpots + " of " + tried + " spots, at least " + fewest + " pieces)");
        }
        Check(Expansion.CardRooms(empty, RoomCard.Box, 300, 200, 0, false, out _) == null, "a card can't be played in the middle of the start room");
        // Service cards (Docs/ArmsDealer.md): each builds a tiny 4 x 4 room with its job and nothing in it; drops roll them now and then.
        foreach (var c in Expansion.ServiceCards)
        {
            var o = Expansion.CardOptions(empty, c)[0];
            var made = Expansion.CardRooms(empty, c, o.X, o.Y, o.Turn, o.Mirror, out _);
            var w = Expansion.Build(made);
            Check(made.Count == 1 && made[0].W == Expansion.ShopRoomSide && made[0].H == Expansion.ShopRoomSide
                  && !w.Cover.Any(sd => sd.Pos.X >= made[0].X && sd.Pos.X <= made[0].X1 && sd.Pos.Y >= made[0].Y && sd.Pos.Y <= made[0].Y1),
                "the " + Expansion.CardName(c) + " card builds a clear 4 x 4 room");
        }
        Check(Expansion.CardRoomKind(RoomCard.Shop) == RoomKind.Shop && Expansion.CardRoomKind(RoomCard.Market) == RoomKind.Vendor
              && Expansion.CardRoomKind(RoomCard.Arms) == RoomKind.Arms && Expansion.CardRoomKind(RoomCard.Ring) == RoomKind.Combat, "each service card names its room's job");
        var drops = Enumerable.Range(0, 2000).Select(i => Expansion.DropCard(i * 7919 + 3)).ToList();
        int service = drops.Count(Expansion.IsServiceCard);
        Check(service > 300 && service < 500 && Expansion.ServiceCards.All(c => drops.Contains(c)) && drops.All(c => Expansion.IsServiceCard(c) || Expansion.ShapeCards.Contains(c)),
            "a dropped card is a service card about 1 in " + Expansion.ServiceCardChance + " times (" + service + " of 2000), else a shape");
        Check(Enumerable.Range(0, 60).All(cy => Expansion.CardOffers(11, cy).All(c => !Expansion.IsServiceCard(c))), "the Black Market doesn't sell service cards");
        var offers = Expansion.CardOffers(7, 2);
        Check(offers.Length == Expansion.CardOfferCount && offers.SequenceEqual(Expansion.CardOffers(7, 2)), "the vendor's cards are the same every time for a seed and cycle");
        Check(Enumerable.Range(0, 9).Count(cy => Expansion.CardOffers(5, cy).Contains(RoomCard.MiniBoss)) == 3, "the vendor sells a mini-boss card one cycle in three");
        Check(Enumerable.Range(0, 30).All(cy => { var of = Expansion.CardOffers(11, cy); return (of[0] == RoomCard.Box || of[0] == RoomCard.Hall) && of[1] != of[2]; }),
            "each shelf: a basic card and two different others");

        // A saved hand comes back.
        var save = new RunSave();
        var seat = new SeatSave { Vectors = 30, VectorCap = 70 };
        seat.Hand.AddRange(new[] { (int)RoomCard.Ring, (int)RoomCard.MiniBoss });
        save.Seats.Add(seat);
        var back = RunSave.Parse(save.ToText());
        Check(back.Seats.Count == 1 && back.Seats[0].Hand.SequenceEqual(seat.Hand), "a seat's hand is saved and read back");
    }

    static void RoomCardsGameTest(float dt)
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
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.RoomCards, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); }
        Check(g.RoomCardsOn && cg.RoomCardsOn && g.FreeBuild && !g.BuildVoting, "room cards on: builds go up at once, no vote");
        g.Diff.Set(DiffKnob.BuildVote, 1f);
        Check(!g.BuildVoting, "even with the team vote set, cards never go to a vote");
        g.Diff.Set(DiffKnob.BuildVote, 0f);

        // The first build round deals the starting hand (Box, Hall, a shape) and the round's basic card, and the friend sees theirs.
        typeof(Game).GetMethod("OpenBuildRound", flags).Invoke(g, null);  // (mid-wave here: the round stays open, as in ExpandingFreeBuildTest)
        var friend = g.Players[2];
        Check(Game.HandCount(friend) == 4 && friend.Hand[0] == (byte)RoomCard.Box && friend.Hand[1] == (byte)RoomCard.Hall
              && Expansion.ShapeCards.Contains((RoomCard)friend.Hand[2]), "the first round deals Box, Hall, a shape and the round's card (" + string.Join(",", friend.Hand) + ")");
        Run(s, dt, 1f, () => cg.ExpandOpen && Game.HandCount(cg.Players[2]) == 4, noPicks);
        Check(cg.ExpandOpen && cg.Players[2].Hand.SequenceEqual(friend.Hand), "the friend's game has their hand");

        // Drawing is off: a drawn room is refused. (The map may already hold this cycle's sealed shop, vendor and mini-boss rooms.)
        int start = g.Expanded.Count;
        cg.ProposeExpandRoom(1600, 100, 800, 600);
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Expanded.Count == start, "drawing a room does nothing with cards on");

        // The friend plays their Ring card (or whatever shape came) at its best spot: built at once, the card leaves the hand.
        int slot = 2;
        var card = (RoomCard)friend.Hand[slot];
        var opt = Expansion.CardOptions(cg.Expanded, card)[0];
        int vec = friend.Vectors;
        cg.PlayRoomCard(slot, opt.X, opt.Y, opt.Turn, opt.Mirror);
        Run(s, dt, 1f, () => cg.Expanded.Count == start + opt.Rooms.Count, noPicks);
        Check(g.Expanded.Count == start + opt.Rooms.Count && cg.Expanded.Count == g.Expanded.Count && g.Expanded.Skip(start).All(q => q.Owner == 2 && q.Price == 0),
            "a played " + Expansion.CardName(card) + " is built for the friend at once (" + g.Expanded.Count + " boxes)");
        Check(Game.HandCount(friend) == 3, "the card leaves the hand");
        Check(friend.Vectors == vec, "playing a card costs no vectors");

        // The same spot again: refused, nothing changes.
        int before = g.Expanded.Count, held = Game.HandCount(friend);
        cg.PlayRoomCard(0, opt.X, opt.Y, opt.Turn, opt.Mirror);
        Run(s, dt, 0.5f, null, noPicks);
        Check(g.Expanded.Count == before && Game.HandCount(friend) == held, "a card that doesn't fit is refused and kept");

        // A boss gives no free cards now: its reward is the card pick (BossCards.cs, tested in suite armsdealer).
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        g.Wave = 10;
        typeof(Game).GetMethod("DropWaveCards", flags).Invoke(g, new object[] { true });
        Check(g.Players.Values.All(p => Game.HandCount(p) == 0), "a boss drops no free cards (its card pick replaces them)");
        g.Players.Values.First().Hand[0] = (byte)RoomCard.MiniBoss;

        // The mini-boss card builds a duel room holding the coming boss's faction mini-boss.
        var mbOwner = g.Players.Values.First(p => p.Hand.Contains((byte)RoomCard.MiniBoss));
        int mbSlot = Array.IndexOf(mbOwner.Hand, (byte)RoomCard.MiniBoss);
        var mbOpt = Expansion.CardOptions(g.Expanded, RoomCard.MiniBoss)[0];
        typeof(Game).GetMethod("HostPlayCard", flags).Invoke(g, new object[] { mbOwner.Id, mbSlot, mbOpt.X, mbOpt.Y, (int)mbOpt.Turn, mbOpt.Mirror });
        var duel = g.Expanded[g.Expanded.Count - 1];
        Check(duel.Kind == RoomKind.Gatekeeper && duel.Mini == (byte)g.MiniBossNow() && !mbOwner.Hand.Contains((byte)RoomCard.MiniBoss), "the mini-boss card builds a duel room with the faction mini-boss");

        // Service cards build their tiny room, already open: a shop, a Black Market and an Arms Dealer, all safe from spawns.
        foreach (var sc in Expansion.ServiceCards)
        {
            Array.Clear(friend.Hand, 0, friend.Hand.Length);
            friend.Hand[0] = (byte)sc;
            var so = Expansion.CardOptions(g.Expanded, sc)[0];
            int n0 = g.Expanded.Count;
            typeof(Game).GetMethod("HostPlayCard", flags).Invoke(g, new object[] { friend.Id, 0, so.X, so.Y, (int)so.Turn, so.Mirror });
            var built = g.Expanded[g.Expanded.Count - 1];
            var area = g.ExpansionWorld.Areas[g.Expanded.Count];
            float weight = (float)typeof(Game).GetMethod("SpawnWeight", flags).Invoke(g, new object[] { area });
            Check(g.Expanded.Count == n0 + 1 && built.Kind == Expansion.CardRoomKind(sc) && built.Owner == friend.Id && !area.Sealed && area.Kind == built.Kind && weight == 0f
                  && Game.HandCount(friend) == 0, "the " + Expansion.CardName(sc) + " card builds an open, safe " + built.Kind + " room");
        }
        Check(g.HasShopRoom && g.HasVendorRoom && g.ExpansionWorld.Areas.Any(a => a.Kind == RoomKind.Arms), "the map now has a shop, a Black Market and an Arms Dealer");

        // A full hand drops what comes in.
        for (int i = 0; i < friend.Hand.Length; i++) friend.Hand[i] = (byte)RoomCard.Box;
        typeof(Game).GetMethod("GiveCard", flags).Invoke(g, new object[] { friend, RoomCard.Ring, "test" });
        Check(friend.Hand.All(h => h == (byte)RoomCard.Box), "a full hand of " + Expansion.HandMax + " drops a new card");

        // The vendor sells its cards for vectors.
        Array.Clear(friend.Hand, 0, friend.Hand.Length);
        friend.Vectors = 100;
        var sold = g.VendorCards[1];
        typeof(Game).GetMethod("BuyRoomCard", flags).Invoke(g, new object[] { friend, 1 });
        Check(friend.Hand[0] == (byte)sold && friend.Vectors == 100 - Expansion.CardPrice(sold), "the vendor sells a " + Expansion.CardName(sold) + " for " + Expansion.CardPrice(sold) + " vectors");
        friend.Vectors = 1;
        typeof(Game).GetMethod("BuyRoomCard", flags).Invoke(g, new object[] { friend, 0 });
        Check(Game.HandCount(friend) == 1 && friend.Vectors == 1, "not without the vectors");

        // Off (the lobby's Free draw): no cards dealt, drawing works again.
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.RoomCards, 0f); gg.ExpandOpen = false; }
        foreach (var p in g.Players.Values) { Array.Clear(p.Hand, 0, p.Hand.Length); p.CardsDealt = false; }
        typeof(Game).GetMethod("OpenBuildRound", flags).Invoke(g, null);
        Check(g.Players.Values.All(p => Game.HandCount(p) == 0), "with cards off nobody is dealt cards");
    }

    // Bots play their cards: over a few rounds the map grows from cards only.
    static void RoomCardsBotTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1, StartWave = 6 }, port, true, "Bot", Role.Tank);
        var g = host.Game;
        host.StartMatch();
        var s = new[] { host };
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f); g.Diff.Set(DiffKnob.RoomCards, 1f); g.Expanded.Clear(); g.Nodes.Clear();
        var open = typeof(Game).GetMethod("OpenBuildRound", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        int rounds = 0;
        for (int r = 0; r < 3; r++)
        {
            g.ExpandOpen = false;
            open.Invoke(g, null);
            int n0 = g.Expanded.Count;
            Run(s, dt, 3f, () => g.Expanded.Count > n0, noPicks);
            if (g.Expanded.Count > n0) rounds++;
        }
        Check(rounds == 3 && g.Expanded.Count(q => !q.Floating && q.Owner == g.MyId) > 0, "a bot plays a card every round (" + rounds + " of 3, " + g.Expanded.Count + " boxes)");
    }
}

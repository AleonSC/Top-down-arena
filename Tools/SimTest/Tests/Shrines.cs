using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Health shrines and room chests (owner Nov 20, Docs/Nodes.md): the shrine is the only node a cycle places; once claimed each player can use it once for
// full health plus health past max (Player.OverHp); a placed piece or mystery room may hold a chest whose kind is rolled by rarity; vendor rooms never do.
static partial class Program
{
    static void ShrinesTest(float dt)
    {
        // The room chest roll: about RoomChestChance percent, the better kinds rarer (RoomChestWeights), seeded.
        var counts = new int[Expansion.RoomChestWeights.Length];
        int hits = 0, n = 20000;
        var rng = new DetRandom(5);
        for (int i = 0; i < n; i++)
        {
            var k = Expansion.RollRoomChest(rng);
            if (!k.HasValue) continue;
            hits++;
            counts[(int)k.Value]++;
        }
        float rate = hits * 100f / n;
        Check(Math.Abs(rate - Expansion.RoomChestChance) < 2f, "a new room holds a chest about " + Expansion.RoomChestChance + "% of the time (" + rate.ToString("0.0") + "%)");
        Check(counts[(int)ChestKind.Gold] > counts[(int)ChestKind.Piece] && counts[(int)ChestKind.Piece] > counts[(int)ChestKind.Upgrade] && counts[(int)ChestKind.Upgrade] > counts[(int)ChestKind.Relic]
              && counts.All(c => c > 0), "every chest kind turns up, the better ones rarer (" + string.Join(",", counts) + ")");
        var rolled = new MapNode { X = 5000, Y = 450, Kind = NodeKind.Chest, Rule = (byte)((int)ChestKind.Relic + 1) };
        Check(Expansion.NodeChestKind(7, rolled, true) == ChestKind.Relic && Expansion.NodeChestKind(7, new MapNode { X = 5000, Y = 450, Kind = NodeKind.Chest }, true) == Expansion.NodeChestKind(7, 5000, 450, true),
            "a room's chest keeps its rolled kind; a plain chest node still rolls by tier");

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
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);  // (the Grid core)
        foreach (var e in g.Enemies.Values) e.Hp = 0f;

        // Pieces: a shape piece (modifiers or not) holds a chest at a cell's middle when the roll says so; a vendor's stall never does.
        var me = g.Players[1];
        var play = typeof(Game).GetMethod("HostPlayPieceCard", flags);
        Func<PieceCard, PieceShape, List<ExpansionRoom>> place = (card, shape) =>
        {
            Array.Clear(me.Hand, 0, me.Hand.Length);
            me.Hand[0] = (int)card;
            var o = Expansion.PlacementsForCard(g.Expanded, g.Nodes, card)[0];
            int before = g.Expanded.Count;
            play.Invoke(g, new object[] { me.Id, 0, o.Orient, o.X, o.Y });
            return g.Expanded.Skip(before).ToList();
        };
        int keep = Expansion.RoomChestChance;
        Expansion.RoomChestChance = 100;
        int nodes0 = g.Nodes.Count;
        var made = place(Expansion.MakePieceCardMods(PieceShape.Domino, PieceMods.Floor.Set(0, (int)PathId.BurningGrounds)), PieceShape.Domino);
        var chestNode = g.Nodes.Skip(nodes0).FirstOrDefault();
        Check(made.Count > 0 && g.Nodes.Count == nodes0 + 1 && chestNode.Kind == NodeKind.Chest && chestNode.Rule > 0 && made.Any(r => Expansion.NodeEncloses(r, chestNode))
              && ((chestNode.X - Expansion.GridX0) % Expansion.GridCell) == Expansion.GridCell / 2,
            "a placed piece with a modifier holds a rolled chest in the middle of one of its cells");
        Check(g.Props.Values.Any(p => p.Kind == PropKind.Chest && p.Chest == g.NodeChestKind(chestNode)), "its chest comes out at once, of the rolled kind");
        int nodes1 = g.Nodes.Count;
        place(PieceCard.Shop, PieceShape.Mono);
        place(PieceCard.Medic, PieceShape.Mono);
        Check(g.Nodes.Count == nodes1, "a vendor's room (shop, Medic) never holds a chest");
        Expansion.RoomChestChance = 0;
        place(Expansion.MakePieceCard(PieceShape.Mono), PieceShape.Mono);
        Check(g.Nodes.Count == nodes1, "with the roll against it a piece holds none");
        var lootCard = Expansion.MakePieceCardMods(PieceShape.Mono, PieceMods.Loot.Set(0, (int)ChestKind.Piece + 1));
        Check(Expansion.PieceCardLoot(lootCard) && Expansion.CardName(lootCard) == "Piece Loot Single" && Expansion.CardPrice(lootCard) == 6 + 8, "a Loot card names its chest kind and costs 8 more");
        var lootMade = place(lootCard, PieceShape.Mono);
        var lootNode = g.Nodes.Skip(nodes1).FirstOrDefault();
        Check(lootMade.Count == 1 && g.Nodes.Count == nodes1 + 1 && lootNode.Kind == NodeKind.Chest && lootNode.Rule == (int)ChestKind.Piece + 1 && Expansion.NodeEncloses(lootMade[0], lootNode),
            "a Loot piece holds its chest even with the room roll at 0%");
        Expansion.RoomChestChance = 100;
        int nodes1b = g.Nodes.Count;
        place(Expansion.MakePieceCardMods(PieceShape.Mono, PieceMods.Loot.Set(0, (int)ChestKind.Gold + 1)), PieceShape.Mono);
        Check(g.Nodes.Count == nodes1b + 1 && g.Nodes.Last().Rule == (int)ChestKind.Gold + 1, "and only that one: a Loot piece skips the random room chest");
        nodes1 = g.Nodes.Count;
        Expansion.RoomChestChance = keep;
        Run(s, dt, 0.5f, () => cg.Nodes.Count == g.Nodes.Count, noPicks);
        Check(cg.Nodes.SequenceEqual(g.Nodes), "the client gets the room chests");

        // A new boss cycle places health shrines and its faction's relic nodes (Relics.cs).
        var placeFloating = typeof(Game).GetMethod("PlaceFloatingRooms", flags);
        typeof(Game).GetField("floatCycle", flags).SetValue(g, -1);
        int nodes2 = g.Nodes.Count;
        placeFloating.Invoke(g, null);
        var fresh = g.Nodes.Skip(nodes2).ToList();
        var freshRelics = fresh.Where(nd => nd.Kind == NodeKind.Relic).ToList();
        Check(fresh.Count >= 1 && fresh.All(nd => nd.Kind == NodeKind.Shrine || nd.Kind == NodeKind.Relic) && fresh.Any(nd => nd.Kind == NodeKind.Shrine), "a boss cycle places health shrines and relic nodes only (" + fresh.Count + ")");
        Check(freshRelics.Count >= 1 && freshRelics.Count <= 3 && freshRelics.All(nd => Relics.IsFaction(Relics.Held(nd)) && Relics.FactionOf(Relics.Held(nd)) == ((g.Wave - 1) / Expansion.Milestone) % Game.BossCycle.Length && freshRelics.Count == Relics.PerFaction[((g.Wave - 1) / Expansion.Milestone) % Game.BossCycle.Length]), "a cycle places its boss faction's relic nodes (" + freshRelics.Count + " wave " + g.Wave + " faction " + string.Join(",", freshRelics.Select(nd => Relics.FactionOf(Relics.Held(nd)))) + ")");

        // A shrine claimed in a room: once per player, full health plus half of max past it; damage takes that first; going down loses it.
        var room = g.Expanded.First(r => r.Piece != 0 || r.Owner != 0);
        var cellMid = Expansion.CellMiddles(new List<ExpansionRoom> { room })[0];
        g.Nodes.Add(new MapNode { X = (short)cellMid.X, Y = (short)cellMid.Y, Kind = NodeKind.Shrine });
        typeof(Game).GetField("expWorld", flags).SetValue(g, null);
        int si = g.Nodes.Count - 1;
        V2 spot = g.NodeSpot(si);
        var p1 = g.Players[1]; var p2 = g.Players[2];
        foreach (var p in new[] { p1, p2 }) { p.Pos = p.Disp = spot + new V2(600f, 0f); p.Hp = p.MaxHp * 0.4f; p.OverHp = 0f; p.ShrinesUsed = 0; p.InvulnT = 0f; p.Wound = p.MaxHp * 0.2f; }
        var tick = typeof(Game).GetMethod("TickShrines", flags);
        tick.Invoke(g, null);
        Check(p1.OverHp == 0f && p1.ShrinesUsed == 0, "nobody near: the shrine waits");
        p1.Pos = p1.Disp = spot;
        tick.Invoke(g, null);
        Check(p1.Wound == 0f && Math.Abs(p1.Hp - p1.MaxHp) < 0.01f && Math.Abs(p1.OverHp - p1.MaxHp * Shrines.OverFrac) < 0.01f && g.ShrineUsed(p1, si) && !g.ShrineUsed(p2, si),
            "walking up clears your wound, fills your health and adds half your max past it (" + p1.Hp + "+" + p1.OverHp + ")");
        p1.Hp = p1.MaxHp * 0.5f;
        p1.OverHp = 10f;
        tick.Invoke(g, null);
        Check(p1.OverHp == 10f && Math.Abs(p1.Hp - p1.MaxHp * 0.5f) < 0.01f, "once only: standing on it again does nothing");
        p2.Pos = p2.Disp = spot;
        tick.Invoke(g, null);
        Check(g.ShrineUsed(p2, si) && p2.OverHp > 0f, "a teammate gets their own use");
        Run(s, dt, 0.5f, () => cg.Players[2].OverHp > 0f && cg.Players[2].ShrinesUsed != 0, noPicks);
        Check(cg.Players[2].OverHp > 0f && cg.ShrineUsed(cg.Players[2], si), "the client sees the health past max and the use");
        p1.Pos = p1.Disp = spot + new V2(600f, 0f);
        p1.OverHp = 30f; p1.ShieldHp = 0f; p1.InvulnT = 0f;
        float hpBefore = p1.Hp;
        typeof(Game).GetMethod("Damage", flags).Invoke(g, new object[] { p1, 20f, "test" });
        Check(p1.Hp == hpBefore && p1.OverHp < 30f, "damage takes the health past max first (" + p1.OverHp + " left, health " + p1.Hp + ")");
        p2.OverHp = 40f; p2.Hp = 0f;
        typeof(Game).GetMethod("DownIfDead", flags).Invoke(g, new object[] { p2, "test" });
        Check(p2.Downed && p2.OverHp == 0f, "going down loses it");

        host.Leave(""); client.Leave("");

        // Saved and loaded with the seat.
        var saved = new RunSave { Wave = 6 };
        saved.Seats.Add(new SeatSave { Name = "Pilgrim", Role = Role.Tank, Kit = Kit.Soldier, ShrinesUsed = 5, OverHp = 12f, Wound = 7f });
        var back = RunSave.Parse(saved.ToText());
        var seat = back != null ? back.Seats.FirstOrDefault(x => x.Name == "Pilgrim") : null;
        Check(seat != null && seat.ShrinesUsed == 5 && Math.Abs(seat.OverHp - 12f) < 0.01f && Math.Abs(seat.Wound - 7f) < 0.01f, "a save keeps the shrines used, the health past max and the wound");
    }
}

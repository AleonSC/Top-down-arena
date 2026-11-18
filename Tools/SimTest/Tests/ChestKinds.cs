using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Chest kinds (owner Nov 18, ChestKind in Props.cs, look Game/ChestLooks.cs): a wave's chest is Upgrade; an Expanding chest
// node rolls Gold (a burst of coins), Grid piece (a card each), Buildables (a Black Market build each) or Upgrade (a card each).
static partial class Program
{
    static void ChestKindsTest(float dt)
    {
        // The node roll by tier (owner Nov 18): inner Gold, middle Grid piece or Buildables (Buildables with room cards off), outer Upgrade; seeded, the same every time.
        var seen = new HashSet<ChestKind>(); int unstable = 0, cardsOff = 0, wrongTier = 0;
        for (int i = 0; i < 400; i++)
        {
            int x = (int)Game.Width + 50 + i * 10, y = 450, tier = Expansion.NodeTier(x, y);
            var k = Expansion.NodeChestKind(7, x, y, true);
            seen.Add(k);
            if (k != Expansion.NodeChestKind(7, x, y, true)) unstable++;
            if (Expansion.NodeChestKind(7, x, y, false) == ChestKind.Piece) cardsOff++;
            if (tier == 0 ? k != ChestKind.Gold : tier == 1 ? k != ChestKind.Piece && k != ChestKind.Buildable : k != ChestKind.Upgrade) wrongTier++;
        }
        Check(seen.SetEquals(new[] { ChestKind.Gold, ChestKind.Piece, ChestKind.Buildable, ChestKind.Upgrade }) && unstable == 0 && wrongTier == 0,
            "a chest node's kind follows its tier: Gold inner, Grid piece or Buildables middle, Upgrade outer, the same each time (" + string.Join(",", seen) + ")");
        Check(Expansion.NodeTier((int)Game.Width + Expansion.TierMid - 10, 450) == 0 && Expansion.NodeTier((int)Game.Width + Expansion.TierMid, 450) == 1
            && Expansion.NodeTier(800, -Expansion.TierOuter) == 2 && Expansion.NodeTier(800, 450) == 0, "tiers by distance from the hub's wall (15 and 30 tiles)");
        Check(cardsOff == 0, "no Grid piece chest with cards off");
        int tieredOk = 0;
        for (int i = 0; i < 300; i++)
        {
            var tn = Expansion.Tiered(7, new MapNode { X = (short)(Game.Width + 50 + i * 15), Y = 450, Kind = NodeKind.Relic });
            int tier = Expansion.NodeTier(tn);
            if (tier == 0 ? Expansion.IsChest(tn.Kind) : tier == 1 ? tn.Kind == NodeKind.Chest : tn.Kind == NodeKind.Chest || tn.Kind == NodeKind.Relic) tieredOk++;
        }
        Check(tieredOk == 300 && Expansion.Tiered(7, new MapNode { X = 3000, Y = 450, Kind = NodeKind.Vein }).Kind == NodeKind.Vein,
            "a prize node takes its tier's kind (inner gold or vector chest, middle chest, outer chest or relic); a vein stays a vein");

        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); }
        var open = typeof(Game).GetMethod("OpenChest", flags);
        V2 at = new V2(800f, 300f);

        // Each kind reaches the client, and opening it pays its own reward only.
        foreach (var kind in new[] { ChestKind.Upgrade, ChestKind.Gold, ChestKind.Buildable })
        {
            g.Props.Clear(); g.Loots.Clear(); g.Coins.Clear();
            var c = g.AddProp(PropKind.Chest, at); c.Chest = kind;
            Run(s, dt, 1f, () => cg.Props.ContainsKey(c.Id), noPicks);
            Check(cg.Props.ContainsKey(c.Id) && cg.Props[c.Id].Chest == kind, kind + " chest: the client sees its kind");
            int stash0 = g.Players.Values.Sum(p => p.Stash.Sum(b => b));
            int gold = g.Players.Count * (Game.ChestGold + Game.ChestGoldPerWave * g.Wave);
            open.Invoke(g, new object[] { c });
            int coins = g.Coins.Values.Count(q => !q.Vec), coinGold = g.Coins.Values.Where(q => !q.Vec).Sum(q => q.Value);
            int stash = g.Players.Values.Sum(p => p.Stash.Sum(b => b)) - stash0;
            switch (kind)
            {
                case ChestKind.Upgrade:
                    Check(g.Players.Values.All(p => g.Loots.Values.Any(l => l.Owner == p.Id)) && coins == 0 && stash == 0, "Upgrade: every player gets their own card, nothing else");
                    break;
                case ChestKind.Gold:
                    Check(coins == Game.ChestGoldCoins && coinGold == gold && g.Loots.Count == 0, "Gold: " + Game.ChestGoldCoins + " coins worth " + gold + " burst out (" + coins + " worth " + coinGold + "), no cards");
                    break;
                case ChestKind.Buildable:
                    Check(stash == g.Players.Count && g.Loots.Count == 0 && coins == 0, "Buildables: one Black Market build in each player's stash (" + stash + ")");
                    break;
            }
        }
        g.Coins.Clear();

        // A full stash: Buildables picks a kind there's room for, and gives nothing when all are full.
        var full = g.Players.Values.First();
        for (int k = 0; k < Market.Count; k++) full.Stash[k] = (byte)(k == 2 ? Market.StashMax - 1 : Market.StashMax);
        var give = typeof(Game).GetMethod("GiveChestBuild", flags);
        give.Invoke(g, new object[] { full });
        Check(full.Stash.All(b => b == Market.StashMax), "Buildables fills the one kind with room");
        give.Invoke(g, new object[] { full });
        Check(full.Stash.All(b => b == Market.StashMax), "and gives nothing past the cap");
        for (int k = 0; k < Market.Count; k++) full.Stash[k] = 0;

        // A Grid piece chest node: a room round it, the chest comes out as Grid piece, opening it deals a card each and no upgrade card.
        UseMap(new[] { g }, GridMap(GridPiece.TetI));  // (a line of four cells east of the core: the chest out at its far end, a middle-tier spot)
        var far = g.Expanded.Skip(4).OrderBy(r => -r.X1).First();
        var node = new MapNode { X = (short)(far.X1 - Expansion.GridCell / 2), Y = (short)((far.Y + far.Y1) / 2), Kind = NodeKind.Chest };
        var seedField = typeof(Game).GetField("expandSeed", flags);
        int sd = 1;
        while (sd < 200 && Expansion.NodeChestKind(sd, node.X, node.Y, true) != ChestKind.Piece) sd++;
        seedField.SetValue(g, sd);
        g.Nodes.Add(node);
        g.Props.Clear(); g.Loots.Clear();
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        var tickNodes = typeof(Game).GetMethod("TickNodes", flags);
        tickNodes.Invoke(g, null);
        var chest = g.Props.Values.FirstOrDefault(q => q.Kind == PropKind.Chest);
        Check(chest != null && chest.Chest == ChestKind.Piece && g.NodeChestKind(node) == ChestKind.Piece, "the node's chest comes out as its rolled kind (Grid piece)");
        if (chest != null)
        {
            open.Invoke(g, new object[] { chest });
            Check(g.Nodes[0].Spent && g.Players.Values.All(p => Game.HandCount(p) == 1 && Expansion.IsGridCard((RoomCard)p.Hand[0])) && g.Loots.Count == 0, "Grid piece: every player gets a grid piece card, no upgrade card");
        }
    }
}

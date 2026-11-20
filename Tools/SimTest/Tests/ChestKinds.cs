using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Chest kinds (owner Nov 18, ChestKind in Props.cs, look Game/ChestLooks.cs): a wave's chest is Upgrade; an Expanding chest
// node rolls Gold (a burst of coins), Piece chest (a card each), Buildables (a Black Market build each) or Upgrade (a card each).
static partial class Program
{
    static void ChestKindsTest(float dt)
    {
        // The node roll by tier (owner Nov 18): inner Gold, middle Piece chest or Buildables (Buildables with room cards off), outer Upgrade; seeded, the same every time.
        var seen = new HashSet<ChestKind>(); int unstable = 0, cardsOff = 0, wrongTier = 0;
        for (int i = 0; i < 400; i++)
        {
            int x = (int)Game.Width + 50 + i * 10, y = 450, tier = Expansion.NodeTier(x, y);
            var k = Expansion.NodeChestKind(7, x, y, true);
            seen.Add(k);
            if (k != Expansion.NodeChestKind(7, x, y, true)) unstable++;
            if (Expansion.NodeChestKind(7, x, y, false) == ChestKind.Piece) cardsOff++;
            if (tier == 0 ? k != ChestKind.Gold && k != ChestKind.Vector : tier == 1 ? k != ChestKind.Piece && k != ChestKind.Buildable : k != ChestKind.Upgrade) wrongTier++;
        }
        Check(seen.SetEquals(new[] { ChestKind.Gold, ChestKind.Vector, ChestKind.Piece, ChestKind.Buildable, ChestKind.Upgrade }) && unstable == 0 && wrongTier == 0,
            "a chest node's kind follows its tier: Gold or Vector inner, Piece chest or Buildables middle, Upgrade outer, the same each time (" + string.Join(",", seen) + ")");
        Check(Expansion.NodeTier((int)Game.Width + Expansion.TierMid - 10, 450) == 0 && Expansion.NodeTier((int)Game.Width + Expansion.TierMid, 450) == 1
            && Expansion.NodeTier(800, -Expansion.TierOuter) == 2 && Expansion.NodeTier(800, 450) == 0, "tiers by distance from the hub's wall (15 and 30 tiles)");
        Check(Expansion.NodeTier((int)Game.Width + Expansion.TierMid, -Expansion.TierMid) == 1 && Expansion.NodeTier(-Expansion.TierOuter, -Expansion.TierMid) == 2
            && Expansion.NodeTier((int)Game.Width + Expansion.TierMid - 1, -Expansion.TierMid + 1) == 0, "tier zones are square: a corner counts by its farther axis");
        Check(cardsOff == 0, "no Piece chest with cards off");
        int tieredOk = 0;
        for (int i = 0; i < 300; i++)
        {
            var tn = Expansion.Tiered(7, new MapNode { X = (short)(Game.Width + 50 + i * 15), Y = 450, Kind = NodeKind.Relic });
            int tier = Expansion.NodeTier(tn);
            if (tier == 0 ? Expansion.IsChest(tn.Kind) : tier == 1 ? tn.Kind == NodeKind.Chest : tn.Kind == NodeKind.Chest || tn.Kind == NodeKind.Relic) tieredOk++;
        }
        Check(tieredOk == 300 && Expansion.Tiered(7, new MapNode { X = 3000, Y = 450, Kind = NodeKind.Rule }).Kind == NodeKind.Rule,
            "a prize node takes its tier's kind (inner gold or vector chest, middle chest, outer chest or relic); a rule node stays one");

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
        var hostLoot = typeof(Game).GetMethod("HostLoot", flags);
        V2 at = new V2(800f, 300f);

        // Each kind reaches the client, and opening it pays its own reward only.
        foreach (var kind in new[] { ChestKind.Upgrade, ChestKind.Gold, ChestKind.Buildable })
        {
            g.Props.Clear(); g.Loots.Clear(); g.Coins.Clear();
            var c = g.AddProp(PropKind.Chest, at); c.Chest = kind;
            Run(s, dt, 1f, () => cg.Props.ContainsKey(c.Id), noPicks);
            Check(cg.Props.ContainsKey(c.Id) && cg.Props[c.Id].Chest == kind, kind + " chest: the client sees its kind");
            int stash0 = g.Players.Values.Sum(p => p.Stash.Sum(b => b));
            int gold = g.Players.Count * (Game.ChestGold + Game.ChestGoldPerWave * g.Wave);  // (each player's own burst: Docs/RoundFlow.md step 3)
            open.Invoke(g, new object[] { c, false });  // (not remote: RemoteChests.cs)
            int coins = g.Coins.Values.Count(q => !q.Vec), coinGold = g.Coins.Values.Where(q => !q.Vec).Sum(q => q.Value);
            int stash = g.Players.Values.Sum(p => p.Stash.Sum(b => b)) - stash0;
            switch (kind)
            {
                case ChestKind.Upgrade:
                    Check(g.Players.Values.All(p => g.Loots.Values.Any(l => l.Owner == p.Id && l.Kind == LootKind.Upgrade)) && coins == 0 && stash == 0, "Upgrade: every player gets their own card, nothing else");
                    break;
                case ChestKind.Gold:
                    Check(coins == Game.ChestGoldCoins * g.Players.Count && coinGold == gold && g.Loots.Count == 0 && g.Players.Values.All(p => g.Coins.Values.Count(q => q.Owner == p.Id) == Game.ChestGoldCoins),
                        "Gold: " + Game.ChestGoldCoins + " coins for each player, worth " + gold + " in all, burst out (" + coins + " worth " + coinGold + "), no cards");
                    break;
                case ChestKind.Buildable:  // a build card each, to take or throw away (nothing in the stash yet)
                    Check(stash == 0 && coins == 0 && g.Players.Values.All(p => g.Loots.Values.Count(l => l.Owner == p.Id && l.Kind == LootKind.Build && l.Item >= 0 && l.Item < Market.Count) == 1),
                        "Buildables: every player gets their own build card to take or discard, nothing in the stash yet (" + g.Loots.Count + " cards)");
                    Run(s, dt, 0.4f, null, noPicks);
                    Check(cg.Loots.Values.Any(l => l.Owner == 2 && l.Kind == LootKind.Build), "the client gets its build card with its kind");
                    var bl = g.Loots.Values.First(l => l.Owner == 1);
                    int k0 = g.Players[1].Stash[bl.Item];
                    g.Players[1].Pos = bl.Pos;  // (taking needs you standing on it)
                    hostLoot.Invoke(g, new object[] { 1, bl.Id, true });
                    Check(g.Players[1].Stash[bl.Item] == k0 + 1 && !g.Loots.ContainsKey(bl.Id), "taking it puts the build in the stash");
                    var bl2 = g.Loots.Values.First(l => l.Owner == 2);
                    int s2 = g.Players[2].Stash.Sum(b => b);
                    g.Players[2].Pos = bl2.Pos;
                    hostLoot.Invoke(g, new object[] { 2, bl2.Id, false });
                    Check(g.Players[2].Stash.Sum(b => b) == s2 && !g.Loots.ContainsKey(bl2.Id), "discarding it leaves the stash alone");
                    break;
            }
        }
        g.Coins.Clear();

        // A full stash: Buildables offers a kind there's room for, and nothing when all are full.
        var full = g.Players.Values.First();
        for (int k = 0; k < Market.Count; k++) full.Stash[k] = (byte)(k == 2 ? Market.StashMax - 1 : Market.StashMax);
        var roll = typeof(Game).GetMethod("RollChestBuild", flags);
        Check((int)roll.Invoke(g, new object[] { full }) == 2, "Buildables offers the one kind with room");
        full.Stash[2] = Market.StashMax;
        Check((int)roll.Invoke(g, new object[] { full }) == -1, "and nothing past the cap");
        for (int k = 0; k < Market.Count; k++) full.Stash[k] = 0;

        // A Piece chest node: a room round it, the chest comes out as Piece chest, opening it deals a card each and no upgrade card.
        UseMap(new[] { g }, GridMap(PieceShape.TetI));  // (a line of four cells east of the core: the chest out at its far end, a middle-tier spot)
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
        Check(chest != null && chest.Chest == ChestKind.Piece && g.NodeChestKind(node) == ChestKind.Piece, "the node's chest comes out as its rolled kind (Piece chest)");
        if (chest != null)
        {
            open.Invoke(g, new object[] { chest, false });
            Check(g.Nodes[0].Spent && g.Players.Values.All(p => Game.HandCount(p) == 0 && g.Loots.Values.Count(l => l.Owner == p.Id && l.Kind == LootKind.Card && Expansion.IsGridPieceCard((PieceCard)l.Item)) == 1),
                "Piece chest: every player gets a piece to take or discard (not in the hand yet), no upgrade card");
            var cl = g.Loots.Values.First(l => l.Owner == 1);
            g.Players[1].Pos = cl.Pos;
            hostLoot.Invoke(g, new object[] { 1, cl.Id, true });
            Check(Game.HandCount(g.Players[1]) == 1 && g.Players[1].Hand[0] == (int)cl.Item, "taking it puts the card in the hand");
        }
    }
}

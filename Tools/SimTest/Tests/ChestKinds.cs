using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Chest kinds (owner Nov 18, ChestKind in Props.cs, look Game/ChestLooks.cs): a wave's chest is Upgrade; an Expanding chest
// node rolls Gold (a burst of coins), Room card (a card each), Buildables (a Black Market build each) or Upgrade (a card each).
static partial class Program
{
    static void ChestKindsTest(float dt)
    {
        // The node roll: seeded, the same every time, all four kinds, no Room card with room cards off.
        var seen = new HashSet<ChestKind>(); int unstable = 0, cardsOff = 0;
        for (int x = 0; x < 20; x++)
            for (int y = 0; y < 20; y++)
            {
                var k = Expansion.NodeChestKind(7, x * 100 + 50, y * 100 + 50, true);
                seen.Add(k);
                if (k != Expansion.NodeChestKind(7, x * 100 + 50, y * 100 + 50, true)) unstable++;
                if (Expansion.NodeChestKind(7, x * 100 + 50, y * 100 + 50, false) == ChestKind.RoomCard) cardsOff++;
            }
        Check(seen.SetEquals(new[] { ChestKind.Gold, ChestKind.RoomCard, ChestKind.Buildable, ChestKind.Upgrade }) && unstable == 0,
            "a chest node rolls Gold, Room card, Buildables or Upgrade, the same each time (" + string.Join(",", seen) + ")");
        Check(cardsOff == 0, "no Room card chest with room cards off");

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

        // A Room card chest node: a room round it, the chest comes out as Room card, opening it deals a card each and no upgrade card.
        string why;
        var room = Expansion.Custom(g.Expanded, (int)Game.Width, 200, 1000, 500, out why);
        Check(room.HasValue, "a room east of the start room (" + why + ")");
        g.Expanded.Add(room.Value);
        typeof(Game).GetField("expWorld", flags).SetValue(g, null);
        var node = new MapNode { X = (short)(Game.Width + 550), Y = 450, Kind = NodeKind.Chest };
        var seedField = typeof(Game).GetField("expandSeed", flags);
        int sd = 1;
        while (sd < 200 && Expansion.NodeChestKind(sd, node.X, node.Y, true) != ChestKind.RoomCard) sd++;
        seedField.SetValue(g, sd);
        g.Nodes.Add(node);
        g.Props.Clear(); g.Loots.Clear();
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        var tickNodes = typeof(Game).GetMethod("TickNodes", flags);
        tickNodes.Invoke(g, null);
        var chest = g.Props.Values.FirstOrDefault(q => q.Kind == PropKind.Chest);
        Check(chest != null && chest.Chest == ChestKind.RoomCard && g.NodeChestKind(node) == ChestKind.RoomCard, "the node's chest comes out as its rolled kind (Room card)");
        if (chest != null)
        {
            open.Invoke(g, new object[] { chest });
            Check(g.Nodes[0].Spent && g.Players.Values.All(p => Game.HandCount(p) == 1) && g.Loots.Count == 0, "Room card: every player gets a room card, no upgrade card");
        }
    }
}

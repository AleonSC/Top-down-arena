using System;
using System.Linq;
using Arena;

static partial class Program
{
    // The Arms Dealer (Docs/ArmsDealer.md step 2): its room sells every weapon mod for gold while the room shops are open, priced by the
    // stacks you hold, only to someone standing in it; a client buys through the host.
    static void ArmsDealerTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Game.ModIds.Length >= 5 && Game.ModIds.All(id => Upgrades.All[id].Rarity == Rarity.Mod)
              && Game.ModIds.Length == Upgrades.All.Count(u => u.Rarity == Rarity.Mod), "the Arms Dealer stocks every weapon mod (" + Game.ModIds.Length + ")");
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Diff.Set(DiffKnob.RoomCards, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true; }

        // An Arms Dealer and a shop, from their cards.
        var me = g.Players[1];
        Func<RoomCard, V2> play = card =>
        {
            Array.Clear(me.Hand, 0, me.Hand.Length);
            me.Hand[0] = (byte)card;
            var o = Expansion.CardOptions(g.Expanded, card)[0];
            typeof(Game).GetMethod("HostPlayCard", flags).Invoke(g, new object[] { me.Id, 0, o.X, o.Y, (int)o.Turn, o.Mirror });
            var r = g.Expanded[g.Expanded.Count - 1];
            return new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f);
        };
        V2 armsAt = play(RoomCard.Arms), shopAt = play(RoomCard.Shop), hubAt = g.CurrentWorld.Areas[0].Mid;
        Check(g.HasArmsRoom && g.ServiceRooms, "an Arms Dealer room makes the room shops open");
        g.ShopOpen = true;
        int mod = Game.ModIds[0];
        me.Gold = 2000;

        me.Pos = me.Disp = hubAt;
        g.Shop(ShopAction.BuyMod, mod);
        Check(me.Gold == 2000 && Upgrades.Count(me.Taken, mod) == 0, "in the hub the Arms Dealer sells nothing");
        me.Pos = me.Disp = shopAt;
        g.Shop(ShopAction.BuyMod, mod);
        Check(me.Gold == 2000 && Upgrades.Count(me.Taken, mod) == 0, "nor in the shop room");

        me.Pos = me.Disp = armsAt;
        int gold = me.Gold;
        bool rising = true;
        for (int k = 0; k < Upgrades.All[mod].MaxStacks; k++)
        {
            int price = Game.ModPrice(me, mod);
            rising &= price == Game.ModPrices[Math.Min(k, Game.ModPrices.Length - 1)];
            g.Shop(ShopAction.BuyMod, mod);
            rising &= me.Gold == gold - price && Upgrades.Count(me.Taken, mod) == k + 1;
            gold = me.Gold;
        }
        Check(rising, "in its room: " + Upgrades.All[mod].Name + " for " + string.Join(", ", Game.ModPrices) + " gold, a stack each");
        g.Shop(ShopAction.BuyMod, mod);
        Check(me.Gold == gold && Upgrades.Count(me.Taken, mod) == Upgrades.All[mod].MaxStacks, "a maxed mod isn't sold again");
        int notMod = Enumerable.Range(0, Upgrades.All.Length).First(i => Upgrades.All[i].Rarity == Rarity.Common);
        g.Shop(ShopAction.BuyMod, notMod);
        Check(me.Gold == gold && !me.Taken.Contains(notMod), "it only sells weapon mods");
        me.Gold = 100;
        g.Shop(ShopAction.BuyMod, Game.ModIds[1]);
        Check(me.Gold == 100 && Upgrades.Count(me.Taken, Game.ModIds[1]) == 0, "not without the gold");
        me.Gold = 500; me.Hp = me.MaxHp * 0.5f;
        g.Shop(ShopAction.Heal);
        Check(me.Gold == 500 - Game.HealPrice && me.Hp >= me.MaxHp, "its services work too: it heals");

        // The friend buys through the host, standing in the room.
        var friend = g.Players[2];
        friend.Gold = 300; friend.Pos = friend.Disp = armsAt;
        int fm = Game.ModIds[2];
        PollBoth(host, client);
        cg.Shop(ShopAction.BuyMod, fm);
        Run(s, dt, 1f, () => Upgrades.Count(friend.Taken, fm) == 1, noPicks);
        Run(s, dt, 0.5f, null, noPicks);
        Check(Upgrades.Count(friend.Taken, fm) == 1 && friend.Gold == 300 - Game.ModPrices[0] && cg.Players[2].Gold == friend.Gold, "the friend buys a mod too, and sees their gold drop");
    }
}

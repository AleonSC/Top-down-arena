using System;
using System.Linq;
using Arena;

static partial class Program
{
    // The Arms Dealer (Docs/ArmsDealer.md step 2): its room sells every weapon mod for gold while the room shops are open, priced by the
    // stacks you hold, only to someone standing in it; a client buys through the host. A boss cycle without one deals an Arms Dealer card.
    // The Medic (Medic.cs): the only room shop with heals and revive tokens.
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
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true; }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);  // (the Grid core)

        // The guarantee: a boss cycle with no Arms Dealer on the map and no Arms Dealer card in a hand deals one, once a cycle.
        var guarantee = typeof(Game).GetMethod("GuaranteeArmsCard", flags);
        Func<int> armsCards = () => g.Players.Values.Sum(p => p.Hand.Count(c => c == (byte)RoomCard.Arms));
        Action clearHands = () => { foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length); };
        clearHands();
        g.Wave = 6;
        guarantee.Invoke(g, null);
        Check(armsCards() == 1, "with no Arms Dealer anywhere, someone is dealt an Arms Dealer card");
        clearHands();
        guarantee.Invoke(g, null);
        Check(armsCards() == 0, "only once a boss cycle");
        g.Wave = 10;
        g.Players[2].Hand[0] = (byte)RoomCard.Arms;
        guarantee.Invoke(g, null);
        Check(armsCards() == 1, "not while someone holds one");
        clearHands();
        g.Wave = 15;
        foreach (var p in g.Players.Values) for (int i = 0; i < p.Hand.Length; i++) p.Hand[i] = (byte)RoomCard.Shop;
        guarantee.Invoke(g, null);
        Check(armsCards() == 0, "every hand full: nobody gets it yet");
        g.Players[2].Hand[3] = (byte)RoomCard.None;
        guarantee.Invoke(g, null);
        Check(g.Players[2].Hand.Contains((byte)RoomCard.Arms), "the next round it goes to the hand with room");
        clearHands();
        g.Wave = 6;

        // An Arms Dealer and a shop, from their cards.
        var me = g.Players[1];
        Func<RoomCard, V2> play = card =>
        {
            Array.Clear(me.Hand, 0, me.Hand.Length);
            me.Hand[0] = (byte)card;
            var o = Expansion.GridPieceOptions(g.Expanded, GridPiece.Mono)[0];
            typeof(Game).GetMethod("HostGridCard", flags).Invoke(g, new object[] { me.Id, 0, o.Orient, o.X, o.Y });
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
        g.Shop(ShopAction.Revive);
        Check(me.Gold == 500 && me.Hp < me.MaxHp * 0.6f && !me.ReviveToken, "it sells no heals or revives (the Medic does)");

        // The friend buys through the host, standing in the room.
        var friend = g.Players[2];
        friend.Gold = 300; friend.Pos = friend.Disp = armsAt;
        int fm = Game.ModIds[2];
        PollBoth(host, client);
        cg.Shop(ShopAction.BuyMod, fm);
        Run(s, dt, 1f, () => Upgrades.Count(friend.Taken, fm) == 1, noPicks);
        Run(s, dt, 0.5f, null, noPicks);
        Check(Upgrades.Count(friend.Taken, fm) == 1 && friend.Gold == 300 - Game.ModPrices[0] && cg.Players[2].Gold == friend.Gold, "the friend buys a mod too, and sees their gold drop");

        // With an Arms Dealer on the map the guarantee deals nothing.
        g.Wave = 20;
        guarantee.Invoke(g, null);
        Check(armsCards() == 0, "with an Arms Dealer on the map nobody is dealt another");

        // The Medic (Medic.cs): heals and revive tokens, only in its room, and nothing else.
        V2 medicAt = play(RoomCard.Medic);
        Check(g.HasMedicRoom && Expansion.CardRoomKind(RoomCard.Medic) == RoomKind.Medic, "a Medic card builds a Medic");
        me.Pos = me.Disp = medicAt;
        me.Gold = 500; me.Hp = me.MaxHp * 0.5f; me.ReviveToken = false;
        int taken = me.Taken.Count;
        g.Shop(ShopAction.Heal);
        Check(me.Gold == 500 - Game.HealPrice && me.Hp >= me.MaxHp, "the Medic heals you to full for " + Game.HealPrice);
        g.Shop(ShopAction.Revive);
        Check(me.ReviveToken && me.Gold == 500 - Game.HealPrice - Game.RevivePrice, "and sells a revive token for " + Game.RevivePrice);
        int g3 = me.Gold;
        g.Shop(ShopAction.Sell, me.Taken[0]);
        g.Shop(ShopAction.BuyMod, Game.ModIds[3]);
        g.Shop(ShopAction.BuyBuild, 0);
        Check(me.Gold == g3 && me.Taken.Count == taken && me.Stash[0] == 0, "it sells nothing else and buys nothing back");
        friend.Gold = 200; friend.Hp = friend.MaxHp * 0.5f; friend.Pos = friend.Disp = medicAt;
        PollBoth(host, client);
        cg.Shop(ShopAction.Heal);
        Run(s, dt, 1f, () => friend.Hp >= friend.MaxHp, noPicks);
        Check(cg.HasMedicRoom && friend.Hp >= friend.MaxHp && friend.Gold == 200 - Game.HealPrice, "the friend heals there through the host");
    }
}

static partial class Program
{
    // The boss card pick (Docs/ArmsDealer.md step 3): with room cards on a beaten boss gives a pick of 5 cards and kits (not a weapon mod), no
    // free cards or kit; it rerolls, a card goes to the hand and a kit to the team; what can't be taken is refused; with cards off, mods as before.
    static void BossCardsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        // The offer on its own: 5 different codes, all valid, every group turning up over many rolls.
        var seen = new System.Collections.Generic.HashSet<int>();
        bool fine = true;
        for (int i = 0; i < 400; i++)
        {
            var of = BossCards.Offer(new DetRandom(i * 977 + 5), new System.Collections.Generic.List<ExpansionRoom>(), new System.Collections.Generic.List<RoomToken>(), 12);
            fine &= of.Length == BossCards.OfferSize && of.Distinct().Count() == of.Length
                && of.All(c => BossCards.IsKit(c) ? Array.IndexOf(Expansion.Floors, BossCards.FloorOf(c)) >= 0 : BossCards.CardOf(c) != RoomCard.None);
            foreach (int c in of) seen.Add(c);
        }
        Check(fine, "an offer is " + BossCards.OfferSize + " different cards or kits");
        Check(seen.Any(c => !BossCards.IsKit(c) && Expansion.IsPieceCard(BossCards.CardOf(c))) && Expansion.ServiceCards.All(c => seen.Contains((int)c)) && seen.Contains((int)RoomCard.BuilderShop)
              && Expansion.Floors.All(f => seen.Contains(BossCards.KitCode(f))), "pieces, every service card, the Builder Shop and every floor kit turn up");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); }
        noPicks();
        foreach (var p in g.Players.Values) Array.Clear(p.Hand, 0, p.Hand.Length);
        g.RoomTokens.Clear();

        // The boss falls: a legendary, then the card pick (no weapon mod).
        var boss = new Enemy { Id = 9901, Type = EnemyType.Warden, Def = Defs.Get(EnemyType.Warden), Hp = 0f };
        typeof(Game).GetField("bossRewarded", flags).SetValue(g, false);
        typeof(Game).GetMethod("OnEnemyKilled", flags).Invoke(g, new object[] { boss });
        Check(g.BossCardPicks && g.Players.Values.All(p => p.PendingCards == 1 && p.PendingMods == 0 && p.LegendaryQueue.Count == 1), "with room cards a boss gives a card pick, not a weapon mod");
        var me = g.Players[1];
        var nextOffer = typeof(Game).GetMethod("NextOffer", flags);
        me.LegendaryQueue.Clear();
        g.State = MatchState.Intermission;
        nextOffer.Invoke(g, new object[] { me });
        Check(me.OfferCards && me.Offer != null && me.Offer.Length == BossCards.OfferSize, "after the legendary comes the card pick (" + string.Join(", ", me.Offer.Select(BossCards.Name)) + ")");

        // No free drops: the wave's boss clear gives no shape card and no Floor kit.
        g.Wave = 10;
        typeof(Game).GetMethod("DropWaveCards", flags).Invoke(g, new object[] { true });
        typeof(Game).GetMethod("GrantVectorsForWave", flags).Invoke(g, null);
        Check(Game.HandCount(me) == 0 && g.RoomTokens.Count == 0, "a boss gives no free card or kit any more");

        // Reroll: a new set, one reroll spent.
        var before = (int[])me.Offer.Clone();
        me.Rerolls = 2;
        g.HostReroll(me.Id);
        Check(me.Rerolls == 1 && me.OfferCards && !me.Offer.SequenceEqual(before), "the card pick can be rerolled");

        // Take a card: it goes to the hand. Then a kit: it goes to the team.
        me.Offer = new[] { (int)RoomCard.Ring, BossCards.KitCode(PathId.FrozenLake), (int)RoomCard.Arms, (int)RoomCard.Box, (int)RoomCard.MiniBoss };
        me.PendingCards = 2;
        g.PickUpgrade(2);
        Check(me.Hand[0] == (byte)RoomCard.Arms && me.PendingCards == 1 && me.OfferCards, "a picked card goes to your hand, and the next pick comes");
        me.Offer = new[] { (int)RoomCard.Ring, BossCards.KitCode(PathId.FrozenLake), (int)RoomCard.Arms, (int)RoomCard.Box, (int)RoomCard.MiniBoss };
        g.PickUpgrade(1);
        Check(g.RoomTokens.Count == 1 && g.RoomTokens[0].Kind == RoomKind.Floor && g.RoomTokens[0].Floor == PathId.FrozenLake && me.PendingCards == 0 && me.Offer == null,
            "a picked kit goes to the team's kits");

        // A full hand: cards are refused, a kit still works; full kits too: the pick is skipped.
        for (int i = 0; i < me.Hand.Length; i++) me.Hand[i] = (byte)RoomCard.Box;
        me.PendingCards = 1;
        me.Offer = new[] { (int)Expansion.PieceCard(GridPiece.Mono), BossCards.KitCode(PathId.BloodMoon), (int)RoomCard.Arms, (int)RoomCard.Shop, (int)RoomCard.BuilderShop };
        me.OfferCards = true;  // (set by hand: a rolled offer may hold no kit, and then a full hand skips the pick)
        g.PickUpgrade(0);
        Check(me.PendingCards == 1 && me.Hand.All(h => h == (byte)RoomCard.Box), "with a full hand a card can't be taken");
        while (g.RoomTokens.Count < Expansion.MaxTokens) g.RoomTokens.Add(new RoomToken { Kind = RoomKind.Floor, Floor = PathId.Sanctuary });
        nextOffer.Invoke(g, new object[] { me });
        Check(me.PendingCards == 0 && !me.OfferCards, "with a full hand and full kits the pick is skipped");
        g.RoomTokens.Clear();

        // The friend's pick reaches their screen, and they pick through the host.
        var friend = g.Players[2];
        Array.Clear(friend.Hand, 0, friend.Hand.Length);
        friend.LegendaryQueue.Clear();
        friend.PendingCards = 1;
        nextOffer.Invoke(g, new object[] { friend });
        Action hold = () => g.State = MatchState.Intermission;  // (keep the break open: picks only count in it)
        Run(s, dt, 1f, () => cg.Players[2].OfferCards && cg.Players[2].Offer != null && cg.Players[2].Offer.SequenceEqual(friend.Offer), hold);
        Check(cg.Players[2].OfferCards && cg.Players[2].PendingCards == 1, "the friend sees their card pick");
        int want = friend.Offer[0];
        cg.PickUpgrade(0);
        Run(s, dt, 1f, () => friend.PendingCards == 0, hold);
        Check(friend.PendingCards == 0 && (BossCards.IsKit(want) ? g.RoomTokens.Count == 1 : friend.Hand[0] == (byte)want), "and their pick (" + BossCards.Name(want) + ") lands");

        // A bot takes a service card the map lacks over the rest.
        me.Offer = new[] { (int)RoomCard.Box, BossCards.KitCode(PathId.FrozenLake), (int)RoomCard.Arms, (int)RoomCard.MiniBoss, (int)RoomCard.Ring };
        Array.Clear(me.Hand, 0, me.Hand.Length);
        int botPick = (int)typeof(Game).GetMethod("BotBossCard", flags).Invoke(g, new object[] { me });
        Check(botPick == 2, "a bot takes the Arms Dealer card when the map has none");

        // Out of the Expanding arena: back to the weapon mod pick.
        foreach (var gg in new[] { g, cg }) gg.Diff.Set(DiffKnob.ArenaExpanding, 0f);
        noPicks();
        g.State = MatchState.Fighting;
        typeof(Game).GetField("bossRewarded", flags).SetValue(g, false);
        typeof(Game).GetMethod("OnEnemyKilled", flags).Invoke(g, new object[] { boss });
        Check(!g.BossCardPicks && g.Players.Values.All(p => p.PendingMods == 1 && p.PendingCards == 0), "with room cards off a boss gives the weapon mod pick as before");
    }
}

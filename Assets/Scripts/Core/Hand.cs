using System;
using System.Collections.Generic;

namespace Arena
{
    /// A card code in a player's hand (Docs/GridPieces.md): codes from Expansion.PieceCardBase up are grid pieces (GridCards.cs); below it the
    /// service and utility cards, which build one cell. New values go at the end. Box to Chamfer and MiniBoss were the old room cards (retired
    /// with them, Nov 18): kept so the codes stay the same.
    public enum RoomCard : byte { None, Box, Hall, Ring, Cross, LHall, THall, Chamfer, MiniBoss, BuilderShop, Shop, Market, Arms }

    public static partial class Expansion
    {
        public const int HandMax = 6;  // cards a player holds; more are dropped

        /// The service cards: each builds one cell with a job (Docs/ArmsDealer.md).
        public static readonly RoomCard[] ServiceCards = { RoomCard.Shop, RoomCard.Market, RoomCard.Arms };

        public const int ServiceCardChance = 5;  // a dropped card (a wave drop, a chest) is a service card 1 in this many times

        public static bool IsServiceCard(RoomCard c) => c == RoomCard.Shop || c == RoomCard.Market || c == RoomCard.Arms;

        /// The job of the room a card builds: a service card's shop, Black Market or Arms Dealer, a Builder Shop card's card shop, else combat.
        public static RoomKind CardRoomKind(RoomCard c)
            => c == RoomCard.Shop ? RoomKind.Shop : c == RoomCard.Market ? RoomKind.Vendor : c == RoomCard.Arms ? RoomKind.Arms : c == RoomCard.BuilderShop ? RoomKind.Builder : RoomKind.Combat;

        public static string CardName(RoomCard c)
        {
            if (IsPieceCard(c))
                return PieceCardName(c);
            switch (c)
            {
                case RoomCard.Shop: return "Shop";
                case RoomCard.Market: return "Black Market";
                case RoomCard.Arms: return "Arms Dealer";
                case RoomCard.BuilderShop: return "Builder Shop";
                default: return "";
            }
        }

        /// What the card's room is for, one line (the build screen's tip).
        public static string CardBlurb(RoomCard c)
        {
            if (IsPieceCard(c))
                return PieceCardBlurb(c);
            switch (c)
            {
                case RoomCard.Shop: return "A one-cell shop: upgrade cards for gold, heals and revives. Safe, no enemies spawn in it.";
                case RoomCard.Market: return "A one-cell Black Market: buildables, services and piece cards. Safe, no enemies spawn in it.";
                case RoomCard.Arms: return "A one-cell Arms Dealer: weapon mods for gold. Safe, no enemies spawn in it.";
                case RoomCard.BuilderShop: return "A utility room: a shop that sells piece cards for vectors, a new shelf each wave, a quarter off.";
                default: return "";
            }
        }

        /// What the vendor charges for one, in vectors.
        public static int CardPrice(RoomCard c)
        {
            if (IsPieceCard(c))
                return PiecePrice(c);
            switch (c)
            {
                case RoomCard.BuilderShop: return 36;
                default: return 30;  // (a service card: not on the Black Market's shelf; a price for completeness)
            }
        }

        public const int CardOfferCount = 3;  // the vendor's card shelf
    }

    public partial class Game
    {
        public const int CardWaveChance = 4;  // a cleared regular wave: 1 in this many players gets a card

        public static int HandCount(Player p)
        {
            int n = 0;
            foreach (var c in p.Hand)
                if (c != (byte)RoomCard.None) n++;
            return n;
        }

        /// Host: a card goes into this player's hand (dropped, with a log line, when it is full).
        bool GiveCard(Player p, RoomCard c, string why)
        {
            if (!IsHost || c == RoomCard.None)
                return false;
            for (int i = 0; i < p.Hand.Length; i++)
                if (p.Hand[i] == (byte)RoomCard.None)
                {
                    p.Hand[i] = (byte)c;
                    DoLog(p.Name + " gets a " + Expansion.CardName(c) + " card (" + why + ")");
                    return true;
                }
            DoLog(p.Name + "'s hand is full: a " + Expansion.CardName(c) + " card is lost (" + why + ")");
            return false;
        }

        static void TakeCard(Player p, int slot)
        {
            for (int i = slot; i < p.Hand.Length - 1; i++)
                p.Hand[i] = p.Hand[i + 1];
            p.Hand[p.Hand.Length - 1] = (byte)RoomCard.None;
        }

        /// Host, as a build round opens: anyone not dealt yet gets the starting hand (Expansion.GridStartHand), then everyone the round's basic piece.
        void DealRoundCards()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents)
                return;
            foreach (var p in Players.Values)
            {
                if (!p.CardsDealt)
                {
                    p.CardsDealt = true;
                    if (HandCount(p) == 0)
                        foreach (var c in Expansion.GridStartHand(expandSeed + p.Id * 31))
                            GiveCard(p, c, "starting hand");
                }
                GiveCard(p, Expansion.GridRoundCard(Wave), "build round");
            }
        }

        /// Host, when a wave is cleared (before the round opens): a regular wave gives each player a 1 in CardWaveChance card. A boss
        /// gives nothing here: its reward is the card pick (BossCards.cs).
        void DropWaveCards(bool boss)
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents || boss)
                return;
            var ids = SnapKeys(Players);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                var rng = new DetRandom(expandSeed * 13 + Wave * 977 + ids[i] * 61);
                if (rng.Int(CardWaveChance) == 0)
                    GiveCard(Players[ids[i]], Expansion.GridDropCard(rng, Wave), "wave drop");
            }
            Free(ids);
        }

        /// Host: a chest node was opened: everyone gets a card (Expansion.GridDropCard).
        void ChestCards(int nodeIndex)
        {
            if (!IsHost || !ExpandingOn)
                return;
            foreach (var p in Players.Values)
                GiveCard(p, Expansion.GridDropCard(new DetRandom(expandSeed * 7 + nodeIndex * 131 + p.Id * 17 + Wave), Wave), "chest");
        }

        /// The vendor's cards this boss cycle.
        public RoomCard[] VendorCards => Expansion.GridCardOffers(expandSeed, (Math.Max(1, Wave) - 1) / Expansion.Milestone);

        /// Host: the vendor sells a card from its shelf (offer index) for vectors (Shop.cs, ShopAction.BuyCard).
        void BuyRoomCard(Player p, int offer)
        {
            if (!ExpandingOn || offer < 0 || offer >= Expansion.CardOfferCount || HandCount(p) >= Expansion.HandMax)
                return;
            var c = VendorCards[offer];
            int price = Expansion.CardPrice(c);
            if (SpendableVectors(p) < price)
                return;
            PayVectors(p, price);
            GiveCard(p, c, "bought for " + price + " vectors");
        }

        /// What a bot builds toward: the nearest sealed room waiting off the map (not a mini-boss's), else the nearest unclaimed node (null if none).
        V2? BotCardGoal()
        {
            V2? goal = null;
            float best = float.MaxValue;
            for (int i = 0; i < Expanded.Count; i++)
                if (Expanded[i].Floating && i + 1 < ExpansionWorld.Areas.Length && ExpansionWorld.Areas[i + 1].Sealed && Expanded[i].Kind != Arena.RoomKind.Gatekeeper)
                {
                    var m = new V2((Expanded[i].X + Expanded[i].X1) / 2f, (Expanded[i].Y + Expanded[i].Y1) / 2f);
                    float d = m.DistanceTo(new V2(Width / 2f, Height / 2f));
                    if (d < best) { best = d; goal = m; }
                }
            if (goal.HasValue)
                return goal;
            foreach (var nd in Nodes)
                if (!nd.Spent && !Expansion.NodeClaimed(Expanded, nd))
                {
                    float d = nd.Pos.DistanceTo(new V2(Width / 2f, Height / 2f));
                    if (d < best) { best = d; goal = nd.Pos; }
                }
            return goal;
        }
    }
}

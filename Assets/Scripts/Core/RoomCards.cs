using System;
using System.Collections.Generic;

namespace Arena
{
    /// A card in a player's hand. In a Grid run (the only building mode since Nov 18) a hand code from Expansion.PieceCardBase up is a grid piece
    /// (GridCards.cs); below it are the service cards (Shop, Market, Arms, Builder Shop: one cell each). Box..Chamfer and MiniBoss are the retired room cards
    /// (Docs/RoomCards.md): an old save's hand may still hold them; they can't be played. New values go at the end.
    public enum RoomCard : byte { None, Box, Hall, Ring, Cross, LHall, THall, Chamfer, MiniBoss, BuilderShop, Shop, Market, Arms }

    public static partial class Expansion
    {
        public const int HandMax = 6;  // cards a player holds; more are dropped

        /// The service cards: each builds one cell with a job (Docs/ArmsDealer.md). They drop like pieces.
        public static readonly RoomCard[] ServiceCards = { RoomCard.Shop, RoomCard.Market, RoomCard.Arms };

        public const int ServiceCardChance = 5;  // a drop (a wave, a chest) is a service card 1 in this many times

        public static bool IsServiceCard(RoomCard c) => c == RoomCard.Shop || c == RoomCard.Market || c == RoomCard.Arms;

        /// The job of the room a card builds: a service card's shop, Black Market or Arms Dealer, the Builder Shop's card shop, else combat.
        public static RoomKind CardRoomKind(RoomCard c)
            => c == RoomCard.Shop ? RoomKind.Shop : c == RoomCard.Market ? RoomKind.Vendor : c == RoomCard.Arms ? RoomKind.Arms : c == RoomCard.BuilderShop ? RoomKind.Builder : RoomKind.Combat;

        public static string CardName(RoomCard c)
        {
            if (IsPieceCard(c))
                return PieceCardName(c);
            switch (c)
            {
                case RoomCard.None: return "";
                case RoomCard.Shop: return "Shop";
                case RoomCard.Market: return "Black Market";
                case RoomCard.Arms: return "Arms Dealer";
                case RoomCard.BuilderShop: return "Builder Shop";
                default: return "Old room card";
            }
        }

        /// What the card's room is for, one line (the build screen's tip).
        public static string CardBlurb(RoomCard c)
        {
            if (IsPieceCard(c))
                return PieceCardBlurb(c);
            switch (c)
            {
                case RoomCard.Shop: return "One cell of shop: upgrade cards for gold, heals and revives. Safe, no enemies spawn in it.";
                case RoomCard.Market: return "One cell of Black Market: buildables, services and pieces. Safe, no enemies spawn in it.";
                case RoomCard.Arms: return "One cell of Arms Dealer: weapon mods for gold. Safe, no enemies spawn in it.";
                case RoomCard.BuilderShop: return "One cell of Builder Shop: pieces for vectors, a new shelf each wave, a quarter off.";
                default: return "A room card from before Grid: it can't be played.";
            }
        }

        /// What the vendor charges for one, in vectors.
        public static int CardPrice(RoomCard c)
        {
            if (IsPieceCard(c))
                return PiecePrice(c);
            return c == RoomCard.BuilderShop ? 36 : IsServiceCard(c) ? 30 : 0;
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

        /// Host, as a build round opens: anyone not dealt yet gets the starting hand (a single, a domino, a tromino), then everyone the round's piece.
        void DealRoundCards()
        {
            if (!IsHost || !HandOn || !ModeRules.RunEvents)
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
            if (!IsHost || !HandOn || !ModeRules.RunEvents || boss)
                return;  // (a boss gives the card pick instead: BossCards.cs, Docs/ArmsDealer.md)
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

        /// Host: a chest node was opened: everyone gets a card (a piece or a service card, Expansion.GridDropCard).
        void ChestCards(int nodeIndex)
        {
            if (!IsHost || !HandOn)
                return;
            foreach (var p in Players.Values)
                GiveCard(p, Expansion.GridDropCard(new DetRandom(expandSeed * 7 + nodeIndex * 131 + p.Id * 17 + Wave), Wave), "chest");
        }

        /// The vendor's cards this boss cycle.
        public RoomCard[] VendorCards => Expansion.GridCardOffers(expandSeed, (Math.Max(1, Wave) - 1) / Expansion.Milestone);

        /// Host: the vendor sells a card from its shelf (offer index) for vectors (Shop.cs, ShopAction.BuyCard).
        void BuyRoomCard(Player p, int offer)
        {
            if (!HandOn || offer < 0 || offer >= Expansion.CardOfferCount || HandCount(p) >= Expansion.HandMax)
                return;
            var c = VendorCards[offer];
            int price = Expansion.CardPrice(c);
            if (SpendableVectors(p) < price)
                return;
            PayVectors(p, price);
            GiveCard(p, c, "bought for " + price + " vectors");
        }

        /// What a bot builds toward: the nearest sealed shop or vendor room, else the nearest unclaimed node (null if none).
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

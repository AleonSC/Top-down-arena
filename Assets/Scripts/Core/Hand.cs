using System;
using System.Collections.Generic;

namespace Arena
{
    /// A card code in a player's hand (Docs/GridPieces.md): codes from Expansion.PieceCodeBase up are grid pieces (GridPieces.cs); below it the
    /// service and utility cards, which build one cell. New values go at the end. Box to Chamfer and MiniBoss were the old room cards (retired
    /// with them, Nov 18): kept so the codes stay the same.
    public enum PieceCard : ushort { None, Box, Hall, Ring, Cross, LHall, THall, Chamfer, MiniBoss, BuilderShop, Shop, Market, Arms, Medic }

    public static partial class Expansion
    {
        public const int HandMax = 6;  // cards a player holds; more are dropped

        /// The service cards: each builds one cell with a job (Docs/ArmsDealer.md).
        public static readonly PieceCard[] ServiceCards = { PieceCard.Shop, PieceCard.Market, PieceCard.Arms, PieceCard.Medic };

        public const int ServiceCardChance = 5;  // a dropped card (a wave drop, a chest) is a service card 1 in this many times

        public static bool IsServiceCard(PieceCard c) => c == PieceCard.Shop || c == PieceCard.Market || c == PieceCard.Arms || c == PieceCard.Medic;

        /// The job of the room a card builds: a service card's shop, Black Market, Arms Dealer or Medic, a Builder Shop card's card shop, else combat.
        public static RoomKind CardRoomKind(PieceCard c)
            => c == PieceCard.Shop ? RoomKind.Shop : c == PieceCard.Market ? RoomKind.BlackMarket : c == PieceCard.Arms ? RoomKind.Arms : c == PieceCard.Medic ? RoomKind.Medic : c == PieceCard.BuilderShop ? RoomKind.Builder : RoomKind.Combat;

        public static string CardName(PieceCard c)
        {
            if (IsShapePieceCard(c))
                return PieceCardName(c);
            switch (c)
            {
                case PieceCard.Shop: return "Shop";
                case PieceCard.Market: return "Black Market";
                case PieceCard.Arms: return "Arms Dealer";
                case PieceCard.Medic: return "Medic";
                case PieceCard.BuilderShop: return "Builder Shop";
                default: return "";
            }
        }

        /// What the card's room is for, one line (the build screen's tip).
        public static string CardBlurb(PieceCard c)
        {
            if (IsShapePieceCard(c))
                return PieceCardBlurb(c);
            switch (c)
            {
                case PieceCard.Shop: return "A one-cell shop: upgrade cards for gold, selling, floor kits and vector packs. Safe, no enemies spawn in it.";
                case PieceCard.Market: return "A one-cell Black Market: teleporters and traps for gold, and services. Safe, no enemies spawn in it.";
                case PieceCard.Arms: return "A one-cell Arms Dealer: weapon mods for gold. Safe, no enemies spawn in it.";
                case PieceCard.Medic: return "A one-cell Medic: full heals and revive tokens for gold. Safe, no enemies spawn in it.";
                case PieceCard.BuilderShop: return "A utility room: a shop that sells piece cards for vectors, a new shelf each wave, a quarter off.";
                default: return "";
            }
        }

        /// A card's base price in vectors (the Builder Shop charges three quarters of it, BuilderShop.cs).
        public static int CardPrice(PieceCard c)
        {
            if (IsShapePieceCard(c))
                return PieceCardPrice(c);
            switch (c)
            {
                case PieceCard.BuilderShop: return 36;
                default: return 30;  // (a service card: no shop sells one; a price for completeness)
            }
        }
    }

    public partial class Game
    {
        public const int CardWaveChance = 4;  // a cleared regular wave: 1 in this many players gets a card

        public static int HandCount(Player p)
        {
            int n = 0;
            foreach (var c in p.Hand)
                if (c != (int)PieceCard.None) n++;
            return n;
        }

        /// Host: a card goes into this player's hand (dropped, with a log line, when it is full).
        bool GiveCard(Player p, PieceCard c, string why)
        {
            if (!IsHost || c == PieceCard.None)
                return false;
            for (int i = 0; i < p.Hand.Length; i++)
                if (p.Hand[i] == (int)PieceCard.None)
                {
                    p.Hand[i] = (int)c;
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
            p.Hand[p.Hand.Length - 1] = (int)PieceCard.None;
        }

        /// Host, as a run starts (after a resume, whose seats were dealt): each player with an empty hand gets the starting piece pick now,
        /// so it's made before the opening GET READY (the countdown waits while anyone picks, then runs OpeningCountdown: Waves.cs).
        void DealStartPicks()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents)
                return;
            foreach (var p in Players.Values)
                if (!p.CardsDealt)
                {
                    p.CardsDealt = true;
                    if (HandCount(p) > 0)
                        continue;
                    p.StartPick = true;
                    p.PendingCards++;
                    if (p.Offer == null)
                        NextOffer(p);
                }
        }

        /// Host, as a build round opens: anyone not dealt yet (a seat the run start missed) with an empty hand gets the starting piece pick (StartCardOffer: one of two, first),
        /// then everyone a wave card pick (WaveCardOffer: take one of two, after any boss pick).
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
                    {
                        p.StartPick = true;
                        p.PendingCards++;
                    }
                }
                p.PendingCards++;
                p.WavePicks++;
                if (p.Offer == null)
                    NextOffer(p);
            }
        }

        /// Host: the starting piece pick's two cards (Expansion.GridStartOffer), seeded per player.
        int[] StartCardOffer(Player p) => Array.ConvertAll(Expansion.GridStartOffer(expandSeed + p.Id * 31), c => (int)c);

        /// Host: a wave card pick's two cards: the round's basic piece (Expansion.GridRoundCard) and a dropped card (Expansion.GridDropCard), seeded per player and wave.
        int[] WaveCardOffer(Player p)
        {
            var rng = new DetRandom(expandSeed * 31 + Wave * 1009 + p.Id * 67);
            int basic = (int)Expansion.GridRoundCard(rng, Wave);
            int drop = basic;
            for (int tries = 0; drop == basic && tries < 20; tries++)
                drop = (int)Expansion.GridDropCard(rng, Math.Max(1, Wave));
            return new[] { basic, drop };
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
        /// The piece a piece chest offers this player (seeded from its node, so a reload offers the same): they take it or throw it away
        /// (Props.cs LootKind.Card).
        PieceCard ChestCardFor(Player p, int nodeIndex) => Expansion.GridDropCard(new DetRandom(expandSeed * 7 + nodeIndex * 131 + p.Id * 17 + Wave), Wave);

        /// What a bot builds toward: the nearest sealed room waiting off the map (not a mini-boss's), else the nearest unclaimed node (null if none).
        V2? BotCardGoal()
        {
            V2? goal = null;
            float best = float.MaxValue;
            for (int i = 0; i < Expanded.Count; i++)
                if (Expanded[i].Floating && i + 1 < ExpansionWorld.Areas.Length && ExpansionWorld.Areas[i + 1].Sealed && Expanded[i].Kind != Arena.RoomKind.Gatekeeper)
                {
                    var m = new V2((Expanded[i].X + Expanded[i].X1) / 2f, (Expanded[i].Y + Expanded[i].Y1) / 2f);
                    float d = m.DistanceTo(ArenaMid);
                    if (d < best) { best = d; goal = m; }
                }
            if (goal.HasValue)
                return goal;
            foreach (var nd in Nodes)
                if (!nd.Spent && !Expansion.NodeClaimed(Expanded, nd))
                {
                    float d = nd.Pos.DistanceTo(ArenaMid);
                    if (d < best) { best = d; goal = nd.Pos; }
                }
            return goal;
        }
    }
}

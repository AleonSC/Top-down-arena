using System;
using System.Collections.Generic;

namespace Arena
{
    /// The boss card pick (Docs/ArmsDealer.md step 3): in the Expanding arena, a beaten boss gives each player a pick of
    /// BossCards.OfferSize random cards (pieces, service cards, floor kits) in place of the weapon mod pick
    /// (mods are bought at the Arms Dealer now). The offer rides in Player.Offer as codes, with Player.OfferCards set: a code below KitBase
    /// is a Piece (to your hand), KitBase + a PathId is a Floor kit (to the team's kits). Rerollable.
    public static class BossCards
    {
        public const int OfferSize = 5;
        public const int KitBase = 20000;

        /// The pool's groups and their weights: pieces 65, service cards 20, floor kits 15 (the mini-boss waits on the lattice instead, Docs/GridPieces.md).
        static readonly int[] Weights = { 65, 20, 15 };
        static readonly PieceCard[] Services = { PieceCard.Shop, PieceCard.Market, PieceCard.Arms, PieceCard.Medic, PieceCard.BuilderShop };  // (the service cards and the Builder Shop)

        public static bool IsKit(int code) => code >= KitBase;
        public static PieceCard CardOf(int code) => IsKit(code) ? PieceCard.None : (PieceCard)code;
        public static PathId FloorOf(int code) => IsKit(code) ? (PathId)(code - KitBase) : PathId.None;
        public static int KitCode(PathId floor) => KitBase + (int)floor;

        public static string Name(int code) => IsKit(code) ? Paths.Get(FloorOf(code)).Name + " floor kit" : Expansion.CardName(CardOf(code)) + " card";

        public static string Blurb(int code) => IsKit(code)
            ? "For the team: turn one of your combat rooms into this floor. " + Paths.Get(FloorOf(code)).Blurb
            : Expansion.CardBlurb(CardOf(code));

        /// A seeded offer: OfferSize different codes, each from a group picked by weight (pieces as dropped at this wave). Floor kits prefer floors nobody has yet.
        public static int[] Offer(DetRandom rng, IList<ExpansionRoom> built, IList<RoomToken> held, int wave)
        {
            var floors = new List<PathId>();
            foreach (var f in Expansion.Floors)
            {
                bool used = false;
                foreach (var r in built) used |= r.Kind == RoomKind.Floor && r.Floor == f;
                foreach (var t in held) used |= t.Kind == RoomKind.Floor && t.Floor == f;
                if (!used) floors.Add(f);
            }
            if (floors.Count == 0)
                floors.AddRange(Expansion.Floors);
            var offer = new List<int>(OfferSize);
            for (int tries = 0; offer.Count < OfferSize && tries < 200; tries++)
            {
                int g = Pick(rng, Weights);
                int code = g == 0 ? (int)Expansion.GridDropCard(rng, Math.Max(1, wave)) : g == 1 ? (int)Services[rng.Int(Services.Length)] : KitCode(floors[rng.Int(floors.Count)]);
                if (!offer.Contains(code))
                    offer.Add(code);
            }
            return offer.ToArray();
        }

        static int Pick(DetRandom rng, int[] w)
        {
            int total = 0;
            foreach (int x in w) total += x;
            int roll = rng.Int(total);
            for (int k = 0; k < w.Length; k++)
            {
                if (roll < w[k]) return k;
                roll -= w[k];
            }
            return 0;
        }
    }

    public partial class Game
    {
        /// Bosses give the card pick instead of a weapon mod: the Expanding arena, in a run with events.
        public bool BossCardPicks => ExpandingOn && ModeRules.RunEvents;

        /// Host: this player's boss card offer (from the host's random numbers, so a reroll gives a new one).
        int[] BossCardOffer(Player p) => BossCards.Offer(new DetRandom(rng.Next() ^ (p.Id * 7919)), Expanded, RoomTokens, Wave);

        /// Can this player take this code now: a card needs room in their hand, a kit room in the team's kits.
        public bool BossCardUsable(Player p, int code) => BossCards.IsKit(code) ? RoomTokens.Count < Expansion.MaxTokens : HandCount(p) < Expansion.HandMax;

        bool AnyBossCardUsable(Player p, int[] offer)
        {
            foreach (int c in offer)
                if (BossCardUsable(p, c))
                    return true;
            return false;
        }

        /// Host: one card pick is used up (a wave pick counts in WavePicks too; the starting pick clears StartPick).
        static void UseCardPick(Player p)
        {
            if (p.OfferStart)
                p.StartPick = false;
            else if (p.OfferWave)
                p.WavePicks = Math.Max(0, p.WavePicks - 1);
            p.PendingCards = Math.Max(0, p.PendingCards - 1);
        }

        /// Host: the picked code goes to the hand or the team's kits.
        void GrantBossCard(Player p, int code)
        {
            if (BossCards.IsKit(code))
                GrantRoomToken(new RoomToken { Kind = Arena.RoomKind.Floor, Floor = BossCards.FloorOf(code) });
            else
                GiveCard(p, BossCards.CardOf(code), "boss pick");
        }

        /// A bot's boss card: a service card the map lacks, else a piece, else the first it can take.
        int BotBossCard(Player me)
        {
            int best = -1, bestScore = int.MinValue;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                int c = me.Offer[i];
                if (!BossCardUsable(me, c))
                    continue;
                var card = BossCards.CardOf(c);
                int score = BossCards.IsKit(c) ? 1 : Expansion.IsShapePieceCard(card) ? 3 : 2;
                if (Expansion.IsServiceCard(card))
                {
                    var job = Expansion.CardRoomKind(card);
                    bool have = job == Arena.RoomKind.Shop ? HasShopRoom : job == Arena.RoomKind.BlackMarket ? HasBlackMarketRoom : job == Arena.RoomKind.Medic ? HasMedicRoom : HasArmsRoom;
                    score = have ? 2 : 4;
                }
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return Math.Max(0, best);
        }
    }
}

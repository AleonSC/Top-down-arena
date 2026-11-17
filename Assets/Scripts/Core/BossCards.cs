using System;
using System.Collections.Generic;

namespace Arena
{
    /// The boss card pick (Docs/ArmsDealer.md step 3): in the Expanding arena with room cards on, a beaten boss gives each player a pick of
    /// BossCards.OfferSize random cards (room cards, shape and service cards, the Mini-boss card, floor kits) in place of the weapon mod pick
    /// (mods are bought at the Arms Dealer now). The offer rides in Player.Offer as codes, with Player.OfferCards set: a code below KitBase
    /// is a RoomCard (to your hand), KitBase + a PathId is a Floor kit (to the team's kits). Rerollable.
    public static class BossCards
    {
        public const int OfferSize = 5;
        public const int KitBase = 1000;

        /// The pool's groups and their weights (first guesses): shape cards 40, Box / Hall 15, service cards 20, Floor kits 15, Mini-boss 10.
        static readonly int[] GroupWeights = { 40, 15, 20, 15, 10 };
        static readonly RoomCard[] Basics = { RoomCard.Box, RoomCard.Hall };

        public static bool IsKit(int code) => code >= KitBase;
        public static RoomCard CardOf(int code) => IsKit(code) ? RoomCard.None : (RoomCard)code;
        public static PathId FloorOf(int code) => IsKit(code) ? (PathId)(code - KitBase) : PathId.None;
        public static int KitCode(PathId floor) => KitBase + (int)floor;

        public static string Name(int code) => IsKit(code) ? Paths.Get(FloorOf(code)).Name + " floor kit" : Expansion.CardName(CardOf(code)) + " card";

        public static string Blurb(int code) => IsKit(code)
            ? "For the team: turn one of your combat rooms into this floor. " + Paths.Get(FloorOf(code)).Blurb
            : Expansion.CardBlurb(CardOf(code));

        /// A seeded offer: OfferSize different codes, each from a group picked by weight. Floor kits prefer floors nobody has yet.
        public static int[] Offer(DetRandom rng, IList<ExpansionRoom> built, IList<RoomToken> held)
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
                int code;
                switch (Pick(rng, GroupWeights))
                {
                    case 0: code = (int)Expansion.ShapeCards[rng.Int(Expansion.ShapeCards.Length)]; break;
                    case 1: code = (int)Basics[rng.Int(Basics.Length)]; break;
                    case 2: code = (int)Expansion.ServiceCards[rng.Int(Expansion.ServiceCards.Length)]; break;
                    case 3: code = KitCode(floors[rng.Int(floors.Count)]); break;
                    default: code = (int)RoomCard.MiniBoss; break;
                }
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
        /// Bosses give the card pick instead of a weapon mod: the Expanding arena building from room cards, in a run with events.
        public bool BossCardPicks => RoomCardsOn && ModeRules.RunEvents;

        /// Host: this player's boss card offer (from the host's random numbers, so a reroll gives a new one).
        int[] BossCardOffer(Player p) => BossCards.Offer(new DetRandom(rng.Next() ^ (p.Id * 7919)), Expanded, RoomTokens);

        /// Can this player take this code now: a card needs room in their hand, a kit room in the team's kits.
        public bool BossCardUsable(Player p, int code) => BossCards.IsKit(code) ? RoomTokens.Count < Expansion.MaxTokens : HandCount(p) < Expansion.HandMax;

        bool AnyBossCardUsable(Player p, int[] offer)
        {
            foreach (int c in offer)
                if (BossCardUsable(p, c))
                    return true;
            return false;
        }

        /// Host: the picked code goes to the hand or the team's kits.
        void GrantBossCard(Player p, int code)
        {
            if (BossCards.IsKit(code))
                GrantRoomToken(new RoomToken { Kind = Arena.RoomKind.Floor, Floor = BossCards.FloorOf(code) });
            else
                GiveCard(p, BossCards.CardOf(code), "boss pick");
        }

        /// A bot's boss card: a service card the map lacks, else a shape card, else the first it can take.
        int BotBossCard(Player me)
        {
            int best = -1, bestScore = int.MinValue;
            for (int i = 0; i < me.Offer.Length; i++)
            {
                int c = me.Offer[i];
                if (!BossCardUsable(me, c))
                    continue;
                var card = BossCards.CardOf(c);
                int score = BossCards.IsKit(c) ? 1 : card == RoomCard.MiniBoss ? 0 : Array.IndexOf(Expansion.ShapeCards, card) >= 0 ? 3 : 2;
                if (Expansion.IsServiceCard(card))
                {
                    var job = Expansion.CardRoomKind(card);
                    bool have = job == Arena.RoomKind.Shop ? HasShopRoom : job == Arena.RoomKind.Vendor ? HasVendorRoom : HasArmsRoom;
                    score = have ? 2 : 4;
                }
                if (score > bestScore) { bestScore = score; best = i; }
            }
            return Math.Max(0, best);
        }
    }
}

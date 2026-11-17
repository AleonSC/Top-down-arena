using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A room card (Docs/RoomCards.md): a preset room a player holds and places against a room's wall. New values go at the end.
    public enum RoomCard : byte { None, Box, Hall, Ring, Cross, LHall, THall, Chamfer, MiniBoss, Shop, Market, Arms }  // (Shop, Market, Arms: service cards, a tiny 4 x 4 room, Docs/ArmsDealer.md)

    public static partial class Expansion
    {
        public const int HandMax = 6;  // cards a player holds; more are dropped

        /// The shape cards (not the basics, not the mini-boss): what drops and chests give.
        public static readonly RoomCard[] ShapeCards = { RoomCard.Ring, RoomCard.Cross, RoomCard.LHall, RoomCard.THall, RoomCard.Chamfer };

        /// The service cards: each builds a tiny 4 x 4 room with a job (Docs/ArmsDealer.md). They drop like shape cards.
        public static readonly RoomCard[] ServiceCards = { RoomCard.Shop, RoomCard.Market, RoomCard.Arms };

        public const int ServiceCardChance = 5;  // a shape-card roll (a drop, a chest) is a service card 1 in this many times

        public static bool IsServiceCard(RoomCard c) => c == RoomCard.Shop || c == RoomCard.Market || c == RoomCard.Arms;

        /// The job of the room a card builds: a service card's shop, Black Market or Arms Dealer, a Mini-boss card's duel, else combat.
        public static RoomKind CardRoomKind(RoomCard c)
            => c == RoomCard.Shop ? RoomKind.Shop : c == RoomCard.Market ? RoomKind.Vendor : c == RoomCard.Arms ? RoomKind.Arms : c == RoomCard.MiniBoss ? RoomKind.Gatekeeper : RoomKind.Combat;

        public static SurpriseShape CardShape(RoomCard c)
        {
            switch (c)
            {
                case RoomCard.Box: return SurpriseShape.Box;
                case RoomCard.Hall: return SurpriseShape.Hall;
                case RoomCard.Ring: return SurpriseShape.Ring;
                case RoomCard.Cross: return SurpriseShape.Cross;
                case RoomCard.LHall: return SurpriseShape.LHall;
                case RoomCard.THall: return SurpriseShape.THall;
                case RoomCard.MiniBoss: return SurpriseShape.Duel;
                case RoomCard.Shop:
                case RoomCard.Market:
                case RoomCard.Arms: return SurpriseShape.Tiny;
                default: return SurpriseShape.Chamfer;
            }
        }

        public static string CardName(RoomCard c)
        {
            switch (c)
            {
                case RoomCard.None: return "";
                case RoomCard.MiniBoss: return "Mini-boss";
                case RoomCard.Shop: return "Shop";
                case RoomCard.Market: return "Black Market";
                case RoomCard.Arms: return "Arms Dealer";
                default: return SurpriseName(CardShape(c));
            }
        }

        /// What the card's room is for, one line (the build screen's tip).
        public static string CardBlurb(RoomCard c)
        {
            switch (c)
            {
                case RoomCard.Box: return "A plain 7 x 6 room.";
                case RoomCard.Hall: return "A straight hallway, 8 tiles long: reach a shop, a vendor or a node.";
                case RoomCard.Ring: return "A lane round a solid block: a loop to kite enemies round.";
                case RoomCard.Cross: return "Four arms and a crossing: four sightlines, one fight point.";
                case RoomCard.LHall: return "A corner that breaks line of sight: ranged enemies walk into melee.";
                case RoomCard.THall: return "Two exits and one choke.";
                case RoomCard.Chamfer: return "A room with cut corners: nowhere to be pinned.";
                case RoomCard.MiniBoss: return "A sealed duel room with the coming boss's faction mini-boss. Walk in to wake it; beat it for a prize pick.";
                case RoomCard.Shop: return "A tiny 4 x 4 shop: upgrade cards for gold, heals and revives. Safe, no enemies spawn in it.";
                case RoomCard.Market: return "A tiny 4 x 4 Black Market: buildables, services and room cards. Safe, no enemies spawn in it.";
                case RoomCard.Arms: return "A tiny 4 x 4 Arms Dealer: weapon mods for gold. Safe, no enemies spawn in it.";
                default: return "";
            }
        }

        /// What the vendor charges for one, in vectors.
        public static int CardPrice(RoomCard c)
        {
            switch (c)
            {
                case RoomCard.Box: return 16;
                case RoomCard.Hall: return 10;
                case RoomCard.Ring: return 34;
                case RoomCard.Cross: return 30;
                case RoomCard.LHall: return 24;
                case RoomCard.THall: return 26;
                case RoomCard.MiniBoss: return 40;
                case RoomCard.Shop:
                case RoomCard.Market:
                case RoomCard.Arms: return 30;  // (not on the Black Market's shelf; a price for completeness)
                default: return 22;
            }
        }

        public const int CardOfferCount = 3;  // the vendor's card shelf

        /// The vendor's card offers for a boss cycle: the same on every machine. One basic, one shape, and a third that is a mini-boss one cycle in three.
        public static RoomCard[] CardOffers(int seed, int cycle)
        {
            var rng = new DetRandom(seed * 4099 + cycle * 271 + 17);
            var a = rng.Int(2) == 0 ? RoomCard.Box : RoomCard.Hall;
            var b = ShapeCards[rng.Int(ShapeCards.Length)];
            RoomCard c;
            if (cycle % 3 == 2)
                c = RoomCard.MiniBoss;
            else
                do c = ShapeCards[rng.Int(ShapeCards.Length)]; while (c == b);
            return new[] { a, b, c };
        }

        /// A seeded shape card (the starting hand): never a service card.
        public static RoomCard RandomShapeCard(int seed) => ShapeCards[new DetRandom(seed).Int(ShapeCards.Length)];

        /// A dropped card (a wave drop, a chest): a shape card, or a service card 1 in ServiceCardChance times.
        public static RoomCard DropCard(DetRandom rng)
            => rng.Int(ServiceCardChance) == 0 ? ServiceCards[rng.Int(ServiceCards.Length)] : ShapeCards[rng.Int(ShapeCards.Length)];

        public static RoomCard DropCard(int seed) => DropCard(new DetRandom(seed));

        /// The card's room with its mask's corner at (x, y), turned and mirrored: its boxes (Price 0) if it is legal there (the outline rules:
        /// no overlap, a door's worth of shared wall, the size caps), else null with `why`.
        public static List<ExpansionRoom> CardRooms(IList<ExpansionRoom> built, RoomCard card, int x, int y, int turn, bool mirror, out string why)
        {
            why = null;
            if (card == RoomCard.None) { why = "No card"; return null; }
            if (BoughtCount(built) >= MaxRooms) { why = "The map is full"; return null; }
            if (x % Step != 0 || y % Step != 0) { why = "Off the grid"; return null; }
            var made = FromTiles(built, SurpriseMask(CardShape(card), turn, mirror), x, y, PathId.None, out why);
            if (made == null)
                return null;
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Price = 0;
                q.Surprise = 0;
                q.Card = (byte)card;
                made[i] = q;
            }
            return made;
        }

        /// Every spot the card's room can go, best first (Expansion.SurpriseOptions: loops closed, dead ends, a compact map).
        public static List<SurpriseOption> CardOptions(IList<ExpansionRoom> built, RoomCard card) => card == RoomCard.None ? new List<SurpriseOption>() : SurpriseOptions(built, CardShape(card));
    }

    public partial class Game
    {
        /// The run builds from room cards (the lobby's Building: Room cards), not by drawing.
        public bool RoomCardsOn => ExpandingOn && D(DiffKnob.RoomCards) >= 0.5f;

        public const int CardWaveChance = 4;  // a cleared regular wave: 1 in this many players gets a shape card

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

        /// Host, as a build round opens: anyone not dealt yet gets the starting hand (Box, Hall, a seeded shape), then everyone a basic card.
        void DealRoundCards()
        {
            if (!IsHost || !RoomCardsOn || !ModeRules.RunEvents)
                return;
            foreach (var p in Players.Values)
            {
                if (!p.CardsDealt)
                {
                    p.CardsDealt = true;
                    if (HandCount(p) == 0)
                    {
                        GiveCard(p, RoomCard.Box, "starting hand");
                        GiveCard(p, RoomCard.Hall, "starting hand");
                        GiveCard(p, Expansion.RandomShapeCard(expandSeed + p.Id * 31), "starting hand");
                    }
                }
                GiveCard(p, Wave % 2 == 1 ? RoomCard.Hall : RoomCard.Box, "build round");
            }
        }

        /// Host, when a wave is cleared (before the round opens): a regular wave gives each player a 1 in CardWaveChance shape card. A boss
        /// gives nothing here: its reward is the card pick (BossCards.cs).
        void DropWaveCards(bool boss)
        {
            if (!IsHost || !RoomCardsOn || !ModeRules.RunEvents)
                return;
            var ids = SnapKeys(Players);
            ids.Sort();
            for (int i = 0; i < ids.Count; i++)
            {
                var p = Players[ids[i]];
                var rng = new DetRandom(expandSeed * 13 + Wave * 977 + ids[i] * 61);
                if (boss)
                    continue;  // (a boss gives the card pick instead: BossCards.cs, Docs/ArmsDealer.md)
                if (rng.Int(CardWaveChance) == 0)
                    GiveCard(p, Expansion.DropCard(rng), "wave drop");
            }
            Free(ids);
        }

        /// Host: a chest node was opened: everyone gets a shape card (or a service card, Expansion.DropCard).
        void ChestCards(int nodeIndex)
        {
            if (!IsHost || !RoomCardsOn)
                return;
            foreach (var p in Players.Values)
                GiveCard(p, Expansion.DropCard(expandSeed * 7 + nodeIndex * 131 + p.Id * 17 + Wave), "chest");
        }

        /// The vendor's cards this boss cycle.
        public RoomCard[] VendorCards => Expansion.CardOffers(expandSeed, (Math.Max(1, Wave) - 1) / Expansion.Milestone);

        /// Host: the vendor sells a card from its shelf (offer index) for vectors (Shop.cs, ShopAction.BuyCard).
        void BuyRoomCard(Player p, int offer)
        {
            if (!RoomCardsOn || offer < 0 || offer >= Expansion.CardOfferCount || HandCount(p) >= Expansion.HandMax)
                return;
            var c = VendorCards[offer];
            int price = Expansion.CardPrice(c);
            if (SpendableVectors(p) < price)
                return;
            PayVectors(p, price);
            GiveCard(p, c, "bought for " + price + " vectors");
        }

        // --- Playing a card ----------------------------------------------------------------------------------------

        /// This game's player plays the card in this hand slot with its room's corner at (x, y), turned and mirrored. Only in a build round.
        public void PlayRoomCard(int slot, int x, int y, int turn, bool mirror)
        {
            if (!ExpandOpen || Me == null || !RoomCardsOn)
                return;
            if (IsHost)
            {
                HostPlayCard(MyId, slot, x, y, turn, mirror);
                return;
            }
            var w = Proto.Begin(Msg.PlayCard);
            w.Write((byte)slot); w.Write((short)x); w.Write((short)y); w.Write((byte)turn); w.Write(mirror);
            session.SendToHost(Proto.End(w));
        }

        public void OnPlayCard(int peer, BinaryReader r) => HostPlayCard(peer, r.ReadByte(), r.ReadInt16(), r.ReadInt16(), r.ReadByte(), r.ReadBoolean());

        /// Host: the card's room is built at once if it fits there; the card leaves the hand. A Mini-boss card's room holds the coming boss's faction mini-boss;
        /// a service card's room is a shop, Black Market or Arms Dealer.
        void HostPlayCard(int id, int slot, int x, int y, int turn, bool mirror)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !RoomCardsOn || !Players.TryGetValue(id, out p) || slot < 0 || slot >= p.Hand.Length)
                return;
            var card = (RoomCard)p.Hand[slot];
            string why;
            var made = Expansion.CardRooms(Expanded, card, x, y, turn, mirror, out why);
            if (made == null)
            {
                DoLog("card refused (" + why + ")");
                return;
            }
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Owner = id;
                q.Kind = Expansion.CardRoomKind(card);  // (a service card's shop, Black Market or Arms Dealer, already open)
                if (card == RoomCard.MiniBoss)
                    q.Mini = (byte)MiniBossNow();
                made[i] = q;
            }
            TakeCard(p, slot);
            DoLog(p.Name + " plays a " + Expansion.CardName(card) + " card at " + x + "," + y);
            BuildNow(id, made[0], made.GetRange(1, made.Count - 1));
        }

        /// A bot plays its first card: a Hall toward the nearest shop, vendor or node waiting off the map, else the best spot for it.
        void BotPlayCard()
        {
            var me = Me;
            if (me == null || HandCount(me) == 0)
                return;
            int slot = 0;
            var card = (RoomCard)me.Hand[slot];
            var options = Expansion.CardOptions(Expanded, card);
            if (options.Count == 0)
            {
                for (slot = 1; slot < me.Hand.Length && me.Hand[slot] != 0; slot++)
                {
                    card = (RoomCard)me.Hand[slot];
                    options = Expansion.CardOptions(Expanded, card);
                    if (options.Count > 0) break;
                }
                if (options.Count == 0)
                    return;
            }
            var pick = options[0];
            V2? goal = BotCardGoal();
            if (card == RoomCard.Hall && goal.HasValue)
            {
                float best = float.MaxValue;
                foreach (var o in options)
                {
                    float d = Expansion.OptionMid(o).DistanceTo(goal.Value);
                    if (d < best) { best = d; pick = o; }
                }
            }
            PlayRoomCard(slot, pick.X, pick.Y, pick.Turn, pick.Mirror);
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

    public static partial class Expansion
    {
        /// The middle of an option's footprint (its boxes' bounds).
        public static V2 OptionMid(SurpriseOption o)
        {
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (var q in o.Rooms)
            {
                x0 = Math.Min(x0, q.X); y0 = Math.Min(y0, q.Y); x1 = Math.Max(x1, q.X1); y1 = Math.Max(y1, q.Y1);
            }
            return new V2((x0 + x1) / 2f, (y0 + y1) / 2f);
        }
    }
}

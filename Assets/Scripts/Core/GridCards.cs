using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public static partial class Expansion
    {
        /// Grid piece cards (Docs/GridPieces.md step 2): the hand holds card codes (RoomCards.cs). Codes from here up are pieces: PieceCardBase + floor * 16 + the
        /// GridPiece, so any piece can be a plain combat room (floor None) or a floor-modifier room. Below it the old room cards and the service cards
        /// (Shop, Market, Arms, Builder Shop), which in a Grid run build one cell.
        public const int PieceCardBase = 32;

        public static bool IsPieceCard(RoomCard c) => (int)c >= PieceCardBase;
        public static RoomCard PieceCard(GridPiece p, PathId floor = PathId.None) => (RoomCard)(PieceCardBase + (int)floor * 16 + (int)p);
        public static GridPiece CardPiece(RoomCard c) => (GridPiece)(((int)c - PieceCardBase) % 16);
        public static PathId CardFloor(RoomCard c) => (PathId)(((int)c - PieceCardBase) / 16);

        /// A card a Grid run can play: a piece, or a service card (one cell).
        public static bool IsGridCard(RoomCard c) => IsPieceCard(c) ? (int)CardPiece(c) < gridPieceBase.Length && (int)CardFloor(c) <= (int)PathId.Bazaar : IsServiceCard(c) || c == RoomCard.BuilderShop;

        /// The shape a Grid card builds: its piece, or a single cell for a service card.
        public static GridPiece GridCardShape(RoomCard c) => IsPieceCard(c) ? CardPiece(c) : GridPiece.Mono;

        /// What the vendor charges for a piece: 6 vectors a cell, 8 more for a floor.
        public static int PiecePrice(RoomCard c) => GridPieceCells(CardPiece(c)) * 6 + (CardFloor(c) != PathId.None ? 8 : 0);

        public static string PieceCardName(RoomCard c) => (CardFloor(c) != PathId.None ? Paths.Get(CardFloor(c)).Name + " " : "") + GridPieceName(CardPiece(c));

        public static string PieceCardBlurb(RoomCard c)
        {
            int n = GridPieceCells(CardPiece(c));
            return (n == 1 ? "One cell" : n + " cells joined into one open room") + (CardFloor(c) != PathId.None ? ", with " + Paths.Get(CardFloor(c)).Name + " ground: " + Paths.Get(CardFloor(c)).Blurb : ", a plain combat room") + ". Place it on a door spot; R turns it.";
        }

        static readonly GridPiece[] gridSmall = { GridPiece.Mono, GridPiece.Domino };
        static readonly GridPiece[] gridTri = { GridPiece.TriI, GridPiece.TriL };
        static readonly GridPiece[] gridTet = { GridPiece.TetI, GridPiece.TetO, GridPiece.TetT, GridPiece.TetS, GridPiece.TetL };

        /// A floor a themed piece can carry at this wave (an unlocked one, seeded), or None if the roll says plain.
        static PathId GridFloorRoll(DetRandom rng, int wave, int plainIn)
        {
            if (rng.Int(plainIn) != 0)
                return PathId.None;
            var open = new List<PathId>();
            foreach (var f in Floors)
                if (ThemeUnlocked(f, wave))
                    open.Add(f);
            return open.Count == 0 ? PathId.None : open[rng.Int(open.Count)];
        }

        /// The starting hand: a single, a domino and a seeded tromino, all plain combat rooms.
        public static RoomCard[] GridStartHand(int seed) => new[] { PieceCard(GridPiece.Mono), PieceCard(GridPiece.Domino), PieceCard(gridTri[new DetRandom(seed).Int(gridTri.Length)]) };

        /// The basic card each build round gives: a domino after odd waves, a single after even ones.
        public static RoomCard GridRoundCard(int wave) => PieceCard(wave % 2 == 1 ? GridPiece.Domino : GridPiece.Mono);

        /// A dropped card (a wave drop, a chest): a service card 1 in ServiceCardChance times, else a piece (a single or domino 35%, a tromino 30%, a tetromino 35%),
        /// 1 in 4 of them with a floor.
        public static RoomCard GridDropCard(DetRandom rng, int wave)
        {
            if (rng.Int(ServiceCardChance) == 0)
                return ServiceCards[rng.Int(ServiceCards.Length)];
            int roll = rng.Int(100);
            var set = roll < 35 ? gridSmall : roll < 65 ? gridTri : gridTet;
            return PieceCard(set[rng.Int(set.Length)], GridFloorRoll(rng, wave, 4));
        }

        /// The vendor's piece cards for a boss cycle: a basic, a bigger piece, and a third that is a Builder Shop one cycle in three, else a floor piece. Same on every machine.
        public static RoomCard[] GridCardOffers(int seed, int cycle)
        {
            var rng = new DetRandom(seed * 4099 + cycle * 271 + 29);
            var a = PieceCard(gridSmall[rng.Int(gridSmall.Length)]);
            var set = rng.Int(2) == 0 ? gridTri : gridTet;
            var b = PieceCard(set[rng.Int(set.Length)]);
            RoomCard c;
            if (cycle % 3 == 1)
                c = RoomCard.BuilderShop;
            else
            {
                var f = GridFloorRoll(rng, cycle * Milestone + 1, 1);
                c = PieceCard(gridTet[rng.Int(gridTet.Length)], f);
            }
            return new[] { a, b, c };
        }

        /// The Builder Shop's shelf in a Grid run: a single, a domino, a tromino, a tetromino and a floor piece (seeded by wave).
        public static RoomCard[] GridBuilderOffers(int seed, int wave)
        {
            var rng = new DetRandom(seed * 7919 + wave * 389 + 43);
            return new[]
            {
                PieceCard(GridPiece.Mono), PieceCard(GridPiece.Domino), PieceCard(gridTri[rng.Int(gridTri.Length)]), PieceCard(gridTet[rng.Int(gridTet.Length)]),
                PieceCard(gridTet[rng.Int(gridTet.Length)], GridFloorRoll(rng, wave, 1)),
            };
        }
    }

    public partial class Game
    {
        /// Cards are in play: room cards, or a Grid run (whose cards are grid pieces).
        public bool HandOn => RoomCardsOn || GridRoomsOn;

        /// This game's player plays the card in this hand slot as a grid piece: its orientation, with its cell box's corner at (x, y). Only in a Grid run's build round.
        public void PlayGridCard(int slot, int orient, int x, int y)
        {
            if (!ExpandOpen || Me == null || !GridRoomsOn)
                return;
            if (IsHost)
            {
                HostGridCard(MyId, slot, orient, x, y);
                return;
            }
            var w = Proto.Begin(Msg.GridPiece);
            w.Write((byte)slot); w.Write((byte)orient); w.Write((short)x); w.Write((short)y);
            session.SendToHost(Proto.End(w));
        }

        public void OnGridPiece(int peer, BinaryReader r) => HostGridCard(peer, r.ReadByte(), r.ReadByte(), r.ReadInt16(), r.ReadInt16());

        /// Host: the card in this slot is built at once as its piece if it fits there, and leaves the hand. Playing costs nothing; a floor piece is a floor room, a service
        /// card's one cell a shop, Black Market, Arms Dealer or Builder Shop.
        void HostGridCard(int id, int slot, int orient, int x, int y)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !GridRoomsOn || !Players.TryGetValue(id, out p) || slot < 0 || slot >= p.Hand.Length)
                return;
            var card = (RoomCard)p.Hand[slot];
            if (!Expansion.IsGridCard(card))
                return;
            string why;
            var made = Expansion.GridPieceRooms(Expanded, Expansion.GridCardShape(card), orient, x, y, out why);
            if (made == null)
            {
                DoLog("piece refused (" + why + ")");
                return;
            }
            bool piece = Expansion.IsPieceCard(card);
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Owner = id;
                if (piece && Expansion.CardFloor(card) != PathId.None)
                {
                    q.Kind = Arena.RoomKind.Floor;
                    q.Floor = Expansion.CardFloor(card);
                }
                else if (!piece)
                    q.Kind = Expansion.CardRoomKind(card);
                made[i] = q;
            }
            TakeCard(p, slot);
            DoLog(p.Name + " plays a " + Expansion.CardName(card) + " card at " + x + "," + y);
            BuildNow(id, made[0], made.GetRange(1, made.Count - 1));
        }

        /// Bot: plays the first card in its hand that fits somewhere, at the spot nearest what it builds toward (RoomCards.cs BotCardGoal), else the first spot. False if none fits.
        bool BotPlayGridCard()
        {
            var me = Me;
            if (me == null)
                return false;
            V2? goal = BotCardGoal();
            for (int slot = 0; slot < me.Hand.Length; slot++)
            {
                var card = (RoomCard)me.Hand[slot];
                if (!Expansion.IsGridCard(card))
                    continue;
                var options = Expansion.GridPieceOptions(Expanded, Expansion.GridCardShape(card));
                if (options.Count == 0)
                    continue;
                var pick = options[0];
                if (goal.HasValue)
                {
                    float best = float.MaxValue;
                    foreach (var o in options)
                    {
                        float d = o.Mid.DistanceTo(goal.Value);
                        if (d < best) { best = d; pick = o; }
                    }
                }
                PlayGridCard(slot, pick.Orient, pick.X, pick.Y);
                return true;
            }
            return false;
        }
    }
}

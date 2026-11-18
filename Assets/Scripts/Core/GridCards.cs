using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A grid piece's modifier kind (GridCards.cs): Unknown is a number kept for a modifier not defined yet.
    public enum PieceModKind : byte { None, Floor, Unknown }

    public static partial class Expansion
    {
        /// Grid piece cards (Docs/GridPieces.md step 2): the hand holds card codes (RoomCards.cs). Codes from here up are pieces: PieceCardBase + mod * 16 + the
        /// GridPiece, so any piece can carry one modifier (mod 0: a plain combat room). Below it the old room cards and the service cards
        /// (Shop, Market, Arms, Builder Shop), which in a Grid run build one cell.
        public const int PieceCardBase = 32;

        /// A piece's modifier slot (owner Nov 18: "each grid piece has a chance of a modifier, floor or enemy; specifics later"). A mod is a number 0..PieceModMax
        /// in the card code: 1 to 10 are the floors (the PathId, so older hands read the same), 11 up are kept for the modifiers still to come (enemy and the like).
        /// To add one: give it a number from FirstFreeMod, a kind in ModKind, a name and blurb, add it to RollMods if it drops, and apply it in ApplyPieceMod.
        public const int PieceModMax = (255 - PieceCardBase) / 16;  // 13: the most a byte card code holds
        public const int FirstFreeMod = (int)PathId.Bazaar + 1;     // 11: the first number not taken by a floor

        public static bool IsPieceCard(RoomCard c) => (int)c >= PieceCardBase;
        public static RoomCard PieceCard(GridPiece p, PathId floor = PathId.None) => PieceCardMod(p, (int)floor);
        public static RoomCard PieceCardMod(GridPiece p, int mod) => (RoomCard)(PieceCardBase + mod * 16 + (int)p);
        public static GridPiece CardPiece(RoomCard c) => (GridPiece)(((int)c - PieceCardBase) % 16);
        public static int CardMod(RoomCard c) => IsPieceCard(c) ? ((int)c - PieceCardBase) / 16 : 0;
        public static PathId CardFloor(RoomCard c) => ModKind(CardMod(c)) == PieceModKind.Floor ? (PathId)CardMod(c) : PathId.None;

        /// What a modifier number is: none, a floor, or not yet defined (11 up, so not playable).
        public static PieceModKind ModKind(int mod) => mod == 0 ? PieceModKind.None : mod <= (int)PathId.Bazaar ? PieceModKind.Floor : PieceModKind.Unknown;

        public static string ModName(int mod) => ModKind(mod) == PieceModKind.Floor ? Paths.Get((PathId)mod).Name : "";

        public static string ModBlurb(int mod) => ModKind(mod) == PieceModKind.Floor ? "with " + Paths.Get((PathId)mod).Name + " ground: " + Paths.Get((PathId)mod).Blurb : "a plain combat room";

        /// What a modifier adds to a piece's vendor price.
        public static int ModPrice(int mod) => ModKind(mod) == PieceModKind.None ? 0 : 8;

        /// The modifiers a piece can roll at this wave: every unlocked floor (enemy modifiers join here once they exist).
        static List<int> RollMods(int wave)
        {
            var open = new List<int>();
            foreach (var f in Floors)
                if (ThemeUnlocked(f, wave))
                    open.Add((int)f);
            return open;
        }

        /// Puts a piece card's modifier on one of the rooms it builds.
        public static ExpansionRoom ApplyPieceMod(ExpansionRoom q, int mod)
        {
            if (ModKind(mod) == PieceModKind.Floor)
            {
                q.Kind = RoomKind.Floor;
                q.Floor = (PathId)mod;
            }
            return q;
        }

        /// A card a Grid run can play: a piece (with a known modifier), or a service card (one cell).
        public static bool IsGridCard(RoomCard c) => IsPieceCard(c) ? (int)CardPiece(c) < gridPieceBase.Length && ModKind(CardMod(c)) != PieceModKind.Unknown : IsServiceCard(c) || c == RoomCard.BuilderShop;

        /// The shape a Grid card builds: its piece, or a single cell for a service card.
        public static GridPiece GridCardShape(RoomCard c) => IsPieceCard(c) ? CardPiece(c) : GridPiece.Mono;

        /// What the vendor charges for a piece: 6 vectors a cell, plus its modifier (8 for a floor).
        public static int PiecePrice(RoomCard c) => GridPieceCells(CardPiece(c)) * 6 + ModPrice(CardMod(c));

        public static string PieceCardName(RoomCard c) => (CardMod(c) != 0 ? ModName(CardMod(c)) + " " : "") + GridPieceName(CardPiece(c));

        public static string PieceCardBlurb(RoomCard c)
        {
            int n = GridPieceCells(CardPiece(c));
            return (n == 1 ? "One cell" : n + " cells joined into one open room") + ", " + ModBlurb(CardMod(c)) + ". Place it on a door spot; R turns it.";
        }

        static readonly GridPiece[] gridSmall = { GridPiece.Mono, GridPiece.Domino };
        static readonly GridPiece[] gridTri = { GridPiece.TriI, GridPiece.TriL };
        static readonly GridPiece[] gridTet = { GridPiece.TetI, GridPiece.TetO, GridPiece.TetT, GridPiece.TetS, GridPiece.TetL };

        /// A modifier a piece carries at this wave (an unlocked one, seeded): 1 in plainIn pieces gets one, else 0 (plain).
        static int GridModRoll(DetRandom rng, int wave, int plainIn)
        {
            if (rng.Int(plainIn) != 0)
                return 0;
            var open = RollMods(wave);
            return open.Count == 0 ? 0 : open[rng.Int(open.Count)];
        }

        /// The starting hand: a single, a domino and a seeded tromino, all plain combat rooms.
        public static RoomCard[] GridStartHand(int seed) => new[] { PieceCard(GridPiece.Mono), PieceCard(GridPiece.Domino), PieceCard(gridTri[new DetRandom(seed).Int(gridTri.Length)]) };

        /// The basic card each build round gives: a domino after odd waves, a single after even ones.
        public static RoomCard GridRoundCard(int wave) => PieceCard(wave % 2 == 1 ? GridPiece.Domino : GridPiece.Mono);

        /// A dropped card (a wave drop, a chest): a service card 1 in ServiceCardChance times, else a piece (a single or domino 35%, a tromino 30%, a tetromino 35%),
        /// 1 in 4 of them with a modifier.
        public static RoomCard GridDropCard(DetRandom rng, int wave)
        {
            if (rng.Int(ServiceCardChance) == 0)
                return ServiceCards[rng.Int(ServiceCards.Length)];
            int roll = rng.Int(100);
            var set = roll < 35 ? gridSmall : roll < 65 ? gridTri : gridTet;
            return PieceCardMod(set[rng.Int(set.Length)], GridModRoll(rng, wave, 4));
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
                var m = GridModRoll(rng, cycle * Milestone + 1, 1);
                c = PieceCardMod(gridTet[rng.Int(gridTet.Length)], m);
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
                PieceCardMod(gridTet[rng.Int(gridTet.Length)], GridModRoll(rng, wave, 1)),
            };
        }
    }

    public static partial class Expansion
    {
        /// A Grid run's mini-boss room (Docs/GridPieces.md step 4): two cells (a domino, 1200 x 600 either way) waiting sealed on the lattice one cell out from the map,
        /// so building a piece into the cell between joins it. Seeded; null if no spot fits. Kind Gatekeeper, Floating (GatekeeperRoom.cs runs the duel once it opens).
        public static ExpansionRoom? GridMiniFor(int seed, int cycle, IList<ExpansionRoom> built, IList<MapNode> nodes)
        {
            var rng = new DetRandom(seed * 6007 + cycle * 811 + 71);
            var start = new V2(Game.Width / 2f, Game.Height / 2f);
            var firsts = GridPieceOptions(built, GridPiece.Mono);
            firsts.Sort((p, q) => q.Mid.DistanceTo(start).CompareTo(p.Mid.DistanceTo(start)));
            for (int tries = 0; tries < Math.Min(firsts.Count, 24); tries++)
            {
                var step = firsts[(rng.Int(Math.Max(1, firsts.Count / 2)) + tries) % firsts.Count];
                var map = new List<ExpansionRoom>(built);
                map.AddRange(step.Rooms);
                var outs = GridPieceOptions(map, GridPiece.Domino);
                var ok = new List<ExpansionRoom>();
                foreach (var o in outs)
                {
                    var r = o.Rooms[0];
                    if (o.Rooms.Count != 1)
                        continue;
                    bool clear = true;
                    for (int i = 0; i <= built.Count && clear; i++)  // touches only the cell between, never the built map
                    {
                        BoxOf(built, i, out int bx0, out int by0, out int bx1, out int by1);
                        clear = !Overlaps(r.X - 1, r.Y - 1, r.X1 + 1, r.Y1 + 1, bx0, by0, bx1, by1);
                    }
                    foreach (var nd in nodes)
                        clear &= nd.Spent || !(nd.X > r.X - NodeApart / 2 && nd.X < r.X1 + NodeApart / 2 && nd.Y > r.Y - NodeApart / 2 && nd.Y < r.Y1 + NodeApart / 2);
                    if (clear)
                        ok.Add(r);
                }
                if (ok.Count == 0)
                    continue;
                var pick = ok[rng.Int(ok.Count)];
                pick.Floating = true;
                pick.Kind = RoomKind.Gatekeeper;
                pick.Price = 0;
                pick.Parent = -1;
                return pick;
            }
            return null;
        }

        /// Sealed mini-boss rooms still waiting for a piece.
        public static int GridMinisWaiting(IList<ExpansionRoom> built)
        {
            var linked = ConnectedAreas(built);
            int n = 0;
            for (int i = 0; i < built.Count; i++)
                if (built[i].Floating && built[i].Kind == RoomKind.Gatekeeper && !linked[i + 1])
                    n++;
            return n;
        }
    }

    public partial class Game
    {
        /// Host, as a boss cycle's nodes are placed: a Grid run gets a mini-boss room waiting on the lattice when none waits (its foe the coming boss's faction's).
        bool PlaceGridMini(int cycle)
        {
            if (!IsHost || !GridRoomsOn || Expansion.GridMinisWaiting(Expanded) > 0)
                return false;
            var r = Expansion.GridMiniFor(expandSeed, cycle, Expanded, Nodes);
            if (!r.HasValue)
                return false;
            var q = r.Value;
            q.Mini = (byte)MiniBossNow();
            Expanded.Add(q);
            DoLog("a mini-boss room waits at " + q.X + "," + q.Y);
            return true;
        }

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
                if (piece)
                    q = Expansion.ApplyPieceMod(q, Expansion.CardMod(card));
                else
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

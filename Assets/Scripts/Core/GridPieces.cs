using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    public static partial class Expansion
    {
        /// Pieces (Docs/GridPieces.md step 2): the hand holds Piece codes (Hand.cs). Codes from here up are pieces: PieceCodeBase + mod * 16 + the
        /// PieceShape, so any piece can carry one modifier (mod 0: a plain combat room). Below it the old room cards and the service cards
        /// (Shop, Market, Arms, Builder Shop), which in a Grid run build one cell.
        public const int PieceCodeBase = 32;

        /// A shape card's modifiers (PieceMods.cs, Docs/PieceMods.md): code = PieceCodeBase + shape + 16 * mods, where `mods` packs every modifier the card
        /// carries (each in its own bits; 0 = a plain empty room). Floor-only cards keep their old codes (floor 1-10 in the low bits).
        public static bool IsShapePieceCard(PieceCard c) => (int)c >= PieceCodeBase;
        public static PieceCard MakePieceCard(PieceShape p, PathId floor = PathId.None) => MakePieceCardMods(p, PieceMods.Floor.Set(0, (int)floor));
        public static PieceCard MakePieceCardMods(PieceShape p, int mods) => (PieceCard)(PieceCodeBase + (PieceMods.Clean(mods) << 4) + (int)p);
        public static PieceShape ShapeOfPieceCard(PieceCard c) => (PieceShape)(((int)c - PieceCodeBase) & 15);
        /// The modifiers a card carries (0 for a service card or a plain piece), with any undefined bits cleared.
        public static int PieceCardMods(PieceCard c) => IsShapePieceCard(c) ? PieceMods.Clean(((int)c - PieceCodeBase) >> 4) : 0;
        public static PathId PieceCardFloor(PieceCard c) => (PathId)PieceMods.Floor.Get(PieceCardMods(c));
        public static bool PieceCardCombat(PieceCard c) => PieceMods.Combat.Get(PieceCardMods(c)) != 0;
        public static bool PieceCardLoot(PieceCard c) => PieceMods.Loot.Get(PieceCardMods(c)) != 0;

        /// A card a Grid run can play: a piece, or a service card (one cell).
        public static bool IsGridPieceCard(PieceCard c) => IsShapePieceCard(c) ? (int)ShapeOfPieceCard(c) < pieceShapeBase.Length : IsServiceCard(c) || c == PieceCard.BuilderShop;

        /// The shape a Grid card builds: its piece, or a single cell for a service card.
        public static PieceShape PieceShapeOf(PieceCard c) => IsShapePieceCard(c) ? ShapeOfPieceCard(c) : PieceShape.Mono;

        /// A service card or the Builder Shop: a one-cell stall room that can't be built on a prize node (owner Nov 19).
        public static bool IsStallCard(PieceCard c) => IsServiceCard(c) || c == PieceCard.BuilderShop;

        /// One of these new rooms would enclose a chest, vector chest or relic node (opened or not: its fixture stays).
        public static bool CoversPrizeNode(IList<ExpansionRoom> made, IList<MapNode> nodes)
        {
            foreach (var n in nodes)
            {
                if (!CentresInRoom(n.Kind))
                    continue;
                foreach (var r in made)
                    if (NodeEncloses(r, n))
                        return true;
            }
            return false;
        }

        /// Every place this card can go: PlacementsFor its shape, less the prize nodes for a stall card.
        public static List<Placement> PlacementsForCard(IList<ExpansionRoom> built, IList<MapNode> nodes, PieceCard card)
        {
            var res = PlacementsFor(built, PieceShapeOf(card));
            if (IsStallCard(card))
                res.RemoveAll(o => CoversPrizeNode(o.Rooms, nodes));
            return res;
        }

        /// What the Builder Shop charges for a piece: 6 vectors a cell, plus each modifier's price (8 a modifier).
        public static int PieceCardPrice(PieceCard c) => PieceShapeCells(ShapeOfPieceCard(c)) * 6 + PieceMods.Price(PieceCardMods(c));

        public static string PieceCardName(PieceCard c)
        {
            var names = PieceMods.Names(PieceCardMods(c));
            names.Add(PieceShapeName(ShapeOfPieceCard(c)));
            return string.Join(" ", names);
        }

        public static string PieceCardBlurb(PieceCard c)
        {
            int n = PieceShapeCells(ShapeOfPieceCard(c));
            return (n == 1 ? "One cell" : n + " cells joined into one open room") + ", " + PieceMods.Blurb(PieceCardMods(c)) + ". Place it on a door spot; R turns it.";
        }

        static readonly PieceShape[] gridSmall = { PieceShape.Mono, PieceShape.Domino };
        static readonly PieceShape[] gridTri = { PieceShape.TriI, PieceShape.TriL };
        static readonly PieceShape[] gridTet = { PieceShape.TetI, PieceShape.TetO, PieceShape.TetT, PieceShape.TetS, PieceShape.TetL };

        static readonly PieceShape[] gridStart = { PieceShape.Mono, PieceShape.Domino, PieceShape.TriI, PieceShape.TriL, PieceShape.TetI, PieceShape.TetO, PieceShape.TetT, PieceShape.TetS, PieceShape.TetL };

        /// The starting hand: one seeded piece card (any 1 to 4 cell shape), its modifiers rolled like any offered card.
        public static PieceCard[] GridStartHand(int seed)
        {
            var rng = new DetRandom(seed);
            var shape = gridStart[rng.Int(gridStart.Length)];
            return new[] { MakePieceCardMods(shape, PieceMods.Roll(rng, 1)) };
        }

        /// The starting piece pick: two seeded piece cards of different shapes (any 1 to 4 cells), modifiers rolled; the first is GridStartHand's.
        public static PieceCard[] GridStartOffer(int seed)
        {
            var rng = new DetRandom(seed);
            var shape = gridStart[rng.Int(gridStart.Length)];
            var first = MakePieceCardMods(shape, PieceMods.Roll(rng, 1));
            var other = gridStart[(System.Array.IndexOf(gridStart, shape) + 1 + rng.Int(gridStart.Length - 1)) % gridStart.Length];
            return new[] { first, MakePieceCardMods(other, PieceMods.Roll(rng, 1)) };
        }

        /// The basic card each build round gives: a domino after odd waves, a single after even ones, its modifiers rolled.
        public static PieceCard GridRoundCard(DetRandom rng, int wave) => MakePieceCardMods(wave % 2 == 1 ? PieceShape.Domino : PieceShape.Mono, PieceMods.Roll(rng, wave));

        /// A dropped card (a wave drop, a chest, a boss pick): a service card 1 in ServiceCardChance times, else a piece (a single or domino 35%, a tromino 30%,
        /// a tetromino 35%) with its modifiers rolled (service cards carry none).
        public static PieceCard GridDropCard(DetRandom rng, int wave)
        {
            if (rng.Int(ServiceCardChance) == 0)
                return ServiceCards[rng.Int(ServiceCards.Length)];
            int roll = rng.Int(100);
            var set = roll < 35 ? gridSmall : roll < 65 ? gridTri : gridTet;
            var shape = set[rng.Int(set.Length)];
            return MakePieceCardMods(shape, PieceMods.Roll(rng, wave));
        }

        /// The Builder Shop's shelf in a Grid run: a single, a domino, a tromino, a tetromino and a modified tetromino (seeded by wave); every card rolls modifiers.
        public static PieceCard[] GridBuilderOffers(int seed, int wave)
        {
            var rng = new DetRandom(seed * 7919 + wave * 389 + 43);
            var tri = gridTri[rng.Int(gridTri.Length)];
            var tet = gridTet[rng.Int(gridTet.Length)];
            var tet2 = gridTet[rng.Int(gridTet.Length)];
            return new[]
            {
                MakePieceCardMods(PieceShape.Mono, PieceMods.Roll(rng, wave)), MakePieceCardMods(PieceShape.Domino, PieceMods.Roll(rng, wave)),
                MakePieceCardMods(tri, PieceMods.Roll(rng, wave)), MakePieceCardMods(tet, PieceMods.Roll(rng, wave)),
                MakePieceCardMods(tet2, PieceMods.Roll(rng, wave, true)),
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
            var start = Game.ArenaMid;
            var firsts = PlacementsFor(built, PieceShape.Mono);
            firsts.Sort((p, q) => q.Mid.DistanceTo(start).CompareTo(p.Mid.DistanceTo(start)));
            for (int tries = 0; tries < Math.Min(firsts.Count, 24); tries++)
            {
                var step = firsts[(rng.Int(Math.Max(1, firsts.Count / 2)) + tries) % firsts.Count];
                var map = new List<ExpansionRoom>(built);
                map.AddRange(step.Rooms);
                var outs = PlacementsFor(map, PieceShape.Domino);
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
            if (!IsHost || !ExpandingOn || Expansion.GridMinisWaiting(Expanded) > 0)
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

        /// This game's player plays the card in this hand slot as a piece: its orientation, with its cell box's corner at (x, y). Only in a Grid run's build round.
        public void PlayPieceCard(int slot, int orient, int x, int y)
        {
            if (!ExpandOpen || Me == null || !ExpandingOn)
                return;
            if (IsHost)
            {
                HostPlayPieceCard(MyId, slot, orient, x, y);
                return;
            }
            var w = Proto.Begin(Msg.PieceShape);
            w.Write((byte)slot); w.Write((byte)orient); w.Write((short)x); w.Write((short)y);
            session.SendToHost(Proto.End(w));
        }

        public void OnPieceShape(int peer, BinaryReader r) => HostPlayPieceCard(peer, r.ReadByte(), r.ReadByte(), r.ReadInt16(), r.ReadInt16());

        /// Host: the card in this slot is built at once as its piece if it fits there, and leaves the hand. Playing costs nothing; each modifier it carries is applied (a floor, enemies, ...), a service
        /// card's one cell a shop, Black Market, Arms Dealer or Builder Shop.
        void HostPlayPieceCard(int id, int slot, int orient, int x, int y)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !ExpandingOn || !Players.TryGetValue(id, out p) || slot < 0 || slot >= p.Hand.Length)
                return;
            var card = (PieceCard)p.Hand[slot];
            if (!Expansion.IsGridPieceCard(card))
                return;
            string why;
            var made = Expansion.PieceShapeRooms(Expanded, Expansion.PieceShapeOf(card), orient, x, y, out why);
            if (made == null)
            {
                DoLog("piece refused (" + why + ")");
                return;
            }
            if (Expansion.IsStallCard(card) && Expansion.CoversPrizeNode(made, Nodes))
            {
                DoLog("piece refused (a stall can't stand on a chest or relic node)");
                return;
            }
            bool piece = Expansion.IsShapePieceCard(card);
            int mods = Expansion.PieceCardMods(card);
            for (int i = 0; i < made.Count; i++)
            {
                var q = made[i];
                q.Owner = id;
                if (piece)
                    q = PieceMods.ApplyRoom(q, mods);
                else
                    q.Kind = Expansion.CardRoomKind(card);
                made[i] = q;
            }
            TakeCard(p, slot);
            DoLog(p.Name + " plays a " + Expansion.CardName(card) + " card at " + x + "," + y);
            BuildNow(id, made[0], made.GetRange(1, made.Count - 1));
            if (piece)
            {
                PieceMods.OnPlaced(this, made, mods);
                if (PieceMods.Combat.Get(mods) == 0)
                    PlantDormantGroups(made, false, null, 0, false, DormantGroups.PlainChance);  // (a plain piece holds a few sleepers too; owner Nov 20)
                if (PieceMods.Loot.Get(mods) == 0)  // (a Loot piece's chest is guaranteed, not rolled)
                    RollRoomChest(made, "piece");  // (owner Nov 20: a shape piece may hold a chest, modifiers or not; RoomChests.cs)
                PlantBonusLoot(made);  // (BonusLoot.cs: and a stash or two at its layout's loot slots)
            }  // (the room went out unseen in BuildNow's list: RoomFog.cs)
        }

        /// Bot: plays the first card in its hand that fits somewhere, at the spot nearest what it builds toward (Pieces.cs BotCardGoal), else the first spot. False if none fits.
        bool BotPlayPieceCard()
        {
            var me = Me;
            if (me == null)
                return false;
            V2? goal = BotCardGoal();
            for (int slot = 0; slot < me.Hand.Length; slot++)
            {
                var card = (PieceCard)me.Hand[slot];
                if (!Expansion.IsGridPieceCard(card))
                    continue;
                var options = Expansion.PlacementsForCard(Expanded, Nodes, card);
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
                PlayPieceCard(slot, pick.Orient, pick.X, pick.Y);
                return true;
            }
            return false;
        }
    }
}

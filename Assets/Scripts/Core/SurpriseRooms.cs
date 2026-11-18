using System;
using System.Collections.Generic;

namespace Arena
{
    /// The well-shaped rooms the game gives the Expanding arena for free (Docs/SurpriseRooms.md, step 1).
    public enum SurpriseShape : byte { Ring, Cross, LHall, THall, Chamfer, Stash, Tiny, Duel, Box, Hall, SmallRect, Medium, LongHall }  // (Tiny, SmallRect, Medium, Box, LongHall: mystery door rooms, MysteryDoors.cs; Duel, Box and Hall: room cards, RoomCards.cs)

    /// A spot for a surprise room: its boxes (Price 0), how many loops it closes and its score.
    public struct SurpriseOption
    {
        public List<ExpansionRoom> Rooms;
        public SurpriseShape Shape;
        public int Loops;      // existing rooms it joins besides its parent
        public bool DeadEnd;   // it touches a room with a single door
        public int Score;
        public int Price;      // what drawing it would cost (Expansion.Cost of its outline; a mystery door charges a share, MysteryDoors.cs)
        public int X, Y;       // the mask's top-left corner in world px, and its turn and mirror (SurpriseMask): enough to build it again (a room card, RoomCards.cs)
        public byte Turn;
        public bool Mirror;
    }

    public static partial class Expansion
    {
        public const int SurpriseTop = 3;  // the pick is seeded among this many best spots

        public static string SurpriseName(SurpriseShape s)
        {
            switch (s)
            {
                case SurpriseShape.Ring: return "Ring";
                case SurpriseShape.Cross: return "Cross";
                case SurpriseShape.LHall: return "L hall";
                case SurpriseShape.THall: return "T hall";
                case SurpriseShape.Stash: return "Stash";
                case SurpriseShape.Tiny: return "Small room";
                case SurpriseShape.SmallRect: return "Small room";
                case SurpriseShape.Medium: return "Medium room";
                case SurpriseShape.LongHall: return "Long hallway";
                case SurpriseShape.Duel: return "Duel room";
                case SurpriseShape.Box: return "Box";
                case SurpriseShape.Hall: return "Hall";
                default: return "Chamfer";
            }
        }

        /// A shape's floor in its base orientation (mask[col, row], true = floor; one tile is Step px). Every passage is at least
        /// 3 tiles (MinSide) wide, so each one is a legal outline.
        public static bool[,] SurpriseBase(SurpriseShape s)
        {
            int cols, rows;
            Func<int, int, bool> f;
            switch (s)
            {
                case SurpriseShape.Ring:  // 11 x 11, a 5 x 5 block in the middle: a 3 tile lane all round
                    cols = rows = 11; f = (c, r) => !(c >= 3 && c <= 7 && r >= 3 && r <= 7); break;
                case SurpriseShape.Cross:  // arms 3 wide, 15 across
                    cols = rows = 15; f = (c, r) => (c >= 6 && c <= 8) || (r >= 6 && r <= 8); break;
                case SurpriseShape.LHall:  // a 12 tile leg along the top, a 9 tile leg down the left
                    cols = 12; rows = 9; f = (c, r) => r <= 2 || c <= 2; break;
                case SurpriseShape.THall:  // a 15 tile bar with a 9 tile stem down the middle
                    cols = 15; rows = 9; f = (c, r) => r <= 2 || (c >= 6 && c <= 8); break;
                case SurpriseShape.Stash:  // 5 x 3: a small pocket behind a breach (Docs/BreachRooms.md)
                    cols = 5; rows = 3; f = (c, r) => true; break;
                case SurpriseShape.Tiny:  // 4 x 4: a mystery door's small square room (Expansion.ShopRoomSide)
                    cols = rows = 4; f = (c, r) => true; break;
                case SurpriseShape.SmallRect:  // 5 x 4: a mystery door's small rectangle
                    cols = 5; rows = 4; f = (c, r) => true; break;
                case SurpriseShape.Medium:  // 6 x 6: a mystery door's medium square (its medium rectangle is the Box)
                    cols = rows = 6; f = (c, r) => true; break;
                case SurpriseShape.LongHall:  // 12 x 3: a mystery door's long hallway, a chest at its far end
                    cols = 12; rows = 3; f = (c, r) => true; break;
                case SurpriseShape.Duel:  // 8 x 6: a mystery door's mini-boss room (the floating one's size)
                    cols = 8; rows = 6; f = (c, r) => true; break;
                case SurpriseShape.Box:  // 7 x 6: a room card's plain room
                    cols = 7; rows = 6; f = (c, r) => true; break;
                case SurpriseShape.Hall:  // 8 x 3: a room card's straight hallway
                    cols = 8; rows = 3; f = (c, r) => true; break;
                default:  // 9 x 7 with 2 x 2 corners notched out
                    cols = 9; rows = 7; f = (c, r) => !((c < 2 || c > 6) && (r < 2 || r > 4)); break;
            }
            var m = new bool[cols, rows];
            for (int c = 0; c < cols; c++)
                for (int r = 0; r < rows; r++)
                    m[c, r] = f(c, r);
            return m;
        }

        /// A mask mirrored left to right, then turned a quarter (clockwise) `turn` times.
        public static bool[,] SurpriseMask(SurpriseShape s, int turn, bool mirror) => TurnMask(SurpriseBase(s), turn, mirror);

        /// A tile mask mirrored (left to right) and then turned a quarter clockwise `turn` times.
        public static bool[,] TurnMask(bool[,] m, int turn, bool mirror)
        {
            if (mirror)
            {
                int cols = m.GetLength(0), rows = m.GetLength(1);
                var mm = new bool[cols, rows];
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rows; r++)
                        mm[cols - 1 - c, r] = m[c, r];
                m = mm;
            }
            for (int t = 0; t < (turn & 3); t++)
            {
                int cols = m.GetLength(0), rows = m.GetLength(1);
                var rot = new bool[rows, cols];
                for (int c = 0; c < cols; c++)
                    for (int r = 0; r < rows; r++)
                        rot[rows - 1 - r, c] = m[c, r];
                m = rot;
            }
            return m;
        }

        static ExpansionRoom Free(ExpansionRoom r, SurpriseShape shape)
        {
            r.Price = 0;
            r.Surprise = (byte)(1 + (int)shape);
            return r;
        }

    }
}

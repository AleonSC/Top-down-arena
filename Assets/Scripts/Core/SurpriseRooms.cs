using System;
using System.Collections.Generic;

namespace Arena
{
    /// The well-shaped rooms the game gives the Expanding arena for free (Docs/SurpriseRooms.md, step 1).
    public enum SurpriseShape : byte { Ring, Cross, LHall, THall, Chamfer, Stash }

    /// A spot for a surprise room: its boxes (Price 0), how many loops it closes and its score.
    public struct SurpriseOption
    {
        public List<ExpansionRoom> Rooms;
        public SurpriseShape Shape;
        public int Loops;      // existing rooms it joins besides its parent
        public bool DeadEnd;   // it touches a room with a single door
        public int Score;
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
        public static bool[,] SurpriseMask(SurpriseShape s, int turn, bool mirror)
        {
            var m = SurpriseBase(s);
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

        static string MaskKey(bool[,] m)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(m.GetLength(0)).Append('x').Append(m.GetLength(1)).Append(':');
            for (int r = 0; r < m.GetLength(1); r++)
                for (int c = 0; c < m.GetLength(0); c++)
                    sb.Append(m[c, r] ? '1' : '0');
            return sb.ToString();
        }

        /// Every spot this shape can go, best first. A spot is a turn and mirror of the shape set against a wall of a room on the map,
        /// legal by the outline rules (no overlap, 3 tiles of one wall shared, the size cap). Score: +3 for each loop it closes, +2 if it
        /// touches a dead end (a room with one door), -1 for each tile the map's outline (width + height) grows. No random numbers. With `through`,
        /// only spots that have floor on that point (a breach event's room behind the cracked wall).
        public static List<SurpriseOption> SurpriseOptions(IList<ExpansionRoom> built, SurpriseShape shape, V2? through = null)
        {
            var found = new List<SurpriseOption>();
            if (BoughtCount(built) >= MaxRooms)
                return found;
            var linked = ConnectedAreas(built);
            int mapX0 = 0, mapY0 = 0, mapX1 = (int)Game.Width, mapY1 = (int)Game.Height;
            foreach (var r in built)
            {
                mapX0 = Math.Min(mapX0, r.X); mapY0 = Math.Min(mapY0, r.Y); mapX1 = Math.Max(mapX1, r.X1); mapY1 = Math.Max(mapY1, r.Y1);
            }
            var deadEnd = new bool[built.Count + 1];
            for (int a = 0; a <= built.Count; a++)
                deadEnd[a] = Doors(built, a) == 1;
            var seenMask = new HashSet<string>();
            var seenSpot = new HashSet<string>();
            for (int turn = 0; turn < 4; turn++)
                for (int flip = 0; flip < 2; flip++)
                {
                    var mask = SurpriseMask(shape, turn, flip == 1);
                    if (!seenMask.Add(MaskKey(mask)))
                        continue;
                    int w = mask.GetLength(0) * Step, h = mask.GetLength(1) * Step;
                    for (int a = 0; a <= built.Count; a++)
                    {
                        if (!linked[a])
                            continue;
                        BoxOf(built, a, out int bx0, out int by0, out int bx1, out int by1);
                        for (int side = 0; side < 4; side++)  // 0 north, 1 east, 2 south, 3 west
                        {
                            bool across = side == 0 || side == 2;
                            int lo = across ? bx0 - w + MinDoorRun : by0 - h + MinDoorRun, hi = across ? bx1 - MinDoorRun : by1 - MinDoorRun;
                            for (int at = lo; at <= hi; at += Step)
                            {
                                int x = across ? at : side == 1 ? bx1 : bx0 - w;
                                int y = across ? (side == 0 ? by0 - h : by1) : at;
                                if (!seenSpot.Add(MaskKey(mask) + "@" + x + "," + y))
                                    continue;
                                if (through.HasValue && !(through.Value.X >= x && through.Value.X < x + w && through.Value.Y >= y && through.Value.Y < y + h))
                                    continue;
                                var made = FromTiles(built, mask, x, y, PathId.None, out string why);
                                if (made == null)
                                    continue;
                                if (through.HasValue && !made.Exists(q => through.Value.X >= q.X && through.Value.X < q.X1 && through.Value.Y >= q.Y && through.Value.Y < q.Y1))
                                    continue;
                                var opt = new SurpriseOption { Rooms = made, Shape = shape };
                                for (int p = 0; p < made.Count; p++)
                                    made[p] = Free(made[p], shape);
                                int touched = 0;
                                for (int b = 0; b <= built.Count; b++)
                                {
                                    BoxOf(built, b, out int cx0, out int cy0, out int cx1, out int cy1);
                                    foreach (var q in made)
                                        if (SharedRun(q.X, q.Y, q.X1, q.Y1, cx0, cy0, cx1, cy1) >= MinDoorRun)
                                        {
                                            touched++;
                                            opt.DeadEnd |= deadEnd[b];
                                            break;
                                        }
                                }
                                opt.Loops = Math.Max(0, touched - 1);
                                int nx0 = Math.Min(mapX0, x), ny0 = Math.Min(mapY0, y), nx1 = Math.Max(mapX1, x + w), ny1 = Math.Max(mapY1, y + h);
                                int grow = ((nx1 - nx0) + (ny1 - ny0) - (mapX1 - mapX0) - (mapY1 - mapY0)) / Step;
                                opt.Score = 3 * opt.Loops + (opt.DeadEnd ? 2 : 0) - grow;
                                found.Add(opt);
                            }
                        }
                    }
                }
            // Best first; equal scores keep the order they were found in (the sort is stable by index).
            var order = new List<int>();
            for (int i = 0; i < found.Count; i++) order.Add(i);
            order.Sort((i, j) => found[j].Score != found[i].Score ? found[j].Score.CompareTo(found[i].Score) : i.CompareTo(j));
            var sorted = new List<SurpriseOption>(found.Count);
            foreach (var i in order) sorted.Add(found[i]);
            return sorted;
        }

        static ExpansionRoom Free(ExpansionRoom r, SurpriseShape shape)
        {
            r.Price = 0;
            r.Surprise = (byte)(1 + (int)shape);
            return r;
        }

        /// The surprise room for this wave: the shape asked for if it fits, else the next (smaller) ones; one of the SurpriseTop best
        /// spots, picked from the seed, so every machine gets the same room. Null with `why` set if nothing fits (a full map).
        public static SurpriseOption? Surprise(int seed, int wave, IList<ExpansionRoom> built, SurpriseShape wanted, out string why, V2? through = null)
        {
            why = null;
            var tries = new List<SurpriseShape> { wanted };
            foreach (var s in new[] { SurpriseShape.Chamfer, SurpriseShape.THall, SurpriseShape.LHall, SurpriseShape.Cross, SurpriseShape.Ring })
                if (!tries.Contains(s))
                    tries.Add(s);
            // (the others, smallest first, are only tried when the wanted one has no legal spot)
            var rng = new DetRandom(seed * 7919 + wave * 104729 + built.Count + 13);
            foreach (var shape in tries)
            {
                var options = SurpriseOptions(built, shape, through);
                if (options.Count == 0)
                    continue;
                return options[rng.Int(Math.Min(SurpriseTop, options.Count))];
            }
            why = BoughtCount(built) >= MaxRooms ? "The map is full" : "No spot fits a surprise room";
            return null;
        }
    }
}

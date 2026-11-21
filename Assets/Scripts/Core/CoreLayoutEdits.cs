using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// The core's floor plans (`CoreLayouts`) edited in the game with the Cell Editor's CORE tab. An edit replaces a layout's `Pieces` (the quarter, or the half for a turned
    /// one; the rest is mirrored from it) and lives in CoreLayoutEdits.txt until Tools/CellEdits/fold.py writes it into CoreLayouts.cs. One line per layout: name, then its pieces
    /// ({kind,x,y,w,h} wall, {1,x,y,r} pillar, {2,x,y} crate, {3,x,y} heavy crate; comma separated, pieces separated by ;), tab separated.
    public static class CoreLayoutEdits
    {
        public const string FileName = "CoreLayoutEdits.txt";

        static int[][][] original;

        static int[][] Copy(int[][] a)
        {
            var c = new int[a.Length][];
            for (int i = 0; i < a.Length; i++)
                c[i] = (int[])a[i].Clone();
            return c;
        }

        static void Snapshot()
        {
            if (original != null)
                return;
            original = new int[CoreLayouts.All.Length][][];
            for (int i = 0; i < original.Length; i++)
                original[i] = Copy(CoreLayouts.All[i].Pieces);
        }

        public static int[][] CopyOf(int[][] pieces) => Copy(pieces);

        /// The layout's pieces as the code has them (a copy).
        public static int[][] Original(int index)
        {
            Snapshot();
            return Copy(original[index]);
        }

        public static string Line(string name, int[][] pieces)
        {
            var parts = new List<string>();
            foreach (var p in pieces)
                parts.Add(string.Join(",", Array.ConvertAll(p, v => v.ToString())));
            return name + "\t" + string.Join(";", parts);
        }

        public static bool IsEdited(int index)
        {
            Snapshot();
            return Line("", CoreLayouts.All[index].Pieces) != Line("", original[index]);
        }

        public static void Set(int index, int[][] pieces)
        {
            Snapshot();
            CoreLayouts.All[index].Pieces = Copy(pieces);
        }

        public static void Revert(int index)
        {
            Snapshot();
            Set(index, original[index]);
        }

        static bool Hits(int[] b, int[] d) => b[0] < d[2] && b[2] > d[0] && b[1] < d[3] && b[3] > d[1];

        static int[] BoxOf(int[] p) => p[0] == 0 ? new[] { p[1], p[2], p[1] + p[3], p[2] + p[4] } : p[0] == 1 ? new[] { p[1] - p[3], p[2] - p[3], p[1] + p[3], p[2] + p[3] } : new[] { p[1] - (p[0] == 3 ? 23 : 18), p[2] - (p[0] == 3 ? 23 : 18), p[1] + (p[0] == 3 ? 23 : 18), p[2] + (p[0] == 3 ? 23 : 18) };

        /// Every door spot's lane in the core's frame: x0, y0, x1, y1.
        public static List<int[]> DoorLanes()
        {
            const int W = CoreLayouts.W, H = CoreLayouts.H;
            var doors = new List<int[]>();
            foreach (int x in new[] { 300, 900, 1500, 2100 })
            {
                doors.Add(new[] { x - CoreLayouts.LaneHalf, 0, x + CoreLayouts.LaneHalf, CoreLayouts.LaneDeep });
                doors.Add(new[] { x - CoreLayouts.LaneHalf, H - CoreLayouts.LaneDeep, x + CoreLayouts.LaneHalf, H });
            }
            foreach (int y in new[] { 300, 900, 1500 })
            {
                doors.Add(new[] { 0, y - CoreLayouts.LaneHalf, CoreLayouts.LaneDeep, y + CoreLayouts.LaneHalf });
                doors.Add(new[] { W - CoreLayouts.LaneDeep, y - CoreLayouts.LaneHalf, W, y + CoreLayouts.LaneHalf });
            }
            return doors;
        }

        /// What breaks the core rules (the `corelayouts` test also checks): empty = fine. Counts pieces over the whole core, mirrors included.
        public static List<string> Problems(CoreLayout l, int[][] pieces)
        {
            const int W = CoreLayouts.W, H = CoreLayouts.H;
            var old = l.Pieces;
            l.Pieces = pieces;
            var all = CoreLayouts.Expanded(l.Id);
            l.Pieces = old;
            var doors = DoorLanes();
            int outside = 0, lane = 0, gate = 0;
            foreach (var p in all)
            {
                var b = BoxOf(p);
                if (b[0] < 14 || b[1] < 14 || b[2] > W - 14 || b[3] > H - 14) { outside++; continue; }
                if (doors.Exists(d => Hits(b, d))) { lane++; continue; }
                float nx = Math.Max(b[0], Math.Min(W / 2, b[2])), ny = Math.Max(b[1], Math.Min(H / 2, b[3]));
                if (Math.Sqrt((nx - W / 2) * (nx - W / 2) + (ny - H / 2) * (ny - H / 2)) < CoreLayouts.GateClear)
                    gate++;
            }
            var list = new List<string>();
            if (outside > 0) list.Add(outside + " pieces leave the core");
            if (lane > 0) list.Add(lane + " pieces are in a door lane");
            if (gate > 0) list.Add(gate + " pieces are on the gate's floor");
            return list;
        }

        // --- The file ----------------------------------------------------------------

        public static string Serialize()
        {
            Snapshot();
            var sb = new StringBuilder();
            sb.Append("#! Core floor plans edited in the game (F1 > LABS > CELL EDITOR > CORE). One line per layout: name, pieces (kind,x,y,w,h / 1,x,y,r / 2,x,y / 3,x,y) separated by ;, tab separated.\n");
            sb.Append("#! Write them into the code with: python3 Tools/CellEdits/fold.py\n");
            for (int i = 0; i < CoreLayouts.All.Length; i++)
                if (IsEdited(i))
                    sb.Append(Line(CoreLayouts.All[i].Name, CoreLayouts.All[i].Pieces)).Append('\n');
            return sb.ToString();
        }

        public static int Parse(string content)
        {
            Snapshot();
            int n = 0;
            foreach (var raw in content.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#!"))
                    continue;
                var f = line.Split('\t');
                if (f.Length != 2)
                    continue;
                int index = Array.FindIndex(CoreLayouts.All, l => l.Name == f[0]);
                if (index < 0)
                    continue;
                try
                {
                    var pieces = new List<int[]>();
                    if (f[1].Length > 0)
                        foreach (var item in f[1].Split(';'))
                        {
                            var parts = item.Split(',');
                            var v = new int[parts.Length];
                            for (int i = 0; i < v.Length; i++)
                                v[i] = int.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                            if (v.Length < 3)
                                throw new FormatException("too short");
                            pieces.Add(v);
                        }
                    Set(index, pieces.ToArray());
                    n++;
                }
                catch (FormatException)
                {
                }
            }
            return n;
        }

        public static int Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : 0;

        public static void Save(string path) => File.WriteAllText(path, Serialize());
    }
}

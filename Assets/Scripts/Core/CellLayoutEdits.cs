using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// Cell layouts edited in the game with the Cell Editor (F1 > LABS > CELL EDITOR). An edit replaces one entry of
    /// `CellLayouts.All` (same index and name, so every plan keeps pointing at it) and lives in CellLayoutEdits.txt, one line
    /// per edited layout, until Tools/CellEdits/fold.py writes it into CellLayouts.cs. Dev only: every machine in a game needs the
    /// same file, as a layout's walls are part of the room.
    public static class CellLayoutEdits
    {
        public const string FileName = "CellLayoutEdits.txt";

        static CellLayout[] original;

        /// Remember the layouts as the code has them (before the first edit lands), for `Original` and `IsEdited`.
        static void Snapshot()
        {
            if (original != null)
                return;
            original = new CellLayout[CellLayouts.All.Length];
            for (int i = 0; i < original.Length; i++)
                original[i] = Clone(CellLayouts.All[i]);
        }

        /// A deep copy.
        public static CellLayout Clone(CellLayout l) => new CellLayout
        {
            Name = l.Name,
            Walls = CopyOf(l.Walls),
            Sleepers = CopyOf(l.Sleepers),
            Traps = CopyOf(l.Traps),
            Loot = CopyOf(l.Loot),
            Pits = CopyOf(l.Pits),
        };

        static int[][] CopyOf(int[][] a)
        {
            var c = new int[a.Length][];
            for (int i = 0; i < a.Length; i++)
                c[i] = (int[])a[i].Clone();
            return c;
        }

        /// The layout as the code has it (a copy).
        public static CellLayout Original(int index)
        {
            Snapshot();
            return Clone(original[index]);
        }

        /// Does the layout differ from the code's?
        public static bool IsEdited(int index)
        {
            Snapshot();
            return Line(CellLayouts.All[index]) != Line(original[index]);
        }

        /// Put `l` in place of layout `index` (the turned copies are made again).
        public static void Set(int index, CellLayout l)
        {
            Snapshot();
            l = Clone(l);
            l.Name = CellLayouts.All[index].Name;
            l.Pits = CellLayouts.All[index].Pits;  // (the Cell Editor edits walls and slots; a plain layout has no pits)
            CellLayouts.All[index] = l;
            CellLayouts.ForgetTurned();
        }

        /// Put layout `index` back as the code has it.
        public static void Revert(int index)
        {
            Snapshot();
            Set(index, original[index]);
        }

        /// What is wrong with a layout (the room rules the cell layouts test also checks): empty = fine.
        public static List<string> Problems(CellLayout l)
        {
            var list = new List<string>();
            Func<float, float, float, bool> Hit = (x, y, pad) =>
            {
                foreach (var w in l.Walls)
                    if (x > w[0] - pad && x < w[0] + w[2] + pad && y > w[1] - pad && y < w[1] + w[3] + pad)
                        return true;
                return false;
            };
            if (Hit(300, 300, 40))
                list.Add("a wall touches the chest in the middle");
            if (Hit(300, 0, 50) || Hit(300, 600, 50) || Hit(0, 300, 50) || Hit(600, 300, 50))
                list.Add("a wall blocks a door lane (the middle of a side)");
            foreach (var w in l.Walls)
                if (CellLayouts.InDoorway(w))
                {
                    list.Add("a wall reaches into a doorway (keep it flush with the door's edge)");
                    break;
                }
            foreach (var w in l.Walls)
                if (w[0] < 0 || w[1] < 0 || w[0] + w[2] > CellLayouts.Cell || w[1] + w[3] > CellLayouts.Cell)
                {
                    list.Add("a wall leaves the cell");
                    break;
                }
            foreach (var s in l.Sleepers)
                if (Hit(s[0], s[1], 24))
                {
                    list.Add("a sleeper spot is inside a wall");
                    break;
                }
            foreach (var t in l.Traps)
                if (t[2] == 0 && Hit(t[0], t[1], 20))
                {
                    list.Add("a trap spot is inside a wall");
                    break;
                }
            foreach (var p in l.Loot)
                if (Hit(p[0], p[1], 20) || Math.Abs(p[0] - 300) + Math.Abs(p[1] - 300) < 120)
                {
                    list.Add("a loot spot is inside a wall or too near the middle");
                    break;
                }
            if (l.Sleepers.Length == 0) list.Add("no sleeper spot");
            if (l.Traps.Length == 0) list.Add("no trap spot");
            if (l.Loot.Length == 0) list.Add("no loot spot");
            return list;
        }

        // --- The file ----------------------------------------------------------------

        static string Join(int[][] a)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < a.Length; i++)
            {
                if (i > 0)
                    sb.Append(';');
                sb.Append(string.Join(",", Array.ConvertAll(a[i], v => v.ToString())));
            }
            return sb.ToString();
        }

        /// One layout as a line: name, then walls, sleepers, traps and loot (`x,y,w,h;...`), tab separated.
        public static string Line(CellLayout l) => l.Name + "\t" + Join(l.Walls) + "\t" + Join(l.Sleepers) + "\t" + Join(l.Traps) + "\t" + Join(l.Loot);

        internal static int[][] Split(string s, int n)
        {
            var list = new List<int[]>();
            if (s.Length == 0)
                return list.ToArray();
            foreach (var item in s.Split(';'))
            {
                var parts = item.Split(',');
                if (parts.Length != n)
                    throw new FormatException("expected " + n + " numbers in '" + item + "'");
                var v = new int[n];
                for (int i = 0; i < n; i++)
                    v[i] = int.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                list.Add(v);
            }
            return list.ToArray();
        }

        /// Every layout that differs from the code's, one line each.
        public static string Serialize()
        {
            Snapshot();
            var sb = new StringBuilder();
            sb.Append("#! Cell layouts edited in the game (F1 > LABS > CELL EDITOR). One line per layout: name, walls (x,y,w,h), sleepers (x,y), traps (x,y,wall), loot (x,y), tab separated.\n");
            sb.Append("#! Write them into the code with: python3 Tools/CellEdits/fold.py\n");
            for (int i = 0; i < CellLayouts.All.Length; i++)
                if (IsEdited(i))
                    sb.Append(Line(CellLayouts.All[i])).Append('\n');
            return sb.ToString();
        }

        /// Read edits (lines of a name that matches a layout) and apply them; returns how many landed.
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
                if (f.Length != 5)
                    continue;
                int index = Array.FindIndex(CellLayouts.All, l => l.Name == f[0]);
                if (index < 0)
                    continue;
                try
                {
                    Set(index, new CellLayout { Name = f[0], Walls = Split(f[1], 4), Sleepers = Split(f[2], 2), Traps = Split(f[3], 3), Loot = Split(f[4], 2) });
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// What the Cell Editor asks of the code, in CellTemplateRequests.txt: your own templates for the code to add (`NEW`, one per
    /// line like a layout line) and the built-in layouts you marked for deletion (`DELETE`, by name). Nothing here changes the
    /// game: layout indexes stay put until the code adds or removes them.
    public static class CellTemplateRequests
    {
        public const string FileName = "CellTemplateRequests.txt";

        public static readonly List<CellLayout> Mine = new List<CellLayout>();
        public static readonly HashSet<string> Deletes = new HashSet<string>();

        /// A new template with no walls or spots.
        public static CellLayout Blank()
        {
            int n = 1;
            while (Mine.Exists(l => l.Name == "Mine " + n))
                n++;
            var t = new CellLayout { Name = "Mine " + n, Walls = new int[0][], Sleepers = new int[0][], Traps = new int[0][], Loot = new int[0][], Pits = new int[0][] };
            Mine.Add(t);
            return t;
        }

        public static string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("#! Cell Editor requests. NEW: your own template (name, walls, sleepers, traps, loot) to add; DELETE: a layout to remove.\n");
            foreach (var l in Mine)
                sb.Append("NEW\t").Append(CellLayoutEdits.Line(l)).Append('\n');
            foreach (var d in Deletes)
                sb.Append("DELETE\t").Append(d).Append('\n');
            return sb.ToString();
        }

        public static int Parse(string content)
        {
            Mine.Clear();
            Deletes.Clear();
            int n = 0;
            foreach (var raw in content.Split('\n'))
            {
                var f = raw.TrimEnd('\r').Split('\t');
                try
                {
                    if (f[0] == "DELETE" && f.Length == 2)
                        Deletes.Add(f[1]);
                    else if (f[0] == "NEW" && f.Length == 6)
                        Mine.Add(new CellLayout { Name = f[1], Walls = CellLayoutEdits.Split(f[2], 4), Sleepers = CellLayoutEdits.Split(f[3], 2), Traps = CellLayoutEdits.Split(f[4], 3), Loot = CellLayoutEdits.Split(f[5], 2), Pits = new int[0][] });
                    else
                        continue;
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

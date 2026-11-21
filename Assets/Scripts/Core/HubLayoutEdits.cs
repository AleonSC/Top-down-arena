using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// One hub floor plan's walls and crates as the Cell Editor's Hub tab edits them (the 1600 x 900 hub; not the edge wall).
    public sealed class HubLayoutData
    {
        public LayoutId Id;
        public float[][] Boxes;    // each: x, y, w, h (top-left)
        public float[][] Pillars;  // each: x, y, r (the middle)
        public float[][] Crates;   // each: x, y, heavy (1: a heavy crate)
    }

    /// Hub floor plans (Layouts.cs) edited in the game, like `CellLayoutEdits`: an edit replaces a plan's walls and crates and lives in
    /// HubLayoutEdits.txt until Tools/CellEdits/fold.py writes it into HubLayouts.Generated.cs (which `Layouts` reads as it builds). One line per
    /// layout: name, boxes (x,y,w,h), pillars (x,y,r), crates (x,y,heavy), tab separated.
    public static class HubLayoutEdits
    {
        public const string FileName = "HubLayoutEdits.txt";

        static Dictionary<string, HubLayoutData> committed;
        static HubLayoutData[] original;

        public static IEnumerable<LayoutId> Ids()
        {
            foreach (var d in Layouts.Playable())
                yield return d.Id;
        }

        static float[][] Copy(float[][] a)
        {
            var c = new float[a.Length][];
            for (int i = 0; i < a.Length; i++)
                c[i] = (float[])a[i].Clone();
            return c;
        }

        public static HubLayoutData Clone(HubLayoutData d) => new HubLayoutData { Id = d.Id, Boxes = Copy(d.Boxes), Pillars = Copy(d.Pillars), Crates = Copy(d.Crates) };

        /// The plan as it is now.
        public static HubLayoutData Current(LayoutId id)
        {
            List<Solid> solids;
            List<LayoutCrate> crates;
            Layouts.PartsOf(Layouts.Get(id), out solids, out crates);
            var boxes = new List<float[]>();
            var pillars = new List<float[]>();
            foreach (var s in solids)
                if (s.Round)
                    pillars.Add(new[] { s.Pos.X, s.Pos.Y, s.R });
                else
                    boxes.Add(new[] { s.Pos.X - s.HW, s.Pos.Y - s.HH, s.HW * 2f, s.HH * 2f });
            var cr = new List<float[]>();
            foreach (var c in crates)
                cr.Add(new[] { c.Pos.X, c.Pos.Y, c.Heavy ? 1f : 0f });
            return new HubLayoutData { Id = id, Boxes = boxes.ToArray(), Pillars = pillars.ToArray(), Crates = cr.ToArray() };
        }

        static void Snapshot()
        {
            if (original != null)
                return;
            original = new HubLayoutData[Enum.GetValues(typeof(LayoutId)).Length];
            foreach (var id in Ids())
                original[(int)id] = Current(id);
        }

        /// The plan as the code has it (a copy).
        public static HubLayoutData Original(LayoutId id)
        {
            Snapshot();
            return Clone(original[(int)id]);
        }

        public static bool IsEdited(LayoutId id)
        {
            Snapshot();
            return Line(Current(id)) != Line(original[(int)id]);
        }

        public static void Set(LayoutId id, HubLayoutData d)
        {
            Snapshot();
            var solids = new List<Solid>();
            foreach (var b in d.Boxes)
                solids.Add(new Solid { Pos = new V2(b[0] + b[2] / 2f, b[1] + b[3] / 2f), HW = b[2] / 2f, HH = b[3] / 2f });
            foreach (var p in d.Pillars)
                solids.Add(new Solid { Round = true, Pos = new V2(p[0], p[1]), R = p[2] });
            var crates = new List<LayoutCrate>();
            foreach (var c in d.Crates)
                crates.Add(new LayoutCrate { Pos = new V2(c[0], c[1]), Heavy = c[2] > 0.5f });
            Layouts.Replace(id, solids, crates);
        }

        public static void Revert(LayoutId id)
        {
            Snapshot();
            Set(id, original[(int)id]);
        }

        // --- The file ----------------------------------------------------------------

        static string F(float v) => Math.Round(v, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);

        static string Join(float[][] a)
        {
            var parts = new List<string>();
            foreach (var r in a)
                parts.Add(string.Join(",", Array.ConvertAll(r, F)));
            return string.Join(";", parts);
        }

        public static string Line(HubLayoutData d) => Layouts.Get(d.Id).Name + "\t" + Join(d.Boxes) + "\t" + Join(d.Pillars) + "\t" + Join(d.Crates);

        static float[][] Split(string s, int n)
        {
            var list = new List<float[]>();
            if (s.Length == 0)
                return list.ToArray();
            foreach (var item in s.Split(';'))
            {
                var parts = item.Split(',');
                if (parts.Length != n)
                    throw new FormatException("expected " + n + " numbers in '" + item + "'");
                var v = new float[n];
                for (int i = 0; i < n; i++)
                    v[i] = float.Parse(parts[i].Trim(), System.Globalization.CultureInfo.InvariantCulture);
                list.Add(v);
            }
            return list.ToArray();
        }

        static Dictionary<string, HubLayoutData> ParseLines(string content)
        {
            var map = new Dictionary<string, HubLayoutData>();
            foreach (var raw in content.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#!"))
                    continue;
                var f = line.Split('\t');
                if (f.Length != 4)
                    continue;
                try
                {
                    map[f[0]] = new HubLayoutData { Boxes = Split(f[1], 4), Pillars = Split(f[2], 3), Crates = Split(f[3], 3) };
                }
                catch (FormatException)
                {
                }
            }
            return map;
        }

        /// Layouts.cs, as it builds a plan: swap in the folded edit for this plan, if there is one (it only reads `HubLayoutsGenerated`, so it can run while `Layouts` starts up).
        internal static void Committed(LayoutId id, ref List<Solid> solids, ref List<LayoutCrate> crates)
        {
            if (HubLayoutsGenerated.Data.Length == 0)
                return;
            if (committed == null)
                committed = ParseLines(HubLayoutsGenerated.Data);
            string name = NameOf(id);
            HubLayoutData d;
            if (name == null || !committed.TryGetValue(name, out d))
                return;
            solids = new List<Solid>();
            foreach (var b in d.Boxes)
                solids.Add(new Solid { Pos = new V2(b[0] + b[2] / 2f, b[1] + b[3] / 2f), HW = b[2] / 2f, HH = b[3] / 2f });
            foreach (var p in d.Pillars)
                solids.Add(new Solid { Round = true, Pos = new V2(p[0], p[1]), R = p[2] });
            crates = new List<LayoutCrate>();
            foreach (var c in d.Crates)
                crates.Add(new LayoutCrate { Pos = new V2(c[0], c[1]), Heavy = c[2] > 0.5f });
        }

        // (the names are fixed by Layouts.Build: needed here without asking `Layouts`, which is still starting up)
        static string NameOf(LayoutId id)
        {
            switch (id)
            {
                case LayoutId.PillarRing: return "Pillar Ring";
                case LayoutId.TwinLanes: return "Twin Lanes";
                case LayoutId.Cross: return "Cross";
                case LayoutId.Islands: return "Islands";
                case LayoutId.Funnels: return "Funnels";
                case LayoutId.Bunker: return "Bunker";
                default: return null;
            }
        }

        public static string Serialize()
        {
            Snapshot();
            var sb = new StringBuilder();
            sb.Append("#! Hub floor plans edited in the game (F1 > LABS > CELL EDITOR > HUB). One line per layout: name, boxes (x,y,w,h), pillars (x,y,r), crates (x,y,heavy), tab separated.\n");
            sb.Append("#! Write them into the code with: python3 Tools/CellEdits/fold.py\n");
            foreach (var id in Ids())
                if (IsEdited(id))
                    sb.Append(Line(Current(id))).Append('\n');
            return sb.ToString();
        }

        public static int Parse(string content)
        {
            Snapshot();
            int n = 0;
            foreach (var kv in ParseLines(content))
                foreach (var id in Ids())
                    if (Layouts.Get(id).Name == kv.Key)
                    {
                        kv.Value.Id = id;
                        Set(id, kv.Value);
                        n++;
                    }
            return n;
        }

        public static int Load(string path) => File.Exists(path) ? Parse(File.ReadAllText(path)) : 0;

        public static void Save(string path) => File.WriteAllText(path, Serialize());
    }
}

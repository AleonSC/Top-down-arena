using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Arena
{
    /// One wave of a practice run: which enemies, how many, and how strong (the wave level their
    /// health and fire rate scale with).
    public class PracticeWave
    {
        public int Level = 1;
        public readonly Dictionary<EnemyType, int> Counts = new Dictionary<EnemyType, int>();

        public int Total => Counts.Values.Sum();

        public PracticeWave Copy()
        {
            var w = new PracticeWave { Level = Level };
            foreach (var kv in Counts)
                w.Counts[kv.Key] = kv.Value;
            return w;
        }
    }

    /// A practice range setup: any number of hand-built waves. Saves to (and loads from) a short text file.
    public class PracticePlan
    {
        public readonly List<PracticeWave> Waves = new List<PracticeWave>();
        public bool Loop;

        /// Everything you can put in a practice wave, in the order the picker shows them: each EnemyDef's Picker group,
        /// then EnemyType order. (Boss summons like Gravity Wells and Hive Bombers are Hidden: they come with their boss.)
        public static readonly EnemyType[] Choices = ((EnemyType[])Enum.GetValues(typeof(EnemyType)))
            .Where(t => Defs.Get(t).Picker != PickerGroup.Hidden).OrderBy(t => Defs.Get(t).Picker).ThenBy(t => (int)t).ToArray();

        public PracticePlan Copy()
        {
            var p = new PracticePlan { Loop = Loop };
            foreach (var w in Waves)
                p.Waves.Add(w.Copy());
            return p;
        }

        public static string NameOf(EnemyType t) => t == EnemyType.TwinA ? "Twin Reapers" : Defs.Get(t).Name;

        /// One line on what each enemy does (shown when you hover it in the Practice Range).
        public static string BlurbOf(EnemyType t) => Defs.Get(t).Blurb ?? "";

        public static PracticePlan Default()
        {
            var p = new PracticePlan();
            var w = new PracticeWave { Level = 1 };
            w.Counts[EnemyType.Grunt] = 5;
            p.Waves.Add(w);
            return p;
        }

        // --- Text format ---------------------------------------------------------------
        //   # comments
        //   loop: yes
        //   wave 5: The Warden x1, Grunt x4
        // One line per wave: its level, then each enemy and how many.

        public string ToText(string title = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("# Top-Down Arena practice setup" + (string.IsNullOrEmpty(title) ? "" : ": " + title));
            sb.AppendLine("# Load it in the lobby (Practice Range > Load). One line per wave: \"wave <level>: <enemy> x<count>, ...\".");
            sb.AppendLine("# The level sets how strong the enemies are (their health and fire rate, as on that wave of a run).");
            sb.AppendLine("loop: " + (Loop ? "yes" : "no"));
            foreach (var w in Waves)
            {
                var parts = Choices.Where(t => w.Counts.ContainsKey(t) && w.Counts[t] > 0).Select(t => NameOf(t) + " x" + w.Counts[t]);
                sb.AppendLine("wave " + w.Level + ": " + string.Join(", ", parts));
            }
            return sb.ToString();
        }

        /// Reads a saved setup. Unknown enemies and bad lines are skipped (listed in `problems`).
        public static PracticePlan Parse(string text, out List<string> problems)
        {
            problems = new List<string>();
            var plan = new PracticePlan();
            foreach (var raw in (text ?? "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#"))
                    continue;
                string lower = line.ToLowerInvariant();
                if (lower.StartsWith("loop:"))
                {
                    string v = lower.Substring(5).Trim();
                    plan.Loop = v == "yes" || v == "true" || v == "on" || v == "1";
                    continue;
                }
                if (!lower.StartsWith("wave"))
                {
                    problems.Add("skipped: " + line);
                    continue;
                }
                int colon = line.IndexOf(':');
                var w = new PracticeWave();
                string head = colon >= 0 ? line.Substring(4, colon - 4).Trim() : "";
                int level;
                if (int.TryParse(head, NumberStyles.Integer, CultureInfo.InvariantCulture, out level))
                    w.Level = Math.Max(1, Math.Min(Game.MaxWave, level));
                string body = colon >= 0 ? line.Substring(colon + 1) : "";
                foreach (var item in body.Split(','))
                {
                    string it = item.Trim();
                    if (it.Length == 0)
                        continue;
                    int count = 1;
                    int x = it.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
                    string name = it;
                    if (x >= 0 && int.TryParse(it.Substring(x + 2).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out count))
                        name = it.Substring(0, x).Trim();
                    var type = Find(name);
                    if (type == null)
                    {
                        problems.Add("unknown enemy: " + name);
                        continue;
                    }
                    int had;
                    w.Counts.TryGetValue(type.Value, out had);
                    w.Counts[type.Value] = Math.Max(0, Math.Min(99, had + count));
                }
                plan.Waves.Add(w);
            }
            if (plan.Waves.Count == 0)
                problems.Add("no waves found");
            return plan;
        }

        static EnemyType? Find(string name)
        {
            foreach (var t in Choices)
                if (string.Equals(NameOf(t), name, StringComparison.OrdinalIgnoreCase) || string.Equals(t.ToString(), name, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Defs.Get(t).Name.Replace("The ", ""), name.Replace("The ", ""), StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }
    }

    public partial class Game
    {
        /// Host: a practice range run (null in a normal run). Waves come from the plan instead of the usual mix.
        public PracticePlan Practice;
        public int PracticeIndex = -1;  // the practice wave being fought (or just cleared)

        void BuildPracticeWave(PracticeWave w)
        {
            spawnQueue.Clear();
            bossRewarded = false;
            var rest = new List<EnemyType>();
            foreach (var t in PracticePlan.Choices)
            {
                int n;
                if (!w.Counts.TryGetValue(t, out n))
                    continue;
                for (int i = 0; i < n; i++)
                {
                    if (t == EnemyType.TwinA)
                    {
                        spawnQueue.Add(EnemyType.TwinA);  // the Reapers come as a pair
                        spawnQueue.Add(EnemyType.TwinB);
                    }
                    else if (Defs.Get(t).IsBoss)
                        spawnQueue.Add(t);  // bosses first
                    else
                        rest.Add(t);
                }
            }
            for (int i = rest.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                var tmp = rest[i];
                rest[i] = rest[j];
                rest[j] = tmp;
            }
            spawnQueue.AddRange(rest);
        }
    }
}

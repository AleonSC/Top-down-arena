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

        /// Everything you can put in a practice wave, in the order the picker shows them.
        /// (Boss summons like Gravity Wells and Hive Bombers come with their boss.)
        public static readonly EnemyType[] Choices =
        {
            EnemyType.Swarmer, EnemyType.Brute, EnemyType.Lunger, EnemyType.Grunt, EnemyType.Burster, EnemyType.Sniper, EnemyType.Spinner,
            EnemyType.Charger, EnemyType.Splitter, EnemyType.Shard, EnemyType.Mortar, EnemyType.Shielder, EnemyType.Medic,
            EnemyType.Phantom, EnemyType.Hexer, EnemyType.Juggernaut, EnemyType.Nest,
            EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus, EnemyType.Duelist,
            EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
        };

        public PracticePlan Copy()
        {
            var p = new PracticePlan { Loop = Loop };
            foreach (var w in Waves)
                p.Waves.Add(w.Copy());
            return p;
        }

        public static string NameOf(EnemyType t) => t == EnemyType.TwinA ? "Twin Reapers" : Defs.Get(t).Name;

        /// One line on what each enemy does (shown when you hover it in the Practice Range).
        public static string BlurbOf(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Swarmer: return "Small and fast melee rusher that weaves toward you in pairs.";
                case EnemyType.Brute: return "Slow, tough melee bruiser that hits hard (24) when it reaches you.";
                case EnemyType.Lunger: return "Circles in close, flashes a pink line, then leaps along it.";
                case EnemyType.Grunt: return "Fires aimed 3-shot spreads.";
                case EnemyType.Burster: return "Fires rings of bullets; more in each ring on later waves.";
                case EnemyType.Sniper: return "Fast, accurate shots from far away.";
                case EnemyType.Spinner: return "Wanders and sprays spirals in bursts; it spins up while bursting.";
                case EnemyType.Charger: return "Flashes a red line, then dashes along it and bursts into a ring.";
                case EnemyType.Splitter: return "Splits into 3 fast Shards when killed.";
                case EnemyType.Shard: return "Tiny and fast. Comes from Splitters, Nests and the Hive Queen.";
                case EnemyType.Mortar: return "Lobs bombs at where you stand; they burst into a ring on landing.";
                case EnemyType.Shielder: return "A front shield blocks shots and swings. It turns slowly: flank it.";
                case EnemyType.Medic: return "Heals nearby enemies every few seconds. Kill it first.";
                case EnemyType.Phantom: return "Flickers, blinks next to you and bursts into a ring.";
                case EnemyType.Hexer: return "Fires curving pincer shots that close in from both sides.";
                case EnemyType.Juggernaut: return "Armored and slow, can't be slowed or knocked back. Rotating stars of orbs.";
                case EnemyType.Nest: return "Stationary spawner that keeps hatching Shards until destroyed.";
                case EnemyType.Boss: return "Boss (wave 5): spirals, orb rings, aimed fans and cross streams.";
                case EnemyType.HiveQueen: return "Boss (wave 10): immune behind shooting drones, then bombers and fans.";
                case EnemyType.TwinA: return "Boss pair (wave 15): Soul Bond, a sweeping tether, and an enrage.";
                case EnemyType.Singularity: return "Boss (wave 20): corner gravity wells, pull, lasers and shrinking cages.";
                case EnemyType.Colossus: return "Boss (wave 25): huge stomp rings with one gap, and thrown rocks.";
                case EnemyType.Duelist: return "Boss (wave 30): teleports and leaves decoys that burst unless killed.";
                case EnemyType.Lich: return "Boss (wave 35): immune while its phylactery stands; raises skeletons.";
                case EnemyType.StormHerald: return "Boss (wave 40): lightning strikes where you stand. Keep moving.";
                case EnemyType.Devourer: return "Boss (wave 45): hungry for 3 s of every 10, when your shots heal it. Hold fire.";
                case EnemyType.Eclipse: return "Final boss (wave 50): spirals and rings, then wells and phantoms, then storms.";
                default: return "";
            }
        }

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

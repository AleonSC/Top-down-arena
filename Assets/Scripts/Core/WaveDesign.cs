using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Arena
{
    /// How a designed group's enemies come in at their spawn point.
    public enum ArriveMode : byte
    {
        OneByOne,  // one after another, `Every` seconds apart
        Together,  // all at once, bunched round the point
        Line,      // all at once, side by side across the point, facing the middle
        Ring,      // all at once, in a ring round a player (the point is ignored)
    }

    /// A group of enemies on a designed wave's timeline: who, from where, when, and how they arrive.
    /// A squad group (Squad set) brings that squad as the game knows it (its members and formation, e.g. the Riot line).
    public class DesignGroup
    {
        public string Name = "Group";
        public readonly List<EnemyType> Members = new List<EnemyType>();
        public string Squad;                 // a squad's name (Variety.Squads or a boss faction's), or null
        public string From = "A";            // spawn point letters; with several, each enemy takes the next in turn
        public float At, Every = 0.4f;       // seconds into the wave; seconds between enemies (one by one)
        public ArriveMode Arrive = ArriveMode.OneByOne;

        /// The squad this group brings, if it's a squad group.
        public SquadDef SquadDef => Squad == null ? null : WaveSet.FindSquad(Squad);

        /// Who actually arrives (a squad's members, or the group's).
        public IList<EnemyType> Arrivals => SquadDef != null ? (IList<EnemyType>)SquadDef.Members : Members;

        /// Seconds from its first arrival to its last.
        public float Span => Squad == null && Arrive == ArriveMode.OneByOne ? Every * Math.Max(0, Members.Count - 1) : 0f;

        public DesignGroup Copy()
        {
            var g = new DesignGroup { Name = Name, Squad = Squad, From = From, At = At, Every = Every, Arrive = Arrive };
            g.Members.AddRange(Members);
            return g;
        }
    }

    public class DesignProp
    {
        public PropKind Kind;
        public V2 Pos;
    }

    /// One designed wave: its strength (the level, as a run's wave number), its groups and the props on the floor.
    public class DesignWave
    {
        public int Level = 1;
        public readonly List<DesignGroup> Groups = new List<DesignGroup>();
        public readonly List<DesignProp> Props = new List<DesignProp>();

        public int EnemyCount => Groups.Sum(g => g.Arrivals.Count);
        public float Length => Groups.Count == 0 ? 0f : Groups.Max(g => g.At + g.Span);

        public DesignWave Copy()
        {
            var w = new DesignWave { Level = Level };
            foreach (var g in Groups)
                w.Groups.Add(g.Copy());
            foreach (var p in Props)
                w.Props.Add(new DesignProp { Kind = p.Kind, Pos = p.Pos });
            return w;
        }
    }

    /// A set of designed waves (the F1 wave editor's file, Game/WaveEditorUi.cs): spawn points A-E (moveable), then the
    /// waves. Saved as a short text file (.waves) that reads like this:
    ///   # Top-Down Arena waves: warden drills
    ///   point A 800,60
    ///   wave 6 level 6
    ///     group Breakout rush | Rioter x6, Guard | from B | at 0 | every 0.4 | one by one
    ///     group Riot line | squad Riot line | from A | at 11
    ///     prop crate 610,330
    /// Played in the Practice Range (Game.Design): each wave's groups arrive on their own timers and its props are laid out.
    public class WaveSet
    {
        public const string Letters = "ABCDE";
        public string Name = "untitled";
        public readonly V2[] Points =
        {
            new V2(800f, 70f), new V2(70f, 450f), new V2(110f, 110f), new V2(1490f, 790f), new V2(1530f, 450f),
        };
        public readonly List<DesignWave> Waves = new List<DesignWave>();
        public bool Loop;

        public V2 Point(char letter)
        {
            int i = Letters.IndexOf(char.ToUpperInvariant(letter));
            return Points[i < 0 ? 0 : i];
        }

        public WaveSet Copy()
        {
            var s = new WaveSet { Name = Name, Loop = Loop };
            Array.Copy(Points, s.Points, Points.Length);
            foreach (var w in Waves)
                s.Waves.Add(w.Copy());
            return s;
        }

        /// A new set: one wave, with a first group, so the editor never opens empty.
        public static WaveSet Starter()
        {
            var s = new WaveSet();
            var w = new DesignWave { Level = 3 };
            var g = new DesignGroup { Name = "Opening rush", From = "B", At = 0f, Every = 0.4f };
            for (int i = 0; i < 6; i++)
                g.Members.Add(EnemyType.Swarmer);
            w.Groups.Add(g);
            w.Groups.Add(new DesignGroup { Name = "Firing line", Squad = "Firing line", From = "A", At = 5f });
            w.Props.Add(new DesignProp { Kind = PropKind.Crate, Pos = new V2(620f, 330f) });
            w.Props.Add(new DesignProp { Kind = PropKind.Crate, Pos = new V2(980f, 570f) });
            s.Waves.Add(w);
            return s;
        }

        /// Every squad a group can bring: the regular ones, then each boss faction's (and its set piece).
        public static IEnumerable<SquadDef> AllSquads()
        {
            foreach (var f in Approaches.All)
            {
                foreach (var q in f.Squads)
                    yield return q;
                if (f.Finale != null)
                    yield return f.Finale;
            }
            foreach (var q in Game.Squads)
                yield return q;
        }

        /// The Practice Range plan that goes with it (each wave's level and enemy counts, for the HUD and the lobby).
        public PracticePlan ToPlan()
        {
            var p = new PracticePlan { Loop = Loop };
            foreach (var w in Waves)
            {
                var pw = new PracticeWave { Level = w.Level };
                foreach (var g in w.Groups)
                    foreach (var t in g.Arrivals)
                    {
                        int n;
                        pw.Counts.TryGetValue(t, out n);
                        pw.Counts[t] = n + 1;
                    }
                p.Waves.Add(pw);
            }
            return p;
        }

        public static SquadDef FindSquad(string name) => AllSquads().FirstOrDefault(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase));

        /// An enemy by its name as the game shows it (or its code name), ignoring case and "The".
        public static EnemyType? FindEnemy(string name)
        {
            string n = name.Trim().Replace("The ", "").Replace("the ", "");
            foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
                if (string.Equals(t.ToString(), n, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(Defs.Get(t).Name.Replace("The ", ""), n, StringComparison.OrdinalIgnoreCase))
                    return t;
            return null;
        }

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static float Fl(string s) => float.Parse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture);

        static readonly string[] ArriveNames = { "one by one", "together", "line", "ring" };
        public static string ArriveName(ArriveMode a) => ArriveNames[(int)a];

        static readonly string[] PropNames = { "crate", "heavy crate", "pot", "barrel", "chest", "health barrel" };
        public static string PropName(PropKind k) => PropNames[(int)k];

        /// "Rioter x6, Guard": runs of the same enemy counted.
        public static string MembersText(IList<EnemyType> m)
        {
            var parts = new List<string>();
            for (int i = 0; i < m.Count;)
            {
                int j = i;
                while (j < m.Count && m[j] == m[i])
                    j++;
                parts.Add(Defs.Get(m[i]).Name + (j - i > 1 ? " x" + (j - i) : ""));
                i = j;
            }
            return string.Join(", ", parts);
        }

        public string ToText()
        {
            var sb = new StringBuilder();
            sb.Append("# Top-Down Arena waves: ").Append(Name).Append('\n');
            sb.Append("loop: ").Append(Loop ? "yes" : "no").Append('\n');
            for (int i = 0; i < Points.Length; i++)
                sb.Append("point ").Append(Letters[i]).Append(' ').Append(F(Points[i].X)).Append(',').Append(F(Points[i].Y)).Append('\n');
            for (int k = 0; k < Waves.Count; k++)
            {
                var w = Waves[k];
                sb.Append('\n').Append("wave ").Append(k + 1).Append(" level ").Append(w.Level).Append('\n');
                foreach (var g in w.Groups)
                {
                    sb.Append("  group ").Append(g.Name.Replace("|", "/")).Append(" | ");
                    sb.Append(g.Squad != null ? "squad " + g.Squad : MembersText(g.Members));
                    sb.Append(" | from ").Append(g.From).Append(" | at ").Append(F(g.At));
                    if (g.Squad == null)
                        sb.Append(" | every ").Append(F(g.Every)).Append(" | ").Append(ArriveName(g.Arrive));
                    sb.Append('\n');
                }
                foreach (var p in w.Props)
                    sb.Append("  prop ").Append(PropName(p.Kind)).Append(' ').Append(F(p.Pos.X)).Append(',').Append(F(p.Pos.Y)).Append('\n');
            }
            return sb.ToString();
        }

        /// Reads a .waves file. Lines it can't use are skipped and described in `problems`.
        public static WaveSet Parse(string text, out List<string> problems)
        {
            problems = new List<string>();
            var s = new WaveSet();
            DesignWave cur = null;
            int lineNo = 0;
            foreach (var raw in (text ?? "").Split('\n'))
            {
                lineNo++;
                string line = raw.Trim();
                if (line.Length == 0)
                    continue;
                try
                {
                    if (line.StartsWith("#"))
                    {
                        int c = line.IndexOf(':');
                        if (c > 0 && line.Contains("waves"))
                            s.Name = line.Substring(c + 1).Trim();
                        continue;
                    }
                    if (line.StartsWith("loop:"))
                    {
                        s.Loop = line.Substring(5).Trim().StartsWith("y", StringComparison.OrdinalIgnoreCase);
                        continue;
                    }
                    var words = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                    switch (words[0].ToLowerInvariant())
                    {
                        case "point":
                        {
                            int i = Letters.IndexOf(char.ToUpperInvariant(words[1][0]));
                            if (i < 0)
                                throw new FormatException("spawn points are A to E");
                            s.Points[i] = ParsePos(words[2]);
                            break;
                        }
                        case "wave":
                        {
                            cur = new DesignWave();
                            int li = Array.FindIndex(words, x => x.Equals("level", StringComparison.OrdinalIgnoreCase));
                            cur.Level = Math.Max(1, Math.Min(Game.MaxWave, li >= 0 && li + 1 < words.Length ? int.Parse(words[li + 1]) : 1));
                            s.Waves.Add(cur);
                            break;
                        }
                        case "group":
                        {
                            if (cur == null)
                                throw new FormatException("a group before any wave");
                            cur.Groups.Add(ParseGroup(line.Substring(5).Trim(), problems, lineNo));
                            break;
                        }
                        case "prop":
                        {
                            if (cur == null)
                                throw new FormatException("a prop before any wave");
                            string rest = line.Substring(4).Trim();
                            int sp = rest.LastIndexOf(' ');
                            string kind = rest.Substring(0, sp).Trim().ToLowerInvariant();
                            int k = Array.IndexOf(PropNames, kind);
                            if (k < 0)
                                throw new FormatException("unknown prop \"" + kind + "\"");
                            cur.Props.Add(new DesignProp { Kind = (PropKind)k, Pos = ParsePos(rest.Substring(sp + 1)) });
                            break;
                        }
                        default:
                            throw new FormatException("don't know \"" + words[0] + "\"");
                    }
                }
                catch (Exception e) when (e is FormatException || e is IndexOutOfRangeException || e is ArgumentException || e is OverflowException)
                {
                    problems.Add("line " + lineNo + ": " + e.Message);
                }
            }
            if (s.Waves.Count == 0)
                problems.Add("no waves found");
            return s;
        }

        static V2 ParsePos(string s)
        {
            var xy = s.Split(',');
            return new V2(Math.Max(0f, Math.Min(Game.Width, Fl(xy[0]))), Math.Max(0f, Math.Min(Game.Height, Fl(xy[1]))));
        }

        static DesignGroup ParseGroup(string text, List<string> problems, int lineNo)
        {
            var parts = text.Split('|').Select(x => x.Trim()).ToArray();
            var g = new DesignGroup { Name = parts[0].Length > 0 ? parts[0] : "Group" };
            if (parts.Length < 2)
                throw new FormatException("a group needs its enemies after a |");
            if (parts[1].StartsWith("squad ", StringComparison.OrdinalIgnoreCase))
            {
                string name = parts[1].Substring(6).Trim();
                var q = FindSquad(name);
                if (q == null)
                    throw new FormatException("unknown squad \"" + name + "\"");
                g.Squad = q.Name;
            }
            else
                foreach (var piece in parts[1].Split(','))
                {
                    string p = piece.Trim();
                    int n = 1;
                    int x = p.LastIndexOf(" x", StringComparison.OrdinalIgnoreCase);
                    if (x > 0 && int.TryParse(p.Substring(x + 2), out n))
                        p = p.Substring(0, x);
                    var t = FindEnemy(p);
                    if (t == null)
                    {
                        problems.Add("line " + lineNo + ": unknown enemy \"" + p + "\"");
                        continue;
                    }
                    for (int i = 0; i < Math.Min(n, 200); i++)
                        g.Members.Add(t.Value);
                }
            for (int i = 2; i < parts.Length; i++)
            {
                string p = parts[i];
                string lower = p.ToLowerInvariant();
                if (lower.StartsWith("from "))
                    g.From = new string(p.Substring(5).ToUpperInvariant().Where(ch => Letters.IndexOf(ch) >= 0).ToArray());
                else if (lower.StartsWith("at "))
                    g.At = Math.Max(0f, Fl(p.Substring(3).TrimEnd('s')));
                else if (lower.StartsWith("every "))
                    g.Every = Math.Max(0f, Fl(p.Substring(6).TrimEnd('s')));
                else if (Array.IndexOf(ArriveNames, lower) >= 0)
                    g.Arrive = (ArriveMode)Array.IndexOf(ArriveNames, lower);
                else
                    problems.Add("line " + lineNo + ": skipped \"" + p + "\"");
            }
            if (g.From.Length == 0)
                g.From = "A";
            return g;
        }
    }

    public partial class Game
    {
        /// Host: a designed wave set played in the Practice Range (the F1 wave editor's Test), or null. With it, each
        /// practice wave is that design's wave: its groups arrive on their timers from their points, its props are laid out.
        public WaveSet Design;

        class DesignSpawn { public float T; public EnemyType Type; public V2 Pos; public SquadDef Squad; public bool Ring; }
        readonly List<DesignSpawn> designQueue = new List<DesignSpawn>();
        float designClock;

        /// A designed wave's enemies still to come (the wave isn't over until they have).
        public int DesignPending => designQueue.Count;

        /// Host, as a designed wave starts: its arrivals in time order, and its props on the floor.
        void BuildDesignWave(DesignWave w)
        {
            spawnQueue.Clear();
            squadQueue.Clear();
            designQueue.Clear();
            designClock = 0f;
            bossRewarded = false;
            foreach (var g in w.Groups)
            {
                string from = g.From.Length > 0 ? g.From : "A";
                var q = g.SquadDef;
                if (q != null)
                {
                    designQueue.Add(new DesignSpawn { T = g.At, Squad = q, Pos = Design.Point(from[0]) });
                    continue;
                }
                for (int i = 0; i < g.Members.Count; i++)
                {
                    V2 at = Design.Point(from[i % from.Length]);
                    float t = g.At;
                    if (g.Arrive == ArriveMode.OneByOne)
                        t += g.Every * i;
                    else if (g.Arrive == ArriveMode.Together)
                        at += V2.FromAngle(MathX.Tau * i / Math.Max(1, g.Members.Count)) * (g.Members.Count > 1 ? 45f : 0f);
                    else if (g.Arrive == ArriveMode.Line)
                    {
                        V2 fwd = (new V2(Width / 2f, Height / 2f) - at).Normalized();
                        at += fwd.Orthogonal() * ((i - (g.Members.Count - 1) / 2f) * 60f);
                    }
                    designQueue.Add(new DesignSpawn { T = t, Type = g.Members[i], Pos = at, Ring = g.Arrive == ArriveMode.Ring });
                }
            }
            designQueue.Sort((a, b) => a.T.CompareTo(b.T));
            ClearProps(false);
            foreach (var p in w.Props)
                AddProp(p.Kind, ClampToArena(p.Pos, 40f));
        }

        /// Host, each tick of a designed wave: whoever's time has come arrives.
        void TickDesign(float dt)
        {
            if (designQueue.Count == 0)
                return;
            designClock += dt;
            var ringTarget = AlivePlayersList().FirstOrDefault();
            int ringI = 0;
            while (designQueue.Count > 0 && designQueue[0].T <= designClock)
            {
                var d = designQueue[0];
                designQueue.RemoveAt(0);
                if (d.Squad != null)
                {
                    SpawnSquadAt(d.Squad, d.Pos);
                    continue;
                }
                V2 at = d.Pos;
                if (d.Ring && ringTarget != null)
                    at = ringTarget.Pos + V2.FromAngle(MathX.Tau * (ringI++) / 8f + designClock) * 280f;
                var def = Defs.Get(d.Type);
                SpawnEnemyAt(d.Type, ClampToArena(at, def.Radius + 10f), def.IsBoss ? 1.5f : 0.9f, 0, RollElite(def));
                if (d.Type == EnemyType.TwinA)  // the Reapers come as a pair
                    SpawnEnemyAt(EnemyType.TwinB, ClampToArena(new V2(Width - at.X, at.Y), def.Radius + 10f), 1.5f, 0);
            }
        }
    }
}

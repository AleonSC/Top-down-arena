using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Arena
{
    /// Text edited in the game with the dev text editor (F4): what the UI shows in place of the text the code
    /// draws. Numbers are left to the game: in a key every run of digits is "#", so one edit of
    /// "Pay 15% of your max health" also covers "Pay 20% ...". The edits live in TextOverrides.txt (one
    /// "old<TAB>new" line each) until Tools/TextEdits/fold.py writes them into the code.
    public static class TextOverrides
    {
        public const string FileName = "TextOverrides.txt";

        static readonly Dictionary<string, string> map = new Dictionary<string, string>();
        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();

        public static int Count => map.Count;

        public static IEnumerable<KeyValuePair<string, string>> All => map;

        /// The text to show for `text`: its override (with this text's numbers put back in), or itself.
        public static string Apply(string text)
        {
            if (map.Count == 0 || string.IsNullOrEmpty(text))
                return text;
            string shown;
            if (cache.TryGetValue(text, out shown))
                return shown;
            shown = text;
            var nums = new List<string>();
            string key = Template(text, nums);
            string repl;
            if (map.TryGetValue(key, out repl))
                shown = Fill(repl, nums);
            if (cache.Count > 5000)
                cache.Clear();
            cache[text] = shown;
            return shown;
        }

        /// The override key for `text`: every run of digits becomes "#" (the runs, in order, go into nums).
        public static string Template(string text, List<string> nums = null)
        {
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length;)
            {
                if (char.IsDigit(text[i]))
                {
                    int j = i;
                    while (j < text.Length && char.IsDigit(text[j]))
                        j++;
                    if (nums != null)
                        nums.Add(text.Substring(i, j - i));
                    sb.Append('#');
                    i = j;
                }
                else
                    sb.Append(text[i++]);
            }
            return sb.ToString();
        }

        /// A template with its "#"s replaced by nums, in order.
        public static string Fill(string template, List<string> nums)
        {
            var sb = new StringBuilder(template.Length + 8);
            int n = 0;
            foreach (char c in template)
                sb.Append(c == '#' && n < nums.Count ? nums[n++] : c.ToString());
            return sb.ToString();
        }

        public static int Numbers(string text)
        {
            var nums = new List<string>();
            Template(text, nums);
            return nums.Count;
        }

        /// The edit can be saved if it keeps the same count of numbers as the original (they're filled in by
        /// the game). Returns null if it's fine, or why not.
        public static string Problem(string original, string edited)
        {
            if (edited.Trim().Length == 0)
                return "The text can't be empty.";
            if (edited.IndexOf('\t') >= 0)
                return "Tabs aren't allowed.";
            int a = Numbers(original), b = Numbers(edited);
            if (a != b)
                return "Keep " + (a == 0 ? "it free of numbers" : a == 1 ? "exactly 1 number" : "exactly " + a + " numbers") + ": the game fills them in.";
            return null;
        }

        /// What the override for this text is, if any (the text as it would be shown).
        public static bool Has(string original) => map.ContainsKey(Template(original));

        /// Set (or, if edited is the original, remove) the override for `original`.
        public static void Set(string original, string edited)
        {
            string key = Template(original), value = Template(edited);
            if (key == value)
                map.Remove(key);
            else
                map[key] = value;
            cache.Clear();
        }

        public static void Remove(string original)
        {
            map.Remove(Template(original));
            cache.Clear();
        }

        public static void Clear()
        {
            map.Clear();
            cache.Clear();
        }

        // --- The file ----------------------------------------------------------------

        public static void Load(string path)
        {
            Clear();
            if (!File.Exists(path))
                return;
            Parse(File.ReadAllText(path));
        }

        public static void Parse(string content)
        {
            foreach (var raw in content.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#!"))
                    continue;
                int tab = line.IndexOf('\t');
                if (tab <= 0)
                    continue;
                map[Unescape(line.Substring(0, tab))] = Unescape(line.Substring(tab + 1));
            }
            cache.Clear();
        }

        public static string Serialize()
        {
            var sb = new StringBuilder();
            sb.Append("#! Text edited in the game (F4). One \"old<TAB>new\" line each; # = a number the game fills in.\n");
            sb.Append("#! Write them into the code with: python3 Tools/TextEdits/fold.py\n");
            var keys = new List<string>(map.Keys);
            keys.Sort(StringComparer.Ordinal);
            foreach (var k in keys)
                sb.Append(Escape(k)).Append('\t').Append(Escape(map[k])).Append('\n');
            return sb.ToString();
        }

        public static void Save(string path)
        {
            File.WriteAllText(path, Serialize());
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\n", "\\n").Replace("\t", "\\t");

        static string Unescape(string s)
        {
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] == '\\' && i + 1 < s.Length)
                {
                    char c = s[++i];
                    sb.Append(c == 'n' ? '\n' : c == 't' ? '\t' : c);
                }
                else
                    sb.Append(s[i]);
            }
            return sb.ToString();
        }
    }
}

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arena
{
    /// The player's bound keys for alternative fire and the abilities. Game text names abilities by slot (1st-4th,
    /// Docs/Glossary.md), so only "Alt" in a description is swapped for the bound key (Fill); the ability keys are for
    /// labels that show the key itself (the stat sheet). Core has no Unity, so the front end sets these (Keybinds.cs).
    public static class KeyText
    {
        public static string Alt = "Alt", Space = "Space", Q = "Q", E = "E", Shift = "Shift";

        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly Regex AltWord = new Regex(@"\bAlt\b");

        static bool Stock => Alt == "Alt";

        public static void Set(string alt, string space, string q, string e, string shift)
        {
            Alt = alt;
            Space = space;
            Q = q;
            E = e;
            Shift = shift;
            cache.Clear();
        }

        /// `s` with "Alt" swapped for the bound alternative-fire key (unchanged with the stock key).
        public static string Fill(string s)
        {
            if (string.IsNullOrEmpty(s) || Stock)
                return s;
            string r;
            if (cache.TryGetValue(s, out r))
                return r;
            r = AltWord.Replace(s, Alt);
            if (cache.Count > 512)
                cache.Clear();
            cache[s] = r;
            return r;
        }
    }
}

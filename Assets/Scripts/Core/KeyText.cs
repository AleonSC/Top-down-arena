using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arena
{
    /// The rebindable keys that game text names ("Alt fires them all", "your Space ability", "your Q, E and Shift abilities").
    /// Core has no Unity, so the front end sets these from the player's keybinds (Keybinds.cs); the defaults are the stock
    /// keys. Fill swaps the stock names in a description for the bound ones.
    public static class KeyText
    {
        public static string Alt = "Alt", Space = "Space", Q = "Q", E = "E", Shift = "Shift";

        static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
        static readonly Regex AltWord = new Regex(@"\bAlt\b"), SpaceAbility = new Regex(@"\bSpace (?=abilit)"), QES = new Regex(@"\bQ, E and Shift\b");

        static bool Stock => Alt == "Alt" && Space == "Space" && Q == "Q" && E == "E" && Shift == "Shift";

        public static void Set(string alt, string space, string q, string e, string shift)
        {
            Alt = alt;
            Space = space;
            Q = q;
            E = e;
            Shift = shift;
            cache.Clear();
        }

        /// `s` with the stock key names swapped for the bound ones (unchanged with the stock keys).
        public static string Fill(string s)
        {
            if (string.IsNullOrEmpty(s) || Stock)
                return s;
            string r;
            if (cache.TryGetValue(s, out r))
                return r;
            r = QES.Replace(s, Q + ", " + E + " and " + Shift);
            r = SpaceAbility.Replace(r, Space + " ");
            r = AltWord.Replace(r, Alt);
            if (cache.Count > 512)
                cache.Clear();
            cache[s] = r;
            return r;
        }
    }
}

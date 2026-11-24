using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Arena
{
    /// What a wiki entry is (its tag in the list). New kinds go at the end.
    public enum WikiKind : byte { Class, Role, Weapon, Ability, Ultimate, Card, Perk, Enemy, Boss }

    /// One thing in the game the wiki can show: its name, a line on what it is, its text and the entries it links to.
    public class WikiEntry
    {
        public string Name;
        public WikiKind Kind;
        public string Sub;    // a short line under the name ("Soldier · DPS weapon", "Rare card")
        public string Text;   // what it does (the game's own description: Desc, Blurb, Line...)
        public readonly List<WikiEntry> Links = new List<WikiEntry>();      // what it belongs to or works with (from the data)
        public readonly List<WikiEntry> Mentions = new List<WikiEntry>();   // entries its text names
        public readonly List<WikiEntry> MentionedBy = new List<WikiEntry>(); // entries that name it or link to it
        internal string search;  // name and text, lower case (Search)
        public override string ToString() => Kind + ": " + Name;
    }

    /// The in-game wiki (Docs/Wiki.md): every class, role, weapon, ability, upgrade card, perk, enemy and boss, built from the game's
    /// own tables (Defs, Upgrades, Perks, Classes), so new content shows up with no extra work. References come two ways: links from the
    /// data (a weapon's class and role, a card's weapons, ability or boss, a perk's boss) and mentions, where one entry's text names
    /// another; both are shown from each side. Search ranks by name first, then by text.
    public static class Wiki
    {
        static List<WikiEntry> all;
        static Dictionary<string, WikiEntry> byName;
        static Dictionary<EnemyType, WikiEntry> enemyEntry;
        static Dictionary<Kit, WikiEntry> kitEntry;

        /// The entry for an enemy or boss on the field (a boss's decoy shows its boss), or null.
        public static WikiEntry Of(EnemyType t)
        {
            Build();
            if (enemyEntry.TryGetValue(t, out var e))
                return e;
            var d = (int)t < Defs.Enemies.Length ? Defs.Get(t) : null;
            return d != null ? Find(d.Name) : null;
        }

        /// The entry for a class.
        public static WikiEntry Of(Kit k) { Build(); return kitEntry.TryGetValue(k, out var e) ? e : null; }

        /// The entry a piece of on-screen text is about (the eyedropper): its whole text as a name, else the longest entry name in it
        /// as a whole word ("Revolver: Hot Lead" → Hot Lead), else null.
        public static WikiEntry FindIn(string text)
        {
            Build();
            if (string.IsNullOrWhiteSpace(text))
                return null;
            string t = text.Trim();
            var e = Find(t);
            if (e != null)
                return e;
            WikiEntry best = null;
            foreach (var n in byName.Values)
            {
                if (n.Name.Length < 4 || best != null && n.Name.Length <= best.Name.Length)
                    continue;
                int at = t.IndexOf(n.Name, StringComparison.OrdinalIgnoreCase);
                while (at >= 0)
                {
                    int end = at + n.Name.Length;
                    if ((at == 0 || !char.IsLetterOrDigit(t[at - 1])) && (end >= t.Length || !char.IsLetterOrDigit(t[end])))
                    {
                        best = n;
                        break;
                    }
                    at = t.IndexOf(n.Name, end, StringComparison.OrdinalIgnoreCase);
                }
            }
            return best;
        }

        public static List<WikiEntry> All { get { Build(); return all; } }

        public static string KindName(WikiKind k) => k == WikiKind.Card ? "Upgrade card" : k.ToString();

        /// The entry with this name (any case), or null. Kind narrows it when two share a name.
        public static WikiEntry Find(string name, WikiKind? kind = null)
        {
            Build();
            if (string.IsNullOrEmpty(name))
                return null;
            if (kind == null && byName.TryGetValue(name.ToLowerInvariant(), out var e))
                return e;
            return all.FirstOrDefault(x => string.Equals(x.Name, name, StringComparison.OrdinalIgnoreCase) && (kind == null || x.Kind == kind));
        }

        /// Entries matching the query, best first: the whole name, the name's start, a word's start in the name, anywhere in the name,
        /// then in the text. Every word of the query must match. An empty query lists everything by kind, then name.
        public static List<WikiEntry> Search(string query, int max = 200)
        {
            Build();
            var words = (query ?? "").ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0)
                return all.OrderBy(e => e.Kind).ThenBy(e => e.Name).Take(max).ToList();
            string q = string.Join(" ", words);
            var scored = new List<KeyValuePair<int, WikiEntry>>();
            foreach (var e in all)
            {
                if (!words.All(w => e.search.Contains(w)))
                    continue;
                string n = e.Name.ToLowerInvariant();
                int s = n == q ? 0 : n.StartsWith(q) ? 1 : (" " + n).Contains(" " + q) ? 2 : n.Contains(q) ? 3 : words.All(w => n.Contains(w)) ? 4 : 5;
                scored.Add(new KeyValuePair<int, WikiEntry>(s, e));
            }
            return scored.OrderBy(p => p.Key).ThenBy(p => p.Value.Kind).ThenBy(p => p.Value.Name).Take(max).Select(p => p.Value).ToList();
        }

        /// Every other entry this one touches: its links, what its text names and what names or links to it (no repeats).
        public static List<WikiEntry> References(WikiEntry e)
        {
            var seen = new HashSet<WikiEntry> { e };
            var list = new List<WikiEntry>();
            foreach (var x in e.Links.Concat(e.Mentions).Concat(e.MentionedBy))
                if (seen.Add(x))
                    list.Add(x);
            return list;
        }

        /// Builds the entries once (the tables don't change in a run). Rebuild drops them (tests, dev tools).
        public static void Rebuild() { all = null; Build(); }

        static void Build()
        {
            if (all != null)
                return;
            all = new List<WikiEntry>();
            var roleOf = new Dictionary<Role, WikiEntry>();
            var kitOf = new Dictionary<Kit, WikiEntry>();
            var abilityOf = new Dictionary<AbilityId, WikiEntry>();
            var weaponOf = new Dictionary<WeaponId, WikiEntry>();
            var enemyOf = new Dictionary<EnemyType, WikiEntry>();

            for (int i = 0; i < Defs.Roles.Length; i++)
                roleOf[(Role)i] = Add(Defs.Roles[i].Name, WikiKind.Role, "Role", Defs.Roles[i].Desc);
            foreach (var k in Classes.All)
                kitOf[k.Id] = Add(k.Name, WikiKind.Class, "Class", k.Blurb);
            foreach (var w in Defs.Weapons)
            {
                if (w == null || string.IsNullOrEmpty(w.Name) || weaponOf.ContainsKey(w.Id))
                    continue;
                string role = roleOf.TryGetValue(w.Role, out var re) ? re.Name : w.Role.ToString();
                var e = Add(w.Name, WikiKind.Weapon, kitOf[w.Kit].Name + " · " + role + (w.Variant ? " weapon variant" : " weapon"), w.Desc);
                Link(e, kitOf[w.Kit]);
                Link(e, roleOf[w.Role]);
                weaponOf[w.Id] = e;
            }
            foreach (var a in Upgrades.Abilities)
            {
                if (a == null || string.IsNullOrEmpty(a.Name) || abilityOf.ContainsKey(a.Id))
                    continue;
                bool ult = a.Choice == AbilityDef.Ult;
                string slot = ult ? "Ultimate" : a.Choice == AbilityDef.CardOnly ? "ability from a very rare card" : "1st ability";
                string text = a.Desc + (string.IsNullOrEmpty(a.Capstone) ? "" : "\nMastery 5: " + a.Capstone);
                var e = Add(a.Name, ult ? WikiKind.Ultimate : WikiKind.Ability, Classes.Get(a.Kit).Name + " · " + (roleOf.TryGetValue(a.Role, out var re) ? re.Name : a.Role.ToString()) + " " + slot, text);
                Link(e, kitOf[a.Kit]);
                if (roleOf.TryGetValue(a.Role, out var r))
                    Link(e, r);
                abilityOf[a.Id] = e;
            }
            foreach (var t in (EnemyType[])Enum.GetValues(typeof(EnemyType)))
            {
                if ((int)t >= Defs.Enemies.Length)
                    continue;
                var d = Defs.Get(t);
                // Skip repeats, and a boss's stand-ins that carry its name (the Duelist's decoys).
                if (d == null || string.IsNullOrEmpty(d.Name) || Find(d.Name, d.IsBoss ? WikiKind.Boss : WikiKind.Enemy) != null
                    || !d.IsBoss && Find(d.Name, WikiKind.Boss) != null)
                    continue;
                string sub = d.IsBoss ? "Boss" : d.Mini ? "Mini-boss" : d.Melee ? "Melee enemy" : "Ranged enemy";
                enemyOf[t] = Add(d.Name, d.IsBoss ? WikiKind.Boss : WikiKind.Enemy, sub, d.Blurb);
            }
            foreach (var kv in enemyOf)  // a boss's faction: the enemies its build-up waves bring
            {
                var f = Approaches.Of(kv.Key);
                if (f != null && f.Boss != kv.Key && enemyOf.TryGetValue(f.Boss, out var boss))
                    Link(kv.Value, boss);
            }
            foreach (var c in Upgrades.All)
            {
                if (c == null || c.Unoffered || string.IsNullOrEmpty(c.Name))
                    continue;
                // A card that grants an ability is that ability: the ability's entry says so instead of a second entry.
                if (c.Grants != AbilityId.None && abilityOf.TryGetValue(c.Grants, out var ga) && ga.Name == c.Name)
                {
                    if (!ga.Sub.Contains("card"))
                        ga.Sub += " · also a very rare card";
                    continue;
                }
                if (c.ClassAbility || Find(c.Name, WikiKind.Card) != null)
                    continue;
                var e = Add(c.Name, WikiKind.Card, RarityName(c.Rarity) + " card", c.Desc);
                if (c.Kit.HasValue)
                    Link(e, kitOf[c.Kit.Value]);
                if (c.Role.HasValue && roleOf.TryGetValue(c.Role.Value, out var rr))
                    Link(e, rr);
                if (c.Grants != AbilityId.None && abilityOf.TryGetValue(c.Grants, out var g))
                    Link(e, g);
                if (c.RequiresAbility != AbilityId.None && abilityOf.TryGetValue(c.RequiresAbility, out var ra))
                    Link(e, ra);
                if (c.Weapons != null)
                    foreach (var w in c.Weapons)
                        if (weaponOf.TryGetValue(w, out var we))
                            Link(e, we);
                if (c.Boss.HasValue && enemyOf.TryGetValue(c.Boss.Value, out var be))
                    Link(e, be);
            }
            foreach (var p in Perks.All)
            {
                if (p == null || string.IsNullOrEmpty(p.Name))
                    continue;
                var e = Add(p.Name, WikiKind.Perk, "Perk", p.Line + (string.IsNullOrEmpty(p.UnlockText) ? "" : "\nUnlock: " + p.UnlockText));
                if (enemyOf.TryGetValue(p.Boss, out var be))
                    Link(e, be);
            }

            enemyEntry = enemyOf;
            kitEntry = kitOf;
            byName = new Dictionary<string, WikiEntry>();
            foreach (var e in all)
            {
                string key = e.Name.ToLowerInvariant();
                if (!byName.ContainsKey(key))
                    byName[key] = e;
            }
            FindMentions();
            foreach (var e in all)
            {
                e.search = (e.Name + " " + e.Sub + " " + e.Text).ToLowerInvariant();
                e.MentionedBy.Sort((a, b) => a.Kind != b.Kind ? a.Kind.CompareTo(b.Kind) : string.CompareOrdinal(a.Name, b.Name));
            }
        }

        static WikiEntry Add(string name, WikiKind kind, string sub, string text)
        {
            var e = new WikiEntry { Name = name.Trim(), Kind = kind, Sub = sub, Text = text ?? "" };
            all.Add(e);
            return e;
        }

        /// A data link: e lists to, and to lists e among what references it.
        static void Link(WikiEntry e, WikiEntry to)
        {
            if (to == null || to == e || e.Links.Contains(to))
                return;
            e.Links.Add(to);
            if (!to.MentionedBy.Contains(e))
                to.MentionedBy.Add(e);
        }

        /// Mentions: an entry's text naming another entry as a whole word (names of 4+ letters, case as written; the longest name
        /// wins where names overlap, so "Arc Cannon" doesn't also count as "Arc").
        static void FindMentions()
        {
            var names = byName.Values.Where(x => x.Name.Length >= 4).OrderByDescending(x => x.Name.Length).ToList();
            foreach (var e in all)
            {
                if (e.Text.Length == 0)
                    continue;
                var taken = new bool[e.Text.Length];
                foreach (var n in names)
                {
                    if (n == e || n.Name == e.Name)
                        continue;
                    int at = 0;
                    while ((at = e.Text.IndexOf(n.Name, at, StringComparison.Ordinal)) >= 0)
                    {
                        int end = at + n.Name.Length;
                        bool word = (at == 0 || !char.IsLetterOrDigit(e.Text[at - 1])) && (end >= e.Text.Length || !char.IsLetterOrDigit(e.Text[end]));
                        if (word && !taken[at])
                        {
                            for (int i = at; i < end; i++)
                                taken[i] = true;
                            if (!e.Mentions.Contains(n))
                                e.Mentions.Add(n);
                            if (!n.MentionedBy.Contains(e))
                                n.MentionedBy.Add(e);
                            break;
                        }
                        at = end;
                    }
                }
            }
        }

        public static string RarityName(Rarity r) =>
            r == Rarity.VeryRare ? "Very rare" : r == Rarity.Mod ? "Weapon mod" : r.ToString();

        /// A plain-text dump of every entry (the wiki test's listing).
        public static string Dump()
        {
            var sb = new StringBuilder();
            foreach (var e in All.OrderBy(x => x.Kind).ThenBy(x => x.Name))
                sb.Append(KindName(e.Kind)).Append(": ").Append(e.Name).Append(" (").Append(References(e).Count).Append(" refs)\n");
            return sb.ToString();
        }
    }
}

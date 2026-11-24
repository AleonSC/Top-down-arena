using System;
using System.Linq;
using Arena;

// The in-game wiki (Core/Wiki.cs, Docs/Wiki.md): every entry has a name and text, search ranks names first, references go both ways.
static partial class Program
{
    static void WikiTest()
    {
        Wiki.Rebuild();
        var all = Wiki.All;
        if (Environment.GetEnvironmentVariable("WIKIDUMP") == "1")
            Console.Write(Wiki.Dump());
        foreach (WikiKind k in Enum.GetValues(typeof(WikiKind)))
            Console.WriteLine("  " + Wiki.KindName(k) + ": " + all.Count(e => e.Kind == k));
        var blank = all.Where(e => string.IsNullOrWhiteSpace(e.Text)).ToList();
        Check(blank.Count == 0, "every wiki entry has text" + (blank.Count > 0 ? " (missing: " + string.Join(", ", blank.Take(12)) + (blank.Count > 12 ? ", ..." : "") + ")" : ""));
        Check(all.Count(e => e.Kind == WikiKind.Class) == Classes.All.Length, "every class has an entry");
        Check(Defs.Weapons.Where(w => w != null).Select(w => w.Name).Distinct().All(n => Wiki.Find(n) != null), "every weapon has an entry");
        Check(Perks.All.All(p => Wiki.Find(p.Name, WikiKind.Perk) != null), "every perk has an entry");
        var dupes = all.GroupBy(e => e.Kind + e.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Check(dupes.Count == 0, "no entry twice" + (dupes.Count > 0 ? " (" + string.Join(", ", dupes) + ")" : ""));

        var rev = Wiki.Search("revolver");
        Check(rev.Count > 0 && rev[0].Name == "Revolver" && rev[0].Kind == WikiKind.Weapon, "searching revolver finds the Revolver first");
        Check(Wiki.Search("REVOLVER").FirstOrDefault() == rev[0], "search ignores case");
        Check(Wiki.Search("zzqqxx").Count == 0, "a search with no match finds nothing");
        Check(Wiki.Search("").Count == Math.Min(200, all.Count), "an empty search lists everything");

        var soldier = Wiki.Find("Soldier", WikiKind.Class);
        var revolver = Wiki.Find("Revolver", WikiKind.Weapon);
        Check(soldier != null && revolver != null && revolver.Links.Contains(soldier) && soldier.MentionedBy.Contains(revolver),
            "a weapon links to its class, and the class lists the weapon");
        Check(all.Where(e => e.Kind == WikiKind.Weapon).All(e => e.Links.Any(l => l.Kind == WikiKind.Class) && e.Links.Any(l => l.Kind == WikiKind.Role)),
            "every weapon links to its class and role");
        var mention = all.FirstOrDefault(e => e.Mentions.Count > 0);
        Check(mention != null && mention.Mentions.All(m => m.MentionedBy.Contains(mention) && mention.Text.Contains(m.Name)),
            "a mention goes both ways and is named in the text" + (mention != null ? " (" + mention + " names " + mention.Mentions[0].Name + ")" : ""));
        Check(all.All(e => !Wiki.References(e).Contains(e)), "nothing references itself");
        // The eyedropper's lookups (BayWiki.cs, WikiPick.cs).
        Check(Wiki.FindIn("Revolver") == revolver && Wiki.FindIn("  REVOLVER ") == revolver, "on-screen text that is a name finds it");
        Check(Wiki.FindIn("Equipped: Revolver (Soldier)") == revolver, "a name inside longer text finds it");
        Check(Wiki.FindIn("Revolvers") == null || Wiki.FindIn("Revolvers") != revolver, "only whole words count");
        Check(Wiki.FindIn("WAVE 12") == null && Wiki.FindIn("") == null, "text naming nothing finds nothing");
        Check(Wiki.Of(EnemyType.Warden) != null && Wiki.Of(EnemyType.Warden).Kind == WikiKind.Boss, "an enemy on the field finds its entry (the Warden)");
        Check(Wiki.Of(EnemyType.Decoy) == Wiki.Find("The Duelist", WikiKind.Boss), "a boss's decoy finds its boss");
        Check(Wiki.Of(Kit.Knight) == Wiki.Find("Knight", WikiKind.Class), "a player finds their class");
        int linked = all.Count(e => Wiki.References(e).Count > 0);
        Console.WriteLine("  " + linked + " of " + all.Count + " entries have references");
        var lone = all.Where(e => e.Kind != WikiKind.Role && e.Kind <= WikiKind.Ultimate || e.Kind == WikiKind.Perk || e.Kind == WikiKind.Boss).Where(e => Wiki.References(e).Count == 0).ToList();
        Check(lone.Count == 0, "every class, weapon, ability, perk and boss has a reference" + (lone.Count > 0 ? " (none: " + string.Join(", ", lone.Take(10)) + ")" : ""));
        var lich = Wiki.Find("The Lich", WikiKind.Boss);
        var jar = Wiki.Find("Phylactery", WikiKind.Enemy);
        Check(lich != null && jar != null && Wiki.References(lich).Contains(jar), "a boss lists its parts (the Lich: the Phylactery)");
        Check(Wiki.Find("The Duelist", WikiKind.Enemy) == null, "a boss's decoy isn't an entry of its own");
    }
}

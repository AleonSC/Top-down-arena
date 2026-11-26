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
        Check(mention != null && mention.Mentions.All(m => m.MentionedBy.Contains(mention) && mention.Text.IndexOf(m.Name, StringComparison.OrdinalIgnoreCase) >= 0),
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
        // Step 2's kinds: each has entries, from its own table.
        foreach (WikiKind k in Enum.GetValues(typeof(WikiKind)))
            Check(all.Any(e => e.Kind == k), "the wiki has " + Wiki.KindName(k) + " entries");
        Check(Wiki.Find("Land Mine", WikiKind.Gadget) != null && Wiki.Find("Wall Bounce", WikiKind.Relic) != null && Wiki.Find("Nightmare", WikiKind.Difficulty) != null
            && Wiki.Find("Kill Rush", WikiKind.Challenge) != null && Wiki.Find("Wound", WikiKind.Mechanic) != null, "gadgets, relics, presets, challenge rooms and mechanics are in");
        var golem = Wiki.Find("Golem", WikiKind.Minion);
        Check(golem != null && golem.Links.Any(l => l.Kind == WikiKind.Weapon) && golem.Text.Length > 10, "a companion links to its weapon and its text comes from its numbers (Golem: " + (golem != null ? golem.Text : "none") + ")");
        Check(Wiki.Of(MinionKind.Golem) == golem, "a minion on the field finds its entry");
        Check(Wiki.Find("Grenade", WikiKind.Gadget) != null && Wiki.Find("Grenade", WikiKind.Gadget).Links.Any(l => l.Name == "Gadget chest"), "a gadget links to the gadget chest");
        var burning = Wiki.Status("Burning");
        Check(burning != null && Wiki.References(burning).Any(r => r.Name == "Flamethrower"), "a status links to what causes it (Burning: Flamethrower)");
        Check(burning != null && burning.MentionedBy.Count >= 2, "statuses are found in other entries' text in any case (" + (burning != null ? burning.MentionedBy.Count : 0) + " name burning)");
        Check(Wiki.Of(PropKind.Barrel, ChestKind.Upgrade) == Wiki.Find("Barrel", WikiKind.Prop) && Wiki.Of(PropKind.Chest, ChestKind.Gadget) == Wiki.Find("Gadget chest", WikiKind.Prop),
            "a prop or chest on the field finds its entry");
        Check(WikiTopics.Things.All(t => t.Chest ? Enum.IsDefined(typeof(ChestKind), (ChestKind)t.Kind) : Enum.IsDefined(typeof(PropKind), (PropKind)t.Kind)), "every prop entry names a real kind");
        // Boss phases (WikiPhases.cs) and player statuses.
        var gale = Wiki.PhaseIn("GALE", EnemyType.StormHerald);
        Check(gale != null && gale.Kind == WikiKind.Phase && gale.Links.Any(l => l.Name == "Storm Herald"), "a phase name on the boss bar finds that boss's phase (Gale), linked to the boss");
        Check(Wiki.PhaseIn("STAGE 2 / 5 · PENUMBRA", EnemyType.Eclipse)?.Name == "Penumbra", "a phase inside longer boss-bar text is found");
        Check(Wiki.PhaseIn("LAST STAND", EnemyType.Warden)?.Name == WikiPhases.LastStandName && Wiki.PhaseIn("LAST STAND", EnemyType.Lich) == null, "LAST STAND opens the shared last stand for a boss that has one");
        Check(Wiki.PhaseIn("FRENZY", EnemyType.HiveQueen)?.Name == "Frenzy" && Wiki.PhaseIn("FRENZY", EnemyType.Devourer) == null, "a phase belongs to its own boss");
        Check(Wiki.PhaseIn("SUNDERED", EnemyType.ReaperOfDawn)?.Name == "Sundered", "either Reaper finds the Reapers' phases");
        foreach (var b in Defs.Bosses)
            Check(WikiPhases.All.Count(p => p.Boss == b) >= 3, "the " + Defs.Get(b).Name + " has its phases in the wiki");
        // Boss attacks (WikiAttacks.cs): each under its boss and phase, found from a boss bar's text.
        foreach (var at in WikiAttacks.All)
            if (at.Phase != null && !WikiPhases.All.Any(p => p.Boss == at.Boss && p.Name == at.Phase))
                Check(false, "the attack " + at.Name + " names a phase its boss has (" + at.Phase + ")");
        var inhale = Wiki.Find("Inhale", WikiKind.Attack);
        Check(inhale != null && inhale.Links.Any(l => l.Kind == WikiKind.Phase && l.Name == "Gorging") && inhale.Links.Any(l => l.Kind == WikiKind.Boss),
            "an attack links to its phase and boss (Inhale: Gorging, the Devourer)");
        Check(Wiki.PhaseIn("INHALE", EnemyType.Devourer) == inhale && Wiki.PhaseIn("SEARCHLIGHT IN 3 S", EnemyType.Warden)?.Name == "Searchlight", "a boss bar's attack name finds the attack");
        Check(Wiki.Find("Gorging", WikiKind.Phase).MentionedBy.Contains(inhale), "a phase lists its attacks");
        Check(Wiki.Find("Swallowed", WikiKind.Status) != null && Wiki.References(Wiki.Find("Swallowed", WikiKind.Status)).Any(r => r.Name == "The Devourer"), "a player status links to what causes it (Swallowed: the Devourer)");
        Check(WikiTopics.PlayerStatuses.All(t => t.From.All(f => Wiki.Find(f) != null)), "every player status's cause is an entry");
        var seal = Wiki.Find("Warden's Seal", WikiKind.Relic);
        Check(seal == null || seal.Links.Any(l => l.Kind == WikiKind.Boss && l.Name == "The Warden"), "a faction relic links to its boss");
        var wound = Wiki.Find("Wound", WikiKind.Mechanic);
        Check(wound != null && Wiki.References(wound).Any(r => r.Name == "Health shrine" || r.Kind == WikiKind.Mechanic), "a mechanic's text links to what it names (Wound: Health shrine)");
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

using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Arena;

// Classes: every class has everything a class needs. Run it first when adding one (run.sh 1 1 classes): each
// missing piece is named, with the file it goes in (see "Adding a class" in HANDOFF.md).
static partial class Program
{
    static string ScriptsDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "Assets", "Scripts");

    static string AllSource(string folder) =>
        string.Join("\n", Directory.GetFiles(Path.Combine(ScriptsDir, folder), "*.cs").Select(File.ReadAllText));

    static void ClassCompletenessTest()
    {
        string core = AllSource("Core");
        string pixelArt = File.ReadAllText(Path.Combine(ScriptsDir, "Game", "PixelArt.cs"));
        string looks = File.ReadAllText(Path.Combine(ScriptsDir, "Game", "ClassLooks.cs"));
        string fireVolley = File.ReadAllText(Path.Combine(ScriptsDir, "Core", "Weapons.cs"));
        var roles = new[] { Role.Tank, Role.Healer, Role.Dps };
        var problems = new List<string>();
        Action<bool, string> need = (ok, what) => { if (!ok) problems.Add(what); };

        int kits = Enum.GetValues(typeof(Kit)).Length;
        need(kits == Defs.KitCount && Classes.All.Length == kits, "Kit, Defs.KitCount and Classes.All agree on the number of classes");
        need(Regex.Matches(looks, @"new KitLook\b").Count == kits, "ClassLooks.cs has one KitLook per class, in Kit order");
        need(Classes.All.Select(k => k.Name).Distinct().Count() == kits, "every class has its own name");

        foreach (var k in Classes.All)
        {
            string who = Defs.KitName(k.Id) + " (Kit." + k.Id + ")";
            need(!string.IsNullOrEmpty(k.Blurb), who + ": a KitDef.Blurb");
            need(!string.IsNullOrEmpty(k.Icon) && pixelArt.Contains("\"" + k.Icon + "\""), who + ": its icon \"" + k.Icon + "\" in PixelArt.cs");
            need(k.HpMult > 0.5f && k.HpMult < 1.6f && k.SpeedMult > 0.7f && k.SpeedMult < 1.4f, who + ": a sensible HpMult and SpeedMult");

            // Weapons: three per role (four where it has a fourth), all its own.
            foreach (var role in roles)
                for (int c = 0; c < Defs.WeaponCount(role, k.Id); c++)
                {
                    if (Defs.WeaponGap(role, k.Id, c))
                        continue;  // a class still being built: a stand-in, locked in the lobby
                    var w = Defs.Weapon(role, k.Id, c);
                    string wn = who + " " + role + " weapon " + c + " (" + w.Name + ")";
                    need(w.Kit == k.Id && w.Role == role && w.Choice == c, wn + ": in its own lobby place");
                    need(!string.IsNullOrEmpty(w.Name) && !string.IsNullOrEmpty(w.Desc) && !string.IsNullOrEmpty(w.ExtraDesc), wn + ": a Name, Desc and ExtraDesc");
                    need(w.FireInterval > 0f, wn + ": a FireInterval");
                    need(w.Melee ? w.Reach > 0f : w.Speed * w.Life > 0f || w.Reach > 0f || fireVolley.Contains("case WeaponId." + w.Id + ":"),
                        wn + ": a range (Reach for a swing or beam, Speed and Life for a shot, or a WeaponRange case)");
                    need(k.Fire != null || fireVolley.Contains("case WeaponId." + w.Id + ":"), wn + ": something fires it (KitDef.Fire, or its own case in FireVolley)");
                    need(core.Contains("case WeaponId." + w.Id + ":"), wn + ": its own case in its class's fire method (a case WeaponId." + w.Id + ")");
                }

            // Abilities: three 1st abilities per role and an ultimate, all its own, each with a cast.
            foreach (var role in roles)
            {
                var placed = Enumerable.Range(0, Upgrades.FirstAbilityChoices).Where(c => !Upgrades.AbilityGap(role, k.Id, c)).Select(c => Upgrades.FirstAbility(role, k.Id, c)).ToList();
                if (!Upgrades.UltGap(role, k.Id))
                    placed.Add(Upgrades.Ultimate(role, k.Id));
                for (int u = 1; u < Upgrades.UltKeys; u++)  // any other ultimates on the lobby's Ultimate row
                    if (Upgrades.AltUltimate(role, k.Id, u) != AbilityId.None)
                        placed.Add(Upgrades.AltUltimate(role, k.Id, u));
                foreach (var id in placed)
                {
                    var a = Upgrades.Ability(id);
                    string an = who + " " + role + " ability " + a.Name;
                    need(a.Kit == k.Id && a.Role == role, an + ": belongs to its class and role");
                    need(core.Contains("case AbilityId." + id + ":"), an + ": a cast (a case AbilityId." + id + " in its class's switch)");
                    need(a.Choice == AbilityDef.Ult || a.Icon != null, an + ": an AbilityDef.Icon (ultimates have none)");
                }
            }
        }
        // Casts in a class's own file need its KitDef.Ability: the Soldier's, Elementalist's and Summoner's are in CastAbility / KitAbility.
        foreach (var k in Classes.All.Where(k => k.Id > Kit.Summoner))
            need(k.Ability != null, Defs.KitName(k.Id) + ": a KitDef.Ability that casts its abilities");

        foreach (var p in problems)
            Console.WriteLine("  missing: " + p);
        Check(problems.Count == 0, "every class is complete: " + kits + " classes, each with a name, blurb, icon, look, 9 weapons that fire, 9 1st abilities and 3 ultimates that cast");

        AbilityIconsTest(pixelArt);
        WeaponAltIdsTest();

        var thrown = Defs.Weapons.Where(w => w.Sound == Sfx.Throw && !w.Melee).Select(w => w.Name);
        Console.WriteLine("  weapons with the default throw sound (set WeaponDef.Sound if that's wrong): " + string.Join(", ", thrown));
    }

    /// Every ability's icon is keyed by AbilityDef.Icon (not its name), so a rename can't drop it: every ability but the
    /// ultimates has one, and each key is drawn in PixelArt.cs. The card table (PixelArt.ByUpgrade) is still by name,
    /// so each name there must be a real card.
    static void AbilityIconsTest(string pixelArt)
    {
        // Abilities still waiting for an icon of their own; the test fails when one gets one, so take it off the list then.
        var noIconYet = new HashSet<AbilityId> { AbilityId.BambooGrove, AbilityId.SenbonSeekers, AbilityId.LandShark, AbilityId.Starburst, AbilityId.Flood, AbilityId.WaterWard, AbilityId.DeployableTurret, AbilityId.Stampede, AbilityId.ThornBloom };
        var bad = new List<string>();
        foreach (AbilityId id in Enum.GetValues(typeof(AbilityId)))
        {
            if (id == AbilityId.None)
                continue;
            var a = Upgrades.Ability(id);
            if (a.Icon != null && !pixelArt.Contains("[\"" + a.Icon + "\"] = new[]"))
                bad.Add(a.Name + ": its icon \"" + a.Icon + "\" isn't drawn in PixelArt.cs");
            else if (a.Icon == null && a.Choice != AbilityDef.Ult && !noIconYet.Contains(id))
                bad.Add(a.Name + " (AbilityId." + id + "): no AbilityDef.Icon");
            else if (a.Icon != null && noIconYet.Contains(id))
                bad.Add(a.Name + " has an icon now: take it off noIconYet");
        }
        int at = pixelArt.IndexOf("Dictionary<string, string> ByUpgrade");
        string table = pixelArt.Substring(at, pixelArt.IndexOf("};", at) - at);
        var cards = new HashSet<string>(Upgrades.All.Select(u => u.Name));
        foreach (Match m in Regex.Matches(table, @"\{ ""([^""]+)"", ""[^""]+"" \}"))
            if (!cards.Contains(m.Groups[1].Value))
                bad.Add("PixelArt.ByUpgrade's \"" + m.Groups[1].Value + "\" is no card's name (renamed? or it grants an ability: use AbilityDef.Icon)");
        foreach (var b in bad)
            Console.WriteLine("  icon: " + b);
        Check(bad.Count == 0, "every ability but the ultimates has an AbilityDef.Icon that PixelArt.cs draws, and every ByUpgrade name is a card");
    }

    /// The weapon Alt ids (WeaponAlt.cs) go over the network as Msg.WeaponDash's kind byte: no two may share a number.
    static void WeaponAltIdsTest()
    {
        var ids = typeof(WeaponAlt).GetFields().Where(f => f.IsLiteral && f.FieldType == typeof(byte)).Select(f => new { f.Name, Value = (byte)f.GetRawConstantValue() }).ToList();
        var twice = ids.GroupBy(f => f.Value).Where(g => g.Count() > 1).Select(g => g.Key + ": " + string.Join(", ", g.Select(f => f.Name))).ToList();
        foreach (var t in twice)
            Console.WriteLine("  WeaponAlt id used twice: " + t);
        Check(ids.Count >= 50 && twice.Count == 0, "the " + ids.Count + " WeaponAlt ids are unique");
    }
}

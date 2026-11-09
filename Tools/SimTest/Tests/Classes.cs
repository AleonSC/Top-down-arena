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

            // Weapons: three per role, all its own.
            foreach (var role in roles)
                for (int c = 0; c < Defs.WeaponsPerRole; c++)
                {
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

            // Abilities: three Space abilities per role and an ultimate, all its own, each with a cast.
            foreach (var role in roles)
            {
                var placed = Enumerable.Range(0, Upgrades.RoleAbilityChoices).Select(c => Upgrades.RoleAbility(role, k.Id, c)).ToList();
                for (int u = 0; u < Upgrades.UltCount(role, k.Id); u++)
                    placed.Add(Upgrades.Ultimate(role, k.Id, u));
                foreach (var id in placed)
                {
                    var a = Upgrades.Ability(id);
                    string an = who + " " + role + " ability " + a.Name;
                    need(a.Kit == k.Id && a.Role == role, an + ": belongs to its class and role");
                    need(core.Contains("case AbilityId." + id + ":"), an + ": a cast (a case AbilityId." + id + " in its class's switch)");
                    need(a.Choice == AbilityDef.Ult || pixelArt.Contains("{ \"" + a.Name + "\", \""), an + ": an icon in PixelArt.cs (ByUpgrade maps its name to an icon; ultimates have none)");
                }
            }
        }
        // Casts in a class's own file need its KitDef.Ability: the Soldier's, Elementalist's and Summoner's are in CastAbility / KitAbility.
        foreach (var k in Classes.All.Where(k => k.Id > Kit.Summoner))
            need(k.Ability != null, Defs.KitName(k.Id) + ": a KitDef.Ability that casts its abilities");

        foreach (var p in problems)
            Console.WriteLine("  missing: " + p);
        Check(problems.Count == 0, "every class is complete: " + kits + " classes, each with a name, blurb, icon, look, 9 weapons that fire, 9 Space abilities and 3 ultimates that cast");

        var thrown = Defs.Weapons.Where(w => w.Sound == Sfx.Throw && !w.Melee).Select(w => w.Name);
        Console.WriteLine("  weapons with the default throw sound (set WeaponDef.Sound if that's wrong): " + string.Join(", ", thrown));
    }
}

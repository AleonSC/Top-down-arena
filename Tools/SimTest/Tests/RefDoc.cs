using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Arena;

// Docs/Reference.md: the weapon, ability and enemy tables, generated from the definitions so a balance change
// is one edit. `run.sh 1 1 refdocwrite` rewrites it; the `refdoc` test fails when the file is stale.
static partial class Program
{
    const string RefDocPath = "../../Docs/Reference.md";

    static string N(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    static string Cell(string s) => (s ?? "").Replace("|", "/").Replace("\r", " ").Replace("\n", " ").Trim();

    static string BuildRefDoc()
    {
        var sb = new StringBuilder();
        sb.Append("# Reference tables\n\n");
        sb.Append("Generated from `Defs`, `Upgrades` and `Classes` by `Tools/SimTest/run.sh 1 1 refdocwrite`; don't edit by hand (the `refdoc` test fails when it is stale).\n");
        sb.Append("Numbers are before upgrades. Player-facing prose is in [README.md](../README.md).\n");

        sb.Append("\n## Roles\n\n| Role | Health | Speed | Fire interval | Ability | Cooldown |\n|---|---|---|---|---|---|\n");
        foreach (var r in Defs.Roles)
            sb.AppendFormat("| {0} | {1} | {2} | {3} s | {4} | {5} s |\n", Cell(r.Name), N(r.Hp), N(r.Speed), N(r.FireInterval), Cell(r.Ability), N(r.AbilityCd));

        sb.Append("\n## Classes\n\n| Class | Health | Speed | About |\n|---|---|---|---|\n");
        foreach (var k in Classes.All)
            sb.AppendFormat("| {0} | x{1} | x{2} | {3} |\n", Cell(k.Name), N(k.HpMult), N(k.SpeedMult), Cell(k.Blurb));

        sb.Append("\n## Weapons\n");
        foreach (var k in Classes.All)
        {
            var ws = Defs.Weapons.Where(w => w != null && w.Kit == k.Id).OrderBy(w => w.Variant).ThenBy(w => w.Role).ThenBy(w => w.Choice).ThenBy(w => (int)w.Id).ToList();
            if (ws.Count == 0) continue;
            sb.AppendFormat("\n### {0}\n\n| Weapon | Role | Interval | Damage | Speed x life | Reach | Sim DPS | What it does |\n|---|---|---|---|---|---|---|---|\n", Cell(k.Name));
            foreach (var w in ws)
                sb.AppendFormat("| {0}{1} | {2} | {3} s | {4} | {5} | {6} | {7} | {8} |\n", Cell(w.Name), w.Variant ? " (variant)" : "", w.Role,
                    N(w.FireInterval), N(w.Damage), w.Speed > 0 ? N(w.Speed) + " x " + N(w.Life) + " s" : "-", w.Reach > 0 ? N(w.Reach) : "-",
                    N(Defs.WeaponDps(w.Id)), Cell(w.Desc));
        }

        sb.Append("\n## Abilities\n");
        foreach (var k in Classes.All)
        {
            var abs = Upgrades.Abilities.Where(a => a != null && a.Kit == k.Id).OrderBy(a => a.Role).ThenBy(a => a.Choice < 0 ? 99 : a.Choice).ThenBy(a => a.UltKey).ThenBy(a => (int)a.Id).ToList();
            if (abs.Count == 0) continue;
            sb.AppendFormat("\n### {0}\n\n| Ability | Role | Slot | Cooldown | Cost | Mastery (level 5) | What it does |\n|---|---|---|---|---|---|---|\n", Cell(k.Name));
            foreach (var a in abs)
            {
                string slot = a.Choice == AbilityDef.CardOnly ? "card" : a.Choice == AbilityDef.Ult ? "ultimate" : "Space " + (a.Choice + 1);
                string cost = a.HealthCost > 0 ? N(a.HealthCost * 100f) + "% of " + (a.CostsCurrent ? "current" : "max") + " health" : "-";
                sb.AppendFormat("| {0} | {1} | {2} | {3} s{4} | {5} | {6} | {7} |\n", Cell(a.Name), a.Role, slot, N(a.Cooldown),
                    a.ExtraCharges > 0 ? " (+" + a.ExtraCharges + " charges)" : "", cost, Cell(a.Capstone), Cell(a.Desc));
            }
        }

        sb.Append("\n## Enemies\n\n| Enemy | Group | Health | Speed | Radius | Contact | Score | XP | Notes | What it does |\n|---|---|---|---|---|---|---|---|---|---|\n");
        var types = (EnemyType[])Enum.GetValues(typeof(EnemyType));
        foreach (var t in types.OrderBy(t => (int)t))
        {
            if ((int)t >= Defs.Enemies.Length) continue;
            var e = Defs.Enemies[(int)t];
            if (e == null || string.IsNullOrEmpty(e.Name)) continue;
            var notes = new List<string>();
            if (e.IsBoss) notes.Add("boss");
            if (e.Mini) notes.Add("mini-boss");
            if (e.Melee) notes.Add("melee");
            if (e.Heavy) notes.Add("heavy");
            sb.AppendFormat("| {0} | {1} | {2} | {3} | {4} | {5} | {6} | {7} | {8} | {9} |\n", Cell(e.Name), e.Picker, N(e.Hp), N(e.Speed), N(e.Radius),
                e.Contact > 0 ? N(e.Contact) : "-", e.Score, e.Xp, string.Join(", ", notes), Cell(e.Blurb));
        }
        return sb.ToString();
    }

    static void RefDocReport(bool write)
    {
        string text = BuildRefDoc();
        Console.WriteLine("  {0} lines", text.Count(c => c == '\n'));
        if (!write) return;
        System.IO.File.WriteAllText(RefDocPath, text);
        Console.WriteLine("  wrote Docs/Reference.md");
    }

    static void RefDocTest()
    {
        string text = BuildRefDoc();
        Check(text.Contains("## Weapons") && text.Contains("## Enemies") && text.Length > 2000, "the reference builds with weapons, abilities and enemies");
        bool have = System.IO.File.Exists(RefDocPath);
        bool fresh = have && System.IO.File.ReadAllText(RefDocPath).Replace("\r\n", "\n") == text;
        Check(fresh, "Docs/Reference.md matches the definitions" + (fresh ? "" : " (run Tools/SimTest/run.sh 1 1 refdocwrite)"));
    }
}

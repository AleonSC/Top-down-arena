using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The lobby's loadout preview presses every weapon's Alt (ShowcaseAlt.cs).
static partial class Program
{
    /// Every weapon with an Alt in the preview: a 20 s run with Alt pressed shows something a run without it never does (a field of
    /// you taking a new value, a bullet kind, an enemy status), so the Alt really fires there.
    static void ShowcaseAltsTest(float dt)
    {
        var fields = typeof(Player).GetFields().Where(f => f.FieldType.IsPrimitive && f.FieldType != typeof(float) || f.FieldType.IsEnum).ToArray();
        var floats = typeof(Player).GetFields().Where(f => f.FieldType == typeof(float)).ToArray();
        var colls = typeof(Game).GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic)
            .Where(f => typeof(System.Collections.ICollection).IsAssignableFrom(f.FieldType) && f.Name != "Bullets" && f.Name != "Enemies" && f.Name != "Sounds").ToArray();
        Func<bool, WeaponId, Showcase, HashSet<string>> run = (alt, id, show) =>
        {
            var seen = new HashSet<string>();
            ShowcaseAlt.Off = !alt;
            for (int i = 0; i < (int)(20f / dt); i++)
            {
                show.Step(dt);
                var g = show.Game;
                foreach (var f in fields)
                    seen.Add(f.Name + "=" + f.GetValue(g.Me));
                foreach (var f in floats)
                    if ((float)f.GetValue(g.Me) > 0f)
                        seen.Add(f.Name + ">0");
                foreach (var m in g.Minions.Values)
                    if (m.Alpha > 0)
                        seen.Add("alpha wolf");
                foreach (var b in g.Bullets.Values)
                    seen.Add("bullet " + b.Kind);
                foreach (var c in colls)
                {
                    var v = (System.Collections.ICollection)c.GetValue(g);
                    if (v != null && v.Count > 0)
                        seen.Add("has " + c.Name);
                }
                seen.Add("bullets>" + Math.Min(40, g.Bullets.Count / 8));
                seen.Add("enemies " + g.Enemies.Count);
                foreach (var e in g.Enemies.Values)
                {
                    seen.Add("hp " + (int)(e.Hp / 40f));
                    if (e.Burning) seen.Add("burning");
                    if (e.Penned) seen.Add("penned");
                }
                seen.Add("pos " + (int)(g.Me.Pos.X / 30f) + "," + (int)(g.Me.Pos.Y / 30f));
            }
            ShowcaseAlt.Off = false;
            return seen;
        };
        foreach (Role role in new[] { Role.Dps, Role.Tank, Role.Healer })
            foreach (Kit kit in Enum.GetValues(typeof(Kit)))
                for (int w = 0; w < Defs.WeaponCount(role, kit); w++)
                {
                    var id = Defs.Weapon(role, kit, w).Id;
                    if (!ShowcaseAlt.Has(id) || id == WeaponId.SpiritChalk)
                        continue;
                    var on = new Showcase { AltFire = true };
                    on.Show(role, kit, w, Upgrades.FirstAbility(role, kit, 0), false);
                    var a = run(true, id, on);
                    on.Stop();
                    var off = new Showcase { AltFire = true };
                    off.Show(role, kit, w, Upgrades.FirstAbility(role, kit, 0), false);
                    var b = run(false, id, off);
                    off.Stop();
                    var extra = a.Where(x => !b.Contains(x) && !x.StartsWith("pos ") && !x.StartsWith("hp ") && !x.StartsWith("enemies")).ToList();
                    Console.WriteLine("  {0} ({1}/{2}): {3}", id, role, kit, string.Join(", ", extra.Take(6)));
                    Check(extra.Count > 0, "the preview's Alt shows on the " + id);
                }
    }
}

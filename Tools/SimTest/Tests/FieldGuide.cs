using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The pause menu's Field guide: the in-game Bestiary's data (BestiaryData) and the numbers it shows.
static partial class Program
{
    static void FieldGuideTest(float dt)
    {
        var all = BestiaryData.All;
        var missing = new List<string>();
        var doubled = new List<string>();
        foreach (EnemyType t in Enum.GetValues(typeof(EnemyType)))
        {
            if (t == EnemyType.TwinB)
                continue;  // shares the Twin Reapers' entry
            if (t == EnemyType.Buzzsaw || t == EnemyType.SpikeBlock || t == EnemyType.Spiker
                || t == EnemyType.FuseRunner || t == EnemyType.Hookman || t == EnemyType.Sapper || t == EnemyType.SapperMine
                || t == EnemyType.Swordsman || t == EnemyType.ShieldSwordsman)
                continue;  // TODO: no Bestiary entry yet; the old Swarmer entry is still filed under the new Swarmer (Bestiary pages only change when the owner asks; remove this then; Docs/AfterReset.md)
            int n = all.Count(e => e.Type == t);
            if (n == 0) missing.Add(t.ToString());
            if (n > 1) doubled.Add(t.ToString());
        }
        Console.WriteLine("  {0} Bestiary entries{1}", all.Length, missing.Count > 0 ? "; missing: " + string.Join(", ", missing) : "");
        Check(missing.Count == 0 && doubled.Count == 0 && BestiaryData.For(EnemyType.TwinB) == BestiaryData.For(EnemyType.TwinA),
            "every enemy type has one Bestiary entry (the second Twin Reaper shares the first's)");
        Check(all.All(e => BestiaryData.Groups.Contains(e.Group) && !string.IsNullOrEmpty(e.Lead) && !string.IsNullOrEmpty(e.Appears) && e.Wave >= 1 && e.Wave <= Game.MaxWave),
            "each entry has a known group, a description, when it appears and a first wave");
        var texts = all.SelectMany(e => new[] { e.Lead, e.Appears }.Concat(e.Notes).Concat(e.Attacks.SelectMany(a => new[] { a.Pattern, a.Notes })));
        Check(!texts.Any(x => x != null && x.IndexOf("preview", StringComparison.OrdinalIgnoreCase) >= 0),
            "no entry talks about the web page's preview");
        var groupOrder = all.Select(e => Array.IndexOf(BestiaryData.Groups, e.Group)).ToArray();
        Check(groupOrder.Zip(groupOrder.Skip(1), (a, b) => a <= b).All(x => x), "the Bestiary lists its groups in order");

        // The lobby's Damage stat: every weapon has a measured damage a second (run.sh 1 1 weapondps write).
        var unmeasured = Defs.Weapons.Where(w => Defs.WeaponDps(w.Id) <= 0f).Select(w => w.Name).ToList();
        Check(unmeasured.Count == 0, "every weapon has a measured damage a second for the lobby" + (unmeasured.Count > 0 ? " (missing: " + string.Join(", ", unmeasured) + ")" : ""));

        // The health the Bestiary shows is what spawns.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var s = StartBuffed(dt, 12, 0);
        var g = s[0].Game;
        Run(s, dt, 3f, () => g.State == MatchState.Fighting);
        bool same = true;
        foreach (var t in new[] { EnemyType.Grunt, EnemyType.Juggernaut, EnemyType.Boss, EnemyType.Eclipse })
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { t, new V2(40f, 40f), 0f, 0, false });
            same &= Math.Abs(e.MaxHp - g.SpawnHp(t, g.Wave)) < 0.01f;
            e.Hp = 0f;
        }
        Check(same, "the Bestiary's health at your wave matches what spawns");
        Check(Math.Abs(Game.WaveFireRate(1) - 1f) < 1e-4f && Math.Abs(Game.WaveFireRate(11) - 1.5f) < 1e-4f && Game.WaveFireRate(40) == 2f,
            "enemies fire 5% faster a wave, up to twice as fast");
        s[0].Leave(""); s[1].Leave("");
    }
}

using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Numbers the lobby and the waves rely on: every weapon's measured damage, the spawn health, the fire-rate ramp.
static partial class Program
{
    static void EnemyNumbersTest(float dt)
    {
        // The lobby's Damage stat: every weapon has a measured damage a second (run.sh 1 1 weapondps write).
        var unmeasured = Defs.Weapons.Where(w => w.Calling == CallingId.None && Defs.WeaponDps(w.Id) <= 0f).Select(w => w.Name).ToList();  // (a Calling's weapon is never in the lobby: SoldierCallings.cs)
        Check(unmeasured.Count == 0, "every weapon has a measured damage a second for the lobby" + (unmeasured.Count > 0 ? " (missing: " + string.Join(", ", unmeasured) + ")" : ""));

        // The health a spawn is given matches SpawnHp.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var s = StartBuffed(dt, 12, 0);
        var g = s[0].Game;
        Run(s, dt, 3f, () => g.State == MatchState.Fighting);
        bool same = true;
        foreach (var t in new[] { EnemyType.Grunt, EnemyType.Thunderhead, EnemyType.Warden, EnemyType.Eclipse })
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { t, new V2(40f, 40f), 0f, 0, false });
            same &= Math.Abs(e.MaxHp - g.SpawnHp(t, g.Wave)) < 0.01f;
            e.Hp = 0f;
        }
        Check(same, "the health at your wave matches what spawns");
        Check(Math.Abs(Game.WaveFireRate(1) - 1f) < 1e-4f && Math.Abs(Game.WaveFireRate(11) - 1.5f) < 1e-4f && Game.WaveFireRate(40) == 2f,
            "enemies fire 5% faster a wave, up to twice as fast");
        s[0].Leave(""); s[1].Leave("");
    }
}

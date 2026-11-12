using System;
using System.Linq;
using Arena;

static partial class Program
{
    // The session recording (SessionRecorder.SourceName) credits a weapon's hits to "Weapon: <name>" whether they are
    // bullets, swings, beams, lobbed shots or companion bites, and counts blood costs as health spent, not damage taken.
    // (Tools/SimTest/run.sh 1 1 weaponsrc lists every weapon.)
    static void SessionSourcesTest(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        string folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arena-sessionsrc-" + Guid.NewGuid().ToString("N").Substring(0, 6));
        foreach (var id in new[] { WeaponId.LeechBeam, WeaponId.ArcKnuckles, WeaponId.Lance, WeaponId.Katana, WeaponId.WolfWhistle, WeaponId.ClotBolt })
        {
            var weapon = Defs.Weapons.First(w => w.Id == id);
            SessionRecorder rec = null;
            var src = new System.Collections.Generic.Dictionary<string, float>();
            foreach (float hold in Game.ChargeWeapon(id) ? new[] { 0.6f, 0.3f, 1.2f } : new[] { 0f })
            foreach (float dist in new[] { 0.55f, 0.3f, 0.8f })  // a lobbed clot can fall short of or past the dummy at one range
                if (src.Count == 0)
                {
                    rec = WeaponSources(dt, spawn, folder, weapon.Role, weapon.Kit, weapon.Choice, weapon, hold, dist);
                    src = rec.DamageBySource(1);
                }
            float total = src.Values.Sum(), mine;
            src.TryGetValue("Weapon: " + weapon.Name, out mine);
            Check(total > 0f && mine >= 0.9f * total, string.Format("{0}: {1:0}% of its {2:0} damage is credited to the weapon", weapon.Name, 100f * mine / Math.Max(1f, total), total));
            if (id == WeaponId.ClotBolt)
                Check(rec.HealthSpentBy(1) > 0f && rec.DamageTakenBy(1) == 0f, string.Format("the Clot Cannon's shots cost health ({0:0}), recorded as health spent, not damage taken", rec.HealthSpentBy(1)));
        }
        try { System.IO.Directory.Delete(folder, true); } catch { }
    }
}

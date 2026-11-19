using System;
using System.Collections.Generic;

namespace Arena
{
    /// A weapon skin: a look for one weapon, earned at an expertise level of the weapon's class. Id is stored in the profile:
    /// never change one once players have it. Tint is the colour the Look window's preview and thumbnail draw its shots in.
    public class WeaponSkinDef
    {
        public string Id, Name;
        public WeaponId Weapon;
        public int Level;  // the class expertise level that unlocks it (1 to Expertise.MaxLevel)
        public Rgba Tint = new Rgba(1f, 0.85f, 0.45f);
    }

    /// Class expertise: every finished run's XP also goes to the class you played (Profile.AwardXp), and its level unlocks that
    /// class's weapon skins. Cosmetic only, like the account rank (Rank.cs). Not ability mastery (Mastery.cs), which is per run.
    /// Plain C#, no Unity.
    public static class Expertise
    {
        public const int MaxLevel = 10;

        /// The colour of the default look, for the preview and the "Standard issue" row.
        public static readonly Rgba StandardTint = new Rgba(1f, 0.85f, 0.45f);

        /// XP to get from this level to the next (level 0 to 1 costs 1,000; 9 to 10 costs 10,000: 55,000 in all).
        public static int NeedFor(int level) => 1000 * (Math.Max(0, level) + 1);

        public struct Info
        {
            public int Level;
            public long Into;  // XP earned towards the next level (0 at the top)
            public int Need;   // XP the next level costs (0 at the top)
            public bool Maxed => Level >= MaxLevel;
            public float Fraction => Maxed ? 1f : Need > 0 ? Math.Min(1f, (float)Into / Need) : 0f;
        }

        public static Info Of(long xp)
        {
            int level = 0;
            xp = Math.Max(0, xp);
            while (level < MaxLevel && xp >= NeedFor(level))
            {
                xp -= NeedFor(level);
                level++;
            }
            return level >= MaxLevel ? new Info { Level = MaxLevel } : new Info { Level = level, Into = xp, Need = NeedFor(level) };
        }

        /// The profile key of a class's expertise XP ("Soldier").
        public static string Key(Kit kit) => kit.ToString();

        // --- Weapon skins ----------------------------------------------------------------------------

        /// Every weapon skin. None yet: add a def here (and its look where the weapon is drawn) to give a weapon one.
        public static WeaponSkinDef[] Skins = new WeaponSkinDef[0];  // (not readonly: the rank test swaps in its own)

        public static WeaponSkinDef Skin(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            foreach (var s in Skins)
                if (s.Id == id)
                    return s;
            return null;
        }

        /// A weapon's skins, lowest level first.
        public static List<WeaponSkinDef> For(WeaponId weapon)
        {
            var l = new List<WeaponSkinDef>();
            foreach (var s in Skins)
                if (s.Weapon == weapon)
                    l.Add(s);
            l.Sort((a, b) => a.Level.CompareTo(b.Level));
            return l;
        }

        /// The class a weapon belongs to, whose expertise unlocks its skins.
        public static Kit ClassOf(WeaponId weapon)
        {
            var w = Defs.WeaponOf(weapon);
            return w != null ? w.Kit : Kit.Soldier;
        }

        /// The next skin a class's weapons unlock above this level, or null.
        public static WeaponSkinDef NextFor(Kit kit, int level)
        {
            WeaponSkinDef best = null;
            foreach (var s in Skins)
                if (ClassOf(s.Weapon) == kit && s.Level > level && (best == null || s.Level < best.Level))
                    best = s;
            return best;
        }
    }
}

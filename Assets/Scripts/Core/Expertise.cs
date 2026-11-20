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

        public const string RubberChicken = "twin-chicken";

        /// Every weapon skin. To add one: a def here (new ones at the end: the index is sent), its look where the weapon is drawn,
        /// and any sounds it swaps. The Rubber Chicken: Game/ChickenLook.cs, Game/ChickenSoundBank.cs, Game.TwinSfx.
        public static WeaponSkinDef[] Skins =  // (not readonly: the rank test swaps in its own)
        {
            new WeaponSkinDef { Id = RubberChicken, Name = "Rubber Chicken", Weapon = WeaponId.TwinBlades, Level = 3, Tint = new Rgba(1f, 0.83f, 0.18f) },
        };

        /// A skin's number on the wire: its place in Skins plus one (0 the standard look).
        public static byte IndexOf(string id)
        {
            for (int i = 0; i < Skins.Length && i < 254; i++)
                if (Skins[i].Id == id)
                    return (byte)(i + 1);
            return 0;
        }

        public static WeaponSkinDef At(int index) => index >= 1 && index <= Skins.Length ? Skins[index - 1] : null;

        public static readonly byte[] NoSkins = new byte[0];
        public const int MaxSent = 16;  // skins a player sends at most (pairs)

        /// A player's equipped skins as sent in the lobby: pairs of (weapon, skin index), only the ones the profile owns.
        public static byte[] Pack(Profile p)
        {
            var l = new List<byte>();
            foreach (var s in Skins)
                if (l.Count < MaxSent * 2 && p.SkinOf(s.Weapon) == s.Id)
                {
                    l.Add((byte)s.Weapon);
                    l.Add(IndexOf(s.Id));
                }
            return l.ToArray();
        }

        /// Pairs read off the wire, keeping only skins that exist and belong to the weapon they are paired with.
        public static byte[] Clean(IList<byte> raw)
        {
            var l = new List<byte>();
            for (int i = 0; i + 1 < raw.Count && l.Count < MaxSent * 2; i += 2)
            {
                var s = At(raw[i + 1]);
                if (s != null && (byte)s.Weapon == raw[i])
                {
                    l.Add(raw[i]);
                    l.Add(raw[i + 1]);
                }
            }
            return l.ToArray();
        }

        /// The skin id a lobby entry's pairs give a weapon ("" the standard look).
        public static string SkinIn(byte[] pairs, WeaponId weapon)
        {
            for (int i = 0; pairs != null && i + 1 < pairs.Length; i += 2)
                if (pairs[i] == (byte)weapon)
                {
                    var s = At(pairs[i + 1]);
                    return s != null ? s.Id : "";
                }
            return "";
        }

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

    public partial class Game
    {
        /// The skin a player shows on a weapon, from their lobby entry ("" the standard look, or no entry: a bot).
        public string SkinOf(int playerId, WeaponId weapon)
        {
            LobbyEntry e;
            return session != null && session.Lobby.TryGetValue(playerId, out e) ? Expertise.SkinIn(e.Skins, weapon) : "";
        }

        /// Host: a Twin Blades cue in the swinger's skin's sound pack (the Rubber Chicken squeaks; any other weapon keeps it).
        internal Sfx TwinSfx(Player p, Sfx s)
        {
            if (p == null || p.Weapon.Id != WeaponId.TwinBlades || SkinOf(p.Id, WeaponId.TwinBlades) != Expertise.RubberChicken)
                return s;
            switch (s)
            {
                case Sfx.TwinCut1: return Sfx.ChickCut1;
                case Sfx.TwinCut2: return Sfx.ChickCut2;
                case Sfx.TwinJab: return Sfx.ChickJab;
                case Sfx.TwinSpin: return Sfx.ChickSpin;
                case Sfx.TwinShear: return Sfx.ChickShear;
                case Sfx.TwinHit: return Sfx.ChickHit;
                case Sfx.TwinHitHeavy: return Sfx.ChickHitHeavy;
                case Sfx.TwinThrow: return Sfx.ChickThrow;
                case Sfx.TwinCross: return Sfx.ChickCross;
                case Sfx.TwinCatch: return Sfx.ChickCatch;
                default: return s;
            }
        }
    }
}

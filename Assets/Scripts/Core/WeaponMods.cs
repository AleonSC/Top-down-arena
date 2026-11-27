using System;
using System.Collections.Generic;

namespace Arena
{
    /// How a weapon's damage lands (WeaponDef.Kind). A card can do something different for each kind (UpgradeDef.ByKind).
    public enum WeaponKind : byte { Shot, Area, Cone, Melee }

    /// The weapon mods, so code and weapons can name one (UpgradeDef.Mod, WeaponDef.Mods). Never sent: cards go by
    /// their index in Upgrades.All.
    public enum ModId : byte { None, Cryo, Explosive, Homing, Incendiary, Mirror, Ricochet, Shock, Velocity }

    /// What a card does for one weapon kind (UpgradeDef.ByKind) or one weapon (WeaponDef.Mods). Anything left
    /// unset falls back: a weapon's entry, then its kind's, then the card's own Desc and Apply.
    public class KindEffect
    {
        public string Desc;          // the card text; "{weapon}" is the weapon's name, "{area}" its AreaDesc
        public Action<Stats> Apply;  // the stats it gives
        public bool Off;             // never offered: it would do nothing for this weapon
    }

    /// The weapon mods: one picked after every boss, from every mod your weapon can use (Upgrades.ModOffer), and
    /// sold by the Arms Dealer. To change what a mod does for a kind of weapon, edit its ByKind row here; for one
    /// weapon, give that weapon a Mods entry (WeaponMods.Say, WeaponMods.As, WeaponMods.Without).
    public static class WeaponMods
    {
        /// Homing Rounds on a melee weapon: swings reach this much farther a stack (Knight.MeleeReach).
        public const float HomingReachPerStack = 0.1f;

        /// The cards, in their place in Upgrades.All (their order there is their id: keep it).
        public static UpgradeDef[] Cards() => new[]
        {
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Homing, MaxStacks = 3, Name = "Homing Rounds", Desc = "Shots bend toward an enemy close ahead of them (fixes near misses), but deal 5% less damage per stack. More stacks bend harder",
                Apply = s => s.Homing += 2f,
                ByKind = Kinds(
                    WeaponKind.Area, new KindEffect { Desc = "{weapon}: {area} per stack, instead of bending shots. 5% less damage per stack" },
                    WeaponKind.Cone, new KindEffect { Desc = "{weapon}: the cone 15% narrower and 10% stronger per stack, instead of bending shots" },
                    WeaponKind.Melee, new KindEffect { Desc = "Your swings reach 10% farther per stack, so near misses connect, and sword waves bend toward enemies close ahead (5% less wave damage per stack)",
                        Apply = s => { s.Homing += 2f; s.HomingReach += HomingReachPerStack; } }) },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Ricochet, MaxStacks = 3, Name = "Ricochet", Desc = "Shots bounce to another nearby enemy after a hit (40% damage). +1 bounce per stack. Weapons whose hits can't bounce fling a bounce bolt instead",
                Apply = s => s.Ricochet += 1,
                ByKind = Kinds(
                    WeaponKind.Melee, new KindEffect { Desc = "Your hits fling a bounce bolt at another nearby enemy (40% of the damage, up to 4 a second), and sword waves bounce. +1 bounce per stack" }) },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Incendiary, MaxStacks = 3, Name = "Incendiary Rounds", Desc = "Hits set enemies on fire: each burns for another 40% of the hit's damage over 3 s, and burns from every hit add up",
                Apply = s => s.BurnFrac += 0.4f },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Cryo, MaxStacks = 3, Name = "Cryo Rounds", Desc = "Hits chill enemies (20% slower) with a 6% chance to freeze them for 1 s",
                Apply = s => { s.CryoSlow = Math.Min(0.6f, s.CryoSlow + 0.2f); s.FreezeChance += 0.06f; } },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Shock, MaxStacks = 3, Name = "Shock Rounds", Desc = "25% chance per hit for lightning to arc to 2 more enemies (60% damage)",
                Apply = s => s.ShockChance += 0.25f },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Explosive, MaxStacks = 3, Name = "Explosive Rounds", Desc = "Shots explode on impact, dealing 30% damage to enemies nearby",
                Apply = s => s.ExplosiveRadius += s.ExplosiveRadius == 0f ? 40f : 15f,
                ByKind = Kinds(
                    WeaponKind.Melee, new KindEffect { Desc = "Your hits explode, dealing 30% damage to enemies nearby" }) },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Mirror, MaxStacks = 3, Name = "Mirror Guard", Desc = "Each bullet your parry blocks has a 25% chance to fly back at enemies",
                Apply = s => s.MirrorChance = Math.Min(0.75f, s.MirrorChance + 0.25f) },
            new UpgradeDef { Rarity = Rarity.Mod, Mod = ModId.Velocity, MaxStacks = 3, Name = "Velocity Rounds", Desc = "Shots fly 30% faster and farther and deal 25% more damage",
                Apply = s => { s.ShotSpeedMult += 0.3f; s.DmgMult += 0.25f; },
                ByKind = Kinds(
                    WeaponKind.Melee, new KindEffect { Desc = "Sword waves fly 30% faster and farther, and everything you do deals 25% more damage" }) },
        };

        /// A ByKind table from (kind, effect) pairs.
        public static Dictionary<WeaponKind, KindEffect> Kinds(params object[] pairs)
        {
            var d = new Dictionary<WeaponKind, KindEffect>();
            for (int i = 0; i + 1 < pairs.Length; i += 2)
                d[(WeaponKind)pairs[i]] = (KindEffect)pairs[i + 1];
            return d;
        }

        /// A weapon's Mods entry: what the mod does for it, in its own words (after "<weapon>: ").
        public static Dictionary<ModId, KindEffect> Say(ModId mod, string desc) =>
            new Dictionary<ModId, KindEffect> { [mod] = new KindEffect { Desc = "{weapon}: " + desc } };

        /// A weapon's Mods entry: the mod works for it as for another kind (a melee weapon whose hits land in an area
        /// takes Homing Rounds' area version: Heart Flail, Twin Tessen).
        public static Dictionary<ModId, KindEffect> As(ModId mod, WeaponKind kind)
        {
            var u = Card(mod);
            KindEffect k;
            if (u.ByKind == null || !u.ByKind.TryGetValue(kind, out k))
                k = new KindEffect();
            return new Dictionary<ModId, KindEffect> { [mod] = new KindEffect { Desc = k.Desc ?? u.Desc, Apply = k.Apply ?? u.Apply, Off = k.Off } };  // (all filled in, so its own kind's row can't show through)
        }

        /// A weapon's Mods entry: the mod is never offered for it.
        public static Dictionary<ModId, KindEffect> Without(ModId mod) =>
            new Dictionary<ModId, KindEffect> { [mod] = new KindEffect { Off = true } };

        static UpgradeDef[] cards;

        /// The card of a weapon mod: its own copy, as weapon tables read it while Upgrades.All may still be building.
        public static UpgradeDef Card(ModId mod)
        {
            if (cards == null)
                cards = Cards();
            foreach (var u in cards)
                if (u.Mod == mod)
                    return u;
            throw new ArgumentException("No weapon mod " + mod);
        }

        /// The weapon's own entry for this card, then its kind's; null if neither has one.
        static KindEffect Own(UpgradeDef u, WeaponDef w)
        {
            KindEffect k;
            return u.Mod != ModId.None && w.Mods != null && w.Mods.TryGetValue(u.Mod, out k) ? k : null;
        }

        static KindEffect OfKind(UpgradeDef u, WeaponDef w)
        {
            KindEffect k;
            return u.ByKind != null && u.ByKind.TryGetValue(w.Kind, out k) ? k : null;
        }

        /// The card does nothing for this weapon, so it isn't offered.
        public static bool Off(UpgradeDef u, WeaponDef w)
        {
            if (w == null)
                return false;
            var own = Own(u, w);
            if (own != null)
                return own.Off;
            var kind = OfKind(u, w);
            return kind != null && kind.Off;
        }

        /// The stats this card gives with this weapon (null: none).
        public static Action<Stats> ApplyFor(UpgradeDef u, WeaponDef w)
        {
            if (w == null)
                return u.Apply;
            var own = Own(u, w);
            if (own != null && own.Apply != null)
                return own.Apply;
            var kind = OfKind(u, w);
            return kind != null && kind.Apply != null ? kind.Apply : u.Apply;
        }

        /// The card text for this weapon, or null for the card's own.
        public static string DescFor(UpgradeDef u, WeaponDef w)
        {
            var own = Own(u, w);
            string d = own != null && own.Desc != null ? own.Desc : null;
            if (d == null)
            {
                var kind = OfKind(u, w);
                d = kind != null ? kind.Desc : null;
            }
            return d == null ? null : d.Replace("{weapon}", w.Name).Replace("{area}", w.AreaDesc ?? "");
        }
    }
}

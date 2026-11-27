using System;

namespace Arena
{
    /// A class's own hooks into the game loop. Each class keeps its KitDef beside its code (Weapons.cs: Soldier,
    /// Minions.cs: Elementalist and Summoner, Knight.cs, Ninja.cs, Hemomancer.cs, Engineer.cs, Arcanist.cs); Classes.All gathers them in Kit order.
    /// Every hook is optional: leave it null when the class has nothing to do there.
    public delegate void KitFireFn(Game g, Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra);
    public delegate bool KitAbilityFn(Game g, Player p, AbilityId id, V2 target);
    public delegate void KitTickFn(Game g, float dt);
    public delegate void KitEnemyFn(Game g, Enemy e, float dt);
    public delegate void KitHitFn(Game g, Bullet b, Enemy e, float hit);
    public delegate void KitKillFn(Game g, Enemy e);

    public class KitDef
    {
        public Kit Id;
        public string Name;           // what players see ("Elementalist" for Kit.Elementalist)
        public string Blurb;          // the lobby detail box's description of it (a few sentences)
        public string Icon;           // its PixelArt icon (the lobby's class card)
        public float HpMult = 1f;     // share of the role's health
        public float SpeedMult = 1f;  // share of the role's speed
        public float DodgeCdMult = 1f;     // share of the Dodge's cooldown (Game.DodgeCd; Dodge.cs)
        public float SprintBoostMult = 1f; // share of the sprint's speed boost (Game.SprintBoost)
        public float SprintTimeMult = 1f;  // share of the sprint's length (Game.SprintTime)

        // --- Host hooks (the host runs the run; clients only draw) ---
        /// Fires the class's weapons that have no case of their own in FireVolley (Weapons.cs).
        public KitFireFn Fire;
        /// Casts what CastAbility's own cases don't: its 1st abilities. Return false for an id that isn't the class's.
        public KitAbilityFn Ability;
        /// Every host tick (HostTick, after the players move), whether or not anyone plays the class: its timers and effects.
        public KitTickFn Tick;
        /// Every enemy, every host tick (TickEnemies, with the other status effects): the class's marks on enemies.
        public KitEnemyFn TickEnemy;
        /// A player's shot has just hurt an enemy for `hit` (Collide). Check the bullet's kind or owner: it runs for every shot.
        public KitHitFn OnShotHit;
        /// An enemy has just died (Collide), before the kill's rewards.
        public KitKillFn OnKill;
    }

    public static class Classes
    {
        /// A class's three lists: its KitDef, its weapons and its 1st abilities (and cards).
        public struct KitLists
        {
            public KitDef Kit; public WeaponDef[] Weapons; public AbilityDef[] Abilities;
            public KitLists(KitDef kit, WeaponDef[] weapons, AbilityDef[] abilities) { Kit = kit; Weapons = weapons; Abilities = abilities; }
        }

        /// **The one place a class is registered**, in Kit order: Defs.Weapons, Upgrades.Abilities and Classes.All are all built from it.
        static readonly KitLists[] Lists =
        {
            new KitLists(Game.SoldierKit, Game.SoldierWeapons, Game.SoldierAbilities),
            new KitLists(Game.ElementalistKit, Game.ElementalistWeapons, Game.ElementalistAbilities),
            new KitLists(Game.SummonerKit, Game.SummonerWeapons, Game.SummonerAbilities),
            new KitLists(Game.KnightKit, Game.KnightWeapons, Game.KnightAbilities),
            new KitLists(Game.NinjaKit, Game.NinjaWeapons, Game.NinjaAbilities),
            new KitLists(Game.HemomancerKit, Game.HemomancerWeapons, Game.HemomancerAbilities),
            new KitLists(Game.EngineerKit, Game.EngineerWeapons, Game.EngineerAbilities),
            new KitLists(Game.ArcanistKit, Game.ArcanistWeapons, Game.ArcanistAbilities),
        };

        /// Every class's weapon list, for Defs.Weapons.
        internal static WeaponDef[][] WeaponLists()
        {
            var all = new WeaponDef[Lists.Length][];
            for (int i = 0; i < all.Length; i++) all[i] = Lists[i].Weapons;
            Array.Resize(ref all, all.Length + 1);
            all[all.Length - 1] = Game.SoldierCallingWeapons;  // the Soldier's Calling weapons (no lobby place: SoldierCallings.cs)
            return all;
        }

        /// Every class's ability list, for Upgrades.Abilities.
        internal static AbilityDef[][] AbilityLists()
        {
            var all = new AbilityDef[Lists.Length][];
            for (int i = 0; i < all.Length; i++) all[i] = Lists[i].Abilities;
            return all;
        }

        /// Every class, in Kit order. The game refuses to start if one is missing, doubled or out of place.
        public static readonly KitDef[] All = Registry(Lists[0].Kit, Lists[1].Kit, Lists[2].Kit, Lists[3].Kit, Lists[4].Kit, Lists[5].Kit, Lists[6].Kit, Lists[7].Kit);

        public static KitDef Get(Kit k) => All[(int)k];

        // The hooks that are set, gathered once so the game loop doesn't walk empty ones.
        internal static readonly KitAbilityFn[] AbilityHooks = Hooks(k => k.Ability);
        internal static readonly KitTickFn[] TickHooks = Hooks(k => k.Tick);
        internal static readonly KitEnemyFn[] EnemyHooks = Hooks(k => k.TickEnemy);
        internal static readonly KitHitFn[] HitHooks = Hooks(k => k.OnShotHit);
        internal static readonly KitKillFn[] KillHooks = Hooks(k => k.OnKill);

        static T[] Hooks<T>(Func<KitDef, T> pick) where T : class
        {
            var list = new System.Collections.Generic.List<T>();
            foreach (var k in All)
                if (pick(k) != null)
                    list.Add(pick(k));
            return list.ToArray();
        }

        static KitDef[] Registry(params KitDef[] kits)
        {
            int count = Enum.GetValues(typeof(Kit)).Length;
            if (kits.Length != count || count != Defs.KitCount)
                throw new InvalidOperationException("Kit has " + count + " classes, Defs.KitCount says " + Defs.KitCount + " and Classes.All lists " + kits.Length);
            for (int i = 0; i < kits.Length; i++)
            {
                var k = kits[i];
                if (k == null || (int)k.Id != i)
                    throw new InvalidOperationException("Classes.All[" + i + "] should be Kit." + (Kit)i + "'s KitDef");
                if (string.IsNullOrEmpty(k.Name) || string.IsNullOrEmpty(k.Blurb) || string.IsNullOrEmpty(k.Icon))
                    throw new InvalidOperationException("Kit." + k.Id + "'s KitDef needs a Name, Blurb and Icon");
            }
            return kits;
        }
    }
}

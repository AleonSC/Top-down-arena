using System;

namespace Arena
{
    /// A class's own hooks into the game loop. Each class keeps its KitDef beside its code (Weapons.cs: Soldier,
    /// Minions.cs: Mage and Summoner, Knight.cs, Ninja.cs, BloodMage.cs); Classes.All gathers them in Kit order.
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
        public string Blurb;          // the lobby's and the Field guide's one line about it
        public string Icon;           // its PixelArt icon (the lobby's class card)
        public float HpMult = 1f;     // share of the role's health
        public float SpeedMult = 1f;  // share of the role's speed

        // --- Host hooks (the host runs the match; clients only draw) ---
        /// Fires the class's weapons that have no case of their own in FireVolley (Weapons.cs).
        public KitFireFn Fire;
        /// Casts what CastAbility's own cases don't: its Space abilities. Return false for an id that isn't the class's.
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
        /// Every class, in Kit order. The game refuses to start if one is missing, doubled or out of place.
        public static readonly KitDef[] All = Registry(Game.SoldierKit, Game.ElementalistKit, Game.SummonerKit, Game.KnightKit, Game.NinjaKit, Game.HemomancerKit);

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

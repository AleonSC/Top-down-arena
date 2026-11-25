using System;

namespace Arena
{
    /// Armor Drop: the Tank Soldier's fourth ultimate (key 3 of the lobby's Ultimate row). A smoke flare at your cursor (up to
    /// `ArmorReach` away) calls in a steel crate: for `ArmorCallIn` s a red ring marks where it lands, then it slams down for
    /// `ArmorImpactDmg` to every enemy within `ArmorBlast` and staggers them. Its sides fall open over `ArmorOpen` s and it kicks
    /// out `ArmorVests` ballistic vests (mastered `ArmorVestsMastered`) that lie on the floor for `ArmorVestLife` s.
    /// Walking over a vest sets your ballistic armor (`Player.Armor`, replicated) to `ArmorCap` (it never stacks past it; a vest
    /// you can't use stays for someone else). Armor soaks damage after the Juggernaut Suit's and before shields and health
    /// (`ArmorSoak`, from `Damage` in Combat.cs), never regenerates, and is kept between waves until it is shot off.
    /// One `ZoneKind.ArmorDrop` zone (its replicated timer gives every screen the phase: flare, landing, open, fold); it folds
    /// away once every vest is taken or gone. The vests are `Orb`s with `Vest` set (replicated), `Supply` the zone's id.
    /// Look: Game/ArmorDropLook.cs. Mockup: https://claude.ai/artifact/4CUNVxkwG8EgAe3tNViHpC. Test suite `armordrop`; preview
    /// `ARMORDROP=<s since the cast>`.
    public partial class Game
    {
        public const float ArmorReach = 520f, ArmorCallIn = 1.6f, ArmorOpen = 0.6f, ArmorFold = 0.6f;
        public const float ArmorBlast = 110f, ArmorImpactDmg = 60f, ArmorStagger = 0.4f;
        public const float ArmorCap = 50f;              // what one vest gives (and the most you can wear)
        public const float ArmorVestLife = 20f;         // how long an unclaimed vest lies on the floor
        public const float ArmorVestMin = 70f, ArmorVestMax = 110f;  // how far out a vest lands
        public const int ArmorVests = 3, ArmorVestsMastered = 4;
        public const float ArmorVestR = 13f;            // a vest's pickup size

        /// Its entry: key 3 of the Tank Soldier's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] ArmorUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.ArmorDrop, Kit = Kit.Soldier, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Armor Drop", Cooldown = 80f,
                Desc = "Call in a crate that hits enemies where it lands, then opens: 3 ballistic vests, 50 armor each. Armor never regenerates.",
                Capstone = "The crate holds a 4th vest.",
                Bot = b => b.HaveTarget && b.TargetD < 520f,
            },
        };

        /// Seconds since the flare was thrown, from the zone's replicated timer.
        public static float ArmorAge(Zone z) => z.Duration - z.T;

        /// The crate has landed.
        public static bool ArmorLanded(Zone z) => ArmorAge(z) >= ArmorCallIn;

        /// This player wears ballistic armor.
        public static bool Armored(Player p) => p.Armor > 0f && !p.Downed;

        /// Host: throw the flare.
        void CastArmorDrop(Player p, V2 target, bool cap)
        {
            V2 to = target - p.Pos;
            if (to.Length > ArmorReach)
                to = to.Normalized() * ArmorReach;
            V2 at = ClampToArena(p.Pos + to, 60f);
            var z = AddZone(ZoneKind.ArmorDrop, p, at, ArmorBlast, ArmorCallIn + ArmorOpen + ArmorVestLife + ArmorFold);
            z.Side = cap ? ArmorVestsMastered : ArmorVests;  // (host: how many vests it holds)
            BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.35f, 0.4f), ArmorBlast, 0.5f, Sfx.Throw);
        }

        /// Host, each tick for an Armor Drop zone: the landing, the vests, folding once they're all gone.
        void TickArmorDrop(Zone z, float dt)
        {
            float age = ArmorAge(z), before = age - dt, open = ArmorCallIn + ArmorOpen * 0.5f;
            if (before < ArmorCallIn && age >= ArmorCallIn)
                ArmorImpact(z);
            if (before < open && age >= open)
                for (int i = 0; i < z.Side; i++)
                    ThrowVest(z, i);
            if (age < open + 0.1f || z.T <= ArmorFold)
                return;
            foreach (var o in Orbs.Values)
                if (o.Supply == z.Id)
                    return;
            z.T = ArmorFold;  // every vest taken: the crate folds away
        }

        void ArmorImpact(Zone z)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Pos.FartherThan(z.Pos, ArmorBlast + e.Radius))
                    continue;
                HurtEnemy(e, ArmorImpactDmg * z.DmgMult, z.Owner);
                if (!e.Def.IsBoss)
                {
                    Stun(e, ArmorStagger);
                    V2 away = e.Pos - z.Pos;
                    e.Pos = ClampToArena(e.Pos + (away.IsZero ? RandDir() : away.Normalized()) * 40f, e.Radius);
                }
            }
            BroadcastFx(FxKind.Ring, z.Pos, new Rgba(1f, 0.8f, 0.35f), ArmorBlast, 0.45f, Sfx.Explode);
        }

        /// One vest out of the crate, spread evenly round it.
        void ThrowVest(Zone z, int i)
        {
            float a = -MathX.Pi / 2f + MathX.Tau * i / Math.Max(1, z.Side) + Rand(-0.25f, 0.25f);
            V2 at = ClampToArena(z.Pos + V2.FromAngle(a) * Rand(ArmorVestMin, ArmorVestMax), 16f);
            var orb = new Orb { Id = nextOrbId++, Vest = true, Supply = z.Id, Pos = at, Life = ArmorVestLife };
            orb.Disp = z.Pos;  // it flies out of the crate
            Orbs[orb.Id] = orb;
        }

        /// From TickOrbs: a vest lies still and goes to the first player over it who isn't already at full armor.
        /// Returns true when it was taken.
        bool TickVest(Orb o)
        {
            foreach (var p in Players.Values)
            {
                if (p.Downed || p.Armor >= ArmorCap || !p.Pos.CloserThan(o.Pos, p.Def.BodyR + ArmorVestR))
                    continue;
                p.Armor = ArmorCap;
                BroadcastFx(FxKind.Ring, o.Pos, new Rgba(0.62f, 0.85f, 1f), 46f, 0.45f, Sfx.PickupBig);
                return true;
            }
            return false;
        }

        /// From Damage: the vest armor takes what it can; returns what is left for shields and health.
        float ArmorSoak(Player p, float amount)
        {
            if (p.Armor <= 0f || amount <= 0f)
                return amount;
            float soak = Math.Min(p.Armor, amount);
            p.Armor -= soak;
            if (p.Armor <= 0.01f)
            {
                p.Armor = 0f;
                BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.62f, 0.85f, 1f), 34f, 0.35f, Sfx.Clang);  // the shell cracks off
            }
            return amount - soak;
        }
    }
}

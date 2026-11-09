using System;

namespace Arena
{
    /// Bloodbath (the Tank Hemomancer's second ultimate, key 1 of the lobby's Ultimate row; Vein Tithe stays the default), from
    /// Docs/Mockups/ultimate-ideas.html (Tank > Hemomancer, the first card) and Docs/Mockups/bloodbath.html.
    /// You pay BathCost of your max health up front (AbilityDef.HealthCost, so PayAbilityCost takes it like a Space ability's: never
    /// below 1 HP, and at 1 HP it still goes off). Blood floods the floor round where you cast: a `ZoneKind.Bloodbath` zone of
    /// BathRadius (mastered BathRadiusMastered) that grows out over BathGrow s, stays BathTime s and doesn't follow you.
    ///   Enemies wading in it are slowed (BathSlow; Heavy enemies BathSlowHeavy and bosses BathSlowBoss, through `Enemy.WadeMult`, as the usual
    ///   slow skips them) and bleed BathBleed a second,
    ///   bosses too, as a hit from you every BathTick s (so your damage multiplier, Blood Price and the rest scale it).
    ///   Every tick heals you BathHeal (mastered BathHealMastered) of the damage it dealt, wherever you are; those ticks don't also
    ///   give the class's usual 3% (bathTick, BloodLifesteal). Allies standing in it, you included, take BathGuard less damage.
    ///   It ends when its time runs out or between waves (WaveCleared clears the zones); going down doesn't end it.
    /// Its own tick rather than the shared bleed (ApplyBleed): that bleed stacks and lingers BleedTime s after the last hit, and
    /// heals through the normal 3%, while this one must stop the moment an enemy steps out and pay back its own share.
    /// Replicated as an ordinary zone (Pos, Radius, T, Duration), so every screen grows, ripples and dries it the same
    /// (Game/BloodbathLook.cs); each tick's heal is sent as `FxKind.BathHeal` for the small numbers over you.
    public partial class Game
    {
        public const float BathCost = 0.15f, BathCooldown = 75f;
        public const float BathRadius = 170f, BathRadiusMastered = 220f, BathGrow = 0.9f, BathTime = 10f;
        public const float BathSlow = 0.5f, BathSlowHeavy = 0.35f, BathSlowBoss = 0.2f;
        public const float BathBleed = 15f, BathTick = 0.5f, BathHeal = 0.25f, BathHealMastered = 0.35f, BathGuard = 0.2f;
        public const float BathStart = 0.12f;  // the pool's size the moment it's cast, as a share of its full size
        public const float BathDry = 1.2f;     // the last seconds, when the look dries it up (it still works until it's gone)
        public const float BathHealFxTime = 0.9f;
        public static readonly Rgba BathRed = new Rgba(0.55f, 0.03f, 0.08f);

        bool bathTick;  // a Bloodbath tick is dealing its damage: BloodLifesteal gives no 3% on it
        readonly System.Collections.Generic.List<Enemy> bathScratch = new System.Collections.Generic.List<Enemy>();

        /// How far the pool has spread: from BathStart of its size to all of it over BathGrow s (easing out).
        public static float BathReach(Zone z)
        {
            float x = Math.Max(0f, Math.Min(1f, (z.Duration - z.T) / BathGrow));
            float ease = 1f - (1f - x) * (1f - x) * (1f - x);
            return z.Radius * (BathStart + (1f - BathStart) * ease);
        }

        /// Is something of this radius at pos wading in the pool?
        public static bool InBath(Zone z, V2 pos, float radius) => z.Kind == ZoneKind.Bloodbath && z.T > 0f && pos.DistanceTo(z.Pos) <= BathReach(z) + radius;

        /// How much an enemy in the pool is slowed: bosses least, Heavy enemies less than the rest.
        public static float BathSlowFor(Enemy e) => e.Def.IsBoss ? BathSlowBoss : e.Def.Heavy ? BathSlowHeavy : BathSlow;

        /// Damage cut for a player standing in any Bloodbath (Damage).
        float BathCut(Player p)
        {
            foreach (var z in Zones.Values)
                if (InBath(z, p.Pos, 0f))
                    return BathGuard;
            return 0f;
        }

        void CastBloodbath(Player p, bool cap)
        {
            var z = AddZone(ZoneKind.Bloodbath, p, p.Pos, cap ? BathRadiusMastered : BathRadius, BathTime);
            z.HealBack = cap ? BathHealMastered : BathHeal;
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), z.Radius, 0.6f, Sfx.BossSplat);
        }

        /// Host, every tick, for each Bloodbath (TickZones): slow what's in it, and every BathTick s bleed it and heal the owner.
        void TickBloodbath(Zone z, float dt)
        {
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f || !InBath(z, e.Pos, e.Radius))
                    continue;
                if (Heavy(e))  // (LateGame.cs: Heavy enemies and bosses) the usual slow skips them, so they wade instead
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - BathSlowFor(e));
                }
                else  // the usual slow, so what cares about slowed enemies (Graviton Rounds) counts it
                {
                    e.SlowT = Math.Max(e.SlowT, 0.2f);
                    e.SlowMult = Math.Min(e.SlowMult, 1f - BathSlowFor(e));
                }
            }
            z.TickT += dt;
            while (z.TickT >= BathTick)
            {
                z.TickT -= BathTick;
                BathBleedTick(z);
            }
        }

        void BathBleedTick(Zone z)
        {
            float heal = 0f, dmg = BathBleed * BathTick * z.DmgMult;
            bathScratch.Clear();
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && InBath(z, e.Pos, e.Radius))
                    bathScratch.Add(e);
            foreach (var e in bathScratch)
            {
                float before = e.Hp;
                bathTick = true;
                try
                {
                    HurtEnemy(e, dmg, z.Owner);
                }
                finally
                {
                    bathTick = false;
                }
                heal += Math.Max(0f, before - Math.Max(0f, e.Hp)) * z.HealBack;
            }
            bathScratch.Clear();
            Player p;
            if (heal <= 0f || !Players.TryGetValue(z.Owner, out p))
                return;
            float healed = Heal(p, heal, p, "Bloodbath");  // nothing while you're down; the pool goes on
            if (healed >= 0.5f)
                BroadcastFx(FxKind.BathHeal, p.Pos, new V2(p.Id, 0f), new Rgba(1f, 0.35f, 0.4f), healed, BathHealFxTime, Sfx.None);
        }
    }
}

using System;

namespace Arena
{
    /// Honey Raid: the Healer Summoner's second ultimate (key 1 of the lobby's Ultimate row; World Tree stays the default), from the
    /// mockup (claude.ai artifact "Honey Raid").
    ///   - Flight (HrFly s): a giant bee comes in from HrRun px behind you and flies straight over the spot you aimed at (your cursor,
    ///     kept within HrRange px), dripping honey on the way: a honey puddle (ZoneKind.HoneyPuddle, HrDripR px, HrDripTime s, the
    ///     usual 40% slow) every HrDripEvery px, from the arena's edge up to HrDripStop px short of the spot.
    ///   - Drop (HrDrop s): over the spot it dumps its whole load; the glob falls and splashes: HrSplashDmg to every enemy within
    ///     HrSplash px. The bee flies on off the map (drawn for HrLeave s).
    ///   - Pool (HrPool s): a HrRadius px honey pool (mastered HrRadiusMastered) spreading out over HrSpread s. Enemies wading in it are
    ///     slowed (HrSlow; Heavy enemies HrSlowHeavy and bosses HrSlowBoss, through Enemy.WadeMult as in a Bloodbath) and take HrDps a
    ///     second, as a hit from you every HrTick s (your damage scales it); allies standing in it heal HrHeal a second.
    ///   - Set: HrSet s before it ends the honey sets once: every enemy in it but bosses is stuck (a HrStuck s stun).
    /// It is one `ZoneKind.HoneyRaid` zone: Pos the spot (the pool's centre), Origin where the bee came in (replicated), Radius the
    /// pool; the bee's place is worked out from those and the age, so every screen flies it the same (Game/HoneyRaidLook.cs).
    public partial class Game
    {
        public const float HrFly = 1.6f, HrDrop = 0.4f, HrPool = 8f, HrLeave = 1.2f, HrSpread = 0.3f, HrSet = 1.5f, HrCooldown = 80f;
        public const float HrRange = 600f, HrRun = 900f, HrRadius = 240f, HrRadiusMastered = 300f;
        public const float HrSplash = 120f, HrSplashDmg = 40f, HrDps = 12f, HrTick = 0.5f, HrHeal = 10f, HrStuck = 1f;
        public const float HrSlow = 0.55f, HrSlowHeavy = 0.35f, HrSlowBoss = 0.2f;
        public const float HrDripEvery = 70f, HrDripR = 32f, HrDripTime = 4f, HrDripStop = 90f;

        /// Honey Raid's entry: key 1 of the Healer Summoner's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] HoneyUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.HoneyRaid, Kit = Kit.Summoner, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Honey Raid", Cooldown = HrCooldown,
                Desc = "A giant bee flies over your cursor and dumps its honey: a big pool that slows and hurts enemies and heals allies.",
                Capstone = "The pool is 25% wider.",
                Bot = b => b.HaveTarget && b.TargetD < HrRange,
            },
        };

        /// The whole show: the flight, the drop and the pool.
        public const float HrTotal = HrFly + HrDrop + HrPool;

        /// When the glob lands and the pool starts.
        public const float HrLandAt = HrFly + HrDrop;

        /// Seconds since the cast.
        public static float HrAge(Zone z) => z.Duration - z.T;

        /// Has the pool landed?
        public static bool HrLanded(Zone z) => HrAge(z) >= HrLandAt;

        /// Has the honey set (its last HrSet s)?
        public static bool HrSetting(Zone z) => HrLanded(z) && z.T <= HrSet;

        /// The pool's reach now: from a third of its size to all of it over HrSpread s (easing out); 0 before it lands.
        public static float HrReach(Zone z)
        {
            float a = HrAge(z) - HrLandAt;
            if (a < 0f)
                return 0f;
            float x = Math.Min(1f, a / HrSpread), ease = 1f - (1f - x) * (1f - x);
            return z.Radius * (0.35f + 0.65f * ease);
        }

        /// Is something of this radius at pos in the pool?
        public static bool InHoneyRaid(Zone z, V2 pos, float radius) => z.Kind == ZoneKind.HoneyRaid && HrLanded(z) && z.T > 0f && pos.WithinReach(z.Pos, HrReach(z) + radius * 0.5f);

        /// Where the bee is (over the ground) at `age` s: in from Origin, over Pos at HrFly, then on the same way at the same speed.
        public static V2 HrBeeAt(Zone z, float age)
        {
            V2 d = z.Pos - z.Origin;
            float len = d.Length;
            if (len < 1f)
                return z.Pos;
            return z.Origin + d / len * (len * age / HrFly);
        }

        /// The bee's heading.
        public static float HrHeading(Zone z) => (z.Pos - z.Origin).LengthSq > 1f ? (z.Pos - z.Origin).Angle : 0f;

        /// How much an enemy in the pool is slowed: bosses least, Heavy enemies less than the rest.
        public static float HrSlowFor(Enemy e) => e.Def.IsBoss ? HrSlowBoss : e.Def.Heavy ? HrSlowHeavy : HrSlow;

        void CastHoneyRaid(Player p, V2 target, bool cap)
        {
            V2 off = target - p.Pos;
            if (off.Length > HrRange)
                target = p.Pos + off.Normalized() * HrRange;
            target = ClampToArena(target, 30f);
            V2 dir = (target - p.Pos).LengthSq > 1f ? (target - p.Pos).Normalized() : V2.FromAngle(p.Aim);
            var z = AddZone(ZoneKind.HoneyRaid, p, target, cap ? HrRadiusMastered : HrRadius, HrTotal);
            z.Origin = target - dir * HrRun;  // behind you, usually off the map
            z.Leg = 0;          // drips dropped so far
            z.Landed = false;   // the glob has splashed
            z.Congealed = false;  // the honey has set
            z.TickT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.HoneyRaid).Color, 46f, 0.5f, Sfx.Summon);
        }

        /// Host, every tick, for each Honey Raid (TickZones; the drips on the way are HoneyRaidDrips): the splash, the pool's slow, burn and heal, the set.
        void TickHoneyRaid(Zone z, float dt)
        {
            float age = HrAge(z);
            Player owner;
            Players.TryGetValue(z.Owner, out owner);
            if (!z.Landed)
            {
                if (age < HrLandAt)
                    return;
                z.Landed = true;
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && e.Pos.WithinReach(z.Pos, HrSplash + e.Radius))
                        HurtEnemy(e, HrSplashDmg * z.DmgMult, z.Owner);
                BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.HoneyRaid).Color, z.Radius, 0.5f, Sfx.BossSplat);
            }
            bool set = !z.Congealed && HrSetting(z);
            if (set)
            {
                z.Congealed = true;
                BroadcastFx(FxKind.Ring, z.Pos, new Rgba(1f, 0.94f, 0.75f), z.Radius, 0.35f, Sfx.None);
            }
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !InHoneyRaid(z, e.Pos, e.Radius))
                    continue;
                if (Heavy(e))  // the usual slow skips Heavy enemies and bosses, so they wade instead (as in a Bloodbath)
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - HrSlowFor(e));
                }
                else
                {
                    Slow(e, 0.2f, 1f - HrSlowFor(e));
                }
                if (set && !e.Def.IsBoss)
                    Stun(e, HrStuck);
            }
            foreach (var q in Players.Values)
                if (!q.Downed && InHoneyRaid(z, q.Pos, 0f))
                    Heal(q, HrHeal * z.HealMult * dt, owner);
            z.TickT += dt;
            while (z.TickT >= HrTick)
            {
                z.TickT -= HrTick;
                hrScratch.Clear();
                foreach (var e in Enemies.Values)
                    if (Targetable(e) && InHoneyRaid(z, e.Pos, e.Radius))
                        hrScratch.Add(e);
                foreach (var e in hrScratch)
                    HurtEnemy(e, HrDps * HrTick * z.DmgMult, z.Owner);
                hrScratch.Clear();
            }
        }

        /// Host, before the zone loop (TickZones): each flying bee drips a honey puddle every HrDripEvery px it has flown, inside the
        /// arena, up to HrDripStop px short of the spot. (Zone.Leg counts the drips so far.)
        void HoneyRaidDrips()
        {
            hrRaids.Clear();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.HoneyRaid && !z.Landed)
                    hrRaids.Add(z);
            foreach (var z in hrRaids)
            {
                Player owner;
                if (!Players.TryGetValue(z.Owner, out owner))
                    continue;
                float run = (z.Pos - z.Origin).Length, flown = run * Math.Min(1f, HrAge(z) / HrFly);
                while ((z.Leg + 1) * HrDripEvery <= Math.Min(flown, run - HrDripStop))
                {
                    z.Leg++;
                    V2 at = z.Origin + (z.Pos - z.Origin) / Math.Max(1f, run) * (z.Leg * HrDripEvery);
                    if ((ClampToArena(at, 10f) - at).LengthSq < 1f)
                        AddZone(ZoneKind.HoneyPuddle, owner, at, HrDripR, HrDripTime);
                }
            }
            hrRaids.Clear();
        }

        readonly System.Collections.Generic.List<Zone> hrRaids = new System.Collections.Generic.List<Zone>();
        readonly System.Collections.Generic.List<Enemy> hrScratch = new System.Collections.Generic.List<Enemy>();
    }
}

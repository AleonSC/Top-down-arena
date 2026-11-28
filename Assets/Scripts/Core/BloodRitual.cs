using System;

namespace Arena
{
    /// Blood Ritual: the DPS Hemomancer's fourth ultimate (key 3 of the lobby's Ultimate row; Crimson Harvest stays the default,
    /// Blood Ravager is key 1, Blood Boil key 2). Docs/Mockups/blood-ritual.html. A `ZoneKind.BloodRitual` zone at the cursor
    /// (clamped to RitReach), RitRadius across, that runs through its phases by age (Duration - T, replicated):
    ///   - Inscribe (RitInscribe s): a pentagram is bled onto the floor; RitCost of your max health is paid over it (never below
    ///     the blood floor). Going down, or the slot changing, while it's still being drawn breaks it (nothing more happens).
    ///   - Open (RitOpen s): the sigil flares and caves into a portal. From here until the close, enemies on it are slowed RitSlow
    ///     (Heavy enemies and bosses wade, as in a Bloodbath).
    ///   - Flood (RitFlood s, mastered RitFloodMastered): RitBurst blood imps burst out at once, then RitRate a second, up to RitTotal
    ///     (mastered RitTotalMastered) in all and RitAlive at once.
    ///   - Close (RitClose s): the portal shuts; every imp still out flies home into it, and each that makes it heals you RitPayback
    ///     of your max health (`FxKind.Drain` from the portal to you).
    /// A blood imp (`MinionKind.BloodImp`, TickBloodImp) climbs out over RitEmerge s (Minion.Scale grows), then dives at the nearest
    /// enemy with the fewest imps on it and circles it, clawing for its Damage every Interval s (each claw adds a bleed stack,
    /// RitBleed a second) and taunting it onto itself for RitTaunt s (bosses ignore the taunt): its walking and shots go at the imp
    /// (ImpTarget), and the imp's BlockRadius stops the shots, which wear its BlockCapacity. The enemy it clings to also swats it,
    /// RitSwat a second (twice that for a boss). An imp worn out pops: RitPopDamage to enemies within RitPopRadius.
    /// The imp's look reads Minion.Stacks (0 climbing out, 1 flying, 2 clawing, 3 going home), Scale and Wind (a claw's flash).
    public partial class Game
    {
        public const float RitCooldown = 80f, RitCost = 0.15f, RitReach = 320f, RitRadius = 90f;
        public const float RitInscribe = 1.2f, RitOpen = 0.4f, RitFlood = 8f, RitFloodMastered = 10f, RitClose = 0.8f;
        public const int RitBurst = 8, RitTotal = 40, RitTotalMastered = 50, RitAlive = 24;
        public const float RitRate = 4f, RitSlow = 0.4f, RitEmerge = 0.45f;
        public const float RitBleed = 4f, RitTaunt = 1f, RitSwat = 8f, RitPopDamage = 10f, RitPopRadius = 40f, RitPayback = 0.01f;
        public const float RitImpRing = 8f;        // how far out from an enemy's edge an imp circles it
        public const float RitHunt = 700f;         // how far an imp looks for something to claw
        public const float RitCrowd = 90f;         // px an imp counts each imp already on an enemy as, picking its prey
        public const float RitHomeSpeed = 420f;
        public static readonly Rgba RitRed = new Rgba(0.86f, 0.1f, 0.2f);

        internal static readonly AbilityDef[] RitualUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodRitual, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Blood Ritual", Cooldown = RitCooldown,
                Desc = "Bleed a pentagram onto the floor that tears open and floods out blood imps to claw and taunt enemies.",
                Capstone = "The portal stays open 10 s and pours out 50 imps.",
                Bot = b => b.NearEnemies >= 3 && b.Me.Hp > b.Me.MaxHp * 0.45f,
            },
        };

        /// Seconds since the ritual was cast.
        public static float RitAge(Zone z) => z.Duration - z.T;
        /// When the portal opens (the inscribe is done) and when the imps start pouring out.
        public const float RitOpenAt = RitInscribe, RitFloodAt = RitInscribe + RitOpen;
        /// When the portal shuts and the imps fly home.
        public static float RitCloseAt(Zone z) => z.Duration - RitClose;
        /// The portal is open (from the flare to the close): it slows what stands on it.
        public static bool RitOpenNow(Zone z) => z.Kind == ZoneKind.BloodRitual && RitAge(z) >= RitOpenAt && RitAge(z) < RitCloseAt(z);

        void CastRitual(Player p, V2 target, bool cap)
        {
            V2 at = ClampToArena(p.Pos + (target - p.Pos).LimitLength(RitReach), RitRadius * 0.6f);
            var z = AddZone(ZoneKind.BloodRitual, p, at, RitRadius, RitInscribe + RitOpen + (cap ? RitFloodMastered : RitFlood) + RitClose);
            z.Cap = cap;
            BroadcastFx(FxKind.Ring, at, RitRed, RitRadius, 0.5f, Sfx.BossSplat);
        }

        /// Blood imps out of this ritual right now.
        int RitImpsOut(Zone z)
        {
            int n = 0;
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.BloodImp && m.Head == z.Id)
                    n++;
            return n;
        }

        /// Host, every tick, for each Blood Ritual (TickZones): the price while it's drawn, then the slow and the imps.
        void TickRitual(Zone z, float dt)
        {
            Player p;
            bool owned = Players.TryGetValue(z.Owner, out p);
            float age = RitAge(z);
            if (age < RitInscribe)
            {
                if (!owned || p.Downed || p.Slots[Upgrades.UltSlot].Id != AbilityId.BloodRitual)
                {
                    z.T = Math.Min(z.T, 0.001f);  // broken while drawn: it's gone
                    return;
                }
                if (!p.DevGod)
                    PayHealth(p, p.MaxHp * RitCost * dt / RitInscribe, "Blood Ritual");
                return;
            }
            if (!z.Landed)
            {
                z.Landed = true;  // the flare as it opens
                BroadcastFx(FxKind.Ring, z.Pos, RitRed, RitRadius * 1.4f, 0.45f, Sfx.Summon);
            }
            if (!RitOpenNow(z))
                return;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || !e.Pos.WithinReach(z.Pos, z.Radius + e.Radius * 0.5f))
                    continue;
                if (Heavy(e))  // the usual slow skips Heavy enemies and bosses, so they wade (as in a Bloodbath)
                {
                    e.WadeT = Math.Max(e.WadeT, 0.2f);
                    e.WadeMult = Math.Min(e.WadeMult, 1f - RitSlow);
                }
                else
                    Slow(e, 0.2f, 1f - RitSlow);
            }
            if (age < RitFloodAt || !owned)
                return;
            int total = z.Cap ? RitTotalMastered : RitTotal;
            if (z.Side == 0)
            {
                for (int i = 0; i < RitBurst && z.Side < total; i++)
                    SpawnRitImp(z, p);
                BroadcastFx(FxKind.Ring, z.Pos, RitRed, RitRadius, 0.35f, Sfx.None);
                return;
            }
            z.TickT += dt * RitRate;
            while (z.TickT >= 1f)
            {
                z.TickT -= 1f;
                if (z.Side < total && RitImpsOut(z) < RitAlive)
                    SpawnRitImp(z, p);
            }
        }

        void SpawnRitImp(Zone z, Player p)
        {
            V2 at = z.Pos + V2.FromAngle(RandAngle()) * (z.Radius * 0.55f * (float)Math.Sqrt(rng.NextDouble()));
            var m = SpawnMinion(MinionKind.BloodImp, p, at, z.T + 2f);
            m.Head = z.Id;
            m.Launch = RitEmerge;
            m.Scale = 0.2f;
            m.Power = z.DmgMult;
            m.Cd = Rand(0f, m.Def.Interval);
            m.Aim = -MathX.Pi / 2f;
            m.Stacks = 0;
            z.Side++;
        }

        /// The enemy an imp goes for: the nearest within RitHunt, counting each imp already on one as RitCrowd px farther.
        Enemy RitPrey(Minion m)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(m.Pos, RitHunt))
                    continue;
                int on = 0;
                foreach (var o in Minions.Values)
                    if (o.Kind == MinionKind.BloodImp && o != m && o.GoalEnemy == e.Id)
                        on++;
                float d = m.Pos.DistanceTo(e.Pos) + on * RitCrowd;
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host, each tick for a blood imp (TickMinions, instead of its usual moves). True when it's gone (home, popped, or its
        /// portal gone).
        bool TickBloodImp(Minion m, Player owner, float dt)
        {
            Zone z;
            if (!Zones.TryGetValue(m.Head, out z) || z.Kind != ZoneKind.BloodRitual)
                return true;  // its portal is gone (the wave cleared): it melts away
            var d = m.Def;
            m.Wind = Math.Max(0f, m.Wind - dt * 4f);
            if (m.Returning || RitAge(z) >= RitCloseAt(z))
            {
                m.Returning = true;
                m.Stacks = 3;
                V2 to = z.Pos - m.Pos;
                float len = to.Length;
                if (len <= RitHomeSpeed * dt + 6f)
                {
                    if (!owner.Downed)
                    {
                        Heal(owner, owner.MaxHp * RitPayback, owner, "Blood Ritual");
                        BroadcastFx(FxKind.Drain, z.Pos, owner.Pos, RitRed, 2.5f, 0.6f, Sfx.None);
                    }
                    return true;
                }
                m.Pos += to / len * (RitHomeSpeed * dt);
                m.Aim = to.Angle;
                m.Scale = Math.Max(0.35f, Math.Min(1f, len / 60f));
                return false;
            }
            if (m.Launch > 0f)  // climbing out of the portal
            {
                m.Launch -= dt;
                m.Pos += new V2(0f, -22f * dt);
                m.Scale = 0.2f + 0.8f * MathX.Clamp01(1f - m.Launch / RitEmerge);
                m.Stacks = 0;
                return false;
            }
            m.Scale = 1f;
            Enemy e;
            if (m.GoalEnemy == 0 || !Enemies.TryGetValue(m.GoalEnemy, out e) || !Targetable(e))
            {
                e = RitPrey(m);
                m.GoalEnemy = e != null ? e.Id : 0;
            }
            if (e == null)  // nothing to claw: it circles over the portal
            {
                m.Ang += 2f * dt;
                V2 goal = z.Pos + V2.FromAngle(m.Ang + m.Id) * (z.Radius * 0.7f);
                V2 step = (goal - m.Pos).LimitLength(d.Speed * dt);
                if (step.Length > 0.5f)
                    m.Aim = step.Angle;
                m.Pos = ClampToArena(m.Pos + step, d.Radius);
                m.Stacks = 1;
                return false;
            }
            V2 spot = e.Pos + V2.FromAngle(m.Ang) * (e.Radius + RitImpRing);
            V2 toSpot = spot - m.Pos;
            if (toSpot.Length > d.Speed * dt)  // flying at it
            {
                m.Pos += toSpot.Normalized() * (d.Speed * dt);
                m.Aim = toSpot.Angle;
                m.Stacks = 1;
                return false;
            }
            m.Ang += 1.6f * dt;  // on it: circling and clawing
            m.Pos = ClampToArena(e.Pos + V2.FromAngle(m.Ang) * (e.Radius + RitImpRing), d.Radius);
            m.Aim = (e.Pos - m.Pos).Angle;
            m.Stacks = 2;
            m.Cd -= dt;
            if (m.Cd <= 0f)
            {
                m.Cd = d.Interval;
                HurtEnemy(e, d.Damage * m.Power, owner.Id);
                ApplyBleed(e, RitBleed * m.Power, owner.Id, true);
                if (!e.Def.IsBoss)
                {
                    e.ImpTauntId = m.Id;
                    e.ImpTauntT = RitTaunt;
                }
                m.Wind = 1f;
                if (m.BiteN++ % 2 == 0)
                    BroadcastFx(FxKind.Slash, m.Pos, e.Pos, new Rgba(1f, 0.75f, 0.78f), 0.9f, 0.18f, Sfx.None);
            }
            m.BlockHp -= RitSwat * (e.Def.IsBoss ? 2f : 1f) * dt;  // the enemy it clings to swats at it
            if (m.BlockHp <= 0f)
            {
                RitImpPop(m);
                return true;
            }
            return false;
        }

        /// A blood imp worn out (shot or swatted): it pops in a splash that hurts the enemies round it.
        void RitImpPop(Minion m)
        {
            foreach (var e in FoesInCircle(m.Pos, RitPopRadius, by: m.Owner))
                HurtEnemy(e, RitPopDamage * m.Power, m.Owner);
            BroadcastFx(FxKind.Boom, m.Pos, RitRed, RitPopRadius * 0.6f, 0.3f, Sfx.None);
        }

        /// Host, from TickEnemies: a regular enemy a blood imp clawed in the last RitTaunt s goes for the imp, walking and shooting.
        V2 ImpTarget(Enemy e, V2 tpos)
        {
            if (e.ImpTauntT <= 0f || e.Def.IsBoss)
                return tpos;
            Minion m;
            return Minions.TryGetValue(e.ImpTauntId, out m) && (m.Kind == MinionKind.BloodImp || m.Kind == MinionKind.BroodQueen) ? m.Pos : tpos;  // (a Brood Queen taunts the same way: BroodQueen.cs)
        }
    }
}

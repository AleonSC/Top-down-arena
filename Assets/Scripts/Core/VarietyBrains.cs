using System;
using System.Collections.Generic;

namespace Arena
{
    /// Step 8b of the enemy behaviour classes (Docs/EnemyBehaviours.md): the Variety enemies (Variety.cs): Beam Sentry,
    /// Scorcher, Reaver, Warder, Pauser, Leader, Stitcher. Each one's code moved here unchanged out of VarietyMove,
    /// VarietySpecial and VarietyFire; its parts (CodeMove, CodeSpecial, CodeFire) call it.
    public partial class Game
    {
        /// (moved from VarietyMove, BeamSentry)
        V2 VarietyMove_BeamSentry(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            return e.Phase == 0 ? KeepDistance(dir, dist, SentryRange) + dir.Orthogonal() * e.Strafe * 0.3f : V2.Zero;
        }

        /// (moved from VarietyMove, Scorcher)
        V2 VarietyMove_Scorcher(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            // round you, drawing a ring of embers
            return dir.Orthogonal() * e.Strafe + KeepDistance(dir, dist, ScorchRange) * 0.8f;
        }

        /// (moved from VarietyMove, Reaver)
        V2 VarietyMove_Reaver(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            return e.Phase == 0 ? dir : V2.Zero;
        }

        /// (moved from VarietyMove, Pauser)
        V2 VarietyMove_Pauser(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            return KeepDistance(dir, dist, PauserRange) + dir.Orthogonal() * e.Strafe * 0.4f;
        }

        /// (moved from VarietyMove, Leader)
        V2 VarietyMove_Leader(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            // quick on its feet, circling at range
            return e.Phase == 1 ? V2.Zero : KeepDistance(dir, dist, LeaderRange) + dir.Orthogonal() * e.Strafe * 0.7f;
        }

        /// (moved from VarietyMove, Stitcher)
        V2 VarietyMove_Stitcher(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            return KeepDistance(dir, dist, StitcherRange) + dir.Orthogonal() * e.Strafe * 0.3f;
        }

        /// (moved from VarietyMove, Warder)
        V2 VarietyMove_Warder(Enemy e, V2 tpos, V2 dir, float dist, ref float speed)
        {
            // Behind its allies, between them and you.
            V2 sum = V2.Zero;
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o != e && !o.Def.IsBoss && o.Type != EnemyType.Warder && o.Hp > 0f && o.Pos.DistanceTo(e.Pos) < 600f)
                {
                    sum += o.Pos;
                    n++;
                }
            if (n == 0)
                return KeepDistance(dir, dist, SentryRange);
            V2 mid = sum / n, away = (mid - tpos).IsZero ? -dir : (mid - tpos).Normalized();  // the straight line, not the way round a wall
            V2 goal = mid + away * 160f;
            return RouteVel(e, goal, (goal - e.Pos) / 80f);  // round walls and crates (LayoutRules.cs)
        }

        /// (moved from VarietySpecial, BeamSentry)
        void VarietySpecial_BeamSentry(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos);
            if (e.Phase == 0 && e.T > 1.5f && dist > SentryRange - 70f && dist < SentryRange + 70f)
            {
                e.Phase = 1;
                e.PhaseT = 0.6f;
            }
            else if (e.Phase == 1 && e.PhaseT <= 0f)
            {
                e.Phase = 2;
                e.PhaseT = SentryAim;
                e.Facing = aim;
                BroadcastSound(Sfx.BossCharge);
            }
            else if (e.Phase == 2)
            {
                float diff = MathX.Fmod(aim - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
                e.Facing += Math.Max(-SentryTurn * dt, Math.Min(SentryTurn * dt, diff));
                if (e.PhaseT <= 0f)
                {
                    // It sweeps toward the side you were heading: it starts a little behind you on the other.
                    e.Strafe = diff >= 0f ? 1 : -1;
                    e.Facing -= e.Strafe * 0.5f;
                    e.Phase = 3;
                    e.PhaseT = SentrySweep;
                }
            }
            else if (e.Phase == 3)
            {
                e.Facing += e.Strafe * SentrySweepArc / SentrySweep * dt;
                V2 tip = e.Pos + V2.FromAngle(e.Facing) * SentryBeamLen;
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && SegmentDistance(p.Pos, e.Pos, tip) < SentryBeamWidth / 2f + p.Def.HitR)
                        Damage(p, SentryDamage, e.Def.Name + " (beam)");
                if (e.PhaseT <= 0f)
                {
                    e.Phase = 4;
                    e.PhaseT = SentryRest / TraitFireMult(e);
                }
            }
            else if (e.Phase == 4 && e.PhaseT <= 0f)
            {
                // Still in range? Aim again; otherwise walk on.
                e.Phase = dist < SentryRange + 200f ? (byte)2 : (byte)0;
                e.PhaseT = SentryAim;
            }
        }

        /// (moved from VarietySpecial, Leader)
        void VarietySpecial_Leader(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            if (e.Phase == 1)
                e.Facing = LeadAngle(e, tpos);  // the aim line follows where you'll be
        }

        /// (moved from VarietySpecial, Scorcher)
        void VarietySpecial_Scorcher(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            e.PhaseT -= dt;
            if (e.PhaseT <= 0f)
            {
                e.PhaseT = EmberEvery;
                SpawnPatchAt(PatchKind.Embers, e.Pos, 0.15f, EmberLife, EmberRadius, EmberRadius);
            }
        }

        /// (moved from VarietySpecial, Reaver)
        void VarietySpecial_Reaver(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos);
            if (e.Phase == 0 && dist < ReaverSwipeR + e.Radius)
            {
                e.Phase = 1;
                e.PhaseT = ReaverWindup;
                e.Facing = aim;
            }
            else if (e.Phase == 1 && e.PhaseT <= 0f)
            {
                // The rake: everyone in the cone.
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    V2 rel = p.Pos - e.Pos;
                    float off = Math.Abs(MathX.Fmod(rel.Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi);
                    if (rel.Length > ReaverSwipeR + e.Radius + p.Def.HitR || off > ReaverCone)
                        continue;
                    if (p.ParryActive && ParryContact(p, e, ReaverDamage))
                        continue;  // a raised guard takes it
                    Damage(p, ReaverDamage, e.Def.Name + " (swipe)");
                    RodTouched(p, e);  // LightningRod.cs
                }
                BroadcastFx(FxKind.Claw, e.Pos, e.Pos + V2.FromAngle(e.Facing) * (ReaverSwipeR + e.Radius), e.Def.Color, ReaverCone, 0.25f, Sfx.SwingHeavy);
                e.Phase = 3;
                e.PhaseT = ReaverOpen;
            }
            else if (e.Phase == 3 && e.PhaseT <= 0f)
                e.Phase = 0;
        }

        /// (moved from VarietySpecial, Warder)
        void VarietySpecial_Warder(Enemy e, V2 tpos, float dt)
        {
            float aim = (tpos - e.Pos).Angle;
            e.WardT -= dt;
            e.Phase = (byte)(e.WardT <= WarderCharge ? 1 : 0);  // charging: drawn only, the timing is unchanged
            if (e.WardT <= 0f)
            {
                e.WardT = WarderEvery;
                e.Phase = 0;
                var near = new List<Enemy>();
                foreach (var o in Enemies.Values)
                    if (o != e && !o.Def.IsBoss && o.Type != EnemyType.Warder && Targetable(o) && o.Pos.DistanceTo(e.Pos) < WarderRange)
                        near.Add(o);
                near.Sort((a, b) => a.Pos.DistanceSq(e.Pos).CompareTo(b.Pos.DistanceSq(e.Pos)));
                for (int i = 0; i < near.Count && i < WarderMax; i++)
                {
                    var o = near[i];
                    o.WardId = e.Id;
                    o.BubbleMax = Math.Max(o.BubbleMax, o.MaxHp * WarderShare);
                    o.BubbleHp = Math.Max(o.BubbleHp, o.MaxHp * WarderShare);
                }
                if (near.Count > 0)
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, WarderRange, 0.5f, Sfx.Ability);
            }
        }

        /// (moved from VarietyFire, BeamSentry)
        bool VarietyFire_BeamSentry(Enemy e, float aim)
        {
            e.FireT = 99f;  // its beam is its attack (VarietySpecial)
            return true;
        }

        /// (moved from VarietyFire, Scorcher)
        bool VarietyFire_Scorcher(Enemy e, float aim)
        {
            e.FireT = 2.5f;
            EnemyShot(e, aim - 0.1f, 170f, Defs.SmallShot);
            EnemyShot(e, aim + 0.1f, 170f, Defs.SmallShot);
            return true;
        }

        /// (moved from VarietyFire, Warder)
        bool VarietyFire_Warder(Enemy e, float aim)
        {
            e.FireT = 3f;
            EnemyShot(e, aim - 0.2f, 120f, Defs.BigShot);
            EnemyShot(e, aim + 0.2f, 120f, Defs.BigShot);
            return true;
        }

        /// (moved from VarietyFire, Pauser)
        bool VarietyFire_Pauser(Enemy e, float aim)
        {
            e.FireT = PauseEvery;
            for (int i = 0; i < PauseShots; i++)
            {
                V2 dir = V2.FromAngle(aim + PauseFan * (i / (PauseShots - 1f) - 0.5f));
                var b = SpawnBullet(Defs.PauseShot, e.Pos + dir * e.Radius, dir * PauseSpeed, 0f, PauseFly + PauseHold, Defs.BulletDamage(Defs.PauseShot), 0);
                b.Source = e.Type;
                b.Rush = true;
            }
            return true;
        }

        /// (moved from VarietyFire, Leader)
        bool VarietyFire_Leader(Enemy e, float aim)
        {
            if (e.Phase == 0)
            {
                // Aim first: a thin line that follows where you'll be.
                e.Phase = 1;
                e.FireT = LeaderAim;
                e.Facing = aim;
                return true;
            }
            e.Phase = 0;
            e.FireT = LeaderEvery;
            EnemyShot(e, e.Facing - LeaderSpread, LeaderSpeed, Defs.FastShot);
            EnemyShot(e, e.Facing + LeaderSpread, LeaderSpeed, Defs.FastShot);
            return true;
        }

        /// (moved from VarietyFire, Stitcher)
        bool VarietyFire_Stitcher(Enemy e, float aim)
        {
            e.FireT = StitchEvery;
            // In a pair, the first fires for both (one orb each, at the same moment); its partner waits.
            List<Enemy> mates;
            Enemy lead = null;
            if (e.SquadId != 0 && squadMembers.TryGetValue(e.SquadId, out mates))
                foreach (var o in mates)
                    if (o.Type == EnemyType.Stitcher && o.Hp > 0f && (lead == null || o.SquadSlot < lead.SquadSlot))
                        lead = o;
            Enemy partner = null;
            if (lead != null && squadMembers.TryGetValue(e.SquadId, out mates))
                foreach (var o in mates)
                    if (o != lead && o.Type == EnemyType.Stitcher && Targetable(o)) { partner = o; break; }
            if (partner != null)
            {
                if (lead != e)
                    return true;  // the lead fires ours
                // Both orbs fly the same way (at you from between them), so the thread between them stays as wide as the pair.
                Player tp;
                V2 mid = (e.Pos + partner.Pos) / 2f;
                V2 target = Players.TryGetValue(e.TargetId, out tp) ? tp.Pos : e.Pos + V2.FromAngle(aim) * 300f;
                float course = (target - mid).Angle;
                StitchOrb(e, course);
                StitchOrb(partner, course);
                partner.FireT = StitchEvery;
                return true;
            }
            // Alone: two orbs a little apart, tied to each other.
            StitchOrb(e, aim - StitchSpread);
            StitchOrb(e, aim + StitchSpread);
            return true;
        }

        static void VarietyBrains(Brain[] t)
        {
            t[(int)EnemyType.BeamSentry] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_BeamSentry(e, p, d, di, ref sp), e => e.Phase >= 2),
                Special = new CodeSpecial((g, e, p, dt) => g.VarietySpecial_BeamSentry(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.VarietyFire_BeamSentry(e, a)) };
            t[(int)EnemyType.Scorcher] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Scorcher(e, p, d, di, ref sp)),
                Special = new CodeSpecial((g, e, p, dt) => g.VarietySpecial_Scorcher(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.VarietyFire_Scorcher(e, a)) };
            t[(int)EnemyType.Reaver] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Reaver(e, p, d, di, ref sp), e => e.Phase != 0),
                Special = new CodeSpecial((g, e, p, dt) => g.VarietySpecial_Reaver(e, p, dt)) };
            t[(int)EnemyType.Warder] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Warder(e, p, d, di, ref sp)),
                Special = new CodeSpecial((g, e, p, dt) => g.VarietySpecial_Warder(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.VarietyFire_Warder(e, a)) };
            t[(int)EnemyType.Pauser] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Pauser(e, p, d, di, ref sp)),
                Fire = new CodeFire((g, e, p, a) => g.VarietyFire_Pauser(e, a)) };
            t[(int)EnemyType.Leader] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Leader(e, p, d, di, ref sp), e => e.Phase == 1),
                Special = new CodeSpecial((g, e, p, dt) => g.VarietySpecial_Leader(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.VarietyFire_Leader(e, a)) };
            t[(int)EnemyType.Stitcher] = new Brain {
                Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.VarietyMove_Stitcher(e, p, d, di, ref sp)),
                Fire = new CodeFire((g, e, p, a) => g.VarietyFire_Stitcher(e, a)) };
        }
    }
}

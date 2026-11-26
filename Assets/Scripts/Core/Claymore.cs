using System;
using System.Collections.Generic;

namespace Arena
{
    /// Claymore, the DPS Soldier's 3rd choice (`AbilityId.Claymore`, took Mark for Death's place and number), from the Claymore
    /// mockup (session chat). Two charges, ClaymoreCooldown each; each plants a directional mine at your cursor (up to
    /// ClaymoreRange away), facing away from you:
    ///   it arms over ClaymoreArm s, then watches a cone in front of it (ClaymoreTrigHalf either side, ClaymoreTrigReach long);
    ///   the first enemy to step in trips it: a ClaymoreFuse s beep, then the blast, a wider cone (ClaymoreBlastHalf either side,
    ///   ClaymoreBlastReach long) doing ClaymoreDamage up close, falling to (1 - ClaymoreFalloff) of it at the far edge, and
    ///   shoving what isn't Heavy ClaymoreShove px. Nothing behind it is hit.
    /// ClaymoreMax per player (a 4th removes the oldest), ClaymoreLife s each; they fold away at the wave's end.
    /// Mastered: each one re-arms once after its first blast (it goes off twice). Its Rare card Shaped Charge adds
    /// Stats.ClaymoreDmgMult.
    /// It is one `ZoneKind.Claymore` zone: Pos the mine, Aim its facing, Radius the trigger's reach; GuardFrac its state (0-1 the
    /// arming, 1 armed, 2 tripped) and Marched 1 while a mastered second blast is left (both replicated); TickT the host's phase
    /// timer. FxKind.ClaymoreBlast is the blast (Game/ClaymoreLook.cs draws it and the mine).
    public partial class Game
    {
        public const float ClaymoreCooldown = 5f, ClaymoreRange = 220f, ClaymoreR = 10f;
        public const float ClaymoreArm = 0.6f, ClaymoreFuse = 0.15f, ClaymoreLife = 30f, ClaymoreRearm = 1f;
        public const float ClaymoreTrigReach = 150f, ClaymoreBlastReach = 210f;
        public static readonly float ClaymoreTrigHalf = MathX.Pi / 6f, ClaymoreBlastHalf = MathX.Pi / 4f;
        public const float ClaymoreDamage = 140f, ClaymoreFalloff = 0.4f, ClaymoreShove = 80f;
        public const int ClaymoreMax = 3, ClaymoreCharges = 2;
        public const float ClaymoreArming = 0f, ClaymoreArmed = 1f, ClaymoreTripped = 2f;  // GuardFrac: 0-1 arming, then these

        /// Is this claymore watching its cone (armed, not tripped)?
        public static bool ClaymoreWatching(Zone z) => z.Kind == ZoneKind.Claymore && z.T > 0f && z.GuardFrac >= ClaymoreArmed && z.GuardFrac < ClaymoreTripped;

        /// Is something of this radius at pos in this cone of the mine?
        public static bool InClaymoreCone(Zone z, V2 pos, float radius, float half, float reach)
            => MathX.InCone(z.Pos, z.Aim, half, reach, pos, radius, inner: radius, padBody: true);  // (its body counts)

        /// This player's claymores, oldest first.
        public List<Zone> ClaymoresOf(int owner)
        {
            var list = new List<Zone>();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Claymore && z.Owner == owner && z.T > 0f)
                    list.Add(z);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        void CastClaymore(Player p, V2 target, bool cap)
        {
            var mine = ClaymoresOf(p.Id);
            for (int i = 0; i <= mine.Count - ClaymoreMax; i++)
                FoldClaymore(mine[i]);  // a 4th removes the oldest
            V2 to = (target - p.Pos).LimitLength(ClaymoreRange);
            V2 at = ClampToArena(p.Pos + to, ClaymoreR);
            var z = AddZone(ZoneKind.Claymore, p, at, ClaymoreTrigReach, ClaymoreLife);
            z.Aim = to.LengthSq > 1f ? to.Angle : p.Aim;
            z.GuardFrac = ClaymoreArming;
            z.TickT = ClaymoreArm;
            z.Cap = cap;
            z.Marched = cap ? 1f : 0f;
            BroadcastFx(FxKind.Ring, at, Kits.Get(ZoneKind.Claymore).Color, 14f, 0.25f, Sfx.Ability);
        }

        /// Removes a claymore without a blast; it goes at the end of this tick.
        void FoldClaymore(Zone z)
        {
            if (z.T <= 0f)
                return;
            BroadcastFx(FxKind.Ring, z.Pos, Kits.Get(ZoneKind.Claymore).Color, 12f, 0.3f, Sfx.None);
            z.T = 0f;
        }

        /// Host, as the wave is cleared: every claymore folds away.
        void FoldAllClaymores()
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Claymore)
                    FoldClaymore(z);
        }

        /// Host, every tick, for each claymore (TickZones): arming, watching the cone, the fuse and the blast.
        void TickClaymore(Zone z, float dt)
        {
            Player owner;
            if (!Battle || !Players.TryGetValue(z.Owner, out owner) || z.T <= dt * 1.5f)
            {
                FoldClaymore(z);
                return;
            }
            if (z.GuardFrac < ClaymoreArmed)  // arming (or re-arming after a mastered first blast)
            {
                z.TickT -= dt;
                float span = z.Marched > 0f || !z.Cap ? ClaymoreArm : ClaymoreRearm;
                z.GuardFrac = Math.Min(ClaymoreArmed, 1f - Math.Max(0f, z.TickT) / span);
                return;
            }
            if (z.GuardFrac >= ClaymoreTripped)  // the fuse
            {
                z.TickT -= dt;
                if (z.TickT <= 0f)
                    BlowClaymore(z, owner);
                return;
            }
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!InClaymoreCone(z, e.Pos, e.Radius, ClaymoreTrigHalf, ClaymoreTrigReach))
                    continue;
                z.GuardFrac = ClaymoreTripped;
                z.TickT = ClaymoreFuse;
                BroadcastFx(FxKind.Ring, z.Pos, new Rgba(1f, 0.3f, 0.3f), 16f, 0.15f, Sfx.Pick);  // the beep
                break;
            }
        }

        /// The blast: everything in the wide cone in front, hurt by how near it is and shoved away. Mastered: it re-arms once.
        void BlowClaymore(Zone z, Player owner)
        {
            float dmg = ClaymoreDamage * z.DmgMult * owner.Stats.ClaymoreDmgMult;
            V2 face = V2.FromAngle(z.Aim);
            foreach (var e in AllFoes(by: z.Owner))
            {
                if (!InClaymoreCone(z, e.Pos, e.Radius, ClaymoreBlastHalf, ClaymoreBlastReach))
                    continue;
                float near = Math.Min(1f, Math.Max(0f, e.Pos.DistanceTo(z.Pos) - e.Radius) / ClaymoreBlastReach);
                HurtEnemy(e, dmg * (1f - ClaymoreFalloff * near), z.Owner);
                if (!Heavy(e))
                    ShoveFrom(e, z.Pos, ClaymoreShove, face);
            }
            BroadcastFx(FxKind.ClaymoreBlast, z.Pos, z.Pos + face, Kits.Get(ZoneKind.Claymore).Color, ClaymoreBlastReach, 0.7f, Sfx.Explode);
            if (z.Marched > 0f)  // mastered: the spare blast
            {
                z.Marched = 0f;
                z.GuardFrac = ClaymoreArming;
                z.TickT = ClaymoreRearm;
                return;
            }
            z.T = 0f;  // gone at the end of this tick
        }
    }
}

using System;
using System.Collections.Generic;

namespace Arena
{
    /// Deployable Cover, the Tank Soldier's fourth Space ability (`AbilityId.DeployableCover`, `AbilityDef.Fourth`: the lobby's 4th
    /// ability key), from Docs/Mockups/deployable-cover.html (look A, Stake Drop). Two charges; each plants a "[" of steel plate
    /// CoverAhead px in front of you along your aim, its back wall facing out and its two arms reaching back toward you:
    ///   a `ZoneKind.Cover` zone: Pos its middle, Aim the way it faces, BlockHp its health and SoakMax its full health
    ///     (replicated as GuardFrac), Duration CoverLife (its age is Duration - T);
    ///   solid from CoverSolid s after the throw: enemy bullets that touch a plate stop there and wear it (CoverStops, from the
    ///     enemy-bullet loop in Combat.cs) and enemies can't walk through it (KeepOffCovers); your team's shots and bodies pass;
    ///   at most CoverMax standing per player: one more folds away the oldest; all of them fold when the fight ends.
    /// Mastered: CoverHpMastered health and CoverCdMastered of the cooldown. FxKind.CoverHit is a shot ringing off a plate,
    /// CoverBreak one bursting apart and CoverFold one folding away (Game/DeployableCoverLook.cs draws them and the cover).
    public partial class Game
    {
        public const float CoverCooldown = 12f, CoverCdMastered = 0.75f;
        public const float CoverHp = 400f, CoverHpMastered = 600f;
        public const int CoverMax = 3, CoverCharges = 2;
        public const float CoverAhead = 64f, CoverSolid = 0.4f, CoverLife = 100000f;
        /// The bracket in its own frame (x out along Aim, y across): the back wall and the two arms reaching back from its ends.
        public const float CoverHalf = 41f, CoverThick = 5f, CoverArm = 31f, CoverArmW = 10f;

        /// Seconds since this cover was thrown.
        public static float CoverAge(Zone z) => z.Duration - z.T;

        /// Is it up and blocking (thrown, landed and unfolded enough)?
        public static bool CoverUp(Zone z) => z.Kind == ZoneKind.Cover && z.T > 0f && CoverAge(z) >= CoverSolid;

        /// A world point in the cover's own frame: x out along its Aim, y across.
        public static V2 CoverLocal(Zone z, V2 pos)
        {
            V2 d = pos - z.Pos, f = V2.FromAngle(z.Aim);
            return new V2(d.X * f.X + d.Y * f.Y, -d.X * f.Y + d.Y * f.X);
        }

        /// A point in the cover's frame back in the world.
        public static V2 CoverWorld(Zone z, float lx, float ly)
        {
            V2 f = V2.FromAngle(z.Aim);
            return z.Pos + new V2(lx * f.X - ly * f.Y, lx * f.Y + ly * f.X);
        }

        /// Piece k of the bracket (0 the wall, 1 and 2 the arms) as a box in the cover's frame.
        public static void CoverPiece(int k, out float x0, out float x1, out float y0, out float y1)
        {
            if (k == 0)
            {
                x0 = -CoverThick; x1 = CoverThick; y0 = -CoverHalf; y1 = CoverHalf;
                return;
            }
            x0 = -CoverArm; x1 = -CoverThick;
            y0 = k == 1 ? -CoverHalf : CoverHalf - CoverArmW;
            y1 = k == 1 ? -CoverHalf + CoverArmW : CoverHalf;
        }

        /// Does a circle of this radius at pos touch the bracket?
        public static bool CoverTouches(Zone z, V2 pos, float radius)
        {
            V2 l = CoverLocal(z, pos);
            for (int k = 0; k < 3; k++)
            {
                CoverPiece(k, out float x0, out float x1, out float y0, out float y1);
                float cx = Math.Max(x0, Math.Min(x1, l.X)), cy = Math.Max(y0, Math.Min(y1, l.Y));
                if ((l.X - cx) * (l.X - cx) + (l.Y - cy) * (l.Y - cy) <= radius * radius)
                    return true;
            }
            return false;
        }

        /// Its health left, 0-1 (the host's own numbers, or the snapshot's GuardFrac on a client).
        public float CoverFrac(Zone z) => IsHost && z.SoakMax > 0f ? Math.Max(0f, z.BlockHp / z.SoakMax) : z.GuardFrac;

        /// How many covers this player has standing, oldest first.
        public List<Zone> CoversOf(int owner)
        {
            var list = new List<Zone>();
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Cover && z.Owner == owner && z.T > 0f)
                    list.Add(z);
            list.Sort((a, b) => a.Id.CompareTo(b.Id));
            return list;
        }

        void CastCover(Player p, V2 target, bool cap)
        {
            var mine = CoversOf(p.Id);
            for (int i = 0; i <= mine.Count - CoverMax; i++)
                FoldCover(mine[i]);  // a fourth folds away the oldest
            V2 to = target - p.Pos;
            float aim = to.LengthSq > 1f ? to.Angle : p.Aim;
            V2 at = ClampToArena(p.Pos + V2.FromAngle(aim) * CoverAhead, CoverHalf * 0.5f);
            var z = AddZone(ZoneKind.Cover, p, at, CoverHalf, CoverLife);
            z.Aim = aim;
            z.SoakMax = z.BlockHp = cap ? CoverHpMastered : CoverHp;
            z.GuardFrac = 1f;
            Sounds.Add(Sfx.TurretDeploy);
        }

        /// Folds a cover away (FxKind.CoverFold draws it sinking); it goes at the end of this tick.
        void FoldCover(Zone z)
        {
            if (z.T <= 0f)
                return;
            BroadcastFx(FxKind.CoverFold, z.Pos, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.Cover).Color, CoverFrac(z), 0.45f, Sfx.None);
            z.T = 0f;
        }

        /// Host, as the wave is cleared: every cover folds away.
        void FoldAllCovers()
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Cover)
                    FoldCover(z);
        }

        /// Host, every tick, for each cover (TickZones): it folds away when the fight ends.
        void TickCover(Zone z)
        {
            if (!Battle)
                FoldCover(z);
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a cover stop this shot? It wears the cover by what it would have hit for.
        bool CoverStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Cover || !CoverUp(z) || b.Pos.FartherThan(z.Pos, CoverHalf + CoverArm + b.Radius))
                    continue;
                if (!CoverTouches(z, b.Pos, b.Radius))
                    continue;
                float dmg = b.Dmg * EnemyDamageScale(Math.Max(1, Wave));
                z.BlockHp -= dmg;
                z.GuardFrac = Math.Max(0f, z.BlockHp / Math.Max(1f, z.SoakMax));
                BroadcastFx(FxKind.CoverHit, b.Pos, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.Cover).Color, b.Radius, 0.22f, Sfx.None);
                if (z.BlockHp <= 0f)
                {
                    BroadcastFx(FxKind.CoverBreak, z.Pos, z.Pos + V2.FromAngle(z.Aim), Kits.Get(ZoneKind.Cover).Color, z.Id, 3f, Sfx.Clang);
                    z.T = 0f;  // gone at the end of this tick
                }
                return true;
            }
            return false;
        }

        /// Host, every tick: enemies can't walk through a cover's plates.
        void KeepOffCovers()
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Cover || !CoverUp(z))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (!Targetable(e) || e.Pos.FartherThan(z.Pos, CoverHalf + CoverArm + e.Radius))
                        continue;
                    V2 l = CoverLocal(z, e.Pos);
                    bool moved = false;
                    for (int k = 0; k < 3; k++)
                    {
                        CoverPiece(k, out float x0, out float x1, out float y0, out float y1);
                        float cx = Math.Max(x0, Math.Min(x1, l.X)), cy = Math.Max(y0, Math.Min(y1, l.Y));
                        float dx = l.X - cx, dy = l.Y - cy, d2 = dx * dx + dy * dy, r = e.Radius;
                        if (d2 >= r * r)
                            continue;
                        if (d2 > 0.0001f)
                        {
                            float d = (float)Math.Sqrt(d2);
                            l = new V2(cx + dx / d * (r + 0.5f), cy + dy / d * (r + 0.5f));
                        }
                        else  // its middle is inside the plate: out by the shortest way
                        {
                            float left = l.X - x0, right = x1 - l.X, top = l.Y - y0, bottom = y1 - l.Y;
                            float m = Math.Min(Math.Min(left, right), Math.Min(top, bottom));
                            if (m == right) l.X = x1 + r + 0.5f;
                            else if (m == left) l.X = x0 - r - 0.5f;
                            else if (m == top) l.Y = y0 - r - 0.5f;
                            else l.Y = y1 + r + 0.5f;
                        }
                        moved = true;
                    }
                    if (moved)
                    {
                        e.Pos = ClampToArena(CoverWorld(z, l.X, l.Y), e.Radius);
                        e.KnockLeft = V2.Zero;
                    }
                }
            }
        }
    }
}

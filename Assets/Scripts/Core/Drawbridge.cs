using System;

namespace Arena
{
    /// The DPS Knight's ultimate, the Drawbridge (`AbilityId.BladeTempest` kept; was Blade Tempest), the Oak & Iron look
    /// of Docs/Mockups/knight-dps-abilities.html. A gatehouse rises behind you and a drawbridge BridgeLength long and
    /// twice BridgeHalfWidth wide falls along your aim (its shadow sweeping the lane first). At BridgeSlam it lands:
    /// BridgeCrush to every enemy in the lane (bosses too: they aren't killed outright), BridgeEdgeDamage and a shove of
    /// BridgeEdgeShove px out to enemies at its edges. Then it lies there for BridgeStand seconds as a raised walkway:
    /// enemies can't step onto it (they're kept to its rails and far end) and their shots stop at its rails, red aimed
    /// shots too; you and your allies walk on it. Then the planks give way from the far end.
    /// A `ZoneKind.Drawbridge` zone: Pos is the hinge, Aim the way it fell, Radius its length; its age sets the phase.
    ///   FxKind.BridgeSpark: an enemy shot stopped by a rail (Pos = where, Pos2 = out from the rail).
    /// Drawn in Game/DrawbridgeLook.cs.
    public partial class Game
    {
        public const float BridgeLength = 500f, BridgeHalfWidth = 50f, BridgeHingeOut = 30f;
        public const float BridgeFallStart = 0.42f, BridgeChainsSnap = 0.72f, BridgeSlam = 1.05f;
        public const float BridgeStand = 5f, BridgeStandMastered = 8f, BridgeCrumble = 0.9f;
        public const float BridgeCrush = 140f, BridgeEdgeDamage = 45f, BridgeEdgeBand = 40f, BridgeEdgeShove = 40f;

        public static float BridgeAge(Zone z) => z.Duration - z.T;

        /// Is the bridge lying in the lane (after the slam, until the planks start giving way)?
        public static bool BridgeDown(Zone z)
        {
            float age = BridgeAge(z);
            return age >= BridgeSlam && age < z.Duration - BridgeCrumble + 0.35f;
        }

        /// A point in the bridge's own frame: x along it from the hinge, y across it.
        public static V2 BridgeLocal(Zone z, V2 p)
        {
            V2 d = V2.FromAngle(z.Aim), off = p - z.Pos;
            return new V2(off.Dot(d), off.X * -d.Y + off.Y * d.X);
        }

        public static V2 BridgeWorld(Zone z, float u, float v)
        {
            V2 d = V2.FromAngle(z.Aim);
            return z.Pos + d * u + new V2(-d.Y, d.X) * v;
        }

        /// Host: the Drawbridge was cast toward `target`.
        void CastDrawbridge(Player p, V2 target, bool mastered)
        {
            V2 off = target - p.Pos;
            float aim = off.LengthSq > 1f ? off.Angle : p.Aim;
            float stand = mastered ? BridgeStandMastered : BridgeStand;
            var z = AddZone(ZoneKind.Drawbridge, p, p.Pos + V2.FromAngle(aim) * BridgeHingeOut, BridgeLength, BridgeSlam + stand + BridgeCrumble);
            z.Aim = aim;
            z.Bursts = true;  // (not landed yet)
            Sounds.Add(Sfx.BossRumble);
        }

        /// Host, from TickZones: the slam.
        void TickDrawbridge(Zone z)
        {
            if (!z.Bursts || BridgeAge(z) < BridgeSlam)
                return;
            z.Bursts = false;
            Sounds.Add(Sfx.BossSlam);
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 l = BridgeLocal(z, e.Pos);
                if (l.X < 0f || l.X > BridgeLength)
                    continue;
                float side = Math.Abs(l.Y);
                if (side < BridgeHalfWidth)
                    HurtEnemy(e, BridgeCrush * z.DmgMult, z.Owner);
                else if (side < BridgeHalfWidth + BridgeEdgeBand)
                {
                    HurtEnemy(e, BridgeEdgeDamage * z.DmgMult, z.Owner);
                    if (!e.Def.IsBoss && !e.Def.Heavy)
                    {
                        V2 outward = BridgeWorld(z, 0f, l.Y >= 0f ? 1f : -1f) - z.Pos;
                        KnockEnemy(e, outward * (BridgeHalfWidth + e.Radius + BridgeEdgeShove - side));
                    }
                }
            }
        }

        /// Host, after the enemies move: nobody steps onto a bridge that's down (they're kept to its rails and far end).
        void KeepOffBridges()
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Drawbridge || !BridgeDown(z))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f)
                        continue;
                    V2 l = BridgeLocal(z, e.Pos);
                    float m = BridgeHalfWidth + e.Radius + 2f;
                    if (l.X <= -4f - e.Radius || l.X >= BridgeLength + e.Radius || Math.Abs(l.Y) >= m)
                        continue;
                    float toEnd = BridgeLength + e.Radius - l.X, toSide = m - Math.Abs(l.Y);
                    if (toEnd < toSide)
                        l.X = BridgeLength + e.Radius;
                    else
                        l.Y = (l.Y >= 0f ? 1f : -1f) * m;
                    e.Pos = ClampToArena(BridgeWorld(z, l.X, l.Y), e.Radius);
                    e.KnockLeft = V2.Zero;
                }
            }
        }

        /// Host, from the enemy-bullet loop (Combat.cs): an enemy shot reaching a bridge that's down stops at its rail.
        bool BridgeStops(Bullet b)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.Drawbridge || !BridgeDown(z))
                    continue;
                V2 l = BridgeLocal(z, b.Pos);
                if (l.X < -4f || l.X > BridgeLength || Math.Abs(l.Y) > BridgeHalfWidth + 2f)
                    continue;
                V2 n = BridgeWorld(z, 0f, l.Y >= 0f ? 1f : -1f) - z.Pos;
                BroadcastFx(FxKind.BridgeSpark, b.Pos, n, new Rgba(1f, 0.82f, 0.55f), 0f, 0.3f, Sfx.None);
                return true;
            }
            return false;
        }
    }
}

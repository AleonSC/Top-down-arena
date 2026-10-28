using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A ring of three small gravity wells orbiting a spot a player was standing on. The ring shrinks;
    /// touching it hurts, and it's gone once its three wells are shot down.
    public class Cage
    {
        public int Id, PlayerId;
        public V2 Center;
        public float Radius, Spin;
        public readonly List<int> Wells = new List<int>();  // host: the Cage Well enemies holding it up
    }

    /// The Singularity (wave 20). It sits in the middle and loops four attacks:
    ///   Pull (4 s): drags everyone toward it while a three-armed spiral winds out.
    ///   Collapse (3 s): rings of bullets.
    ///   Normal (8 s): the purple spiral, then three red lasers from its centre to the edges that sweep around.
    ///   Cage (9 s): a shrinking ring of small gravity wells around every player; shoot them to get out.
    /// Four big gravity wells sit in the corners all fight: they bend your shots into themselves and
    /// drift you slowly toward them. Kill all four and they come back 15 s later.
    public partial class Game
    {
        // Phase (replicated) tells clients what to draw.
        public const byte SingNormal = 0, SingPull = 1, SingCollapse = 2, SingCage = 3, SingLaserWarn = 4, SingLasers = 5;
        const float SingPullTime = 4f, SingCollapseTime = 3f, SingNormalTime = 8f, SingCageTime = 9f;
        const float SingCycle = SingPullTime + SingCollapseTime + SingNormalTime + SingCageTime;
        const float LaserWarnAt = 1.5f, LaserOnAt = 2.3f;  // seconds into the normal attack
        public const int LaserCount = 3;
        public const float LaserWidth = 10f, LaserSpin = 0.32f;  // radians per second, always counter-clockwise
        public const float CageStartRadius = 180f, CageShrink = 22f, CageWallWidth = 10f, CageSpin = 1.4f;
        const int CageWellsEach = 3;

        // Corner wells
        public const float WellPullRange = 420f;      // players' shots this close bend into a corner well
        public const float WellDriftRange = 480f, WellDrift = 45f;  // and players this close drift toward it (px/s at the well)
        const float WellInset = 110f;

        public readonly Dictionary<int, Cage> Cages = new Dictionary<int, Cage>();
        int nextCageId = 1;

        static byte SingularityPhase(float t)
        {
            float c = MathX.Fmod(t, SingCycle);
            if (c < SingPullTime) return SingPull;
            c -= SingPullTime;
            if (c < SingCollapseTime) return SingCollapse;
            c -= SingCollapseTime;
            if (c < SingNormalTime) return c < LaserWarnAt ? SingNormal : c < LaserOnAt ? SingLaserWarn : SingLasers;
            return SingCage;
        }

        void SingularitySpecial(Enemy e, float dt)
        {
            byte was = e.Phase;
            e.Phase = SingularityPhase(e.T);
            if (e.Phase == SingPull)
            {
                PullCenter = e.Pos;
                PullStrength = SingularityPull;
            }
            if (e.Phase == SingLaserWarn && was != SingLaserWarn)
                e.Facing = Rand(0f, MathX.Tau);  // a new starting angle for each sweep
            if (e.Phase == SingLaserWarn || e.Phase == SingLasers)
                e.Facing += LaserSpin * (e.Phase == SingLasers ? dt : 0f);
            if (e.Phase == SingCage && was != SingCage)
                SpawnCages(e);
            if (e.Phase != SingCage && was == SingCage)
                ClearCages();

            // The corner wells. Kill all four and they come back 15 s later.
            int wells = CountChildren(e, EnemyType.GravityWell);
            e.T2 -= dt;
            if (wells > 0)
                e.T2 = Math.Max(e.T2, WellRespawn);
            else if (e.T2 <= 0f)
            {
                var corners = new[] { new V2(WellInset, WellInset), new V2(Width - WellInset, WellInset), new V2(WellInset, Height - WellInset), new V2(Width - WellInset, Height - WellInset) };
                foreach (var c in corners)
                {
                    var w = SpawnEnemyAt(EnemyType.GravityWell, c, 0.8f, e.Id);
                    w.Waypoint = c;
                    BroadcastFx(FxKind.Ring, c, Defs.Get(EnemyType.GravityWell).Color, 120f, 0.6f, Sfx.None);
                }
                Sounds.Add(Sfx.Summon);
            }
        }

        void SingularityFire(Enemy e, float aim)
        {
            switch (e.Phase)
            {
                case SingPull:  // gravity pull: slow three-armed spiral
                    e.FireT = 0.13f;
                    e.Ang += 0.21f;
                    for (int k = 0; k < 3; k++)
                        EnemyShot(e, e.Ang + MathX.Tau * k / 3f, 110f, Defs.SmallShot);
                    break;
                case SingCollapse:  // rings
                    e.FireT = 0.9f;
                    e.Ang += 0.11f;
                    for (int i = 0; i < 26; i++)
                        EnemyShot(e, e.Ang + MathX.Tau * i / 26f, 170f, Defs.SmallShot);
                    break;
                case SingCage:  // it holds fire while the cages close in
                    e.FireT = 0.3f;
                    break;
                default:  // normal: the purple spiral (the lasers are separate)
                    e.FireT = 0.65f;
                    e.Ang += 0.3f;
                    for (int d = 0; d < 4; d++)
                        EnemyShot(e, e.Ang + MathX.Tau * d / 4f, 150f, Defs.BigShot);
                    break;
            }
        }

        void SpawnCages(Enemy boss)
        {
            ClearCages();
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                var cage = new Cage { Id = nextCageId++, PlayerId = p.Id, Center = p.Pos, Radius = CageStartRadius, Spin = Rand(0f, MathX.Tau) };
                for (int i = 0; i < CageWellsEach; i++)
                {
                    var w = SpawnEnemyAt(EnemyType.CageWell, CagePoint(cage, i), 0.5f, boss.Id);
                    w.Ang = MathX.Tau * i / CageWellsEach;
                    cage.Wells.Add(w.Id);
                }
                Cages[cage.Id] = cage;
                BroadcastFx(FxKind.Ring, cage.Center, Defs.Get(EnemyType.CageWell).Color, CageStartRadius, 0.5f, Sfx.None);
            }
            Sounds.Add(Sfx.Summon);
        }

        static V2 CagePoint(Cage c, int i) => c.Center + V2.FromAngle(c.Spin + MathX.Tau * i / CageWellsEach) * c.Radius;

        void ClearCages()
        {
            foreach (var c in Cages.Values)
                foreach (int id in c.Wells)
                {
                    Enemy w;
                    if (Enemies.TryGetValue(id, out w))
                        Enemies.Remove(id);  // they wink out (no kill, no reward)
                }
            Cages.Clear();
        }

        /// Host: lasers and cage walls hurt, cages shrink and spin, and a cage with no wells left breaks.
        void TickSingularityHazards(float dt)
        {
            Enemy sing = null;
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Singularity && !IsSpawning(e))
                    sing = e;
            if (sing == null)
            {
                if (Cages.Count > 0)
                    ClearCages();
                return;
            }
            float dmg = 14f + Wave * 0.4f;
            if (sing.Phase == SingLasers)
                foreach (var p in Players.Values)
                {
                    if (p.Downed || p.InvulnT > 0f)
                        continue;
                    for (int k = 0; k < LaserCount; k++)
                        if (OnLaser(sing.Pos, sing.Facing + MathX.Tau * k / LaserCount, p.Pos, LaserWidth / 2f + p.Def.HitR))
                        {
                            Damage(p, dmg, "The Singularity (laser)");
                            break;
                        }
                }

            var broken = new List<int>();
            foreach (var c in Cages.Values)
            {
                c.Wells.RemoveAll(id => !Enemies.ContainsKey(id) || Enemies[id].Hp <= 0f);
                if (c.Wells.Count == 0)
                {
                    broken.Add(c.Id);
                    BroadcastFx(FxKind.Ring, c.Center, Defs.Get(EnemyType.CageWell).Color, c.Radius + 20f, 0.4f, Sfx.Hit);
                    continue;
                }
                c.Radius = Math.Max(0f, c.Radius - CageShrink * dt);
                c.Spin += CageSpin * dt;
                for (int i = 0; i < c.Wells.Count; i++)
                {
                    var w = Enemies[c.Wells[i]];
                    w.Pos = c.Center + V2.FromAngle(c.Spin + w.Ang) * c.Radius;
                }
                foreach (var p in Players.Values)
                    if (!p.Downed && p.InvulnT <= 0f && Math.Abs(p.Pos.DistanceTo(c.Center) - c.Radius) < CageWallWidth / 2f + p.Def.HitR)
                        Damage(p, dmg, "The Singularity (cage)");
            }
            foreach (int id in broken)
                Cages.Remove(id);
        }

        /// Is pos within `reach` of a laser fired from `from` at angle `ang` (it runs to the arena's edge)?
        public static bool OnLaser(V2 from, float ang, V2 pos, float reach)
        {
            V2 dir = V2.FromAngle(ang);
            V2 to = pos - from;
            float along = to.X * dir.X + to.Y * dir.Y;
            if (along < 0f)
                return false;
            return Math.Abs(to.X * dir.Y - to.Y * dir.X) < reach;
        }

        /// The corner wells drift players slowly toward them (applied on each player's own game).
        V2 WellDriftAt(V2 pos)
        {
            V2 drift = V2.Zero;
            foreach (var e in Enemies.Values)
            {
                if (e.Type != EnemyType.GravityWell || IsSpawning(e))
                    continue;
                float d = e.Pos.DistanceTo(pos);
                if (d < WellDriftRange && d > 1f)
                    drift += (e.Pos - pos) / d * WellDrift * (1f - d / WellDriftRange);
            }
            return drift;
        }

        void WriteCages(BinaryWriter w)
        {
            w.Write((byte)Cages.Count);
            foreach (var c in Cages.Values)
            {
                w.Write(c.Id);
                w.Write(c.PlayerId);
                w.Write(c.Center);
                w.Write(c.Radius);
            }
        }

        void ReadCages(BinaryReader r)
        {
            Cages.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                var c = new Cage { Id = r.ReadInt32(), PlayerId = r.ReadInt32(), Center = r.ReadV2(), Radius = r.ReadSingle() };
                Cages[c.Id] = c;
            }
        }
    }
}

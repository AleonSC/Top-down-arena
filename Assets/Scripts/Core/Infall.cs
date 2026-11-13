using System;

namespace Arena
{
    /// The Singularity's faction, the Infall (Docs/SingularityFaction.md, mockup Docs/Mockups/singularity-faction.html; drawn
    /// in Game/InfallLook.cs). Each enemy previews one thing the boss tests. Step 1 has three:
    ///   Debris (the Pull): melee rubble that falls at you on an inward spiral. It keeps its side (Enemy.Strafe) and turns
    ///     in from DebrisSpiral rad off your line when far to straight at you within DebrisStraight px.
    ///   Acolyte (bent shots): keeps AcolyteRange and strafes; every AcolyteEvery s it fires three slow orbs, the middle one
    ///     straight and the outer two AcolyteSpread rad out, curving (Bullet.AngVel) so all three meet where you stood.
    ///   Pulsar (Lasers): Phase 0 walks in to PulsarRange; 1 plants and flickers its two beams (one each way along Facing)
    ///     for PulsarWarn s, starting PulsarLead rad behind you; 2 the beams are on and sweep PulsarArc toward and past you
    ///     over PulsarSweep s (Strafe is the way round); 3 rests PulsarRest s, then plants again if you're near. Phase and
    ///     Facing are in the snapshot, so clients draw the beams with no extra fields (the flicker times from Enemy.SeenT).
    /// Step 2:
    ///   Graviton (Pull and Gravity Wells): keeps GravitonRange; every GravitonEvery s it winds up and lobs a Small Well
    ///     (EnemyType.MiniWell) by you: a ring marks the spot, the well lands GravitonFlight s later (its spawn-in: it can't
    ///     be hurt yet) and lasts MiniWellLife s (Enemy.PhaseT). Like the boss's corner wells, a landed well drags players
    ///     toward it (WellDriftAt, on each player's own game) and bends their shots into it (PullIntoWells), only closer and
    ///     weaker. One well a Graviton: a new lob takes the old one away.
    ///   Orbiter (the disk and Slingshot): keeps OrbiterRange and circles you; three orbs turn round it (drawn only). It
    ///     winds up OrbiterWindUp s (the orb facing you flares), then slings it straight at you (OrbiterOrbSpeed). Phase
    ///     counts its throws, so the look knows which orb is gone (it grows back over OrbiterEvery).
    ///   Collapsar (Collapse and Hawking): a slow melee heavy that shrinks as it's shot (drawn). Killed, it leaves an
    ///     Implosion (EnemyType.Implosion: no XP, so shots pass through it) that pulls players in for ImplosionTime s, then
    ///     bursts into a ring of ImplosionShots and is gone.
    public partial class Game
    {
        public const float DebrisSpiral = 1.05f, DebrisStraight = 90f, DebrisFar = 420f;
        public const float AcolyteRange = 280f, AcolyteEvery = 2.4f, AcolyteSpread = 0.5f, AcolyteOrbSpeed = 150f;
        public const float PulsarRange = 300f, PulsarWarn = 1f, PulsarSweep = 4f, PulsarRest = 2.5f, PulsarArc = MathX.Pi / 2f,
            PulsarLead = 0.5f, PulsarBeamLen = 900f, PulsarBeamWidth = 14f;
        float PulsarDamage => 16f;  // a touch; the difficulty's per-wave damage growth scales it, like the Beam Sentry's

        public const float GravitonRange = 320f, GravitonEvery = 6f, GravitonWindUp = 0.6f, GravitonFlight = 0.8f, GravitonScatter = 50f,
            MiniWellLife = 4f, MiniWellDriftRange = 240f, MiniWellDrift = 80f, MiniWellBendRange = 150f;
        public const float OrbiterRange = 260f, OrbiterEvery = 1.8f, OrbiterWindUp = 0.55f, OrbiterOrbSpeed = 330f, OrbiterOrbit = 34f;
        public const float ImplosionTime = 0.8f, ImplosionDriftRange = 260f, ImplosionDrift = 100f, ImplosionShotSpeed = 150f;
        public const int ImplosionShots = 12;

        public static bool IsInfall(EnemyType t) => t >= EnemyType.Debris && t <= EnemyType.Implosion;

        /// Pulls players toward it (WellDriftAt): the boss's corner wells, a Graviton's Small Well, a Collapsar's Implosion.
        static bool Pulls(EnemyType t) => t == EnemyType.GravityWell || t == EnemyType.MiniWell || t == EnemyType.Implosion;

        /// The Pulsar keeps its beams' line while they flicker and sweep.
        public static bool PulsarLocked(Enemy e) => e.Type == EnemyType.Pulsar && (e.Phase == 1 || e.Phase == 2);

        V2 InfallMove(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            switch (e.Type)
            {
                case EnemyType.Debris:
                {
                    // Off your line by up to DebrisSpiral when far, straight in up close: an inward spiral.
                    float k = MathX.Clamp01((dist - DebrisStraight) / (DebrisFar - DebrisStraight));
                    float a = DebrisSpiral * k;
                    return dir * (float)Math.Cos(a) + dir.Orthogonal() * e.Strafe * (float)Math.Sin(a);
                }
                case EnemyType.Acolyte:
                    return KeepDistance(dir, dist, AcolyteRange) + dir.Orthogonal() * e.Strafe * 0.6f;
                case EnemyType.Pulsar:
                    if (e.Phase == 1 || e.Phase == 2)
                        return V2.Zero;  // planted while its beams flicker and sweep
                    return KeepDistance(dir, dist, PulsarRange) + dir.Orthogonal() * e.Strafe * 0.3f;
                case EnemyType.Graviton:
                    if (e.Winding)
                        return V2.Zero;  // plants its feet to lob
                    return KeepDistance(dir, dist, GravitonRange) + dir.Orthogonal() * e.Strafe * 0.4f;
                case EnemyType.Orbiter:
                    return KeepDistance(dir, dist, OrbiterRange) * 0.7f + dir.Orthogonal() * e.Strafe;  // round you
                case EnemyType.MiniWell:
                case EnemyType.Implosion:
                    return V2.Zero;
            }
            return dir;
        }

        void InfallSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.Type == EnemyType.MiniWell || e.Type == EnemyType.Implosion)
            {
                e.PhaseT -= dt;
                if (e.PhaseT > 0f)
                    return;
                if (e.Type == EnemyType.Implosion)
                {
                    // The burst: a ring of shots, turned a little each time.
                    float off = Rand(0f, MathX.Tau / ImplosionShots);
                    for (int i = 0; i < ImplosionShots; i++)
                        EnemyShot(e, off + MathX.Tau * i / ImplosionShots, ImplosionShotSpeed, Defs.SmallShot);
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 90f, 0.4f, Sfx.Explode);
                }
                else
                    BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, 30f, 0.3f, Sfx.None);  // the well closes
                Enemies.Remove(e.Id);  // gone: no kill, no reward
                e.Hp = 0f;  // (and nothing else this tick: no touch)
                return;
            }
            if (e.Type != EnemyType.Pulsar)
                return;
            e.PhaseT -= dt;
            float dist = tpos.DistanceTo(e.Pos), aim = (tpos - e.Pos).Angle;
            switch (e.Phase)
            {
                case 0:
                case 3:
                    if (e.Phase == 3 && e.PhaseT > 0f)
                        break;
                    if (e.T > 1.2f && dist < PulsarRange + 90f)
                    {
                        // Plant: the beams start a little behind you and sweep through you.
                        e.Strafe = e.Strafe >= 0f ? 1f : -1f;
                        e.Facing = aim - e.Strafe * PulsarLead;
                        e.Phase = 1;
                        e.PhaseT = PulsarWarn;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    else
                        e.Phase = 0;
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 2;
                        e.PhaseT = PulsarSweep;
                    }
                    break;
                case 2:
                    e.Facing += e.Strafe * PulsarArc / PulsarSweep * dt;
                    foreach (var p in Players.Values)
                        if (!p.Downed && p.InvulnT <= 0f && PulsarBeamDistance(e, p.Pos) < PulsarBeamWidth / 2f + p.Def.HitR)
                            Damage(p, PulsarDamage, e.Def.Name + " (beam)");
                    if (e.PhaseT <= 0f)
                    {
                        e.Phase = 3;
                        e.PhaseT = PulsarRest / TraitFireMult(e);
                    }
                    break;
            }
        }

        /// How far a point is from either of a Pulsar's two beams (one each way along its Facing).
        public static float PulsarBeamDistance(Enemy e, V2 p)
        {
            V2 u = V2.FromAngle(e.Facing) * PulsarBeamLen;
            return SegmentDistance(p, e.Pos - u, e.Pos + u);
        }

        /// Returns true when it handled the shot.
        bool InfallFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Pulsar:
                    e.FireT = 99f;  // its beams are its attack (InfallSpecial)
                    return true;
                case EnemyType.Acolyte:
                    e.FireT = AcolyteEvery;
                    AcolyteVolley(e, tpos, aim);
                    return true;
                case EnemyType.Graviton:
                    e.FireT = GravitonEvery;
                    GravitonLob(e, tpos);
                    return true;
                case EnemyType.Orbiter:
                {
                    e.FireT = OrbiterEvery;
                    V2 from = e.Pos + V2.FromAngle(e.Facing) * OrbiterOrbit;
                    float ang = (tpos - from).Angle;
                    var b = SpawnBullet(Defs.FastShot, from, V2.FromAngle(ang) * OrbiterOrbSpeed, 0f, 4f, Defs.BulletDamage(Defs.FastShot), 0, false);
                    b.Source = e.Type;
                    b.Shooter = e.Id;
                    b.Look = b.Sent.Look = LookFor(e, ang, 0f);
                    e.Phase = (byte)((e.Phase + 1) % 3);  // the next orb (the look hides the one just thrown)
                    BroadcastFx(FxKind.Spark, from, e.Def.Color, 16f, 0.15f, Sfx.EnemyShot);
                    return true;
                }
            }
            return false;
        }

        /// Lobs a Small Well near where you are: the old one (if any) goes, a ring marks the spot, and the well lands
        /// GravitonFlight s later (its spawn-in).
        void GravitonLob(Enemy e, V2 tpos)
        {
            scratchIds.Clear();
            foreach (var o in Enemies.Values)
                if (o.Type == EnemyType.MiniWell && o.ParentId == e.Id)
                    scratchIds.Add(o.Id);
            foreach (int id in scratchIds)
                Enemies.Remove(id);
            V2 at = ClampToArena(tpos + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(0f, GravitonScatter), 30f);
            var w = SpawnEnemyAt(EnemyType.MiniWell, at, GravitonFlight, e.Id);
            w.PhaseT = MiniWellLife;
            BroadcastFx(FxKind.Ring, at, e.Def.Color, 26f, GravitonFlight, Sfx.Throw);
        }

        /// Host: a Collapsar died; it collapses where it stood.
        void Collapse(Enemy e)
        {
            var im = SpawnEnemyAt(EnemyType.Implosion, e.Pos, 0f, 0);
            im.PhaseT = ImplosionTime;
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, ImplosionDriftRange, ImplosionTime, Sfx.BossWarp);
        }

        /// How hard a pulling body drags players toward it, and from how far (WellDriftAt).
        static void DriftOf(EnemyType t, out float range, out float drift)
        {
            switch (t)
            {
                case EnemyType.MiniWell: range = MiniWellDriftRange; drift = MiniWellDrift; return;
                case EnemyType.Implosion: range = ImplosionDriftRange; drift = ImplosionDrift; return;
                default: range = WellDriftRange; drift = WellDrift; return;
            }
        }

        /// Three slow orbs: one straight, two that leave AcolyteSpread out either side and bend back in to meet it where
        /// you stood. A shot turning at a steady rate runs along a circle; to cross the chord d after setting off at angle
        /// s to it, it turns 2s over an arc of d·s/sin(s).
        void AcolyteVolley(Enemy e, V2 tpos, float aim)
        {
            float d = Math.Max(120f, Math.Min(600f, tpos.DistanceTo(e.Pos) - e.Radius));
            float s = AcolyteSpread, arc = d * s / (float)Math.Sin(s), time = arc / AcolyteOrbSpeed, turn = 2f * s / time;
            EnemyShot(e, aim, AcolyteOrbSpeed, Defs.SmallShot);
            for (int side = -1; side <= 1; side += 2)
            {
                float ang = aim + side * s;
                V2 dir = V2.FromAngle(ang);
                // Gone a little after the meeting point, so it doesn't loop round the arena.
                var b = SpawnBullet(Defs.SmallShot, e.Pos + dir * e.Radius, dir * AcolyteOrbSpeed, -side * turn, time * 1.5f + 0.4f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                b.Source = e.Type;
                b.Shooter = e.Id;
                b.Look = b.Sent.Look = LookFor(e, ang, -side * turn);
                NoteVolley(e, b);
            }
            BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(aim) * (e.Radius + 6f), new Rgba(1f, 0.47f, 0.86f), 14f, 0.15f, Sfx.EnemyShot);
        }
    }
}

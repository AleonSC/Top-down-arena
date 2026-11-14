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
    /// Step 3:
    ///   Lens (Gravity Wells): a melee walker with a gravity lens held in front (Facing, turning at LensTurn rad/s, so you
    ///     can get round it). Players' shots that come within LensRange of it on its front (LensArc either side of Facing)
    ///     bend away round it (MoveBullets, on every game), and a hit on that side lands on the lens: no damage (BlocksShot).
    ///   White Dwarf (White hole): Phase 0 darts in zigzagging; within WhiteReach it stops and flashes (1) for WhiteFlash s,
    ///     then pulses: players within WhitePushRange are knocked WhiteKnock px back (KnockPlayer) and your shots within
    ///     WhiteTurnRange turn round as its own (WhiteSpeed, at most WhiteMaxTurned); then (2) it backs off for
    ///     WhiteRetreat s and comes again.
    /// Step 4, the set piece: the Accretion ring (SquadForm.Accretion), eight Debris and four Orbiters in a ring of
    ///   AccretionSlots slots round a player, two of them left open (AccretionGaps). It turns at AccretionSpin rad/s and
    ///   closes from AccretionStart to AccretionEnd px at AccretionClose px/s; touching a member hurts as usual and the
    ///   Orbiters sling their orbs in. Walk out through a gap. It breaks up after AccretionLife s, or once half of it is down.
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

        public const float LensRange = 90f, LensArc = 1.05f, LensTurn = 1.3f, LensBend = 16f;
        public const float WhiteReach = 120f, WhiteFlash = 0.5f, WhiteRetreat = 1.5f, WhitePushRange = 170f, WhiteKnock = 150f, WhiteTurnRange = 150f,
            WhiteSpeed = 260f, WhiteZig = 7f;
        public const int WhiteMaxTurned = 10;

        public const float AccretionStart = 320f, AccretionEnd = 110f, AccretionClose = 22f, AccretionSpin = 0.25f, AccretionLife = 12f;
        public const int AccretionSlots = 14;
        public static readonly int[] AccretionGaps = { 3, 10 };

        sealed class AccretionRing { public V2 Center; public float Spin0; public int Size; public bool Seen, Broken; }

        /// A ring still closing in, for drawing its band and for the bots (the host fills it each tick; clients from the snapshot).
        public struct AccretionBand { public V2 Center; public float Radius, Spin; }
        public readonly System.Collections.Generic.List<AccretionBand> AccretionBands = new System.Collections.Generic.List<AccretionBand>();

        /// Host, before the enemies move: the bands are drawn afresh from the rings' members this tick.
        void PrepareAccretion()
        {
            AccretionBands.Clear();
            foreach (var r in accretionRings.Values)
                r.Seen = false;
        }

        /// The snapshot's ring block: each closing ring's middle, radius and turn.
        void SyncAccretion(NetSync s)
        {
            int n = AccretionBands.Count;
            s.Byte(ref n);
            if (s.Reading)
                AccretionBands.Clear();
            for (int i = 0; i < n; i++)
            {
                var b = s.Reading ? new AccretionBand() : AccretionBands[i];
                s.Vec(ref b.Center);
                s.Float(ref b.Radius);
                s.Float(ref b.Spin);
                if (s.Reading)
                    AccretionBands.Add(b);
            }
        }

        /// Whether an angle round a ring falls in one of its open slots.
        public static bool InAccretionGap(AccretionBand b, float ang)
        {
            float slotF = MathX.Fmod(ang - b.Spin, MathX.Tau) / (MathX.Tau / AccretionSlots);
            int slot = ((int)Math.Round(slotF)) % AccretionSlots;
            return Array.IndexOf(AccretionGaps, slot) >= 0;
        }

        /// Bots (BotBrain.cs): how dangerous standing at p is because of the Infall, beyond its bullets and bodies. A White Dwarf
        /// about to pulse, a Small Well or an Implosion pulling, and a closing Accretion ring's band (its gaps are safe, and
        /// inside a small ring is worse than outside it).
        public float InfallBotDanger(Enemy e, V2 p, float hitR)
        {
            float d = p.DistanceTo(e.Pos);
            switch (e.Type)
            {
                case EnemyType.WhiteDwarf:
                    return e.Phase == 1 && d < WhitePushRange + hitR + 20f ? 2.5f : 0f;
                case EnemyType.MiniWell:
                    return d < MiniWellDriftRange * 0.55f ? 1.2f * (1f - d / (MiniWellDriftRange * 0.55f)) + 0.3f : 0f;
                case EnemyType.Implosion:
                    return d < ImplosionDriftRange * 0.7f ? 2.5f : 0f;
            }
            return 0f;
        }

        public float AccretionBotDanger(V2 p, float hitR)
        {
            float danger = 0f;
            foreach (var b in AccretionBands)
            {
                V2 to = p - b.Center;
                float dr = to.Length;
                if (Math.Abs(dr - b.Radius) < 40f + hitR && !InAccretionGap(b, to.Angle))
                    danger += 2.5f;  // on the band, away from a gap
                else if (dr < b.Radius - 40f)
                    danger += 0.8f * (1f - b.Radius / AccretionStart) + 0.2f;  // inside: get out while the gaps are open
            }
            return danger;
        }
        readonly System.Collections.Generic.Dictionary<int, AccretionRing> accretionRings = new System.Collections.Generic.Dictionary<int, AccretionRing>();  // host: by squad id

        /// The ring slot of a squad's i-th member: the slots in order, skipping the gaps.
        public static int AccretionSlot(int i)
        {
            for (int slot = 0, n = 0; slot < AccretionSlots; slot++)
            {
                if (Array.IndexOf(AccretionGaps, slot) >= 0)
                    continue;
                if (n++ == i)
                    return slot;
            }
            return i;
        }

        static V2 AccretionPoint(V2 center, float spin, int slot, float radius) => center + V2.FromAngle(spin + MathX.Tau * slot / AccretionSlots) * radius;

        /// Where the ring's i-th member comes in: round the player (the ring's middle kept far enough from the walls).
        V2 AccretionSpawn(int squad, int i, Player target)
        {
            if (!accretionRings.TryGetValue(squad, out var ring))
            {
                V2 lo = ArenaMin + new V2(180f, 180f), hi = ArenaMax - new V2(180f, 180f);
                V2 c = new V2(Math.Max(lo.X, Math.Min(hi.X, target.Pos.X)), Math.Max(lo.Y, Math.Min(hi.Y, target.Pos.Y)));
                accretionRings[squad] = ring = new AccretionRing { Center = c, Spin0 = Rand(0f, MathX.Tau) };
            }
            ring.Size++;
            return ClampToArena(AccretionPoint(ring.Center, ring.Spin0, AccretionSlot(i), AccretionStart), 30f);
        }

        /// Its place in the turning, closing ring; once the ring breaks up, its own way.
        V2 AccretionMove(Enemy e, System.Collections.Generic.List<Enemy> mates, V2 v)
        {
            var ring = accretionRings[e.SquadId];
            if (ring.Broken || e.T > AccretionLife || mates.Count * 2 < ring.Size)
            {
                ring.Broken = true;  // broken up: everyone its own way, and no band
                return v;
            }
            float t = Math.Max(0f, e.T - 0.9f);  // (from when it has spawned in)
            float radius = Math.Max(AccretionEnd, AccretionStart - AccretionClose * t);
            if (!ring.Seen)
            {
                ring.Seen = true;
                AccretionBands.Add(new AccretionBand { Center = ring.Center, Radius = radius, Spin = ring.Spin0 + AccretionSpin * t });
            }
            return GoalVel(e, ClampToArena(AccretionPoint(ring.Center, ring.Spin0 + AccretionSpin * t, AccretionSlot(e.SquadSlot), radius), 30f));
        }

        public static bool IsInfall(EnemyType t) => t >= EnemyType.Debris && t <= EnemyType.WhiteDwarf;

        /// A Lens's face is toward `from`: shots from there bend away, and a hit from there lands on the lens.
        public static bool LensFaces(Enemy e, V2 from) =>
            e.Type == EnemyType.Lens && Math.Abs(MathX.Fmod((from - e.Pos).Angle - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi) < LensArc;

        /// Every game, for each player's shot (MoveBullets): near a Lens's face it turns away from the Lens.
        void BendRoundLenses(Bullet b, float dt)
        {
            foreach (var e in lenses)
            {
                float d = b.Pos.DistanceTo(e.Pos);
                if (d > LensRange + e.Radius || !LensFaces(e, b.Pos))
                    continue;
                float toLens = (e.Pos - b.Pos).Angle, have = b.Vel.Angle;
                float diff = MathX.Fmod(toLens - have + MathX.Pi, MathX.Tau) - MathX.Pi;  // heading this far off the Lens's middle
                if (Math.Abs(diff) > MathX.Pi / 2f)
                    return;  // already going away
                float rate = LensBend * (1f - Math.Min(1f, d / (LensRange + e.Radius))) * dt + 2f * dt;
                b.Vel = b.Vel.Rotated(diff >= 0f ? -rate : rate);  // turn away from its middle, the side it's already on
                return;
            }
        }

        readonly System.Collections.Generic.List<Enemy> lenses = new System.Collections.Generic.List<Enemy>();

        /// MoveBullets, before the bullets move: the Lenses on the field.
        bool PrepareLenses()
        {
            lenses.Clear();
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Lens && !IsSpawning(e))
                    lenses.Add(e);
            return lenses.Count > 0;
        }

        /// A Lens turns slowly toward you.
        void LensFace(Enemy e, float want, float dt)
        {
            float diff = MathX.Fmod(want - e.Facing + MathX.Pi, MathX.Tau) - MathX.Pi;
            e.Facing += Math.Max(-LensTurn * dt, Math.Min(LensTurn * dt, diff));
        }

        /// Pulls players toward it (WellDriftAt): the boss's corner wells, a Graviton's Small Well, a Collapsar's Implosion.
        static bool Pulls(EnemyType t) => t == EnemyType.GravityWell || t == EnemyType.MiniWell || t == EnemyType.Implosion;

        /// The Pulsar keeps its beams' line while they flicker and sweep.
        public static bool PulsarLocked(Enemy e) => e.Type == EnemyType.Pulsar && (e.Phase == 1 || e.Phase == 2);


        /// A Small Well or an Implosion: when its time is up it bursts into a ring (Burst: an Implosion) or just closes;
        /// then it's gone.
        void WellClose(Enemy e, float dt, bool burst)
        {
            e.PhaseT -= dt;
            if (e.PhaseT > 0f)
                return;
            if (burst)
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
        }

        /// A Pulsar: plants, flickers, sweeps its two beams a quarter turn, rests.
        void PulsarSpecial(Enemy e, V2 tpos, float dt)
        {
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

        void WhiteDwarfSpecial(Enemy e, V2 tpos, float dt)
        {
            e.PhaseT -= dt;
            switch (e.Phase)
            {
                case 0:
                    if (e.T > 0.8f && tpos.DistanceTo(e.Pos) < WhiteReach)
                    {
                        e.Phase = 1;
                        e.PhaseT = WhiteFlash;
                        BroadcastSound(Sfx.BossCharge);
                    }
                    break;
                case 1:
                    if (e.PhaseT <= 0f)
                    {
                        WhitePulse(e);
                        e.Phase = 2;
                        e.PhaseT = WhiteRetreat;
                    }
                    break;
                case 2:
                    if (e.PhaseT <= 0f)
                        e.Phase = 0;
                    break;
            }
        }

        /// The pulse: players near it are shoved back, and the players' shots near it turn round as its own.
        void WhitePulse(Enemy e)
        {
            BroadcastFx(FxKind.Ring, e.Pos, e.Def.Color, WhitePushRange, 0.35f, Sfx.Shockwave);
            foreach (var p in Players.Values)
            {
                if (p.Downed)
                    continue;
                float d = p.Pos.DistanceTo(e.Pos);
                if (d < WhitePushRange + p.Def.BodyR)
                    KnockPlayer(p, d > 1f ? (p.Pos - e.Pos) / d : V2.FromAngle(Rand(0f, MathX.Tau)), WhiteKnock);  // WardenFaction.cs
            }
            scratchIds.Clear();
            foreach (var b in Bullets.Values)
                if (b.Owner != 0 && !Defs.IsEnemyShot(b.Kind) && b.Pos.DistanceTo(e.Pos) < WhiteTurnRange && scratchIds.Count < WhiteMaxTurned)
                    scratchIds.Add(b.Id);
            var turned = new System.Collections.Generic.List<V2>();
            foreach (int id in scratchIds)
            {
                var b = Bullets[id];
                turned.Add(b.Pos);
                turned.Add(b.Vel.IsZero ? V2.FromAngle(0f) : -b.Vel.Normalized());
                KillBullet(id);
            }
            for (int i = 0; i < turned.Count; i += 2)
            {
                var nb = SpawnBullet(Defs.SmallShot, turned[i], turned[i + 1] * WhiteSpeed, 0f, 3f, Defs.BulletDamage(Defs.SmallShot), 0, false);
                nb.Source = e.Type;
                nb.Shooter = e.Id;
                nb.Look = nb.Sent.Look = LookFor(e, turned[i + 1].Angle, 0f);
            }
        }

        /// How far a point is from either of a Pulsar's two beams (one each way along its Facing).
        public static float PulsarBeamDistance(Enemy e, V2 p)
        {
            V2 u = V2.FromAngle(e.Facing) * PulsarBeamLen;
            return SegmentDistance(p, e.Pos - u, e.Pos + u);
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
        void CollapsarDied(Enemy e)
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
        /// (moved from InfallMove, Debris)
        V2 InfallMove_Debris(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            // Off your line by up to DebrisSpiral when far, straight in up close: an inward spiral.
            float k = MathX.Clamp01((dist - DebrisStraight) / (DebrisFar - DebrisStraight));
            float a = DebrisSpiral * k;
            return dir * (float)Math.Cos(a) + dir.Orthogonal() * e.Strafe * (float)Math.Sin(a);
        }

        /// (moved from InfallMove, VoidAcolyte)
        V2 InfallMove_VoidAcolyte(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, AcolyteRange) + dir.Orthogonal() * e.Strafe * 0.6f;
        }

        /// (moved from InfallMove, Pulsar)
        V2 InfallMove_Pulsar(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            if (e.Phase == 1 || e.Phase == 2)
                return V2.Zero;  // planted while its beams flicker and sweep
            return KeepDistance(dir, dist, PulsarRange) + dir.Orthogonal() * e.Strafe * 0.3f;
        }

        /// (moved from InfallMove, Graviton)
        V2 InfallMove_Graviton(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            if (e.Winding)
                return V2.Zero;  // plants its feet to lob
            return KeepDistance(dir, dist, GravitonRange) + dir.Orthogonal() * e.Strafe * 0.4f;
        }

        /// (moved from InfallMove, Orbiter)
        V2 InfallMove_Orbiter(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return KeepDistance(dir, dist, OrbiterRange) * 0.7f + dir.Orthogonal() * e.Strafe;  // round you
        }

        /// (moved from InfallMove, MiniWell, Implosion)
        V2 InfallMove_MiniWell(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            return V2.Zero;
        }

        /// (moved from InfallMove, WhiteDwarf)
        V2 InfallMove_WhiteDwarf(Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed)
        {
            if (e.Phase == 1)
                return V2.Zero;  // flashing
            if (e.Phase == 2)
                return -dir + dir.Orthogonal() * e.Strafe * 0.5f;  // backing off
            return dir + dir.Orthogonal() * (float)Math.Sin(e.T * WhiteZig + e.Id) * 0.7f;  // darting in, zigzagging
        }

        /// (moved from InfallFire, Pulsar)
        bool InfallFire_Pulsar(Enemy e, V2 tpos, float aim)
        {
            e.FireT = 99f;  // its beams are its attack (InfallSpecial)
            return true;
        }

        /// (moved from InfallFire, VoidAcolyte)
        bool InfallFire_VoidAcolyte(Enemy e, V2 tpos, float aim)
        {
            e.FireT = AcolyteEvery;
            AcolyteVolley(e, tpos, aim);
            return true;
        }

        /// (moved from InfallFire, Graviton)
        bool InfallFire_Graviton(Enemy e, V2 tpos, float aim)
        {
            e.FireT = GravitonEvery;
            GravitonLob(e, tpos);
            return true;
        }

        /// (moved from InfallFire, Orbiter)
        bool InfallFire_Orbiter(Enemy e, V2 tpos, float aim)
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

        // --- Brains (Docs/EnemyBehaviours.md step 8d) ------------------------------------------------------------------

        /// A Lens's face: it blocks hits from the front (LensFaces).
        sealed class LensGuard : GuardPart
        {
            public override bool Blocks(Game g, Enemy e, V2 from) => LensFaces(e, from);
        }

        /// A Lens moves straight at you and turns slowly (LensFace).
        sealed class LensMove : MovePart
        {
            public override V2 Move(Game g, Enemy e, V2 tpos, V2 dir, float dist, float dt, ref float speed) => dir;
            public override bool Turn(Game g, Enemy e, float want, V2 tpos, float dt)
            {
                g.LensFace(e, want, dt);
                return true;
            }
        }

        static void InfallBrains(Brain[] t)
        {
            t[(int)EnemyType.Debris] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_Debris(e, p, d, di, dt, ref sp)) };
            t[(int)EnemyType.VoidAcolyte] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_VoidAcolyte(e, p, d, di, dt, ref sp)),
                Fire = new CodeFire((g, e, p, a) => g.InfallFire_VoidAcolyte(e, p, a)) };
            t[(int)EnemyType.Pulsar] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_Pulsar(e, p, d, di, dt, ref sp), e => e.Phase == 1 || e.Phase == 2),
                Special = new CodeSpecial((g, e, p, dt) => g.PulsarSpecial(e, p, dt)), Fire = new CodeFire((g, e, p, a) => g.InfallFire_Pulsar(e, p, a)) };
            t[(int)EnemyType.Graviton] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_Graviton(e, p, d, di, dt, ref sp)),
                Fire = new CodeFire((g, e, p, a) => g.InfallFire_Graviton(e, p, a)) };
            t[(int)EnemyType.Orbiter] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_Orbiter(e, p, d, di, dt, ref sp)),
                Fire = new CodeFire((g, e, p, a) => g.InfallFire_Orbiter(e, p, a)) };
            t[(int)EnemyType.Collapsar] = new Brain { Move = new ChaseMove() };
            var still = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_MiniWell(e, p, d, di, dt, ref sp));
            t[(int)EnemyType.MiniWell] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.WellClose(e, dt, false)) };
            t[(int)EnemyType.Implosion] = new Brain { Move = still, Special = new CodeSpecial((g, e, p, dt) => g.WellClose(e, dt, true)) };
            t[(int)EnemyType.Lens] = new Brain { Move = new LensMove(), Guard = new LensGuard() };
            t[(int)EnemyType.WhiteDwarf] = new Brain { Move = new CodeMove((Game g, Enemy e, V2 p, V2 d, float di, float dt, ref float sp) => g.InfallMove_WhiteDwarf(e, p, d, di, dt, ref sp)),
                Special = new CodeSpecial((g, e, p, dt) => g.WhiteDwarfSpecial(e, p, dt)) };
        }
    }
}

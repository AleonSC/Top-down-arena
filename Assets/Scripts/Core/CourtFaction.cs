using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Duelist's faction, the Masked Court (Docs/Approaches.md "The Masked Court", mockup Docs/Mockups/duelist-faction.html).
    /// Masked fencers, each previewing one of the Duelist's moves. Step 1 is three enemies that share a Warden counterpart's
    /// behaviour through HiveKin's table:
    ///   Fencer (a Shiv): the Lunger's line and leap, shorter: it starts within FencerStart, shows its line for FencerWarn
    ///     and hops for FencerHop (about 100 px, FencerHopDamage on a hit), then recovers for FencerRecover (EnemyAI.cs, the Lunger's case).
    ///   Blade Dancer (a Guard): within DancerRange it shows the Duelist's warning line for DancerWarn, then lunges up to
    ///     DancerReach and leaves a blade every DancerBladeEvery; each hangs DancerBladeHang and fires one shot out of each
    ///     side. It reuses the Duelist's dash and blades (Duelist.cs: a DuelState with DuelDash.Dancer, TickDuelDash,
    ///     TickDuelBlades, SyncDuel; drawn by DuelistLook's DrawDuelFloor).
    ///   Parrier (a Riot Shield): its shield is a main-gauche. A shot it blocks from the front is sent straight back at
    ///     whoever fired it (ParrierBounce, from Collide; at most one every ParrierEvery s), like the Duelist's Guard.
    /// Step 2:
    ///   Second (a Security Guard): moves, locks on and winds up like one, but each shot is an aimed fan of SecondFan
    ///     daggers (Defs.ThrownDagger), every SecondEvery s, and it never reloads (CourtFire).
    ///   Marshal (a Gas Grenadier): keeps its distance and winds up like one (no lob), then draws a dueling circle
    ///     (PatchKind.DuelCircle) MarshalR round where you stand: it forms over MarshalWarn, then for MarshalHold s a player
    ///     inside can't leave (KeepInMarshalCircles, each game for its own player), and leaning on the edge hurts
    ///     (MarshalDamage, at most every MarshalHurtEvery s, UpdateCourt). The Duelist's own circle, small.
    ///   Gallant (a Taser Guard that never fires): within GallantRange it salutes for GallantTell (the Duelist's salute:
    ///     a DuelState's SaluteT), then lunges through you (DuelDash.Salute). A perfect parry as it arrives knocks it off
    ///     balance for StaggerTime, taking DuelOpenMult damage (DuelOpen); dodged, it rakes the spot with GallantRake shots.
    /// Step 3:
    ///   Harlequin (a Thug): within HarlequinRange, the Crossing. It shows a warning line through you (HarlequinWarn) and
    ///     dashes it, HarlequinArm past you, leaving the Duelist's crossing blades (DuelDash.Cross: they fire out sideways);
    ///     then it vaults to the other arm of the X, shows the crossing line (HarlequinWarn2) and dashes that. Stand in a
    ///     gap of the X. Every HarlequinEvery s. (Enemy.CrossAt is where the X crosses; DuelState.Step is how far it is.)
    ///   Mummer (a Shiv): within MummerRange it blinks and becomes three: it and two Mummer Copies on a ring MummerRing
    ///     round you, circling for MummerMask s, each firing every MummerShotEvery s; only its own shots are solid, the
    ///     copies' are hollow (Defs.HollowShot, harmless). Copies have 1 health and no XP; they vanish when time is up or
    ///     it dies (MummerDied). Clients can't tell them apart: they look the same.
    public static class CourtKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Fencer && t <= EnemyType.MummerCopy;
    }

    public partial class Enemy
    {
        public V2 CrossAt;     // host: where a Harlequin's X crosses
        public float MaskT;    // host: a Mummer's masquerade time left (> 0: masked)
        public float DanceT;   // host: a Blade Dancer's rest until it may lunge again; a Parrier's until it may parry again
    }

    public partial class Game
    {
        public const float FencerStart = 160f, FencerWarn = 0.4f, FencerHop = 0.16f, FencerRecover = 0.55f, FencerHopDamage = 16f;
        public const float DancerRange = 300f, DancerReach = 230f, DancerWarn = 0.6f, DancerEvery = 3.5f, DancerSpeed = 1100f;
        public const float DancerBladeEvery = 70f, DancerBladeHang = 0.6f, DancerBladeSpeed = 170f;
        public const float ParrierEvery = 0.12f, ParrierBounceSpeed = 320f;
        public const float SecondEvery = 2.2f, SecondSpread = 0.16f, SecondSpeed = 230f;
        public const int SecondFan = 5;
        public const float MarshalEvery = 7f, MarshalR = 140f, MarshalWarn = 0.6f, MarshalHold = 4f, MarshalDamage = 8f, MarshalHurtEvery = 0.6f;
        public const float GallantRange = 340f, GallantEvery = 4.5f, GallantTell = 0.8f, GallantReach = 400f;
        public const int GallantRake = 5;
        public const float HarlequinRange = 260f, HarlequinArm = 170f, HarlequinWarn = 0.7f, HarlequinWarn2 = 0.45f, HarlequinEvery = 6f;
        public const float MummerRange = 260f, MummerRing = 150f, MummerMask = 5f, MummerSpin = 0.6f, MummerShotEvery = 0.9f, MummerEvery = 9f, MummerShotSpeed = 260f;
        public static readonly Rgba MummerViolet = new Rgba(0.71f, 0.61f, 1f);
        public static readonly Rgba CourtGoldColor = new Rgba(1f, 0.8f, 0.35f);

        /// A Blade Dancer showing its line or lunging (it moves itself, so its walk stops).
        public static bool DancerBusy(Enemy e) => e.Type == EnemyType.BladeDancer && e.Duel != null && e.Duel.Dash != DuelDash.None;

        /// Host, in Special: its blades count down and fire; its lunge runs; rested and close enough, it shows a new line.
        void BladeDancerSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = e.Duel ?? (e.Duel = new DuelState { Step = 0 });
            TickDuelBlades(s, dt, true, EnemyType.BladeDancer);
            if (s.Dash != DuelDash.None)
            {
                TickDuelDash(e, s, dt);  // Duelist.cs
                return;
            }
            e.DanceT -= dt;
            float dist = e.Pos.DistanceTo(tpos);
            if (e.DanceT > 0f || e.SpawnT > 0f || dist > DancerRange)
                return;
            e.DanceT = DancerEvery;
            s.Dash = DuelDash.Dancer;
            s.DashWarn = DancerWarn;
            s.DashFrom = e.Pos;
            s.DashAng = (tpos - e.Pos).Angle;
            s.DashLen = Math.Min(DancerReach, dist + 40f);
            s.DashDone = 0f;
            e.Facing = s.DashAng;
        }

        readonly List<Bullet> parrierBounces = new List<Bullet>();

        /// Host, in Collide: a shot its main-gauche stopped goes back at whoever fired it (fired next tick, as the
        /// Duelist's guard does, since Collide is walking the bullets).
        void ParrierBounce(Enemy e, Bullet b)
        {
            if (b.Owner == 0 || e.DanceT > 0f)  // (only a Parrier's guard calls it: ShieldBrains.cs)
                return;
            e.DanceT = ParrierEvery;
            V2 at = e.Pos + (b.Pos - e.Pos).Normalized() * (e.Radius + 10f);
            float aim = Players.TryGetValue(b.Owner, out Player shooter) ? (shooter.Pos - at).Angle : (b.Pos - e.Pos).Angle;
            parrierBounces.Add(new Bullet { Pos = at, Vel = V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * ParrierBounceSpeed });
        }

        /// Host, in EnemyFire (a Marshal's brain, LobBrains.cs): the baton points and a dueling circle draws where you stand.
        void MarshalCircle(Enemy e, V2 tpos)
        {
            e.FireT = MarshalEvery;
            SpawnPatchAt(PatchKind.DuelCircle, ClampToArena(tpos, MarshalR * 0.5f), MarshalWarn, MarshalWarn + MarshalHold, MarshalR, MarshalR);
            BroadcastFx(FxKind.Ring, tpos, CourtGoldColor, MarshalR, MarshalWarn, Sfx.BossGong);  // the baton points: the circle draws
        }

        /// Host, in EnemyFire: the Second's dagger fan; a Gallant never shoots (its lunge is
        /// GallantSpecial). False for anyone else.
        bool CourtFire(Enemy e, V2 tpos, float aim)
        {
            switch (e.Type)
            {
                case EnemyType.Second:
                    e.FireT = SecondEvery;
                    e.Phase = 1;  // (a Security Guard's rounds left: it never runs dry)
                    for (int i = 0; i < SecondFan; i++)
                        EnemyShot(e, e.Facing + (i - (SecondFan - 1) / 2f) * SecondSpread, SecondSpeed, Defs.ThrownDagger);
                    BroadcastFx(FxKind.Spark, e.Pos + V2.FromAngle(e.Facing) * (e.Radius + 10f), CourtGoldColor, 16f, 0.15f, Sfx.Throw);
                    return true;
                case EnemyType.Gallant:
                    e.FireT = 99f;
                    return true;
            }
            return false;
        }

        /// A Gallant saluting, lunging or reeling from a parry: it doesn't walk.
        public static bool GallantBusy(Enemy e) => e.Type == EnemyType.Gallant && e.Duel != null && (e.Duel.SaluteT > 0f || e.Duel.Dash != DuelDash.None || e.Duel.StaggerT > 0f);

        /// Host, in Special: reeling, saluting, lunging; rested and in range, a new salute.
        void GallantSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = e.Duel ?? (e.Duel = new DuelState { Step = 0 });
            if (s.StaggerT > 0f)
            {
                s.StaggerT = Math.Max(0f, s.StaggerT - dt);
                return;
            }
            if (s.Dash != DuelDash.None)
            {
                TickDuelDash(e, s, dt);  // Duelist.cs: a perfect parry staggers it, a dodge gets raked
                return;
            }
            if (s.SaluteT > 0f)
            {
                e.Facing = (tpos - e.Pos).Angle;
                s.SaluteT -= dt;
                if (s.SaluteT <= 0f)
                {
                    s.SaluteT = 0f;
                    s.Connected = false;
                    s.Dash = DuelDash.Salute;
                    s.DashWarn = 0f;
                    s.DashFrom = e.Pos;
                    s.DashAng = e.Facing;
                    s.DashLen = Math.Min(GallantReach, e.Pos.DistanceTo(tpos) + 40f);
                    s.DashDone = 0f;
                    BroadcastSound(Sfx.BossScythe);
                }
                return;
            }
            e.DanceT -= dt;
            if (e.DanceT > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > GallantRange)
                return;
            e.DanceT = GallantEvery;
            s.SaluteT = GallantTell;
            e.Facing = (tpos - e.Pos).Angle;
            BroadcastFx(FxKind.Ring, e.Pos, CourtGoldColor, 50f, GallantTell, Sfx.None);  // the salute's glint
        }

        /// A Harlequin showing a line, dashing or vaulting: it doesn't walk.
        public static bool HarlequinBusy(Enemy e) => e.Type == EnemyType.Harlequin && e.Duel != null && (e.Duel.Dash != DuelDash.None || e.Duel.Step > 0);

        /// Host, in Special: its blades fire; the first arm, the vault, the crossing arm; rested and in range, a new X.
        void HarlequinSpecial(Enemy e, V2 tpos, float dt)
        {
            var s = e.Duel ?? (e.Duel = new DuelState { Step = 0 });
            TickDuelBlades(s, dt, true, EnemyType.Harlequin);
            if (s.Dash != DuelDash.None)
            {
                TickDuelDash(e, s, dt);  // Duelist.cs: the crossing's blades
                return;
            }
            if (s.Step == 1)
            {
                // The vault: to the other arm of the X, a right angle round from the first, on the side with more room.
                float first = s.DashAng;
                V2 side = V2.FromAngle(first + MathX.Pi / 2f);
                V2 mid = new V2(Width / 2f, Height / 2f);
                if ((mid - e.CrossAt).Dot(side) < 0f)
                    side = side * -1f;
                V2 from = ClampToArena(e.CrossAt + side * HarlequinArm, e.Radius + 10f);
                BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.Harlequin).Color, 40f, 0.25f, Sfx.BossWhoosh);
                e.Pos = from;
                BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.Harlequin).Color, 40f, 0.25f, Sfx.None);
                StartCross(e, s, (e.CrossAt - from).Angle, from.DistanceTo(e.CrossAt) + HarlequinArm, HarlequinWarn2);
                s.Step = 2;
                return;
            }
            if (s.Step == 2)
            {
                s.Step = 0;
                e.DanceT = HarlequinEvery;
                return;
            }
            e.DanceT -= dt;
            float dist = e.Pos.DistanceTo(tpos);
            if (e.DanceT > 0f || e.SpawnT > 0f || dist > HarlequinRange)
                return;
            e.CrossAt = tpos;
            StartCross(e, s, (tpos - e.Pos).Angle, dist + HarlequinArm, HarlequinWarn);
            s.Step = 1;
        }

        void StartCross(Enemy e, DuelState s, float ang, float len, float warn)
        {
            s.Dash = DuelDash.Cross;
            s.DashWarn = warn;
            s.DashFrom = e.Pos;
            s.DashAng = ang;
            s.DashLen = len;
            s.DashDone = 0f;
            e.Facing = ang;
        }

        /// A Mummer in its masquerade (circling you with its copies).
        public static bool MummerMasked(Enemy e) => e.Type == EnemyType.Mummer && e.MaskT > 0f;

        /// Host, in Special: rested and in range, it blinks into three; masked, it and its copies circle you and fire. True
        /// while masked (its Shiv lunge waits).
        bool MummerSpecial(Enemy e, V2 tpos, float dt)
        {
            if (e.MaskT <= 0f)
            {
                e.DanceT -= dt;
                if (e.DanceT > 0f || e.SpawnT > 0f || e.Phase != 0 || e.Pos.DistanceTo(tpos) > MummerRange)
                    return false;
                // The blink: a puff where it was, then three of it round you.
                e.DanceT = MummerEvery;
                e.MaskT = MummerMask;
                e.CrossAt = tpos;
                e.Ang = (e.Pos - tpos).Angle;
                BroadcastFx(FxKind.Ring, e.Pos, MummerViolet, 40f, 0.3f, Sfx.BossWhoosh);
                for (int k = 1; k <= 2; k++)
                {
                    var c = SpawnEnemyAt(EnemyType.MummerCopy, MummerSlot(e, k), 0f, e.Id);
                    c.SpawnT = 0f;
                    c.Spawning = false;
                    c.PhaseT = MummerShotEvery * k / 3f;  // (its shot clock: FireT is a melee enemy's, held off)
                    BroadcastFx(FxKind.Ring, c.Pos, MummerViolet, 30f, 0.3f, Sfx.None);
                }
                e.PhaseT = 0f;
                e.Pos = MummerSlot(e, 0);
                return true;
            }
            e.MaskT -= dt;
            e.CrossAt = tpos;  // the ring follows you
            e.Ang += MummerSpin * dt;
            e.Pos = MummerSlot(e, 0);
            e.Facing = (tpos - e.Pos).Angle;
            e.Phase = 0;
            e.PhaseT -= dt;  // (its shot clock while masked; its lunge waits)
            if (e.PhaseT <= 0f)
            {
                e.PhaseT = MummerShotEvery;
                EnemyShot(e, e.Facing, MummerShotSpeed, Defs.SmallShot);
            }
            int slot = 1;
            foreach (var c in Enemies.Values)
            {
                if (c.Type != EnemyType.MummerCopy || c.ParentId != e.Id || c.Hp <= 0f)
                    continue;
                c.Pos = MummerSlot(e, slot++);
                c.Facing = (tpos - c.Pos).Angle;
                c.PhaseT -= dt;
                if (c.PhaseT <= 0f)
                {
                    c.PhaseT = MummerShotEvery;
                    V2 dir = V2.FromAngle(c.Facing);
                    SpawnBullet(Defs.HollowShot, c.Pos + dir * c.Radius, dir * MummerShotSpeed, 0f, 4f, 0f, 0, true).Source = EnemyType.Mummer;
                }
            }
            if (e.MaskT <= 0f)
            {
                EndMasquerade(e);
                e.PhaseT = Rand(0.8f, 1.6f);  // (then a Shiv's rest before its next lunge)
            }
            return true;
        }

        /// Slot k (0: the Mummer itself) on its ring round you, a third of a turn apart.
        V2 MummerSlot(Enemy e, int k) => ClampToArena(e.CrossAt + V2.FromAngle(e.Ang + MathX.Tau * k / 3f) * MummerRing, e.Radius + 4f);

        /// Host: its copies vanish (no credit), each with a puff.
        void EndMasquerade(Enemy e)
        {
            e.MaskT = 0f;
            foreach (var c in Enemies.Values)
                if (c.Type == EnemyType.MummerCopy && c.ParentId == e.Id && c.Hp > 0f)
                {
                    c.LastHitBy = 0;
                    c.Hp = 0f;
                    BroadcastFx(FxKind.Ring, c.Pos, MummerViolet, 30f, 0.3f, Sfx.None);
                }
        }

        /// Host, as an enemy dies: a Mummer takes its copies with it; a copy shot down puffs.
        void MummerDied(Enemy e)
        {
            if (e.Type == EnemyType.Mummer)
                EndMasquerade(e);
            else if (e.Type == EnemyType.MummerCopy)
                BroadcastFx(FxKind.Ring, e.Pos, MummerViolet, 30f, 0.3f, Sfx.Hit);
        }

        // --- The Ring of Honour (step 4, the set piece) ----------------------------------------------------------------

        public const float HonourStart = 300f, HonourEnd = 150f, HonourClose = 14f, HonourSpin = 0.15f, HonourLife = 14f, HonourEvery = 1.4f, HonourFirst = 2f;
        public const int HonourSlots = 12;

        sealed class HonourRing { public V2 Center; public float Spin0; public int Size; public bool Broken; }
        readonly Dictionary<int, HonourRing> honourRings = new Dictionary<int, HonourRing>();  // host: by squad id
        readonly Dictionary<int, int> honourTurn = new Dictionary<int, int>();                 // host: enemy id -> the last pair turn it lunged on

        static V2 HonourPoint(V2 center, float spin, int slot, float radius) => center + V2.FromAngle(spin + MathX.Tau * slot / HonourSlots) * radius;

        /// Where the ring's i-th member comes in: round the player (the ring's middle kept far enough from the walls).
        V2 HonourSpawn(int squad, int i, Player target)
        {
            if (!honourRings.TryGetValue(squad, out var ring))
            {
                V2 lo = ArenaMin + new V2(170f, 170f), hi = ArenaMax - new V2(170f, 170f);
                V2 c = new V2(Math.Max(lo.X, Math.Min(hi.X, target.Pos.X)), Math.Max(lo.Y, Math.Min(hi.Y, target.Pos.Y)));
                honourRings[squad] = ring = new HonourRing { Center = c, Spin0 = Rand(0f, MathX.Tau) };
            }
            ring.Size++;
            return ClampToArena(HonourPoint(ring.Center, ring.Spin0, i % HonourSlots, HonourStart), 30f);
        }

        HonourRing HonourOf(Enemy e, List<Enemy> mates)
        {
            if (e.SquadId == 0 || !honourRings.TryGetValue(e.SquadId, out var ring) || ring.Broken)
                return null;
            if (e.T > HonourLife || mates != null && mates.Count * 2 < ring.Size)
            {
                ring.Broken = true;  // broken up: everyone its own way
                return null;
            }
            return ring;
        }

        /// Its place in the turning, closing ring, facing in. Every HonourEvery s the next opposite pair of Fencers lunges in
        /// (the Lunger's own line and hop); the Gallants salute on their own clock. Lunging or saluting, it goes its own way.
        V2 HonourMove(Enemy e, List<Enemy> mates, V2 v)
        {
            var ring = HonourOf(e, mates);
            if (ring == null)
                return v;
            float t = Math.Max(0f, e.T - 0.9f);  // (from when it has spawned in)
            if (e.Phase != 0 || GallantBusy(e))
                return v;  // lunging, or its salute
            float radius = Math.Max(HonourEnd, HonourStart - HonourClose * t);
            int slot = e.SquadSlot % HonourSlots;
            V2 at = ClampToArena(HonourPoint(ring.Center, ring.Spin0 + HonourSpin * t, slot, radius), 30f);
            e.Facing = (ring.Center - e.Pos).Angle;
            if (e.Type == EnemyType.Fencer && t >= HonourFirst)
            {
                int turn = (int)((t - HonourFirst) / HonourEvery);
                honourTurn.TryGetValue(e.Id, out int last);
                if (turn % (HonourSlots / 2) == slot % (HonourSlots / 2) && last != turn + 1)
                {
                    honourTurn[e.Id] = turn + 1;
                    e.Phase = 1;  // its turn: the line, then the hop (EnemyAI.cs, the Lunger's case)
                    e.PhaseT = FencerWarn;
                    return V2.Zero;
                }
            }
            return GoalVel(e, at);
        }

        /// A Fencer still in the ring waits for its turn to lunge (Variety.cs LungerWaits).
        bool HonourHolds(Enemy e) => e.Type == EnemyType.Fencer && squadMembers.TryGetValue(e.SquadId, out var mates) && HonourOf(e, mates) != null;

        /// Your own game: a Marshal's circle you're inside holds you (from your move, `before` the step).
        V2 KeepInMarshalCircles(V2 before, V2 pos, float r)
        {
            foreach (var pt in Patches.Values)
            {
                if (pt.Kind != PatchKind.DuelCircle || !pt.Active)
                    continue;
                float lim = pt.R0 - r;
                if (before.DistanceTo(pt.Pos) > lim + 1f)
                    continue;  // you weren't inside it
                V2 d = pos - pt.Pos;
                if (d.LengthSq > lim * lim)
                    pos = pt.Pos + d.Normalized() * lim;
            }
            return pos;
        }

        readonly Dictionary<int, float> marshalHurt = new Dictionary<int, float>();  // host: player id -> seconds until the edge may hurt again

        /// Every game, each frame: on the host, Parriers' rests run down and their parried shots fly, and a player leaning
        /// on a Marshal's circle is hurt.
        void UpdateCourt(float dt)
        {
            if (!IsHost)
                return;
            foreach (var p in Players.Values)
            {
                marshalHurt.TryGetValue(p.Id, out float wait);
                wait -= dt;
                if (wait <= 0f && !p.Downed && p.InvulnT <= 0f)
                    foreach (var pt in Patches.Values)
                    {
                        if (pt.Kind != PatchKind.DuelCircle || !pt.Active)
                            continue;
                        float d = p.Pos.DistanceTo(pt.Pos);
                        if (d < pt.R0 - p.Def.BodyR + 1f && d > pt.R0 - p.Def.BodyR - 3f)  // pressed on the edge from inside
                        {
                            Damage(p, MarshalDamage, "Marshal (dueling circle)");
                            wait = MarshalHurtEvery;
                            break;
                        }
                    }
                marshalHurt[p.Id] = Math.Max(0f, wait);
            }
            foreach (var e in Enemies.Values)
                if (e.Type == EnemyType.Parrier && e.DanceT > 0f)
                    e.DanceT -= dt;
            foreach (var b in parrierBounces)
                SpawnBullet(Defs.SmallShot, b.Pos, b.Vel, 0f, 6f, Defs.BulletDamage(Defs.SmallShot), 0, true).Source = EnemyType.Parrier;
            if (parrierBounces.Count > 0)
                BroadcastSound(Sfx.Clang);
            parrierBounces.Clear();
        }
    }
}

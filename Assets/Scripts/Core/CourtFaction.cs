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
    public static class CourtKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Fencer && t <= EnemyType.Gallant;
    }

    public partial class Enemy
    {
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
            if (e.Type != EnemyType.Parrier || b.Owner == 0 || e.DanceT > 0f)
                return;
            e.DanceT = ParrierEvery;
            V2 at = e.Pos + (b.Pos - e.Pos).Normalized() * (e.Radius + 10f);
            float aim = Players.TryGetValue(b.Owner, out Player shooter) ? (shooter.Pos - at).Angle : (b.Pos - e.Pos).Angle;
            parrierBounces.Add(new Bullet { Pos = at, Vel = V2.FromAngle(aim + Rand(-0.06f, 0.06f)) * ParrierBounceSpeed });
        }

        /// Host, in EnemyFire: the Second's dagger fan and the Marshal's circle; a Gallant never shoots (its lunge is
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
                case EnemyType.Marshal:
                    e.FireT = MarshalEvery;
                    SpawnPatchAt(PatchKind.DuelCircle, ClampToArena(tpos, MarshalR * 0.5f), MarshalWarn, MarshalWarn + MarshalHold, MarshalR, MarshalR);
                    BroadcastFx(FxKind.Ring, tpos, CourtGoldColor, MarshalR, MarshalWarn, Sfx.BossGong);  // the baton points: the circle draws
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

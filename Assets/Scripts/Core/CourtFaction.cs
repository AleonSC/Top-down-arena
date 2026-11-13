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
    public static class CourtKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Fencer && t <= EnemyType.Parrier;
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

        /// Every game, each frame: on the host, Parriers' rests run down and their parried shots fly.
        void UpdateCourt(float dt)
        {
            if (!IsHost)
                return;
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

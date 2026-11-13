using System;

namespace Arena
{
    /// The Colossus's faction, The Quarry (Docs/Approaches.md "The Quarry", mockup Docs/Mockups/colossus-faction.html).
    /// Step 1 is three enemies that share their Warden counterpart's behaviour through HiveKin's table: Rubble plays like a
    /// Rioter, the Quarryman like a Guard and the Stomper like a Thug (a Brute). The Stomper's own trick is a small
    /// Colossus stomp (Colossus.cs StompRing): within StomperRange of its target it plants its feet and lifts a foot for
    /// StomperWind (the ring's Warn, while its green gap already shows), then stomps out a ring of push blocks with one
    /// gap a short step beside you, which fades at StomperReach. Each Stomper has at most one ring (Enemy.Stomp,
    /// replicated by SyncStomper); every game spreads it, the host hurts whoever it passes over, and each game pushes its
    /// own player out of it (StomperPush), as the Colossus's rings do.
    public static class QuarryKin
    {
        public static bool Is(EnemyType t) => t >= EnemyType.Rubble && t <= EnemyType.Stomper;
    }

    public partial class Enemy
    {
        public StompRing Stomp;  // a Stomper's ring: winding up (Warn > 0, it stands still) or spreading (replicated)
        public float StompT;     // host: until it may stomp again
    }

    public partial class Game
    {
        public const float StomperEvery = 4f, StomperWind = 0.8f, StomperRange = 260f, StomperReach = 220f, StomperDamage = 12f;

        /// Lifting its foot: it stands still until the stomp lands.
        public static bool StomperPlanted(Enemy e) => e.Stomp != null && e.Stomp.Warn > 0f;

        /// How high its foot is, 0 to 1 (drawing).
        public static float StomperLift(Enemy e) => StomperPlanted(e) ? MathX.Clamp01(1f - e.Stomp.Warn / StomperWind) : 0f;

        /// Host, in Special: starts a stomp once it's close enough and rested. The gap is placed now, so it shows through the wind-up.
        void StomperSpecial(Enemy e, V2 tpos, float dt)
        {
            e.StompT -= dt;
            if (e.Stomp != null || e.StompT > 0f || e.SpawnT > 0f || e.Pos.DistanceTo(tpos) > StomperRange)
                return;
            e.StompT = StomperEvery;
            float gap = GapBeside(e, tpos, Rand(0f, 1f) < 0.5f ? -1f : 1f);
            e.Stomp = new StompRing { Center = e.Pos, Radius = e.Radius + 6f, Warn = StomperWind, Gap1 = MathX.Fmod(gap, MathX.Tau) };
        }

        /// Every game, each frame: the wind-up runs down, then the ring spreads until StomperReach; the host hurts once
        /// whoever it passes over outside the gap.
        void UpdateStompRings(float dt)
        {
            foreach (var e in Enemies.Values)
            {
                var r = e.Stomp;
                if (r == null || e.Type != EnemyType.Stomper)
                    continue;
                if (r.Warn > 0f)
                {
                    r.Warn -= dt;
                    if (r.Warn <= 0f && IsHost)
                        BroadcastFx(FxKind.Ring, e.Pos, Defs.Get(EnemyType.Stomper).Color, e.Radius + 40f, 0.35f, Sfx.BossSlam);  // the stomp
                    continue;
                }
                r.Radius += RingSpeed * dt;
                if (IsHost)
                    HurtByRing(r, StomperDamage, "Stomper (stomp)");
                if (RingBack(r) > StomperReach)
                    e.Stomp = null;
            }
        }

        /// Your own game: a Stomper's ring passes over you or shoves you back, like the Colossus's.
        void StomperPush(Player me)
        {
            if (me.Downed)
                return;
            foreach (var e in Enemies.Values)
                if (e.Stomp != null && e.Type == EnemyType.Stomper)
                    PushOutOfRing(e.Stomp, me);
        }

        /// The ring a Stomper has going, if any (the snapshot; the rest of its state is the Thug's).
        void SyncStomper(NetSync s, Enemy e)
        {
            bool has = !s.Reading && e.Stomp != null;
            s.Bool(ref has);
            if (!has)
            {
                if (s.Reading)
                    e.Stomp = null;
                return;
            }
            if (s.Reading && e.Stomp == null)
                e.Stomp = new StompRing();
            var r = e.Stomp;
            s.Vec(ref r.Center);
            s.Float(ref r.Radius);
            s.Float(ref r.Warn);
            s.Float(ref r.Gap1);
        }
    }
}

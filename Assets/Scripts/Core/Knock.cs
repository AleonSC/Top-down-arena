using System;

namespace Arena
{
    /// Pushes slide. A knocked-back enemy travels its push over KnockTime instead of jumping the whole way in one tick
    /// (hard to follow by eye): every push (the Repulsion Cannon, Roar and the shockwave, the Scattergun's point-blank
    /// shove, Knight swings, Bulldoze, the Fuma Shuriken's fan) goes through KnockEnemy, and TickKnock moves
    /// the enemy each tick. Pushes that land together add up. Host only: clients see the slide in the snapshot, and a
    /// FxKind.Knock effect (start and end) draws its dust streak and impact puff on every screen (WorldEffects.cs).
    public partial class Game
    {
        public const float KnockTime = 0.18f;   // how long a push takes to travel
        public const float KnockFxMin = 24f;    // pushes shorter than this slide but show no streak (the Scattergun's shove)
        public const float KnockFxEvery = 0.25f; // at most one streak per enemy this often
        public const float KnockFxDur = 0.45f;
        public const float WallSlamMin = 8f;      // a push must lose at least this many px to the wall to hurt
        public const float WallSlamBase = 6f, WallSlamPerPx = 0.12f, WallSlamMax = 40f;  // slam damage: base + per px of push the wall ate, capped

        /// Knock `e` by `delta` (direction × distance): it slides there over KnockTime.
        void KnockEnemy(Enemy e, V2 delta)
        {
            if (delta.Length < 0.01f)
                return;
            e.KnockLeft += delta;
            e.KnockSpeed = e.KnockLeft.Length / KnockTime;
            if (delta.Length >= KnockFxMin && e.T - e.KnockFxAt >= KnockFxEvery)
            {
                e.KnockFxAt = e.T;
                BroadcastFx(FxKind.Knock, e.Pos, e.Pos + e.KnockLeft, e.Def.Color, e.Radius, KnockFxDur, Sfx.None);
            }
        }

        /// Each tick, for each enemy: travel some of a push still to go.
        void TickKnock(Enemy e, float dt)
        {
            float len = e.KnockLeft.Length;
            if (len <= 0f)
                return;
            float step = Math.Min(len, e.KnockSpeed * dt);
            V2 move = e.KnockLeft * (step / len);
            V2 want = e.Pos + move;
            e.Pos = ClampToArena(want, e.Radius);
            float lost = (want - e.Pos).Length;
            if (lost > 0.01f && len - step + lost >= WallSlamMin)
            {
                // Slammed into the arena wall: the push that's left is spent as damage, and the slide stops.
                float dmg = Math.Min(WallSlamMax, WallSlamBase + WallSlamPerPx * (len - step + lost));
                e.KnockLeft = V2.Zero;
                e.KnockSpeed = 0f;
                BroadcastFx(FxKind.Spark, e.Pos, e.Pos, e.Def.Color, e.Radius, 0.25f, Sfx.None);
                HurtEnemy(e, dmg, 0);
                return;
            }
            if (len - step < 0.01f)
            {
                e.KnockLeft = V2.Zero;
                e.KnockSpeed = 0f;
            }
            else
                e.KnockLeft -= move;
        }
    }
}

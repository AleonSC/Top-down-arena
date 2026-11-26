using UnityEngine;

namespace Arena
{
    /// The Acupuncture Needles' look (Core/AcupunctureNeedles.cs; Docs/Mockups/acupuncture-needles.html). Drawing only.
    ///   - a needle in flight: a thin steel shaft with a bead handle, a faint trail behind it; jade for a healing needle
    ///     (Defs.NeedleShot), longer with a blood-red bead for Bloodletting (Defs.BloodNeedle);
    ///   - needles in a teammate (Player.NeedleN): stuck at the body's edge pointing out the way they came, their beads
    ///     pulsing, a jade halo and motes rising while they heal, and five pips under the body;
    ///   - needles in an enemy (Enemy.NeedlesIn): red-beaded needles at its edge;
    ///   - FxKind.NeedleBurst: Meridian Open (jade) or Pressure Point (blood): the needles fly out of the body, a ring.
    /// With Visual effects off: no trails, halo or motes.
    public partial class ArenaApp
    {
        static readonly Rgba NeedleSteel = new Rgba(0.8f, 0.85f, 0.84f);

        /// One needle: its point at `tip`, its shaft running back along -dir, `len` long, a bead on the handle.
        void DrawAcuNeedle(V2 tip, V2 dir, float len, Rgba bead, float alpha = 1f)
        {
            V2 handle = tip - dir * len;
            Line(handle, tip, 1.6f, NeedleSteel.WithAlpha(alpha));
            Line(tip - dir * 2.5f, tip, 1f, bead.WithAlpha(alpha));
            Circle(handle, 2.3f, bead.WithAlpha(alpha), 8);
            Circle(handle - dir * 0.6f, 0.8f, Rgba.White.WithAlpha(0.7f * alpha), 4);
        }

        /// A needle in flight (WorldShots.DrawNinjaShot).
        void DrawAcuNeedleShot(Bullet b)
        {
            bool blood = b.Kind == Defs.BloodNeedle;
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized();
            Rgba bead = blood ? Game.NeedleBlood : Game.NeedleJade;
            if (fxOn)
            {
                Line(b.Pos - dir * 30f, b.Pos - dir * 8f, 2.2f, bead.WithAlpha(0.12f));
                Line(b.Pos - dir * 18f, b.Pos - dir * 6f, 1.6f, bead.WithAlpha(0.25f));
            }
            DrawAcuNeedle(b.Pos + dir * 4f, dir, blood ? 16f : 12f, bead);
        }

        /// Where the k-th needle sits round a body: spread round it, the same each time.
        static float NeedleAngle(int k, int id) => k * 2.4f + id * 0.9f + 0.6f;

        /// Needles in a teammate, healing (WorldRenderer, under the HUD bits).
        void DrawNeedlesInPlayer(Player p, float t)
        {
            float r = p.Def.BodyR;
            V2 c = p.Disp;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
            if (fxOn)
            {
                Arc(c, r + 4f, 3f, Game.NeedleJade.WithAlpha(0.12f + 0.08f * pulse), 28);
                for (int k = 0; k < 3; k++)  // motes rising off you while it heals
                {
                    float f = MathX.Fmod(t * 1.1f + k * 0.33f + p.Id * 0.3f, 1f);
                    V2 at = c + new V2((k - 1f) * r * 0.5f, r * 0.5f - f * r * 1.8f);
                    Circle(at, 2f * (1f - f) + 0.7f, Game.NeedleJade.WithAlpha(0.8f * (1f - f)), 6);
                }
            }
            for (int k = 0; k < p.NeedleN; k++)
            {
                V2 dir = V2.FromAngle(NeedleAngle(k, p.Id));
                DrawAcuNeedle(c + dir * (r - 3f), dir * -1f, 13f, Game.NeedleJade.Mix(Rgba.White, 0.25f * pulse));
            }
            for (int k = 0; k < Game.NeedleMax; k++)  // how many of five: the fifth opens the meridian
                Circle(c + new V2((k - 2f) * 5f, r + 7f), 1.6f, k < p.NeedleN ? Game.NeedleJade : new Rgba(1f, 1f, 1f, 0.18f), 6);
        }

        /// Bloodletting needles in an enemy (WorldEnemies, next to the stuck stars).
        void DrawNeedlesInEnemy(Enemy e, V2 pos, float r)
        {
            for (int k = 0; k < e.NeedlesIn && k < Game.NeedleMax; k++)
            {
                V2 dir = V2.FromAngle(NeedleAngle(k, e.Id));
                DrawAcuNeedle(pos + dir * (r - 3f), dir * -1f, 14f, Game.NeedleBlood);
            }
        }

        /// FxKind.NeedleBurst: the needles fly out of the body at once, a ring opening behind them.
        void DrawNeedleBurstFx(Effect fx, float f)
        {
            float k = 1f - f, ease = 1f - k * k;
            int id = (int)fx.Pos2.Y;
            if (fxOn)
            {
                Circle(fx.Pos, fx.Radius + 30f * ease, fx.Color.WithAlpha(0.14f * k), 24);
                Arc(fx.Pos, fx.Radius + 6f + 40f * ease, 1f + 2.5f * k, fx.Color.WithAlpha(0.85f * k), 32);
            }
            for (int i = 0; i < Game.NeedleMax; i++)
            {
                V2 dir = V2.FromAngle(NeedleAngle(i, id));
                DrawAcuNeedle(fx.Pos + dir * (fx.Radius + 4f + 36f * ease), dir, 11f, fx.Color, k);
            }
        }
    }
}

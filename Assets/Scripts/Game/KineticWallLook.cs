using UnityEngine;

namespace Arena
{
    /// Kinetic Wall (Core/KineticWall.cs), look B "Gravity Lens" of Docs/Mockups/kinetic-wall.html: a violet lens bowing out in front of
    /// where it was cast, soft bands under two bright rims and a ripple drawn in toward its middle; caught shots hang in it, drifting
    /// in small orbits, brighter as the charge climbs; a countdown along its outer edge and a charge bar under the caster. The caster's
    /// own screen shows the cone the unleash will cover. FxKind.KwCatch is a shot slowing to a halt in it, FxKind.KwBlast the unleash:
    /// ripple arcs rolling out over a violet fan, the hung shots flung along it.
    public partial class ArenaApp
    {
        static readonly Rgba KwRim = new Rgba(0.86f, 0.82f, 1f), KwCore = new Rgba(0.98f, 0.96f, 1f);

        static float KwHash(int i) => Mathf.Repeat(Mathf.Sin(i * 12.9898f + 4.1f) * 43758.5453f, 1f);

        /// A caught shot's place, drifting round its hang point.
        static V2 KwDotAt(Player p, int i, float t)
        {
            Game.KwDot(p, i, out float lat, out float rad);
            float o = t * (0.8f + KwHash(i) * 0.6f) + KwHash(i + 7) * 6.28f;
            return Game.KwDotPos(p, lat, rad) + new V2(Mathf.Cos(o) * 3.5f, Mathf.Sin(o) * 2.5f);
        }

        /// Called for each player with a lens up.
        void DrawKineticWall(Player p, float t)
        {
            if (p.KwT <= 0f)
                return;
            V2 c = Game.KwCentre(p);
            float R = Game.KwRadius, span = Game.KwSpan(p), from = p.KwAim - span, sweep = span * 2f;
            float k = Mathf.Clamp01(p.KwDots.Count / (float)Game.KwCapOf(p));
            float age = Game.KwTime - p.KwT, grow = Mathf.Clamp01(age / 0.25f), sw = sweep * (0.4f + 0.6f * grow), sf = p.KwAim - sw * 0.5f;
            float a = grow * Mathf.Clamp01(p.KwT / 0.2f + 0.3f);
            var col = Game.KwColor;

            GlowDisc(Game.KwDotPos(p, 0f, 0f), 90f + 40f * k, col, (0.12f + 0.2f * k) * a);
            for (int i = 0; i < 3; i++)  // soft bands
                Arc(c, R, 22f - i * 6f, col.WithAlpha((0.16f + 0.18f * k) * (1f - i * 0.25f) * a), 28, sf, sw);
            Arc(c, R + 9f, 1.8f, KwRim.WithAlpha(0.9f * a), 28, sf, sw);  // the rims
            Arc(c, R - 9f, 1.8f, KwRim.WithAlpha(0.7f * a), 28, sf, sw);
            float rp = Mathf.Repeat(t * 0.9f, 1f);  // a ripple drawn in from the outer rim
            Arc(c, R + 9f - rp * 18f, 1.2f, KwRim.WithAlpha(0.5f * (1f - rp) * a), 28, sf, sw);
            for (int s = -1; s <= 1; s += 2)  // end caps
            {
                V2 e0 = c + V2.FromAngle(p.KwAim + s * sw * 0.5f) * (R - 11f), e1 = c + V2.FromAngle(p.KwAim + s * sw * 0.5f) * (R + 11f);
                Line(e0, e1, 2.2f, KwRim.WithAlpha(0.85f * a));
            }
            float left = Mathf.Clamp01(p.KwT / Game.KwTime);  // time left: a thin line along the outer edge, from the middle out
            Arc(c, R + 15f, 2.2f, col.Mix(Rgba.White, 0.4f).WithAlpha(0.8f * a), 28, p.KwAim - span * left, span * 2f * left);

            for (int i = 0; i < p.KwDots.Count; i++)  // the hung shots
            {
                V2 d = KwDotAt(p, i, t);
                GlowDisc(d, 9f + 5f * k, col, 0.45f);
                Circle(d, 3.8f, col.Mix(Rgba.White, 0.35f + 0.4f * k), 10);
                Circle(d, 1.6f, KwCore, 6);
            }

            if (p.Downed)
                return;
            V2 under = p.Disp + new V2(0f, p.Def.BodyR + 14f);  // the charge bar under the caster
            float bw = 54f;
            Line(under - new V2(bw * 0.5f, 0f), under + new V2(bw * 0.5f, 0f), 5f, new Rgba(0.1f, 0.12f, 0.16f, 0.85f));
            if (k > 0f)
                Line(under - new V2(bw * 0.5f, 0f), under + new V2(bw * (k - 0.5f), 0f), 3.4f, col.Mix(Rgba.White, 0.3f));
            if (p.Id == game.MyId)  // where the unleash will reach
            {
                float face = (p.AimAt - p.Pos).IsZero ? p.Aim : (p.AimAt - p.Pos).Angle, half = Game.KwConeHalf, reach = Game.KwReach;
                var edge = col.WithAlpha(0.28f);
                Line(p.Disp + V2.FromAngle(face - half) * 30f, p.Disp + V2.FromAngle(face - half) * reach, 1.4f, edge);
                Line(p.Disp + V2.FromAngle(face + half) * 30f, p.Disp + V2.FromAngle(face + half) * reach, 1.4f, edge);
                Arc(p.Disp, reach, 1.4f, edge, 20, face - half, half * 2f);
            }
        }

        /// FxKind.KwCatch: a shot sliding the last stretch into the lens as it slows, and a small flash where it stops.
        void DrawKwCatchFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), ease = 1f - (1f - f) * (1f - f);
            V2 dir = V2.FromAngle(fx.Pos2.Y);
            V2 at = fx.Pos - dir * (16f * (1f - ease));
            var col = fx.Color;
            Circle(at, 3.5f + 1.5f * (1f - f), col.Mix(new Rgba(1f, 0.55f, 0.4f), 1f - ease), 10);  // warm while moving, violet once held
            Arc(fx.Pos, 6f + 12f * f, 1.5f, KwRim.WithAlpha(0.8f * (1f - f)), 16);
        }

        /// FxKind.KwBlast: the unleash. A bright fan grows out to the reach in 0.18 s and fades; four ripple arcs roll out; a streak per
        /// charge flies along it.
        void DrawKwBlastFx(Effect fx)
        {
            float T = fx.T, grow = Mathf.Clamp01(T / 0.18f), fade = T < 0.18f ? 1f : Mathf.Clamp01(1f - (T - 0.18f) / Mathf.Max(0.01f, fx.Dur - 0.18f));
            V2 o = fx.Pos, far = fx.Pos2 - fx.Pos;
            float face = far.Angle, reach = far.Length, half = Game.KwConeHalf, r = reach * grow;
            var col = fx.Color;
            FanGradient(o, r, KwCore.WithAlpha(0.5f * fade), col.WithAlpha(0f), 18, face - half, half * 2f);
            Wedge(o, r, face, half, col.WithAlpha(0.22f * fade), 18);
            for (int i = 0; i < 4; i++)
            {
                float rr = reach * Mathf.Min(1f, T * 1.7f + i * 0.05f) - i * 26f;
                if (rr > 20f)
                    Arc(o, rr, 5f - i, KwRim.WithAlpha(fade * (0.85f - i * 0.18f)), 20, face - half, half * 2f);
            }
            for (int s = -1; s <= 1; s += 2)
                Line(o, o + V2.FromAngle(face + s * half) * r, 1.6f, KwCore.WithAlpha(0.9f * fade));
            int n = Mathf.Min(60, (int)fx.Radius);
            for (int i = 0; i < n; i++)  // the hung shots, flung out
            {
                float a = face + (KwHash(i) * 2f - 1f) * half * 0.9f, d = 60f + T * (520f + KwHash(i + 3) * 160f);
                if (d > reach)
                    continue;
                V2 head = o + V2.FromAngle(a) * d;
                Line(head - V2.FromAngle(a) * 18f, head, 2.4f, col.Mix(Rgba.White, 0.4f).WithAlpha(fade));
                Circle(head, 2.6f, KwCore.WithAlpha(fade), 6);
            }
            if (T < 0.2f)
                GlowDisc(o + V2.FromAngle(face) * 30f, 70f, col, 0.6f * (1f - T / 0.2f));
        }
    }
}

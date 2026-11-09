using UnityEngine;

namespace Arena
{
    /// A perfect parry (Core/Parry.cs; Docs/Mockups/parry-perfect-rework.html), the same for every character in its own colour:
    /// the shield's whole arc flashes white, a ring and sparks burst from where the shot met it, and the shot goes straight back
    /// as a fast white needle trailing the parrier's colour (Defs.ParryNeedle). The world slowing and your own picture holding
    /// are Core/Parry.cs (WorldSlowT) and Juice.cs.
    public partial class ArenaApp
    {
        const float PerfectFlashTime = 0.2f;

        Player PerfectParrier(Effect fx)
        {
            Player p;
            return game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p : null;
        }

        /// The body pass: the flashing shield arc, the ring where the shot met it and the sparks thrown off.
        void DrawPerfectParryFx(Effect fx)
        {
            var p = PerfectParrier(fx);
            float age = fx.T, aim = fx.Pos2.Y, half = fx.Radius;
            Rgba col = fx.Color.WithAlpha(1f);
            V2 c = p != null ? p.Disp : fx.Pos - V2.FromAngle(aim) * 24f;
            float rr = (p != null ? p.Def.BodyR : 14f) + Game.ParryReach * 0.55f;
            if (age < PerfectFlashTime)
            {
                float k = 1f - age / PerfectFlashTime, grow = 0.15f * k;
                Arc(c, rr + 2f, 4f + 8f * k, Rgba.White.WithAlpha(0.95f * k), 24, aim - half - grow, (half + grow) * 2f);
                Arc(c, rr + 9f + 6f * (1f - k), 2f, col.WithAlpha(0.9f * k), 24, aim - half, half * 2f);
            }
            if (age < 0.3f)  // the ring from the point of contact
            {
                float q = age / 0.3f;
                Arc(fx.Pos, 4f + 28f * EaseOut3(q), 2.5f * (1f - q) + 0.5f, col.WithAlpha(1f - q), 20);
                Arc(c, rr + 4f + 22f * EaseOut3(q), 2f * (1f - q) + 0.5f, Rgba.White.WithAlpha(0.6f * (1f - q)), 28);
            }
            float sk = Mathf.Clamp01(1f - age / 0.32f);
            if (sk > 0f)  // sparks thrown back along the shield's face
                for (int i = 0; i < 12; i++)
                {
                    float side = (i & 1) == 0 ? 1f : -1f, an = aim + side * (MathX.Pi / 2f) + (Hash01(i * 3.7f) - 0.5f) * 1.3f;
                    float d = (60f + 160f * Hash01(i * 1.9f + 0.4f)) * EaseOut3(Mathf.Clamp01(age / 0.32f));
                    V2 at = fx.Pos + V2.FromAngle(an) * d, tail = fx.Pos + V2.FromAngle(an) * (d * 0.7f);
                    Line(tail, at, 2f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(sk));
                }
        }

        /// The additive pass: a burst of light at the contact point and along the shield.
        void DrawPerfectParryGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.PerfectParry || fx.T > 0.3f)
                    continue;
                float k = 1f - fx.T / 0.3f;
                GlowDisc(fx.Pos, 46f, fx.Color.WithAlpha(1f), 0.7f * k);
                GlowDisc(fx.Pos, 18f, Rgba.White, 0.8f * k);
            }
        }

        /// A parried shot flying back: a white needle, its trail the parrier's colour.
        void DrawParryNeedle(Bullet b)
        {
            if (b.Vel.IsZero)
                return;
            Player o;
            Rgba col = game.Players.TryGetValue(b.Owner, out o) ? o.Def.Color : Rgba.White;
            V2 d = b.Vel.Normalized(), side = d.Orthogonal();
            float flown = Mathf.Max(0f, (b.Life0 - b.Life) * Game.NeedleSpeed), tl = Mathf.Min(70f, flown);
            V2 tail = b.Pos - d * tl;
            VTri(tail, col.WithAlpha(0f), b.Pos + side * 3.5f, col.WithAlpha(0.75f), b.Pos - side * 3.5f, col.WithAlpha(0.75f));
            V2 mid = b.Pos - d * Mathf.Min(34f, tl);
            VTri(mid, Rgba.White.WithAlpha(0f), b.Pos + side * 1.3f, Rgba.White, b.Pos - side * 1.3f, Rgba.White);
            VTri(b.Pos + side * 1.3f, Rgba.White, b.Pos + d * 6f, Rgba.White, b.Pos - side * 1.3f, Rgba.White);
        }
    }
}

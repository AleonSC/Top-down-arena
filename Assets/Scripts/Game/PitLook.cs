using UnityEngine;

namespace Arena
{
    /// Pits (Core/Pits.cs) on the floor, in the owner's Hazard rim look (Docs/Mockups/cell-pits.html, look B): a band of gold and
    /// black stripes marching round the edge, a black void, and a gold grid falling away inside it (outlines shrinking to the
    /// middle, lines down to a far floor). Drawn over the floor and under the fog veil and the walls; a pit in a room you haven't
    /// seen stays hidden. FxKind.PitFall: the body falling (shrinking and turning into the dark); a player then rises on the rim.
    public partial class ArenaApp
    {
        static readonly Rgba PitGold = Rgba.Gold, PitDark = new Rgba(0.09f, 0.07f, 0.03f), PitVoid = new Rgba(0.005f, 0.005f, 0.012f);
        const float PitBand = 7f, PitDash = 12f, PitMarch = 28f;

        void DrawPits(float t)
        {
            var pits = game.ActivePits;
            foreach (var s in pits)
            {
                if (game.FogHidesStill(s.Pos))
                    continue;
                if (s.Round) DrawRoundPit(s.Pos, s.R, t);
                else DrawBoxPit(s.Pos, s.HW, s.HH, t);
            }
        }

        void DrawRoundPit(V2 c, float r, float t)
        {
            Arc(c, r + PitBand * 0.5f, PitBand, PitDark, 48);  // the band, then its stripes marching round
            float ring = MathX.Tau * (r + PitBand * 0.5f);
            int n = Mathf.Max(6, Mathf.RoundToInt(ring / (2f * PitDash)));
            float step = MathX.Tau / n, shift = (t * PitMarch / ring * MathX.Tau) % step;
            for (int i = 0; i < n; i++)
                Arc(c, r + PitBand * 0.5f, PitBand, PitGold.WithAlpha(0.95f), 4, i * step + shift, step * 0.5f);
            Circle(c, r, PitVoid, 40);
            for (int k = 0; k < 5; k++)  // the grid falling away: outlines shrinking to the middle
            {
                float f = (k + (t * 0.6f) % 1f) / 5f, rr = r * (1f - Mathf.Pow(f, 0.7f));
                if (rr > 1.5f)
                    Arc(c, rr, 1.4f, PitGold.WithAlpha(0.2f * (1f - f)), 32);
            }
            for (int a = 0; a < 12; a++)  // and lines down to the far floor
            {
                V2 d = V2.FromAngle(MathX.Tau * a / 12f);
                Line(c + d * r * 0.97f, c + d * r * 0.16f, 1.2f, PitGold.WithAlpha(0.11f));
            }
            Circle(c, r * 0.55f, PitVoid.WithAlpha(0.6f), 28);  // (deeper in the middle)
            Arc(c, r - 0.8f, 1.6f, PitGold.WithAlpha(0.7f), 48);  // the lip
        }

        void DrawBoxPit(V2 c, float hw, float hh, float t)
        {
            float x0 = c.X - hw, y0 = c.Y - hh, w = hw * 2f, h = hh * 2f, b = PitBand;
            FillRect(x0 - b, y0 - b, w + 2f * b, h + 2f * b, PitDark);
            // The stripes along each side, marching clockwise; the corners stay dark.
            float per = 2f * (w + h), off = (t * PitMarch) % (2f * PitDash);
            for (float s = -2f * PitDash + off; s < per; s += 2f * PitDash)
                StripeAlong(x0, y0, w, h, Mathf.Max(0f, s), Mathf.Min(per, s + PitDash), b);
            FillRect(x0, y0, w, h, PitVoid);
            for (int k = 0; k < 5; k++)  // the grid falling away
            {
                float f = (k + (t * 0.6f) % 1f) / 5f, g = 1f - Mathf.Pow(f, 0.7f);
                float ix = hw * g, iy = hh * g;
                if (ix > 1.5f && iy > 1.5f)
                    RectEdge(c.X - ix, c.Y - iy, ix * 2f, iy * 2f, 1.4f, PitGold.WithAlpha(0.2f * (1f - f)));
            }
            float fx = hw * 0.18f, fy = hh * 0.18f;  // the corners down to a far floor
            Line(new V2(x0, y0), new V2(c.X - fx, c.Y - fy), 1.2f, PitGold.WithAlpha(0.12f));
            Line(new V2(x0 + w, y0), new V2(c.X + fx, c.Y - fy), 1.2f, PitGold.WithAlpha(0.12f));
            Line(new V2(x0, y0 + h), new V2(c.X - fx, c.Y + fy), 1.2f, PitGold.WithAlpha(0.12f));
            Line(new V2(x0 + w, y0 + h), new V2(c.X + fx, c.Y + fy), 1.2f, PitGold.WithAlpha(0.12f));
            FillRect(c.X - hw * 0.5f, c.Y - hh * 0.5f, hw, hh, PitVoid.WithAlpha(0.55f));  // (deeper in the middle)
            RectEdge(x0, y0, w, h, 1.6f, PitGold.WithAlpha(0.7f));  // the lip
        }

        /// One stripe of a box pit's band: from s0 to s1 along its edge (clockwise from the top left), b wide, outside the edge.
        void StripeAlong(float x0, float y0, float w, float h, float s0, float s1, float b)
        {
            float[] lens = { w, h, w, h };
            float at = 0f;
            for (int side = 0; side < 4; side++)
            {
                float a = Mathf.Max(s0, at), z = Mathf.Min(s1, at + lens[side]);
                if (z > a)
                {
                    float u0 = a - at, u1 = z - at;
                    var col = PitGold.WithAlpha(0.95f);
                    switch (side)
                    {
                        case 0: FillRect(x0 + u0, y0 - b, u1 - u0, b, col); break;
                        case 1: FillRect(x0 + w, y0 + u0, b, u1 - u0, col); break;
                        case 2: FillRect(x0 + w - u1, y0 + h, u1 - u0, b, col); break;
                        default: FillRect(x0 - b, y0 + h - u1, b, u1 - u0, col); break;
                    }
                }
                at += lens[side];
            }
        }

        void RectEdge(float x, float y, float w, float h, float lw, Rgba c)
        {
            FillRect(x, y, w, lw, c);
            FillRect(x, y + h - lw, w, lw, c);
            FillRect(x, y + lw, lw, h - 2f * lw, c);
            FillRect(x + w - lw, y + lw, lw, h - 2f * lw, c);
        }

        /// A player down in a pit isn't drawn (the fall's look stands in): your own while `PitFallT` runs; a teammate while a player's
        /// fall (Color.A 1) at their spot is still falling.
        bool PitHides(Player p)
        {
            if (!ReferenceEquals(game, session.Game))
                return false;
            if (p.Id == game.MyId)
                return game.PitFallT > 0f;
            foreach (var fx in FxOf(FxKind.PitFall))
                if (fx.Kind == FxKind.PitFall && fx.Color.A > 0.75f && fx.T < Game.PitDrop && p.Disp.CloserThan(fx.Pos, 30f))
                    return true;
            return false;
        }

        /// FxKind.PitFall: the body shrinks and turns into the dark where it fell; a player (Color.A 1) then rises on the rim in a
        /// gold ring with chevrons; an enemy (0.5) leaves a red puff over the void.
        void DrawPitFall(Effect fx, float f)
        {
            bool player = fx.Color.A > 0.75f;
            var col = new Rgba(fx.Color.R, fx.Color.G, fx.Color.B, 1f);
            float r = Mathf.Max(8f, fx.Radius);
            float fall = Mathf.Clamp01(f / 0.55f);
            if (fall < 1f)  // falling: shrinking, turning, fading to black
            {
                float k = 1f - fall * fall, spin = fall * 4f;
                Circle(fx.Pos, r * k, col.Mix(PitVoid, fall).WithAlpha(0.85f), 20);
                for (int i = 0; i < 3; i++)
                {
                    float a = spin + MathX.Tau * i / 3f;
                    Arc(fx.Pos, r * k * 1.15f, 2f, col.WithAlpha(0.9f * (1f - fall)), 6, a, 1.3f);
                }
                if (f < 0.25f)
                    Arc(fx.Pos, r * (1.2f + f * 3f), 2f, PitGold.WithAlpha(0.8f * (1f - f / 0.25f)), 28);  // dust off the lip
            }
            if (!player)
            {
                if (f > 0.45f)  // the kill: a red puff and sparks up out of the dark
                {
                    float g = (f - 0.45f) / 0.55f;
                    for (int i = 0; i < 8; i++)
                    {
                        V2 d = V2.FromAngle(MathX.Tau * i / 8f + 0.3f);
                        V2 p = fx.Pos + d * (r * 0.4f + g * r * 1.6f);
                        Circle(p, 2.5f * (1f - g) + 0.5f, new Rgba(1f, 0.3f, 0.34f, 1f - g), 6);
                    }
                }
                return;
            }
            if (f > 0.35f)  // climbing out on the rim
            {
                float g = (f - 0.35f) / 0.65f;
                Arc(fx.Pos2, r + 4f + g * 26f, 2.5f * (1f - g) + 0.5f, PitGold.WithAlpha(0.9f * (1f - g)), 32);
                for (int i = 0; i < 3; i++)
                {
                    float y = fx.Pos2.Y - r - 6f - i * 8f - g * 16f;
                    var cc = PitGold.WithAlpha((1f - g) * (1f - i * 0.25f));
                    Line(new V2(fx.Pos2.X - 7f, y + 5f), new V2(fx.Pos2.X, y), 2f, cc);
                    Line(new V2(fx.Pos2.X, y), new V2(fx.Pos2.X + 7f, y + 5f), 2f, cc);
                }
            }
        }
    }
}

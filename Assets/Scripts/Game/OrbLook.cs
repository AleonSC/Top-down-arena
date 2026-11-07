using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        const byte PBit = 12, PHexRing = 13, PPlusUp = 14;  // particle kinds (Juice.cs draws them through DrawOrbPart)

        static float OrbHash(float n) { float s = Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f; return s - (float)System.Math.Floor(s); }

        /// A thin ellipse outline (rx, ry), drawn as short lines.
        void EllipseLine(V2 c, float rx, float ry, float w, Rgba col, int segs = 16)
        {
            V2 prev = c + new V2(rx, 0f);
            for (int i = 1; i <= segs; i++)
            {
                float a = MathX.Tau * i / segs;
                V2 next = c + new V2(Mathf.Cos(a) * rx, Mathf.Sin(a) * ry);
                Line(prev, next, w, col);
                prev = next;
            }
        }

        /// A health orb as a projected hologram (Mockups/digital-health-orbs.html, look C): a see-through
        /// wireframe sphere with scrolling latitude lines, a turning meridian, a flat white plus that
        /// glitches now and then, and a ring of ticks. Big orbs keep the gold ring; tiny ones are paler.
        void DrawHoloOrb(Orb o, float t)
        {
            var col = o.Tiny ? new Rgba(0.55f, 1f, 0.75f) : new Rgba(0.35f, 1f, 0.5f);
            float r = o.Radius * 1.1f * (1f + 0.05f * Mathf.Sin(t * 8f + o.Id));
            float lw = o.Big ? 1.6f : 1.2f;
            V2 c = o.Disp;
            Circle(c, r * 2f, col.WithAlpha(0.08f), 20);
            Circle(c, r, col.WithAlpha(0.18f), 16);
            // scrolling latitude rings, squashed so the sphere reads as seen from above
            float ph = MathX.Fmod(t * 0.6f, 1f);
            for (int i = -3; i <= 3; i++)
            {
                float f = (i + ph) / 3.5f;
                float w = Mathf.Sqrt(Mathf.Max(0f, 1f - f * f)) * r;
                if (w > 0.5f)
                    EllipseLine(c + new V2(0f, f * r), w, w * 0.28f, lw, col.WithAlpha(0.75f), 10);
            }
            EllipseLine(c, r * Mathf.Abs(Mathf.Cos(t * 1.4f)), r, lw, col.WithAlpha(0.55f), 12);
            Arc(c, r, lw * 1.2f, col, 16);
            // the plus; a rare sideways glitch
            float sh = OrbHash((float)System.Math.Floor(t * 6f) + o.Id) > 0.9f ? (OrbHash(o.Id + t) * 4f - 2f) : 0f;
            float u = r * 0.2f;
            FillRect(c.X - u * 0.6f + sh, c.Y - u * 2f, u * 1.2f, u * 4f, Rgba.White);
            FillRect(c.X - u * 2f + sh, c.Y - u * 0.6f, u * 4f, u * 1.2f, Rgba.White);
            if (!o.Tiny)
            {
                float rr = r * 1.6f;
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * MathX.Tau + t * 0.8f, l = i % 3 == 0 ? 0.18f : 0.1f;
                    Line(c + V2.FromAngle(a) * rr, c + V2.FromAngle(a) * rr * (1f + l), lw, col.WithAlpha(0.8f));
                }
            }
            if (o.Big)
                Arc(c, r + 5f, 2f, new Rgba(1f, 0.95f, 0.5f, 0.85f), 24);
        }

        /// A picked-up orb: the sphere pops into square data bits that fly out and flicker away, a hex ring spreads
        /// from the spot, and a small plus rises off the player.
        void DataBurst(V2 at, V2 player, float bodyR, bool big)
        {
            int n = big ? 16 : 9;
            for (int i = 0; i < n; i++)
            {
                float a = Rnd(0f, MathX.Tau), sp = Rnd(50f, big ? 190f : 140f);
                AddPart(new Part { Kind = PBit, Pos = at, Vel = V2.FromAngle(a) * sp, Life = Rnd(0.35f, 0.6f), Max = 0.6f,
                    Size = Rnd(1.6f, big ? 4f : 3f), Col = i % 4 == 0 ? Rgba.White : OrbGreen });
            }
            Ring(at, (big ? 40f : 26f), 0.35f, OrbGreen, PHexRing);
            if (big)
                Ring(at, 58f, 0.45f, new Rgba(1f, 0.95f, 0.5f), PHexRing);
            AddPart(new Part { Kind = PPlusUp, Pos = player + new V2(0f, -bodyR), Vel = new V2(0f, -60f), Life = 0.6f, Max = 0.6f, Size = big ? 5f : 3.5f, Col = Rgba.White });
        }

        void DrawOrbPart(Part p, float f)
        {
            switch (p.Kind)
            {
                case PBit:  // a square that flickers off in its last half
                {
                    if (f < 0.5f && MathX.Fmod(f * 30f, 1f) < 0.5f)
                        return;
                    float s = p.Size;
                    FillRect(p.Pos.X - s / 2f, p.Pos.Y - s / 2f, s, s, p.Col);
                    break;
                }
                case PHexRing:
                {
                    float r = p.Size * (1f - f * f);
                    V2 prev = p.Pos + new V2(r, 0f);
                    for (int i = 1; i <= 6; i++)
                    {
                        V2 next = p.Pos + V2.FromAngle(MathX.Tau * i / 6f) * r;
                        Line(prev, next, 1.5f + 2f * f, p.Col.WithAlpha(f));
                        prev = next;
                    }
                    break;
                }
                case PPlusUp:
                {
                    float u = p.Size, w = u * 0.3f;
                    var c = p.Col.WithAlpha(Mathf.Min(1f, f * 1.6f));
                    FillRect(p.Pos.X - w, p.Pos.Y - u, w * 2f, u * 2f, c);
                    FillRect(p.Pos.X - u, p.Pos.Y - w, u * 2f, w * 2f, c);
                    break;
                }
            }
        }
    }
}

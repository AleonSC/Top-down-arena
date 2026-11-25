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

        /// A health orb, drawn like a Medkit's orb (HealerKitLooks.cs): a soft glow, a solid green disc with a white
        /// rim and a white cross, pulsing and bobbing over a ground shadow, in a green neon outline. Big orbs add a gold ring; tiny ones are paler.
        void DrawPlainOrb(Orb o, float t)
        {
            if (o.Blood)
            {
                DrawBloodOrb(o, t);
                return;
            }
            var col = o.Tiny ? new Rgba(0.6f, 1f, 0.75f) : OrbGreen;
            float r = o.Radius * (1f + 0.12f * Mathf.Sin(t * 6f + o.Id));
            float bob = 2.5f * Mathf.Sin(t * 3f + o.Id);
            V2 at = o.Disp + new V2(0f, bob);
            PickupShadow(o.Disp, o.Radius, 2.5f - bob);  // CoinLook.cs
            Circle(at, r * 2.4f, col.WithAlpha(0.16f), 20);
            NeonRing(at, r + 2.5f, o.Tiny ? col : OrbNeon);
            Circle(at, r, col, 16);
            Arc(at, r + 1.5f, 1.5f, Rgba.White.WithAlpha(0.8f), 16);
            float arm = r * 0.6f, w = o.Tiny ? 1.5f : 2.5f;
            Line(at - new V2(arm, 0f), at + new V2(arm, 0f), w, Rgba.White);
            Line(at - new V2(0f, arm), at + new V2(0f, arm), w, Rgba.White);
            if (o.Big)
                Arc(at, r + 5f, 2f, new Rgba(1f, 0.95f, 0.5f, 0.85f), 24);
        }

        static readonly Rgba BloodOrbCol = new Rgba(0.82f, 0.08f, 0.16f), BloodOrbNeon = new Rgba(1f, 0.3f, 0.35f);

        /// A Hemomancer's blood orb (anyone can drink it): a dark crimson disc in a red neon outline, slowly beating like a
        /// heart, with a white blood drop on it and two droplets circling it.
        void DrawBloodOrb(Orb o, float t)
        {
            float beat = Mathf.Max(0f, Mathf.Sin(t * 5f + o.Id));
            float r = o.Radius * (1f + 0.14f * beat * beat * beat);
            float bob = 2.5f * Mathf.Sin(t * 3f + o.Id);
            V2 at = o.Disp + new V2(0f, bob);
            PickupShadow(o.Disp, o.Radius, 2.5f - bob);  // CoinLook.cs
            Circle(at, r * 2.6f, BloodOrbCol.WithAlpha(0.18f), 20);
            NeonRing(at, r + 2.5f, BloodOrbNeon);
            Circle(at, r, BloodOrbCol, 16);
            Arc(at, r + 1.5f, 1.5f, new Rgba(1f, 0.75f, 0.75f, 0.8f), 16);
            // the drop: a round bottom and a point on top
            float d = r * 0.42f;
            V2 c = at + new V2(0f, d * 0.35f);
            Circle(c, d, Rgba.White, 12);
            Tri3(c + new V2(-d * 0.93f, -d * 0.35f), c + new V2(d * 0.93f, -d * 0.35f), c + new V2(0f, -d * 2.3f), Rgba.White);  // NonShooterLooks.cs
            for (int i = 0; i < 2; i++)
            {
                float a = t * 2.2f + o.Id + i * Mathf.PI;
                Circle(at + new V2(Mathf.Cos(a) * (r + 7f), Mathf.Sin(a) * (r + 7f) * 0.6f), 1.6f, BloodOrbNeon, 8);
            }
        }

        /// A picked-up orb: green sparks fly out, a ring spreads from the spot, and a small plus rises off the player.
        void DataBurst(V2 at, V2 player, float bodyR, bool big, bool blood = false)
        {
            if (blood)
            {
                Sparks(at, BloodOrbNeon, 12, 170f, 0.5f, 2.6f);
                Ring(at, 34f, 0.35f, BloodOrbCol);
                AddPart(new Part { Kind = PPlusUp, Pos = player + new V2(0f, -bodyR), Vel = new V2(0f, -60f), Life = 0.6f, Max = 0.6f, Size = 4.5f, Col = Rgba.White });
                return;
            }
            Sparks(at, OrbGreen, big ? 14 : 8, big ? 190f : 140f, 0.5f, big ? 3f : 2.2f);
            Sparks(at, Rgba.White, big ? 4 : 2, big ? 160f : 120f, 0.4f, 2f);
            Ring(at, big ? 40f : 26f, 0.35f, OrbGreen);
            if (big)
                Ring(at, 58f, 0.45f, new Rgba(1f, 0.95f, 0.5f));
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

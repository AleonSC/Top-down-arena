using UnityEngine;

namespace Arena
{
    /// MOCKUP: three proposed reworks of the Twin Reapers' look, picked by ReaperConcept (0 = today's look):
    /// 1 Hooded Reapers (tattered cloaks, hoods, scythes in hand), 2 Spectral Shrouds (ghosts with bone masks, the
    /// scythe circling them, a twisted soul tether), 3 Reaper's Clock (dials whose hands are the scythes; the Dual
    /// Reaper's hands are its lasers). Preview only (Tools/Preview REAPERLOOK=...); nothing sets it in the game.
    public partial class ArenaApp
    {
        internal static int ReaperConcept;

        static readonly Rgba ConceptInk = new Rgba(0.07f, 0.05f, 0.1f), ConceptBone = new Rgba(0.93f, 0.9f, 0.84f);

        void DrawReaperConceptBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            bool dual = e.Type == EnemyType.DualReaper, dusk = e.Type == EnemyType.ReaperOfDusk;
            var R = game.Reapers;
            float facing = dual ? (R != null && R.Turn == ReaperTurn.Lasers ? R.Laser : R != null ? R.Heading : 0f) : e.Facing;
            Ellipse(pos + new V2(0f, r * 0.55f), r * 1.05f, r * 0.4f, new Rgba(0f, 0f, 0f, 0.35f));  // shadow
            var side = dusk ? DuskCol : DawnCol;
            if (e.Flash > 0f)
                side = side.Mix(Rgba.White, 0.5f);
            switch (ReaperConcept)
            {
                case 1: if (dual) HoodedDual(pos, r, facing, t); else Hooded(pos, r, facing, side, dusk, t); break;
                case 2: if (dual) ShroudDual(pos, r, facing, t); else Shroud(pos, r, facing, side, dusk, t); break;
                default: if (dual) ClockDual(pos, r, facing, t); else Clock(pos, r, facing, side, dusk, t); break;
            }
        }

        // ---- shared bits ----

        /// A ragged fan: radius per angle from rad(a), filled in one colour, rimmed in another.
        void RaggedFan(V2 pos, System.Func<float, int, float> rad, int n, Rgba fill, Rgba rim, float rimW)
        {
            V2 prev = pos + V2.FromAngle(0f) * rad(0f, 0);
            for (int k = 1; k <= n; k++)
            {
                float a = MathX.Tau * k / n;
                V2 pt = pos + V2.FromAngle(a) * rad(a, k % n);
                Tri3(pos, prev, pt, fill);
                Line(prev, pt, rimW, rim);
                prev = pt;
            }
        }

        /// Dusk's crescent moon or Dawn's sun, small.
        void Sigil(V2 c, float s, bool dusk, Rgba col, float t)
        {
            if (dusk)
                Arc(c, s, s * 0.45f, col, 14, MathX.Pi * 0.35f, MathX.Pi * 1.3f);
            else
            {
                Circle(c, s * 0.5f, col, 12);
                for (int k = 0; k < 8; k++)
                {
                    V2 d = V2.FromAngle(t * 0.6f + MathX.Tau * k / 8f);
                    Line(c + d * s * 0.7f, c + d * s * 1.15f, 2f, col);
                }
            }
        }

        void GlowEyes(V2 c, V2 f, V2 sd, float s, Rgba col)
        {
            for (int k = -1; k <= 1; k += 2)
            {
                V2 at = c + f * s * 0.15f + sd * s * 0.38f * k;
                Circle(at, s * 0.26f, col.WithAlpha(0.35f), 8);
                Circle(at, s * 0.14f, col, 8);
                Circle(at, s * 0.06f, Rgba.White, 6);
            }
        }

        // ---- 1: Hooded Reapers ----

        void Hooded(V2 pos, float r, float facing, Rgba side, bool dusk, float t)
        {
            V2.Axes(facing, out V2 f, out V2 sd);
            // The cloak: short in front, long and torn behind, swaying.
            RaggedFan(pos, (a, k) =>
            {
                float back = (1f - Mathf.Cos(a - facing)) * 0.5f;
                float torn = (k & 1) == 0 ? 1f : 0.82f;
                return r * (0.8f + 0.55f * back * torn) + 3f * back * Mathf.Sin(t * 5f + k * 1.3f);
            }, 22, side.Darkened(0.72f), side.WithAlpha(0.9f), 2.5f);
            Sigil(pos - f * r * 0.5f, r * 0.24f, dusk, side.WithAlpha(0.8f), t);
            // The scythe, held out on its own side (Dusk's right, Dawn's left).
            float hand = dusk ? 1f : -1f;
            V2 grip = pos + sd * r * 0.75f * hand + f * r * 0.1f;
            Circle(grip, r * 0.16f, ConceptBone.Darkened(0.2f), 8);  // the bony hand
            DrawScytheShape(grip, facing - 0.35f * hand, r * 1.5f, side.Mix(Rgba.White, 0.25f), 0.7f);
            // The hood: dark, rimmed, a black void with two eyes.
            V2 hc = pos + f * r * 0.22f;
            Tri3(hc + sd * r * 0.5f, hc - sd * r * 0.5f, hc - f * r * 0.95f, ConceptInk);  // the hood's point, hanging back
            Line(hc + sd * r * 0.5f, hc - f * r * 0.95f, 2f, side.WithAlpha(0.8f));
            Line(hc - sd * r * 0.5f, hc - f * r * 0.95f, 2f, side.WithAlpha(0.8f));
            Circle(hc, r * 0.52f, ConceptInk, 16);
            Arc(hc, r * 0.52f, 2.5f, side, 16, facing - 2.2f, 4.4f);
            Circle(hc + f * r * 0.1f, r * 0.34f, new Rgba(0f, 0f, 0f, 1f), 14);
            GlowEyes(hc + f * r * 0.12f, f, sd, r * 0.34f, side.Mix(Rgba.White, 0.3f));
        }

        void HoodedDual(V2 pos, float r, float facing, float t)
        {
            V2.Axes(facing, out V2 f, out V2 sd);
            Circle(pos, r + 22f, DualGold.WithAlpha(0.06f + 0.04f * Mathf.Sin(t * 4f)), 32);
            // One torn cloak: Dawn's half ahead, Dusk's behind, a gold seam between.
            RaggedFan(pos, (a, k) => r * ((k & 1) == 0 ? 1.12f : 0.94f) + 3f * Mathf.Sin(t * 4f + k), 32,
                ConceptInk, DualGold.WithAlpha(0.8f), 2.5f);
            for (int h = 0; h < 2; h++)
            {
                var c = h == 0 ? DawnCol : DuskCol;
                float from = facing + (h == 0 ? -MathX.Pi / 2f : MathX.Pi / 2f);
                SetColor(c.Darkened(0.6f));
                for (int i = 0; i < 10; i++)
                    Tri(pos, pos + V2.FromAngle(from + MathX.Pi * i / 10f) * r * 0.86f, pos + V2.FromAngle(from + MathX.Pi * (i + 1) / 10f) * r * 0.86f);
            }
            Line(pos - sd * r, pos + sd * r, 3f, new Rgba(1f, 0.94f, 0.78f, 0.9f));
            // Two arms, a scythe each, held like a pinwheel: Dawn's ahead on one side, Dusk's behind on the other.
            DrawScytheShape(pos + sd * r * 0.8f, facing + 0.45f, r * 1.05f, DawnCol.Mix(Rgba.White, 0.25f), 0.6f);
            DrawScytheShape(pos - sd * r * 0.8f, facing + MathX.Pi + 0.45f, r * 1.05f, DuskCol.Mix(Rgba.White, 0.25f), 0.6f);
            // Two hoods back to back: Dawn looks along the lasers, Dusk the other way.
            for (int h = 0; h < 2; h++)
            {
                var c = h == 0 ? DawnCol : DuskCol;
                V2 dir = h == 0 ? f : -f, hc = pos + dir * r * 0.4f;
                Circle(hc, r * 0.36f, ConceptInk, 16);
                Arc(hc, r * 0.36f, 2.5f, c, 16, facing + (h == 0 ? 0f : MathX.Pi) - 2.2f, 4.4f);
                Circle(hc + dir * r * 0.06f, r * 0.24f, new Rgba(0f, 0f, 0f, 1f), 14);
                GlowEyes(hc + dir * r * 0.08f, dir, h == 0 ? sd : -sd, r * 0.24f, c.Mix(Rgba.White, 0.3f));
            }
        }

        // ---- 2: Spectral Shrouds ----

        void Shroud(V2 pos, float r, float facing, Rgba side, bool dusk, float t)
        {
            V2.Axes(facing, out V2 f, out V2 sd);
            for (int k = 3; k >= 1; k--)  // a wisp trailing behind
                Circle(pos - f * (r * 0.45f * k) + sd * 5f * Mathf.Sin(t * 4f + k), r * (0.75f - 0.15f * k), side.WithAlpha(0.12f + 0.05f * (3 - k)), 14);
            RaggedFan(pos, (a, k) => r * (1f + 0.09f * Mathf.Sin(a * 7f + t * 6f)), 28, side.Darkened(0.6f).WithAlpha(0.92f), side.WithAlpha(0.7f), 2f);
            Circle(pos, r * 0.78f, side.Darkened(0.4f).WithAlpha(0.5f), 20);
            // The scythe circles it: Dusk one way, Dawn the other.
            float spin = t * 2.2f * (dusk ? 1f : -1f);
            V2 orb = pos + V2.FromAngle(spin) * (r + 16f);
            Arc(pos, r + 16f, 1.5f, side.WithAlpha(0.25f), 32);
            DrawScytheShape(orb, spin + (dusk ? MathX.Pi / 2f : -MathX.Pi / 2f), r * 1.05f, side.Mix(Rgba.White, 0.3f), 1f);
            // The bone mask, its sigil on the brow, eye slits.
            V2 mc = pos + f * r * 0.12f;
            Polygon(mc, r * 0.5f, 6, facing, ConceptBone);
            Polygon(mc - f * r * 0.12f, r * 0.34f, 6, facing, ConceptBone.Darkened(0.12f));
            for (int k = -1; k <= 1; k += 2)
            {
                V2 eye = mc + f * r * 0.06f + sd * r * 0.2f * k;
                Tri3(eye - sd * r * 0.1f * k + f * r * 0.04f, eye + sd * r * 0.1f * k + f * r * 0.06f, eye - f * r * 0.06f, new Rgba(0.05f, 0.02f, 0.06f));
                Circle(eye, r * 0.045f, side, 6);
            }
            Sigil(mc - f * r * 0.24f, r * 0.11f, dusk, side, t);
        }

        void ShroudDual(V2 pos, float r, float facing, float t)
        {
            V2.Axes(facing, out V2 f, out V2 sd);
            Circle(pos, r + 26f, DualGold.WithAlpha(0.06f + 0.04f * Mathf.Sin(t * 4f)), 32);
            for (int h = 0; h < 2; h++)
            {
                var c = h == 0 ? DawnCol : DuskCol;
                float from = facing + (h == 0 ? -MathX.Pi / 2f : MathX.Pi / 2f);
                V2 prev = pos + V2.FromAngle(from) * r;
                for (int i = 1; i <= 16; i++)
                {
                    float a = from + MathX.Pi * i / 16f;
                    V2 pt = pos + V2.FromAngle(a) * r * (1f + 0.08f * Mathf.Sin(a * 7f + t * 6f));
                    Tri3(pos, prev, pt, c.Darkened(0.6f).WithAlpha(0.94f));
                    Line(prev, pt, 2f, c.WithAlpha(0.8f));
                    prev = pt;
                }
            }
            // Two scythes circling opposite each other, a faint orbit.
            Arc(pos, r + 18f, 1.5f, DualGold.WithAlpha(0.3f), 40);
            for (int k = 0; k < 2; k++)
            {
                float a = t * 1.6f + MathX.Pi * k;
                DrawScytheShape(pos + V2.FromAngle(a) * (r + 18f), a + MathX.Pi / 2f, r * 0.85f, (k == 0 ? DuskCol : DawnCol).Mix(Rgba.White, 0.3f), 1f);
            }
            // A two-faced mask: Dawn's half ahead, Dusk's behind, split by a gold crack.
            for (int h = 0; h < 2; h++)
            {
                V2 dir = h == 0 ? f : -f;
                var c = h == 0 ? DawnCol : DuskCol;
                V2 mc = pos + dir * r * 0.3f;
                Polygon(mc, r * 0.36f, 6, facing, ConceptBone.Mix(c, 0.15f));
                for (int k = -1; k <= 1; k += 2)
                {
                    V2 eye = mc + dir * r * 0.06f + sd * r * 0.14f * k;
                    Circle(eye, r * 0.07f, new Rgba(0.05f, 0.02f, 0.06f), 8);
                    Circle(eye, r * 0.035f, c, 6);
                }
            }
            Line(pos - sd * r * 0.5f, pos + sd * r * 0.5f, 3f, DualGold);
        }

        // ---- 3: Reaper's Clock ----

        void Dial(V2 pos, float r, Rgba rim, Rgba face, float t)
        {
            Circle(pos, r, face, 32);
            Arc(pos, r, 4f, rim, 32);
            Arc(pos, r * 0.78f, 1.5f, rim.WithAlpha(0.35f), 32);
            for (int k = 0; k < 12; k++)
            {
                V2 d = V2.FromAngle(MathX.Tau * k / 12f - MathX.Pi / 2f);
                Line(pos + d * r * (k % 3 == 0 ? 0.72f : 0.82f), pos + d * r * 0.94f, k % 3 == 0 ? 3f : 1.5f, rim.WithAlpha(0.85f));
            }
        }

        void Clock(V2 pos, float r, float facing, Rgba side, bool dusk, float t)
        {
            Dial(pos, r, side, ConceptInk.Mix(side, 0.12f), t);
            Sigil(pos + new V2(0f, -r * 0.5f), r * 0.16f, dusk, side.WithAlpha(0.7f), t);
            // The seconds hand: Dusk's runs backwards, Dawn's forwards.
            float sec = t * 1.4f * (dusk ? -1f : 1f);
            Line(pos, pos + V2.FromAngle(sec) * r * 0.85f, 1.5f, side.Mix(Rgba.White, 0.5f));
            // The hour hand is the scythe: it points where the Reaper is facing (where it will reap).
            DrawScytheShape(pos + V2.FromAngle(facing) * r * 0.35f, facing, r * 1.25f, side.Mix(Rgba.White, 0.3f), 0.6f);
            Circle(pos, r * 0.13f, DualGold, 10);
            Circle(pos, r * 0.06f, Rgba.White, 8);
        }

        void ClockDual(V2 pos, float r, float facing, float t)
        {
            Circle(pos, r + 24f, DualGold.WithAlpha(0.06f + 0.04f * Mathf.Sin(t * 4f)), 32);
            Circle(pos, r, ConceptInk, 40);
            for (int h = 0; h < 2; h++)  // Dawn's half of the face on one side, Dusk's on the other
            {
                var c = h == 0 ? DawnCol : DuskCol;
                float from = facing + (h == 0 ? -MathX.Pi / 2f : MathX.Pi / 2f);
                SetColor(c.Darkened(0.78f));
                for (int i = 0; i < 12; i++)
                    Tri(pos, pos + V2.FromAngle(from + MathX.Pi * i / 12f) * r, pos + V2.FromAngle(from + MathX.Pi * (i + 1) / 12f) * r);
                Arc(pos, r - 3f, 4f, c, 20, from, MathX.Pi);
            }
            Arc(pos, r + 5f, 2f, DualGold, 48);
            for (int k = 0; k < 12; k++)
            {
                V2 d = V2.FromAngle(facing + MathX.Tau * k / 12f);
                Line(pos + d * r * (k % 3 == 0 ? 0.7f : 0.82f), pos + d * r * 0.93f, k % 3 == 0 ? 4f : 2f, DualGold.WithAlpha(0.9f));
            }
            // Its two hands are scythes, and they are the lasers: Dawn's along the beam, Dusk's opposite.
            V2 f = V2.FromAngle(facing);
            DrawScytheShape(pos + f * r * 0.4f, facing, r * 1.05f, DawnCol.Mix(Rgba.White, 0.3f), 0.6f);
            DrawScytheShape(pos - f * r * 0.4f, facing + MathX.Pi, r * 1.05f, DuskCol.Mix(Rgba.White, 0.3f), 0.6f);
            Circle(pos, r * 0.14f, DualGold, 12);
            Circle(pos, r * 0.07f, Rgba.White, 8);
        }

        // ---- the tether ----

        void DrawConceptTether(V2 from, V2 to, bool lit, bool warm, float thick, float t)
        {
            float len = from.DistanceTo(to);
            V2 n = (to - from).Normalized().Orthogonal();
            switch (ReaperConcept)
            {
                case 1:  // a chain of soul links
                {
                    int links = Mathf.Max(4, (int)(len / 22f));
                    for (int k = 0; k < links; k++)
                    {
                        float u = (k + 0.5f) / links;
                        V2 c = V2.Lerp(from, to, u) + (lit ? V2.Zero : n * 10f * Mathf.Sin(u * MathX.Pi));  // slack when dark
                        var col = DuskCol.Mix(DawnCol, u);
                        if (!lit)
                            col = warm ? new Rgba(1f, 1f, 1f, ((int)(t * 12f) & 1) == 0 ? 0.6f : 0.3f) : col.WithAlpha(0.3f);
                        V2 d = (to - from).Normalized();
                        V2 axis = (k & 1) == 0 ? d : n;
                        float w = (k & 1) == 0 ? 11f : 6f;
                        if (lit)
                            Line(c - axis * w, c + axis * w, 10f * thick, col.WithAlpha(0.3f));
                        Line(c - axis * w, c + axis * w, (lit ? 4f : 2f) * thick, col);
                    }
                    if (lit)
                        Line(from, to, 2f, new Rgba(1f, 0.95f, 1f, 0.8f));
                    break;
                }
                case 2:  // two strands twisting round each other
                {
                    int seg = Mathf.Max(8, (int)(len / 10f));
                    for (int s = 0; s < 2; s++)
                    {
                        V2 prev = from;
                        for (int k = 1; k <= seg; k++)
                        {
                            float u = k / (float)seg, w = Mathf.Sin(u * MathX.Pi) * (lit ? 9f * thick : 6f);
                            V2 pt = V2.Lerp(from, to, u) + n * w * Mathf.Sin(u * len / 18f + t * 8f + s * MathX.Pi);
                            var col = (s == 0 ? DuskCol : DawnCol);
                            if (!lit)
                                col = warm ? new Rgba(1f, 1f, 1f, ((int)(t * 12f) & 1) == 0 ? 0.6f : 0.3f) : col.WithAlpha(0.28f);
                            if (lit)
                                Line(prev, pt, 9f * thick, col.WithAlpha(0.25f));
                            Line(prev, pt, lit ? 3.5f * thick : 2f, col);
                            prev = pt;
                        }
                    }
                    break;
                }
                default:  // a ruled beam: tick marks, and pulses running in from both ends
                {
                    if (!lit)
                    {
                        var c = warm ? new Rgba(1f, 1f, 1f, ((int)(t * 12f) & 1) == 0 ? 0.6f : 0.3f) : new Rgba(0.7f, 0.67f, 0.86f, 0.25f);
                        Line(from, to, 1.5f, c);
                        for (float d = 0f; d < len; d += 24f)
                        {
                            V2 p = V2.Lerp(from, to, d / len);
                            Line(p - n * 5f, p + n * 5f, 1.5f, c);
                        }
                        break;
                    }
                    Line(from, to, (Game.TetherWidth + 14f) * thick, new Rgba(1f, 0.9f, 1f, 0.12f));
                    for (int k = 0; k < 8; k++)
                        Line(V2.Lerp(from, to, k / 8f), V2.Lerp(from, to, (k + 1) / 8f), Game.TetherWidth * thick, DuskCol.Mix(DawnCol, (k + 0.5f) / 8f).WithAlpha(0.85f));
                    for (float d = 12f; d < len; d += 24f)
                    {
                        V2 p = V2.Lerp(from, to, d / len);
                        float big = ((int)(d / 24f) % 5 == 0) ? 1.6f : 1f;
                        Line(p - n * Game.TetherWidth * 0.5f * big * thick, p + n * Game.TetherWidth * 0.5f * big * thick, 2f, DualGold.WithAlpha(0.9f));
                    }
                    for (int k = 0; k < 3; k++)
                    {
                        float u = MathX.Fmod(t * 0.7f + k / 3f, 1f) * 0.5f;
                        Circle(V2.Lerp(from, to, u), 6f * thick, Rgba.White, 10);
                        Circle(V2.Lerp(to, from, u), 6f * thick, Rgba.White, 10);
                    }
                    break;
                }
            }
        }
    }
}

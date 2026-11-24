using UnityEngine;

namespace Arena
{
    /// The Twin Reapers' bodies, the Hooded Reapers look (the rework page's concept A): a torn cloak swaying long
    /// behind, a pointed hood with a black void and two glowing eyes, a pointed scythe in hand (Dusk's right,
    /// Dawn's left) and Dusk's moon or Dawn's sun on the back. The Dual Reaper is one torn cloak, Dawn's half ahead
    /// (along its lasers) and Dusk's behind, with a hood each way and the two scythes held like a pinwheel.
    /// Colours from ReaperLook.cs; the tether, lasers, pillars and holes are there too.
    public partial class ArenaApp
    {
        static readonly Rgba ReaperInk = new Rgba(0.07f, 0.05f, 0.1f), ReaperBone = new Rgba(0.93f, 0.9f, 0.84f);

        /// Dusk or Dawn. Faces its reap while it has one (marked, dashing, swinging), else where it's looking.
        void DrawTwinBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            bool dusk = e.Type == EnemyType.ReaperOfDusk;
            var R = game.Reapers;
            var rp = R == null ? null : dusk ? R.ReapA : R.ReapB;
            bool reaping = rp != null && R.Stage == ReaperStage.Sundered && rp.Phase > 0;
            float facing = reaping ? rp.A : e.Facing, hand = dusk ? 1f : -1f;
            V2.Axes(facing, out V2 f, out V2 sd);
            var eye = e.Phase == 1 ? new Rgba(1f, 0.3f, 0.22f) : e.Phase == 2 ? new Rgba(0.92f, 0.94f, 1f) : col.Mix(Rgba.White, 0.3f);  // enraged red, Soul Bond pale
            Ellipse(pos + new V2(0f, r * 0.55f), r * 1.05f, r * 0.4f, new Rgba(0f, 0f, 0f, 0.35f));  // shadow
            // The cloak: short in front, long and torn behind, swaying (faster while it dashes).
            float sway = reaping && rp.Phase == 2 ? 14f : 5f;
            RaggedCloak(pos, r, facing, t, sway, col.Darkened(0.72f), col.WithAlpha(0.9f));
            DrawReaperSigil(pos - f * r * 0.5f, r * 0.24f, dusk, col.WithAlpha(0.8f), t);
            // The scythe: held across the front at rest, drawn back when its reap is marked, leading the dash, then swung.
            float aim = facing - 0.35f * hand;
            if (reaping)
                aim = rp.Phase == 1 ? facing + 1.3f * hand : rp.Phase == 2 ? facing - 0.2f * hand : facing + hand * (1.3f - 2.6f * Mathf.Clamp01(rp.T / 0.25f));
            V2 grip = pos + sd * r * 0.75f * hand + f * r * 0.1f;
            Circle(grip, r * 0.16f, ReaperBone.Darkened(0.2f), 8);  // the bony hand
            DrawReaperScythe(grip, aim, r * 2.3f, -hand, col.Mix(Rgba.White, 0.3f), reaping && rp.Phase == 1 ? 0.5f + 0.5f * Mathf.Sin(t * 14f) : 0f);
            DrawReaperHood(pos + f * r * 0.22f, r * 0.52f, facing, col, eye);
        }

        /// The fused pair: one torn cloak in two halves, two hoods back to back, two scythes.
        void DrawDualBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var R = game.Reapers;
            float facing = R != null && R.Turn == ReaperTurn.Lasers ? R.Laser : R != null && R.Turn == ReaperTurn.Chase ? R.Heading : t * 0.4f;
            V2.Axes(facing, out V2 f, out V2 sd);
            var tint = e.Flash > 0f || JuiceFlash(e) ? HitFlashMix(e) : 0f;
            Rgba dawn = DawnCol.Mix(Rgba.White, tint), dusk = DuskCol.Mix(Rgba.White, tint);
            Ellipse(pos + new V2(0f, r * 0.55f), r * 1.1f, r * 0.42f, new Rgba(0f, 0f, 0f, 0.35f));
            Circle(pos, r + 22f, DualGold.WithAlpha(0.06f + 0.04f * Mathf.Sin(t * 4f)), 32);
            RaggedFan(pos, (a, k) => r * ((k & 1) == 0 ? 1.12f : 0.94f) + 3f * Mathf.Sin(t * 4f + k), 32, ReaperInk, DualGold.WithAlpha(0.8f), 2.5f);
            for (int h = 0; h < 2; h++)  // Dawn's half ahead, Dusk's behind
            {
                SetColor((h == 0 ? dawn : dusk).Darkened(0.6f));
                float from = facing + (h == 0 ? -MathX.Pi / 2f : MathX.Pi / 2f);
                for (int i = 0; i < 10; i++)
                    Tri(pos, pos + V2.FromAngle(from + MathX.Pi * i / 10f) * r * 0.86f, pos + V2.FromAngle(from + MathX.Pi * (i + 1) / 10f) * r * 0.86f);
            }
            Line(pos - sd * r, pos + sd * r, 3f, new Rgba(1f, 0.94f, 0.78f, 0.9f));  // the gold seam
            // Two arms, a scythe each, held like a pinwheel: Dawn's forward on one side, Dusk's back on the other.
            float warn = R != null && R.Turn == ReaperTurn.Chase && !R.Trapped && R.Leap <= 0f && R.SwingT <= Game.SwingWarn ? 1f : 0f;
            for (int k = 0; k < 2; k++)
            {
                V2 grip = pos + sd * r * 1.02f * (k == 0 ? 1f : -1f);
                Circle(grip, r * 0.1f, ReaperBone.Darkened(0.2f), 8);
                DrawReaperScythe(grip, facing + (k == 0 ? 0f : MathX.Pi) + 0.45f, r * 1.9f, 1f, (k == 0 ? dawn : dusk).Mix(Rgba.White, 0.3f), warn);
            }
            for (int h = 0; h < 2; h++)  // Dawn's hood looks along the lasers, Dusk's the other way
            {
                var c = h == 0 ? dawn : dusk;
                float a = facing + (h == 0 ? 0f : MathX.Pi);
                DrawReaperHood(pos + V2.FromAngle(a) * r * 0.4f, r * 0.36f, a, c, R != null && R.Trapped && R.ShieldFrac <= 0f ? new Rgba(1f, 0.3f, 0.22f) : c.Mix(Rgba.White, 0.3f));
            }
        }

        /// A hood seen from above: its point hanging back, rimmed in the Reaper's colour, a black void and two eyes.
        void DrawReaperHood(V2 hc, float s, float facing, Rgba rim, Rgba eye)
        {
            V2.Axes(facing, out V2 f, out V2 sd);
            V2 tip = hc - f * s * 1.8f;
            Tri3(hc + sd * s, hc - sd * s, tip, ReaperInk);
            Line(hc + sd * s, tip, 2f, rim.WithAlpha(0.8f));
            Line(hc - sd * s, tip, 2f, rim.WithAlpha(0.8f));
            Circle(hc, s, ReaperInk, 16);
            Arc(hc, s, 2.5f, rim, 16, facing - 2.2f, 4.4f);
            V2 v = hc + f * s * 0.2f;
            Circle(v, s * 0.66f, new Rgba(0f, 0f, 0f, 1f), 14);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 at = v + f * s * 0.1f + sd * s * 0.25f * k;
                Circle(at, s * 0.17f, eye.WithAlpha(0.35f), 8);
                Circle(at, s * 0.09f, eye, 8);
                Circle(at, s * 0.04f, Rgba.White, 6);
            }
        }

        /// A pointed scythe: a haft through the grip and a blade from its head that curls to one side and back,
        /// thick at the heel and tapering to a sharp tip, with a bright edge on the inside of the curve.
        /// glint (0-1) lights the edge (a marked reap, a swing about to come).
        void DrawReaperScythe(V2 grip, float aim, float len, float curl, Rgba col, float glint)
        {
            V2 dir = V2.FromAngle(aim), perp = V2.FromAngle(aim + curl * MathX.Pi / 2f);
            V2 foot = grip - dir * len * 0.35f, head = grip + dir * len * 0.65f;
            Line(foot, head, Mathf.Max(2.5f, len * 0.055f), new Rgba(0.32f, 0.26f, 0.24f));  // the haft
            Line(grip - dir * len * 0.06f, grip + dir * len * 0.06f, Mathf.Max(3.5f, len * 0.075f), ReaperBone.Darkened(0.35f));  // its wrap
            float rb = len * 0.46f, w = len * 0.15f;
            V2 c = head - dir * rb;  // the blade follows a circle round this, from the head out to the side and back
            Tri3(head + dir * w * 0.25f, head - perp * w * 0.45f, head - dir * w * 0.3f, col.Darkened(0.25f));  // the heel
            const int n = 12;
            V2 pb = head + dir * w * 0.35f, pe = head - dir * w * 0.65f;
            for (int i = 1; i <= n; i++)
            {
                float u = i / (float)n, wu = w * Mathf.Pow(1f - u, 0.85f);
                V2 d = V2.FromAngle(aim + curl * 1.55f * u);
                V2 nb = c + d * (rb + wu * 0.35f), ne = c + d * (rb - wu * 0.65f);
                Tri3(pb, pe, ne, col);
                Tri3(pb, ne, nb, col);
                Line(pe, ne, 1.5f, new Rgba(1f, 1f, 1f, 0.75f + 0.25f * glint));  // the edge
                pb = nb; pe = ne;
            }
            if (glint > 0f)
                Circle(pe, len * 0.06f, new Rgba(1f, 1f, 1f, 0.7f * glint), 8);  // a glint at the tip
        }

        /// The cloak's outline: short in front, long and torn behind.
        void RaggedCloak(V2 pos, float r, float facing, float t, float sway, Rgba fill, Rgba rim)
        {
            RaggedFan(pos, (a, k) =>
            {
                float back = (1f - Mathf.Cos(a - facing)) * 0.5f;
                float torn = (k & 1) == 0 ? 1f : 0.82f;
                return r * (0.8f + 0.55f * back * torn) + sway * back * Mathf.Sin(t * 5f + k * 1.3f);
            }, 22, fill, rim, 2.5f);
        }

        /// A ragged fan round pos: its radius per angle from rad(angle, index), filled, with a rim.
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

        /// Dusk's crescent moon or Dawn's sun.
        void DrawReaperSigil(V2 c, float s, bool dusk, Rgba col, float t)
        {
            if (dusk)
            {
                Arc(c, s, s * 0.45f, col, 14, MathX.Pi * 0.35f, MathX.Pi * 1.3f);
                return;
            }
            Circle(c, s * 0.5f, col, 12);
            for (int k = 0; k < 8; k++)
            {
                V2 d = V2.FromAngle(t * 0.6f + MathX.Tau * k / 8f);
                Line(c + d * s * 0.7f, c + d * s * 1.15f, 2f, col);
            }
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// The Rubber Chicken skin for the Twin Blades (Expertise.cs; picked in Profile > Look > Weapon skin). Each sabre becomes a rubber chicken held by its feet: a plump body,
    /// a long floppy neck that lags behind the swing and stretches with its speed, a head that screams (beak open) while it
    /// cuts, squashes on the hit, and sheds feathers instead of the pale blade trail. Drawing only: the motion and the hits
    /// are the Twin Blades' own (TwinBladesMotion.cs), so the chickens are where the hits say.
    public partial class ArenaApp
    {
        bool chickenOn;  // the Twin Blades being drawn now are chickens: each draw entry (TwinBladesLook.cs) sets it from its swinger's skin

        bool IsChicken(Player p) => p != null && game.SkinOf(p.Id, WeaponId.TwinBlades) == Expertise.RubberChicken;

        struct ChickState { public float Bend, Stretch, Squash; public bool Scream; }
        ChickState chickR, chickL, chickCur;

        static readonly Rgba ChickYellow = new Rgba(1f, 0.83f, 0.18f), ChickShade = new Rgba(0.88f, 0.6f, 0.08f), ChickHi = new Rgba(1f, 0.95f, 0.62f),
            ChickOrange = new Rgba(1f, 0.52f, 0.1f), ChickComb = new Rgba(0.92f, 0.16f, 0.16f), ChickInk = new Rgba(0.12f, 0.07f, 0.04f);

        /// How a hand's chicken moves at `c` seconds into a step: the neck lags the swing (bend) and stretches with its speed;
        /// it screams while that hand cuts and squashes just after.
        ChickState ChickAt(TwinBladesMotion.Step s, float c, bool right)
        {
            const float dt = 0.03f;
            var a = TwinBladesMotion.At(s, c);
            var b = TwinBladesMotion.At(s, Mathf.Max(0f, c - dt));
            float angA = (right ? a.Rb : a.Lb) + a.Sp, angB = (right ? b.Rb : b.Lb) + b.Sp;
            float vel = (angA - angB) / dt;  // rad/s, + clockwise on screen
            float rvel = ((right ? a.Rr : a.Lr) - (right ? b.Rr : b.Lr)) / dt;  // px/s out from the body (the jabs: a peck)
            float k = c / s.Dur, h0 = right ? s.HotR0 : s.HotL0, h1 = right ? s.HotR1 : s.HotL1;
            float after = (k - h1) * s.Dur;  // seconds since its cut ended
            return new ChickState
            {
                Bend = Mathf.Clamp(-vel * 0.045f, -1.3f, 1.3f),
                Stretch = 1f + Mathf.Min(0.4f, Mathf.Abs(vel) * 0.016f) + Mathf.Clamp(rvel * 0.0016f, 0f, 0.55f),
                Squash = after > 0f && after < 0.16f ? Mathf.Sin(after / 0.16f * Mathf.PI) : 0f,
                Scream = k >= h0 - 0.05f && k <= h1 + 0.1f,
            };
        }

        /// The idle chickens: they hang a little limp and sway with the breath.
        ChickState ChickIdle(float t, bool right) => new ChickState { Bend = 0.35f * Mathf.Sin(t * 2.6f + (right ? 0f : 1.3f)) + (right ? 0.25f : -0.25f), Stretch = 1f };

        /// One rubber chicken held at its feet in the glove, pointing along `ang`.
        void DrawChicken(V2 hand, float ang, float alpha, bool shadow, ChickState st)
        {
            V2.Axes(ang, out V2 dir, out V2 side);
            Rgba sh = new Rgba(0f, 0f, 0f, 0.26f * alpha);
            float sq = st.Squash, along = 1f - 0.22f * sq, across = 1f + 0.35f * sq;
            // the neck: a curve that bends by st.Bend over its length
            const int N = 7;
            float neckLen = 22f * st.Stretch * along;
            V2 bodyC = hand + dir * (15f * along);
            V2 neck0 = hand + dir * (24f * along);
            var pts = new V2[N + 1];
            pts[0] = neck0;
            float a = ang;
            for (int i = 1; i <= N; i++)
            {
                a += st.Bend / N * 1.6f;
                pts[i] = pts[i - 1] + V2.FromAngle(a) * (neckLen / N);
            }
            V2 head = pts[N], hdir = V2.FromAngle(a), hside = new V2(-hdir.Y, hdir.X);
            if (shadow)
            {
                Ellipse(bodyC, 11f * along, 8f * across, ang, sh, 14);
                for (int i = 1; i <= N; i++)
                    Line(pts[i - 1], pts[i], 5f, sh);
                Circle(head, 6f, sh, 12);
                return;
            }
            // legs and feet in the glove
            Line(hand + dir * 2f - side * 2.5f, hand + dir * 9f - side * 3f, 1.8f, ChickOrange.WithAlpha(alpha));
            Line(hand + dir * 2f + side * 2.5f, hand + dir * 9f + side * 3f, 1.8f, ChickOrange.WithAlpha(alpha));
            for (int f = -1; f <= 1; f += 2)
                for (int toe = -1; toe <= 1; toe++)
                    Line(hand + side * (2.5f * f), hand - dir * 4f + side * (2.5f * f + toe * 2f), 1.3f, ChickOrange.WithAlpha(alpha));
            // the body: outline, shade, fill, highlight, a wing nub
            Ellipse(bodyC, 12.2f * along, 9f * across, ang, ChickInk.WithAlpha(0.9f * alpha), 16);
            Ellipse(bodyC + side * 0.8f, 11f * along, 8f * across, ang, ChickShade.WithAlpha(alpha), 16);
            Ellipse(bodyC - side * 1f, 10f * along, 6.6f * across, ang, ChickYellow.WithAlpha(alpha), 16);
            Ellipse(bodyC - side * 3.4f - dir * 2f, 4.2f, 2f, ang, ChickHi.WithAlpha(0.8f * alpha), 10);
            Ellipse(bodyC + side * 4f + dir * 1f, 5f * along, 2.6f * across, ang + 0.3f, ChickShade.WithAlpha(alpha), 10);
            // the neck: outline, then yellow, thinner toward the head
            for (int i = 1; i <= N; i++)
                Line(pts[i - 1], pts[i], 6.2f - 1.2f * i / N, ChickInk.WithAlpha(0.9f * alpha));
            for (int i = 1; i <= N; i++)
                Line(pts[i - 1], pts[i], 4.4f - 1.1f * i / N, ChickYellow.WithAlpha(alpha));
            // the head: comb on top, beak (open while it screams), eye (an X on the squash)
            for (int c = 0; c < 3; c++)
                Circle(head - hside * 5f + hdir * (c - 1) * 2.6f, 2.3f - 0.3f * Mathf.Abs(c - 1), ChickComb.WithAlpha(alpha), 8);
            Circle(head, 6.3f, ChickInk.WithAlpha(0.9f * alpha), 14);
            Circle(head, 5.2f, ChickYellow.WithAlpha(alpha), 14);
            Circle(head + hside * 4.2f + hdir * 1f, 1.9f, ChickComb.WithAlpha(alpha), 8);  // the wattle
            V2 bk = head + hdir * 4.6f;
            if (st.Scream)
            {
                SetColor(ChickOrange.WithAlpha(alpha));
                Tri(bk - hside * 1.2f, bk + hdir * 6f - hside * 3.2f, bk - hside * 2.6f + hdir * 0.5f);
                Tri(bk + hside * 1.2f, bk + hdir * 6f + hside * 3.2f, bk + hside * 2.6f + hdir * 0.5f);
                Circle(bk + hdir * 1.5f, 1.4f, ChickComb.Mix(ChickInk, 0.5f).WithAlpha(alpha), 6);  // the open mouth
            }
            else
            {
                SetColor(ChickOrange.WithAlpha(alpha));
                Tri(bk - hside * 2f, bk + hdir * 5f, bk + hside * 2f);
            }
            V2 eye = head - hside * 1.6f + hdir * 1.2f;
            if (sq > 0.3f)
            {
                Line(eye - hdir * 1.3f - hside * 1.3f, eye + hdir * 1.3f + hside * 1.3f, 0.9f, ChickInk.WithAlpha(alpha));
                Line(eye - hdir * 1.3f + hside * 1.3f, eye + hdir * 1.3f - hside * 1.3f, 0.9f, ChickInk.WithAlpha(alpha));
            }
            else
                Circle(eye, 1.2f, ChickInk.WithAlpha(alpha), 6);
            if (st.Scream)  // three squeak ticks off the beak
                for (int k = -1; k <= 1; k++)
                {
                    V2 d = V2.FromAngle(a + k * 0.55f);
                    Line(bk + hdir * 7f + d * 2f, bk + hdir * 7f + d * 6f, 1.1f, new Rgba(1f, 1f, 1f, 0.75f * alpha));
                }
        }

        /// A feather: a small pale leaf with a quill line.
        void DrawFeather(V2 at, float rot, float size, float alpha)
        {
            if (alpha <= 0.02f)
                return;
            Ellipse(at, size, size * 0.38f, rot, new Rgba(1f, 0.97f, 0.82f, 0.9f * alpha), 8);
            Line(at - V2.FromAngle(rot) * size * 1.2f, at + V2.FromAngle(rot) * size * 0.8f, 0.7f, new Rgba(0.9f, 0.7f, 0.3f, alpha));
        }

        /// Feathers shed along the head's path while a hand cuts (in place of the blade trail), and a puff where its cut ends.
        void DrawChickenTrail(TwinBladesMotion.Step s, V2 o, float aim, float c, bool right)
        {
            float h0 = right ? s.HotR0 : s.HotL0, h1 = right ? s.HotR1 : s.HotL1;
            if (h0 > 1.5f)
                return;
            for (int i = 0; i < 10; i++)
            {
                float born = (h0 + (h1 - h0) * i / 9f) * s.Dur, age = c - born;
                if (age < 0f || age > 0.5f)
                    continue;
                var mi = TwinBladesMotion.At(s, born);
                V2 hand, dir;
                TwinHand(o, aim, mi, right, out hand, out dir);
                float seed = i * 1.7f + (right ? 0.4f : 2.1f);
                V2 at = hand + dir * 44f + V2.FromAngle(seed * 2.3f) * (age * 40f) + new V2(0f, age * 18f);
                DrawFeather(at, seed + age * 6f, 3.2f, 1f - age / 0.5f);
            }
            float since = c - h1 * s.Dur;
            if (since > 0f && since < 0.4f)
            {
                var mi = TwinBladesMotion.At(s, h1 * s.Dur);
                V2 hand, dir;
                TwinHand(o, aim, mi, right, out hand, out dir);
                V2 hit = hand + dir * 46f;
                float u = since / 0.4f;
                GlowDisc(hit, 14f * (1f - u), new Rgba(1f, 0.9f, 0.5f), 0.5f * (1f - u));
                for (int k = 0; k < 7; k++)
                {
                    float fa = k * 0.9f + (right ? 0.3f : 1.1f);
                    DrawFeather(hit + V2.FromAngle(fa) * (8f + 34f * u) + new V2(0f, 14f * u * u), fa + u * 4f, 3.6f, 1f - u);
                }
            }
        }
    }
}

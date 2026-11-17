using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// MOCKUP: Dragon's Dogma, a proposed Tank Ninja ultimate. A great spirit dragon bursts out of the Ninja and
    /// hunts across the arena for 5 s, devouring the enemies it reaches, then coils back into the Ninja.
    /// Drawing only, with no game logic yet: the preview scene (Tools/Preview/ShotDragonsDogma.cs, DOGMA=<s>) sets the
    /// Dogma* fields below and removes each enemy as it's eaten. Nothing in the game sets them, so this draws nothing.
    ///
    /// The look, by the clock (DogmaT seconds since the cast):
    ///   0-0.9 s  a sigil opens under the Ninja and the arena dims; the dragon pours out of them, head first.
    ///   then     the head follows a smooth path through the knots (DogmaKnots, timed by DogmaKnotT), the body a ribbon
    ///            680 px long laid along where the head has been: shadow on the floor (it flies), a soft azure body with
    ///            a bright core, belly scales, a gold spine with fins, two pairs of claws, a mane, antlers, whiskers,
    ///            and a tail of three flame fronds. Spirit motes peel off its sides.
    ///   bites    the jaws spread as it nears a victim (DogmaVictims, eaten at DogmaEatT): a shock ring and the enemy's
    ///            soul spiralling into the maw, then a glowing lump that travels down the body to the tail.
    ///   4.4-5 s  the head dives back into the Ninja and the body follows it in; the sigil closes.
    public partial class ArenaApp
    {
        public static float DogmaT = -1f;                    // seconds since the cast; < 0: nothing to draw
        public static V2[] DogmaKnots;                       // the head's path: [0] the Ninja, ..., [last] the Ninja again
        public static float[] DogmaKnotT;                    // when the head passes each knot
        public static V2[] DogmaVictims;                     // where each enemy is eaten
        public static float[] DogmaEatT;                     // when
        public static Rgba[] DogmaVictimCol;                 // its colour (the soul)

        public const float DogmaDur = 5f, DogmaLen = 680f;
        static readonly Rgba DgCore = new Rgba(0.82f, 0.97f, 1f), DgBody = new Rgba(0.32f, 0.66f, 1f),
            DgDeep = new Rgba(0.1f, 0.22f, 0.62f), DgGold = new Rgba(1f, 0.83f, 0.44f), DgMaw = new Rgba(0.55f, 1f, 0.95f);

        readonly List<V2> dgPts = new List<V2>(80);
        readonly List<float> dgS = new List<float>(80);

        /// The head's position at time t: a Catmull-Rom curve through the knots.
        static V2 DogmaPath(float t)
        {
            var k = DogmaKnots; var kt = DogmaKnotT;
            if (t <= kt[0]) return k[0];
            int n = k.Length;
            if (t >= kt[n - 1]) return k[n - 1];
            int i = 0;
            while (i < n - 2 && t > kt[i + 1]) i++;
            float u = (t - kt[i]) / Mathf.Max(0.001f, kt[i + 1] - kt[i]);
            V2 p0 = k[Mathf.Max(0, i - 1)], p1 = k[i], p2 = k[i + 1], p3 = k[Mathf.Min(n - 1, i + 2)];
            float u2 = u * u, u3 = u2 * u;
            return 0.5f * ((2f * p1) + (p2 - p0) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u2 + (3f * p1 - p0 - 3f * p2 + p3) * u3);
        }

        /// The body: points every ~9 px back along the head's path, 680 px long (bunched at the Ninja while it's
        /// still pouring out or already gone back in).
        void DogmaBody(float t)
        {
            dgPts.Clear(); dgS.Clear();
            V2 prev = DogmaPath(t);
            dgPts.Add(prev); dgS.Add(0f);
            float s = 0f, next = 9f, tt = t;
            for (int guard = 0; guard < 6000 && dgPts.Count < 76; guard++)
            {
                tt -= 1f / 400f;
                V2 p = DogmaPath(tt);
                s += p.DistanceTo(prev);
                prev = p;
                if (s >= next || tt < -1.5f)
                {
                    dgPts.Add(p); dgS.Add(s);
                    next += 9f;
                }
            }
            // A slither: each point swings sideways, a wave running down the body.
            for (int i = 1; i < dgPts.Count - 1; i++)
            {
                V2 n = (dgPts[i - 1] - dgPts[i + 1]).Normalized().Orthogonal();
                float u = i / (float)(dgPts.Count - 1);
                dgPts[i] = dgPts[i] + n * (Mathf.Sin(i * 0.42f - t * 9f) * 7f * Mathf.Clamp01(u * 4f));
            }
        }

        /// Half-width of the body at u (0 head, 1 tail), with the swallowed lumps.
        float DogmaWidth(float u, float t)
        {
            float w = 1.45f * (u < 0.08f ? Mathf.Lerp(13f, 17f, u / 0.08f) : u < 0.3f ? Mathf.Lerp(17f, 20f, (u - 0.08f) / 0.22f) : Mathf.Lerp(20f, 2.5f, Mathf.Pow((u - 0.3f) / 0.7f, 1.3f)));
            return w * (1f + 0.7f * DogmaLump(u, t));
        }

        /// How swollen the body is at u by an enemy going down (0..1).
        float DogmaLump(float u, float t)
        {
            float best = 0f;
            if (DogmaEatT == null) return 0f;
            foreach (float e in DogmaEatT)
            {
                float a = (t - e) / 0.9f;
                if (a < 0f || a > 1f) continue;
                float d = Mathf.Abs(u - a * 0.95f) / 0.07f;
                if (d < 1f) best = Mathf.Max(best, (1f - d * d) * (1f - a * 0.6f));
            }
            return best;
        }

        static float DgHash(int i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.55f; return x - Mathf.Floor(x); }

        /// How open the jaws are (0..1): wide just before each bite, snapping shut on it.
        static float DogmaJaw(float t)
        {
            float open = 0f;
            if (DogmaEatT == null) return 0f;
            foreach (float e in DogmaEatT)
            {
                float a = e - t;
                if (a > 0f && a < 0.35f) open = Mathf.Max(open, Mathf.Sin(Mathf.Clamp01(1f - a / 0.35f) * Mathf.PI * 0.5f));
                else if (a <= 0f && a > -0.08f) open = Mathf.Max(open, 1f + a / 0.08f);
            }
            return open;
        }

        /// WorldRenderer.cs, after the arena's contents: the whole dragon.
        void DrawDragonsDogma()
        {
            float t = DogmaT;
            if (t < 0f || DogmaKnots == null || t > DogmaDur + 0.4f) return;
            V2 home = DogmaKnots[0];
            float life = Mathf.Clamp01(t / 0.3f) * (1f - Mathf.Clamp01((t - DogmaDur) / 0.4f));

            // The dim, so the spirit reads as the brightest thing on screen.
            SetColor(new Rgba(0.01f, 0.02f, 0.06f, 0.42f * life));
            Quad(home + new V2(-4000f, -4000f), home + new V2(4000f, -4000f), home + new V2(4000f, 4000f), home + new V2(-4000f, 4000f));

            DrawDogmaSigil(home, t);
            DogmaBody(t);
            int n = dgPts.Count;
            if (n < 3) return;
            float headIn = 1f - Mathf.Clamp01((t - (DogmaKnotT[DogmaKnotT.Length - 1] - 0.1f)) / 0.25f);  // the head sinking into the Ninja

            // Its shadow on the floor, down and right: it's flying.
            V2 sh = new V2(22f, 34f);
            for (int i = n - 2; i >= 0; i--)
            {
                float u0 = i / (float)(n - 1), u1 = (i + 1) / (float)(n - 1);
                V2 a = dgPts[i] + sh, b = dgPts[i + 1] + sh;
                V2 na = Nrm(i), nb = Nrm(i + 1);
                float wa = DogmaWidth(u0, t), wb = DogmaWidth(u1, t);
                Rgba c = new Rgba(0f, 0.01f, 0.05f, 0.32f * life);
                VTri(a + na * wa, c.WithAlpha(0f), b + nb * wb, c.WithAlpha(0f), b, c);
                VTri(a + na * wa, c.WithAlpha(0f), b, c, a, c);
                VTri(a - na * wa, c.WithAlpha(0f), a, c, b, c);
                VTri(a - na * wa, c.WithAlpha(0f), b, c, b - nb * wb, c.WithAlpha(0f));
            }

            DrawDogmaTail(t, life);

            // Claws: two pairs, under the body.
            DrawDogmaLegs(0.2f, t, life);
            DrawDogmaLegs(0.5f, t, life);

            // The body, tail to head: a soft azure band, a brighter core, scales, the gold spine.
            for (int i = n - 2; i >= 0; i--)
            {
                float u0 = i / (float)(n - 1), u1 = (i + 1) / (float)(n - 1);
                V2 a = dgPts[i], b = dgPts[i + 1];
                V2 na = Nrm(i), nb = Nrm(i + 1);
                float wa = DogmaWidth(u0, t), wb = DogmaWidth(u1, t);
                float fa = life * (1f - 0.35f * u0), fb = life * (1f - 0.35f * u1);
                float lumpA = DogmaLump(u0, t), lumpB = DogmaLump(u1, t);
                Rgba ea = DgDeep.WithAlpha(0.5f * fa), eb = DgDeep.WithAlpha(0.5f * fb);
                Rgba ma = DgBody.Mix(DgCore, 0.6f * lumpA).WithAlpha(0.9f * fa), mb = DgBody.Mix(DgCore, 0.6f * lumpB).WithAlpha(0.9f * fb);
                VTri(a + na * wa, ea, b + nb * wb, eb, b, mb);
                VTri(a + na * wa, ea, b, mb, a, ma);
                VTri(a - na * wa, ea, a, ma, b, mb);
                VTri(a - na * wa, ea, b, mb, b - nb * wb, eb);
                // core
                float ca = wa * 0.38f, cb = wb * 0.38f;
                Rgba ka = DgCore.WithAlpha(0.55f * fa), kb = DgCore.WithAlpha(0.55f * fb);
                VTri(a + na * ca, ka.WithAlpha(0f), b + nb * cb, kb.WithAlpha(0f), b, kb);
                VTri(a + na * ca, ka.WithAlpha(0f), b, kb, a, ka);
                VTri(a - na * ca, ka.WithAlpha(0f), a, ka, b, kb);
                VTri(a - na * ca, ka.WithAlpha(0f), b, kb, b - nb * cb, kb.WithAlpha(0f));
                // scales: a chevron every other point, pointing at the head
                if (i % 2 == 0 && u0 > 0.05f && u0 < 0.92f)
                {
                    V2 fwd = (dgPts[Mathf.Max(0, i - 1)] - b).Normalized();
                    V2 tip = a + fwd * 4f;
                    Rgba sc = DgCore.WithAlpha(0.4f * fa);
                    Line(a + na * (wa * 0.82f) - fwd * 5f, tip, 1.6f, sc);
                    Line(a - na * (wa * 0.82f) - fwd * 5f, tip, 1.6f, sc);
                }
                // the spine, gold, with a fin every fourth point
                Line(a, b, 2f, DgGold.WithAlpha(0.85f * fa));
                if (i % 4 == 2 && u0 > 0.06f && u0 < 0.85f)
                {
                    V2 back = (b - a).Normalized();
                    float fin = 9f * (1f - u0 * 0.6f);
                    SetColor(DgGold.WithAlpha(0.9f * fa));
                    Tri(a + na * 3.5f, a - na * 3.5f, a + back * fin * 1.6f + na * Mathf.Sin(t * 12f + i) * 2f);
                }
            }

            DrawDogmaHead(t, life * headIn);
            DrawDogmaMotes(t, life);
            DrawDogmaBites(t);
        }

        V2 Nrm(int i)
        {
            int n = dgPts.Count;
            V2 d = dgPts[Mathf.Max(0, i - 1)] - dgPts[Mathf.Min(n - 1, i + 1)];
            if (d.LengthSq < 0.01f) return new V2(0f, 1f);
            return d.Normalized().Orthogonal();
        }

        /// The summoning sigil under the Ninja: a ring of runes that opens fast and closes as the dragon returns.
        void DrawDogmaSigil(V2 c, float t)
        {
            float open = Mathf.Clamp01(t / 0.35f), close = 1f - Mathf.Clamp01((t - 0.9f) / 0.6f);
            float back = Mathf.Clamp01((t - (DogmaDur - 0.7f)) / 0.3f) * (1f - Mathf.Clamp01((t - DogmaDur) / 0.35f));
            float a = Mathf.Max(close, back) * open;
            if (a <= 0f) return;
            float r = 70f * (1f - Mathf.Pow(1f - open, 3f));
            Arc(c, r, 2.5f, DgGold.WithAlpha(0.85f * a), 48);
            Arc(c, r * 0.78f, 1.5f, DgBody.WithAlpha(0.7f * a), 40);
            for (int k = 0; k < 12; k++)  // runes: tick pairs turning slowly
            {
                float ang = t * 0.8f + k * MathX.Tau / 12f;
                V2 d = V2.FromAngle(ang), o = d.Orthogonal();
                V2 p = c + d * (r * 0.89f);
                Line(p - d * 4f - o * 3f, p + d * 4f + o * 3f, 1.6f, DgGold.WithAlpha(0.8f * a));
                if (k % 2 == 0) Line(p - o * 4f, p + o * 4f, 1.2f, DgCore.WithAlpha(0.6f * a));
            }
            for (int k = 0; k < 3; k++)  // a triskele of swirls inside
            {
                float ang = -t * 2f + k * MathX.Tau / 3f;
                Arc(c, r * 0.5f, 2f, DgCore.WithAlpha(0.55f * a), 14, ang, 1.4f);
            }
        }

        /// A pair of small four-toed claws at u along the body, paddling.
        void DrawDogmaLegs(float u, float t, float life)
        {
            int n = dgPts.Count, i = Mathf.Clamp(Mathf.RoundToInt(u * (n - 1)), 1, n - 2);
            V2 p = dgPts[i], nn = Nrm(i), fwd = (dgPts[i - 1] - dgPts[i + 1]).Normalized();
            float w = DogmaWidth(u, t);
            for (int side = -1; side <= 1; side += 2)
            {
                float paddle = Mathf.Sin(t * 10f + side * 1.6f + u * 9f);
                V2 knee = p + nn * (side * (w + 14f)) + fwd * (6f + paddle * 8f);
                V2 foot = knee + nn * (side * 12f) + fwd * (14f + paddle * 5f);
                Line(p + nn * (side * w * 0.6f), knee, 5f, DgBody.WithAlpha(0.85f * life));
                Line(knee, foot, 3.5f, DgBody.WithAlpha(0.85f * life));
                for (int c = -1; c <= 1; c++)
                {
                    V2 cd = (fwd + nn * (side * 0.5f * c)).Normalized();
                    Line(foot, foot + cd * 9f, 2f, DgGold.WithAlpha(0.95f * life));
                }
            }
        }

        /// The tail: three flame fronds fanning out and flickering.
        void DrawDogmaTail(float t, float life)
        {
            int n = dgPts.Count;
            V2 end = dgPts[n - 1], back = (dgPts[n - 1] - dgPts[n - 4]).Normalized();
            if (back.LengthSq < 0.5f) return;
            for (int k = -1; k <= 1; k++)
            {
                float flick = Mathf.Sin(t * 14f + k * 2.1f);
                V2 dir = back.Rotated(k * 0.45f + flick * 0.12f);
                float len = (k == 0 ? 62f : 44f) * (1f + 0.15f * flick);
                V2 o = dir.Orthogonal();
                V2 mid = end + dir * (len * 0.5f) + o * (flick * 5f);
                Rgba c = (k == 0 ? DgCore : DgBody).WithAlpha(0.75f * life);
                VTri(end + o * 6f, c, mid, c.WithAlpha(0.5f * life), end - o * 6f, c);
                VTri(mid + o * 4f, c.WithAlpha(0.5f * life), end + dir * len, c.WithAlpha(0f), mid - o * 4f, c.WithAlpha(0.5f * life));
            }
        }

        /// The head: a wedge of a skull with a mane, swept-back gold antlers, long whiskers, white-gold eyes, and
        /// jaws that split sideways to show a glowing maw.
        void DrawDogmaHead(float t, float a)
        {
            if (a <= 0.01f) return;
            V2 h = dgPts[0];
            V2 f = (dgPts[0] - dgPts[3]);
            if (f.LengthSq < 0.5f) return;
            f = f.Normalized();
            V2 s = f.Orthogonal();
            float jaw = DogmaJaw(t);
            const float HS = 1.55f;
            System.Func<float, float, V2> H = (x, y) => h + f * (x * HS) + s * (y * HS);
            Rgba bc = DgBody.WithAlpha(0.95f * a), cc = DgCore.WithAlpha(0.95f * a), dc = DgDeep.Mix(DgBody, 0.5f).WithAlpha(0.95f * a);

            // mane: curved, tapering strands streaming back off the crown and cheeks
            for (int k = 0; k < 8; k++)
            {
                float side = k % 2 == 0 ? 1f : -1f, j = k / 2;
                V2 prev = H(-8f - j * 3f, side * (6f + j * 3f));
                for (int q = 1; q <= 6; q++)
                {
                    float u = q / 6f;
                    float wave = Mathf.Sin(t * 10f + k * 1.3f + u * 3f) * 7f * u;
                    V2 p = H(-8f - j * 3f - u * (30f + j * 6f), side * (6f + j * 3f + u * (10f + j * 4f) + wave));
                    SoftLine(prev, p, 10f * (1f - u * 0.6f), DgBody.Mix(DgCore, 0.4f).WithAlpha(0.5f * a * (1f - u * 0.6f)));
                    Line(prev, p, 1.8f * (1f - u * 0.7f), DgCore.WithAlpha(0.7f * a * (1f - u)));
                    prev = p;
                }
            }

            // antlers: swept back, branching
            for (int side = -1; side <= 1; side += 2)
            {
                V2 r0 = H(-2f, side * 7f), r1 = H(-14f, side * 14f), r2 = H(-30f, side * 12f);
                Line(r0, r1, 3.6f, DgGold.WithAlpha(a)); Line(r1, r2, 2.8f, DgGold.WithAlpha(a));
                Line(r1, H(-14f, side * 24f), 2f, DgGold.WithAlpha(a));
                Line(H(-22f, side * 13.5f), H(-27f, side * 21f), 1.8f, DgGold.WithAlpha(a));
            }

            // whiskers: long, rippling back past the head
            for (int side = -1; side <= 1; side += 2)
            {
                V2 prev = H(30f, side * 6f);
                for (int k = 1; k <= 12; k++)
                {
                    float u = k / 12f;
                    V2 p = H(30f + u * 4f - u * u * 70f, side * (8f + u * 26f + Mathf.Sin(t * 8f + u * 5f) * 8f * u));
                    Line(prev, p, 1.8f * (1f - u * 0.7f), DgCore.WithAlpha(0.85f * a * (1f - u * 0.6f)));
                    prev = p;
                }
            }

            // the skull's back half: crown and swept cheek spikes
            for (int side = -1; side <= 1; side += 2)
            {
                SetColor(DgGold.WithAlpha(0.9f * a));
                Tri(H(-6f, side * 12f), H(2f, side * 14f), H(-16f, side * 22f));  // cheek spike
                VTri(H(-12f, 0f), dc, H(-10f, side * 9f), bc, H(-2f, side * 14f), bc);
                VTri(H(-12f, 0f), dc, H(-2f, side * 14f), bc, H(6f, side * 12f), bc);
                VTri(H(-12f, 0f), dc, H(6f, side * 12f), bc, H(6f, 0f), cc);
            }

            // the maw, between the jaws
            if (jaw > 0.02f)
            {
                SetColor(DgDeep.WithAlpha(0.9f * a * jaw));
                Tri(H(4f, 0f), H(42f, 15f * jaw), H(42f, -15f * jaw));
                SetColor(DgMaw.WithAlpha(0.85f * a * jaw));
                Tri(H(8f, 0f), H(36f, 8f * jaw), H(36f, -8f * jaw));
            }

            // the jaws: each half of the snout swings out from its hinge
            for (int side = -1; side <= 1; side += 2)
            {
                float open = side * jaw * 0.38f;
                V2 pivot = new V2(4f, side * 8f);
                System.Func<float, float, V2> J = (x, y) => { V2 l = (new V2(x, y) - pivot).Rotated(open) + pivot; return H(l.X, l.Y); };
                VTri(J(4f, 0f), cc, J(4f, side * 12f), bc, J(20f, side * 9f), bc);
                VTri(J(4f, 0f), cc, J(20f, side * 9f), bc, J(34f, side * 5.5f), bc);
                VTri(J(4f, 0f), cc, J(34f, side * 5.5f), bc, J(42f, side * 1.5f), cc);
                VTri(J(4f, 0f), cc, J(42f, side * 1.5f), cc, J(42f, 0f), cc);
                if (jaw > 0.1f)  // fangs along the inner edge
                    for (int k = 0; k < 4; k++)
                    {
                        float x = 14f + k * 7f;
                        SetColor(Rgba.White.WithAlpha(a * Mathf.Clamp01(jaw * 2f)));
                        Tri(J(x, 0f), J(x + 3.5f, 0f), J(x + 1.5f, -side * (k == 3 ? 6f : 3.5f)));
                    }
                Line(J(24f, side * 6f), J(38f, side * 3f), 1.4f, DgGold.WithAlpha(0.8f * a));  // the ridge to the nostril
                SetColor(DgGold.WithAlpha(a));
                Tri(J(38f, side * 3.5f), J(42f, side * 2f), J(36f, side * 7f));  // nostril curl
            }
            // brows and eyes: slanted white slits under a gold ridge
            for (int side = -1; side <= 1; side += 2)
            {
                Line(H(-4f, side * 12f), H(12f, side * 7f), 2.6f, DgGold.WithAlpha(a));
                V2 e = H(4f, side * 9f);
                Line(e - f * 5f + s * (side * 2f), e + f * 5f - s * (side * 1.5f), 3.4f, Rgba.White.WithAlpha(a));
                Circle(e + f * 1f, 1.4f, DgGold.WithAlpha(a), 6);
            }
        }


        /// Spirit motes: little sparks shed from the body that drift out and fade.
        void DrawDogmaMotes(float t, float life)
        {
            int n = dgPts.Count;
            for (int i = 2; i < n; i += 2)
            {
                float r = DgHash(i), ph = (t * 1.4f + r) % 1f;
                V2 p = dgPts[i] + Nrm(i) * ((r < 0.5f ? -1f : 1f) * (14f + ph * 26f)) + new V2(0f, -ph * 14f);
                float sz = 2.2f * (1f - ph);
                SetColor((r < 0.3f ? DgGold : DgCore).WithAlpha(0.8f * (1f - ph) * life));
                Quad(p + new V2(0f, -sz * 1.6f), p + new V2(sz, 0f), p + new V2(0f, sz * 1.6f), p + new V2(-sz, 0f));
            }
        }

        /// Each bite: the shock ring, shards, and the enemy's soul spiralling into the maw.
        void DrawDogmaBites(float t)
        {
            if (DogmaEatT == null) return;
            for (int v = 0; v < DogmaEatT.Length; v++)
            {
                float a = t - DogmaEatT[v];
                if (a < 0f || a > 0.7f) continue;
                V2 at = DogmaVictims[v];
                float u = a / 0.7f, f = 1f - u;
                Arc(at, 18f + u * 70f, 3f * f + 0.5f, DgCore.WithAlpha(0.9f * f), 32);
                Arc(at, 10f + u * 46f, 1.5f, DgGold.WithAlpha(0.7f * f), 28);
                for (int k = 0; k < 8; k++)  // shards flung out
                {
                    float ang = k * MathX.Tau / 8f + v;
                    V2 d = V2.FromAngle(ang);
                    V2 p = at + d * (14f + u * 60f);
                    Line(p, p + d * (10f * f), 2f, DgCore.WithAlpha(f));
                }
                // the soul: a ghost of the enemy pulled along a spiral into the head
                V2 head = dgPts[0];
                for (int k = 0; k < 6; k++)
                {
                    float q = Mathf.Clamp01(u * 1.6f - k * 0.06f);
                    if (q >= 1f) continue;
                    V2 p = V2.Lerp(at, head, q * q) + V2.FromAngle(q * 9f + k) * (18f * (1f - q));
                    Circle(p, (9f - k) * (1f - q * 0.7f), DogmaVictimCol[v].Mix(DgCore, 0.5f).WithAlpha(0.55f * (1f - q)), 10);
                }
            }
        }

        /// Juice.DrawGlowPass: the dragon's light.
        void DrawDragonsDogmaGlow()
        {
            float t = DogmaT;
            if (t < 0f || DogmaKnots == null || t > DogmaDur + 0.4f || dgPts.Count < 3) return;
            float life = Mathf.Clamp01(t / 0.3f) * (1f - Mathf.Clamp01((t - DogmaDur) / 0.4f));
            int n = dgPts.Count;
            for (int i = 0; i < n; i += 3)
            {
                float u = i / (float)(n - 1);
                GlowDisc(dgPts[i], DogmaWidth(u, t) * 2.8f, DgBody, 0.24f * life * (1f - u * 0.5f));
                float lump = DogmaLump(u, t);
                if (lump > 0.05f) GlowDisc(dgPts[i], 44f, DgCore, 0.3f * lump * life);
            }
            V2 f = (dgPts[0] - dgPts[3]).Normalized(), s = f.Orthogonal();
            GlowDisc(dgPts[0], 70f, DgBody, 0.3f * life);
            for (int side = -1; side <= 1; side += 2)
                GlowDisc(dgPts[0] + f * 6f + s * (side * 14f), 14f, DgGold, 0.7f * life);
            float jaw = DogmaJaw(t);
            if (jaw > 0f) GlowDisc(dgPts[0] + f * 45f, 44f, DgMaw, 0.5f * jaw * life);
            V2 home = DogmaKnots[0];
            float sig = Mathf.Clamp01(t / 0.35f) * Mathf.Max(1f - Mathf.Clamp01((t - 0.9f) / 0.6f), Mathf.Clamp01((t - (DogmaDur - 0.7f)) / 0.3f) * (1f - Mathf.Clamp01((t - DogmaDur) / 0.35f)));
            if (sig > 0f) GlowDisc(home, 110f, DgGold, 0.35f * sig);
            if (DogmaEatT != null)
                for (int v = 0; v < DogmaEatT.Length; v++)
                {
                    float a = t - DogmaEatT[v];
                    if (a >= 0f && a < 0.5f) GlowDisc(DogmaVictims[v], 100f * (0.5f + a), DgCore, 0.6f * (1f - a / 0.5f));
                }
        }
    }
}

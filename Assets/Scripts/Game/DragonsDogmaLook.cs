using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Dragon's Dogma, the Tank Ninja's second ultimate (Core/DragonsDogma.cs): a great spirit dragon that bursts out of the
    /// Ninja, hunts across the arena devouring enemies, and coils back in. Drawn over the arena's contents (WorldRenderer.cs)
    /// and in the glow pass (Juice.cs). Preview: DOGMA=<s> (Tools/Preview/ShotDragonsDogma.cs).
    ///
    /// The body is laid along Player.DogmaTrail, the points the head has really passed (UpdateDogmaView keeps it on every
    /// game), DogmaLen (680 px) long:
    ///   the cast   a sigil opens under the Ninja and the arena dims; the dragon pours out of them, head first (the trail is
    ///              still short, so only the head and neck show).
    ///   the hunt   shadow on the floor (it flies), a soft azure body with a bright core, belly scales, a gold spine with
    ///              fins, two pairs of paddling claws, a mane, antlers, whiskers, a tail of three flame fronds; spirit motes
    ///              peel off its sides. The jaws spread as the head closes on its quarry (DogmaTgt).
    ///   a bite     (FxKind.DragonBite) the jaws snap, a shock ring and shards, the enemy's soul spirals into the maw, and a
    ///              glowing lump runs down the body to the tail. A big one only bitten gets the ring and shards.
    ///   the end    the head dives into the Ninja and the body drains in after it (DogmaSink); the sigil flashes shut.
    /// With Visual effects off: no dim, motes, sigil or lumps.
    public partial class ArenaApp
    {
        static readonly Rgba DgCore = new Rgba(0.82f, 0.97f, 1f), DgBody = Game.DogmaBlue,
            DgDeep = new Rgba(0.1f, 0.22f, 0.62f), DgGold = Game.DogmaGold, DgMaw = new Rgba(0.55f, 1f, 0.95f);
        const float DgStep = 9f;  // px between the body's points

        readonly List<V2> dgPts = new List<V2>(80);
        readonly List<float> dgU = new List<float>(80);    // each point's place along the body: 0 the head, 1 the tail tip
        readonly List<float> dgLumps = new List<float>(4); // this dragon's bites still going down (their age / DogmaBiteFx)
        bool dgHead;                                       // the head is out (not gone back into the Ninja)

        /// The body of p's dragon: points every DgStep px back along its trail from the head (or, while it drains back in,
        /// from DogmaSink px behind it), with a slither. False when there's nothing to draw.
        bool DogmaBody(Player p, float t)
        {
            dgPts.Clear(); dgU.Clear(); dgLumps.Clear();
            var tr = p.DogmaTrail;
            if (tr.Count == 0)
                return false;
            dgHead = p.DogmaOn;
            V2 prev = p.DogmaOn ? p.DogmaDisp : tr[tr.Count - 1];
            float s = 0f, skip = p.DogmaOn ? 0f : p.DogmaSink, next = skip;
            int i = tr.Count - 1;
            V2 cur = prev;
            if (skip <= 0f) { dgPts.Add(cur); dgU.Add(0f); next = DgStep; }
            while (i >= 0 && s < Game.DogmaLen)
            {
                V2 to = tr[i];
                float seg = cur.DistanceTo(to);
                if (s + seg >= next)
                {
                    float need = next - s;
                    cur = cur + (to - cur) * (need / Mathf.Max(0.001f, seg));
                    s = next;
                    if (s <= Game.DogmaLen)
                    {
                        dgPts.Add(cur); dgU.Add(s / Game.DogmaLen);
                    }
                    next += DgStep;
                }
                else
                {
                    s += seg;
                    cur = to;
                    i--;
                }
            }
            if (dgPts.Count < 3)
                return false;
            // A slither: each point swings sideways, a wave running down the body.
            for (int k = 1; k < dgPts.Count - 1; k++)
            {
                V2 n = (dgPts[k - 1] - dgPts[k + 1]).Normalized().Orthogonal();
                dgPts[k] = dgPts[k] + n * (Mathf.Sin(k * 0.42f - t * 9f) * 7f * Mathf.Clamp01(dgU[k] * 4f));
            }
            if (fxOn)
                foreach (var fx in FxOf(FxKind.DragonBite))
                    if (fx.Kind == FxKind.DragonBite && (int)fx.Pos2.X == p.Id && fx.Pos2.Y > 0.5f)
                        dgLumps.Add(fx.T / Game.DogmaBiteFx);
            return true;
        }

        /// Half-width of the body at u (0 head, 1 tail), with the swallowed lumps.
        float DogmaWidth(float u, float t)
        {
            float w = 1.45f * (u < 0.08f ? Mathf.Lerp(13f, 17f, u / 0.08f) : u < 0.3f ? Mathf.Lerp(17f, 20f, (u - 0.08f) / 0.22f) : Mathf.Lerp(20f, 2.5f, Mathf.Pow(Mathf.Clamp01((u - 0.3f) / 0.7f), 1.3f)));
            return w * (1f + 0.7f * DogmaLump(u));
        }

        /// How swollen the body is at u by an enemy going down (0..1).
        float DogmaLump(float u)
        {
            float best = 0f;
            foreach (float a in dgLumps)
            {
                if (a < 0f || a > 1f) continue;
                float d = Mathf.Abs(u - a * 0.95f) / 0.07f;
                if (d < 1f) best = Mathf.Max(best, (1f - d * d) * (1f - a * 0.6f));
            }
            return best;
        }

        static float DgHash(int i) { float x = Mathf.Sin(i * 127.1f + 311.7f) * 43758.55f; return x - Mathf.Floor(x); }

        /// How open p's dragon's jaws are (0..1): spreading as the head closes on its quarry, snapping shut on a bite.
        float DogmaJaw(Player p)
        {
            float open = 0f;
            Enemy e;
            if (p.DogmaTgt != 0 && game.Enemies.TryGetValue(p.DogmaTgt, out e))
            {
                float d = p.DogmaDisp.DistanceTo(e.Disp) - e.Radius;
                open = Mathf.Clamp01(1f - (d - 20f) / 120f);
                open = Mathf.Sin(open * Mathf.PI * 0.5f);
            }
            foreach (var fx in FxOf(FxKind.DragonBite))
                if (fx.Kind == FxKind.DragonBite && (int)fx.Pos2.X == p.Id && fx.T < 0.12f)
                    open = Mathf.Min(open, fx.T / 0.12f);  // just bit: shut, opening again
            return open;
        }

        /// How strongly the dragon shows (fading in as it comes out, out as its last of it drains in).
        static float DogmaLife(Player p) =>
            Mathf.Clamp01(p.DogmaSince / 0.2f) * (p.DogmaOn ? 1f : 1f - Mathf.Clamp01((p.DogmaSink - Game.DogmaLen * 0.6f) / (Game.DogmaLen * 0.4f + 40f)));

        /// WorldRenderer.cs, after the arena's contents: every dragon that's out.
        void DrawDragonsDogma()
        {
            float dim = 0f;
            foreach (var p in game.Players.Values)
                if (p.DogmaTrail.Count > 0)
                    dim = Mathf.Max(dim, DogmaLife(p) * (p.DogmaOn ? 1f : 1f - Mathf.Clamp01(p.DogmaSink / Game.DogmaLen)));
            if (dim > 0f && fxOn)  // the dim, so the spirit reads as the brightest thing on screen
            {
                V2 c = new V2(AW / 2f, AH / 2f);
                SetColor(new Rgba(0.01f, 0.02f, 0.06f, 0.3f * dim));
                Quad(c + new V2(-6000f, -6000f), c + new V2(6000f, -6000f), c + new V2(6000f, 6000f), c + new V2(-6000f, 6000f));
            }
            foreach (var p in game.Players.Values)
                if (p.DogmaTrail.Count > 0)
                    DrawDogma(p);
        }

        void DrawDogma(Player p)
        {
            float t = game.AnimT, life = DogmaLife(p);
            if (fxOn)
                DrawDogmaSigil(p);
            if (!DogmaBody(p, t))
            {
                if (fxOn) DrawDogmaBites(p);
                return;
            }
            int n = dgPts.Count;

            // Its shadow on the floor, down and right: it's flying.
            V2 sh = new V2(22f, 34f);
            for (int i = n - 2; i >= 0; i--)
            {
                V2 a = dgPts[i] + sh, b = dgPts[i + 1] + sh;
                V2 na = Nrm(i), nb = Nrm(i + 1);
                float wa = DogmaWidth(dgU[i], t), wb = DogmaWidth(dgU[i + 1], t);
                Rgba c = new Rgba(0f, 0.01f, 0.05f, 0.32f * life);
                VTri(a + na * wa, c.WithAlpha(0f), b + nb * wb, c.WithAlpha(0f), b, c);
                VTri(a + na * wa, c.WithAlpha(0f), b, c, a, c);
                VTri(a - na * wa, c.WithAlpha(0f), a, c, b, c);
                VTri(a - na * wa, c.WithAlpha(0f), b, c, b - nb * wb, c.WithAlpha(0f));
            }

            if (dgU[n - 1] > 0.9f)
                DrawDogmaTail(t, life);

            // Claws: two pairs, under the body.
            DrawDogmaLegs(0.2f, t, life);
            DrawDogmaLegs(0.5f, t, life);

            // The body, tail to head: a soft azure band, a brighter core, scales, the gold spine.
            for (int i = n - 2; i >= 0; i--)
            {
                float u0 = dgU[i], u1 = dgU[i + 1];
                V2 a = dgPts[i], b = dgPts[i + 1];
                V2 na = Nrm(i), nb = Nrm(i + 1);
                float wa = DogmaWidth(u0, t), wb = DogmaWidth(u1, t);
                float fa = life * (1f - 0.35f * u0), fb = life * (1f - 0.35f * u1);
                float lumpA = DogmaLump(u0), lumpB = DogmaLump(u1);
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
                int k = Mathf.RoundToInt(u0 * Game.DogmaLen / DgStep);  // (the patterns stay put on the body as it drains in)
                // scales: a chevron every other point, pointing at the head
                if (k % 2 == 0 && u0 > 0.05f && u0 < 0.92f && i > 0)
                {
                    V2 fwd = (dgPts[i - 1] - b).Normalized();
                    V2 tip = a + fwd * 4f;
                    Rgba sc = DgCore.WithAlpha(0.4f * fa);
                    Line(a + na * (wa * 0.82f) - fwd * 5f, tip, 1.6f, sc);
                    Line(a - na * (wa * 0.82f) - fwd * 5f, tip, 1.6f, sc);
                }
                // the spine, gold, with a fin every fourth point
                Line(a, b, 2f, DgGold.WithAlpha(0.85f * fa));
                if (k % 4 == 2 && u0 > 0.06f && u0 < 0.85f)
                {
                    V2 back = (b - a).Normalized();
                    float fin = 9f * (1f - u0 * 0.6f);
                    SetColor(DgGold.WithAlpha(0.9f * fa));
                    Tri(a + na * 3.5f, a - na * 3.5f, a + back * fin * 1.6f + na * Mathf.Sin(t * 12f + k) * 2f);
                }
            }

            if (dgHead)
                DrawDogmaHead(p, t, life * Mathf.Clamp01(p.DogmaSince / 0.12f));
            if (fxOn)
            {
                DrawDogmaMotes(t, life);
                DrawDogmaBites(p);
            }
        }

        V2 Nrm(int i)
        {
            int n = dgPts.Count;
            V2 d = dgPts[Mathf.Max(0, i - 1)] - dgPts[Mathf.Min(n - 1, i + 1)];
            if (d.LengthSq < 0.01f) return new V2(0f, 1f);
            return d.Normalized().Orthogonal();
        }

        /// How open the sigil under the Ninja is: opening as the dragon pours out, flashing again as it drains back in.
        static float DogmaSigilOpen(Player p)
        {
            float t = p.DogmaSince;
            float a = Mathf.Clamp01(t / 0.35f) * (1f - Mathf.Clamp01((t - 0.9f) / 0.6f));
            if (!p.DogmaOn)
                a = Mathf.Max(a, Mathf.Clamp01(p.DogmaSink / 120f) * (1f - Mathf.Clamp01((p.DogmaSink - Game.DogmaLen * 0.7f) / (Game.DogmaLen * 0.3f))));
            return a;
        }

        /// The summoning sigil under the Ninja: a ring of runes.
        void DrawDogmaSigil(Player p)
        {
            float a = DogmaSigilOpen(p);
            if (a <= 0f) return;
            V2 c = p.Disp;
            float t = game.AnimT;
            float r = 70f * (1f - Mathf.Pow(1f - Mathf.Clamp01(p.DogmaOn ? p.DogmaSince / 0.35f : 1f), 3f));
            Arc(c, r, 2.5f, DgGold.WithAlpha(0.85f * a), 48);
            Arc(c, r * 0.78f, 1.5f, DgBody.WithAlpha(0.7f * a), 40);
            for (int k = 0; k < 12; k++)  // runes: tick pairs turning slowly
            {
                float ang = t * 0.8f + k * MathX.Tau / 12f;
                V2.Axes(ang, out V2 d, out V2 o);
                V2 q = c + d * (r * 0.89f);
                Line(q - d * 4f - o * 3f, q + d * 4f + o * 3f, 1.6f, DgGold.WithAlpha(0.8f * a));
                if (k % 2 == 0) Line(q - o * 4f, q + o * 4f, 1.2f, DgCore.WithAlpha(0.6f * a));
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
            int n = dgPts.Count, i = 1;
            while (i < n - 2 && dgU[i] < u) i++;
            if (dgU[i] < u - 0.03f || dgU[i] > u + 0.03f) return;  // (that part isn't out, or has gone back in)
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
            V2 end = dgPts[n - 1], back = (dgPts[n - 1] - dgPts[Mathf.Max(0, n - 4)]).Normalized();
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
        void DrawDogmaHead(Player p, float t, float a)
        {
            if (a <= 0.01f) return;
            V2 h = dgPts[0];
            V2 f = (dgPts[0] - dgPts[Mathf.Min(3, dgPts.Count - 1)]);
            if (f.LengthSq < 0.5f) return;
            f = f.Normalized();
            V2 s = f.Orthogonal();
            float jaw = DogmaJaw(p);
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
                    V2 w = H(-8f - j * 3f - u * (30f + j * 6f), side * (6f + j * 3f + u * (10f + j * 4f) + wave));
                    SoftLine(prev, w, 10f * (1f - u * 0.6f), DgBody.Mix(DgCore, 0.4f).WithAlpha(0.5f * a * (1f - u * 0.6f)));
                    Line(prev, w, 1.8f * (1f - u * 0.7f), DgCore.WithAlpha(0.7f * a * (1f - u)));
                    prev = w;
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
                    V2 w = H(30f + u * 4f - u * u * 70f, side * (8f + u * 26f + Mathf.Sin(t * 8f + u * 5f) * 8f * u));
                    Line(prev, w, 1.8f * (1f - u * 0.7f), DgCore.WithAlpha(0.85f * a * (1f - u * 0.6f)));
                    prev = w;
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

        /// Each bite of p's dragon: the shock ring, shards, and (devoured) the enemy's soul spiralling into the maw.
        void DrawDogmaBites(Player p)
        {
            foreach (var fx in FxOf(FxKind.DragonBite))
            {
                if (fx.Kind != FxKind.DragonBite || (int)fx.Pos2.X != p.Id)
                    continue;
                float a = fx.T;
                if (a > 0.7f) continue;
                V2 at = fx.Pos;
                float u = a / 0.7f, f = 1f - u, s = Mathf.Clamp(fx.Radius / 18f, 0.7f, 2.5f);
                Arc(at, (18f + u * 70f) * s, 3f * f + 0.5f, DgCore.WithAlpha(0.9f * f), 32);
                Arc(at, (10f + u * 46f) * s, 1.5f, DgGold.WithAlpha(0.7f * f), 28);
                for (int k = 0; k < 8; k++)  // shards flung out
                {
                    float ang = k * MathX.Tau / 8f + at.X * 0.01f;
                    V2 d = V2.FromAngle(ang);
                    V2 q = at + d * (14f + u * 60f) * s;
                    Line(q, q + d * (10f * f), 2f, DgCore.WithAlpha(f));
                }
                if (fx.Pos2.Y < 0.5f)
                    continue;  // a big one, only bitten
                V2 head = p.DogmaOn ? p.DogmaDisp : p.Disp;  // the soul: a ghost of the enemy pulled along a spiral into the head
                for (int k = 0; k < 6; k++)
                {
                    float q = Mathf.Clamp01(u * 1.6f - k * 0.06f);
                    if (q >= 1f) continue;
                    V2 pos = V2.Lerp(at, head, q * q) + V2.FromAngle(q * 9f + k) * (18f * (1f - q));
                    Circle(pos, (9f - k) * (1f - q * 0.7f) * s, fx.Color.Mix(DgCore, 0.5f).WithAlpha(0.55f * (1f - q)), 10);
                }
            }
        }

        /// Juice.DrawGlowPass: every dragon's light.
        void DrawDragonsDogmaGlow()
        {
            foreach (var p in game.Players.Values)
                if (p.DogmaTrail.Count > 0)
                    DrawDogmaGlow(p);
        }

        void DrawDogmaGlow(Player p)
        {
            float t = game.AnimT, life = DogmaLife(p);
            float sig = DogmaSigilOpen(p);
            if (sig > 0f && fxOn) GlowDisc(p.Disp, 110f, DgGold, 0.35f * sig);
            foreach (var fx in FxOf(FxKind.DragonBite))
                if (fx.Kind == FxKind.DragonBite && (int)fx.Pos2.X == p.Id && fx.T < 0.5f)
                    GlowDisc(fx.Pos, 100f * (0.5f + fx.T), DgCore, 0.6f * (1f - fx.T / 0.5f));
            if (!DogmaBody(p, t))
                return;
            int n = dgPts.Count;
            for (int i = 0; i < n; i += 3)
            {
                float u = dgU[i];
                GlowDisc(dgPts[i], DogmaWidth(u, t) * 2.8f, DgBody, 0.24f * life * (1f - u * 0.5f));
                float lump = DogmaLump(u);
                if (lump > 0.05f) GlowDisc(dgPts[i], 44f, DgCore, 0.3f * lump * life);
            }
            if (!dgHead)
                return;
            V2 f = (dgPts[0] - dgPts[Mathf.Min(3, dgPts.Count - 1)]).Normalized(), s = f.Orthogonal();
            GlowDisc(dgPts[0], 70f, DgBody, 0.3f * life);
            for (int side = -1; side <= 1; side += 2)
                GlowDisc(dgPts[0] + f * 6f + s * (side * 14f), 14f, DgGold, 0.7f * life);
            float jaw = DogmaJaw(p);
            if (jaw > 0f) GlowDisc(dgPts[0] + f * 45f, 44f, DgMaw, 0.5f * jaw * life);
        }
    }
}

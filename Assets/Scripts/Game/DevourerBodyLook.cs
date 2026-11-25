using UnityEngine;

namespace Arena
{
    /// Devourer body rework mockups (three directions, picked with DevourerLook; 0 keeps the old 12-sided disc and its
    /// mouth, DevourerLook.cs). Each draws the whole body and its mouth on the side it faces, opens wide when the old
    /// mouth did (Hungry, Inhale, the Maw, Vomit, a lunge's dash), shows whoever it swallowed, and changes with its phase:
    /// Appetite raw pink, Gorging swollen with bile, Ravenous dark and split, the last stand burning from the throat.
    /// A: the Lamprey (a ridged worm head with a round disc of grinding tooth rings, gill pores, a swaying tail).
    /// B: the Glutton (a bloated gut with stretch marks and stitches, a wide hinged jaw, stubby claws; what it ate
    ///    shows through the belly, and in Ravenous a second mouth splits its back open).
    /// C: the Maw Bloom (six fleshy petals round a toothed throat, opening wider each phase, three tongues reaching out).
    public partial class ArenaApp
    {
        internal static int DevourerLook;  // 0 old, 1 A, 2 B, 3 C (preview: DEVOURLOOK=1..3)

        static readonly Rgba DvThroat = new Rgba(0.09f, 0.01f, 0.03f), DvGum = new Rgba(0.45f, 0.06f, 0.12f),
            DvLip = new Rgba(0.98f, 0.7f, 0.68f), DvVein = new Rgba(0.45f, 0.16f, 0.4f), DvScar = new Rgba(1f, 0.85f, 0.8f);

        /// Its skin in each phase.
        static Rgba DevourFlesh(int stage) => stage == 0 ? new Rgba(0.88f, 0.42f, 0.48f) : stage == 1 ? new Rgba(0.86f, 0.5f, 0.42f)
            : stage == 2 ? new Rgba(0.6f, 0.13f, 0.2f) : new Rgba(0.48f, 0.06f, 0.13f);

        void DrawDevourerBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            var s = e.Devour;
            float a = col.A, hit = (e.Flash > 0f || JuiceFlash(e)) ? HitFlashMix(e) : 0f;
            int stage = s == null ? 0 : (int)(s.TransT > 0f ? s.TransTo : s.Stage);
            bool wide = e.Phase == 1 || s != null && (s.InhaleT > 0f || s.Stage == DevourStage.Ravenous || s.Turn == DevourTurn.Vomit || s.Lunging && s.LungeWarn <= 0f);
            float open = s != null && s.SwallowId != 0 ? 0.25f : wide ? 1f : 0.3f + 0.06f * Mathf.Sin(t * 2.5f);
            Rgba? eaten = null;
            Player q;
            if (s != null && s.SwallowId != 0 && game.Players.TryGetValue(s.SwallowId, out q))
                eaten = q.Def.Color;
            V2.Axes(e.Facing, out V2 f, out V2 sd);
            Ellipse(pos + new V2(0f, r * 0.3f), r * 1.1f, r * 0.6f, 0f, new Rgba(0f, 0f, 0f, 0.3f * a), 24);  // shadow
            Rgba flesh = DevourFlesh(stage).Mix(Rgba.White, hit).WithAlpha(a);
            if (DevourerLook == 1)
                DrawDevourLamprey(pos, r, e.Facing, f, sd, stage, open, flesh, eaten, a, t);
            else if (DevourerLook == 2)
                DrawDevourGlutton(pos, r, e.Facing, f, sd, stage, open, flesh, eaten, a, t);
            else
                DrawDevourBloom(pos, r, e.Facing, f, sd, stage, open, flesh, eaten, a, t);
        }

        /// A ring of `n` teeth at radius `rr` round `c`, all pointing in, turned by `rot`.
        void ToothRing(V2 c, float rr, int n, float size, float rot, float alpha)
        {
            for (int k = 0; k < n; k++)
            {
                float ang = rot + MathX.Tau * k / n;
                Tooth(c + V2.FromAngle(ang) * rr, ang + MathX.Pi, size, alpha);
            }
        }

        /// The throat's light from the last stand on (and a hint of it in Ravenous).
        void DevourThroatGlow(V2 c, float rr, int stage, float a, float t)
        {
            if (stage < 2)
                return;
            float k = stage == 3 ? 0.75f + 0.25f * Mathf.Sin(t * 7f) : 0.45f;
            GlowDisc(c, rr * 1.6f, new Rgba(1f, 0.35f, 0.2f), k * a);
            Circle(c, rr * 0.45f, new Rgba(1f, 0.6f, 0.35f, 0.7f * k * a), 14);
        }

        /// Bile running from a spot (Gorging): a few green drops.
        void DevourDrool(V2 at, V2 dir, float r, float a, float t)
        {
            for (int k = 0; k < 3; k++)
            {
                float u = Fmod(t * 0.8f + k * 0.33f, 1f);
                Circle(at + dir * (r * 0.25f * u) + dir.Orthogonal() * (r * 0.12f * (k - 1)), r * (0.07f - 0.03f * u), BileGreen.WithAlpha(a * (1f - u)), 8);
            }
        }

        // ---- A: the Lamprey ------------------------------------------------------------------------------------

        void DrawDevourLamprey(V2 pos, float r, float face, V2 f, V2 sd, int stage, float open, Rgba flesh, Rgba? eaten, float a, float t)
        {
            float swell = stage == 1 ? 1.08f : 1f;
            // the tail, back to front: four ringed segments swaying behind it
            for (int i = 3; i >= 0; i--)
            {
                float sr = r * (0.8f - 0.15f * i) * swell, back = r * (0.72f + 0.42f * i);
                V2 c = pos - f * back + sd * (Mathf.Sin(t * 3f - i * 0.9f) * r * 0.08f * (i + 1));
                Rgba sc = flesh.Mix(new Rgba(0.2f, 0.03f, 0.07f), 0.15f + 0.12f * i);
                Ellipse(c, sr * 0.82f + 2.5f, sr + 2.5f, face, LarderInk.WithAlpha(a), 18);
                Ellipse(c, sr * 0.82f, sr, face, sc, 18);
                Line(c - sd * sr * 0.85f + f * sr * 0.1f, c + sd * sr * 0.85f + f * sr * 0.1f, 2.5f, sc.Mix(LarderInk, 0.4f));
                Circle(c + f * sr * 0.2f - sd * sr * 0.3f, sr * 0.22f, flesh.Mix(Rgba.White, 0.25f).WithAlpha(0.35f * a), 10);
            }
            // the head
            float hr = r * swell;
            Circle(pos, hr + 3f, LarderInk.WithAlpha(a), 32);
            Circle(pos, hr, flesh, 32);
            Circle(pos - f * hr * 0.2f - sd * hr * 0.25f, hr * 0.5f, flesh.Mix(Rgba.White, 0.18f).WithAlpha(0.5f * a), 20);
            for (int k = 1; k <= 3; k++)  // muscle bands across its back
            {
                float x = -0.15f - 0.22f * k, half = Mathf.Sqrt(1f - x * x) * hr * 0.92f;
                V2 c = pos + f * (x * hr);
                Line(c - sd * half, c + sd * half, 3f, flesh.Mix(LarderInk, 0.45f));
            }
            for (int side = -1; side <= 1; side += 2)  // gill pores down each side
                for (int k = 0; k < 6; k++)
                {
                    float ang = face + side * (1.25f + 0.22f * k);
                    Ellipse(pos + V2.FromAngle(ang) * hr * 0.8f, hr * 0.07f, hr * 0.04f, ang, LarderInk.WithAlpha(a), 8);
                }
            if (stage >= 2)  // Ravenous: the skin splits, raw red showing through
                for (int k = 0; k < 4; k++)
                {
                    float ang = face + MathX.Pi + (k - 1.5f) * 0.55f;
                    V2 p0 = pos + V2.FromAngle(ang) * hr * 0.3f, p1 = pos + V2.FromAngle(ang + 0.2f) * hr * 0.62f, p2 = pos + V2.FromAngle(ang - 0.1f) * hr * 0.88f;
                    Rgba crack = new Rgba(1f, 0.32f, 0.3f, (stage == 3 ? 0.6f + 0.4f * Mathf.Sin(t * 7f + k) : 0.75f) * a);
                    Line(p0, p1, 2.5f, crack);
                    Line(p1, p2, 2f, crack);
                }
            // the oral disc: a lip of papillae, then rings of teeth grinding in turn down to the throat
            V2 mc = pos + f * hr * 0.36f;
            float dr = hr * (0.5f + 0.16f * open);
            Circle(mc, dr + 3f, LarderInk.WithAlpha(a), 28);
            Circle(mc, dr, DvLip.Mix(flesh, 0.35f).WithAlpha(a), 28);
            for (int k = 0; k < 14; k++)
                Circle(mc + V2.FromAngle(face + MathX.Tau * k / 14f) * dr * 0.97f, dr * 0.09f, DvLip.Mix(Rgba.White, 0.3f).WithAlpha(a), 8);
            Circle(mc, dr * 0.86f, DvGum.WithAlpha(a), 26);
            ToothRing(mc, dr * 0.8f, 16, dr * 0.17f, t * 0.35f, a);
            Circle(mc, dr * 0.6f, DvGum.Mix(DvThroat, 0.5f).WithAlpha(a), 22);
            ToothRing(mc, dr * 0.56f, 11, dr * 0.15f, -t * 0.55f, a);
            float tr = dr * (0.25f + 0.17f * open);
            Circle(mc, tr, DvThroat.WithAlpha(a), 18);
            if (stage >= 2)
                ToothRing(mc, tr * 0.95f, 8, dr * 0.1f, t * 0.9f, a);
            if (eaten.HasValue)
                Circle(mc, tr * 0.7f, eaten.Value.WithAlpha((0.45f + 0.3f * Mathf.Sin(t * 8f)) * a), 14);
            DevourThroatGlow(mc, tr, stage, a, t);
            if (stage == 1)
                DevourDrool(mc + f * dr, f, hr, a, t);
        }

        // ---- B: the Glutton ------------------------------------------------------------------------------------

        void DrawDevourGlutton(V2 pos, float r, float face, V2 f, V2 sd, int stage, float open, Rgba flesh, Rgba? eaten, float a, float t)
        {
            float swell = stage == 1 ? 1.1f : stage >= 2 ? 1.04f : 1f;
            float bx = r * 0.95f * swell, by = r * 1.05f * swell;
            // stubby clawed arms, reaching forward on either side, grabbing in turn
            for (int side = -1; side <= 1; side += 2)
            {
                float reach = 0.5f + 0.12f * Mathf.Sin(t * 4f + side);
                V2 sh = pos + sd * side * by * 0.78f, hand = pos + sd * side * by * 1.02f + f * r * reach;
                Line(sh, hand, r * 0.26f + 5f, LarderInk.WithAlpha(a));
                Line(sh, hand, r * 0.26f, flesh.Mix(LarderInk, 0.15f));
                Circle(hand, r * 0.17f + 2.5f, LarderInk.WithAlpha(a), 12);
                Circle(hand, r * 0.17f, flesh, 12);
                for (int k = -1; k <= 1; k++)
                    Tooth(hand + V2.FromAngle(face + k * 0.45f) * r * 0.14f, face + k * 0.3f, r * 0.13f, a);
            }
            // the gut
            Ellipse(pos, bx + 3f, by + 3f, face, LarderInk.WithAlpha(a), 32);
            Ellipse(pos, bx, by, face, flesh, 32);
            Ellipse(pos - f * bx * 0.15f - sd * by * 0.3f, bx * 0.45f, by * 0.35f, face, flesh.Mix(Rgba.White, 0.2f).WithAlpha(0.5f * a), 20);
            // what it ate, pressing against the skin (and whoever it swallowed, struggling)
            if (stage >= 1 || eaten.HasValue)
                for (int k = 0; k < 3; k++)
                {
                    V2 at = pos - f * bx * 0.3f + V2.FromAngle(k * 2.1f + t * 0.4f) * r * 0.28f;
                    Circle(at, r * (0.16f + 0.04f * k), flesh.Mix(LarderInk, 0.3f).WithAlpha(0.55f * a), 14);
                }
            if (eaten.HasValue)
            {
                V2 at = pos - f * bx * 0.2f + sd * (Mathf.Sin(t * 9f) * r * 0.08f);
                Circle(at, r * 0.3f, eaten.Value.WithAlpha((0.35f + 0.2f * Mathf.Sin(t * 8f)) * a), 16);
                Arc(at, r * 0.3f, 2f, eaten.Value.WithAlpha(0.8f * a), 16);
            }
            for (int side = -1; side <= 1; side += 2)  // stretch marks on its flanks
                for (int k = 0; k < 3; k++)
                {
                    float ang = face + side * (1.7f + 0.25f * k);
                    V2 c = pos + V2.FromAngle(ang) * by * 0.82f;
                    Line(c - f * r * 0.1f, c + f * r * 0.1f, 1.5f, DvScar.WithAlpha(0.45f * a));
                }
            for (int k = 0; k < 4; k++)  // veins over its back
            {
                float ang = face + MathX.Pi + (k - 1.5f) * 0.6f;
                V2 p0 = pos + V2.FromAngle(ang) * r * 0.25f, p1 = pos + V2.FromAngle(ang + 0.25f) * r * 0.55f, p2 = pos + V2.FromAngle(ang - 0.15f) * r * 0.85f;
                Rgba vc = stage == 3 ? new Rgba(1f, 0.35f, 0.3f, (0.6f + 0.4f * Mathf.Sin(t * 7f + k)) * a) : DvVein.WithAlpha(0.7f * a);
                Line(p0, p1, 2.5f, vc);
                Line(p1, p2, 1.8f, vc);
            }
            if (stage < 2)  // a stitched seam across its back
            {
                V2 s0 = pos - f * r * 0.55f - sd * r * 0.45f, s1 = pos - f * r * 0.55f + sd * r * 0.45f;
                Line(s0, s1, 2f, LarderInk.WithAlpha(0.8f * a));
                for (int k = 0; k <= 5; k++)
                {
                    V2 c = V2.Lerp(s0, s1, k / 5f);
                    Line(c - f * r * 0.07f, c + f * r * 0.07f, 2f, DvScar.WithAlpha(0.8f * a));
                }
            }
            else  // Ravenous: the seam has burst into a second mouth
            {
                V2 bc = pos - f * r * 0.52f;
                float w = r * 0.5f, h = r * (0.12f + 0.06f * Mathf.Sin(t * 5f));
                Ellipse(bc, h + 3f, w + 3f, face, LarderInk.WithAlpha(a), 18);
                Ellipse(bc, h, w, face, DvThroat.WithAlpha(a), 18);
                for (int k = 0; k < 5; k++)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        float u = (k - 2f) / 2.5f;
                        V2 at = bc + sd * (u * w * 0.85f) + f * (side * h * Mathf.Sqrt(1f - u * u * 0.8f));
                        Tooth(at, face + MathX.Pi * (side > 0 ? 1f : 0f), r * 0.09f, a);
                    }
                DevourThroatGlow(bc, h * 1.2f, stage, a, t);
            }
            // the jaw: a wide slot across its front, tusks on both lips, the tongue lolling out when it opens
            V2 mc = pos + f * bx * 0.52f;
            float mw = by * 0.74f, mh = r * (0.1f + 0.3f * open);
            Ellipse(mc, mh + 4f, mw + 4f, face, LarderInk.WithAlpha(a), 24);
            Ellipse(mc, mh + 1.5f, mw + 1.5f, face, DvLip.Mix(flesh, 0.5f).WithAlpha(a), 24);
            Ellipse(mc, mh, mw, face, DvThroat.WithAlpha(a), 24);
            if (eaten == null && open > 0.6f)
                DevourThroatGlow(mc, mh, stage, a, t);
            if (open > 0.5f)
            {
                float lick = Mathf.Sin(t * 3f) * 0.15f;
                Ellipse(mc + f * mh * 0.6f, r * 0.24f, r * 0.15f, face + lick, new Rgba(0.95f, 0.45f, 0.55f, a), 14);
                Line(mc + f * mh * 0.3f, mc + f * (mh * 0.6f + r * 0.18f), 1.5f, new Rgba(0.6f, 0.15f, 0.25f, a));
            }
            for (int k = 0; k < 7; k++)
                for (int side = -1; side <= 1; side += 2)
                {
                    float u = (k - 3f) / 3.6f;
                    V2 at = mc + sd * (u * mw * 0.9f) + f * (side * mh * Mathf.Sqrt(1f - u * u * 0.85f));
                    float size = r * (k == 0 || k == 6 ? 0.2f : 0.12f + 0.03f * ((k * 3 + side) % 3));
                    Tooth(at, face + (side > 0 ? MathX.Pi : 0f), size, a);
                }
            if (stage == 1)
                DevourDrool(mc + f * mh, f, r, a, t);
        }

        // ---- C: the Maw Bloom ----------------------------------------------------------------------------------

        /// A petal from `c` along `ang`: a teardrop `len` long and `wid` wide.
        void DevourPetal(V2 c, float ang, float len, float wid, Rgba col)
        {
            V2.Axes(ang, out V2 d, out V2 n);
            SetColor(col);
            const int segs = 8;
            V2 pl = c, pr = c;
            for (int k = 1; k <= segs; k++)
            {
                float u = (float)k / segs, hw = wid * 0.5f * Mathf.Pow(Mathf.Sin(MathX.Pi * Mathf.Min(1f, u * 0.92f + 0.04f)), 0.7f);
                V2 mid = c + d * (len * u), l = mid + n * hw, rr = mid - n * hw;
                Tri(pl, pr, l);
                Tri(pr, rr, l);
                pl = l;
                pr = rr;
            }
        }

        /// A tongue from the throat: tapering segments, waving, curling at the tip.
        void DevourTongue(V2 from, float ang, float len, float w0, float phase, Rgba col, float a, float t)
        {
            const int segs = 9;
            V2 p = from;
            float dir = ang;
            for (int k = 1; k <= segs; k++)
            {
                float u = (float)k / segs;
                dir += Mathf.Sin(t * 3f + phase + u * 3f) * 0.22f + u * 0.06f;
                V2 next = p + V2.FromAngle(dir) * (len / segs);
                float w = w0 * (1f - 0.75f * u);
                Line(p, next, w + 3f, LarderInk.WithAlpha(a));
                p = next;
            }
            p = from;
            dir = ang;
            for (int k = 1; k <= segs; k++)
            {
                float u = (float)k / segs;
                dir += Mathf.Sin(t * 3f + phase + u * 3f) * 0.22f + u * 0.06f;
                V2 next = p + V2.FromAngle(dir) * (len / segs);
                float w = w0 * (1f - 0.75f * u);
                Line(p, next, w, col);
                Circle(next, w * 0.5f, col, 8);
                p = next;
            }
            Tooth(p, dir + 1.2f, w0 * 0.6f, a);
        }

        void DrawDevourBloom(V2 pos, float r, float face, V2 f, V2 sd, int stage, float open, Rgba flesh, Rgba? eaten, float a, float t)
        {
            float bloom = Mathf.Clamp01((stage == 0 ? 0.15f : stage == 1 ? 0.4f : stage == 2 ? 0.7f : 0.9f) + open * 0.35f);
            // the root: a dark lump the petals grow from (its body)
            SetColor(LarderInk.WithAlpha(a));
            for (int i = 0; i < 14; i++)
                Tri(pos, LumpPt(pos, r * 0.82f + 3f, 14, i, face, 9), LumpPt(pos, r * 0.82f + 3f, 14, i + 1, face, 9));
            SetColor(flesh.Mix(new Rgba(0.15f, 0.02f, 0.05f), 0.55f));
            for (int i = 0; i < 14; i++)
                Tri(pos, LumpPt(pos, r * 0.82f, 14, i, face, 9), LumpPt(pos, r * 0.82f, 14, i + 1, face, 9));
            // six petals, the front pair biggest and parted on its facing; they spread and lengthen as it blooms
            float len = r * (0.7f + 0.45f * bloom), wid = r * (0.95f - 0.25f * bloom), spread = 0.55f + 0.45f * bloom;
            Rgba outer = flesh, inner = flesh.Mix(DvGum, 0.55f);
            for (int pass = 0; pass < 3; pass++)
                for (int i = 0; i < 6; i++)
                {
                    float ang = face + MathX.Tau * (i + 0.5f) / 6f, front = Mathf.Cos(ang - face) > 0.5f ? 1.15f : 1f;
                    float l = len * front * (0.97f + 0.03f * Mathf.Sin(t * 2f + i)), back = r * 0.15f * (1f - bloom);
                    V2 c = pos + V2.FromAngle(ang) * back;
                    if (pass == 0)
                        DevourPetal(c, ang, l + 3f, wid * spread + 4f, LarderInk.WithAlpha(a));
                    else if (pass == 1)
                    {
                        DevourPetal(c, ang, l, wid * spread, outer);
                        DevourPetal(c, ang, l * (0.35f + 0.55f * bloom), wid * spread * 0.62f, inner);  // the inside shows as it opens
                    }
                    else
                    {
                        V2.Axes(ang, out V2 d, out V2 n);
                        Line(c + d * l * 0.2f, c + d * l * 0.85f, 1.5f, (stage >= 2 ? new Rgba(1f, 0.35f, 0.3f, (stage == 3 ? 0.6f + 0.4f * Mathf.Sin(t * 7f + i) : 0.6f)) : DvVein.WithAlpha(0.6f)).WithAlpha(a));
                        for (int side = -1; side <= 1; side += 2)  // hooked teeth on its edges, pointing in
                            Tooth(c + d * l * 0.55f + n * side * wid * spread * 0.4f, ang + MathX.Pi - side * 1.1f, r * 0.11f, a);
                        if (stage == 1)
                            DevourDrool(c + d * l * 0.9f, d, r * 0.7f, a, t + i * 0.3f);
                    }
                }
            // the throat in the middle, ringed with teeth, wider as it blooms
            float tr = r * (0.2f + 0.2f * bloom);
            Circle(pos, tr + 4f, LarderInk.WithAlpha(a), 22);
            Circle(pos, tr + 1f, DvGum.WithAlpha(a), 22);
            ToothRing(pos, tr * 0.95f, 12, tr * 0.32f, t * 0.4f, a);
            Circle(pos, tr * 0.62f, DvThroat.WithAlpha(a), 18);
            if (eaten.HasValue)
                Circle(pos, tr * 0.5f, eaten.Value.WithAlpha((0.45f + 0.3f * Mathf.Sin(t * 8f)) * a), 14);
            DevourThroatGlow(pos, tr * 0.62f, stage, a, t);
            // tongues reaching out over the front when it opens
            if (bloom > 0.35f)
            {
                Rgba tongue = new Rgba(0.95f, 0.5f, 0.58f, a);
                for (int k = -1; k <= 1; k++)
                    DevourTongue(pos + V2.FromAngle(face + k * 0.5f) * tr * 0.5f, face + k * 0.5f, r * (0.5f + 0.7f * (bloom - 0.35f)), r * 0.14f, k * 2.1f, tongue, a, t);
            }
        }
    }
}

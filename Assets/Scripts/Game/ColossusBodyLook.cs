using UnityEngine;

namespace Arena
{
    /// MOCKUP: three candidate bodies for the Colossus boss, one per look (ColLook 1-3; 0 = today's hexagon), each drawn
    /// per phase: Awakening (armoured), Crumbling (its four plates gone: they are the cover slabs), Molten Core (charred
    /// shell, lava seams) and Last Stand (kneeling, about to blow). Drawn facing e.Facing, in units where the body is r.
    ///   1 Quarry Golem: a hulking giant seen from above, two huge fists, slab plates bolted on.
    ///   2 Monolith: today's hexagon kept as a carved core, its armour shards orbiting it.
    ///   3 Mountain: a stone tortoise carrying a mountain whose peak becomes a volcano.
    public partial class ArenaApp
    {
        public static int ColLook;

        static readonly Rgba SandBase = new Rgba(0.7f, 0.56f, 0.4f), SandLite = new Rgba(0.88f, 0.76f, 0.58f), SandDark = new Rgba(0.33f, 0.24f, 0.17f),
            CharBase = new Rgba(0.3f, 0.22f, 0.18f), CharLite = new Rgba(0.46f, 0.34f, 0.27f), CharDark = new Rgba(0.12f, 0.08f, 0.06f),
            LavaHot = new Rgba(1f, 0.55f, 0.2f), LavaCore = new Rgba(1f, 0.86f, 0.5f), PlateEdge = new Rgba(0.78f, 0.62f, 0.45f);

        /// Paints TitanStone in the Colossus's colours: sandstone, or charred once molten; restores the Titan's after.
        void ColPaint(bool charred)
        {
            TitanBase = charred ? CharBase : SandBase;
            TitanLite = charred ? CharLite : SandLite;
            TitanDark = charred ? CharDark : SandDark;
            TitanGlow = LavaHot;
        }

        void ColPolyFill(V2[] p, Rgba c)
        {
            SetColor(c);
            for (int i = 1; i < p.Length - 1; i++)
                Tri(p[0], p[i], p[i + 1]);
        }

        void ColEllipse(V2 c, float rx, float ry, float ang, Rgba col, int n = 24)
        {
            V2.Axes(ang, out V2 X, out V2 Y);
            SetColor(col);
            V2 prev = c + X * rx;
            for (int i = 1; i <= n; i++)
            {
                float a = MathX.Tau * i / n;
                V2 p = c + X * (Mathf.Cos(a) * rx) + Y * (Mathf.Sin(a) * ry);
                Tri(c, prev, p);
                prev = p;
            }
        }

        /// A glowing seam: a soft wide line under a hot thin one.
        void ColSeam(V2 a, V2 b, float w, float k)
        {
            Line(a, b, w * 3f, LavaHot.WithAlpha(0.2f * k));
            Line(a, b, w, LavaHot.Mix(LavaCore, 0.3f).WithAlpha(0.95f * k));
        }

        /// One armour plate: the cover slab's look (DrawSlab), turned and bolted.
        void ColPlate(V2 c, float w, float h, float ang)
        {
            V2.Axes(ang, out V2 X, out V2 Y);
            System.Func<float, float, V2> P = (x, y) => c + X * x + Y * y;
            SetColor(new Rgba(0f, 0f, 0f, 0.35f));
            Quad(P(-w / 2f + 2f, -h / 2f + 3f), P(w / 2f + 2f, -h / 2f + 3f), P(w / 2f + 2f, h / 2f + 3f), P(-w / 2f + 2f, h / 2f + 3f));
            SetColor(new Rgba(0.37f, 0.3f, 0.24f));
            Quad(P(-w / 2f, -h / 2f), P(w / 2f, -h / 2f), P(w / 2f, h / 2f), P(-w / 2f, h / 2f));
            SetColor(new Rgba(0.5f, 0.41f, 0.32f));
            Quad(P(-w / 2f + 3f, -h / 2f + 3f), P(w / 2f - 3f, -h / 2f + 3f), P(w / 2f - 3f, h / 2f - 3f), P(-w / 2f + 3f, h / 2f - 3f));
            Line(P(-w / 2f, -h / 2f), P(w / 2f, -h / 2f), 2f, PlateEdge); Line(P(w / 2f, -h / 2f), P(w / 2f, h / 2f), 2f, PlateEdge);
            Line(P(w / 2f, h / 2f), P(-w / 2f, h / 2f), 2f, PlateEdge); Line(P(-w / 2f, h / 2f), P(-w / 2f, -h / 2f), 2f, PlateEdge);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    Circle(P(sx * (w / 2f - 6f), sy * (h / 2f - 6f)), 2.2f, new Rgba(0.25f, 0.19f, 0.14f), 6);
        }

        void DrawColossusBodyLook(Enemy e, V2 pos, float r, float t)
        {
            var c = e.Colossus;
            var stage = c != null ? c.Stage : ColStage.Awakening;
            ColPaint(stage >= ColStage.MoltenCore);
            try
            {
                if (ColLook == 1) ColGolem(e, pos, r, t, stage);
                else if (ColLook == 2) ColMonolith(e, pos, r, t, stage);
                else ColMountain(e, pos, r, t, stage);
            }
            finally { TitanPaint(false); }
        }

        // ---- 1: Quarry Golem ------------------------------------------------------------------------------------------

        void ColGolem(Enemy e, V2 pos, float r, float t, ColStage st)
        {
            float s = r / 54f, ang = e.Facing;
            bool kneel = st == ColStage.LastStand, molten = st >= ColStage.MoltenCore;
            V2.Axes(ang, out V2 fx, out V2 fy);
            System.Func<float, float, V2> L = (x, y) => pos + (fx * x + fy * y) * s;
            float wk = kneel ? 0f : Mathf.Sin(t * 3f);
            float glow = molten ? 0.75f + 0.25f * Mathf.Sin(t * 5f) : 0f;
            Circle(pos + new V2(5f, 9f) * s, 64f * s, new Rgba(0f, 0f, 0f, 0.35f), 28);
            if (!kneel)  // feet behind
            {
                TitanStone(L(-26f + wk * 8f, -22f), 15f * s, 11f, 5, 0f, 1f);
                TitanStone(L(-26f - wk * 8f, 22f), 15f * s, 12f, 5, 0f, 1f);
            }
            for (int sd = -1; sd <= 1; sd += 2)  // arms and fists: forward at its sides, planted ahead when kneeling
            {
                V2 sh = L(-6f, sd * 40f);
                V2 fp = kneel ? L(50f, sd * 36f) : L(30f + wk * sd * 6f, sd * 68f);
                Line(sh, fp, 22f * s, TitanDark);
                Line(sh, fp, 15f * s, TitanBase);
                Line(sh - fy * (sd * 4f * s), fp - fy * (sd * 4f * s), 3f * s, TitanLite.WithAlpha(0.5f));
                if (kneel)  // fists in the ground: a cracked ring under each
                    for (int k = 0; k < 5; k++)
                        ColSeam(fp + V2.FromAngle(ang + k * 1.25f + sd) * 18f * s, fp + V2.FromAngle(ang + k * 1.25f + sd + 0.2f) * 34f * s, 2f * s, 0.8f);
                TitanStone(fp, 24f * s, sd > 0 ? 21f : 22f, 7, glow, 1f);
                if (!molten)
                    for (int k = 0; k < 3; k++)  // knuckles
                        Circle(fp + (fx * 15f + fy * ((k - 1) * 9f)) * s, 4f * s, TitanDark.WithAlpha(0.7f), 6);
            }
            float back = kneel ? -8f : 0f;
            TitanStone(L(-6f + back, 0f), 40f * s, 31f, 9, 0f, 1f);  // the torso
            TitanStone(L(-6f + back, -40f), 22f * s, 32f, 7, glow * 0.5f, 1f);  // shoulders
            TitanStone(L(-6f + back, 40f), 22f * s, 33f, 7, glow * 0.5f, 1f);
            if (st == ColStage.Awakening)  // four plates: two pauldrons and a back plate pair (they fall as the cover slabs)
            {
                ColPlate(L(-8f, -42f), 30f * s, 22f * s, ang + 0.15f);
                ColPlate(L(-8f, 42f), 30f * s, 22f * s, ang - 0.15f);
                ColPlate(L(-22f, -13f), 26f * s, 24f * s, ang);
                ColPlate(L(-22f, 13f), 26f * s, 24f * s, ang);
            }
            else
            {
                for (int k = 0; k < 4; k++)  // the bare sockets where the plates were
                {
                    V2 at = k < 2 ? L(-8f, (k == 0 ? -1 : 1) * 42f) : L(-22f, (k == 2 ? -1 : 1) * 13f);
                    Circle(at, 7f * s, TitanDark.WithAlpha(0.85f), 8);
                }
                var crack = molten ? LavaHot : new Rgba(0.12f, 0.08f, 0.05f, 0.9f);
                for (int k = 0; k < (molten ? 6 : 4); k++)  // fractures across the torso
                {
                    float a = k * 1.1f + 0.4f;
                    V2 p1 = L(-6f + back, 0f), p2 = p1 + V2.FromAngle(ang + a) * 20f * s + fy * (TH(k) - 0.5f) * 8f * s, p3 = p1 + V2.FromAngle(ang + a - 0.25f) * 38f * s;
                    if (molten) { ColSeam(p1, p2, 3f * s, glow); ColSeam(p2, p3, 2.5f * s, glow); }
                    else { Line(p1, p2, 3f * s, crack); Line(p2, p3, 2.5f * s, crack); }
                }
                if (molten)  // the core in its chest
                {
                    float cr = (kneel ? 15f : 11f) + 2f * Mathf.Sin(t * 6f);
                    Circle(L(-6f + back, 0f), cr * 2.2f * s, LavaHot.WithAlpha(0.25f), 18);
                    Circle(L(-6f + back, 0f), cr * s, LavaCore, 16);
                }
            }
            // the head, sunk between the shoulders; bowed when kneeling
            V2 head = L(kneel ? 22f : 36f, 0f);
            Circle(head + fx * 3f * s, 17f * s, new Rgba(0f, 0f, 0f, 0.3f), 14);
            TitanStone(head, 16f * s, 41f, 6, 0f, 1f);
            Line(head + (fx * 6f - fy * 10f) * s, head + (fx * 6f + fy * 10f) * s, 4f * s, TitanDark);  // the brow
            var eye = molten ? LavaCore : new Rgba(1f, 0.92f, 0.72f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Circle(head + (fx * 10f + fy * (sd * 6f)) * s, 6f * s, eye.WithAlpha(0.3f), 10);
                Circle(head + (fx * 10f + fy * (sd * 6f)) * s, 2.6f * s, eye, 8);
            }
        }

        // ---- 2: Monolith ----------------------------------------------------------------------------------------------

        void ColMonolith(Enemy e, V2 pos, float r, float t, ColStage st)
        {
            float s = r / 54f, ang = e.Facing;
            bool molten = st >= ColStage.MoltenCore, kneel = st == ColStage.LastStand;
            float glow = molten ? 0.75f + 0.25f * Mathf.Sin(t * 5f) : 0f;
            Circle(pos + new V2(4f, 8f) * s, 60f * s, new Rgba(0f, 0f, 0f, 0.32f), 28);
            int shards = st == ColStage.Awakening ? 6 : st == ColStage.Crumbling ? 2 : kneel ? 6 : 3;
            float orbit = (kneel ? 44f : 66f) * s, spin = t * (kneel ? 2.4f : 0.6f);
            for (int i = 0; i < shards; i++)  // its armour: wedge shards circling it (four fall as the cover slabs)
            {
                float a = spin + MathX.Tau * i / shards;
                V2 at = pos + V2.FromAngle(a) * orbit;
                V2.Axes(a, out V2 X, out V2 Y);
                float w0 = 20f * s, w1 = 13f * s, d = 12f * s;
                var pts = new[] { at - X * d - Y * w0, at + X * d - Y * w1, at + X * d + Y * w1, at - X * d + Y * w0 };
                Circle(at + new V2(3f, 6f) * s, 15f * s, new Rgba(0f, 0f, 0f, 0.3f), 10);
                ColPolyFill(pts, molten ? CharDark : SandDark);
                var inner = new[] { V2.Lerp(at, pts[0], 0.75f), V2.Lerp(at, pts[1], 0.75f), V2.Lerp(at, pts[2], 0.75f), V2.Lerp(at, pts[3], 0.75f) };
                ColPolyFill(inner, molten ? CharBase : SandBase);
                Line(pts[1], pts[2], 2.5f, molten ? LavaHot.WithAlpha(0.8f * glow) : PlateEdge);
                if (!molten)
                    TitanRune(at, 8f * s, a, i, 1.6f * s, SandDark.WithAlpha(0.8f));
            }
            float core = (kneel ? 46f : 44f) * s, rot = ang + MathX.Pi / 6f;
            var hex = new V2[6];
            for (int i = 0; i < 6; i++)
                hex[i] = pos + V2.FromAngle(rot + MathX.Tau * i / 6f) * core;
            if (!molten)
            {
                ColPolyFill(hex, SandDark);
                var inner = new V2[6];
                for (int i = 0; i < 6; i++) inner[i] = V2.Lerp(pos, hex[i], 0.86f);
                ColPolyFill(inner, SandBase);
                var top = new V2[6];
                for (int i = 0; i < 6; i++) top[i] = V2.Lerp(pos + new V2(-4f, -5f) * s, hex[i], 0.5f);
                ColPolyFill(top, SandLite.WithAlpha(0.7f));
                for (int i = 0; i < 6; i++) Line(hex[i], hex[(i + 1) % 6], 3f, SandDark);
                if (st == ColStage.Awakening)
                    for (int i = 0; i < 6; i++)  // carved rune band
                        TitanRune(V2.Lerp(pos, V2.Lerp(hex[i], hex[(i + 1) % 6], 0.5f), 0.68f), 7f * s, rot + MathX.Tau * (i + 0.5f) / 6f + MathX.Pi / 2f, i, 1.6f * s, SandDark);
                else
                    for (int k = 0; k < 4; k++)  // cracked across
                    {
                        float ca = rot + k * 1.57f + 0.5f;
                        V2 p2 = pos + V2.FromAngle(ca + 0.2f) * core * 0.45f, p3 = pos + V2.FromAngle(ca - 0.08f) * core * 0.92f;
                        Line(pos, p2, 3f * s, new Rgba(0.12f, 0.08f, 0.05f, 0.9f));
                        Line(p2, p3, 2.5f * s, new Rgba(0.12f, 0.08f, 0.05f, 0.9f));
                    }
            }
            else
            {
                // the shell split into three charred pieces round a molten orb
                float gap = kneel ? 10f * s : 6f * s;
                float orb = (kneel ? 26f : 20f) * s + 2f * s * Mathf.Sin(t * 6f);
                GlowDisc(pos, orb * 3f, LavaHot, 0.5f * glow);
                Circle(pos, orb * 1.35f, LavaHot.WithAlpha(0.6f), 20);
                Circle(pos, orb, LavaCore, 20);
                for (int k = 0; k < 3; k++)
                {
                    V2 off = V2.FromAngle(rot + MathX.Tau * (k * 2 + 1) / 6f) * gap;
                    var piece = new[] { pos + off + V2.FromAngle(rot + MathX.Tau * (k * 2) / 6f) * core * 0.45f, hex[k * 2] + off, hex[k * 2 + 1] + off, hex[(k * 2 + 2) % 6] + off, pos + off + V2.FromAngle(rot + MathX.Tau * (k * 2 + 2) / 6f) * core * 0.45f };
                    ColPolyFill(piece, CharDark);
                    var inner = new V2[5];
                    V2 mid = (piece[1] + piece[2] + piece[3]) / 3f;
                    for (int i = 0; i < 5; i++) inner[i] = V2.Lerp(mid, piece[i], 0.8f);
                    ColPolyFill(inner, CharBase);
                    ColSeam(piece[0], piece[4], 2f * s, glow);
                }
            }
            var eye = molten ? LavaCore : new Rgba(1f, 0.92f, 0.72f);  // its eyes, on its target
            for (int k = -1; k <= 1; k += 2)
            {
                V2 at = pos + V2.FromAngle(ang + k * 0.32f) * core * 0.72f;
                Circle(at, 7f * s, eye.WithAlpha(0.3f), 10);
                Circle(at, 3.5f * s, eye, 8);
            }
        }

        // ---- 3: Mountain ----------------------------------------------------------------------------------------------

        void ColMountain(Enemy e, V2 pos, float r, float t, ColStage st)
        {
            float s = r / 54f, ang = e.Facing;
            bool molten = st >= ColStage.MoltenCore, kneel = st == ColStage.LastStand;
            float glow = molten ? 0.75f + 0.25f * Mathf.Sin(t * 5f) : 0f;
            V2.Axes(ang, out V2 fx, out V2 fy);
            System.Func<float, float, V2> L = (x, y) => pos + (fx * x + fy * y) * s;
            ColEllipse(pos + new V2(5f, 9f) * s, 66f * s, 56f * s, ang, new Rgba(0f, 0f, 0f, 0.35f));
            float wk = kneel ? 0f : Mathf.Sin(t * 3f);
            if (!kneel)
                for (int i = 0; i < 4; i++)  // four stumpy legs at the corners
                {
                    float lx = i < 2 ? 30f : -30f, ly = i % 2 == 0 ? -46f : 46f, sw = (i == 0 || i == 3 ? 1f : -1f) * wk * 7f;
                    TitanStone(L(lx + sw, ly), 15f * s, 50f + i, 6, glow * 0.6f, 1f);
                }
            // the head: low and wide, two tusks
            V2 head = L(kneel ? 46f : 60f, 0f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                V2 tb = head + (fx * 6f + fy * (sd * 12f)) * s, tt = head + (fx * 24f + fy * (sd * 20f)) * s;
                Line(tb, tt, 6f * s, SandLite.Mix(Rgba.White, 0.2f));
                Line(tb, tt, 2f * s, SandDark.WithAlpha(0.5f));
            }
            TitanStone(head, 18f * s, 61f, 7, 0f, 1f);
            var eye = molten ? LavaCore : new Rgba(1f, 0.92f, 0.72f);
            for (int sd = -1; sd <= 1; sd += 2)
            {
                Circle(head + (fx * 9f + fy * (sd * 8f)) * s, 5.5f * s, eye.WithAlpha(0.3f), 10);
                Circle(head + (fx * 9f + fy * (sd * 8f)) * s, 2.5f * s, eye, 8);
            }
            // the shell: an oval of rock plates rising to a peak
            float rx = 52f * s, ry = 46f * s;
            ColEllipse(pos, rx, ry, ang, TitanDark);
            ColEllipse(pos + new V2(-2f, -2f) * s, rx - 4f * s, ry - 4f * s, ang, TitanBase);
            for (int ring = 0; ring < 2; ring++)  // two rings of scutes
            {
                int n = ring == 0 ? 10 : 6;
                float rr = ring == 0 ? 0.78f : 0.45f;
                for (int i = 0; i < n; i++)
                {
                    float a = MathX.Tau * (i + ring * 0.5f) / n;
                    V2 at = pos + fx * (Mathf.Cos(a) * rx * rr) + fy * (Mathf.Sin(a) * ry * rr);
                    if (molten && ring == 1)
                        continue;
                    bool lost = st == ColStage.Crumbling && ring == 0 && i % 3 == 1;
                    if (lost) { Circle(at, 9f * s, TitanDark.WithAlpha(0.9f), 8); continue; }
                    TitanStone(at, (ring == 0 ? 12f : 13f) * s, 70f + ring * 20f + i, 6, molten ? glow * 0.5f : 0f, 1f);
                }
            }
            if (st == ColStage.Awakening)  // the four plates, strapped on its flanks (they fall as the cover slabs)
            {
                ColPlate(L(8f, -36f), 34f * s, 18f * s, ang);
                ColPlate(L(8f, 36f), 34f * s, 18f * s, ang);
                ColPlate(L(-30f, -24f), 18f * s, 30f * s, ang + 0.5f);
                ColPlate(L(-30f, 24f), 18f * s, 30f * s, ang - 0.5f);
            }
            if (!molten)  // the peak
            {
                TitanStone(L(-4f, 0f), 20f * s, 91f, 7, 0f, 1f);
                TitanStone(L(-1f, -2f), 11f * s, 92f, 5, 0f, 1f);
                if (st == ColStage.Crumbling)
                    for (int k = 0; k < 3; k++)
                        Line(L(-4f, 0f), L(-4f, 0f) + V2.FromAngle(ang + k * 2.1f + 0.5f) * 40f * s, 3f * s, new Rgba(0.12f, 0.08f, 0.05f, 0.9f));
            }
            else  // the peak has blown: a crater of lava, seams running down the shell
            {
                for (int k = 0; k < 6; k++)
                {
                    V2 a0 = L(-4f, 0f) + V2.FromAngle(ang + k * 1.05f + 0.2f) * 18f * s, a1 = L(-4f, 0f) + V2.FromAngle(ang + k * 1.05f + 0.35f) * 46f * s;
                    ColSeam(a0, a1, 2.5f * s, glow);
                }
                float cr = (kneel ? 24f : 18f) * s;
                Circle(L(-4f, 0f), cr + 7f * s, CharDark, 18);
                GlowDisc(L(-4f, 0f), cr * 2.4f, LavaHot, 0.45f * glow);
                Circle(L(-4f, 0f), cr, LavaHot, 18);
                Circle(L(-4f, 0f), cr * 0.55f + 2f * s * Mathf.Sin(t * 6f), LavaCore, 14);
                if (kneel)
                    for (int k = 0; k < 7; k++)  // erupting: embers thrown up
                    {
                        float a = TH(k * 3.3f) * MathX.Tau, d = (0.4f + ((t * 0.8f + TH(k)) % 1f)) * 44f * s;
                        Circle(L(-4f, 0f) + V2.FromAngle(a) * d, 3f * s, LavaCore.WithAlpha(0.9f), 6);
                    }
            }
        }
    }
}

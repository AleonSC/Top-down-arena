using UnityEngine;

namespace Arena
{
    /// The Colossus's body (Colossus.cs), look A of Docs/Mockups (the Quarry Golem): a sandstone giant seen from above, two
    /// huge fists, four slab plates bolted on (they come off at Crumbling: they're the cover slabs), charred with lava
    /// seams and a core in its chest from Molten Core, kneeling with its fists in the ground for the Last Stand.
    /// Every pose comes from what the snapshot already carries: a stomp ring winding up at its feet (fists up, then the
    /// slam), the charge's warning and run (ChargeWarn, ChargeLeft, ChargeDir), a boulder or meteor just thrown (its
    /// drop's From and T), the daze (DazeT) and the phase change (TransT).
    public partial class ArenaApp
    {
        static readonly Rgba SandBase = new Rgba(0.7f, 0.56f, 0.4f), SandLite = new Rgba(0.88f, 0.76f, 0.58f), SandDark = new Rgba(0.33f, 0.24f, 0.17f),
            CharBase = new Rgba(0.3f, 0.22f, 0.18f), CharLite = new Rgba(0.46f, 0.34f, 0.27f), CharDark = new Rgba(0.12f, 0.08f, 0.06f),
            LavaHot = new Rgba(1f, 0.55f, 0.2f), LavaCore = new Rgba(1f, 0.86f, 0.5f), PlateEdge = new Rgba(0.78f, 0.62f, 0.45f);
        const float ColUnit = 60f;  // drawing units per body radius (the fists reach about 1.6 radii out)
        const float ColSlamShow = 45f;  // how far a fresh ring spreads while the fists still show the slam

        /// Paints TitanStone (TitanLook.cs) in the Colossus's colours: sandstone, or charred once molten.
        static void ColPaint(bool charred)
        {
            TitanBase = charred ? CharBase : SandBase;
            TitanLite = charred ? CharLite : SandLite;
            TitanDark = charred ? CharDark : SandDark;
            TitanGlow = LavaHot;
        }

        /// A glowing seam: a soft wide line under a hot thin one.
        void ColSeam(V2 a, V2 b, float w, float k)
        {
            Line(a, b, w * 3f, LavaHot.WithAlpha(0.2f * k));
            Line(a, b, w, LavaHot.Mix(LavaCore, 0.3f).WithAlpha(0.95f * k));
        }

        /// One armour plate: the cover slab's look (DrawSlab), turned and bolted on.
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

        /// Its newest stomp ring still at its feet: winding up (Warn left), or just slammed (how far it has spread).
        static StompRing ColFreshRing(ColossusState c, Enemy e)
        {
            StompRing best = null;
            foreach (var ring in c.Rings)
                if ((ring.Center - e.Disp).Length < e.Radius && ring.Radius - e.Radius - 6f < ColSlamShow && (best == null || ring.Radius < best.Radius))
                    best = ring;
            return best;
        }

        /// A rock it threw a moment ago (its arm still out), or null.
        static ColDrop ColFreshThrow(ColossusState c, Enemy e)
        {
            foreach (var d in c.Drops)
                if (d.Kind != ColDrop.Pillar && d.T < 0.3f && (d.From - e.Disp).Length < e.Radius * 1.5f)
                    return d;
            return null;
        }

        void DrawColossusBody(Enemy e, V2 pos, float r, float t)
        {
            var c = e.Colossus;
            var st = c != null ? c.Stage : ColStage.Awakening;
            bool kneel = st == ColStage.LastStand, molten = st >= ColStage.MoltenCore;
            bool charging = c != null && c.ChargeLeft > 0f, bracing = c != null && c.ChargeWarn > 0f && !charging;
            bool dazed = c != null && c.DazeT > 0f, roaring = c != null && c.TransT > 0f;
            var ring = c != null ? ColFreshRing(c, e) : null;
            var thrown = c != null && !charging ? ColFreshThrow(c, e) : null;
            float s = r / ColUnit, ang = charging || bracing ? c.ChargeDir : e.Facing;
            if (roaring || bracing)  // it shakes as it roars or braces
                pos += new V2(Mathf.Sin(t * 53f), Mathf.Cos(t * 47f)) * (roaring ? 2.5f : 1.5f) * s;
            V2.Axes(ang, out V2 fx, out V2 fy);
            System.Func<float, float, V2> L = (x, y) => pos + (fx * x + fy * y) * s;
            float wk = kneel || dazed || roaring ? 0f : Mathf.Sin(t * (charging ? 12f : 3f));
            float glow = molten ? 0.75f + 0.25f * Mathf.Sin(t * 5f) : 0f;
            float lean = charging ? 8f : bracing ? -6f * Mathf.Clamp01(1f - c.ChargeWarn / Game.ColChargeWarn) : kneel ? -8f : 0f;

            ColPaint(molten);
            try
            {
                Circle(pos + new V2(5f, 9f) * s, 64f * s, new Rgba(0f, 0f, 0f, 0.35f), 28);
                if (charging)  // dust kicked up behind
                    for (int k = 0; k < 5; k++)
                    {
                        float d = ((t * 3f + k * 0.2f) % 1f);
                        Circle(L(-40f - d * 50f, (TH(k + Mathf.Floor(t * 3f)) - 0.5f) * 60f), (8f + d * 10f) * s, TitanDust.Mix(SandLite, 0.3f).WithAlpha(0.35f * (1f - d)), 10);
                    }
                if (!kneel)  // feet, behind
                {
                    TitanStone(L(-26f + wk * 8f, -22f), 15f * s, 11f, 5, 0f, 1f);
                    TitanStone(L(-26f - wk * 8f, 22f), 15f * s, 12f, 5, 0f, 1f);
                }
                for (int sd = -1; sd <= 1; sd += 2)
                    ColArm(c, e, L, fx, fy, s, sd, t, ang, st, wk, glow, lean, charging, bracing, dazed, roaring, ring, thrown);
                ColTorso(L, fx, fy, s, t, ang, st, glow, lean);
                // the head, sunk between the shoulders; bowed when kneeling, lolling when dazed
                V2 head = L((kneel ? 22f : 36f) + lean, dazed ? Mathf.Sin(t * 4f) * 7f : 0f);
                Circle(head + fx * 3f * s, 17f * s, new Rgba(0f, 0f, 0f, 0.3f), 14);
                TitanStone(head, 16f * s, 41f, 6, 0f, 1f);
                Line(head + (fx * 6f - fy * 10f) * s, head + (fx * 6f + fy * 10f) * s, 4f * s, TitanDark);  // the brow
                var eye = molten || roaring || bracing ? LavaCore : new Rgba(1f, 0.92f, 0.72f);
                float eyeA = dazed ? 0.35f : 1f;
                for (int sd = -1; sd <= 1; sd += 2)
                {
                    V2 at = head + (fx * 10f + fy * (sd * 6f)) * s;
                    Circle(at, (roaring ? 9f : 6f) * s, eye.WithAlpha(0.3f * eyeA), 10);
                    Circle(at, 2.6f * s, eye.WithAlpha(eyeA), 8);
                }
                if (dazed)
                    for (int k = 0; k < 3; k++)  // dizzy stars circling its head
                    {
                        V2 at = head + new V2(Mathf.Cos(t * 5f + k * 2.1f) * 28f, Mathf.Sin(t * 5f + k * 2.1f) * 9f - 22f) * s;
                        Polygon(at, 6f * s, 5, t * 4f, new Rgba(1f, 0.85f, 0.35f));
                    }
            }
            finally { TitanPaint(false); }
        }

        /// One arm and fist (sd: -1 left, 1 right), posed for what it's doing.
        void ColArm(ColossusState c, Enemy e, System.Func<float, float, V2> L, V2 fx, V2 fy, float s, int sd, float t, float ang, ColStage st,
            float wk, float glow, float lean, bool charging, bool bracing, bool dazed, bool roaring, StompRing ring, ColDrop thrown)
        {
            bool kneel = st == ColStage.LastStand, molten = st >= ColStage.MoltenCore;
            V2 rest = kneel ? new V2(50f, sd * 36f) : new V2(30f + wk * sd * 6f, sd * 68f), at = rest;
            float lift = 0f, slam = 0f;
            if (ring != null && ring.Warn > 0f)  // a stomp winding up: both fists rise over its head
            {
                lift = TEase(1f - ring.Warn / Game.RingWarn);
                at = V2.Lerp(rest, new V2(8f, sd * 44f), lift);
            }
            else if (ring != null)  // the slam: fists down in front, the ground cracked under them
            {
                slam = 1f - (ring.Radius - e.Radius - 6f) / ColSlamShow;
                at = new V2(46f, sd * 40f);
            }
            else if (charging)
                at = new V2(56f, sd * 26f);  // fists forward together, a battering ram
            else if (bracing)
                at = V2.Lerp(rest, new V2(-12f, sd * 72f), TEase(1f - c.ChargeWarn / Game.ColChargeWarn));  // drawn back to brace
            else if (dazed)
                at = new V2(8f + Mathf.Sin(t * 3f + sd) * 4f, sd * 80f);  // hanging limp at its sides
            else if (roaring)
            {
                at = new V2(-2f, sd * 82f);  // flung wide, up
                lift = 0.7f;
            }
            else if (thrown != null && (Mathf.Sin((thrown.Pos - thrown.From).Angle - ang) <= 0f) == (sd == 1))  // the arm on the throw's side, still out toward where the rock is going
            {
                float k = thrown.T / 0.3f;
                float rel = (thrown.Pos - thrown.From).Angle - ang;
                at = V2.Lerp(new V2(Mathf.Cos(rel), -Mathf.Sin(rel)) * 84f, rest, k);  // (local y is the -90 degree side)
                lift = 0.5f * (1f - k);
            }
            V2 sh = L(-6f + lean, sd * 40f), fp = L(at.X + lean * 0.5f, at.Y);
            float fs = 1f + 0.35f * lift;
            Line(sh, fp, 22f * s, TitanDark);
            Line(sh, fp, 15f * s, TitanBase);
            Line(sh - fy * (sd * 4f * s), fp - fy * (sd * 4f * s), 3f * s, TitanLite.WithAlpha(0.5f));
            if (lift > 0.05f)  // its shadow, falling away below a raised fist
                Circle(fp + new V2(14f, 16f) * (lift * s), 22f * s, new Rgba(0f, 0f, 0f, 0.3f * lift), 16);
            if (kneel || slam > 0f)  // fists in the ground: the floor cracked round each
            {
                float k = kneel ? 0.8f : slam;
                for (int i = 0; i < 5; i++)
                {
                    float a = ang + i * 1.25f + sd;
                    if (molten) ColSeam(fp + V2.FromAngle(a) * 18f * s, fp + V2.FromAngle(a + 0.2f) * 34f * s, 2f * s, k);
                    else Line(fp + V2.FromAngle(a) * 18f * s, fp + V2.FromAngle(a + 0.2f) * 34f * s, 3f * s, SandDark.WithAlpha(0.8f * k));
                }
                if (slam > 0f)
                    Arc(fp, (24f + 20f * (1f - slam)) * s, 3f * s, SandLite.WithAlpha(0.6f * slam), 16);
            }
            TitanStone(fp, 24f * fs * s, sd > 0 ? 21f : 22f, 7, glow, 1f);
            if (!molten)
                for (int k = 0; k < 3; k++)  // knuckles
                    Circle(fp + (fx * 15f + fy * ((k - 1) * 9f)) * fs * s, 4f * s, TitanDark.WithAlpha(0.7f), 6);
        }

        /// Its torso and shoulders: plates on (Awakening), then bare sockets and fractures, then lava seams and a core.
        void ColTorso(System.Func<float, float, V2> L, V2 fx, V2 fy, float s, float t, float ang, ColStage st, float glow, float lean)
        {
            bool kneel = st == ColStage.LastStand, molten = st >= ColStage.MoltenCore;
            float b = -6f + lean;
            TitanStone(L(b, 0f), 40f * s, 31f, 9, 0f, 1f);
            TitanStone(L(b, -40f), 22f * s, 32f, 7, glow * 0.5f, 1f);
            TitanStone(L(b, 40f), 22f * s, 33f, 7, glow * 0.5f, 1f);
            if (st == ColStage.Awakening)  // two pauldrons and a pair of back plates
            {
                ColPlate(L(b - 2f, -42f), 30f * s, 22f * s, ang + 0.15f);
                ColPlate(L(b - 2f, 42f), 30f * s, 22f * s, ang - 0.15f);
                ColPlate(L(b - 16f, -13f), 26f * s, 24f * s, ang);
                ColPlate(L(b - 16f, 13f), 26f * s, 24f * s, ang);
                return;
            }
            for (int k = 0; k < 4; k++)  // the bare sockets where the plates were
            {
                V2 at = k < 2 ? L(b - 2f, (k == 0 ? -1 : 1) * 42f) : L(b - 16f, (k == 2 ? -1 : 1) * 13f);
                Circle(at, 7f * s, TitanDark.WithAlpha(0.85f), 8);
            }
            var crack = new Rgba(0.12f, 0.08f, 0.05f, 0.9f);
            for (int k = 0; k < (molten ? 6 : 4); k++)  // fractures across the torso
            {
                float a = k * 1.1f + 0.4f;
                V2 p1 = L(b, 0f), p2 = p1 + V2.FromAngle(ang + a) * 20f * s + fy * (TH(k) - 0.5f) * 8f * s, p3 = p1 + V2.FromAngle(ang + a - 0.25f) * 38f * s;
                if (molten) { ColSeam(p1, p2, 3f * s, glow); ColSeam(p2, p3, 2.5f * s, glow); }
                else { Line(p1, p2, 3f * s, crack); Line(p2, p3, 2.5f * s, crack); }
            }
            if (molten)  // the core in its chest
            {
                float cr = (kneel ? 15f : 11f) + 2f * Mathf.Sin(t * 6f);
                Circle(L(b, 0f), cr * 2.2f * s, LavaHot.WithAlpha(0.25f), 18);
                Circle(L(b, 0f), cr * s, LavaCore, 16);
            }
        }
    }
}

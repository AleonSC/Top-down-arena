using UnityEngine;

namespace Arena
{
    /// The Knight Banners (Core/KnightBanners.cs; mockup Docs/Mockups/knight-banners.html), all from replicated state:
    ///   the standard (the zone, its age the phase) falls in with its shadow growing, slams down with a dust ring, then stands:
    ///                 a dark oak pole, an iron crossbar, a gold finial and a swallowtail cloth swaying in the wind with the
    ///                 banner's emblem (Aegis: a silver kite shield on steel blue; Pilgrim's: a gold sun on ivory; War: crossed
    ///                 gold swords on crimson). Lifted, it droops and fades.
    ///   the ring      Aegis: hex plates turning round the rim; Pilgrim's: soft gold light and motes drifting up; War: chevrons
    ///                 turning outward and embers. A time arc drains over the 60 s.
    ///   the allies    Aegis: a hex bubble while Player.StdShieldHp is up (its fill the shield left, flashing as it drops);
    ///                 Pilgrim's: a gold glow under them and a light thread from the finial; War: a crimson ring under their feet.
    public partial class ArenaApp
    {
        static readonly Rgba BnSteel = new Rgba(0.62f, 0.78f, 1f), BnSteelCloth = new Rgba(0.18f, 0.35f, 0.59f), BnSteelDark = new Rgba(0.11f, 0.23f, 0.4f), BnSilver = new Rgba(0.84f, 0.92f, 1f);
        static readonly Rgba BnHoly = new Rgba(1f, 0.89f, 0.6f), BnIvory = new Rgba(0.93f, 0.89f, 0.76f), BnIvoryDark = new Rgba(0.79f, 0.72f, 0.52f);
        static readonly Rgba BnWar = new Rgba(1f, 0.42f, 0.35f), BnCrimson = new Rgba(0.64f, 0.13f, 0.18f), BnCrimsonDark = new Rgba(0.38f, 0.07f, 0.11f);
        static readonly Rgba BnOak = new Rgba(0.42f, 0.29f, 0.17f), BnOakDark = new Rgba(0.16f, 0.11f, 0.07f), BnIron = new Rgba(0.23f, 0.25f, 0.29f);
        const float BnPole = 74f, BnClothW = 30f, BnClothL = 44f;
        readonly System.Collections.Generic.Dictionary<int, float> bnShownShield = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.Dictionary<int, float> bnFlash = new System.Collections.Generic.Dictionary<int, float>();

        static Rgba BnGlow(ZoneKind k) => k == ZoneKind.AegisStandard ? BnSteel : k == ZoneKind.PilgrimsStandard ? BnHoly : BnWar;

        // ---------------- the zone: ring and standard ----------------

        void DrawKnightBanner(Zone z, float t)
        {
            float age = Game.BannerAge(z);
            bool folding = z.Aim > 0f;
            float fold = folding ? 1f - Mathf.Clamp01(z.T / Game.BannerFold) : 0f, alpha = 1f - fold;
            Rgba glow = BnGlow(z.Kind);
            V2 c = z.Pos;
            if (age < Game.BannerThrow)  // in the air: the shadow grows, the standard drops in
            {
                float k = age / Game.BannerThrow;
                Circle(c, 6f + 8f * k, new Rgba(0f, 0f, 0f, 0.2f + 0.35f * k), 14);
                DrawStandard(z.Kind, c + new V2(0f, -90f * (1f - k) * (1f - k)), t, 0f, 1f);
                return;
            }
            float r = Game.BannerReach(z) * (folding ? 1f - 0.25f * fold : 1f);
            DrawBannerRing(z.Kind, c, r, alpha, t);
            if (!folding)
            {
                float left = 1f - Mathf.Clamp01(age / Game.BannerTime);
                Arc(c, r + 8f, 3f, glow.WithAlpha(0.85f), 64, -MathX.Pi / 2f, MathX.Tau * left);  // the time arc
            }
            float since = age - Game.BannerThrow;
            if (since < 0.4f && !folding)  // the slam: a white ring and dust
            {
                float k = since / 0.4f;
                EllipseLine(c, 20f + 90f * k, (20f + 90f * k) * 0.45f, 3f, new Rgba(1f, 1f, 1f, 0.7f * (1f - k)), 28);
                if (!fxReduced)
                    for (int i = 0; i < 14; i++)
                    {
                        float a = MathX.Tau * i / 14f + i * 0.41f;
                        Circle(c + V2.FromAngle(a) * (14f + k * (50f + 20f * Mathf.Sin(i * 1.7f))), 4f * (1f - k) + 1f, new Rgba(0.6f, 0.53f, 0.42f, 0.6f * (1f - k)), 8);
                    }
            }
            Ellipse(c + new V2(0f, 1f), 9f, 3.5f, new Rgba(0f, 0f, 0f, 0.45f * alpha));
            if (fxOn)
                GlowDisc(c + new V2(0f, -50f), 60f, glow, 0.22f * alpha);
            float kick = since < 0.15f ? Mathf.Sin(since / 0.15f * MathX.Pi) * 3f : 0f;
            DrawStandard(z.Kind, c + new V2(0f, kick), t, fold, alpha);
        }

        void DrawBannerRing(ZoneKind k, V2 c, float r, float a, float t)
        {
            if (r < 2f || a <= 0f)
                return;
            Rgba glow = BnGlow(k);
            Circle(c, r, glow.WithAlpha(0.08f * a), 56);
            Arc(c, r * 0.97f, r * 0.06f, glow.WithAlpha(0.08f * a), 56);
            if (k == ZoneKind.AegisStandard)
            {
                int n = Mathf.Max(12, Mathf.RoundToInt(r * MathX.Tau / 38f));
                for (int i = 0; i < n; i++)
                {
                    V2 at = c + V2.FromAngle(MathX.Tau * i / n + t * 0.15f) * (r - 6f);
                    HexFill(at, 7f, glow.WithAlpha(0.1f * a));
                    HexOutline(at, 7f, 1f, glow.WithAlpha(0.55f * a));
                }
            }
            else if (k == ZoneKind.PilgrimsStandard)
            {
                Arc(c, r, 1.5f, glow.WithAlpha(0.6f * a), 64);
                Arc(c, r - 10f, 1f, glow.WithAlpha(0.25f * a), 48);
                if (!fxReduced)
                    for (int i = 0; i < 26; i++)  // motes drifting up
                    {
                        float seed = i * 97.13f, ang = seed % MathX.Tau, rr = r * (0.2f + (seed * 7f) % 0.8f);
                        float ph = ((t + i * 0.37f) % 2.4f) / 2.4f;
                        Circle(c + V2.FromAngle(ang) * rr + new V2(0f, -34f * ph), 1.6f, new Rgba(1f, 0.94f, 0.78f, a * Mathf.Sin(ph * MathX.Pi) * 0.8f), 6);
                    }
            }
            else
            {
                Arc(c, r, 2f, glow.WithAlpha(0.7f * a), 64);
                for (int i = 0; i < 18; i++)  // chevrons turning outward
                {
                    float ang = MathX.Tau * i / 18f - t * 0.4f;
                    V2 fw = V2.FromAngle(ang), side = fw.Orthogonal();
                    V2 tip = c + fw * (r - 9f + Mathf.Sin(t * 5f + i) * 2f);
                    Line(tip - fw * 7f + side * 6f, tip, 2f, glow.WithAlpha(0.6f * a));
                    Line(tip - fw * 7f - side * 6f, tip, 2f, glow.WithAlpha(0.6f * a));
                }
                if (!fxReduced)
                    for (int i = 0; i < 22; i++)  // embers
                    {
                        float seed = i * 53.7f, ang = seed % MathX.Tau, rr = r * (0.15f + (seed * 3f) % 0.85f);
                        float ph = ((t * 1.1f + i * 0.29f) % 1.6f) / 1.6f;
                        V2 at = c + V2.FromAngle(ang) * rr + new V2(Mathf.Sin(ph * 9f + i) * 3f, -40f * ph);
                        Circle(at, 1.3f, new Rgba(1f, 0.55f, 0.35f, a * (1f - ph) * 0.9f), 5);
                    }
            }
        }

        /// The standard upright: foot at `foot`, the cloth swaying with t; fold 0-1 droops it.
        void DrawStandard(ZoneKind k, V2 foot, float t, float fold, float a)
        {
            if (a <= 0f)
                return;
            Rgba cloth = k == ZoneKind.AegisStandard ? BnSteelCloth : k == ZoneKind.PilgrimsStandard ? BnIvory : BnCrimson;
            Rgba clothDark = k == ZoneKind.AegisStandard ? BnSteelDark : k == ZoneKind.PilgrimsStandard ? BnIvoryDark : BnCrimsonDark;
            Rgba trim = k == ZoneKind.AegisStandard ? BnSilver : Rgba.Gold;
            V2 top = foot + new V2(0f, -BnPole);
            Line(foot, top, 4.5f, BnOakDark.WithAlpha(a));  // the pole
            Line(foot + new V2(-0.6f, -2f), top + new V2(-0.6f, 0f), 2f, BnOak.WithAlpha(a));
            Line(top + new V2(-3f, 6f), top + new V2(BnClothW + 3f, 6f), 3f, BnIron.WithAlpha(a));  // the crossbar
            Tri3(top + new V2(0f, -12f), top + new V2(3.4f, -3f), top + new V2(-3.4f, -3f), trim.WithAlpha(a));  // the finial
            Tri3(top + new V2(3.4f, -3f), top, top + new V2(-3.4f, -3f), trim.WithAlpha(a));
            // the cloth: rows down from the crossbar, swaying more lower down, a swallowtail at the bottom
            const int N = 8;
            float L = BnClothL * (1f - 0.55f * fold), y0 = top.Y + 7f;
            V2 prevL = default(V2), prevR = default(V2);
            for (int i = 0; i <= N; i++)
            {
                float v = i / (float)N;
                float sway = Mathf.Sin(t * 2.6f + v * 3.6f) * 3.2f * v + Mathf.Sin(t * 4.1f + v * 6f) * 1.1f * v + fold * v * 5f;
                V2 l = new V2(top.X + 2f + sway, y0 + v * L), rr = new V2(top.X + 2f + BnClothW + sway * 1.15f, y0 + v * L);
                if (i > 0)
                    Quad(prevL, prevR, rr, l, cloth.Mix(clothDark, v).WithAlpha(a));
                prevL = l;
                prevR = rr;
            }
            float swayN = Mathf.Sin(t * 2.6f + 3.6f) * 3.2f + Mathf.Sin(t * 4.1f + 6f) * 1.1f + fold * 5f;
            V2 notch = new V2(top.X + 2f + BnClothW / 2f + swayN * 0.8f, y0 + L * 0.78f);
            V2 tailL = prevL + new V2(0f, L * 0.12f), tailR = prevR + new V2(0f, L * 0.12f);
            Tri3(prevL, notch, tailL, clothDark.WithAlpha(a));
            Tri3(prevR, tailR, notch, clothDark.WithAlpha(a));
            Tri3(prevL, prevR, notch, clothDark.WithAlpha(a));
            Line(new V2(top.X + 2f, y0), prevL, 1.2f, trim.WithAlpha(0.8f * a));
            Line(new V2(top.X + 2f + BnClothW, y0), prevR, 1.2f, trim.WithAlpha(0.8f * a));
            float swayE = Mathf.Sin(t * 2.6f + 1.44f) * 1.3f;
            DrawBannerEmblem(k, new V2(top.X + 2f + BnClothW / 2f + swayE, y0 + L * 0.38f), 0.95f * (1f - 0.3f * fold), a);
        }

        void DrawBannerEmblem(ZoneKind k, V2 c, float s, float a)
        {
            if (k == ZoneKind.AegisStandard)  // a silver kite shield with a blue cross
            {
                V2 tl = c + new V2(-8f, -9f) * s, tr = c + new V2(8f, -9f) * s, mr = c + new V2(8f, 0f) * s, ml = c + new V2(-8f, 0f) * s, tip = c + new V2(0f, 12f) * s;
                Quad(tl, tr, mr, ml, new Rgba(0.79f, 0.86f, 0.95f, a));
                Tri3(ml, mr, tip, new Rgba(0.79f, 0.86f, 0.95f, a));
                Line(c + new V2(0f, -9f) * s, tip, 1.6f, BnSteelCloth.WithAlpha(a));
                Line(c + new V2(-8f, -2f) * s, c + new V2(8f, -2f) * s, 1.6f, BnSteelCloth.WithAlpha(a));
            }
            else if (k == ZoneKind.PilgrimsStandard)  // a gold sun
            {
                for (int i = 0; i < 12; i++)
                {
                    V2 fw = V2.FromAngle(MathX.Tau * i / 12f), side = fw.Orthogonal();
                    Tri3(c + fw * 6f * s + side * 1.6f * s, c + fw * (12f - (i % 2) * 2.5f) * s, c + fw * 6f * s - side * 1.6f * s, Rgba.Gold.WithAlpha(a));
                }
                Circle(c, 6f * s, Rgba.Gold.WithAlpha(a), 14);
                Circle(c, 3.4f * s, new Rgba(1f, 0.95f, 0.81f, a), 10);
            }
            else  // crossed gold swords
            {
                for (int d = -1; d <= 1; d += 2)
                {
                    V2 up = V2.FromAngle(-MathX.Pi / 2f + d * 0.72f), side = up.Orthogonal();
                    Line(c - up * 7f * s, c + up * 12f * s, 2.4f, Rgba.Gold.WithAlpha(a));
                    Line(c - up * 6f * s + side * 4f * s, c - up * 6f * s - side * 4f * s, 1.8f, Rgba.Gold.WithAlpha(a));
                    Line(c - up * 7f * s, c - up * 11f * s, 2f, Rgba.Gold.WithAlpha(a));
                }
            }
        }

        // ---------------- the allies ----------------

        /// Under a player (DrawPlayerAuras): the War Standard's ring, the Pilgrim's glow and thread, the Aegis shield's bubble.
        void DrawBannerAura(Player p, float t)
        {
            if (game == null)
                return;
            foreach (var z in game.Zones.Values)
            {
                if (!Game.InBanner(z, p.Pos))
                    continue;
                if (z.Kind == ZoneKind.WarStandard)
                {
                    RingEllipse(p.Disp + new V2(0f, 10f), 18f, 7f, 2f, BnWar.WithAlpha(0.7f));
                    Ellipse(p.Disp + new V2(0f, 10f), 18f, 7f, BnWar.WithAlpha(0.15f));
                }
                else if (z.Kind == ZoneKind.PilgrimsStandard)
                {
                    Ellipse(p.Disp + new V2(0f, 10f), 20f, 8f, BnHoly.WithAlpha(0.18f));
                    V2 fin = z.Pos + new V2(0f, -BnPole - 8f), to = p.Disp + new V2(0f, -12f);
                    float pulse = 0.4f + 0.2f * Mathf.Sin(t * 5f + p.Id);
                    V2 mid = (fin + to) * 0.5f + new V2(0f, -30f);
                    const int S = 10;
                    V2 prev = fin;
                    for (int i = 1; i <= S; i++)  // the light thread, brighter toward the ally
                    {
                        float u = i / (float)S;
                        V2 q = fin * ((1f - u) * (1f - u)) + mid * (2f * u * (1f - u)) + to * (u * u);
                        Line(prev, q, 1.5f, BnHoly.WithAlpha(pulse * u));
                        prev = q;
                    }
                    for (int i = 0; i < 3; i++)  // small + rising
                    {
                        float ph = (t * 0.9f + i / 3f + p.Id * 0.2f) % 1f;
                        V2 at = p.Disp + new V2(-12f + i * 12f, -8f - ph * 22f);
                        Line(at + new V2(-3f, 0f), at + new V2(3f, 0f), 1.5f, BnHoly.WithAlpha(1f - ph));
                        Line(at + new V2(0f, -3f), at + new V2(0f, 3f), 1.5f, BnHoly.WithAlpha(1f - ph));
                    }
                }
            }
            DrawStdShield(p, t);
        }

        void DrawStdShield(Player p, float t)
        {
            float shown;
            bnShownShield.TryGetValue(p.Id, out shown);
            float flash;
            bnFlash.TryGetValue(p.Id, out flash);
            if (p.StdShieldHp < shown - 0.5f)
                flash = 0.18f;  // it just took a hit
            flash = Mathf.Max(0f, flash - Time.deltaTime);
            bnFlash[p.Id] = flash;
            bnShownShield[p.Id] = p.StdShieldHp;
            if (p.StdShieldHp <= 0.3f || p.StdShieldMax <= 0f)
                return;
            float k = Mathf.Clamp01(p.StdShieldHp / p.StdShieldMax), R = p.Def.BodyR + 9f;
            V2 c = p.Disp;
            Rgba col = BnSteel;
            for (int i = 0; i < 6; i++)
            {
                V2 a = c + V2.FromAngle(MathX.Tau * i / 6f + MathX.Pi / 6f) * R, b = c + V2.FromAngle(MathX.Tau * (i + 1) / 6f + MathX.Pi / 6f) * R;
                Tri3(c, a, b, col.WithAlpha(0.08f + 0.1f * k + (flash > 0f ? 0.25f : 0f)));
                Line(a, b, 1.6f, col.WithAlpha(0.35f + 0.55f * k));
                Line(c, a, 0.8f, col.WithAlpha(0.3f * k));
            }
            if (p.StdShieldHp < p.StdShieldMax - 0.5f)  // regenerating (or waiting to): a sweep round it
                Arc(c, R + 4f, 2f, new Rgba(0.86f, 0.94f, 1f, 0.8f), 10, t * 6f, 1.1f);
        }
    }
}

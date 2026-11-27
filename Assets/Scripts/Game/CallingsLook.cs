using UnityEngine;

namespace Arena
{
    /// The Callings' minions (Core/Callings.cs, Docs/Callings.md). The spider: a violet hunting spider over its shadow,
    /// eight jointed legs that step as it walks and tuck in a leap (the replicated Wind), a round abdomen with a pale
    /// hourglass, a cluster of glowing eyes, its owner's ring under it and a health bar once hurt; Spider Queen's
    /// spiderlings are the same at half size, with no ring. The fairy: a tiny pale body in a soft pink glow, four
    /// flickering wings and a trail of sparkles, brighter while it heals (Wind), with a faint thread to the ally it tends
    /// (LinkTo). The demon: a horned red imp-lord with bat wings and a crown of flame over its shadow, a fireball
    /// kindling in its claw toward its aim; berserk (Infernal Pact, Wind) it blazes orange in a ring of flame. The web:
    /// six spokes and two strands round an enemy a bite caught, fading.
    public partial class ArenaApp
    {
        static readonly Rgba SpiderBody = new Rgba(0.36f, 0.22f, 0.5f), SpiderBack = new Rgba(0.5f, 0.32f, 0.7f), SpiderLeg = new Rgba(0.24f, 0.14f, 0.34f),
            SpiderMark = new Rgba(0.95f, 0.85f, 1f), SpiderEye = new Rgba(0.8f, 1f, 0.95f), WebSilk = new Rgba(0.93f, 0.92f, 1f);
        static readonly Rgba FairyGlow = new Rgba(1f, 0.6f, 0.9f), FairyCore = new Rgba(1f, 0.97f, 1f), FairyWing = new Rgba(0.8f, 0.95f, 1f);
        static readonly Rgba DemonSkin = new Rgba(0.72f, 0.12f, 0.1f), DemonDark = new Rgba(0.32f, 0.04f, 0.06f), DemonHorn = new Rgba(0.95f, 0.88f, 0.75f),
            DemonFlame = new Rgba(1f, 0.55f, 0.12f), DemonEye = new Rgba(1f, 0.95f, 0.35f);

        void DrawCallingMinion(Minion m, float t)
        {
            if (m.Kind == MinionKind.Spider) DrawSpider(m, t);
            else if (m.Kind == MinionKind.Fairy) DrawFairy(m, t);
            else DrawDemon(m, t);
        }

        void DrawSpider(Minion m, float t)
        {
            float s = m.Scale, r = m.Def.Radius * s, leap = Mathf.Clamp01(m.Wind);
            V2 c = m.Disp;
            V2.Axes(m.Aim, out V2 f, out V2 side);
            Ellipse(c + new V2(2f, r * 0.7f), r * 1.5f, r * 0.6f, new Rgba(0f, 0f, 0f, 0.28f));
            if (m.Companion)
            {
                Player owner;
                var mark = game.Players.TryGetValue(m.Owner, out owner) ? owner.Def.Color : Rgba.White;
                Arc(c, r + 9f, 2f, mark.WithAlpha(0.7f), 24);
            }
            // Legs: four a side, each hip -> knee (up and out) -> foot; pairs step in turn, tucked in a leap.
            float walk = t * 11f + m.Id;
            for (int k = 0; k < 2; k++)
            {
                float sgn = k == 0 ? 1f : -1f;
                for (int i = 0; i < 4; i++)
                {
                    float fan = -0.9f + 0.6f * i;  // from forward to back
                    float step = Mathf.Sin(walk + i * 1.6f + k * 3.14f) * 0.18f * (1f - leap);
                    float reach = r * (1.9f - 0.8f * leap);
                    V2 hip = c + f * (r * (0.35f - 0.22f * i)) + side * (sgn * r * 0.35f);
                    V2 dir = (f * Mathf.Sin(-fan + step) + side * (sgn * Mathf.Cos(-fan + step))).Normalized();
                    V2 knee = hip + dir * (reach * 0.55f) - f * (r * 0.1f) + new V2(0f, -r * 0.35f);
                    V2 foot = hip + dir * reach + f * (r * 0.25f * (i < 2 ? 1f : -1f));
                    Line(hip, knee, 2.2f * s + 0.6f, SpiderLeg);
                    Line(knee, foot, 1.6f * s + 0.5f, SpiderLeg);
                }
            }
            V2 back = c - f * (r * 0.55f);
            Circle(back, r * 0.85f, SpiderBody, 18);
            Circle(back - f * (r * 0.08f) + new V2(-r * 0.15f, -r * 0.2f), r * 0.55f, SpiderBack, 14);
            // the hourglass
            V2 hg = back - f * (r * 0.05f);
            Tri2(hg, hg + f * (r * 0.42f) + side * (r * 0.22f), hg + f * (r * 0.42f) - side * (r * 0.22f), SpiderMark.WithAlpha(0.85f));
            Tri2(hg, hg - f * (r * 0.42f) + side * (r * 0.22f), hg - f * (r * 0.42f) - side * (r * 0.22f), SpiderMark.WithAlpha(0.85f));
            V2 head = c + f * (r * 0.45f);
            Circle(head, r * 0.55f, SpiderBody, 14);
            // fangs and eyes
            Line(head + f * (r * 0.4f) + side * (r * 0.18f), head + f * (r * 0.75f) + side * (r * 0.08f), 1.4f * s + 0.4f, SpiderMark.WithAlpha(0.8f));
            Line(head + f * (r * 0.4f) - side * (r * 0.18f), head + f * (r * 0.75f) - side * (r * 0.08f), 1.4f * s + 0.4f, SpiderMark.WithAlpha(0.8f));
            for (int i = -1; i <= 1; i += 2)
            {
                Circle(head + f * (r * 0.25f) + side * (i * r * 0.2f), r * 0.13f + 0.5f, SpiderEye, 8);
                Circle(head + f * (r * 0.12f) + side * (i * r * 0.36f), r * 0.09f + 0.4f, SpiderEye.WithAlpha(0.8f), 6);
            }
            if (m.Companion && m.GuardFrac < 0.999f)
                Bar(c + new V2(-14f, -r - 12f), 28f, 3.5f, Mathf.Clamp01(m.GuardFrac), m.Def.Color);
        }

        void DrawFairy(Minion m, float t)
        {
            float heal = Mathf.Clamp01(m.Wind), bob = Mathf.Sin(t * 5f + m.Id) * 3f;
            V2 c = m.Disp + new V2(0f, bob);
            Player tend;
            if (heal > 0.05f && m.LinkTo != 0 && game.Players.TryGetValue(m.LinkTo, out tend))
            {
                V2 to = tend.Disp - c;
                int dots = Mathf.Clamp((int)(to.Length / 9f), 2, 16);
                for (int i = 1; i < dots; i++)
                {
                    float u = (i + (t * 2f) % 1f) / dots;
                    if (u < 1f)
                        Circle(c + to * u, 1.4f, FairyGlow.WithAlpha(0.5f * heal * (1f - u * 0.5f)), 6);
                }
            }
            Ellipse(m.Disp + new V2(0f, 14f), 6f, 2.2f, new Rgba(0f, 0f, 0f, 0.18f));
            // sparkle trail: motes drifting down behind it
            for (int i = 0; i < 6; i++)
            {
                float u = (t * 0.9f + i / 6f) % 1f;
                V2 at = c + new V2(Mathf.Sin(i * 2.3f + t * 1.7f) * 9f * u, 4f + u * 18f);
                Circle(at, 1.6f * (1f - u) + 0.4f, FairyCore.WithAlpha(0.8f * (1f - u)), 6);
            }
            Circle(c, 15f + 4f * heal, FairyGlow.WithAlpha(0.12f + 0.12f * heal), 20);
            Circle(c, 9f + 2f * heal, FairyGlow.WithAlpha(0.22f + 0.18f * heal), 16);
            // wings: two pairs, flickering
            float flap = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * 26f + m.Id));
            for (int k = -1; k <= 1; k += 2)
            {
                Ellipse(c + new V2(k * 6f * flap, -3f), 6.5f * flap, 3.6f, FairyWing.WithAlpha(0.55f));
                Ellipse(c + new V2(k * 4.5f * flap, 3f), 4.2f * flap, 2.6f, FairyWing.WithAlpha(0.45f));
            }
            Circle(c, 3.4f, FairyCore, 12);
            Circle(c + new V2(0f, -4.2f), 2.2f, FairyCore, 10);
            if (heal > 0.05f)
            {
                float tw = (t * 3f) % 1f;
                Arc(c, 6f + 14f * tw, 1.2f, FairyGlow.WithAlpha(0.6f * heal * (1f - tw)), 20);
            }
        }

        void DrawDemon(Minion m, float t)
        {
            float rage = Mathf.Clamp01(m.Wind), r = m.Def.Radius * (1f + 0.12f * rage);
            V2 c = m.Disp;
            V2 f = V2.FromAngle(m.Aim);
            Ellipse(c + new V2(0f, r + 8f), r * 1.1f, r * 0.35f, new Rgba(0f, 0f, 0f, 0.25f));
            if (rage > 0.05f)
                for (int i = 0; i < 10; i++)
                {
                    float a = MathX.Tau * i / 10f + t * 2f;
                    float h = 5f + 4f * Mathf.Abs(Mathf.Sin(t * 9f + i * 1.7f));
                    V2 at = c + V2.FromAngle(a) * (r + 10f);
                    Line(at, at + V2.FromAngle(a) * h, 3f, DemonFlame.WithAlpha(0.7f * rage));
                }
            // bat wings, beating
            float beat = Mathf.Sin(t * 7f + m.Id) * 0.25f;
            for (int k = -1; k <= 1; k += 2)
            {
                V2 root = c + new V2(k * r * 0.5f, -r * 0.2f);
                V2 tip = root + new V2(k * r * 1.7f, -r * (0.9f + beat));
                V2 low = root + new V2(k * r * 1.5f, r * (0.3f - beat));
                Tri2(root, tip, root + new V2(k * r * 1.1f, r * 0.1f), DemonDark.WithAlpha(0.9f));
                Tri2(root + new V2(k * r * 1.1f, r * 0.1f), tip, low, DemonDark.WithAlpha(0.8f));
                Line(root, tip, 1.6f, DemonSkin);
            }
            Circle(c, r, DemonSkin.Mix(DemonFlame, 0.35f * rage), 18);
            Circle(c + new V2(-r * 0.25f, -r * 0.3f), r * 0.5f, DemonSkin.Mix(Rgba.White, 0.15f).WithAlpha(0.6f), 12);
            // horns
            for (int k = -1; k <= 1; k += 2)
            {
                V2 b0 = c + new V2(k * r * 0.45f, -r * 0.75f);
                Tri2(b0 - new V2(r * 0.18f, 0f), b0 + new V2(r * 0.18f, 0f), b0 + new V2(k * r * 0.45f, -r * 0.75f), DemonHorn);
            }
            // flame crown
            for (int i = 0; i < 3; i++)
            {
                float x = (i - 1) * r * 0.35f, h = r * (0.5f + 0.25f * Mathf.Abs(Mathf.Sin(t * 10f + i * 2.1f))) * (1f + rage * 0.6f);
                V2 b0 = c + new V2(x, -r * 0.95f);
                Tri2(b0 - new V2(r * 0.16f, 0f), b0 + new V2(r * 0.16f, 0f), b0 + new V2(0f, -h), DemonFlame.WithAlpha(0.9f));
            }
            // eyes, looking where it throws
            V2 look = f * (r * 0.12f);
            Circle(c + new V2(-r * 0.32f, -r * 0.1f) + look, r * 0.16f, DemonEye, 8);
            Circle(c + new V2(r * 0.32f, -r * 0.1f) + look, r * 0.16f, DemonEye, 8);
            // the fireball kindling in its claw
            float kindle = 0.6f + 0.4f * Mathf.Sin(t * 12f + m.Id);
            V2 claw = c + f * (r + 4f);
            Circle(claw, 6f + 2f * kindle, DemonFlame.WithAlpha(0.35f), 12);
            Circle(claw, 3.5f + 1.2f * kindle, DemonEye, 10);
        }

        /// A filled triangle in one colour.
        void Tri2(V2 a, V2 b, V2 c, Rgba col)
        {
            SetColor(col);
            Tri(a, b, c);
        }

        /// A spider's bite webbing an enemy: six spokes and two strands round it, fading.
        void DrawSpiderWebFx(Effect fx, float f)
        {
            float r = fx.Radius * (0.8f + 0.2f * f), a = 1f - f * f;
            float rot = fx.Pos.X * 0.05f;
            for (int i = 0; i < 6; i++)
                Line(fx.Pos, fx.Pos + V2.FromAngle(rot + MathX.Tau * i / 6f) * r, 1.2f, WebSilk.WithAlpha(0.8f * a));
            for (int ring = 1; ring <= 2; ring++)
            {
                float rr = r * ring / 2.4f;
                for (int i = 0; i < 6; i++)
                {
                    V2 p0 = fx.Pos + V2.FromAngle(rot + MathX.Tau * i / 6f) * rr, p1 = fx.Pos + V2.FromAngle(rot + MathX.Tau * (i + 1) / 6f) * rr;
                    Line(p0, p1, 1f, WebSilk.WithAlpha(0.65f * a));
                }
            }
        }
    }
}

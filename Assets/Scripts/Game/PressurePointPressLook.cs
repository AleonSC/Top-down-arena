using UnityEngine;

namespace Arena
{
    /// Pressure Point Press's look (Core/PressurePointPress.cs; from its mockup, the artifact "Pressure Point Press"). Drawing only,
    /// from FxKind.PressurePoint (Radius = Game.PppFx*), so every game draws the same:
    ///   - Mark: a jade ring sweeping out to the reach with turning ticks, over a faint jade wash;
    ///   - Target: a pulsing jade reticle on each marked enemy until it's pressed;
    ///   - Dash: a tapered jade streak from where you were to where you landed, with a fading afterimage where you left;
    ///   - Press: a gold burst with six spokes and a jade needle point on the enemy;
    ///   - Wisp: a jade orb arcing from the pressed enemy to the teammate it heals;
    ///   - Seal: a jade ring and seal star where you cast, and a fading thread to each pressed enemy.
    /// The stun stars and poison bubbles are the enemies' own status looks. With Visual effects off: no glow or afterimages.
    public partial class ArenaApp
    {
        static readonly Rgba PppInk = new Rgba(0.05f, 0.1f, 0.2f);

        void DrawPressurePointFx(Effect fx)
        {
            float age = fx.T, k = fx.Dur > 0f ? Mathf.Clamp01(age / fx.Dur) : 1f;
            V2 a = fx.Pos, b = fx.Pos2;
            Rgba jade = Game.PppJade, gold = Game.PppGold;
            switch (Mathf.RoundToInt(fx.Radius))
            {
                case Game.PppFxMark:
                {
                    float u = Mathf.Clamp01(age / Game.PppMark), e = 1f - (1f - u) * (1f - u);
                    float r = Game.PppRange * e, fade = 1f - Mathf.Clamp01((age - Game.PppMark) / 0.3f);
                    if (fxOn)
                        Circle(a, r, jade.WithAlpha(0.07f * fade * (1f - u * 0.5f)), 48);
                    Arc(a, r, 3f, jade.WithAlpha(0.9f * fade), 64);
                    Arc(a, r * 0.94f, 1.2f, jade.WithAlpha(0.4f * fade), 64);
                    for (int i = 0; i < 8; i++)
                    {
                        V2 d = V2.FromAngle(i * MathX.Tau / 8f + age * 1.5f);
                        Line(a + d * (r - 10f), a + d * (r + 10f), 2f, jade.WithAlpha(fade));
                    }
                    break;
                }
                case Game.PppFxTarget:
                {
                    float pop = Mathf.Clamp01(age / 0.15f), pulse = 1f + Mathf.Sin(age * 10f) * 0.12f;
                    float r = 22f * pulse * (1.6f - 0.6f * pop);
                    Arc(a, r, 1.6f, jade.WithAlpha(0.9f * pop), 24);
                    for (int i = 0; i < 4; i++)
                    {
                        V2 d = V2.FromAngle(i * MathX.Tau / 4f + age * 2f);
                        Line(a + d * (r - 4f), a + d * (r + 8f), 1.8f, jade.WithAlpha(pop));
                    }
                    break;
                }
                case Game.PppFxDash:
                {
                    float fade = 1f - k;
                    V2 d = b - a;
                    if (d.IsZero)
                        break;
                    V2 side = new V2(-d.Y, d.X).Normalized();
                    Quad(a + side * 1f, a - side * 1f, b - side * 5f * fade, b + side * 5f * fade, jade.WithAlpha(0.85f * fade));
                    Line(a, b, 1.2f, Rgba.White.WithAlpha(0.6f * fade * fade));
                    if (fxOn)
                    {
                        Circle(a, 13f, PppInk.WithAlpha(0.5f * fade), 18);
                        Arc(a, 13f, 2f, jade.WithAlpha(0.6f * fade), 18);
                    }
                    break;
                }
                case Game.PppFxPress:
                {
                    float fade = 1f - k, r = 10f + 34f * (1f - (1f - k) * (1f - k));
                    if (fxOn)
                        GlowDisc(a, 60f, gold, 0.5f * fade * fade);
                    Arc(a, r, 3f, gold.WithAlpha(fade), 28);
                    for (int i = 0; i < 6; i++)
                    {
                        V2 d = V2.FromAngle(i * MathX.Tau / 6f + 0.26f);
                        Line(a + d * (6f + 6f * k), a + d * (14f + 30f * k), 2f, gold.WithAlpha(fade));
                    }
                    Circle(a, 4f * (1f - 0.5f * k), jade.WithAlpha(fade), 10);
                    if (!(b - a).IsZero)  // the strike: a short gold slash from your side through the point
                    {
                        V2 d = (a - b).Normalized();
                        float s = 1f - Mathf.Clamp01(age / 0.15f);
                        Line(a - d * 22f, a + d * 22f, 3f * s, gold.WithAlpha(s));
                    }
                    break;
                }
                case Game.PppFxWisp:
                {
                    for (int j = 0; j < 4; j++)
                    {
                        float u = Mathf.Clamp01(k - j * 0.06f), e = u * u;
                        V2 at = a + (b - a) * e + new V2(0f, -Mathf.Sin(u * Mathf.PI) * 60f);
                        float s = 1f - j * 0.22f;
                        if (j == 0 && fxOn)
                            GlowDisc(at, 22f, jade, 0.5f);
                        Circle(at, 5f * s, jade.WithAlpha(0.9f * s), 12);
                    }
                    if (k > 0.85f)
                        Arc(b, 16f + 20f * (k - 0.85f) / 0.15f, 2f, jade.WithAlpha((1f - k) / 0.15f), 20);
                    break;
                }
                case Game.PppFxSeal:
                {
                    float fade = 1f - k, r = 20f + 110f * (1f - (1f - k) * (1f - k));
                    if (fxOn)
                        GlowDisc(a, 120f, jade, 0.45f * fade);
                    Arc(a, r, 3f, jade.WithAlpha(fade), 40);
                    Arc(a, r * 0.6f, 1.5f, jade.WithAlpha(0.6f * fade), 32);
                    Star4(a, 26f * (0.6f + 0.4f * fade), jade.WithAlpha(fade));
                    break;
                }
                case Game.PppFxSealLine:
                {
                    float fade = 1f - k;
                    Line(a, b, 2f * fade + 0.5f, jade.WithAlpha(0.75f * fade));
                    Arc(b, 14f + 10f * k, 2f, jade.WithAlpha(fade), 18);
                    break;
                }
            }
        }
    }
}

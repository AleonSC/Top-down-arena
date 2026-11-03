using UnityEngine;

namespace Arena
{
    /// Chain Light in style A ("Radiance", Docs/Mockups/chain-light.html, the owner's pick), ported from the mockup's
    /// numbers: a soft fan of light blooming out of your hand (0.12 s) and fading (0.33 s), bright at your hand and gone
    /// at the reach, with soft edges, flickering god-rays and motes; a soft beam to each enemy it hits, with a halo and a
    /// ring. All of it is light, so it's drawn in the additive glow pass (DrawChainLightGlow, from DrawGlowPass), which
    /// the lobby's loadout preview runs too (clipped to its window). Drawing only, with Visual effects on; off, the cone
    /// (DrawLightConeFx) and its bolts (DrawBoltFx) draw as before. Only Chain Light's effects: the cone is its own
    /// FxKind.LightCone, and its bolts are the only ones in the light's warm colour (Defs.HolyShot; the other bolts,
    /// Chain Lightning's, the Storm Hammer's, the bosses', are other colours).
    public partial class ArenaApp
    {
        const float RadianceBloom = 0.12f, RadianceFade = 0.33f;  // the fan: blooming out, then fading
        const float RadianceBeam = 0.34f;                         // each beam and halo

        static readonly Rgba ChainLightGlowColor = Defs.BulletColor(Defs.HolyShot);

        static bool IsChainLightBolt(Effect fx) => fx.Kind == FxKind.Bolt && fx.Color.R == ChainLightGlowColor.R && fx.Color.G == ChainLightGlowColor.G
            && fx.Color.B == ChainLightGlowColor.B;

        /// DrawLightConeFx / DrawBoltFx: with Visual effects on, Chain Light's cone and bolts are drawn in the glow pass
        /// instead (true: draw nothing here). Also lets them last as long as the radiance does.
        bool ChainLightDrawnAsRadiance(Effect fx)
        {
            if (!fxOn)
                return false;
            if (fx.Kind == FxKind.LightCone)
            {
                if (fx.Dur < RadianceBloom + RadianceFade)
                    fx.Dur = RadianceBloom + RadianceFade;  // (front end only: the effect is drawing, nothing reads it)
                return true;
            }
            if (IsChainLightBolt(fx))
            {
                if (fx.Dur < RadianceBeam)
                    fx.Dur = RadianceBeam;
                return true;
            }
            return false;
        }

        /// The glow pass: every Chain Light cone's fan and every beam from it.
        void DrawChainLightGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.LightCone)
                    DrawRadianceFan(fx);
                else if (IsChainLightBolt(fx))
                    DrawRadianceBeam(fx);
            }
        }

        void DrawRadianceFan(Effect fx)
        {
            V2 o = fx.Pos, to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius, T = fx.T;
            if (reach < 1f)
                return;
            float k = EaseOut3(Mathf.Clamp01(T / RadianceBloom));
            float fade = T < RadianceBloom ? 1f : 1f - Mathf.Clamp01((T - RadianceBloom) / RadianceFade);
            if (fade <= 0f)
                return;
            float r = reach * (0.35f + 0.65f * k);
            var col = fx.Color;
            // The fan: across it, white at your hand -> the light at a quarter of the way -> nothing at its edge; along its
            // arc, full inside 80% of its half angle, fading to nothing at 115% (the mockup's three stacked wedges).
            float edge = half * 1.15f, inner = half * 0.8f;
            int n = Mathf.Max(8, Mathf.CeilToInt(2f * edge * 18f));
            float[] rr = { 0f, 0.25f, 1f };
            for (int i = 0; i < n; i++)
            {
                float a0 = aim - edge + 2f * edge * i / n, a1 = aim - edge + 2f * edge * (i + 1) / n;
                float m0 = FanEdge(a0 - aim, inner, edge), m1 = FanEdge(a1 - aim, inner, edge);
                if (m0 <= 0f && m1 <= 0f)
                    continue;
                V2 d0 = V2.FromAngle(a0), d1 = V2.FromAngle(a1);
                for (int j = 0; j < 2; j++)
                {
                    Rgba c00 = FanColor(col, j, fade * m0), c01 = FanColor(col, j, fade * m1), c10 = FanColor(col, j + 1, fade * m0), c11 = FanColor(col, j + 1, fade * m1);
                    V2 p00 = o + d0 * (r * rr[j]), p01 = o + d1 * (r * rr[j]), p10 = o + d0 * (r * rr[j + 1]), p11 = o + d1 * (r * rr[j + 1]);
                    VTri(p00, c00, p10, c10, p11, c11);
                    VTri(p00, c00, p11, c11, p01, c01);
                }
            }
            // God-rays: nine thin rays, white at your hand fading out, each flickering.
            uint s = (uint)(fx.Pos.X * 7f + fx.Pos.Y * 13f + aim * 100f);
            for (int i = 0; i < 9; i++)
            {
                float a = aim + (Rand01(ref s) - 0.5f) * 2f * half * 0.9f, len = r * (0.55f + 0.4f * Rand01(ref s));
                float fl = 0.5f + 0.5f * Mathf.Sin(T * 40f + i * 2.1f);
                SoftLineFade(o, o + V2.FromAngle(a) * len, 3f, Rgba.White.WithAlpha(0.35f * fade * fl), col.WithAlpha(0f));
            }
            // Motes drifting out through the fan.
            for (int i = 0; i < 16; i++)
            {
                float a = aim + (Rand01(ref s) - 0.5f) * 2f * half, d = r * (0.2f + 0.7f * Rand01(ref s)) + T * 60f;
                Square(o + V2.FromAngle(a) * d, 2f, col.WithAlpha(0.8f * fade));
            }
        }

        /// How much of the fan shows at `off` radians from its middle: all of it inside `inner`, none past `edge`.
        static float FanEdge(float off, float inner, float edge)
        {
            float a = Mathf.Abs(off);
            return a <= inner ? 1f : a >= edge ? 0f : 1f - (a - inner) / (edge - inner);
        }

        /// The fan's colour at ring j (0: your hand, white at 0.5; 1: a quarter out, the light at 0.3; 2: its edge, clear).
        static Rgba FanColor(Rgba col, int j, float a) => j == 0 ? Rgba.White.WithAlpha(0.5f * a) : j == 1 ? col.WithAlpha(0.3f * a) : col.WithAlpha(0f);

        /// A soft beam from your hand to an enemy it hit (fainter at your end), a white core, a halo and a ring on it.
        void DrawRadianceBeam(Effect fx)
        {
            float bf = 1f - Mathf.Clamp01(fx.T / RadianceBeam);
            if (bf <= 0f)
                return;
            var col = fx.Color;
            V2 a = fx.Pos, b = fx.Pos2;
            SoftLineFade(a, b, 9f, col.WithAlpha(0.15f * bf), col.WithAlpha(0.55f * bf));
            Line(a, b, 1.5f, Rgba.White.WithAlpha(0.85f * bf));
            GlowDisc(b, 34f, Rgba.White, 0.7f * bf);
            Arc(b, 14f + 22f * (1f - bf), 2f, col.WithAlpha(0.7f * bf), 24);
        }
    }
}

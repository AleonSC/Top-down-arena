using UnityEngine;

namespace Arena
{
    /// The Healer Soldier's Mend Pulse with Visual effects on (Docs/Mockups/mend-pulse.html, look B, Heartbeat), drawing only.
    /// Abilities.cs sends the pulse as a green Ring out to its range; this recognises it and draws a double beat instead: a soft
    /// fill that swells to the range, two rings a beat apart, and a small heart and ring on each ally as the wave reaches them
    /// (the heal itself is instant). With Visual effects off the plain ring draws as before.
    public partial class ArenaApp
    {
        static readonly Rgba MendGreen = new Rgba(0.4f, 1f, 0.55f);
        const float MendDur = 0.6f, MendBeat = 0.1f, MendPop = 0.3f;

        /// A Mend Pulse ring: the pulse's colour, 0.6 s, and at least its base range (the Soul Link ring sharing the colour is 90).
        bool IsMendPulse(Effect fx)
        {
            if (!fxOn || fx.Kind != FxKind.Ring || fx.Radius < 240f || fx.Dur < MendDur - 0.01f || fx.Dur > MendDur + 0.01f)
                return false;
            return Mathf.Abs(fx.Color.R - MendGreen.R) < 0.01f && Mathf.Abs(fx.Color.G - MendGreen.G) < 0.01f && Mathf.Abs(fx.Color.B - MendGreen.B) < 0.01f;
        }

        void DrawMendPulseFx(Effect fx, float f)
        {
            float R = fx.Radius, t = f * MendDur;
            float r1 = R * EaseOut3(f);

            // Fill: brighter towards the rim, fading out over the pulse.
            float fade = 1f - f;
            const int Bands = 6;
            for (int i = 0; i < Bands; i++)
            {
                float u = (i + 1f) / Bands;
                Arc(fx.Pos, r1 * (i + 0.5f) / Bands, r1 / Bands + 1f, MendGreen.WithAlpha(0.22f * u * u * fade), 40);
            }

            // Two beats: a thick ring, then a thinner one a tenth of a second behind.
            Arc(fx.Pos, r1, 7f, MendGreen.WithAlpha(0.25f * fade), 64);
            Arc(fx.Pos, r1, 4f, MendGreen.WithAlpha(fade), 64);
            float k2 = (t - MendBeat) / (MendDur - MendBeat);
            if (k2 > 0f)
                Arc(fx.Pos, R * 0.94f * EaseOut3(k2), 2.5f, MendGreen.WithAlpha(1f - k2), 64);

            // Allies in range: a heart and ring as the first ring passes.
            foreach (var p in game.Players.Values)
            {
                float d = p.Disp.DistanceTo(fx.Pos);
                if (d >= R)
                    continue;
                float reach = 1f - Mathf.Pow(1f - d / R, 1f / 3f);  // when the first ring gets to them
                float age = (f - reach) * MendDur;
                if (age < 0f || age > MendPop)
                    continue;
                float k = age / MendPop;
                DrawMendHeart(p.Disp, k);
            }
        }

        /// A heart rising off an ally, beating once, with a ring round them.
        void DrawMendHeart(V2 at, float k)
        {
            float a = 1f - k;
            float s = 1f + 0.35f * Mathf.Sin(k * MathX.Tau);
            V2 c = at + new V2(0f, -30f - 8f * k);
            Rgba col = MendGreen.WithAlpha(a);
            Circle(c + new V2(-4.5f * s, -2f * s), 5f * s, col, 10);
            Circle(c + new V2(4.5f * s, -2f * s), 5f * s, col, 10);
            SetColor(col);
            Tri(c + new V2(-9.2f * s, 0.5f * s), c + new V2(9.2f * s, 0.5f * s), c + new V2(0f, 11f * s));
            Arc(at, 22f + 10f * k, 2f, MendGreen.WithAlpha(0.8f * a), 24);
        }
    }
}

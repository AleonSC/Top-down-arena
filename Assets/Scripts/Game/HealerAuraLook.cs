using UnityEngine;

namespace Arena
{
    /// The Healer's aura (Game.AuraRange, 170 px): drawn under the player. AuraStyle picks the look while the owner
    /// chooses one (0: the old disc and ring; 1 Bloom, 2 Pulse, 3 Sigil).
    public partial class ArenaApp
    {
        static readonly Rgba AuraGreen = new Rgba(0.3f, 1f, 0.5f);
        public static int AuraStyle = 0;

        void DrawHealerAura(Player p, float t)
        {
            float R = p.AuraRange;
            V2 c = p.Disp;
            switch (AuraStyle)
            {
                case 1: AuraBloom(p, c, R, t); break;
                case 2: AuraPulse(p, c, R, t); break;
                case 3: AuraSigil(p, c, R, t); break;
                default:
                    Circle(c, R, AuraGreen.WithAlpha(0.03f), 64);
                    Arc(c, R, 2f, AuraGreen.WithAlpha(0.18f), 64);
                    break;
            }
            if (AuraStyle != 0)
                AuraLinks(p, c, R, t);
        }

        /// Bloom: a soft edge that brightens towards the rim, a slow dashed ring, and small crosses drifting up.
        void AuraBloom(Player p, V2 c, float R, float t)
        {
            Circle(c, R, AuraGreen.WithAlpha(0.025f), 64);
            for (int i = 0; i < 4; i++)  // the rim glow: thin bands brighter towards the edge
                Arc(c, R - 3f - i * 6f, 6f, AuraGreen.WithAlpha(0.06f - i * 0.013f), 64);
            Arc(c, R, 2f, AuraGreen.WithAlpha(0.35f), 64);
            const int dashes = 24;  // a dashed inner ring turning slowly
            float rot = t * 0.25f;
            for (int i = 0; i < dashes; i++)
                Arc(c, R - 12f, 1.5f, AuraGreen.WithAlpha(0.22f), 4, rot + MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
            for (int k = 0; k < 7; k++)  // crosses rising and fading inside the aura
            {
                float f = MathX.Fmod(t * 0.35f + k * 0.143f + p.Id * 0.21f, 1f);
                float ang = k * 2.39f + p.Id;
                float d = R * (0.25f + 0.6f * MathX.Fmod(k * 0.618f, 1f));
                V2 at = c + V2.FromAngle(ang) * d + new V2(0f, -f * 26f);
                AuraCross(at, 3.5f, AuraGreen.WithAlpha(0.55f * Mathf.Sin(f * Mathf.PI)));
            }
        }

        /// Pulse: a wave sweeping out from the healer to the rim once a second, in time with the healing.
        void AuraPulse(Player p, V2 c, float R, float t)
        {
            Circle(c, R, AuraGreen.WithAlpha(0.025f), 64);
            Arc(c, R, 2f, AuraGreen.WithAlpha(0.3f), 64);
            for (int w = 0; w < 2; w++)
            {
                float f = MathX.Fmod(t * 0.8f + w * 0.5f, 1f);
                float r = R * (0.15f + 0.85f * Mathf.Sqrt(f));
                float a = (1f - f) * (1f - f);
                Arc(c, r, 3f + 5f * (1f - f), AuraGreen.WithAlpha(0.22f * a), 64);
                Arc(c, r - 6f, 6f, AuraGreen.WithAlpha(0.05f * a), 64);
            }
            float rim = 0.5f + 0.5f * Mathf.Sin(t * 0.8f * MathX.Tau);  // the rim brightens as each wave lands
            Arc(c, R + 2f, 1.5f, AuraGreen.WithAlpha(0.12f * rim), 64);
        }

        /// Sigil: two rings turning opposite ways with tick marks, and four leaf points on the rim.
        void AuraSigil(Player p, V2 c, float R, float t)
        {
            Circle(c, R, AuraGreen.WithAlpha(0.03f), 64);
            Arc(c, R, 2f, AuraGreen.WithAlpha(0.32f), 64);
            Arc(c, R - 18f, 1f, AuraGreen.WithAlpha(0.18f), 64);
            float a0 = t * 0.18f, a1 = -t * 0.3f;
            for (int i = 0; i < 36; i++)  // ticks on the outer ring, every third one longer
            {
                V2 d = V2.FromAngle(a0 + MathX.Tau * i / 36);
                float len = i % 3 == 0 ? 8f : 4f;
                Line(c + d * (R - len), c + d * R, 1.2f, AuraGreen.WithAlpha(i % 3 == 0 ? 0.35f : 0.18f));
            }
            for (int i = 0; i < 4; i++)  // leaf points on the rim, turning with the ticks
            {
                V2 d = V2.FromAngle(a0 + MathX.Tau * i / 4 + 0.1f), n = d.Orthogonal();
                V2 tip = c + d * (R + 7f), b = c + d * (R - 7f), m = c + d * R;
                SetColor(AuraGreen.WithAlpha(0.5f));
                Quad(tip, m + n * 3.5f, b, m - n * 3.5f);
            }
            for (int i = 0; i < 12; i++)  // dots on the inner ring, turning the other way
                Circle(c + V2.FromAngle(a1 + MathX.Tau * i / 12) * (R - 18f), 1.8f, AuraGreen.WithAlpha(0.4f), 6);
        }

        /// Allies inside the aura: a soft tether from the healer and a small cross pulsing over them.
        void AuraLinks(Player p, V2 c, float R, float t)
        {
            foreach (var q in game.Players.Values)
            {
                if (q == p || q.Downed || !q.Disp.CloserThan(c, R))
                    continue;
                V2 d = (q.Disp - c).Normalized();
                V2 a = c + d * (p.Def.BodyR + 4f), b = q.Disp - d * (q.Def.BodyR + 4f);
                Line(a, b, 3f, AuraGreen.WithAlpha(0.06f));
                Line(a, b, 1f, AuraGreen.WithAlpha(0.22f));
                float f = MathX.Fmod(t * 0.8f + q.Id * 0.3f, 1f);  // a bead running down the tether to them
                Circle(a + (b - a) * f, 2.2f, AuraGreen.WithAlpha(0.7f * Mathf.Sin(f * Mathf.PI)), 6);
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + q.Id);
                Arc(q.Disp, q.Def.BodyR + 6f, 1.5f, AuraGreen.WithAlpha(0.18f + 0.15f * pulse), 32);
                AuraCross(q.Disp + new V2(0f, -q.Def.BodyR - 12f - 2f * pulse), 3.5f, AuraGreen.WithAlpha(0.6f + 0.3f * pulse));
            }
        }

        void AuraCross(V2 at, float s, Rgba col)
        {
            Line(at + new V2(-s, 0f), at + new V2(s, 0f), s * 0.6f, col);
            Line(at + new V2(0f, -s), at + new V2(0f, s), s * 0.6f, col);
        }
    }
}

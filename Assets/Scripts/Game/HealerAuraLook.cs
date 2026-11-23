using UnityEngine;

namespace Arena
{
    /// The Healer's aura (Game.AuraRange, 170 px), drawn under the player. The fill is brightest at the Healer and
    /// fades out to the rim, as the healing does (Game.AuraHealAt); a rim line and a slow dashed ring mark the edge,
    /// green crosses drift up inside, and each ally in it gets a tether that's brighter the closer they stand.
    public partial class ArenaApp
    {
        static readonly Rgba AuraGreen = new Rgba(0.3f, 1f, 0.5f);
        const int AuraBands = 7;  // discs stacked from the rim in: the fill gets brighter towards the middle

        void DrawHealerAura(Player p, float t)
        {
            float R = p.AuraRange;
            V2 c = p.Disp;
            for (int i = 0; i < AuraBands; i++)  // the fill: each disc smaller and adding a little, so it fades outward
                Circle(c, R * (1f - i / (float)AuraBands), AuraGreen.WithAlpha(i == 0 ? 0.018f : 0.014f), 64);
            Arc(c, R, 2f, AuraGreen.WithAlpha(0.3f), 64);
            Arc(c, R - 4f, 6f, AuraGreen.WithAlpha(0.035f), 64);  // a soft lip just inside the rim line
            const int dashes = 24;  // a dashed ring turning slowly just inside the rim
            float rot = t * 0.25f;
            for (int i = 0; i < dashes; i++)
                Arc(c, R - 12f, 1.5f, AuraGreen.WithAlpha(0.16f), 4, rot + MathX.Tau * i / dashes, MathX.Tau / dashes * 0.5f);
            for (int k = 0; k < 8; k++)  // crosses rising and fading inside, more of them (and brighter) near the middle
            {
                float f = MathX.Fmod(t * 0.35f + k * 0.125f + p.Id * 0.21f, 1f);
                float out01 = MathX.Fmod(k * 0.618f, 1f);
                float d = R * (0.15f + 0.7f * out01 * out01);
                V2 at = c + V2.FromAngle(k * 2.39f + p.Id) * d + new V2(0f, -f * 26f);
                AuraCross(at, 3.5f, AuraGreen.WithAlpha((0.65f - 0.35f * out01) * Mathf.Sin(f * Mathf.PI)));
            }
            foreach (var q in game.Players.Values)
                if (q != p && !q.Downed)
                    AuraLink(p, q, c, t);
        }

        /// An ally inside the aura: a tether from the Healer (brighter and faster the more it heals them) and a +
        /// pulsing over them.
        void AuraLink(Player p, Player q, V2 c, float t)
        {
            float dist = (q.Disp - c).Length;
            if (dist >= p.AuraRange)
                return;
            float k = 1f - dist / p.AuraRange;  // 1 next to the Healer, 0 at the rim: where on Game.AuraHealAt's line they are
            V2 d = (q.Disp - c).Normalized();
            V2 a = c + d * (p.Def.BodyR + 4f), b = q.Disp - d * (q.Def.BodyR + 4f);
            Line(a, b, 3f, AuraGreen.WithAlpha(0.03f + 0.07f * k));
            Line(a, b, 1f, AuraGreen.WithAlpha(0.12f + 0.25f * k));
            float f = MathX.Fmod(t * (0.6f + 0.8f * k) + q.Id * 0.3f, 1f);  // a bead running down the tether to them
            Circle(a + (b - a) * f, 2.2f, AuraGreen.WithAlpha((0.4f + 0.4f * k) * Mathf.Sin(f * Mathf.PI)), 6);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 5f + q.Id);
            Arc(q.Disp, q.Def.BodyR + 6f, 1.5f, AuraGreen.WithAlpha(0.12f + 0.12f * k + 0.12f * pulse), 32);
            AuraCross(q.Disp + new V2(0f, -q.Def.BodyR - 12f - 2f * pulse), 3f + 1.5f * k, AuraGreen.WithAlpha(0.5f + 0.4f * pulse));
        }

        void AuraCross(V2 at, float s, Rgba col)
        {
            Line(at + new V2(-s, 0f), at + new V2(s, 0f), s * 0.6f, col);
            Line(at + new V2(0f, -s), at + new V2(0f, s), s * 0.6f, col);
        }
    }
}

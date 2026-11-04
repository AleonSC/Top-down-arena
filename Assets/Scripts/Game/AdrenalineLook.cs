using UnityEngine;

namespace Arena
{
    /// The Adrenaline Shot's buff on a player: a strong double heartbeat ring every 0.6 s and a heart that thumps over them.
    /// Flickers for the last 1.5 s. (Mockup: Docs/Mockups/adrenaline-shot.html, look C.)
    public partial class ArenaApp
    {
        static readonly Rgba AdrenalineRed = new Rgba(1f, 0.31f, 0.35f);

        void DrawAdrenaline(Player p, float t)
        {
            if (p.Downed)
                return;
            float fade = p.AdrenalineT < 1.5f ? (MathX.Fmod(t, 0.1f) < 0.05f ? 1f : 0.3f) : 1f;
            float r = p.Def.BodyR;
            float beat = MathX.Fmod(t, 0.6f) / 0.6f, beat2 = MathX.Fmod(t + 0.12f, 0.6f) / 0.6f;
            Arc(p.Disp, r + 4f + 26f * EaseOut(beat), 3f, AdrenalineRed.WithAlpha(0.8f * (1f - beat) * fade), 28);
            Arc(p.Disp, r + 4f + 26f * EaseOut(beat2), 2f, AdrenalineRed.WithAlpha(0.45f * (1f - beat2) * fade), 28);
            // The heart: two lobes and a point, swelling on each beat.
            float s = 1f + 0.35f * Mathf.Max(0f, 1f - beat * 5f);
            V2 c = p.Disp + new V2(0f, -(r + 20f));
            var col = AdrenalineRed.WithAlpha(fade);
            Circle(c + new V2(-3.2f * s, -1.5f * s), 3.6f * s, col, 10);
            Circle(c + new V2(3.2f * s, -1.5f * s), 3.6f * s, col, 10);
            Line(c + new V2(-6.3f * s, 0.2f * s), c + new V2(0f, 6f * s), 3f * s, col);
            Line(c + new V2(6.3f * s, 0.2f * s), c + new V2(0f, 6f * s), 3f * s, col);
        }
    }
}

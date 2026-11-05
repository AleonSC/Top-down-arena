using UnityEngine;

namespace Arena
{
    /// The Katana's meter, its aiming overlay and the Alt strike's line (Core/Katana.cs; Docs/KatanaCombo.md, mockup
    /// Docs/Mockups/katana-look-glow.html). All in the Katana's rose, the same energy look the swipes and lunge use
    /// (MeleeLooks.cs): a soft beam, a narrower brighter one and a white core.
    public partial class ArenaApp
    {
        const float KatanaStrikeFxTime = 0.55f;

        /// The strike meter, an arc round the swordsman that fills clockwise from the top and pulses once it's full.
        void DrawKatanaMeter(Player p, float t)
        {
            float f = Mathf.Clamp01(p.KatanaMeter / Game.KatanaMeterMax);
            if (f <= 0.01f)
                return;
            float r = p.Def.BodyR + 11f;
            bool full = f >= 1f;
            Arc(p.Disp, r, 2f, new Rgba(1f, 1f, 1f, 0.12f), 28);
            var col = Game.KatanaRose.WithAlpha(full ? 0.7f + 0.3f * Mathf.Sin(t * 10f) : 0.85f);
            Arc(p.Disp, r, full ? 3.5f : 2.5f, col, 28, -MathX.Pi / 2f, MathX.Tau * f);
        }

        /// Aiming a strike: the arena dims, every enemy gets a bracket, and the one it would cut to gets a reticle and the
        /// route, ending where you would land behind it. Only for your own Katana (the host never sees anyone else's).
        void DrawKatanaAim(Player p, float t)
        {
            if (!game.KatanaAiming)
                return;
            SetColor(new Rgba(0.02f, 0.03f, 0.05f, 0.4f));
            Quad(new V2(-3000f, -3000f), new V2(3000f, -3000f), new V2(3000f, 3000f), new V2(-3000f, 3000f));
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                bool mark = e.Id == game.KatanaMark;
                float r = e.Radius + (mark ? 9f : 5f);
                var col = mark ? Game.KatanaRose : new Rgba(1f, 1f, 1f, 0.55f);
                float w = mark ? 2.5f : 1.5f;
                for (int c = 0; c < 4; c++)  // four corner brackets
                {
                    float sx = (c & 1) == 0 ? -1f : 1f, sy = (c & 2) == 0 ? -1f : 1f;
                    V2 k = e.Pos + new V2(sx * r, sy * r);
                    Line(k, k + new V2(-sx * 6f, 0f), w, col);
                    Line(k, k + new V2(0f, -sy * 6f), w, col);
                }
            }
            Enemy m;
            if (!game.Enemies.TryGetValue(game.KatanaMark, out m))
                return;
            V2 end = game.KatanaStrikeEnd(p.Disp, m.Pos, p.Def.BodyR);
            V2 path = end - p.Disp;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len;
            for (float d = (t * 40f) % 14f; d < len; d += 14f)  // the route, marching toward the target
                Line(p.Disp + dir * d, p.Disp + dir * Mathf.Min(len, d + 8f), 2f, Game.KatanaRose.WithAlpha(0.9f));
            Arc(end, 14f, 1.5f, new Rgba(0.37f, 0.82f, 0.77f, 0.9f), 20);  // where you will stand
            Arc(m.Pos, m.Radius + 16f, 2f, Game.KatanaRose, 20, t * 1.1f, 4.8f);  // the reticle, turning
        }

        /// The cut itself, from Pos to Pos2: the energy look's beams along its whole length, fading, with sparks at the far end.
        void DrawKatanaStrikeFx(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            float fade = 1f - Mathf.Clamp01(fx.T / KatanaStrikeFxTime);
            if (fade <= 0f)
                return;
            var col = Game.KatanaRose;
            SoftLine(fx.Pos, fx.Pos2, 34f * fade + 6f, col.WithAlpha(0.5f * fade));
            SoftLine(fx.Pos, fx.Pos2, 12f * fade + 2f, col.WithAlpha(0.6f * fade));
            Line(fx.Pos, fx.Pos2, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
            V2 dir = path / len, at = fx.Pos2 - dir * Game.KatanaBehind;  // where the target stood
            for (int i = 0; i < 10; i++)
            {
                float a = i * 0.628f + 0.4f * Mathf.Sin(i * 3.1f), d = (20f + 70f * fx.T) * (0.5f + 0.5f * Mathf.Sin(i * 1.7f + 1f));
                Square(at + V2.FromAngle(a) * d, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * fade));
            }
        }
    }
}

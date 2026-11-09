using UnityEngine;

namespace Arena
{
    /// The Katana's meter, its chain notches, the aiming overlay and the Alt strike (Core/Katana.cs; Docs/KatanaCombo.md, mockups
    /// Docs/Mockups/katana-look-glow.html and katana-chain.html). The Katana's rose, the same energy look the swipes and
    /// lunge use (MeleeLooks.cs); an empowered strike (five kills in a row) is gold-white, wider, and shakes harder.
    ///
    /// The strike is one effect (FxKind.KatanaStrike) drawn from its age: the stance (rings drawing in, the arena dimming),
    /// the cut (a beam crossing the arena, afterimages, a white flash), the held frame (the line fading, a sheathing ring) and
    /// the fading line. Letterbox bars slide in on the striker's own screen. Each enemy it cuts gets a KatanaCut: a bright line
    /// across it and, if it died, its two halves sliding apart.
    public partial class ArenaApp
    {
        static readonly Rgba KatanaGold = new Rgba(1f, 0.89f, 0.63f);
        const float KatanaFadeTail = 0.7f;  // the line's fade after the blow

        static Rgba KatanaStrikeCol(Effect fx) => fx.Color.A > 0.75f ? KatanaGold : Game.KatanaRose;

        /// The strike meter, an arc round the swordsman that fills clockwise from the top and pulses once it's full, and the
        /// chain: a small blade over their head with a notch lit for each kill in a row.
        void DrawKatanaMeter(Player p, float t)
        {
            float f = Mathf.Clamp01(p.KatanaMeter / Game.KatanaMeterMax);
            bool emp = p.KatanaChain >= Game.KatanaChainNeed;
            if (f > 0.01f)
            {
                float r = p.Def.BodyR + 11f;
                bool full = f >= 1f;
                Arc(p.Disp, r, 2f, new Rgba(1f, 1f, 1f, 0.12f), 28);
                var col = (emp ? KatanaGold : Game.KatanaRose).WithAlpha(full ? 0.7f + 0.3f * Mathf.Sin(t * 10f) : 0.85f);
                Arc(p.Disp, r, full ? 3.5f : 2.5f, col, 28, -MathX.Pi / 2f, MathX.Tau * f);
            }
            if (f > 0.01f || p.KatanaChain > 0)
                DrawKatanaBlade(p, emp, t);
        }

        /// The Katana held drawn at the ready (strike meter not full; a full meter keeps the model's sheathed scabbard), drawn over the
        /// body in place of the model's own blade. A swipe or lunge draws the blade and comes back to this pose (DrawSwingModel,
        /// MeleeLooks.cs); it waits until then.
        void DrawKatanaIdle(Player p, float t)
        {
            float ang, reach, bend;
            if (!MeleeRest(p, WeaponId.Katana, out ang, out reach, out bend) || MeleeSwingShowing(p))
                return;
            DrawMeleeModel(WeaponId.Katana, p.Disp, ang, reach, 1f, MeleeColor(WeaponId.Katana, Rgba.White), false, bend, ArmColor(p.Id));
        }

        /// Blade notches: a tiny katana over the swordsman, one notch lit per kill in a row; at five the whole blade burns gold.
        void DrawKatanaBlade(Player p, bool emp, float t)
        {
            V2 o = p.Disp + new V2(-40f, -(p.Def.BodyR + 38f));
            var on = emp ? KatanaGold : Game.KatanaRose;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 8f);
            FillRect(o.X - 4f, o.Y - 5f, 94f, 10f, new Rgba(0f, 0f, 0f, 0.45f));
            FillRect(o.X, o.Y - 2f, 14f, 4f, new Rgba(0.42f, 0.34f, 0.25f));
            FillRect(o.X + 14f, o.Y - 4f, 3f, 8f, new Rgba(0.85f, 0.75f, 0.48f));
            for (int i = 0; i < Game.KatanaChainNeed; i++)
            {
                float nx = o.X + 22f + i * 12f;
                bool lit = i < p.KatanaChain;
                var c = lit ? on : new Rgba(0.9f, 0.93f, 0.99f, 0.28f);
                if (lit)
                    SoftLine(new V2(nx - 2f, o.Y), new V2(nx + 11f, o.Y), emp ? 14f + 4f * pulse : 10f, on.WithAlpha(0.35f));
                SetColor(c);
                Quad(new V2(nx, o.Y - 3f), new V2(nx + 9f, o.Y - 3f), new V2(nx + 9f, o.Y + 3f), new V2(nx, o.Y + 3f));
                Tri(new V2(nx + 9f, o.Y - 3f), new V2(nx + 11f, o.Y), new V2(nx + 9f, o.Y + 3f));
            }
        }

        /// Aiming a strike: the arena dims, every enemy gets a bracket, and the one it would cut to gets a reticle and the
        /// route, ending where you would land behind it. Enemies the strike would kill (Game.KatanaWouldKill, so the streak
        /// goes on) have rose brackets, the rest faint white ones; the reticle is grey on a target that would live. Only for your own Katana.
        void DrawKatanaAim(Player p, float t)
        {
            if (!game.KatanaAiming)
                return;
            bool emp = p.KatanaChain >= Game.KatanaChainNeed;
            var rose = emp ? KatanaGold : Game.KatanaRose;
            SetColor(new Rgba(0.02f, 0.03f, 0.05f, 0.4f));
            Quad(new V2(-3000f, -3000f), new V2(3000f, -3000f), new V2(3000f, 3000f), new V2(-3000f, 3000f));
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                bool mark = e.Id == game.KatanaMark, kills = game.KatanaWouldKill(p, e);
                float r = e.Radius + (mark ? 9f : 5f);
                var col = kills ? (mark ? rose : rose.WithAlpha(0.8f)) : new Rgba(1f, 1f, 1f, mark ? 0.75f : 0.35f);
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
                Line(p.Disp + dir * d, p.Disp + dir * Mathf.Min(len, d + 8f), 2f, rose.WithAlpha(0.9f));
            Arc(end, 14f, 1.5f, new Rgba(0.37f, 0.82f, 0.77f, 0.9f), 20);  // where you will stand
            Arc(m.Pos, m.Radius + 16f, 2f, game.KatanaWouldKill(p, m) ? rose : new Rgba(0.75f, 0.75f, 0.78f, 0.9f), 20, t * 1.1f, 4.8f);  // the reticle, turning (grey: it would live)
        }

        /// Letterbox bars on your own screen for your own strike (called from DrawHud).
        void DrawKatanaLetterbox()
        {
            float land = Game.KatanaWind + Game.KatanaCut, end = land + Game.KatanaHold;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.KatanaStrike || (int)fx.Radius != game.MyId || fx.Dur < 0.5f)
                    continue;
                float age = fx.T, k = age < 0.25f ? age / 0.25f : age < end ? 1f : 1f - (age - end) / 0.3f;
                k = Mathf.Clamp01(k);
                if (k <= 0f)
                    continue;
                float bh = 58f * k;
                Ui.Fill(new Rect(0f, 0f, W, bh), new Color(0f, 0f, 0f, 1f));
                Ui.Fill(new Rect(0f, H - bh, W, bh), new Color(0f, 0f, 0f, 1f));
            }
        }

        /// The strike's body pass: the dimmed arena, the stance rings, the afterimages and the sheathing ring.
        void DrawKatanaStrikeFx(Effect fx)
        {
            if (fx.Dur < 0.5f)
                return;  // (the blow's own sound cue draws nothing)
            V2 a = fx.Pos, b = fx.Pos2;
            float age = fx.T, wind = Game.KatanaWind, land = wind + Game.KatanaCut, end = land + Game.KatanaHold;
            bool emp = fx.Color.A > 0.75f;
            var col = KatanaStrikeCol(fx);
            float dim = age < wind ? age / wind : age < end ? 1f : 1f - (age - end) / 0.4f;
            if (dim > 0f)
            {
                SetColor(new Rgba(0.02f, 0.03f, 0.05f, (emp ? 0.5f : 0.4f) * Mathf.Clamp01(dim)));
                Quad(new V2(-3000f, -3000f), new V2(3000f, -3000f), new V2(3000f, 3000f), new V2(-3000f, 3000f));
            }
            if (age < wind)  // the stance: rings drawing in, motes pulled to the blade
            {
                float c = age / wind;
                for (int r = 0; r < 3; r++)
                    Arc(a, 52f - 34f * c + r * 9f, 1.5f, Rgba.White.WithAlpha(0.35f * (1f - c)), 24);
                for (int i = 0; i < 8; i++)
                {
                    float an = i * 0.785f + age * 3f, d = (46f + 10f * i % 3) * (1f - c);
                    Square(a + V2.FromAngle(an) * d, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.8f * c));
                }
            }
            if (age >= wind && age < land + 0.2f)  // the cut: afterimages along the line
            {
                float q = Mathf.Clamp01((age - wind) / Game.KatanaCut);
                float[] fr = { 0.2f, 0.45f, 0.7f };
                foreach (float f in fr)
                    if (q > f)
                        Circle(Vector2Lerp(a, b, f * q), 14f, Rgba.White.WithAlpha(0.25f * (1f - f) * (1f - Mathf.Clamp01((age - land) / 0.2f))), 14);
            }
            if (age > land + 0.1f && age < land + 0.45f)  // sheathed: a ring at the landing
            {
                float r = (age - land - 0.1f) / 0.35f;
                Arc(b, 14f + r * 26f, 2f, Rgba.White.WithAlpha(1f - r), 24);
            }
        }

        static V2 Vector2Lerp(V2 a, V2 b, float t) => a + (b - a) * t;

        /// The strike's light, in the additive pass: the beam crossing the arena, then the held line, the white flash at the end
        /// of the cut, and the glow gathering on the blade in the stance.
        void DrawKatanaGlow()
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.KatanaStrike && fx.Dur >= 0.5f)
                    DrawKatanaStrikeGlow(fx);
        }

        void DrawKatanaStrikeGlow(Effect fx)
        {
            V2 a = fx.Pos, b = fx.Pos2;
            if (a.DistanceTo(b) < 1f)
                return;
            float age = fx.T, wind = Game.KatanaWind, land = wind + Game.KatanaCut, end = land + Game.KatanaHold;
            bool emp = fx.Color.A > 0.75f;
            var col = KatanaStrikeCol(fx);
            float wide = emp ? 60f : 34f;
            if (age < wind)
            {
                float c = age / wind;
                GlowDisc(a, 24f + 16f * c, col, 0.45f * c);
                return;
            }
            float fade, head = 1f;
            if (age < land)
            {
                fade = 1f;
                head = Mathf.Clamp01((age - wind) / Game.KatanaCut);
            }
            else if (age < end)
                fade = Mathf.Lerp(1f, 0.45f, (age - land) / Game.KatanaHold);
            else
                fade = 0.45f * (1f - (age - end) / KatanaFadeTail);
            if (fade <= 0f)
                return;
            V2 tip = Vector2Lerp(a, b, head);
            SoftLine(a, tip, wide * fade + 6f, col.WithAlpha(0.5f * fade));
            SoftLine(a, tip, wide * 0.36f * fade + 2f, col.WithAlpha(0.6f * fade));
            Line(a, tip, 2.5f, Rgba.White.WithAlpha(0.9f * fade));
            if (emp)  // empowered: a second, wider bloom
                SoftLine(a, tip, wide * 1.8f * fade, col.WithAlpha(0.18f * fade));
            if (age >= land && age < land + 0.25f)  // the flash as the cut ends
            {
                float k = 1f - (age - land) / 0.25f;
                SetColor(new Rgba(1f, emp ? 0.97f : 1f, 1f, (emp ? 0.55f : 0.45f) * k));
                Quad(new V2(-3000f, -3000f), new V2(3000f, -3000f), new V2(3000f, 3000f), new V2(-3000f, 3000f));
            }
        }

        /// One enemy the strike cut: a bright line across it along the cut and sparks; if it died, its two halves slide apart.
        void DrawKatanaCutFx(Effect fx)
        {
            float age = fx.T, R = fx.Radius;
            V2 dir = fx.Pos2, n = dir.Orthogonal();
            bool died = fx.Color.A > 0.75f;
            if (age < 0.35f)
            {
                float k = 1f - age / 0.35f;
                SoftLine(fx.Pos - dir * (R + 14f), fx.Pos + dir * (R + 14f), 10f * k + 2f, Game.KatanaRose.WithAlpha(0.6f * k));
                Line(fx.Pos - dir * (R + 10f), fx.Pos + dir * (R + 10f), 2.5f, Rgba.White.WithAlpha(k));
            }
            for (int i = 0; i < 8; i++)
            {
                float an = i * 0.785f + 0.3f * Mathf.Sin(i * 3.1f), d = (10f + 60f * age) * (0.5f + 0.5f * Mathf.Sin(i * 1.7f + 1f));
                float k = Mathf.Clamp01(1f - age / 0.5f);
                if (k > 0f)
                    Square(fx.Pos + V2.FromAngle(an) * d, 3f, (i % 3 == 0 ? Rgba.White : Game.KatanaRose).WithAlpha(0.9f * k));
            }
            if (!died)
                return;
            float q = Mathf.Clamp01(age / 0.8f), off = EaseOut3(q) * 22f, al = 1f - Mathf.Clamp01((q - 0.45f) / 0.55f);
            if (al <= 0f)
                return;
            for (int s = -1; s <= 1; s += 2)
                KatanaHalfDisc(fx.Pos + n * (off * s), R, dir.Angle + (s > 0 ? 0f : MathX.Pi), al);
        }

        /// Half of an enemy: a half disc from `start` round to `start` + 180 degrees, edged white where it was cut.
        void KatanaHalfDisc(V2 c, float r, float start, float alpha)
        {
            SetColor(new Rgba(0.85f, 0.32f, 0.25f, 0.8f * alpha));
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a0 = start + MathX.Pi * i / n, a1 = start + MathX.Pi * (i + 1) / n;
                Tri(c, c + V2.FromAngle(a0) * r, c + V2.FromAngle(a1) * r);
            }
            Line(c + V2.FromAngle(start) * r, c + V2.FromAngle(start + MathX.Pi) * r, 2f, Game.KatanaRose.WithAlpha(alpha));
        }
    }
}

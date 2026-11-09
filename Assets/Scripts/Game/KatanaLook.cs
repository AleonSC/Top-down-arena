using UnityEngine;

namespace Arena
{
    /// The Katana's two stances (Core/Katana.cs; Docs/KatanaCombo.md; mockup Docs/Mockups/katana-stances.html). Hayate is moon
    /// steel (the blade held low to the side), Senbon rose (the blade forward). Drawn here: the held blade, the stance's meter round
    /// the swordsman (the other stance's, kept, as a faint inner arc), Flow's diamonds over their head, Senbon's marks on enemies,
    /// the switch's burst (FxKind.KatanaStanceFx), the hops of a Kage Chain or Zanshin (FxKind.KatanaHop) and each enemy they cut
    /// (FxKind.KatanaCut). Your own Zanshin dims the arena and letterboxes the screen. The dash-cut and the swipes are swings,
    /// drawn by MeleeLooks.cs in the Moon Steel look.
    public partial class ArenaApp
    {
        static V2 Vector2Lerp(V2 a, V2 b, float t) => a + (b - a) * t;  // (JuggernautLook.cs uses it too)

        static Rgba KatanaStanceCol(int stance) => stance == 0 ? Game.KatanaSteel : Game.KatanaRose;

        /// The Katana held at the ready in the stance's guard (MeleeRest), drawn over the body in place of the model's own blade. A
        /// swing draws the blade and comes back to this pose (DrawSwingModel, MeleeLooks.cs); it waits until then.
        void DrawKatanaIdle(Player p, float t)
        {
            float ang, reach, bend;
            if (!MeleeRest(p, WeaponId.Katana, out ang, out reach, out bend) || MeleeSwingShowing(p))
                return;
            DrawMeleeModel(WeaponId.Katana, p.Disp, ang, reach, 1f, MeleeColor(WeaponId.Katana, Rgba.White), false, bend, ArmColor(p.Id));
            if (p.KatanaStance == 1)  // Senbon: the edge lit rose
                SoftLine(p.Disp + V2.FromAngle(ang) * (reach * 0.3f), p.Disp + V2.FromAngle(ang) * reach, 6f, Game.KatanaRose.WithAlpha(0.35f));
        }

        /// The stance's meter, an arc round the swordsman filling clockwise from the top (pulsing when full), the other stance's
        /// as a faint inner arc, Flow's five diamonds over their head, and on your own screen the hold that spends a full meter.
        void DrawKatanaHud(Player p, float t)
        {
            if (p.Downed)
                return;
            bool hay = p.KatanaStance == 0;
            float kage = Mathf.Clamp01((float)p.KatanaKage / Game.KatanaKageNeed), zan = Mathf.Clamp01((float)p.KatanaZan / Game.KatanaZanNeed);
            float mine = hay ? kage : zan, other = hay ? zan : kage;
            var col = KatanaStanceCol(p.KatanaStance);
            float r = p.Def.BodyR + 11f;
            if (mine > 0.01f)
            {
                bool full = mine >= 1f;
                Arc(p.Disp, r, 2f, new Rgba(1f, 1f, 1f, 0.12f), 28);
                Arc(p.Disp, r, full ? 3.5f : 2.5f, col.WithAlpha(full ? 0.7f + 0.3f * Mathf.Sin(t * 10f) : 0.85f), 28, -MathX.Pi / 2f, MathX.Tau * mine);
            }
            if (other > 0.01f)
                Arc(p.Disp, r - 5f, 1.5f, KatanaStanceCol(hay ? 1 : 0).WithAlpha(0.35f), 28, -MathX.Pi / 2f, MathX.Tau * other);
            if (p.Id == game.MyId && game.KatanaHoldProgress > 0f)  // holding fire to spend it: a white arc closing round the meter
                Arc(p.Disp, r + 5f, 2f, Rgba.White.WithAlpha(0.9f), 28, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(game.KatanaHoldProgress));
            if (p.KatanaFlow > 0 || hay)
            {
                float a = hay ? 1f : 0.35f;
                V2 o = p.Disp + new V2(-22f, -(p.Def.BodyR + 18f));
                for (int i = 0; i < Game.KatanaFlowMax; i++)
                {
                    bool on = i < p.KatanaFlow;
                    var c = on ? (p.KatanaFlow >= Game.KatanaFlowMax ? Rgba.White : Game.KatanaSteel) : new Rgba(0.6f, 0.64f, 0.73f, 0.25f);
                    V2 d = o + new V2(i * 11f, 0f);
                    SetColor(c.WithAlpha(c.A * a));
                    Quad(d + new V2(0f, -4f), d + new V2(3.5f, 0f), d + new V2(0f, 4f), d + new V2(-3.5f, 0f));
                }
            }
        }

        /// Senbon's marks: a rose stroke over an enemy per mark, and a faint ring while it carries any.
        void DrawKatanaMarks(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.KatanaMarks == 0 || e.Hp <= 0f)
                    continue;
                for (int i = 0; i < e.KatanaMarks; i++)
                {
                    V2 m = e.Disp + new V2(e.Radius * 0.3f + i * 6f, -e.Radius - 10f);
                    Line(m + new V2(-2.5f, 4f), m + new V2(2.5f, -4f), 2.2f, Game.KatanaRose);
                }
                Arc(e.Disp, e.Radius + 4f, 1.4f, Game.KatanaRose.WithAlpha(0.3f + 0.15f * Mathf.Sin(t * 6f + e.Id)), 20);
            }
        }

        /// The switch: a burst ring out to its reach, streaks thrown off it, and the blade's sweep from one guard to the other.
        void DrawKatanaStanceFx(Effect fx)
        {
            float age = fx.T, q = Mathf.Clamp01(age / 0.35f);
            var col = fx.Color.WithAlpha(1f);
            Player p;
            V2 c = game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p.Disp : fx.Pos;
            if (q < 1f)
            {
                Arc(c, 14f + (fx.Radius - 14f) * EaseOut3(q), 3f * (1f - q) + 0.5f, col.WithAlpha(1f - q), 32);
                Arc(c, 10f + (fx.Radius * 0.6f) * EaseOut3(q), 1.5f, Rgba.White.WithAlpha(0.7f * (1f - q)), 32);
                for (int i = 0; i < 10; i++)
                {
                    float an = i * MathX.Tau / 10f + 0.3f, d0 = 16f + fx.Radius * 0.7f * EaseOut3(q);
                    Line(c + V2.FromAngle(an) * d0, c + V2.FromAngle(an) * (d0 + 10f * (1f - q)), 2f, (i % 2 == 0 ? Rgba.White : col).WithAlpha(1f - q));
                }
            }
            if (p != null && age < Game.KatanaSwitchTime)  // the blade turning to its new guard
            {
                float u = age / Game.KatanaSwitchTime, to = p.Aim + ((int)fx.Pos2.Y == 0 ? -1.15f : -0.12f), from = p.Aim + ((int)fx.Pos2.Y == 0 ? -0.12f : -1.15f);
                float at = Mathf.Lerp(from, to, EaseOut3(u));
                KatanaCrescent(c, 46f, from, at, age, Game.KatanaSwitchTime, col, 0.2f, false);
            }
        }

        /// A hop of a Kage Chain (steel, fading at once) or Zanshin (rose, hanging until the cuts fall): a streak from where it began.
        void DrawKatanaHopFx(Effect fx)
        {
            bool zan = fx.Color.A > 0.75f;
            float age = fx.T, grow = Mathf.Clamp01(age / (zan ? Game.KatanaZanHop : Game.KatanaKageHop));
            float fade = zan ? (fx.Dur - age < 0.45f ? (fx.Dur - age) / 0.45f : 1f) : 1f - Mathf.Clamp01(age / fx.Dur);
            if (fade <= 0f)
                return;
            V2 tip = fx.Pos + (fx.Pos2 - fx.Pos) * EaseOut3(grow);
            Line(fx.Pos, tip, zan ? 2.2f : 2f, Rgba.White.WithAlpha(0.9f * fade));
            if (age < 0.25f)  // afterimages along it
                for (int i = 1; i <= 3; i++)
                    Arc(fx.Pos + (fx.Pos2 - fx.Pos) * (i / 4f), 13f, 1.2f, fx.Color.WithAlpha(0.4f * (1f - age / 0.25f)), 16);
        }

        /// The additive pass: the hops' light, the switch's flash, and on your own screen the dark of your Zanshin.
        void DrawKatanaGlow()
        {
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.KatanaHop)
                {
                    bool zan = fx.Color.A > 0.75f;
                    float fade = zan ? (fx.Dur - fx.T < 0.45f ? (fx.Dur - fx.T) / 0.45f : 1f) : 1f - Mathf.Clamp01(fx.T / fx.Dur);
                    if (fade > 0f)
                        SoftLine(fx.Pos, fx.Pos2, (zan ? 16f : 12f) * fade + 2f, fx.Color.WithAlpha(0.55f * fade));
                }
                else if (fx.Kind == FxKind.KatanaStanceFx && fx.T < 0.25f)
                    GlowDisc(fx.Pos, 50f, fx.Color.WithAlpha(1f), 0.5f * (1f - fx.T / 0.25f));
            }
        }

        /// Your own Zanshin: the arena dims while you cut through the marked (drawn over the world, under the hops).
        void DrawKatanaZanshinDim()
        {
            if (!game.KatanaZanshinLocal)
                return;
            float t = game.KatanaRunT, total = game.KatanaRunTotal, k = Mathf.Min(1f, t / 0.12f) * Mathf.Clamp01((total - t) / 0.1f + 0.2f);
            SetColor(new Rgba(0.02f, 0.03f, 0.06f, 0.5f * k));
            Quad(new V2(-3000f, -3000f), new V2(3000f, -3000f), new V2(3000f, 3000f), new V2(-3000f, 3000f));
        }

        /// Letterbox bars on your own screen through your own Zanshin (called from DrawHud).
        void DrawKatanaLetterbox()
        {
            if (!game.KatanaZanshinLocal)
                return;
            float t = game.KatanaRunT, total = game.KatanaRunTotal;
            float k = Mathf.Clamp01(Mathf.Min(t / 0.2f, (total - t) / 0.2f + 0.3f));
            float bh = 52f * k;
            Ui.Fill(new Rect(0f, 0f, W, bh), new Color(0f, 0f, 0f, 1f));
            Ui.Fill(new Rect(0f, H - bh, W, bh), new Color(0f, 0f, 0f, 1f));
        }

        /// One enemy a Kage Chain or Zanshin cut: a bright line across it and sparks; if it died, its two halves slide apart.
        void DrawKatanaCutFx(Effect fx)
        {
            float age = fx.T, R = fx.Radius;
            V2 dir = fx.Pos2, n = dir.Orthogonal();
            bool died = fx.Color.A > 0.75f;
            var col = fx.Color.WithAlpha(1f);
            if (age < 0.35f)
            {
                float k = 1f - age / 0.35f;
                SoftLine(fx.Pos - dir * (R + 14f), fx.Pos + dir * (R + 14f), 10f * k + 2f, col.WithAlpha(0.6f * k));
                Line(fx.Pos - dir * (R + 10f), fx.Pos + dir * (R + 10f), 2.5f, Rgba.White.WithAlpha(k));
            }
            for (int i = 0; i < 8; i++)
            {
                float an = i * 0.785f + 0.3f * Mathf.Sin(i * 3.1f), d = (10f + 60f * age) * (0.5f + 0.5f * Mathf.Sin(i * 1.7f + 1f));
                float k = Mathf.Clamp01(1f - age / 0.5f);
                if (k > 0f)
                    Square(fx.Pos + V2.FromAngle(an) * d, 3f, (i % 3 == 0 ? Rgba.White : col).WithAlpha(0.9f * k));
            }
            if (!died)
                return;
            float q = Mathf.Clamp01(age / 0.8f), off = EaseOut3(q) * 22f, al = 1f - Mathf.Clamp01((q - 0.45f) / 0.55f);
            if (al <= 0f)
                return;
            for (int s = -1; s <= 1; s += 2)
                KatanaHalfDisc(fx.Pos + n * (off * s), R, dir.Angle + (s > 0 ? 0f : MathX.Pi), al, col);
        }

        /// Half of an enemy: a half disc from `start` round to `start` + 180 degrees, edged where it was cut.
        void KatanaHalfDisc(V2 c, float r, float start, float alpha, Rgba edge)
        {
            SetColor(new Rgba(0.85f, 0.32f, 0.25f, 0.8f * alpha));
            const int n = 10;
            for (int i = 0; i < n; i++)
            {
                float a0 = start + MathX.Pi * i / n, a1 = start + MathX.Pi * (i + 1) / n;
                Tri(c, c + V2.FromAngle(a0) * r, c + V2.FromAngle(a1) * r);
            }
            Line(c + V2.FromAngle(start) * r, c + V2.FromAngle(start + MathX.Pi) * r, 2f, edge.WithAlpha(alpha));
        }
    }
}

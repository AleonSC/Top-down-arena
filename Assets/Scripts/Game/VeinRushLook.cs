using UnityEngine;

namespace Arena
{
    /// The DPS Hemomancer's Vein Rush (Core/VeinRush.cs), look B "Needle" of Docs/Mockups/vein-rush.html, ported from the
    /// mockup: a glowing crimson streak with a white-hot core is drawn from where you started to where you are, your figure
    /// stretched thin along it, with afterimages hanging every 34 px. Every enemy cut gets a sharp slash across it, a white
    /// flash, a hit-stop and a faint screen flash (Juice.cs, the HUD), and three drops of its blood home back to you with a
    /// "+2%". The trail of blood (ZoneKind.VeinTrail) is one continuous smear with a dotted bright vein down it; a mastered
    /// trail swells and pulses, then bursts with spikes at 1 s.
    public partial class ArenaApp
    {
        static readonly Rgba NeedleRed = new Rgba(1f, 0.24f, 0.33f), NeedleWhite = new Rgba(1f, 0.91f, 0.93f), RefundPink = new Rgba(1f, 0.7f, 0.74f);
        public const float VeinCutSlash = 0.4f, VeinDropTime = 0.7f;

        /// The mockup's Hemomancer figure, for the stretched rusher and the afterimages: robe, hood, eye and hands, in local
        /// coordinates (x along `ang`), scaled by sx along it and sy across.
        void DrawHemoFigure(V2 at, float ang, float sx, float sy, float alpha)
        {
            if (alpha <= 0.01f)
                return;
            V2 u = V2.FromAngle(ang), v = u.Orthogonal();
            System.Func<float, float, V2> P = (x, y) => at + u * (x * sx) + v * (y * sy);
            Ellipse(P(2f, 3f), 12f * sx, 10f * sy, ang, new Rgba(0f, 0f, 0f, 0.35f * alpha), 14);
            Ellipse(P(-2.5f, 0f), 10.5f * sx, 11.5f * sy, ang, new Rgba(0.07f, 0.02f, 0.04f, alpha), 16);  // the outline
            Ellipse(P(-2.5f, 0f), 9.3f * sx, 10.3f * sy, ang, new Rgba(0.29f, 0.05f, 0.11f, alpha), 16);   // the robe
            Ellipse(P(0f, 0f), 6.5f * sx, 6.5f * sy, ang, new Rgba(0.55f, 0.08f, 0.15f, alpha), 12);      // the hood
            Ellipse(P(1.5f, 0f), 2.3f * sx, 2.3f * sy, ang, NeedleRed.WithAlpha(alpha), 8);                 // the eye
            Ellipse(P(9f, -6f), 2.2f * sx, 2.2f * sy, ang, new Rgba(0.91f, 0.78f, 0.78f, alpha), 8);        // the hands
            Ellipse(P(9f, 6f), 2.2f * sx, 2.2f * sy, ang, new Rgba(0.91f, 0.78f, 0.78f, alpha), 8);
            Ellipse(P(12f, 6f), 2.8f * sx, 2.8f * sy, ang, NeedleRed.WithAlpha(0.9f * alpha), 8);          // blood at the fingertips
        }

        /// The streak (FxKind.VeinRush): grows with the rush, the figure stretched at its head and afterimages behind;
        /// then it thins and fades.
        void DrawVeinRushFx(Effect fx, float t)
        {
            float rush = Game.VeinRushTime, post = Game.VeinRushFxTime, age = fx.T;
            V2 from = fx.Pos, to = fx.Pos2, path = to - from;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len;
            float ang = Mathf.Atan2(dir.Y, dir.X);
            Player p;
            bool known = game.Players.TryGetValue((int)fx.Radius, out p);
            float grow = Mathf.Clamp01(age / rush);
            V2 head = from + path * grow;
            if (known && age <= rush)  // follow the rusher's drawn position when it's on the path
            {
                float along = (p.Disp.X - from.X) * dir.X + (p.Disp.Y - from.Y) * dir.Y;
                if (along > 0f && along <= len + 4f && (from + dir * along).DistanceTo(p.Disp) < 20f)
                    head = from + dir * Mathf.Min(len, along);
            }
            float done = head.DistanceTo(from);
            // Afterimages: every 34 px, each fading 0.4 s after the rush passed it.
            for (float d = 34f; d < done; d += 34f)
            {
                float ghostAge = age - d / len * rush;
                if (ghostAge < 0f || ghostAge >= 0.4f)
                    continue;
                DrawHemoFigure(from + dir * d, ang, 1.2f * 1.3f, 0.8f * 1.3f, (1f - ghostAge / 0.4f) * 0.55f);
            }
            if (age <= rush)
            {
                Line(from, head, 20f, NeedleRed.WithAlpha(0.45f));
                Line(from, head, 7f, NeedleRed);
                Line(from, head, 2.6f, NeedleWhite);
                Circle(from, 7f, NeedleRed.WithAlpha(0.45f), 12);
                DrawHemoFigure(head, ang, 1.7f * 1.3f, 0.55f * 1.3f, 1f);  // you, stretched thin along the needle
                return;
            }
            float k = Mathf.Clamp01((age - rush) / post);
            Line(from, to, 6f * (1f - k), NeedleRed.WithAlpha(1f - k));
            Line(from, to, 2f * (1f - k), NeedleWhite.WithAlpha(1f - k));
            if (k < 0.35f && known)  // snapping back into shape where you land
                DrawHemoFigure(p.Disp, ang, Mathf.Lerp(1.4f, 1f, k / 0.35f), Mathf.Lerp(0.75f, 1f, k / 0.35f), 0.6f * (1f - k / 0.35f));
        }

        /// One enemy cut (FxKind.VeinCut): a sharp slash across it, a white flash, and three drops of its blood homing back
        /// to the rusher.
        void DrawVeinCutFx(Effect fx, float t)
        {
            float age = fx.T, r = fx.Radius % 1000f;
            int rusher = (int)(fx.Radius / 1000f);
            V2 dir = fx.Pos2.IsZero ? new V2(1f, 0f) : fx.Pos2.Normalized();
            float side = SprayHash(fx.Pos.X * 0.37f + fx.Pos.Y * 0.11f) < 0.5f ? 0.6f : -0.6f;
            if (age < VeinCutSlash)
            {
                float k = age / VeinCutSlash, L = (r + 34f) * (k < 0.25f ? k * 4f : 1f);
                V2 s = V2.FromAngle(Mathf.Atan2(dir.Y, dir.X) + side);
                Line(fx.Pos - s * L, fx.Pos + s * L, 7f, NeedleRed.WithAlpha(1f - k));
                Line(fx.Pos - s * L, fx.Pos + s * L, 2.4f, NeedleWhite.WithAlpha(1f - k));
            }
            if (age < 0.15f)
                Circle(fx.Pos, r, new Rgba(1f, 1f, 1f, 0.75f * (1f - age / 0.15f)), 16);
            Player p;
            if (!game.Players.TryGetValue(rusher, out p))
                return;
            for (int j = 0; j < 3; j++)
            {
                float u = Mathf.Clamp01(age / VeinDropTime - j * 0.06f);
                if (u <= 0f || u >= 1f)
                    continue;
                float e = u * u * (3f - 2f * u);
                V2 kick = V2.FromAngle(SprayHash(fx.Pos.X + j * 7.7f) * MathX.Tau) * (30f + 25f * SprayHash(fx.Pos.Y + j * 3.1f));
                V2 mid = fx.Pos + kick;
                V2 at = fx.Pos * ((1f - e) * (1f - e)) + mid * (2f * (1f - e) * e) + p.Disp * (e * e);
                Circle(at, 8f, RefundPink.WithAlpha(0.25f), 10);
                Circle(at, 3.6f, RefundPink, 8);
            }
        }

        /// A Vein Rush trail (ZoneKind.VeinTrail, a row of pools): one continuous smear from each pool to the next, a dotted
        /// bright vein down it; mastered, it swells and pulses, then bursts with spikes at 1 s.
        void DrawVeinTrail(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float f = z.T < 0.5f ? Mathf.Clamp01(z.T / 0.5f) : 1f, grow = Mathf.Clamp01(age / 0.12f);
            Zone next;
            bool link = game.Zones.TryGetValue(z.Id + 1, out next) && next.Kind == ZoneKind.VeinTrail && next.Owner == z.Owner && next.Pos.DistanceTo(z.Pos) < 50f;
            V2 a = z.Pos, b = link ? next.Pos : z.Pos;
            var deep = new Rgba(0.23f, 0.03f, 0.07f, 0.85f * f * grow);
            var mid = new Rgba(0.43f, 0.04f, 0.11f, 0.55f * f * grow);
            Circle(a, 11f * grow, deep, 14);
            Circle(a, 6f * grow, mid, 12);
            if (link)
            {
                Line(a, b, 22f * grow, deep);
                Line(a, b, 12f * grow, mid);
                float l = a.DistanceTo(b);
                V2 d = (b - a) / Mathf.Max(1f, l);
                for (float x = 0f; x < l; x += 11f)  // the dotted vein
                    Line(a + d * x, a + d * Mathf.Min(l, x + 2f), 3f, NeedleRed.WithAlpha(0.35f * f * grow));
            }
            if (z.Aim <= 0.5f)
                return;
            float burstAt = Game.TrailBurstAtPublic;
            if (age < burstAt)  // mastered: swelling and pulsing toward the burst
            {
                float k = age / burstAt;
                var glow = new Rgba(1f, 0.24f, 0.33f, 0.5f * (0.25f + 0.5f * k * k) * (0.6f + 0.4f * Mathf.Sin(t * 40f)) * f);
                Circle(a, (5f + 10f * k) * 0.5f, glow, 10);
                if (link)
                    Line(a, b, 5f + 10f * k, glow);
                return;
            }
            float bf = 1f - (age - burstAt) * 3f;
            if (bf <= 0f)
                return;
            if (link)
            {
                Line(a, b, 30f * bf + 6f, NeedleRed.WithAlpha(0.55f * bf));
                Line(a, b, 6f, NeedleWhite.WithAlpha(bf));
            }
            Circle(a, (30f * bf + 6f) * 0.5f, NeedleRed.WithAlpha(0.55f * bf), 14);
            for (int k = 0; k < 2; k++)  // spikes
            {
                float sa = (z.Id * 2.3f + k * 3.1f) % MathX.Tau, L = 10f + 14f * bf;
                Line(a, a + V2.FromAngle(sa) * L, 2.5f, NeedleRed.WithAlpha(bf));
            }
        }

        /// On the HUD: "−10%" where a rush starts, "+2%" off each enemy cut, and a faint flash of the screen on your own cuts.
        void DrawVeinRushPops()
        {
            if (game == null)
                return;
            float flash = 0f;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind == FxKind.VeinRush && fx.T < 1.1f)
                    RisingPop(fx.Pos + new V2(0f, -20f), fx.T, "-10%", NeedleWhite, 15);
                else if (fx.Kind == FxKind.VeinCut && fx.T < 1.1f)
                {
                    float r = fx.Radius % 1000f;
                    RisingPop(fx.Pos + new V2(14f, -r - 22f), fx.T, "+2%", RefundPink, 12);
                    if ((int)(fx.Radius / 1000f) == game.MyId && fx.T < 0.15f)
                        flash = Mathf.Max(flash, 1f - fx.T / 0.15f);
                }
            }
            if (flash > 0f && !fxReduced)
                Ui.Fill(new Rect(-W, -H, W * 3f, H * 3f), new Color(1f, 0.91f, 0.93f, 0.12f * flash));
        }

        /// A small number rising off a world point and fading (the mockups' pops), on the HUD.
        void RisingPop(V2 world, float age, string text, Rgba col, int size)
        {
            V2 at = ToUi(world);
            at.Y -= 18f * age;
            Color c = ToColor(col);
            c.a = 1f - Mathf.Clamp01((age - 0.6f) / 0.5f);
            Ui.Shadowed(new Rect(at.X - 60f, at.Y - 10f, 120f, 20f), text, size, c);
        }
    }
}

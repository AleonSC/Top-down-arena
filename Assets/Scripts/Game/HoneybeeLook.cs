using UnityEngine;

namespace Arena
{
    /// How the Honeybee Staff looks (Core/HoneybeeStaff.cs; the mockup's variant C, "Honey Harvest"): your gold bees hatching out of a wax
    /// cell, buzzing a little loop, then streaking at an enemy; the sting (a burst, the stinger left in the enemy, the bee tumbling down
    /// while its wings drift off); a Swarm's white ring; the honey spray off the staff and its globs; the puddles (lumpy, glossy, bubbling,
    /// enemies stuck in them dripping); a harvest bursting gold with comb cells and honey flying; the honey meter behind you; and the
    /// "SWARM" and "HARVEST" pops over the action (HUD).
    public partial class ArenaApp
    {
        static readonly Rgba BeeGold = new Rgba(1f, 0.77f, 0.24f), BeeDark = new Rgba(0.16f, 0.1f, 0.02f, 0.95f), BeeWing = new Rgba(0.86f, 0.94f, 1f, 0.55f);
        static readonly Rgba HoneyAmber = new Rgba(0.91f, 0.6f, 0.11f), HoneyLight = new Rgba(1f, 0.81f, 0.35f), HoneyShine = new Rgba(1f, 0.94f, 0.75f);

        static float BeeHash(float x) { float s = Mathf.Sin(x * 12.9898f) * 43758.5453f; return s - Mathf.Floor(s); }

        /// One bee: wings beating either side, a striped gold body facing `aim`, a dark head and the sting.
        void DrawBeeBody(V2 at, float aim, float scale, float t, float seed, float alpha)
        {
            V2.Axes(aim, out V2 f, out V2 s);
            float beat = 0.5f + 0.5f * Mathf.Sin(t * 50f + seed * 7f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 wc = at - f * (1f * scale) + s * (k * (3.6f + 1.4f * beat) * scale);
                Ellipse(wc, 4f * scale, (1.8f + 0.8f * beat) * scale, aim + k * 0.5f, BeeWing.WithAlpha(BeeWing.A * alpha), 10);
            }
            Ellipse(at, 6f * scale, 3.6f * scale, aim, BeeGold.WithAlpha(alpha), 14);
            for (int i = 0; i < 2; i++)
            {
                V2 c = at - f * ((1.6f + 3.2f * i) * scale);
                Line(c + s * (3.2f * scale), c - s * (3.2f * scale), 1.5f * scale, BeeDark.WithAlpha(BeeDark.A * alpha));
            }
            Circle(at + f * (5.2f * scale), 2.2f * scale, BeeDark.WithAlpha(BeeDark.A * alpha), 8);
            Line(at - f * (5.5f * scale), at - f * (9f * scale), 1.2f * scale, BeeDark.WithAlpha(BeeDark.A * alpha));
        }

        /// A flat-topped wax cell.
        void CombCell(V2 at, float r, float w, Rgba line, Rgba fill)
        {
            if (fill.A > 0f)
                Polygon(at, r, 6, 0f, fill);
            HexOutline(at, r, w, line);
        }

        /// A Honeybee Staff bee (MinionKind.Bee). Wind: 0..1 through its hatch and buzz, 1 once it flies (or for a harvest bee).
        void DrawBee(Minion m, float t)
        {
            float hatch = Game.BeeHatch / Game.BeeWait;
            if (m.Wind < 1f && m.Wind < hatch)  // hatching: the cell holds, the bee pushes up out of it
            {
                float k = m.Wind / hatch;
                CombCell(m.Disp, 9f + 3f * k, 1.6f, HoneyLight.WithAlpha(0.9f), HoneyAmber.WithAlpha(0.55f * (1f - k)));
                DrawBeeBody(m.Disp + new V2(0f, -4f * k), -MathX.Pi / 2f, 0.25f + 0.75f * k, t, m.Id, 1f);
                return;
            }
            if (m.Wind < 1f)  // buzzing a little loop where it hatched; the closing ring says it is about to go
            {
                float q = (m.Wind - hatch) / Mathf.Max(0.01f, 1f - hatch), a = t * 22f + m.Id;
                V2 at = m.Disp + new V2(Mathf.Cos(a) * 5f, -5f + Mathf.Sin(a * 2f) * 2.5f);
                DrawBeeBody(at, a + MathX.Pi / 2f, 1f, t, m.Id, 1f);
                Arc(m.Disp + new V2(0f, -5f), 13f * (1f - q) + 4f, 1.2f, BeeGold.WithAlpha(0.55f), 18);
                if (Mathf.Sin(t * 40f + m.Id) > 0f)  // buzz lines
                    for (int k = -1; k <= 1; k += 2)
                        Line(at + new V2(k * 7f, -5f), at + new V2(k * 11f, -8f), 1f, HoneyShine.WithAlpha(0.6f));
                return;
            }
            V2 f = V2.FromAngle(m.Aim);
            float fade = m.T < 0.6f ? Mathf.Clamp01(m.T / 0.6f) : 1f;  // a lost bee fading as it fizzles
            for (int i = 1; i <= 4; i++)  // the streak behind it
                Line(m.Disp - f * (4f + 4f * (i - 1)), m.Disp - f * (4f + 4f * i), 2.4f - 0.4f * i, BeeGold.WithAlpha(0.35f * (1f - i / 5f) * fade));
            DrawBeeBody(m.Disp, m.Aim, 1f, t, m.Id, fade);
        }

        /// FxKind.BeeSting: the sting's burst, the stinger left in the enemy, the bee tumbling down as its wings drift off; a Swarm's ring.
        void DrawBeeStingFx(Effect fx, float t)
        {
            float age = fx.T, dir = fx.Radius;
            V2.Axes(dir, out V2 f, out V2 s);
            Enemy e;
            bool alive = game.Enemies.TryGetValue((int)fx.Pos2.X, out e);
            if (age < 0.25f)  // the burst: three white lines off the point
            {
                float k = age / 0.25f;
                for (int i = -1; i <= 1; i++)
                {
                    V2 d = V2.FromAngle(dir + i * 0.55f);
                    Line(fx.Pos + d * (2f + 4f * k), fx.Pos + d * (6f + 8f * k), 1.6f, Rgba.White.WithAlpha(1f - k));
                }
            }
            if (alive && age < 0.85f)  // the stinger, stuck in it on the side the bee came from
            {
                V2 back = V2.FromAngle(dir + MathX.Pi);
                float a = age < 0.6f ? 1f : 1f - (age - 0.6f) / 0.25f;
                Line(e.Disp + back * (e.Radius * 0.55f), e.Disp + back * (e.Radius + 4f), 1.6f, BeeDark.WithAlpha(a));
            }
            if (age < 0.8f)  // the bee tumbles back and down, spinning and fading
            {
                float k = age / 0.8f;
                V2 at = fx.Pos - f * (22f * k) + new V2(0f, 26f * k * k);
                DrawBeeBody(at, dir + MathX.Pi + k * 9f, 0.85f - 0.25f * k, 0f, fx.Pos.X, (1f - k) * 0.85f);
                for (int w = -1; w <= 1; w += 2)  // its wings, come off and drifting away
                {
                    V2 wp = fx.Pos + s * (w * (6f + 24f * k)) - f * (8f * k) + new V2(0f, 10f * k);
                    Ellipse(wp, 4f, 2f, k * 8f * w, BeeWing.WithAlpha(0.6f * (1f - k)), 8);
                }
            }
            if (fx.Pos2.Y > 0f && age < 0.4f)  // Swarm: a white ring round the enemy (bigger on the first swarming sting)
            {
                V2 c = alive ? e.Disp : fx.Pos;
                float r = (alive ? e.Radius : 12f) + (fx.Pos2.Y >= 1f ? 26f : 14f) * Mathf.Sqrt(age / 0.4f);
                Arc(c, r, 2.2f * (1f - age / 0.4f) + 0.6f, Rgba.White.WithAlpha(0.9f * (1f - age / 0.4f)), 28);
            }
        }

        /// FxKind.HoneyHarvest: a gold flash, wax cells flung out, honey splashing, and two rings.
        void DrawHoneyHarvestFx(Effect fx)
        {
            float age = fx.T, k = Mathf.Clamp01(age / 0.6f), R = Mathf.Max(8f, fx.Radius);
            if (k < 1f)
            {
                Circle(fx.Pos, R * (1.6f + 1.2f * k), HoneyLight.WithAlpha(0.35f * (1f - k)), 24);
                Circle(fx.Pos, R * (0.9f + 0.4f * k), HoneyShine.WithAlpha(0.5f * (1f - k)), 20);
                for (int j = 0; j < 6; j++)  // wax cells flung out
                {
                    float a = MathX.Tau * j / 6f + fx.Pos.X * 0.01f;
                    V2 c = fx.Pos + V2.FromAngle(a) * (R + 26f * Mathf.Sqrt(k));
                    CombCell(c, 5f, 1.4f, BeeGold.WithAlpha(1f - k), HoneyAmber.WithAlpha(0.35f * (1f - k)));
                }
                for (int i = 0; i < 12; i++)  // honey drops arcing out and falling
                {
                    float a = MathX.Tau * BeeHash(fx.Pos.X + i * 3.1f), v = 50f + 90f * BeeHash(fx.Pos.Y + i * 1.7f);
                    V2 d = V2.FromAngle(a);
                    V2 at = fx.Pos + d * (v * age) + new V2(0f, -40f * age + 160f * age * age);
                    float r = 1.6f + 2f * BeeHash(i + fx.Pos.X);
                    Ellipse(at, r, r * 1.3f, 0f, HoneyAmber.WithAlpha(1f - k), 8);
                }
            }
            for (int j = 0; j < 2; j++)
            {
                float q = Mathf.Clamp01((age - j * 0.12f) / 0.5f);
                if (q > 0f && q < 1f)
                    Arc(fx.Pos, R + 8f * j + 34f * Mathf.Sqrt(q), 2.4f * (1f - q) + 0.5f, BeeGold.WithAlpha(0.85f * (1f - q)), 32);
            }
        }

        /// A honey puddle (ZoneKind.HoneyPuddle): a lumpy glossy pool with a shine drifting over it, bubbles popping, a splash ring when
        /// it's fresh, and the enemies stuck in it ringed in honey and dripping.
        void DrawHoneyPuddle(Zone z, float t)
        {
            float age = z.Duration - z.T, fade = Mathf.Clamp01(z.T / 0.5f), R = z.Radius, seed = z.Id * 1.37f;
            const int n = 16;
            V2 prev = default(V2);
            SetColor(HoneyAmber.WithAlpha(0.55f * fade));
            for (int i = 0; i <= n; i++)  // the lumpy edge
            {
                float a = MathX.Tau * i / n, rr = R * (0.86f + 0.14f * Mathf.Sin(a * 3f + seed) + 0.04f * Mathf.Sin(a * 5f + t * 1.5f));
                V2 p = z.Pos + new V2(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr * 0.86f);
                if (i > 0)
                    Tri(z.Pos, prev, p);
                prev = p;
            }
            Ellipse(z.Pos + new V2(-R * 0.12f, -R * 0.1f), R * 0.62f, R * 0.5f, 0f, HoneyLight.WithAlpha(0.35f * fade), 18);
            float sh = Mathf.Sin(t * 2f + seed) * R * 0.12f;  // the shine, sliding a little
            Ellipse(z.Pos + new V2(-R * 0.3f + sh, -R * 0.26f), R * 0.22f, R * 0.08f, -0.4f, HoneyShine.WithAlpha(0.6f * fade), 10);
            if (R > 30f)
                HexOutline(z.Pos + new V2(R * 0.25f, R * 0.12f), 7f, 1f, HoneyShine.WithAlpha(0.18f * fade));
            for (int i = 0; i < 3; i++)  // bubbles rising and popping
            {
                float ph = (t * 0.7f + i * 0.37f + seed * 0.13f) % 1f, a = BeeHash(seed + i + Mathf.Floor(t * 0.7f + i * 0.37f + seed * 0.13f)) * MathX.Tau;
                V2 at = z.Pos + V2.FromAngle(a) * (R * 0.5f * BeeHash(a));
                Arc(at, 1f + 3f * ph, 1f, HoneyShine.WithAlpha(0.8f * (1f - ph) * fade), 10);
            }
            if (age < 0.3f)  // freshly splashed down
                Arc(z.Pos, R * (1f + age * 1.6f), 2f, HoneyLight.WithAlpha(0.5f - age / 0.6f), 28);
            foreach (var e in game.Enemies.Values)  // stuck in it: a honey collar and drips
            {
                if (e.Disp.FartherThan(z.Pos, R + e.Radius * 0.4f))
                    continue;
                Arc(e.Disp + new V2(0f, e.Radius * 0.3f), e.Radius * 0.95f, 2.6f, HoneyAmber.WithAlpha(0.75f * fade), 16, 0.2f, MathX.Pi - 0.4f);
                float d = (t * 1.6f + e.Id * 0.31f) % 1f;
                Circle(e.Disp + new V2(e.Radius * 0.45f, e.Radius * 0.55f + 9f * d), 1.8f * (1f - d * 0.5f), HoneyAmber.WithAlpha(fade * (1f - d)), 6);
            }
        }

        /// A honey glob in flight (Defs.HoneyGlob): a stretched drop with a glint.
        void DrawHoneyGlob(Bullet b)
        {
            float a = b.Vel.IsZero ? 0f : b.Vel.Angle;
            Ellipse(b.Pos, 6f, 3f, a, HoneyAmber, 10);
            Circle(b.Pos + V2.FromAngle(a) * 1.5f + new V2(0f, -1f), 1.2f, HoneyShine.WithAlpha(0.85f), 6);
        }

        /// While a player sprays: honey streaming off the staff's hive in a short cone.
        void DrawHoneySpray(Player p, float t)
        {
            if (!p.HoneyOn || p.Downed)
                return;
            V2 f = V2.FromAngle(p.Aim);
            V2 tip = p.Disp + f * (p.Def.BodyR + 14f);
            Circle(tip, 7f + Mathf.Sin(t * 40f) * 1f, HoneyLight.WithAlpha(0.35f), 12);
            for (int i = 0; i < 10; i++)
            {
                float ph = (t * 3.2f + i / 10f) % 1f, off = (BeeHash(i * 1.3f) - 0.5f) * 2f * Game.HoneySpread * 0.8f;
                V2 at = tip + V2.FromAngle(p.Aim + off) * (6f + 52f * ph);
                Ellipse(at, 3f - ph, 1.8f - ph * 0.6f, p.Aim + off, HoneyAmber.WithAlpha(0.9f * (1f - ph)), 8);
            }
        }

        /// The honey meter: an amber arc behind you while it isn't full, glowing while you spray; a notch where the spray can start.
        void DrawHoneyMeter(Player p)
        {
            if (!fxOn || p.Downed || p.HoneyFill >= Game.HoneyMax - 0.5f && !p.HoneyOn)
                return;
            float fill = Mathf.Clamp01(p.HoneyFill / Game.HoneyMax), sweep = 1.9f, r = p.Def.BodyR + 13f;
            float start = p.Aim + MathX.Pi - sweep / 2f;
            Arc(p.Disp, r, 5f, HoneyAmber.WithAlpha(0.16f), 20, start, sweep);
            if (fill > 0f)
            {
                if (p.HoneyOn)
                    Arc(p.Disp, r, 9f, HoneyLight.WithAlpha(0.18f + 0.1f * Mathf.Sin(Time.unscaledTime * 7f)), 20, start, sweep * fill);
                Arc(p.Disp, r, 5f, p.HoneyFill >= Game.HoneyMin ? BeeGold : BeeGold.WithAlpha(0.45f), 20, start, sweep * fill);
            }
            float notch = start + sweep * Game.HoneyMin / Game.HoneyMax;
            Line(p.Disp + V2.FromAngle(notch) * (r - 4f), p.Disp + V2.FromAngle(notch) * (r + 4f), 1.4f, HoneyShine.WithAlpha(0.6f));
        }

        /// HUD: "SWARM" over an enemy on its first swarming sting, "HARVEST +n" over a burst; they pop in, rise and fade.
        void DrawHoneyCallouts()
        {
            if (!fxOn || game == null)
                return;
            foreach (var fx in game.Effects)
            {
                string text;
                Color c;
                if (fx.Kind == FxKind.BeeSting && fx.Pos2.Y >= 1f)
                {
                    text = "SWARM";
                    c = Color.white;
                }
                else if (fx.Kind == FxKind.HoneyHarvest)
                {
                    text = "HARVEST +" + (int)fx.Pos2.Y;
                    c = CrtStyle.Gold;
                }
                else
                    continue;
                float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur)), pop = Mathf.Min(1f, fx.T * 9f);
                V2 at = ToUi(fx.Pos);
                at.Y -= 28f + 22f * f;
                c.a = f < 0.6f ? 1f : 1f - (f - 0.6f) / 0.4f;
                int size = Mathf.RoundToInt(15f * (0.6f + 0.4f * pop + (pop >= 1f ? 0.15f * (1f - Mathf.Min(1f, (fx.T - 0.11f) * 6f)) : 0f)));
                Ui.Shadowed(new Rect(at.X - 90f, at.Y - 11f, 180f, 22f), text, size, c);
            }
        }
    }
}

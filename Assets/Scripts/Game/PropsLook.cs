using UnityEngine;

namespace Arena
{
    /// The look of the arena's props (Props.cs): wooden and iron-bound crates, clay pots, red barrels, the treasure
    /// chest, and your chest item: a card spinning in a beam of its rarity's colour (only you see yours).
    public partial class ArenaApp
    {
        static readonly Rgba PropWood = new Rgba(0.62f, 0.43f, 0.24f), PropWoodDark = new Rgba(0.36f, 0.23f, 0.12f);
        static readonly Rgba PropIron = new Rgba(0.5f, 0.53f, 0.58f), PropGold = new Rgba(1f, 0.8f, 0.3f);
        static readonly Rgba PropClay = new Rgba(0.78f, 0.45f, 0.27f), BarrelRed = new Rgba(0.78f, 0.17f, 0.13f);

        void DrawProps(float t)
        {
            foreach (var p in game.Props.Values)
            {
                // Dropping in: it falls from a little above, then a puff of dust where it lands.
                float drop = Mathf.Clamp01(p.Age / 0.3f), lift = (1f - drop * drop) * 40f;
                float a = Mathf.Clamp01(p.Age / 0.15f);
                if (p.Age < 0.7f && p.Age > 0.25f)
                {
                    float k = (p.Age - 0.25f) / 0.45f;
                    Arc(p.Pos, Game.HalfW(p) + 6f + 22f * k, 3f, new Rgba(0.8f, 0.75f, 0.65f, 0.4f * (1f - k)), 24);
                }
                V2 at = p.Pos - new V2(0f, lift);
                // Its shadow stays on the ground.
                float hw = Game.HalfW(p), hh = Game.HalfH(p);
                if (Game.Round(p))
                    Circle(p.Pos + new V2(3f, 5f), hw, new Rgba(0f, 0f, 0f, 0.3f * a), 20);
                else
                    FillRect(p.Pos.X - hw + 4f, p.Pos.Y - hh + 6f, hw * 2f, hh * 2f, new Rgba(0f, 0f, 0f, 0.3f * a));
                switch (p.Kind)
                {
                    case PropKind.Crate:
                    case PropKind.HeavyCrate:
                        if (fxOn)
                            DrawCrateHolo(p, at, a, t);  // PropsLook.cs: look A "Hologram" of Docs/Mockups/digital-props.html
                        else
                            DrawCrate(p, at, a);
                        break;
                    case PropKind.Pot:
                        if (fxOn)
                            DrawPotHolo(p, at, a, t);
                        else
                            DrawPot(p, at, a, t);
                        break;
                    case PropKind.Barrel:
                        DrawBarrel(p, at, a, t);  // looks A (Hologram) when Visual effects are on
                        break;
                    case PropKind.HealthBarrel:
                        DrawHealthBarrel(p, at, a, t);
                        break;
                    case PropKind.Chest:
                        DrawChest(p, at, a, t);
                        break;
                }
                if (p.HitT > 0f)  // a white flash when it's hit
                {
                    var flash = new Rgba(1f, 1f, 1f, 0.45f * p.HitT / 0.15f);
                    if (Game.Round(p))
                        Circle(at, hw, flash, 20);
                    else
                        FillRect(at.X - hw, at.Y - hh, hw * 2f, hh * 2f, flash);
                }
            }
        }

        // --- Look A "Hologram" (Docs/Mockups/digital-props.html), used while Visual effects are on ---------------------
        // Crates and pots are projected wireframes: a cyan glow, scanlines, edge segments that drop out and flicker as a crate
        // is worn down (pink below a third health), a heavy crate with an inner frame and corner nodes, and a pot that is a
        // ring with a turning dashed band, a pulsing diamond and a scan line. With Visual effects off the wood and clay
        // below draw as before; Reduced effects drops the scanlines, flicker, glitch slice and the pot's motion.

        static readonly Rgba PropHoloCyan = new Rgba(0.31f, 0.89f, 1f), PropHoloBlue = new Rgba(0.56f, 0.82f, 1f), PropHoloPink = new Rgba(1f, 0.42f, 0.6f);

        /// The mockup's hash (so the dropouts fall exactly where the mockup's do), 0..1.
        static float PropHash(int n)
        {
            unchecked
            {
                uint x = (uint)(n ^ (int)0x9e3779b9);
                x *= 0x85ebca6bu;
                x ^= x >> 13;
                x *= 0xc2b2ae35u;
                x ^= x >> 16;
                return x / 4294967296f;
            }
        }

        /// A glowing line: two wide faint passes under the core.
        void GlowLine(V2 a, V2 b, float w, Rgba col)
        {
            Line(a, b, w + 5f, col.WithAlpha(col.A * 0.06f));
            Line(a, b, w + 2.5f, col.WithAlpha(col.A * 0.14f));
            Line(a, b, w, col);
        }

        void DrawCrateHolo(Prop p, V2 c, float al, float t)
        {
            bool heavy = p.Kind == PropKind.HeavyCrate;
            float h = Game.HalfW(p), hp = p.MaxHp > 0f ? p.Hp / p.MaxHp : 1f, dmg = 1f - hp;
            int seed = p.Seed;
            var col = (hp < 0.34f ? PropHoloPink : heavy ? PropHoloBlue : PropHoloCyan).WithAlpha(al);
            // The projected pane and its scanlines.
            FillRect(c.X - h, c.Y - h, h * 2f, h * 2f, (heavy ? PropHoloBlue : PropHoloCyan).WithAlpha((heavy ? 0.09f : 0.07f) * al));
            if (!fxReduced)
                for (float y = -h + 1f; y < h; y += 3f)
                    FillRect(c.X - h, c.Y + y, h * 2f, 1f, PropHoloCyan.WithAlpha(0.10f * al));
            // The frame: 6 segments a side; worn crates lose some and the lost ones flicker back now and then.
            float lw = heavy ? 2f : 1.5f;
            int id = 0, n = 6;
            for (int i = 0; i < n; i++)
            {
                float a = -h + 2f * h * i / n, b = -h + 2f * h * (i + 1) / n;
                HoloSeg(c, a, -h, b, -h, id++, seed, dmg, t, lw, col);
                HoloSeg(c, h, a, h, b, id++, seed, dmg, t, lw, col);
                HoloSeg(c, b, h, a, h, id++, seed, dmg, t, lw, col);
                HoloSeg(c, -h, b, -h, a, id++, seed, dmg, t, lw, col);
            }
            // The brace across, in five pieces.
            float inset = heavy ? 9f : 6f, span = 2f * h - 2f * inset, flip = (seed & 8) != 0 ? -1f : 1f;
            for (int i = 0; i < 5; i++)
            {
                float f1 = i / 5f, f2 = (i + 1) / 5f;
                HoloSeg(c, -h + inset + span * f1, flip * (-h + inset + span * f1), -h + inset + span * f2, flip * (-h + inset + span * f2), id++, seed, dmg, t, 1f, col);
            }
            if (heavy)
            {
                float x0 = -h + 7f, s2 = 2f * h - 14f;
                GlowLine(c + new V2(x0, x0), c + new V2(x0 + s2, x0), 1f, col);
                GlowLine(c + new V2(x0 + s2, x0), c + new V2(x0 + s2, x0 + s2), 1f, col);
                GlowLine(c + new V2(x0 + s2, x0 + s2), c + new V2(x0, x0 + s2), 1f, col);
                GlowLine(c + new V2(x0, x0 + s2), c + new V2(x0, x0), 1f, col);
                for (int i = 0; i < 4; i++)
                {
                    float nx = (i & 1) != 0 ? h - 9f : -h + 5f, ny = (i & 2) != 0 ? h - 9f : -h + 5f;
                    FillRect(nx - 2f, c.Y + ny - 2f, 8f, 8f, col.WithAlpha(col.A * 0.12f));
                    FillRect(c.X + nx, c.Y + ny, 4f, 4f, col);
                }
                GlowLine(c + new V2(-4f, 0f), c + new V2(4f, 0f), 1f, col);
                GlowLine(c + new V2(0f, -4f), c + new V2(0f, 4f), 1f, col);
            }
            // Corner brackets, always whole, so the crate stays readable however worn.
            float L = heavy ? 11f : 9f, bw = 2.8f, e = bw / 2f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) != 0 ? 1f : -1f, sy = (i & 2) != 0 ? 1f : -1f;
                V2 corner = c + new V2(sx * h, sy * h);
                GlowLine(corner + new V2(sx * e, 0f), corner + new V2(-sx * L, 0f), bw, col);
                GlowLine(corner + new V2(0f, sy * e), corner + new V2(0f, -sy * L), bw, col);
            }
            // A worn crate glitches: now and then a bright slice jumps across it.
            if (dmg > 0f && !fxReduced && MathX.Fmod(t * 0.7f + seed * 0.13f, 1f) < 0.07f)
            {
                float gy = -h + PropHash(Mathf.FloorToInt(t * 9f) + seed) * (2f * h - 5f);
                FillRect(c.X - h + 4f, c.Y + gy, 2f * h - 2f, 3f, col.WithAlpha(col.A * 0.4f));
            }
        }

        void HoloSeg(V2 c, float x1, float y1, float x2, float y2, int id, int seed, float dmg, float t, float w, Rgba col)
        {
            bool on = PropHash(seed * 31 + id) > dmg * 0.8f || (!fxReduced && Mathf.Sin(t * 14f + id * 2.3f) > 0.9f);
            if (on)
                GlowLine(c + new V2(x1, y1), c + new V2(x2, y2), w, col);
        }

        void DrawPotHolo(Prop p, V2 c, float al, float t)
        {
            float r = Game.PotR;
            Circle(c, r, PropHoloCyan.WithAlpha(0.10f * al), 24);
            GlowArc(c, r, 1.8f, PropHoloCyan.WithAlpha(al), 32);
            // The dashed inner band turns (dashes 3 long, gaps 3, creeping 18 px a second).
            float rb = r * 0.72f, step = 6f / rb, phase = fxReduced ? 0f : t * 18f / rb;
            int dashes = Mathf.FloorToInt(MathX.Tau / step);
            for (int i = 0; i < dashes; i++)
                Arc(c, rb, 1f, PropHoloCyan.WithAlpha(al), 4, phase + i * step, 3f / rb);
            // The diamond core, pulsing.
            float s = r * 0.3f * (1f + (fxReduced ? 0f : 0.15f * Mathf.Sin(t * 4f)));
            var core = new Rgba(0.73f, 0.96f, 1f, al);
            Circle(c, s * 1.8f, PropHoloCyan.WithAlpha(0.10f * al), 16);
            SetColor(core);
            Quad(c + new V2(0f, -s), c + new V2(s, 0f), c + new V2(0f, s), c + new V2(-s, 0f));
            // A scan line sweeping down the ring.
            float sy = -r + MathX.Fmod((fxReduced ? 0.5f : t * 0.8f) + p.Seed * 0.1f, 1f) * 2f * r, hw = Mathf.Sqrt(Mathf.Max(0f, r * r - sy * sy));
            FillRect(c.X - hw, c.Y + sy, 2f * hw, 1.5f, new Rgba(0.75f, 0.96f, 1f, 0.55f * al));
        }

        /// A glowing ring.
        void GlowArc(V2 c, float r, float w, Rgba col, int segs)
        {
            Arc(c, r, w + 5f, col.WithAlpha(col.A * 0.06f), segs);
            Arc(c, r, w + 2.5f, col.WithAlpha(col.A * 0.14f), segs);
            Arc(c, r, w, col, segs);
        }

        /// A crate or pot smashing with Visual effects on: the hologram's ring bursts and its edges fly apart in
        /// shards (a pot's lasts 1.3 s, the mockup's loop, a crate's the usual 0.5 s).
        void DrawHoloBreakFx(Effect fx, float k, bool pot)
        {
            float al = 1f - k, r = pot ? Game.PotR : fx.Radius / 1.6f;
            var col = PropHoloCyan.WithAlpha(al);
            GlowArc(fx.Pos, r * (1f + k * 1.3f), 1.5f, col, 32);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 0.785f + 0.3f, d = r * (0.4f + k * 2.4f);
                V2 from = fx.Pos + V2.FromAngle(a) * d, to = fx.Pos + V2.FromAngle(a + 0.5f) * (d + 5f);
                GlowLine(from, to, 1.5f, col);
            }
            if (k < 0.25f)
                Circle(fx.Pos, 4f, new Rgba(1f, 1f, 1f, 1f - k * 4f), 12);
        }

        /// Was this Debris a crate or a pot (not the boss fight's green health barrel)? Props.BreakProp sends their colours.
        static bool IsPotDebris(Effect fx) => fx.Color.R > 0.8f && fx.Color.G < 0.5f;
        static bool IsCrateDebris(Effect fx) => fx.Color.R > 0.55f && fx.Color.R < 0.8f && fx.Color.B < 0.45f && fx.Color.G >= 0.49f;

        // --- Look A, round two: barrels and the chest (Docs/Mockups/digital-props.html, "Barrels and the chest") -------

        static readonly Rgba PropHoloRed = new Rgba(1f, 0.35f, 0.35f), PropHoloAmber = new Rgba(1f, 0.82f, 0.23f), PropHoloGold = new Rgba(1f, 0.81f, 0.35f);

        /// A projected disc: a faint fill and (unless Reduced) scanlines across it.
        void HoloDisc(V2 c, float r, Rgba fill, Rgba scan)
        {
            Circle(c, r, fill, 24);
            if (fxReduced)
                return;
            for (float y = -r + 1f; y < r; y += 3f)
            {
                float hw = Mathf.Sqrt(r * r - y * y);
                FillRect(c.X - hw, c.Y + y, 2f * hw, 1f, scan);
            }
        }

        void DrawBarrelHolo(V2 c, float r, float a)
        {
            HoloDisc(c, r, new Rgba(1f, 0.35f, 0.35f, 0.08f * a), new Rgba(1f, 0.55f, 0.55f, 0.10f * a));
            var red = PropHoloRed.WithAlpha(a);
            GlowArc(c, r, 1.8f, red, 32);
            Arc(c, r - 4f, 1f, red.WithAlpha(0.7f * a), 28);
            Arc(c, r * 0.5f, 1f, red.WithAlpha(0.6f * a), 20);
            // The hazard triangle, outlined in amber.
            var am = PropHoloAmber.WithAlpha(a);
            V2 top = c + new V2(0f, -6f), bl = c + new V2(-5.5f, 4f), br = c + new V2(5.5f, 4f);
            GlowLine(top, br, 1.4f, am);
            GlowLine(br, bl, 1.4f, am);
            GlowLine(bl, top, 1.4f, am);
            GlowLine(c + new V2(0f, -2.5f), c + new V2(0f, 0.8f), 1.6f, am);
            Circle(c + new V2(0f, 2.4f), 0.9f, am, 6);
        }

        void DrawChestHolo(Prop p, V2 c, float a, float hw, float hh)
        {
            var col = PropHoloGold.WithAlpha(a);
            var fill = new Rgba(1f, 0.81f, 0.35f, 0.08f * a);
            float lift = 0f;
            if (p.Open)
            {
                lift = 18f * Mathf.Clamp01(p.OpenT / 0.25f);
                // Light pours up out of it.
                for (int i = 0; i < 4; i++)
                {
                    float w = hw * (1.1f - i * 0.22f), up = 70f + i * 20f;
                    SetColor(new Rgba(1f, 0.88f, 0.45f, 0.1f * a));
                    Quad(new V2(c.X - hw * 0.8f, c.Y - hh + 4f), new V2(c.X + hw * 0.8f, c.Y - hh + 4f), new V2(c.X + w, c.Y - hh - up), new V2(c.X - w, c.Y - hh - up));
                }
            }
            FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, fill);
            if (!fxReduced)
                for (float y = -hh + 1f; y < hh; y += 3f)
                    FillRect(c.X - hw, c.Y + y, hw * 2f, 1f, fill.WithAlpha(0.10f * a));
            if (p.Open)
            {
                FillRect(c.X - hw + 5f, c.Y - hh + 4f, hw * 2f - 10f, hh - 2f, new Rgba(1f, 0.89f, 0.47f, 0.55f * a));
                // The lid, lifted away as an outline.
                var lid = col.WithAlpha(0.7f * a);
                float ly = c.Y - hh - lift, lh = hh * 0.9f;
                GlowLine(new V2(c.X - hw, ly), new V2(c.X + hw, ly), 1.2f, lid);
                GlowLine(new V2(c.X + hw, ly), new V2(c.X + hw, ly + lh), 1.2f, lid);
                GlowLine(new V2(c.X + hw, ly + lh), new V2(c.X - hw, ly + lh), 1.2f, lid);
                GlowLine(new V2(c.X - hw, ly + lh), new V2(c.X - hw, ly), 1.2f, lid);
            }
            else
                GlowLine(new V2(c.X - hw, c.Y - hh * 0.15f), new V2(c.X + hw, c.Y - hh * 0.15f), 1.5f, col);  // the lid's seam
            // The frame, the two bands and the corner brackets.
            GlowLine(new V2(c.X - hw, c.Y - hh), new V2(c.X + hw, c.Y - hh), 1.5f, col);
            GlowLine(new V2(c.X + hw, c.Y - hh), new V2(c.X + hw, c.Y + hh), 1.5f, col);
            GlowLine(new V2(c.X + hw, c.Y + hh), new V2(c.X - hw, c.Y + hh), 1.5f, col);
            GlowLine(new V2(c.X - hw, c.Y + hh), new V2(c.X - hw, c.Y - hh), 1.5f, col);
            GlowLine(new V2(c.X - hw * 0.55f, c.Y - hh), new V2(c.X - hw * 0.55f, c.Y + hh), 1f, col);
            GlowLine(new V2(c.X + hw * 0.55f, c.Y - hh), new V2(c.X + hw * 0.55f, c.Y + hh), 1f, col);
            const float L = 9f, e = 1.4f;
            for (int i = 0; i < 4; i++)
            {
                float sx = (i & 1) != 0 ? 1f : -1f, sy = (i & 2) != 0 ? 1f : -1f;
                V2 corner = c + new V2(sx * hw, sy * hh);
                GlowLine(corner + new V2(sx * e, 0f), corner + new V2(-sx * L, 0f), 2.8f, col);
                GlowLine(corner + new V2(0f, sy * e), corner + new V2(0f, -sy * L), 2.8f, col);
            }
            if (!p.Open)
            {
                // The lock: a diamond with a keyhole.
                SetColor(col.WithAlpha(0.2f * a));
                Quad(c + new V2(0f, -6f), c + new V2(6f, 0f), c + new V2(0f, 6f), c + new V2(-6f, 0f));
                GlowLine(c + new V2(0f, -6f), c + new V2(6f, 0f), 1.4f, col);
                GlowLine(c + new V2(6f, 0f), c + new V2(0f, 6f), 1.4f, col);
                GlowLine(c + new V2(0f, 6f), c + new V2(-6f, 0f), 1.4f, col);
                GlowLine(c + new V2(-6f, 0f), c + new V2(0f, -6f), 1.4f, col);
                Circle(c + new V2(0f, -0.5f), 1.6f, new Rgba(1f, 0.95f, 0.77f, a), 8);
                GlowLine(c + new V2(0f, 0.5f), c + new V2(0f, 3.5f), 1.2f, new Rgba(1f, 0.95f, 0.77f, a));
            }
        }

        void DrawCrate(Prop p, V2 c, float a)
        {
            bool heavy = p.Kind == PropKind.HeavyCrate;
            float h = Game.HalfW(p), s = h * 2f, x = c.X - h, y = c.Y - h;
            var wood = (heavy ? PropWood.Darkened(0.25f) : PropWood).WithAlpha(a);
            var dark = PropWoodDark.WithAlpha(a);
            FillRect(x, y, s, s, dark);
            // Three planks, each a slightly different shade, with dark seams between.
            float inset = heavy ? 6f : 4f, pw = (s - inset * 2f) / 3f;
            for (int i = 0; i < 3; i++)
            {
                float shade = ((p.Seed >> i) & 1) == 0 ? 0f : 0.08f;
                FillRect(x + inset, y + inset + i * pw + 0.75f, s - inset * 2f, pw - 1.5f, wood.Darkened(shade));
                FillRect(x + inset, y + inset + i * pw + 0.75f, s - inset * 2f, 1.5f, new Rgba(1f, 0.9f, 0.7f, 0.12f * a));  // a lit edge
            }
            // The brace across, and the frame.
            bool flip = (p.Seed & 8) != 0;
            Line(new V2(x + inset, flip ? y + s - inset : y + inset), new V2(x + s - inset, flip ? y + inset : y + s - inset), heavy ? 6f : 5f, dark);
            Line(new V2(x + inset, flip ? y + s - inset : y + inset), new V2(x + s - inset, flip ? y + inset : y + s - inset), 2f, wood.Mix(new Rgba(1f, 0.9f, 0.7f), 0.15f));
            if (heavy)
            {
                // PropIron bands and corner plates with rivets.
                var iron = PropIron.WithAlpha(a);
                FillRect(x, c.Y - 3f, s, 6f, iron.Darkened(0.2f));
                FillRect(c.X - 3f, y, 6f, s, iron.Darkened(0.2f));
                for (int i = 0; i < 4; i++)
                {
                    float cx = (i & 1) == 0 ? x : x + s - 12f, cy = (i & 2) == 0 ? y : y + s - 12f;
                    FillRect(cx, cy, 12f, 12f, iron);
                    Circle(new V2(cx + 6f, cy + 6f), 2f, new Rgba(0.85f, 0.88f, 0.92f, a), 6);
                }
            }
            // Cracks as it's worn down.
            float hp = p.MaxHp > 0f ? p.Hp / p.MaxHp : 1f;
            var crack = new Rgba(0.12f, 0.07f, 0.03f, 0.9f * a);
            if (hp < 0.67f)
            {
                V2 o = new V2(x + s * 0.3f, y + s * 0.2f);
                Line(o, o + new V2(6f, 8f), 1.5f, crack);
                Line(o + new V2(6f, 8f), o + new V2(3f, 15f), 1.5f, crack);
            }
            if (hp < 0.34f)
            {
                V2 o = new V2(x + s * 0.72f, y + s * 0.62f);
                Line(o, o + new V2(-7f, 5f), 1.5f, crack);
                Line(o + new V2(-7f, 5f), o + new V2(-5f, 13f), 1.5f, crack);
                Line(o + new V2(-7f, 5f), o + new V2(-13f, 4f), 1.5f, crack);
            }
        }

        void DrawPot(Prop p, V2 c, float a, float t)
        {
            float r = Game.PotR;
            Circle(c, r, PropClay.Darkened(0.35f).WithAlpha(a), 20);
            Circle(c + new V2(-1f, -1f), r - 2f, PropClay.WithAlpha(a), 20);
            Circle(c + new V2(-2.5f, -2.5f), r * 0.6f, PropClay.Mix(new Rgba(1f, 0.8f, 0.6f), 0.25f).WithAlpha(a), 16);  // its rounded belly catching the light
            Arc(c, r * 0.78f, 1.6f, new Rgba(0.95f, 0.85f, 0.6f, 0.5f * a), 20, 0.3f, 2.4f);  // a painted band
            Circle(c, r * 0.36f, PropClay.Darkened(0.15f).WithAlpha(a), 12);   // the lip
            Circle(c, r * 0.22f, new Rgba(0.1f, 0.06f, 0.04f, a), 10);         // the mouth
            Circle(c + new V2(-r * 0.5f, -r * 0.4f), 1.8f, new Rgba(1f, 0.95f, 0.85f, 0.6f * a), 8);  // a glint
        }

        void DrawBarrel(Prop p, V2 c, float a, float t)
        {
            float r = Game.BarrelR;
            bool lit = p.Fuse > 0f;
            // Lit: it shakes and flashes faster and faster.
            if (lit)
                c += new V2(Mathf.Sin(t * 90f), Mathf.Cos(t * 77f)) * 1.5f;
            if (fxOn)
                DrawBarrelHolo(c, r, a);
            else
            {
            Circle(c, r, BarrelRed.Darkened(0.4f).WithAlpha(a), 22);
            Circle(c, r - 2f, BarrelRed.WithAlpha(a), 22);
            Arc(c, r - 4f, 2f, PropIron.WithAlpha(a), 22);
            Arc(c, r * 0.5f, 2f, PropIron.Darkened(0.2f).WithAlpha(a), 18);
            // A hazard triangle on the lid.
            var yellow = new Rgba(1f, 0.82f, 0.2f, a);
            V2 top = c + new V2(0f, -6f), bl = c + new V2(-5.5f, 4f), br = c + new V2(5.5f, 4f);
            SetColor(yellow);
            Tri(top, br, bl);
            Line(c + new V2(0f, -2.5f), c + new V2(0f, 0.8f), 1.6f, new Rgba(0.1f, 0.08f, 0.05f, a));
            Circle(c + new V2(0f, 2.4f), 0.9f, new Rgba(0.1f, 0.08f, 0.05f, a), 6);
            }
            if (lit)
            {
                float rate = 1f - Mathf.Clamp01(p.Fuse / Game.BarrelFuse);
                bool on = Mathf.Sin(t * (25f + 40f * rate)) > 0f;
                if (on)
                    Circle(c, r, new Rgba(1f, 0.95f, 0.6f, 0.55f), 22);
                Arc(c, r + 4f + 8f * rate, 2f, new Rgba(1f, 0.6f, 0.2f, 0.8f), 24);
                for (int k = 0; k < 4; k++)  // sparks spitting from the fuse
                {
                    V2 d = V2.FromAngle(t * 13f + k * 1.7f);
                    Line(c + new V2(0f, -r) + d * 2f, c + new V2(0f, -r) + d * 7f, 1.5f, new Rgba(1f, 0.85f, 0.4f, 0.9f));
                }
            }
        }

        /// A boss fight's health barrel: green with iron hoops and a white cross on the lid, glowing softly so it
        /// shows through the bullets.
        void DrawHealthBarrel(Prop p, V2 c, float a, float t)
        {
            float r = Game.BarrelR, pulse = 0.5f + 0.5f * Mathf.Sin(t * 3.5f + p.Id);
            var green = new Rgba(0.25f, 0.68f, 0.35f);
            Circle(c, r + 8f + 3f * pulse, new Rgba(0.4f, 1f, 0.5f, (0.08f + 0.07f * pulse) * a), 24);
            if (fxOn)
            {
                var hg = new Rgba(0.36f, 1f, 0.54f, a);
                HoloDisc(c, r, hg.WithAlpha(0.09f * a), new Rgba(0.59f, 1f, 0.7f, 0.10f * a));
                GlowArc(c, r, 1.8f, hg, 32);
                GlowArc(c, r - 4f, 1f, hg.WithAlpha(0.7f * a), 28);
                var wh = new Rgba(1f, 1f, 1f, a);
                FillRect(c.X - 5.5f, c.Y - 10.5f, 11f, 21f, wh.WithAlpha(0.12f * a));
                FillRect(c.X - 10.5f, c.Y - 5.5f, 21f, 11f, wh.WithAlpha(0.12f * a));
                FillRect(c.X - 2.5f, c.Y - 7.5f, 5f, 15f, wh);
                FillRect(c.X - 7.5f, c.Y - 2.5f, 15f, 5f, wh);
                return;
            }
            Circle(c, r, green.Darkened(0.45f).WithAlpha(a), 22);
            Circle(c, r - 2f, green.WithAlpha(a), 22);
            Arc(c, r - 4f, 2f, PropIron.WithAlpha(a), 22);
            Circle(c + new V2(-r * 0.35f, -r * 0.35f), r * 0.35f, new Rgba(0.6f, 0.95f, 0.65f, 0.25f * a), 12);  // a sheen
            var white = new Rgba(1f, 1f, 1f, a);
            FillRect(c.X - 2.5f, c.Y - 7.5f, 5f, 15f, white);
            FillRect(c.X - 7.5f, c.Y - 2.5f, 15f, 5f, white);
        }

        void DrawChest(Prop p, V2 c, float a, float t)
        {
            float hw = Game.ChestW / 2f, hh = Game.ChestH / 2f;
            float fade = p.Open ? Mathf.Clamp01(1f - (p.OpenT - 1.8f) / 0.7f) : 1f;
            a *= fade;
            if (a <= 0f)
                return;
            // A soft golden glow under it while it waits, and twinkles around it.
            if (!p.Open)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
                Circle(c, hw + 14f + 4f * pulse, new Rgba(1f, 0.8f, 0.3f, 0.08f + 0.06f * pulse), 28);
                for (int k = 0; k < 4; k++)
                {
                    float f = MathX.Fmod(t * 0.7f + k * 0.25f, 1f);
                    V2 s = c + V2.FromAngle(k * 1.9f + p.Seed) * (hw + 6f) + new V2(0f, -18f * f);
                    float sz = 4f * Mathf.Sin(f * Mathf.PI);
                    var tw = new Rgba(1f, 0.95f, 0.7f, 0.9f * Mathf.Sin(f * Mathf.PI));
                    Line(s - new V2(sz, 0f), s + new V2(sz, 0f), 1.4f, tw);
                    Line(s - new V2(0f, sz), s + new V2(0f, sz), 1.4f, tw);
                }
            }
            if (fxOn)
            {
                DrawChestHolo(p, c, a, hw, hh);
                return;
            }
            var wood = new Rgba(0.5f, 0.28f, 0.14f, a);
            var gold = PropGold.WithAlpha(a);
            var goldDark = new Rgba(0.75f, 0.52f, 0.15f, a);
            if (p.Open)
            {
                // Open: the lid stands up behind it, the inside glows, and light pours up out of it.
                float k = Mathf.Clamp01(p.OpenT / 0.25f);
                FillRect(c.X - hw, c.Y - hh - 18f * k, hw * 2f, hh * 0.9f, wood.Darkened(0.2f));
                FillRect(c.X - hw, c.Y - hh - 18f * k, hw * 2f, 3f, gold);
                FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, wood);
                FillRect(c.X - hw + 5f, c.Y - hh + 4f, hw * 2f - 10f, hh - 2f, new Rgba(1f, 0.85f, 0.4f, 0.85f * a));
                for (int i = 0; i < 4; i++)
                {
                    float w = hw * (1.1f - i * 0.22f), up = 70f + i * 20f;
                    SetColor(new Rgba(1f, 0.88f, 0.45f, 0.1f * a));
                    Quad(new V2(c.X - hw * 0.8f, c.Y - hh + 4f), new V2(c.X + hw * 0.8f, c.Y - hh + 4f), new V2(c.X + w, c.Y - hh - up), new V2(c.X - w, c.Y - hh - up));
                }
            }
            else
            {
                FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, wood);
                FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 0.85f, wood.Mix(new Rgba(0.75f, 0.48f, 0.25f), 0.35f));  // the lid
                FillRect(c.X - hw, c.Y - hh + hh * 0.85f - 1.5f, hw * 2f, 3f, goldDark);  // its seam
            }
            // Gold trim: the rim and two bands, and the lock.
            FillRect(c.X - hw, c.Y - hh, hw * 2f, 3f, gold);
            FillRect(c.X - hw, c.Y + hh - 3f, hw * 2f, 3f, goldDark);
            FillRect(c.X - hw, c.Y - hh, 3f, hh * 2f, gold);
            FillRect(c.X + hw - 3f, c.Y - hh, 3f, hh * 2f, goldDark);
            FillRect(c.X - hw * 0.55f - 2.5f, c.Y - hh, 5f, hh * 2f, gold);
            FillRect(c.X + hw * 0.55f - 2.5f, c.Y - hh, 5f, hh * 2f, gold);
            if (!p.Open)
            {
                FillRect(c.X - 6f, c.Y - 4f, 12f, 12f, gold);
                FillRect(c.X - 4f, c.Y - 2f, 8f, 8f, goldDark);
                Circle(c + new V2(0f, 1f), 1.6f, new Rgba(0.1f, 0.06f, 0.02f, a), 6);
                FillRect(c.X - 0.8f, c.Y + 1f, 1.6f, 4f, new Rgba(0.1f, 0.06f, 0.02f, a));
            }
        }

        /// Your chest item (nobody else sees theirs): a card spinning on the spot above a ring, in a beam of its
        /// rarity's colour, its icon on the face and a diamond on the back. Legendaries throw out turning rays.
        void DrawLoot(float t)
        {
            foreach (var l in game.Loots.Values)
            {
                if (l.Owner != game.MyId || l.Upgrade < 0 || l.Upgrade >= Upgrades.All.Length)
                    continue;
                var u = Upgrades.All[l.Upgrade];
                var rc = Upgrades.RarityColor(u.Rarity);
                float grow = Mathf.Clamp01(l.Age / 0.45f), pop = 1f + 0.25f * Mathf.Sin(grow * Mathf.PI);
                bool near = game.MyLootInReach == l;
                // The beam and the ring on the ground.
                for (int i = 0; i < 4; i++)
                {
                    float w = (14f - i * 3f) * grow, up = (80f + i * 25f) * grow;
                    SetColor(rc.WithAlpha(0.07f));
                    Quad(l.Pos + new V2(-w, 0f), l.Pos + new V2(w, 0f), l.Pos + new V2(w * 0.4f, -up), l.Pos + new V2(-w * 0.4f, -up));
                }
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f);
                Arc(l.Pos, 20f * grow, near ? 3f : 2f, rc.WithAlpha(near ? 0.9f : 0.45f + 0.2f * pulse), 28);
                if (near)
                    Arc(l.Pos, 20f + 10f * MathX.Fmod(t * 1.5f, 1f), 2f, rc.WithAlpha(0.6f * (1f - MathX.Fmod(t * 1.5f, 1f))), 28);
                Circle(l.Pos + new V2(0f, 2f), 10f * grow, new Rgba(0f, 0f, 0f, 0.3f), 16);  // its shadow
                V2 c = l.Pos + new V2(0f, -58f - 4f * Mathf.Sin(l.Age * 2.5f) - 20f * (1f - grow));
                if (u.Rarity == Rarity.Legendary)
                    for (int k = 0; k < 6; k++)
                    {
                        V2 d = V2.FromAngle(t * 0.8f + k * MathX.Tau / 6f);
                        SetColor(rc.WithAlpha(0.13f * grow));
                        Tri(c, c + d.Rotated(0.12f) * 46f * grow, c + d.Rotated(-0.12f) * 46f * grow);
                    }
                Circle(c, 28f * grow, rc.WithAlpha(0.12f + 0.06f * pulse), 20);  // a halo
                // The card turns round its upright axis: its width is the cosine of the turn.
                float spin = l.Age * 2.2f, cos = Mathf.Cos(spin);
                float hw = 16f * grow * pop * Mathf.Max(0.08f, Mathf.Abs(cos)), hh = 21f * grow * pop;
                bool face = cos >= 0f;
                FillRect(c.X - hw - 1.5f, c.Y - hh - 1.5f, hw * 2f + 3f, hh * 2f + 3f, rc);
                FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, face ? rc.Darkened(0.78f) : rc.Darkened(0.55f));
                if (face)
                    DrawIconGl(PixelArt.RowsFor(u.Name), c, 27f * grow * pop, cos);
                else
                {
                    // The back: a diamond and a dot.
                    float dw = 9f * Mathf.Abs(cos) * grow, dh = 12f * grow;
                    SetColor(rc.WithAlpha(0.9f));
                    Quad(c + new V2(0f, -dh), c + new V2(dw, 0f), c + new V2(0f, dh), c + new V2(-dw, 0f));
                    Circle(c, 2f * grow, rc.Darkened(0.7f), 8);
                }
                // A sheen across the face as it turns toward you.
                if (Mathf.Abs(cos) > 0.85f)
                    FillRect(c.X - hw, c.Y - hh, hw * 2f, hh * 2f, new Rgba(1f, 1f, 1f, 0.25f * (Mathf.Abs(cos) - 0.85f) / 0.15f));
            }
        }

        /// A pixel-art icon drawn with GL, size px across, squeezed sideways by `squeeze` (a turning card).
        void DrawIconGl(string[] rows, V2 c, float size, float squeeze)
        {
            if (rows == null)
            {
                Circle(c, size * 0.25f, new Rgba(1f, 1f, 1f, 0.8f), 12);
                return;
            }
            int n = rows.Length;
            float px = size / n;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < rows[y].Length; x++)
                {
                    Rgba col;
                    if (!PixelArt.PixelColor(rows[y][x], out col))
                        continue;
                    float x0 = (x * px - size / 2f) * squeeze, x1 = ((x + 1) * px - size / 2f) * squeeze;
                    float lo = Mathf.Min(x0, x1), w = Mathf.Abs(x1 - x0);
                    FillRect(c.X + lo, c.Y - size / 2f + y * px, w + 0.2f, px + 0.2f, col);
                }
        }

        /// A crate or pot smashed: splinters or shards flying out and a puff of dust.
        void DrawDebrisFx(Effect fx, float f)
        {
            if (fxOn && IsPotDebris(fx))
            {
                DrawHoloBreakFx(fx, f, true);
                return;
            }
            if (fxOn && IsCrateDebris(fx))
            {
                DrawHoloBreakFx(fx, f, false);
                return;
            }
            int seed = (int)(fx.Pos.X * 7f + fx.Pos.Y * 13f);
            if (IsPotDebris(fx))
                f = Mathf.Min(1f, fx.T / 0.4f);  // a pot's effect lasts 1.3 s for the hologram's burst; the clay chips keep their 0.4 s
            float ease = 1f - (1f - f) * (1f - f);
            Arc(fx.Pos, fx.Radius * (0.3f + 0.7f * ease), 4f * (1f - f), new Rgba(0.8f, 0.75f, 0.65f, 0.35f * (1f - f)), 24);
            for (int i = 0; i < 10; i++)
            {
                float ang = i * 0.63f + (seed % 17) * 0.37f, dist = fx.Radius * (0.5f + 0.5f * ((seed >> i) & 3) / 3f) * ease;
                V2 at = fx.Pos + V2.FromAngle(ang) * dist + new V2(0f, 10f * f * f);
                V2 along = V2.FromAngle(ang + f * (6f + i)) * (3f + i % 3);
                Line(at - along, at + along, 2.5f, (i % 3 == 0 ? fx.Color.Darkened(0.35f) : fx.Color).WithAlpha(1f - f));
            }
        }

        // --- The HUD: your item's card, and what the wave is waiting for ---------------------------------

        /// Standing on your chest item: its card, with G to take it and X to discard it. Otherwise a line saying
        /// what's left to do (open the chest, fetch your item, or wait for a teammate's).
        void DrawTreasureHud(Player me)
        {
            if (me == null || game.State != MatchState.Fighting || tabHeld)
                return;
            var near = game.MyLootInReach;
            if (near != null)
            {
                DrawLootCard(me, near);
                return;
            }
            bool holding = game.Enemies.Count == 0 && game.Remaining == 0;
            string hint = null;
            if (game.MyLoot != null)
                hint = holding ? "The wave waits: walk onto your chest item to take it or discard it" : "Your chest item is waiting for you";
            else if (holding)
            {
                foreach (var p in game.Props.Values)
                    if (p.Kind == PropKind.Chest && !p.Open)
                        hint = "Touch the chest to open it";
                if (hint == null)
                {
                    var names = new System.Collections.Generic.List<string>();
                    foreach (var l in game.Loots.Values)
                    {
                        Player owner;
                        if (l.Owner != game.MyId && game.Players.TryGetValue(l.Owner, out owner) && !names.Contains(owner.Name))
                            names.Add(owner.Name);
                    }
                    if (names.Count > 0)
                        hint = "Waiting for " + string.Join(" and ", names.ToArray()) + " to deal with their chest item";
                }
            }
            if (hint == null)
                return;
            float pulse = holding ? 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3f) : 0.7f;
            var r = HudChestHint;  // over the XP strip (HudLayout.cs)
            Ui.Pill(r, new Color(0.12f, 0.09f, 0.03f, 0.7f));
            Ui.Label(r, hint, 13, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, pulse), TextAnchor.MiddleCenter, true);
        }

        void DrawLootCard(Player me, Loot l)
        {
            if (l.Upgrade < 0 || l.Upgrade >= Upgrades.All.Length)
                return;
            var u = Upgrades.All[l.Upgrade];
            Color rc = ToColor(Upgrades.RarityColor(u.Rarity));
            float now = Time.unscaledTime;
            // Beside the item (right of it, or left if there's no room), so you still see the arena around you.
            V2 at = ToUi(l.Pos);
            float w = 280f, h = 236f;
            float x = at.X + 46f;
            if (x + w > W - 10f)
                x = at.X - 46f - w;
            var r = new Rect(Mathf.Clamp(x, 10f, W - w - 10f), Mathf.Clamp(at.Y - h / 2f - 20f, 40f, H - h - 100f), w, h);
            Ui.Pill(new Rect(r.x - 6f, r.y - 6f, r.width + 12f, r.height + 12f), new Color(rc.r, rc.g, rc.b, 0.16f + 0.06f * Mathf.Sin(now * 4f)));
            Ui.Card(r, rc, 1f);
            Ui.Pill(new Rect(r.x + 3f, r.y + 3f, r.width - 6f, 92f), new Color(rc.r, rc.g, rc.b, 0.08f));
            if (u.Rarity == Rarity.Legendary)
            {
                float sweep = MathX.Fmod(now * 0.6f, 1.6f) - 0.3f;
                GUI.BeginGroup(r);
                Ui.Fill(new Rect(sweep * r.width, 0f, 18f, r.height), new Color(1f, 0.9f, 0.6f, 0.07f));
                GUI.EndGroup();
            }
            Ui.Label(new Rect(r.x + 12f, r.y + 10f, 120f, 16f), "FROM A CHEST", 10, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f), TextAnchor.MiddleLeft, true);
            string rarity = Upgrades.RarityName(u.Rarity);
            float rw = 24f + rarity.Length * 7.5f;
            var ribbon = new Rect(r.xMax - rw - 12f, r.y + 8f, rw, 20f);
            Ui.Pill(ribbon, new Color(rc.r, rc.g, rc.b, 0.85f));
            Ui.Label(ribbon, rarity, 10, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            var tile = new Rect(r.x + r.width / 2f - 32f, r.y + 34f, 64f, 64f);
            Ui.Pill(new Rect(tile.x - 3f, tile.y - 3f, tile.width + 6f, tile.height + 6f), new Color(rc.r, rc.g, rc.b, 0.3f));
            Ui.Pill(tile, new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f));
            Ui.Icon(new Rect(tile.x + 8f, tile.y + 8f + 2f * Mathf.Sin(now * 3f), 48f, 48f), PixelArt.ForUpgrade(u.Name));
            Ui.Label(new Rect(r.x + 10f, r.y + 104f, r.width - 20f, 24f), u.Name, 18, Color.white, TextAnchor.MiddleCenter, true);
            string desc = Upgrades.DescFor(u, me.Weapon);
            if (u.Grants != AbilityId.None)
                desc += "  (on your first free key; move it in the Tab menu)";
            Ui.Label(new Rect(r.x + 16f, r.y + 132f, r.width - 32f, 62f), desc, desc.Length > 120 ? 11 : 12, Ui.Text, TextAnchor.UpperCenter, false, true);
            // The two keys.
            var take = new Rect(r.x + 16f, r.yMax - 36f, r.width / 2f - 22f, 26f);
            var drop = new Rect(r.x + r.width / 2f + 6f, r.yMax - 36f, r.width / 2f - 22f, 26f);
            Ui.Pill(take, new Color(rc.r, rc.g, rc.b, 0.85f));
            Ui.Label(take, Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant() + "   TAKE", 12, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
            Ui.Pill(drop, new Color(1f, 1f, 1f, 0.1f));
            Ui.Label(drop, Keybinds.KeyName(Keybinds.Get(KeyAction.DiscardItem)).ToUpperInvariant() + "   DISCARD", 12, new Color(1f, 1f, 1f, 0.75f), TextAnchor.MiddleCenter, true);
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// The animated backgrounds behind the menus and lobby. Each player picks their own in the lobby
    /// (saved on their machine), and can freeze it into a still image.
    public partial class ArenaApp
    {
        public static readonly string[] BackgroundNames =
            {
                "Spiral", "Warp", "Aurora", "Synthwave", "Bokeh", "Hex Pulse", "Orbits", "Digital Rain", "Nebula", "Tides",
                "Ember Storm", "Constellations", "Radar", "Blueprint", "Bullet Curtain", "Frost Peaks", "Circuit", "Event Horizon", "Firefly Wood", "Plotter",
            };

        int menuBg;
        bool menuBgAnimate = true;
        bool bgPickerMin;            // the lobby's picker is minimized to a tab that fades away
        float bgTabSeen = -100f;     // when the minimized tab was last shown (minimized or hovered)
        const float BgTabStay = 3f, BgTabFade = 0.8f;

        void LoadBackgroundSettings()
        {
            menuBg = Mathf.Clamp(PlayerPrefs.GetInt("menu_bg", 0), 0, BackgroundNames.Length - 1);
            menuBgAnimate = PlayerPrefs.GetInt("menu_bg_anim", 1) == 1;
            bgPickerMin = PlayerPrefs.GetInt("menu_bg_min", 0) == 1;
        }

        void SaveBackgroundSettings()
        {
            PlayerPrefs.SetInt("menu_bg", menuBg);
            PlayerPrefs.SetInt("menu_bg_anim", menuBgAnimate ? 1 : 0);
            PlayerPrefs.SetInt("menu_bg_min", bgPickerMin ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// Behind the menus, in UI canvas coordinates. A still background is the animation frozen at a nice moment.
        void DrawMenuBackground(float time)
        {
            float t = menuBgAnimate ? time : 20f;
            switch (menuBg)
            {
                case 1: BgWarp(t); break;
                case 2: BgAurora(t); break;
                case 3: BgSynthwave(t); break;
                case 4: BgBokeh(t); break;
                case 5: BgHexPulse(t); break;
                case 6: BgOrbits(t); break;
                case 7: BgDigitalRain(t); break;
                case 8: BgNebula(t); break;
                case 9: BgTides(t); break;
                case 10: BgEmbers(t); break;
                case 11: BgConstellations(t); break;
                case 12: BgRadar(t); break;
                case 13: BgBlueprint(t); break;
                case 14: BgBulletCurtain(t); break;
                case 15: BgFrostPeaks(t); break;
                case 16: BgCircuit(t); break;
                case 17: BgEventHorizon(t); break;
                case 18: BgFireflies(t); break;
                case 19: BgPlotter(t); break;
                default: BgSpiral(t); break;
            }
        }

        /// Each background's own colour: the picker's rim, glow, name and swatch take it.
        static readonly Color[] BackgroundAccents =
        {
            new Color(0.55f, 0.6f, 1f),   // Spiral
            new Color(0.62f, 0.76f, 1f),  // Warp
            new Color(0.3f, 1f, 0.65f),   // Aurora
            new Color(1f, 0.42f, 0.8f),   // Synthwave
            new Color(1f, 0.66f, 0.55f),  // Bokeh
            new Color(0.4f, 0.82f, 1f),   // Hex Pulse
            new Color(1f, 0.8f, 0.4f),    // Orbits
            new Color(0.3f, 1f, 0.55f),   // Digital Rain
            new Color(0.86f, 0.42f, 0.95f), // Nebula
            new Color(0.35f, 0.72f, 1f),  // Tides
            new Color(1f, 0.55f, 0.25f),  // Ember Storm
            new Color(0.6f, 0.72f, 1f),   // Constellations
            new Color(0.3f, 1f, 0.5f),    // Radar
            new Color(0.5f, 0.75f, 1f),   // Blueprint
            new Color(1f, 0.5f, 0.68f),   // Bullet Curtain
            new Color(0.75f, 0.88f, 1f),  // Frost Peaks
            new Color(0.25f, 0.92f, 0.82f), // Circuit
            new Color(1f, 0.68f, 0.35f),  // Event Horizon
            new Color(0.75f, 1f, 0.38f),  // Firefly Wood
            new Color(1f, 0.82f, 0.4f),   // Plotter
        };

        float bgChangedAt = -100f;   // when the background last changed (the name slides in)
        int bgChangeDir = 1;         // which way it slides: +1 from the right (next), -1 from the left (previous)

        static Rgba ToRgba(Color c, float a) => new Rgba(c.r, c.g, c.b, a);

        /// A chevron (two strokes meeting at a point) centred on c, pointing along dir.
        void Chevron(V2 c, V2 dir, float size, float width, Rgba col)
        {
            var side = new V2(-dir.Y, dir.X);
            var tip = c + dir * (size * 0.3f);
            Line(c - dir * (size * 0.3f) + side * (size * 0.5f), tip, width, col);
            Line(c - dir * (size * 0.3f) - side * (size * 0.5f), tip, width, col);
        }

        /// A live thumbnail of the current background, rimmed in its colour.
        void BackgroundThumb(Rect thumb, Color accent, float rimAlpha)
        {
            UiModelAt(new V2(thumb.x, thumb.y), thumb.width / W);
            UiClip(thumb);
            DrawMenuBackground(Time.time);
            UiNoClip();
            UiModelAt(V2.Zero, 1f);
            var edge = ToRgba(accent, rimAlpha);
            Line(new V2(thumb.x, thumb.y), new V2(thumb.xMax, thumb.y), 1.5f, edge);
            Line(new V2(thumb.x, thumb.yMax), new V2(thumb.xMax, thumb.yMax), 1.5f, edge);
            Line(new V2(thumb.x, thumb.y), new V2(thumb.x, thumb.yMax), 1.5f, edge);
            Line(new V2(thumb.xMax, thumb.y), new V2(thumb.xMax, thumb.yMax), 1.5f, edge);
        }

        /// The background's name, sliding in from the side it came from and glowing in its colour.
        void BackgroundName(Rect r, int size, float alpha)
        {
            var accent = BackgroundAccents[menuBg];
            float f = Mathf.Clamp01((Time.unscaledTime - bgChangedAt) / 0.25f);
            f = 1f - (1f - f) * (1f - f);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, alpha * f);
            Ui.Fancy(new Rect(r.x + (1f - f) * 18f * bgChangeDir, r.y, r.width, r.height), BackgroundNames[menuBg], size, Color.white,
                Color.Lerp(accent, Color.white, 0.45f), r.width > 120f ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft, accent);
            GUI.color = old;
        }

        /// The lobby's background picker (bottom left): a live thumbnail of the current background (click it for
        /// the next one), its name between arrows, a swatch per background in its colour (click one to jump to
        /// it), a LIVE / STILL switch for the animation, and a button that folds it into a small tab.
        void DrawBackgroundPicker(Rect r)
        {
            if (bgPickerMin)
            {
                DrawBackgroundTab(r);
                return;
            }
            float now = Time.unscaledTime;
            var mouse = Event.current.mousePosition;
            var accent = BackgroundAccents[menuBg];
            Ui.Card(r, accent, 0.4f + 0.15f * Mathf.Sin(now * 2f));
            var thumb = new Rect(r.x + 10f, r.y + 10f, (r.height - 20f) * W / H, r.height - 20f);
            bool thumbHover = thumb.Contains(mouse);
            float x = thumb.xMax + 14f, w = r.xMax - 12f - x;
            var live = new Rect(r.xMax - 12f - 24f - 6f - 58f, r.y + 7f, 58f, 20f);
            var minimize = new Rect(r.xMax - 12f - 24f, r.y + 7f, 24f, 20f);
            var prev = new Rect(x, r.y + 32f, 26f, 26f);
            var next = new Rect(x + w - 26f, r.y + 32f, 26f, 26f);
            float gap = BackgroundNames.Length > 12 ? 2f : 4f;
            float sw = (w - gap * (BackgroundNames.Length - 1)) / BackgroundNames.Length, sy = r.yMax - 15f;

            if (BeginUiGl())
            {
                BackgroundThumb(thumb, accent, thumbHover ? 0.95f : 0.55f);
                EndUiGl();
            }
            if (thumbHover)
                Ui.Fill(thumb, new Color(0f, 0f, 0f, 0.35f));

            bool liveHover = live.Contains(mouse);
            Ui.Pill(live, menuBgAnimate ? new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, liveHover ? 1f : 0.85f) : new Color(1f, 1f, 1f, liveHover ? 0.14f : 0.07f));
            Ui.Label(new Rect(live.x + 20f, live.y, live.width - 22f, live.height), menuBgAnimate ? "LIVE" : "STILL", 10, menuBgAnimate ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
            // Ui.Button takes the click itself, so act on what it returns (the chevrons are drawn on it below).
            bool clickMin = Ui.Button(minimize, "", Ui.Neutral);
            bool clickPrev = Ui.Button(prev, "", Ui.Neutral);
            bool clickNext = Ui.Button(next, "", Ui.Neutral);
            BackgroundName(new Rect(prev.xMax + 4f, prev.y, next.x - prev.xMax - 8f, prev.height), 16, 1f);

            // A swatch per background, in its colour: the current one lit and taller.
            int swatchHover = -1;
            for (int i = 0; i < BackgroundNames.Length; i++)
            {
                var hit = new Rect(x + i * (sw + gap), sy - 7f, sw + gap, 16f);
                if (hit.Contains(mouse))
                    swatchHover = i;
                var c = BackgroundAccents[i];
                if (i == menuBg)
                {
                    Ui.Pill(new Rect(x + i * (sw + gap) - 3f, sy - 5f, sw + 6f, 14f), new Color(c.r, c.g, c.b, 0.22f + 0.1f * Mathf.Sin(now * 3f)));
                    Ui.Pill(new Rect(x + i * (sw + gap), sy - 2f, sw, 8f), c);
                }
                else
                    Ui.Pill(new Rect(x + i * (sw + gap), sy, sw, 4f), new Color(c.r, c.g, c.b, swatchHover == i ? 0.85f : 0.3f));
            }
            // The label names the swatch under the mouse, in its colour.
            if (swatchHover >= 0 && swatchHover != menuBg)
                Ui.Label(new Rect(x, r.y + 8f, live.x - x - 4f, 18f), BackgroundNames[swatchHover].ToUpperInvariant(), 10, BackgroundAccents[swatchHover], TextAnchor.MiddleLeft, true);
            else
                Ui.Label(new Rect(x, r.y + 8f, live.x - x - 4f, 18f), "BACKGROUND", 10, Ui.Muted, TextAnchor.MiddleLeft, true);

            if (BeginUiGl())
            {
                var ink = new Rgba(0.85f, 0.9f, 1f, 0.9f);
                Chevron(new V2(prev.center.x + 1f, prev.center.y), new V2(-1f, 0f), 10f, 2.5f, ink);
                Chevron(new V2(next.center.x - 1f, next.center.y), new V2(1f, 0f), 10f, 2.5f, ink);
                Chevron(new V2(minimize.center.x, minimize.center.y), new V2(0f, 1f), 9f, 2f, ink);
                var dot = new V2(live.x + 11f, live.center.y);
                if (menuBgAnimate)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(now * 4f);
                    Circle(dot, 5.5f, ToRgba(accent, 0.25f * pulse), 14);
                    Circle(dot, 3f, ToRgba(Color.Lerp(accent, Color.white, 0.3f), 1f), 12);
                }
                else
                {
                    FillRect(dot.X - 3.5f, dot.Y - 3.5f, 2.5f, 7f, new Rgba(0.75f, 0.8f, 0.9f, 0.9f));
                    FillRect(dot.X + 1f, dot.Y - 3.5f, 2.5f, 7f, new Rgba(0.75f, 0.8f, 0.9f, 0.9f));
                }
                if (thumbHover)
                {
                    var tc = new V2(thumb.center.x, thumb.center.y);
                    Circle(tc, 16f, new Rgba(0f, 0f, 0f, 0.45f), 24);
                    Chevron(tc + new V2(1.5f, 0f), new V2(1f, 0f), 12f, 3f, Rgba.White);
                }
                EndUiGl();
            }

            if (clickMin)
            {
                bgPickerMin = true;
                bgTabSeen = now;
                SaveBackgroundSettings();
            }
            if (Ui.HitArea(live))
            {
                menuBgAnimate = !menuBgAnimate;
                SaveBackgroundSettings();
            }
            if (clickPrev)
                SetBackground(menuBg - 1, -1);
            if (clickNext || Ui.HitArea(thumb))
                SetBackground(menuBg + 1, 1);
            for (int i = 0; i < BackgroundNames.Length; i++)
                if (i != menuBg && Ui.HitArea(new Rect(x + i * (sw + gap), sy - 7f, sw + gap, 16f)))
                    SetBackground(i, i > menuBg ? 1 : -1);
        }

        /// The minimized picker: a slim card at the bottom of where the full one was, with a tiny live
        /// thumbnail, the name, quick arrows and an expand button. It stays a few seconds, then fades away
        /// (the thumbnail shrinks into its centre); hovering brings it back, and clicking it opens the full picker.
        void DrawBackgroundTab(Rect card)
        {
            var tab = new Rect(card.x, card.yMax - 38f, 240f, 38f);
            float now = Time.unscaledTime;
            var mouse = Event.current.mousePosition;
            var hot = new Rect(tab.x - 16f, tab.y - 24f, tab.width + 32f, tab.height + 40f);
            if (hot.Contains(mouse))
                bgTabSeen = now;
            float a = 1f - Mathf.Clamp01((now - bgTabSeen - BgTabStay) / BgTabFade);
            if (a <= 0.01f)
                return;
            var accent = BackgroundAccents[menuBg];
            var thumbFull = new Rect(tab.x + 5f, tab.y + 5f, (tab.height - 10f) * W / H, tab.height - 10f);
            var thumb = new Rect(thumbFull.center.x - thumbFull.width * a / 2f, thumbFull.center.y - thumbFull.height * a / 2f, thumbFull.width * a, thumbFull.height * a);
            var prev = new Rect(tab.xMax - 72f, tab.y + 9f, 20f, 20f);
            var next = new Rect(tab.xMax - 50f, tab.y + 9f, 20f, 20f);
            var open = new Rect(tab.xMax - 27f, tab.y + 9f, 20f, 20f);
            float tx = thumbFull.xMax + 9f;

            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            Ui.Card(tab, accent, (0.35f + 0.15f * Mathf.Sin(now * 2f)) * a);
            GUI.color = old;
            if (BeginUiGl())
            {
                if (a > 0.05f)
                    BackgroundThumb(thumb, accent, 0.7f * a);
                EndUiGl();
            }
            GUI.color = new Color(1f, 1f, 1f, a);
            Ui.Label(new Rect(tx, tab.y + 3f, prev.x - tx, 13f), "BACKGROUND", 9, Ui.Muted, TextAnchor.MiddleLeft, true);
            GUI.color = old;
            BackgroundName(new Rect(tx, tab.y + 15f, prev.x - tx - 4f, 20f), 14, a);
            GUI.color = new Color(1f, 1f, 1f, a);
            bool clickPrev = Ui.Button(prev, "", Ui.Neutral);
            bool clickNext = Ui.Button(next, "", Ui.Neutral);
            bool clickOpen = Ui.Button(open, "", Ui.Neutral);
            GUI.color = old;
            if (BeginUiGl())
            {
                var ink = new Rgba(0.85f, 0.9f, 1f, 0.9f * a);
                Chevron(new V2(prev.center.x + 1f, prev.center.y), new V2(-1f, 0f), 8f, 2f, ink);
                Chevron(new V2(next.center.x - 1f, next.center.y), new V2(1f, 0f), 8f, 2f, ink);
                Chevron(new V2(open.center.x, open.center.y + 1f), new V2(0f, -1f), 8f, 2f, ToRgba(Color.Lerp(accent, Color.white, 0.4f), a));
                EndUiGl();
            }
            // The small buttons took their own clicks above, so a click on one doesn't also open the picker.
            if (clickPrev)
                SetBackground(menuBg - 1, -1);
            else if (clickNext)
                SetBackground(menuBg + 1, 1);
            else if (clickOpen || Ui.HitArea(tab))
            {
                bgPickerMin = false;
                SaveBackgroundSettings();
            }
        }

        void SetBackground(int i, int dir = 1)
        {
            menuBg = (i % BackgroundNames.Length + BackgroundNames.Length) % BackgroundNames.Length;
            bgChangedAt = Time.unscaledTime;
            bgChangeDir = dir;
            SaveBackgroundSettings();
        }

        // --- Helpers ------------------------------------------------------------------

        /// A quick repeatable "random" number in 0..1 for index i.
        static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// A quad shaded from `top` (a, b) to `bottom` (c, d).
        void GradQuad(V2 a, V2 b, V2 c, V2 d, Rgba top, Rgba bottom)
        {
            SetColor(top); Vtx(Px(a)); Vtx(Px(b));
            SetColor(bottom); Vtx(Px(c));
            SetColor(top); Vtx(Px(a));
            SetColor(bottom); Vtx(Px(c)); Vtx(Px(d));
        }

        void SoftGlow(V2 at, float r, Rgba c, int rings = 4)
        {
            for (int k = 0; k < rings; k++)
                Circle(at, r * (1f - k / (float)rings), c.WithAlpha(c.A / rings), 32);
        }

        void Stars(int n, float t, float alpha, int seed = 0)
        {
            for (int i = 0; i < n; i++)
            {
                var p = new V2(Hash(seed + i * 3) * W, Hash(seed + i * 3 + 1) * H);
                float tw = 0.5f + 0.5f * Mathf.Sin(t * (1f + Hash(seed + i * 3 + 2) * 2f) + i);
                Circle(p, 0.8f + Hash(i + 7) * 1.2f, new Rgba(0.85f, 0.9f, 1f, alpha * (0.3f + 0.7f * tw)), 6);
            }
        }

        // --- The backgrounds -------------------------------------------------------------

        /// A slow drifting grid and a boss-like spiral of glowing bullets (the original).
        void BgSpiral(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.035f, 0.04f, 0.065f));
            var grid = new Rgba(0.4f, 0.55f, 1f, 0.05f);
            float scroll = MathX.Fmod(t * 12f, 64f);
            for (float x = -64f + scroll; x <= W; x += 64f)
                Line(new V2(x, 0), new V2(x, H), 1f, grid);
            for (float y = -64f + scroll; y <= H; y += 64f)
                Line(new V2(0, y), new V2(W, y), 1f, grid);
            Circle(new V2(W * 0.25f + 60f * Mathf.Sin(t * 0.2f), H * 0.35f), 260f, new Rgba(0.3f, 0.45f, 1f, 0.05f), 48);
            Circle(new V2(W * 0.78f, H * 0.7f + 50f * Mathf.Cos(t * 0.17f)), 300f, new Rgba(0.8f, 0.35f, 0.9f, 0.04f), 48);
            var center = new V2(W / 2f, H / 2f);
            for (int arm = 0; arm < 5; arm++)
                for (int k = 0; k < 14; k++)
                {
                    float dist = MathX.Fmod(t * 60f + k * 52f, 14f * 52f);
                    float ang = t * 0.25f + arm * MathX.Tau / 5f + dist * 0.004f;
                    V2 pos = center + V2.FromAngle(ang) * dist;
                    float a = 0.35f * MathX.Clamp01(dist / 120f) * (1f - dist / (14f * 52f));
                    var c = arm % 2 == 0 ? new Rgba(1f, 0.35f, 0.5f) : new Rgba(0.45f, 0.65f, 1f);
                    Circle(pos, 7f, c.WithAlpha(a * 0.5f), 10);
                    Circle(pos, 3.5f, c.WithAlpha(a), 8);
                }
        }

        /// Stars streaking out from the centre, like a jump to light speed.
        void BgWarp(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.015f, 0.04f));
            var center = new V2(W / 2f, H / 2f);
            SoftGlow(center, 220f, new Rgba(0.35f, 0.45f, 1f, 0.12f));
            const float far = 760f;
            for (int i = 0; i < 160; i++)
            {
                float ang = Hash(i) * MathX.Tau, speed = 0.35f + Hash(i + 500) * 0.65f;
                float d = MathX.Fmod(t * 160f * speed + Hash(i + 900) * far, far);
                float f = d / far;
                V2 dir = V2.FromAngle(ang);
                var c = Hash(i + 77) < 0.25f ? new Rgba(0.6f, 0.75f, 1f) : new Rgba(1f, 1f, 1f);
                Line(center + dir * (d * 0.82f), center + dir * d, 1f + 2f * f, c.WithAlpha(0.7f * f));
            }
        }

        /// Ribbons of green and violet light rippling across a starry sky.
        void BgAurora(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.01f, 0.02f, 0.06f), new Rgba(0.02f, 0.06f, 0.08f));
            Stars(110, t, 0.6f);
            Rgba[] cols = { new Rgba(0.2f, 1f, 0.6f), new Rgba(0.3f, 0.85f, 1f), new Rgba(0.75f, 0.4f, 1f), new Rgba(0.25f, 1f, 0.75f) };
            for (int k = 0; k < 4; k++)
            {
                float baseY = H * (0.28f + 0.1f * k);
                V2 prev = default(V2);
                for (float x = -20f; x <= W + 20f; x += 20f)
                {
                    float y = baseY + Mathf.Sin(x * 0.005f + t * 0.35f * (1f + 0.3f * k) + k) * 55f + Mathf.Sin(x * 0.013f - t * 0.25f + k * 2f) * 18f;
                    var p = new V2(x, y);
                    if (x > -20f)
                    {
                        float shimmer = 0.5f + 0.5f * Mathf.Sin(x * 0.03f + t * 2f + k);
                        var c = cols[k];
                        GradQuad(prev - new V2(0, 90f), p - new V2(0, 90f), p, prev, c.WithAlpha(0f), c.WithAlpha(0.1f + 0.08f * shimmer));
                        GradQuad(prev, p, p + new V2(0, 30f), prev + new V2(0, 30f), c.WithAlpha(0.1f + 0.08f * shimmer), c.WithAlpha(0f));
                    }
                    prev = p;
                }
            }
        }

        /// A neon sun over a perspective grid rolling toward you.
        void BgSynthwave(float t)
        {
            float horizon = H * 0.6f;
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, horizon), new V2(0, horizon), new Rgba(0.04f, 0.01f, 0.1f), new Rgba(0.35f, 0.06f, 0.3f));
            Stars(60, t, 0.5f, 3000);
            var sun = new V2(W / 2f, horizon - 70f);
            SoftGlow(sun, 260f, new Rgba(1f, 0.35f, 0.5f, 0.18f));
            // The sun in horizontal slices, yellow to pink, with gaps that widen and drift down its lower half.
            const float sr = 150f;
            float drift = MathX.Fmod(t * 8f, 22f);
            for (float y = -sr; y < sr; y += 3f)
            {
                if (y > 10f)
                {
                    float band = MathX.Fmod(y - 10f - drift + 220f, 22f), gap = 3f + (y - 10f) / sr * 9f;
                    if (band < gap)
                        continue;
                }
                float yc = y + 1.5f, half = Mathf.Sqrt(Mathf.Max(0f, sr * sr - yc * yc));
                float f = (y + sr) / (2f * sr);
                FillRect(sun.X - half, sun.Y + y, 2f * half, 3f, new Rgba(1f, 0.85f - 0.65f * f, 0.35f + 0.2f * f));
            }
            FillRect(0, horizon, W, H - horizon, new Rgba(0.03f, 0.0f, 0.07f));
            var grid = new Rgba(1f, 0.3f, 0.85f, 0.75f);
            for (int k = -14; k <= 14; k++)  // lines running to the vanishing point
                Line(new V2(W / 2f + k * 18f, horizon), new V2(W / 2f + k * 160f, H), 1.5f, grid.WithAlpha(0.5f));
            for (int k = 0; k < 12; k++)  // cross lines coming toward you
            {
                float z = MathX.Fmod(k / 12f + t * 0.12f, 1f);
                float y = horizon + (H - horizon) * z * z;
                Line(new V2(0, y), new V2(W, y), 1f + 2f * z, grid.WithAlpha(0.2f + 0.6f * z));
            }
            Line(new V2(0, horizon), new V2(W, horizon), 3f, new Rgba(1f, 0.5f, 0.9f, 0.9f));
        }

        /// Soft out-of-focus lights drifting upward.
        void BgBokeh(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.05f, 0.03f, 0.09f), new Rgba(0.02f, 0.04f, 0.07f));
            Rgba[] cols = { new Rgba(1f, 0.5f, 0.6f), new Rgba(0.5f, 0.7f, 1f), new Rgba(1f, 0.8f, 0.4f), new Rgba(0.6f, 1f, 0.8f), new Rgba(0.8f, 0.55f, 1f) };
            for (int i = 0; i < 46; i++)
            {
                float r = 18f + Hash(i + 40) * 80f, speed = 8f + Hash(i + 80) * 22f;
                float y = H + r - MathX.Fmod(t * speed + Hash(i + 120) * (H + 2f * r), H + 2f * r);
                float x = Hash(i) * W + Mathf.Sin(t * 0.4f + i) * 30f;
                var c = cols[i % cols.Length];
                float a = 0.05f + 0.07f * Hash(i + 160);
                Circle(new V2(x, y), r, c.WithAlpha(a), 40);
                Arc(new V2(x, y), r, 1.5f, c.WithAlpha(a * 1.8f), 40);
            }
        }

        /// A honeycomb that lights up in rings rippling out from the middle.
        void BgHexPulse(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.02f, 0.03f, 0.05f));
            const float r = 34f;
            float w = r * 1.5f, h = r * 1.732f;
            var center = new V2(W / 2f, H / 2f);
            for (int col = -1; col * w < W + r; col++)
                for (int row = -1; row * h < H + r; row++)
                {
                    var p = new V2(col * w, row * h + (col % 2 == 0 ? 0f : h / 2f));
                    float d = p.DistanceTo(center);
                    float wave = Mathf.Max(0f, Mathf.Sin(d * 0.018f - t * 1.8f));
                    wave *= wave * wave;
                    var c = new Rgba(0.3f + 0.4f * wave, 0.7f + 0.2f * wave, 1f, 0.06f + 0.4f * wave);
                    if (wave > 0.3f)
                        Circle(p, r * 0.86f, c.WithAlpha(0.08f * wave), 6);
                    Arc(p, r - 2f, 1.5f, c, 6, MathX.Pi / 6f, MathX.Tau);
                }
        }

        /// A little star system: glowing sun, faint orbits, planets and moons going round.
        void BgOrbits(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.015f, 0.02f, 0.045f));
            Stars(130, t, 0.55f, 7000);
            var sun = new V2(W * 0.5f, H * 0.52f);
            SoftGlow(sun, 160f, new Rgba(1f, 0.75f, 0.35f, 0.25f), 6);
            Circle(sun, 26f, new Rgba(1f, 0.9f, 0.6f), 40);
            Rgba[] cols = { new Rgba(0.6f, 0.75f, 1f), new Rgba(1f, 0.55f, 0.45f), new Rgba(0.5f, 1f, 0.7f), new Rgba(0.9f, 0.7f, 1f), new Rgba(1f, 0.85f, 0.5f), new Rgba(0.45f, 0.85f, 1f) };
            for (int k = 0; k < 6; k++)
            {
                float rad = 80f + k * 62f;
                Arc(sun, rad, 1f, new Rgba(1f, 1f, 1f, 0.07f), 96);
                float ang = t * (0.9f / (1f + k * 0.7f)) + k * 1.3f;
                var p = sun + V2.FromAngle(ang) * rad;
                float pr = 5f + Hash(k + 30) * 9f;
                SoftGlow(p, pr * 3f, cols[k].WithAlpha(0.2f), 3);
                Circle(p, pr, cols[k], 24);
                if (k % 2 == 1)  // a moon
                    Circle(p + V2.FromAngle(t * 2.5f + k) * (pr + 9f), 2.5f, new Rgba(0.85f, 0.88f, 0.95f), 10);
            }
        }

        /// Columns of glowing glyph blocks raining down.
        void BgDigitalRain(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.0f, 0.02f, 0.02f));
            const float step = 22f;
            for (int c = 0; c * step < W; c++)
            {
                float speed = 60f + Hash(c + 300) * 140f, len = 8 + (int)(Hash(c + 400) * 14f);
                float head = MathX.Fmod(t * speed + Hash(c + 500) * (H + 400f), H + 400f) - 100f;
                for (int k = 0; k < len; k++)
                {
                    float y = head - k * 16f;
                    if (y < -16f || y > H)
                        continue;
                    float f = 1f - k / len;
                    bool bright = k == 0;
                    float flick = Hash(c * 31 + k + (int)(t * 6f)) * 0.4f;
                    var col = bright ? new Rgba(0.8f, 1f, 0.9f, 0.9f) : new Rgba(0.2f, 1f, 0.55f, (0.15f + 0.45f * f) * (0.6f + flick));
                    FillRect(c * step + 6f, y, 9f, 11f, col);
                }
            }
        }

        /// Big soft clouds of colour slowly turning, with twinkling stars.
        void BgNebula(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.02f, 0.01f, 0.04f));
            Rgba[] cols = { new Rgba(0.9f, 0.3f, 0.7f), new Rgba(0.3f, 0.5f, 1f), new Rgba(0.2f, 0.9f, 0.85f), new Rgba(0.6f, 0.3f, 1f), new Rgba(1f, 0.55f, 0.35f) };
            for (int i = 0; i < 9; i++)
            {
                var anchor = new V2(Hash(i + 1000) * W, Hash(i + 1100) * H);
                var p = anchor + V2.FromAngle(t * 0.08f * (i % 2 == 0 ? 1f : -1f) + i) * 70f;
                SoftGlow(p, 200f + Hash(i + 1200) * 160f, cols[i % cols.Length].WithAlpha(0.13f), 6);
            }
            Stars(200, t, 0.8f, 1300);
        }

        /// Layered wavy lines, like a contour map of rolling water.
        void BgTides(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.01f, 0.04f, 0.07f), new Rgba(0.02f, 0.02f, 0.06f));
            for (int k = 0; k < 20; k++)
            {
                float baseY = 20f + k * 36f;
                var c = new Rgba(0.2f + 0.02f * k, 0.6f + 0.015f * k, 1f - 0.02f * k, 0.12f + 0.012f * k);
                V2 prev = default(V2);
                for (float x = 0f; x <= W; x += 16f)
                {
                    float y = baseY + Mathf.Sin(x * 0.006f + t * 0.5f + k * 0.45f) * 22f + Mathf.Sin(x * 0.017f - t * 0.8f + k) * 7f;
                    var p = new V2(x, y);
                    if (x > 0f)
                        Line(prev, p, 1.6f, c);
                    prev = p;
                }
            }
        }

        // --- Added Oct 2026 (mocked up first in a web page, ported shape for shape) ---

        /// Sparks rise from a glowing forge below, drifting on the wind and cooling from yellow to red as they climb.
        void BgEmbers(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.03f, 0.01f, 0.02f), new Rgba(0.2f, 0.045f, 0.02f));
            SoftGlow(new V2(W / 2f, H + 120f), 620f, new Rgba(1f, 0.35f, 0.1f, 0.22f), 6);
            for (int i = 0; i < 150; i++)
            {
                float speed = 30f + Hash(i + 50) * 80f, span = H + 40f;
                float y = H + 20f - MathX.Fmod(t * speed + Hash(i + 90) * span, span);
                float f = 1f - y / H;
                float x = MathX.Fmod(Hash(i) * W + Mathf.Sin(t * 0.8f + i) * 25f + f * 160f, W);
                float r = 1.2f + Hash(i + 130) * 2.4f, flick = 0.65f + 0.35f * Mathf.Sin(t * 9f + i * 1.7f);
                var c = new Rgba(1f, 0.8f - 0.5f * f, Mathf.Max(0f, 0.35f - 0.3f * f));
                var p = new V2(x, y);
                Circle(p, r * 4f, c.WithAlpha(Mathf.Clamp01(0.08f * (1f - f) * flick)), 10);
                Circle(p, r, c.WithAlpha(Mathf.Clamp01((1f - f * 0.85f) * flick)), 8);
            }
        }

        readonly V2[] constellationPts = new V2[42];

        /// Bright stars wander slowly and link up with faint lines whenever two get close.
        void BgConstellations(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.015f, 0.02f, 0.05f));
            Stars(160, t, 0.45f, 2000);
            var pts = constellationPts;
            for (int i = 0; i < pts.Length; i++)
            {
                float s = 0.6f + Hash(i + 300) * 0.8f;
                pts[i] = new V2(Hash(i + 100) * W + Mathf.Sin(t * 0.1f * s + i) * 70f, Hash(i + 200) * H + Mathf.Cos(t * 0.13f * s + i * 1.7f) * 45f);
            }
            for (int a = 0; a < pts.Length; a++)
                for (int b = a + 1; b < pts.Length; b++)
                {
                    float d = pts[a].DistanceTo(pts[b]);
                    if (d < 180f)
                        Line(pts[a], pts[b], 1.5f, new Rgba(0.55f, 0.7f, 1f, 0.5f * (1f - d / 180f)));
                }
            for (int k = 0; k < pts.Length; k++)
            {
                float tw = 0.6f + 0.4f * Mathf.Sin(t * 1.5f + k * 2.1f);
                Circle(pts[k], 9f, new Rgba(0.6f, 0.75f, 1f, 0.08f * tw), 14);
                Circle(pts[k], 2.4f, new Rgba(0.85f, 0.92f, 1f, tw), 8);
            }
        }

        /// A green radar sweep; blips (a few red) light up as the beam passes, then fade.
        void BgRadar(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0f, 0.03f, 0.02f));
            var center = new V2(W / 2f, H / 2f);
            var g = new Rgba(0.2f, 1f, 0.5f);
            for (float r = 90f; r <= 450f; r += 90f)
                Arc(center, r, 1f, g.WithAlpha(0.14f), 96);
            Line(new V2(center.X - 470f, center.Y), new V2(center.X + 470f, center.Y), 1f, g.WithAlpha(0.1f));
            Line(new V2(center.X, 0), new V2(center.X, H), 1f, g.WithAlpha(0.1f));
            float a = t * 1.1f;
            for (int k = 0; k < 36; k++)
                Line(center, center + V2.FromAngle(a - k * 0.018f) * 470f, 4f, g.WithAlpha(0.2f * (1f - k / 36f)));
            Line(center, center + V2.FromAngle(a) * 470f, 2f, new Rgba(0.6f, 1f, 0.75f, 0.8f));
            for (int i = 0; i < 16; i++)
            {
                float ba = Hash(i + 700) * MathX.Tau + t * 0.04f * (i % 2 == 1 ? 1f : -1f), br = 60f + Hash(i + 800) * 390f;
                float lit = (float)System.Math.Exp(-MathX.Fmod(a - ba, MathX.Tau) * 0.9f);
                var c = Hash(i + 900) < 0.3f ? new Rgba(1f, 0.35f, 0.35f) : g;
                var p = center + V2.FromAngle(ba) * br;
                Circle(p, 14f, c.WithAlpha(0.12f * lit), 16);
                Circle(p, 4f, c.WithAlpha(0.15f + 0.85f * lit), 10);
            }
        }

        /// An arena drafted on blueprint paper: a circle, a hexagon and a measured wall draw themselves in, then clear.
        void BgBlueprint(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.04f, 0.11f, 0.26f));
            for (float x = 0; x <= W; x += 16f)
                Line(new V2(x, 0), new V2(x, H), 1f, new Rgba(1f, 1f, 1f, x % 80f == 0f ? 0.09f : 0.035f));
            for (float y = 0; y <= H; y += 16f)
                Line(new V2(0, y), new V2(W, y), 1f, new Rgba(1f, 1f, 1f, y % 80f == 0f ? 0.09f : 0.035f));
            const float cyc = 9f;
            float p = MathX.Fmod(t, cyc), fade = p > cyc - 1f ? cyc - p : 1f;
            var ink = new Rgba(0.85f, 0.93f, 1f);
            var center = new V2(W * 0.42f, H * 0.5f);
            float pc = Mathf.Clamp01(p / 2.5f);
            if (pc > 0f)
                Arc(center, 230f, 2f, ink.WithAlpha(0.75f * fade), 96, -MathX.Pi / 2f, MathX.Tau * pc);
            if (pc > 0f && pc < 1f)
                Circle(center + V2.FromAngle(-MathX.Pi / 2f + MathX.Tau * pc) * 230f, 4f, ink.WithAlpha(fade), 10);
            float segs = 6f * Mathf.Clamp01((p - 1.5f) / 2.5f);
            for (int k = 0; k < 6; k++)
            {
                float f = Mathf.Clamp01(segs - k);
                if (f <= 0f)
                    break;
                V2 a0 = center + V2.FromAngle(k * MathX.Tau / 6f) * 140f, a1 = center + V2.FromAngle((k + 1) * MathX.Tau / 6f) * 140f;
                Line(a0, a0 + (a1 - a0) * f, 1.5f, ink.WithAlpha(0.6f * fade));
            }
            float axes = 0.25f * fade * Mathf.Clamp01(p - 0.5f);
            Line(center - new V2(260f, 0), center + new V2(260f, 0), 1f, ink.WithAlpha(axes));
            Line(center - new V2(0, 260f), center + new V2(0, 260f), 1f, ink.WithAlpha(axes));
            float pw = Mathf.Clamp01((p - 3.5f) / 2f), wx = W * 0.76f, wy0 = H * 0.2f, wy1 = H * 0.8f;
            if (pw > 0f)
                Line(new V2(wx, wy0), new V2(wx, wy0 + (wy1 - wy0) * pw), 3f, ink.WithAlpha(0.75f * fade));
            float pd = Mathf.Clamp01(p - 5.5f);
            if (pd > 0f)
            {
                var gold = new Rgba(1f, 0.8f, 0.4f, 0.8f * fade);
                Line(new V2(wx + 40f, wy0), new V2(wx + 40f, wy0 + (wy1 - wy0) * pd), 1f, gold);
                Line(new V2(wx + 30f, wy0), new V2(wx + 50f, wy0), 1f, gold);
                if (pd >= 1f)
                    Line(new V2(wx + 30f, wy1), new V2(wx + 50f, wy1), 1f, gold);
            }
        }

        /// Three emitters fire slow, rotating rings of bullets that overlap into a calm danmaku pattern.
        void BgBulletCurtain(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.03f, 0.02f, 0.06f));
            V2[] at = { new V2(W * 0.24f, H * 0.34f), new V2(W * 0.76f, H * 0.3f), new V2(W * 0.5f, H * 0.72f) };
            Rgba[] cols = { new Rgba(1f, 0.45f, 0.65f), new Rgba(0.45f, 0.8f, 1f), new Rgba(1f, 0.82f, 0.4f) };
            const float period = 1.5f;
            for (int e = 0; e < 3; e++)
            {
                var c = cols[e];
                var core = new Rgba(Mathf.Min(1f, c.R + 0.3f), Mathf.Min(1f, c.G + 0.3f), Mathf.Min(1f, c.B + 0.3f));
                SoftGlow(at[e], 70f, c.WithAlpha(0.25f));
                Circle(at[e], 9f, c.WithAlpha(0.9f), 16);
                float te = t + e * 0.5f;
                int last = Mathf.FloorToInt(te / period);
                for (int n = 0; n < 8; n++)
                {
                    int shot = last - n;
                    float rad = (te - shot * period) * 85f;
                    float al = Mathf.Max(0f, 1f - rad / 760f) * Mathf.Min(1f, rad / 40f);
                    float spin = shot * 0.21f * (e == 1 ? -1f : 1f);
                    for (int b = 0; b < 22; b++)
                    {
                        var p = at[e] + V2.FromAngle(spin + b * MathX.Tau / 22f) * rad;
                        Circle(p, 7f, c.WithAlpha(0.18f * al), 10);
                        Circle(p, 3.2f, core.WithAlpha(0.85f * al), 8);
                    }
                }
            }
        }

        /// Moonlit mountain ranges in three layers drift past at different speeds, with snow falling in front.
        void BgFrostPeaks(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.02f, 0.03f, 0.08f), new Rgba(0.1f, 0.14f, 0.24f));
            Stars(90, t, 0.5f, 4000);
            var moon = new V2(W * 0.78f, H * 0.2f);
            SoftGlow(moon, 170f, new Rgba(0.75f, 0.85f, 1f, 0.16f), 6);
            Circle(moon, 40f, new Rgba(0.92f, 0.95f, 1f), 40);
            float[] bases = { 0.42f, 0.58f, 0.74f }, speeds = { 6f, 14f, 28f }, amps = { 140f, 110f, 90f };
            Rgba[] cols = { new Rgba(0.14f, 0.18f, 0.3f), new Rgba(0.08f, 0.1f, 0.19f), new Rgba(0.03f, 0.04f, 0.09f) };
            for (int l = 0; l < 3; l++)
            {
                float baseY = H * bases[l], off = t * speeds[l], amp = amps[l];
                V2 prev = default(V2);
                for (float x = -20f; x <= W + 20f; x += 20f)
                {
                    float u = x + off;
                    var p = new V2(x, baseY - Mathf.Abs(Mathf.Sin(u * 0.004f + l * 2f)) * amp - Mathf.Sin(u * 0.011f + l) * amp * 0.25f);
                    if (x > -20f)
                        GradQuad(prev, p, new V2(p.X, H), new V2(prev.X, H), cols[l], cols[l]);
                    prev = p;
                }
            }
            for (int i = 0; i < 130; i++)
            {
                float depth = 0.4f + Hash(i + 1500) * 0.6f, span = H + 20f;
                float fy = MathX.Fmod(t * (25f + depth * 55f) + Hash(i + 1600) * span, span) - 10f;
                float fx = MathX.Fmod(Hash(i + 1700) * W + Mathf.Sin(t * 0.7f + i) * 18f * depth - t * 12f * depth, W);
                Circle(new V2(fx, fy), 1f + depth * 2f, new Rgba(0.9f, 0.95f, 1f, 0.35f + 0.5f * depth), 8);
            }
        }

        // Circuit traces: built once (always the same), then pulses run along them.
        static System.Collections.Generic.List<V2[]> circuitPaths;
        static float[] circuitLens;

        static void BuildCircuit()
        {
            circuitPaths = new System.Collections.Generic.List<V2[]>();
            const float step = 40f;
            float[] sx = { step, 0f, -step, 0f }, sy = { 0f, step, 0f, -step };
            for (int i = 0; i < 46; i++)
            {
                float x = (float)System.Math.Floor(Hash(i + 2000) * (W / step)) * step, y = (float)System.Math.Floor(Hash(i + 2100) * (H / step)) * step;
                int dir = Hash(i + 2200) < 0.5f ? 0 : 1, n = 6 + (int)(Hash(i + 2300) * 10f);
                var pts = new V2[n + 1];
                pts[0] = new V2(x, y);
                for (int k = 0; k < n; k++)
                {
                    float r = Hash(i * 31 + k + 2400);
                    if (r < 0.25f) dir = (dir + 1) % 4;
                    else if (r < 0.35f) dir = (dir + 3) % 4;
                    float dx = sx[dir], dy = sy[dir];
                    if (r > 0.8f)  // a 45 degree jog
                    {
                        dx = step * (dir % 2 == 0 ? (dir == 0 ? 1f : -1f) : 1f);
                        dy = step * (dir % 2 == 0 ? 1f : (dir == 1 ? 1f : -1f));
                    }
                    x += dx;
                    y += dy;
                    pts[k + 1] = new V2(x, y);
                }
                circuitPaths.Add(pts);
            }
            circuitLens = new float[circuitPaths.Count];
            for (int i = 0; i < circuitPaths.Count; i++)
                for (int m = 1; m < circuitPaths[i].Length; m++)
                    circuitLens[i] += circuitPaths[i][m].DistanceTo(circuitPaths[i][m - 1]);
        }

        static V2 AlongPath(V2[] pts, float s)
        {
            for (int m = 1; m < pts.Length; m++)
            {
                float d = pts[m].DistanceTo(pts[m - 1]);
                if (s <= d)
                    return pts[m - 1] + (pts[m] - pts[m - 1]) * (s / d);
                s -= d;
            }
            return pts[pts.Length - 1];
        }

        /// Teal circuit traces on a dark board, with pulses of light racing along them to the solder pads.
        void BgCircuit(float t)
        {
            if (circuitPaths == null)
                BuildCircuit();
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.04f, 0.045f));
            var c = new Rgba(0.2f, 0.9f, 0.8f);
            for (int i = 0; i < circuitPaths.Count; i++)
            {
                var pts = circuitPaths[i];
                float len = circuitLens[i];
                for (int m = 1; m < pts.Length; m++)
                    Line(pts[m - 1], pts[m], 2f, c.WithAlpha(0.13f));
                Arc(pts[0], 5f, 2f, c.WithAlpha(0.35f), 12);
                Arc(pts[pts.Length - 1], 5f, 2f, c.WithAlpha(0.35f), 12);
                float s = MathX.Fmod(t * 140f * (0.6f + Hash(i + 2500) * 0.9f) + Hash(i + 2600) * (len + 300f), len + 300f);
                if (s > len)
                    continue;
                for (int k = 0; k < 8; k++)
                    Circle(AlongPath(pts, Mathf.Max(0f, s - k * 6f)), 3f - k * 0.25f, c.WithAlpha(0.9f * (1f - k / 8f)), 8);
                Circle(AlongPath(pts, s), 14f, c.WithAlpha(0.12f), 14);
                if (len - s < 30f)
                    Circle(pts[pts.Length - 1], 9f, c.WithAlpha(0.5f), 12);
            }
        }

        /// A black hole with a tilted, glowing accretion disk; matter spirals in, in front of and behind it.
        void BgEventHorizon(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.01f, 0.005f, 0.02f));
            Stars(150, t, 0.5f, 5000);
            var center = new V2(W / 2f, H / 2f);
            SoftGlow(center, 320f, new Rgba(1f, 0.5f, 0.2f, 0.1f), 6);
            AccretionDisk(center, t, false);
            Circle(center, 82f, new Rgba(0f, 0f, 0f), 48);
            Arc(center, 86f, 3f, new Rgba(1f, 0.8f, 0.5f, 0.85f), 64);
            Arc(center, 94f, 6f, new Rgba(1f, 0.6f, 0.3f, 0.18f), 64);
            AccretionDisk(center, t, true);
        }

        void AccretionDisk(V2 center, float t, bool front)
        {
            for (int i = 0; i < 650; i++)
            {
                float f = 1f - MathX.Fmod(Hash(i + 3000) + t * 0.012f * (0.7f + Hash(i + 3100) * 0.6f), 1f);
                float r = 95f + 340f * f, ang = Hash(i + 3200) * MathX.Tau + t * 2.6f * Mathf.Pow(95f / r, 1.5f);
                float sy = Mathf.Sin(ang);
                if ((sy > 0f) != front)
                    continue;
                var c = new Rgba(1f, 0.85f - 0.45f * f, 0.55f - 0.45f * f, 0.15f + 0.7f * (1f - f));
                Circle(center + new V2(Mathf.Cos(ang) * r, sy * r * 0.32f), 1.2f + (1f - f) * 1.6f, c, 6);
            }
        }

        /// A dark pine forest at night; fireflies wander between the trees and blink on and off.
        void BgFireflies(float t)
        {
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, H), new V2(0, H), new Rgba(0.01f, 0.035f, 0.04f), new Rgba(0.02f, 0.09f, 0.06f));
            Stars(50, t, 0.35f, 6000);
            Pines(22, 6100, H * 0.82f, new Rgba(0.04f, 0.11f, 0.08f), 0.8f);
            FillRect(0, H * 0.82f, W, H * 0.18f, new Rgba(0.02f, 0.06f, 0.04f));
            for (int i = 0; i < 70; i++)
            {
                float s1 = 0.5f + Hash(i + 6400), s2 = 0.5f + Hash(i + 6500);
                var p = new V2(Hash(i + 6200) * W + Mathf.Sin(t * 0.3f * s1 + i) * 90f, H * 0.3f + Hash(i + 6300) * H * 0.62f + Mathf.Sin(t * 0.4f * s2 + i * 2f) * 50f);
                float blink = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * (1f + Hash(i + 6600)) * 1.4f + i * 3f)), 4f);
                Circle(p, 20f, new Rgba(0.75f, 1f, 0.35f, 0.14f * blink), 16);
                Circle(p, 2.2f, new Rgba(0.9f, 1f, 0.6f, 0.15f + 0.85f * blink), 8);
            }
            Pines(9, 6700, H + 30f, new Rgba(0.005f, 0.02f, 0.015f), 1.5f);
        }

        void Pines(int n, int seed, float baseY, Rgba col, float scale)
        {
            for (int i = 0; i < n; i++)
            {
                float x = Hash(seed + i) * W, h = (160f + Hash(seed + i + 50) * 160f) * scale, w = h * 0.36f;
                FillRect(x - 4f * scale, baseY - h * 0.25f, 8f * scale, h * 0.25f + 2f, col);
                for (int k = 0; k < 3; k++)
                {
                    float ty = baseY - h * 0.2f - k * h * 0.25f, tw = w * (1f - k * 0.22f);
                    var tip = new V2(x, ty - h * 0.42f);
                    GradQuad(tip, tip, new V2(x + tw / 2f, ty), new V2(x - tw / 2f, ty), col, col);
                }
            }
        }

        /// Graph paper with x and y axes through the middle; four curves are plotted live, left to right.
        void BgPlotter(float t)
        {
            FillRect(0, 0, W, H, new Rgba(0.03f, 0.035f, 0.06f));
            var grid = new Rgba(0.5f, 0.6f, 1f, 0.05f);
            for (float x = 0; x <= W; x += 40f)
                Line(new V2(x, 0), new V2(x, H), 1f, grid);
            for (float y = 0; y <= H; y += 40f)
                Line(new V2(0, y), new V2(W, y), 1f, grid);
            float cx = W / 2f, cy = H / 2f;
            var axis = new Rgba(1f, 1f, 1f, 0.35f);
            Line(new V2(0, cy), new V2(W, cy), 1.5f, axis);
            Line(new V2(cx, 0), new V2(cx, H), 1.5f, axis);
            for (float x = 0; x <= W; x += 40f)
                Line(new V2(x, cy - 5f), new V2(x, cy + 5f), 1f, axis.WithAlpha(0.3f));
            for (float y = 0; y <= H; y += 40f)
                Line(new V2(cx - 5f, y), new V2(cx + 5f, y), 1f, axis.WithAlpha(0.3f));
            Rgba[] cols = { new Rgba(1f, 0.45f, 0.6f), new Rgba(0.45f, 0.7f, 1f), new Rgba(1f, 0.82f, 0.4f), new Rgba(0.45f, 1f, 0.7f) };
            for (int k = 0; k < 4; k++)
            {
                float pr = MathX.Fmod(t * 0.1f + k * 0.27f, 1.3f), end = Mathf.Min(1f, pr) * W, al = pr > 1f ? 1f - (pr - 1f) / 0.3f : 1f;
                V2 prev = default(V2);
                bool any = false;
                for (float sx = 0f; sx <= end; sx += 6f)
                {
                    var p = new V2(sx, cy - PlotFn(k, (sx - cx) / 60f) * 80f);
                    if (any)
                        Line(prev, p, 2.5f, cols[k].WithAlpha(0.85f * al));
                    prev = p;
                    any = true;
                }
                if (any && pr < 1f)
                {
                    Circle(prev, 14f, cols[k].WithAlpha(0.2f), 16);
                    Circle(prev, 4f, cols[k], 10);
                }
            }
        }

        static float PlotFn(int k, float u)
        {
            switch (k)
            {
                case 0: return Mathf.Sin(u);
                case 1: return 0.35f * u * Mathf.Cos(u) / 3f;
                case 2: return u == 0f ? 2.4f : 2.4f * Mathf.Sin(u) / u;
                default: return Mathf.Sin(u * 0.5f) + 0.5f * Mathf.Sin(u * 1.7f);
            }
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// The animated backgrounds behind the menus and lobby. Each player picks their own in the lobby
    /// (saved on their machine), and can freeze it into a still image.
    public partial class ArenaApp
    {
        public static readonly string[] BackgroundNames =
            { "Spiral", "Warp", "Aurora", "Synthwave", "Bokeh", "Hex Pulse", "Orbits", "Digital Rain", "Nebula", "Tides" };

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
            const float gap = 4f;
            float sw = (w - gap * (BackgroundNames.Length - 1)) / BackgroundNames.Length, sy = r.yMax - 15f;

            if (BeginUiGl())
            {
                BackgroundThumb(thumb, accent, thumbHover ? 0.95f : 0.55f);
                EndUiGl();
            }
            if (thumbHover)
                Ui.Fill(thumb, new Color(0f, 0f, 0f, 0.35f));

            Ui.Label(new Rect(x, r.y + 8f, 100f, 18f), "BACKGROUND", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            bool liveHover = live.Contains(mouse);
            Ui.Pill(live, menuBgAnimate ? new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, liveHover ? 1f : 0.85f) : new Color(1f, 1f, 1f, liveHover ? 0.14f : 0.07f));
            Ui.Label(new Rect(live.x + 20f, live.y, live.width - 22f, live.height), menuBgAnimate ? "LIVE" : "STILL", 10, menuBgAnimate ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Button(minimize, "", Ui.Neutral);
            Ui.Button(prev, "", Ui.Neutral);
            Ui.Button(next, "", Ui.Neutral);
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
            if (swatchHover >= 0 && swatchHover != menuBg)  // the swatch under the mouse names itself where the label was
                Ui.Label(new Rect(x + 90f, r.y + 8f, live.x - x - 96f, 18f), BackgroundNames[swatchHover], 10, BackgroundAccents[swatchHover], TextAnchor.MiddleLeft, true);

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

            if (GUI.Button(minimize, GUIContent.none, GUIStyle.none))
            {
                bgPickerMin = true;
                bgTabSeen = now;
                SaveBackgroundSettings();
            }
            if (GUI.Button(live, GUIContent.none, GUIStyle.none))
            {
                menuBgAnimate = !menuBgAnimate;
                SaveBackgroundSettings();
            }
            if (GUI.Button(prev, GUIContent.none, GUIStyle.none))
                SetBackground(menuBg - 1, -1);
            if (GUI.Button(next, GUIContent.none, GUIStyle.none) || GUI.Button(thumb, GUIContent.none, GUIStyle.none))
                SetBackground(menuBg + 1, 1);
            for (int i = 0; i < BackgroundNames.Length; i++)
                if (i != menuBg && GUI.Button(new Rect(x + i * (sw + gap), sy - 7f, sw + gap, 16f), GUIContent.none, GUIStyle.none))
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
            Ui.Button(prev, "", Ui.Neutral);
            Ui.Button(next, "", Ui.Neutral);
            Ui.Button(open, "", Ui.Neutral);
            GUI.color = old;
            if (BeginUiGl())
            {
                var ink = new Rgba(0.85f, 0.9f, 1f, 0.9f * a);
                Chevron(new V2(prev.center.x + 1f, prev.center.y), new V2(-1f, 0f), 8f, 2f, ink);
                Chevron(new V2(next.center.x - 1f, next.center.y), new V2(1f, 0f), 8f, 2f, ink);
                Chevron(new V2(open.center.x, open.center.y + 1f), new V2(0f, -1f), 8f, 2f, ToRgba(Color.Lerp(accent, Color.white, 0.4f), a));
                EndUiGl();
            }
            // The small buttons first, so a click on one doesn't also open the picker.
            if (GUI.Button(prev, GUIContent.none, GUIStyle.none))
                SetBackground(menuBg - 1, -1);
            else if (GUI.Button(next, GUIContent.none, GUIStyle.none))
                SetBackground(menuBg + 1, 1);
            else if (GUI.Button(tab, GUIContent.none, GUIStyle.none))
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
            SetColor(top); GL.Vertex(Px(a)); GL.Vertex(Px(b));
            SetColor(bottom); GL.Vertex(Px(c));
            SetColor(top); GL.Vertex(Px(a));
            SetColor(bottom); GL.Vertex(Px(c)); GL.Vertex(Px(d));
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
    }
}

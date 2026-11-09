using UnityEngine;

namespace Arena
{
    /// The name effects a rank unlocks (Rank.cs, CosmeticKind.NameEffect): how a player's name is drawn wherever it shows to others
    /// (the lobby party card and Hangout plate, the tag over a player in a match, your look card). Mockups: Docs/Mockups/name-effects.html and name-effects-more.html (the last eight: Chrome to Petals).
    /// Built from IMGUI labels: colour gradients are the same text drawn in a few clipped bands, glows and outlines are copies at
    /// small offsets, sheens are a clipped strip. With Visual effects off each is its still self; a small name (13 px or less) keeps
    /// only its colour and glow, so it stays readable.
    public partial class ArenaApp
    {
        int NameFxOf(int playerId)
        {
            LobbyEntry e;
            return session.Lobby.TryGetValue(playerId, out e) ? e.NameFx : 0;
        }

        /// Draw `text` in rect r with name effect `fx` (Rank.NameFxIndex; 0 = plain, in `plain`). `shadow`: a dark copy underneath, for names over the arena.
        void DrawName(Rect r, string text, int size, int fx, float t, Color plain, TextAnchor align = TextAnchor.MiddleLeft, bool shadow = false)
        {
            string id = fx > 0 ? Rank.NameFxId(fx) : "";
            if (id == "")
            {
                if (shadow)
                    Ui.Shadowed(r, text, size, plain, align, true);
                else
                    Ui.Label(r, text, size, plain, align, true);
                return;
            }
            if (!fxOn)
                t = 0f;
            bool small = size <= 13;
            float w = Ui.TextWidth(text, size, true);
            float x = align == TextAnchor.MiddleCenter || align == TextAnchor.UpperCenter ? r.center.x - w / 2f : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight ? r.xMax - w : r.x;
            if (id == "name-terminal")  // fixed pitch, so its width isn't the font's
            {
                w = text.Length * Ui.TextWidth("0", size, true) * 1.02f;
                x = align == TextAnchor.MiddleCenter || align == TextAnchor.UpperCenter ? r.center.x - w / 2f : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight ? r.xMax - w : r.x;
            }
            if (shadow)
                NRun(r, x, text, size, new Color(0f, 0f, 0f, 0.65f), 1f, 1f);
            float cy = r.center.y;
            switch (id)
            {
                case "name-outline":
                    NOutline(r, x, text, size, new Color(0.42f, 0.61f, 1f), 1.1f);
                    NRun(r, x, text, size, Color.white);
                    break;
                case "name-frost":
                    NGlow(r, x, text, size, new Color(0.47f, 0.82f, 1f, 0.2f), 1.6f);
                    NGradient(r, x, w, text, size, Color.white, new Color(0.78f, 0.93f, 1f), new Color(0.53f, 0.83f, 1f));
                    break;
                case "name-ember":
                    NGlow(r, x, text, size, new Color(1f, 0.43f, 0.12f, 0.22f), 1.5f);
                    NGradient(r, x, w, text, size, new Color(1f, 0.88f, 0.48f), new Color(1f, 0.65f, 0.22f), new Color(1f, 0.35f, 0.12f));
                    if (!small)
                        for (int i = 0; i < 7; i++)
                        {
                            float life = (t * 0.7f + FrameHash(i)) % 1f;
                            EDot(new Vector2(x + FrameHash(i * 5 + 1) * w, cy - size * 0.5f - life * size * 0.9f), 1f + 1.4f * (1f - life), new Color(1f, 0.59f + 0.35f * (1f - life), 0.24f, (1f - life) * 0.9f));
                        }
                    break;
                case "name-neon":
                {
                    float k = 0.65f + 0.35f * Mathf.Sin(t * 3f);
                    NGlow(r, x, text, size, new Color(0.27f, 0.88f, 1f, 0.12f * k), 2.6f);
                    NGlow(r, x, text, size, new Color(0.27f, 0.88f, 1f, 0.2f * k), 1.3f);
                    NRun(r, x, text, size, new Color(0.92f, 1f, 1f));
                    break;
                }
                case "name-wave":
                    for (int i = 0; i < text.Length; i++)
                    {
                        var c = Color.HSVToRGB(((190f + i * 14f) % 360f) / 360f, 0.55f, 1f);
                        NRun(r, x + NPrefix(text, i, size), text.Substring(i, 1), size, c, 0f, small ? 0f : Mathf.Sin(t * 3f + i * 0.6f) * size * 0.1f);
                    }
                    break;
                case "name-shimmer":
                    NGradient(r, x, w, text, size, new Color(0.96f, 0.97f, 1f), new Color(0.78f, 0.82f, 0.9f), new Color(0.67f, 0.71f, 0.8f));
                    NSheen(r, x, w, text, size, t, new Color(1f, 1f, 1f, 0.95f), 0.45f);
                    break;
                case "name-glitch":
                {
                    float o = Mathf.Max(0.6f, size * 0.055f);
                    bool burst = !small && t > 0f && (t % 2.3f) < 0.16f;
                    float j = burst ? (FrameHash(Mathf.FloorToInt(t * 40f)) - 0.5f) * size * 0.35f : 0f;
                    NRun(r, x, text, size, new Color(1f, 0.16f, 0.31f, 0.8f), -o + j);
                    NRun(r, x, text, size, new Color(0.16f, 0.9f, 1f, 0.8f), o - j);
                    if (burst)
                    {
                        NBand(r, x, w, text, size, -0.8f, 0.05f, Color.white, 0f);
                        NBand(r, x, w, text, size, 0.05f, 0.85f, Color.white, j * 0.9f);
                    }
                    else
                        NRun(r, x, text, size, Color.white);
                    break;
                }
                case "name-glitchy":
                    NGlitchy(r, x, w, text, size, t, small);
                    break;
                case "name-fire":
                    if (!small)
                        for (int i = 0; i < 10; i++)
                        {
                            float life = (t * 1.1f + FrameHash(i)) % 1f, rad = size * (0.16f + 0.12f * (1f - life));
                            EDot(new Vector2(x + FrameHash(i * 3 + 1) * w, cy + size * 0.2f - life * size * 0.9f), rad, new Color(1f, (70f + 120f * (1f - life)) / 255f, 0.08f, 0.5f * (1f - life)));
                        }
                    for (int i = 0; i < text.Length; i++)
                    {
                        float flick = small ? 0f : Mathf.Sin(t * 14f + i * 2f) * size * 0.04f;
                        NGradient(r, x + NPrefix(text, i, size), Ui.TextWidth(text.Substring(i, 1), size, true), text.Substring(i, 1), size, new Color(1f, 0.35f, 0.12f), new Color(1f, 0.79f, 0.23f), new Color(1f, 0.95f, 0.69f), flick);
                    }
                    break;
                case "name-gold":
                    NGradient(r, x, w, text, size, new Color(1f, 0.94f, 0.63f), new Color(0.91f, 0.73f, 0.17f), new Color(0.56f, 0.37f, 0.03f));
                    NSheen(r, x, w, text, size, t * 0.8f, new Color(1f, 0.97f, 0.8f, 0.9f), 0.45f);
                    break;
                case "name-void":
                    NOutline(r, x, text, size, new Color(0.11f, 0.04f, 0.22f), Mathf.Max(1f, size * 0.07f));
                    NGlow(r, x, text, size, new Color(0.63f, 0.35f, 1f, 0.28f), 2.2f);
                    NRun(r, x, text, size, new Color(0.89f, 0.8f, 1f));
                    if (!small)
                        for (int i = 0; i < 3; i++)
                        {
                            float a = t * 1.2f + i * 2.094f;
                            EDot(new Vector2(x + w / 2f + Mathf.Cos(a) * (w / 2f + 8f), cy + Mathf.Sin(a) * size * 0.55f), 1.6f, new Color(0.75f, 0.55f, 1f, 0.85f));
                        }
                    break;
                case "name-rainbow":
                    for (int i = 0; i < text.Length; i++)
                        NRun(r, x + NPrefix(text, i, size), text.Substring(i, 1), size, Color.HSVToRGB(((t * 70f + i * 38f) % 360f) / 360f, 0.85f, 0.95f));
                    break;
                case "name-chrome":
                    NOutline(r, x, text, size, new Color(0.04f, 0.06f, 0.12f), 1.1f);
                    NBand(r, x, w, text, size, -0.8f, -0.35f, new Color(1f, 1f, 1f));
                    NBand(r, x, w, text, size, -0.35f, -0.05f, new Color(0.74f, 0.84f, 1f));
                    NBand(r, x, w, text, size, -0.05f, 0.08f, new Color(0.12f, 0.16f, 0.28f));
                    NBand(r, x, w, text, size, 0.08f, 0.4f, new Color(0.49f, 0.61f, 0.82f));
                    NBand(r, x, w, text, size, 0.4f, 0.85f, new Color(0.96f, 0.89f, 0.78f));
                    NSheen(r, x, w, text, size, t * 0.8f, new Color(1f, 1f, 1f, 0.7f), 0.45f);
                    break;
                case "name-toxic":
                    NOutline(r, x, text, size, new Color(0.05f, 0.18f, 0.02f), Mathf.Max(1f, size * 0.07f));
                    NGlow(r, x, text, size, new Color(0.47f, 1f, 0.16f, 0.22f), 1.8f);
                    NGradient(r, x, w, text, size, new Color(0.89f, 1f, 0.42f), new Color(0.6f, 0.95f, 0.2f), new Color(0.31f, 0.82f, 0f));
                    if (!small && t > 0f)
                        for (int i = 0; i < text.Length; i++)
                        {
                            if (text[i] == ' ' || FrameHash(i * 3 + 2) >= 0.55f)
                                continue;
                            float px = x + NPrefix(text, i, size) + Ui.TextWidth(text.Substring(i, 1), size, true) * 0.5f, life = (t * 0.45f + FrameHash(i * 5 + 7)) % 1f, len = Mathf.Min(1f, life / 0.6f) * size * 0.3f;
                            Ui.Fill(new Rect(px - 1f, cy + size * 0.3f, 2f, len), new Color(0.49f, 1f, 0.16f, 1f));
                            EDot(new Vector2(px, cy + size * 0.3f + len), 1.8f, new Color(0.49f, 1f, 0.16f, 1f));
                            if (life > 0.6f)
                            {
                                float f = (life - 0.6f) / 0.4f;
                                EDot(new Vector2(px, cy + size * 0.6f + f * size * 0.5f), 1.6f, new Color(0.49f, 1f, 0.16f, 1f - f));
                            }
                        }
                    break;
                case "name-holo":
                {
                    float oldA = Ui.Alpha;
                    if (!small && t > 0f && FrameHash(Mathf.FloorToInt(t * 20f) + 5) < 0.06f)
                        Ui.Alpha = oldA * 0.5f;  // drops out for a frame
                    NRun(r, x, text, size, new Color(1f, 0.24f, 0.78f, 0.6f), size * 0.05f);
                    NGradient(r, x, w, text, size, new Color(0.79f, 0.99f, 1f), new Color(0.55f, 0.93f, 1f), new Color(0.31f, 0.86f, 1f));
                    if (t > 0f)
                    {
                        float pos = -0.8f + (((t * 0.7f) % 1.4f) / 1.4f) * 1.65f;
                        NBand(r, x, w, text, size, pos - 0.08f, pos + 0.08f, new Color(1f, 1f, 1f, 0.9f));
                    }
                    if (!small)
                        for (float yy = cy - size * 0.7f; yy < cy + size * 0.7f; yy += 3f)
                            Ui.Fill(new Rect(x - 2f, yy, w + 4f, 1f), new Color(0.03f, 0.05f, 0.09f, 0.3f));
                    Ui.Alpha = oldA;
                    break;
                }
                case "name-nebula":
                    for (int i = 0; i < text.Length; i++)
                    {
                        float f = text.Length > 1 ? i / (float)(text.Length - 1) : 0f;
                        var c = f < 0.5f ? Color.Lerp(new Color(1f, 0.61f, 0.91f), new Color(0.66f, 0.61f, 1f), f * 2f) : Color.Lerp(new Color(0.66f, 0.61f, 1f), new Color(0.44f, 0.85f, 1f), (f - 0.5f) * 2f);
                        NRun(r, x + NPrefix(text, i, size), text.Substring(i, 1), size, c);
                    }
                    NSheen(r, x, w, text, size, t * 0.7f, new Color(1f, 1f, 1f, 0.55f), 0.3f);
                    if (!small)
                        for (int i = 0; i < 9; i++)
                        {
                            float tw = 0.5f + 0.5f * Mathf.Sin(t * 3f + i * 1.9f);
                            EDot(new Vector2(x + FrameHash(i) * w, cy + (FrameHash(i + 9) - 0.5f) * size * 0.8f), 0.8f, new Color(1f, 1f, 1f, 0.4f + 0.6f * tw));
                        }
                    break;
                case "name-pixel":  // chunky colour blocks stepping along the name (a clipped column of the text each)
                {
                    var pal = new[] { new Color(1f, 0.3f, 0.43f), new Color(1f, 0.82f, 0.4f), new Color(0.02f, 0.84f, 0.63f), new Color(0.3f, 0.79f, 0.94f) };
                    int step = t > 0f ? Mathf.FloorToInt(t * 6f) : 0;
                    float block = Mathf.Max(5f, size * 0.3f);
                    NOutline(r, x, text, size, new Color(0.04f, 0.05f, 0.1f), 1f);
                    for (int k = 0; k * block < w + block; k++)
                    {
                        var clip = new Rect(x + k * block, cy - size * 0.8f, Mathf.Min(block, w + 6f - k * block), size * 1.6f);
                        GUI.BeginGroup(clip);
                        Ui.Label(new Rect(x - clip.x, r.y - clip.y, w + 24f, r.height), text, size, pal[(k + step) % 4], TextAnchor.MiddleLeft, true);
                        GUI.EndGroup();
                    }
                    break;
                }
                case "name-terminal":  // fixed-pitch green with a blinking block cursor; now and then it scrambles and decodes
                {
                    float pitch = Ui.TextWidth("0", size, true) * 1.02f, cyc = t % 4f;
                    int shown = small || t <= 0f || cyc > 0.8f ? text.Length : Mathf.FloorToInt(cyc / 0.8f * text.Length);
                    var green = new Color(0.42f, 1f, 0.6f);
                    for (int i = 0; i < text.Length; i++)
                    {
                        string ch = i < shown ? text.Substring(i, 1) : GlitchChars.Substring(Mathf.FloorToInt(FrameHash(Mathf.FloorToInt(t * 25f) + i * 7) * GlitchChars.Length) % GlitchChars.Length, 1);
                        float cx0 = x + i * pitch + (pitch - Ui.TextWidth(ch, size, true)) * 0.5f;
                        NRun(r, cx0, ch, size, new Color(0.24f, 1f, 0.51f, 0.2f), 1f);
                        NRun(r, cx0, ch, size, green);
                    }
                    if (small || t <= 0f || Mathf.FloorToInt(t * 2f) % 2 == 0)
                        Ui.Fill(new Rect(x + text.Length * pitch + 2f, cy - size * 0.42f, pitch * 0.7f, size * 0.84f), new Color(0.42f, 1f, 0.6f, 0.9f));
                    break;
                }
                case "name-electric":
                {
                    int seed = t > 0f ? Mathf.FloorToInt(t * 14f) : 0;
                    NGlow(r, x, text, size, new Color(0.43f, 0.67f, 1f, 0.2f + 0.12f * FrameHash(seed)), 1.7f);
                    NRun(r, x, text, size, new Color(1f, 0.97f, 0.78f));
                    if (!small && t > 0f)
                        for (int b = 0; b < 2; b++)
                        {
                            if (FrameHash(seed * 3 + b * 11) < 0.45f)
                                continue;
                            float sy = cy + (FrameHash(seed + b * 5) - 0.5f) * size * 0.9f;
                            var prev = new Vector2(x - 4f, sy);
                            for (float px = x + 8f; px < x + w + 4f; px += 9f)
                            {
                                var nx = new Vector2(px, sy + (FrameHash(seed * 13 + (int)px + b) - 0.5f) * size * 0.7f);
                                FrameLine(prev, nx, 1.5f, new Color(0.39f, 0.59f, 1f, 0.35f));
                                FrameLine(prev, nx, 0.55f, new Color(0.92f, 0.96f, 1f, 0.95f));
                                prev = nx;
                            }
                        }
                    break;
                }
                case "name-petals":
                    NGlow(r, x, text, size, new Color(1f, 0.51f, 0.69f, 0.2f), 1.5f);
                    NGradient(r, x, w, text, size, new Color(1f, 0.96f, 0.97f), new Color(1f, 0.83f, 0.89f), new Color(1f, 0.69f, 0.8f));
                    if (!small && t > 0f)
                        for (int i = 0; i < 8; i++)
                        {
                            float life = (t * 0.22f + FrameHash(i)) % 1f, px = x - 6f + FrameHash(i * 3 + 1) * (w + 12f) + Mathf.Sin(t * 1.6f + i * 2f) * size * 0.35f, py = cy - size * 0.8f + life * size * 1.7f;
                            float pw = size * 0.22f * Mathf.Max(0.25f, Mathf.Abs(Mathf.Cos(t * 1.4f + i * 1.3f)));
                            Ui.Fill(new Rect(px - pw / 2f, py - size * 0.05f, pw, size * 0.11f), new Color(1f, 0.69f + 0.24f * FrameHash(i + 4), 0.78f + 0.16f * FrameHash(i + 6), Mathf.Sin(life * Mathf.PI) * 0.95f));
                        }
                    break;
                default:
                    NRun(r, x, text, size, plain);
                    break;
            }
        }

        // --- Pieces ---------------------------------------------------------------------------------------------

        static float NPrefix(string text, int i, int size) => i <= 0 ? 0f : Ui.TextWidth(text.Substring(0, i), size, true);

        /// One run of text starting at x (the label is centred on the rect's height), moved by dx, dy.
        static void NRun(Rect r, float x, string s, int size, Color c, float dx = 0f, float dy = 0f)
            => Ui.Label(new Rect(x + dx, r.y + dy, Ui.TextWidth(s, size, true) + 24f, r.height), s, size, c, TextAnchor.MiddleLeft, true);

        static readonly Vector2[] Ring8 = { new Vector2(1, 0), new Vector2(0.7f, 0.7f), new Vector2(0, 1), new Vector2(-0.7f, 0.7f), new Vector2(-1, 0), new Vector2(-0.7f, -0.7f), new Vector2(0, -1), new Vector2(0.7f, -0.7f) };

        static void NOutline(Rect r, float x, string s, int size, Color c, float d)
        {
            foreach (var o in Ring8)
                NRun(r, x, s, size, c, o.x * d, o.y * d);
        }

        static void NGlow(Rect r, float x, string s, int size, Color c, float d)
        {
            for (int i = 0; i < 8; i += 2)
                NRun(r, x, s, size, c, Ring8[i].x * d, Ring8[i].y * d);
        }

        /// The text in a horizontal band of the line (y0..y1 in units of the font size from the line's middle), moved sideways by dx.
        static void NBand(Rect r, float x, float w, string s, int size, float y0, float y1, Color c, float dx = 0f, float dy = 0f)
        {
            float cy = r.center.y;
            var clip = new Rect(Mathf.Min(x, x + dx) - 6f, cy + y0 * size + dy, w + Mathf.Abs(dx) + 20f, (y1 - y0) * size);
            GUI.BeginGroup(clip);
            Ui.Label(new Rect(x + dx - clip.x, r.y - clip.y - dy, Ui.TextWidth(s, size, true) + 24f, r.height), s, size, c, TextAnchor.MiddleLeft, true);
            GUI.EndGroup();
        }

        /// A top-to-bottom colour fade, as three bands of the same text.
        static void NGradient(Rect r, float x, float w, string s, int size, Color top, Color mid, Color bottom, float dy = 0f)
        {
            NBand(r, x, w, s, size, -0.8f, -0.18f, top, 0f, dy);
            NBand(r, x, w, s, size, -0.18f, 0.2f, mid, 0f, dy);
            NBand(r, x, w, s, size, 0.2f, 0.85f, bottom, 0f, dy);
        }

        /// A bright strip of the text gliding across (speed in widths a second).
        static void NSheen(Rect r, float x, float w, string s, int size, float t, Color c, float speed)
        {
            if (t <= 0f)
                return;
            float p = (((t * speed) % 1.6f) - 0.3f) * (w + 40f);
            var clip = new Rect(x + p - 12f, r.center.y - size * 0.8f, 24f, size * 1.6f);
            if (clip.xMax < x - 4f || clip.x > x + w + 4f)
                return;
            GUI.BeginGroup(clip);
            Ui.Label(new Rect(x - clip.x, r.y - clip.y, Ui.TextWidth(s, size, true) + 24f, r.height), s, size, c, TextAnchor.MiddleLeft, true);
            GUI.EndGroup();
        }

        static readonly string GlitchChars = "#%&01/=+?";

        /// Glitchy: always going. Every ~70 ms the letters re-roll: some jump a little, some are swapped for a symbol, the
        /// whole line splits in two halves that slide apart, red and cyan fringes pulse, and now and then it flickers.
        static void NGlitchy(Rect r, float x, float w, string text, int size, float t, bool small)
        {
            int step = t > 0f ? Mathf.FloorToInt(t * 14f) : 0;
            float o = Mathf.Max(0.7f, size * (0.06f + 0.04f * FrameHash(step + 3)));
            float oldAlpha = Ui.Alpha;
            if (!small && t > 0f && FrameHash(step * 7 + 1) < 0.07f)
                Ui.Alpha = oldAlpha * 0.55f;  // a flicker
            bool slide = !small && t > 0f && FrameHash(step + 99) < 0.5f;
            float slideBy = slide ? (FrameHash(step + 41) - 0.5f) * size * 0.4f : 0f;
            for (int pass = 0; pass < 3; pass++)
            {
                Color c = pass == 0 ? new Color(1f, 0.16f, 0.31f, 0.8f) : pass == 1 ? new Color(0.16f, 0.9f, 1f, 0.8f) : Color.white;
                float fringe = pass == 0 ? -o : pass == 1 ? o : 0f;
                for (int i = 0; i < text.Length; i++)
                {
                    int seed = step * 31 + i;
                    string ch = text.Substring(i, 1);
                    bool swap = t > 0f && ch != " " && FrameHash(seed) < (small ? 0.04f : 0.12f);
                    if (swap)
                        ch = GlitchChars.Substring(Mathf.FloorToInt(FrameHash(seed + 5) * GlitchChars.Length) % GlitchChars.Length, 1);
                    float jx = !small && t > 0f && FrameHash(seed + 7) < 0.35f ? (FrameHash(seed + 11) - 0.5f) * size * 0.18f : 0f;
                    float lx = x + NPrefix(text, i, size) + jx + fringe;
                    float cw = Ui.TextWidth(ch, size, true);
                    if (pass == 2 && slide)
                    {
                        NBand(r, lx, cw + 8f, ch, size, -0.8f, 0.05f, c, 0f);
                        NBand(r, lx, cw + 8f, ch, size, 0.05f, 0.85f, c, slideBy);
                    }
                    else
                        NRun(r, lx, ch, size, c);
                }
            }
            Ui.Alpha = oldAlpha;
        }
    }
}

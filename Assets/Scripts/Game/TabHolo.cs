using System;
using UnityEngine;

namespace Arena
{
    /// The Tab screen's hologram look (look B, "Hologram", of Docs/Mockups/crt-tab-menu.html): panels projected over the dimmed arena,
    /// lit from a beam along the bottom of the screen. Each panel is the level-up cards' Projection glass (CardArt.Holo in the CRT
    /// colour, see-through) with scanlines every 4 px, a band drifting down, a magenta / cyan edge fringe and a faint flicker, and
    /// it switches on from a thin line when the screen opens or the page changes. The text is drawn over the glass effects so it stays
    /// sharp. Reduced effects: no band, flicker, cone lines or switch-on. Used by RunStatsUi.cs (the screen, Run so far, Team) and
    /// HudTab.cs (your stats, Your build). Preview: `STATSTAB=0|1|2 [HOLOAT=<s>] Tools/Preview/shot.sh x tab 23 12.2 3`.
    public partial class ArenaApp
    {
        // --- Layout (the mockup's B) ---------------------------------------------------------------------------------------------

        const float HoloTop = 56f, HoloBottom = 696f;                     // the panels' rows
        const float HoloStatsX = 24f, HoloStatsW = 250f;                  // YOUR STATS, down the left
        const float HoloPageX = 290f, HoloPageR = W - 24f;                // the page's panels, right of it
        const float HoloHeadH = 92f, HoloGap = 16f;                       // the header panel on every page, the gap between panels
        const float TabDark = 0.42f;                                      // how dark the arena goes behind (lighter than other windows: it shows through)
        const float HoloBootLen = 0.38f;                                  // a panel switching on

        float holoOpenAt = -10f, holoPageAt = -10f;  // when the screen opened, and when its page last changed (the panels switch on)
        bool holoBusy;                               // a panel is mid switch-on (scaled): live models (GL) wait

        static Color HoloBody => new Color(0.9f, 0.93f, 1f);              // body text on the glass
        static Color HoloDim => Color.Lerp(CrtUi.Dim, Color.white, 0.22f); // labels on the glass (a little brighter than the screens' dim)
        static Color HoloDesc => new Color(0.73f, 0.8f, 0.94f);           // descriptions

        bool HoloCalm => fxReduced || !fxOn;

        // --- The projector ------------------------------------------------------------------------------------------------------

        /// The beam strip along the bottom of the screen and the faint cone of light it throws up behind the panels.
        void DrawHoloBeam()
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var ink = CrtUi.Ink;
            const int strips = 36;
            const float top = 0f, bottom = 706f;
            for (int i = 0; i < strips; i++)
            {
                float f = (i + 0.5f) / strips;  // 0 at the bottom, 1 at the top
                float a = f < 0.45f ? Mathf.Lerp(0.16f, 0.05f, f / 0.45f) : f < 0.85f ? Mathf.Lerp(0.05f, 0f, (f - 0.45f) / 0.4f) : 0f;
                if (a <= 0f)
                    continue;
                float inset = Mathf.Lerp(0.11f, 0f, f) * W, h = (bottom - top) / strips;
                Ui.Fill(new Rect(inset, bottom - (i + 1) * h, W - 2f * inset, h + 0.5f), new Color(ink.r, ink.g, ink.b, a));
            }
            if (!HoloCalm)  // faint vertical lines in the cone
                for (float x = 0f; x < W; x += 22f)
                    Ui.Fill(new Rect(x, 0f, 1f, bottom), new Color(ink.r, ink.g, ink.b, 0.035f));
            var strip = new Rect(140f, 704f, 1000f, 5f);
            Ui.Glow(strip, ink, 0.45f);
            var hot = CrtUi.T(new Color(0.81f, 0.89f, 1f));
            const int n = 40;
            for (int i = 0; i < n; i++)
            {
                float u = (i + 0.5f) / n, d = Mathf.Abs(u - 0.5f) * 2f;  // 0 in the middle, 1 at the ends
                var c = d < 0.76f ? Color.Lerp(hot, ink, d / 0.76f) : ink;
                float a = d < 0.76f ? 1f : 1f - (d - 0.76f) / 0.24f;
                Ui.Fill(new Rect(strip.x + i * strip.width / n, strip.y, strip.width / n + 0.5f, strip.height), new Color(c.r, c.g, c.b, a));
            }
        }

        // --- A panel ------------------------------------------------------------------------------------------------------------

        /// A hologram panel at `r`: it switches on `since` seconds after `bootAt` (from a line, flaring), then its glass and, over it,
        /// `draw` (the content).
        void HoloPanel(Rect r, float bootAt, Action draw)
        {
            float now = Time.unscaledTime, since = now - bootAt;
            float k = HoloCalm ? 1f : Mathf.Clamp01(since / HoloBootLen);
            if (k <= 0f)
                return;
            float sy = k >= 1f ? 1f : Mathf.Lerp(0.015f, 1f, 1f - (1f - k) * (1f - k) * (1f - k));
            var oldMatrix = GUI.matrix;
            if (sy < 1f)
            {
                // squashed toward its middle line (like HudGroup, from the screen's offset and scale)
                GUI.matrix = Matrix4x4.TRS(new Vector3(offX, offY + r.center.y * (1f - sy) * scale, 0f), Quaternion.identity, new Vector3(scale, scale * sy, 1f));
                holoBusy = true;
            }
            DrawHoloGlass(r, now);
            draw();
            if (sy < 1f)
            {
                float flare = 1f - k;  // brightest as it opens
                Ui.Fill(r, new Color(1f, 1f, 1f, 0.35f * flare));
                GUI.matrix = oldMatrix;
                holoBusy = false;
            }
        }

        /// The glass: the Projection body in the CRT colour (see-through), scanlines counted up from the bottom edge, the drifting band
        /// and the colour fringe down the sides; two short dips every 5 s.
        void DrawHoloGlass(Rect r, float now)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            var ink = CrtUi.Ink;
            float t = MathX.Fmod(now, 5f) / 5f;
            bool dip = !HoloCalm && ((t >= 0.94f && t < 0.96f) || t >= 0.98f);
            float oldAlpha = Ui.Alpha;
            Ui.Alpha = oldAlpha * (dip ? 0.82f : 0.9f);  // see-through: the arena shows a little
            Ui.Picture(r, CardArt.Holo(Color.Lerp(ink, new Color(0.02f, 0.05f, 0.12f), 0.35f)));
            Ui.Alpha = oldAlpha;
            var line = new Color(ink.r, ink.g, ink.b, 0.11f);
            for (float y = Mathf.Round(r.yMax) - 1f; y >= r.y; y -= 4f)
                Ui.Fill(new Rect(r.x, y, r.width, 1f), line);
            if (!HoloCalm)
            {
                float h = r.height, p = MathX.Fmod(now, 3.4f) / 3.4f;
                float by = Mathf.Lerp(-0.288f * h, 1.152f * h, p);
                var bc = Color.Lerp(ink, Color.white, 0.5f);
                var old = GUI.color;
                GUI.BeginGroup(r);
                GUI.color = Ui.Faded(new Color(bc.r, bc.g, bc.b, 0.14f));
                GUI.DrawTexture(new Rect(0f, by, r.width, 0.28f * h), CardArt.Band());
                GUI.EndGroup();
                GUI.color = old;
            }
            Ui.Fill(new Rect(r.x, r.y, 2f, r.height), new Color(1f, 60f / 255f, 200f / 255f, 0.35f));
            Ui.Fill(new Rect(r.xMax - 2f, r.y, 2f, r.height), new Color(60f / 255f, 240f / 255f, 1f, 0.35f));
        }

        // --- Text on the glass -------------------------------------------------------------------------------------------------------

        /// A panel's title in the CRT font with the blinking cursor, and a line under it.
        void HoloTitle(float x, float y, string title, string sub, Color subColor, int size = 34)
        {
            var ink = CrtUi.Ink;
            CrtUi.Title(new Rect(x, y, 700f, size + 4f), title, size, ink, TextAnchor.MiddleLeft, HoloCalm);
            if (HoloCalm || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
                Ui.Fill(new Rect(x + CrtUi.TitleWidth(title, size) + 5f, y + size * 0.28f, size * 0.26f, size * 0.55f), ink);
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(x, y + size + 4f, 900f, 16f), sub, 12, subColor, TextAnchor.MiddleLeft, true);
        }

        /// Label / value rows with every other one faintly lit (the stats, your fight).
        void HoloRows(float x, float y, float w, string[,] rows, float rowH = 26f)
        {
            var ink = CrtUi.Ink;
            for (int i = 0; i < rows.GetLength(0); i++)
            {
                var r = new Rect(x, y + i * rowH, w, rowH);
                if (i % 2 == 0)
                    Ui.Fill(r, new Color(ink.r, ink.g, ink.b, 0.09f));
                Ui.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), rows[i, 0], 13, HoloBody, TextAnchor.MiddleLeft, false);
                CrtUi.Title(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), rows[i, 1], 22, Color.white, TextAnchor.MiddleRight, true);
            }
        }

        /// A number tile: the value big in gold, its name small under it, in a faint lit box.
        void HoloTile(Rect r, string value, string name, Color? valueColor = null, int size = 32, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var ink = CrtUi.Ink;
            Ui.Fill(r, new Color(ink.r, ink.g, ink.b, 0.07f));
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.4f));
            float pad = align == TextAnchor.MiddleLeft ? 10f : 4f;
            var vr = new Rect(r.x + pad, r.y + 4f, r.width - 2f * pad, size + 2f);
            string v = value;
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            v = LobbyKit.Truncate(v, size, vr.width, false);
            Ui.CrtFont = was;
            CrtUi.Title(vr, v, size, valueColor ?? CrtStyle.Gold, align, HoloCalm);
            Ui.Label(new Rect(r.x + pad, r.yMax - 17f, r.width - 2f * pad, 13f), name.ToUpperInvariant(), 10, HoloDim, align == TextAnchor.MiddleLeft ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, true);
        }

        /// A segmented bar (health, a share) in a colour.
        static void HoloBar(Rect r, float frac, Color c)
        {
            Ui.Fill(r, new Color(1f, 1f, 1f, 0.08f));
            float fw = r.width * Mathf.Clamp01(frac);
            for (float sx = 0f; sx < fw; sx += 8f)
                Ui.Fill(new Rect(r.x + sx, r.y, Mathf.Min(6f, fw - sx), r.height), c);
        }
    }
}

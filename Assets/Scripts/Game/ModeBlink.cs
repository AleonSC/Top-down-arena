using UnityEngine;

namespace Arena
{
    /// Leaving the Game Mode screen for the lobby with effects on: the "CRT blink" (the channel-change cousin of the lobby's
    /// power-cycle leave, LobbyCrtLeave.cs), 0.5 s:
    ///   0.0 s   TO THE LOBBY, Back or Esc: the mode screen squashes to a white-hot line about the middle (the CRT back and bezel
    ///           stay on; the screen goes dark round the band)
    ///   0.16 s  just the line, blooming
    ///   0.22 s  the lobby opens out of it with a slight overshoot, white at first, the flash gone by 0.4 s; scanlines fade by 0.5 s
    /// The switch itself is instant (`lobbyTab` is the lobby's at once); the closing half draws the old page, drawn only (Repaint,
    /// Ui.NoHit), so nothing takes a click. GL models squash with it (crtSquashOn, CanvasPx). Reduced effects or Visual effects
    /// off: the plain switch. Preview: `LOBBYTAB=8 MODEBLINK=<s> shot.sh x lobby` (0.08, 0.19, 0.27, 0.36).
    public partial class ArenaApp
    {
        const float ModeBlinkLen = 0.5f, MbOffEnd = 0.16f, MbLineEnd = 0.22f, MbOnEnd = 0.42f;
        float modeBlinkAt = -9f;  // when the blink started (unscaled time)
        int modeBlinkFrom;        // the mode screen's page, drawn while it closes

        float ModeBlinkT => Time.unscaledTime - modeBlinkAt;
        bool ModeBlinking => ModeBlinkT >= 0f && ModeBlinkT < ModeBlinkLen;

        /// Back to the lobby from the Game Mode screen (TO THE LOBBY, Back, Esc).
        void LeaveModeScreen()
        {
            if (fxOn && !fxReduced && OnModeScreen)
            {
                modeBlinkAt = Time.unscaledTime;
                modeBlinkFrom = lobbyTab;
            }
            lobbyTab = 0;
        }

        /// In place of DrawLobbyContent while blinking (inside DrawLobby's group).
        void DrawModeBlink()
        {
            if (Event.current.type != EventType.Repaint)
                return;  // drawn only: nothing takes a click
            float k = ModeBlinkT;
            bool open = k >= MbLineEnd;
            float sx, sy;
            if (k < MbOffEnd)
            {
                float c = Seg(k, 0f, MbOffEnd);
                sx = 1f + 0.04f * c;
                sy = Mathf.Max(0.006f, 1f - c * c * c);
            }
            else if (k < MbLineEnd)
                sx = sy = 0f;
            else
            {
                float o = Seg(k, MbLineEnd, MbOnEnd);
                sx = 1.03f - 0.03f * EaseOut(o);
                sy = Mathf.Max(0.006f, EaseBackOut(o));
            }
            var full = new Rect(0f, 0f, W, H);
            // The screen dark round the band (the CRT back shows through a little; the bezel is drawn over it after).
            float bandH = sy > 0f ? H * Mathf.Min(1f, sy) : 0f, bandY = (H - bandH) / 2f;
            var dark = new Color(0f, 0f, 0f, 0.9f);
            Ui.Fill(new Rect(0f, 0f, W, bandY), dark);
            Ui.Fill(new Rect(0f, bandY + bandH, W, H - bandY - bandH), dark);

            if (sy <= 0f)
            {
                // The line, shrinking a little and blooming.
                float l = Seg(k, MbOffEnd, MbLineEnd), lw = W * 1.04f * (1f - 0.45f * EaseOut(l));
                float glow = 120f + 60f * l;
                Ui.RadialGlow(new Rect(W / 2f - glow * 1.6f, H / 2f - glow, glow * 3.2f, glow * 2f), new Color(0.63f, 0.78f, 1f, 0.3f * l + 0.25f));
                Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 4f, lw, 8f), new Color(0.6f, 0.77f, 1f, 0.35f));
                Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 1.5f, lw, 3f), Color.white);
                return;
            }

            var old = GUI.matrix;
            float sc = old.m00, tx = old.m03, ty = old.m13;
            GUI.matrix = Matrix4x4.TRS(new Vector3(tx + sc * W / 2f * (1f - sx), ty + sc * H / 2f * (1f - sy), 0f), Quaternion.identity, new Vector3(sc * sx, sc * sy, 1f));
            crtSquashOn = true;
            crtSqX = sx;
            crtSqY = sy;
            int keepTab = lobbyTab;
            bool noHit = Ui.NoHit;
            Ui.NoHit = true;  // no hover lights under the pointer
            if (!open)
                lobbyTab = modeBlinkFrom;
            DrawLobbyContent();
            lobbyTab = keepTab;
            Ui.NoHit = noHit;
            // The tube's white: building as it closes, fading as it opens.
            float flash = open ? 0.8f * (1f - Seg(k, MbLineEnd, 0.4f)) : 0.9f * Mathf.Pow(Seg(k, 0f, MbOffEnd), 3f);
            if (flash > 0f)
                Ui.Fill(full, new Color(0.81f, 0.88f, 1f, flash));
            crtSquashOn = false;
            GUI.matrix = old;
            if (open)
                CrtScanlines(full, 0.35f * (1f - Seg(k, 0.36f, ModeBlinkLen)));
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// Leaving the Game Mode screen for the lobby with effects on: the "CRT blink" (the channel-change cousin of the lobby's
    /// power-cycle leave, LobbyCrtLeave.cs), 0.72 s, every curve eased at both ends so no frame jumps:
    ///   0.0 s   TO THE LOBBY, Back or Esc: the mode screen squashes (ease in-out) to a white-hot line about the middle, whitening
    ///           as it goes (the CRT back and bezel stay on; the screen goes dark round the band)
    ///   0.28 s  just the line, blooming
    ///   0.36 s  the lobby opens out of it (ease out, a slight overshoot) while the line fades over it, the white gone by 0.56 s;
    ///           the scanlines, there all along, fade by 0.72 s
    /// The switch itself is instant (`lobbyTab` is the lobby's at once); the closing half draws the old page, drawn only (Repaint,
    /// Ui.NoHit), so nothing takes a click. GL models squash with it (crtSquashOn, CanvasPx). Reduced effects or Visual effects
    /// off: the plain switch. Preview: `LOBBYTAB=8 MODEBLINK=<s> shot.sh x lobby` (0.1, 0.24, 0.32, 0.42, 0.55).
    public partial class ArenaApp
    {
        const float ModeBlinkLen = 0.72f, MbOffEnd = 0.28f, MbLineEnd = 0.36f, MbOnEnd = 0.62f;
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
                float c = Mathf.SmoothStep(0f, 1f, Seg(k, 0f, MbOffEnd));  // moves from the first frame, no late slam
                sx = 1f + 0.03f * c;
                sy = Mathf.Max(0.006f, 1f - c);
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
            float scan = 0.35f * Seg(k, 0f, 0.12f) * (1f - Seg(k, 0.5f, ModeBlinkLen));  // the scanlines: in, there throughout, out
            // The screen dark round the band (the CRT back shows through a little; the bezel is drawn over it after).
            float bandH = sy > 0f ? H * Mathf.Min(1f, sy) : 0f, bandY = (H - bandH) / 2f;
            var dark = new Color(0f, 0f, 0f, 0.9f);
            Ui.Fill(new Rect(0f, 0f, W, bandY), dark);
            Ui.Fill(new Rect(0f, bandY + bandH, W, H - bandY - bandH), dark);

            if (sy <= 0f)
            {
                DrawModeBlinkLine(Seg(k, MbOffEnd, MbLineEnd), 1f);
                CrtScanlines(full, scan);
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
            float flash = open ? 0.8f * (1f - Mathf.SmoothStep(0f, 1f, Seg(k, MbLineEnd, 0.56f))) : 0.8f * Mathf.SmoothStep(0f, 1f, Seg(k, 0.06f, MbOffEnd));
            if (flash > 0f)
                Ui.Fill(full, new Color(0.81f, 0.88f, 1f, flash));
            crtSquashOn = false;
            GUI.matrix = old;
            if (open)
                DrawModeBlinkLine(1f, 1f - Seg(k, MbLineEnd, MbLineEnd + 0.1f));  // the line fades over the opening band
            CrtScanlines(full, scan);
        }

        /// The bright line at the screen's middle: `l` 0..1 shrinks it a little and blooms it, `a` fades it.
        void DrawModeBlinkLine(float l, float a)
        {
            if (a <= 0f)
                return;
            float lw = W * 1.03f * (1f - 0.35f * EaseOut(l)), glow = 120f + 60f * l;
            Ui.RadialGlow(new Rect(W / 2f - glow * 1.6f, H / 2f - glow, glow * 3.2f, glow * 2f), new Color(0.63f, 0.78f, 1f, (0.3f * l + 0.25f) * a));
            Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 4f, lw, 8f), new Color(0.6f, 0.77f, 1f, 0.35f * a));
            Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 1.5f, lw, 3f), new Color(1f, 1f, 1f, a));
        }
    }
}

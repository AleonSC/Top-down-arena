using UnityEngine;

namespace Arena
{
    /// Leaving the Game Mode screen for the lobby with effects on: the "quick relink" (Docs/Mockups/gamemode-lobby-transition.html, B),
    /// the blink closing and New run's boot (LobbyCrt.cs) opening, about 2.6 s, every curve eased at both ends so no frame jumps:
    ///   0.0 s   TO THE LOBBY, Back or Esc: the mode screen squashes (ease in-out) to a white-hot line about the middle, whitening
    ///           as it goes (the CRT back and bezel stay on; the screen goes dark round the band)
    ///   0.28 s  just the line, blooming and cooling to the boot's blue; the screen behind is the dark blue CRT (ModeRelinkBlue)
    ///   0.36 s  the blue screen opens out of it (ease out, a 2% overshoot settling by 0.6 s) while the line fades over it
    ///   0.62 s  one line typed in the boot's fuzzy CRT text: "> QUICK PLAY", a blinking pause, " ... SET." (the mode's name)
    ///   then    New run's power-on (DrawPowerOnOpen): the line squeezes to a bright line that opens top and bottom onto the lobby
    /// Any key or click from 0.5 s jumps to the end with the skip's channel flip (SkipStatic.cs); the boot's sounds play along.
    /// Its clock steps at most 1/30 s a frame (like LobbyEnter.cs): the lobby's first draw can hitch, which would otherwise
    /// skip the opening and pop the lobby in.
    /// The switch itself is instant (`lobbyTab` is the lobby's at once); the closing half draws the old page, drawn only (Repaint,
    /// Ui.NoHit), so nothing takes a click. GL models squash with it (crtSquashOn, CanvasPx). Reduced effects or Visual effects
    /// off: the plain switch. Preview: `LOBBYTAB=8 MODEBLINK=<s> shot.sh x lobby` (0.1, 0.24, 0.32, 0.45, 0.6, 0.8).
    public partial class ArenaApp
    {
        const float MbOffEnd = 0.28f, MbLineEnd = 0.36f, MbOnEnd = 0.6f, MbLogAt = 0.62f, MbLogCharS = 0.018f, MbLogPause = 0.2f, MbLogHold = 0.25f, MbPowerLen = 1.1f;
        float modeBlinkT = -1f;   // seconds into the blink (capped steps, TickModeBlink); below 0 when not blinking
        int modeBlinkFrom;        // the mode screen's page, drawn while it closes
        bool modeScreenFirst;     // the mode screen New run opened (HostGame): Back and Esc leave to the main menu, not the lobby

        float ModeBlinkT => modeBlinkT;
        bool ModeBlinking => modeBlinkT >= 0f && modeBlinkT < ModeBlinkLen;

        /// The typed line: the mode's name, then " ... SET.".
        string[][] ModeRelinkLog() => new[] { new[] { "> " + ModeDef.For(session.Mode).Name.ToUpperInvariant(), " ... SET." } };

        /// When the power-on starts: the line typed, then a short hold.
        float ModeRelinkPowerAt
        {
            get
            {
                float at = MbLogAt;
                foreach (var part in ModeRelinkLog()[0])
                    at += part.Length * MbLogCharS;
                return at + MbLogPause + MbLogHold;
            }
        }

        float ModeBlinkLen => ModeRelinkPowerAt + MbPowerLen;

        /// The lobby's back is the boot's dark blue from the line until the lobby has opened over it (LobbyCrt.cs CrtBlue).
        bool ModeRelinkBlue => ModeBlinking && modeBlinkT >= MbOffEnd;
        bool ModeRelinkGlassBlue => ModeRelinkBlue && modeBlinkT < ModeRelinkPowerAt + 0.8f;

        /// Update: run the blink's clock, a hitch pausing it rather than skipping ahead.
        void TickModeBlink(float dt)
        {
            if (modeBlinkT < 0f)
                return;
            float was = modeBlinkT, len = ModeBlinkLen;
            modeBlinkT += Mathf.Min(dt, EnterMaxStep);
            if (was > 0.5f && modeBlinkT < ModeRelinkPowerAt + 0.35f && Input.anyKeyDown)  // a skip: straight to the lobby
            {
                modeBlinkT = len;
                StartSkipStatic();
            }
            PlayModeRelinkCues(was, modeBlinkT);
            if (modeBlinkT >= len)
                modeBlinkT = -1f;
        }

        /// Back to the lobby from the Game Mode screen (TO THE LOBBY, Back, Esc).
        void LeaveModeScreen()
        {
            modeScreenFirst = false;
            if (fxOn && !fxReduced && OnModeScreen)
            {
                modeBlinkT = 0f;
                modeBlinkFrom = lobbyTab;
            }
            lobbyTab = 0;
        }

        /// Back and Esc on the Game Mode screen: out to the main menu when New run opened it (nothing seen of the lobby yet),
        /// else back to the lobby.
        void ModeScreenBack()
        {
            if (modeScreenFirst && session.IsHost)
            {
                modeScreenFirst = false;
                StartLobbyLeave();  // LobbyEnter.cs
            }
            else
                LeaveModeScreen();
        }

        /// In place of DrawLobbyContent while blinking (inside DrawLobby's group).
        void DrawModeBlink()
        {
            if (Event.current.type != EventType.Repaint)
                return;  // drawn only: nothing takes a click
            float k = ModeBlinkT, t = Time.unscaledTime;
            var full = new Rect(0f, 0f, W, H);
            float scan = 0.35f * Mathf.SmoothStep(0f, 1f, Seg(k, 0f, 0.12f)) * (1f - Mathf.SmoothStep(0f, 1f, Seg(k, MbLineEnd, MbOnEnd)));  // the blink's heavy scanlines
            var dark = new Color(0f, 0f, 0f, 0.9f);

            if (k < MbOffEnd)  // the mode screen squashing to the line (its own colour: LobbyAccent reads the tab)
            {
                float c = Mathf.SmoothStep(0f, 1f, Seg(k, 0f, MbOffEnd));  // moves from the first frame, no late slam
                float sx = 1f + 0.03f * c, sy = Mathf.Max(0.006f, 1f - c);
                float bandH = H * sy, bandY = (H - bandH) / 2f;
                Ui.Fill(new Rect(0f, 0f, W, bandY), dark);
                Ui.Fill(new Rect(0f, bandY + bandH, W, H - bandY - bandH), dark);
                var old = GUI.matrix;
                float sc = old.m00, tx = old.m03, ty = old.m13;
                GUI.matrix = Matrix4x4.TRS(new Vector3(tx + sc * W / 2f * (1f - sx), ty + sc * H / 2f * (1f - sy), 0f), Quaternion.identity, new Vector3(sc * sx, sc * sy, 1f));
                crtSquashOn = true;
                crtSqX = sx;
                crtSqY = sy;
                int keepTab = lobbyTab;
                bool noHit = Ui.NoHit;
                var keepAccent = CrtStyle.Accent;
                Ui.NoHit = true;  // no hover lights under the pointer
                lobbyTab = modeBlinkFrom;
                CrtStyle.Accent = LobbyAccent();
                DrawLobbyContent();
                CrtStyle.Accent = keepAccent;
                lobbyTab = keepTab;
                Ui.NoHit = noHit;
                Ui.Fill(full, new Color(0.81f, 0.88f, 1f, 0.8f * Mathf.SmoothStep(0f, 1f, Seg(k, 0.06f, MbOffEnd))));  // the tube's white, building
                crtSquashOn = false;
                GUI.matrix = old;
                CrtScanlines(full, scan);
                return;
            }

            // The boot's dark blue screen opening out of the line (the back is already blue: ModeRelinkBlue), dark round it.
            float o = Seg(k, MbLineEnd, MbOnEnd);
            float half = k < MbLineEnd ? 0f : H / 2f * Mathf.Max(0.006f, EaseBackSoft(o));
            Ui.Fill(new Rect(0f, 0f, W, H / 2f - half), dark);
            Ui.Fill(new Rect(0f, H / 2f + half, W, H / 2f - half), dark);
            if (half > 0f && o < 1f)
                Ui.Fill(new Rect(0f, H / 2f - half, W, half * 2f), new Color(0.47f, 0.67f, 1f, 0.25f * (1f - EaseOut(o))));
            if (k < MbLineEnd + 0.14f)
                DrawModeBlinkLine(Seg(k, MbOffEnd, MbLineEnd), 1f - Seg(k, MbLineEnd + 0.04f, MbLineEnd + 0.14f), Seg(k, MbOffEnd - 0.04f, MbLineEnd + 0.02f));

            // The typed line, squeezing into the power-on's line as it starts.
            float pe = k - ModeRelinkPowerAt;
            float squeeze = EaseIn(Seg(pe, 0f, 0.2f)), logA = Seg(k, MbLogAt, MbLogAt + 0.1f);
            if (logA > 0f && squeeze < 1f)
            {
                var lines = ModeRelinkLog();
                var shown = CrtLogShown(lines, k, MbLogAt, MbLogCharS, MbLogPause);
                if (squeeze > 0f)
                {
                    float bh = 200f * (1f - squeeze);
                    GUI.BeginGroup(new Rect(0f, H / 2f - bh / 2f, W, bh));
                    GUI.BeginGroup(new Rect(0f, -(H / 2f - bh / 2f), W, H));
                }
                float y = H / 2f - lines.Length * 24f;
                for (int i = 0; i < shown.Length; i++)
                {
                    bool current = i == shown.Length - 1;  // the last line carries the cursor
                    string line = shown[i] + (current && Mathf.FloorToInt(t * 2f) % 2 == 0 ? "_" : "");
                    CrtFuzzyText(new Rect(W / 2f - 330f, y + i * 48f, 700f, 48f), line, 36, logA, t, i);
                }
                if (squeeze > 0f)
                {
                    GUI.EndGroup();
                    GUI.EndGroup();
                }
            }

            // New run's power-on opens the lobby (drawn here, inside DrawLobby, so LobbyRevealed draws the content only).
            if (pe > 0f)
            {
                revealDraw = DrawLobbyContent;
                try { DrawPowerOnOpen(pe); }
                finally { revealDraw = null; }
            }
            CrtScanlines(full, scan);
        }

        /// The relink's sounds for a..b seconds in (New run's set, LobbyCrt.cs): a key per typed character and a beep as the line
        /// starts, the sweep as the power-on starts and the ready chime as the lobby opens. A skip plays only the chime.
        void PlayModeRelinkCues(float a, float b)
        {
            if (sounds == null)  // the preview tool has no sound bank
                return;
            float now = Time.unscaledTime, pon = ModeRelinkPowerAt;
            bool skipped = b - a > 0.5f;
            if (!skipped)
            {
                var lines = ModeRelinkLog();
                int ca = CrtLogTyped(lines, a, MbLogAt, MbLogCharS, MbLogPause), cb = CrtLogTyped(lines, b, MbLogAt, MbLogCharS, MbLogPause);
                for (int i = 0, n = Mathf.Min(3, cb - ca); i < n; i++)
                    sounds.Play(Sfx.CrtType, 0.5f, now);
                if (a < MbLogAt && b >= MbLogAt)
                    sounds.Play(Sfx.CrtLine, 0.5f, now);
                if (a < pon && b >= pon)
                    sounds.Play(Sfx.CrtSweep, 0.5f, now);
            }
            if (a < pon + 0.35f && b >= pon + 0.35f)
                sounds.Play(Sfx.CrtReady, 0.55f, now);
        }

        /// Ease out with a gentle overshoot (about 2%, LobbyCrtLeave's EaseBackOut is 9%), level at the end.
        static float EaseBackSoft(float x) { const float c = 0.8f; return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f); }

        /// The bright line at the screen's middle: `l` 0..1 shrinks it a little and blooms it, `a` fades it, `cool` 0..1 turns it from
        /// the tube's white to the boot's blue.
        void DrawModeBlinkLine(float l, float a, float cool = 0f)
        {
            if (a <= 0f)
                return;
            float lw = W * 1.03f * (1f - 0.35f * EaseOut(l)), glow = 120f + 60f * l;
            var glowCol = Color.Lerp(new Color(0.63f, 0.78f, 1f), LobbyInk, cool);
            Ui.RadialGlow(new Rect(W / 2f - glow * 1.6f, H / 2f - glow, glow * 3.2f, glow * 2f), glowCol.WithAlpha((0.3f * l + 0.25f) * a));
            Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 4f, lw, 8f), new Color(0.6f, 0.77f, 1f, 0.35f * a));
            Ui.Fill(new Rect(W / 2f - lw / 2f, H / 2f - 1.5f, lw, 3f), Color.Lerp(Color.white, new Color(0.44f, 0.64f, 0.93f), cool).WithAlpha(a));
        }
    }
}

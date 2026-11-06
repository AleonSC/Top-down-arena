using UnityEngine;

namespace Arena
{
    /// How the CRT cabinet windows move (Docs/Mockups/crt-pause-menu.html; the cabinet and its windows are in CrtWindows.cs):
    /// the pause menu, Settings (main menu, lobby, pause menu), the lobby's menu, How to play and the profile.
    ///   Opening: the cabinet slides in from the left (0.3 s); then its screens power on and type (CrtWindows.cs).
    ///   Closing: the cabinet and its window slide back out (0.3 s); the window really closes when that's done.
    /// GL drawn inside a moving window (a boss preview, the effects preview) moves with it through `uiShift`.
    public partial class ArenaApp
    {
        const float WinCloseLen = 0.3f;

        float winOpenAt = -10f;         // when the window now drawn opened (or, for the pause menu, where its opening is)
        float winCloseK;                // 0..1: how far it has closed
        float winClosingAt = -1f;       // a window closing (not the pause menu's): when it started
        System.Action winAfterClose;    // what closes it for real, once it has slid out
        V2 uiShift;                     // added to the UI canvas's GL while a panel is moving (CanvasPx)
        Matrix4x4 shiftMatrix;          // GUI.matrix before PushShift
        bool shifted;

        float WinNow => Time.unscaledTime;
        float WinT => WinNow - winOpenAt;  // seconds since the window opened

        /// A window (not the pause menu) is sliding out: nothing in it can be clicked.
        bool WindowClosing => winClosingAt >= 0f;

        /// Draws are on Repaint only while a window closes (it takes no clicks).
        bool SkipWhileClosing => WindowClosing && Event.current.type != EventType.Repaint;

        /// How visible the window is, 0..1 (the dim layer behind it follows this).
        float WindowVis => Mathf.Clamp01(WinT / 0.3f) * (1f - winCloseK);

        /// Close a window with its slide: `done` closes it once it's out.
        void CloseWindow(System.Action done)
        {
            if (WindowClosing)
                return;
            winClosingAt = WinNow;
            winAfterClose = done;
        }

        /// Each frame: run a closing window's clock, then close it.
        void TickWindowClose()
        {
            if (!settingsOpen && !howToOpen && !profileOpen && !lobbyMenuOpen && pauseAnim <= 0f && !WindowClosing)
            {
                drawerKey = null;  // nothing open: the next window opens with its slide, however the last one closed
                if (cabWin)
                    ResetCabWindow();
            }
            if (!WindowClosing)
                return;
            winCloseK = Mathf.Clamp01((WinNow - winClosingAt) / WinCloseLen);
            if (winCloseK < 1f)
                return;
            var done = winAfterClose;
            winClosingAt = -1f;
            winAfterClose = null;
            winCloseK = 0f;
            drawerKey = null;  // the next one opens fresh
            ResetCabWindow();
            done?.Invoke();
        }

        static float EaseIn3(float x) => x * x * x;

        static float BackOut(float x)
        {
            const float c = 1.6f;
            float q = x - 1f;
            return 1f + (c + 1f) * q * q * q + c * q * q;
        }

        /// The drawer's slide (BeginDrawer): in from the left as it opens, out as it closes.
        float DrawerSlide => -(1f - EaseOut(Mathf.Clamp01(WinT / 0.3f))) * (DrawerW + 30f) - EaseIn3(winCloseK) * (DrawerW + 30f);

        /// Move everything drawn (IMGUI and the canvas's GL) by (dx, dy) and fade it, until PopShift.
        void PushShift(float dx, float dy, float alpha)
        {
            shiftMatrix = GUI.matrix;
            shifted = true;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + dx * scale, offY + dy * scale, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            uiShift = new V2(dx, dy);
            Ui.Alpha = alpha;
            var c = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, alpha);
        }

        void PopShift()
        {
            if (!shifted)
                return;
            shifted = false;
            GUI.matrix = shiftMatrix;
            uiShift = V2.Zero;
            Ui.Alpha = 1f;
            var c = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, 1f);
        }

        // --- The pause menu -------------------------------------------------------------------------------------------

        float pauseAnim;     // 0 closed .. 1 fully slid in; opening and closing take 0.3 s
        float pauseOpenedAt = -10f;  // when the pause menu last opened: its cabinet's clock (the opening plays once from here, CrtWindows.cs)

        /// Is the pause menu (or a screen opened from it) wanted?
        bool PauseMenuWanted => session.Screen == AppScreen.Game && (pauseMenuOpen || (game.Paused && !tabPaused && !devFrozen)) && !devMenuOpen && !labSetupOpen && !RecallActive;

        /// Each frame: run the pause menu's opening or closing, and its sounds.
        void StepPauseAnim(float dt)
        {
            bool want = PauseMenuWanted;
            float before = pauseAnim;
            pauseAnim = want ? Mathf.Min(1f, pauseAnim + dt / 0.3f) : Mathf.Max(0f, pauseAnim - dt / WinCloseLen);
            if (session.Screen != AppScreen.Game)
            {
                pauseAnim = 0f;
                return;
            }
            if (want && before <= 0f && pauseAnim > 0f)
            {
                drawerKey = null;  // its cabinet comes in afresh (also when a friend paused)
                pauseOpenedAt = Time.unscaledTime;
                ResetCabWindow();
                sounds.Play(Sfx.PauseSettle, 0.6f, Time.unscaledTime);  // a soft thunk as it slides in
            }
            if (!want && before >= 1f && pauseAnim < 1f)
                sounds.Play(Sfx.PauseClose, 0.5f, Time.unscaledTime);
        }

        /// The pause menu with its slide: in as it opens, out as it closes (nothing in it can be clicked meanwhile).
        void DrawPauseMenu()
        {
            bool closing = !PauseMenuWanted;
            if (closing && Event.current.type != EventType.Repaint)
                return;
            // The window clock runs from when the menu opened (its opening plays once); closing, everything is in and it slides out.
            float savedOpen = winOpenAt, savedClose = winCloseK;
            winOpenAt = closing ? WinNow - 10f : pauseOpenedAt;
            winCloseK = closing ? 1f - pauseAnim : 0f;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.55f * WindowVis));
            DrawPauseWindow();
            if (closing)
            {
                winOpenAt = savedOpen;
                winCloseK = savedClose;
            }
        }
    }
}

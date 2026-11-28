using UnityEngine;

namespace Arena
{
    /// How the CRT cabinet windows move (Docs/Mockups/crt-pause-menu.html; the cabinet and its windows are in CrtWindows.cs):
    /// the pause menu, Settings (main menu, lobby, pause menu), the lobby's menu, How to play and the profile.
    ///   Opening: the cabinet soft-sweeps in place (HoloBoot.SoftClip/SoftOver, LobbyFx.cs; with Reduced effects or Visual effects off it slides in from the left,
    ///   0.3 s); then its screens power on and type (CrtWindows.cs).
    ///   Closing: the cabinet and its window slide back out (0.3 s); the window really closes when that's done.
    ///   The pause menu and the lobby's Esc menu DOCK instead (`docking`, Docs/Mockups/pause-dock.html): the cabinet comes in from the left
    ///   and the run card (or the window out) from the right, each with two fading frame trails;
    ///   the keys slide in one by one. They leave the same ways, faster (DockOutLen). Reduced effects: a plain slide, no trails.
    /// GL drawn inside a moving window (a boss preview, the effects preview) moves with it through `uiShift`.
    public partial class ArenaApp
    {
        const float WinCloseLen = 0.3f;
        const float DockOutLen = 0.22f;  // the pause and lobby menus leave faster than the other windows

        float winOpenAt = -10f;         // when the window now drawn opened (or, for the pause menu, where its opening is)
        float winCloseK;                // 0..1: how far it has closed
        float winClosingAt = -1f;       // a window closing (not the pause menu's): when it started
        System.Action winAfterClose;    // what closes it for real, once it has slid out
        float winCloseLen = WinCloseLen;  // how long this closing takes
        V2 uiShift;                     // added to the UI canvas's GL while a panel is moving (CanvasPx)
        Matrix4x4 shiftMatrix;          // GUI.matrix before PushShift
        bool shifted;

        float WinNow => Time.unscaledTime;
        float winFreeze;  // while the pause menu closes: how long since it started closing (its windows' clocks stop there)
        float WinClock => WinNow - winFreeze;  // the clock a window's power-on runs on (CrtWindows.cs)
        float WinT => WinNow - winOpenAt;  // seconds since the window opened

        /// A window (not the pause menu) is sliding out: nothing in it can be clicked.
        bool WindowClosing => winClosingAt >= 0f;

        /// Draws are on Repaint only while a window closes (it takes no clicks).
        bool SkipWhileClosing => WindowClosing && Event.current.type != EventType.Repaint;

        /// How visible the window is, 0..1 (the dim layer behind it follows this).
        float WindowVis => Mathf.Clamp01(WinT / 0.3f) * (1f - winCloseK);

        /// Close a window with its slide: `done` closes it once it's out.
        void CloseWindow(System.Action done, float len = WinCloseLen)
        {
            if (WindowClosing)
                return;
            winClosingAt = WinNow;
            winCloseLen = len;
            winAfterClose = done;
        }

        /// Each frame: run a closing window's clock, then close it.
        void TickWindowClose()
        {
            if (!settingsOpen && !profileOpen && !lobbyMenuOpen && pauseAnim <= 0f && !WindowClosing)
            {
                drawerKey = null;  // nothing open: the next window opens with its slide, however the last one closed
                if (cabWin)
                    ResetCabWindow();
            }
            if (!WindowClosing)
                return;
            winCloseK = Mathf.Clamp01((WinNow - winClosingAt) / winCloseLen);
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

        /// The drawer's slide (BeginDrawer): in from the left as it opens, out as it closes.
        /// With effects on it holo-boots in place instead of sliding in (CrtWindows.cs, BeginDrawer); it still slides out.
        /// Centred (the pause menu, cabCentreK), it slides that much further to be out of sight.
        float DrawerSlide => ((FxCalm ? -(1f - EaseOut(Mathf.Clamp01(WinT / 0.3f))) * DrawerSlideW : docking ? DockDrawerIn(WinT) : 0f) - EaseIn3(winCloseK) * DrawerSlideW) * CentreSlideScale + CabCentreDX;

        float CentreSlideScale => 1f + CabCentreDX / DrawerSlideW;

        // --- Docking (the pause menu and the lobby's Esc menu) ---------------------------------------------------------

        bool docking;  // drawing the pause menu or the lobby's menu: they dock in from the sides (DockDrawerIn, DockPanelX)
        bool Dock => docking && !FxCalm;


        /// The cabinet's x offset `age` seconds into the opening: in from the left (0.28 s).
        static float DockDrawerIn(float age) => -(1f - EaseOut(Seg(age, 0f, 0.28f))) * DrawerSlideW;

        float DockPanelW => W - DrawerPanel.x + 40f;  // how far right the run card goes to be out of sight

        /// The run card's (or the window's) x offset `age` seconds in: in from the right a beat after the cabinet (0.04-0.32 s).
        float DockPanelIn(float age) => (1f - EaseOut(Seg(age, 0.04f, 0.32f))) * DockPanelW;

        /// Where the run card or the window is: docking in, or (the menu closing) back out to the right.
        float DockPanelX => (Dock ? DockPanelIn(WinT) : 0f) + EaseIn3(winCloseK) * DockPanelW;

        /// The two frames trailing a box as it docks (drawn under it), `lag` seconds behind where `xAt` puts it.
        void DockTrails(Rect r, System.Func<float, float> xAt, Color c)
        {
            float age = WinT;
            if (!Dock || winCloseK > 0f || age < 0.02f || age > 0.34f)
                return;
            for (int i = 0; i < 2; i++)
            {
                float a = 0.4f / (i + 1) * (1f - Seg(age, 0.26f, 0.34f));
                CrtKit.Frame(new Rect(r.x + xAt(age - (i + 1) * 0.035f), r.y, r.width, r.height), 2f, c.WithAlpha(a));
            }
        }

        /// A key in a docking cabinet: how far it has come in (0..1); it slides in from the left after the box (0.18 s on, 0.03 s apart).
        float DockKeyK(int i) => Dock ? EaseOut(Seg(WinT, 0.18f + i * 0.03f, 0.3f + i * 0.03f)) : 1f;

        /// Move everything drawn (IMGUI and the canvas's GL) by (dx, dy) and fade it, until PopShift.
        void PushShift(float dx, float dy, float alpha)
        {
            shiftMatrix = GUI.matrix;
            shifted = true;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + dx * scale, offY + dy * scale, 0f), Quaternion.identity, new Vector3(scale, scale, 1f));
            uiShift = new V2(dx, dy);
            Ui.Alpha = alpha;
            var c = GUI.color;
            GUI.color = c.WithAlpha(alpha);
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
            GUI.color = c.WithAlpha(1f);
        }

        // --- The pause menu -------------------------------------------------------------------------------------------

        float pauseAnim;     // 0 closed .. 1 fully in; opening takes 0.3 s, closing DockOutLen
        float pauseOpenedAt = -10f;  // when the pause menu last opened: its cabinet's clock (the opening plays once from here, CrtWindows.cs)
        float pauseClosedAt = -1f;   // when it started closing (-1: it isn't): its opening's clock stops there, so nothing pops in as it slides out

        /// Is the pause menu (or a screen opened from it) wanted?
        bool PauseMenuWanted => session.Screen == AppScreen.Game && (pauseMenuOpen || (game.Paused && !tabPaused && !wikiPickSticky)) && !ModalScreenOpen && !RecallActive;  // (PICK keeps a solo run paused with no menu over it)

        /// Each frame: run the pause menu's opening or closing, and its sounds.
        void StepPauseAnim(float dt)
        {
            bool want = PauseMenuWanted;
            float before = pauseAnim;
            pauseAnim = want ? Mathf.Min(1f, pauseAnim + dt / 0.3f) : Mathf.Max(0f, pauseAnim - dt / DockOutLen);
            if (session.Screen != AppScreen.Game)
            {
                pauseAnim = 0f;
                return;
            }
            if (want && before <= 0f && pauseAnim > 0f)
            {
                drawerKey = null;  // its cabinet comes in afresh (also when a friend paused)
                pauseOpenedAt = Time.unscaledTime;
                pauseClosedAt = -1f;
                ResetCabWindow();
                sounds.Play(Sfx.PauseSettle, 0.6f, Time.unscaledTime);  // a soft thunk as it slides in
            }
            else if (want && pauseClosedAt >= 0f)
            {
                pauseOpenedAt += Time.unscaledTime - pauseClosedAt;  // reopened while sliding out: its opening carries on from where it stopped
                pauseClosedAt = -1f;
            }
            if (!want && pauseClosedAt < 0f && pauseAnim > 0f)
                pauseClosedAt = Time.unscaledTime;
            if (!want && before >= 1f && pauseAnim < 1f)
                sounds.Play(Sfx.PauseClose, 0.5f, Time.unscaledTime);
        }

        /// The pause menu with its slide: in as it opens, out as it closes (nothing in it can be clicked meanwhile).
        float pauseCentre = 1f, pauseCentreAt = -10f;

        /// The pause menu's list is centred; with a window out beside it (Settings, How to play) it glides to the left edge (0.25 s).
        /// A frame after a gap (the menu just opened) snaps to where it belongs.
        void StepPauseCentre()
        {
            float want = pauseSettings || pauseHowTo ? 0f : 1f;
            float dt = WinNow - pauseCentreAt;
            if (Event.current.type == EventType.Repaint)
            {
                pauseCentre = dt < 0f || dt > 0.1f ? want : Mathf.MoveTowards(pauseCentre, want, dt / 0.25f);
                pauseCentreAt = WinNow;
            }
            cabCentreK = pauseCentre * pauseCentre * (3f - 2f * pauseCentre);
        }

        void DrawPauseMenu()
        {
            bool closing = !PauseMenuWanted;
            if (closing && Event.current.type != EventType.Repaint)
                return;
            // Just wanted (Esc this frame): its opening starts next frame (StepPauseAnim); drawn now, the last opening's clock would show it fully open.
            if (!closing && pauseAnim <= 0f)
                return;
            // The window clock runs from when the menu opened (its opening plays once); closing, it stops where it was (a half-done opening
            // slides out half-done, nothing still to come pops in) and the menu slides out.
            float savedOpen = winOpenAt, savedClose = winCloseK;
            winOpenAt = closing ? pauseOpenedAt + WinNow - (pauseClosedAt >= 0f ? pauseClosedAt : WinNow) : pauseOpenedAt;
            winCloseK = closing ? 1f - pauseAnim : 0f;
            winFreeze = closing && pauseClosedAt >= 0f ? WinNow - pauseClosedAt : 0f;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.55f * WindowVis));
            docking = true;
            StepPauseCentre();
            DrawPauseWindow();
            cabCentreK = 0f;  // (the lobby's menu and the rest stay at the edge)
            docking = false;
            winFreeze = 0f;
            if (closing)
            {
                winOpenAt = savedOpen;
                winCloseK = savedClose;
            }
        }
    }
}

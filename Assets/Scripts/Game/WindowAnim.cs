using UnityEngine;

namespace Arena
{
    /// How the side-drawer windows move ("slide and stagger", Docs/Mockups/menu-animations.html option A): the pause
    /// menu, Settings (main menu, lobby, pause menu), Records and How to play.
    ///   Opening: the drawer slides in from the left (0.3 s); its rows follow one after another (from 0.18 s, 0.06 s
    ///            apart); the line under the title draws across; the party cards rise; the run card (or the open
    ///            section's panel) drops in with a small bounce.
    ///   A new list in the same drawer (pause menu → Settings → How to play, and back): its title rises in and its rows
    ///            come in from the right, one after another. A new section: its panel slides in from the right.
    ///   Closing: the drawer slides back out and the panels fade (0.3 s); the window really closes when that's done.
    /// GL drawn inside a moving panel (a boss preview, the effects preview) moves with it through `uiShift`.
    public partial class ArenaApp
    {
        const float WinOpenLen = 0.9f, WinCloseLen = 0.3f, RowsAt = 0.18f, RowStep = 0.06f, RowTime = 0.32f, PanelAt = 0.35f, PanelTime = 0.28f;

        float winOpenAt = -10f;         // when the window now drawn opened (or, for the pause menu, where its opening is)
        float winCloseK;                // 0..1: how far it has closed
        float winClosingAt = -1f;       // a window closing (not the pause menu's): when it started
        System.Action winAfterClose;    // what closes it for real, once it has slid out
        string listKey, panelKey;       // the drawer's list (its title) and the open section's panel, last frame
        float listAt = -10f, panelAt = -10f;
        bool listFromRight;
        int rowIndex;                   // DrawerRow's count in the list being drawn (its stagger)
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
            if (!settingsOpen && !howToOpen && !recordsOpen && !cosmeticsOpen && !lobbyMenuOpen && pauseAnim <= 0f && !WindowClosing)
                drawerKey = null;  // nothing open: the next window opens with its slide, however the last one closed
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

        /// A list starts drawing (BeginDrawer, with its title as its key): a new one staggers in.
        void NoteList(string key)
        {
            if (key == listKey)
                return;
            bool opening = WinT < 0.15f || listKey == null;  // the window's first list arrives with it
            listKey = key;
            listAt = opening ? winOpenAt + RowsAt : WinNow;
            listFromRight = !opening;
            rowIndex = 0;
        }

        /// The title's rise as a new list arrives (0..1).
        float TitleIn => listFromRight ? EaseOut(Mathf.Clamp01((WinNow - listAt) / 0.25f)) : 1f;

        /// The line under the title drawing across (0..1).
        float UnderlineIn => EaseOut(Mathf.Clamp01((WinT - 0.3f) / 0.5f));

        /// The next row's stagger: (sideways offset, alpha).
        Vector2 NextRowAnim()
        {
            int i = rowIndex++;
            float k = EaseOut(Mathf.Clamp01((WinNow - listAt - i * RowStep) / RowTime));
            return new Vector2((listFromRight ? 40f : -50f) * (1f - k), k);
        }

        /// The party cards' rise (an offset down, 0 when in) and alpha.
        Vector2 PartyAnim
        {
            get
            {
                float k = EaseOut(Mathf.Clamp01((WinT - 0.5f) / 0.35f));
                return new Vector2(30f * (1f - k), k);
            }
        }

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

        /// The open section's panel (or the pause menu's run card) moving in: a new one slides in from the right; the
        /// first, as the window opens, drops in with a bounce. It fades as the window closes.
        void BeginPanelAnim(string key, bool drop = false)
        {
            if (key != panelKey)
            {
                // The first panel comes with the window while it's opening; one shown later (a new section, or a list
                // opened from another, like the pause menu's How to play) slides in now. (It used to count from the
                // window's opening whenever the list changed, so those appeared all at once.)
                panelAt = WinT < PanelAt ? winOpenAt + PanelAt : WinNow;
                panelKey = key;
            }
            float k = Mathf.Clamp01((WinNow - panelAt) / (drop && WinNow - winOpenAt < 1.2f ? 0.45f : PanelTime));
            float fade = 1f - Mathf.Clamp01(winCloseK / 0.6f);
            bool opening = panelAt - winOpenAt < 0.5f;
            if (opening)
                PushShift(0f, -40f * (1f - BackOut(k)), Mathf.Clamp01(k * 2f) * fade);
            else
                PushShift(80f * (1f - EaseOut(k)), 0f, k * fade);
        }

        void EndPanelAnim() => PopShift();

        // --- The pause menu -------------------------------------------------------------------------------------------

        float pauseAnim;  // 0 closed .. 1 fully open; opening takes WinOpenLen, closing WinCloseLen

        /// Is the pause menu (or a screen opened from it) wanted?
        bool PauseMenuWanted => session.Screen == AppScreen.Game && (pauseMenuOpen || (game.Paused && !tabPaused && !devFrozen)) && !devMenuOpen && !labSetupOpen && !RecallActive;

        /// Each frame: run the pause menu's opening or closing, and its sounds.
        void StepPauseAnim(float dt)
        {
            bool want = PauseMenuWanted;
            float before = pauseAnim;
            pauseAnim = want ? Mathf.Min(1f, pauseAnim + dt / WinOpenLen) : Mathf.Max(0f, pauseAnim - dt / WinCloseLen);
            if (session.Screen != AppScreen.Game)
            {
                pauseAnim = 0f;
                return;
            }
            if (want && before <= 0f && pauseAnim > 0f)
            {
                drawerKey = listKey = panelKey = null;  // its list and run card come in afresh (also when a friend paused)
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
            // The window clock follows pauseAnim: opening, it's that far in; closing, everything is in and it slides out.
            float savedOpen = winOpenAt, savedClose = winCloseK;
            winOpenAt = closing ? WinNow - 10f : WinNow - pauseAnim * WinOpenLen;
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

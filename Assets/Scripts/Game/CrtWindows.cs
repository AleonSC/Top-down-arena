using System;
using UnityEngine;

namespace Arena
{
    /// The drawer the in-game menus are made of (the lobby loadout's box, Docs/Mockups/pause-settings-loadout-box.html): the pause menu, the lobby's Esc menu,
    /// Settings, How to play and the profile. Pieces:
    ///   BeginDrawer / DrawerRow / EndDrawerScreen / EndDrawer   the box on the left: a title ("PAUSED_") and a line under it, then
    ///       the option keys, and (drawn by the caller, CabBackKey) a digital Back key right under the last one. Its OPENING plays
    ///       once, from the window's clock (WinT, `CabAge`): the box soft-sweeps in (HoloBoot.SoftClip/SoftOver, LobbyFx.cs: shown top-down under a
    ///       feathered edge), the title types, its sub-line types a boot log and
    ///       then the real line, a scan bar sweeps the list; the keys come in under the box's sweep (KeyOpening) with their texts typing in. After CabOpenEnd seconds nothing here animates (a list
    ///       that changes retypes its texts, CrtKit.Typed, and that is all). Reduced effects (fxReduced) or Visual effects off:
    ///       no animation.
    ///   OpenCabWindow / CloseCabWindow / DrawCabWindow          the window to the right of the box (a second loadout box). It soft-sweeps
    ///       in place the FIRST time something is picked; later picks only retype its text; closing slides it back
    ///       in. BeginPanelAnim / OpenSectionPanel / EndPanelAnim are the window's frame and title (a panel drawn without
    ///       OpenCabWindow, like the pause menu's run summary, comes in after the box).
    ///   WinKey / WinChoices / WinSlider / SwitchRow / WinLabel   the digital controls the windows are filled with.
    public partial class ArenaApp
    {
        const float CabOpenEnd = 3.5f;  // seconds after opening: the opening is over, nothing animates any more
        static Color CabSoft => CrtStyle.Desc;       // small descriptions on a screen (CrtStyle.cs)
        static readonly Color CabDanger = CrtStyle.Danger;
        static readonly Color CabWarm = new Color(1f, 0.82f, 0.45f);         // a changed key

        float CabAge => WinT;  // seconds since the cabinet opened

        /// What the header's sub-line types before it settles on the real line (the pause menu adds the wave).
        string cabBootLog = "> LINK OK   > SAVE OK";

        /// For previews (Tools/Preview): typed text in a window counts as already typed.
#pragma warning disable 0649  // set only by Tools/Preview (reflection)
        bool cabInstant;
#pragma warning restore 0649

        // --- Typed text -------------------------------------------------------------------------------------------------

        /// A label on the cabinet: it types in once during the opening (stateless, from `delay` seconds after it opened), and
        /// when its text changes afterwards (a list that switches) it rewrites in place (CrtKit.Typed, CrtKit.Morph).
        string CabText(string slot, string text, float delay)
        {
            float now = WinNow;
            string full = CrtKit.Typed(slot, text, now, FxCalm, -10f);  // (a slot seen for the first time counts as typed)
            if (FxCalm || CabAge >= CabOpenEnd || !CrtKit.Settled(slot, now) || string.IsNullOrEmpty(text))
                return full;
            return CrtKit.Reveal(text, CabAge - delay, 0.3f, false);
        }

        /// A label in a window: types in when the window opens and retypes when its text changes (slots are by position).
        string WinText(string slot, string text) => CrtKit.Typed(slot, text, WinNow, FxCalm, cabInstant ? -10f : 0f);

        // --- The cabinet ------------------------------------------------------------------------------------------------

        Rect cabScreen;
        int rowIndex;  // DrawerRow's count in the list being drawn (its typing slot and opening delay)
        bool cabScreenOn, cabScreenOpen;
        float cabEdgeVis;  // how visible the soft edges are this frame (0: no cabinet menu was drawn)
        float cabScreenAge;

        /// The cabinet: its chassis, the header screen and the options screen (rows go in it until EndDrawerScreen). What
        /// follows is drawn in cabinet coordinates (the inside of its outline, CabW x CabH) until EndDrawer. `screen` is the options
        /// screen's rect (default 26,150, 358 x 456); `header` the header screen's (default 26,26, 358 x 100; a shorter one gets a
        /// tighter title and sub-line).
        /// The drawer is the lobby loadout's box (CrtKit.LoadoutBox) with the title and keys straight on it, no screens inside.
        void BeginDrawer(string title, Color accent, float k, string sub = null, int titleSize = 44, Rect? screen = null, Rect? header = null)
        {
            float dx = DrawerSlide;  // in from the left as the window opens, out as it closes (WindowAnim.cs)
            bool calm = FxCalm;
            float age = CabAge, now = WinNow;
            rowIndex = 0;
            if (age >= CabOpenEnd && !calm)
                CrtKit.SeedRows(16, now);
            cabEdgeVis = WindowVis;  // the soft edges go over everything (DrawCabEdges)
            // The soft sweep (HoloBoot, LobbyFx.cs): the box shows top-down under a feathered edge as it opens (EndDrawer).
            cabBootE = calm || Dock ? 99f : age;  // (closing, the pause menu's clock is stopped: a half-swept box slides out as it is; docking: no sweep)
            cabBootRect = new Rect(dx + CabX, CabY, CabW + 2f, CabH + 2f);
            DockTrails(new Rect(CabX, CabY, CabW + 2f, CabH + 2f), a => DockDrawerIn(a), CrtStyle.Ink);  // (docking: WindowAnim.cs)
            float bootH = Mathf.Min(H, HoloBoot.SoftClip(cabBootRect, cabBootE));
            GUI.BeginGroup(new Rect(dx, 0f, CabX + CabW + 2f + 60f, bootH));
            CrtKit.LoadoutBox(new Rect(CabX, CabY, CabW + 2f, CabH + 2f), !CabNeon);
            if (cabGlass)  // the pause and lobby menus: the Tab screen's hologram glass (TabHolo.cs)
            {
                DrawHoloGlass(new Rect(CabX + 1f, CabY + 1f, CabW, CabH), WinNow);
                CabShade(new Rect(CabX + 1f, CabY + 1f, CabW, CabH));
            }
            GUI.BeginGroup(new Rect(CabX + 1f, CabY + 1f, CabW, CabH));  // the cabinet's inside: its own origin

            // The header screen: big phosphor text and a small line under it.
            var hr = header ?? new Rect(26f, 26f, CabInW, 100f);
            bool tight = hr.height < 90f;
            float ty = tight ? 2f : 6f, th = tight ? 46f : 56f, sy = tight ? 50f : 64f;
            float ha = age - (Dock ? 0.14f : 0.5f);  // (docking, the title types as the box lands)
            if (ha > 0f || calm)  // a thin rule under the title and its line
                Ui.Fill(new Rect(hr.x, hr.yMax + 4f, hr.width, 1f), CrtUi.T(new Color(0.12f, 0.23f, 0.4f)));
            if (calm || ha >= 0f)
            {
                int size = Mathf.Min(titleSize, 40);
                while (size > 16 && CrtUi.TitleWidth(title, size) > hr.width - 60f)
                    size--;
                string t = CabText("cab.title", title, Dock ? 0.16f : 0.8f);
                CrtUi.Title(new Rect(hr.x + 18f, hr.y + ty, hr.width - 36f, th), t, size, CrtStyle.Ink, TextAnchor.MiddleLeft, calm);
                if (calm || Mathf.FloorToInt(now * 2f) % 2 == 0)  // the blinking block after the text
                {
                    float tw = t.Length == 0 ? 0f : CrtUi.TitleWidth(t, size);
                    Ui.Fill(new Rect(hr.x + 22f + tw, hr.y + ty + th / 2f - size * 0.36f, Mathf.Max(8f, size * 0.36f), size * 0.72f), CrtStyle.Ink);
                }
                string subFull = string.IsNullOrEmpty(sub) ? "" : sub.ToUpperInvariant();
                string subShown = CrtKit.Typed("cab.sub", subFull, now, calm, -10f);
                if (!calm && age < CabOpenEnd && CrtKit.Settled("cab.sub", now))
                    subShown = CrtKit.BootLine(cabBootLog, subFull, age - (Dock ? 0.3f : 0.98f), false);  // the opening: a boot log first
                Ui.Label(new Rect(hr.x + 20f, hr.y + sy, hr.width - 40f, 20f), subShown, 12, CabSoft, TextAnchor.MiddleLeft, true);
            }

            // The options screen.
            cabScreen = screen ?? new Rect(26f, 150f, CabInW, 456f);
            cabScreenAge = age;  // (its keys come in under the box's sweep, KeyOpening)
            cabScreenOpen = true;
            cabScreenOn = calm || cabScreenAge >= 0f;
        }

        /// The scan bar that sweeps the options list in the opening.
        void EndDrawerScreen()
        {
            if (!cabScreenOpen)
                return;
            cabScreenOpen = false;
            if (!cabScreenOn)
                return;
        }

        void EndDrawer()
        {
            EndDrawerScreen();
            GUI.EndGroup();
            GUI.EndGroup();
            HoloBoot.SoftOver(cabBootRect, cabBootE);
            if (CabNeon)
                DrawPanelNeon(cabBootRect, cabBootE, 7f, 1.4f);  // Settings and Profile: the neon edge and glowing corners (MenuHolo.cs)
        }

        /// Over a menu box's glass, under its contents: a dark navy shade so the box reads darker than the glass alone.
        static void CabShade(Rect r) => Ui.Fill(r, CrtStyle.Greyed ? new Color(0.01f, 0.01f, 0.012f, 0.42f) : new Color(0.004f, 0.008f, 0.025f, 0.42f));

        bool cabGlass;  // the pause, lobby, Settings and Profile cabinets and the pause run card are hologram glass (their windows too) (set round their drawing, MenuDrawer.cs)
        float cabBootE = 99f;  // seconds into the cabinet's holo boot (99: none)
        Rect cabBootRect;

        /// Each frame a cabinet menu was drawn, over it and under the cursor: the screen's soft edges (CrtKit.SoftEdges).
        void DrawCabEdges()
        {
            if (cabEdgeVis > 0f)
                CrtKit.SoftEdges(new Rect(0f, 0f, W, H), cabEdgeVis);
            cabEdgeVis = 0f;
        }

        /// A digital key in the cabinet's options screen: its name and a small line, inverse video under the mouse or when
        /// it's the selected one (`on`). `key` is a hint tag at the right (Esc), `tag` a plain note there. Texts show with the key
        /// and retype when the list switches. Returns true when clicked.
        bool DrawerRow(Rect r, Texture2D icon, string name, string line, string key, string tag, Color col, bool on, Color? tagColor = null, bool danger = false)
        {
            int i = rowIndex++;
            if (!cabScreenOn)
                return false;
            float x0 = Mathf.Max(r.x, cabScreen.x + 8f), x1 = Mathf.Min(r.xMax, cabScreen.xMax - 8f);
            r = new Rect(x0, r.y, x1 - x0, r.height);
            float ka;
            if (!KeyOpening(r, i, out ka))
                return false;
            float dk = DockKeyK(i);  // docking: the keys slide in one by one, faded and not clickable yet (WindowAnim.cs)
            if (dk <= 0f)
                return false;
            if (dk >= 1f)
                return DrawerKey(r, i, icon, name, line, key, tag, col, on, tagColor, danger);
            float oldA = Ui.Alpha;
            var oldC = GUI.color;
            bool oldE = GUI.enabled;
            Ui.Alpha = oldA * dk;
            GUI.color = oldC.WithAlpha(oldC.a * dk);
            GUI.enabled = false;
            DrawerKey(new Rect(r.x - 30f * (1f - dk), r.y, r.width, r.height), i, icon, name, line, key, tag, col, on, tagColor, danger);
            Ui.Alpha = oldA;
            GUI.color = oldC;
            GUI.enabled = oldE;
            return false;
        }

        bool DrawerKey(Rect r, int i, Texture2D icon, string name, string line, string key, string tag, Color col, bool on, Color? tagColor, bool danger)
        {
            if (CrtStyle.Greyed && !danger)
                col = CrtStyle.Ink;  // a grey screen (the pause and lobby menus): every key in its one colour, like the Game Mode screen
            string slot = "cab.r" + i;
            // The texts are there as soon as the key is (blank keys read as broken while they typed in); they still retype when the list switches.
            string n = CrtKit.Typed(slot + ".n", name, WinNow, FxCalm, -10f);
            string s = string.IsNullOrEmpty(line) ? null : CrtKit.Typed(slot + ".s", line, WinNow, FxCalm, -10f);
            if (s != null && s.Length == 0)
                s = " ";  // (the two-line layout holds while the line is still typing)
            bool clicked = CrtKit.Key(r, n, s, icon, on, FxCalm, WinNow, null, r.height >= 54f ? 24 : 20, col, danger);
            bool inv = on || (GUI.enabled && r.Contains(Event.current.mousePosition));
            Color tc = CrtKit.SoftText(danger ? CabDanger : col, inv);
            if (!string.IsNullOrEmpty(key))
            {
                string hk = key.ToUpperInvariant();
                float kw = Ui.TextWidth(hk, 12, true) + 4f;
                var kr = new Rect(r.xMax - 12f - kw, r.y + 9f, kw, 20f);
                CrtKit.Frame(kr, 1f, tc.WithAlpha(0.8f));
                Ui.Label(kr, hk, 12, tc, TextAnchor.MiddleCenter, true);
            }
            else if (!string.IsNullOrEmpty(tag))
                Ui.Label(new Rect(r.xMax - 126f, r.y, 114f, r.height), tag, 12, inv ? CrtKit.SoftText(col, true) : (tagColor ?? CabSoft), TextAnchor.MiddleRight, true);
            return clicked;
        }

        /// The small hint lines at the cabinet's foot.
        void CabFoot(string a, string b = null)
        {
            var c = CrtStyle.Greyed ? CabSoft : new Color(0.49f, 0.56f, 0.78f);
            if (a != null)
                Ui.Label(new Rect(30f, CabH - 44f, CabW - 50f, 16f), a, 11, c, TextAnchor.MiddleLeft, false);
            if (b != null)
                Ui.Label(new Rect(30f, CabH - 28f, CabW - 50f, 16f), b, 11, c, TextAnchor.MiddleLeft, false);
        }

        /// A key of the options list as the drawer opens: the keys come in with the box, under its soft sweep (BeginDrawer).
        /// `age` is seconds since then.
        /// False until it starts. After the opening, and with Reduced effects, every key is simply there.
        const float KeyBootAt = 0f;  // with the box itself

        bool KeyOpening(Rect r, int i, out float age)
        {
            age = CabAge - KeyBootAt;
            if (FxCalm || CabAge >= CabOpenEnd)
            {
                age = 99f;
                return true;
            }
            return age > 0f;
        }

        /// The Back key at `y` in the drawer, right under the options list (Settings, How to play, the profile). It comes in
        /// after the last key. True when pressed.
        bool CabBackKey(float y, string line = "Close this menu", float h = 76f)
        {
            if (!cabScreenOn)
                return false;
            // A digital key like the rows above it (DrawerRow), in a dim grey so it reads apart from the options.
            var r = new Rect(34f, Mathf.Min(y, CabH - h - 8f), CabRowW, h);
            return DrawerRow(r, PixelArt.Get("ui_back"), "Back", h >= 54f ? line : null, "Esc", null, new Color(0.62f, 0.68f, 0.8f), false);
        }

        // --- The window to the right ------------------------------------------------------------------------------------

        bool cabWin;           // a window is out (or sliding)
        int cabKind;           // 0 a panel drawn by its own screen, 1 a Settings section, 2 How to play, 3 a profile option
        float cabWinInAt, cabWinOutAt = -1f;
        Action cabWinAfter;
        Rect cabWinRect;
        bool cabWinOn, cabWinEnabledWas;
        float cabWinPower;
        int cabWinRow;         // labels in the window so far (their typing slots)

        /// Open the window with content kind 1 (a Settings section), 2 (How to play) or 3 (a profile option). The first time it slides out from behind
        /// the cabinet and powers on; when one is already out, only its text retypes.
        void OpenCabWindow(int kind)
        {
            if (!cabWin || cabWinOutAt >= 0f)
            {
                cabWin = true;
                cabWinInAt = WinClock;
                CrtKit.Forget("win.");
            }
            cabWinOutAt = -1f;
            cabWinAfter = null;
            cabKind = kind;
        }

        /// Slide the window back in (Esc, its X key, Back). `after` runs once it is in.
        void CloseCabWindow(Action after = null)
        {
            Keybinds.Listening = -1;
            if (!cabWin || cabWinOutAt >= 0f)
            {
                after?.Invoke();
                return;
            }
            if (FxCalm)
            {
                cabWin = false;
                pauseHowTo = false;
                after?.Invoke();
                return;
            }
            cabWinOutAt = WinNow;
            cabWinAfter = () =>
            {
                pauseHowTo = false;  // (the pause or lobby menu's How to play is just a window)
                after?.Invoke();
            };
        }

        /// The window is gone and nothing waits on it (a new menu opening, a screen closing).
        void ResetCabWindow()
        {
            if (cabWin)
                CrtKit.Forget("win.");
            cabWin = false;
            cabWinOutAt = -1f;
            cabWinAfter = null;
            Keybinds.Listening = -1;
        }

        /// A window that's closing is gone once its slide is done.
        void TickCabWindow()
        {
            if (cabWin && cabWinOutAt >= 0f && WinNow - cabWinOutAt >= WinCloseLen)
            {
                var a = cabWinAfter;
                cabWin = false;
                cabWinOutAt = -1f;
                cabWinAfter = null;
                a?.Invoke();
            }
        }

        /// Is a window out and not on its way back in? (what Esc closes first)
        bool CabWindowOpen => cabWin && cabKind != 0 && cabWinOutAt < 0f;

        /// How far out the window is, 0 hidden .. 1 in place (the run card fades as it comes).
        float CabWinSlideK(bool menuClose = true)
        {
            if (!cabWin)
                return 0f;
            const float inK = 1f;  // no slide out: with effects on it soft-sweeps in place (BeginPanelAnim), and calm it is just there
            float outK = cabWinOutAt >= 0f ? EaseIn3(Mathf.Clamp01((WinNow - cabWinOutAt) / WinCloseLen)) : 0f;
            return inK * (1f - outK) * (menuClose ? 1f - EaseIn3(winCloseK) : 1f);
        }

        /// Each frame the cabinet is up: draw the window it holds (behind the cabinet, so it comes out from under it).
        void DrawCabWindow()
        {
            TickCabWindow();
            if (!cabWin || cabKind == 0)
                return;
            if (cabKind == 1)
                DrawSettingsSection(Time.unscaledTime);
            else if (cabKind == 3)
                DrawProfileSection(Time.unscaledTime);  // ProfileWindows.cs
            else
                DrawHowToBody(Time.unscaledTime);
        }

        /// The window's frame: a CRT screen in the drawer panel's place. Its content (drawn in the same coordinates, moving
        /// with it) is hidden until it has powered on. A panel drawn without OpenCabWindow (the profile) powers on in place
        /// after the cabinet instead of sliding.
        string panelKeyDrawn;  // the panel last drawn (a different one types in from nothing)

        void BeginPanelAnim(string key)
        {
            if (key != panelKeyDrawn)
            {
                // A new section's layout shows at once, so its labels must not erase the last section's text first
                // (that read as the window switching, then rewriting itself): they type in from nothing.
                panelKeyDrawn = key;
                CrtKit.Forget("win.");
            }
            if (!cabWin)
            {
                cabWin = true;
                cabKind = 0;
                cabWinOutAt = -1f;
                cabWinInAt = WinT < 1.3f ? WinClock + 1.3f - WinT : WinClock;
                CrtKit.Forget("win.");
            }
            // (In a docking menu it comes and goes with the menu from the right, DockPanelX; on its own it goes back in behind the cabinet.)
            float k = CabWinSlideK(!docking);
            float dx = -(DrawerPanel.xMax + 20f) * (1f - k) + (docking ? DockPanelX : 0f);
            cabWinPower = cabKind == 0 || !FxCalm ? WinClock - cabWinInAt : WinClock - cabWinInAt - 0.2f;
            cabWinRect = new Rect(DrawerPanel.x + dx, DrawerPanel.y, DrawerPanel.width, DrawerPanel.height);
            cabWinOn = FxCalm || cabWinPower >= 0f;  // the loadout box slides out whole, and its content with it
            cabWinBootE = FxCalm || cabWinOutAt >= 0f ? 99f : cabWinPower;  // (closing mid-boot, it slides out as it was)
            // The soft sweep (HoloBoot, LobbyFx.cs): the window shows top-down under a feathered edge (EndPanelAnim).
            cabWinBooting = cabWinOn && cabWinBootE < HoloBoot.SoftLen;
            if (cabWinBooting)
                GUI.BeginGroup(new Rect(0f, 0f, W, HoloBoot.SoftClip(cabWinRect, cabWinBootE)));
            if (cabWinOn)
            {
                CrtKit.LoadoutBox(cabWinRect, !CabNeon);
                if (cabKind != 0)  // a Settings section, How to play or a profile option: the Tab screen's hologram glass (TabHolo.cs)
                {
                    DrawHoloGlass(new Rect(cabWinRect.x + 1f, cabWinRect.y + 1f, cabWinRect.width - 2f, cabWinRect.height - 2f), WinNow);
                    CabShade(new Rect(cabWinRect.x + 1f, cabWinRect.y + 1f, cabWinRect.width - 2f, cabWinRect.height - 2f));
                }
            }
            cabWinEnabledWas = GUI.enabled;
            if (!cabWinOn || cabWinOutAt >= 0f)
                GUI.enabled = false;
            PushShift(dx, 0f, cabWinOn ? 1f : 0f);
            cabWinRow = 0;
        }

        float cabWinBootE = 99f;
        bool cabWinBooting;

        float panelSwitchAt = -1f;

        /// Which section a window draws: when the pick changes, the section on screen first backspaces its text away
        /// (CrtKit.EraseAll, held by CrtKit.Hold), then the new one is shown and types in. Not while the window is opening.
        int PanelShown(ref int shown, int want)
        {
            if (FxCalm || !cabWin || WinClock - cabWinInAt < 0.8f || shown == want)
            {
                shown = want;
                panelSwitchAt = -1f;
                return shown;
            }
            if (panelSwitchAt < 0f)
            {
                panelSwitchAt = WinNow;
                CrtKit.EraseAll("win.", WinNow);
            }
            if (WinNow - panelSwitchAt >= CrtKit.EraseLen)
            {
                shown = want;
                panelSwitchAt = -1f;
            }
            else
                CrtKit.Hold = true;
            return shown;
        }

        void EndPanelAnim()
        {
            CrtKit.Hold = false;
            PopShift();
            GUI.enabled = cabWinEnabledWas;
            if (cabWinBooting)
                GUI.EndGroup();
            cabWinBooting = false;
            if (cabWinOn)
                HoloBoot.SoftOver(cabWinRect, cabWinBootE);
            if (cabWinOn && CabNeon)
                DrawPanelNeon(cabWinRect, cabWinBootE, 9f, 3f);
        }

        /// The window's heading: its title (big, typed, with the cursor), a small line under it and a rule; an X key at the
        /// right closes the windows that came out of the cabinet.
        void OpenSectionPanel(string title, Color col, string sub, float k)
        {
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y;
            string t = WinText("win.title", title);
            CrtUi.Title(new Rect(x, y + 12f, w - 90f, 44f), t, 34, CrtStyle.Ink, TextAnchor.MiddleLeft, FxCalm);
            if (FxCalm || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
            {
                float tw = t.Length == 0 ? 0f : CrtUi.TitleWidth(t, 34);
                Ui.Fill(new Rect(x + 4f + tw, y + 24f, 14f, 22f), CrtStyle.Ink);
            }
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(x + 2f, y + 54f, w - 4f, 34f), WinText("win.sub", sub), 13, CabSoft, TextAnchor.UpperLeft, false, true);
            DashLine(x, y + 90f, w);
            if (cabKind != 0 && DkKey(new Rect(DrawerPanel.xMax - 28f - 46f, y + 16f, 46f, 36f), "X", false, FxCalm, size: 18))
            {
                if (cabHolo)
                {
                    Keybinds.Listening = -1;
                    CloseWindow(() => settingsOpen = false);  // Settings: its X closes the whole screen (there's no Back key), sliding out (WindowAnim.cs)
                }
                else
                    CloseCabWindow();
            }
            if (cabHolo && cabKind != 0)  // the Esc tag under it
            {
                var er = new Rect(DrawerPanel.xMax - 28f - 23f - 14f, y + 56f, 28f, 14f);
                CrtKit.Frame(er, 1f, CabSoft.WithAlpha(0.6f));
                Ui.Label(er, "ESC", 9, CabSoft, TextAnchor.MiddleCenter, true);
            }
        }

        static void DashLine(float x, float y, float w)
        {
            var c = CrtStyle.Ink.WithAlpha(0.25f);
            for (float d = 0f; d < w; d += 6f)
                Ui.Fill(new Rect(x + d, y, Mathf.Min(3f, w - d), 1f), c);
        }

        /// A faint lit tile on a screen (what the old translucent white pills are now).
        static void CabCard(Rect r, float a)
        {
            Ui.Fill(r, CrtStyle.Ink.WithAlpha(a * 1.6f));
            CrtKit.Frame(r, 1f, CrtStyle.Ink.WithAlpha(0.22f));
        }

        // --- Digital controls -------------------------------------------------------------------------------------------

        /// The left side of a row: its title (lit) and a small line under it. Types in with the window and retypes when changed.
        void WinLabel(Rect r, string title, string desc, bool dim = false)
        {
            int i = cabWinRow++;
            bool two = !string.IsNullOrEmpty(desc);
            Color c = dim ? CrtStyle.Dim : CrtStyle.Ink;
            CrtUi.Text(new Rect(r.x, r.y, r.width, two ? 24f : r.height), WinText("win.t" + i, title), 17, c, TextAnchor.MiddleLeft, true, FxCalm);
            if (two)
                Ui.Label(new Rect(r.x, r.y + 23f, r.width, 16f), WinText("win.d" + i, desc), 12, dim ? CrtStyle.Dim : CabSoft, TextAnchor.MiddleLeft, false);
        }

        /// A setting with an ON / OFF digital key on the right (lamp and text), a dashed line under it. Returns the new value.
        bool SwitchRow(Rect r, bool value, string title, string desc = null, string key = null)
        {
            const float kw = 86f;
            WinLabel(new Rect(r.x, r.y, r.width - kw - 14f, r.height), title, desc);
            if (!string.IsNullOrEmpty(key))
            {
                string hk = key.ToUpperInvariant();
                float tw = Ui.TextWidth(title, 17, true) - 6f, w = Ui.TextWidth(hk, 11, true) + 4f;
                var kr = new Rect(r.x + tw + 12f, r.y + (!string.IsNullOrEmpty(desc) ? 4f : r.height / 2f - 9f), w, 18f);
                CrtKit.Frame(kr, 1f, CrtStyle.Ink.WithAlpha(0.5f));
                Ui.Label(kr, hk, 11, CabSoft, TextAnchor.MiddleCenter, true);
            }
            bool click = DkKey(new Rect(r.xMax - kw, r.center.y - 17f, kw, 34f), value ? "ON" : "OFF", false, FxCalm, size: 18, accent: value ? CrtStyle.Ink : CrtStyle.Dim, lamp: true, lampLit: value);
            DashLine(r.x, r.yMax, r.width);
            return click ? !value : value;
        }

        /// Digital segment keys side by side (Off / Low / Full): the chosen one lit. Returns the new choice.
        int WinChoices(Rect r, string[] labels, int value, bool dim = false)
        {
            float gap = 4f, w = (r.width - gap * (labels.Length - 1)) / labels.Length;
            int pick = value;
            for (int i = 0; i < labels.Length; i++)
                if (DkKey(new Rect(r.x + i * (w + gap), r.y, w, r.height), labels[i].ToUpperInvariant(), i == value, FxCalm, size: 18, accent: dim ? CrtStyle.Dim : (Color?)null))
                    pick = i;
            return pick;
        }

        static int sliderDrag;  // the slider the mouse went down on (until it comes up)

        /// A blocky bar slider with - and + keys and its value (in 5% steps). Click or drag the bar. Returns the new value.
        float WinSlider(Rect r, float value)
        {
            var e = Event.current;
            var minus = new Rect(r.x, r.center.y - 16f, 34f, 32f);
            var plus = new Rect(r.xMax - 34f, r.center.y - 16f, 34f, 32f);
            var val = new Rect(plus.x - 58f, r.y, 54f, r.height);
            var bar = new Rect(minus.xMax + 10f, r.center.y - 13f, val.x - 4f - (minus.xMax + 10f), 26f);
            float v = value;
            if (DkKey(minus, "-", false, FxCalm, size: 22, accent: null))
                v = Mathf.Clamp01(Mathf.Round(value * 20f) / 20f - 0.05f);
            if (DkKey(plus, "+", false, FxCalm, size: 22, accent: null))
                v = Mathf.Clamp01(Mathf.Round(value * 20f) / 20f + 0.05f);
            int id = Mathf.RoundToInt(r.y * 7f + r.x) + 1;
            var hit = new Rect(bar.x - 4f, bar.y - 8f, bar.width + 8f, bar.height + 16f);
            Ui.NoteHover(hit);
            if (GUI.enabled && e.button == 0)
            {
                if (e.type == EventType.MouseDown && hit.Contains(e.mousePosition))
                {
                    sliderDrag = id;
                    v = Mathf.Round(Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width) * 20f) / 20f;
                    e.Use();
                }
                else if (e.type == EventType.MouseDrag && sliderDrag == id)
                {
                    v = Mathf.Round(Mathf.Clamp01((e.mousePosition.x - bar.x) / bar.width) * 20f) / 20f;
                    e.Use();
                }
            }
            if (e.type == EventType.MouseUp && sliderDrag == id)
                sliderDrag = 0;
            int blocks = bar.width >= 160f ? 16 : 10;  // (fewer on a short slider, so each block stays readable)
            float gap = 3f, bw = (bar.width - gap * (blocks - 1)) / blocks;
            int lit = Mathf.RoundToInt(v * blocks);
            Color ink = CrtStyle.Ink;
            for (int i = 0; i < blocks; i++)
            {
                var b = new Rect(bar.x + i * (bw + gap), bar.y, bw, bar.height);
                if (i < lit)
                {
                    if (!FxCalm)
                        Ui.Fill(new Rect(b.x - 1f, b.y - 1f, b.width + 2f, b.height + 2f), ink.WithAlpha(0.2f));
                    Ui.Fill(b, ink);
                }
                else
                {
                    Ui.Fill(b, ink.WithAlpha(0.14f));
                    CrtKit.Frame(b, 1f, ink.WithAlpha(0.3f));
                }
            }
            CrtUi.Text(val, Mathf.RoundToInt(v * 100f) + "%", 20, ink, TextAnchor.MiddleRight, true, FxCalm);
            return v;
        }
    }
}

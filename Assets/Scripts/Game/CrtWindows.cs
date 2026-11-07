using System;
using UnityEngine;

namespace Arena
{
    /// The drawer the in-game menus are made of (the lobby loadout's box, Docs/Mockups/pause-settings-loadout-box.html): the pause menu, the lobby's Esc menu,
    /// Settings, How to play and the profile. Pieces:
    ///   BeginDrawer / DrawerRow / EndDrawerScreen / EndDrawer   the box on the left: a title ("PAUSED_") and a line under it, then
    ///       the option keys, and (drawn by the caller, CabBackKey) a physical Back key right under the last one. Its OPENING plays
    ///       once, from the window's clock (WinT, `CabAge`): the box slides out, the title types, its sub-line types a boot log and
    ///       then the real line, a scan bar sweeps the list and the keys open one after another (KeyOpening: a bright line that
    ///       opens to the key, then a flash) with their texts typing in. After CabOpenEnd seconds nothing here animates (a list
    ///       that changes retypes its texts, CrtKit.Typed, and that is all). Reduced effects (fxReduced) or Visual effects off:
    ///       no animation.
    ///   OpenCabWindow / CloseCabWindow / DrawCabWindow          the window to the right of the box (a second loadout box). It slides
    ///       out from behind the box the FIRST time something is picked; later picks only retype its text; closing slides it back
    ///       in. BeginPanelAnim / OpenSectionPanel / EndPanelAnim are the window's frame and title (a panel drawn without
    ///       OpenCabWindow, like the pause menu's run summary, comes in after the box).
    ///   WinKey / WinChoices / WinSlider / SwitchRow / WinLabel   the digital controls the windows are filled with.
    public partial class ArenaApp
    {
        const float CabOpenEnd = 3.5f;  // seconds after opening: the opening is over, nothing animates any more
        static readonly Color CabSoft = new Color(0.55f, 0.7f, 0.97f);       // small descriptions on a screen
        static readonly Color CabDanger = new Color(1f, 0.42f, 0.48f);
        static readonly Color CabWarm = new Color(1f, 0.82f, 0.45f);         // a changed key

        bool CabCalm => fxReduced || !fxOn;
        float CabAge => WinT;  // seconds since the cabinet opened

        /// What the header's sub-line types before it settles on the real line (the pause menu adds the wave).
        string cabBootLog = "> LINK OK   > SAVE OK";

        /// For previews (Tools/Preview): typed text in a window counts as already typed.
        bool cabInstant;

        // --- Typed text -------------------------------------------------------------------------------------------------

        /// A label on the cabinet: it types in once during the opening (stateless, from `delay` seconds after it opened), and
        /// when its text changes afterwards (a list that switches) it backspaces and retypes (CrtKit.Typed).
        string CabText(string slot, string text, float delay)
        {
            float now = WinNow;
            string full = CrtKit.Typed(slot, text, now, CabCalm, -10f);  // (a slot seen for the first time counts as typed)
            if (CabCalm || CabAge >= CabOpenEnd || !CrtKit.Settled(slot, now) || string.IsNullOrEmpty(text))
                return full;
            return CrtKit.Reveal(text, CabAge - delay, 0.3f, false);
        }

        /// A label in a window: types in when the window opens and retypes when its text changes (slots are by position).
        string WinText(string slot, string text) => CrtKit.Typed(slot, text, WinNow, CabCalm, cabInstant ? -10f : 0f);

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
            bool calm = CabCalm;
            float age = CabAge, now = WinNow;
            rowIndex = 0;
            cabEdgeVis = WindowVis;  // the soft edges go over everything (DrawCabEdges)
            GUI.BeginGroup(new Rect(dx, 0f, CabX + CabW + 2f + 60f, H));
            CrtKit.LoadoutBox(new Rect(CabX, CabY, CabW + 2f, CabH + 2f));
            GUI.BeginGroup(new Rect(CabX + 1f, CabY + 1f, CabW, CabH));  // the cabinet's inside: its own origin

            // The header screen: big phosphor text and a small line under it.
            var hr = header ?? new Rect(26f, 26f, CabInW, 100f);
            bool tight = hr.height < 90f;
            float ty = tight ? 2f : 6f, th = tight ? 46f : 56f, sy = tight ? 50f : 64f;
            float ha = age - 0.5f;
            if (ha > 0f || calm)  // a thin rule under the title and its line
                Ui.Fill(new Rect(hr.x, hr.yMax + 4f, hr.width, 1f), CrtUi.T(new Color(0.12f, 0.23f, 0.4f)));
            if (calm || ha >= 0f)
            {
                int size = Mathf.Min(titleSize, 40);
                while (size > 16 && CrtUi.TitleWidth(title, size) > hr.width - 60f)
                    size--;
                string t = CabText("cab.title", title, 0.8f);
                CrtUi.Title(new Rect(hr.x + 18f, hr.y + ty, hr.width - 36f, th), t, size, CrtUi.Ink, TextAnchor.MiddleLeft, calm);
                if (calm || Mathf.FloorToInt(now * 2f) % 2 == 0)  // the blinking block after the text
                {
                    float tw = t.Length == 0 ? 0f : CrtUi.TitleWidth(t, size);
                    Ui.Fill(new Rect(hr.x + 22f + tw, hr.y + ty + th / 2f - size * 0.36f, Mathf.Max(8f, size * 0.36f), size * 0.72f), CrtUi.Ink);
                }
                string subFull = string.IsNullOrEmpty(sub) ? "" : sub.ToUpperInvariant();
                string subShown = CrtKit.Typed("cab.sub", subFull, now, calm, -10f);
                if (!calm && age < CabOpenEnd && CrtKit.Settled("cab.sub", now))
                    subShown = CrtKit.BootLine(cabBootLog, subFull, age - 0.98f, false);  // the opening: a boot log first
                Ui.Label(new Rect(hr.x + 20f, hr.y + sy, hr.width - 40f, 20f), subShown, 12, CabSoft, TextAnchor.MiddleLeft, true);
            }

            // The options screen.
            cabScreen = screen ?? new Rect(26f, 150f, CabInW, 456f);
            cabScreenAge = age - 0.9f;
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
            CrtKit.ScanBar(cabScreen, CabAge - 1.2f, CabCalm);
        }

        void EndDrawer()
        {
            EndDrawerScreen();
            GUI.EndGroup();
            GUI.EndGroup();
        }

        /// Each frame a cabinet menu was drawn, over it and under the cursor: the screen's soft edges (CrtKit.SoftEdges).
        void DrawCabEdges()
        {
            if (cabEdgeVis > 0f)
                CrtKit.SoftEdges(new Rect(0f, 0f, W, H), cabEdgeVis);
            cabEdgeVis = 0f;
        }

        /// A digital key in the cabinet's options screen: its name and a small line, inverse video under the mouse or when
        /// it's the selected one (`on`). `key` is a hint tag at the right (Esc), `tag` a plain note there. Texts type in one
        /// key after another during the opening and retype when the list switches. Returns true when clicked.
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
            string slot = "cab.r" + i;
            float d = 1.25f + i * 0.095f;
            string n = CabText(slot + ".n", name, d);
            string s = string.IsNullOrEmpty(line) ? null : CabText(slot + ".s", line, d + 0.05f);
            if (s != null && s.Length == 0)
                s = " ";  // (the two-line layout holds while the line is still typing)
            bool clicked = CrtKit.Key(r, n, s, icon, on, CabCalm, WinNow, null, r.height >= 54f ? 24 : 20, col, danger);
            bool inv = on || (GUI.enabled && r.Contains(Event.current.mousePosition));
            Color tc = CrtKit.SoftText(danger ? CabDanger : col, inv);
            if (!string.IsNullOrEmpty(key))
            {
                string hk = key.ToUpperInvariant();
                float kw = Ui.TextWidth(hk, 12, true) + 4f;
                var kr = new Rect(r.xMax - 12f - kw, r.y + 9f, kw, 20f);
                CrtKit.Frame(kr, 1f, new Color(tc.r, tc.g, tc.b, 0.8f));
                Ui.Label(kr, hk, 12, tc, TextAnchor.MiddleCenter, true);
            }
            else if (!string.IsNullOrEmpty(tag))
                Ui.Label(new Rect(r.xMax - 126f, r.y, 114f, r.height), tag, 12, inv ? CrtKit.SoftText(col, true) : (tagColor ?? CabSoft), TextAnchor.MiddleRight, true);
            CrtKit.PowerFlash(r, ka, CabCalm);
            return clicked;
        }

        /// The small hint lines at the cabinet's foot.
        void CabFoot(string a, string b = null)
        {
            var c = new Color(0.49f, 0.56f, 0.78f);
            Ui.Label(new Rect(30f, CabH - 44f, CabW - 50f, 16f), a, 11, c, TextAnchor.MiddleLeft, false);
            if (b != null)
                Ui.Label(new Rect(30f, CabH - 28f, CabW - 50f, 16f), b, 11, c, TextAnchor.MiddleLeft, false);
        }

        /// A key of the options list as the drawer opens: nothing until its turn (they come one after another down the list), then a
        /// bright line that opens to the whole key (the screens' power-on, CrtKit.PowerRect), then the key with a flash fading over it
        /// (DrawerRow). `age` is seconds since its turn. False while it isn't a whole key yet. After the opening, and with Reduced
        /// effects, every key is simply there.
        bool KeyOpening(Rect r, int i, out float age)
        {
            age = CabAge - (1.0f + i * 0.095f);
            if (CabCalm || CabAge >= CabOpenEnd)
                return true;
            int st = CrtKit.PowerState(age, false);
            if (st == 1)
                Ui.Fill(CrtKit.PowerRect(r, age), new Color(0.8f, 0.9f, 1f, 0.55f));
            return st == 2;
        }

        /// The physical Back key at `y` in the drawer, right under the options list (Settings, How to play, the profile). It comes in
        /// after the last key. True when pressed.
        bool CabBackKey(float y)
        {
            if (!cabScreenOn)
                return false;
            var r = new Rect(26f, Mathf.Min(y, CabH - 54f), CabInW, 44f);
            float ka;
            if (!KeyOpening(r, rowIndex, out ka))
                return false;
            bool pressed = CrtKit.PhysicalKey(r, "BACK");
            CrtKit.PowerFlash(r, ka, CabCalm);
            return pressed;
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
                cabWinInAt = WinNow;
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
            if (CabCalm)
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
        float CabWinSlideK()
        {
            if (!cabWin)
                return 0f;
            float inK = cabKind == 0 || CabCalm ? 1f : EaseOut(Mathf.Clamp01((WinNow - cabWinInAt) / 0.55f));
            float outK = cabWinOutAt >= 0f ? EaseIn3(Mathf.Clamp01((WinNow - cabWinOutAt) / WinCloseLen)) : 0f;
            return inK * (1f - outK) * (1f - EaseIn3(winCloseK));
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
        void BeginPanelAnim(string key)
        {
            if (!cabWin)
            {
                cabWin = true;
                cabKind = 0;
                cabWinOutAt = -1f;
                cabWinInAt = WinT < 1.3f ? winOpenAt + 1.3f : WinNow;
                CrtKit.Forget("win.");
            }
            float k = CabWinSlideK();
            float dx = -(DrawerPanel.xMax + 20f) * (1f - k);
            cabWinPower = cabKind == 0 ? WinNow - cabWinInAt : WinNow - cabWinInAt - 0.2f;
            cabWinRect = new Rect(DrawerPanel.x + dx, DrawerPanel.y, DrawerPanel.width, DrawerPanel.height);
            cabWinOn = CabCalm || cabWinPower >= 0f;  // the loadout box slides out whole, and its content with it
            if (cabWinOn)
                CrtKit.LoadoutBox(cabWinRect);
            cabWinEnabledWas = GUI.enabled;
            if (!cabWinOn || cabWinOutAt >= 0f)
                GUI.enabled = false;
            PushShift(dx, 0f, cabWinOn ? 1f : 0f);
            cabWinRow = 0;
        }

        void EndPanelAnim()
        {
            PopShift();
            GUI.enabled = cabWinEnabledWas;
        }

        /// The window's heading: its title (big, typed, with the cursor), a small line under it and a rule; an X key at the
        /// right closes the windows that came out of the cabinet.
        void OpenSectionPanel(string title, Color col, string sub, float k)
        {
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y;
            string t = WinText("win.title", title);
            CrtUi.Title(new Rect(x, y + 12f, w - 90f, 44f), t, 34, CrtUi.Ink, TextAnchor.MiddleLeft, CabCalm);
            if (CabCalm || Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0)
            {
                float tw = t.Length == 0 ? 0f : CrtUi.TitleWidth(t, 34);
                Ui.Fill(new Rect(x + 4f + tw, y + 24f, 14f, 22f), CrtUi.Ink);
            }
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(x + 2f, y + 54f, w - 4f, 34f), WinText("win.sub", sub), 13, CabSoft, TextAnchor.UpperLeft, false, true);
            DashLine(x, y + 90f, w);
            if (cabKind != 0 && WinKey(new Rect(DrawerPanel.xMax - 28f - 46f, y + 16f, 46f, 36f), "X", false, 18))
                CloseCabWindow();
        }

        static void DashLine(float x, float y, float w)
        {
            var c = new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.25f);
            for (float d = 0f; d < w; d += 6f)
                Ui.Fill(new Rect(x + d, y, Mathf.Min(3f, w - d), 1f), c);
        }

        /// A faint lit tile on a screen (what the old translucent white pills are now).
        static void CabCard(Rect r, float a)
        {
            Ui.Fill(r, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, a * 1.6f));
            CrtKit.Frame(r, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.22f));
        }

        // --- Digital controls -------------------------------------------------------------------------------------------

        /// A soft digital key in a window (CrtKit.SoftBox): a faint tint, brighter under the mouse, outlined 2 px when `sel`. A lamp
        /// before the text (lit, or dim when `lampLit` is false) makes it a switch. isStatic: a label that looks like a key.
        bool WinKey(Rect r, string text, bool sel, int size = 18, Color? accent = null, bool lamp = false, bool lampLit = true, bool isStatic = false)
        {
            bool hover = !isStatic && GUI.enabled && r.Contains(Event.current.mousePosition);
            if (!isStatic)
                Ui.NoteHover(r);
            size = CrtKit.Sz(size);
            Color c = accent ?? CrtUi.Ink;
            bool lit = (hover || sel) && !isStatic;
            if (isStatic)
            {
                Ui.Fill(r, new Color(c.r, c.g, c.b, 0.06f));
                CrtKit.Frame(r, 1f, new Color(c.r, c.g, c.b, 0.5f));
            }
            else
                CrtKit.SoftBox(r, c, sel, hover, false);
            Color tc = CrtKit.SoftText(c, lit);
            float tw = Ui.TextWidth(text, size, true) - 6f, lw = lamp ? 19f : 0f;
            float x = r.center.x - (lw + tw) / 2f;
            if (lamp)
            {
                var lr = new Rect(x, r.center.y - 4.5f, 9f, 9f);
                if (lampLit && !CabCalm)
                    Ui.Fill(new Rect(lr.x - 3f, lr.y - 3f, 15f, 15f), new Color(c.r, c.g, c.b, 0.25f));
                Ui.Fill(lr, new Color(tc.r, tc.g, tc.b, lampLit ? 1f : 0.4f));
            }
            var tr = new Rect(x + lw, r.y, tw + 14f, r.height);
            CrtUi.Text(tr, text, size, tc, TextAnchor.MiddleLeft, true, CabCalm);
            return !isStatic && Ui.HitArea(r);
        }

        /// The left side of a row: its title (lit) and a small line under it. Types in with the window and retypes when changed.
        void WinLabel(Rect r, string title, string desc, bool dim = false)
        {
            int i = cabWinRow++;
            bool two = !string.IsNullOrEmpty(desc);
            Color c = dim ? CrtUi.Dim : CrtUi.Ink;
            CrtUi.Text(new Rect(r.x, r.y, r.width, two ? 24f : r.height), WinText("win.t" + i, title), 17, c, TextAnchor.MiddleLeft, true, CabCalm);
            if (two)
                Ui.Label(new Rect(r.x, r.y + 23f, r.width, 16f), WinText("win.d" + i, desc), 12, dim ? CrtUi.Dim : CabSoft, TextAnchor.MiddleLeft, false);
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
                CrtKit.Frame(kr, 1f, new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.5f));
                Ui.Label(kr, hk, 11, CabSoft, TextAnchor.MiddleCenter, true);
            }
            bool click = WinKey(new Rect(r.xMax - kw, r.center.y - 17f, kw, 34f), value ? "ON" : "OFF", false, 18, value ? CrtUi.Ink : CrtUi.Dim, true, value);
            DashLine(r.x, r.yMax, r.width);
            return click ? !value : value;
        }

        /// Digital segment keys side by side (Off / Low / Full): the chosen one lit. Returns the new choice.
        int WinChoices(Rect r, string[] labels, int value, bool dim = false)
        {
            float gap = 4f, w = (r.width - gap * (labels.Length - 1)) / labels.Length;
            int pick = value;
            for (int i = 0; i < labels.Length; i++)
                if (WinKey(new Rect(r.x + i * (w + gap), r.y, w, r.height), labels[i].ToUpperInvariant(), i == value, 18, dim ? CrtUi.Dim : (Color?)null))
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
            if (WinKey(minus, "-", false, 22, null))
                v = Mathf.Clamp01(Mathf.Round(value * 20f) / 20f - 0.05f);
            if (WinKey(plus, "+", false, 22, null))
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
            const int blocks = 16;
            float gap = 3f, bw = (bar.width - gap * (blocks - 1)) / blocks;
            int lit = Mathf.RoundToInt(v * blocks);
            Color ink = CrtUi.Ink;
            for (int i = 0; i < blocks; i++)
            {
                var b = new Rect(bar.x + i * (bw + gap), bar.y, bw, bar.height);
                if (i < lit)
                {
                    if (!CabCalm)
                        Ui.Fill(new Rect(b.x - 1f, b.y - 1f, b.width + 2f, b.height + 2f), new Color(ink.r, ink.g, ink.b, 0.2f));
                    Ui.Fill(b, ink);
                }
                else
                {
                    Ui.Fill(b, new Color(ink.r, ink.g, ink.b, 0.14f));
                    CrtKit.Frame(b, 1f, new Color(ink.r, ink.g, ink.b, 0.3f));
                }
            }
            CrtUi.Text(val, Mathf.RoundToInt(v * 100f) + "%", 20, ink, TextAnchor.MiddleRight, true, CabCalm);
            return v;
        }
    }
}

using System;
using UnityEngine;

namespace Arena
{
    /// The main menu's Settings, on the case's bay screen (Docs/Mockups/menu-settings-bay.html): the SETTINGS key rolls the shutter up onto
    /// SETTINGS_ with four tiles (Audio, Visual effects, Gameplay, Keybinds); picking one switches the little tube off to a bright line and on
    /// again with that group's settings, all on the same screen: bar sliders with − and +, ON / OFF keys, rows of choices, key caps to rebind,
    /// the network port. ‹ BACK (or Esc) returns to the tiles; SETTINGS again (or Esc on the tiles) rolls the shutter down. Each change
    /// flashes SAVED. The values and their saving are Settings' own (MenuDrawer.cs); the lobby and pause menus keep the drawer.
    public partial class ArenaApp
    {
        bool menuSetOpen;          // Settings rolled the shutter up
        int menuSetGroup = -1;     // the group on the screen: -1 the tiles, 0 Audio, 1 Visual effects, 2 Gameplay, 3 Keybinds
        int menuSetShown = -1;     // what the screen shows (follows menuSetGroup once the tube is off)
        int menuSetSel;            // the tile the arrow keys are on
        float menuSetSwitchAt = -10f, menuSetShownAt, menuSetSavedAt = -10f, menuSetScroll;
        const float SetOffLen = 0.14f, SetOnLen = 0.18f;

        static readonly string[] SetSubs = { "5 VOLUMES · MUTE", "GLOW · SHAKE · CRT", "AIM · HUD · NETWORK", "CHANGE YOUR CONTROLS" };
        static readonly string[] SetIcons = { "pulse", "nova", "crosshair", "ui_pencil" };

        /// SETTINGS on the case: roll the shutter up onto the tiles, or back down.
        void ToggleMenuSettings()
        {
            menuSetOpen = !menuSetOpen;
            if (menuSetOpen)
            {
                menuPlayOpen = menuJoinOpen = menuWikiOpen = false;
                menuSetGroup = menuSetShown = -1;
                menuSetShownAt = Time.unscaledTime;
                menuSetScroll = 0f;
            }
            Keybinds.Listening = -1;
        }

        /// Esc on the bay's settings: back to the tiles, or the shutter down. True if it took the key.
        bool MenuSettingsEsc()
        {
            if (!menuSetOpen || Keybinds.Listening >= 0)
                return false;
            if (menuSetGroup >= 0)
                SetGo(-1);
            else
                ToggleMenuSettings();
            return true;
        }

        /// Switches the screen to group g (-1: the tiles): the tube goes off to a line, then comes back on with it.
        void SetGo(int g)
        {
            if (g == menuSetGroup)
                return;
            menuSetGroup = g;
            menuSetSwitchAt = fxOn && !fxReduced ? Time.unscaledTime : -10f;
            Keybinds.Listening = -1;
            MenuSound(Sfx.BayTick, 0.5f);
        }

        void SetSaved() => menuSetSavedAt = Time.unscaledTime;

        /// The bay's screen while Settings is up (scr: the screen, MenuCard). live: it takes clicks.
        void DrawBaySettings(Rect scr, bool live)
        {
            bool calm = fxReduced;
            float now = Time.unscaledTime, sw = now - menuSetSwitchAt;
            if (menuSetShown != menuSetGroup && sw >= SetOffLen || sw < 0f || menuSetSwitchAt < 0f && menuSetShown != menuSetGroup)
            {
                menuSetShown = menuSetGroup;
                menuSetShownAt = now;
                menuSetScroll = 0f;
            }
            bool switching = sw >= 0f && sw < SetOffLen + SetOnLen;
            CrtUi.Begin(scr);
            // The tube going off to a line and on again: the screen's insides squashed about its middle line, brightening.
            float sy = !switching ? 1f : sw < SetOffLen ? Mathf.Lerp(1f, 0.01f, EaseIn(sw / SetOffLen)) : Mathf.Lerp(0.01f, 1f, EaseOut((sw - SetOffLen) / SetOnLen));
            var old = GUI.matrix;
            if (sy < 1f)
                GUIUtility.ScaleAroundPivot(new Vector2(1f, sy), scr.center);
            bool inLive = live && !switching;
            if (menuSetShown < 0)
                DrawSetTiles(scr, inLive, calm);
            else
                DrawSetGroup(scr, menuSetShown, inLive, calm);
            GUI.matrix = old;
            if (switching)
            {
                float k = sy < 0.2f ? 1f : 1f - sy;
                Ui.RadialGlow(new Rect(scr.x - 20f, scr.center.y - 26f, scr.width + 40f, 52f), new Color(0.61f, 0.75f, 1f, 0.6f * k));
                Ui.Fill(new Rect(scr.x + 6f, scr.center.y - 1.5f, scr.width - 12f, 3f), new Color(0.81f, 0.878f, 1f, k));
            }
            float saved = now - menuSetSavedAt;
            if (saved >= 0f && saved < 0.9f)  // SAVED, flashed at the foot
            {
                float a = 1f - Seg(saved, 0.6f, 0.9f);
                var tr = new Rect(scr.center.x - 34f, scr.yMax - 50f, 68f, 18f);
                Ui.Fill(tr, CrtStyle.Ink.WithAlpha(0.9f * a));
                Ui.Label(tr, "SAVED", 10, CrtStyle.Bg.WithAlpha(a), TextAnchor.MiddleCenter, true);
            }
            CrtUi.End(scr, now, calm);
        }

        /// The header: the title typing itself in, with a blinking cursor.
        void SetHeader(Rect r, string title, bool calm)
        {
            int n = calm ? title.Length : Mathf.Min(title.Length, Mathf.FloorToInt((Time.unscaledTime - menuSetShownAt) / 0.022f));
            bool cursor = n < title.Length || !calm && Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            CrtUi.Text(r, title.Substring(0, n) + (cursor ? "_" : " "), 22, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
        }

        /// SETTINGS_ and the four group tiles (the arrow keys move between them, Enter opens one).
        void DrawSetTiles(Rect scr, bool live, bool calm)
        {
            SetHeader(new Rect(scr.x + 18f, scr.y + 12f, scr.width - 36f, 32f), "SETTINGS", calm);
            Ui.Fill(new Rect(scr.x + 12f, scr.y + 50f, scr.width - 24f, 1f), CrtStyle.Ink.WithAlpha(0.25f));
            var e = Event.current;
            if (live && e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
            {
                int mv = e.keyCode == KeyCode.RightArrow ? 1 : e.keyCode == KeyCode.LeftArrow ? -1 : e.keyCode == KeyCode.DownArrow ? 2 : e.keyCode == KeyCode.UpArrow ? -2 : 0;
                if (mv != 0)
                {
                    menuSetSel = (menuSetSel + mv + 4) % 4;
                    e.Use();
                }
                else if (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter)
                {
                    e.Use();
                    SetGo(menuSetSel);
                }
            }
            float tw = (scr.width - 24f - 8f) / 2f, th = (scr.height - 60f - 34f - 8f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(scr.x + 12f + (i % 2) * (tw + 8f), scr.y + 60f + (i / 2) * (th + 8f), tw, th);
                bool hover = live && r.Contains(e.mousePosition);
                if (hover && e.type == EventType.Repaint)
                    menuSetSel = i;
                bool sel = menuSetSel == i;
                Ui.Fill(r, CrtStyle.Ink.WithAlpha(sel ? 0.16f : 0.05f));
                EdgeRing(r, 1f, CrtStyle.Ink.WithAlpha(sel ? 0.9f : 0.45f));
                if (sel)
                {
                    Ui.RadialGlow(new Rect(r.x - 6f, r.y, 12f, r.height), CrtStyle.Ink.WithAlpha(0.5f));
                    Ui.Fill(new Rect(r.x, r.y, 3f, r.height), MachNeon);
                }
                Ui.Icon(new Rect(r.x + 12f, r.y + 12f, 28f, 28f), PixelArt.Get(SetIcons[i]), CrtStyle.Ink.WithAlpha(sel ? 1f : 0.8f));
                CrtUi.Text(new Rect(r.x + 12f, r.yMax - 46f, r.width - 20f, 22f), SettingsGroups[i].ToUpperInvariant(), 15, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(r.x + 12f, r.yMax - 24f, r.width - 20f, 14f), SetSubs[i], 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
                if (live && Ui.HitArea(r))
                    SetGo(i);
            }
            CrtUi.Text(new Rect(scr.x + 16f, scr.yMax - 24f, 200f, 14f), "PICK A GROUP", 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(scr.xMax - 216f, scr.yMax - 24f, 200f, 14f), "ESC CLOSES", 10, CrtStyle.Dim, TextAnchor.MiddleRight, false, calm);
        }

        /// A group's page: ‹ BACK and its name, then its settings in a list that scrolls inside the screen.
        void DrawSetGroup(Rect scr, int g, bool live, bool calm)
        {
            if (live && DkKey(new Rect(scr.x + 14f, scr.y + 15f, 66f, 26f), "‹ BACK", false, calm, size: 11, rawSize: true))
            {
                SetGo(-1);
                return;
            }
            if (!live)
                DkKey(new Rect(scr.x + 14f, scr.y + 15f, 66f, 26f), "‹ BACK", false, calm, size: 11, rawSize: true);
            SetHeader(new Rect(scr.x + 90f, scr.y + 12f, scr.width - 100f, 32f), SettingsGroups[g].ToUpperInvariant(), calm);
            Ui.Fill(new Rect(scr.x + 12f, scr.y + 50f, scr.width - 24f, 1f), CrtStyle.Ink.WithAlpha(0.25f));
            var view = new Rect(scr.x + 12f, scr.y + 54f, scr.width - 24f, scr.height - 54f - 30f);
            var e = Event.current;
            GUI.BeginGroup(view);
            float y = 2f - menuSetScroll, w = view.width - 10f;
            bool inView = view.Contains(e.mousePosition);
            switch (g)
            {
                case 0: y = SetAudio(y, w, live && inView, calm); break;
                case 1: y = SetFx(y, w, live && inView, calm); break;
                case 2: y = SetGameplay(y, w, live && inView, calm); break;
                default: y = SetKeys(y, w, live && inView, calm); break;
            }
            GUI.EndGroup();
            float content = y + menuSetScroll, maxScroll = Mathf.Max(0f, content - view.height);
            menuSetScroll += WheelOver(view, live) * 24f;  // MachineLook.cs
            menuSetScroll = Mathf.Clamp(menuSetScroll, 0f, maxScroll);
            if (maxScroll > 0f)
            {
                float th = view.height * view.height / content, ty = view.y + (view.height - th) * (menuSetScroll / maxScroll);
                Ui.Fill(new Rect(view.xMax - 3f, view.y, 2f, view.height), CrtStyle.Ink.WithAlpha(0.1f));
                Ui.Fill(new Rect(view.xMax - 4f, ty, 4f, th), CrtStyle.Dim);
            }
            CrtUi.Text(new Rect(scr.x + 16f, scr.yMax - 24f, 200f, 14f), SettingsGroups[g].ToUpperInvariant(), 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(scr.xMax - 216f, scr.yMax - 24f, 200f, 14f), Keybinds.Listening >= 0 ? "PRESS A KEY · ESC CANCELS" : "ESC: BACK", 10, CrtStyle.Dim, TextAnchor.MiddleRight, false, calm);
        }

        // ---- the rows (in the list's own coordinates; each returns the y under it) ----

        float SetLabel(float y, float w, string title, string line, bool off = false)
        {
            Ui.Label(new Rect(4f, y + 6f, w * 0.55f, 16f), title, 12, CrtStyle.Ink.WithAlpha(off ? 0.45f : 1f), TextAnchor.MiddleLeft, true);
            if (!string.IsNullOrEmpty(line))
                Ui.Label(new Rect(4f, y + 21f, w * 0.6f, 14f), line, 9, CrtStyle.Dim, TextAnchor.MiddleLeft);
            return y;
        }

        static float SetRule(float y, float w)
        {
            Ui.Fill(new Rect(0f, y, w, 1f), CrtStyle.Ink.WithAlpha(0.14f));
            return y + 1f;
        }

        /// A bar of 20 cells (the last four amber when lit), − and + either side and the value: a volume 0 to 1.
        float SetBar(float y, float w, string title, string line, ref float v, bool live, bool calm, float min = 0f)
        {
            SetLabel(y, w, title, line);
            float cellsW = 20f * 6f + 19f * 2f, x1 = w - 30f, x0 = x1 - 22f - cellsW - 22f;
            var minus = new Rect(x0, y + 11f, 18f, 18f);
            var cells = new Rect(x0 + 22f, y + 12f, cellsW, 16f);
            var plus = new Rect(cells.xMax + 4f, y + 11f, 18f, 18f);
            float before = v;
            if (SetStep(minus, false, live))
                v = Mathf.Max(min, Mathf.Round(v * 20f - 1f) / 20f);
            if (SetStep(plus, true, live))
                v = Mathf.Min(1f, Mathf.Round(v * 20f + 1f) / 20f);
            int lit = Mathf.RoundToInt(v * 20f);
            for (int i = 0; i < 20; i++)
                Ui.Fill(new Rect(cells.x + i * 8f, cells.y, 6f, cells.height), i < lit ? (i >= 16 ? DriveAmber : CrtStyle.Ink) : CrtStyle.Ink.WithAlpha(0.14f));
            var ev = Event.current;
            if (live && (ev.type == EventType.MouseDown || ev.type == EventType.MouseDrag) && ev.button == 0 && new Rect(cells.x - 2f, cells.y - 4f, cells.width + 4f, cells.height + 8f).Contains(ev.mousePosition))
            {
                v = Mathf.Max(min, Mathf.Round(Mathf.Clamp01((ev.mousePosition.x - cells.x) / cells.width) * 20f) / 20f);
                ev.Use();
            }
            Ui.Label(new Rect(x1, y + 10f, 30f, 20f), Mathf.RoundToInt(v * 100f).ToString(), 11, CrtStyle.Ink, TextAnchor.MiddleRight, true);
            if (Mathf.Abs(v - before) > 0.0001f)
                SetSaved();
            return SetRule(y + 40f, w);
        }

        /// A small − or + key beside a bar.
        static bool SetStep(Rect r, bool plus, bool live)
        {
            bool hover = live && r.Contains(Event.current.mousePosition);
            Ui.Fill(r, CrtStyle.Ink.WithAlpha(hover ? 0.22f : 0.06f));
            EdgeRing(r, 1f, CrtStyle.Ink.WithAlpha(hover ? 1f : 0.55f));
            Ui.Fill(new Rect(r.center.x - 4f, r.center.y - 1f, 8f, 2f), CrtStyle.Ink);
            if (plus)
                Ui.Fill(new Rect(r.center.x - 1f, r.center.y - 4f, 2f, 8f), CrtStyle.Ink);
            return live && Ui.HitArea(r);
        }

        /// An ON / OFF key.
        bool SetToggle(ref float y, float w, string title, string line, bool on, bool live, bool calm, string keyHint = null)
        {
            SetLabel(y, w, title + (keyHint != null ? "  [" + keyHint + "]" : ""), line);
            bool to = on;
            if (DkKey(new Rect(w - 56f, y + 8f, 56f, 24f), on ? "ON" : "OFF", on, calm, size: 11, rawSize: true) && live)
            {
                to = !on;
                SetSaved();
            }
            y = SetRule(y + 40f, w);
            return to;
        }

        /// A row of choices under the setting's name (they take the whole width).
        int SetChoices(ref float y, float w, string title, string line, string[] names, int pick, bool live, bool calm, bool off = false)
        {
            SetLabel(y, w, title, line, off);
            float gap = 3f, cw = (w - gap * (names.Length - 1)) / names.Length;
            int to = pick;
            for (int i = 0; i < names.Length; i++)
                if (DkKey(new Rect(i * (cw + gap), y + 38f, cw, 22f), names[i].ToUpperInvariant(), i == pick, calm, size: 9, rawSize: true, accent: off ? CrtStyle.Dim : (Color?)null) && live && i != pick)
                {
                    to = i;
                    SetSaved();
                }
            y = SetRule(y + 68f, w);
            return to;
        }

        float SetAudio(float y, float w, bool live, bool calm)
        {
            float m = sounds.Master, mu = sounds.Music, wp = sounds.Weapons, en = sounds.Enemies, ef = sounds.Effects;
            y = SetBar(y, w, "Master", "Everything", ref m, live, calm);
            y = SetBar(y, w, "Music", "Menu, battle and boss tracks", ref mu, live, calm);
            y = SetBar(y, w, "Your weapons", "Your shots and hits", ref wp, live, calm);
            y = SetBar(y, w, "Enemies", "Their shots, the bosses", ref en, live, calm);
            y = SetBar(y, w, "Effects", "Explosions, abilities, the menus", ref ef, live, calm);
            bool mute = SetToggle(ref y, w, "Mute all sound", "The volumes are kept", sounds.Muted, live, calm, Keybinds.NameOrNull(KeyAction.Mute));
            if (m != sounds.Master || mu != sounds.Music || wp != sounds.Weapons || en != sounds.Enemies || ef != sounds.Effects || mute != sounds.Muted)
            {
                sounds.Master = m; sounds.Music = mu; sounds.Weapons = wp; sounds.Enemies = en; sounds.Effects = ef; sounds.Muted = mute;
                SaveAudioSettings();
            }
            return y;
        }

        float SetFx(float y, float w, bool live, bool calm)
        {
            bool changed = false;
            bool on = SetToggle(ref y, w, "Visual effects", "Glow, sparks, shattering enemies, lighting", fxOn, live, calm);
            if (on != fxOn) { fxOn = on; changed = true; }
            bool lit = SetToggle(ref y, w, "Dynamic lighting", "Shots, blasts and fire cast shadows", lightsOn, live, calm);
            if (lit != lightsOn) { lightsOn = lit; changed = true; }
            int edge = SetChoices(ref y, w, "Shadow edges", "Hard lines, or soft and blurred", new[] { "Hard", "Soft" }, lightsSoft ? 1 : 0, live, calm, !fxOn || !lightsOn);
            if ((edge == 1) != lightsSoft) { lightsSoft = edge == 1; changed = true; }
            int dim = SetChoices(ref y, w, "Floor dim", "The floor darker, so the lights stand out", FloorDimNames, floorDim, live, calm, !fxOn || !lightsOn);
            if (dim != floorDim) { floorDim = dim; changed = true; }
            int shake = SetChoices(ref y, w, "Screen shake", "How hard hits rattle the view", new[] { "Off", "Low", "Full" }, fxShake, live, calm, !fxOn);
            if (shake != fxShake) { fxShake = shake; changed = true; }
            bool nums = SetToggle(ref y, w, "Damage numbers", "Small numbers rising off what's hit", fxNumbers, live, calm);
            if (nums != fxNumbers) { fxNumbers = nums; changed = true; }
            bool names = SetToggle(ref y, w, "Ability names", "Each ability's name over whoever casts it", fxCallouts, live, calm);
            if (names != fxCallouts) { fxCallouts = names; changed = true; }
            bool red = SetToggle(ref y, w, "Reduced effects", "No screen shake, hit-pause or white flashes", fxReduced, live, calm);
            if (red != fxReduced) { fxReduced = red; changed = true; }
            int style = SetChoices(ref y, w, "Arena floor", "The floor's pattern", FloorStyleNames, floorStyle, live, calm, !fxOn);
            if (style != floorStyle) { floorStyle = style; changed = true; }
            bool bend = SetToggle(ref y, w, "Floor bending", "The grid dips under heavy hits", floorBend, live, calm);
            if (bend != floorBend) { floorBend = bend; changed = true; }
            int theme = SetChoices(ref y, w, "CRT colour", "The menus' phosphor", CrtUi.ThemeNames, CrtUi.Theme, live, calm);
            if (theme != CrtUi.Theme) { CrtUi.Theme = theme; changed = true; }
            if (changed)
                SaveFxSettings();
            return y;
        }

        float SetGameplay(float y, float w, bool live, bool calm)
        {
            bool l2 = SetToggle(ref y, w, "Aim line", "A line toward the cursor, as far as you reach", aimLine, live, calm, Keybinds.KeyName(Keybinds.Get(KeyAction.AimLine)));
            if (l2 != aimLine)
                SetAimLine(l2);
            int hud = SetChoices(ref y, w, "HUD", "Where it sits; H cycles it", HudModeNames, hudMode, live, calm);
            if (hud != hudMode)
                SetHudMode(hud);
            bool lean = SetToggle(ref y, w, "Camera lean", "The view eases toward where you aim", camLean, live, calm);
            if (lean != camLean)
                SetCamLean(lean);
            bool zoom = SetToggle(ref y, w, "Scroll-wheel zoom", "Scroll up to zoom in on yourself", scrollZoom, live, calm);
            if (zoom != scrollZoom)
                SetCamZoom(zoom);
            bool hover = SetToggle(ref y, w, "Enemy names on hover", "Point at an enemy for its name and job", hoverNames, live, calm);
            if (hover != hoverNames)
                SetHoverNames(hover);
            float mate = mateShotAlpha;
            y = SetBar(y, w, "Teammate shots", "How solid other players' bullets look", ref mate, live, calm, 0.25f);
            if (Mathf.Abs(mate - mateShotAlpha) > 0.0001f)
                SetMateShotAlpha(mate);
            float sens = FpSensSetting;
            y = SetBar(y, w, "First-person mouse", "How fast the mouse turns you in the F8 view", ref sens, live, calm, 0.05f);
            if (Mathf.Abs(sens - FpSensSetting) > 0.0001f)
                SetFpSens(sens);
            SetLabel(y, w, "Network port", "Hosting and joining (" + Session.DefaultPort + " unless changed)");
            var pr = new Rect(w - 80f, y + 8f, 80f, 26f);
            Ui.Fill(pr, CrtStyle.Ink.WithAlpha(0.08f));
            EdgeRing(pr, 1f, CrtStyle.Ink.WithAlpha(0.6f));
            if (live)
            {
                var oldTint = GUI.contentColor;
                GUI.contentColor = CrtStyle.Ink;
                string port = Ui.BareField(new Rect(pr.x + 6f, pr.y, pr.width - 12f, pr.height), portText, 5, 15);
                GUI.contentColor = oldTint;
                if (port != portText)
                {
                    portText = port;
                    SaveMenuFields();
                    SetSaved();
                }
            }
            else
                Ui.Label(pr, portText, 15, CrtStyle.Ink, TextAnchor.MiddleCenter, true);
            return SetRule(y + 42f, w);
        }

        float SetKeys(float y, float w, bool live, bool calm)
        {
            for (int g = 0; g < Keybinds.GroupNames.Length; g++)
            {
                Ui.Label(new Rect(4f, y + 4f, w, 14f), Keybinds.GroupNames[g], 10, CrtStyle.Dim, TextAnchor.MiddleLeft, true);
                y += 20f;
                foreach (var a in Keybinds.Groups[g])
                {
                    Ui.Label(new Rect(4f, y + 4f, w - 210f, 22f), Keybinds.Title(a), 12, CrtStyle.Ink, TextAnchor.MiddleLeft, true);
                    for (int slot = 1; slot >= 0; slot--)
                    {
                        var k = Keybinds.Get(a, slot);
                        if (slot == 1 && k == KeyCode.None && !(Keybinds.Listening == (int)a && Keybinds.ListeningSlot == 1))
                            continue;
                        bool listening = Keybinds.Listening == (int)a && Keybinds.ListeningSlot == slot;
                        string label = listening ? (Mathf.FloorToInt(Time.unscaledTime * 4f) % 2 == 0 ? "…" : " ") : k == KeyCode.None ? "—" : Keybinds.KeyName(k);
                        var r = new Rect(w - 114f - (slot == 1 ? 88f : 0f), y + 3f, 84f, 24f);
                        if (DkKey(r, label, listening, calm, size: 10, rawSize: true, accent: listening ? DriveAmber : (Color?)null) && live)
                        {
                            Keybinds.Listening = listening ? -1 : (int)a;
                            Keybinds.ListeningSlot = slot;
                        }
                    }
                    // The X: no key at all for this action (e.g. so Reroll can't be pressed by mistake).
                    if (Keybinds.Bound(a) && DkKey(new Rect(w - 24f, y + 3f, 24f, 24f), "×", false, calm, size: 12, rawSize: true, accent: CrtStyle.Danger) && live)
                    {
                        if (Keybinds.Listening == (int)a)
                            Keybinds.Listening = -1;
                        Keybinds.Unbind(a);
                        SetSaved();
                    }
                    y = SetRule(y + 30f, w);
                }
                y += 6f;
            }
            if (DkKey(new Rect(0f, y + 4f, w, 28f), "RESET ALL KEYS", false, calm, size: 11, rawSize: true) && live)
            {
                Keybinds.Listening = -1;
                Keybinds.ResetAll();
                SetSaved();
            }
            return y + 40f;
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The main menu, How to play, and the pause menu and audio settings.
    public partial class ArenaApp
    {
        // The main menu ("Big play", Docs/Mockups/menu-d.html, player card below: Docs/Mockups/menu-card-bottom.html): the title on its
        // x-axis with a shot arcing over it; in a box (MenuCard, which grows into the lobby's loadout, LobbyEnter.cs) one big Play
        // button over Join game, Records and Settings, and your player card under the box (DrawMenuPlayerCard, MenuPlayerRect). The tip
        // box (DrawMenuTipBox, MenuTip, HowToTips) is parked: nothing draws it for now. Play swaps the box's insides for the saved-runs
        // picker (DrawPlayPicker, SavesUi.cs): continue your latest run or start a new one. Join opens the address row
        // under the box. Quit is a faint power icon at the bottom right of the screen. The port is in Settings (Gameplay).
        // The first time the menu shows, it draws itself in (MenuIntro.cs); each piece asks MenuIntroK how far in it is.
        const float MenuTitleY = 126f, MenuTitleSize = 74f, MenuAxisY = 188f, MenuAxisHalf = 390f;
        const float ArcX0 = 250f, ArcX1 = 1030f, ArcTop = 24f, ArcBase = 162f;  // the shot's arc over the title
        static readonly Rect MenuCard = new Rect(W / 2f - 340f, 268f, 680f, 320f);  // taller than the picker needs (the saved-runs list gets the extra)
        static readonly Rect MenuPlayerRect = new Rect(W / 2f - 310f, MenuCard.yMax + 22f, 620f, 64f);  // your player card, under the box
        static readonly Rect MenuHostRect = new Rect(MenuCard.x + 30f, MenuCard.y + 40f, 620f, 156f);  // the Play button
        static readonly Rect MenuJoinRect = new Rect(MenuCard.x + 30f, MenuCard.y + 212f, 194f, 64f);
        static readonly Rect MenuRecordsRect = new Rect(MenuCard.x + 238f, MenuCard.y + 212f, 194f, 64f);
        static readonly Rect MenuSettingsRect = new Rect(MenuCard.x + 446f, MenuCard.y + 212f, 194f, 64f);
        static readonly Rect MenuJoinRow = new Rect(W / 2f - 340f, MenuCard.yMax + 12f, 680f, 64f);
        static readonly Rect MenuQuitRect = new Rect(W - 72f, H - 66f, 48f, 48f);  // bottom right
        bool menuJoinOpen;    // Join opened the address row
        bool menuPlayOpen;    // Play opened the saved-runs picker in the box (SavesUi.cs)
        float menuPlayK;      // 0 = the menu's buttons, 1 = the picker; in between a scan line is sweeping across the box
        const float MenuPlayLen = 0.85f;  // seconds for that sweep, in either direction
        static readonly Color ScanColor = new Color(0.44f, 0.89f, 0.77f);

        void DrawMenu()
        {
            float t = Time.unscaledTime;
            DrawMenuIntroBackdrop();  // the background fading up as the intro starts (MenuIntro.cs)
            float box = MenuIntroK(1.65f, 2.0f);
            if (box > 0f)  // the card's glow first, so it sits under the title, the tip box and everything else
                TileTransformed(MenuCard.center, Mathf.Lerp(0.3f, 1f, EaseBack(box)), 0f, () => WithAlpha(Mathf.Clamp01(box * 2.5f), () => Ui.BodyGlow(MenuCard)));
            DrawMenuTitle(t);
            if (box > 0f)
                TileTransformed(MenuCard.center, Mathf.Lerp(0.3f, 1f, EaseBack(box)), 0f, () => WithAlpha(Mathf.Clamp01(box * 2.5f), () => Ui.BodyBox(MenuCard)));
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, MenuAxisY), MenuAxisHalf * EaseOut(MenuAxisK), t);  // the title sits on an x-axis
                DrawMenuArc(t, 1f);
                float snap = MenuIntroK(1.85f, 2.0f), off = 18f * (1f - EaseOut(snap));
                if (snap > 0f)
                    DrawPlotCorners(new Rect(MenuCard.x - off, MenuCard.y - off, MenuCard.width + 2f * off, MenuCard.height + 2f * off), 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f * snap));
                DrawMenuIntroPen(t);  // the pen writing the title and tracing the arc (MenuIntro.cs)
                EndUiGl();
            }
            bool live = !MenuIntroPlaying;  // nothing takes a click until it has all landed
            DrawMenuCardInside(t, live);
            DrawMenuSecondary(t, live);
            float cardK = MenuIntroK(2.2f, 2.55f);
            if (cardK > 0f)
                DrawMenuPlayerCard(t, 1f, cardK, live);
            DrawMenuIntroFade();  // the short version of the intro: a fade (MenuIntro.cs)
        }

        /// The title, drawn in ink then filled during the intro.
        void DrawMenuTitle(float t, float alpha = 1f)
        {
            var r = new Rect(0f, MenuTitleY - MenuTitleSize * 0.6f, W, MenuTitleSize * 1.2f);
            DrawMenuTitleText(r);
        }

        const float MenuTipCycle = 8f, MenuTipGap = 22f, MenuTipH = 92f;
        static readonly Rect MenuTipRect = new Rect(W / 2f - 340f, MenuCard.yMax + MenuTipGap, 680f, MenuTipH);
        int menuTipIdx;               // the tip showing (HowToTips row)
        float menuTipAt = -10f;       // when it came up (the next one follows after MenuTipCycle, or at once on Prev / Next)
        float menuTipHide;            // 0 to 1: the box fades out while the Join address row is open (they share the spot)

        /// The tip box's current tip, stepping to the next one every MenuTipCycle seconds. `age` is how long this one has been up.
        void MenuTip(float t, out string topic, out string body, out float age)
        {
            int n = HowToTips.GetLength(0);
            if (t - menuTipAt >= MenuTipCycle)
            {
                menuTipIdx = (menuTipIdx + 1) % n;
                menuTipAt = t;
            }
            age = t - menuTipAt;
            topic = HowToTips[menuTipIdx, 0];
            body = HowToTips[menuTipIdx, 1];
        }

        /// How far the tip box is showing (0 to 1): the menu's intro brings it in, and the Join row's address row takes its spot.
        float MenuTipShow(float intro)
        {
            menuTipHide = Mathf.MoveTowards(menuTipHide, menuJoinOpen ? 1f : 0f, Time.unscaledDeltaTime / 0.25f);
            return intro * (1f - EaseOut(menuTipHide));
        }

        /// The tip box (Docs/Mockups/menu-tip-box.html, C with D's corner outlines): its own box under the menu card, a drawing for the
        /// tip on the left, TIP and the topic in bold over the text, and Prev / Next with the count on the right. The tip changes with
        /// a quick fade. `intro` is how far the menu's intro has brought it in (1 = in place); `live` lets the buttons take clicks.
        /// GUI part; DrawMenuTipGl draws the drawing and the corners over it.
        void DrawMenuTipBox(float t, float alpha, float intro, bool live)
        {
            float show = MenuTipShow(intro);
            MenuTip(t, out string topic, out string body, out float age);
            float a = alpha * show;
            if (a <= 0f)
                return;
            var r = MenuTipRect;
            r.y += 12f * (1f - show);  // rises into place
            float ca = Mathf.Clamp01(age / 0.25f);  // the new tip fades in
            var blue = new Color(0.43f, 0.65f, 1f);
            int n = HowToTips.GetLength(0);
            bool prev = false, next = false;
            WithAlpha(a, () =>
            {
                Ui.BodyBox(r);
                // The tip sits on a green CRT screen inset in the box (Docs/Mockups/crt-tip-box.html, B): picture tile, TIP, topic, text,
                // and the count with Prev / Next, all on the screen. DrawMenuTipGl draws the picture over the tile.
                var scr = new Rect(r.x + 10f, r.y + 10f, r.width - 20f, r.height - 20f);
                bool calm = fxReduced;
                CrtUi.Begin(scr);
                CrtUi.Tile(new Rect(r.x + 22f, r.center.y - 26f, 52f, 52f));
                float tx = r.x + 92f, tw = r.width - 92f - 196f;
                var ink = CrtUi.Ink;
                var dim = CrtUi.Dim;
                CrtUi.Text(new Rect(tx, r.y + 15f, 32f, 20f), "TIP", 11, new Color(dim.r, dim.g, dim.b, ca), TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(tx + 32f, r.y + 15f, tw - 32f, 20f), topic, 17, new Color(ink.r, ink.g, ink.b, ca), TextAnchor.MiddleLeft, true, calm);
                CrtUi.Text(new Rect(tx, r.y + 34f, tw, r.height - 40f), body, 13, new Color(dim.r + 0.1f, dim.g + 0.25f, dim.b + 0.2f, 0.95f * ca), TextAnchor.UpperLeft, false, calm, true);
                float bx = r.xMax - 20f - 168f;
                CrtUi.Text(new Rect(bx, r.y + 17f, 168f, 18f), (menuTipIdx + 1) + " / " + n, 11, dim, TextAnchor.MiddleRight, true, calm);
                prev = CrtUi.Small(new Rect(bx, r.y + 42f, 80f, 30f), "< PREV", null, 14, calm);
                next = CrtUi.Small(new Rect(bx + 88f, r.y + 42f, 80f, 30f), "NEXT >", null, 14, calm);
                CrtUi.End(scr, Time.unscaledTime, calm);
            });
            if (live && show >= 1f && (prev || next))
            {
                menuTipIdx = (menuTipIdx + (next ? 1 : n - 1)) % n;
                menuTipAt = t;
            }
        }

        /// GL (inside BeginUiGl): the tip's drawing in its tile, and the corner outlines around the box (the same ones as the menu card's).
        void DrawMenuTipGl(float t, float alpha, float intro)
        {
            float show = MenuTipShow(intro) * alpha;
            if (show <= 0f)
                return;
            var r = MenuTipRect;
            r.y += 12f * (1f - show);
            UiModelAt(V2.Zero, 1f);
            DrawTipGlyph(menuTipIdx, new V2(r.x + 48f, r.center.y), t);
            DrawPlotCorners(r, 5f, 14f, new Rgba(0.45f, 0.65f, 1f, 0.7f * show));
        }

        /// GL (inside BeginUiGl): the shot's dashed arc over the title, the shot flying along it, and its label.
        void DrawMenuArc(float t, float alpha)
        {
            UiModelAt(V2.Zero, 1f);
            float upTo = MenuIntroK(1.4f, 1.9f);
            if (upTo <= 0f || alpha <= 0f)
                return;
            var c = new Rgba(0.45f, 0.65f, 1f, 0.35f * alpha);
            const int Segs = 60;
            int n = Mathf.CeilToInt(Segs * upTo);
            for (int i = 0; i < n; i += 2)  // dashes: every other step
                Line(ArcAt(Mathf.Min(upTo, (float)i / Segs)), ArcAt(Mathf.Min(upTo, (float)(i + 1) / Segs)), 1.5f, c);
            float shotIn = MenuIntroK(1.9f, 2.2f) * alpha;
            if (shotIn > 0f)
            {
                float u = MathX.Fmod(t * 0.22f, 1f);
                for (int i = 6; i >= 0; i--)  // the shot and its fading tail
                {
                    float uu = Mathf.Max(0f, u - i * 0.008f);
                    Circle(ArcAt(uu), 5f - i * 0.5f, new Rgba(1f, 0.47f, 0.59f, (0.12f + 0.5f * (1f - i / 6f)) * shotIn), 10);
                }
            }
        }

        static V2 ArcAt(float u) => new V2(Mathf.Lerp(ArcX0, ArcX1, u), ArcBase - (ArcBase - ArcTop) * 4f * u * (1f - u));

        /// The box's insides: your name card, Play, Join game, Records and Settings, or (after Play) the saved-runs picker,
        /// then the address row Join opens and how joining is going.
        /// live = false draws them only (Repaint), for the intro and the move into the lobby (LobbyEnter.cs).
        void DrawMenuCardInside(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float name = MenuIntroK(1.75f, 2.1f), host = MenuIntroK(1.8f, 2.2f), join = MenuIntroK(2.0f, 2.3f), cont = MenuIntroK(2.1f, 2.4f);
            if (live && Event.current.type == EventType.Repaint)
            {
                float target = menuPlayOpen ? 1f : 0f;
                menuPlayK = fxOn && !fxReduced ? Mathf.MoveTowards(menuPlayK, target, Time.unscaledDeltaTime / MenuPlayLen) : target;
            }
            bool sweeping = menuPlayK != (menuPlayOpen ? 1f : 0f);  // also true on the first frame of a move, so no stray click lands
            if (sweeping)
            {
                // The picker shows left of the scan line and the menu's buttons right of it; neither takes a click until it lands.
                float x = MenuCard.x - 10f + EaseInOut(menuPlayK) * (MenuCard.width + 20f);
                GUI.BeginGroup(new Rect(0f, 0f, x, H));
                DrawPlayPicker(t, false);
                GUI.EndGroup();
                GUI.BeginGroup(new Rect(x, 0f, W - x, H));
                GUI.BeginGroup(new Rect(-x, 0f, W, H));
                menuGlClipX = x;
                DrawMenuButtons(t, false, name, host, join, cont);
                menuGlClipX = -1f;
                GUI.EndGroup();
                GUI.EndGroup();
                float edge = Mathf.Sin(menuPlayK * Mathf.PI);
                for (int g = 3; g >= 0; g--)  // the line, with a soft glow either side
                    Ui.Fill(new Rect(x - 1.5f - g * 5f, MenuCard.y + 2f, 3f + g * 10f, MenuCard.height - 4f), new Color(ScanColor.r, ScanColor.g, ScanColor.b, (g == 0 ? 0.95f : 0.07f) * edge));
            }
            else if (menuPlayOpen)
                DrawPlayPicker(t, live);
            else
                DrawMenuButtons(t, live, name, host, join, cont);
            // Join's address row (or, while connecting, how it's going and Cancel), then any message from the last try.
            var row = MenuJoinRow;
            if (session.Connecting)
            {
                // How it's going, on a CRT screen of its own, with Cancel as a key beside it.
                Ui.Panel(row, false);
                var ss = new Rect(row.x + 14f, row.y + 10f, row.width - 14f - 170f, 44f);
                CrtUi.Begin(ss);
                CrtUi.Text(new Rect(ss.x + 14f, ss.y, ss.width - 28f, ss.height), session.Status, 14, CrtUi.Ink, TextAnchor.MiddleLeft, false, fxReduced);
                CrtUi.End(ss, Time.unscaledTime, fxReduced);
                var cr = new Rect(row.xMax - 150f, row.y + 12f, 136f, 40f);
                if (MenuVhs.Draw(cr, "TRK 02", live, fxReduced, () => Ui.ActionButton(cr, "Cancel", false, Ui.Danger, PixelArt.Get("ui_back"), new Color(1f, 0.6f, 0.64f), 15)) && live)
                    session.Leave("");
            }
            else if (menuJoinOpen && join >= 1f && !menuPlayOpen)
            {
                // The address on a CRT screen (label above it), and Connect as a key with a lamp beside it.
                Ui.Panel(row, false);
                var ss = new Rect(row.x + 14f, row.y + 10f, row.width - 14f - 170f, 44f);
                CrtUi.Begin(ss);
                CrtUi.Text(new Rect(ss.x + 14f, ss.y + 3f, 200f, 12f), "HOST ADDRESS", 9, CrtUi.Dim, TextAnchor.MiddleLeft, false, true);
                var oldTint = GUI.contentColor;
                GUI.contentColor = CrtUi.Ink;
                addressText = Ui.BareField(new Rect(ss.x + 10f, ss.y + 15f, ss.width - 20f, 28f), addressText, 64, 19);
                GUI.contentColor = oldTint;
                CrtUi.End(ss, Time.unscaledTime, fxReduced);
                var kr = new Rect(row.xMax - 150f, row.y + 11f, 136f, 42f);
                if (MenuVhs.Draw(kr, "TRK 01", live, fxReduced, () =>
                {
                    bool hit = Ui.ActionButton(kr, "Connect", true, Ui.Primary, PixelArt.Get("ui_arrow"), null, 16);
                    Ui.Fill(new Rect(kr.xMax - 14f, kr.center.y - 3f, 6f, 6f), CrtUi.Ink);   // the lamp
                    return hit;
                }) && live)
                    JoinGame();
            }
            if (session.Status.Length > 0 && !session.Connecting && live)
                Ui.Label(new Rect(row.x - 40f, (menuJoinOpen ? row.yMax : row.y) + 6f, row.width + 80f, 40f), session.Status, 13, Ui.Warn, TextAnchor.UpperCenter, false, true);
        }

        /// Your player card under the box, where the tip box used to be (Docs/Mockups/menu-card-bottom.html). It fades out while Join's
        /// address row is open, since the two share the spot (the same fade the tip box had), and rises in with the intro.
        void DrawMenuPlayerCard(float t, float alpha, float intro, bool live)
        {
            float show = MenuTipShow(intro);
            float a = alpha * show;
            if (a <= 0f)
                return;
            var r = MenuPlayerRect;
            r.y += 12f * (1f - show);  // rises into place
            WithAlpha(a, () => DrawNameCard(r, 0f, live && show >= 1f, t));
        }

        /// The box's buttons: Play, Join game, Records and Settings (each rising in during the intro).
        void DrawMenuButtons(float t, bool live, float name, float host, float join, float cont)
        {
            if (host > 0f)
                MenuRise(host, 60f, () =>
                {
                    Ui.PulseRing(KeyUi.Well(MenuHostRect), Ui.Primary, t);
                    if (MenuVhs.Draw(MenuHostRect, "PLAY", live, fxReduced, () => KeyUi.Key(MenuHostRect, "PLAY", PixelArt.Get("ui_play"), 34)) && live)
                        OpenPlayPicker();
                });
            if (join > 0f)
                MenuRise(join, 24f, () =>
                {
                    if (MenuVhs.Draw(MenuJoinRect, "TRK 02", live, fxReduced, () => KeyUi.Key(MenuJoinRect, menuJoinOpen ? "Join  ▲" : "Join game", PixelArt.Get("ui_arrow"), 20, menuJoinOpen)) && live)
                        menuJoinOpen = !menuJoinOpen;
                });
            if (cont > 0f)
                MenuRise(cont, 24f, () =>
                {
                    if (MenuVhs.Draw(MenuRecordsRect, "TRK 03", live, fxReduced, () => KeyUi.Key(MenuRecordsRect, "Records", PixelArt.Get("ui_trophy"), 20)) && live)
                        recordsOpen = true;
                    if (MenuVhs.Draw(MenuSettingsRect, "TRK 04", live, fxReduced, () => KeyUi.Key(MenuSettingsRect, "Settings", PixelArt.Get("ui_gear"), 20)) && live)
                        settingsOpen = true;
                });
        }

        void JoinGame()
        {
            SaveMenuFields();
            session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice);
        }

        /// A piece of the menu rising into place as the intro brings it in (k 0 to 1; 1 = in place).
        void MenuRise(float k, float from, Action draw)
        {
            if (k >= 1f)
            {
                draw();
                return;
            }
            GUI.BeginGroup(new Rect(0f, from * (1f - EaseBack(k)), W, H));
            WithAlpha(Mathf.Clamp01(k * 2f), draw);
            GUI.EndGroup();
        }

        /// What's left outside the box: the "Switch profile" tab under your player card (only while the mouse is over the
        /// card or the tab), the faint Quit icon at the bottom centre, and the tooltip. live = false draws it only (Repaint),
        /// for the intro and the move into the lobby.
        float switchProfA;  // 0 to 1: how visible the "Switch profile" button is

        void DrawMenuSecondary(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float k = MenuIntroK(2.1f, 2.5f);
            if (k <= 0f)
                return;
            GUI.BeginGroup(new Rect(0f, 100f * (1f - EaseOut(k)), W, H));
            if (live)
                TipClear();  // the rank badge's hover card (RankUi.cs)
            if (live && ProfileStore.Root != null && !menuPlayOpen)
            {
                var sw = new Rect(MenuPlayerRect.center.x - 70f, MenuPlayerRect.yMax + 6f, 140f, 24f);  // under the card
                bool near = MenuPlayerRect.Contains(Event.current.mousePosition) || sw.Contains(Event.current.mousePosition);
                if (Event.current.type == EventType.Repaint)
                    switchProfA = Mathf.MoveTowards(switchProfA, near ? 1f : 0f, Time.unscaledDeltaTime * 5f);
                if (switchProfA > 0.01f)
                {
                    bool clicked = false;
                    WithAlpha(switchProfA, () => clicked = Ui.ActionButton(sw, "Switch profile", false, Ui.Primary, null, null, 14));
                    if (clicked && switchProfA > 0.5f)
                        StartProfTrOut();  // the move back to the picker (ProfileTransition.cs)
                }
            }
            // Quit: a see-through power icon that lights up red under the mouse.
            var q = MenuQuitRect;
            bool hot = live && q.Contains(Event.current.mousePosition);
            Ui.Icon(new Rect(q.center.x - 16f, q.center.y - 16f, 32f, 32f), PixelArt.Get("ui_power"), hot ? new Color(1f, 0.45f, 0.5f, 1f) : new Color(0.8f, 0.85f, 1f, 0.6f));
            if (hot)
                Ui.Label(new Rect(q.x - 30f, q.y - 14f, q.width + 60f, 14f), "Quit", 12, new Color(1f, 0.6f, 0.65f), TextAnchor.MiddleCenter, true);
            if (live && Ui.HitArea(q))
                Application.Quit();
            if (live)
                DrawTip();
            GUI.EndGroup();
        }

        /// Your name card, sized for the top row of the menu box (k = 0): a neutral player figure (no role or class: that's chosen in the
        /// lobby), your name, your rank title and badge, and the XP towards the next rank. Click the name to
        /// rename; click anywhere else on it to open your cosmetics. (k between 0 and 1 is the move into the lobby: it lays
        /// out toward a full party card, with the class you've picked.)
        void DrawNameCard(Rect r, float k, bool editable, float t)
        {
            bool plain = k < 0.01f;  // the menu's chip: no role or class shown
            var color = plain ? Ui.Accent : ToColor(Defs.Get(hostRole).Color);
            var prof = Profile.Current;
            int myRank = prof.RankNow;
            var mouse = Event.current.mousePosition;
            bool hover = editable && plain && r.Contains(mouse);
            int cardBg = plain && Rank.Owns(myRank, prof.CardBg) ? Rank.CardBgIndex(prof.CardBg) : 0;  // your chosen card background
            if (Rank.Owns(myRank, prof.Frame))  // your equipped frame (Rank.cs) lights the rim
                DrawFrameCard(r, Rank.FrameIndex(prof.Frame), t, false, cardBg);  // FrameLooks.cs
            else
            {
                Ui.Card(r, color, hover ? 1f : 0.85f);
                DrawCardBackground(r, cardBg, t);  // CardBackgrounds.cs
            }
            if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.05f));
            float fs = plain ? r.height - 12f : Mathf.Lerp(r.height - 10f, 72f, k);
            var face = new Rect(r.x + Mathf.Lerp(5f, 8f, k), r.y + Mathf.Lerp((r.height - fs) / 2f, 8f, k), fs, fs);
            Ui.Pill(face, new Color(color.r * 0.25f, color.g * 0.25f, color.b * 0.25f, 0.9f));
            if (BeginUiGl())
            {
                if (plain)
                {
                    UiModelAt(new V2(face.center.x, face.center.y), 0.8f);
                    DrawGenericPlayer(t);  // RankUi.cs
                }
                else
                {
                    UiModelAt(new V2(face.center.x, face.center.y), Mathf.Lerp(0.45f, 0.95f, k));
                    DrawPreviewPlayer(hostRole, kitChoice, weaponChoice, t);
                }
                EndUiGl();
            }
            // The badge sits at the right, so it never covers the name or the XP bar.
            float bsz = plain ? 46f : Mathf.Lerp(22f, 30f, k);
            float badgeRight = r.xMax - 10f;
            var rankRect = plain ? new Rect(badgeRight - bsz, r.y + (r.height - bsz) / 2f, bsz, bsz) : new Rect(face.xMax - bsz * 0.7f, face.yMax - bsz * 0.7f, bsz, bsz);
            RankLook.Draw(rankRect, myRank);
            if (editable)
                RankBadgeTip(rankRect, myRank, true);
            float tx = face.xMax + Mathf.Lerp(10f, 14f, k), tw = (plain ? rankRect.x - 8f : r.xMax - 8f) - tx;
            var nameRect = new Rect(tx, r.y + (plain ? 4f : Mathf.Lerp(3f, 8f, k)), tw, plain ? 30f : Mathf.Lerp(25f, 26f, k));
            int size = plain ? 22 : Mathf.RoundToInt(Mathf.Lerp(17f, 19f, k));
            while (size > 11 && Ui.TextWidth(nameText, size, true) + 10f > tw)
                size--;  // a long name gets smaller until it fits
            Ui.Label(nameRect, nameText, size, Color.white, TextAnchor.MiddleLeft, true);  // renamed with the pencil in Your look (CosmeticsUi.cs)
            if (plain)
            {
                // Under the name: the rank title and number, then the XP towards the next rank, thin.
                Ui.Label(new Rect(tx, r.y + 33f, tw, 18f), Rank.Title(myRank) + "  ·  Rank " + myRank, 14, RankLook.TierColor(myRank), TextAnchor.MiddleLeft, true);
                Ui.Bar(new Rect(tx, r.yMax - 11f, tw, 4f), Rank.Of(prof.Xp).Fraction, RankLook.TierColor(myRank));
                // Anywhere on the card opens your cosmetics.
                if (editable && Ui.HitArea(r))
                    OpenCosmetics();
            }
            else
                Ui.Label(new Rect(tx, r.y + Mathf.Lerp(27f, 34f, k), tw, 18f), Defs.KitName(kitChoice) + " " + Defs.Get(hostRole).Name,
                    Mathf.RoundToInt(Mathf.Lerp(11f, 14f, k)), Color.Lerp(color, Color.white, 0.15f), TextAnchor.MiddleLeft, true);
        }

        /// Your name and the port are kept between sessions (unless given on the command line).
        void LoadMenuFields()
        {
            nameText = PlayerPrefs.GetString("player_name", nameText);
            portText = PlayerPrefs.GetString("net_port", portText);
            addressText = PlayerPrefs.GetString("join_address", addressText);
        }

        void SaveMenuFields()
        {
            PlayerPrefs.SetString("player_name", nameText);
            PlayerPrefs.SetString("net_port", portText);
            PlayerPrefs.SetString("join_address", addressText);
            PlayerPrefs.Save();
            SyncProfileName();  // your name is the profile's name (ProfilesUi.cs)
        }

        /// A glowing x-axis under a title: ticks (a long one every fourth), arrowheads, a pulsing origin and
        /// sparks running outward. Call between BeginUiGl and EndUiGl (UI canvas coordinates).
        void DrawAxis(V2 o, float half, float t, float alpha = 1f)
        {
            UiModelAt(V2.Zero, 1f);
            var c = new Rgba(0.45f, 0.65f, 1f);
            const int Segs = 24;
            for (int i = 0; i < Segs; i++)  // brightest at the origin, fading out to the ends
            {
                float a = (float)i / Segs, b = (float)(i + 1) / Segs, fade = 1f - 0.7f * a;
                for (int side = -1; side <= 1; side += 2)
                {
                    V2 p0 = o + new V2(side * half * a, 0f), p1 = o + new V2(side * half * b, 0f);
                    Line(p0, p1, 6f, c.WithAlpha((0.06f * fade) * alpha));
                    Line(p0, p1, 1.5f, c.WithAlpha((0.6f * fade) * alpha));
                }
            }
            for (int k = 1; k * 24f < half - 10f; k++)
            {
                float fade = 1f - 0.7f * k * 24f / half, len = k % 4 == 0 ? 6f : 3f;
                for (int side = -1; side <= 1; side += 2)
                {
                    V2 p = o + new V2(side * k * 24f, 0f);
                    Line(p - new V2(0f, len), p + new V2(0f, len), 1.2f, c.WithAlpha((0.55f * fade) * alpha));
                }
            }
            for (int side = -1; side <= 1; side += 2)  // arrowheads
            {
                V2 tip = o + new V2(side * (half + 10f), 0f);
                SetColor(c.WithAlpha((0.5f) * alpha));
                Tri(tip, tip - new V2(side * 10f, 5f), tip - new V2(side * 10f, -5f));
                float f = MathX.Fmod(t * 0.45f, 1f);  // a spark running out along the axis
                Circle(o + new V2(side * half * f, 0f), 2.2f, Rgba.White.WithAlpha((0.8f * (1f - f)) * alpha), 10);
            }
            float pulse = MathX.Fmod(t * 0.8f, 1f);
            Arc(o, 5f + 14f * pulse, 1.5f, c.WithAlpha((0.7f * (1f - pulse)) * alpha), 24);
            Circle(o, 8f, c.WithAlpha((0.18f) * alpha), 20);
            Circle(o, 3.5f, new Rgba(0.85f, 0.92f, 1f, alpha), 14);
        }

        /// Corner brackets just outside a panel, like the frame of a plot.
        void DrawPlotCorners(Rect r, float gap, float len, Rgba c)
        {
            UiModelAt(V2.Zero, 1f);
            float x0 = r.x - gap, x1 = r.xMax + gap, y0 = r.y - gap, y1 = r.yMax + gap;
            foreach (var corner in new[] { new V2(x0, y0), new V2(x1, y0), new V2(x0, y1), new V2(x1, y1) })
            {
                float sx = corner.X < r.center.x ? 1f : -1f, sy = corner.Y < r.center.y ? 1f : -1f;
                Line(corner, corner + new V2(sx * len, 0f), 2f, c);
                Line(corner, corner + new V2(0f, sy * len), 2f, c);
            }
        }

        static string K(KeyAction a) => Keybinds.KeyName(Keybinds.Get(a));
        static string[][] howToKeys;
        static int howToKeysVersion = -1;

        /// The keys shown beside each How to play action: yours (Settings → Keybinds).
        static string[][] HowToKeys
        {
            get
            {
                if (howToKeys == null || howToKeysVersion != Keybinds.Version)
                {
                    howToKeysVersion = Keybinds.Version;
                    howToKeys = new[]
                    {
                        new[] { K(KeyAction.MoveUp), K(KeyAction.MoveLeft), K(KeyAction.MoveDown), K(KeyAction.MoveRight) }, new[] { K(KeyAction.Fire) }, new[] { K(KeyAction.Parry) },
                        new[] { K(KeyAction.Ability) }, new[] { K(KeyAction.WeaponAction) }, new[] { K(KeyAction.Ultimate) },
                        new[] { K(KeyAction.AbilityQ), K(KeyAction.AbilityE), K(KeyAction.AbilityShift) },
                        new[] { "1", "2", "3" }, new[] { K(KeyAction.Reroll) }, new[] { "Tab" }, new[] { K(KeyAction.AimLine), K(KeyAction.ToggleHud) },
                        new[] { "Esc" }, new[] { K(KeyAction.Mute) }, new[] { "R", "L", "C" }, new[] { "F1" },
                    };
                }
                return howToKeys;
            }
        }
        static readonly string[,] HowToActions =
        {
            { "Move", "or the arrow keys; change keys in Settings" },
            { "Aim and shoot", "aim with the mouse, hold to fire" },
            { "Parry", "hold: a shield toward your cursor (no shooting)" },
            { "Role ability", "" },
            { "Weapon secondary", "Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama reel" },
            { "Ultimate", "one per role and class; starts each match half charged" },
            { "Bonus abilities", "from upgrade cards; move them between keys in Tab" },
            { "Pick an upgrade", "or click a card, then Confirm (Enter)" },
            { "Reroll upgrades", "" },
            { "Stats & upgrades", "hold: just for you, doesn't pause" },
            { "Aim line, HUD", "aim line on / off · hide or show the HUD" },
            { "Pause menu", "with the Field guide: every enemy, weapon and upgrade" },
            { "Mute", "" },
            { "After a run (host)", "R: play again · L: back to lobby · C: keep going" },
            { "Developer tools", "and the Boss Lab" },
        };
        static readonly string[,] HowToTips =
        {
            { "Hitbox", "Your hitbox is the small ring in the middle of your character, with a coloured dot at its centre. Only the ring's inside can be hit, not the whole body." },
            { "Wrap around", "Walk off an edge to come in on the other side. Enemies and bullets can't follow." },
            { "Status icons", "Snowflake = frozen, arrows = slowed, flame = burning, drop = bleeding, crosshair = marked." },
            { "Downed", "At 0 HP you're downed: stand by a downed ally for 3 s to revive them. Clearing a wave brings everyone back." },
            { "Upgrades", "Levels give upgrade picks at the end of each wave; bosses give legendary ones." },
            { "Boss shots", "Dark-cored shots are a boss's: violet ones wear blockers down, red ones can't be blocked." },
            { "Co-op", "1 to 3 players: one hosts, the others join (Tailscale makes this easy)." },
            { "Perfect parry", "Raise your parry just as a shot arrives: blocked bullets fly back at enemies and cost nothing." },
            { "Roles", "Enemies prefer to target the Tank. A Healer's aura heals allies nearby. DPS hits hardest." },
            { "Health orbs", "Kills sometimes drop green orbs that heal you. They drift toward you when you're close." },
            { "Ability mastery", "Abilities level up from 1 to 5 as you use them in fights. Level 5 unlocks a capstone." },
            { "Paths", "After each boss the host picks a path. More skulls: harder waves, better prize. The Bazaar has a shop." },
            { "Boss bodies", "Touching a boss hurts and shoves you back. Parry up close to get past its fire." },
            { "Practice Range", "Pick it in the lobby to play waves and bosses you set up yourself." },
        };

        /// How to play, as the side drawer (MenuDrawer.cs).
        void DrawHowToPlay() { if (!SkipWhileClosing) DrawHowToWindow(); }  // (a closing window takes no clicks, WindowAnim.cs)

        /// A keyboard key: a raised cap with its label. Returns its width.
        float KeyCap(float x, float y, string key)
        {
            float w = Mathf.Max(26f, 12f + key.Length * 7.5f);
            Ui.Pill(new Rect(x, y + 2f, w, 24f), new Color(0.03f, 0.035f, 0.06f, 0.9f));  // its side
            Ui.Pill(new Rect(x, y, w, 23f), new Color(0.24f, 0.27f, 0.38f));
            Ui.Label(new Rect(x, y, w, 23f), key, 11, Color.white, TextAnchor.MiddleCenter, true);
            return w;
        }

        /// The small drawing beside each How to play tip.
        void DrawTipGlyph(int i, V2 c, float t)
        {
            Circle(c, 17f, new Rgba(1f, 1f, 1f, 0.04f), 24);
            switch (i)
            {
                case 0:  // hitbox: a character with its white hitbox ring and coloured dot
                    Circle(c, 11f, new Rgba(0.36f, 0.6f, 1f), 24);
                    Circle(c, 7.5f, new Rgba(0.08f, 0.1f, 0.16f), 20);
                    Arc(c, 5.5f, 1.6f, new Rgba(1f, 1f, 1f, 0.95f), 16);
                    Circle(c, 2f + 0.5f * Mathf.Sin(t * 4f), new Rgba(0.36f, 0.6f, 1f), 12);
                    break;
                case 1:  // wrap around: out one side, in the other
                {
                    var col = new Rgba(0.6f, 0.8f, 1f, 0.9f);
                    float s = MathX.Fmod(t * 0.7f, 1f) * 24f - 12f;
                    Line(c - new V2(12f, 0f), c + new V2(12f, 0f), 1.5f, col.WithAlpha(0.35f));
                    Line(c + new V2(-12f, -9f), c + new V2(-12f, 9f), 2f, col);
                    Line(c + new V2(12f, -9f), c + new V2(12f, 9f), 2f, col);
                    Circle(c + new V2(s, 0f), 3.5f, col, 12);
                    break;
                }
                case 2:  // status icons
                    Circle(c + new V2(-7f, -6f), 4f, new Rgba(0.6f, 0.9f, 1f), 12);
                    Circle(c + new V2(7f, -6f), 4f, new Rgba(0.35f, 0.55f, 1f), 12);
                    Circle(c + new V2(-7f, 6f), 4f, new Rgba(1f, 0.55f, 0.2f), 12);
                    Circle(c + new V2(0f, 0f), 3f, new Rgba(0.85f, 0.1f, 0.2f), 10);
                    Arc(c + new V2(7f, 6f), 4f, 1.5f, new Rgba(1f, 0.3f, 0.3f), 12);
                    break;
                case 3:  // downed: a revive cross
                {
                    var g = new Rgba(0.45f, 1f, 0.55f);
                    Line(c - new V2(9f, 0f), c + new V2(9f, 0f), 5f, g);
                    Line(c - new V2(0f, 9f), c + new V2(0f, 9f), 5f, g);
                    break;
                }
                case 4:  // upgrades: a gold gem
                    Polygon(c, 11f, 4, 0f, new Rgba(1f, 0.75f, 0.25f));
                    Polygon(c, 6f, 4, 0f, new Rgba(1f, 0.92f, 0.6f));
                    break;
                case 5:  // boss shots: violet and red, dark cores
                    Circle(c + new V2(-6f, -3f), 7f, new Rgba(0.7f, 0.4f, 1f), 16);
                    Circle(c + new V2(-6f, -3f), 3f, new Rgba(0.1f, 0.05f, 0.15f), 10);
                    Circle(c + new V2(7f, 5f), 6f, new Rgba(1f, 0.3f, 0.3f), 16);
                    Circle(c + new V2(7f, 5f), 2.5f, new Rgba(0.15f, 0.03f, 0.03f), 10);
                    break;
                case 6:  // co-op: three players in their role colours
                    for (int k = 0; k < 3; k++)
                        Circle(c + V2.FromAngle(-MathX.Pi / 2f + k * MathX.Tau / 3f + t * 0.5f) * 8f, 4.5f, Defs.Roles[k].Color, 14);
                    break;
                case 7:  // perfect parry: a shield arc and a shot bouncing back off it
                {
                    var blue = new Rgba(0.55f, 0.8f, 1f);
                    float k = MathX.Fmod(t * 0.8f, 1f), x = k < 0.5f ? 12f - k * 2f * 16f : -4f + (k - 0.5f) * 2f * 16f;
                    Circle(c + new V2(-9f, 0f), 5f, new Rgba(0.36f, 0.6f, 1f), 14);
                    Arc(c + new V2(-9f, 0f), 11f, 2.5f, blue, 12, -0.9f, 1.8f);
                    Circle(c + new V2(x, 0f), 3f, k < 0.5f ? new Rgba(1f, 0.3f, 0.45f) : blue, 10);
                    break;
                }
                case 8:  // roles: the tank drawing enemies' eyes, a healer's aura, a DPS spark
                    Circle(c + new V2(0f, -7f), 5f, Defs.Roles[0].Color, 14);
                    Arc(c + new V2(0f, -7f), 8f + 1.5f * Mathf.Sin(t * 4f), 1.5f, Defs.Roles[0].Color.WithAlpha(0.6f), 16);
                    Circle(c + new V2(-8f, 7f), 4f, Defs.Roles[1].Color, 12);
                    Circle(c + new V2(8f, 7f), 4f, Defs.Roles[2].Color, 12);
                    break;
                case 9:  // health orbs: a green orb drifting in, pulsing
                {
                    var g = new Rgba(0.4f, 1f, 0.5f);
                    float s = 5f + Mathf.Sin(t * 5f);
                    Circle(c, s + 4f, g.WithAlpha(0.25f), 16);
                    Circle(c, s, g, 16);
                    Line(c - new V2(2.5f, 0f), c + new V2(2.5f, 0f), 1.5f, Rgba.White);
                    Line(c - new V2(0f, 2.5f), c + new V2(0f, 2.5f), 1.5f, Rgba.White);
                    break;
                }
                case 10:  // ability mastery: five pips filling up
                {
                    int lit = 1 + (int)MathX.Fmod(t * 1.2f, 5f);
                    for (int k = 0; k < 5; k++)
                        Circle(c + new V2(-12f + k * 6f, 0f), 2.4f, k < lit ? new Rgba(0.5f, 1f, 0.6f) : new Rgba(1f, 1f, 1f, 0.15f), 8);
                    break;
                }
                case 11:  // paths: a fork in the road
                {
                    var col = new Rgba(1f, 0.85f, 0.5f, 0.9f);
                    Line(c + new V2(0f, 11f), c + new V2(0f, 1f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(-9f, -9f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(9f, -9f), 2f, col);
                    Circle(c + new V2(-9f, -9f), 2.5f, col, 8);
                    Circle(c + new V2(9f, -9f), 2.5f, new Rgba(1f, 0.4f, 0.35f), 8);
                    break;
                }
                case 12:  // boss bodies: a big body and you bounced off it
                {
                    float k = Mathf.Abs(Mathf.Sin(t * 2f));
                    Polygon(c + new V2(-5f, 0f), 10f, 8, t * 0.4f, new Rgba(1f, 0.25f, 0.5f));
                    Circle(c + new V2(8f + k * 5f, 0f), 3.5f, new Rgba(0.36f, 0.6f, 1f), 12);
                    break;
                }
                default:  // practice range: a target
                    Arc(c, 11f, 2f, new Rgba(1f, 0.45f, 0.4f), 20);
                    Arc(c, 6f, 2f, new Rgba(1f, 0.45f, 0.4f), 16);
                    Circle(c, 2f, Rgba.White, 8);
                    break;
            }
        }

        // --- Pause menu and audio settings --------------------------------------

        void OpenPauseMenu()
        {
            pauseMenuOpen = true;
            pauseSel = 0;
            pauseSettings = pauseHowTo = pauseRecords = pauseConfirmLobby = false;
            if (game.State != MatchState.GameOver)
                game.RequestPause(true);  // pauses the match for both players
        }

        void ResumeGame()
        {
            pauseMenuOpen = false;
            pauseSettings = pauseHowTo = pauseRecords = pauseConfirmLobby = false;
            runStatsOpen = false;
            if (game.Paused)
                game.RequestPause(false);
        }

        // The pause menu is the side drawer (DrawPauseWindow, MenuDrawer.cs): Resume, Run stats, Field guide, Settings,
        // Leave game, the party, and the run's card. DrawPauseMenu (WindowAnim.cs) slides it in and out.

        /// Settings (main menu, lobby; the pause menu's opens from its drawer), as the side drawer (MenuDrawer.cs).
        void DrawSettings() { if (!SkipWhileClosing) DrawSettingsWindow(false); }

        bool VolumeSlider(float x, ref float y, float w, string label, ref float value, Color tint)
        {
            Ui.Label(new Rect(x, y, w - 60f, 20f), label, 14, Ui.Text);
            Ui.Label(new Rect(x + w - 60f, y, 60f, 20f), Mathf.RoundToInt(value * 100f) + "%", 14, Ui.Muted, TextAnchor.MiddleRight, true);
            float v = Ui.MeterSlider(new Rect(x, y + 22f, w, 36f), value, tint);
            y += 66f;
            if (Mathf.Abs(v - value) < 0.0001f)
                return false;
            value = v;
            return true;
        }

        void LoadAudioSettings()
        {
            sounds.Master = PlayerPrefs.GetFloat("vol_master", 1f);
            sounds.Weapons = PlayerPrefs.GetFloat("vol_weapons", 1f);
            sounds.Enemies = PlayerPrefs.GetFloat("vol_enemies", 1f);
            sounds.Effects = PlayerPrefs.GetFloat("vol_effects", 1f);
            sounds.Music = PlayerPrefs.GetFloat("vol_music", 0.7f);
            sounds.Muted = PlayerPrefs.GetInt("muted", 0) == 1;
            aimLine = PlayerPrefs.GetInt("aim_line", 1) == 1;
        }

        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("vol_master", sounds.Master);
            PlayerPrefs.SetFloat("vol_weapons", sounds.Weapons);
            PlayerPrefs.SetFloat("vol_enemies", sounds.Enemies);
            PlayerPrefs.SetFloat("vol_effects", sounds.Effects);
            PlayerPrefs.SetFloat("vol_music", sounds.Music);
            PlayerPrefs.SetInt("muted", sounds.Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        static string KitBlurb(Kit k) => Classes.Get(k).Blurb;  // KitDef.Blurb
    }
}

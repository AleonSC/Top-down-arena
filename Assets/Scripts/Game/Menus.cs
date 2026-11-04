using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The main menu, How to play, and the pause menu and audio settings.
    public partial class ArenaApp
    {
        // The main menu ("Big play", Docs/Mockups/menu-d.html): the title on its x-axis with a shot arcing over it and a
        // line under it that changes; in a box (MenuCard, which grows into the lobby's loadout, LobbyEnter.cs) one big
        // Host game button over Join and Continue; Join opens the address row under the box; a bar along the bottom with
        // your name, Records, Settings and Quit. The port is in Settings (Gameplay). The first time the menu shows, it
        // draws itself in (MenuIntro.cs); each piece asks MenuIntroK how far in it is.
        const float MenuTitleY = 126f, MenuTitleSize = 74f, MenuAxisY = 188f, MenuAxisHalf = 390f, MenuTipY = 220f;
        const float ArcX0 = 250f, ArcX1 = 1030f, ArcTop = 24f, ArcBase = 162f;  // the shot's arc over the title
        const float MenuBarY = H - 70f;                                        // the bottom bar
        static readonly Rect MenuCard = new Rect(W / 2f - 220f, (H - 214f) / 2f, 440f, 214f);  // in the middle of the screen
        static readonly Rect MenuHostRect = new Rect(W / 2f - 190f, MenuCard.y + 24f, 380f, 82f);
        static readonly Rect MenuJoinRect = new Rect(W / 2f - 190f, MenuCard.y + 124f, 186f, 52f);
        static readonly Rect MenuContinueRect = new Rect(W / 2f + 4f, MenuCard.y + 124f, 186f, 52f);
        static readonly Rect MenuJoinRow = new Rect(W / 2f - 220f, MenuCard.yMax + 12f, 440f, 64f);
        static readonly Rect MenuNameRect = new Rect(28f, MenuBarY + 11f, 300f, 48f);
        bool menuJoinOpen;    // Join opened the address row
        int menuTipSeed;      // which line shows under the title (a new one each time the menu opens)

        void DrawMenu()
        {
            float t = Time.unscaledTime;
            DrawMenuIntroBackdrop();  // the background fading up as the intro starts (MenuIntro.cs)
            DrawMenuTitle(t);
            float box = MenuIntroK(1.65f, 2.0f);
            if (box > 0f)
                TileTransformed(MenuCard.center, Mathf.Lerp(0.3f, 1f, EaseBack(box)), 0f, () => WithAlpha(Mathf.Clamp01(box * 2.5f), () => Ui.Panel(MenuCard, true)));
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, MenuAxisY), MenuAxisHalf * EaseOut(MenuIntroK(0f, 0.45f)), t);  // the title sits on an x-axis
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
            DrawMenuIntroFade();  // the short version of the intro: a fade (MenuIntro.cs)
        }

        /// The title, the line under it (typed in during the intro), drawn in ink then filled during the intro.
        void DrawMenuTitle(float t, float alpha = 1f)
        {
            var r = new Rect(0f, MenuTitleY - MenuTitleSize * 0.6f, W, MenuTitleSize * 1.2f);
            DrawMenuTitleText(r);
            float tip = MenuIntroK(2.2f, 2.55f);
            if (tip > 0f)
                DrawMenuTipChip(t, alpha, tip);
        }

        /// The line under the title: your best run, then the How to play tips, a new one every 8 s. Gives its topic and the
        /// rest separately, whether it's a record (else a tip), and how long it has been up.
        void MenuTip(float t, out string topic, out string body, out bool record, out float age)
        {
            var p = Profile.Current;
            int n = HowToTips.GetLength(0) + (p.BestWave > 0 ? 1 : 0);
            int i = (Mathf.FloorToInt(t / MenuTipCycle) + menuTipSeed) % n;
            age = t % MenuTipCycle;
            record = p.BestWave > 0 && i == 0;
            if (record)
            {
                topic = "Best run";
                body = "wave " + p.BestWave + "  ·  " + p.RunsStarted + (p.RunsStarted == 1 ? " run" : " runs") + " started";
                return;
            }
            int k = i - (p.BestWave > 0 ? 1 : 0);
            topic = HowToTips[k, 0];
            body = HowToTips[k, 1];
        }

        const float MenuTipCycle = 8f;

        /// The line under the title as a chip ("A", Docs/Mockups/menu-tip.html): a dark pill with a coloured tag (no box behind it: a bulb and
        /// TIP in blue, or a trophy and RECORD in gold), the topic in bold, then the rest. Each line slides up and fades in,
        /// and slides on up as it fades out. `intro` is how far the menu's intro has brought it in (1 = in place).
        void DrawMenuTipChip(float t, float alpha, float intro = 1f)
        {
            MenuTip(t, out string topic, out string body, out bool record, out float age);
            float inK = intro < 1f ? intro : Mathf.Clamp01(age / 0.35f), outK = intro < 1f ? 0f : Mathf.Clamp01((age - (MenuTipCycle - 0.3f)) / 0.3f);
            inK = 1f - (1f - inK) * (1f - inK) * (1f - inK);
            float a = inK * (1f - outK) * alpha;
            if (a <= 0f)
                return;
            float dy = 10f * (1f - inK) - 8f * outK;
            var col = record ? new Color(1f, 0.8f, 0.35f) : new Color(0.43f, 0.65f, 1f);
            string tag = record ? "RECORD" : "TIP";
            float tagW = Ui.TextWidth(tag, 11, true) + 30f, topicW = Ui.TextWidth(topic, 15, true) + 10f, bodyW = Ui.TextWidth(body, 15);
            float w = tagW + 6f + topicW + bodyW + 16f, h = 30f, x = W / 2f - w / 2f, y = MenuTipY - h / 2f + dy;
            Ui.Capsule(new Rect(x, y, w, h), new Color(0.05f, 0.06f, 0.1f, 0.88f * a), new Color(col.r, col.g, col.b, 0.35f * a));
            Ui.Icon(new Rect(x + 12f, y + 9f, 12f, 12f), PixelArt.Get(record ? "ui_trophy" : "ui_bulb"), new Color(col.r, col.g, col.b, a));  // at its pixel size: crisp
            Ui.Label(new Rect(x + 28f, y, tagW - 24f, h), tag, 11, new Color(col.r, col.g, col.b, a), TextAnchor.MiddleLeft, true);
            float tx = x + tagW + 6f;
            Ui.Label(new Rect(tx, y, topicW, h), topic, 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(tx + topicW, y, bodyW + 8f, h), body, 15, new Color(0.78f, 0.82f, 0.91f, 0.95f * a), TextAnchor.MiddleLeft);
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

        /// The box's buttons: Host game, Join and Continue, the address row Join opens, and how joining is going.
        /// live = false draws them only (Repaint), for the intro and the move into the lobby (LobbyEnter.cs).
        void DrawMenuCardInside(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float host = MenuIntroK(1.8f, 2.2f), join = MenuIntroK(2.0f, 2.3f), cont = MenuIntroK(2.1f, 2.4f);
            if (host > 0f)
                MenuRise(host, 60f, () =>
                {
                    Ui.PulseRing(MenuHostRect, Ui.Primary, t);
                    if (Ui.FancyButton(MenuHostRect, "HOST GAME", Ui.Primary, PixelArt.Get("ui_play"), t, 30) && live)
                        HostGame();
                });
            if (join > 0f)
                MenuRise(join, 24f, () =>
                {
                    if (Ui.FancyButton(MenuJoinRect, menuJoinOpen ? "Join  ▲" : "Join game", Ui.Neutral, PixelArt.Get("ui_arrow"), t, 18) && live)
                        menuJoinOpen = !menuJoinOpen;
                });
            if (cont > 0f)
                MenuRise(cont, 24f, () =>
                {
                    int saved = savedRunsCount;  // saved runs (SavesUi.cs)
                    var col = saved > 0 ? new Color(SaveColor.r * 0.6f, SaveColor.g * 0.6f, SaveColor.b * 0.6f) : Ui.Neutral;
                    if (Ui.FancyButton(MenuContinueRect, "Continue", col, PixelArt.Get("ui_play"), t, 18) && live)
                        OpenContinue();
                    Ui.Label(new Rect(MenuContinueRect.x, MenuContinueRect.yMax + 4f, MenuContinueRect.width, 18f),
                        saved > 0 ? saved + (saved == 1 ? " saved run" : " saved runs") : "No saved runs yet", 12,
                        saved > 0 ? new Color(0.66f, 0.85f, 0.75f, 0.85f) : new Color(1f, 1f, 1f, 0.4f), TextAnchor.MiddleCenter);
                });
            // Join's address row (or, while connecting, how it's going and Cancel), then any message from the last try.
            var row = MenuJoinRow;
            if (session.Connecting)
            {
                Ui.Panel(row, false);
                Ui.Label(new Rect(row.x + 16f, row.y, row.width - 170f, row.height), session.Status, 14, Ui.Muted, TextAnchor.MiddleLeft, false, true);
                if (Ui.FancyButton(new Rect(row.xMax - 150f, row.y + 12f, 136f, 40f), "Cancel", Ui.Danger, PixelArt.Get("ui_back"), t, 15) && live)
                    session.Leave("");
            }
            else if (menuJoinOpen && join >= 1f)
            {
                Ui.Panel(row, false);
                Ui.Label(new Rect(row.x + 16f, row.y + 4f, 200f, 14f), "HOST ADDRESS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                addressText = Ui.Field(new Rect(row.x + 14f, row.y + 20f, row.width - 178f, 36f), addressText, 64);
                if (Ui.FancyButton(new Rect(row.xMax - 154f, row.y + 18f, 140f, 38f), "Connect", Ui.Primary, PixelArt.Get("ui_arrow"), t, 16) && live)
                    JoinGame();
            }
            if (session.Status.Length > 0 && !session.Connecting && live)
                Ui.Label(new Rect(row.x - 40f, (menuJoinOpen ? row.yMax : row.y) + 6f, row.width + 80f, 40f), session.Status, 13, Ui.Warn, TextAnchor.UpperCenter, false, true);
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

        /// The bar along the bottom: your name (type to change it), Records, Settings and Quit. live = false draws it only
        /// (Repaint), for the intro and the move into the lobby.
        void DrawMenuSecondary(float t, bool live, bool withName = true)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float k = MenuIntroK(2.1f, 2.5f);
            if (k <= 0f)
                return;
            GUI.BeginGroup(new Rect(0f, 80f * (1f - EaseOut(k)), W, H));
            if (live)
                TipClear();  // the rank badge's hover card (RankUi.cs)
            Ui.Fill(new Rect(0f, MenuBarY, W, 70f), new Color(0.03f, 0.04f, 0.06f, 0.82f));
            Ui.Fill(new Rect(0f, MenuBarY, W, 1f), new Color(0.42f, 0.6f, 1f, 0.3f));
            if (withName)
                DrawNameCard(MenuNameRect, 0f, live, t);
            if (Ui.FancyButton(new Rect(W / 2f - 125f, MenuBarY + 16f, 120f, 38f), "Records", new Color(0.36f, 0.3f, 0.14f), PixelArt.Get("ui_trophy"), t, 14) && live)
                recordsOpen = true;
            if (Ui.FancyButton(new Rect(W / 2f + 5f, MenuBarY + 16f, 120f, 38f), "Settings", Ui.Neutral, PixelArt.Get("ui_gear"), t, 14) && live)
                settingsOpen = true;
            if (Ui.FancyButton(new Rect(W - 148f, MenuBarY + 16f, 120f, 38f), "Quit", new Color(0.46f, 0.18f, 0.21f), PixelArt.Get("ui_power"), t, 14) && live)
                Application.Quit();
            if (live)
                DrawTip();
            GUI.EndGroup();
        }

        /// Your name card, sized for the bottom bar (k = 0): a neutral player figure (no role or class: that's chosen in the
        /// lobby), your name, your rank title and badge, and the XP towards the next rank. Click the name or the pencil to
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
            if (Rank.Owns(myRank, prof.Frame))  // your equipped frame (Rank.cs) lights the rim
                Ui.Card(r, RankLook.Tint(Rank.FrameColor(Rank.FrameIndex(prof.Frame))), 1f);
            else
                Ui.Card(r, color, hover ? 1f : 0.85f);
            if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.05f));
            float fs = Mathf.Lerp(r.height - 10f, 72f, k);
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
            // The badge sits at the right, before the pencil, so it never covers the name or the XP bar.
            float bsz = plain ? 30f : Mathf.Lerp(22f, 30f, k);
            float badgeRight = r.xMax - (editable ? 46f : 10f);
            var rankRect = plain ? new Rect(badgeRight - bsz, r.y + (r.height - bsz) / 2f, bsz, bsz) : new Rect(face.xMax - bsz * 0.7f, face.yMax - bsz * 0.7f, bsz, bsz);
            RankLook.Draw(rankRect, myRank);
            if (editable)
                RankBadgeTip(rankRect, myRank, true);
            float tx = face.xMax + Mathf.Lerp(10f, 14f, k), tw = (plain ? rankRect.x - 8f : r.xMax - 8f - (editable ? 36f : 0f)) - tx;
            var nameRect = new Rect(tx - (editable ? 4f : 0f), r.y + Mathf.Lerp(3f, 8f, k), tw + (editable ? 4f : 0f), Mathf.Lerp(25f, 26f, k));
            int size = Mathf.RoundToInt(Mathf.Lerp(17f, 19f, k));
            while (size > 11 && Ui.TextWidth(nameText, size, true) + 10f > tw)
                size--;  // a long name gets smaller until it fits
            var pencil = new Rect(r.xMax - 38f, r.y + (r.height - 30f) / 2f, 30f, 30f);
            if (editable)
            {
                // The pencil puts the cursor in your name (clicking the name itself works too); it lights up while typing.
                bool typing = GUI.GetNameOfFocusedControl() == "menu_name";
                if (Ui.Button(pencil, "", typing ? Ui.Primary : Ui.Neutral))
                    GUI.FocusControl("menu_name");
                Ui.Icon(new Rect(pencil.x + 7f, pencil.y + 7f, 16f, 16f), PixelArt.Get("ui_pencil"), Color.white);
                string before = nameText;
                GUI.SetNextControlName("menu_name");
                nameText = Ui.BareField(nameRect, nameText, 16, size);
                if (nameText != before)
                    SaveMenuFields();
            }
            else
                Ui.Label(nameRect, nameText, size, Color.white, TextAnchor.MiddleLeft, true);
            if (plain)
            {
                // Under the name: the rank title and number, then the XP towards the next rank, thin.
                Ui.Label(new Rect(tx, r.y + 25f, tw, 16f), Rank.Title(myRank) + "  ·  Rank " + myRank, 11, RankLook.TierColor(myRank), TextAnchor.MiddleLeft, true);
                Ui.Bar(new Rect(tx, r.yMax - 7f, tw, 3f), Rank.Of(prof.Xp).Fraction, RankLook.TierColor(myRank));
                // Anywhere else on the chip opens your cosmetics. (Drawn last: the name and the pencil take their own clicks first.)
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
            { "Hitbox", "Your hitbox is just the small white dot in the middle of your character." },
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
                case 0:  // hitbox: a character with its white dot
                    Circle(c, 11f, new Rgba(0.36f, 0.6f, 1f), 24);
                    Circle(c, 7.5f, new Rgba(0.08f, 0.1f, 0.16f), 20);
                    Circle(c, 2.6f + 0.6f * Mathf.Sin(t * 4f), Rgba.White, 12);
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

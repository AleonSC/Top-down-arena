using UnityEngine;

namespace Arena
{
    /// Your profile (Docs/Mockups/profile-merge.html, layout 1: card plus tabs). One drawer for what used to be two windows, "Your look"
    /// and Records. On the left of the drawer: your card (badge, name, title, XP) and four tabs. Under the tabs, the rows of the open tab:
    ///   Look     the cosmetics (CosmeticsUi.cs: Frame, Trail, Background, Card background, Name effect, Aim line, Cursor)
    ///   Rank     the Reward ladder (CosmeticsUi.cs) and Rank and runs (RankUi.cs)
    ///   Records  Overview, Bosses, Classes, Challenge rooms (MenuDrawer.cs, DrawRecordsSection)
    ///   Unlocks  the unlock list
    /// The open row fills the panel on the right. It opens from your player card on the main menu (OpenProfile). In the lobby and pause
    /// menus it opens without the Look tab (nothing is renamed or equipped mid-run; Rank > Rank and runs still has Equip).
    public partial class ArenaApp
    {
        bool profileOpen;
        int profileTab;      // 0 Look, 1 Rank, 2 Records, 3 Unlocks
        int rankSection;     // in the Rank tab: 0 Reward ladder, 1 Rank and runs

        static readonly string[] ProfileTabs = { "Look", "Rank", "Records", "Unlocks" };
        static readonly string[] RankRows = { "Reward ladder", "Rank and runs" };
        static readonly string[] RecordRows = { "Overview", "Bosses", "Classes", "Challenge rooms" };

        /// Open the profile on a tab (0 Look, 1 Rank, 2 Records, 3 Unlocks). Opening it ends the main menu's "your profile" nudge for good.
        void OpenProfile(int tab = 0)
        {
            profileOpen = true;
            profileTab = Mathf.Clamp(tab, 0, ProfileTabs.Length - 1);
            cosmeticsSection = 0;
            rankSection = 0;
            recordsSection = Mathf.Clamp(recordsSection, 0, RecordRows.Length - 1);
            ladderScroll = -1f;  // the ladder opens at your rank
            editingName = false;
            var p = Profile.Current;
            if (!p.ProfileOpened)
            {
                p.ProfileOpened = true;
                p.Save();
            }
        }

        void DrawProfile() { if (!SkipWhileClosing) DrawProfileWindow(); }

        void DrawProfileWindow(bool inMenu = false)
        {
            var p = Profile.Current;
            float t = Time.unscaledTime;
            float k = DrawerIn(inMenu ? "pause-profile" : "profile", !inMenu);
            if (inMenu && profileTab == 0)
                profileTab = 1;
            if (inMenu)
                editingName = false;
            var info = Rank.Of(p.Xp);
            var tc = RankLook.TierColor(info.Rank);
            BeginDrawer("PROFILE", Ui.Gold, k, "Your look, rank and records");
            bool closeChip = Ui.CloseChip(new Rect(DrawerW - 40f - 44f, 38f, 44f, 44f));

            // The card: the badge, your name and title, the XP towards the next rank.
            RankLook.Draw(new Rect(40f, 130f, 72f, 72f), info.Rank);
            int nsize = 22;
            while (nsize > 12 && Ui.TextWidth(nameText, nsize, true) + 10f > DrawerW - 210f)
                nsize--;
            var nameBox = new Rect(128f, 134f, DrawerW - 210f, 30f);
            if (editingName)
            {
                string before = nameText;
                GUI.SetNextControlName("look_name");
                nameText = Ui.BareField(nameBox, nameText, 16, nsize);
                if (nameText != before)
                    SaveMenuFields();
                if (Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
                {
                    editingName = false;
                    GUI.FocusControl(null);
                    Event.current.Use();
                }
            }
            else
                Ui.Label(nameBox, nameText, nsize, Color.white, TextAnchor.MiddleLeft, true);
            if (!inMenu)
            {
                // The pencil: rename yourself here (the main menu's card only opens this window).
                float nw = editingName ? Ui.TextWidth(nameText, nsize, true) + 10f : Ui.TextWidth(nameText, nsize, true);
                var pencil = new Rect(Mathf.Min(128f + nw + 4f, DrawerW - 72f), 138f, 24f, 24f);
                if (Ui.HitArea(pencil))
                {
                    editingName = !editingName;
                    if (editingName)
                        GUI.FocusControl("look_name");
                    else
                    {
                        GUI.FocusControl(null);
                        SaveMenuFields();
                    }
                }
                Ui.Icon(new Rect(pencil.x + 3f, pencil.y + 3f, 18f, 18f), PixelArt.Get("ui_pencil"), pencil.Contains(Event.current.mousePosition) || editingName ? Ui.Gold : Ui.Muted);
            }
            Ui.Label(new Rect(128f, 164f, DrawerW - 160f, 20f), Rank.Title(info.Rank) + "  ·  Rank " + info.Rank, 14, tc, TextAnchor.MiddleLeft, true);
            Ui.Bar(new Rect(42f, 218f, DrawerW - 84f, 9f), info.Fraction, tc);
            Ui.Label(new Rect(42f, 230f, DrawerW - 84f, 18f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP", 11, Ui.Muted, TextAnchor.MiddleRight);

            // The tabs.
            int first = inMenu ? 1 : 0, n = ProfileTabs.Length - first;
            float gap = 6f, tw = (DrawerW - 56f - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                int id = first + i;
                if (Ui.SmallButton(new Rect(28f + i * (tw + gap), 262f, tw, 34f), ProfileTabs[id], profileTab == id ? Ui.GoldButton : Ui.Neutral, 14) && profileTab != id)
                {
                    profileTab = id;
                    if (id == 2)
                        recordsSection = Mathf.Clamp(recordsSection, 0, RecordRows.Length - 1);
                }
            }

            // The rows of the open tab.
            int bossesBeaten = 0, classWins = 0, roomsWon = 0;
            foreach (var b in RecordBosses)
                if (p.Beaten(b) > 0)
                    bossesBeaten++;
            foreach (var kv in p.WinsAs)
                if (kv.Value > 0)
                    classWins++;
            foreach (var c in Challenges.All)
                roomsWon += p.RoomWon(c);
            Color[] cols = { Ui.Gold, Ui.Gold, new Color(1f, 0.6f, 0.3f), new Color(0.45f, 0.9f, 1f), Ui.Gold, tc };
            float y = 312f;
            if (profileTab == 0)
            {
                for (int i = 0; i < 7; i++)  // (the Reward ladder is in the Rank tab)
                {
                    bool on = i == cosmeticsSection;
                    var r = new Rect(28f, y, DrawerW - 56f, on ? 56f : 46f);
                    if (DrawerRow(r, PixelArt.Get(CosmeticIcons[i]), CosmeticSections[i], EquippedName(p, KindOfSection(i)), null, null, Ui.Gold, on))
                        cosmeticsSection = i;
                    y += r.height + 4f;
                }
            }
            else if (profileTab == 1)
            {
                string[] icons = { "ui_trophy", "ui_star" };
                string[] lines = { "What each rank unlocks", "Your XP and the last runs" };
                string[] tags = { null, "Rank " + p.RankNow };
                Color[] rc = { Ui.Gold, tc };
                for (int i = 0; i < RankRows.Length; i++)
                {
                    bool on = i == rankSection;
                    var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                    if (DrawerRow(r, PixelArt.Get(icons[i]), RankRows[i], lines[i], null, tags[i], rc[i], on))
                        rankSection = i;
                    y += r.height + 4f;
                }
            }
            else if (profileTab == 2)
            {
                string[] icons = { "ui_star", "ui_skull", "class_knight", "blink" };
                string[] tags = { p.RunsStarted + " runs", bossesBeaten + " / " + RecordBosses.Length, classWins + " won", roomsWon + " won" };
                for (int i = 0; i < RecordRows.Length; i++)
                {
                    bool on = i == recordsSection;
                    var r = new Rect(28f, y, DrawerW - 56f, on ? 62f : 52f);
                    if (DrawerRow(r, PixelArt.Get(icons[i]), RecordRows[i], i == 0 ? "Practice and dev-tool runs don't count" : null, null, tags[i], cols[i], on,
                        i == 1 && bossesBeaten > 0 ? Ui.Good : (Color?)null))
                        recordsSection = i;
                    y += r.height + 4f;
                }
            }
            else
            {
                int got = 0;
                foreach (var u in Unlocks.All)
                    if (p.IsUnlocked(u.Id))
                        got++;
                Ui.Label(new Rect(42f, y + 4f, DrawerW - 84f, 20f), Unlocks.All.Count == 0 ? "Unlocks are on their way" : got + " of " + Unlocks.All.Count + " unlocked", 15, Ui.Text, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(42f, y + 30f, DrawerW - 84f, 40f), "Each unlock opens up when its condition is met. Locked ones show what they need.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            }
            if (closeChip)
            {
                if (inMenu)
                    pauseRecords = false;
                else
                    CloseWindow(() => profileOpen = false);
            }
            EndDrawer();

            // The open row, in the panel on the right.
            int sub = profileTab == 0 ? cosmeticsSection : profileTab == 1 ? rankSection : profileTab == 2 ? recordsSection : 0;
            BeginPanelAnim("profile" + profileTab + "_" + sub);
            switch (profileTab)
            {
                case 0: DrawCosmeticPanel(p, t, k); break;  // CosmeticsUi.cs
                case 1:
                    if (rankSection == 0)
                        DrawLadderPanel(p, k);  // CosmeticsUi.cs
                    else
                        DrawRecordsSection(5, p, t, bossesBeaten, roomsWon, cols);  // the Rank page (RankUi.cs)
                    break;
                case 2: DrawRecordsSection(recordsSection, p, t, bossesBeaten, roomsWon, cols); break;
                default: DrawRecordsSection(4, p, t, bossesBeaten, roomsWon, cols); break;  // Unlocks
            }
            EndPanelAnim();
        }

        // --- The nudge on the main menu ----------------------------------------------------------------------------------------
        // Docs/Mockups/profile-merge.html, hint 5: a small tag beside the player card, bouncing toward it, until the profile has been
        // opened once (or for the first five launches). The card is the only way into the profile, so this is how players find it.

        Profile hintCountedFor;  // the profile this launch's menu has been counted for (Profile.NoteMenuLaunch)

        void DrawProfileHint(Rect card, float t, bool live)
        {
            var p = Profile.Current;
            if (!ReferenceEquals(hintCountedFor, p))
            {
                hintCountedFor = p;
                p.NoteMenuLaunch();
                p.Save();
            }
            if (!p.ShowProfileHint)
                return;
            const string text = "YOUR PROFILE";
            float bob = fxReduced ? 4f : Mathf.Abs(Mathf.Sin(t * 3f)) * 8f;  // toward the card and back (still with Reduced effects)
            float w = Ui.TextWidth(text, 14, true) + 28f;
            var r = new Rect(card.xMax + 24f + bob, card.center.y - 15f, w, 30f);
            var violet = new Color(0.6f, 0.37f, 0.9f);
            Ui.Pill(r, new Color(0.2f, 0.1f, 0.38f, 0.96f));
            Ui.Rim(r, violet, 0.9f);
            for (int i = 0; i < 9; i++)  // the wedge pointing back at the card
                Ui.Fill(new Rect(r.x - 10f + i, r.center.y - (1 + i), 1f, 2f * (1 + i)), i == 0 ? violet : new Color(0.2f, 0.1f, 0.38f, 0.96f));
            Ui.Label(r, text, 14, new Color(0.9f, 0.83f, 1f), TextAnchor.MiddleCenter, true);
            if (live && Ui.HitArea(new Rect(r.x - 10f, r.y, r.width + 10f, r.height)))
                OpenProfile();
        }
    }
}

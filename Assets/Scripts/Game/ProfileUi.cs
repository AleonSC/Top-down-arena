using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Your profile as a CRT cabinet (Docs/Mockups/crt-profile.html), the same cabinet, opening and side windows as the pause menu
    /// and Settings (CrtWindows.cs). The cabinet on the left: a "PROFILE_" header screen, a profile screen (rank badge, your name with
    /// a pencil key to rename, title and rank, XP bar), four tab keys and the open tab's options as digital keys, and a physical Back
    /// key. An option opens in a CRT window to the right (it slides out and powers on the first time, later picks only retype it):
    ///   Look     Frame, Trail, Background, Card background, Name effect, Aim line, Cursor (CosmeticsUi.cs: try it on, equip)
    ///   Rank     Progress, Reward ladder, Tiers, XP sources (ProfileWindows.cs, CosmeticsUi.cs)
    ///   Records  Overview, Bosses, Classes, Challenge rooms, Recent runs (MenuDrawer.cs, DrawRecordsSection; RunGraphs.cs)
    ///   Unlocks  every cosmetic kind with checks and padlocks (ProfileWindows.cs)
    /// It opens from your player card on the main menu (OpenProfile). In the lobby and pause menus it opens without the Look tab
    /// (nothing is renamed or equipped mid-run).
    public partial class ArenaApp
    {
        bool profileOpen;
        int profileTab;      // 0 Look, 1 Rank, 2 Records, 3 Unlocks
        int rankSection;     // in the Rank tab: 0 Progress, 1 Reward ladder, 2 Tiers, 3 XP sources
        int unlocksSection;  // in the Unlocks tab: 0..6 the Look kinds, 7 Emotes, 8 Other unlocks

        static readonly string[] ProfileTabs = { "Look", "Rank", "Records", "Unlocks" };
        const int RecordRowCount = 5;  // Overview, Bosses, Classes, Challenge rooms, Recent runs

        /// Open the profile on a tab (0 Look, 1 Rank, 2 Records, 3 Unlocks). Opening it ends the main menu's "your profile" nudge for good.
        void OpenProfile(int tab = 0)
        {
            profileOpen = true;
            profileTab = Mathf.Clamp(tab, 0, ProfileTabs.Length - 1);
            cosmeticsSection = 0;
            rankSection = 0;
            unlocksSection = 0;
            recordsSection = Mathf.Clamp(recordsSection, 0, RecordRowCount - 1);
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

        /// The profile: the window (first, so it comes out from behind the cabinet) and the cabinet.
        void DrawProfileWindow(bool inMenu = false)
        {
            DrawerIn(inMenu ? "pause-profile" : "profile", !inMenu);
            if (inMenu && profileTab == 0)
                profileTab = 1;
            if (inMenu)
                editingName = false;
            DrawCabWindow();  // CrtWindows.cs: DrawProfileSection (ProfileWindows.cs) when an option is open
            DrawProfileCabinet(inMenu);
        }

        /// One key of the cabinet's option list.
        struct ProfOpt
        {
            public string Name, Icon, Line, Tag;
        }

        int ProfSel
        {
            get { return profileTab == 0 ? cosmeticsSection : profileTab == 1 ? rankSection : profileTab == 2 ? recordsSection : unlocksSection; }
            set
            {
                if (profileTab == 0) cosmeticsSection = value;
                else if (profileTab == 1) rankSection = value;
                else if (profileTab == 2) recordsSection = value;
                else unlocksSection = value;
            }
        }

        /// The Unlocks tab's kinds: the seven Look kinds, then Emotes.
        static readonly string[] UnlockKinds = { "Frame", "Trail", "Background", "Card background", "Name effect", "Aim line", "Cursor", "Emotes" };

        /// How many of this kind's cosmetics the rank has reached, and how many there are.
        static void UnlockCount(int rank, CosmeticKind kind, out int got, out int all)
        {
            got = all = 0;
            foreach (var c in Rank.OfKind(kind))
            {
                all++;
                if (rank >= c.Rank)
                    got++;
            }
        }

        /// The open tab's option keys.
        List<ProfOpt> ProfileOptions(Profile p, int tab)
        {
            var l = new List<ProfOpt>();
            if (tab == 0)
            {
                for (int i = 0; i < 7; i++)
                    l.Add(new ProfOpt { Name = CosmeticSections[i], Icon = CosmeticIcons[i], Line = EquippedShort(p, KindOfSection(i)) });
            }
            else if (tab == 1)
            {
                l.Add(new ProfOpt { Name = "Progress", Icon = "ui_star", Line = "Rank, XP and what is next" });
                l.Add(new ProfOpt { Name = "Reward ladder", Icon = "ui_trophy", Line = "What each rank unlocks" });
                l.Add(new ProfOpt { Name = "Tiers", Icon = "ui_card", Line = "Five titles, five colours" });
                l.Add(new ProfOpt { Name = "XP sources", Icon = "ui_book", Line = "What earns XP" });
            }
            else if (tab == 2)
            {
                int bossesBeaten = 0, classWins = 0, roomsWon = 0;
                foreach (var b in RecordBosses)
                    if (p.Beaten(b) > 0)
                        bossesBeaten++;
                foreach (var kv in p.WinsAs)
                    if (kv.Value > 0)
                        classWins++;
                foreach (var c in Challenges.All)
                    roomsWon += p.RoomWon(c);
                l.Add(new ProfOpt { Name = "Overview", Icon = "ui_star", Line = p.RunsStarted + " runs started" });
                l.Add(new ProfOpt { Name = "Bosses", Icon = "ui_skull", Line = bossesBeaten + " of " + RecordBosses.Length + " beaten" });
                l.Add(new ProfOpt { Name = "Classes", Icon = "class_knight", Line = classWins + " won" });
                l.Add(new ProfOpt { Name = "Challenge rooms", Icon = "blink", Line = roomsWon + " won" });
                l.Add(new ProfOpt { Name = "Recent runs", Icon = "ui_book", Line = p.Recent.Count == 0 ? "none yet" : "graphs of the last " + p.Recent.Count });
            }
            else
            {
                int rank = p.RankNow;
                for (int i = 0; i < UnlockKinds.Length; i++)
                {
                    int got, all;
                    UnlockCount(rank, i == 7 ? CosmeticKind.Emote : KindOfSection(i), out got, out all);
                    l.Add(new ProfOpt { Name = UnlockKinds[i], Icon = i == 7 ? "ui_emote" : CosmeticIcons[i], Tag = got + " of " + all });
                }
                if (Unlocks.All.Count > 0)
                {
                    int got = 0;
                    foreach (var u in Unlocks.All)
                        if (p.IsUnlocked(u.Id))
                            got++;
                    l.Add(new ProfOpt { Name = "Other unlocks", Icon = "ui_lock", Tag = got + " of " + Unlocks.All.Count });
                }
            }
            return l;
        }

        /// What you have equipped, in a few words (the kind's name left off: "Ember" for "Ember trail").
        static string EquippedShort(Profile p, CosmeticKind kind)
        {
            var c = Rank.Cosmetic(p.Equipped(kind));
            return c == null ? EquippedName(p, kind) : StripKind(c.Name);
        }

        /// A cosmetic's name without the kind's word at the end ("Ember" for "Ember trail").
        static string StripKind(string n)
        {
            foreach (var suffix in new[] { " frame", " trail", " card", " name", " aim line", " cursor" })
                if (n.EndsWith(suffix, StringComparison.OrdinalIgnoreCase) && n.Length > suffix.Length + 2)
                    return n.Substring(0, n.Length - suffix.Length);
            return n;
        }

        /// A cosmetic's name for a list row `width` wide: in full at 18, smaller down to `minSize`, then without the kind's word,
        /// and only then wrapped onto two lines.
        static string FitCosmeticName(string name, float width, int minSize, out int size, out bool wrap)
        {
            string n = name;
            if (Ui.TextWidth(n, minSize, true) - 6f > width)
                n = StripKind(name);
            size = 18;
            while (size > minSize && Ui.TextWidth(n, size, true) - 6f > width)
                size--;
            wrap = Ui.TextWidth(n, size, true) - 6f > width;
            return n;
        }

        // --- A screen on the cabinet beside the header and the option list ---------------------------------------------------

        bool CabScreenBegin(Rect r, float delay) => CrtKit.PowerOn(r, CabAge - delay, CabCalm);

        void CabScreenEnd(Rect r, float delay)
        {
            CrtUi.End(r, WinNow, CabCalm);
            CrtKit.PowerFlash(r, CabAge - delay, CabCalm);
        }

        /// A small square digital key with a pixel icon (the profile's pencil). True when pressed.
        bool IconKey(Rect r, string icon, bool sel)
        {
            bool hover = GUI.enabled && r.Contains(Event.current.mousePosition);
            Ui.NoteHover(r);
            var c = CrtUi.Ink;
            bool fill = hover || sel;
            Ui.Fill(r, fill ? new Color(c.r, c.g, c.b, 0.95f) : new Color(c.r, c.g, c.b, 0.08f));
            CrtKit.Frame(r, 2f, c);
            Ui.Icon(new Rect(r.center.x - 10f, r.center.y - 10f, 20f, 20f), PixelArt.Get(icon), fill ? CrtUi.Bg : c);
            return Ui.HitArea(r);
        }

        // --- The cabinet ------------------------------------------------------------------------------------------------------

        void DrawProfileCabinet(bool inMenu)
        {
            var p = Profile.Current;
            bool calm = CabCalm;
            var info = Rank.Of(p.Xp);
            var tc = RankLook.TierColor(info.Rank);
            cabBootLog = "> LINK OK   > PROFILE OK   > RANK " + info.Rank;
            var opts = ProfileOptions(p, profileTab);
            int n = opts.Count;
            ProfSel = Mathf.Clamp(ProfSel, 0, n - 1);
            bool oneLine = n > 7;
            float rowH = oneLine ? 29f : 40f, step = rowH + (oneLine ? 3f : 2f);
            BeginDrawer("PROFILE", Ui.Gold, 1f, "Your look, rank and records", 44, new Rect(26f, 314f, 358f, 302f));

            // The profile screen: the badge, your name (a pencil key renames), title and rank, the XP bar.
            var ps = new Rect(26f, 136f, 358f, 120f);
            if (CabScreenBegin(ps, 0.9f))
            {
                var badge = new Rect(ps.x + 12f, ps.y + 8f, 76f, 76f);
                if (!calm)
                    Ui.Pill(new Rect(badge.x - 4f, badge.y + 2f, badge.width + 8f, badge.height - 4f), new Color(tc.r, tc.g, tc.b, 0.07f + 0.07f * (0.5f + 0.5f * Mathf.Sin(WinNow * 2f))));
                RankLook.Draw(badge, info.Rank);
                int nsize = 28;
                float nameW = 190f;
                while (nsize > 14 && Ui.TextWidth(nameText, nsize, true) + 10f > nameW)
                    nsize--;
                var nameBox = new Rect(ps.x + 100f, ps.y + 8f, nameW, 36f);
                if (editingName && !inMenu)
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
                    CrtUi.Text(nameBox, CabText("cab.pname", nameText, 1.2f), nsize, Color.white, TextAnchor.MiddleLeft, true, calm);
                if (!inMenu && IconKey(new Rect(ps.xMax - 14f - 46f, ps.y + 8f, 46f, 34f), "ui_pencil", editingName))
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
                CrtUi.Text(new Rect(ps.x + 100f, ps.y + 50f, 250f, 28f), CabText("cab.ptitle", Rank.Title(info.Rank) + "  ·  Rank " + info.Rank, 1.3f), 22, tc, TextAnchor.MiddleLeft, true, calm);
                var bar = new Rect(ps.x + 14f, ps.y + 94f, ps.width - 28f, 8f);
                Ui.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
                if (!calm)
                    Ui.Fill(new Rect(bar.x - 1f, bar.y - 2f, bar.width * info.Fraction + 2f, bar.height + 4f), new Color(tc.r, tc.g, tc.b, 0.18f));
                Ui.Fill(new Rect(bar.x, bar.y, bar.width * info.Fraction, bar.height), tc);
                Ui.Label(new Rect(ps.x + 14f, ps.y + 102f, ps.width - 28f, 16f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP", 13, CrtUi.Dim, TextAnchor.MiddleRight, true);
                CabScreenEnd(ps, 0.9f);
            }

            // The tab keys.
            var ts = new Rect(26f, 264f, 358f, 42f);
            if (CabScreenBegin(ts, 0.9f))
            {
                int first = inMenu ? 1 : 0, nt = ProfileTabs.Length - first;
                float gap = 5f, tw = (ts.width - 12f - gap * (nt - 1)) / nt;
                for (int i = 0; i < nt; i++)
                {
                    int id = first + i;
                    string label = CabText("cab.tab" + id, ProfileTabs[id].ToUpperInvariant(), 1.15f + i * 0.06f);
                    if (WinKey(new Rect(ts.x + 6f + i * (tw + gap), ts.y + 6f, tw, 30f), label, profileTab == id, 14) && profileTab != id)
                    {
                        profileTab = id;
                        if (CabWindowOpen)
                        {
                            ProfSel = 0;  // the open window swaps to the new tab's first option (its text retypes)
                            OpenCabWindow(3);
                        }
                    }
                }
                CabScreenEnd(ts, 0.9f);
            }

            // The options of the open tab.
            float y = cabScreen.y + 5f;
            for (int i = 0; i < n; i++)
            {
                bool on = cabWin && cabKind == 3 && cabWinOutAt < 0f && ProfSel == i;
                var o = opts[i];
                if (DrawerRow(new Rect(34f, y, 342f, rowH), PixelArt.Get(o.Icon), o.Name, oneLine ? null : o.Line, null, o.Tag, Ui.Accent, on))
                {
                    ProfSel = i;
                    OpenCabWindow(3);  // (the first pick slides it out; later ones only retype it)
                }
                y += step;
            }
            EndDrawerScreen();
            if (cabScreenOn && CrtKit.PhysicalKey(new Rect(40f, 628f, 330f, 52f), "BACK", calm))
            {
                if (CabWindowOpen)
                    CloseCabWindow();  // the window slides back in first
                else if (inMenu)
                    pauseRecords = false;
                else
                    CloseWindow(() => profileOpen = false);
            }
            EndDrawer();
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

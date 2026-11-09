using UnityEngine;

namespace Arena
{
    /// The profile's windows (ProfileUi.cs holds the cabinet): what an option of the open tab shows in the CRT window to the right.
    /// Look options are in CosmeticsUi.cs (DrawLookWindow) and the Reward ladder is DrawLadderPanel; the Records pages are
    /// DrawRecordsSection (MenuDrawer.cs). Here: Rank > Progress, Tiers and XP sources, and the Unlocks tab.
    public partial class ArenaApp
    {
        /// The window of the open option (CrtWindows.cs calls this for a window of kind 3).
        void DrawProfileSection(float t)
        {
            var p = Profile.Current;
            int sel = ProfSel;
            if (profileTab != 1 || sel != 1)
                ladderScroll = -1f;  // the ladder opens at your rank each time
            BeginPanelAnim("profile" + profileTab + "_" + sel);
            switch (profileTab)
            {
                case 0:
                    DrawLookWindow(p, t, Mathf.Clamp(sel, 0, 6));  // CosmeticsUi.cs
                    break;
                case 1:
                    if (sel == 0)
                        DrawRankProgress(p);
                    else if (sel == 1)
                        DrawLadderPanel(p, 1f);  // CosmeticsUi.cs
                    else if (sel == 2)
                        DrawRankTiers(p);
                    else
                        DrawXpSources();
                    break;
                case 2:
                {
                    int bossesBeaten = 0, roomsWon = 0;
                    foreach (var b in RecordBosses)
                        if (p.Beaten(b) > 0)
                            bossesBeaten++;
                    foreach (var c in Challenges.All)
                        roomsWon += p.RoomWon(c);
                    var tc = RankLook.TierColor(p.RankNow);
                    Color[] cols = { Ui.Gold, Ui.Gold, new Color(1f, 0.6f, 0.3f), new Color(0.45f, 0.9f, 1f), Ui.Gold, tc, new Color(0.45f, 0.9f, 1f) };
                    DrawRecordsSection(recordsSection >= 4 ? 6 : recordsSection, p, t, bossesBeaten, roomsWon, cols);  // (row 4 is Recent runs, page 6)
                    break;
                }
                default:
                    DrawUnlocksWindow(p, t, sel);
                    break;
            }
            EndPanelAnim();
        }

        // --- Pieces ------------------------------------------------------------------------------------------------------------

        /// A row of a window: a title and a small line on the left, a value on the right, a dashed line under it.
        void InfoRow(Rect r, string title, string desc, string value, Color valueCol)
        {
            WinLabel(new Rect(r.x, r.y, r.width - 260f, r.height), title, desc);
            CrtUi.Text(new Rect(r.xMax - 280f, r.y, 280f, r.height - 2f), value, 24, valueCol, TextAnchor.MiddleRight, true, CabCalm);
            DashLine(r.x, r.yMax, r.width);
        }

        /// A blocky meter like the sliders', not clickable: `frac` of the blocks lit.
        void BlockBar(Rect r, float frac, Color c, int blocks = 40)
        {
            float gap = 3f, bw = (r.width - gap * (blocks - 1)) / blocks;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(frac) * blocks);
            for (int i = 0; i < blocks; i++)
            {
                var b = new Rect(r.x + i * (bw + gap), r.y, bw, r.height);
                if (i < lit)
                {
                    if (!CabCalm)
                        Ui.Fill(new Rect(b.x - 1f, b.y - 1f, b.width + 2f, b.height + 2f), new Color(c.r, c.g, c.b, 0.2f));
                    Ui.Fill(b, c);
                }
                else
                {
                    Ui.Fill(b, new Color(c.r, c.g, c.b, 0.14f));
                    CrtKit.Frame(b, 1f, new Color(c.r, c.g, c.b, 0.3f));
                }
            }
        }

        /// A small heading inside a window.
        void WinHeading(Rect r, string text)
        {
            Ui.Label(r, WinText("win.h" + cabWinRow++, text), 12, CabSoft, TextAnchor.MiddleLeft, true);
        }

        // --- Rank ------------------------------------------------------------------------------------------------------------

        void DrawRankProgress(Profile p)
        {
            var info = Rank.Of(p.Xp);
            var tc = RankLook.TierColor(info.Rank);
            OpenSectionPanel("PROGRESS", CrtUi.Ink, "Rank " + info.Rank + " of the ladder. Every run earns XP; harder modifiers and difficulties earn more. Rewards are cosmetic.", 1f);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 102f;
            InfoRow(new Rect(x, y, w, 44f), "Rank", null, info.Rank.ToString(), CrtUi.Ink);
            y += 46f;
            InfoRow(new Rect(x, y, w, 44f), "Title", null, Rank.Title(info.Rank), tc);
            y += 46f;
            InfoRow(new Rect(x, y, w, 44f), "XP this rank", p.Xp.ToString("N0") + " XP in total", info.Into.ToString("N0") + " / " + info.Need.ToString("N0"), CrtUi.Ink);
            y += 52f;
            BlockBar(new Rect(x, y, w, 22f), info.Fraction, tc);
            y += 40f;

            var rows = Ladder();  // RankUi.cs
            int shown = 0;
            foreach (var row in rows)
                if (row.Rank > info.Rank)
                    shown++;
            WinHeading(new Rect(x, y, w, 20f), shown > 0 ? "NEXT UNLOCKS" : "EVERYTHING UNLOCKED");
            y += 24f;
            if (shown == 0)
            {
                InfoRow(new Rect(x, y, w, 44f), "You own every reward", "Nothing is left on the ladder", "DONE", Ui.Good);
                return;
            }
            int n = 0;
            foreach (var row in rows)
            {
                if (row.Rank <= info.Rank)
                    continue;
                if (n++ == 5)
                    break;
                InfoRow(new Rect(x, y, w, 44f), row.Name, row.Kind, "RANK " + row.Rank, CrtUi.Dim);
                y += 46f;
            }
        }

        static readonly int[] TierStarts = { 1, 10, 20, 30, 40 };

        void DrawRankTiers(Profile p)
        {
            int rank = p.RankNow, mine = Rank.Tier(rank);
            OpenSectionPanel("TIERS", CrtUi.Ink, "Five titles. Your colour and the chevrons on your badge come from your tier.", 1f);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 102f;
            for (int i = 0; i < Rank.TierNames.Length; i++)
            {
                var tc = RankLook.TierColor(TierStarts[i]);
                bool you = i == mine;
                var r = new Rect(x, y, w, 84f);
                Ui.Fill(r, you ? new Color(tc.r, tc.g, tc.b, 0.1f) : new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.04f));
                CrtKit.Frame(r, 1f, you ? tc : new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.25f));
                RankLook.Draw(new Rect(r.x + 14f, r.y + 10f, 56f, 56f), TierStarts[i], you || rank >= TierStarts[i] ? 1f : 0.6f);
                string range = i + 1 < TierStarts.Length ? "Ranks " + TierStarts[i] + " to " + (TierStarts[i + 1] - 1) : "Rank " + TierStarts[i] + " and up";
                CrtUi.Text(new Rect(r.x + 92f, r.y + 10f, 360f, 36f), WinText("win.tier" + i, Rank.TierNames[i]), 28, tc, TextAnchor.MiddleLeft, true, CabCalm);
                Ui.Label(new Rect(r.x + 92f, r.y + 48f, 360f, 20f), range + "  ·  " + (i + 1) + (i == 0 ? " chevron" : " chevrons"), 14, CabSoft, TextAnchor.MiddleLeft);
                if (you)
                    WinKey(new Rect(r.xMax - 20f - 150f, r.y + 24f, 150f, 36f), "< YOU", false, 20, tc, false, true, true);
                else
                    Ui.Label(new Rect(r.xMax - 20f - 150f, r.y + 24f, 150f, 36f), rank >= TierStarts[i] ? "reached" : "rank " + TierStarts[i], 15, rank >= TierStarts[i] ? Ui.Good : CrtUi.Dim, TextAnchor.MiddleRight, true);
                y += 92f;
            }
        }

        void DrawXpSources()
        {
            OpenSectionPanel("XP SOURCES", CrtUi.Ink, "What a finished run is worth, before the difficulty and modifier bonuses.", 1f);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, y = DrawerPanel.y + 102f;
            InfoRow(new Rect(x, y, w, 56f), "Waves cleared", "Every wave you get through", "+" + Rank.XpPerWave + " each", CrtUi.Ink);
            y += 58f;
            InfoRow(new Rect(x, y, w, 56f), "Bosses defeated", "Each boss that goes down", "+" + Rank.XpPerBoss + " each", CrtUi.Ink);
            y += 58f;
            InfoRow(new Rect(x, y, w, 56f), "Allies revived", "Standing by a downed teammate", "+" + Rank.XpPerRevive + " each", CrtUi.Ink);
            y += 58f;
            InfoRow(new Rect(x, y, w, 56f), "Victory", "Finishing a run alive", "+" + Rank.XpWin, CrtUi.Ink);
            y += 58f;
            InfoRow(new Rect(x, y, w, 56f), "Difficulty", "Easy x0.75, Normal x1, Hard x1.25, Nightmare x1.5", "x0.75 to x1.5", CabWarm);
            y += 58f;
            InfoRow(new Rect(x, y, w, 56f), "Modifiers", "Each harder modifier adds " + Mathf.RoundToInt(Rank.HarderBonus * 100f) + "%, each risky one " + Mathf.RoundToInt(Rank.RiskyBonus * 100f) + "%", "up to x" + Rank.ModCap.ToString("0"), CabWarm);
        }

        // --- Unlocks ---------------------------------------------------------------------------------------------------------

        void DrawUnlocksWindow(Profile p, float t, int sel)
        {
            int rank = p.RankNow;
            sel = Mathf.Clamp(sel, 0, UnlockKinds.Length);
            float x = DrawerPanel.x + 28f, w = DrawerPanel.width - 56f, top = DrawerPanel.y + 102f;
            if (sel >= UnlockKinds.Length)
            {
                DrawOtherUnlocks(p, x, top, w);
                return;
            }
            var kind = sel == 7 ? CosmeticKind.Emote : KindOfSection(sel);
            var items = Rank.OfKind(kind);
            items.Sort((a, b) => a.Rank.CompareTo(b.Rank));
            int got = 0;
            foreach (var c in items)
                if (rank >= c.Rank)
                    got++;
            OpenSectionPanel(UnlockKinds[sel].ToUpperInvariant(), CrtUi.Ink, got + " of " + items.Count + " unlocked at rank " + rank + ". Ranks unlock more.", 1f);
            float cw = (w - 14f) / 2f;
            var ink = CrtUi.Ink;
            for (int i = 0; i < items.Count; i++)
            {
                var c = items[i];
                bool ok = rank >= c.Rank;
                var rr = new Rect(x + (i % 2) * (cw + 14f), top + (i / 2) * 58f, cw, 54f);
                Ui.Fill(rr, new Color(ink.r, ink.g, ink.b, ok ? 0.05f : 0.025f));
                CrtKit.Frame(rr, 1f, new Color(ink.r, ink.g, ink.b, ok ? 0.3f : 0.18f));
                var th = new Rect(rr.x + 8f, rr.y + 7f, 64f, 40f);
                if (kind == CosmeticKind.Emote)
                {
                    Ui.Fill(th, new Color(0.05f, 0.07f, 0.12f, 0.95f));
                    Ui.Icon(new Rect(th.center.x - 14f, th.center.y - 14f, 28f, 28f), PixelArt.Get("ui_emote"), ok ? Color.white : new Color(1f, 1f, 1f, 0.4f));
                }
                else
                    RowThumb(th, kind, c, p, t, ok);
                CrtKit.Frame(th, 1f, new Color(ink.r, ink.g, ink.b, ok ? 0.5f : 0.25f));
                float nx = rr.x + 82f, nw = rr.width - 82f - 44f;
                int size;
                bool wrap;
                string cname = FitCosmeticName(c.Name, nw, 15, out size, out wrap);
                string shown = WinText("win.u" + sel + "." + c.Id, cname);
                if (wrap)
                    CrtUi.Text(new Rect(nx, rr.y + 2f, nw, 32f), shown, 14, ok ? CrtUi.Ink : CrtUi.Dim, TextAnchor.UpperLeft, true, CabCalm, true);
                else
                    CrtUi.Text(new Rect(nx, rr.y + 4f, nw, 24f), shown, size, ok ? CrtUi.Ink : CrtUi.Dim, TextAnchor.MiddleLeft, true, CabCalm);
                Ui.Label(new Rect(nx, rr.y + 31f, nw, 18f), ok ? "unlocked" : "locked: rank " + c.Rank, 13, ok ? Ui.Good : CrtUi.Dim, TextAnchor.MiddleLeft);
                Ui.Icon(new Rect(rr.xMax - 32f, rr.center.y - 11f, 22f, 22f), PixelArt.Get(ok ? "ui_check" : "ui_lock"), ok ? Ui.Good : CrtUi.Dim);
            }
        }

        /// The conditional unlocks (Profile.cs, Unlocks.All): a run feat opens a lobby choice.
        void DrawOtherUnlocks(Profile p, float x, float top, float w)
        {
            int got = 0;
            foreach (var u in Unlocks.All)
                if (p.IsUnlocked(u.Id))
                    got++;
            OpenSectionPanel("OTHER UNLOCKS", CrtUi.Ink, got + " of " + Unlocks.All.Count + " unlocked. Each opens up when its condition is met.", 1f);
            var ink = CrtUi.Ink;
            float uy = top;
            foreach (var u in Unlocks.All)
            {
                bool on = p.IsUnlocked(u.Id);
                var rr = new Rect(x, uy, w, 62f);
                Ui.Fill(rr, new Color(ink.r, ink.g, ink.b, on ? 0.05f : 0.025f));
                CrtKit.Frame(rr, 1f, new Color(ink.r, ink.g, ink.b, on ? 0.3f : 0.18f));
                IconTile(new Rect(rr.x + 8f, rr.y + 8f, 46f, 46f), PixelArt.Get(on ? "ui_star" : "ui_lock"), on ? Ui.Gold : CrtUi.Dim, 26f);
                CrtUi.Text(new Rect(rr.x + 68f, rr.y + 6f, w - 200f, 26f), WinText("win.o" + u.Id, u.Name), 20, on ? Ui.Gold : CrtUi.Dim, TextAnchor.MiddleLeft, true, CabCalm);
                Ui.Label(new Rect(rr.x + 68f, rr.y + 33f, w - 84f, 22f), u.Desc, 13, CabSoft, TextAnchor.MiddleLeft);
                if (on)
                    Ui.Label(new Rect(rr.xMax - 130f, rr.y + 6f, 118f, 26f), "UNLOCKED", 14, Ui.Good, TextAnchor.MiddleRight, true);
                uy += 70f;
            }
        }
    }
}

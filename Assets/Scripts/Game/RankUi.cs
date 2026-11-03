using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Where the account rank shows (Docs/Rank.md, Docs/Mockups/rank-screens.html): the strip at the foot of the run's results,
    /// the hover card on a badge, and the Rank section of Records with the reward ladder and the equipped cosmetics.
    public partial class ArenaApp
    {
        // --- End of run ---------------------------------------------------------------------------------------

        /// The rank strip under the results cards: the badge, the XP bar filling by what the run earned (k 0..1 = how far it has
        /// played), the total, how it was worked out, and a RANK UP! with what it unlocked.
        void DrawRankStrip(Rect r, float k, float now)
        {
            var g = game.LastGain;
            if (g == null)
                return;
            var info = Rank.Of(g.Before + (long)(g.Award.Total * Ease(k)));
            bool up = info.Rank > g.RankBefore;
            var col = RankLook.TierColor(info.Rank);
            Ui.Card(r, up ? Ui.Gold : col, up ? 0.9f : 0.55f);

            float pop = up ? 1f + 0.18f * Mathf.Max(0f, Mathf.Sin(now * 6f)) * Mathf.Clamp01(1f - (k - 0.9f) * 6f) : 1f;
            float bs = 34f * pop;
            RankLook.Draw(new Rect(r.x + 10f + (34f - bs) / 2f, r.y + 5f + (34f - bs) / 2f, bs, bs), info.Rank);
            Ui.Label(new Rect(r.x + 54f, r.y + 4f, 150f, 18f), "RANK " + info.Rank, 12, Color.Lerp(col, Color.white, 0.4f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 54f, r.y + 22f, 150f, 18f), Rank.Title(info.Rank), 13, col, TextAnchor.MiddleLeft, true);

            Ui.Bar(new Rect(r.x + 214f, r.y + 15f, 220f, 14f), info.Fraction, col);
            Ui.Label(new Rect(r.x + 442f, r.y + 8f, 110f, 28f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0"), 12, Ui.Text, TextAnchor.MiddleLeft, true);
            Ui.Fancy(new Rect(r.x + 556f, r.y + 6f, 140f, 32f), "+" + g.Award.Total.ToString("N0") + " XP", 20, Color.white, Ui.Gold, TextAnchor.MiddleLeft);

            // How it was worked out (line 1), then the modifiers behind the multiplier, or on a rank up what it unlocked (line 2).
            var parts = new List<string>();
            foreach (var l in g.Award.Lines)
                parts.Add(l.Label.Split(' ')[0] + " " + l.Xp);
            string how = string.Join("  ·  ", parts.ToArray());
            float mult = g.Award.ModMult * g.Award.DiffMult;
            if (Mathf.Abs(mult - 1f) > 0.001f)
                how += "  ×" + mult.ToString("0.##");
            float tx = r.x + 706f, tw = r.width - 716f;
            DrawOneLine(new Rect(tx, r.y + 4f, tw, 18f), how, 12, 10, Ui.Text, false);
            if (up)
            {
                string line = "RANK UP!";
                if (g.Unlocked.Count > 0)
                {
                    var names = new List<string>();
                    foreach (var c in g.Unlocked)
                        names.Add(c.Name);
                    line += "  Unlocked: " + string.Join(", ", names.ToArray());
                }
                else if (Rank.Title(g.RankAfter) != Rank.Title(g.RankBefore))
                    line += "  New title: " + Rank.Title(g.RankAfter);
                DrawOneLine(new Rect(tx, r.y + 22f, tw, 18f), line, 12, 10, Ui.Gold, true);
            }
            else if (g.Award.ModNotes.Count > 0)
                DrawOneLine(new Rect(tx, r.y + 22f, tw, 18f), string.Join(", ", g.Award.ModNotes.ToArray()), 11, 9, new Color(0.78f, 0.65f, 1f), false);
        }

        /// A player's rank for the results cards: yours from the profile (it's just been paid out), the others' as the lobby knew it.
        int RankOfPlayer(Player p)
        {
            if (p.Id == game.MyId)
                return Profile.Current.RankNow;
            LobbyEntry e;
            return session.Lobby.TryGetValue(p.Id, out e) ? e.Rank : 1;
        }

        // --- Hover card ---------------------------------------------------------------------------------------

        /// Hovering a rank badge: its title and rank, and (for yours) the XP towards the next. Call between TipClear and DrawTip.
        void RankBadgeTip(Rect r, int rank, bool mine)
        {
            if (!r.Contains(Event.current.mousePosition))
                return;
            var col = RankLook.TierColor(rank);
            TipStart(r, Rank.Title(rank), Color.Lerp(col, Color.white, 0.3f));
            TipAdd("RANK " + rank, 10, col, true);
            if (mine)
            {
                var info = Rank.Of(Profile.Current.Xp);
                TipAdd(info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP to rank " + (rank + 1), 12, Ui.Text);
            }
        }

        // --- Records: the Rank section -------------------------------------------------------------------------

        /// One row of the reward ladder: a title that comes with a tier, or a cosmetic.
        class LadderRow
        {
            public int Rank;
            public string Name, Kind;
            public CosmeticDef Cosmetic;  // null for a title
        }

        static List<LadderRow> Ladder()
        {
            var rows = new List<LadderRow>();
            for (int t = 1; t < Rank.TierNames.Length; t++)
            {
                int at = new[] { 0, 5, 10, 20, 30 }[t];
                rows.Add(new LadderRow { Rank = at, Name = Rank.TierNames[t], Kind = "Title" });
            }
            foreach (var c in Rank.Cosmetics)
                rows.Add(new LadderRow { Rank = c.Rank, Name = c.Name, Kind = c.Kind.ToString(), Cosmetic = c });
            rows.Sort((a, b) => a.Rank != b.Rank ? a.Rank.CompareTo(b.Rank) : string.CompareOrdinal(a.Kind, b.Kind));
            return rows;
        }

        void DrawRankRecords(Profile p, float t, Color col, float k)
        {
            var info = Rank.Of(p.Xp);
            var tc = RankLook.TierColor(info.Rank);
            OpenSectionPanel("RANK", col, "Earned from every run. Harder modifiers and difficulties give more XP. Rewards are cosmetic.", k);
            float x = DrawerPanel.x + 26f, top = DrawerPanel.y + 112f;
            float lw = 230f, mw = 262f, gap = 14f, rw = DrawerPanel.width - 52f - lw - mw - 2f * gap;

            // You.
            RankLook.Draw(new Rect(x + (lw - 112f) / 2f, top, 112f, 112f), info.Rank);
            Ui.Fancy(new Rect(x, top + 122f, lw, 34f), Rank.Title(info.Rank), 24, Color.white, Color.Lerp(tc, Color.white, 0.4f), TextAnchor.MiddleCenter, new Color(tc.r, tc.g, tc.b, 0.5f));
            Ui.Label(new Rect(x, top + 156f, lw, 20f), "Rank " + info.Rank, 14, tc, TextAnchor.MiddleCenter, true);
            Ui.Bar(new Rect(x + 8f, top + 184f, lw - 16f, 14f), info.Fraction, tc);
            Ui.Label(new Rect(x, top + 202f, lw, 18f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP", 12, Ui.Text, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(x, top + 220f, lw, 18f), p.Xp.ToString("N0") + " XP in total", 11, Ui.Muted, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(x, top + 262f, lw, 16f), "EQUIPPED", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            string[,] eq = { { "Title", Rank.Title(info.Rank) }, { "Frame", NameOf(p.Frame) }, { "Trail", NameOf(p.Trail) }, { "Background", NameOf(p.Background) } };
            for (int i = 0; i < 4; i++)
            {
                var rr = new Rect(x, top + 284f + i * 36f, lw, 30f);
                Ui.Pill(rr, new Color(1f, 1f, 1f, 0.04f));
                Ui.Label(new Rect(rr.x + 10f, rr.y, 90f, rr.height), eq[i, 0], 12, Ui.Muted, TextAnchor.MiddleLeft);
                Ui.Label(new Rect(rr.x + 90f, rr.y, rr.width - 100f, rr.height), eq[i, 1], 12, eq[i, 1] == "none" ? Ui.Muted : Color.white, TextAnchor.MiddleRight, true);
            }

            // The ladder.
            float mx = x + lw + gap;
            Ui.Label(new Rect(mx, top - 4f, mw, 16f), "REWARDS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            var rows = Ladder();
            int nextAt = 0;
            foreach (var row in rows)
                if (row.Rank > info.Rank) { nextAt = row.Rank; break; }
            float y = top + 16f;
            foreach (var row in rows)
            {
                bool got = row.Rank <= info.Rank, next = row.Rank == nextAt;
                var rr = new Rect(mx, y, mw, 44f);
                Ui.Pill(rr, next ? new Color(1f, 0.8f, 0.35f, 0.12f) : new Color(1f, 1f, 1f, got ? 0.05f : 0.025f));
                RankLook.Draw(new Rect(rr.x + 6f, rr.y + 6f, 32f, 32f), row.Rank, got ? 1f : 0.5f);
                Ui.Label(new Rect(rr.x + 46f, rr.y + 4f, rr.width - 130f, 20f), row.Name, 14, got ? Color.white : Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(rr.x + 46f, rr.y + 23f, rr.width - 130f, 16f), row.Kind + (got ? "" : next ? "  ·  next" : ""), 11, got ? Ui.Good : next ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
                if (row.Cosmetic != null && got)
                {
                    bool on = p.Equipped(row.Cosmetic.Kind) == row.Cosmetic.Id;
                    if (Ui.SmallButton(new Rect(rr.xMax - 84f, rr.y + 9f, 76f, 26f), on ? "On" : "Equip", on ? Ui.GoldButton : Ui.Neutral, 12))
                    {
                        if (row.Cosmetic.Kind == CosmeticKind.Background)
                            SetBackground(on ? 0 : DuskGridIndex);  // the menu background picker (MenuBackgrounds.cs); it keeps the profile too
                        else
                        {
                            p.Equip(row.Cosmetic.Kind, on ? "" : row.Cosmetic.Id);
                            p.Save();
                            session.ShareCosmetics();
                        }
                    }
                }
                y += 48f;
            }

            // Recent runs.
            float rx = mx + mw + gap;
            Ui.Label(new Rect(rx, top - 4f, rw, 16f), "RECENT RUNS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (p.Recent.Count == 0)
                Ui.Label(new Rect(rx, top + 16f, rw, 60f), "Finish a run to earn your first XP.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y = top + 16f;
            foreach (var l in p.Recent)
            {
                var rr = new Rect(rx, y, rw, 70f);
                Ui.Pill(rr, new Color(1f, 1f, 1f, 0.04f));
                bool hunt = l.Result >= 2, won = l.Result == 1 || l.Result == 3;
                Ui.Label(new Rect(rr.x + 10f, rr.y + 4f, rr.width - 90f, 20f), (hunt ? "Hunt" : "Wave " + l.Wave) + (won ? "  ·  won" : ""), 13, won ? Ui.Gold : Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(rr.xMax - 90f, rr.y + 4f, 80f, 20f), "+" + l.Xp.ToString("N0"), 13, Ui.Gold, TextAnchor.MiddleRight, true);
                string mods = l.Mods.Replace("|", ", ");
                Ui.Label(new Rect(rr.x + 10f, rr.y + 26f, rr.width - 20f, 40f), l.MultPct != 100 ? "×" + (l.MultPct / 100f).ToString("0.##") + (mods.Length > 0 ? "  " + mods : "") : "No modifiers", 11,
                    l.MultPct != 100 ? new Color(0.78f, 0.65f, 1f) : Ui.Muted, TextAnchor.UpperLeft, false, true);
                y += 74f;
            }
        }

        static string NameOf(string cosmeticId)
        {
            var c = Rank.Cosmetic(cosmeticId);
            return c == null ? "none" : c.Name;
        }
    }
}

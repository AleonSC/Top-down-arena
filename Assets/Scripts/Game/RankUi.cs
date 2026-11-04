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

        // --- The generic player ---------------------------------------------------------------------------

        /// GL (inside BeginUiGl, after UiModelAt): a neutral top-down figure for the menu chip: no role, class or weapon, just a
        /// body, shoulders, two hands out front and a head, turning slowly. The costume colours come from cosmetics later.
        void DrawGenericPlayer(float t)
        {
            float a = -0.5f + 0.9f * Mathf.Sin(t * 0.7f), c = Mathf.Cos(a), s = Mathf.Sin(a);
            System.Func<float, float, V2> at = (x, y) => new V2(x * c - y * s, x * s + y * c);  // along the aim, to the right
            var skin = new Rgba(0.86f, 0.88f, 0.95f);
            var cloth = new Rgba(0.5f, 0.58f, 0.76f);
            Circle(V2.Zero, 21f, new Rgba(0.06f, 0.08f, 0.14f, 0.9f), 24);
            Line(at(2f, -11f), at(24f, -7f), 5f, cloth);   // arms out to the hands
            Line(at(2f, 11f), at(24f, 7f), 5f, cloth);
            Circle(at(24f, -7f), 3.4f, skin, 8);
            Circle(at(24f, 7f), 3.4f, skin, 8);
            Circle(V2.Zero, 17f, cloth, 24);                // the body
            Circle(at(-1f, -11f), 7f, new Rgba(0.62f, 0.7f, 0.88f), 12);  // shoulders
            Circle(at(-1f, 11f), 7f, new Rgba(0.62f, 0.7f, 0.88f), 12);
            Circle(at(1f, 0f), 9f, skin, 16);               // the head
            Line(at(5f, -4f), at(5f, 4f), 2f, new Rgba(0.25f, 0.3f, 0.45f));  // a visor line, so you can tell which way it faces
        }

        // --- The rank report (the results screen's second page) ----------------------------------------------------

        bool resultsRankPage;     // the results show the rank report instead of the stat cards
        float rankPageAt;         // when it was opened (its rows tick in and its bar fills from then)
        RankGain rankGainSeen;    // which run's gain the page choice belongs to

        void OpenRankReport(float now)
        {
            resultsRankPage = true;
            rankPageAt = now;
        }

        /// The full report (Docs/Mockups/rank-screens.html, screen 1): your badge and bar with the XP breakdown ticking in under
        /// it, the rank-up, your party's ranks, and the reward you've just earned or the next one to go for. rt is the seconds
        /// since the page opened.
        void DrawRankReport(Rect r, float rt)
        {
            var g = game.LastGain;
            if (g == null)
                return;
            float now = Time.unscaledTime;
            const float RowAt = 0.45f, RowGap = 0.32f;

            // The breakdown, as rows.
            var rows = new List<string[]>();  // label, detail, value, kind (0 normal, 1 muted, 2 modifier, 3 total)
            foreach (var l in g.Award.Lines)
                rows.Add(new[] { l.Label, l.Detail, l.Xp.ToString("N0"), "0" });
            bool hasMult = Mathf.Abs(g.Award.ModMult - 1f) > 0.001f, hasDiff = Mathf.Abs(g.Award.DiffMult - 1f) > 0.001f;
            if (hasMult || hasDiff)
                rows.Add(new[] { "Subtotal", "", g.Award.Subtotal.ToString("N0"), "1" });
            if (hasMult)
                rows.Add(new[] { "Modifiers", string.Join(", ", g.Award.ModNotes.ToArray()), "×" + g.Award.ModMult.ToString("0.##"), "2" });
            if (hasDiff)
                rows.Add(new[] { "Difficulty", "", "×" + g.Award.DiffMult.ToString("0.##"), "2" });
            rows.Add(new[] { "Total", "", "+" + g.Award.Total.ToString("N0") + " XP", "3" });
            float barAt = RowAt + RowGap * rows.Count + 0.25f;
            float k = Seg(rt, barAt, barAt + 1.7f);
            var info = Rank.Of(g.Before + (long)(g.Award.Total * Ease(k)));
            bool up = info.Rank > g.RankBefore;
            var col = RankLook.TierColor(info.Rank);

            // Left: you.
            var left = new Rect(r.x, r.y, 700f, r.height);
            Ui.Card(left, up ? Ui.Gold : col, up ? 0.9f : 0.6f);
            Ui.Label(new Rect(left.x + 26f, left.y + 16f, 300f, 16f), "YOUR RANK", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            float crossAt = barAt + 1.7f * 0.5f, since = rt - crossAt;
            float pop = up ? 1f + 0.22f * Mathf.Max(0f, Mathf.Sin(Mathf.Clamp(since * 4f, 0f, Mathf.PI))) : 1f;
            float bs = 112f * pop;
            RankLook.Draw(new Rect(left.x + 26f + (112f - bs) / 2f, left.y + 40f + (112f - bs) / 2f, bs, bs), info.Rank);
            Ui.Fancy(new Rect(left.x + 156f, left.y + 40f, 380f, 36f), nameText, 28, Color.white, Color.Lerp(col, Color.white, 0.4f), TextAnchor.MiddleLeft, new Color(col.r, col.g, col.b, 0.4f));
            Ui.Label(new Rect(left.x + 156f, left.y + 78f, 320f, 22f), Rank.Title(info.Rank) + "  ·  Rank " + info.Rank, 16, col, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(left.xMax - 226f, left.y + 78f, 200f, 22f), info.Into.ToString("N0") + " / " + info.Need.ToString("N0") + " XP", 14, Ui.Text, TextAnchor.MiddleRight, true);
            Ui.Bar(new Rect(left.x + 156f, left.y + 112f, left.width - 182f, 20f), info.Fraction, col);
            if (up && since > 0f && since < 0.5f)  // a flash across the bar as the rank turns over
                Ui.Fill(new Rect(left.x + 156f, left.y + 112f, left.width - 182f, 20f), new Color(1f, 1f, 1f, 0.5f * (1f - since / 0.5f)));
            if (up && since > 0f)
                Ui.Fancy(new Rect(left.x + 330f, left.y + 36f, 340f, 40f), "RANK UP!", 30, Color.white, Ui.Gold, TextAnchor.MiddleRight, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.6f * Mathf.Clamp01(since * 3f)));

            // The breakdown ticking in.
            float y = left.y + 158f;
            for (int i = 0; i < rows.Count; i++)
            {
                float a = Seg(rt, RowAt + RowGap * i, RowAt + RowGap * i + 0.3f);
                if (a <= 0f)
                    continue;
                var row = rows[i];
                int kind = int.Parse(row[3]);
                var rr = new Rect(left.x + 20f + 24f * (1f - Ease(a)) * -1f, y + i * 34f, left.width - 40f, 30f);
                int ri = i;
                WithAlpha(a, () =>
                {
                    if (kind == 3)
                        Ui.Pill(rr, new Color(1f, 0.8f, 0.35f, 0.12f));
                    else if (kind == 2)
                        Ui.Pill(rr, new Color(0.7f, 0.45f, 1f, 0.12f));
                    Ui.Label(new Rect(rr.x + 14f, rr.y, 230f, rr.height), row[0], kind == 3 ? 18 : 15, kind == 3 ? Ui.Gold : kind == 1 ? Ui.Muted : Ui.Text, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(rr.x + 230f, rr.y, rr.width - 360f, rr.height), row[1], 12, kind == 2 ? new Color(0.78f, 0.65f, 1f) : Ui.Muted, TextAnchor.MiddleLeft);
                    Ui.Label(new Rect(rr.xMax - 150f, rr.y, 136f, rr.height), row[2], kind == 3 ? 20 : 16, kind == 3 ? Ui.Gold : kind == 2 ? new Color(0.78f, 0.65f, 1f) : Color.white, TextAnchor.MiddleRight, true);
                });
            }

            // Right, top: the party's ranks.
            var party = new Rect(r.x + 720f, r.y, r.width - 720f, 196f);
            Ui.Card(party, Ui.Accent, 0.5f);
            Ui.Label(new Rect(party.x + 22f, party.y + 14f, 200f, 16f), "PARTY", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            float py = party.y + 38f;
            foreach (var pl in game.Players.Values)
            {
                if (py + 46f > party.yMax)
                    break;
                int pr = pl.Id == game.MyId ? info.Rank : RankOfPlayer(pl);
                var pc = ToColor(pl.Def.Color);
                RankLook.Draw(new Rect(party.x + 22f, py, 40f, 40f), pr);
                Ui.Label(new Rect(party.x + 74f, py - 2f, party.width - 100f, 24f), pl.Name + (pl.Id == game.MyId ? "  (you)" : ""), 16, Color.Lerp(pc, Color.white, 0.25f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(party.x + 74f, py + 20f, party.width - 100f, 18f), Rank.Title(pr) + "  ·  Rank " + pr, 12, RankLook.TierColor(pr), TextAnchor.MiddleLeft, true);
                py += 50f;
            }

            // Right, bottom: the reward you earned, or the next one.
            var rw = new Rect(party.x, party.yMax + 16f, party.width, r.yMax - party.yMax - 16f);
            Ui.Card(rw, Ui.Gold, 0.4f);
            LadderRow shown = null;
            bool earned = false;
            if (g.Unlocked.Count > 0)
            {
                earned = true;
                shown = new LadderRow { Rank = g.Unlocked[0].Rank, Name = g.Unlocked[0].Name, Kind = g.Unlocked[0].Kind.ToString(), Cosmetic = g.Unlocked[0] };
            }
            else if (g.RankedUp && Rank.Title(g.RankAfter) != Rank.Title(g.RankBefore))
            {
                earned = true;
                shown = new LadderRow { Rank = g.RankAfter, Name = Rank.Title(g.RankAfter), Kind = "Title" };
            }
            else
                foreach (var row in Ladder())
                    if (row.Rank > g.RankAfter) { shown = row; break; }
            bool got = earned && up;  // shown once the bar has crossed
            Ui.Label(new Rect(rw.x + 22f, rw.y + 14f, 300f, 16f), earned ? (got ? "NEW REWARD" : "REWARD AT RANK " + shown.Rank) : "NEXT REWARD", 11, earned && got ? Ui.Gold : Ui.Muted, TextAnchor.MiddleLeft, true);
            if (shown == null)
                Ui.Label(new Rect(rw.x + 22f, rw.y + 60f, rw.width - 44f, 40f), "You've earned every reward.", 15, Ui.Text, TextAnchor.UpperLeft, true, true);
            else
            {
                DrawRewardPreview(new Rect(rw.x + 22f, rw.y + 38f, rw.width - 44f, 110f), shown, now, !(earned && got));
                Ui.Fancy(new Rect(rw.x + 22f, rw.y + 154f, rw.width - 44f, 32f), shown.Name, 22, Color.white, earned && got ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter);
                string line = earned && got ? shown.Kind + ". Equip it from your name chip on the main menu."
                    : shown.Kind + "  ·  rank " + shown.Rank + "  ·  " + Mathf.Max(0L, Rank.XpAt(shown.Rank) - (g.Before + (long)(g.Award.Total * Ease(k)))).ToString("N0") + " XP to go";
                Ui.Label(new Rect(rw.x + 22f, rw.y + 188f, rw.width - 44f, 36f), line, 12, Ui.Muted, TextAnchor.UpperCenter, false, true);
            }
        }

        /// A picture of a reward in a box: a frame's rim round the figure, a trail behind it, a background, or a title in words.
        void DrawRewardPreview(Rect b, LadderRow row, float t, bool dim)
        {
            Ui.Pill(b, new Color(0.04f, 0.05f, 0.09f, 0.55f));
            if (row.Cosmetic == null)
            {
                int rk = row.Rank;
                Ui.Fancy(b, row.Name, 34, Color.white, RankLook.TierColor(rk), TextAnchor.MiddleCenter, new Color(0.4f, 0.5f, 0.8f, 0.3f));
            }
            else if (row.Cosmetic.Kind == CosmeticKind.Background)
            {
                if (BeginUiGl())
                {
                    int keep = menuBg;
                    menuBg = DuskGridIndex;
                    BackgroundThumb(b, BackgroundAccents[DuskGridIndex], 0.4f);
                    menuBg = keep;
                    EndUiGl();
                }
            }
            else
            {
                if (row.Cosmetic.Kind == CosmeticKind.Frame)
                    FrameOutline(new Rect(b.x + b.width * 0.3f, b.y + 14f, b.width * 0.4f, b.height - 28f), RankLook.Tint(Rank.FrameColor(Rank.FrameIndex(row.Cosmetic.Id))));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(b.center.x + (row.Cosmetic.Kind == CosmeticKind.Trail ? 40f : 0f), b.center.y), 1.3f);
                    if (row.Cosmetic.Kind == CosmeticKind.Trail)
                        DrawTrailSample(Rank.TrailIndex(row.Cosmetic.Id), 80f, t, 12f);
                    DrawGenericPlayer(t);
                    EndUiGl();
                }
            }
            if (dim)
            {
                Ui.Fill(b, new Color(0.02f, 0.03f, 0.06f, 0.55f));
                Ui.Icon(new Rect(b.center.x - 14f, b.center.y - 14f, 28f, 28f), PixelArt.Get("ui_lock"), Ui.Muted);
            }
        }

        // --- Dev tool ----------------------------------------------------------------------------------------------

        /// F1 > Tools > Account rank: set this computer's rank straight away (1 to Rank.MaxRank). Cosmetics the new rank hasn't
        /// reached come off, and the lobby is told.
        void DevSetRank(int rank)
        {
            var p = Profile.Current;
            p.Xp = Rank.XpAt(Mathf.Clamp(rank, 1, Rank.MaxRank));
            foreach (CosmeticKind kind in System.Enum.GetValues(typeof(CosmeticKind)))
                if (!Rank.Owns(p.RankNow, p.Equipped(kind)))
                {
                    if (kind == CosmeticKind.Background)
                        SetDuskGridEquipped(false);  // back to your own pick (MenuBackgrounds.cs)
                    else
                        p.Equip(kind, "");
                }
            p.Save();
            session.ShareCosmetics();
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
                            SetDuskGridEquipped(!on);  // CosmeticsUi.cs; it keeps the profile too
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

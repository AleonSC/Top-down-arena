using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The end-of-run summary: the run's results and each player's card (SummaryRows).
    public partial class ArenaApp
    {
        // --- End of run ---------------------------------------------------------

        struct SummaryRow
        {
            public string Label, Award;
            public Func<RunStats, float> Value;
            public Func<RunStats, string> Format;
        }

        static readonly SummaryRow[] SummaryRows =
        {
            new SummaryRow { Label = "Damage dealt", Award = "Most damage", Value = s => s.DamageDealt, Format = s => Big(s.DamageDealt) },
            new SummaryRow { Label = "Kills", Award = "Most kills", Value = s => s.Kills, Format = s => s.Kills + (s.EliteKills + s.BossKills > 0 ? "  (" + s.EliteKills + " elite, " + s.BossKills + " boss)" : "") },
            new SummaryRow { Label = "Biggest hit", Award = "Heavy hitter", Value = s => s.BiggestHit, Format = s => Big(s.BiggestHit) },
            new SummaryRow { Label = "Accuracy", Award = "Sharpshooter", Value = s => s.Accuracy, Format = s => Mathf.RoundToInt(s.Accuracy * 100f) + "%" },
            new SummaryRow { Label = "Healing done", Award = "Best healer", Value = s => s.HealingDone, Format = s => Big(s.HealingDone) },
            new SummaryRow { Label = "Allies revived", Award = "Lifesaver", Value = s => s.Revives, Format = s => s.Revives.ToString() },
            new SummaryRow { Label = "Damage taken", Award = "Damage sponge", Value = s => s.DamageTaken, Format = s => Big(s.DamageTaken) },
            new SummaryRow { Label = "Times downed", Award = null, Value = s => s.Downs, Format = s => s.Downs.ToString() },
            new SummaryRow { Label = "Abilities used", Award = null, Value = s => s.AbilitiesUsed, Format = s => s.AbilitiesUsed.ToString() },
        };

        static string Big(float v) => v >= 10000f ? (v / 1000f).ToString("0.#") + "k" : Mathf.RoundToInt(v).ToString();

        // The results screen is the "Rack" of Docs/Mockups/crt-run-failed.html: separate blue CRT tubes (the lobby's) on the dark layer, the
        // main menu's keycaps along the bottom. Left column (x 40, 740 wide): the title tube, the players' tubes, the rank strip. Right
        // column (x 800, 440 wide): the four totals, then the run's damage-per-second and health charts (RunGraphs.cs).
        const float RackL = 40f, RackLW = 740f, RackR = 800f, RackRW = 440f, RackFull = 1200f;
        const float RackPlayersY = 180f, RackPlayersH = 362f, RackRankY = 554f, RackRankH = 72f;

        /// The end of a run: a big outlined title (GAME OVER in red, VICTORY! in gold) in its own tube, the totals, a tube per player
        /// (their class icon big on a hatched island in their colour, then their numbers with dot leaders and bars), the run's charts,
        /// the rank strip, and the main menu's keycaps.
        void DrawRunSummary()
        {
            bool won = game.State == MatchState.Victory;
            float now = Time.unscaledTime;
            // Everything comes in as the run's ending plays out (RunEnding.cs): the dark layer, the title stamped in the
            // middle and gliding up, then the totals, the tubes and the keys.
            RoundBackdrop(ResultsDark * EndBg);  // (RoundWindows.cs)
            Color theme = won ? Ui.Gold : new Color(1f, 0.36f, 0.38f);
            string title = game.Practice != null ? (won ? "PRACTICE COMPLETE" : "PRACTICE OVER") : game.Hunt != null ? (won ? "HUNT COMPLETE!" : "HUNT FAILED") : won ? "VICTORY!" : "GAME OVER";
            string sub = game.Practice != null ? (won ? "YOU CLEARED ALL " + game.Practice.Waves.Count + " PRACTICE WAVES" : "YOU MADE IT TO PRACTICE WAVE " + Math.Max(1, game.PracticeIndex + 1) + " OF " + game.Practice.Waves.Count)
                : game.Hunt != null ? (won ? "YOU HUNTED DOWN " + BossName(game.Hunt.Boss).ToUpperInvariant() : "YOU MADE IT TO HUNT WAVE " + Math.Max(1, game.HuntWave) + " OF " + Hunts.Length)
                : won ? "YOU BEAT ALL " + game.ShownLastWave + " WAVES AND THE ECLIPSE" : "YOU MADE IT TO WAVE " + game.ShownWave;
            if (!won && game.Practice == null && game.BossNow)
                sub += "  ·  " + Defs.Get(game.BossAt(game.Wave)).Name.ToUpperInvariant();

            // The title tube. Before the title has glided up it is just the stamp in the middle of the screen.
            var titleBox = new Rect(RackL, 24f, RackLW, 140f);
            System.Action drawTitle = () =>
            {
                float titleIn = EndTitleIn;
                if (titleIn > 0f)
                {
                    float ts = EndTitleScale, lift = EndLift, ty = Mathf.Lerp(H / 2f - 33f, 36f, lift);
                    float tx0 = Mathf.Lerp(0f, titleBox.x, lift), tw0 = Mathf.Lerp(W, titleBox.width, lift);
                    int baseSize = game.Practice != null ? 50 : 60;
                    var shake = EndTitleShake();
                    var tr = new Rect(shake.x + tx0 - tw0 * (ts - 1f) / 2f, ty + shake.y - 33f * (ts - 1f), tw0 * ts, 66f * ts);
                    RoundPart(titleIn, Vector2.zero, () =>
                        Ui.Glitch(tr, title, Mathf.RoundToInt(baseSize * ts), Color.white, TextAnchor.MiddleCenter, new Color(theme.r, theme.g, theme.b, 0.55f)));
                }
                RoundPart(EndSubIn, Vector2.zero, () =>
                    LobbyKit.Spaced(new Rect(titleBox.x, titleBox.y + 88f, titleBox.width, 30f), sub, 14, CrtUi.Ink, TextAnchor.MiddleCenter, 3f));
            };
            if (EndLift > 0f)
                RoundTube(titleBox, EndLift, false, drawTitle);
            else
                drawTitle();

            // Run totals: four small tubes, two by two.
            int kills = 0;
            foreach (var p in game.Players.Values)
                kills += p.Run.Kills;
            int secs = Mathf.RoundToInt(game.RunTime);
            string[,] tiles = { { "WAVE", game.ShownWave.ToString() }, { "TIME", secs / 60 + ":" + (secs % 60).ToString("00") }, { "SCORE", game.Score.ToString("N0") }, { "KILLS", kills.ToString() } };
            float tileW = (RackRW - 16f) / 2f, tileH = 62f;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rect(RackR + (i % 2) * (tileW + 16f), 24f + (i / 2) * (tileH + 16f), tileW, tileH);
                int ti = i;
                RoundTube(c, EndTileIn(i), true, () =>  // each drops in (RunEnding.cs)
                {
                    CrtUi.Title(new Rect(c.x, c.y + 3f, c.width, 38f), tiles[ti, 1], 36, LobbyKit.Gold, TextAnchor.MiddleCenter, fxReduced);
                    Ui.Label(new Rect(c.x, c.yMax - 20f, c.width, 14f), tiles[ti, 0], 10, CrtUi.Dim, TextAnchor.MiddleCenter, true);
                });
            }

            // Which page: the players' tubes, or the rank report (RankUi.cs; a new run's results start on the stats).
            if (!ReferenceEquals(game.LastGain, rankGainSeen))
            {
                rankGainSeen = game.LastGain;
                resultsRankPage = false;
            }
            bool rankPage = resultsRankPage && game.RunStatsReady && game.LastGain != null;

            if (rankPage)
            {
                float rk = EndCardIn(0);
                RoundPart(rk, new Vector2(0f, 60f * (1f - Ease(rk))), () => DrawRankReport(new Rect(70f, 176f, W - 140f, 448f), now - rankPageAt));
            }
            else if (!game.RunStatsReady)
                Ui.Label(new Rect(0f, RackPlayersY + 160f, W, 30f), "Collecting results…", 18, Ui.Muted, TextAnchor.MiddleCenter);
            else
            {
                var players = new List<Player>(game.Players.Values);
                int n = Mathf.Max(1, players.Count);
                float pgap = 12f, pw = (RackLW - pgap * (n - 1)) / n;
                for (int i = 0; i < players.Count; i++)
                {
                    var pr = new Rect(RackL + i * (pw + pgap), RackPlayersY, pw, RackPlayersH);
                    int pi = i;
                    RoundTube(pr, EndCardIn(i), true, () => DrawPlayerSummary(players[pi], players, pr, now));
                }

                // The run's charts, from its timeline (RunTimeline.cs): damage per second over health, as the Recent runs page draws them.
                float cw = RackRW, ch = (RackRankY + RackRankH - RackPlayersY - 8f) / 2f;
                var chartsIn = EndCardIn(0);
                var tl = SummaryTimeline();
                for (int c = 0; c < 2; c++)
                {
                    var cr = new Rect(RackR, RackPlayersY + c * (ch + 8f), cw, ch);
                    int ci = c;
                    RoundTube(cr, chartsIn, false, () =>
                    {
                        if (tl == null)
                        {
                            if (ci == 0)
                                LobbyKit.Spaced(new Rect(cr.x, cr.center.y - 14f, cr.width, 28f), game.Practice != null ? "PRACTICE RUNS AREN'T GRAPHED" : "NO GRAPH WAS KEPT FOR THIS RUN", 13, CrtUi.Dim, TextAnchor.MiddleCenter, 2f);
                            return;
                        }
                        if (chartsIn < 1f)
                            return;  // (the lines are drawn with GL, which can't fade: they appear once the tube is in)
                        chartTipBounds = new Rect(0f, 0f, W, H);
                        if (ci == 0)
                            DrawDpsChart(tl, cr);
                        else
                            DrawHealthChart(tl, cr);
                        chartTipBounds = null;
                    });
                }
            }

            // The rank strip: what the run earned towards your account rank (Rank.cs, RankUi.cs). Click it for the full report.
            if (game.RunStatsReady && game.LastGain != null && !rankPage)
            {
                float sk = EndingOn ? Seg(endT, EndButtonsAt, EndButtonsAt + 1.8f) : 1f;
                var strip = new Rect(RackL, RackRankY, RackLW, RackRankH);
                RoundPart(EndCardIn(0), new Vector2(0f, 40f * (1f - Ease(EndCardIn(0)))), () =>
                {
                    DrawRankStrip(strip, sk, now);
                    if (Ui.HitArea(strip))
                        OpenRankReport(now);
                });
            }

            // The keys along the bottom, the main menu's keycaps: Leave and the rank report on the left, the host's choices after them.
            float buttonsIn = EndButtonsIn;
            bool endless = won && game.Hunt == null && session.IsHost;  // (a Boss Hunt is over once its boss is: no endless)
            RoundPart(buttonsIn, new Vector2(0f, 30f * (1f - Ease(buttonsIn))), () =>  // they come up last (RunEnding.cs)
            {
                float by = H - 82f, bh = 58f, gap = 22f;
                bool ranked = game.RunStatsReady && game.LastGain != null, host = session.IsHost;
                int count = 1 + (ranked ? 1 : 0) + (host ? (endless ? 3 : 2) : 0);
                float kw = (RackFull - gap * (count - 1)) / count;
                if (!host)
                    kw = Mathf.Min(kw, 260f);
                float x = RackL;
                if (KeyUi.Key(new Rect(x, by, kw, bh), "Leave", PixelArt.Get("ui_power"), 22))
                    session.Leave("");
                x += kw + gap;
                if (ranked)  // the two pages of the results
                {
                    bool lit = game.LastGain.RankedUp && !rankPage;  // a new rank: the key's lamp lights and a gold ring pulses
                    var rankRect = new Rect(x, by, kw, bh);
                    if (lit)
                        Ui.PulseRing(KeyUi.Well(rankRect), Ui.GoldButton, now);
                    if (KeyUi.Key(rankRect, rankPage ? "Stats" : "Rank report", PixelArt.Get(rankPage ? "ui_back" : "ui_trophy"), 22, lit))
                    {
                        if (rankPage)
                            resultsRankPage = false;
                        else
                            OpenRankReport(now);
                    }
                    x += kw + gap;
                }
                if (host)
                {
                    if (endless)
                    {
                        if (KeyUi.Key(new Rect(x, by, kw, bh), "Keep going  [C]", PixelArt.Get("ui_star"), 22))
                            game.ContinueEndless();
                        x += kw + gap;
                    }
                    if (KeyUi.Key(new Rect(x, by, kw, bh), "Play again  [R]", PixelArt.Get("ui_play"), 22, true))
                        session.StartMatch();
                    x += kw + gap;
                    if (KeyUi.Key(new Rect(x, by, kw, bh), "Back to lobby  [L]", PixelArt.Get("ui_back"), 22))
                        RecallToLobby();  // everyone beams out first (Recall.cs)
                }
                else
                    Ui.Label(new Rect(x, by, RackL + RackFull - x, bh), "Waiting for the host…", 16, Ui.Muted, TextAnchor.MiddleRight, true);
            });
            if (game.Recorder != null)
                Ui.Label(new Rect(RackL, H - 16f, RackFull, 14f), "Session recorded: " + game.Recorder.Path, 10, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
        }

        /// This run's curve for the results charts: the newest Recent runs entry (the run just paid out, so only once LastGain is set).
        RunTimeline SummaryTimeline()
        {
            if (game.LastGain == null || game.Practice != null || Profile.Current.Recent.Count == 0)
                return null;
            var log = Profile.Current.Recent[0];
            if ((game.Hunt != null) != (log.Result >= 2))
                return null;
            return TimelineFor(log);
        }

        /// One player's tube: on the left their class icon big on a glowing, hatched island in their colour, their role, level and
        /// awards (where they had the best number); on the right their name and every number with dot leaders and a bar against the
        /// best in the party (gold where they lead), then their upgrades by rarity. With two or three players the tubes are narrow:
        /// the island runs along the top and the numbers are smaller and bar-less.
        void DrawPlayerSummary(Player p, List<Player> all, Rect r, float now)
        {
            var color = ToColor(p.Def.Color);
            bool mine = p.Id == game.MyId, compact = r.width < 600f;
            var art = compact ? new Rect(r.x + 10f, r.y + 10f, r.width - 20f, 88f) : new Rect(r.x + 14f, r.y + 14f, 200f, r.height - 28f);
            DkBox(art, color, mine ? 0.85f : 0.45f);
            var inner = new Rect(art.x + 3f, art.y + 3f, art.width - 6f, art.height - 6f);
            Ui.Pill(inner, new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 0.95f));
            if (!compact)
                for (int k = 0; k < 4; k++)
                {
                    float s = 1f - k * 0.2f;
                    Ui.Pill(new Rect(inner.center.x - inner.width * s / 2f, inner.y + 20f + 70f * (1f - s), inner.width * s, 150f * s), new Color(color.r, color.g, color.b, 0.06f + 0.02f * k));
                }
            Ui.Icon(inner, Hatch(), new Color(color.r, color.g, color.b, 0.06f));
            RankLook.Draw(new Rect(inner.xMax - (compact ? 30f : 40f), inner.y + 6f, compact ? 24f : 32f, compact ? 24f : 32f), RankOfPlayer(p));  // their account rank (RankUi.cs)
            float isz = compact ? 64f : 108f;
            var iconRect = compact ? new Rect(inner.x + 12f, inner.center.y - isz / 2f + 2f * Mathf.Sin(now * 2.2f + p.Id), isz, isz)
                : new Rect(inner.center.x - isz / 2f, inner.y + 28f + 2f * Mathf.Sin(now * 2.2f + p.Id), isz, isz);
            Ui.Icon(iconRect, PixelArt.Get("class_" + Defs.KitName(p.Kit).ToLowerInvariant()));
            float chipX = compact ? iconRect.xMax + 10f : inner.x + 12f, chipW = compact ? inner.xMax - chipX - 34f : inner.width - 24f;
            float ay = compact ? inner.y + 18f : iconRect.yMax + 14f;
            var roleChip = new Rect(chipX, ay, chipW, 24f);
            Ui.Pill(roleChip, new Color(color.r, color.g, color.b, 0.22f));
            Ui.Icon(new Rect(roleChip.x + 8f, roleChip.y + 4f, 16f, 16f), PixelArt.Get(RoleIcons[(int)p.Role]), color);
            Ui.Label(new Rect(roleChip.x + 28f, roleChip.y, roleChip.width - 32f, 24f), p.Def.Name.ToUpperInvariant(), 11, Color.Lerp(color, Color.white, 0.4f), TextAnchor.MiddleLeft, true);
            ay += 32f;
            var lvl = new Rect(chipX, ay, chipW, 24f);
            Ui.Pill(lvl, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f));
            Ui.Label(lvl, "LEVEL " + p.Level, 12, new Color(0.08f, 0.06f, 0.02f), TextAnchor.MiddleCenter, true);
            ay += 36f;
            // Awards: rows where this player has the best number (and it isn't zero).
            if (all.Count > 1 && !compact)
                foreach (var row in SummaryRows)
                {
                    if (row.Award == null || row.Value(p.Run) <= 0f || ay > inner.yMax - 28f)
                        continue;
                    bool best = true;
                    foreach (var o in all)
                        if (o != p && row.Value(o.Run) >= row.Value(p.Run))
                            best = false;
                    if (!best)
                        continue;
                    var award = new Rect(inner.x + 12f, ay, inner.width - 24f, 22f);
                    Ui.Pill(award, new Color(Ui.GoldButton.r, Ui.GoldButton.g, Ui.GoldButton.b, 0.28f));
                    Ui.Icon(new Rect(award.x + 7f, award.y + 4f, 14f, 14f), PixelArt.Get("ui_trophy"), Ui.Gold);
                    Ui.Label(new Rect(award.x + 25f, award.y, award.width - 28f, 22f), row.Award.ToUpperInvariant(), 10, Ui.Gold, TextAnchor.MiddleLeft, true);
                    ay += 27f;
                }

            // Name and class, then the numbers.
            float x = compact ? r.x + 14f : art.xMax + 22f, w = compact ? r.width - 28f : r.xMax - 16f - x;
            float ny = compact ? art.yMax + 6f : r.y + 16f;
            DkTitle(new Rect(x, ny, w, compact ? 26f : 34f), p.Name, compact ? 20 : 26, Color.Lerp(color, Color.white, 0.35f), TextAnchor.MiddleLeft);
            string cls = Defs.KitName(p.Kit) + " " + p.Def.Name + (compact ? "" : "  ·  " + p.Weapon.Name);
            Ui.Label(new Rect(x, ny + (compact ? 26f : 38f), w, 18f), cls, compact ? 11 : 13, color, TextAnchor.MiddleLeft, true);
            float y = ny + (compact ? 50f : 66f);
            Ui.Fill(new Rect(x, y - 4f, w, 1f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.3f));
            float pitch = compact ? 19f : 26f;
            int vsize = compact ? 17 : 22;
            foreach (var row in SummaryRows)
            {
                float val = row.Value(p.Run), top = 0f;
                foreach (var o in all)
                    top = Mathf.Max(top, row.Value(o.Run));
                bool leading = all.Count > 1 && row.Award != null && val > 0f && val >= top;
                string text = row.Format(p.Run);
                float lw = CrtUi.TitleWidth(row.Label, vsize), vw = CrtUi.TitleWidth(text, vsize);
                CrtUi.Title(new Rect(x, y, lw + 10f, 22f), row.Label, vsize, CrtUi.Dim, TextAnchor.MiddleLeft, true);
                CrtUi.Title(new Rect(x + w - vw - 10f, y, vw + 10f, 22f), text, vsize, leading ? Ui.Gold : Color.white, TextAnchor.MiddleRight, fxReduced);
                var dotCol = new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.4f);
                for (float dx = x + lw + 12f; dx < x + w - vw - 12f; dx += 5f)  // the dot leader
                    Ui.Fill(new Rect(dx, y + 15f, 2f, 2f), dotCol);
                if (!compact && top > 0f && row.Award != null)
                {
                    Ui.Fill(new Rect(x, y + 22f, w, 3f), new Color(1f, 1f, 1f, 0.05f));
                    Ui.Fill(new Rect(x, y + 22f, w * Mathf.Clamp01(val / top), 3f), leading ? Ui.Gold : new Color(color.r, color.g, color.b, 0.75f));
                }
                y += pitch;
            }

            // Upgrades picked, by rarity, and any legendaries by name.
            var counts = new int[Enum.GetValues(typeof(Rarity)).Length];
            var legendaries = new List<string>();
            foreach (int id in p.Taken)
            {
                counts[(int)Upgrades.All[id].Rarity]++;
                if (Upgrades.All[id].Rarity == Rarity.Legendary)
                    legendaries.Add(Upgrades.All[id].Name);
            }
            y += 4f;
            Ui.Label(new Rect(x, y, 90f, 22f), "UPGRADES", 11, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            float px = x + 86f;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                    continue;
                var rc = ToColor(Upgrades.RarityColor((Rarity)i));
                var pill = new Rect(px, y + 1f, compact ? 30f : 40f, 20f);
                Ui.Pill(pill, new Color(rc.r, rc.g, rc.b, 0.9f));
                Ui.Label(pill, counts[i].ToString(), 11, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
                px += compact ? 34f : 46f;
            }
            if (legendaries.Count > 0 && !compact)
                Ui.Label(new Rect(px + 6f, y, x + w - px - 6f, 22f), string.Join(", ", legendaries.ToArray()), 12, ToColor(Upgrades.RarityColor(Rarity.Legendary)), TextAnchor.MiddleLeft, true);
        }
    }
}

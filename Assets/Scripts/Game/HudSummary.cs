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

        /// The end of a run, in the level-up picker's "trophy" style: a big outlined title (GAME OVER in red, VICTORY! in
        /// gold), the run's totals as tiles, a trophy card per player (their class icon big on a hatched backdrop in their
        /// colour, level and awards beside it, then their numbers with bars), and the lobby's fancy buttons.
        void DrawRunSummary()
        {
            bool won = game.State == MatchState.Victory;
            float now = Time.unscaledTime;
            // Everything comes in as the run's ending plays out (RunEnding.cs): the dark layer, the title stamped in the
            // middle and gliding up, then the tiles, the cards and the buttons.
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.84f * EndBg));
            Color theme = won ? Ui.Gold : new Color(1f, 0.36f, 0.38f);
            string title = game.Practice != null ? (won ? "PRACTICE COMPLETE" : "PRACTICE OVER") : game.Hunt != null ? (won ? "HUNT COMPLETE!" : "HUNT FAILED") : won ? "VICTORY!" : "GAME OVER";
            float titleIn = EndTitleIn;
            if (titleIn > 0f)
            {
                float ts = EndTitleScale, ty = Mathf.Lerp(H / 2f - 33f, 22f, EndLift);
                int baseSize = game.Practice != null ? 46 : 56;
                var shake = EndTitleShake();
                var tr = new Rect(shake.x - W * (ts - 1f) / 2f, ty + shake.y - 33f * (ts - 1f), W * ts, 66f * ts);
                EndPart(titleIn, Vector2.zero, () =>
                    Ui.Fancy(tr, title, Mathf.RoundToInt(baseSize * ts), Color.white, theme, TextAnchor.MiddleCenter, new Color(theme.r, theme.g, theme.b, 0.55f)));
            }
            var oldAlpha = Ui.Alpha;
            var oldColor = GUI.color;
            float subIn = EndSubIn;
            Ui.Alpha = oldAlpha * subIn;
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * subIn);
            string sub = game.Practice != null ? (won ? "YOU CLEARED ALL " + game.Practice.Waves.Count + " PRACTICE WAVES" : "YOU MADE IT TO PRACTICE WAVE " + Math.Max(1, game.PracticeIndex + 1) + " OF " + game.Practice.Waves.Count)
                : game.Hunt != null ? (won ? "YOU HUNTED DOWN " + BossName(game.Hunt.Boss).ToUpperInvariant() : "YOU MADE IT TO HUNT WAVE " + Math.Max(1, game.HuntWave) + " OF " + Hunts.Length)
                : won ? "YOU BEAT ALL " + Game.MaxWave + " WAVES AND THE ECLIPSE" : "YOU MADE IT TO WAVE " + game.Wave;
            if (!won && game.Practice == null && game.IsBossWave(game.Wave))
                sub += "  ·  " + Defs.Get(game.BossAt(game.Wave)).Name.ToUpperInvariant();
            if (subIn > 0f)
                Ui.Label(new Rect(0f, 88f, W, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Alpha = oldAlpha;
            GUI.color = oldColor;

            // Run totals, as tiles.
            int kills = 0;
            foreach (var p in game.Players.Values)
                kills += p.Run.Kills;
            int secs = Mathf.RoundToInt(game.RunTime);
            string[,] tiles = { { "WAVE", game.Wave.ToString() }, { "TIME", secs / 60 + ":" + (secs % 60).ToString("00") }, { "SCORE", game.Score.ToString("N0") }, { "KILLS", kills.ToString() } };
            const float tw = 168f, tgap = 12f;
            float tx = W / 2f - (tw * 4f + tgap * 3f) / 2f;
            for (int i = 0; i < 4; i++)
            {
                var c = new Rect(tx + i * (tw + tgap), 112f, tw, 56f);
                float k = EndTileIn(i);
                int ti = i;
                EndPart(k, new Vector2(0f, -40f * (1f - Ease(k))), () =>  // each drops in (RunEnding.cs)
                {
                    Ui.Card(c, theme, 0.25f);
                    Ui.Fancy(new Rect(c.x, c.y + 3f, c.width, 32f), tiles[ti, 1], 24, Color.white, Color.Lerp(theme, Color.white, 0.55f), TextAnchor.MiddleCenter);
                    Ui.Label(new Rect(c.x, c.y + 36f, c.width, 16f), tiles[ti, 0], 10, Ui.Muted, TextAnchor.MiddleCenter, true);
                });
            }

            // Which page: the players' stat cards, or the rank report (RankUi.cs; a new run's results start on the stats).
            if (!ReferenceEquals(game.LastGain, rankGainSeen))
            {
                rankGainSeen = game.LastGain;
                resultsRankPage = false;
            }
            bool rankPage = resultsRankPage && game.RunStatsReady && game.LastGain != null;

            // A trophy card for each player.
            const float cardTop = 176f, cardH = 408f;
            if (rankPage)
            {
                float rk = EndCardIn(0);
                EndPart(rk, new Vector2(0f, 60f * (1f - Ease(rk))), () => DrawRankReport(new Rect(70f, cardTop, W - 140f, 460f), now - rankPageAt));
            }
            else if (!game.RunStatsReady)
                Ui.Label(new Rect(0f, cardTop + 180f, W, 30f), "Collecting results…", 18, Ui.Muted, TextAnchor.MiddleCenter);
            else
            {
                var players = new List<Player>(game.Players.Values);
                int n = Mathf.Max(1, players.Count);
                float cgap = 24f, cw = n == 1 ? 760f : (W - 140f - cgap * (n - 1)) / n;
                float cx = W / 2f - (cw * n + cgap * (n - 1)) / 2f;
                for (int i = 0; i < players.Count; i++)
                {
                    float k = EndCardIn(i);
                    int pi = i;
                    EndPart(k, new Vector2(0f, 80f * (1f - Ease(k))), () =>  // each rises in (RunEnding.cs)
                        DrawPlayerSummary(players[pi], players, new Rect(cx + pi * (cw + cgap), cardTop, cw, cardH), now));
                }
            }

            // The rank strip: what the run earned towards your account rank (Rank.cs, RankUi.cs). Click it for the full report.
            if (game.RunStatsReady && game.LastGain != null && !rankPage)
            {
                float sk = EndingOn ? Seg(endT, EndButtonsAt, EndButtonsAt + 1.8f) : 1f;
                var strip = new Rect(70f, cardTop + cardH + 8f, W - 140f, 44f);
                EndPart(EndCardIn(0), new Vector2(0f, 40f * (1f - Ease(EndCardIn(0)))), () =>
                {
                    DrawRankStrip(strip, sk, now);
                    if (Ui.HitArea(strip))
                        OpenRankReport(now);
                });
            }

            // Buttons, laid out like the lobby's: Leave on the left, the host's choices on the right.
            float buttonsIn = EndButtonsIn;
            EndPart(buttonsIn, new Vector2(0f, 30f * (1f - Ease(buttonsIn))), () =>  // they come up last (RunEnding.cs)
            {
                float by = H - 82f;
                if (Ui.FancyButton(new Rect(70f, by, 170f, 48f), "Leave", Ui.Danger, PixelArt.Get("ui_power"), now))
                    session.Leave("");
                if (game.RunStatsReady && game.LastGain != null)  // the two pages of the results
                {
                    bool up = game.LastGain.RankedUp;
                    var tint = up && !rankPage ? Color.Lerp(Ui.GoldButton, new Color(1f, 0.85f, 0.4f), 0.5f + 0.5f * Mathf.Sin(now * 5f)) : Ui.Neutral;
                    if (Ui.FancyButton(new Rect(256f, by, 168f, 48f), rankPage ? "Stats" : "Rank report", tint, PixelArt.Get(rankPage ? "ui_back" : "ui_trophy"), now, 16))
                    {
                        if (rankPage)
                            resultsRankPage = false;
                        else
                            OpenRankReport(now);
                    }
                }
                if (session.IsHost)
                {
                    float bx = W - 70f;
                    if (won && game.Hunt == null)  // (a Boss Hunt is over once its boss is: no endless)
                    {
                        bx -= 300f;
                        if (Ui.FancyButton(new Rect(bx, by, 300f, 48f), "Keep going (endless)  [C]", Ui.GoldButton, PixelArt.Get("ui_star"), now))
                            game.ContinueEndless();
                        bx -= 14f;
                    }
                    bx -= 230f;
                    if (Ui.FancyButton(new Rect(bx, by, 230f, 48f), "Back to lobby  [L]", Ui.Neutral, PixelArt.Get("ui_back"), now))
                        RecallToLobby();  // everyone beams out first (Recall.cs)
                    bx -= 244f;
                    if (Ui.FancyButton(new Rect(bx, by, 230f, 48f), "Play again  [R]", Ui.Primary, PixelArt.Get("ui_play"), now))
                        session.StartMatch();
                }
                else
                    Ui.Label(new Rect(W - 470f, by, 400f, 48f), "Waiting for the host…", 16, Ui.Muted, TextAnchor.MiddleRight, true);
            });
            if (game.Recorder != null)
                Ui.Label(new Rect(70f, H - 26f, W - 140f, 16f), "Session recorded: " + game.Recorder.Path, 10, new Color(1f, 1f, 1f, 0.35f), TextAnchor.MiddleLeft);
        }

        /// One player's trophy card: on the left their class icon big on a glowing, hatched backdrop in their colour,
        /// their role, level and awards (where they had the best number); on the right their name and every number,
        /// with a bar against the best in the party (gold where they lead), and their upgrades by rarity.
        void DrawPlayerSummary(Player p, List<Player> all, Rect r, float now)
        {
            var color = ToColor(p.Def.Color);
            bool mine = p.Id == game.MyId;
            if (mine && all.Count > 1)
                for (int k = 3; k >= 1; k--)
                    Ui.Pill(new Rect(r.x - 4f * k, r.y - 4f * k, r.width + 8f * k, r.height + 8f * k), new Color(color.r, color.g, color.b, 0.05f * (4 - k) / 3f));
            Ui.Card(r, color, mine ? 0.85f : 0.45f);

            // The art column.
            float artW = Mathf.Min(170f, r.width * 0.3f);
            var art = new Rect(r.x + 3f, r.y + 3f, artW, r.height - 6f);
            Ui.Pill(art, new Color(color.r * 0.12f, color.g * 0.12f, color.b * 0.12f, 0.95f));
            for (int k = 0; k < 4; k++)
            {
                float s = 1f - k * 0.2f;
                Ui.Pill(new Rect(art.center.x - art.width * s / 2f, art.y + 20f + 70f * (1f - s), art.width * s, 150f * s), new Color(color.r, color.g, color.b, 0.06f + 0.02f * k));
            }
            Ui.Icon(art, Hatch(), new Color(color.r, color.g, color.b, 0.06f));
            RankLook.Draw(new Rect(art.xMax - 40f, art.y + 8f, 32f, 32f), RankOfPlayer(p));  // their account rank (RankUi.cs)
            float isz = Mathf.Min(108f, artW - 30f);
            Ui.Icon(new Rect(art.center.x - isz / 2f, art.y + 30f + 2f * Mathf.Sin(now * 2.2f + p.Id), isz, isz), PixelArt.Get("class_" + Defs.KitName(p.Kit).ToLowerInvariant()));
            float ay = art.y + 46f + isz;
            var roleChip = new Rect(art.x + 12f, ay, art.width - 24f, 24f);
            Ui.Pill(roleChip, new Color(color.r, color.g, color.b, 0.22f));
            Ui.Icon(new Rect(roleChip.x + 8f, roleChip.y + 4f, 16f, 16f), PixelArt.Get(RoleIcons[(int)p.Role]), color);
            Ui.Label(new Rect(roleChip.x + 28f, roleChip.y, roleChip.width - 32f, 24f), p.Def.Name.ToUpperInvariant(), 11, Color.Lerp(color, Color.white, 0.4f), TextAnchor.MiddleLeft, true);
            ay += 32f;
            var lvl = new Rect(art.x + 12f, ay, art.width - 24f, 24f);
            Ui.Pill(lvl, new Color(Ui.Gold.r, Ui.Gold.g, Ui.Gold.b, 0.9f));
            Ui.Label(lvl, "LEVEL " + p.Level, 12, new Color(0.08f, 0.06f, 0.02f), TextAnchor.MiddleCenter, true);
            ay += 36f;
            // Awards: rows where this player has the best number (and it isn't zero).
            if (all.Count > 1)
                foreach (var row in SummaryRows)
                {
                    if (row.Award == null || row.Value(p.Run) <= 0f || ay > art.yMax - 28f)
                        continue;
                    bool best = true;
                    foreach (var o in all)
                        if (o != p && row.Value(o.Run) >= row.Value(p.Run))
                            best = false;
                    if (!best)
                        continue;
                    var award = new Rect(art.x + 12f, ay, art.width - 24f, 22f);
                    Ui.Pill(award, new Color(Ui.GoldButton.r, Ui.GoldButton.g, Ui.GoldButton.b, 0.28f));
                    Ui.Icon(new Rect(award.x + 7f, award.y + 4f, 14f, 14f), PixelArt.Get("ui_trophy"), Ui.Gold);
                    Ui.Label(new Rect(award.x + 25f, award.y, award.width - 28f, 22f), row.Award.ToUpperInvariant(), 10, Ui.Gold, TextAnchor.MiddleLeft, true);
                    ay += 27f;
                }

            // Name, class and weapon, then the numbers.
            float x = art.xMax + 18f, w = r.xMax - 18f - x;
            Ui.Fancy(new Rect(x, r.y + 12f, w, 34f), p.Name, 26, Color.white, Color.Lerp(color, Color.white, 0.35f), TextAnchor.MiddleLeft,
                mine ? (Color?)new Color(color.r, color.g, color.b, 0.5f) : null);
            Ui.Label(new Rect(x, r.y + 46f, w, 18f), Defs.KitName(p.Kit) + " " + p.Def.Name + "  ·  " + p.Weapon.Name, 13, color, TextAnchor.MiddleLeft, true);
            Ui.Fill(new Rect(x, r.y + 72f, w, 1f), new Color(1f, 1f, 1f, 0.08f));
            float y = r.y + 82f;
            foreach (var row in SummaryRows)
            {
                float val = row.Value(p.Run), top = 0f;
                foreach (var o in all)
                    top = Mathf.Max(top, row.Value(o.Run));
                bool leading = all.Count > 1 && row.Award != null && val > 0f && val >= top;
                Ui.Label(new Rect(x, y, w * 0.55f, 20f), row.Label, 14, Ui.Muted);
                Ui.Label(new Rect(x + w * 0.4f, y, w * 0.6f, 20f), row.Format(p.Run), 15, leading ? Ui.Gold : Color.white, TextAnchor.MiddleRight, true);
                if (top > 0f && row.Award != null)
                {
                    Ui.Fill(new Rect(x, y + 22f, w, 3f), new Color(1f, 1f, 1f, 0.05f));
                    Ui.Fill(new Rect(x, y + 22f, w * Mathf.Clamp01(val / top), 3f), leading ? Ui.Gold : new Color(color.r, color.g, color.b, 0.75f));
                }
                y += 30f;
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
            Ui.Label(new Rect(x, y, 90f, 22f), "UPGRADES", 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            float px = x + 86f;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                    continue;
                var rc = ToColor(Upgrades.RarityColor((Rarity)i));
                var pill = new Rect(px, y + 1f, 40f, 20f);
                Ui.Pill(pill, new Color(rc.r, rc.g, rc.b, 0.9f));
                Ui.Label(pill, counts[i].ToString(), 11, new Color(0.06f, 0.06f, 0.1f), TextAnchor.MiddleCenter, true);
                px += 46f;
            }
            if (legendaries.Count > 0)
                Ui.Label(new Rect(x, y + 24f, w, 18f), string.Join(", ", legendaries.ToArray()), 12, ToColor(Upgrades.RarityColor(Rarity.Legendary)), TextAnchor.MiddleLeft, true);
        }
    }
}

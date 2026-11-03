using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Run stats screen: hold Tab in a match (solo, that pauses it) or press Run stats in the pause menu (so a
    /// paused party can look together). Three tabs: Run so far (wave, route, the run's numbers, the party's share),
    /// Your build (the loadout screen in Hud.cs) and Team (each teammate's build). The numbers come from each
    /// player's RunStats, which the host sends everyone about once a second (Msg.RunStatsLive).
    public partial class ArenaApp
    {
        int statsTab;          // 0 Run so far, 1 Your build, 2 Team (kept between openings)
        bool runStatsOpen;     // opened from the pause menu

        static readonly Color CardBg = new Color(0.094f, 0.11f, 0.165f, 1f);
        static readonly Color TileBg = new Color(0.118f, 0.137f, 0.2f, 1f);

        // Switching tabs (with Visual effects on): "zoom through" (Docs/Mockups/tab-transitions.html). The old page grows
        // a little and fades as if you pass through it; the new one settles in from just behind. About a quarter second.
        public const float StatsZoomOut = 0.14f, StatsZoomIn = 0.2f, StatsZoomDelay = 0.04f;
        int statsShown = -1, statsFrom;
        float statsSwitchAt = -10f;

        void DrawRunStatsScreen(Player me, bool fromPause)
        {
            float t = Time.unscaledTime;
            if (statsShown < 0)
                statsShown = statsTab;  // (opening the screen isn't a switch)
            if (statsTab != statsShown)
            {
                statsFrom = statsShown;
                statsShown = statsTab;
                statsSwitchAt = t;
            }
            float e = t - statsSwitchAt;
            if (!fxOn || e >= StatsZoomDelay + StatsZoomIn)
                DrawStatsPage(statsTab, me, t, true);
            else
            {
                // One backdrop, blending from the old page's darkness to the new one's; then both pages over it.
                float into = Mathf.Clamp01((e - StatsZoomDelay) / StatsZoomIn), smooth = 1f - (1f - into) * (1f - into) * (1f - into);
                float dim = StatsDim(statsFrom, me) + (StatsDim(statsTab, me) - StatsDim(statsFrom, me)) * smooth;
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, dim));
                float outT = Mathf.Clamp01(e / StatsZoomOut);
                if (outT < 1f && Event.current.type == EventType.Repaint)  // the old page is only drawn, never clicked
                    DrawStatsZoomed(statsFrom, me, t, 1f + 0.06f * outT * outT, 1f - outT);
                if (into > 0f)
                    DrawStatsZoomed(statsTab, me, t, 0.94f + 0.06f * smooth, smooth);
            }

            // The tabs, centred above the panel; Back (from the pause menu) or how to close, on the right.
            int mates = game.Players.Count - (me != null ? 1 : 0);
            string[] names = { "Run so far", "Your build", "Team" + (mates > 0 ? "  (" + mates + ")" : "") };
            const float tw = 150f, gap = 8f;
            float tx = W / 2f - (3f * tw + 2f * gap) / 2f;
            for (int i = 0; i < 3; i++)
                if (Ui.Choice(new Rect(tx + i * (tw + gap), 12f, tw, 34f), names[i], statsTab == i, Ui.Accent, null, null, 14))
                    statsTab = i;
            if (fromPause)
            {
                if (Ui.FancyButton(new Rect(W - 164f, 10f, 140f, 38f), "Back", Ui.Primary, PixelArt.Get("ui_back"), t))
                    runStatsOpen = false;
            }
            else
                Ui.Label(new Rect(W - 330f, 12f, 306f, 34f), tabPaused ? "Paused while you look  ·  let go of Tab" : "Let go of Tab to close",
                    11, Ui.Muted, TextAnchor.MiddleRight);
        }

        static float StatsDim(int tab, Player me) => tab == 1 && me != null ? 0.6f : 0.78f;

        /// One page of the screen (with its own backdrop when `dim`).
        void DrawStatsPage(int tab, Player me, float t, bool dim)
        {
            if (tab == 1 && me != null)
            {
                DrawTabMenu(me, dim);  // Your build: the loadout screen (Hud.cs)
                return;
            }
            if (dim)
                Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.012f, 0.03f, 0.78f));
            var panel = new Rect(24f, 56f, W - 48f, H - 56f - 20f);
            Ui.Panel(panel, true);
            if (tab == 0)
                DrawRunSoFar(panel, me);
            else
                DrawTeam(panel, me, t);
        }

        /// A page scaled about the screen's middle and faded (the switch): like the HUD's own show and hide (HudGroup).
        /// Reduced effects: just the fade.
        /// True while a page is drawn mid zoom: its live models (GL, which ignores GUI.matrix and alpha) wait until it settles.
        bool statsZooming;

        void DrawStatsZoomed(int tab, Player me, float t, float k, float alpha)
        {
            if (fxReduced)
                k = 1f;
            Vector2 c = new Vector2(W / 2f, H / 2f + 20f);
            var oldMatrix = GUI.matrix;
            var oldColor = GUI.color;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + c.x * (1f - k) * scale, offY + c.y * (1f - k) * scale, 0f), Quaternion.identity, new Vector3(scale * k, scale * k, 1f));
            GUI.color = new Color(oldColor.r, oldColor.g, oldColor.b, oldColor.a * alpha);
            Ui.Alpha = alpha;
            statsZooming = true;
            DrawStatsPage(tab, me, t, false);
            statsZooming = false;
            Ui.Alpha = 1f;
            GUI.color = oldColor;
            GUI.matrix = oldMatrix;
        }

        // --- Run so far ----------------------------------------------------------------------

        /// A rounded chip, laid out from the right: draws it ending at `right` and moves `right` left past it.
        void StatChip(ref float right, float y, string text, Color bg, Color fg)
        {
            float w = Ui.TextWidth(text, 12, true) + 4f;
            var r = new Rect(right - w, y, w, 26f);
            Ui.Pill(r, bg);
            Ui.Label(r, text, 12, fg, TextAnchor.MiddleCenter, true);
            right = r.x - 8f;
        }

        void DrawRunSoFar(Rect panel, Player me)
        {
            float x = panel.x + 20f, w = panel.width - 40f, y = panel.y + 16f;
            bool practice = game.Practice != null;
            int wave = Mathf.Max(0, game.Wave);

            // Header: the wave and where you are, then the chips.
            string waveText = practice ? "PRACTICE" : game.Hunt != null ? "HUNT WAVE " + wave : "WAVE " + wave;
            float ww = Ui.TextWidth(waveText, 30, true) - 4f;
            Ui.Fancy(new Rect(x, y, ww + 10f, 40f), waveText, 30, Color.white, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleLeft, Ui.Accent);
            float hx = x + ww + 8f;
            if (!practice)
            {
                Ui.Label(new Rect(hx, y + 6f, 60f, 34f), game.Endless ? "endless" : "/ " + game.LastWave, 15, Ui.Muted);
                hx += 54f;
            }
            string where;
            Color whereC = new Color(0.5f, 0.89f, 1f);
            if (practice)
                where = "Practice Range";
            else if (game.IsBossWave(wave))
            {
                where = "Boss wave";
                whereC = new Color(1f, 0.6f, 0.65f);
            }
            else if (game.CurrentPath != PathId.None && wave >= game.PathFrom && wave <= game.PathTo)
            {
                where = Game.PathName(game.CurrentPath) + " path  ·  wave " + (wave - game.PathFrom + 1) + " of " + (game.PathTo - game.PathFrom + 1);
                whereC = Color.Lerp(ToColor(Paths.Get(game.CurrentPath).Accent), Color.white, 0.3f);
            }
            else if (game.Hunt != null)
                where = wave == 0 ? "Getting ready" : game.Hunt.Name + "  ·  " + BossName(game.Hunt.Boss) + "'s own enemies";
            else
                where = wave == 0 ? "Getting ready" : "Plain arena";
            Ui.Label(new Rect(hx, y + 2f, 320f, 18f), where, 13, whereC, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(hx, y + 20f, 320f, 18f), game.State == MatchState.Fighting ? game.Remaining + " enemies left" : "Between waves", 12, Ui.Muted);

            float right = x + w, cy = y + 7f;
            var modBg = new Color(0.18f, 0.14f, 0.26f);
            var modFg = new Color(0.84f, 0.72f, 1f);
            for (int i = Modifiers.All.Length - 1; i >= 0; i--)
                if (game.Has(Modifiers.All[i].Mod))
                    StatChip(ref right, cy, Modifiers.All[i].Name, modBg, modFg);
            if (!practice)
            {
                int nextBoss = game.NextBossWave(wave, game.State == MatchState.Fighting);
                string boss = BossName(game.BossAt(nextBoss));
                StatChip(ref right, cy, nextBoss == wave ? "Boss now: " + boss : "Next boss: " + boss + "  ·  wave " + nextBoss,
                    new Color(0.23f, 0.14f, 0.16f), new Color(1f, 0.7f, 0.73f));
            }
            StatChip(ref right, cy, "Score " + game.Score.ToString("N0"), new Color(0.145f, 0.17f, 0.25f), Ui.Text);
            int secs = Mathf.FloorToInt(game.RunTime);
            StatChip(ref right, cy, (secs / 60) + ":" + (secs % 60).ToString("00") + " played", new Color(0.145f, 0.17f, 0.25f), Ui.Text);

            // The run's numbers, in one strip.
            y += 54f;
            var s = me != null ? me.Run : new RunStats();
            string[,] tiles =
            {
                { "Damage dealt", Mathf.RoundToInt(s.DamageDealt).ToString("N0") }, { "Kills", s.Kills.ToString("N0") },
                { "Healing done", Mathf.RoundToInt(s.HealingDone).ToString("N0") }, { "Accuracy", s.ShotsFired > 0 ? Mathf.RoundToInt(s.Accuracy * 100f) + "%" : "—" },
                { "Biggest hit", Mathf.RoundToInt(s.BiggestHit).ToString("N0") }, { "Level", me != null ? me.Level.ToString() : "—" },
                { "Downed", s.Downs.ToString() }, { "Allies revived", s.Revives.ToString() },
            };
            float tileW = (w - 7f * 8f) / 8f;
            for (int i = 0; i < 8; i++)
            {
                var r = new Rect(x + i * (tileW + 8f), y, tileW, 56f);
                Ui.Pill(r, TileBg);
                Ui.Label(new Rect(r.x + 12f, r.y + 7f, r.width - 18f, 14f), tiles[i, 0].ToUpperInvariant(), 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 12f, r.y + 22f, r.width - 18f, 28f), tiles[i, 1], 19, Color.white, TextAnchor.MiddleLeft, true);
            }

            // Two equal columns: the route, then your fight and the party's share stacked.
            y += 70f;
            float colW = (w - 16f) / 2f, bottom = panel.yMax - 18f;
            DrawRouteCard(new Rect(x, y, colW, bottom - y), practice);
            float half = (bottom - y - 16f) / 2f;
            DrawFightCard(new Rect(x + colW + 16f, y, colW, half), s);
            DrawShareCard(new Rect(x + colW + 16f, y + half + 16f, colW, half));
        }

        void CardHeading(Rect card, string left, string right)
        {
            Ui.Pill(card, CardBg);
            Ui.Label(new Rect(card.x + 16f, card.y + 12f, card.width - 32f, 18f), left.ToUpperInvariant(), 11, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (right != null)
                Ui.Label(new Rect(card.x + 16f, card.y + 12f, card.width - 32f, 18f), right, 12, Ui.Muted, TextAnchor.MiddleRight);
        }

        struct RouteRow
        {
            public string Name, Waves, Note;
            public Color Color;
            public bool Boss;
            public int State;  // 0 done, 1 now, 2 next
        }

        /// The run as a journey: each stretch of waves (its path, or the plain arena), then the boss that ends it.
        List<RouteRow> BuildRoute()
        {
            var rows = new List<RouteRow>();
            int wave = Mathf.Max(1, game.Wave);
            bool between = game.State == MatchState.Intermission && game.Wave > 0;
            int cur = between ? wave + 1 : wave;  // the wave being played, or the one coming up
            var plain = new Color(0.48f, 0.52f, 0.62f);
            if (game.Hunt != null)
            {
                // A Boss Hunt: its nine build-up waves, then its boss.
                int last = Hunts.Length;
                int st0 = cur >= last ? 0 : 1;
                rows.Add(new RouteRow
                {
                    Name = game.Hunt.Name, Waves = "waves 1–" + (last - 1),
                    Note = st0 == 0 ? "cleared" : "now  ·  wave " + cur + " of " + (last - 1),
                    Color = new Color(0.95f, 0.42f, 0.32f), State = st0,
                });
                int bs0 = cur > last ? 0 : cur == last ? 1 : 2;
                rows.Add(new RouteRow
                {
                    Name = BossName(game.Hunt.Boss), Waves = "wave " + last, Boss = true, State = game.State == MatchState.Victory ? 0 : bs0,
                    Note = game.State == MatchState.Victory ? "beaten" : bs0 == 1 ? "fighting now" : "next  ·  in " + (last - wave) + " wave" + (last - wave == 1 ? "" : "s"),
                    Color = new Color(0.89f, 0.33f, 0.42f),
                });
                return rows;
            }
            for (int k = 0; k < 40; k++)
            {
                int from = k * 5 + 1, to = k * 5 + 4, bossWave = k * 5 + 5;
                if (from <= cur)
                {
                    var id = k == 0 || k - 1 >= game.Route.Count ? PathId.None : game.Route[k - 1];
                    int st = cur > to ? 0 : 1;
                    rows.Add(new RouteRow
                    {
                        Name = k == 0 ? "Standard" : Game.PathName(id),
                        Waves = "waves " + from + "–" + to,
                        Note = st == 0 ? "cleared" : "now  ·  wave " + (cur - from + 1) + " of 4",
                        Color = id == PathId.None ? plain : ToColor(Paths.Get(id).Accent),
                        State = st,
                    });
                }
                int bs = cur > bossWave ? 0 : cur == bossWave ? 1 : 2;
                rows.Add(new RouteRow
                {
                    Name = BossName(Game.BossForWave(bossWave)), Waves = "wave " + bossWave, Boss = true, State = bs,
                    Note = bs == 0 ? "beaten" : bs == 1 ? "fighting now" : "next  ·  in " + (bossWave - wave) + " wave" + (bossWave - wave == 1 ? "" : "s"),
                    Color = new Color(0.89f, 0.33f, 0.42f),
                });
                if (bs != 0)
                    break;
            }
            return rows;
        }

        void DrawRouteCard(Rect card, bool practice)
        {
            if (practice)
            {
                CardHeading(card, "Route", null);
                Ui.Label(card, "The Practice Range plays your own waves: no route.", 13, Ui.Muted, TextAnchor.MiddleCenter);
                return;
            }
            var rows = BuildRoute();
            int beaten = 0;
            foreach (var r in rows)
                if (r.Boss && r.State == 0)
                    beaten++;
            CardHeading(card, "Route", game.Hunt != null ? (beaten > 0 ? "hunt won" : "the hunt") : beaten + " of " + Game.MaxWave / 5 + " bosses beaten");
            // Keep the newest rows when the run outgrows the card.
            float top = card.y + 40f, avail = card.yMax - 10f - top;
            int fit = Mathf.Max(1, Mathf.FloorToInt(avail / 30f));
            int first = Mathf.Max(0, rows.Count - fit);
            float rowH = Mathf.Min(34f, avail / Mathf.Max(1, rows.Count - first));
            for (int i = first; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = new Rect(card.x + 8f, top + (i - first) * rowH, card.width - 16f, rowH - 2f);
                if (r.State == 1)
                    Ui.Pill(row, new Color(0.12f, 0.19f, 0.25f));
                float cx = row.x + 20f, cyy = row.center.y;
                // The rail down the left, through every node.
                float railTop = i == first ? cyy : row.y - 1f, railBottom = i == rows.Count - 1 ? cyy : row.yMax + 2f;
                Ui.Fill(new Rect(cx - 1f, railTop, 2f, railBottom - railTop), new Color(0.17f, 0.2f, 0.29f));
                Color c = r.State == 2 ? new Color(0.29f, 0.16f, 0.2f) : r.Color;
                if (r.Boss)
                {
                    if (r.State != 2)
                        Ui.Pill(new Rect(cx - 9f, cyy - 9f, 18f, 18f), new Color(0.23f, 0.12f, 0.15f));
                    Ui.Pill(new Rect(cx - 6f, cyy - 6f, 12f, 12f), c);
                }
                else
                {
                    if (r.State == 1)
                        Ui.Pill(new Rect(cx - 8f, cyy - 8f, 16f, 16f), Color.white);
                    Ui.Pill(new Rect(cx - 5f, cyy - 5f, 10f, 10f), c);
                }
                Color name = r.State == 2 ? new Color(0.56f, 0.6f, 0.7f) : r.Boss ? new Color(1f, 0.7f, 0.73f) : Color.white;
                Ui.Label(new Rect(row.x + 40f, row.y, 170f, row.height), r.Name, 13, name, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(row.x + 214f, row.y, 100f, row.height), r.Waves, 12, Ui.Muted);
                Ui.Label(new Rect(row.x + 314f, row.y, row.width - 324f, row.height), r.Note, 12,
                    r.State == 1 ? new Color(0.56f, 0.9f, 1f) : r.State == 2 ? Ui.Muted : new Color(0.79f, 0.82f, 0.91f), TextAnchor.MiddleRight);
            }
        }

        void DrawFightCard(Rect card, RunStats s)
        {
            CardHeading(card, "Your fight", null);
            string[,] rows =
            {
                { "Damage taken", Mathf.RoundToInt(s.DamageTaken).ToString("N0") },
                { "Elite kills", s.EliteKills.ToString("N0") },
                { "Boss kills", s.BossKills.ToString("N0") },
                { "Shots that hit", s.ShotsHit.ToString("N0") + " of " + s.ShotsFired.ToString("N0") },
                { "Abilities used", s.AbilitiesUsed.ToString("N0") },
            };
            float top = card.y + 38f, rowH = (card.yMax - 10f - top) / 5f;
            for (int i = 0; i < 5; i++)
            {
                var r = new Rect(card.x + 10f, top + i * rowH, card.width - 20f, rowH - 2f);
                if (i % 2 == 0)
                    Ui.Pill(r, new Color(1f, 1f, 1f, 0.03f));
                Ui.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), rows[i, 0], 13, Ui.Muted);
                Ui.Label(new Rect(r.x + 8f, r.y, r.width - 16f, r.height), rows[i, 1], 14, Color.white, TextAnchor.MiddleRight, true);
            }
        }

        void DrawShareCard(Rect card)
        {
            float total = 0f;
            foreach (var p in game.Players.Values)
                total += p.Run.DamageDealt;
            CardHeading(card, "Party share of damage", game.Players.Count < 2 ? "solo" : null);
            int n = Mathf.Max(1, game.Players.Count), i = 0;
            float top = card.y + 40f, rowH = Mathf.Min(52f, (card.yMax - 10f - top) / n);
            foreach (var p in game.Players.Values)
            {
                var r = new Rect(card.x + 16f, top + i++ * rowH, card.width - 32f, rowH);
                var rc = ToColor(p.Def.Color);
                var s = p.Run;
                float share = total > 0f ? s.DamageDealt / total : 0f;
                Ui.Pill(new Rect(r.x, r.y + 5f, 10f, 10f), rc);
                float nw = Ui.TextWidth(p.Name, 13, true) - 6f;
                Ui.Label(new Rect(r.x + 16f, r.y, nw + 8f, 20f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 22f + nw, r.y, 160f, 20f), Defs.KitName(p.Kit) + " " + p.Def.Name, 12, rc);
                string line = "Down " + s.Downs + "  ·  revived " + s.Revives + (s.ShotsFired > 0 ? "  ·  " + Mathf.RoundToInt(s.Accuracy * 100f) + "% accuracy" : "");
                Ui.Label(new Rect(r.x, r.y, r.width, 20f), line, 11, Ui.Muted, TextAnchor.MiddleRight);
                Ui.Bar(new Rect(r.x, r.y + 25f, r.width - 52f, 8f), share, rc);
                Ui.Label(new Rect(r.xMax - 46f, r.y + 19f, 46f, 20f), Mathf.RoundToInt(share * 100f) + "%", 13, Color.white, TextAnchor.MiddleRight, true);
            }
        }

        // --- Team --------------------------------------------------------------------------

        void DrawTeam(Rect panel, Player me, float t)
        {
            var mates = new List<Player>();
            foreach (var p in game.Players.Values)
                if (me == null || p.Id != me.Id)
                    mates.Add(p);
            if (mates.Count == 0)
            {
                Ui.Label(panel, "You're playing solo: your build is on the Your build tab.", 15, Ui.Muted, TextAnchor.MiddleCenter);
                return;
            }
            float x = panel.x + 20f, w = panel.width - 40f, gap = 16f;
            float cw = Mathf.Min(580f, (w - gap * (mates.Count - 1)) / mates.Count);
            float x0 = x + (w - (cw * mates.Count + gap * (mates.Count - 1))) / 2f;
            TipClear();  // hovering their weapon, an ability or an upgrade shows what it does (Tooltips.cs)
            for (int i = 0; i < mates.Count; i++)
                DrawMateCard(new Rect(x0 + i * (cw + gap), panel.y + 18f, cw, panel.height - 36f), mates[i], t + i * 1.3f);
            DrawTip();
        }

        void DrawMateCard(Rect card, Player p, float t)
        {
            var rc = ToColor(p.Def.Color);
            Ui.Pill(card, CardBg);
            // A live model of them, then who they are.
            var stage = new Rect(card.x + 14f, card.y + 14f, 104f, 104f);
            Ui.Pill(stage, new Color(rc.r * 0.12f + 0.02f, rc.g * 0.12f + 0.02f, rc.b * 0.12f + 0.03f, 0.95f));
            int weapon = 0;
            for (int k = 0; k < Defs.WeaponsPerRole; k++)
                if (Defs.Weapon(p.Role, p.Kit, k) == p.Weapon)
                    weapon = k;
            if (!statsZooming && BeginUiGl())
            {
                UiClip(stage);
                UiModelAt(new V2(stage.center.x, stage.center.y), 1.7f);
                DrawPreviewPlayer(p.Role, p.Kit, weapon, t);
                EndUiGl();
            }
            float ix = stage.xMax + 14f, iw = card.xMax - 14f - ix;
            Ui.Fancy(new Rect(ix, card.y + 12f, iw, 30f), p.Name, 22, Color.white, Color.Lerp(rc, Color.white, 0.4f), TextAnchor.MiddleLeft, rc);
            Ui.Label(new Rect(ix, card.y + 42f, iw, 18f), Defs.KitName(p.Kit) + " " + p.Def.Name + "  ·  Level " + p.Level, 13, rc, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(ix, card.y + 62f, iw, 16f), p.Downed ? "DOWNED" : Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp) + " HP", 11, p.Downed ? Ui.Danger : Ui.Muted);
            Ui.Bar(new Rect(ix, card.y + 80f, iw, 8f), p.MaxHp > 0f ? p.Hp / p.MaxHp : 0f, new Color(0.31f, 0.84f, 0.54f));
            var s = p.Run;
            Ui.Label(new Rect(ix, card.y + 94f, iw, 18f), Mathf.RoundToInt(s.DamageDealt).ToString("N0") + " damage  ·  " + s.Kills + " kills  ·  down " + s.Downs, 12, Ui.Text);

            // Weapon.
            float y = stage.yMax + 14f, x = card.x + 16f, w = card.width - 32f;
            float dh = Mathf.Min(48f, Ui.TextHeight(p.Weapon.Desc, 11, w));
            TipWeapon(new Rect(x - 6f, y + 14f, w + 12f, 24f + dh), p);
            Ui.Label(new Rect(x, y, w, 16f), "WEAPON", 10, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x, y + 16f, w, 20f), p.Weapon.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
            GUI.BeginGroup(new Rect(x, y + 37f, w, dh));
            Ui.Label(new Rect(0f, 0f, w, Ui.TextHeight(p.Weapon.Desc, 11, w)), p.Weapon.Desc, 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            GUI.EndGroup();
            y += 42f + dh;

            // Abilities, one row per key.
            Ui.Label(new Rect(x, y, w, 16f), "ABILITIES", 10, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 18f;
            for (int k = 0; k < Upgrades.SlotCount; k++)
            {
                var id = p.Slots[k].Id;
                TipAbility(new Rect(x - 6f, y, w + 12f, 24f), p, k);
                var key = new Rect(x, y + 3f, 46f, 18f);
                Ui.Pill(key, k == Upgrades.UltSlot ? new Color(0.38f, 0.3f, 0.14f) : new Color(0.22f, 0.26f, 0.39f));
                Ui.Label(key, SlotKeys[k], 9, Color.white, TextAnchor.MiddleCenter, true);
                if (id == AbilityId.None)
                    Ui.Label(new Rect(x + 56f, y, w - 56f, 24f), "—", 13, Ui.Muted);
                else
                {
                    Ui.Icon(new Rect(x + 56f, y + 4f, 16f, 16f), PixelArt.ForUpgrade(Upgrades.Ability(id).Name));
                    Ui.Label(new Rect(x + 78f, y, w - 140f, 24f), Upgrades.Ability(id).Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(x, y, w, 24f), "Lv " + Game.MasteryLevel(p, id), 11, new Color(0.56f, 0.9f, 1f), TextAnchor.MiddleRight);
                }
                y += 24f;
            }

            // Upgrades: rarest first, two columns, as many as fit.
            y += 8f;
            var counts = new Dictionary<int, int>();
            foreach (int u in p.Taken)
                counts[u] = counts.TryGetValue(u, out int c) ? c + 1 : 1;
            var ids = new List<int>(counts.Keys);
            ids.Sort((a, b) => Upgrades.All[b].Rarity != Upgrades.All[a].Rarity ? Upgrades.All[b].Rarity.CompareTo(Upgrades.All[a].Rarity) : a.CompareTo(b));
            Ui.Label(new Rect(x, y, w, 16f), "UPGRADES  ·  " + p.Taken.Count, 10, Ui.Gold, TextAnchor.MiddleLeft, true);
            y += 20f;
            const float rowH = 21f;
            int perCol = Mathf.Max(1, Mathf.FloorToInt((card.yMax - 12f - y) / rowH));
            float colW = w / 2f;
            int shown = Mathf.Min(ids.Count, perCol * 2);
            if (shown < ids.Count)
                shown = Mathf.Max(0, shown - 1);  // room for "+N more"
            for (int i = 0; i < shown; i++)
            {
                var u = Upgrades.All[ids[i]];
                float ux = x + (i / perCol) * colW, uy = y + (i % perCol) * rowH;
                TipUpgrade(new Rect(ux - 4f, uy, colW - 4f, rowH), p, u, counts[ids[i]]);
                Ui.Icon(new Rect(ux, uy + 2f, 16f, 16f), PixelArt.ForUpgrade(u.Name));
                Ui.Label(new Rect(ux + 22f, uy, colW - 60f, rowH), u.Name, 12, Color.Lerp(ToColor(Upgrades.RarityColor(u.Rarity)), Color.white, 0.45f));
                if (counts[ids[i]] > 1)
                    Ui.Label(new Rect(ux, uy, colW - 12f, rowH), "×" + counts[ids[i]], 11, Ui.Muted, TextAnchor.MiddleRight, true);
            }
            if (shown < ids.Count)
            {
                int i = shown;
                var rest = new List<string>();
                for (int j = shown; j < ids.Count; j++)
                    rest.Add(Upgrades.All[ids[j]].Name + (counts[ids[j]] > 1 ? " ×" + counts[ids[j]] : ""));
                TipMore(new Rect(x + (i / perCol) * colW - 4f, y + (i % perCol) * rowH, colW - 4f, rowH), rest);
                Ui.Label(new Rect(x + (i / perCol) * colW + 22f, y + (i % perCol) * rowH, colW - 30f, rowH), "+ " + (ids.Count - shown) + " more", 12, Ui.Muted);
            }
        }
    }
}

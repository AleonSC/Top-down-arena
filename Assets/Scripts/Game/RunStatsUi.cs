using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Run stats screen: hold Tab in a match (solo, that pauses it) or press Run stats in the pause menu (so a
    /// paused party can look together). Three tabs: Run so far (wave, route, the run's numbers, the party's share),
    /// Your build (your loadout, HudTab.cs) and Team (each teammate's build). The numbers come from each
    /// player's RunStats, which the host sends everyone about once a second (Msg.RunStatsLive). Drawn as hologram panels over
    /// the dimmed arena (TabHolo.cs; look B of Docs/Mockups/crt-tab-menu.html): your stats down the left, the page's panels to the
    /// right, the tab keys above; the panels switch on when the screen opens and when the page changes.
    public partial class ArenaApp
    {
        int statsTab;          // 0 Run so far, 1 Your build, 2 Team (kept between openings)
        bool runStatsOpen;     // opened from the pause menu

        static readonly Color CardBg = new Color(0.094f, 0.11f, 0.165f, 1f);  // (the old card colour; other screens use it)

        int statsShown = -1;   // the page last drawn (a change switches the panels on again)
        int statsLastFrame = -10;  // the frame the screen was last drawn (a gap means it has just opened)

        void DrawRunStatsScreen(Player me, bool fromPause)
        {
            float t = Time.unscaledTime;
            if (Time.frameCount - statsLastFrame > 2)  // just opened: everything switches on
            {
                holoOpenAt = holoPageAt = t;
                statsShown = statsTab;
            }
            statsLastFrame = Time.frameCount;
            if (statsTab != statsShown)
            {
                statsShown = statsTab;
                holoPageAt = t;  // a new page: its panels switch on (your stats stay)
            }
            RoundBackdrop(TabDark);  // (RoundWindows.cs)
            DrawHoloBeam();
            if (statsTab == 1 && me != null)
                DrawTabMenu(me, false);  // Your build (HudTab.cs)
            else
            {
                if (me != null)
                    DrawHoloStats(me);
                if (statsTab == 0)
                    DrawRunSoFar(me);
                else
                    DrawTeam(me, t);
            }

            // The tab keys, over the page; Back (from the pause menu) or how to close, on the right.
            bool calm = HoloCalm;
            int mates = game.Players.Count - (me != null ? 1 : 0);
            string[] names = { "RUN SO FAR", "YOUR BUILD", "TEAM" + (mates > 0 ? " (" + mates + ")" : "") };
            const float tw = 168f, gap = 8f;
            float tx = 420f;
            for (int i = 0; i < 3; i++)
                if (LobbyKit.Key(new Rect(tx + i * (tw + gap), 12f, tw, 32f), names[i], statsTab == i, calm, size: 15, minSize: 11))
                    statsTab = i;
            if (fromPause)
            {
                if (LobbyKit.Key(new Rect(W - 24f - 38f, 9f, 38f, 38f), "X", false, calm, size: 18))
                    runStatsOpen = false;
            }
            else
            {
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                CrtUi.Text(new Rect(W - 340f, 14f, 316f, 28f), tabPaused ? "PAUSED WHILE YOU LOOK · LET GO OF TAB" : "LET GO OF TAB TO CLOSE", 17, HoloDim, TextAnchor.MiddleRight, false, calm);
                Ui.CrtFont = was;
            }
        }

        // --- Run so far ----------------------------------------------------------------------

        void DrawRunSoFar(Player me)
        {
            float x = HoloPageX, w = HoloPageR - HoloPageX;
            bool practice = game.Practice != null;
            int wave = Mathf.Max(0, game.Wave);
            var s = me != null ? me.Run : new RunStats();

            // The header: the wave and where you are, then the time, the score and the next boss.
            var head = new Rect(x, HoloTop, w, HoloHeadH);
            HoloPanel(head, holoPageAt, () =>
            {
                string waveText = practice ? "PRACTICE" : game.Hunt != null ? "HUNT WAVE " + wave : "WAVE " + game.ShownWave;
                string of = practice ? "" : game.Endless ? " ENDLESS" : " / " + game.ShownLastWave;
                string where;
                Color whereC = HoloDim;
                if (practice)
                    where = "Practice Range";
                else if (game.BossNow)
                {
                    where = "Boss fight";
                    whereC = new Color(1f, 0.6f, 0.65f);
                }
                else if (game.CurrentPath != PathId.None && wave >= game.PathFrom && wave <= game.PathTo)
                {
                    where = Game.PathName(game.CurrentPath) + " path · wave " + (game.ShownWave - game.ShownStart(game.PathFrom) + 1) + " of " + (game.ShownEnd(game.PathTo) - game.ShownStart(game.PathFrom) + 1);
                    whereC = Color.Lerp(ToColor(Paths.Get(game.CurrentPath).Accent), Color.white, 0.3f);
                }
                else if (game.Hunt != null)
                    where = wave == 0 ? "Getting ready" : game.Hunt.Name + " · " + BossName(game.Hunt.Boss) + "'s own enemies";
                else if (game.Quick)
                    where = wave == 0 ? "Getting ready" : "Quick Play";
                else
                    where = wave == 0 ? "Getting ready" : "Plain arena";
                where += " · " + (game.State == MatchState.Fighting ? game.Remaining + " enemies left" : "between waves");
                HoloTitle(head.x + 18f, head.y + 10f, waveText + of, where.ToUpperInvariant(), whereC);
                var mods = new List<string>();
                foreach (var m in Modifiers.All)
                    if (game.Has(m.Mod))
                        mods.Add(m.Name);
                if (mods.Count > 0)
                    Ui.Label(new Rect(head.x + 18f, head.y + 66f, 520f, 15f), LobbyKit.Truncate("MODIFIERS: " + string.Join(", ", mods.ToArray()).ToUpperInvariant(), 10, 520f), 10, new Color(0.84f, 0.72f, 1f), TextAnchor.MiddleLeft, true);
                // Tiles from the right: the next boss, the score, the time played.
                float right = head.xMax - 16f, ty = head.y + 16f;
                if (!practice)
                {
                    int nextBoss = game.NextBossWave(wave, game.State == MatchState.Fighting);
                    string boss = BossName(game.BossAt(nextBoss));
                    float bw = Mathf.Max(130f, CrtUi.TitleWidth(boss, 24) + 24f);
                    HoloTile(new Rect(right - bw, ty, bw, 56f), boss, game.BossNow && game.State == MatchState.Fighting ? "boss now" : "next boss · after wave " + nextBoss, new Color(1f, 0.45f, 0.52f), 24, TextAnchor.MiddleCenter);
                    right -= bw + 8f;
                }
                HoloTile(new Rect(right - 110f, ty, 110f, 56f), game.Score.ToString("N0"), "score", null, 24, TextAnchor.MiddleCenter);
                right -= 118f;
                int secs = Mathf.FloorToInt(game.RunTime);
                HoloTile(new Rect(right - 96f, ty, 96f, 56f), (secs / 60) + ":" + (secs % 60).ToString("00"), "played", null, 24, TextAnchor.MiddleCenter);
            });

            // The run's numbers: eight tiles, four by two.
            var tilesR = new Rect(x, HoloTop + HoloHeadH + HoloGap, w, 170f);
            HoloPanel(tilesR, holoPageAt, () =>
            {
                string[,] tiles =
                {
                    { "Damage dealt", Mathf.RoundToInt(s.DamageDealt).ToString("N0") }, { "Kills", s.Kills.ToString("N0") },
                    { "Healing done", Mathf.RoundToInt(s.HealingDone).ToString("N0") }, { "Accuracy", s.ShotsFired > 0 ? Mathf.RoundToInt(s.Accuracy * 100f) + "%" : "—" },
                    { "Biggest hit", Mathf.RoundToInt(s.BiggestHit).ToString("N0") }, { "Level", me != null ? me.Level.ToString() : "—" },
                    { "Downed", s.Downs.ToString() }, { "Allies revived", s.Revives.ToString() },
                };
                float tg = 8f, tw = (tilesR.width - 32f - 3f * tg) / 4f, th = (tilesR.height - 32f - tg) / 2f;
                for (int i = 0; i < 8; i++)
                    HoloTile(new Rect(tilesR.x + 16f + (i % 4) * (tw + tg), tilesR.y + 16f + (i / 4) * (th + tg), tw, th), tiles[i, 1], tiles[i, 0]);
            });

            // Two columns: the route, then your fight and the party's share.
            float y = tilesR.yMax + HoloGap, colW = (w - HoloGap) / 2f;
            var routeR = new Rect(x, y, colW, HoloBottom - y);
            var fightR = new Rect(x + colW + HoloGap, y, colW, HoloBottom - y);
            HoloPanel(routeR, holoPageAt, () => DrawRouteCard(routeR, practice));
            HoloPanel(fightR, holoPageAt, () => DrawFightCard(fightR, s));
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
            int cur = between ? (game.BossNext ? wave : game.NextTrackWave) : wave;  // the regular wave being played, or the one coming up (the boss keeps its wave's number)
            int shownCur = between && !game.BossNext ? game.ShownWave + 1 : game.ShownWave;  // (as the player counts: a Campaign's 1-100)
            bool bossFight = game.BossSlot && game.State == MatchState.Fighting;
            var plain = new Color(0.48f, 0.52f, 0.62f);
            if (game.Hunt != null)
            {
                // A Boss Hunt: its nine build-up waves, then its boss.
                int last = Hunts.Length;
                int st0 = cur >= last ? 0 : 1;
                rows.Add(new RouteRow
                {
                    Name = game.Hunt.Name, Waves = "waves 1–" + (last - 1),
                    Note = st0 == 0 ? "cleared" : "now · " + cur + " of " + (last - 1),
                    Color = new Color(0.95f, 0.42f, 0.32f), State = st0,
                });
                int bs0 = cur > last ? 0 : cur == last ? 1 : 2;
                rows.Add(new RouteRow
                {
                    Name = BossName(game.Hunt.Boss), Waves = "wave " + last, Boss = true, State = game.State == MatchState.Victory ? 0 : bs0,
                    Note = game.State == MatchState.Victory ? "beaten" : bs0 == 1 ? "fighting now" : "in " + (last - wave) + " wave" + (last - wave == 1 ? "" : "s"),
                    Color = new Color(0.89f, 0.33f, 0.42f),
                });
                return rows;
            }
            int every = game.Quick ? QuickPlay.Length : 5;  // regular waves between bosses (Quick Play: ten)
            for (int k = 0; k < 40; k++)
            {
                int from = k * every + 1, to = k * every + every, bossWave = k * every + every;
                if (from <= cur)
                {
                    var id = k == 0 || k - 1 >= game.Route.Count ? PathId.None : game.Route[k - 1];
                    int st = cur > to || cur == to && game.BossSlot ? 0 : 1;
                    rows.Add(new RouteRow
                    {
                        Name = game.Quick ? "Quick Play" : k == 0 ? "Standard" : Game.PathName(id),
                        Waves = "waves " + game.ShownStart(from) + "–" + game.ShownEnd(to),
                        Note = st == 0 ? "cleared" : "now · " + (shownCur - game.ShownStart(from) + 1) + " of " + (game.ShownEnd(to) - game.ShownStart(from) + 1),
                        Color = id == PathId.None ? plain : ToColor(Paths.Get(id).Accent),
                        State = st,
                    });
                }
                int bs = cur > bossWave ? 0 : bossFight && cur == bossWave ? 1 : 2;
                rows.Add(new RouteRow
                {
                    Name = BossName(game.BossAt(bossWave)), Waves = "after wave " + game.ShownEnd(bossWave), Boss = true, State = bs,
                    Note = bs == 0 ? "beaten" : bs == 1 ? "fighting now" : game.ShownEnd(bossWave) - game.ShownWave <= 0 ? "next up" : "after wave " + game.ShownEnd(bossWave),
                    Color = new Color(0.89f, 0.33f, 0.42f),
                });
                if (bs != 0)
                    break;
            }
            return rows;
        }

        void DrawRouteCard(Rect card, bool practice)
        {
            bool calm = HoloCalm;
            float x = card.x + 16f, w = card.width - 32f;
            if (practice)
            {
                LobbyKit.Heading(x, card.y + 8f, w, "Route", null, calm);
                Ui.Label(card, "The Practice Range plays your own waves: no route.", 13, HoloDim, TextAnchor.MiddleCenter);
                return;
            }
            var rows = BuildRoute();
            int beaten = 0;
            foreach (var r in rows)
                if (r.Boss && r.State == 0)
                    beaten++;
            LobbyKit.Heading(x, card.y + 8f, w, "Route", game.Hunt != null ? (beaten > 0 ? "hunt won" : "the hunt") : game.Quick ? beaten + (beaten == 1 ? " boss" : " bosses") + " beaten" : beaten + " of " + Game.MaxWave / 5 + " bosses beaten", calm);
            // Keep the newest rows when the run outgrows the panel.
            float top = card.y + 46f, avail = card.yMax - 12f - top;
            int fit = Mathf.Max(1, Mathf.FloorToInt(avail / 24f));
            int first = Mathf.Max(0, rows.Count - fit);
            float rowH = Mathf.Min(28f, avail / Mathf.Max(1, rows.Count - first));
            var ink = CrtUi.Ink;
            for (int i = first; i < rows.Count; i++)
            {
                var r = rows[i];
                var row = new Rect(x - 6f, top + (i - first) * rowH, w + 12f, rowH - 2f);
                if (r.State == 1)
                {
                    Ui.Fill(row, new Color(ink.r, ink.g, ink.b, 0.16f));
                    CrtKit.Frame(row, 1f, ink);
                }
                float cyy = row.center.y;
                Color c = r.State == 2 ? new Color(r.Color.r, r.Color.g, r.Color.b, 0.4f) : r.Color;
                Ui.Dot(new Rect(row.x + 10f, cyy - 5f, 10f, 10f), c, r.Boss && r.State == 2);
                Color name = r.State == 2 ? HoloDim : r.Boss ? new Color(1f, 0.7f, 0.73f) : (r.State == 1 ? Color.white : HoloBody);
                Ui.Label(new Rect(row.x + 28f, row.y, 170f, row.height), r.Name, 13, name, TextAnchor.MiddleLeft, r.State == 1);
                bool was = Ui.CrtFont;
                Ui.CrtFont = true;
                // the note at the right; the waves take what's left of the space between the name and it
                string note = r.Note.ToUpperInvariant();
                float noteW = Mathf.Min(row.width - 210f, Ui.TextWidth(note, 16, false));
                float wavesW = row.xMax - 10f - noteW - 10f - (row.x + 200f);
                CrtUi.Text(new Rect(row.x + 200f, row.y, Mathf.Max(0f, wavesW), row.height), LobbyKit.Truncate(r.Waves.ToUpperInvariant(), 16, Mathf.Max(0f, wavesW), false), 16, HoloDim, TextAnchor.MiddleLeft, false, calm);
                CrtUi.Text(new Rect(row.xMax - 10f - noteW, row.y, noteW, row.height), LobbyKit.Truncate(note, 16, noteW, false), 16, r.State == 1 ? ink : HoloDim, TextAnchor.MiddleRight, false, calm);
                Ui.CrtFont = was;
            }
        }

        /// Your fight (five numbers), then each player's share of the party's damage.
        void DrawFightCard(Rect card, RunStats s)
        {
            bool calm = HoloCalm;
            float x = card.x + 16f, w = card.width - 32f, y = card.y + 8f;
            LobbyKit.Heading(x, y, w, "Your fight", null, calm);
            string[,] rows =
            {
                { "Damage taken", Mathf.RoundToInt(s.DamageTaken).ToString("N0") },
                { "Elite kills", s.EliteKills.ToString("N0") },
                { "Boss kills", s.BossKills.ToString("N0") },
                { "Shots that hit", s.ShotsHit.ToString("N0") + " of " + s.ShotsFired.ToString("N0") },
                { "Abilities used", s.AbilitiesUsed.ToString("N0") },
            };
            HoloRows(x, y + 38f, w, rows);
            y += 38f + 5f * 26f + 12f;

            float total = 0f;
            foreach (var p in game.Players.Values)
                total += p.Run.DamageDealt;
            LobbyKit.Heading(x, y, w, "Party share of damage", game.Players.Count < 2 ? "solo" : null, calm);
            y += 38f;
            int n = Mathf.Max(1, game.Players.Count);
            float rowH = Mathf.Min(34f, (card.yMax - 10f - y) / n);
            foreach (var p in game.Players.Values)
            {
                var rc = ToColor(p.Def.Color);
                var ps = p.Run;
                float share = total > 0f ? ps.DamageDealt / total : 0f;
                Ui.Dot(new Rect(x, y + 4f, 10f, 10f), rc);
                float nw = Ui.TextWidth(p.Name, 13, true) - 6f;
                Ui.Label(new Rect(x + 16f, y, nw + 8f, 18f), p.Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(x + 22f + nw, y, 170f, 18f), Defs.KitName(p.Kit) + " " + p.Def.Name, 12, rc);
                CrtUi.Title(new Rect(x, y - 2f, w, 20f), Mathf.RoundToInt(share * 100f) + "%", 20, Color.white, TextAnchor.MiddleRight, true);
                HoloBar(new Rect(x, y + 20f, w, 6f), share, rc);
                y += rowH;
            }
        }

        // --- Team --------------------------------------------------------------------------

        void DrawTeam(Player me, float t)
        {
            var mates = new List<Player>();
            foreach (var p in game.Players.Values)
                if (me == null || p.Id != me.Id)
                    mates.Add(p);
            float x = HoloPageX, w = HoloPageR - HoloPageX;
            if (mates.Count == 0)
            {
                var r = new Rect(x, HoloTop, w, HoloHeadH);
                HoloPanel(r, holoPageAt, () => Ui.Label(r, "You're playing solo: your build is on the Your build tab.", 15, HoloBody, TextAnchor.MiddleCenter));
                return;
            }
            float cw = (w - HoloGap * (mates.Count - 1)) / mates.Count;
            TipClear();  // hovering their weapon, an ability or an upgrade shows what it does (Tooltips.cs)
            for (int i = 0; i < mates.Count; i++)
            {
                var card = new Rect(x + i * (cw + HoloGap), HoloTop, cw, HoloBottom - HoloTop);
                var p = mates[i];
                float mt = t + i * 1.3f;
                HoloPanel(card, holoPageAt, () => DrawMateCard(card, p, mt));
            }
            DrawTip();
        }

        void DrawMateCard(Rect card, Player p, float t)
        {
            bool calm = HoloCalm;
            var rc = ToColor(p.Def.Color);
            // A live model of them on a hatched island in their colour, then who they are.
            var stage = new Rect(card.x + 16f, card.y + 16f, 92f, 92f);
            Ui.Fill(stage, new Color(rc.r * 0.12f + 0.02f, rc.g * 0.12f + 0.02f, rc.b * 0.12f + 0.03f, 0.95f));
            for (float d = -stage.height; d < stage.width; d += 12f)  // the hatching: short diagonal dashes
                for (float k = 0f; k < stage.height; k += 3f)
                {
                    float hx = stage.x + d + k;
                    if (hx >= stage.x && hx < stage.xMax - 2f)
                        Ui.Fill(new Rect(hx, stage.yMax - k - 3f, 2f, 3f), new Color(rc.r, rc.g, rc.b, 0.1f));
                }
            CrtKit.Frame(stage, 2f, rc);
            int weapon = 0;
            for (int k = 0; k < Defs.WeaponCount(p.Role, p.Kit); k++)
                if (Defs.Weapon(p.Role, p.Kit, k) == p.Weapon)
                    weapon = k;
            if (!holoBusy && BeginUiGl())
            {
                UiClip(stage);
                UiModelAt(new V2(stage.center.x, stage.center.y), 1.6f);
                DrawPreviewPlayer(p.Role, p.Kit, weapon, t);
                EndUiGl();
            }
            float ix = stage.xMax + 14f, iw = card.xMax - 16f - ix;
            Ui.Label(new Rect(ix, card.y + 14f, iw, 26f), p.Name, 22, rc, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(ix, card.y + 40f, iw, 16f), (Defs.KitName(p.Kit) + " " + p.Def.Name + " · Level " + p.Level).ToUpperInvariant(), 11, Color.Lerp(rc, Color.white, 0.2f), TextAnchor.MiddleLeft, true);
            bool was = Ui.CrtFont;
            Ui.CrtFont = true;
            CrtUi.Text(new Rect(ix, card.y + 58f, iw, 16f), "HEALTH", 16, HoloDim, TextAnchor.MiddleLeft, false, calm);
            CrtUi.Text(new Rect(ix, card.y + 58f, iw, 16f), p.Downed ? "DOWNED" : Mathf.CeilToInt(p.Hp) + " / " + Mathf.CeilToInt(p.MaxHp), 16, p.Downed ? CrtStyle.Danger : Color.white, TextAnchor.MiddleRight, false, calm);
            HoloBar(new Rect(ix, card.y + 76f, iw, 8f), p.MaxHp > 0f ? p.Hp / p.MaxHp : 0f, Ui.Good);
            var s = p.Run;
            CrtUi.Text(new Rect(ix, card.y + 88f, iw, 18f), (Mathf.RoundToInt(s.DamageDealt).ToString("N0") + " damage · " + s.Kills + " kills · down " + s.Downs).ToUpperInvariant(), 17, CrtUi.Ink, TextAnchor.MiddleLeft, false, calm);
            Ui.CrtFont = was;

            // Weapon.
            float y = stage.yMax + 12f, x = card.x + 16f, w = card.width - 32f;
            LobbyKit.Heading(x, y, w, "Weapon", null, calm);
            y += 34f;
            float dh = Mathf.Min(42f, Ui.TextHeight(p.Weapon.Desc, 11, w));
            TipWeapon(new Rect(x - 6f, y - 2f, w + 12f, 22f + dh), p);
            Ui.Label(new Rect(x, y, w, 20f), p.Weapon.Name, 15, Color.white, TextAnchor.MiddleLeft, true);
            GUI.BeginGroup(new Rect(x, y + 20f, w, dh));
            Ui.Label(new Rect(0f, 0f, w, Ui.TextHeight(p.Weapon.Desc, 11, w)), p.Weapon.Desc, 11, HoloDesc, TextAnchor.UpperLeft, false, true);
            GUI.EndGroup();
            y += 26f + dh;

            // Abilities, one row per key.
            LobbyKit.Heading(x, y, w, "Abilities", null, calm);
            y += 34f;
            for (int k = 0; k < Upgrades.SlotCount; k++)
            {
                var id = p.Slots[k].Id;
                TipAbility(new Rect(x - 6f, y, w + 12f, 22f), p, k);
                bool ult = k == Upgrades.UltSlot;
                DrawCrtCap(new Rect(x, y + 3f, 0f, 16f), ult ? "ULT · " + SlotKeys[4] : SlotKeys[k], ult ? CrtStyle.Gold : CrtUi.Ink);
                if (id == AbilityId.None)
                    Ui.Label(new Rect(x + 80f, y, w - 80f, 22f), "—", 13, HoloDim);
                else
                {
                    Ui.Icon(new Rect(x + 80f, y + 3f, 16f, 16f), PixelArt.ForUpgrade(Upgrades.Ability(id).Name));
                    Ui.Label(new Rect(x + 102f, y, w - 160f, 22f), Upgrades.Ability(id).Name, 13, Color.white, TextAnchor.MiddleLeft, true);
                    bool w2 = Ui.CrtFont;
                    Ui.CrtFont = true;
                    CrtUi.Text(new Rect(x, y, w, 22f), "LV " + Game.MasteryLevel(p, id), 16, HoloDim, TextAnchor.MiddleRight, false, calm);
                    Ui.CrtFont = w2;
                }
                y += 22f;
            }

            // Upgrades: rarest first, two columns, as many as fit.
            y += 6f;
            var counts = new Dictionary<int, int>();
            foreach (int u in p.Taken)
                counts[u] = counts.TryGetValue(u, out int c) ? c + 1 : 1;
            var ids = new List<int>(counts.Keys);
            ids.Sort((a, b) => Upgrades.All[b].Rarity != Upgrades.All[a].Rarity ? Upgrades.All[b].Rarity.CompareTo(Upgrades.All[a].Rarity) : a.CompareTo(b));
            LobbyKit.Heading(x, y, w, "Upgrades", p.Taken.Count.ToString(), calm);
            y += 36f;
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
                    CrtUi.Title(new Rect(ux, uy, colW - 12f, rowH), "×" + counts[ids[i]], 17, CrtStyle.Gold, TextAnchor.MiddleRight, calm);
            }
            if (shown < ids.Count)
            {
                int i = shown;
                var rest = new List<string>();
                for (int j = shown; j < ids.Count; j++)
                    rest.Add(Upgrades.All[ids[j]].Name + (counts[ids[j]] > 1 ? " ×" + counts[ids[j]] : ""));
                TipMore(new Rect(x + (i / perCol) * colW - 4f, y + (i % perCol) * rowH, colW - 4f, rowH), rest);
                Ui.Label(new Rect(x + (i / perCol) * colW + 22f, y + (i % perCol) * rowH, colW - 30f, rowH), "+ " + (ids.Count - shown) + " more", 12, HoloDim);
            }
        }
    }
}

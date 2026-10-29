using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Arena
{
    /// The F1 developer menu: session recording, the aim debug overlay, the bot, jumping to a
    /// wave, and editing each player's stats by hand. Changes to the match are host only.
    public partial class ArenaApp
    {
        bool devMenuOpen;
        int devTab = -1;          // 0 Match, 1 Players, 2 Tools (-1: pick one when the menu opens)
        int devWave = 1;          // the wave the "Go" button jumps to
        int devPlayer;            // whose stats are shown (player id; 0 = you)
        float devScroll;          // stat list scroll offset
        string devFocus = "";     // the stat field being typed in, and what's typed so far
        string devBuffer = "";
        string devFilter = "";    // only show stats whose name contains this

        static readonly Rect DevPanel = new Rect(W / 2f - 470f, 40f, 940f, 640f);

        static readonly string[] DevTabNames = { "Match", "Players", "Tools" };
        static readonly string[] DevTabBlurbs = { "Waves and bosses", "Stats, god mode, levels", "Recording, bots, Boss Lab" };
        static readonly Color[] DevTabColors = { new Color(1f, 0.55f, 0.4f), new Color(0.45f, 0.75f, 1f), new Color(1f, 0.8f, 0.35f) };

        void DrawDevMenu()
        {
            float t = Time.unscaledTime;
            bool inMatch = session.Screen == AppScreen.Game;
            if (devTab < 0)
                devTab = inMatch ? 0 : 2;
            var panel = DevPanel;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.55f));
            Ui.Panel(panel, true);

            // Header.
            Ui.Fancy(new Rect(panel.x + 24f, panel.y + 14f, 400f, 36f), "DEVELOPER TOOLS", 26, Color.white, new Color(0.7f, 0.8f, 1f), TextAnchor.MiddleLeft, Ui.Accent);
            Ui.Label(new Rect(panel.x + 26f, panel.y + 48f, 500f, 16f), (inMatch ? "In a match" : "Not in a match") + (session.IsHost ? "" : "  ·  only the host can change the match") + "  ·  F1 or Esc to close",
                11, Ui.Muted);
            if (Ui.Button(new Rect(panel.xMax - 52f, panel.y + 16f, 32f, 30f), "X", Ui.Danger))
                devMenuOpen = false;
            Ui.Fill(new Rect(panel.x + 20f, panel.y + 74f, panel.width - 40f, 1f), new Color(1f, 1f, 1f, 0.07f));

            // Sidebar.
            float sx = panel.x + 20f, sw = 196f, sy = panel.y + 90f;
            for (int i = 0; i < DevTabNames.Length; i++)
            {
                var r = new Rect(sx, sy + i * 66f, sw, 58f);
                bool on = devTab == i, hover = r.Contains(Event.current.mousePosition);
                var c = DevTabColors[i];
                Ui.Pill(r, on ? new Color(c.r * 0.35f, c.g * 0.35f, c.b * 0.35f, 0.95f) : new Color(1f, 1f, 1f, hover ? 0.08f : 0.035f));
                if (on)
                    Ui.Fill(new Rect(r.x, r.y + 8f, 4f, r.height - 16f), c);
                Ui.Fancy(new Rect(r.x + 16f, r.y + 7f, r.width - 20f, 26f), DevTabNames[i], 17, Color.white, on ? Color.Lerp(c, Color.white, 0.4f) : new Color(0.8f, 0.84f, 0.95f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(r.x + 16f, r.y + 32f, r.width - 20f, 18f), DevTabBlurbs[i], 11, on ? new Color(1f, 1f, 1f, 0.75f) : Ui.Muted);
                if (!on && GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    devTab = i;
                    devFocus = "";
                }
            }
            DrawDevStatusCard(new Rect(sx, panel.yMax - 136f, sw, 116f), inMatch);

            // Content.
            var body = new Rect(sx + sw + 20f, panel.y + 90f, panel.xMax - 24f - (sx + sw + 20f), panel.height - 110f);
            switch (devTab)
            {
                case 0:
                    if (inMatch) DrawDevMatch(body, t); else DevNotInMatch(body, "change the wave");
                    break;
                case 1:
                    if (inMatch) DrawDevPlayers(body.x, body.y, body.width, body.yMax); else DevNotInMatch(body, "edit players' stats");
                    break;
                default:
                    DrawDevTools(body);
                    break;
            }
        }

        void DevNotInMatch(Rect body, string what)
        {
            Ui.Pill(new Rect(body.x, body.y, body.width, 90f), new Color(1f, 1f, 1f, 0.03f));
            Ui.Label(new Rect(body.x, body.y + 18f, body.width, 24f), "Not in a match", 16, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(body.x, body.y + 46f, body.width, 20f), "Start or join a match to " + what + ".", 13, Ui.Muted, TextAnchor.MiddleCenter);
        }

        /// Bottom of the sidebar: what's on right now.
        void DrawDevStatusCard(Rect r, bool inMatch)
        {
            Ui.Pill(r, new Color(0f, 0f, 0f, 0.3f));
            float y = r.y + 10f;
            Action<string, string, Color> line = (k, v, c) =>
            {
                Ui.Label(new Rect(r.x + 12f, y, 90f, 18f), k, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 12f, y, r.width - 24f, 18f), v, 11, c, TextAnchor.MiddleRight, true);
                y += 20f;
            };
            line("WAVE", inMatch ? Mathf.Max(1, game.Wave) + (game.Endless ? " (endless)" : " / " + Game.MaxWave) : "-", Color.white);
            line("RECORDING", recordSessions ? (game.Recorder != null ? "on, recording" : "on") : "off", recordSessions ? new Color(1f, 0.5f, 0.5f) : Ui.Muted);
            line("BOT", game.Bot ? "playing for you" : "off", game.Bot ? Ui.Good : Ui.Muted);
            line("AIM DEBUG", aimDebug ? "on" : "off", aimDebug ? Ui.Accent : Ui.Muted);
            line("PLAYERS", session.Lobby.Count.ToString(), Color.white);
        }

        /// A titled card for the content area; returns the y to draw its contents at.
        float DevCard(Rect r, string title, string blurb, Color accent)
        {
            Ui.Card(r, accent, 0.35f);
            Ui.Fill(new Rect(r.x + 1f, r.y + 10f, 3f, 24f), accent);
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 22f), title, 15, Color.white, TextAnchor.MiddleLeft, true);
            if (blurb.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 30f, r.width - 32f, 18f), blurb, 11, Ui.Muted);
            return r.y + (blurb.Length > 0 ? 56f : 40f);
        }

        // --- Match ------------------------------------------------------------------------

        void DrawDevMatch(Rect body, float t)
        {
            var card = new Rect(body.x, body.y, body.width, 250f);
            float y = DevCard(card, "Jump to a wave", "Clears the arena and starts that wave in 2 s. Downed players get back up.", DevTabColors[0]);
            float x = card.x + 16f;
            Ui.Label(new Rect(x, y, 300f, 20f), "Now on wave " + Mathf.Max(1, game.Wave) + (game.Endless ? " (endless)" : " of " + Game.MaxWave), 13, Ui.Text);
            y += 30f;
            int max = game.Endless ? 999 : Game.MaxWave;
            bool host = session.IsHost;
            bool wasEnabled = GUI.enabled;
            GUI.enabled = host;
            float bx = x;
            foreach (int step in new[] { -10, -5, -1, 0, 1, 5, 10 })
            {
                if (step == 0)
                {
                    Ui.Fancy(new Rect(bx, y, 70f, 40f), devWave.ToString(), 28, Color.white, Ui.Gold, TextAnchor.MiddleCenter, Ui.Gold);
                    bx += 74f;
                    continue;
                }
                if (Ui.Button(new Rect(bx, y + 4f, 42f, 32f), step > 0 ? "+" + step : step.ToString()))
                    devWave = Mathf.Clamp(devWave + step, 1, max);
                bx += 46f;
            }
            y += 54f;
            if (Ui.Button(new Rect(x, y, 300f, 40f), "Go to wave " + devWave, Ui.Primary))
                game.DevJumpToWave(devWave);
            GUI.enabled = wasEnabled;
            if (!host)
                Ui.Label(new Rect(x, y + 44f, 300f, 18f), "Only the host can change the wave.", 11, Ui.Muted);

            // What's waiting on that wave: the boss, drawn live, or the next one.
            var show = new Rect(card.xMax - 230f, card.y + 52f, 210f, 184f);
            Ui.Pill(show, new Color(0f, 0f, 0f, 0.35f));
            int bossWave = devWave % 5 == 0 ? devWave : (devWave / 5 + 1) * 5;
            var boss = Game.BossForWave(bossWave);
            if (BeginUiGl())
            {
                DrawEnemyPreview(boss == EnemyType.TwinB ? EnemyType.TwinA : boss, new V2(show.center.x, show.y + 78f), 120f, t, true);
                EndUiGl();
            }
            Ui.Label(new Rect(show.x, show.yMax - 50f, show.width, 18f), devWave % 5 == 0 ? "BOSS ON THIS WAVE" : "NEXT BOSS  ·  WAVE " + bossWave, 10, Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Fancy(new Rect(show.x, show.yMax - 32f, show.width, 24f), PracticePlan.NameOf(boss == EnemyType.TwinB ? EnemyType.TwinA : boss), 16, Color.white, Ui.Gold, TextAnchor.MiddleCenter);

            // Quick actions for everyone.
            var quick = new Rect(body.x, card.yMax + 14f, body.width, 120f);
            y = DevCard(quick, "Everyone", "Applies to every player in the match.", DevTabColors[0]);
            GUI.enabled = host;
            float qw = (quick.width - 32f - 24f) / 4f;
            string[] names = { "Full heal", "Level up", "Max mastery", "God mode on/off" };
            for (int i = 0; i < names.Length; i++)
                if (Ui.Button(new Rect(quick.x + 16f + i * (qw + 8f), y, qw, 34f), names[i], i == 3 ? Ui.GoldButton : Ui.Neutral))
                    foreach (var p in game.Players.Values)
                    {
                        if (i == 0) game.DevHeal(p.Id);
                        else if (i == 1) game.DevLevelUp(p.Id);
                        else if (i == 2) game.DevMasterAll(p.Id);
                        else game.DevSetGod(p.Id, !p.DevGod);
                    }
            GUI.enabled = wasEnabled;
        }

        // --- Tools ------------------------------------------------------------------------

        void DrawDevTools(Rect body)
        {
            float half = (body.width - 14f) / 2f;
            // Recording.
            var rec = new Rect(body.x, body.y, half, 200f);
            float y = DevCard(rec, "Session recording", "Saves each match you host as JSON for balancing.", new Color(1f, 0.45f, 0.45f));
            bool on = Ui.Toggle(new Rect(rec.x + 16f, y, rec.width - 32f, 26f), recordSessions, "Record sessions (when hosting)");
            if (on != recordSessions)
            {
                recordSessions = on;
                PlayerPrefs.SetInt("record_sessions", on ? 1 : 0);
                PlayerPrefs.Save();
                ApplyRecording();
            }
            y += 34f;
            if (Ui.Button(new Rect(rec.x + 16f, y, 190f, 30f), "Open sessions folder"))
                OpenFolder(SessionsFolder);
            Ui.Label(new Rect(rec.x + 16f, y + 36f, rec.width - 32f, 18f), "Summarise them with Tools/SessionReport", 11, Ui.Muted);

            // Overlays and the bot.
            var ov = new Rect(body.x + half + 14f, body.y, half, 200f);
            y = DevCard(ov, "Overlays and helpers", "Just for you, on this computer.", Ui.Accent);
            aimDebug = Ui.Toggle(new Rect(ov.x + 16f, y, ov.width - 32f, 26f), aimDebug, "Aim debug overlay  (F3)");
            y += 32f;
            bool bot = Ui.Toggle(new Rect(ov.x + 16f, y, ov.width - 32f, 26f), game.Bot, "Bot plays for you");
            if (bot != game.Bot)
            {
                game.Bot = bot;
                game.BotLevel = bot ? BotSkill.Good : BotSkill.Off;  // the Boss Lab's smart bot; the sim tests keep the simple one
            }
            y += 32f;
            bool editText = Ui.Toggle(new Rect(ov.x + 16f, y, ov.width - 32f, 26f), textEditMode, "Edit any text on screen  (F4)");
            if (editText != textEditMode)
            {
                SetTextEditMode(editText);
                devMenuOpen = !editText;  // out of the way, so you can click the text you want
            }
            y += 32f;
            if (Ui.Button(new Rect(ov.x + 16f, y, 190f, 30f), "Open practice folder"))
                OpenFolder(PracticeFolder);

            // Boss Lab.
            var lab = new Rect(body.x, body.y + 214f, body.width, 150f);
            y = DevCard(lab, "Boss Lab", "Watch bots that play like people fight any boss, with any party, at up to 8x speed, and see what hurts them.", Ui.Gold);
            if (Ui.Button(new Rect(lab.x + 16f, y + 6f, 260f, 44f), "Open Boss Lab", Ui.GoldButton))
            {
                devMenuOpen = false;
                labSetupOpen = true;
            }
            Ui.Label(new Rect(lab.x + 292f, y + 6f, lab.width - 308f, 44f), "Starting the lab leaves any match you're in.", 11, Ui.Muted, TextAnchor.MiddleLeft, false, true);
        }

        static void OpenFolder(string path)
        {
            System.IO.Directory.CreateDirectory(path);
            Application.OpenURL("file:///" + path.Replace('\\', '/'));
        }

        // --- Players ---------------------------------------------------------------------

        void DrawDevPlayers(float x, float y, float w, float bottom)
        {
            var players = new List<Player>(game.Players.Values);
            players.Sort((a, b) => a.Id.CompareTo(b.Id));
            if (players.Count == 0)
                return;
            Player p = players.Find(q => q.Id == devPlayer) ?? game.Me ?? players[0];
            devPlayer = p.Id;
            float tw = Mathf.Min(170f, (w - 8f * (players.Count - 1)) / players.Count);
            for (int i = 0; i < players.Count; i++)
            {
                var q = players[i];
                string label = q.Name + (q.Id == game.MyId ? " (you)" : "");
                if (Ui.Choice(new Rect(x + i * (tw + 8f), y, tw, 32f), label, q.Id == p.Id, ToColor(q.Def.Color)))
                {
                    devPlayer = q.Id;
                    devFocus = "";
                }
            }
            y += 40f;

            bool host = session.IsHost;
            Ui.Label(new Rect(x, y, w, 20f), string.Format("{0} {1} · level {2} · HP {3:0}/{4:0}{5}", Defs.KitName(p.Kit), p.Def.Name, p.Level, p.Hp, p.MaxHp,
                p.StatOverrides.Count > 0 ? "  ·  " + p.StatOverrides.Count + " edited" : ""), 13, Ui.Text);
            y += 26f;
            if (host)
            {
                bool god = Ui.Toggle(new Rect(x, y, 140f, 28f), p.DevGod, "God mode");
                if (god != p.DevGod)
                    game.DevSetGod(p.Id, god);
                if (Ui.Button(new Rect(x + 150f, y, 100f, 28f), "Full heal"))
                    game.DevHeal(p.Id);
                if (Ui.Button(new Rect(x + 256f, y, 100f, 28f), "Level up"))
                    game.DevLevelUp(p.Id);
                if (Ui.Button(new Rect(x + 362f, y, 120f, 28f), "Max mastery"))
                    game.DevMasterAll(p.Id);
                if (Ui.Button(new Rect(x + w - 130f, y, 130f, 28f), "Reset stats", Ui.Danger))
                {
                    game.DevResetStats(p.Id);
                    devFocus = "";
                }
                y += 36f;
            }

            // The stat list: every stat (or those matching the filter), scrollable. Edited ones are gold;
            // click a value to type one in.
            Ui.Label(new Rect(x, y, 60f, 28f), "FILTER", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            string filter = Ui.Field(new Rect(x + 52f, y, 220f, 28f), devFilter, 24);
            if (filter != devFilter)
            {
                devFilter = filter;
                devScroll = 0f;
            }
            var shownStats = new List<int>();
            for (int i = 0; i < Game.StatFields.Length; i++)
                if (devFilter.Trim().Length == 0 || Spaced(Game.StatFields[i].Name).IndexOf(devFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    shownStats.Add(i);
            Ui.Label(new Rect(x + 280f, y, w - 280f, 28f), shownStats.Count + " of " + Game.StatFields.Length + " stats" + (devFilter.Length > 0 ? "" : "  ·  type to narrow down"), 11, Ui.Muted);
            y += 36f;
            var list = new Rect(x, y, w, bottom - y);
            Ui.Pill(list, new Color(0f, 0f, 0f, 0.3f));
            const float row = 30f;
            int n = shownStats.Count;
            float maxScroll = Mathf.Max(0f, n * row - list.height + 8f);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition))
            {
                devScroll = Mathf.Clamp(devScroll + ev.delta.y * 24f, 0f, maxScroll);
                ev.Use();
            }
            devScroll = Mathf.Clamp(devScroll, 0f, maxScroll);
            GUI.BeginGroup(list);
            for (int i = 0; i < n; i++)
            {
                float ry = 4f + i * row - devScroll;
                if (ry < -row || ry > list.height)
                    continue;
                if (i % 2 == 0)
                    Ui.Fill(new Rect(4f, ry - 1f, list.width - 8f, row - 2f), new Color(1f, 1f, 1f, 0.025f));
                DrawStatRow(p, shownStats[i], new Rect(8f, ry, list.width - 16f, row - 4f), host);
            }
            GUI.EndGroup();
            if (maxScroll > 0f)  // scrollbar
            {
                float frac = list.height / (n * row + 8f);
                float th = list.height * frac, ty = list.y + (list.height - th) * (devScroll / maxScroll);
                Ui.Fill(new Rect(list.xMax - 4f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }
        }

        void DrawStatRow(Player p, int i, Rect r, bool host)
        {
            var f = Game.StatFields[i];
            float value = Game.StatValue(p.Stats, i);
            bool edited = p.StatOverrides.ContainsKey(i);
            Ui.Label(new Rect(r.x, r.y, r.width * 0.45f, r.height), Spaced(f.Name), 13, edited ? Ui.Gold : Ui.Text);
            bool isInt = f.FieldType == typeof(int), isBool = f.FieldType == typeof(bool);
            string shown = isBool ? (value != 0f ? "on" : "off") : isInt ? value.ToString("0") : value.ToString("0.###");
            float vx = r.x + r.width * 0.45f;
            if (!host)
            {
                Ui.Label(new Rect(vx, r.y, 120f, r.height), shown, 13, edited ? Ui.Gold : Ui.Muted);
                return;
            }
            if (isBool)
            {
                bool on = Ui.Toggle(new Rect(vx, r.y, 120f, r.height), value != 0f, shown);
                if (on != (value != 0f))
                    game.DevSetStat(p.Id, i, on ? 1f : 0f);
            }
            else
            {
                float step = StatStep(f.Name, isInt);
                if (Ui.Button(new Rect(vx, r.y, 32f, r.height), "-"))
                    Commit(p, i, value - step);
                // Typing: the field keeps what you type while it has focus, and applies it whenever it's a number.
                string name = "devstat" + p.Id + "_" + i;
                bool focused = GUI.GetNameOfFocusedControl() == name;
                if (!focused && devFocus == name)
                    devFocus = "";
                string text = focused && devFocus == name ? devBuffer : shown;
                GUI.SetNextControlName(name);
                string typed = Ui.Field(new Rect(vx + 36f, r.y, 96f, r.height), text, 10);
                if (focused)
                {
                    devFocus = name;
                    devBuffer = typed;
                    float v;
                    if (typed != text && float.TryParse(typed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v != value)
                        Commit(p, i, v);
                }
                if (Ui.Button(new Rect(vx + 136f, r.y, 32f, r.height), "+"))
                    Commit(p, i, value + step);
            }
            if (edited)
                Ui.Label(new Rect(r.xMax - 70f, r.y, 70f, r.height), "edited", 11, Ui.Gold, TextAnchor.MiddleRight);
        }

        void Commit(Player p, int field, float value)
        {
            if (Game.StatFields[field].FieldType == typeof(int))
                value = Mathf.Round(value);
            game.DevSetStat(p.Id, field, value);
        }

        /// How much the - / + buttons change a stat by.
        static float StatStep(string name, bool isInt)
        {
            if (isInt) return 1f;
            if (name.EndsWith("Hp") || name.Contains("Capacity") && !name.EndsWith("Mult")) return 10f;
            if (name.Contains("Chance") || name.Contains("Frac") || name.Contains("Slow")) return 0.05f;
            return 0.1f;
        }

        /// "DmgMult" -> "Dmg Mult"
        static string Spaced(string name)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < name.Length; i++)
            {
                if (i > 0 && char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))
                    sb.Append(' ');
                sb.Append(name[i]);
            }
            return sb.ToString();
        }
    }
}

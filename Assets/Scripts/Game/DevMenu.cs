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
        int devWave = 1;          // the wave the "Go" button jumps to
        int devPlayer;            // whose stats are shown (player id; 0 = you)
        float devScroll;          // stat list scroll offset
        string devFocus = "";     // the stat field being typed in, and what's typed so far
        string devBuffer = "";

        static readonly Rect DevPanel = new Rect(W / 2f - 470f, 40f, 940f, 640f);

        void DrawDevMenu()
        {
            var panel = DevPanel;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.45f));
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 14f, panel.width, 36f), "DEVELOPER TOOLS", 28, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 48f, panel.width, 18f), "F1 to close" + (session.IsHost ? "" : "  ·  only the host can change the match"),
                12, Ui.Muted, TextAnchor.MiddleCenter);

            float lx = panel.x + 28f, lw = 300f, y = panel.y + 84f;
            y = DrawDevTools(lx, y, lw);
            if (session.Screen == AppScreen.Game)
                DrawDevStage(lx, y + 14f, lw);

            float rx = lx + lw + 32f, rw = panel.xMax - 28f - rx;
            if (session.Screen == AppScreen.Game)
                DrawDevPlayers(rx, panel.y + 84f, rw, panel.yMax - 24f);
            else
                Ui.Label(new Rect(rx, panel.y + 84f, rw, 60f), "Start or join a match to change the wave and edit players' stats.", 14, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        float DrawDevTools(float x, float y, float w)
        {
            Ui.Heading(x, y, w, "Tools");
            y += 30f;
            bool rec = Ui.Toggle(new Rect(x, y, w, 26f), recordSessions, "Record sessions (when hosting)");
            if (rec != recordSessions)
            {
                recordSessions = rec;
                PlayerPrefs.SetInt("record_sessions", rec ? 1 : 0);
                PlayerPrefs.Save();
                ApplyRecording();
            }
            y += 30f;
            if (Ui.Button(new Rect(x, y, 170f, 28f), "Open sessions folder"))
            {
                System.IO.Directory.CreateDirectory(SessionsFolder);
                Application.OpenURL("file:///" + SessionsFolder.Replace('\\', '/'));
            }
            Ui.Label(new Rect(x + 178f, y, w - 178f, 28f), "Tools/SessionReport", 11, Ui.Muted, TextAnchor.MiddleLeft);
            y += 36f;
            aimDebug = Ui.Toggle(new Rect(x, y, w, 26f), aimDebug, "Aim debug overlay  (F3)");
            y += 30f;
            game.Bot = Ui.Toggle(new Rect(x, y, w, 26f), game.Bot, "Bot plays for you");
            y += 32f;
            if (Ui.Button(new Rect(x, y, w, 32f), "Boss Lab: watch bots fight a boss", Ui.GoldButton))
            {
                devMenuOpen = false;
                labSetupOpen = true;
            }
            y += 38f;
            if (Ui.Button(new Rect(x, y, w, 32f), "Practice Range: build your own waves", Ui.GoldButton))
                OpenPractice();
            return y + 38f;
        }

        void DrawDevStage(float x, float y, float w)
        {
            Ui.Heading(x, y, w, "Stage");
            y += 30f;
            Ui.Label(new Rect(x, y, w, 20f), "Now on wave " + Mathf.Max(1, game.Wave) + (game.Endless ? " (endless)" : " of " + Game.MaxWave), 14, Ui.Text);
            y += 28f;
            if (!session.IsHost)
            {
                Ui.Label(new Rect(x, y, w, 20f), "Only the host can change the wave.", 13, Ui.Muted);
                return;
            }
            int max = game.Endless ? 999 : Game.MaxWave;
            float bx = x;
            foreach (int step in new[] { -5, -1 })
            {
                if (Ui.Button(new Rect(bx, y, 40f, 32f), step.ToString()))
                    devWave = Mathf.Clamp(devWave + step, 1, max);
                bx += 44f;
            }
            Ui.Label(new Rect(bx, y, 56f, 32f), devWave.ToString(), 20, Ui.Gold, TextAnchor.MiddleCenter, true);
            bx += 60f;
            foreach (int step in new[] { 1, 5 })
            {
                if (Ui.Button(new Rect(bx, y, 40f, 32f), "+" + step))
                    devWave = Mathf.Clamp(devWave + step, 1, max);
                bx += 44f;
            }
            y += 38f;
            string boss = devWave % 5 == 0 ? "Boss: " + Defs.Get(Game.BossForWave(devWave)).Name : "";
            Ui.Label(new Rect(x, y, w, 18f), boss, 12, Ui.Gold);
            y += 22f;
            if (Ui.Button(new Rect(x, y, w, 36f), "Go to wave " + devWave, Ui.Primary))
                game.DevJumpToWave(devWave);
            y += 40f;
            Ui.Label(new Rect(x, y, w, 36f), "Clears the arena and starts that wave in 2 s. Downed players get back up.", 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        void DrawDevPlayers(float x, float y, float w, float bottom)
        {
            Ui.Heading(x, y, w, "Players");
            y += 30f;
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

            // The stat list: every stat, scrollable. Edited ones are gold; click a value to type one in.
            var list = new Rect(x, y, w, bottom - y);
            Ui.Fill(list, new Color(0f, 0f, 0f, 0.25f));
            const float row = 30f;
            int n = Game.StatFields.Length;
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
                DrawStatRow(p, i, new Rect(8f, ry, list.width - 16f, row - 4f), host);
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

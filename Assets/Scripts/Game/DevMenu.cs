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
        int devWave = 1;          // the wave the "Go" button jumps to
        int devPlayer;            // whose stats are shown (player id; 0 = you)
        float devScroll;          // stat list scroll offset
        string devFocus = "";     // the stat field being typed in, and what's typed so far
        string devBuffer = "";
        string devFilter = "";    // only show stats whose name contains this

        static readonly Rect DevPanel = new Rect(W / 2f - 470f, 40f, 940f, 640f);

        void DevNotInMatch(Rect body, string what)
        {
            DkSub(new Rect(body.x, body.y, body.width, 90f));
            Ui.Label(new Rect(body.x, body.y + 18f, body.width, 24f), "Not in a match", 16, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(body.x, body.y + 46f, body.width, 20f), "Start or join a match to " + what + ".", 13, CrtStyle.Desc, TextAnchor.MiddleCenter);
        }

        /// A titled card for the content area; returns the y to draw its contents at.
        float DevCard(Rect r, string title, string blurb, Color accent)
        {
            return DkCard(r, title, blurb, accent);
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
                if (DkChoice(new Rect(x + i * (tw + 8f), y, tw, 32f), label, q.Id == p.Id, ToColor(q.Def.Color)))
                {
                    devPlayer = q.Id;
                    devFocus = "";
                }
            }
            y += 40f;

            bool host = session.IsHost;
            Ui.Label(new Rect(x, y, w, 20f), string.Format("{0} {1} · level {2} · HP {3:0}/{4:0}{5}", Defs.KitName(p.Kit), p.Def.Name, p.Level, p.Hp, p.MaxHp,
                p.StatOverrides.Count > 0 ? "  ·  " + p.StatOverrides.Count + " edited" : ""), 13, CrtStyle.Body);
            y += 26f;
            if (host)
            {
                var c = DkStack.Cells(new Rect(x, y, w, 28f), 5, 4f);
                if (DkKey(c[0], "GOD", CrtStyle.Gold, p.DevGod, 12))
                    game.DevSetGod(p.Id, !p.DevGod);
                if (DkKey(c[1], "HEAL", Ui.Neutral, false, 12))
                    game.DevHeal(p.Id);
                if (DkKey(c[2], "LEVEL UP", Ui.Neutral, false, 12))
                    game.DevLevelUp(p.Id);
                if (DkKey(c[3], "MASTERY", Ui.Neutral, false, 12))
                    game.DevMasterAll(p.Id);
                if (DkKey(c[4], "RESET", Ui.Danger, false, 12))
                {
                    game.DevResetStats(p.Id);
                    devFocus = "";
                }
                y += 36f;
            }

            // The stat list: every stat (or those matching the filter), scrollable. Edited ones are gold;
            // click a value to type one in.
            Ui.Label(new Rect(x, y, 60f, 28f), "FILTER", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            string filter = DkField(new Rect(x + 52f, y, 220f, 28f), devFilter, 24);
            if (filter != devFilter)
            {
                devFilter = filter;
                devScroll = 0f;
            }
            var shownStats = new List<int>();
            for (int i = 0; i < Game.StatFields.Length; i++)
                if (devFilter.Trim().Length == 0 || Spaced(Game.StatFields[i].Name).IndexOf(devFilter.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                    shownStats.Add(i);
            Ui.Label(new Rect(x + 280f, y, w - 280f, 28f), shownStats.Count + " of " + Game.StatFields.Length, 11, CrtStyle.Desc);
            y += 36f;
            var list = new Rect(x, y, w, bottom - y);
            DkSub(list);
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
            Ui.Label(new Rect(r.x, r.y, r.width * 0.45f, r.height), Spaced(f.Name), 13, edited ? Ui.Gold : CrtStyle.Body);
            bool isInt = f.FieldType == typeof(int), isBool = f.FieldType == typeof(bool);
            string shown = isBool ? (value != 0f ? "on" : "off") : isInt ? value.ToString("0") : value.ToString("0.###");
            float vx = r.x + r.width * 0.45f;
            if (!host)
            {
                Ui.Label(new Rect(vx, r.y, 120f, r.height), shown, 13, edited ? Ui.Gold : CrtStyle.Desc);
                return;
            }
            if (isBool)
            {
                bool on = DkSwitch(new Rect(vx, r.y, 120f, r.height), value != 0f, shown);
                if (on != (value != 0f))
                    game.DevSetStat(p.Id, i, on ? 1f : 0f);
            }
            else
            {
                float step = StatStep(f.Name, isInt);
                if (DkKey(new Rect(vx, r.y, 32f, r.height), "-"))
                    Commit(p, i, value - step);
                // Typing: the field keeps what you type while it has focus, and applies it whenever it's a number.
                string name = "devstat" + p.Id + "_" + i;
                bool focused = GUI.GetNameOfFocusedControl() == name;
                if (!focused && devFocus == name)
                    devFocus = "";
                string text = focused && devFocus == name ? devBuffer : shown;
                GUI.SetNextControlName(name);
                string typed = DkField(new Rect(vx + 36f, r.y, 96f, r.height), text, 10);
                if (focused)
                {
                    devFocus = name;
                    devBuffer = typed;
                    float v;
                    if (typed != text && float.TryParse(typed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out v) && v != value)
                        Commit(p, i, v);
                }
                if (DkKey(new Rect(vx + 136f, r.y, 32f, r.height), "+"))
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

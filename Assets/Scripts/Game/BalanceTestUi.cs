using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The balance test's screens (Core/BalanceTest.cs, Docs/BalanceTest.md): the F1 > Balance tab that launches it, the
    /// strip shown while it runs, and the report over the screen when the fifth wave clears.
    public partial class ArenaApp
    {
        static string BalanceFolderPath => System.IO.Path.Combine(Application.persistentDataPath, "balance");

        int btStart = 1;
        bool btBot, btFine = true, btRefill = true;
        int btTab;
        float btScroll;
        BalanceRun btDismissed;   // the finished test whose report was closed
        string btNote = "";
        static readonly int[] BtStarts = { 1, 6, 11, 16, 21, 26 };
        static readonly Color BtColor = new Color(0.44f, 0.89f, 0.77f);

        // --- F1 > Balance -----------------------------------------------------------------

        void DrawDevBalance(Rect body)
        {
            var me = game.Me;
            bool running = game.TestRunning, host = session.IsHost;
            var card = new Rect(body.x, body.y, body.width, 304f);
            float y = DevCard(card, "Balance test", "Plays 5 waves with the build you picked and logs every number for balancing. Never saved to Records or XP.", BtColor);
            float x = card.x + 16f, w = card.width - 32f;
            if (me != null)
            {
                string build = me.Def.Name + "  ·  " + me.Weapon.Name + "  ·  " + Upgrades.Ability(me.RoleAbility).Name + "  ·  " + Upgrades.Ability(Upgrades.Ultimate(me.Role, me.UltKit, me.UltPick)).Name;
                Ui.Label(new Rect(x, y - 2f, w, 20f), "Your build: " + build, 12, Ui.Gold, TextAnchor.MiddleLeft, true);
            }
            y += 24f;
            Ui.Label(new Rect(x, y, 120f, 22f), "START WAVE", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float bw = 54f;
            for (int i = 0; i < BtStarts.Length; i++)
                if (Ui.Choice(new Rect(x + 120f + i * (bw + 4f), y - 2f, bw, 26f), BtStarts[i].ToString(), btStart == BtStarts[i], BtColor))
                    btStart = BtStarts[i];
            Ui.Label(new Rect(x + 120f + BtStarts.Length * (bw + 4f) + 8f, y, 260f, 22f), "tests waves " + btStart + " to " + (btStart + Game.BalanceWaves - 1), 11, Ui.Muted, TextAnchor.MiddleLeft);
            y += 34f;
            Ui.Label(new Rect(x, y, 120f, 22f), "WHO PLAYS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.Choice(new Rect(x + 120f, y - 2f, 90f, 26f), "Me", !btBot, BtColor)) btBot = false;
            if (Ui.Choice(new Rect(x + 214f, y - 2f, 150f, 26f), "Bot, my build", btBot, BtColor)) btBot = true;
            Ui.Label(new Rect(x + 376f, y, w - 376f, 22f), "The bot gives repeatable runs to compare changes.", 11, Ui.Muted, TextAnchor.MiddleLeft);
            y += 34f;
            btRefill = Ui.Toggle(new Rect(x, y, w, 26f), btRefill, "Refill health between waves (keeps each wave a fair sample)");
            y += 30f;
            btFine = Ui.Toggle(new Rect(x, y, w, 26f), btFine, "A sample every second, not every 5 s (bigger file: health, nearest enemy, enemies in view)");
            y += 38f;
            GUI.enabled = host && !running && game.Practice == null && game.Hunt == null;
            if (Ui.Button(new Rect(x, y, 230f, 40f), "Launch test", Ui.Primary))
            {
                if (game.DevStartBalanceTest(btStart, btBot, btFine, btRefill))
                {
                    devMenuOpen = false;
                    btDismissed = null;
                    btNote = "";
                }
                else
                    btNote = "Could not start. Needs to be the host of a normal run.";
            }
            GUI.enabled = true;
            string status = running ? "Running: wave " + Mathf.Max(1, game.Wave) + " of " + game.Test.FirstWave + " to " + game.Test.LastWave
                : !host ? "Only the host can run it." : game.Practice != null || game.Hunt != null ? "Not in Practice or a Boss Hunt." : btNote.Length > 0 ? btNote : "Starts in 2 s, replacing any fight going on.";
            Ui.Label(new Rect(x + 246f, y, w - 246f, 40f), status, 12, running ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft, false, true);

            // The last test, and the log folder.
            var logs = new Rect(body.x, body.y + 318f, body.width, 130f);
            y = DevCard(logs, "Logs", "One JSON file per test (format 4), summarised by Tools/SessionReport.", BtColor);
            x = logs.x + 16f;
            if (Ui.Button(new Rect(x, y, 190f, 32f), "Open logs folder"))
                OpenFolder(BalanceFolderPath);
            var t = game.Test;
            if (running && Ui.Button(new Rect(x + 202f, y, 190f, 32f), "End test now", Ui.Danger))
                game.DevEndBalanceTest("ended by the player");
            if (t != null && t.Done && Ui.Button(new Rect(x + 202f, y, 190f, 32f), "Open last report", Ui.GoldButton))
                btDismissed = null;
            y += 42f;
            Ui.Label(new Rect(x, y, logs.width - 32f, 36f), t != null && t.Path.Length > 0 ? "This test: " + t.Path : "Folder: " + BalanceFolderPath, 10, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }

        // --- While it runs ----------------------------------------------------------------

        void DrawBalanceStrip()
        {
            var t = game.Test;
            if (t == null || t.Done)
                return;
            var r = new Rect(W / 2f - 250f, 8f, 500f, 30f);
            Ui.Pill(r, new Color(0.04f, 0.12f, 0.1f, 0.9f));
            bool blink = (int)(Time.unscaledTime * 1.6f) % 2 == 0;
            Ui.Fill(new Rect(r.x + 12f, r.y + 11f, 8f, 8f), blink ? new Color(1f, 0.42f, 0.42f) : new Color(1f, 0.42f, 0.42f, 0.3f));
            int n = Mathf.Clamp(game.Wave - t.FirstWave + 1, 0, t.Waves);
            Ui.Label(new Rect(r.x + 26f, r.y, 70f, r.height), "TEST", 12, new Color(1f, 0.55f, 0.55f), TextAnchor.MiddleLeft, true);
            for (int i = 0; i < t.Waves; i++)
                Ui.Fill(new Rect(r.x + 74f + i * 26f, r.y + 11f, 22f, 8f), i < n - 1 ? BtColor : i == n - 1 ? Ui.Gold : new Color(1f, 1f, 1f, 0.12f));
            Ui.Label(new Rect(r.x + 214f, r.y, r.width - 226f, r.height), "WAVE " + Mathf.Max(game.Wave, t.FirstWave) + "   ·   F1 > Balance to end", 11, Ui.Muted, TextAnchor.MiddleRight);
        }

        // --- The report -------------------------------------------------------------------

        /// True while the report is on screen (it needs the pointer; ArenaApp.cs).
        bool BalanceReportUp()
        {
            var t = game.Test;
            return t != null && t.Done && t.Report != null && t != btDismissed;
        }

        void DrawBalanceReport()
        {
            var t = game.Test;
            if (!BalanceReportUp())
                return;
            var rep = t.Report;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0.02f, 0.78f));
            var panel = new Rect(W / 2f - 520f, 28f, 1040f, H - 56f);
            Ui.Panel(panel, true);
            Ui.Fancy(new Rect(panel.x + 24f, panel.y + 12f, 600f, 34f), "BALANCE TEST", 26, Color.white, BtColor, TextAnchor.MiddleLeft, BtColor);
            Ui.Label(new Rect(panel.x + 26f, panel.y + 46f, panel.width - 60f, 16f), rep.Title, 12, Ui.Muted);
            if (Ui.Button(new Rect(panel.xMax - 52f, panel.y + 14f, 32f, 30f), "X", Ui.Danger))
                btDismissed = t;

            // Tiles.
            float tw = (panel.width - 48f - 5f * 8f) / 6f, ty = panel.y + 74f;
            for (int i = 0; i < rep.Tiles.Count; i++)
            {
                var tr = new Rect(panel.x + 24f + i * (tw + 8f), ty, tw, 62f);
                Ui.Pill(tr, new Color(1f, 1f, 1f, 0.05f));
                Ui.Label(new Rect(tr.x + 10f, tr.y + 4f, tr.width - 14f, 14f), rep.Tiles[i].Label.ToUpperInvariant(), 9, Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tr.x + 10f, tr.y + 17f, tr.width - 14f, 26f), rep.Tiles[i].Value, 20, i == 0 && rep.Cleared ? Ui.Good : Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tr.x + 10f, tr.y + 42f, tr.width - 14f, 16f), rep.Tiles[i].Sub, 10, Ui.Muted, TextAnchor.MiddleLeft);
            }

            // Tabs.
            float tabY = ty + 74f, tx = panel.x + 24f;
            btTab = Mathf.Clamp(btTab, 0, rep.Tables.Count - 1);
            for (int i = 0; i < rep.Tables.Count; i++)
            {
                float tabW = 24f + rep.Tables[i].Name.Length * 8.2f;
                if (Ui.Choice(new Rect(tx, tabY, tabW, 28f), rep.Tables[i].Name, btTab == i, BtColor))
                {
                    btTab = i;
                    btScroll = 0f;
                }
                tx += tabW + 4f;
            }

            // The table, scrolling.
            var tbl = rep.Tables[btTab];
            var list = new Rect(panel.x + 24f, tabY + 36f, panel.width - 48f, panel.height - (tabY - panel.y) - 36f - 150f);
            Ui.Pill(list, new Color(0f, 0f, 0f, 0.25f));
            int cols = tbl.Head.Length;
            float cw0 = Mathf.Min(300f, list.width * 0.34f), cw = (list.width - 20f - cw0) / Mathf.Max(1, cols - 1);
            float rowH = 24f, total = (tbl.Rows.Count + 1) * rowH;
            float maxScroll = Mathf.Max(0f, total - list.height + 8f);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && list.Contains(ev.mousePosition))
            {
                btScroll = Mathf.Clamp(btScroll + ev.delta.y * 24f, 0f, maxScroll);
                ev.Use();
            }
            GUI.BeginGroup(list);
            for (int c = 0; c < cols; c++)
            {
                float cx = 10f + (c == 0 ? 0f : cw0 + (c - 1) * cw), cwid = c == 0 ? cw0 : cw;
                Ui.Label(new Rect(cx, 2f, cwid - 6f, rowH), tbl.Head[c].ToUpperInvariant(), 9, Ui.Muted, c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, true);
            }
            for (int r = 0; r < tbl.Rows.Count; r++)
            {
                float ry = (r + 1) * rowH - btScroll + 4f;
                if (ry < rowH - 4f || ry > list.height)
                    continue;
                if (r % 2 == 0)
                    Ui.Fill(new Rect(2f, ry, list.width - 4f, rowH - 2f), new Color(1f, 1f, 1f, 0.03f));
                for (int c = 0; c < cols && c < tbl.Rows[r].Length; c++)
                {
                    float cx = 10f + (c == 0 ? 0f : cw0 + (c - 1) * cw), cwid = c == 0 ? cw0 : cw;
                    Ui.Label(new Rect(cx, ry, cwid - 6f, rowH - 2f), tbl.Rows[r][c], 12, c == 0 ? Color.white : new Color(0.85f, 0.9f, 1f), c == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight, c == 0);
                }
            }
            GUI.EndGroup();

            // Findings and buttons.
            var fr = new Rect(panel.x + 24f, list.yMax + 8f, panel.width - 48f, 78f);
            Ui.Pill(fr, new Color(1f, 0.7f, 0.2f, 0.08f));
            Ui.Label(new Rect(fr.x + 12f, fr.y + 4f, 200f, 14f), "FINDINGS", 9, Ui.Gold, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < rep.Findings.Count && i < 4; i++)
                Ui.Label(new Rect(fr.x + 12f, fr.y + 19f + i * 14f, fr.width - 24f, 14f), "·  " + rep.Findings[i], 11, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleLeft);
            float by = fr.yMax + 10f;
            if (Ui.Button(new Rect(panel.x + 24f, by, 170f, 32f), "Copy summary", Ui.Primary))
            {
                GUIUtility.systemCopyBuffer = rep.AsText();
                btNote = "Copied.";
            }
            if (Ui.Button(new Rect(panel.x + 204f, by, 170f, 32f), "Open logs folder"))
                OpenFolder(BalanceFolderPath);
            Ui.Label(new Rect(panel.x + 384f, by, panel.width - 480f, 32f), btNote.Length > 0 ? btNote : rep.Path, 10, Ui.Muted, TextAnchor.MiddleLeft, false, true);
            if (Ui.Button(new Rect(panel.xMax - 154f, by, 130f, 32f), "Close", Ui.Neutral))
                btDismissed = t;
        }
    }
}

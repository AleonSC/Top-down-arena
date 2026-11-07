using System;
using UnityEngine;

namespace Arena
{
    /// The DPS meter (F2 in a match; Shift+F2 starts it over): damage a second for each player, as the host sees it,
    /// next to what the sim measured for that weapon (Defs.WeaponDps). Host only; see DpsTools.cs.
    public partial class ArenaApp
    {
        bool dpsMeter;  // F2

        static readonly Color DpsColor = new Color(1f, 0.62f, 0.3f);

        void DpsMeterKeys()
        {
            if (!Input.GetKeyDown(KeyCode.F2))
                return;
            if (session.Screen != AppScreen.Game)
            {
                OpenLogFolder();  // outside a run F2 opens the folder holding Player.log
                return;
            }
            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                game.DevResetDps();
            else
                dpsMeter = !dpsMeter;
        }

        /// Opens the folder with Player.log (Application.consoleLogPath), for sending it in when something looks wrong on one machine.
        static void OpenLogFolder()
        {
            try
            {
                string log = Application.consoleLogPath, dir = string.IsNullOrEmpty(log) ? null : System.IO.Path.GetDirectoryName(log);
                if (string.IsNullOrEmpty(dir) || !System.IO.Directory.Exists(dir))
                    dir = Application.persistentDataPath;  // no log path (editor, some platforms): the save folder is next to it
                Application.OpenURL("file:///" + dir.Replace('\\', '/'));
            }
            catch (System.Exception) { }
        }

        static string Whole(float v) => Mathf.RoundToInt(v).ToString("N0");

        void DrawDpsMeter()
        {
            const float rowH = 62f;
            int rows = session.IsHost ? game.Players.Count : 0;
            int dummies = session.IsHost ? game.DummyCount() : 0;
            var r = new Rect(14f, 124f, 300f, 36f + Mathf.Max(1, rows) * rowH + (dummies > 0 ? 22f : 0f));
            Ui.HudPanel(r, DpsColor);
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), "DPS METER", 11, DpsColor, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 12f, r.y + 6f, r.width - 24f, 16f), "F2 hide  ·  Shift+F2 reset", 10, Ui.Muted, TextAnchor.MiddleRight);
            if (!session.IsHost)
            {
                Ui.Label(new Rect(r.x + 12f, r.y + 30f, r.width - 24f, 40f), "Only the host sees the meter.", 12, Ui.Muted, TextAnchor.MiddleLeft, false, true);
                return;
            }
            float y = r.y + 28f;
            foreach (var p in game.Players.Values)
            {
                DpsTrack t;
                game.Dps.TryGetValue(p.Id, out t);
                bool live = t != null && !t.Idle;
                float now = t != null ? t.Window(1f) : 0f, avg = t != null ? t.Average : 0f;
                float sim = p.Weapon != null ? Defs.WeaponDps(p.Weapon.Id) : 0f;
                Ui.Label(new Rect(r.x + 12f, y, 170f, 18f), p.Name + (p.Weapon != null ? "  ·  " + p.Weapon.Name : ""), 11, ToColor(p.Def.Color), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 120f, y - 2f, r.width - 132f, 24f), live ? Whole(now) : "-", 20, live ? Color.white : Ui.Muted, TextAnchor.MiddleRight, true);
                if (t != null && t.Total > 0f)
                {
                    Ui.Label(new Rect(r.x + 12f, y + 20f, r.width - 24f, 16f), "5 s " + Whole(t.Window(5f)) + "  ·  avg " + Whole(avg) + "  ·  peak " + Whole(t.Peak), 11, Ui.Text);
                    float over = t.OverTime / t.Total;
                    Ui.Label(new Rect(r.x + 12f, y + 36f, r.width - 24f, 16f),
                        Whole(t.Total) + " total" + (over >= 0.005f ? "  ·  " + Mathf.RoundToInt(over * 100f) + "% burn/bleed" : ""), 10, Ui.Muted);
                    if (sim > 0f)
                        Ui.Label(new Rect(r.x + 12f, y + 36f, r.width - 24f, 16f), "sim " + Whole(sim) + "  (" + Mathf.RoundToInt(avg / sim * 100f) + "%)", 10,
                            avg >= sim * 0.9f ? Ui.Good : Ui.Muted, TextAnchor.MiddleRight);
                }
                else
                    Ui.Label(new Rect(r.x + 12f, y + 20f, r.width - 24f, 16f), "waiting for a hit" + (sim > 0f ? "  ·  sim " + Whole(sim) : ""), 11, Ui.Muted);
                y += rowH;
            }
            if (dummies > 0)
                Ui.Label(new Rect(r.x + 12f, r.yMax - 22f, r.width - 24f, 16f), dummies + (dummies == 1 ? " dummy" : " dummies") + " took " + Whole(game.DummyDamage()), 10, Ui.Muted);
        }

        // --- The DPS tab of the F1 menu ----------------------------------------------------

        void DrawDevDps(Rect body)
        {
            bool host = session.IsHost;
            bool wasEnabled = GUI.enabled;
            var meter = new Rect(body.x, body.y, body.width, 190f);
            float y = DevCard(meter, "DPS meter", "Damage a second for every player, from every source (shots, minions, burning). Host only.", DpsColor);
            dpsMeter = Ui.Toggle(new Rect(meter.x + 16f, y, meter.width - 32f, 26f), dpsMeter, "Show the meter on screen  (F2)");
            y += 34f;
            GUI.enabled = host;
            if (Ui.Button(new Rect(meter.x + 16f, y, 190f, 30f), "Reset  (Shift+F2)"))
                game.DevResetDps();
            GUI.enabled = wasEnabled;
            Ui.Label(new Rect(meter.x + 16f, y + 38f, meter.width - 32f, 54f),
                "Now is the last second; avg counts only the seconds you were hitting something. The sim figure is the no-upgrades number from the lobby (Defs.WeaponDps), so a low percentage means a weapon misses, or you do.",
                11, Ui.Muted, TextAnchor.UpperLeft, false, true);

            var dummy = new Rect(body.x, meter.yMax + 14f, body.width, 230f);
            y = DevCard(dummy, "Target dummy", "A Juggernaut that never dies, shoots or hurts, and doesn't hold up the wave. Up to " + Game.MaxDummies + ".", DpsColor);
            int count = host ? game.DummyCount() : 0;
            GUI.enabled = host && count < Game.MaxDummies;
            if (Ui.Button(new Rect(dummy.x + 16f, y, 220f, 34f), "Spawn one in front of you", Ui.Primary))
                game.DevSpawnDummy(game.MyId);
            GUI.enabled = host && count > 0;
            if (Ui.Button(new Rect(dummy.x + 246f, y, 160f, 34f), "Remove all"))
                game.DevClearDummies();
            GUI.enabled = host;
            y += 44f;
            game.DummyMoves = Ui.Toggle(new Rect(dummy.x + 16f, y, dummy.width - 32f, 26f), game.DummyMoves, "Patrol side to side (tests moving targets and aim)");
            GUI.enabled = wasEnabled;
            y += 34f;
            Ui.Label(new Rect(dummy.x + 16f, y, dummy.width - 32f, 18f), host ? count + " on the field  ·  taken " + Whole(game.DummyDamage()) + " since the last reset" : "Only the host can spawn dummies.", 12, Ui.Text);
            Ui.Label(new Rect(dummy.x + 16f, y + 22f, dummy.width - 32f, 36f),
                "Using this marks the run as a dev run, like god mode. In the Practice Range, build a wave with no enemies and untick Loop to test undisturbed.",
                11, Ui.Muted, TextAnchor.UpperLeft, false, true);
        }
    }
}

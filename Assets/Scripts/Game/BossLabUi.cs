using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// Boss Lab (F1 -> Boss Lab): watch bots fight any boss with any party, at up to 8x speed, with a
    /// live readout of how the fight is going. Or take seat 1 and play it alongside them.
    public partial class ArenaApp
    {
        readonly BossLab lab = new BossLab();
        bool labSetupOpen;
        float labSpeed = 1f;
        bool labPaused;
        static readonly float[] LabSpeeds = { 1f, 2f, 4f, 8f };
        static readonly Rect LabPanel = new Rect(W - 262f, 96f, 250f, 346f);
        static readonly string[] RoleNames = { "Tank", "Healer", "DPS" };

        bool LabRunning => lab.Active && session.Screen == AppScreen.Game;

        /// Fixed step for the bot seats and the score keeping (the host is stepped as usual).
        void LabFixedStep(float dt)
        {
            if (!lab.Active)
                return;
            lab.StepClients(dt);
            lab.Watch(dt);
        }

        void LabUpdate(float dt)
        {
            if (!lab.Active)
                return;
            if (session.Screen == AppScreen.Menu)  // left through the pause menu
            {
                StopLab();
                return;
            }
            lab.UpdateClients(dt);
            foreach (var c in lab.Clients)
            {
                c.Game.Sounds.Clear();  // only the host's sounds play
                c.Game.MasteryToasts.Clear();
            }
            bool done = lab.Outcome != LabOutcome.Running;
            Time.timeScale = labPaused || done ? 0f : labSpeed;
            if (Input.GetKeyDown(KeyCode.P) && GUIUtility.keyboardControl == 0)
                labPaused = !labPaused;
        }

        void StartLab()
        {
            labSetupOpen = false;
            devMenuOpen = false;
            labPaused = false;
            lab.Seed = 0;
            game.RecordFolder = null;  // lab fights aren't recorded as sessions
            if (!lab.Start(session, Session.DefaultPort + 20 + UnityEngine.Random.Range(0, 200)))
                labSetupOpen = true;  // couldn't host: the status line says why
        }

        void StopLab()
        {
            lab.Stop();
            Time.timeScale = 1f;
            labPaused = false;
            ApplyRecording();
        }

        /// Is the mouse over the lab's panel or result card (so clicks there don't shoot)?
        bool MouseOnLab()
        {
            if (!LabRunning)
                return false;
            Vector3 m = Input.mousePosition;
            var p = new Vector2((m.x - offX) / scale, (UnityEngine.Screen.height - m.y - offY) / scale);
            return LabPanel.Contains(p) || lab.Outcome != LabOutcome.Running;
        }

        // --- Setup -------------------------------------------------------------------------

        void DrawLabSetup()
        {
            var panel = DevPanel;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.55f));
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 14f, panel.width, 36f), "BOSS LAB", 28, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 48f, panel.width, 18f), "Bots fight a boss on the real game so you can watch it play out. Esc to close.", 12, Ui.Muted, TextAnchor.MiddleCenter);

            float x = panel.x + 28f, w = 300f, y = panel.y + 82f;
            Ui.Heading(x, y, w, "Boss");
            y += 28f;
            for (int i = 0; i < BossLab.BossWaves.Length; i++)
            {
                int wave = BossLab.BossWaves[i];
                var r = new Rect(x + (i % 2) * (w / 2f + 2f), y + (i / 2) * 34f, w / 2f - 2f, 30f);
                if (Ui.Choice(r, wave + "  " + LabBossName(wave), lab.Wave == wave, Ui.Gold))
                    lab.Wave = wave;
            }
            y += 5 * 34f + 12f;

            Ui.Heading(x, y, w, "Bot skill");
            y += 28f;
            var skills = new[] { BotSkill.Casual, BotSkill.Good, BotSkill.Expert };
            for (int i = 0; i < 3; i++)
                if (Ui.Choice(new Rect(x + i * (w / 3f + 1f), y, w / 3f - 2f, 30f), skills[i].ToString(), lab.Skill == skills[i], Ui.Primary))
                    lab.Skill = skills[i];
            y += 34f;
            Ui.Label(new Rect(x, y, w, 30f), lab.Skill == BotSkill.Casual ? "Reacts late, loses track of bullets in a crowd." :
                lab.Skill == BotSkill.Good ? "A solid player: reads most patterns, sometimes clipped." : "Reacts fast and threads tight gaps.", 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 34f;

            Ui.Heading(x, y, w, "Upgrades");
            y += 28f;
            int picks = lab.PicksFor;
            if (Ui.Button(new Rect(x, y, 34f, 30f), "-"))
                lab.Picks = Math.Max(0, picks - 1);
            Ui.Label(new Rect(x + 38f, y, 150f, 30f), picks + " picks" + (lab.Picks < 0 ? " (typical)" : ""), 14, Ui.Text, TextAnchor.MiddleCenter, true);
            if (Ui.Button(new Rect(x + 192f, y, 34f, 30f), "+"))
                lab.Picks = picks + 1;
            if (lab.Picks >= 0 && Ui.Button(new Rect(x + 232f, y, 68f, 30f), "Typical"))
                lab.Picks = -1;
            y += 36f;
            lab.Legendaries = Ui.Toggle(new Rect(x, y, w, 26f), lab.Legendaries, "Legendary + mod from earlier bosses");
            y += 28f;
            Ui.Label(new Rect(x, y, w, 30f), "Random picks, like a level-up each. Ability mastery is set for the wave too.", 11, Ui.Muted, TextAnchor.UpperLeft, false, true);

            // Party
            float rx = x + w + 32f, rw = panel.xMax - 28f - rx;
            float ry = panel.y + 82f;
            Ui.Heading(rx, ry, rw, "Party");
            for (int n = 1; n <= 3; n++)
                if (Ui.Choice(new Rect(rx + rw - (4 - n) * 92f, ry - 4f, 88f, 26f), n + (n == 1 ? " player" : " players"), lab.PlayerCount == n, Ui.Primary))
                    lab.PlayerCount = n;
            ry += 30f;
            for (int i = 0; i < lab.PlayerCount; i++)
                ry = DrawLabSeat(i, rx, ry, rw) + 8f;

            var start = new Rect(panel.xMax - 28f - 220f, panel.yMax - 58f, 220f, 40f);
            if (Ui.Button(start, "Start fight", Ui.Primary))
                StartLab();
            if (Ui.Button(new Rect(start.x - 130f, start.y, 120f, 40f), "Close"))
                labSetupOpen = false;
            if (session.Status.Length > 0 && session.Screen == AppScreen.Menu)
                Ui.Label(new Rect(x, start.y, start.x - 150f - x, 40f), session.Status, 12, Ui.Warn, TextAnchor.MiddleLeft, false, true);
        }

        float DrawLabSeat(int i, float x, float y, float w)
        {
            var s = lab.Seats[i];
            var box = new Rect(x, y, w, 150f);
            Ui.HudPanel(box, ToColor(Defs.Get(s.Role).Color));
            float ix = x + 12f, iw = w - 24f, iy = y + 8f;
            Ui.Label(new Rect(ix, iy, 120f, 20f), "Player " + (i + 1), 14, Color.white, TextAnchor.MiddleLeft, true);
            if (i == 0)
                s.Human = Ui.Toggle(new Rect(ix + iw - 170f, iy - 2f, 170f, 24f), s.Human, "I'll play this one");
            else
                Ui.Label(new Rect(ix + iw - 170f, iy, 170f, 20f), "Bot", 12, Ui.Muted, TextAnchor.MiddleRight);
            iy += 26f;
            float lw = 60f;
            Action<string, string[], int, Action<int>> row = (label, names, sel, set) =>
            {
                float cw = (iw - lw) / names.Length;
                Ui.Label(new Rect(ix, iy, lw, 26f), label, 11, Ui.Muted, TextAnchor.MiddleLeft, true);
                for (int k = 0; k < names.Length; k++)
                    if (Ui.Choice(new Rect(ix + lw + k * cw, iy, cw - 3f, 26f), names[k], sel == k, ToColor(Defs.Get(s.Role).Color)))
                        set(k);
                iy += 28f;
            };
            row("ROLE", RoleNames, (int)s.Role, k => s.Role = (Role)k);
            row("CLASS", Enumerable.Range(0, Defs.KitCount).Select(k => Defs.KitName((Kit)k)).ToArray(), (int)s.Kit, k => s.Kit = (Kit)k);
            row("WEAPON", Enumerable.Range(0, 3).Select(k => Defs.Weapon(s.Role, s.Kit, k).Name).ToArray(), s.Weapon, k => s.Weapon = k);
            row("ABILITY", Enumerable.Range(0, 3).Select(k => Upgrades.Ability(Upgrades.RoleAbility(s.Role, s.Kit, k)).Name).ToArray(), s.Ability, k => s.Ability = k);
            return box.yMax;
        }

        static string LabBossName(int wave)
        {
            var t = Game.BossForWave(wave);
            return Game.IsTwin(t) ? "Twin Reapers" : Defs.Get(t).Name;
        }

        // --- During the fight ----------------------------------------------------------------

        void DrawLabPanel()
        {
            var r = LabPanel;
            Ui.HudPanel(r, Ui.Gold);
            float x = r.x + 10f, w = r.width - 20f, y = r.y + 8f;
            Ui.Label(new Rect(x, y, w, 18f), "BOSS LAB", 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x, y, w, 18f), lab.BossName.Length > 0 ? lab.BossName : LabBossName(lab.Wave), 12, Color.white, TextAnchor.MiddleRight, true);
            y += 22f;

            // Speed
            float bw = (w - 4f * 3f) / 5f;
            if (Ui.Choice(new Rect(x, y, bw, 24f), labPaused ? "Play" : "Pause", labPaused, Ui.Gold))
                labPaused = !labPaused;
            for (int i = 0; i < LabSpeeds.Length; i++)
                if (Ui.Choice(new Rect(x + (i + 1) * (bw + 3f), y, bw, 24f), LabSpeeds[i] + "x", !labPaused && labSpeed == LabSpeeds[i], Ui.Primary))
                {
                    labSpeed = LabSpeeds[i];
                    labPaused = false;
                }
            y += 30f;

            int shots = game.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
            Line(ref y, x, w, "Fight time", lab.FightTime.ToString("0.0") + " s");
            Line(ref y, x, w, "Boss health", Mathf.RoundToInt(lab.BossHpFrac * 100f) + "%");
            Line(ref y, x, w, "Team DPS on boss", Mathf.RoundToInt(lab.TeamBossDps).ToString());
            Line(ref y, x, w, "Enemy bullets", shots + "  (peak " + lab.PeakBullets + ")");
            Line(ref y, x, w, "Bullet budget", game.ShotBudget + " (bosses can go over)");
            y += 4f;
            foreach (var kv in lab.Stats)
            {
                Player p;
                game.Players.TryGetValue(kv.Key, out p);
                var st = kv.Value;
                Color c = p != null ? ToColor(p.Def.Color) : Color.white;
                Ui.Label(new Rect(x, y, w, 16f), (p != null ? p.Name + " · " + Defs.KitName(p.Kit) + " " + p.Def.Name : st.Name), 11, c, TextAnchor.MiddleLeft, true);
                y += 15f;
                Ui.Label(new Rect(x, y, w, 15f), string.Format("boss dmg {0:0} · taken {1:0} · downs {2} · casts {3}", st.BossDamage, st.DamageTaken, st.Downs, st.Abilities), 10, Ui.Muted, TextAnchor.MiddleLeft);
                y += 17f;
            }
            var top = TopSources(3);
            if (top.Count > 0)
            {
                Ui.Label(new Rect(x, y, w, 15f), "HIT MOST BY", 9, Ui.Gold, TextAnchor.MiddleLeft, true);
                y += 15f;
                foreach (var kv in top)
                {
                    Line(ref y, x, w, kv.Key, Mathf.RoundToInt(kv.Value).ToString());
                }
            }

            float by = r.yMax - 32f, third = (w - 6f) / 3f;
            if (Ui.Button(new Rect(x, by, third, 26f), "Restart"))
                StartLab();
            if (Ui.Button(new Rect(x + third + 3f, by, third, 26f), "Setup"))
            {
                StopLab();
                labSetupOpen = true;
            }
            if (Ui.Button(new Rect(x + 2f * (third + 3f), by, third, 26f), "Exit", Ui.Danger))
                StopLab();

            if (lab.Outcome != LabOutcome.Running)
                DrawLabResult();
        }

        void Line(ref float y, float x, float w, string label, string value)
        {
            Ui.Label(new Rect(x, y, w, 16f), label, 11, Ui.Muted, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(x, y, w, 16f), value, 11, Color.white, TextAnchor.MiddleRight, true);
            y += 16f;
        }

        List<KeyValuePair<string, float>> TopSources(int n)
        {
            var all = new Dictionary<string, float>();
            foreach (var st in lab.Stats.Values)
                foreach (var kv in st.TakenFrom)
                {
                    float had;
                    all.TryGetValue(kv.Key, out had);
                    all[kv.Key] = had + kv.Value;
                }
            return all.OrderByDescending(kv => kv.Value).Take(n).ToList();
        }

        void DrawLabResult()
        {
            var r = new Rect(W / 2f - 230f, H / 2f - 110f, 460f, 200f);
            Color accent = lab.Outcome == LabOutcome.Won ? Ui.Good : lab.Outcome == LabOutcome.Lost ? Ui.Danger : Ui.Warn;
            Ui.Card(r, accent, 0.85f);
            string title = lab.Outcome == LabOutcome.Won ? "BOSS DOWN" : lab.Outcome == LabOutcome.Lost ? "PARTY WIPED" : "TIME'S UP";
            Ui.Title(new Rect(r.x, r.y + 16f, r.width, 36f), title, 28, Color.white);
            int downs = lab.Stats.Values.Sum(s => s.Downs);
            float taken = lab.Stats.Values.Sum(s => s.DamageTaken);
            Ui.Label(new Rect(r.x, r.y + 60f, r.width, 20f), string.Format("{0} in {1:0} s  ·  boss health left {2}%", lab.BossName, lab.FightTime, Mathf.RoundToInt(lab.BossHpFrac * 100f)), 14, Ui.Text, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 84f, r.width, 20f), string.Format("team damage taken {0:0}  ·  downs {1}  ·  boss DPS {2:0}", taken, downs, lab.TeamBossDps), 13, Ui.Muted, TextAnchor.MiddleCenter);
            float bw = 130f, bx = r.x + (r.width - 3f * bw - 16f) / 2f, by = r.yMax - 56f;
            if (Ui.Button(new Rect(bx, by, bw, 36f), "Run again", Ui.Primary))
                StartLab();
            if (Ui.Button(new Rect(bx + bw + 8f, by, bw, 36f), "Change setup"))
            {
                StopLab();
                labSetupOpen = true;
            }
            if (Ui.Button(new Rect(bx + 2f * (bw + 8f), by, bw, 36f), "Exit lab"))
                StopLab();
        }
    }
}

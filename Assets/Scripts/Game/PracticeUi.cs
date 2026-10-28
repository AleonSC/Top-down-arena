using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// Practice Range (F1 -> Practice Range): build your own waves (any enemies, any number, any level),
    /// pick a loadout and play them. Setups save as small text files you can load again later.
    public partial class ArenaApp
    {
        bool practiceSetupOpen, practiceLoadOpen;
        PracticePlan practicePlan = PracticePlan.Default();
        readonly LabSeat practiceSeat = new LabSeat { Human = true };
        int practiceSel, practiceScroll;
        string practiceName = "my practice", practiceStatus = "";
        bool practiceSeatSynced;

        static string PracticeFolder => Path.Combine(Application.persistentDataPath, "practice");

        void OpenPractice()
        {
            devMenuOpen = false;
            practiceSetupOpen = true;
            practiceLoadOpen = false;
            if (!practiceSeatSynced)  // start from the loadout picked in the lobby
            {
                practiceSeat.Role = hostRole;
                practiceSeat.Kit = kitChoice;
                practiceSeat.Weapon = weaponChoice;
                practiceSeat.Ability = abilityChoice;
                practiceSeatSynced = true;
            }
        }

        void StartPractice()
        {
            if (lab.Active)
                StopLab();
            var s = practiceSeat;
            hostRole = s.Role;
            kitChoice = s.Kit;
            weaponChoice = s.Weapon;
            abilityChoice = s.Ability;
            if (!session.Host(Session.DefaultPort + 400 + UnityEngine.Random.Range(0, 100), nameText, s.Role, s.Weapon, s.Ability, s.Kit))
            {
                practiceStatus = session.Status;
                return;
            }
            List<string> problems;
            game.Practice = PracticePlan.Parse(practicePlan.ToText(), out problems);  // a copy, so editing doesn't change the run
            session.AutoStart = 1;
            session.StartMatch();
            session.AutoStart = 0;
            practiceSetupOpen = false;
        }

        // --- Setup screen ------------------------------------------------------------------

        void DrawPracticeSetup()
        {
            var plan = practicePlan;
            if (plan.Waves.Count == 0)
                plan.Waves.Add(new PracticeWave());
            practiceSel = Mathf.Clamp(practiceSel, 0, plan.Waves.Count - 1);
            var sel = plan.Waves[practiceSel];

            var panel = DevPanel;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.55f));
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 14f, panel.width, 36f), "PRACTICE RANGE", 28, Color.white);
            Ui.Label(new Rect(panel.x, panel.y + 48f, panel.width, 18f), "Build your own waves, then play them. Esc to close.", 12, Ui.Muted, TextAnchor.MiddleCenter);

            // Waves
            float x0 = panel.x + 24f, w0 = 260f, y = panel.y + 82f;
            Ui.Heading(x0, y, w0, "Waves (" + plan.Waves.Count + ")");
            const int rows = 10;
            practiceScroll = Mathf.Clamp(practiceScroll, 0, Mathf.Max(0, plan.Waves.Count - rows));
            if (plan.Waves.Count > rows)
            {
                if (Ui.Button(new Rect(x0 + w0 - 62f, y - 4f, 30f, 22f), "^") && practiceScroll > 0)
                    practiceScroll--;
                if (Ui.Button(new Rect(x0 + w0 - 30f, y - 4f, 30f, 22f), "v"))
                    practiceScroll++;
            }
            float ly = y + 28f;
            for (int i = practiceScroll; i < plan.Waves.Count && i < practiceScroll + rows; i++)
            {
                var w = plan.Waves[i];
                var boss = PracticePlan.Choices.FirstOrDefault(t => Defs.Get(t).IsBoss && w.Counts.ContainsKey(t) && w.Counts[t] > 0);
                string label = "Wave " + (i + 1) + "  ·  lv " + w.Level + "  ·  " + (Defs.Get(boss).IsBoss ? PracticePlan.NameOf(boss) : w.Total + " enemies");
                if (Ui.Choice(new Rect(x0, ly, w0, 28f), label, i == practiceSel, Defs.Get(boss).IsBoss ? Ui.Gold : Ui.Primary))
                {
                    practiceSel = i;
                    practiceLoadOpen = false;
                }
                ly += 31f;
            }
            float by = panel.y + 110f + rows * 31f + 6f, bw = (w0 - 12f) / 3f;
            if (Ui.Button(new Rect(x0, by, bw, 30f), "+ Add"))
            {
                plan.Waves.Insert(practiceSel + 1, new PracticeWave { Level = sel.Level });
                practiceSel++;
                practiceScroll = Mathf.Max(practiceScroll, practiceSel - rows + 1);
            }
            if (Ui.Button(new Rect(x0 + bw + 6f, by, bw, 30f), "Copy"))
            {
                plan.Waves.Insert(practiceSel + 1, sel.Copy());
                practiceSel++;
                practiceScroll = Mathf.Max(practiceScroll, practiceSel - rows + 1);
            }
            if (Ui.Button(new Rect(x0 + 2f * (bw + 6f), by, bw, 30f), "Remove", Ui.Danger) && plan.Waves.Count > 1)
            {
                plan.Waves.RemoveAt(practiceSel);
                practiceSel = Mathf.Min(practiceSel, plan.Waves.Count - 1);
            }
            by += 38f;
            Ui.Label(new Rect(x0, by, 90f, 30f), "Level", 13, Ui.Muted, TextAnchor.MiddleLeft, true);
            float lx = x0 + 52f;
            foreach (int step in new[] { -5, -1 })
            {
                if (Ui.Button(new Rect(lx, by, 34f, 30f), step.ToString()))
                    sel.Level = Mathf.Clamp(sel.Level + step, 1, Game.MaxWave);
                lx += 38f;
            }
            Ui.Label(new Rect(lx, by, 40f, 30f), sel.Level.ToString(), 18, Ui.Gold, TextAnchor.MiddleCenter, true);
            lx += 44f;
            foreach (int step in new[] { 1, 5 })
            {
                if (Ui.Button(new Rect(lx, by, 34f, 30f), "+" + step))
                    sel.Level = Mathf.Clamp(sel.Level + step, 1, Game.MaxWave);
                lx += 38f;
            }
            by += 36f;
            Ui.Label(new Rect(x0, by, w0, 28f), "How strong the enemies are: their health and fire rate as on that wave of a run.", 10, Ui.Muted, TextAnchor.UpperLeft, false, true);
            by += 30f;
            plan.Loop = Ui.Toggle(new Rect(x0, by, w0, 26f), plan.Loop, "Loop the waves");

            // Loadout and the selected wave's enemies (or the saved setups)
            float x1 = x0 + w0 + 24f, w1 = panel.xMax - 24f - x1;
            DrawSeat(practiceSeat, "Your loadout", 0, x1, panel.y + 82f, w1);
            float gy = panel.y + 244f;
            if (practiceLoadOpen)
                DrawPracticeFiles(x1, gy, w1);
            else
            {
                Ui.Heading(x1, gy, w1, "Enemies in wave " + (practiceSel + 1) + " (" + sel.Total + ")");
                if (Ui.Button(new Rect(x1 + w1 - 70f, gy - 4f, 70f, 22f), "Clear"))
                    sel.Counts.Clear();
                const int cols = 4;
                float tw = (w1 - 6f * (cols - 1)) / cols, th = 30f;
                for (int i = 0; i < PracticePlan.Choices.Length; i++)
                {
                    var t = PracticePlan.Choices[i];
                    var r = new Rect(x1 + (i % cols) * (tw + 6f), gy + 28f + (i / cols) * (th + 4f), tw, th);
                    int n;
                    sel.Counts.TryGetValue(t, out n);
                    bool boss = Defs.Get(t).IsBoss;
                    Color accent = boss ? Ui.Gold : ToColor(Defs.Get(t).Color);
                    Ui.Fill(r, n > 0 ? new Color(accent.r, accent.g, accent.b, 0.22f) : new Color(1f, 1f, 1f, 0.04f));
                    if (n > 0)
                        Ui.Fill(new Rect(r.x, r.y, 3f, r.height), accent);
                    Ui.Label(new Rect(r.x + 8f, r.y, r.width - 80f, r.height), PracticePlan.NameOf(t).Replace("The ", ""), 11, boss ? Ui.Gold : n > 0 ? Color.white : Ui.Text, TextAnchor.MiddleLeft, n > 0);
                    if (Ui.Button(new Rect(r.xMax - 74f, r.y + 4f, 22f, 22f), "-") && n > 0)
                        n--;
                    Ui.Label(new Rect(r.xMax - 52f, r.y, 26f, r.height), n.ToString(), 13, n > 0 ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
                    if (Ui.Button(new Rect(r.xMax - 26f, r.y + 4f, 22f, 22f), "+"))
                        n = Mathf.Min(99, n + 1);
                    if (n > 0)
                        sel.Counts[t] = n;
                    else
                        sel.Counts.Remove(t);
                }
            }

            // Save / load / start
            float fy = panel.yMax - 54f;
            if (practiceStatus.Length > 0)
                Ui.Label(new Rect(x0, fy - 24f, panel.width - 48f, 20f), practiceStatus, 11, Ui.Muted, TextAnchor.MiddleLeft);
            practiceName = Ui.Field(new Rect(x0, fy, 180f, 36f), practiceName, 40);
            if (Ui.Button(new Rect(x0 + 186f, fy, 64f, 36f), "Save"))
                SavePractice();
            if (Ui.Button(new Rect(x0 + 256f, fy, 64f, 36f), practiceLoadOpen ? "Back" : "Load"))
                practiceLoadOpen = !practiceLoadOpen;
            if (Ui.Button(new Rect(x0 + 326f, fy, 110f, 36f), "Open folder"))
            {
                Directory.CreateDirectory(PracticeFolder);
                Application.OpenURL("file:///" + PracticeFolder.Replace('\\', '/'));
            }
            var start = new Rect(panel.xMax - 24f - 190f, fy, 190f, 36f);
            if (Ui.Button(start, "Start practice", Ui.Primary))
                StartPractice();
            if (Ui.Button(new Rect(start.x - 106f, fy, 100f, 36f), "Close"))
                practiceSetupOpen = false;
        }

        void DrawPracticeFiles(float x, float y, float w)
        {
            Ui.Heading(x, y, w, "Saved setups");
            string[] files;
            try
            {
                files = Directory.Exists(PracticeFolder)
                    ? Directory.GetFiles(PracticeFolder, "*.txt").OrderByDescending(File.GetLastWriteTime).Take(16).ToArray()
                    : new string[0];
            }
            catch (Exception)
            {
                files = new string[0];
            }
            if (files.Length == 0)
            {
                Ui.Label(new Rect(x, y + 30f, w, 40f), "Nothing saved yet. Name your setup and press Save; it goes in the practice folder as a text file.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
                return;
            }
            float bw = (w - 6f) / 2f;
            for (int i = 0; i < files.Length; i++)
            {
                var r = new Rect(x + (i % 2) * (bw + 6f), y + 28f + (i / 2) * 32f, bw, 28f);
                if (Ui.Button(r, Path.GetFileNameWithoutExtension(files[i])))
                    LoadPractice(files[i]);
            }
        }

        void SavePractice()
        {
            string clean = new string((practiceName ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim();
            if (clean.Length == 0)
                clean = "practice";
            practiceName = clean;
            try
            {
                Directory.CreateDirectory(PracticeFolder);
                string path = Path.Combine(PracticeFolder, clean + ".txt");
                bool existed = File.Exists(path);
                File.WriteAllText(path, practicePlan.ToText(clean));
                practiceStatus = (existed ? "Updated " : "Saved ") + path;
            }
            catch (Exception e)
            {
                practiceStatus = "Couldn't save: " + e.Message;
            }
        }

        void LoadPractice(string path)
        {
            try
            {
                List<string> problems;
                var plan = PracticePlan.Parse(File.ReadAllText(path), out problems);
                if (plan.Waves.Count == 0)
                {
                    practiceStatus = "No waves in " + Path.GetFileName(path);
                    return;
                }
                practicePlan = plan;
                practiceSel = practiceScroll = 0;
                practiceName = Path.GetFileNameWithoutExtension(path);
                practiceLoadOpen = false;
                practiceStatus = "Loaded " + Path.GetFileName(path) + (problems.Count > 0 ? "  (" + string.Join("; ", problems.Take(3)) + ")" : "");
            }
            catch (Exception e)
            {
                practiceStatus = "Couldn't load: " + e.Message;
            }
        }
    }
}

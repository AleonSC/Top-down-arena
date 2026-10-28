using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The Practice Range editor, in the lobby (LobbyUi.cs) when the host picks Practice Range: any number
    /// of waves, each with its own enemies (shown live, so you don't need to know their names) and level.
    /// Everyone sees the setup as it changes; the host can let the others edit it too. Setups save as small
    /// text files you can load again later.
    public partial class ArenaApp
    {
        int practiceSel, practiceScroll, practiceSeenRev = -1;
        float practiceFlashT = -10f;
        bool practiceLoadOpen;
        string practiceName = "my practice", practiceStatus = "";
        readonly Dictionary<EnemyType, Enemy> previewEnemies = new Dictionary<EnemyType, Enemy>();

        static string PracticeFolder => Path.Combine(Application.persistentDataPath, "practice");

        string MyLobbyName()
        {
            LobbyEntry me;
            return session.Lobby.TryGetValue(session.MyId, out me) ? me.Name : "";
        }

        void PracticeChanged() => session.PracticeEdited(practiceSel);

        void DrawPracticeEditor(Rect r, float t)
        {
            var plan = session.Plan;
            bool canEdit = session.CanEditPractice;
            if (plan.Waves.Count == 0)
                plan.Waves.Add(new PracticeWave());

            // Someone else changed it: viewers jump to the wave they're working on; editors get a marker.
            if (session.PlanRevision != practiceSeenRev)
            {
                if (practiceSeenRev >= 0 && session.PlanEditor != MyLobbyName() && session.PlanEditor.Length > 0)
                {
                    practiceFlashT = t;
                    if (!canEdit)
                        practiceSel = session.PlanEditorSel;
                }
                practiceSeenRev = session.PlanRevision;
            }
            practiceSel = Mathf.Clamp(practiceSel, 0, plan.Waves.Count - 1);
            var sel = plan.Waves[practiceSel];

            Ui.Panel(r);
            DrawPracticeHeader(r, t, canEdit);

            // --- Waves (left): a numbered list, then what you can do to the selected wave.
            float x0 = r.x + 16f, w0 = 214f, y = r.y + 58f;
            const int rows = 7;
            const float rowH = 26f, rowStep = 29f;
            Ui.Label(new Rect(x0, y, w0, 14f), "WAVES  ·  " + plan.Waves.Count, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 20f;
            practiceScroll = Mathf.Clamp(practiceScroll, 0, Mathf.Max(0, plan.Waves.Count - rows));
            if (practiceSel < practiceScroll)
                practiceScroll = practiceSel;
            if (practiceSel >= practiceScroll + rows)
                practiceScroll = practiceSel - rows + 1;
            string other = session.PlanEditor != MyLobbyName() ? session.PlanEditor : "";
            for (int i = practiceScroll; i < plan.Waves.Count && i < practiceScroll + rows; i++)
            {
                var w = plan.Waves[i];
                var row = new Rect(x0, y, w0, rowH);
                var boss = BossIn(w);
                bool on = i == practiceSel;
                Color c = boss != null ? Ui.Gold : PracticeColor;
                Ui.Pill(row, on ? new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, 0.95f) : new Color(1f, 1f, 1f, row.Contains(Event.current.mousePosition) ? 0.08f : 0.035f));
                if (other.Length > 0 && session.PlanEditorSel == i && t - practiceFlashT < 3f)
                    Ui.Pill(row, new Color(1f, 1f, 1f, 0.12f * (1f - (t - practiceFlashT) / 3f)));
                var num = new Rect(row.x + 4f, row.y + 3f, 20f, 20f);  // the wave's number in a round badge
                Ui.Pill(num, on ? c : new Color(1f, 1f, 1f, 0.08f));
                Ui.Label(num, (i + 1).ToString(), 11, on ? new Color(0.05f, 0.06f, 0.09f) : Ui.Muted, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(row.x + 30f, row.y, 40f, row.height), "Lv " + w.Level, 10, on ? new Color(1f, 1f, 1f, 0.75f) : Ui.Muted, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(row.x + 70f, row.y, row.width - 76f, row.height), boss != null ? PracticePlan.NameOf(boss.Value).Replace("The ", "") : w.Total == 1 ? "1 enemy" : w.Total + " enemies", 12,
                    boss != null ? Ui.Gold : on ? Color.white : Ui.Text, TextAnchor.MiddleLeft, boss != null || on);
                if (other.Length > 0 && session.PlanEditorSel == i)  // where the other editor is working
                    Ui.Badge(new Rect(row.xMax - 54f, row.y + 5f, 50f, 16f), other.Length > 6 ? other.Substring(0, 6) : other, Ui.Accent);
                if (!on && GUI.Button(row, GUIContent.none, GUIStyle.none))
                {
                    practiceSel = i;
                    practiceLoadOpen = false;
                }
                y += rowStep;
            }
            float by = r.y + 78f + rows * rowStep;
            if (plan.Waves.Count > rows)
            {
                Ui.Label(new Rect(x0, by, w0, 16f), (practiceScroll + 1) + "-" + Math.Min(plan.Waves.Count, practiceScroll + rows) + " of " + plan.Waves.Count, 10, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.SmallButton(new Rect(x0, by - 1f, 28f, 18f), "▲", 9) && practiceScroll > 0)
                    practiceScroll--;
                if (Ui.SmallButton(new Rect(x0 + w0 - 28f, by - 1f, 28f, 18f), "▼", 9))
                    practiceScroll++;
            }
            by += 20f;

            bool wasEnabled = GUI.enabled;
            GUI.enabled = canEdit;
            float bw = (w0 - 12f) / 3f;
            if (Ui.SmallButton(new Rect(x0, by, bw, 26f), "+ Add", new Color(PracticeColor.r * 0.8f, PracticeColor.g * 0.8f, PracticeColor.b * 0.8f)))
            {
                plan.Waves.Insert(practiceSel + 1, new PracticeWave { Level = sel.Level });
                practiceSel++;
                PracticeChanged();
            }
            if (Ui.SmallButton(new Rect(x0 + bw + 6f, by, bw, 26f), "Copy"))
            {
                plan.Waves.Insert(practiceSel + 1, sel.Copy());
                practiceSel++;
                PracticeChanged();
            }
            if (Ui.SmallButton(new Rect(x0 + 2f * (bw + 6f), by, bw, 26f), "Remove", Ui.Danger) && plan.Waves.Count > 1)
            {
                plan.Waves.RemoveAt(practiceSel);
                practiceSel = Mathf.Min(practiceSel, plan.Waves.Count - 1);
                PracticeChanged();
            }
            by += 34f;

            // Level: a stepper in a pill.
            var lp = new Rect(x0, by, w0, 30f);
            Ui.Pill(lp, new Color(0f, 0f, 0f, 0.3f));
            Ui.Label(new Rect(lp.x + 10f, lp.y, 50f, lp.height), "LEVEL", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            float lx = lp.x + 52f;
            foreach (int step in new[] { -5, -1, 0, 1, 5 })
            {
                if (step == 0)
                {
                    Ui.Fancy(new Rect(lx, lp.y, 38f, lp.height), sel.Level.ToString(), 17, Color.white, Ui.Gold, TextAnchor.MiddleCenter);
                    lx += 38f;
                    continue;
                }
                if (Ui.SmallButton(new Rect(lx, lp.y + 3f, 28f, 24f), step > 0 ? "+" + step : "−" + (-step), 11))
                {
                    sel.Level = Mathf.Clamp(sel.Level + step, 1, Game.MaxWave);
                    PracticeChanged();
                }
                lx += 31f;
            }
            by += 38f;
            bool loop = Ui.Toggle(new Rect(x0, by, w0, 22f), plan.Loop, "Loop the waves");
            if (loop != plan.Loop && canEdit)
            {
                plan.Loop = loop;
                PracticeChanged();
            }
            GUI.enabled = wasEnabled;
            Ui.Label(new Rect(x0, by + 26f, w0, 28f), "Level sets enemy health and fire rate, as on that wave of a run.", 10, Ui.Muted, TextAnchor.UpperLeft, false, true);

            // --- Enemies (right): or the saved setups.
            Ui.Fill(new Rect(x0 + w0 + 12f, r.y + 58f, 1f, r.height - 58f - 60f), new Color(1f, 1f, 1f, 0.05f));
            var grid = new Rect(x0 + w0 + 26f, r.y + 58f, r.xMax - 16f - (x0 + w0 + 26f), r.height - 58f - 62f);
            if (practiceLoadOpen)
                DrawPracticeFiles(grid);
            else
                DrawEnemyGrid(grid, sel, canEdit, t);

            DrawPracticeFooter(r, canEdit);
        }

        static EnemyType? BossIn(PracticeWave w)
        {
            foreach (var t in PracticePlan.Choices)
                if (Defs.Get(t).IsBoss && w.Counts.ContainsKey(t) && w.Counts[t] > 0)
                    return t;
            return null;
        }

        void DrawPracticeHeader(Rect r, float t, bool canEdit)
        {
            float x = r.x + 16f, y = r.y + 10f;
            string status;
            Color sc;
            if (session.IsHost)
            {
                status = session.OthersCanEdit ? "Everyone can edit these waves" : "You're editing  ·  the others see every change";
                sc = PracticeColor;
            }
            else if (canEdit)
            {
                status = "You can edit these waves  ·  everyone sees every change";
                sc = Ui.Good;
            }
            else
            {
                status = "Watching " + HostName() + "'s setup  ·  only the host can edit";
                sc = Ui.Muted;
            }
            // A live dot, then who's editing.
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Ui.Pill(new Rect(x, y + 9f, 10f, 10f), new Color(sc.r, sc.g, sc.b, 0.35f + 0.5f * pulse));
            Ui.Label(new Rect(x + 18f, y, r.width - 250f, 28f), status, 13, Ui.Text, TextAnchor.MiddleLeft, true);
            if (session.PlanEditor.Length > 0 && session.PlanEditor != MyLobbyName() && t - practiceFlashT < 4f)
                Ui.Label(new Rect(x + 18f, y + 22f, r.width - 250f, 16f), session.PlanEditor + " changed wave " + (session.PlanEditorSel + 1), 11,
                    new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 1f - (t - practiceFlashT) / 4f));
            if (session.IsHost)
            {
                bool on = Ui.Toggle(new Rect(r.xMax - 176f, y + 3f, 160f, 22f), session.OthersCanEdit, "Let others edit");
                if (on != session.OthersCanEdit)
                    session.SetOthersCanEdit(on);
            }
            else if (canEdit)
                Ui.Badge(new Rect(r.xMax - 116f, y + 4f, 100f, 20f), "YOU CAN EDIT", new Color(0.2f, 0.62f, 0.32f));
            Ui.Fill(new Rect(r.x + 14f, r.y + 46f, r.width - 28f, 1f), new Color(1f, 1f, 1f, 0.06f));
        }

        /// The enemies to pick from, each drawn live: click to add one, right-click (or the corner -) to take one away.
        void DrawEnemyGrid(Rect g, PracticeWave sel, bool canEdit, float t)
        {
            const int cols = 6;
            const float gap = 6f, infoH = 46f, headH = 20f;
            float tw = (g.width - gap * (cols - 1)) / cols;
            var tiles = new Rect[PracticePlan.Choices.Length];
            int regular = PracticePlan.Choices.Count(c => !Defs.Get(c).IsBoss);
            int tileRows = (regular + cols - 1) / cols + (tiles.Length - regular + cols - 1) / cols;
            // Tiles shrink to fit above the info bar.
            float th = Mathf.Min(62f, (g.height - infoH - 14f - 2f * headH - 6f - gap * (tileRows - 1)) / tileRows);
            float y = g.y;
            Ui.Label(new Rect(g.x, y, g.width, 14f), "ENEMIES IN WAVE " + (practiceSel + 1) + "  ·  " + sel.Total, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (canEdit && sel.Total > 0 && Ui.SmallButton(new Rect(g.xMax - 56f, y - 3f, 56f, 20f), "Clear", 11))
            {
                sel.Counts.Clear();
                PracticeChanged();
            }
            y += headH;
            for (int i = 0; i < tiles.Length; i++)
            {
                int k = i < regular ? i : i - regular;
                if (i == regular)
                {
                    y += (regular + cols - 1) / cols * (th + gap) + 4f;
                    Ui.Label(new Rect(g.x, y, g.width, 14f), "BOSSES  ·  they bring their usual adds", 10, Ui.Gold, TextAnchor.MiddleLeft, true);
                    y += headH;
                }
                tiles[i] = new Rect(g.x + (k % cols) * (tw + gap), y + (k / cols) * (th + gap), tw, th);
            }

            var ev = Event.current;
            EnemyType? hovered = null;
            // Tile backs (and clicks).
            for (int i = 0; i < tiles.Length; i++)
            {
                var type = PracticePlan.Choices[i];
                var tr = tiles[i];
                int n;
                sel.Counts.TryGetValue(type, out n);
                bool boss = Defs.Get(type).IsBoss, hover = tr.Contains(ev.mousePosition);
                if (hover)
                    hovered = type;
                Color accent = boss ? Ui.Gold : ToColor(Defs.Get(type).Color);
                Ui.Pill(tr, n > 0 ? new Color(accent.r * 0.3f, accent.g * 0.3f, accent.b * 0.3f, 0.95f) : new Color(1f, 1f, 1f, hover ? 0.09f : 0.04f));
                if (n > 0)
                    Ui.Card(tr, accent, 0.9f);
                else if (hover)
                    Ui.Card(tr, accent, 0.35f);
                if (canEdit && ev.type == EventType.MouseDown && hover)
                {
                    var minus = new Rect(tr.x + 2f, tr.y + 2f, 18f, 18f);
                    int change = ev.button == 1 || (n > 0 && minus.Contains(ev.mousePosition)) ? -1 : ev.button == 0 ? 1 : 0;
                    int next = Mathf.Clamp(n + change, 0, 99);
                    if (next != n)
                    {
                        if (next > 0)
                            sel.Counts[type] = next;
                        else
                            sel.Counts.Remove(type);
                        PracticeChanged();
                    }
                    ev.Use();
                }
            }
            // The enemies themselves.
            if (BeginUiGl())
            {
                for (int i = 0; i < tiles.Length; i++)
                {
                    var type = PracticePlan.Choices[i];
                    int n;
                    sel.Counts.TryGetValue(type, out n);
                    bool hover = hovered == type;
                    DrawEnemyPreview(type, new V2(tiles[i].center.x, tiles[i].y + (th - 14f) / 2f + 1f), Mathf.Min(40f, th - 18f), t, n > 0 || hover);
                }
                if (hovered != null)
                    DrawEnemyPreview(hovered.Value, new V2(g.x + 24f, g.yMax - infoH / 2f), 32f, t, true);
                EndUiGl();
            }
            // Names and counts on top.
            for (int i = 0; i < tiles.Length; i++)
            {
                var type = PracticePlan.Choices[i];
                var tr = tiles[i];
                int n;
                sel.Counts.TryGetValue(type, out n);
                bool boss = Defs.Get(type).IsBoss;
                Ui.Label(new Rect(tr.x + 2f, tr.yMax - 15f, tr.width - 4f, 13f), PracticePlan.NameOf(type).Replace("The ", ""), 10,
                    boss ? Ui.Gold : n > 0 ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, n > 0);
                if (n > 0)
                {
                    var badge = new Rect(tr.xMax - 30f, tr.y + 3f, 27f, 17f);
                    Ui.Pill(badge, new Color(0f, 0f, 0f, 0.65f));
                    Ui.Label(badge, "×" + n, 11, Color.white, TextAnchor.MiddleCenter, true);
                    if (canEdit)
                    {
                        var minus = new Rect(tr.x + 3f, tr.y + 3f, 17f, 17f);
                        Ui.Pill(minus, new Color(0f, 0f, 0f, minus.Contains(ev.mousePosition) ? 0.8f : 0.5f));
                        Ui.Label(minus, "−", 13, Color.white, TextAnchor.MiddleCenter, true);
                    }
                }
            }
            // What the hovered one does, in a bar under the tiles (its enemy is drawn at the left, above).
            var info = new Rect(g.x, g.yMax - infoH, g.width, infoH);
            Ui.Pill(info, new Color(0f, 0f, 0f, 0.28f));
            if (hovered != null)
            {
                bool boss = Defs.Get(hovered.Value).IsBoss;
                Ui.Label(new Rect(info.x + 50f, info.y + 4f, info.width - 60f, 16f), PracticePlan.NameOf(hovered.Value), 12, boss ? Ui.Gold : Color.white, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(info.x + 50f, info.y + 20f, info.width - 60f, 26f), PracticePlan.BlurbOf(hovered.Value), 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            }
            else
                Ui.Label(new Rect(info.x + 14f, info.y, info.width - 28f, infoH), canEdit
                    ? "Click an enemy to add one, right-click (or its −) to take one away. Hover one to see what it does."
                    : "Hover an enemy to see what it does.", 11, Ui.Muted, TextAnchor.MiddleCenter, false, true);
        }

        /// One enemy, drawn as in a match and fitted into a box `size` across (call between BeginUiGl and EndUiGl).
        void DrawEnemyPreview(EnemyType type, V2 center, float size, float t, bool lively)
        {
            Enemy e;
            if (!previewEnemies.TryGetValue(type, out e))
            {
                var def = Defs.Get(type);
                e = new Enemy { Id = 900 + (int)type, Type = type, Def = def, Radius = def.Radius, Hp = 1f, MaxHp = 1f };
                previewEnemies[type] = e;
            }
            float r = e.Radius;
            float extent = type == EnemyType.Singularity ? r * 1.65f : e.Def.IsBoss ? r + 16f : r + (e.Def.Melee ? 7f : 4f);
            float zoom = Mathf.Min(1.25f, size * 0.5f / extent);
            float speed = lively ? 1f : 0.35f;
            e.Facing = t * 0.9f * speed + e.Id;
            e.Phase = (byte)(type == EnemyType.Spinner && lively && t % 3.3f < 1.3f ? 1 : 0);
            V2 bob = new V2(0f, (lively ? 2.5f : 1f) * Mathf.Sin(t * 2.2f + e.Id));
            if (type == EnemyType.TwinA)  // the pair, side by side
            {
                Enemy b;
                if (!previewEnemies.TryGetValue(EnemyType.TwinB, out b))
                {
                    var def = Defs.Get(EnemyType.TwinB);
                    b = new Enemy { Id = 950, Type = EnemyType.TwinB, Def = def, Radius = def.Radius, Hp = 1f, MaxHp = 1f };
                    previewEnemies[EnemyType.TwinB] = b;
                }
                zoom *= 0.62f;
                b.Facing = -e.Facing;
                UiModelAt(center, zoom);
                e.Disp = new V2(-r * 1.05f, 0f) + bob;
                b.Disp = new V2(r * 1.05f, 0f) - bob;
                Line(e.Disp, b.Disp, 2f, new Rgba(0.9f, 0.85f, 1f, 0.35f + 0.2f * Mathf.Sin(t * 5f)));  // their tether
                DrawEnemy(e, t * speed);
                DrawEnemy(b, t * speed);
                return;
            }
            UiModelAt(center, zoom);
            e.Disp = bob;
            menuPreview = true;  // e.g. a Phantom doesn't flicker out of sight in its tile
            DrawEnemy(e, t * speed);
            menuPreview = false;
        }

        void DrawPracticeFooter(Rect r, bool canEdit)
        {
            float x = r.x + 16f, y = r.yMax - 44f;
            Ui.Fill(new Rect(r.x + 14f, y - 10f, r.width - 28f, 1f), new Color(1f, 1f, 1f, 0.06f));
            Ui.Label(new Rect(x, y, 54f, 30f), "SETUP", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            practiceName = Ui.Field(new Rect(x + 48f, y, 170f, 30f), practiceName, 40);
            float bx = x + 226f;
            if (Ui.SmallButton(new Rect(bx, y, 64f, 30f), "Save", new Color(PracticeColor.r * 0.8f, PracticeColor.g * 0.8f, PracticeColor.b * 0.8f)))
                SavePractice();
            bool wasEnabled = GUI.enabled;
            GUI.enabled = canEdit;
            if (Ui.SmallButton(new Rect(bx + 70f, y, 64f, 30f), practiceLoadOpen ? "Back" : "Load", practiceLoadOpen ? Ui.Primary : Ui.Neutral))
                practiceLoadOpen = !practiceLoadOpen;
            GUI.enabled = wasEnabled;
            if (Ui.SmallButton(new Rect(bx + 140f, y, 96f, 30f), "Open folder"))
            {
                Directory.CreateDirectory(PracticeFolder);
                Application.OpenURL("file:///" + PracticeFolder.Replace('\\', '/'));
            }
            float sx = bx + 248f;
            Ui.Label(new Rect(sx, y, r.xMax - 16f - sx, 30f), practiceStatus.Length > 0 ? practiceStatus : PracticeSummary(), 11,
                practiceStatus.Length > 0 ? Ui.Muted : PracticeColor, TextAnchor.MiddleRight, practiceStatus.Length == 0);
        }

        void DrawPracticeFiles(Rect g)
        {
            Ui.Label(new Rect(g.x, g.y, g.width, 14f), "SAVED SETUPS  ·  click one to load it", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            string[] files;
            try
            {
                files = Directory.Exists(PracticeFolder)
                    ? Directory.GetFiles(PracticeFolder, "*.txt").OrderByDescending(File.GetLastWriteTime).Take(20).ToArray()
                    : new string[0];
            }
            catch (Exception)
            {
                files = new string[0];
            }
            if (files.Length == 0)
            {
                Ui.Label(new Rect(g.x, g.y + 24f, g.width, 40f), "Nothing saved yet. Name your setup and press Save: it goes in the practice folder as a text file you can share or edit.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
                return;
            }
            float bw = (g.width - 6f) / 2f;
            for (int i = 0; i < files.Length; i++)
            {
                var b = new Rect(g.x + (i % 2) * (bw + 6f), g.y + 22f + (i / 2) * 34f, bw, 30f);
                if (Ui.SmallButton(b, Path.GetFileNameWithoutExtension(files[i]), 13))
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
                File.WriteAllText(path, session.Plan.ToText(clean));
                practiceStatus = (existed ? "Updated " : "Saved ") + clean + ".txt";
            }
            catch (Exception e)
            {
                practiceStatus = "Couldn't save: " + e.Message;
            }
        }

        void LoadPractice(string path)
        {
            if (!session.CanEditPractice)
                return;
            try
            {
                List<string> problems;
                var plan = PracticePlan.Parse(File.ReadAllText(path), out problems);
                if (plan.Waves.Count == 0)
                {
                    practiceStatus = "No waves in " + Path.GetFileName(path);
                    return;
                }
                practiceSel = practiceScroll = 0;
                practiceName = Path.GetFileNameWithoutExtension(path);
                practiceLoadOpen = false;
                practiceStatus = "Loaded " + Path.GetFileName(path) + (problems.Count > 0 ? "  (" + string.Join("; ", problems.Take(2)) + ")" : "");
                session.Plan = plan;
                PracticeChanged();
            }
            catch (Exception e)
            {
                practiceStatus = "Couldn't load: " + e.Message;
            }
        }
    }
}

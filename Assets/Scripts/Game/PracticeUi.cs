using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The Practice tab of the lobby (LobbyUi.cs), in the CRT look of Docs/Mockups/crt-practice-redesign.html (look A, the wave rack): any number
    /// of waves, each with its own enemies (shown live, so you don't need to know their names) and level.
    /// Everyone sees the setup as it changes; the host can let the others edit it too. Setups save as small
    /// text files you can load again later.
    public partial class ArenaApp
    {
        int practiceSel, practiceScroll, practiceSeenRev = -1;
        float practiceFlashT = -10f;
        bool practiceLoadOpen;
        string practiceName = "my practice", practiceStatus = "";
        static readonly Color PracticeCyan = new Color(0.37f, 0.94f, 1f), TrainAmber = new Color(1f, 0.72f, 0.28f);
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
            bool canEdit = session.CanEditPractice, calm = fxReduced;
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

            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            DrawPracticeHeader(r, t, canEdit, calm);

            // --- Waves (left): the rack, then what you can do to the selected wave.
            float x0 = r.x + 16f, w0 = 196f, y = r.y + 54f;
            const int rows = 5;
            const float rowH = 48f, rowStep = 54f;
            LobbyKit.Heading(x0, y, w0, "Waves", plan.Waves.Count.ToString(), calm, 16);
            y += 38f;
            practiceScroll = Mathf.Clamp(practiceScroll, 0, Mathf.Max(0, plan.Waves.Count - rows));
            if (practiceSel < practiceScroll)
                practiceScroll = practiceSel;
            if (practiceSel >= practiceScroll + rows)
                practiceScroll = practiceSel - rows + 1;
            string other = session.PlanEditor != MyLobbyName() ? session.PlanEditor : "";
            for (int i = practiceScroll; i < plan.Waves.Count && i < practiceScroll + rows; i++)
            {
                var row = new Rect(x0, y, w0, rowH);
                DrawWaveCard(row, i, plan.Waves[i], i == practiceSel, other, t);
                if (i != practiceSel && Ui.HitArea(row))
                {
                    practiceSel = i;
                    practiceLoadOpen = false;
                }
                y += rowStep;
            }
            float by = r.y + 54f + 38f + rows * rowStep;
            if (plan.Waves.Count > rows)
            {
                LobbyKit.Spaced(new Rect(x0 + 34f, by - 2f, w0 - 68f, 18f), (practiceScroll + 1) + "-" + Math.Min(plan.Waves.Count, practiceScroll + rows) + " OF " + plan.Waves.Count, 10, CrtUi.Dim, TextAnchor.MiddleCenter, 1.2f);
                if (LobbyKit.Key(new Rect(x0, by - 2f, 28f, 20f), "▲", false, calm, size: 10, minSize: 9) && practiceScroll > 0)
                    practiceScroll--;
                if (LobbyKit.Key(new Rect(x0 + w0 - 28f, by - 2f, 28f, 20f), "▼", false, calm, size: 10, minSize: 9))
                    practiceScroll++;
            }
            by += 22f;

            float bw = (w0 - 12f) / 3f;
            if (LobbyKit.Key(new Rect(x0, by, bw, 30f), "+ ADD", false, calm, size: 13, minSize: 10, enabled: canEdit, accent: ReviveMintUi))
            {
                plan.Waves.Insert(practiceSel + 1, new PracticeWave { Level = sel.Level });
                practiceSel++;
                PracticeChanged();
            }
            if (LobbyKit.Key(new Rect(x0 + bw + 6f, by, bw, 30f), "COPY", false, calm, size: 13, minSize: 10, enabled: canEdit))
            {
                plan.Waves.Insert(practiceSel + 1, sel.Copy());
                practiceSel++;
                PracticeChanged();
            }
            if (LobbyKit.Key(new Rect(x0 + 2f * (bw + 6f), by, bw, 30f), "REMOVE", false, calm, size: 13, minSize: 9, danger: true, enabled: canEdit) && plan.Waves.Count > 1)
            {
                plan.Waves.RemoveAt(practiceSel);
                practiceSel = Mathf.Min(practiceSel, plan.Waves.Count - 1);
                PracticeChanged();
            }
            by += 42f;

            // Level: a stepper.
            LobbyKit.Lab(new Rect(x0, by, w0, 16f), "Level  ·  health + fire rate", 10, calm);
            by += 20f;
            float lx = x0;
            foreach (int step in new[] { -5, -1, 0, 1, 5 })
            {
                if (step == 0)
                {
                    CrtUi.Title(new Rect(lx, by, 40f, 30f), sel.Level.ToString(), 28, Color.white, TextAnchor.MiddleCenter, calm);
                    lx += 44f;
                    continue;
                }
                if (LobbyKit.Key(new Rect(lx, by, 32f, 30f), step > 0 ? "+" + step : "−" + (-step), false, calm, size: 12, minSize: 10, enabled: canEdit))
                {
                    sel.Level = Mathf.Clamp(sel.Level + step, 1, Game.MaxWave);
                    PracticeChanged();
                }
                lx += 36f;
            }
            by += 40f;
            if (LobbyKit.Key(new Rect(x0, by, w0, 32f), "LOOP THE WAVES", plan.Loop, calm, size: 14, minSize: 10, enabled: canEdit, accent: PracticeCyan))
            {
                plan.Loop = !plan.Loop;
                PracticeChanged();
            }
            Ui.Label(new Rect(x0, by + 38f, w0, 30f), "Level sets enemy health and fire rate, as on that wave of a run.", 10, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);

            // --- Enemies (right): or the saved setups.
            float gx = x0 + w0 + 18f;
            Ui.Fill(new Rect(gx - 9f, r.y + 54f, 1f, r.height - 54f - 70f), LobbyKit.Ink(0.22f));
            var grid = new Rect(gx, r.y + 54f, r.xMax - 16f - gx, r.height - 54f - 70f);
            if (practiceLoadOpen)
                DrawPracticeFiles(grid, calm);
            else
                DrawEnemyGrid(grid, sel, canEdit, t, calm);

            DrawPracticeFooter(r, canEdit, calm);
        }

        /// One wave in the rack: its number and level, then a line on what's in it. The selected wave is lit; a boss wave is gold.
        void DrawWaveCard(Rect row, int i, PracticeWave w, bool on, string other, float t)
        {
            var boss = BossIn(w);
            Color c = boss != null ? CrtStyle.Gold : PracticeCyan;
            Ui.Fill(row, new Color(c.r, c.g, c.b, on ? 0.16f : row.Contains(Event.current.mousePosition) ? 0.09f : 0.04f));
            CrtKit.Frame(row, on ? 2f : 1f, new Color(c.r, c.g, c.b, on ? 0.95f : 0.35f));
            if (other.Length > 0 && session.PlanEditorSel == i && t - practiceFlashT < 3f)
                Ui.Fill(row, new Color(1f, 1f, 1f, 0.12f * (1f - (t - practiceFlashT) / 3f)));
            LobbyKit.Spaced(new Rect(row.x + 10f, row.y + 2f, 100f, 22f), "WAVE " + (i + 1), 15, on ? Color.white : CrtStyle.Body, TextAnchor.MiddleLeft, 1.4f);
            LobbyKit.Spaced(new Rect(row.xMax - 70f, row.y + 2f, 62f, 22f), "LV " + w.Level.ToString("00"), 14, c, TextAnchor.MiddleRight, 1.2f);
            string brief = boss != null ? PracticePlan.NameOf(boss.Value) : w.Total == 1 ? "1 enemy" : w.Total + " enemies";
            foreach (var type in PracticePlan.Choices)
            {
                int n;
                if (w.Counts.TryGetValue(type, out n) && n > 0 && (boss == null || type != boss.Value))
                    brief += "  ·  " + PracticePlan.NameOf(type).Replace("The ", "") + " ×" + n;
            }
            Ui.Label(new Rect(row.x + 10f, row.y + 24f, row.width - 20f, 22f), LobbyKit.Truncate(brief, 11, row.width - (other.Length > 0 && session.PlanEditorSel == i ? 76f : 20f), false), 11, boss != null ? CrtStyle.Gold : CrtStyle.Desc, TextAnchor.MiddleLeft, false);
            if (other.Length > 0 && session.PlanEditorSel == i)  // where the other editor is working
                DkTag(new Rect(row.xMax - 60f, row.y + 28f, 52f, 16f), other.Length > 6 ? other.Substring(0, 6) : other, Ui.Accent);
        }

        static EnemyType? BossIn(PracticeWave w)
        {
            foreach (var t in PracticePlan.Choices)
                if (Defs.Get(t).IsBoss && w.Counts.ContainsKey(t) && w.Counts[t] > 0)
                    return t;
            return null;
        }

        /// The top line: who is editing (a live dot), "NO XP · RESPAWN ON", and the host's switch to let the others edit.
        void DrawPracticeHeader(Rect r, float t, bool canEdit, bool calm)
        {
            float x = r.x + 16f, y = r.y + 10f;
            string status;
            Color sc;
            if (session.IsHost)
            {
                status = session.OthersCanEdit ? "Everyone can edit these waves" : "You're editing  ·  the others see every change";
                sc = PracticeCyan;
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
            float pulse = calm ? 0.8f : 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Ui.Fill(new Rect(x, y + 11f, 8f, 8f), new Color(sc.r, sc.g, sc.b, 0.35f + 0.6f * pulse));
            Ui.Label(new Rect(x + 16f, y, 340f, 30f), LobbyKit.Truncate(status.ToUpperInvariant(), 12, 340f), 12, CrtStyle.Body, TextAnchor.MiddleLeft, true);
            if (session.PlanEditor.Length > 0 && session.PlanEditor != MyLobbyName() && t - practiceFlashT < 4f)
                Ui.Label(new Rect(x + 16f, y + 22f, 340f, 16f), session.PlanEditor + " changed wave " + (session.PlanEditorSel + 1), 11,
                    new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 1f - (t - practiceFlashT) / 4f));
            // No profile XP here, and a downed player can respawn (PracticeRespawn.cs).
            var mint = ReviveMintUi;
            Ui.Fill(new Rect(r.xMax - 396f, y + 11f, 8f, 8f), mint);
            LobbyKit.Spaced(new Rect(r.xMax - 382f, y, 220f, 30f), "NO XP  ·  RESPAWN ON", 12, mint, TextAnchor.MiddleLeft, 1.4f);
            if (session.IsHost)
            {
                if (LobbyKit.Key(new Rect(r.xMax - 160f, y, 144f, 30f), "LET OTHERS EDIT", session.OthersCanEdit, calm, size: 12, minSize: 9, accent: PracticeCyan))
                    session.SetOthersCanEdit(!session.OthersCanEdit);
            }
            else if (canEdit)
                DkTag(new Rect(r.xMax - 116f, y + 5f, 100f, 20f), "YOU CAN EDIT", new Color(0.2f, 0.62f, 0.32f));
            Ui.Fill(new Rect(r.x + 14f, r.y + 46f, r.width - 28f, 1f), LobbyKit.Ink(0.25f));
        }

        /// The enemies to pick from, each drawn live and grouped Melee, Ranged, Late and Bosses: click to add one, right-click (or the corner -) to take one away.
        void DrawEnemyGrid(Rect g, PracticeWave sel, bool canEdit, float t, bool calm)
        {
            const int cols = 6;
            const float gap = 5f, infoH = 44f, headH = 18f, titleH = 34f;
            float tw = (g.width - gap * (cols - 1)) / cols;
            var types = PracticePlan.Choices;
            var tiles = new Rect[types.Length];
            // Two blocks, regular enemies then bosses (the picker's four groups don't leave room for tiles big enough to show the enemy).
            var groups = new List<int>();  // index of each group's first type
            for (int i = 0; i < types.Length; i++)
                if (i == 0 || Defs.Get(types[i]).IsBoss != Defs.Get(types[i - 1]).IsBoss)
                    groups.Add(i);
            int tileRows = 0;
            for (int gi = 0; gi < groups.Count; gi++)
                tileRows += (((gi + 1 < groups.Count ? groups[gi + 1] : types.Length) - groups[gi]) + cols - 1) / cols;
            float th = Mathf.Clamp((g.height - (titleH - 6f) - groups.Count * (headH + 2f) - infoH - 8f - tileRows * gap) / tileRows, 30f, 62f);  // tiles shrink to fit above the info bar
            LobbyKit.Heading(g.x, g.y - 6f, g.width, "Enemies in wave " + (practiceSel + 1), sel.Total.ToString(), calm, 16);
            if (canEdit && sel.Total > 0 && LobbyKit.Key(new Rect(g.xMax - 66f, g.y - 2f, 66f, 22f), "CLEAR", false, calm, size: 11, minSize: 9, danger: true))
            {
                sel.Counts.Clear();
                PracticeChanged();
            }
            float y = g.y + titleH - 6f;
            var heads = new List<KeyValuePair<float, string>>();
            for (int gi = 0; gi < groups.Count; gi++)
            {
                int from = groups[gi], to = gi + 1 < groups.Count ? groups[gi + 1] : types.Length;
                heads.Add(new KeyValuePair<float, string>(y, Defs.Get(types[from]).IsBoss ? "BOSSES  ·  they bring their usual adds" : "ENEMIES"));
                y += headH;
                for (int i = from; i < to; i++)
                    tiles[i] = new Rect(g.x + ((i - from) % cols) * (tw + gap), y + ((i - from) / cols) * (th + gap), tw, th);
                y += ((to - from + cols - 1) / cols) * (th + gap) + 2f;
            }
            foreach (var h in heads)
                LobbyKit.Spaced(new Rect(g.x, h.Key, g.width, headH), h.Value, 10, h.Value.StartsWith("BOSSES") ? CrtStyle.Gold : CrtUi.Dim, TextAnchor.MiddleLeft, 1.3f);

            var ev = Event.current;
            EnemyType? hovered = null;
            // Tile backs (and clicks).
            for (int i = 0; i < tiles.Length; i++)
            {
                var type = types[i];
                var tr = tiles[i];
                int n;
                sel.Counts.TryGetValue(type, out n);
                bool boss = Defs.Get(type).IsBoss, hover = tr.Contains(ev.mousePosition);
                if (hover)
                    hovered = type;
                Color accent = n > 0 ? PracticeCyan : boss ? CrtStyle.Gold : CrtUi.Ink;
                Ui.Fill(tr, new Color(accent.r, accent.g, accent.b, n > 0 ? 0.14f : hover ? 0.1f : 0.05f));
                CrtKit.Frame(tr, n > 0 ? 2f : 1f, new Color(accent.r, accent.g, accent.b, n > 0 ? 0.95f : hover ? 0.7f : 0.28f));
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
                    var type = types[i];
                    int n;
                    sel.Counts.TryGetValue(type, out n);
                    bool hover = hovered == type;
                    DrawEnemyPreview(type, new V2(tiles[i].center.x, tiles[i].y + (th - 13f) / 2f + 1f), Mathf.Min(40f, th - 16f), t, n > 0 || hover);
                }
                if (hovered != null)
                    DrawEnemyPreview(hovered.Value, new V2(g.x + 26f, g.yMax - infoH / 2f), 32f, t, true);
                EndUiGl();
            }
            // Names and counts on top.
            for (int i = 0; i < tiles.Length; i++)
            {
                var type = types[i];
                var tr = tiles[i];
                int n;
                sel.Counts.TryGetValue(type, out n);
                bool boss = Defs.Get(type).IsBoss;
                Ui.Label(new Rect(tr.x + 2f, tr.yMax - 14f, tr.width - 4f, 12f), PracticePlan.NameOf(type).Replace("The ", ""), 10,
                    boss ? CrtStyle.Gold : n > 0 ? Color.white : CrtStyle.Body, TextAnchor.MiddleCenter, n > 0);
                if (n > 0)
                {
                    var badge = new Rect(tr.xMax - 30f, tr.y + 3f, 27f, 16f);
                    Ui.Fill(badge, new Color(0f, 0f, 0f, 0.7f));
                    Ui.Label(badge, "×" + n, 11, PracticeCyan, TextAnchor.MiddleCenter, true);
                    if (canEdit)
                    {
                        var minus = new Rect(tr.x + 3f, tr.y + 3f, 16f, 16f);
                        Ui.Fill(minus, new Color(0f, 0f, 0f, minus.Contains(ev.mousePosition) ? 0.85f : 0.55f));
                        Ui.Label(minus, "−", 13, Color.white, TextAnchor.MiddleCenter, true);
                    }
                }
            }
            // What the hovered one does, in a bar under the tiles (its enemy is drawn at the left, above).
            var info = new Rect(g.x, g.yMax - infoH, g.width, infoH);
            Ui.Fill(info, new Color(0f, 0f, 0f, 0.3f));
            CrtKit.Frame(info, 1f, LobbyKit.Ink(0.25f));
            if (hovered != null)
            {
                bool boss = Defs.Get(hovered.Value).IsBoss;
                LobbyKit.Spaced(new Rect(info.x + 54f, info.y + 3f, info.width - 64f, 18f), PracticePlan.NameOf(hovered.Value).ToUpperInvariant(), 13, boss ? CrtStyle.Gold : PracticeCyan, TextAnchor.MiddleLeft, 1.3f);
                Ui.Label(new Rect(info.x + 54f, info.y + 20f, info.width - 64f, 24f), PracticePlan.BlurbOf(hovered.Value), 11, CrtStyle.Body, TextAnchor.UpperLeft, false, true);
            }
            else
                Ui.Label(new Rect(info.x + 14f, info.y, info.width - 28f, infoH), canEdit
                    ? "Click an enemy to add one, right-click (or its −) to take one away. Hover one to see what it does."
                    : "Hover an enemy to see what it does.", 11, CrtStyle.Desc, TextAnchor.MiddleCenter, false, true);
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

        void DrawPracticeFooter(Rect r, bool canEdit, bool calm)
        {
            float x = r.x + 16f, y = r.yMax - 48f;
            Ui.Fill(new Rect(r.x + 14f, y - 8f, r.width - 28f, 1f), LobbyKit.Ink(0.25f));
            LobbyKit.Lab(new Rect(x, y, 54f, 32f), "Setup", 12, calm);
            practiceName = DkField(new Rect(x + 56f, y, 190f, 32f), practiceName, 40);
            float bx = x + 256f;
            if (LobbyKit.Key(new Rect(bx, y, 70f, 32f), "SAVE", false, calm, size: 14, minSize: 10, accent: ReviveMintUi))
                SavePractice();
            if (LobbyKit.Key(new Rect(bx + 76f, y, 70f, 32f), practiceLoadOpen ? "BACK" : "LOAD", practiceLoadOpen, calm, size: 14, minSize: 10, enabled: canEdit))
                practiceLoadOpen = !practiceLoadOpen;
            if (LobbyKit.Key(new Rect(bx + 152f, y, 104f, 32f), "FOLDER", false, calm, size: 14, minSize: 10))
            {
                Directory.CreateDirectory(PracticeFolder);
                Application.OpenURL("file:///" + PracticeFolder.Replace('\\', '/'));
            }
            float sx = bx + 268f;
            Ui.Label(new Rect(sx, y, r.xMax - 16f - sx, 32f), practiceStatus.Length > 0 ? practiceStatus : PracticeSummary(), 11,
                practiceStatus.Length > 0 ? CrtStyle.Desc : PracticeCyan, TextAnchor.MiddleRight, practiceStatus.Length == 0);
        }

        void DrawPracticeFiles(Rect g, bool calm)
        {
            LobbyKit.Heading(g.x, g.y - 6f, g.width, "Saved setups", "click one to load it", calm, 16);
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
                Ui.Label(new Rect(g.x, g.y + 40f, g.width, 40f), "Nothing saved yet. Name your setup and press Save: it goes in the practice folder as a text file you can share or edit.", 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
                return;
            }
            float bw = (g.width - 6f) / 2f;
            for (int i = 0; i < files.Length; i++)
            {
                var b = new Rect(g.x + (i % 2) * (bw + 6f), g.y + 40f + (i / 2) * 38f, bw, 32f);
                if (LobbyKit.Key(b, Path.GetFileNameWithoutExtension(files[i]).ToUpperInvariant(), false, calm, size: 14, minSize: 10))
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

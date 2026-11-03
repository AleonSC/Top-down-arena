using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The wave editor (F1 -> Tools -> Wave editor): design waves on a timeline (Docs/Mockups/wave-editor.html, option B).
    /// Each wave is groups of enemies (or squads) that arrive from spawn points A-E at set times, plus props on the floor.
    /// The small map shows the wave at the playhead; Play runs it. Saves .waves text files (Core/WaveDesign.cs) to the
    /// "waves" folder; Test plays the waves in the Practice Range (you host it).
    public partial class ArenaApp
    {
        bool waveEdOpen;
        WaveSet edSet = WaveSet.Starter();
        int edWave, edGroup, edTrackScroll;
        float edT;                // the playhead, in seconds
        bool edPlaying, edPropsMode, edLoadOpen;
        int edPalTab;             // 0 enemies, 1 squads, 2 bosses
        PropKind edPropKind = PropKind.Crate;
        string edStatus = "";

        enum EdDrag { None, Clip, ClipEnd, Playhead, Point, Prop }
        EdDrag edDrag;
        int edDragIndex;
        float edDragOffset;

        static readonly Color EdColor = new Color(0.3f, 0.78f, 0.62f);
        static readonly Rect EdMapPanel = new Rect(20f, 64f, 500f, 330f);
        static readonly Rect EdMap = new Rect(30f, 104f, 480f, 270f);
        static readonly Rect EdPalette = new Rect(534f, 64f, 352f, 330f);
        static readonly Rect EdInspector = new Rect(900f, 64f, 360f, 330f);
        static readonly Rect EdTimeline = new Rect(20f, 408f, 1240f, 292f);
        const float EdLabelW = 150f, EdRowH = 30f;
        const int EdRows = 6;

        static Color EdDark(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        static string WavesFolder => Path.Combine(Application.persistentDataPath, "waves");

        DesignWave EdW => edSet.Waves[Mathf.Clamp(edWave, 0, edSet.Waves.Count - 1)];
        DesignGroup EdG => edGroup >= 0 && edGroup < EdW.Groups.Count ? EdW.Groups[edGroup] : null;

        void OpenWaveEditor()
        {
            devMenuOpen = false;
            waveEdOpen = true;
            edPlaying = false;
            edLoadOpen = false;
            GUIUtility.keyboardControl = 0;
        }

        /// Update: the playhead runs while playing; Space plays and pauses, Delete removes the selected group.
        void WaveEditorUpdate(float dt)
        {
            if (!waveEdOpen)
                return;
            float len = EdSpan();
            if (edPlaying)
            {
                edT += dt;
                if (edT > len)
                    edT = 0f;
            }
            if (GUIUtility.keyboardControl != 0)
                return;
            if (Input.GetKeyDown(KeyCode.Space))
                edPlaying = !edPlaying;
            if (Input.GetKeyDown(KeyCode.Delete) && EdG != null)
                EdRemoveGroup();
        }

        float EdSpan() => Mathf.Max(20f, Mathf.Ceil((EdW.Length + 4f) / 2f) * 2f);

        void DrawWaveEditor()
        {
            float t = Time.unscaledTime;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.02f, 0.025f, 0.04f, 0.94f));
            if (edSet.Waves.Count == 0)
                edSet.Waves.Add(new DesignWave());
            edWave = Mathf.Clamp(edWave, 0, edSet.Waves.Count - 1);
            EdMouse();

            // Header: the title, the set's name, the wave and its level; then files and testing.
            Ui.Fancy(new Rect(24f, 14f, 220f, 36f), "WAVE EDITOR", 24, Color.white, new Color(0.75f, 1f, 0.9f), TextAnchor.MiddleLeft, EdColor);
            edSet.Name = Ui.Field(new Rect(214f, 18f, 160f, 28f), edSet.Name, 40);
            float x = 386f;
            if (Ui.SmallButton(new Rect(x, 18f, 30f, 28f), "<") && edWave > 0) EdSelectWave(edWave - 1);
            Ui.Label(new Rect(x + 32f, 18f, 92f, 28f), "WAVE " + (edWave + 1) + " / " + edSet.Waves.Count, 12, Color.white, TextAnchor.MiddleCenter, true);
            if (Ui.SmallButton(new Rect(x + 126f, 18f, 30f, 28f), ">") && edWave < edSet.Waves.Count - 1) EdSelectWave(edWave + 1);
            if (Ui.SmallButton(new Rect(x + 162f, 18f, 30f, 28f), "+", EdDark(EdColor, 0.8f)))
            {
                edSet.Waves.Insert(edWave + 1, new DesignWave { Level = EdW.Level + 1 });
                EdSelectWave(edWave + 1);
            }
            if (Ui.SmallButton(new Rect(x + 196f, 18f, 46f, 28f), "Copy"))
            {
                edSet.Waves.Insert(edWave + 1, EdW.Copy());
                EdSelectWave(edWave + 1);
            }
            bool wasEnabled = GUI.enabled;
            GUI.enabled = edSet.Waves.Count > 1;
            if (Ui.SmallButton(new Rect(x + 246f, 18f, 30f, 28f), "X", Ui.Danger))
            {
                edSet.Waves.RemoveAt(edWave);
                EdSelectWave(Mathf.Min(edWave, edSet.Waves.Count - 1));
            }
            GUI.enabled = wasEnabled;
            Ui.Label(new Rect(x + 290f, 18f, 40f, 28f), "LEVEL", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(x + 330f, 18f, 26f, 28f), "−")) EdW.Level = Mathf.Max(1, EdW.Level - 1);
            Ui.Label(new Rect(x + 356f, 18f, 30f, 28f), EdW.Level.ToString(), 15, Ui.Gold, TextAnchor.MiddleCenter, true);
            if (Ui.SmallButton(new Rect(x + 386f, 18f, 26f, 28f), "+")) EdW.Level = Mathf.Min(Game.MaxWave, EdW.Level + 1);
            edSet.Loop = Ui.Toggle(new Rect(x + 424f, 19f, 74f, 26f), edSet.Loop, "Loop");

            float rx = W - 24f;
            rx -= 70f;
            if (Ui.SmallButton(new Rect(rx, 16f, 70f, 32f), "Close"))
                waveEdOpen = false;
            rx -= 92f;
            if (Ui.SmallButton(new Rect(rx, 16f, 86f, 32f), "Test", EdDark(EdColor, 0.85f), 13))
                EdTest();
            rx -= 66f;
            if (Ui.SmallButton(new Rect(rx, 16f, 60f, 32f), edLoadOpen ? "Back" : "Load", edLoadOpen ? Ui.Primary : Ui.Neutral))
                edLoadOpen = !edLoadOpen;
            rx -= 66f;
            if (Ui.SmallButton(new Rect(rx, 16f, 60f, 32f), "Save", Ui.Primary))
                EdSave();

            DrawEdMap(t);
            if (edLoadOpen)
                DrawEdFiles(EdPalette);
            else
                DrawEdPalette(EdPalette, t);
            DrawEdInspector(EdInspector);
            DrawEdTimeline(EdTimeline);
        }

        void EdSelectWave(int i)
        {
            edWave = Mathf.Clamp(i, 0, edSet.Waves.Count - 1);
            edGroup = EdW.Groups.Count > 0 ? 0 : -1;
            edTrackScroll = 0;
            edT = 0f;
        }

        // --- The map ------------------------------------------------------------------------------------------

        static Vector2 EdToMap(V2 p) => new Vector2(EdMap.x + p.X / Game.Width * EdMap.width, EdMap.y + p.Y / Game.Height * EdMap.height);
        static V2 EdFromMap(Vector2 m) => new V2(Mathf.Clamp((m.x - EdMap.x) / EdMap.width, 0f, 1f) * Game.Width, Mathf.Clamp((m.y - EdMap.y) / EdMap.height, 0f, 1f) * Game.Height);

        void DrawEdMap(float t)
        {
            var p = EdMapPanel;
            Ui.Panel(p);
            Ui.Label(new Rect(p.x + 12f, p.y + 8f, 160f, 20f), "ARENA AT " + edT.ToString("0.0") + " S", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.Choice(new Rect(p.xMax - 160f, p.y + 6f, 72f, 24f), "Spawns", !edPropsMode, Ui.Accent)) edPropsMode = false;
            if (Ui.Choice(new Rect(p.xMax - 84f, p.y + 6f, 72f, 24f), "Props", edPropsMode, Ui.Warn)) edPropsMode = true;

            var m = EdMap;
            Ui.Fill(m, new Color(0.06f, 0.075f, 0.11f));
            for (int gx = 1; gx < 16; gx++)
                Ui.Fill(new Rect(m.x + gx * m.width / 16f, m.y, 1f, m.height), new Color(1f, 1f, 1f, 0.035f));
            for (int gy = 1; gy < 9; gy++)
                Ui.Fill(new Rect(m.x, m.y + gy * m.height / 9f, m.width, 1f), new Color(1f, 1f, 1f, 0.035f));
            var c = EdToMap(new V2(Game.Width / 2f, Game.Height / 2f));
            Ui.Pill(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), new Color(1f, 1f, 1f, 0.2f));

            // Props.
            foreach (var pr in EdW.Props)
            {
                var q = EdToMap(pr.Pos);
                float s = pr.Kind == PropKind.Crate || pr.Kind == PropKind.HeavyCrate || pr.Kind == PropKind.Chest ? 20f : 13f;
                var col = pr.Kind == PropKind.Barrel || pr.Kind == PropKind.HealthBarrel ? new Color(0.88f, 0.38f, 0.29f) : pr.Kind == PropKind.Chest ? Ui.Gold : new Color(0.71f, 0.54f, 0.35f);
                if (pr.Kind == PropKind.Pot || pr.Kind == PropKind.Barrel || pr.Kind == PropKind.HealthBarrel)
                    Ui.Pill(new Rect(q.x - s / 2f, q.y - s / 2f, s, s), col);
                else
                    Ui.Fill(new Rect(q.x - s / 2f, q.y - s / 2f, s, s), col);
            }

            // The wave at the playhead: who's arrived, heading in from their points.
            foreach (var d in EdPreview(EdW, edT))
            {
                var q = EdToMap(d.Pos);
                float r = Mathf.Max(5f, d.Radius * 0.42f);
                Ui.Pill(new Rect(q.x - r, q.y - r, r * 2f, r * 2f), d.Color);
            }

            // Spawn points, the selected group's lit up.
            var sel = EdG;
            for (int i = 0; i < WaveSet.Letters.Length; i++)
            {
                char letter = WaveSet.Letters[i];
                var q = EdToMap(edSet.Points[i]);
                bool on = sel != null && sel.From.IndexOf(letter) >= 0;
                if (on)
                    Ui.Pill(new Rect(q.x - 17f, q.y - 17f, 34f, 34f), new Color(0.3f, 0.55f, 1f, 0.25f + 0.1f * Mathf.Sin(t * 4f)));
                Ui.Pill(new Rect(q.x - 11f, q.y - 11f, 22f, 22f), on ? Ui.Primary : new Color(0.16f, 0.2f, 0.3f));
                Ui.Label(new Rect(q.x - 11f, q.y - 11f, 22f, 22f), letter.ToString(), 12, Color.white, TextAnchor.MiddleCenter, true);
            }

            Ui.Label(new Rect(p.x + 12f, p.yMax - 22f, p.width - 24f, 16f), edPropsMode
                ? "Click to place a " + WaveSet.PropName(edPropKind) + "  ·  drag to move  ·  right-click to remove"
                : "Drag a letter to move that spawn point (for every wave)", 10, Ui.Muted);
            if (edPropsMode)
            {
                var kinds = new[] { PropKind.Crate, PropKind.HeavyCrate, PropKind.Pot, PropKind.Barrel, PropKind.Chest };
                for (int i = 0; i < kinds.Length; i++)
                    if (Ui.Choice(new Rect(p.x + 150f + i * 54f, p.y + 6f, 50f, 24f), WaveSet.PropName(kinds[i]).Replace("heavy ", "big "), edPropKind == kinds[i], Ui.Warn, null, null, 10))
                        edPropKind = kinds[i];
            }
        }

        struct EdDot { public V2 Pos; public Color Color; public float Radius; }

        /// Where each enemy of the wave is at time t, roughly: from its point toward the middle at 140 px/s (squads keep
        /// their shape). Only for the editor's map: the real wave uses the game's own AI.
        List<EdDot> EdPreview(DesignWave w, float t)
        {
            var dots = new List<EdDot>();
            var mid = new V2(Game.Width / 2f, Game.Height / 2f);
            foreach (var g in w.Groups)
            {
                var arr = g.Arrivals;
                string from = g.From.Length > 0 ? g.From : "A";
                for (int i = 0; i < arr.Count; i++)
                {
                    float at = g.At + (g.Squad == null && g.Arrive == ArriveMode.OneByOne ? g.Every * i : 0f);
                    float age = t - at;
                    if (age < 0f || age > 9f)
                        continue;
                    V2 start = edSet.Point(from[g.Squad != null ? 0 : i % from.Length]);
                    V2 fwd = (mid - start).Normalized();
                    if (g.Squad != null || g.Arrive == ArriveMode.Line)
                        start += fwd.Orthogonal() * ((i - (arr.Count - 1) / 2f) * 60f);
                    else if (g.Arrive == ArriveMode.Together)
                        start += V2.FromAngle(MathX.Tau * i / Mathf.Max(1, arr.Count)) * 45f;
                    else if (g.Arrive == ArriveMode.Ring)
                        start = mid + V2.FromAngle(MathX.Tau * i / Mathf.Max(1, arr.Count)) * 280f;
                    float dist = Mathf.Min(age * 140f, Mathf.Max(0f, mid.DistanceTo(start) - 90f));
                    var def = Defs.Get(arr[i]);
                    dots.Add(new EdDot { Pos = start + fwd * dist, Color = ToColor(def.Color), Radius = def.Radius });
                }
            }
            return dots;
        }

        // --- The palette --------------------------------------------------------------------------------------

        void DrawEdPalette(Rect r, float t)
        {
            Ui.Panel(r);
            string[] tabs = { "Enemies", "Squads", "Bosses" };
            for (int i = 0; i < tabs.Length; i++)
                if (Ui.Choice(new Rect(r.x + 10f + i * 86f, r.y + 8f, 82f, 24f), tabs[i], edPalTab == i, EdColor))
                    edPalTab = i;
            Ui.Label(new Rect(r.x + 12f, r.yMax - 22f, r.width - 24f, 16f), edPalTab == 1 ? "Click to add the squad as a new group at the playhead"
                : "Click: new group at the playhead  ·  Shift-click: add to selected", 10, Ui.Muted);
            var area = new Rect(r.x + 10f, r.y + 40f, r.width - 20f, r.height - 68f);
            if (edPalTab == 1)
            {
                var squads = WaveSet.AllSquads().ToArray();
                float bw = (area.width - 8f) / 3f;
                for (int i = 0; i < squads.Length && i < 24; i++)
                {
                    var b = new Rect(area.x + (i % 3) * (bw + 4f), area.y + (i / 3) * 31f, bw, 27f);
                    bool faction = Approaches.Of(squads[i].Members[0]) != null && Approaches.All.Any(f => f.Squads.Contains(squads[i]) || f.Finale == squads[i]);
                    if (Ui.SmallButton(b, squads[i].Name, faction ? new Color(0.55f, 0.42f, 0.2f) : Ui.Neutral, 11))
                        EdAddGroup(null, squads[i]);
                }
                return;
            }
            var list = EdPaletteEnemies(edPalTab == 2);
            const int cols = 6;
            float tw = (area.width - (cols - 1) * 4f) / cols, th = 44f;
            var cells = new List<KeyValuePair<EnemyType, Rect>>();
            for (int i = 0; i < list.Count; i++)
            {
                var cell = new Rect(area.x + (i % cols) * (tw + 4f), area.y + (i / cols) * (th + 4f), tw, th);
                if (cell.yMax > area.yMax)
                    break;
                bool faction = Approaches.Of(list[i]) != null;
                Ui.Pill(cell, faction ? new Color(0.55f, 0.42f, 0.2f, 0.28f) : new Color(1f, 1f, 1f, cell.Contains(Event.current.mousePosition) ? 0.1f : 0.04f));
                Ui.Label(new Rect(cell.x, cell.yMax - 14f, cell.width, 12f), PracticePlan.NameOf(list[i]), 8, Ui.Muted, TextAnchor.MiddleCenter);
                cells.Add(new KeyValuePair<EnemyType, Rect>(list[i], cell));
                if (Ui.HitArea(cell))
                {
                    if (Event.current.shift && EdG != null && EdG.Squad == null)
                    {
                        EdG.Members.Add(list[i]);
                        edStatus = "Added a " + Defs.Get(list[i]).Name + " to " + EdG.Name;
                    }
                    else
                        EdAddGroup(list[i], null);
                }
            }
            if (BeginUiGl())
            {
                foreach (var kv in cells)
                {
                    UiClip(kv.Value);
                    DrawEnemyPreview(kv.Key, new V2(kv.Value.center.x, kv.Value.y + 17f), 28f, t, kv.Value.Contains(Event.current.mousePosition));
                }
                EndUiGl();
            }
        }

        /// The palette's enemies: boss factions' first (they're the point of hunts and approaches), then the rest; or the bosses.
        static List<EnemyType> EdPaletteEnemies(bool bosses)
        {
            var list = new List<EnemyType>();
            if (bosses)
            {
                foreach (var t in PracticePlan.Choices)
                    if (Defs.Get(t).IsBoss)
                        list.Add(t);
                return list;
            }
            foreach (var f in Approaches.All)
                foreach (var m in f.Members)
                    if (!list.Contains(m.Type))
                        list.Add(m.Type);
            foreach (var t in PracticePlan.Choices)
                if (!Defs.Get(t).IsBoss && !list.Contains(t))
                    list.Add(t);
            return list;
        }

        void EdAddGroup(EnemyType? type, SquadDef squad)
        {
            var g = new DesignGroup { At = Mathf.Round(edT * 10f) / 10f, From = "A" };
            if (squad != null)
            {
                g.Squad = squad.Name;
                g.Name = squad.Name;
            }
            else if (type.HasValue)
            {
                bool boss = Defs.Get(type.Value).IsBoss;
                g.Name = Defs.Get(type.Value).Name + (boss ? "" : "s");
                for (int i = 0; i < (boss ? 1 : 4); i++)
                    g.Members.Add(type.Value);
            }
            EdW.Groups.Add(g);
            edGroup = EdW.Groups.Count - 1;
            edTrackScroll = Mathf.Max(0, edGroup - EdRows + 1);
            edStatus = "Added " + g.Name + " at " + g.At.ToString("0.0") + " s";
        }

        void EdRemoveGroup()
        {
            EdW.Groups.RemoveAt(edGroup);
            edGroup = Mathf.Min(edGroup, EdW.Groups.Count - 1);
        }

        // --- The inspector ------------------------------------------------------------------------------------

        void DrawEdInspector(Rect r)
        {
            Ui.Panel(r);
            var g = EdG;
            float x = r.x + 14f, w = r.width - 28f, y = r.y + 10f;
            if (g == null)
            {
                Ui.Label(new Rect(x, y, w, 60f), "No group selected. Click an enemy or squad on the left to add one at the playhead, or pick a track below.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
                return;
            }
            Ui.Label(new Rect(x, y, w, 14f), "SELECTED GROUP", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(r.xMax - 118f, y - 3f, 50f, 22f), "Copy", 10))
            {
                var copy = g.Copy();
                copy.At = g.At + g.Span + 1f;
                EdW.Groups.Insert(edGroup + 1, copy);
                edGroup++;
            }
            if (Ui.SmallButton(new Rect(r.xMax - 64f, y - 3f, 50f, 22f), "Delete", Ui.Danger, 10))
            {
                EdRemoveGroup();
                return;
            }
            y += 20f;
            g.Name = Ui.Field(new Rect(x, y, w, 28f), g.Name, 30);
            y += 36f;

            Ui.Label(new Rect(x, y, 60f, 24f), "FROM", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < WaveSet.Letters.Length; i++)
            {
                char letter = WaveSet.Letters[i];
                bool on = g.From.IndexOf(letter) >= 0;
                if (Ui.Choice(new Rect(x + 56f + i * 34f, y, 30f, 24f), letter.ToString(), on, Ui.Accent) && !(on && g.From.Length == 1))
                    g.From = on ? g.From.Replace(letter.ToString(), "") : new string((g.From + letter).OrderBy(ch => ch).ToArray());
            }
            y += 32f;

            Ui.Label(new Rect(x, y, 60f, 24f), "STARTS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(x + 56f, y, 26f, 24f), "−")) g.At = Mathf.Max(0f, g.At - 0.5f);
            Ui.Label(new Rect(x + 84f, y, 60f, 24f), g.At.ToString("0.0") + " s", 13, Color.white, TextAnchor.MiddleCenter, true);
            if (Ui.SmallButton(new Rect(x + 146f, y, 26f, 24f), "+")) g.At += 0.5f;
            if (g.Squad == null && g.Arrive == ArriveMode.OneByOne)
            {
                Ui.Label(new Rect(x + 184f, y, 44f, 24f), "EVERY", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
                if (Ui.SmallButton(new Rect(x + 226f, y, 22f, 24f), "−")) g.Every = Mathf.Max(0f, Mathf.Round((g.Every - 0.1f) * 10f) / 10f);
                Ui.Label(new Rect(x + 248f, y, 36f, 24f), g.Every.ToString("0.0"), 13, Color.white, TextAnchor.MiddleCenter, true);
                if (Ui.SmallButton(new Rect(x + 284f, y, 22f, 24f), "+")) g.Every = Mathf.Round((g.Every + 0.1f) * 10f) / 10f;
            }
            y += 32f;

            if (g.Squad != null)
            {
                Ui.Label(new Rect(x, y, w, 18f), "SQUAD  ·  " + g.Squad.ToUpperInvariant(), 10, Ui.Gold, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(x, y + 20f, w, 60f), WaveSet.MembersText(g.Arrivals) + ". It arrives in its own formation, from the first point picked.", 12, Ui.Text, TextAnchor.UpperLeft, false, true);
                return;
            }
            string[] modes = { "One by one", "Together", "Line", "Ring" };
            for (int i = 0; i < modes.Length; i++)
                if (Ui.Choice(new Rect(x + i * 82f, y, 78f, 24f), modes[i], g.Arrive == (ArriveMode)i, EdColor, null, null, 11))
                    g.Arrive = (ArriveMode)i;
            y += 32f;

            // Members: each kind with its count.
            var kinds = g.Members.Distinct().ToList();
            Ui.Label(new Rect(x, y, w, 14f), "ENEMIES  ·  " + g.Members.Count, 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 18f;
            for (int i = 0; i < kinds.Count && y < r.yMax - 26f; i++)
            {
                var k = kinds[i];
                int n = g.Members.Count(m => m == k);
                var row = new Rect(x, y, w, 24f);
                Ui.Pill(row, new Color(1f, 1f, 1f, 0.04f));
                Ui.Pill(new Rect(row.x + 6f, row.y + 7f, 10f, 10f), ToColor(Defs.Get(k).Color));
                Ui.Label(new Rect(row.x + 22f, row.y, 160f, 24f), Defs.Get(k).Name, 12, Ui.Text);
                if (Ui.SmallButton(new Rect(row.xMax - 90f, row.y + 2f, 24f, 20f), "−"))
                    g.Members.RemoveAt(g.Members.LastIndexOf(k));
                Ui.Label(new Rect(row.xMax - 64f, row.y, 36f, 24f), "×" + n, 12, Color.white, TextAnchor.MiddleCenter, true);
                if (Ui.SmallButton(new Rect(row.xMax - 28f, row.y + 2f, 24f, 20f), "+"))
                    g.Members.Insert(g.Members.LastIndexOf(k) + 1, k);
                y += 28f;
            }
            if (g.Members.Count == 0)
                Ui.Label(new Rect(x, y, w, 30f), "Empty: shift-click enemies on the left to add them.", 11, Ui.Warn, TextAnchor.UpperLeft, false, true);
        }

        // --- The timeline -------------------------------------------------------------------------------------

        float EdTrackX0 => EdTimeline.x + EdLabelW;
        float EdTrackX1 => EdTimeline.xMax - 12f;
        float EdTimeToX(float s) => EdTrackX0 + s / EdSpan() * (EdTrackX1 - EdTrackX0);
        float EdXToTime(float x) => Mathf.Clamp((x - EdTrackX0) / (EdTrackX1 - EdTrackX0), 0f, 1f) * EdSpan();
        float EdRowY(int i) => EdTimeline.y + 50f + (i - edTrackScroll) * EdRowH;

        Rect EdClip(DesignGroup g, int i)
        {
            float x0 = EdTimeToX(g.At), x1 = EdTimeToX(g.At + g.Span + 0.9f);
            return new Rect(x0, EdRowY(i) + 3f, Mathf.Max(18f, x1 - x0), EdRowH - 8f);
        }

        void DrawEdTimeline(Rect r)
        {
            Ui.Panel(r);
            var w = EdW;
            float span = EdSpan();
            Ui.Label(new Rect(r.x + 12f, r.y + 8f, 400f, 16f), "WAVE " + (edWave + 1) + "  ·  " + w.Groups.Count + " GROUPS  ·  " + w.EnemyCount + " ENEMIES  ·  " + w.Props.Count + " PROPS",
                10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(r.xMax - 210f, r.y + 6f, 70f, 22f), edPlaying ? "Pause" : "Play", EdDark(EdColor, 0.8f), 11))
                edPlaying = !edPlaying;
            if (Ui.SmallButton(new Rect(r.xMax - 136f, r.y + 6f, 60f, 22f), "Back to 0", 11))
                edT = 0f;
            Ui.Label(new Rect(r.xMax - 72f, r.y + 6f, 60f, 22f), edT.ToString("0.0") + " s", 12, Color.white, TextAnchor.MiddleRight, true);

            // Seconds along the top.
            int rows = Mathf.Min(EdRows, Mathf.Max(1, w.Groups.Count - edTrackScroll));
            float trackTop = r.y + 50f, trackBottom = trackTop + EdRows * EdRowH;
            for (int s = 0; s <= span; s += 2)
            {
                float tx = EdTimeToX(s);
                Ui.Fill(new Rect(tx, trackTop - 4f, 1f, trackBottom - trackTop), new Color(1f, 1f, 1f, 0.05f));
                Ui.Label(new Rect(tx - 20f, trackTop - 20f, 40f, 14f), s + "s", 10, Ui.Muted, TextAnchor.MiddleCenter);
            }

            // One track a group.
            for (int i = edTrackScroll; i < w.Groups.Count && i < edTrackScroll + EdRows; i++)
            {
                var g = w.Groups[i];
                float y = EdRowY(i);
                bool sel = i == edGroup;
                var label = new Rect(r.x + 8f, y, EdLabelW - 14f, EdRowH - 4f);
                Ui.Pill(label, sel ? new Color(0.3f, 0.55f, 1f, 0.22f) : new Color(1f, 1f, 1f, 0.03f));
                var col = g.Arrivals.Count > 0 ? ToColor(Defs.Get(g.Arrivals[0]).Color) : Ui.Muted;
                Ui.Pill(new Rect(label.x + 6f, label.y + 8f, 10f, 10f), col);
                Ui.Label(new Rect(label.x + 22f, label.y, label.width - 26f, label.height), g.Name, 11, sel ? Color.white : Ui.Text, TextAnchor.MiddleLeft, sel);
                if (Ui.HitArea(label))
                    edGroup = i;
                Ui.Fill(new Rect(EdTrackX0, y, EdTrackX1 - EdTrackX0, EdRowH - 4f), new Color(1f, 1f, 1f, 0.025f));
                var clip = EdClip(g, i);
                Ui.Pill(clip, sel ? Color.Lerp(col, Color.white, 0.25f) : EdDark(col, 0.85f));
                if (sel)
                    Ui.Fill(new Rect(clip.xMax - 4f, clip.y + 3f, 2f, clip.height - 6f), new Color(0f, 0f, 0f, 0.5f));
                string text = (g.Squad != null ? g.Squad : WaveSet.MembersText(g.Members)) + "  ·  from " + g.From;
                if (Ui.TextWidth(text, 10, true) < clip.width - 10f)
                    Ui.Label(new Rect(clip.x + 6f, clip.y, clip.width - 8f, clip.height), text, 10, new Color(0.04f, 0.05f, 0.08f), TextAnchor.MiddleLeft, true);
                else  // too short to hold it: beside the clip, in light text
                    Ui.Label(new Rect(clip.xMax + 6f, clip.y, 320f, clip.height), text, 10, sel ? Color.white : Ui.Text, TextAnchor.MiddleLeft, true);
            }
            if (w.Groups.Count == 0)
                Ui.Label(new Rect(EdTrackX0, trackTop, 600f, 40f), "No groups yet: click an enemy or squad above to add one here.", 12, Ui.Muted);
            if (w.Groups.Count > EdRows)
            {
                if (Ui.SmallButton(new Rect(r.x + 8f, trackBottom + 2f, 66f, 22f), "Up", 10) && edTrackScroll > 0) edTrackScroll--;
                if (Ui.SmallButton(new Rect(r.x + 78f, trackBottom + 2f, 66f, 22f), "Down", 10) && edTrackScroll < w.Groups.Count - EdRows) edTrackScroll++;
            }

            // The playhead.
            float px = EdTimeToX(Mathf.Min(edT, span));
            Ui.Fill(new Rect(px - 1f, trackTop - 8f, 2f, trackBottom - trackTop + 8f), Color.white);
            Ui.Pill(new Rect(px - 6f, trackTop - 14f, 12f, 10f), Color.white);

            Ui.Label(new Rect(r.x + 12f, r.yMax - 26f, r.width - 24f, 18f), edStatus.Length > 0 ? edStatus
                : "Drag a clip to change when it starts, its right edge to spread its enemies out  ·  Space plays  ·  Delete removes the group  ·  the wave ends when all are down",
                10, Ui.Muted);
        }

        // --- Mouse: dragging clips, the playhead, spawn points and props ---------------------------------------

        void EdMouse()
        {
            var e = Event.current;
            var mp = e.mousePosition;
            var w = EdW;
            if (e.type == EventType.MouseDown && e.button == 0)
            {
                // A clip (its right edge: the spacing), then the track area (the playhead).
                for (int i = edTrackScroll; i < w.Groups.Count && i < edTrackScroll + EdRows; i++)
                {
                    var clip = EdClip(w.Groups[i], i);
                    if (!clip.Contains(mp))
                        continue;
                    edGroup = i;
                    var g = w.Groups[i];
                    bool spreadable = g.Squad == null && g.Arrive == ArriveMode.OneByOne && g.Members.Count > 1;
                    edDrag = spreadable && mp.x > clip.xMax - 8f ? EdDrag.ClipEnd : EdDrag.Clip;
                    edDragIndex = i;
                    edDragOffset = EdXToTime(mp.x) - g.At;
                    e.Use();
                    return;
                }
                var tracks = new Rect(EdTrackX0, EdTimeline.y + 30f, EdTrackX1 - EdTrackX0, EdRows * EdRowH + 24f);
                if (tracks.Contains(mp))
                {
                    edDrag = EdDrag.Playhead;
                    edT = EdXToTime(mp.x);
                    e.Use();
                    return;
                }
                if (EdMap.Contains(mp) && !edLoadOpen)
                {
                    if (!edPropsMode)
                    {
                        for (int i = 0; i < edSet.Points.Length; i++)
                            if (Vector2.Distance(EdToMap(edSet.Points[i]), mp) < 14f)
                            {
                                edDrag = EdDrag.Point;
                                edDragIndex = i;
                                e.Use();
                                return;
                            }
                        return;
                    }
                    int hit = EdPropAt(mp);
                    if (hit < 0)
                    {
                        w.Props.Add(new DesignProp { Kind = edPropKind, Pos = EdFromMap(mp) });
                        hit = w.Props.Count - 1;
                    }
                    edDrag = EdDrag.Prop;
                    edDragIndex = hit;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseDown && e.button == 1 && edPropsMode && EdMap.Contains(mp))
            {
                int hit = EdPropAt(mp);
                if (hit >= 0)
                    w.Props.RemoveAt(hit);
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && edDrag != EdDrag.None)
            {
                float tm = EdXToTime(mp.x);
                switch (edDrag)
                {
                    case EdDrag.Clip:
                        if (edDragIndex < w.Groups.Count)
                            w.Groups[edDragIndex].At = Mathf.Max(0f, Mathf.Round((tm - edDragOffset) * 10f) / 10f);
                        break;
                    case EdDrag.ClipEnd:
                        if (edDragIndex < w.Groups.Count)
                        {
                            var g = w.Groups[edDragIndex];
                            g.Every = Mathf.Max(0f, Mathf.Round((tm - g.At - 0.9f) / Mathf.Max(1, g.Members.Count - 1) * 20f) / 20f);
                        }
                        break;
                    case EdDrag.Playhead:
                        edT = tm;
                        break;
                    case EdDrag.Point:
                        edSet.Points[edDragIndex] = EdFromMap(mp);
                        break;
                    case EdDrag.Prop:
                        if (edDragIndex < w.Props.Count)
                            w.Props[edDragIndex].Pos = EdFromMap(mp);
                        break;
                }
                e.Use();
            }
            else if (e.type == EventType.MouseUp && edDrag != EdDrag.None)
            {
                edDrag = EdDrag.None;
                e.Use();
            }
        }

        int EdPropAt(Vector2 mp)
        {
            var props = EdW.Props;
            for (int i = props.Count - 1; i >= 0; i--)
                if (Vector2.Distance(EdToMap(props[i].Pos), mp) < 12f)
                    return i;
            return -1;
        }

        // --- Files and testing --------------------------------------------------------------------------------

        void EdSave()
        {
            string clean = new string((edSet.Name ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim();
            if (clean.Length == 0)
                clean = "untitled";
            edSet.Name = clean;
            try
            {
                Directory.CreateDirectory(WavesFolder);
                string path = Path.Combine(WavesFolder, clean + ".waves");
                bool existed = File.Exists(path);
                File.WriteAllText(path, edSet.ToText());
                edStatus = (existed ? "Updated " : "Saved ") + clean + ".waves in the waves folder";
            }
            catch (Exception ex)
            {
                edStatus = "Couldn't save: " + ex.Message;
            }
        }

        void DrawEdFiles(Rect r)
        {
            Ui.Panel(r);
            Ui.Label(new Rect(r.x + 12f, r.y + 8f, r.width - 24f, 16f), "SAVED WAVES  ·  click one to open it", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(r.xMax - 104f, r.y + 6f, 92f, 22f), "Open folder", 10))
            {
                Directory.CreateDirectory(WavesFolder);
                Application.OpenURL("file:///" + WavesFolder.Replace('\\', '/'));
            }
            string[] files;
            try
            {
                files = Directory.Exists(WavesFolder) ? Directory.GetFiles(WavesFolder, "*.waves").OrderByDescending(File.GetLastWriteTime).Take(16).ToArray() : new string[0];
            }
            catch (Exception)
            {
                files = new string[0];
            }
            if (Ui.SmallButton(new Rect(r.x + 12f, r.y + 36f, r.width - 24f, 28f), "New: start from a blank set", EdDark(EdColor, 0.7f)))
            {
                edSet = WaveSet.Starter();
                EdSelectWave(0);
                edLoadOpen = false;
                edStatus = "New wave set";
            }
            if (files.Length == 0)
                Ui.Label(new Rect(r.x + 12f, r.y + 72f, r.width - 24f, 40f), "Nothing saved yet. Name your set at the top and press Save.", 12, Ui.Muted, TextAnchor.UpperLeft, false, true);
            float bw = (r.width - 30f) / 2f;
            for (int i = 0; i < files.Length; i++)
            {
                var b = new Rect(r.x + 12f + (i % 2) * (bw + 6f), r.y + 72f + (i / 2) * 32f, bw, 28f);
                if (Ui.SmallButton(b, Path.GetFileNameWithoutExtension(files[i]), 12))
                    EdLoad(files[i]);
            }
        }

        void EdLoad(string path)
        {
            try
            {
                List<string> problems;
                var set = WaveSet.Parse(File.ReadAllText(path), out problems);
                if (set.Waves.Count == 0)
                {
                    edStatus = "No waves in " + Path.GetFileName(path);
                    return;
                }
                edSet = set;
                edSet.Name = Path.GetFileNameWithoutExtension(path);
                EdSelectWave(0);
                edLoadOpen = false;
                edStatus = "Opened " + Path.GetFileName(path) + (problems.Count > 0 ? "  (" + string.Join("; ", problems.Take(2).ToArray()) + ")" : "");
            }
            catch (Exception ex)
            {
                edStatus = "Couldn't open: " + ex.Message;
            }
        }

        /// Test: play these waves in the Practice Range, hosted by you (from the main menu, the lobby or a match you host).
        void EdTest()
        {
            if (edSet.Waves.Sum(wv => wv.EnemyCount) == 0)
            {
                edStatus = "Nothing to test: add a group first";
                return;
            }
            if (session.Screen == AppScreen.Menu)
                HostGame();
            if (!session.IsHost)
            {
                edStatus = session.Screen == AppScreen.Menu ? "Couldn't host: " + session.Status : "Only the host can test waves";
                return;
            }
            var set = edSet.Copy();
            session.Plan = set.ToPlan();
            session.DesignTest = set;
            session.SetMode(true, false);
            int auto = session.AutoStart;
            session.AutoStart = 1;  // start now, without waiting for Ready
            session.StartMatch();
            session.AutoStart = auto;
            game.PracticeIndex = Mathf.Max(-1, edWave - 1);  // start on the wave being edited
            waveEdOpen = false;
            edStatus = "";
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Arena
{
    /// The wave editor (F1 -> Tools -> Wave editor): design waves on a timeline (Docs/Mockups/wave-editor.html, option B), in the
    /// CRT look of the other tools (DevKit.cs). Each wave is groups of enemies (or squads) that arrive from spawn points A-E at set
    /// times, plus props on the floor. The small map shows the wave at the playhead; Play runs it. Saves .waves text files
    /// (Core/WaveDesign.cs) to the "waves" folder; Test plays the waves in the Practice Range (you host it).
    ///
    /// Made to be picked up without a manual: enemies and squads are dragged from the palette onto the timeline (when it starts), onto
    /// the map (which spawn point it comes from) or onto a clip (into that group); a click on the map's spawn points sends the
    /// selected group from them; every key says what it does, the strip under the timeline always says what you can do next, and
    /// Ctrl+Z / Ctrl+Y undo and redo every change.
    public partial class ArenaApp
    {
        WaveSet edSet = WaveSet.Starter();
        int edWave, edGroup, edTrackScroll, edWaveScroll, edPalScroll;
        float edT;                // the playhead, in seconds
        bool edPlaying, edPropsMode, edLoadOpen;
        int edPalTab;             // 0 enemies, 1 squads, 2 bosses
        PropKind edPropKind = PropKind.Crate;
        string edStatus = "";
        string edStatusSeen = "";
        float edStatusAt;

        enum EdDrag { None, Clip, ClipEnd, Playhead, Point, Prop, Palette }
        EdDrag edDrag;
        int edDragIndex;
        float edDragOffset;
        Vector2 edDragStart;
        bool edDragMoved;
        EnemyType? edDragEnemy;   // what a palette drag carries
        SquadDef edDragSquad;

        // Undo / redo: the set as text before each change (taken when the mouse is up and no text field has focus).
        readonly List<string> edUndo = new List<string>(), edRedo = new List<string>();
        string edLast;

        static readonly Color EdColor = new Color(0.3f, 0.78f, 0.62f);
        static readonly Rect EdMapPanel = new Rect(20f, 94f, 500f, 300f);
        static readonly Rect EdMap = new Rect(60f, 132f, 420f, 236f);
        static readonly Rect EdPalette = new Rect(534f, 94f, 352f, 300f);
        static readonly Rect EdInspector = new Rect(900f, 94f, 360f, 300f);
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
            EdHistoryReset();
        }

        /// Update: the playhead runs while playing; Space plays and pauses, Delete removes the selected group, Ctrl+D copies it,
        /// Ctrl+Z / Ctrl+Y (or Ctrl+Shift+Z) undo and redo.
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
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (ctrl && Input.GetKeyDown(KeyCode.Z))
            {
                if (shift) EdRedoStep(); else EdUndoStep();
            }
            else if (ctrl && Input.GetKeyDown(KeyCode.Y))
                EdRedoStep();
            else if (ctrl && Input.GetKeyDown(KeyCode.D))
                EdCopyGroup();
            else if (Input.GetKeyDown(KeyCode.Space))
                edPlaying = !edPlaying;
            else if (Input.GetKeyDown(KeyCode.Delete) && EdG != null)
                EdRemoveGroup();
        }

        float EdSpan() => Mathf.Max(20f, Mathf.Ceil((EdW.Length + 4f) / 2f) * 2f);

        // --- Undo / redo ----------------------------------------------------------------------------------------

        void EdHistoryReset()
        {
            edUndo.Clear();
            edRedo.Clear();
            edLast = edSet.ToText();
        }

        /// Each frame: when the mouse is up and no text field is being typed in, a changed set pushes the one before it on the undo list.
        void EdHistoryTick()
        {
            if (Event.current.type != EventType.Repaint || Input.GetMouseButton(0) || edDrag != EdDrag.None || GUIUtility.keyboardControl != 0)
                return;
            string cur = edSet.ToText();
            if (edLast == null)
            {
                edLast = cur;
                return;
            }
            if (cur == edLast)
                return;
            edUndo.Add(edLast);
            if (edUndo.Count > 60)
                edUndo.RemoveAt(0);
            edRedo.Clear();
            edLast = cur;
        }

        void EdRestore(string text)
        {
            string name = edSet.Name;
            List<string> problems;
            var set = WaveSet.Parse(text, out problems);
            if (set.Waves.Count == 0)
                set.Waves.Add(new DesignWave());
            set.Name = name;
            edSet = set;
            edWave = Mathf.Clamp(edWave, 0, edSet.Waves.Count - 1);
            edGroup = EdW.Groups.Count == 0 ? -1 : Mathf.Clamp(edGroup, 0, EdW.Groups.Count - 1);
            edLast = text;
        }

        void EdUndoStep()
        {
            if (edUndo.Count == 0)
                return;
            edRedo.Add(edSet.ToText());
            string s = edUndo[edUndo.Count - 1];
            edUndo.RemoveAt(edUndo.Count - 1);
            EdRestore(s);
            edStatus = "Undone";
        }

        void EdRedoStep()
        {
            if (edRedo.Count == 0)
                return;
            edUndo.Add(edSet.ToText());
            string s = edRedo[edRedo.Count - 1];
            edRedo.RemoveAt(edRedo.Count - 1);
            EdRestore(s);
            edStatus = "Redone";
        }

        // --- The screen ---------------------------------------------------------------------------------------

        void DrawWaveEditor()
        {
            float t = Time.unscaledTime;
            Ui.Fill(new Rect(0, 0, W, H), new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.96f));
            if (edSet.Waves.Count == 0)
                edSet.Waves.Add(new DesignWave());
            edWave = Mathf.Clamp(edWave, 0, edSet.Waves.Count - 1);
            if (edStatus != edStatusSeen)
            {
                edStatusSeen = edStatus;
                edStatusAt = t;
            }
            EdMouse();

            DrawEdHeader();
            DrawEdWaveStrip();
            DrawEdMap(t);
            if (edLoadOpen)
                DrawEdFiles(EdPalette);
            else
                DrawEdPalette(EdPalette, t);
            DrawEdInspector(EdInspector);
            DrawEdTimeline(EdTimeline);
            DrawEdDragGhost();
            EdHistoryTick();
        }

        /// Row 1: the title, the set's name, loop, undo / redo and the file keys, each with its word.
        void DrawEdHeader()
        {
            DkTitle(new Rect(24f, 10f, 200f, 36f), "WAVE EDITOR", 24, EdColor);
            Ui.Label(new Rect(230f, 8f, 120f, 12f), "SET NAME", 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            edSet.Name = DkField(new Rect(230f, 20f, 190f, 26f), edSet.Name, 40);
            edSet.Loop = DkSwitch(new Rect(436f, 20f, 190f, 26f), edSet.Loop, "Loop the waves");

            float rx = W - 24f;
            rx -= 80f;
            if (DkKey(new Rect(rx, 14f, 80f, 32f), "CLOSE", Ui.Neutral, false, 13))
                waveEdOpen = false;
            rx -= 164f;
            if (DkKey(new Rect(rx, 14f, 158f, 32f), "TEST IN PRACTICE", Ui.GoldButton, false, 13))
                EdTest();
            rx -= 92f;
            if (DkKey(new Rect(rx, 14f, 86f, 32f), edLoadOpen ? "BACK" : "OPEN", Ui.Neutral, edLoadOpen, 13))
                edLoadOpen = !edLoadOpen;
            rx -= 92f;
            if (DkKey(new Rect(rx, 14f, 86f, 32f), "SAVE", Ui.Neutral, false, 13))
                EdSave();
            bool was = GUI.enabled;
            rx -= 82f;
            GUI.enabled = was && edRedo.Count > 0;
            if (DkKey(new Rect(rx, 14f, 76f, 32f), "REDO", Ui.Neutral, false, 13))
                EdRedoStep();
            rx -= 82f;
            GUI.enabled = was && edUndo.Count > 0;
            if (DkKey(new Rect(rx, 14f, 76f, 32f), "UNDO", Ui.Neutral, false, 13))
                EdUndoStep();
            GUI.enabled = was;
            Ui.Fill(new Rect(20f, 52f, W - 40f, 1f), new Color(CrtUi.Ink.r, CrtUi.Ink.g, CrtUi.Ink.b, 0.18f));
        }

        /// Row 2: one numbered key per wave (click to open it), then new / copy / delete / move and the wave's level.
        void DrawEdWaveStrip()
        {
            const float y = 58f, h = 28f;
            Ui.Label(new Rect(24f, y, 56f, h), "WAVES", 11, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            int n = edSet.Waves.Count;
            const int visible = 16;
            edWaveScroll = Mathf.Clamp(edWaveScroll, 0, Mathf.Max(0, n - visible));
            if (edWave < edWaveScroll)
                edWaveScroll = edWave;
            if (edWave >= edWaveScroll + visible)
                edWaveScroll = edWave - visible + 1;
            float x = 80f;
            bool more = n > visible;
            if (more && DkKey(new Rect(x, y, 24f, h), "<", Ui.Neutral, false, 13) && edWaveScroll > 0)
                edWaveScroll--;
            if (more)
                x += 28f;
            for (int i = edWaveScroll; i < n && i < edWaveScroll + visible; i++)
            {
                if (DkKey(new Rect(x, y, 32f, h), (i + 1).ToString(), EdColor, i == edWave, 13))
                    EdSelectWave(i);
                x += 36f;
            }
            if (more && DkKey(new Rect(x, y, 24f, h), ">", Ui.Neutral, false, 13) && edWaveScroll < n - visible)
                edWaveScroll++;

            float bx = 700f;
            if (DkKey(new Rect(bx, y, 100f, h), "+ NEW WAVE", EdColor, false, 12))
            {
                edSet.Waves.Insert(edWave + 1, new DesignWave { Level = EdW.Level + 1 });
                EdSelectWave(edWave + 1);
                edStatus = "Added wave " + (edWave + 1) + ": drag an enemy from the palette onto the timeline";
            }
            if (DkKey(new Rect(bx + 104f, y, 96f, h), "DUPLICATE", Ui.Neutral, false, 12))
            {
                edSet.Waves.Insert(edWave + 1, EdW.Copy());
                EdSelectWave(edWave + 1);
                edStatus = "Copied the wave to " + (edWave + 1);
            }
            bool was = GUI.enabled;
            GUI.enabled = was && edSet.Waves.Count > 1;
            if (DkKey(new Rect(bx + 204f, y, 80f, h), "DELETE", Ui.Danger, false, 12))
            {
                edSet.Waves.RemoveAt(edWave);
                EdSelectWave(Mathf.Min(edWave, edSet.Waves.Count - 1));
                edStatus = "Deleted the wave (Ctrl+Z brings it back)";
            }
            GUI.enabled = was && edWave > 0;
            if (DkKey(new Rect(bx + 288f, y, 30f, h), "<", Ui.Neutral, false, 12))
            {
                var w = EdW;
                edSet.Waves.RemoveAt(edWave);
                edSet.Waves.Insert(edWave - 1, w);
                edWave--;
            }
            GUI.enabled = was && edWave < edSet.Waves.Count - 1;
            if (DkKey(new Rect(bx + 322f, y, 30f, h), ">", Ui.Neutral, false, 12))
            {
                var w = EdW;
                edSet.Waves.RemoveAt(edWave);
                edSet.Waves.Insert(edWave + 1, w);
                edWave++;
            }
            GUI.enabled = was;
            float lx = bx + 366f;
            Ui.Label(new Rect(lx, y, 44f, h), "LEVEL", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (DkKey(new Rect(lx + 44f, y, 26f, h), "−", Ui.Neutral, false, 13)) EdW.Level = Mathf.Max(1, EdW.Level - 1);
            Ui.Label(new Rect(lx + 72f, y, 34f, h), EdW.Level.ToString(), 15, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            if (DkKey(new Rect(lx + 108f, y, 26f, h), "+", Ui.Neutral, false, 13)) EdW.Level = Mathf.Min(Game.MaxWave, EdW.Level + 1);
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

        /// The spawn point nearest a map position.
        int EdNearestPoint(Vector2 mp)
        {
            int best = 0;
            float bd = float.MaxValue;
            for (int i = 0; i < edSet.Points.Length; i++)
            {
                float d = Vector2.Distance(EdToMap(edSet.Points[i]), mp);
                if (d < bd)
                {
                    bd = d;
                    best = i;
                }
            }
            return best;
        }

        void DrawEdPanelTitle(Rect p, string title)
        {
            Ui.Label(new Rect(p.x + 14f, p.y + 8f, 220f, 20f), title, 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
        }

        void DrawEdMap(float t)
        {
            var p = EdMapPanel;
            DkPanel(p);
            DrawEdPanelTitle(p, "ARENA AT " + edT.ToString("0.0") + " S");
            if (DkChoice(new Rect(p.xMax - 160f, p.y + 6f, 72f, 24f), "SPAWNS", !edPropsMode, EdColor)) edPropsMode = false;
            if (DkChoice(new Rect(p.xMax - 84f, p.y + 6f, 72f, 24f), "PROPS", edPropsMode, CrtStyle.Gold)) edPropsMode = true;

            var m = EdMap;
            var ink = CrtUi.Ink;
            Ui.Fill(m, new Color(0f, 0f, 0f, 0.35f));
            for (int gx = 1; gx < 16; gx++)
                Ui.Fill(new Rect(m.x + gx * m.width / 16f, m.y, 1f, m.height), new Color(ink.r, ink.g, ink.b, 0.06f));
            for (int gy = 1; gy < 9; gy++)
                Ui.Fill(new Rect(m.x, m.y + gy * m.height / 9f, m.width, 1f), new Color(ink.r, ink.g, ink.b, 0.06f));
            CrtKit.Frame(m, 1f, new Color(ink.r, ink.g, ink.b, 0.3f));
            var c = EdToMap(new V2(Game.Width / 2f, Game.Height / 2f));
            Ui.Dot(new Rect(c.x - 4f, c.y - 4f, 8f, 8f), new Color(1f, 1f, 1f, 0.2f));

            // Props.
            foreach (var pr in EdW.Props)
            {
                var q = EdToMap(pr.Pos);
                float s = pr.Kind == PropKind.Crate || pr.Kind == PropKind.HeavyCrate || pr.Kind == PropKind.Chest ? 18f : 12f;
                var col = pr.Kind == PropKind.Barrel || pr.Kind == PropKind.HealthBarrel ? new Color(0.88f, 0.38f, 0.29f) : pr.Kind == PropKind.Chest ? CrtStyle.Gold : new Color(0.71f, 0.54f, 0.35f);
                var pr2 = new Rect(q.x - s / 2f, q.y - s / 2f, s, s);
                if (pr.Kind == PropKind.Pot || pr.Kind == PropKind.Barrel || pr.Kind == PropKind.HealthBarrel)
                    Ui.Dot(pr2, col);
                else
                    Ui.Fill(pr2, col);
            }

            // The wave at the playhead: who's arrived, heading in from their points.
            foreach (var d in EdPreview(EdW, edT))
            {
                var q = EdToMap(d.Pos);
                float r = Mathf.Max(4f, d.Radius * 0.36f);
                Ui.Dot(new Rect(q.x - r, q.y - r, r * 2f, r * 2f), d.Color);
            }

            // Spawn points, the selected group's lit up (and the one a palette drag would land on).
            var sel = EdG;
            int dropPoint = edDrag == EdDrag.Palette && edDragMoved && EdMap.Contains(Event.current.mousePosition) ? EdNearestPoint(Event.current.mousePosition) : -1;
            for (int i = 0; i < WaveSet.Letters.Length; i++)
            {
                char letter = WaveSet.Letters[i];
                var q = EdToMap(edSet.Points[i]);
                bool on = sel != null && sel.From.IndexOf(letter) >= 0 || i == dropPoint;
                var box = new Rect(q.x - 12f, q.y - 12f, 24f, 24f);
                if (on)
                    Ui.Glow(box, EdColor, 0.5f + 0.2f * Mathf.Sin(t * 4f));
                Ui.Fill(box, on ? new Color(EdColor.r, EdColor.g, EdColor.b, 0.35f) : new Color(0f, 0f, 0f, 0.6f));
                CrtKit.Frame(box, on ? 2f : 1f, on ? EdColor : new Color(ink.r, ink.g, ink.b, 0.7f));
                Ui.Label(box, letter.ToString(), 12, on ? Color.white : CrtUi.Ink, TextAnchor.MiddleCenter, true);
            }

            string hint = edPropsMode
                ? "Click empty ground to place a " + WaveSet.PropName(edPropKind) + "  ·  drag to move  ·  right-click removes"
                : sel != null ? "Click a letter to send \"" + sel.Name + "\" from it  ·  drag a letter to move that point"
                : "Select a group below, then click a letter to send it from there";
            Ui.Label(new Rect(p.x + 14f, p.yMax - 24f, p.width - 28f, 18f), hint, 10, CrtStyle.Desc);
            if (edPropsMode)
            {
                var kinds = new[] { PropKind.Crate, PropKind.HeavyCrate, PropKind.Pot, PropKind.Barrel, PropKind.Chest };
                for (int i = 0; i < kinds.Length; i++)
                    if (DkChoice(new Rect(p.x + 108f + i * 48f, p.y + 6f, 46f, 24f), WaveSet.PropName(kinds[i]).Replace("heavy ", "big ").ToUpperInvariant(), edPropKind == kinds[i], CrtStyle.Gold))
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
            DkPanel(r);
            string[] tabs = { "ENEMIES", "SQUADS", "BOSSES" };
            for (int i = 0; i < tabs.Length; i++)
                if (DkChoice(new Rect(r.x + 10f + i * 86f, r.y + 8f, 82f, 24f), tabs[i], edPalTab == i, EdColor))
                {
                    edPalTab = i;
                    edPalScroll = 0;
                }
            Ui.Label(new Rect(r.x + 14f, r.yMax - 24f, r.width - 28f, 18f), edPalTab == 1 ? "Drag a squad onto the timeline  ·  or click to add it at the playhead"
                : "Drag onto the timeline, map or a bar  ·  click adds", 10, CrtStyle.Desc);
            var area = new Rect(r.x + 10f, r.y + 40f, r.width - 20f, r.height - 70f);
            var ev = Event.current;
            bool overArea = area.Contains(ev.mousePosition);
            if (edPalTab == 1)
            {
                var squads = WaveSet.AllSquads().ToArray();
                float bw = (area.width - 8f) / 3f;
                const int sRows = 7;
                int sTotal = (squads.Length + 2) / 3;
                if (ev.type == EventType.ScrollWheel && overArea)
                {
                    edPalScroll += ev.delta.y > 0 ? 1 : -1;
                    ev.Use();
                }
                edPalScroll = Mathf.Clamp(edPalScroll, 0, Mathf.Max(0, sTotal - sRows));
                for (int i = edPalScroll * 3; i < squads.Length && i < (edPalScroll + sRows) * 3; i++)
                {
                    int k = i - edPalScroll * 3;
                    var b = new Rect(area.x + (k % 3) * (bw + 4f), area.y + (k / 3) * 31f, bw, 27f);
                    bool faction = Approaches.Of(squads[i].Members[0]) != null && Approaches.All.Any(f => f.Squads.Contains(squads[i]) || f.Finale == squads[i]);
                    DkKey(b, squads[i].Name, faction ? CrtStyle.Gold : (Color)Ui.Neutral, false, 11);
                    if (ev.type == EventType.MouseDown && ev.button == 0 && b.Contains(ev.mousePosition))
                        EdStartPaletteDrag(null, squads[i], ev);
                }
                return;
            }
            var list = EdPaletteEnemies(edPalTab == 2);
            const int cols = 6;
            float tw = (area.width - (cols - 1) * 4f) / cols, th = 40f;
            int rowsVisible = Mathf.Max(1, Mathf.FloorToInt((area.height + 4f) / (th + 4f)));
            int total = (list.Count + cols - 1) / cols;
            if (ev.type == EventType.ScrollWheel && overArea)
            {
                edPalScroll += ev.delta.y > 0 ? 1 : -1;
                ev.Use();
            }
            edPalScroll = Mathf.Clamp(edPalScroll, 0, Mathf.Max(0, total - rowsVisible));
            var cells = new List<KeyValuePair<EnemyType, Rect>>();
            var ink = CrtUi.Ink;
            for (int i = edPalScroll * cols; i < list.Count && i < (edPalScroll + rowsVisible) * cols; i++)
            {
                int k = i - edPalScroll * cols;
                var cell = new Rect(area.x + (k % cols) * (tw + 4f), area.y + (k / cols) * (th + 4f), tw, th);
                bool faction = Approaches.Of(list[i]) != null;
                bool hover = cell.Contains(ev.mousePosition);
                Color tint = faction ? CrtStyle.Gold : ink;
                Ui.Fill(cell, new Color(tint.r, tint.g, tint.b, hover ? 0.14f : faction ? 0.07f : 0.04f));
                CrtKit.Frame(cell, 1f, new Color(tint.r, tint.g, tint.b, hover ? 0.75f : 0.25f));
                Ui.Label(new Rect(cell.x, cell.yMax - 13f, cell.width, 12f), PracticePlan.NameOf(list[i]), 8, CrtStyle.Desc, TextAnchor.MiddleCenter);
                cells.Add(new KeyValuePair<EnemyType, Rect>(list[i], cell));
                if (ev.type == EventType.MouseDown && ev.button == 0 && hover)
                    EdStartPaletteDrag(list[i], null, ev);
            }
            if (total > rowsVisible)
                Ui.Label(new Rect(r.xMax - 84f, r.yMax - 24f, 72f, 18f), "scroll " + (edPalScroll + 1) + "/" + (total - rowsVisible + 1), 9, CrtStyle.Desc, TextAnchor.MiddleRight);
            if (BeginUiGl())
            {
                foreach (var kv in cells)
                {
                    UiClip(kv.Value);
                    DrawEnemyPreview(kv.Key, new V2(kv.Value.center.x, kv.Value.y + 14f), 24f, t, kv.Value.Contains(ev.mousePosition));
                }
                EndUiGl();
            }
        }

        void EdStartPaletteDrag(EnemyType? type, SquadDef squad, Event ev)
        {
            edDrag = EdDrag.Palette;
            edDragEnemy = type;
            edDragSquad = squad;
            edDragStart = ev.mousePosition;
            edDragMoved = false;
            ev.Use();
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

        void EdAddGroup(EnemyType? type, SquadDef squad, float at = -1f, string from = "A")
        {
            var g = new DesignGroup { At = at >= 0f ? at : Mathf.Round(edT * 10f) / 10f, From = from };
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
            edStatus = "Added " + g.Name + " at " + g.At.ToString("0.0") + " s from " + g.From;
        }

        void EdRemoveGroup()
        {
            if (EdG == null)
                return;
            EdW.Groups.RemoveAt(edGroup);
            edGroup = Mathf.Min(edGroup, EdW.Groups.Count - 1);
            edStatus = "Deleted the group (Ctrl+Z brings it back)";
        }

        void EdCopyGroup()
        {
            var g = EdG;
            if (g == null)
                return;
            var copy = g.Copy();
            copy.At = g.At + g.Span + 1f;
            EdW.Groups.Insert(edGroup + 1, copy);
            edGroup++;
            edTrackScroll = Mathf.Max(edTrackScroll, edGroup - EdRows + 1);
            edStatus = "Copied the group to " + copy.At.ToString("0.0") + " s";
        }

        /// Send the selected group from this spawn point (`only`: just it; otherwise toggle it among the group's points).
        void EdSetFrom(int point, bool only)
        {
            var g = EdG;
            if (g == null)
                return;
            char letter = WaveSet.Letters[point];
            bool on = g.From.IndexOf(letter) >= 0;
            if (only)
                g.From = letter.ToString();
            else if (!(on && g.From.Length == 1))
                g.From = on ? g.From.Replace(letter.ToString(), "") : new string((g.From + letter).OrderBy(ch => ch).ToArray());
        }

        // --- The inspector ------------------------------------------------------------------------------------

        void DrawEdInspector(Rect r)
        {
            DkPanel(r);
            var g = EdG;
            float x = r.x + 14f, w = r.width - 28f, y = r.y + 10f;
            if (g == null)
            {
                DrawEdPanelTitle(r, "SELECTED GROUP");
                Ui.Label(new Rect(x, y + 24f, w, 90f), "Nothing selected.\n\nDrag an enemy or squad from the palette onto the timeline to make a group, or click a group's bar or name in the timeline to change it.",
                    12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
                return;
            }
            Ui.Label(new Rect(x, y, 150f, 14f), "SELECTED GROUP", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (DkKey(new Rect(r.xMax - 152f, y - 4f, 68f, 22f), "COPY", Ui.Neutral, false, 11))
                EdCopyGroup();
            if (DkKey(new Rect(r.xMax - 80f, y - 4f, 66f, 22f), "DELETE", Ui.Danger, false, 11))
            {
                EdRemoveGroup();
                return;
            }
            y += 22f;
            g.Name = DkField(new Rect(x, y, w, 28f), g.Name, 30);
            y += 36f;

            Ui.Label(new Rect(x, y, 60f, 24f), "FROM", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            for (int i = 0; i < WaveSet.Letters.Length; i++)
            {
                char letter = WaveSet.Letters[i];
                bool on = g.From.IndexOf(letter) >= 0;
                if (DkChoice(new Rect(x + 56f + i * 34f, y, 30f, 24f), letter.ToString(), on, EdColor))
                    EdSetFrom(i, false);
            }
            y += 32f;

            Ui.Label(new Rect(x, y, 60f, 24f), "STARTS", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (DkKey(new Rect(x + 56f, y, 26f, 24f), "−", Ui.Neutral, false, 13)) g.At = Mathf.Max(0f, g.At - 0.5f);
            Ui.Label(new Rect(x + 84f, y, 60f, 24f), g.At.ToString("0.0") + " s", 13, Color.white, TextAnchor.MiddleCenter, true);
            if (DkKey(new Rect(x + 146f, y, 26f, 24f), "+", Ui.Neutral, false, 13)) g.At += 0.5f;
            if (g.Squad == null && g.Arrive == ArriveMode.OneByOne)
            {
                Ui.Label(new Rect(x + 184f, y, 44f, 24f), "GAP", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
                if (DkKey(new Rect(x + 218f, y, 24f, 24f), "−", Ui.Neutral, false, 13)) g.Every = Mathf.Max(0f, Mathf.Round((g.Every - 0.1f) * 10f) / 10f);
                Ui.Label(new Rect(x + 244f, y, 40f, 24f), g.Every.ToString("0.0") + " s", 12, Color.white, TextAnchor.MiddleCenter, true);
                if (DkKey(new Rect(x + 286f, y, 24f, 24f), "+", Ui.Neutral, false, 13)) g.Every = Mathf.Round((g.Every + 0.1f) * 10f) / 10f;
            }
            y += 32f;

            if (g.Squad != null)
            {
                Ui.Label(new Rect(x, y, w, 18f), "SQUAD  ·  " + g.Squad.ToUpperInvariant(), 10, CrtStyle.Gold, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(x, y + 20f, w, 60f), WaveSet.MembersText(g.Arrivals) + ". It arrives in its own formation, from the first point picked.", 12, CrtStyle.Body, TextAnchor.UpperLeft, false, true);
                return;
            }
            Ui.Label(new Rect(x, y - 2f, w, 12f), "ARRIVE", 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            y += 12f;
            string[] modes = { "ONE BY ONE", "TOGETHER", "LINE", "RING" };
            for (int i = 0; i < modes.Length; i++)
                if (DkChoice(new Rect(x + i * 82f, y, 78f, 24f), modes[i], g.Arrive == (ArriveMode)i, EdColor))
                    g.Arrive = (ArriveMode)i;
            y += 30f;

            // Members: each kind with its count.
            var kinds = g.Members.Distinct().ToList();
            Ui.Label(new Rect(x, y, w, 14f), "ENEMIES  ·  " + g.Members.Count + "   (drop more on the bar, or use + and −)", 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            y += 16f;
            for (int i = 0; i < kinds.Count && y < r.yMax - 26f; i++)
            {
                var k = kinds[i];
                int n = g.Members.Count(m => m == k);
                var row = new Rect(x, y, w, 24f);
                DkSub(row, new Color(1f, 1f, 1f, 0.03f));
                Ui.Dot(new Rect(row.x + 6f, row.y + 7f, 10f, 10f), ToColor(Defs.Get(k).Color));
                Ui.Label(new Rect(row.x + 22f, row.y, 160f, 24f), Defs.Get(k).Name, 12, CrtStyle.Body);
                if (DkKey(new Rect(row.xMax - 90f, row.y + 2f, 24f, 20f), "−", Ui.Neutral, false, 12))
                    g.Members.RemoveAt(g.Members.LastIndexOf(k));
                Ui.Label(new Rect(row.xMax - 64f, row.y, 36f, 24f), "×" + n, 12, Color.white, TextAnchor.MiddleCenter, true);
                if (DkKey(new Rect(row.xMax - 28f, row.y + 2f, 24f, 20f), "+", Ui.Neutral, false, 12))
                    g.Members.Insert(g.Members.LastIndexOf(k) + 1, k);
                y += 28f;
            }
            if (g.Members.Count == 0)
                Ui.Label(new Rect(x, y, w, 40f), "Empty: drag enemies from the palette onto this group's bar in the timeline.", 11, Ui.Warn, TextAnchor.UpperLeft, false, true);
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
            DkPanel(r);
            var w = EdW;
            float span = EdSpan();
            var ink = CrtUi.Ink;
            Ui.Label(new Rect(r.x + 14f, r.y + 8f, 400f, 16f), "WAVE " + (edWave + 1) + "  ·  " + w.Groups.Count + " GROUPS  ·  " + w.EnemyCount + " ENEMIES  ·  " + w.Props.Count + " PROPS",
                10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (DkKey(new Rect(r.xMax - 250f, r.y + 6f, 80f, 24f), edPlaying ? "PAUSE" : "PLAY", EdColor, edPlaying, 12))
                edPlaying = !edPlaying;
            if (DkKey(new Rect(r.xMax - 164f, r.y + 6f, 84f, 24f), "TO START", Ui.Neutral, false, 12))
                edT = 0f;
            Ui.Label(new Rect(r.xMax - 72f, r.y + 6f, 60f, 24f), edT.ToString("0.0") + " s", 12, Color.white, TextAnchor.MiddleRight, true);

            // Seconds along the top.
            float trackTop = r.y + 50f, trackBottom = trackTop + EdRows * EdRowH;
            for (int s = 0; s <= span; s += 2)
            {
                float tx = EdTimeToX(s);
                Ui.Fill(new Rect(tx, trackTop - 4f, 1f, trackBottom - trackTop), new Color(ink.r, ink.g, ink.b, 0.07f));
                Ui.Label(new Rect(tx - 20f, trackTop - 20f, 40f, 14f), s + "s", 10, CrtStyle.Desc, TextAnchor.MiddleCenter);
            }

            // One track a group.
            var mp = Event.current.mousePosition;
            for (int i = edTrackScroll; i < w.Groups.Count && i < edTrackScroll + EdRows; i++)
            {
                var g = w.Groups[i];
                float y = EdRowY(i);
                bool sel = i == edGroup;
                var label = new Rect(r.x + 8f, y, EdLabelW - 14f, EdRowH - 4f);
                DkSub(label, sel ? new Color(EdColor.r, EdColor.g, EdColor.b, 0.2f) : new Color(1f, 1f, 1f, 0.03f));
                var col = g.Arrivals.Count > 0 ? ToColor(Defs.Get(g.Arrivals[0]).Color) : CrtStyle.Desc;
                Ui.Dot(new Rect(label.x + 6f, label.y + 8f, 10f, 10f), col);
                Ui.Label(new Rect(label.x + 22f, label.y, label.width - 26f, label.height), g.Name, 11, sel ? Color.white : CrtStyle.Body, TextAnchor.MiddleLeft, sel);
                if (Ui.HitArea(label))
                    edGroup = i;
                Ui.Fill(new Rect(EdTrackX0, y, EdTrackX1 - EdTrackX0, EdRowH - 4f), new Color(ink.r, ink.g, ink.b, 0.03f));
                var clip = EdClip(g, i);
                bool dropOn = edDrag == EdDrag.Palette && edDragMoved && edDragEnemy.HasValue && g.Squad == null && clip.Contains(mp);
                Ui.Fill(clip, sel || dropOn ? Color.Lerp(col, Color.white, 0.25f) : EdDark(col, 0.85f));
                if (sel || dropOn)
                    CrtKit.Frame(clip, 2f, Color.white);
                if (sel)
                    Ui.Fill(new Rect(clip.xMax - 4f, clip.y + 3f, 2f, clip.height - 6f), new Color(0f, 0f, 0f, 0.5f));
                string text = (g.Squad != null ? g.Squad : WaveSet.MembersText(g.Members)) + "  ·  from " + g.From;
                if (Ui.TextWidth(text, 10, true) < clip.width - 10f)
                    Ui.Label(new Rect(clip.x + 6f, clip.y, clip.width - 8f, clip.height), text, 10, new Color(0.04f, 0.05f, 0.08f), TextAnchor.MiddleLeft, true);
                else  // too short to hold it: beside the clip, in light text
                    Ui.Label(new Rect(clip.xMax + 6f, clip.y, 320f, clip.height), text, 10, sel ? Color.white : CrtStyle.Body, TextAnchor.MiddleLeft, true);
            }
            if (w.Groups.Count == 0)
                Ui.Label(new Rect(EdTrackX0, trackTop + 6f, 700f, 40f), "No groups yet. Drag an enemy or squad from the palette and drop it here: where you drop it is when it arrives.", 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            if (w.Groups.Count > EdRows)
            {
                if (DkKey(new Rect(r.x + 8f, trackBottom + 2f, 66f, 22f), "UP", Ui.Neutral, false, 11) && edTrackScroll > 0) edTrackScroll--;
                if (DkKey(new Rect(r.x + 78f, trackBottom + 2f, 66f, 22f), "DOWN", Ui.Neutral, false, 11) && edTrackScroll < w.Groups.Count - EdRows) edTrackScroll++;
            }

            // The playhead.
            float px = EdTimeToX(Mathf.Min(edT, span));
            Ui.Fill(new Rect(px - 1f, trackTop - 8f, 2f, trackBottom - trackTop + 8f), Color.white);
            Ui.Fill(new Rect(px - 6f, trackTop - 14f, 12f, 8f), Color.white);

            // A palette drag over the timeline: where it would start.
            if (edDrag == EdDrag.Palette && edDragMoved && r.Contains(mp))
            {
                float dt = Mathf.Round(EdXToTime(mp.x) * 10f) / 10f, dx = EdTimeToX(dt);
                Ui.Fill(new Rect(dx - 1f, trackTop - 8f, 2f, trackBottom - trackTop + 8f), EdColor);
                Ui.Label(new Rect(dx - 30f, trackBottom + 2f, 60f, 16f), dt.ToString("0.0") + " s", 11, EdColor, TextAnchor.MiddleCenter, true);
            }

            Ui.Label(new Rect(r.x + 14f, r.yMax - 26f, r.width - 28f, 18f), EdHint(), 11, edStatus.Length > 0 && Time.unscaledTime - edStatusAt < 5f ? EdColor : CrtStyle.Desc);
        }

        /// The line under the timeline: what just happened, or what you can do next.
        string EdHint()
        {
            if (edDrag == EdDrag.Palette && edDragMoved)
                return "Release on the timeline to set when it starts  ·  on the map to pick where it comes from  ·  on a group's bar to add to it";
            if (edStatus.Length > 0 && Time.unscaledTime - edStatusAt < 5f)
                return edStatus;
            if (EdW.Groups.Count == 0)
                return "Start here: drag an enemy from the palette onto the timeline  ·  Space plays the wave";
            if (EdG != null)
                return "Drag the bar to change when it starts  ·  its right edge spreads the enemies out  ·  Ctrl+D copies  ·  Delete removes  ·  Ctrl+Z undoes  ·  Space plays";
            return "Click a group's bar or name to select it  ·  click the ruler to move the playhead  ·  Space plays the wave";
        }

        /// While a palette item is being dragged: a chip following the mouse.
        void DrawEdDragGhost()
        {
            if (edDrag != EdDrag.Palette || !edDragMoved)
                return;
            string name = edDragSquad != null ? edDragSquad.Name : PracticePlan.NameOf(edDragEnemy.Value);
            var mp = Event.current.mousePosition;
            var chip = new Rect(mp.x + 14f, mp.y + 8f, Mathf.Max(90f, Ui.TextWidth(name, 12, true) + 24f), 26f);
            Ui.Fill(chip, new Color(CrtUi.Bg.r, CrtUi.Bg.g, CrtUi.Bg.b, 0.95f));
            CrtKit.Frame(chip, 1f, EdColor);
            Ui.Label(chip, name, 12, Color.white, TextAnchor.MiddleCenter, true);
        }

        // --- Mouse: dragging clips, the playhead, spawn points and props; dropping palette items ----------------

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
                    edDragStart = mp;
                    edDragMoved = false;
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
                        if (EdG != null)  // empty ground: send the group from the nearest point
                        {
                            EdSetFrom(EdNearestPoint(mp), true);
                            edStatus = "\"" + EdG.Name + "\" now comes from " + EdG.From;
                            e.Use();
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
                if (Vector2.Distance(mp, edDragStart) > 5f)
                    edDragMoved = true;
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
                        if (edDragMoved)
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
                if (edDrag == EdDrag.Point && !edDragMoved && EdG != null)  // a click on a letter: send the group from it too
                {
                    EdSetFrom(edDragIndex, false);
                    edStatus = "\"" + EdG.Name + "\" now comes from " + EdG.From;
                }
                else if (edDrag == EdDrag.Palette)
                    EdDropPalette(mp, e.shift);
                edDrag = EdDrag.None;
                edDragMoved = false;
                e.Use();
            }
        }

        /// A palette item let go: a click adds it at the playhead (shift: into the selected group); a drag onto a group's bar adds it
        /// there, onto the timeline starts a group at that time, onto the map starts one at the playhead from the nearest point.
        void EdDropPalette(Vector2 mp, bool shift)
        {
            var w = EdW;
            if (!edDragMoved)
            {
                if (shift && edDragEnemy.HasValue && EdG != null && EdG.Squad == null)
                {
                    EdG.Members.Add(edDragEnemy.Value);
                    edStatus = "Added a " + Defs.Get(edDragEnemy.Value).Name + " to " + EdG.Name;
                }
                else
                    EdAddGroup(edDragEnemy, edDragSquad);
                return;
            }
            if (EdTimeline.Contains(mp))
            {
                if (edDragEnemy.HasValue)
                    for (int i = edTrackScroll; i < w.Groups.Count && i < edTrackScroll + EdRows; i++)
                        if (w.Groups[i].Squad == null && EdClip(w.Groups[i], i).Contains(mp))
                        {
                            w.Groups[i].Members.Add(edDragEnemy.Value);
                            edGroup = i;
                            edStatus = "Added a " + Defs.Get(edDragEnemy.Value).Name + " to " + w.Groups[i].Name;
                            return;
                        }
                EdAddGroup(edDragEnemy, edDragSquad, Mathf.Round(EdXToTime(mp.x) * 10f) / 10f);
            }
            else if (EdMap.Contains(mp) && !edLoadOpen)
                EdAddGroup(edDragEnemy, edDragSquad, -1f, WaveSet.Letters[EdNearestPoint(mp)].ToString());
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

        /// The waves folder in the project (next to Assets), whose files Tools/WaveGen/build.sh builds into the game.
        static string ProjectWavesFolder => Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Data", "Waves"));

        void DrawEdFiles(Rect r)
        {
            DkPanel(r);
            Ui.Label(new Rect(r.x + 14f, r.y + 8f, r.width - 28f, 16f), "OPEN WAVES  ·  click one to open it", 10, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            if (DkKey(new Rect(r.xMax - 114f, r.y + 6f, 102f, 22f), "OPEN FOLDER", Ui.Neutral, false, 10))
            {
                Directory.CreateDirectory(WavesFolder);
                Application.OpenURL("file:///" + WavesFolder.Replace('\\', '/'));
            }
            float half = (r.width - 30f) / 2f;
            bool inEditor = Application.isEditor;
            if (DkKey(new Rect(r.x + 12f, r.y + 34f, inEditor ? half : r.width - 24f, 28f), "NEW: A BLANK SET", EdColor, false, 12))
            {
                edSet = WaveSet.Starter();
                EdSelectWave(0);
                EdHistoryReset();
                edLoadOpen = false;
                edStatus = "New wave set";
            }
            if (inEditor && DkKey(new Rect(r.x + 18f + half, r.y + 34f, half, 28f), "SAVE TO PROJECT", Ui.GoldButton, false, 12))
                EdSaveToProject();

            // Built into the game (Data/Waves, by Tools/WaveGen/build.sh): a copy to look at or change.
            float y = r.y + 70f;
            Ui.Label(new Rect(r.x + 12f, y, r.width - 24f, 14f), "BUILT INTO THE GAME", 9, EdColor, TextAnchor.MiddleLeft, true);
            y += 16f;
            for (int i = 0; i < DesignedWaves.Names.Length; i++)
            {
                var b = new Rect(r.x + 12f + (i % 2) * (half + 6f), y + (i / 2) * 30f, half, 26f);
                if (DkKey(b, DesignedWaves.Names[i], Ui.Neutral, false, 12))
                {
                    edSet = DesignedWaves.Get(DesignedWaves.Names[i]);
                    edSet.Name = DesignedWaves.Names[i];
                    EdSelectWave(0);
                    EdHistoryReset();
                    edLoadOpen = false;
                    edStatus = "Opened the built-in " + DesignedWaves.Names[i] + " (save to keep your changes)";
                }
            }
            if (DesignedWaves.Names.Length == 0)
                Ui.Label(new Rect(r.x + 12f, y, r.width - 24f, 26f), "None yet.", 11, CrtStyle.Desc);
            y += Mathf.Max(1, (DesignedWaves.Names.Length + 1) / 2) * 30f + 6f;

            // Your saved files (this computer's waves folder).
            Ui.Label(new Rect(r.x + 12f, y, r.width - 24f, 14f), "YOUR SAVED FILES", 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
            y += 16f;
            string[] files;
            try
            {
                files = Directory.Exists(WavesFolder) ? Directory.GetFiles(WavesFolder, "*.waves").OrderByDescending(File.GetLastWriteTime).Take(16).ToArray() : new string[0];
            }
            catch (Exception)
            {
                files = new string[0];
            }
            if (files.Length == 0)
                Ui.Label(new Rect(r.x + 12f, y, r.width - 24f, 40f), "Nothing saved yet. Name your set at the top and press SAVE.", 12, CrtStyle.Desc, TextAnchor.UpperLeft, false, true);
            for (int i = 0; i < files.Length; i++)
            {
                var b = new Rect(r.x + 12f + (i % 2) * (half + 6f), y + (i / 2) * 30f, half, 26f);
                if (b.yMax > r.yMax - 8f)
                    break;
                if (DkKey(b, Path.GetFileNameWithoutExtension(files[i]), Ui.Neutral, false, 12))
                    EdLoad(files[i]);
            }
        }

        /// In the Unity editor: save the set into the project's Data/Waves, ready for Tools/WaveGen/build.sh to build in.
        void EdSaveToProject()
        {
            string clean = new string((edSet.Name ?? "").Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' || c == '_').ToArray()).Trim().Replace(' ', '-').ToLowerInvariant();
            if (clean.Length == 0)
                clean = "untitled";
            try
            {
                Directory.CreateDirectory(ProjectWavesFolder);
                string path = Path.Combine(ProjectWavesFolder, clean + ".waves");
                bool existed = File.Exists(path);
                File.WriteAllText(path, edSet.ToText());
                edStatus = (existed ? "Updated" : "Saved") + " Data/Waves/" + clean + ".waves: run Tools/WaveGen/build.sh to build it into the game";
            }
            catch (Exception ex)
            {
                edStatus = "Couldn't save to the project: " + ex.Message;
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
                EdHistoryReset();
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

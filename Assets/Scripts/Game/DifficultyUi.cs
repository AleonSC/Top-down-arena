using UnityEngine;

namespace Arena
{
    /// The lobby's Difficulty tab (Difficulty.cs): five cards (Easy, Normal, Hard, Nightmare, Custom), then every
    /// number as a slider with a text box. The host picks a card or changes a number (which makes it Custom);
    /// everyone else sees the same, read-only. The difficulty also shows under the wave plate during the match.
    public partial class ArenaApp
    {
        static readonly Color[] DiffColors =
        {
            new Color(0.45f, 1f, 0.55f),    // Easy
            new Color(0.36f, 0.6f, 1f),     // Normal
            new Color(1f, 0.6f, 0.3f),      // Hard
            new Color(1f, 0.3f, 0.38f),     // Nightmare
            new Color(1f, 0.8f, 0.35f),     // Custom
        };

        static Color DiffColor(DifficultyLevel l) => DiffColors[(int)l];

        static Color DiffBadgeColor(DifficultyLevel l)
        {
            var c = DiffColor(l);
            return new Color(c.r * 0.7f, c.g * 0.7f, c.b * 0.7f);
        }

        string diffFocus = "", diffBuffer = "";

        void DrawDifficulty(Rect r, float t)
        {
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            bool host = session.IsHost, locked = session.Resume != null;
            bool canEdit = host && !locked;
            var diff = session.Diff;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;

            // The five keys.
            const float gap = 8f, ch = 62f;
            int n = Difficulty.Names.Length;
            float cw = (w - gap * (n - 1)) / n;
            for (int i = 0; i < n; i++)
            {
                var level = (DifficultyLevel)i;
                var c = new Rect(x + i * (cw + gap), y, cw, ch);
                if (DrawDiffCard(c, level, diff.Level == level, canEdit, t))
                    session.SetDifficulty(level);
            }
            y += ch + 8f;

            // What it means (or the hovered number's description).
            string line = locked ? "A continued run keeps the difficulty it was saved with."
                : !host ? "The host picks the difficulty. " + Difficulty.Blurbs[(int)diff.Level]
                : Difficulty.Blurbs[(int)diff.Level] + (diff.Level == DifficultyLevel.Custom ? "" : "  Change any number to make it Custom.");
            var lineRect = new Rect(x, y, w, 18f);
            y += 24f;

            // Every number: two columns of a name, its value, a slider and a text box.
            const int cols = 2;
            const float colGap = 22f, rowH = 41f;
            float colW = (w - colGap) / cols;
            int shown = 0;  // the flavour knobs (boss approach) have quick picks below instead of sliders
            foreach (var fk in Difficulty.Knobs)
                if (!fk.Flavour)
                    shown++;
            int rows = (shown + cols - 1) / cols;
            string hoverDesc = null;
            int slotIdx = 0;
            for (int ki = 0; ki < Difficulty.Knobs.Length; ki++)
            {
                var k = Difficulty.Knobs[ki];
                if (k.Flavour)
                    continue;
                int i = slotIdx++;
                var row = new Rect(x + (i / rows) * (colW + colGap), y + (i % rows) * rowH, colW, rowH - 5f);
                if (row.Contains(Event.current.mousePosition))
                    hoverDesc = k.Name + ": " + k.Desc + "  (Normal: " + k.Format(k.Normal) + ")";
                DrawKnob(row, k, diff[k.Knob], canEdit);
            }
            // Quick picks for the boss approach (Approaches.cs), in the slot after the last number.
            {
                var slot = new Rect(x, y + rows * rowH + 4f, colW, 24f);  // its own row under the numbers (the numbers fill both columns)
                bool blend = Mathf.Abs(diff[DiffKnob.ApproachShare] - 0.85f) < 1e-4f && diff[DiffKnob.ApproachRamp] >= 0.5f;
                bool full = diff[DiffKnob.ApproachShare] >= 0.999f && diff[DiffKnob.ApproachRamp] < 0.5f;
                float bw = (colW - 8f) / 2f;
                if (slot.Contains(Event.current.mousePosition))
                    hoverDesc = "Boss approach: Blend is 85% building up over the four waves (34%, 51%, 68%, 85%); Fully themed makes all four waves the boss's own enemies.";
                if (LobbyKit.Key(new Rect(slot.x, slot.y, bw, slot.height), "Blend", blend, fxReduced, size: 14, minSize: 11, enabled: canEdit) && canEdit && !blend)
                {
                    session.SetDifficultyKnob(DiffKnob.ApproachShare, 0.85f);
                    session.SetDifficultyKnob(DiffKnob.ApproachRamp, 1f);
                }
                if (LobbyKit.Key(new Rect(slot.x + bw + 8f, slot.y, bw, slot.height), "Fully themed", full, fxReduced, size: 14, minSize: 11, enabled: canEdit) && canEdit && !full)
                {
                    session.SetDifficultyKnob(DiffKnob.ApproachShare, 1f);
                    session.SetDifficultyKnob(DiffKnob.ApproachRamp, 0f);
                }
            }
            // (The arena is no longer chosen here: the Campaign is the Expanding arena, the Simple Arena modifier keeps the open
            // one with its floor plans, and the Rooms arena is a prototype switch in the dev dock. Rooms.cs ArenaKind.)
            // How the Expanding arena is built (ExpansionGame.cs): free by default, or every room to a team vote. Right of the
            // approach picks; the Campaign is the Expanding arena unless the Simple Arena modifier is on (Rooms.cs ArenaKind).
            if ((session.Modifiers & RunMod.SimpleArena) == 0)
            {
                var slot = new Rect(x + colW + colGap, y + rows * rowH + 4f, colW, 24f);
                bool vote = diff[DiffKnob.BuildVote] >= 0.5f;
                float bw = (colW - 16f) / 3f;
                bool purse = diff[DiffKnob.BuildPurse] >= 0.5f;
                if (slot.Contains(Event.current.mousePosition))
                {
                    int hover = (int)((Event.current.mousePosition.x - slot.x) / (bw + 8f));
                    hoverDesc = hover >= 2 ? "Team purse: " + Difficulty.Knobs[(int)DiffKnob.BuildPurse].Desc : "Building: " + Difficulty.Knobs[(int)DiffKnob.BuildVote].Desc;
                }
                if (LobbyKit.Key(new Rect(slot.x, slot.y, bw, slot.height), "Free building", !vote, fxReduced, size: 14, minSize: 11, enabled: canEdit) && canEdit && vote)
                    session.SetDifficultyKnob(DiffKnob.BuildVote, 0f);
                if (LobbyKit.Key(new Rect(slot.x + bw + 8f, slot.y, bw, slot.height), "Team vote", vote, fxReduced, size: 14, minSize: 11, enabled: canEdit) && canEdit && !vote)
                    session.SetDifficultyKnob(DiffKnob.BuildVote, 1f);
                if (LobbyKit.Key(new Rect(slot.x + 2f * (bw + 8f), slot.y, bw, slot.height), "Team purse", purse, fxReduced, size: 14, minSize: 11, enabled: canEdit) && canEdit)
                    session.SetDifficultyKnob(DiffKnob.BuildPurse, purse ? 0f : 1f);
            }
            Ui.Label(lineRect, hoverDesc ?? line, 12, hoverDesc != null ? LobbyKit.Soft : CrtUi.Dim);
        }

        bool DrawDiffCard(Rect c, DifficultyLevel level, bool on, bool canClick, float t)
        {
            var col = DiffColor(level);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            bool fill = on || hover;
            var ink = CrtUi.Ink;
            // A soft key like the lobby's others: a faint tint and a thin outline, a little brighter when picked or under the mouse
            // (a 2 px outline and an underline glow for the picked one), never a solid block.
            CrtKit.SoftBox(c, ink, on, hover, false);
            var tc = CrtKit.SoftText(ink, fill);
            var nr = new Rect(c.x + 4f, c.y + 6f, c.width - 8f, 24f);
            if (fill)
                Ui.Label(nr, Difficulty.Names[(int)level].ToUpperInvariant(), 16, tc, TextAnchor.MiddleCenter, true);
            else
                CrtUi.Text(nr, Difficulty.Names[(int)level].ToUpperInvariant(), 16, tc, TextAnchor.MiddleCenter, true, fxReduced);
            // Danger: one skull for Easy up to four for Nightmare; a gear for Custom. The icons keep the level's own colour.
            float iy = c.y + 36f;
            var ic = new Color(col.r, col.g, col.b, fill ? 1f : 0.9f);
            if (level == DifficultyLevel.Custom)
                Ui.Icon(new Rect(c.center.x - 8f, iy, 16f, 16f), PixelArt.Get("ui_gear"), ic);
            else
            {
                int skulls = (int)level + 1;
                float sx = c.center.x - skulls * 9f;
                for (int s = 0; s < skulls; s++)
                    Ui.Icon(new Rect(sx + s * 18f + 1f, iy, 16f, 16f), PixelArt.Get("ui_skull"), ic);
            }
            return canClick && !on && Ui.HitArea(c);
        }

        /// One number: its name and value (red when harder than Normal, green when easier), the slider with a tick
        /// where Normal is, and a text box to type it.
        void DrawKnob(Rect r, KnobDef k, float value, bool canEdit)
        {
            float normal = k.Normal;
            bool harder = !k.Flavour && (k.HarderUp ? value > normal + 1e-5f : value < normal - 1e-5f);
            bool easier = !k.Flavour && (k.HarderUp ? value < normal - 1e-5f : value > normal + 1e-5f);
            var vc = harder ? HarderColor : easier ? EasierColor : LobbyKit.Soft;
            const float boxW = 64f;
            Ui.Label(new Rect(r.x, r.y, r.width - boxW - 8f, 16f), k.Name.ToUpperInvariant(), 11, CrtUi.Dim, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x, r.y, r.width - boxW - 8f, 16f), k.Format(value), 12, vc, TextAnchor.MiddleRight, true);

            var sr = new Rect(r.x, r.y + 18f, r.width - boxW - 8f, 18f);
            float frac = k.ToSlider(value);
            // Normal's place on the track.
            float nx = sr.x + 5f + (sr.width - 10f) * k.ToSlider(normal);
            if (canEdit)
            {
                float s = Ui.CrtSlider(sr, frac);
                Ui.Fill(new Rect(nx - 1f, sr.y + 1f, 2f, 3f), LobbyKit.Gold);
                Ui.Fill(new Rect(nx - 1f, sr.yMax - 4f, 2f, 3f), LobbyKit.Gold);
                if (Mathf.Abs(s - frac) > 1e-4f)
                {
                    float v = k.FromSlider(s);
                    if (Mathf.Abs(v - value) > 1e-5f)
                        session.SetDifficultyKnob(k.Knob, v);
                }
            }
            else
            {
                Ui.CrtSliderDraw(sr, frac, false);
                Ui.Fill(new Rect(nx - 1f, sr.y + 1f, 2f, 3f), LobbyKit.Gold);
                Ui.Fill(new Rect(nx - 1f, sr.yMax - 4f, 2f, 3f), LobbyKit.Gold);
            }

            // The text box: keeps what you type while it has focus, and applies it whenever it's a number.
            var box = new Rect(r.xMax - boxW, r.y + 14f, boxW, 22f);
            string shown = k.Edit(value);
            if (!canEdit)
            {
                Ui.Fill(box, LobbyKit.Ink(0.05f));
                CrtKit.Frame(box, 1f, LobbyKit.Ink(0.35f));
                Ui.Label(box, shown, 12, CrtUi.Dim, TextAnchor.MiddleCenter, true);
                return;
            }
            string name = "diffknob" + (int)k.Knob;
            bool focused = GUI.GetNameOfFocusedControl() == name;
            if (!focused && diffFocus == name)
                diffFocus = "";
            if (focused && Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter))
            {
                GUI.FocusControl(null);  // Enter: done typing (the box shows the number as it was taken)
                diffFocus = "";
                focused = false;
            }
            string text = focused && diffFocus == name ? diffBuffer : shown;
            GUI.SetNextControlName(name);
            Ui.Fill(box, LobbyKit.Ink(focused ? 0.16f : 0.08f));
            CrtKit.Frame(box, 1f, focused ? CrtUi.Ink : LobbyKit.Ink(0.6f));
            string typed = Ui.BareField(box, text, 8, 12);
            if (focused)
            {
                diffFocus = name;
                diffBuffer = typed;
                float v;
                if (typed != text && k.TryParse(typed, out v) && Mathf.Abs(k.Clamp(v) - value) > 1e-5f)
                    session.SetDifficultyKnob(k.Knob, v);
            }
        }

        /// During a match: the difficulty, as the first chip under the wave plate (not shown on Normal).
        float DrawActiveDifficulty(float x, float y)
        {
            var d = game.Diff;
            if (d.Level == DifficultyLevel.Normal)
                return x;
            string name = d.Name.ToUpperInvariant();
            float w = 14f + name.Length * 6.6f;
            var chip = new Rect(x, y, w, 18f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.5f));
            Ui.Label(chip, name, 9, DiffColor(d.Level), TextAnchor.MiddleCenter, true);
            return x + w + 4f;
        }
    }
}

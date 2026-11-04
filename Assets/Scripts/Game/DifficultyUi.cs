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
            Ui.BodyBox(r);
            bool host = session.IsHost, locked = session.Resume != null;
            bool canEdit = host && !locked;
            var diff = session.Diff;
            var accent = DiffColor(diff.Level);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            Ui.Label(new Rect(x, y, w, 28f), diff.Name.ToUpperInvariant(), 13, accent, TextAnchor.MiddleRight, true);
            y += 32f;

            // The five cards.
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
                if (Ui.Choice(new Rect(slot.x, slot.y, bw, slot.height), "Blend", blend, ApproachColor, null, null, 12, glow: false) && canEdit && !blend)
                {
                    session.SetDifficultyKnob(DiffKnob.ApproachShare, 0.85f);
                    session.SetDifficultyKnob(DiffKnob.ApproachRamp, 1f);
                }
                if (Ui.Choice(new Rect(slot.x + bw + 8f, slot.y, bw, slot.height), "Fully themed", full, ApproachColor, null, null, 12, glow: false) && canEdit && !full)
                {
                    session.SetDifficultyKnob(DiffKnob.ApproachShare, 1f);
                    session.SetDifficultyKnob(DiffKnob.ApproachRamp, 0f);
                }
            }
            Ui.Label(lineRect, hoverDesc ?? line, 12, hoverDesc != null ? Ui.Text : Ui.Muted);
        }

        bool DrawDiffCard(Rect c, DifficultyLevel level, bool on, bool canClick, float t)
        {
            var col = DiffColor(level);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            Ui.Card(c, col, on ? 0.9f : hover ? 0.5f : 0.2f);
            if (on)
                Ui.Pill(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, c.height - 4f), new Color(col.r, col.g, col.b, 0.14f + 0.05f * Mathf.Sin(t * 3f)));
            Ui.Glitch(new Rect(c.x, c.y + 8f, c.width, 24f), Difficulty.Names[(int)level], 16,
                on ? Color.Lerp(col, Color.white, 0.75f) : new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleCenter);
            // Danger: one skull for Easy up to four for Nightmare; a gear for Custom.
            float iy = c.y + 36f;
            if (level == DifficultyLevel.Custom)
                Ui.Icon(new Rect(c.center.x - 8f, iy, 16f, 16f), PixelArt.Get("ui_gear"), on ? Color.white : new Color(col.r, col.g, col.b, 0.8f));
            else
            {
                int skulls = (int)level + 1;
                float sx = c.center.x - skulls * 9f;
                for (int s = 0; s < skulls; s++)
                    Ui.Icon(new Rect(sx + s * 18f + 1f, iy, 16f, 16f), PixelArt.Get("ui_skull"), on ? Color.white : new Color(col.r, col.g, col.b, 0.75f));
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
            var vc = harder ? HarderColor : easier ? EasierColor : Ui.Text;
            const float boxW = 64f;
            Ui.Label(new Rect(r.x, r.y, r.width - boxW - 8f, 16f), k.Name, 12, Ui.Text, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x, r.y, r.width - boxW - 8f, 16f), k.Format(value), 12, vc, TextAnchor.MiddleRight, true);

            var sr = new Rect(r.x, r.y + 18f, r.width - boxW - 8f, 18f);
            float frac = k.ToSlider(value);
            // Normal's place on the track.
            float nx = sr.x + 4f + (sr.width - 8f) * k.ToSlider(normal);
            if (canEdit)
            {
                float s = Ui.Slider(sr, frac);
                Ui.Fill(new Rect(nx - 1f, sr.y + 2f, 2f, sr.height - 4f), new Color(1f, 1f, 1f, 0.45f));
                if (Mathf.Abs(s - frac) > 1e-4f)
                {
                    float v = k.FromSlider(s);
                    if (Mathf.Abs(v - value) > 1e-5f)
                        session.SetDifficultyKnob(k.Knob, v);
                }
            }
            else
            {
                var bar = new Rect(sr.x + 4f, sr.y + sr.height / 2f - 3f, sr.width - 8f, 6f);
                Ui.Fill(bar, new Color(0.18f, 0.2f, 0.28f));
                Ui.Fill(new Rect(bar.x, bar.y, bar.width * frac, bar.height), new Color(Ui.Accent.r, Ui.Accent.g, Ui.Accent.b, 0.6f));
                Ui.Fill(new Rect(nx - 1f, sr.y + 2f, 2f, sr.height - 4f), new Color(1f, 1f, 1f, 0.45f));
                Ui.Pill(new Rect(bar.x + bar.width * frac - 6f, sr.y + 3f, 12f, 12f), new Color(0.85f, 0.88f, 0.95f));
            }

            // The text box: keeps what you type while it has focus, and applies it whenever it's a number.
            var box = new Rect(r.xMax - boxW, r.y + 14f, boxW, 22f);
            string shown = k.Edit(value);
            if (!canEdit)
            {
                Ui.Pill(box, new Color(1f, 1f, 1f, 0.05f));
                Ui.Label(box, shown, 12, Ui.Muted, TextAnchor.MiddleCenter);
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
            string typed = Ui.Field(box, text, 8);
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

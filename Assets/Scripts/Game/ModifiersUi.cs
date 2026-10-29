using UnityEngine;

namespace Arena
{
    /// The lobby's Modifiers tab: switches that change the whole run (Modifiers.cs). The host turns them on and
    /// off; everyone sees them. Active ones also show under the wave plate during the match.
    public partial class ArenaApp
    {
        static readonly Color HarderColor = new Color(1f, 0.45f, 0.45f), EasierColor = new Color(0.45f, 1f, 0.55f), RiskyColor = new Color(1f, 0.78f, 0.35f);

        static Color LeanColor(ModLean l) => l == ModLean.Harder ? HarderColor : l == ModLean.Easier ? EasierColor : RiskyColor;

        void DrawModifiers(Rect r, float t)
        {
            Ui.Panel(r);
            bool host = session.IsHost;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            Ui.Fancy(new Rect(x, y, w, 28f), "RUN MODIFIERS", 20, Color.white, Color.Lerp(ModColor, Color.white, 0.4f), TextAnchor.MiddleLeft, ModColor);
            int n = Modifiers.Count(session.Modifiers);
            Ui.Label(new Rect(x, y, w, 28f), n == 0 ? "None on: a normal run" : n + " on", 12, n == 0 ? Ui.Muted : ModColor, TextAnchor.MiddleRight, true);
            if (n > 0 && host && Ui.Button(new Rect(x + w - 150f, y + 2f, 100f, 24f), "Turn all off"))
                foreach (var d in Modifiers.All)
                    session.SetModifier(d.Mod, false);
            Ui.Label(new Rect(x, y + 28f, w, 18f), host ? "Click a card to switch it on or off. They last the whole run, for everyone (Practice Range too)."
                : "The host picks these. They last the whole run, for everyone.", 12, Ui.Muted);
            y += 54f;

            const int cols = 3;
            const float gap = 8f, ch = 94f;
            float cw = (w - gap * (cols - 1)) / cols;
            for (int i = 0; i < Modifiers.All.Length; i++)
            {
                var d = Modifiers.All[i];
                var c = new Rect(x + (i % cols) * (cw + gap), y + (i / cols) * (ch + gap), cw, ch);
                bool on = (session.Modifiers & d.Mod) != 0;
                if (DrawModCard(c, d, on, host, t))
                    session.SetModifier(d.Mod, !on);
            }
        }

        bool DrawModCard(Rect c, ModDef d, bool on, bool canClick, float t)
        {
            var lean = LeanColor(d.Lean);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            Ui.Card(c, on ? ModColor : lean, on ? 0.9f : hover ? 0.5f : 0.2f);
            if (on)
                Ui.Pill(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, c.height - 4f), new Color(ModColor.r, ModColor.g, ModColor.b, 0.12f + 0.04f * Mathf.Sin(t * 3f)));
            // Name, then what it does.
            Ui.Fancy(new Rect(c.x + 14f, c.y + 10f, c.width - 120f, 26f), d.Name, 16, Color.white, on ? Color.Lerp(ModColor, Color.white, 0.5f) : new Color(0.82f, 0.86f, 0.96f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(c.x + 14f, c.y + 34f, c.width - 26f, 36f), d.Desc, 11, on ? Ui.Text : Ui.Muted, TextAnchor.UpperLeft, false, true);
            // Harder / easier / risky tag, and the switch.
            string tag = d.Lean == ModLean.Harder ? "HARDER" : d.Lean == ModLean.Easier ? "EASIER" : "RISKY";
            Ui.Badge(new Rect(c.x + 14f, c.yMax - 21f, 64f, 16f), tag, new Color(lean.r * 0.7f, lean.g * 0.7f, lean.b * 0.7f));
            var sw = new Rect(c.xMax - 66f, c.y + 12f, 50f, 22f);
            Ui.Pill(sw, on ? new Color(ModColor.r * 0.85f, ModColor.g * 0.85f, ModColor.b * 0.85f) : new Color(1f, 1f, 1f, 0.1f));
            var knob = new Rect(on ? sw.xMax - 20f : sw.x + 2f, sw.y + 2f, 18f, 18f);
            Ui.Pill(knob, on ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            Ui.Label(new Rect(sw.x - 44f, sw.y, 40f, sw.height), on ? "ON" : "OFF", 11, on ? Color.white : Ui.Muted, TextAnchor.MiddleRight, true);
            return canClick && GUI.Button(c, GUIContent.none, GUIStyle.none);
        }

        /// During a match: the active modifiers, in a line under the wave plate.
        void DrawActiveModifiers(float x, float y)
        {
            if (game.Mods == RunMod.None)
                return;
            foreach (var d in Modifiers.All)
            {
                if (!game.Has(d.Mod))
                    continue;
                float w = 14f + d.Name.Length * 6.6f;
                var chip = new Rect(x, y, w, 18f);
                var c = LeanColor(d.Lean);
                Ui.Pill(chip, new Color(0f, 0f, 0f, 0.5f));
                Ui.Label(chip, d.Name.ToUpperInvariant(), 9, c, TextAnchor.MiddleCenter, true);
                x += w + 4f;
            }
        }
    }
}

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

            // Four across (three while there were 12 or fewer), as tall as the panel allows.
            int cols = Modifiers.All.Length > 12 ? 4 : 3;
            int rows = (Modifiers.All.Length + cols - 1) / cols;
            const float gap = 8f;
            float ch = Mathf.Min(104f, (r.yMax - 12f - y) / rows - gap);
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
            // Name and the switch, then what it does.
            bool narrow = c.width < 220f;
            float swW = narrow ? 40f : 50f;
            Ui.Fancy(new Rect(c.x + 12f, c.y + 8f, c.width - swW - 30f, 24f), d.Name, narrow ? 14 : 16, Color.white, on ? Color.Lerp(ModColor, Color.white, 0.5f) : new Color(0.82f, 0.86f, 0.96f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(c.x + 12f, c.y + 32f, c.width - 22f, c.height - 56f), d.Desc, 11, on ? Ui.Text : Ui.Muted, TextAnchor.UpperLeft, false, true);
            // Harder / easier / risky tag, and the switch (ON / OFF beside it when there's room).
            string tag = d.Lean == ModLean.Harder ? "HARDER" : d.Lean == ModLean.Easier ? "EASIER" : "RISKY";
            Ui.Badge(new Rect(c.x + 12f, c.yMax - 21f, 64f, 16f), tag, new Color(lean.r * 0.7f, lean.g * 0.7f, lean.b * 0.7f));
            var sw = new Rect(c.xMax - swW - 12f, c.y + 10f, swW, narrow ? 20f : 22f);
            Ui.Pill(sw, on ? new Color(ModColor.r * 0.85f, ModColor.g * 0.85f, ModColor.b * 0.85f) : new Color(1f, 1f, 1f, 0.1f));
            float kn = sw.height - 4f;
            var knob = new Rect(on ? sw.xMax - kn - 2f : sw.x + 2f, sw.y + 2f, kn, kn);
            Ui.Pill(knob, on ? Color.white : new Color(1f, 1f, 1f, 0.45f));
            if (!narrow)
                Ui.Label(new Rect(sw.x - 44f, sw.y, 40f, sw.height), on ? "ON" : "OFF", 11, on ? Color.white : Ui.Muted, TextAnchor.MiddleRight, true);
            return canClick && Ui.HitArea(c);
        }

        /// During a match: the active modifiers, in a line under the wave plate. Hunted names who's hunted this
        /// wave; Second Wind shows whether yours is still there.
        void DrawActiveModifiers(float x, float y)  // x: after the difficulty's chip
        {
            if (game.Mods == RunMod.None)
                return;
            foreach (var d in Modifiers.All)
            {
                if (!game.Has(d.Mod))
                    continue;
                string text = d.Name;
                Player who;
                if (d.Mod == RunMod.Hunted && game.Players.TryGetValue(game.HuntedId, out who))
                    text = "Hunted: " + (who.Id == game.MyId ? "you" : who.Name);
                else if (d.Mod == RunMod.SecondWind && game.Players.TryGetValue(game.MyId, out who))
                    text = who.SecondWind ? "Second Wind ready" : "Second Wind used";
                float w = 14f + text.Length * 6.6f;
                var chip = new Rect(x, y, w, 18f);
                var c = LeanColor(d.Lean);
                Ui.Pill(chip, new Color(0f, 0f, 0f, 0.5f));
                Ui.Label(chip, text.ToUpperInvariant(), 9, c, TextAnchor.MiddleCenter, true);
                x += w + 4f;
            }
        }

        static readonly Rgba HuntRed = new Rgba(1f, 0.3f, 0.32f);
        static readonly Rgba WallBlue = new Rgba(0.55f, 0.65f, 0.9f);

        /// Hunted: four brackets turning round the quarry, and a faint pulse.
        void DrawHuntedMark(V2 at, float r, float t)
        {
            float rr = r + 12f + 2f * Mathf.Sin(t * 6f);
            Arc(at, rr + 6f, 1.5f, HuntRed.WithAlpha(0.18f + 0.12f * Mathf.Sin(t * 6f)), 28);
            for (int k = 0; k < 4; k++)
                Arc(at, rr, 3f, HuntRed.WithAlpha(0.9f), 6, t * 1.5f + k * MathX.Pi / 2f - 0.35f, 0.7f);
        }

        /// Solid Walls: the arena's edges drawn as walls (a bright inner edge with a glow falling off inward).
        void DrawSolidWalls(float t)
        {
            float a = 0.55f + 0.1f * Mathf.Sin(t * 1.5f);
            V2[] c = { new V2(0f, 0f), new V2(AW, 0f), new V2(AW, AH), new V2(0f, AH) };
            for (int k = 0; k < 4; k++)
            {
                V2 p0 = c[k], p1 = c[(k + 1) % 4];
                V2 inward = (new V2(AW / 2f, AH / 2f) - (p0 + p1) / 2f).Normalized();
                inward = Mathf.Abs(inward.X) > Mathf.Abs(inward.Y) ? new V2(inward.X > 0f ? 1f : -1f, 0f) : new V2(0f, inward.Y > 0f ? 1f : -1f);
                Line(p0, p1, 10f, WallBlue.WithAlpha(0.35f * a));
                Line(p0 + inward * 9f, p1 + inward * 9f, 8f, WallBlue.WithAlpha(0.1f * a));
                Line(p0 + inward * 3f, p1 + inward * 3f, 2f, new Rgba(0.85f, 0.9f, 1f, a));
            }
        }
    }
}

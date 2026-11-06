using System.Collections.Generic;
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
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            bool host = session.IsHost;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            int n = Modifiers.Count(session.Modifiers);
            Ui.Label(new Rect(x, y, w, 28f), n == 0 ? "None on: a normal run" : n + " on", 12, n == 0 ? Ui.Muted : ModColor, TextAnchor.MiddleLeft, true);
            if (n > 0 && host && Ui.Button(new Rect(x + w - 150f, y + 2f, 100f, 24f), "Turn all off"))
                foreach (var d in Modifiers.All)
                    session.SetModifier(d.Mod, false);
            Ui.Label(new Rect(x, y + 28f, w, 18f), host ? "Click a switch to turn it on or off. They last the whole run, for everyone (Practice Range too)."
                : "The host picks these. They last the whole run, for everyone.", 12, Ui.Muted);
            y += 54f;

            // Grouped by tier (Easier, Risky, Harder): each group starts on its own row under a heading that spans the panel.
            // Click a heading to fold its group away (remembered on this computer); folded, it says how many are on.
            // Cards are as tall as the panel allows (84 to 104); when the open groups don't fit, the list scrolls.
            const float gap = 4f, head = 22f, rowH = 42f;
            int rows = 0;
            foreach (var lean in LeanOrder)
                if (!ModTierFolded(lean))
                    rows += ModsIn(lean).Count;
            var area = new Rect(x - 4f, y, w + 8f, r.yMax - 10f - y);
            float content = LeanOrder.Length * head + rows * (rowH + gap);
            float maxScroll = Mathf.Max(0f, content - area.height);
            var ev = Event.current;
            if (ev.type == EventType.ScrollWheel && area.Contains(ev.mousePosition))
            {
                modsScroll += ev.delta.y * 24f;
                ev.Use();
            }
            modsScroll = Mathf.Clamp(modsScroll, 0f, maxScroll);
            if (maxScroll > 0f)
            {
                // A thin bar on the right: how much is showing, and where.
                float bh = area.height * area.height / content, by = area.y + (area.height - bh) * modsScroll / maxScroll;
                Ui.Pill(new Rect(r.xMax - 9f, by, 4f, bh), new Color(1f, 1f, 1f, 0.18f));
            }
            GUI.BeginGroup(area);
            x = 4f;
            y = -modsScroll;
            foreach (var lean in LeanOrder)
            {
                var mods = ModsIn(lean);
                bool folded = ModTierFolded(lean);
                // The heading: a fold marker, its tier, a few words, a line to the panel's edge; folded, how many are on.
                var lc = LeanColor(lean);
                var hr = new Rect(x, y, w, head - 4f);
                bool hover = hr.Contains(Event.current.mousePosition);
                if (hover)
                    Ui.Pill(hr, new Color(lc.r, lc.g, lc.b, 0.08f));
                Ui.Label(new Rect(x + 2f, y, 16f, head - 4f), folded ? "+" : "−", 14, lc, TextAnchor.MiddleCenter, true);
                string tag = LeanTag(lean), blurb = LeanBlurb(lean);
                float tx = x + 22f, tw = Ui.TextWidth(tag, 11, true) + 10f, bw = Ui.TextWidth(blurb, 11);
                Ui.Label(new Rect(tx, y, tw, head - 4f), tag, 11, lc, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(tx + tw, y, bw + 4f, head - 4f), blurb, 11, Ui.Muted, TextAnchor.MiddleLeft);
                int onCount = mods.FindAll(d => (session.Modifiers & d.Mod) != 0).Count;
                string count = folded ? mods.Count + " hidden" + (onCount > 0 ? "  ·  " + onCount + " on" : "") : onCount > 0 ? onCount + " on" : "";
                float cwid = count.Length > 0 ? Ui.TextWidth(count, 11, true) + 8f : 0f;
                if (count.Length > 0)
                    Ui.Label(new Rect(x + w - cwid, y, cwid, head - 4f), count, 11, onCount > 0 ? ModColor : Ui.Muted, TextAnchor.MiddleRight, true);
                float lx = tx + tw + bw + 12f, lend = x + w - cwid - 10f;
                if (lend - lx > 10f)
                    Ui.Fill(new Rect(lx, y + (head - 4f) / 2f, lend - lx, 1f), new Color(lc.r, lc.g, lc.b, 0.2f));
                if (Ui.HitArea(hr))
                    FoldModTier(lean, !folded);
                y += head;
                if (folded)
                    continue;
                for (int i = 0; i < mods.Count; i++)
                {
                    var d = mods[i];
                    var c = new Rect(x, y + i * (rowH + gap), w - (maxScroll > 0f ? 8f : 0f), rowH);
                    bool on = (session.Modifiers & d.Mod) != 0;
                    if (DrawModRow(c, d, on, host, t))
                        session.SetModifier(d.Mod, !on);
                }
                y += mods.Count * (rowH + gap);
            }
            GUI.EndGroup();
        }

        float modsScroll;  // the Modifiers tab's list, scrolled this far down

        static List<ModDef> ModsIn(ModLean lean) => new List<ModDef>(System.Array.FindAll(Modifiers.All, d => d.Lean == lean));

        /// Which tiers are folded away in the Modifiers tab (a bit per ModLean, kept in PlayerPrefs "mods_folded").
        int modsFolded = -1;

        bool ModTierFolded(ModLean lean)
        {
            if (modsFolded < 0)
                modsFolded = PlayerPrefs.GetInt("mods_folded", 0);
            return (modsFolded & (1 << (int)lean)) != 0;
        }

        void FoldModTier(ModLean lean, bool folded)
        {
            ModTierFolded(lean);
            modsFolded = folded ? modsFolded | (1 << (int)lean) : modsFolded & ~(1 << (int)lean);
            PlayerPrefs.SetInt("mods_folded", modsFolded);
            PlayerPrefs.Save();
        }

        static readonly ModLean[] LeanOrder = { ModLean.Easier, ModLean.Risky, ModLean.Harder };

        static string LeanTag(ModLean l) => l == ModLean.Harder ? "HARDER" : l == ModLean.Easier ? "EASIER" : "RISKY";

        static string LeanBlurb(ModLean l) => l == ModLean.Harder ? "a tougher run" : l == ModLean.Easier ? "a helping hand" : "a trade: something gained, something lost";

        /// One modifier as a row of the list: a digital toggle (an OFF | ON slide with an LED), the name, what it does.
        bool DrawModRow(Rect c, ModDef d, bool on, bool canClick, float t)
        {
            var lean = LeanColor(d.Lean);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            Ui.Fill(c, new Color(0.02f, 0.06f, 0.12f, on ? 0.75f : 0.5f));
            if (on)
                Ui.Fill(c, new Color(ModColor.r, ModColor.g, ModColor.b, 0.08f + 0.03f * Mathf.Sin(t * 3f)));
            else if (hover)
                Ui.Fill(c, new Color(1f, 1f, 1f, 0.05f));
            var edge = on ? new Color(ModColor.r, ModColor.g, ModColor.b, 0.7f) : new Color(lean.r, lean.g, lean.b, hover ? 0.5f : 0.22f);
            Ui.Fill(new Rect(c.x, c.y, c.width, 1f), edge);
            Ui.Fill(new Rect(c.x, c.yMax - 1f, c.width, 1f), edge);
            Ui.Fill(new Rect(c.x, c.y, 3f, c.height), new Color(lean.r, lean.g, lean.b, on ? 0.95f : 0.55f));  // its tier

            // The toggle: two segments, the lit one shows the state; a square LED over the lit half.
            var sw = new Rect(c.x + 16f, c.y + (c.height - 24f) / 2f, 76f, 24f);
            var line = on ? ModColor : new Color(0.45f, 0.55f, 0.7f, 0.6f);
            Ui.Fill(sw, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(sw.x, sw.y, sw.width, 1f), line);
            Ui.Fill(new Rect(sw.x, sw.yMax - 1f, sw.width, 1f), line);
            Ui.Fill(new Rect(sw.x, sw.y, 1f, sw.height), line);
            Ui.Fill(new Rect(sw.xMax - 1f, sw.y, 1f, sw.height), line);
            var half = new Rect(on ? sw.x + sw.width / 2f : sw.x + 2f, sw.y + 2f, sw.width / 2f - 2f, sw.height - 4f);
            Ui.Fill(half, on ? new Color(ModColor.r * 0.9f, ModColor.g * 0.9f, ModColor.b * 0.9f, 0.95f) : new Color(1f, 1f, 1f, 0.14f));
            Ui.Fill(new Rect(half.x, half.y, half.width, 1f), new Color(1f, 1f, 1f, on ? 0.5f : 0.2f));
            Ui.Label(new Rect(sw.x + 2f, sw.y, sw.width / 2f - 2f, sw.height), "OFF", 10, on ? Ui.Muted : Ui.Text, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(sw.x + sw.width / 2f, sw.y, sw.width / 2f - 2f, sw.height), "ON", 10, on ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
            var led = new Rect(sw.xMax + 8f, c.y + c.height / 2f - 3f, 6f, 6f);
            if (on)
                Ui.Fill(new Rect(led.x - 3f, led.y - 3f, 12f, 12f), new Color(ModColor.r, ModColor.g, ModColor.b, 0.25f + 0.1f * Mathf.Sin(t * 4f)));
            Ui.Fill(led, on ? Color.Lerp(ModColor, Color.white, 0.6f) : new Color(1f, 1f, 1f, 0.15f));

            float nx = led.xMax + 14f, nw = 170f;
            Ui.Glitch(new Rect(nx, c.y, nw, c.height), d.Name, 14, on ? Color.Lerp(ModColor, Color.white, 0.75f) : new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleLeft);
            float dx = nx + nw + 8f;
            Ui.Label(new Rect(dx, c.y + 2f, c.xMax - dx - 10f, c.height - 4f), d.Desc, 11, on ? Ui.Text : Ui.Muted, TextAnchor.MiddleLeft, false, true);
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

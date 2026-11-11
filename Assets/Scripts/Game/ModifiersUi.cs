using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The lobby's Modifiers tab: switches that change the whole run (Modifiers.cs). The host turns them on and
    /// off; everyone sees them. Active ones also show under the wave plate during the match.
    public partial class ArenaApp
    {
        static readonly Color ModLit = new Color(0.44f, 0.72f, 1f);  // a modifier that is on: the lobby's lit blue
        static readonly Color HarderColor = new Color(1f, 0.45f, 0.45f), EasierColor = new Color(0.45f, 1f, 0.55f), RiskyColor = new Color(1f, 0.78f, 0.35f);

        static Color LeanColor(ModLean l) => l == ModLean.Harder ? HarderColor : l == ModLean.Easier ? EasierColor : RiskyColor;

        void DrawModifiers(Rect r, float t)
        {
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            bool host = session.IsHost;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            int n = Modifiers.Count(session.Modifiers);
            // Terminal-style readout: a prompt, the status in caps, a blinking block cursor; the hint underneath is a "//" comment.
            string status = (n == 0 ? "NONE ON: A NORMAL RUN" : n.ToString("00") + " ON");
            var sc = n == 0 ? Ui.Muted : ModLit;
            Ui.Label(new Rect(x, y, 16f, 28f), ">", 12, ModLit, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 16f, y, w, 28f), status, 12, sc, TextAnchor.MiddleLeft, true);
            if (fxReduced || Mathf.Repeat(t * 1.6f, 1f) < 0.6f)
                Ui.Fill(new Rect(x + 16f + Ui.TextWidth(status, 12, true) + 5f, y + 8f, 7f, 12f), new Color(ModLit.r, ModLit.g, ModLit.b, 0.8f));
            if (n > 0 && host && DkKey(new Rect(x + w - 150f, y + 2f, 100f, 24f), "Turn all off", Ui.Neutral))
                foreach (var d in Modifiers.All)
                    session.SetModifier(d.Mod, false);
            Ui.Label(new Rect(x, y + 28f, w, 18f), "// " + (host ? "click a tile to turn it on or off. they last the whole run, for everyone (Practice Range too)."
                : "the host picks these. they last the whole run, for everyone."), 11, Ui.Muted);
            y += 54f;

            // Grouped by tier (Easier, Risky, Harder): each group starts on its own row under a heading that spans the panel.
            // Click a heading to fold its group away (remembered on this computer); folded, it says how many are on.
            // Under each heading the modifiers are tiles in two columns (icon, name, what it does, an ON/OFF key); when the open
            // groups don't fit the panel, the list scrolls. Docs/Mockups/modifiers-crt-icons.html (C1).
            const float gap = 8f, head = 22f, tileH = 112f;
            int rows = 0;
            foreach (var lean in LeanOrder)
                if (!ModTierFolded(lean))
                    rows += (ModsIn(lean).Count + 1) / 2;
            var area = new Rect(x - 4f, y, w + 8f, r.yMax - 10f - y);
            float content = LeanOrder.Length * head + rows * (tileH + gap);
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
                float hh = head - 4f;
                Ui.Fill(new Rect(x, y, 2f, hh), lc);  // a tier-coloured edge, like a console row
                Ui.Label(new Rect(x + 6f, y, 28f, hh), folded ? "[+]" : "[−]", 11, lc, TextAnchor.MiddleLeft, true);
                string tag = LeanTag(lean), blurb = "// " + LeanBlurb(lean);
                float tx = x + 38f, tw = Ui.TextWidth(tag, 11, true) + 14f, bw = Ui.TextWidth(blurb, 11);
                Ui.Fill(new Rect(tx, y + 2f, tw, hh - 4f), new Color(lc.r, lc.g, lc.b, 0.16f));  // the tag as a lit chip
                ModEdge(new Rect(tx, y + 2f, tw, hh - 4f), new Color(lc.r, lc.g, lc.b, 0.55f));
                Ui.Label(new Rect(tx, y, tw, hh), tag, 11, lc, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(tx + tw + 8f, y, bw + 4f, hh), blurb, 11, Ui.Muted, TextAnchor.MiddleLeft);
                int onCount = mods.FindAll(d => (session.Modifiers & d.Mod) != 0).Count;
                string count = folded ? "[" + mods.Count.ToString("00") + " HIDDEN" + (onCount > 0 ? " · " + onCount.ToString("00") + " ON" : "") + "]" : onCount > 0 ? "[" + onCount.ToString("00") + " ON]" : "";
                float cwid = count.Length > 0 ? Ui.TextWidth(count, 11, true) + 8f : 0f;
                if (count.Length > 0)
                    Ui.Label(new Rect(x + w - cwid, y, cwid, hh), count, 11, onCount > 0 ? ModLit : Ui.Muted, TextAnchor.MiddleRight, true);
                float lx = tx + tw + 8f + bw + 12f, lend = x + w - cwid - 10f;
                // A dashed rule of small segments with an end tick, instead of a solid hairline.
                float ly = y + hh / 2f;
                for (float sx = lx; sx + 4f <= lend; sx += 7f)
                    Ui.Fill(new Rect(sx, ly, 4f, 1f), new Color(lc.r, lc.g, lc.b, 0.3f));
                if (lend - lx > 10f)
                    Ui.Fill(new Rect(lend - 1f, ly - 3f, 1f, 7f), new Color(lc.r, lc.g, lc.b, 0.5f));
                if (Ui.HitArea(hr))
                    FoldModTier(lean, !folded);
                y += head;
                if (folded)
                    continue;
                float tw2 = (w - (maxScroll > 0f ? 8f : 0f) - gap) / 2f;
                for (int i = 0; i < mods.Count; i++)
                {
                    var d = mods[i];
                    var c = new Rect(x + (i % 2) * (tw2 + gap), y + (i / 2) * (tileH + gap), tw2, tileH);
                    bool on = (session.Modifiers & d.Mod) != 0;
                    if (DrawModTile(c, d, on, host, t))
                        session.SetModifier(d.Mod, !on);
                }
                y += (mods.Count + 1) / 2 * (tileH + gap);
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

        static string ModIconKey(RunMod m)
        {
            switch (m)
            {
                case RunMod.Lucky: return "mod_lucky";
                case RunMod.HeadStart: return "mod_headstart";
                case RunMod.Practiced: return "mod_practiced";
                case RunMod.GoldRush: return "mod_goldrush";
                case RunMod.SecondWind: return "mod_secondwind";
                case RunMod.GlassCannon: return "mod_glasscannon";
                case RunMod.Bloodlust: return "mod_bloodlust";
                case RunMod.Hunted: return "mod_hunted";
                case RunMod.ToughCrowd: return "mod_toughcrowd";
                case RunMod.Frenzy: return "mod_frenzy";
                case RunMod.SwiftFoes: return "mod_swiftfoes";
                case RunMod.Famine: return "mod_famine";
                case RunMod.TinyArena: return "mod_tinyarena";
                case RunMod.Nightfall: return "mod_nightfall";
                case RunMod.DeathThroes: return "mod_deaththroes";
                case RunMod.SolidWalls: return "mod_solidwalls";
                case RunMod.ZombieInfection: return "mod_zombie";
                case RunMod.Cluttered: return "mod_cluttered";
                case RunMod.SimpleArena: return "mod_simplearena";
                default: return null;
            }
        }

        static void ModEdge(Rect r, Color c)
        {
            Ui.Fill(new Rect(r.x, r.y, r.width, 1f), c);
            Ui.Fill(new Rect(r.x, r.yMax - 1f, r.width, 1f), c);
            Ui.Fill(new Rect(r.x, r.y, 1f, r.height), c);
            Ui.Fill(new Rect(r.xMax - 1f, r.y, 1f, r.height), c);
        }

        /// One modifier as a tile: an icon plate (its tier's colour, lit blue when on), the name, what it does, and an ON/OFF key
        /// along the bottom (the same key as READY?). The whole tile is the click target. Returns true on a click.
        bool DrawModTile(Rect c, ModDef d, bool on, bool canClick, float t)
        {
            var lean = LeanColor(d.Lean);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            Ui.Fill(c, on ? new Color(0.12f, 0.27f, 0.58f, 0.3f + (fxReduced ? 0f : 0.03f * Mathf.Sin(t * 3f))) : new Color(0.03f, 0.07f, 0.17f, hover ? 0.8f : 0.6f));
            ModEdge(c, on ? new Color(ModLit.r, ModLit.g, ModLit.b, 0.75f) : new Color(0.27f, 0.41f, 0.72f, hover ? 0.75f : 0.4f));
            Ui.Fill(new Rect(c.x, c.y, 28f, 2f), lean);  // its tier

            var plate = new Rect(c.x + 12f, c.y + 12f, 44f, 44f);
            var pc = on ? new Color(ModLit.r, ModLit.g, ModLit.b, 0.9f) : new Color(lean.r, lean.g, lean.b, 0.45f);
            Ui.Fill(plate, on ? new Color(0.16f, 0.35f, 0.74f, 0.4f) : new Color(0.01f, 0.03f, 0.09f, 0.7f));
            ModEdge(plate, pc);
            var icon = PixelArt.Get(ModIconKey(d.Mod));
            if (icon != null)
                Ui.Icon(new Rect(plate.x + 8f, plate.y + 8f, 28f, 28f), icon, on ? Color.white : lean);

            float tx = plate.xMax + 12f, tw = c.xMax - 12f - tx;
            Ui.Glitch(new Rect(tx, c.y + 10f, tw, 22f), d.Name, 16, on ? Color.Lerp(ModLit, Color.white, 0.75f) : new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(tx, c.y + 33f, tw, 44f), d.Desc, 11, on ? Ui.Text : Ui.Muted, TextAnchor.UpperLeft, false, true);

            // The key: shown, not clicked (the tile takes the click); a guest sees the host's choice, so it never goes hot.
            LobbyKit.Key(new Rect(c.x + 12f, c.yMax - 36f, c.width - 24f, 26f), on ? "ON" : "OFF", on, fxReduced, on ? PixelArt.Get("ui_check") : null,
                size: 15, minSize: 11, enabled: false, accent: on ? ModLit : (Color?)null);
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

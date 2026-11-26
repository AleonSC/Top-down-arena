using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The lobby's Modifiers tab: switches that change the whole run (Modifiers.cs). The host turns them on and
    /// off; everyone sees them. Active ones also show under the wave plate during the run.
    public partial class ArenaApp
    {
        static readonly Color ModLit = new Color(0.44f, 0.72f, 1f);  // a modifier that is on: the lobby's lit blue
        static readonly Color HarderColor = new Color(1f, 0.45f, 0.45f), EasierColor = new Color(0.45f, 1f, 0.55f), RiskyColor = new Color(1f, 0.78f, 0.35f);

        static Color LeanColor(ModLean l) => l == ModLean.Harder ? HarderColor : l == ModLean.Easier ? EasierColor : RiskyColor;

        void DrawModifiers(Rect r, float t)
        {
            LobbyKit.Panel(r);  // the lobby screen (LobbyKit.cs)
            bool host = session.IsHost, calm = fxReduced;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            int n = Modifiers.Count(session.Modifiers);
            // Terminal-style readout: a prompt, the status in caps, a blinking block cursor; the hint underneath is a "//" comment.
            string status = (n == 0 ? "NONE ON: A NORMAL RUN" : n.ToString("00") + " ON");
            var sc = n == 0 ? Ui.Muted : ModLit;
            Ui.Label(new Rect(x, y, 16f, 28f), ">", 12, ModLit, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 16f, y, w, 28f), status, 12, sc, TextAnchor.MiddleLeft, true);
            if (calm || Mathf.Repeat(t * 1.6f, 1f) < 0.6f)
                Ui.Fill(new Rect(x + 16f + Ui.TextWidth(status, 12, true) + 5f, y + 8f, 7f, 12f), ModLit.WithAlpha(0.8f));
            if (n > 0 && host && DkKey(new Rect(x + w - 110f, y + 2f, 110f, 24f), "Turn all off", Ui.Neutral))
                foreach (var d in Modifiers.All)
                    session.SetModifier(d.Mod, false);
            Ui.Label(new Rect(x, y + 28f, w, 18f), "// " + (host ? "click a card to turn it on or off. they last the whole run, for everyone."
                : "the host picks these. they last the whole run, for everyone."), 11, Ui.Muted);
            y += 46f;

            // One carousel row per tier (Easier, Risky, Harder), headed like the Loadout's rows (LoadoutCarousel.cs): three cards
            // in view, a bare arrow at each end, the wheel over the row. The Enabled box along the bottom holds what's on.
            float trayTop = r.yMax - 12f - ModTrayH - CarKeyY;
            float rowStep = (trayTop - 4f - y) / LeanOrder.Length, cardH = rowStep - CarKeyY - 8f;
            for (int k = 0; k < LeanOrder.Length; k++)
            {
                var lean = LeanOrder[k];
                var mods = ModsIn(lean);
                int onCount = mods.FindAll(d => (session.Modifiers & d.Mod) != 0).Count;
                LobbyKit.Heading(x, y, w, LeanTag(lean), LeanBlurb(lean) + "  ·  " + onCount + "/" + mods.Count + " on", calm, 15);
                int from, to;
                BeginCarousel(CarModEasier + k, new Rect(x, y + CarKeyY, w, cardH), mods.Count, ModsInView, -1, out from, out to);
                for (int j = from; j <= to; j++)
                {
                    var d = mods[CarIndex(j)];
                    bool on = (session.Modifiers & d.Mod) != 0;
                    bool inert = session.SurvivalMode && (Modifiers.NotInSurvival & d.Mod) != 0;
                    if (DrawModCard(CarItem(j), d, on, host, t, inert))
                        session.SetModifier(d.Mod, !on);
                }
                EndCarousel();
                y += rowStep;
            }
            DrawModTray(x, trayTop, w, host, calm);
        }

        const int ModsInView = 3;      // cards in view on each tier's row
        const float ModTrayH = 46f;    // the Enabled box under its heading: one line of keys

        /// The Enabled box: a key per modifier that's on (its tier's colour, its icon, its name), in the tiers' order. A click
        /// (host only) turns it off. When the names don't fit on one line, the keys show only their icons.
        void DrawModTray(float x, float y, float w, bool host, bool calm)
        {
            var on = new List<ModDef>();
            foreach (var lean in LeanOrder)
                foreach (var d in ModsIn(lean))
                    if ((session.Modifiers & d.Mod) != 0)
                        on.Add(d);
            LobbyKit.Heading(x, y, w, "Enabled modifiers", on.Count == 0 ? "none on" : on.Count + " on" + (host ? "  ·  click one to turn it off" : ""), calm, 15);
            var box = new Rect(x, y + CarKeyY, w, ModTrayH);
            DkSub(box);
            if (on.Count == 0)
            {
                Ui.Label(new Rect(box.x + 12f, box.y, box.width - 24f, box.height), "// none on: a normal run. pick from the rows above.", 11, Ui.Muted);
                return;
            }
            const float gap = 6f, pad = 8f;
            float kh = box.height - 2f * pad, full = -gap;
            foreach (var d in on)
                full += Ui.TextWidth(d.Name.ToUpperInvariant(), 12, true) + 50f + gap;
            bool iconsOnly = full > box.width - 2f * pad;
            float kx = box.x + pad;
            foreach (var d in on)
            {
                float kw = iconsOnly ? kh + 6f : Ui.TextWidth(d.Name.ToUpperInvariant(), 12, true) + 50f;
                var kr = new Rect(kx, box.y + pad, kw, kh);
                if (DkKey(kr, iconsOnly ? "" : d.Name.ToUpperInvariant(), true, calm, PixelArt.Get(ModIconKey(d.Mod)), colorIcon: true,
                    size: 12, minSize: 10, enabled: host, accent: LeanColor(d.Lean), rawSize: true) && host)
                    session.SetModifier(d.Mod, false);
                if (iconsOnly && kr.Contains(Event.current.mousePosition))
                {
                    TipStart(kr, d.Name, LeanColor(d.Lean));
                    TipAdd(d.Desc, 13, Ui.Text);
                }
                kx += kw + gap;
            }
        }

        static List<ModDef> ModsIn(ModLean lean) => new List<ModDef>(System.Array.FindAll(Modifiers.All, d => d.Lean == lean));

        static readonly ModLean[] LeanOrder = { ModLean.Easier, ModLean.Risky, ModLean.Harder };

        static string LeanTag(ModLean l) => l == ModLean.Harder ? "Harder" : l == ModLean.Easier ? "Easier" : "Risky";

        static string LeanBlurb(ModLean l) => l == ModLean.Harder ? "a tougher run" : l == ModLean.Easier ? "a helping hand" : "something gained, something lost";

        static string ModIconKey(RunMod m)
        {
            switch (m)
            {
                case RunMod.Lucky: return "mod_lucky";
                case RunMod.HeadStart: return "mod_headstart";
                case RunMod.Practiced: return "mod_practiced";
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
                case RunMod.ZombieInfection: return "mod_zombie";
                case RunMod.Cluttered: return "mod_cluttered";
                case RunMod.SimpleArena: return "mod_simplearena";
                default: return null;
            }
        }

        /// One modifier as a carousel card: its tier's colour along the top, an icon plate and the name, what it does across the
        /// card, and an ON/OFF key along the bottom (shown, not clicked: the card takes the click). Returns true on a click.
        bool DrawModCard(Rect c, ModDef d, bool on, bool canClick, float t, bool inert = false)  // inert: does nothing in this mode (Modifiers.NotInSurvival)
        {
            var lean = LeanColor(d.Lean);
            bool hover = canClick && c.Contains(Event.current.mousePosition);
            Ui.Fill(c, on ? new Color(0.12f, 0.27f, 0.58f, 0.3f + (fxReduced ? 0f : 0.03f * Mathf.Sin(t * 3f))) : new Color(0.03f, 0.07f, 0.17f, hover ? 0.8f : 0.6f));
            CrtKit.Frame(c, 1f, on ? ModLit.WithAlpha(0.75f) : new Color(0.27f, 0.41f, 0.72f, hover ? 0.75f : 0.4f));
            Ui.Fill(new Rect(c.x, c.y, c.width, 2f), lean.WithAlpha(on ? 1f : 0.7f));  // its tier

            var plate = new Rect(c.x + 10f, c.y + 10f, 34f, 34f);
            Ui.Fill(plate, on ? new Color(0.16f, 0.35f, 0.74f, 0.4f) : new Color(0.01f, 0.03f, 0.09f, 0.7f));
            CrtKit.Frame(plate, 1f, on ? ModLit.WithAlpha(0.9f) : lean.WithAlpha(0.45f));
            var icon = PixelArt.Get(ModIconKey(d.Mod));
            if (icon != null)
                Ui.Icon(new Rect(plate.x + 6f, plate.y + 6f, 22f, 22f), icon, on ? Color.white : lean);
            float tx = plate.xMax + 10f;
            Ui.Glitch(new Rect(tx, plate.y, c.xMax - 8f - tx, plate.height), d.Name, 15, on ? Color.Lerp(ModLit, Color.white, 0.75f) : new Color(0.9f, 0.93f, 1f), TextAnchor.MiddleLeft);
            float keyY = c.yMax - 32f;
            Ui.Label(new Rect(c.x + 10f, plate.yMax + 5f, c.width - 20f, keyY - plate.yMax - 7f), d.Desc, 11, on ? Ui.Text : Ui.Muted, TextAnchor.UpperLeft, false, true);
            DkKey(new Rect(c.x + 10f, keyY, c.width - 20f, 24f), inert ? (on ? "ON · " : "") + "NO EFFECT IN SURVIVAL" : on ? "ON" : "OFF", on, fxReduced,
                on && !inert ? PixelArt.Get("ui_check") : null, size: 14, minSize: 10, enabled: false, accent: on ? ModLit : (Color?)null);
            if (inert)
                Ui.Fill(c, new Color(0.01f, 0.02f, 0.06f, 0.55f));  // dimmed: Survival's room has nothing for it to act on
            return canClick && Ui.HitArea(c);
        }

        /// During a run: the active modifiers, in a line under the wave plate. Hunted names who's hunted this
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

        /// Hunted: four brackets turning round the quarry, and a faint pulse.
        void DrawHuntedMark(V2 at, float r, float t)
        {
            float rr = r + 12f + 2f * Mathf.Sin(t * 6f);
            Arc(at, rr + 6f, 1.5f, HuntRed.WithAlpha(0.18f + 0.12f * Mathf.Sin(t * 6f)), 28);
            for (int k = 0; k < 4; k++)
                Arc(at, rr, 3f, HuntRed.WithAlpha(0.9f), 6, t * 1.5f + k * MathX.Pi / 2f - 0.35f, 0.7f);
        }
    }
}

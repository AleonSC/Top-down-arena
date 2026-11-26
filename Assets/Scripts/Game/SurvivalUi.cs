using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        // Survival's HUD (Docs/Survival.md step 3): the clock plate, the level-up hold and the new-faction notice.
        const int SurvivalTab = 9;
        static readonly Color SurvivalColor = new Color(0.55f, 0.95f, 0.6f);
        static readonly string[] SurvivalRules = { "NO WAVES OR BOSSES", "LEVEL-UP PICKS FREEZE THE CLOCK", "NO PATHS OR SHOPS", "NO LIVES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" };
        int survPhaseSeen = -1;
        float survPhaseAt = -99f;

        /// The wave plate's place in Survival: SURVIVE, the clock, enemies left, the score, and a bar to the next faction.
        void DrawSurvivalPlate(Rect r)
        {
            Color ink = CrtStyle.Ink, dim = CrtStyle.Dim;
            HudCrt(new Rect(r.x + 14f, r.y + 5f, 150f, 16f), "SURVIVE", 17, dim);
            string clock = Game.SurvClock(game.SurvTime);
            HudCrt(new Rect(r.x + 12f, r.y + 14f, 150f, 46f), clock, 46, ink);
            HudCrt(new Rect(r.xMax - 110f, r.y + 5f, 100f, 16f), "LEFT", 17, dim, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 110f, r.y + 16f, 100f, 30f), game.Remaining.ToString(), 32, ink, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 130f, r.y + 44f, 120f, 14f), "SCORE " + game.Score.ToString("N0"), 17, CrtStyle.Gold, TextAnchor.MiddleRight);
            // The bar fills towards the next faction (full once all have joined).
            float progress = game.SurvPhase >= Survival.FactionsAt(99999f).Count - 1 ? 1f : (game.SurvTime % Survival.PhaseLength) / Survival.PhaseLength;
            var bar = new Rect(r.x + 14f, r.yMax - 9f, r.width - 28f, 5f);
            Ui.Fill(bar, new Color(1f, 1f, 1f, 0.1f));
            for (float sx = 0f; sx < bar.width * progress; sx += 8f)
                Ui.Fill(new Rect(bar.x + sx, bar.y, Mathf.Min(6f, bar.width * progress - sx), bar.height), ink);
        }

        /// The match is held while someone picks a level-up card (your own cards draw instead of this): a plate near the top.
        void DrawSurvivalPick(List<string> choosing)
        {
            string sub = choosing.Count > 0 ? "WAITING FOR " + string.Join(" AND ", choosing.ToArray()).ToUpperInvariant() + " TO CHOOSE" : "CHOOSE AN UPGRADE";
            var box = new Rect(W / 2f - 190f, 96f, 380f, 54f);
            Ui.Pill(box, new Color(0f, 0f, 0f, 0.55f));
            Ui.Label(new Rect(box.x, box.y + 6f, box.width, 18f), "LEVEL UP  ·  CLOCK HELD", 12, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(box.x, box.y + 24f, box.width, 24f), sub, 15, Color.white, TextAnchor.MiddleCenter, true);
        }

        /// A new faction joins: a plate near the top for a few seconds (and the first frame of a run says nothing).
        void DrawSurvivalPhase()
        {
            int ph = game.SurvPhase;
            if (survPhaseSeen < 0 || ph < survPhaseSeen || game.SurvTime < 1f)
                survPhaseSeen = ph;
            else if (ph > survPhaseSeen)
            {
                survPhaseSeen = ph;
                survPhaseAt = Time.unscaledTime;
            }
            float age = Time.unscaledTime - survPhaseAt;
            if (age < 0f || age > 5f)
                return;
            var fs = Survival.FactionsAt(game.SurvTime);
            var f = fs[fs.Count - 1];
            float a = Mathf.Clamp01(Mathf.Min(age / 0.3f, (5f - age) / 0.6f));
            var box = new Rect(W / 2f - 190f, 96f, 380f, 54f);
            Ui.Pill(box, new Color(0f, 0f, 0f, 0.55f * a));
            Ui.Label(new Rect(box.x, box.y + 6f, box.width, 18f), "NEW ENEMIES", 12, CrtStyle.Gold.WithAlpha(a), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(box.x, box.y + 24f, box.width, 24f), SurvFactionName(f) + " JOINS THE FIGHT", 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
        }

        /// "THE WARDEN'S FACTION", "TWIN REAPERS' FACTION".
        static string SurvFactionName(FactionDef f)
        {
            string n = BossName(f.Boss).ToUpperInvariant();
            return n + (n.EndsWith("S") ? "'" : "'S") + " FACTION";
        }

        /// The lobby's Survival tab (Docs/Survival.md step 4), in the Boss Hunt tab's layout (ModeTabKit.cs): the factions in the
        /// order they join as a list, a scope with the pressure climbing over the clock (the hovered or furthest faction's
        /// stretch lit, your best time marked), your records and the rules.

        void DrawSurvivalTab(Rect r, float t) => DrawModePage(r, t, SurvivalPage());

        /// The page (ModePageUi.cs): the factions in the order they join, the pressure graph, your records and the rules.
        ModePage SurvivalPage()
        {
            var profile = game.Profile ?? new Profile();
            var all = Survival.FactionsAt(99999f);
            int n = all.Count, best = profile.SurvivalBestPhase;
            var p = new ModePage { Title = "SURVIVAL", Acc = SurvivalColor, Rules = SurvivalRules,
                Tagline = "Pressure climbs with the clock until everyone is down.", ListHead = "FACTIONS", ListCap = "one joins every " + (int)(Survival.PhaseLength / 60f) + " min",
                Pct = Mathf.Clamp01(profile.SurvivalBestTime / 960f), Ring = ModeClock(profile.SurvivalBestTime),
                Note = "Two and three players face 1.5x and 2x the enemies." };
            for (int i = 0; i < n; i++)
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = i == 0 ? "The basics" : BossName(all[i].Boss), Known = true, Lit = true,
                    Model = i == 0 ? EnemyType.Grunt : all[i].Boss, Chip = Game.SurvClock(i * Survival.PhaseLength), ChipCol = i < best ? SurvivalColor : CrtStyle.Ink.WithAlpha(0.55f) });
            p.Default = Mathf.Clamp(best - 1, 0, n - 1);
            p.ScopeDraw = (g, shown) => DrawSurvivalGraph(g, n, shown, profile.SurvivalBestTime);
            LobbyEntry mine;
            session.Lobby.TryGetValue(session.MyId, out mine);
            float classBest = mine != null ? profile.SurvivalBest(Arena.Profile.ClassKey(mine.Role, mine.Kit)) : 0f;
            p.Focus = i => new ModeFocus { Eyebrow = "PRESSURE", ScopeL = "PRESSURE", ScopeR = profile.SurvivalBestTime > 0f ? "BEST " + Game.SurvClock(profile.SurvivalBestTime) : "NO RUNS YET",
                Name = SurvRowTitle(all, i), Copy = i == 0 ? "From the first second: the basic enemies, singles topping the field up and a squad every 45 s."
                    : "Joins at " + Game.SurvClock(i * Survival.PhaseLength) + ": " + SurvOwn(all[i]) + " own enemies come in with the rest, and the pressure keeps climbing.",
                RecVals = new[] { profile.SurvivalPlays.ToString(), ModeClock(profile.SurvivalBestTime), ModeClock(classBest), best > 0 ? best.ToString() : "-" },
                RecLabels = new[] { "PLAYED", "BEST TIME", "AS THIS CLASS", "BEST PHASE" } };
            return p;
        }
        static string SurvOwn(FactionDef f) => BossName(f.Boss) + (BossName(f.Boss).EndsWith("s") ? "'" : "'s");

        static string SurvRowTitle(System.Collections.Generic.List<FactionDef> all, int i) => i == 0 ? "THE BASIC FACTION" : SurvFactionName(all[i]);

        /// The scope's graph: the threat budget (one player) from 0:00 to a phase past the last faction, a tick and time at each
        /// faction, the `shown` faction's stretch lit and a gold line at your best time.
        static void DrawSurvivalGraph(Rect g, int phases, int shown, float bestTime)
        {
            float end = phases * Survival.PhaseLength, top = Survival.Pressure(end) * 1.08f;
            var ph = PhosGreen;
            Ui.Fill(new Rect(g.x, g.yMax, g.width, 1f), ph.WithAlpha(0.35f));
            float sx0 = g.x + g.width * shown / phases;
            Ui.Fill(new Rect(sx0, g.y, g.width / phases, g.height), ph.WithAlpha(0.07f));
            for (int i = 0; i <= phases; i++)
            {
                float px = g.x + g.width * i / phases;
                for (float dy = g.y; dy < g.yMax; dy += 6f)
                    Ui.Fill(new Rect(px, dy, 1f, 3f), ph.WithAlpha(0.18f));
                if (i < phases)
                    Ui.Label(new Rect(px + 3f, g.y - 2f, 40f, 12f), Game.SurvClock(i * Survival.PhaseLength), 9, ph.WithAlpha(i == shown ? 0.95f : 0.5f), TextAnchor.MiddleLeft);
            }
            float prevY = 0f;
            for (int k = 0; k <= 160; k++)
            {
                float s = end * k / 160f;
                float gx = g.x + g.width * k / 160f, gy = g.yMax - g.height * Survival.Pressure(s) / top;
                bool lit = s >= shown * Survival.PhaseLength && s <= (shown + 1) * Survival.PhaseLength;
                var c = ph.WithAlpha(lit ? 1f : 0.55f);
                if (k > 0)
                {
                    float y0 = Mathf.Min(prevY, gy), h = Mathf.Max(2f, Mathf.Abs(prevY - gy) + 1f);
                    Ui.Fill(new Rect(gx - 1f, y0, 2f, h), c);
                }
                prevY = gy;
            }
            if (bestTime > 0f)
            {
                float bx = g.x + g.width * Mathf.Min(1f, bestTime / end);
                Ui.Fill(new Rect(bx, g.y - 4f, 1f, g.height + 4f), CrtStyle.Gold.WithAlpha(0.85f));
            }
        }
    }
}

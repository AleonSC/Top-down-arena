using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        // Survival's HUD (Docs/Survival.md step 3): the clock plate, the level-up hold and the new-faction notice.
        const int SurvivalTab = 9;
        static readonly Color SurvivalColor = new Color(0.55f, 0.95f, 0.6f);
        static readonly string[] SurvivalRules = { "NO WAVES, NO BOSSES", "LEVEL-UP PICKS FREEZE THE CLOCK", "NO PATHS, NO SHOPS", "NO LIVES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" };
        int survPhaseSeen = -1;
        float survPhaseAt = -99f;

        /// The wave plate's place in Survival: SURVIVE, the clock, enemies left, the score, and a bar to the next faction.
        void DrawSurvivalPlate(Rect r)
        {
            Color ink = CrtUi.Ink, dim = CrtUi.Dim;
            HudCrt(new Rect(r.x + 14f, r.y + 5f, 150f, 16f), "SURVIVE", 17, dim);
            string clock = Game.SurvClock(game.SurvTime);
            HudCrt(new Rect(r.x + 12f, r.y + 14f, 150f, 46f), clock, 46, ink);
            HudCrt(new Rect(r.xMax - 110f, r.y + 5f, 100f, 16f), "LEFT", 17, dim, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 110f, r.y + 16f, 100f, 30f), game.Remaining.ToString(), 32, ink, TextAnchor.MiddleRight);
            HudCrt(new Rect(r.xMax - 130f, r.y + 44f, 120f, 14f), "SCORE " + game.Score.ToString("N0"), 17, Ui.Gold, TextAnchor.MiddleRight);
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
            Ui.Label(new Rect(box.x, box.y + 6f, box.width, 18f), "LEVEL UP  ·  CLOCK HELD", 12, Ui.Gold, TextAnchor.MiddleCenter, true);
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
            Ui.Label(new Rect(box.x, box.y + 6f, box.width, 18f), "NEW ENEMIES", 12, new Color(1f, 0.8f, 0.35f, a), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(box.x, box.y + 24f, box.width, 24f), SurvFactionName(f) + " JOINS THE FIGHT", 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
        }

        /// "THE WARDEN'S FACTION", "TWIN REAPERS' FACTION".
        static string SurvFactionName(FactionDef f)
        {
            string n = BossName(f.Boss).ToUpperInvariant();
            return n + (n.EndsWith("S") ? "'" : "'S") + " FACTION";
        }

        /// The lobby's Survival tab (Docs/Survival.md step 4): what the mode is, when each faction joins, and the rules.
        void DrawSurvivalTab(Rect r, float t)
        {
            DkPanel(r);
            var ink = CrtUi.Ink;
            var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            DkTitle(new Rect(x, y - 2f, 200f, 24f), "SURVIVAL", 18);
            Ui.Label(new Rect(x + w - 330f, y, 330f, 20f), "ONE CLOCK  ·  NO WAVES  ·  LAST AS LONG AS YOU CAN", 11, Ui.Muted, TextAnchor.MiddleRight);
            Ui.Fill(new Rect(x + 150f, y + 10f, w - 520f, 1f), new Color(ink.r, ink.g, ink.b, 0.25f));
            y += 26f;
            Ui.Label(new Rect(x, y, w, 18f), "Enemies keep coming and the pressure climbs with the clock. The run ends when everyone is down.", 12, Ui.Muted);
            y += 30f;

            // Left: the clock's ladder: when each faction joins.
            float colW = Mathf.Floor((w - 22f) / 2f);
            DkTitle(new Rect(x, y - 2f, colW, 18f), "THE CLOCK", 12, dim);
            var all = Survival.FactionsAt(99999f);
            float rowH = Mathf.Min(44f, (r.yMax - 20f - (y + 24f)) / all.Count - 6f);
            for (int i = 0; i < all.Count; i++)
            {
                var row = new Rect(x, y + 24f + i * (rowH + 6f), colW, rowH);
                DkSub(row);
                string when = i == 0 ? "0:00" : Game.SurvClock(i * Survival.PhaseLength);
                string who = i == 0 ? "THE BASIC FACTION" : SurvFactionName(all[i]);
                DkTitle(new Rect(row.x + 10f, row.y + 2f, 70f, rowH - 4f), when, 16, SurvivalColor);
                Ui.Label(new Rect(row.x + 90f, row.y, row.width - 100f, row.height), who, 12, Color.white, TextAnchor.MiddleLeft, true);
            }

            // Right: the rules, then the party-size note.
            float x2 = x + colW + 22f;
            DkTitle(new Rect(x2, y - 2f, colW, 18f), "THE RULES", 12, dim);
            float yr = y + 24f;
            foreach (var rule in SurvivalRules)
            {
                var row = new Rect(x2, yr, colW, 26f);
                DkSub(row);
                Ui.Label(new Rect(row.x + 10f, row.y, row.width - 20f, row.height), rule, 11, Ui.Text, TextAnchor.MiddleLeft, true);
                yr += 32f;
            }
            yr += 6f;
            Ui.Label(new Rect(x2, yr, colW, 16f), "Two and three players face 1.5x and 2x the enemies.", 11, Ui.Muted);

            // Your records: plays, the longest run, the longest with this class, the furthest faction.
            var profile = game.Profile ?? new Profile();
            LobbyEntry mine;
            session.Lobby.TryGetValue(session.MyId, out mine);
            float classBest = mine != null ? profile.SurvivalBest(Arena.Profile.ClassKey(mine.Role, mine.Kit)) : 0f;
            yr += 28f;
            DkTitle(new Rect(x2, yr - 2f, colW, 18f), "YOUR RECORDS", 12, dim);
            yr += 22f;
            float tw = Mathf.Floor((colW - 3f * 8f) / 4f), th = 50f;
            QuickRecord(new Rect(x2, yr, tw, th), profile.SurvivalPlays.ToString(), "PLAYED");
            QuickRecord(new Rect(x2 + (tw + 8f), yr, tw, th), profile.SurvivalBestTime > 0f ? Game.SurvClock(profile.SurvivalBestTime) : "-", "BEST TIME");
            QuickRecord(new Rect(x2 + 2f * (tw + 8f), yr, tw, th), classBest > 0f ? Game.SurvClock(classBest) : "-", "AS THIS CLASS");
            QuickRecord(new Rect(x2 + 3f * (tw + 8f), yr, tw, th), profile.SurvivalBestPhase > 0 ? profile.SurvivalBestPhase.ToString() : "-", "BEST PHASE");
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        // Survival's HUD (Docs/Survival.md step 3): the clock plate, the level-up hold and the new-faction notice.
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
            Ui.Label(new Rect(box.x, box.y + 24f, box.width, 24f), BossName(f.Boss).ToUpperInvariant() + "'S FACTION JOINS THE FIGHT", 15, new Color(1f, 1f, 1f, a), TextAnchor.MiddleCenter, true);
        }
    }
}

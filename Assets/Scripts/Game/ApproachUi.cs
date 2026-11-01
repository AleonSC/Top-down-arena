using UnityEngine;

namespace Arena
{
    /// Boss approaches on screen (Approaches.cs): a chip above the wave panel while a wave leads up to a boss, and a
    /// line in the between-waves banner as you set off and when the boss is near.
    public partial class ArenaApp
    {
        static readonly Color ApproachColor = new Color(1f, 0.72f, 0.4f);

        /// "APPROACH · THE WARDEN · 3/4" above the wave panel during an approach wave; returns the height it took.
        float DrawApproachChip(float x, float y)
        {
            if (game.State != MatchState.Fighting || game.ApproachShare(game.Wave) <= 0f)
                return 0f;
            string text = "APPROACH  ·  " + Defs.Get(Game.ApproachBoss(game.Wave)).Name.ToUpperInvariant() + "  ·  " + Game.ApproachStep(game.Wave) + "/4";
            float w = 26f + text.Length * 6.4f;
            var chip = new Rect(x, y, w, 20f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), ApproachColor);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, ApproachColor, TextAnchor.MiddleLeft, true);
            return 24f;
        }

        /// The banner's line before the next wave: setting off toward a boss, or that it's near.
        string ApproachLine(int next)
        {
            if (game.ApproachShare(next) <= 0f)
                return "";
            string boss = Defs.Get(Game.ApproachBoss(next)).Name;
            int step = Game.ApproachStep(next);
            return step == 4 ? boss + " is near  ·  " : step == 1 ? "On the road to " + boss + "  ·  " : "";
        }
    }
}

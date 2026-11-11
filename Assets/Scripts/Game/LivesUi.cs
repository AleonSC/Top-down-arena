using UnityEngine;

namespace Arena
{
    /// Lives on screen (Core/Checkpoints.cs): a chip over the wave panel with the lives left (a heart each, up to five).
    /// The Retry key and the Deathless line on the results screen are in HudSummary.cs, "CHECKPOINT SAVED" in
    /// WaveClearedUi.cs.
    public partial class ArenaApp
    {
        static readonly Color LifeRed = new Color(1f, 0.38f, 0.42f);

        /// DrawWaveCorner: the lives chip; false (nothing drawn) for a run without lives.
        bool DrawLivesChip(float x, float y)
        {
            int max = game.LivesMax;
            if (max <= 0)
                return false;
            int left = game.LivesLeft;
            string text = left == 1 ? "1 life" : left + " lives";
            float hearts = Mathf.Min(max, 5) * 13f;
            var chip = new Rect(x, y, 14f + hearts + 6f + Ui.TextWidth(text, 11, true) + 10f, 20f);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), LifeRed);
            var heart = PixelArt.Get("mod_secondwind");
            for (int i = 0; i < Mathf.Min(max, 5); i++)
                Ui.Icon(new Rect(chip.x + 12f + i * 13f, chip.y + 4f, 12f, 12f), heart, i < left ? LifeRed : new Color(1f, 1f, 1f, 0.18f));
            Ui.Label(new Rect(chip.x + 14f + hearts + 6f, chip.y, chip.width, chip.height), text, 11, left > 0 ? LifeRed : Ui.Muted, TextAnchor.MiddleLeft, true);
            return true;
        }
    }
}

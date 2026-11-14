using UnityEngine;

namespace Arena
{
    /// Lives on screen (Core/Checkpoints.cs): a chip over the wave panel with the lives left (a heart each, up to five), and
    /// the RESPAWN / GAME OVER choice when everyone goes down with a life left (before the results; RunEnding.cs holds the
    /// ending until it's made). The Deathless line on the results screen is in HudSummary.cs, "CHECKPOINT SAVED" in
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

        /// The respawn choice (game over with a life left): the hearts, the one a respawn uses blinking, then Respawn [Y] and
        /// Game over [N] for the host (the others wait for it). It fades in as the ending slows to its hold.
        void DrawLifePrompt()
        {
            float a = EndingOn ? Mathf.Clamp01((endT - 0.55f) / 0.4f) : 1f;
            if (a <= 0f)
                return;
            WithAlpha(a, () =>
            {
                bool solo = game.Players.Count == 1, host = session.IsHost;
                int max = Mathf.Min(game.LivesMax, 5), left = game.LivesLeft;
                var box = new Rect(W / 2f - 230f, H / 2f - 104f, 460f, 208f);
                DkPanel(box);
                DkTitle(new Rect(box.x, box.y + 18f, box.width, 34f), solo ? "YOU WENT DOWN" : "EVERYONE WENT DOWN", 28, LifeRed, TextAnchor.MiddleCenter);
                var heart = PixelArt.Get("mod_secondwind");
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.9f) < 0.55f;
                float hx = box.center.x - max * 30f / 2f;
                for (int i = 0; i < max; i++)
                {
                    Color c = i >= left ? new Color(1f, 1f, 1f, 0.15f) : i == left - 1 && !blink ? new Color(LifeRed.r, LifeRed.g, LifeRed.b, 0.35f) : LifeRed;
                    Ui.Icon(new Rect(hx + i * 30f + 3f, box.y + 64f, 24f, 24f), heart, c);
                }
                string after = left - 1 == 1 ? "1 life" : (left - 1) + " lives";
                Ui.Label(new Rect(box.x, box.y + 100f, box.width, 20f), "Respawn at the start of wave " + game.RetryWave + "  ·  " + after + " left after", 14, CrtStyle.Body, TextAnchor.MiddleCenter);
                float by = box.y + 140f, kw = 190f, gap = 20f;
                if (host)
                {
                    if (DkKey(new Rect(box.center.x - kw - gap / 2f, by, kw, 44f), "RESPAWN  [Y]", LifeRed, true, 16))
                        session.RetryRun();
                    if (DkKey(new Rect(box.center.x + gap / 2f, by, kw, 44f), "GAME OVER  [N]", Ui.Neutral, false, 16))
                        game.GiveUpRetry();
                }
                else
                    Ui.Label(new Rect(box.x, by, box.width, 44f), "The host is choosing: respawn or game over…", 15, Ui.Muted, TextAnchor.MiddleCenter, true);
            });
        }
    }
}

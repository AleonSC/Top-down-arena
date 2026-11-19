using UnityEngine;

namespace Arena
{
    /// The lobby's Campaign tab (Normal mode), in the Boss Hunt tab's layout (ModeTabKit.cs): the ten bosses of the 100-wave
    /// ladder as a list (named once you've reached or beaten them), a scope on the next one to beat (or the hovered row),
    /// its wave on the ladder and your Campaign records. The numbers come from `Profile`.
    public partial class ArenaApp
    {
        const int CampaignTab = 10;
        static readonly Color CampaignColor = new Color(0.45f, 0.85f, 1f);

        void DrawCampaignTab(Rect r, float t)
        {
            LobbyKit.Panel(r);
            var profile = game.Profile ?? new Profile();
            var ink = CrtStyle.Ink;
            float bodyY = ModeHeader(r, "CAMPAIGN", "100 WAVES  ·  A BOSS EVERY 10TH  ·  THEN ENDLESS",
                "One long run on a growing arena. Build as you go.", Ui.Muted);
            float x = r.x + 18f, w = r.width - 36f, bodyH = Mathf.Max(200f, r.yMax - 14f - bodyY);
            var ev = Event.current;

            // Left: the ladder's ten bosses, wave 10 to 100.
            const int n = 10;
            float step = Mathf.Min(50f, (bodyH - 8f) / n), rowH = step - 6f;
            var rows = new Rect[n];
            var bosses = new EnemyType[n];
            var known = new bool[n];
            int next = n - 1, hoverI = -1;
            for (int i = n - 1; i >= 0; i--)
            {
                bosses[i] = BossSchedule.Campaign.BossAt((i + 1) * 5);
                known[i] = profile.Beaten(bosses[i]) > 0 || profile.BestWave >= (i + 1) * 10;
                if (profile.Beaten(bosses[i]) == 0)
                    next = i;  // the first boss not beaten yet
                rows[i] = new Rect(x, bodyY + 8f + i * step, ModeListW, rowH);
                if (rows[i].Contains(ev.mousePosition))
                    hoverI = i;
            }
            int shown = hoverI >= 0 ? hoverI : next;
            for (int i = 0; i < n; i++)
            {
                int beaten = profile.Beaten(bosses[i]);
                string chip = beaten > 0 ? "BEATEN x" + beaten : known[i] ? "REACHED" : "LOCKED";
                Color cc = beaten > 0 ? CrtStyle.Gold : known[i] ? new Color(0.5f, 0.88f, 0.69f) : CrtStyle.Ink.WithAlpha(0.55f);
                ModeRow(rows[i], ((i + 1) * 10).ToString(), BossName(bosses[i]), known[i], known[i], i == shown, i == hoverI, CampaignColor, chip, cc);
            }

            // Right: the target's dossier.
            float x2 = x + ModeListW + ModeGap, w2 = w - ModeListW - ModeGap, y2 = bodyY;
            var sb = bosses[shown];
            int wave = (shown + 1) * 10, sBeaten = profile.Beaten(sb);
            var scope = new Rect(x2, y2, w2, Mathf.Min(220f, bodyH * 0.38f));
            ModeScope(scope, shown == next && sBeaten == 0 ? "NEXT TARGET" : "TARGET", "WAVE " + wave);
            if (!known[shown])
                CrtUi.Text(scope, "?", 72, PhosGreen.WithAlpha(0.7f), TextAnchor.MiddleCenter, true, FxCalm);
            y2 += scope.height + 12f;
            Ui.Glitch(new Rect(x2, y2, w2, 28f), known[shown] ? BossName(sb).ToUpperInvariant() : "???", 20, Color.white, TextAnchor.MiddleLeft);
            y2 += 32f;
            string copy = sBeaten > 0 ? BossName(sb) + " holds wave " + wave + " of the 100. Beaten " + sBeaten + (sBeaten == 1 ? " time." : " times.")
                : known[shown] ? "Wave " + wave + ": you've reached " + BossName(sb) + " but not beaten it yet."
                : "Wave " + wave + ": reach it in a Campaign run to meet this boss.";
            Ui.Label(new Rect(x2, y2, w2, 42f), copy, 13, known[shown] ? Ui.Text : Ui.Muted, TextAnchor.UpperLeft, false, true);
            y2 += 48f;
            float pw = Mathf.Min(40f, (w2 - 5f * (n - 1)) / n);
            for (int i = 0; i < n; i++)
            {
                bool beaten = profile.Beaten(bosses[i]) > 0;
                ModePip(new Rect(x2 + i * (pw + 5f), y2, pw, 20f), ((i + 1) * 10).ToString(), beaten || i == shown ? CampaignColor : ink.WithAlpha(1f), i == shown);
            }
            y2 += 30f;
            ModeTiles(x2, y2, w2, 46f, new[] { profile.RunsStarted.ToString(), profile.RunsWon.ToString(), profile.RunsLost.ToString(),
                profile.BestWave > 0 ? profile.BestWave.ToString() : "-", profile.BestEndlessWave > 0 ? profile.BestEndlessWave.ToString() : "-" },
                new[] { "RUNS", "WON", "LOST", "BEST WAVE", "BEST ENDLESS" });
            y2 += 54f;
            ModeTiles(x2, y2, w2, 46f, new[] { profile.BestScore.ToString("N0"), profile.Kills.ToString("N0"), profile.BossKills.ToString(),
                profile.Downs.ToString(), profile.Revives.ToString() },
                new[] { "BEST SCORE", "KILLS", "BOSSES DOWN", "TIMES DOWNED", "REVIVES" });

            if (BeginUiGl())
            {
                for (int i = 0; i < n; i++)
                    if (known[i])
                        ModeRowModel(rows[i], bosses[i], t, i == shown);
                if (known[shown])
                    ModeScopeModel(scope, sb, t);
                EndUiGl();
            }
            ModeScopeFinish(scope);
        }
    }
}

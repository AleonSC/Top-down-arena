using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The lobby's Campaign tab (Normal mode), in the Boss Hunt tab's layout (ModeTabKit.cs): the ten bosses of the 100-wave
    /// ladder as a list (every model shown, named once you've reached or beaten them), a compact scope on the next one to beat (or the hovered row),
    /// its wave on the ladder and your Campaign records. The numbers come from `Profile`.
    public partial class ArenaApp
    {
        const int CampaignTab = 10;
        static readonly Color CampaignColor = new Color(0.45f, 0.85f, 1f);


        void DrawCampaignTab(Rect r, float t) => DrawModePage(r, t, CampaignPage());

        /// The page (ModePageUi.cs): the ladder's ten bosses (named once reached or beaten), the next one in the scope, your records.
        ModePage CampaignPage()
        {
            var profile = game.Profile ?? new Profile();
            var p = new ModePage { Title = "CAMPAIGN", Acc = CampaignColor,
                Tagline = "One long run on a growing arena. Build as you go.", ListHead = "THE LADDER",
                Rules = new[] { "PATHS + SHOPS", "LIVES", "SAVING", "CHALLENGES", "MODIFIERS + DIFFICULTY APPLY" },
                ShowLocked = true, ScopeCompact = true };
            var bosses = new EnemyType[10];
            int beatenN = 0, next = 9;
            for (int i = 9; i >= 0; i--)
            {
                bosses[i] = BossSchedule.Campaign.BossAt((i + 1) * 5);
                if (profile.Beaten(bosses[i]) == 0) next = i;
            }
            for (int i = 0; i < 10; i++)
            {
                int bt = profile.Beaten(bosses[i]);
                bool known = bt > 0 || profile.BestWave >= (i + 1) * 10;
                if (bt > 0) beatenN++;
                p.Rows.Add(new ModeRowData { Num = ((i + 1) * 10).ToString(), Name = BossName(bosses[i]), Known = known, Lit = known, Model = bosses[i],
                    Chip = bt > 0 ? "BEATEN x" + bt : known ? "REACHED" : "LOCKED", ChipCol = bt > 0 ? CrtStyle.Gold : known ? new Color(0.5f, 0.88f, 0.69f) : CrtStyle.Ink.WithAlpha(0.55f) });
            }
            p.Pct = beatenN / 10f;
            p.Ring = beatenN + "/10";
            p.ListCap = beatenN + " of 10 beaten";
            p.Default = next;
            p.Focus = i => {
                var b = bosses[i];
                int wave = (i + 1) * 10, bt = profile.Beaten(b);
                bool known = p.Rows[i].Known;
                var f = new ModeFocus { Eyebrow = i == next && bt == 0 ? "NEXT TARGET" : "TARGET", ScopeL = i == next && bt == 0 ? "NEXT TARGET" : "TARGET", ScopeR = "WAVE " + wave,
                    Unknown = !known, Model = b, Name = known ? BossName(b).ToUpperInvariant() : "???",
                    Copy = bt > 0 ? BossName(b) + " holds wave " + wave + " of the 100. Beaten " + bt + (bt == 1 ? " time." : " times.")
                        : known ? "Wave " + wave + ": you've reached " + BossName(b) + " but not beaten it yet." : "Wave " + wave + ": reach it in a Campaign run to meet this boss.",
                    CopyCol = known ? Color.white : Ui.Muted,
                    RecVals = new[] { profile.RunsStarted.ToString(), profile.RunsWon.ToString(), profile.BestWave > 0 ? profile.BestWave.ToString() : "-",
                        profile.BestEndlessWave > 0 ? profile.BestEndlessWave.ToString() : "-", profile.BestScore.ToString("N0"), profile.Kills.ToString("N0"), profile.BossKills.ToString(), profile.Revives.ToString() },
                    RecLabels = new[] { "RUNS", "WON", "BEST WAVE", "BEST ENDLESS", "BEST SCORE", "KILLS", "BOSSES DOWN", "REVIVES" } };
                return f;
            };
            return p;
        }
    }
}

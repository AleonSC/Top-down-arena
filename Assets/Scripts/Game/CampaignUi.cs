using UnityEngine;

namespace Arena
{
    /// The lobby's Campaign tab (Normal mode): the 100-wave ladder with a boss after every 10th (lit once you've reached or
    /// beaten it) and your Campaign records. A first mock-up in the CRT widgets; the numbers come from `Profile`.
    public partial class ArenaApp
    {
        const int CampaignTab = 10;
        static readonly Color CampaignColor = new Color(0.45f, 0.85f, 1f);

        void CampaignRecord(Rect r, string value, string label, Color? col = null)
        {
            DkSub(r);
            DkTitle(new Rect(r.x + 10f, r.y + 2f, r.width - 20f, 26f), value, 18, col ?? CrtStyle.Gold);
            Ui.Label(new Rect(r.x + 10f, r.yMax - 18f, r.width - 20f, 14f), label, 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
        }

        void DrawCampaignTab(Rect r, float t)
        {
            DkPanel(r);
            var profile = game.Profile ?? new Profile();
            var ink = CrtUi.Ink;
            var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            DkTitle(new Rect(x, y - 2f, 200f, 24f), "CAMPAIGN", 18);
            Ui.Label(new Rect(x + w - 360f, y, 360f, 20f), "100 WAVES  ·  A BOSS EVERY 10TH  ·  THEN ENDLESS", 11, Ui.Muted, TextAnchor.MiddleRight);
            Ui.Fill(new Rect(x + 130f, y + 10f, w - 510f, 1f), new Color(ink.r, ink.g, ink.b, 0.25f));
            y += 26f;

            // The ladder: ten boss tiles, wave 10 to 100, in two rows of five.
            DkTitle(new Rect(x, y - 2f, w, 18f), "THE LADDER", 12, dim);
            y += 22f;
            var bosses = Game.BossCycle;
            const float gap = 8f;
            float tileW = (w - 4f * gap) / 5f, tileH = 92f;
            for (int i = 0; i < 10; i++)
            {
                int wave = (i + 1) * 10;
                var b = BossSchedule.Campaign.BossAt(wave / 2);
                bool beaten = profile.Beaten(b) > 0, reached = profile.BestWave >= wave;
                var c = new Rect(x + (i % 5) * (tileW + gap), y + (i / 5) * (tileH + gap), tileW, tileH);
                if (beaten)
                    DkBox(c, CampaignColor, 0.45f);
                else
                    DkSub(c);
                Color tc = reached || beaten ? Color.white : new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.8f);
                Ui.Label(new Rect(c.x + 10f, c.y + 8f, c.width - 20f, 14f), "WAVE " + wave, 9, beaten ? CampaignColor : CrtUi.Dim, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(c.x + 10f, c.y + 26f, c.width - 64f, 40f), reached || beaten ? BossName(b) : "???", 13, tc, TextAnchor.MiddleLeft, true, true);
                string tag = beaten ? "BEATEN x" + profile.Beaten(b) : reached ? "REACHED" : "LOCKED";
                Ui.Label(new Rect(c.x + 10f, c.yMax - 20f, c.width - 20f, 14f), tag, 9, beaten ? Ui.Gold : reached ? Ui.Text : CrtUi.Dim, TextAnchor.MiddleLeft, true);
            }
            y += 2f * (tileH + gap) + 6f;

            // Your records.
            DkTitle(new Rect(x, y - 2f, w, 18f), "YOUR CAMPAIGN", 12, dim);
            y += 22f;
            const int cols = 6;
            float tw = (w - (cols - 1) * gap) / cols, th = 46f;
            string[] vals = { profile.RunsStarted.ToString(), profile.RunsWon.ToString(), profile.RunsLost.ToString(),
                profile.BestWave > 0 ? profile.BestWave.ToString() : "-", profile.BestEndlessWave > 0 ? profile.BestEndlessWave.ToString() : "-",
                profile.BestScore.ToString("N0") };
            string[] labels = { "RUNS", "WON", "LOST", "BEST WAVE", "BEST ENDLESS", "BEST SCORE" };
            for (int i = 0; i < cols; i++)
                CampaignRecord(new Rect(x + i * (tw + gap), y, tw, th), vals[i], labels[i]);
            y += th + gap;
            string[] vals2 = { profile.Kills.ToString("N0"), profile.BossKills.ToString(), profile.Downs.ToString(), profile.Revives.ToString() };
            string[] labels2 = { "KILLS", "BOSSES DOWN", "TIMES DOWNED", "REVIVES" };
            float tw2 = (w - 3f * gap) / 4f;
            for (int i = 0; i < 4; i++)
                CampaignRecord(new Rect(x + i * (tw2 + gap), y, tw2, th), vals2[i], labels2[i], CampaignColor);

            // The models of the bosses you've met.
            if (BeginUiGl())
            {
                for (int i = 0; i < 10; i++)
                {
                    int wave = (i + 1) * 10;
                    var b = BossSchedule.Campaign.BossAt(wave / 2);
                    if (profile.Beaten(b) == 0 && profile.BestWave < wave)
                        continue;
                    float ty = r.y + 14f + 26f + 22f;
                    var c = new Rect(x + (i % 5) * (tileW + gap), ty + (i / 5) * (tileH + gap), tileW, tileH);
                    UiClip(new Rect(c.xMax - 56f, c.y + 22f, 52f, c.height - 44f));
                    DrawEnemyPreview(b, new V2(c.xMax - 30f, c.center.y), 46f, t, false);
                }
                EndUiGl();
            }
        }
    }
}

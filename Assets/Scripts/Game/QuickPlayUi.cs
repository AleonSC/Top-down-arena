using UnityEngine;

namespace Arena
{
    /// The lobby's Quick Play tab (Core/QuickPlay.cs, Docs/QuickPlay.md step 4): what the mode is, the boss pool (a boss you've
    /// never beaten is a "???" tile; your first Quick Play's Warden is marked FIRST), the wave ladder, the rules and your
    /// Quick Play records.
    public partial class ArenaApp
    {
        // (kept between frames: the tab is drawn every frame)
        static readonly string[] QuickRules = { "ROOM BUILDING", "NO PATHS", "NO DATA GATES", "NO LIVES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" };
        Rect[] quickTiles = new Rect[0];
        bool[] quickMet = new bool[0];

        const int QuickTab = 8;
        static readonly Color QuickColor = new Color(1f, 0.82f, 0.4f);

        /// One of your Quick Play records: a recessed tile, the number glowing, its name under it.
        void QuickRecord(Rect r, string value, string label)
        {
            DkSub(r);
            DkTitle(new Rect(r.x + 10f, r.y + 2f, r.width - 20f, 26f), value, 18, CrtStyle.Gold);
            Ui.Label(new Rect(r.x + 10f, r.yMax - 18f, r.width - 20f, 14f), label, 9, CrtStyle.Desc, TextAnchor.MiddleLeft, true);
        }

        void DrawQuickPlayTab(Rect r, float t)
        {
            DkPanel(r);
            var profile = game.Profile ?? new Profile();
            var ink = CrtUi.Ink;
            var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            DkTitle(new Rect(x, y - 2f, 200f, 24f), "QUICK PLAY", 18);
            Ui.Label(new Rect(x + w - 300f, y, 300f, 20f), "10 WAVES  ·  A RANDOM BOSS  ·  ENDLESS", 11, Ui.Muted, TextAnchor.MiddleRight);
            Ui.Fill(new Rect(x + 150f, y + 10f, w - 470f, 1f), new Color(ink.r, ink.g, ink.b, 0.25f));
            y += 24f;
            bool first = profile.QuickPlays == 0;
            Ui.Label(new Rect(x, y, w, 18f), first ? "Your first Quick Play is the tutorial, and it ends on the Warden."
                : "The boss is drawn at random: every boss comes up once before any repeats.", 12, first ? QuickColor : Ui.Muted);
            y += 28f;
            float bodyY = y, bodyH = Mathf.Max(200f, r.yMax - 14f - bodyY);

            // Left: the boss pool, two columns of five.
            const float tileW = 150f, gap = 8f;
            float tileH = Mathf.Clamp((bodyH - 22f - 4f * gap) / 5f, 52f, 96f);  // five rows down the panel
            float poolW = tileW * 2f + gap;
            DkTitle(new Rect(x, bodyY - 2f, poolW, 18f), "THE BOSS POOL", 12, dim);
            var bosses = Game.BossCycle;
            if (quickTiles.Length != bosses.Length)
            {
                quickTiles = new Rect[bosses.Length];
                quickMet = new bool[bosses.Length];
            }
            var tiles = quickTiles;
            var met = quickMet;
            for (int i = 0; i < bosses.Length; i++)
            {
                var b = bosses[i];
                var c = new Rect(x + (i % 2) * (tileW + gap), bodyY + 22f + (i / 2) * (tileH + gap), tileW, tileH);
                tiles[i] = c;
                met[i] = profile.Beaten(b) > 0;
                bool firstBoss = first && b == EnemyType.Boss;
                if (met[i] || firstBoss)
                    DkBox(c, firstBoss ? QuickColor : ink, firstBoss ? 1f : 0.45f);  // met (or the first run's Warden): a lit card
                else
                    DkSub(c);  // not met yet: a dark recess
                string name = met[i] || firstBoss ? BossName(b) : "???";
                Color tc = met[i] || firstBoss ? Color.white : new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.8f);
                Ui.Label(new Rect(c.x + 54f, c.y + 6f, c.width - 60f, c.height - 24f), name, 13, tc, TextAnchor.MiddleLeft, true, true);
                string tag = firstBoss ? "FIRST" : met[i] ? "BEATEN x" + profile.Beaten(b) : "NOT MET";
                Ui.Label(new Rect(c.x + 54f, c.yMax - 20f, c.width - 60f, 14f), tag, 9, firstBoss ? QuickColor : met[i] ? Ui.Gold : CrtUi.Dim, TextAnchor.MiddleLeft, true);
                if (!met[i] && !firstBoss)
                    CrtUi.Text(new Rect(c.x + 6f, c.y, 42f, c.height), "?", 30, new Color(CrtUi.Dim.r, CrtUi.Dim.g, CrtUi.Dim.b, 0.6f), TextAnchor.MiddleCenter, true, CabCalm);
            }

            // Right: the run's shape, the rules and the records.
            float x2 = x + poolW + 22f, w2 = w - poolW - 22f, y2 = bodyY;
            DkTitle(new Rect(x2, y2 - 2f, w2, 18f), "THE RUN", 12, dim);
            y2 += 22f;
            float pip = Mathf.Min(24f, (w2 - 12f * 5f - 46f - 70f) / 10f);
            float px = x2;
            for (int i = 1; i <= QuickPlay.Length + 2; i++)
            {
                bool boss = i == QuickPlay.Length + 1, endless = i == QuickPlay.Length + 2;
                var pr = new Rect(px, y2, boss ? 46f : endless ? 70f : pip, 22f);
                if (boss || endless)
                    DkTag(pr, boss ? "BOSS" : "ENDLESS", boss ? QuickColor : Ui.Gold);
                else
                {
                    DkSub(pr, new Color(ink.r, ink.g, ink.b, 0.08f));
                    Ui.Label(pr, i.ToString(), 9, Ui.Muted, TextAnchor.MiddleCenter);
                }
                px += pr.width + (i == QuickPlay.Length ? 8f : 4f);
            }
            y2 += 30f;
            Ui.Label(new Rect(x2, y2, w2, 52f), "Ten waves of the boss's own enemies (or the usual mix), then a random boss with about two minutes of health. " +
                "Beat it to win, then keep going: a new boss every ten waves.", 12, Ui.Text, TextAnchor.UpperLeft, false, true);
            y2 += 56f;
            float rx = x2;
            foreach (var rule in QuickRules)
            {
                float rw = LobbyKit.SpacedWidth(rule, 10, 1f) + 12f;
                if (rx + rw > x2 + w2) { rx = x2; y2 += 22f; }
                DkTag(new Rect(rx, y2, rw, 18f), rule, dim);
                rx += rw + 6f;
            }
            y2 += 32f;
            DkTitle(new Rect(x2, y2 - 2f, w2, 18f), "YOUR QUICK PLAYS", 12, dim);
            y2 += 22f;
            float tw = (w2 - 24f) / 4f, th = 46f;
            QuickRecord(new Rect(x2, y2, tw, th), profile.QuickPlays.ToString(), "PLAYED");
            QuickRecord(new Rect(x2 + (tw + 8f), y2, tw, th), profile.QuickWins.ToString(), "WON");
            QuickRecord(new Rect(x2 + 2f * (tw + 8f), y2, tw, th), profile.QuickBestWave > 0 ? profile.QuickBestWave.ToString() : "-", "BEST WAVE");
            QuickRecord(new Rect(x2 + 3f * (tw + 8f), y2, tw, th), profile.QuickBosses.ToString(), "BOSSES");

            // The models of the bosses you've met (and the Warden on a first run).
            if (BeginUiGl())
            {
                for (int i = 0; i < bosses.Length; i++)
                {
                    if (!met[i] && !(first && bosses[i] == EnemyType.Boss))
                        continue;
                    var c = tiles[i];
                    UiClip(new Rect(c.x + 4f, c.y + 3f, 46f, c.height - 6f));
                    DrawEnemyPreview(bosses[i], new V2(c.x + 27f, c.center.y), Mathf.Min(44f, c.height - 8f), t, false);
                }
                EndUiGl();
            }
        }
    }
}

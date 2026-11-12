using UnityEngine;

namespace Arena
{
    /// The lobby's Quick Play tab (Core/QuickPlay.cs, Docs/QuickPlay.md step 4): what the mode is, the boss pool (a boss you've
    /// never beaten is a "???" tile; your first Quick Play's Warden is marked FIRST), the wave ladder, the rules and your
    /// Quick Play records.
    public partial class ArenaApp
    {
        const int QuickTab = 8;
        static readonly Color QuickColor = new Color(1f, 0.82f, 0.4f);

        void DrawQuickPlayTab(Rect r, float t)
        {
            LobbyKit.Panel(r);
            var profile = game.Profile ?? new Profile();
            var ink = CrtUi.Ink;
            var dim = new Color(ink.r, ink.g, ink.b, 0.7f);
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 14f;
            LobbyKit.Spaced(new Rect(x, y, 200f, 20f), "QUICK PLAY", 14, new Color(ink.r, ink.g, ink.b, 0.9f), TextAnchor.MiddleLeft, 3.6f);
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
            LobbyKit.Spaced(new Rect(x, bodyY, poolW, 16f), "THE BOSS POOL", 11, dim, TextAnchor.MiddleLeft, 2f);
            var bosses = Game.BossCycle;
            var tiles = new Rect[bosses.Length];
            var met = new bool[bosses.Length];
            for (int i = 0; i < bosses.Length; i++)
            {
                var b = bosses[i];
                var c = new Rect(x + (i % 2) * (tileW + gap), bodyY + 22f + (i / 2) * (tileH + gap), tileW, tileH);
                tiles[i] = c;
                met[i] = profile.Beaten(b) > 0;
                bool firstBoss = first && b == EnemyType.Boss;
                if (met[i] || firstBoss)
                    CrtKit.SoftBox(c, firstBoss ? QuickColor : ink, firstBoss, false, false);
                else
                {
                    var d = CrtUi.Dim;
                    Ui.Fill(c, new Color(d.r, d.g, d.b, 0.015f));
                    CrtKit.Dashed(c, 1f, new Color(d.r, d.g, d.b, 0.35f));
                }
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
            LobbyKit.Spaced(new Rect(x2, y2, w2, 16f), "THE RUN", 11, dim, TextAnchor.MiddleLeft, 2f);
            y2 += 22f;
            float pip = Mathf.Min(24f, (w2 - 12f * 5f - 46f - 70f) / 10f);
            float px = x2;
            for (int i = 1; i <= QuickPlay.Length + 2; i++)
            {
                bool boss = i == QuickPlay.Length + 1, endless = i == QuickPlay.Length + 2;
                var pr = new Rect(px, y2, boss ? 46f : endless ? 70f : pip, 22f);
                var pc = boss ? QuickColor : endless ? Ui.Gold : ink;
                Ui.Fill(pr, new Color(pc.r, pc.g, pc.b, boss ? 0.3f : 0.12f));
                if (endless)
                    CrtKit.Dashed(pr, 1f, new Color(pc.r, pc.g, pc.b, 0.7f));
                else
                    HuntBox(pr, new Color(pc.r, pc.g, pc.b, 0.7f));
                Ui.Label(pr, boss ? "BOSS" : endless ? "ENDLESS" : i.ToString(), boss || endless ? 10 : 9, boss || endless ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, boss || endless);
                px += pr.width + (i == QuickPlay.Length ? 8f : 4f);
            }
            y2 += 30f;
            Ui.Label(new Rect(x2, y2, w2, 52f), "Ten waves of the boss's own enemies (or the usual mix), then a random boss with about two minutes of health. " +
                "Beat it to win, then keep going: a new boss every ten waves.", 12, Ui.Text, TextAnchor.UpperLeft, false, true);
            y2 += 56f;
            float rx = x2;
            foreach (var rule in new[] { "ROOM BUILDING", "NO PATHS", "NO DATA GATES", "NO LIVES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" })
            {
                float rw = LobbyKit.SpacedWidth(rule, 10, 1f) + 12f;
                if (rx + rw > x2 + w2) { rx = x2; y2 += 22f; }
                var rr = new Rect(rx, y2, rw, 18f);
                HuntBox(rr, new Color(ink.r, ink.g, ink.b, 0.3f));
                LobbyKit.Spaced(rr, rule, 10, dim, TextAnchor.MiddleCenter, 1f);
                rx += rw + 6f;
            }
            y2 += 32f;
            LobbyKit.Spaced(new Rect(x2, y2, w2, 16f), "YOUR QUICK PLAYS", 11, dim, TextAnchor.MiddleLeft, 2f);
            y2 += 22f;
            float tw = (w2 - 24f) / 4f, th = 46f;
            LobbyKit.Tile(new Rect(x2, y2, tw, th), profile.QuickPlays.ToString(), "PLAYED", false);
            LobbyKit.Tile(new Rect(x2 + (tw + 8f), y2, tw, th), profile.QuickWins.ToString(), "WON", false);
            LobbyKit.Tile(new Rect(x2 + 2f * (tw + 8f), y2, tw, th), profile.QuickBestWave > 0 ? profile.QuickBestWave.ToString() : "-", "BEST WAVE", false);
            LobbyKit.Tile(new Rect(x2 + 3f * (tw + 8f), y2, tw, th), profile.QuickBosses.ToString(), "BOSSES", false);

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

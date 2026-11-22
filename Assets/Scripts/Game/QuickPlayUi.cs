using UnityEngine;

namespace Arena
{
    /// The lobby's Quick Play tab (Core/QuickPlay.cs, Docs/QuickPlay.md step 4): what the mode is, the boss pool (a boss you've
    /// never beaten is a "???" tile; your first Quick Play's Warden is marked FIRST), the wave ladder, the rules and your
    /// Quick Play records.
    public partial class ArenaApp
    {
        static readonly string[] QuickRules = { "ROOM BUILDING", "NO PATHS", "NO DATA GATES", "NO LIVES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" };

        const int QuickTab = 8;
        static readonly Color QuickColor = new Color(1f, 0.82f, 0.4f);

        /// The Boss Hunt tab's layout (ModeTabKit.cs): the boss pool as a list on the left, a scope on the right that rolls
        /// through the bosses you've met (the hovered one while the mouse is on a row), then the run's ladder, records and rules.
        void DrawQuickPlayTab(Rect r, float t)
        {
            if (modeLook != 0 && DrawModePage(r, t, QuickPage(t))) return;
            LobbyKit.Panel(r);
            var profile = game.Profile ?? new Profile();
            var ink = CrtStyle.Ink;
            bool first = profile.QuickPlays == 0;
            int metN = 0;
            foreach (var mb in Game.BossCycle) if (profile.Beaten(mb) > 0) metN++;
            float bodyY = ModeHeader(r, "QUICK PLAY", "10 WAVES  ·  A RANDOM BOSS  ·  ENDLESS", first ? "Your first Quick Play is the tutorial, and it ends on the Warden."
                : "Every boss comes up once before any repeats.", first ? QuickColor : Ui.Muted, QuickColor, metN / (float)Game.BossCycle.Length, metN + "/" + Game.BossCycle.Length);
            float x = r.x + 18f, w = r.width - 36f, bodyH = Mathf.Max(200f, r.yMax - 14f - bodyY);
            var ev = Event.current;

            // Left: the boss pool.
            var bosses = Game.BossCycle;
            int n = bosses.Length;
            float step = Mathf.Min(50f, (bodyH - 8f) / n), rowH = step - 6f;
            var rows = new Rect[n];
            var known = new bool[n];
            var shownList = new System.Collections.Generic.List<int>();
            int hoverI = -1;
            for (int i = 0; i < n; i++)
            {
                rows[i] = new Rect(x, bodyY + 8f + i * step, ModeListW, rowH);
                known[i] = profile.Beaten(bosses[i]) > 0 || (first && bosses[i] == EnemyType.Warden);
                if (known[i])
                    shownList.Add(i);
                if (rows[i].Contains(ev.mousePosition) && known[i])
                    hoverI = i;
            }
            int shown = hoverI >= 0 ? hoverI : shownList.Count > 0 ? shownList[(int)(t / 2.5f) % shownList.Count] : -1;
            for (int i = 0; i < n; i++)
            {
                var b = bosses[i];
                bool firstBoss = first && b == EnemyType.Warden, met = profile.Beaten(b) > 0;
                string chip = firstBoss ? "FIRST" : met ? "BEATEN x" + profile.Beaten(b) : "NOT MET";
                Color cc = firstBoss ? QuickColor : met ? CrtStyle.Gold : CrtStyle.Ink.WithAlpha(0.55f);
                ModeRow(rows[i], (i + 1).ToString("00"), BossName(b), known[i], known[i], i == shown, i == hoverI, QuickColor, chip, cc);
            }

            // Right: the draw, then the run.
            float x2 = x + ModeListW + ModeGap, w2 = w - ModeListW - ModeGap, y2 = bodyY;
            var scope = new Rect(x2, y2, w2, Mathf.Min(220f, bodyH * 0.38f));
            ModeScope(scope, "BOSS DRAW", shownList.Count.ToString("00") + " / " + n.ToString("00") + " MET");
            if (shown < 0)
                CrtUi.Text(scope, "?", 72, PhosGreen.WithAlpha(0.7f), TextAnchor.MiddleCenter, true, FxCalm);
            y2 += scope.height + 12f;
            Ui.Glitch(new Rect(x2, y2, w2, 28f), "TEN WAVES, THEN A BOSS", 20, Color.white, TextAnchor.MiddleLeft);
            y2 += 32f;
            Ui.Label(new Rect(x2, y2, w2, 42f), "Ten waves of the boss's own enemies, then a random boss with about two minutes of health. Beat it, then keep going: a new boss every ten.", 13, Ui.Text, TextAnchor.UpperLeft, false, true);
            y2 += 48f;
            float px = x2;
            for (int i = 1; i <= QuickPlay.Length + 2; i++)
            {
                bool boss = i == QuickPlay.Length + 1, endless = i == QuickPlay.Length + 2;
                var pr = new Rect(px, y2, boss ? 34f : endless ? 62f : 22f, 20f);
                ModePip(pr, boss ? "BOSS" : endless ? "ENDLESS" : i.ToString(), boss ? QuickColor : endless ? CrtStyle.Gold : ink.WithAlpha(1f), boss || endless);
                px += pr.width + 5f;
            }
            y2 += 30f;
            ModeTiles(x2, y2, w2, 46f, new[] { profile.QuickPlays.ToString(), profile.QuickWins.ToString(),
                profile.QuickBestWave > 0 ? profile.QuickBestWave.ToString() : "-", profile.QuickBosses.ToString() },
                new[] { "PLAYED", "WON", "BEST WAVE", "BOSSES" });
            y2 += 56f;
            ModeRules(x2, y2, w2, QuickRules);

            if (BeginUiGl())
            {
                for (int i = 0; i < n; i++)
                    if (known[i])
                        ModeRowModel(rows[i], bosses[i], t, i == shown);
                if (shown >= 0)
                    ModeScopeModel(scope, bosses[shown], t);
                EndUiGl();
            }
            ModeScopeFinish(scope);
        }
    }
}

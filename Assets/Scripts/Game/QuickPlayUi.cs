using System.Collections.Generic;
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
        static readonly Color QuickColor = CrtStyle.Gold;

        /// The Boss Hunt tab's layout (ModeTabKit.cs): the boss pool as a list on the left, a scope on the right that rolls
        /// through the bosses you've met (the hovered one while the mouse is on a row), then the run's ladder, records and rules.

        void DrawQuickPlayTab(Rect r, float t) => DrawModePage(r, t, QuickPage(t));

        /// The page (ModePageUi.cs): the boss pool, a scope rolling through the bosses you've met, the run's ladder, records and rules.
        ModePage QuickPage(float t)
        {
            var profile = game.Profile ?? new Profile();
            bool first = profile.QuickPlays == 0;
            var bosses = Game.BossCycle;
            var p = new ModePage { Title = "QUICK PLAY", Acc = QuickColor, Rules = QuickRules,
                Tagline = first ? "Your first Quick Play is the tutorial, and it ends on the Warden." : "Every boss comes up once before any repeats." };
            var met = new List<int>();
            for (int i = 0; i < bosses.Length; i++)
            {
                var b = bosses[i];
                bool fb = first && b == EnemyType.Warden, m = profile.Beaten(b) > 0;
                if (fb || m) met.Add(i);
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = BossName(b), Known = fb || m, Lit = fb || m, Model = b,
                    Chip = fb ? "FIRST" : m ? "BEATEN x" + profile.Beaten(b) : "NOT MET", ChipCol = fb ? QuickColor : m ? CrtStyle.Gold : CrtStyle.Ink.WithAlpha(0.55f) });
            }
            p.Pct = met.Count / (float)bosses.Length;
            p.Ring = met.Count + "/" + bosses.Length;
            p.ListHead = "BOSS POOL";
            p.ListCap = met.Count + " of " + bosses.Length + " met";
            p.Default = met.Count > 0 ? met[(int)(t / 2.5f) % met.Count] : 0;
            p.Focus = i => new ModeFocus {
                Eyebrow = "BOSS DRAW", ScopeL = "BOSS DRAW", ScopeR = met.Count.ToString("00") + " / " + bosses.Length.ToString("00") + " MET",
                Unknown = !p.Rows[i].Known, Model = bosses[i], Name = p.Rows[i].Known ? BossName(bosses[i]).ToUpperInvariant() : "???",
                Copy = "Ten waves of the boss's own enemies, then the boss with about two minutes of health. Beat it, then keep going: a new boss every ten.",
                RecVals = new[] { profile.QuickPlays.ToString(), profile.QuickWins.ToString(), profile.QuickBestWave > 0 ? profile.QuickBestWave.ToString() : "-", profile.QuickBosses.ToString() },
                RecLabels = new[] { "PLAYED", "WON", "BEST WAVE", "BOSSES" } };
            return p;
        }
    }
}

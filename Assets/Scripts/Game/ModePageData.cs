using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// MOCKUP: each game mode's `ModePage` (ModePageUi.cs): the same facts its old tab shows, as one model.
    public partial class ArenaApp
    {
        static List<ModePip2> ModeLadder(int waves, Color acc, bool endless, int every = 1)
        {
            var l = new List<ModePip2>();
            for (int i = 1; i < waves; i++)
                l.Add(new ModePip2 { Label = every == 1 || i % every == 0 ? i.ToString() : "", Col = CrtStyle.Ink, W = every == 1 ? 22f : 12f });
            l.Add(new ModePip2 { Label = "BOSS", Col = acc, Strong = true, W = 36f });
            if (endless)
                l.Add(new ModePip2 { Label = "ENDLESS", Col = CrtStyle.Gold, Strong = true, W = 62f });
            return l;
        }

        ModePage QuickPage(float t)
        {
            var profile = game.Profile ?? new Profile();
            bool first = profile.QuickPlays == 0;
            var bosses = Game.BossCycle;
            var p = new ModePage { Title = "QUICK PLAY", Facts = "10 WAVES  ·  A RANDOM BOSS  ·  ENDLESS", Acc = QuickColor, Rules = QuickRules,
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
                Ladder = ModeLadder(QuickPlay.Length + 1, QuickColor, true),
                RecVals = new[] { profile.QuickPlays.ToString(), profile.QuickWins.ToString(), profile.QuickBestWave > 0 ? profile.QuickBestWave.ToString() : "-", profile.QuickBosses.ToString() },
                RecLabels = new[] { "PLAYED", "WON", "BEST WAVE", "BOSSES" } };
            return p;
        }

        ModePage CampaignPage()
        {
            var profile = game.Profile ?? new Profile();
            var p = new ModePage { Title = "CAMPAIGN", Facts = "100 WAVES  ·  A BOSS EVERY 10TH  ·  THEN ENDLESS", Acc = CampaignColor,
                Tagline = "One long run on a growing arena. Build as you go.", ListHead = "THE LADDER",
                Rules = new[] { "PATHS + SHOPS", "LIVES", "SAVING", "CHALLENGES", "MODIFIERS + DIFFICULTY APPLY" } };
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
                    CopyCol = known ? Color.white : Ui.Muted, LadderHead = "BOSSES",
                    RecVals = new[] { profile.RunsStarted.ToString(), profile.RunsWon.ToString(), profile.BestWave > 0 ? profile.BestWave.ToString() : "-",
                        profile.BestEndlessWave > 0 ? profile.BestEndlessWave.ToString() : "-", profile.BestScore.ToString("N0"), profile.Kills.ToString("N0"), profile.BossKills.ToString(), profile.Revives.ToString() },
                    RecLabels = new[] { "RUNS", "WON", "BEST WAVE", "BEST ENDLESS", "BEST SCORE", "KILLS", "BOSSES DOWN", "REVIVES" } };
                for (int k = 0; k < 10; k++)
                    f.Ladder.Add(new ModePip2 { Label = ((k + 1) * 10).ToString(), Col = profile.Beaten(bosses[k]) > 0 || k == i ? CampaignColor : CrtStyle.Ink, Strong = k == i, W = 34f });
                return f;
            };
            return p;
        }

        ModePage HuntPage()
        {
            var profile = game.Profile ?? new Profile();
            bool host = session.IsHost;
            var bosses = new EnemyType[10];
            int huntN = 0;
            var p = new ModePage { Title = "BOSS HUNT", Facts = "20-STEP MAP  ·  ONE BOSS", Acc = HuntColor, ListHead = "HUNTS",
                Tagline = host ? "Pick a boss. Beat one in a run to open its hunt." : "The host picks the boss (from the hunts they've opened).",
                Rules = new[] { "NO PATHS", "NO DATA GATES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" } };
            for (int i = 0; i < 10; i++)
            {
                var b = bosses[i] = BossSchedule.Campaign.BossAt((i + 1) * 5);
                var h = Hunts.Get(b);
                bool open = Hunts.Available(h, profile), known = HuntKnown(b, open, profile);
                int won = profile.HuntWon(b, true);
                if (profile.Beaten(b) > 0) huntN++;
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = BossName(b), Known = known, Lit = open, Model = b,
                    Chip = h == null ? "SOON" : !open ? "LOCKED" : won > 0 ? "WON x" + won : "OPEN",
                    ChipCol = h == null ? CrtStyle.Ink.WithAlpha(0.55f) : !open ? HuntColor : won > 0 ? CrtStyle.Gold : new Color(0.5f, 0.88f, 0.69f) });
            }
            p.Pct = huntN / 10f;
            p.Ring = huntN + "/10";
            p.ListCap = huntN + " of 10 open";
            p.Default = Mathf.Max(0, System.Array.IndexOf(bosses, session.HuntBoss));
            int steps = session.HuntLong ? Hunts.LongLength : Hunts.Length;
            p.Note = "Each step the party votes on the next node. An Elite gives a boss clue; step " + steps / 2 + " is a miniboss and saves the hunt.";
            p.Focus = i => {
                var b = bosses[i];
                var h = Hunts.Get(b);
                bool can = Hunts.Available(h, profile), known = p.Rows[i].Known;
                string name = known ? BossName(b) : "this boss";
                int played = profile.HuntPlayed(b, true);
                float best = profile.HuntBestTime(b, true);
                return new ModeFocus { Eyebrow = "TARGET LOCK", ScopeL = "TARGET LOCK", ScopeR = (i + 1).ToString("00") + " / 10", Unknown = !known, Model = b,
                    Name = !known ? "???" : (h != null ? h.Name : name).ToUpperInvariant(),
                    Copy = h == null ? (known ? name + " has no hunt yet." : "Unknown boss: meet it in a run to learn more.")
                        : !can ? (host ? "Locked: beat " + name + " in a run to open this hunt." : "Locked for the host.")
                        : "Steps 1-" + (steps - 1) + ": only " + name + "'s own enemies, tougher ones joining as you go. Step " + steps + ": " + name + ".",
                    CopyCol = can ? Color.white : h == null ? Ui.Muted : Ui.Warn, LadderHead = "THE MAP",
                    Ladder = h == null ? new List<ModePip2>() : ModeLadder(steps, HuntColor, false, 5),
                    RecVals = new[] { can ? profile.HuntWon(b, true) + " / " + played : "-", ModeClock(best), can ? "OPEN" : "LOCKED" },
                    RecLabels = new[] { "HUNTS WON", "BEST TIME", "STATUS" } };
            };
            return p;
        }

        ModePage SurvivalPage()
        {
            var profile = game.Profile ?? new Profile();
            var all = Survival.FactionsAt(99999f);
            int n = all.Count, best = profile.SurvivalBestPhase;
            var p = new ModePage { Title = "SURVIVAL", Facts = "ONE CLOCK  ·  NO WAVES  ·  LAST AS LONG AS YOU CAN", Acc = SurvivalColor, Rules = SurvivalRules,
                Tagline = "Pressure climbs with the clock until everyone is down.", ListHead = "FACTIONS", ListCap = "one joins every " + (int)(Survival.PhaseLength / 60f) + " min",
                Pct = Mathf.Clamp01(profile.SurvivalBestTime / 960f), Ring = ModeClock(profile.SurvivalBestTime),
                Note = "Two and three players face 1.5x and 2x the enemies." };
            for (int i = 0; i < n; i++)
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = i == 0 ? "The basics" : BossName(all[i].Boss), Known = true, Lit = true,
                    Model = i == 0 ? EnemyType.Grunt : all[i].Boss, Chip = Game.SurvClock(i * Survival.PhaseLength), ChipCol = i < best ? SurvivalColor : CrtStyle.Ink.WithAlpha(0.55f) });
            p.Default = Mathf.Clamp(best - 1, 0, n - 1);
            p.ScopeDraw = (g, shown) => DrawSurvivalGraph(g, n, shown, profile.SurvivalBestTime);
            LobbyEntry mine;
            session.Lobby.TryGetValue(session.MyId, out mine);
            float classBest = mine != null ? profile.SurvivalBest(Arena.Profile.ClassKey(mine.Role, mine.Kit)) : 0f;
            p.Focus = i => new ModeFocus { Eyebrow = "PRESSURE", ScopeL = "PRESSURE", ScopeR = profile.SurvivalBestTime > 0f ? "BEST " + Game.SurvClock(profile.SurvivalBestTime) : "NO RUNS YET",
                Name = SurvRowTitle(all, i), Copy = i == 0 ? "From the first second: the basic enemies, singles topping the field up and a squad every 45 s."
                    : "Joins at " + Game.SurvClock(i * Survival.PhaseLength) + ": " + SurvOwn(all[i]) + " own enemies come in with the rest, and the pressure keeps climbing.",
                RecVals = new[] { profile.SurvivalPlays.ToString(), ModeClock(profile.SurvivalBestTime), ModeClock(classBest), best > 0 ? best.ToString() : "-" },
                RecLabels = new[] { "PLAYED", "BEST TIME", "AS THIS CLASS", "BEST PHASE" } };
            return p;
        }
    }
}

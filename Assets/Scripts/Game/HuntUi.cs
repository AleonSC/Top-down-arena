using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Game Mode screen's Boss Hunt page (Hunts.cs): the ten bosses as a list, whether each hunt is open (beaten in a run),
    /// locked or still to come, the picked one's details and this player's hunt records.
    public partial class ArenaApp
    {
        /// A boss the Boss Hunt list names: beaten at least once (or its hunt opened), else a "???" row like the Quick Play tab's pool.
        static bool HuntKnown(EnemyType boss, bool open, Profile profile) => open || (profile != null && profile.Beaten(boss) > 0);

        static readonly Color HuntColor = new Color(0.95f, 0.42f, 0.32f);

        static readonly Color PhosGreen = new Color(0.30f, 1f, 0.60f);

        static void HuntBox(Rect r, Color c)
        {
            CrtKit.Frame(r, 1f, c);  // (snapped to whole pixels)
        }

        /// A small outlined status tag ("WON x2", "OPEN", ...), right-aligned to xRight. Returns its width.
        static void HuntChip(float xRight, float cy, string text, Color c)
        {
            float tw = LobbyKit.SpacedWidth(text, 10, 1f) + 10f;
            var r = new Rect(xRight - tw, cy - 8f, tw, 16f);
            Ui.Fill(r, c.WithAlpha(0.12f));
            HuntBox(r, c.WithAlpha(0.55f));
            LobbyKit.Spaced(r, text, 10, c, TextAnchor.MiddleCenter, 1f);
        }

        void DrawHuntPicker(Rect r, float t) => DrawModePage(r, t, HuntPage());

        /// The page (ModePageUi.cs): the ten bosses, click an open hunt to pick it (the host), the picked or hovered one in the scope.
        ModePage HuntPage()
        {
            var profile = game.Profile ?? new Profile();
            bool host = session.IsHost;
            var bosses = new EnemyType[10];
            int huntN = 0;
            int steps = session.HuntLong ? Hunts.LongLength : Hunts.Length;
            var p = new ModePage { Title = "BOSS HUNT", Acc = HuntColor, ListHead = "HUNTS",
                Tagline = host ? "Pick a boss. Beat one in a run to open its hunt." : "The host picks the boss (from the hunts they've opened).",
                Rules = new[] { "NO PATHS", "NO DATA GATES", "NO SAVING", "MODIFIERS + DIFFICULTY APPLY" } };
            for (int i = 0; i < 10; i++)
            {
                var b = bosses[i] = BossSchedule.Campaign.BossAt((i + 1) * 5);
                var h = Hunts.Get(b);
                bool open = Hunts.Available(h, profile), known = HuntKnown(b, open, profile);
                int won = profile.HuntWon(b, true);
                if (open) huntN++;
                p.Rows.Add(new ModeRowData { Num = (i + 1).ToString("00"), Name = BossName(b), Known = known, Lit = open, Model = b,
                    Chip = h == null ? "SOON" : !open ? "LOCKED" : won > 0 ? "WON x" + won : "OPEN",
                    ChipCol = h == null ? CrtStyle.Ink.WithAlpha(0.55f) : !open ? HuntColor : won > 0 ? CrtStyle.Gold : new Color(0.5f, 0.88f, 0.69f) });
            }
            p.Pct = huntN / 10f;
            p.Ring = huntN + "/10";
            p.ListCap = huntN + " of 10 open";
            p.Default = Mathf.Max(0, System.Array.IndexOf(bosses, session.HuntBoss));
            if (host)
                p.OnPick = i => { if (Hunts.Available(Hunts.Get(bosses[i]), profile)) session.SetHuntBoss(bosses[i]); };
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
                    CopyCol = can ? Color.white : h == null ? Ui.Muted : Ui.Warn,
                    RecVals = new[] { can ? profile.HuntWon(b, true) + " / " + played : "-", ModeClock(best), can ? "OPEN" : "LOCKED" },
                    RecLabels = new[] { "HUNTS WON", "BEST TIME", "STATUS" } };
            };
            return p;
        }
    }
}

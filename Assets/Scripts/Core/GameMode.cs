namespace Arena
{
    /// The game modes, in the lobby carousel's order (LobbyUi.DrawModeSwitch). The lobby holds the one picked
    /// (Session.Mode, sent as one byte); a run works its own out from what the start set up (Game.Mode).
    public enum GameMode : byte { Quick, Campaign, Hunt, Practice, Survival, None }

    /// What a mode allows: one row per mode, so a rule that differs between modes is read here instead of being
    /// spelled out as a chain of mode checks wherever it's needed. A new mode adds an enum value and a row.
    public sealed class ModeDef
    {
        public GameMode Mode;
        public string Key;          // the profile's LastMode
        public string Name;         // the one name for the mode: the lobby carousel, logs, recent runs (RunLabel)
        public string Blurb;        // the lobby carousel's line under the name
        public bool Lives;          // lives and checkpoints (Checkpoints.cs LivesMax)
        public bool Saves;          // saved to continue (RunSaves.cs Saveable)
        public bool RunEvents;      // a run's between-wave events: Expanding rooms, breaches, duels (not Practice or a hunt)
        public bool Challenges;     // wave challenges (Challenges.cs)
        public bool Legs;           // the 100-wave Campaign's legs and faction buildup (Hunts.cs LongLegs, Approaches.cs CampaignOn)
        public bool Paths;          // two paths offered after a boss on the classic arena (Paths.cs OfferPaths)
        public bool BossBuildup;    // build-up waves are only the coming boss's faction (Approaches.cs ApproachShare; BasicFaction.cs FactionFor)
        public bool Extras = true;  // the run's dressing: props, layouts, timed waves, zombies, the boss gate, breach warnings (off: the Practice Range plays its plan bare)
        public bool MapResets;      // the Expanding map goes back to the hub after each boss (MapReset.cs, Docs/MapReset.md)
        public bool Records = true; // recorded on the profile, each mode its own way (RunSaves.cs RecordsRuns)
        public WaveRules Waves = WaveRules.Standard;  // wave size (WaveNumbers.cs)

        static readonly ModeDef[] all =
        {
            new ModeDef { Mode = GameMode.Quick,    Key = "quick",    Name = "Quick Play", Blurb = "10 waves, then a random boss", RunEvents = true, BossBuildup = true },
            new ModeDef { Mode = GameMode.Campaign, Key = "campaign", Name = "Campaign", Blurb = "100 waves, build as you go", Lives = true, Saves = true, RunEvents = true, Challenges = true, Legs = true, Paths = true, MapResets = true },
            new ModeDef { Mode = GameMode.Hunt,     Key = "hunt",     Name = "Boss Hunt", Blurb = "10 waves to one boss", Saves = true, BossBuildup = true, Waves = WaveRules.Hunt },
            new ModeDef { Mode = GameMode.Practice, Key = "practice", Name = "Practice Range", Blurb = "Build your own waves, or train a mechanic", Extras = false, Records = false },
            // Survival: one clock, never saved (so no lives), no wave clears (so no challenges), its own faction phases (no legs).
            new ModeDef { Mode = GameMode.Survival, Key = "survival", Name = "Survival", Blurb = "One clock, no waves, no end", RunEvents = true },
        };

        public static ModeDef For(GameMode m) => (int)m < all.Length ? all[(int)m] : all[(int)GameMode.Campaign];

        /// The mode a profile key names ("" and unknown keys: Quick Play, a new player's default).
        public static GameMode FromKey(string key)
        {
            foreach (var d in all)
                if (d.Key == key)
                    return d.Mode;
            return GameMode.Quick;
        }
    }

    public partial class Game
    {
        /// This match's mode, from what the start set up (Practice, Hunt, SurvivalOn, Quick; none of them: the Campaign).
        public GameMode Mode => Practice != null ? GameMode.Practice : Hunt != null ? GameMode.Hunt : SurvivalOn ? GameMode.Survival
            : Quick ? GameMode.Quick : GameMode.Campaign;

        /// What this run's mode allows (ModeDef).
        public ModeDef ModeRules => ModeDef.For(Mode);

        /// This match's name for logs and recent runs: the mode's name, then " · Endless" or " · Deathless".
        public string RunLabel(bool endless = false, bool deathless = false) => ModeRules.Name + (endless ? " · Endless" : deathless ? " · Deathless" : "");
    }
}

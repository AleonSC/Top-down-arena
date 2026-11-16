namespace Arena
{
    /// The game modes, in the lobby carousel's order (LobbyUi.DrawModeSwitch). The lobby holds the one picked
    /// (Session.Mode, sent as one byte); a match works its own out from what the start set up (Game.Mode).
    public enum GameMode : byte { Quick, Campaign, Hunt, Practice, Survival, None }

    /// What a mode allows: one row per mode, so a rule that differs between modes is read here instead of being
    /// spelled out as a chain of mode checks wherever it's needed. A new mode adds an enum value and a row.
    public sealed class ModeDef
    {
        public GameMode Mode;
        public string Key;          // the profile's LastMode
        public string Name;         // shown in logs and replays
        public bool Lives;          // lives and checkpoints (Checkpoints.cs LivesMax)
        public bool Saves;          // saved to continue (RunSaves.cs Saveable)
        public bool RunEvents;      // a run's between-wave events: Expanding rooms, breaches, duels, the Balance test (not Practice or a hunt)
        public bool Challenges;     // wave challenges (Challenges.cs)
        public bool Legs;           // the 100-wave Campaign's legs and faction buildup (Hunts.cs LongLegs, Approaches.cs CampaignOn)

        static readonly ModeDef[] all =
        {
            new ModeDef { Mode = GameMode.Quick,    Key = "quick",    Name = "Quick Play", RunEvents = true },
            new ModeDef { Mode = GameMode.Campaign, Key = "campaign", Name = "Campaign",   Lives = true, Saves = true, RunEvents = true, Challenges = true, Legs = true },
            new ModeDef { Mode = GameMode.Hunt,     Key = "hunt",     Name = "Boss Hunt" },
            new ModeDef { Mode = GameMode.Practice, Key = "practice", Name = "Practice" },
            // Survival runs on the Campaign's wave machinery under its clock, so it keeps the Campaign's lives, events and buildup.
            new ModeDef { Mode = GameMode.Survival, Key = "survival", Name = "Survival",   Lives = true, RunEvents = true, Challenges = true, Legs = true },
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

        /// What this match's mode allows (ModeDef).
        public ModeDef ModeRules => ModeDef.For(Mode);
    }
}

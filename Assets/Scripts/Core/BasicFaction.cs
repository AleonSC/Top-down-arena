using System;

namespace Arena
{
    /// The basic "faction": the eight plain enemies left once the rest were retired, one per role, standing in for every
    /// boss that has no faction of its own yet (from the Duelist on) until its faction pack is built. Quick Play's and a
    /// Boss Hunt's build-up waves use it like a real faction (Game.FactionFor); the Campaign's usual mix is these eight.
    public static class BasicFaction
    {
        /// Rusher, tank, charger, gunner, sniper, spray, artillery, guard.
        public static readonly EnemyType[] Types =
        {
            EnemyType.Swarmer, EnemyType.Brute, EnemyType.Lunger, EnemyType.Grunt,
            EnemyType.Sniper, EnemyType.Spinner, EnemyType.Mortar, EnemyType.Shielder,
        };

        /// Not in Approaches.All (it has no boss: Boss is left at its default), so Approaches.For never finds it.
        public static readonly FactionDef Def = new FactionDef
        {
            Members = new[]
            {
                new FactionMember { Type = EnemyType.Swarmer, Weight = 2f },
                new FactionMember { Type = EnemyType.Grunt, Weight = 3f },
                new FactionMember { Type = EnemyType.Brute, Weight = 1.5f, From = 2 },
                new FactionMember { Type = EnemyType.Sniper, Weight = 2f, From = 2 },
                new FactionMember { Type = EnemyType.Lunger, Weight = 1.5f, From = 3 },
                new FactionMember { Type = EnemyType.Spinner, Weight = 1.5f, From = 3 },
                new FactionMember { Type = EnemyType.Mortar, Weight = 1f, From = 4 },
                new FactionMember { Type = EnemyType.Shielder, Weight = 1f, From = 4 },
            },
            Squads = new[]
            {
                new SquadDef { Name = "Rush", From = 1, Members = new[] { EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer, EnemyType.Swarmer } },
                new SquadDef { Name = "Firing line", From = 2, Members = new[] { EnemyType.Grunt, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Sniper } },
                new SquadDef { Name = "Brute pack", From = 3, Members = new[] { EnemyType.Brute, EnemyType.Brute, EnemyType.Lunger } },
                new SquadDef { Name = "Shield wall", From = 4, Members = new[] { EnemyType.Shielder, EnemyType.Sniper, EnemyType.Sniper } },
                new SquadDef { Name = "Barrage", From = 4, Members = new[] { EnemyType.Shielder, EnemyType.Mortar, EnemyType.Mortar, EnemyType.Spinner } },
            },
            // The last wave before the boss: everything at once, the brutes and lungers in front of a gun line.
            Finale = new SquadDef { Name = "Last stand", From = 4, Members = new[] {
                EnemyType.Brute, EnemyType.Brute, EnemyType.Lunger, EnemyType.Lunger,
                EnemyType.Shielder, EnemyType.Grunt, EnemyType.Grunt, EnemyType.Spinner, EnemyType.Mortar, EnemyType.Sniper } },
        };
    }

    public partial class Game
    {
        /// Retired from every wave (kept in the code for the faction reskins and future packs): the plain enemies that
        /// aren't in BasicFaction. Boss attacks, the Rooms map's nests and the Wave Lab can still bring them.
        public static bool IsRetired(EnemyType t)
        {
            switch (t)
            {
                case EnemyType.Spiker: case EnemyType.Buzzsaw: case EnemyType.SpikeBlock: case EnemyType.Swordsman:
                case EnemyType.ShieldSwordsman: case EnemyType.Burster: case EnemyType.Charger: case EnemyType.Splitter:
                case EnemyType.Medic: case EnemyType.Pauser: case EnemyType.FuseRunner: case EnemyType.Hookman:
                case EnemyType.Sapper: case EnemyType.Phantom: case EnemyType.BeamSentry: case EnemyType.Leader:
                case EnemyType.Hexer: case EnemyType.Stitcher: case EnemyType.Scorcher: case EnemyType.Juggernaut:
                case EnemyType.Reaver: case EnemyType.Nest: case EnemyType.Warder:
                    return true;
            }
            return false;
        }

        /// A squad the regular waves may still bring: none of its members retired.
        public static bool SquadLive(SquadDef q) => !Array.Exists(q.Members, IsRetired);

        /// The faction Quick Play's and a Boss Hunt's build-up waves draw from: the boss's own, else the basic one.
        public static FactionDef FactionFor(EnemyType boss) => Approaches.For(boss) ?? BasicFaction.Def;
    }
}

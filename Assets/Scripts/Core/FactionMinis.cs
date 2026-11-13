namespace Arena
{
    /// Faction mini-bosses (Docs/FactionMinis.md): each boss's faction has its own mini-boss for the mini-boss rooms (the Expanding
    /// arena's sealed duel room, GatekeeperRoom.cs, and the Rooms arena's vault). A room placed in a boss cycle keeps that cycle's
    /// faction (ExpansionRoom.Mini). The Gatekeeper is the Data Gates' mini-boss only (Challenges.cs).
    public static class FactionMinis
    {
        /// The mini-boss of the faction building up to this boss. Only the Warden's is built so far: the others bring the Riot Captain
        /// until theirs exist.
        public static EnemyType For(EnemyType boss)
        {
            switch (boss)
            {
                case EnemyType.Boss: return EnemyType.RiotCaptain;  // the Warden
                default: return EnemyType.RiotCaptain;
            }
        }

        /// A mini-boss room's duel foe (never the Data Gates' Gatekeeper).
        public static bool Is(EnemyType t) => t == EnemyType.RiotCaptain;

        /// A duel foe of a mini-boss room or the vault: a faction mini-boss, or an old save's Gatekeeper.
        public static bool Duels(EnemyType t) => Is(t) || t == EnemyType.Gatekeeper;

        /// Its name on the map before the room is opened.
        public static string Name(EnemyType t) => Is(t) ? Defs.Get(t).Name : "Mini-boss";
    }

    public partial class Game
    {
        /// The mini-boss for a room placed now: the next boss's faction's.
        public EnemyType MiniBossNow() => FactionMinis.For(ApproachBossAt(System.Math.Max(1, Wave)));
    }
}

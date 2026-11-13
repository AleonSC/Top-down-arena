namespace Arena
{
    /// The Hive Queen's faction (Docs/HiveFaction.md): eight enemies that each play the part of one of the Warden's, so they
    /// share its behaviour. A Worker moves like a Rioter, a Soldier a Guard, a Comb Shield a Riot Shield, a Honey Lobber
    /// a Gas Grenadier (its puddle slows instead of hurting), a Stinger a Taser Guard, a Needler a Security Guard,
    /// a Bumble a Thug (a Brute) and a Hornet a Shiv (a Lunger). Anything that asks "is it a Riot Shield / a Taser Guard"
    /// goes through `Kin`; the looks (Game/HiveLook.cs) and Defs are their own.
    public static class HiveKin
    {
        static readonly EnemyType[] table = Build();

        static EnemyType[] Build()
        {
            var t = new EnemyType[256];
            for (int i = 0; i < t.Length; i++)
                t[i] = (EnemyType)i;
            t[(int)EnemyType.Worker] = EnemyType.Rioter;
            t[(int)EnemyType.Soldier] = EnemyType.Guard;
            t[(int)EnemyType.CombShield] = EnemyType.RiotShield;
            t[(int)EnemyType.HoneyLobber] = EnemyType.GasGrenadier;
            t[(int)EnemyType.Stinger] = EnemyType.TaserGuard;
            t[(int)EnemyType.Needler] = EnemyType.SecurityGuard;
            t[(int)EnemyType.Bumble] = EnemyType.Thug;
            t[(int)EnemyType.Hornet] = EnemyType.Shiv;
            // the Colossus's faction (QuarryFaction.cs) shares the table: its enemies play like these
            t[(int)EnemyType.Rubble] = EnemyType.Rioter;
            t[(int)EnemyType.Quarryman] = EnemyType.Guard;
            t[(int)EnemyType.Stomper] = EnemyType.Thug;
            t[(int)EnemyType.SlabBearer] = EnemyType.RiotShield;
            t[(int)EnemyType.RockHurler] = EnemyType.GasGrenadier;
            return t;
        }

        /// The Warden enemy this one plays like (itself, for anything that isn't a Hive or Quarry enemy).
        public static EnemyType Of(EnemyType t) => table[(byte)t];

        public static bool Is(EnemyType t) => t >= EnemyType.Worker && t <= EnemyType.Hornet;
    }

    public partial class Enemy
    {
        public EnemyType Kin => HiveKin.Of(Type);
    }
}

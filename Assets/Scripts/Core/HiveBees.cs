using System;

namespace Arena
{
    /// What sets the Hive's bees apart from their Warden counterparts (Docs/HiveFaction.md): Workers speed up in a crowd, a Soldier
    /// is armoured while it charges, a Comb Shield's plate lets some of each shot through as it breaks, and a Needler strafes
    /// faster. The rest is HiveKin.cs.
    public partial class Game
    {
        public const float WorkerPackRange = 110f, WorkerPackStep = 0.1f, WorkerPackMax = 0.5f;  // +10% speed for each Worker within 110 px, up to +50%
        public const float SoldierChargeRange = 260f, SoldierChargeSpeed = 1.3f, SoldierArmor = 0.5f;  // inside 260 px it charges: 30% faster, half damage
        public const float CombLeakBase = 0.15f, CombLeakStep = 0.15f;  // the share of a shot that gets past the plate: 15% on a whole one, 15% more for each crack
        public const float NeedlerStrafe = 1.2f, NeedlerAimStrafe = 1.4f;  // its sideways pull (a Security Guard's is 0.7) and its pace while it locks on (x1.4)

        /// How fast a Worker runs for the Workers round it (1 alone).
        float WorkerPackSpeed(Enemy e)
        {
            int n = 0;
            foreach (var o in Enemies.Values)
                if (o != e && o.Type == EnemyType.Worker && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < WorkerPackRange)
                    n++;
            return 1f + Math.Min(WorkerPackMax, WorkerPackStep * n);
        }

        /// A Soldier within SoldierChargeRange of its target is charging (Enemy.Phase 1, so every game draws its armour); returns its speed multiplier.
        float SoldierCharge(Enemy e, float dist)
        {
            e.Phase = (byte)(dist < SoldierChargeRange ? 1 : 0);
            return e.Phase == 1 ? SoldierChargeSpeed : 1f;
        }

        public static bool SoldierCharging(Enemy e) => e.Type == EnemyType.Soldier && e.Phase == 1;

        /// A Comb Shield's plate lets a share of a blocked shot through, more as it cracks.
        public static float CombLeak(Enemy e) => CombLeakBase + CombLeakStep * RiotCrack(e);
    }
}

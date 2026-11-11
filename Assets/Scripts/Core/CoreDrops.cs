using System;

namespace Arena
{
    // Surprise Core drops (Docs/Variants.md): now and then a regular enemy spawns glowing gold; kill it for 1-3 Cores, paid with the run's
    // other Cores when it ends (the End record carries CoresFound, so every peer's profile hears about it).
    public partial class Game
    {
        public static float CoreCarrierChance = 0.008f;  // per regular spawn, from CoreCarrierFromWave, one alive at a time
        public const int CoreCarrierFromWave = 3, CoreCarrierMaxPerRun = 6;

        /// Cores the run's carriers have dropped so far (host; sent with the End record).
        public int CoresFound;
        int coreCarriersSpawned;

        /// Host: a fresh regular enemy might be the glowing one. Never a boss, an elite, a summoned add or an enemy that pays no XP.
        void RollCoreCarrier(Enemy e)
        {
            if (!IsHost || !RecordsRuns || Endless || e.Def.IsBoss || e.Def.Xp <= 0 || Wave < CoreCarrierFromWave || coreCarriersSpawned >= CoreCarrierMaxPerRun)
                return;
            if (IdRoll(e.Id, 1) >= CoreCarrierChance)  // (a hash of the enemy's id, not the game's rng, so the rest of the run plays out the same)
                return;
            foreach (var o in Enemies.Values)
                if (HasTrait(o, EliteTrait.CoreCarrier))
                    return;
            e.Traits |= (byte)EliteTrait.CoreCarrier;
            coreCarriersSpawned++;
        }

        /// A repeatable 0..1 value for an enemy id (and a salt), from a small integer hash.
        static float IdRoll(int id, uint salt)
        {
            uint h = (uint)id * 2654435761u + salt * 40503u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }

        /// Host: the glowing enemy died: 1-3 Cores for the run.
        void DropCores(Enemy e)
        {
            int n = Variants.CarrierMin + (int)(IdRoll(e.Id, 2) * (Variants.CarrierMax - Variants.CarrierMin + 1));
            CoresFound += n;
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.82f, 0.25f), 60f, 0.5f, Sfx.PickupBig);
        }
    }
}

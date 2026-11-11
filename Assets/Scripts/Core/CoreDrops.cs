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
            if (rng.NextDouble() >= CoreCarrierChance)
                return;
            foreach (var o in Enemies.Values)
                if (HasTrait(o, EliteTrait.CoreCarrier))
                    return;
            e.Traits |= (byte)EliteTrait.CoreCarrier;
            coreCarriersSpawned++;
        }

        /// Host: the glowing enemy died: 1-3 Cores for the run.
        void DropCores(Enemy e)
        {
            int n = Variants.CarrierMin + rng.Next(Variants.CarrierMax - Variants.CarrierMin + 1);
            CoresFound += n;
            BroadcastFx(FxKind.Ring, e.Pos, new Rgba(1f, 0.82f, 0.25f), 60f, 0.5f, Sfx.PickupBig);
        }
    }
}

namespace Arena
{
    /// Clears the per-run lists the classes, weapons and enemies keep on the host (swings in flight, stuck stars,
    /// shocks, ink, ...). Called by Stop: enemy, bullet and minion ids start again at 1 in the next run, so anything
    /// left over would land on an unrelated enemy (a stale stuck star, a coating, a steering waypoint).
    public partial class Game
    {
        void ResetRunLists()
        {
            // Knight and Soldier weapons
            pendingSwings.Clear(); steelWalls.Clear(); slams.Clear();
            hookRuns.Clear(); lanceRuns.Clear(); gauntlets.Clear(); boShoves.Clear(); cavalry.Clear(); volleys.Clear(); cannonBalls.Clear(); cannonCalls.Clear(); oaths.Clear();
            holyCrosses.Clear(); bladedRuns.Clear(); castRuns.Clear(); grabRuns.Clear(); airRuns.Clear();
            hammerThrows.Clear(); hammerSlams.Clear(); hammerSlamCd.Clear(); shieldThrows.Clear(); bashShards.Clear();
            ionTrails.Clear(); packBiters.Clear(); shocks.Clear(); rodCap.Clear(); splashSeen.Clear();
            // Ninja
            stuckStars.Clear(); fangRuns.Clear(); katanaLater.Clear(); caltrops.Clear(); groves.Clear(); groveBleeds.Clear(); bells.Clear();
            // Hemomancer and Elementalist
            coatings.Clear(); freezeWaves.Clear(); shockRuns.Clear(); harvestTargets.Clear(); harvests.Clear(); waves.Clear();
            Ink.Clear(); pens.Clear(); hands.Clear();
            ResetHive();  // HiveCenser.cs
            heartDrops.Clear(); hfHost.Clear();  // HeartFlail.cs
            // enemies and bosses
            dives.Clear(); scytheCuts.Clear(); throwHits.Clear();
            steerMem.Clear();  // LayoutRules.cs: keyed by enemy id
            hostDodgeAt.Clear();  // Dodge.cs (keyed by player id)
        }
    }
}

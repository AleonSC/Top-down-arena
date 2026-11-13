using System;

namespace Arena
{
    /// Spawning (Docs/WaveRefactor.md step 3): how a built wave's queue (`spawnQueue` singles, `squadQueue` squads) comes
    /// into the arena. One place hands out the next entry (`SpawnNextEntry`), one place counts what is still to come
    /// (`PendingEnemies`), and the pacing, when each entry comes, is one of three (`SpawnPacing`).
    /// A designed wave's groups (WaveDesign.cs `TickDesign`) have their own queue and timers and run beside this.
    enum SpawnPacing
    {
        Hold,      // Rooms arena, room by room: every room is full (or every nest is down), wait
        Timed,     // a timed wave: the arrival curve over a window, direct or through portals (TimedWaves.cs, SpawnPortals.cs)
        Interval,  // one entry every so often (waves that aren't timed: designed, practice, Zombie Infection, bosses)
    }

    public partial class Game
    {
        /// Host: one wave tick of spawning. False when nothing is queued (the wave then waits for its last foes or ends).
        bool TickSpawning(float dt)
        {
            if (QueuedUnits() == 0)
                return false;
            switch (Pacing())
            {
                case SpawnPacing.Hold:
                    spawnTimer = Math.Max(spawnTimer, 0.3f);
                    break;
                case SpawnPacing.Timed:
                    TickTimedSpawn(dt);
                    break;
                default:
                    TickIntervalSpawn(dt);
                    break;
            }
            return true;
        }

        SpawnPacing Pacing()
        {
            if (RoomsOn && Rooms.RoomByRoom && !RoomsSpawnReady())
                return SpawnPacing.Hold;  // every room full (or every nest down: the rest is called off)
            return timedWave ? SpawnPacing.Timed : SpawnPacing.Interval;
        }

        /// Pacing Interval: the next entry every `WaveNumbers.SpawnInterval` (a horde comes faster, up to its cap).
        void TickIntervalSpawn(float dt)
        {
            spawnTimer -= dt;
            float next = WaveNumbers.SpawnInterval(Wave) * (huntAmbush ? AmbushIntervalScale : 1f);  // (an Ambush's enemies come fast: HuntMapGame.cs)
            if (spawnTimer <= 0f && ZombieSpawnReady(ref next))
            {
                spawnTimer = next;
                if (SpawnNextEntry())
                    spawnTimer *= WaveNumbers.SquadPause;  // a squad takes a longer breath
            }
        }

        /// The next squad (one comes after every few singles, or once the singles run out: Variety.cs), else the next single.
        /// True when it was a squad.
        bool SpawnNextEntry()
        {
            if (SpawnNextSquad())
                return true;
            SpawnNextSingle();
            return false;
        }

        /// The first single in the queue comes in at once.
        void SpawnNextSingle()
        {
            SpawnEnemy(spawnQueue[0]);
            spawnQueue.RemoveAt(0);
            singlesSinceSquad++;
        }

        /// Everything the wave still has to throw at the party: queued singles and squad members, living foes, risers (Zombies.cs),
        /// a designed wave's groups still to come and the enemies still inside open portals.
        int PendingEnemies() => QueuedUnits() + Foes() + risers.Count + DesignPending + PortalCargoCount();
    }
}

using System;

namespace Arena
{
    /// Timed waves: a regular wave's enemies arrive over a set window instead of in one burst, ramping up, holding, then
    /// tapering off like the wave dying down. The wave still ends only when everything is dead (no clock-out).
    public static class TimedWaves
    {
        public static bool Enabled = true;   // off in the sim tests (their waves are short), except the "timed" suite
        public const float Window = 45f;     // seconds the spawning lasts (the clear-up after it brings a wave to about a minute)
        public const float RampEnd = 0.15f;  // share of the window spent ramping up
        public const float TaperStart = 0.6f; // share of the window where the taper begins
        public const int AliveBase = 14, AlivePerWave = 3, AliveMax = 60;  // most foes alive at once before spawns wait

        /// The spawn window: shorter in the first waves, so the opening isn't a wait for the last few enemies (24 s on wave 1, the full 45 s by wave 6).
        public static float WindowFor(int wave) => Math.Min(Window, 24f + 4f * Math.Max(0, wave - 1));

        public static int AliveCap(int wave) => Math.Min(AliveMax, AliveBase + AlivePerWave * wave);

        /// Share (0..1) of the wave that should have arrived x (0..1) of the way through the window: the area under a
        /// trapezoid density (ramp up, plateau, taper to nothing).
        public static float Share(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float a = RampEnd, b = TaperStart, L = 1f - b;
            float total = a / 2f + (b - a) + L / 2f;
            float area;
            if (x < a) area = x * x / (2f * a);
            else if (x < b) area = a / 2f + (x - a);
            else { float t = x - b; area = a / 2f + (b - a) + t - t * t / (2f * L); }
            return area / total;
        }
    }

    public partial class Game
    {
        bool timedWave;          // this wave spawns on the clock
        float timedClock;        // seconds into the wave's spawn window (negative: the opening delay)
        int timedTotal;          // enemies (squad members counted) the wave started with

        int QueuedUnits()
        {
            int n = spawnQueue.Count;
            foreach (var sq in squadQueue)
                n += sq.Members.Length;
            return n;
        }

        /// Host: a regular wave just built. Timed unless it is a boss, practice, designed, zombie or room-by-room wave.
        void BeginTimedWave()
        {
            timedWave = TimedWaves.Enabled && Practice == null && Design == null && !BossNow && !ZombiesOn && !(RoomsOn && Rooms.RoomByRoom) && QueuedUnits() > 0;
            timedTotal = QueuedUnits();
            timedClock = -spawnTimer;  // (the usual opening delay: 0.5 s, longer while the breaches flicker up)
        }

        /// Host: one tick of a timed wave's spawning; true when it handled the queue this tick.
        bool TickTimedSpawn(float dt)
        {
            if (!timedWave)
                return false;
            timedClock += dt;
            int left = QueuedUnits();
            if (left > timedTotal)
                timedTotal = left;  // (something added to the queue mid-wave)
            int spawned = timedTotal - left;
            int cap = (int)Math.Ceiling(TimedWaves.AliveCap(Wave) * ExpansionScale);  // (a bigger Expanding map holds a bigger crowd: ExpansionGame.cs)
            if (spawned < (int)Math.Ceiling(TimedWaves.Share(timedClock / TimedWaves.WindowFor(Wave)) * timedTotal) && Foes() < (BreachesOn ? (int)(cap * Breaches.AliveScale) : cap))
            {
                if (!SpawnNextSquad())
                {
                    SpawnEnemy(spawnQueue[0]);
                    spawnQueue.RemoveAt(0);
                    singlesSinceSquad++;
                }
            }
            return true;
        }
    }
}

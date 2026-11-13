using System;

namespace Arena
{
    /// Timed waves: a regular wave's enemies arrive over a set window instead of in one burst, ramping up, holding, then
    /// tapering off like the wave dying down. The wave still ends only when everything is dead (no clock-out).
    /// Inside that shape the arrivals come in surges: three busy stretches (Surges) with quiet lulls between them
    /// (SurgeLull of the surge rate), so a wave breathes in and out instead of dripping in at a steady rate.
    public static class TimedWaves
    {
        public static bool Enabled = true;   // off in the sim tests (their waves are short), except the "timed" suite
        public const float Window = 45f;     // seconds the spawning lasts (the clear-up after it brings a wave to about a minute)
        public const float RampEnd = 0.15f;  // share of the window spent ramping up
        public const float TaperStart = 0.6f; // share of the window where the taper begins
        public const int AliveBase = 14, AlivePerWave = 3, AliveMax = 60;  // most foes alive at once before spawns wait
        public static readonly float[] Surges = { 0.2f, 0.48f, 0.76f };  // the surges' middles, as shares of the window
        public const float SurgeHalfWidth = 0.08f;  // each surge lasts 2 x this share of the window (about 7 s of 45)
        public const float SurgeLull = 0.4f;        // the arrival rate between surges, as a share of the rate at a surge's peak

        /// The spawn window: shorter in the first waves, so the opening isn't a wait for the last few enemies (24 s on wave 1, the full 45 s by wave 6).
        public static float WindowFor(int wave) => Math.Min(Window, 24f + 4f * Math.Max(0, wave - 1));

        public static int AliveCap(int wave) => Math.Min(AliveMax, AliveBase + AlivePerWave * wave);

        /// The ramp, plateau and taper (0..1) x of the way through the window.
        static float Trapezoid(float x)
        {
            if (x <= 0f || x >= 1f) return 0f;
            if (x < RampEnd) return x / RampEnd;
            if (x < TaperStart) return 1f;
            return (1f - x) / (1f - TaperStart);
        }

        /// 1 at a surge's peak, SurgeLull between surges (a smooth cos² bump each).
        public static float Pulse(float x)
        {
            float bump = 0f;
            foreach (float c in Surges)
            {
                float u = Math.Abs(x - c) / SurgeHalfWidth;
                if (u < 1f)
                {
                    float k = (float)Math.Cos(u * Math.PI / 2.0);
                    bump = Math.Max(bump, k * k);
                }
            }
            return SurgeLull + (1f - SurgeLull) * bump;
        }

        /// True in the busy middle of a surge (the spawner opens more portals at once: SpawnPortals.cs).
        public static bool InSurge(float x)
        {
            foreach (float c in Surges)
                if (Math.Abs(x - c) < SurgeHalfWidth * 0.6f)
                    return true;
            return false;
        }

        const int Steps = 400;
        static readonly float[] cumulative = BuildCurve();

        static float[] BuildCurve()
        {
            var c = new float[Steps + 1];
            for (int i = 1; i <= Steps; i++)
            {
                float x = (i - 0.5f) / Steps;
                c[i] = c[i - 1] + Trapezoid(x) * Pulse(x);
            }
            for (int i = 1; i <= Steps; i++)
                c[i] /= c[Steps];
            return c;
        }

        /// Share (0..1) of the wave that should have arrived x (0..1) of the way through the window: the area under the
        /// trapezoid density times the surges' pulse.
        public static float Share(float x)
        {
            if (x <= 0f) return 0f;
            if (x >= 1f) return 1f;
            float f = x * Steps;
            int i = (int)f;
            return cumulative[i] + (cumulative[i + 1] - cumulative[i]) * (f - i);
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
            ResetPortals();  // SpawnPortals.cs
        }

        /// Host: one tick of a timed wave's spawning (pacing Timed: Spawner.cs).
        void TickTimedSpawn(float dt)
        {
            timedClock += dt;
            int left = QueuedUnits();
            if (left > timedTotal)
                timedTotal = left;  // (something added to the queue mid-wave)
            int spawned = timedTotal - left;
            int cap = (int)Math.Ceiling(TimedWaves.AliveCap(Wave) * ExpansionScale);  // (a bigger Expanding map holds a bigger crowd: ExpansionGame.cs)
            if (BreachesOn)
                cap = (int)(cap * Breaches.AliveScale);
            float x = timedClock / TimedWaves.WindowFor(Wave);
            int due = (int)Math.Ceiling(TimedWaves.Share(x) * timedTotal);
            if (PortalsOn)
            {
                portalSurge = TimedWaves.InSurge(x);  // portals look busier in a surge (TickPortals)
                TickPortalSpawn(due - spawned, cap - Foes() - PortalCargoCount(), x);  // in bursts, through portals (SpawnPortals.cs)
                return;
            }
            if (spawned < due && Foes() < cap)
                SpawnNextEntry();
        }
    }
}

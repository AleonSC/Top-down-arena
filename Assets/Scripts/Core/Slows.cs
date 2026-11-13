using System;

namespace Arena
{
    /// Enemy slows (host). An enemy keeps its strongest slow (SlowMult for SlowT) and the next-strongest one
    /// (SlowMult2 for SlowT2). When the strongest ends, the backup takes over, so a short freeze (0.1x) can't be kept
    /// going by a weak slow that is topped up every tick (Wet, ink, a slow zone): it ends on time, then the weak one applies.
    public partial class Game
    {
        const float SlowEps = 1e-4f;

        /// Slow e to mult (0..1, lower is slower) for t seconds. Every slow source goes through here.
        public void Slow(Enemy e, float t, float mult)
        {
            if (t <= 0f || mult >= 1f)
                return;
            if (e.SlowT <= 0f || e.SlowMult >= 1f)
            {
                e.SlowT = Math.Max(e.SlowT, t);  // (SlowT may be running with no slow: a stun keeps the chill look on)
                e.SlowMult = mult;
            }
            else if (mult < e.SlowMult - SlowEps)  // stronger: the old one becomes the backup
            {
                SlowBackup(e, e.SlowT, e.SlowMult);
                e.SlowT = t;
                e.SlowMult = mult;
            }
            else if (mult <= e.SlowMult + SlowEps)  // the same slow again: it lasts the longer of the two
                e.SlowT = Math.Max(e.SlowT, t);
            else if (t > e.SlowT)  // weaker, and outlasts the strongest: keep it for after
                SlowBackup(e, t, mult);
        }

        static void SlowBackup(Enemy e, float t, float mult)
        {
            if (e.SlowT2 <= 0f || mult < e.SlowMult2 - SlowEps)
            {
                e.SlowT2 = t;
                e.SlowMult2 = mult;
            }
            else if (mult <= e.SlowMult2 + SlowEps)
                e.SlowT2 = Math.Max(e.SlowT2, t);
        }

        /// Host, every tick per enemy: slows wear off; the backup takes over when the strongest ends.
        static void TickSlow(Enemy e, float dt)
        {
            e.SlowT = Math.Max(0f, e.SlowT - dt);
            e.SlowT2 = Math.Max(0f, e.SlowT2 - dt);
            if (e.SlowT > 0f)
                return;
            if (e.SlowT2 > 0f)
            {
                e.SlowT = e.SlowT2;
                e.SlowMult = e.SlowMult2;
                e.SlowT2 = 0f;
            }
            else
                e.SlowMult = 1f;
            e.SlowMult2 = 1f;
        }
    }
}

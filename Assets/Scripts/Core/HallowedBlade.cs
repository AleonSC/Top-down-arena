using System;

namespace Arena
{
    /// Hallowed Blade (Healer Knight's ultimate): the sword falls (the `HolySword` zone, 0.6 s, 200 damage when it lands) and
    /// then stays planted as a `ZoneKind.HallowedBlade` zone. Inside its ring allies heal 15 HP/s and enemies burn 20/s (the
    /// zone def). On top of that a pulse leaves the blade every 2 s (4 pulses, the last at 8 s) and crosses the WHOLE arena:
    /// each ally anywhere is healed and each enemy anywhere is hurt as the ring reaches them, for a third of what the ring
    /// does in 2 s. The ring itself stops at 8 s; the zone lives a little longer so the last pulse can finish crossing.
    /// Host logic only (the zone's timer is replicated, so every screen works out where the rings are); looks:
    /// Game/HallowedBladeLook.cs. Mockup: Docs/Mockups/hallowed-blade-rework.html (look B, Pillar of Light).
    public partial class Game
    {
        public const int HallowedPulses = 4;
        public const float HallowedPulseEvery = 2f, HallowedPulseRing = 0.95f;
        public const float HallowedRingTime = 8f, HallowedLife = 9f;
        public const float HallowedPulseHeal = 10f, HallowedPulseDamage = 40f / 3f;  // a third of the ring's 15 HP/s and 20/s over 2 s

        /// When pulse k leaves the blade, in seconds after it lands.
        public static float HallowedPulseAt(int k) => HallowedPulseEvery * (k + 1);

        /// How far a pulse from `from` has to travel to reach the farthest corner of the arena.
        public static float HallowedReach(V2 from)
        {
            float dx = Math.Max(from.X, Width - from.X), dy = Math.Max(from.Y, Height - from.Y);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        /// The ring part (heal and burn) is over; only the last pulse's ring is left to finish.
        static bool HallowedSpent(Zone z) => z.Kind == ZoneKind.HallowedBlade && z.Duration - z.T > HallowedRingTime;

        void HallowedPulse(Zone z, float dt)
        {
            float now = z.Duration - z.T, before = now - dt, reach = HallowedReach(z.Pos);
            var owner = PlayerOrNull(z.Owner);
            for (int k = 0; k < HallowedPulses; k++)
            {
                float start = HallowedPulseAt(k);
                if (start > now || start + HallowedPulseRing <= before)
                    continue;  // not out yet, or already past everyone
                foreach (var q in Players.Values)
                {
                    float at = start + HallowedPulseRing * q.Pos.DistanceTo(z.Pos) / reach;  // when the ring reaches them
                    if (at > before && at <= now)
                        Heal(q, HallowedPulseHeal * z.HealMult, owner);
                }
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f)
                        continue;
                    float at = start + HallowedPulseRing * e.Pos.DistanceTo(z.Pos) / reach;
                    if (at > before && at <= now)
                        HurtEnemy(e, HallowedPulseDamage * z.DmgMult, z.Owner);
                }
            }
        }
    }
}

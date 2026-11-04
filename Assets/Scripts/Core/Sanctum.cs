using System;

namespace Arena
{
    /// Sanctum (Healer Elementalist's ultimate): a holy dome for 7 s that stops enemy bullets and keeps allies inside from going
    /// down, and sends a healing pulse out from the middle seven times instead of a steady trickle. A pulse is a ring that
    /// leaves the caster's spot at the dome's centre and reaches the rim in `SanctumPulseRing` s; each ally inside is healed
    /// as the ring passes them, so those near the middle heal first. The last pulse is bigger. Host logic only (the zone's
    /// timer is replicated, so every screen works out where the rings are); looks: Game/SanctumLook.cs.
    /// Mockup: Docs/Mockups/sanctum.html (look B, Bubble).
    public partial class Game
    {
        public const int SanctumPulses = 7;
        public const float SanctumFirstPulse = 0.9f, SanctumPulseEvery = 0.9f, SanctumPulseRing = 0.55f;
        public const float SanctumPulseHeal = 20f, SanctumFinaleHeal = 30f;  // 6 x 20 + 30 = 150, against the old steady 20 a second (140)

        /// When pulse k leaves the middle, in seconds after the cast.
        public static float SanctumPulseAt(int k) => SanctumFirstPulse + SanctumPulseEvery * k;

        void SanctumPulse(Zone z, float dt)
        {
            float now = z.Duration - z.T, before = now - dt;
            var owner = PlayerOrNull(z.Owner);
            foreach (var q in Players.Values)
            {
                float dist = q.Pos.DistanceTo(z.Pos);
                if (dist > z.Radius)
                    continue;
                float lag = SanctumPulseRing * dist / z.Radius;  // when the ring reaches them
                for (int k = 0; k < SanctumPulses; k++)
                {
                    float at = SanctumPulseAt(k) + lag;
                    if (at > before && at <= now)
                        Heal(q, (k == SanctumPulses - 1 ? SanctumFinaleHeal : SanctumPulseHeal) * z.HealMult, owner);
                }
            }
        }
    }
}

using System;

namespace Arena
{
    /// The Summoner Healer's Phoenix (host): it glides after its owner's cursor, circling the point, and every
    /// PhoenixPulse seconds (from the cast) heals everyone standing within PhoenixPulseRange for PhoenixPulseHeal of their
    /// max health. It still shoots, and still trades itself to revive a downed ally (Minions.cs). Look: Game/PhoenixLook.cs,
    /// which times its rings from the same clock (Duration - T), so no extra state goes over the network.
    public partial class Game
    {
        public const float PhoenixPulse = 2f, PhoenixPulseRange = 220f, PhoenixPulseHeal = 0.08f;
        const float PhoenixSpeed = 320f, PhoenixCircle = 45f, PhoenixTurn = 2.4f;

        void PhoenixFlies(Minion m, Player owner, float dt)
        {
            m.Ang += PhoenixTurn * dt;
            V2 goal = owner.AimAt + V2.FromAngle(m.Ang) * PhoenixCircle;
            V2 step = (goal - m.Pos).LimitLength(PhoenixSpeed * dt);
            m.Pos += step;
            if (step.Length > 0.05f)
                m.Aim = step.Angle;
            if (m.Duration <= 0f || m.T < 0f)
                return;
            float age = m.Duration - m.T, before = age - dt;
            if (before < dt * 0.5f || Math.Floor(age / PhoenixPulse) != Math.Floor(before / PhoenixPulse))
                foreach (var q in Players.Values)
                    if (!q.Downed && q.Hp > 0f && q.Pos.CloserThan(m.Pos, PhoenixPulseRange))
                        Heal(q, q.MaxHp * PhoenixPulseHeal, owner);
        }
    }
}

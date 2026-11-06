using System;

namespace Arena
{
    /// Sanguine Rain (Healer Hemomancer ultimate), reworked from Docs/Mockups/sanguine-rain.html. Three steps, all timed by `Player.RainClock`
    /// (seconds since the cast, replicated, 0 = off) so every screen draws the same show from one number:
    ///   Siphon (RainCast s):  blood is torn out of the healer into an orb overhead. The healer pays RainUpfront of max health across it.
    ///   Seed   (RainSeed s):  the orb bursts and a bead of blood arcs to each ally.
    ///   Rain   (RainLen s, mastered RainLenMastered): a rain circle follows each ally (the ring is a picture: it heals wherever they are) and
    ///                         every RainTick s it heals each living ally RainHeal of their max health. It drains the healer RainDrain of
    ///                         max health a second, and ends on a second press, when the time is up, or when the healer falls to RainFloor.
    /// `Player.UltT` holds the time left (siphon and seed included), so the look gets the rain's fraction from RainClock + UltT.
    public partial class Game
    {
        public const float RainCast = 1.8f, RainSeed = 0.6f, RainLen = 10f, RainLenMastered = 14f, RainTick = 0.5f;
        public const float RainHeal = 0.03f, RainHealMastered = 0.04f, RainUpfront = 0.12f, RainDrain = 0.03f, RainFloor = 0.15f;

        /// A Sanguine Rain is on for this player (siphon, seed or rain).
        public static bool RainActive(Player p) => p.RainClock > 0f;

        /// The rings are down and healing (the siphon and the seed are over).
        public static bool RainRaining(Player p) => p.RainClock >= RainCast + RainSeed;

        /// True if this slot is a Sanguine Rain that's running: pressing it cancels it instead of casting again.
        public bool RainPress(Player p, int slot) => p.Slots[slot].Id == AbilityId.SanguineRain && p.RainClock > 0f;

        void CastRain(Player p, bool cap)
        {
            p.RainClock = 0.0001f;
            p.RainAcc = 0f;
            p.UltT = RainCast + (cap ? RainLenMastered : RainLen);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), 120f, 0.5f, Sfx.SpellHoly);
        }

        /// Stop the rain: the drain stops at once and each ally's ring splashes away. `cancelled` is the healer pressing it again.
        void EndRain(Player p, bool cancelled)
        {
            if (p.RainClock <= 0f)
                return;
            bool wasRaining = RainRaining(p);
            p.RainClock = 0f;
            p.RainAcc = 0f;
            p.UltT = 0f;
            if (wasRaining)
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed)
                        BroadcastFx(FxKind.RainRelease, q.Pos, q.Pos, new Rgba(0.88f, 0.13f, 0.25f), RainRingR, 0.9f, Sfx.None);
        }

        /// How big an ally's ring is, in px.
        public const float RainRingR = 80f;

        /// Host, every tick: run the clock, pay the healer's blood and heal the allies.
        void TickRain(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.RainClock <= 0f)
                    continue;
                if (p.Downed || p.UltT <= 0f || p.Slots[Upgrades.UltSlot].Id != AbilityId.SanguineRain)
                {
                    EndRain(p, false);
                    continue;
                }
                p.RainClock += dt;
                if (p.RainClock < RainCast)
                {
                    PayHealth(p, p.MaxHp * RainUpfront * dt / RainCast);
                    continue;
                }
                if (!RainRaining(p))
                    continue;
                PayHealth(p, p.MaxHp * RainDrain * dt);
                p.RainAcc += dt;
                float frac = Mastered(p, AbilityId.SanguineRain) ? RainHealMastered : RainHeal;
                while (p.RainAcc >= RainTick)
                {
                    p.RainAcc -= RainTick;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed)
                            Heal(q, q.MaxHp * frac, p);
                }
                if (p.Hp <= p.MaxHp * RainFloor)
                    EndRain(p, false);
            }
        }
    }
}

using System;

namespace Arena
{
    /// The Tank role's passive, Ironhide: a shield of IronhideMax that takes IronhideShare of each hit (the rest still reaches
    /// health, so wounds still build; owner Nov 24) after ability shields, before a shrine's health past max, and refills at IronhideRegen a second once you've gone IronhideDelay seconds
    /// without taking damage. Going down empties it; it starts full. Healer has the aura and DPS Momentum; a Wildcard
    /// has none. Kept as damage taken off the shield (Player.IronLost) so a new player starts full with no set-up.
    public partial class Game
    {
        public const float IronhideMax = 25f, IronhideShare = 0.5f, IronhideRegen = 25f, IronhideDelay = 3f;
        /// Tools only: SimTest turns Ironhide off so the many tests that read a Tank's health loss stay exact
        /// (the `ironhide` test turns it back on). Never set in the game.
        public static bool IronhideOff;

        /// The shield a player has now: 0 for anyone but a Tank.
        public static float Ironhide(Player p) => p.Role == Role.Tank && !IronhideOff ? Math.Max(0f, IronhideMax - p.IronLost) : 0f;

        /// Damage (Combat.cs): any damage that reaches the shield restarts the wait; the shield takes IronhideShare of the hit
        /// (all it has left at most) and the rest goes on to health.
        float IronhideSoak(Player p, float amount)
        {
            if (p.Role != Role.Tank || amount <= 0f || IronhideOff)
                return amount;
            p.IronWait = IronhideDelay;
            float soak = Math.Min(Ironhide(p), amount * IronhideShare);
            p.IronLost += soak;
            return amount - soak;
        }

        void TickIronhide(Player p, float dt)
        {
            if (p.Role != Role.Tank)
                return;
            if (p.Downed)
            {
                p.IronLost = IronhideMax;
                p.IronWait = IronhideDelay;
                return;
            }
            if (p.IronWait > 0f)
                p.IronWait = Math.Max(0f, p.IronWait - dt);
            else if (p.IronLost > 0f)
                p.IronLost = Math.Max(0f, p.IronLost - IronhideRegen * dt);
        }
    }
}

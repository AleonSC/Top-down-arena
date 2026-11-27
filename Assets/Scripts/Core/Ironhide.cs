using System;

namespace Arena
{
    /// The Tank role's passive, Ironhide: a shield that takes IronhideShare of each hit (the rest still reaches health, so
    /// wounds still build; owner Nov 24) after ability shields, before a shrine's health past max, and refills at
    /// IronhideRegen a second once you've gone IronhideDelay seconds without taking damage. Going down empties it; it
    /// starts full. Healer has the aura and DPS Momentum; a Wildcard has none. Kept as damage taken off the shield
    /// (Player.IronLost) so a new player starts full with no set-up.
    /// Cards (Tank only): Iron Plates (+IronPlatesBonus), Quick Refit (the wait IronRefitCut shorter, the refill
    /// IronRefitRegen faster) and Shared Plating (your whole shield halved, so every plate counts half; every other player
    /// gets a fixed IronShareGift shield that works like Ironhide at the base share, wait and refill; it never stacks).
    /// Knights (every role) add Chainmail: KnightMail more on the same shield (same share, wait and refill; on a Tank Knight
    /// it stacks with Ironhide, and Shared Plating doesn't halve it).
    /// The host works out each player's cap (Player.IronCap, in the snapshot) every tick.
    public partial class Game
    {
        public const float IronhideMax = 25f, IronhideShare = 0.5f, IronhideRegen = 25f, IronhideDelay = 3f;
        public const float KnightMail = 15f;  // a Knight's Chainmail
        public const float IronPlatesBonus = 15f, IronRefitCut = 0.5f, IronRefitRegen = 0.25f, IronShareGift = 25f, IronShareKeep = 0.5f;
        /// Tools only: SimTest turns Ironhide off so the many tests that read a Tank's health loss stay exact
        /// (the `ironhide` test turns it back on). Never set in the game.
        public static bool IronhideOff;

        /// The shield a player has now: 0 for anyone without one (a Tank, a Knight, or an ally of a Tank with Shared Plating).
        public static float Ironhide(Player p) => IronhideOff ? 0f : Math.Max(0f, p.IronCap - p.IronLost);

        /// A player's own shield: a Tank's Ironhide (the base plus Iron Plates, halved by Shared Plating) plus a Knight's Chainmail.
        public static float OwnIronCap(Player p) => (p.Role != Role.Tank ? 0f
            : (IronhideMax + p.Stats.IronBonus) * (p.Stats.SharedPlating ? IronShareKeep : 1f)) + (p.Kit == Kit.Knight ? KnightMail : 0f);

        /// The shield a role and class start with, before cards (the lobby's stats).
        public static float StartShield(Role r, Kit k) => (r == Role.Tank ? IronhideMax : 0f) + (k == Kit.Knight ? KnightMail : 0f);

        /// The most a player's shield holds: their own Ironhide plus the Shared Plating gift from another Tank (once however
        /// many give it).
        float IronCapOf(Player p)
        {
            if (IronhideOff)
                return 0f;
            float cap = OwnIronCap(p);
            foreach (var q in Players.Values)
                if (q != p && q.Role == Role.Tank && q.Stats.SharedPlating)
                    return cap + IronShareGift;
            return cap;
        }

        /// The wait and refill rate: a Tank's own Quick Refit cards; anyone else has the base ones.
        static float IronDelayOf(Player p) => Math.Max(0f, IronhideDelay - (p.Role == Role.Tank ? p.Stats.IronDelayCut : 0f));
        static float IronRegenOf(Player p) => IronhideRegen * (p.Role == Role.Tank ? p.Stats.IronRegenMult : 1f);

        /// Damage (Combat.cs): any damage that reaches the shield restarts the wait; the shield takes IronhideShare of the hit
        /// (all it has left at most) and the rest goes on to health.
        float IronhideSoak(Player p, float amount)
        {
            if (p.IronCap <= 0f || amount <= 0f || IronhideOff)
                return amount;
            p.IronWait = IronDelayOf(p);
            float soak = Math.Min(Ironhide(p), amount * IronhideShare);
            p.IronLost += soak;
            return amount - soak;
        }

        void TickIronhide(Player p, float dt)
        {
            p.IronCap = IronCapOf(p);
            if (p.IronCap <= 0f)
            {
                p.IronLost = p.IronWait = 0f;
                return;
            }
            if (p.IronLost > p.IronCap)
                p.IronLost = p.IronCap;  // (the cap shrank: Shared Plating)
            if (p.Downed)
            {
                p.IronLost = p.IronCap;
                p.IronWait = IronDelayOf(p);
                return;
            }
            if (p.IronWait > 0f)
                p.IronWait = Math.Max(0f, p.IronWait - dt);
            else if (p.IronLost > 0f)
                p.IronLost = Math.Max(0f, p.IronLost - IronRegenOf(p) * dt);
        }

        // --- Network: the player's Ironhide fields (SyncPlayer) ---

        void SyncIronhide(NetSync s, Player p)
        {
            s.Float(ref p.IronLost);  // Ironhide.cs
            s.Float(ref p.IronWait);
            s.Float(ref p.IronCap);
        }
    }
}

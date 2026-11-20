using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Greatsword's chain clock (TickGreat) and its reset (ResetGreatChain); the chain itself is in GreatswordChain.cs.
    public partial class Game
    {
        public const int GreatTemperMax = 6;  // a Rend's Power is Temper / this (1: Eclipse's gold lance)
        public const float GreatRendFxTime = 0.5f;

        /// Host, every tick: the chain forgets its place, everything goes when the sword is put away.
        void TickGreat(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.GreatGap = Math.Min(p.GreatGap + dt, 5f);
                if (p.Weapon.Id != WeaponId.Greatsword)
                {
                    p.GreatStep = 0;
                    continue;
                }
                if (p.GreatStep != 0 && p.GreatGap > GreatMoves[(int)GreatMoveOf(p.GreatStep)].Dur / FireRate(p) + GreatChainWindow)
                    p.GreatStep = 0;
            }
        }

        /// Raising the parry (or throwing the Moonwheel) cancels the chain: back to Undertow, a hit still winding up never lands.
        void ResetGreatChain(Player p)
        {
            p.GreatStep = 0;
            p.GreatGap = 5f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Delay > 0f && s.Great != 0);
            steelWalls.RemoveAll(c => c.Owner == p.Id && c.Wait > 0f);  // (and the crescent that was waiting for it)
        }
    }
}

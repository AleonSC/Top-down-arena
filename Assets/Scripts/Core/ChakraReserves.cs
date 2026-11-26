using System;

namespace Arena
{
    /// Chakra Reserves, the Healer Ninja's 4th choice (`AbilityId.ChakraReserves`, `AbilityDef.Fourth`: the lobby's 4th
    /// ability key; mockup https://claude.ai/artifact/TRfxeCuY27ZcNHay8sJYrJ). The Ninja pours chakra into the living teammate
    /// nearest the cursor within CrRange of them: it heals CrHeal plus CrMissing of their missing health, gives them a
    /// **Chakra Surge** (Player.CrSurgeT: CrMove move speed in Player.Speed, CrFire attack speed in FireRate) for CrSurgeTime s,
    /// and refreshes every ability they have but the ultimate (and Chakra Reserves itself): each is back to full charges.
    /// Cooldowns run on each player's own game, so the refresh goes out as Msg.CooldownCut with a share of 1 (ReaperLoot.cs).
    /// With no teammate in reach it goes on the Ninja: CrSolo of the heal and the Surge, no refresh.
    /// Mastered, a second thread reaches the next teammate nearest the first (within CrRange of the Ninja): CrSolo of the heal
    /// and the full Surge, no refresh. The card Deep Reserves: +CrCardTime s of Surge and +CrCardHeal heal a stack.
    /// Every screen draws the thread and the lotus from `FxKind.ChakraThread` and the Surge from Player.CrSurgeT
    /// (Game/ChakraReservesLook.cs).
    public partial class Game
    {
        public const float CrCooldown = 28f, CrRange = 460f, CrHeal = 40f, CrMissing = 0.15f, CrSolo = 0.5f;
        public const float CrSurgeTime = 5f, CrMove = 1.25f, CrFire = 1.35f;
        public const float CrCardTime = 1.5f, CrCardHeal = 10f;
        public const float CrFxDur = 0.95f;  // the thread reaches them by CrFxReach, the lotus blooms and fades by the end
        public const float CrFxReach = 0.22f;

        void CastChakraReserves(Player p, V2 cursor, float pw, bool mastered)
        {
            Player to = null;
            foreach (var q in Players.Values)
                if (q != p && !q.Downed && q.Pos.WithinReach(p.Pos, CrRange) && (to == null || q.Pos.DistanceSq(cursor) < to.Pos.DistanceSq(cursor)))
                    to = q;
            float surge = (CrSurgeTime + p.Stats.CrBonusTime) * pw;
            if (to == null)
            {
                Chakra(p, p, CrSolo, surge * CrSolo, false);
                return;
            }
            Chakra(p, to, 1f, surge, true);
            if (!mastered)
                return;
            Player next = null;
            foreach (var q in Players.Values)
                if (q != p && q != to && !q.Downed && q.Pos.WithinReach(p.Pos, CrRange) && (next == null || q.Pos.DistanceSq(to.Pos) < next.Pos.DistanceSq(to.Pos)))
                    next = q;
            if (next != null)
                Chakra(p, next, CrSolo, surge, false);
        }

        /// One thread of chakra from p to q: the heal (share of it), the Surge, and the refresh when asked.
        void Chakra(Player p, Player q, float share, float surge, bool refresh)
        {
            Heal(q, share * (CrHeal + p.Stats.CrBonusHeal + CrMissing * Math.Max(0f, q.MaxHp - q.Hp)), p);
            q.CrSurgeT = Math.Max(q.CrSurgeT, surge);
            q.CrSurgeMax = Math.Max(q.CrSurgeT, 0.01f);
            if (refresh)
                RefreshCooldowns(q);
            if (q != p)
                castHits++;
            BroadcastFx(FxKind.ChakraThread, p.Pos, q.Pos, new Rgba(0.37f, 0.95f, 0.82f), q.Id + (refresh ? 0.5f : 0f), CrFxDur, Sfx.SpellHoly);
        }

        /// Every ability but the ultimate (and Chakra Reserves) back to full charges, on the host and on that player's own game.
        void RefreshCooldowns(Player q)
        {
            for (int i = 0; i < q.Slots.Length; i++)
            {
                var sl = q.Slots[i];
                if (i == Upgrades.UltSlot || sl.Id == AbilityId.None || sl.Id == AbilityId.ChakraReserves)
                    continue;
                if (sl.Charges < sl.MaxCharges || sl.Timer > 0f)
                    CutCooldown(q, sl.Id, 1f);
            }
        }

        /// The Surge's move and attack speed, for Player.Speed and FireRate.
        public static float CrMoveMult(Player p) => p.CrSurgeT > 0f && !p.Downed ? CrMove : 1f;
        public static float CrFireMult(Player p) => p.CrSurgeT > 0f && !p.Downed ? CrFire : 1f;
    }
}

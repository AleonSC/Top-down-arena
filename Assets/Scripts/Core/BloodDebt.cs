using System;

namespace Arena
{
    /// The DPS Hemomancer's Blood Debt (`AbilityId.FrenzyPact` kept; was Frenzy Pact). Your health drops to a quarter and
    /// what it took becomes a debt; for 8 s kills pay it back (and your hits heal 15% instead of 3%). Pay it all and the
    /// 15% stays 2 s longer; what's still owed when the time runs out is gone.
    /// Player.DebtT, Debt and DebtOwed are replicated so everyone can draw the ledger: Game/BloodDebtLook.cs (look A
    /// "Ledger" of Docs/Mockups/blood-debt.html).
    public partial class Game
    {
        public const float DebtFloor = 0.25f;        // the cast leaves you at this share of your max health...
        public const float DebtMinCost = 0.05f;      // ...or costs this share, if you're already at or below the floor
        public const float DebtTime = 8f;            // seconds to pay it back
        public const float DebtPaidBonus = 2f;       // paid in full: the lifesteal lasts this much longer
        const float DebtSteal = 0.12f;               // on top of the class's 3%: 15% while the debt runs
        const float DebtGrunt = 0.04f, DebtHeavy = 0.10f;  // a kill repays this share of your max health (Heavy, elite or boss: the second)
        const float DebtBossShare = 0.10f;           // hurting a boss repays this share of the damage
        const float DebtMastered = 1.5f;             // mastered: kills repay this much more

        /// Host: Blood Debt was cast.
        void BorrowDebt(Player p, float power, bool mastered)
        {
            float floor = p.MaxHp * DebtFloor, took;
            if (p.Hp > floor + 1f)
            {
                took = p.Hp - floor;
                p.Hp = floor;
            }
            else
                took = PayHealth(p, p.MaxHp * DebtMinCost);
            p.DebtT = DebtTime + p.Stats.DebtBonusTime;
            p.Debt = p.DebtOwed = took;
            p.DebtRepay = (mastered ? DebtMastered : 1f) * (1f + p.Stats.DebtRepay) * power;
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            if (took >= 1f)
                BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), blood, took, 0.55f, Sfx.None);  // the blood it took spurting off you
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.8f, 0.35f), 60f, 0.4f, Sfx.Roar);
        }

        /// Host, from the class's kill hook: whoever landed the last hit pays back some of their debt.
        void DebtKill(Enemy e)
        {
            var p = PlayerOrNull(e.LastHitBy);
            if (p == null || p.Downed || p.Debt <= 0f || e.Def.Xp <= 0)
                return;
            float share = e.Def.IsBoss || e.Def.Heavy || e.Elite ? DebtHeavy : DebtGrunt;
            RepayDebt(p, p.MaxHp * share * p.DebtRepay, e.Pos, true);
        }

        /// Health comes back against the debt (never more than is owed). A kill's coins fly; a boss's trickle doesn't.
        void RepayDebt(Player p, float amount, V2 from, bool coins)
        {
            amount = Math.Min(amount, p.Debt);
            if (amount <= 0f || p.Downed)
                return;
            p.Debt -= amount;
            Heal(p, amount, p, "Blood Debt");
            if (coins)
                BroadcastFx(FxKind.DebtCoin, from, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), amount, 0.6f, Sfx.None);
            if (p.Debt > 0.01f)
                return;
            p.Debt = 0f;  // paid in full: a gold stamp, and the lifesteal stays a little longer
            p.DebtT += DebtPaidBonus;
            BroadcastFx(FxKind.DebtSettle, p.Pos, new V2(p.Id, 1f), new Rgba(1f, 0.8f, 0.35f), p.DebtOwed, 1.2f, Sfx.SpellHoly);
        }

        /// Host, every tick: the window runs out; whatever is still owed is lost.
        void TickDebt(Player p, float dt)
        {
            if (p.DebtT <= 0f)
                return;
            p.DebtT -= dt;
            if (p.DebtT <= 0f)
                ForfeitDebt(p, true);
        }

        /// The debt ends unpaid (time ran out, or you went down: then quietly).
        void ForfeitDebt(Player p, bool show)
        {
            if (show && p.Debt > 0.5f)
                BroadcastFx(FxKind.DebtSettle, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), p.Debt, 1.2f, Sfx.Hit);
            p.DebtT = 0f;
            p.Debt = 0f;
        }
    }
}

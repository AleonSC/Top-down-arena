using System;

namespace Arena
{
    /// The smart bot's Dodge (BotBrain.cs; the Dodge itself is Dodge.cs), used like a careful player would, not on cooldown:
    /// - Evade: when every step it could walk still gets it hit (its best step's danger over the skill's nerve) and a dodge
    ///   comes out much safer (danger only counted once the dodge's invulnerability runs out), it dodges that way. A shot a
    ///   ready perfect parry would take is left to the parry.
    /// - Travel: far from where it wants to be (the target at range, a downed ally, the next room), nothing to shoot and
    ///   nothing coming, it holds the dodge on into a sprint.
    /// Each skill waits its own gap between dodges on top of the cooldown, so even an Expert keeps them for when they count.
    public partial class Game
    {
        V2 botDashDir;             // ChooseMove's best dodge this think, and how dangerous it looked (99: none worth it)
        float botDashDanger = 99f, botWalkDanger;
        float botLastDodge = -99f, botLastSprint = -99f, botDodgeHoldT;
        public int BotDodges, BotSprints;  // evasive dodges and travel sprints made (tests, Boss Lab)

        public const float BotSprintFar = 450f;  // farther than this past where it wants to be: worth a sprint

        /// Each skill's dodge habits: Nerve, how dangerous its best step has to look before it dodges; Gap, seconds since its
        /// last evasive dodge; SprintGap, seconds between travel sprints.
        public static void DodgeSkillOf(BotSkill s, out float nerve, out float gap, out float sprintGap)
        {
            switch (s)
            {
                case BotSkill.Casual: nerve = 2.5f; gap = 3f; sprintGap = 7f; return;
                case BotSkill.Expert: nerve = 1.4f; gap = 1.5f; sprintGap = 3.5f; return;
                default: nerve = 2f; gap = 2.2f; sprintGap = 5f; return;
            }
        }

        bool BotCanDodge(Player me) => dodgeCd <= 0f && DashT <= 0f && !me.Downed && me.StunT <= 0f && !me.StandOn && !HoldControls && !KatanaStriking;

        /// ChooseMove's dodge options: each of the 8 ways, 95 px in DodgeTime then walking, hits only counted after the invulnerability.
        void ScoreDodges(Player me, SkillParams sk, MoveScene ms, float speed, V2 anchor, float pref, Player stayNear, float hitR, int lockedArea, float walkDanger)
        {
            botWalkDanger = walkDanger;
            botDashDanger = 99f;
            DodgeSkillOf(BotLevel, out float nerve, out float gap, out _);
            if (walkDanger < nerve || botClock - botLastDodge < gap || !BotCanDodge(me))
                return;
            float T = sk.Lookahead, step = T / sk.Steps, dashSpeed = DodgeDist / DodgeTime;
            float bestCost = float.MaxValue;
            for (int c = 0; c < 8; c++)
            {
                V2 dir = V2.FromAngle(MathX.Tau * c / 8f);
                float d = 0f;
                V2 p = me.Pos;
                for (int k = 1; k <= sk.Steps; k++)
                {
                    float t = k * step;
                    p = me.Pos + dir * (Math.Min(t, DodgeTime) * dashSpeed + Math.Max(0f, t - DodgeTime) * speed);
                    if (t < DodgeInvuln)
                        continue;  // nothing can hurt it yet
                    float w = 1.6f - t / T;
                    AddShotDanger(ref d, me, sk, ms, p, t, w, hitR);
                    AddEnemyDanger(ref d, me, sk, ms, p, t, w, hitR);
                    AddArenaBossDanger(ref d, me, sk, ms, p, t, w, hitR);
                    AddLateBossDanger(ref d, me, sk, ms, p, t, w, hitR);
                }
                if (NearSolid(me.Pos + dir * DodgeDist * 0.6f, hitR))
                    d += 3f;  // into a wall: the dodge stops short
                AddEdgeDanger(ref d, p, hitR, ms, lockedArea);
                float cost = d + PlaceCost(me, ms, p, anchor, pref, stayNear, hitR) * 0.5f;
                if (cost < bestCost)
                {
                    bestCost = cost;
                    botDashDir = dir;
                    botDashDanger = d;
                }
            }
        }

        /// Sets this tick's dodge press and hold (and turns `move` the way of an evasive dodge).
        void BotDodge(Player me, SkillParams sk, float dt, V2 anchor, float pref, bool firing, bool parry, ref V2 move, ref bool dodge, ref bool dodgeHeld)
        {
            dodge = false;
            dodgeHeld = botDodgeHoldT > 0f;
            botDodgeHoldT = Math.Max(0f, botDodgeHoldT - dt);
            if (parry || !BotCanDodge(me))
                return;
            DodgeSkillOf(BotLevel, out float nerve, out float gap, out float sprintGap);

            // --- Evade: a hit it can't walk out of, and a dodge that clears it. A ready perfect parry takes shots instead.
            if (botDashDanger < 99f && botWalkDanger >= nerve && botDashDanger < botWalkDanger * 0.35f && botClock - botLastDodge >= gap
                && !(me.ParryBroken <= 0f && botParryDown >= BotParryRearm && me.ParryHp >= ParryMax(me) * 0.4f && FirstHit(me, 0.45f, out _, out _, out _)))
            {
                move = botDashDir;
                dodge = true;
                botLastDodge = botClock;
                botDashDanger = 99f;  // one dodge per think
                BotDodges++;
                return;
            }

            // --- Travel: a long way to go, nothing to shoot and nothing coming: dodge and hold on into a sprint.
            if (!firing && SprintT <= 0f && BotDanger < 0.3f && !move.IsZero && bowDraw < 0f && botClock - botLastSprint >= sprintGap
                && anchor.FartherThan(me.Pos, pref + BotSprintFar) && move.Normalized().Dot((anchor - me.Pos).Normalized()) > 0.7f)
            {
                dodge = dodgeHeld = true;
                botDodgeHoldT = SprintHold + 0.1f;
                botLastSprint = botClock;
                BotSprints++;
            }
        }
    }
}

using System;

namespace Arena
{
    /// The smart bot's Katana (Docs/KatanaCombo.md): it plays the two stance combos like a person would.
    /// - Store: a stance one step from its finisher (Hayate before the Momentum Burst, Senbon before Caltrops) is left
    ///   there by switching away, to build the other one.
    /// - Pair: with both stored it waits a moment (rolled each time, never past the 1.5 s reset) for a crowd, a boss in
    ///   reach or a volley, then plays this stance's finisher, switches and plays the other's straight after.
    ///   Sometimes it doesn't wait and plays one finisher on its own.
    /// - Cancel: sometimes it leaves a combo halfway for the other stance's opening moves: Hayate's dash-cuts to close
    ///   on a far target or slip a volley (untouchable), Senbon's wide sweeps when a crowd hugs it.
    /// Called from the Katana's Alt rule (BotWeaponAlt.cs); it may hold the trigger (`firing`).
    public partial class Game
    {
        int botKatIdx = -1, botKatStance = -1, botKatLastFin = -1;
        bool botKatPair;           // playing both finishers back to back
        float botKatHoldT, botKatPatience = -1f, botKatFinT = 99f, botKatCancelCd;
        Random botKatRng;
        public int BotKatanaPairs, BotKatanaStores, BotKatanaCancels, BotKatanaFinishers;  // (tests)

        static bool KatanaPrimed(int stance, int idx) => idx == (stance == 0 ? 3 : 4);
        static int KatanaFinisherCode(int stance) => stance == 0 ? 3 : 12;

        bool BotKatana(AltView v, float dt, bool late, ref bool firing)
        {
            var me = v.Me;
            if (botKatRng == null)
                botKatRng = NewRng();
            botKatFinT += dt;
            botKatCancelCd -= dt;
            int cur = me.KatanaStance, other = cur ^ 1;
            int ci = cur == 0 ? me.KatanaHay : me.KatanaSen, oi = cur == 0 ? me.KatanaSen : me.KatanaHay;

            // A finisher just played: this stance went from its finisher back to step 1 (an idle reset leaves the last step
            // played as the one before the finisher).
            bool finished = botKatStance == cur && KatanaPrimed(cur, botKatIdx) && ci == 0 && me.KatanaStep == KatanaFinisherCode(cur);
            botKatIdx = ci;
            botKatStance = cur;
            if (finished)
            {
                BotKatanaFinishers++;
                if (botKatLastFin == other && botKatFinT < 1.5f)
                {
                    BotKatanaPairs++;
                    botKatPair = false;
                }
                botKatLastFin = cur;
                botKatFinT = 0f;
            }
            if (botKatPair && botKatFinT > 1.5f && botKatLastFin >= 0 && !KatanaPrimed(cur, ci))
                botKatPair = false;  // the pair fell through (the other was lost to a parry)
            if (late || v.Near == 0)
            {
                botKatHoldT = 0f;
                botKatPatience = -1f;
                return false;
            }

            bool curPrimed = KatanaPrimed(cur, ci), otherPrimed = KatanaPrimed(other, oi);
            bool crowd = EnemiesNear(me.Pos, 150f) >= 3, boss = v.Target != null && v.Target.Def.IsBoss && v.InReach;
            bool moment = crowd || boss || v.Incoming >= 2 || BotDanger > 2f;
            bool canSwitch = katanaPlantT <= 0f && katanaSwitchLocalCd <= 0f && !botAltWas;  // (LocalKatana drops a press while planted)
            bool justFinished = botKatLastFin == cur && botKatFinT < 1f && ci == 0;

            // --- Pair: the first finisher has played and the other is stored: switch to it (the trigger waits so this stance
            // doesn't start over), then the trigger plays the second.
            if (botKatPair && justFinished && otherPrimed)
            {
                firing = false;
                return canSwitch && SwitchStance();
            }
            if (botKatPair && curPrimed && botKatLastFin == other && botKatFinT < 1.5f)
                return false;

            if (curPrimed)
            {
                if (otherPrimed)
                {
                    if (botKatPair)
                        return false;  // the first finisher is on its way
                    // Both stored: wait for a good moment, but never long enough for this stance to start over.
                    if (botKatPatience < 0f)
                        botKatPatience = botKatRng.NextDouble() < 0.25 ? 0f : 0.4f + (float)botKatRng.NextDouble() * 0.7f;
                    if (moment || botKatHoldT >= botKatPatience)
                    {
                        botKatPair = true;
                        botKatHoldT = 0f;
                        botKatPatience = -1f;
                        return false;  // fire this finisher; the switch follows it
                    }
                    if (firing)
                        botKatHoldT += dt;
                    firing = false;
                    return false;
                }
                // Only this one ready: store it and build the other (most of the time), unless a crowd wants it now.
                if (botKatPatience < 0f)
                    botKatPatience = botKatRng.NextDouble() < (moment ? 0.35 : 0.8) ? 1f : 0f;
                if (botKatPatience > 0f)
                {
                    firing = false;
                    if (canSwitch && SwitchStance())
                    {
                        BotKatanaStores++;
                        botKatPatience = -1f;
                        return true;
                    }
                }
                return false;
            }
            botKatPatience = -1f;
            botKatHoldT = 0f;

            // --- After a lone finisher, carry on building the other stance if it was started.
            if (justFinished && oi > 0 && !otherPrimed)
                return canSwitch && SwitchStance();

            // --- Cancel: leave this combo halfway for the other stance's opening moves.
            if (botKatCancelCd <= 0f && ci > 0 && canSwitch)
            {
                bool want = false;
                if (cur == 1 && oi <= 1)  // Hayate's dash-cuts: close on a far target, or slip a volley untouchable
                    want = v.ShootD > 150f && v.ShootD < 330f || v.Incoming >= 2;
                else if (cur == 0 && oi <= 1 && ci < 3)  // Senbon's wide sweeps: a crowd hugging the bot
                    want = EnemiesNear(me.Pos, 110f) >= 3;
                if (want)
                {
                    botKatCancelCd = 2.5f;  // rolled once a window, so it's only sometimes
                    if (botKatRng.NextDouble() < 0.6 && SwitchStance())
                    {
                        BotKatanaCancels++;
                        return true;
                    }
                }
            }
            return false;
        }

        bool SwitchStance() => Tap(true, KatanaSwitchCd + 0.02f);

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): store a finisher, pair both finishers, cancel for opening moves
        static bool KatanaBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            return g.BotKatana(v, dt, late, ref firing);
        }
    }
}

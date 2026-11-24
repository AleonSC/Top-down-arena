using System;

namespace Arena
{
    /// Slowed and Chilled are two statuses.
    ///   Slowed: any slow (Game.Slow, Slows.cs): wind, honey, gas, ink, Graviton... It only slows, never builds to a freeze.
    ///   Chilled: ice only. Every ice attack goes through Chill: it slows (its own amount and time) and adds chill stacks
    ///     (Enemy.FrostChill, the pips under it; they wear off FlChillHold s after the last one). The FlFreezeAt-th stack freezes
    ///     it (FlFreezeTime, heavies FlHeavyFreeze, + the attacker's freeze bonus) and lifts its slows, since the ice holds it
    ///     still anyway. Bosses are only slowed and shown chilled (Enemy.IceSlowT), never stacked. Steady ice (Blizzard, Cryo
    ///     Rounds, Ice Shards) passes gated: such a source adds at most one stack a ChillGate s per enemy.
    ///   Sources: Frost Lance and Absolute Zero (FrostLance.cs), Frost Nova (Minions.cs), Blizzard (Minions.cs TickZones), Cryo
    ///     Rounds (Combat.cs), Ice Shards (Combat.cs), Wind Chill (Wind.cs). Glacial Tomb and Blood Freeze freeze outright.
    /// Replicated: Enemy.Chilled (status bit 4), Enemy.Slowed (its own bool), Enemy.FrostChill (the pips). Frozen means real ice
    /// only (IceFrozen), never a strong slow.
    public partial class Game
    {
        public const float ChillGate = 1f, IceShardSlow = 0.25f, IceShardChillTime = 1.5f;

        /// Host: an ice hit on e: slowed to slowMult for slowFor s, plus `stacks` chill. True if that froze it.
        internal bool Chill(Enemy e, int stacks, float slowFor, float slowMult, bool gated = false, float freezeBonus = 0f)
        {
            if (e == null || e.Hp <= 0f || IceFrozen(e))
                return false;
            Slow(e, slowFor, slowMult);
            e.IceSlowT = Math.Max(e.IceSlowT, slowFor);
            if (stacks <= 0 || e.Def.IsBoss)
                return false;
            if (gated)
            {
                if (e.ChillGateT > 0f)
                    return false;
                e.ChillGateT = ChillGate;
            }
            e.FrostChillT = FlChillHold;
            e.FrostChill = (byte)Math.Min(FlFreezeAt, e.FrostChill + stacks);
            if (e.FrostChill < FlFreezeAt)
                return false;
            e.FrostChill = 0;
            e.FrostChillT = 0f;
            e.FrozenT = Math.Max(e.FrozenT, (e.Def.Heavy ? FlHeavyFreeze : FlFreezeTime) + freezeBonus);
            e.SlowT = e.SlowT2 = e.IceSlowT = 0f;  // frozen solid: the slows are lifted (the ice holds it)
            e.SlowMult = e.SlowMult2 = 1f;
            BroadcastFx(FxKind.FrostBurst, e.Pos, FlColor, 0f, 2.4f, Sfx.FrostFreeze);
            return true;
        }

        void TickChill(Enemy e, float dt)
        {
            e.IceSlowT = Math.Max(0f, e.IceSlowT - dt);
            e.ChillGateT = Math.Max(0f, e.ChillGateT - dt);
        }

        /// Slowed, for drawing: any slow that moves it (heavies and bosses shrug off movement slows: EnemyAI.cs), or a wade.
        bool ShowsSlowed(Enemy e) => !IceFrozen(e) && ((e.SlowMult < 1f && e.SlowT > e.StunT + StunSlack && !Heavy(e)) || (e.WadeT > 0f && e.WadeMult < 1f));
    }
}

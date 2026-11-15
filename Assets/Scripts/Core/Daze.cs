using System;

namespace Arena
{
    /// Daze: a close-in hit (Bulldoze, Knight and Lance swings, the Katana's burst, the Bo Staff, the Kusarigama's whirl, Out of Ammo's bashes, knife and spin)
    /// stops the enemy attacking for a split second, so charging into a pack isn't a guaranteed hit back. A dazed enemy
    /// still moves, but doesn't shoot, run its special attacks or hurt by touch, and a wind-up it was in starts over
    /// (its full glow plays again before it fires). Bosses are dazed for half as long. After a daze an enemy can't be
    /// dazed again for DazeImmune, so steady swings can't lock it down. Host only; `Enemy.Dazed` is replicated for the
    /// stars over its head (WorldEnemies.cs).
    public partial class Game
    {
        public const float DazeTime = 0.4f, DazeBossTime = 0.2f, DazeImmune = 0.8f;

        /// Daze `e` (a close-in hit just landed on it).
        void Daze(Enemy e)
        {
            if (e.DazeImmuneT > 0f || e.Hp <= 0f || e.SpawnT > 0f || e.Dummy)
                return;
            float t = e.Def.IsBoss ? DazeBossTime : DazeTime;
            e.DazeT = t;
            e.DazeImmuneT = t + DazeImmune;
            e.Winding = false;
        }

        /// Stun `e` for `t` s: it can't move or shoot (as FrozenT holds it), but it isn't frozen: no ice block, frost tint or chill,
        /// just the daze stars over its head (the Arc Knuckles' Uppercut, Twin Hammer, grab and throw).
        void Stun(Enemy e, float t)
        {
            e.FrozenT = Math.Max(e.FrozenT, t);
            e.StunT = Math.Max(e.StunT, t);
        }

        /// Is `e` held by real ice, not only a Stun (a freeze lasting longer than the stun counts as ice)?
        internal static bool IceFrozen(Enemy e) => e.FrozenT > e.StunT + StunSlack;

        /// Is `e` slowed by something other than a Stun (EnemyAI keeps SlowT at FrozenT while it's held)?
        internal static bool IceSlowed(Enemy e) => e.SlowT > e.StunT + StunSlack;

        const float StunSlack = 0.02f;

        /// Each tick, for each enemy.
        static void TickDaze(Enemy e, float dt)
        {
            e.DazeT = Math.Max(0f, e.DazeT - dt);
            e.StunT = Math.Max(0f, e.StunT - dt);
            e.DazeImmuneT = Math.Max(0f, e.DazeImmuneT - dt);
        }
    }
}

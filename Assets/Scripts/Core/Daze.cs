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

        /// Each tick, for each enemy.
        static void TickDaze(Enemy e, float dt)
        {
            e.DazeT = Math.Max(0f, e.DazeT - dt);
            e.DazeImmuneT = Math.Max(0f, e.DazeImmuneT - dt);
        }
    }
}

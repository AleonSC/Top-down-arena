using System.Collections.Generic;

namespace Arena
{
    /// Enemies by type, gathered once per tick (or when the count changes), so a per-enemy check like a Worker's pack,
    /// a Rioter's rally or the Warden lookup reads a short list instead of scanning every enemy (O(n) instead of O(n^2)).
    /// Positions are read live from the enemies; an enemy removed mid-tick is skipped by StillIn.
    public partial class Game
    {
        readonly List<Enemy>[] byType = new List<Enemy>[256];  // (EnemyType is a byte)
        readonly List<EnemyType> byTypeUsed = new List<EnemyType>();
        bool byTypeStale = true;
        int byTypeCount = -1;

        /// Every game, at the start of each tick: the next EnemiesOf call regathers.
        void StaleEnemyIndex() => byTypeStale = true;

        /// The enemies of one type in play (may hold one removed earlier this tick: check StillIn). Don't keep the list.
        public List<Enemy> EnemiesOf(EnemyType t)
        {
            if (byTypeStale || byTypeCount != Enemies.Count)
            {
                foreach (var used in byTypeUsed)
                    byType[(int)used].Clear();
                byTypeUsed.Clear();
                foreach (var e in Enemies.Values)
                {
                    var list = byType[(int)e.Type] ?? (byType[(int)e.Type] = new List<Enemy>());
                    if (list.Count == 0)
                        byTypeUsed.Add(e.Type);
                    list.Add(e);
                }
                byTypeStale = false;
                byTypeCount = Enemies.Count;
            }
            return byType[(int)t] ?? (byType[(int)t] = new List<Enemy>());
        }

        /// Is this enemy still in the sim (not removed earlier this tick)?
        public bool StillIn(Enemy e) => Enemies.TryGetValue(e.Id, out var cur) && cur == e;
    }
}

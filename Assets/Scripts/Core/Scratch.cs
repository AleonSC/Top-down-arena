using System.Collections.Generic;

namespace Arena
{
    /// Reusable lists, so a per-tick "copy the enemies, then change them while walking" doesn't allocate every tick.
    /// Rent one, walk it, Return it (cleared). A list that is never returned (an early `return`) is just garbage collected.
    /// Nesting is safe: each Rent hands out a different list.
    public sealed class ListPool<T>
    {
        readonly Stack<List<T>> free = new Stack<List<T>>();
        public List<T> Rent() => free.Count > 0 ? free.Pop() : new List<T>();
        public void Return(List<T> list) { list.Clear(); free.Push(list); }
    }

    public partial class Game
    {
        readonly ListPool<Enemy> enemyLists = new ListPool<Enemy>();
        readonly ListPool<Bullet> bulletLists = new ListPool<Bullet>();
        readonly ListPool<Hazard> hazardLists = new ListPool<Hazard>();
        readonly ListPool<int> idLists = new ListPool<int>();

        /// A copy of every enemy, safe to walk while enemies are hurt, killed or spawned. Give it back with `Free`.
        List<Enemy> SnapEnemies()
        {
            var l = enemyLists.Rent();
            l.AddRange(Enemies.Values);
            return l;
        }

        /// A copy of a dictionary's keys. Give it back with `Free`.
        List<int> SnapKeys<V>(IDictionary<int, V> d)
        {
            var l = idLists.Rent();
            l.AddRange(d.Keys);
            return l;
        }

        void Free(List<Enemy> l) => enemyLists.Return(l);
        void Free(List<int> l) => idLists.Return(l);
    }
}

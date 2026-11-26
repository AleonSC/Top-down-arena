using System;
using System.Collections.Generic;

namespace Arena
{
    /// Tables of definitions keyed by id (Defs.Enemies, Kits.Zones, Kits.Minions): written as `[Id] = def` entries in any
    /// order, so a new id's definition can sit beside its relatives, and turned into an array indexed by the id's number.
    /// The ids keep their lists in name order (Tools/Ids/ids.py), so their numbers move; the table follows them.
    public static class IdTable
    {
        public static T[] Of<K, T>(Dictionary<K, T> byId) where K : struct where T : class
        {
            int n = 0;
            foreach (var k in byId.Keys)
                n = Math.Max(n, Convert.ToInt32(k) + 1);
            var a = new T[n];
            foreach (var kv in byId)
                a[Convert.ToInt32(kv.Key)] = kv.Value;
            for (int i = 0; i < n; i++)
                if (a[i] == null)
                    throw new InvalidOperationException(typeof(K).Name + " " + Enum.GetName(typeof(K), i) + " has no " + typeof(T).Name);
            return a;
        }
    }
}

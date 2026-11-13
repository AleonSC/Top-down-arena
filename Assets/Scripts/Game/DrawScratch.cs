using System.Collections.Generic;

namespace Arena
{
    /// Reused lists for the draw code's small bookkeeping (forgetting what's gone from a per-entity cache), so a frame
    /// allocates nothing for it. Only for a fill-then-use that calls nothing else that could use the same list.
    static class DrawScratch
    {
        public static readonly List<int> Ids = new List<int>();
        public static readonly List<Effect> Fx = new List<Effect>();

        public static List<int> FreshIds() { Ids.Clear(); return Ids; }
        public static List<Effect> FreshFx() { Fx.Clear(); return Fx; }
    }
}

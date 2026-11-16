using System;
using System.Collections.Generic;

namespace Arena
{
    /// The props in a grid of 128 px cells, so an enemy's prop checks look only at the props near it instead of every
    /// prop on the map (Survival's room holds about 160). A query returns the props in Props' own order, so every
    /// check gives exactly what a full pass would. The grid rebuilds when a prop is made, a prop crosses a cell
    /// (Prop.Pos) or the count changes.
    public partial class Game
    {
        const float PropCellSize = 128f;
        const float PropExtent = 28f;       // the widest prop's half size (a heavy crate's 27), plus a little
        const float PropOutMargin = 48f;    // PushOutOfProps: how far a push may carry the point before the full pass takes over
        internal static int PropStamp;      // bumped by every prop made or crossing a cell (all games share it: a spare rebuild is harmless)

        readonly Dictionary<int, List<Prop>> propCells = new Dictionary<int, List<Prop>>();
        readonly List<Prop> propOrder = new List<Prop>();
        readonly List<Prop> pushNear = new List<Prop>(), shoveNear = new List<Prop>(), aheadNear = new List<Prop>(), stopNear = new List<Prop>();
        int propGridStamp = -1, propGridCount = -1;

        internal static int PropCell(V2 at) => PropCellKey((int)Math.Floor(at.X / PropCellSize), (int)Math.Floor(at.Y / PropCellSize));
        static int PropCellKey(int cx, int cy) => (cx + 4096) * 8192 + cy + 4096;

        /// Every prop, in Props' order (each one's GridOrder is its index).
        List<Prop> PropOrder()
        {
            int stamp = PropStamp;
            if (stamp == propGridStamp && Props.Count == propGridCount)
                return propOrder;
            propGridStamp = stamp;
            propGridCount = Props.Count;
            propOrder.Clear();
            foreach (var l in propCells.Values)
                l.Clear();
            foreach (var p in Props.Values)
            {
                p.GridOrder = propOrder.Count;
                propOrder.Add(p);
                List<Prop> cell;
                if (!propCells.TryGetValue(p.GridCell, out cell))
                    propCells[p.GridCell] = cell = new List<Prop>();
                cell.Add(p);
            }
            return propOrder;
        }

        /// The props that may reach within reach of at (their centres within reach + PropExtent, by cell), in Props' order.
        List<Prop> PropsNear(V2 at, float reach, List<Prop> into)
        {
            into.Clear();
            var all = PropOrder();
            if (all.Count == 0)
                return into;
            float d = reach + PropExtent;
            int x0 = (int)Math.Floor((at.X - d) / PropCellSize), x1 = (int)Math.Floor((at.X + d) / PropCellSize);
            int y0 = (int)Math.Floor((at.Y - d) / PropCellSize), y1 = (int)Math.Floor((at.Y + d) / PropCellSize);
            if ((x1 - x0 + 1) * (y1 - y0 + 1) > propCells.Count)
            {
                into.AddRange(all);  // (a query wider than the props' cells: the lot)
                return into;
            }
            for (int cx = x0; cx <= x1; cx++)
                for (int cy = y0; cy <= y1; cy++)
                {
                    List<Prop> cell;
                    if (propCells.TryGetValue(PropCellKey(cx, cy), out cell))
                        into.AddRange(cell);
                }
            for (int i = 1; i < into.Count; i++)  // back into Props' order (a few at most: insertion sort)
            {
                var p = into[i];
                int j = i - 1;
                while (j >= 0 && into[j].GridOrder > p.GridOrder)
                {
                    into[j + 1] = into[j];
                    j--;
                }
                into[j + 1] = p;
            }
            return into;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Arena
{
    /// Dormant groups (Docs/FogOfWar.md, "Dormant groups"): when a piece card is built, each of its cells has a chance of holding a
    /// sleeping group of the faction enemies of the wave it was built on. They stand still (no moving, shooting, or counting for the
    /// wave) in the unseen room until it is seen (RoomFog.cs) or one of them is hit; then the room's groups wake and fight.
    public static class DormantGroups
    {
        public static float CellChance = 0.4f;  // each cell of a built piece: the chance it holds a group (static: tests set it)
        public const float Spread = 46f;        // the group's spacing, a tight cluster round the cell's middle

        /// How many enemies a group holds on this wave: 3, then one more every 15 waves, at most 6.
        public static int GroupSize(int wave) => Math.Min(6, 3 + Math.Max(0, wave) / 15);

        /// The middles of every grid cell in a room's boxes.
        public static List<V2> CellMids(IList<ExpansionRoom> boxes)
        {
            var mids = new List<V2>();
            int c = Expansion.GridCell;
            foreach (var b in boxes)
                for (int y = b.Y; y + c <= b.Y1; y += c)
                    for (int x = b.X; x + c <= b.X1; x += c)
                        mids.Add(new V2(x + c / 2f, y + c / 2f));
            return mids;
        }

        /// Where the k-th of n stands: a ring round the middle (one in the middle from 5 up).
        public static V2 Spot(V2 mid, int k, int n)
        {
            if (n >= 5 && k == 0)
                return mid;
            int ring = n >= 5 ? n - 1 : n, i = n >= 5 ? k - 1 : k;
            return mid + V2.FromAngle(MathX.Tau * i / ring + 0.4f) * Spread * (n >= 5 ? 1.6f : 1.1f);
        }
    }

    public partial class Game
    {
        /// The faction enemies a group can be drawn from on this wave: the coming boss's faction as far as the wave reaches into it,
        /// with no bosses, minis, planted things or pairs. Sorted, so the host's rolls repeat.
        public List<EnemyType> DormantPool(int wave)
        {
            wave = Math.Max(1, wave);
            var f = FactionFor(ApproachBossAt(wave));
            int reach = Math.Max(1, ApproachStepAt(wave));
            var pool = new List<EnemyType>();
            foreach (var m in f.Members)
            {
                var d = Defs.Get(m.Type);
                if (m.From > reach || d.IsBoss || d.Mini || IsPlanted(m.Type) || m.Type == EnemyType.SpawnPortal
                    || m.Type == EnemyType.BoundWraith || m.Type == EnemyType.Pylon || pool.Contains(m.Type))
                    continue;
                pool.Add(m.Type);
            }
            if (pool.Count == 0)
                pool.Add(f.Members[0].Type);
            pool.Sort();
            return pool;
        }

        /// Host, as a piece card is built: each cell rolls for a sleeping group of this wave's faction enemies. Returns how many groups.
        internal int PlantDormantGroups(IList<ExpansionRoom> boxes, bool atLeastOne = false, EnemyType? only = null, int groupSize = 0, bool all = false)
        {
            if (!IsHost || !ExpandingOn || boxes.Count == 0)
                return 0;
            var pool = only.HasValue ? new List<EnemyType> { only.Value } : DormantPool(Wave);
            var roll = new DetRandom(expandSeed * 31 + boxes[0].X * 7 + boxes[0].Y * 13 + Wave);
            int groups = 0, size = groupSize > 0 ? groupSize : DormantGroups.GroupSize(Wave);
            var mids = Expanded.Exists(Expansion.IsGridCore) ? CellLayouts.SleeperSlots(boxes) : new List<V2>();  // (a cell layout's sleeper slots; rooms off the lattice use their cells' middles)
            bool slots = mids.Count > 0;
            if (!slots)
                mids = DormantGroups.CellMids(boxes);
            for (int c = 0; c < mids.Count; c++)
            {
                var mid = mids[c];
                if (!all && roll.Float() >= DormantGroups.CellChance && !(atLeastOne && groups == 0 && c == mids.Count - 1))  // (a Combat piece always holds at least one group)
                    continue;
                var type = pool[roll.Int(pool.Count)];
                for (int k = 0; k < size; k++)
                {
                    V2 spot = DormantGroups.Spot(mid, k, size);
                    var e = SpawnEnemyAt(type, slots ? CellLayouts.Clear(spot, mid) : spot, 0f, 0);
                    e.Dormant = true;
                    e.Facing = (float)(roll.Float() * MathX.Tau);
                }
                groups++;
            }
            if (groups > 0)
                DoLog(groups + " dormant group(s) of " + size + " wait in a new room");
            return groups;
        }

        /// Host: every dormant enemy in the room at `at` wakes (the room seen, or one of them hit).
        void WakeDormant(V2 at)
        {
            if (!IsHost)
                return;
            int area = CurrentWorld.AreaAt(at);
            foreach (var e in Enemies.Values)
                if (e.Dormant && (area < 0 ? e.Pos.CloserThan(at, 300f) : CurrentWorld.AreaAt(e.Pos) == area))
                    e.Dormant = false;
        }

        /// Host, every frame (from TickRoomFog): a dormant enemy whose room is seen, or that a player looks at from close, wakes.
        void TickDormant()
        {
            List<V2> wake = null;
            foreach (var e in Enemies.Values)
                if (e.Dormant && (!UnseenAt(e.Pos) || FogSees(e.Pos, RoomFog.WakeReach)))
                    (wake ?? (wake = new List<V2>())).Add(e.Pos);
            if (wake != null)
                foreach (var p in wake)
                    WakeDormant(p);
        }

        /// Host, as the map resets: the old rooms' sleeping groups go with them.
        void ClearDormant()
        {
            var gone = new List<int>();
            foreach (var e in Enemies.Values)
                if (e.Dormant)
                    gone.Add(e.Id);
            foreach (int id in gone)
                Enemies.Remove(id);
        }
    }
}

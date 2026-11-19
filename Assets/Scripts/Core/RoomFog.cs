using System.Collections.Generic;

namespace Arena
{
    /// Room fog (Docs/FogOfWar.md): a room the map gets after the core starts unseen (`ExpansionRoom.Unseen`): dark on the
    /// maps and dim in the world, what's inside hidden, until a standing player walks in. Then the whole room (every box of
    /// its piece) is seen by the whole team for the rest of the run. Enemies still spawn in unseen rooms; bots scout them first.
    public static class RoomFog
    {
        public const float PeekReach = 450f;    // an enemy, coin or pickup in an unseen room shows to a player this close (from the doorway)
        public const float RevealTime = 0.6f;   // the gold sweep across a room as it's seen (the look)

        /// Whether a room the map gets starts unseen: everything but the core (a kept room is settled by the map reset).
        public static bool StartsUnseen(ExpansionRoom r) => !Expansion.IsGridCore(r);

        /// How many rooms (pieces, not boxes) are still unseen.
        public static int UnseenCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Unseen && Expansion.HeadOf(rooms, i) == i)
                    n++;
            return n;
        }
    }

    public partial class Game
    {
        /// Host: rooms from this index on are new since the last look (-1: settle quietly, the core or a loaded map).
        int roomFogMark = -1;

        /// Whether world area `area` (0 is the hub, area i is Expanded[i - 1]) is an unseen room.
        public bool AreaUnseen(int area) => ExpandingOn && area >= 1 && area <= Expanded.Count && Expanded[area - 1].Unseen;

        /// Whether the point is inside an unseen room.
        public bool UnseenAt(V2 p) => ExpandingOn && RoomsOn && AreaUnseen(CurrentWorld.AreaAt(p));

        /// Whether something at `p` is hidden by room fog: inside an unseen room and no standing player within peeking reach.
        public bool FogHides(V2 p)
        {
            if (!UnseenAt(p))
                return false;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Pos.CloserThan(p, RoomFog.PeekReach))
                    return false;
            return true;
        }

        /// Host: the map was replaced wholesale (a new core, a map reset, a loaded save): what's there now isn't new.
        void SettleRoomFog() => roomFogMark = -1;

        /// Host: rooms added since the last look start unseen. Run before every room list goes out, so nobody sees a new room lit.
        bool FogNewRooms()
        {
            if (!IsHost || !ExpandingOn)
                return false;
            if (roomFogMark < 0 || roomFogMark > Expanded.Count)
            {
                roomFogMark = Expanded.Count;
                return false;
            }
            bool changed = false;
            for (int i = roomFogMark; i < Expanded.Count; i++)
            {
                var r = Expanded[i];
                if (r.Unseen || !RoomFog.StartsUnseen(r))
                    continue;
                r.Unseen = true;
                Expanded[i] = r;
                changed = true;
            }
            roomFogMark = Expanded.Count;
            return changed;
        }

        /// Host, every frame: new rooms go dark; a standing player inside an unseen room reveals all of it for everyone.
        void TickRoomFog()
        {
            if (!IsHost || !ExpandingOn || Expanded.Count == 0)
                return;
            bool changed = FogNewRooms();
            if (RoomsOn)
                foreach (var p in Players.Values)
                {
                    if (p.Downed)
                        continue;
                    int area = CurrentWorld.AreaAt(p.Pos);
                    if (!AreaUnseen(area))
                        continue;
                    foreach (int i in Expansion.GroupOf(Expanded, area - 1))
                    {
                        var r = Expanded[i];
                        r.Unseen = false;
                        Expanded[i] = r;
                    }
                    DoLog("room seen by " + p.Name + " at " + Expanded[area - 1].X + "," + Expanded[area - 1].Y);
                    changed = true;
                }
            if (changed)
                BroadcastExpansion();
        }

        /// Bots: the middle of the nearest unseen room's box (null: none left), to scout it.
        public V2? NearestUnseen(V2 from)
        {
            if (!ExpandingOn || !RoomsOn)
                return null;
            var wd = CurrentWorld;
            V2? best = null;
            float bd = float.MaxValue;
            for (int a = 1; a < wd.Areas.Length; a++)
            {
                if (!AreaUnseen(a) || wd.Areas[a].Sealed)
                    continue;  // (a sealed room can't be walked into)
                V2 m = wd.Areas[a].Mid;
                float d = m.DistanceSq(from);
                if (d < bd)
                {
                    bd = d;
                    best = m;
                }
            }
            return best;
        }
    }
}

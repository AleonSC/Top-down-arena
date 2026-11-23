using System;
using System.Collections.Generic;

namespace Arena
{
    /// One-way pieces (Docs/PieceMods.md, PieceMods.OneWay; owner Nov 20): a room built from a One-way card keeps one door that works both ways
    /// (the first one joining it to the map: the one it was built through), and every other door into or out of it lets you through one way
    /// only, in or out (a seeded coin per door). With a single door it is both ways. Everyone obeys them: players, bots and enemies, and enemy
    /// routes only cross them the way they open (LayoutRules.cs). Shots fly through both ways.
    public static partial class Expansion
    {
        /// Sets RoomDoor.Pass on the doors of every One-way piece (from Build). A door that is some One-way piece's both-ways door stays both
        /// ways for everyone, so two One-way pieces side by side never leave either without one. Opened walls (wide gaps) always stay both ways,
        /// and so does a door into a mini-boss room or a dead end (MustStayOpen), so a fight there never leaves you stuck.
        static void MarkOneWayDoors(IList<ExpansionRoom> rooms, int[] group, List<RoomDoor> doors)
        {
            var heads = new List<int>();
            for (int a = 1; a < group.Length; a++)
                if (rooms[a - 1].OneWay && !heads.Contains(group[a]))
                    heads.Add(group[a]);
            if (heads.Count == 0)
                return;
            var free = new HashSet<int>();
            foreach (int h in heads)
                for (int i = 0; i < doors.Count; i++)
                    if (OutOf(doors[i], group, h))
                    {
                        free.Add(i);
                        break;
                    }
            foreach (int h in heads)
                for (int i = 0; i < doors.Count; i++)
                {
                    var d = doors[i];
                    if (!OutOf(d, group, h) || free.Contains(i) || d.Pass != 0 || d.Width > 0f || MustStayOpen(rooms, group, doors, i, h))
                        continue;
                    bool inward = OneWayInward(d.Pos);
                    bool bInside = group[d.B] == h;
                    d.Pass = (sbyte)(inward == bInside ? 1 : -1);
                    doors[i] = d;
                }
        }

        /// Is the room on the far side of this door (from the piece with group head h) one a one-way door would trap you in or shut you out of
        /// (owner Nov 23)? A mini-boss room (its door locks for the duel, then you must walk back out), or a dead end: its only door is this one.
        static bool MustStayOpen(IList<ExpansionRoom> rooms, int[] group, List<RoomDoor> doors, int i, int h)
        {
            var d = doors[i];
            int g = group[d.A] == h ? group[d.B] : group[d.A];
            for (int a = 1; a < group.Length; a++)
                if (group[a] == g && rooms[a - 1].Kind == RoomKind.Gatekeeper)
                    return true;
            for (int j = 0; j < doors.Count; j++)
                if (j != i && OutOf(doors[j], group, g))
                    return false;
            return true;
        }

        /// Does this door join the piece (group head h) to a room outside it?
        static bool OutOf(RoomDoor d, int[] group, int h) => (group[d.A] == h) != (group[d.B] == h);

        /// The seeded coin for a One-way piece's door at this spot: true lets you in only, false out only.
        public static bool OneWayInward(V2 at) => new DetRandom((int)Math.Round(at.X) * 7919 + (int)Math.Round(at.Y) * 31 + 17).Int(2) == 0;
    }

    public partial class Game
    {
        /// Frames a one-way door has held your own player back, and where you were (your screen's bump sound and spark: OneWayDoorLook.cs).
        public int OneWayPushes;
        public V2 OneWayPushAt;
    }

    public sealed partial class World
    {
        int[] oneWays;
        float[] oneWayStay;

        /// The doors that let you through one way only (RoomDoor.Pass != 0).
        public int[] OneWays
        {
            get
            {
                if (oneWays != null)
                    return oneWays;
                var l = new List<int>();
                for (int i = 0; i < Doors.Length; i++)
                    if (Doors[i].Pass != 0)
                        l.Add(i);
                oneWayStay = new float[l.Count];
                for (int k = 0; k < l.Count; k++)
                    oneWayStay[k] = StaySide(Doors[l[k]]);
                return oneWays = l.ToArray();
            }
        }

        /// Which side of a one-way door (+1 or -1 along its Across) you can't leave through it from: the side of the room it lets you into.
        float StaySide(RoomDoor d)
        {
            int into = d.Pass > 0 ? d.B : d.A;
            return (Areas[into].Mid - d.Pos).Dot(d.Across) >= 0f ? 1f : -1f;
        }

        /// Keeps something of radius r from poking through a one-way door the wrong way: on the side it can't leave by, it stays a body's
        /// width off the door line (from PushOutOfSolids). Coming the open way it is never held, and once over the line it is nudged clear.
        public V2 OneWayHold(V2 pos, float r)
        {
            var ow = OneWays;
            for (int k = 0; k < ow.Length; k++)
            {
                var d = Doors[ow[k]];
                V2 o = pos - d.Pos;
                float off = o.Dot(d.Across) * oneWayStay[k];
                if (off >= 0f && off < r && Math.Abs(o.Dot(d.Along)) < d.W / 2f)
                    pos += d.Across * (oneWayStay[k] * (r - off));
            }
            return pos;
        }

        /// A move from `from` to `to` (a step, a dash): a one-way door crossed the wrong way puts it back on its own side; then OneWayHold.
        public V2 OneWayStop(V2 from, V2 to, float r)
        {
            var ow = OneWays;
            for (int k = 0; k < ow.Length; k++)
                if (CrossesWrongWay(k, from, to))
                {
                    var d = Doors[ow[k]];
                    float off = (to - d.Pos).Dot(d.Across) * oneWayStay[k];
                    to += d.Across * (oneWayStay[k] * (r - off));
                }
            return OneWayHold(to, r);
        }

        /// Does the straight line from a to b go through a one-way door the way it doesn't open (steering's routes)?
        public bool OneWayBlocks(V2 a, V2 b)
        {
            var ow = OneWays;
            for (int k = 0; k < ow.Length; k++)
                if (CrossesWrongWay(k, a, b))
                    return true;
            return false;
        }

        bool CrossesWrongWay(int k, V2 a, V2 b)
        {
            var d = Doors[oneWays[k]];
            float s = oneWayStay[k];
            float oa = (a - d.Pos).Dot(d.Across) * s, ob = (b - d.Pos).Dot(d.Across) * s;
            if (oa < 0f || ob >= 0f)
                return false;
            V2 at = a + (b - a) * (oa / (oa - ob));  // where it crosses the door line
            return Math.Abs((at - d.Pos).Dot(d.Along)) < d.W / 2f;
        }
    }
}

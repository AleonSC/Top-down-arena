using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// Map reset (Docs/MapReset.md step 1): in the Campaign the Expanding map goes back to the bare hub after every boss, so
    /// a run plays on ten maps instead of piling 100 waves of rooms, nodes, doors and breaches onto one. Room kits are kept
    /// and each player gets half the vectors they spent on the old map back (on top of the boss refill).
    public partial class Game
    {
        /// The share of a map's spent vectors each player gets back when it resets.
        public const float MapRefundShare = 0.5f;

        /// Which map of the run this is (1 at the start, +1 per reset; synced in Msg.ExpandState, saved).
        public int MapNumber = 1;

        /// Every game, for the look: seconds since the map last reset (99: not this run).
        public float MapResetAge = 99f;

        /// Host: the vectors the team got back at the last reset, all players together (for the log and the tests).
        public int MapRefund;

        /// This mode resets its map after each boss (the Campaign on the Expanding arena).
        public bool MapResetOn => ExpandingOn && ModeRules.MapResets && ModeRules.RunEvents;

        /// What a player gets back for spending this many vectors on the map.
        public static int RefundFor(int spent) => Math.Max(0, (int)(spent * MapRefundShare));

        /// Host, as a boss wave is cleared (WaveCleared in Waves.cs, after the boss refill): everything built on the map goes
        /// and the team stands in the bare hub. Kits stay; each player gets RefundFor(MapSpent) on top of their vectors (above
        /// the cap, like the boss bonus). Coins still lying in the old rooms go to the nearest player. Not after the run's last boss.
        void ResetMap(int keep = -1)
        {
            if (!IsHost || !MapResetOn || (Wave >= LastWave && !Endless))
                return;
            int rooms = Expansion.BoughtCount(Expanded);
            int newSeed = sideRng.Next(1, 1 << 30);  // a new map rolls new breaches, nodes and doors (sideRng: ResetExpansion)
            var kept = keep >= 0 && keep < Expanded.Count ? Expansion.PlaceKeepsake(Expanded, keep, newSeed) : null;
            string keptName = kept != null ? Expanded[keep].Shape : null;
            CollectStrayCoins();
            MapRefund = 0;
            foreach (var p in Players.Values)
            {
                int back = RefundFor(p.MapSpent);
                p.Vectors = Math.Min(short.MaxValue, p.Vectors + back);
                MapRefund += back;
                p.MapSpent = 0;
            }
            Expanded.Clear();
            Openings.Clear();
            Links.Clear();
            Nodes.Clear();
            Placed.Clear();
            duelBoss.Clear();
            ResetGates();
            ResetMysteryDoors();
            EventMark = null;
            ExpandOpen = false;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            botProposed = false;
            expWorld = null;
            LayoutAge = 0f;
            expandSeed = newSeed;
            if (RoomsUp)
            {
                RoomsUp = false;
                PullIntoHub();
            }
            MovePlayersIntoHub();
            KeptHead = -1;
            if (kept != null && kept.Count > 0)
            {
                Expanded.AddRange(kept);
                KeptHead = 0;
                RaiseRoomsNow();
            }
            MapNumber++;
            MapResetAge = 0f;
            Sounds.Add(Sfx.Pick);
            DoLog("map reset: map " + MapNumber + ", " + rooms + " rooms gone, " + MapRefund + " vectors back, " + RoomTokens.Count + " kits kept" + (keptName != null ? ", kept a " + keptName + " room (" + (kept.Count > 0 ? kept.Count + " boxes)" : "no spot)") : ""));
            BroadcastExpansion();
        }

        /// Host: coins and vector pickups outside the hub go straight to the nearest living player (their rooms are going).
        void CollectStrayCoins()
        {
            var gone = new List<int>();
            foreach (var c in Coins.Values)
            {
                if (InHub(c.Pos, 0f))
                    continue;
                Player nearest = null;
                foreach (var p in Players.Values)
                    if (!p.Downed && (nearest == null || p.Pos.DistanceSq(c.Pos) < nearest.Pos.DistanceSq(c.Pos)))
                        nearest = p;
                if (nearest == null)
                    continue;
                if (c.Vec)
                    AddVectors(nearest, c.Value);
                else
                    nearest.Gold += c.Value;
                gone.Add(c.Id);
            }
            foreach (int id in gone)
                Coins.Remove(id);
        }

        /// Host: anyone standing outside the hub (the boss wave keeps the rooms down, so normally nobody) steps back inside it.
        void MovePlayersIntoHub()
        {
            const float margin = 90f;
            foreach (var p in Players.Values)
                if (!InHub(p.Pos, -1f))
                    MovePlayer(p, PushOutOfProps(p.Pos.Clamp(new V2(margin, margin), new V2(Width - margin, Height - margin)), p.Def.BodyR));
        }

        static bool InHub(V2 pos, float pad) => pos.X >= -pad && pos.Y >= -pad && pos.X <= Width + pad && pos.Y <= Height + pad;

        /// Every game but the host's: the map number from Msg.ExpandState; a higher one is a reset (the look's clock starts).
        void ReadMapNumber(int n)
        {
            if (n > MapNumber)
                MapResetAge = 0f;
            MapNumber = Math.Max(1, n);
        }

        // --- The keepsake vote (Docs/MapReset.md step 2) --------------------------------------------------------------

        /// Seconds the team has to pick the room to keep; bots vote after KeepsakeBotDelay.
        public const float KeepsakeTime = 15f, KeepsakeBotDelay = 1.5f;

        /// The vote is open (between a boss and the reset; synced in Msg.ExpandState, holds the break's countdown).
        public bool KeepsakeOpen;

        /// Seconds left to vote (every game counts it down for the screen; the host's decides).
        public float KeepsakeLeft;

        /// Each player's pick: the index in Expanded of the room's first box (Expansion.KeepsakeChoices).
        public readonly Dictionary<int, int> KeepsakeVotes = new Dictionary<int, int>();

        /// The kept room on the new map: the index in Expanded of its first box (-1: none; synced, for the look).
        public int KeptHead = -1;

        /// The rooms the team can keep now.
        public List<int> KeepsakeChoices => Expansion.KeepsakeChoices(Expanded);

        /// Host, as a boss wave is cleared (WaveCleared, after the boss refill): the keepsake vote opens if there is a room to
        /// keep; true then (the build round and new mystery doors wait for the reset). With no room, the map resets at once.
        bool OpenKeepsake()
        {
            if (!IsHost || !MapResetOn || (Wave >= LastWave && !Endless))
                return false;
            KeepsakeVotes.Clear();
            if (KeepsakeChoices.Count == 0)
            {
                ResetMap();
                return false;
            }
            KeepsakeOpen = true;
            KeepsakeLeft = KeepsakeTime;
            DoLog("keepsake vote: " + KeepsakeChoices.Count + " rooms to choose from");
            BroadcastExpansion();
            return true;
        }

        /// This player's pick (the host's own goes straight in).
        public void CastKeepsakeVote(int head)
        {
            if (Me == null || !KeepsakeOpen)
                return;
            if (IsHost)
            {
                HostKeepsakeVote(MyId, head);
                return;
            }
            KeepsakeVotes[MyId] = head;  // (shown at once; the host's state follows)
            var w = Proto.Begin(Msg.KeepsakeVote);
            w.Write((short)head);
            session.SendToHost(Proto.End(w));
        }

        public void OnKeepsakeVote(int peer, BinaryReader r) => HostKeepsakeVote(peer, r.ReadInt16());

        void HostKeepsakeVote(int id, int head)
        {
            if (!IsHost || !KeepsakeOpen || !Players.ContainsKey(id) || !KeepsakeChoices.Contains(head))
                return;
            KeepsakeVotes[id] = head;
            BroadcastExpansion();
        }

        /// The room the votes keep: the most votes; a tie goes to the host's pick if it's tied, else the biggest; no votes, the biggest.
        public int KeepsakeWinner()
        {
            var choices = KeepsakeChoices;
            if (choices.Count == 0)
                return -1;
            var count = new Dictionary<int, int>();
            foreach (var v in KeepsakeVotes.Values)
                if (choices.Contains(v))
                    count[v] = (count.TryGetValue(v, out int n) ? n : 0) + 1;
            int top = 0;
            foreach (var n in count.Values)
                top = Math.Max(top, n);
            var tied = top == 0 ? choices : choices.FindAll(c => count.TryGetValue(c, out int n) && n == top);
            if (tied.Count > 1 && KeepsakeVotes.TryGetValue(MyId, out int mine) && tied.Contains(mine))
                return mine;
            return Expansion.BiggestRoom(Expanded, tied);
        }

        /// Every game, each frame (Update): a client's clock runs down for the screen, and a bot votes for the biggest room.
        void TickKeepsakeView(float dt)
        {
            if (!KeepsakeOpen)
                return;
            if (!IsHost)
                KeepsakeLeft = Math.Max(0f, KeepsakeLeft - dt);
            if (Bot && Me != null && !KeepsakeVotes.ContainsKey(MyId) && KeepsakeLeft <= KeepsakeTime - KeepsakeBotDelay)
                CastKeepsakeVote(Expansion.BiggestRoom(Expanded, KeepsakeChoices));
        }

        /// Host, between waves: the clock runs down; the vote ends once everyone has voted or the time is up: the map resets with
        /// the winner kept, then the build round and the new doors open.
        void TickKeepsake(float dt)
        {
            if (!KeepsakeOpen || !IsHost)
                return;
            KeepsakeLeft = Math.Max(0f, KeepsakeLeft - dt);
            bool all = true;
            foreach (var p in Players.Values)
                all &= KeepsakeVotes.ContainsKey(p.Id);
            if (!all && KeepsakeLeft > 0f)
                return;
            int keep = KeepsakeWinner();
            KeepsakeOpen = false;
            KeepsakeVotes.Clear();
            ResetMap(keep);
            OpenBuildRound();  // (ExpansionGame.cs)
            RollMysteryDoors();  // (MysteryDoors.cs)
        }

        void WriteKeepsake(BinaryWriter w)
        {
            w.Write((short)KeptHead);
            w.Write(KeepsakeOpen);
            if (!KeepsakeOpen)
                return;
            w.Write(KeepsakeLeft);
            w.Write((byte)Math.Min(255, KeepsakeVotes.Count));
            foreach (var kv in KeepsakeVotes)
            {
                w.Write(kv.Key);
                w.Write((short)kv.Value);
            }
        }

        void ReadKeepsake(BinaryReader r)
        {
            KeptHead = r.ReadInt16();
            KeepsakeOpen = r.ReadBoolean();
            KeepsakeVotes.Clear();
            if (!KeepsakeOpen)
                return;
            KeepsakeLeft = r.ReadSingle();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                KeepsakeVotes[id] = r.ReadInt16();
            }
        }
    }

    public static partial class Expansion
    {
        /// The boxes of the room whose first box is `head`: it and every later piece (Leg > 0) grown from one of them.
        public static List<int> RoomGroup(IList<ExpansionRoom> rooms, int head)
        {
            var group = new List<int> { head };
            for (int j = head + 1; j < rooms.Count; j++)
                if (rooms[j].Leg > 0 && group.Contains(rooms[j].Parent - 1))  // (Parent is an area: 0 the hub, i + 1 room i)
                    group.Add(j);
            return group;
        }

        /// The rooms a team can keep through a map reset: every room's first box except the game's floating ones (shop, vendor, Gatekeeper).
        public static List<int> KeepsakeChoices(IList<ExpansionRoom> rooms)
        {
            var list = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Leg == 0 && !rooms[i].Floating)
                    list.Add(i);
            return list;
        }

        /// A room's floor in tiles (all its boxes).
        public static int RoomTiles(IList<ExpansionRoom> rooms, int head)
        {
            int n = 0;
            foreach (int i in RoomGroup(rooms, head))
                n += rooms[i].W / Step * (rooms[i].H / Step);
            return n;
        }

        /// The biggest of these rooms (the first one on a tie; -1 for none).
        public static int BiggestRoom(IList<ExpansionRoom> rooms, IList<int> heads)
        {
            int best = -1, bestTiles = -1;
            foreach (int h in heads)
            {
                int n = RoomTiles(rooms, h);
                if (n > bestTiles) { best = h; bestTiles = n; }
            }
            return best;
        }

        /// A room's tiles as a mask (mask[col, row] from its top-left corner).
        public static bool[,] RoomMask(IList<ExpansionRoom> rooms, int head)
        {
            var group = RoomGroup(rooms, head);
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (int i in group)
            {
                x0 = Math.Min(x0, rooms[i].X); y0 = Math.Min(y0, rooms[i].Y); x1 = Math.Max(x1, rooms[i].X1); y1 = Math.Max(y1, rooms[i].Y1);
            }
            var mask = new bool[(x1 - x0) / Step, (y1 - y0) / Step];
            foreach (int i in group)
                for (int x = rooms[i].X; x < rooms[i].X1; x += Step)
                    for (int y = rooms[i].Y; y < rooms[i].Y1; y += Step)
                        mask[(x - x0) / Step, (y - y0) / Step] = true;
            return mask;
        }

        /// The kept room rebuilt on a bare hub (MapReset.cs): its own shape, turned or mirrored as needed, on the hub's best wall spot
        /// (SurpriseOptions' scoring: the map grows least); if it fits nowhere, the biggest surprise shape that does. It keeps its kind,
        /// theme, drawer, mini-boss and gold look; it's free. Empty if nothing fits.
        public static List<ExpansionRoom> PlaceKeepsake(IList<ExpansionRoom> rooms, int head, int seed)
        {
            var hub = new List<ExpansionRoom>();
            var opts = MaskOptions(hub, RoomMask(rooms, head), SurpriseShape.Ring);
            List<ExpansionRoom> made = null;
            if (opts.Count > 0)
                made = opts[0].Rooms;
            else
            {
                var s = Surprise(seed, 0, hub, SurpriseShape.Ring, out string why);
                if (s.HasValue)
                    made = s.Value.Rooms;
            }
            if (made == null)
                return new List<ExpansionRoom>();
            var src = rooms[head];
            for (int i = 0; i < made.Count; i++)
            {
                var r = made[i];
                r.Kind = src.Kind;
                r.Floor = src.Floor;
                r.Owner = src.Owner;
                r.Mini = src.Mini;
                r.Surprise = src.Surprise;
                r.Claimed = src.Claimed;
                r.Price = 0;
                r.Floating = false;
                made[i] = r;
            }
            return made;
        }
    }
}

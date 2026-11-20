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

        /// Every game: what each player got back at the last reset (synced in Msg.ExpandState, for the MAP n/10 banner).
        public readonly Dictionary<int, int> MapRefunds = new Dictionary<int, int>();

        /// Every game, for the look: the old map's boxes at the last reset (they fold into the hub, MapResetLook.cs).
        public readonly List<ExpansionRoom> FoldRooms = new List<ExpansionRoom>();

        /// How many maps a run plays: one, plus one per boss before the last wave (0: endless).
        public int MapsInRun
        {
            get
            {
                if (Endless)
                    return 0;
                int n = 1;
                for (int w = 1; w < LastWave; w++)
                    if (IsBossWave(w))
                        n++;
                return n;
            }
        }

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
            var core = Expansion.GridCore();
            var kept = keep >= 0 && keep < Expanded.Count ? Expansion.PlaceKeepsake(Expanded, keep, core) : null;
            string keptName = kept != null ? Expanded[keep].Shape : null;
            CollectStrayCoins();
            MapRefund = 0;
            MapRefunds.Clear();
            foreach (var p in Players.Values)
            {
                int back = RefundFor(p.MapSpent);
                p.Vectors = Math.Min(short.MaxValue, p.Vectors + back);
                MapRefund += back;
                MapRefunds[p.Id] = back;
                p.MapSpent = 0;
            }
            FoldRooms.Clear();
            FoldRooms.AddRange(Expanded);
            Expanded.Clear();
            ClearDormant();  // the old rooms' sleeping groups go with them (DormantGroups.cs)
            ClearTrapWrecks();  // and the shot-up turrets (RoomTraps.cs)
            Openings.Clear();
            Links.Clear();
            Nodes.Clear();
            Placed.Clear();
            duelBoss.Clear();
            ResetGates();
            ResetMysteryDoors();
            EventMark = null;
            ExpandOpen = false;
            botProposed = false;
            expWorld = null;
            LayoutAge = 0f;
            expandSeed = newSeed;
            PickCoreLayout();  // a new faction, a new floor plan for the core (CoreLayouts.cs)
            if (RoomsUp)
            {
                RoomsUp = false;
                PullIntoHub();
            }
            MovePlayersIntoHub();
            KeptHead = -1;
            Expanded.AddRange(core);  // (the fresh map's core first, GridRooms.cs, then the kept room against it)
            Openings.AddRange(Expansion.GridCoreCuts());
            if (kept != null && kept.Count > 0)
            {
                KeptHead = Expanded.Count;
                Expanded.AddRange(kept);
            }
            for (int i = 0; i < Expanded.Count; i++)  // the core and the kept room start seen (RoomFog.cs)
            {
                var q = Expanded[i];
                q.Unseen = false;
                Expanded[i] = q;
            }
            SettleRoomFog();
            RaiseRoomsNow();
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
                if (c.Owner != 0)
                    Players.TryGetValue(c.Owner, out nearest);  // (your own copy goes to you)
                else
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
        void ReadMapNumber(int n, List<ExpansionRoom> before)
        {
            if (n == MapNumber + 1)  // (a reset; a joiner mid-run jumps further and gets no banner)
            {
                MapResetAge = 0f;
                FoldRooms.Clear();
                FoldRooms.AddRange(before);
            }
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
            w.Write((byte)Math.Min(255, MapRefunds.Count));
            foreach (var kv in MapRefunds)
            {
                w.Write(kv.Key);
                w.Write((short)Math.Min(short.MaxValue, kv.Value));
            }
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
            MapRefunds.Clear();
            int nr = r.ReadByte();
            for (int i = 0; i < nr; i++)
            {
                int id = r.ReadInt32();
                MapRefunds[id] = r.ReadInt16();
            }
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
                if (rooms[j].Piece > 0 && group.Contains(rooms[j].Parent - 1))  // (Parent is an area: 0 the hub, i + 1 room i)
                    group.Add(j);
            return group;
        }

        /// The rooms a team can keep through a map reset: every room's first box except the game's floating ones (shop, Black Market, Gatekeeper).
        public static List<int> KeepsakeChoices(IList<ExpansionRoom> rooms)
        {
            var list = new List<int>();
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Piece == 0 && !rooms[i].Floating && !IsGridCore(rooms[i]))  // (the core comes back free with every map)
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

        /// The kept room rebuilt against a fresh core (MapReset.cs): its boxes moved whole by whole cells onto the lattice, one of its cells on a door spot of
        /// the core, overlapping nothing; the spot nearest where it stood wins. It keeps its kind, floor, owner, mini-boss and gold look; it's free. Empty if
        /// it fits nowhere (or isn't on the lattice).
        public static List<ExpansionRoom> PlaceKeepsake(IList<ExpansionRoom> rooms, int head, IList<ExpansionRoom> core)
        {
            var group = RoomGroup(rooms, head);
            var cells = new List<int[]>();
            foreach (int i in group)
            {
                var b = rooms[i];
                if ((b.X - GridX0) % GridCell != 0 || (b.Y - GridY0) % GridCell != 0 || b.W % GridCell != 0 || b.H % GridCell != 0)
                    return new List<ExpansionRoom>();  // (off the lattice)
                for (int x = b.X; x < b.X1; x += GridCell)
                    for (int y = b.Y; y < b.Y1; y += GridCell)
                        cells.Add(new[] { x, y });
            }
            List<ExpansionRoom> best = null;
            long bestD = long.MaxValue;
            foreach (var o in PlacementsFor(core, PieceShape.Mono))
                foreach (var c in cells)
                {
                    int dx = o.X - c[0], dy = o.Y - c[1];
                    long d = (long)dx * dx + (long)dy * dy;
                    if (d >= bestD)
                        continue;
                    bool clear = true;
                    foreach (int i in group)
                    {
                        var b = rooms[i];
                        foreach (var q in core)
                            clear &= !(b.X + dx < q.X1 && q.X < b.X1 + dx && b.Y + dy < q.Y1 && q.Y < b.Y1 + dy);
                    }
                    if (!clear)
                        continue;
                    var made = new List<ExpansionRoom>();
                    foreach (int i in group)
                    {
                        var r = rooms[i];
                        r.X += dx; r.Y += dy;
                        r.Price = 0;
                        r.Floating = false;
                        r.Parent = 0;
                        made.Add(r);
                    }
                    best = made;
                    bestD = d;
                }
            return best ?? new List<ExpansionRoom>();
        }
    }
}

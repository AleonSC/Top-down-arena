using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The Expanding arena in a run (Docs/ExpandingArena.md, Docs/VectorBuilding.md): each player's vectors, the rooms built, the
    /// building round between waves (players draw proposals, the team votes, the winner's drawer pays) and the world built from
    /// the rooms. The host decides; everyone else is told by Msg.ExpandState (the rooms, the proposals, the votes) and the
    /// player snapshot (the vectors). Expansion.cs holds the maps.
    public partial class Game
    {
        /// How much more a bigger map adds to a wave's enemies: 1 + this x (floor area / hub area - 1), up to MaxExpandScale.
        public const float ExpandScaleRate = 0.35f, MaxExpandScale = 2f;

        /// The rooms built so far, in order (area 1, 2, ...).
        public readonly List<ExpansionRoom> Expanded = new List<ExpansionRoom>();

        /// A building round is open: from a cleared wave until the next one starts, any player can draw a room and put it to
        /// the team (synced by Msg.ExpandState).
        public bool ExpandOpen;

        /// This round's proposals (at most one per player; Owner is the drawer) and the votes on them: voter id -> the proposer
        /// id voted for, or 0 for "no room".
        public readonly List<ExpansionRoom> ExpandProposals = new List<ExpansionRoom>();
        public readonly Dictionary<int, int> ExpandVotes = new Dictionary<int, int>();

        int expandSeed;
        float botVoteT;
        bool botProposed;
        World expWorld;

        /// The run's arena is the Expanding one.
        public bool ExpandingOn => ArenaKind == ArenaLayout.Expanding;

        /// The world of the hub plus the rooms bought (built once per change).
        public World ExpansionWorld => expWorld ?? (expWorld = Expansion.Build(Expanded));

        /// More floor means more enemies (the Rooms arena makes up for its size with damage instead, Rooms.EnemyDamageMult).
        public float ExpansionScale
        {
            get
            {
                if (!RoomsOn || !ExpandingOn)
                    return 1f;
                float area = 0f;
                foreach (var a in ExpansionWorld.Areas)
                    area += a.Size.X * a.Size.Y;
                return Math.Min(MaxExpandScale, 1f + ExpandScaleRate * (area / (Width * Height) - 1f));
            }
        }

        // --- Vectors (Docs/VectorBuilding.md, step 1) ------------------------------------------------------------

        /// Each player's drawing budget: 1 vector = 1 tile of new wall (Expansion.Cost). Start with VectorStart, hold up to VectorCap
        /// (VectorCapBase for now), +VectorWaveGain for each cleared wave, +1 every VectorKillEvery kills, and a cleared boss wave
        /// refills everyone to the cap and VectorBossBonus on top.
        public const int VectorStart = 25, VectorCapBase = 40, VectorWaveGain = 6, VectorBossBonus = 10, VectorKillEvery = 25;

        /// Host: gains stop at the cap (a stock already above it, from a boss, is left alone).
        void AddVectors(Player p, int n)
        {
            if (n > 0 && p.Vectors < p.VectorCap)
                p.Vectors = Math.Min(p.VectorCap, p.Vectors + n);
        }

        /// Host, when a wave is cleared: a boss wave refills everyone (downed too), any other gives a few.
        void GrantVectorsForWave()
        {
            if (!ExpandingOn)
                return;
            bool boss = IsBossWave(Wave);
            foreach (var p in Players.Values)
            {
                p.VectorCap = VectorCapBase;
                if (boss)
                    p.Vectors = Math.Max(p.Vectors, p.VectorCap + VectorBossBonus);
                else
                    AddVectors(p, VectorWaveGain);
            }
        }

        /// Host, on a kill: every VectorKillEvery kills a player has made give them a vector.
        void GrantVectorKill(Player killer)
        {
            if (ExpandingOn && killer != null && killer.Run.Kills > 0 && killer.Run.Kills % VectorKillEvery == 0)
                AddVectors(killer, 1);
        }

        void SyncVectors(NetSync s, Player p)
        {
            s.Short(ref p.Vectors);
            s.Short(ref p.VectorCap);
        }

        // --- Room features (Docs/ExpandingArena.md, step 5) ------------------------------------------------------------

        /// The map has a shop room: kills drop personal gold, and the shop opens after the first wave following each boss.
        public bool HasShopRoom
        {
            get
            {
                foreach (var r in Expanded)
                    if (r.Kind == Arena.RoomKind.Shop)
                        return true;
                return false;
            }
        }

        /// The room this point is in, if the Expanding map is up (else null).
        RoomArea? ExpandedAreaAt(V2 pos)
        {
            if (!ExpandingOn || !RoomsOn)
                return null;
            var w = ExpansionWorld;
            var a = w.Areas[w.AreaAt(pos)];
            return a.Contains(pos) ? a : (RoomArea?)null;
        }

        /// The path whose ground this point is on (a Floor room), else None. Its rules hold in that room only.
        public PathId FloorAt(V2 pos)
        {
            var a = ExpandedAreaAt(pos);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Floor ? a.Value.Floor : PathId.None;
        }

        /// Kills in a Floor room pay that path's bonus (+25% XP and score at danger 2, +50% at 3).
        float FloorBonusAt(V2 pos)
        {
            var f = FloorAt(pos);
            return f == PathId.None ? 1f : Paths.Bonus(Paths.Get(f).Danger);
        }

        /// This player is standing in a shop room (the shop screen opens for them there).
        public bool InShopRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Shop;
        }

        /// The map has a room with this floor, and how much of the hub's area those rooms add up to.
        float FloorShare(PathId id)
        {
            if (!ExpandingOn || !RoomsOn)
                return 0f;
            float area = 0f;
            foreach (var a in ExpansionWorld.Areas)
                if (a.Kind == Arena.RoomKind.Floor && a.Floor == id)
                    area += a.Size.X * a.Size.Y;
            return area / (Width * Height);
        }

        /// A random spot inside a room of this floor (null if there is none).
        V2? RandomInFloorRoom(PathId id, float margin)
        {
            var list = new List<RoomArea>();
            foreach (var a in ExpansionWorld.Areas)
                if (a.Kind == Arena.RoomKind.Floor && a.Floor == id)
                    list.Add(a);
            if (list.Count == 0)
                return null;
            var r = list[rng.Next(list.Count)];
            float mx = Math.Min(margin, r.Size.X / 2f - 1f), my = Math.Min(margin, r.Size.Y / 2f - 1f);
            return new V2(Rand(r.Min.X + mx, r.Max.X - mx), Rand(r.Min.Y + my, r.Max.Y - my));
        }

        void ResetExpansion()
        {
            Expanded.Clear();
            ExpandOpen = false;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            expWorld = null;
            botVoteT = 0f;
            botProposed = false;
            expandSeed = new Random().Next(1, 1 << 30);  // (not the game's rng: asking it would shift every other random choice of the run)
        }

        // --- The building round -------------------------------------------------------------------------------------

        /// This player's proposal in the open round, if any.
        public ExpansionRoom? ProposalOf(int owner)
        {
            foreach (var q in ExpandProposals)
                if (q.Owner == owner)
                    return q;
            return null;
        }

        /// Host, when a wave is cleared: a building round opens (not in the Practice Range, a Boss Hunt, after the last wave or on a full map).
        void OpenBuildRound()
        {
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null || (Wave >= MaxWave && !Endless) || Expanded.Count >= Expansion.MaxRooms)
                return;
            ExpandOpen = true;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            botVoteT = 0f;
            botProposed = false;
            BroadcastExpansion();
            DoLog("building round opens");
        }

        /// A vote from this game's player: the proposer's id, or 0 for "no room".
        public void CastExpandVote(int choice)
        {
            if (!ExpandOpen || Me == null || choice < 0 || (choice != 0 && ProposalOf(choice) == null))
                return;
            if (IsHost)
            {
                HostExpandVote(MyId, choice);
                return;
            }
            var w = Proto.Begin(Msg.ExpandVote);
            w.Write((short)choice);
            session.SendToHost(Proto.End(w));
            ExpandVotes[MyId] = choice;  // (shown at once; the host's state confirms it)
        }

        public void OnExpandVote(int peer, BinaryReader r) => HostExpandVote(peer, r.ReadInt16());

        void HostExpandVote(int id, int choice)
        {
            if (!IsHost || !ExpandOpen || !Players.ContainsKey(id) || choice < 0 || (choice != 0 && ProposalOf(choice) == null))
                return;
            ExpandVotes[id] = choice;
            FinishOrBroadcast();
        }

        /// This game's player draws a room (a box in world px on the 100 px grid) and puts it to the vote.
        public void ProposeExpandRoom(int x, int y, int w, int h)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostExpandDraw(MyId, x, y, w, h);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandDraw);
            pw.Write((short)x); pw.Write((short)y); pw.Write((short)w); pw.Write((short)h);
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandDraw(int peer, BinaryReader r) => HostExpandDraw(peer, r.ReadInt16(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16());

        /// Host: a drawing becomes its drawer's proposal (a newer one replaces the last, and the votes for the old one go). The drawer
        /// backs their own. It must fit and the drawer must have the vectors; they only pay if it wins.
        void HostExpandDraw(int id, int x, int y, int w, int h)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            string why;
            var made = Expansion.Custom(Expanded, x, y, w, h, out why);
            if (!made.HasValue || made.Value.Price > p.Vectors)
            {
                DoLog("proposal refused (" + (why ?? "needs " + made.Value.Price + " vectors, has " + p.Vectors) + ")");
                return;
            }
            var room = made.Value;
            room.Owner = id;
            int old = ExpandProposals.FindIndex(q => q.Owner == id);
            if (old >= 0)
            {
                ExpandProposals[old] = room;
                foreach (int voter in new List<int>(ExpandVotes.Keys))
                    if (ExpandVotes[voter] == id)
                        ExpandVotes.Remove(voter);
            }
            else
                ExpandProposals.Add(room);
            ExpandVotes[id] = id;
            DoLog(p.Name + " proposes a " + w + " x " + h + " room for " + room.Price + " vectors");
            FinishOrBroadcast();
        }

        /// Host: once everyone has voted the round is settled; until then everyone is told how it stands.
        void FinishOrBroadcast()
        {
            bool all = true;
            foreach (int pid in Players.Keys)
                all &= ExpandVotes.ContainsKey(pid);
            if (all)
                ResolveBuildRound();
            else
                BroadcastExpansion();
        }

        /// Host, when everyone has voted or the next wave is about to start: the proposal with the most votes is built, its drawer
        /// pays; a tie (a 50/50) is a random pick among the tied, and "no room" counts as one of them. Nothing is built with no
        /// proposals, no votes, or "no room" winning.
        void ResolveBuildRound()
        {
            if (!IsHost || !ExpandOpen)
                return;
            var standing = ExpandProposals.FindAll(q => Players.ContainsKey(q.Owner) && Players[q.Owner].Vectors >= q.Price);
            var tally = new Dictionary<int, int>();
            foreach (int choice in ExpandVotes.Values)
            {
                if (choice != 0 && !standing.Exists(q => q.Owner == choice))
                    continue;
                int n;
                tally.TryGetValue(choice, out n);
                tally[choice] = n + 1;
            }
            int best = 0;
            foreach (var kv in tally)
                best = Math.Max(best, kv.Value);
            if (best > 0 && standing.Count > 0)
            {
                var leaders = new List<int>();
                foreach (var kv in tally)
                    if (kv.Value == best)
                        leaders.Add(kv.Key);
                leaders.Sort();
                int pick = leaders[rng.Next(leaders.Count)];
                if (leaders.Count > 1)
                    DoLog("tied vote: random pick among " + leaders.Count);
                if (pick != 0)
                {
                    var room = standing.Find(q => q.Owner == pick);
                    Players[pick].Vectors -= room.Price;
                    Expanded.Add(room);
                    expWorld = null;
                    LayoutAge = 0f;  // the new walls rise
                    Sounds.Add(Sfx.Pick);
                    DoLog("room built: " + room.W + " x " + room.H + " by " + Players[pick].Name + " for " + room.Price + " vectors");
                }
                else
                    DoLog("the team voted for no room");
            }
            else
                DoLog("building round closes: nothing built");
            ExpandOpen = false;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            BroadcastExpansion();
        }

        /// Every frame, every game: a bot player draws a room it can afford a moment after the round opens, then votes.
        void TickExpansion(float dt)
        {
            if (!ExpandOpen)
            {
                botVoteT = 0f;
                botProposed = false;
                return;
            }
            if (!Bot || Me == null)
                return;
            botVoteT += dt;
            if (!botProposed && botVoteT >= 1.2f)
            {
                botProposed = true;
                foreach (var o in Expansion.Offers(expandSeed + MyId * 31, Wave, Expanded))
                    if (o.Price <= Me.Vectors && rng.Next(10) < 7)
                    {
                        ProposeExpandRoom(o.X, o.Y, o.W, o.H);
                        break;
                    }
            }
            if (botVoteT >= 3f && !ExpandVotes.ContainsKey(MyId) && ExpandProposals.Count > 0)
                CastExpandVote(rng.Next(10) < 2 ? 0 : ExpandProposals[rng.Next(ExpandProposals.Count)].Owner);
        }

        // --- Net ---------------------------------------------------------------------------------------------------

        static void WriteRooms(BinaryWriter w, IList<ExpansionRoom> list)
        {
            w.Write((byte)list.Count);
            foreach (var r in list)
            {
                w.Write((byte)r.Parent);
                w.Write((short)r.X); w.Write((short)r.Y); w.Write((short)r.W); w.Write((short)r.H);
                w.Write((byte)r.Kind); w.Write((byte)r.Floor);
                w.Write((short)r.Price);
                w.Write((short)r.Owner);
            }
        }

        static List<ExpansionRoom> ReadRooms(BinaryReader r)
        {
            int n = r.ReadByte();
            var list = new List<ExpansionRoom>(n);
            for (int i = 0; i < n; i++)
            {
                var room = new ExpansionRoom { Parent = r.ReadByte(), X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                room.Kind = (RoomKind)r.ReadByte();
                room.Floor = (PathId)r.ReadByte();
                room.Price = r.ReadInt16();
                room.Owner = r.ReadInt16();
                list.Add(room);
            }
            return list;
        }

        /// Host -> all: the rooms built, whether a round is open, its proposals and the votes so far.
        void BroadcastExpansion()
        {
            var w = Proto.Begin(Msg.ExpandState);
            WriteRooms(w, Expanded);
            w.Write(ExpandOpen);
            if (ExpandOpen)
            {
                WriteRooms(w, ExpandProposals);
                w.Write((byte)ExpandVotes.Count);
                foreach (var kv in ExpandVotes)
                {
                    w.Write(kv.Key);
                    w.Write((short)kv.Value);
                }
            }
            session.Broadcast(Proto.End(w));
        }

        void ReadExpandState(BinaryReader r)
        {
            var rooms = ReadRooms(r);
            bool changed = rooms.Count != Expanded.Count;
            for (int i = 0; !changed && i < rooms.Count; i++)
                changed |= !rooms[i].Equals(Expanded[i]);
            if (changed)
            {
                Expanded.Clear();
                Expanded.AddRange(rooms);
                expWorld = null;
                LayoutAge = 0f;
            }
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            ExpandOpen = r.ReadBoolean();
            if (!ExpandOpen)
                return;
            ExpandProposals.AddRange(ReadRooms(r));
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                ExpandVotes[id] = r.ReadInt16();
            }
        }
    }
}

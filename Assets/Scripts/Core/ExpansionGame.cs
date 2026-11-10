using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// The Expanding arena in a run (Docs/ExpandingArena.md, step 3): the team purse, the rooms bought, the offer the
    /// team votes on after each boss and the world built from them. The host decides; everyone else is told by
    /// Msg.ExpandState (the rooms, the offer, the votes) and the snapshot (the purse). Expansion.cs holds the maps.
    public partial class Game
    {
        /// Purse gold per point of the gold a kill is worth (Shop.GoldPerXp): every kill pays the team.
        public const float PurseRate = 1f;

        /// How much more a bigger map adds to a wave's enemies: 1 + this x (floor area / hub area - 1), up to MaxExpandScale.
        public const float ExpandScaleRate = 0.35f, MaxExpandScale = 2f;

        /// The team's shared gold for rooms (synced in the snapshot).
        public int Purse;

        /// The rooms bought so far, in order (area 1, 2, ...).
        public readonly List<ExpansionRoom> Expanded = new List<ExpansionRoom>();

        /// The rooms on offer while the team is voting (else null), and each player's vote: an offer's index, or
        /// ExpandOffer.Count for "buy nothing".
        public List<ExpansionRoom> ExpandOffer;
        public readonly Dictionary<int, int> ExpandVotes = new Dictionary<int, int>();

        /// Which of the offers is a room a player drew (the last one), or -1.
        public int ExpandCustom = -1;

        /// The offer is an optional one (after a regular wave): it doesn't hold the countdown, and it closes when the next wave starts.
        public bool ExpandOptional;

        /// An offer is open that the wave has to wait for (the vote after a boss).
        public bool ExpandBlocksWave => ExpandOffer != null && !ExpandOptional;

        int expandSeed;
        float botVoteT;
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
            Purse = 0;
            Expanded.Clear();
            ExpandOffer = null;
            ExpandCustom = -1;
            ExpandOptional = false;
            ExpandVotes.Clear();
            expWorld = null;
            botVoteT = 0f;
            expandSeed = new Random().Next(1, 1 << 30);  // (not the game's rng: asking it would shift every other random choice of the run)
        }

        /// Host, on a kill: the team purse gets the kill's gold.
        void AddPurse(Enemy e)
        {
            if (!ExpandingOn)
                return;
            Purse += Math.Max(1, (int)Math.Round(e.Def.Xp * (e.Elite ? 3 : 1) * GoldPerXp * PurseRate));
        }

        bool CanBuy(int i) => ExpandOffer != null && i >= 0 && i < ExpandOffer.Count && ExpandOffer[i].Price <= Purse;

        /// Host, after a boss wave is cleared: offer rooms for the purse (nothing if none is affordable, or at the cap).
        void OfferExpansion() => MakeExpansionOffer(false);

        /// Host, after a regular wave is cleared: if the purse covers a room, an optional offer (a chip on screen; the team can
        /// vote while the countdown runs, and a room is bought only if most of the team voted for it before the wave starts).
        void OfferOptionalExpansion()
        {
            if (!IsBossWave(Wave) && ExpandOffer == null)
                MakeExpansionOffer(true);
        }

        void MakeExpansionOffer(bool optional)
        {
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null || (Wave >= MaxWave && !Endless))
                return;
            var offers = Expansion.Offers(expandSeed + (optional ? 7 : 0), Wave, Expanded);
            bool any = false;
            foreach (var o in offers)
                any |= o.Price <= Purse;
            if (!any)
            {
                if (!optional)
                    DoLog("expansion: nothing affordable (purse " + Purse + (offers.Count == 0 ? ", map full" : "") + ")");
                return;
            }
            ExpandOffer = offers;
            ExpandOptional = optional;
            ExpandCustom = -1;
            ExpandVotes.Clear();
            botVoteT = 0f;
            BroadcastExpansion();
            DoLog((optional ? "optional " : "") + "expansion offered (purse " + Purse + "): " + string.Join(" / ", offers.ConvertAll(o => o.Shape + " " + o.Kind + " " + o.Price).ToArray()));
        }

        /// A vote from this game's player: an offer's index, or ExpandOffer.Count to buy nothing.
        public void CastExpandVote(int choice)
        {
            if (ExpandOffer == null || Me == null || choice < 0 || choice > ExpandOffer.Count)
                return;
            if (IsHost)
            {
                HostExpandVote(MyId, choice);
                return;
            }
            var w = Proto.Begin(Msg.ExpandVote);
            w.Write((byte)choice);
            session.SendToHost(Proto.End(w));
            ExpandVotes[MyId] = choice;  // (shown at once; the host's state confirms it)
        }

        public void OnExpandVote(int peer, BinaryReader r) => HostExpandVote(peer, r.ReadByte());

        /// This game's player draws a room (a box in world px on the 100 px grid) and puts it to the vote.
        public void ProposeExpandRoom(int x, int y, int w, int h)
        {
            if (ExpandOffer == null || Me == null)
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

        /// Host: a drawn room joins the offer as its last card (a newer drawing replaces the last one, and its votes go).
        void HostExpandDraw(int id, int x, int y, int w, int h)
        {
            if (!IsHost || ExpandOffer == null || !Players.ContainsKey(id))
                return;
            string why;
            var room = Expansion.Custom(Expanded, x, y, w, h, out why);
            if (!room.HasValue || room.Value.Price > Purse)
            {
                DoLog("expansion: drawn room refused (" + (why ?? "can't afford it") + ")");
                return;
            }
            if (ExpandCustom >= 0)
            {
                ExpandOffer[ExpandCustom] = room.Value;
                var drop = new List<int>();
                foreach (var kv in ExpandVotes)
                    if (kv.Value == ExpandCustom)
                        drop.Add(kv.Key);
                foreach (int pid in drop)
                    ExpandVotes.Remove(pid);
            }
            else
            {
                int skip = ExpandOffer.Count;  // the "buy nothing" vote moves up one place
                foreach (int pid in new List<int>(ExpandVotes.Keys))
                    if (ExpandVotes[pid] == skip)
                        ExpandVotes[pid] = skip + 1;
                ExpandOffer.Add(room.Value);
                ExpandCustom = skip;
            }
            DoLog("expansion: a drawn room, " + room.Value.W + " x " + room.Value.H + " for " + room.Value.Price);
            BroadcastExpansion();
        }

        void HostExpandVote(int id, int choice)
        {
            if (!IsHost || ExpandOffer == null || !Players.ContainsKey(id) || choice < 0 || choice > ExpandOffer.Count)
                return;
            if (choice < ExpandOffer.Count && !CanBuy(choice))
                return;
            ExpandVotes[id] = choice;
            bool all = true;
            foreach (int pid in Players.Keys)
                all &= ExpandVotes.ContainsKey(pid);
            if (all)
                ResolveExpansion();
            else
                BroadcastExpansion();
        }

        /// Host: everyone has voted. The most votes wins (a tie goes to the lower offer, and buying beats buying nothing).
        /// Host, as the next wave is about to start: an optional offer closes. A room is bought only if more than half the
        /// team voted for it; anything less and the purse keeps its gold.
        void ResolveOptionalExpansion()
        {
            if (ExpandOffer != null && ExpandOptional)
                ResolveExpansion(true);
        }

        void ResolveExpansion(bool deadline = false)
        {
            int n = ExpandOffer.Count;
            var tally = new int[n + 1];
            foreach (var v in ExpandVotes.Values)
                tally[Math.Min(v, n)]++;
            int win = 0;
            for (int i = 1; i <= n; i++)
                if (tally[i] > tally[win])
                    win = i;
            if (win < n && CanBuy(win) && (!deadline || tally[win] > Players.Count / 2))
            {
                var room = ExpandOffer[win];
                Purse -= room.Price;
                Expanded.Add(room);
                expWorld = null;
                LayoutAge = 0f;  // the new walls rise
                Sounds.Add(Sfx.Pick);
                DoLog("expansion bought: " + room.Shape + " " + room.Kind + (room.Kind == Arena.RoomKind.Floor ? " " + room.Floor : "") + " for " + room.Price + " (purse " + Purse + ")");
            }
            else
                DoLog("expansion: nothing bought (purse " + Purse + ")");
            ExpandOffer = null;
            ExpandCustom = -1;
            ExpandOptional = false;
            ExpandVotes.Clear();
            BroadcastExpansion();
        }

        /// Every frame, every game: a bot player votes for a random room it can afford a moment after the offer comes up.
        void TickExpansion(float dt)
        {
            if (ExpandOffer == null || !Bot || Me == null || ExpandVotes.ContainsKey(MyId))
                return;
            botVoteT += dt;
            if (botVoteT < 1.5f)
                return;
            var ok = new List<int>();
            for (int i = 0; i < ExpandOffer.Count; i++)
                if (CanBuy(i))
                    ok.Add(i);
            bool pass = ExpandOptional && rng.Next(10) < 4;  // (a bot passes on an optional room now and then)
            CastExpandVote(ok.Count > 0 && !pass ? ok[rng.Next(ok.Count)] : ExpandOffer.Count);
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
                list.Add(room);
            }
            return list;
        }

        /// Host -> all: the rooms bought, the offer (if the team is voting) and the votes so far.
        void BroadcastExpansion()
        {
            var w = Proto.Begin(Msg.ExpandState);
            WriteRooms(w, Expanded);
            w.Write(ExpandOffer != null);
            if (ExpandOffer != null)
            {
                WriteRooms(w, ExpandOffer);
                w.Write((byte)(ExpandCustom < 0 ? 255 : ExpandCustom));
                w.Write(ExpandOptional);
            }
            w.Write((byte)ExpandVotes.Count);
            foreach (var kv in ExpandVotes)
            {
                w.Write(kv.Key);
                w.Write((byte)kv.Value);
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
            ExpandOffer = r.ReadBoolean() ? ReadRooms(r) : null;
            ExpandCustom = ExpandOffer != null ? (r.ReadByte() == 255 ? -1 : ExpandOffer.Count - 1) : -1;
            ExpandOptional = ExpandOffer != null && r.ReadBoolean();
            ExpandVotes.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
            {
                int id = r.ReadInt32();
                ExpandVotes[id] = r.ReadByte();
            }
            if (ExpandOffer == null)
                ExpandVotes.Clear();
        }

        void SyncExpansion(NetSync s) => s.Int(ref Purse);
    }
}

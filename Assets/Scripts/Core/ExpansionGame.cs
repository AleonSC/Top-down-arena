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
        public static float ExpandScaleRate = 0.6f, MaxExpandScale = 1.4f;  // (static, not const: the balance report tries others)

        /// The rooms built so far, in order (area 1, 2, ...).
        public readonly List<ExpansionRoom> Expanded = new List<ExpansionRoom>();

        /// The map's nodes (Docs/Nodes.md): shop and vendor points out in empty space, placed once and never respawned; a built room that
        /// encloses one takes on its job (synced after the tokens in Msg.ExpandState).
        public readonly List<MapNode> Nodes = new List<MapNode>();

        /// A building round is open: from a cleared wave until the next one starts, any player can draw a room and put it to
        /// the team (synced by Msg.ExpandState).
        public bool ExpandOpen;

        /// This round's proposals (at most one per player; Owner is the drawer) and the votes players have cast: voter id -> the
        /// proposer id voted for, or 0 for "no room". A player who hasn't voted still backs their own proposal (EffectiveVote), but
        /// only cast votes count towards settling the round early.
        public readonly List<ExpansionRoom> ExpandProposals = new List<ExpansionRoom>();
        public readonly Dictionary<int, int> ExpandVotes = new Dictionary<int, int>();

        /// The room kits the team holds (spent with ApplyRoomToken; synced in Msg.ExpandState). Bosses give a Floor kit, a won challenge room a
        /// Shop kit, the vendor sells a Floor kit.
        public readonly List<RoomToken> RoomTokens = new List<RoomToken>();

        /// The run builds by team vote (the lobby's "Team vote building"): off = free building, a drawing is built at once.
        public bool BuildVoting => D(DiffKnob.BuildVote) >= 0.5f;

        /// The lobby's "Team purse": builds can spend every player's vectors, not just the builder's.
        public bool TeamPurse => D(DiffKnob.BuildPurse) >= 0.5f;

        /// What this player can put towards a build: their own vectors, or (Team purse) the whole team's.
        public int SpendableVectors(Player p)
        {
            if (p == null)
                return 0;
            if (!TeamPurse)
                return p.Vectors;
            int n = 0;
            foreach (var q in Players.Values)
                n += q.Vectors;
            return n;
        }

        /// Host: charge a build. The payer goes first, then (Team purse) the richest teammates until it's covered.
        void PayVectors(Player payer, int price)
        {
            int take = Math.Min(payer.Vectors, price);
            payer.Vectors -= take;
            price -= take;
            while (TeamPurse && price > 0)
            {
                Player rich = null;
                foreach (var q in Players.Values)
                    if (q.Vectors > 0 && (rich == null || q.Vectors > rich.Vectors))
                        rich = q;
                if (rich == null)
                    break;
                take = Math.Min(rich.Vectors, price);
                rich.Vectors -= take;
                price -= take;
            }
        }

        /// Free building: anyone builds with their own vectors the moment they draw, any number of rooms a round.
        public bool FreeBuild => ExpandingOn && !BuildVoting;

        /// Free building, outside a building round (a wave is on, or the map is full): only openings, so doors can go in mid-wave.
        /// Not while a Gatekeeper duel has its room locked.
        public bool DoorsOpen
        {
            get
            {
                if (!FreeBuild || ExpandOpen || Practice != null || Hunt != null || (State != MatchState.Fighting && State != MatchState.Intermission))
                    return false;
                for (int a = 1; a < ExpansionWorld.Areas.Length; a++)
                    if (AreaLocked(a))
                        return false;
                return true;
            }
        }

        /// A wall can be opened now: in a building round, or (free building) mid-wave.
        public bool CanCut => ExpandOpen || DoorsOpen;

        int expandSeed;
        int roundBuilt;  // free building: drawings built since this round opened (bots only build when it's 0)
        float botVoteT;
        bool botProposed;
        World expWorld;

        /// The run's arena is the Expanding one.
        public bool ExpandingOn => ArenaKind == ArenaLayout.Expanding;

        /// The world of the hub plus the rooms bought (built once per change).
        /// The stretches of interior wall the team has opened.
        public readonly List<WallCut> Openings = new List<WallCut>();

        /// The portal pairs the team bought (Docs/RoomBuilding2.md step 2; synced after the openings in Msg.ExpandState).
        public readonly List<PortalLink> Links = new List<PortalLink>();
        public World ExpansionWorld => expWorld ?? (expWorld = Expansion.Build(Expanded, Openings, Links, Nodes));

        /// More floor means more enemies (the Rooms arena makes up for its size with damage instead, Rooms.EnemyDamageMult).
        public float ExpansionScale
        {
            get
            {
                if (!RoomsOn || !ExpandingOn)
                    return 1f;
                float area = 0f;
                foreach (var a in ExpansionWorld.Areas)
                    if (!a.Sealed)
                        area += a.Size.X * a.Size.Y;  // (a sealed floating room adds no crowd)
                return Math.Min(MaxExpandScale, 1f + ExpandScaleRate * (area / (Width * Height) - 1f));
            }
        }

        // --- Vectors (Docs/VectorBuilding.md, step 1) ------------------------------------------------------------

        /// Each player's drawing budget: 1 vector = 1 tile of new wall (Expansion.Cost). Start with VectorStart, hold up to VectorCap
        /// (VectorCapBase for now), +VectorWaveGain for each cleared wave, +1 every VectorKillEvery kills, and a cleared boss wave
        /// refills everyone to the cap and VectorBossBonus on top.
        public const int VectorStart = 50, VectorCapBase = 70, VectorWaveGain = 8, VectorBossBonus = 20, VectorKillEvery = 8;

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
            bool boss = BossNow;
            if (boss && IsHost && Practice == null && Hunt == null)
                GrantRoomToken(Expansion.FloorToken(expandSeed, Wave, Expanded, RoomTokens));
            foreach (var p in Players.Values)
            {
                p.VectorCap = VectorCapBase + p.Stats.VectorCap;
                if (boss)
                    p.Vectors = Math.Max(p.Vectors, p.VectorCap + VectorBossBonus);
                else
                    AddVectors(p, LongLegs ? VectorWaveGain / 2 : VectorWaveGain);  // (a Campaign has twice the waves)
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
                foreach (var a in ExpansionWorld.Areas)
                    if (a.Kind == Arena.RoomKind.Shop && !a.Sealed)  // (a floating shop counts once a built room opens it)
                        return true;
                return false;
            }
        }

        /// A vendor room is on the connected map.
        public bool HasVendorRoom
        {
            get
            {
                foreach (var a in ExpansionWorld.Areas)
                    if (a.Kind == Arena.RoomKind.Vendor && !a.Sealed)
                        return true;
                return false;
            }
        }

        /// The Expanding arena has a shop or vendor you can walk into: kills drop gold, the shop opens in each intermission without holding the
        /// countdown, your gold and shelf are kept between waves, and what you can do depends on the room you stand in.
        public bool ServiceRooms => ExpandingOn && (HasShopRoom || HasVendorRoom);

        /// The shop is open and the wave has to wait for it (the Bazaar and Gold Rush shops; the room shops never hold the countdown).
        public bool ShopBlocksWave => ShopOpen && !ServiceRooms;

        public bool InVendorRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Vendor;
        }

        public bool InServiceRoom(Player p) => InShopRoom(p) || InVendorRoom(p);

        /// In a room shop, upgrades and Add stock are bought in the shop room and heals, revives and removals at the vendor.
        bool ServiceAllowed(Player p, ShopAction action)
        {
            if (!ServiceRooms)
                return true;
            switch (action)
            {
                case ShopAction.Buy:
                case ShopAction.Restock:
                    return InShopRoom(p);
                case ShopAction.Heal:
                case ShopAction.Revive:
                case ShopAction.Remove:
                case ShopAction.RoomKit:
                case ShopAction.Vectors:
                    return InVendorRoom(p);
                default:
                    return true;
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

        int floatCycle = -1;

        /// Host, as a regular wave starts: a new boss cycle gets a Gatekeeper room off the connected map (sealed until a built room touches it)
        /// and a shop, a vendor and a chest node (Docs/Nodes.md). Nothing placed is ever placed again: at most MaxSealed rooms and MaxNodesWaiting nodes wait.
        void PlaceFloatingRooms()
        {
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null)
                return;
            int cycle = (Wave - 1) / Expansion.Milestone;
            if (cycle <= floatCycle)
                return;
            floatCycle = cycle;
            bool added = false;
            if (Expansion.SealedCount(Expanded) < Expansion.MaxSealed)
            {
                var f = Expansion.FloatingFor(expandSeed, cycle, Expanded, Arena.RoomKind.Gatekeeper);
                if (f.HasValue)
                {
                    Expanded.Add(f.Value);
                    added = true;
                    DoLog("a floating Gatekeeper room appears at " + f.Value.X + "," + f.Value.Y);
                }
            }
            foreach (var kind in new[] { NodeKind.Shop, NodeKind.Vendor, NodeKind.Chest })
            {
                if (Expansion.UnclaimedCount(Expanded, Nodes) >= Expansion.MaxNodesWaiting)
                    break;
                var nd = Expansion.NodeFor(expandSeed, cycle, Expanded, Nodes, kind);
                if (!nd.HasValue)
                    continue;
                Nodes.Add(nd.Value);
                added = true;
                DoLog("a " + kind + " node appears at " + nd.Value.X + "," + nd.Value.Y);
            }
            if (added)
            {
                expWorld = null;
                BroadcastExpansion();
            }
        }

        // --- Surprise rooms (Docs/SurpriseRooms.md) --------------------------------------------------------------

        /// Seconds since the last surprise room appeared (every game, for the look), the wave it came in, and its shape + 1.
        public float SurpriseAge = 99f;
        public int SurpriseWave = -1;
        public byte SurpriseKind;

        /// Its rooms stay gold on the minimap for the rest of the wave they came in.
        public bool SurpriseFresh => SurpriseKind > 0 && Wave <= SurpriseWave;

        public static int SurpriseCount(IList<ExpansionRoom> rooms)
        {
            int n = 0;
            foreach (var r in rooms)
                if (r.Surprise > 0 && r.Leg == 0)
                    n++;
            return n;
        }

        /// The shape the next surprise room tries first: the five come round in turn from a seeded start.
        SurpriseShape NextSurpriseShape()
        {
            int shapes = Enum.GetValues(typeof(SurpriseShape)).Length;
            return (SurpriseShape)(int)(((uint)expandSeed % (uint)shapes + (uint)SurpriseCount(Expanded)) % (uint)shapes);
        }

        void NoteSurprise(byte kind)
        {
            SurpriseAge = 0f;
            SurpriseWave = Wave;
            SurpriseKind = kind;
        }

        /// Host, when a boss is cleared: the map gets a free, well-shaped room that closes a loop (Expansion.Surprise). The shape rotates
        /// through the five from a seeded start. Nothing on a full map or without a spot.
        void GiveSurpriseRoom()
        {
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null || (Wave >= MaxWave && !Endless) || Expansion.BoughtCount(Expanded) > Expansion.MaxRooms - 2)
                return;
            string why;
            var pick = Expansion.Surprise(expandSeed, Wave, Expanded, NextSurpriseShape(), out why);
            if (!pick.HasValue)
            {
                DoLog("no surprise room: " + why);
                return;
            }
            Expanded.AddRange(pick.Value.Rooms);
            expWorld = null;
            LayoutAge = 0f;
            NoteSurprise(pick.Value.Rooms[0].Surprise);
            Sounds.Add(Sfx.Pick);
            DoLog("surprise room: " + Expansion.SurpriseName(pick.Value.Shape) + " of " + pick.Value.Rooms.Count + " boxes at " + pick.Value.Rooms[0].X + "," + pick.Value.Rooms[0].Y + ", closes " + pick.Value.Loops + " loops");
            BroadcastExpansion();
        }

        public const int ChestGold = 40, ChestGoldPerCycle = 10, ChestVectors = 6;  // what a chest node gives each player, once

        /// Host, every tick of a wave: a player who walks up to a claimed chest node opens it. Everyone gets gold and a few vectors; it stays spent.
        void TickNodes()
        {
            if (!IsHost || !ExpandingOn || Nodes.Count == 0)
                return;
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.Chest || nd.Spent || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                bool near = false;
                foreach (var p in Players.Values)
                    near |= !p.Downed && p.Pos.DistanceTo(nd.Pos) < p.Def.BodyR + 45f;
                if (!near)
                    continue;
                nd.Spent = true;
                Nodes[i] = nd;
                int gold = ChestGold + ChestGoldPerCycle * ((Wave - 1) / Expansion.Milestone);
                foreach (var p in Players.Values)
                {
                    p.Gold += gold;
                    AddVectors(p, ChestVectors);
                }
                BroadcastFx(FxKind.Ring, nd.Pos, new Rgba(1f, 0.82f, 0.3f), 110f, 0.7f, Sfx.Pick);
                DoLog("a chest opens: " + gold + " gold and " + ChestVectors + " vectors each");
                BroadcastExpansion();
            }
        }

        /// Host: the team gains a room kit (at most Expansion.MaxTokens held).
        void GrantRoomToken(RoomToken t)
        {
            if (!IsHost || !ExpandingOn || RoomTokens.Count >= Expansion.MaxTokens)
                return;
            RoomTokens.Add(t);
            DoLog("room kit gained: " + t.Name);
            BroadcastExpansion();
        }

        /// This game's player spends a kit on a bought room (index into Expanded). Only while a building round is open.
        public void ApplyRoomToken(int token, int room)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostApplyToken(token, room);
                return;
            }
            var w = Proto.Begin(Msg.ExpandApply);
            w.Write((byte)token); w.Write((byte)room);
            session.SendToHost(Proto.End(w));
        }

        public void OnExpandApply(int peer, BinaryReader r) => HostApplyToken(r.ReadByte(), r.ReadByte());

        void HostApplyToken(int token, int room)
        {
            string why;
            if (!IsHost || !ExpandOpen || token < 0 || token >= RoomTokens.Count || !Expansion.CanRetype(Expanded, room, RoomTokens[token], out why))
                return;
            var t = RoomTokens[token];
            foreach (int i in Expansion.GroupOf(Expanded, room))  // (a drawn outline's pieces all change: they are one room)
                Expanded[i] = Expansion.Retype(Expanded[i], t);
            RoomTokens.RemoveAt(token);
            expWorld = null;
            Sounds.Add(Sfx.Pick);
            DoLog("room " + (room + 1) + " became " + t.Name);
            BroadcastExpansion();
        }

        void ResetExpansion()
        {
            RoomTokens.Clear();
            floatCycle = -1;
            Expanded.Clear();
            Nodes.Clear();
            Openings.Clear();
            Links.Clear();
            ResetGates();
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
                if (q.Owner == owner && q.Leg == 0)
                    return q;
            return null;
        }

        /// The open round's proposals, one each (a drawn outline's later pieces left out: LegsOf has them).
        public List<ExpansionRoom> ProposalHeads => ExpandProposals.FindAll(q => q.Leg == 0);

        /// A drawn outline proposal's later pieces, in order (empty for anything else).
        public List<ExpansionRoom> LegsOf(int owner) => ExpandProposals.FindAll(q => q.Owner == owner && q.Leg > 0);

        /// Host: this player's proposal is now these entries (a newer one replaces the last, and the votes for the old one go;
        /// the drawer backs their own until they vote).
        void SetProposal(int id, List<ExpansionRoom> entries)
        {
            bool had = ExpandProposals.RemoveAll(q => q.Owner == id) > 0;
            if (had)
                foreach (int voter in new List<int>(ExpandVotes.Keys))
                    if (ExpandVotes[voter] == id)
                        ExpandVotes.Remove(voter);
            foreach (var e in entries)
            {
                var q = e;
                q.Owner = id;
                ExpandProposals.Add(q);
            }
            ExpandVotes.Remove(id);
        }

        /// Host, when a wave is cleared: a building round opens (not in the Practice Range, a Boss Hunt, after the last wave or on a full map).
        void OpenBuildRound()
        {
            if (!IsHost || !ExpandingOn || Practice != null || Hunt != null || (Wave >= MaxWave && !Endless) || Expansion.BoughtCount(Expanded) >= Expansion.MaxRooms)
                return;
            ExpandOpen = true;
            roundBuilt = 0;
            ExpandProposals.Clear();
            ExpandVotes.Clear();
            botVoteT = 0f;
            botProposed = false;
            BroadcastExpansion();
            DoLog("building round opens");
        }

        /// The proposer id this player backs: the vote they cast (0 = no room), else their own proposal, else -1.
        public int EffectiveVote(int player)
        {
            int v;
            if (ExpandVotes.TryGetValue(player, out v))
                return v;
            return ProposalOf(player).HasValue ? player : -1;
        }

        /// How many players back this proposal, counting a drawer who hasn't voted as backing their own.
        public int VotesFor(int owner)
        {
            int n = 0;
            foreach (int id in Players.Keys)
                if (EffectiveVote(id) == owner)
                    n++;
            return n;
        }

        /// A vote from this game's player: the proposer's id, or 0 for "no room".
        public void CastExpandVote(int choice)
        {
            if (!ExpandOpen || !BuildVoting || Me == null || choice < 0 || (choice != 0 && ProposalOf(choice) == null))
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
            if (!IsHost || !ExpandOpen || !BuildVoting || !Players.ContainsKey(id) || choice < 0 || (choice != 0 && ProposalOf(choice) == null))
                return;
            ExpandVotes[id] = choice;
            FinishOrBroadcast();
        }

        /// This game's player draws a room (a box in world px on the 100 px grid) and puts it to the vote.
        public void ProposeExpandRoom(int x, int y, int w, int h, PathId theme = PathId.None)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostExpandDraw(MyId, x, y, w, h, theme);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandDraw);
            pw.Write((short)x); pw.Write((short)y); pw.Write((short)w); pw.Write((short)h); pw.Write((byte)theme);
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandDraw(int peer, BinaryReader r) => HostExpandDraw(peer, r.ReadInt16(), r.ReadInt16(), r.ReadInt16(), r.ReadInt16(), (PathId)r.ReadByte());

        /// Host: a drawing becomes its drawer's proposal (a newer one replaces the last, and the votes for the old one go). Drawing
        /// doesn't cast a vote: the drawer backs their own until they vote, and the round isn't settled early by drawing. It must fit and the drawer must have the vectors; they only pay if it wins.
        void HostExpandDraw(int id, int x, int y, int w, int h, PathId theme = PathId.None)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            string why;
            if (!Expansion.ThemeUnlocked(theme, Wave))
            {
                DoLog("proposal refused (the " + theme + " theme is still locked)");
                return;
            }
            var made = Expansion.Custom(Expanded, x, y, w, h, theme, out why);
            if (!made.HasValue || made.Value.Price > SpendableVectors(p))
            {
                DoLog("proposal refused (" + (why ?? "needs " + made.Value.Price + " vectors, has " + p.Vectors) + ")");
                return;
            }
            var room = made.Value;
            if (FreeBuild)
            {
                room.Owner = id;
                BuildNow(id, room, null);
                return;
            }
            SetProposal(id, new List<ExpansionRoom> { room });
            DoLog(p.Name + " proposes a " + w + " x " + h + (theme != PathId.None ? " " + theme : "") + " room for " + room.Price + " vectors");
            FinishOrBroadcast();
        }

        /// This game's player proposes opening a stretch of interior wall (it pays vectors back, 1 per tile still wall).
        public void ProposeExpandCut(WallCut c)
        {
            if (!CanCut || Me == null)
                return;
            if (IsHost)
            {
                HostExpandCut(MyId, c);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandCut);
            pw.Write(c.Vertical); pw.Write(c.At); pw.Write(c.From); pw.Write(c.Till);
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandCut(int peer, BinaryReader r) => HostExpandCut(peer, new WallCut { Vertical = r.ReadBoolean(), At = r.ReadInt16(), From = r.ReadInt16(), Till = r.ReadInt16() });

        /// Host: a wall opening becomes its proposer's proposal (it replaces their room proposal, if any, and the votes it had).
        void HostExpandCut(int id, WallCut c)
        {
            Player p;
            if (!IsHost || !CanCut || !Players.TryGetValue(id, out p))
                return;
            string why;
            int refund = Expansion.CutRefund(Expanded, Openings, c, out why);
            if (refund <= 0)
            {
                DoLog("opening refused (" + why + ")");
                return;
            }
            if (FreeBuild)
            {
                var cutRoom = Expansion.CutProposal(c, refund);
                cutRoom.Owner = id;
                BuildNow(id, cutRoom, null);
                return;
            }
            SetProposal(id, new List<ExpansionRoom> { Expansion.CutProposal(c, refund) });
            DoLog(p.Name + " proposes opening " + c.Length / Expansion.Step + " tiles of wall for " + refund + " vectors");
            FinishOrBroadcast();
        }

        /// This game's player proposes a shutter gate on the door at `at` (Docs/RoomBuilding2.md step 3).
        public void ProposeExpandGate(V2 at)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostExpandGate(MyId, at);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandGate);
            pw.Write((short)Math.Round(at.X)); pw.Write((short)Math.Round(at.Y));
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandGate(int peer, BinaryReader r) => HostExpandGate(peer, new V2(r.ReadInt16(), r.ReadInt16()));

        /// Host: a gate becomes its proposer's proposal (replacing theirs, and the votes it had), if the door can take one and they can pay.
        void HostExpandGate(int id, V2 at)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            string why;
            if (!Gates.CanFit(ExpansionWorld, GateFits, at, out why) || Gates.Price > SpendableVectors(p))
            {
                DoLog("gate refused (" + (why ?? "needs " + Gates.Price + " vectors, has " + p.Vectors) + ")");
                return;
            }
            var prop = Gates.Proposal(Gates.Of(ExpansionWorld.Doors[Gates.DoorAt(ExpansionWorld, at)]));
            prop.Owner = id;
            if (FreeBuild)
            {
                BuildNow(id, prop, null);
                return;
            }
            int old = ExpandProposals.FindIndex(q => q.Owner == id);
            if (old >= 0)
            {
                ExpandProposals[old] = prop;
                foreach (int voter in new List<int>(ExpandVotes.Keys))
                    if (ExpandVotes[voter] == id)
                        ExpandVotes.Remove(voter);
            }
            else
                ExpandProposals.Add(prop);
            ExpandVotes.Remove(id);
            DoLog(p.Name + " proposes a gate for " + Gates.Price + " vectors");
            FinishOrBroadcast();
        }

        /// This game's player proposes a portal pair between two rooms (it costs vectors, by the walking it saves).
        public void ProposeExpandLink(PortalLink l)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostExpandLink(MyId, l);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandLink);
            pw.Write(l.AX); pw.Write(l.AY); pw.Write(l.BX); pw.Write(l.BY);
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandLink(int peer, BinaryReader r) => HostExpandLink(peer, new PortalLink { AX = r.ReadInt16(), AY = r.ReadInt16(), BX = r.ReadInt16(), BY = r.ReadInt16() });

        /// Host: a portal pair becomes its proposer's proposal (replacing theirs, and the votes it had), if it fits and they can pay.
        void HostExpandLink(int id, PortalLink l)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            string why;
            int price = Expansion.LinkPrice(ExpansionWorld, Links.Count, l, out why);
            if (price <= 0 || price > SpendableVectors(p))
            {
                DoLog("portal refused (" + (why ?? "needs " + price + " vectors, has " + p.Vectors) + ")");
                return;
            }
            if (FreeBuild)
            {
                var linkRoom = Expansion.LinkProposal(l, price);
                linkRoom.Owner = id;
                BuildNow(id, linkRoom, null);
                return;
            }
            SetProposal(id, new List<ExpansionRoom> { Expansion.LinkProposal(l, price) });
            DoLog(p.Name + " proposes a portal pair for " + price + " vectors");
            FinishOrBroadcast();
        }

        /// This game's player draws a room as an outline (corners on the 100 px grid, each {x, y}, walls straight across or up) and puts it
        /// to the vote. fill: the corners are an open run of walls from one room's wall to another, and the space they fence in is the room (Expansion.Fill).
        public void ProposeExpandOutline(IList<int[]> points, PathId theme = PathId.None, bool fill = false)
        {
            if (!ExpandOpen || Me == null || points == null || points.Count < (fill ? 2 : 4) || points.Count > Expansion.MaxPoints)
                return;
            if (IsHost)
            {
                HostExpandOutline(MyId, points, theme, fill);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandOutline);
            pw.Write((byte)theme);
            pw.Write(fill);
            pw.Write((byte)points.Count);
            foreach (var p in points)
            {
                pw.Write((short)p[0]); pw.Write((short)p[1]);
            }
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandOutline(int peer, BinaryReader r)
        {
            var theme = (PathId)r.ReadByte();
            bool fill = r.ReadBoolean();
            int n = r.ReadByte();
            if (n > Expansion.MaxPoints)
                return;
            var points = new List<int[]>(n);
            for (int i = 0; i < n; i++)
                points.Add(new int[] { r.ReadInt16(), r.ReadInt16() });
            HostExpandOutline(peer, points, theme, fill);
        }

        /// Host: a drawn outline becomes its drawer's proposal (one head and its pieces), if it fits and they can pay.
        void HostExpandOutline(int id, IList<int[]> points, PathId theme, bool fill)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            if (!Expansion.ThemeUnlocked(theme, Wave))
            {
                DoLog("proposal refused (the " + theme + " theme is still locked)");
                return;
            }
            string why;
            var made = fill ? Expansion.Fill(Expanded, points, theme, out why) : Expansion.Outline(Expanded, points, theme, out why);
            if (made == null || made[0].Price > SpendableVectors(p))
            {
                DoLog("proposal refused (" + (why ?? "needs " + made[0].Price + " vectors, has " + p.Vectors) + ")");
                return;
            }
            if (FreeBuild)
            {
                var ohead = made[0];
                ohead.Owner = id;
                var opieces = new List<ExpansionRoom>();
                for (int i = 1; i < made.Count; i++)
                {
                    var piece = made[i];
                    piece.Owner = id;
                    opieces.Add(piece);
                }
                BuildNow(id, ohead, opieces);
                return;
            }
            SetProposal(id, made);
            DoLog(p.Name + " proposes a " + points.Count + "-corner " + (fill ? "fenced-in " : "") + "room (" + made.Count + " boxes) for " + made[0].Price + " vectors");
            FinishOrBroadcast();
        }

        /// Host: the round is settled the moment a vote decides it (a choice backed by more than half the team, a drawer who
        /// hasn't voted backing their own) or once everyone has cast a vote; until then everyone is told how it stands.
        void FinishOrBroadcast()
        {
            bool all = true;
            foreach (int pid in Players.Keys)
            {
                // A solo player has nobody to vote with: their proposal is built the moment it is made.
                if (Players.Count == 1 && ProposalOf(pid).HasValue)
                    ExpandVotes[pid] = pid;
                all &= ExpandVotes.ContainsKey(pid);
            }
            if (all || HasMajority())
                ResolveBuildRound();
            else
                BroadcastExpansion();
        }

        /// True when one choice (a standing proposal or "no room") is backed by more than half the players: no later vote can change it.
        bool HasMajority()
        {
            var tally = new Dictionary<int, int>();
            foreach (int pid in Players.Keys)
            {
                int choice = EffectiveVote(pid);
                if (choice < 0)
                    continue;
                if (choice != 0)
                {
                    var q = ProposalOf(choice);
                    if (!q.HasValue || !Players.ContainsKey(choice) || SpendableVectors(Players[choice]) < q.Value.Price)
                        continue;
                }
                int n;
                tally.TryGetValue(choice, out n);
                tally[choice] = ++n;
                if (n * 2 > Players.Count)
                    return true;
            }
            return false;
        }

        /// Host, free building: a drawing is built the moment it's valid, its drawer pays (an opening pays them), everyone is told.
        void BuildNow(int id, ExpansionRoom room, List<ExpansionRoom> legs)
        {
            ApplyBuilt(id, room, legs ?? new List<ExpansionRoom>());
            roundBuilt++;
            BroadcastExpansion();
        }

        /// Host: build this drawing for this player (a room, a wall opening or a portal pair), charging or paying them.
        void ApplyBuilt(int pick, ExpansionRoom room, List<ExpansionRoom> legs)
        {
            if (room.Kind == Arena.RoomKind.Gate)
            {
                GateFits.Add(Gates.FromProposal(room));
                PayVectors(Players[pick], room.Price);
                DoLog("gate fitted by " + Players[pick].Name + " for " + room.Price + " vectors");
            }
            else if (room.Kind == Arena.RoomKind.Link)
            {
                Links.Add(Expansion.LinkOf(room));
                PayVectors(Players[pick], room.Price);
                DoLog("portal pair built by " + Players[pick].Name + " for " + room.Price + " vectors");
            }
            else if (room.Kind == Arena.RoomKind.Opening)
            {
                Openings.Add(Expansion.CutOf(room));
                AddVectors(Players[pick], -room.Price);
                DoLog("wall opened: " + (room.W + room.H) / Expansion.Step + " tiles by " + Players[pick].Name + " for +" + -room.Price + " vectors");
            }
            else
            {
                PayVectors(Players[pick], room.Price);
                Expanded.Add(room);
                Expanded.AddRange(legs);  // (a drawn outline's other pieces, right after the first)
                DoLog((legs.Count > 0 ? "drawn room of " + (legs.Count + 1) + " boxes" : "room") + " built: " + room.W + " x " + room.H + " by " + Players[pick].Name + " for " + room.Price + " vectors");
            }
            expWorld = null;
            LayoutAge = 0f;  // the new walls rise
            Sounds.Add(Sfx.Pick);
        }

        /// Host, when everyone has voted or the next wave is about to start: the proposal with the most votes is built, its drawer
        /// pays; a tie (a 50/50) is a random pick among the tied, and "no room" counts as one of them. Nothing is built with no
        /// proposals, no votes, or "no room" winning.
        void ResolveBuildRound()
        {
            if (!IsHost || !ExpandOpen)
                return;
            if (!BuildVoting)  // free building: everything was built as it was drawn, the round just closes
            {
                ExpandOpen = false;
                BroadcastExpansion();
                return;
            }
            var standing = ExpandProposals.FindAll(q => q.Leg == 0 && Players.ContainsKey(q.Owner) && SpendableVectors(Players[q.Owner]) >= q.Price);
            var tally = new Dictionary<int, int>();
            foreach (int pid in new List<int>(Players.Keys))
            {
                int choice = EffectiveVote(pid);
                if (choice < 0)
                    continue;
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
                    ApplyBuilt(pick, standing.Find(q => q.Owner == pick), LegsOf(pick));
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

        /// A bot draws the cheapest box that claims an unclaimed node (a 3-tile strip out to it, then a room round it), else opens the Gatekeeper's
        /// floating room, if it can afford it. True if it proposed one.
        bool BotBuildsToFloater()
        {
            ExpansionRoom? pick = null;
            int bestRank = 99;
            foreach (var nd in Nodes)
            {
                if (Expansion.NodeClaimed(Expanded, nd))
                    continue;
                int rank = nd.Kind == NodeKind.Shop ? 0 : nd.Kind == NodeKind.Vendor ? 1 : 2;
                var hall = Expansion.NodeStep(Expanded, nd);
                if (hall.HasValue && rank < bestRank)
                {
                    pick = hall;
                    bestRank = rank;
                }
            }
            if (pick.HasValue)
            {
                if (pick.Value.Price > SpendableVectors(Me))
                    return false;
                ProposeExpandRoom(pick.Value.X, pick.Value.Y, pick.Value.W, pick.Value.H);
                return true;
            }
            int best = -1;
            for (int i = 0; i < Expanded.Count; i++)
            {
                if (!Expanded[i].Floating)
                    continue;
                int rank = Expanded[i].Kind == Arena.RoomKind.Shop ? 0 : Expanded[i].Kind == Arena.RoomKind.Vendor ? 1 : 2;
                var a = ExpansionWorld.Areas[i + 1];
                if (a.Sealed && rank < bestRank)
                {
                    best = i;
                    bestRank = rank;
                }
            }
            if (best < 0)
                return false;
            var c = Expansion.ConnectorFor(Expanded, best);
            if (!c.HasValue || c.Value.Price > SpendableVectors(Me))
                return false;
            ProposeExpandRoom(c.Value.X, c.Value.Y, c.Value.W, c.Value.H);
            return true;
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
            if (RoomTokens.Count > 0 && botVoteT >= 0.8f)  // a bot spends its team's kit on a random plain room
            {
                var plain = new List<int>();
                string why;
                for (int i = 0; i < Expanded.Count; i++)
                    if (Expansion.CanRetype(Expanded, i, RoomTokens[0], out why))
                        plain.Add(i);
                if (plain.Count > 0)
                    ApplyRoomToken(0, plain[rng.Next(plain.Count)]);
            }
            if (!botProposed && botVoteT >= 1.2f)
            {
                botProposed = true;
                if (FreeBuild && roundBuilt > 0)
                    return;  // (free building: a bot adds a room only if nobody has this round, so the map doesn't fill at once)
                if (rng.Next(10) < 6 && BotBuildsToFloater())
                    return;
                if (rng.Next(10) < 2)  // now and then a bot links the two rooms farthest apart (Docs/RoomBuilding2.md step 2)
                {
                    var bl = Expansion.BestLink(ExpansionWorld, Links.Count);
                    string lw;
                    int lp = bl.HasValue ? Expansion.LinkPrice(ExpansionWorld, Links.Count, bl.Value, out lw) : 0;
                    if (lp >= 15 && lp <= SpendableVectors(Me))
                    {
                        ProposeExpandLink(bl.Value);
                        return;
                    }
                }
                foreach (var o in Expansion.Offers(expandSeed + MyId * 31, Wave, Expanded))
                {
                    var theme = o.Kind == Arena.RoomKind.Floor && Expansion.ThemeUnlocked(o.Floor, Wave) ? o.Floor : PathId.None;
                    if (o.Price + Expansion.ThemeCost(theme) <= SpendableVectors(Me) && rng.Next(10) < 7)
                    {
                        ProposeExpandRoom(o.X, o.Y, o.W, o.H, theme);
                        break;
                    }
                }
            }
            if (botVoteT >= 3f && !ExpandVotes.ContainsKey(MyId) && ExpandProposals.Count > 0)
                CastExpandVote(rng.Next(10) < 2 ? 0 : ExpandProposals[rng.Next(ExpandProposals.Count)].Owner);
        }

        // --- Saved runs (RunSaves.cs, SaveData.cs) ------------------------------------------------------------------

        /// Host, saving the run: the map the team built, its kits and the seed. A room's drawer is kept as their seat + 1.
        void SaveExpansion(RunSave save, Dictionary<int, int> seatOf)
        {
            save.HasExpansion = true;
            save.ExpandSeed = expandSeed;
            save.FloatCycle = floatCycle;
            foreach (var r in Expanded)
            {
                var room = r;
                int seat;
                room.Owner = r.Owner != 0 && seatOf.TryGetValue(r.Owner, out seat) ? seat + 1 : 0;
                save.Rooms.Add(room);
            }
            save.Cuts.AddRange(Openings);
            save.Portals.AddRange(Links);
            save.Kits.AddRange(RoomTokens);
            save.Nodes.AddRange(Nodes);
        }

        /// Host, saving the run: this player's vectors and shop room shelf.
        static void SaveExpansionSeat(Player p, SeatSave seat)
        {
            seat.Vectors = p.Vectors;
            seat.VectorCap = p.VectorCap;
            bool any = false;
            for (int i = 0; i < MaxShelf; i++)
                any |= p.Shelf[i] >= 0;
            if (!any)
                return;
            for (int i = 0; i < MaxShelf; i++)
                seat.Shelf.Add(p.Shelf[i] >= 0 && p.Shelf[i] < Upgrades.All.Length ? Upgrades.All[p.Shelf[i]].Name : "");
            seat.ShelfSold = p.ShelfSold;
            seat.Restocks = p.Restocks;
        }

        /// Host, continuing a saved run: the map, kits, seed and each seat's vectors and shelf back, then everyone is told.
        void ResumeExpansion(RunSave save)
        {
            var playerOf = new Dictionary<int, int>();  // seat -> the player id now in it
            foreach (var kv in ResumeSeats)
                playerOf[kv.Value] = kv.Key;
            expandSeed = save.ExpandSeed;
            floatCycle = save.FloatCycle;
            Expanded.Clear();
            foreach (var r in save.Rooms)
            {
                var room = r;
                int id;
                room.Owner = r.Owner > 0 && playerOf.TryGetValue(r.Owner - 1, out id) ? id : 0;
                Expanded.Add(room);
            }
            Openings.Clear();
            Openings.AddRange(save.Cuts);
            Links.Clear();
            Links.AddRange(save.Portals);
            Nodes.Clear();
            Nodes.AddRange(save.Nodes);
            RoomTokens.Clear();
            RoomTokens.AddRange(save.Kits);
            expWorld = null;
            LayoutAge = 0f;
            foreach (var p in Players.Values)
            {
                int idx;
                if (!ResumeSeats.TryGetValue(p.Id, out idx) || idx < 0 || idx >= save.Seats.Count)
                    continue;
                var s = save.Seats[idx];
                if (s.Vectors >= 0)
                {
                    p.Vectors = s.Vectors;
                    p.VectorCap = Math.Max(VectorCapBase, s.VectorCap);
                }
                for (int i = 0; i < MaxShelf; i++)
                {
                    string name = i < s.Shelf.Count ? s.Shelf[i] : "";
                    p.Shelf[i] = name.Length == 0 ? -1 : Array.FindIndex(Upgrades.All, u => u.Name == name);
                }
                p.ShelfSold = s.ShelfSold;
                p.Restocks = Math.Min(StockMax, s.Restocks);
            }
            DoLog("the Expanding arena's map is back: " + Expansion.BoughtCount(Expanded) + " rooms");
            BroadcastExpansion();
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
                w.Write(r.Floating);
                w.Write(r.Leg);
                w.Write(r.Surprise);
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
                room.Floating = r.ReadBoolean();
                room.Leg = r.ReadByte();
                room.Surprise = r.ReadByte();
                list.Add(room);
            }
            return list;
        }

        /// Host -> all: the rooms built, whether a round is open, its proposals and the votes so far.
        void BroadcastExpansion()
        {
            var w = Proto.Begin(Msg.ExpandState);
            WriteRooms(w, Expanded);
            w.Write((byte)Openings.Count);
            foreach (var c in Openings)
            {
                w.Write(c.Vertical); w.Write(c.At); w.Write(c.From); w.Write(c.Till);
            }
            w.Write((byte)Links.Count);
            foreach (var l in Links)
            {
                w.Write(l.AX); w.Write(l.AY); w.Write(l.BX); w.Write(l.BY);
            }
            w.Write((byte)GateFits.Count);
            foreach (var g in GateFits)
            {
                w.Write(g.X); w.Write(g.Y); w.Write(g.Vertical);
            }
            w.Write((byte)RoomTokens.Count);
            foreach (var t in RoomTokens)
            {
                w.Write((byte)t.Kind); w.Write((byte)t.Floor);
            }
            w.Write((byte)Nodes.Count);
            foreach (var nd in Nodes)
            {
                w.Write(nd.X); w.Write(nd.Y); w.Write((byte)nd.Kind); w.Write(nd.Spent);
            }
            w.Write(EventMark.HasValue);  // the breach event's gold breach (BreachEvent.cs)
            w.Write((short)(EventMark.HasValue ? EventMark.Value.X : 0f)); w.Write((short)(EventMark.HasValue ? EventMark.Value.Y : 0f));
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
                int before = SurpriseCount(Expanded), oldCount = Expanded.Count;
                bool eventOn = EventMark.HasValue;  // (the gold breach was up when the room came)
                Expanded.Clear();
                Expanded.AddRange(rooms);
                expWorld = null;
                LayoutAge = 0f;
                if (SurpriseCount(Expanded) > before && Expanded.Count > oldCount && ((BossNow && State == MatchState.Intermission) || eventOn))
                {
                    PinBreaches(Expanded.GetRange(oldCount, Expanded.Count - oldCount));  // (Breaches.cs: the wave's other breaches stay put)
                    NoteSurprise(Expanded[Expanded.Count - 1].Surprise);  // (the host's gift, after a boss or from a breach event: the build-in and the chip; a joiner mid-run gets the rooms quietly)
                }
            }
            int nc = r.ReadByte();
            var cuts = new List<WallCut>(nc);
            for (int i = 0; i < nc; i++)
                cuts.Add(new WallCut { Vertical = r.ReadBoolean(), At = r.ReadInt16(), From = r.ReadInt16(), Till = r.ReadInt16() });
            bool cutsChanged = cuts.Count != Openings.Count;
            for (int i = 0; !cutsChanged && i < cuts.Count; i++)
                cutsChanged |= !cuts[i].Equals(Openings[i]);
            if (cutsChanged)
            {
                Openings.Clear();
                Openings.AddRange(cuts);
                expWorld = null;
                LayoutAge = 0f;
            }
            int nl = r.ReadByte();
            var links = new List<PortalLink>(nl);
            for (int i = 0; i < nl; i++)
                links.Add(new PortalLink { AX = r.ReadInt16(), AY = r.ReadInt16(), BX = r.ReadInt16(), BY = r.ReadInt16() });
            bool linksChanged = links.Count != Links.Count;
            for (int i = 0; !linksChanged && i < links.Count; i++)
                linksChanged |= !links[i].Equals(Links[i]);
            if (linksChanged)
            {
                Links.Clear();
                Links.AddRange(links);
                expWorld = null;
                LayoutAge = 0f;
            }
            int ng = r.ReadByte();
            GateFits.Clear();
            for (int i = 0; i < ng; i++)
                GateFits.Add(new GateFit { X = r.ReadInt16(), Y = r.ReadInt16(), Vertical = r.ReadBoolean() });
            RoomTokens.Clear();
            int nt = r.ReadByte();
            for (int i = 0; i < nt; i++)
                RoomTokens.Add(new RoomToken { Kind = (RoomKind)r.ReadByte(), Floor = (PathId)r.ReadByte() });
            int nn = r.ReadByte();
            var nodes = new List<MapNode>(nn);
            for (int i = 0; i < nn; i++)
                nodes.Add(new MapNode { X = r.ReadInt16(), Y = r.ReadInt16(), Kind = (NodeKind)r.ReadByte(), Spent = r.ReadBoolean() });
            bool nodesChanged = nodes.Count != Nodes.Count;
            for (int i = 0; !nodesChanged && i < nodes.Count; i++)
                nodesChanged |= !nodes[i].Equals(Nodes[i]);
            if (nodesChanged)
            {
                Nodes.Clear();
                Nodes.AddRange(nodes);
                expWorld = null;
                LayoutAge = 0f;
            }
            bool hasMark = r.ReadBoolean();
            short markX = r.ReadInt16(), markY = r.ReadInt16();
            EventMark = hasMark ? new V2(markX, markY) : (V2?)null;
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

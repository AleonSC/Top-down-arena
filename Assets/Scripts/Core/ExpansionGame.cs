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
        public static float ExpandScaleRate = 0.6f, MaxExpandScale = 1.4f;
        public static float ExpandScaleRateEarly = 1.1f, MaxExpandScaleEarly = 2.4f;
        public const int EarlyWaves = 10;  // (static, not const: the balance report tries others)

        /// The rooms built so far, in order (area 1, 2, ...).
        public readonly List<ExpansionRoom> Expanded = new List<ExpansionRoom>();

        /// The map's nodes (Docs/Nodes.md): shop and Black Market points out in empty space, placed once and never respawned; a built room that
        /// encloses one takes on its job (synced after the tokens in Msg.ExpandState).
        public readonly List<MapNode> Nodes = new List<MapNode>();

        /// A building round is open: from a cleared wave until the next one starts, any player can draw a room and put it to
        /// the team (synced by Msg.ExpandState).
        public bool ExpandOpen;

        /// The room kits the team holds (spent with ApplyRoomToken; synced in Msg.ExpandState). Bosses give a Floor kit, a won challenge room a
        /// Shop kit, the Black Market sells a Floor kit.
        public readonly List<RoomToken> RoomTokens = new List<RoomToken>();

        /// What this player can put towards a build: their own vectors (the old Team purse is gone, Nov 18).
        public int SpendableVectors(Player p) => p != null ? p.Vectors : 0;

        /// Host: charge a build to its payer.
        void PayVectors(Player payer, int price)
        {
            int take = Math.Min(payer.Vectors, price);
            payer.Vectors -= take;
            payer.MapSpent += take;  // (half comes back when the map resets, MapReset.cs)
        }

        int expandSeed;
        int roundBuilt;  // free building: drawings built since this round opened (bots only build when it's 0)
        float botBuildT;
        bool botProposed;
        World expWorld;

        /// The run's arena is the Expanding one.
        public bool ExpandingOn => ArenaKind == ArenaLayout.Expanding;

        /// The world of the hub plus the rooms bought (built once per change).
        /// The stretches of interior wall the team has opened.
        public readonly List<WallCut> Openings = new List<WallCut>();

        /// The portal pairs the team bought (Docs/RoomBuilding2.md step 2; synced after the openings in Msg.ExpandState).
        public readonly List<PortalLink> Links = new List<PortalLink>();
        /// Where node i stands now: a claimed chest or relic stands in the middle of its room (World.NodeSpots, Expansion.NodeSpotIn), else at its own point.
        public V2 NodeSpot(int i)
        {
            var w = Expansion.CentresInRoom(Nodes[i].Kind) ? ExpansionWorld : null;
            return w != null && w.NodeSpots != null && i < w.NodeSpots.Length ? w.NodeSpots[i] : Nodes[i].Pos;
        }

        public World ExpansionWorld => expWorld ?? (expWorld = Expansion.Build(Expanded, Openings, Links, Nodes, CoreLayout));

        /// More floor means more enemies (Rooms.EnemyDamageMult also makes its enemies hit harder).
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
                float ratio = area / (Width * Height) - 1f;
                if (Wave <= EarlyWaves)  // the first ten waves: the crowd was thin on a big map, rooms stayed empty
                    return Math.Min(MaxExpandScaleEarly, 1f + ExpandScaleRateEarly * ratio);
                return Math.Min(MaxExpandScale, 1f + ExpandScaleRate * ratio);
            }
        }

        // --- Vectors (Docs/VectorBuilding.md, step 1) ------------------------------------------------------------

        /// Each player's drawing budget: 1 vector = 1 tile of new wall (Expansion.Cost). Start with VectorStart, hold up to VectorCap
        /// (VectorCapBase for now), +VectorWaveGain for each cleared wave, a 1-vector pickup every VectorKillEvery kills, and a cleared boss wave
        /// refills everyone to the cap and VectorBossBonus on top.
        public const int VectorStart = 40, VectorCapBase = 70, VectorWaveGain = 8, VectorBossBonus = 20, VectorKillEvery = 8;

        /// Host: gains stop at the cap (a stock already above it, from a boss, is left alone).
        void AddVectors(Player p, int n)
        {
            n = Draftsman(p, n);  // the Engineer's passive: +25% (Engineer.cs)
            if (n > 0 && p.Vectors < p.VectorCap)
                p.Vectors = Math.Min(p.VectorCap, p.Vectors + n);
        }

        /// Host, when a wave is cleared: a boss wave refills everyone (downed too), any other gives a few.
        void GrantVectorsForWave()
        {
            if (!ExpandingOn)
                return;
            bool boss = BossNow;
            if (boss && IsHost && ModeRules.RunEvents && !BossCardPicks)  // (with room cards the boss's card pick offers kits instead)
                GrantRoomToken(Expansion.FloorToken(expandSeed, Wave, Expanded, RoomTokens));
            DropWaveCards(boss);  // Pieces.cs
            foreach (var p in Players.Values)
            {
                p.VectorCap = VectorCapBase + p.Stats.VectorCap;
                if (boss)
                    p.Vectors = Math.Max(p.Vectors, p.VectorCap + VectorBossBonus);
                else
                    AddVectors(p, LongLegs ? VectorWaveGain / 2 : VectorWaveGain);  // (a Campaign has twice the waves)
            }
        }

        /// Host, on a kill: every VectorKillEvery kills a player has made drop a small vector pickup where the enemy fell (Coins.cs;
        /// the killer's own, like their copy of any vector pickup).
        void GrantVectorKill(Player killer, V2 at)
        {
            if (ExpandingOn && killer != null && killer.Run.Kills > 0 && killer.Run.Kills % VectorKillEvery == 0)
                AddCoin(killer.Id, 1, true, at, 10f, 14f);  // (the killer's own reward: theirs alone, not a copy each)
        }

        void SyncVectors(NetSync s, Player p)
        {
            s.Short(ref p.Vectors);
            s.Short(ref p.VectorCap);
            for (int i = 0; i < p.Hand.Length; i++)
                s.Short(ref p.Hand[i]);  // room cards (Pieces.cs): 2 bytes, a shape card carries its modifiers in its code
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

        /// A Black Market room is on the connected map.
        public bool HasBlackMarketRoom
        {
            get
            {
                foreach (var a in ExpansionWorld.Areas)
                    if (a.Kind == Arena.RoomKind.BlackMarket && !a.Sealed)
                        return true;
                return false;
            }
        }

        /// The Expanding arena has a shop or Black Market you can walk into: kills drop gold, the shop opens in each intermission without holding the
        /// countdown, your gold and shelf are kept between waves, and what you can do depends on the room you stand in.
        public bool ServiceRooms => ExpandingOn && (HasShopRoom || HasBlackMarketRoom || HasArmsRoom || HasMedicRoom);

        /// The shop is open and the wave has to wait for it (the Bazaar shop; the room shops never hold the countdown).
        public bool ShopBlocksWave => ShopOpen && !ServiceRooms;

        public bool InBlackMarketRoom(Player p)
        {
            var a = ExpandedAreaAt(p.Disp);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.BlackMarket;
        }

        public bool InServiceRoom(Player p) => InShopRoom(p) || InBlackMarketRoom(p) || InArmsRoom(p) || InMedicRoom(p);

        /// In a room shop, upgrades and Add stock are bought in the shop room, buildables only at the Black Market (Docs/BlackMarket.md), mods at the
        /// Arms Dealer, heals and revive tokens only at the Medic, and the other services (sell, Floor kit, vectors) in the shop room: each vendor sells its own wares only.
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
                    return InMedicRoom(p);
                case ShopAction.Sell:
                case ShopAction.RoomKit:
                case ShopAction.Vectors:
                    return InShopRoom(p);
                case ShopAction.BuyBuild:
                    return InBlackMarketRoom(p);
                case ShopAction.BuyCard:
                    return false;  // (retired: the Black Market no longer sells cards)
                case ShopAction.BuyMod:
                    return InArmsRoom(p);
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

        /// The path whose ground this point is on (a rule room), else None. Its rules hold in that room only.
        public PathId FloorAt(V2 pos)
        {
            var a = ExpandedAreaAt(pos);
            return a.HasValue && a.Value.Kind == Arena.RoomKind.Floor ? a.Value.Floor : PathId.None;
        }

        /// Kills in a rule room pay that path's bonus (+25% XP and score at danger 2, +50% at 3).
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

        /// Host, as a regular wave starts: a new boss cycle gets its nodes off the connected map (chest, relic, rule, vein; Docs/Nodes.md). Nothing placed is ever
        /// placed again: at most MaxNodesWaiting nodes wait. Shop, Black Market and Gatekeeper rooms are no longer placed here (room cards and mystery doors only).
        void PlaceFloatingRooms()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents)
                return;
            int cycle = (Wave - 1) / Expansion.Milestone;
            if (cycle <= floatCycle)
                return;
            floatCycle = cycle;
            bool added = false;
            // (no floating rooms any more, owner Nov 18: shop, Black Market and Gatekeeper rooms come from room cards and mystery doors only; old saves keep theirs)
            // (owner Nov 20: the health shrine is the only node; chests roll in the rooms players build instead, RoomChests.cs. The first cycle rings the hub with
            // ShrineRing shrines, one per side in turn; each later cycle adds one)
            int count = cycle == 0 ? Shrines.Ring : 1;
            for (int i = 0; i < count; i++)
            {
                if (Expansion.UnclaimedCount(Expanded, Nodes) >= Expansion.MaxNodesWaiting)
                    break;
                var nd = Expansion.GridNodeFor(expandSeed, cycle, Expanded, Nodes, NodeKind.Shrine, cycle == 0 ? i : -1, cycle == 0 ? Expansion.RingTier(i) : Expansion.CycleTier(NodeKind.Chest, cycle));
                if (!nd.HasValue)
                    continue;
                Nodes.Add(nd.Value);
                added = true;
                DoLog("a health shrine node appears at " + nd.Value.X + "," + nd.Value.Y);
            }
            added |= PlaceRelicNodes(cycle);  // Relics.cs: this cycle's boss faction's relics
            added |= PlaceGridMini(cycle);  // GridPieces.cs: a Grid run's mini-boss waits on the lattice
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
                if (r.Surprise > 0 && r.Surprise != 1 + (int)SurpriseShape.Stash && r.Surprise != RelicGauntlet.Mark && r.Piece == 0)  // (the breach Stashes and the Relic Gauntlet aren't gifts: they don't turn the rotation)
                    n++;
            return n;
        }

        /// The shape the next surprise room tries first: the five come round in turn from a seeded start.
        SurpriseShape NextSurpriseShape()
        {
            int shapes = (int)SurpriseShape.Stash;  // the five gifts (the Stash is only for breaches)
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
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents || (Wave >= LastWave && !Endless) || Expansion.BoughtCount(Expanded) > Expansion.MaxRooms - 2)
                return;
            string why;
            var pick = Expansion.SurpriseFor(expandSeed, Wave, Expanded, NextSurpriseShape(), out why);  // (a Grid run: on the lattice, PieceShapes.cs)
            if (!pick.HasValue)
            {
                DoLog("no surprise room: " + why);
                return;
            }
            Expanded.AddRange(pick.Value.Rooms);
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            NoteSurprise(pick.Value.Rooms[0].Surprise);
            Sounds.Add(Sfx.Pick);
            DoLog("surprise room: " + Expansion.SurpriseName(pick.Value.Shape) + " of " + pick.Value.Rooms.Count + " boxes at " + pick.Value.Rooms[0].X + "," + pick.Value.Rooms[0].Y + ", closes " + pick.Value.Loops + " loops");
            BroadcastExpansion();
        }

        public const int VectorChestPickups = 4;  // a vector chest bursts into this many vector pickups (VectorDropSmall each: 24 vectors)

        /// Host, every tick of a wave: a chest node whose room is built holds a treasure chest (SpawnChest; put back each wave if it was cleared
        /// away). Once it is opened the node is spent (its `onOpened` hook). The wave never waits on it. A rule room's chest comes out once the room is cleared.
        void TickNodes()
        {
            if (!IsHost || !ExpandingOn || Nodes.Count == 0)
                return;
            TickRelicNodes();  // Relics.cs
            for (int i = 0; i < Nodes.Count; i++)
            {
                var nd = Nodes[i];
                if (nd.Kind != NodeKind.Chest || nd.Spent || !Expansion.NodeClaimed(Expanded, nd))
                    continue;
                if (ChestAt(NodeSpot(i)) != null)
                    continue;
                int node = i;
                SpawnChest(NodeSpot(i), NodeChestKind(nd), c => SpendNode(node), false, i);
            }
        }

        /// Host: a node's chest has opened, so the node is spent.
        void SpendNode(int i)
        {
            if (!IsHost || i < 0 || i >= Nodes.Count)
                return;
            var nd = Nodes[i];
            nd.Spent = true;
            Nodes[i] = nd;
            DoLog("a chest node's chest was opened");
            BroadcastExpansion();
        }

        /// What a chest node's chest holds: seeded from the map and the node's spot, so every machine knows it before it opens
        /// (the node's look). Room card only with room cards on.
        public ChestKind NodeChestKind(MapNode nd) => Expansion.NodeChestKind(expandSeed, nd, ExpandingOn);

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
            CellLayouts.ClearForced();  // (a layout test's cells, LayoutTest.cs)
            RoomTokens.Clear();
            duelBoss.Clear();  // (GatekeeperRoom.cs: a duel from the last run's map must not outlive it)
            floatCycle = -1;
            Expanded.Clear();
            Nodes.Clear();
            Openings.Clear();
            Links.Clear();
            ResetGates();
            ResetMysteryDoors();  // MysteryDoors.cs
            MysteryDoorsOpened = MysteryVectorsSpent = 0;  // (a run's numbers; a map reset keeps them)
            ExpandOpen = false;
            expWorld = null;
            botBuildT = 0f;
            botProposed = false;
            expandSeed = sideRng.Next(1, 1 << 30);  // (not the game's rng: asking it would shift every other random choice of the run; Game.cs)
            MapNumber = 1;
            CoreLayoutsUsed = 0;
            CoreLayout = CoreLayoutId.None;
            PickCoreLayout();  // (CoreLayouts.cs)
            MapResetAge = 99f;
            KeepsakeOpen = false;
            KeepsakeVotes.Clear();
            KeptHead = -1;
            MapRefunds.Clear();
            FoldRooms.Clear();
            MapRefund = 0;
            foreach (var p in Players.Values)
                p.MapSpent = 0;
        }

        // --- The building round -------------------------------------------------------------------------------------

        /// Host, when a wave is cleared: a building round opens (not in a Boss Hunt, after the last wave or on a full map).
        void OpenBuildRound()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents || (Wave >= LastWave && !Endless) || Expansion.BoughtCount(Expanded) >= Expansion.MaxRooms)
                return;
            ExpandOpen = true;
            roundBuilt = 0;
            DealRoundCards();  // Pieces.cs
            botBuildT = 0f;
            botProposed = false;
            BroadcastExpansion();
            DoLog("building round opens");
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

        /// Host: a gate is fitted at once, if the door can take one and they can pay.
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
            BuildNow(id, prop, null);
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

        /// Host: a portal pair is built at once from the player's stash, if it fits.
        void HostExpandLink(int id, PortalLink l)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            string why;
            if (p.Stash[(int)BuildKind.Teleporter] == 0)
            {
                DoLog("portal refused (no teleporter in your stash: buy one at the Black Market)");
                return;
            }
            if (!Expansion.LinkFits(ExpansionWorld, Links.Count, l, out why))
            {
                DoLog("portal refused (" + why + ")");
                return;
            }
            p.Stash[(int)BuildKind.Teleporter]--;  // (a teleporter pair from the stash: no vectors)
            var linkRoom = Expansion.LinkProposal(l);
            linkRoom.Owner = id;
            BuildNow(id, linkRoom, null);
            SendSnapshot();
        }

        /// This game's player dismantles the portal pair with an end at this wall spot (the pair goes back to their stash).
        public void ProposeUnlink(V2 end)
        {
            if (!ExpandOpen || Me == null)
                return;
            if (IsHost)
            {
                HostUnlink(MyId, end);
                return;
            }
            var pw = Proto.Begin(Msg.ExpandUnlink);
            pw.Write((short)end.X); pw.Write((short)end.Y);
            session.SendToHost(Proto.End(pw));
        }

        public void OnExpandUnlink(int peer, BinaryReader r) => HostUnlink(peer, new V2(r.ReadInt16(), r.ReadInt16()));

        /// Host, in a build round: the pair with an end at `end` is taken down and its teleporter goes to the dismantler's stash.
        void HostUnlink(int id, V2 end)
        {
            Player p;
            if (!IsHost || !ExpandOpen || !Players.TryGetValue(id, out p))
                return;
            int at = -1;
            for (int i = 0; i < Links.Count && at < 0; i++)
                if (Links[i].A.CloserThan(end, 1f) || Links[i].B.CloserThan(end, 1f))
                    at = i;
            if (at < 0)
            {
                DoLog("dismantle refused (no portal pair there)");
                return;
            }
            Links.RemoveAt(at);
            p.Stash[(int)BuildKind.Teleporter]++;
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            DoLog("portal pair dismantled by " + p.Name + " (back to their stash)");
            Sounds.Add(Sfx.Pick);
            BroadcastExpansion();
            SendSnapshot();
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
                DoLog("portal pair built by " + Players[pick].Name);
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
            RaiseRoomsNow();  // (the first room: the world goes up now, Rooms.cs)
            TickNodes();  // a chest node this room encloses gets its chest now, not at the next wave's first tick
            Sounds.Add(Sfx.Pick);
        }

        /// Host, when the next wave is about to start: the building round closes (everything was built as it was placed).
        void ResolveBuildRound()
        {
            if (!IsHost || !ExpandOpen)
                return;
            ExpandOpen = false;
            BroadcastExpansion();
        }

        /// Every frame, every game: a bot player builds a room it can afford a moment after the round opens.
        void TickExpansion(float dt)
        {
            TickNodeFog(dt);  // NodeScouting.cs
            TickShrines();    // Shrines.cs (host: in waves and between them)
            TickGridCore();  // GridRooms.cs
            TickRoomFog(dt);  // RoomFog.cs
            if (!ExpandOpen)
            {
                botBuildT = 0f;
                botProposed = false;
                return;
            }
            if (!Bot || Me == null)
                return;
            botBuildT += dt;
            if (RoomTokens.Count > 0 && botBuildT >= 0.8f)  // a bot spends its team's kit on a random plain room
            {
                var plain = new List<int>();
                string why;
                for (int i = 0; i < Expanded.Count; i++)
                    if (Expansion.CanRetype(Expanded, i, RoomTokens[0], out why))
                        plain.Add(i);
                if (plain.Count > 0)
                    ApplyRoomToken(0, plain[rng.Next(plain.Count)]);
            }
            if (!botProposed && botBuildT >= 1.2f)
            {
                botProposed = true;
                if (roundBuilt > 0)
                    return;  // (a bot adds a room only if nobody has this round, so the map doesn't fill at once)
                BotPlayPieceCard();  // GridPieces.cs
            }
        }

        // --- Saved runs (RunSaves.cs, SaveData.cs) ------------------------------------------------------------------

        /// Host, saving the run: the map the team built, its kits and the seed. A room's drawer is kept as their seat + 1.
        void SaveExpansion(RunSave save, Dictionary<int, int> seatOf)
        {
            save.HasExpansion = true;
            save.ExpandSeed = expandSeed;
            save.FloatCycle = floatCycle;
            save.MapNumber = MapNumber;
            save.CoreLayout = CoreLayout;
            save.CoreLayoutsUsed = CoreLayoutsUsed;
            save.KeepsakePending = KeepsakeOpen;  // (left mid-vote: the vote opens again on Continue)
            foreach (var r in Expanded)
            {
                var room = r;
                int seat;
                room.Owner = r.Owner != 0 && seatOf.TryGetValue(r.Owner, out seat) ? seat + 1 : 0;
                save.Rooms.Add(room);
            }
            for (int i = 0; i < Expanded.Count && i < fogCells.Length; i++)
                if (Expanded[i].Unseen && fogCells[i] != null)
                    save.Fog[i] = FogHex(fogCells[i]);  // (the explored cells, RoomFog.cs)
            save.Cuts.AddRange(Openings);
            save.Portals.AddRange(Links);
            save.Builds.AddRange(Placed);
            save.Kits.AddRange(RoomTokens);
            save.Nodes.AddRange(Nodes);
        }

        /// Host, saving the run: this player's vectors and shop room shelf.
        static void SaveExpansionSeat(Player p, SeatSave seat)
        {
            seat.Vectors = p.Vectors;
            for (int i = 0; i < Market.Count; i++)
                seat.Stash[i] = p.Stash[i];
            seat.VectorCap = p.VectorCap;
            seat.MapSpent = p.MapSpent;
            foreach (var c in p.Hand)
                if (c != 0) seat.Hand.Add(c);
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
            MapNumber = Math.Max(1, save.MapNumber);
            CoreLayoutsUsed = save.CoreLayoutsUsed;
            CoreLayout = save.CoreLayout;
            if (CoreLayout == CoreLayoutId.None)  // (a save from before core layouts)
                PickCoreLayout();
            else
                layoutFresh = true;
            KeepsakeOpen = false;
            KeepsakeVotes.Clear();
            KeptHead = -1;
            duelBoss.Clear();
            SettleRoomFog();  // (the saved rooms keep their own fog)
            Expanded.Clear();
            foreach (var r in save.Rooms)
            {
                var room = r;
                int id;
                room.Owner = r.Owner > 0 && playerOf.TryGetValue(r.Owner - 1, out id) ? id : 0;
                Expanded.Add(room);
            }
            LoadFogCells(save.Fog);  // (RoomFog.cs)
            Openings.Clear();
            Openings.AddRange(save.Cuts);
            Links.Clear();
            Links.AddRange(save.Portals);
            Placed.Clear();
            Placed.AddRange(save.Builds);
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
                    p.MapSpent = Math.Max(0, s.MapSpent);
                }
                for (int i = 0; i < MaxShelf; i++)
                {
                    string name = i < s.Shelf.Count ? s.Shelf[i] : "";
                    p.Shelf[i] = name.Length == 0 ? -1 : Array.FindIndex(Upgrades.All, u => u.Name == name);
                }
                for (int i = 0; i < Market.Count; i++)
                    p.Stash[i] = (byte)Math.Min(Market.StashMax, s.Stash[i]);
                Array.Clear(p.Hand, 0, p.Hand.Length);
                for (int i = 0; i < s.Hand.Count && i < p.Hand.Length; i++)
                    p.Hand[i] = s.Hand[i];
                p.CardsDealt = s.Hand.Count > 0 || s.Vectors >= 0;  // (a saved seat had its starting hand)
                p.ShelfSold = s.ShelfSold;
                p.Restocks = Math.Min(255, s.Restocks);
                p.ShelfLen = 0;
                for (int i = 0; i < MaxShelf; i++)
                    if (p.Shelf[i] >= 0)
                        p.ShelfLen = i + 1;  // (the saved shelf's length: up to its last card)
            }
            DoLog("the Expanding arena's map is back: " + Expansion.BoughtCount(Expanded) + " rooms");
            if (save.KeepsakePending)
                OpenKeepsake();  // the run was left mid keepsake vote: it opens again (MapReset.cs)
            BroadcastExpansion();
        }

        // --- Net ---------------------------------------------------------------------------------------------------

        static void WriteRooms(BinaryWriter w, IList<ExpansionRoom> list)
        {
            w.Write((ushort)list.Count);  // (ushort: a big map can pass 255)
            foreach (var r in list)
            {
                w.Write((short)r.Parent);
                w.Write((short)r.X); w.Write((short)r.Y); w.Write((short)r.W); w.Write((short)r.H);
                w.Write((byte)r.Kind); w.Write((byte)r.Floor);
                w.Write((short)r.Price);
                w.Write((short)r.Owner);
                w.Write(r.Floating);
                w.Write(r.Piece);
                w.Write(r.Surprise);
                w.Write(r.Claimed);
                w.Write(r.Mini);
                w.Write(r.Unseen);
                w.Write(r.OneWay);
            }
        }

        static List<ExpansionRoom> ReadRooms(BinaryReader r)
        {
            int n = r.ReadUInt16();
            var list = new List<ExpansionRoom>(n);
            for (int i = 0; i < n; i++)
            {
                var room = new ExpansionRoom { Parent = r.ReadInt16(), X = r.ReadInt16(), Y = r.ReadInt16(), W = r.ReadInt16(), H = r.ReadInt16() };
                room.Kind = (RoomKind)r.ReadByte();
                room.Floor = (PathId)r.ReadByte();
                room.Price = r.ReadInt16();
                room.Owner = r.ReadInt16();
                room.Floating = r.ReadBoolean();
                room.Piece = r.ReadByte();
                room.Surprise = r.ReadByte();
                room.Claimed = r.ReadBoolean();
                room.Mini = r.ReadByte();
                room.Unseen = r.ReadBoolean();
                room.OneWay = r.ReadBoolean();
                list.Add(room);
            }
            return list;
        }

        /// Host -> all: the rooms built, whether a round is open, its proposals and the votes so far.
        void BroadcastExpansion()
        {
            FogNewRooms();  // rooms added since the last list go out unseen (RoomFog.cs)
            var w = Proto.Begin(Msg.ExpandState);
            WriteRooms(w, Expanded);
            w.Write((ushort)Openings.Count);  // (ushort: a big map can pass 255)
            foreach (var c in Openings)
            {
                w.Write(c.Vertical); w.Write(c.At); w.Write(c.From); w.Write(c.Till);
            }
            w.Write((ushort)Links.Count);  // (ushort: a big map can pass 255)
            foreach (var l in Links)
            {
                w.Write(l.AX); w.Write(l.AY); w.Write(l.BX); w.Write(l.BY);
            }
            w.Write((ushort)Placed.Count);  // (ushort: a big map can pass 255)
            foreach (var b in Placed)
            {
                w.Write((byte)b.Kind); w.Write(b.X); w.Write(b.Y); w.Write(b.Owner); w.Write(b.Waves);
            }
            w.Write((ushort)GateFits.Count);  // (ushort: a big map can pass 255)
            foreach (var g in GateFits)
            {
                w.Write(g.X); w.Write(g.Y); w.Write(g.Vertical);
            }
            w.Write((ushort)RoomTokens.Count);  // (ushort: a big map can pass 255)
            foreach (var t in RoomTokens)
            {
                w.Write((byte)t.Kind); w.Write((byte)t.Floor);
            }
            w.Write((ushort)Nodes.Count);  // (ushort: a big map can pass 255)
            foreach (var nd in Nodes)
            {
                w.Write(nd.X); w.Write(nd.Y); w.Write((byte)nd.Kind); w.Write(nd.Spent); w.Write(nd.Rule); w.Write(nd.Hidden); w.Write(nd.Drop);
            }
            w.Write(EventMark.HasValue);  // the breach event's gold breach (BreachEvent.cs)
            w.Write((short)(EventMark.HasValue ? EventMark.Value.X : 0f)); w.Write((short)(EventMark.HasValue ? EventMark.Value.Y : 0f));
            WriteMysteryDoors(w);  // the sealed doors (MysteryDoors.cs)
            w.Write((byte)Math.Min(255, MapNumber));  // which map of the run (MapReset.cs)
            w.Write((byte)CoreLayout);  // the core's floor plan (CoreLayouts.cs)
            WriteKeepsake(w);  // the kept room and the keepsake vote (MapReset.cs)
            w.Write(ExpandOpen);
            WriteFogCells(w);  // the explored cells of the unseen rooms (RoomFog.cs)
            WriteTrapWrecks(w);  // the room turrets shot to bits (RoomTraps.cs)
            WriteRelicGauntletClock(w);  // the lasers' clock (Gauntlet.cs)
            session.Broadcast(Proto.End(w));
        }

        void ReadExpandState(BinaryReader r)
        {
            var rooms = ReadRooms(r);
            var oldMap = new List<ExpansionRoom>(Expanded);  // (a map reset folds the old map away, MapReset.cs)
            bool changed = rooms.Count != Expanded.Count, claimsOnly = false;
            for (int i = 0; !changed && i < rooms.Count; i++)
            {
                var same = rooms[i]; same.Claimed = Expanded[i].Claimed; same.Unseen = Expanded[i].Unseen;
                changed |= !same.Equals(Expanded[i]);
            }
            if (!changed)  // (only a Stash's cache taken or a room seen: no rebuild, no build-in)
                for (int i = 0; i < rooms.Count; i++)
                    if (rooms[i].Claimed != Expanded[i].Claimed || rooms[i].Unseen != Expanded[i].Unseen)
                    {
                        Expanded[i] = rooms[i];
                        claimsOnly = true;
                    }
            if (changed && !claimsOnly)
            {
                int before = SurpriseCount(Expanded), oldCount = Expanded.Count;
                bool eventOn = EventMark.HasValue;  // (the gold breach was up when the room came)
                if (BreachesOn)
                    _ = ActiveBreaches;  // (this wave's breaches, worked out on the old map first: new rooms must not re-roll them)
                Expanded.Clear();
                Expanded.AddRange(rooms);
                expWorld = null;
                LayoutAge = 0f;
                var fresh = Expanded.Count > oldCount ? Expanded.GetRange(oldCount, Expanded.Count - oldCount) : new List<ExpansionRoom>();
                bool stashNew = oldCount > 0 && fresh.Exists(q => q.Surprise == 1 + (int)SurpriseShape.Stash);  // (breach Stashes: Docs/BreachRooms.md; a joiner's first list is quiet)
                if ((SurpriseCount(Expanded) > before && Expanded.Count > oldCount && ((BossNow && State == MatchState.Intermission) || eventOn)) || stashNew)
                {
                    PinBreaches(Expanded.GetRange(oldCount, Expanded.Count - oldCount));  // (Breaches.cs: the wave's other breaches stay put)
                    NoteSurprise(Expanded[Expanded.Count - 1].Surprise);  // (the host's gift, after a boss or from a breach event: the build-in and the chip; a joiner mid-run gets the rooms quietly)
                }
            }
            int nc = r.ReadUInt16();
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
            int nl = r.ReadUInt16();
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
            int npb = r.ReadUInt16();
            Placed.Clear();
            for (int i = 0; i < npb; i++)
                Placed.Add(new PlacedBuild { Kind = (BuildKind)r.ReadByte(), X = r.ReadInt16(), Y = r.ReadInt16(), Owner = r.ReadByte(), Waves = r.ReadByte() });
            int ng = r.ReadUInt16();
            GateFits.Clear();
            for (int i = 0; i < ng; i++)
                GateFits.Add(new GateFit { X = r.ReadInt16(), Y = r.ReadInt16(), Vertical = r.ReadBoolean() });
            RoomTokens.Clear();
            int nt = r.ReadUInt16();
            for (int i = 0; i < nt; i++)
                RoomTokens.Add(new RoomToken { Kind = (RoomKind)r.ReadByte(), Floor = (PathId)r.ReadByte() });
            int nn = r.ReadUInt16();
            var nodes = new List<MapNode>(nn);
            for (int i = 0; i < nn; i++)
                nodes.Add(new MapNode { X = r.ReadInt16(), Y = r.ReadInt16(), Kind = (NodeKind)r.ReadByte(), Spent = r.ReadBoolean(), Rule = r.ReadByte(), Hidden = r.ReadBoolean(), Drop = r.ReadByte() });
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
            ReadMysteryDoors(r);
            ReadMapNumber(r.ReadByte(), oldMap);  // (MapReset.cs)
            ReadCoreLayout(r.ReadByte());  // (CoreLayouts.cs)
            ReadKeepsake(r);
            ExpandOpen = r.ReadBoolean();
            ReadFogCells(r);  // (RoomFog.cs)
            ReadTrapWrecks(r);  // (RoomTraps.cs)
            ReadRelicGauntletClock(r);  // (Gauntlet.cs)
        }
    }
}

using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    /// A sealed door on the Expanding arena's outer wall (Docs/MysteryDoors.md): a room is rolled behind it but hidden; any player pays
    /// vectors to open it, any time. Synced in Msg.ExpandState (after the breach event's mark); the room behind stays on the host.
    public struct MysteryDoor
    {
        public short X, Y;     // the door's middle, on the wall it will open in
        public bool Vertical;  // the wall runs up and down
        public short Price;    // vectors to open it
        public int Saver;      // the player who saved it from expiring (0: nobody)
        public V2 Pos => new V2(X, Y);
    }

    /// The room behind a mystery door (host only, never sent; owner Nov 17): a small or medium square or rectangle, or a long hallway.
    public enum MysteryRoom : byte { Small, Medium, Hallway }

    /// What is in it (host only, never sent): enemies (most square rooms), nothing (rarer), a shop, a Black Market or an Arms Dealer (small
    /// rooms only), or a chest at the far end (hallways).
    public enum MysteryKind : byte { Enemies, Empty, Shop, Vendor, Chest, Arms }

    /// The rules of mystery doors.
    public static class Mystery
    {
        public const int AmbushBase = 4, AmbushMax = 10;  // a medium room's enemies: this many, +1 every 5 waves, at most AmbushMax
        public const int SmallFewer = 2;                   // a small room holds this many fewer (at least 3)

        /// Room weights: small 35, medium 35, hallway 30.
        static readonly int[] RoomWeights = { 35, 35, 30 };
        /// What a small room holds: enemies 50, empty 17, shop 11, Black Market 11, Arms Dealer 11. A medium room: enemies 78, empty 22. A hallway: a chest.
        static readonly int[] SmallWeights = { 50, 17, 11, 11, 11 }, MediumWeights = { 78, 22 };
        static readonly MysteryKind[] SmallKinds = { MysteryKind.Enemies, MysteryKind.Empty, MysteryKind.Shop, MysteryKind.Vendor, MysteryKind.Arms };

        static int Pick(DetRandom rg, int[] w)
        {
            int total = 0;
            foreach (int x in w) total += x;
            int roll = rg.Int(total);
            for (int k = 0; k < w.Length; k++)
            {
                if (roll < w[k]) return k;
                roll -= w[k];
            }
            return 0;
        }

        /// The room and what is in it, seeded (the same on every machine). Without enemies (round a boss) a room that rolled them is empty.
        public static MysteryRoom RoomFor(int seed, int wave, int n) => (MysteryRoom)Pick(new DetRandom(seed * 7121 + wave * 389 + n * 53 + 3), RoomWeights);

        public static MysteryKind KindFor(int seed, int wave, int n, MysteryRoom room, bool enemies)
        {
            var rg = new DetRandom(seed * 5167 + wave * 211 + n * 89 + 7);
            MysteryKind k = room == MysteryRoom.Hallway ? MysteryKind.Chest : room == MysteryRoom.Small ? SmallKinds[Pick(rg, SmallWeights)] : (MysteryKind)Pick(rg, MediumWeights);
            return k == MysteryKind.Enemies && !enemies ? MysteryKind.Empty : k;
        }

        /// The shapes a room comes in (one picked by the roll): small 4 x 4 or 5 x 4, medium 6 x 6 or 7 x 6, the hallway 12 x 3.
        /// The grid pieces a room comes as in a Grid run: small one cell (a shop, Black Market or Arms Dealer always one cell), medium a domino or a corner, the hallway a line of 3 or 4.
        public static GridPiece[] PiecesOf(MysteryRoom r) =>
            r == MysteryRoom.Small ? new[] { GridPiece.Mono }
            : r == MysteryRoom.Medium ? new[] { GridPiece.Domino, GridPiece.TriL }
            : new[] { GridPiece.TriI, GridPiece.TetI };

        public static SurpriseShape[] ShapesOf(MysteryRoom r) =>
            r == MysteryRoom.Small ? new[] { SurpriseShape.Tiny, SurpriseShape.SmallRect }
            : r == MysteryRoom.Medium ? new[] { SurpriseShape.Medium, SurpriseShape.Box }
            : new[] { SurpriseShape.LongHall };

        public static MysteryRoom RoomOfShape(SurpriseShape s) => s == SurpriseShape.LongHall ? MysteryRoom.Hallway : s == SurpriseShape.Tiny || s == SurpriseShape.SmallRect ? MysteryRoom.Small : MysteryRoom.Medium;

        /// How many enemies a room of this size holds at this wave.
        public static int AmbushCount(int wave, MysteryRoom r = MysteryRoom.Medium)
        {
            int n = Math.Min(AmbushMax, AmbushBase + wave / 5);
            return r == MysteryRoom.Small ? Math.Max(3, n - SmallFewer) : n;
        }

        /// A hallway's door must be on one of its short ends (so its chest is at the far end); a single box with no other room touching.
        public static bool HallFits(SurpriseOption o, MysteryDoor d)
        {
            if (o.Rooms.Count != 1 || o.Loops > 0)
                return false;
            var r = o.Rooms[0];
            return (r.W > r.H) == d.Vertical;  // (a long box lying left-right opens through an up-down wall at one end)
        }

        /// Where a hallway's chest goes: the middle of its far end, 70 px in.
        public static V2 FarEnd(ExpansionRoom r, V2 door)
        {
            V2 mid = new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f);
            if (r.W > r.H)
                return new V2(door.X < mid.X ? r.X1 - 70f : r.X + 70f, mid.Y);
            return new V2(mid.X, door.Y < mid.Y ? r.Y1 - 70f : r.Y + 70f);
        }

        public const int BotReserve = 10;      // a bot keeps this many vectors back for building when it opens a door
        public const float PriceShare = 0.6f;  // a door costs this share of drawing the same room (you don't pick its shape or spot)
        public const int Cap = 3;              // doors on the map at once
        public const float UseRange = 150f;    // stand this close to open or save one
        public const float Behind = 30f;       // the planned room has floor this far behind the door's wall

        /// What opening a room that would cost `drawPrice` to draw costs.
        public static int PriceFor(int drawPrice) => Math.Max(1, (int)Math.Round(PriceShare * drawPrice));

        /// How many new doors a cleared wave rolls: 1 or 2, seeded (the same on every machine).
        public static int NewDoors(int seed, int wave) => 1 + new DetRandom(seed * 4421 + wave * 613 + 29).Int(2);
    }

    public partial class Game
    {
        /// The sealed doors on the map (synced in Msg.ExpandState).
        public readonly List<MysteryDoor> MysteryDoors = new List<MysteryDoor>();

        /// Every game, for the look: doors that just opened and when (AnimT), for the reveal (Game/MysteryDoorLook.cs).
        public readonly List<KeyValuePair<MysteryDoor, float>> MysteryOpened = new List<KeyValuePair<MysteryDoor, float>>();
        public const float RevealTime = 1.2f;

        /// Doors that were in `before` and are gone now, with a doorway where they stood, just opened (an expired one leaves only wall).
        void NoteOpenedDoors(List<MysteryDoor> before)
        {
            MysteryOpened.RemoveAll(kv => AnimT - kv.Value > RevealTime);
            if (before.Count == 0 || !ExpandingOn)
                return;
            var w = ExpansionWorld;
            foreach (var d in before)
            {
                if (MysteryDoors.Exists(o => o.X == d.X && o.Y == d.Y))
                    continue;
                foreach (var wd in w.Doors)
                    if (wd.Pos.CloserThan(d.Pos, 2f))
                    {
                        MysteryOpened.Add(new KeyValuePair<MysteryDoor, float>(d, AnimT));
                        break;
                    }
            }
        }

        /// Host only: the room behind each door (same index as MysteryDoors) and the point just behind its wall.
        readonly List<SurpriseOption> mysteryPlans = new List<SurpriseOption>();
        readonly List<V2> mysteryBehind = new List<V2>();
        readonly List<MysteryKind> mysteryKinds = new List<MysteryKind>();
        readonly List<V2> ambushPending = new List<V2>();  // enemy rooms opened between waves: their enemies come as the wave starts
        readonly List<MysteryRoom> ambushSizes = new List<MysteryRoom>();
        int mysteryMapCount = -1;  // Expanded.Count when the plans were last checked against the map

        void ResetMysteryDoors()
        {
            MysteryDoors.Clear();
            mysteryPlans.Clear();
            mysteryBehind.Clear();
            mysteryKinds.Clear();
            ambushPending.Clear();
            ambushSizes.Clear();
            MysteryOpened.Clear();
            mysteryMapCount = -1;
        }

        void RemoveMysteryDoor(int i)
        {
            MysteryDoors.RemoveAt(i);
            if (i < mysteryPlans.Count) mysteryPlans.RemoveAt(i);
            if (i < mysteryBehind.Count) mysteryBehind.RemoveAt(i);
            if (i < mysteryKinds.Count) mysteryKinds.RemoveAt(i);
        }

        /// The door of a planned room: where its walls meet the map (the first plain door the new rooms share with the old ones), and
        /// the point just behind it, inside the new room. False if the plan has no such door.
        bool MysteryDoorOf(SurpriseOption plan, out MysteryDoor door, out V2 behind)
        {
            door = default(MysteryDoor);
            behind = default(V2);
            var all = new List<ExpansionRoom>(Expanded);
            all.AddRange(plan.Rooms);
            var w = Expansion.Build(all, Openings, Links, Nodes);
            int oldAreas = Expanded.Count + 1;  // (area 0 is the hub, area i is Expanded[i - 1])
            foreach (var d in w.Doors)
            {
                bool aOld = d.A < oldAreas, bOld = d.B < oldAreas;
                if (aOld == bOld || d.Width > 0f)
                    continue;
                int fresh = aOld ? d.B : d.A;
                var a = w.Areas[fresh];
                V2 inside = a.Contains(d.Pos + d.Across * Mystery.Behind) ? d.Pos + d.Across * Mystery.Behind : d.Pos - d.Across * Mystery.Behind;
                door = new MysteryDoor { X = (short)Math.Round(d.Pos.X), Y = (short)Math.Round(d.Pos.Y), Vertical = d.Vertical, Price = (short)Mystery.PriceFor(plan.Price) };
                behind = inside;
                return true;
            }
            return false;
        }

        /// Would this plan sit on (or crowd) another door's planned room, or put its door next to another one?
        bool MysteryClashes(SurpriseOption plan, MysteryDoor door, int skip)
        {
            for (int i = 0; i < MysteryDoors.Count; i++)
            {
                if (i == skip)
                    continue;
                if (MysteryDoors[i].Pos.CloserThan(door.Pos, 400f))
                    return true;
                if (i < mysteryPlans.Count)
                    foreach (var q in plan.Rooms)
                        foreach (var e in mysteryPlans[i].Rooms)
                            if (q.X < e.X1 + Expansion.Step && e.X < q.X1 + Expansion.Step && q.Y < e.Y1 + Expansion.Step && e.Y < q.Y1 + Expansion.Step)
                                return true;
            }
            return TouchesPlannedEventRoom(plan.Rooms);
        }

        /// Host, when a wave is cleared: the doors nobody saved expire, then 1 or 2 new ones are rolled (up to Mystery.Cap). Each is a
        /// surprise shape set against the map's outer wall (Expansion.SurpriseOptions, seeded), kept apart from the other doors.
        void RollMysteryDoors()
        {
            if (!IsHost || !ExpandingOn || !ModeRules.RunEvents)
                return;
            int expired = 0;
            for (int i = MysteryDoors.Count - 1; i >= 0; i--)
                if (MysteryDoors[i].Saver == 0 || !Players.ContainsKey(MysteryDoors[i].Saver))
                {
                    RemoveMysteryDoor(i);
                    expired++;
                }
            if (expired > 0)
                DoLog("mystery doors: " + expired + " expired");
            if ((Wave >= LastWave && !Endless) || Expansion.BoughtCount(Expanded) > Expansion.MaxRooms - 2)
            {
                BroadcastExpansion();
                return;
            }
            int want = Math.Min(Mystery.NewDoors(expandSeed, Wave), Mystery.Cap - MysteryDoors.Count);
            var rg = new DetRandom(expandSeed * 3301 + Wave * 977 + Expanded.Count);
            bool enemies = !BossNow && !BossNext;  // (round a boss the rooms come empty)
            for (int n = 0; n < want; n++)
            {
                var room = Mystery.RoomFor(expandSeed, Wave, n + Expanded.Count * 7);
                var kind = Mystery.KindFor(expandSeed, Wave, n + Expanded.Count * 7, room, enemies);
                var shapes = Mystery.ShapesOf(room);
                var pieces = Mystery.PiecesOf(room);
                bool grid = ExpandingOn;  // (a Grid run: the room behind is a piece on the lattice, a shop or vendor one cell; owner Nov 18)
                int nShapes = grid ? pieces.Length : shapes.Length;
                int first = rg.Int(nShapes);
                bool placed = false;
                for (int t = 0; t < nShapes && !placed; t++)
                {
                    var opts = grid ? Expansion.GridMysteryOptions(Expanded, pieces[(first + t) % pieces.Length]) : Expansion.SurpriseOptions(Expanded, shapes[(first + t) % shapes.Length]);
                    int top = Math.Min(opts.Count, 8);  // (the best spots: closing loops, not stretching the map)
                    int start = rg.Int(Math.Max(1, top));
                    for (int k = 0; k < opts.Count && !placed; k++)
                    {
                        var o = opts[(start + k) % opts.Count];
                        MysteryDoor d; V2 behind;
                        if (!MysteryDoorOf(o, out d, out behind) || MysteryClashes(o, d, -1) || (!grid && room == MysteryRoom.Hallway && !Mystery.HallFits(o, d)))
                            continue;
                        MysteryDoors.Add(d);
                        mysteryPlans.Add(o);
                        mysteryBehind.Add(behind);
                        mysteryKinds.Add(kind);
                        placed = true;
                        DoLog("mystery door at " + d.X + "," + d.Y + ": " + Expansion.SurpriseName(o.Shape) + ", " + kind + ", for " + d.Price + " vectors");
                    }
                }
            }
            mysteryMapCount = Expanded.Count;
            BroadcastExpansion();
        }

        /// Host, every tick: when the map changed (a room drawn, a gift), each door's room is checked again. One that no longer fits is
        /// re-planned behind the same spot (any shape that fits there); a door with no room left behind it goes.
        void TickMysteryDoors()
        {
            if (!IsHost || MysteryDoors.Count == 0 || mysteryMapCount == Expanded.Count)
                return;
            mysteryMapCount = Expanded.Count;
            bool changed = false;
            for (int i = MysteryDoors.Count - 1; i >= 0; i--)
            {
                SurpriseOption? plan = StillFits(i);
                if (!plan.HasValue)
                {
                    DoLog("mystery door at " + MysteryDoors[i].X + "," + MysteryDoors[i].Y + " is gone: no room fits behind it now");
                    RemoveMysteryDoor(i);
                    changed = true;
                    continue;
                }
                MysteryDoor d; V2 behind;
                if (!MysteryDoorOf(plan.Value, out d, out behind))
                {
                    RemoveMysteryDoor(i);
                    changed = true;
                    continue;
                }
                d.Saver = MysteryDoors[i].Saver;
                changed |= d.X != MysteryDoors[i].X || d.Y != MysteryDoors[i].Y || d.Price != MysteryDoors[i].Price;
                MysteryDoors[i] = d;
                mysteryPlans[i] = plan.Value;
                mysteryBehind[i] = behind;
            }
            if (changed)
                BroadcastExpansion();
        }

        /// The room behind door i on today's map: its plan if that is still legal, else the best that fits through the same spot.
        SurpriseOption? StillFits(int i)
        {
            var plan = mysteryPlans[i];
            if (ExpandingOn)  // a Grid run's piece behind the door: kept while nothing sits on it (no re-plan: another spot would leave the lattice look)
            {
                foreach (var q in plan.Rooms)
                    foreach (var r in Expanded)
                        if (q.X < r.X1 && r.X < q.X1 && q.Y < r.Y1 && r.Y < q.Y1)
                            return null;
                return plan;
            }
            var opts = Expansion.SurpriseOptions(Expanded, plan.Shape, mysteryBehind[i]);
            foreach (var o in opts)
                if (SameRooms(o.Rooms, plan.Rooms))
                    return o;
            foreach (var o in opts)  // (else the same shape through the same door; a hallway still opens at one end)
            {
                MysteryDoor d; V2 behind;
                if (plan.Shape != SurpriseShape.LongHall || (MysteryDoorOf(o, out d, out behind) && Mystery.HallFits(o, d)))
                    return o;
            }
            return null;
        }

        static bool SameRooms(List<ExpansionRoom> a, List<ExpansionRoom> b)
        {
            if (a.Count != b.Count)
                return false;
            for (int i = 0; i < a.Count; i++)
                if (a[i].X != b[i].X || a[i].Y != b[i].Y || a[i].W != b[i].W || a[i].H != b[i].H)
                    return false;
            return true;
        }

        /// The door a player at p can work (closest within Mystery.UseRange), or -1.
        public int MysteryNear(V2 p)
        {
            int best = -1;
            float bestD = Mystery.UseRange;
            for (int i = 0; i < MysteryDoors.Count; i++)
            {
                float d = MysteryDoors[i].Pos.DistanceTo(p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// The door this player has saved, or -1.
        public int MysterySavedBy(int id) => MysteryDoors.FindIndex(d => d.Saver == id);

        /// This game's player opens (G) or saves (X) the door they stand by. The host decides.
        public void UseMysteryDoor(bool save)
        {
            if (Me == null || Me.Downed)
                return;
            int i = MysteryNear(Me.Pos);
            if (i < 0)
                return;
            if (IsHost)
            {
                HostMysteryDoor(MyId, MysteryDoors[i].X, MysteryDoors[i].Y, save);
                return;
            }
            var w = Proto.Begin(Msg.MysteryDoor);
            w.Write(MysteryDoors[i].X); w.Write(MysteryDoors[i].Y); w.Write(save);
            session.SendToHost(Proto.End(w));
        }

        public void OnMysteryDoor(int peer, BinaryReader r) => HostMysteryDoor(peer, r.ReadInt16(), r.ReadInt16(), r.ReadBoolean());

        /// Host: a player at the door (x, y) saves it (one door per player; saving another moves the save, saving your own again lifts it)
        /// or pays to open it.
        void HostMysteryDoor(int id, short x, short y, bool save)
        {
            Player p;
            if (!IsHost || !Players.TryGetValue(id, out p) || p.Downed)
                return;
            int i = MysteryDoors.FindIndex(d => d.X == x && d.Y == y);
            if (i < 0 || p.Pos.FartherThan(MysteryDoors[i].Pos, Mystery.UseRange + 40f))
                return;
            var door = MysteryDoors[i];
            if (save)
            {
                if (door.Saver != 0 && door.Saver != id && Players.ContainsKey(door.Saver))
                    return;  // (someone else's save)
                bool lift = door.Saver == id;
                for (int k = 0; k < MysteryDoors.Count; k++)
                    if (MysteryDoors[k].Saver == id)
                    {
                        var o = MysteryDoors[k]; o.Saver = 0; MysteryDoors[k] = o;
                    }
                if (!lift)
                {
                    door.Saver = id;
                    MysteryDoors[i] = door;
                }
                DoLog(p.Name + (lift ? " lets a mystery door go" : " saves a mystery door"));
                Sounds.Add(Sfx.Pick);
                BroadcastExpansion();
                return;
            }
            OpenMysteryDoor(p, i);
        }

        /// Host: the door opens if the player can pay. Mid-wave only once the rooms are up (the first room raises them between waves).
        bool OpenMysteryDoor(Player p, int i)
        {
            if (State == MatchState.Fighting && !RoomsUp)
                return false;
            var plan = StillFits(i);
            if (!plan.HasValue)
            {
                RemoveMysteryDoor(i);
                BroadcastExpansion();
                return false;
            }
            int price = Mystery.PriceFor(plan.Value.Price);
            if (price > SpendableVectors(p))
            {
                DoLog("mystery door refused: needs " + price + " vectors, " + p.Name + " has " + p.Vectors);
                return false;
            }
            PayVectors(p, price);
            MysteryDoorsOpened++;
            MysteryVectorsSpent += price;
            var kind = i < mysteryKinds.Count ? mysteryKinds[i] : MysteryKind.Empty;
            var size = Mystery.RoomOfShape(plan.Value.Shape);
            var rooms = new List<ExpansionRoom>(plan.Value.Rooms.Count);
            for (int k = 0; k < plan.Value.Rooms.Count; k++)
            {
                var q = plan.Value.Rooms[k];
                q.Surprise = 0;  // (a bought room, not a gift)
                q.Price = k == 0 ? price : 0;
                q.Owner = p.Id;
                if (kind == MysteryKind.Shop) q.Kind = Arena.RoomKind.Shop;
                else if (kind == MysteryKind.Vendor) q.Kind = Arena.RoomKind.Vendor;
                else if (kind == MysteryKind.Arms) q.Kind = Arena.RoomKind.Arms;
                rooms.Add(q);
            }
            var doorsBefore = new List<MysteryDoor>(MysteryDoors);
            V2 doorAt = MysteryDoors[i].Pos;
            RemoveMysteryDoor(i);
            var head = rooms[0];
            V2 mid = new V2((head.X + head.X1) / 2f, (head.Y + head.Y1) / 2f);
            if (kind == MysteryKind.Chest)
            {
                V2 end = Mystery.FarEnd(head, doorAt);
                Nodes.Add(new MapNode { X = (short)end.X, Y = (short)end.Y, Kind = NodeKind.Chest });  // (TickNodes puts the chest there)
            }
            Expanded.AddRange(rooms);
            if (State == MatchState.Fighting)
                PinBreaches(rooms);  // (the wave's other breaches stay put)
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            TickNodes();
            mysteryMapCount = -1;  // (the other doors are checked against the new map)
            NoteOpenedDoors(doorsBefore);  // the reveal (MysteryDoorLook.cs)
            Sounds.Add(Sfx.Pick);
            DoLog(p.Name + " opens a mystery door: " + kind + " (" + Expansion.SurpriseName(plan.Value.Shape) + ") for " + price + " vectors");
            if (kind == MysteryKind.Enemies)
            {
                if (State == MatchState.Fighting)
                    SpringAmbush(mid, size);
                else
                {
                    ambushPending.Add(mid);  // (between waves: they wait in the dark and come out as the wave starts)
                    ambushSizes.Add(size);
                }
            }
            BroadcastExpansion();
            return true;
        }

        /// Host: an ambush room's enemies appear in it (the wave's own types) and swarm out at the players.
        void SpringAmbush(V2 at, MysteryRoom size)
        {
            var pool = WavePool(Math.Max(1, Wave));
            int n = Mystery.AmbushCount(Wave, size);
            float ring = size == MysteryRoom.Small ? 40f : 60f;
            for (int k = 0; k < n; k++)
            {
                var type = pool[rng.Next(pool.Count)];
                V2 pos = at + V2.FromAngle(MathX.Tau * k / n) * (ring + 30f * (k % 2));
                SpawnEnemyAt(type, pos, 0.7f, 0);
            }
            BroadcastFx(FxKind.Ring, at, new Rgba(1f, 0.3f, 0.25f), 220f, 0.6f, Sfx.Pick);
            DoLog("ambush: " + n + " enemies swarm out of a mystery room");
        }

        /// Host, as a wave starts: the ambushes opened between waves spring.
        void SpringPendingAmbushes()
        {
            if (!IsHost || ambushPending.Count == 0)
                return;
            for (int k = 0; k < ambushPending.Count; k++)
                SpringAmbush(ambushPending[k], k < ambushSizes.Count ? ambushSizes[k] : MysteryRoom.Medium);
            ambushPending.Clear();
            ambushSizes.Clear();
        }

        void WriteMysteryDoors(BinaryWriter w)
        {
            w.Write((byte)MysteryDoors.Count);
            foreach (var d in MysteryDoors)
            {
                w.Write(d.X); w.Write(d.Y); w.Write(d.Vertical); w.Write(d.Price); w.Write(d.Saver);
            }
        }

        void ReadMysteryDoors(BinaryReader r)
        {
            var before = new List<MysteryDoor>(MysteryDoors);
            MysteryDoors.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
                MysteryDoors.Add(new MysteryDoor { X = r.ReadInt16(), Y = r.ReadInt16(), Vertical = r.ReadBoolean(), Price = r.ReadInt16(), Saver = r.ReadInt32() });
            NoteOpenedDoors(before);
        }

        /// Host: doors opened this run (the balance numbers, Docs/MysteryDoors.md step 4; not synced).
        public int MysteryDoorsOpened, MysteryVectorsSpent;

        /// Bots, between waves (each game runs its own bot): walk to the nearest door it can pay for while keeping Mystery.BotReserve
        /// vectors back, then open it. Not while a level-up pick waits, the boss gate is up, or the bot is down.
        void BotMysteryDoor(Player me, ref V2 move)
        {
            if (!ExpandingOn || me.Downed || me.Offer != null || GateUp || State != MatchState.Intermission || MysteryDoors.Count == 0)
                return;
            int best = -1;
            float bestD = float.MaxValue;
            int purse = SpendableVectors(me);
            for (int i = 0; i < MysteryDoors.Count; i++)
            {
                var d = MysteryDoors[i];
                if (purse - d.Price < Mystery.BotReserve || (d.Saver != 0 && d.Saver != me.Id))
                    continue;
                float dist = d.Pos.DistanceTo(me.Pos);
                if (dist < bestD) { bestD = dist; best = i; }
            }
            if (best < 0)
                return;
            var door = MysteryDoors[best];
            V2 across = door.Vertical ? new V2(1f, 0f) : new V2(0f, 1f);
            var w = CurrentWorld;
            int ar = w.AreaAt(door.Pos + across * 70f);
            V2 goal = ar >= 0 && w.Areas[ar].Contains(door.Pos + across * 70f) ? door.Pos + across * 70f : door.Pos - across * 70f;  // (just inside the room the door is on)
            if (MysteryNear(me.Pos) == best && me.Pos.CloserThan(goal, 60f))
            {
                move = V2.Zero;
                if (MathX.Fmod(botT, 0.5f) < tickDt + 0.001f)
                    UseMysteryDoor(false);
                return;
            }
            V2 straight = (goal - me.Pos).LimitLength(1f);
            move = RoomsOn && straight.Length > 0.01f ? SteerDir(-2000 - me.Id, me.Pos, botT, me.Def.BodyR + 4f, goal, straight.Normalized()) : straight;
        }
    }
}

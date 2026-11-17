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

    /// What a mystery door hides (host only, never sent: Docs/MysteryDoors.md step 2).
    public enum MysteryKind : byte { Combat, Stash, Chest, Shop, Vendor, MiniBoss, Ambush }

    /// The rules of mystery doors.
    public static class Mystery
    {
        public const int RiskFrom = 5;         // no mini-boss or ambush before this wave
        public const int AmbushBase = 4, AmbushMax = 10;  // an ambush is this many enemies, +1 every 5 waves, at most AmbushMax

        /// First-guess weights: combat 50, loot 20 (Stash 10, chest 10), shop / vendor 15 (8, 7), risk 15 (mini-boss 7, ambush 8).
        static readonly int[] Weights = { 50, 10, 10, 8, 7, 7, 8 };

        /// The kind behind a door, seeded (the same on every machine). Without risk (early waves, around a boss) the risk weights are left out.
        public static MysteryKind KindFor(int seed, int wave, int n, bool risk)
        {
            int total = 0;
            for (int k = 0; k < Weights.Length; k++)
                total += !risk && IsRisk((MysteryKind)k) ? 0 : Weights[k];
            int roll = new DetRandom(seed * 7121 + wave * 389 + n * 53 + 3).Int(total);
            for (int k = 0; k < Weights.Length; k++)
            {
                int wgt = !risk && IsRisk((MysteryKind)k) ? 0 : Weights[k];
                if (roll < wgt)
                    return (MysteryKind)k;
                roll -= wgt;
            }
            return MysteryKind.Combat;
        }

        public static bool IsRisk(MysteryKind k) => k == MysteryKind.MiniBoss || k == MysteryKind.Ambush;

        /// The room shape a kind is built in (combat and ambush: one of the five surprise shapes, picked by the roll).
        public static SurpriseShape ShapeOf(MysteryKind k, SurpriseShape combat)
        {
            switch (k)
            {
                case MysteryKind.Stash: return SurpriseShape.Stash;
                case MysteryKind.Chest:
                case MysteryKind.Shop:
                case MysteryKind.Vendor: return SurpriseShape.Tiny;
                case MysteryKind.MiniBoss: return SurpriseShape.Duel;
                default: return combat;
            }
        }

        /// How many enemies an ambush room holds at this wave.
        public static int AmbushCount(int wave) => Math.Min(AmbushMax, AmbushBase + wave / 5);

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

        /// Host only: the room behind each door (same index as MysteryDoors) and the point just behind its wall.
        readonly List<SurpriseOption> mysteryPlans = new List<SurpriseOption>();
        readonly List<V2> mysteryBehind = new List<V2>();
        readonly List<MysteryKind> mysteryKinds = new List<MysteryKind>();
        readonly List<V2> ambushPending = new List<V2>();  // ambush rooms opened between waves: their enemies come as the wave starts
        int mysteryMapCount = -1;  // Expanded.Count when the plans were last checked against the map

        void ResetMysteryDoors()
        {
            MysteryDoors.Clear();
            mysteryPlans.Clear();
            mysteryBehind.Clear();
            mysteryKinds.Clear();
            ambushPending.Clear();
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
            int shapes = (int)SurpriseShape.Stash;  // (the five room shapes)
            bool risk = Wave >= Mystery.RiskFrom && !BossNow && !BossNext && FactionMinis.Is(MiniBossNow());
            for (int n = 0; n < want; n++)
            {
                var kind = Mystery.KindFor(expandSeed, Wave, n + Expanded.Count * 7, risk);
                int first = rg.Int(shapes);
                bool placed = false;
                bool combat = kind == MysteryKind.Combat || kind == MysteryKind.Ambush;
                for (int t = 0; t < (combat ? shapes : 1) && !placed; t++)
                {
                    var opts = Expansion.SurpriseOptions(Expanded, Mystery.ShapeOf(kind, (SurpriseShape)((first + t) % shapes)));
                    int top = Math.Min(opts.Count, 8);  // (the best spots: closing loops, not stretching the map)
                    int start = rg.Int(Math.Max(1, top));
                    for (int k = 0; k < opts.Count && !placed; k++)
                    {
                        var o = opts[(start + k) % opts.Count];
                        MysteryDoor d; V2 behind;
                        if (!MysteryDoorOf(o, out d, out behind) || MysteryClashes(o, d, -1))
                            continue;
                        MysteryDoors.Add(d);
                        mysteryPlans.Add(o);
                        mysteryBehind.Add(behind);
                        mysteryKinds.Add(kind);
                        placed = true;
                        DoLog("mystery door at " + d.X + "," + d.Y + ": " + kind + " (" + Expansion.SurpriseName(o.Shape) + ") for " + d.Price + " vectors");
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
            var opts = Expansion.SurpriseOptions(Expanded, plan.Shape, mysteryBehind[i]);
            foreach (var o in opts)
                if (SameRooms(o.Rooms, plan.Rooms))
                    return o;
            if (plan.Shape >= SurpriseShape.Stash)  // (a loot, shop or mini-boss room keeps its shape: any spot through the door, else nothing)
                return opts.Count > 0 ? opts[0] : (SurpriseOption?)null;
            string why;
            return Expansion.Surprise(expandSeed, Wave, Expanded, plan.Shape, out why, mysteryBehind[i]);
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
            var kind = i < mysteryKinds.Count ? mysteryKinds[i] : MysteryKind.Combat;
            var rooms = new List<ExpansionRoom>(plan.Value.Rooms.Count);
            for (int k = 0; k < plan.Value.Rooms.Count; k++)
            {
                var q = plan.Value.Rooms[k];
                q.Surprise = kind == MysteryKind.Stash ? (byte)(1 + (int)SurpriseShape.Stash) : (byte)0;  // (a bought room, not a gift; a Stash keeps its mark so its cache can be taken)
                q.Price = k == 0 ? price : 0;
                q.Owner = p.Id;
                if (kind == MysteryKind.Shop) q.Kind = Arena.RoomKind.Shop;
                else if (kind == MysteryKind.Vendor) q.Kind = Arena.RoomKind.Vendor;
                else if (kind == MysteryKind.MiniBoss) { q.Kind = Arena.RoomKind.Gatekeeper; q.Mini = (byte)MiniBossNow(); }
                rooms.Add(q);
            }
            RemoveMysteryDoor(i);
            var head = rooms[0];
            V2 mid = new V2((head.X + head.X1) / 2f, (head.Y + head.Y1) / 2f);
            if (kind == MysteryKind.Chest)
                Nodes.Add(new MapNode { X = (short)mid.X, Y = (short)mid.Y, Kind = NodeKind.Chest });  // (TickNodes puts the chest in it)
            Expanded.AddRange(rooms);
            if (State == MatchState.Fighting)
                PinBreaches(rooms);  // (the wave's other breaches stay put)
            expWorld = null;
            LayoutAge = 0f;
            RaiseRoomsNow();
            TickNodes();
            mysteryMapCount = -1;  // (the other doors are checked against the new map)
            Sounds.Add(Sfx.Pick);
            DoLog(p.Name + " opens a mystery door: " + kind + " (" + Expansion.SurpriseName(plan.Value.Shape) + ") for " + price + " vectors");
            if (kind == MysteryKind.Ambush)
            {
                if (State == MatchState.Fighting)
                    SpringAmbush(mid);
                else
                    ambushPending.Add(mid);  // (between waves: they wait in the dark and come out as the wave starts)
            }
            BroadcastExpansion();
            return true;
        }

        /// Host: an ambush room's enemies appear in it (the wave's own types) and swarm out at the players.
        void SpringAmbush(V2 at)
        {
            var pool = WavePool(Math.Max(1, Wave));
            int n = Mystery.AmbushCount(Wave);
            for (int k = 0; k < n; k++)
            {
                var type = pool[rng.Next(pool.Count)];
                V2 pos = at + V2.FromAngle(MathX.Tau * k / n) * (60f + 30f * (k % 2));
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
            foreach (var at in ambushPending)
                SpringAmbush(at);
            ambushPending.Clear();
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
            MysteryDoors.Clear();
            int n = r.ReadByte();
            for (int i = 0; i < n; i++)
                MysteryDoors.Add(new MysteryDoor { X = r.ReadInt16(), Y = r.ReadInt16(), Vertical = r.ReadBoolean(), Price = r.ReadInt16(), Saver = r.ReadInt32() });
        }
    }
}

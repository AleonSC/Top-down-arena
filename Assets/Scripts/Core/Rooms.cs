using System;
using System.Collections.Generic;

namespace Arena
{
    /// One part of the Rooms world: the hub (today's arena), a side room, a hallway or the vault. A box in world coordinates.
    public struct RoomArea
    {
        public string Name;
        public V2 Min, Max;
        public bool Hub, Hall, Vault;
        public V2 Mid => (Min + Max) * 0.5f;
        public V2 Size => Max - Min;
        public bool Contains(V2 p, float pad = 0f) => p.X >= Min.X - pad && p.X <= Max.X + pad && p.Y >= Min.Y - pad && p.Y <= Max.Y + pad;
    }

    /// A doorway between two areas: a gap in the wall, Rooms.DoorW long. Vertical: the wall runs up and down (x = Pos.X).
    public struct RoomDoor
    {
        public V2 Pos;
        public bool Vertical;
        public int A, B;  // the areas it joins (indexes into Rooms.Areas)
        public V2 Across => Vertical ? new V2(1f, 0f) : new V2(0f, 1f);  // through the door
        public V2 Along => Vertical ? new V2(0f, 1f) : new V2(1f, 0f);   // along the wall
        public bool Joins(int area) => A == area || B == area;
        public int Other(int area) => A == area ? B : A;
        /// The spot DoorPad in from the door on the given area's side.
        public V2 Side(int area) => Rooms.Areas[area].Contains(Pos + Across * Rooms.DoorPad) ? Pos + Across * Rooms.DoorPad : Pos - Across * Rooms.DoorPad;
    }

    /// The Rooms arena (Docs/Rooms.md, the Arena setting): on regular waves the arena grows past today's 1600x900 hub
    /// into side rooms joined by hallways. This is the first map, Wings: a big room left and right of the hub, each with
    /// a door into it, and a hallway above and below the hub joining the two rooms into a loop round it (a door from
    /// each room into each hallway). The hub's own edges are wall except the two room doors, so nothing wraps.
    /// One regular wave in ten also opens the vault, a small room off the top hallway with a mini-boss in it.
    /// The world is a box (-900..2500 by -260..1160, or from -660 with the vault), so Game.ArenaMin and ArenaMax simply
    /// grow (Paths.cs). The walls are ordinary layout solids: Rooms.With adds them to the boss cycle's floor plan, so
    /// walking, shots and the enemies' steering (LayoutRules.cs) work through the doors as is.
    public static class Rooms
    {
        public const float RoomW = 900f, HallH = 260f, DoorW = 160f, WallT = 24f, DoorPad = 60f;
        public const float VaultW = 500f, VaultH = 400f;
        const float W = Game.Width, H = Game.Height;

        public static V2 WorldMin => new V2(-RoomW, -HallH);
        public static V2 VaultWorldMin => new V2(-RoomW, -HallH - VaultH);
        public static V2 WorldMax => new V2(W + RoomW, H + HallH);
        public static V2 MinFor(bool vault) => vault ? VaultWorldMin : WorldMin;

        public const int Hub = 0, West = 1, East = 2, TopHall = 3, BottomHall = 4, Vault = 5;

        /// The rooms with a nest in them (Docs/Rooms.md: each room's nest keeps it stocked and spills into the hub).
        public static readonly int[] NestRooms = { West, East };

        public static readonly RoomArea[] Areas =
        {
            new RoomArea { Name = "Hub", Min = new V2(0f, 0f), Max = new V2(W, H), Hub = true },
            new RoomArea { Name = "West room", Min = new V2(-RoomW, 0f), Max = new V2(0f, H) },
            new RoomArea { Name = "East room", Min = new V2(W, 0f), Max = new V2(W + RoomW, H) },
            new RoomArea { Name = "Top hall", Min = new V2(-RoomW, -HallH), Max = new V2(W + RoomW, 0f), Hall = true },
            new RoomArea { Name = "Bottom hall", Min = new V2(-RoomW, H), Max = new V2(W + RoomW, H + HallH), Hall = true },
            new RoomArea { Name = "Vault", Min = new V2(W / 2f - VaultW / 2f, -HallH - VaultH), Max = new V2(W / 2f + VaultW / 2f, -HallH), Vault = true },
        };

        public static readonly RoomDoor[] Doors =
        {
            new RoomDoor { Pos = new V2(0f, H / 2f), Vertical = true, A = Hub, B = West },
            new RoomDoor { Pos = new V2(W, H / 2f), Vertical = true, A = Hub, B = East },
            new RoomDoor { Pos = new V2(-RoomW / 2f, 0f), A = West, B = TopHall },
            new RoomDoor { Pos = new V2(W + RoomW / 2f, 0f), A = East, B = TopHall },
            new RoomDoor { Pos = new V2(-RoomW / 2f, H), A = West, B = BottomHall },
            new RoomDoor { Pos = new V2(W + RoomW / 2f, H), A = East, B = BottomHall },
            new RoomDoor { Pos = new V2(W / 2f, 0f), A = Hub, B = TopHall },     // the hub's top and bottom doors (owner, Nov 10: less linear)
            new RoomDoor { Pos = new V2(W / 2f, H), A = Hub, B = BottomHall },
            new RoomDoor { Pos = new V2(W / 2f, -HallH), A = TopHall, B = Vault },  // the vault's: only on a vault wave (keep it last: VaultDoor)
        };

        /// The vault's door (the last one).
        public static int VaultDoor => Doors.Length - 1;

        /// Wraparound portals (owner, Nov 10): step into one and you come out of its partner on the far side of the map,
        /// which makes the map a ring. Players and enemies both use them (enemies steer through them when it's shorter).
        /// Pairs: the West room's far wall with the East room's, and each hallway's two ends.
        public struct Portal { public V2 A, B; public V2 InA, InB; }  // centres, and the way into the map from each
        public const float PortalR = 42f, PortalExit = 70f, PortalCooldown = 1.2f, PortalInset = 40f;
        public static readonly Portal[] Portals =
        {
            new Portal { A = new V2(-RoomW + PortalInset, H / 2f), B = new V2(W + RoomW - PortalInset, H / 2f), InA = new V2(1f, 0f), InB = new V2(-1f, 0f) },
            new Portal { A = new V2(-RoomW + PortalInset, -HallH / 2f), B = new V2(W + RoomW - PortalInset, -HallH / 2f), InA = new V2(1f, 0f), InB = new V2(-1f, 0f) },
            new Portal { A = new V2(-RoomW + PortalInset, H + HallH / 2f), B = new V2(W + RoomW - PortalInset, H + HallH / 2f), InA = new V2(1f, 0f), InB = new V2(-1f, 0f) },
        };

        /// Where something stepping into a portal at `at` comes out (null: not in a portal). Keeps its offset along the wall.
        public static V2? PortalExitFor(V2 at)
        {
            foreach (var p in Portals)
            {
                if (at.DistanceTo(p.A) < PortalR)
                    return p.B + p.InB * PortalExit + new V2(0f, at.Y - p.A.Y);
                if (at.DistanceTo(p.B) < PortalR)
                    return p.A + p.InA * PortalExit + new V2(0f, at.Y - p.B.Y);
            }
            return null;
        }

        /// Is this door there (the vault's only with the vault up)?
        public static bool DoorUp(int i, bool vault) => vault || !Doors[i].Joins(Vault);

        /// The area a point is in (the nearest one for a point in a wall or just outside the world).
        public static int AreaAt(V2 p)
        {
            for (int i = 0; i < Areas.Length; i++)
                if (Areas[i].Contains(p))
                    return i;
            int best = Hub;
            float bestD = float.MaxValue;
            for (int i = 0; i < Areas.Length; i++)
            {
                var a = Areas[i];
                V2 c = p.Clamp(a.Min, a.Max);
                float d = c.DistanceTo(p);
                if (d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        /// Where a room's nest stands: in the far half of the room from the hub, clear of its cover.
        public static V2 NestSpot(int room) => room == West ? new V2(-RoomW / 2f - 110f, H / 2f) : new V2(W + RoomW / 2f + 110f, H / 2f);

        /// Where the rooms' camera centres a view hx by hy half-size on someone at `at`: held inside the area they're in
        /// (centred on it when it's smaller than the view), then inside the world (CameraZoom.cs; the run log's samples).
        public static V2 ViewCentre(V2 at, float hx, float hy, bool vault)
        {
            if (!RoomByRoom)  // the open map: on you, held inside the world
            {
                V2 wlo = MinFor(vault), whi = WorldMax;
                return new V2(Math.Max(wlo.X + hx, Math.Min(whi.X - hx, at.X)), Math.Max(wlo.Y + hy, Math.Min(whi.Y - hy, at.Y)));
            }
            var a = Areas[AreaAt(at)];
            float x = a.Size.X >= 2f * hx ? Math.Max(a.Min.X + hx, Math.Min(a.Max.X - hx, at.X)) : a.Mid.X;
            float y = a.Size.Y >= 2f * hy ? Math.Max(a.Min.Y + hy, Math.Min(a.Max.Y - hy, at.Y)) : a.Mid.Y;
            V2 lo = MinFor(vault), hi = WorldMax;
            return new V2(Math.Max(lo.X + hx, Math.Min(hi.X - hx, x)), Math.Max(lo.Y + hy, Math.Min(hi.Y - hy, y)));
        }

        // --- The walls ---------------------------------------------------------------------------------------------

        static Solid Box(float x0, float y0, float x1, float y1) => new Solid { Pos = new V2((x0 + x1) / 2f, (y0 + y1) / 2f), HW = (x1 - x0) / 2f, HH = (y1 - y0) / 2f };
        static Solid Pillar(float x, float y, float r) => new Solid { Round = true, Pos = new V2(x, y), R = r };

        /// A wall along a line (horizontal at y from x0 to x1, or vertical at x from y0 to y1), cut at its doors.
        static void WallLine(List<Solid> to, bool vault, bool vertical, float at, float from, float till)
        {
            var gaps = new List<float>();
            for (int i = 0; i < Doors.Length; i++)
            {
                var d = Doors[i];
                if (DoorUp(i, vault) && d.Vertical == vertical && Math.Abs((vertical ? d.Pos.X : d.Pos.Y) - at) < 0.5f)
                {
                    float u = vertical ? d.Pos.Y : d.Pos.X;
                    if (u > from && u < till)
                        gaps.Add(u);
                }
            }
            gaps.Sort();
            float a = from, t = WallT / 2f;
            gaps.Add(float.NaN);
            foreach (var g in gaps)
            {
                float b = float.IsNaN(g) ? till : g - DoorW / 2f;
                if (b - a > 1f)
                    to.Add(vertical ? Box(at - t, a, at + t, b) : Box(a, at - t, b, at + t));
                if (!float.IsNaN(g))
                    a = g + DoorW / 2f;
            }
        }

        static readonly Solid[][] walls = new Solid[2][];
        static Solid[] cover;

        /// The walls between the areas and round the world (cut at the doors), with or without the vault.
        public static Solid[] WallsFor(bool vault)
        {
            int k = vault ? 1 : 0;
            if (walls[k] != null)
                return walls[k];
            var l = new List<Solid>();
            V2 lo = WorldMin, hi = WorldMax;
            WallLine(l, vault, true, 0f, 0f, H);                 // hub | west room
            WallLine(l, vault, true, W, 0f, H);                  // hub | east room
            WallLine(l, vault, false, 0f, lo.X, hi.X);           // top hall | west room, hub, east room
            WallLine(l, vault, false, H, lo.X, hi.X);            // bottom hall | west room, hub, east room
            WallLine(l, vault, true, lo.X, lo.Y, hi.Y);          // the world's own edges (the top one has the vault's door)
            WallLine(l, vault, true, hi.X, lo.Y, hi.Y);
            WallLine(l, vault, false, lo.Y, lo.X, hi.X);
            WallLine(l, vault, false, hi.Y, lo.X, hi.X);
            if (vault)
            {
                var v = Areas[Vault];
                WallLine(l, true, true, v.Min.X, v.Min.Y, v.Max.Y);
                WallLine(l, true, true, v.Max.X, v.Min.Y, v.Max.Y);
                WallLine(l, true, false, v.Min.Y, v.Min.X - WallT / 2f, v.Max.X + WallT / 2f);
            }
            return walls[k] = l.ToArray();
        }

        public static Solid[] Walls => WallsFor(false);

        /// The small obstacles in the rooms and hallways (Docs/Rooms.md decision 11).
        public static Solid[] Cover
        {
            get
            {
                if (cover != null)
                    return cover;
                float wx = -RoomW / 2f, ex = W + RoomW / 2f;
                return cover = new[]
                {
                    Pillar(wx, 250f, 40f), Pillar(wx, H - 250f, 40f), Box(-RoomW + 165f, H / 2f - 120f, -RoomW + 195f, H / 2f + 120f),  // west room
                    Pillar(ex, 250f, 40f), Pillar(ex, H - 250f, 40f), Box(W + RoomW - 195f, H / 2f - 120f, W + RoomW - 165f, H / 2f + 120f),  // east room
                    Box(170f, -HallH / 2f - 30f, 230f, -HallH / 2f + 30f), Box(W - 230f, -HallH / 2f - 30f, W - 170f, -HallH / 2f + 30f),  // top hall
                    Pillar(200f, H + HallH / 2f, 26f), Pillar(W - 200f, H + HallH / 2f, 26f),  // bottom hall
                };
            }
        }

        /// Either side of each door, for the steering graph: routes from room to room go through these.
        static IEnumerable<V2> DoorSpots(bool vault)
        {
            for (int i = 0; i < Doors.Length; i++)
                if (DoorUp(i, vault))
                {
                    var d = Doors[i];
                    yield return d.Pos + d.Across * DoorPad;
                    yield return d.Pos - d.Across * DoorPad;
                }
        }

        static readonly Dictionary<int, LayoutDef> withRooms = new Dictionary<int, LayoutDef>();

        /// The boss cycle's floor plan with the rooms round it: the plan's own walls, pillars and crates stay in the hub
        /// (decision 11), its edge wall and gates go (the rooms' walls and doors take their place).
        public static LayoutDef With(LayoutId hub, bool vault = false)
        {
            int key = (int)hub * 2 + (vault ? 1 : 0);
            LayoutDef d;
            if (withRooms.TryGetValue(key, out d))
                return d;
            var plan = Layouts.Get(hub);
            var solids = new List<Solid>();
            foreach (var s in plan.Solids)
                if (!s.Edge)
                    solids.Add(s);
            solids.AddRange(WallsFor(vault));
            solids.AddRange(Cover);
            d = new LayoutDef
            {
                Id = hub,
                Name = hub == LayoutId.Open ? "Wings" : plan.Name + " · Wings",
                Blurb = plan.Blurb,
                Edge = EdgeGates.Open,
                Solids = solids.ToArray(),
                Crates = plan.Crates,
                PlayerGates = new EdgeSpan[0],
                EnemyGates = new EdgeSpan[0],
                HasHold = plan.HasHold,
                HoldPos = plan.HoldPos,
                HoldR = plan.HoldR,
            };
            // Steering spots: the usual ones (18 px clear of every wall), minus any within 24 px of the rooms' own walls (the
            // stubs where two room walls meet leave corners the steering graph's body can't get away from); the hub's floor
            // plan keeps its own, so its narrow doors still have corners to route through.
            var wps = new List<V2>();
            foreach (var p in Layouts.MakeWaypoints(d, MinFor(vault), WorldMax, DoorSpots(vault)))
            {
                bool tight = false;
                foreach (var w in WallsFor(vault))
                    tight |= Layouts.Inside(w, p, 24f);
                if (!tight)
                    wps.Add(p);
            }
            // The portals: a steering spot in each, linked to its partner (LayoutRules.cs GraphFor), so enemies route through them.
            var links = new List<int>();
            foreach (var pt in Portals)
            {
                links.Add(wps.Count); wps.Add(pt.A);
                links.Add(wps.Count); wps.Add(pt.B);
            }
            d.Links = links.ToArray();
            d.Waypoints = wps.ToArray();
            withRooms[key] = d;
            return d;
        }

        // --- Pacing (Docs/Rooms.md, "How a wave plays") ----------------------------------------------------------------

        /// The room-by-room version (phases 2-3: nests, spills, room locks, overflow, a camera held to the room you're in).
        /// Off since Nov 10 (owner: "too cookie cutter"): the map is one open space, enemies come in at random spots and
        /// the camera follows you anywhere. Parked, not deleted: the code below still works with this on (the tests do).
        public static bool RoomByRoom = false;

        /// Open map: no enemy comes in closer than this to a player.
        public const float SpawnClear = 350f;

        /// Open map: room rounds start at this zoom, with a prompt to zoom out (CameraZoom.cs).
        public const float StartZoom = 1.5f;

        public const float NestHpMult = 3f;      // a room's nest: this many times a Nest's health at the wave
        public const float SpillWarn = 2f;       // the door flashes this long before a spill
        public const float SpillClear = 180f;    // a spill picks another door if someone stands this close to where it would come out
        public const float FirstSpill = 18f;     // the first spill of a wave comes after this (plus a few seconds per room)
        public const int VaultEvery = 10;        // one regular wave in each stretch of this many has the vault
        public const int FirstVault = 6;         // the first stretch's vault comes after the first boss (its mini-boss is a Data Gate duel's strength)

        /// Room enemies kept alive at once in each room while its nest lives: more later and with more players. (Phase 3:
        /// from 5 + wave/8 + 2 a player, max 12, so a room's worst moment stays near the 8-10 threats in view the design
        /// asks for; measured with run.sh 1 1 arenabal.)
        public static int RoomCap(int wave, int players) => Math.Min(10, 4 + wave / 7 + Math.Max(0, players - 1));

        /// Phase 3: everything that hurts players hits this much harder while the rooms are up (regular waves of the Rooms
        /// arena only: boss waves and the Classic arena are unchanged). Fewer threats at once, so each one counts.
        public static float EnemyDamageMult = 1.3f;  // (static, not const: the balance report tries others)

        /// Seconds between a room's spills: shorter later in the run.
        public static float SpillTime(int wave) => Math.Max(11f, 22f - wave * 0.25f);

        /// How long a room's enemy guards its nest before it overflows out of the room to hunt you: shorter later in the run.
        public static float GuardTime(int wave) => Math.Max(5f, 10f - wave * 0.15f);

        /// Runners in each spill.
        public static int SpillSize(int wave, int players) => 3 + wave / 15 + Math.Max(0, players - 1);

        /// A cleared room's XP for everyone.
        public static float ClearXp(int wave) => 30f + wave * 3f;
    }

    /// One room's state (Docs/Rooms.md): its nest, whether it's cleared, its spill timer and the door the next spill uses.
    public sealed class RoomState
    {
        public int Area;
        public int NestId;          // host: the nest enemy
        public bool NestAlive, Cleared, Seen;
        public float SpillT;        // seconds to the next spill (synced in tenths)
        public int NextDoor = -1;   // index into Rooms.Doors
    }

    public partial class Game
    {
        /// Host, at each wave's start: the Rooms world is up for this wave (synced in the snapshot with the layout).
        public bool RoomsUp;

        /// The vault is open on this wave (one regular wave in ten; synced).
        public bool VaultUp;
        public bool VaultCleared;   // its mini-boss is down (synced)
        int vaultStretch = -1, vaultWave = -1, vaultBossId;

        /// The rooms with nests (Wings: West and East).
        public readonly RoomState[] RoomStates = { new RoomState { Area = Rooms.West }, new RoomState { Area = Rooms.East } };

        /// The Rooms world is what you're playing in right now (not in a Data Gate's challenge, the Practice Range or
        /// the Showcase).
        public bool RoomsOn => RoomsUp && !InRoom && Practice == null && !ShowcaseMode;
        public bool VaultOn => RoomsOn && VaultUp;

        /// Enemy damage to players: Rooms.EnemyDamageMult while the rooms are up, else 1 (Combat.cs).
        public float RoomsDamageMult => RoomsOn ? Rooms.EnemyDamageMult : 1f;

        /// The run's Arena setting is Rooms (the lobby's Difficulty tab; Classic, today's arena, by default).
        public bool RoomsArena => D(DiffKnob.ArenaRooms) >= 0.5f;

        /// Host, at the start of each wave (with PickLayout): regular waves get the rooms, boss waves the hub alone
        /// (bosses are tuned for 1600x900). Not with Tiny Arena or on Narrow Pass, which wall the hub in themselves,
        /// and not in a Boss Hunt, the Practice Range or the Showcase.
        void PickRooms()
        {
            bool was = RoomsUp, vaultWas = VaultUp;
            RoomsUp = RoomsArena && Practice == null && !ShowcaseMode && Hunt == null && !IsBossWave(Wave)
                && !Has(RunMod.TinyArena) && !OnPathOf(PathId.NarrowPass);
            VaultUp = RoomsUp && Wave == VaultWaveFor(Wave);
            VaultCleared = false;
            if (RoomsUp != was || VaultUp != vaultWas)
            {
                LayoutAge = 0f;  // the walls rise (or the old ones go) with the change
                DoLog(RoomsUp ? "rooms: up (Wings" + (VaultUp ? ", the vault" : "") + ")" : "rooms: down (the hub alone)");
            }
            if (was && !RoomsUp)
                PullIntoHub();
            foreach (var rs in RoomStates)
            {
                rs.NestAlive = rs.Seen = false;
                rs.Cleared = true;
                rs.NestId = 0;
                rs.NextDoor = -1;
            }
        }

        /// The wave in this stretch of ten that has the vault (picked once per stretch, among its regular waves still to come).
        int VaultWaveFor(int wave)
        {
            int stretch = (wave - 1) / Rooms.VaultEvery;
            if (stretch != vaultStretch)
            {
                vaultStretch = stretch;
                var pool = new List<int>();
                int from = stretch == 0 ? Rooms.FirstVault : stretch * Rooms.VaultEvery + 2;  // (not before the first boss)
                for (int w = Math.Max(wave, from); w <= (stretch + 1) * Rooms.VaultEvery; w++)
                    if (!IsBossWave(w))
                        pool.Add(w);
                vaultWave = pool.Count > 0 ? pool[rng.Next(pool.Count)] : -1;
            }
            return vaultWave;
        }

        /// Host, once the wave is built: each room's nest goes down and its spill timer starts; the vault gets its mini-boss.
        void StartRoomsWave()
        {
            if (!RoomsOn)
                return;
            int i = 0;
            foreach (var rs in RoomStates)
            {
                if (!Rooms.RoomByRoom)
                    break;  // (the open map: no nests)
                var nest = SpawnEnemyAt(EnemyType.Nest, Rooms.NestSpot(rs.Area), 0.9f, 0);
                nest.Home = rs.Area;
                nest.MaxHp *= Rooms.NestHpMult;
                nest.Hp = nest.MaxHp;
                rs.NestId = nest.Id;
                rs.NestAlive = true;
                rs.Cleared = rs.Seen = false;
                rs.SpillT = Rooms.FirstSpill + 4f * i++;
                rs.NextDoor = PickSpillDoor(rs.Area);
            }
            if (VaultUp)
            {
                // The vault's mini-boss (Docs/Rooms.md decision 7): The Gatekeeper for now, moved here from the Data Gates.
                var v = Rooms.Areas[Rooms.Vault];
                var boss = SpawnEnemyAt(EnemyType.Gatekeeper, v.Mid - new V2(0f, 40f), 1.2f, 0);
                boss.Home = Rooms.Vault;
                boss.T2 = 2.5f;
                vaultBossId = boss.Id;
            }
        }

        /// A random door out of a room for its next spill: into the hub or a hallway, never into another room.
        int PickSpillDoor(int area, bool clearOfPlayers = false)
        {
            var ok = new List<int>();
            for (int i = 0; i < Rooms.Doors.Length; i++)
            {
                var d = Rooms.Doors[i];
                if (d.Joins(area) && !Rooms.Areas[d.Other(area)].Vault && (Rooms.Areas[d.Other(area)].Hub || Rooms.Areas[d.Other(area)].Hall)
                    && !(clearOfPlayers && PlayerNear(d.Side(d.Other(area)), Rooms.SpillClear)))
                    ok.Add(i);
            }
            if (ok.Count == 0 && clearOfPlayers)
                return PickSpillDoor(area);  // every door has someone at it: any will do
            return ok.Count > 0 ? ok[rng.Next(ok.Count)] : -1;
        }

        bool PlayerNear(V2 at, float r)
        {
            foreach (var p in Players.Values)
                if (!p.Downed && p.Pos.DistanceTo(at) < r)
                    return true;
            return false;
        }

        RoomState StateOf(int area)
        {
            foreach (var rs in RoomStates)
                if (rs.Area == area)
                    return rs;
            return null;
        }

        /// Host, every tick of a wave: nests, spills and cleared rooms.
        void TickRoomsWave(float dt)
        {
            if (!RoomsOn || State != MatchState.Fighting)
                return;
            int players = Players.Count;
            foreach (var rs in RoomStates)
            {
                if (rs.Cleared)
                    continue;
                var area = Rooms.Areas[rs.Area];
                if (!rs.Seen)
                    foreach (var p in Players.Values)
                        rs.Seen |= area.Contains(p.Pos, 200f);
                Enemy nest;
                rs.NestAlive = Enemies.TryGetValue(rs.NestId, out nest) && nest.Hp > 0f;
                if (rs.NestAlive)
                {
                    if (rs.SpillT > Rooms.SpillWarn + 0.5f && rs.SpillT - dt <= Rooms.SpillWarn + 0.5f && rs.NextDoor >= 0 && PlayerNear(Rooms.Doors[rs.NextDoor].Side(Rooms.Doors[rs.NextDoor].Other(rs.Area)), Rooms.SpillClear))
                        rs.NextDoor = PickSpillDoor(rs.Area, true);  // someone's standing where it would come out: another door (before its warning shows)
                    rs.SpillT -= dt;
                    if (rs.SpillT <= 0f && rs.NextDoor >= 0)
                        Spill(rs, players);
                    continue;
                }
                bool left = false;
                foreach (var e in Enemies.Values)
                    if (e.Home == rs.Area && !e.Spilled && e.Hp > 0f)
                    {
                        left = true;
                        break;
                    }
                if (!left)
                    ClearRoom(rs);
            }
            if (VaultUp && !VaultCleared)
            {
                Enemy boss;
                if (!Enemies.TryGetValue(vaultBossId, out boss) || boss.Hp <= 0f)
                    WinVault();
            }
        }

        /// A room sends a squad of fast runners out through its chosen door (Docs/Rooms.md decision 10).
        void Spill(RoomState rs, int players)
        {
            var door = Rooms.Doors[rs.NextDoor];
            V2 at = door.Side(door.Other(rs.Area));  // just outside, in the hub or the hallway
            int n = Rooms.SpillSize(Wave, players);
            for (int i = 0; i < n; i++)
            {
                var e = SpawnEnemyAt(EnemyType.Runner, at + door.Along * ((i - (n - 1) / 2f) * 26f), 0.3f, 0);
                e.Home = rs.Area;
                e.Spilled = true;
            }
            BroadcastFx(FxKind.Ring, door.Pos, new Rgba(1f, 0.7f, 0.28f), 120f, 0.5f, Sfx.None);
            DoLog("spill: " + Rooms.Areas[rs.Area].Name + " -> " + Rooms.Areas[door.Other(rs.Area)].Name + " (" + n + ")");
            rs.SpillT = Rooms.SpillTime(Wave);
            rs.NextDoor = PickSpillDoor(rs.Area);
        }

        /// A room's nest and everything it kept are down: its doors open for good, and a small reward (decision 4).
        void ClearRoom(RoomState rs)
        {
            rs.Cleared = true;
            rs.NestAlive = false;
            V2 at = Rooms.NestSpot(rs.Area);
            SpawnOrb(at + new V2(-20f, 0f), true);
            SpawnOrb(at + new V2(20f, 0f), true);
            GrantXp(Rooms.ClearXp(Wave));
            BroadcastFx(FxKind.Ring, at, new Rgba(0.25f, 0.9f, 0.7f), 300f, 0.7f, Sfx.LevelUp);
            DoLog("room cleared: " + Rooms.Areas[rs.Area].Name);
        }

        /// The vault's mini-boss is down: everyone gets a prize pick (rare or better, sometimes a legendary), as the
        /// Gatekeeper's Data Gate gave (Challenges.cs ClaimPrize).
        void WinVault()
        {
            VaultCleared = true;
            int next = (Wave / 5 + 1) * 5;
            var bossType = BossForWave(Math.Min(MaxWave, Math.Max(5, next)));
            var key = IsReaper(bossType) ? EnemyType.TwinA : bossType;
            foreach (var p in Players.Values)
            {
                p.PrizePicks++;
                if (rng.NextDouble() < Challenges.PrizeLegendaryChance)
                    p.LegendaryQueue.Add(key);
            }
            BroadcastFx(FxKind.Ring, Rooms.Areas[Rooms.Vault].Mid, new Rgba(1f, 0.85f, 0.4f), 400f, 0.8f, Sfx.WaveClear);
            DoLog("vault cleared");
        }

        /// The wave's queue can spawn now: a room with a living nest has room for more. With every nest down the rest
        /// of the queue is called off (kill the nests fast and fewer come).
        bool RoomsSpawnReady()
        {
            bool anyNest = false;
            foreach (var rs in RoomStates)
                if (rs.NestAlive)
                {
                    anyNest = true;
                    if (RoomCount(rs.Area) < Rooms.RoomCap(Wave, Players.Count))
                        return true;
                }
            if (!anyNest && State == MatchState.Fighting)
            {
                spawnQueue.Clear();
                squadQueue.Clear();
            }
            return false;
        }

        int RoomCount(int area)
        {
            int n = 0;
            foreach (var e in Enemies.Values)
                if (e.Home == area && !e.Spilled && e.Type != EnemyType.Nest && e.Hp > 0f)
                    n++;
            return n;
        }

        /// The open map: where an enemy (or a squad) comes in: a random spot anywhere on the map (an area picked by its
        /// size, never the vault), clear of walls and at least SpawnClear from every player.
        V2 RoomsRandomSpot(float radius)
        {
            V2 at = new V2(Width / 2f, Height / 2f);
            for (int tries = 0; tries < 60; tries++)
            {
                at = RandomInRooms(radius + 30f, radius + 30f);
                if (NearSolid(at, radius + 8f))
                    continue;
                bool near = false;
                foreach (var p in Players.Values)
                    near |= !p.Downed && p.Pos.DistanceTo(at) < Rooms.SpawnClear;
                if (!near)
                    return at;
            }
            return at;
        }

        /// Where the next of the wave's enemies comes in: by the nest of the room with the most room to spare.
        V2 RoomSpawnSpot(float radius, out int home)
        {
            RoomState best = null;
            int bestFree = 0, cap = Rooms.RoomCap(Wave, Players.Count);
            foreach (var rs in RoomStates)
                if (rs.NestAlive)
                {
                    int free = cap - RoomCount(rs.Area);
                    if (best == null || free > bestFree)
                    {
                        best = rs;
                        bestFree = free;
                    }
                }
            if (best == null)
            {
                home = -1;
                return RandomInArena(80f);
            }
            home = best.Area;
            V2 nest = Rooms.NestSpot(best.Area);
            for (int i = 0; i < 12; i++)
            {
                V2 at = nest + V2.FromAngle(Rand(0f, MathX.Tau)) * Rand(60f, 180f);
                if (Rooms.Areas[best.Area].Contains(at, -radius - 20f) && !NearSolid(at, radius + 6f))
                    return at;
            }
            return nest + new V2(0f, 60f);
        }

        /// Host, from TickEnemies: a room's enemies guard their nest while it lives and nobody's in the room; the vault's
        /// mini-boss never leaves the vault. Returns true while holding (no shooting at nothing).
        bool RoomsLeash(Enemy e, ref V2 tpos)
        {
            if (!RoomsOn || e.Home < 0)
                return false;
            if (e.Spilled)
            {
                if (e.ExitDoor >= 0)
                {
                    var door = Rooms.Doors[e.ExitDoor];
                    if (Rooms.AreaAt(e.Pos) == e.Home)
                    {
                        V2 outward = (door.Side(door.Other(e.Home)) - door.Pos).Normalized();
                        tpos = door.Pos + outward * 500f + door.Along * (((e.Id % 5) - 2) * 18f);  // on its way out through its door (aimed well past it: shooters stop short of what they head for)
                        return true;
                    }
                    e.ExitDoor = -1;  // out: now it hunts
                }
                return false;
            }
            bool vault = e.Home == Rooms.Vault;
            var rs = vault ? null : StateOf(e.Home);
            if (!vault && (rs == null || !rs.NestAlive))
                return false;  // the nest is down: what's left comes for you
            var area = Rooms.Areas[e.Home];
            Player near = null;
            foreach (var p in Players.Values)
                if (!p.Downed && area.Contains(p.Pos) && (near == null || p.Pos.DistanceSq(e.Pos) < near.Pos.DistanceSq(e.Pos)))
                    near = p;
            if (near != null)
            {
                tpos = near.Pos;
                return false;
            }
            if (!vault && e.Type != EnemyType.Nest)
            {
                // Overflow (owner, Nov 2026): it guards only so long, then leaves through one of the room's doors, picked at
                // random, and hunts. The nest refills the room behind it, so while a nest lives the rooms keep pouring out.
                e.HomeT += tickDt;
                if (e.HomeT >= Rooms.GuardTime(Wave))
                {
                    e.Spilled = true;
                    e.ExitDoor = PickSpillDoor(e.Home);
                    return RoomsLeash(e, ref tpos);
                }
            }
            V2 home = vault ? area.Mid : Rooms.NestSpot(e.Home);
            float rx = Math.Min(150f, area.Size.X / 3f), ry = Math.Min(130f, area.Size.Y / 3f);
            tpos = home + new V2((float)Math.Cos(e.T * 0.5f + e.Id) * rx, (float)Math.Sin(e.T * 0.4f + e.Id * 1.7f) * ry);
            return true;
        }

        /// Is this area's door locked for whoever is in it (decision 9): a room while its nest lives, the vault while its
        /// mini-boss does. One way: you can come in, not go out.
        public bool AreaLocked(int area)
        {
            if (!RoomsOn)
                return false;
            if (area == Rooms.Vault)
                return VaultUp && !VaultCleared;
            var rs = StateOf(area);
            return rs != null && rs.NestAlive && !rs.Cleared;
        }

        /// Your own game, after you move: in a locked room you stay in it (a dodge or dash can't take you out either).
        V2 RoomsHold(V2 before, V2 now, float r)
        {
            if (!RoomsOn)
                return now;
            int a = Rooms.AreaAt(before);
            if (!AreaLocked(a) || !Rooms.Areas[a].Contains(before) || Rooms.AreaAt(now) == a)
                return now;
            var area = Rooms.Areas[a];
            return now.Clamp(area.Min + new V2(r, r), area.Max - new V2(r, r));
        }

        /// Host, when a wave is cleared: the vault closes with it (an unbeaten mini-boss goes, anyone inside is let out).
        void EndRoomsWave()
        {
            if (!VaultUp)
                return;
            Enemy boss;
            if (Enemies.TryGetValue(vaultBossId, out boss))
                Enemies.Remove(vaultBossId);
            var v = Rooms.Areas[Rooms.Vault];
            var door = Rooms.Doors[Rooms.VaultDoor];
            foreach (var p in Players.Values)
                if (v.Contains(p.Pos))
                    MovePlayer(p, door.Side(Rooms.TopHall) + new V2(0f, 40f));
        }

        /// The wave's foes, leaving out the vault's (it's optional: the wave ends without it).
        int FoesOutsideVault()
        {
            int n = Foes();
            if (VaultOn)
                foreach (var e in Enemies.Values)
                    if (e.Home == Rooms.Vault)
                        n--;
            return n;
        }

        /// Host, when the rooms go (a boss wave): everyone outside the hub is brought in, and the props left in the
        /// rooms and hallways are cleared away.
        void PullIntoHub()
        {
            var hub = Rooms.Areas[Rooms.Hub];
            foreach (var p in Players.Values)
                if (!hub.Contains(p.Pos, -1f))
                {
                    V2 at = p.Pos.Clamp(hub.Min + new V2(90f, 90f), hub.Max - new V2(90f, 90f));
                    MovePlayer(p, PushOutOfProps(at, p.Def.BodyR));
                }
            scratchIds.Clear();
            foreach (var pr in Props.Values)
                if (!hub.Contains(pr.Pos))
                    scratchIds.Add(pr.Id);
            foreach (int id in scratchIds)
                Props.Remove(id);
        }

        /// The rooms' state for everyone (after the layout byte): each nest room's flags, spill timer and next door, then
        /// the vault. Fixed size, so it reads the same with the rooms up or down.
        void SyncRooms(NetSync s)
        {
            foreach (var rs in RoomStates)
            {
                byte f = (byte)((rs.NestAlive ? 1 : 0) | (rs.Cleared ? 2 : 0) | (rs.Seen ? 4 : 0));
                byte t = (byte)Math.Max(0, Math.Min(255, (int)Math.Ceiling(rs.SpillT * 10f)));
                byte d = (byte)(rs.NextDoor + 1);
                s.Byte(ref f);
                s.Byte(ref t);
                s.Byte(ref d);
                if (s.Reading)
                {
                    rs.NestAlive = (f & 1) != 0;
                    rs.Cleared = (f & 2) != 0;
                    rs.Seen = (f & 4) != 0;
                    rs.SpillT = t / 10f;
                    rs.NextDoor = d - 1;
                }
            }
            byte v = (byte)((VaultUp ? 1 : 0) | (VaultCleared ? 2 : 0));
            s.Byte(ref v);
            if (s.Reading)
            {
                bool up = (v & 1) != 0;
                if (up != VaultUp)
                    LayoutAge = 0f;
                VaultUp = up;
                VaultCleared = (v & 2) != 0;
            }
        }

        /// Your own game for you, the host for enemies: through a portal if you're in one (and not just out of one).
        V2 RoomsPortal(V2 pos, ref float cooldown, float dt)
        {
            cooldown = Math.Max(0f, cooldown - dt);
            if (!RoomsOn || cooldown > 0f)
                return pos;
            var exit = Rooms.PortalExitFor(pos);
            if (!exit.HasValue)
                return pos;
            cooldown = Rooms.PortalCooldown;
            return exit.Value;
        }

        /// The hub's bounds while the rooms are up (else the playable arena): things that belong in the middle of
        /// the fight, like a Data Gate, go here.
        public V2 HubMin => RoomsOn ? V2.Zero : ArenaMin;
        public V2 HubMax => RoomsOn ? new V2(Width, Height) : ArenaMax;

        /// Is this the vault's mini-boss (any game: by where it stands)? Its intro and boss bar wait till you go to it.
        public bool InVault(Enemy e) => VaultOn && Rooms.Areas[Rooms.Vault].Contains(e.Disp, 20f);

        /// Is someone at the vault: in it, or in the top hallway near its door?
        public bool AtVault(Player p) => VaultOn && p != null && (Rooms.Areas[Rooms.Vault].Contains(p.Disp) || p.Disp.DistanceTo(Rooms.Doors[Rooms.VaultDoor].Pos) < 260f);

        /// Which part of the Rooms world a point is in (the hub when the rooms aren't up).
        public int AreaAt(V2 p) => RoomsOn ? Rooms.AreaAt(p) : Rooms.Hub;

        /// A random spot in the Rooms world (m from an area's edges): in one of its areas, chosen by size (never the vault).
        V2 RandomInRooms(float mx, float my)
        {
            float total = 0f;
            foreach (var a in Rooms.Areas)
                if (!a.Vault)
                    total += a.Size.X * a.Size.Y;
            float pick = Rand(0f, total);
            foreach (var a in Rooms.Areas)
            {
                if (a.Vault)
                    continue;
                pick -= a.Size.X * a.Size.Y;
                if (pick <= 0f)
                {
                    float x0 = a.Min.X + Math.Min(mx, a.Size.X / 2f - 1f), x1 = a.Max.X - Math.Min(mx, a.Size.X / 2f - 1f);
                    float y0 = a.Min.Y + Math.Min(my, a.Size.Y / 2f - 1f), y1 = a.Max.Y - Math.Min(my, a.Size.Y / 2f - 1f);
                    return new V2(Rand(x0, x1), Rand(y0, y1));
                }
            }
            return new V2(Width / 2f, Height / 2f);
        }
    }
}

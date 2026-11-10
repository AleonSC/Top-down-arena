using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// The Rooms arena (Rooms.cs, Docs/Rooms.md, phase 1): the Arena setting, the Wings world on regular waves (side rooms and
// hallways joined by doors round the hub), the hub alone on boss waves, no wrapping, walls that stop walking and shots,
// spawns in the rooms, the Data Gate in the hub, and enemies and the smart bot finding their way through the doors.
static partial class Program
{
    static void RoomsArenaTest(float dt)
    {
        // The parked room-by-room version (nests, spills, room locks, overflow) runs first, then the open map (the default).
        Check(!Rooms.RoomByRoom, "the open map is the default (the room-by-room version is parked)");
        Rooms.RoomByRoom = true;
        Game.LayoutsByDefault = true;
        Game.PropsByDefault = true;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var spawnEnemy = typeof(Game).GetMethod("SpawnEnemy", flags);
        var spawnEnemyAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var moveEnemy = typeof(Game).GetMethod("MoveEnemy", flags);
        var pickRooms = typeof(Game).GetMethod("PickRooms", flags);
        var pickLayout = typeof(Game).GetMethod("PickLayout", flags);
        var keepIn = typeof(Game).GetMethod("KeepInArena", flags);

        // The world: one box round the hub, the same margin on each side; doors in the walls; nothing inside a wall.
        V2 lo = Rooms.WorldMin, hi = Rooms.WorldMax;
        Check(Math.Abs(lo.X + (hi.X - Game.Width)) < 0.01f && Math.Abs(lo.Y + (hi.Y - Game.Height)) < 0.01f, "the Rooms world reaches as far past the hub on each side (ArenaMin/ArenaMax stay symmetric)");
        bool tiled = true;
        var rngT = new Random(3);
        for (int i = 0; i < 2000; i++)
        {
            var q = new V2(lo.X + (float)rngT.NextDouble() * (hi.X - lo.X), lo.Y + (float)rngT.NextDouble() * (hi.Y - lo.Y));
            tiled &= Rooms.Areas.Count(a => a.Contains(q)) >= 1;
        }
        Check(tiled, "the hub, the two rooms and the two hallways fill the whole world (no gaps)");
        var withRooms = Rooms.With(LayoutId.Open);
        bool doorsOpen = Rooms.Doors.Where((d, i) => Rooms.DoorUp(i, false)).All(d => withRooms.Solids.All(sd => !Layouts.Inside(sd, d.Pos, 30f) && !Layouts.Inside(sd, d.Pos + d.Across * Rooms.DoorPad, 20f) && !Layouts.Inside(sd, d.Pos - d.Across * Rooms.DoorPad, 20f)));
        Check(doorsOpen, "every doorway is open, with room to stand either side");
        bool wallsOk = true;
        foreach (var ld in new[] { LayoutId.Open }.Concat(Layouts.Playable().Select(x => x.Id)))
        foreach (bool vaultUp in new[] { false, true })
        {
            var d = Rooms.With(ld, vaultUp);
            wallsOk &= d.Waypoints.All(p => d.Solids.All(sd => !Layouts.Inside(sd, p, 18f)));
            wallsOk &= d.Solids.All(sd => !sd.Edge) && d.EnemyGates.Length == 0 && d.PlayerGates.Length == 0;
            wallsOk &= Rooms.Doors.Where((dr, i) => Rooms.DoorUp(i, vaultUp)).All(dr => d.Waypoints.Any(w => w.DistanceTo(dr.Pos + dr.Across * Rooms.DoorPad) < 1f) && d.Waypoints.Any(w => w.DistanceTo(dr.Pos - dr.Across * Rooms.DoorPad) < 1f));
        }
        Check(wallsOk, "with every floor plan in the hub, with and without the vault: steering spots clear of the walls, no edge gates, a spot each side of every door");
        var vaultWalls = Rooms.With(LayoutId.Open, true);
        var vd = Rooms.Doors[Rooms.Doors.Length - 1];
        Check(vaultWalls.Solids.All(sd => !Layouts.Inside(sd, vd.Pos, 30f)) && withRooms.Solids.Any(sd => Layouts.Inside(sd, vd.Pos, 0f)), "the vault's door is open on a vault wave and walled up otherwise");
        Check(Rooms.Cover.All(c => Rooms.Areas.Count(a => !a.Hub && a.Contains(c.Pos, -30f)) == 1), "the small obstacles stand inside the rooms and hallways");

        // The Arena setting: Classic by default on every preset, a flavour knob, kept by saves and the network.
        var roomsDiff = Difficulty.Of(DifficultyLevel.Hard);
        roomsDiff.Set(DiffKnob.ArenaRooms, 1f);
        var back = Difficulty.Parse(roomsDiff.ToText());
        var ms = new System.IO.MemoryStream();
        roomsDiff.Write(new System.IO.BinaryWriter(ms));
        ms.Position = 0;
        var sent = Difficulty.Read(new System.IO.BinaryReader(ms));
        bool classicDefault = Enumerable.Range(0, 4).All(l => Difficulty.Of((DifficultyLevel)l)[DiffKnob.ArenaRooms] < 0.5f) && new Difficulty()[DiffKnob.ArenaRooms] < 0.5f;
        Check(classicDefault && roomsDiff.Level == DifficultyLevel.Hard && back[DiffKnob.ArenaRooms] >= 0.5f && sent[DiffKnob.ArenaRooms] >= 0.5f,
            "the Arena setting is Classic on every preset, Rooms doesn't make a Custom difficulty, and it survives saving and sending");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Run(s, dt, 0.5f);

        // Classic (the default): today's arena, no rooms.
        Check(!g.RoomsArena && !g.RoomsUp && !g.RoomsOn && g.ArenaMin.X == 0f && !g.Walled, "Classic by default: no rooms, the arena wraps as always");

        // Rooms: the world comes up on a regular wave, and the client gets it too.
        g.Diff.Set(DiffKnob.ArenaRooms, 1f);
        cg.Diff.Set(DiffKnob.ArenaRooms, 1f);
        pickRooms.Invoke(g, null);
        Run(s, dt, 0.4f);
        Check(g.RoomsOn && cg.RoomsOn && g.Walled && cg.ArenaMin.DistanceTo(Rooms.WorldMin) < 0.01f && g.ArenaMax.DistanceTo(Rooms.WorldMax) < 0.01f,
            "with the Rooms arena a regular wave has the side rooms up, on both screens (the arena is the whole world, walled)");
        Check(g.LayoutOn && g.ActiveLayout.Solids.Length > Rooms.Walls.Length && g.ActiveLayout.Name.Contains("Wings"), "the rooms' walls are part of the floor plan (" + g.ActiveLayout.Name + ")");
        var me = g.Players[1];
        V2 clamped = (V2)keepIn.Invoke(g, new object[] { new V2(lo.X - 60f, 450f), 14f });
        Check(clamped.X >= lo.X + 13.9f, "no wrapping round the world's edge: it holds you in (" + clamped.X + ")");

        // Walls: no walking through them, no shots through them, the doors let both through.
        V2 inWall = g.PushOutOfSolids(new V2(2f, 150f), 12f);
        V2 inDoor = g.PushOutOfSolids(new V2(2f, Game.Height / 2f), 12f);
        Check(Math.Abs(inWall.X) >= Rooms.WallT / 2f + 12f - 0.1f && Math.Abs(inDoor.X - 2f) < 0.01f, "the wall between the hub and a room pushes you out; its door lets you through (" + inWall.X + ", " + inDoor.X + ")");
        Quiet();
        g.Props.Clear();
        var wallShot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, new V2(200f, 150f), new V2(-400f, 0f), 0f, 3f, 10f, 0, false, 0f });
        var doorShot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(200f, Game.Height / 2f), new V2(-500f, 0f), 0f, 3f, 10f, 1, false, 0f });
        float wallMin = 9999f, doorMin = 9999f;
        Run(s, dt, 0.9f, null, () =>
        {
            if (g.Bullets.ContainsKey(wallShot.Id)) wallMin = Math.Min(wallMin, wallShot.Pos.X);
            if (g.Bullets.ContainsKey(doorShot.Id)) doorMin = Math.Min(doorMin, doorShot.Pos.X);
        });
        Check(wallMin > 0f && doorMin < -100f, "a wall stops a shot; a shot through the doorway flies on into the room (" + (int)wallMin + ", " + (int)doorMin + ")");

        var startRooms = typeof(Game).GetMethod("StartRoomsWave", flags);
        var spawnReady = typeof(Game).GetMethod("RoomsSpawnReady", flags);
        var roomsHold = typeof(Game).GetMethod("RoomsHold", flags);
        var movePlayer = typeof(Game).GetMethod("MovePlayer", flags);
        var spawnQueue = (IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        var hub = Rooms.Areas[Rooms.Hub];
        Func<RoomState> west = () => g.RoomStates.First(r => r.Area == Rooms.West), east = () => g.RoomStates.First(r => r.Area == Rooms.East);
        Quiet();
        Run(s, dt, 0.3f);
        g.Props.Clear();
        foreach (var p in g.Players.Values) movePlayer.Invoke(g, new object[] { p, new V2(Game.Width / 2f, Game.Height / 2f) });

        // Each room gets a nest (three times a Nest's health), its spill timer and a door for its first spill.
        startRooms.Invoke(g, null);
        var nests = g.Enemies.Values.Where(e => e.Type == EnemyType.Nest && e.Home >= 0).ToList();
        float nestHp = (float)typeof(Game).GetMethod("SpawnHp").Invoke(g, new object[] { EnemyType.Nest, g.Wave });
        bool doorsOk = g.RoomStates.All(r => r.NextDoor >= 0 && Rooms.Doors[r.NextDoor].Joins(r.Area) && !Rooms.Areas[Rooms.Doors[r.NextDoor].Other(r.Area)].Vault
            && !Rooms.Doors[r.NextDoor].Joins(r.Area == Rooms.West ? Rooms.East : Rooms.West));
        Check(nests.Count == 2 && nests.All(n => Rooms.AreaAt(n.Pos) == n.Home && Math.Abs(n.MaxHp - nestHp * Rooms.NestHpMult) < 1f)
            && g.RoomStates.All(r => r.NestAlive && !r.Cleared && r.SpillT > 5f) && doorsOk,
            "each room gets a nest (" + Rooms.NestHpMult + "x a Nest's health), a spill timer and a door out for its first spill");
        Run(s, dt, 0.4f);
        Check(cg.RoomStates.All(r => r.NestAlive && !r.Cleared && r.SpillT > 4f && r.NextDoor >= 0), "the client knows the nests, the spill timers and the doors");

        // The wave's enemies come in by the nests, up to each room's cap, then wait.
        int cap = Rooms.RoomCap(g.Wave, g.Players.Count), spawned = 0;
        foreach (var r in g.RoomStates) r.SpillT = 999f;
        while ((bool)spawnReady.Invoke(g, null) && spawned < 100)
        {
            spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt });
            spawned++;
        }
        var roomFoes = g.Enemies.Values.Where(e => e.Type != EnemyType.Nest && e.Home >= 0).ToList();
        Check(spawned == cap * 2 && roomFoes.All(e => Rooms.AreaAt(e.Pos) == e.Home && (e.Home == Rooms.West || e.Home == Rooms.East)),
            "the wave's enemies come in by the nests, in their rooms, " + cap + " to a room, then wait (" + spawned + " spawned)");
        Check(!g.Enemies.Values.Any(e => hub.Contains(e.Pos)), "...none in the hub");

        // Nobody in a room: its enemies stay and guard the nest (no shooting at nothing).
        int shotsBefore = g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
        Run(s, dt, 4f, null, () => { foreach (var r in g.RoomStates) r.SpillT = 999f; });
        Check(roomFoes.All(e => !g.Enemies.ContainsKey(e.Id) || Rooms.AreaAt(e.Pos) == e.Home), "with nobody in a room its enemies stay in it, round the nest");
        Check(g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)) <= shotsBefore + 2, "...and don't shoot at nothing");

        // Overflow: after guarding for GuardTime they leave the room through its doors (any of them) and come for you,
        // and the nest fills the room up again behind them.
        foreach (var e in roomFoes) if (g.Enemies.ContainsKey(e.Id)) e.HomeT = Rooms.GuardTime(g.Wave);
        var exits = new HashSet<int>();
        var cameOut = new HashSet<int>();
        Run(s, dt, 12f, null, () =>
        {
            foreach (var r in g.RoomStates) r.SpillT = 999f;
            foreach (var e in roomFoes)
            {
                if (!g.Enemies.ContainsKey(e.Id)) continue;
                if (e.ExitDoor >= 0) exits.Add(e.ExitDoor);
                if (Rooms.AreaAt(e.Pos) != e.Home) cameOut.Add(e.Id);
            }
        });
        Check(roomFoes.All(e => !g.Enemies.ContainsKey(e.Id) || e.Spilled) && exits.Count >= 3 && cameOut.Count >= roomFoes.Count / 2,
            "after guarding " + Rooms.GuardTime(g.Wave).ToString("0.#") + " s the room's enemies pour out through its doors (" + exits.Count + " doors used, " + cameOut.Count + " of " + roomFoes.Count + " out)");
        Check(roomFoes.Any(e => g.Enemies.ContainsKey(e.Id) && (hub.Contains(e.Pos) || Rooms.Areas[Rooms.AreaAt(e.Pos)].Hall)), "...into the hub and the hallways");
        int fresh = g.Enemies.Values.Count(e => e.Home >= 0 && !e.Spilled && e.Type != EnemyType.Nest && !roomFoes.Contains(e));
        Check(fresh >= cap, "the nests fill their rooms again behind them, from the wave (" + fresh + " new guards)");
        foreach (var e in g.Enemies.Values) if (e.Type != EnemyType.Nest) e.Hp = 0f;
        Run(s, dt, 0.3f);

        // A spill: fast runners out through the chosen door, hunting.
        west().NextDoor = 0;  // the hub door (Rooms.Doors[0]: hub | West)
        int spillDoor = west().NextDoor;
        west().SpillT = 0.05f;
        Run(s, dt, 0.3f);
        var runners = g.Enemies.Values.Where(e => e.Spilled && e.Type == EnemyType.Runner).ToList();
        Check(runners.Count == Rooms.SpillSize(g.Wave, g.Players.Count) && runners.All(e => e.Pos.DistanceTo(Rooms.Doors[spillDoor].Pos) < 150f && Rooms.AreaAt(e.Pos) == Rooms.Hub) && west().SpillT > 5f,
            "a room spills " + runners.Count + " runners out through its door (" + Rooms.Areas[Rooms.Doors[spillDoor].Other(Rooms.West)].Name + "), and its timer starts again");
        Run(s, dt, 3f, null, () => { foreach (var r in g.RoomStates) r.SpillT = Math.Max(r.SpillT, 50f); });
        Check(runners.All(e => !g.Enemies.ContainsKey(e.Id) || Rooms.AreaAt(e.Pos) != Rooms.West || Rooms.Doors[spillDoor].Other(Rooms.West) != Rooms.Hub),
            "spilled into the hub, the runners stay out of the room and come after you");
        foreach (var e in runners) e.Hp = 0f;

        // Doors lock behind you: in a room with its nest alive you can't step back out; you can always step in.
        V2 inside = new V2(-30f, Game.Height / 2f), outDoor = new V2(30f, Game.Height / 2f);
        V2 held2 = (V2)roomsHold.Invoke(g, new object[] { inside, outDoor, 14f });
        V2 entered = (V2)roomsHold.Invoke(g, new object[] { outDoor, inside, 14f });
        Check(g.AreaLocked(Rooms.West) && held2.X <= -14f + 0.01f && entered.DistanceTo(inside) < 0.01f, "a room's doors lock behind you while its nest lives (" + held2.X + "); stepping in is fine");
        Check(!g.AreaLocked(Rooms.TopHall) && !g.AreaLocked(Rooms.Hub), "hallways and the hub never lock");

        // Clearing a room: the nest and what it kept. Its doors open, two big health orbs and XP for everyone.
        float xp0 = g.Players.Values.First().Xp;
        int orbs0 = g.Orbs.Count;
        foreach (var e in g.Enemies.Values) if (e.Home == Rooms.West) e.Hp = 0f;
        Run(s, dt, 0.4f);
        Check(west().Cleared && !west().NestAlive && !g.AreaLocked(Rooms.West) && g.Orbs.Count >= orbs0 + 2, "kill the nest and its room's enemies: the room is cleared, its doors open, two big health orbs drop");
        Check(cg.RoomStates.First(r => r.Area == Rooms.West).Cleared, "...on the client too");
        Check(g.Players.Values.First().Xp > xp0 || g.Players.Values.First().Level > 1, "...and everyone gets some XP");
        Check(((V2)roomsHold.Invoke(g, new object[] { inside, outDoor, 14f })).DistanceTo(outDoor) < 0.01f, "a cleared room lets you out");

        // With every nest down the rest of the wave's queue is called off.
        foreach (var e in g.Enemies.Values) if (e.Home == Rooms.East && e.Type == EnemyType.Nest) e.Hp = 0f;
        Run(s, dt, 0.3f);
        spawnQueue.Add(EnemyType.Grunt); spawnQueue.Add(EnemyType.Grunt);
        bool ready = (bool)spawnReady.Invoke(g, null);
        Check(!ready && spawnQueue.Count == 0, "with every nest down, the rest of the wave is called off");
        Quiet();
        Run(s, dt, 0.3f);

        Rooms.RoomByRoom = false;
        Quiet();
        Run(s, dt, 0.3f);
        // The open map (Rooms.RoomByRoom off, the default since Nov 10): no nests, no room locks, enemies come in anywhere
        // at random but never close to a player, and the camera follows you over the whole map.
        startRooms.Invoke(g, null);
        Check(!g.Enemies.Values.Any(e => e.Type == EnemyType.Nest) && !g.AreaLocked(Rooms.West) && !g.AreaLocked(Rooms.East), "no nests and no room locks on the open map");
        {
            var mine = new HashSet<int>(g.Enemies.Keys);
            for (int i = 0; i < 80; i++) spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt });
            var got = g.Enemies.Values.Where(e => !mine.Contains(e.Id)).ToList();
            float nearest = got.Min(e => g.Players.Values.Min(p => p.Pos.DistanceTo(e.Pos)));
            int areasUsed = got.Select(e => Rooms.AreaAt(e.Pos)).Distinct().Count();
            bool clear = got.All(e => g.ActiveLayout.Solids.All(sd => !Layouts.Inside(sd, e.Pos, e.Radius)) && Rooms.AreaAt(e.Pos) != Rooms.Vault);
            Check(got.Count == 80 && nearest >= Rooms.SpawnClear - 1f && areasUsed >= 4 && clear,
                "enemies come in anywhere on the map: " + areasUsed + " of 5 areas, never closer than " + (int)nearest + " px to a player, never in a wall or the vault");
            foreach (var e in got) e.Hp = 0f;
            Run(s, dt, 0.3f);
        }
        V2 camAt = Rooms.ViewCentre(new V2(-300f, 450f), Game.Width / 3f, Game.Height / 3f, false);
        Check(Math.Abs(camAt.X + 300f) < 0.5f && Math.Abs(camAt.Y - 450f) < 0.5f, "the camera follows you anywhere (no snapping to the room you're in)");


        // The Data Gate opens in the hub.
        {
            var state0 = g.State;
            g.State = MatchState.Intermission;
            int outside = 0;
            for (int i = 0; i < 40; i++)
            {
                g.Room = RoomPhase.None;
                g.DevOpenPortal(ChallengeKind.Defend);
                if (!hub.Contains(g.PortalPos, -Challenges.PortalR)) outside++;
            }
            g.Room = RoomPhase.None; g.RoomKind = ChallengeKind.None; g.State = state0;
            Check(outside == 0, "a Data Gate opens inside the hub (" + outside + " of 40 outside)");
        }

        // Steering: enemies get from anywhere to anywhere through the doors (rooms, hallways, the hub, any floor plan).
        {
            var rng = new Random(11);
            int tried = 0, reached = 0;
            foreach (var ld in new[] { LayoutId.Open, LayoutId.Cross, LayoutId.Funnels, LayoutId.Bunker })
            {
                g.DevSetLayout(ld);
                g.Props.Clear();
                var d = g.ActiveLayout;
                for (int k = 0; k < 16; k++)
                {
                    Func<int, V2> spot = area =>
                    {
                        var a = Rooms.Areas[area];
                        for (int tries = 0; tries < 200; tries++)
                        {
                            var q = new V2(a.Min.X + 50f + (float)rng.NextDouble() * (a.Size.X - 100f), a.Min.Y + 50f + (float)rng.NextDouble() * (a.Size.Y - 100f));
                            if (d.Solids.All(sd => !Layouts.Inside(sd, q, 34f)))
                                return q;
                        }
                        return a.Mid;
                    };
                    int fromArea = 1 + k % 4, toArea = k % 2 == 0 ? Rooms.Hub : 1 + (k + 1) % 4;
                    if (k >= 8) { fromArea = k % 2 == 0 ? Rooms.TopHall : Rooms.BottomHall; toArea = k % 4 < 2 ? Rooms.West : Rooms.East; }
                    V2 from = spot(fromArea), to = spot(toArea);
                    var walker = (Enemy)spawnEnemyAt.Invoke(g, new object[] { k % 3 == 2 ? EnemyType.Runner : EnemyType.Brute, from, 0f, 0, false });
                    walker.SpawnT = 0f; walker.Hp = walker.MaxHp = 99999f;
                    float near = 9999f;
                    for (int i = 0; i < 4000 && near > 50f; i++)
                    {
                        moveEnemy.Invoke(g, new object[] { walker, to, dt });
                        walker.T += dt;
                        near = Math.Min(near, walker.Pos.DistanceTo(to));
                    }
                    tried++;
                    if (near <= 50f) reached++;
                    else Console.WriteLine("  stuck: {5} {0} {1:0},{6:0} -> {2} {3:0},{7:0}, got within {4} (ended at {8:0},{9:0}, layout {10})", Rooms.Areas[fromArea].Name, from.X, Rooms.Areas[toArea].Name, to.X, (int)near, walker.Type, from.Y, to.Y, walker.Pos.X, walker.Pos.Y, ld);
                    walker.Hp = 0f;
                    Quiet();
                }
            }
            Check(reached >= tried - 1, "enemies find their way between rooms, hallways and the hub through the doors (" + reached + " of " + tried + ")");
        }
        g.DevSetLayout(LayoutId.Open);
        g.Props.Clear();

        // The smart bot (F1 "Bot plays for you") walks through a door to get at an enemy in a room.
        {
            Quiet();
            Run(s, dt, 0.2f);
            g.BotLevel = BotSkill.Good;
            g.Bot = true;
            me.Pos = me.Disp = new V2(Game.Width / 2f, 200f);
            var mark = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Spinner, new V2(Game.Width + Rooms.RoomW - 150f, 150f), 0f, 0, false });
            mark.SpawnT = 0f; mark.Hp = mark.MaxHp = 99999f; mark.Speed = 0f;
            bool inRoom = false;
            Run(s, dt, 25f, () => inRoom, () => { inRoom |= Rooms.AreaAt(me.Pos) == Rooms.East; mark.Pos = new V2(Game.Width + Rooms.RoomW - 150f, 150f); });
            Check(inRoom, "the smart bot goes through the door into the room to get at an enemy there");
            mark.Hp = 0f;
            g.BotLevel = BotSkill.Off;
            Quiet();
        }

        // A boss wave: the rooms go, and anyone out in them is brought into the hub.
        me.Pos = me.Disp = new V2(-450f, 450f);
        g.Props.Clear();
        var outCrate = g.AddProp(PropKind.Crate, new V2(-450f, 700f));
        g.Wave = 10;
        pickRooms.Invoke(g, null);
        pickLayout.Invoke(g, null);
        Run(s, dt, 0.4f);
        var cme = cg.Players[1];
        Check(!g.RoomsUp && !cg.RoomsOn && g.ArenaMin.X == 0f && cg.ArenaMin.X == 0f, "a boss wave is the hub alone (on both screens)");
        Check(hub.Contains(me.Pos, -60f) && hub.Contains(cme.Pos, -60f) && !g.Props.ContainsKey(outCrate.Id), "anyone out in the rooms is brought into the hub, and the rooms' props go (" + me.Pos + ")");
        g.Wave = 11;
        pickRooms.Invoke(g, null);
        Check(g.RoomsUp, "the next cycle's first wave has the rooms again");

        // Not with Tiny Arena (it walls the hub in itself), and not in a Boss Hunt or the Practice Range.
        g.Mods |= RunMod.TinyArena;
        pickRooms.Invoke(g, null);
        Check(!g.RoomsUp, "Tiny Arena keeps the hub alone");
        g.Mods &= ~RunMod.TinyArena;
        pickRooms.Invoke(g, null);

        // The vault: one regular wave in each stretch of ten.
        {
            var fStretch = typeof(Game).GetField("vaultStretch", flags);
            var fWave = typeof(Game).GetField("vaultWave", flags);
            g.Mods &= ~RunMod.TinyArena;
            fStretch.SetValue(g, -1);
            var vaultWaves = new List<int>();
            for (int w = 1; w <= 50; w++)
            {
                g.Wave = w;
                pickRooms.Invoke(g, null);
                if (g.VaultUp) vaultWaves.Add(w);
            }
            bool onePer = Enumerable.Range(0, 5).All(st => vaultWaves.Count(w => (w - 1) / 10 == st) == 1) && vaultWaves.All(w => w % 5 != 0);
            Check(onePer, "one regular wave in each stretch of ten has the vault (" + string.Join(", ", vaultWaves) + ")");

            // A vault wave: the world reaches up to it, its mini-boss is the Gatekeeper, and he stays in it.
            Quiet();
            foreach (var p in g.Players.Values) movePlayer.Invoke(g, new object[] { p, new V2(Game.Width / 2f, Game.Height / 2f) });
            g.Wave = 7; fStretch.SetValue(g, 0); fWave.SetValue(g, 7);
            pickRooms.Invoke(g, null);
            startRooms.Invoke(g, null);
            Run(s, dt, 0.4f);
            var vault = Rooms.Areas[Rooms.Vault];
            var keeper = g.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Gatekeeper);
            Check(g.VaultOn && cg.VaultOn && g.ArenaMin.Y < -Rooms.HallH - Rooms.VaultH + 1f && cg.ArenaMin.DistanceTo(g.ArenaMin) < 0.01f && g.ActiveLayout.Waypoints.Any(w => vault.Contains(w)),
                "a vault wave opens the vault off the top hallway (on both screens)");
            Check(keeper != null && keeper.Home == Rooms.Vault && vault.Contains(keeper.Pos), "the Gatekeeper waits in the vault");
            Run(s, dt, 5f, null, () => { foreach (var r in g.RoomStates) r.SpillT = 999f; });
            Check(keeper != null && vault.Contains(keeper.Pos, 10f), "...and stays there while nobody comes in");
            Check(g.AreaLocked(Rooms.Vault), "the vault locks behind you while he lives");

            // The vault is optional: clear the rooms and the wave ends without him (he goes with it).
            foreach (var e in g.Enemies.Values) if (e.Type != EnemyType.Gatekeeper) e.Hp = 0f;
            spawnQueue.Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            g.State = MatchState.Fighting;
            Run(s, dt, 3f, () => g.State == MatchState.Intermission, () => { foreach (var r in g.RoomStates) r.SpillT = 999f; });
            Check(g.State == MatchState.Intermission && !g.Enemies.Values.Any(e => e.Type == EnemyType.Gatekeeper), "the wave ends without the vault, and its mini-boss goes with the wave");

            // Beat him: everyone gets a prize pick (rare or better), as from his old Data Gate.
            g.State = MatchState.Fighting;
            g.Wave = 8; fStretch.SetValue(g, 0); fWave.SetValue(g, 8);
            pickRooms.Invoke(g, null);
            startRooms.Invoke(g, null);
            var keepWave = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Grunt, new V2(-450f, 800f), 0f, 0, false });  // (keeps the wave going: the vault is optional)
            keepWave.Hp = keepWave.MaxHp = 99999f;
            var picks0 = g.Players.Values.Select(p => p.PrizePicks).ToList();
            Run(s, dt, 0.3f);
            foreach (var e in g.Enemies.Values) if (e.Type == EnemyType.Gatekeeper) e.Hp = 0f;
            Run(s, dt, 0.4f, null, () => { foreach (var p in g.Players.Values) { p.Offer = null; } });
            Check(g.VaultCleared && cg.VaultCleared && g.Players.Values.Select(p => p.PrizePicks).Zip(picks0, (a2, b2) => a2 >= b2 + 1).All(x => x) || g.VaultCleared && g.Players.Values.All(p => p.OfferPrize),
                "beating the vault's Gatekeeper gives everyone a prize pick");
            Quiet();
            Run(s, dt, 0.3f);
            foreach (var p in g.Players.Values) { p.PrizePicks = 0; p.Offer = null; p.LegendaryQueue.Clear(); }
        }

        // The Gatekeeper's Data Gate: gone from the Rooms arena (he's in the vault), still there in Classic.
        {
            var maybe = typeof(Game).GetMethod("MaybeOpenPortal", flags);
            var fBlock = typeof(Game).GetField("portalBlock", flags);
            bool chOn = g.ChallengesOn;
            g.ChallengesOn = true;
            var kinds = new Dictionary<bool, HashSet<ChallengeKind>> { { true, new HashSet<ChallengeKind>() }, { false, new HashSet<ChallengeKind>() } };
            foreach (bool roomsArena in new[] { true, false })
            {
                g.Diff.Set(DiffKnob.ArenaRooms, roomsArena ? 1f : 0f);
                for (int i = 0; i < 80; i++)
                {
                    g.Room = RoomPhase.None;
                    fBlock.SetValue(g, -1);
                    g.Wave = 3;
                    maybe.Invoke(g, null);
                    if (g.Room == RoomPhase.Portal) kinds[roomsArena].Add(g.RoomKind);
                }
            }
            g.Room = RoomPhase.None; g.RoomKind = ChallengeKind.None; g.ChallengesOn = chOn;
            g.Diff.Set(DiffKnob.ArenaRooms, 1f);
            Check(!kinds[true].Contains(ChallengeKind.MiniBoss) && kinds[true].Count == 4 && kinds[false].Contains(ChallengeKind.MiniBoss),
                "the Rooms arena's Data Gates never hold the Gatekeeper (" + string.Join(", ", kinds[true]) + "); Classic's still do");
        }

        // A whole regular wave with the rooms up: the enemies come for you through the doors and the wave ends.
        {
            g.Wave = 6;
            Quiet();
            Run(s, dt, 1f);
            g.Bot = cg.Bot = true;  // both players: the smart bot
            g.BotLevel = cg.BotLevel = BotSkill.Good;
            foreach (var p in g.Players.Values) { p.Pos = p.Disp = new V2(Game.Width / 2f, Game.Height / 2f); }
            g.DevJumpToWave(7);
            Run(s, dt, 3f, () => g.State == MatchState.Fighting, NoPicks);
            int startWave = g.Wave;
            Check(g.RoomsOn, "wave " + g.Wave + " starts with the rooms up");
            float worstSpread = 0f;
            int seenIn = 0;
            Run(s, dt, 240f, () => g.Wave > startWave || g.State == MatchState.Intermission, () =>
            {
                NoPicks();
                foreach (var e in g.Enemies.Values)
                {
                    if (hub.Contains(e.Pos)) seenIn++;
                    worstSpread = Math.Max(worstSpread, Math.Abs(e.Pos.X - Game.Width / 2f));
                }
            });
            Console.WriteLine("  wave {0}: state {1}, enemies left {2}", g.Wave, g.State, g.Enemies.Count);
            Check(seenIn > 0 && (g.State == MatchState.Intermission || g.Wave > startWave), "smart bots clear a whole wave: into the rooms, the nests down, the spills dealt with");
        }
        host.Leave(""); client.Leave("");
    }
}

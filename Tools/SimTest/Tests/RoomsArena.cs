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
        bool doorsOpen = Rooms.Doors.All(d => withRooms.Solids.All(sd => !Layouts.Inside(sd, d.Pos, 30f) && !Layouts.Inside(sd, d.Pos + d.Across * Rooms.DoorPad, 20f) && !Layouts.Inside(sd, d.Pos - d.Across * Rooms.DoorPad, 20f)));
        Check(doorsOpen, "every doorway is open, with room to stand either side");
        bool wallsOk = true;
        foreach (var ld in new[] { LayoutId.Open }.Concat(Layouts.Playable().Select(x => x.Id)))
        {
            var d = Rooms.With(ld);
            wallsOk &= d.Waypoints.All(p => d.Solids.All(sd => !Layouts.Inside(sd, p, 18f)));
            wallsOk &= d.Solids.All(sd => !sd.Edge) && d.EnemyGates.Length == 0 && d.PlayerGates.Length == 0;
            wallsOk &= Rooms.Doors.All(dr => d.Waypoints.Any(w => w.DistanceTo(dr.Pos + dr.Across * Rooms.DoorPad) < 1f) && d.Waypoints.Any(w => w.DistanceTo(dr.Pos - dr.Across * Rooms.DoorPad) < 1f));
        }
        Check(wallsOk, "with every floor plan in the hub: steering spots clear of the walls, no edge gates, a spot each side of every door");
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

        // Spawns: regular enemies come in out in the rooms and hallways, not in the hub.
        var before = new HashSet<int>(g.Enemies.Keys);
        for (int i = 0; i < 60; i++)
            spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt });
        var made = g.Enemies.Values.Where(e => !before.Contains(e.Id)).ToList();
        var hub = Rooms.Areas[Rooms.Hub];
        Check(made.Count == 60 && made.All(e => !hub.Contains(e.Pos)), "regular enemies come in out in the rooms and hallways (" + made.Count(e => hub.Contains(e.Pos)) + " of 60 in the hub)");
        Check(new[] { Rooms.West, Rooms.East, Rooms.TopHall, Rooms.BottomHall }.All(a => made.Any(e => Rooms.AreaAt(e.Pos) == a)), "...in every room and hallway");
        Quiet();

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
                for (int k = 0; k < 8; k++)
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
                    V2 from = spot(fromArea), to = spot(toArea);
                    var walker = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Brute, from, 0f, 0, false });
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
                    else Console.WriteLine("  stuck: {0} {1} -> {2} {3}, got within {4}", Rooms.Areas[fromArea].Name, from, Rooms.Areas[toArea].Name, to, (int)near);
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
            Run(s, dt, 150f, () => g.Wave > startWave || g.State == MatchState.Intermission, () =>
            {
                NoPicks();
                foreach (var e in g.Enemies.Values)
                {
                    if (hub.Contains(e.Pos)) seenIn++;
                    worstSpread = Math.Max(worstSpread, Math.Abs(e.Pos.X - Game.Width / 2f));
                }
            });
            Console.WriteLine("  wave {0}: state {1}, enemies left {2}", g.Wave, g.State, g.Enemies.Count);
            Check(seenIn > 0 && (g.State == MatchState.Intermission || g.Wave > startWave), "the enemies came into the hub through the doors and the wave was cleared");
        }
        host.Leave(""); client.Leave("");
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Room fog (Docs/FogOfWar.md, Core/RoomFog.cs): a room the map gets after the core starts unseen for everyone; a player's
// line-of-sight cone (aim, stopped by walls) explores its cells for the whole team and shows what's in it; a mostly explored
// piece is seen for everyone; it's on the wire and in saves; bots scout unseen rooms.
static partial class Program
{
    static void RoomFogTest()
    {
        var core = Expansion.GridCore();
        Check(core.All(r => !RoomFog.StartsUnseen(r)), "the core never starts unseen");
        var room = new ExpansionRoom { X = 2000, Y = 0, W = 600, H = 600, Kind = Arena.RoomKind.Combat, Unseen = true };
        Check(RoomFog.StartsUnseen(room), "a built room starts unseen");
        var list = core.ToList(); list.Add(room);
        Check(RoomFog.UnseenCount(list) == 1, "the unseen count counts it");

        // Saves keep it; an old save's room (no field) is seen.
        var save = new RunSave { Wave = 6, HasExpansion = true };
        save.Seats.Add(new SeatSave());
        save.Rooms.AddRange(list);
        var back = RunSave.Parse(save.ToText());
        Check(back != null && back.Rooms.Count == list.Count && back.Rooms.Last().Unseen && back.Rooms.Take(4).All(r => !r.Unseen), "a save keeps which rooms are unseen");
        var line = save.ToText().Split('\n').Last(l => l.StartsWith("room "));
        save.Fog[4] = "FF0F";
        back = RunSave.Parse(save.ToText());
        Check(back != null && back.Fog.Count == 1 && back.Fog[4] == "FF0F", "a save keeps the explored cells");
        var old = RunSave.Parse(save.ToText().Replace(line, line.Substring(0, line.LastIndexOf(' '))));
        Check(old != null && !old.Rooms.Last().Unseen, "an old save's rooms are seen");
    }

    static void RoomFogGameTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        Check(g.Expanded.Count == 4 && g.Expanded.All(r => !r.Unseen) && cg.Expanded.All(r => !r.Unseen), "the core starts seen");

        // Two pieces: both go up unseen on both screens.
        var friend = g.Players[2];
        friend.Hand[0] = (int)Expansion.MakePieceCard(PieceShape.Domino);
        friend.Hand[1] = (int)Expansion.MakePieceCard(PieceShape.Mono);
        var a = Expansion.PlacementsFor(g.Expanded, PieceShape.Domino).First();
        cg.PlayPieceCard(0, a.Orient, a.X, a.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == 4 + a.Rooms.Count, noPicks);
        int aHead = 4, bHead = g.Expanded.Count;
        var b = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First(o => !o.Rooms.Any(r => r.X < g.Expanded[aHead].X1 + 100 && r.X1 > g.Expanded[aHead].X - 100 && r.Y < g.Expanded[aHead].Y1 + 100 && r.Y1 > g.Expanded[aHead].Y - 100));
        int slot = Array.FindIndex(friend.Hand, h => h == (int)Expansion.MakePieceCard(PieceShape.Mono));
        cg.PlayPieceCard(slot, b.Orient, b.X, b.Y);
        Run(s, dt, 1f, () => cg.Expanded.Count == bHead + b.Rooms.Count, noPicks);
        Check(g.Expanded.Count == bHead + b.Rooms.Count && g.Expanded.Skip(4).All(r => r.Unseen) && cg.Expanded.Skip(4).All(r => r.Unseen), "new pieces go up unseen, every box, on both screens");
        Check(RoomFog.UnseenCount(g.Expanded) == 2, "two unseen rooms");
        Run(s, dt, 0.3f, null, noPicks);

        // Fog hides what's inside unless a player is close; bots head for the nearest one.
        var head = g.Expanded[aHead];
        var mid = new V2((head.X + head.X1) / 2f, (head.Y + head.Y1) / 2f);
        bool far = g.Players.Values.All(p => p.Pos.FartherThan(mid, RoomFog.ConeReach));
        Check(cg.UnseenAt(mid) && (!far || cg.FogHides(mid)), "inside an unseen room is hidden from afar");
        Check(cg.NearestUnseen(g.Players[1].Pos).HasValue, "a bot has an unseen room to scout");

        // The cone: a player in the middle facing +x sees ahead (and the circle round them), not behind; walls stop it.
        var wd = g.CurrentWorld;
        var area = wd.Areas[aHead + 1];
        Check(RoomFog.Sees(wd, mid, 0f, mid + new V2(250f, 0f)) && !RoomFog.Sees(wd, mid, 0f, mid + new V2(-250f, 0f)), "the cone sees ahead, not behind");
        Check(RoomFog.Sees(wd, mid, 0f, mid + new V2(-100f, 0f)), "the close circle sees all round");
        Check(!RoomFog.Sees(wd, mid, 0f, mid + new V2(RoomFog.ConeReach + 50f, 0f)), "the cone has a reach");
        var wall = wd.Walls()[0];
        Check(RoomFog.Blocked(wd, wall.Pos - new V2(wall.HW + 40f, 0f), wall.Pos + new V2(wall.HW + 40f, 0f)) && !RoomFog.Blocked(wd, mid, mid + new V2(20f, 0f)), "a wall in the way blocks the line");
        aimOverride = mid + new V2(1000f, 0f);  // (the player faces +x)
        Action face = () => { noPicks(); g.Players[1].Pos = mid; };
        Run(s, dt, 0.6f, null, face);
        int fc = (int)((mid.X + 200f - area.Min.X) / RoomFog.Cell), fr = (int)((mid.Y - area.Min.Y) / RoomFog.Cell);
        int bc = (int)((mid.X - 260f - area.Min.X) / RoomFog.Cell);
        Check(g.Expanded[aHead].Unseen && cg.Expanded[aHead].Unseen, "a look from the middle doesn't see the whole room");
        Check(g.FogExplored(aHead, fc, fr) && !g.FogExplored(aHead, bc, fr), "the cone explores ahead and not behind");
        Check(cg.FogExplored(aHead, fc, fr) && !cg.FogExplored(aHead, bc, fr), "the friend's game has the same cells");
        Check(!g.FogHides(mid + new V2(200f, 0f)) && g.FogHides(mid + new V2(-260f, 0f)), "what the cone is on shows, what it isn't on stays hidden");

        float spin = 0f; int tick = 0;
        // (the stops are the middle, then the four corners and the four edge middles of the box, 110 px in: fins inside cast shadows, so a few spots in the middle leave the corners dark)
        V2 half = (area.Max - area.Min) * 0.5f - new V2(110f, 110f);
        var stops = new[] { V2.Zero, new V2(half.X, half.Y), new V2(-half.X, -half.Y), new V2(-half.X, half.Y), new V2(half.X, -half.Y), new V2(half.X, 0f), new V2(-half.X, 0f), new V2(0f, half.Y), new V2(0f, -half.Y) };
        // The host's player turns on the spot at a few points (fins inside cast shadows): the cone sweeps the whole piece, and it's seen on both screens; the second stays unseen.
        Run(s, dt, 24f, () => cg.Expanded.Count > aHead && !cg.Expanded[aHead].Unseen, () => { noPicks(); g.Players[1].Pos = mid + stops[(tick++ / 120) % stops.Length]; spin += 0.05f; aimOverride = mid + V2.FromAngle(spin) * 1000f; });
        var group = Expansion.GroupOf(g.Expanded, aHead);
        Check(group.All(i => !g.Expanded[i].Unseen) && group.All(i => !cg.Expanded[i].Unseen), "exploring most of it reveals the whole piece for everyone (" + group.Count + " boxes)");
        Check(g.Expanded[bHead].Unseen && cg.Expanded[bHead].Unseen, "the other room stays unseen");
        Check(!cg.UnseenAt(mid) && !cg.FogHides(mid), "inside a seen room shows");
        Check(g.Expanded.Count == bHead + b.Rooms.Count && cg.Expanded.Count == g.Expanded.Count, "a reveal doesn't rebuild the map");
        aimOverride = null;
        host.Leave(""); client.Leave("");
    }

    static void RoomFogBotTest(float dt)
    {
        // A bot with nothing to fight walks into the unseen room and reveals it.
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 1, StartWave = 6 }, port, true, "Bot", Role.Dps);
        var s = new[] { host };
        var g = host.Game;
        host.StartMatch();
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        g.DevSetGod(1, true);
        g.BotLevel = BotSkill.Good;
        g.Diff.Set(DiffKnob.ArenaExpanding, 1f);
        g.Expanded.Clear(); g.Nodes.Clear(); g.ExpandOpen = true;
        Run(s, dt, 0.6f, () => g.Expanded.Count == 4, noPicks);
        var me = g.Players.Values.First();
        me.Hand[0] = (int)Expansion.MakePieceCard(PieceShape.Mono);
        var opt = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).First();
        g.PlayPieceCard(0, opt.Orient, opt.X, opt.Y);
        Run(s, dt, 1f, () => g.Expanded.Count == 5, noPicks);
        Check(g.Expanded.Count == 5 && g.Expanded[4].Unseen, "the bot's room goes up unseen");
        Action calm = () => { noPicks(); g.Enemies.Clear(); };
        Run(s, dt, 25f, () => !g.Expanded[4].Unseen, calm);
        Check(!g.Expanded[4].Unseen, "with nothing to fight the bot scouts it");
        host.Leave("");
    }

    // Dormant groups (Core/DormantGroups.cs): each cell of a built piece may hold a sleeping group of the wave's faction enemies.
    static void DormantGroupsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(DormantGroups.GroupSize(1) == 3 && DormantGroups.GroupSize(15) == 4 && DormantGroups.GroupSize(99) == 6, "groups grow from 3 to 6 over the run");
        Check(DormantGroups.CellMids(new[] { new ExpansionRoom { X = 0, Y = 0, W = 1200, H = 600 } }).Count == 2, "a two-cell box has two cells");
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; p.InvulnT = 1f; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg })
        {
            gg.Diff.Set(DiffKnob.ArenaExpanding, 1f);
            gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true;
        }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);
        float chance = DormantGroups.CellChance;
        DormantGroups.CellChance = 1f;
        try
        {
            var friend = g.Players[2];
            friend.Hand[0] = (int)Expansion.MakePieceCardMods(PieceShape.Domino, PieceMods.Combat.Set(0, 1));
            var a = Expansion.PlacementsFor(g.Expanded, PieceShape.Domino).First();
            var before = new HashSet<int>(g.Enemies.Keys);
            cg.PlayPieceCard(0, a.Orient, a.X, a.Y);
            Run(s, dt, 1f, () => cg.Expanded.Count > 4, noPicks);
            var sleepers = g.Enemies.Values.Where(e => !before.Contains(e.Id) && e.Dormant).ToList();
            int size = DormantGroups.GroupSize(g.Wave);
            var pool = g.DormantPool(g.Wave);
            Check(sleepers.Count == 2 * size, "a Combat piece: every cell of a two-cell piece holds a group of " + size + " (" + sleepers.Count + ")");
            Check(sleepers.All(e => pool.Contains(e.Type)) && pool.All(t => !Defs.Get(t).IsBoss), "they are the wave's faction enemies (" + string.Join(",", pool) + ")");
            Check(sleepers.All(e => g.UnseenAt(e.Pos)), "they sleep in the unseen room");
            Run(s, dt, 0.4f, null, noPicks);
            Check(sleepers.All(e => cg.Enemies.ContainsKey(e.Id) && cg.Enemies[e.Id].Dormant), "the friend's game sees them asleep");
            int foes = g.Foes();
            Check(foes == g.Enemies.Values.Count(e => !e.Dormant && !Game.IsPlanted(e.Type)), "they don't count for the wave");
            var at = sleepers.ToDictionary(e => e.Id, e => e.Pos);
            Run(s, dt, 2f, null, noPicks);
            Check(sleepers.All(e => e.Dormant && e.Pos.CloserThan(at[e.Id], 1f)), "they don't move while the room is unseen");

            // One hit from the doorway wakes its room's groups.
            var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
            hurt.Invoke(g, new object[] { sleepers[0], 1f, 1 });
            Check(sleepers.All(e => !e.Dormant), "a hit wakes the room's groups");
            Run(s, dt, 0.4f, null, noPicks);
            Check(sleepers.Where(e => e.Hp > 0f).All(e => cg.Enemies.ContainsKey(e.Id) && !cg.Enemies[e.Id].Dormant), "the friend's game sees them wake");

            // A second piece: a player looking at its group from close wakes it.
            var combatMono = Expansion.MakePieceCardMods(PieceShape.Mono, PieceMods.Combat.Set(0, 1));
            friend.Hand[1] = (int)combatMono;
            int n0 = g.Expanded.Count;
            var b = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).Last();
            before = new HashSet<int>(g.Enemies.Keys);
            int slot = Array.FindIndex(friend.Hand, h => h == (int)combatMono);
            cg.PlayPieceCard(slot, b.Orient, b.X, b.Y);
            Run(s, dt, 1f, () => g.Expanded.Count > n0, noPicks);
            var second = g.Enemies.Values.Where(e => !before.Contains(e.Id) && e.Dormant).ToList();
            Check(second.Count == size, "the one-cell piece holds one group");
            var room = g.Expanded[n0];
            var mid = new V2((room.X + room.X1) / 2f, (room.Y + room.Y1) / 2f);
            // (a fin inside the room can stand in the way: take a spot with a clear line to the first sleeper)
            var look = new V2[] { new V2(100f, 0f), new V2(-100f, 0f), new V2(0f, 100f), new V2(0f, -100f), new V2(70f, 70f), new V2(-70f, -70f) }
                .Select(o => second[0].Pos + o).First(q => !RoomFog.Blocked(g.CurrentWorld, q, second[0].Pos));
            aimOverride = second[0].Pos;
            Run(s, dt, 1f, () => second.All(e => !e.Dormant), () => { noPicks(); g.Players[1].Pos = look; });
            Check(second.All(e => !e.Dormant), "looking at a group from close wakes it");

            // A plain piece holds nobody; a Combat piece holds at least one group even when no cell rolls one.
            DormantGroups.CellChance = 0f;
            foreach (bool combat in new[] { false, true })
            {
                friend.Hand[0] = (int)Expansion.MakePieceCardMods(PieceShape.Mono, combat ? PieceMods.Combat.Set(0, 1) : 0);
                int n1 = g.Expanded.Count;
                var pl = Expansion.PlacementsFor(g.Expanded, PieceShape.Mono).Last();
                var before2 = new HashSet<int>(g.Enemies.Keys);
                cg.PlayPieceCard(0, pl.Orient, pl.X, pl.Y);
                Run(s, dt, 1f, () => g.Expanded.Count > n1, noPicks);
                int slept = g.Enemies.Values.Count(e => !before2.Contains(e.Id) && e.Dormant);
                Check(combat ? slept == size : slept == 0, combat ? "a Combat piece always holds a group (" + slept + " of " + size + ")" : "a plain piece holds no enemies (" + slept + ")");
            }
        }
        finally { DormantGroups.CellChance = chance; aimOverride = null; }
        host.Leave(""); client.Leave("");
    }
}

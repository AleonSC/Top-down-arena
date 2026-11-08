using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Arena layouts (Layouts.cs, LayoutRules.cs): the six floor plans, one per boss cycle, walls and pillars that stop
// walking and every shot, edge gates (teal for players, red holograms enemies come in through), steering round walls.
static partial class Program
{
    static void LayoutsTest(float dt)
    {
        Game.LayoutsByDefault = true;
        Game.PropsByDefault = true;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var spawnEnemy = typeof(Game).GetMethod("SpawnEnemy", flags);
        var spawnEnemyAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var moveEnemy = typeof(Game).GetMethod("MoveEnemy", flags);
        var waveProps = typeof(Game).GetMethod("WaveProps", flags);
        var pick = typeof(Game).GetMethod("PickLayout", flags);

        // The floor plans themselves.
        var all = Layouts.Playable().ToList();
        Check(all.Count == 6, "six playable layouts (" + all.Count + ")");
        foreach (var d in all)
        {
            bool wpsClear = d.Waypoints.All(p => d.Solids.All(sd => !Layouts.Inside(sd, p, 18f)));
            bool cratesClear = d.Crates.All(c => d.Solids.All(sd => !Layouts.Inside(sd, c.Pos, 22f)));
            bool inBounds = d.Solids.All(sd => sd.Pos.X > -1f && sd.Pos.X < Game.Width + 1f);
            Check(wpsClear && cratesClear && inBounds, d.Name + ": waypoints and crates clear of the walls, all on the floor");
            if (d.Edge != EdgeGates.Open)
            {
                bool gatesOk = d.PlayerGates.All(gt => gt.Len >= 120f) && d.PlayerGates.Concat(d.EnemyGates).All(gt => gt.From >= 0f && gt.To <= (gt.Side < 2 ? Game.Width : Game.Height));
                bool apart = d.EnemyGates.All(f => d.PlayerGates.All(gt => gt.Side != f.Side || f.To <= gt.From - 40f || f.From >= gt.To + 40f));
                // The edge wall must cover every enemy gate (they're part of it) and leave every player gate open.
                bool wallCovers = d.EnemyGates.All(f => d.Solids.Any(sd => sd.Edge && Layouts.Inside(sd, EdgePoint(f), 0f)));
                bool gatesOpen = d.PlayerGates.All(gt => !d.Solids.Any(sd => sd.Edge && Layouts.Inside(sd, EdgePoint(gt), 0f)));
                Check(gatesOk && apart && wallCovers && gatesOpen, d.Name + ": player gates open and at least 120 px wide, enemy gates sit in the wall away from them");
            }
        }
        var funnels = Layouts.Get(LayoutId.Funnels);
        Check(!funnels.SpawnSide[0] && !funnels.SpawnSide[1] && funnels.SpawnSide[2] && funnels.SpawnSide[3], "Funnels: enemies only come in from the left and right");

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

        // A regular wave has a floor plan; everyone sees the same one, and it brings its own cover.
        Check(g.Layout != LayoutId.Open && cg.Layout == g.Layout, "wave 6 has a floor plan (" + g.Layout + ") and the client sees it");
        Check(g.Props.Values.Count(p => Game.IsCrate(p)) >= Layouts.Get(g.Layout).Crates.Length, "its cover is down: " + g.Props.Values.Count(p => Game.IsCrate(p)) + " crates");

        // A Data Gate never opens under a wall, pillar or crate (the whole disc and its ring are clear).
        {
            var state0 = g.State;
            g.State = MatchState.Intermission;
            int bad = 0;
            for (int i = 0; i < 60; i++)
            {
                g.Room = RoomPhase.None;
                g.DevOpenPortal(ChallengeKind.Defend);
                var at = g.PortalPos;
                foreach (var sol in Layouts.Get(g.Layout).Solids)
                    if (!(sol.Edge && g.Walled) && Layouts.Inside(sol, at, Challenges.PortalR + 12f)) bad++;
                foreach (var pr in g.Props.Values)
                    if (Game.Solid(pr) && pr.Hp > 0f && Game.PropCovers(pr, at, Challenges.PortalR + 12f)) bad++;
            }
            g.Room = RoomPhase.None; g.RoomKind = ChallengeKind.None; g.State = state0;
            Check(bad == 0, "a Data Gate never opens under walls, pillars or crates (" + g.Layout + ", 60 tries, " + bad + " overlaps)");
        }

        // One per cycle: waves 6-9 keep it, the boss wave is open, the next cycle is a different one.
        LayoutId first = g.Layout;
        g.Wave = 8; pick.Invoke(g, null);
        Check(g.Layout == first, "the same floor plan all cycle");
        g.Wave = 10; pick.Invoke(g, null);
        Check(g.Layout == LayoutId.Open && !g.LayoutOn, "a boss wave is the open arena");
        g.Wave = 11; pick.Invoke(g, null);
        Check(g.Layout != LayoutId.Open && g.Layout != first, "the next cycle gets a different floor plan (" + first + " then " + g.Layout + ")");
        var seen = new HashSet<LayoutId>();
        for (int cycle = 3; cycle < 60; cycle++)
        {
            g.Wave = cycle * 5 + 5; pick.Invoke(g, null);
            g.Wave = cycle * 5 + 6; pick.Invoke(g, null);
            seen.Add(g.Layout);
        }
        Check(seen.Count == 6, "every floor plan comes up over many cycles (" + seen.Count + " of 6)");
        g.LayoutsOn = false; g.Wave = 71; pick.Invoke(g, null);
        Check(g.Layout == LayoutId.Open, "with layouts off it's always the open arena");
        g.LayoutsOn = true;
        // The lobby's Arena layouts setting: off means the open arena all run, and it survives a save and the network.
        var offDiff = Difficulty.Of(DifficultyLevel.Hard);
        offDiff.Set(DiffKnob.ArenaLayouts, 0f);
        var back = Difficulty.Parse(offDiff.ToText());
        var ms = new System.IO.MemoryStream();
        offDiff.Write(new System.IO.BinaryWriter(ms));
        ms.Position = 0;
        var sent = Difficulty.Read(new System.IO.BinaryReader(ms));
        Check(Difficulty.Of(DifficultyLevel.Nightmare)[DiffKnob.ArenaLayouts] >= 0.5f && offDiff.Level == DifficultyLevel.Hard && back[DiffKnob.ArenaLayouts] < 0.5f && sent[DiffKnob.ArenaLayouts] < 0.5f,
            "the Arena layouts setting is on in every preset, doesn't make a Custom difficulty, and survives saving and sending");
        var keep = g.Diff;
        g.Diff = offDiff; g.Wave = 71; pick.Invoke(g, null);
        Check(g.Layout == LayoutId.Open && !g.LayoutOn, "with Arena layouts off in the lobby, every wave is the open arena");
        g.Diff = keep;
        g.Wave = 6;

        Quiet();
        g.Props.Clear();
        var me = g.Players.Values.First();

        // More enemies on a floor plan than in the open arena.
        var buildWave = typeof(Game).GetMethod("BuildWave", flags);
        Func<int> queued = () => ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Count
            + ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Cast<object>().Sum(q => ((SquadDef)q).Members.Length);
        g.Wave = 3;
        g.DevSetLayout(LayoutId.Open); buildWave.Invoke(g, new object[] { 3 }); int openCount = queued();
        g.DevSetLayout(LayoutId.Cross); buildWave.Invoke(g, new object[] { 3 }); int laidCount = queued();
        Console.WriteLine("  wave 3 enemies: {0} open, {1} on a floor plan", openCount, laidCount);
        Check(laidCount >= openCount * 1.2f && laidCount <= openCount * 1.45f, "a floor plan brings about " + (int)((Game.LayoutEnemyMult - 1f) * 100f) + "% more enemies");
        Quiet();
        g.Wave = 6;

        // Walls and pillars: you can't walk into them, and no shot gets through (yours or theirs).
        g.DevSetLayout(LayoutId.TwinLanes);
        V2 pushed = g.PushOutOfSolids(new V2(500f, 300f), 10f);
        Check(Math.Abs(pushed.Y - 300f) >= 22f - 0.1f, "a wall pushes you out (" + pushed.Y + ")");
        var ring = Layouts.Get(LayoutId.PillarRing);
        g.DevSetLayout(LayoutId.PillarRing);
        var pil = ring.Solids.First(x => x.Round);
        V2 pp = g.PushOutOfSolids(pil.Pos + new V2(5f, 0f), 10f);
        Check(Math.Abs(pp.DistanceTo(pil.Pos) - (pil.R + 10f)) < 0.1f, "a pillar pushes you out round its edge");
        g.DevSetLayout(LayoutId.TwinLanes);
        me.Pos = new V2(100f, 100f);
        var foe = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, new V2(500f, 200f), new V2(0f, 300f), 0f, 3f, 10f, 0, false, 0f });
        var mine = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(540f, 200f), new V2(0f, 400f), 0f, 1f, 10f, 1, false, 0f });
        float deepest = 0f;
        Run(s, dt, 0.8f, null, () => { foreach (var b in new[] { foe, mine }) if (g.Bullets.ContainsKey(b.Id)) deepest = Math.Max(deepest, b.Pos.Y); });
        Check(!g.Bullets.ContainsKey(foe.Id) && !g.Bullets.ContainsKey(mine.Id) && deepest < 330f, "a wall stops enemy shots and yours (deepest " + deepest + ")");
        var crate = g.AddProp(PropKind.Crate, new V2(800f, 560f));
        var over = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 500f), new V2(0f, 400f), 0f, 1f, 10f, 1, false, 0f });
        bool crossed = false;
        Run(s, dt, 0.5f, null, () => { if (g.Bullets.ContainsKey(over.Id) && over.Pos.Y > 600f) crossed = true; });
        Check(crossed, "your shots still fly over crates");
        g.Props.Clear();

        // Where enemies come in. The Pillar Ring walls the edges (Centre gates): every spawn is at a red gate.
        g.DevSetLayout(LayoutId.PillarRing);
        Check(g.EdgesGated, "the Pillar Ring walls the edges in");
        var rd = Layouts.Get(LayoutId.PillarRing);
        var before = new HashSet<int>(g.Enemies.Keys);
        for (int i = 0; i < 60; i++)
            spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt });
        var made = g.Enemies.Values.Where(e => !before.Contains(e.Id)).ToList();
        bool allAtGates = made.All(e => rd.EnemyGates.Any(gt => AtGate(gt, e.Pos, e.Radius + 30f)));
        Check(made.Count == 60 && allAtGates, "enemies only come in through the red gates (" + made.Count + " spawned)");
        bool bothSides = made.Any(e => e.Pos.X < 100f) && made.Any(e => e.Pos.X > Game.Width - 100f) && made.Any(e => e.Pos.Y < 100f) && made.Any(e => e.Pos.Y > Game.Height - 100f);
        Check(bothSides, "...and from every side");
        foreach (var e in made) e.Hp = 0f;
        Quiet();

        // Open edges still honour a layout's allowed sides: Funnels only sends them in from the left and right.
        g.DevSetLayout(LayoutId.Funnels);
        Check(!g.EdgesGated, "the Funnels leave the edges open");
        before = new HashSet<int>(g.Enemies.Keys);
        for (int i = 0; i < 40; i++)
            spawnEnemy.Invoke(g, new object[] { EnemyType.Grunt });
        made = g.Enemies.Values.Where(e => !before.Contains(e.Id)).ToList();
        Check(made.Count == 40 && made.All(e => e.Pos.X < 80f || e.Pos.X > Game.Width - 80f), "Funnels: every enemy comes in from the left or right");
        foreach (var e in made) e.Hp = 0f;
        Quiet();

        // Wrapping: through a gap, not through the wall.
        g.DevSetLayout(LayoutId.PillarRing);
        V2 inWall = g.PushOutOfSolids(new V2(8f, 100f), 10f);
        V2 inGap = g.PushOutOfSolids(new V2(8f, 450f), 10f);
        Check(inWall.X >= Layouts.EdgeT + 10f - 0.1f && Math.Abs(inGap.X - 8f) < 0.01f, "the edge wall holds you in; the gate lets you through (" + inWall.X + ", " + inGap.X + ")");
        g.Mods |= RunMod.SolidWalls;
        Check(g.Walled && !g.EdgesGated && Math.Abs(g.PushOutOfSolids(new V2(8f, 100f), 10f).X - 8f) < 0.01f, "Solid Walls closes the arena, so the edge gates drop away");
        g.Mods &= ~RunMod.SolidWalls;

        // Steering: a Brute behind a long wall walks round it.
        g.DevSetLayout(LayoutId.TwinLanes);
        g.Props.Clear();
        var brute = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Brute, new V2(500f, 255f), 0f, 0, false });
        brute.SpawnT = 0f; brute.Hp = brute.MaxHp = 99999f;
        V2 goal = new V2(500f, 345f);
        float closest = 9999f;
        for (int i = 0; i < 2400 && closest > 50f; i++)
        {
            moveEnemy.Invoke(g, new object[] { brute, goal, dt });
            brute.T += dt;
            closest = Math.Min(closest, brute.Pos.DistanceTo(goal));
        }
        Check(closest <= 50f, "an enemy walks round a wall to get at you (got within " + (int)closest + " px, " + (int)(brute.T) + " s)");
        brute.Hp = 0f;
        Quiet();

        // Routing: across every floor plan, enemies find a way between far-apart spots even when it takes several corners.
        {
            var rng = new Random(7);
            int tried = 0, reached = 0;
            foreach (var ld in Layouts.Playable())
            {
                g.DevSetLayout(ld.Id);
                g.Props.Clear();
                for (int k = 0; k < 12; k++)
                {
                    Func<V2> spot = () =>
                    {
                        for (int tries = 0; tries < 200; tries++)
                        {
                            var q = new V2(80f + (float)rng.NextDouble() * (Game.Width - 160f), 80f + (float)rng.NextDouble() * (Game.Height - 160f));
                            if (ld.Solids.All(sd => !Layouts.Inside(sd, q, 30f)))
                                return q;
                        }
                        return new V2(Game.Width / 2f, Game.Height / 2f);
                    };
                    V2 from = spot(), to = spot();
                    var walker = (Enemy)spawnEnemyAt.Invoke(g, new object[] { EnemyType.Brute, from, 0f, 0, false });
                    walker.SpawnT = 0f; walker.Hp = walker.MaxHp = 99999f;
                    float near = 9999f;
                    for (int i = 0; i < 3000 && near > 50f; i++)
                    {
                        moveEnemy.Invoke(g, new object[] { walker, to, dt });
                        walker.T += dt;
                        near = Math.Min(near, walker.Pos.DistanceTo(to));
                    }
                    tried++;
                    if (near <= 50f) reached++;
                    walker.Hp = 0f;
                    Quiet();
                }
            }
            Check(reached >= tried - 2, "enemies route round walls on every floor plan (" + reached + " of " + tried + " trips made it)");
        }

        // The cover of a new cycle, and random props staying out of the walls.
        g.Props.Clear();
        g.DevSetLayout(LayoutId.Islands);
        waveProps.Invoke(g, null);
        int n = g.Props.Values.Count(p => Game.IsCrate(p));
        var isl = Layouts.Get(LayoutId.Islands);
        Check(n >= isl.Crates.Length && g.Props.Values.All(p => isl.Solids.All(sd => !Layouts.Inside(sd, p.Pos, 8f))), "Islands: its crates go down (" + n + "), nothing lands in a wall");
        g.Props.Clear();
        g.DevSetLayout(LayoutId.Bunker);
        for (int w = 0; w < 6; w++) waveProps.Invoke(g, null);
        var bk = Layouts.Get(LayoutId.Bunker);
        Check(g.Props.Values.All(p => bk.Solids.All(sd => !Layouts.Inside(sd, p.Pos, 8f))), "random props keep clear of the Bunker's walls");

        // Sync: everyone sees a change.
        g.DevSetLayout(LayoutId.Cross);
        Run(s, dt, 0.5f);
        Check(cg.Layout == LayoutId.Cross, "a layout change reaches the client");
        Run(s, dt, 0.6f);
        Check(cg.LayoutAge > 0.5f, "the client's layout clock runs, so its walls fade in and are drawn (" + cg.LayoutAge.ToString("0.00") + " s)");
        // Nobody's open arena has walls.
        g.DevSetLayout(LayoutId.Open);
        Check(!g.LayoutOn && g.PushOutOfSolids(new V2(500f, 300f), 10f).DistanceTo(new V2(500f, 300f)) < 0.01f, "the open arena has no walls");
        Console.WriteLine("  protocol " + Session.ProtocolVersion);
    }

    /// The middle of a gate's mouth at the very edge (for checking the edge wall's shape).
    static V2 EdgePoint(EdgeSpan g)
    {
        return g.Side == 0 ? new V2(g.Mid, 5f) : g.Side == 1 ? new V2(g.Mid, Game.Height - 5f) : g.Side == 2 ? new V2(5f, g.Mid) : new V2(Game.Width - 5f, g.Mid);
    }

    /// Is a spawn spot (m in from the edge) along this gate?
    static bool AtGate(EdgeSpan g, V2 p, float m)
    {
        float along = g.Side < 2 ? p.X : p.Y, across = g.Side == 0 ? p.Y : g.Side == 1 ? Game.Height - p.Y : g.Side == 2 ? p.X : Game.Width - p.X;
        return Math.Abs(across - m) < 0.6f && along >= g.From - 0.1f && along <= g.To + 0.1f;
    }
}

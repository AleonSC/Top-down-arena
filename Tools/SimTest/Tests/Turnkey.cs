using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// The Turnkey (Core/Turnkey.cs): the Warden faction's trapper. It debuts on the approach's third wave, runs from you (out
// of corners too), plants Floor Mines, Shackle Traps and Mop Slicks, builds Cell Doors that let Rioters out (jammed once it
// dies), and none of what it leaves holds a wave open.
static partial class Program
{
    static void TurnkeyTest(float dt)
    {
        var NPf = BindingFlags.NonPublic | BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", NPf);
        var hurt = typeof(Game).GetMethod("HurtEnemy", NPf);
        var build = typeof(Game).GetMethod("BuildWave", NPf);
        var queueF = typeof(Game).GetField("spawnQueue", NPf);
        var squadsF = typeof(Game).GetField("squadQueue", NPf);

        // --- The roster and its debut ---------------------------------------------------------------------------------
        var tm = Approaches.For(EnemyType.Warden).Members.FirstOrDefault(m => m.Type == EnemyType.Turnkey);
        Check(tm != null && tm.Debut == 3 && tm.From == 3, "the Warden's faction has the Turnkey, from its third step, debuting on the approach's third wave");
        Check(Approaches.All.All(f => f.Members.All(m => m.Debut == 0 || m.Type == EnemyType.Turnkey)), "only the Turnkey has a debut wave");
        Func<Action<Game>, int, int, int> turnkeys = (setup, n, seed) =>
        {
            Game.SeedForTests(5150 + n * 100 + seed);
            var g0 = new Session().Game;
            setup(g0);
            build.Invoke(g0, new object[] { n });
            var all = new List<EnemyType>((List<EnemyType>)queueF.GetValue(g0));
            foreach (var q in (List<SquadDef>)squadsF.GetValue(g0))
                all.AddRange(q.Members);
            return all.Count(t => t == EnemyType.Turnkey);
        };
        Action<Game> quick = g0 => { g0.Quick = true; g0.QuickOrder = new[] { EnemyType.Warden, EnemyType.HiveQueen }; };
        Action<Game> quickLater = g0 => { g0.Quick = true; g0.QuickOrder = new[] { EnemyType.HiveQueen, EnemyType.Warden }; };
        Action<Game> plain = g0 => { g0.Diff.Set(DiffKnob.Campaign, 0f); g0.Diff.Set(DiffKnob.ApproachShare, 0.6f); };
        int q3 = 0, q12 = 0, l13 = 0, p3 = 0, hive3 = 0;
        for (int r = 0; r < 12; r++)
        {
            q3 += turnkeys(quick, 3, r) > 0 ? 1 : 0;
            q12 += turnkeys(quick, 1, r) + turnkeys(quick, 2, r);
            l13 += turnkeys(quickLater, 13, r) > 0 ? 1 : 0;
            p3 += turnkeys(plain, 3, r) > 0 ? 1 : 0;
            hive3 += turnkeys(quickLater, 3, r);
        }
        Console.WriteLine("  Turnkey in wave 3 of the Warden's approach: Quick Play {0}/12, Quick Play's second leg (wave 13) {1}/12, a plain run {2}/12; in waves 1-2: {3}; before the Hive Queen: {4}",
            q3, l13, p3, q12, hive3);
        Check(q3 == 12 && l13 == 12 && p3 == 12, "a Turnkey always comes on the third wave of the Warden's approach (Quick Play, its later legs, a plain run)");
        Check(q12 == 0 && hive3 == 0, "never before that, and not in another boss's approach");

        // --- Live: a test arena held open by a frozen anchor --------------------------------------------------------------
        var s = StartPlain(dt, 19);
        var g = s[0].Game;
        var me = g.Players[1];
        var friend = g.Players[2];
        Func<EnemyType, V2, Enemy> put = (t, at) =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, false });
            e.SpawnT = 0f;
            return e;
        };
        Enemy anchor = null;
        Action quiet = () =>
        {
            ((List<EnemyType>)queueF.GetValue(g)).Clear();
            ClearSquads(g);
            if (anchor == null) { anchor = put(EnemyType.Grunt, new V2(1500f, 80f)); anchor.Hp = anchor.MaxHp = 1e7f; }
            foreach (var e0 in g.Enemies.Values) if (e0 != anchor) e0.Hp = 0f;
            Run(s, dt, 0.2f, null, () => { anchor.FrozenT = 1f; anchor.FireT = 99f; anchor.Pos = new V2(1500f, 80f); });
            g.Bullets.Clear();
            g.Patches.Clear();
        };
        Run(s, dt, 8f, () => g.State == MatchState.Fighting);
        quiet();
        g.DevSetGod(2, true);
        Action away = () => { friend.Pos = new V2(150f, 820f); friend.VanishT = 1e6f; };
        Action hold = () => { ((List<EnemyType>)queueF.GetValue(g)).Clear(); ClearSquads(g); anchor.FrozenT = 1f; anchor.FireT = 99f; away(); };
        V2 home = new V2(800f, 450f);
        me.Pos = home; me.Hp = me.MaxHp = 5000f;

        // Runs: kept from working, it gets away from you.
        var tk = put(EnemyType.Turnkey, home + new V2(-150f, 0f));
        Run(s, dt, 2f, null, () => { hold(); me.Pos = home; tk.Phase = 1; tk.PhaseT = 99f; tk.T2 = 99f; });
        float fled = tk.Pos.DistanceTo(home);
        // ...and out of a corner, round you rather than into the walls.
        V2 corner = g.ArenaMin + new V2(30f, 30f);
        tk.Pos = corner;
        V2 meAt = corner + new V2(110f, 110f);
        Run(s, dt, 2.5f, null, () => { hold(); me.Pos = meAt; tk.Phase = 1; tk.PhaseT = 99f; tk.T2 = 99f; });
        float outOfCorner = tk.Pos.DistanceTo(corner), fromMe = tk.Pos.DistanceTo(meAt);
        Console.WriteLine("  runs: 150 -> {0:0} px from you in 2 s; from a corner with you 155 px off: {1:0} px out of it, {2:0} px from you", fled, outOfCorner, fromMe);
        Check(fled > Game.TurnkeyFlee - 30f, "a Turnkey runs from you (to about TurnkeyFlee away)");
        Check(outOfCorner > 250f && fromMe > 200f, "a cornered Turnkey slides out round you instead of pinning itself in the corner");
        tk.Hp = 0f;
        quiet();

        // Works: plants traps, builds a door that lets Rioters out; the friend's game sees it all.
        me.Pos = home;
        tk = put(EnemyType.Turnkey, home + new V2(-380f, 0f));
        int tkId = tk.Id;
        bool planted = false, built = false, clientPhase = false, clientDoor = false;
        int traps = 0, slicks = 0;
        var seenTraps = new HashSet<int>();
        var rioters = new HashSet<int>();
        Run(s, dt, 12f, null, () =>
        {
            hold();
            me.Pos = home;
            planted |= tk.Phase == 2;
            built |= tk.Phase == 3;
            foreach (var o in g.Enemies.Values)
            {
                if ((o.Type == EnemyType.FloorMine || o.Type == EnemyType.ShackleTrap) && o.ParentId == tkId && seenTraps.Add(o.Id)) traps++;
                if (o.Type == EnemyType.Rioter && g.Enemies.TryGetValue(o.ParentId, out var d) && d.Type == EnemyType.CellDoor) rioters.Add(o.Id);
            }
            slicks = Math.Max(slicks, g.Patches.Values.Count(pt => pt.Kind == PatchKind.Slick));
            Enemy ce;
            if (s[1].Game.Enemies.TryGetValue(tkId, out ce) && ce.Phase >= 2 && ce.PhaseT > 0f) clientPhase = true;
            clientDoor |= s[1].Game.Enemies.Values.Any(o => o.Type == EnemyType.CellDoor);
        });
        var doors = g.Enemies.Values.Where(o => o.Type == EnemyType.CellDoor).ToList();
        int foes = g.Foes(), counted = g.Enemies.Values.Count(o => !o.Dummy && !Game.IsPlanted(o.Type));
        Console.WriteLine("  works for 12 s: planted {0}, built {1}; {2} traps, {3} slicks, {4} doors, {5} Rioters let out; Foes {6} (of {7} enemies)",
            planted, built, traps, slicks, doors.Count, rioters.Count, foes, g.Enemies.Count);
        Check(planted && traps + slicks >= 2, "it stops to plant traps (mines, shackles, slicks) as it goes");
        Check(built && doors.Count >= 1 && rioters.Count >= 1, "it builds a Cell Door, and the door lets Rioters out");
        Check(doors.All(d => d.ParentId == tkId) && rioters.Count <= Game.DoorKids * Math.Max(1, doors.Count) + 2, "a door's Rioters are capped");
        Check(foes == counted && doors.Count > 0, "doors and traps don't hold the wave open (Foes skips them)");
        Check(clientPhase && clientDoor, "the friend's game sees the Turnkey working and its door");

        // Its death jams its doors: no more Rioters.
        hurt.Invoke(g, new object[] { tk, 1e6f, 1 });
        Run(s, dt, 0.5f, null, () => { hold(); me.Pos = home; });
        int before = rioters.Count;
        Run(s, dt, 6f, null, () =>
        {
            hold();
            me.Pos = home;
            foreach (var o in g.Enemies.Values)
                if (o.Type == EnemyType.Rioter && g.Enemies.TryGetValue(o.ParentId, out var d) && d.Type == EnemyType.CellDoor) rioters.Add(o.Id);
        });
        bool jammed = doors.All(d => !g.Enemies.ContainsKey(d.Id) || g.Enemies[d.Id].Phase == 2);
        Check(!g.Enemies.ContainsKey(tkId) && jammed && rioters.Count == before, "with its Turnkey dead a door jams shut and lets no more out");
        quiet();

        // A Floor Mine: arms, beeps when you step close, blows up.
        me.Pos = home;
        var mine = put(EnemyType.FloorMine, home + new V2(0f, -120f));
        int mineId = mine.Id;
        Run(s, dt, Game.FloorMineArm + 0.1f, null, () => { hold(); me.Pos = home; });
        bool armed = mine.Phase == 1;
        float hp0 = me.Hp;
        Run(s, dt, 1.5f, () => !g.Enemies.ContainsKey(mineId), () => { hold(); me.Pos = home + new V2(0f, -120f + Game.FloorMineTrigger); });
        Run(s, dt, 0.2f, null, () => { hold(); });
        Console.WriteLine("  Floor Mine: armed {0}, blew up {1}, you lost {2:0}", armed, !g.Enemies.ContainsKey(mineId), hp0 - me.Hp);
        Check(armed && !g.Enemies.ContainsKey(mineId) && me.Hp < hp0, "a Floor Mine arms, then blows up when you step close");

        // A Shackle Trap: snaps shut and holds you.
        me.Pos = home;
        var snare = put(EnemyType.ShackleTrap, home + new V2(120f, 0f));
        int snareId = snare.Id;
        Run(s, dt, Game.SnareArm + 0.1f, null, () => { hold(); me.Pos = home; });
        me.InvulnT = 0f;
        bool held = false;
        Run(s, dt, 0.3f, () => me.StunT > 0f, () => { hold(); me.Pos = home + new V2(120f, 0f); });
        held = me.StunT > 0f;
        Run(s, dt, Game.SnareLinger + 0.3f, null, () => { hold(); });
        Check(held && !g.Enemies.ContainsKey(snareId), "a Shackle Trap snaps shut on you (rooted), then lets go and is gone");

        // A Mop Slick slows you while you stand in it.
        g.Patches.Clear();
        typeof(Game).GetMethod("SpawnPatchAt", NPf).Invoke(g, new object[] { PatchKind.Slick, home, Game.SlickWarn, Game.SlickWarn + Game.SlickLife, Game.SlickRadius, Game.SlickRadius });
        Run(s, dt, Game.SlickWarn + 0.1f, null, () => { hold(); me.Pos = home; });
        var slowAt = typeof(Game).GetMethod("HoneySlowAt", NPf);
        float inside = (float)slowAt.Invoke(g, new object[] { home }), outside = (float)slowAt.Invoke(g, new object[] { home + new V2(200f, 0f) });
        Check(Math.Abs(inside - Game.SlickSlow) < 0.01f && outside == 1f && s[1].Game.Patches.Values.Any(pt => pt.Kind == PatchKind.Slick), "a Mop Slick slows you inside it (and the friend's game has it)");

        // The wave cleared: what it left behind goes.
        put(EnemyType.CellDoor, home + new V2(-200f, 0f));
        put(EnemyType.FloorMine, home + new V2(-250f, 60f));
        typeof(Game).GetMethod("ClearPods", NPf).Invoke(g, new object[0]);
        Check(!g.Enemies.Values.Any(o => Game.IsPlanted(o.Type)), "a cleared wave takes its doors and traps with it");
        foreach (var h in s) h.Leave("");
    }
}

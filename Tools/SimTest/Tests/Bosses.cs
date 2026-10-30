using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Bosses: the Warden, the Singularity, boss sound cues, and every late boss's mechanics.
static partial class Program
{
    // The Warden (Warden.cs): a prison riot in three stages by health, one mechanic per turn.
    /// The Twin Reapers' three stages (Reapers.cs): Twilight, the fused Dual Reaper, then Sundered.
    static void ReapersTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var s = StartBuffed(dt, 15, 0);
        var g = s[0].Game; var cg = s[1].Game;
        g.Bot = false; cg.Bot = false;
        g.DevSetGod(1, true);
        Run(s, dt, 15, () => { var a0 = Find(s[0], EnemyType.TwinA); var b0 = Find(s[0], EnemyType.TwinB); return a0 != null && b0 != null && a0.SpawnT <= 0f && b0.SpawnT <= 0f; });
        var me = g.Players[1]; var friend = g.Players[2];
        Action<V2> PutFriend = pos => { friend.Pos = pos; cg.Players[2].Pos = pos; };
        Action Park = () => { me.Pos = new V2(60f, 840f); };
        var a = Find(s[0], EnemyType.TwinA); var b = Find(s[0], EnemyType.TwinB);
        Check(a.MaxHp > 0f && Defs.Get(EnemyType.TwinA).Hp == 2600f, "the Twin Reapers have 2600 health each");

        // Twilight: one turn at a time; the tether only burns on its sweep, which follows its target.
        var seen = new List<ReaperTurn>();
        float tetherDmg = 0f; float sweepGap0 = -1f, sweepGap1 = 0f;
        Run(s, dt, 21f, null, () =>
        {
            Park(); friend.Hp = friend.MaxHp;
            var R = g.Reapers; if (R == null) return;
            if (seen.Count == 0 || seen[seen.Count - 1] != R.Turn) seen.Add(R.Turn);
            a.TargetId = b.TargetId = 2;
            if (R.Turn == ReaperTurn.Sweep)
            {
                if (sweepGap0 < 0f) { PutFriend(new V2(1300f, 600f)); sweepGap0 = R.SweepC.DistanceTo(friend.Pos); }
                else PutFriend(new V2(1300f, 600f));
                sweepGap1 = R.SweepC.DistanceTo(friend.Pos);
            }
            else PutFriend(new V2(1500f, 820f));
        });
        Console.WriteLine("  reapers twilight turns: " + string.Join(", ", seen) + "; sweep centre to the friend " + sweepGap0.ToString("0") + " -> " + sweepGap1.ToString("0"));
        Check(seen.Count >= 6 && seen[0] == ReaperTurn.Sweep && seen[2] == ReaperTurn.Lines && seen[4] == ReaperTurn.Cross, "Twilight: tether sweep, lines, cross, with breathers between");
        Check(sweepGap1 < sweepGap0 - 100f, "the tether sweep follows its target");
        Check(cg.Reapers != null && cg.Reapers.Stage == ReaperStage.Twilight, "clients see the Reapers' fight");
        // Stand on the tether during a sweep.
        Run(s, dt, 30f, () => g.Reapers.Turn == ReaperTurn.Sweep && g.Reapers.TurnT > 1f, Park);
        float t0 = friend.Run.DamageTaken;
        Run(s, dt, 1f, null, () => { Park(); PutFriend(V2.Lerp(a.Pos, b.Pos, 0.5f)); foreach (var bl in new List<Bullet>(g.Bullets.Values)) if (Defs.IsEnemyShot(bl.Kind)) g.Bullets.Remove(bl.Id); });
        tetherDmg = friend.Run.DamageTaken - t0;
        Check(tetherDmg > 0f, "the lit tether hurts");

        // The fusion at 65%: they can't be hurt, then one Dual Reaper with their health; the power-up blasts.
        a.Hp = a.MaxHp * 0.6f; b.Hp = b.MaxHp * 0.6f;
        float maxSum = a.MaxHp + b.MaxHp, hpSum = a.Hp + b.Hp;
        Run(s, dt, 0.2f, null, Park);
        float ha = a.Hp; hurt.Invoke(g, new object[] { a, 300f, 1 });
        bool immune = g.Reapers.Trans == 1 && a.Hp == ha;
        float tf = friend.Run.DamageTaken;
        Run(s, dt, 4.6f, () => g.Reapers.Trans == 0 && g.Reapers.Stage == ReaperStage.Dual, () => { Park(); PutFriend(new V2(1200f, 700f)); friend.Hp = friend.MaxHp; });
        var d = Find(s[0], EnemyType.DualReaper);
        float blastDmg = friend.Run.DamageTaken - tf;
        Console.WriteLine("  reapers fusion: immune {0}, dual {1}, health {2:0}/{3:0} (twins had {4:0}/{5:0}), power-up hit the friend for {6:0}", immune, d != null, d?.Hp, d?.MaxHp, hpSum, maxSum, blastDmg);
        Check(immune && d != null && Find(s[0], EnemyType.TwinA) == null && Math.Abs(d.MaxHp - maxSum) < 1f && Math.Abs(d.Hp - hpSum) < 20f, "at 65% they fuse into the Dual Reaper (can't be hurt meanwhile), keeping their health");
        Check(blastDmg >= 39.5f, "the power-up blasts hurt whoever stays in a marked circle");
        Run(s, dt, 1f, null, Park);
        int pillars = g.Enemies.Values.Count(e => e.Type == EnemyType.ReaperPillar);
        Check(g.Reapers.Turn == ReaperTurn.Lasers && pillars == 4 && cg.Enemies.Values.Count(e => e.Type == EnemyType.ReaperPillar) == 4, "the Dual Reaper fires its lasers and raises 4 pillars (everyone sees them)");
        float hd = d.Hp; hurt.Invoke(g, new object[] { d, 100f, 0 });
        Check(Math.Abs(hd - d.Hp - 35f) < 1f, "the Dual Reaper is armored (a third of the damage)");

        // Leave the pillars standing: it pulls everyone in, then blows.
        bool pulled = false; tf = friend.Run.DamageTaken;
        Run(s, dt, 14f, () => g.Reapers.TurnT < 1f && pulled, () => { Park(); PutFriend(new V2(900f, 450f)); friend.Hp = friend.MaxHp; pulled |= cg.PullStrength > 0f; });
        Console.WriteLine("  reapers pillars stood: pulled on the client {0}, friend took {1:0}", pulled, friend.Run.DamageTaken - tf);
        Check(pulled && friend.Run.DamageTaken - tf >= 40f, "pillars left standing: it pulls you in and blows up");

        // Kill the pillars: holes, then the chase.
        foreach (var e in g.Enemies.Values) if (e.Type == EnemyType.ReaperPillar) e.Hp = 0f;
        Run(s, dt, 3f, () => g.Reapers.Turn == ReaperTurn.Chase, Park);
        Check(g.Reapers.Holes.Count == 4 && g.Reapers.Turn == ReaperTurn.Chase && cg.Reapers.Holes.Count == 4, "every pillar killed leaves a hole; then it chases you");
        V2 dp = d.Pos; tf = friend.Run.DamageTaken;
        Run(s, dt, 1.5f, null, () => { Park(); PutFriend(d.Pos + new V2(0f, 100f)); friend.Hp = friend.MaxHp; });
        Check(friend.Run.DamageTaken - tf >= 26f, "its scythes hit on both sides as it chases");

        // Into a hole: shield, then open (extra damage), then back to the middle and round again.
        d.Pos = g.Reapers.Holes[0];
        Run(s, dt, 0.2f, null, Park);
        Check(g.Reapers.Trapped && cg.Reapers.Trapped, "lured over a hole, it falls in (everyone sees it)");
        hd = d.Hp; hurt.Invoke(g, new object[] { d, 100f, 0 });
        bool shieldTook = d.Hp == hd;
        hurt.Invoke(g, new object[] { d, d.MaxHp, 0 });
        hd = d.Hp; hurt.Invoke(g, new object[] { d, 100f, 0 });
        Check(shieldTook && Math.Abs(hd - d.Hp - 150f) < 1f && g.Reapers.Open > 0f, "its shield takes hits until broken, then it's open and takes extra damage");
        Run(s, dt, 6f, () => g.Reapers.Turn == ReaperTurn.Lasers && g.Reapers.Leap <= 0f && !g.Reapers.Trapped, Park);
        Run(s, dt, 0.8f, null, Park);
        Check(g.Reapers.Holes.Count == 0 && d.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 10f && g.Enemies.Values.Count(e => e.Type == EnemyType.ReaperPillar) == 4,
            "then it leaps back to the middle, the holes close, and new pillars rise");

        // The split at 30%: Dusk and Dawn again, sharing what was left.
        d.Hp = d.MaxHp * 0.29f; float dualHp = d.Hp;
        Run(s, dt, 2.8f, () => g.Reapers.Trans == 0 && g.Reapers.Stage == ReaperStage.Sundered, Park);
        a = Find(s[0], EnemyType.TwinA); b = Find(s[0], EnemyType.TwinB);
        Check(a != null && b != null && Find(s[0], EnemyType.DualReaper) == null && Math.Abs(a.Hp + b.Hp - dualHp) < 20f && g.Enemies.Values.Count(e => e.Type == EnemyType.ReaperPillar) == 0,
            "at 30% it splits back into Dusk and Dawn, sharing its health");

        // Sundered: reap, spiral, crossfire.
        seen.Clear(); bool marked = false; tf = friend.Run.DamageTaken;
        Run(s, dt, 22f, null, () =>
        {
            Park(); friend.Hp = friend.MaxHp;
            if (a.Hp < a.MaxHp * 0.2f) a.Hp = a.MaxHp * 0.25f;
            if (b.Hp < b.MaxHp * 0.2f) b.Hp = b.MaxHp * 0.25f;
            a.TargetId = b.TargetId = 2; a.RetargetT = b.RetargetT = 99f;
            if (g.Reapers.Turn == ReaperTurn.Reap) PutFriend(new V2(900f, 600f)); else PutFriend(new V2(1500f, 820f));
            marked |= cg.Reapers.ReapA.Phase == 1 || cg.Reapers.ReapB.Phase == 1;
            if (seen.Count == 0 || seen[seen.Count - 1] != g.Reapers.Turn) seen.Add(g.Reapers.Turn);
        });
        Console.WriteLine("  reapers sundered turns: " + string.Join(", ", seen) + "; reaps marked on the client " + marked);
        Check(seen.Count >= 5 && seen[0] == ReaperTurn.Reap && seen[2] == ReaperTurn.Spiral && seen[4] == ReaperTurn.Crossfire, "Sundered: reap, twin spiral, then crossfire");
        Check(marked, "each reap is marked first (everyone sees it)");
        s[0].Leave(""); s[1].Leave("");
    }

    /// The Hive Queen's three stages (Queen.cs): Swarm, Honeycomb, Frenzy, then a last stand.
    static void QueenTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var blocks = typeof(Game).GetMethod("BlocksShot", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var s = StartBuffed(dt, 10, 0);
        var g = s[0].Game; var cg = s[1].Game;
        g.Bot = false; cg.Bot = false;
        g.DevSetGod(1, true);
        Run(s, dt, 12, () => { var b = Find(s[0], EnemyType.HiveQueen); return b != null && b.SpawnT <= 0f; });
        var q = Find(s[0], EnemyType.HiveQueen);
        var me = g.Players[1]; var friend = g.Players[2];
        Action KillDrones = () => { foreach (var e in g.Enemies.Values) if (e.Type == EnemyType.Shard) e.Hp = 0f; };
        Action Park = () => { me.Pos = new V2(60f, 840f); };
        Action<V2> PutFriend = pos => { friend.Pos = pos; cg.Players[2].Pos = pos; };  // the friend's own game moves them

        // Swarm: one turn at a time.
        var seen = new List<QueenTurn>();
        Run(s, dt, 22f, null, () =>
        {
            KillDrones(); Park();
            PutFriend(new V2(1400f, 800f)); friend.Hp = friend.MaxHp;
            var t = q.Queen.Turn;
            if (seen.Count == 0 || seen[seen.Count - 1] != t) seen.Add(t);
        });
        Console.WriteLine("  queen swarm turns: " + string.Join(", ", seen));
        Check(seen.Count >= 8 && seen[0] == QueenTurn.Drones && seen[1] == QueenTurn.Rest && seen[2] == QueenTurn.Bombers && seen[4] == QueenTurn.Fan && seen[6] == QueenTurn.Rings,
            "Swarm: drones, bombers, aimed fan, orb rings, with a breather between each");
        Check(cg.Enemies.ContainsKey(q.Id) && cg.Enemies[q.Id].Queen != null && cg.Enemies[q.Id].Queen.Stage == QueenStage.Swarm, "clients see the Hive Queen's stage");

        // Honeycomb at 65%: 2 s where she can't be hurt, the screen cleared.
        q.Hp = q.MaxHp * 0.64f;
        Run(s, dt, 0.2f, null, Park);
        float during = q.Hp;
        hurt.Invoke(g, new object[] { q, 500f, 1 });
        Check(q.Queen.TransT > 0f && q.Hp == during && (bool)blocks.Invoke(g, new object[] { q, q.Pos + new V2(0f, 200f) })
            && g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)) == 0 && g.Enemies.Values.Count(e => e.Type == EnemyType.Shard || e.Type == EnemyType.HiveBomber) == 0,
            "at 65% she changes stage: she can't be hurt for 2 s and the screen is cleared");
        Run(s, dt, 2.2f, null, Park);
        Check(q.Queen.Stage == QueenStage.Honeycomb && q.Queen.Turn == QueenTurn.Eggs, "then Honeycomb: she lays eggs");

        // Eggs: break two, let two hatch into drones.
        Run(s, dt, 1f, null, Park);
        var eggs = g.Enemies.Values.Where(e => e.Type == EnemyType.HiveEgg).ToList();
        bool clientEggs = cg.Enemies.Values.Count(e => e.Type == EnemyType.HiveEgg) == eggs.Count;
        for (int i = 0; i < 2 && i < eggs.Count; i++) eggs[i].Hp = 0f;
        Run(s, dt, 6f, () => q.Queen.Turn != QueenTurn.Eggs, () => { Park(); foreach (var p in g.Players.Values) p.Hp = p.MaxHp; });
        Run(s, dt, 0.1f, null, Park);
        int hatched = g.Enemies.Values.Count(e => e.Type == EnemyType.Shard), eggsLeft = g.Enemies.Values.Count(e => e.Type == EnemyType.HiveEgg);
        Console.WriteLine("  queen eggs: {0} laid (client saw them {1}), 2 broken, {2} drones hatched, {3} eggs left", eggs.Count, clientEggs, hatched, eggsLeft);
        Check(eggs.Count == 4 && clientEggs && hatched == 4 && eggsLeft == 0, "4 eggs; the ones you don't break hatch into 2 drones each");

        // Sealing cells: she holds still; standing still in your cell gets you hurt; everyone sees the cells.
        Run(s, dt, 3f, () => q.Queen.Turn == QueenTurn.Cells, () => { KillDrones(); Park(); });
        V2 at = q.Pos; bool moved = false, clientCells = false;
        float taken = friend.Run.DamageTaken;
        Run(s, dt, 6.2f, () => q.Queen.Turn != QueenTurn.Cells, () =>
        {
            KillDrones(); Park();
            PutFriend(new V2(800f, 600f)); friend.Hp = friend.MaxHp;
            moved |= q.Pos.DistanceTo(at) > 1f;
            clientCells |= cg.Enemies[q.Id].Queen.Cells.Count > 0;
        });
        float cellDmg = friend.Run.DamageTaken - taken;
        Console.WriteLine("  queen cells: friend standing still took {0:0}, she moved {1}, client saw cells {2}", cellDmg, moved, clientCells);
        Check(cellDmg >= 18f && !moved && clientCells, "sealing cells hurt whoever stands in them (everyone sees them); she holds still");

        // Nectar spray: honey globs, and puddles that slow you on your own game.
        Run(s, dt, 3f, () => q.Queen.Turn == QueenTurn.Spray, () => { KillDrones(); Park(); });
        var honeyIds = new HashSet<int>(); int globs = 0; var seenGlob = new HashSet<int>();
        Run(s, dt, 5.5f, () => q.Queen.Turn != QueenTurn.Spray, () =>
        {
            KillDrones(); Park();
            PutFriend(new V2(1500f, 80f));
            foreach (var pt in cg.Patches.Values) if (pt.Kind == PatchKind.Honey) honeyIds.Add(pt.Id);
            foreach (var b in g.Bullets.Values) if (b.Kind == Defs.HoneyShot && seenGlob.Add(b.Id)) globs++;
        });
        var puddle = cg.Patches.Values.FirstOrDefault(pt => pt.Kind == PatchKind.Honey && pt.Active);
        float slow = puddle == null ? 1f : (float)typeof(Game).GetMethod("HoneySlowAt", flags).Invoke(cg, new object[] { puddle.Pos });
        Console.WriteLine("  queen spray: {0} globs, {1} honey puddles on the client, slow there x{2:0.00}", globs, honeyIds.Count, slow);
        Check(globs >= 30 && honeyIds.Count >= 8 && slow < 0.6f, "the nectar spray leaves honey puddles that slow you (seen on client)");

        // Frenzy at 30%: the swarm cloud hunts the nearest player.
        q.Hp = q.MaxHp * 0.29f;
        Run(s, dt, 2.6f, null, () => { KillDrones(); Park(); });
        Check(q.Queen.Stage == QueenStage.Frenzy && q.Queen.Turn == QueenTurn.Cloud && q.Queen.CloudOn, "at 30%: Frenzy, starting with the swarm cloud");
        taken = friend.Run.DamageTaken;
        bool clientCloud = false; float cloudGap = 999f;
        me.Pos = new V2(60f, 840f);
        Run(s, dt, 3f, null, () =>
        {
            KillDrones(); Park();
            PutFriend(new V2(900f, 600f)); friend.Hp = friend.MaxHp;
            clientCloud |= cg.Enemies[q.Id].Queen.CloudOn;
            cloudGap = q.Queen.Cloud.DistanceTo(friend.Pos);
        });
        float beeDmg = friend.Run.DamageTaken - taken;
        Run(s, dt, 8f, () => q.Queen.Turn != QueenTurn.Cloud, () => { KillDrones(); Park(); });
        Console.WriteLine("  queen cloud: reached the friend (gap {0:0}), stinging {1:0}; client saw it {2}; turn over {3}", cloudGap, beeDmg, clientCloud, q.Queen.Turn != QueenTurn.Cloud);
        Check(cloudGap < 20f && beeDmg > 0f && clientCloud && q.Queen.Turn != QueenTurn.Cloud, "the swarm cloud hunts a player who stands still and stings, then flies home");

        // Royal dive: her shadow lands on her target, she can't be hurt in the air, a ring where she lands.
        Run(s, dt, 3f, () => q.Queen.Turn == QueenTurn.Dive, Park);
        taken = friend.Run.DamageTaken;
        bool immuneInAir = false, clientDive = false; int rings = 0;
        int landedNear = 0;
        Run(s, dt, 6.5f, () => q.Queen.Turn != QueenTurn.Dive, () =>
        {
            KillDrones(); Park();
            q.TargetId = 2; q.RetargetT = 99f;
            PutFriend(new V2(1000f, 500f)); friend.Hp = friend.MaxHp;
            if (Game.QueenAir(q.Queen) > 0.8f && !immuneInAir) { float h0 = q.Hp; hurt.Invoke(g, new object[] { q, 50f, 1 }); immuneInAir = q.Hp == h0; }
            clientDive |= cg.Enemies[q.Id].Queen.Turn == QueenTurn.Dive;
            if (q.Queen.Landed && q.Queen.DiveT < 1.32f) { rings++; if (q.Queen.Shadow.DistanceTo(friend.Pos) < 30f) landedNear++; }
        });
        float diveDmg = friend.Run.DamageTaken - taken;
        Console.WriteLine("  queen dive: friend took {0:0}, immune in the air {1}, landings near the target {2}, client saw it {3}", diveDmg, immuneInAir, landedNear, clientDive);
        Check(diveDmg >= 25f && immuneInAir && landedNear >= 2 && clientDive, "the royal dive lands on its target (can't be hurt in the air)");

        // Bomber barrage: two per player per volley (one leads you).
        Run(s, dt, 3f, () => q.Queen.Turn == QueenTurn.Barrage, Park);
        var bomberIds = new HashSet<int>();
        Run(s, dt, 0.75f, null, () => { Park(); PutFriend(new V2(1000f, 500f)); foreach (var e in g.Enemies.Values) if (e.Type == EnemyType.HiveBomber) bomberIds.Add(e.Id); });
        Console.WriteLine("  queen barrage: {0} bombers in the first volley", bomberIds.Count);
        Check(bomberIds.Count == 4, "the barrage sends two bombers at each player");

        // Last stand below 10%.
        q.Hp = q.MaxHp * 0.09f;
        Run(s, dt, 4f, () => q.Queen.Stage == QueenStage.LastStand && q.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 10f, Park);
        Check(q.Queen.Stage == QueenStage.LastStand && q.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 10f, "below 10%: the last stand, in the middle");
        s[0].Leave(""); s[1].Leave("");
    }

    static void WardenTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var blocks = typeof(Game).GetMethod("BlocksShot", flags);
        var s = StartBuffed(dt, 5, 0);
        var g = s[0].Game; var cg = s[1].Game;
        g.Bot = false; cg.Bot = false;  // the players stand where the test puts them
        g.DevSetGod(1, true);
        Run(s, dt, 12, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
        var w = Find(s[0], EnemyType.Boss);
        var me = g.Players[1]; var friend = g.Players[2];
        Action Clear = () => { foreach (var e in g.Enemies.Values) if (e.Type == EnemyType.Grunt) e.Hp = 0f; };
        Check(w != null && Math.Abs(w.Pos.X - Game.Width / 2f) < 5f && Math.Abs(w.Pos.Y - Game.Height / 2f) < 5f, "the Warden appears in the middle of the arena");

        // Count: its turns in order, holding still for the spiral.
        var seen = new List<WardenTurn>();
        V2 spiralAt = V2.Zero; bool spiralMoved = false;
        Run(s, dt, 27f, null, () =>
        {
            Clear();
            var t = w.Warden.Turn;
            if (seen.Count == 0 || seen[seen.Count - 1] != t) { seen.Add(t); if (t == WardenTurn.Spiral) spiralAt = w.Pos; }
            if (t == WardenTurn.Spiral && w.Pos.DistanceTo(spiralAt) > 1f) spiralMoved = true;
            me.Pos = new V2(200f, 800f); friend.Pos = new V2(1400f, 800f);
        });
        Console.WriteLine("  warden count turns: " + string.Join(", ", seen));
        Check(seen.Count >= 7 && seen[0] == WardenTurn.Rings && seen[2] == WardenTurn.Spiral && seen[4] == WardenTurn.Fan && seen[6] == WardenTurn.Light && seen[1] == WardenTurn.Rest,
            "Count: orb rings, spiral, aimed fan, searchlight, with a breather between each");
        Check(!spiralMoved, "the Warden holds still while it spirals");
        Check(cg.Enemies.ContainsKey(w.Id) && cg.Enemies[w.Id].Warden != null && cg.Enemies[w.Id].Warden.Stage == WardenStage.Count, "clients see the Warden's stage");

        // The searchlight: a stun shot stops you, the light catches you, a shackle ring drops round you.
        while (w.Warden.Turn == WardenTurn.Light) { Clear(); Step(s[0], dt); Step(s[1], dt); }  // (the first light turn may already be under way)
        while (w.Warden.Turn != WardenTurn.Light) { Clear(); me.Pos = new V2(60f, 60f); Step(s[0], dt); Step(s[1], dt); }
        float hp0 = w.Hp;
        int shackleBefore = g.Bullets.Values.Count(b => b.Kind == Defs.BigShot);
        friend.Pos = new V2(1400f, 800f);
        bool stunned = false, clientStunned = false, caught = false;
        V2 clientAt = V2.Zero;
        int ringOrbs = 0;
        Run(s, dt, 8f, () => caught && w.Warden.LightMode == WardenLight.Ring, () =>
        {
            Clear();
            w.TargetId = 2; w.RetargetT = 99f;  // the light hunts the friend (the host is in god mode)
            me.Pos = new V2(60f, 60f);
            var t = g.Players[w.TargetId];
            foreach (var b in g.Bullets.Values) if (b.Kind == Defs.StunShot) { b.Pos = t.Pos; break; }  // make the next stun shot land
            stunned |= t.StunT > 0f;
            if (cg.Players[t.Id].StunT > 0f && t.Id == 2) { if (!clientStunned) clientAt = cg.Players[2].Pos; clientStunned = true; }
            caught |= w.Warden.LightMode == WardenLight.Lock;
        });
        int ring = w.Warden.Shackle.Count;
        Console.WriteLine("  warden searchlight: stunned {0}, caught {1}, ring orbs {2}, target {3}", stunned, caught, ring, w.TargetId);
        Check(stunned && caught && ring >= 20, "a stun shot stops you, the searchlight catches you, and a shackle ring closes in");
        Check(clientStunned, "the stun reaches the stunned player's own game");
        Run(s, dt, 20f, () => w.Warden.Turn != WardenTurn.Light, Clear);
        Check(w.Warden.Turn != WardenTurn.Light, "the searchlight turn ends once the ring has closed");

        // Lockdown at 65%: 2 s where it can't be hurt, the screen cleared.
        w.Hp = w.MaxHp * 0.64f;
        Run(s, dt, 0.2f, null, Clear);
        float during = w.Hp;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        hurt.Invoke(g, new object[] { w, 500f, 1 });
        Check(w.Warden.TransT > 0f && w.Hp == during && (bool)blocks.Invoke(g, new object[] { w, w.Pos + new V2(0f, 200f) }) && g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)) == 0,
            "at 65% it changes stage: it can't be hurt for 2 s and the screen is cleared");
        Run(s, dt, 2.2f, null, Clear);
        Check(w.Warden.Stage == WardenStage.Lockdown && w.Warden.Turn == WardenTurn.Cross && cg.Enemies[w.Id].Warden.Stage == WardenStage.Lockdown, "then Lockdown: the cross (everyone sees it)");

        // The gates: everyone sees them; one shoves the friend (on the friend's own game) and hurts once, or twice when it rolls over them.
        int gates = 0, gas = 0; float taken0 = friend.Run.DamageTaken; bool clientGate = false;
        var seenGas = new HashSet<int>();
        float friendStart = 0f; bool startSet = false, pushed = false;
        Run(s, dt, 11.5f, null, () =>
        {
            Clear();
            me.Pos = new V2(60f, 60f);
            gates = Math.Max(gates, w.Warden.Gates.Count);
            clientGate |= cg.Enemies[w.Id].Warden.Gates.Count > 0;
            foreach (var pt in g.Patches.Values) if (pt.Kind == PatchKind.Gas) seenGas.Add(pt.Id);
            if (!startSet && w.Warden.Gates.Count > 0) { cg.Players[2].Pos = new V2(800f, 700f); friendStart = 700f; startSet = true; }
            if (startSet && cg.Players[2].Pos.Y > friendStart + 30f) pushed = true;
        });
        float gateDmg = friend.Run.DamageTaken - taken0;
        Console.WriteLine("  warden lockdown: {0} gates at once, {1} gas clouds, friend pushed {2}, friend took {3:0} (gates and gas)", gates, seenGas.Count, pushed, gateDmg);
        Check(gates >= 1 && clientGate && seenGas.Count == 2, "Lockdown: gates sweep across (everyone sees them) with gas from the 1st and 3rd");
        Check(pushed && gateDmg > 0f, "a gate shoves you along on your own game, and hurts");

        // Riot at 30%: doors burst, rioters run at the nearest, the Warden included; the shield blocks from the front.
        w.Hp = w.MaxHp * 0.29f;
        Run(s, dt, 2.6f, null, Clear);
        Check(w.Warden.Stage == WardenStage.Riot && w.Warden.Turn == WardenTurn.Rioters, "at 30%: the riot");
        Run(s, dt, 0.6f, null, Clear);
        int rioters = g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter);
        V2 front = w.Pos + V2.FromAngle(w.Warden.Aux) * 200f, behind = w.Pos - V2.FromAngle(w.Warden.Aux) * 200f;
        Check(rioters >= 6 && (bool)blocks.Invoke(g, new object[] { w, front }) && !(bool)blocks.Invoke(g, new object[] { w, behind }),
            "the doors burst open (" + rioters + " rioters) and the riot shield stops shots from the front only");
        me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f);
        foreach (var r in g.Enemies.Values) if (r.Type == EnemyType.Rioter) r.Pos = w.Pos + new V2(80f, 0f);
        float hpR = w.Hp;
        Run(s, dt, 1f, null, () => { me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f); });
        Check(w.Hp < hpR, "rioters that reach the Warden hurt it");

        // Rioters still standing at 10 s bring the guards: they beat the rioters down, then come for the players.
        int before = 0;
        Run(s, dt, 12f, () => g.Enemies.Values.Any(e => e.Type == EnemyType.Guard), () =>
        {
            me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f); cg.Players[2].Pos = friend.Pos;
            before = Math.Max(before, g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter));  // the second two doors burst at 3 s
            foreach (var r in g.Enemies.Values) if (r.Type == EnemyType.Rioter && w.Warden.TurnT > 3.5f) r.Pos = new V2(1450f, 780f);  // keep them away from the Warden
        });
        int guards = g.Enemies.Values.Count(e => e.Type == EnemyType.Guard);
        Run(s, dt, 0.3f, null, null);
        bool clientGuards = cg.Enemies.Values.Any(e => e.Type == EnemyType.Guard);
        int atGuards = g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter);
        Run(s, dt, 12f, () => g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter) == 0, () =>
        {
            me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f); cg.Players[2].Pos = friend.Pos;
            foreach (var p in g.Players.Values) p.InvulnT = 1f;
            foreach (var r in g.Enemies.Values) if (r.Type == EnemyType.Rioter) r.Pos = new V2(1450f, 780f);  // held in the corner for the guards
        });
        int afterBeating = g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter);
        float gap0 = g.Enemies.Values.Where(e => e.Type == EnemyType.Guard).Select(e => Math.Min(e.Pos.DistanceTo(me.Pos), e.Pos.DistanceTo(friend.Pos))).DefaultIfEmpty(9999f).Min();
        Run(s, dt, 3f, null, () => { me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f); cg.Players[2].Pos = friend.Pos; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        float gap1 = g.Enemies.Values.Where(e => e.Type == EnemyType.Guard).Select(e => Math.Min(e.Pos.DistanceTo(me.Pos), e.Pos.DistanceTo(friend.Pos))).DefaultIfEmpty(9999f).Min();
        Console.WriteLine("  warden guards: {0} rioters before, {1} when {2} guards came (client saw them {3}), {4} after the beating; nearest guard to a player {5:0} -> {6:0} px",
            before, atGuards, guards, clientGuards, afterBeating, gap0, gap1);
        Check(before >= 18 && guards >= 3 && clientGuards, "the riot pours out 9 a door; rioters left at 10 s bring the guards (everyone sees them)");
        Check(afterBeating < atGuards && gap1 < gap0, "the guards beat the rioters down, then come for the players");

        // The last stand below 10%: the rioters scatter.
        w.Hp = w.MaxHp * 0.09f;
        Run(s, dt, 3f, () => w.Warden.Stage == WardenStage.LastStand && w.Warden.Turn == WardenTurn.LastStand, Clear);
        Check(w.Warden.Stage == WardenStage.LastStand && g.Enemies.Values.Count(e => e.Type == EnemyType.Rioter) == 0, "below 10%: the last stand, and the rioters scatter");
        s[0].Leave(""); s[1].Leave("");
    }

    // The Singularity's reworked fight: corner wells, Pull -> Collapse -> Normal (spiral + sweeping lasers) -> Cage.
    // The Lich in three stages and a last stand (Lich.cs): its phylacteries' guard and window, the turns of each stage,
    // the soul beams, sweep, prisons, spirits, grave hops and dead hands, all seen on the client too.
    static void LichTest(float dt)
    {
        var s = StartBuffed(dt, 35, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var hurt = typeof(Game).GetMethod("HurtEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Lich) != null && !g.IsSpawning(Find(s[0], EnemyType.Lich)));
        var lich = Find(s[0], EnemyType.Lich);
        Check(lich != null, "wave 35 brings the Lich");
        if (lich == null) { s[0].Leave(""); s[1].Leave(""); return; }
        Run(s, dt, 1.5f, null, safe);
        Func<List<Enemy>> Jars = () => g.Enemies.Values.Where(e => e.Type == EnemyType.Phylactery && e.ParentId == lich.Id && e.Hp > 0f).ToList();
        Func<Enemy> CL = () => c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Lich);

        // The Crypt: one phylactery guards it; break it for a 12 s window.
        float hp = lich.Hp; hurt.Invoke(g, new object[] { lich, 300f, 1 });
        bool guarded = lich.Hp == hp && lich.Phase == Game.LichGuarded;
        int jars = Jars().Count;
        foreach (var j in Jars()) j.Hp = 0f;
        Run(s, dt, 0.3f, null, safe);
        hp = lich.Hp; hurt.Invoke(g, new object[] { lich, 300f, 1 });
        bool open = lich.Hp < hp && lich.Lich.Window > 10f;
        Run(s, dt, 0.3f, null, safe);
        float clientWindow = CL()?.Lich?.Window ?? 0f;
        Console.WriteLine("  crypt: {0} phylactery, guarded {1}; broken: open {2} (client window {3:0.0} s)", jars, guarded, open, clientWindow);
        Check(lich.Lich.Stage == LichStage.Crypt && jars == 1 && guarded, "the Crypt: one phylactery keeps the Lich from being hurt");
        Check(open && clientWindow > 9f, "breaking it opens a 12 s window to hurt the Lich (seen on the client)");
        Run(s, dt, 12.5f, null, safe);
        Check(Jars().Count == 1 && lich.Phase == Game.LichGuarded, "after the window another phylactery rises");

        // Its turns: fans, rings and the dead rising, with breathers.
        var turns = new List<LichTurn>();
        bool fans = false, rings = false;
        Run(s, dt, 24f, null, () =>
        {
            safe();
            if (turns.Count == 0 || turns[turns.Count - 1] != lich.Lich.Turn) turns.Add(lich.Lich.Turn);
            foreach (var b in g.Bullets.Values)
                if (b.Source == EnemyType.Lich) { fans |= b.Kind == Defs.PierceShot; rings |= b.Kind == Defs.BigShot; }
        });
        int dead = g.Enemies.Values.Count(e => e.Type == EnemyType.Grunt && e.ParentId == lich.Id);
        string order = string.Join(" > ", turns);
        Console.WriteLine("  crypt turns: {0}; raised {1}", order, dead);
        Check(order.Contains("Fans > Rest > Rings > Rest > Raise > Rest"), "the Crypt takes turns: soul fans, grave rings, raising the dead, breathers between");
        Check(fans && rings && dead > 0, "it fires fans of piercing shots and rings of orbs, and the dead climb out of graves");

        // 65%: it changes stage (can't be hurt, screen cleared), then Soul Harvest with three phylacteries.
        foreach (var j in Jars()) j.Hp = 0f;
        Run(s, dt, 0.3f, null, safe);
        lich.Hp = lich.MaxHp * 0.6f;
        Run(s, dt, 0.2f, null, safe);
        hp = lich.Hp; hurt.Invoke(g, new object[] { lich, 300f, 1 });
        bool shift = lich.Lich.TransT > 0f && lich.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Lich);
        Run(s, dt, 3f, () => lich.Lich.Stage == LichStage.Harvest && lich.Lich.TransT <= 0f, safe);
        Run(s, dt, 1.2f, null, safe);
        Console.WriteLine("  stage change: immune {0}; now {1} (client {2}); phylacteries {3}", shift, lich.Lich.Stage, CL()?.Lich?.Stage, Jars().Count);
        Check(shift && lich.Lich.Stage == LichStage.Harvest && CL()?.Lich?.Stage == LichStage.Harvest, "at 65% it changes stage (can't be hurt, screen cleared), then Soul Harvest (seen on the client)");
        Check(Jars().Count == 3 && lich.Phase == Game.LichGuarded, "Soul Harvest raises three phylacteries that guard it");

        // A soul beam between two phylacteries hurts.
        var me = g.Players[1];
        var two = Jars().OrderBy(e => e.Id).Take(2).ToList();
        float taken = me.Run.DamageTaken;
        me.InvulnT = 0f; me.DevGod = false;
        Run(s, dt, 0.6f, () => me.Run.DamageTaken > taken, () => { me.Pos = (two[0].Pos + two[1].Pos) * 0.5f; foreach (var p in g.Players.Values) if (p != me) p.InvulnT = 1f; });
        Console.WriteLine("  soul beam: damage taken standing on it {0:0.0}", me.Run.DamageTaken - taken);
        Check(me.Run.DamageTaken > taken, "the soul beams between the phylacteries hurt");

        // Its turns: the sweep, bone prisons, wailing spirits.
        bool sweepLive = false, prisons = false, prisonShots = false, spirits = false, clientSpirits = false, clientSweep = false;
        turns.Clear();
        Run(s, dt, 22f, null, () =>
        {
            safe();
            var q = lich.Lich;
            if (turns.Count == 0 || turns[turns.Count - 1] != q.Turn) turns.Add(q.Turn);
            sweepLive |= q.SweepOn == 2;
            clientSweep |= CL()?.Lich?.SweepOn == 2;
            prisons |= q.Prisons.Count > 0;
            prisonShots |= q.Prisons.Any(pr => pr.Fired);
            spirits |= q.Spirits.Count >= 6;
            clientSpirits |= (CL()?.Lich?.Spirits.Count ?? 0) > 0;
        });
        order = string.Join(" > ", turns);
        Console.WriteLine("  harvest turns: {0}; sweep {1} (client {2}), prisons {3} fired {4}, spirits {5} (client {6})", order, sweepLive, clientSweep, prisons, prisonShots, spirits, clientSpirits);
        Check(order.Contains("Sweep > Rest > Prison > Rest > Spirits > Rest"), "Soul Harvest takes turns: soul sweep, bone prison, wailing spirits");
        Check(sweepLive && clientSweep, "the soul sweep swings across (on the client too)");
        Check(prisons && prisonShots, "bone prisons rise round the players and fire inward");
        Check(spirits && clientSpirits, "six wailing spirits circle and dive (on the client too)");

        // 30%: Undeath, no phylacteries; it blinks between graves and hands burst up.
        lich.Hp = lich.MaxHp * 0.25f;
        foreach (var j in Jars()) j.Hp = 0f;
        Run(s, dt, 4f, () => lich.Lich.Stage == LichStage.Undeath && lich.Lich.TransT <= 0f, safe);
        Run(s, dt, 0.2f, null, safe);
        bool atGrave = Enumerable.Range(0, Game.GraveCount).Any(i => lich.Pos.DistanceTo(g.GravePos(i)) < 120f);
        var spots = new HashSet<int>();
        bool faded = false, hands = false, clientHands = false, storm = false;
        turns.Clear();
        Run(s, dt, 22f, null, () =>
        {
            safe();
            var q = lich.Lich;
            if (turns.Count == 0 || turns[turns.Count - 1] != q.Turn) turns.Add(q.Turn);
            faded |= lich.Phase == Game.LichFaded;
            for (int i = 0; i < Game.GraveCount; i++) if (lich.Pos.DistanceTo(g.GravePos(i)) < 120f) spots.Add(i);
            hands |= g.Hazards.Values.Any(h => h.Kind == Game.HazardHand);
            clientHands |= c.Hazards.Values.Any(h => h.Kind == Game.HazardHand);
            storm |= q.Turn == LichTurn.Storm && g.Bullets.Values.Any(b => b.Source == EnemyType.Lich && b.Kind == Defs.SmallShot);
        });
        order = string.Join(" > ", turns);
        Console.WriteLine("  undeath: at a grave {0}; turns {1}; graves visited {2}; faded {3}; hands {4} (client {5}); storm {6}; phylacteries {7}", atGrave, order, spots.Count, faded, hands, clientHands, storm, Jars().Count);
        Check(atGrave && Jars().Count == 0 && order.Contains("Hop > Rest > Hands > Rest > Storm > Rest"), "Undeath: no phylacteries; grave hops, dead hands, soul storm");
        Check(spots.Count >= 3 && faded, "it blinks from grave to grave (can't be hurt while it fades)");
        Check(hands && clientHands && storm, "dead hands burst up under the players (seen on the client), and the soul storm spirals");

        // 10%: the final rite.
        lich.Hp = lich.MaxHp * 0.08f;
        Run(s, dt, 7f, null, safe);
        int risen = g.Enemies.Values.Count(e => e.Type == EnemyType.Grunt && e.ParentId == lich.Id);
        Console.WriteLine("  last stand: {0}, turn {1}; dead risen {2}", lich.Lich.Stage, lich.Lich.Turn, risen);
        Check(lich.Lich.Stage == LichStage.LastStand && lich.Lich.Turn == LichTurn.Rite && risen > 0, "below 10%: the final rite (the dead rise from every grave)");
        s[0].Leave(""); s[1].Leave("");
    }

    static void SingularityTest(float dt)
    {
        var s = StartBuffed(dt, 20, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var hurt = typeof(Game).GetMethod("HurtEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Singularity) != null && !g.IsSpawning(Find(s[0], EnemyType.Singularity)));
        var sing = Find(s[0], EnemyType.Singularity);
        Run(s, dt, 1.5f, null, safe);
        Func<List<Enemy>> Wells = () => g.Enemies.Values.Where(e => e.Type == EnemyType.GravityWell).ToList();
        var wells = Wells();
        bool corners = wells.Count == 4 && wells.All(w => Math.Min(w.Pos.X, Game.Width - w.Pos.X) < 150f && Math.Min(w.Pos.Y, Game.Height - w.Pos.Y) < 150f);
        Console.WriteLine("  corner wells: {0} at {1}; stage {2}", wells.Count, string.Join(" ", wells.Select(w => "(" + (int)w.Pos.X + "," + (int)w.Pos.Y + ")")), sing.Sing?.Stage);
        Check(corners && sing.Sing != null && sing.Sing.Stage == SingStage.EventHorizon, "the fight opens in the Event Horizon with 4 gravity wells in the corners");

        // A player near a corner drifts toward its well; one in the middle doesn't.
        var drift = typeof(Game).GetMethod("WellDriftAt", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var w0 = wells[0];
        V2 nearPos = w0.Pos + (new V2(Game.Width / 2f, Game.Height / 2f) - w0.Pos).Normalized() * 200f;
        V2 near = (V2)drift.Invoke(g, new object[] { nearPos });
        V2 mid = (V2)drift.Invoke(g, new object[] { new V2(Game.Width / 2f, Game.Height / 2f) });
        Console.WriteLine("  well drift: {0:0} px/s 200 px from a well, {1:0} in the middle", near.Length, mid.Length);
        Check(near.Length > 10f && near.Length < 60f && mid.Length < 1f, "corner wells drift nearby players slowly toward them");

        // Event Horizon: Pull, Collapse, then the spiral and three lasers, with breathers between.
        var phases = new List<byte>();
        var facings = new List<float>();
        bool redShots = false, purple = false;
        Run(s, dt, 19f, null, () =>
        {
            safe();
            if (phases.Count == 0 || phases[phases.Count - 1] != sing.Phase) phases.Add(sing.Phase);
            if (sing.Phase == Game.SingLasers) facings.Add(sing.Facing);
            foreach (var b in g.Bullets.Values)
                if (b.Source == EnemyType.Singularity) { redShots |= b.Kind == Defs.PierceShot; purple |= b.Kind == Defs.BigShot; }
        });
        Func<byte, string> Name = p => p == Game.SingPull ? "Pull" : p == Game.SingCollapse ? "Collapse" : p == Game.SingNormal ? "Normal" : p == Game.SingLaserWarn ? "LaserWarning"
            : p == Game.SingLasers ? "Lasers" : p == Game.SingRest ? "Rest" : p.ToString();
        string order = string.Join(" > ", phases.Select(Name));
        Console.WriteLine("  event horizon: {0}", order);
        Check(order.Contains("Pull > Rest > Collapse > Rest > Normal > LaserWarning > Lasers > Rest"), "Event Horizon: Pull, Collapse, then the spiral and lasers after a warning, breathers between");
        Check(facings.Count > 60 && facings.Last() - facings.First() > 0.3f * Game.LaserSpin * facings.Count * dt && Game.SingLaserCount(sing) == 3, "three lasers sweep slowly around (counter-clockwise)");
        Check(!redShots && purple, "it fires the purple spiral but no red shots");

        // Killed wells stay gone for the rest of the stage.
        foreach (var w in Wells()) w.Hp = 0f;
        Run(s, dt, 20f, null, safe);
        Console.WriteLine("  wells 20 s after killing them all: {0}", Wells().Count);
        Check(Wells().Count == 0 && sing.Sing.Stage == SingStage.EventHorizon, "killed gravity wells don't come back until the next stage");

        // 65%: it changes stage (can't be hurt, the screen clears), then Accretion with four fresh wells and the disk.
        sing.Hp = sing.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        float hp = sing.Hp; hurt.Invoke(g, new object[] { sing, 500f, 1 });
        bool immune = sing.Sing.TransT > 0f && sing.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Singularity);  // (its Grunts and Snipers keep shooting)
        Run(s, dt, 3f, () => sing.Sing.Stage == SingStage.Accretion && sing.Sing.TransT <= 0f, safe);
        Run(s, dt, 1f, null, safe);
        var cs = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Singularity);
        Console.WriteLine("  stage change: immune {0}; now {1}, client sees {2} with the disk {3}; wells {4}", immune, sing.Sing.Stage, cs?.Sing?.Stage, cs != null && Game.DiskUp(cs), Wells().Count);
        Check(immune && sing.Sing.Stage == SingStage.Accretion && cs?.Sing?.Stage == SingStage.Accretion, "at 65% it changes stage (can't be hurt, screen cleared), then Accretion (seen on the client)");
        Check(Wells().Count == 4, "the next stage brings four fresh gravity wells");
        Check(cs != null && Game.DiskUp(cs), "the accretion disk is up, on the client too");

        // Standing on a disk orb hurts.
        var me = g.Players[1];
        V2 orb;
        int slot = Enumerable.Range(0, Game.DiskInnerN).First(i => Game.DiskOrb(sing.Sing, sing.Pos, 0, i, out orb));
        float taken = me.Run.DamageTaken;
        Run(s, dt, 0.3f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            V2 at; Game.DiskOrb(sing.Sing, sing.Pos, 0, slot, out at); me.InvulnT = 0f; me.Pos = s[1].Game.Players[1].Pos = at;
        });
        Console.WriteLine("  disk orb hit the player: {0}", me.Run.DamageTaken > taken);
        Check(me.Run.DamageTaken > taken, "the disk's orbs hurt");
        me.Pos = s[1].Game.Players[1].Pos = new V2(200f, 450f);

        // Accretion's turns: the Cage, the Slingshot (orbs flung off the disk), the twin lasers; and it orbits.
        int cagesSeen = 0, flung = 0, maxGone = 0;
        bool twin = false;
        float moved = 0f;
        Run(s, dt, 26f, null, () =>
        {
            safe();
            cagesSeen = Math.Max(cagesSeen, c.Cages.Count);
            if (sing.Phase == Game.SingSlingshot) flung = Math.Max(flung, g.Bullets.Values.Count(b => b.Kind == Defs.BigShot && b.Source == EnemyType.Singularity));
            maxGone = Math.Max(maxGone, CountBits(sing.Sing.DiskGone));
            twin |= sing.Phase == Game.SingLasers && Game.SingLaserCount(sing) == 2;
            moved = Math.Max(moved, sing.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)));
        });
        Console.WriteLine("  accretion: cages on the client {0}, slingshot orbs {1} (disk slots gone {2}), twin lasers {3}, orbit {4:0} px out", cagesSeen, flung, maxGone, twin, moved);
        Check(cagesSeen == 2, "the Cage rings every player (seen on the client)");
        Check(flung >= 3 && maxGone >= 3, "the Slingshot flings orbs off the disk at the players");
        Check(twin, "two lasers sweep in Accretion");
        Check(moved > 120f, "it orbits the middle in Accretion");

        // 30%: Spaghettify. The event horizon grows (the core's body grows with it) and pulls all the time.
        sing.Hp = sing.MaxHp * 0.25f;
        Run(s, dt, 3f, () => sing.Sing.Stage == SingStage.Spaghettify && sing.Sing.TransT <= 0f, safe);
        Run(s, dt, 2f, null, safe);
        cs = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Singularity);
        Console.WriteLine("  spaghettify: horizon {0:0} (body {1:0}, client {2:0}), pull {3:0}, wells {4}", sing.Sing.Horizon, sing.Radius, cs?.Radius, g.PullStrength, Wells().Count);
        Check(sing.Sing.Stage == SingStage.Spaghettify && sing.Radius > 70f && cs != null && Math.Abs(cs.Radius - sing.Radius) < 15f && g.PullStrength > 0f && Wells().Count == 4,
            "Spaghettify: the event horizon opens (its body on the client too), pulls all the time, and four fresh wells spawn");

        // The White hole: it pushes, and every enemy bullet turns round (on the client too). Then the Split.
        Run(s, dt, 12f, () => sing.Phase == Game.SingWhiteHole && sing.Sing.TurnT > 0.9f, safe);
        var hb = g.Bullets.Values.FirstOrDefault(b => Defs.IsEnemyShot(b.Kind));
        int hid = hb?.Id ?? -1;
        V2 v0 = hb?.Vel ?? V2.Zero, cv0 = hid >= 0 && c.Bullets.ContainsKey(hid) ? c.Bullets[hid].Vel : V2.Zero;
        Run(s, dt, 0.4f, null, safe);
        bool pushed = g.PullStrength < 0f;
        bool reversed = hid >= 0 && g.Bullets.ContainsKey(hid) && Vdot(g.Bullets[hid].Vel, v0) < 0f;
        bool clientReversed = hid >= 0 && c.Bullets.ContainsKey(hid) && Vdot(c.Bullets[hid].Vel, cv0) < 0f;
        Console.WriteLine("  white hole: push {0:0}, a bullet reversed on the host {1}, on the client {2}", g.PullStrength, reversed, clientReversed);
        Check(pushed && reversed && clientReversed, "the White hole pushes and turns every enemy bullet round (on the client too)");
        Run(s, dt, 6f, () => sing.Phase == Game.SingSplit && g.Enemies.Values.Count(e => e.Type == EnemyType.SingShard && !g.IsSpawning(e)) == 3, safe);
        var shard = g.Enemies.Values.First(e => e.Type == EnemyType.SingShard);
        float h0 = sing.Hp; hurt.Invoke(g, new object[] { sing, 300f, 1 });
        bool coreImmune = sing.Hp == h0;
        float direct = h0 - sing.Hp;
        hurt.Invoke(g, new object[] { shard, 300f, 1 });
        Console.WriteLine("  split: {0} shards, core hit directly {1:0}, through a shard {2:0}", g.Enemies.Values.Count(e => e.Type == EnemyType.SingShard), direct, h0 - sing.Hp);
        Check(coreImmune && sing.Hp < h0 - 200f, "Split: three shards; the core can't be hurt, the shards pass their damage to it");

        // Below 10%: the last stand. The horizon swells, the pull doubles, and rings of big orbs.
        sing.Hp = sing.MaxHp * 0.08f;
        Run(s, dt, 10f, () => sing.Sing.Stage == SingStage.LastStand, safe);
        bool rings = false;
        Run(s, dt, 3f, null, () => { safe(); rings |= g.Bullets.Values.Count(b => b.Kind == Defs.BigShot && b.Source == EnemyType.Singularity) >= 16; });
        Console.WriteLine("  last stand: horizon {0:0}, pull {1:0}, orb rings {2}, shards left {3}", sing.Sing.Horizon, g.PullStrength, rings, g.Enemies.Values.Count(e => e.Type == EnemyType.SingShard));
        Check(sing.Sing.Stage == SingStage.LastStand && sing.Sing.Horizon > 170f && g.PullStrength > 100f && rings, "below 10%: the last stand (a bigger horizon, a stronger pull, orb rings)");
        s[0].Leave(""); s[1].Leave("");
    }

    static int CountBits(int v) { int n = 0; while (v != 0) { v &= v - 1; n++; } return n; }
    static float Vdot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

    // The Colossus (Colossus.cs): three stages that break it down, stomp rings of push blocks, then a last stand.
    static void ColossusTest(float dt)
    {
        var s = StartBuffed(dt, 25, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Colossus) != null && !g.IsSpawning(Find(s[0], EnemyType.Colossus)));
        var col = Find(s[0], EnemyType.Colossus);
        foreach (var e in g.Enemies.Values) if (e != col) e.Hp = 0f;  // just the Colossus

        // Awakening: Stomps, Boulders, Fissures, with breathers; rings, landings and cracks reach the client.
        var turns = new List<ColTurn>();
        bool ringOnClient = false, oneGap = false, dropOnClient = false, crackOnClient = false, volley = false;
        Run(s, dt, 20f, null, () =>
        {
            safe();
            if (col.Colossus != null && (turns.Count == 0 || turns[turns.Count - 1] != col.Colossus.Turn)) turns.Add(col.Colossus.Turn);
            var cc = Find(s[1], EnemyType.Colossus)?.Colossus;
            if (cc != null) { ringOnClient |= cc.Rings.Count > 0; oneGap |= cc.Rings.Any(r => r.Gaps == 1); dropOnClient |= cc.Drops.Count > 0; crackOnClient |= cc.Cracks.Count > 0; }
            volley |= g.Bullets.Values.Any(b => b.Source == EnemyType.Colossus && b.Kind == Defs.PierceShot);
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  awakening: {0}", order);
        Check(col.Colossus.Stage == ColStage.Awakening && order.StartsWith("Stomps > Rest > Boulders > Rest > Fissures > Rest"), "Awakening: Stomps, Boulders, Fissures, with breathers between");
        Check(ringOnClient && oneGap && dropOnClient && crackOnClient, "stomp rings (one gap), thrown boulders and fissures show on the client");
        Check(volley, "its rock volley still flies between turns");

        // The stomp ring is a wall of push blocks: walk into it and it shoves you back; caught, it carries you out;
        // in its gap you're left alone; pinned against the edge, it rolls over you. It hurts once (twice if it rolls you).
        var cst = col.Colossus;
        cst.Rings.Clear(); cst.Drops.Clear(); cst.Cracks.Clear();
        var me = g.Players[1];
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp;
        V2 center = new V2(800f, 450f);
        Func<float, float, StompRing> Ring = (radius, gap) => { var r = new StompRing { Center = center, Radius = radius, Gap1 = gap, Gaps = 1 }; cst.Rings.Add(r); return r; };
        Action quiet = () => { col.FireT = 99f; cst.TurnT = 0f; cst.ActT = 99f; cst.Drops.Clear(); cst.Cracks.Clear(); foreach (var b in g.Bullets.Values.Where(b => b.Owner == 0).ToList()) g.Bullets.Remove(b.Id); };
        Action<float> Hold = secs => Run(s, dt, secs, null, quiet);
        // Shoved back: standing just inside a ring that's about to pass (it moves out at 150 px/s).
        me.Pos = center + new V2(0f, -200f);
        var r1 = Ring(190f, 0f);  // gap to the right, the player is above
        float took = me.Run.DamageTaken;
        Hold(0.3f);
        float d1 = me.Pos.DistanceTo(center), hit1 = me.Run.DamageTaken - took;
        Hold(0.7f);  // (by 1.7 s it would pin you against the top edge and roll over you)
        float d2 = me.Pos.DistanceTo(center), hit2 = me.Run.DamageTaken - took;
        Console.WriteLine("  ring catches you: {0:0} px out after 0.3 s, {1:0} after 1 s (ring at {2:0}); damage {3:0} then {4:0}", d1, d2, r1.Radius, hit1, hit2);
        Check(d2 > r1.Radius && hit1 > 0f && hit2 == hit1, "a stomp ring that catches you carries you out in front of it, and hurts once");
        // Walking into it from inside: shoved back in.
        cst.Rings.Clear();
        var r2 = Ring(260f, 0f);
        r2.Hit.Add(me.Id);
        me.Pos = center + new V2(0f, -150f);
        Run(s, dt, 0.6f, null, () => { quiet(); me.Pos += new V2(0f, -400f) * dt; });
        Console.WriteLine("  walking out into a ring: {0:0} px from its centre (ring back {1:0})", me.Pos.DistanceTo(center), Game.RingBack(r2));
        Check(me.Pos.DistanceTo(center) < Game.RingBack(r2) + 1f, "walk into a stomp ring and it shoves you back: only its gap lets you through");
        // In the gap: untouched.
        cst.Rings.Clear();
        me.Pos = center + new V2(250f, 0f);
        took = me.Run.DamageTaken;
        var r3 = Ring(230f, 0f);
        Hold(1f);
        Console.WriteLine("  in the gap: {0:0} px out, damage {1:0}", me.Pos.DistanceTo(center), me.Run.DamageTaken - took);
        Check(Math.Abs(me.Pos.DistanceTo(center) - 250f) < 2f && me.Run.DamageTaken == took && r3.Radius > 300f, "a ring passes you by if you're in its gap");
        // Pinned against the edge: it rolls over you.
        cst.Rings.Clear();
        me.Pos = new V2(800f, 30f);
        took = me.Run.DamageTaken;
        var r4 = Ring(380f, MathX.Pi / 2f);  // gap at the bottom; the player is at the top edge
        Hold(1.5f);
        Console.WriteLine("  pinned at the edge: now {0:0} px from the centre (ring at {1:0}); damage {2:0}", me.Pos.DistanceTo(center), r4.Radius, me.Run.DamageTaken - took);
        Check(me.Pos.DistanceTo(center) < Game.RingBack(r4) && me.Run.DamageTaken - took > 1.5f * hit1, "pinned against the edge, a ring rolls over you (and hurts again)");
        cst.Rings.Clear();
        g.DevSetGod(1, true);

        // 65%: can't be hurt while it changes stage, the screen clears; Crumbling brings four plates of cover.
        col.Hp = col.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        float hp = col.Hp; hurt.Invoke(g, new object[] { col, 500f, 1 });
        bool immune = cst.TransT > 0f && col.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Colossus);
        Run(s, dt, 3f, () => cst.Stage == ColStage.Crumbling && cst.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        var cc2 = Find(s[1], EnemyType.Colossus)?.Colossus;
        Console.WriteLine("  stage change: immune {0}; now {1} with {2} plates; client sees {3} with {4}", immune, cst.Stage, cst.Slabs.Count, cc2?.Stage, cc2?.Slabs.Count);
        Check(immune && cst.Stage == ColStage.Crumbling && cc2?.Stage == ColStage.Crumbling, "at 65% it changes stage (can't be hurt, screen cleared), then Crumbling (seen on the client)");
        Check(cst.Slabs.Count == 4 && cc2?.Slabs.Count == 4, "its armour falls as four plates of cover");

        // A plate stops shots both ways; enemy shots wear it down.
        var slab = cst.Slabs[0];
        int slabHp = slab.Hp;
        var mine = (Bullet)shoot.Invoke(g, new object[] { Defs.DpsShot, slab.Pos - new V2(0f, 60f), new V2(0f, 500f), 0f, 1f, 10f, 1, false, 0f });
        var theirs = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, slab.Pos + new V2(0f, 60f), new V2(0f, -500f), 0f, 1f, 10f, 0, true, 0f });
        Run(s, dt, 0.3f, null, safe);
        Console.WriteLine("  plate: player shot gone {0}, enemy shot gone {1}, plate {2} -> {3}", !g.Bullets.ContainsKey(mine.Id), !g.Bullets.ContainsKey(theirs.Id), slabHp, slab.Hp);
        Check(!g.Bullets.ContainsKey(mine.Id) && !g.Bullets.ContainsKey(theirs.Id) && slab.Hp < slabHp, "plates stop shots from both sides, and enemy shots wear them down");

        // Charging into a plate breaks it and dazes the Colossus: +50% damage taken.
        var target = cst.Slabs[1];
        col.Pos = target.Pos + new V2(0f, 200f);
        cst.Turn = ColTurn.Rest; cst.TurnT = 0f; cst.TurnLen = 99f; cst.ActT = 99f;
        cst.ChargeDir = -MathX.Pi / 2f; cst.ChargeWarn = 0.01f;
        int plates = cst.Slabs.Count;
        Run(s, dt, 1f, () => cst.DazeT > 0f, safe);
        hp = col.Hp; hurt.Invoke(g, new object[] { col, 100f, 1 });
        float dazedHit = hp - col.Hp;
        Console.WriteLine("  charge into a plate: plates {0} -> {1}, dazed {2:0.0} s, a 100 hit took {3:0}", plates, cst.Slabs.Count, cst.DazeT, dazedHit);
        Check(cst.Slabs.Count == plates - 1 && cst.DazeT > 1f && Math.Abs(dazedHit - 150f) < 1f, "a charge into a plate breaks it and dazes the Colossus (+50% damage taken)");
        cst.TurnLen = 1f;

        // 30%: Molten Core; its stomps leave lava, which hurts.
        col.Hp = col.MaxHp * 0.25f;
        Run(s, dt, 4f, () => cst.Stage == ColStage.MoltenCore && cst.TransT <= 0f, safe);
        Run(s, dt, 3f, () => g.Patches.Values.Any(pt => pt.Kind == PatchKind.Lava && pt.Active), safe);
        var lava = g.Patches.Values.FirstOrDefault(pt => pt.Kind == PatchKind.Lava && pt.Active);
        bool lavaOnClient = lava != null && c.Patches.ContainsKey(lava.Id);
        float burned = 0f;
        if (lava != null)
        {
            g.DevSetGod(1, false);
            me.Hp = me.MaxHp;
            took = me.Run.DamageTaken;
            Run(s, dt, 1f, null, () => { quiet(); cst.Rings.Clear(); col.Pos = lava.Pos + (lava.Pos.X < 800f ? new V2(500f, 0f) : new V2(-500f, 0f)); me.Pos = lava.Pos; me.InvulnT = 0f; });  // (its body away: touching it hurts too)
            burned = me.Run.DamageTaken - took;
            g.DevSetGod(1, true);
        }
        Console.WriteLine("  molten core: stage {0}, lava {1} (client {2}), a second in it: {3:0} damage", cst.Stage, lava != null, lavaOnClient, burned);
        Check(cst.Stage == ColStage.MoltenCore && cst.Slabs.Count == 0, "at 30% the shell splits: Molten Core, and the plates are gone");
        Check(lava != null && lavaOnClient && burned > 10f && burned < 40f, "its stomps leave lava pools that hurt about 20 a second (seen on the client)");

        // Below 10%: the last stand, kneeling in the middle with stomps of two gaps.
        col.Hp = col.MaxHp * 0.08f;
        Run(s, dt, 6f, null, safe);
        Console.WriteLine("  last stand: stage {0}, {1:0} px from the middle, rings with two gaps {2}", cst.Stage, col.Pos.DistanceTo(new V2(800f, 450f)), cst.Rings.Any(r => r.Gaps == 2));
        Check(cst.Stage == ColStage.LastStand && col.Pos.DistanceTo(new V2(800f, 450f)) < 10f && cst.Rings.Any(r => r.Gaps == 2), "below 10% it kneels in the middle for its last stand: stomps with two gaps");
        col.Hp = 0f;
        Run(s, dt, 1f, null, safe);
        Check(Find(s[0], EnemyType.Colossus) == null && Find(s[1], EnemyType.Colossus) == null, "the Colossus goes down (and its rings with it)");
        s[0].Leave(""); s[1].Leave("");
    }

    // The Singularity's Cage (in Accretion): standing still inside hurts once the ring reaches you; shooting the wells frees you.
    static void SingularityCageTest(float dt)
    {
        var s = StartBuffed(dt, 20, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Singularity) != null && !g.IsSpawning(Find(s[0], EnemyType.Singularity)));
        var sing = Find(s[0], EnemyType.Singularity);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Run(s, dt, 0.5f, null, safe);
        sing.Hp = sing.MaxHp * 0.6f;
        Run(s, dt, 6f, () => g.Cages.Count > 0, safe);
        float first = g.Cages.Count > 0 ? g.Cages.Values.First().Radius : 0f;
        int cageWells = g.Enemies.Values.Count(e => e.Type == EnemyType.CageWell);
        var me = g.Players[1];
        var cage = g.Cages.Values.First(cg => cg.PlayerId == 1);
        cage.Center = new V2(300f, 160f);  // well away from the accretion disk, whose orbs would hurt first
        me.Pos = s[1].Game.Players[1].Pos = cage.Center;
        bool cageHurts = false;
        g.DamageEvent = (p, a, src) => cageHurts |= p.Id == 1 && src.ToLowerInvariant().Contains("cage");  // its wall, or its wells
        // Only the cage may hurt: everything else is cleared away and the player keeps out of the disk's way.
        Run(s, dt, 8.5f, () => cageHurts, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
            me.Pos = cage.Center;
            me.InvulnT = cage.Radius > 40f ? 1f : 0f;  // safe until the ring is about to reach it
        });
        g.DamageEvent = null;
        float last = cage.Radius;
        foreach (int id in cage.Wells.ToList()) g.Enemies[id].Hp = 0f;
        Run(s, dt, 0.3f, null, safe);
        Console.WriteLine("  cage: {0} cage wells, radius {1:0} -> {2:0}; hurt when it closed {3}; gone once its wells died {4}", cageWells, first, last, cageHurts, !g.Cages.ContainsKey(cage.Id));
        Check(cageWells == 6 && last < first - 100f, "the Cage rings every player with 3 small wells, shrinking");
        Check(cageHurts && !g.Cages.ContainsKey(cage.Id), "the shrinking cage hurts when it reaches you, and breaks when its wells are shot down");
        s[0].Leave(""); s[1].Leave("");
    }

    static void LaserDebug()
    {
        float dt = 1f / 60f;
        var lab = new BossLab { Wave = 20, Skill = BotSkill.Good, PlayerCount = 1, Seed = 3 };
        lab.Seats[0].Role = Role.Healer; lab.Seats[0].Kit = Kit.Mage;
        var host = new Session();
        lab.Start(host, nextPort++);
        var g = host.Game;
        g.DamageEvent = (p, a, src) => {
            var sing = g.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Singularity);
            float rel = sing == null ? 0 : (p.Pos - sing.Pos).Angle - sing.Facing;
            Console.WriteLine("  t {0:0.00} hit {1:0} by {2} at ({3:0},{4:0}) dist {5:0} relAng {6:0.00} phase {7} danger {8:0.0} hp {9:0}",
                g.RunTime, a, src, p.Pos.X, p.Pos.Y, sing == null ? 0 : p.Pos.DistanceTo(sing.Pos), MathX.Fmod(rel, MathX.Tau / 3f), sing?.Phase, g.BotDanger, p.Hp);
            var mv = (V2)typeof(Game).GetField("botMove", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(g);
            Console.WriteLine("     onLaser now: {0}  move ({1:0.0},{2:0.0}) facing {3:0.00} downed {4} bot {5} lvl {6}", Enumerable.Range(0, 3).Any(k => Game.OnLaser(sing.Pos, sing.Facing + MathX.Tau * k / 3f, p.Pos, 12f)), mv.X, mv.Y, sing.Facing, p.Downed, g.Bot, g.BotLevel);
        };
        for (int f = 0; f < 60 * 25 && lab.Outcome == LabOutcome.Running; f++) { Step(host, dt); lab.Watch(dt); }
        lab.Stop();
    }

    // Bosses play soft cues for their abilities, heard on both games.
    static void BossSoundTest(float dt)
    {
        var s = StartBuffed(dt, 20, 0);
        s[0].Game.DevSetGod(1, true); s[0].Game.DevSetGod(2, true);
        Run(s, dt, 15, () => s[0].Game.State == MatchState.Fighting && Find(s[0], EnemyType.Singularity) != null);
        clientHeard.Clear();
        Run(s, dt, 25);
        var heard = clientHeard.Where(x => x.ToString().StartsWith("Boss")).ToList();
        Console.WriteLine("  client heard boss cues: " + string.Join(", ", heard.GroupBy(x => x).Select(gr => gr.Key + " x" + gr.Count())));
        Check(heard.Contains(Sfx.BossCharge) && heard.Contains(Sfx.BossPulse) && heard.Contains(Sfx.BossShift) && heard.Count < 20,
            "the Singularity's attacks each play a soft cue, heard by the other player too (and not too often)");  // (the Cage's chime is in its second stage)
        s[0].Leave(""); s[1].Leave("");
    }

    static void BossMechanics(float dt)
    {
        // The Warden's Lockdown cross: it dashes to a spot, plants, and fires straight streams up, down, left and
        // right (a static "+" that cuts the arena into quadrants), then moves and does it again, four times.
        {
            var s = StartBuffed(dt, 5, 0);
            Run(s, dt, 10, () => { var b = Find(s[0], EnemyType.Boss); return b != null && b.SpawnT <= 0f; });
            var w = Find(s[0], EnemyType.Boss);
            s[0].Game.Bot = s[1].Game.Bot = false;
            // Drop it into the Lockdown, and wait for the cross to start.
            Run(s, dt, 8, () => w.Warden != null && w.Warden.Turn == WardenTurn.Cross && w.Warden.TransT <= 0f, () => w.Hp = Math.Min(w.Hp, w.MaxHp * 0.6f));
            V2 start = w.Pos;
            float farthest = 0f, movedWhileFiring = 0f, crossTime = 0f, guidesWhileMoving = 0f;
            V2 lastPos = w.Pos;
            bool phaseOnClient = false, movingOnClient = false;
            var plants = new List<V2>();
            int onAxes = 0, offAxes = 0;
            var seen = new HashSet<int>();
            Run(s, dt, 13f, null, () =>
            {
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                w.Hp = Math.Max(w.Hp, w.MaxHp * 0.5f);
                var cw = Find(s[1], EnemyType.Boss);
                bool cCross = cw != null && cw.Warden != null && cw.Warden.Turn == WardenTurn.Cross;
                phaseOnClient |= cCross && !cw.Warden.Dashing;
                movingOnClient |= cCross && cw.Warden.Dashing;
                bool cross = w.Warden.Turn == WardenTurn.Cross, planted = cross && !w.Warden.Dashing;
                if (cross)
                    crossTime += dt;
                if (planted && w.Pos.DistanceTo(lastPos) > 0.5f)
                    guidesWhileMoving += dt;  // the guides show while planted
                if (!planted)
                {
                    lastPos = w.Pos;
                    return;
                }
                if (plants.Count == 0 || plants[plants.Count - 1].DistanceTo(w.Pos) > 100f)
                    plants.Add(w.Pos);
                farthest = Math.Max(farthest, w.Pos.DistanceTo(start));
                bool fired = false;
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Kind == Defs.SmallShot && b.FromBoss && seen.Add(b.Id) && b.Pos.DistanceTo(w.Pos) < w.Radius + 12f)
                    {
                        fired = true;
                        // Each shot leaves straight along one of the four axes.
                        float rel = MathX.Fmod(b.Vel.Angle + MathX.Pi / 4f, MathX.Pi / 2f) - MathX.Pi / 4f;
                        if (Math.Abs(rel) < 0.01f && b.AngVel == 0f) onAxes++; else offAxes++;
                    }
                if (fired)
                    movedWhileFiring = Math.Max(movedWhileFiring, w.Pos.DistanceTo(lastPos));
                lastPos = w.Pos;
            });
            Console.WriteLine("  warden cross: lasted {0:0.0}s, {1} plants, client saw firing {2} / moving {3}, moved {4:0}px between plants, {5:0.00}s of guides while moving, moved {6:0.0}px per tick while firing, shots on the axes {7}, off {8}",
                crossTime, plants.Count, phaseOnClient, movingOnClient, farthest, guidesWhileMoving, movedWhileFiring, onAxes, offAxes);
            Check(phaseOnClient && movingOnClient && farthest > 150f, "the Warden's cross attack moves it to new spots (seen on client)");
            Check(Math.Abs(crossTime - 12f) < 0.3f && plants.Count >= 4, "the Warden's cross lasts 12 s across four plants");
            Check(guidesWhileMoving == 0f, "the cross guides are hidden while the Warden moves");
            Check(onAxes > 100 && offAxes == 0 && movedWhileFiring < 0.5f, "the Warden's cross is a static '+' along the axes, fired while it stands still");
            s[0].Leave(""); s[1].Leave("");
        }
        // Hive Queen (wave 10): summons a swarm and is immune while it's big.
        {
            var s = StartBuffed(dt, 10, 25);
            bool shielded = false, swarm = false, dead = false, seenQueen = false;
            Run(s, dt, 70, () => dead, () =>
            {
                var q = Find(s[1], EnemyType.HiveQueen);
                seenQueen |= q != null;
                shielded |= q != null && q.Phase == 1;
                swarm |= Find(s[1], EnemyType.Shard) != null;
                dead = seenQueen && Find(s[0], EnemyType.HiveQueen) == null;
            });
            Check(swarm && shielded, "Hive Queen summons a swarm and shields herself (seen on client)");
            Check(dead && s[0].Game.Players[1].Rerolls == Game.StartingRerolls + 2, "Hive Queen can be killed and pays out rerolls");
            s[0].Leave(""); s[1].Leave("");
        }
        // Her drones get their own turn: while any live she's shielded and waits (no bombers) for up to 10 s;
        // then the ones left fly back into her and heal her.
        {
            var s = StartBuffed(dt, 10, 0);
            Run(s, dt, 10, () => { var qq = Find(s[0], EnemyType.HiveQueen); return qq != null && qq.SpawnT <= 0f; });
            s[0].Game.Bot = s[1].Game.Bot = false;
            var q = Find(s[0], EnemyType.HiveQueen);
            bool alwaysShielded = true, bombersEarly = false;
            q.Hp = q.MaxHp * 0.9f;
            Run(s, dt, 9.5f, null, () =>
            {
                foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f;
                bombersEarly |= Find(s[0], EnemyType.HiveBomber) != null;
                alwaysShielded &= q.Phase == 1 && q.Queen.Turn == QueenTurn.Drones;
            });
            float hpBefore = q.Hp;
            Run(s, dt, 6f, () => Find(s[0], EnemyType.Shard) == null, () => { foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f; });
            bool home = Find(s[0], EnemyType.Shard) == null;
            Console.WriteLine("  queen with her drones alive: shielded throughout {0}, bombers early {1}, drones flew home {2}, health {3:0.0}% -> {4:0.0}%",
                alwaysShielded, bombersEarly, home, hpBefore / q.MaxHp * 100f, q.Hp / q.MaxHp * 100f);
            Check(alwaysShielded && !bombersEarly, "the Hive Queen waits behind her drones' shield while they live");
            Check(home && q.Hp > hpBefore + q.MaxHp * 0.05f && q.Phase == 0, "drones left after 10 s fly back into her and heal her");
            s[0].Leave(""); s[1].Leave("");
        }
        // Hive Queen's loop: shooting drones (shielded until they're all dead), then explosive drones
        // aimed at where each player was when they launched, then her bullet patterns, then again.
        {
            var s = StartBuffed(dt, 10, 0);
            Run(s, dt, 10, () => { var qq = Find(s[0], EnemyType.HiveQueen); return qq != null && qq.SpawnT <= 0f; });
            s[0].Game.Bot = s[1].Game.Bot = false;
            var q = Find(s[0], EnemyType.HiveQueen);
            Check(q.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 40f, "bosses appear in the middle of the arena");
            q.Pos = new V2(800f, 140f);  // she spawns in the middle; move her clear of the runners' circles
            Run(s, dt, 1.5f);
            bool shieldUp = q.Phase == 1 && Find(s[0], EnemyType.Shard) != null && q.Queen.Turn == QueenTurn.Drones;
            bool bombersEarly = Find(s[0], EnemyType.HiveBomber) != null;
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.Shard) e.Hp = 0f;
            var stages = new List<int> { (int)q.Queen.Turn };
            var aimedAt = new Dictionary<int, V2>();
            var lastPos = new Dictionary<int, V2>();
            int launched = 0, onTarget = 0, offTarget = 0, aimedRight = 0, fans = 0;
            bool clientBomber = false, clientMarker = false, resummoned = false;
            float hpBefore = 1f, hpAfter = 1f, t = 0f;  // minus damage taken so far, when the bomber stage starts / ends
            Run(s, dt, 24f, null, () =>
            {
                t += dt;
                int st = (int)q.Queen.Turn;
                if (st != (int)QueenTurn.Rest && stages[stages.Count - 1] != st) stages.Add(st);
                // Both players keep running in circles.
                int k = 0;
                foreach (var p in s[0].Game.Players.Values)
                {
                    // Separate circles, so neither runs into the spot a bomber was sent at the other.
                    p.Pos = new V2(800f, 480f) + V2.FromAngle(t * 1.1f + k * MathX.Pi) * (k == 0 ? 300f : 130f);
                    k++;
                    Player cp;
                    if (s[1].Game.Players.TryGetValue(p.Id, out cp)) cp.Pos = p.Pos;  // the joining player reports its own position
                    if (st != 2) p.Hp = p.MaxHp;
                }
                if (st == 2 && hpBefore > 0f) hpBefore = -(s[0].Game.Players[1].Run.DamageTaken + s[0].Game.Players[2].Run.DamageTaken);
                if (st == 3 && hpAfter > 0f) hpAfter = -(s[0].Game.Players[1].Run.DamageTaken + s[0].Game.Players[2].Run.DamageTaken);
                if (st == 1 && stages.Count > 4) resummoned = true;
                foreach (var e in s[0].Game.Enemies.Values)
                {
                    if (e.Type != EnemyType.HiveBomber) continue;
                    if (!aimedAt.ContainsKey(e.Id))
                    {
                        launched++;
                        aimedAt[e.Id] = e.Waypoint;
                        foreach (var p in s[0].Game.Players.Values)
                            if (p.Pos.DistanceTo(e.Waypoint) < 15f) { aimedRight++; break; }  // within a frame or two of running
                    }
                    lastPos[e.Id] = e.Pos;
                }
                foreach (var id in new List<int>(lastPos.Keys))
                    if (!s[0].Game.Enemies.ContainsKey(id))
                    {
                        if (lastPos[id].DistanceTo(aimedAt[id]) < 25f) onTarget++; else offTarget++;
                        lastPos.Remove(id);
                    }
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Kind == Defs.PierceShot && b.Source == EnemyType.HiveQueen) { fans++; break; }
                clientBomber |= Find(s[1], EnemyType.HiveBomber) != null;
                foreach (var fx in s[1].Game.Effects) clientMarker |= fx.Kind == FxKind.Target;
            });
            Console.WriteLine("  queen loop: stages {0}; {1} bombers launched ({2} aimed at a player's spot), {3} blew up on their spot, {4} elsewhere; running players lost {5:0} HP to them; fans {6}",
                string.Join(">", stages), launched, aimedRight, onTarget, offTarget, hpBefore - hpAfter, fans);
            Check(shieldUp && !bombersEarly, "the Hive Queen opens with shooting drones and is shielded while they live");
            Check(stages.Count >= 5 && stages[0] == 1 && stages[1] == 2 && stages[2] == 3 && stages[3] == 4 && resummoned, "the Hive Queen loops, one turn at a time: drones, bombers, fan, rings, then drones again");
            Check(launched >= 8 && aimedRight == launched && onTarget >= launched - 2 && clientBomber && clientMarker,
                "Hive bombers fly to where a player was when they launched and blow up there (seen on client, with a marker)");
            Check(hpBefore - hpAfter < 45f && fans > 0, "keeping on the move dodges the bombers; her bullet patterns follow");
            // ...but standing still gets you blown up.
            {
                foreach (var e in s[0].Game.Enemies.Values) if (e.Type != EnemyType.HiveQueen) e.Hp = 0f;
                q.Queen.Turn = QueenTurn.Rest; q.Queen.TurnT = 0f;  // hold her in a breather
                Run(s, dt, 0.1f);
                var still = s[0].Game.Players[1];
                still.Hp = still.MaxHp; still.InvulnT = 0f;
                typeof(Game).GetMethod("LaunchBomber", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .Invoke(s[0].Game, new object[] { q, still.Pos });
                V2 at = still.Pos;
                float taken0 = still.Run.DamageTaken;
                Run(s, dt, 2.5f, null, () => { still.Pos = at; q.Queen.Turn = QueenTurn.Rest; q.Queen.TurnT = 0f; });
                Check(still.Run.DamageTaken - taken0 >= 20f, "a Hive bomber blows up a player who stands still");
            }
            s[0].Leave(""); s[1].Leave("");
        }
        // Twin Reapers (wave 15): killing one enrages the other; it revives unless both die in time.
        {
            var s = StartBuffed(dt, 15, 0);
            Run(s, dt, 15, () => Find(s[0], EnemyType.TwinA) != null && Find(s[0], EnemyType.TwinB) != null && Find(s[0], EnemyType.TwinB).SpawnT <= 0f);
            s[0].Game.Bot = s[1].Game.Bot = false;  // stop shooting so the survivor lives
            Find(s[0], EnemyType.TwinA).Hp = 0f;
            bool enraged = false, timerShown = false;
            Run(s, dt, 2, null, () =>
            {
                var b = Find(s[1], EnemyType.TwinB);
                enraged |= b != null && b.Phase == 1;
                timerShown |= s[1].Game.TwinReviveT > 0f;
            });
            Check(Find(s[0], EnemyType.TwinA) == null && enraged && timerShown, "killing one twin enrages the other and starts the revive timer (seen on client)");
            Run(s, dt, 8, () => Find(s[0], EnemyType.TwinA) != null);
            Run(s, dt, 0.5f);
            var revived = Find(s[1], EnemyType.TwinA);
            Check(revived != null && revived.Hp <= revived.MaxHp * 0.61f && revived.Hp > revived.MaxHp * 0.5f, "the fallen twin revives at 60% health");
            int before = s[0].Game.Players[1].Rerolls;
            Find(s[0], EnemyType.TwinA).Hp = 0f;
            Find(s[0], EnemyType.TwinB).Hp = 0f;
            Run(s, dt, 1);
            Check(Find(s[0], EnemyType.TwinA) == null && Find(s[0], EnemyType.TwinB) == null && s[0].Game.TwinReviveT == 0f
                && s[0].Game.Players[1].Rerolls == before + 2, "killing both together ends the fight with one reward");
            s[0].Leave(""); s[1].Leave("");
        }
        // Twin Reapers: Soul Bond makes the more-hurt one immune, and their tether hurts.
        {
            var s = StartBuffed(dt, 15, 0);
            Run(s, dt, 15, () => Find(s[0], EnemyType.TwinA) != null && Find(s[0], EnemyType.TwinB) != null && Find(s[0], EnemyType.TwinB).SpawnT <= 0f
                && Find(s[0], EnemyType.TwinA).SpawnT <= 0f);
            var a = Find(s[0], EnemyType.TwinA);
            var b = Find(s[0], EnemyType.TwinB);
            a.Hp = a.MaxHp * 0.5f;
            b.Hp = b.MaxHp;
            bool boundOnClient = false;
            Run(s, dt, 0.5f, null, () => { var ca = Find(s[1], EnemyType.TwinA); boundOnClient |= ca != null && ca.Phase == 2; });
            // Shoot only the bound one: the bots skip immune targets, so aim them by hand via a host-side check.
            float hpBound = a.Hp;
            foreach (var e in s[0].Game.Enemies.Values)
                if (!e.Def.IsBoss)
                    e.Hp = 0f;
            Run(s, dt, 3);
            bool immune = a.Hp >= hpBound - 0.01f && Game.SoulBound(a);
            bool otherHurt = b.Hp < b.MaxHp;  // the bots turned to the other Reaper instead
            b.Hp = b.MaxHp * 0.52f;
            Run(s, dt, 0.5f);
            bool released = !Game.SoulBound(a);
            Console.WriteLine("  soul bond: bound on client {0}, immune {1}, bots switched {2}, released {3}", boundOnClient, immune, otherHurt, released);
            Check(boundOnClient && immune && released, "Soul Bond: the more-hurt Reaper is immune until the other is evened out (seen on client)");
            Check(otherHurt, "bots switch to the Reaper that can be hurt");

            // Stand on the tether between them.
            s[0].Game.Bot = s[1].Game.Bot = false;
            var p = s[0].Game.Players[1];
            float hpBefore = 0f;
            bool hurt = false;
            Run(s, dt, 1.5f, null, () =>
            {
                var ta = Find(s[0], EnemyType.TwinA);
                var tb = Find(s[0], EnemyType.TwinB);
                if (ta == null || tb == null)
                    return;
                p.Pos = V2.Lerp(ta.Pos, tb.Pos, 0.5f);
                foreach (var bl in new List<Bullet>(s[0].Game.Bullets.Values))
                    if (Defs.IsEnemyShot(bl.Kind))
                        s[0].Game.Bullets.Remove(bl.Id);  // only the tether should hurt
                if (hpBefore == 0f)
                    hpBefore = p.Hp;
                hurt |= p.Hp < hpBefore - 5f;
            });
            Console.WriteLine("  tether: hp {0:0} -> {1:0}", hpBefore, p.Hp);
            Check(hurt, "the Reapers' tether hurts players standing in it");
            s[0].Leave(""); s[1].Leave("");
        }
        // The Singularity (wave 20): gravity phases pull players in.
        {
            var s = StartBuffed(dt, 20, 0);
            Run(s, dt, 15, () => s[0].Game.State == MatchState.Fighting && Find(s[0], EnemyType.Singularity) != null && Find(s[0], EnemyType.Singularity).SpawnT <= 0f);
            s[1].Game.Bot = false;  // the friend stands still; only the pull should move them
            V2 start = s[1].Game.Players[2].Pos;
            bool pullSeen = false;
            float closest = float.MaxValue;
            V2 center = new V2(Game.Width / 2f, Game.Height / 2f);
            Run(s, dt, 13, null, () =>
            {
                pullSeen |= s[1].Game.PullStrength > 0f;
                closest = Math.Min(closest, s[1].Game.Players[2].Pos.DistanceTo(center));
            });
            Console.WriteLine("  singularity: friend started {0:F0}px from the boss, got pulled to {1:F0}px", start.DistanceTo(center), closest);
            Check(pullSeen && closest < start.DistanceTo(center) - 100f, "The Singularity's gravity pulls the client's player in");
            s[1].Game.Bot = true;

            // Its corner wells bend the players' shots into themselves, and come back after you kill them.
            int wellsOnClient = 0, pulled = 0, compared = 0;
            float drift = 0f;
            var counted = new HashSet<int>();
            int frame = 0;
            Run(s, dt, 6, null, () =>
            {
                int n = 0;
                foreach (var e in s[1].Game.Enemies.Values)
                    if (e.Type == EnemyType.GravityWell)
                        n++;
                wellsOnClient = Math.Max(wellsOnClient, n);
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Pulled && counted.Add(b.Id))
                        pulled++;
                foreach (var p in s[0].Game.Players.Values)
                    p.InvulnT = 1f;
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                    foreach (var b in s[1].Game.Bullets.Values)
                    {
                        Bullet hb;
                        if (s[0].Game.Bullets.TryGetValue(b.Id, out hb) && hb.Pulled)
                        {
                            drift = Math.Max(drift, hb.Pos.DistanceTo(b.Pos));
                            compared++;
                        }
                    }
                }
            });
            Console.WriteLine("  gravity wells: {0} on the client, {1} shots bent, drift {2:0.0}px over {3} samples", wellsOnClient, pulled, drift, compared);
            Check(wellsOnClient == 4, "the Singularity brings 4 gravity wells (seen on client)");
            Check(pulled > 10 && drift < 40f, "gravity wells bend players' shots into themselves, in sync on the client");
            foreach (var e in s[0].Game.Enemies.Values)
                if (e.Type == EnemyType.GravityWell)
                    e.Hp = 0f;
            Run(s, dt, 2);
            bool gone = Find(s[0], EnemyType.GravityWell) == null;
            Run(s, dt, 18, () => Find(s[0], EnemyType.GravityWell) != null, () => { foreach (var p in s[0].Game.Players.Values) p.InvulnT = 1f; });
            bool back = Find(s[0], EnemyType.GravityWell) != null;
            var sing = Find(s[0], EnemyType.Singularity);
            if (sing != null)
                sing.Hp = 0f;
            Run(s, dt, 2);
            bool collapsed = Find(s[0], EnemyType.GravityWell) == null;
            Console.WriteLine("  wells killed {0}, came back within the stage {1}, collapsed with the boss {2}", gone, back, collapsed);
            Check(gone && !back && collapsed, "killed gravity wells stay gone until the next stage, and collapse when the Singularity dies");
            s[0].Leave(""); s[1].Leave("");
        }
    }
}

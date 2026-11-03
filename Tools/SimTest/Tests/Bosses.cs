using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Bosses: the Twin Reapers, Hive Queen and Warden. Later bosses are in BossesLate.cs; sounds, the cage and the
// mechanics sweep in BossMechanics.cs.
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
        // A session recording, to check the fusion and split close the old bosses' records (SessionRecorder.BossMerged).
        var rec = g.Recorder = new SessionRecorder(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "arena-reapers-" + Guid.NewGuid().ToString("N").Substring(0, 6)));
        rec.Begin(g, 15);
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
        var orbIds = typeof(Game).GetField("nextOrbId", flags);
        int orbs0 = (int)orbIds.GetValue(g);
        Run(s, dt, 0.2f, null, Park);
        float ha = a.Hp; hurt.Invoke(g, new object[] { a, 300f, 1 });
        bool immune = g.Reapers.Trans == 1 && a.Hp == ha;
        float tf = friend.Run.DamageTaken;
        Run(s, dt, 4.6f, () => g.Reapers.Trans == 0 && g.Reapers.Stage == ReaperStage.Dual, () => { Park(); PutFriend(new V2(1200f, 700f)); friend.Hp = friend.MaxHp; });
        var d = Find(s[0], EnemyType.DualReaper);
        float blastDmg = friend.Run.DamageTaken - tf;
        Console.WriteLine("  reapers fusion: immune {0}, dual {1}, health {2:0}/{3:0} (twins had {4:0}/{5:0}), power-up hit the friend for {6:0}", immune, d != null, d?.Hp, d?.MaxHp, hpSum, maxSum, blastDmg);
        Check(immune && d != null && Find(s[0], EnemyType.TwinA) == null && Math.Abs(d.MaxHp - maxSum) < 1f && Math.Abs(d.Hp - hpSum) < 20f, "at 65% they fuse into the Dual Reaper (can't be hurt meanwhile), keeping their health");
        int fuseOrbs = (int)orbIds.GetValue(g) - orbs0;
        Console.WriteLine("  reapers fusion: {0} small health orbs dropped ({1} players)", fuseOrbs, g.Players.Count);
        Check(fuseOrbs == Game.StageOrbBase + g.Players.Count, "the fusion (a stage change) drops a few small health orbs: two plus one per player");
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
        {
            var order = (System.Collections.IList)typeof(SessionRecorder).GetField("bossOrder", flags).GetValue(rec);
            var recs = order.Cast<object>().Select(o => new { Name = (string)o.GetType().GetField("Name").GetValue(o), Merged = (float)o.GetType().GetField("Merged").GetValue(o), Died = (float)o.GetType().GetField("Died").GetValue(o) }).ToList();
            int merged = recs.Count(r => r.Merged >= 0f), open = recs.Count(r => r.Merged < 0f && r.Died < 0f);
            Console.WriteLine("  recording: {0}", string.Join(", ", recs.Select(r => r.Name + (r.Merged >= 0f ? " (merged)" : r.Died >= 0f ? " (killed)" : " (open)"))));
            Check(recs.Count == 5 && merged == 3 && open == 2, "the session recording closes the twins' records when they fuse and the Dual Reaper's when it splits (only the new pair stays open)");
        }

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

        // Swarm: one turn at a time. Its bombers swoop out and come at you from the side (Phase 1, then 2 when they turn in).
        var seen = new List<QueenTurn>();
        var flankAngles = new List<float>();
        var turnedIn = new HashSet<int>();
        bool clientFlanker = false, straightInSwarm = false;
        float friendHit = 0f;
        Run(s, dt, 22f, null, () =>
        {
            KillDrones(); Park();
            PutFriend(new V2(1400f, 800f)); friend.Hp = friend.MaxHp; friend.InvulnT = 0f;
            var t = q.Queen.Turn;
            if (seen.Count == 0 || seen[seen.Count - 1] != t) seen.Add(t);
            foreach (var e in g.Enemies.Values)
            {
                if (e.Type != EnemyType.HiveBomber) continue;
                straightInSwarm |= e.Phase == 0;
                if (e.Phase == 2 && turnedIn.Add(e.Id) && e.TargetId == friend.Id)
                {
                    // How far round from the Queen's side it comes in: the angle at the friend between the Queen and it.
                    V2 a = q.Pos - friend.Pos, b = e.Pos - friend.Pos;
                    flankAngles.Add(Math.Abs(MathX.Fmod(a.Angle - b.Angle + MathX.Pi, MathX.Tau) - MathX.Pi) * 180f / MathX.Pi);
                }
            }
            clientFlanker |= cg.Enemies.Values.Any(e => e.Type == EnemyType.HiveBomber && e.Phase >= 1);
        });
        friendHit = friend.Run.DamageTaken;
        Console.WriteLine("  queen swarm bombers: {0} turned in on the friend, from {1} degrees off the Queen's side; client saw them {2}; friend took {3:0}",
            flankAngles.Count, string.Join(", ", flankAngles.Select(x => x.ToString("0"))), clientFlanker, friendHit);
        Check(flankAngles.Count >= 2 && flankAngles.All(x => x > 50f) && !straightInSwarm && clientFlanker,
            "Swarm's bombers swoop out and come at you from the side, not straight from the Queen (and clients see them)");
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

        // A perfect parry sends a stun shot straight back at the Warden: it's frozen for 1.5 s.
        {
            me.Pos = new V2(300f, 300f);
            g.HoldParry = true;
            Run(s, dt, 0.05f, null, () => { Clear(); me.Pos = new V2(300f, 300f); });  // the shield's up
            var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
            V2 toward = V2.FromAngle(me.Aim);  // a stun shot coming straight at the shield
            spawnBullet.Invoke(g, new object[] { Defs.StunShot, me.Pos + toward * (me.Def.BodyR + 20f), -toward * 280f, 0f, 2f, 9f, 0, true, 0f });
            bool stunnedW = false, clientSawStun = false; V2 frozenAt = V2.Zero; bool movedWhileStunned = false;
            Run(s, dt, 3f, () => stunnedW && w.Warden.StunT < 1f, () =>
            {
                Clear(); me.Pos = new V2(300f, 300f);
                if (w.Warden.StunT > 0f && !stunnedW) { stunnedW = true; frozenAt = w.Pos; }
                if (stunnedW && w.Warden.StunT > 0.05f && w.Pos.DistanceTo(frozenAt) > 1f) movedWhileStunned = true;
                clientSawStun |= cg.Enemies[w.Id].Warden.StunT > 0f;
            });
            g.HoldParry = false;
            Console.WriteLine("  warden parry: stunned {0} (client saw it {1}), moved while stunned {2}", stunnedW, clientSawStun, movedWhileStunned);
            Check(stunnedW && clientSawStun && !movedWhileStunned, "a perfectly parried stun shot flies back and stuns the Warden (it stands frozen; everyone sees it)");
            Run(s, dt, 2f, () => w.Warden.StunT <= 0f, Clear);
        }

        // The aimed fan: red lanes show where a volley goes before it fires, aimed when they show.
        {
            Run(s, dt, 30f, () => w.Warden.Turn == WardenTurn.Fan && w.Warden.FanWarn > 0f, () => { Clear(); me.Pos = new V2(200f, 800f); });
            float warnAim = w.Warden.FanAim;
            bool clientWarn = false;
            var had = new HashSet<int>(g.Bullets.Keys);
            float fired = -1f; float firedAt = 0f;
            Run(s, dt, 1f, () => fired >= 0f, () =>
            {
                Clear();
                me.Pos = new V2(1400f, 200f);  // dodge after the warning: the volley still goes where it showed
                clientWarn |= cg.Enemies[w.Id].Warden.FanWarn > 0f;
                firedAt += dt;
                foreach (var b in g.Bullets.Values)
                    if (!had.Contains(b.Id) && b.Kind == Defs.PierceShot && fired < 0f) fired = firedAt;
            });
            var volley = g.Bullets.Values.Where(b => !had.Contains(b.Id) && b.Kind == Defs.PierceShot).ToList();
            float off = volley.Count == 0 ? 9f : volley.Min(b => Math.Abs(MathX.Fmod(b.Vel.Angle - warnAim + MathX.Pi, MathX.Tau) - MathX.Pi));
            Console.WriteLine("  warden fan: warned {0:0.00} s ahead (client saw it {1}); {2} shots, the middle {3:0.000} rad off the warned aim", fired, clientWarn, volley.Count, off);
            Check(clientWarn && fired >= Game.FanWarnTime - 0.1f && volley.Count == 9 && off < 0.01f, "the aimed fan shows red lanes before each volley, which goes where they showed");
        }

        // Its own searchlight held on it blinds it: exposed for 3 s, taking 50% more damage and not firing.
        {
            Run(s, dt, 40f, () => w.Warden.Turn == WardenTurn.Light && w.Warden.LightMode == WardenLight.Follow && w.Warden.TurnT > 1f, () => { Clear(); me.Pos = new V2(200f, 800f); });
            Run(s, dt, 1f, () => w.Warden.ExposedT > 0f, () => { Clear(); w.Warden.Light = w.Pos; });
            bool exposed = w.Warden.ExposedT > 0f;
            Run(s, dt, 0.2f, null, Clear);
            var hurtE = typeof(Game).GetMethod("HurtEnemy", flags);
            float h0 = w.Hp;
            hurtE.Invoke(g, new object[] { w, 100f, 1 });
            float took = h0 - w.Hp;
            var had = new HashSet<int>(g.Bullets.Keys);
            bool clientExposed = cg.Enemies[w.Id].Warden.ExposedT > 0f;
            Run(s, dt, 2f, null, () => { Clear(); me.Pos = new V2(200f, 800f); });
            int shots = g.Bullets.Values.Count(b => !had.Contains(b.Id) && Defs.IsEnemyShot(b.Kind) && b.Source == EnemyType.Boss);
            Console.WriteLine("  warden exposed: {0} (client saw it {1}); a 100 hit took {2:0}; boss shots while blinded {3}", exposed, clientExposed, took, shots);
            Check(exposed && clientExposed && Math.Abs(took - 100f * Game.ExposedMult) < 1f && shots == 0,
                "led across the Warden, its own searchlight blinds it: it takes 50% more damage and holds its fire (everyone sees it)");
            Run(s, dt, 2f, () => w.Warden.ExposedT <= 0f, Clear);
        }

        // Every boss's stage change (StageStartBurst) knocks a few small health orbs loose round it.
        {
            var burst = typeof(Game).GetMethod("StageStartBurst", flags);
            var hadOrbs = new HashSet<int>(g.Orbs.Keys);
            burst.Invoke(g, new object[] { w, Rgba.White });
            var dropped = g.Orbs.Values.Where(o => !hadOrbs.Contains(o.Id)).ToList();
            float near = dropped.Count > 0 ? dropped.Min(o => o.Pos.DistanceTo(w.Pos)) : 0f, far = dropped.Count > 0 ? dropped.Max(o => o.Pos.DistanceTo(w.Pos)) : 0f;
            Console.WriteLine("  stage change: {0} small health orbs, {1:0}-{2:0} px from the boss (radius {3:0})", dropped.Count, near, far, w.Radius);
            Check(dropped.Count == Game.StageOrbBase + g.Players.Count && dropped.All(o => !o.Big && !o.Tiny) && near >= w.Radius + 60f,
                "a boss changing stage drops small health orbs (two plus one per player) in a ring just outside it");
            foreach (var o in dropped) g.Orbs.Remove(o.Id);
        }

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
        float heldHost = 0f; int holdFrames = 0; bool heldClient = false;
        var inLane = typeof(Game).GetMethod("InDoorLane", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        int gasInLane = 0; var gateDirs = new HashSet<float>();
        Run(s, dt, 11.5f, null, () =>
        {
            Clear();
            me.Pos = new V2(60f, 60f);
            gates = Math.Max(gates, w.Warden.Gates.Count);
            clientGate |= cg.Enemies[w.Id].Warden.Gates.Count > 0;
            foreach (var pt in g.Patches.Values) if (pt.Kind == PatchKind.Gas && seenGas.Add(pt.Id) && (bool)inLane.Invoke(null, new object[] { w.Warden, pt.Pos.X })) gasInLane++;
            foreach (var gt in w.Warden.Gates) gateDirs.Add(gt.Dir);
            if (!startSet && w.Warden.Gates.Count > 0) { cg.Players[2].Pos = new V2(800f, 700f); friendStart = 700f; startSet = true; }
            if (startSet && cg.Players[2].Pos.Y > friendStart + 30f) pushed = true;
            foreach (var gt in w.Warden.Gates)
                if (Game.GateHolding(gt)) { heldHost = Math.Max(heldHost, Math.Abs(gt.Front - (gt.Dir > 0f ? 6f : Game.Height - 6f))); holdFrames++; }
            foreach (var gt in cg.Enemies[w.Id].Warden.Gates)
                if (Game.GateHolding(gt)) heldClient = true;
        });
        float gateDmg = friend.Run.DamageTaken - taken0;
        Console.WriteLine("  warden lockdown: {0} gates at once, {1} gas clouds, friend pushed {2}, friend took {3:0} (gates and gas)", gates, seenGas.Count, pushed, gateDmg);
        Check(gates >= 1 && clientGate && seenGas.Count == 2, "Lockdown: gates sweep across (everyone sees them) with gas from the 1st and 3rd");
        Check(pushed && gateDmg > 0f, "a gate shoves you along on your own game, and hurts");
        Console.WriteLine("  warden gate stop: {0:0.0} px in, {1:0.00} s per gate, client saw it {2}", heldHost, holdFrames * dt / 3f, heldClient);
        Check(Math.Abs(heldHost - Game.GateHoldIn) < 1f && holdFrames * dt >= Game.GateHold * 2f && heldClient,
            "each gate stops just inside the arena for a moment before it carries on (seen on client)");
        Console.WriteLine("  warden gates: from {0} edge(s); gas clouds over a door's lane {1}", gateDirs.Count, gasInLane);
        Check(gateDirs.Count == 1, "a cross turn's gates all come from the same edge, so two never close in on you");
        Check(gasInLane == 0, "tear gas never lands across the doors (or the lock) of the gate it comes with");

        // The tear gas barrage: two volleys of canisters in patterns, slow aimed bursts between.
        {
            Run(s, dt, 6f, () => w.Warden.Turn == WardenTurn.Gas, () => { Clear(); me.Pos = new V2(60f, 60f); });
            var cloudsBefore = new HashSet<int>(g.Patches.Keys);
            var hadShots = new HashSet<int>(g.Bullets.Keys);
            int bursts = 0;
            Run(s, dt, 6f, null, () =>
            {
                Clear(); me.Pos = new V2(60f, 60f);
                foreach (var b in g.Bullets.Values) if (hadShots.Add(b.Id) && b.Source == EnemyType.Boss) bursts++;
            });
            var clouds = g.Patches.Values.Where(pt => pt.Kind == PatchKind.Gas && !cloudsBefore.Contains(pt.Id)).ToList();
            int clientClouds = cg.Patches.Values.Count(pt => pt.Kind == PatchKind.Gas);
            Console.WriteLine("  warden gas barrage: {0} clouds (client sees {1}), {2} boss shots", clouds.Count, clientClouds, bursts);
            Check(w.Warden.Stage == WardenStage.Lockdown && clouds.Count >= 8 && clientClouds > 0 && bursts >= 6,
                "Lockdown's tear gas barrage: two volleys of canisters in patterns (everyone sees them), with aimed bursts between");
        }

        // Each gate has a gold lock: six of your shots break it, opening a third door in the middle.
        {
            Run(s, dt, 12f, () => w.Warden.Turn == WardenTurn.Cross && w.Warden.Gates.Any(gt => gt.Warn <= 0f), () => { Clear(); me.Pos = new V2(60f, 60f); });
            var gate = w.Warden.Gates.First(gt => gt.Warn <= 0f);
            var spawnShot = typeof(Game).GetMethod("SpawnBullet", flags);
            bool blockedBefore = !Game.InGateGap(gate, gate.LockX);
            for (int k = 0; k < Game.LockHits; k++)
            {
                V2 at = new V2(gate.LockX, (gate.Front + Game.GateBack(gate)) / 2f);
                spawnShot.Invoke(g, new object[] { Defs.DpsShot, at, new V2(0f, 10f), 0f, 0.5f, 5f, 1, false, 0f });
                Run(s, dt, 0.05f, null, Clear);
            }
            Run(s, dt, 0.3f, null, Clear);
            var cgate = cg.Enemies[w.Id].Warden.Gates.FirstOrDefault(gt => Math.Abs(gt.LockX - gate.LockX) < 1f);
            Console.WriteLine("  warden gate lock: open {0} (client sees it {1}); hits left {2}", gate.LockOpen, cgate != null && cgate.LockOpen, gate.LockHp);
            Check(blockedBefore && gate.LockOpen && Game.InGateGap(gate, gate.LockX) && cgate != null && cgate.LockOpen,
                "six shots break a gate's gold lock, opening a third door in the middle (everyone sees it)");
        }

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

        // ...but only up to RioterCap a riot turn: past that, it bats them away unharmed.
        var spawnR = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var reach = typeof(Game).GetMethod("RiotersReachWarden", flags);
        w.Warden.RioterDealt = w.MaxHp * 0.05f;
        var late = (Enemy)spawnR.Invoke(g, new object[] { EnemyType.Rioter, w.Pos, 0f, 0, false });
        late.SpawnT = 0f;
        float hpCap = w.Hp;
        reach.Invoke(g, new object[] { w });
        Check(w.Hp == hpCap && !g.Enemies.ContainsKey(late.Id), "past the rioters' cap, one that reaches the Warden is batted away and doesn't hurt it");

        // Its riot shield wears from the front, then breaks: no more blocking, and it takes 50% more for the rest of the turn.
        var wear = typeof(Game).GetMethod("WearShield", flags);
        var hurtW = typeof(Game).GetMethod("HurtEnemy", flags);
        float shieldMax = w.Warden.RiotShieldMax;
        wear.Invoke(g, new object[] { w, shieldMax * 0.6f });
        bool cracked = Game.WardenShieldCrack(w.Warden) == 2 && (bool)blocks.Invoke(g, new object[] { w, front });
        wear.Invoke(g, new object[] { w, shieldMax });
        float hpB = w.Hp;
        hurtW.Invoke(g, new object[] { w, 10f, 0 });
        Run(s, dt, 0.2f, null, () => { me.Pos = new V2(60f, 60f); friend.Pos = new V2(1540f, 60f); });
        var cw = cg.Enemies[w.Id].Warden;
        Console.WriteLine("  warden riot shield: {0:0} strong, cracked at 60% {1}, broken {2} (client {3}), a 10 hit took {4:0.0}", shieldMax, cracked, w.Warden.RiotShield <= 0f, cw.RiotShield <= 0f, hpB - w.Hp);
        Check(shieldMax > 0f && cracked && w.Warden.RiotShield <= 0f && !(bool)blocks.Invoke(g, new object[] { w, front }) && Math.Abs(hpB - w.Hp - 15f) < 0.01f && cw.RiotShield <= 0f,
            "the riot shield cracks as it wears, then breaks: it stops blocking and the Warden takes 50% more (everyone sees it)");

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
        Run(s, dt, 0.5f, null, Clear);
        Check(w.Warden.LightMode == WardenLight.Follow && cg.Enemies[w.Id].Warden.LightMode == WardenLight.Follow && cg.Enemies[w.Id].Warden.Stage == WardenStage.LastStand,
            "the last stand sounds the alarm: the lights go out and its searchlight hunts you (everyone sees it)");
        s[0].Leave(""); s[1].Leave("");
    }
}

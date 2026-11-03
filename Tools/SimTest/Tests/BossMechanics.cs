using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Boss odds and ends: the Singularity's cage, the laser debug report, boss sound cues and every boss's mechanics sweep.
static partial class Program
{
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
        lab.Seats[0].Role = Role.Healer; lab.Seats[0].Kit = Kit.Elementalist;
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
            s[0].Game.ScaleBosses = false;  // 25 damage stacks: a quick kill, not the tougher Queen such a party would meet
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
        // Hive Queen's loop: shooting drones (shielded until they're all dead), then explosive drones that swoop out to
        // a player's side and lock onto where they are as they turn in, then her bullet patterns, then again.
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
                    if (e.Phase == 1 && !lastPos.ContainsKey(e.Id)) launched++;
                    if (e.Phase != 1 && !aimedAt.ContainsKey(e.Id))
                    {
                        aimedAt[e.Id] = e.Waypoint;  // the spot it locked onto as it turned in
                        foreach (var p in s[0].Game.Players.Values)
                            if (p.Pos.DistanceTo(e.Waypoint) < 15f) { aimedRight++; break; }  // within a frame or two of running
                    }
                    lastPos[e.Id] = e.Pos;
                }
                foreach (var id in new List<int>(lastPos.Keys))
                    if (!s[0].Game.Enemies.ContainsKey(id))
                    {
                        V2 spot;
                        if (aimedAt.TryGetValue(id, out spot) && lastPos[id].DistanceTo(spot) < 25f) onTarget++; else offTarget++;
                        lastPos.Remove(id);
                    }
                foreach (var b in s[0].Game.Bullets.Values)
                    if (b.Kind == Defs.PierceShot && b.Source == EnemyType.HiveQueen) { fans++; break; }
                clientBomber |= Find(s[1], EnemyType.HiveBomber) != null;
                foreach (var fx in s[1].Game.Effects) clientMarker |= fx.Kind == FxKind.Target;
            });
            Console.WriteLine("  queen loop: stages {0}; {1} bombers launched ({2} locked onto a player's spot), {3} blew up on their spot, {4} elsewhere (on a player on the way); running players lost {5:0} HP to them; fans {6}",
                string.Join(">", stages), launched, aimedRight, onTarget, offTarget, hpBefore - hpAfter, fans);
            Check(shieldUp && !bombersEarly, "the Hive Queen opens with shooting drones and is shielded while they live");
            Check(stages.Count >= 5 && stages[0] == 1 && stages[1] == 2 && stages[2] == 3 && stages[3] == 4 && resummoned, "the Hive Queen loops, one turn at a time: drones, bombers, fan, rings, then drones again");
            // (one that runs into a player while it's still swooping out blows up there, before it locks on)
            Check(launched >= 8 && aimedAt.Count >= launched / 2 && aimedRight == aimedAt.Count && onTarget >= aimedAt.Count / 2 && clientBomber && clientMarker,
                "Hive bombers swoop to a player's side, lock onto where they are and blow up there (seen on client, with a marker)");
            Check(hpBefore - hpAfter < launched * 22f * 0.5f && fans > 0, "keeping on the move dodges most of the bombers; her bullet patterns follow");
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

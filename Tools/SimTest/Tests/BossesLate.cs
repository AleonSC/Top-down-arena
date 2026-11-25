using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The later bosses: Lich, Singularity, Storm Herald, Devourer, Eclipse and Colossus.
static partial class Program
{
    // The Singularity's reworked fight: corner wells, Pull -> Collapse -> Normal (spiral + sweeping lasers) -> Cage.
    // The Lich in three phases and a last stand (Lich.cs): its phylacteries' guard and window, the turns of each phase,
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

        // 65%: it changes phase (can't be hurt, screen cleared), then Soul Harvest with three phylacteries.
        foreach (var j in Jars()) j.Hp = 0f;
        Run(s, dt, 0.3f, null, safe);
        lich.Hp = lich.MaxHp * 0.6f;
        Run(s, dt, 0.2f, null, safe);
        hp = lich.Hp; hurt.Invoke(g, new object[] { lich, 300f, 1 });
        bool shift = lich.Lich.TransT > 0f && lich.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Lich);
        Run(s, dt, 3f, () => lich.Lich.Stage == LichStage.Harvest && lich.Lich.TransT <= 0f, safe);
        Run(s, dt, 1.2f, null, safe);
        Console.WriteLine("  phase change: immune {0}; now {1} (client {2}); phylacteries {3}", shift, lich.Lich.Stage, CL()?.Lich?.Stage, Jars().Count);
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

        // Killed wells stay gone for the rest of the phase.
        foreach (var w in Wells()) w.Hp = 0f;
        Run(s, dt, 20f, null, safe);
        Console.WriteLine("  wells 20 s after killing them all: {0}", Wells().Count);
        Check(Wells().Count == 0 && sing.Sing.Stage == SingStage.EventHorizon, "killed gravity wells don't come back until the next stage");

        // 65%: it changes phase (can't be hurt, the screen clears), then Accretion with four fresh wells and the disk.
        sing.Hp = sing.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        float hp = sing.Hp; hurt.Invoke(g, new object[] { sing, 500f, 1 });
        bool immune = sing.Sing.TransT > 0f && sing.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Singularity);  // (its Grunts and Snipers keep shooting)
        Run(s, dt, 3f, () => sing.Sing.Stage == SingStage.Accretion && sing.Sing.TransT <= 0f, safe);
        Run(s, dt, 1f, null, safe);
        var cs = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Singularity);
        Console.WriteLine("  phase change: immune {0}; now {1}, client sees {2} with the disk {3}; wells {4}", immune, sing.Sing.Stage, cs?.Sing?.Stage, cs != null && Game.DiskUp(cs), Wells().Count);
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
        Run(s, dt, 6f, () => sing.Phase == Game.SingSplit && g.Enemies.Values.Count(e => e.Type == EnemyType.SingularityShard && !g.IsSpawning(e)) == 3, safe);
        var shard = g.Enemies.Values.First(e => e.Type == EnemyType.SingularityShard);
        float h0 = sing.Hp; hurt.Invoke(g, new object[] { sing, 300f, 1 });
        bool coreImmune = sing.Hp == h0;
        float direct = h0 - sing.Hp;
        hurt.Invoke(g, new object[] { shard, 300f, 1 });
        Console.WriteLine("  split: {0} shards, core hit directly {1:0}, through a shard {2:0}", g.Enemies.Values.Count(e => e.Type == EnemyType.SingularityShard), direct, h0 - sing.Hp);
        Check(coreImmune && sing.Hp < h0 - 200f, "Split: three shards; the core can't be hurt, the shards pass their damage to it");

        // Below 10%: the last stand. The horizon swells, the pull doubles, and rings of big orbs.
        sing.Hp = sing.MaxHp * 0.08f;
        Run(s, dt, 10f, () => sing.Sing.Stage == SingStage.LastStand, safe);
        bool rings = false;
        Run(s, dt, 3f, null, () => { safe(); rings |= g.Bullets.Values.Count(b => b.Kind == Defs.BigShot && b.Source == EnemyType.Singularity) >= 16; });
        Console.WriteLine("  last stand: horizon {0:0}, pull {1:0}, orb rings {2}, shards left {3}", sing.Sing.Horizon, g.PullStrength, rings, g.Enemies.Values.Count(e => e.Type == EnemyType.SingularityShard));
        Check(sing.Sing.Stage == SingStage.LastStand && sing.Sing.Horizon > 170f && g.PullStrength > 100f && rings, "below 10%: the last stand (a bigger horizon, a stronger pull, orb rings)");
        s[0].Leave(""); s[1].Leave("");
    }

    // The Storm Herald's three phases and last stand (Herald.cs), built from Docs/Mockups/storm-herald-3-phase.html.
    static void HeraldTest(float dt)
    {
        var s = StartBuffed(dt, 40, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var hurt = typeof(Game).GetMethod("HurtEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        // Both players held in place on both games (each game moves its own player).
        Action<V2, V2> Place = (a, b) => { g.Players[1].Pos = c.Players[1].Pos = a; g.Players[2].Pos = c.Players[2].Pos = b; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.StormHerald) != null && !g.IsSpawning(Find(s[0], EnemyType.StormHerald)));
        var h = Find(s[0], EnemyType.StormHerald);
        foreach (var e in g.Enemies.Values) if (e != h) e.Hp = 0f;  // just the boss
        var me = g.Players[1];

        // Gathering Storm: Strikes, Lightning rods, Thunderclap, breathers between; the volley only in breathers.
        var turns = new List<HeraldTurn>();
        int strikes = 0, rods = 0, clientRods = 0, clientArcs = 0, clap = 0;
        bool volleyOutside = false;
        Run(s, dt, 22f, null, () =>
        {
            safe(); Place(new V2(700f, 600f), new V2(1000f, 650f));
            var hs = h.Herald;
            if (hs == null) return;
            if (turns.Count == 0 || turns[turns.Count - 1] != hs.Turn) turns.Add(hs.Turn);
            if (hs.Turn == HeraldTurn.Strikes) strikes = Math.Max(strikes, g.Hazards.Count);
            if (hs.Turn == HeraldTurn.Rods) rods = Math.Max(rods, hs.Rods.Count);
            var ch = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.StormHerald)?.Herald;
            if (ch != null) { clientRods = Math.Max(clientRods, ch.Rods.Count); clientArcs = Math.Max(clientArcs, ch.Arcs.Count); }
            if (hs.Turn == HeraldTurn.Thunderclap) clap = Math.Max(clap, g.Bullets.Values.Count(b => b.Source == EnemyType.StormHerald && b.Kind == Defs.SmallShot));
            if (hs.Turn != HeraldTurn.Rest && hs.Turn != HeraldTurn.Crosswind && g.Bullets.Values.Any(b => b.Source == EnemyType.StormHerald && b.Kind == Defs.PierceShot && b.Life > 7.9f)) volleyOutside = true;
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  gathering storm: {0}; strikes up {1}, rods {2} (client {3}, arcs {4}), thunderclap shots {5}", order, strikes, rods, clientRods, clientArcs, clap);
        Check(h.Herald.Stage == HeraldStage.Gathering && order.Contains("Strikes > Rest > Rods > Rest > Thunderclap > Rest"), "Gathering Storm: Strikes, Lightning rods, Thunderclap, with breathers between");
        Check(strikes >= 4 && rods == 4 && clientRods == 4 && clientArcs >= 1, "strikes on both players and two random spots; four rods with arcs between them (seen on the client)");
        Check(clap >= 28 && !volleyOutside, "thunderclap rings with gaps; the piercing volley only in breathers");

        // A rod arc hurts whoever stands on its line as it fires.
        Run(s, dt, 25f, () => h.Herald.Turn == HeraldTurn.Rods && h.Herald.Rods.Count == 4 && h.Herald.Rods.All(r => r.Fall <= 0f), () => { safe(); Place(new V2(700f, 600f), new V2(1000f, 650f)); });
        V2 mid = (h.Herald.Rods[0].Pos + h.Herald.Rods[1].Pos) / 2f;
        float taken = me.Run.DamageTaken;
        Run(s, dt, 5f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            Place(mid, new V2(1500f, 100f));
            g.Hazards.Clear(); foreach (var b in g.Bullets.Values) b.Life = 0f;
        });
        Console.WriteLine("  standing between two rods: hurt {0}", me.Run.DamageTaken > taken);
        Check(me.Run.DamageTaken > taken, "a rod arc hurts a player on its line");

        // 65%: it changes phase (can't be hurt, the screen clears), then the Gale with the wind.
        h.Hp = h.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        float hp = h.Hp; hurt.Invoke(g, new object[] { h, 500f, 1 });
        bool immune = h.Herald.TransT > 0f && h.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.StormHerald) && g.Hazards.Count == 0;
        Run(s, dt, 3f, () => h.Herald.Stage == HeraldStage.Gale && h.Herald.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        var chs = c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.StormHerald)?.Herald;
        Console.WriteLine("  phase change: immune {0}; now {1} (client {2}), wind {3} (client {4})", immune, h.Herald.Stage, chs?.Stage, h.Herald.Wind, chs?.Wind);
        Check(immune && h.Herald.Stage == HeraldStage.Gale && chs?.Stage == HeraldStage.Gale, "at 65% it changes stage (can't be hurt, screen cleared), then the Gale (seen on the client)");
        Check(h.Herald.Wind != 0 && chs?.Wind == h.Herald.Wind, "the wind blows, and the client knows which way");

        // The wind pushes each player on their own game, and bends bullets.
        V2 before = c.Players[2].Pos;
        float windDir = h.Herald.Wind;
        for (int f = 0; f < 30; f++) { safe(); g.Players[1].Pos = c.Players[1].Pos = new V2(700f, 600f); Step(s[0], dt); Step(s[1], dt); Thread.Sleep(1); }
        float pushed = (c.Players[2].Pos.X - before.X) * windDir;
        Console.WriteLine("  wind: the client's player drifted {0:0} px downwind in 0.5 s", pushed);
        Check(pushed > 20f && pushed < 50f, "the wind pushes players about 70 px/s");

        // The Gale's turns: Crosswind, Ball lightning (orbs the client sees, which pop after 6 s), Downburst rows.
        int volley5 = 0, balls = 0, clientBalls = 0, row = 0;
        bool popped = false;
        var turns2 = new List<HeraldTurn>();
        Run(s, dt, 23f, null, () =>
        {
            safe(); Place(new V2(700f, 600f), new V2(1000f, 650f));
            var hs = h.Herald;
            if (turns2.Count == 0 || turns2[turns2.Count - 1] != hs.Turn) turns2.Add(hs.Turn);
            if (hs.Turn == HeraldTurn.Crosswind) volley5 = Math.Max(volley5, g.Bullets.Values.Count(b => b.Source == EnemyType.StormHerald && b.Kind == Defs.PierceShot && b.Life > 7.95f));
            int nb = g.Enemies.Values.Count(e => e.Type == EnemyType.BallLightning);
            if (balls > 0 && nb < balls) popped = true;
            balls = Math.Max(balls, nb);
            clientBalls = Math.Max(clientBalls, c.Enemies.Values.Count(e => e.Type == EnemyType.BallLightning));
            if (hs.Turn == HeraldTurn.Downburst) row = Math.Max(row, g.Hazards.Values.GroupBy(z => (int)z.Pos.Y).Select(z => z.Count()).DefaultIfEmpty(0).Max());
        });
        Console.WriteLine("  gale: {0}; volley {1}, ball lightning {2} (client {3}, popped {4}), downburst row {5}", string.Join(" > ", turns2), volley5, balls, clientBalls, popped, row);
        Check(volley5 == 5, "Crosswind: 5-shot piercing volleys");
        Check(balls >= 3 && clientBalls >= 3 && popped, "Ball lightning: orbs drift at you (seen on the client) and pop");
        Check(row >= 5, "Downburst: a row of big strikes across the arena");

        // 30%: the Supercell. The Conductor marks a player; when it runs out, an ally inside the ring is hit too.
        h.Hp = h.MaxHp * 0.25f;
        Run(s, dt, 3f, () => h.Herald.Stage == HeraldStage.Supercell && h.Herald.TransT <= 0f, safe);
        Run(s, dt, 6f, () => h.Herald.CondId != 0, () => { safe(); Place(new V2(700f, 600f), new V2(780f, 600f)); });
        int cond = h.Herald.CondId, clientCond = c.Enemies.Values.First(e => e.Type == EnemyType.StormHerald).Herald.CondId;
        var other = g.Players.Values.First(p => p.Id != cond);
        float otherTaken = other.Run.DamageTaken;
        Run(s, dt, 3.5f, () => h.Herald.CondId == 0, () =>
        {
            Place(new V2(700f, 600f), new V2(780f, 600f));
            g.Hazards.Clear(); foreach (var b in g.Bullets.Values) b.Life = 0f;
            foreach (var p in g.Players.Values) p.InvulnT = p.Id == cond ? 1f : 0f;
        });
        Run(s, dt, 0.1f, null, safe);
        Console.WriteLine("  conductor: player {0} marked (client sees {1}); the ally 80 px away took {2:0}", cond, clientCond, other.Run.DamageTaken - otherTaken);
        Check(cond != 0 && clientCond == cond, "the Conductor marks one player (seen on the client)");
        Check(other.Run.DamageTaken > otherTaken, "the charge jumps to an ally inside the ring");

        // Storm front columns and the rod cage.
        int column = 0, cage = 0;
        bool openSide = false;
        Run(s, dt, 24f, null, () =>
        {
            safe(); Place(new V2(700f, 500f), new V2(1100f, 500f));
            var hs = h.Herald;
            if (hs.Turn == HeraldTurn.StormFront) column = Math.Max(column, g.Hazards.Values.GroupBy(z => (int)z.Pos.X).Select(z => z.Count()).DefaultIfEmpty(0).Max());
            if (hs.Turn == HeraldTurn.RodCage) { cage = Math.Max(cage, hs.Rods.Count); if (hs.Arcs.Count >= 5) openSide |= hs.Arcs.Count == 5; }
        });
        Console.WriteLine("  supercell: storm front column {0}, rod cage rods {1}, one open side {2}", column, cage, openSide);
        Check(column == 5, "Storm front: columns of five strikes with a two-strike gap");
        Check(cage == 6 && openSide, "Rod cage: six rods around you, arcs on all sides but one");

        // Below 10%: the last stand in the middle; the spokes hurt.
        h.Hp = h.MaxHp * 0.08f;
        Run(s, dt, 6f, () => h.Herald.Stage == HeraldStage.LastStand && h.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 40f, safe);
        bool middle = h.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 40f;
        taken = me.Run.DamageTaken;
        Run(s, dt, 3f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            V2 on = h.Pos + V2.FromAngle(h.Herald.Spin) * 250f;
            Place(on, new V2(100f, 100f));
            g.Hazards.Clear(); foreach (var b in g.Bullets.Values) b.Life = 0f;
        });
        Console.WriteLine("  last stand: {0}, in the middle {1}, a spoke hurt the player {2}", h.Herald.Stage, middle, me.Run.DamageTaken > taken);
        Check(h.Herald.Stage == HeraldStage.LastStand && middle && me.Run.DamageTaken > taken, "below 10%: the last stand in the middle, its turning spokes hurt");
        s[0].Leave(""); s[1].Leave("");
    }

    // The Devourer (Devourer.cs): Appetite, Gorging, Ravenous, then the last meal.
    static void DevourerTest(float dt)
    {
        var s = StartBuffed(dt, 45, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var blocks = typeof(Game).GetMethod("BlocksShot", flags);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Action clear = () => { foreach (var b in g.Bullets.Values) b.Life = 0f; };
        Action<V2, V2> Place = (a, b) => { g.Players[1].Pos = c.Players[1].Pos = a; g.Players[2].Pos = c.Players[2].Pos = b; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Devourer) != null && !g.IsSpawning(Find(s[0], EnemyType.Devourer)));
        var h = Find(s[0], EnemyType.Devourer);
        foreach (var e in g.Enemies.Values) if (e != h) e.Hp = 0f;  // just the boss
        var me = g.Players[1];
        Func<DevourState> cs = () => c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Devourer)?.Devour;

        // Appetite: Spiral, Snap, Hungry, breathers between; lunges and morsels seen on the client.
        var turns = new List<DevourTurn>();
        int spiral = 0, morsels = 0, clientMorsels = 0;
        bool clientLunge = false, clientHungry = false, restQuiet = true;
        Run(s, dt, 21f, null, () =>
        {
            safe(); Place(new V2(500f, 650f), new V2(1100f, 650f));
            var ds = h.Devour;
            if (ds == null) return;
            if (turns.Count == 0 || turns[turns.Count - 1] != ds.Turn) turns.Add(ds.Turn);
            if (ds.Turn == DevourTurn.Spiral) spiral = Math.Max(spiral, g.Bullets.Values.Count(b => b.Source == EnemyType.Devourer && b.Kind == Defs.SmallShot));
            if (ds.Turn == DevourTurn.Rest && ds.TurnT > 0.5f && g.Bullets.Values.Any(b => b.Source == EnemyType.Devourer && b.Life > 7.9f)) restQuiet = false;
            morsels = Math.Max(morsels, g.Enemies.Values.Count(e => e.Type == EnemyType.Morsel));
            clientMorsels = Math.Max(clientMorsels, c.Enemies.Values.Count(e => e.Type == EnemyType.Morsel));
            var cd = cs();
            if (cd != null) clientLunge |= cd.Lunging;
            clientHungry |= c.Enemies.Values.Any(e => e.Type == EnemyType.Devourer && Game.Hungry(e));
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  appetite: {0}; spiral shots {1}, morsels {2} (client {3}), lunge seen on client {4}, hungry on client {5}", order, spiral, morsels, clientMorsels, clientLunge, clientHungry);
        Check(h.Devour.Stage == DevourStage.Appetite && order.Contains("Spiral > Rest > Snap > Rest > Hungry > Rest"), "Appetite: Spiral, Snap, Hungry, with breathers between");
        Check(spiral >= 40 && restQuiet, "the old double spiral; breathers fire nothing");
        Check(morsels == 4 && clientMorsels == 4 && clientLunge && clientHungry, "Snap lunges and Hungry's four morsels (seen on the client)");

        // A Snap bite hurts a player who stands still on its line.
        Run(s, dt, 20f, () => h.Devour.Turn == DevourTurn.Snap, () => { safe(); Place(new V2(500f, 650f), new V2(1100f, 650f)); });
        float taken = me.Run.DamageTaken;
        Run(s, dt, 5.5f, () => h.Devour.Turn != DevourTurn.Snap, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            Place(new V2(500f, 650f), new V2(1100f, 650f)); clear();
        });
        Console.WriteLine("  snap: the player standing still took {0:0}", me.Run.DamageTaken - taken);
        Check(me.Run.DamageTaken - taken >= 30f, "Snap: a lunging bite hurts whoever stays on the line");

        // Hungry: shots heal it; a morsel stepped on pops; one it reaches heals it 3%.
        Run(s, dt, 20f, () => h.Devour.Turn == DevourTurn.Hungry && g.Enemies.Values.Count(e => e.Type == EnemyType.Morsel && !g.IsSpawning(e)) == 4, () => { safe(); Place(new V2(500f, 650f), new V2(1100f, 650f)); });
        h.Hp = h.MaxHp * 0.9f;
        float hp = h.Hp;
        hurt.Invoke(g, new object[] { h, 100f, 1 });
        bool healed = h.Hp > hp + 40f;
        var ms = g.Enemies.Values.Where(e => e.Type == EnemyType.Morsel).OrderBy(e => e.Pos.DistanceTo(h.Pos)).ToList();
        var stepOn = ms[ms.Count - 1];
        Run(s, dt, 0.3f, null, () => { safe(); Place(stepOn.Pos, new V2(1100f, 650f)); });
        bool popped = !g.Enemies.ContainsKey(stepOn.Id);
        var eat = ms[0];
        hp = h.Hp;
        Run(s, dt, 0.3f, () => !g.Enemies.ContainsKey(eat.Id), () => { safe(); Place(new V2(500f, 650f), new V2(1100f, 650f)); if (g.Enemies.ContainsKey(eat.Id)) eat.Pos = h.Pos + new V2(20f, 0f); });
        float ate = (h.Hp - hp) / h.MaxHp;
        Console.WriteLine("  hungry: a shot healed it {0}; stepped-on morsel popped {1}; eating one healed {2:0.0}%", healed, popped, ate * 100f);
        Check(healed && popped && ate > Game.MorselEat * 0.9f, "Hungry: shots heal it; morsels pop underfoot, and each it eats heals it a little");

        // 65%: it changes phase (can't be hurt, the screen clears), then the Gorging, bigger.
        h.Hp = h.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        hp = h.Hp; hurt.Invoke(g, new object[] { h, 500f, 1 });
        bool immune = h.Devour.TransT > 0f && h.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Devourer) && !g.Enemies.Values.Any(e => e.Type == EnemyType.Morsel);
        Run(s, dt, 3f, () => h.Devour.Stage == DevourStage.Gorging && h.Devour.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        var ch = c.Enemies.Values.First(e => e.Type == EnemyType.Devourer);
        Console.WriteLine("  phase change: immune {0}; now {1} (client {2}), radius {3} (client {4})", immune, h.Devour.Stage, ch.Devour?.Stage, h.Radius, ch.Radius);
        Check(immune && h.Devour.Stage == DevourStage.Gorging && ch.Devour?.Stage == DevourStage.Gorging, "at 65% it changes stage (can't be hurt, screen cleared), then the Gorging (seen on the client)");
        Check(h.Radius > 57f && ch.Radius > 57f, "it's bigger in the Gorging, on both games");

        // Inhale: the cone pulls your own player in; reaching the mouth means being swallowed.
        Run(s, dt, 22f, () => h.Devour.Turn == DevourTurn.Inhale && h.Devour.InhaleT > 1.7f, () => { safe(); Place(new V2(300f, 150f), new V2(1300f, 150f)); });
        V2 dir = V2.FromAngle(h.Facing), start = h.Pos + dir * 230f;
        Place(new V2(100f, 100f), start);
        for (int f = 0; f < 20; f++) { safe(); g.Players[1].Pos = c.Players[1].Pos = new V2(100f, 100f); Step(s[0], dt); Step(s[1], dt); Thread.Sleep(1); }
        float pulled = start.DistanceTo(h.Pos) - c.Players[2].Pos.DistanceTo(h.Pos);
        Run(s, dt, 3f, () => h.Devour.SwallowId != 0, () => { safe(); g.Players[1].Pos = c.Players[1].Pos = new V2(100f, 100f); clear(); });
        var friend = g.Players[2];
        Run(s, dt, 0.4f, null, () => { safe(); g.Players[1].Pos = c.Players[1].Pos = new V2(100f, 100f); });
        bool swallowed = h.Devour.SwallowId == 2 && friend.SwallowT > 0f && c.Players[2].SwallowT > 0f && cs()?.SwallowId == 2;
        bool pinned = c.Players[2].Pos.DistanceTo(ch.Pos) < 30f;
        float friendTaken = friend.Run.DamageTaken;
        Run(s, dt, 1.1f, null, () => { g.Players[1].Pos = c.Players[1].Pos = new V2(100f, 100f); g.Players[1].InvulnT = 1f; });
        bool stomach = friend.Run.DamageTaken > friendTaken;
        hp = h.Hp; hurt.Invoke(g, new object[] { h, 100f, 1 });
        float doubled = hp - h.Hp;
        hurt.Invoke(g, new object[] { h, 100f, 1 });
        Run(s, dt, 0.5f, () => h.Devour.SwallowId == 0, safe);
        Run(s, dt, 0.4f, null, safe);
        Console.WriteLine("  inhale: pulled {0:0} px in 1/3 s on the client's own game; swallowed {1} (pinned {2}), hurt inside {3}; an ally's hit did {4:0} for 100; spat out {5} ({6:0} px away on the client)",
            pulled, swallowed, pinned, stomach, doubled, h.Devour.SwallowId == 0, c.Players[2].Pos.DistanceTo(ch.Pos));
        Check(pulled > 25f, "Inhale: the cone pulls your own player towards its mouth");
        Check(swallowed && pinned && stomach, "reaching the mouth swallows you (seen on the client, held inside it, hurt each second)");
        Check(doubled > 190f && h.Devour.SwallowId == 0 && c.Players[2].SwallowT <= 0f && c.Players[2].Pos.DistanceTo(ch.Pos) > 150f, "allies' hits do double, and 300 damage spits the player out");

        // Bile: pools seen on the client hurt whoever stands in them.
        Run(s, dt, 20f, () => h.Devour.Turn == DevourTurn.Bile && g.Patches.Values.Any(p => p.Kind == PatchKind.Bile && p.Active), () => { safe(); Place(new V2(300f, 150f), new V2(1300f, 750f)); });
        var pool = g.Patches.Values.First(p => p.Kind == PatchKind.Bile && p.Active);
        int clientPools = c.Patches.Values.Count(p => p.Kind == PatchKind.Bile);
        taken = me.Run.DamageTaken;
        me.InvulnT = 0f;
        Run(s, dt, 0.8f, null, () => { foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f; Place(pool.Pos, new V2(1300f, 750f)); clear(); });
        Console.WriteLine("  bile: client pools {0}; standing in one hurt {1:0}", clientPools, me.Run.DamageTaken - taken);
        Check(clientPools > 0 && me.Run.DamageTaken > taken, "Bile: pools of acid (seen on the client) hurt whoever stands in them");

        // Burrow: it can't be hurt underground; it surfaces under you.
        Run(s, dt, 20f, () => h.Devour.BurrowT > 0.2f, () => { safe(); Place(new V2(300f, 150f), new V2(1300f, 750f)); });
        hp = h.Hp; hurt.Invoke(g, new object[] { h, 300f, 1 });
        bool under = h.Hp == hp && cs()?.BurrowT > 0f;
        Run(s, dt, 3f, () => h.Devour.BurrowT >= Game.BurrowChase, () => { safe(); Place(new V2(300f, 150f), new V2(1300f, 750f)); });
        taken = me.Run.DamageTaken;
        me.InvulnT = 0f;
        V2 spot = h.Pos;
        Run(s, dt, 1f, () => h.Devour.BurrowT <= 0f && me.Run.DamageTaken > taken, () => { foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f; Place(spot, new V2(1300f, 750f)); });
        Console.WriteLine("  burrow: immune underground {0} (client sees it {1}); erupting under a player hurt {2:0}", h.Hp == hp, under, me.Run.DamageTaken - taken);
        Check(under && me.Run.DamageTaken - taken >= 30f, "Burrow: underground it can't be hurt; it erupts under you");

        // 30%: the Ravenous. The Maw heals it from the front, hurts it more from behind.
        h.Hp = h.MaxHp * 0.25f;
        Run(s, dt, 4f, () => h.Devour.Stage == DevourStage.Ravenous && h.Devour.TransT <= 0f, safe);
        Run(s, dt, 0.3f, null, () => { safe(); Place(new V2(300f, 150f), new V2(1300f, 750f)); });
        V2 face = V2.FromAngle(h.Facing);
        hp = h.Hp;
        if (!(bool)blocks.Invoke(g, new object[] { h, h.Pos + face * 200f })) hurt.Invoke(g, new object[] { h, 100f, 1 });
        float front = h.Hp - hp;
        hp = h.Hp;
        if (!(bool)blocks.Invoke(g, new object[] { h, h.Pos - face * 200f })) hurt.Invoke(g, new object[] { h, 100f, 1 });
        float back = hp - h.Hp;
        Console.WriteLine("  maw: a 100 hit from the front healed {0:0}; from behind it did {1:0}", front, back);
        Check(front > 40f && back > 120f, "the Maw: shots into its mouth heal it, shots on its back do x1.25");

        // Frenzy lunges, Jaws (14 teeth), Vomit (a spray of bile).
        int teeth = 0, vomit = 0, frenzy = 0;
        var turns3 = new List<DevourTurn>();
        Run(s, dt, 21f, null, () =>
        {
            safe(); Place(new V2(500f, 450f), new V2(1100f, 450f));
            var ds = h.Devour;
            if (turns3.Count == 0 || turns3[turns3.Count - 1] != ds.Turn) turns3.Add(ds.Turn);
            if (ds.Turn == DevourTurn.Frenzy && ds.Lunging && ds.LungeWarn > Game.FrenzyWarn - 0.05f) frenzy++;
            teeth = Math.Max(teeth, g.Bullets.Values.Count(b => b.Kind == Defs.ToothShot));
            if (ds.Turn == DevourTurn.Vomit) vomit = Math.Max(vomit, g.Bullets.Values.Count(b => b.Kind == Defs.BileShot));
        });
        Console.WriteLine("  ravenous: {0}; frenzy lunges {1}, jaws teeth {2}, vomit shots {3}", string.Join(" > ", turns3), frenzy, teeth, vomit);
        Check(string.Join(" > ", turns3).Contains("Frenzy > Rest > Jaws > Rest > Vomit"), "Ravenous: Frenzy, Jaws, Vomit, with breathers between");
        Check(frenzy >= 3 && teeth >= 14 && vomit >= 20, "Frenzy lunges, two rows of seven teeth, a spray of bile");

        // Below 10%: the last meal in the middle; it pulls everyone in, its teeth hurt.
        h.Hp = h.MaxHp * 0.08f;
        Run(s, dt, 15f, () => h.Devour.Stage == DevourStage.LastStand && h.Devour.SwallowId == 0 && h.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 30f, () => { safe(); Place(new V2(200f, 150f), new V2(1400f, 750f)); });
        bool middle = h.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 30f;
        Run(s, dt, 0.3f, null, () => { safe(); Place(new V2(200f, 150f), new V2(1400f, 750f)); });
        float pull = c.PullStrength;
        taken = me.Run.DamageTaken;
        Run(s, dt, 2f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) if (p.Id != 1) p.InvulnT = 1f;
            Place(Game.ToothPos(h.Pos, h.Devour.Spin, 3), new V2(1400f, 750f)); clear();
        });
        Console.WriteLine("  last meal: {0}, in the middle {1}, pull on the client {2}, a tooth hurt {3}", h.Devour.Stage, middle, pull, me.Run.DamageTaken > taken);
        Check(h.Devour.Stage == DevourStage.LastStand && middle && pull > 50f && me.Run.DamageTaken > taken, "below 10%: the last meal in the middle; it pulls you in and its teeth hurt");
        s[0].Leave(""); s[1].Leave("");
    }

    // The Eclipse (Eclipse.cs): the final boss in five phases and a last stand.
    static void EclipseTest(float dt)
    {
        var s = StartBuffed(dt, 50, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        Action safe = () => { foreach (var p in g.Players.Values) p.InvulnT = 1f; };
        Action<V2, V2> Place = (a, b) => { g.Players[1].Pos = c.Players[1].Pos = a; g.Players[2].Pos = c.Players[2].Pos = b; };
        Action quiet = () => { g.Hazards.Clear(); foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f; foreach (var e in g.Enemies.Values) if (e.Type != EnemyType.Eclipse) e.Hp = 0f; };
        Run(s, dt, 15, () => g.State == MatchState.Fighting && Find(s[0], EnemyType.Eclipse) != null && !g.IsSpawning(Find(s[0], EnemyType.Eclipse)));
        var h = Find(s[0], EnemyType.Eclipse);
        foreach (var e in g.Enemies.Values) if (e != h) e.Hp = 0f;  // just the boss
        var me = g.Players[1];
        Func<EclipseState> cs = () => c.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Eclipse)?.Eclipse;

        // Corona: Spiral, Corona rings, Solar flares, breathers between.
        var turns = new List<EclipseTurn>();
        int flares = 0, clientFlares = 0, ring = 0;
        Run(s, dt, 21f, null, () =>
        {
            safe(); Place(new V2(500f, 750f), new V2(1100f, 750f));
            var es = h.Eclipse;
            if (es == null) return;
            if (turns.Count == 0 || turns[turns.Count - 1] != es.Turn) turns.Add(es.Turn);
            flares = Math.Max(flares, es.Flares.Count);
            clientFlares = Math.Max(clientFlares, cs()?.Flares.Count ?? 0);
            if (es.Turn == EclipseTurn.CoronaRings) ring = Math.Max(ring, g.Bullets.Values.Count(b => b.Source == EnemyType.Eclipse && b.Kind == Defs.BigShot));
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  corona: {0}; flares {1} (client {2}), ring shots {3}", order, flares, clientFlares, ring);
        Check(h.Eclipse.Stage == EclipseStage.Corona && order.Contains("Spiral > Rest > CoronaRings > Rest > Flares > Rest"), "Corona: Spiral, Corona rings, Solar flares, with breathers between");
        Check(flares >= 3 && clientFlares >= 3 && ring >= 24, "flares at each player and one more (seen on the client); 24-shot rings");

        // A flare burns whoever stands in its lane.
        Run(s, dt, 25f, () => h.Eclipse.Turn == EclipseTurn.Flares && h.Eclipse.Flares.Count > 0 && h.Eclipse.Flares[0].T < 0.3f, () => { safe(); Place(new V2(500f, 750f), new V2(1100f, 750f)); });
        var fl = h.Eclipse.Flares[0];
        V2 inLane = fl.From + V2.FromAngle(fl.Ang) * 400f;
        float taken = me.Run.DamageTaken;
        Run(s, dt, 1.5f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) p.InvulnT = p.Id == 1 ? 0f : 1f;
            Place(inLane, new V2(1500f, 100f)); quiet();
        });
        Console.WriteLine("  standing in a flare's lane: hurt {0}", me.Run.DamageTaken > taken);
        Check(me.Run.DamageTaken > taken, "a solar flare burns a player in its lane");

        // 80%: it changes phase (can't be hurt, the screen clears), then the Penumbra: the moon and two Embers.
        h.Hp = h.MaxHp * 0.78f;
        Run(s, dt, 0.3f, null, safe);
        float hp = h.Hp; hurt.Invoke(g, new object[] { h, 500f, 1 });
        bool immune = h.Eclipse.TransT > 0f && h.Hp == hp && g.Bullets.Values.All(b => b.Source != EnemyType.Eclipse) && g.Hazards.Count == 0;
        Run(s, dt, 3.5f, () => h.Eclipse.Stage == EclipseStage.Penumbra && h.Eclipse.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        int embers = g.Enemies.Values.Count(e => e.Type == EnemyType.Ember);
        Console.WriteLine("  phase change: immune {0}; now {1} (client {2}), moon {3:0} px out (client {4:0}), embers {5}", immune, h.Eclipse.Stage, cs()?.Stage, h.Eclipse.MoonD, cs()?.MoonD, embers);
        Check(immune && h.Eclipse.Stage == EclipseStage.Penumbra && cs()?.Stage == EclipseStage.Penumbra, "at 80% it changes stage (can't be hurt, screen cleared), then the Penumbra (seen on the client)");
        Check(h.Eclipse.MoonD > 0f && cs()?.MoonD > 0f && embers == 2, "the moon orbits it (the client knows where), and two Embers come");

        // The moon soaks players' shots.
        typeof(Game).GetMethod("FindEclipseMoon", flags).Invoke(g, null);
        var shot = new Bullet { Kind = Defs.DpsShot, Pos = Game.MoonPos(h, h.Pos), Radius = 4f, Owner = 1 };
        var enemyShot = new Bullet { Kind = Defs.SmallShot, Pos = Game.MoonPos(h, h.Pos), Radius = 6f };
        var stops = typeof(Game).GetMethod("MoonStops", flags);
        bool soaks = (bool)stops.Invoke(g, new object[] { shot }) && !(bool)stops.Invoke(g, new object[] { enemyShot });
        Check(soaks, "the moon soaks players' shots (not its own side's)");

        // The flash: hide in the moon's shadow. You in the shadow, your ally out in the light.
        Run(s, dt, 25f, () => h.Eclipse.Turn == EclipseTurn.Flash && h.Eclipse.FlashT > 2f, () => { safe(); quiet(); });
        float meTaken = me.Run.DamageTaken, allyTaken = g.Players[2].Run.DamageTaken;
        bool shadowed = true;
        Run(s, dt, 3.5f, () => h.Eclipse.FlashT <= 0f, () =>
        {
            V2 u = V2.FromAngle(h.Eclipse.MoonA);
            V2 inShadow = h.Pos + u * (h.Eclipse.MoonD + 120f), outside = h.Pos + u.Orthogonal() * 350f;
            Place(inShadow, outside);
            shadowed &= Game.InMoonShadow(h.Eclipse, h.Pos, inShadow);
            foreach (var p in g.Players.Values) p.InvulnT = 0f;
            quiet();
        });
        Run(s, dt, 0.1f);
        Console.WriteLine("  eclipse flash: in the shadow took {0:0}, in the light took {1:0}", me.Run.DamageTaken - meTaken, g.Players[2].Run.DamageTaken - allyTaken);
        Check(shadowed && me.Run.DamageTaken == meTaken && g.Players[2].Run.DamageTaken > allyTaken, "the Eclipse flash hits everyone outside the moon's shadow");

        // 60%: the Umbra. Sunspots (seen on the client), Corona Priests; the dark drains you; crescents; closing rings.
        h.Hp = h.MaxHp * 0.58f;
        Run(s, dt, 4f, () => h.Eclipse.Stage == EclipseStage.Umbra && h.Eclipse.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        int priests = g.Enemies.Values.Count(e => e.Type == EnemyType.CoronaPriest);
        int spots = h.Eclipse.Spots.Count, clientSpots = cs()?.Spots.Count ?? 0;
        quiet();
        taken = me.Run.DamageTaken;
        Run(s, dt, 2f, null, () =>
        {
            V2 dark = new V2(60f, 60f);
            foreach (var sp in h.Eclipse.Spots) if (sp.Pos.DistanceTo(dark) < 200f) dark = new V2(Game.Width - 60f, 60f);
            g.Players[1].Pos = c.Players[1].Pos = dark;
            g.Players[2].Pos = c.Players[2].Pos = h.Eclipse.Spots[0].Pos;
            g.Players[2].InvulnT = 1f;
            g.Hazards.Clear(); foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f;
        });
        float drained = me.Run.DamageTaken - taken;
        Console.WriteLine("  umbra: sunspots {0} (client {1}), corona priests {2}; 2 s in the dark drained {3:0}", spots, clientSpots, priests, drained);
        Check(spots == 3 && clientSpots == 3 && priests == 2, "the Umbra: three sunspots (seen on the client) and two Corona Priests");
        float flat = drained / g.EnemyDamageScale(g.Wave);  // before the difficulty's damage growth
        Check(flat >= 9f && flat <= 15f, "the dark drains about 6 health a second outside the light (times the difficulty's enemy damage)");
        bool looped = false;
        int rings = 0, clientRings = 0;
        var turns3 = new List<EclipseTurn>();
        Run(s, dt, 22f, null, () =>
        {
            safe(); Place(new V2(400f, 700f), new V2(1200f, 700f));
            var es = h.Eclipse;
            if (turns3.Count == 0 || turns3[turns3.Count - 1] != es.Turn) turns3.Add(es.Turn);
            looped |= g.Bullets.Values.Any(b => b.Kind == Defs.CrescentShot && b.AngVel != 0f) && c.Bullets.Values.Any(b => b.Kind == Defs.CrescentShot);
            rings = Math.Max(rings, es.Rings.Count);
            clientRings = Math.Max(clientRings, cs()?.Rings.Count ?? 0);
        });
        Console.WriteLine("  umbra turns: {0}; crescents loop {1}, closing rings {2} (client {3})", string.Join(" > ", turns3), looped, rings, clientRings);
        Check(looped && rings >= 1 && clientRings >= 1, "crescents loop out and back; rings of shadow close in (seen on the client)");

        // A closing ring hurts away from its gaps.
        Run(s, dt, 25f, () => h.Eclipse.Rings.Count > 0 && h.Eclipse.Rings[0].R < 700f, () => { safe(); quiet(); });
        var rg = h.Eclipse.Rings[0];
        float wall = rg.Gap1 + MathX.Pi / 2f;
        if (Game.InUmbraGap(rg, rg.Center + V2.FromAngle(wall) * 100f)) wall += 0.9f;
        taken = me.Run.DamageTaken;
        Run(s, dt, 3f, () => me.Run.DamageTaken > taken, () =>
        {
            foreach (var p in g.Players.Values) p.InvulnT = p.Id == 1 ? 0f : 1f;
            V2 on = rg.Center + V2.FromAngle(wall) * Math.Max(80f, rg.R);
            g.Players[1].Pos = c.Players[1].Pos = on;
            foreach (var sp in h.Eclipse.Spots) sp.Pos = on;  // in the light, so only the ring can hurt
            g.Hazards.Clear(); foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f;
        });
        Check(me.Run.DamageTaken > taken, "a closing ring of shadow hurts where it has no gap");

        // 40%: the Echoes, turns borrowed from the bosses before it.
        h.Hp = h.MaxHp * 0.38f;
        Run(s, dt, 4f, () => h.Eclipse.Stage == EclipseStage.Echoes && h.Eclipse.TransT <= 0f, safe);
        var turns4 = new List<EclipseTurn>();
        bool sweep = false, disk = false, pull = false, strikes = false, moonShots = false;
        Run(s, dt, 29f, null, () =>
        {
            safe(); Place(new V2(300f, 750f), new V2(1300f, 750f));
            var es = h.Eclipse;
            if (turns4.Count == 0 || turns4[turns4.Count - 1] != es.Turn) turns4.Add(es.Turn);
            sweep |= es.SweepOn == 2 && cs()?.SweepOn == 2;
            disk |= es.DiskT > 1f && cs()?.DiskT > 1f;
            pull |= g.PullStrength > 0f && es.Turn == EclipseTurn.EchoSingularity;
            strikes |= es.Turn == EclipseTurn.EchoHerald && c.Hazards.Count >= 4;
            moonShots |= g.Bullets.Values.Any(b => b.Source == EnemyType.Eclipse && b.Pos.DistanceTo(Game.MoonPos(h, h.Pos)) < 70f && b.Vel.Length > 200f);
        });
        string order4 = string.Join(" > ", turns4);
        Console.WriteLine("  echoes: {0}; sweep {1}, disk {2}, pull {3}, strike rain {4}, moon shots {5}", order4, sweep, disk, pull, strikes, moonShots);
        Check(order4.Contains("EchoColossus > Rest > EchoLich > Rest > EchoHerald > Rest > EchoSingularity"), "the Echoes: the Colossus, the Lich, the Storm Herald and the Singularity in turn");
        Check(sweep && disk && pull && strikes && moonShots, "the Lich's beams and the Singularity's disk (seen on the client), its pull, strike rain, and the moon shooting back");

        // 20%: Last Light. The edge burns in, and each game keeps its own player inside it.
        h.Hp = h.MaxHp * 0.18f;
        Run(s, dt, 4f, () => h.Eclipse.Stage == EclipseStage.LastLight && h.Eclipse.TransT <= 0f, safe);
        Run(s, dt, 6f, null, () => { safe(); quiet(); });
        float inset = h.Eclipse.Inset, clientInset = cs()?.Inset ?? 0f;
        c.Players[2].Pos = new V2(5f, 5f);
        Run(s, dt, 0.3f, null, () => { safe(); quiet(); });
        V2 kept = c.Players[2].Pos;
        Console.WriteLine("  last light: edge {0:0} px (client {1:0}); the client's player pushed to the corner stays at ({2:0}, {3:0})", inset, clientInset, kept.X, kept.Y);
        Check(inset > 30f && Math.Abs(clientInset - inset) < 10f && kept.X >= clientInset && kept.Y >= clientInset, "Last Light: the edge burns in (seen on the client) and you can't pass it");
        Run(s, dt, 30f, () => h.Eclipse.Turn == EclipseTurn.ShadowNova, () => { safe(); quiet(); });
        int novaFlashes = 0, left0 = h.Eclipse.FlashLeft;
        float moonBefore = h.Eclipse.MoonA, jump = 0f;
        Run(s, dt, 7f, () => h.Eclipse.FlashLeft == 0, () =>
        {
            safe(); quiet();
            if (h.Eclipse.FlashLeft < left0)
            {
                novaFlashes++;
                left0 = h.Eclipse.FlashLeft;
                if (novaFlashes == 1)
                    jump = h.Eclipse.MoonA - moonBefore;
            }
        });
        Console.WriteLine("  shadow nova: {0} flashes, the moon jumped {1:0} degrees between them", novaFlashes, jump * 180f / MathX.Pi);
        Check(novaFlashes == 2 && jump > 1.1f && jump < 1.4f, "Shadow nova: two flashes, the moon jumping round between them");

        // Below 5%: Totality. No moon, a nova timer, +50% damage; the nova hits and heals it.
        h.Hp = h.MaxHp * 0.04f;
        Run(s, dt, 6f, () => h.Eclipse.Stage == EclipseStage.Totality && h.Pos.DistanceTo(new V2(Game.Width / 2f, Game.Height / 2f)) < 40f, () => { safe(); quiet(); });
        bool totality = h.Eclipse.Stage == EclipseStage.Totality && h.Eclipse.MoonD == 0f && cs()?.Stage == EclipseStage.Totality && h.Eclipse.Inset == 0f;
        hp = h.Hp; hurt.Invoke(g, new object[] { h, 100f, 1 });
        float dealt = hp - h.Hp;
        h.Eclipse.NovaT = 0.5f;
        float before = h.Hp, novaTaken = me.Run.DamageTaken;
        Run(s, dt, 1f, null, () => { foreach (var p in g.Players.Values) p.InvulnT = 0f; quiet(); });
        Console.WriteLine("  totality: {0}; 100 damage dealt {1:0}; the nova took {2:0} from you and healed it {3:0}", totality, dealt, me.Run.DamageTaken - novaTaken, h.Hp - before);
        Check(totality, "below 5%: Totality in the middle, the moon gone, the edge gone");
        Check(dealt > 140f && dealt < 160f, "it takes +50% damage in Totality");
        Check(me.Run.DamageTaken - novaTaken > me.MaxHp * 0.5f && Math.Abs(h.Hp - before - dealt * Game.EclipseNovaHealShare) < 1f,
            "the nova hits everyone hard and heals it back half of what it lost since Totality began");
        // A nova after nothing hurt it heals nothing: every cycle leaves it lower, so Totality can always be won.
        before = h.Hp;
        h.Eclipse.NovaT = 0.5f;
        Run(s, dt, 1f, null, () => { foreach (var p in g.Players.Values) p.InvulnT = 0f; quiet(); });
        Console.WriteLine("  a nova with no damage since healed it {0:0}", h.Hp - before);
        Check(Math.Abs(h.Hp - before) < 1f, "a nova with no damage taken since the last heals nothing");
        s[0].Leave(""); s[1].Leave("");
    }

    static int CountBits(int v) { int n = 0; while (v != 0) { v &= v - 1; n++; } return n; }
    static float Vdot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

    // The Colossus (Colossus.cs): three phases that break it down and shrink it, a stagger at the end of each, stomp
    // rings of orbs with small openings, held boulders, fissures that stay, then a last stand.
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
        float baseR = col.Def.Radius;
        Func<float> clientR = () => Find(s[1], EnemyType.Colossus)?.Radius ?? 0f;

        // Awakening: three times its size; Stomps, Boulders, Fissures, with breathers; rings, held boulders, landings
        // and cracks reach the client.
        var turns = new List<ColTurn>();
        bool ringOnClient = false, openings = false, dropOnClient = false, crackOnClient = false, holdOnClient = false, volley = false;
        Run(s, dt, 25f, null, () =>
        {
            safe();
            if (col.Colossus != null && (turns.Count == 0 || turns[turns.Count - 1] != col.Colossus.Turn)) turns.Add(col.Colossus.Turn);
            var cc = Find(s[1], EnemyType.Colossus)?.Colossus;
            if (cc != null)
            {
                ringOnClient |= cc.Rings.Any(r => r.Orbs); openings |= cc.Rings.Any(r => r.Orbs && r.Gaps == 3);
                dropOnClient |= cc.Drops.Count > 0; crackOnClient |= cc.Cracks.Count > 0; holdOnClient |= cc.HoldT > 0f;
            }
            volley |= g.Bullets.Values.Any(b => b.Source == EnemyType.Colossus && b.Kind == Defs.PierceShot);
        });
        string order = string.Join(" > ", turns);
        Console.WriteLine("  awakening: {0}; radius {1:0} (client {2:0})", order, col.Radius, clientR());
        Check(Math.Abs(col.Radius - baseR * 3f) < 0.5f && Math.Abs(clientR() - baseR * 3f) < 0.5f, "in Awakening it's three times its size (seen on the client)");
        Check(col.Colossus.Stage == ColStage.Awakening && order.StartsWith("Stomps > Rest > Boulders > Rest > Fissures > Rest"), "Awakening: Stomps, Boulders, Fissures, with breathers between");
        Check(ringOnClient && openings && holdOnClient && dropOnClient && crackOnClient, "rings of orbs (three openings), boulders held up then thrown, and fissures show on the client");
        Check(volley, "its rock volley still flies between turns");
        var cst = col.Colossus;
        Check(cst.Cracks.Count >= 2 && cst.Cracks.All(k => k.State == ColCrack.Dormant), "its fissures stay on the floor as scars after they erupt");

        // A ring of orbs: touch an orb and it hurts (once), and nothing shoves you; in an opening you're left alone.
        cst.Rings.Clear(); cst.Drops.Clear(); cst.Cracks.Clear();
        var me = g.Players[1];
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp;
        V2 center = new V2(800f, 450f);
        Func<float, float, StompRing> Ring = (radius, gap) => { var r = new StompRing { Center = center, Radius = radius, Gap1 = gap, Gaps = 1, Orbs = true, Rot = 0.013f }; cst.Rings.Add(r); return r; };
        Action quiet = () => { col.FireT = 99f; cst.TurnT = 0f; cst.ActT = 99f; cst.HoldT = 0f; cst.Drops.Clear(); cst.Cracks.Clear(); foreach (var b in g.Bullets.Values.Where(b => b.Owner == 0).ToList()) g.Bullets.Remove(b.Id); };
        Action<float> Hold = secs => Run(s, dt, secs, null, quiet);
        col.Pos = center + new V2(0f, 380f);  // (its body out of the way)
        me.Pos = center + new V2(0f, -200f);
        me.InvulnT = 0f;
        var r1 = Ring(170f, 0f);  // the opening to the right, the player is above
        float took = me.Run.DamageTaken;
        Hold(0.6f);
        float hit1 = me.Run.DamageTaken - took, d1 = me.Pos.DistanceTo(center);
        Hold(1f);
        float hit2 = me.Run.DamageTaken - took, d2 = me.Pos.DistanceTo(center);
        Console.WriteLine("  orb ring catches you: damage {0:0} then {1:0}; you stand {2:0} then {3:0} px out (ring at {4:0})", hit1, hit2, d1, d2, r1.Radius);
        Check(hit1 > 0f && hit2 == hit1 && Math.Abs(d1 - 200f) < 1f && Math.Abs(d2 - 200f) < 1f, "a ring of orbs that reaches you hurts once, and nothing shoves you");
        cst.Rings.Clear();
        me.Pos = center + new V2(250f, 0f);
        took = me.Run.DamageTaken;
        me.InvulnT = 0f;
        var r3 = Ring(200f, 0f);
        Hold(1f);
        Console.WriteLine("  in an opening: damage {0:0}, ring at {1:0}", me.Run.DamageTaken - took, r3.Radius);
        Check(me.Run.DamageTaken == took && r3.Radius > 300f, "a ring of orbs passes you by in one of its openings");
        Check(Game.OrbOpenHalfAt(60f) * 2f * 60f >= Game.ColOpenMinArc - 0.5f && Game.OrbOpenHalfAt(600f) == Game.ColOpenHalf,
            "an opening is small (0.24 rad) but never under 70 px across near its middle");
        // A stomp: three openings, one a short step beside its target.
        var add = typeof(Game).GetMethod("AddOrbRing", flags);
        cst.Rings.Clear();
        col.Pos = center;
        me.Pos = center + new V2(0f, 300f);
        col.TargetId = me.Id;
        add.Invoke(g, new object[] { col, cst, 3 });
        var st = cst.Rings[0];
        float nearest = 99f;
        for (int k = 0; k < st.Gaps; k++) nearest = Math.Min(nearest, Math.Abs(MathX.WrapPi(Game.RingGapAt(st, k) - MathX.Pi / 2f)));
        Console.WriteLine("  a stomp: {0} openings, the nearest {1:0} px beside you", st.Gaps, nearest * 300f);
        Check(st.Orbs && st.Gaps == 3 && nearest * 300f > 60f && nearest * 300f < 200f, "a stomp's ring has three openings, one a short step to your side");
        cst.Rings.Clear();

        // Fissures: one that erupts under you hurts (once an eruption); with five scars up, a Fissures turn wakes them all.
        col.Pos = center + new V2(-300f, 0f);
        var ck = new ColCrack { From = col.Pos, Ang = 0f, Len = 600f, Max = 600f, State = ColCrack.Waiting };
        cst.Cracks.Add(ck);
        me.Pos = col.Pos + new V2(400f, 5f);
        me.InvulnT = 0f;
        took = me.Run.DamageTaken;
        Run(s, dt, 2.2f, null, () => { col.FireT = 99f; cst.TurnT = 0f; cst.ActT = 99f; cst.Rings.Clear(); foreach (var b in g.Bullets.Values.Where(b => b.Owner == 0).ToList()) g.Bullets.Remove(b.Id); });
        float burnt = me.Run.DamageTaken - took;
        Console.WriteLine("  standing on an erupting fissure: {0:0} damage (its burn and a side shot); it's now {1}", burnt, ck.State);
        Check(burnt > 0f && burnt < 80f && ck.State == ColCrack.Dormant, "an erupting fissure hurts what stands on it, once, then lies as a scar");
        g.DevSetGod(1, true);
        cst.Cracks.Clear();
        for (int k = 0; k < 5; k++) cst.Cracks.Add(new ColCrack { From = col.Pos, Ang = k * 0.5f, Len = 400f, Max = 400f, State = ColCrack.Dormant });
        cst.Step = 3; cst.ActT = 0f;  // the next turn is Fissures
        typeof(Game).GetMethod("NextColTurn", flags).Invoke(g, new object[] { col, cst });
        int woke = 0;
        Run(s, dt, 3f, null, () => { safe(); woke = Math.Max(woke, cst.Cracks.Count(k => k.State != ColCrack.Dormant)); });
        Console.WriteLine("  five scars: turn {0}, cycling {1}, {2} woken, still {3} on the floor", cst.Turn, cst.Cycling, woke, cst.Cracks.Count);
        Check(cst.Turn == ColTurn.Fissures && woke >= 4 && cst.Cracks.Count == 5, "with five fissures up it doesn't open more: they all wake and erupt again in turn");
        Run(s, dt, 6f, () => cst.Turn != ColTurn.Fissures, safe);

        // 65%: it kneels, staggered: hits fill its Break meter, not its health; broken, its shell blasts off, it drops to
        // twice its size, can't be hurt while it stands back up, and Crumbling brings four plates of cover.
        col.Hp = col.MaxHp * 0.6f;
        Run(s, dt, 0.3f, null, safe);
        float hp = col.Hp;
        hurt.Invoke(g, new object[] { col, 100f, 1 });
        Run(s, dt, 0.3f, null, safe);
        var cs = Find(s[1], EnemyType.Colossus)?.Colossus;
        Console.WriteLine("  staggered {0} (client {1}): health held at {2:0}%, a hit took {3:0} and broke {4:0}% (client {5:0}%)", cst.Stagger, cs?.Stagger, hp / col.MaxHp * 100f, hp - col.Hp, cst.Break * 100f, (cs?.Break ?? 0f) * 100f);
        Check(cst.Stagger && cs != null && cs.Stagger && Math.Abs(hp / col.MaxHp - 0.65f) < 0.001f && col.Hp == hp && cst.Break > 0f && cs.Break > 0f,
            "at 65% it kneels, staggered: its health holds and hits fill its Break meter (seen on the client)");
        Check(cst.Rings.Count == 0 && cst.Cracks.Count == 0 && g.Bullets.Values.All(b => b.Source != EnemyType.Colossus), "the stagger clears the field (its rings, fissures and shots)");
        Run(s, dt, 2f, null, safe);
        Check(cst.Stagger && cst.TurnT == 0f || cst.Stagger, "a stagger waits to be broken");
        Action breakIt = () => { for (int k = 0; k < 40 && cst.Stagger; k++) { hurt.Invoke(g, new object[] { col, col.MaxHp * 0.01f, 1 }); Run(s, dt, 0.05f, null, safe); } };
        breakIt();
        hp = col.Hp; hurt.Invoke(g, new object[] { col, 500f, 1 });
        bool immune = cst.TransT > 0f && col.Hp == hp;
        Run(s, dt, 0.3f, null, safe);
        Console.WriteLine("  broken: immune {0}, radius {1:0} (client {2:0})", immune, col.Radius, clientR());
        Check(!cst.Stagger && immune && Math.Abs(col.Radius - baseR * 2f) < 0.5f && Math.Abs(clientR() - baseR * 2f) < 0.5f, "broken, its shell blasts off: it drops to twice its size at once and can't be hurt while it stands up");
        Run(s, dt, 3f, () => cst.Stage == ColStage.Crumbling && cst.TransT <= 0f, safe);
        Run(s, dt, 0.5f, null, safe);
        var cc2 = Find(s[1], EnemyType.Colossus)?.Colossus;
        Console.WriteLine("  now {0} with {1} plates; client sees {2} with {3}", cst.Stage, cst.Slabs.Count, cc2?.Stage, cc2?.Slabs.Count);
        Check(cst.Stage == ColStage.Crumbling && cc2?.Stage == ColStage.Crumbling, "then Crumbling (seen on the client)");
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
        col.Pos = target.Pos + new V2(0f, 260f);
        cst.Turn = ColTurn.Rest; cst.TurnT = 0f; cst.TurnLen = 99f; cst.ActT = 99f;
        cst.ChargeDir = -MathX.Pi / 2f; cst.ChargeWarn = 0.01f;
        int plates = cst.Slabs.Count;
        Run(s, dt, 1f, () => cst.DazeT > 0f, safe);
        hp = col.Hp; hurt.Invoke(g, new object[] { col, 100f, 1 });
        float dazedHit = hp - col.Hp;
        Console.WriteLine("  charge into a plate: plates {0} -> {1}, dazed {2:0.0} s, a 100 hit took {3:0}", plates, cst.Slabs.Count, cst.DazeT, dazedHit);
        Check(cst.Slabs.Count == plates - 1 && cst.DazeT > 1f && Math.Abs(dazedHit - 150f) < 1f, "a charge into a plate breaks it and dazes the Colossus (+50% damage taken)");
        cst.TurnLen = 1f;

        // 30%: staggered again; broken, Molten Core at its own size; its stomps leave lava, which hurts.
        col.Hp = col.MaxHp * 0.25f;
        Run(s, dt, 1.5f, () => cst.Stagger, safe);
        bool stagger2 = cst.Stagger;
        breakIt();
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
        Console.WriteLine("  molten core: staggered first {0}, stage {1}, radius {2:0}, lava {3} (client {4}), a second in it: {5:0} damage", stagger2, cst.Stage, col.Radius, lava != null, lavaOnClient, burned);
        Check(stagger2 && cst.Stage == ColStage.MoltenCore && cst.Slabs.Count == 0 && Math.Abs(col.Radius - baseR) < 0.5f, "at 30% it's staggered, then the shell splits: Molten Core at its own size, and the plates are gone");
        Check(lava != null && lavaOnClient && burned > 10f && burned < 40f, "its stomps leave lava pools that hurt about 20 a second (seen on the client)");

        // Below 10%: staggered once more, then the last stand, smaller still, kneeling in the middle; its rings have four openings.
        col.Hp = col.MaxHp * 0.08f;
        Run(s, dt, 1.5f, () => cst.Stagger, safe);
        bool stagger3 = cst.Stagger;
        breakIt();
        Run(s, dt, 7f, null, safe);
        Console.WriteLine("  last stand: staggered first {0}, stage {1}, radius {2:0}, {3:0} px from the middle, rings with four openings {4}", stagger3, cst.Stage, col.Radius, col.Pos.DistanceTo(new V2(800f, 450f)), cst.Rings.Any(r => r.Gaps == 4));
        Check(stagger3 && cst.Stage == ColStage.LastStand && Math.Abs(col.Radius - baseR * 0.7f) < 0.5f && col.Pos.DistanceTo(new V2(800f, 450f)) < 10f && cst.Rings.Any(r => r.Gaps == 4),
            "below 10% it's staggered, then kneels in the middle for its last stand, smaller still: stomps with four openings");
        col.Hp = 0f;
        Run(s, dt, 1f, null, safe);
        Check(Find(s[0], EnemyType.Colossus) == null && Find(s[1], EnemyType.Colossus) == null, "the Colossus goes down (and its rings with it)");
        s[0].Leave(""); s[1].Leave("");
    }
}

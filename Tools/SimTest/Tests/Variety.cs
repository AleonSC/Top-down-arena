using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The second half's variety (Variety.cs): elite traits and squads.
static partial class Program
{
    static void VarietyTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        var squadField = typeof(Game).GetField("squadQueue", flags);
        var buildWave = typeof(Game).GetMethod("BuildWave", flags);
        var s = StartBuffed(dt, 22, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        var g = s[0].Game;
        var c = s[1].Game;
        Run(s, dt, 15, () => g.State == MatchState.Fighting);

        // Squads: a regular wave is about 70% squads, unlocked by its wave, none more than twice.
        int tried = 0, okShare = 0, okRepeat = 0, okUnlock = 0;
        foreach (int w in new[] { 4, 12, 22, 33, 44 })
        {
            g.Wave = w; g.BossSlot = (w) % 5 == 0 && (w) > 0;
            buildWave.Invoke(g, new object[] { w });
            var squads = (List<SquadDef>)squadField.GetValue(g);
            var singles = (List<EnemyType>)queueField.GetValue(g);
            int members = squads.Sum(q => q.Members.Length), total = members + singles.Count;
            float share = total > 0 ? members / (float)total : 0f;
            Console.WriteLine("  wave {0}: {1} squads ({2}), {3} of {4} enemies in squads", w, squads.Count, string.Join(", ", squads.Select(q => q.Name)), members, total);
            tried++;
            if (share > 0.25f && share <= 0.85f) okShare++;  // (small early waves swing with the roll)
            if (squads.GroupBy(q => q).All(q => q.Count() <= 2)) okRepeat++;
            if (squads.All(q => q.From <= w)) okUnlock++;
        }
        Check(okShare == tried && okRepeat == tried && okUnlock == tried, "waves are about 70% squads, only ones unlocked by then, none more than twice");
        // At most MaxPulsars Pulsars a wave, squads included (CapSentries).
        var cap = typeof(Game).GetMethod("CapSentries", flags);
        int mostPulsars = 0;
        for (int w = 18; w <= 45; w++)
            for (int k = 0; k < 3; k++)
            {
                g.Wave = w; g.BossSlot = (w) % 5 == 0 && (w) > 0;
                buildWave.Invoke(g, new object[] { w });
                cap.Invoke(g, null);
                int n = ((List<EnemyType>)queueField.GetValue(g)).Count(t => t == EnemyType.Pulsar)
                        + ((List<SquadDef>)squadField.GetValue(g)).Sum(q => q.Members.Count(t => t == EnemyType.Pulsar));
                mostPulsars = Math.Max(mostPulsars, n);
            }
        Console.WriteLine("  pulsars: at most {0} in a wave (waves 18-45, 3 rolls each)", mostPulsars);
        Check(mostPulsars <= Game.MaxPulsars, "at most " + Game.MaxPulsars + " Pulsars in a wave, squads included");
        g.Wave = 22; g.BossSlot = false;

        // Clear the field; a frozen Grunt in a corner keeps the wave going while we test.
        ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g);
        ((List<SquadDef>)squadField.GetValue(g)).Clear();
        var anchor = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(40f, 40f), 0f, 0, false });
        anchor.Hp = anchor.MaxHp = 1e6f;
        foreach (var e in g.Enemies.Values) if (e != anchor) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var me = g.Players[1];
        g.DevSetGod(2, true);
        Action<V2, V2> Place = (a, b) => { g.Players[1].Pos = c.Players[1].Pos = a; g.Players[2].Pos = c.Players[2].Pos = b; };
        Action hold = () => { anchor.FrozenT = 9f; anchor.FireT = 9f; };
        Func<EnemyType, V2, bool, Enemy> Spawn = (t, at, el) => (Enemy)spawn.Invoke(g, new object[] { t, at, 0f, 0, el });
        Action clear = () => { foreach (var e in g.Enemies.Values) if (e != anchor) e.Hp = 0f; foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f; g.Hazards.Clear(); g.Patches.Clear(); };

        // A Shield wall: the Shielder ends up between you and its Snipers.
        var wall = Game.Squads.First(q => q.Name == "Shield wall");
        typeof(Game).GetMethod("SpawnSquad", flags).Invoke(g, new object[] { wall });
        Run(s, dt, 7f, null, () => { hold(); Place(new V2(800f, 450f), new V2(1500f, 800f)); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var sh = g.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.Shielder && e.SquadId != 0);
        var snipers = g.Enemies.Values.Where(e => e.Type == EnemyType.Sniper && sh != null && e.SquadId == sh.SquadId).ToList();
        bool guards = sh != null && snipers.Count == 2 && snipers.Average(n => n.Pos.DistanceTo(me.Pos)) > sh.Pos.DistanceTo(me.Pos) + 30f;
        Console.WriteLine("  shield wall: shielder {0:0} px from you, snipers {1}", sh?.Pos.DistanceTo(me.Pos), string.Join(", ", snipers.Select(n => n.Pos.DistanceTo(me.Pos).ToString("0"))));
        Check(guards, "a Shield wall squad arrives together and its Shielder stands between you and its Snipers");
        clear(); Run(s, dt, 0.2f);

        // Elite traits: every elite has one (two from wave 35), seen on the client.
        var elite = Spawn(EnemyType.Grunt, new V2(800f, 200f), true);
        g.Wave = 36; g.BossSlot = false;
        var elite2 = Spawn(EnemyType.Grunt, new V2(900f, 200f), true);
        g.Wave = 22; g.BossSlot = false;
        Run(s, dt, 0.3f, null, () => { hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var ce = c.Enemies.Values.FirstOrDefault(e => e.Id == elite.Id);
        Func<byte, int> Bits = b => Enumerable.Range(0, 8).Count(i => (b & (1 << i)) != 0);
        Console.WriteLine("  elite traits: wave 22 {0} (client {1}), wave 36 {2}", (EliteTrait)elite.Traits, ce == null ? "?" : ((EliteTrait)ce.Traits).ToString(), (EliteTrait)elite2.Traits);
        Check(Bits(elite.Traits) == 1 && Bits(elite2.Traits) == 2 && ce != null && ce.Traits == elite.Traits, "elites roll one trait (two from wave 35), and the client knows which");
        clear(); Run(s, dt, 0.2f);

        Func<EliteTrait, V2, Enemy> Elite = (t, at) => { var e = Spawn(EnemyType.Grunt, at, true); e.Traits = (byte)t; e.BubbleHp = e.BubbleMax = t == EliteTrait.Shielded ? e.MaxHp * Game.ShieldedShare : 0f; return e; };
        // Shielded: a bubble that soaks, and grows back.
        var shd = Elite(EliteTrait.Shielded, new V2(800f, 200f));
        float hp = shd.Hp; hurt.Invoke(g, new object[] { shd, shd.BubbleMax * 0.5f, 1 });
        bool soak = shd.Hp == hp && shd.BubbleHp > 0f;
        hurt.Invoke(g, new object[] { shd, shd.BubbleMax, 1 });
        bool through = shd.Hp < hp && shd.BubbleHp <= 0f;
        Run(s, dt, Game.BubbleRegrow + 0.5f, null, () => { hold(); shd.FrozenT = 1f; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        Check(soak && through && shd.BubbleHp > 0f, "Shielded: a bubble soaks hits first, then grows back 4 s later");
        // Cells: the bubble is twelve cells, and a hit wears down the one facing the shooter (and spills to its neighbours).
        var cell = Elite(EliteTrait.Shielded, new V2(1200f, 400f));
        var shooter = g.Players.Values.First();
        shooter.Pos = cell.Pos + new V2(200f, 0f);  // due east: cell 3 (cell 0 is straight up, one every 30 degrees)
        float one = cell.BubbleMax / Game.BubbleCells;
        hurt.Invoke(g, new object[] { cell, one * 0.5f, shooter.Id });
        int mid = Game.CellMask(cell);
        hurt.Invoke(g, new object[] { cell, one, shooter.Id });
        int after = Game.CellMask(cell);
        Run(s, dt, 0.3f, null, () => { hold(); cell.FrozenT = 1f; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var cc = c.Enemies.Values.FirstOrDefault(e => e.Id == cell.Id);
        Console.WriteLine("  cells: after half a cell {0:X}, after one and a half {1:X}, client {2:X}, bubble {3:0.0} of {4:0.0}", mid, after, cc?.Cells ?? -1, cell.BubbleHp, cell.BubbleMax);
        Check(mid == (0xFFF | 1 << (3 + 12)), "a hit from the east wears only cell 3 (the others stay full)");
        Check((after & 1 << 3) == 0 && (after & 1 << 4) != 0 && (after & 1 << 16) != 0 && (after & 1 << 2) != 0 && (after & 1 << 14) == 0, "it breaks, the spill wears its neighbour, the far side is untouched");
        Check(cc != null && cc.Cells == after && Math.Abs(cell.BubbleHp - (cell.BubbleMax - one * 1.5f)) < 0.01f, "the client sees the same cells, and the bubble's total is the sum of them");
        shooter.Pos = cell.Pos + new V2(-200f, 0f);
        hurt.Invoke(g, new object[] { cell, one, shooter.Id });
        Check((Game.CellMask(cell) & 1 << 9) == 0 && (Game.CellMask(cell) & 1 << 3) == 0, "a hit from the west breaks cell 9, so cells go by where they are hit");
        // Hasted and Commander: faster fire.
        var traitMult = typeof(Game).GetMethod("TraitFireMult", flags);
        var hasted = Elite(EliteTrait.Hasted, new V2(400f, 200f));
        Elite(EliteTrait.Commander, new V2(1200f, 200f));
        var follower = Spawn(EnemyType.Grunt, new V2(1300f, 200f), false);
        typeof(Game).GetMethod("PrepareVariety", flags).Invoke(g, null);
        float hm = (float)traitMult.Invoke(g, new object[] { hasted }), fm = (float)traitMult.Invoke(g, new object[] { follower }), am = (float)traitMult.Invoke(g, new object[] { anchor });
        Console.WriteLine("  fire rate: hasted x{0}, near a commander x{1}, far away x{2}", hm, fm, am);
        Check(Math.Abs(hm - 1.4f) < 0.01f && Math.Abs(fm - 1.3f) < 0.01f && am == 1f, "Hasted fires 40% faster; allies near a Commander 30% faster");
        clear(); Run(s, dt, 0.2f);
        // Volatile: a burst of 12 shots after its fuse. Splitting: Shards.
        var vol = Elite(EliteTrait.Volatile, new V2(800f, 200f));
        var spl = Elite(EliteTrait.Splitting, new V2(1200f, 200f));
        vol.Hp = 0f; spl.Hp = 0f;
        int burst = 0, shards = 0;
        Run(s, dt, 1.2f, null, () =>
        {
            hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f;
            burst = Math.Max(burst, g.Bullets.Values.Count(b => b.Kind == Defs.SmallShot && b.Pos.DistanceTo(new V2(800f, 200f)) < 120f));
            shards = Math.Max(shards, g.Enemies.Values.Count(e => e.Type == EnemyType.Shard));
        });
        Console.WriteLine("  volatile burst {0} shots; splitting left {1} shards", burst, shards);
        Check(burst >= Game.VolatileShots - 2 && shards == 2, "Volatile bursts into a ring when it dies; Splitting leaves Shards");
        clear(); Run(s, dt, 0.2f);
        // Mirrored: every fourth of your shots that hits comes back at you.
        var mir = Elite(EliteTrait.Mirrored, new V2(800f, 300f));
        mir.Hp = mir.MaxHp = 1e5f;
        var bounce = typeof(Game).GetMethod("MirrorBounces", flags);
        int bounced = 0;
        for (int i = 0; i < 8; i++)
            if ((bool)bounce.Invoke(g, new object[] { mir, new Bullet { Owner = 1, Pos = mir.Pos, Kind = Defs.DpsShot } })) bounced++;
        Run(s, dt, 0.1f, null, () => { hold(); mir.FrozenT = 1f; foreach (var p in g.Players.Values) p.InvulnT = 1f; Place(new V2(800f, 700f), new V2(100f, 800f)); });
        bool back = g.Bullets.Values.Any(b => b.Kind == Defs.FastShot && b.Vel.Y > 0f);
        Console.WriteLine("  mirrored: {0} of 8 shots bounced; one flying back at you {1}", bounced, back);
        Check(bounced == 2 && back, "Mirrored sends one in four of your shots back at you");
        clear(); Run(s, dt, 0.2f);

        // Damage over time goes through a bubble first, like a hit.
        var burning = Elite(EliteTrait.Shielded, new V2(800f, 200f));
        float bhp = burning.Hp, bubble = burning.BubbleHp;
        burning.BurnT = 1f; burning.BurnDps = bubble * 0.5f; burning.BurnBy = 1;
        Run(s, dt, 0.5f, null, () => { hold(); burning.FrozenT = 1f; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        Console.WriteLine("  burning shielded elite: health {0:0} -> {1:0}, bubble {2:0} -> {3:0}", bhp, burning.Hp, bubble, burning.BubbleHp);
        Check(burning.Hp == bhp && burning.BubbleHp < bubble, "burning eats a Shielded elite's bubble before its health");
        clear(); Run(s, dt, 0.2f);

        // A Volatile burst still on its fuse when the wave ends doesn't go off in the intermission.
        var lastVol = Elite(EliteTrait.Volatile, new V2(800f, 200f));
        lastVol.Hp = 0f;
        Run(s, dt, 0.05f, null, () => { hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        typeof(Game).GetMethod("WaveCleared", flags).Invoke(g, null);
        int late = 0;
        Run(s, dt, 1.2f, null, () =>
        {
            foreach (var p in g.Players.Values) p.InvulnT = 1f;
            late = Math.Max(late, g.Bullets.Values.Count(b => b.Kind == Defs.SmallShot && b.Pos.DistanceTo(new V2(800f, 200f)) < 120f));
        });
        Console.WriteLine("  volatile fuse at the wave's end: {0} shots after it", late);
        Check(late == 0 && g.State == MatchState.Intermission, "a Volatile burst on its fuse is dropped when the wave ends");
        s[0].Leave(""); s[1].Leave("");
    }
}

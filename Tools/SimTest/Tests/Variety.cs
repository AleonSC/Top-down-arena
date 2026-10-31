using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The second half's variety (Variety.cs): four new enemies, elite traits and squads.
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
            g.Wave = w;
            buildWave.Invoke(g, new object[] { w });
            var squads = (List<SquadDef>)squadField.GetValue(g);
            var singles = (List<EnemyType>)queueField.GetValue(g);
            int members = squads.Sum(q => q.Members.Length), total = members + singles.Count(t => t != EnemyType.Swarmer) + singles.Count(t => t == EnemyType.Swarmer);
            float share = total > 0 ? members / (float)total : 0f;
            Console.WriteLine("  wave {0}: {1} squads ({2}), {3} of {4} enemies in squads", w, squads.Count, string.Join(", ", squads.Select(q => q.Name)), members, total);
            tried++;
            if (share > 0.45f && share <= 0.85f) okShare++;
            if (squads.GroupBy(q => q).All(q => q.Count() <= 2)) okRepeat++;
            if (squads.All(q => q.From <= w)) okUnlock++;
        }
        Check(okShare == tried && okRepeat == tried && okUnlock == tried, "waves are about 70% squads, only ones unlocked by then, none more than twice");
        g.Wave = 22;

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

        // The Beam Sentry: plants itself, aims, then sweeps a beam that hurts.
        var sentry = Spawn(EnemyType.BeamSentry, new V2(800f, 450f + 380f), false);
        float taken = me.Run.DamageTaken;
        int phases = 0, clientPhase = 0;
        bool beamHit = false;
        Run(s, dt, 9f, () => beamHit, () =>
        {
            hold();
            beamHit |= sentry.Phase == 3 && me.Run.DamageTaken > taken;
            me.InvulnT = sentry.Phase == 3 ? 0f : 1f;  // only the sweep can hurt you here
            taken = sentry.Phase == 3 ? taken : me.Run.DamageTaken;
            // Stand on its line, just ahead of where it points, so the sweep comes through.
            V2 at = sentry.Phase >= 2 ? sentry.Pos + V2.FromAngle(sentry.Facing) * 300f : new V2(800f, 450f);
            g.Players[1].Pos = c.Players[1].Pos = at;
            g.Players[2].Pos = c.Players[2].Pos = new V2(100f, 100f);
            phases |= 1 << sentry.Phase;
            var cs = c.Enemies.Values.FirstOrDefault(e => e.Id == sentry.Id);
            if (cs != null) clientPhase |= 1 << cs.Phase;
        });
        Console.WriteLine("  beam sentry: phases seen {0} (client {1}), the sweep hurt {2}", Convert.ToString(phases, 2), Convert.ToString(clientPhase, 2), beamHit);
        Check((phases & 0b1100) == 0b1100 && (clientPhase & 0b1000) != 0 && beamHit, "a Beam Sentry plants, aims and sweeps a beam that hurts (seen on the client)");
        clear(); Run(s, dt, 0.2f);

        // The Scorcher: a trail of embers (on the client too) that burns you.
        var scorcher = Spawn(EnemyType.Scorcher, new V2(1050f, 450f), false);
        Run(s, dt, 4f, () => g.Patches.Values.Count(p => p.Kind == PatchKind.Embers) >= 6, () => { hold(); Place(new V2(800f, 450f), new V2(100f, 100f)); me.InvulnT = 1f; });
        Run(s, dt, 0.2f, null, () => { hold(); me.InvulnT = 1f; });
        int embers = g.Patches.Values.Count(p => p.Kind == PatchKind.Embers), clientEmbers = c.Patches.Values.Count(p => p.Kind == PatchKind.Embers);
        var ember = g.Patches.Values.FirstOrDefault(p => p.Kind == PatchKind.Embers && p.Active);
        taken = me.Run.DamageTaken;
        if (ember != null) Run(s, dt, 0.6f, null, () => { hold(); scorcher.FrozenT = 1f; foreach (var b in g.Bullets.Values) if (Defs.IsEnemyShot(b.Kind)) b.Life = 0f; me.InvulnT = 0f; g.Players[1].Pos = c.Players[1].Pos = ember.Pos; });
        Console.WriteLine("  scorcher: {0} embers (client {1}); standing in one hurt {2:0}", embers, clientEmbers, me.Run.DamageTaken - taken);
        Check(embers >= 5 && clientEmbers >= 5 && me.Run.DamageTaken > taken, "a Scorcher leaves a trail of embers (seen on the client) that burns you");
        clear(); Run(s, dt, 0.2f);

        // The Reaver: winds up a cone, rakes you, then is open (+50%).
        Run(s, dt, 0.2f, null, () => { hold(); Place(new V2(800f, 450f), new V2(800f, 470f)); });
        var reaver = Spawn(EnemyType.Reaver, new V2(1000f, 450f), false);
        reaver.Hp = reaver.MaxHp = 1e5f;
        taken = me.Run.DamageTaken;
        bool wound = false;
        Run(s, dt, 5f, () => reaver.Phase == 3, () => { hold(); Place(new V2(800f, 450f), new V2(800f, 470f)); me.InvulnT = 0f; wound |= reaver.Phase == 1; });  // both close, whichever it targets
        bool raked = me.Run.DamageTaken - taken >= Game.ReaverDamage * 0.5f;
        float hp = reaver.Hp; hurt.Invoke(g, new object[] { reaver, 100f, 1 });
        float open = hp - reaver.Hp;
        Run(s, dt, 1f, () => reaver.Phase == 0, () => { hold(); Place(new V2(500f, 450f), new V2(100f, 100f)); me.InvulnT = 1f; });
        hp = reaver.Hp; hurt.Invoke(g, new object[] { reaver, 100f, 1 });
        float closed = hp - reaver.Hp;
        Console.WriteLine("  reaver: wound up {0}, raked {1}, 100 damage while open {2:0}, after {3:0}", wound, raked, open, closed);
        Check(wound && raked, "a Reaver winds up its cone, then rakes you");
        Check(Math.Abs(open - 150f) < 1f && Math.Abs(closed - 100f) < 1f, "it takes +50% while it's open after a swipe");
        clear(); Run(s, dt, 0.2f);

        // The Warder: bubbles on its allies (seen on the client, with the tether), which pop when it dies.
        var warder = Spawn(EnemyType.Warder, new V2(900f, 250f), false);
        var g1 = Spawn(EnemyType.Grunt, new V2(950f, 300f), false);
        var g2 = Spawn(EnemyType.Grunt, new V2(850f, 300f), false);
        Run(s, dt, 3f, () => g1.BubbleHp > 0f && g2.BubbleHp > 0f, () => { hold(); Place(new V2(800f, 700f), new V2(100f, 800f)); foreach (var p in g.Players.Values) p.InvulnT = 1f; warder.WardT = Math.Min(warder.WardT, 0.5f); });
        Run(s, dt, 0.3f, null, () => { hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var cg1 = c.Enemies.Values.FirstOrDefault(e => e.Id == g1.Id);
        bool warded = g1.BubbleHp > 0f && g2.BubbleHp > 0f && g1.WardId == warder.Id && cg1 != null && cg1.BubbleFrac > 0f && cg1.WardId == warder.Id;
        hp = g1.Hp; hurt.Invoke(g, new object[] { g1, 10f, 1 });
        bool soaked = g1.Hp == hp;
        warder.Hp = 0f;
        Run(s, dt, 0.3f, null, () => { hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        Console.WriteLine("  warder: allies warded {0} (client {1}), a hit soaked {2}; after it died their bubbles {3:0} / {4:0}", warded, cg1?.BubbleFrac, soaked, g1.BubbleHp, g2.BubbleHp);
        Check(warded && soaked, "a Warder bubbles its allies, linked to it (seen on the client), and the bubbles soak hits");
        Check(g1.BubbleHp <= 0f && g2.BubbleHp <= 0f, "killing the Warder pops every bubble it held up");
        clear(); Run(s, dt, 0.2f);

        // Elite traits: every elite has one (two from wave 35), seen on the client.
        var elite = Spawn(EnemyType.Grunt, new V2(800f, 200f), true);
        g.Wave = 36;
        var elite2 = Spawn(EnemyType.Grunt, new V2(900f, 200f), true);
        g.Wave = 22;
        Run(s, dt, 0.3f, null, () => { hold(); foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        var ce = c.Enemies.Values.FirstOrDefault(e => e.Id == elite.Id);
        Func<byte, int> Bits = b => Enumerable.Range(0, 8).Count(i => (b & (1 << i)) != 0);
        Console.WriteLine("  elite traits: wave 22 {0} (client {1}), wave 36 {2}", (EliteTrait)elite.Traits, ce == null ? "?" : ((EliteTrait)ce.Traits).ToString(), (EliteTrait)elite2.Traits);
        Check(Bits(elite.Traits) == 1 && Bits(elite2.Traits) == 2 && ce != null && ce.Traits == elite.Traits, "elites roll one trait (two from wave 35), and the client knows which");
        clear(); Run(s, dt, 0.2f);

        Func<EliteTrait, V2, Enemy> Elite = (t, at) => { var e = Spawn(EnemyType.Grunt, at, true); e.Traits = (byte)t; e.BubbleHp = e.BubbleMax = t == EliteTrait.Shielded ? e.MaxHp * Game.ShieldedShare : 0f; return e; };
        // Shielded: a bubble that soaks, and grows back.
        var shd = Elite(EliteTrait.Shielded, new V2(800f, 200f));
        hp = shd.Hp; hurt.Invoke(g, new object[] { shd, shd.BubbleMax * 0.5f, 1 });
        bool soak = shd.Hp == hp && shd.BubbleHp > 0f;
        hurt.Invoke(g, new object[] { shd, shd.BubbleMax, 1 });
        bool through = shd.Hp < hp && shd.BubbleHp <= 0f;
        Run(s, dt, Game.BubbleRegrow + 0.5f, null, () => { hold(); shd.FrozenT = 1f; foreach (var p in g.Players.Values) p.InvulnT = 1f; });
        Check(soak && through && shd.BubbleHp > 0f, "Shielded: a bubble soaks hits first, then grows back 4 s later");
        // Hasted and Commander: faster fire.
        var traitMult = typeof(Game).GetMethod("TraitFireMult", flags);
        var hasted = Elite(EliteTrait.Hasted, new V2(400f, 200f));
        var cmd = Elite(EliteTrait.Commander, new V2(1200f, 200f));
        var follower = Spawn(EnemyType.Grunt, new V2(1300f, 200f), false);
        typeof(Game).GetMethod("PrepareVariety", flags).Invoke(g, null);
        float hm = (float)traitMult.Invoke(g, new object[] { hasted }), fm = (float)traitMult.Invoke(g, new object[] { follower }), am = (float)traitMult.Invoke(g, new object[] { anchor });
        Console.WriteLine("  fire rate: hasted x{0}, near a commander x{1}, far away x{2}", hm, fm, am);
        Check(Math.Abs(hm - 1.4f) < 0.01f && Math.Abs(fm - 1.3f) < 0.01f && am == 1f, "Hasted fires 40% faster; allies near a Commander 30% faster");
        clear(); Run(s, dt, 0.2f);
        // Volatile: a burst of 16 shots after its fuse. Splitting: Shards.
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
        Check(burst >= 14 && shards == 2, "Volatile bursts into a ring when it dies; Splitting leaves Shards");
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
        s[0].Leave(""); s[1].Leave("");
    }
}

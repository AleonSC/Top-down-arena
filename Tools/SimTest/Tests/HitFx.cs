using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

static partial class Program
{
    /// Hit feedback that doesn't pile up: a burning enemy isn't flashed white by its fire (host or client), a real hit
    /// still flashes it, steady fire builds FlashHeat (the flash softens) and it cools once the hits stop, and Explosive
    /// Rounds on a fast weapon show far fewer blasts than they deal (the damage all lands).
    static void HitFxTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);

        // --- Burning alone doesn't flash; a hit does (host and client) ----------------------------------------------
        var s = StartPlain(dt, 3);
        var g = s[0].Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        ClearSquads(g);
        foreach (var e0 in g.Enemies.Values) e0.Hp = 0f;
        Run(s, dt, 0.3f);
        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(1100f, 450f), 0f, 0, false });
        e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; e.FireT = 99f;
        Run(s, dt, 0.5f, null, () => { e.FrozenT = 1f; });
        e.BurnPool = 3000f; e.PoolT = 3f; e.BurnBy = 1;
        bool hostFlash = false, clientFlash = false, clientBurning = false;
        float clientHp0 = s[1].Game.Enemies.TryGetValue(e.Id, out var ce0) ? ce0.Hp : 0f;
        Run(s, dt, 1.5f, null, () =>
        {
            e.FrozenT = 1f;
            hostFlash |= e.Flash > 0f;
            Enemy ce;
            if (s[1].Game.Enemies.TryGetValue(e.Id, out ce)) { clientFlash |= ce.Flash > 0f; clientBurning |= ce.Burning; }
        });
        Enemy ce1;
        float clientLost = s[1].Game.Enemies.TryGetValue(e.Id, out ce1) ? clientHp0 - ce1.Hp : 0f;
        Console.WriteLine("  burning 1.5 s: client saw it lose {0:0} HP, burning {1}; flash on host {2}, on client {3}", clientLost, clientBurning, hostFlash, clientFlash);
        Check(clientLost > 100f && clientBurning, "a burning enemy loses health on the client and shows as burning");
        Check(!hostFlash && !clientFlash, "fire alone never flashes an enemy white (it only wears its health down)");

        bool clientHit = false;
        hurt.Invoke(g, new object[] { e, 10f, 1 });
        Run(s, dt, 0.15f, null, () => { Enemy ce; if (s[1].Game.Enemies.TryGetValue(e.Id, out ce)) clientHit |= ce.Flash > 0f; });
        Check(clientHit, "a real hit still flashes it on the client (the snapshot's hit flag)");

        // --- FlashHeat: up under steady fire, down after -------------------------------------------------------------
        e.BurnPool = 0f; e.PoolT = 0f;
        Run(s, dt, 1f, null, () => { e.FrozenT = 1f; hurt.Invoke(g, new object[] { e, 1f, 1 }); });
        float heatUp = e.FlashHeat;
        Enemy ce2;
        float clientHeat = s[1].Game.Enemies.TryGetValue(e.Id, out ce2) ? ce2.FlashHeat : 0f;
        Run(s, dt, 1f, null, () => { e.FrozenT = 1f; });
        float heatDown = e.FlashHeat;
        Console.WriteLine("  flash heat: {0:0.00} after 1 s of hits (client {1:0.00}), {2:0.00} 1 s after they stop", heatUp, clientHeat, heatDown);
        Check(heatUp > 0.95f && clientHeat > 0.8f && heatDown < 0.05f, "steady hits build flash heat on host and client, and it cools once they stop");
        s[0].Leave(""); s[1].Leave("");

        // --- Explosive Rounds: the blasts shown are thinned, the damage isn't ---------------------------------------
        var host = new Session { AutoStart = 1, StartWave = 3 };
        host.Host(nextPort++, "A", Role.Tank, 0);
        host.StartMatch();
        var hg = host.Game;
        for (int i = 0; i < 300 && !hg.Running; i++) { host.Poll(); hg.FixedStep(dt, new PlayerInput()); }
        hg.DevSetGod(1, true);
        ((List<EnemyType>)queueField.GetValue(hg)).Clear();
        ClearSquads(hg);
        foreach (var e0 in hg.Enemies.Values) e0.Hp = 0f;
        for (int f = 0; f < 3; f++) { host.Poll(); hg.FixedStep(dt, new PlayerInput()); }
        hg.Bullets.Clear();
        var me = hg.Players[1];
        me.Weapon = Defs.Weapons.First(w => w.Id == WeaponId.BurstSmg);
        hg.GrantUpgrade(1, Array.FindIndex(Upgrades.All, u => u.Name == "Explosive Rounds"));
        V2 from = new V2(400f, 450f), at = from + new V2(200f, 0f);
        var target = (Enemy)spawn.Invoke(hg, new object[] { EnemyType.Brute, at, 0f, 0, false });
        var near = (Enemy)spawn.Invoke(hg, new object[] { EnemyType.Brute, at + new V2(0f, 30f), 0f, 0, false });
        foreach (var x in new[] { target, near }) { x.Hp = x.MaxHp = 1e6f; x.SpawnT = 0f; x.FireT = 99f; }
        var seenFx = new HashSet<Effect>();
        var seenShots = new HashSet<int>();
        int booms = 0;
        for (int f = 0; f < 60 * 3; f++)
        {
            target.Pos = at; near.Pos = at + new V2(0f, 30f); target.FrozenT = near.FrozenT = 1f; me.Pos = from;
            host.Poll();
            hg.FixedStep(dt, new PlayerInput { Fire = true, AimAt = at });
            foreach (var fx in hg.Effects)
                if (fx.Kind == FxKind.Boom && seenFx.Add(fx))
                    booms++;
            foreach (var b in hg.Bullets.Values)
                if (b.Owner == 1)
                    seenShots.Add(b.Id);
        }
        float splashDmg = near.MaxHp - near.Hp;
        Console.WriteLine("  Explosive Rounds, Burst SMG 3 s: {0} shots, {1} blasts shown, {2:0} splash damage on the enemy beside it", seenShots.Count, booms, splashDmg);
        Check(splashDmg > 0f && seenShots.Count >= 30, "Explosive Rounds still hurt the enemy next to the one hit");
        Check(booms > 0 && booms <= 3 * 60 / Game.SplashFxGap + 3 && booms < seenShots.Count * 0.75f, "the blasts on one spot are thinned to one every few ticks (fewer blasts than hits)");
        host.Leave("");
    }
}

static partial class Program
{
    /// What a regular wave is made of, by what each enemy does: shoots, melee (contact, charges, lunges) or support
    /// (heals, bubbles, spawns). Averaged over many built waves (two players, Normal). Docs/EnemyRoles.md.
    /// Recipe waves show a bounded number of distinct enemy types (Docs/EnemyReadability.md section 3).
    static void WaveKindsTest()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = typeof(Game).GetMethod("BuildWave", flags);
        var queueF = typeof(Game).GetField("spawnQueue", flags);
        var squadF = typeof(Game).GetField("squadQueue", flags);
        var g = new Session().Game;
        foreach (int n in new[] { 13, 14, 17, 20, 22, 24, 25, 27, 29, 33, 37 })
        {
            int cap = Game.KindCap(n), most = 0;
            for (int r = 0; r < 60; r++)
            {
                build.Invoke(g, new object[] { n });
                var kinds = new HashSet<EnemyType>((List<EnemyType>)queueF.GetValue(g));
                foreach (var sq in (List<SquadDef>)squadF.GetValue(g)) foreach (var m in sq.Members) kinds.Add(m);
                if (kinds.Any(k => Zombies.Is(k))) continue;  // a Zombie Infection horde is its own mode, not the recipe
                if (kinds.Count > most) { most = kinds.Count; if (most > cap) Console.WriteLine("    over the cap: " + string.Join(", ", kinds)); }
            }
            Console.WriteLine("  wave {0}: at most {1} distinct types (cap {2})", n, most, cap);
            Check(most <= cap, "wave " + n + " shows " + most + " types against a cap of " + cap);
        }
    }

    /// The Phantom's blink and the Mortar's bomb mark where they land (a Target ring) before they burst.
    static void LandingMarksTest()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var special = typeof(Game).GetMethod("Special", flags);
        var fire = typeof(Game).GetMethod("EnemyFire", flags);
        var g = new Session().Game;
        g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
        var target = new V2(600f, 400f);
        var ph = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Phantom, new V2(200f, 200f), 0f, 0, false });
        ph.Spawning = false; ph.PhaseT = 0f;
        special.Invoke(g, new object[] { ph, target, 0.016f });
        bool marked = g.Effects.Exists(fx => fx.Kind == FxKind.Target && fx.Pos.DistanceTo(ph.Waypoint) < 1f);
        Check(ph.Phase == 1 && marked && g.Bullets.Count == 0, "a Phantom marks where it will blink and fires nothing yet");
        V2 before = ph.Pos;
        special.Invoke(g, new object[] { ph, target, Game.PhantomWarn * 0.5f });
        Check(ph.Pos.DistanceTo(before) < 1f && g.Bullets.Count == 0, "...and waits out the warning");
        special.Invoke(g, new object[] { ph, target, Game.PhantomWarn });
        Check(ph.Phase == 0 && ph.Pos.DistanceTo(ph.Waypoint) < 1f && g.Bullets.Count == 8, "then blinks to the marked spot and fires its ring of 8");
        g.Effects.Clear(); g.Bullets.Clear();
        var mo = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Mortar, new V2(200f, 200f), 0f, 0, false });
        mo.Spawning = false; mo.FireT = 0f;
        fire.Invoke(g, new object[] { mo, target });
        Check(g.Effects.Exists(fx => fx.Kind == FxKind.Target), "a Mortar's bomb comes with a landing ring");
        g.Effects.Clear(); g.Bullets.Clear();
        var ch = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Charger, new V2(200f, 400f), 0f, 0, false });
        ch.Spawning = false; ch.FireT = 0f;
        fire.Invoke(g, new object[] { ch, new V2(900f, 400f) });
        V2 end = new V2(200f + Game.ChargerDashSpeed * Game.ChargerDashTime, 400f);
        Check(ch.Phase == 1 && g.Effects.Exists(fx => fx.Kind == FxKind.Target && fx.Pos.DistanceTo(end) < 2f), "a Charger marks where its dash ends (and its ring will burst) from the start of the wind-up");
    }

    /// The new-enemy chips: which types a wave brings, and a note for each.
    static void NewEnemiesTest()
    {
        Check(Game.NewTypesAt(1).Contains(EnemyType.Grunt) && Game.NewTypesAt(1).Contains(EnemyType.Swarmer), "wave 1 brings the Grunt and the Swarmer");
        Check(Game.NewTypesAt(9).SequenceEqual(new[] { EnemyType.Shielder }), "wave 9 brings the Shielder only");
        Check(Enumerable.Range(1, 40).All(n => !Game.NewTypesAt(n).Any(Game.IsRetired)), "no wave brings a retired enemy as new (BasicFaction.cs)");
        Check(Game.NewTypesAt(5).Contains(EnemyType.Lunger) && !Game.NewTypesAt(5).Contains(EnemyType.SpikeBlock), "wave 5 brings the Lunger (the melee grunts are retired)");
        var missing = new List<EnemyType>();
        for (int n = 1; n <= 40; n++)
            foreach (var t in Game.NewTypesAt(n))
                if (Game.EnemyNote(t).Length == 0 || Game.EnemyNote(t).Length > 70)
                    missing.Add(t);
        Check(missing.Count == 0, "every new type has a note of up to 70 characters" + (missing.Count > 0 ? " (" + string.Join(", ", missing) + ")" : ""));
        var seen = new HashSet<EnemyType>();
        bool dup = false;
        for (int n = 1; n <= 40; n++)
            foreach (var t in Game.NewTypesAt(n))
                dup |= !seen.Add(t);
        Check(!dup, "no type is new twice");
    }

    static void WaveMixReport()
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var build = typeof(Game).GetMethod("BuildWave", flags);
        var queueF = typeof(Game).GetField("spawnQueue", flags);
        var squadF = typeof(Game).GetField("squadQueue", flags);
        var support = new HashSet<EnemyType> { EnemyType.Medic, EnemyType.Warder, EnemyType.Nest };
        var g = new Session().Game;
        Console.WriteLine("  wave  enemies  shoot  melee  support  heads  kinds   most common   (heads: the wave's headliners, from wave 13)");
        foreach (int n in new[] { 1, 3, 6, 9, 12, 14, 17, 19, 22, 24, 27, 29, 33, 37, 39 })
        {
            int shoot = 0, melee = 0, sup = 0, total = 0, runs = 40, heads = 0;
            var kinds = new Dictionary<EnemyType, int>();
            for (int r = 0; r < runs; r++)
            {
                build.Invoke(g, new object[] { n });
                var all = new List<EnemyType>((List<EnemyType>)queueF.GetValue(g));
                foreach (var sq in (List<SquadDef>)squadF.GetValue(g)) all.AddRange(sq.Members);
                if (n >= Game.RecipeFrom) heads += all.Count(t => g.Headliners.Contains(t));
                foreach (var t in all)
                {
                    total++;
                    kinds[t] = kinds.TryGetValue(t, out int k) ? k + 1 : 1;
                    if (support.Contains(t)) sup++;
                    else if (Defs.Get(t).Melee) melee++;  // contact, charges, lunges, and the enemies that don't shoot (Hookman, Sapper)
                    else shoot++;
                }
            }
            var top = kinds.OrderByDescending(kv => kv.Value).Take(4).Select(kv => string.Format("{0} {1:0}%", kv.Key, 100f * kv.Value / total));
            Console.WriteLine("  {0,4}  {1,7:0}  {2,4:0}%  {3,4:0}%  {4,6:0}%  {5,4:0}%  {6,5}   {7}", n, (float)total / runs, 100f * shoot / total, 100f * melee / total, 100f * sup / total, 100f * heads / total, kinds.Count, string.Join(", ", top));
        }
    }
}

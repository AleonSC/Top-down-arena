using System.Linq;
using System;
using System.Collections.Generic;
using System.Threading;
using Arena;

// The Twin Reapers' legendaries: Twin Scythes, Final Reap and Scythe Throw (ReaperLoot.cs).
static partial class Program
{
    static void ReaperLootTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var hurt = typeof(Game).GetMethod("HurtEnemy", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);

        // Their pool: the three new cards; Executioner, Twin Fang and Soul Harvest stay but are never offered.
        var own = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].Boss == EnemyType.TwinA).Select(i => Upgrades.All[i].Name).ToArray();
        bool neverOffered = true;
        var rng = new Random(7);
        for (int k = 0; k < 300; k++)
            neverOffered &= !Upgrades.LegendaryOffer(rng, (EnemyType)rng.Next(0, 60), Role.Dps, AbilityId.Dash, new List<int>())
                .Any(i => Upgrades.All[i].Name == "Executioner" || Upgrades.All[i].Name == "Twin Fang" || Upgrades.All[i].Name == "Soul Harvest");
        Console.WriteLine("  the Twin Reapers' legendaries: {0}; the old ones never offered in 300 boss offers: {1}", string.Join(", ", own), neverOffered);
        Check(own.Length == 3 && own.Contains("Twin Scythes") && own.Contains("Final Reap") && own.Contains("Scythe Throw")
            && Upgrades.All[U("Scythe Throw")].Grants == AbilityId.ScytheThrow && U("Executioner") >= 0 && neverOffered,
            "the Twin Reapers offer Twin Scythes, Final Reap and Scythe Throw; Executioner, Twin Fang and Soul Harvest are kept but never offered");

        var s = StartBuffed(dt, 3, 0);
        var g = s[0].Game;
        g.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var hp = g.Players[2];
        var cp = s[1].Game.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        var keep = new HashSet<Enemy>();
        var pins = new Dictionary<Enemy, V2>();
        Action clear = () => { ((List<EnemyType>)queueField.GetValue(g)).Clear(); ClearSquads(g); foreach (var e in g.Enemies.Values) if (!keep.Contains(e)) e.Hp = 0f; };
        Func<V2, Enemy> dummy = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e6f; e.SpawnT = 0f; keep.Add(e); pins[e] = at; return e; };
        V2 home = new V2(500f, 450f);
        Action<PlayerInput> step = input =>
        {
            clear();
            foreach (var e in keep) { e.FireT = 9f; e.Speed = 0f; if (pins.ContainsKey(e)) e.Pos = pins[e]; }
            g.State = MatchState.Fighting;
            hp.Pos = home;
            s[0].Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 100f) }); g.Update(dt);
            s[1].Poll(); s[1].Game.FixedStep(dt, input); s[1].Game.Update(dt);
        };
        Action<float> drive = secs => { for (int f = 0; f < secs * 60; f++) { step(new PlayerInput { AimAt = home + new V2(100f, 0f) }); if (f % 2 == 0) Thread.Sleep(1); } };
        Action reset = () => { foreach (var e in keep) e.Hp = 0f; keep.Clear(); pins.Clear(); drive(0.1f); };
        cp.Pos = cp.Disp = hp.Pos = home;
        drive(0.3f);

        // Twin Scythes: Dusk close, Dawn wide, turning opposite ways; they cut what they pass, and small bullets.
        g.GrantUpgrade(2, U("Twin Scythes"));
        drive(0.5f);
        Func<Game, List<Minion>> scythes = gm => gm.Minions.Values.Where(m => m.Owner == 2 && m.Kind == MinionKind.Scythe).ToList();
        var rings = scythes(g).Select(m => m.Pos.DistanceTo(hp.Pos)).OrderBy(x => x).ToList();
        int clientScythes = scythes(s[1].Game).Count;
        Console.WriteLine("  twin scythes: {0} round the friend at {1} px (their game sees {2})", rings.Count, string.Join(" and ", rings.Select(x => x.ToString("0"))), clientScythes);
        Check(rings.Count == 2 && Math.Abs(rings[0] - Game.ScytheInner) < 2f && Math.Abs(rings[1] - Game.ScytheOuter) < 2f && clientScythes == 2,
            "Twin Scythes: two scythes circle you, one close and one wide (seen on your own game too)");
        var near = dummy(home + new V2(Game.ScytheInner, 0f));
        drive(5f);  // the close one circles about every 2 s
        float cut = near.MaxHp - near.Hp, each = Kits.Get(MinionKind.Scythe).Damage * hp.Stats.DmgMult;
        var bullet = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, scythes(g)[0].Pos, new V2(0f, 0f), 0f, 3f, 5f, 0, false, 0f });
        drive(0.05f);
        bool bulletCut = !g.Bullets.ContainsKey(bullet.Id);
        Console.WriteLine("  twin scythes: an enemy on the close ring took {0:0.0} in 5 s ({1:0.0} a cut); a small enemy bullet on a blade was cut: {2}", cut, each, bulletCut);
        Check(cut >= each * 2f && Math.Abs(cut / each - Math.Round(cut / each)) < 0.01f && bulletCut,
            "the scythes cut enemies they pass (once per pass) and slice small enemy bullets out of the air");
        foreach (var m in scythes(g)) g.Minions.Remove(m.Id);
        hp.Taken.Remove(U("Twin Scythes"));
        g.GrantUpgrade(2, U("Vitality"));
        reset();

        // Final Reap: a hit that leaves a regular enemy below 25% reaps it and heals you 3; bosses below 40% take double.
        g.GrantUpgrade(2, U("Final Reap"));
        g.DevSetGod(2, false);
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, home + new V2(200f, 0f), 0f, 0, false });
        grunt.Hp = grunt.MaxHp = 1000f; grunt.SpawnT = 0f; keep.Add(grunt);
        hp.Hp = hp.MaxHp * 0.5f;
        float before = hp.Hp, regen0 = hp.Stats.Regen;
        hurt.Invoke(g, new object[] { grunt, 880f, 2 });  // leaves 120: 12%
        bool reaped = grunt.Hp <= 0f;
        float healed = hp.Hp - before;
        var big = dummy(home + new V2(-200f, 0f));
        var bossDef = (EnemyDef)typeof(object).GetMethod("MemberwiseClone", flags).Invoke(big.Def, null);
        bossDef.IsBoss = true; big.Def = bossDef;
        big.Hp = big.MaxHp * 0.5f;
        float h0 = big.Hp; hurt.Invoke(g, new object[] { big, 100f, 2 }); float hitHigh = h0 - big.Hp;
        big.Hp = big.MaxHp * 0.25f;
        h0 = big.Hp; hurt.Invoke(g, new object[] { big, 100f, 2 }); float hitLow = h0 - big.Hp;
        g.DevSetGod(2, true);
        Console.WriteLine("  final reap: a grunt left at 12% was reaped {0}, the friend healed {1:0.0}; a boss hit for 100 took {2:0.0} at half health and {3:0.0} below 40%",
            reaped, healed, hitHigh, hitLow);
        Check(reaped && Math.Abs(healed - Game.FinalReapHeal) < 0.5f && Math.Abs(hitLow / hitHigh - 2f) < 0.01f,
            "Final Reap: enemies your hit leaves below 25% are reaped (healing you 3), and bosses below 40% take double damage");
        hp.Taken.Remove(U("Final Reap"));
        g.GrantUpgrade(2, U("Vitality"));
        reset();

        // Scythe Throw: on a free key; out to the cursor and back, cutting both ways; catching it cuts the cooldown
        // (on your own game too).
        g.GrantUpgrade(2, U("Scythe Throw"));
        drive(0.3f);
        int slot = Array.FindIndex(hp.Slots, k => k.Id == AbilityId.ScytheThrow);
        Check(slot >= 1 && slot <= 3 && cp.Slots[slot].Id == AbilityId.ScytheThrow, "Scythe Throw goes on a Q / E / Shift key");
        var target = dummy(home + new V2(200f, 0f));
        cp.Pos = cp.Disp = home;
        step(new PlayerInput { AbilityQ = slot == 1, AbilityE = slot == 2, AbilityShift = slot == 3, AimAt = home + new V2(300f, 0f) });
        float prev = cp.Slots[slot].Timer, drop = 0f;
        bool flew = false;
        for (int f = 0; f < 60 * 2; f++)
        {
            step(new PlayerInput { AimAt = home + new V2(300f, 0f) });
            flew |= g.Minions.Values.Any(m => m.Owner == 2 && m.Kind == MinionKind.ThrownScythe);
            float now = cp.Slots[slot].Timer;
            if (prev - now > 0.5f) drop = prev - now;
            prev = now;
        }
        float thrown = target.MaxHp - target.Hp, per = Kits.Get(MinionKind.ThrownScythe).Damage * hp.Stats.DmgMult;
        bool gone = !g.Minions.Values.Any(m => m.Owner == 2 && m.Kind == MinionKind.ThrownScythe);
        Console.WriteLine("  scythe throw: flew {0}; the enemy in its path took {1:0.0} ({2:0.0} each way); caught (gone) {3}; the friend's own cooldown dropped {4:0.0} s at the catch, now {5:0.0} s",
            flew, thrown, per, gone, drop, cp.Slots[slot].Timer);
        Check(flew && Math.Abs(thrown - 2f * per) < 1f && gone && drop > 1.5f,
            "Scythe Throw cuts on the way out and back, and catching it takes 40% off the cooldown on your own game");

        foreach (var x in s) x.Leave("");
    }
}

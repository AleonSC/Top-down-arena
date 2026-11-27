using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The DPS Ninja's Dragon Fang Kunai (DragonFang.cs).
static partial class Program
{
    /// One huge kunai at a time; one bite with no cards, Dragon's Hunger +4 a card up to 12; with them it bites 12 enemies in turn, 10% harder each bite, never one twice in a row of new ones; the other
    /// screen sees each bite (FxKind.DragonFangBite) and the bullet; a lone target is bitten again off the arena's edges; the last
    /// bite bursts; the chain leaves the Kunai's mark to blink to; an extra projectile adds two bites.
    static void DragonFangTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Ninja", Role.Dps, 2, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.DragonFangKunai && me.Weapon.Name == "Dragon Fang Kunai" && Defs.Weapon(Role.Dps, Kit.Ninja, 2) == me.Weapon,
            "the DPS Ninja's third weapon is the Dragon Fang Kunai (" + me.Weapon.Name + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 lo = g.ArenaMin, hi = g.ArenaMax;
        V2 from = new V2(lo.X + 200f, (lo.Y + hi.Y) / 2f);
        var dummies = new List<Enemy>();
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            dummies.Add(d);
            return d;
        };
        var pins = new Dictionary<Enemy, V2>();
        Action<float, V2, bool> Step = (secs, cursor, fire) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var d in dummies) { d.Pos = pins.ContainsKey(d) ? pins[d] : d.Pos; d.FireT = 99f; }
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire && f == 0 } : new PlayerInput()); }
            }
        };
        Func<int> fangs = () => g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind == Defs.DragonFang);
        Action Clear = () => { foreach (var d in dummies) d.Hp = 0f; dummies.Clear(); pins.Clear(); Step(0.3f, from, false); g.Enemies.Clear(); g.Bullets.Clear(); Step(0.1f, from, false); client.Game.Bullets.Clear(); client.Game.Effects.Clear(); me.FireCd = 0f; };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // By default it bites one: the first enemy, then it's gone.
        V2 c0 = from + new V2(260f, 0f);
        var one = Dummy(c0); pins[one] = c0;
        var two = Dummy(c0 + new V2(150f, 0f)); pins[two] = c0 + new V2(150f, 0f);
        Check(g.DragonFangMax(me) == 1, "with no cards it bites one enemy a throw (" + g.DragonFangMax(me) + ")");
        Step(dt, c0, true);
        Step(1f, c0, false);
        Check(one.Hp < one.MaxHp && two.Hp == two.MaxHp && fangs() == 0, "one bite, then it's gone (the one beside it untouched)");
        var hunger = Upgrades.All.First(u => u.Name == "Dragon's Hunger");
        int[] maxes = new int[4];
        for (int k = 0; k < 4; k++) { maxes[k] = g.DragonFangMax(me); if (k < 3) hunger.Apply(me.Stats); }
        Check(maxes[0] == 1 && maxes[1] == 5 && maxes[2] == 9 && maxes[3] == 12 && hunger.MaxStacks == 3, "Dragon's Hunger: +4 bites a card, three cards to 12 (" + string.Join(",", maxes) + ")");
        Clear();

        // A 4 x 4 grid 150 px apart (the burst's 120 px reaches none of the others): 12 bitten, each 10% harder than the last.
        V2 corner = from + new V2(260f, -225f);
        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                pins[Dummy(corner + new V2(150f * x, 150f * y))] = corner + new V2(150f * x, 150f * y);
        int most = 0;
        bool clientSaw = false;
        Step(dt, corner + new V2(0f, 150f), true);
        for (int f = 0; f < 4f / dt; f++)
        {
            Step(dt, corner, false);
            most = Math.Max(most, fangs());
            clientSaw |= client.Game.Bullets.Values.Any(b => b.Kind == Defs.DragonFang);
        }
        var bitten = dummies.Where(d => d.Hp < d.MaxHp).Select(d => d.MaxHp - d.Hp).OrderBy(v => v).ToList();
        bool grows = bitten.Count == Game.DragonFangHits;
        for (int k = 1; grows && k < bitten.Count; k++)
            grows = Math.Abs(bitten[k] / bitten[0] - Game.DragonFangBiteMult(k + 1)) < 0.02f;
        Console.WriteLine("  grid: {0} bitten ({1}), most out at once {2}", bitten.Count, string.Join(" ", bitten.Select(v => v.ToString("0"))), most);
        Check(bitten.Count == Game.DragonFangHits, "one throw bites 12 enemies (" + bitten.Count + ")");
        Check(grows, "each bite is 10% harder than the one before");
        Check(Math.Abs(bitten[0] - 60f * me.Stats.DmgMult) < 1f, "the first bite is 60 (" + bitten[0].ToString("0.#") + ")");
        Check(most == 1 && fangs() == 0, "one kunai at a time, and it's gone after the 12th bite");
        Check(clientSaw, "the other screen sees the kunai");
        Check(me.KunaiMarkT > 0f, "the chain leaves the Kunai's mark");

        // Held Fire with one out throws no second.
        Clear();
        pins[Dummy(from + new V2(250f, 0f))] = from + new V2(250f, 0f);
        int held = 0;
        for (int f = 0; f < 0.8f / dt; f++)
        {
            me.Pos = from; queue.Clear();
            foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = from + new V2(250f, 0f), Fire = true } : new PlayerInput()); }
            held = Math.Max(held, fangs());
        }
        Check(held == 1, "holding Fire with a kunai out throws no second (" + held + " out)");

        // A lone target: off the arena's edges it comes back for it, 12 bites in all.
        Clear();
        V2 lone = from + new V2(160f, 0f);
        var boss = Dummy(lone);
        pins[boss] = lone;
        var bites = new HashSet<float>();
        Step(dt, lone, true);
        for (int f = 0; f < 25f / dt && fangs() + (f < 5 ? 1 : 0) > 0; f++)
        {
            Step(dt, lone, false);
            foreach (var e in client.Game.Effects)
                if (e.Kind == FxKind.DragonFangBite && e.Pos2.Y >= 1f)
                    bites.Add(e.Pos2.Y);
        }
        float total = boss.MaxHp - boss.Hp, want = 0f;
        for (int k = 1; k <= Game.DragonFangHits; k++)
            want += 60f * me.Stats.DmgMult * Game.DragonFangBiteMult(k);
        Console.WriteLine("  lone target: {0:0} damage (all 12 bites: {1:0}); the other screen saw bites {2}", total, want, string.Join(",", bites.OrderBy(v => v)));
        Check(Math.Abs(total - want) < 2f, "a lone target takes all 12 bites, off the edges (" + total.ToString("0") + " of " + want.ToString("0") + ")");
        Check(bites.Count == Game.DragonFangHits && bites.Max() == Game.DragonFangHits, "the other screen sees every bite and its count (FxKind.DragonFangBite)");

        // The last bite bursts: an enemy beside the 12th takes half of it. Two enemies 100 px apart, the lone chain ends on one.
        Clear();
        var a = Dummy(lone);
        var b2 = Dummy(lone + new V2(0f, 100f));
        pins[a] = lone; pins[b2] = lone + new V2(0f, 100f);
        Step(dt, lone, true);
        for (int f = 0; f < 25f / dt && (f < 5 || fangs() > 0); f++)
            Step(dt, lone, false);
        float da = a.MaxHp - a.Hp, db = b2.MaxHp - b2.Hp;
        float sum = 0f;
        for (int k = 1; k <= Game.DragonFangHits; k++)
            sum += 60f * me.Stats.DmgMult * Game.DragonFangBiteMult(k);
        float burst = 60f * me.Stats.DmgMult * Game.DragonFangBiteMult(Game.DragonFangHits) * Game.DragonFangBurstShare;
        Console.WriteLine("  burst: the two took {0:0} + {1:0} = {2:0}; 12 bites {3:0} + the burst {4:0}", da, db, da + db, sum, burst);
        Check(Math.Abs(da + db - sum - burst) < 2f, "the 12th bite bursts for half of it on what's beside it");

        // An extra projectile: two more bites, still never past 12.
        Clear();
        me.Stats.FangBites = 0;
        me.Stats.ExtraShots += 1;
        int withExtra = g.DragonFangMax(me);
        me.Stats.FangBites = 11;
        int capped = g.DragonFangMax(me);
        me.Stats.ExtraShots -= 1;
        Check(withExtra == 1 + Game.DragonFangExtraHits && capped == Game.DragonFangHits, "an extra projectile adds two bites, up to 12 (" + withExtra + ", " + capped + ")");

    }
}

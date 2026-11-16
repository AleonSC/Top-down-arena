using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Arc Lightning's lock-on chain and its Alt, Forked Volley (ArcLock.cs).
static partial class Program
{
    /// Holding tags enemies in the cone, the one nearest the cursor first, one every ArcTagGap, up to ten; a tag sticks when the
    /// enemy leaves the cone; the other screen sees the tags; letting go chains through them in order, a hop at a time, each for
    /// (16 + 4 per tag) and a stun; nothing tagged fizzles; Forked Volley hits the three enemies nearest the cursor, then cools down;
    /// Static Field tags faster and Barrage adds tags.
    static void ArcLockTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Caster", Role.Dps, 1, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.ArcLightning && Game.ChargeWeapon(WeaponId.ArcLightning), "the Elementalist DPS's second weapon is Arc Lightning, held to charge (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, V2, bool, bool> Step = (secs, aimAt, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aimAt, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(0.5f, from + new V2(200f, 0f), false, false);

        // Tagging: the one nearest the cursor first, one at a time; not behind you or past the reach.
        var a = Dummy(from + new V2(160f, 10f));
        var b = Dummy(from + new V2(250f, 120f));
        var c = Dummy(from + new V2(300f, -110f));
        var behind = Dummy(from + new V2(-160f, 0f));
        var past = Dummy(from + new V2(Game.ArcConeReach + 120f, 0f));
        V2 cursor = from + new V2(170f, 0f);
        float gap = g.ArcTagGap(me);
        Step(dt * 3f, cursor, true, false);
        Check(me.ArcTags.Count == 1 && me.ArcTags[0] == a.Id, "holding tags the enemy nearest the cursor first (" + me.ArcTags.Count + " tagged)");
        Check(g.Effects.Any(e => e.Kind == FxKind.ArcTag && (int)e.Pos2.Y == a.Id), "a tag is sent as FxKind.ArcTag");
        Step(gap * 0.6f, cursor, true, false);
        Check(me.ArcTags.Count == 1, "the next tag waits ArcTagGap (" + gap.ToString("0.00") + " s)");
        Step(gap * 3f, cursor, true, false);
        Check(me.ArcTags.Count == 3 && !me.ArcTags.Contains(behind.Id) && !me.ArcTags.Contains(past.Id), "then the rest of the cone (" + me.ArcTags.Count + "), not behind you or past its reach");
        // A tagged enemy walking out of the cone stays tagged.
        b.Pos = from + new V2(-120f, 60f);
        Step(0.2f, cursor, true, false);
        Check(me.ArcTags.Contains(b.Id), "a tag sticks after the enemy leaves the cone");
        Step(dt * 4f, cursor, true, false);
        Check(client.Game.Players[1].ArcTags.SequenceEqual(me.ArcTags), "the other screen sees the tags");

        // Letting go: the chain, a hop at a time, in tag order; each for 16 + 4 a tag; stunned; the untagged untouched.
        int n = me.ArcTags.Count;
        var order = me.ArcTags.Select(id => g.Enemies[id]).ToList();
        Step(dt * 2f, cursor, false, false);
        Check(me.ArcTags.Count == 0 && took(order[0]) > 0f && took(order[n - 1]) == 0f, "letting go starts the chain at the first tag; the last waits its hop");
        Step(0.5f, cursor, false, false);
        float each = (me.Weapon.Damage + Game.ArcPerTag * n) * me.Stats.DmgMult;
        Console.WriteLine("  arc lock: {0} tagged, each hit {1:0.0} (expected {2:0.0}); behind {3:0.0}, past {4:0.0}", n, took(order[0]), each, took(behind), took(past));
        Check(order.All(e => Math.Abs(took(e) - each) < each * 0.05f), "every tag takes 16 + 4 per tag (" + each.ToString("0.#") + ")");
        Check(took(behind) == 0f && took(past) == 0f, "nothing untagged is hit");
        Check(g.Effects.Count(e => e.Kind == FxKind.ArcLink && e.Radius >= 0f) >= n, "each hop is sent as FxKind.ArcLink");
        Check(order.All(e => e.FrozenT > 0f || took(e) > 0f), "the chain stuns");

        // Ten at most.
        g.Enemies.Clear();
        for (int i = 0; i < 13; i++)
            Dummy(from + new V2(120f + 15f * i, (i % 2 == 0 ? 1f : -1f) * (10f + 6f * i)));
        Step(Game.ArcTagEvery * 20f, cursor, true, false);
        Check(me.ArcTags.Count == Game.ArcMaxTags, "up to ten tags (" + me.ArcTags.Count + ")");
        Step(1f, cursor, false, false);

        // Nothing tagged: a fizzle.
        g.Enemies.Clear(); g.Effects.Clear();
        Step(0.3f, cursor, true, false);
        Step(0.1f, cursor, false, false);
        Check(g.Effects.Any(e => e.Kind == FxKind.ArcLink && e.Radius < 0f), "letting go with nothing tagged fizzles");

        // Forked Volley: the three nearest the cursor, 14 each; a fourth further out untouched; then a cooldown.
        var f1 = Dummy(from + new V2(280f, -40f));
        var f2 = Dummy(from + new V2(320f, 30f));
        var f3 = Dummy(from + new V2(250f, 70f));
        var f4 = Dummy(from + new V2(300f, 260f));
        V2 fcur = from + new V2(290f, 10f);
        g.Effects.Clear();
        Step(0.4f, fcur, false, true);
        float bolt = Game.ForkDamage * me.Stats.DmgMult;
        Console.WriteLine("  forked volley: {0:0.0} / {1:0.0} / {2:0.0}, far {3:0.0} (expected {4:0.0} each)", took(f1), took(f2), took(f3), took(f4), bolt);
        Check(new[] { f1, f2, f3 }.All(e => Math.Abs(took(e) - bolt) < bolt * 0.05f) && took(f4) == 0f, "Forked Volley hits the three enemies nearest the cursor for 14 each");
        Check(g.Effects.Count(e => e.Kind == FxKind.ArcFork) == 3, "three bolts, sent as FxKind.ArcFork");
        float before = took(f1);
        Step(0.4f, fcur, false, true);
        Check(took(f1) == before, "then it cools down");
        Step(Game.ForkCd, fcur, false, false);
        g.Enemies.Clear();
        var lone = Dummy(from + new V2(260f, 0f));
        Step(0.4f, fcur, false, true);
        Check(Math.Abs(took(lone) - 3f * bolt) < bolt * 0.15f, "a lone enemy takes all three bolts (" + took(lone).ToString("0.#") + ")");

        // Static Field tags 25% faster; Barrage adds two tags a stack.
        me.Stats.ArcChargeMult *= 0.75f;
        Check(Math.Abs(g.ArcTagGap(me) / gap - 0.75f) < 0.01f, "Static Field tags 25% faster");
        me.Stats.ArcChargeMult /= 0.75f;
        me.Stats.Fan += 1;
        Check(Game.ArcTagMax(me) == Game.ArcMaxTags + 2, "Barrage adds two tags a stack");
        me.Stats.Fan -= 1;
        host.Leave(""); client.Leave("");
    }
}

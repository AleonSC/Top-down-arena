using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Summoner's fourth weapon, the Hive Censer (HiveCenser.cs).
static partial class Program
{
    /// Holding Fire pours insects from the cursor (no farther than 420 px from you) that settle into swarms (up to four);
    /// a swarm wraps the nearest enemy within reach (damage scaled by its size, a slow) and leaves one far off alone; with no
    /// enemy near it shells a teammate in chitin, which soaks a hit before health, wears off and is topped up near a swarm;
    /// insects die after 9 s and you keep 900 at most; the other screen sees the swarms, the pour and the chitin; Alt (Hive
    /// Call) sends every swarm to the cursor, with a 6 s cooldown.
    static void HiveCenserTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Hive", Role.Healer, 3, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.HiveCenser && Defs.WeaponCount(Role.Healer, Kit.Summoner) == 4 && Defs.Weapon(Role.Healer, Kit.Summoner, 3).Id == WeaponId.HiveCenser,
            "the Healer Summoner's fourth key is the Hive Censer (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f), mateAt = new V2(500f, 750f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action Clear = () => { queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); };
        int calls = 0;
        var counted = new HashSet<Effect>();
        Action<float, V2, bool, bool> Step = (secs, aim, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; mate.Pos = mateAt; client.Game.Players[2].Pos = mateAt; queue.Clear();
                foreach (var x in s)
                {
                    x.Poll();
                    var input = x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput { AimAt = mateAt + new V2(300f, 0f) };
                    x.Game.FixedStep(dt, input);
                }
                foreach (var fx in g.Effects)
                    if (counted.Add(fx) && fx.Kind == FxKind.HiveCall)
                        calls++;
            }
        };
        Func<int> MySwarms = () => g.Swarms.Count(sw => sw.Owner == 1);
        Clear();
        Step(0.3f, from + new V2(300f, 0f), false, false);

        // pouring: a swarm gathers where you pour, no farther than the reach; the other screen sees it
        V2 spot = from + new V2(200f, -100f);
        Step(1.2f, spot, true, false);
        Check(me.HiveOn && client.Game.Players[1].HiveOn, "holding Fire pours (seen on the other screen)");
        Check(MySwarms() == 1 && g.Swarms[0].Pos.DistanceTo(spot) < 60f, "the insects settle into one swarm where you pour (" + MySwarms() + ", " + (g.Swarms.Count > 0 ? g.Swarms[0].Pos.DistanceTo(spot).ToString("0") : "-") + " px off)");
        float n1 = g.HiveInsects(1);
        Check(Math.Abs(n1 - Game.HivePour * 1.2f) < Game.HivePour * 0.15f, "it pours " + Game.HivePour + " a second (" + n1.ToString("0") + " in 1.2 s)");
        Check(client.Game.Swarms.Count == 1 && Math.Abs(client.Game.Swarms[0].N - g.Swarms[0].N) < 20f, "the other screen has the swarm (" + client.Game.Swarms.Count + ")");
        Check(me.HivePour.DistanceTo(spot) < 1f, "the pour point is the cursor");
        Step(0.1f, from + new V2(900f, 0f), true, false);
        Check(Math.Abs(me.HivePour.DistanceTo(from) - Game.HiveReach) < 1f, "a far cursor pours at the reach (" + me.HivePour.DistanceTo(from).ToString("0") + " px)");
        Step(0.2f, spot, false, false);
        Check(!me.HiveOn, "letting go stops the pour");

        // swarms: up to four
        foreach (var at in new[] { new V2(-300f, -250f), new V2(300f, -250f), new V2(-300f, 250f), new V2(300f, 250f), new V2(0f, -380f) })
            Step(0.5f, from + at, true, false);
        Step(1f, from, false, false);
        Check(MySwarms() <= Game.HiveSwarmCap && MySwarms() >= 3, "no more than " + Game.HiveSwarmCap + " swarms (" + MySwarms() + ")");

        // insects die after 9 s; chitin wears off with them
        Step(Game.HiveLife + 0.5f, from, false, false);
        Check(MySwarms() == 0 && g.HiveInsects(1) < 0.5f && client.Game.Swarms.Count == 0, "insects live " + Game.HiveLife + " s: the swarms are gone (" + MySwarms() + ")");

        // on an enemy: it wraps it, hurts it (a full swarm: the weapon's damage a second) and slows it; one far off is left alone
        Clear();
        var foe = Dummy(from + new V2(250f, 0f));
        var far = Dummy(from + new V2(-450f, -300f));
        Step(1.0f, from + new V2(150f, 0f), true, false);
        Step(1.5f, from + new V2(150f, 0f), false, false);
        var sw0 = g.Swarms.FirstOrDefault(sw => sw.Owner == 1);
        Check(sw0 != null && sw0.Kind == Game.HiveFoe && sw0.Wrapping && sw0.TargetId == foe.Id, "the swarm flies to the enemy and wraps it");
        Check(foe.Hp < 1e6f && far.Hp >= 1e6f, "it hurts the enemy it wraps, not one far off (" + (1e6f - foe.Hp).ToString("0.0") + ")");
        Check(foe.SlowT > 0f && Math.Abs(foe.SlowMult - (1f - Game.HiveSlow)) < 0.01f, "it slows it " + (Game.HiveSlow * 100f) + "% (" + foe.SlowMult.ToString("0.00") + ")");
        Check(client.Game.Swarms.Any(x => x.Wrapping && x.TargetId == foe.Id), "the other screen sees it wrapped");
        float hp0 = foe.Hp, size = sw0 != null ? sw0.N : 0f;
        Step(1f, from + new V2(150f, 0f), false, false);
        float took = hp0 - foe.Hp, want = me.Weapon.Damage * me.Stats.DmgMult * size / Game.HiveFull;
        Check(want > 0f && Math.Abs(took - want) < want * 0.12f, "damage scales with its size: " + took.ToString("0.0") + " a second from " + size.ToString("0") + " insects (want about " + want.ToString("0.0") + ")");
        Step(Game.HiveLife, from, false, false);

        // chitin: a teammate near a swarm (no enemies about) is shelled; it soaks a hit, the other screen sees it, it wears off
        Clear();
        Step(1.0f, mateAt + new V2(30f, 0f), true, false);
        Step(1.5f, mateAt + new V2(30f, 0f), false, false);
        Check(mate.ChitinHp > 10f && client.Game.Players[2].ChitinHp > 5f, "a teammate by a swarm grows chitin (" + mate.ChitinHp.ToString("0.0") + ", seen " + client.Game.Players[2].ChitinHp.ToString("0.0") + ")");
        Check(g.Swarms.Any(x => x.Owner == 1 && x.Kind == Game.HiveAlly && x.TargetId == mate.Id), "with no enemy near, the swarm goes to the teammate");
        Step(3f, mateAt + new V2(30f, 0f), false, false);
        Check(mate.ChitinHp <= Game.ChitinMax + 0.01f && mate.ChitinHp > Game.ChitinMax * 0.6f, "chitin tops out at " + Game.ChitinMax + " (" + mate.ChitinHp.ToString("0.0") + ")");
        g.DevSetGod(2, false);
        float chit = mate.ChitinHp, hp = mate.Hp;
        mate.InvulnT = 0f;
        damage.Invoke(g, new object[] { mate, 5f, "Test" });
        Check(mate.ChitinHp < chit && Math.Abs(mate.Hp - hp) < 0.01f, "chitin takes a hit before health (" + chit.ToString("0.0") + " -> " + mate.ChitinHp.ToString("0.0") + ", health " + hp + " -> " + mate.Hp + ")");
        g.DevSetGod(2, true);
        Step(Game.HiveLife + 1f, from, false, false);
        Check(mate.ChitinHp < Game.ChitinMax - Game.ChitinWear * 2f, "away from the swarms it wears off (" + mate.ChitinHp.ToString("0.0") + ")");

        // you keep 900 insects at most
        Clear();
        Step(Game.HiveLife - 1f, from + new V2(200f, 0f), true, false);
        float kept = g.HiveInsects(1);
        Check(kept <= Game.HiveMaxInsects + 1f && kept > Game.HiveMaxInsects - 40f, "you keep " + Game.HiveMaxInsects + " insects at most (" + kept.ToString("0") + ")");
        Step(0.5f, from, false, false);

        // Hive Call: every swarm rushes to the cursor; 6 s cooldown
        V2 callAt = from + new V2(-350f, 200f);
        var start = g.Swarms.Where(x => x.Owner == 1).ToDictionary(x => x.Id, x => x.Pos.DistanceTo(callAt));
        Step(0.05f, callAt, false, true);
        Step(1.2f, callAt, false, false);
        bool rushed = start.Count > 0 && g.Swarms.Where(x => x.Owner == 1).All(x => !start.ContainsKey(x.Id) || x.Pos.DistanceTo(callAt) < Math.Max(60f, start[x.Id] - 250f));
        Check(calls == 1 && rushed, "Alt sends every swarm rushing to the cursor (" + string.Join(", ", g.Swarms.Where(x => x.Owner == 1 && start.ContainsKey(x.Id)).Select(x => start[x.Id].ToString("0") + " -> " + x.Pos.DistanceTo(callAt).ToString("0"))) + " px, " + calls + " call)");
        Step(1.5f, from + new V2(300f, 0f), false, true);
        Check(calls == 1, "Hive Call has a cooldown (" + calls + " calls)");
        Step(Game.HiveCallCd, from, false, false);
        Step(0.1f, from + new V2(300f, 0f), false, true);
        Check(calls == 2, "after " + Game.HiveCallCd + " s it calls again (" + calls + ")");


    }
}

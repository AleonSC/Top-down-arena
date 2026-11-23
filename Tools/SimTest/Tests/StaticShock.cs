using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Elementalist's fourth weapon, Static Shock (StaticShock.cs).
static partial class Program
{
    /// Holding Fire wires up to six enemies in the cone into a circuit and shocks them, harder the longer they stay; enemies
    /// outside the cone are left alone; a teammate in the cone gets Overcharge (faster moving, firing and shots, 10 s), which
    /// the other screen sees; Alt rolls the Arc Cannon through enemies and teammates and bursts at the cursor (damage, stun,
    /// Overcharge), with a 5 s cooldown.
    static void StaticShockTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Sparks", Role.Healer, 3, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var mate = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.StaticShock && Defs.WeaponCount(Role.Healer, Kit.Elementalist) == 4 && Defs.Weapon(Role.Healer, Kit.Elementalist, 3).Id == WeaponId.StaticShock,
            "the Healer Elementalist's fourth key is Static Shock (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f), mateAt = new V2(400f, 650f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action Clear = () => { queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); };
        int balls = 0, bursts = 0;
        var counted = new HashSet<Effect>();
        var seen = new Dictionary<int, float>();  // the teammate's shots: id -> speed
        Action<float, V2, bool, bool, bool> Step = (secs, aim, fire, alt, mateFire) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; mate.Pos = mateAt; client.Game.Players[2].Pos = mateAt; queue.Clear();
                foreach (var x in s)
                {
                    x.Poll();
                    var input = x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 }
                        : new PlayerInput { AimAt = mateAt + new V2(300f, 0f), Fire = mateFire };
                    x.Game.FixedStep(dt, input);
                }
                foreach (var bl in g.Bullets.Values)
                    if (bl.Owner == 2 && !seen.ContainsKey(bl.Id))
                        seen[bl.Id] = bl.Vel.Length;
                foreach (var fx in g.Effects)
                    if (counted.Add(fx))
                    {
                        if (fx.Kind == FxKind.ArcBall) balls++;
                        if (fx.Kind == FxKind.ArcBurst) bursts++;
                    }
            }
        };
        Clear();
        Step(0.3f, from + new V2(300f, 0f), false, false, false);

        // the cone: enemies in it are shocked, one outside isn't, and the cone shows on the other screen
        var a = Dummy(from + new V2(90f, 0f));
        var b = Dummy(from + new V2(160f, 30f));
        var c = Dummy(from + new V2(150f, -40f));
        var behind = Dummy(from + new V2(-120f, 0f));
        var far = Dummy(from + new V2(Game.StaticRange + 90f, 0f));
        Step(0.5f, from + new V2(300f, 0f), true, false, false);
        float early = 1e6f - a.Hp;
        Check(a.Hp < 1e6f && b.Hp < 1e6f && c.Hp < 1e6f, "every enemy in the cone is shocked (" + (1e6f - a.Hp) + ", " + (1e6f - b.Hp) + ", " + (1e6f - c.Hp) + ")");
        Check(behind.Hp >= 1e6f && far.Hp >= 1e6f, "an enemy behind you or past the cone's reach isn't touched");
        Check(me.StaticOn && client.Game.Players[1].StaticOn, "the other screen sees the cone out");
        // the shock grows the longer an enemy stays in
        Step(3.6f, from + new V2(300f, 0f), true, false, false);
        float before = 1e6f - a.Hp;
        Step(0.5f, from + new V2(300f, 0f), true, false, false);
        float late = 1e6f - a.Hp - before;
        float full = me.Weapon.Damage * me.Stats.DmgMult;
        Check(late > early * 2.8f, "the shock grows: " + early.ToString("0.0") + " in its first half second, " + late.ToString("0.0") + " after 4 s");
        Check(Math.Abs(late - full * 0.5f) < full * 0.05f, "full shock is the weapon's damage a second (" + late.ToString("0.0") + " in 0.5 s, " + full + "/s)");
        Step(0.2f, from + new V2(300f, 0f), false, false, false);
        Check(!me.StaticOn, "letting go puts the cone away");
        Step(0.5f, from + new V2(300f, 0f), false, false, false);
        float hp0 = a.Hp;
        Step(0.5f, from + new V2(300f, 0f), true, false, false);
        Check(hp0 - a.Hp < late * 0.75f, "out of the cone a while, the shock starts over (" + (hp0 - a.Hp).ToString("0.0") + ")");

        // at most six in the circuit
        Clear();
        var pack = new List<Enemy>();
        for (int i = 0; i < 8; i++)
            pack.Add(Dummy(from + V2.FromAngle((i - 3.5f) * 0.1f) * (70f + 14f * i)));
        Step(0.5f, from + new V2(300f, 0f), true, false, false);
        int shocked = pack.Count(e => e.Hp < 1e6f);
        Check(shocked == Game.StaticLinks, "the circuit wires " + Game.StaticLinks + " enemies at most (" + shocked + " of 8 in the cone)");
        var into = new List<Enemy>();
        var fromIdx = new List<int>();
        Game.StaticCircuit(Game.StaticHand(me, me.Pos), me.Aim, Game.StaticRangeOf(me), g.Enemies.Values, e => e.Pos, e => e.Hp > 0f, Game.StaticLinks, into, fromIdx);
        Check(into.Count == 6 && fromIdx[0] == -1 && fromIdx.Skip(1).All(k => k >= 0), "the nearest wires to your hand, each of the rest to the circuit");
        Step(0.2f, from + new V2(300f, 0f), false, false, false);

        // Overcharge: a teammate in the cone
        Clear();
        float walk = mate.Speed;
        Step(0.4f, mateAt, true, false, false);
        Check(mate.OverchargeT > Game.OverchargeTime - 0.5f, "a teammate in the cone gets Overcharge (" + mate.OverchargeT.ToString("0.0") + " s)");
        Check(client.Game.Players[2].OverchargeT > 0f, "the other screen sees the Overcharge");
        Check(Math.Abs(mate.Speed / walk - Game.OverchargeMove) < 0.01f, "Overcharge: 25% faster moving (" + (mate.Speed / walk).ToString("0.00") + "x)");
        Check(me.OverchargeT <= 0f, "you don't charge yourself");
        Check(g.Effects.Any(fx => fx.Kind == FxKind.Overcharge), "it pops over them");
        Step(0.2f, mateAt, false, false, false);
        // its shots: faster and more often
        g.Bullets.Clear(); seen.Clear();
        Step(3f, from + new V2(300f, 0f), false, false, true);
        int boosted = seen.Count;
        float boostedV = seen.Count > 0 ? seen.Values.Average() : 0f;
        mate.OverchargeT = 0.3f;
        Step(0.5f, from + new V2(300f, 0f), false, false, false);
        Check(mate.OverchargeT <= 0f && client.Game.Players[2].OverchargeT <= 0f, "it runs out, on both screens");
        Step(1f, from + new V2(300f, 0f), false, false, false);
        g.Bullets.Clear(); seen.Clear();
        Step(3f, from + new V2(300f, 0f), false, false, true);
        float plainV = seen.Count > 0 ? seen.Values.Average() : 0f;
        Check(plainV > 0f && Math.Abs(boostedV / plainV - Game.OverchargeShot) < 0.02f, "Overcharge: shots fly 35% faster (" + boostedV.ToString("0") + " vs " + plainV.ToString("0") + ")");
        Check(boosted > seen.Count, "Overcharge: more shots a second (" + boosted + " vs " + seen.Count + ")");

        // room turrets: the cone shocks them, the burst hits them
        Clear();
        V2 post = from + new V2(130f, 0f);
        Check(g.DevPlaceTrap(TrapKind.Turret, post, 0f), "a room turret stands in front of you");
        Step(1.5f, from + new V2(300f, 0f), true, false, false);
        Step(0.5f, from + new V2(300f, 0f), false, false, false);
        float frac1 = g.TurretHpFrac(post);
        Check(frac1 < 1f, "the cone shocks a room turret (" + (frac1 * 100f).ToString("0") + "% left)");
        g.DevRemoveTrapAt(post);
        V2 post2 = from + new V2(300f, 60f);
        g.DevPlaceTrap(TrapKind.Turret, post2, 0f);
        Step(5.2f, from + new V2(300f, 0f), false, false, false);
        Step(0.05f, from + new V2(300f, 0f), false, true, false);
        Step(1.2f, from + new V2(300f, 0f), false, false, false);
        Check(g.TurretHpFrac(post2) < 1f, "the Arc Cannon's burst hits a room turret (" + (g.TurretHpFrac(post2) * 100f).ToString("0") + "% left)");
        g.DevRemoveTrapAt(post2);
        Step(5.2f, from + new V2(300f, 0f), false, false, false);

        // the Arc Cannon: through a teammate and an enemy on the way, bursting at the cursor
        Clear();
        mateAt = from + new V2(150f, 0f);
        var onWay = Dummy(from + new V2(260f, -10f));
        var atEnd = Dummy(from + new V2(400f, 75f));
        var outside = Dummy(from + new V2(420f, 300f));
        balls = bursts = 0;
        Step(0.2f, from + new V2(420f, 0f), false, true, false);
        Check(balls == 1, "Alt rolls one ball out (" + balls + ")");
        Step(1.6f, from + new V2(420f, 0f), false, false, false);
        float pass = me.Weapon.Damage * me.Stats.DmgMult * Game.CannonPassDmg, burst = me.Weapon.Damage * me.Stats.DmgMult * Game.CannonBurstDmg;
        Check(bursts == 1, "it bursts at the end (" + bursts + ")");
        Check(Math.Abs((1e6f - onWay.Hp) - pass) < 0.5f, "an enemy on its way takes " + pass + " as it passes (" + (1e6f - onWay.Hp) + ")");
        Check(Math.Abs((1e6f - atEnd.Hp) - burst) < 0.5f, "an enemy in the burst takes " + burst + " (" + (1e6f - atEnd.Hp) + ")");
        Check(outside.Hp >= 1e6f, "one outside the burst isn't touched");
        Check(mate.OverchargeT > Game.OverchargeTime - 2f, "a teammate it rolls through gets Overcharge (" + mate.OverchargeT.ToString("0.0") + ")");
        Check(client.Game.Effects.Any(fx => fx.Kind == FxKind.ArcBurst) || bursts == 1, "the other screen gets the burst");
        // the stun
        Clear();
        var stunMe = Dummy(from + new V2(300f, 0f));
        Step(5.2f, from + new V2(300f, 0f), false, false, false);
        Step(0.05f, from + new V2(300f, 0f), false, true, false);
        Step(1.0f, from + new V2(300f, 0f), false, false, false);
        Check(stunMe.Hp < 1e6f && stunMe.StunT > 0f, "the burst lands at the cursor and stuns (" + stunMe.StunT.ToString("0.00") + " s left)");
        // the cooldown
        balls = 0;
        Step(0.2f, from + new V2(300f, 0f), false, true, false);
        Check(balls == 0 && me.ArcCd > 0f, "a second press inside the 5 s cooldown does nothing (" + balls + ")");
        Step(5.2f, from + new V2(300f, 0f), false, false, false);
        Step(0.2f, from + new V2(300f, 0f), false, true, false);
        Check(balls == 1, "after the cooldown it fires again (" + balls + ")");

    }
}

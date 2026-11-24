using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Charged (Charged.cs): lightning charges what it hits, charged enemies conduct like wet ones, and a wet, charged enemy
// shorts out (constant damage and an arc to the nearest three every 2.5 s) until the charge wears off.
static partial class Program
{
    static void ChargedTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var volley = typeof(Game).GetMethod("FireVolley", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Caster", Role.Dps, 1, 2, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Check(me.Weapon.Id == WeaponId.ArcLightning, "the Elementalist DPS holds Arc Lightning");
        g.Enemies.Clear(); g.Bullets.Clear();
        var spots = new Dictionary<Enemy, V2>();
        Func<V2, Enemy> pin = at => { var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; spots[e] = at; return e; };
        Action<float> step = secs => { for (int f = 0; f < secs / dt; f++) { foreach (var kv in spots) kv.Key.Pos = kv.Value; me.Pos = new V2(200f, 450f); me.Hp = me.MaxHp; host.Poll(); client.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(100f, 450f) }); client.Game.FixedStep(dt, new PlayerInput()); } };
        Action<Enemy> zap = target => { me.Aim = MathX.Pi; me.ArcTags.Clear(); me.ArcTags.Add(target.Id); volley.Invoke(g, new object[] { me, me.Aim, 0 }); step(dt); };
        me.Pos = new V2(200f, 450f);

        // A lightning hit charges what it hits.
        var first = pin(new V2(110f, 450f)); var second = pin(new V2(110f, 300f)); var bystander = pin(new V2(1200f, 800f)); var wet = pin(new V2(1300f, 200f));
        wet.WetT = 30f;
        zap(first);
        Console.WriteLine("  charged: first {0:0.0} s, bystander {1:0.0} s, wet {2:0.0} s", first.ChargedT, bystander.ChargedT, wet.ChargedT);
        Check(first.ChargedT > Game.ChargedTime - 0.2f && first.ChargedBy == me.Id, "an Arc Lightning hit charges the enemy for 4 s");
        Check(bystander.ChargedT == 0f && wet.ChargedT == 0f, "the arcs it sends to wet enemies don't charge them");
        step(0.3f);
        Check(client.Game.Enemies.ContainsKey(first.Id) && client.Game.Enemies[first.Id].Charged && !client.Game.Enemies[bystander.Id].Charged, "the other screen sees who is charged");

        // Charged alone doesn't conduct; wet does, charged or not.
        step(0.5f);
        wet.ChargedT = Game.ChargedTime; wet.ChargedBy = me.Id; wet.ChargePulseT = 99f;  // (no shorting arc in the way)
        float f0 = first.Hp, s0 = second.Hp, b0 = bystander.Hp, w0 = wet.Hp;
        zap(second);
        float hit = s0 - second.Hp, arc = f0 - first.Hp, wetArc = w0 - wet.Hp;
        Console.WriteLine("  hit on second {0:0.0}: charged first takes {1:0.0}, wet and charged {2:0.0}, dry bystander {3:0.0}", hit, arc, wetArc, b0 - bystander.Hp);
        Check(hit > 5f && arc == 0f, "a charged but dry enemy takes no arc from lightning elsewhere");
        Check(Math.Abs(wetArc - hit * Game.WetArcShare) < 1f, "a wet, charged enemy still takes the 40% arc from lightning elsewhere");
        Check(bystander.Hp == b0, "a dry, uncharged enemy takes no arc");

        // Wet and charged: it shorts out.
        g.Enemies.Clear(); spots.Clear();
        var hub = pin(new V2(800f, 450f));
        var n1 = pin(new V2(900f, 450f)); var n2 = pin(new V2(800f, 580f)); var n3 = pin(new V2(680f, 450f)); var n4 = pin(new V2(800f, 250f)); var away = pin(new V2(1300f, 450f));
        hub.WetT = 30f; hub.ChargedT = Game.ChargedTime; hub.ChargedBy = me.Id; hub.ChargePulseT = Game.ChargeArcEvery;
        float h0 = hub.Hp;
        step(1f);
        float perSec = h0 - hub.Hp, want = Game.ChargeDps + Game.ChargeDpsWave * g.Wave;
        Console.WriteLine("  shorting: {0:0.0} a second (want {1:0.0})", perSec, want);
        Check(Math.Abs(perSec - want) < want * 0.25f, "a wet, charged enemy takes constant damage");
        Check(n1.Hp == n1.MaxHp && n2.Hp == n2.MaxHp, "no arc in its first 2.5 s");
        step(1.7f);
        float arcDmg = Game.ChargeArcDmg + Game.ChargeArcDmgWave * g.Wave;
        var lost = new[] { n1, n2, n3, n4, away }.Select(e => e.MaxHp - e.Hp).ToArray();
        Console.WriteLine("  arc at 2.5 s: {0} (want {1:0.0} to the nearest three)", string.Join(" / ", lost.Select(v => v.ToString("0.0"))), arcDmg);
        Check(lost.Take(3).All(v => Math.Abs(v - arcDmg) < arcDmg * 0.25f), "every 2.5 s an arc hits the three nearest enemies");
        Check(lost[3] == 0f && lost[4] == 0f, "the fourth-nearest and anything past 220 px take nothing");
        Check(n1.ChargedT == 0f, "its arcs don't charge what they hit");

        // The charge wears off, and the shorting with it.
        step(Game.ChargedTime);
        float stop = hub.Hp, n1Stop = n1.Hp;
        step(3f);
        Check(hub.ChargedT == 0f && hub.Hp == stop && n1.Hp == n1Stop, "after 4 s the charge is gone: no more damage or arcs, though it's still wet");

        // Dry and charged does nothing on its own.
        g.Enemies.Clear(); spots.Clear();
        var lone = pin(new V2(800f, 450f)); var nb = pin(new V2(900f, 450f));
        lone.ChargedT = Game.ChargedTime; lone.ChargedBy = me.Id;
        step(3f);
        Check(lone.Hp == lone.MaxHp && nb.Hp == nb.MaxHp, "charged but dry: no damage and no arcs of its own");
        host.Leave(""); client.Leave("");
    }
}

using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Soldier DPS's Railgun (RailBeam.cs): hold to charge a ricochet beam, Alt dumps the heat as a cone of fire.
static partial class Program
{
    /// A tap is weak and thin, a full charge five times harder and wider and passes everything, a perfect release harder
    /// still; the beam bounces off the arena's edge into what's behind you; every shot heats the gun, the other screen sees
    /// the heat; Heat Dump burns and shoves what's in its cone (not what's behind), empties the meter, and does nothing cold.
    static void RailgunTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Rail", Role.Dps, 1, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.Railgun && Game.ChargeWeapon(WeaponId.Railgun), "the Soldier DPS's second weapon is the Railgun, held to charge (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = (g.ArenaMin + g.ArenaMax) * 0.5f;
        var dummies = new List<Enemy>();
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            dummies.Add(d);
            return d;
        };
        Action Clear = () => { foreach (var d in dummies) g.Enemies.Remove(d.Id); dummies.Clear(); };
        Action<float, V2, bool, bool> Step = (secs, aim, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> Lost = e => e.MaxHp - e.Hp;
        // Hold fire for `hold` s at `aim`, let go, and let the shot land (and the recovery pass).
        Action<float, V2> Shoot = (hold, aim) => { Step(hold, aim, true, false); Step(0.05f, aim, false, false); };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        V2 right = from + new V2(200f, 0f);

        // a tap and a full charge
        var a = Dummy(right);
        float heat0 = me.RailHeatF;
        Shoot(0.05f, right);
        float tap = Lost(a), tapHeat = me.RailHeatF - heat0;
        Step(0.5f, right, false, false);
        Check(Math.Abs(Lost(a) - tap) < 0.01f, "a tap leaves no charged path");
        a.Hp = a.MaxHp;
        Shoot(1.6f, right);
        float full = Lost(a), fullHeat = me.RailHeatF - heat0 - tapHeat;
        Console.WriteLine("  railgun: tap {0:0.0} (+{1:0.0} heat), full {2:0.0} (+{3:0.0} heat)", tap, tapHeat, full, fullHeat);
        Check(tap > 0f && full / tap > 4f && full / tap < 6.5f, "a full charge hits about five times harder than a tap (" + tap.ToString("0.0") + " -> " + full.ToString("0.0") + ")");
        Check(Math.Abs(tapHeat - Game.RailHeatTap) < 0.5f && Math.Abs(fullHeat - Game.RailHeatFull) < 0.5f, "a tap heats the gun by 5, a full charge by 20");
        Step(0.5f, right, false, false);
        Check(Lost(a) > full + 5f, "a full charge leaves its path charged, hurting what stands in it");
        Step(1.5f, right, false, false);

        // a perfect release
        a.Hp = a.MaxHp;
        Shoot(g.BowFull(me) + 0.08f, right);
        float perfect = Lost(a);
        Check(perfect / full > 1.15f && perfect / full < 1.35f, "letting go just as it fills is a perfect shot, 25% harder (" + perfect.ToString("0.0") + ")");
        Step(2.2f, right, false, false);
        Clear();

        // pierce: a tap stops at the second enemy, a full charge passes them all
        var line = Enumerable.Range(0, 4).Select(i => Dummy(from + new V2(120f + 60f * i, 0f))).ToList();
        Shoot(0.05f, right);
        int tapHits = line.Count(e => Lost(e) > 0f);
        Step(0.5f, right, false, false);
        foreach (var e in line) e.Hp = e.MaxHp;
        Shoot(1.6f, right);
        int fullHits = line.Count(e => Lost(e) > 0f);
        Check(tapHits == 2 && fullHits == 4, "a tap passes 2 enemies, a full charge all of them (" + tapHits + ", " + fullHits + ")");
        Step(2.2f, right, false, false);
        Clear();

        // the ricochet: straight at the edge, back past you into what's behind
        var behind = Dummy(from - new V2(200f, 0f));
        int bounceHits = g.RailBounceHits;
        Shoot(0.05f, from + new V2(10f, 0f));
        Check(Lost(behind) > 0f && g.RailBounceHits > bounceHits, "the beam bounces off the arena's edge and hits the enemy behind you");
        Step(0.5f, right, false, false);
        Clear();

        // the other screen sees the heat
        Step(0.2f, right, false, false);
        Check(me.RailHeat > 0 && client.Game.Players[1].RailHeat == me.RailHeat, "the other screen sees the heat (" + me.RailHeat + ")");

        // Heat Dump: cold, nothing
        me.RailHeatF = 5f; me.RailHeat = 5;
        int dumps = g.HeatDumps;
        Step(0.3f, right, false, true);
        Check(g.HeatDumps == dumps && me.RailHeat == 5, "with under 10 heat, Alt does nothing");

        // full: the cone burns, hits and shoves; behind and to the side are spared; the meter empties
        Step(1f, right, false, false);
        me.RailHeatF = 100f; me.RailHeat = 100;
        var front = Dummy(from + new V2(200f, 0f));
        var back = Dummy(from - new V2(200f, 0f));
        var side = Dummy(from + V2.FromAngle(0.9f) * 200f);
        var far = Dummy(from + new V2(Game.DumpReach(100f) + 80f, 0f));
        V2 frontAt = front.Pos;
        Step(dt * 3f, right, false, true);
        float hitFront = Lost(front);
        Console.WriteLine("  heat dump at 100: front {0:0.0} (burning {1}), pushed {2:0} px", hitFront, front.BurnT > 0f, front.Pos.DistanceTo(frontAt));
        Check(g.HeatDumps == dumps + 1 && me.RailHeat == 0, "Alt with a full meter dumps it all");
        Check(hitFront >= Game.DumpHit(100f) * 0.9f && front.BurnT > 2f, "what's in the cone takes the hit and burns");
        Check(Lost(back) == 0f && Lost(side) == 0f && Lost(far) == 0f, "behind you, outside the 70 degree cone and past its reach are spared");
        Step(0.5f, right, false, false);
        Check(front.Pos.DistanceTo(frontAt) > 100f, "the blast shoves what it hits");
        Step(3f, right, false, false);
        Check(Lost(front) > hitFront + Game.DumpBurn(100f) * 2f, "the burn keeps hurting for 3 s");
        Clear();
        host.Leave(""); client.Leave("");
    }
}

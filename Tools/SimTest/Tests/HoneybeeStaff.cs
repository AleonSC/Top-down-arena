using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Summoner DPS's fourth weapon, the Honeybee Staff (HoneybeeStaff.cs).
static partial class Program
{
    /// Fire hatches a bee at the cursor (capped at HatchReach); it buzzes, then stings the nearest enemy once and dies; with nothing
    /// to sting it fizzles; three stings in a row on one enemy swarm it; held Alt sprays honey that lands as puddles that slow and drains
    /// the meter, which refills after; an enemy dying in your honey bursts into free bees; the other screen sees the bees, the spray and
    /// the meter; Bigger Hive adds a harvest bee and wider puddles.
    static void HoneybeeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Keeper", Role.Dps, 3, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.HoneybeeStaff && Defs.WeaponCount(Role.Dps, Kit.Summoner) == 4, "the Summoner DPS's fourth key is the Honeybee Staff (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<V2, float, Enemy> Dummy = (at, hp) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = hp; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt } : new PlayerInput()); }
            }
        };
        Func<List<Minion>> bees = () => g.Minions.Values.Where(m => m.Kind == MinionKind.Bee && m.Owner == 1).ToList();
        Action Clear = () => { queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Zones.Clear(); foreach (var m in bees()) g.Minions.Remove(m.Id); Dummy(from + new V2(-370f, 200f), 1e6f); };  // (an enemy far off keeps the wave going)
        Clear();
        float sting = 22f * me.Stats.DmgMult * me.Stats.MinionDmgMult;

        // a click hatches one bee at the cursor; it waits there, then stings the nearest enemy and is gone
        V2 click = from + new V2(150f, -60f);
        var near = Dummy(click + new V2(90f, 0f), 1e6f);
        var farther = Dummy(click + new V2(-200f, 0f), 1e6f);
        me.FireCd = 0f;
        Step(dt * 2f, click, true, false);
        Check(bees().Count == 1 && bees()[0].Pos.DistanceTo(click) < 2f, "a click hatches one bee at the cursor (" + bees().Count + ")");
        Step(Game.BeeWait - dt * 4f, click, false, false);
        Check(bees().Count == 1 && bees()[0].Pos.DistanceTo(click) < 8f && near.Hp == near.MaxHp, "it buzzes where it hatched for the wait");
        Step(dt * 3f, click, false, false);
        Check(client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Bee), "the other screen sees the bee");
        Step(0.6f, click, false, false);
        Check(Math.Abs(near.MaxHp - near.Hp - sting) < 0.5f && farther.Hp == farther.MaxHp, "it stings the nearest enemy once for 22 (" + (near.MaxHp - near.Hp).ToString("0.#") + ")");
        Check(bees().Count == 0, "and dies");
        Check(g.Effects.Any(e => e.Kind == FxKind.BeeSting && (int)e.Pos2.X == near.Id), "the sting is sent as FxKind.BeeSting");

        // too far: it hatches at HatchReach toward the cursor
        Clear(); me.FireCd = 0f;
        Step(dt * 2f, from + new V2(900f, 0f), true, false);
        Check(bees().Count == 1 && Math.Abs(bees()[0].Pos.DistanceTo(from) - Game.HatchReach) < 3f, "a click past reach hatches the bee at " + Game.HatchReach + " px (" + bees().Count + ", " + (bees().Count > 0 ? bees()[0].Pos.DistanceTo(from) : 0f) + ")");

        // nothing to sting: it hangs about and fizzles after BeeLife
        Step(Game.BeeLife - 0.3f, from, false, false);
        Check(bees().Count == 1, "with nothing near it hangs about");
        Step(0.5f, from, false, false);
        Check(bees().Count == 0, "and fizzles after " + Game.BeeLife + " s");

        // the cap: holding fire never has more than BeeCap out
        Clear();
        Step(3.5f, from + new V2(0f, -200f), true, false);
        Check(bees().Count == Game.BeeCap, "holding fire stops at " + Game.BeeCap + " bees out (" + bees().Count + ")");

        // Swarm: from the third sting in a row on one enemy, 40% harder
        Clear();
        var target = Dummy(from + new V2(0f, -150f), 1e6f);
        float before = target.Hp;
        Step(3f, target.Pos + new V2(0f, 40f), true, false);
        int stings = target.BeeChain;  // (every sting, in one chain)
        float took = before - target.Hp;
        Check(target.BeeChain >= Game.SwarmN && took > stings * sting * 1.15f, "steady stings swarm it: " + stings + " stings did " + took.ToString("0") + " (more than " + (stings * sting).ToString("0") + ")");
        Check(g.Effects.Any(e => e.Kind == FxKind.BeeSting && e.Pos2.Y == 1f), "the first swarming sting is flagged for the look");

        // the spray: held Alt drains the meter and lands puddles round the cursor that slow
        Clear(); me.HoneyFill = Game.HoneyMax;
        V2 aimAt = from + new V2(0f, -130f);
        Step(1f, aimAt, false, true);
        Check(me.HoneyOn && me.HoneyFill < Game.HoneyMax - 30f, "holding Alt sprays (meter " + me.HoneyFill.ToString("0") + ")");
        Check(client.Game.Players[1].HoneyOn && Math.Abs(client.Game.Players[1].HoneyFill - me.HoneyFill) < 6f, "the other screen sees the spray and the meter");
        var puddles = g.Zones.Values.Where(z => z.Kind == ZoneKind.HoneyPuddle && z.Owner == 1).ToList();
        Check(puddles.Count > 0 && puddles.All(z => z.Pos.DistanceTo(from) > 90f && z.Pos.DistanceTo(from) < 200f), "the globs land as puddles 110-160 px out (" + puddles.Count + ")");
        Check(puddles.Count < 12 && puddles.Any(z => z.Radius > Game.PuddleR), "globs that land in a puddle grow it instead of making more");
        var slowed = Dummy(puddles[0].Pos, 1e6f);
        Step(dt * 4f, aimAt, false, true);
        Check(slowed.SlowMult < 0.7f, "an enemy in the honey is slowed (x" + slowed.SlowMult.ToString("0.00") + ")");
        Step(3f, aimAt, false, true);
        Check(!me.HoneyOn && me.HoneyFill <= 0.5f, "the spray stops when the meter runs dry");
        Step(0.4f, aimAt, false, false);
        float low = me.HoneyFill;
        Step(1.5f, aimAt, false, false);
        Check(me.HoneyFill > low + 15f, "let go, it refills (" + low.ToString("0") + " -> " + me.HoneyFill.ToString("0") + ")");
        Step(Game.PuddleTime + 0.2f, aimAt, false, false);
        Check(!g.Zones.Values.Any(z => z.Kind == ZoneKind.HoneyPuddle), "the puddles dry up after " + Game.PuddleTime + " s");

        // harvest: an enemy that dies in your honey bursts into two free bees
        Clear(); me.HoneyFill = Game.HoneyMax;
        Step(0.5f, aimAt, false, true);
        Step(dt * 2f, aimAt, false, false);
        var pool = g.Zones.Values.First(z => z.Kind == ZoneKind.HoneyPuddle);
        var victim = Dummy(pool.Pos, 5f);
        var next = Dummy(pool.Pos + new V2(220f, 0f), 1e6f);
        me.FireCd = 0f;
        Step(dt * 2f, victim.Pos + new V2(0f, 30f), true, false);
        Step(0.8f, victim.Pos, false, false);
        Check(!g.Enemies.ContainsKey(victim.Id), "the bee kills the weak enemy in the honey");
        Check(g.Effects.Any(e => e.Kind == FxKind.HoneyHarvest && (int)e.Pos2.Y == Game.HarvestBees), "it bursts (FxKind.HoneyHarvest)");
        Check(bees().Count == Game.HarvestBees, "into " + Game.HarvestBees + " free bees (" + bees().Count + ")");
        Step(1.2f, victim.Pos, false, false);
        Check(next.Hp < next.MaxHp - sting * 1.5f, "which fly at the next enemy and sting it (" + (next.MaxHp - next.Hp).ToString("0") + ")");

        // going down stops the spray
        me.HoneyFill = Game.HoneyMax;
        Step(0.2f, aimAt, false, true);
        me.Downed = true;
        Step(dt * 2f, aimAt, false, true);
        Check(!me.HoneyOn, "going down stops the spray");
        me.Downed = false; me.Hp = me.MaxHp;
        Step(dt * 2f, aimAt, false, false);

        // Bigger Hive: one more harvest bee, wider puddles
        var hive = Upgrades.All.First(u => u.Name == "Bigger Hive");
        Check(hive.Weapons != null && hive.Weapons.Contains(WeaponId.HoneybeeStaff), "Bigger Hive is a Honeybee Staff card");
        hive.Apply(me.Stats);
        Clear(); me.HoneyFill = Game.HoneyMax;
        Step(0.1f, aimAt, false, true);
        Step(0.5f, aimAt, false, false);
        Check(g.Zones.Values.Any(z => z.Kind == ZoneKind.HoneyPuddle && z.Radius >= Game.PuddleR * 1.2f - 0.1f), "with Bigger Hive the puddles start 20% wider");
        pool = g.Zones.Values.First(z => z.Kind == ZoneKind.HoneyPuddle);
        victim = Dummy(pool.Pos, 5f);
        me.FireCd = 0f;
        Step(dt * 2f, victim.Pos + new V2(0f, 30f), true, false);
        Step(0.8f, victim.Pos, false, false);
        Check(g.Effects.Any(e => e.Kind == FxKind.HoneyHarvest && (int)e.Pos2.Y == Game.HarvestBees + 1), "and a harvest gives " + (Game.HarvestBees + 1) + " bees");
    }
}

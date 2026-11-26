using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Elementalist DPS's fourth weapon, the Stonespear (Stonespear.cs).
static partial class Program
{
    /// Fire slams (wind-up, then damage round you and six spears up), each Fire then throws the risen spear nearest the cursor (it
    /// pierces one), Alt sends the rest at the enemy nearest the cursor (they curve in on it), the next slam waits SsCd after the last,
    /// the other screen sees the spears and the lock, and Tectonic adds a spear a side.
    static void StonespearTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Caster", Role.Dps, 3, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.Stonespear && Defs.WeaponCount(Role.Dps, Kit.Elementalist) == 4, "the Elementalist DPS's fourth key is the Stonespear (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, V2, bool, bool> Step = (secs, cursor, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = cursor, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<int> spears = () => { int n = 0; for (int m = me.SsMask; m != 0; m &= m - 1) n++; return n; };
        Func<int> flying = () => g.Bullets.Values.Count(b => b.Owner == 1 && b.Kind == Defs.StoneSpear);
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // the slam: nothing for the wind-up, then the ring hurts what is near and six spears come up
        var near = Dummy(from + new V2(60f, 0f));
        var outside = Dummy(from + new V2(0f, 260f));
        V2 far = from + new V2(380f, 0f);
        Step(dt * 2f, far, true, false);
        Check(me.SsMask == 0 && me.SsWindT > 0f && near.Hp == near.MaxHp, "the first Fire winds up the slam (nothing yet)");
        Step(Game.SsWind, far, false, false);
        Check(spears() == 6 && me.SsSide == 3, "the slam raises six spears, three a side (" + spears() + ")");
        Check(near.Hp < near.MaxHp && Math.Abs(near.MaxHp - near.Hp - 24f * Game.SsSlamDmg * me.Stats.DmgMult) < 1f, "it hits an enemy 60 px away for 18 (" + (near.MaxHp - near.Hp).ToString("0.#") + ")");
        Check(outside.Hp == outside.MaxHp, "and not one 260 px away");
        Check(g.Effects.Any(e => e.Kind == FxKind.StoneSlam && e.Pos2.X >= 1f), "the impact is sent as FxKind.StoneSlam");
        Step(dt * 3f, far, false, false);
        Check(client.Game.Players[1].SsMask == me.SsMask && client.Game.Players[1].SsSide == 3, "the other screen sees the spears");

        // throws: none until a spear has risen, then the one nearest the cursor, one a click
        g.Enemies.Clear(); g.Bullets.Clear();
        me.SsAge = 0f; me.FireCd = 0f;
        Step(dt * 2f, far, true, false);
        Check(spears() == 6 && flying() == 0, "nothing is thrown while the spears are still rising");
        Step(Game.SsRisen(5) + 0.05f, far, false, false);
        var a = Dummy(from + new V2(200f, 0f)); var b2 = Dummy(from + new V2(260f, 0f)); var c3 = Dummy(from + new V2(330f, 0f));
        Step(dt * 2f, far, true, false);
        Check(spears() == 5 && flying() == 1, "a click throws one spear (" + spears() + " left)");
        var shot = g.Bullets.Values.First(b => b.Owner == 1 && b.Kind == Defs.StoneSpear);
        int thrownSlot = Enumerable.Range(0, 6).First(k => (me.SsMask & (1 << k)) == 0);
        Check(thrownSlot == 0 || thrownSlot == 3, "the spear nearest the cursor goes first, an inner one (slot " + thrownSlot + ")");
        Check(Math.Abs(shot.Vel.Length - 900f * me.Stats.ShotSpeedMult) < 2f && Math.Abs(shot.Dmg - 24f * me.Stats.DmgMult) < 0.5f, "it flies at 900 px/s for 24");
        Step(0.5f, far, false, false);
        Check(a.Hp < a.MaxHp && b2.Hp < b2.MaxHp && c3.Hp == c3.MaxHp, "it passes through the first enemy into the second, and stops there");
        Step(0.15f, far, true, false);
        Check(spears() == 4, "holding Fire keeps throwing, one every 0.18 s (" + spears() + " left)");

        // Alt: the rest go at the enemy nearest the cursor, curving in on it
        g.Enemies.Clear(); g.Bullets.Clear();
        var mark = Dummy(from + new V2(250f, -140f));
        var bystander = Dummy(from + new V2(250f, 160f));
        int left = spears();
        Step(dt * 4f, mark.Pos + new V2(30f, 0f), false, true);
        Check(me.SsLock == mark.Id, "Alt locks on to the enemy nearest the cursor");
        Step(0.4f, mark.Pos, false, false);
        Check(spears() == 0, "every spear left goes in the volley");
        me.FireCd = 0f;
        Step(dt * 2f, mark.Pos, true, false);
        Check(me.SsWindT <= 0f && spears() == 0, "no slam straight after the last spear leaves (" + me.SsCdT.ToString("0.00") + " s to wait)");
        Check(client.Game.Players[1].SsLock == mark.Id, "the other screen sees the lock");
        Step(1.2f, mark.Pos, false, false);
        float took = mark.MaxHp - mark.Hp;
        Check(Math.Abs(took - 24f * Game.SsVolleyMult * me.Stats.DmgMult * (1f + (left - 1) * Game.BrittleMult)) < 2f && bystander.Hp == bystander.MaxHp && mark.BrittleT > 0f,
            "all " + left + " curve in on it, 30 each, the first's cracks making the rest 20% harder (" + took.ToString("0") + "), and nothing else is hit");
        Check(me.SsLock == 0, "the lock goes when the volley is spent");

        // SsCd later, the next Fire slams again
        me.FireCd = 0f;
        Step(dt * 2f, far, true, false);
        Check(me.SsWindT > 0f, "a slam once SsCd has passed");
        Step(Game.SsWind + dt * 2f, far, false, false);
        Check(spears() == 6, "six up again");

        // putting it away (going down) drops the spears
        me.Downed = true;
        Step(dt * 2f, far, false, false);
        Check(me.SsMask == 0, "going down drops the spears");
        me.Downed = false; me.Hp = me.MaxHp;

        // Tectonic: a spear more a side, a wider slam
        var tect = Upgrades.All.First(u => u.Name == "Tectonic");
        Check(tect.Weapons != null && tect.Weapons.Contains(WeaponId.Stonespear), "Tectonic is a Stonespear card");
        tect.Apply(me.Stats);
        g.Enemies.Clear(); g.Bullets.Clear(); me.FireCd = 0f; me.SsCdT = 0f;
        var edge = Dummy(from + new V2(125f, 0f));
        Step(dt * 2f, far, true, false);
        Step(Game.SsWind + dt * 2f, far, false, false);
        Check(spears() == 8 && me.SsSide == 4, "with Tectonic the slam raises eight (" + spears() + ")");
        Check(edge.Hp < edge.MaxHp, "and reaches 20% farther (an enemy 125 px away is hit)");
    }
}

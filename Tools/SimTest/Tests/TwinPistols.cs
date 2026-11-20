using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Soldier's fourth weapon, the Twin Pistols (TwinPistols.cs).
static partial class Program
{
    /// The guns take turns, left then right; hits knock tiny health orbs loose (at most one per enemy every 1.5 s); Alt turns on
    /// Laser Sights: both guns at once, harder, down beams that meet at the cursor, slower walking, a 12 s cooldown; the other
    /// screen sees which gun is next and the lasers.
    static void TwinPistolsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Medic", Role.Healer, 3, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.TwinPistols && Defs.WeaponCount(Role.Healer, Kit.Soldier) == 4 && Defs.Weapon(Role.Healer, Kit.Soldier, 3).Id == WeaponId.TwinPistols,
            "the Healer Soldier's fourth key is the Twin Pistols (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(400f, 450f);
        Func<V2, Enemy> Dummy = at =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        var fresh = new List<Bullet>();
        var seenOrbs = new HashSet<int>();
        Action<float, V2, bool, bool> Step = (secs, aim, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
                fresh.AddRange(g.Bullets.Values.Where(b => b.Owner == 1 && b.Kind == Defs.HealerShot && b.Life0 - b.Life < dt * 1.5f && !fresh.Contains(b)));
                foreach (var o in g.Orbs.Values)
                    if (o.Tiny) seenOrbs.Add(o.Id);
            }
        };
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear(); g.Orbs.Clear();

        // the guns take turns: left, right, left...
        var target = Dummy(from + new V2(220f, 0f));
        Step(1f, target.Pos, true, false);
        Check(fresh.Count >= 5 && fresh.Count <= 7, "about six rounds a second, one at a time (" + fresh.Count + ")");
        var sides = fresh.Select(b => b.Pos.Y < from.Y ? 'L' : 'R').ToArray();
        bool alternates = sides.Length > 1;
        for (int i = 1; i < sides.Length; i++)
            alternates &= sides[i] != sides[i - 1];
        Check(alternates, "each round comes from the other gun (" + new string(sides) + ")");
        Check(fresh.All(b => Math.Abs(b.Dmg - 10f * me.Stats.DmgMult) < 0.01f), "each round hits for 10");
        Check(target.Hp < target.MaxHp, "the rounds hit");
        Step(0.2f, target.Pos, false, false);
        Check(client.Game.Players[1].TpSide == me.TpSide, "the other screen knows which gun is next");

        // hits knock tiny health orbs loose, at most one per enemy every 1.5 s
        seenOrbs.Clear(); g.Orbs.Clear();
        Step(9f, target.Pos, true, false);
        Check(seenOrbs.Count >= 1 && seenOrbs.Count <= 7, "9 s on one enemy knocks a few tiny orbs loose, never more than one every 1.5 s (" + seenOrbs.Count + ")");

        // Alt: Laser Sights
        Step(0.5f, target.Pos, false, false);
        fresh.Clear();
        float walk = me.Speed;
        V2 cursor = from + new V2(300f, 0f);
        Step(dt * 4f, cursor, false, true);
        Check(me.TpLaserT > Game.TpLaserDur - 0.2f && me.TpCd > Game.TpLaserCd - 0.2f, "Alt turns the lasers on for 4 s and starts the 12 s cooldown (" + me.TpLaserT.ToString("0.0") + ", " + me.TpCd.ToString("0.0") + ")");
        Check(Math.Abs(me.Speed / walk - Game.TpLaserSlow) < 0.01f, "you move at 85% with the lasers on");
        Step(0.1f, cursor, false, false);
        Check(client.Game.Players[1].TpLaserT > 0f && client.Game.Players[1].TpCd > 0f, "the other screen sees the lasers and the cooldown");
        fresh.Clear();
        Step(1f, cursor, true, false);
        Check(fresh.Count >= 8 && fresh.Count <= 12 && fresh.Count % 2 == 0, "both guns fire together, a volley every 0.22 s (" + fresh.Count + " rounds in 1 s)");
        Check(fresh.All(b => Math.Abs(b.Dmg - 10f * Game.TpLaserMult * me.Stats.DmgMult) < 0.01f), "laser rounds hit for 14");
        if (fresh.Count >= 2)
        {
            Bullet l = fresh[0], r = fresh[1];
            // where the two rounds' paths cross (they start either side of you and turn in)
            V2 dl = l.Vel.Normalized(), dr = r.Vel.Normalized();
            float den = dl.X * dr.Y - dl.Y * dr.X;
            V2 cross = V2.Zero;
            if (Math.Abs(den) > 1e-5f)
            {
                V2 w = r.Pos - l.Pos;
                float tl = (w.X * dr.Y - w.Y * dr.X) / den;
                cross = l.Pos + dl * tl;
            }
            Check(Math.Abs(den) > 1e-5f && cross.DistanceTo(cursor) < 40f, "the two rounds meet at the cursor (" + cross.DistanceTo(cursor).ToString("0") + " px off)");
        }

        // pressing again while it cools down does nothing; the lasers go off after 4 s
        float cd = me.TpCd;
        Step(dt * 4f, cursor, false, true);
        Check(me.TpCd < cd, "Alt does nothing while it cools down");
        Step(Game.TpLaserDur, cursor, false, false);
        Check(me.TpLaserT <= 0f && me.TpCd > 0f, "the lasers run out and the key keeps cooling down (" + me.TpCd.ToString("0.0") + " s left)");
        fresh.Clear();
        Step(0.5f, cursor, true, false);
        Check(fresh.Count >= 2 && fresh.Count <= 4 && fresh.All(b => Math.Abs(b.Dmg - 10f * me.Stats.DmgMult) < 0.01f), "back to one gun at a time (" + fresh.Count + " rounds in 0.5 s)");
    }
}

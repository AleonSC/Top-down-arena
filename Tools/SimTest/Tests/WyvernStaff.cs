using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Tank Summoner's fourth weapon, the Wyvern Staff (WyvernStaff.cs).
static partial class Program
{
    /// The Wyvern hovers over you; its gale slows and shoves enemies and slows their shots; holding Fire streams fire at the cursor
    /// for as long as the Cinder meter lasts (30 a second, 15 to start) and the Wyvern drifts back slowly after; Alt dives and slams
    /// (damage, stun, cleared shots, Cinder back, a 7 s cooldown); the other screen sees the Wyvern, the meter and the slam; Stormwing.
    static void WyvernStaffTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Stormwing", Role.Tank, 3, 0, Kit.Summoner);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.WyvernStaff && Defs.WeaponCount(Role.Tank, Kit.Summoner) == 4, "the Tank Summoner's fourth key is the Wyvern Staff (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        V2 aim = from + new V2(300f, 0f);
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = aim, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();
        Step(1.5f, false, false);
        var wyv = g.Minions.Values.FirstOrDefault(m => m.Kind == MinionKind.Wyvern && m.Owner == 1);
        Check(wyv != null && wyv.Companion, "the staff comes with a Wyvern companion");
        Check(wyv != null && wyv.Pos.CloserThan(from, 110f), "which hovers over you (" + (wyv == null ? "-" : wyv.Pos.Length.ToString("0")) + ")");

        // the gale: slow, shove, shots
        var near = Dummy(EnemyType.Grunt, wyv.Pos + new V2(60f, 0f));
        var far = Dummy(EnemyType.Grunt, wyv.Pos + new V2(0f, 300f));
        V2 near0 = near.Pos;
        Step(0.5f, false, false);
        Check(near.SlowT > 0f && near.SlowMult < 0.72f, "an enemy in the gale is slowed 30% (" + near.SlowMult.ToString("0.00") + ")");
        Check((near.Pos - near0).X > 8f, "and shoved away from the Wyvern, about 34 px a second (" + (near.Pos - near0).X.ToString("0") + " px in 0.5 s)");
        Check(far.SlowT <= 0f, "an enemy outside it is not");
        g.Enemies.Clear(); g.Bullets.Clear();
        var speedOf = typeof(Game).GetMethod("WyvGaleSpeed", flags);
        Func<V2, float> shotSpeed = at => (float)speedOf.Invoke(g, new object[] { new Bullet { Pos = at, Kind = Defs.SmallShot, Owner = 0 } });
        Check(Math.Abs(shotSpeed(wyv.Pos + new V2(40f, 0f)) - Game.WyvGaleShotSlow) < 0.001f, "an enemy shot in it flies 30% slower");
        Check(shotSpeed(wyv.Pos + new V2(0f, 250f)) == 1f, "one outside it does not");
        g.Bullets.Clear();

        // Fire: held, drains Cinder, burns what it points at, stops when the meter is dry; needs 15 to start
        var target = Dummy(EnemyType.Brute, aim);
        me.Cinder = 10f;
        Step(0.4f, true, false);
        Check(wyv.Wind == 0f && took(target) == 0f, "with under 15 Cinder, Fire does not start the breath");
        me.Cinder = Game.CinderMax;
        Step(1f, true, false);
        Check(wyv.Wind == 1f, "Fire held starts the breath (Wind " + wyv.Wind + ")");
        Check(me.Cinder < 90f && me.Cinder > 60f, "which spends Cinder, about 24 a second net of the gale's refill (" + me.Cinder.ToString("0") + ")");
        Check(took(target) > 14f && took(target) < 40f, "and hurts the enemy it points at for about 22 a second (" + took(target).ToString("0.#") + ")");
        Check(target.BurnT > 0f, "and sets it burning");
        Check(wyv.Pos.CloserThan(aim - new V2(Game.BreathStand, 0f), 40f), "the Wyvern stands " + Game.BreathStand + " px short of the cursor (" + (aim.X - wyv.Pos.X).ToString("0") + ")");
        Check(client.Game.Players[1].Cinder < 95f && client.Game.Minions.Values.Any(m => m.Kind == MinionKind.Wyvern && m.Wind == 1f), "the other screen sees the meter and the breath");
        Step(6f, true, false);
        Check(me.Cinder <= 15f + 0.01f || wyv.Wind == 0f, "held long enough, the Cinder runs dry and the breath stops (" + me.Cinder.ToString("0") + ")");
        Step(0.1f, false, false);
        Check(wyv.Wind == 0f, "letting go ends it");
        V2 left = wyv.Pos;
        Step(0.5f, false, false);
        float drift = (wyv.Pos - left).Length;
        Check(drift > 8f && drift < 0.5f * Game.WyvReturn + 5f, "it drifts slowly back to you, not at full speed (" + drift.ToString("0") + " px in 0.5 s)");
        g.Enemies.Clear();

        // Alt: Wyrm Slam
        me.Cinder = 50f;
        g.Bullets.Clear();
        var hit = Dummy(EnemyType.Grunt, aim + new V2(60f, 0f));
        var miss = Dummy(EnemyType.Grunt, aim + new V2(0f, 230f));
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, aim + new V2(40f, 40f), new V2(0f, 0f), 0f, 3f, 10f, 0, false, 0f });
        int slams = client.Game.Effects.Count(e => e.Kind == FxKind.WyvernSlam);
        Step(dt * 4f, false, true);
        Check(wyv.Wind == 2f && me.WyvSlamCd > 6f, "Alt sends the Wyvern diving and starts the 7 s cooldown");
        Step(0.7f, false, false);
        Check(Math.Abs(took(hit) - 30f) < 8f && took(miss) == 0f, "the slam hurts an enemy within 130 px for about 30 and not one beyond (" + took(hit).ToString("0.#") + ")");
        Check(hit.StunT > 0f || hit.FrozenT > 0f, "and stuns it");
        Check(!g.Bullets.ContainsKey(shot.Id), "and clears enemy shots in it");
        Check(me.Cinder > 55f, "and gives Cinder back (" + me.Cinder.ToString("0") + ")");
        Check(client.Game.Effects.Count(e => e.Kind == FxKind.WyvernSlam) > slams || g.Effects.Any(e => e.Kind == FxKind.WyvernSlam), "the other screen sees the slam (FxKind.WyvernSlam)");
        Check(wyv.Wind == 0f, "the Wyvern is back to hovering");
        Step(dt * 4f, false, true);
        Check(wyv.Wind == 0f, "Alt does nothing again while it cools down");

        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Stormwing");
        Check(card != null && card.Weapons != null && card.Weapons.Contains(WeaponId.WyvernStaff), "Stormwing is a Wyvern Staff card");
    }
}

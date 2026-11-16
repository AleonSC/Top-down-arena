using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Tank Elementalist's fourth weapon, Wind Clash (WindClash.cs).
static partial class Program
{
    /// Holding Fire runs the combo: a crescent ahead (not behind), the back crescent, the gale lance (reaching far down the aim), the
    /// clash ring (all round you); each wave hits an enemy once and shoves it away; stopping starts the combo over. Alt's Tempest:
    /// hurts enemies in it, catches enemy shots, slows you 30%, stops your attacks, ends after 3 s, cools down 9 s; the other screen
    /// sees the waves and the storm; Gale Force is its card.
    static void WindClashTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gale", Role.Tank, 3, 0, Kit.Elementalist);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.WindClash && Defs.WeaponCount(Role.Tank, Kit.Elementalist) == 4, "the Tank Elementalist's fourth key is Wind Clash (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f), right = from + new V2(400f, 0f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; queue.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = right, Fire = fire, WeaponAction = alt && f < 3 } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        float unit = me.Weapon.Damage * me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // the combo, Fire held
        var ahead = Dummy(EnemyType.Brute, from + new V2(110f, 0f));
        var behind = Dummy(EnemyType.Brute, from + new V2(-90f, 0f));
        var far = Dummy(EnemyType.Brute, from + new V2(290f, 0f));
        var side = Dummy(EnemyType.Brute, from + new V2(0f, 85f));
        Step(0.3f, true, false);
        Check(Math.Abs(took(ahead) - unit) < 0.6f, "the crescent hits the enemy ahead once, for 18 (" + took(ahead).ToString("0.#") + ")");
        Check(took(behind) == 0f && took(far) == 0f && took(side) == 0f, "and not the ones behind, far off or to the side");
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.WindWave), "the other screen sees the wave (FxKind.WindWave)");
        Step(0.34f, true, false);
        Check(Math.Abs(took(ahead) - 2f * unit) < 0.6f, "the back crescent hits it again (" + took(ahead).ToString("0.#") + ")");
        Step(0.44f, true, false);
        Check(Math.Abs(took(far) - 1.6f * unit) < 0.6f, "the gale lance reaches the enemy 290 px down the aim, for 29 (" + took(far).ToString("0.#") + ")");
        Check(took(side) == 0f, "but not the one beside you");
        Step(0.4f, true, false);
        Check(Math.Abs(took(side) - 1.2f * unit) < 0.6f && Math.Abs(took(behind) - 1.2f * unit) < 0.6f, "the clash ring hits all round you, for 22 (" + took(side).ToString("0.#") + ", " + took(behind).ToString("0.#") + ")");
        Check(me.WcStep == 0, "and the combo comes round to the first step again");

        // the shove, and the combo starting over
        g.Enemies.Clear();
        var grunt = Dummy(EnemyType.Grunt, from + new V2(100f, 0f));
        Step(1.2f, false, false);
        me.FireCd = 0f;
        Step(0.5f, true, false);
        Check(grunt.Pos.X > from.X + 125f, "a crescent shoves a regular enemy away from you (" + (grunt.Pos.X - from.X).ToString("0") + " px out)");
        Step(1.2f, false, false);
        Check(me.WcStep == 0, "stop for a moment and the combo starts over");

        // Alt: the Tempest
        g.Enemies.Clear(); g.Bullets.Clear();
        var inStorm = Dummy(EnemyType.Brute, from + new V2(80f, 0f));
        var outStorm = Dummy(EnemyType.Brute, from + new V2(0f, 260f));
        float walk = me.Speed;
        Step(dt * 4f, false, true);
        Check(me.WcTempestT > 0f && me.WcTempestCd > 8f, "Alt starts the Tempest and its cooldown");
        Check(Math.Abs(me.Speed / walk - Game.TempestSlow) < 0.01f, "you move 30% slower in it");
        var shot = (Bullet)shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(-200f, 0f), new V2(300f, 0f), 0f, 3f, 10f, 0, false, 0f });
        Step(0.6f, false, false);
        Check(!g.Bullets.ContainsKey(shot.Id), "an enemy shot coming into it is caught");
        Check(g.Effects.Any(e => e.Kind == FxKind.WindCatch) || client.Game.Effects.Any(e => e.Kind == FxKind.WindCatch), "and spins away (FxKind.WindCatch)");
        Check(took(inStorm) > 0f && took(outStorm) == 0f, "it hurts an enemy in it, not one outside (" + took(inStorm).ToString("0.#") + ")");
        Check(client.Game.Players[1].WcTempestT > 0f, "the other screen sees the storm");
        float before = took(inStorm);
        int waves = g.Effects.Count(e => e.Kind == FxKind.WindWave);
        me.FireCd = 0f;
        Step(0.3f, true, false);
        Check(g.Effects.Count(e => e.Kind == FxKind.WindWave) == waves, "you can't attack while it blows");
        Step(2.3f, false, false);
        Check(me.WcTempestT == 0f && Math.Abs(me.Speed / walk - 1f) < 0.01f, "it ends after 3 s and your speed comes back");
        Check(took(inStorm) - before > 0f, "it kept hurting the enemy while it blew");
        Step(dt * 4f, false, true);
        Check(me.WcTempestT == 0f, "Alt does nothing again while it cools down");

        var card = Upgrades.All.FirstOrDefault(u => u.Name == "Gale Force");
        Check(card != null && card.Weapons != null && card.Weapons.Contains(WeaponId.WindClash), "Gale Force is a Wind Clash card");
    }
}

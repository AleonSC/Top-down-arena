using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// The Healer Ninja's fourth weapon, the Twin Tessen (TwinTessen.cs).
static partial class Program
{
    /// Holding Fire runs the Crane Dance: the Left Flick hits ahead and heals an ally it brushes, the steps chain to the Crane
    /// Bloom (the only one that reaches far), the Petal Spin's petals heal the hurt ally, the Bloom gives Tailwind; hits fill
    /// Breeze (seen on the other screen); a pause starts the dance over. Alt held: the fans pull back (you slow, no attacks), letting
    /// go throws enemies back, turns enemy shots round, heals the ally and starts the 9 s cooldown; a full Breeze makes it a Typhoon
    /// that stuns and empties the bar; Divine Wind is its card.
    static void TwinTessenTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var shoot = typeof(Game).GetMethod("SpawnBullet", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Crane", Role.Healer, 3, 0, Kit.Ninja);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var ally = g.Players[2];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Check(me.Weapon.Id == WeaponId.TwinTessen && Defs.WeaponCount(Role.Healer, Kit.Ninja) == 4, "the Healer Ninja's fourth key is the Twin Tessen (" + me.Weapon.Id + ")");
        var queue = (List<EnemyType>)typeof(Game).GetField("spawnQueue", flags).GetValue(g);
        V2 from = new V2(500f, 450f), right = from + new V2(400f, 0f), allyAt = from + new V2(70f, 40f);
        Func<EnemyType, V2, Enemy> Dummy = (type, at) =>
        {
            var d = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false });
            d.MaxHp = d.Hp = 1e6f; d.Speed = 0f; d.FireT = 99f; d.SpawnT = 0f;
            return d;
        };
        V2 allyPos = allyAt;
        Action<float, bool, bool> Step = (secs, fire, alt) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                me.Pos = from; ally.Pos = allyPos; client.Game.Players[2].Pos = allyPos; queue.Clear();
                foreach (var e in g.Enemies.Values) e.FireT = 99f;
                foreach (var x in s) { x.Poll(); ally.Pos = allyPos; x.Game.FixedStep(dt, x == host ? new PlayerInput { AimAt = right, Fire = fire, WeaponAction = alt } : new PlayerInput()); }
            }
        };
        Func<Enemy, float> took = e => e.MaxHp - e.Hp;
        float unit = me.Weapon.Damage * me.Stats.DmgMult;
        queue.Clear(); g.Enemies.Clear(); g.Bullets.Clear();

        // the Crane Dance, Fire held
        var ahead = Dummy(EnemyType.Grunt, from + new V2(80f, -20f));
        var far = Dummy(EnemyType.Grunt, from + new V2(200f, 0f));
        var behind = Dummy(EnemyType.Grunt, from + new V2(-90f, 0f));
        ally.Hp = ally.MaxHp * 0.3f;
        float allyHp0 = ally.Hp;
        Step(0.17f, true, false);
        Check(Math.Abs(took(ahead) - unit) < 0.6f, "the Left Flick hits the enemy ahead for 10 (" + took(ahead).ToString("0.#") + ")");
        Check(took(far) == 0f && took(behind) == 0f, "and not one far off or behind");
        Check(ally.Hp > allyHp0 + 3f, "it heals the ally it brushes (" + (ally.Hp - allyHp0).ToString("0.#") + ")");
        Check(me.TtBreeze > 0, "the hit fills Breeze (" + me.TtBreeze + ")");
        int maxStep = 0;
        bool spinSeen = false, bloomFar = false;
        for (int f = 0; f < 1.6f / dt; f++)
        {
            int st = Game.TtStepOf(me.Combo);
            if (st == 4) spinSeen = true;
            if (maxStep < 5 && took(far) > 0f) bloomFar = true;  // (hit before the Bloom: only the Bloom should reach it)
            maxStep = Math.Max(maxStep, st);
            Step(dt, true, false);
        }
        Check(maxStep == 5, "the steps chain up to the Crane Bloom (" + maxStep + ")");
        Check(!bloomFar && took(far) >= unit * 2.8f - 0.6f, "only the Crane Bloom reaches the enemy 200 px off, for 28 (" + took(far).ToString("0.#") + ")");
        Check(spinSeen && client.Game.Effects.Any(e => e.Kind == FxKind.TessenStep), "the other screen sees the steps (FxKind.TessenStep)");
        Check(ally.TtTailT > 0f, "the Crane Bloom gives the ally in it Tailwind (" + ally.TtTailT.ToString("0.0") + " s)");
        Check(client.Game.Players[1].TtBreeze == me.TtBreeze && me.TtBreeze > 0, "Breeze reaches the other screen (" + client.Game.Players[1].TtBreeze + ")");

        // the Petal Spin's petals fly to the hurt ally
        ally.Hp = ally.MaxHp * 0.3f;
        allyPos = from + new V2(-200f, 0f);  // out of every step's reach: only petals can heal it
        Step(1.0f, false, false);
        Check(Game.TtStepOf(me.Combo) == 0 || me.ComboT <= 0f, "a pause starts the dance over");
        float hp0 = ally.Hp;
        bool petals = false;
        for (int f = 0; f < 1.6f / dt; f++)
        {
            Step(dt, true, false);
            petals |= client.Game.Effects.Any(e => e.Kind == FxKind.TessenPetals);
        }
        Check(petals && ally.Hp >= hp0 + Game.TtPetals * Game.TtPetalHeal - 0.5f, "the Petal Spin's petals heal the hurt ally 6 x 5 (" + (ally.Hp - hp0).ToString("0.#") + ")");

        // Kamikaze Gust: hold, then let go
        Step(1.0f, false, false);
        g.Enemies.Clear(); g.Bullets.Clear();
        allyPos = from + new V2(150f, 60f);
        ally.Hp = ally.MaxHp * 0.3f;
        me.TtBreezeF = 0f; me.TtBreeze = 0;
        var target = Dummy(EnemyType.Grunt, from + new V2(220f, 0f));
        var boss = Dummy(EnemyType.Brute, from + new V2(-150f, 0f));
        float speed0 = me.Speed;
        Step(0.6f, true, true);
        Check(me.TtCharging && client.Game.Players[1].TtCharging, "holding Alt pulls the fans back, on both screens");
        Check(me.Speed < speed0 * 0.7f, "you move slower while it charges (" + (me.Speed / speed0).ToString("0.00") + ")");
        float dmgBefore = took(target);
        Step(0.6f, true, true);
        Check(took(target) == dmgBefore, "no attacks while charging");
        shoot.Invoke(g, new object[] { Defs.SmallShot, from + new V2(120f, 10f), new V2(-300f, 0f), 0f, 2f, 5f, -1, false, 0f });
        int enemyShots0 = g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind));
        float allyHp1 = ally.Hp;
        V2 t0 = target.Pos;
        Step(dt * 2f, false, false);
        Check(!me.TtCharging && me.TtGustCd > Game.TtGustCd - 0.2f, "letting go sends the gust and starts the 9 s cooldown (" + me.TtGustCd.ToString("0.0") + ")");
        Check(took(target) > 0f, "the gust hurts the enemy in it (" + took(target).ToString("0.#") + ")");
        Step(0.5f, false, false);
        Check(target.Pos.X - t0.X > 120f, "and throws it back (" + (target.Pos.X - t0.X).ToString("0") + " px)");
        Check(took(boss) == 0f, "but not the one behind you");
        Check(g.Bullets.Values.Count(b => Defs.IsEnemyShot(b.Kind)) < enemyShots0, "the enemy shot in it turns round");
        Check(ally.Hp >= allyHp1 + Game.TtGustHeal - 0.5f && ally.TtTailT > 0f, "the ally in it is healed 20 and gets Tailwind (" + (ally.Hp - allyHp1).ToString("0.#") + ")");
        Check(client.Game.Effects.Any(e => e.Kind == FxKind.TessenGust) || client.Game.Players[1].TtGustCd > 0f, "the other screen sees the gust");
        Step(0.3f, false, true);
        Check(!me.TtCharging, "Alt does nothing while it cools down");
        Step(0.1f, false, false);

        // a full Breeze: the Typhoon
        Step(Game.TtGustCd, false, false);
        target.Pos = from + new V2(200f, 0f);
        me.TtBreezeF = Game.TtBreezeMax; me.TtBreeze = (byte)Game.TtBreezeMax;
        Step(0.5f, false, true);
        Step(dt * 2f, false, false);
        Check(target.StunT > 0.5f && me.TtBreeze == 0, "with a full Breeze the gust is a Typhoon: it stuns (" + target.StunT.ToString("0.0") + " s) and empties the bar (" + me.TtBreeze + ")");

        // Divine Wind
        var card = Upgrades.All.First(u => u.Name == "Divine Wind");
        Check(card.Weapons != null && card.Weapons.Contains(WeaponId.TwinTessen), "Divine Wind is the Twin Tessen's card");
        float reach0 = Game.TtGustReachOf(me);
        card.Apply(me.Stats);
        Check(Game.TtGustReachOf(me) > reach0 * 1.2f && me.Stats.TtHealMult > 1.4f, "Divine Wind: the gust reaches farther and heals more");

    }
}

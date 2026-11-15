using System;
using Arena;

// The Twin Blades (TwinBlades.cs): the first four steps shove enemies slightly, the Shear more, heavy enemies not at all.
static partial class Program
{
    static void TwinBladesTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var fire = typeof(Game).GetMethod("FireTwinBlades", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Dps", Role.Dps, 0, 0, Kit.Knight);
        var client = JoinAs(port, false, "Tank", Role.Tank, 0);
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(new[] { host, client }, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Bot = client.Game.Bot = false;
        Check(me.Weapon.Id == WeaponId.TwinBlades, "the DPS Knight's first weapon is the Twin Blades");
        V2 from = new V2(400f, 450f);
        Func<EnemyType, V2, Enemy> dummy = (type, at) => { var e = (Enemy)spawn.Invoke(g, new object[] { type, at, 0f, 0, false }); e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f; e.FrozenT = 99f; e.Pos = at; return e; };
        // One step: clear the field, put an enemy 50 px ahead, play the step, and see how far it was pushed.
        Func<int, EnemyType, float> push = (step, type) =>
        {
            g.Enemies.Clear(); g.Bullets.Clear();
            var at = new V2(from.X + 50f, from.Y);
            var e = dummy(type, at);
            me.Combo = step - 1; me.ComboT = step > 1 ? 1f : 0f; me.FireCd = 0f;
            me.Pos = from; me.Aim = 0f;
            fire.Invoke(g, new object[] { me, 0f, 1f, 0 });
            for (int f = 0; f < 0.9f / dt; f++) { me.Pos = from; e.FireT = 99f; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(900f, 450f) }); }
            return e.Pos.X - at.X;
        };
        float cut = push(1, EnemyType.Grunt), jab = push(3, EnemyType.Grunt), spin = push(4, EnemyType.Grunt), shear = push(5, EnemyType.Grunt);
        float heavy = push(1, EnemyType.Juggernaut);
        Console.WriteLine("  twin blades push: cut {0:0.0}, jab {1:0.0}, spin {2:0.0}, shear {3:0.0}, heavy {4:0.0}", cut, jab, spin, shear, heavy);
        Check(Math.Abs(cut - Game.MeleeKnock) < 2f, "a cut gives the standard melee shove (MeleeHit.cs)");
        Check(Math.Abs(jab - 2f * Game.MeleeKnock) < 4f, "the Double Jab: the standard shove a thrust");
        Check(Math.Abs(spin - 2f * Game.MeleeKnock) < 4f, "the Pirouette: the standard shove a turn");
        Check(Math.Abs(shear - Game.MeleeKnock) < 2f, "the Crescent Shear: the standard shove");
        Check(Math.Abs(heavy) < 1f, "a heavy enemy isn't shoved");
        host.Leave(""); client.Leave("");
    }
}

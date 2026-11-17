using System;
using System.Collections.Generic;
using Arena;

static partial class Program
{
    /// Cauterize (Core/Cauterize.cs): fire on a bleeding enemy lands the bleed it had left at once, times 1.25, split
    /// between the bleeder and the burner, ends the bleed, sends its look, and waits a second before it can go again.
    static void CauterizeTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var bleed = typeof(Game).GetMethod("ApplyBleed", flags);
        var hit = typeof(Game).GetMethod("OnBulletHit", flags);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Bleeder", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Burner", Role.Tank, 0, 0, Kit.Soldier);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        g.Enemies.Clear(); g.Bullets.Clear(); g.Effects.Clear();
        var p1 = g.Players[1]; var p2 = g.Players[2];

        var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 450f), 0f, 0, false });
        e.Hp = e.MaxHp = 1e5f; e.SpawnT = 0f; e.FireT = 99f;
        bleed.Invoke(g, new object[] { e, 10f, 1, false });
        bleed.Invoke(g, new object[] { e, 10f, 1, false });
        float left = Game.BleedLeft(e);
        Check(Math.Abs(left - 60f) < 0.01f, "two 10/s bleed stacks for 3 s: 60 left (" + left + ")");
        float d1 = p1.Run.DamageDealt, d2 = p2.Run.DamageDealt, hp = e.Hp;

        hit.Invoke(g, new object[] { new Bullet { Owner = 2, Kind = Defs.FireballShot, Ignite = Game.FireballBurn, Dmg = 0f, Pos = e.Pos }, e });
        Check(Math.Abs((hp - e.Hp) - 75f) < 0.5f, "fire on it lands the bleed at once, times 1.25: 75 (" + (hp - e.Hp) + ")");
        Check(Math.Abs((p1.Run.DamageDealt - d1) - 60f) < 0.5f, "the bleeder is credited the bleed's 60");
        Check(Math.Abs((p2.Run.DamageDealt - d2) - 15f) < 0.5f, "the burner is credited the 15 bonus");
        Check(e.BleedT <= 0f && e.BleedDps == 0f && e.BleedStacks == 0, "the bleed ends");
        Check(e.BurnT > 0f, "it still burns");
        bool fx = false;
        foreach (var f in g.Effects) fx |= f.Kind == FxKind.Cauterize && (int)f.Pos2.X == e.Id && Math.Abs(f.Pos2.Y - 75f) < 0.5f;
        Check(fx, "its look is sent with the enemy's id and the burst");

        // Within a second: a fresh bleed isn't cauterized; after it, it is.
        bleed.Invoke(g, new object[] { e, 10f, 1, false });
        hit.Invoke(g, new object[] { new Bullet { Owner = 2, Kind = Defs.FireballShot, Ignite = Game.FireballBurn, Dmg = 0f, Pos = e.Pos }, e });
        Check(e.BleedT > 0f, "within CauterizeGap a new bleed keeps running");
        for (int f = 0; f < 1.1f / dt; f++) foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput()); }
        bleed.Invoke(g, new object[] { e, 10f, 1, false });
        hp = e.Hp;
        hit.Invoke(g, new object[] { new Bullet { Owner = 2, Kind = Defs.FireballShot, Ignite = Game.FireballBurn, Dmg = 0f, Pos = e.Pos }, e });
        Check(e.BleedT <= 0f && hp - e.Hp > 1f, "after it, fire cauterizes again");

        // Fire with no bleed does nothing extra.

        for (int f = 0; f < 1.1f / dt; f++) foreach (var x in s) { x.Poll(); x.Game.FixedStep(dt, new PlayerInput()); }
        hp = e.Hp;
        hit.Invoke(g, new object[] { new Bullet { Owner = 2, Kind = Defs.FireballShot, Ignite = Game.FireballBurn, Dmg = 0f, Pos = e.Pos }, e });
        Check(hp - e.Hp < 0.01f, "fire on an enemy that isn't bleeding: no burst");
        foreach (var x in s) x.Leave("");
    }
}

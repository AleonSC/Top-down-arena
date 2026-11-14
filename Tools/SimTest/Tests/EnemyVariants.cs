using System;
using Arena;

// Enemy variants (EnemyVariants.cs): the Rioter's Ball and Chain spawns in its place, slower, and reaches the client.
static partial class Program
{
    static void EnemyVariantsTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 5 };
        host.Host(port, "Host", Role.Dps, 0);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Tank, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);

        float old = Game.VariantChanceScale;
        Game.VariantChanceScale = 1000f;
        var chained = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(200f, 200f), 0.5f, 0, false });
        var add = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(240f, 200f), 0.5f, 9, false });
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(280f, 200f), 0.5f, 0, false });
        Game.VariantChanceScale = 0f;
        var brawler = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(320f, 200f), 0.5f, 0, false });
        Game.VariantChanceScale = old;

        Check(chained.Variant == EnemyVariant.BallAndChain && chained.Type == EnemyType.Rioter, "a Rioter can spawn as a Ball and Chain (still a Rioter)");
        Check(brawler.Variant == EnemyVariant.None, "with no roll it's the regular Brawler");
        Check(add.Variant == EnemyVariant.None && grunt.Variant == EnemyVariant.None, "a summoned add and an enemy with no variant stay regular");
        Check(Math.Abs(chained.Speed - brawler.Speed * Game.VariantDef(EnemyType.Rioter, EnemyVariant.BallAndChain).SpeedMult) < 0.01f && chained.Speed < brawler.Speed,
            "the Ball and Chain runs slower (" + chained.Speed.ToString("0") + " vs " + brawler.Speed.ToString("0") + ")");

        Run(s, dt, 1, () => client.Game.Enemies.ContainsKey(chained.Id) && client.Game.Enemies.ContainsKey(brawler.Id));
        Enemy ce, cb;
        Check(client.Game.Enemies.TryGetValue(chained.Id, out ce) && ce.Variant == EnemyVariant.BallAndChain
            && client.Game.Enemies.TryGetValue(brawler.Id, out cb) && cb.Variant == EnemyVariant.None, "the client sees which is which");

        int n = 0, chains = 0;
        for (int i = 0; i < 400; i++)
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Rioter, new V2(400f, 400f), 0.5f, 0, false });
            n++;
            if (e.Variant == EnemyVariant.BallAndChain) chains++;
            g.Enemies.Remove(e.Id);
        }
        float share = chains / (float)n;
        Check(share > 0.2f && share < 0.4f, "about 30% of Rioters are Ball and Chains (" + (share * 100f).ToString("0") + "%)");
    }
}

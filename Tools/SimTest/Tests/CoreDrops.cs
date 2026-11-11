using System.Linq;
using System;
using Arena;

// Weapon variants, step 2 (Variants.cs, CoreDrops.cs, Docs/Variants.md): what a run and a hunt pay in Cores, and the glowing Core Carrier.
static partial class Program
{
    static void CoreDropsTest(float dt)
    {
        Check(Variants.RunCores(true, 10, false) == 15 && Variants.RunCores(false, 3, false) == 3 && Variants.RunCores(true, 10, true) == 0, "a Campaign pays 1 a boss and 5 for the win; Endless pays none");
        Check(Variants.HuntCores(true) == 5 && Variants.HuntCores(false) == 0, "a won Boss Hunt pays 5 Cores, a lost one nothing");

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
        float old = Game.CoreCarrierChance;
        Game.CoreCarrierChance = 1f;
        var a = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(200f, 200f), 0.5f, 0, false });
        var b = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(260f, 200f), 0.5f, 0, false });
        var c = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(300f, 200f), 0.5f, 7, false });
        Game.CoreCarrierChance = old;
        Check(Game.HasTrait(a, EliteTrait.CoreCarrier), "a regular enemy can spawn as a Core Carrier");
        Check(!Game.HasTrait(b, EliteTrait.CoreCarrier) && !Game.HasTrait(c, EliteTrait.CoreCarrier), "only one carrier is alive at a time, and a summoned add never is");
        var dropCores = typeof(Game).GetMethod("DropCores", flags);
        dropCores.Invoke(g, new object[] { a });
        Check(g.CoresFound >= Variants.CarrierMin && g.CoresFound <= Variants.CarrierMax, "killing the carrier drops 1-3 Cores (" + g.CoresFound + ")");
    }
}

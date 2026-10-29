using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Divergent paths and the Bazaar.
static partial class Program
{
    static void PathsTest(float dt)
    {
        // Divergent paths: after a boss the host picks one of two paths for the next four waves.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var build = typeof(Game).GetMethod("BuildWave", flags);
        var cleared = typeof(Game).GetMethod("WaveCleared", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 5 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        // Beat the wave-5 boss.
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Enemies.Count > 0);
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        NoPicks();
        Run(s, dt, 1f);
        Check(g.PathOffer != null && g.PathOffer[0] != g.PathOffer[1] && client.Game.PathOffer != null && client.Game.PathOffer[1] == g.PathOffer[1],
            "after a boss, two different paths are offered to everyone");
        float timer = g.StateTimer;
        client.Game.ChoosePath(0);
        Run(s, dt, 3f, null, NoPicks);
        Check(g.PathOffer != null && g.State == MatchState.Intermission && g.StateTimer >= timer, "the next wave waits for the host; a friend's click doesn't choose");
        var chosen = g.PathOffer[1];
        g.ChoosePath(1);
        Run(s, dt, 1f, null, NoPicks);
        Check(g.CurrentPath == chosen && client.Game.CurrentPath == chosen && g.PathFrom == 6 && g.PathTo == 9 && client.Game.PathTo == 9 && client.Game.Route.Count == 1,
            "the host's pick is the path for waves 6-9, for everyone");
        Check(client.Game.PathWavesLeft == 4 && !client.Game.OnPath, "the HUD counts 4 waves left before it starts");

        // Each path's enemy mix (built directly at wave 7).
        Func<PathId, List<EnemyType>> Mix = id =>
        {
            g.CurrentPath = id; g.PathFrom = 6; g.PathTo = 9; g.Wave = 7;
            build.Invoke(g, new object[] { 7 });
            return new List<EnemyType>((List<EnemyType>)queueField.GetValue(g));
        };
        g.CurrentPath = PathId.None;
        build.Invoke(g, new object[] { 7 });
        int plain = ((List<EnemyType>)queueField.GetValue(g)).Count;
        var swarm = Mix(PathId.SwarmFields);
        var ridge = Mix(PathId.SniperRidge);
        var elite = Mix(PathId.EliteGauntlet);
        float meleeFrac = swarm.Count(t => Defs.Get(t).Melee) / (float)swarm.Count;
        float ridgeFrac = ridge.Count(t => t == EnemyType.Sniper || t == EnemyType.Mortar || t == EnemyType.Grunt) / (float)ridge.Count;
        Console.WriteLine("  paths at wave 7: plain {0} enemies; Swarm Fields {1} ({2:0}% melee); Sniper Ridge {3} ({4:0}% snipers/mortars/grunts); Elite Gauntlet {5}",
            plain, swarm.Count, meleeFrac * 100f, ridge.Count, ridgeFrac * 100f, elite.Count);
        int baseCount = 4 + 2 * 7;  // the wave-7 enemy count before swarmers pair up (BuildWave)
        Check(meleeFrac > 0.7f && swarm.Count >= baseCount * 1.5f, "Swarm Fields: mostly melee rushers, about twice as many");
        Check(ridgeFrac > 0.6f, "Sniper Ridge: mostly snipers, mortars and grunts");
        Check(elite.Count <= baseCount / 2 + 1, "Elite Gauntlet: half as many enemies");
        g.CurrentPath = PathId.SwarmFields;
        var brute = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 300f), 0f, 0, false });
        g.CurrentPath = PathId.None;
        var plainBrute = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 300f), 0f, 0, false });
        g.CurrentPath = PathId.EliteGauntlet;
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(800f, 300f), 0f, 0, false });
        Check(Math.Abs(brute.MaxHp - plainBrute.MaxHp * 0.6f) < 0.1f && grunt.Elite, "Swarm Fields rushers have 60% health; the Gauntlet's enemies are all elite");

        // The bonus while on a path (danger 2: +25% XP).
        g.CurrentPath = PathId.SniperRidge;
        g.State = MatchState.Fighting;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Step(host, dt);
        var me = g.Players[1];
        float xp0 = me.Xp; int lv0 = me.Level;
        var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Sniper, new V2(900f, 300f), 0f, 0, false });
        target.Elite = false; target.Hp = 0f; target.LastHitBy = 1;
        Step(host, dt);
        Check(me.Level == lv0 && Math.Abs(me.Xp - xp0 - Defs.Get(EnemyType.Sniper).Xp * 1.25f) < 0.01f, "a danger-2 path gives +25% XP");

        // Finishing: the prize, then the plain arena for the boss.
        foreach (var p in g.Players.Values) p.PendingPicks = 0;
        g.Wave = 9; g.PathTo = 9;
        cleared.Invoke(g, null);
        Run(s, dt, 0.5f);
        Check(g.CurrentPath == PathId.None && client.Game.CurrentPath == PathId.None && g.Players[1].PendingPicks >= 1 && g.Players[2].PendingPicks >= 1,
            "after the path's last wave everyone gets its prize (danger 2: +1 pick) and the boss wave is plain");
        host.Leave(""); client.Leave("");

        // A bot host picks a path by itself, so automated runs never stall.
        port = nextPort++;
        host = new Session { AutoStart = 1, StartWave = 5 };
        host.Host(port, "Bot", Role.Dps);
        g = host.Game; g.Bot = true;
        host.StartMatch();
        g.DevSetGod(1, true);
        for (int f = 0; f < 60 * 40 && !(g.Wave >= 6 && g.State == MatchState.Fighting); f++)
        {
            Step(host, dt);
            if (g.State == MatchState.Fighting && g.Wave == 5) foreach (var e in g.Enemies.Values) e.Hp = 0f;
        }
        Check(g.Wave >= 6 && g.Route.Count == 1 && g.OnPath, "a bot host picks a path and the run carries on");
        host.Leave("");
    }

    // Mender's Bow, drawn by the joining player: taps are weak, a full draw hits hard and pierces, a
    // release right at full draw is perfect, drawing slows you, and the draw is timed on the shooter's side.
    static void PathHazardsTest(float dt)
    {
        // Paths, step 2: no path, Burning Grounds' fire and Sanctuary's pools.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var build = typeof(Game).GetMethod("BuildWave", flags);
        var cleared = typeof(Game).GetMethod("WaveCleared", flags);
        var offer = typeof(Game).GetMethod("OfferPaths", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 6 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        Run(s, dt, 20f, () => g.State == MatchState.Fighting);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.5f);

        // No path: the plain arena for the stretch, noted in the route.
        g.Wave = 5;
        offer.Invoke(g, null);
        Run(s, dt, 0.3f);
        g.ChoosePath(2);
        Run(s, dt, 0.5f);
        Check(g.PathOffer == null && g.CurrentPath == PathId.None && client.Game.PathOffer == null && client.Game.Route.Count == 1 && client.Game.Route[0] == PathId.None,
            "the host can choose no path: the plain arena, noted in everyone's route");

        // Burning Grounds.
        g.CurrentPath = PathId.BurningGrounds; g.PathFrom = 6; g.PathTo = 9; g.Wave = 7; g.State = MatchState.Fighting;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        g.Patches.Clear();
        Run(s, dt, Paths.FireEvery + 0.3f);
        for (int k = 0; k < 20; k++) { client.Poll(); Thread.Sleep(2); }
        Check(g.Patches.Count >= 1 && g.Patches.Keys.All(k => client.Game.Patches.ContainsKey(k)), "Burning Grounds starts fires, and everyone sees them");
        var fire = g.Patches.Values.First();
        var me = g.Players[1];
        var target = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, fire.Pos, 0f, 0, false });
        target.Hp = target.MaxHp = 1e5f;
        float taken0 = me.Run.DamageTaken;
        Run(s, dt, Paths.FireWarn * 0.5f, null, () => { me.Pos = fire.Pos; me.InvulnT = 0f; target.Pos = fire.Pos; target.FrozenT = 1f; });
        float duringWarn = me.Run.DamageTaken - taken0;
        float r0 = fire.Radius;
        Run(s, dt, Paths.FireWarn + 1.5f, null, () => { me.Pos = fire.Pos; me.InvulnT = 0f; target.Pos = fire.Pos; target.FrozenT = 1f; });
        float burned = me.Run.DamageTaken - taken0;
        Console.WriteLine("  burning grounds: {0:0} damage during the warning, {1:0} once lit; the Brute lost {2:0}; radius {3:0} -> {4:0}", duringWarn, burned, target.MaxHp - target.Hp, r0, fire.Radius);
        Check(duringWarn == 0f && burned > 0f && target.Hp < target.MaxHp && fire.Radius > r0, "fire warns first, then burns players and enemies, and spreads");
        cleared.Invoke(g, null);
        Run(s, dt, 0.5f);
        Check(g.Patches.Count == 0 && client.Game.Patches.Count == 0, "clearing a wave puts the fires out everywhere");

        // Sanctuary: 20% fewer enemies, healing pools, and the safe path's prize.
        g.CurrentPath = PathId.None; g.Wave = 7;
        build.Invoke(g, new object[] { 7 });
        // Count spawn rolls, not heads: swarmers come in random pairs.
        Func<int> Rolls = () => { var q = (List<EnemyType>)queueField.GetValue(g); return q.Count - q.Count(t => t == EnemyType.Swarmer) / 2; };
        int plain = Rolls();
        g.CurrentPath = PathId.Sanctuary;
        build.Invoke(g, new object[] { 7 });
        int calm = Rolls();
        Check(calm == (int)(plain * 0.8f), "Sanctuary: a fifth fewer enemies (" + plain + " -> " + calm + ")");
        g.State = MatchState.Fighting;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        var keepAlive = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(60f, 60f), 0f, 0, false });  // so the wave doesn't end
        keepAlive.Hp = keepAlive.MaxHp = 1e6f;
        g.Patches.Clear();
        Run(s, dt, Paths.PoolEvery + Paths.PoolWarn + 0.3f, null, () => { keepAlive.FrozenT = 1f; keepAlive.FireT = 9f; });
        var pool = g.Patches.Values.FirstOrDefault(pt => pt.Kind == PatchKind.Heal);
        me.Hp = me.MaxHp * 0.4f;
        float hp0 = me.Hp;
        if (pool != null)
            Run(s, dt, 1.2f, null, () => { me.Pos = pool.Pos; keepAlive.FrozenT = 1f; keepAlive.FireT = 9f; });
        Check(pool != null && me.Hp > hp0 + Paths.PoolHeal * 0.8f, "Sanctuary's pools heal whoever stands in them");
        me.Hp = me.MaxHp * 0.5f;
        int rerolls = me.Rerolls;
        g.Wave = 9; g.PathTo = 9;
        cleared.Invoke(g, null);
        Run(s, dt, 0.3f);
        Check(me.Hp >= me.MaxHp - 0.5f && me.Rerolls == rerolls + 2 && g.CurrentPath == PathId.None, "the safe path's prize: a full heal and 2 rerolls");
        host.Leave(""); client.Leave("");
    }

    static void PathTwistsTest(float dt)
    {
        // Paths, step 3: Frozen Lake's sliding and chill, Blood Moon's hunt.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 1, StartWave = 6 };
        host.Host(port, "Solo", Role.Dps);
        var g = host.Game;
        host.StartMatch();
        for (int f = 0; f < 60 * 20 && g.State != MatchState.Fighting; f++) Step(host, dt);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        Step(host, dt);
        var keepAlive = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, new V2(60f, 60f), 0f, 0, false });  // so the wave doesn't end
        keepAlive.Hp = keepAlive.MaxHp = 1e6f;
        var me = g.Players[1];
        g.DevSetGod(1, true);
        Func<V2, float, float> Move = (dir, secs) =>
        {
            me.Pos = new V2(800f, 450f);
            for (int f = 0; f < secs * 60; f++)
            {
                keepAlive.FrozenT = 1f; keepAlive.FireT = 9f;
                host.Poll(); g.FixedStep(dt, new PlayerInput { Move = dir, AimAt = new V2(800f, 200f) });
            }
            return me.Pos.X - 800f;
        };
        Action Settle = () => { for (int f = 0; f < 120; f++) { keepAlive.FrozenT = 1f; host.Poll(); g.FixedStep(dt, new PlayerInput { AimAt = new V2(800f, 200f) }); } };

        // Plain ground: you start at full speed and stop when you let go.
        g.CurrentPath = PathId.None;
        Settle();
        float plainStart = Move(new V2(1f, 0f), 0.15f), plainCoast = Move(V2.Zero, 0.4f);
        // Frozen Lake: speed builds, and you keep sliding.
        g.CurrentPath = PathId.FrozenLake; g.PathFrom = 6; g.PathTo = 9; g.Wave = 7;
        Settle();
        float iceStart = Move(new V2(1f, 0f), 0.15f), iceCoast = Move(V2.Zero, 0.4f);
        Console.WriteLine("  frozen lake: first 0.15 s {0:0} px (plain {1:0}); after letting go {2:0} px more (plain {3:0})", iceStart, plainStart, iceCoast, plainCoast);
        Check(iceStart < plainStart * 0.7f && iceCoast > 20f && Math.Abs(plainCoast) < 1f, "Frozen Lake: speed builds up, and you slide on after letting go");
        var chilled = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(900f, 300f), 0f, 0, false });
        Check(Math.Abs(chilled.Speed - Defs.Get(EnemyType.Grunt).Speed * Paths.IceEnemySpeed) < 0.01f, "Frozen Lake: enemies are 10% slower");
        chilled.Hp = 0f; Step(host, dt);

        // Blood Moon: no regeneration, no orbs, kills heal.
        g.CurrentPath = PathId.BloodMoon;
        g.DevSetGod(1, false);
        me.Hp = me.MaxHp * 0.5f;
        float hp0 = me.Hp;
        for (int f = 0; f < 120; f++) { keepAlive.FrozenT = 1f; keepAlive.FireT = 9f; me.InvulnT = 1f; Step(host, dt); }
        Check(Math.Abs(me.Hp - hp0) < 0.01f, "Blood Moon: no regeneration");
        int orbs = g.Orbs.Count;
        float before = me.Hp;
        for (int k = 0; k < 10; k++)
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(700f + k * 10f, 300f), 0f, 0, false });
            e.Hp = 0f; e.LastHitBy = 1;
        }
        keepAlive.FrozenT = 1f; Step(host, dt);
        Console.WriteLine("  blood moon: 10 kills healed {0:0.#} HP (2 + 0.1 x wave {1} each)", me.Hp - before, g.Wave);
        Check(g.Orbs.Count == orbs && Math.Abs(me.Hp - before - 10f * Paths.BloodMoonHeal(g.Wave)) < 0.5f, "Blood Moon: no orbs, and every kill heals its killer");
        host.Leave("");
    }

    static void PathArenaRulesTest(float dt)
    {
        // Paths, step 4: Narrow Pass's walls (everyone, movement, spawns, bullets) and Fogbound's mist.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnAt = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var spawnEnemy = typeof(Game).GetMethod("SpawnEnemy", flags);
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var broadcast = typeof(Game).GetMethod("BroadcastPath", flags);
        var cleared = typeof(Game).GetMethod("WaveCleared", flags);
        var queueField = typeof(Game).GetField("spawnQueue", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 6 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        g.Bot = client.Game.Bot = false;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Run(s, dt, 20f, () => g.State == MatchState.Fighting);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        ((List<EnemyType>)queueField.GetValue(g)).Clear();
        Run(s, dt, 0.1f);
        var keepAlive = (Enemy)spawnAt.Invoke(g, new object[] { EnemyType.Brute, new V2(800f, 450f), 0f, 0, false });  // so the wave doesn't end
        keepAlive.Hp = keepAlive.MaxHp = 1e6f;
        Action Hold = () => { keepAlive.FrozenT = 1f; keepAlive.FireT = 9f; };

        // The walls close in for everyone.
        g.CurrentPath = PathId.NarrowPass; g.PathFrom = 6; g.PathTo = 9;
        broadcast.Invoke(g, null);
        Run(s, dt, 0.5f, null, Hold);
        Check(g.NarrowF > 0.1f && g.NarrowF < 0.6f, "Narrow Pass: the walls close in gradually, not all at once");
        Run(s, dt, 1.5f, null, Hold);
        var lo = g.ArenaMin; var hi = g.ArenaMax;
        Console.WriteLine("  narrow pass: playable area {0:0}x{1:0} (from {2:0},{3:0}); client agrees: {4}", hi.X - lo.X, hi.Y - lo.Y, lo.X, lo.Y, client.Game.ArenaMin.X == lo.X);
        Check(g.NarrowF == 1f && client.Game.NarrowF == 1f && lo.X == Paths.NarrowInsetX && client.Game.ArenaMax.Y == hi.Y, "Narrow Pass: the walls stand at the same place for everyone");

        // The friend walks into the left wall: stopped, not wrapped round.
        var friend = client.Game.Players[2];
        for (int f = 0; f < 60 * 4; f++)
        {
            Hold();
            Step(host, dt);
            client.Poll();
            client.Game.FixedStep(dt, new PlayerInput { Move = new V2(-1f, 0.3f), AimAt = new V2(640, 360) });
            client.Game.Update(dt);
            if (f % 2 == 0) Thread.Sleep(1);
        }
        Run(s, dt, 0.2f, null, Hold);
        float r = friend.Def.BodyR, hostX = g.Players[2].Pos.X;
        Console.WriteLine("  narrow pass: the friend stopped at x {0:0} (wall at {1:0}); the host sees {2:0}", friend.Pos.X, lo.X, hostX);
        Check(Math.Abs(friend.Pos.X - (lo.X + r)) < 1f && Math.Abs(hostX - friend.Pos.X) < 3f, "Narrow Pass: the walls stop you (no wrapping round), for the host too");

        // Enemies come in at the walls, not outside them.
        bool allInside = true;
        for (int k = 0; k < 40; k++)
        {
            int before = g.Enemies.Count;
            spawnEnemy.Invoke(g, new object[] { k % 2 == 0 ? EnemyType.Grunt : EnemyType.Spinner });
            foreach (var e in g.Enemies.Values)
                if (e != keepAlive && (e.Pos.X < lo.X || e.Pos.X > hi.X || e.Pos.Y < lo.Y || e.Pos.Y > hi.Y))
                    allInside = false;
        }
        Run(s, dt, 3f, null, () => { Hold(); foreach (var e in g.Enemies.Values) if (e != keepAlive && (e.Pos.X < lo.X || e.Pos.X > hi.X || e.Pos.Y < lo.Y || e.Pos.Y > hi.Y)) allInside = false; });
        Check(allInside, "Narrow Pass: enemies spawn and move inside the walls");
        foreach (var e in g.Enemies.Values) if (e != keepAlive) e.Hp = 0f;
        Run(s, dt, 0.2f, null, Hold);

        // Bullets stop at the walls.
        var shot = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(lo.X + 40f, 300f), new V2(-600f, 0f), 0f, 3f, 1f, 1, false, 0f });
        Run(s, dt, 0.3f, null, Hold);
        Check(!g.Bullets.ContainsKey(shot.Id), "Narrow Pass: bullets stop at the walls");

        // The walls open again for the boss.
        g.Wave = 9;
        cleared.Invoke(g, null);
        Run(s, dt, 2f, null, () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = 0; p.LegendaryQueue.Clear(); p.Offer = null; } });
        Check(g.CurrentPath == PathId.None && g.NarrowF == 0f && client.Game.NarrowF == 0f && g.ArenaMin.X == 0f, "Narrow Pass: the walls open again before the boss");

        // Fogbound: the mist rolls in on everyone's screen from the path alone (nothing else is sent).
        g.CurrentPath = PathId.Fogbound; g.PathFrom = 11; g.PathTo = 14; g.Wave = 10;
        broadcast.Invoke(g, null);
        Run(s, dt, 2.5f);
        Check(client.Game.FogF == 1f && g.FogF == 1f && g.NarrowF == 0f, "Fogbound: the mist rolls in for everyone");
        host.Leave(""); client.Leave("");
    }

    // The Bazaar (Shop.cs): kills on its waves drop gold, then a shop before the boss that closes when everyone is done.
    static void BazaarTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var cleared = typeof(Game).GetMethod("WaveCleared", flags);
        var openShop = typeof(Game).GetMethod("OpenShop", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 5 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Enemies.Count > 0);
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; });
        NoPicks();
        Run(s, dt, 0.5f);
        g.PathOffer = new[] { PathId.Bazaar, PathId.SniperRidge };
        g.ChoosePath(0);
        Run(s, dt, 0.5f, null, NoPicks);
        Check(client.Game.CurrentPath == PathId.Bazaar && Paths.Get(PathId.Bazaar).Danger == 1, "the Bazaar is a 1-skull path everyone sees");

        // Gold: every kill on its waves gives every player the same gold.
        var me = g.Players[1]; var friend = g.Players[2];
        g.Wave = 7; g.State = MatchState.Fighting;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        Step(host, dt);
        int gold0 = me.Gold, friend0 = friend.Gold;
        var grunt = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, new V2(900f, 300f), 0f, 0, false });
        grunt.Elite = false; grunt.Hp = 0f; grunt.LastHitBy = 2;
        Step(host, dt);
        int each = (int)Math.Round(Defs.Get(EnemyType.Grunt).Xp * Game.GoldPerXp);
        Run(s, dt, 0.3f);
        Check(each > 0 && me.Gold - gold0 == each && friend.Gold - friend0 == each && client.Game.Players[2].Gold == friend.Gold,
            "a kill on the Bazaar gives every player the same gold (" + each + " for a grunt), and clients see it");

        // The shop opens after its last wave, and the next wave waits for it.
        me.Gold = 400; friend.Gold = 1000;
        g.Wave = 9; g.PathTo = 9;
        cleared.Invoke(g, null);
        NoPicks();
        Run(s, dt, 0.5f, null, NoPicks);
        var cme = client.Game.Players[2];
        int stocked = 0;
        foreach (int u in cme.Shelf) if (u >= 0) stocked++;
        Check(g.ShopOpen && client.Game.ShopOpen && g.CurrentPath == PathId.None && stocked >= 5, "after the Bazaar's last wave the shop opens with a shelf for each player (" + stocked + " items)");
        float timer = g.StateTimer;
        Run(s, dt, 2f, null, NoPicks);
        Check(g.State == MatchState.Intermission && g.StateTimer >= timer, "the next wave waits while the shop is open (no timer)");

        // Buying, healing, a revive token, removing and restocking (as the client, through the host).
        int slot = Array.FindIndex(friend.Shelf, u => u >= 0 && Upgrades.All[u].Grants == AbilityId.None);
        int bought = friend.Shelf[slot], price = Game.Price(bought), have = Upgrades.Count(friend.Taken, bought);
        client.Game.Shop(ShopAction.Buy, slot);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(Upgrades.Count(friend.Taken, bought) == have + 1 && friend.Gold == 1000 - price && Game.Sold(cme, slot), "buying takes the gold and gives the upgrade (" + Upgrades.All[bought].Name + " for " + price + ")");
        client.Game.Shop(ShopAction.Buy, slot);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(Upgrades.Count(friend.Taken, bought) == have + 1, "a shelf item can only be bought once");
        friend.Hp = friend.MaxHp * 0.5f;
        int before = friend.Gold;
        client.Game.Shop(ShopAction.Heal);
        client.Game.Shop(ShopAction.Revive);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(friend.Hp >= friend.MaxHp && friend.ReviveToken && cme.ReviveToken && friend.Gold == before - Game.HealPrice - Game.RevivePrice, "full heal and a revive token");
        client.Game.Shop(ShopAction.Revive);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(friend.Gold == before - Game.HealPrice - Game.RevivePrice, "only one revive token at a time");
        client.Game.Shop(ShopAction.Remove, bought);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(Upgrades.Count(friend.Taken, bought) == have, "removing an upgrade takes one stack away");
        before = friend.Gold;
        client.Game.Shop(ShopAction.Restock);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(friend.Restocks == 1 && friend.Gold == before - 25 && friend.ShelfSold == 0 && Game.RestockPrice(friend) == 50, "restocking rolls a new shelf; the next restock costs more");

        // Giving gold, and giving to someone who's done reopens their shop.
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(friend.ShopDone && g.ShopOpen, "one player done: the shop stays open for the others");
        before = friend.Gold;
        g.Shop(ShopAction.Give, 2, 50);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(friend.Gold == before + 50 && me.Gold == 350 && !friend.ShopDone && client.Game.ShopNotices.Exists(n => n.Text.Contains("gave you 50")),
            "giving gold moves it, tells them, and reopens a done player's shop");

        // Everyone done: the shop closes and leftover gold becomes each player's own XP.
        Func<Player, float> TotalXp = p => { float t = p.Xp; for (int l = 1; l < p.Level; l++) t += Defs.XpToNext(l); return t; };
        float xpMe = TotalXp(me), xpFriend = TotalXp(friend);
        int goldMe = me.Gold, goldFriend = friend.Gold;
        g.Shop(ShopAction.Close);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(g.ShopOpen, "the host can't close the shop early");
        g.Shop(ShopAction.Done, 1);
        client.Game.Shop(ShopAction.Done, 1);
        Run(s, dt, 0.5f);
        Console.WriteLine("  bazaar: leftover gold {0} and {1} -> XP {2:0} and {3:0}", goldMe, goldFriend, TotalXp(me) - xpMe, TotalXp(friend) - xpFriend);
        Check(!g.ShopOpen && !client.Game.ShopOpen && me.Gold == 0 && friend.Gold == 0
            && Math.Abs(TotalXp(me) - xpMe - goldMe * Game.GoldToXp) < 0.5f && Math.Abs(TotalXp(friend) - xpFriend - goldFriend * Game.GoldToXp) < 0.5f,
            "when everyone is done the shop closes and leftover gold becomes each player's own XP");
        NoPicks();
        timer = g.StateTimer;
        Run(s, dt, 0.5f, null, NoPicks);
        Check(g.StateTimer < timer || g.State == MatchState.Fighting, "then the countdown to the boss goes on");

        // The host can close a stalled shop after a minute.
        openShop.Invoke(g, null);
        g.ShopT = Game.ShopHostCloseAfter + 1f;
        client.Game.Shop(ShopAction.Close);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(g.ShopOpen, "only the host can close the shop");
        g.Shop(ShopAction.Close);
        Run(s, dt, 0.3f, null, NoPicks);
        Check(!g.ShopOpen && !client.Game.ShopOpen, "after a minute the host can close the shop for everyone");

        // A revive token gets you back up by yourself, and keeps the run going if you were the last one standing.
        friend.ReviveToken = true;
        friend.Downed = true; friend.Hp = 0f;
        me.Downed = true; me.Hp = 0f;
        Run(s, dt, 2.5f, null, NoPicks);
        Check(!friend.Downed && !friend.ReviveToken && g.State != MatchState.GameOver, "a revive token gets you up on your own (and it isn't game over meanwhile)");
        host.Leave(""); client.Leave("");

        // A bot shops by itself and finishes, so automated runs never stall.
        port = nextPort++;
        host = new Session { AutoStart = 1, StartWave = 5 };
        host.Host(port, "Bot", Role.Dps);
        g = host.Game; g.Bot = true;
        host.StartMatch();
        g.DevSetGod(1, true);
        for (int f = 0; f < 60 * 20 && !(g.State == MatchState.Fighting && g.Enemies.Count > 0); f++) Step(host, dt);
        g.CurrentPath = PathId.Bazaar; g.PathFrom = 6; g.PathTo = 9; g.Wave = 9;
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        g.Players[1].Gold = 300;
        int taken = g.Players[1].Taken.Count;
        cleared.Invoke(g, null);
        for (int f = 0; f < 60 * 15 && g.State != MatchState.Fighting; f++) Step(host, dt);
        Check(g.State == MatchState.Fighting && !g.ShopOpen && g.Players[1].Taken.Count > taken, "a bot buys something, finishes shopping, and the run carries on");
        host.Leave("");
    }
}

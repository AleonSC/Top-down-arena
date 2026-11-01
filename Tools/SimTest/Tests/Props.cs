using System;
using System.Collections;
using System.Linq;
using System.Threading;
using Arena;

// The arena's props (Props.cs): crates as cover, pots, barrels, and the treasure chest with an item for each player.
static partial class Program
{
    static void PropsTest(float dt)
    {
        Game.PropsByDefault = true;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var spawnBullet = typeof(Game).GetMethod("SpawnBullet", flags);
        var spawnEnemy = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var waveProps = typeof(Game).GetMethod("WaveProps", flags);
        int port = nextPort++;
        var host = new Session { AutoStart = 2, StartWave = 9 };
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        var s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        var g = host.Game;
        var cg = client.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action Quiet = () =>
        {
            ((IList)typeof(Game).GetField("spawnQueue", flags).GetValue(g)).Clear();
            ((IList)typeof(Game).GetField("squadQueue", flags).GetValue(g)).Clear();
            foreach (var e in g.Enemies.Values) e.Hp = 0f;
            foreach (var b in g.Bullets.Values.Where(b => Defs.IsEnemyShot(b.Kind)).ToList()) g.Bullets.Remove(b.Id);
        };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 9, NoPicks);
        Run(s, dt, 0.5f);

        // A regular wave brings crates (more as the waves go on), pots, and everyone sees them.
        int crates = g.Props.Values.Count(p => Game.IsCrate(p)), pots = g.Props.Values.Count(p => p.Kind == PropKind.Pot);
        Console.WriteLine("  props at wave 9: {0} crates (target {1}), {2} pots; wave 1 target {3} crates, wave 30 {4}",
            crates, Game.CrateTarget(9), pots, Game.CrateTarget(1), Game.CrateTarget(30));
        Check(crates >= 3 && pots >= 1 && cg.Props.Count == g.Props.Count && Game.CrateTarget(30) > Game.CrateTarget(9) && Game.CrateTarget(9) > Game.CrateTarget(1),
            "regular waves bring crates and pots (more crates as the waves go on), seen on the client");
        bool clearOfPlayers = g.Props.Values.All(p => g.Players.Values.All(pl => pl.Pos.DistanceTo(p.Pos) > 100f));
        Check(clearOfPlayers, "props never land on top of a player");

        // Clear the field for the checks below.
        Quiet();
        g.Props.Clear();
        Run(s, dt, 0.4f, null, Quiet);
        var me = g.Players[1]; var friend = g.Players[2];
        me.Pos = new V2(200f, 200f); friend.Pos = new V2(1400f, 200f); cg.Players[2].Pos = friend.Pos;

        // Cover: an enemy shot stops at a crate and wears it; your shot flies over it.
        var crate = g.AddProp(PropKind.Crate, new V2(800f, 450f));
        float hp0 = crate.Hp;
        var foe = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.SmallShot, new V2(800f, 300f), new V2(0f, 300f), 0f, 3f, 10f, 0, false, 0f });
        var mine = (Bullet)spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(800f, 380f), new V2(0f, 400f), 0f, 1f, 10f, 1, false, 0f });
        bool mineOver = false;
        Run(s, dt, 0.8f, null, () => { if (g.Bullets.ContainsKey(mine.Id) && mine.Pos.Y > 480f) mineOver = true; });
        Check(!g.Bullets.ContainsKey(foe.Id) && crate.Hp == hp0 - 1f && mineOver, "a crate stops enemy shots (and wears down), while your shots fly over it");
        crate.Hp = 1f;
        spawnBullet.Invoke(g, new object[] { Defs.SmallShot, new V2(800f, 300f), new V2(0f, 300f), 0f, 3f, 10f, 0, false, 0f });
        Run(s, dt, 0.8f);
        Check(!g.Props.ContainsKey(crate.Id) && !cg.Props.ContainsKey(crate.Id), "enough enemy hits break it (gone for everyone)");

        // Solid: you can't walk into a crate.
        crate = g.AddProp(PropKind.Crate, new V2(800f, 450f));
        Run(s, dt, 0.2f);
        me.Pos = new V2(805f, 455f);
        Run(s, dt, 0.1f);
        Check(!Game.PropCovers(crate, me.Pos, me.Def.HitR), "you're pushed out of a crate you walk into");
        g.Props.Clear();

        // Pots smash when you walk through them.
        var pot = g.AddProp(PropKind.Pot, new V2(600f, 600f));
        Run(s, dt, 0.2f);
        me.Pos = pot.Pos;
        Run(s, dt, 0.2f);
        Check(!g.Props.ContainsKey(pot.Id), "walking through a pot smashes it");

        // A barrel: shoot it and it blows, hurting enemies around it and smashing crates; other barrels go off too.
        var barrel = g.AddProp(PropKind.Barrel, new V2(1000f, 600f));
        var barrel2 = g.AddProp(PropKind.Barrel, new V2(1090f, 600f));
        var near = g.AddProp(PropKind.Crate, new V2(1000f, 690f));
        var grunt = (Enemy)spawnEnemy.Invoke(g, new object[] { EnemyType.Brute, new V2(960f, 560f), 0f, 0, false });
        grunt.SpawnT = 0f; grunt.Hp = grunt.MaxHp = 5000f; grunt.Speed = 0f;
        me.Pos = new V2(1000f, 200f);
        spawnBullet.Invoke(g, new object[] { Defs.DpsShot, new V2(1000f, 520f), new V2(0f, 400f), 0f, 1f, 10f, 1, false, 0f });
        float taken0 = me.Run.DamageTaken;
        Run(s, dt, 1f, null, () => { grunt.FireT = 5f; });
        Check(!g.Props.ContainsKey(barrel.Id) && !g.Props.ContainsKey(barrel2.Id) && !g.Props.ContainsKey(near.Id) && grunt.Hp < 5000f - Game.BarrelDamage(g.Wave) * 1.5f,
            "a shot barrel blows up: it hurts enemies, sets off the barrel beside it and smashes a crate (" + (5000f - grunt.Hp) + " to the brute)");
        Check(me.Run.DamageTaken == taken0, "players out of reach aren't hurt");
        grunt.Hp = 0f;
        Run(s, dt, 0.2f, null, Quiet);

        // The chest: the wave waits for it, anyone down gets up, touching it opens it with an item for each player.
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Enemies.Count > 0, NoPicks);  // the next wave
        g.Props.Clear();
        me.Pos = new V2(200f, 200f); friend.Pos = new V2(1400f, 200f); cg.Players[2].Pos = friend.Pos;
        var chest = g.AddProp(PropKind.Chest, new V2(800f, 450f));
        Quiet();
        Run(s, dt, 1.5f, null, Quiet);
        Check(g.State == MatchState.Fighting && g.PropsHoldWave(), "with the enemies gone, an unopened chest holds the wave");
        friend.Downed = true; friend.Hp = 0f;
        Run(s, dt, 0.3f);
        Check(!friend.Downed, "while the wave waits for treasure, anyone down gets back up to go and collect");
        cg.Players[2].Pos = chest.Pos + new V2(0f, Game.ChestH / 2f + 10f);
        Run(s, dt, 0.5f);
        var hostLoot = g.Loots.Values.FirstOrDefault(l => l.Owner == 1);
        var friendLoot = g.Loots.Values.FirstOrDefault(l => l.Owner == 2);
        Check(chest.Open && hostLoot != null && friendLoot != null && cg.MyLoot != null && cg.MyLoot.Id == friendLoot.Id,
            "the friend touches the chest: it opens with an item for each player (" + (hostLoot != null ? Upgrades.All[hostLoot.Upgrade].Name : "-") + " / " + (friendLoot != null ? Upgrades.All[friendLoot.Upgrade].Name : "-") + ")");
        Check(hostLoot != null && Upgrades.All[hostLoot.Upgrade].Rarity >= Rarity.Uncommon && Upgrades.All[hostLoot.Upgrade].Rarity != Rarity.Mod, "a chest item is an uncommon or better card, or a legendary");
        Run(s, dt, 3f);
        Check(!g.Props.ContainsKey(chest.Id) && g.State == MatchState.Fighting, "the open chest goes after a moment; the items stay and the wave still waits");

        // Taking yours (host) and discarding yours (client, through the host).
        int had = me.Taken.Count;
        me.Pos = hostLoot.Pos;
        Run(s, dt, 0.1f);
        g.ResolveLoot(true);
        Run(s, dt, 0.3f);
        Check(me.Taken.Count == had + 1 && me.Taken.Last() == hostLoot.Upgrade && !g.Loots.ContainsKey(hostLoot.Id), "standing on your item and taking it gives you the upgrade");
        int friendHad = friend.Taken.Count;
        cg.Players[2].Pos = friendLoot.Pos;
        Run(s, dt, 0.3f);
        cg.ResolveLoot(false);
        Run(s, dt, 0.5f, null, NoPicks);
        Check(friend.Taken.Count == friendHad && !g.Loots.ContainsKey(friendLoot.Id) && cg.Loots.Count == 0, "discarding it (from the client) throws it away");
        Run(s, dt, 0.5f, null, NoPicks);
        Check(g.State == MatchState.Intermission, "with everything dealt with, the wave clears");

        // Bosses get a clear arena.
        g.AddProp(PropKind.Crate, new V2(300f, 700f));
        g.Wave = 10;
        waveProps.Invoke(g, null);
        Check(g.Props.Count == 0, "a boss wave clears the props away");
        host.Leave(""); client.Leave("");

        // Bots open the chest and take their items, so the wave can't stall.
        port = nextPort++;
        host = new Session { AutoStart = 2, StartWave = 3 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Dps);
        client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Healer);
        s = new[] { host, client };
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        g = host.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 3);
        Quiet();
        g.AddProp(PropKind.Chest, new V2(800f, 300f));
        int takenBefore = g.Players.Values.Sum(p => p.Taken.Count);
        Run(s, dt, 25f, () => g.State == MatchState.Intermission, () => { if (g.State == MatchState.Fighting) Quiet(); });
        Check(g.State == MatchState.Intermission && g.Loots.Count == 0 && g.Players.Values.Sum(p => p.Taken.Count) >= takenBefore + 2,
            "bots open the chest and take their items, and the wave goes on");
        host.Leave(""); client.Leave("");
    }
}

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
        var host = HostAs(new Session { AutoStart = 2, StartWave = 9 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
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

        // Cluttered: a lot more crates, growing by wave, still clear of the players.
        Check(Game.ClutterTarget(9) >= 3 * Game.CrateTarget(9) && Game.ClutterTarget(20) > Game.ClutterTarget(9),
            "Cluttered: many more crates than a normal wave, and more each wave");
        foreach (var id in g.Props.Keys.ToList()) g.Props.Remove(id);
        g.Mods |= RunMod.Cluttered;
        waveProps.Invoke(g, null);
        int cluttered = g.Props.Values.Count(p => Game.IsCrate(p));
        g.Mods &= ~RunMod.Cluttered;
        Console.WriteLine("  Cluttered wave 9: {0} crates (target {1})", cluttered, Game.ClutterTarget(9));
        Check(cluttered >= Game.ClutterTarget(9) * 3 / 4 && g.Props.Values.All(p => g.Players.Values.All(pl => pl.Pos.DistanceTo(p.Pos) > 100f)),
            "Cluttered: the wave fills the arena with crates, none on a player");

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

        // In a boss fight, health barrels drop in now and then; walking into one breaks it into health orbs.
        Quiet();
        g.State = MatchState.Fighting;
        var queen = (Enemy)spawnEnemy.Invoke(g, new object[] { EnemyType.HiveQueen, new V2(800f, 450f), 0f, 0, false });
        queen.SpawnT = 0f;
        me.Pos = new V2(200f, 200f); friend.Pos = new V2(1400f, 200f); cg.Players[2].Pos = friend.Pos;
        var timer = typeof(Game).GetField("bossBarrelT", flags);
        timer.SetValue(g, 0.05f);
        Run(s, dt, 0.3f, null, () => { queen.FireT = 5f; });
        var hb = g.Props.Values.FirstOrDefault(p => p.Kind == PropKind.HealthBarrel);
        float next = (float)timer.GetValue(g);
        Check(hb != null && cg.Props.Values.Any(p => p.Kind == PropKind.HealthBarrel) && hb.Pos.DistanceTo(queen.Pos) > queen.Radius + 100f && next >= 15f,
            "during a boss fight a health barrel drops in away from the boss (seen on the client), the next one 18-26 s later");
        int orbs0 = g.Orbs.Count;
        me.Hp = me.MaxHp * 0.5f;
        me.Pos = hb.Pos;
        Run(s, dt, 0.2f, null, () => { queen.FireT = 5f; });
        float healed = me.Hp - me.MaxHp * 0.5f;
        Console.WriteLine("  health barrel: broke {0}; orbs {1} -> {2}, healed {3:0.0}", !g.Props.ContainsKey(hb.Id), orbs0, g.Orbs.Count, healed);
        Check(!g.Props.ContainsKey(hb.Id) && healed >= Game.OrbHeal * 2f - 0.5f, "walking into it breaks it into health orbs (two, which you pick up)");
        host.Leave(""); client.Leave("");

        // Pushing: crates and barrels slide when leaned on. Tanks shove fastest, then Dps, then Healers; enemies barely.
        port = nextPort++;
        host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Host", Role.Tank);
        client = JoinAs(port, false, "Friend", Role.Dps);
        s = new[] { host, client };
        WaitForGame(host, client, dt);
        Run(s, dt, 20f, () => host.Game.State == MatchState.Fighting && host.Game.Wave == 3);
        g = host.Game;
        g.Props.Clear();
        Func<PropKind, float, float> slide = (kind, speed) =>
        {
            var pr = g.AddProp(kind, new V2(Game.Width / 2f, Game.Height / 2f));
            V2 start = pr.Pos;
            for (int i = 0; i < 60; i++)  // a second of leaning in from the left, 4 px deep each step
                g.ShoveProps(pr.Pos - new V2(Game.HalfW(pr) + 5f - 4f, 0f), 5f, speed, 1f / 60f, 0f, V2.Zero);
            float d = pr.Pos.X - start.X;
            g.Props.Remove(pr.Id);
            return d;
        };
        float tank = slide(PropKind.Crate, Game.PushTank), dps = slide(PropKind.Crate, Game.PushDps), healer = slide(PropKind.Crate, Game.PushHealer);
        float foeSlide = slide(PropKind.Crate, Game.PushEnemy), heavy = slide(PropKind.HeavyCrate, Game.PushTank), barrelSlide = slide(PropKind.Barrel, Game.PushTank);
        Console.WriteLine("  pushed in 1 s: tank {0:0.0}, dps {1:0.0}, healer {2:0.0}, enemy {3:0.0}, heavy crate (tank) {4:0.0}, barrelSlide (tank) {5:0.0}", tank, dps, healer, foeSlide, heavy, barrelSlide);
        Check(tank > dps && dps > healer && healer > 5f * foeSlide && foeSlide > 0.5f, "tanks shove props fastest, then dps, then healers; healers are far faster than enemies, who still move them");
        Check(heavy > 0f && heavy < tank && barrelSlide > 0f, "a heavy crate gives more slowly; barrels slide too");
        var pushChest = g.AddProp(PropKind.Chest, new V2(Game.Width / 2f, Game.Height / 2f));
        V2 chestAt = pushChest.Pos;
        g.ShoveProps(pushChest.Pos - new V2(30f, 0f), 5f, 100f, 1f, 0f, V2.Zero);
        Check(pushChest.Pos.DistanceTo(chestAt) < 0.01f, "chests don't slide");
        g.Props.Remove(pushChest.Id);
        var edge = g.AddProp(PropKind.Crate, new V2(g.ArenaMax.X - Game.CrateSize / 2f - 1f, Game.Height / 2f));
        for (int i = 0; i < 120; i++)
            g.ShoveProps(edge.Pos - new V2(Game.HalfW(edge) + 1f, 0f), 5f, Game.PushTank, 1f / 60f, 0f, V2.Zero);
        Check(edge.Pos.X <= g.ArenaMax.X - Game.CrateSize / 2f + 0.01f, "a pushed crate stops at the arena's edge");
        g.Props.Remove(edge.Id);
        var c1 = g.AddProp(PropKind.Crate, new V2(Game.Width / 2f, Game.Height / 2f));
        var c2 = g.AddProp(PropKind.Crate, new V2(Game.Width / 2f + Game.CrateSize, Game.Height / 2f));
        for (int i = 0; i < 120; i++)
            g.ShoveProps(c1.Pos - new V2(Game.HalfW(c1) + 1f, 0f), 5f, Game.PushTank, 1f / 60f, 0f, V2.Zero);
        Check(c1.Pos.X <= c2.Pos.X - Game.CrateSize + 0.6f, "a pushed crate can't be shoved through another");
        // Enemies walk round crates when there's a way, and only shove when boxed in.
        var propDir = typeof(Game).GetMethod("PropDir", flags);
        g.Props.Clear();
        var foeA = new Enemy { Id = 2, Type = EnemyType.Grunt, Pos = new V2(Game.Width / 2f, Game.Height / 2f) };
        foeA.Def = Defs.Get(EnemyType.Grunt);
        V2 ahead = new V2(1f, 0f);
        g.AddProp(PropKind.Crate, foeA.Pos + new V2(45f, 0f));
        V2 turned = (V2)propDir.Invoke(g, new object[] { foeA, ahead });
        Check(turned.DistanceTo(ahead) > 0.3f, "an enemy with a crate ahead turns to go round it");
        foreach (var id in g.Props.Keys.ToList()) g.Props.Remove(id);
        for (int i = 0; i < 12; i++)
            g.AddProp(PropKind.Crate, foeA.Pos + V2.FromAngle(i * MathX.Pi * 2f / 12f) * 60f);
        V2 boxed = (V2)propDir.Invoke(g, new object[] { foeA, ahead });
        Check(boxed.DistanceTo(ahead) < 0.001f, "boxed in by crates, it walks straight on (and shoves)");
        g.Props.Clear();
        host.Leave(""); client.Leave("");

        // Bots open the pushChest and take their items, so the wave can't stall.
        port = nextPort++;
        host = new Session { AutoStart = 2, StartWave = 3 };
        host.Game.Bot = true;
        host.Host(port, "Host", Role.Dps);
        client = new Session();
        client.Game.Bot = true;
        client.Join("127.0.0.1", port, "Friend", Role.Healer);
        s = new[] { host, client };
        WaitForGame(host, client, dt);
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

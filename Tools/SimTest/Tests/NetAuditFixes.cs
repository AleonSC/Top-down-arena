using System;
using System.Collections.Generic;
using Arena;

// The November 2026 second audit's network fixes: more than 255 zones in a snapshot, a pause keeping each shot's look and size,
// and a player who leaves vanishing from the other screens.
static partial class Program
{
    static void NetAuditFixesTest(float dt)
    {
        var s = StartBuffed(dt, 1, 0);
        var host = s[0].Game;
        var client = s[1].Game;
        foreach (var p in host.Players.Values)
            p.InvulnT = 999f;

        // 1. 300 zones (two Hemomancers' Blood Walls can lay that many): the count went out in one byte.
        for (int i = 0; i < 300; i++)
            host.Zones[900000 + i] = new Zone { Id = 900000 + i, Kind = ZoneKind.Smoke, Pos = new V2(200f + i, 300f), Radius = 20f, T = 999f, Duration = 999f };
        Run(s, dt, 1f);
        int seen = 0;
        foreach (var z in client.Zones.Values)
            if (z.Id >= 900000)
                seen++;
        Console.WriteLine("  client sees {0} of 300 zones (host {2}); client still in the game: {1}", seen, client.Running, host.Zones.Count);
        Check(seen == 300 && client.Running, "more than 255 zones reach the client and the snapshot still reads");
        foreach (var z in new List<int>(host.Zones.Keys))
            if (z >= 900000)
                host.Zones.Remove(z);

        // 2. A pause resends every shot: a dart keeps its look, and a bigger shot its size.
        var b = new Bullet { Id = 990001, Kind = Defs.SmallShot, Pos = new V2(800f, 450f), Vel = V2.Zero, Life = 30f, Life0 = 30f, Radius = 17f, Look = ShotLook.Dart };
        host.Bullets[b.Id] = b;
        host.RequestPause(true);
        Run(s, dt, 0.5f);
        Bullet cb;
        bool kept = client.Bullets.TryGetValue(b.Id, out cb) && cb.Look == ShotLook.Dart && Math.Abs(cb.Radius - 17f) < 0.01f;
        Check(kept, "after a pause a client's shots keep their look (dart, crescent) and size");
        host.RequestPause(false);
        s[0].Leave(""); s[1].Leave("");

        // 3. Three players; one leaves: they're gone from the other client's screen too.
        int port = nextPort++;
        var h = HostAs(new Session { AutoStart = 3, StartWave = 1 }, port, true, "Host", Role.Tank);
        var a = JoinAs(port, true, "A", Role.Dps);
        var c = JoinAs(port, true, "C", Role.Healer);
        var all = new[] { h, a, c };
        for (int i = 0; i < 600 && (a.Screen != AppScreen.Game || c.Screen != AppScreen.Game); i++)
        {
            foreach (var x in all) Step(x, dt);
            Nap(2);
        }
        bool three = a.Game.Players.Count == 3;
        int leftId = c.Game.MyId;
        c.Leave("");
        Run(new[] { h, a }, dt, 2f);
        Console.WriteLine("  three joined: {0}; after one left the host has {1} players, the other client {2}", three, h.Game.Players.Count, a.Game.Players.Count);
        Check(three && !a.Game.Players.ContainsKey(leftId) && a.Game.Players.Count == 2, "a player who leaves mid-run vanishes from the other clients too");
        h.Leave(""); a.Leave("");
    }
}

using System.Linq;
using System;
using Arena;

// The Dodge: a tiny dash with invulnerability. The host's own player and a client's both move about DodgeDist, the host
// sees the client invulnerable, the cooldown blocks a second press, and with nothing held it steps back from the aim.
static partial class Program
{
    static void DodgeTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2 }, port, false, "Host", Role.Tank, 0, 0);
        var client = JoinAs(port, false, "Friend", Role.Dps, 0);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        g.Bot = client.Game.Bot = false;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        foreach (var e in g.Enemies.Values) e.Hp = 0f;
        var hostMe = g.Players[1];
        var cliMe = client.Game.Me;
        Action<float, Func<int, PlayerInput>, Func<int, PlayerInput>> play = (secs, hi, ci) =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var e in g.Enemies.Values) e.Hp = 0f;
                host.Poll(); g.FixedStep(dt, hi(f)); g.Update(dt);
                client.Poll(); client.Game.FixedStep(dt, ci(f)); client.Game.Update(dt);
            }
        };
        Func<int, PlayerInput> idle = f => new PlayerInput { AimAt = new V2(800f, 450f) };

        // The host's player dodges to the right.
        hostMe.Pos = new V2(300f, 300f); cliMe.Pos = new V2(300f, 700f);
        play(0.1f, idle, idle);
        V2 h0 = hostMe.Pos;
        play(0.4f, f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, AimAt = new V2(800f, 450f) }, idle);
        float walked = 0.4f * hostMe.Speed;
        float moved = hostMe.Pos.X - h0.X;
        Console.WriteLine("  dodge: host moved {0:0} px (walking alone would be {1:0})", moved, walked);
        Check(moved > walked + Game.DodgeDist * 0.6f, "the host's dodge carries the player about " + Game.DodgeDist + " px");
        hostMe.InvulnT = 0f;

        // A client dodges up; the host sees it invulnerable at once and the cooldown stops a second press.
        cliMe.Pos = new V2(500f, 600f); play(0.1f, idle, idle);
        V2 c0 = cliMe.Pos;
        float maxInv = 0f;
        for (int f = 0; f < 0.3f / dt; f++)
        {
            host.Poll(); g.FixedStep(dt, idle(f)); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput { Move = new V2(0f, -1f), Dodge = f == 0, AimAt = new V2(800f, 450f) }); client.Game.Update(dt);
            maxInv = Math.Max(maxInv, g.Players[2].InvulnT);
        }
        float up = c0.Y - cliMe.Pos.Y;
        Console.WriteLine("  dodge: client moved {0:0} px up, the host saw {1:0.00} s of invulnerability", up, maxInv);
        Check(up > Game.DodgeDist * 0.8f, "a client's dodge moves it");
        Check(maxInv > 0.1f, "the host makes a dodging client invulnerable");
        V2 p1 = cliMe.Pos;
        play(0.2f, idle, f => new PlayerInput { Dodge = true, AimAt = new V2(800f, 450f) });
        Check(cliMe.Pos.DistanceTo(p1) < 5f, "a second press during the cooldown does nothing");

        // Nothing held: a step back from the aim.
        play(Game.DodgeCd, idle, idle);
        hostMe.Pos = new V2(400f, 450f);
        play(0.05f, idle, idle);
        V2 b0 = hostMe.Pos;
        play(0.3f, f => new PlayerInput { Dodge = f == 0, AimAt = new V2(800f, 450f) }, idle);
        Check(hostMe.Pos.X < b0.X - Game.DodgeDist * 0.7f, "with no direction held the dodge steps back from the aim");
        s[0].Leave(""); s[1].Leave("");
    }
}

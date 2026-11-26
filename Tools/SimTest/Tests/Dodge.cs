using System.Linq;
using System;
using Arena;

// The Dodge: a tiny dash with invulnerability. The host's own player and a client's both move about DodgeDist, the host
// sees the client invulnerable, the cooldown blocks a second press, with nothing held it steps back from the aim,
// holding the key on after the dodge sprints (a tap doesn't), and a dodge into a crate kicks off it.
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

        // Sprint: a tap gives none; holding the key on after the dodge does, faster than walking and fading away.
        play(Game.DodgeCd, idle, idle);
        hostMe.Pos = new V2(300f, 450f); play(0.05f, idle, idle);
        play(0.5f, f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, DodgeHeld = f < 3, AimAt = new V2(800f, 450f) }, idle);
        Check(g.SprintT <= 0f, "a tapped dodge doesn't sprint");
        play(Game.DodgeCd, idle, idle);
        hostMe.Pos = new V2(300f, 450f); play(0.05f, idle, idle);
        play(0.3f, f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, DodgeHeld = true, AimAt = new V2(800f, 450f) }, idle);
        Check(g.SprintT > 0f && g.SprintMult > 1.4f, "holding the dodge key starts a sprint (x" + g.SprintMult.ToString("0.00") + ")");
        V2 r0 = hostMe.Pos;
        play(0.3f, f => new PlayerInput { Move = new V2(1f, 0f), AimAt = new V2(800f, 450f) }, idle);
        float sprinted = hostMe.Pos.X - r0.X;
        Console.WriteLine("  sprint: {0:0} px in 0.3 s (walking {1:0})", sprinted, 0.3f * hostMe.Speed);
        Check(sprinted > 0.3f * hostMe.Speed * 1.3f, "the sprint moves you faster than walking, even after letting go");
        float m1 = g.SprintMult;
        play(1f, idle, idle);
        Check(g.SprintMult < m1, "the sprint fades");
        play(Game.SprintTime, idle, idle);
        Check(g.SprintT <= 0f && g.SprintMult == 1f, "and ends after " + Game.SprintTime + " s");

        // Everyone sees the sprint (Player.Sprint): the client's reaches the host, the host's reaches the client.
        play(Game.DodgeCd, idle, idle);
        cliMe.Pos = new V2(300f, 700f); hostMe.Pos = new V2(300f, 300f); play(0.05f, idle, idle);
        Func<int, PlayerInput> sprintRight = f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, DodgeHeld = true, AimAt = new V2(800f, 450f) };
        play(0.4f, sprintRight, sprintRight);
        byte seenCli = g.Players[2].Sprint, seenHost = client.Game.Players[1].Sprint;
        Console.WriteLine("  sprint seen: host sees the client at {0}, the client sees the host at {1} (of 255)", seenCli, seenHost);
        Check(seenCli > 150 && seenHost > 150, "each sees the other's sprint");
        play(Game.SprintTime, idle, idle);
        Check(g.Players[2].Sprint == 0 && client.Game.Players[1].Sprint == 0, "and its end");

        // Wall kick: a client dodges into a crate, bounces back off it, and gets one free dodge the host lets in.
        play(Game.DodgeCd, idle, idle);
        g.AddProp(PropKind.Crate, new V2(700f, 450f));
        cliMe.Pos = new V2(620f, 450f); hostMe.Pos = new V2(300f, 200f);
        play(0.2f, idle, idle);
        int kicks0 = client.Game.WallKicks;
        float peak = 0f;
        play(0.25f, idle, f => { peak = Math.Max(peak, cliMe.Pos.X); return new PlayerInput { Dodge = f == 0, Move = f == 0 ? new V2(1f, 0f) : V2.Zero, AimAt = new V2(800f, 450f) }; });
        Console.WriteLine("  wall kick: {0} kick(s), the client bounced {1:0} px back off the crate", client.Game.WallKicks - kicks0, peak - cliMe.Pos.X);
        Check(client.Game.WallKicks == kicks0 + 1, "a dodge into a crate kicks off it");
        Check(cliMe.Pos.X < peak - 30f, "and bounces back the way it came");
        g.Players[2].InvulnT = 0f;
        float inv2 = 0f; V2 k1 = cliMe.Pos;
        int kicks1 = client.Game.WallKicks;
        for (int f = 0; f < 0.25f / dt; f++)  // the free dodge, straight back into the crate
        {
            host.Poll(); g.FixedStep(dt, idle(f)); g.Update(dt);
            client.Poll(); client.Game.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, AimAt = new V2(800f, 450f) }); client.Game.Update(dt);
            inv2 = Math.Max(inv2, g.Players[2].InvulnT);
        }
        Console.WriteLine("  wall kick: the free dodge went {0:0} px, the host saw {1:0.00} s of invulnerability", cliMe.Pos.X - k1.X, inv2);
        Check(cliMe.Pos.X > k1.X + 20f, "the kick gives the cooldown back: a dodge at once");
        Check(inv2 > 0.1f, "and the host lets the free dodge in");
        Check(client.Game.WallKicks == kicks1, "the free dodge doesn't kick again");

        // Kick reach: a dodge that would end short of the crate (but within KickReach) runs on to it and kicks.
        play(Game.DodgeCd + 0.2f, idle, idle);
        float face = 700f - Game.HalfW(PropKind.Crate) - cliMe.Def.HitR - 5f;  // where a push-out stops you
        cliMe.Pos = new V2(face - Game.DodgeDist - Game.KickReach * 0.7f, 450f);
        play(0.2f, idle, idle);
        float start = cliMe.Pos.X;
        int kicks2 = client.Game.WallKicks;
        float peak2 = 0f;
        play(0.35f, idle, f => { peak2 = Math.Max(peak2, cliMe.Pos.X); return new PlayerInput { Dodge = f == 0, Move = f == 0 ? new V2(1f, 0f) : V2.Zero, AimAt = new V2(800f, 450f) }; });
        Console.WriteLine("  kick reach: from {0:0} px off the crate, {1} kick(s), kicked back {2:0} px", face - start, client.Game.WallKicks - kicks2, peak2 - cliMe.Pos.X);
        Check(client.Game.WallKicks == kicks2 + 1, "a dodge ending just short of a crate reaches on and kicks");
        Check(peak2 - cliMe.Pos.X > Game.DodgeDist * 1.2f, "and the kickoff carries further than a dodge");
        // The Ninja: a shorter dodge cooldown, and a faster, longer sprint.
        hostMe.Kit = Kit.Ninja;
        play(Game.DodgeCd, idle, idle);
        hostMe.Pos = new V2(300f, 450f); play(0.05f, idle, idle);
        play(0.3f, f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0, DodgeHeld = true, AimAt = new V2(800f, 450f) }, idle);
        float nCd = Defs.KitDodgeCd(Kit.Ninja), nLen = Defs.KitSprintTime(Kit.Ninja);
        Console.WriteLine("  ninja: dodge cooldown {0:0.00} s, sprint x{1:0.00} for {2:0.0} s", nCd, g.SprintMult, nLen);
        Check(nCd < Game.DodgeCd && nLen > Game.SprintTime && g.SprintMult > 1f + Game.SprintBoost + 0.1f, "a Ninja's dodge comes back sooner and its sprint is faster");
        play(Game.SprintTime + 0.2f - 0.3f, idle, idle);
        Check(g.SprintT > 0f, "and lasts longer");
        play(nLen - Game.SprintTime + 0.1f, idle, idle);
        Check(g.SprintT <= 0f, "and still ends");
        hostMe.Pos = new V2(300f, 450f); play(0.05f, idle, idle);
        int d0 = g.Dodges;
        play(nCd + 0.05f, f => new PlayerInput { Move = new V2(1f, 0f), Dodge = f == 0 || f == (int)((nCd + 0.02f) / dt), AimAt = new V2(800f, 450f) }, idle);
        Check(g.Dodges == d0 + 2, "a Ninja dodges again after " + nCd.ToString("0.00") + " s");
        hostMe.Kit = Kit.Soldier;
        s[0].Leave(""); s[1].Leave("");
    }
}

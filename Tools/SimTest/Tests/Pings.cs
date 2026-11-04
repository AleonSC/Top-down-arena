using System;
using System.Threading;
using Arena;

// Pings (Core/Pings.cs): a player marks a spot in a match, the host keeps the pause and the cap, a Focus ping snaps to an enemy
// and follows it, and they lapse.
static partial class Program
{
    static void PingTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        WaitForLobby(host, client);
        PollBoth(host, client, 40);
        Check(!client.SendPing(PingKind.Here, new V2(100f, 100f)), "no pinging in the lobby");
        host.RequestReady(true);
        client.RequestReady(true);
        PollBoth(host, client, 40);
        host.StartMatch();
        for (int i = 0; i < 40; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        int cid = client.MyId;

        Check(client.SendPing(PingKind.Danger, new V2(400f, 300f)), "a client can ping in a match");
        for (int i = 0; i < 40; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        V2 at; float age;
        Check(host.Pings.Count == 1 && client.Pings.Count == 1 && host.PingNow(0, out at, out age) && client.PingNow(0, out at, out age) && (at - new V2(400f, 300f)).Length < 0.01f && client.Pings[0].Owner == cid && client.Pings[0].Kind == PingKind.Danger,
            "the ping shows on the host and on the client, with its owner and kind");
        Check(!client.SendPing(PingKind.Here, new V2(50f, 50f)) && client.PingWait > 0f && client.PingWait <= Session.PingPause, "there's a short pause before the next one");

        // The cap: a fourth replaces the oldest.
        for (int n = 0; n < 4; n++)
        {
            Thread.Sleep((int)(Session.PingPause * 1000f) + 60);
            Check(host.SendPing(PingKind.Here, new V2(100f + n * 10f, 200f)), "the host pings too");
        }
        PollBoth(host, client, 40);
        int mine = 0;
        foreach (var p in client.Pings)
            if (p.Owner == 1) mine++;
        Check(mine == Session.PingMax && host.Pings.Count == client.Pings.Count, "three live pings each at most, the oldest goes");

        // The host refuses a nonsense position, or a ping spammed past the pause.
        Thread.Sleep(500);
        int before = host.Pings.Count;
        Check(!client.SendPing(PingKind.Here, new V2(float.NaN, 5f)), "a nonsense position is never sent");
        Check(!host.SendPing((PingKind)9, new V2(5f, 5f)), "an unknown kind is never sent");

        // Focus snaps to the nearest enemy and follows it until it dies.
        var e = new Enemy { Id = 9001, Pos = new V2(800f, 450f), Disp = new V2(800f, 450f), Hp = 10f, MaxHp = 10f, Radius = 12f };
        host.Game.Enemies[e.Id] = e;
        client.Game.Enemies[e.Id] = new Enemy { Id = e.Id, Pos = e.Pos, Disp = e.Pos, Hp = 10f, MaxHp = 10f, Radius = 12f };
        Check(client.SendPing(PingKind.Focus, new V2(830f, 450f)), "a Focus ping goes out");
        PollBoth(host, client, 60);
        int last = client.Pings.Count - 1;
        Check(client.Pings[last].Target == 9001 && client.PingNow(last, out at, out age) && (at - new V2(800f, 450f)).Length < 0.01f, "it snaps to the enemy beside the cursor");
        client.Game.Enemies[9001].Disp = new V2(900f, 500f);
        Check(client.PingNow(last, out at, out age) && (at - new V2(900f, 500f)).Length < 0.01f, "and follows it");
        client.Game.Enemies[9001].Hp = 0f;
        Check(!client.PingNow(last, out at, out age), "and lapses when it dies");

        Check(Session.PingLife(PingKind.Danger) == 4f && Session.PingLife(PingKind.Focus) == 6f && Session.PingLife(PingKind.Here) == 5f, "each kind lasts its own time");
        host.Leave("");
        Check(host.Pings.Count == 0, "leaving clears them");
    }
}

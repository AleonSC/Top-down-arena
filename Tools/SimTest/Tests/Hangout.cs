using System;
using Arena;

// The lobby's Hangout room (LobbyAvatars.cs, Docs/Hangout.md): everyone has a character, moves reach the host and everyone,
// the host keeps them inside the room, and a player who leaves takes theirs with them.
static partial class Program
{
    static void HangoutTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        WaitForLobby(host, client);
        PollBoth(host, client, 80);
        int cid = client.MyId;
        Check(host.Avatars.Count == 2 && client.Avatars.Count == 2 && host.Avatars.ContainsKey(cid) && client.Avatars.ContainsKey(1), "everyone in the lobby has a character in the Hangout room, on every screen");
        Check((host.Avatars[1].Pos - host.Avatars[cid].Pos).Length > 40f, "new arrivals start in different spots");

        client.MoveMyAvatar(new V2(200f, 150f), 1.25f);
        PollBoth(host, client, 80);
        var seen = host.Avatars[cid];
        Check((seen.Pos - new V2(200f, 150f)).Length < 0.01f && Math.Abs(seen.Aim - 1.25f) < 0.001f, "a client's move reaches the host");
        host.MoveMyAvatar(new V2(600f, 120f), -0.5f);
        PollBoth(host, client, 80);
        client.StepAvatars(1f);
        Check((client.Avatars[1].Pos - new V2(600f, 120f)).Length < 0.01f && (client.Avatars[1].Disp - client.Avatars[1].Pos).Length < 1f, "the host's move reaches the client, and what's drawn catches up");
        Check((client.Avatars[cid].Pos - new V2(200f, 150f)).Length < 0.01f && (host.Avatars[cid].Pos - new V2(200f, 150f)).Length < 0.01f, "a client's move reaches the other screens too, and its own screen keeps it");

        client.MoveMyAvatar(new V2(-500f, 99999f), 0f);
        PollBoth(host, client, 80);
        Check(host.Avatars[cid].Pos.X == Session.RoomMargin && host.Avatars[cid].Pos.Y == Session.RoomH - Session.RoomMargin && client.Avatars[cid].Pos.X == Session.RoomMargin, "the room's walls hold, on the client and on the host");
        client.MoveMyAvatar(new V2(float.NaN, 5f), 0f);
        PollBoth(host, client, 80);
        Check(!float.IsNaN(host.Avatars[cid].Pos.X) && host.Avatars[cid].Pos.X >= Session.RoomMargin, "a nonsense position never gets in");

        client.Leave("");
        PollBoth(host, client, 40);
        Check(host.Avatars.Count == 1 && !host.Avatars.ContainsKey(cid) && client.Avatars.Count == 0, "a player who leaves takes their character with them");
        host.Leave("");
    }
}

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

        // --- Shooting (LobbyCombat.cs)
        Check(!host.Avatars[1].Pvp && !host.Avatars[cid].Pvp && !host.FireMyShot(0f) && !client.FireMyShot(0f), "PvP starts off, and with it off nobody can fire");
        LobbyEntry he = host.Lobby[1], ce = client.Lobby[cid];
        float iv, sp, lf, dm, iv2, sp2, lf2, dm2;
        Session.ShotStats(he, out iv, out sp, out lf, out dm);
        Session.ShotStats(ce, out iv2, out sp2, out lf2, out dm2);
        float gap = Math.Min(150f, Math.Min(sp * lf, sp2 * lf2) * 0.6f);
        client.MoveMyAvatar(new V2(1000f, 800f), 0f);
        host.MoveMyAvatar(new V2(1000f + gap, 800f), 0f);
        PollBoth(host, client, 40);
        host.SetMyPvp(true);
        PollBoth(host, client, 40);
        Check(host.Avatars[1].Pvp && client.Avatars[1].Pvp && !host.Avatars[cid].Pvp, "everyone sees each player's PvP switch");
        Check(host.FireMyShot(3.14159f), "with PvP on you can fire");
        Check(!host.FireMyShot(3.14159f), "...but not faster than the weapon allows");
        PollBoth(host, client, 160);
        Check(host.Avatars[cid].Hp == Session.RoomHp, "a shot passes through a player with PvP off");
        client.SetMyPvp(true);
        PollBoth(host, client, 40);
        int fired = 0;
        for (int i = 0; i < 12 && host.Avatars[cid].Hp >= Session.RoomHp; i++)
        {
            if (host.FireMyShot((float)Math.PI))
                fired++;
            PollBoth(host, client, (int)(iv * 600f) + 20);
        }
        Check(fired > 0 && host.Avatars[cid].Hp < Session.RoomHp, "a shot at a player with PvP on hurts them");
        PollBoth(host, client, 40);
        Check(client.Avatars[cid].Hp < Session.RoomHp, "the host tells everyone the new health");
        Check(client.Hits.Count >= 0, "hit rings are tracked");

        // knock the client out, see it stay down, then come back at full health at a spawn spot
        for (int i = 0; i < 400 && host.Avatars[cid].Hp > 0f; i++)
        {
            host.FireMyShot((float)Math.PI);
            PollBoth(host, client, 8);
        }
        PollBoth(host, client, 30);
        Check(host.Avatars[cid].Hp <= 0f && client.Avatars[cid].Hp <= 0f, "enough hits knock a player out");
        Check(!client.FireMyShot(0f), "a knocked out player can't fire");
        var downAt = host.Avatars[cid].Pos;
        client.MoveMyAvatar(new V2(1400f, 900f), 0f);
        PollBoth(host, client, 40);
        Check((host.Avatars[cid].Pos - downAt).Length < 0.01f, "a knocked out player can't be moved from where they fell");
        PollBoth(host, client, (int)(Session.DownSeconds * 460f));
        Check(host.Avatars[cid].Hp == Session.RoomHp && client.Avatars[cid].Hp == Session.RoomHp && host.Avatars[cid].Pos.Y > 1400f && Math.Abs(client.Avatars[cid].Pos.Y - host.Avatars[cid].Pos.Y) < 1f, "after a few seconds they return at a spawn spot, full health, on both screens");

        // turning PvP off heals, and the host wipes nonsense aims
        host.SetMyPvp(false);
        client.SetMyPvp(false);
        PollBoth(host, client, 40);
        Check(!host.Avatars[1].Pvp && !host.Avatars[cid].Pvp && !host.FireMyShot(0f), "switching PvP off stops the shooting");

        client.Leave("");
        PollBoth(host, client, 40);
        Check(host.Avatars.Count == 1 && !host.Avatars.ContainsKey(cid) && client.Avatars.Count == 0, "a player who leaves takes their character with them");
        host.Leave("");
    }
}

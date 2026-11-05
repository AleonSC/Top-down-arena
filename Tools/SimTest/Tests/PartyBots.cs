using System;
using System.Threading;
using Arena;

// Bots in the lobby's open slots (PartyBots.cs).
static partial class Program
{
    static void PartyBotsTest(float dt)
    {
        var host = HostAs(new Session(), nextPort++, false, "Host", Role.Tank);
        var pb = new PartyBots();
        bool added1 = pb.Add(host), added2 = pb.Add(host), added3 = pb.Add(host);  // the third has no room
        Action<float, Func<bool>> run = (secs, stop) =>
        {
            for (int i = 0; i < secs * 60; i++)
            {
                Step(host, dt);
                pb.Step(host, dt);
                pb.Update(dt);
                if (i % 2 == 0) Thread.Sleep(1);
                if (stop != null && stop()) break;
            }
        };
        run(4f, null);
        bool filled = host.Lobby.Count == 3 && pb.Bots.Count == 2;
        bool rolesDiffer = host.Lobby[2].Role != host.Lobby[3].Role && host.Lobby[2].Role != Role.Tank && host.Lobby[3].Role != Role.Tank;
        bool ready = host.Lobby[2].Ready && host.Lobby[3].Ready;
        bool full = !pb.CanAdd(host);
        Console.WriteLine("  added {0}/{1}/{2}, lobby {3}, roles differ {4}, bots ready {5}, full {6}", added1, added2, added3, host.Lobby.Count, rolesDiffer, ready, full);
        Check(added1 && added2 && !added3 && filled, "two bots fill the open slots and a third is refused");
        Check(rolesDiffer && ready && full, "bots take the free roles and ready up by themselves");

        // Remove one, add another, then start together.
        pb.Remove(pb.Bots[0]);
        run(1.5f, () => host.Lobby.Count == 2);
        bool removed = host.Lobby.Count == 2 && pb.CanAdd(host);
        pb.Add(host);
        Func<bool> botsReady = () => { foreach (var e in host.Lobby.Values) if (e.Id != host.MyId && !e.Ready) return false; return true; };
        run(4f, () => host.Lobby.Count == 3 && botsReady());
        Check(removed && host.Lobby.Count == 3 && botsReady(), "a bot can be removed and another added");

        host.RequestReady(true);
        Check(host.AllReady, "everyone's ready once the host is");
        host.StartMatch();
        run(2f, () => host.Game.Running && host.Game.Players.Count == 3);
        Check(host.Game.Running && host.Game.Players.Count == 3 && host.Game.Unrecorded, "the match starts with both bots, unrecorded");
        float t = 0;
        run(40f, () => { t += dt; return false; });
        float dmg = 0;
        foreach (var p in host.Game.Players.Values)
            if (p.Id != host.MyId) dmg += p.Run.DamageDealt;
        Console.WriteLine("  bots dealt {0:0} damage in 40 s, wave {1}", dmg, host.Game.Wave);
        Check(dmg > 0, "the bots play: they deal damage");

        host.ReturnToLobby();
        run(3f, null);
        bool back = host.Screen == AppScreen.Lobby && host.Lobby.Count == 3;
        Check(back, "the bots come back to the lobby with the host");
        host.Leave("");
        run(0.2f, null);
        Check(pb.Bots.Count == 0, "the bots go when the host leaves");
    }
}

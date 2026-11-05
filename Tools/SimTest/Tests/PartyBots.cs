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

        // In the Hangout the bots stroll about and emote.
        var startAt = new System.Collections.Generic.Dictionary<int, V2>();
        foreach (var bt in pb.Bots) startAt[bt.Id] = host.Avatars[bt.Id].Pos;
        bool emoted = false;
        run(14f, () => { foreach (var bt in pb.Bots) { int ix; float age; emoted |= host.EmoteOf(bt.Id, out ix, out age); } return false; });
        float moved = 0f;
        foreach (var bt in pb.Bots) moved = Math.Max(moved, host.Avatars[bt.Id].Pos.DistanceTo(startAt[bt.Id]));
        Console.WriteLine("  hangout: a bot walked {0:0} px, emoted {1}", moved, emoted);
        Check(moved > 100f, "bots walk around the Hangout");
        Check(emoted, "bots emote in the Hangout (any emote, whatever the rank)");

        // Remove one, add another, then start together.
        pb.Remove(pb.Bots[0]);
        run(1.5f, () => host.Lobby.Count == 2);
        bool removed = host.Lobby.Count == 2 && pb.CanAdd(host);
        pb.Add(host);
        Func<bool> botsReady = () => { foreach (var e in host.Lobby.Values) if (e.Id != host.MyId && !e.Ready) return false; return true; };
        run(4f, () => host.Lobby.Count == 3 && botsReady());
        Check(removed && host.Lobby.Count == 3 && botsReady(), "a bot can be removed and another added");

        // The host chooses a bot's loadout (the lobby's panel sends these through the bot's own session).
        var chosen = pb.Bots[0];
        chosen.S.RequestKit(Kit.Knight);
        chosen.S.RequestWeapon(2);
        chosen.S.RequestAbility(1);
        run(2f, () => host.Lobby[chosen.Id].Ready && host.Lobby[chosen.Id].Kit == Kit.Knight && host.Lobby[chosen.Id].Weapon == 2 && host.Lobby[chosen.Id].Ability == 1);
        var ce = host.Lobby[chosen.Id];
        Console.WriteLine("  chosen loadout: {0} weapon {1} ability {2}, ready again {3}", ce.Kit, ce.Weapon, ce.Ability, ce.Ready);
        Check(ce.Kit == Kit.Knight && ce.Weapon == 2 && ce.Ability == 1 && ce.Ready, "a bot's loadout can be chosen, and it readies up again");

        pb.Rename(host, chosen, "Big Bob");
        run(1f, () => host.Lobby[chosen.Id].Name == "Big Bob");
        Check(host.Lobby[chosen.Id].Name == "Big Bob", "a bot can be given a name");

        host.RequestReady(true);
        Check(host.AllReady, "everyone's ready once the host is");
        host.StartMatch();
        run(2f, () => host.Game.Running && host.Game.Players.Count == 3);
        Check(host.Game.Running && host.Game.Players.Count == 3 && host.Game.Unrecorded, "the match starts with both bots, unrecorded");
        int shotsFar = 0, shotsAll = 0;
        run(40f, () =>
        {
            foreach (var p in host.Game.Players.Values)
            {
                if (p.Id == host.MyId || !p.Firing || p.Downed || host.Game.Enemies.Count == 0) continue;
                shotsAll++;
                bool near = false;
                float reach = Math.Max(host.Game.WeaponRange(p), 150f);
                foreach (var e in host.Game.Enemies.Values)
                    if (e.Pos.DistanceTo(p.Pos) <= Math.Max(reach + e.Radius, p.Weapon.BotRange) + 60f) near = true;
                if (!near) shotsFar++;
            }
            return false;
        });
        Console.WriteLine("  bot trigger frames {0}, with nothing in reach {1}", shotsAll, shotsFar);
        Check(shotsAll > 0 && shotsFar <= shotsAll / 8, "bots only shoot with an enemy in reach");
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

    // A bot stands in the lee of a Tank's planted Last Stand: close behind the tank, away from where the plates face.
    static void PartyBotShelterTest(float dt)
    {
        var host = HostAs(new Session(), nextPort++, false, "Host", Role.Tank, 0, 0, Kit.Knight);
        var pb = new PartyBots();
        pb.Add(host);
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
        run(3f, () => host.Lobby.Count == 2 && host.Lobby[2].Ready);
        host.RequestReady(true);
        host.StartMatch();
        run(8f, () => host.Game.State == MatchState.Fighting && host.Game.Players.Count == 2);
        var g = host.Game;
        var tank = g.Players[1];
        var bot = g.Players[pb.Bots[0].Id];
        tank.Pos = new V2(Game.Width / 2f, Game.Height / 2f);
        bot.Pos = tank.Pos + new V2(260f, -200f);  // start away from the lee
        int sheltered = 0, frames = 0;
        run(12f, () =>
        {
            tank.StandOn = true;  // planted (the plates themselves aren't needed to see where the bot goes)
            tank.Pos = new V2(Game.Width / 2f, Game.Height / 2f);
            tank.Hp = tank.MaxHp; bot.Hp = bot.MaxHp;
            if (g.State == MatchState.Fighting && ++frames > 8 * 60)  // the last 4 s
            {
                V2 rel = bot.Pos - tank.Pos;
                float behind = rel.Length > 1f ? (float)Math.Cos(rel.Angle - tank.StandAim) : 0f;
                if (rel.Length < Game.StandRadius && behind < -0.5f)
                    sheltered++;
            }
            return false;
        });
        int total = Math.Max(1, frames - 8 * 60);
        Console.WriteLine("  bot in the lee for {0} of {1} frames", sheltered, total);
        Check(sheltered > total * 0.7, "a bot ally shelters behind a planted Last Stand");
    }
}

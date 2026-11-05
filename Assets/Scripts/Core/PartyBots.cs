using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bots that fill the open slots of your lobby and play with you. The host adds them from the lobby's open slots.
    /// Each one is a separate session that joins the host over the local network, exactly like a friend (and like the
    /// Boss Lab's bots), with the smart bot (BotBrain.cs) at the controls: it readies up by itself, picks its own
    /// upgrades and carries on between waves. The app steps them next to the host's own game (ArenaApp).
    /// A run with bots in the party is saved (their seats are marked as bots, and Continue brings them back) but isn't
    /// counted in anyone's records (Game.NoRecords).
    public class PartyBots
    {
        public class PartyBot
        {
            public Session S;
            public BotSkill Skill;
            public string Name;
            public float ReadyT;  // seconds until it asks to be ready again
            public int Id => S.MyId;
        }

        public readonly List<PartyBot> Bots = new List<PartyBot>();
        public BotSkill NewSkill = BotSkill.Good;  // the skill the next bot gets
        readonly Random rng = new Random();
        bool marked;  // we set the host game's NoRecords

        public PartyBot Find(int lobbyId)
        {
            foreach (var b in Bots)
                if (b.S.Lobby.Count > 0 && b.Id == lobbyId)
                    return b;
            return null;
        }

        int Pending()
        {
            int n = 0;
            foreach (var b in Bots)
                if (b.S.Lobby.Count == 0)
                    n++;
            return n;
        }

        /// Can the host add a bot right now? (In the lobby, with a free slot, and not continuing a saved run, whose seats are for people.)
        public bool CanAdd(Session host) =>
            host != null && host.IsHost && host.Screen == AppScreen.Lobby && !host.Game.Running && (host.Resume == null || ResumeBotSeat(host) >= 0)
            && host.Lobby.Count + Pending() < Session.MaxPlayers;

        /// Continuing a saved run: the first bot seat nobody has taken or been promised to a bot (-1: none).
        int ResumeBotSeat(Session host)
        {
            if (host.Resume == null)
                return -1;
            for (int i = 0; i < host.Resume.Seats.Count; i++)
            {
                var seat = host.Resume.Seats[i];
                if (!seat.Bot || Bots.Exists(b => string.Equals(b.Name, seat.Name, StringComparison.OrdinalIgnoreCase)))
                    continue;
                bool taken = false;
                foreach (var e in host.Lobby.Values)
                    if (e.Seat == i)
                        taken = true;
                if (!taken)
                    return i;
            }
            return -1;
        }

        /// A saved run's bots come back by themselves: each joins under its old name and takes its seat.
        void RestoreBots(Session host)
        {
            if (host.Resume == null || host.Screen != AppScreen.Lobby || host.Game.Running)
                return;
            int seat = ResumeBotSeat(host);
            if (seat >= 0 && host.Lobby.Count + Pending() < Session.MaxPlayers)
                Add(host, host.Resume.Seats[seat]);
        }

        public bool Add(Session host, SeatSave from = null)
        {
            if (from == null ? !CanAdd(host) : host == null || !host.IsHost || host.Game.Running || host.Lobby.Count + Pending() >= Session.MaxPlayers)
                return false;
            int n = 1;
            while (Bots.Exists(b => b.Name == "Bot " + n))
                n++;
            var bot = new PartyBot { S = new Session(), Skill = from != null ? from.BotSkill : NewSkill, Name = from != null ? from.Name : "Bot " + n };
            var g = bot.S.Game;
            g.Unrecorded = true;
            g.Bot = true;
            g.BotLevel = bot.Skill;
            g.BotRerolls = false;
            var kit = (Kit)rng.Next(Defs.KitCount);
            // role null: it takes the first role nobody has (Tank, then Healer, then DPS)
            bot.S.Join("127.0.0.1", host.Port, bot.Name, null, rng.Next(Defs.WeaponsPerRole), rng.Next(Upgrades.RoleAbilityChoices), kit);
            Bots.Add(bot);
            return true;
        }

        /// Give a bot a name (the host's lobby shows it, and it carries into the run).
        public void Rename(Session host, PartyBot bot, string name)
        {
            bot.Name = Session.CleanName(name);
            host.RenameSeat(bot.Id, bot.Name);
        }

        public void SetSkill(PartyBot bot, BotSkill skill)
        {
            bot.Skill = skill;
            bot.S.Game.BotLevel = skill;
        }

        /// A new random class, weapon and Space ability for it (the role stays).
        public void Shuffle(PartyBot bot)
        {
            bot.S.RequestKit((Kit)rng.Next(Defs.KitCount));
            bot.S.RequestWeapon(rng.Next(Defs.WeaponsPerRole));
            bot.S.RequestAbility(rng.Next(Upgrades.RoleAbilityChoices));
        }

        public void Remove(PartyBot bot)
        {
            bot.S.Leave("");
            Bots.Remove(bot);
        }

        public void Clear()
        {
            foreach (var b in Bots)
                b.S.Leave("");
            Bots.Clear();
        }

        /// Fixed step: the network and simulation for the bots (the host is stepped by whoever owns it).
        public void Step(Session host, float dt)
        {
            if (Bots.Count == 0)
            {
                if (host != null && host.IsHost)
                    RestoreBots(host);
                if (marked && host != null && !host.Game.Running)
                {
                    host.Game.NoRecords = false;
                    host.Game.PartyBotIds.Clear();  // the last bot left: the run counts again
                    marked = false;
                }
                return;
            }
            if (host != null && host.IsHost)
                RestoreBots(host);
            if (host == null || !host.IsHost || host.Screen == AppScreen.Menu)
            {
                Clear();  // the host left (or is hosting something else): the bots go with it
                return;
            }
            for (int i = Bots.Count - 1; i >= 0; i--)
            {
                var b = Bots[i];
                if (b.S.Net == null || b.S.Port != host.Port)  // turned away (the lobby filled up), or lost the host
                {
                    b.S.Leave("");
                    Bots.RemoveAt(i);
                    continue;
                }
                b.S.Poll();
                if (b.S.Net == null)
                    continue;
                if (b.S.Screen == AppScreen.Lobby)
                {
                    LobbyEntry me;
                    b.ReadyT -= dt;
                    if (b.S.Lobby.TryGetValue(b.Id, out me) && !me.Ready && b.ReadyT <= 0f)
                    {
                        b.S.RequestReady(true);
                        b.ReadyT = 0.5f;
                    }
                }
                if (!host.Game.Paused)
                    b.S.Game.FixedStep(dt, new PlayerInput());
            }
            if (!host.Game.Running)
            {
                host.Game.NoRecords = true;  // (a run in progress keeps what it started with)
                marked = true;
            }
            host.Game.PartyBotIds.Clear();
            foreach (var b in Bots)
                if (b.S.Lobby.Count > 0)
                    host.Game.PartyBotIds[b.Id] = b.Skill;
        }

        public void Update(float dt)
        {
            foreach (var b in Bots)
            {
                if (b.S.Net == null)
                    continue;
                b.S.Game.Update(dt);
                b.S.Game.Sounds.Clear();  // only the host's sounds play
                b.S.Game.MasteryToasts.Clear();
            }
        }
    }
}

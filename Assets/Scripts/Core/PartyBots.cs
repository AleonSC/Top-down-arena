using System;
using System.Collections.Generic;

namespace Arena
{
    /// Bots that fill the open slots of your lobby and play with you. The host adds them from the lobby's open slots.
    /// Each one is a separate session that joins the host over the local network, exactly like a friend (and like the
    /// Boss Lab's bots), with the smart bot (BotBrain.cs) at the controls: it readies up by itself, picks its own
    /// upgrades and carries on between waves. The app steps them next to the host's own game (ArenaApp).
    /// A run with bots in the party isn't saved and isn't counted in anyone's records (Game.Unrecorded), like the
    /// Boss Lab and the F1 bot.
    public class PartyBots
    {
        public class PartyBot
        {
            public Session S;
            public BotSkill Skill;
            public string Name;
            public float ReadyT;  // seconds until it asks to be ready again
            public V2 Goal;       // the Hangout: where it's strolling to
            public float Idle;    // ...and how long it stands about once it gets there
            public float EmoteT;  // seconds until it next emotes
            public bool HasGoal;
            public int Id => S.MyId;
        }

        public readonly List<PartyBot> Bots = new List<PartyBot>();
        public BotSkill NewSkill = BotSkill.Good;  // the skill the next bot gets
        readonly Random rng = new Random();
        bool marked;  // we set the host game's Unrecorded

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
            host != null && host.IsHost && host.Screen == AppScreen.Lobby && !host.Game.Running && host.Resume == null
            && host.Lobby.Count + Pending() < Session.MaxPlayers;

        public bool Add(Session host)
        {
            if (!CanAdd(host))
                return false;
            int n = 1;
            while (Bots.Exists(b => b.Name == "Bot " + n))
                n++;
            var bot = new PartyBot { S = new Session(), Skill = NewSkill, Name = "Bot " + n };
            var g = bot.S.Game;
            g.Unrecorded = true;
            g.Bot = true;
            g.BotLevel = bot.Skill;
            g.BotRerolls = false;
            bot.S.AnyEmote = true;
            bot.EmoteT = 2f + (float)rng.NextDouble() * 4f;
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
                if (host != null)
                    host.BotSeats.Clear();
                if (marked && host != null && !host.Game.Running)
                {
                    host.Game.Unrecorded = false;  // the last bot left: the run counts again
                    marked = false;
                }
                return;
            }
            if (host == null || !host.IsHost || host.Screen == AppScreen.Menu)
            {
                Clear();  // the host left (or is hosting something else): the bots go with it
                return;
            }
            host.BotSeats.Clear();
            foreach (var b in Bots)
                if (b.S.Lobby.Count > 0)
                    host.BotSeats.Add(b.Id);  // (they may play any emote)
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
                    Wander(b, dt);
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
                host.Game.Unrecorded = true;  // (a run in progress keeps what it started with)
                marked = true;
            }
        }

        /// In the lobby's Hangout room a bot strolls about like a person: walks to a spot, stands a while, then picks another,
        /// and now and then plays an emote. (It moves its character the way a player does; everyone sees it whichever tab they're on.)
        void Wander(PartyBot b, float dt)
        {
            Avatar a;
            if (!b.S.Avatars.TryGetValue(b.Id, out a) || a.Hp <= 0f)
                return;
            b.EmoteT -= dt;
            if (b.EmoteT <= 0f)
            {
                b.EmoteT = 4f + (float)rng.NextDouble() * 8f;
                b.S.SendEmote(1 + rng.Next(Arena.Rank.EmoteCount));
            }
            if (b.Idle > 0f)
            {
                b.Idle -= dt;
                return;
            }
            if (!b.HasGoal)
            {
                // somewhere in the middle of the room most of the time, anywhere now and then
                float cx = Session.RoomW / 2f, cy = Session.RoomH / 2f;
                float spread = rng.NextDouble() < 0.7 ? 0.35f : 0.9f;
                b.Goal = new V2(cx + ((float)rng.NextDouble() * 2f - 1f) * cx * spread, cy + ((float)rng.NextDouble() * 2f - 1f) * cy * spread);
                b.HasGoal = true;
            }
            V2 to = b.Goal - a.Pos;
            float step = Session.AvatarSpeed * 0.55f * dt;
            if (to.Length <= step)
            {
                b.S.MoveMyAvatar(b.Goal, a.Aim);
                b.HasGoal = false;
                b.Idle = 1f + (float)rng.NextDouble() * 4f;
                return;
            }
            b.S.MoveMyAvatar(a.Pos + to.Normalized() * step, to.Angle);
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

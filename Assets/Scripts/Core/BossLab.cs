using System;
using System.Collections.Generic;

namespace Arena
{
    /// One seat in a Boss Lab fight.
    public class LabSeat
    {
        public Role Role = Role.Dps;
        public Kit Kit = Kit.Soldier;
        public int Weapon, Ability;
        public bool Human;  // seat 1 only: you play it yourself
    }

    public enum LabOutcome { Running, Won, Lost, TimedOut }

    /// What happened to one player in a lab fight.
    public class LabPlayerStats
    {
        public string Name;
        public float BossDamage, DamageTaken, Healing;
        public int Downs, Abilities;
        public readonly Dictionary<string, float> TakenFrom = new Dictionary<string, float>();
    }

    /// Boss Lab: sets up a boss fight with 1-3 bots (any role, class and weapon) on the real game, gives them
    /// the upgrades a team would have by that wave, and keeps score. The host session is passed in (the
    /// game's own, so it's drawn as usual); the other seats are extra sessions that join it over the local
    /// network, exactly like a friend would, and the lab steps them.
    public class BossLab
    {
        public static readonly int[] BossWaves = { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 };
        public const float TimeLimit = 300f;

        // Setup
        public int Wave = 5;
        public readonly List<LabSeat> Seats = new List<LabSeat> { new LabSeat { Role = Role.Tank }, new LabSeat { Role = Role.Dps }, new LabSeat { Role = Role.Healer } };
        public int PlayerCount = 2;
        public BotSkill Skill = BotSkill.Good;
        public int Picks = -1;          // upgrade picks each player gets; -1 = typical for the wave
        public bool Legendaries = true; // a legendary and a weapon mod for every boss already beaten
        public int Seed;

        // Live
        public Session Host;
        public readonly List<Session> Clients = new List<Session>();
        public LabOutcome Outcome = LabOutcome.Running;
        public float FightTime;          // seconds since the boss appeared
        public float BossHpFrac = 1f;    // all bosses' health left (0-1)
        public string BossName = "";
        public readonly Dictionary<int, LabPlayerStats> Stats = new Dictionary<int, LabPlayerStats>();
        public int PeakBullets;
        bool prepared, bossSeen;
        Random rng;

        public EnemyType Boss => Game.BossForWave(Wave);

        /// Level-up picks a player typically has when this boss arrives (measured from full runs played by Good bots).
        public static int TypicalPicks(int wave) => Math.Max(0, (int)Math.Round(wave * 1.15f - 1.5f));  // level picks only; legendaries and mods are extra

        public int PicksFor => Picks >= 0 ? Picks : TypicalPicks(Wave);

        /// Starts the fight. host is the session to host it on; it gets the first seat.
        public bool Start(Session host, int port)
        {
            Stop();
            rng = new Random(Seed != 0 ? Seed : Environment.TickCount);
            Host = host;
            Outcome = LabOutcome.Running;
            FightTime = 0f;
            BossHpFrac = 1f;
            PeakBullets = 0;
            prepared = bossSeen = false;
            Stats.Clear();
            host.AutoStart = PlayerCount;
            host.StartWave = Wave;
            var s0 = Seats[0];
            if (!host.Host(port, s0.Human ? "You" : "Bot 1", s0.Role, s0.Weapon, s0.Ability, s0.Kit))
                return false;
            host.Game.Bot = !s0.Human;
            host.Game.BotLevel = s0.Human ? BotSkill.Off : Skill;
            host.Game.BotRerolls = false;
            host.Game.DamageEvent = OnDamage;
            for (int i = 1; i < PlayerCount; i++)
            {
                var c = new Session();
                c.Game.Bot = true;
                c.Game.BotLevel = Skill;
                c.Game.BotRerolls = false;
                var s = Seats[i];
                c.Join("127.0.0.1", port, "Bot " + (i + 1), s.Role, s.Weapon, s.Ability, s.Kit);
                Clients.Add(c);
            }
            if (PlayerCount == 1)
                host.StartMatch();  // nobody joins a solo fight, so start it straight away
            return true;
        }

        public void Stop()
        {
            foreach (var c in Clients)
                c.Leave("");
            Clients.Clear();
            if (Host != null)
            {
                Host.Game.DamageEvent = null;
                Host.Game.Bot = false;
                Host.Game.BotLevel = BotSkill.Off;
                Host.AutoStart = 0;
                Host.StartWave = 1;
                Host.Leave("");
            }
            Host = null;
        }

        public bool Active => Host != null;

        /// Network and simulation for the bot seats (the host is stepped by whoever owns it).
        public void StepClients(float dt)
        {
            foreach (var c in Clients)
            {
                c.Poll();
                c.Game.FixedStep(dt, new PlayerInput());
            }
        }

        public void UpdateClients(float dt)
        {
            foreach (var c in Clients)
                c.Game.Update(dt);
        }

        /// Call once per fixed step after the host has stepped: hands out upgrades and keeps score.
        public void Watch(float dt)
        {
            if (Host == null)
                return;
            var g = Host.Game;
            if (!g.Running)
                return;
            if (!prepared && g.Players.Count >= PlayerCount)
            {
                prepared = true;
                foreach (var p in g.Players.Values)
                {
                    Equip(g, p);
                    Stats[p.Id] = new LabPlayerStats { Name = p.Name + " (" + Defs.KitName(p.Kit) + " " + p.Def.Name + ", " + p.Weapon.Name + ")" };
                }
            }
            if (Outcome != LabOutcome.Running)
                return;

            float hp = 0f, max = 0f;
            foreach (var e in g.Enemies.Values)
                if (e.Def.IsBoss)
                {
                    hp += Math.Max(0f, e.Hp);
                    max += e.MaxHp;
                    if (!bossSeen)
                        BossName = e.Def.Name.StartsWith("Reaper") ? "Twin Reapers" : e.Def.Name;
                    bossSeen = true;
                }
            if (bossSeen)
            {
                FightTime += dt;
                if (max > 0f)
                    BossHpFrac = hp / max;
                int shots = 0;
                foreach (var b in g.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind))
                        shots++;
                PeakBullets = Math.Max(PeakBullets, shots);
            }
            foreach (var p in g.Players.Values)
            {
                LabPlayerStats st;
                if (!Stats.TryGetValue(p.Id, out st))
                    continue;
                st.BossDamage = p.Run.BossDamage;
                st.DamageTaken = p.Run.DamageTaken;
                st.Healing = p.Run.HealingDone;
                st.Downs = p.Run.Downs;
                st.Abilities = p.Run.AbilitiesUsed;
            }

            if (g.State == MatchState.GameOver)
                Outcome = LabOutcome.Lost;
            else if (bossSeen && (max == 0f || g.State == MatchState.Intermission || g.State == MatchState.Victory))
            {
                Outcome = LabOutcome.Won;
                BossHpFrac = 0f;
            }
            else if (FightTime > TimeLimit)
                Outcome = LabOutcome.TimedOut;
        }

        void OnDamage(Player p, float amount, string source)
        {
            LabPlayerStats st;
            if (!Stats.TryGetValue(p.Id, out st) || Outcome != LabOutcome.Running)
                return;
            float had;
            st.TakenFrom.TryGetValue(source, out had);
            st.TakenFrom[source] = had + amount;
        }

        /// The upgrades, legendaries, weapon mods and ability mastery a player would have by this wave.
        void Equip(Game g, Player p)
        {
            for (int i = 0; i < PicksFor; i++)
            {
                var offer = Upgrades.RollOffer(rng, p.Role, p.RoleAbility, p.Taken, 1 + i);
                if (offer != null && offer.Length > 0)
                    g.GrantUpgrade(p.Id, offer[rng.Next(offer.Length)]);
            }
            if (Legendaries)
                for (int w = 5; w < Wave; w += 5)
                {
                    g.GrantUpgrade(p.Id, Upgrades.LegendaryOffer(rng, Game.BossForWave(w), p.Role, p.RoleAbility, p.Taken)[rng.Next(3)]);
                    var mods = Upgrades.ModOffer(p.Role, p.RoleAbility, p.Taken);
                    if (mods != null)
                        g.GrantUpgrade(p.Id, mods[rng.Next(mods.Length)]);
                }
            float xp = Math.Min(Game.MasteryXpFor[Game.MasteryMax - 1], Wave * 33f);
            foreach (var s in p.Slots)
                if (s.Id != AbilityId.None)
                    g.GainMastery(p, s.Id, xp);
            g.DevHeal(p.Id);
        }

        public float TeamBossDps => FightTime > 0f ? SumBossDamage() / FightTime : 0f;

        float SumBossDamage()
        {
            float d = 0f;
            foreach (var s in Stats.Values)
                d += s.BossDamage;
            return d;
        }
    }
}

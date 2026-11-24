using System;
using System.Collections.Generic;

namespace Arena
{
    /// PvP (Docs/Targeting.md step 3): in the Hangout players' attacks hurt each other. Nothing here knows any weapon: every attack
    /// asks FoeQuery for what it hits (FoesInCircle, FoesInBox, FoesInCone, AllFoes, given the attacker as `by`), and with PvP on
    /// that also gives each other player in the shape as their stand-in, an Enemy that is never in Game.Enemies (StandOf). HurtEnemy
    /// sends what a stand-in takes to its player (PvpHurt), at PvpDamage of it: weapons are tuned for enemies. Your own attacks
    /// never touch you. Status effects (slows, stuns, knockback, marks) land on the stand-in and do nothing yet.
    public partial class Game
    {
        public const float PvpDamage = 0.35f;  // the share of an attack's damage a player takes from another player

        /// PvP is on: the Hangout (HangoutGame.cs).
        public bool PvpOn => HangoutOn;

        readonly Dictionary<int, Enemy> stands = new Dictionary<int, Enemy>();

        /// A stand-in's id: StandBase - the player's id (well clear of 0 and -1, which code uses for "none").
        public const int StandBase = -1000;

        /// Host: the foe with this id: an enemy, or with PvP on a player's stand-in (an attack that keeps its target's id: a lock-on,
        /// a tether, a beam, a summon's prey). Use it instead of Enemies.TryGetValue in a player's attack.
        bool TryFoe(int id, out Enemy e)
        {
            if (Enemies.TryGetValue(id, out e))
                return true;
            e = null;
            Player q;
            if (id > StandBase || !PvpOn || !Players.TryGetValue(StandBase - id, out q) || q.Downed || q.WarpT > 0f)
                return false;
            e = StandOf(q);
            return true;
        }

        /// Host: TryFoe without the enemy.
        bool FoeExists(int id)
        {
            Enemy e;
            return TryFoe(id, out e);
        }

        /// Player q's stand-in for attack queries: placed on them each time it's asked for, their body's size, never a boss or
        /// heavy, and too tough to die (its health is never what counts: PvpHurt is).
        internal Enemy StandOf(Player q)
        {
            Enemy s;
            if (!stands.TryGetValue(q.Id, out s))
            {
                var d = Defs.Get(EnemyType.Grunt);
                stands[q.Id] = s = new Enemy { Id = StandBase - q.Id, Type = EnemyType.Grunt, Def = d, Stand = q };
            }
            s.Pos = s.Disp = q.Pos;
            s.Radius = q.Def.BodyR;
            s.Hp = s.MaxHp = 1e9f;
            s.SpawnT = 0f;
            return s;
        }

        /// Host: an attack by player `attacker` landed on player q (through their stand-in).
        /// `tick`: damage over time (a burn, a poison, ink), which a hit's grace doesn't stop and which leaves no grace behind.
        void PvpHurt(Player q, float dmg, int attacker, bool tick = false)
        {
            if (!PvpOn || attacker <= 0 || attacker == q.Id || q.Downed || q.WarpT > 0f || (!tick && q.InvulnT > 0f) || dmg <= 0f)
                return;
            Player by;
            string src = Players.TryGetValue(attacker, out by) ? "PvP: " + by.Name : "PvP";
            if (tick)
                DamageOverTime(q, dmg * PvpDamage, src);
            else
                Damage(q, dmg * PvpDamage, src);
        }
    }

    public partial class Game
    {
        public const float PvpStatus = 0.5f, PvpStunCap = 1.2f, PvpSlowCap = 3f, PvpSlowFloor = 0.45f;  // status effects from another player: half as long, a stun never past 1.2 s, a slow never past 3 s or below 45% speed

        /// Host, each Hangout tick: what other players' attacks left on each stand-in becomes that player's own version, the way the
        /// stand-in's damage does (PvpHurt). Every attack writes enemy fields, however it's written, so this catches them all: a
        /// freeze, stun or hold is a stun (StunT, your own game stops you), a slow is PvpSlowT (replicated, Player.Speed), a push
        /// is a knockback (KnockPlayer), and burns, bleeds and poison tick as damage over time from whoever set them. A daze (every
        /// melee hit) does nothing: on a player it would be a stun-lock.
        void TickPvp(float dt)
        {
            if (!PvpOn || !IsHost)
                return;
            foreach (var q in Players.Values)
            {
                Enemy s;
                if (!stands.TryGetValue(q.Id, out s))
                    continue;
                if (q.Downed || q.WarpT > 0f)
                {
                    ClearStand(s);
                    continue;
                }
                float stun = Math.Max(s.FrozenT, s.StunT);
                if (stun > 0f)
                    q.StunT = Math.Max(q.StunT, Math.Min(PvpStunCap, stun * PvpStatus));
                if (s.SlowT > 0f && s.SlowMult < 1f && s.FrozenT <= 0f)
                {
                    q.PvpSlowT = Math.Max(q.PvpSlowT, Math.Min(PvpSlowCap, s.SlowT * PvpStatus));
                    q.PvpSlowMult = Math.Max(PvpSlowFloor, Math.Min(q.PvpSlowT > 0f ? q.PvpSlowMult : 1f, s.SlowMult));
                }
                if (s.KnockLeft.Length > 1f)
                    KnockPlayer(q, s.KnockLeft, s.KnockLeft.Length * PvpStatus);
                if (s.BurnT > 0f)
                    PvpHurt(q, s.BurnDps * dt, s.BurnBy, true);
                if (s.BleedT > 0f)
                    PvpHurt(q, s.BleedDps * dt, s.BleedBy, true);
                if (s.PoisonT > 0f)
                    PvpHurt(q, s.PoisonDps * dt, s.PoisonBy, true);
                s.BurnT = Math.Max(0f, s.BurnT - dt);
                s.BleedT = Math.Max(0f, s.BleedT - dt);
                s.PoisonT = Math.Max(0f, s.PoisonT - dt);
                s.FrozenT = s.StunT = s.SlowT = s.SlowT2 = s.DazeT = 0f;  // (taken over: the player's own timers run them now)
                s.SlowMult = s.SlowMult2 = 1f;
                s.KnockLeft = default(V2);
            }
        }

        static void ClearStand(Enemy s)
        {
            s.FrozenT = s.StunT = s.SlowT = s.SlowT2 = s.DazeT = s.BurnT = s.BleedT = s.PoisonT = 0f;
            s.SlowMult = s.SlowMult2 = 1f;
            s.KnockLeft = default(V2);
        }
    }

    public partial class Enemy
    {
        /// PvP: the player this is the stand-in for (Pvp.cs); null for every real enemy.
        public Player Stand;
    }
}

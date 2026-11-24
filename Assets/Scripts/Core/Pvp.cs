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

    public partial class Enemy
    {
        /// PvP: the player this is the stand-in for (Pvp.cs); null for every real enemy.
        public Player Stand;
    }
}

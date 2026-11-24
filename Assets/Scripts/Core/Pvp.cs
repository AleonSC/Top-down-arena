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

        /// Player q's stand-in for attack queries: placed on them each time it's asked for, their body's size, never a boss or
        /// heavy, and too tough to die (its health is never what counts: PvpHurt is).
        internal Enemy StandOf(Player q)
        {
            Enemy s;
            if (!stands.TryGetValue(q.Id, out s))
            {
                var d = Defs.Get(EnemyType.Grunt);
                stands[q.Id] = s = new Enemy { Id = -q.Id, Type = EnemyType.Grunt, Def = d, Stand = q };
            }
            s.Pos = s.Disp = q.Pos;
            s.Radius = q.Def.BodyR;
            s.Hp = s.MaxHp = 1e9f;
            s.SpawnT = 0f;
            return s;
        }

        /// Host: an attack by player `attacker` landed on player q (through their stand-in).
        void PvpHurt(Player q, float dmg, int attacker)
        {
            if (!PvpOn || attacker <= 0 || attacker == q.Id || q.Downed || q.WarpT > 0f || q.InvulnT > 0f || dmg <= 0f)
                return;
            Player by;
            Damage(q, dmg * PvpDamage, Players.TryGetValue(attacker, out by) ? "PvP: " + by.Name : "PvP");
        }
    }

    public partial class Enemy
    {
        /// PvP: the player this is the stand-in for (Pvp.cs); null for every real enemy.
        public Player Stand;
    }
}

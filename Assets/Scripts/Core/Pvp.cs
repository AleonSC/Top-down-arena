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

        /// PvP is on: always in Practice (the Hangout, HangoutGame.cs).
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

        readonly Dictionary<int, float> wallPushAt = new Dictionary<int, float>();
        public const float PvpWallPushGap = 0.12f;  // a wall's push-out goes to a player's game at most this often

        /// Host, PvP: another player's wall (Blood Wall, Ossuary Wall) pushes q out by `push`. Players move on their own game, so
        /// it's a knockback (KnockPlayer), sent no more often than PvpWallPushGap.
        void PvpWallPush(Player q, V2 push)
        {
            if (push.Length < 0.5f)
                return;
            float last;
            if (wallPushAt.TryGetValue(q.Id, out last) && hostClock - last < PvpWallPushGap)
                return;
            wallPushAt[q.Id] = hostClock;
            KnockPlayer(q, push, push.Length + 4f);
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
        public const float PvpStunImmune = 1.5f;  // after a PvP stun ends, this long before another can land (no stun-lock)
        public const float PvpStatus = 0.5f, PvpStunCap = 1.2f, PvpSlowCap = 3f, PvpSlowFloor = 0.45f;  // status effects from another player: half as long, a stun never past 1.2 s, a slow never past 3 s or below 45% speed

        /// Host, each Hangout tick: what other players' attacks left on each stand-in becomes that player's own, the way the
        /// stand-in's damage does (PvpHurt). Every attack writes enemy fields, however it's written, so this catches them all.
        /// First the stand-in runs the same status ticks an enemy does (TickEnemies): burns and incendiary pools, bleeds,
        /// poison, venom, wet, bone cages and each class's own hook, so their damage reaches the player (DotDamage) and their
        /// timers and marks run down as they do on an enemy. Then: a freeze, stun or hold (a grasp, a cage, a lift) is a stun
        /// (StunT, your own game stops you), a slow (wet included) is PvpSlowT (replicated, Player.Speed), a push is a knockback
        /// (KnockPlayer). Marks stay on the stand-in and raise the hits it takes (PvpHitMult). A daze (every melee hit) does
        /// nothing: on a player it would be a stun-lock; and after a stun the player can't be stunned again for PvpStunImmune.
        void TickPvp(float dt)
        {
            if (!PvpOn || !IsHost)
                return;
            foreach (var q in Players.Values)
            {
                Enemy s;
                if (!stands.TryGetValue(q.Id, out s))
                    continue;
                q.PvpStunImmuneT = Math.Max(0f, q.PvpStunImmuneT - dt);
                if (q.Downed || q.WarpT > 0f)
                {
                    ClearStand(s);
                    continue;
                }
                StandOf(q);  // (on the player now: an arc or a spreading poison starts from here)
                s.T += dt;
                s.MarkT = Math.Max(0f, s.MarkT - dt);
                s.IceT = Math.Max(0f, s.IceT - dt);
                s.LitT = Math.Max(0f, s.LitT - dt);
                TickWet(s, dt);
                TickBurn(s, dt);
                TickBleed(s, dt);
                TickBoneCage(s, dt);
                TickPoison(s, dt);
                TickNeedlesIn(s, dt);
                TickVenom(s, dt);
                foreach (var tick in Classes.EnemyHooks)
                    tick(this, s, dt);
                s.Suppress = Math.Max(0f, s.Suppress - SuppressFade * dt);
                s.SmokeT = Math.Max(0f, s.SmokeT - dt);
                float stun = Math.Max(Math.Max(s.FrozenT, s.StunT), Math.Max(Math.Max(s.GraspT, s.CageT), s.BsLiftT));
                if (stun > 0f && q.PvpStunImmuneT <= 0f)
                {
                    float t = Math.Min(PvpStunCap, stun * PvpStatus);
                    q.StunT = Math.Max(q.StunT, t);
                    q.PvpStunImmuneT = t + PvpStunImmune;
                }
                if (s.SlowT > 0f && s.SlowMult < 1f && s.FrozenT <= 0f)
                {
                    q.PvpSlowT = Math.Max(q.PvpSlowT, Math.Min(PvpSlowCap, s.SlowT * PvpStatus));
                    q.PvpSlowMult = Math.Max(PvpSlowFloor, Math.Min(q.PvpSlowT > 0f ? q.PvpSlowMult : 1f, s.SlowMult));
                }
                if (s.KnockLeft.Length > 1f)
                    KnockPlayer(q, s.KnockLeft, s.KnockLeft.Length * PvpStatus);
                s.FrozenT = s.StunT = s.SlowT = s.SlowT2 = s.DazeT = s.GraspT = s.BsLiftT = 0f;  // (taken over: the player's own timers run them now)
                if (s.CageT > dt)
                    s.CageT = dt;  // (ends next tick, through TickBoneCage, so the cage's own immunity starts)
                s.SlowMult = s.SlowMult2 = 1f;
                s.KnockLeft = default(V2);
            }
        }

        /// PvP: what makes a hit on a stand-in bigger, as on an enemy (HurtEnemy): a mark (Lightbringer's, the Ninja's...), a
        /// Searchlight, the attacker's damage cards (Blood Price, Vein Tithe, Execute on a player low on health, Graviton Rounds on a
        /// slowed one, crits) and an ability's cast power. The attacker's on-hit slow lands on the stand-in.
        float PvpHitMult(Enemy s, Player q, int attacker)
        {
            float m = 1f;
            if (casting)
                m *= castPower;
            if (s.MarkT > 0f)
                m *= s.MarkMult;
            m *= LitMult(s);
            Player p;
            if (!Players.TryGetValue(attacker, out p))
                return m;
            var st = p.Stats;
            m *= BloodDamageMult(p) * TitheOutput(p);
            if (IsHunted(p))
                m *= Modifiers.HuntedDamage;
            if (st.ExecuteBonus > 0f && q.Hp < q.MaxHp * ExecuteBelow)
                m *= 1f + st.ExecuteBonus;
            if (st.SlowedBonus > 0f && q.PvpSlowT > 0f)
                m *= 1f + st.SlowedBonus;
            if (st.CritChance > 0f && rng.NextDouble() < st.CritChance)
                m *= 2f;
            if (st.SlowOnHit > 0f)
                Slow(s, 2f, 1f - st.SlowOnHit);
            return m;
        }

        static void ClearStand(Enemy s)
        {
            s.FrozenT = s.StunT = s.SlowT = s.SlowT2 = s.DazeT = s.BurnT = s.BleedT = s.PoisonT = s.VenomT = s.PoolT = s.WetT = 0f;
            s.GraspT = s.CageT = s.BsLiftT = s.MarkT = s.IceT = s.LitT = s.SmokeT = s.Suppress = 0f;
            s.BurnPool = 0f;
            s.BleedStacks = s.BleedHits = 0;
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

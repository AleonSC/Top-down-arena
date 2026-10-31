using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Hive Queen's legendaries, each one of her own tricks turned yours (like the Warden's in Stun.cs):
    /// Hive Drones (drones circle you, blocking bullets; after your kills they fly out to sting and come home to heal
    /// you), Royal Dive (a Q / E / Shift ability: her dive, landing in a honey pool that slows enemies and heals
    /// allies) and Bomber Escort (her bombers fly from you at the toughest enemy near you and burst on it).
    /// Plan and numbers: Docs/QueenLoot.md.
    public partial class Game
    {
        // --- Hive Drones -------------------------------------------------------------------

        public const float DroneSting = 20f, DroneStingEvery = 0.3f, DroneSpeed = 520f, DroneReach = 300f, HiveDroneHeal = 4f, DroneBossEvery = 3f, DroneGiveUp = 4f;
        public const int DroneStings = 3;

        /// Host, from TickMinions (before the minions move): keep a player's drones up, one more coming every 0.4 s.
        void TickHiveDrones(Player p, float dt)
        {
            p.DroneBossCd -= dt;
            int want = p.Stats.HiveDrones, have = 0;
            Minion surplus = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.HiveDrone)
                {
                    have++;
                    surplus = m;
                }
            if (have > want && surplus != null)
                Minions.Remove(surplus.Id);  // a dev tool took the card away
            if (have >= want)
                return;
            p.DroneCd -= dt;
            if (p.DroneCd > 0f)
                return;
            var d = SpawnMinion(MinionKind.HiveDrone, p, p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 20f, -1f, true);  // a companion: it stays between waves, and is dazed (not broken) when worn out
            d.Ang = MathX.Tau * have / Math.Max(1, want);
            p.DroneCd = 0.4f;
        }

        /// Drones sent at once: one a stack (the first stack brings two drones, so one stays on guard).
        static int DronesPerSend(Player p) => Math.Max(1, p.Stats.HiveDrones - 1);

        /// Send the idle drones nearest the target out to sting it, one a stack (field changes only: safe inside any loop).
        void SendDrone(Player p, Enemy target)
        {
            for (int i = 0; i < DronesPerSend(p); i++)
                SendOneDrone(p, target);
        }

        void SendOneDrone(Player p, Enemy target)
        {
            Minion pick = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.HiveDrone && !m.Sent && m.StaggerT <= 0f
                    && (pick == null || m.Pos.DistanceSq(target.Pos) < pick.Pos.DistanceSq(target.Pos)))
                    pick = m;
            if (pick == null)
                return;
            pick.Sent = true;
            pick.Returning = false;
            pick.GoalEnemy = target.Id;
            pick.Stings = DroneStings;
            pick.Cd = 0f;
            pick.SentT = 0f;
        }

        /// KillEffects: a kill sends a drone at the nearest enemy within DroneReach of it.
        void DroneOnKill(Player p, Enemy dead)
        {
            if (p.Stats.HiveDrones <= 0)
                return;
            var next = BounceTarget(dead.Pos, dead.Id, DroneReach);
            if (next != null)
                SendDrone(p, next);
        }

        /// HurtEnemy: hitting a boss sends a drone at it every DroneBossEvery seconds (bosses are rarely killed).
        void DroneOnBossHit(Player p, Enemy e)
        {
            if (p.DroneBossCd > 0f)
                return;
            p.DroneBossCd = DroneBossEvery;
            SendDrone(p, e);
        }

        /// Host, each tick for a drone (instead of its usual moves): circle the owner; or fly out, sting, fly home.
        void MoveDrone(Minion m, Player owner, float dt)
        {
            V2 slot = owner.Pos + V2.FromAngle(m.Ang) * m.Def.OrbitRadius;
            float step = DroneSpeed * dt;
            if (!m.Sent)
            {
                m.Ang += m.Def.Speed * dt;
                m.Pos += (owner.Pos + V2.FromAngle(m.Ang) * m.Def.OrbitRadius - m.Pos).LimitLength(step);
                m.Aim = m.Ang + MathX.Pi / 2f;  // facing along its circle
                return;
            }
            if (m.Returning)
            {
                m.Ang += m.Def.Speed * dt;
                V2 back = slot - m.Pos;
                if (back.Length <= step + 4f)
                {
                    m.Pos = slot;
                    m.Sent = m.Returning = false;
                    if (!owner.Downed)
                        Heal(owner, HiveDroneHeal, owner);  // home: it heals you, as her drones heal her
                    return;
                }
                m.Pos += back.Normalized() * step;
                m.Aim = back.Angle;
                return;
            }
            m.SentT += dt;
            Enemy e;
            if (!Enemies.TryGetValue(m.GoalEnemy, out e) || e.Hp <= 0f || e.SpawnT > 0f)
            {
                e = m.SentT < DroneGiveUp ? BounceTarget(m.Pos, m.GoalEnemy, 200f) : null;  // its target died: the next one near, if any
                if (e == null)
                {
                    m.Returning = true;
                    return;
                }
                m.GoalEnemy = e.Id;
            }
            V2 to = e.Pos - m.Pos;
            float reach = e.Radius + m.Def.Radius + 4f;
            if (to.Length > reach + 2f)
            {
                m.Pos += to.Normalized() * Math.Min(step, to.Length - reach);
                m.Aim = to.Angle;
                if (m.SentT > DroneGiveUp)
                    m.Returning = true;
                return;
            }
            m.Ang += 7f * dt;  // buzzing round it while it stings
            m.Pos = e.Pos + V2.FromAngle(m.Ang) * reach;
            m.Aim = (e.Pos - m.Pos).Angle;
            m.Cd -= dt;
            if (m.Cd > 0f)
                return;
            m.Cd = DroneStingEvery;
            HurtEnemy(e, DroneSting * owner.Stats.DmgMult, owner.Id);
            BroadcastFx(FxKind.Spark, e.Pos + (m.Pos - e.Pos) * 0.5f, m.Def.Color, 10f, 0.15f, Sfx.None);
            if (--m.Stings <= 0)
                m.Returning = true;
        }

        // --- Bomber Escort -----------------------------------------------------------------

        public const float BomberEvery = 5f, BomberReach = 400f, BomberSpeed = 430f, BomberTurn = 7f, BomberLife = 2.5f;

        /// Host, from TickMinions: every BomberEvery seconds, while something is near, a bomber per stack sets off
        /// from you at the toughest enemy within BomberReach.
        void TickBombers(Player p, float dt)
        {
            if (p.Stats.Bombers <= 0 || p.Downed || State != MatchState.Fighting)
                return;
            p.BomberCd -= dt;
            if (p.BomberCd > 0f)
                return;
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && (e.Def.Xp > 0 || e.Def.IsBoss) && e.Pos.DistanceTo(p.Pos) < BomberReach + e.Radius
                    && (best == null || e.Hp > best.Hp))
                    best = e;
            if (best == null)
            {
                p.BomberCd = 0.5f;  // nothing near: look again shortly
                return;
            }
            p.BomberCd = BomberEvery;
            float at = (best.Pos - p.Pos).Angle;
            for (int i = 0; i < p.Stats.Bombers; i++)
            {
                float a = at + (i - (p.Stats.Bombers - 1) / 2f) * 0.6f;  // more stacks: they fan out, then curve in
                var b = SpawnMinion(MinionKind.HiveBomber, p, p.Pos + V2.FromAngle(a) * (p.Def.BodyR + 6f), BomberLife);
                b.GoalEnemy = best.Id;
                b.Aim = a;
            }
            Sounds.Add(new SoundCue { Sfx = Sfx.Summon, Ally = p.Id != MyId });
        }

        /// Host, each tick for a bomber: steer at its target (the next one near if it died); true when it bursts
        /// (it got there, or its fuse ran out).
        bool MoveBomber(Minion m, float dt)
        {
            Enemy e;
            if (!Enemies.TryGetValue(m.GoalEnemy, out e) || e.Hp <= 0f || e.SpawnT > 0f)
            {
                e = NearestEnemy(m.Pos, 300f);
                if (e != null)
                    m.GoalEnemy = e.Id;
            }
            if (e != null)
            {
                float want = (e.Pos - m.Pos).Angle;
                float diff = MathX.Fmod(want - m.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                m.Aim += Math.Max(-BomberTurn * dt, Math.Min(BomberTurn * dt, diff));
            }
            m.Pos += V2.FromAngle(m.Aim) * BomberSpeed * dt;
            return e != null && e.Pos.DistanceTo(m.Pos) < e.Radius + m.Def.Radius + 4f || m.T >= 0f && m.T < 0.05f;
        }

        // --- Royal Dive --------------------------------------------------------------------

        public const float RoyalDiveRange = 380f, RoyalDiveTime = 0.6f, RoyalDiveRadius = 110f, RoyalDiveDamage = 120f, RoyalDiveKnock = 100f;
        public const float DivePool = 100f, DivePoolTime = 4f, DivePoolMastered = 150f, DivePoolMasteredTime = 6f;

        /// Royal Dive, from the Hive Queen's legendary card (Q / E / Shift only). (Upgrades.Abilities gathers it.)
        internal static readonly AbilityDef[] QueenAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.RoyalDive,
                Name = "Royal Dive", Cooldown = 12f,
                Desc = "Leap (nothing can hit you) and crash down at your cursor, up to 380 px away: 120 damage to everything within 110 px, enemies knocked back and enemy bullets there wiped out. The landing leaves a honey pool for 4 s that slows enemies 40% and heals allies 6 a second.",
                Capstone = "A wider honey pool (150 px) that lasts 6 s.",
                Bot = b => b.HaveTarget && b.TargetD > 120f && b.TargetD < 380f || b.BotDanger > 2.5f,
            },
        };

        class Dive
        {
            public int Owner;
            public V2 Pos;
            public float T, Power;
            public bool Mastered;
        }

        readonly List<Dive> dives = new List<Dive>();

        /// Cast (CastAbility): the player's own game carries them to the landing (LocalControl, like Leap Slam);
        /// they can't be hit on the way, and the host lands the crash when they come down.
        void StartRoyalDive(Player p, V2 target, float power, bool mastered)
        {
            p.InvulnT = Math.Max(p.InvulnT, RoyalDiveTime + 0.1f);
            dives.Add(new Dive { Owner = p.Id, Pos = target, T = RoyalDiveTime, Power = power, Mastered = mastered });
            BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.HoneyPool).Color, 40f, 0.3f, Sfx.Blink);
        }

        /// Host, every tick (with the Knights' timers): dives land.
        void TickDives(float dt)
        {
            for (int i = dives.Count - 1; i >= 0; i--)
            {
                var dv = dives[i];
                dv.T -= dt;
                if (dv.T > 0f)
                    continue;
                dives.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(dv.Owner, out p))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(dv.Pos) > RoyalDiveRadius + e.Radius)
                        continue;
                    HurtEnemy(e, RoyalDiveDamage * p.Stats.DmgMult * dv.Power, p.Id);
                    if (!e.Def.IsBoss && !Heavy(e))
                    {
                        V2 away = (e.Pos - dv.Pos).IsZero ? V2.FromAngle(Rand(0f, MathX.Tau)) : (e.Pos - dv.Pos).Normalized();
                        e.Pos = ClampToArena(e.Pos + away * RoyalDiveKnock, e.Radius);
                    }
                }
                EraseEnemyBullets(dv.Pos, RoyalDiveRadius);
                AddZone(ZoneKind.HoneyPool, p, dv.Pos, dv.Mastered ? DivePoolMastered : DivePool, dv.Mastered ? DivePoolMasteredTime : DivePoolTime);
                BroadcastFx(FxKind.Boom, dv.Pos, Kits.Get(ZoneKind.HoneyPool).Color, RoyalDiveRadius, 0.4f, Sfx.Explode);
                BroadcastFx(FxKind.Ring, dv.Pos, new Rgba(1f, 0.9f, 0.55f), RoyalDiveRadius * 1.3f, 0.35f, Sfx.None);
            }
        }
    }
}

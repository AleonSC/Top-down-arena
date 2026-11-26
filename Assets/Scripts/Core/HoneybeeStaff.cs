using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Summoner DPS's Honeybee Staff (the DPS weapon on the lobby's 4th key; mockup: the Honeybee Staff artifact, variant C "Honey Harvest").
    ///   Fire: a bee hatches out of a wax cell at your cursor (no farther than HatchReach from you), buzzes BeeHatch + BeeHover where it
    ///     hatched, then darts at the enemy nearest it (within BeeSeek) and stings it once for the weapon's damage, and dies (MinionKind.Bee,
    ///     FxKind.BeeSting). With nothing to sting it hangs about and fizzles after BeeLife. At most BeeCap of your hatched bees are out.
    ///   Swarm: a sting that lands within SwarmGap of the last sting on the same enemy builds its chain (Enemy.BeeChain); from the
    ///     SwarmN-th on, stings hit SwarmMult harder.
    ///   Alt, held (Msg.WeaponDash, WeaponAlt.HoneyOn / WeaponAlt.HoneyOff): the staff sprays honey globs (Defs.HoneyGlob) in a short cone at your
    ///     cursor while the honey meter (Player.HoneyFill) lasts; they land as honey puddles (ZoneKind.HoneyPuddle, slow 40%), a glob that
    ///     lands in one of your puddles grows it and keeps it fresh. The meter refills HoneyRegenWait after you stop.
    ///   Harvest: an enemy that dies standing in your honey bursts open (FxKind.HoneyHarvest) and HarvestBees free bees fly out of it at the
    ///     enemies nearest (they don't count against BeeCap).
    ///   Extra projectiles hatch one more bee a click, fanned round the cursor; Bigger Hive (the weapon's card) adds a harvest bee and
    ///   widens the puddles 20%.
    public partial class Game
    {
        public const float BeeHatch = 0.15f, BeeHover = 0.3f, BeeLife = 4f, BeeSpeed = 360f, BeeTurn = 9f, BeeSeek = 320f, HatchReach = 380f;
        public const float SwarmGap = 0.5f, SwarmMult = 1.4f, BeeFan = 22f;
        public const int SwarmN = 3, BeeCap = 8, HarvestBees = 2, BeesMax = 24;
        public const float HoneyMax = 100f, HoneyDrain = 38f, HoneyRegen = 22f, HoneyRegenWait = 0.6f, HoneyMin = 6f;
        public const float HoneyRate = 16f, HoneySpread = 0.42f, HoneySpeed = 300f, HoneyRangeMin = 110f, HoneyRangeMax = 160f;
        public const float PuddleR = 22f, PuddleGrow = 2.5f, PuddleMaxR = 58f, PuddleTime = 3f, PuddleMerge = 0.8f;

        /// The whole wait before a fresh bee goes for an enemy (hatching, then buzzing on the spot).
        public static float BeeWait => BeeHatch + BeeHover;

        /// How far a bee looks for an enemy: farther with Homing Rounds (the weapon's HomingDesc).
        float BeeSeekRange(Player p) => BeeSeek * (1f + 0.125f * p.Stats.Homing);

        /// Fire: hatch a bee at the cursor (one more per extra projectile, fanned round it).
        void HatchBees(Player p, float dmg, int extra)
        {
            V2 at = p.AimAt;
            V2 off = at - p.Pos;
            if (off.Length > HatchReach)
                at = p.Pos + off.Normalized() * HatchReach;
            int n = 1 + extra, mine = 0;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Bee && !m.Mastered)
                    mine++;
            for (int i = 0; i < n && mine < BeeCap + 2 * extra; i++, mine++)
            {
                V2 spot = n == 1 ? at : at + V2.FromAngle(MathX.Tau * i / n + p.Aim) * BeeFan;
                var b = SpawnMinion(MinionKind.Bee, p, spot, BeeLife);
                b.Power = dmg * p.Stats.MinionDmgMult;  // (the volley's damage, with Pack Bond's)
                b.Hold = BeeWait;
                b.Wind = 0f;
                b.Aim = -MathX.Pi / 2f;
            }
            p.Run.ShotsFired++;
        }

        /// Harvest: a bee that flies out of a burst enemy, already moving (free: it doesn't count against BeeCap).
        void HatchFreeBee(Player p, V2 at, V2 vel)
        {
            int all = 0;
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.Bee)
                    all++;
            if (all >= BeesMax)
                return;
            var b = SpawnMinion(MinionKind.Bee, p, at, BeeLife);
            b.Power = p.Stats.DmgMult * p.Stats.MinionDmgMult;
            b.Mastered = true;  // marks a free bee (host only)
            b.Hold = 0f;
            b.Wind = 1f;
            b.Vel = vel;
            b.Aim = vel.Angle;
        }

        /// Host, each tick for a bee (instead of the usual moves). True when it is spent (it stung).
        bool MoveBee(Minion m, Player owner, float dt)
        {
            if (m.Hold > 0f)
            {
                m.Hold -= dt;
                m.Wind = MathX.Clamp01(1f - m.Hold / BeeWait);
                return false;  // hatching, then buzzing where it hatched (the look loops it about)
            }
            m.Wind = 1f;
            Enemy e;
            if (m.GoalEnemy == 0 || !TryFoe(m.GoalEnemy, out e) || !Targetable(e))
            {
                e = NearestEnemy(m.Pos, BeeSeekRange(owner), by: owner.Id);
                m.GoalEnemy = e != null ? e.Id : 0;
            }
            if (e == null)
            {
                m.Vel = m.Vel * (float)Math.Pow(0.05, dt);  // nothing to sting: it drifts to a stop and hangs about
                m.Pos += m.Vel * dt;
                return false;
            }
            V2 to = e.Pos - m.Pos;
            V2 want = to.Normalized() * BeeSpeed;
            m.Vel += (want - m.Vel) * Math.Min(1f, dt * BeeTurn);
            m.Pos += m.Vel * dt;
            if (!m.Vel.IsZero)
                m.Aim = m.Vel.Angle;
            if (e.Pos.FartherThan(m.Pos, e.Radius + m.Def.Radius + 3f))
                return false;
            Sting(m, owner, e, to);
            return true;
        }

        /// A bee reaches its enemy: one sting, the Swarm chain, and it dies.
        void Sting(Minion m, Player owner, Enemy e, V2 dir)
        {
            if (hostClock - e.BeeLastT > SwarmGap)
                e.BeeChain = 0;
            e.BeeChain++;
            e.BeeLastT = hostClock;
            bool swarm = e.BeeChain >= SwarmN;
            float dmg = Defs.WeaponOf(WeaponId.HoneybeeStaff).Damage * m.Power * (swarm ? SwarmMult : 1f);
            WeaponHurt(e, dmg, owner.Id);
            ModHit(e, owner.Id, dmg, Defs.SwarmShot, dir.Normalized());
            owner.Run.ShotsHit++;
            // Pos: where the bee died; Pos2: (the enemy's id, 1 the first Swarm sting, 0.5 a later one, 0 none); Radius: the way it flew
            BroadcastFx(FxKind.BeeSting, m.Pos, new V2(e.Id, swarm ? (e.BeeChain == SwarmN ? 1f : 0.5f) : 0f), Kits.Get(MinionKind.Bee).Color, dir.Angle, 0.9f, Sfx.None);
        }

        /// From the kill loop: an enemy that died in someone's honey bursts into free bees for the puddle's owner.
        void HoneyHarvest(Enemy dead)
        {
            foreach (var z in Zones.Values)
            {
                if (z.Kind != ZoneKind.HoneyPuddle || dead.Pos.FartherThan(z.Pos, z.Radius + dead.Radius * 0.4f))
                    continue;
                Player p;
                if (!Players.TryGetValue(z.Owner, out p) || p.Weapon.Id != WeaponId.HoneybeeStaff)
                    return;
                int n = HarvestBees + p.Stats.HarvestBonus;
                for (int i = 0; i < n; i++)
                {
                    float a = -MathX.Pi / 2f + (i - (n - 1) / 2f) * 1.1f + Rand(-0.25f, 0.25f);
                    HatchFreeBee(p, dead.Pos + V2.FromAngle(a) * 6f, V2.FromAngle(a) * 160f);
                }
                // Pos2: (the owner's id, bees); Radius: the enemy's size
                BroadcastFx(FxKind.HoneyHarvest, dead.Pos, new V2(p.Id, n), Kits.Get(ZoneKind.HoneyPuddle).Color, dead.Radius, 0.9f, Sfx.Summon);
                return;
            }
        }

        /// Host, every tick the Honeybee Staff is out (and you're up): the spray and the honey meter.
        void TickHoneybee(Player p, float dt)
        {
            if (p.HoneyOn && (p.StunT > 0f || p.SwallowT > 0f || p.HoneyFill <= 0f))
                p.HoneyOn = false;
            if (!p.HoneyOn)
            {
                if (p.HoneyHeld && p.HoneyFill <= 0f)
                    return;  // run dry with Alt still held: it refills once you let go
                p.HoneyWait -= dt;
                if (p.HoneyWait <= 0f)
                    p.HoneyFill = Math.Min(HoneyMax, p.HoneyFill + HoneyRegen * dt);
                return;
            }
            p.HoneyWait = HoneyRegenWait;
            p.HoneyFill = Math.Max(0f, p.HoneyFill - HoneyDrain * dt);
            p.HoneyGlobT -= dt;
            while (p.HoneyGlobT <= 0f)
            {
                p.HoneyGlobT += 1f / HoneyRate;
                SprayGlob(p);
            }
        }

        void SprayGlob(Player p)
        {
            float a = (p.AimAt - p.Pos).IsZero ? p.Aim : (p.AimAt - p.Pos).Angle;
            a += Rand(-HoneySpread, HoneySpread);
            float range = Rand(HoneyRangeMin, HoneyRangeMax), speed = HoneySpeed * Rand(0.9f, 1.1f);
            V2 dir = V2.FromAngle(a);
            var b = SpawnBullet(Defs.HoneyGlob, p.Pos + dir * (p.Def.BodyR + 14f), dir * speed, 0f, range / speed, 0f, p.Id);
            b.Burst = 1;  // lands where it stops (MoveBullets: LandHoney)
        }

        /// Host: a honey glob comes down: it grows a puddle of yours it lands in, or starts a new one.
        void LandHoney(Bullet b)
        {
            Player p;
            if (!Players.TryGetValue(b.Owner, out p))
                return;
            float size = p.Stats.PuddleSizeMult;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.HoneyPuddle && z.Owner == p.Id && b.Pos.CloserThan(z.Pos, z.Radius * PuddleMerge))
                {
                    z.Radius = Math.Min(PuddleMaxR * size, z.Radius + PuddleGrow * size);
                    z.T = Math.Max(z.T, PuddleTime);
                    return;
                }
            AddZone(ZoneKind.HoneyPuddle, p, b.Pos, PuddleR * size, PuddleTime);
        }

        /// The Honeybee Staff was put away (or you went down): the spray stops.
        void ClearHoneybee(Player p)
        {
            p.HoneyOn = p.HoneyHeld = false;
            p.HoneyGlobT = 0f;
        }

        /// Host: Alt pressed or let go with the Honeybee Staff.
        void HostHoney(Player p, bool on)
        {
            if (p.Weapon.Id != WeaponId.HoneybeeStaff)
                return;
            p.HoneyHeld = on;
            if (!on)
            {
                p.HoneyOn = false;
                return;
            }
            if (p.HoneyOn || p.StunT > 0f || p.SwallowT > 0f || p.HoneyFill < HoneyMin)
                return;
            p.HoneyOn = true;
            p.HoneyGlobT = 0f;
            BroadcastSound(Sfx.Throw);
        }

        /// Your own game: Alt with the Honeybee Staff is held, so both edges go to the host.
        bool LocalHoneySpray(Player me, bool pressed, bool released, V2 aimAt)
        {
            if (me.Weapon.Id != WeaponId.HoneybeeStaff)
                return false;
            if (!pressed && !released)
                return true;
            byte kind = pressed ? WeaponAlt.HoneyOn : WeaponAlt.HoneyOff;
            if (IsHost)
                HostWeaponDash(MyId, kind, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(kind);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// From HostWeaponDash: the Honeybee Staff's kinds.
        bool HostHoneyDash(Player p, int kind)
        {
            if (kind != WeaponAlt.HoneyOn && kind != WeaponAlt.HoneyOff)
                return false;
            HostHoney(p, kind == WeaponAlt.HoneyOn);
            return true;
        }

        // --- Network: the player's Honeybee Staff fields (SyncPlayer) ---

        void SyncHoneybee(NetSync s, Player p)
        {
            s.Float(ref p.HoneyFill);  // the Honeybee Staff
            s.Bool(ref p.HoneyOn);
        }
    }
}

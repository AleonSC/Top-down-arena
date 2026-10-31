using System;
using System.Collections.Generic;

namespace Arena
{
    /// Mage and summoner kits: their weapons, abilities, minions and ground zones (host only).
    public partial class Game
    {
        const float MinionChaseRange = 600f;
        public const float FireballBurn = 8f;  // the Fireball sets what it passes through burning: 8 a second (for FlameBurnTime)
        public const float FlameBurn = 5f, FlameBurnTime = 3f, FlamePierced = 1f / 3f;  // the Flamethrower sets what it hits burning: 5 a second for 3 s; after the first enemy a flame does a third

        // --- Weapons -----------------------------------------------------------

        /// Mage and summoner weapons. `extra` is the number of extra projectiles (see FireVolley).
        const float BearArc = 110f, BearSwipeGap = 0.12f;  // Bear Totem (its reach and damage are in Defs)
        const float BearBleed = 0.3f;              // a claw hit bleeds for 30% of its damage per second...
        const float BleedTime = 3f, BleedStacks = 5f;  // ...for 3 s, stacking up to five hits' worth

        /// Bear Totem: two quick claw swipes in front of you, raking one way then back, that make enemies bleed.
        /// Each extra projectile adds 15 px of reach and 25% more bleed.
        void FireBearClaws(Player p, float aim, float dmg, int extra)
        {
            p.SwingCount++;
            for (int k = 0; k < 2; k++)
            {
                float side = k == 0 ? 1f : -1f;
                var s = NewSwing(p, aim + side * 0.12f, p.Weapon.Reach + p.Weapon.Grow * extra, BearArc, p.Weapon.Damage * dmg);
                s.Delay = BearSwipeGap * k;
                s.ClawSide = side;
                s.Bleed = BearBleed * (1f + 0.25f * extra);
                s.NoCut = true;  // claws, not a blade: they don't cut bullets
                pendingSwings.Add(s);
            }
        }

        /// Bleeding: each hit adds to the damage per second (up to five hits' worth) and restarts the 3 s.
        void ApplyBleed(Enemy e, float dps, int owner)
        {
            if (dps <= 0f || e.Hp <= 0f)
                return;
            e.BleedDps = Math.Min(e.BleedDps + dps, dps * BleedStacks);
            e.BleedT = BleedTime;
            e.BleedBy = owner;
        }

        void TickBleed(Enemy e, float dt)
        {
            if (e.BleedT <= 0f)
                return;
            e.BleedT -= dt;
            if (e.BleedT <= 0f)
                e.BleedDps = 0f;
            if (Hungry(e) || e.SpawnT > 0f || SoulBound(e))
                return;
            DotDamage(e, e.BleedDps * dt, e.BleedBy);
        }

        void FireKitWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;  // its Speed, Life and Damage (Defs)
            switch (w.Id)
            {
                // Mage
                case WeaponId.Flamethrower:
                {
                    // A steady stream: two flames a burst (+1 and 25% more reach per extra projectile) that pass
                    // through enemies and set them burning.
                    float reach = w.Life * (1f + w.Grow * extra);
                    for (int i = 0; i < 2 + extra; i++)
                    {
                        var b = SpawnBullet(Defs.FlameShot, muzzle, V2.FromAngle(aim + Rand(-0.2f, 0.2f)) * (i == 0 ? w.Speed : Rand(0.8f * w.Speed, w.Speed)), 0f, reach, w.Damage * dmg, p.Id);  // the lead flame goes the whole way
                        b.Pierce += 99;
                        b.Ignite = FlameBurn * dmg;
                    }
                    break;
                }
                case WeaponId.FrostLance:
                {
                    // A great icicle juts out from you along your aim, then splinters off its sides into
                    // whatever stands beside it. Both slow.
                    dmg *= HomingCost(p);  // Homing Rounds widen the splinters instead (AreaMult)
                    var ice = NewSwing(p, aim, w.Reach + w.Grow * extra, 0f, w.Damage * dmg);
                    ice.HalfWidth = IcicleHalfWidth;
                    ice.Ice = 1;
                    ice.Slow = IcicleSlow;
                    ice.NoCut = true;
                    pendingSwings.Add(ice);
                    var shards = NewSwing(p, aim, w.Reach + w.Grow * extra, 0f, w.Damage * SplinterMult * dmg);
                    shards.HalfWidth = (SplinterReach + SplinterGrow * extra) * AreaMult(p);
                    shards.Hollow = IcicleHalfWidth;
                    shards.Delay = SplinterDelay;
                    shards.Ice = 2;
                    shards.Slow = IcicleSlow;
                    shards.NoCut = true;
                    pendingSwings.Add(shards);
                    break;
                }
                case WeaponId.StormHammer:  // most of its damage is the smash; the electrified enemy's arcs are a little extra
                    SpawnBullet(Defs.HammerShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * (1f + HammerGrow * extra) * dmg, p.Id).Shock = 3 + extra;
                    break;
                case WeaponId.RadiantOrb:
                    break;  // held on the cursor instead of fired (RadiantOrb.cs); extra projectiles become Light Orbs circling you (TickMinions)
                case WeaponId.ChainLight:
                    LightCone(p, aim, w.Damage * (1f + LightConeGrow * extra) * HomingCost(p) * dmg, extra);
                    break;
                case WeaponId.PrismBeam:
                {
                    // +30% length, +25% damage and +50% healing per extra projectile.
                    var b = SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + Rand(-0.03f, 0.03f)) * w.Speed, 0f, w.Life * (1f + w.Grow * extra),
                        w.Damage * (1f + 0.25f * extra) * dmg, p.Id);
                    b.HealAlly = 2f * (1f + 0.5f * extra);
                    b.Refract = true;  // through an ally it splits in three (RefractThroughAlly)
                    break;
                }
                case WeaponId.Fireball:
                {
                    // A big fireball that rolls on through everything, setting it all burning; extras fan out beside it.
                    for (int i = 0; i <= extra; i++)
                    {
                        var b = SpawnBullet(Defs.FireballShot, muzzle, V2.FromAngle(Fanned(aim, i, extra + 1, 0.22f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Pierce += 99;
                        b.Ignite = FireballBurn * dmg;
                    }
                    break;
                }
                case WeaponId.ArcLightning:
                    ArcField(p, aim, w.Damage * (1f + ArcGrow * extra) * HomingCost(p) * dmg, BowCharge(p, p.BowShotDraw));
                    break;
                case WeaponId.ArcaneMissiles:
                {
                    // Conjured into orbit round you (TickOrbiters launches them at enemies that come near). With the
                    // orbit full, the cast flies at your aim instead: fanned out, homing.
                    int n = 3 + extra;
                    if (p.Orbiters < OrbitCap(extra))
                    {
                        p.Orbiters = Math.Min(OrbitCap(extra), p.Orbiters + n);
                        break;
                    }
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + (i * 2f / (n - 1) - 1f) * 0.3f) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id, false, MissileTurn);
                    break;
                }
                // Summoner (extra projectiles mostly add companions: see CompanionCount)
                case WeaponId.GolemStaff:
                {
                    // Hold and let go: a boulder lobbed over enemies, landing farther (and harder) the longer you held.
                    float c = BowCharge(p, p.BowShotDraw), flight = BoulderFlight(c);
                    V2 vel = V2.FromAngle(aim) * (BoulderDistance(c) / flight);  // SpawnBullet adds Velocity Rounds, which throw it farther
                    var b = SpawnBullet(Defs.BoulderShot, p.Pos, vel, 0f, flight, w.Damage * (BoulderTap + (1f - BoulderTap) * c) * dmg, p.Id);
                    b.Splash = BoulderSplash * AreaMult(p);
                    b.Burst = 1;  // marks it as landing (MoveBullets)
                    break;
                }
                case WeaponId.BoneGuard:
                case WeaponId.WolfWhistle:
                    SpawnBullet(Defs.BoneShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.ImpCaller:
                    SendImps(p, BowCharge(p, p.BowShotDraw));  // a tap sends them; held to full, they merge into one big imp
                    break;
                case WeaponId.SpriteStaff:
                case WeaponId.TotemStaff:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.TreantRod:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).HealAlly = 7f;
                    break;
                case WeaponId.WispLantern:
                    for (int i = 0; i <= extra; i++)
                        SendWisp(p);  // your wisps are your shots: one a shot, plus one per extra projectile
                    break;
            }
        }

        /// Companions a player's weapon keeps around: the weapon's own, plus one per extra projectile
        /// (Golems top out at 3). The Radiant Orb gets Light Orbs; the Treant grows instead of multiplying.
        public MinionKind CompanionKind(Player p) => p.Weapon.Id == WeaponId.RadiantOrb ? MinionKind.LightOrb : p.Weapon.Companion;

        public int CompanionCount(Player p)
        {
            var w = p.Weapon;
            int extra = ExtraShots(p);
            switch (w.Id)
            {
                case WeaponId.RadiantOrb: return extra;
                case WeaponId.TreantRod: return w.CompanionCount;
                case WeaponId.BearTotem: return w.CompanionCount;  // the bear cleaves instead
                case WeaponId.GolemStaff: return Math.Min(3, w.CompanionCount + extra);
                default: return w.Companion == MinionKind.None ? 0 : w.CompanionCount + extra;
            }
        }

        /// Lightning jumps from the enemy that was hit to the nearest others.
        const float ChainReach = 170f;  // how far lightning jumps between enemies

        void ChainLightning(Enemy from, float dmg, int jumps, int owner, float reach = ChainReach)
        {
            var hit = new HashSet<int> { from.Id };
            Enemy cur = from;
            for (int j = 0; j < jumps; j++)
            {
                Enemy next = null;
                foreach (var e in Enemies.Values)
                    if (!hit.Contains(e.Id) && e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(cur.Pos) < reach &&
                        (next == null || e.Pos.DistanceSq(cur.Pos) < next.Pos.DistanceSq(cur.Pos)))
                        next = e;
                if (next == null)
                    break;
                HurtEnemy(next, dmg, owner);
                BroadcastFx(FxKind.Bolt, cur.Pos, next.Pos, Defs.BulletColor(Defs.BoltShot), 0f, 0.15f, Sfx.None);
                hit.Add(next.Id);
                cur = next;
            }
        }

        // --- Healer kit: the Censer's incense, Sprite Staff seeds, Totem Staff charge, Ofuda wards ---------

        public const float IncenseStep = 45f, IncenseRadius = 45f, IncenseTime = 3f, IncenseStray = 0.35f;  // dropped every IncenseStep you move; enemy shots fired inside stray this far either side
        public const float SeedTime = 4f, SpriteFreed = 6f, TotemThrow = 400f, TotemCharge = 6f, TotemPulse = 25f, TotemReach = 150f;
        public const int FreedCap = 4;

        bool InIncense(V2 pos)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Incense && pos.DistanceTo(z.Pos) <= z.Radius)
                    return true;
            return false;
        }

        /// A seed hit something: a Sprite Staff seed sticks in it; a Totem Staff seed charges your totems near it.
        void SeedHit(Bullet b, Enemy e)
        {
            Player p;
            if (b.Kind != Defs.SeedShot || !Players.TryGetValue(b.Owner, out p))
                return;
            if (p.Weapon.Id == WeaponId.SpriteStaff)
            {
                e.SeededBy = p.Id;
                e.SeededT = SeedTime;
            }
            else if (p.Weapon.Id == WeaponId.TotemStaff)
                foreach (var z in Zones.Values)
                    if (z.Kind == ZoneKind.Totem && z.Owner == p.Id && z.Pos.DistanceTo(e.Pos) <= TotemReach)
                    {
                        z.Charge += 1f;
                        if (z.Charge >= TotemCharge)
                        {
                            z.Charge = 0f;
                            foreach (var q in Players.Values)
                                if (!q.Downed && q.Pos.DistanceTo(z.Pos) <= TotemReach)
                                    Heal(q, TotemPulse, p);
                            BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, TotemReach, 0.5f, Sfx.SpellHoly);
                        }
                    }
        }

        /// A seeded enemy died: a sprite springs from it, healing whoever is hurt for a while.
        void SeededKill(Enemy e)
        {
            Player p;
            if (e.SeededT <= 0f || !Players.TryGetValue(e.SeededBy, out p) || p.Downed)
                return;
            int freed = 0;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Sprite && !m.Companion)
                    freed++;
            if (freed < FreedCap)
                SpawnMinion(MinionKind.Sprite, p, e.Pos, SpriteFreed);
        }

        /// Host, every tick: the Censer leaves incense as you move; wards burst under enemies; seeds wear off.
        void TickHealerKit(float dt)
        {
            foreach (var p in Players.Values)
                if (p.Weapon.Id == WeaponId.Censer && !p.Downed && p.Pos.DistanceTo(p.LastIncense) >= IncenseStep)
                {
                    p.LastIncense = p.Pos;
                    AddZone(ZoneKind.Incense, p, p.Pos, IncenseRadius, IncenseTime);
                }
            List<Zone> burst = null;
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.OfudaWard)
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(z.Pos) < z.Radius + e.Radius)
                        {
                            (burst = burst ?? new List<Zone>()).Add(z);
                            break;
                        }
            if (burst != null)
                foreach (var z in burst)
                {
                    BurstWard(z);  // Ninja.cs
                    Zones.Remove(z.Id);
                }
            foreach (var e in Enemies.Values)
                if (e.SeededT > 0f)
                    e.SeededT -= dt;
        }

        // --- Treant Rod: the vine ------------------------------------------------------------------

        public const float VineMax = 360f, VineRegroup = 160f, VineWidth = 14f, VineHeal = 8f, VineDps = 6f, VineSlow = 0.5f;

        /// The Treant roots where it is; once you're VineMax away it walks back toward you (to VineRegroup).
        V2 TreantGoal(Minion m, Player owner)
        {
            if (owner.Weapon.Id != WeaponId.TreantRod)
                return owner.Pos + V2.FromAngle(owner.Aim) * 45f;
            float d = m.Pos.DistanceTo(owner.Pos);
            if (d > VineMax)
                m.Returning = true;
            if (m.Returning && d <= VineRegroup)
                m.Returning = false;
            return m.Returning ? owner.Pos : m.Pos;
        }

        /// Host, every tick: the vine from each Treant Rod user to their Treant heals allies crossing it and slows
        /// (and stings) enemies crossing it.
        void TickVines(float dt)
        {
            foreach (var m in Minions.Values)
            {
                Player p;
                if (m.Kind != MinionKind.Treant || !m.Companion || !Players.TryGetValue(m.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.TreantRod)
                    continue;
                V2 a = p.Pos, b = m.Pos;
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed && DistToSegment(q.Pos, a, b) <= VineWidth + q.Def.BodyR)
                        Heal(q, VineHeal * dt, p);
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || DistToSegment(e.Pos, a, b) > VineWidth + e.Radius)
                        continue;
                    if (!e.Def.IsBoss)
                    {
                        e.SlowT = Math.Max(e.SlowT, 0.3f);
                        e.SlowMult = Math.Min(e.SlowMult, 1f - VineSlow);
                    }
                    DotDamage(e, VineDps * p.Stats.DmgMult * dt, p.Id);
                }
            }
        }

        static float DistToSegment(V2 pt, V2 a, V2 b)
        {
            V2 ab = b - a;
            float len2 = ab.X * ab.X + ab.Y * ab.Y;
            float k = len2 < 1f ? 0f : Math.Max(0f, Math.Min(1f, ((pt.X - a.X) * ab.X + (pt.Y - a.Y) * ab.Y) / len2));
            return pt.DistanceTo(a + ab * k);
        }

        // --- Wisp Lantern and Imp Caller: companions you send ---------------------------------------

        public const float WispReach = 420f, WispSpeed = 650f, SendPick = 120f, ImpReach = 600f, ImpSpeed = 300f, ImpMergeBonus = 1.25f, SentMax = 2.5f;

        /// What a sent companion goes for: the enemy nearest your cursor (within SendPick of it), or just the cursor.
        void PickGoal(Player p, Minion m, float reach)
        {
            V2 at = p.Pos + (p.AimAt - p.Pos).LimitLength(reach);
            var e = NearestEnemy(at, SendPick);
            m.GoalEnemy = e != null ? e.Id : 0;
            m.Goal = at;
            m.Sent = true;
            m.Returning = false;
            m.SentT = 0f;
        }

        /// The Wisp Lantern: a waiting wisp flies out at what you're aiming at, strikes it and comes back.
        void SendWisp(Player p)
        {
            Minion pick = null;
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Wisp && !m.Sent && (pick == null || AngleTo(p.Pos, m.Pos, p.Aim) < AngleTo(p.Pos, pick.Pos, p.Aim)))
                    pick = m;  // the one most in front of you
            if (pick == null)
                return;
            PickGoal(p, pick, WispReach);
            p.Run.ShotsFired++;
        }

        /// The Imp Caller: every imp waiting by you charges at what you're aiming at. Held to full charge (with two
        /// or more waiting), they merge first into one big imp whose blast is all of theirs and then some.
        void SendImps(Player p, float charge)
        {
            var idle = new List<Minion>();
            foreach (var m in Minions.Values)
                if (m.Owner == p.Id && m.Kind == MinionKind.Imp && !m.Sent)
                    idle.Add(m);
            if (idle.Count == 0)
                return;
            if (charge >= 1f && idle.Count >= 2)
            {
                var big = idle[0];
                big.Power = idle.Count * ImpMergeBonus;
                big.Scale = 1f + 0.25f * (idle.Count - 1);
                for (int i = 1; i < idle.Count; i++)
                    Minions.Remove(idle[i].Id);
                BroadcastFx(FxKind.Ring, big.Pos, big.Def.Color, 30f * big.Scale, 0.3f, Sfx.Ability);
                idle.RemoveRange(1, idle.Count - 1);
            }
            foreach (var m in idle)
                PickGoal(p, m, ImpReach);
        }

        /// Host, each tick for a Wisp Lantern wisp or Imp Caller imp (instead of its usual moves). Returns true if it
        /// explodes this tick (imps).
        bool MoveSent(Minion m, Player owner, float dt)
        {
            var d = m.Def;
            bool imp = m.Kind == MinionKind.Imp;
            if (!m.Sent)
            {
                if (imp)  // waiting by you, a little behind
                {
                    V2 home = owner.Pos + V2.FromAngle(owner.Aim + MathX.Pi + (m.Id % 5 - 2) * 0.5f) * 36f;
                    m.Pos += (home - m.Pos).LimitLength(d.Speed * dt);
                    m.Aim = owner.Aim;
                }
                else  // orbiting you
                {
                    m.Ang += d.Speed * dt;
                    m.Pos = owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius;
                }
                return false;
            }
            m.SentT += dt;
            Enemy e = null;
            if (m.GoalEnemy == 0 || !Enemies.TryGetValue(m.GoalEnemy, out e) || e.Hp <= 0f)
                e = null;  // sent at a point, or its target is gone
            bool alive = e != null;
            if (!imp && m.Returning)
            {
                V2 slot = owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius;
                V2 back = slot - m.Pos;
                if (back.Length < WispSpeed * dt + 4f)
                {
                    m.Pos = slot;
                    m.Sent = m.Returning = false;
                }
                else
                    m.Pos += back.Normalized() * WispSpeed * dt;
                return false;
            }
            V2 goal = alive ? e.Pos : m.Goal;
            V2 to = goal - m.Pos;
            float speed = imp ? ImpSpeed : WispSpeed;
            m.Aim = to.Angle;
            m.Pos += to.LimitLength(speed * dt);
            // What it reached: its target, or (on the way, for an imp) any enemy it runs into.
            Enemy hit = null;
            foreach (var o in Enemies.Values)
                if (o.SpawnT <= 0f && o.Hp > 0f && o.Pos.DistanceTo(m.Pos) < o.Radius + d.Radius * m.Scale + 4f && (imp || o == e))
                {
                    hit = o;
                    break;
                }
            bool arrived = to.Length <= speed * dt + 2f || m.SentT > SentMax;
            if (imp)
                return hit != null || arrived;  // it blows up on whatever it reached
            if (hit != null)
            {
                float dmg = owner.Weapon.Damage * owner.Stats.DmgMult * m.Power;
                HurtEnemy(hit, dmg, owner.Id);
                OnBulletHit(new Bullet { Pos = hit.Pos, Vel = to.Normalized(), Owner = owner.Id, Dmg = dmg, Kind = Defs.ArcaneShot }, hit);
                BroadcastFx(FxKind.Spark, hit.Pos, d.Color, hit.Radius + 10f, 0.25f, Sfx.None);
                owner.Run.ShotsHit++;
                m.Returning = true;
            }
            else if (arrived)
                m.Returning = true;  // nothing there: back it comes
            return false;
        }

        // --- Arcane Missiles: the stockpile -------------------------------------------------------

        public const float OrbitRadius = 34f, LaunchRange = 300f, LaunchEvery = 0.08f, MissileTurn = 6f;  // launching outpaces casting, so more missiles a cast is more damage
        public static int OrbitCap(int extra) => 6 + 2 * extra;

        /// Where the i-th of n orbiting missiles is now (the same formula draws them).
        public static V2 OrbiterPos(V2 center, int i, int n, float t) => center + V2.FromAngle(t * 3f + MathX.Tau * i / Math.Max(1, n)) * OrbitRadius;

        /// Host, every tick: while an enemy is within LaunchRange, orbiting missiles launch at the nearest, one every LaunchEvery.
        void TickOrbiters(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.LaunchCd -= dt;
                if (p.Orbiters <= 0)
                    continue;
                if (p.Downed || p.Weapon.Id != WeaponId.ArcaneMissiles)
                {
                    p.Orbiters = 0;
                    continue;
                }
                if (p.LaunchCd > 0f || State == MatchState.GameOver)
                    continue;
                var e = NearestEnemy(p.Pos, LaunchRange);
                if (e == null)
                    continue;
                V2 from = OrbiterPos(p.Pos, p.Orbiters - 1, p.Orbiters, AnimT);
                var w = p.Weapon;
                SpawnBullet(Defs.ArcaneShot, from, (e.Pos - from).Normalized() * w.Speed, 0f, w.Life, w.Damage * p.Stats.DmgMult, p.Id, false, MissileTurn);
                p.Orbiters--;
                p.LaunchCd = LaunchEvery / (1f + 0.35f * ExtraShots(p));  // more missiles a cast: they launch faster too
            }
        }

        // --- Prism Beam: refraction --------------------------------------------------------------

        public const float RefractFan = 0.28f, RefractLife = 0.3f;  // the split beams fan this far either side; they fly at least this long

        readonly List<Bullet> refracts = new List<Bullet>();

        /// After Collide's pass: each refracted Prism Beam becomes three, fanned out.
        void SplitRefracted()
        {
            foreach (var b in refracts)
            {
                Player owner = PlayerOrNull(b.Owner);
                float a = b.Vel.Angle, speed = b.Vel.Length / (owner != null ? owner.Stats.ShotSpeedMult : 1f);  // SpawnBullet adds Velocity Rounds back
                for (int k = -1; k <= 1; k++)
                {
                    var s = SpawnBullet(b.Kind, b.Pos, V2.FromAngle(a + k * RefractFan) * speed, 0f, b.Life, b.Dmg, b.Owner);
                    s.Dmg = b.Dmg;  // the same beam, split: no upgrade applies twice
                    s.LastHit = b.LastHit;
                }
                AddFx(FxKind.Spark, b.Pos, new Rgba(0.9f, 0.85f, 1f), 14f, 0.2f);
            }
            refracts.Clear();
        }

        /// A Prism Beam passing through an ally: it heals them (if hurt) and splits into three beams carrying on
        /// beyond them. The split beams don't split again.
        bool RefractThroughAlly(Bullet b)
        {
            foreach (var q in Players.Values)
            {
                float r = q.Def.BodyR + b.Radius;
                if (q.Id == b.Owner || q.Downed || b.Pos.DistanceSq(q.Pos) >= r * r)
                    continue;
                if (q.Hp < q.MaxHp)
                    Heal(q, b.HealAlly * (b.Dmg / 9f), PlayerOrNull(b.Owner));
                refracts.Add(new Bullet { Kind = b.Kind, Pos = q.Pos + b.Vel.Normalized() * r, Vel = b.Vel, Life = Math.Max(b.Life, RefractLife), Dmg = b.Dmg, Owner = b.Owner, LastHit = b.LastHit });
                return true;
            }
            return false;
        }

        // --- Railgun: the ion trail --------------------------------------------------------------

        public const float IonTrailTime = 1f, IonTrailDps = 25f, IonTrailWidth = 10f;

        class Trail
        {
            public int Owner;
            public V2 From, Dir;
            public float Speed, Len, Age, Dps;
        }

        readonly List<Trail> ionTrails = new List<Trail>();

        /// A Railgun shot charges the line it flies along: for a second after the round passes, anything touching
        /// that stretch takes IonTrailDps.
        void IonTrail(Player p, V2 from, V2 dir, float speed, float life, float dmg)
        {
            float len = speed * life;
            ionTrails.Add(new Trail { Owner = p.Id, From = from, Dir = dir, Speed = speed, Len = len, Dps = IonTrailDps * dmg });
            BroadcastFx(FxKind.IonTrail, from, from + dir * len, Defs.BulletColor(Defs.RailShot), speed, IonTrailTime + life, Sfx.None);
        }

        void TickIonTrails(float dt)
        {
            for (int i = ionTrails.Count - 1; i >= 0; i--)
            {
                var tr = ionTrails[i];
                tr.Age += dt;
                float head = Math.Min(tr.Len, tr.Speed * tr.Age), tail = Math.Max(0f, tr.Speed * (tr.Age - IonTrailTime));  // charged behind the round, for IonTrailTime
                if (tail >= tr.Len)
                {
                    ionTrails.RemoveAt(i);
                    continue;
                }
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f)
                        continue;
                    V2 rel = e.Pos - tr.From;
                    float along = Dot(rel, tr.Dir), across = Math.Abs(Dot(rel, tr.Dir.Orthogonal()));
                    if (along >= tail - e.Radius && along <= head + e.Radius && across <= IonTrailWidth + e.Radius)
                        HurtEnemy(e, tr.Dps * dt, tr.Owner);
                }
            }
        }

        // --- Wolf Whistle: the pack hunt ----------------------------------------------------------

        public const float PreyTime = 4f, PackWindow = 1f, PackTakedown = 20f, PackStun = 0.6f;  // marked for this long; every wolf biting within this; the takedown's damage per wolf

        readonly Dictionary<int, HashSet<int>> packBiters = new Dictionary<int, HashSet<int>>();

        /// A Wolf Whistle dart marks what it hit as the pack's prey.
        void WolfDartHit(Bullet b, Enemy e)
        {
            Player p;
            if (b.Kind != Defs.BoneShot || !Players.TryGetValue(b.Owner, out p) || p.Weapon.Id != WeaponId.WolfWhistle)
                return;
            if (p.PreyId != e.Id)
                p.PackT = 0f;
            p.PreyId = e.Id;
            p.PreyT = PreyTime;
        }

        Enemy Prey(Player p)
        {
            Enemy e;
            return p.PreyT > 0f && Enemies.TryGetValue(p.PreyId, out e) && e.Hp > 0f && e.SpawnT <= 0f ? e : null;
        }

        /// A wolf bit something: if it's the prey and every wolf has now bitten it within PackWindow, a takedown.
        void PackBite(Player owner, Minion wolf, Enemy e)
        {
            if (e.Id != owner.PreyId || owner.PreyT <= 0f)
                return;
            HashSet<int> bit;
            if (!packBiters.TryGetValue(owner.Id, out bit))
                packBiters[owner.Id] = bit = new HashSet<int>();
            if (owner.PackT <= 0f)
            {
                bit.Clear();
                owner.PackT = PackWindow;
            }
            bit.Add(wolf.Id);
            int wolves = 0;
            foreach (var m in Minions.Values)
                if (m.Owner == owner.Id && m.Kind == MinionKind.Wolf)
                    wolves++;
            if (bit.Count < Math.Max(2, wolves))
                return;
            HurtEnemy(e, PackTakedown * wolves * owner.Stats.DmgMult, owner.Id);
            if (!e.Def.IsBoss)
                e.FrozenT = Math.Max(e.FrozenT, PackStun);
            BroadcastFx(FxKind.Claw, e.Pos - new V2(30f, 0f), e.Pos + new V2(30f, 0f), new Rgba(0.85f, 0.85f, 0.95f), 0.9f, 0.3f, Sfx.SwingHeavy);
            bit.Clear();
            owner.PackT = 0f;
        }

        void TickPack(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.PackT = Math.Max(0f, p.PackT - dt);
                if (p.PreyT > 0f && ((p.PreyT -= dt) <= 0f || Prey(p) == null))
                {
                    p.PreyT = 0f;
                    p.PreyId = 0;
                }
            }
        }

        /// Arc Lightning: hold to charge a strip of ground in front of you, longer the longer you hold (out to half
        /// the arena); let go and lightning shocks everything in it.
        public const float ArcMin = 160f, ArcMax = Width * 0.5f, ArcHalfWidth = 45f, ArcStun = 0.4f, ArcGrow = 0.25f;  // lengths from your centre; half its width; stun (not bosses); +damage per extra projectile
        public static float ArcLength(Player p, float c) => (ArcMin + (ArcMax - ArcMin) * c) * p.Stats.ShotSpeedMult;  // Velocity Rounds stretch it
        public static float ArcWidth(Player p) => ArcHalfWidth * AreaMult(p);  // Homing Rounds widen it

        /// Barrage adds strips side by side, parallel to the first and alternating sides, so they never overlap: an
        /// enemy anywhere in them is shocked once.
        void ArcField(Player p, float aim, float dmg, float c)
        {
            float len = ArcLength(p, c), half = ArcWidth(p);
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            var bolt = Defs.BulletColor(Defs.BoltShot);
            int n = 1 + p.Stats.Fan;
            var shifts = new float[n];  // each strip's middle, sideways from yours
            for (int v = 0; v < n; v++)
                shifts[v] = v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * 2f * half * ((v + 1) / 2);
            for (int v = 0; v < n; v++)
                BroadcastFx(FxKind.ArcField, p.Pos + side * shifts[v], p.Pos + side * shifts[v] + dir * len, bolt, half, 0.35f,
                    v > 0 ? Sfx.None : c > 0.95f ? Sfx.BigExplode : Sfx.Shockwave);
            p.Run.ShotsFired++;
            bool any = false;
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                V2 rel = e.Pos - p.Pos;
                float along = Dot(rel, dir), across = Dot(rel, side);
                if (along < -e.Radius - p.Def.BodyR || along > len + e.Radius)
                    continue;
                int strip = -1;
                for (int v = 0; v < n && strip < 0; v++)
                    if (Math.Abs(across - shifts[v]) <= half + e.Radius)
                        strip = v;
                if (strip < 0)
                    continue;
                if (BlocksShot(e, p.Pos))
                {
                    WearShield(e, dmg);
                    continue;
                }
                any = true;
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.BoltShot }, e);
                if (!e.Def.IsBoss)
                    e.FrozenT = Math.Max(e.FrozenT, ArcStun);
                BroadcastFx(FxKind.Bolt, p.Pos + side * shifts[strip] + dir * Math.Max(0f, Math.Min(len, along)), e.Pos, bolt, 0f, 0.2f, Sfx.None);  // from its strip's middle line
            }
            if (any)
                p.Run.ShotsHit++;
        }

        /// Chain Light: a great cone of light in front of you. Its damage is shared out between every enemy in it,
        /// so one enemy alone takes all of it; light arcs from you to each.
        public const float LightConeReach = 320f, LightConeHalf = 0.6f, LightConeGrow = 0.25f;  // reach from your centre; half its angle (about 70 degrees across); +damage per extra projectile
        public static float LightConeLength(Player p, int extra) => LightConeReach * (1f + 0.15f * extra);

        /// Barrage adds cones edge to edge beside the first, alternating sides (evenly round you if they'd go all the
        /// way round). Each enemy belongs to one cone only, the one it's nearest the middle of, and each cone shares
        /// its own damage out, so Barrage reaches more enemies but a lone one still takes one cone's worth.
        void LightCone(Player p, float aim, float total, int extra)
        {
            float reach = LightConeLength(p, extra), half = LightConeHalf * AreaMult(p);  // Homing Rounds widen it
            int n = 1 + p.Stats.Fan;
            float step = Math.Min(2f * half, MathX.Tau / n);
            var aims = new float[n];
            for (int v = 0; v < n; v++)
                aims[v] = aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * step * ((v + 1) / 2));
            var light = Defs.BulletColor(Defs.HolyShot);
            foreach (float a in aims)
                BroadcastFx(FxKind.LightCone, p.Pos, p.Pos + V2.FromAngle(a) * reach, light, half, 0.3f, Sfx.None);
            var hits = new List<Enemy>[n];
            for (int v = 0; v < n; v++)
                hits[v] = new List<Enemy>();
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                float d = e.Pos.DistanceTo(p.Pos);
                if (d - e.Radius > reach)
                    continue;
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi;
                int best = -1;
                float bestOff = float.MaxValue;
                for (int v = 0; v < n; v++)
                {
                    float off = AngleTo(p.Pos, e.Pos, aims[v]);
                    if ((off <= half + slack || d <= e.Radius + p.Def.BodyR) && off < bestOff)
                    {
                        best = v;
                        bestOff = off;
                    }
                }
                if (best < 0)
                    continue;
                if (BlocksShot(e, p.Pos))
                    WearShield(e, total);
                else
                    hits[best].Add(e);
            }
            p.Run.ShotsFired++;
            bool any = false;
            for (int v = 0; v < n; v++)
            {
                if (hits[v].Count == 0)
                    continue;
                any = true;
                float share = total / hits[v].Count;
                V2 dir = V2.FromAngle(aims[v]), from = p.Pos + dir * p.Def.BodyR;
                foreach (var e in hits[v])
                {
                    HurtEnemy(e, share, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = share, Kind = Defs.HolyShot }, e);  // weapon mods apply to each share
                    BroadcastFx(FxKind.Bolt, from, e.Pos, light, 0f, 0.2f, Sfx.None);
                }
            }
            if (any)
                p.Run.ShotsHit++;
        }

        /// Bone Guard: darts stick in enemies; the third within BoneDartHold cages it (not bosses). Caged enemies
        /// can't move or shoot, and the Skeleton Guards shoot at them instead of biting.
        public const int BoneCageHits = 3;
        public const float BoneDartHold = 3f, BoneCageTime = 1.5f, BoneCageImmune = 1f, GuardShotRange = 320f;

        void BoneDartHit(Bullet b, Enemy e)
        {
            Player p;
            if (b.Kind != Defs.BoneShot || !Players.TryGetValue(b.Owner, out p) || p.Weapon.Id != WeaponId.BoneGuard
                || e.Def.IsBoss || e.CageT > 0f || e.CageImmuneT > 0f)
                return;
            e.BoneDartT = BoneDartHold;
            if (++e.BoneDarts < BoneCageHits)
                return;
            e.BoneDarts = 0;
            e.CageT = BoneCageTime;
            e.Caged = true;
            BroadcastFx(FxKind.Ring, e.Pos, Defs.BulletColor(Defs.BoneShot), e.Radius + 16f, 0.3f, Sfx.Hit);
        }

        /// Host, every tick for each enemy: the darts fall out in time, and a cage breaks.
        void TickBoneCage(Enemy e, float dt)
        {
            if (e.BoneDarts > 0 && (e.BoneDartT -= dt) <= 0f)
                e.BoneDarts = 0;
            e.CageImmuneT -= dt;
            if (e.CageT > 0f && (e.CageT -= dt) <= 0f)
            {
                e.CageImmuneT = BoneCageImmune;
                BroadcastFx(FxKind.Spark, e.Pos, Defs.BulletColor(Defs.BoneShot), e.Radius + 10f, 0.25f, Sfx.None);
            }
            e.Caged = e.CageT > 0f;
        }

        /// The nearest caged enemy a Skeleton Guard can shoot at, if any.
        Enemy NearestCaged(V2 from)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (e.CageT > 0f && e.Hp > 0f && e.Pos.DistanceTo(from) < GuardShotRange && (best == null || e.Pos.DistanceSq(from) < best.Pos.DistanceSq(from)))
                    best = e;
            return best;
        }

        /// The Golem Staff's boulder: how far it lands (from your centre, before Velocity Rounds) and how long it
        /// flies for a charge `c` (0 = a tap, 1 = held to full).
        public const float BoulderMin = 140f, BoulderMax = 460f, BoulderSplash = 75f, BoulderTap = 0.5f;
        public static float BoulderDistance(float c) => BoulderMin + (BoulderMax - BoulderMin) * c;
        public static float BoulderFlight(float c) => 0.4f + 0.3f * c;

        /// A boulder landed: it crushes every enemy under it (weapon mods apply to each, as on a melee hit).
        void LandBoulder(Bullet b)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius && !BlocksShot(e, b.Pos))
                {
                    HurtEnemy(e, b.Dmg, b.Owner);
                    OnBulletHit(b, e);
                }
            BroadcastFx(FxKind.Boom, b.Pos, Defs.BulletColor(Defs.BoulderShot), b.Splash, 0.35f, Sfx.Explode);
            BroadcastFx(FxKind.Ring, b.Pos, new Rgba(0.85f, 0.78f, 0.65f), b.Splash * 1.2f, 0.3f, Sfx.None);
        }

        /// An enemy the Storm Hammer electrified: it arcs lightning to the enemies round it a few times.
        class Shock
        {
            public int Enemy, Owner, Jumps, Pulses;
            public float T, Dmg, Reach;
        }

        readonly List<Shock> shocks = new List<Shock>();
        public const float HammerArcMult = 0.125f, ShockEvery = 0.5f;  // each arc's share of the hammer's damage; time between arcs
        public const float HammerGrow = 0.15f;                         // more hammer damage per extra projectile
        public const int ShockPulses = 3;                              // arcs per electrified enemy (the first as the hammer lands)

        /// The Storm Hammer landed: `e` is electrified (hit again, it starts over).
        void Electrify(Enemy e, float hammerDmg, int jumps, int owner)
        {
            shocks.RemoveAll(s => s.Enemy == e.Id);
            Player p;
            float reach = ChainReach * (Players.TryGetValue(owner, out p) ? AreaMult(p) : 1f);  // Homing Rounds: the arcs reach further
            var sh = new Shock { Enemy = e.Id, Owner = owner, Jumps = jumps, Pulses = ShockPulses, Dmg = hammerDmg * HammerArcMult, Reach = reach };
            shocks.Add(sh);
            ShockPulse(sh, e);
            sh.T = ShockEvery;
        }

        /// One arc: lightning from the electrified enemy to the nearest few round it.
        void ShockPulse(Shock sh, Enemy from)
        {
            sh.Pulses--;
            BroadcastFx(FxKind.Spark, from.Pos, Defs.BulletColor(Defs.HammerShot), from.Radius + 14f, 0.3f, Sfx.None);
            var near = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e != from && e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(from.Pos) < sh.Reach + e.Radius)
                    near.Add(e);
            near.Sort((a, b) => a.Pos.DistanceSq(from.Pos).CompareTo(b.Pos.DistanceSq(from.Pos)));
            for (int i = 0; i < near.Count && i < sh.Jumps; i++)
            {
                HurtEnemy(near[i], sh.Dmg, sh.Owner);
                BroadcastFx(FxKind.Bolt, from.Pos, near[i].Pos, Defs.BulletColor(Defs.BoltShot), 0f, 0.15f, Sfx.None);
            }
        }

        /// Host, every tick: electrified enemies arc again every ShockEvery until they've arced ShockPulses times.
        void TickShocks(float dt)
        {
            for (int i = shocks.Count - 1; i >= 0; i--)
            {
                var sh = shocks[i];
                Enemy e;
                if (!Enemies.TryGetValue(sh.Enemy, out e) || e.Hp <= 0f || sh.Pulses <= 0)
                {
                    shocks.RemoveAt(i);
                    continue;
                }
                if ((sh.T -= dt) > 0f)
                    continue;
                ShockPulse(sh, e);
                sh.T = ShockEvery;
            }
        }

        // --- Abilities ---------------------------------------------------------

        /// The Mage's Space abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] MageAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ArcaneAegis, Kit = Kit.Mage, Role = Role.Tank, Choice = 0,
                Name = "Arcane Aegis", Cooldown = 11f, Desc = "For 3 s, enemy bullets that touch you are reflected back as your own.",
                Capstone = "Lasts 4.5 s and reflects bullets for double damage.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.FrostNova, Kit = Kit.Mage, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Frost Nova", Cooldown = 12f, Desc = "Freeze every enemy near you for 3 s and deal 40 damage.",
                Capstone = "Also erases enemy bullets around you.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.FlameWall, Kit = Kit.Mage, Role = Role.Tank, Choice = 2,
                Name = "Flame Wall", Cooldown = 14f, Desc = "A wall of fire at your cursor for 5 s: burns enemies and blocks bullets.",
                Capstone = "The fire also slows enemies by 40%.",
            },
            new AbilityDef
            {
                Id = AbilityId.TimeRewind, Kit = Kit.Mage, Role = Role.Healer, Choice = 0, Instant = true,
                Name = "Time Rewind", Cooldown = 22f, Desc = "Set everyone's health back to where it was 4 s ago, even undoing a downing.",
                Capstone = "Also erases enemy bullets around every player.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.ArcaneWard, Kit = Kit.Mage, Role = Role.Healer, Choice = 1, Instant = true,
                Name = "Arcane Ward", Cooldown = 14f, Desc = "Shield you and allies within 300 px for 60 damage for 8 s. When a shield breaks or runs out, half of what it soaked comes back as healing.",
                Capstone = "The shield soaks 120 damage.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Sanctify, Kit = Kit.Mage, Role = Role.Healer, Choice = 2, Instant = true,
                Name = "Sanctify", Cooldown = 12f, Desc = "Erase enemy bullets around you and heal everyone nearby for 25.",
                Capstone = "Also freezes enemies around you solid for 1.5 s (not bosses).",
                Bot = b => b.AllyHurt || b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Meteor, Kit = Kit.Mage, Role = Role.Dps, Choice = 0,
                Name = "Meteor", Cooldown = 10f, Desc = "Call a meteor at your cursor: after a short warning it hits for 150 damage.",
                Capstone = "Leaves burning ground for 3 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blizzard, Kit = Kit.Mage, Role = Role.Dps, Choice = 1,
                Name = "Blizzard", Cooldown = 14f, Desc = "A storm at your cursor for 5 s that slows and damages enemies.",
                Capstone = "Lasts 10 s.",
            },
            new AbilityDef
            {
                Id = AbilityId.ArcaneSurge, Kit = Kit.Mage, Role = Role.Dps, Choice = 2,
                Name = "Arcane Surge", Cooldown = 16f, Desc = "For 5 s your shots deal 60% more damage and pierce 2 more enemies.",
                Capstone = "+1 extra projectile while it lasts.",
            },
        };

        /// The Summoner's Space abilities (its ultimates are in Ultimates.cs). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] SummonerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.GolemWall, Kit = Kit.Summoner, Role = Role.Tank, Choice = 0,
                Name = "Golem Wall", Cooldown = 13f, Desc = "Raise three stone walls in front of you for 6 s that block bullets.",
                Capstone = "Raises five walls.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Earthquake, Kit = Kit.Summoner, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Earthquake", Cooldown = 10f, Desc = "You and every minion you have slam the ground, damaging and slowing enemies.",
                Capstone = "Also freezes the enemies it hits for 1 s (not bosses).",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.GuardianSpirit, Kit = Kit.Summoner, Role = Role.Tank, Choice = 2,
                Name = "Guardian Spirit", Cooldown = 18f, Desc = "A guardian follows you for 8 s and absorbs every bullet near it.",
                Capstone = "The guardian also heals you 6 HP/s.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.SpiritSwarm, Kit = Kit.Summoner, Role = Role.Healer, Choice = 0,
                Name = "Spirit Swarm", Cooldown = 14f, Desc = "Release 5 healing sprites for 6 s.",
                Capstone = "Releases 8 sprites.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Phoenix, Kit = Kit.Summoner, Role = Role.Healer, Choice = 1,
                Name = "Phoenix", Cooldown = 22f, Desc = "A phoenix fights for 10 s. If anyone goes down (you too), it revives them instantly.",
                Capstone = "Revives at full health, and the phoenix keeps fighting.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.SoulLantern, Kit = Kit.Summoner, Role = Role.Healer, Choice = 2,
                Name = "Soul Lantern", Cooldown = 20f, Desc = "For 5 s, allies near the lantern can't go down.",
                Capstone = "The lantern follows you.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.BatSwarm, Kit = Kit.Summoner, Role = Role.Dps, Choice = 0,
                Name = "Bat Swarm", Cooldown = 12f, Desc = "Release 8 bats that hunt enemies for 6 s.",
                Capstone = "Releases 14 bats.",
            },
            new AbilityDef
            {
                Id = AbilityId.Sacrifice, Kit = Kit.Summoner, Role = Role.Dps, Choice = 1, Instant = true,
                Name = "Sacrifice", Cooldown = 10f, Desc = "All your minions explode; your companions return 3 s later.",
                Capstone = "Double explosion damage, and your companions return at once.",
            },
            new AbilityDef
            {
                Id = AbilityId.AlphaCall, Kit = Kit.Summoner, Role = Role.Dps, Choice = 2,
                Name = "Alpha Call", Cooldown = 14f, Desc = "Your minions frenzy for 6 s: faster, and they attack twice as often.",
                Capstone = "Also summons 3 wolves for 6 s.",
            },
        };

        void KitAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            float dmg = s.DmgMult;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                // Mage
                case AbilityId.ArcaneAegis:
                    p.AegisT = cap ? 4.5f : 3f;
                    p.AegisHp = AegisCapacity * pw;
                    p.AegisReflectMult = pw * (cap ? 2f : 1f);
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.ArcaneShot), 50f, 0.4f, Sfx.Ability);
                    break;
                case AbilityId.FrostNova:
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > 220f + e.Radius)
                            continue;
                        HurtEnemy(e, 40f * dmg, p.Id);
                        e.SlowT = 3f;
                        e.SlowMult = 0.1f;  // nearly frozen
                    }
                    if (cap)
                        EraseEnemyBullets(p.Pos, 220f);
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), 220f, 0.5f, Sfx.Freeze);
                    break;
                case AbilityId.FlameWall:
                    AddZone(ZoneKind.FlameWall, p, target, 90f, 5f).ExtraSlow = cap ? 0.4f : 0f;
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.FlameWall).Color, 90f, 0.4f, Sfx.SpellFire);
                    break;
                case AbilityId.TimeRewind:
                    foreach (var q in Players.Values)
                    {
                        // The oldest entry in the ring buffer is from 4 seconds ago.
                        float hpThen = q.HpHistory[q.HistoryIdx];
                        bool downThen = q.DownHistory[q.HistoryIdx];
                        if (q.Downed && !downThen)
                        {
                            q.Downed = false;
                            q.Revive = 0f;
                            q.InvulnT = 1.5f;
                            q.Hp = Math.Max(1f, hpThen);
                        }
                        else if (!q.Downed)
                            q.Hp = Math.Max(q.Hp, Math.Min(q.MaxHp, hpThen));
                        if (!q.Downed)
                            castHits++;
                        if (cap)
                            EraseEnemyBullets(q.Pos, 200f);
                        BroadcastFx(FxKind.Ring, q.Pos, Defs.BulletColor(Defs.ArcaneShot), 60f, 0.6f, Sfx.None);
                    }
                    Sounds.Add(Sfx.Ability);
                    break;
                case AbilityId.ArcaneWard:
                    foreach (var q in Players.Values)
                        if (!q.Downed && q.Pos.DistanceTo(p.Pos) < 300f)
                        {
                            if (q.ArcaneOn)
                                ArcanePayback(q);  // a fresh ward pays out the old one first
                            q.ShieldHp = (cap ? 120f : 60f) * pw;
                            q.ShieldT = 8f;
                            q.ArcaneOn = true;
                            q.ArcaneStore = 0f;
                            q.ArcaneBy = p.Id;
                            castHits++;
                        }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.7f, 1f), 300f, 0.5f, Sfx.Ability);
                    break;
                case AbilityId.Sanctify:
                {
                    EraseEnemyBullets(p.Pos, 280f);
                    foreach (var q in Players.Values)
                        if (q.Pos.DistanceTo(p.Pos) < 280f)
                            Heal(q, 25f, p);
                    if (cap)
                        Freeze(p.Pos, 280f, 1.5f);
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.HolyShot), 280f, 0.5f, Sfx.SpellHoly);
                    break;
                }
                case AbilityId.Meteor:
                    AddZone(ZoneKind.Meteor, p, target, 120f, 0.9f).Embers = cap;  // the zone is the warning; it hits when it expires
                    Sounds.Add(Sfx.SpellFire);
                    break;
                case AbilityId.Blizzard:
                    AddZone(ZoneKind.Blizzard, p, target, 130f, cap ? 10f : 5f);
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.Blizzard).Color, 130f, 0.4f, Sfx.Freeze);
                    break;
                case AbilityId.ArcaneSurge:
                    p.SurgeT = 5f * pw;
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.ArcaneShot), 70f, 0.4f, Sfx.Ability);
                    break;
                // Summoner
                case AbilityId.GolemWall:
                {
                    V2 fwd = V2.FromAngle(p.Aim), side = fwd.Orthogonal();
                    int half = cap ? 2 : 1;
                    for (int i = -half; i <= half; i++)
                        SpawnMinion(MinionKind.Wall, p, p.Pos + fwd * 90f + side * (i * 55f), 6f);
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Earthquake:
                {
                    var centers = new List<V2> { p.Pos };
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id)
                            centers.Add(m.Pos);
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        foreach (var c in centers)
                            if (e.Pos.DistanceTo(c) < 140f + e.Radius)
                            {
                                HurtEnemy(e, 40f * dmg, p.Id);
                                e.SlowT = Math.Max(e.SlowT, 2f);
                                e.SlowMult = Math.Min(e.SlowMult, 0.5f);
                                if (cap)
                                    Freeze(e.Pos, 0f, 1f);
                                break;
                            }
                    }
                    foreach (var c in centers)
                        BroadcastFx(FxKind.Boom, c, new Rgba(0.7f, 0.6f, 0.45f), 140f, 0.4f, Sfx.None);
                    Sounds.Add(Sfx.Explode);
                    break;
                }
                case AbilityId.GuardianSpirit:
                    SpawnMinion(MinionKind.Guardian, p, p.Pos, 8f);  // mastered: also heals you (TickMinions)
                    Sounds.Add(Sfx.Summon);
                    break;
                case AbilityId.SpiritSwarm:
                {
                    int sprites = cap ? 8 : 5;
                    for (int i = 0; i < sprites; i++)
                        SpawnMinion(MinionKind.Sprite, p, p.Pos + V2.FromAngle(MathX.Tau * i / sprites) * 20f, 6f).HealRate = 10f * pw;
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Phoenix:
                    SpawnMinion(MinionKind.Phoenix, p, p.Pos, 10f);
                    Sounds.Add(Sfx.Summon);
                    break;
                case AbilityId.SoulLantern:
                    AddZone(ZoneKind.Lantern, p, p.Pos, 120f, 5f).Follow = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, Kits.Get(ZoneKind.Lantern).Color, 120f, 0.5f, Sfx.Ability);
                    break;
                case AbilityId.BatSwarm:
                {
                    int bats = cap ? 14 : 8;
                    for (int i = 0; i < bats; i++)
                        SpawnMinion(MinionKind.Bat, p, p.Pos + V2.FromAngle(MathX.Tau * i / bats) * 15f, 6f);
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Sacrifice:
                {
                    var mine = new List<Minion>();
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id)
                            mine.Add(m);
                    foreach (var m in mine)
                        ExplodeMinion(m, 60f * dmg * (cap ? 2f : 1f), 90f);
                    p.CompanionCd = cap ? 0f : 3f;
                    break;
                }
                case AbilityId.AlphaCall:
                    p.FrenzyT = 6f * pw;
                    if (cap)
                        for (int i = 0; i < 3; i++)
                            SpawnMinion(MinionKind.Wolf, p, p.Pos + V2.FromAngle(MathX.Tau * i / 3f) * 30f, 6f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 120f, 0.4f, Sfx.Roar);
                    break;
                default:
                    if (!KnightAbility(p, id, target) && !NinjaAbility(p, id, target) && !BloodAbility(p, id, target))  // Knight.cs, Ninja.cs, BloodMage.cs
                        Ultimate(p, id, target);  // Ultimates.cs
                    break;
            }
        }

        // --- Minions -----------------------------------------------------------

        Minion SpawnMinion(MinionKind kind, Player owner, V2 pos, float life, bool companion = false)
        {
            var m = new Minion
            {
                Id = nextMinionId++, Kind = kind, Owner = owner.Id, Pos = ClampToArena(pos, 10f), T = life, Duration = life,
                Companion = companion, Ang = Rand(0f, MathX.Tau), Aim = owner.Aim,
            };
            m.Disp = m.Pos;
            m.HealRate = m.Def.HealRate;
            m.BlockHp = m.Def.BlockCapacity;
            if (casting)
            {
                m.Power = castPower;
                m.Mastered = castMastered;
                m.HealRate *= castPower;
                m.BlockHp *= castPower;
            }
            Minions[m.Id] = m;
            return m;
        }

        void RemoveTemporaryMinions()
        {
            var gone = new List<int>();
            foreach (var m in Minions.Values)
                if (!m.Companion)
                    gone.Add(m.Id);
            foreach (int id in gone)
                Minions.Remove(id);
        }

        void ExplodeMinion(Minion m, float dmg, float radius)
        {
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < radius + e.Radius)
                    HurtEnemy(e, dmg, m.Owner);
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, radius, 0.35f, Sfx.Explode);
            Minions.Remove(m.Id);
        }

        Enemy NearestEnemy(V2 pos, float range)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (e.SpawnT <= 0f && e.Pos.DistanceTo(pos) < range && (best == null || e.Pos.DistanceSq(pos) < best.Pos.DistanceSq(pos)))
                    best = e;
            return best;
        }

        void TickMinions(float dt)
        {
            // Summoner weapons keep their companions topped up; the totem staff plants totems.
            foreach (var p in Players.Values)
            {
                TickGuards(p, dt);  // Warden's Guard (Stun.cs)
                var w = p.Weapon;
                var kind = CompanionKind(p);
                int extra = ExtraShots(p);
                if (kind != MinionKind.None)
                {
                    int want = CompanionCount(p), have = 0;
                    Minion surplus = null;
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id && m.Companion && m.Kind != MinionKind.WardenGuard)
                        {
                            have++;
                            surplus = m;
                            float scale = m.Kind == MinionKind.Treant ? 1f + 0.3f * extra : 1f;
                            if (scale != m.Scale)
                            {
                                if (m.StaggerT <= 0f)
                                    m.BlockHp *= scale / m.Scale;  // keeps the same fraction of a bigger guard
                                m.Scale = scale;
                            }
                        }
                    if (have > want && surplus != null && kind != MinionKind.Imp)
                        Minions.Remove(surplus.Id);  // Bullet Storm's extras leave when it ends
                    p.CompanionCd -= dt;
                    if (have < want && p.CompanionCd <= 0f)
                    {
                        var m = SpawnMinion(kind, p, p.Pos + V2.FromAngle(Rand(0f, MathX.Tau)) * 30f, -1f, true);
                        if (kind == MinionKind.LightOrb)
                            m.Ang = MathX.Tau * have / Math.Max(1, want);
                        p.CompanionCd = kind == MinionKind.Imp ? w.CompanionRespawn / (1f + 0.5f * extra) : 0.3f;
                    }
                }
                if (w.Id == WeaponId.TotemStaff && !p.Downed && State == MatchState.Fighting)
                {
                    p.TotemCd -= dt;
                    if (p.TotemCd <= 0f && p.Firing)  // ready: the next shot throws it to your cursor
                    {
                        p.TotemCd = 10f / (1f + 0.6f * extra);
                        AddZone(ZoneKind.Totem, p, ClampToArena(p.Pos + (p.AimAt - p.Pos).LimitLength(TotemThrow), 20f), 90f, 8f);
                    }
                }
            }

            var expired = scratchMinionsExpired;
            var exploding = scratchMinionsExploding;
            expired.Clear();
            exploding.Clear();
            foreach (var m in Minions.Values)
            {
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner))
                {
                    expired.Add(m);
                    continue;
                }
                if (m.T >= 0f)
                {
                    m.T -= dt;
                    if (m.T <= 0f)
                    {
                        expired.Add(m);
                        continue;
                    }
                }
                var d = m.Def;
                m.HurtT -= dt;
                m.InvulnT -= dt;
                if (m.StaggerT > 0f)
                {
                    m.StaggerT -= dt;
                    if (m.StaggerT <= 0f)
                        m.BlockHp = d.BlockCapacity * m.Scale;  // back on its feet
                }
                bool frenzy = owner.FrenzyT > 0f && !m.NoFrenzy;
                float speed = d.Speed * (frenzy ? 1.6f : 1f);
                float rate = owner.Stats.FireRateMult * (frenzy ? 2f : 1f);
                float dmg = d.Damage * owner.Stats.DmgMult * (frenzy ? 1.3f : 1f) * m.Scale * m.Power;
                float reach = d.Range * m.Scale;
                m.Cd -= dt * rate;

                if ((m.Kind == MinionKind.Wisp && owner.Weapon.Id == WeaponId.WispLantern || m.Kind == MinionKind.Imp && owner.Weapon.Id == WeaponId.ImpCaller) && m.Companion)
                {
                    if (MoveSent(m, owner, dt))  // sent at something by the Wisp Lantern or Imp Caller (they don't act on their own)
                        exploding.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }

                // Movement
                Enemy target = null;
                switch (d.Move)
                {
                    case MinionMove.Orbit:
                        m.Ang += speed * dt;
                        m.Pos = owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius;
                        break;
                    case MinionMove.Chase:
                    {
                        target = (m.Kind == MinionKind.Wolf ? Prey(owner) : null) ?? NearestEnemy(m.Pos, MinionChaseRange);  // wolves hunt the Wolf Whistle's prey
                        V2 goal = target != null ? target.Pos : owner.Pos + V2.FromAngle(m.Id) * 50f;
                        V2 to = goal - m.Pos;
                        float stop = target != null ? target.Radius + d.Radius : 10f;
                        if (to.Length > stop)
                            m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
                        break;
                    }
                    case MinionMove.FollowHurt:
                    {
                        // Go to whoever has the lowest health fraction; otherwise stay by the owner.
                        Player who = null;
                        foreach (var q in Players.Values)
                            if (!q.Downed && q.Hp < q.MaxHp && (who == null || q.Hp / q.MaxHp < who.Hp / who.MaxHp))
                                who = q;
                        V2 goal = (who ?? owner).Pos + V2.FromAngle(m.Ang + AnimT) * 25f;
                        m.Pos += (goal - m.Pos).LimitLength(speed * dt);
                        break;
                    }
                    case MinionMove.Escort:
                        target = MoveGuard(m, owner, speed, dt);  // Stun.cs
                        break;
                    case MinionMove.FollowOwner:
                    {
                        // The treant stands in front of you; the guardian spirit hovers on you.
                        V2 goal = m.Kind == MinionKind.Treant ? TreantGoal(m, owner)
                            : m.Kind == MinionKind.SoulGhost ? owner.Pos + V2.FromAngle(owner.Aim + 2f) * 60f  // a Soul Ghost keeps beside you, a little back
                            : owner.Pos;
                        m.Pos += (goal - m.Pos).LimitLength(speed * dt);
                        break;
                    }
                }
                m.Pos = ClampToArena(m.Pos, d.Radius);

                // Attacks
                switch (d.Attack)
                {
                    case MinionAttack.Shoot:
                    {
                        var e = NearestEnemy(m.Pos, d.Range);
                        if (e == null)
                            break;
                        m.Aim = (e.Pos - m.Pos).Angle;
                        if (m.Cd <= 0f && State == MatchState.Fighting)
                        {
                            m.Cd = d.Interval;
                            V2 dir = V2.FromAngle(m.Aim);
                            byte kind = m.Kind == MinionKind.Turret ? Defs.TurretShot : m.Kind == MinionKind.Phoenix ? Defs.FireShot
                                : m.Kind == MinionKind.SoulGhost ? Defs.HealerShot : Defs.MinionShot;  // the ghost fires its healer's light bolts
                            SpawnBullet(kind, m.Pos + dir * d.Radius, dir * 650f, 0f, d.Range / 650f, dmg, m.Owner);
                        }
                        break;
                    }
                    case MinionAttack.Bite:
                    {
                        Enemy caged = m.Kind == MinionKind.Guard ? NearestCaged(m.Pos) : null;
                        if (caged != null)  // Skeleton Guards shoot at enemies the Bone Guard has caged
                        {
                            m.Aim = (caged.Pos - m.Pos).Angle;
                            if (m.Cd <= 0f && State == MatchState.Fighting)
                            {
                                m.Cd = d.Interval;
                                V2 dir = V2.FromAngle(m.Aim);
                                SpawnBullet(Defs.MinionShot, m.Pos + dir * d.Radius, dir * 650f, 0f, GuardShotRange / 650f, dmg, m.Owner);
                            }
                            break;
                        }
                        var e = target ?? NearestEnemy(m.Pos, d.Radius + 40f);
                        if (e != null && m.Cd <= 0f && e.Pos.DistanceTo(m.Pos) < e.Radius + d.Radius + 6f)
                        {
                            m.Cd = d.Interval;
                            m.Aim = (e.Pos - m.Pos).Angle;
                            HurtEnemy(e, dmg, m.Owner);
                            if (m.Kind == MinionKind.Wolf)
                                PackBite(owner, m, e);
                            // Bear Totem's extra projectiles: the bear's bites also hit enemies around its target.
                            int extraBear = m.Kind == MinionKind.Bear && owner.Weapon.Id == WeaponId.BearTotem ? ExtraShots(owner) : 0;
                            if (extraBear > 0)
                            {
                                foreach (var o in Enemies.Values)
                                    if (o != e && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < 55f + o.Radius)
                                        HurtEnemy(o, dmg * Math.Min(1f, 0.4f * extraBear), m.Owner);
                                BroadcastFx(FxKind.Ring, e.Pos, d.Color, 55f, 0.25f, Sfx.None);
                            }
                        }
                        if (m.Kind == MinionKind.LightOrb)  // also heals allies it passes over
                            foreach (var q in Players.Values)
                                if (!q.Downed && q.Pos.DistanceTo(m.Pos) < reach + d.Radius)
                                    Heal(q, m.HealRate * dt, owner);
                        break;
                    }
                    case MinionAttack.Slam:
                        if (m.Cd <= 0f && NearestEnemy(m.Pos, reach) != null)
                        {
                            m.Cd = d.Interval;
                            foreach (var e in Enemies.Values)
                                if (e.SpawnT <= 0f && e.Pos.DistanceTo(m.Pos) < reach + e.Radius)
                                    HurtEnemy(e, dmg, m.Owner);
                            BroadcastFx(FxKind.Ring, m.Pos, d.Color, reach, 0.3f, Sfx.None);
                        }
                        break;
                    case MinionAttack.Explode:
                        if (target != null && target.Pos.DistanceTo(m.Pos) < target.Radius + d.Radius + 6f)
                            exploding.Add(m);
                        break;
                    case MinionAttack.Heal:
                        foreach (var q in Players.Values)
                            if (q.Pos.DistanceTo(m.Pos) < d.Range)
                                Heal(q, m.HealRate * dt, owner);
                        break;
                }

                // The phoenix trades itself to bring back a downed ally.
                // Mastered, it revives them at full health and keeps fighting.
                if (m.Kind == MinionKind.Guardian && m.Mastered)
                    Heal(owner, 6f * m.Power * dt, owner);
                if (m.Kind == MinionKind.Phoenix)
                    foreach (var q in Players.Values)
                        if (q.Downed)
                        {
                            RevivePlayer(q, m.Mastered ? 1f : 0.5f, owner);
                            BroadcastFx(FxKind.Ring, q.Pos, d.Color, 90f, 0.6f, Sfx.Revive);
                            if (!m.Mastered)
                                expired.Add(m);
                            break;
                        }
            }
            foreach (var m in exploding)
                if (Minions.ContainsKey(m.Id))
                    ExplodeMinion(m, m.Def.Damage * OwnerDmg(m) * m.Power, m.Def.Range * m.Scale);  // a merged imp blasts wider
            foreach (var m in expired)
                Minions.Remove(m.Id);
        }

        float OwnerDmg(Minion m)
        {
            Player p;
            return Players.TryGetValue(m.Owner, out p) ? p.Stats.DmgMult : 1f;
        }

        /// Golems, guards, treants, walls, the guardian spirit and flame walls eat enemy bullets.
        /// They only stop bullets coming in from outside, and each soaks a limited amount
        /// of damage: walls and the guardian break, companions are staggered for a few seconds.
        bool BlockedBySummons(Bullet b)
        {
            foreach (var m in Minions.Values)
            {
                if (m.Def.BlockRadius <= 0f || m.StaggerT > 0f || m.BlockHp <= 0f || !Enters(b, m.Pos, m.Def.BlockRadius * m.Scale))
                    continue;
                m.BlockHp -= BlockerWear(b);
                if (m.BlockHp <= 0f)
                {
                    if (m.Companion)
                        m.StaggerT = 4f;
                    else
                        m.T = 0.01f;  // breaks at the end of this tick
                    BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, m.Def.BlockRadius * m.Scale, 0.3f, Sfx.Hit);
                }
                return true;
            }
            foreach (var z in Zones.Values)
            {
                if (z.BlockHp <= 0f || !Enters(b, z.Pos, z.Radius))
                    continue;
                z.BlockHp -= BlockerWear(b);
                if (z.BlockHp <= 0f)
                    BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.4f, Sfx.Hit);  // still burns, no longer blocks
                return true;
            }
            return false;
        }

        // --- Zones -------------------------------------------------------------

        internal Zone AddZone(ZoneKind kind, Player owner, V2 pos, float radius, float duration)
        {
            var z = new Zone { Id = nextZoneId++, Kind = kind, Owner = owner.Id, Pos = pos, Radius = radius, Duration = duration, T = duration, DmgMult = owner.Stats.DmgMult };
            z.BlockHp = z.Def.BlockCapacity;
            if (casting)
            {
                z.DmgMult *= castPower;
                z.HealMult = castPower;
                z.BlockHp *= castPower;
            }
            Zones[z.Id] = z;
            return z;
        }

        public const float ArcaneRepay = 0.5f;  // an Arcane Ward pays back this share of what it soaked, as healing, when it ends or breaks

        void ArcanePayback(Player p)
        {
            p.ArcaneOn = false;
            float heal = p.ArcaneStore * ArcaneRepay;
            p.ArcaneStore = 0f;
            if (heal <= 0f || p.Downed)
                return;
            Heal(p, heal, PlayerOrNull(p.ArcaneBy));
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.7f, 1f), 40f, 0.4f, Sfx.SpellHoly);
        }

        // Reused every tick by TickMinions and TickZones.
        readonly List<Minion> scratchMinionsExpired = new List<Minion>();
        readonly List<Minion> scratchMinionsExploding = new List<Minion>();
        readonly List<Zone> scratchZonesExpired = new List<Zone>();

        void TickZones(float dt)
        {
            var expired = scratchZonesExpired;
            expired.Clear();
            foreach (var z in Zones.Values)
            {
                var d = z.Def;
                z.T -= dt;
                Player follow;
                if (z.Follow && Players.TryGetValue(z.Owner, out follow))
                    z.Pos = follow.Pos;
                if (z.T <= 0f)
                {
                    expired.Add(z);
                    continue;
                }
                foreach (var q in Players.Values)
                {
                    if (q.Pos.DistanceTo(z.Pos) > z.Radius)
                        continue;
                    if (q.Downed && d.ReviveBoost > 0f)
                    {
                        q.Revive += dt / ReviveTime * d.ReviveBoost;  // on top of the normal revive
                        q.ReviveBy = z.Owner;
                    }
                    else if (d.HealRate > 0f)
                        Heal(q, d.HealRate * z.HealMult * (d.ReviveBoost > 0f && Players.Count == 1 ? SoloZoneHeal : 1f) * dt, PlayerOrNull(z.Owner));  // alone, nobody to revive: heals faster
                }
                if (z.Kind == ZoneKind.BloodPool && !z.Congealed && z.Duration - z.T >= PoolCongeal)
                    CongealPool(z);  // BloodMage.cs
                if (d.Dps > 0f || d.Slow > 0f || z.ExtraSlow > 0f)
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(z.Pos) > z.Radius + e.Radius)
                            continue;
                        if (d.Dps > 0f)
                            HurtEnemy(e, d.Dps * z.DmgMult * dt, z.Owner);
                        float slow = Math.Max(d.Slow, z.ExtraSlow);
                        if (slow > 0f)
                        {
                            e.SlowT = Math.Max(e.SlowT, 0.3f);
                            e.SlowMult = Math.Min(e.SlowMult, 1f - slow);
                        }
                    }
            }
            foreach (var z in expired)
            {
                if (z.Kind == ZoneKind.Ward)
                    WardEnds(z);  // Ninja.cs
                if (z.Def.BlastDamage > 0f)  // the meteor lands
                {
                    foreach (var e in Enemies.Values)
                        if (e.SpawnT <= 0f && e.Pos.DistanceTo(z.Pos) < z.Radius + e.Radius)
                            HurtEnemy(e, z.Def.BlastDamage * z.DmgMult, z.Owner);
                    BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.BigExplode);
                    Player caster;
                    if (z.Consecrates && Players.TryGetValue(z.Owner, out caster))
                    {
                        var holy = AddZone(ZoneKind.Consecrate, caster, z.Pos, z.Radius, 8f);
                        holy.DmgMult = z.DmgMult;
                        holy.HealMult = z.HealMult * 1.25f;  // 15 HP/s
                    }
                    if (z.Embers && Players.TryGetValue(z.Owner, out caster))
                    {
                        // A mastered Meteor leaves burning ground (it doesn't block bullets).
                        var burn = AddZone(ZoneKind.FlameWall, caster, z.Pos, 60f, 3f);
                        burn.DmgMult = z.DmgMult;
                        burn.BlockHp = 0f;
                    }
                }
                Zones.Remove(z.Id);
            }
        }

        bool InLantern(Player p)
        {
            foreach (var z in Zones.Values)
                if (z.Def.PreventDown && p.Pos.DistanceTo(z.Pos) <= z.Radius)
                    return true;
            return false;
        }

        // --- Time Rewind -------------------------------------------------------

        float historyT;

        /// Every 0.25 s, remember each player's health, overwriting the entry from 4 s ago.
        void RecordHistory(float dt)
        {
            historyT += dt;
            if (historyT < 0.25f)
                return;
            historyT = 0f;
            foreach (var p in Players.Values)
            {
                p.HpHistory[p.HistoryIdx] = p.Hp;
                p.DownHistory[p.HistoryIdx] = p.Downed;
                p.HistoryIdx = (p.HistoryIdx + 1) % p.HpHistory.Length;
            }
        }
    }
}

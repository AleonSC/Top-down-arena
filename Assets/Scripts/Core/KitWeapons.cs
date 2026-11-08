using System;
using System.Collections.Generic;

namespace Arena
{
    /// Elementalist and Summoner weapons (FireKitWeapon) and what they leave behind: bleeds, chain lightning, the
    /// Healer kit's seeds and vines, sent minions, orbiters, refracted shots, ion trails, the wolf pack, arc fields,
    /// light cones, bone cages, boulders and shocks (host only).
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
        public static readonly Rgba BearClawColor = new Rgba(1f, 0.86f, 0.78f);  // the claw effect's colour (the front end tells a Bear Totem's claws from other claws by it)

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
        void ApplyBleed(Enemy e, float dps, int owner, bool claws = false)
        {
            if (dps <= 0f || e.Hp <= 0f)
                return;
            e.BleedDps = Math.Min(e.BleedDps + dps, dps * BleedStacks);
            e.BleedT = BleedTime;
            e.BleedBy = owner;
            if (claws)
                e.BleedHits = Math.Min((int)BleedStacks, e.BleedHits + 1);  // (the Sanguine Orb's coats bleed too, but aren't claw marks)
        }

        void TickBleed(Enemy e, float dt)
        {
            if (e.BleedT <= 0f)
                return;
            e.BleedT -= dt;
            if (e.BleedT <= 0f)
                e.BleedDps = 0f;
            if (e.BleedT <= 0f)
                e.BleedHits = 0;
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
                    float reach = w.Life * (1f + w.Grow * extra) * PierceReach(p);  // the flames fly longer: Piercing Rounds reach farther
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
                    dmg *= 1f + FrostLanceGrow * extra;  // extra projectiles: a bigger, harder icicle
                    var ice = NewSwing(p, aim, MeleeReach(p, w.Reach + w.Grow * extra), 0f, w.Damage * dmg);  // Piercing Rounds lengthen it
                    ice.HalfWidth = IcicleHalfWidth;
                    ice.Ice = 1;
                    ice.Slow = IcicleSlow;
                    ice.NoCut = true;
                    pendingSwings.Add(ice);
                    var shards = NewSwing(p, aim, MeleeReach(p, w.Reach + w.Grow * extra), 0f, w.Damage * SplinterMult * dmg);
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
                    QueueHammerThrow(p, extra, dmg);  // the hammer rises, snaps forward and leaves your hand (StormHammer.cs)
                    break;
                case WeaponId.RadiantOrb:
                    break;  // held on the cursor instead of fired (RadiantOrb.cs); extra projectiles become Light Orbs circling you (TickMinions)
                case WeaponId.ChainLight:
                    LightCone(p, aim, w.Damage * (1f + LightConeGrow * extra) * ConeFocus(p) * dmg, extra);  // Homing Rounds condense it (AreaMult narrows it)
                    break;
                case WeaponId.Nimbus:
                    NimbusStrike(p, w.Damage * dmg, extra);  // lightning under the cloud (Nimbus.cs)
                    break;
                case WeaponId.Fireball:
                {
                    // A big fireball that rolls on through everything, setting it all burning; extras fan out beside it.
                    // It already passes through everything, so Piercing Rounds send it farther instead (15% a stack).
                    for (int i = 0; i <= extra; i++)
                    {
                        var b = SpawnBullet(Defs.FireballShot, muzzle, V2.FromAngle(Fanned(aim, i, extra + 1, 0.22f)) * w.Speed, 0f, w.Life * PierceReach(p), w.Damage * dmg, p.Id);
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
                    // A firefly: seeds the enemy it hits, then hops on to the nearest enemy that isn't seeded (FireflyHop).
                    SpawnBullet(Defs.FireflyShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Hops = FireflyHops;
                    break;
                case WeaponId.SpiritChalk:
                    break;  // drawn while you hold, not fired (Chalk.cs)
                case WeaponId.TreantRod:
                {
                    // A thorny root whips out along your aim, hits everything on the line and mends the allies it touches.
                    // (Extra projectiles grow the Treant instead, below.) Homing Rounds widen it rather than cost damage.
                    dmg *= HomingCost(p);
                    var root = NewSwing(p, aim, w.Reach, 0f, w.Damage * dmg);
                    root.HalfWidth = RootHalfWidth * AreaMult(p);
                    root.Root = true;
                    root.RootHeal = RootHeal;
                    root.NoCut = true;
                    pendingSwings.Add(root);
                    break;
                }
                case WeaponId.WispLantern:
                    for (int i = 0; i <= extra; i++)
                        SendWisp(p);  // your wisps are your shots: one a shot, plus one per extra projectile
                    break;
            }
        }

        /// Companions a player's weapon keeps around: the weapon's own, plus one per extra projectile
        /// (Golems top out at 3). The Radiant Orb gets Light Orbs; the Treant grows instead of multiplying.
        public const float FrostLanceGrow = 0.25f;  // Frost Lance: more damage per extra projectile (icicle and splinters)
        public const float LightOrbRing = 45f, LightOrbSpeed = 900f;  // Radiant Orb's light orbs: their ring round the orb; how fast they follow it
        public MinionKind CompanionKind(Player p) => p.Weapon.Id == WeaponId.RadiantOrb ? MinionKind.LightOrb : p.Weapon.Companion;

        public int CompanionCount(Player p)
        {
            var w = p.Weapon;
            int extra = ExtraShots(p);
            switch (w.Id)
            {
                case WeaponId.RadiantOrb: return extra + p.Stats.Fan;  // Multishot and Barrage each bring a light orb
                case WeaponId.TreantRod: return w.CompanionCount;
                case WeaponId.Nimbus: return w.CompanionCount;  // extra projectiles are extra strikes
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

        // --- Healer kit: the Censer's incense, Sprite Staff seeds, Ofuda wards ---------

        public const float IncenseStep = 45f, IncenseRadius = 45f, IncenseTime = 3f, IncenseStray = 0.35f;  // dropped every IncenseStep you move; enemy shots fired inside stray this far either side
        public const int FireflyHops = 2;  // a firefly hops this many times: three enemies seeded from one shot
        public const float FireflyReach = 300f, FireflySpeed = 860f;
        public const float SeedTime = 4f, SpriteFreed = 6f;
        public const int FreedCap = 4;

        bool InIncense(V2 pos)
        {
            foreach (var z in Zones.Values)
                if (z.Kind == ZoneKind.Incense && pos.DistanceTo(z.Pos) <= z.Radius)
                    return true;
            return false;
        }

        /// A seed (or firefly) hit something: it sticks in the enemy, which a kill then turns into a freed sprite.
        void SeedHit(Bullet b, Enemy e)
        {
            Player p;
            if (b.Kind != Defs.SeedShot && b.Kind != Defs.FireflyShot || !Players.TryGetValue(b.Owner, out p))
                return;
            if (p.Weapon.Id == WeaponId.SpriteStaff)
            {
                e.SeededBy = p.Id;
                e.SeededT = SeedTime;
            }
        }

        /// A firefly landed: it carries on to the nearest enemy that isn't seeded yet (hops left in b.Hops), so one shot can
        /// seed three. Nothing to hop to (everything near is seeded, or dead): it ends there.
        void FireflyHop(Bullet b)
        {
            Player owner;
            if (!Players.TryGetValue(b.Owner, out owner))
                return;
            Enemy next = null;
            float bestD = FireflyReach * FireflyReach;
            foreach (var e in Enemies.Values)
            {
                if (e.Id == b.LastHit || e.Hp <= 0f || e.SpawnT > 0f || e.Def.Xp == 0 || e.SeededT > 0f && e.SeededBy == b.Owner)
                    continue;
                float d = e.Pos.DistanceSq(b.Pos);
                if (d < bestD)
                {
                    next = e;
                    bestD = d;
                }
            }
            if (next == null)
                return;
            V2 to = next.Pos - b.Pos;
            float dist = to.Length;
            if (dist < 1f)
                return;
            // SpawnBullet applies Velocity Rounds and Homing Rounds' cost again, so hand it the base speed and the uncosted damage.
            var hop = SpawnBullet(Defs.FireflyShot, b.Pos, to / dist * (FireflySpeed / owner.Stats.ShotSpeedMult), 0f, dist / FireflySpeed + 0.15f, b.Dmg / Math.Max(0.01f, HomingCost(owner)), b.Owner);
            hop.LastHit = b.LastHit;
            hop.Hops = b.Hops - 1;
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

        public const float SeedDamage = 3f, SeedInterval = 0.4f, SeedRange = 320f, SeedSpeed = 520f;

        /// Host: a Treant Rod's Treant flicks a tiny seed (SeedDamage, every SeedInterval) at the nearest enemy within SeedRange. Seeds take
        /// your damage and fire-rate boosts and reach as far as the Treant's size allows; they get no weapon mods. A staggered Treant holds fire.
        void TickTreantSeeds(Minion m, Player owner, float rate, float dt)
        {
            m.SeedCd -= dt * rate;
            if (!m.Companion || m.StaggerT > 0f || owner.Weapon.Id != WeaponId.TreantRod || owner.Downed || State != MatchState.Fighting || m.SeedCd > 0f)
                return;
            var e = NearestEnemy(m.Pos, SeedRange * m.Scale);
            if (e == null)
                return;
            m.SeedCd = SeedInterval;
            V2 dir = (e.Pos - m.Pos).Normalized();
            SpawnBullet(Defs.TreantSeed, m.Pos + dir * (m.Def.Radius * m.Scale), dir * SeedSpeed, 0f, SeedRange * m.Scale / SeedSpeed, SeedDamage * owner.Stats.DmgMult, m.Owner);
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

        /// Homing Rounds on a sent wisp or imp: it picks an enemy up to 50% farther from your cursor a stack, and one
        /// sent at empty ground (or whose target dies) turns onto the nearest enemy within HomingRange of it.
        public const float SendPickPerStack = 0.5f;
        static float SendPickRange(Player p) => SendPick * (1f + SendPickPerStack * p.Stats.Homing / 2f);  // Homing Rounds give 2 turn a stack

        /// What a sent companion goes for: the enemy nearest your cursor (within SendPick of it), or just the cursor.
        void PickGoal(Player p, Minion m, float reach)
        {
            V2 at = p.Pos + (p.AimAt - p.Pos).LimitLength(reach);
            var e = NearestEnemy(at, SendPickRange(p));
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
            PickGoal(p, pick, WispReach * PierceReach(p));  // Piercing Rounds send it farther
            p.Run.ShotsFired++;
        }

        /// The Imp Caller: a tap sends one imp waiting by you charging at what you're aiming at. Held to full charge
        /// (with two or more waiting), they all merge first into one big imp whose blast is all of theirs and then some.
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
            else
            {
                var one = idle[0];  // a tap sends the oldest waiting imp
                idle.Clear();
                idle.Add(one);
            }
            foreach (var m in idle)
                PickGoal(p, m, ImpReach * PierceReach(p));
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
            if (e == null && owner.Stats.Homing > 0f && !m.Returning)
            {
                e = NearestEnemy(m.Pos, HomingRange);  // Homing Rounds: lock onto an enemy close by instead
                if (e != null)
                    m.GoalEnemy = e.Id;
            }
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
                float dmg = owner.Weapon.Damage * owner.Stats.DmgMult * m.Power * HomingCost(owner);  // Homing Rounds trade damage for forgiveness
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
            var cur = Prey(p);
            if (cur != null && cur != e && e.Pos.DistanceTo(p.AimAt) >= cur.Pos.DistanceTo(p.AimAt))
                return;  // a fanned or piercing dart that clipped something else doesn't pull the pack off what you're aiming at
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
        public const float ArcMin = 160f, ArcMax = Width * 0.5f, ArcHalfWidth = 60f, ArcNear = 0.4f, ArcStun = 0.4f, ArcGrow = 0.25f;  // lengths from your centre; half its width at the far end; how wide it is at your staff, as a fraction of that; stun (not bosses); +damage per extra projectile
        public static float ArcLength(Player p, float c) => (ArcMin + (ArcMax - ArcMin) * c) * p.Stats.ShotSpeedMult * PierceReach(p);  // Velocity and Piercing Rounds stretch it
        public static float ArcWidth(Player p) => ArcHalfWidth * AreaMult(p);  // Homing Rounds widen it

        /// The strip is a slight cone, like a lightning arc fanning out: ArcNear of its far width at your staff, full width
        /// at the far end. Everything (each strip's half width and its sideways shift) scales by this at distance `along`.
        public static float ArcTaper(float along, float len) => ArcNear + (1f - ArcNear) * Math.Max(0f, Math.Min(1f, along / Math.Max(1f, len)));

        /// Barrage adds cones side by side, fanning out from the same staff and alternating sides, so they never overlap:
        /// an enemy anywhere in them is shocked once.
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
                BroadcastFx(FxKind.ArcField, p.Pos + side * (shifts[v] * ArcNear), p.Pos + side * shifts[v] + dir * len, bolt, half, 0.35f,  // from the cone's narrow end's middle to its far end's middle
                    v > 0 ? Sfx.None : c > 0.95f ? Sfx.BigExplode : Sfx.Shockwave);
            p.Run.ShotsFired++;
            GateLockStrike(q =>  // a Warden gate's lock inside any strip takes a hit (Warden.cs)
            {
                V2 r = q - p.Pos;
                float al = Dot(r, dir), ac = Dot(r, side);
                if (al < -LockW / 2f || al > len + LockW / 2f)
                    return false;
                for (int v = 0; v < n; v++)
                    if (Math.Abs(ac - shifts[v]) <= half + LockW / 2f)
                        return true;
                return false;
            });
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
                float tf = ArcTaper(along, len);
                for (int v = 0; v < n && strip < 0; v++)
                    if (Math.Abs(across - shifts[v] * tf) <= half * tf + e.Radius)
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
                WetArcs(e.Pos, e, dmg, p.Id);  // Tsunami.cs
                if (!e.Def.IsBoss)
                    e.FrozenT = Math.Max(e.FrozenT, ArcStun);
                BroadcastFx(FxKind.Bolt, p.Pos + side * (shifts[strip] * tf) + dir * Math.Max(0f, Math.Min(len, along)), e.Pos, bolt, 0f, 0.2f, Sfx.None);  // from its strip's middle line
            }
            if (any)
                p.Run.ShotsHit++;
        }

        /// Chain Light: a great cone of light in front of you. Its damage is shared out between every enemy in it,
        /// so one enemy alone takes all of it; light arcs from you to each.
        public const float LightConeReach = 320f, LightConeHalf = 0.6f, LightConeGrow = 0.25f;
        // Never a ring round you: one cone is at most 100 degrees across however much Homing and Magnum widen it, and
        // Barrage's cones together cover at most 200 degrees (they narrow to fit). Barrage's extra cones deal
        // BarrageCopyDamage (60%) like every other weapon's copies. (A run had three 150-degree cones: a full ring.)
        public const float LightConeMaxHalf = 0.87f, LightConeMaxSpread = 3.5f;  // reach from your centre; half its angle (about 70 degrees across); +damage per extra projectile
        public static float LightConeLength(Player p, int extra) => LightConeReach * (1f + 0.15f * extra) * PierceReach(p);  // Piercing Rounds reach farther

        /// Barrage adds cones edge to edge beside the first, alternating sides, all within LightConeMaxSpread. Each
        /// enemy belongs to one cone only, the one it's nearest the middle of, and each cone shares its own damage out
        /// (the extra cones at BarrageCopyDamage), so Barrage reaches more enemies but a lone one takes one cone's worth.
        /// Half of one cone's angle with n cones: widened by Homing and Magnum Rounds up to LightConeMaxHalf, then
        /// narrowed so all n fit in LightConeMaxSpread.
        public static float LightConeHalfFor(Player p, int n) => Math.Min(Math.Min(LightConeHalf * AreaMult(p), LightConeMaxHalf), LightConeMaxSpread / (2f * n));

        void LightCone(Player p, float aim, float total, int extra)
        {
            int n = 1 + p.Stats.Fan;
            float reach = LightConeLength(p, extra), half = LightConeHalfFor(p, n);  // Magnum Rounds widen it, up to a cap; Homing Rounds narrow it
            float step = 2f * half;
            var aims = new float[n];
            for (int v = 0; v < n; v++)
                aims[v] = aim + (v == 0 ? 0f : (v % 2 == 1 ? 1f : -1f) * step * ((v + 1) / 2));
            var light = Defs.BulletColor(Defs.HolyShot);
            for (int v = 0; v < n; v++)  // one cast sound, however many cones
                BroadcastFx(FxKind.LightCone, p.Pos, p.Pos + V2.FromAngle(aims[v]) * reach, light, half, 0.3f, v == 0 ? Sfx.SpellChainCone : Sfx.None);
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
            bool any = false, arcSounded = false;
            for (int v = 0; v < n; v++)
            {
                if (hits[v].Count == 0)
                    continue;
                any = true;
                float share = total * (v == 0 ? 1f : BarrageCopyDamage) / hits[v].Count;
                V2 dir = V2.FromAngle(aims[v]), from = p.Pos + dir * p.Def.BodyR;
                bool firstArc = !arcSounded;  // the arcs crackle once per cast, not once per enemy
                arcSounded = true;
                foreach (var e in hits[v])
                {
                    HurtEnemy(e, share, p.Id);
                    OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = share, Kind = Defs.HolyShot }, e);  // weapon mods apply to each share
                    WetArcs(e.Pos, e, share, p.Id);  // Tsunami.cs
                    BroadcastFx(FxKind.Bolt, from, e.Pos, light, 0f, 0.2f, firstArc ? Sfx.SpellChainArc : Sfx.None);
                    firstArc = false;
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
            GateLockBlast(b.Pos, b.Splash, b.Owner);  // Warden.cs
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
            WetArcs(from.Pos, from, sh.Dmg / HammerArcMult * 0.5f, sh.Owner);  // Tsunami.cs: a pulse counts as half a hammer hit (its own arcs are only an eighth)
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
    }
}

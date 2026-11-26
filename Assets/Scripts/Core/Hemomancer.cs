using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Hemomancer: health is its resource. Every 1st ability costs health (never below 1), it hits harder
    /// the more health it's missing (Blood Price, up to +40%), and all its hits heal it a little. Tanks turn
    /// health into protection, healers give their own health away, and DPS are strongest near death.
    public partial class Game
    {
        // The Gore Mortar : hold to charge, let go to lob a clot over enemies. The charge sets only
        // how far it lands (ClotMin on a tap to ClotMax at full); the splash hits the same at any range, then a big pool
        // slows enemies and congeals after PoolCongeal (a look only: it never stops them).
        // Every shot costs ClotCost of your max health (never below 1); the pools heal everyone standing in them (the
        // Blood Pool zone's HealRate, Kits.cs), so you can win some of it back by wading into your own blood.
        public const float ClotMin = 160f, ClotMax = 520f, ClotSplash = 70f, ClotPoolRadius = 130f, ClotPoolTime = 4f, ClotFan = 0.2f, ClotCost = 0.06f;
        public static float ClotDistance(float c) => ClotMin + (ClotMax - ClotMin) * c;
        public static float ClotFlight(float c) => 0.45f + 0.3f * c;
        public static float ClotApex(float c) => 50f + 110f * c;  // how high it flies (drawing only)
        public const float PoolCongeal = 1f;  // (each weapon's main reach, speed and damage are in Defs)
        const float BloodPriceMax = 0.4f, BloodSteal = 0.03f, VeinSteal = 0.4f, VeinRange = 300f;
        public const float VeinBeamHalfWidth = 6f;  // Vein Tether's beam (its length and damage per tick are the weapon's Reach and Damage)
        public const float GraspArc = 70f * MathX.Pi / 180f;  // Leech Tendrils reach across this arc (their reach and damage per tick are the weapon's Reach and Damage)
        public const int GraspTendrils = 3;                   // enemies held at once (+1 per extra projectile or Barrage stack)
        const float GraspLetGo = 1.3f, GraspHold = 0.3f, GraspFuse = 5f, GraspCooldown = 1f, GraspCost = 0.02f;      // a held enemy slips free past 130% of the reach; it stays stunned this long after each tick
        public const float TransfuseCost = 4f, TransfuseHeal = 20f, TransfuseSteal = 0.5f;  // a dart gives an ally a lot, costs you a little; each enemy hit steals a sliver
        const float ShellTime = 5f;  // (Blood Gift's shield; Blood Shell itself is BloodShell.cs)
        const float DrainRadius = 220f, DrainAmount = 20f;  // Exsanguinate (Exsanguinate.cs)
        const float GiftMult = 2f, GiftSplash = 200f, MartyrRevive = 0.5f;
        public const float MartyrWardTime = 12f;  // also the halo's twelve beads, one a second (Game/MartyrLook.cs)
        public const float MartyrFxTime = 1.4f;  // how long a MartyrCast effect plays (beads, burst, spent ward)
        public const float ImplodeGrab = 26f;  // how near an enemy the click must land (how far away you can click, and the damage, are the weapon's Reach and Damage)
        public const float LashTipFrom = 0.7f, LashCrack = 2f, LashTaunt = 1.5f;  // Blood Lash: the outer 30% of its reach cracks (BloodLash.cs)
        public const int ClotMaxBase = 5;
        public const float ClotCutEach = 0.04f, ClotHold = 3f, ClotFade = 0.5f;  // each Clot stack; how long they hold, then one fades per ClotFade
        static readonly Rgba LashRed = new Rgba(0.85f, 0.1f, 0.18f);
        const float ImplodeSplash = 55f, ImplodeSplashDamage = 15f, ImplodePull = 18f, ImplodeChain = 180f, ImplodeChainMult = 0.6f;

        // --- Weapons ------------------------------------------------------------------

        /// A Hemomancer weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireBloodWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;  // its Speed, Life, Damage, Reach and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.BloodLash:
                    FireBloodLash(p, aim, dmg, extra);  // the chain combo (BloodLash.cs)
                    break;
                case WeaponId.BloodBlast:
                    FireBloodBlast(p, aim, dmg, extra);  // the six-step chain of blood cuts (BloodBlast.cs)
                    break;
                case WeaponId.GoreMortar:
                {
                    // The Gore Mortar: lobbed over enemies like the Golem Staff's boulder, as far as the charge sends it;
                    // the damage doesn't change with the charge. Extra projectiles fan more clots out to the same distance.
                    float c = BowCharge(p, p.BowShotDraw), flight = ClotFlight(c);
                    if (!casting)  // the clot is your own blood: a shot costs health (extra clots come free)
                    {
                        float paid = PayHealth(p, p.MaxHp * ClotCost, p.Weapon.Name);
                        if (paid >= 1f)
                            BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), paid, 0.55f, Sfx.None);
                    }
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        V2 vel = V2.FromAngle(Fanned(aim, i, n, ClotFan)) * (ClotDistance(c) / flight);  // SpawnBullet adds Velocity Rounds, which send it farther
                        var b = SpawnBullet(Defs.ClotShot, p.Pos, vel, 0f, flight, w.Damage * dmg, p.Id);
                        b.Splash = ClotSplash * AreaMult(p);
                        b.Pool = ClotPoolRadius * AreaMult(p) * p.Stats.PoolSizeMult;  // (Deep Pool: bigger, longer)
                        b.PoolTime = ClotPoolTime + p.Stats.PoolBonusTime;
                        b.Burst = 1;  // marks it as landing (MoveBullets)
                    }
                    break;
                }
                case WeaponId.LeechTendrils:
                    // Leech Tendrils: no projectile; each tick the tendrils hold on and reach for more (Collide); TickGrasp charges them.
                    pendingGrasps.Add(new Grasp { Owner = p.Id, Aim = aim, Extra = extra, Dmg = w.Damage * dmg * (1f + p.Stats.LeechBlastBonus) });  // (Hungry Tendrils: bigger blast)
                    break;
                case WeaponId.VeinTether:
                {
                    // A real beam: it hits whatever is on your aim line right now, so you have to keep it on them.
                    float k = 1f + w.Grow * extra;
                    float beamDmg = w.Damage * k * dmg * HomingCost(p) * (casting && !firingWeapon ? castPower : 1f);
                    int pierce = p.Stats.Pierce;
                    pendingBeams.Add(new VeinBeam { Owner = p.Id, Aim = aim, Range = w.Reach * k * p.Stats.ShotSpeedMult, Dmg = beamDmg, Pierce = pierce, HalfWidth = VeinBeamHalfWidth * AreaMult(p) });  // Velocity Rounds stretch it
                    break;
                }
                case WeaponId.TransfusionDarts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.TransfuseShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.08f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.HealAlly = TransfuseHeal * 9f / w.Damage * p.Stats.TransfuseMult;  // (Generous Blood: more)  // HealAllyHit scales it by Dmg / 9; the dart costs you TransfuseCost when it heals
                    }
                    break;
                }
                case WeaponId.SanguineOrb:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.SanguineShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.35f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id, false, SanguineTurn);  // it homes only at the last moment (Steer)
                    break;
                }
                case WeaponId.Implosion:
                {
                    // No projectile: the enemy under your cursor collapses (ImplodeReady made sure there is one).
                    var e = ImplodeTarget(p, p.AimAt);
                    if (e != null)
                        pendingImplosions.Add(new Implode { Owner = p.Id, Target = e.Id, Extra = extra, Dmg = dmg * (casting && !firingWeapon ? castPower : 1f) });
                    break;
                }
                case WeaponId.BloodBrawler:
                    FireBloodBrawler(p, dmg, extra);  // the Marionette string on the enemy under the cursor (BloodBrawler.cs)
                    break;
            }
        }

        // --- Vein Tether's beam ----------------------------------------------------------

        class VeinBeam { public int Owner, Pierce; public float Aim, Range, Dmg, HalfWidth; }

        readonly List<VeinBeam> pendingBeams = new List<VeinBeam>();
        readonly List<Enemy> beamHits = new List<Enemy>();

        /// Where a beam from `from` along `aim` stops: at the first enemy it can't pass (a shield, or the last it
        /// pierces), else at its full range. Fills `hits` (if given) with the enemies it touches, nearest first.
        /// The host uses real positions; screens pass `drawn` to use where enemies are drawn.
        public V2 BeamEnd(V2 from, float aim, float range, int pierce, List<Enemy> hits, bool drawn = false, int by = 0, float halfWidth = VeinBeamHalfWidth)
        {
            V2.Axes(aim, out V2 dir, out V2 side);
            beamOrder.Clear();
            foreach (var e in AllFoes(by: by))
            {
                if (!Targetable(e))
                    continue;
                V2 rel = (drawn ? e.Disp : e.Pos) - from;
                float along = rel.X * dir.X + rel.Y * dir.Y, across = Math.Abs(rel.X * side.X + rel.Y * side.Y), reach = e.Radius + halfWidth;
                if (across > reach || along < -e.Radius || along - e.Radius > range)
                    continue;
                float enter = along - (float)Math.Sqrt(Math.Max(0f, reach * reach - across * across));
                beamOrder.Add(new KeyValuePair<float, Enemy>(Math.Max(0f, enter), e));
            }
            beamOrder.Sort((a, b) => a.Key.CompareTo(b.Key));
            hits?.Clear();
            int left = pierce;
            foreach (var kv in beamOrder)
            {
                if (BlocksShot(kv.Value, from))
                    return from + dir * kv.Key;  // shields stop it cold
                hits?.Add(kv.Value);
                if (left-- <= 0)
                    return from + dir * kv.Key;
            }
            return from + dir * range;
        }

        readonly List<KeyValuePair<float, Enemy>> beamOrder = new List<KeyValuePair<float, Enemy>>();

        // --- Leech Tendrils: hold to grasp the enemies in front of you ----------------------

        class Grasp { public int Owner, Extra; public float Aim, Dmg; }

        readonly List<Grasp> pendingGrasps = new List<Grasp>();
        readonly List<Enemy> graspNew = new List<Enemy>();
        V2 graspFrom;  // the holder, for the sort below (one comparer made once, not a closure every tick)
        Comparison<Enemy> graspByDist;

        /// How far Leech Tendrils reach from the player's edge.
        public float GraspReach(Player p) => (p.Weapon.Reach + p.Weapon.Grow * ExtraShots(p)) * p.Stats.ShotSpeedMult;  // Velocity Rounds stretch them

        /// How wide the player's tendrils reach (Homing Rounds narrow it 15% a stack, Magnum Rounds widen it).
        public static float GraspArcFor(Player p) => GraspArc * AreaMult(p);

        /// Is the enemy (its body) within the tendrils' arc (`arc` wide) in front of you?
        public static bool InGraspArc(V2 from, float aim, V2 pos, float radius, float arc = GraspArc)
        {
            V2 rel = pos - from;
            float d = rel.Length;
            if (d <= radius)
                return true;
            float cos = (rel.X * (float)Math.Cos(aim) + rel.Y * (float)Math.Sin(aim)) / d;
            float off = (float)Math.Acos(Math.Max(-1f, Math.Min(1f, cos))), slack = (float)Math.Asin(radius / d);
            return off <= arc / 2f + slack;
        }

        /// Called from Collide after the beams: each tick, held enemies too far away (or dead) slip free and free
        /// tendrils grab the nearest enemies in the arc in front of you. (TickGrasp charges and stuns what's held.)
        void ResolveGrasps()
        {
            foreach (var gr in pendingGrasps)
            {
                Player p;
                if (!Players.TryGetValue(gr.Owner, out p) || p.Downed || p.GraspCd > 0f)
                    continue;
                p.GraspDmg = gr.Dmg;
                float reach = p.Def.BodyR + GraspReach(p);
                int max = GraspTendrils + gr.Extra + p.Stats.Fan;
                Enemy e;
                for (int i = p.Grasp.Count - 1; i >= 0; i--)  // (a loop, not RemoveAll: no closure every tick)
                    if (!TryFoe(p.Grasp[i], out e) || IsSpawning(e) || e.Hp <= 0f || e.Pos.DistanceTo(p.Pos) - e.Radius > reach * GraspLetGo)
                        DropGrasp(p, i);  // slipped free: its build-up is lost
                if (p.Grasp.Count > max)
                    while (p.Grasp.Count > max)
                        DropGrasp(p, p.Grasp.Count - 1);
                // Free tendrils reach for the nearest enemies in the arc (shields turn them away).
                graspNew.Clear();
                foreach (var o in AllFoes(by: p.Id))
                    if (!IsSpawning(o) && o.Hp > 0f && !p.Grasp.Contains(o.Id) && o.Pos.DistanceTo(p.Pos) - o.Radius <= reach
                        && InGraspArc(p.Pos, gr.Aim, o.Pos, o.Radius, GraspArcFor(p)) && !BlocksShot(o, p.Pos))
                        graspNew.Add(o);
                graspFrom = p.Pos;
                graspNew.Sort(graspByDist ?? (graspByDist = (a, b) => a.Pos.DistanceSq(graspFrom).CompareTo(b.Pos.DistanceSq(graspFrom))));
                for (int i = 0; i < graspNew.Count && p.Grasp.Count < max; i++)
                {
                    p.Grasp.Add(graspNew[i].Id);
                    p.GraspCharge.Add(0f);
                }
                p.Run.ShotsFired++;
                if (p.Grasp.Count > 0)
                    p.Run.ShotsHit++;
            }
            pendingGrasps.Clear();
        }

        static void DropGrasp(Player p, int i)
        {
            p.Grasp.RemoveAt(i);
            if (i < p.GraspCharge.Count)
                p.GraspCharge.RemoveAt(i);
        }

        /// Every host frame: the cooldown runs down, held enemies stay stunned and build up toward their blast, holding
        /// costs health, and the tendrils burst when one is full or let go of fire.
        void TickGrasp(Player p, float dt)
        {
            if (p.GraspCd > 0f)
                p.GraspCd = Math.Max(0f, p.GraspCd - dt);
            while (p.GraspCharge.Count > p.Grasp.Count)
                p.GraspCharge.RemoveAt(p.GraspCharge.Count - 1);
            while (p.GraspCharge.Count < p.Grasp.Count)
                p.GraspCharge.Add(0f);
            if (p.Grasp.Count == 0)
                return;
            bool mine = !p.Downed && !p.ParryActive && p.Weapon.Id == WeaponId.LeechTendrils;
            if (!mine || !p.Firing)
            {
                ReleaseGrasp(p, mine);  // let go of fire: they burst for what's built up (not if you're downed, parrying or swapped)
                return;
            }
            PayHealth(p, p.MaxHp * GraspCost * dt, p.Weapon.Name);
            bool full = false;
            for (int i = p.Grasp.Count - 1; i >= 0; i--)
            {
                Enemy e;
                if (!TryFoe(p.Grasp[i], out e) || e.Hp <= 0f)
                {
                    DropGrasp(p, i);
                    continue;
                }
                if (!e.Def.IsBoss)
                    e.GraspT = Math.Max(e.GraspT, GraspHold);  // stunned while held (bosses are too strong to hold still)
                p.GraspCharge[i] = Math.Min(1f, p.GraspCharge[i] + dt * p.Stats.LeechDrainMult / GraspFuse);
                full |= p.GraspCharge[i] >= 1f;
            }
            if (full)
                ReleaseGrasp(p, true);
        }

        /// The tendrils let go: each held enemy bursts for its share of a full blast, and the tendrils need a moment.
        void ReleaseGrasp(Player p, bool burst)
        {
            int held = p.Grasp.Count, max = GraspTendrils + ExtraShots(p) + p.Stats.Fan;
            if (burst && held > 0)
            {
                bool was = firingWeapon;
                firingWeapon = true;  // (credited to the weapon)
                for (int i = 0; i < held && i < p.GraspCharge.Count; i++)
                {
                    Enemy e;
                    if (!TryFoe(p.Grasp[i], out e) || e.Hp <= 0f)
                        continue;  // killed by another blast's on-hit effects
                    float frac = p.GraspCharge[i];
                    V2 dir = (e.Pos - p.Pos).Normalized();
                    float hit = p.GraspDmg * frac * TendrilsOn(max, held, i) * ConeFocus(p) * NinjaHitMult(p.Id, Defs.LeechShot, e);
                    HurtEnemy(e, hit, p.Id);
                    var pseudo = new Bullet { Pos = e.Pos, Vel = dir.IsZero ? V2.FromAngle(p.Aim) : dir, Owner = p.Id, Dmg = hit, Kind = Defs.LeechShot };
                    OnBulletHit(pseudo, e);
                    if (frac > 0.1f)
                        BroadcastFx(FxKind.Boom, e.Pos, new Rgba(0.85f, 0.1f, 0.18f), e.Radius + 12f + 30f * frac, 0.3f, Sfx.Explode);
                }
                firingWeapon = was;
                p.GraspCd = GraspCooldown;
            }
            p.Grasp.Clear();
            p.GraspCharge.Clear();
        }

        /// How many of `max` tendrils hold the i-th of `held` enemies: one each, and spare tendrils (nothing else
        /// in reach) pile onto the ones already held, nearest first. A lone boss gets all of them.
        public static int TendrilsOn(int max, int held, int i) => held <= 0 ? 0 : 1 + Math.Max(0, max - held) / held + (i < Math.Max(0, max - held) % held ? 1 : 0);

        /// Are this player's Leech Tendrils out? (Each screen draws them itself, from the held enemies in the snapshot.)
        public bool GraspOn(Player p) =>
            p.Weapon.Id == WeaponId.LeechTendrils && !p.Downed && !p.ParryActive && (p.Id == MyId || !IsHost ? p.FiringSeen : p.Firing)
            && State != MatchState.GameOver && State != MatchState.Victory;

        // --- Implosion: click an enemy and its blood collapses inward ---------------------

        class Implode { public int Owner, Target, Extra; public float Dmg; }

        readonly List<Implode> pendingImplosions = new List<Implode>();
        readonly List<Enemy> imploded = new List<Enemy>();

        /// The enemy an Implosion would hit: the one nearest `at` whose body is within ImplodeGrab of it, and in
        /// reach of the player. Screens pass `drawn` to use where enemies are drawn (the target marker).
        public Enemy ImplodeTarget(Player p, V2 at, bool drawn = false)
        {
            float reach = WeaponRange(p);
            Enemy best = null;
            float bestGap = ImplodeGrab;
            foreach (var e in AllFoes(by: p.Id))
            {
                if (IsSpawning(e) || e.Hp <= 0f)
                    continue;
                V2 pos = drawn ? e.Disp : e.Pos;
                float gap = pos.DistanceTo(at) - e.Radius;
                if (gap <= bestGap && pos.DistanceTo(p.Pos) - e.Radius <= reach)
                {
                    best = e;
                    bestGap = gap;
                }
            }
            return best;
        }

        /// Implosion and the Blood Brawler only fire with an enemy under the cursor; otherwise holding fire waits (and keeps the cooldown).
        bool ImplodeReady(Player p) => p.Weapon.Id != WeaponId.Implosion && p.Weapon.Id != WeaponId.BloodBrawler || ImplodeTarget(p, p.AimAt) != null;  // (and the Blood Brawler's string: BloodBrawler.cs)

        /// Called from Collide after the beams: each implosion hurts its target, then the enemies pressed against
        /// it (pulled in a little), then one more enemy near the target per extra projectile (one not hit yet; with
        /// none left, the target collapses again).
        void ResolveImplosions()
        {
            var blood = Defs.BulletColor(Defs.ImplodeShot);
            foreach (var im in pendingImplosions)
            {
                Player p;
                Enemy e;
                if (!Players.TryGetValue(im.Owner, out p) || p.Downed || !TryFoe(im.Target, out e) || e.Hp <= 0f)
                    continue;
                p.Run.ShotsFired++;
                p.Run.ShotsHit++;
                imploded.Clear();
                Collapse(p, e, im.Dmg, true, blood);
                for (int k = 0; k < im.Extra; k++)
                {
                    Enemy next = null;
                    foreach (var o in AllFoes(by: p.Id))
                        if (!imploded.Contains(o) && !IsSpawning(o) && o.Hp > 0f && o.Pos.CloserThan(e.Pos, ImplodeChain + o.Radius)
                            && (next == null || o.Pos.DistanceSq(e.Pos) < next.Pos.DistanceSq(e.Pos)))
                            next = o;
                    if (next == null && e.Hp > 0f)
                        next = e;
                    if (next == null)
                        break;
                    Collapse(p, next, ImplodeChainMult * im.Dmg, false, blood);
                }
            }
            pendingImplosions.Clear();
        }

        /// One enemy's blood collapsing: its damage (the weapon's, times `mult`), the splash to enemies touching it,
        /// and the effect.
        void Collapse(Player p, Enemy e, float mult, bool splash, Rgba blood)
        {
            imploded.Add(e);
            HitImplode(p, e, p.Weapon.Damage * mult * p.Stats.ImplodeDmgMult);  // (Event Horizon: harder, and from farther)
            float r = e.Radius + ImplodeSplash * AreaMult(p) * p.Stats.ImplodeReachMult;  // Homing Rounds widen it
            if (splash)
                foreach (var o in AllFoes(by: p.Id))
                {
                    if (o == e || o.Pos.FartherOrAt(e.Pos, r + o.Radius))
                        continue;
                    imploded.Add(o);
                    HitImplode(p, o, ImplodeSplashDamage * mult);
                    if (!Heavy(o))  // sucked in toward the collapse, never inside it
                    {
                        V2 toward = e.Pos - o.Pos;
                        float d = toward.Length;
                        if (d > 0.01f)
                            o.Pos = ClampToArena(e.Pos - toward / d * Math.Max(d - ImplodePull, e.Radius + o.Radius), o.Radius);
                    }
                }
            BroadcastFx(FxKind.Implode, e.Pos, blood, splash ? r : e.Radius + 16f, 0.35f, Sfx.SpellArcane);
        }

        void HitImplode(Player p, Enemy e, float dmg)
        {
            float hit = dmg * HomingCost(p) * NinjaHitMult(p.Id, Defs.ImplodeShot, e);
            HurtEnemy(e, hit, p.Id);
            var pseudo = new Bullet { Pos = e.Pos, Vel = (e.Pos - p.Pos).Normalized(), Owner = p.Id, Dmg = hit, Kind = Defs.ImplodeShot };
            BloodShotHit(pseudo, e, hit);
            OnBulletHit(pseudo, e);
        }

        /// Called from Collide with the swings: each beam tick hurts what's on the line, through the usual on-hit
        /// effects (weapon mods, legendaries) and Vein Tether's drain.
        void ResolveBeams()
        {
            foreach (var beam in pendingBeams)
            {
                Player p;
                if (!Players.TryGetValue(beam.Owner, out p) || p.Downed)
                    continue;
                V2 dir = V2.FromAngle(beam.Aim), from = p.Pos + dir * p.Def.BodyR;  // from your edge, like shots (so it ends where the aim line does)
                BeamEnd(from, beam.Aim, beam.Range, beam.Pierce, beamHits, false, p.Id, beam.HalfWidth);
                GateLockStrike(q =>  // a Warden gate's lock on the beam's line takes a hit too (Warden.cs)
                {
                    V2 rel = q - from;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    return along > -LockW / 2f && along < beam.Range + LockW / 2f && across < beam.HalfWidth + LockW / 2f;
                });
                p.Run.ShotsFired++;
                if (beamHits.Count > 0)
                    p.Run.ShotsHit++;
                foreach (var e in beamHits)
                {
                    float hit = beam.Dmg * NinjaHitMult(p.Id, Defs.VeinShot, e);
                    HurtEnemy(e, hit, p.Id);
                    var pseudo = new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = hit, Kind = Defs.VeinShot };
                    BloodShotHit(pseudo, e, hit);
                    OnBulletHit(pseudo, e);
                }
            }
            pendingBeams.Clear();
        }

        /// Is this player's Vein Tether beam showing? (Each screen draws the beams itself.)
        public bool BeamOn(Player p) =>
            p.Weapon.Id == WeaponId.VeinTether && p.StunShots == 0 && !p.Downed && !p.ParryActive && (p.Id == MyId || !IsHost ? p.FiringSeen : p.Firing)
            && State != MatchState.GameOver && State != MatchState.Victory;

        /// Shot i of n, fanned out `step` radians apart around the aim (a lone shot flies straight). Use this for any spread.
        static float Fanned(float aim, int i, int n, float step) => aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * step * (n - 1));

        /// What a Hemomancer's shot does when it hits, after the damage (dmg) is dealt.
        void BloodShotHit(Bullet b, Enemy e, float dmg)
        {
            if (b.Kind < Defs.BloodShot || b.Kind > Defs.BloodBrawlerShot)
                return;
            Player p;
            if (!Players.TryGetValue(b.Owner, out p) || p.Downed)
                return;
            switch (b.Kind)
            {
                case Defs.TransfuseShot:
                    Heal(p, TransfuseSteal, p);  // a sliver of the enemy's blood (it's for giving, not for soloing)
                    break;
                case Defs.VeinShot:
                {
                    // Split between you and the nearest ally (all yours if no one is near).
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && q.Pos.CloserThan(p.Pos, VeinRange) && (ally == null || q.Pos.DistanceSq(p.Pos) < ally.Pos.DistanceSq(p.Pos)))
                            ally = q;
                    float heal = dmg * (VeinSteal + p.Stats.VeinStealBonus);  // (Lifeline: more)
                    if (ally == null)
                        Heal(p, heal, p);
                    else
                    {
                        Heal(p, heal * 0.5f, p);
                        Heal(ally, heal * 0.5f, p);
                    }
                    break;
                }
            }
        }

        // --- Sanguine Orb: coat enemies in blood, then coat allies in healing blood -------------------------

        public const float SanguineTurn = 9f, SanguineHomeRange = 110f;  // it flies straight, then homes hard on an enemy this close
        public const float CoatTime = 0.45f, SanguineBleed = 0.3f;       // circling in to coat an enemy; then it bleeds this share of the hit a second (a stack)
        public const float ReserveMax = 100f, ReserveGain = 10f, AllyCoatCost = 25f, AllyCoatTime = 4f, AllyCoatHeal = 30f;  // the heal reserve, and coating an ally from it

        /// An orb circling in on the enemy it caught.
        class Coating
        {
            public int Enemy, Owner;
            public float T, Dmg;
        }

        readonly List<Coating> coatings = new List<Coating>();

        /// A Sanguine Orb caught an enemy: it circles in and covers it with blood (BloodCoat; Pos2.X carries the
        /// enemy's id so everyone draws it on the enemy as it moves). The hit lands when it closes in (TickCoatings).
        void StartBloodCoat(Bullet b, Enemy e)
        {
            coatings.Add(new Coating { Enemy = e.Id, Owner = b.Owner, T = CoatTime, Dmg = b.Dmg });
            BroadcastFx(FxKind.BloodCoat, e.Pos, new V2(e.Id, 0f), Defs.BulletColor(Defs.SanguineShot), e.Radius, CoatTime, Sfx.None);
        }

        /// A Sanguine Orb touching a hurt ally: with enough heal reserve, it coats them in healing blood instead.
        bool CoatAlly(Bullet b)
        {
            Player owner;
            if (!Players.TryGetValue(b.Owner, out owner) || owner.BloodReserve < AllyCoatCost)
                return false;
            foreach (var q in Players.Values)
            {
                float r = q.Def.BodyR + b.Radius;
                if (q == owner || q.Downed || q.Hp >= q.MaxHp || b.Pos.DistanceSq(q.Pos) >= r * r)
                    continue;
                owner.BloodReserve -= AllyCoatCost;
                q.CoatT = AllyCoatTime;
                q.CoatBy = owner.Id;
                BroadcastFx(FxKind.Ring, q.Pos, Defs.BulletColor(Defs.SanguineShot), q.Def.BodyR + 14f, 0.3f, Sfx.SpellHoly);
                return true;
            }
            return false;
        }

        /// Host, every tick: coatings close in and land (damage, a bleed stack, heal reserve), and coated allies heal.
        void TickCoatings(float dt)
        {
            for (int i = coatings.Count - 1; i >= 0; i--)
            {
                var c = coatings[i];
                if ((c.T -= dt) > 0f)
                    continue;
                coatings.RemoveAt(i);
                Enemy e;
                Player p;
                if (!TryFoe(c.Enemy, out e) || e.Hp <= 0f)
                    continue;
                WeaponHurt(e, c.Dmg, c.Owner);
                ApplyBleed(e, c.Dmg * SanguineBleed, c.Owner);  // KitWeapons.cs: each coat is another stack
                ModHit(e, c.Owner, c.Dmg, Defs.SanguineShot, default(V2));
                if (Players.TryGetValue(c.Owner, out p))
                    p.BloodReserve = Math.Min(ReserveMax, p.BloodReserve + ReserveGain * p.Stats.ReserveGainMult);  // (Rich Blood: faster)
            }
            foreach (var q in Players.Values)
            {
                if (q.CoatT <= 0f)
                    continue;
                q.CoatT = q.Downed ? 0f : Math.Max(0f, q.CoatT - dt);
                if (q.CoatT > 0f)
                {
                    var coater = PlayerOrNull(q.CoatBy);
                    Heal(q, AllyCoatHeal / AllyCoatTime * dt * (coater != null ? coater.Stats.CoatHealMult : 1f), coater);  // (Rich Blood: more)
                }
            }
        }

        // --- Needle Swarm: hemorrhage (NeedleSwarm.cs) ---------------------------------------------------

        public const int BleedBurstAt = 5;
        public const float BoltBleed = 0.3f, BleedBurstMult = 4f, BleedBurstRadius = 80f;  // a stack bleeds this share of the hit a second; the burst is this many hits' damage

        /// The Needle Swarm's wrap cut `e` again (every NsBleedEvery): another bleed stack, and the one that fills its bleed
        /// (BleedBurstAt stacks, from any source) bursts it, hurting it and every enemy round it and clearing the bleed.
        void NeedleBleed(Player p, Enemy e, float boltDmg)
        {
            ApplyBleed(e, boltDmg * BoltBleed, p.Id);  // KitWeapons.cs
            if (e.BleedStacks < BleedBurstAt - p.Stats.HemoHitCut)  // (Hemophilia: fewer)
                return;
            e.BleedStacks = e.BleedHits = 0;
            e.BleedT = e.BleedDps = 0f;
            float dmg = boltDmg * BleedBurstMult, r = BleedBurstRadius * AreaMult(p) * p.Stats.HemoRadiusMult;
            foreach (var o in FoesInCircle(e.Pos, r, by: p.Id))
                HurtEnemy(o, dmg, p.Id);
            BroadcastFx(FxKind.Implode, e.Pos, new Rgba(0.85f, 0.1f, 0.18f), r, 0.35f, Sfx.Explode);
        }

        /// A Gore Mortar's clot landed: it splashes every enemy under it (weapon mods apply to each, as on a melee hit)
        /// and spreads a pool of blood.
        void LandClot(Bullet b)
        {
            foreach (var e in FoesInCircle(b.Pos, b.Splash, b.Pos, by: b.Owner))
            {
                HurtEnemy(e, b.Dmg, b.Owner);
                OnBulletHit(b, e);
            }
            GateLockBlast(b.Pos, b.Splash, b.Owner);  // Warden.cs
            Player p;
            if (Players.TryGetValue(b.Owner, out p))
                AddZone(ZoneKind.BloodPool, p, b.Pos, b.Pool, b.PoolTime);
            BroadcastFx(FxKind.Ring, b.Pos, Defs.BulletColor(Defs.ClotShot), b.Splash * 1.4f, 0.32f, Sfx.Explode);
        }

        /// A Gore Mortar's pool sets: it crusts over with a thump (only the look: enemies in it are slowed, never stuck).
        void CongealPool(Zone z)
        {
            z.Congealed = true;
            BroadcastFx(FxKind.Ring, z.Pos, z.Def.Color, z.Radius, 0.3f, Sfx.None);
        }

        /// Transfusion Darts cost the healer some of their own health for each ally they heal.
        void TransfusionCost(Bullet b)
        {
            Player p;
            if (b.Kind == Defs.TransfuseShot && Players.TryGetValue(b.Owner, out p))
                PayHealth(p, TransfuseCost, p.Weapon.Name);
        }

        // --- Health as a resource ---------------------------------------------------------

        /// Spend health, never below 1 (or nothing while down). Returns how much was actually paid; the session
        /// recording counts it as health spent on `what`, not damage taken.
        float PayHealth(Player p, float cost, string what)
        {
            cost *= p.Stats.HealthCostMult;  // Bloodthirst (the Hemomancer's class card)
            if (p.Downed || cost <= 0f)
                return 0f;
            float paid = Math.Max(0f, Math.Min(cost, p.Hp - 1f));
            p.Hp -= paid;
            return paid;
        }

        float castPaid;  // health paid for the ability being cast (Hemomancer abilities scale with it)

        /// Host, as an ability is cast: pay its health cost, if it has one.
        void PayAbilityCost(Player p, AbilityId id)
        {
            castPaid = 0f;
            if (id == AbilityId.None)
                return;
            var d = Upgrades.Ability(id);
            if (d.HealthCost > 0f)
                castPaid = PayHealth(p, (d.CostsCurrent ? p.Hp : p.MaxHp) * d.HealthCost, d.Name);
            if (castPaid >= 1f)  // blood spurts off you: you can see what it cost
                BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), new Rgba(0.85f, 0.12f, 0.2f), castPaid, 0.55f, Sfx.None);
        }

        /// Blood Price: a Hemomancer deals up to 40% more damage the more health it's missing (full bonus at 10%
        /// health). Applied in HurtEnemy, with Blood Boil's share.
        float BloodDamageMult(Player p)
        {
            float boil = BoilDmgMult(p);  // Blood Boil (BloodBoil.cs), whatever the class (a Wildcard can pick it)
            if (p.Kit != Kit.Hemomancer || p.MaxHp <= 0f)
                return boil;
            float missing = Math.Max(0f, Math.Min(1f, (1f - p.Hp / p.MaxHp) / 0.9f));
            return (1f + BloodPriceMax * missing) * boil;
        }

        /// Healing from damage dealt (HurtEnemy): a Hemomancer's 3%, Blood Debt's 15% while it runs, and a boss's blood
        /// paying the debt back (BloodDebt.cs).
        void BloodLifesteal(Player p, Enemy e, float dealt)
        {
            if (p.Downed || dealt <= 0f)
                return;
            float frac = 0f;
            if (p.Kit == Kit.Hemomancer && !bathTick)
                frac += BloodSteal;  // (a Bloodbath tick pays back its own share instead: Bloodbath.cs)
            if (p.DebtT > 0f)
                frac += DebtSteal;
            if (frac > 0f)
                Heal(p, dealt * frac, p);
            if (p.Debt > 0f && e.Def.IsBoss)
                RepayDebt(p, dealt * DebtBossShare * p.DebtRepay, e.Pos, false);
        }

        /// Blood Lash's Clot (from the links Reap spends): each stack takes 4% off the damage you take.
        public static float ClotCut(Player p) => Math.Min(0.5f, (ClotCutEach + p.Stats.ClotCutBonus) * p.Clot);  // (Crimson Whip: more a stack)

        void AddClot(Player p, int n)
        {
            p.Clot = Math.Min(p.ClotMax, p.Clot + n);
            p.ClotT = ClotHold;
        }

        /// This enemy goes for `p` for a while (a cracking Blood Lash, its Snap, Martyr's Leash).
        void TauntEnemy(Player p, Enemy e, float time)
        {
            e.TargetId = p.Id;
            e.RetargetT = Math.Max(e.RetargetT, time);
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Hemomancer (Kit.Hemomancer): FireBloodWeapon, BloodAbility, TickHemomancers, BloodShotHit. See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef HemomancerKit = new KitDef
        {
            Id = Kit.Hemomancer, Name = "Hemomancer", Icon = "class_hemomancer",
            Blurb = "Blood magic: health is your resource. Every 1st ability costs a share of your health (never below 1 HP), every hit heals you for 3% of its damage, and you hit harder the lower your health (up to +40%). Shorter cooldowns make up for it. Full health and normal speed: live on the edge.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireBloodWeapon(p, aim, dir, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.BloodAbility(p, id, target),
            Tick = (g, dt) => g.TickHemomancers(dt),
            OnShotHit = (g, b, e, hit) => g.BloodShotHit(b, e, hit),  // Hemomancer shots drain, pool and drop orbs
            OnKill = (g, e) => { g.DebtKill(e); g.BlastKill(e); g.GolemKill(e); },  // a kill pays back Blood Debt (BloodDebt.cs), sends Blood Blast stacks home (BloodBlast.cs) and grows a Blood Golem a coat (BloodGolem.cs)
        };

        /// The Hemomancer's weapons (Kit.Hemomancer; FireBloodWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] HemomancerWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.BloodLash, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 0, Damage = 24f, Reach = 130f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach, +1 link per enemy and +1 max Clot", Name = "Blood Lash", FireInterval = 0.32f,
                Desc = "Primary Fire: A four-step blood whip that chains enemies, then yanks them in.\n\nAlternative Fire: Hold for Martyr's Leash." },
            new WeaponDef { Id = WeaponId.GoreMortar, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 1, Damage = 34f, BotRange = 380f, ExtraDesc = "+1 glob, fanned out", Name = "Gore Mortar", FireInterval = 0.75f,
                Desc = "Primary Fire: Hold to charge, then lob a blood clot that leaves a slowing, healing pool.",
                Sound = Sfx.SpellArcane, AreaDesc = "splash and pool 25% wider" },
            new WeaponDef { Id = WeaponId.LeechTendrils, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 2, Damage = 80f, Reach = 160f, Grow = 20f, BotRange = 130f, ExtraDesc = "+1 tendril and +20 px reach", Name = "Leech Tendrils", FireInterval = 0.1f,
                Desc = "Primary Fire: Hold to grab and stun nearby enemies; they burst when the blood builds up.",
                Sound = Sfx.SpellArcane, AreaDesc = "the tendrils reach across a 25% wider arc", HomingDesc = "the tendrils reach across a 15% narrower arc and drain 10% harder per stack, instead of bending shots" },
            new WeaponDef { Id = WeaponId.BloodBlast, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 3, Damage = 22f, Reach = 115f, Melee = true, BotRange = 100f, ExtraDesc = "+15% damage and reach", Name = "Blood Blast", FireInterval = 0.42f,
                Desc = "Primary Fire: A six-step blood sword combo; every hit marks enemies, and their deaths heal you." },
            // Healer
            new WeaponDef { Id = WeaponId.TransfusionDarts, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 0, Speed = 850f, Life = 0.85f, Damage = 7f, ExtraDesc = "+1 dart, fanned out", Name = "Transfusion Darts", FireInterval = 0.2f,
                Desc = "Primary Fire: Darts that heal hurt allies for more than they cost you." },
            new WeaponDef { Id = WeaponId.SanguineOrb, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 1, Speed = 420f, Life = 1.4f, Damage = 16f, ExtraDesc = "+1 orb, fanned out", Name = "Sanguine Orb", FireInterval = 0.45f, BotRange = 380f,
                Desc = "Primary Fire: Homing orbs that make enemies bleed, or heal a hurt ally once charged.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.VeinTether, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 2, Damage = 4f, Reach = 350f, Grow = 0.25f, ExtraDesc = "+25% beam length and +25% damage", Name = "Vein Tether", FireInterval = 0.08f,
                Desc = "Primary Fire: A draining beam that hurts enemies and heals you and the nearest ally.",
                Sound = Sfx.SpellArcane, AreaDesc = "the beam 25% wider" },
            // DPS
            new WeaponDef { Id = WeaponId.NeedleSwarm, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 0, Damage = 4f, Reach = Game.NsReach, ExtraDesc = "+20 needles in the swarm, +5 wrapped round each enemy and 10% harder cuts, +4 in the burst", Name = "Needle Swarm", FireInterval = 0.1f, BotRange = 220f,
                Desc = "A swarm of blood needles circles you, blocking shots. Lost needles are siphoned from your health.\n\nPrimary Fire: Hold to grow a tendril to the enemy nearest your cursor; it wraps them and cuts until they die.\n\nAlternative Fire: Throw a fan of needles from the swarm.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.Implosion, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 1, Damage = 34f, Reach = 480f, ExtraDesc = "+1 implosion: another enemy near your target collapses too (60% damage)", Name = "Implosion", FireInterval = 0.45f,
                Desc = "Primary Fire: Click an enemy to collapse its blood inward and pull nearby enemies in.",
                Sound = Sfx.SpellArcane, AreaDesc = "the collapse hurts and pulls in enemies 25% farther out" },
            new WeaponDef { Id = WeaponId.BloodBrawler, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 2, Damage = 18f, Reach = 520f, ExtraDesc = "+1 echo: every move also lands on the next enemy nearest the mark (50% damage)", Name = "Blood Brawler", FireInterval = Game.BsPulseTime,
                Desc = "Primary Fire: Click an enemy to work its blood from range, ending in a bone-shard burst.\n\nAlternative Fire: Bind the enemies near it so your moves hit them too.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.EmbolusSpears, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 3, Speed = Game.EsSpeed, Damage = 30f, ExtraDesc = "+20% spear and blast damage", Name = "Embolus Spears", FireInterval = 0.25f, BotRange = 600f,
                Desc = "Primary Fire: Throw a spear from each hand that pierces everything, then recall them to get health back.\n\nAlternative Fire: Burst the spears where they are.",
                Sound = Sfx.Throw, AreaDesc = "the burst 25% wider" },
        };

        /// The Hemomancer's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] HemomancerCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BloodBlast }, MaxStacks = 2, Name = "Blood Harvest", Desc = "The Blood Blast's stacks heal 1 more each, and an enemy holds 5 more",
                Apply = s => { s.BlastHealBonus += 1f; s.BlastCapBonus += 5; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.EmbolusSpears }, MaxStacks = 2, Name = "Burst Vessels", Desc = "The Embolus Spears' burst is 30% wider and hits 30% harder",
                Apply = s => s.EsBlastMult += 0.3f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodShell, MaxStacks = 2, Name = "Hardened Blood", Desc = "Blood Shell costs you 25% less health for each bullet it stops, and heals allies inside 50% more",
                Apply = s => s.ShellBonus += 1f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodWall, MaxStacks = 2, Name = "Blood Oath", Desc = "Blood Wall stands 2 s longer and its drain doubles 1 s slower",
                Apply = s => { s.WallLifeBonus += Game.WallLifeUp; s.WallRampBonus += Game.WallDoubleUp; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Exsanguinate, MaxStacks = 2, Name = "Deep Veins", Desc = "Exsanguinate reaches 30% farther and drains 50% more",
                Apply = s => { s.DrainRadiusMult += 0.3f; s.DrainMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodGift, MaxStacks = 2, Name = "Lifeblood", Desc = "Blood Gift gives 50% more",
                Apply = s => s.GiftMult += 0.5f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodFreeze, MaxStacks = 2, Name = "Hungry Earth", Desc = "Blood Freeze holds everything 1.5 s longer",
                Apply = s => { s.FreezeBonusTime += 1.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Martyr, MaxStacks = 2, Name = "Saint's Blood", Desc = "Martyr revives with 25% more health and leaves them invulnerable 1 s longer",
                Apply = s => { s.MartyrBonus += 0.25f; s.MartyrInvuln += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ArterialSpray, MaxStacks = 2, Name = "High Pressure", Desc = "Arterial Spray reaches 25% farther and hits 25% harder",
                Apply = s => { s.SprayReach += 0.25f; s.SprayMult += 0.25f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BloodDebt, MaxStacks = 2, Name = "Usury", Desc = "Blood Debt's kills repay 25% more, and it gives you 1 s longer to pay",
                Apply = s => { s.DebtRepay += 0.25f; s.DebtBonusTime += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.VeinRush, MaxStacks = 2, Name = "Crimson Tide", Desc = "Vein Rush goes 25% farther, and each enemy it cuts gives back 1% more health",
                Apply = s => { s.RushReach += 0.25f; s.RushRefund += 0.01f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ThornBloom, MaxStacks = 2, Name = "Thornwork", Desc = "Thorn Bloom's needles and tendrils hit 20% harder, and each tendril jumps once more",
                Apply = s => { s.TbDmgMult += 0.2f; s.TbExtraJumps++; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Hemomancer, MaxStacks = 2, Name = "Bloodthirst", Desc = "Your abilities cost 20% less health, and each kill heals you 2",
                Apply = s => { s.HealthCostMult *= 0.8f; s.KillHeal += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BloodLash }, MaxStacks = 2, Name = "Crimson Whip", Desc = "The Blood Lash reaches 20% farther, each enemy holds one more link, and each Clot stack cuts 1% more damage",
                Apply = s => { s.LashReachMult += 0.2f; s.LashLinkBonus++; s.ClotCutBonus += 0.01f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.GoreMortar }, MaxStacks = 2, Name = "Deep Pool", Desc = "The Gore Mortar's pool is 30% bigger and lasts 2 s longer",
                Apply = s => { s.PoolSizeMult += 0.3f; s.PoolBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.LeechTendrils }, MaxStacks = 2, Name = "Hungry Tendrils", Desc = "Leech Tendrils charge 30% faster, and their bursts hit 15% harder",
                Apply = s => { s.LeechDrainMult += 0.3f; s.LeechBlastBonus += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.TransfusionDarts }, MaxStacks = 2, Name = "Generous Blood", Desc = "Each Transfusion Dart gives an ally 30% more health for the same cost",
                Apply = s => { s.TransfuseMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.SanguineOrb }, MaxStacks = 2, Name = "Rich Blood", Desc = "The Sanguine Orb's heal reserve fills 30% faster, and its healing coat heals 30% more",
                Apply = s => { s.ReserveGainMult += 0.3f; s.CoatHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.VeinTether }, MaxStacks = 2, Name = "Red Thread", Desc = "15% more of the Vein Tether's damage heals",
                Apply = s => { s.VeinStealBonus += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.NeedleSwarm }, MaxStacks = 2, Name = "Hemophilia", Desc = "The Needle Swarm bursts an enemy one bleed stack sooner, 30% wider",
                Apply = s => { s.HemoHitCut += 1; s.HemoRadiusMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Implosion }, MaxStacks = 2, Name = "Event Horizon", Desc = "Implosion pulls enemies in from 30% farther, and the crush hits 40% harder",
                Apply = s => { s.ImplodeReachMult += 0.3f; s.ImplodeDmgMult += 0.4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BloodBrawler }, MaxStacks = 2, Name = "Marrow", Desc = "The Blood Brawler's Marrow Burst throws 2 more bone shards, and Sympathy binds 1 more enemy",
                Apply = s => { s.BsShardBonus += 2; s.SympBonus += 1; } },
        };

        /// The Hemomancer's abilities and ultimates: every 1st ability costs health. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] HemomancerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodShell, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 0,
                Name = "Blood Shell", Icon = "bloodshell", Cooldown = 9f, Desc = "Hold to wrap yourself and allies in a bubble that blocks bullets.",
                Capstone = "The burst also erases enemy bullets within 160 px.",
                Bot = b => b.Danger && b.Me.Hp > b.Me.MaxHp * 0.55f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodWall, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 1,
                Name = "Blood Wall", Icon = "pact", Cooldown = 8f, Desc = "Toggle a wall of blood behind you that blocks bullets and hurts enemies.",
                Capstone = "Enemies against the wall take double and are slowed 40%.",
                Bot = b => b.HaveTarget && b.TargetD < 320f && b.Me.Hp > b.Me.MaxHp * 0.7f,
            },
            new AbilityDef
            {
                Id = AbilityId.Exsanguinate, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 2,
                Name = "Exsanguinate", Icon = "drain", Cooldown = 8f, HealthCost = 0.1f, Desc = "Yank every nearby enemy in, stunned, and drain them.",
                Capstone = "Also slows the enemies it drains by 50% for 2 s.",
                Bot = b => b.HaveTarget && b.TargetD < 220f && b.Me.Hp > b.Me.MaxHp * 0.35f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodGift, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 0,
                Name = "Blood Gift", Icon = "gift", Cooldown = 8f, HealthCost = 0.25f, CostsCurrent = true, Desc = "Pay health to heal the most hurt ally twice as much.",
                Capstone = "Also heals everyone else within 200 px of them for half as much.",
                Bot = b => b.AllyHurt && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodFreeze, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 1,
                Name = "Blood Freeze", Icon = "bloodfreeze", Cooldown = 14f, HealthCost = 0.25f, Desc = "Freeze every enemy near you at a cost of health.",
                Capstone = "Frozen enemies take 25% more damage.",
                Bot = b => b.Danger && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Martyr, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 2,
                Name = "Martyr", Icon = "martyr", Cooldown = 16f, HealthCost = 0.4f, Desc = "Pay health to revive a downed ally, or ward the most hurt one.",
                Capstone = "You're invulnerable for 2 s after the sacrifice.",
                Bot = b => b.AllyDown || b.Players == 1 && b.Danger && b.Me.WardT <= 0f && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.ArterialSpray, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 0,
                Name = "Arterial Spray", Icon = "spray", Cooldown = 8f, Desc = "Open a vein: a jet of blood hurts and bleeds everything in front of you.",
                Capstone = "The spray also washes away enemy bullets it touches.",
                Bot = b => b.HaveTarget && b.TargetD < 190f && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodDebt, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 1,
                Name = "Blood Debt", Icon = "debt", Cooldown = 16f, Desc = "Borrow health to heal on hit, and pay it back with kills.",
                Capstone = "Kills repay 50% more.",
                Bot = b => b.HaveTarget && b.TargetD < 350f && b.Me.Hp > b.Me.MaxHp * 0.7f,
            },
            new AbilityDef
            {
                Id = AbilityId.VeinRush, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 2, Instant = true,
                Name = "Vein Rush", Icon = "veinrush", Cooldown = 6f, HealthCost = 0.1f, Desc = "Rush toward your cursor untouchable, bleeding enemies on the way.",
                Capstone = "The trail bursts after 1 s for 20 to everything on it.",
                Bot = b => b.HaveTarget && b.TargetD < 260f && b.Me.Hp > b.Me.MaxHp * 0.3f,
            },
            new AbilityDef
            {
                Id = AbilityId.ThornBloom, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (ThornBloom.cs)
                Name = "Thorn Bloom", Cooldown = TbCooldown, HealthCost = TbCost, Desc = "Burst blood needles all round that chain between enemies.",
                Capstone = "Tendrils jump 5 times.",
                Bot = b => b.NearEnemies >= 3 && b.TargetD < 240f && b.Me.Hp > b.Me.MaxHp * 0.4f,
            },
            new AbilityDef
            {
                Id = AbilityId.VeinTithe, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Vein Tithe", Cooldown = 75f, Desc = "Bind every enemy on screen at the cost of your health.",
                Capstone = "Lasts 40 s and costs 25% less.",
                Bot = b => b.Danger && b.HaveTarget && b.Me.Hp > b.Me.MaxHp * 0.6f,
            },
            new AbilityDef
            {
                Id = AbilityId.Bloodbath, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Bloodbath", Cooldown = BathCooldown, Desc = "Flood the floor with blood that slows and bleeds enemies and heals you.",
                Capstone = "The pool spreads to 220 px and gives back 35% of the bleed.",
                Bot = b => b.NearEnemies >= 3 && b.Me.Hp > b.Me.MaxHp * 0.4f,
            },
            new AbilityDef
            {
                Id = AbilityId.SanguineRain, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Sanguine Rain", Cooldown = 80f, Desc = "Rain healing circles that follow your allies.",
                Capstone = "Lasts 14 s and heals 4% a tick.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.CrimsonHarvest, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Crimson Harvest", Cooldown = 65f, Desc = "Rip blood from every enemy into a ball, then throw it as a huge blast.",
                Capstone = "Hold 8 s, and 25% more blood.",
                Bot = b => b.HaveTarget && b.TargetD < 500f,
            },
        };

        /// Hemomancer abilities and ultimates. Returns false if `id` isn't one. castPaid is what this cast cost.
        bool BloodAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            var blood = new Rgba(0.85f, 0.12f, 0.2f);
            switch (id)
            {
                case AbilityId.BloodShell:
                    RaiseShell(p, pw, cap);  // BloodShell.cs: held, not timed
                    BroadcastFx(FxKind.ShellPop, p.Pos, new V2(p.Id, 2f), blood, ShellRadius, 0.5f, Sfx.Ability);  // forming
                    return true;
                case AbilityId.BloodWall:
                    OpenBloodWall(p, pw, cap);  // BloodWall.cs: a toggle, laid as you walk
                    return true;
                case AbilityId.Exsanguinate:
                    CastExsanguinate(p, cap, blood);  // Exsanguinate.cs: the whips hook, the yank lands later
                    return true;
                case AbilityId.BloodGift:
                {
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && MoreHurt(q, ally))
                            ally = q;
                    float gift = castPaid * GiftMult * s.GiftMult;
                    if (ally == null)
                    {
                        // Alone: it becomes a shield for you.
                        p.ShieldHp = Math.Max(p.ShieldHp, gift * pw);
                        p.ShieldT = Math.Max(p.ShieldT, ShellTime);
                        BroadcastFx(FxKind.BloodShell, p.Pos, new V2(p.Id, 0f), blood, p.Def.BodyR, 0.45f, Sfx.SpellHoly);
                        return true;
                    }
                    Heal(ally, gift, p);
                    if (cap)
                        foreach (var q in Players.Values)
                            if (q != ally && !q.Downed && q.Pos.CloserThan(ally.Pos, GiftSplash))
                                Heal(q, gift * 0.5f, p);
                    BroadcastFx(FxKind.Drain, p.Pos, ally.Pos, blood, 10f, 0.5f, Sfx.None);  // your blood flowing into them
                    BroadcastFx(FxKind.Ring, ally.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.BloodFreeze:
                {
                    CastBloodFreeze(p, cap, s.FreezeBonusTime);  // BloodFreeze.cs
                    return true;
                }
                case AbilityId.Martyr:
                {
                    Player down = null, hurt = null;
                    foreach (var q in Players.Values)
                    {
                        if (q == p)
                            continue;
                        if (q.Downed && (down == null || q.Pos.DistanceSq(target) < down.Pos.DistanceSq(target)))
                            down = q;
                        if (!q.Downed && MoreHurt(q, hurt))
                            hurt = q;
                    }
                    if (down != null)
                    {
                        RevivePlayer(down, Math.Min(1f, (MartyrRevive + s.MartyrBonus) * pw), p);
                        down.InvulnT = Math.Max(down.InvulnT, 1.5f + s.MartyrInvuln);
                        castHits += 3;
                        BroadcastFx(FxKind.MartyrCast, p.Pos, down.Pos, blood, 1f + 10f * down.Id, Math.Max(MartyrFxTime, 1.5f + s.MartyrInvuln), Sfx.Revive);  // Game/MartyrLook.cs (the shimmer lasts the invulnerability)
                    }
                    else
                    {
                        // Nobody down: ward the most hurt ally (or yourself, alone) against going down.
                        var ward = hurt ?? p;
                        ward.WardT = MartyrWardTime;
                        ward.WardFrac = Math.Min(1f, (MartyrRevive + s.MartyrBonus) * pw);
                        ward.WardInvuln = 1.5f + s.MartyrInvuln;
                        castHits += 2;
                        BroadcastFx(FxKind.MartyrCast, p.Pos, ward.Pos, blood, 0f + 10f * ward.Id, MartyrFxTime, Sfx.SpellHoly);
                    }
                    if (cap)
                        p.InvulnT = Math.Max(p.InvulnT, 2f);
                    return true;
                }
                case AbilityId.ArterialSpray:
                    OpenSpray(p, pw, cap);  // ArterialSpray.cs: a toggle, not timed
                    return true;
                case AbilityId.BloodDebt:
                    BorrowDebt(p, pw, cap);  // BloodDebt.cs
                    return true;
                case AbilityId.VeinRush:
                    CastVeinRush(p, target, pw, cap);  // VeinRush.cs
                    return true;
                case AbilityId.ThornBloom:
                    CastThornBloom(p, pw, cap);  // ThornBloom.cs
                    return true;

                // Ultimates
                case AbilityId.VeinTithe:
                {
                    CastTithe(p, cap);  // VeinTithe.cs
                    return true;
                }
                case AbilityId.BloodGolem:
                    CastGolem(p, cap);  // BloodGolem.cs
                    return true;
                case AbilityId.Bloodbath:
                    CastBloodbath(p, cap);  // Bloodbath.cs
                    return true;
                case AbilityId.SanguineRain:
                    CastRain(p, cap);  // SanguineRain.cs
                    return true;
                case AbilityId.CrimsonHarvest:
                    CastHarvest(p, cap);  // CrimsonHarvest.cs
                    return true;
            }
            return false;
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Blood Wall drains and is laid, Blood Debt's window runs out, Arterial Spray gushes.
        void TickHemomancers(float dt)
        {
            TickCoatings(dt);
            TickRain(dt);  // SanguineRain.cs
            TickTithe(dt);  // VeinTithe.cs
            foreach (var p in Players.Values)  // a Bloodbath cleared with the wave's zones: its channel's time goes too (Bloodbath.cs)
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath && ActiveBath(p) == null)
                    p.UltT = 0f;
            TickReels(dt);  // Exsanguinate.cs
            TickBloodLash(dt);  // BloodLash.cs: chains, Reap, Martyr's Leash
            TickBloodBrawler(dt);  // BloodBrawler.cs: the string, the mark, Sympathy
            TickThornBloom(dt);  // ThornBloom.cs: needles and tendrils landing
            foreach (var p in Players.Values)
            {
                TickGrasp(p, dt);  // Leech Tendrils: cooldown, charge, cost, bursts
                if (p.Downed)
                {
                    EndBloodWall(p);  // BloodWall.cs
                    ForfeitDebt(p, false);  // BloodDebt.cs
                    if (p.SprayOn)
                        CloseSpray(p, false);  // ArterialSpray.cs
                    p.Clot = 0;
                    continue;
                }
                if (p.Clot > 0 && (p.ClotT -= dt) <= 0f)  // Clot holds while you keep hitting, then fades a stack at a time
                {
                    p.Clot--;
                    p.ClotT = ClotFade;
                }
                TickBloodWall(p, dt);  // BloodWall.cs
                TickDebt(p, dt);   // BloodDebt.cs
                TickSpray(p, dt);  // ArterialSpray.cs
            }
        }
    }
}

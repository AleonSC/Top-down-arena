using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Blood Mage: health is its resource. Every Space ability costs health (never below 1), it hits harder
    /// the more health it's missing (Blood Price, up to +40%), and all its hits heal it a little. Tanks turn
    /// health into protection, healers give their own health away, and DPS are strongest near death.
    public partial class Game
    {
        // The Clot Cannon (WeaponId.ClotBolt kept): hold to charge, let go to lob a clot over enemies. The charge sets only
        // how far it lands (ClotMin on a tap to ClotMax at full); the splash hits the same at any range, then a big pool
        // slows enemies and congeals after PoolCongeal, sticking the enemies in it for PoolStuck (not bosses).
        // Every shot costs ClotCost of your max health (never below 1); the pools heal everyone standing in them (the
        // Blood Pool zone's HealRate, Kits.cs), so you can win some of it back by wading into your own blood.
        public const float ClotMin = 160f, ClotMax = 520f, ClotSplash = 70f, ClotPoolRadius = 130f, ClotPoolTime = 4f, ClotFan = 0.2f, ClotCost = 0.06f;
        public static float ClotDistance(float c) => ClotMin + (ClotMax - ClotMin) * c;
        public static float ClotFlight(float c) => 0.45f + 0.3f * c;
        public static float ClotApex(float c) => 50f + 110f * c;  // how high it flies (drawing only)
        public const float PoolCongeal = 1f, PoolStuck = 1f;  // (each weapon's main reach, speed and damage are in Defs)
        const float BloodPriceMax = 0.4f, BloodSteal = 0.03f, LeechSteal = 0.25f, VeinSteal = 0.4f, VeinRange = 300f;
        public const float VeinBeamHalfWidth = 6f;  // Vein Tether's beam (its length and damage per tick are the weapon's Reach and Damage)
        public const float GraspArc = 70f * MathX.Pi / 180f;  // Leech Tendrils reach across this arc (their reach and damage per tick are the weapon's Reach and Damage)
        public const int GraspTendrils = 3;                   // enemies held at once (+1 per extra projectile or Barrage stack)
        const float GraspLetGo = 1.3f, GraspHold = 0.3f;      // a held enemy slips free past 130% of the reach; it stays stunned this long after each tick
        public const float BoneSpearCost = 2f, TransfuseCost = 4f, TransfuseHeal = 20f, TransfuseSteal = 0.5f;  // a dart gives an ally a lot, costs you a little; each enemy hit steals a sliver
        const float ShellTime = 5f;  // (Blood Gift's shield; Blood Shell itself is BloodShell.cs)
        const float DrainRadius = 220f, DrainAmount = 20f;  // Exsanguinate (Exsanguinate.cs)
        const float GiftMult = 2f, GiftSplash = 200f, MartyrRevive = 0.5f;
        public const float MartyrWardTime = 12f;  // also the halo's twelve beads, one a second (Game/MartyrLook.cs)
        public const float MartyrFxTime = 1.4f;  // how long a MartyrCast effect plays (beads, burst, spent ward)
        public const float ImplodeGrab = 26f;  // how near an enemy the click must land (how far away you can click, and the damage, are the weapon's Reach and Damage)
        public const float LashTipFrom = 0.7f, LashCrack = 2f, LashTaunt = 1.5f;  // Blood Lash: the outer 30% of its reach cracks
        public const int ClotMaxBase = 5;
        public const float ClotCutEach = 0.04f, ClotHold = 3f, ClotFade = 0.5f;  // each Clot stack; how long they hold, then one fades per ClotFade
        const float ClotBurstReach = 1.3f, ClotBurstMult = 2f, ClotBurstKnock = 50f;
        static readonly Rgba LashRed = new Rgba(0.85f, 0.1f, 0.18f);
        const float ImplodeSplash = 55f, ImplodeSplashDamage = 15f, ImplodePull = 18f, ImplodeChain = 180f, ImplodeChainMult = 0.6f;

        // --- Weapons ------------------------------------------------------------------

        /// A Blood Mage weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireBloodWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            var w = p.Weapon;  // its Speed, Life, Damage, Reach and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.BloodLash:
                {
                    p.ClotMax = ClotMaxBase + extra;
                    if (p.Clot >= p.ClotMax)
                    {
                        // Clot Burst: every stack spent on one lash all round you that shoves and taunts.
                        var b = NewSwing(p, aim, (w.Reach + w.Grow * extra) * ClotBurstReach, 360f, w.Damage * ClotBurstMult * dmg);
                        b.NoCut = true;
                        b.Knock = ClotBurstKnock;
                        b.Taunt = LashTaunt;
                        b.Heavy = true;
                        QueueSwing(b);
                        p.Clot = 0;
                        p.ClotT = 0f;
                        BroadcastFx(FxKind.Ring, p.Pos, LashRed, b.Reach, 0.35f, Sfx.SpellArcane);
                        break;
                    }
                    var s = NewSwing(p, aim, w.Reach + w.Grow * extra, 110f, w.Damage * dmg);
                    s.NoCut = true;  // a whip of blood, not a blade
                    s.Tip = s.Reach * LashTipFrom;
                    s.Clots = true;
                    QueueSwing(s);
                    break;
                }
                case WeaponId.ClotBolt:
                {
                    // The Clot Cannon: lobbed over enemies like the Golem Staff's boulder, as far as the charge sends it;
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
                        b.Pool = ClotPoolRadius * AreaMult(p);
                        b.PoolTime = ClotPoolTime;
                        b.Burst = 1;  // marks it as landing (MoveBullets)
                    }
                    break;
                }
                case WeaponId.LeechBeam:
                    // Leech Tendrils: no projectile; each tick the tendrils hold on, reach for more and drain (Collide).
                    pendingGrasps.Add(new Grasp { Owner = p.Id, Aim = aim, Extra = extra, Dmg = w.Damage * dmg });
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
                        b.HealAlly = TransfuseHeal * 9f / w.Damage;  // HealAllyHit scales it by Dmg / 9; the dart costs you TransfuseCost when it heals
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
                case WeaponId.BloodBolts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.BloodShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.07f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
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
                case WeaponId.BoneSpear:
                {
                    PayHealth(p, BoneSpearCost, p.Weapon.Name);
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.BoneSpearShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.12f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Pierce += 3;
                        b.Bolts = true;  // it pierces: Ricochet flings bounce bolts off it
                        b.DropsSpear = i == 0;  // stays stuck where it stops: walk over it for its health back
                    }
                    break;
                }
            }
        }

        // --- Vein Tether's beam ----------------------------------------------------------

        class VeinBeam { public int Owner, Pierce; public float Aim, Range, Dmg, HalfWidth; }

        readonly List<VeinBeam> pendingBeams = new List<VeinBeam>();
        readonly List<Enemy> beamHits = new List<Enemy>();

        /// Where a beam from `from` along `aim` stops: at the first enemy it can't pass (a shield, or the last it
        /// pierces), else at its full range. Fills `hits` (if given) with the enemies it touches, nearest first.
        /// The host uses real positions; screens pass `drawn` to use where enemies are drawn.
        public V2 BeamEnd(V2 from, float aim, float range, int pierce, List<Enemy> hits, bool drawn = false, float halfWidth = VeinBeamHalfWidth)
        {
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            beamOrder.Clear();
            foreach (var e in Enemies.Values)
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

        /// Called from Collide after the beams: each tick, held enemies too far away (or dead) slip free, free
        /// tendrils grab the nearest enemies in the arc in front of you, and every held enemy is stunned and drained.
        void ResolveGrasps()
        {
            foreach (var gr in pendingGrasps)
            {
                Player p;
                if (!Players.TryGetValue(gr.Owner, out p) || p.Downed)
                    continue;
                float reach = p.Def.BodyR + GraspReach(p);
                int max = GraspTendrils + gr.Extra + p.Stats.Fan;
                Enemy e;
                p.Grasp.RemoveAll(id => !Enemies.TryGetValue(id, out e) || IsSpawning(e) || e.Hp <= 0f || e.Pos.DistanceTo(p.Pos) - e.Radius > reach * GraspLetGo);
                if (p.Grasp.Count > max)
                    p.Grasp.RemoveRange(max, p.Grasp.Count - max);
                // Free tendrils reach for the nearest enemies in the arc (shields turn them away).
                graspNew.Clear();
                foreach (var o in Enemies.Values)
                    if (!IsSpawning(o) && o.Hp > 0f && !p.Grasp.Contains(o.Id) && o.Pos.DistanceTo(p.Pos) - o.Radius <= reach
                        && InGraspArc(p.Pos, gr.Aim, o.Pos, o.Radius, GraspArcFor(p)) && !BlocksShot(o, p.Pos))
                        graspNew.Add(o);
                graspNew.Sort((a, b) => a.Pos.DistanceSq(p.Pos).CompareTo(b.Pos.DistanceSq(p.Pos)));
                for (int i = 0; i < graspNew.Count && p.Grasp.Count < max; i++)
                    p.Grasp.Add(graspNew[i].Id);
                p.Run.ShotsFired++;
                if (p.Grasp.Count > 0)
                    p.Run.ShotsHit++;
                for (int i = 0, held = p.Grasp.Count; i < held && i < p.Grasp.Count; i++)
                {
                    if (!Enemies.TryGetValue(p.Grasp[i], out e) || e.Hp <= 0f)
                        continue;  // killed by another tendril's on-hit effects
                    if (!e.Def.IsBoss)
                        e.GraspT = Math.Max(e.GraspT, GraspHold);  // stunned while held (bosses are too strong to hold still)
                    V2 dir = (e.Pos - p.Pos).Normalized();
                    float hit = gr.Dmg * TendrilsOn(max, held, i) * ConeFocus(p) * NinjaHitMult(p.Id, Defs.LeechShot, e);  // one drain per tendril on it
                    HurtEnemy(e, hit, p.Id);
                    var pseudo = new Bullet { Pos = e.Pos, Vel = dir.IsZero ? V2.FromAngle(gr.Aim) : dir, Owner = p.Id, Dmg = hit, Kind = Defs.LeechShot };
                    BloodShotHit(pseudo, e, hit);  // the leech
                    OnBulletHit(pseudo, e);
                }
            }
            pendingGrasps.Clear();
        }

        /// How many of `max` tendrils hold the i-th of `held` enemies: one each, and spare tendrils (nothing else
        /// in reach) pile onto the ones already held, nearest first. A lone boss gets all of them.
        public static int TendrilsOn(int max, int held, int i) => held <= 0 ? 0 : 1 + Math.Max(0, max - held) / held + (i < Math.Max(0, max - held) % held ? 1 : 0);

        /// Are this player's Leech Tendrils out? (Each screen draws them itself, from the held enemies in the snapshot.)
        public bool GraspOn(Player p) =>
            p.Weapon.Id == WeaponId.LeechBeam && !p.Downed && !p.ParryActive && (p.Id == MyId || !IsHost ? p.FiringSeen : p.Firing)
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
            foreach (var e in Enemies.Values)
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

        /// Implosion only fires with an enemy under the cursor; otherwise holding fire waits (and keeps the cooldown).
        bool ImplodeReady(Player p) => p.Weapon.Id != WeaponId.Implosion || ImplodeTarget(p, p.AimAt) != null;

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
                if (!Players.TryGetValue(im.Owner, out p) || p.Downed || !Enemies.TryGetValue(im.Target, out e) || e.Hp <= 0f)
                    continue;
                p.Run.ShotsFired++;
                p.Run.ShotsHit++;
                imploded.Clear();
                Collapse(p, e, im.Dmg, true, blood);
                for (int k = 0; k < im.Extra; k++)
                {
                    Enemy next = null;
                    foreach (var o in Enemies.Values)
                        if (!imploded.Contains(o) && !IsSpawning(o) && o.Hp > 0f && o.Pos.DistanceTo(e.Pos) < ImplodeChain + o.Radius
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
            HitImplode(p, e, p.Weapon.Damage * mult);
            float r = e.Radius + ImplodeSplash * AreaMult(p);  // Homing Rounds widen it
            if (splash)
                foreach (var o in Enemies.Values)
                {
                    if (o == e || IsSpawning(o) || o.Hp <= 0f || o.Pos.DistanceTo(e.Pos) >= r + o.Radius)
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
                BeamEnd(from, beam.Aim, beam.Range, beam.Pierce, beamHits, false, beam.HalfWidth);
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

        /// Shot i of n, fanned out `step` radians apart around the aim.
        static float Fanned(float aim, int i, int n, float step) => aim + (n == 1 ? 0f : (i / (n - 1f) - 0.5f) * step * (n - 1));

        /// What a Blood Mage's shot does when it hits, after the damage (dmg) is dealt.
        void BloodShotHit(Bullet b, Enemy e, float dmg)
        {
            if (b.Kind < Defs.BloodShot || b.Kind > Defs.BoneSpearShot)
                return;
            Player p;
            if (!Players.TryGetValue(b.Owner, out p) || p.Downed)
                return;
            switch (b.Kind)
            {
                case Defs.LeechShot:
                    Heal(p, dmg * LeechSteal, p);
                    break;
                case Defs.TransfuseShot:
                    Heal(p, TransfuseSteal, p);  // a sliver of the enemy's blood (it's for giving, not for soloing)
                    break;
                case Defs.VeinShot:
                {
                    // Split between you and the nearest ally (all yours if no one is near).
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && q.Pos.DistanceTo(p.Pos) < VeinRange && (ally == null || q.Pos.DistanceSq(p.Pos) < ally.Pos.DistanceSq(p.Pos)))
                            ally = q;
                    float heal = dmg * VeinSteal;
                    if (ally == null)
                        Heal(p, heal, p);
                    else
                    {
                        Heal(p, heal * 0.5f, p);
                        Heal(ally, heal * 0.5f, p);
                    }
                    break;
                }
                case Defs.BloodShot:
                    if (p.Weapon.Id == WeaponId.BloodBolts)
                    {
                        string was = CreditTo(BulletSrc(b));
                        Hemorrhage(p, e, b.Dmg);
                        dmgSrc = was;
                    }
                    break;
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
                if (!Enemies.TryGetValue(c.Enemy, out e) || e.Hp <= 0f)
                    continue;
                WeaponHurt(e, c.Dmg, c.Owner);
                ApplyBleed(e, c.Dmg * SanguineBleed, c.Owner);  // KitWeapons.cs: each coat is another stack
                OnBulletHit(new Bullet { Pos = e.Pos, Owner = c.Owner, Dmg = c.Dmg, Kind = Defs.SanguineShot }, e);
                if (Players.TryGetValue(c.Owner, out p))
                    p.BloodReserve = Math.Min(ReserveMax, p.BloodReserve + ReserveGain);
            }
            foreach (var q in Players.Values)
            {
                if (q.CoatT <= 0f)
                    continue;
                q.CoatT = q.Downed ? 0f : Math.Max(0f, q.CoatT - dt);
                if (q.CoatT > 0f)
                    Heal(q, AllyCoatHeal / AllyCoatTime * dt, PlayerOrNull(q.CoatBy));
            }
        }

        // --- Blood Bolts: hemorrhage --------------------------------------------------------------------

        public const int HemorrhageHits = 5;
        public const float HemorrhageHold = 2f, HemorrhageMult = 4f, HemorrhageRadius = 80f;  // bolts fade after this; the burst is this many bolts' damage

        /// A Blood Bolt stuck in `e`; the fifth in a row (each within HemorrhageHold of the last) bursts it, hurting it
        /// and every enemy round it.
        void Hemorrhage(Player p, Enemy e, float boltDmg)
        {
            e.HemorrhageT = HemorrhageHold;
            if (++e.Hemorrhage < HemorrhageHits)
                return;
            e.Hemorrhage = 0;
            float dmg = boltDmg * HemorrhageMult, r = HemorrhageRadius * AreaMult(p);
            foreach (var o in Enemies.Values)
                if (Targetable(o) && o.Pos.DistanceTo(e.Pos) < r + o.Radius)
                    HurtEnemy(o, dmg, p.Id);
            BroadcastFx(FxKind.Implode, e.Pos, new Rgba(0.85f, 0.1f, 0.18f), r, 0.35f, Sfx.Explode);
        }

        void TickHemorrhage(Enemy e, float dt)
        {
            if (e.Hemorrhage > 0 && (e.HemorrhageT -= dt) <= 0f)
                e.Hemorrhage = 0;
        }

        // --- Bone Spear: pick your spears back up -----------------------------------------------------

        public const float SpearPickupTime = 8f, SpearPickupRadius = 16f;

        /// A Bone Spear stopped: it stays stuck in the ground there.
        void DropSpear(Bullet b)
        {
            Player p;
            if (b.DropsSpear && Players.TryGetValue(b.Owner, out p))
                AddZone(ZoneKind.BoneSpear, p, ClampToArena(b.Pos, 10f), SpearPickupRadius, SpearPickupTime);
        }

        /// Host, every tick: a Bone Spear its thrower walks over gives them its health back.
        void TickSpearPickups()
        {
            List<int> taken = null;
            foreach (var z in Zones.Values)
            {
                Player p;
                if (z.Kind != ZoneKind.BoneSpear || !Players.TryGetValue(z.Owner, out p) || p.Downed || p.Pos.DistanceTo(z.Pos) > z.Radius + p.Def.BodyR)
                    continue;
                Heal(p, BoneSpearCost, p);
                AddFx(FxKind.Spark, z.Pos, z.Def.Color, 16f, 0.25f);
                (taken = taken ?? new List<int>()).Add(z.Id);
            }
            if (taken != null)
                foreach (int id in taken)
                    Zones.Remove(id);
        }

        /// A Clot Cannon's clot landed: it splashes every enemy under it (weapon mods apply to each, as on a melee hit)
        /// and spreads a pool of blood.
        void LandClot(Bullet b)
        {
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.DistanceTo(b.Pos) < b.Splash + e.Radius && !BlocksShot(e, b.Pos))
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

        /// A Clot Cannon's pool sets: enemies still in it are stuck for a moment (not bosses).
        void CongealPool(Zone z)
        {
            z.Congealed = true;
            foreach (var e in Enemies.Values)
                if (Targetable(e) && !e.Def.IsBoss && e.Pos.DistanceTo(z.Pos) <= z.Radius + e.Radius)
                {
                    e.FrozenT = Math.Max(e.FrozenT, PoolStuck);
                    e.SlowT = Math.Max(e.SlowT, PoolStuck);
                }
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
            if (p.Downed || cost <= 0f)
                return 0f;
            float paid = Math.Max(0f, Math.Min(cost, p.Hp - 1f));
            p.Hp -= paid;
            Recorder?.HealthSpent(p, paid, what);
            return paid;
        }

        float castPaid;  // health paid for the ability being cast (Blood Mage abilities scale with it)

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

        /// Blood Price: a Blood Mage deals up to 40% more damage the more health it's missing (full bonus at 10%
        /// health). Applied in HurtEnemy.
        float BloodDamageMult(Player p)
        {
            if (p.Kit != Kit.Hemomancer || p.MaxHp <= 0f)
                return 1f;
            float missing = Math.Max(0f, Math.Min(1f, (1f - p.Hp / p.MaxHp) / 0.9f));
            return 1f + BloodPriceMax * missing;
        }

        /// Healing from damage dealt (HurtEnemy): a Blood Mage's 3%, Blood Debt's 15% while it runs, and a boss's blood
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

        /// Blood Lash's Clot: each stack takes 4% off the damage you take.
        public static float ClotCut(Player p) => Math.Min(0.5f, ClotCutEach * p.Clot);

        void AddClot(Player p, int n)
        {
            p.Clot = Math.Min(p.ClotMax, p.Clot + n);
            p.ClotT = ClotHold;
        }

        /// A Blood Lash hit: the tip cracks (its double damage is already in) and taunts; closer in, the enemy is
        /// flung out to the tip, where the next lash cracks it.
        void LashHit(Player p, Enemy e, Swing s, bool crack)
        {
            if (crack)
            {
                TauntEnemy(p, e, LashTaunt);
                BroadcastFx(FxKind.Ring, e.Pos, LashRed, e.Radius + 10f, 0.18f, Sfx.None);
                return;
            }
            if (Heavy(e))
                return;
            V2 away = (e.Pos - p.Pos).Normalized();
            if (away.IsZero)
                away = V2.FromAngle(s.Aim);
            e.Pos = ClampToArena(p.Pos + away * (s.Tip + e.Radius * 0.5f), e.Radius);
        }

        /// This enemy goes for `p` for a while (a Clot Burst, a cracking Blood Lash).
        void TauntEnemy(Player p, Enemy e, float time)
        {
            e.TargetId = p.Id;
            e.RetargetT = Math.Max(e.RetargetT, time);
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Hemomancer (Kit.Hemomancer): FireBloodWeapon, BloodAbility, TickBloodMages, BloodShotHit. See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef HemomancerKit = new KitDef
        {
            Id = Kit.Hemomancer, Name = "Hemomancer", Icon = "class_hemomancer",
            Blurb = "Blood spells that drain and heal. Abilities cost health; hits harder the lower your health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireBloodWeapon(p, aim, dir, muzzle, dmg, extra),
            Ability = (g, p, id, target) => g.BloodAbility(p, id, target),
            Tick = (g, dt) => g.TickBloodMages(dt),
            OnShotHit = (g, b, e, hit) => g.BloodShotHit(b, e, hit),  // Blood Mage shots drain, pool and drop orbs
            OnKill = (g, e) => g.DebtKill(e),  // a kill pays back Blood Debt (BloodDebt.cs)
        };

        /// The Hemomancer's weapons (Kit.Hemomancer; FireBloodWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] HemomancerWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.BloodLash, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 0, Damage = 24f, Reach = 130f, Grow = 20f, Melee = true, ExtraDesc = "+20 px reach and +1 max Clot", Name = "Blood Lash", FireInterval = 0.5f,
                Desc = "A whip of blood. Its tip cracks for double damage and taunts, and enemies hit closer in are flung out to the tip. "
                    + "Each hit clots your blood (+1 Clot, +2 from the tip; 4% less damage taken each, up to 5). At full Clot your next lash bursts all round you." },
            new WeaponDef { Id = WeaponId.ClotBolt, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 1, Damage = 34f, BotRange = 380f, ExtraDesc = "+1 clot, fanned out", Name = "Clot Cannon", FireInterval = 0.75f,
                Desc = "Hold to charge, let go to lob a massive blood clot over enemies; the longer you hold, the farther it flies, and it hits just as hard at any range. Each shot costs 6% of your max health. It bursts into a big pool of blood that slows enemies and slowly heals anyone standing in it, and after a moment the pool congeals and holds enemies still.",
                Sound = Sfx.SpellArcane, AreaDesc = "splash and pool 25% wider" },
            new WeaponDef { Id = WeaponId.LeechBeam, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 2, Damage = 1.2f, Reach = 160f, Grow = 20f, BotRange = 130f, ExtraDesc = "+1 tendril and +20 px reach", Name = "Leech Tendrils", FireInterval = 0.1f,
                Desc = "Hold to lash out blood tendrils that grasp nearby enemies while you hold on. They are stunned and drained, and part of the damage heals you.",
                Sound = Sfx.SpellArcane, AreaDesc = "the tendrils reach across a 25% wider arc", HomingDesc = "the tendrils reach across a 15% narrower arc and drain 10% harder per stack, instead of bending shots" },
            // Healer
            new WeaponDef { Id = WeaponId.TransfusionDarts, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 0, Speed = 850f, Life = 0.85f, Damage = 7f, ExtraDesc = "+1 dart, fanned out", Name = "Transfusion Darts", FireInterval = 0.2f,
                Desc = "Darts that transfuse your blood: one that reaches a hurt ally gives them more health than it costs you. Each enemy hit steals a sliver back." },
            new WeaponDef { Id = WeaponId.SanguineOrb, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 1, Speed = 420f, Life = 1.4f, Damage = 16f, ExtraDesc = "+1 orb, fanned out", Name = "Sanguine Orb", FireInterval = 0.45f, BotRange = 380f,
                Desc = "Orbs of blood that home in at the last moment, circle an enemy and cover it, making it bleed and charging your heal reserve. "
                    + "With 25 reserve, an orb that touches a hurt ally coats them in healing blood instead (30 health over 4 s).",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.VeinTether, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 2, Damage = 4f, Reach = 350f, Grow = 0.25f, ExtraDesc = "+25% beam length and +25% damage", Name = "Vein Tether", FireInterval = 0.08f,
                Desc = "A draining beam that hurts whatever you hold it on. Part of its damage heals, split between you and the nearest ally.",
                Sound = Sfx.SpellArcane, AreaDesc = "the beam 25% wider" },
            // DPS
            new WeaponDef { Id = WeaponId.BloodBolts, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 0, Speed = 800f, Life = 0.8f, Damage = 9f, ExtraDesc = "+1 bolt, fanned out", Name = "Blood Bolts", FireInterval = 0.13f,
                Desc = "Fast bolts of blood that stick in enemies. Enough bolts in one enemy make it hemorrhage, bursting for damage to it and the enemies around it.",
                Sound = Sfx.SpellArcane },
            new WeaponDef { Id = WeaponId.Implosion, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 1, Damage = 34f, Reach = 480f, ExtraDesc = "+1 implosion: another enemy near your target collapses too (60% damage)", Name = "Implosion", FireInterval = 0.45f,
                Desc = "Click an enemy: its blood collapses inward, hurting enemies pressed against it and pulling them in.",
                Sound = Sfx.SpellArcane, AreaDesc = "the collapse hurts and pulls in enemies 25% farther out" },
            new WeaponDef { Id = WeaponId.BoneSpear, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 2, Speed = 1000f, Life = 0.9f, Damage = 60f, ExtraDesc = "+1 spear, fanned out (costs no more health)", Name = "Bone Spear", FireInterval = 0.8f,
                Desc = "A heavy spear that pierces enemies. Each throw costs a little health, but the spear stays stuck where it stops, and walking over it gives the health back." },
        };

        /// The Blood Mage's abilities and ultimates: every Space ability costs health. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] HemomancerAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodShell, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 0,
                Name = "Blood Shell", Cooldown = 9f, Desc = "Hold to wrap yourself in a big bubble of blood. It blocks bullets and enemies; allies can stand inside. Each bullet it stops costs you 7 health and heals every ally inside for 4. It bursts at a third of your health, or when you let go.",
                Capstone = "The burst also erases enemy bullets within 160 px.",
                Bot = b => b.Danger && b.Me.Hp > b.Me.MaxHp * 0.55f,
            },
            new AbilityDef
            {
                Id = AbilityId.CrimsonPact, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 1,
                Name = "Blood Wall", Cooldown = 8f, Desc = "Toggle: a wall of blood is laid behind you as you walk. It stops enemy bullets and enemies can't cross it (they bleed 12 a second against it); allies walk through. It stands 6 s. It drains 1% of your max health a second, doubling every 4 s, until you press again or go down.",
                Capstone = "Enemies against the wall take double and are slowed 40%.",
                Bot = b => b.HaveTarget && b.TargetD < 320f && b.Me.Hp > b.Me.MaxHp * 0.7f,
            },
            new AbilityDef
            {
                Id = AbilityId.Exsanguinate, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = 2,
                Name = "Exsanguinate", Cooldown = 8f, HealthCost = 0.1f, Desc = "Pay 10% of your max health: barbed blood whips hook every enemy within 220 px and yank them in to you, stunned on the way. Each drains 20 health into you as it lands. Heavy enemies and bosses stay put but are still drained.",
                Capstone = "Also slows the enemies it drains by 50% for 2 s.",
                Bot = b => b.HaveTarget && b.TargetD < 220f && b.Me.Hp > b.Me.MaxHp * 0.35f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodGift, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 0,
                Name = "Blood Gift", Cooldown = 8f, HealthCost = 0.25f, CostsCurrent = true, Desc = "Pay 25% of your current health: the most hurt ally heals twice as much. Alone, it becomes a shield for you.",
                Capstone = "Also heals everyone else within 200 px of them for half as much.",
                Bot = b => b.AllyHurt && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.SiphonField, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 1,
                Name = "Blood Freeze", Cooldown = 14f, HealthCost = 0.25f, Desc = "Pay 25% of your max health: a shockwave bursts out 260 px around you and freezes every enemy it reaches for 5 s (bosses for 3 s). Frozen enemies can't move or shoot.",
                Capstone = "Frozen enemies take 25% more damage.",
                Bot = b => b.Danger && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Martyr, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = 2,
                Name = "Martyr", Cooldown = 16f, HealthCost = 0.4f, Desc = "Pay 40% of your max health to revive the downed ally nearest your cursor, anywhere, at 50% health. If no one is down, the most hurt ally (you, if you're alone) is warded for 12 s: if they go down, they get straight back up at 50%.",
                Capstone = "You're invulnerable for 2 s after the sacrifice.",
                Bot = b => b.AllyDown || b.Players == 1 && b.Danger && b.Me.WardT <= 0f && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodBoil, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 0,
                Name = "Arterial Spray", Cooldown = 8f, Desc = "Press to open a vein: a 60° jet of blood, 200 px long, follows your aim, 30 damage a second to everything in it (more the lower your health), slowing them 20% and making them bleed. It drains 4% of your max health a second. Press again to stop; it sputters out at 15% health. The cooldown starts when it stops.",
                Capstone = "The spray also washes away enemy bullets it touches.",
                Bot = b => b.HaveTarget && b.TargetD < 190f && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.FrenzyPact, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 1,
                Name = "Blood Debt", Cooldown = 16f, Desc = "Borrow against your blood: your health drops to 25% (or 5% of your max, if you're already that low) and the rest becomes a debt. For 8 s, kills pay it back (a Grunt 4% of your max health, a Heavy or an elite 10%, hurting a boss 10% of the damage) and your hits heal 15% instead of 3%. Pay it all and you keep the 15% for 2 s more; what's unpaid after 8 s is lost.",
                Capstone = "Kills repay 50% more.",
                Bot = b => b.HaveTarget && b.TargetD < 350f && b.Me.Hp > b.Me.MaxHp * 0.7f,
            },
            new AbilityDef
            {
                Id = AbilityId.HemoNova, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = 2, Instant = true,
                Name = "Vein Rush", Cooldown = 6f, HealthCost = 0.1f, Desc = "Pay 10% of your max health and rush 280 px toward your cursor as a streak of blood, untouchable. Every enemy you pass through takes 25 and bleeds, and gives back 2% of your max health. You leave a trail of blood that slows enemies 30% for 2 s.",
                Capstone = "The trail bursts after 1 s for 20 to everything on it.",
                Bot = b => b.HaveTarget && b.TargetD < 260f && b.Me.Hp > b.Me.MaxHp * 0.3f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodRite, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Vein Tithe", Cooldown = 75f, Desc = "Blood tendrils bind every enemy on screen for 30 s: they can't move or shoot. You pay 0.25% of your max health a second for each one held (a boss costs four times that), and deal 40% less damage. It ends when the time runs out or you go down.",
                Capstone = "Lasts 40 s and costs 25% less.",
                Bot = b => b.Danger && b.HaveTarget && b.Me.Hp > b.Me.MaxHp * 0.6f,
            },
            new AbilityDef
            {
                Id = AbilityId.Bloodbath, Kit = Kit.Hemomancer, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 1,
                Name = "Bloodbath", Cooldown = BathCooldown, Desc = "Channel: blood floods the floor 170 px round you (it stays where you cast it) for up to 10 s, draining 4% of your max health a second. Enemies wading in it are slowed 50% (Heavy enemies 35%, bosses 20%) and bleed 15 a second, and every drop heals you for 25% of it, wherever you are. Allies standing in it, you included, take 20% less damage. Press again to stop; it also stops at 15% health or if you go down.",
                Capstone = "The pool spreads to 220 px and gives back 35% of the bleed.",
                Bot = b => b.NearEnemies >= 3 && b.Me.Hp > b.Me.MaxHp * 0.4f,
            },
            new AbilityDef
            {
                Id = AbilityId.SanguineRain, Kit = Kit.Hemomancer, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Sanguine Rain", Cooldown = 80f, Desc = "Draw 12% of your health into a blood orb (1.8 s), then rain circles follow your allies for 10 s, trickling 3% of their health every 0.5 s. It drains you 3% a second and ends when you press it again or fall to 15%.",
                Capstone = "Lasts 14 s and heals 4% a tick.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.CrimsonApotheosis, Kit = Kit.Hemomancer, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Crimson Harvest", Cooldown = 65f, Desc = "Rip blood from every enemy in the arena (20 damage each) into a floating ball, bigger the more enemies fed it. Press again to throw it at your cursor for a huge blast; it throws itself after 5 s.",
                Capstone = "Hold 8 s, and 25% more blood.",
                Bot = b => b.HaveTarget && b.TargetD < 500f,
            },
        };

        /// Blood Mage abilities and ultimates. Returns false if `id` isn't one. castPaid is what this cast cost.
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
                case AbilityId.CrimsonPact:
                    OpenBloodWall(p, pw, cap);  // BloodWall.cs: a toggle, laid as you walk
                    return true;
                case AbilityId.Exsanguinate:
                    CastExsanguinate(p, cap, blood);  // Exsanguinate.cs: the whips hook, the yank lands later
                    return true;
                case AbilityId.BloodGift:
                {
                    Player ally = null;
                    foreach (var q in Players.Values)
                        if (q != p && !q.Downed && (ally == null || q.Hp / q.MaxHp < ally.Hp / ally.MaxHp))
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
                            if (q != ally && !q.Downed && q.Pos.DistanceTo(ally.Pos) < GiftSplash)
                                Heal(q, gift * 0.5f, p);
                    BroadcastFx(FxKind.Drain, p.Pos, ally.Pos, blood, 10f, 0.5f, Sfx.None);  // your blood flowing into them
                    BroadcastFx(FxKind.Ring, ally.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.SiphonField:
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
                        if (!q.Downed && (hurt == null || q.Hp / q.MaxHp < hurt.Hp / hurt.MaxHp))
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
                case AbilityId.BloodBoil:
                    OpenSpray(p, pw, cap);  // ArterialSpray.cs: a toggle, not timed
                    return true;
                case AbilityId.FrenzyPact:
                    BorrowDebt(p, pw, cap);  // BloodDebt.cs
                    return true;
                case AbilityId.HemoNova:
                    CastVeinRush(p, target, pw, cap);  // VeinRush.cs
                    return true;

                // Ultimates
                case AbilityId.BloodRite:
                {
                    CastTithe(p, cap);  // VeinTithe.cs
                    return true;
                }
                case AbilityId.Bloodbath:
                    CastBloodbath(p, cap);  // Bloodbath.cs
                    return true;
                case AbilityId.SanguineRain:
                    CastRain(p, cap);  // SanguineRain.cs
                    return true;
                case AbilityId.CrimsonApotheosis:
                    CastHarvest(p, cap);  // CrimsonHarvest.cs
                    return true;
            }
            return false;
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Blood Wall drains and is laid, Blood Debt's window runs out, Arterial Spray gushes.
        void TickBloodMages(float dt)
        {
            TickCoatings(dt);
            TickRain(dt);  // SanguineRain.cs
            TickTithe(dt);  // VeinTithe.cs
            foreach (var p in Players.Values)  // a Bloodbath cleared with the wave's zones: its channel's time goes too (Bloodbath.cs)
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.Bloodbath && ActiveBath(p) == null)
                    p.UltT = 0f;
            TickReels(dt);  // Exsanguinate.cs
            foreach (var p in Players.Values)
            {
                if (p.Grasp.Count > 0 && (p.Downed || !p.Firing || p.ParryActive || p.Weapon.Id != WeaponId.LeechBeam))
                    p.Grasp.Clear();  // let go of fire and the tendrils let go too
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

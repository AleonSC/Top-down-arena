using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Blood Mage: health is its resource. Every Space ability costs health (never below 1), it hits harder
    /// the more health it's missing (Blood Price, up to +40%), and all its hits heal it a little. Tanks turn
    /// health into protection, healers give their own health away, and DPS are strongest near death.
    public partial class Game
    {
        const float ClotPoolRadius = 70f, ClotPoolTime = 3f;  // (each weapon's main reach, speed and damage are in Defs)
        const float BloodPriceMax = 0.4f, BloodSteal = 0.03f, LeechSteal = 0.25f, VeinSteal = 0.4f, VeinRange = 300f, SanguineDrop = 0.4f;
        public const float VeinBeamHalfWidth = 6f;  // Vein Tether's beam (its length and damage per tick are the weapon's Reach and Damage)
        public const float GraspArc = 70f * MathX.Pi / 180f;  // Leech Tendrils reach across this arc (their reach and damage per tick are the weapon's Reach and Damage)
        public const int GraspTendrils = 3;                   // enemies held at once (+1 per extra projectile or Barrage stack)
        const float GraspLetGo = 1.3f, GraspHold = 0.3f;      // a held enemy slips free past 130% of the reach; it stays stunned this long after each tick
        public const float BoneSpearCost = 2f, TransfuseCost = 3f;
        const float ShellTime = 5f, ShellMult = 3f;
        const float PactTime = 4f, PactRadius = 220f, PactBurst = 1.5f, PactMasteredCut = 0.3f;
        const float DrainRadius = 220f, DrainAmount = 20f, DrainPull = 60f;
        const float GiftMult = 2f, GiftSplash = 200f, SiphonRadius = 140f, SiphonTime = 6f, MartyrRevive = 0.5f;
        const float BoilRadius = 260f, BoilDelay = 1f, BoilMult = 3f, BoilSplash = 80f;
        const float RushTime = 6f, RushRate = 0.5f, RushSteal = 0.1f, RushDrain = 3f;
        const float NovaDamage = 20f, NovaHeal = 3f;
        const float MoonCut = 0.6f, RainSteal = 0.2f;
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
                        pendingSwings.Add(b);
                        p.Clot = 0;
                        p.ClotT = 0f;
                        BroadcastFx(FxKind.Ring, p.Pos, LashRed, b.Reach, 0.35f, Sfx.SpellArcane);
                        break;
                    }
                    var s = NewSwing(p, aim, w.Reach + w.Grow * extra, 110f, w.Damage * dmg);
                    s.NoCut = true;  // a whip of blood, not a blade
                    s.Tip = s.Reach * LashTipFrom;
                    s.Clots = true;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.ClotBolt:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.ClotShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.2f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.Pool = ClotPoolRadius * AreaMult(p);
                        b.PoolTime = ClotPoolTime;
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
                    if (p.SurgeT > 0f)  // Arcane Surge, as on bullets
                    {
                        beamDmg *= 1.6f;
                        pierce += 2;
                    }
                    pendingBeams.Add(new VeinBeam { Owner = p.Id, Aim = aim, Range = w.Reach * k * p.Stats.ShotSpeedMult, Dmg = beamDmg, Pierce = pierce, HalfWidth = VeinBeamHalfWidth * AreaMult(p) });  // Velocity Rounds stretch it
                    break;
                }
                case WeaponId.TransfusionDarts:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        var b = SpawnBullet(Defs.TransfuseShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.08f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                        b.HealAlly = 8f * 9f / 7f;  // HealAllyHit scales it by Dmg / 9; the dart costs you TransfuseCost when it heals
                    }
                    break;
                }
                case WeaponId.SanguineOrb:
                {
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.SanguineShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.35f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id, false, 3f);
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
                    PayHealth(p, BoneSpearCost);
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.BoneSpearShot, muzzle, V2.FromAngle(Fanned(aim, i, n, 0.12f)) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Pierce += 3;
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
                if (e.SpawnT > 0f || e.Hp <= 0f)
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

        /// How wide the player's tendrils reach (Homing Rounds widen it 25% a stack).
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
                foreach (int id in p.Grasp)
                {
                    if (!Enemies.TryGetValue(id, out e) || e.Hp <= 0f)
                        continue;  // killed by another tendril's on-hit effects
                    if (!e.Def.IsBoss)
                        e.GraspT = Math.Max(e.GraspT, GraspHold);  // stunned while held (bosses are too strong to hold still)
                    V2 dir = (e.Pos - p.Pos).Normalized();
                    float hit = gr.Dmg * HomingCost(p) * NinjaHitMult(p.Id, Defs.LeechShot, e);
                    HurtEnemy(e, hit, p.Id);
                    var pseudo = new Bullet { Pos = e.Pos, Vel = dir.IsZero ? V2.FromAngle(gr.Aim) : dir, Owner = p.Id, Dmg = hit, Kind = Defs.LeechShot };
                    BloodShotHit(pseudo, e, hit);  // the leech
                    OnBulletHit(pseudo, e);
                }
            }
            pendingGrasps.Clear();
        }

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
            if (b.HealOwner > 0f)
                Heal(p, b.HealOwner, p);  // Hemo Nova
            switch (b.Kind)
            {
                case Defs.LeechShot:
                    Heal(p, dmg * LeechSteal, p);
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
                case Defs.SanguineShot:
                    if (e.Hp <= 0f && !e.BloodDropped)
                    {
                        e.BloodDropped = true;
                        if (rng.NextDouble() < SanguineDrop)
                            SpawnOrb(e.Pos, false);
                    }
                    break;
                case Defs.ClotShot:
                    AddZone(ZoneKind.BloodPool, p, b.Pos, b.Pool, b.PoolTime);
                    break;
            }
        }

        /// Transfusion Darts cost the healer some of their own health for each ally they heal.
        void TransfusionCost(Bullet b)
        {
            Player p;
            if (b.Kind == Defs.TransfuseShot && Players.TryGetValue(b.Owner, out p))
                PayHealth(p, TransfuseCost);
        }

        // --- Health as a resource ---------------------------------------------------------

        /// Spend health, never below 1 (or nothing while down). Returns how much was actually paid.
        float PayHealth(Player p, float cost)
        {
            if (p.Downed || cost <= 0f)
                return 0f;
            float paid = Math.Max(0f, Math.Min(cost, p.Hp - 1f));
            p.Hp -= paid;
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
                castPaid = PayHealth(p, (d.CostsCurrent ? p.Hp : p.MaxHp) * d.HealthCost);
        }

        /// Blood Price: a Blood Mage deals up to 40% more damage the more health it's missing (full bonus at 10%
        /// health); during Crimson Apotheosis up to 100% more. Applied in HurtEnemy.
        float BloodDamageMult(Player p)
        {
            if (p.Kit != Kit.BloodMage || p.MaxHp <= 0f)
                return 1f;
            float missing = Math.Max(0f, Math.Min(1f, (1f - p.Hp / p.MaxHp) / 0.9f));
            return 1f + (Exalted(p) ? 1f : BloodPriceMax) * missing;
        }

        float rainSteal;  // the strongest Sanguine Rain up right now (0 if none)

        /// Healing from damage dealt (HurtEnemy): a Blood Mage's 3% and Frenzy Pact's 10%, and Sanguine Rain for everyone.
        void BloodLifesteal(Player p, float dealt)
        {
            if (p.Downed || dealt <= 0f)
                return;
            float frac = rainSteal;
            if (p.Kit == Kit.BloodMage)
                frac += BloodSteal;
            if (p.RushT > 0f)
                frac += RushSteal;
            if (frac > 0f)
                Heal(p, dealt * frac, p);
            if (Exalted(p))
                p.ApoDealt += dealt;
        }

        /// Faster firing during Frenzy Pact (FireRate).
        static float BloodFireRate(Player p) => p.RushT > 0f ? 1f + RushRate + p.Stats.RushRate : 1f;

        bool Exalted(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.CrimsonApotheosis;
        bool BloodRiteUp(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BloodRite;

        /// Enemies all go for a Blood Mage under Crimson Pact or Blood Rite (PickTarget).
        bool BloodTaunting(Player p) => p.PactT > 0f || BloodRiteUp(p);

        /// Damage cut from Blood Rite and a mastered Crimson Pact (Damage).
        float BloodCut(Player p) => Math.Max(BloodRiteUp(p) ? MoonCut : 0f, p.PactT > 0f && p.PactMastered ? PactMasteredCut : 0f);

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

        /// Damage about to come off a player's health (after shields): Crimson Pact stores it, Blood Rite turns it
        /// into healing for everyone else.
        void BloodTookHit(Player p, float amount)
        {
            if (amount <= 0f)
                return;
            if (p.PactT > 0f)
                p.PactStore += amount;
            if (BloodRiteUp(p))
                foreach (var q in Players.Values)
                    if (q != p && !q.Downed)
                        Heal(q, amount, p);
        }

        // --- Abilities ------------------------------------------------------------------

        readonly List<Boil> boils = new List<Boil>();

        class Boil
        {
            public int Enemy, Owner;
            public float T, Dmg;
            public bool Splash;
        }

        /// The Blood Mage's abilities and ultimates: every Space ability costs health. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] BloodMageAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.BloodShell, Kit = Kit.BloodMage, Role = Role.Tank, Choice = 0,
                Name = "Blood Shell", Cooldown = 9f, HealthCost = 0.15f, Desc = "Pay 15% of your max health for a shield worth three times as much, for 5 s.",
                Capstone = "Also erases enemy bullets within 160 px.",
                Bot = b => b.Danger && b.Me.Hp > b.Me.MaxHp * 0.4f,
            },
            new AbilityDef
            {
                Id = AbilityId.CrimsonPact, Kit = Kit.BloodMage, Role = Role.Tank, Choice = 1,
                Name = "Crimson Pact", Cooldown = 10f, HealthCost = 0.1f, Desc = "Pay 10% of your max health: every enemy targets you for 4 s, then the damage you took meanwhile bursts back out at 150% within 220 px.",
                Capstone = "You take 30% less damage while it lasts.",
                Bot = b => b.HaveTarget && b.TargetD < 260f && b.Me.Hp > b.Me.MaxHp * 0.6f,
            },
            new AbilityDef
            {
                Id = AbilityId.Exsanguinate, Kit = Kit.BloodMage, Role = Role.Tank, Choice = 2,
                Name = "Exsanguinate", Cooldown = 8f, HealthCost = 0.1f, Desc = "Pay 10% of your max health to pull enemies within 220 px toward you and drain 20 health from each into you.",
                Capstone = "Also slows the enemies it drains by 50% for 2 s.",
                Bot = b => b.HaveTarget && b.TargetD < 220f && b.Me.Hp > b.Me.MaxHp * 0.35f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodGift, Kit = Kit.BloodMage, Role = Role.Healer, Choice = 0,
                Name = "Blood Gift", Cooldown = 8f, HealthCost = 0.25f, CostsCurrent = true, Desc = "Pay 25% of your current health: the most hurt ally heals twice as much. Alone, it becomes a shield for you.",
                Capstone = "Also heals everyone else within 200 px of them for half as much.",
                Bot = b => b.AllyHurt && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.SiphonField, Kit = Kit.BloodMage, Role = Role.Healer, Choice = 1,
                Name = "Siphon Field", Cooldown = 11f, HealthCost = 0.1f, Desc = "Pay 10% of your max health for a 140 px field at your feet for 6 s: enemies inside lose 10 HP/s and allies inside heal 10 HP/s.",
                Capstone = "The field follows you.",
                Bot = b => b.AllyHurt && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Martyr, Kit = Kit.BloodMage, Role = Role.Healer, Choice = 2,
                Name = "Martyr", Cooldown = 16f, HealthCost = 0.4f, Desc = "Pay 40% of your max health to revive the downed ally nearest your cursor, anywhere, at 50% health. If no one is down, the most hurt ally heals twice what you paid.",
                Capstone = "You're invulnerable for 2 s after the sacrifice.",
                Bot = b => b.AllyDown,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodBoil, Kit = Kit.BloodMage, Role = Role.Dps, Choice = 0,
                Name = "Blood Boil", Cooldown = 8f, HealthCost = 0.2f, Desc = "Pay 20% of your max health: enemies within 260 px boil and burst after 1 s for three times what you paid.",
                Capstone = "Each burst also hits enemies within 80 px of it.",
                Bot = b => b.HaveTarget && b.TargetD < 250f && b.Me.Hp > b.Me.MaxHp * 0.45f,
            },
            new AbilityDef
            {
                Id = AbilityId.FrenzyPact, Kit = Kit.BloodMage, Role = Role.Dps, Choice = 1,
                Name = "Frenzy Pact", Cooldown = 11f, HealthCost = 0.05f, Desc = "Pay 5% of your max health: for 6 s you fire 50% faster and heal 10% of the damage you deal, but lose 3 HP/s.",
                Capstone = "No health drain.",
                Bot = b => b.HaveTarget && b.TargetD < 500f && b.Me.Hp > b.Me.MaxHp * 0.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.HemoNova, Kit = Kit.BloodMage, Role = Role.Dps, Choice = 2,
                Name = "Hemo Nova", Cooldown = 6f, HealthCost = 0.1f, Desc = "Pay 10% of your max health to fire a ring of 16 blood bolts (20 each). Each that hits heals you 3.",
                Capstone = "Bolts pierce 2 enemies.",
                Bot = b => b.HaveTarget && b.TargetD < 220f && b.Me.Hp > b.Me.MaxHp * 0.35f,
            },
            new AbilityDef
            {
                Id = AbilityId.BloodRite, Kit = Kit.BloodMage, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Blood Rite", Cooldown = 75f, Desc = "For 8 s every enemy targets you and you take 60% less damage. The damage you take heals your allies.",
                Capstone = "Lasts 12 s.",
                Bot = b => b.Danger || b.HaveTarget && b.Players > 1,
            },
            new AbilityDef
            {
                Id = AbilityId.SanguineRain, Kit = Kit.BloodMage, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Sanguine Rain", Cooldown = 80f, Desc = "For 6 s, every hit any ally lands heals them for 20% of its damage.",
                Capstone = "Lasts 9 s and heals 30%.",
                Bot = b => b.AllyHurt && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.CrimsonApotheosis, Kit = Kit.BloodMage, Role = Role.Dps, Choice = AbilityDef.Ult,
                Name = "Crimson Apotheosis", Cooldown = 65f, Desc = "For 6 s you can't drop below 1 health and Blood Price can double your damage. When it ends, heal for half the damage you dealt.",
                Capstone = "Lasts 9 s.",
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
                    p.ShieldHp = Math.Max(p.ShieldHp, castPaid * (ShellMult + s.ShellBonus) * pw);
                    p.ShieldT = Math.Max(p.ShieldT, ShellTime + s.ShellBonusTime);
                    if (cap)
                        EraseEnemyBullets(p.Pos, 160f);
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 60f, 0.4f, Sfx.Ability);
                    return true;
                case AbilityId.CrimsonPact:
                    p.PactT = PactTime;
                    p.PactStore = 0f;
                    p.PactPower = pw * s.PactMult;
                    p.PactRadius = PactRadius * s.PactRadiusMult;
                    p.PactMastered = cap;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = PactTime;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 260f, 0.5f, Sfx.Roar);
                    return true;
                case AbilityId.Exsanguinate:
                {
                    float r = DrainRadius * s.DrainRadiusMult, drain = DrainAmount * s.DrainMult;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > r + e.Radius)
                            continue;
                        if (!Heavy(e))
                        {
                            V2 away = (e.Pos - p.Pos).Normalized();
                            float d = Math.Max(e.Pos.DistanceTo(p.Pos) - DrainPull, e.Radius + p.Def.BodyR + 4f);
                            e.Pos = ClampToArena(p.Pos + (away.IsZero ? V2.FromAngle(p.Aim) : away) * d, e.Radius);
                        }
                        HurtEnemy(e, drain * s.DmgMult, p.Id);
                        Heal(p, drain, p);
                        if (cap && !e.Def.IsBoss)
                        {
                            e.SlowT = Math.Max(e.SlowT, 2f);
                            e.SlowMult = Math.Min(e.SlowMult, 0.5f);
                        }
                        BroadcastFx(FxKind.Slash, e.Pos, p.Pos, blood, 0.1f, 0.3f, Sfx.None);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, r, 0.4f, Sfx.Shockwave);
                    return true;
                }
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
                        BroadcastFx(FxKind.Ring, p.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                        return true;
                    }
                    Heal(ally, gift, p);
                    if (cap)
                        foreach (var q in Players.Values)
                            if (q != ally && !q.Downed && q.Pos.DistanceTo(ally.Pos) < GiftSplash)
                                Heal(q, gift * 0.5f, p);
                    BroadcastFx(FxKind.Slash, p.Pos, ally.Pos, blood, 0.12f, 0.35f, Sfx.None);
                    BroadcastFx(FxKind.Ring, ally.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.SiphonField:
                {
                    var z = AddZone(ZoneKind.Siphon, p, p.Pos, SiphonRadius * s.SiphonRadiusMult, SiphonTime + s.SiphonBonusTime);
                    z.Follow = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.45f, Sfx.Ability);
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
                        BroadcastFx(FxKind.Slash, p.Pos, down.Pos, blood, 0.14f, 0.4f, Sfx.None);
                        BroadcastFx(FxKind.Ring, down.Pos, blood, 70f, 0.5f, Sfx.Revive);
                    }
                    else if (hurt != null)
                    {
                        Heal(hurt, castPaid * GiftMult, p);
                        BroadcastFx(FxKind.Ring, hurt.Pos, blood, 50f, 0.4f, Sfx.SpellHoly);
                    }
                    if (cap)
                        p.InvulnT = Math.Max(p.InvulnT, 2f);
                    return true;
                }
                case AbilityId.BloodBoil:
                {
                    float r = BoilRadius * s.BoilRadiusMult, dmg = castPaid * BoilMult * s.BoilMult * s.DmgMult * pw;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > r + e.Radius)
                            continue;
                        boils.Add(new Boil { Enemy = e.Id, Owner = p.Id, T = BoilDelay, Dmg = dmg, Splash = cap });
                        castHits++;
                        BroadcastFx(FxKind.Ring, e.Pos, blood, e.Radius + 14f, BoilDelay, Sfx.None);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, r, 0.45f, Sfx.Ability);
                    return true;
                }
                case AbilityId.FrenzyPact:
                    p.RushT = RushTime + s.RushBonusTime;
                    p.RushMastered = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 60f, 0.4f, Sfx.Roar);
                    return true;
                case AbilityId.HemoNova:
                {
                    int n = 16 + s.NovaExtraBolts;
                    for (int i = 0; i < n; i++)
                    {
                        V2 d = V2.FromAngle(p.Aim + MathX.Tau * i / n);
                        var b = SpawnBullet(Defs.BloodShot, p.Pos + d * p.Def.BodyR, d * 600f, 0f, 0.7f, NovaDamage * s.DmgMult, p.Id);
                        b.HealOwner = NovaHeal;
                        if (cap)
                            b.Pierce += 2;
                    }
                    Sounds.Add(Sfx.SpellArcane);
                    return true;
                }

                // Ultimates
                case AbilityId.BloodRite:
                {
                    float time = cap ? 12f : 8f;
                    p.UltT = time;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = time;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 240f, 0.6f, Sfx.Roar);
                    return true;
                }
                case AbilityId.SanguineRain:
                    p.UltT = cap ? 9f : 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 200f, 0.6f, Sfx.SpellHoly);
                    return true;
                case AbilityId.CrimsonApotheosis:
                    p.UltT = cap ? 9f : 6f;
                    p.ApoDealt = 0f;
                    p.ApoUp = true;
                    BroadcastFx(FxKind.Ring, p.Pos, blood, 120f, 0.5f, Sfx.Roar);
                    return true;
            }
            return false;
        }

        // --- Timed effects ---------------------------------------------------------------

        /// Host, every tick: Crimson Pact runs down and bursts, Frenzy Pact drains, Crimson Apotheosis pays out
        /// when it ends, Blood Boils go off, and Sanguine Rain's strength is noted for HurtEnemy.
        void TickBloodMages(float dt)
        {
            rainSteal = 0f;
            foreach (var p in Players.Values)
            {
                if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.SanguineRain)
                    rainSteal = Math.Max(rainSteal, Mastered(p, AbilityId.SanguineRain) ? 0.3f : RainSteal);
                if (p.Grasp.Count > 0 && (p.Downed || !p.Firing || p.ParryActive || p.Weapon.Id != WeaponId.LeechBeam))
                    p.Grasp.Clear();  // let go of fire and the tendrils let go too
                if (p.Downed)
                {
                    p.PactT = p.RushT = 0f;
                    p.ApoUp = false;
                    p.Clot = 0;
                    continue;
                }
                if (p.Clot > 0 && (p.ClotT -= dt) <= 0f)  // Clot holds while you keep hitting, then fades a stack at a time
                {
                    p.Clot--;
                    p.ClotT = ClotFade;
                }
                if (p.PactT > 0f)
                {
                    p.PactT -= dt;
                    if (p.PactT <= 0f)
                    {
                        p.PactT = 0f;
                        float dmg = p.PactStore * PactBurst * p.PactPower * p.Stats.DmgMult;
                        if (dmg > 0f)
                            foreach (var e in Enemies.Values)
                                if (e.SpawnT <= 0f && e.Pos.DistanceTo(p.Pos) < p.PactRadius + e.Radius)
                                    HurtEnemy(e, dmg, p.Id);
                        BroadcastFx(FxKind.Boom, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), p.PactRadius, 0.4f, Sfx.Explode);
                    }
                }
                if (p.RushT > 0f)
                {
                    p.RushT = Math.Max(0f, p.RushT - dt);
                    if (!p.RushMastered)
                        PayHealth(p, RushDrain * dt);
                }
                if (p.ApoUp && !Exalted(p))
                {
                    p.ApoUp = false;
                    Heal(p, p.ApoDealt * 0.5f, p);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.12f, 0.2f), 70f, 0.4f, Sfx.SpellHoly);
                }
            }
            for (int i = boils.Count - 1; i >= 0; i--)
            {
                var b = boils[i];
                b.T -= dt;
                if (b.T > 0f)
                    continue;
                boils.RemoveAt(i);
                Enemy e;
                if (!Enemies.TryGetValue(b.Enemy, out e) || e.Hp <= 0f)
                    continue;
                HurtEnemy(e, b.Dmg, b.Owner);
                if (b.Splash)
                    foreach (var o in Enemies.Values)
                        if (o != e && o.SpawnT <= 0f && o.Pos.DistanceTo(e.Pos) < BoilSplash + o.Radius)
                            HurtEnemy(o, b.Dmg, b.Owner);
                BroadcastFx(FxKind.Boom, e.Pos, new Rgba(0.85f, 0.12f, 0.2f), b.Splash ? BoilSplash : e.Radius + 20f, 0.3f, Sfx.Explode);
            }
        }
    }
}

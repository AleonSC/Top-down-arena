using System;
using System.Collections.Generic;

namespace Arena
{
    /// One melee strike, waiting to land (host only). Swings are resolved inside Collide so a hit goes
    /// through the same on-hit pipeline as a bullet: weapon mods, legendaries, crits, lifesteal, kills.
    public class Swing
    {
        public int Owner;
        public float Aim, Reach, HalfArc, Dmg, Knock, Delay;
        public int MaxTargets;        // 0 = everything in the arc; otherwise the nearest few (Rapier)
        public bool CutAll;           // also cuts big orbs and mines (mastered Whirlwind)
        public float HealPerHit, HealRange;
        public int HealHits;          // most enemies a swing's healing counts (Holy Mace)
        public float HealSpin;        // heals allies near you once per swing (Censer)
        public bool Rapier, Heavy, Silent;
    }

    /// The Knight: a melee class for every role. Its weapons swing instead of shooting, cutting small
    /// bullets out of the air; its abilities close the distance and keep it standing.
    public partial class Game
    {
        readonly List<Swing> pendingSwings = new List<Swing>();

        const float LeapRange = 320f, LeapTime = 0.45f, SlamRadius = 130f, SlamDamage = 50f;
        const float LungeRange = 260f, LungeSpeed = 1000f, LungeDamage = 35f;
        const float WallCapacity = 400f, WallTime = 4f, WallHalfArc = 1.05f;  // 120 degrees across
        const float WhirlRadius = 110f, WhirlDamage = 12f, WhirlEvery = 0.2f;

        // --- Weapons ------------------------------------------------------------------

        /// How far a melee weapon reaches (Piercing Rounds lengthen it 10% a stack).
        public static float MeleeReach(Player p, float baseReach) => baseReach * (1f + 0.1f * p.Stats.Pierce);

        /// The range a weapon wants to be fought from (bots use it; ranged weapons return 0).
        public static float MeleeRange(WeaponId w)
        {
            switch (w)
            {
                case WeaponId.Warhammer: return 100f;
                case WeaponId.Lance: return 150f;
                case WeaponId.Halberd: return 160f;
                case WeaponId.HolyMace: return 85f;
                case WeaponId.Lightbringer: return 80f;
                case WeaponId.Censer: return 80f;
                case WeaponId.TwinBlades: return 75f;
                case WeaponId.Greatsword: return 125f;
                case WeaponId.Rapier: return 130f;
                default: return 0f;
            }
        }

        Swing NewSwing(Player p, float aim, float reach, float arcDeg, float dmg)
        {
            return new Swing { Owner = p.Id, Aim = aim, Reach = MeleeReach(p, reach), HalfArc = arcDeg * MathX.Pi / 360f, Dmg = dmg };
        }

        /// A Knight weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireKnightWeapon(Player p, float aim, float dmg, int extra)
        {
            p.SwingCount++;
            switch (p.Weapon.Id)
            {
                case WeaponId.Warhammer:
                {
                    var s = NewSwing(p, aim, 100f, 150f, 50f * dmg);
                    s.Knock = 35f;
                    s.Heavy = true;
                    pendingSwings.Add(s);
                    for (int k = 1; k <= extra; k++)  // aftershocks
                    {
                        var a = NewSwing(p, aim, 100f + 30f * k, 150f, 30f * dmg);
                        a.Delay = 0.2f * k;
                        a.Knock = 20f;
                        a.Heavy = true;
                        pendingSwings.Add(a);
                    }
                    break;
                }
                case WeaponId.Lance:
                {
                    // A big jab straight ahead that drives everything it hits far back (not bosses or heavies).
                    var s = NewSwing(p, aim, 150f + 30f * extra, 60f, 36f * dmg);
                    s.Knock = 140f + 60f * extra;
                    s.Heavy = true;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.Halberd:
                {
                    float reach = 160f + 30f * extra;
                    pendingSwings.Add(NewSwing(p, aim, reach, 40f, 32f * dmg));
                    if (extra > 0)
                    {
                        var f = NewSwing(p, aim, reach, 40f, 19f * dmg);
                        f.Delay = 0.12f;
                        pendingSwings.Add(f);
                    }
                    break;
                }
                case WeaponId.HolyMace:
                {
                    var s = NewSwing(p, aim, 85f, 120f, 22f * dmg);
                    s.HealPerHit = 2f + extra;
                    s.HealRange = 220f + 60f * extra;
                    s.HealHits = 4;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.Lightbringer:
                {
                    pendingSwings.Add(NewSwing(p, aim, 80f, 100f, 17f * dmg));
                    int n = 1 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float f = n == 1 ? 0f : i * 2f / (n - 1) - 1f;
                        var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.2f * (n - 1)) * 600f, 0f, 0.45f, 12f * dmg, p.Id);
                        b.Pierce += 2;
                        b.HealAlly = 6f;
                    }
                    break;
                }
                case WeaponId.Censer:
                {
                    var s = NewSwing(p, aim, 80f + 20f * extra, 360f, 14f * dmg);
                    s.HealSpin = 3f + extra;
                    s.HealRange = 140f;
                    pendingSwings.Add(s);
                    break;
                }
                case WeaponId.TwinBlades:
                {
                    float side = p.SwingCount % 2 == 0 ? 0.2f : -0.2f;
                    pendingSwings.Add(NewSwing(p, aim + side, 75f, 90f, 15f * dmg));
                    for (int k = 1; k <= extra; k++)
                    {
                        var c = NewSwing(p, aim - side * (k % 2 == 1 ? 1f : -1f), 75f, 90f, 9f * dmg);
                        c.Delay = 0.05f * k;
                        c.Silent = true;
                        pendingSwings.Add(c);
                    }
                    break;
                }
                case WeaponId.Greatsword:
                {
                    var s = NewSwing(p, aim, 125f, 170f, 60f * dmg);
                    s.Heavy = true;
                    pendingSwings.Add(s);
                    for (int i = 0; i < extra; i++)
                    {
                        float f = extra == 1 ? 0f : i * 2f / (extra - 1) - 1f;
                        var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.25f) * 700f, 0f, 0.6f, 24f * dmg, p.Id);
                        b.Pierce += 99;
                    }
                    break;
                }
                case WeaponId.Rapier:
                {
                    var s = NewSwing(p, aim, 130f, 36f, 18f * dmg);
                    s.Rapier = true;
                    s.MaxTargets = 1 + extra;
                    pendingSwings.Add(s);
                    break;
                }
            }

            // Blade Tempest: every swing also hurls piercing sword beams.
            if (p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.BladeTempest)
            {
                int n = Mastered(p, AbilityId.BladeTempest) ? 5 : 3;
                for (int i = 0; i < n; i++)
                {
                    float f = i * 2f / (n - 1) - 1f;
                    var b = SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(aim) * p.Def.BodyR, V2.FromAngle(aim + f * 0.22f) * 750f, 0f, 0.7f, 20f * dmg, p.Id);
                    b.Pierce += 99;
                }
            }
        }

        /// Barrage on a Knight weapon: a sword wave per stack, fanned out to the sides.
        void FireKnightWaves(Player p, float aim)
        {
            for (int v = 1; v <= p.Stats.Fan; v++)
            {
                float a = aim + (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2);
                SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(a) * p.Def.BodyR, V2.FromAngle(a) * 650f, 0f, 0.5f, 9f * p.Stats.DmgMult, p.Id);
            }
        }

        /// Called from Collide (after it clears its queues): land every swing that's due.
        void ResolveSwings()
        {
            for (int i = pendingSwings.Count - 1; i >= 0; i--)
            {
                var s = pendingSwings[i];
                s.Delay -= tickDt;
                if (s.Delay > 0f)
                    continue;
                pendingSwings.RemoveAt(i);
                Player p;
                if (Players.TryGetValue(s.Owner, out p) && !p.Downed)
                    LandSwing(p, s);
            }
        }

        static float AngleTo(V2 from, V2 to, float aim) => Math.Abs(MathX.Fmod((to - from).Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);

        void LandSwing(Player p, Swing s)
        {
            V2 origin = p.Pos, dir = V2.FromAngle(s.Aim);
            bool full = s.HalfArc >= MathX.Pi - 0.01f;
            BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.KnightSlash), s.HalfArc, s.Heavy ? 0.24f : 0.16f,
                s.Silent ? Sfx.None : s.Heavy ? Sfx.SwingHeavy : Sfx.SwingLight);

            // Who's in the arc (the Rapier only takes the nearest few).
            var hits = new List<Enemy>();
            foreach (var e in Enemies.Values)
            {
                if (e.SpawnT > 0f || e.Hp <= 0f)
                    continue;
                float d = e.Pos.DistanceTo(origin);
                if (d - e.Radius > s.Reach)
                    continue;
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi;
                if (!full && AngleTo(origin, e.Pos, s.Aim) > s.HalfArc + slack && d > e.Radius + p.Def.BodyR)
                    continue;
                if (BlocksShot(e, origin))
                    continue;  // shields stop swords too
                hits.Add(e);
            }
            if (s.MaxTargets > 0 && hits.Count > s.MaxTargets)
            {
                hits.Sort((a, b) => a.Pos.DistanceSq(origin).CompareTo(b.Pos.DistanceSq(origin)));
                hits.RemoveRange(s.MaxTargets, hits.Count - s.MaxTargets);
            }

            p.Run.ShotsFired++;
            if (hits.Count > 0)
                p.Run.ShotsHit++;
            int healed = 0;
            foreach (var e in hits)
            {
                float dmg = s.Dmg;
                if (s.Rapier)
                {
                    // Each hit in a row on the same enemy deals 10% more (up to +60%).
                    p.RapierStreak = e.Id == p.RapierTarget ? Math.Min(6, p.RapierStreak + 1) : 0;
                    p.RapierTarget = e.Id;
                    dmg *= 1f + 0.1f * p.RapierStreak;
                }
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                if (s.Knock > 0f && !Heavy(e))
                {
                    V2 away = (e.Pos - origin).Normalized();
                    e.Pos = ClampToArena(e.Pos + (away.IsZero ? dir : away) * s.Knock, e.Radius);
                }
                if (s.HealPerHit > 0f && healed < s.HealHits)
                {
                    healed++;
                    foreach (var q in Players.Values)
                        if (q.Pos.DistanceTo(origin) < s.HealRange)
                            Heal(q, s.HealPerHit, p);
                }
            }
            if (s.HealSpin > 0f)
                foreach (var q in Players.Values)
                    if (q.Pos.DistanceTo(origin) < s.HealRange)
                        Heal(q, s.HealSpin, p);

            CutBullets(origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, full ? MathX.Pi : s.HalfArc, s.CutAll);
        }

        /// Swings slice small enemy bullets out of the air; a mastered Whirlwind (CutAll) stops big orbs too.
        /// Bosses' red aimed shots always get through.
        int CutBullets(V2 origin, float aim, float reach, float halfArc, bool all)
        {
            var cut = new List<int>();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (!all && b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                    continue;
                if (b.Pos.DistanceTo(origin) > reach + b.Radius)
                    continue;
                if (halfArc < MathX.Pi && AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
            return cut.Count;
        }

        // --- Abilities ------------------------------------------------------------------

        /// Knight abilities and ultimates. Returns false if `id` isn't one.
        bool KnightAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.ShieldWall:
                    p.WallT = WallTime + s.WallBonusTime;
                    p.WallHp = WallCapacity * s.WallCapMult * pw;
                    p.WallReflect = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.75f, 0.85f, 1f), 50f, 0.35f, Sfx.Clang);
                    return true;
                case AbilityId.LeapSlam:
                    // The player's own game moves them (LocalControl); the slam lands where they come down.
                    p.InvulnT = Math.Max(p.InvulnT, LeapTime + 0.1f);
                    slams.Add(new Slam { Owner = p.Id, Pos = target, T = LeapTime, Power = pw, Mastered = cap,
                        Radius = SlamRadius * s.SlamRadiusMult, Stun = 1f + s.SlamStunBonus });
                    return true;
                case AbilityId.Unbreakable:
                    p.UnbreakT = cap ? 8f : 5f;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.85f, 0.75f, 0.5f), 60f, 0.4f, Sfx.Clang);
                    return true;
                case AbilityId.Consecrate:
                {
                    var z = AddZone(ZoneKind.Consecrate, p, p.Pos, 130f * s.ConsecrateRadiusMult, 6f + s.ConsecrateBonusTime);
                    z.Follow = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.GuardiansOath:
                {
                    float shield = 100f * s.OathMult * pw;
                    Player best = null;
                    foreach (var q in Players.Values)
                        if (q.Id != p.Id && !q.Downed && (best == null || q.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                            best = q;
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed)
                            continue;
                        float amount = q == best || cap && q.Pos.DistanceTo(p.Pos) < 300f ? shield : q == p ? shield * 0.5f : 0f;
                        if (best == null && q == p)
                            amount = shield;  // alone: the full shield is yours
                        if (amount <= 0f)
                            continue;
                        q.ShieldHp = Math.Max(q.ShieldHp, amount);
                        q.ShieldT = 6f;
                        castHits++;
                        BroadcastFx(FxKind.Ring, q.Pos, new Rgba(1f, 0.9f, 0.5f), 45f, 0.4f, Sfx.None);
                    }
                    Sounds.Add(Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.LayOnHands:
                {
                    float range = 300f * (1f + 0.5f * (s.LayMult - 1f));
                    Player best = null;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.DistanceTo(p.Pos) > range)
                            continue;
                        if (best == null || q.Downed && !best.Downed || q.Downed == best.Downed && q.Hp / q.MaxHp < best.Hp / best.MaxHp)
                            best = q;
                    }
                    if (best != null)
                    {
                        if (best.Downed)
                            RevivePlayer(best, 0.5f, p);
                        else
                            Heal(best, 70f * s.LayMult, p);
                        castHits++;
                        BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.95f, 0.6f), 70f, 0.5f, Sfx.SpellHoly);
                        if (cap)
                            foreach (var q in Players.Values)
                                if (q != best && !q.Downed && q.Pos.DistanceTo(best.Pos) < 200f)
                                    Heal(q, 35f * s.LayMult, p);
                    }
                    return true;
                }
                case AbilityId.Whirlwind:
                    p.WhirlT = (cap ? 3f : 2f) + s.WhirlBonusTime;
                    p.WhirlTick = 0f;
                    p.WhirlPower = pw;
                    p.WhirlAll = cap;
                    return true;
                case AbilityId.Lunge:
                {
                    // Like Bulldoze: the player's own game moves them; everything near the path takes a hit.
                    p.InvulnT = Math.Max(p.InvulnT, LungeRange * s.LungeRangeMult / LungeSpeed + 0.1f);
                    V2 from = p.Pos, path = target - from;
                    float len = Math.Max(1f, path.Length);
                    V2 dir = path / len;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                        if (e.Pos.DistanceTo(from + dir * along) > e.Radius + 34f)
                            continue;
                        float dmg = LungeDamage * s.DmgMult * s.LungeDmgMult;
                        HurtEnemy(e, dmg, p.Id);
                        OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                    }
                    BroadcastFx(FxKind.Slash, from, target, Defs.BulletColor(Defs.KnightSlash), 0.12f, 0.2f, Sfx.SwingLight);
                    return true;
                }
                case AbilityId.BattleFury:
                    p.FuryT = 6f * pw;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.35f, 0.25f), 60f, 0.4f, Sfx.Roar);
                    return true;

                // Ultimates
                case AbilityId.LastStand:
                {
                    float time = cap ? 12f : 8f;
                    p.UltT = time;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = time;
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 160f, 0.6f, Sfx.Roar);
                    return true;
                }
                case AbilityId.HallowedBlade:
                {
                    var spots = new List<V2> { target };
                    if (cap)
                    {
                        V2 side = (target - p.Pos).Normalized().Orthogonal();
                        if (side.IsZero)
                            side = new V2(1f, 0f);
                        spots.Add(ClampToArena(target + side * 220f, 10f));
                        spots.Add(ClampToArena(target - side * 220f, 10f));
                    }
                    foreach (var at in spots)
                        AddZone(ZoneKind.HolySword, p, at, 200f, 0.6f).Consecrates = true;
                    Sounds.Add(Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.BladeTempest:
                    p.UltT = 6f;
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.KnightWave), 80f, 0.4f, Sfx.SwingHeavy);
                    return true;
            }
            return false;
        }

        bool LastStanding(Player p) => p.UltT > 0f && p.Slots[Upgrades.UltSlot].Id == AbilityId.LastStand;

        /// Damage multiplier from Knight buffs on whoever dealt the hit (applied in HurtEnemy).
        float KnightDamageMult(Player p) => (p.FuryT > 0f ? 1.25f : 1f) * (LastStanding(p) ? 1.5f : 1f);

        /// Unbreakable and Battle Fury (with Bloodlust) heal you for part of what you deal.
        void KnightLifesteal(Player p, float dealt)
        {
            float frac = (p.UnbreakT > 0f ? 0.15f + p.Stats.UnbreakHealBonus : 0f) + (p.FuryT > 0f ? p.Stats.FuryLifesteal : 0f);
            if (frac > 0f && !p.Downed && dealt > 0f)
                Heal(p, dealt * frac, p);
        }

        // --- Timed effects ---------------------------------------------------------------

        class Slam
        {
            public int Owner;
            public V2 Pos;
            public float T, Power, Radius, Stun;
            public bool Mastered;
        }

        readonly List<Slam> slams = new List<Slam>();

        /// Host, every tick: Knight buffs run down, Whirlwind spins, Leap Slams land.
        void TickKnights(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.WallT = Math.Max(0f, p.WallT - dt);
                p.UnbreakT = Math.Max(0f, p.UnbreakT - dt);
                p.FuryT = Math.Max(0f, p.FuryT - dt);
                if (p.Downed)
                    p.WallT = p.WhirlT = p.UnbreakT = p.FuryT = 0f;
                if (p.WhirlT > 0f)
                {
                    p.WhirlT -= dt;
                    p.WhirlTick -= dt;
                    if (p.WhirlTick <= 0f)
                    {
                        p.WhirlTick += WhirlEvery;
                        var s = NewSwing(p, p.Aim, WhirlRadius * p.Stats.WhirlRadiusMult, 360f, WhirlDamage * p.Stats.DmgMult * p.WhirlPower);
                        s.CutAll = p.WhirlAll;
                        s.Silent = MathX.Fmod(p.WhirlT, 0.6f) > 0.2f;
                        pendingSwings.Add(s);
                    }
                }
            }
            for (int i = slams.Count - 1; i >= 0; i--)
            {
                var sl = slams[i];
                sl.T -= dt;
                if (sl.T > 0f)
                    continue;
                slams.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(sl.Owner, out p))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Pos.DistanceTo(sl.Pos) > sl.Radius + e.Radius)
                        continue;
                    HurtEnemy(e, SlamDamage * p.Stats.DmgMult * sl.Power, p.Id);
                    if (!e.Def.IsBoss)
                    {
                        e.FrozenT = Math.Max(e.FrozenT, sl.Stun);
                        e.SlowT = Math.Max(e.SlowT, sl.Stun);
                    }
                }
                if (sl.Mastered)
                    EraseEnemyBullets(sl.Pos, 160f);
                BroadcastFx(FxKind.Boom, sl.Pos, new Rgba(0.85f, 0.8f, 0.65f), sl.Radius, 0.4f, Sfx.Explode);
            }
        }

        /// Does a raised Shield Wall cover this point (in front of the knight)?
        bool WallCovers(Player p, V2 pos, float radius)
        {
            V2 d = pos - p.Pos;
            float reach = p.Def.BodyR + 34f + radius;
            if (d.LengthSq > reach * reach)
                return false;
            return AngleTo(p.Pos, pos, p.Aim) < WallHalfArc * p.Stats.WallArcMult;
        }
    }
}

using System;
using System.Collections.Generic;

namespace Arena
{
    /// Mage and summoner kits: their weapons, abilities, minions and ground zones (host only).
    public partial class Game
    {
        const float MinionChaseRange = 600f;

        // --- Weapons -----------------------------------------------------------

        /// Mage and summoner weapons. `extra` is the number of extra projectiles (see FireVolley).
        void FireKitWeapon(Player p, float aim, V2 dir, V2 muzzle, float dmg, int extra)
        {
            switch (p.Weapon.Id)
            {
                // Mage
                case WeaponId.FlameBurst:
                {
                    // +2 flames and 25% more reach per extra projectile.
                    float reach = 0.35f * (1f + 0.25f * extra);
                    for (int i = 0; i < 6 + 2 * extra; i++)
                        SpawnBullet(Defs.FireShot, muzzle, V2.FromAngle(aim + Rand(-0.35f, 0.35f)) * Rand(380f, 480f), 0f, reach, 6f * dmg, p.Id);
                    break;
                }
                case WeaponId.FrostLance:
                {
                    var b = SpawnBullet(Defs.FrostShot, muzzle, dir * 700f, 0f, 0.9f, 14f * dmg, p.Id);
                    b.Pierce += 1;
                    b.SlowAmt = 0.3f;
                    if (extra > 0)  // shatters into icicles that fly out to the sides
                        SetFrags(b, 2 * extra, Defs.FrostShot, 5f * dmg, 500f, 0.3f, 0f, true);
                    break;
                }
                case WeaponId.StormHammer:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * 420f, 0f, 1.2f, 24f * dmg, p.Id).Chain = 3 + extra;
                    break;
                case WeaponId.RadiantOrb:
                {
                    var b = SpawnBullet(Defs.HolyShot, muzzle, dir * 300f, 0f, 1.8f, 14f * dmg, p.Id);
                    b.Pierce += 2;
                    b.HealAlly = 10f;
                    break;  // extra projectiles become Light Orbs circling you (TickMinions)
                }
                case WeaponId.ChainLight:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * 800f, 0f, 0.8f, 10f * dmg, p.Id).Chain = 2 + extra;
                    break;
                case WeaponId.PrismBeam:
                {
                    // +30% length, +25% damage and +50% healing per extra projectile.
                    var b = SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + Rand(-0.03f, 0.03f)) * 1200f, 0f, 0.35f * (1f + 0.3f * extra),
                        4f * (1f + 0.25f * extra) * dmg, p.Id);
                    b.HealAlly = 2f * (1f + 0.5f * extra);
                    break;
                }
                case WeaponId.Fireball:
                {
                    var b = SpawnBullet(Defs.FireShot, muzzle, dir * 520f, 0f, 1.2f, 22f * dmg, p.Id);
                    b.Splash = 60f;
                    if (extra > 0)  // splits into small fireballs on impact
                        SetFrags(b, 2 * extra, Defs.FireShot, 8f * dmg, 380f, 0.35f, 30f, false);
                    break;
                }
                case WeaponId.ArcLightning:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * 900f, 0f, 0.6f, 12f * dmg, p.Id).Chain = 4 + extra;
                    break;
                case WeaponId.ArcaneMissiles:
                {
                    // Fanned out and curving back in; with extra projectiles, more of them, and they home instead.
                    int n = 3 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float f = i * 2f / (n - 1) - 1f;
                        SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + f * 0.4f) * 520f, extra > 0 ? 0f : -f * 1.2f, 1f, 7f * dmg, p.Id, false, extra > 0 ? 4f : 0f);
                    }
                    break;
                }
                // Summoner (extra projectiles mostly add companions: see CompanionCount)
                case WeaponId.GolemStaff:
                    for (int i = -1; i <= 1; i++)
                        SpawnBullet(Defs.RockShot, muzzle, V2.FromAngle(aim + i * 0.12f) * 480f, 0f, 0.6f, 8f * dmg, p.Id);
                    break;
                case WeaponId.BoneGuard:
                case WeaponId.WolfWhistle:
                case WeaponId.ImpCaller:
                    SpawnBullet(Defs.BoneShot, muzzle, dir * 720f, 0f, 0.8f, (p.Weapon.Id == WeaponId.BoneGuard ? 7f : 6.5f) * dmg, p.Id);
                    break;
                case WeaponId.BearTotem:
                {
                    // +2 claws and 15% wider swipes per extra projectile.
                    int n = 5 + 2 * extra;
                    float width = 1f + 0.15f * extra;
                    for (int i = 0; i < n; i++)
                        SpawnBullet(Defs.BoneShot, muzzle, V2.FromAngle(aim + (i / (n - 1f) - 0.5f) * width) * 500f, 0f, 0.22f, 9f * dmg, p.Id);
                    break;
                }
                case WeaponId.SpriteStaff:
                case WeaponId.TotemStaff:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * 600f, 0f, 1f, 9f * dmg, p.Id);
                    break;
                case WeaponId.TreantRod:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * 600f, 0f, 1f, 8f * dmg, p.Id).HealAlly = 7f;
                    break;
                case WeaponId.WispLantern:
                    SpawnBullet(Defs.ArcaneShot, muzzle, dir * 600f, 0f, 1f, 8f * dmg, p.Id);
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
        void ChainLightning(Enemy from, float dmg, int jumps, int owner)
        {
            var hit = new HashSet<int> { from.Id };
            Enemy cur = from;
            for (int j = 0; j < jumps; j++)
            {
                Enemy next = null;
                foreach (var e in Enemies.Values)
                    if (!hit.Contains(e.Id) && e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(cur.Pos) < 170f &&
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

        // --- Abilities ---------------------------------------------------------

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
                            q.ShieldHp = (cap ? 120f : 60f) * pw;
                            q.ShieldT = 8f;
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
                    if (!KnightAbility(p, id, target) && !NinjaAbility(p, id, target))  // Knight.cs, Ninja.cs
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
                var w = p.Weapon;
                var kind = CompanionKind(p);
                int extra = ExtraShots(p);
                if (kind != MinionKind.None)
                {
                    int want = CompanionCount(p), have = 0;
                    Minion surplus = null;
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id && m.Companion)
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
                    if (p.TotemCd <= 0f)
                    {
                        p.TotemCd = 10f / (1f + 0.6f * extra);
                        AddZone(ZoneKind.Totem, p, p.Pos, 90f, 8f);
                    }
                }
            }

            var expired = new List<Minion>();
            var exploding = new List<Minion>();
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
                if (m.StaggerT > 0f)
                {
                    m.StaggerT -= dt;
                    if (m.StaggerT <= 0f)
                        m.BlockHp = d.BlockCapacity * m.Scale;  // back on its feet
                }
                bool frenzy = owner.FrenzyT > 0f;
                float speed = d.Speed * (frenzy ? 1.6f : 1f);
                float rate = owner.Stats.FireRateMult * (frenzy ? 2f : 1f);
                float dmg = d.Damage * owner.Stats.DmgMult * (frenzy ? 1.3f : 1f) * m.Scale * m.Power;
                float reach = d.Range * m.Scale;
                m.Cd -= dt * rate;

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
                        target = NearestEnemy(m.Pos, MinionChaseRange);
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
                    case MinionMove.FollowOwner:
                    {
                        // The treant stands in front of you; the guardian spirit hovers on you.
                        V2 goal = m.Kind == MinionKind.Treant ? owner.Pos + V2.FromAngle(owner.Aim) * 45f : owner.Pos;
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
                            byte kind = m.Kind == MinionKind.Turret ? Defs.TurretShot : m.Kind == MinionKind.Phoenix ? Defs.FireShot : Defs.MinionShot;
                            SpawnBullet(kind, m.Pos + dir * d.Radius, dir * 650f, 0f, d.Range / 650f, dmg, m.Owner);
                        }
                        break;
                    }
                    case MinionAttack.Bite:
                    {
                        var e = target ?? NearestEnemy(m.Pos, d.Radius + 40f);
                        if (e != null && m.Cd <= 0f && e.Pos.DistanceTo(m.Pos) < e.Radius + d.Radius + 6f)
                        {
                            m.Cd = d.Interval;
                            m.Aim = (e.Pos - m.Pos).Angle;
                            HurtEnemy(e, dmg, m.Owner);
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
                    ExplodeMinion(m, m.Def.Damage * OwnerDmg(m) * m.Power, m.Def.Range);
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

        void TickZones(float dt)
        {
            var expired = new List<Zone>();
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
                        Heal(q, d.HealRate * z.HealMult * dt, PlayerOrNull(z.Owner));
                }
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

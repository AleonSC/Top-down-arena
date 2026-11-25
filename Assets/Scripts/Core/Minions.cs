using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Elementalist's and Summoner's ability casts (KitAbility), minions and ground zones (host only). Their class
    /// definitions are in Elementalist.cs and Summoner.cs; both classes' weapons fire in KitWeapons.cs.
    public partial class Game
    {
        // --- Abilities ---------------------------------------------------------

        public const float FrostNovaFxTime = 3.3f;
        // Alpha Call's Blood Scent: stacks per minion, and what each one adds to its speed and attack rate (2 stacks is about the old flat frenzy)
        public const int ScentStart = 2, ScentMax = 5;
        public const float ScentSpeedPer = 0.16f, ScentRatePer = 0.5f, ScentLunge = 3f;

        /// Where Blood Scent's stacks start for this owner (Pack Leader adds to it), below a lunge.
        static int ScentStartFor(Player owner) => Math.Min(ScentMax - 1, ScentStart + owner.Stats.ScentStartBonus);

        void KitAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            float dmg = s.DmgMult;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                // Elementalist
                case AbilityId.Aeroaegis:  // Aeroaegis (Aegis.cs): the vortex goes to whoever your cursor is nearest
                {
                    var ward = AegisTarget(p, target);
                    ward.AegisT = (cap ? 4.5f : 3f) + s.AegisBonusTime;  // Eye of the Storm
                    ward.AegisHp = AegisCapacity * pw;
                    ward.AegisReflectMult = pw * (cap ? 2f : 1f) * (1f + s.AegisReflectBonus);
                    KeepOutOfAegis(0f);  // enemies already inside are put out at the rim
                    BroadcastFx(FxKind.Ring, ward.Pos, new Rgba(0.84f, 1f, 0.97f), AegisRadius + 70f, 0.5f, Sfx.Ability);
                    break;
                }
                case AbilityId.FrostNova:
                {
                    float reach = 220f * s.NovaRangeMult;  // Permafrost: farther, and a longer freeze
                    foreach (var e in FoesInCircle(p.Pos, reach, by: p.Id))
                    {
                        HurtEnemy(e, 40f * dmg, p.Id);
                        Chill(e, 1, 3f + s.NovaFreezeBonus, 0.1f);  // nearly still, and chilled (Chill.cs)
                    }
                    if (cap)
                        EraseEnemyBullets(p.Pos, reach);
                    BroadcastFx(FxKind.Ring, p.Pos, Defs.BulletColor(Defs.FrostShot), reach, FrostNovaFxTime + s.NovaFreezeBonus, Sfx.Freeze);  // lasts as long as the freeze: the frost stays on the ground (Game/FrostNovaLook.cs)
                    break;
                }
                case AbilityId.FlameWall:
                {
                    var wall = AddZone(ZoneKind.FlameWall, p, target, 90f, 5f + s.FlameWallBonusTime);  // Inferno: longer, hotter
                    wall.ExtraSlow = cap ? 0.4f : 0f;
                    wall.DmgMult *= s.FlameWallBurnMult;
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.FlameWall).Color, 90f, 0.4f, Sfx.SpellFire);
                    break;
                }
                case AbilityId.HealingRain:
                {
                    var rain = AddZone(ZoneKind.HealingRain, p, target, 130f * s.RainRadiusMult, cap ? 10f : 6f);  // Monsoon: wider, heals more
                    rain.ExtraRevive = cap ? 1f : 0f;
                    rain.HealMult *= s.RainHealMult;
                    BroadcastFx(FxKind.Ring, target, Kits.Get(ZoneKind.HealingRain).Color, rain.Radius, 0.4f, Sfx.SpellHoly);
                    break;
                }
                case AbilityId.GustGate:  // Gust Gate (GustGate.cs)
                    CastGustGate(p, target, cap);
                    break;
                case AbilityId.LumenWard:  // Lumen Ward (LumenWard.cs)
                    CastLumenWard(p, target, cap);
                    break;
                case AbilityId.Meteor:
                {
                    var met = AddZone(ZoneKind.Meteor, p, target, 120f * s.MeteorRadiusMult, 0.9f);  // the zone is the warning; it hits when it expires
                    met.Embers = cap;
                    met.DmgMult *= s.MeteorDmgMult;  // Cataclysm
                    Sounds.Add(Sfx.SpellFire);
                    break;
                }
                case AbilityId.Blizzard:
                    AddZone(ZoneKind.Blizzard, p, target, 130f * s.BlizzardRadiusMult, (cap ? 10f : 5f) + s.BlizzardBonusTime).Aim = p.Aim;  // Whiteout; a lane along your aim
                    Sounds.Add(Sfx.Freeze);
                    break;
                case AbilityId.Tsunami:
                    CastTsunami(p, cap, pw);
                    break;
                case AbilityId.GaleWall:
                    CastGaleWall(p, cap);  // GaleWall.cs
                    break;
                case AbilityId.Flood:
                    CastFlood(p, cap, pw);  // Flood.cs
                    break;
                case AbilityId.WaterWard:
                    CastWaterWard(p, cap);  // WaterWard.cs
                    break;
                // Summoner
                case AbilityId.GolemWall:
                {
                    V2.Axes(p.Aim, out V2 fwd, out V2 side);
                    int half = cap ? 2 : 1;
                    var old = new List<int>();  // a new wall replaces the caster's old one
                    foreach (var m in Minions.Values)
                        if (m.Kind == MinionKind.Wall && m.Owner == p.Id)
                            old.Add(m.Id);
                    foreach (int oldId in old)
                        Minions.Remove(oldId);
                    for (int i = -half; i <= half; i++)
                    {
                        var g = SpawnMinion(MinionKind.Wall, p, p.Pos + fwd * GolemWallAhead + side * (i * GolemWallGap), -1f);  // -1: no timer, it stands until broken
                        g.BlockHp *= s.GolemHpMult;  // Basalt Hide
                        g.MaxHp = g.BlockHp;         // (its health bar reads against this)
                    }
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Earthquake:
                    CastEarthquake(p, target, cap, dmg);  // Burrower.cs
                    break;
                case AbilityId.ForcePact:
                    ForcePactCast(p, target, cap);  // ForcePact.cs
                    break;
                case AbilityId.Stampede:
                    CastStampede(p, target, cap);  // Stampede.cs
                    break;
                case AbilityId.LandShark:
                    CastLandShark(p, target, cap);  // LandShark.cs
                    break;
                case AbilityId.SpiritSwarm:
                {
                    int sprites = cap ? 8 : 5;
                    for (int i = 0; i < sprites; i++)
                        SpawnMinion(MinionKind.Sprite, p, p.Pos + V2.FromAngle(MathX.Tau * i / sprites) * 20f, 6f + s.SpriteBonusTime).HealRate = 10f * pw * s.SpriteHealMult;  // Sprite Choir
                    Sounds.Add(Sfx.Summon);
                    break;
                }
                case AbilityId.Phoenix:
                    SpawnMinion(MinionKind.Phoenix, p, p.Pos, 10f + s.PhoenixBonusTime);  // Rebirth Flame
                    Sounds.Add(Sfx.Summon);
                    break;
                case AbilityId.SoulLantern:
                    SpawnSoulLantern(p, target, cap, pw);  // SoulLantern.cs
                    break;
                case AbilityId.BatSwarm:
                {
                    int bats = cap ? 14 : 8;
                    for (int i = 0; i < bats; i++)
                    {
                        var bat = SpawnMinion(MinionKind.Bat, p, p.Pos + V2.FromAngle(MathX.Tau * i / bats) * 15f, 6f + s.BatBonusTime);
                        bat.Power *= s.BatDmgMult;  // Blood Bats
                        bat.Ang = MathX.Tau * i / bats;
                        bat.Launch = BatGuardTime + i * (BatPeelSpan / bats);  // they circle you first, then peel off one by one
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.7f, 0.5f, 0.95f), 120f, 0.5f, Sfx.Summon);  // the bell toll
                    break;
                }
                case AbilityId.Hydra:
                    CastHydra(p, target, pw);  // Hydra.cs (mastered: a fourth head)
                    break;
                case AbilityId.AlphaCall:
                    p.FrenzyT = 6f * pw + s.ScentBonusTime;  // Pack Leader: longer
                    p.ScentT = 6f * pw + s.ScentBonusTime;  // Blood Scent: the minions' stacks build while it runs (ScentStart..ScentMax)
                    if (cap)
                        for (int i = 0; i < 3; i++)
                            SpawnMinion(MinionKind.Wolf, p, p.Pos + V2.FromAngle(MathX.Tau * i / 3f) * 30f, 6f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.5f, 0.3f), 120f, 0.4f, Sfx.Roar);
                    break;
                default:
                    foreach (var cast in Classes.AbilityHooks)  // each class's KitDef.Ability (Knight.cs, Ninja.cs, Hemomancer.cs)
                        if (cast(this, p, id, target))
                            return;
                    Ultimate(p, id, target);  // Ultimates.cs
                    break;
            }
        }

        // --- Minions -----------------------------------------------------------

        const float BatGuardTime = 0.6f, BatPeelSpan = 0.6f, BatRingNear = 34f, BatRingGrow = 18f;  // Bat Swarm: circle the owner 0.6 s, then each bat peels off in turn over 0.6 s

        public const float GolemWallAhead = 112f, GolemWallGap = 76f;  // Golem Wall: golems touch (radius 38), their backs clear the caster

        /// How much more a minion takes before it breaks: Pack Bond for every one of yours, Granite Golem for the Golem Staff's Golem.
        static float MinionTough(Player owner, MinionKind kind, bool companion) =>
            owner.Stats.MinionToughMult * (companion && kind == MinionKind.Golem && owner.Weapon.Id == WeaponId.GolemStaff ? owner.Stats.StaffGolemTough : 1f);

        Minion SpawnMinion(MinionKind kind, Player owner, V2 pos, float life, bool companion = false)
        {
            var m = new Minion
            {
                Id = nextMinionId++, Kind = kind, Owner = owner.Id, Pos = ClampToArena(pos, 10f), T = life, Duration = life,
                Companion = companion, Ang = RandAngle(), Aim = owner.Aim,
            };
            m.Disp = m.Pos;
            m.HealRate = m.Def.HealRate * (kind == MinionKind.Sprite && !casting && owner.Weapon.Id == WeaponId.SpriteStaff ? owner.Stats.StaffSpriteHealMult : 1f);  // (Wildfire Seeds: the staff's sprites heal more)
            m.BlockHp = m.Def.BlockCapacity * MinionTough(owner, kind, companion);  // Pack Bond (the Summoner's class card) and Granite Golem: tougher
            m.Power = owner.Stats.MinionDmgMult;
            if (casting)
            {
                m.Power = castPower * owner.Stats.MinionDmgMult;
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
            Player owner;
            string was = CreditTo(Players.TryGetValue(m.Owner, out owner) && WeaponCompanion(m, owner) ? "weapon" : dmgSrc);
            ExplodeMinionHits(m, dmg, radius);
            dmgSrc = was;
        }

        /// A companion the player's summoner weapon keeps (or the Radiant Orb's light orb), not one an ability or a legendary brought.
        bool WeaponCompanion(Minion m, Player owner) => m.Companion && m.Kind == CompanionKind(owner);

        void ExplodeMinionHits(Minion m, float dmg, float radius)
        {
            bool shot = m.Sent && m.Kind == MinionKind.Imp;  // an Imp Caller imp is the weapon's shot: weapon mods ride its blast
            foreach (var e in FoesInCircle(m.Pos, radius, by: m.Owner))
            {
                HurtEnemy(e, dmg, m.Owner);
                if (shot)
                    ModHit(e, m.Owner, dmg, Defs.FireShot, default(V2));
            }
            BroadcastFx(FxKind.Boom, m.Pos, m.Def.Color, radius, 0.35f, Sfx.Explode);
            Minions.Remove(m.Id);
        }

        void TickMinions(float dt)
        {
            // Summoner weapons keep their companions topped up; the totem staff plants totems.
            foreach (var p in Players.Values)
            {
                TickGuards(p, dt);  // Warden's Guard (Stun.cs)
                TickPact(p, dt);  // Force Pact's thralls (ForcePact.cs)
                TickHiveDrones(p, dt);  // the Hive Queen's Hive Drones and Bomber Escort (QueenLoot.cs)
                TickBombers(p, dt);
                TickTwinScythes(p, dt);  // the Twin Reapers' Twin Scythes (ReaperLoot.cs)
                var w = p.Weapon;
                var kind = CompanionKind(p);
                int extra = ExtraShots(p);
                if (w.Id == WeaponId.WolfWhistle)
                    TickWolfPack(p);  // the pack grows with kills instead (WolfPack.cs)
                else
                    p.PackStarted = false;
                if (kind != MinionKind.None && w.Id != WeaponId.WolfWhistle)
                {
                    int want = CompanionCount(p), have = 0;
                    Minion surplus = null;
                    foreach (var m in Minions.Values)
                        if (m.Owner == p.Id && m.Companion && m.Kind != MinionKind.WardenGuard && m.Kind != MinionKind.Thrall && m.Kind != MinionKind.HiveDrone && m.Kind != MinionKind.Scythe)
                        {
                            have++;
                            surplus = m;
                            float scale = m.Kind == MinionKind.Treant ? 1f + 0.3f * extra : m.Kind == MinionKind.Golem ? GolemScale : 1f;
                            if (scale != m.Scale)
                            {
                                if (m.StaggerT <= 0f)
                                    m.BlockHp *= scale / m.Scale;  // keeps the same fraction of a bigger guard
                                m.Scale = scale;
                            }
                        }
                    if (have > want && surplus != null && kind != MinionKind.Imp)
                        Minions.Remove(surplus.Id);  // Artillery Assault's extras leave when it ends
                    p.CompanionCd -= dt;
                    if (have < want && p.CompanionCd <= 0f)
                    {
                        var m = SpawnMinion(kind, p, p.Pos + RandDir() * 30f, kind == MinionKind.Golem ? GolemLife : -1f, true);
                        if (kind == MinionKind.LightOrb)
                            m.Ang = MathX.Tau * have / Math.Max(1, want);
                        p.CompanionCd = kind == MinionKind.Imp ? w.CompanionRespawn / (1f + 0.5f * extra) / p.Stats.ImpRateMult : 0.3f;  // (Imp Horde: faster)
                    }
                }
            }

            var expired = scratchMinionsExpired;
            var exploding = scratchMinionsExploding;
            expired.Clear();
            exploding.Clear();
            string minionSrc = dmgSrc;
            foreach (var m in Minions.Values)
            {
                Player owner;
                if (!Players.TryGetValue(m.Owner, out owner))
                {
                    expired.Add(m);
                    continue;
                }
                dmgSrc = WeaponCompanion(m, owner) ? "weapon" : null;  // a summoner weapon's companions are the weapon (its bites, blasts and shots)
                if (m.T >= 0f)
                {
                    m.T -= dt;
                    if (m.T <= 0f)
                    {
                        if (m.Kind == MinionKind.Titan)
                            TitanFallen(m);  // Titan.cs
                        else if (m.Kind == MinionKind.Turret)
                            SentryFolds(m);  // SentryNova.cs
                        expired.Add(m);
                        continue;
                    }
                }
                var d = m.Def;
                m.HurtT -= dt;
                if (m.StaggerT > 0f)
                {
                    m.StaggerT -= dt;
                    if (m.StaggerT <= 0f)
                        m.BlockHp = d.BlockCapacity * m.Scale * MinionTough(owner, m.Kind, m.Companion);  // back on its feet
                }
                bool frenzy = owner.FrenzyT > 0f && !m.NoFrenzy;
                bool scent = owner.ScentT > 0f && !m.NoFrenzy;  // Alpha Call: the frenzy builds with each bite
                if (scent && m.Stacks < ScentStartFor(owner))
                    m.Stacks = ScentStartFor(owner);
                else if (!scent && m.Stacks != 0)
                    m.Stacks = 0;
                float speed = d.Speed * PackSpeed(m) * (scent ? 1f + ScentSpeedPer * m.Stacks : frenzy ? 1.6f : 1f) * (m.NoFrenzy && owner.LegionT > 0f ? LegionSpeed : 1f);
                float rate = owner.Stats.FireRateMult * (scent ? 1f + ScentRatePer * m.Stacks : frenzy ? 2f : 1f)
                    * (m.Kind == MinionKind.Guard && m.Companion && owner.Weapon.Id == WeaponId.BoneGuard ? owner.Stats.SkeletonRateMult : 1f);  // (Ossuary: Bone Guard's skeletons shoot faster)
                float dmg = d.Damage * owner.Stats.DmgMult * (frenzy ? 1.3f : 1f) * m.Scale * m.Power;
                float reach = d.Range * m.Scale;
                m.Cd -= dt * rate;

                if (m.Kind == MinionKind.Bee)
                {
                    if (MoveBee(m, owner, dt))  // stung: spent (HoneybeeStaff.cs)
                        expired.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.Thrall)
                {
                    TickThrall(m, owner, dt);  // ForcePact.cs
                    continue;
                }
                if (IsBot(m.Kind))
                {
                    TickBot(m, owner, dt, speed, dmg);  // EngineerBots.cs
                    continue;
                }
                if (m.Kind == MinionKind.SoulLantern)
                {
                    if (!TickSoulLantern(m, owner, dt))  // its player is gone (SoulLantern.cs)
                        expired.Add(m);
                    continue;
                }
                if (m.Kind == MinionKind.Butterfly)
                {
                    if (MoveButterfly(m, owner, dt))  // WorldTree.cs
                        expired.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.Hydra)
                {
                    TickHydra(m, dmg);  // Hydra.cs
                    continue;
                }
                if (m.Kind == MinionKind.Burrower)
                {
                    TickBurrower(m, owner, dt, dmg);  // Burrower.cs
                    continue;
                }
                if (IsTitanLike(m.Kind))
                {
                    TickTitan(m, owner, dt, speed, dmg, reach);  // Titan.cs
                    continue;
                }
                if (m.Kind == MinionKind.HiveDrone)
                {
                    MoveDrone(m, owner, dt);  // QueenLoot.cs
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.Scythe)
                {
                    MoveScythe(m, owner, dt);  // ReaperLoot.cs
                    continue;
                }
                if (m.Kind == MinionKind.ThrownScythe)
                {
                    if (MoveThrownScythe(m, owner, dt))  // caught (ReaperLoot.cs)
                        expired.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if ((m.Kind == MinionKind.ArcShard || m.Kind == MinionKind.ArcBlade) && m.Companion)
                {
                    MoveArcane(m, owner, dt);  // the Arcanist's Orbit Shards and Dancing Blades (Arcanist.cs)
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if (m.Kind == MinionKind.HiveBomber)
                {
                    if (MoveBomber(m, dt))  // QueenLoot.cs
                        exploding.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }
                if ((m.Kind == MinionKind.Wisp && owner.Weapon.Id == WeaponId.WispLantern || m.Kind == MinionKind.Imp && owner.Weapon.Id == WeaponId.ImpCaller) && m.Companion)
                {
                    if (MoveSent(m, owner, dt))  // sent at something by the Wisp Lantern or Imp Caller (they don't act on their own)
                        exploding.Add(m);
                    m.Pos = ClampToArena(m.Pos, d.Radius);
                    continue;
                }

                if (m.Hold > 0f)  // Legion's pack circles its owner before it is let loose
                {
                    m.Hold -= dt;
                    m.Ang += 7f * dt;
                    float ring = m.Kind == MinionKind.Wolf ? LegionRing : LegionRing * 0.65f;
                    m.Pos += (owner.Pos + V2.FromAngle(m.Ang) * ring - m.Pos).LimitLength(900f * dt);
                    continue;
                }

                // Movement
                Enemy target = null;
                switch (d.Move)
                {
                    case MinionMove.Orbit:
                        if (m.Kind == MinionKind.Phoenix)
                        {
                            PhoenixFlies(m, owner, dt);  // Phoenix.cs: glides after your cursor, pulsing heals
                            break;
                        }
                        m.Ang += speed * dt;
                        if (m.Kind == MinionKind.LightOrb && owner.RadiantOn)  // Radiant Orb's light orbs circle the orb while it's out
                            m.Pos += (owner.RadiantPos + V2.FromAngle(m.Ang) * LightOrbRing - m.Pos).LimitLength(LightOrbSpeed * dt);
                        else if (m.Kind == MinionKind.LightOrb)
                            m.Pos += (owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius - m.Pos).LimitLength(LightOrbSpeed * dt);
                        else
                            m.Pos = owner.Pos + V2.FromAngle(m.Ang) * d.OrbitRadius;
                        break;
                    case MinionMove.Chase:
                    {
                        if (m.Launch > 0f)  // a Bat Swarm bat still circling its owner
                        {
                            m.Launch -= dt;
                            V2 before = m.Pos;
                            m.Ang += 6f * dt;
                            float ring = BatRingNear + Math.Min((m.Duration - m.T) * 40f, BatRingGrow);
                            m.Pos = owner.Pos + V2.FromAngle(m.Ang) * ring;
                            m.Aim = (m.Pos - before).Angle;
                            break;
                        }
                        target = (m.Kind == MinionKind.Wolf ? Prey(owner) : null) ?? NearestEnemy(m.Pos, MinionChaseRange, by: owner.Id);  // wolves hunt the Wolf Whistle's prey
                        V2 goal = target != null ? target.Pos : owner.Pos + V2.FromAngle(m.Id) * 50f;
                        V2 to = goal - m.Pos;
                        float stop = target != null ? target.Radius + d.Radius * m.Scale : 10f;
                        if (to.Length > stop)
                            m.Pos += to.Normalized() * Math.Min(speed * dt, to.Length - stop);
                        if (m.Kind == MinionKind.Bat && to.Length > 1f)
                            m.Aim = to.Angle;  // faces where it's flying (the bat's look streaks behind it)
                        break;
                    }
                    case MinionMove.FollowHurt:
                    {
                        // Go to whoever has the lowest health fraction; otherwise stay by the owner.
                        Player who = null;
                        foreach (var q in Players.Values)
                            if (!q.Downed && q.Hp < q.MaxHp && MoreHurt(q, who))
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
                        if (m.Kind == MinionKind.Wyvern)
                        {
                            MoveWyvern(m, owner, dt);  // (WyvernStaff.cs)
                            break;
                        }
                        // The treant stands in front of you; the guardian spirit hovers on you.
                        V2 goal = m.Kind == MinionKind.Treant ? TreantGoal(m, owner)
                            : m.Kind == MinionKind.Nimbus ? NimbusGoal(owner)  // the cloud drifts after your cursor (Nimbus.cs)
                            : owner.Pos;
                        m.Pos += (goal - m.Pos).LimitLength(speed * (m.Kind == MinionKind.Nimbus ? NimbusDrift(owner) : 1f) * dt);
                        break;
                    }
                }
                m.Pos = ClampToArena(m.Pos, d.Radius);

                // Attacks
                switch (d.Attack)
                {
                    case MinionAttack.Shoot:
                    {
                        var e = NearestEnemy(m.Pos, d.Range, by: owner.Id);
                        if (e == null)
                            break;
                        float shotAim = (e.Pos - m.Pos).Angle;
                        if (m.Kind != MinionKind.Phoenix)
                            m.Aim = shotAim;  // the phoenix faces where it flies
                        if (m.Cd <= 0f && Battle)
                        {
                            m.Cd = d.Interval;
                            V2 dir = V2.FromAngle(shotAim);
                            byte kind = m.Kind == MinionKind.Turret ? Defs.TurretShot : m.Kind == MinionKind.Phoenix ? Defs.FireShot : Defs.MinionShot;
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
                            if (m.Cd <= 0f && Battle)
                            {
                                m.Cd = d.Interval;
                                V2 dir = V2.FromAngle(m.Aim);
                                SpawnBullet(Defs.MinionShot, m.Pos + dir * d.Radius, dir * 650f, 0f, GuardShotRange / 650f, dmg, m.Owner);
                            }
                            break;
                        }
                        var e = target ?? NearestEnemy(m.Pos, d.Radius * m.Scale + 40f, by: owner.Id);
                        if (e != null && m.Cd <= 0f && e.Pos.CloserThan(m.Pos, e.Radius + d.Radius * m.Scale + 6f))
                        {
                            m.Cd = d.Interval;
                            m.Aim = (e.Pos - m.Pos).Angle;
                            HurtEnemy(e, dmg, m.Owner);
                            if (m.Kind == MinionKind.Bat && m.BiteN++ % 2 == 0)  // a crescent slash across the prey (Game/BatLook.cs draws the bat)
                                BroadcastFx(FxKind.Slash, m.Pos, e.Pos, new Rgba(0.85f, 0.7f, 1f), 0.9f, 0.2f, Sfx.None);
                            if (scent)
                            {
                                if (m.Stacks >= ScentMax)  // a full stack lunges: triple damage in all, then back to 2
                                {
                                    HurtEnemy(e, dmg * (ScentLunge - 1f), m.Owner);
                                    m.Stacks = ScentStartFor(owner);
                                    BroadcastFx(FxKind.Ring, e.Pos, new Rgba(0.95f, 0.15f, 0.2f), 48f, 0.35f, Sfx.None);
                                }
                                else
                                    m.Stacks++;
                            }
                            if (m.Kind == MinionKind.Wolf)
                                PackBite(owner, m, e);
                            // Bear Totem's extra projectiles: the bear's bites also hit enemies around its target.
                            int extraBear = m.Kind == MinionKind.Bear && owner.Weapon.Id == WeaponId.BearTotem ? ExtraShots(owner) : 0;
                            if (extraBear > 0)
                            {
                                foreach (var o in FoesInCircle(e.Pos, 55f, by: m.Owner))
                                    if (o != e)
                                        HurtEnemy(o, dmg * Math.Min(1f, 0.4f * extraBear), m.Owner);
                                BroadcastFx(FxKind.Ring, e.Pos, d.Color, 55f, 0.25f, Sfx.None);
                            }
                        }
                        if (m.Kind == MinionKind.LightOrb)  // also heals allies it passes over
                            foreach (var q in Players.Values)
                                if (!q.Downed && q.Pos.CloserThan(m.Pos, reach + d.Radius))
                                    Heal(q, m.HealRate * dt, owner);
                        break;
                    }
                    case MinionAttack.Slam:
                        if (m.Kind == MinionKind.Treant)
                            TickTreantSeeds(m, owner, rate, dt);  // KitWeapons.cs
                        if (m.Cd <= 0f && NearestEnemy(m.Pos, reach, by: owner.Id) != null)
                        {
                            m.Cd = d.Interval;
                            foreach (var e in FoesInCircle(m.Pos, reach, by: m.Owner))
                                HurtEnemy(e, dmg, m.Owner);
                            BroadcastFx(FxKind.Ring, m.Pos, d.Color, reach, 0.3f, Sfx.None);
                        }
                        break;
                    case MinionAttack.Explode:
                        if (target != null && target.Pos.CloserThan(m.Pos, target.Radius + d.Radius + 6f))
                            exploding.Add(m);
                        break;
                    case MinionAttack.Heal:
                        foreach (var q in Players.Values)
                            if (q.Pos.CloserThan(m.Pos, d.Range))
                                Heal(q, m.HealRate * dt, owner);
                        break;
                }

                // The phoenix trades itself to bring back a downed ally.
                // Mastered, it revives them at full health and keeps fighting.
                if (m.Kind == MinionKind.Phoenix)
                    foreach (var q in Players.Values)
                        if (q.Downed)
                        {
                            RevivePlayer(q, Math.Min(1f, (m.Mastered ? 1f : 0.5f) + owner.Stats.PhoenixReviveBonus), owner);  // Rebirth Flame
                            BroadcastFx(FxKind.Ring, q.Pos, d.Color, PhoenixPulseRange, 0.6f, Sfx.Revive);  // a last, wide burst of fire
                            if (!m.Mastered)
                                expired.Add(m);
                            break;
                        }
            }
            dmgSrc = minionSrc;
            foreach (var m in exploding)
                if (Minions.ContainsKey(m.Id))
                    ExplodeMinion(m, m.Def.Damage * OwnerDmg(m) * m.Power * SentCost(m), m.Def.Range * m.Scale);  // a merged imp blasts wider
            foreach (var m in expired)
                Minions.Remove(m.Id);
        }

        /// Homing Rounds' damage cost on an Imp Caller imp sent at something (the homing it pays for is in MoveSent).
        float SentCost(Minion m)
        {
            Player p;
            return m.Sent && m.Kind == MinionKind.Imp && Players.TryGetValue(m.Owner, out p) ? HomingCost(p) : 1f;
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
                if (m.Def.BlockRadius <= 0f || m.StaggerT > 0f || m.BlockHp <= 0f || (IsTitanLike(m.Kind) && !TitanBlocking(m)) || !Enters(b, m.Pos, m.Def.BlockRadius * m.Scale))
                    continue;
                m.BlockHp -= m.Kind == MinionKind.CloneWall || m.Kind == MinionKind.Clone || m.Kind == MinionKind.Wall || m.Kind == MinionKind.VectorBlock ? b.Dmg * EnemyDamageScale(Wave) : BlockerWear(b);  // a wall clone or a golem wears from every shot, not just a boss's
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
                if (z.BlockHp <= 0f || z.Kind == ZoneKind.Cover || z.Kind == ZoneKind.DecoyDummy || !Enters(b, z.Pos, z.Radius))  // (a cover blocks by its plates: CoverStops)
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
            if (kind == ZoneKind.Incense)  // Thick Incense (the Censer's card): every cloud it leaves is bigger and lasts longer
            {
                radius *= owner.Stats.IncenseSizeMult;
                duration += owner.Stats.IncenseBonusTime;
            }
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

        // Reused every tick by TickMinions and TickZones.
        readonly List<Minion> scratchMinionsExpired = new List<Minion>();
        readonly List<Minion> scratchMinionsExploding = new List<Minion>();
        readonly List<Zone> scratchZonesExpired = new List<Zone>();

        /// A Blizzard is a lane, not a disc: Pos its middle, Aim its long axis (the cast's aim), Radius the old disc's radius it scales from.
        public const float BlizzardLen = 2.1875f, BlizzardWid = 0.775f;  // half-length and half-width, in Radius (1.75 and 0.62 x 1.25)
        public static bool InBlizzard(Zone z, V2 pos, float r)
        {
            V2 ex = V2.FromAngle(z.Aim), d = pos - z.Pos;
            return Math.Abs(d.Dot(ex)) <= z.Radius * BlizzardLen + r && Math.Abs(d.Dot(ex.Orthogonal())) <= z.Radius * BlizzardWid + r;
        }

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
                    if (z.Kind == ZoneKind.GrandSigil || z.Kind == ZoneKind.ArcCrush)
                        ArcZoneEnds(z);  // the sigil sends its frozen shots back, the crush slams (Arcanist.cs)
                    if (z.Kind == ZoneKind.FieldHospital)
                        EndHospital(z);  // everyone out, the room off the map (FieldHospital.cs)
                    expired.Add(z);
                    continue;
                }
                if (z.Kind == ZoneKind.LumenWard)
                {
                    TickLumen(z, dt);  // a cone, not a circle (LumenWard.cs)
                    continue;
                }
                if (z.Kind == ZoneKind.FieldHospital)
                {
                    TickHospital(z, dt);  // orbs, shrine, laser gate (FieldHospital.cs)
                    continue;
                }
                if (z.Kind == ZoneKind.SupplyDrop)
                {
                    TickSupplyDrop(z, dt);  // the landing, the buff, the health packs (SupplyDrop.cs)
                    continue;
                }
                foreach (var q in Players.Values)
                {
                    if (q.Pos.FartherThan(z.Pos, z.Radius))
                        continue;
                    if (q.Downed && z.ExtraRevive > 0f)
                    {
                        q.Revive += dt / D(DiffKnob.ReviveTime) * z.ExtraRevive;  // on top of the normal revive
                        q.ReviveBy = z.Owner;
                    }
                    else if (d.HealRate > 0f && !HallowedSpent(z))
                        Heal(q, d.HealRate * z.HealMult * dt, PlayerOrNull(z.Owner));
                }
                if (z.Kind == ZoneKind.Sanctum)
                    SanctumPulse(z, dt);  // Sanctum.cs
                if (z.Kind == ZoneKind.HallowedBlade)
                    HallowedPulse(z, dt);  // HallowedBlade.cs
                if (z.Kind == ZoneKind.Grove)
                    WorldTreeWave(z, dt);  // WorldTree.cs
                if (z.Kind == ZoneKind.BloodPool && !z.Congealed && z.Duration - z.T >= PoolCongeal)
                    CongealPool(z);  // Hemomancer.cs
                if (z.Kind == ZoneKind.VeinTrail)
                    TickVeinTrail(z);  // VeinRush.cs
                if (z.Kind == ZoneKind.Drawbridge)
                    TickDrawbridge(z);  // Drawbridge.cs
                if (z.Kind == ZoneKind.Bloodbath)
                    TickBloodbath(z, dt);  // Bloodbath.cs
                if (z.Kind == ZoneKind.Flood)
                    TickFlood(z);  // Flood.cs
                if (z.Kind == ZoneKind.GustGate)
                    TickGustGate(z, dt);  // GustGate.cs
                if (z.Kind == ZoneKind.Cover)
                {
                    TickCover(z);  // DeployableCover.cs
                    continue;  // (not a circle: nothing below applies)
                }
                if (z.Kind == ZoneKind.DecoyDummy)
                {
                    TickDecoy(z, dt);  // DecoyDummy.cs
                    continue;  // (nothing below applies)
                }
                if (z.Kind == ZoneKind.DeployTurret)
                {
                    TickDeployTurret(z, dt);  // DeployableTurret.cs
                    continue;  // (not a circle: nothing below applies)
                }
                if (z.Kind == ZoneKind.Stampede)
                {
                    TickStampede(z);  // Stampede.cs
                    continue;  // (not a circle: nothing below applies)
                }
                if (z.Kind == ZoneKind.LandShark)
                {
                    TickLandShark(z);  // LandShark.cs
                    continue;  // (its own tick: nothing below applies)
                }
                if (z.Kind == ZoneKind.Phalanx)
                {
                    TickPhalanx(z, dt);  // BonePhalanx.cs
                    continue;  // (not a circle: nothing below applies)
                }
                if (z.Kind == ZoneKind.Steam)
                {
                    TickSteamCloud(z, dt);  // Steam.cs
                    continue;
                }
                if (d.Dps > 0f || d.Slow > 0f || z.ExtraSlow > 0f)
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || (z.Kind == ZoneKind.BulletRain
                                ? Math.Abs(e.Pos.X - z.Pos.X) > z.Radius + e.Radius || Math.Abs(e.Pos.Y - z.Pos.Y) > z.Radius + e.Radius  // a square
                                : z.Kind == ZoneKind.Blizzard ? !InBlizzard(z, e.Pos, e.Radius)  // a long lane
                                : e.Pos.FartherThan(z.Pos, z.Radius + e.Radius)))
                            continue;
                        if (d.Dps > 0f && !HallowedSpent(z))
                            HurtEnemy(e, d.Dps * z.DmgMult * dt, z.Owner);
                        if (z.Kind == ZoneKind.BulletRain)
                            e.WetT = Math.Max(e.WetT, BulletRainWetTime);  // soaked: slowed, and lightning arcs to it (Tsunami.cs)
                        float slow = Math.Max(d.Slow, z.ExtraSlow);
                        if (z.Kind == ZoneKind.Blizzard)
                            Chill(e, 1, 0.3f, 1f - slow, gated: true);  // ice: chilled too, a stack a second (Chill.cs)
                        else if (slow > 0f)
                        {
                            Slow(e, 0.3f, 1f - slow);
                        }
                    }
            }
            RememberGustSteps();  // GustGate.cs: where everyone stood, for next tick's crossing test
            foreach (var z in expired)
            {
                if (z.Kind == ZoneKind.DeployTurret)
                    TurretBlow(z);  // the overheat blast (DeployableTurret.cs)
                if (z.Kind == ZoneKind.Ward)
                    WardEnds(z);  // Ninja.cs
                if (z.Def.BlastDamage > 0f)  // the meteor lands
                {
                    foreach (var e in FoesInCircle(z.Pos, z.Radius, by: z.Owner))
                        HurtEnemy(e, z.Def.BlastDamage * z.DmgMult, z.Owner);
                    if (z.Kind == ZoneKind.Meteor)
                        BroadcastFx(FxKind.Meteor, z.Pos, z.Def.Color, z.Radius, 2.8f, Sfx.BigExplode);  // flash, ring, sparks and a scorch mark (ElementalistLooks.cs)
                    else
                        BroadcastFx(FxKind.Boom, z.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.BigExplode);
                    Player caster;
                    if (z.Consecrates && Players.TryGetValue(z.Owner, out caster))
                    {
                        var holy = AddZone(ZoneKind.HallowedBlade, caster, z.Pos, z.Radius, HallowedLife);  // the sword stays planted (HallowedBlade.cs)
                        holy.DmgMult = z.DmgMult;
                        holy.HealMult = z.HealMult;
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
                if (z.Def.PreventDown && p.Pos.WithinReach(z.Pos, z.Radius))
                    return true;
            foreach (var m in Minions.Values)
                if (m.Kind == MinionKind.SoulLantern && m.Mastered && m.LinkTo == p.Id)
                    return true;  // a mastered Soul Lantern's linked player can't go down (SoulLantern.cs)
            return false;
        }

    }
}

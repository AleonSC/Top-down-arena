using System;
using System.Collections.Generic;

namespace Arena
{
    /// Mage and summoner kits: their weapons, abilities, minions and ground zones (host only).
    public partial class Game
    {
        const float MinionChaseRange = 600f;
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
                case WeaponId.StormHammer:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Chain = 3 + extra;
                    break;
                case WeaponId.RadiantOrb:
                {
                    var b = SpawnBullet(Defs.HolyShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    b.Pierce += 2;
                    b.HealAlly = 10f;
                    break;  // extra projectiles become Light Orbs circling you (TickMinions)
                }
                case WeaponId.ChainLight:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Chain = 2 + extra;
                    break;
                case WeaponId.PrismBeam:
                {
                    // +30% length, +25% damage and +50% healing per extra projectile.
                    var b = SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + Rand(-0.03f, 0.03f)) * w.Speed, 0f, w.Life * (1f + w.Grow * extra),
                        w.Damage * (1f + 0.25f * extra) * dmg, p.Id);
                    b.HealAlly = 2f * (1f + 0.5f * extra);
                    break;
                }
                case WeaponId.Fireball:
                {
                    var b = SpawnBullet(Defs.FireShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    b.Splash = 60f * AreaMult(p);
                    if (extra > 0)  // splits into small fireballs on impact
                        SetFrags(b, 2 * extra, Defs.FireShot, 8f * dmg, 380f, 0.35f, 30f * AreaMult(p), false);
                    break;
                }
                case WeaponId.ArcLightning:
                    SpawnBullet(Defs.BoltShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).Chain = 4 + extra;
                    break;
                case WeaponId.ArcaneMissiles:
                {
                    // Fanned out and curving back in; with extra projectiles, more of them, and they home instead.
                    int n = 3 + extra;
                    for (int i = 0; i < n; i++)
                    {
                        float f = i * 2f / (n - 1) - 1f;
                        SpawnBullet(Defs.ArcaneShot, muzzle, V2.FromAngle(aim + f * 0.4f) * w.Speed, extra > 0 ? 0f : -f * 1.2f, w.Life, w.Damage * dmg, p.Id, false, extra > 0 ? 4f : 0f);
                    }
                    break;
                }
                // Summoner (extra projectiles mostly add companions: see CompanionCount)
                case WeaponId.GolemStaff:
                    for (int i = -1; i <= 1; i++)
                        SpawnBullet(Defs.RockShot, muzzle, V2.FromAngle(aim + i * 0.12f) * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.BoneGuard:
                case WeaponId.WolfWhistle:
                case WeaponId.ImpCaller:
                    SpawnBullet(Defs.BoneShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.SpriteStaff:
                case WeaponId.TotemStaff:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
                    break;
                case WeaponId.TreantRod:
                    SpawnBullet(Defs.SeedShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id).HealAlly = 7f;
                    break;
                case WeaponId.WispLantern:
                    SpawnBullet(Defs.ArcaneShot, muzzle, dir * w.Speed, 0f, w.Life, w.Damage * dmg, p.Id);
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
                Name = "Arcane Ward", Cooldown = 14f, Desc = "Shield you and your ally for 60 damage for 8 s.",
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
                Name = "Phoenix", Cooldown = 22f, Desc = "A phoenix fights for 10 s. If your ally goes down, it revives them instantly.",
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
                m.HurtT -= dt;
                m.InvulnT -= dt;
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
                    case MinionMove.Escort:
                        target = MoveGuard(m, owner, speed, dt);  // Stun.cs
                        break;
                    case MinionMove.FollowOwner:
                    {
                        // The treant stands in front of you; the guardian spirit hovers on you.
                        V2 goal = m.Kind == MinionKind.Treant ? owner.Pos + V2.FromAngle(owner.Aim) * 45f
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

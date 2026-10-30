using System;
using System.Collections.Generic;

namespace Arena
{
    /// Casting abilities (host): the Soldier abilities here; kits, Knight and ultimates elsewhere.
    public partial class Game
    {
        void HostAbility(int id, int slot, V2 target, bool consumed)
        {
            Player p;
            if (!Players.TryGetValue(id, out p) || p.Downed)
                return;
            if (!consumed && !Consume(p, slot, true))
                return;
            p.Run.AbilitiesUsed++;
            var id0 = p.Slots[slot].Id;
            Recorder?.AbilityUsed(p, id0);
            bool hadTargets = State == MatchState.Fighting && AnyTarget();
            PayAbilityCost(p, id0);  // Blood Mage abilities cost health (BloodMage.cs)
            BeginCast(p, id0);
            CastAbility(p, slot, target);
            EndCast(p, slot, id0, hadTargets);
        }

        /// The Soldier's abilities, and the ones only very rare cards give (Shockwave, Overdrive, Blink, Nova, Turret). (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] SoldierAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.Barrier, Kit = Kit.Soldier, Role = Role.Tank, Choice = 0,
                Name = "Barrier", Cooldown = 12f, Desc = "A bubble that stops incoming enemy bullets. Bosses can break it (it soaks 360 of their damage).",
                Capstone = "Bullets that hit the barrier are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.MendPulse, Kit = Kit.Soldier, Role = Role.Healer, Choice = 0, Instant = true,
                Name = "Mend Pulse", Cooldown = 10f, Desc = "Heal everyone nearby and revive downed allies.",
                Capstone = "Also erases enemy bullets within its range.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Dash, Kit = Kit.Soldier, Role = Role.Dps, Choice = 0, CapstoneCharge = true,
                Name = "Dash", Cooldown = 3.5f, Desc = "Quick dash with brief invulnerability.",
                Capstone = "+1 charge.",
                Bot = b => b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Shockwave, Instant = true,
                Name = "Shockwave", Cooldown = 14f, Desc = "Erase enemy bullets around you and damage nearby enemies.",
                Capstone = "Also knocks nearby enemies back 120 px.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.Overdrive,
                Name = "Overdrive", Cooldown = 18f, Desc = "Double fire rate for 5 seconds.",
                Capstone = "+1 extra projectile while it lasts.",
            },
            new AbilityDef
            {
                Id = AbilityId.Blink,
                Name = "Blink", Cooldown = 7f, Desc = "Teleport toward your cursor.",
                Capstone = "Erases enemy bullets where you leave and where you land.",
                Bot = b => b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.Nova, Instant = true,
                Name = "Nova", Cooldown = 9f, Desc = "Fire a ring of 24 heavy shots.",
                Capstone = "Fires 48 shots instead of 24.",
                Bot = b => b.Danger || b.HaveTarget && b.TargetD < 230f,
            },
            new AbilityDef
            {
                Id = AbilityId.Turret,
                Name = "Turret", Cooldown = 20f, Desc = "Deploy a turret that shoots for 10 seconds.",
                Capstone = "Deploys two turrets.",
            },
            new AbilityDef
            {
                Id = AbilityId.Roar, Kit = Kit.Soldier, Role = Role.Tank, Choice = 1, Instant = true,
                Name = "Roar", Cooldown = 14f, Desc = "Every enemy targets you for 5 s, and you take 40% less damage meanwhile.",
                Capstone = "Also knocks enemies within 250 px back and slows them for 2 s.",
                Bot = b => b.HaveTarget && b.Players > 1,
            },
            new AbilityDef
            {
                Id = AbilityId.Bulldoze, Kit = Kit.Soldier, Role = Role.Tank, Choice = 2, Instant = true,
                Name = "Bulldoze", Cooldown = 9f, Desc = "Charge forward, damaging and knocking back everything in your path.",
                Capstone = "Erases enemy bullets along the charge.",
            },
            new AbilityDef
            {
                Id = AbilityId.Haven, Kit = Kit.Soldier, Role = Role.Healer, Choice = 1,
                Name = "Haven", Cooldown = 16f, Desc = "Place a healing zone for 6 s. Allies inside regenerate fast and revive 3x quicker (alone: you heal 50% faster).",
                Capstone = "Allies inside also take 30% less damage.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.SoulLink, Kit = Kit.Soldier, Role = Role.Healer, Choice = 2,
                Name = "Soul Link", Cooldown = 18f, Desc = "Tether to your ally for 6 s: they take half damage and you both regenerate. Alone, you link to a ghost of yourself that fights beside you.",
                Capstone = "Lasts 9 s, and both of you are invulnerable for 1 s when it starts.",
                Bot = b => b.AllyHurt || b.Players == 1 && b.HaveTarget,
            },
            new AbilityDef
            {
                Id = AbilityId.Grenade, Kit = Kit.Soldier, Role = Role.Dps, Choice = 1,
                Name = "Cluster Grenade", Cooldown = 6f, Desc = "Throw a grenade at your cursor that explodes into shrapnel.",
                Capstone = "Throws a second grenade 80 px further.",
            },
            new AbilityDef
            {
                Id = AbilityId.Mark, Kit = Kit.Soldier, Role = Role.Dps, Choice = 2, Instant = true,
                Name = "Mark for Death", Cooldown = 12f, Desc = "Mark the enemy nearest your cursor: it takes 50% more damage for 6 s.",
                Capstone = "Marks the three enemies nearest your cursor.",
            },
        };

        /// The ability's effect. Runs inside BeginCast/EndCast, so damage and healing it does directly
        /// (and the zones, minions and shots it makes) are scaled by its mastery. castMastered = its capstone.
        void CastAbility(Player p, int slot, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (p.Slots[slot].Id)
            {
                case AbilityId.Barrier:
                    p.BarrierT = BarrierTime + s.BarrierBonusTime;
                    p.BarrierMax = p.BarrierHp = BarrierCapacity * s.BarrierCapacityMult * pw;
                    p.BarrierReflect = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.5f, 0.8f, 1f), p.BarrierRadius, 0.4f, Sfx.Ability);
                    break;
                case AbilityId.Roar:
                    p.TauntT = (RoarTime + s.RoarBonusTime) * pw;
                    foreach (var e in Enemies.Values)
                    {
                        e.TargetId = p.Id;
                        e.RetargetT = p.TauntT;
                    }
                    if (cap)
                        Knockback(p.Pos, 250f, 120f, 2f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.45f, 0.3f), 320f, 0.5f, Sfx.Roar);
                    break;
                case AbilityId.Bulldoze:
                {
                    // Everything near the charge's path takes damage and is shoved aside.
                    p.InvulnT = Math.Max(p.InvulnT, BulldozeTime + 0.1f);
                    V2 from = p.Pos, path = target - from;
                    float len = Math.Max(1f, path.Length);
                    V2 dir = path / len;
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f)
                            continue;
                        float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                        V2 closest = from + dir * along;
                        if (e.Pos.DistanceTo(closest) > e.Radius + 30f)
                            continue;
                        HurtEnemy(e, BulldozeDamage * s.DmgMult * s.BulldozeMult, p.Id);
                        if (!Heavy(e))
                        {
                            V2 away = (e.Pos - closest).Normalized();
                            e.Pos = ClampToArena(e.Pos + (away.IsZero ? dir.Orthogonal() : away) * 70f, e.Radius);
                        }
                    }
                    if (cap)
                        EraseAlong(from, target, 60f);
                    BroadcastFx(FxKind.Ring, target, new Rgba(0.5f, 0.75f, 1f), 60f, 0.35f, Sfx.Blink);
                    break;
                }
                case AbilityId.Haven:
                {
                    var z = AddZone(ZoneKind.Haven, p, p.Pos, HavenRadius * s.HavenRadiusMult, HavenTime + s.HavenBonusTime);
                    if (cap)
                        z.DamageCut = 0.3f;
                    BroadcastFx(FxKind.Ring, p.Pos, z.Def.Color, z.Radius, 0.5f, Sfx.Ability);
                    break;
                }
                case AbilityId.SoulLink:
                {
                    p.LinkT = (cap ? 9f : LinkTime) * pw;
                    p.LinkTo = p.Id;
                    // Link to the nearest ally who's still standing (or any ally, if all are down).
                    Player best = null;
                    foreach (var q in Players.Values)
                        if (q.Id != p.Id && (best == null || (best.Downed && !q.Downed)
                            || best.Downed == q.Downed && q.Pos.DistanceSq(p.Pos) < best.Pos.DistanceSq(p.Pos)))
                            best = q;
                    if (best != null)
                        p.LinkTo = best.Id;
                    else
                        SpawnSoulGhost(p, p.LinkT, cap);  // alone: a ghost of you to link to (SoulGhost.cs)
                    if (cap)
                        foreach (var q in new[] { p, best })
                            if (q != null && !q.Downed)
                                q.InvulnT = Math.Max(q.InvulnT, 1f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 1f, 0.9f), 60f, 0.4f, Sfx.Ability);
                    break;
                }
                case AbilityId.Grenade:
                {
                    V2 vel = (target - p.Pos) / GrenadeFlight;
                    var g = SpawnBullet(Defs.GrenadeShot, p.Pos, vel, 0f, GrenadeFlight, GrenadeDamage * s.DmgMult, p.Id);
                    g.Splash = GrenadeRadius * s.GrenadeRadiusMult;
                    g.Burst = 8 + s.GrenadeExtraShrapnel;
                    if (cap)
                    {
                        // A second grenade lands 80 px past the first.
                        V2 far = target + (target - p.Pos).Normalized() * 80f;
                        var g2 = SpawnBullet(Defs.GrenadeShot, p.Pos, (far - p.Pos) / GrenadeFlight, 0f, GrenadeFlight, GrenadeDamage * s.DmgMult, p.Id);
                        g2.Splash = g.Splash;
                        g2.Burst = g.Burst;
                    }
                    break;
                }
                case AbilityId.Mark:
                {
                    // The enemy nearest the cursor (the three nearest once mastered).
                    var marked = new List<Enemy>();
                    for (int n = 0; n < (cap ? 3 : 1); n++)
                    {
                        Enemy best = null;
                        foreach (var e in Enemies.Values)
                            if (e.SpawnT <= 0f && !marked.Contains(e) && (best == null || e.Pos.DistanceSq(target) < best.Pos.DistanceSq(target)))
                                best = e;
                        if (best == null)
                            break;
                        marked.Add(best);
                        best.MarkT = MarkTime;
                        best.MarkMult = 1f + (0.5f + s.MarkBonus) * pw;
                        castHits++;
                        BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.25f, 0.25f), best.Radius + 30f, 0.5f, Sfx.Ability);
                    }
                    break;
                }
                case AbilityId.Dash:
                    p.InvulnT = Math.Max(p.InvulnT, DashTime + 0.12f + s.DashIframeBonus);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.7f, 0.35f), 24f, 0.25f, Sfx.Blink);
                    break;
                case AbilityId.MendPulse:
                {
                    float range = PulseRange * s.PulseRangeMult;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.DistanceTo(p.Pos) >= range)
                            continue;
                        if (q.Downed)
                            RevivePlayer(q, 0.5f, p);
                        else
                            Heal(q, PulseHeal * s.PulseHealMult, p);
                    }
                    if (cap)
                        EraseEnemyBullets(p.Pos, range);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.4f, 1f, 0.55f), range, 0.6f, Sfx.Ability);
                    break;
                }
                case AbilityId.Shockwave:
                {
                    EraseEnemyBullets(p.Pos, ShockwaveRange);
                    foreach (var e in Enemies.Values)
                    {
                        if (e.SpawnT > 0f || e.Pos.DistanceTo(p.Pos) > ShockwaveRange + e.Radius)
                            continue;
                        HurtEnemy(e, ShockwaveDamage * s.DmgMult, p.Id);
                    }
                    if (cap)
                        Knockback(p.Pos, ShockwaveRange, 120f, 0f);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.9f, 0.5f), ShockwaveRange, 0.45f, Sfx.Shockwave);
                    break;
                }
                case AbilityId.Overdrive:
                    p.OverdriveT = OverdriveTime * pw;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 0.4f, 0.3f), 60f, 0.4f, Sfx.Ability);
                    break;
                case AbilityId.Blink:
                    p.InvulnT = Math.Max(p.InvulnT, 0.3f);
                    if (cap)
                    {
                        EraseEnemyBullets(p.Pos, 120f);
                        EraseEnemyBullets(target, 120f);
                    }
                    BroadcastFx(FxKind.Ring, target, new Rgba(0.7f, 0.6f, 1f), 40f, 0.35f, Sfx.Blink);
                    break;
                case AbilityId.StunGun:
                    DrawStunGun(p, cap);  // Stun.cs
                    break;
                case AbilityId.Nova:
                    int novaShots = cap ? 48 : 24;
                    for (int i = 0; i < novaShots; i++)
                    {
                        V2 dir = V2.FromAngle(p.Aim + MathX.Tau * i / novaShots);
                        SpawnBullet(Defs.NovaShot, p.Pos + dir * p.Def.BodyR, dir * 480f, 0f, 1.2f, 22f * s.DmgMult, p.Id);
                    }
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.95f, 0.8f, 1f), 50f, 0.3f, Sfx.Ability);
                    break;
                case AbilityId.Turret:
                    if (cap)
                    {
                        V2 side = V2.FromAngle(p.Aim).Orthogonal() * 30f;
                        SpawnMinion(MinionKind.Turret, p, p.Pos + side, TurretTime);
                        SpawnMinion(MinionKind.Turret, p, p.Pos - side, TurretTime);
                    }
                    else
                        SpawnMinion(MinionKind.Turret, p, p.Pos, TurretTime);
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.6f, 0.95f, 1f), 40f, 0.35f, Sfx.Ability);
                    break;
                default:
                    KitAbility(p, p.Slots[slot].Id, target);  // mage and summoner abilities (Minions.cs)
                    break;
            }
        }
    }
}

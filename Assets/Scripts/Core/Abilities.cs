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

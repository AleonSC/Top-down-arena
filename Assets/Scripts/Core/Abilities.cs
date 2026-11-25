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
            if (!Players.TryGetValue(id, out p) || p.Downed || TankOn(p) || ArmOn(p))  // (in A Literal Tank or Iron Armadillo: no abilities, LiteralTank.cs, IronArmadillo.cs)
                return;
            if (OrbitRelease(p, slot))  // Blade Fan pressed again: the kunai fly out (no cooldown spent)
            {
                p.Run.AbilitiesUsed++;
                ReleaseOrbit(p);
                return;
            }
            if (HarvestPress(p, slot))  // Crimson Harvest pressed again: throw the ball once the rip is over (no cooldown spent)
            {
                if (HarvestHolding(p))
                {
                    p.Run.AbilitiesUsed++;
                    ThrowHarvest(p);
                }
                return;
            }
            if (TurretPress(p, slot))  // Deployable Turret pressed again: you step out and it starts to overheat (no cooldown spent)
            {
                p.Run.AbilitiesUsed++;
                DismountTurret(p);
                return;
            }
            if (GalePress(p, slot))  // Gale Wall pressed again: the lens unleashes its cone (no cooldown spent)
            {
                p.Run.AbilitiesUsed++;
                UnleashGaleWall(p);
                return;
            }
            if (RainPress(p, slot))  // Sanguine Rain pressed again: the healer cancels it (no cooldown spent)
            {
                EndRain(p, true);
                return;
            }
            if (BathPress(p, slot))  // Bloodbath pressed again: the channel ends and the pool dries up (no cooldown spent; Bloodbath.cs)
            {
                EndBath(ActiveBath(p));
                return;
            }
            if (StandRelease(p, slot))  // Last Stand pressed again: it comes down (no cooldown spent for the press)
            {
                p.Run.AbilitiesUsed++;
                EndStand(p, true);
                return;
            }
            if (ShellRelease(p, slot))  // Blood Shell pressed again: it comes down (no cooldown spent for the press)
            {
                p.Run.AbilitiesUsed++;
                EndShell(p, false);
                return;
            }
            if (SprayRelease(p, slot))  // Arterial Spray pressed again: the vein closes (no cooldown spent for the press)
            {
                p.Run.AbilitiesUsed++;
                CloseSpray(p, false);
                return;
            }
            if (WallRelease(p, slot))  // Blood Wall pressed again: it stops (no cooldown spent for the press)
            {
                p.Run.AbilitiesUsed++;
                EndBloodWall(p);
                return;
            }
            if (GraspRelease(p, slot))  // Kinetic Grasp pressed again: the held enemy is thrown (no cooldown spent; Arcanist.cs)
            {
                p.Run.AbilitiesUsed++;
                ThrowGrasp(p);
                return;
            }
            if (SteelRelease(p, slot))  // Caught Steel pressed again: the cut throws the shards (no cooldown spent for the press)
            {
                p.Run.AbilitiesUsed++;
                ReleaseSteel(p);
                return;
            }
            if ((p.StandOn || Seated(p)) && MovesYou(p.Slots[slot].Id))
                return;  // planted: nothing that would carry you off
            if (!consumed && p.Slots[slot].Id == AbilityId.WaterWard && WwFull(p))
                return;  // every drop already out (WaterWard.cs)
            if (!consumed && !Consume(p, slot, true))
                return;
            p.Run.AbilitiesUsed++;
            CellSkillUsed(p);  // in a Holding Cell: a second off its clock (HoldingCell.cs)
            var id0 = p.Slots[slot].Id;
            Recorder?.AbilityUsed(p, id0);
            bool hadTargets = Battle && AnyTarget();
            PayAbilityCost(p, id0);  // Hemomancer abilities cost health (Hemomancer.cs)
            BeginCast(p, id0);
            CastAbility(p, slot, target);
            Recorder?.CastHits(p, castHits);
            EndCast(p, slot, id0, hadTargets);
            if (id0 != AbilityId.None)  // its name over you, so everyone can tell what you just did (AbilityLooks.cs)
                BroadcastFx(FxKind.Callout, p.Pos, new V2(p.Id, (float)id0), new Rgba(1f, 1f, 1f), 0f, CalloutTime, Sfx.None);
        }

        public const float CalloutTime = 1.1f;

        public const float SurgeTime = 3f;  // Arcane Surge (the Elementalist's class card): faster firing after a 1st ability cast

        /// The ability's effect. Runs inside BeginCast/EndCast, so damage and healing it does directly
        /// (and the zones, minions and shots it makes) are scaled by its mastery. castMastered = its capstone.
        void CastAbility(Player p, int slot, V2 target)
        {
            var s = p.Stats;
            if (slot == 0 && s.SurgeRate > 0f)
                p.SurgeT = SurgeTime;
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
                case AbilityId.DeployableTurret:
                    CastDeployTurret(p, target, cap);  // DeployableTurret.cs
                    break;
                case AbilityId.DeployableCover:
                    CastCover(p, target, cap);  // DeployableCover.cs
                    break;
                case AbilityId.Bulldoze:
                {
                    // Everything near the charge's path takes damage and is shoved aside.
                    p.InvulnT = Math.Max(p.InvulnT, BulldozeTime + BulldozeTail);  // a short tail past the charge's end, as you land among them
                    V2 from = p.Pos, path = target - from;
                    float len = Math.Max(1f, path.Length);
                    V2 dir = path / len;
                    foreach (var e in AllFoes(by: p.Id))
                    {
                        float along = Math.Max(0f, Math.Min(len, (e.Pos.X - from.X) * dir.X + (e.Pos.Y - from.Y) * dir.Y));
                        V2 closest = from + dir * along;
                        if (e.Pos.FartherThan(closest, e.Radius + 30f))
                            continue;
                        HurtEnemy(e, BulldozeDamage * s.DmgMult * s.BulldozeMult, p.Id);
                        Daze(e);  // can't hit back for a split second (Daze.cs)
                        if (!Heavy(e))
                        {
                            ShoveFrom(e, closest, 70f, dir.Orthogonal());  // slides (Knock.cs)
                        }
                    }
                    if (cap)
                        EraseAlong(from, target, 60f);
                    BroadcastFx(FxKind.Ring, target, new Rgba(0.5f, 0.75f, 1f), 60f, 0.35f, Sfx.Blink);
                    break;
                }
                case AbilityId.Adrenaline:
                    Adrenaline(p, target, cap, pw);  // Adrenaline.cs
                    break;
                case AbilityId.Medkit:
                    ThrowMedkit(p, target, cap, pw);  // Medkit.cs
                    break;
                case AbilityId.DecoyDummy:
                    CastDecoy(p, target, cap);  // DecoyDummy.cs
                    break;
                case AbilityId.Airstrike:
                    CastAirstrike(p, target, cap, pw);  // Airstrike.cs
                    break;
                case AbilityId.Claymore:
                    CastClaymore(p, target, cap);  // Claymore.cs
                    break;
                case AbilityId.PoisonDart:
                    CastPoisonDart(p, target, cap, pw);  // PoisonDart.cs
                    break;
                case AbilityId.MendPulse:
                {
                    float range = PulseRange * s.PulseRangeMult;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.FartherOrAt(p.Pos, range))
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
                    CastShockwave(p, cap, ShockwaveDamage * s.DmgMult);  // Shockwave.cs: the wave runs out over ShockwaveSweep s
                    break;
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
                case AbilityId.RoyalDive:
                    StartRoyalDive(p, target, pw, cap);  // QueenLoot.cs
                    break;
                case AbilityId.ScytheThrow:
                    ThrowScythe(p, target, pw, cap);  // ReaperLoot.cs
                    break;
                case AbilityId.Starburst:
                    CastNova(p, s, cap);  // SentryNova.cs
                    break;
                case AbilityId.Turret:
                    CastTurret(p, cap);  // SentryNova.cs
                    break;
                default:
                    KitAbility(p, p.Slots[slot].Id, target);  // mage and summoner abilities (Minions.cs), then each class's KitDef.Ability, then ultimates
                    break;
            }
        }
    }
}

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// One-off effects in the arena (DrawEffect and its FxKind looks: repulse, shatter, slashes, claws, chains,
    /// arc fields, light cones, icicles, jabs, bolts).
    public partial class ArenaApp
    {
        delegate void FxDraw(Effect fx, float f, float t);
        FxDraw[] fxDraws;

        /// A short-lived effect, by kind: each kind's look is one entry of the table (BuildFxDraws); kinds without one draw nothing.
        void DrawEffect(Effect fx, float t)
        {
            var draws = fxDraws ?? (fxDraws = BuildFxDraws());
            int k = (int)fx.Kind;
            if (k < draws.Length)
                draws[k]?.Invoke(fx, fx.T / fx.Dur, t);
        }

        /// Every FxKind's draw (f: how far through the effect it is, 0-1; t: the animation clock).
        FxDraw[] BuildFxDraws()
        {
            var d = new FxDraw[Enum.GetValues(typeof(FxKind)).Length];
            d[(int)FxKind.Boom] = DrawFxBoom;
            d[(int)FxKind.BloodFreeze] = (fx, f, t) => DrawBloodFreeze(fx);  // BloodFreezeLook.cs
            d[(int)FxKind.Shockwave] = (fx, f, t) => DrawShockwave(fx);  // ShockwaveLook.cs
            d[(int)FxKind.Sentry] = (fx, f, t) => DrawSentryFx(fx);  // SentryNovaLook.cs
            d[(int)FxKind.Nova] = (fx, f, t) => DrawNovaFx(fx);  // SentryNovaLook.cs
            d[(int)FxKind.Debris] = (fx, f, t) => DrawDebrisFx(fx, f);  // PropsLook.cs
            d[(int)FxKind.Spark] = (fx, f, t) => Arc(fx.Pos, Mathf.Max(1f, fx.Radius * f), 2f, fx.Color.WithAlpha(1f - f), 12);
            d[(int)FxKind.Ring] = DrawFxRing;
            d[(int)FxKind.Target] = DrawFxTarget;
            d[(int)FxKind.PortalSeal] = (fx, f, t) => DrawPortalSeal(fx, f);  // SpawnPortalLook.cs
            d[(int)FxKind.PitFall] = (fx, f, t) => DrawPitFall(fx, f);  // PitLook.cs
            d[(int)FxKind.Graze] = (fx, f, t) => DrawGrazeFx(fx, f);  // GrazeLook.cs
            d[(int)FxKind.GasLob] = (fx, f, t) => DrawGasLobFx(fx, t);  // GasLobLook.cs
            d[(int)FxKind.Slash] = DrawFxSlash;
            d[(int)FxKind.Jab] = (fx, f, t) => DrawJabFx(fx, f);
            d[(int)FxKind.Claw] = DrawFxClaw;
            d[(int)FxKind.Bolt] = (fx, f, t) => DrawBoltFx(fx, f);
            d[(int)FxKind.Shatter] = (fx, f, t) => DrawShatterFx(fx, f);
            d[(int)FxKind.Implode] = (fx, f, t) => DrawImplodeFx(fx, f);  // HemomancerLook.cs
            d[(int)FxKind.Bleed] = (fx, f, t) => DrawBleedFx(fx, f);  // HemomancerLook.cs, like the four below
            d[(int)FxKind.BloodShell] = (fx, f, t) => DrawBloodShellFx(fx, f);
            d[(int)FxKind.ShellHit] = (fx, f, t) => DrawShellHitFx(fx, f);  // BloodShellLook.cs
            d[(int)FxKind.StandHit] = (fx, f, t) => DrawStandHitFx(fx, f);  // LastStandLook.cs
            d[(int)FxKind.HolyCross] = (fx, f, t) => DrawHolyCrossFx(fx);  // HolyCrossLook.cs
            d[(int)FxKind.HammerSlam] = (fx, f, t) => DrawHammerSlamFx(fx);  // StormHammerLook.cs
            d[(int)FxKind.HammerThrow] = DrawFxHammerThrow;
            d[(int)FxKind.KatanaStanceFx] = (fx, f, t) => DrawKatanaStanceFx(fx);  // KatanaLook.cs
            d[(int)FxKind.KatanaDash] = (fx, f, t) => DrawKatanaDashFx(fx);  // KatanaLook.cs (its light: DrawKatanaGlow)
            d[(int)FxKind.KatanaHop] = DrawFxKatanaHop;
            d[(int)FxKind.KatanaSweep] = DrawFxKatanaHop;
            d[(int)FxKind.KatanaSlash] = DrawFxKatanaHop;
            d[(int)FxKind.KatanaBurst] = DrawFxKatanaHop;
            d[(int)FxKind.PerfectParry] = (fx, f, t) => DrawPerfectParryFx(fx);  // ParryLook.cs
            d[(int)FxKind.KatanaCut] = (fx, f, t) => DrawKatanaCutFx(fx);
            d[(int)FxKind.StaffShove] = DrawFxStaffShove;
            d[(int)FxKind.CavalryCall] = (fx, f, t) => DrawCavalryCallFx(fx);  // CavalryCallLook.cs
            d[(int)FxKind.WarhornVolley] = (fx, f, t) => DrawWarhornGround(fx);  // WarhornVolleyLook.cs
            d[(int)FxKind.OrbitalLaser] = (fx, f, t) => DrawOrbitalGround(fx);  // OrbitalLaserLook.cs
            d[(int)FxKind.Airstrike] = (fx, f, t) => DrawAirstrikeFx(fx);  // AirstrikeLook.cs
            d[(int)FxKind.BsBlow] = (fx, f, t) => DrawBsBlowFx(fx);  // BloodBrawlerLook.cs
            d[(int)FxKind.EmbolusBlast] = (fx, f, t) => DrawEmbolusBlastFx(fx);  // EmbolusLook.cs
            d[(int)FxKind.NeedleSwarm] = (fx, f, t) => DrawNeedleSwarmFx(fx, f);  // NeedleSwarmLook.cs
            d[(int)FxKind.PistolWhip] = (fx, f, t) => DrawPistolWhipFx(fx);  // PistolWhipLook.cs
            d[(int)FxKind.StoneSlam] = (fx, f, t) => DrawStoneSlamFx(fx);  // StonespearLook.cs
            d[(int)FxKind.BeeSting] = (fx, f, t) => DrawBeeStingFx(fx, t);  // HoneybeeLook.cs
            d[(int)FxKind.WyvernSlam] = (fx, f, t) => DrawWyvernSlamFx(fx);  // WyvernStaffLook.cs
            d[(int)FxKind.HoneyHarvest] = (fx, f, t) => DrawHoneyHarvestFx(fx);  // HoneybeeLook.cs
            d[(int)FxKind.WindWave] = (fx, f, t) => DrawWindWave(fx);  // WindClashLook.cs
            d[(int)FxKind.ArcBall] = (fx, f, t) => DrawArcBallPlain(fx);  // StaticShockLook.cs (Visual effects on: the glow pass)
            d[(int)FxKind.ArcBurst] = (fx, f, t) => DrawArcBurstPlain(fx);
            d[(int)FxKind.BlastCut] = (fx, f, t) => DrawBlastCut(fx);  // BloodBlastLook.cs
            d[(int)FxKind.BlastTide] = (fx, f, t) => DrawBlastTide(fx);  // BloodBlastLook.cs
            d[(int)FxKind.BlastReturn] = (fx, f, t) => DrawBlastReturn(fx);  // BloodBlastLook.cs
            d[(int)FxKind.FrostStep] = (fx, f, t) => DrawFrostStepFx(fx);  // FrostLanceLook.cs
            d[(int)FxKind.FrostBurst] = (fx, f, t) => DrawFrostBurstFx(fx);  // FrostLanceLook.cs
            d[(int)FxKind.FrostBreak] = (fx, f, t) => DrawFrostBreakFx(fx);  // FrostLanceLook.cs
            d[(int)FxKind.AbsoluteZero] = (fx, f, t) => DrawAbsoluteZeroFx(fx);  // FrostLanceLook.cs
            d[(int)FxKind.HalberdStep] = (fx, f, t) => DrawHalberdStepFx(fx, false);  // HalberdLook.cs
            d[(int)FxKind.HalberdPoke] = (fx, f, t) => DrawHalberdPokeFx(fx, false);  // HalberdLook.cs
            d[(int)FxKind.VortexWisp] = (fx, f, t) => DrawVortexWispFx(fx);  // VortexLook.cs
            d[(int)FxKind.LivingBolt] = (fx, f, t) => DrawLivingBoltFx(fx);  // LivingBoltLook.cs
            d[(int)FxKind.ForbiddenPath] = (fx, f, t) => DrawForbiddenFx(fx, t);  // ForbiddenPathLook.cs (the crescents, claw marks and black sun are the floor pass: DrawForbiddenFloor)
            d[(int)FxKind.BambooGrove] = (fx, f, t) => { };  // drawn in the floor and body passes (BambooGroveLook.cs)
            d[(int)FxKind.FlashFire] = (fx, f, t) => { };  // drawn in the floor and body passes (FlashFireLook.cs)
            d[(int)FxKind.SpiritInk] = (fx, f, t) => DrawSpiritInkFx(fx);  // SpiritAvatarLook.cs (the strokes and stains are the floor pass: DrawSpiritFloor)
            d[(int)FxKind.BulwarkStep] = (fx, f, t) => DrawBulwarkStepFx(fx);  // BulwarkBarrierLook.cs
            d[(int)FxKind.BulwarkBash] = (fx, f, t) => DrawBulwarkBashFx(fx, t);  // BulwarkBarrierLook.cs
            d[(int)FxKind.BulwarkBlock] = (fx, f, t) => DrawBulwarkBlockFx(fx);  // BulwarkBarrierLook.cs
            d[(int)FxKind.BulwarkThrow] = (fx, f, t) => DrawBulwarkThrowFx(fx, t);  // BulwarkBarrierLook.cs
            d[(int)FxKind.GaleCatch] = (fx, f, t) => DrawGaleCatchFx(fx);  // GaleWallLook.cs
            d[(int)FxKind.GaleBlast] = (fx, f, t) => DrawGaleBlastFx(fx);  // GaleWallLook.cs
            d[(int)FxKind.WwSplash] = (fx, f, t) => DrawWwSplashFx(fx);  // WaterWardLook.cs
            d[(int)FxKind.WwBurst] = (fx, f, t) => DrawWwBurstFx(fx);  // WaterWardLook.cs
            d[(int)FxKind.CoverHit] = DrawFxCoverHit;
            d[(int)FxKind.CoverBreak] = DrawFxCoverBreak;
            d[(int)FxKind.CoverFold] = (fx, f, t) => DrawCoverFoldFx(fx);  // DeployableCoverLook.cs
            d[(int)FxKind.ClaymoreBlast] = (fx, f, t) => DrawClaymoreBlastFx(fx);  // ClaymoreLook.cs
            d[(int)FxKind.DecoyHit] = (fx, f, t) => DrawDecoyHitFx(fx);  // DecoyDummyLook.cs
            d[(int)FxKind.DecoyPop] = (fx, f, t) => DrawDecoyPopFx(fx);  // DecoyDummyLook.cs
            d[(int)FxKind.DecoyFold] = (fx, f, t) => DrawDecoyFoldFx(fx);  // DecoyDummyLook.cs
            d[(int)FxKind.WindCatch] = (fx, f, t) => DrawWindCatch(fx);  // WindClashLook.cs
            d[(int)FxKind.DoubleBarrel] = (fx, f, t) => DrawDoubleBarrelFx(fx);  // TankAltLook.cs
            d[(int)FxKind.JuggShard] = (fx, f, t) => DrawJuggShardFx(fx);  // JuggernautLook.cs
            d[(int)FxKind.BossBeam] = (fx, f, t) => DrawBossBeamFx(fx);  // BossBeamLook.cs
            d[(int)FxKind.TaserHit] = (fx, f, t) => DrawTaserHitFx(fx);  // TaserLook.cs
            d[(int)FxKind.VenomDart] = (fx, f, t) => DrawVenomDartFx(fx);  // PoisonDartLook.cs, like the one below
            d[(int)FxKind.VenomSpread] = (fx, f, t) => DrawVenomSpreadFx(fx);
            d[(int)FxKind.PactBind] = (fx, f, t) => DrawPactBindFx(fx, f, t);  // ForcePactLook.cs, like the three below
            d[(int)FxKind.PactSnap] = (fx, f, t) => DrawPactSnapFx(fx, f);
            d[(int)FxKind.PactDissolve] = (fx, f, t) => DrawPactDissolveFx(fx, f);
            d[(int)FxKind.PactRise] = (fx, f, t) => DrawPactRiseFx(fx, f);
            d[(int)FxKind.TitheSnap] = (fx, f, t) => DrawTitheSnapFx(fx, f);  // VeinTitheLook.cs
            d[(int)FxKind.Exsanguinate] = (fx, f, t) => DrawExsanguinateFx(fx, t);  // ExsanguinateLook.cs
            d[(int)FxKind.RainRelease] = (fx, f, t) => DrawRainReleaseFx(fx, f);  // SanguineRainLook.cs
            d[(int)FxKind.HarvestThrow] = (fx, f, t) => DrawHarvestThrowFx(fx);  // CrimsonHarvestLook.cs
            d[(int)FxKind.TitanSlam] = (fx, f, t) => DrawTitanFx(fx, false);  // TitanLook.cs (its cracks and rubble are the floor pass: DrawTitanFloor)
            d[(int)FxKind.BurrowSnap] = (fx, f, t) => DrawBurrowSnapFx(fx);  // BurrowerLook.cs
            d[(int)FxKind.ShellPop] = (fx, f, t) => DrawShellPopFx(fx, f);
            d[(int)FxKind.MartyrCast] = (fx, f, t) => DrawMartyrFx(fx, f);  // MartyrLook.cs
            d[(int)FxKind.Drain] = (fx, f, t) => DrawDrainFx(fx, f);
            d[(int)FxKind.DebtCoin] = (fx, f, t) => DrawDebtCoinFx(fx, f);  // BloodDebtLook.cs
            d[(int)FxKind.DebtSettle] = (fx, f, t) => DrawDebtSettleFx(fx, f);  // BloodDebtLook.cs (its stamp is on the HUD)
            d[(int)FxKind.VeinRush] = (fx, f, t) => DrawVeinRushFx(fx, t);  // VeinRushLook.cs
            d[(int)FxKind.GauntletThrow] = (fx, f, t) => DrawGauntletThrowFx(fx, t);  // KnightDpsLook.cs
            d[(int)FxKind.GauntletCharge] = (fx, f, t) => DrawGauntletChargeFx(fx, t);
            d[(int)FxKind.GauntletBowl] = (fx, f, t) => DrawGauntletBowlFx(fx);
            d[(int)FxKind.GauntletImpact] = (fx, f, t) => DrawGauntletImpactFx(fx);
            d[(int)FxKind.GauntletBack] = (fx, f, t) => DrawGauntletBackFx(fx, t);
            d[(int)FxKind.DiveRush] = (fx, f, t) => DrawDiveRushFx(fx, t);  // KnightDpsLook.cs
            d[(int)FxKind.DiveHit] = (fx, f, t) => DrawDiveHitFx(fx);
            d[(int)FxKind.DiveReset] = (fx, f, t) => DrawDiveResetFx(fx, t);
            d[(int)FxKind.BridgeSpark] = (fx, f, t) => DrawBridgeSparkFx(fx);  // DrawbridgeLook.cs
            d[(int)FxKind.BathHeal] = DrawFxBathHeal;
            d[(int)FxKind.DragonBite] = DrawFxDragonBite;
            d[(int)FxKind.Cauterize] = (fx, f, t) => DrawCauterizeFx(fx);  // CauterizeLook.cs
            d[(int)FxKind.BreachSmash] = (fx, f, t) => DrawBreachFx(fx);  // EngineerLook.cs
            d[(int)FxKind.FistShove] = (fx, f, t) => DrawFistShoveFx(fx, f);  // EngineerBotLook.cs
            d[(int)FxKind.RodBolt] = (fx, f, t) => DrawRodBoltFx(fx);  // LightningRodLook.cs
            d[(int)FxKind.RodArc] = (fx, f, t) => DrawRodArcFx(fx);
            d[(int)FxKind.RodGround] = DrawFxRodGround;
            d[(int)FxKind.PhalanxSpark] = (fx, f, t) => DrawPhalanxSparkFx(fx);  // BonePhalanxLook.cs
            d[(int)FxKind.PhalanxBreak] = (fx, f, t) => DrawPhalanxBreakFx(fx);
            d[(int)FxKind.PhalanxJab] = DrawFxPhalanxJab;
            d[(int)FxKind.SteelCatch] = DrawFxSteelCatch;
            d[(int)FxKind.SteelRelease] = DrawFxSteelCatch;
            d[(int)FxKind.SteelHit] = DrawFxSteelCatch;
            d[(int)FxKind.SteelDrop] = DrawFxSteelCatch;
            d[(int)FxKind.VeinCut] = (fx, f, t) => DrawVeinCutFx(fx, t);  // VeinRushLook.cs
            d[(int)FxKind.ThornBloom] = (fx, f, t) => DrawThornBloomFx(fx);  // ThornBloomLook.cs
            d[(int)FxKind.ThornNeedle] = (fx, f, t) => DrawThornNeedleFx(fx);
            d[(int)FxKind.ThornTendril] = (fx, f, t) => DrawThornTendrilFx(fx, t);
            d[(int)FxKind.WallSplash] = (fx, f, t) => DrawWallSplashFx(fx, f);  // BloodWallLook.cs
            d[(int)FxKind.Link] = DrawFxLink;
            d[(int)FxKind.Callout] = (fx, f, t) => DrawTankCastFx(fx);  // Roar and Bulldoze's floor looks (TankLooks.cs); the name over the caster is on the HUD (AbilityLooks.cs)
            d[(int)FxKind.Repulse] = (fx, f, t) => DrawRepulseFx(fx, f);
            d[(int)FxKind.Knock] = (fx, f, t) => DrawKnockFx(fx);
            d[(int)FxKind.RootLash] = (fx, f, t) => DrawRootLashFx(fx, f);  // RootLook.cs
            d[(int)FxKind.Strike] = (fx, f, t) => DrawStrikeFx(fx, f);  // NimbusLook.cs
            d[(int)FxKind.NimbusStrike] = (fx, f, t) => DrawNimbusStrikeFx(fx);  // NimbusLook.cs
            d[(int)FxKind.NimbusStorm] = DrawFxNimbusStorm;
            d[(int)FxKind.Lifeline] = (fx, f, t) => DrawLifelineFx(fx);  // LifelineLook.cs
            d[(int)FxKind.Meteor] = (fx, f, t) => DrawMeteorFx(fx);  // ElementalistLooks.cs
            d[(int)FxKind.Tsunami] = (fx, f, t) => DrawTsunamiFx(fx, t);  // ElementalistLooks.cs
            d[(int)FxKind.DeathLotus] = (fx, f, t) => DrawDeathLotusFx(fx);  // DeathLotusLook.cs
            d[(int)FxKind.CereBlock] = (fx, f, t) => DrawCereBlockFx(fx);  // CeremonialArmorLook.cs
            d[(int)FxKind.CereBreak] = (fx, f, t) => DrawCereBreakFx(fx);
            d[(int)FxKind.RiotBlock] = (fx, f, t) => DrawRiotBlockFx(fx);  // RiotGearLook.cs
            d[(int)FxKind.ShadowStep] = (fx, f, t) => DrawShadowStepFx(fx);  // ShadowStepLook.cs
            d[(int)FxKind.LeapSlam] = (fx, f, t) => DrawLeapSlamFx(fx);  // LeapSlamLook.cs
            d[(int)FxKind.LightCone] = (fx, f, t) => DrawLightConeFx(fx, f, t);
            d[(int)FxKind.MarrowLink] = (fx, f, t) => DrawMarrowLinkFx(fx, f, t);  // BoneGuardLook.cs
            d[(int)FxKind.BloodCoat] = (fx, f, t) => DrawBloodCoatFx(fx, f, t);  // HemomancerLook.cs
            d[(int)FxKind.ArcTag] = (fx, f, t) => DrawArcLockFlatFx(fx);  // Arc Lightning's lock-on chain with Visual effects off (ArcLockLook.cs)
            d[(int)FxKind.ArcLink] = (fx, f, t) => DrawArcLockFlatFx(fx);  // Arc Lightning's lock-on chain with Visual effects off (ArcLockLook.cs)
            d[(int)FxKind.ArcFork] = (fx, f, t) => DrawArcLockFlatFx(fx);  // Arc Lightning's lock-on chain with Visual effects off (ArcLockLook.cs)
            d[(int)FxKind.ChainThrow] = (fx, f, t) => DrawChainThrowFx(fx, f);
            d[(int)FxKind.IonTrail] = (fx, f, t) => DrawIonTrailFx(fx);
            d[(int)FxKind.RailBeam] = (fx, f, t) => DrawRailBeamFx(fx);  // RailBeamLook.cs
            d[(int)FxKind.HeatDump] = (fx, f, t) => DrawHeatDumpFx(fx);
            d[(int)FxKind.SteelWall] = (fx, f, t) => DrawSteelWallFx(fx, f);
            d[(int)FxKind.GreatRend] = (fx, f, t) => DrawGreatRendFx(fx, f);  // GreatswordLook.cs
            d[(int)FxKind.TwinStep] = (fx, f, t) => DrawTwinStepFx(fx);  // TwinBladesLook.cs
            d[(int)FxKind.NunStep] = (fx, f, t) => DrawNunStepFx(fx);  // the Twin Crush's shockwave (NunchakuLook.cs)
            d[(int)FxKind.NodStep] = (fx, f, t) => DrawNodStepFx(fx);
            d[(int)FxKind.VgStep] = (fx, f, t) => DrawVgStepFx(fx);  // the Vowguard's cuts, lanes, pulses (VowguardLook.cs)
            d[(int)FxKind.VgDome] = (fx, f, t) => DrawVgDomeFx(fx);  // an Aegis Dome  // the Nodachi's crescents, moon, shockwave and jabs (NodachiLook.cs)
            d[(int)FxKind.NunCounter] = (fx, f, t) => DrawNunCounterFx(fx);  // NunchakuLook.cs
            d[(int)FxKind.KnuckleStep] = (fx, f, t) => DrawKnuckleStepFx(fx);  // ArcKnucklesLook.cs
            d[(int)FxKind.KnuckleDash] = (fx, f, t) => DrawKnuckleDashFx(fx);  // ArcKnucklesLook.cs
            d[(int)FxKind.KnuckleChain] = (fx, f, t) => DrawKnuckleChainFx(fx);  // ArcKnucklesLook.cs
            d[(int)FxKind.LightStep] = (fx, f, t) => DrawLightStepFx(fx);  // LightbringerLook.cs
            d[(int)FxKind.Sunlance] = (fx, f, t) => DrawSunlanceFx(fx);
            d[(int)FxKind.LightMark] = DrawFxLightMark;
            d[(int)FxKind.TwinFang] = (fx, f, t) => DrawTwinFangFx(fx);
            d[(int)FxKind.LanceStep] = (fx, f, t) => DrawLanceStepFx(fx);  // LanceLook.cs
            d[(int)FxKind.LanceHook] = (fx, f, t) => DrawLanceHookFx(fx);
            d[(int)FxKind.LanceSpark] = (fx, f, t) => DrawLanceSparkFx(fx);
            d[(int)FxKind.LanceImpact] = (fx, f, t) => DrawLanceImpactFx(fx);
            return d;
        }

        void DrawFxBoom(Effect fx, float f, float t)
        {
            if (HammerBoomDrawnByMelee(fx))
                return;  // the Warhammer's own slam draws its impact (MeleeLooks.cs)
            if (IsGrenadeBoom(fx))
            {
                DrawFragBlast(fx, f);  // SoldierLooks.cs
                return;
            }
            if (IsFlakBoom(fx))
            {
                DrawFlakBlast(fx, f);  // TankGunLooks.cs
                return;
            }
            if (IsGlBoom(fx))
            {
                DrawGlBlast(fx, f);  // GrenadeLauncherLook.cs
                return;
            }
            Circle(fx.Pos, fx.Radius * (0.4f + 0.6f * f), fx.Color.WithAlpha(0.35f * (1f - f)), 24);
            Arc(fx.Pos, fx.Radius * (0.5f + f), 3f, fx.Color.WithAlpha(1f - f), 32);
        }

        void DrawFxRing(Effect fx, float f, float t)
        {
            if (IsMendPulse(fx))
            {
                DrawMendPulseFx(fx, f);  // MendPulseLook.cs
                return;
            }
            if (IsFrostNova(fx))
            {
                return;  // drawn on the floor, under everything: DrawFrostNovaGround (FrostNovaLook.cs)
            }
            if (IsGlacialTomb(fx))
            {
                DrawGlacialTomb(fx);  // GlacialTombLook.cs
                return;
            }
            if (IsLifelinePlain(fx))
                return;  // Lifeline's plain ring: its look draws from FxKind.Lifeline (LifelineLook.cs)
            if (IsShadowStepPlain(fx))
                return;  // the Shadow Step's plain ring: its look draws from FxKind.ShadowStep (ShadowStepLook.cs)
            if (IsTankRing(fx))
                return;  // Roar's and Bulldoze's plain rings: their looks draw from the cast's Callout (TankLooks.cs)
            if (IsKusariRing(fx))
                return;  // the Kusarigama's bite and snap: drawn as ink splashes in the glow pass (KusarigamaLook.cs)
            Arc(fx.Pos, fx.Radius * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
        }

        void DrawFxTarget(Effect fx, float f, float t)
        {
            Circle(fx.Pos, fx.Radius, fx.Color.WithAlpha(0.08f + 0.12f * f), 32);
            Arc(fx.Pos, fx.Radius, 2f, fx.Color.WithAlpha(0.5f), 32);
            Arc(fx.Pos, fx.Radius * (1.8f - 0.8f * f), 2f, fx.Color.WithAlpha(0.3f + 0.5f * f), 32);
        }

        void DrawFxSlash(Effect fx, float f, float t)
        {
            if (IsShadowStepPlain(fx))
                return;  // the Shadow Step's strike is drawn by its look
            DrawSlashFx(fx, f, t);
        }

        void DrawFxClaw(Effect fx, float f, float t)
        {
            if (fxOn && IsBearClaw(fx))
                DrawBearClawFx(fx);  // BearLooks.cs
            else
                DrawClawFx(fx, f);
        }

        void DrawFxHammerThrow(Effect fx, float f, float t)
        {
            return;  // a pose and a glow, nothing solid (StormHammerLook.cs)
        }

        void DrawFxKatanaHop(Effect fx, float f, float t)
        {
            return;  // all light (KatanaLook.cs DrawKatanaGlow); the Burst's cracks are DrawKatanaMarks
        }

        void DrawFxStaffShove(Effect fx, float f, float t)
        {
            if (!fxOn || !DrawStaffShoveLayer(fx, false))
                DrawStaffShovePlain(fx, f);  // BoStaffLook.cs
        }

        void DrawFxCoverHit(Effect fx, float f, float t)
        {
            if (fxOn)
                DrawCoverHitFx(fx);  // DeployableCoverLook.cs
        }

        void DrawFxCoverBreak(Effect fx, float f, float t)
        {
            if (fxOn)
                DrawCoverBreakFx(fx);  // DeployableCoverLook.cs
        }

        void DrawFxBathHeal(Effect fx, float f, float t)
        {
            return;  // a Bloodbath tick's heal: a number on the HUD (BloodbathLook.cs DrawBathPops)
        }

        void DrawFxDragonBite(Effect fx, float f, float t)
        {
            return;  // drawn with its dragon (DragonsDogmaLook.cs)
        }

        void DrawFxRodGround(Effect fx, float f, float t)
        {
            return;  // its grey number is on the HUD (LightningRodLook.cs DrawRodPops)
        }

        void DrawFxPhalanxJab(Effect fx, float f, float t)
        {
            return;  // drawn as the skeleton's spear thrust (BonePhalanxLook.cs DrawPhalanx)
        }

        void DrawFxSteelCatch(Effect fx, float f, float t)
        {
            return;  // drawn with the knight's shards (KnightDpsLook.cs DrawSteel)
        }

        void DrawFxLink(Effect fx, float f, float t)
        {
            if (!IsLifelinePlain(fx))  // (Lifeline's own beams: LifelineLook.cs)
                DrawLinkFx(fx, f);  // AbilityLooks.cs
        }

        void DrawFxNimbusStorm(Effect fx, float f, float t)
        {
            return;  // drawn with its cloud (NimbusLook.cs DrawNimbus)
        }

        void DrawFxLightMark(Effect fx, float f, float t)
        {
            if (!Juicy)
                DrawLightMark(fx);  // LightbringerFxLook.cs (in the glow pass when Visual effects are on)
        }

        /// A parry shield breaking: cracks flash across it, then it bursts into jagged pieces with sparks.
        /// The pieces come from a generator seeded by where it broke, so everyone sees the same burst.
        /// The Repulsion Cannon's blast: a cone of air that flashes, then pressure waves rolling out to its reach,
        /// with streaks flying along it. Pos is the gun, Pos2 the reach along the aim, Radius half the cone.
        static readonly Rgba KnockDust = new Rgba(0.78f, 0.72f, 0.62f);

        /// A push sliding (Knock.cs): Pos where it was hit, Pos2 where it ends up, Radius the enemy's size. An impact
        /// puff where it was hit, a motion streak behind the enemy as it slides (following the same path and timing as
        /// the host's TickKnock), and puffs of dust kicked up along the way that linger and fade.
        void DrawKnockFx(Effect fx)
        {
            V2 path = fx.Pos2 - fx.Pos;
            float len = path.Length;
            if (len < 1f)
                return;
            V2 dir = path / len, side = dir.Orthogonal();
            float r = Mathf.Max(6f, fx.Radius), slide = Game.KnockTime;
            float head = Mathf.Clamp01(fx.T / slide), tail = Mathf.Clamp01((fx.T - 0.1f) / slide);
            float fade = 1f - Mathf.Clamp01((fx.T - slide) / (fx.Dur - slide));
            // Impact: a quick ring and a flash on the side it was hit from.
            if (fx.T < 0.18f)
            {
                float k = fx.T / 0.18f;
                Arc(fx.Pos, r + 4f + 16f * k, 3f * (1f - k) + 1f, Rgba.White.WithAlpha(0.7f * (1f - k)), 18);
                Circle(fx.Pos - dir * r * 0.6f, r * 0.55f * (1f - k), Rgba.White.WithAlpha(0.45f * (1f - k)), 10);
            }
            // The streak: a band from tail to head, wide at the head, in the enemy's colour going pale.
            V2 h = fx.Pos + path * head, tl = fx.Pos + path * tail;
            if ((h - tl).Length > 1f && fade > 0f)
            {
                var col = fx.Color.Mix(Rgba.White, 0.5f);
                SetColor(col.WithAlpha(0.32f * fade));
                Tri(tl, h + side * r * 0.8f, h - side * r * 0.8f);
                for (int i = -1; i <= 1; i += 2)  // two thin speed lines at its edges
                    Line(tl + side * (i * r * 0.35f), h + side * (i * r * 0.7f), 1.5f, Rgba.White.WithAlpha(0.35f * fade));
            }
            // Dust kicked up behind it: each puff starts when the enemy passes, grows and fades.
            for (int i = 0; i < 4; i++)
            {
                float at = 0.15f + 0.22f * i;
                if (head < at)
                    break;
                float age = fx.T - at * slide, life = fx.Dur - at * slide;
                float k = Mathf.Clamp01(age / life);
                if (k >= 1f)
                    continue;
                V2 c = fx.Pos + path * at + side * ((i % 2 == 0 ? 1f : -1f) * r * 0.5f) - dir * (4f * k);
                Circle(c, r * (0.35f + 0.5f * k), KnockDust.WithAlpha(0.3f * (1f - k)), 10);
            }
        }

        void DrawRepulseFx(Effect fx, float f)
        {
            if (fxOn)
            {
                DrawRepulseBlast(fx, f);  // the cannon blast (RepulseLook.cs); with Visual effects off, the plain cone below
                return;
            }
            f = Mathf.Min(1f, f * (Game.RepulseFxTime / 0.35f));  // (the effect lives longer now, for the smoke; this look keeps its timing)
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, reach = mid.Length, half = fx.Radius;
            var col = fx.Color;
            if (f < 0.25f)  // the cone flashes
            {
                float k = 1f - f / 0.25f;
                SetColor(col.WithAlpha(0.22f * k));
                for (int i = 0; i < 10; i++)
                {
                    float a0 = aim - half + 2f * half * i / 10f, a1 = aim - half + 2f * half * (i + 1) / 10f;
                    Tri(c, c + V2.FromAngle(a0) * reach, c + V2.FromAngle(a1) * reach);
                }
            }
            for (int k = 0; k < 3; k++)  // pressure waves
            {
                float g = Mathf.Clamp01(f * 1.6f - k * 0.18f);
                if (g <= 0f || g >= 1f)
                    continue;
                Arc(c, 20f + (reach - 20f) * g, 5f * (1f - g) + 1.5f, col.Mix(Rgba.White, 0.4f).WithAlpha(0.85f * (1f - g)), 14, aim - half, 2f * half);
            }
            for (int i = 0; i < 7; i++)  // streaks
            {
                float a = aim - half * 0.85f + 1.7f * half * i / 6f, d0 = reach * (0.2f + 0.8f * f), d1 = d0 + 30f;
                Line(c + V2.FromAngle(a) * d0, c + V2.FromAngle(a) * Mathf.Min(reach, d1), 2f, Rgba.White.WithAlpha(0.6f * (1f - f)));
            }
        }

        /// A small repeatable random number in [0, 1) from seed (xorshift), advancing it. The effects that crackle the same
        /// way each frame use it (no closure allocated per effect per frame).
        static float XorRand(ref uint seed)
        {
            seed ^= seed << 13;
            seed ^= seed >> 17;
            seed ^= seed << 5;
            return (seed & 0xFFFFFF) / 16777216f;
        }
        void DrawShatterFx(Effect fx, float f)
        {
            V2 c = fx.Pos, mid = fx.Pos2 - fx.Pos;
            float aim = mid.Angle, rr = mid.Length, half = fx.Radius, from = aim - half, span = half * 2f;
            float r0 = rr - 7f, r1 = rr + 9f;
            uint seed = (uint)((int)fx.Pos.X * 73856093 ^ (int)fx.Pos.Y * 19349663) | 1u;
            const float crackEnd = 0.18f;
            if (f < crackEnd)  // the shield whitens and cracks run across it
            {
                float k = f / crackEnd;
                Arc(c, rr, 14f, fx.Color.Mix(Rgba.White, 0.5f).WithAlpha(0.6f), 20, from, span);
                Arc(c, rr + 7f, 4f, Rgba.White.WithAlpha(0.95f), 20, from, span);
                for (int n = 0; n < 5; n++)
                {
                    float a = from + span * (0.1f + 0.8f * XorRand(ref seed)), rad = r1 + 1f;
                    V2 prev = c + V2.FromAngle(a) * rad;
                    for (int s = 1; s <= 3; s++)
                    {
                        rad = r1 + 1f - (r1 - r0 + 2f) * s / 3f * Mathf.Min(1f, k * 1.4f);
                        a += (XorRand(ref seed) - 0.5f) * 0.25f;
                        V2 next = c + V2.FromAngle(a) * rad;
                        Line(prev, next, 1.6f, Rgba.White.WithAlpha(0.6f + 0.4f * k));
                        prev = next;
                    }
                }
                return;
            }
            float g = (f - crackEnd) / (1f - crackEnd);
            float e = 1f - (float)Math.Pow(1f - g, 2.6f), fade = Mathf.Max(0f, 1f - (float)Math.Pow(g, 1.4f));
            Circle(c + V2.FromAngle(aim) * rr, (r1 - r0) * (0.4f + 0.5f * e), new Rgba(0.67f, 0.9f, 1f, 0.3f * (1f - g) * (1f - g)), 20);  // burst glow
            var bright = fx.Color.Mix(Rgba.White, 0.7f).WithAlpha(fade);
            for (int n = 0; n < 10; n++)  // sparks
            {
                V2 d = V2.FromAngle(aim + (XorRand(ref seed) - 0.5f) * span * 1.3f);
                V2 tip = c + d * (rr + (40f + 50f * XorRand(ref seed)) * e);
                Line(tip - d * 7f * (1f - g), tip, 2f, bright);
            }
            const int pieces = 9;
            var edges = new float[pieces + 1];
            for (int i = 1; i < pieces; i++)
                edges[i] = (i + (XorRand(ref seed) - 0.5f) * 0.6f) / pieces;
            edges[pieces] = 1f;
            for (int i = 0; i < pieces; i++)
            {
                float a0 = from + span * edges[i] + 0.02f, a1 = from + span * edges[i + 1] - 0.02f;
                float speed = 40f + 25f * XorRand(ref seed), spin = (XorRand(ref seed) - 0.5f) * 5f;
                V2 off = V2.FromAngle((a0 + a1) / 2f + (XorRand(ref seed) - 0.5f) * 0.4f) * (speed * e);
                DrawShieldPiece(c, a0, a1, r0 + 2f * XorRand(ref seed), r1 - 2f * XorRand(ref seed), off, spin * e, (a1 - a0) * 0.25f, fx.Color.WithAlpha(0.55f * fade), bright);
            }
        }

        /// One curved piece of a broken shield (angles a0..a1, radii r0..r1, pointed sides), moved by off and
        /// turned by rot around its own middle.
        void DrawShieldPiece(V2 c, float a0, float a1, float r0, float r1, V2 off, float rot, float jag, Rgba fill, Rgba edge)
        {
            const int n = 4;
            float rm = (r0 + r1) / 2f;
            V2 mid = c + V2.FromAngle((a0 + a1) / 2f) * rm;
            var pts = new V2[2 * n + 4];
            int m = 0;
            for (int i = 0; i <= n; i++)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r1;
            pts[m++] = c + V2.FromAngle(a1 + jag) * rm;
            for (int i = n; i >= 0; i--)
                pts[m++] = c + V2.FromAngle(a0 + (a1 - a0) * i / n) * r0;
            pts[m++] = c + V2.FromAngle(a0 - jag) * rm;
            float cs = Mathf.Cos(rot), sn = Mathf.Sin(rot);
            for (int i = 0; i < m; i++)
            {
                V2 d = pts[i] - mid;
                pts[i] = mid + off + new V2(d.X * cs - d.Y * sn, d.X * sn + d.Y * cs);
            }
            SetColor(fill);
            for (int i = 0; i < m; i++)
                Tri(mid + off, pts[i], pts[(i + 1) % m]);
            for (int i = 0; i < n; i++)  // bright outer edge
                Line(pts[i], pts[i + 1], 2.5f, edge);
        }

        /// A melee swing: a bright crescent sweeping across the arc, fading out (a jab or thrust is a streak).
        void DrawSlashFx(Effect fx, float f, float t)
        {
            if (DrawSlashE(fx, t))
                return;  // a player's melee swing with Visual effects on: the weapon + energy look (MeleeLooks.cs)
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return;
            if (half < 0f)  // a jab: the blade shoots forward, with speed lines along the cone
            {
                float grow = Mathf.Min(1f, f * 4f), len = reach * (0.3f + 0.7f * grow), fade = 1f - f;
                V2.Axes(aim, out V2 dir, out V2 side);
                V2 tipAt = fx.Pos + dir * len;
                for (int k = -1; k <= 1; k += 2)  // the cone it covers, faintly
                    Line(fx.Pos, fx.Pos + V2.FromAngle(aim + k * -half) * len * 0.9f, 1.5f, fx.Color.WithAlpha(0.25f * fade));
                for (int k = 0; k < 4; k++)  // speed lines
                {
                    float o = (k % 2 == 0 ? 1f : -1f) * (0.35f + 0.15f * k) * (float)Math.Tan(-half) * len * 0.5f;
                    V2 a0 = fx.Pos + dir * len * (0.35f + 0.12f * k) + side * o;
                    Line(a0, a0 + dir * len * 0.25f, 1.5f, Rgba.White.WithAlpha(0.5f * fade));
                }
                Line(fx.Pos, tipAt, 10f * fade + 2f, fx.Color.WithAlpha(0.35f * fade));
                Line(fx.Pos, tipAt - dir * 6f, 3f, Rgba.White.WithAlpha(0.9f * fade));
                SetColor(Rgba.White.WithAlpha(0.95f * fade));
                Tri(tipAt + dir * 8f, tipAt - dir * 4f + side * 6f, tipAt - dir * 4f - side * 6f);
                return;
            }
            if (half < 0.2f)  // a thrust or a lunge: a streak along the line
            {
                Line(fx.Pos, fx.Pos2, 8f * (1f - f), fx.Color.WithAlpha(0.35f * (1f - f)));
                Line(fx.Pos, fx.Pos2, 2.5f, Rgba.White.WithAlpha(0.9f * (1f - f)));
                return;
            }
            float sweep = 2f * half * Mathf.Min(1f, f * 3f);
            float from = half >= MathX.Pi - 0.01f ? aim + t * 6f : aim - half;
            Arc(fx.Pos, reach * 0.78f, reach * 0.4f, fx.Color.WithAlpha(0.28f * (1f - f)), 24, from, sweep);
            Arc(fx.Pos, reach, 3f, Rgba.White.WithAlpha(0.9f * (1f - f)), 24, from, sweep);
        }

        /// Bear Totem: three claw marks rake across the arc (the sign of Radius says which way), over a faint
        /// crescent; they draw on quickly, then fade.
        void DrawClawFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = Mathf.Abs(fx.Radius), side = fx.Radius >= 0f ? 1f : -1f;
            if (reach < 1f)
                return;
            float g = Mathf.Min(1f, f * 4f), fade = 1f - f, sweep = 2f * half * g;
            float from = side > 0f ? aim - half : aim + half - sweep;  // rakes from one side to the other
            Arc(fx.Pos, reach * 0.72f, reach * 0.5f, new Rgba(0.9f, 0.3f, 0.25f, 0.16f * fade), 20, from, sweep);
            for (int k = -1; k <= 1; k++)
            {
                float r = reach * (0.78f + 0.13f * k);
                float lag = (1 - k * side) * 0.06f;  // the marks are staggered a little, like a paw's claws
                float a0 = from + (side > 0f ? 0f : sweep * lag), a1 = sweep * (1f - lag);
                Arc(fx.Pos, r, 4.5f * fade + 1f, fx.Color.WithAlpha(0.9f * fade), 14, a0, a1);
                Arc(fx.Pos, r, 1.5f, new Rgba(0.85f, 0.15f, 0.15f, 0.7f * fade), 14, a0, a1);  // a red core
            }
        }

        /// The Lance: its rectangular hitbox slides out from the Knight, lance tip leading.
        /// A Greatsword's crescent of steel hanging where its sweep's edge passed, fading as it goes.
        void DrawSteelWallFx(Effect fx, float f)
        {
            // Moonlit Crescent: a glassy arc that appears as the sweep passes, with a glint that runs along it, and a ripple
            // wherever a bullet died on it (the host's spark for it is the cue).
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius, fade = 1f - f * f, T = fx.T;
            if (reach < 1f)
                return;
            float sg = GreatWallSign(fx, fx.Pos);  // the way its cut swept (GreatswordLook.cs)
            float run0 = 2f * half * EaseOut3(Mathf.Clamp01(T / MeleeSweepTime));
            var moonCol = new Rgba(0.78f, 0.88f, 1f);
            if (run0 <= 0.01f)
                return;
            float from = aim - sg * half;
            DrawGreatCrescent(fx.Pos, from, from + sg * run0, reach - 26f, reach + 2f, 0.75f * Mathf.Sqrt(Mathf.Max(0f, fade)), moonCol, false);  // the mockup's hanging moon (GreatswordLook.cs)
            float gl = Mathf.Clamp01((T - MeleeSweepTime) / (fx.Dur > MeleeSweepTime ? 0.84f * (fx.Dur - MeleeSweepTime) : 0.42f));
            if (gl > 0f && gl < 1f)
            {
                float ga = from + sg * 2f * half * gl;
                GlowDisc(fx.Pos + V2.FromAngle(ga) * reach, 26f, Rgba.White, 0.7f * fade);
                Arc(fx.Pos, reach, 2.4f, Rgba.White.WithAlpha(0.95f * fade), 6, ga - 0.08f, 0.16f);
            }
            var hitCol = Defs.BulletColor(Defs.KnightSlash);
            foreach (var sp in game.Effects)
            {
                if (sp.Kind != FxKind.Spark || sp.Radius != 8f || sp.Color.R != hitCol.R || sp.Color.G != hitCol.G || sp.Color.B != hitCol.B)
                    continue;
                V2 rel = sp.Pos - fx.Pos;
                float d = rel.Length, an = rel.Angle, diff = Mathf.Repeat(an - aim + MathX.Pi, MathX.Tau) - MathX.Pi;
                if (Mathf.Abs(d - reach) > Game.SteelWallBand + 10f || Mathf.Abs(diff) > half + 0.1f)
                    continue;
                float q = Mathf.Clamp01(sp.T / 0.4f);
                if (q >= 1f)
                    continue;
                GlowDisc(fx.Pos + V2.FromAngle(an) * reach, 22f * (1f - q * 0.4f), Rgba.White, 0.7f * (1f - q) * fade);  // the arc brightens where it was hit
                float run = 0.5f * EaseOut3(q);
                Arc(fx.Pos, reach, 2f, Rgba.White.WithAlpha(0.5f * (1f - q) * fade), 12, an - run, 2f * run);  // and a ripple runs along it
            }
        }

        /// A Railgun's ion trail: the stretch the round has passed through in the last second glows and crackles
        /// (the same stretch the host hurts enemies in: KitWeapons.cs TickIonTrails).
        void DrawIonTrailFx(Effect fx)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float len = to.Length, speed = fx.Radius;
            if (len < 1f || speed <= 0f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            float head = Mathf.Min(len, speed * fx.T), tail = Mathf.Max(0f, speed * (fx.T - Game.IonTrailTime));
            if (tail >= head)
                return;
            V2 a = fx.Pos + dir * tail, b = fx.Pos + dir * head;
            Line(a, b, Game.IonTrailWidth * 2f, fx.Color.WithAlpha(0.14f));
            Line(a, b, 2f, fx.Color.WithAlpha(0.7f));
            int n = Math.Max(2, (int)((head - tail) / 30f));
            for (int i = 0; i < n; i++)  // sparks jittering along it
            {
                float along = tail + (head - tail) * ((i + 0.5f) / n);
                float j = Mathf.Sin(fx.T * 47f + i * 2.3f) * Game.IonTrailWidth;
                V2 at = fx.Pos + dir * along;
                Line(at, at + side * j + dir * 6f, 1.2f, Rgba.White.WithAlpha(0.6f));
            }
        }

        /// A chain of links from `a` to `b`, with the sickle's blade at `b`.
        void DrawChain(V2 a, V2 b, float alpha)
        {
            V2 to = b - a;
            float len = to.Length;
            if (len < 1f)
                return;
            V2 dir = to / len, side = dir.Orthogonal();
            int n = Math.Max(2, (int)(len / 9f));
            for (int i = 0; i <= n; i++)
            {
                V2 at = a + to * ((float)i / n);
                Circle(at, i % 2 == 0 ? 2.2f : 1.6f, Steel.WithAlpha((i % 2 == 0 ? 0.95f : 0.7f) * alpha), 6);
            }
            Arc(b - dir * 4f, 7f, 2.5f, Rgba.White.WithAlpha(0.9f * alpha), 8, dir.Angle - MathX.Pi / 2f, MathX.Pi);  // the blade, curving round
            Line(b - dir * 6f, b - dir * 1f + side * 6f, 2f, Steel.WithAlpha(alpha));
        }

        /// The old Kusarigama's throw (the host no longer sends it: the throw is drawn from the player's state, KusarigamaLook.cs).
        void DrawChainThrowFx(Effect fx, float f)
        {
            float run = Mathf.Min(1f, f / 0.35f), fade = fx.Radius > 0f ? 1f : 1f - Mathf.Max(0f, (f - 0.35f) / 0.65f);
            DrawChain(fx.Pos, V2.Lerp(fx.Pos, fx.Pos2, run), fade);
        }

        /// Chain Light's cone: a flash of light fanning out from the caster, brightest at its edges, with rays
        /// running out through it.
        void DrawLightConeFx(Effect fx, float f, float t)
        {
            if (ChainLightDrawnAsRadiance(fx))
                return;  // the radiance, drawn in the glow pass (ChainLightLooks.cs)
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, half = fx.Radius, aim = to.Angle;
            if (reach < 1f)
                return;
            float fade = 1f - f, grow = Mathf.Min(1f, f / 0.15f);
            float r = reach * (0.6f + 0.4f * grow);
            const int segs = 14;
            for (int k = 0; k < segs; k++)  // the cone, filled
            {
                float a0 = aim - half + 2f * half * k / segs, a1 = aim - half + 2f * half * (k + 1) / segs;
                SetColor(fx.Color.WithAlpha(0.22f * fade));
                Tri(fx.Pos, fx.Pos + V2.FromAngle(a0) * r, fx.Pos + V2.FromAngle(a1) * r);
            }
            Line(fx.Pos, fx.Pos + V2.FromAngle(aim - half) * r, 2.5f, Rgba.White.WithAlpha(0.7f * fade));
            Line(fx.Pos, fx.Pos + V2.FromAngle(aim + half) * r, 2.5f, Rgba.White.WithAlpha(0.7f * fade));
            Arc(fx.Pos, r, 3f, fx.Color.WithAlpha(0.8f * fade), segs, aim - half, 2f * half);
            for (int k = 0; k < 5; k++)  // rays
            {
                float a = aim - half * 0.8f + half * 1.6f * k / 4f;
                Line(fx.Pos + V2.FromAngle(a) * (r * 0.15f), fx.Pos + V2.FromAngle(a) * (r * (0.5f + 0.45f * f)), 1.5f, Rgba.White.WithAlpha(0.45f * fade));
            }
        }

        void DrawJabFx(Effect fx, float f)
        {
            if (DrawJabE(fx, Time.unscaledTime))
                return;  // the Lance or the Warhammer with Visual effects on (MeleeLooks.cs)
            if (fx.Color.R == Game.HammerBoomColor.R && fx.Color.G == Game.HammerBoomColor.G && fx.Color.B == Game.HammerBoomColor.B)
                return;  // the Warhammer's slam is a circle: with the melee look off, its blast ring (a Boom beside it) shows it
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, w = fx.Radius;
            if (reach < 1f)
                return;
            V2 dir = to / reach, side = dir.Orthogonal();
            float g = Mathf.Min(1f, f / 0.35f);
            g = 1f - (1f - g) * (1f - g) * (1f - g);  // fast out, easing in to full reach
            float fade = f < 0.35f ? 1f : 1f - (f - 0.35f) / 0.65f;
            float front = reach * g;
            // The box, from your body to its sliding front edge.
            V2 a0 = fx.Pos + side * w, a1 = fx.Pos - side * w, b0 = a0 + dir * front, b1 = a1 + dir * front;
            SetColor(fx.Color.WithAlpha(0.2f * fade));
            Tri(a0, b0, b1);
            Tri(a0, b1, a1);
            Line(a0, b0, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(a1, b1, 1.5f, fx.Color.WithAlpha(0.6f * fade));
            Line(b0, b1, 4f, Rgba.White.WithAlpha(0.9f * fade));  // the leading edge
            // Speed streaks riding along inside it.
            for (int k = 0; k < 4; k++)
            {
                float o = (k - 1.5f) / 1.5f * w * 0.7f, at = front * (0.25f + 0.15f * (k % 2));
                V2 s0 = fx.Pos + dir * at + side * o;
                Line(s0, s0 + dir * front * 0.35f, 1.5f, Rgba.White.WithAlpha(0.45f * fade));
            }
            // The lance itself, slid out with its tip at the front edge.
            V2 tipAt = fx.Pos + dir * front, tail = fx.Pos + dir * Mathf.Max(0f, front - reach * 0.8f);
            Line(tail, tipAt - dir * 10f, 4f, Rgba.White.WithAlpha(0.85f * fade));
            SetColor(Rgba.White.WithAlpha(0.95f * fade));
            Tri(tipAt + dir * 6f, tipAt - dir * 14f + side * 7f, tipAt - dir * 14f - side * 7f);
        }

        /// Jagged lightning between two points.
        void DrawBoltFx(Effect fx, float f)
        {
            if (ChainLightDrawnAsRadiance(fx))
                return;  // Chain Light's beam, drawn in the glow pass (ChainLightLooks.cs)
            if (DrawJuicyBolt(fx, f))
                return;  // flickering, with a white-hot core (ShotLooks.cs)
            V2 a = fx.Pos, b = fx.Pos2, n = (b - a).Normalized().Orthogonal();
            V2 prev = a;
            for (int k = 1; k <= 6; k++)
            {
                V2 pt = k == 6 ? b : V2.Lerp(a, b, k / 6f) + n * (((k * 37 + fx.Pos.X) % 17f) - 8.5f);
                Line(prev, pt, 3f, fx.Color.WithAlpha(1f - f));
                prev = pt;
            }
        }

        bool ImCaged()
        {
            foreach (var c in game.Cages.Values)
                if (c.PlayerId == game.MyId)
                    return true;
            return false;
        }

        bool AnyWells()
        {
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.GravityWell)
                    return true;
            return false;
        }
    }
}

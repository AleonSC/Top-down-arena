using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    // The match's data types: states, effects and sounds, and the Player / Enemy / Bullet / Orb / Zone / Minion /
    // Effect classes the Game holds.
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver, Victory }

    public enum FxKind : byte { Boom, Spark, Ring, Bolt, Target, Slash, Jab, Shatter, Claw, Implode, Repulse, Icicle, Splinter, LightCone, BloodCoat, ArcField, ChainThrow, IonTrail, SteelWall, Debris, Callout, Bleed, BloodShell, Drain, DebtCoin, WallSplash, Link, Knock, RootLash, Strike, Tsunami, Meteor, Lifeline, RiotBlock, DeathLotus, ShadowStep, LeapSlam, ShellHit, ShellPop, HolyCross, KatanaStrike, StandHit, CavalryCall, KatanaCut, HarvestThrow, StaffShove , TitanSlam, RainRelease, TitheSnap, PactBind, PactSnap, PactDissolve, PactRise, BurrowSnap, KatanaHitCue, GreatRend, MartyrCast, BloodFreeze, TwinStep, TwinFang, TwinHit, HammerThrow, HammerSlam, LightStep, Sunlance, DebtSettle, VeinRush, VeinCut, CenserStep, CenserCast, Exsanguinate, LightMark, LanceStep, LanceHook, LanceHit, LanceSpark, LanceImpact, Airstrike, CaltropHit, CaltropRun, SteelHit, CaltropBreak, GauntletThrow, GauntletCharge, GauntletBowl, GauntletImpact, GauntletBack, DiveRush, DiveHit, DiveReset, BridgeSpark, JuggShard, BathHeal , RodBolt, RodArc, RodGround, PhalanxJab, PhalanxSpark, PhalanxBreak, VenomDart, VenomSpread, PerfectParry, KatanaStanceFx, KatanaHop, KatanaDash, KatanaSweep, KatanaSlash, KatanaBurst, TaserHit, BossBeam, DoubleBarrel, KnuckleStep, KnuckleDash, KnuckleChain, KnuckleHit, GasLob, PortalSeal, Shockwave, Sentry, Nova, MarrowLink, StoneSlam, ArcTag, ArcLink, ArcFork, NimbusStrike, NimbusStorm, BeeSting, HoneyHarvest, NunStep, NunCounter, WindWave, WindCatch, EmbolusBlast, BulwarkStep, BulwarkBlock, BulwarkBash, PistolWhip, GaleCatch, GaleBlast, WwSplash, WwBurst, CoverHit, CoverBreak, CoverFold, DragonBite, CereBlock, CereBreak, WyvernSlam, Cauterize, BulwarkThrow, BreachSmash, FrostStep, FrostBurst, FrostBreak, AbsoluteZero, BsBlow, BlastCut, BlastTide, BlastReturn, WarhornVolley, FistShove, NodStep, DecoyHit, DecoyPop, DecoyFold, VgStep, VgDome, PitFall, ThornBloom, ThornNeedle, ThornTendril, Graze, RailBeam, HeatDump, ArcBall, ArcBurst, Overcharge, HalberdStep, HalberdPoke, VortexWisp, LivingBolt , BambooGrove, SpiritInk, OrbitalLaser, FlashFire, ClaymoreBlast, RavagerLash, ForbiddenPath, NeedleSwarm, TalismanBurst, TessenStep, TessenPetals, TessenGust, StaticBurst, BoilEnd, BumbleBurst, HiveCall, NeedleBurst, TrojanEnd, GlassMote, GlassShatter, SiegeBell, HeartDrops, HeartPulse, HeartBurst, GolemFx, ChakraThread, CannonCall, RocketBlast }  // CannonCall: one ball of the DPS Knight's Cannon Call, sent at the cast (Pos = where the knight stood, Pos2 = where it lands, Radius = their id * 1000 + the blast radius, Color.A = the landing time / 4 s, Dur = the landing + Game.CcCraterT; Core/CannonCall.cs, Game/CannonCallLook.cs)  // GolemFx: Blood Golem (Radius = the kind: Game.GolFxShed a coat falling off (Pos = the golem, Pos2 = Pos + the way it falls), GolFxCollapse its burst (Pos2 = (coats left, the Hemomancer's id)), GolFxClot a Hurl Clot in flight then its splash (Pos = from, Pos2 = where it lands, Dur = Game.GolClotFlight + GolClotTail); Core/BloodGolem.cs, Game/BloodGolemLook.cs)  // ChakraThread: Chakra Reserves' thread and lotus (Pos = the caster, Pos2 = the teammate, Radius = their id + 0.5 when it refreshed their cooldowns, Dur = Game.CrFxDur; Core/ChakraReserves.cs, Game/ChakraReservesLook.cs)  // GolemFx: Blood Golem (Radius = the kind: Game.GolFxShed a coat falling off (Pos = the golem, Pos2 = Pos + the way it falls), GolFxCollapse its burst (Pos2 = (coats left, the Hemomancer's id)), GolFxClot a Hurl Clot in flight then its splash (Pos = from, Pos2 = where it lands, Dur = Game.GolClotFlight + GolClotTail); Core/BloodGolem.cs, Game/BloodGolemLook.cs)  // TrojanEnd: a Trojan Horse coming down (Pos = the horse, Pos2 = one px along its heading, Radius = 1 it broke / 0 Sally Forth, Dur = the planks' tumble; Core/TrojanHorse.cs, Game/TrojanHorseLook.cs)  // NeedleBurst: Acupuncture Needles coming out of a body at once (Pos = it, Pos2 = (0 Meridian Open on a player / 1 Pressure Point on an enemy, its id), Radius = its size, Color = jade or blood; Core/AcupunctureNeedles.cs, Game/AcupunctureLook.cs)  // BoilEnd: Blood Boil ending, a burst of steam off the Hemomancer (Pos = where, Pos2.X = their id; Core/BloodBoil.cs, Game/BloodBoilLook.cs)  // StaticBurst: a Static Bubble going off (Pos = the caster, Pos2 = (their id, 1 full / 0 a smaller pop), Radius = the blast's reach; Core/StaticBubble.cs, Game/StaticBubbleLook.cs)  // ClaymoreBlast: a Claymore going off (Pos = the mine, Pos2 = one px along its facing, Radius = the blast's reach; Core/Claymore.cs, Game/ClaymoreLook.cs)  // NeedleSwarm: a needle of the DPS Hemomancer's Needle Swarm leaving the ring (Pos = where, Pos2 = (the owner's id, 0 broken by a shot / 1 the Alt's burst), Radius = the shot's size / needles thrown, Color = the shot's colour; Core/NeedleSwarm.cs, Game/NeedleSwarmLook.cs)  // FlashFire: the DPS Elementalist's Flash Fire for its life (Pos = the middle, Pos2 = (the caster's id, the fire's life), Radius = its radius; Core/FlashFire.cs, Game/FlashFireLook.cs)  // OrbitalLaser: the DPS Soldier's Orbital Laser, sent once at the cast (Pos = the point, Pos2 = where the beam ends up, Radius = the caster's id, Color.A = the circle's radius / 1000, Dur = Game.OrbFxDur; Core/OrbitalLaser.cs, Game/OrbitalLaserLook.cs)  // SpiritInk: Spirit Avatar's ink (Pos2.X = Game.SpFxStroke the cleave's stroke (Pos = the Ninja, Pos2.Y = its heading, Radius = its reach), SpFxRise, SpFxRelease (Radius = the burst), SpFxSwallow a shot swallowed (Pos = where, Radius = its size); Pos2.Y = the Ninja's id except for the stroke; Core/SpiritAvatar.cs, Game/SpiritAvatarLook.cs)  // BambooGrove: the Tank Ninja's Bamboo Grove for its life (Pos = the middle, Pos2 = (the caster's id, the fence's life), Radius = its radius; Core/BambooGrove.cs, Game/BambooGroveLook.cs)  // LivingBolt: one Living Bolt dash (Pos = from, Pos2 = to, Radius = the dasher's id, Dur = Game.BoltDashFx; Core/LivingBolt.cs, Game/LivingBoltLook.cs)  // VortexWisp: an enemy shot swept away by a Vortex (Pos = where, Pos2 = the storm's middle, Radius = the shot's size, Color = its colour; Core/Vortex.cs, Game/VortexLook.cs)  // ArcBall: the Arc Cannon's ball rolling from Pos to Pos2 (Radius = the owner's id, Dur = its flight); ArcBurst: its burst (Radius = the burst's size); Overcharge: a teammate charged up (Pos2 = (their id, the giver's id), Radius = their size; Core/StaticShock.cs, Game/StaticShockLook.cs)  // RailBeam: one stretch of a Railgun beam (Pos = from, Pos2 = to, Radius = its width, Color = gold for a perfect shot, A = 0.5 + charge / 2, Dur = the flash or the charged path after it); HeatDump: the Railgun's Heat Dump (Pos = the shooter, Pos2 = the cone's far middle, Radius = the heat dumped; Core/RailBeam.cs, Game/RailBeamLook.cs)  // ThornBloom: the burst of needles out of a Hemomancer (Pos = them, Pos2 = (their id, the aim the ring starts from), Radius = needles); ThornNeedle: one needle (Pos = from, Pos2 = where it stops, Radius = 1 it struck an enemy, Dur = flight + Game.TbNeedleTail); ThornTendril: one jump of a tendril (Pos = from, Pos2 = to, Radius = the jump from 0, Color.A = its start delay / 4 s, Game.TbTendrilDelay; Core/ThornBloom.cs, Game/ThornBloomLook.cs)  // PitFall: a body falling into a pit (Pos = where it fell, Pos2 = the rim it climbed out on, Radius = its size, Color = its colour with A 1 a player (climbs out), 0.5 an enemy (dies); Core/Pits.cs, Game/PitLook.cs)  // VgStep: one Vowguard step, drawn from its age (Pos = the knight, Pos2 = along the aim, Radius = their id * 100 + the VgMove, Dur = the move + Game.VgFxTail); VgDome: an Aegis Dome for its life (Radius = its size; Core/Vowguard.cs, Game/VowguardLook.cs)  // NodStep: one Nodachi step, drawn from its age (Pos = the Ninja, Pos2 = along the aim, Radius = their id * 100 + the code: stance * 8 + step; Core/Nodachi.cs, Game/NodachiLook.cs);  // WarhornVolley: the DPS Knight's Warhorn Volley, sent once at the cast (Pos = where they stood, Pos2 = the lane's far end, Radius = their id, Color.A = the lane's half width / 1000, Dur = Game.VolFxDur; Core/WarhornVolley.cs, Game/WarhornVolleyLook.cs);  // BlastCut / BlastTide / BlastReturn: the Blood Blast's cuts, Crimson Tide and returning blood (fields in Game/BloodBlastLook.cs; Core/BloodBlast.cs);  // BsBlow: a Blood Brawler move on an enemy, sent at the press and drawn from its age (Pos = the enemy, Pos2 = (its id, the Hemomancer's id), Radius = the step 1-4, +10 for a Sympathy echo, Dur = Game.BsFxDur; the blow lands at Game.BsHitDelay; Core/BloodBrawler.cs, Game/BloodBrawlerLook.cs);  // BulwarkThrow: a thrown Bulwark Barrier in flight, drawn from its age (Pos = where it left the hand, Pos2 = its far end, Radius = the thrower's id, Dur = the flight + Game.BbThrowCatchFx (the catch); Core/BulwarkBarrier.cs, Game/BulwarkBarrierLook.cs);  // (the DPS Knight's Caltrop Run, Gauntlet, Misericorde and Drawbridge kinds are described in Core/CaltropRun.cs, Gauntlet.cs, Misericorde.cs, Drawbridge.cs); Target: a marker where something is about to land; Slash: a melee swing (Pos2 = tip, Radius = half the arc); Jab: the Lance's box (Pos2 = far end, Radius = half its width); Shatter: a parry shield breaking (Pos = its owner, Pos2 = the shield's middle, Radius = half its arc); Claw: a bear-claw swipe (Pos2 = reach, Radius = half the arc, signed by the way it rakes); Implode: blood collapsing into an enemy (Implosion); Icicle: the Frost Lance's icicle (Pos2 = its point, Radius = half its width); Splinter: shards off its sides (Pos2 = its point, Radius = how far out they fly); LightCone: Chain Light (Pos2 = the middle of its far edge, Radius = half its angle); BloodCoat: a Sanguine Orb circling in on an enemy (Pos2.X = the enemy's id, Radius = its radius); ArcField: Arc Lightning going off (Pos2 = the far end of its strip, Radius = half its width); ChainThrow: the Kusarigama's chain flying out (Pos2 = where it ends, Radius = 1 when it hooked something); IonTrail: a Railgun shot's charged path (Pos2 = its far end, Radius = the round's speed); SteelWall: a Greatsword's lingering crescent (Pos = the swing's centre, Pos2 = the middle of its edge, Radius = half its arc); Debris: a crate or barrel smashed (Props.cs); Callout: an ability's name over whoever cast it (Pos2 = (player id, AbilityId), drawn on the HUD); Bleed: a Hemomancer paying health for an ability (Pos2.X = their id, Radius = health paid); BloodShell: Blood Shell forming (Pos2.X = the caster's id); Drain: blood flowing from Pos to Pos2 (Exsanguinate, Blood Gift, Martyr; Radius = how thick); DebtCoin: a Blood Debt repayment, blood coins flying from a kill into the debtor's health bar (Pos = the kill, Pos2.X = the debtor's id, Radius = health repaid; Game/BloodDebtLook.cs); WallSplash: an enemy shot stopping on a Blood Wall (Pos = where, Pos2 = back along the shot, Radius = the shot's size; BloodWall.cs); Link: a beam of light from a caster (Pos) to whoever their ability reached (Pos2); Rewind: Time Rewind turning a player's clock back (Pos2.X = their id); RootLash: the Treant Rod's root whipping out (Pos2 = its tip, Radius = half its width); Strike: the Nimbus's lightning landing (Radius = its blast); Tsunami: a wave rolling from Pos to Pos2 (Radius = half its width; Tsunami.cs); Meteor: one landing (Radius = its blast; drawn by Game/ElementalistLooks.cs); Lifeline: one per player the ultimate reached (Pos = where the caster stood, Pos2 = (that player's id, the caster's id), Radius = how far they were; Game/LifelineLook.cs); RiotBlock: Riot Gear taking a hit (Pos = its owner, Pos2 = where the hit came from, Radius = the plate's reach; Game/RiotGearLook.cs); DeathLotus: the Ninja's Death Lotus (Pos2.X = -1 for the cast itself, Pos = the Ninja, Pos2.Y = their id, Radius = targets struck; otherwise one per enemy struck, Pos = the enemy, Pos2.X = seconds until its cuts land, Radius = its size; Game/DeathLotusLook.cs); ShadowStep: the smoke look of a Ninja's step (Pos = where they stood, Pos2 = where they landed, Radius = their id * 1000 + the radius of the enemy they struck, or just their id * 1000 when it was a short step with no enemy; Game/ShadowStepLook.cs); LeapSlam: the Knight's Leap Slam, sent once at the cast and lasting its leap, landing and stun (Pos = where they leapt from, Pos2 = where they land, Radius = their id * 1000 + the slam's radius, Color.A = 1 when mastered and 0.5 otherwise, Dur = LeapTime + LeapFxTail + the stun; Game/LeapSlamLook.cs); TitanSlam: the Titan (Titan.cs; Radius = its reach, Pos2.X = 0 for a slam, 1 for standing up, 2 for the rubble when it falls, Dur = how long the cracks or rubble stay; Game/TitanLook.cs), and Stone Colossus's earth kinds (Pos2.X = 3 a slam, 4 the rise, 5 the rubble, 6 the rolling boulders with Pos2.Y the first one's angle, 7 a footfall; Core/StoneColossus.cs, Game/StoneColossusLook.cs); HolyCross: a Holy Cross flying from Pos to Pos2 (Radius = its flight, Dur = flight + fade; Game/HolyCrossLook.cs); KatanaStrike: the Katana's Alt strike from the stance to its fading line, Pos to Pos2 (Radius = the striker's id, Color.A = 1 when empowered, 0.5 otherwise, Dur = the whole show; Game/KatanaLook.cs); KatanaCut: one enemy the strike cut (Pos = the enemy, Pos2 = the line's direction, Radius = its size, Color.A = 1 when it died, 0.5 when it lived); StandHit: a shot stopped by a planted Last Stand (Pos = where, Pos2 = the knight, Radius = its damage; Game/LastStandLook.cs); CavalryCall: Cavalry Call (Pos = where the knight stood, Pos2 = the lane's far end, Radius = lancers (2 or 4), colour alpha 1 = mastered; Game/CavalryCallLook.cs); HarvestThrow: a Crimson Harvest ball thrown from Pos (the Hemomancer's feet) to Pos2, flight then blast then the stain, drawn from its age (Radius = the blood in it, Dur = flight + tail; Game/CrimsonHarvestLook.cs); StaffShove: the Bo Staff's shove (Pos = the Ninja, Pos2 = the far end of the bar's push, Radius = half the bar's width; Game/BoStaffLook.cs); RainRelease: a Sanguine Rain ring splashing away when the rain ends (Pos = where the ring was, Radius = its size; Game/SanguineRainLook.cs); TitheSnap: a Vein Tithe tendril letting go of an enemy (Pos = the Hemomancer, Pos2 = the enemy, Radius = its size, Color.A = 1 when it was a boss; Game/VeinTitheLook.cs); PactBind: Force Pact's chain cuffing an enemy (Pos = the caster, Pos2 = the enemy, Radius = the enemy's EnemyType, Color = its colour, Dur = ForcePactBind; Game/ForcePactLook.cs); PactSnap: a thrall's chain snapping as it falls (Pos = the thrall, Pos2 = its owner, Radius = its EnemyType); PactDissolve: an old thrall dissolving into the new one (Pos = the old one, Pos2 = the new one's spot, Radius = the old EnemyType); PactRise: a fallen thrall thrown back out on its chain (Pos = its owner, Pos2 = where it lands, Radius = its EnemyType); BurrowSnap: the Burrower snapping up under an enemy it hurt (Pos = the enemy, Pos2 = the Burrower, Radius = the enemy's size, Dur = 0.7; Game/BurrowerLook.cs); KatanaHitCue: a Katana swipe or lunge that cut something, drawn as nothing: it carries the cut's sound, and your own lunge's hit-pause (Pos = the swordsman, Pos2.X = their id, Radius = enemies cut, Color.A = 1 for the lunge, 0.5 for a swipe; Game/Juice.cs); MartyrCast: Martyr (Pos = the Hemomancer, Pos2 = the ally, Radius = the ally's id * 10 + 1 for a revive, + 0 for a ward put on, + 2 for a ward spent (Pos2 = Pos), Dur = 1.4, or the invulnerability when longer (the shimmer); Game/MartyrLook.cs); BloodFreeze: the Hemomancer's Blood Freeze, sent once at the cast and lasting its charge, wave, freeze and thaw (Pos = where it was cast, Pos2 = (caster's id, seconds a regular enemy stays frozen), Radius = how far the wave goes, Color.A = 1 when mastered and 0.5 otherwise; Game/BloodFreezeLook.cs); TwinStep: one Twin Blades chain step, drawn from its age (Pos = the swordsman, Pos2 = along the aim at reach, Radius = their id * 10 + the step, Dur = the step + its recovery; Game/TwinBladesLook.cs); TwinFang: Twin Fang's wind-up and release (Pos = where from, Pos2 = the cursor, Radius = the thrower's id, Dur = FangRelease + 0.3; the pair in flight is Player.FangOut/FangAt); TwinHit: a Twin Blades step or thrown sabre that cut something, drawn as nothing: it carries the cut's sound, and the swordsman's own hit-pause and shake (Pos = the swordsman, Pos2 = (their id, the step 1-5, 6 the thrown pair cutting, 7 it striking a wall), Radius = enemies cut; Game/Juice.cs); HammerThrow: the Storm Hammer in its owner's hand (Radius = their id, Pos2.X = 0 for a throw, 1 for a slam's wind-up, Dur = how long the hand is busy; the held model reads its pose from it, Game/StormHammerLook.cs); HammerSlam: the Storm Hammer's shockwave (Pos = the crater, Pos2 = the player, Radius = how far the wave runs, Dur = how long the cracks last; Game/StormHammerLook.cs); LightStep: one Lightbringer chain step, drawn from its age (Pos = the knight, Pos2 = along the aim at the cut's reach or the Sunburst's radius, Radius = their id * 10 + the step 1-3, 4 a Radiant Noon, Dur = the step + its recovery; Game/LightbringerLook.cs); Sunlance: the thrust and the lance of light (Pos = the knight, Pos2 = where the ally stood, Radius = the knight's id * 1000 + the ally's id, Dur = the thrust + its recovery); DebtSettle: a Blood Debt's moments (Pos = the debtor, Pos2 = (their id, 2 borrowed at the cast / 1 paid in full / 0 forfeited), Radius = the health borrowed, paid or lost; Game/BloodDebtLook.cs); VeinRush: a Vein Rush streak from Pos to Pos2 (Radius = the rusher's id, Dur = the rush plus its fade; Game/VeinRushLook.cs); VeinCut: an enemy the rush cut through (Pos = the enemy, Pos2 = the rush's direction, Radius = the rusher's id * 1000 + the enemy's size; its blood flies back to the rusher); CenserStep: one Censer rite step, drawn from its age (Pos = the knight, Pos2 = along the aim at reach, Radius = their id * 10 + the step 1-3, Dur = the step + its recovery; Game/CenserLook.cs); CenserCast: Censer Cast thrown (Pos = where from, Pos2 = where the head lands, Radius = the thrower's id * 10 + Embers spent, Dur = CenserMotion.CastTotal); Exsanguinate: the Barbed Lash (Pos2.X = -1 the cast, -2 the impact, otherwise the hooked enemy's id; Pos2.Y = the caster's id; Core/Exsanguinate.cs, Game/ExsanguinateLook.cs); LightMark: a Lightbringer's mark on a player (Radius = their id; Pos2.X = 1 a heal burst, 2 a ward snapping shut, 3 a shot striking the ward; Pos2.Y = seconds before a burst or snap shows (the Sunburst's ring reaching them), or the angle the shot came from; Color.A = 1 for a Radiant Noon's bigger burst; Game/LightbringerLook.cs); LanceStep: one Lance chain step, drawn from its age (Pos = the knight, Pos2 = along the aim at the step's reach, Radius = their id * 10 + the step 1-4, Color.A = the Breaching Thrust's half width / 100, Dur = the step + its recovery; Game/LanceLook.cs); LanceHook: Hook & Haul (Pos = the knight, Pos2 = where the hooked enemy stood (or the aim point on a miss), Radius = their id * 10 + 1 when it hooked something, Dur = the move + its recovery); LanceHit: a Lance step that hit something, drawn as nothing: it carries the hit's sound, and the knight's own hit-pause and shake (Pos = the knight, Pos2 = (their id, the step 1-4, 5 the hook), Radius = enemies hit; Game/Juice.cs); LanceSpark: one enemy a Lance step hit (Pos = the enemy, Pos2 = Pos + the push's direction, Radius = 1 for the Breaching Thrust, 0.5 otherwise); LanceImpact: a shoved enemy hitting another or a wall (Pos = where, Pos2 = (the enemy's id, the other's id or -1 for a wall), Radius = the damage each took; Core/Lance.cs); Airstrike: one wall of the Soldier DPS's Airstrike (Pos = where the jet comes in, Pos2 = the lane's far end, Radius = thrower id * 10000 + the flare's distance along the lane, colour alpha 1 = with the flare, 0.5 = the mastered second wall; Core/Airstrike.cs; JuggShard: a Juggernaut Suit flak shard flying back at a shooter (Pos = where the shot struck the armour, Pos2 = the shooter, Radius = the wearer's id; Core/Juggernaut.cs, Game/JuggernautLook.cs)); RodBolt: Lightning Rod's bolt from the sky (Pos = where it lands, the shooter or where it stood; Pos2 = (the rod's player id, the bolt's damage); Radius = the struck enemy's size, 0 when it was already dead); RodArc: one arc on from a bolt (Pos = from, Pos2 = to, Radius = its damage); RodGround: an enemy hit grounded through a Lightning Rod (Pos = the player, Pos2 = (their id, the damage they took), Radius = 1 for a shot, 2 for a melee or contact hit; Core/LightningRod.cs, Game/LightningRodLook.cs); TaserHit: a Fork Bolt landing on a player (Pos = the player, Pos2 = a point one px on along the bolt's flight, Dur = 0.7; Game/TaserLook.cs); BossBeam: one player beamed into the hub before a boss wave (Pos = where they stood, Pos2 = where they land, Radius = their id, Dur = BeamTotal; Core/BossBeam.cs, Game/BossBeamLook.cs); DoubleBarrel: the Scattergun's Double Barrel going off (Pos = the muzzle, Pos2 = along the aim, Radius = the shooter's id * 10 + shells fired; Core/TankAlts.cs, Game/TankAltLook.cs); KnuckleStep: an Arc Knuckles punch, drawn from its age (Pos = the puncher, Pos2 = along the aim, Radius = the puncher's id * 10 + step, 6 the Counter Hammer; Core/ArcKnuckles.cs, Game/ArcKnucklesLook.cs); KnuckleDash: a Rocket Dash (Pos = where it started, Pos2 = where it ends, Radius = the dasher's id); KnuckleChain: a dash chain move (Pos = the puncher, Pos2 = along the aim, Radius = id * 10 + 1 Flurry, 2 Grab & Throw, 3 Rocket Punch; Core/ArcKnucklesChain.cs); KnuckleHit: an Arc Knuckles punch connecting (Pos = the first enemy hit, Pos2 = (puncher's id, kind), Radius = enemies hit; Game/Juice.cs); GasLob: a Gas Grenadier's lob (Pos = where it lands, Pos2 = (the grenadier's id, flight seconds), Radius = the cloud's size, Dur = the wind-up left + the flight; Pos2.Y = -1 is the release cue instead: Pos = the thrower, Sfx.Throw; Game/GasLobLook.cs); PortalSeal: a spawn portal shot closed (Radius = its size, Color = its colour; Game/SpawnPortalLook.cs); Shockwave: the Shockwave ability's wave (Pos = where it was cast, Pos2.X = the caster's id, Radius = how far it runs, Dur = ShockwaveFxTime; Core/Shockwave.cs, Game/ShockwaveLook.cs); Sentry: a Turret dropping in (Pos2.X = 0, or 2 a mastered second one) or powering down (1) (Pos2.Y = its aim, Radius = the owner's id; Core/SentryNova.cs, Game/SentryNovaLook.cs); Nova: the Nova ability's starburst (Pos2 = (caster id, shots), Radius = the aim the ring starts from); MarrowLink: a Bone Guard marrow thread between two enemies (Pos2 = (their ids), Radius 0 a loose thread for its life, 1 snapping taut as one is caged (drawn 0.45 s; it lasts as long as a loose one could, hiding it); Core/BoneGuard.cs, Game/BoneGuardLook.cs); StoneSlam: the Stonespear's slam (Pos = the slammer, Pos2.X = 0 the wind-up, 1 and up the impact (its fraction seeds the cracks), Pos2.Y = their id; Radius = the slam's reach; Core/Stonespear.cs, Game/StonespearLook.cs); ArcField: retired (Arc Lightning's old strip), never sent; ArcTag: Arc Lightning tagged an enemy (Pos2 = the shooter's id, the enemy's id; Radius = its place, from 0); ArcLink: a hop of its chain from Pos to Pos2 (Radius = the hop's place, -1 a fizzle at the staff); ArcFork: a Forked Volley bolt from Pos to Pos2 (Radius = its bend, px sideways) (Core/ArcLock.cs, Game/ArcLockLook.cs); NunStep: one Nunchaku step, drawn from its age (Pos = the Ninja, Pos2 = along the aim, Radius = their id * 10 + the step 1-4, 5 the Twin Counter); NunCounter: the counter's lash (Pos = the Ninja, Pos2 = its far end, Radius = shots swatted into it; Core/Nunchaku.cs, Game/NunchakuLook.cs); BeeSting: a Honeybee Staff bee's sting (Pos = where it died, Pos2 = (the enemy's id, 1 the first Swarm sting / 0.5 a later one / 0), Radius = the way it flew; Core/HoneybeeStaff.cs, Game/HoneybeeLook.cs); HoneyHarvest: an enemy bursting into bees in honey (Pos2 = (the owner's id, bees), Radius = its size); PistolWhip: the Revolver's Alt swing (Pos = the player, Pos2 = along the aim at reach, Radius = their id, Dur = the cooldown; Core/PistolWhip.cs, Game/PistolWhipLook.cs); GaleCatch: a shot caught by a Gale Wall (Pos = where, Pos2 = (the caster's id, the shot's heading), Radius = its size); GaleBlast: a Gale Wall unleashed (Pos = the caster, Pos2 = the cone's far middle, Radius = the charge; Core/GaleWall.cs, Game/GaleWallLook.cs); WwSplash / WwBurst: a Water Ward drop splashing on an enemy / bursting on a shot (Pos = the drop, Pos2 = its caster / the shot, Radius = the reach; Core/WaterWard.cs, Game/WaterWardLook.cs); CoverHit: a shot ringing off a Deployable Cover (Pos = where, Pos2 = its middle plus its facing, Radius = the shot's size); CoverBreak: a cover bursting apart (Pos = its middle, Pos2 = its middle plus its facing, Radius = its id as a seed, Dur covers the scraps left on the floor); CoverFold: a cover folding away (Pos/Pos2 as CoverBreak, Radius = its health left 0-1; Core/DeployableCover.cs, Game/DeployableCoverLook.cs); DragonBite: Dragon's Dogma biting an enemy (Pos = the enemy, Pos2 = (the Ninja's id, 1 devoured / 0 a big one bitten), Color = its colour, Radius = its size; Core/DragonsDogma.cs, Game/DragonsDogmaLook.cs); CereBlock: a Ceremonial Armor plate stopping a shot (Pos = where, Pos2 = the plate); CereBreak: a plate shattering (Pos = the plate, Pos2 = one px outward, Radius = the mastered burst or 0; Core/CeremonialArmor.cs, Game/CeremonialArmorLook.cs); DecoyHit: a shot or a bite landing on a Decoy Dummy (Pos = where, Pos2 = one px along its throw), DecoyPop: it popping (Radius = the blast), DecoyFold: it deflating (Radius = its health left; Core/DecoyDummy.cs, Game/DecoyDummyLook.cs); HalberdStep: a Reaper's Wheel step played from its age (Pos = the swinger, Pos2 = Pos plus the reach along the aim, Radius = id x 10 + step, Dur = HalberdMotion.StepTotal); HalberdPoke: the set pike jabbing out (Pos2 = the point at full reach, Radius = the poker; Core/Halberd.cs, Game/HalberdLook.cs)  // HiveCall: the Hive Censer's Alt calling every swarm of a player to a spot (Pos = the spot, Pos2.X = their id, Radius = the ring's size, Dur = Game.HiveCallTime; Core/HiveCenser.cs, Game/HiveCenserLook.cs)  // SiegeBell: the Tank Knight's Siege Bell for its life (Pos = the bell, Pos2 = (the caster's id, the stun), Radius = the shockwave's reach; Core/SiegeBell.cs, Game/SiegeBellLook.cs)

    /// Sound cues raised by the game; the Unity layer plays them.
    public enum Sfx : ushort
    {
        None, EnemyShot, Hit, Hurt, Explode, BigExplode, Ability, Shockwave, Blink,
        LevelUp, Pick, WaveStart, WaveClear, GameOver, Downed, Revive,
        // One gunshot per weapon, plus the turret and Nova.
        ShotShotgun, ShotCannon, ShotRifle, ShotPistol, ShotBow, ShotAssault, ShotSniper, ShotSmg,
        ShotTurret, ShotNova,
        Pickup, PickupBig, Roar, Victory,
        SpellFire, SpellFrost, SpellZap, SpellArcane, SpellHoly, Throw, Summon, Freeze,
        SwingLight, SwingHeavy, Clang,  // Knight swings, and a shield taking a hit
        BossShift, BossCharge, BossPulse, BossChime,  // soft cues for bosses' abilities (BossSounds.cs)
        // Bosses' own mechanics (SoundBank.cs recipes; wired in each boss's file). Kept at the end so values don't shift.
        BossLaserWarn, BossLaser, BossSlam, BossWhoosh, BossSiren, BossBuzz, BossSplat, BossGong,
        BossScythe, BossThunder, BossWarp, BossRumble, BossCrack, BossCage, BossChomp,
        // The upgrade picker opening and closing (PickIntro.cs, PickOutro.cs): played on your own screen only, never sent.
        PickRise, PickCardOut, PickCardLand, PickRare, PickReturn, PickAbsorb,
        // A run launching (LaunchIntro.cs): your own screen only, never sent.
        LaunchSweep, LaunchRipple, LaunchBeam, LaunchLand, LaunchHud,
        // The pause menu's sounds (WindowAnim.cs; the typing was its old glitch opening's): your own screen only, never sent.
        PauseType, PauseSettle, PauseClose,
        // The Repulsion Cannon's blast, and Chain Light's cone and its first arc (SoundBank.cs recipes; named Shot/Spell so they use the Weapons volume).
        ShotRepulse, SpellChainCone, SpellChainArc,
        // The Radiant Orb (RadiantOrb.cs): cast, a twinkle each tick that touches something, and fading out. Sent, so quieter for allies.
        RadiantCast, RadiantTouch, RadiantFade,
        // Zombie Infection (Zombies.cs; SoundBank.cs recipes): a groan now and then, getting back up, spit, a grab, a scream, a burst, a mend.
        ZombieGroan, ZombieRise, ZombieSpit, ZombieGrab, ZombieShriek, ZombieBurst, ZombieMend,
        // The profile picker and the menu trading places (ProfileTransition.cs): your own screen only, never sent.
        ProfSelect, ProfSweep, ProfSweepBack, ProfScratch, ProfBar, ProfChord,
        // The menu title typing out (MenuIntro.cs, look B of Docs/Mockups/menu-typing-sounds.html): a key per letter, the space bar, and Enter on the last. Never sent.
        TitleKey, TitleSpace, TitleEnter,
        // A main menu button being hovered (MenuVhs.cs, look A of Docs/Mockups/menu-hover-sounds.html): a tape-head tick. Never sent.
        MenuHover,
        // The menu's Play transition (MenuServo.cs, version 2 of Docs/Mockups/menu-bay-doors.html): beeps, the doors' servo whine (and backwards),
        // a clamp, the screen's power-on zap, the keys' whirr and tick, the box's lift, its seating thud, and a flap click per header letter. Never sent.
        BayBeep, BayServo, BayServoBack, BayClamp, BayZap, BayWhirr, BayTick, BayLift, BaySeat, BayClack,
        // New run's CRT boot into the lobby (LobbyCrt.cs, PlayCrtEntryCues): power-on thump, a key per typed character, a beep per log line, the scan line's sweep, and the ready chime. Never sent.
        CrtPower, CrtType, CrtLine, CrtSweep, CrtReady,
        // Picking a profile disk (ProfileTransition.cs, DriveSlot.cs): the clamps unlatching, the disk's whoosh, its clack as it tips flat, the slide into
        // the drive slot (and the eject going back), the latch click, the drive reading, and a ready chime. Your own screen only, never sent.
        DiskLift, DiskFly, DiskTip, DiskSlide, DiskEject, DiskClick, DiskRead, DiskReady,
        // The melee rushers (MeleeSounds.cs, from Docs/Mockups/melee-enemy-sounds.html): made on every machine from the enemies' replicated state, never sent.
        // Quieter the further they are from you.
        SwarmerChomp, SwarmerDie, BruteStep, BruteGrunt, BruteBash, BruteDie,
        LungerTell, LungerLeap, LungerLand, LungerDie,
        SwarmerPatter, LungerPad,  // their footsteps
        // The Ninja DPS (NinjaSoundBank.cs, the Shinobi set of Docs/Mockups/ninja-dps-sounds.html): the Kunai's whoosh (named Shot so it uses the Weapons
        // volume), its blink, Shadow Step's smoke and its strike, Vanish, Blade Fan opening and flying out, and Death Lotus darkening the arena. Sent, so quieter for allies.
        ShotKunai, NinjaBlink, NinjaStepCast, NinjaStepStrike, NinjaVanish, NinjaFanOpen, NinjaFanBurst, NinjaLotusCast,
        // The Katana (KatanaSoundBank.cs, the Steel set of Docs/Mockups/katana-sounds.html; Katana.cs). Sent by the host: Senbon's swipes, the wind-up,
        // Hayate's dash (KatanaLunge), a cut landing (the first five use the Weapons volume), the strike's cut and a chime per hop.
        KatanaSwipe1, KatanaSwipe2, KatanaWindup, KatanaLunge, KatanaHit, KatanaStrikeCut,
        KatanaChain1, KatanaChain2, KatanaChain3, KatanaChain4, KatanaChain5,
        // The Treant Rod (TreantSoundBank.cs, from Docs/Mockups/treant-rod-sounds-seeds.html): the root lash's leaf rustle, the Treant's seed shot (Pod Pop) and a seed landing (Tick). Sent, so quieter for allies.
        SwingRoot, ShotSeed, ShotSeedHit,
        // The Greatsword (GreatswordSoundBank.cs): the overhead rend's blade lifting, the guard going up, a bullet stopped on it, Temper full and the Rend. Sent by the host; the wind-up and the Rend use the Weapons volume.
        GreatWindup, GreatRaise, GreatBlock, GreatFull, GreatRend,
        // The Twin Blades (TwinSoundBank.cs, the Crescent Waltz): the Rising and Falling Cuts, a jab, the Pirouette, the Crescent Shear, a cut landing (and a heavy one),
        // Twin Fang thrown, its burst where the sabres cross, and the catch. Sent by the host; all but the catch use the Weapons volume.
        TwinCut1, TwinCut2, TwinJab, TwinSpin, TwinShear, TwinHit, TwinHitHeavy, TwinThrow, TwinCross, TwinCatch,
        // The Lightbringer (LightSoundBank.cs): Dawn and Mercy Cuts, Noon (from the click: the swell up to the blow, then the chord) and a Radiant Noon,
        // a cut landing, a crescent healing and warding an ally, the Sunlance gathering and released, and your own key with too little light.
        LightCut1, LightCut2, LightNoon, LightRadiant, LightHit, LightHeal, LightWard, LanceGather, LanceRelease, LanceFizzle,
        // The Censer (CenserSoundBank.cs, the Thurible Rite): the Sweep and Return Sweep, the Benediction's whirl, a hit landing (and a heavy one),
        // the blessing as a heal goes out, the Benediction's bell, Censer Cast thrown, its landing, and the head caught back. Sent by the host; the swings and hits use the Weapons volume.
        CenserSweep1, CenserSweep2, CenserWhirl, CenserHit, CenserHitHeavy, CenserBless, CenserBell, CenserThrow, CenserLand, CenserCatch,
        // The Lance (LanceSoundBank.cs, the Breach): a jab, the Wide Sweep, the Breaching Thrust (from the click: the wind-up, then the thrust), a hit landing (and the
        // Breaching Thrust's heavy one), an Impact (a shoved enemy into another) and a wall slam, Hook & Haul thrown and its haul. Sent by the host; the first four use the Weapons volume.
        LanceJab, LanceSweep, LanceBreach, LanceHitSfx, LanceHitHeavy, LanceImpactSfx, LanceWall, LanceHookOut, LanceHaul,
        // A perfect parry (Parry.cs; ParrySoundBank.cs): a bright ping and the needle leaving. Sent; the Effects volume.
        PerfectParry,
        // The Katana switching stance (KatanaSoundBank.cs): the blade turned from one guard to the other and a short burst. Sent.
        KatanaStanceSwap,
        // The Dodge (DodgeSoundBank.cs, Cloth Swish of Docs/Mockups/dodge-sounds.html): a soft fabric rush as you dodge. Your own screen only; the Effects volume.
        DodgeSwish,
        // The Katana's finishers (KatanaSoundBank.cs): the Momentum Burst's ground stab and shockwave, the caltrops thrown, caltrops flung or seeking. Sent.
        KatanaBurstSfx, KatanaCaltrops, KatanaFling,
        // The Tank Soldier's Alt fires (TankAltSoundBank.cs): Double Barrel and a shell loading, the Proximity Shell fired, armed and
        // going off, Tear Gas fired and hissing. Sent; the three launches use the Weapons volume.
        TankDouble, TankShellLoad, TankMineLaunch, TankMineArm, TankMineBurst, TankGasLaunch, TankGasHiss,
        // The Repulsion Cannon's Undertow (SoundBank.cs; Repulsion.cs): the inhale as the pull starts, a swirling hum repeated while it holds, and the
        // soft pop as it lets go. Sent by the host; named Spell so they use the Weapons volume.
        SpellPullStart, SpellPullHum, SpellPullStop,
        // The Arc Knuckles (ArcKnucklesSoundBank.cs, the Arc Coil look): a jab or cross, the hook, the uppercut, the Twin Hammer's slam,
        // the Counter Hammer, and the coils charging when a perfect parry arms it. Sent.
        KnuckleJab, KnuckleHook, KnuckleUpper, KnuckleSlam, KnuckleCounter, KnuckleArm,
        // The Rocket Dash (ArcKnucklesSoundBank.cs): the thruster firing, the fist landing. Sent.
        KnuckleRocket, KnuckleRocketHit,
        // The dash chain (ArcKnucklesSoundBank.cs): the grab latching on, the throw. Sent.
        KnuckleGrab, KnuckleThrow,
        // A punch connecting (ArcKnucklesSoundBank.cs; sent with FxKind.KnuckleHit): a light knock, a heavy one.
        KnuckleImpact, KnuckleImpactHeavy,
        // The Shuriken's Great Star (NinjaSoundBank.cs; GreatStar.cs): the throw, the catch. Sent.
        GreatStarThrow, GreatStarCatch,
        // A player touching a wall (WallHumSoundBank.cs): a looping hum, played locally, never sent.
        WallHum,
        // Played by each screen from what it sees, never sent (they were a side enum each while this was a byte): the Nova and Turret
        // (SentryNovaSoundBank.cs), Bullet Rain (BulletRainSoundBank.cs), the Stonespear (StonespearSoundBank.cs), footsteps' two layers
        // (FootstepSoundBank.cs), the menus' hover tick and click, and a wall pushed out (SoundBank.cs).
        NovaBurst, TurretDeploy, TurretFold, RainCast, RainPatter, RainEnd, StoneThrow, StoneWind, StoneSlam, StoneHit, StoneStick,
        StepThump, StepTick, UiHover, UiClick, WallPush /* retired with wall push, Nov 18: no sound */,
        // Arc Lightning's lock-on (ArcLockSoundBank.cs): a tag, a hop, letting go, a Forked Volley bolt, a fizzle, and the charge hum (a loop). Never sent.
        ArcTag, ArcHop, ArcCrack, ArcFork, ArcFizzle, ArcHum,
        // The Fireball (FireballSoundBank.cs, Deep Roar of Docs/Mockups/fireball-sounds.html): the launch roar (the weapon's Sound) and the burn when it ignites an enemy. Sent.
        ShotFireball, ShotFireballBurn,
        // The Nunchaku (NunchakuSoundBank.cs, the Storm set of Docs/Mockups/nunchaku-sounds.html): each combo step and the Twin Counter as
        // pressed, a stick landing, the windmill spinning up, a shot swatted. Sent.
        NunSnap, NunBack, NunFig8, NunCrush, NunCounter, NunHit, NunGuard, NunSwat,
        // The Attack Helicopter (HeliSoundBank.cs), played by each game itself from the look: the rotor's loop, a chaingun round,
        // the barrels spinning up on a new target. Not sent.
        HeliRotor, HeliShot, HeliSpin,
        // Flood (FloodSoundBank.cs): the stamp and splash, the water lapping, the pool draining. Played by each screen from the pool's clock, never sent.
        FloodCast, FloodLap, FloodDrain,
        // Water Ward (WaterWardSoundBank.cs): a drop splashing on an enemy, a drop eating a shot and bursting into mist, the cast. Sent.
        WwSplash, WwBurst, WwCast,
        // Gale Wall (GaleWallSoundBank.cs): the cast, the field's hum (a loop), a catch, the full chime, the unleash. Played by each screen from what it sees, never sent.
        GaleCast, GaleHum, GaleCatch, GaleFull, GaleUnleash,
        // Deployable Cover (CoverSoundBank.cs): the throw and build, the cover bursting, folding away, a shot ringing off it. Played by each screen from the zone and FxKind.CoverBreak/CoverFold/CoverHit, never sent.
        CoverDeploy, CoverBreak, CoverFold, CoverTing,
        // Dragon's Dogma (DogmaSoundBank.cs): the cast's roar, a devour, a big one bitten, it coiling back in. Sent.
        DogmaRoar, DogmaDevour, DogmaBite, DogmaHome,
        // Ceremonial Armor (CeremonialArmorSoundBank.cs): a plate summoned (a chime, the drop and the click into its slot, 0.55 s in), a plate shattering. Sent.
        CereSummon, CereBreak,
        // The Frost Lance (FrostSoundBank.cs): the chain's five steps (the slam's swing, then its landing), a freeze, a shatter, a parry
        // breaking the lance, Absolute Zero's charge, wave and shatter. Sent.
        FrostCrescent, FrostBackhand, FrostWaltz, FrostFan, FrostSlamWind, FrostSlam, FrostFreeze, FrostShatter, FrostBreak, ZeroCharge, ZeroWave, ZeroShatter,
        // The Blood Blast (BloodBlastSoundBank.cs): the chain's steps, a cut landing, Crimson Tide's gather and wave, the Blood Vortex. Sent.
        BlastSlash, BlastFlurry, BlastWhirl, BlastCleave, BlastFlight, BlastHit, BlastTideGather, BlastTideWave, BlastReturn,
        // Warhorn Volley (WarhornSoundBank.cs): the war horn and the whole volley after it (hiss, whistles, thuds, crackle) in one take. Sent.
        Warhorn,
        // The Rubber Chicken skin's sound pack for the Twin Blades (ChickenSoundBank.cs), one for each Twin cue: picked by the host from
        // the swinger's skin (Game.TwinSfx). Sent.
        ChickCut1, ChickCut2, ChickJab, ChickSpin, ChickShear, ChickHit, ChickHitHeavy, ChickThrow, ChickCross, ChickCatch,
        // The Nodachi (NodachiSoundBank.cs): each step (sent at the press, timed to its strikes), a cut landing, a shot blocked, the stance switch. Sent.
        NodNuki, NodKesa, NodMoon, NodTenchi, NodNagi, NodKasumi, NodNidan, NodSenbon, NodTsuka, NodHiki, NodHit, NodBlock, NodToStorm, NodToGuard,
        // Decoy Dummy (DecoyDummySoundBank.cs): the throw and puff-up, a hit's rubber squeak, the pop and zip, the deflate. Played by each screen from the zone and FxKind.DecoyHit/DecoyPop/DecoyFold, never sent.
        DecoyThrow, DecoySqueak, DecoyPop, DecoyFold,
        // Deployable Turret (DeployTurretSoundBank.cs): the drop, a heavy round, the barrels spinning up, the overheat, the blast, the early dismount. Played by each screen from the zone, never sent.
        DtDrop, DtShot, DtSpool, DtHeat, DtBlast, DtLatch,
        // A One-way piece's door bumped from the wrong side (OneWayDoorSoundBank.cs): played by your own screen when it holds you back, never sent.
        OneWayBump,
        // The Rioter (RioterSoundBank.cs, the Mob voice): the yell on coming within earshot, the hit, the bottle's wind-up, its
        // throw (sent), the bottle breaking, the death, a running step. Played by each screen from what it sees (MeleeSounds.cs), never sent but the throw.
        RiotYell, RiotAttack, RiotHup, RiotWhoosh, RiotShatter, RiotDie, RiotStep,
        // The main menu and saved-runs screen in the holo look (MenuSoundBank.cs): played by your own screen, never sent.
        MenuPlay, MenuKey, MenuQuit, MenuEject, MenuRooms, NeonSpark, BayVault, BayBack, RunContinue, RunDelArm, RunDelete,
        // Graze (Graze.cs, GrazeSoundBank.cs): an enemy shot skimming past you. Played by your own screen, never sent.
        Graze,
        // A Literal Tank (LitTankSoundBank.cs): the engine's loop, the main gun, the drop's whistle (sent with the cast), the landing,
        // the last seconds' beep, the hatch. The rest are played by each screen from the look or the shell, never sent.
        LitTankEngine, LitTankCannon, LitTankFall, LitTankLand, LitTankWarn, LitTankHatch,
        // Orbital Laser (OrbitalSoundBank.cs): the whole strike in one take (uplink chirp, lock-on beeps, charge whine, the impact, the beam's roar, the sizzle). Sent.
        OrbitalLaser,
        // Rodent Minigun (RodentSoundBank.cs): a rodent squeaking out of the portal (each shot, quiet), the portal tearing open (sent with the cast).
        RodentSqueak, RodentPortal,
        // Siege Bell (SiegeBellSoundBank.cs): the three tolls and the shockwave's thump in one take. Sent with the cast.
        SiegeBell,
    }

    public struct SoundCue
    {
        public Sfx Sfx;
        public bool Ally;  // your friend's shot: played quieter
        public float Quiet;  // 0..1, how much quieter for being far from you (0 = full volume; Game.QuietAt)

        /// The volume to play at, before the player's own sliders.
        public float Volume => (Ally ? 0.45f : 1f) * (1f - Quiet);

        public static implicit operator SoundCue(Sfx s) => new SoundCue { Sfx = s };
    }

    public class AbilitySlot
    {
        public AbilityId Id = AbilityId.None;
        public int Charges, MaxCharges = 1;
        public float Timer;  // time until the next charge comes back
    }

    public class Player
    {
        public int Id;
        public Role Role;
        public RoleDef Def;
        public Kit Kit;
        public bool Recruit;  // a Training recruit: no class look, no abilities, the Training Pistol (DrillGame.cs MakeRecruit)
        public WeaponDef Weapon;
        public AbilityId FirstAbility;
        public AbilityId WildcardAbility = AbilityId.None;  // a Wildcard ability: it sits on a 2nd–4th ability slot from the start (Game.Recalc)
        public Kit WildKit;                            // a Wildcard's ultimate comes from the class of its 1st ability
        public Kit UltKit => Role == Role.Wildcard ? WildKit : Kit;
        public byte UltPick;  // the key picked on the lobby's Ultimate row (LobbyEntry.Ult; 0 the default ultimate), sent in Msg.Start
        public float BaseHp;
        public string Name;
        public V2 Pos, Disp;
        public V2 Motion, MotionFrom;  // host only: how fast and which way they're moving, smoothed (Props.cs shoves crates with it; Variety.cs)
        public float Aim;
        public bool IsBot;       // host: a bot plays this player (a lobby bot, or F1's "Bot plays for you"); sent in Msg.Input, so cursor pickups skip it (Coins.cs)
        public V2 AimAt;         // the point they're aiming at (Implosion picks the enemy under it; sent with inputs)
        public bool Firing;
        public bool FiringSeen;  // holding fire, as everyone's screen knows it (for drawing beams; synced in snapshots)
        public float Hp, MaxHp;
        public float PortalT;   // your own game: just came out of a Rooms portal (Rooms.cs)
        public float Wound;     // health hits took that no heal brings back: WoundShare of each hit, up to WoundCap of max; carried across waves, cleared by a boss falling, a health shrine, the Medic's heal or a Rest (synced, saved)
        /// The most healing can bring you to: max health less the wound.
        public float HealCap => Math.Max(1f, MaxHp - Wound);
        public bool Downed;
        public int MomentumStacks;  // the DPS role passive (Momentum.cs)
        public float MomentumT;
        public float IronLost, IronWait;  // the Tank role passive (Ironhide.cs): shield taken off, seconds until it refills (replicated)
        public float IronCap;  // the most that shield holds now (Ironhide.cs: the host works it out each tick; replicated)
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, AdrenalineT, SwiftT, Flash, LevelFlash;  // AdrenalineT: the Healer's Adrenaline Shot (Adrenaline.cs)
        public float TauntT;  // Roar: enemies target this player
        public float DashT;   // replicated: the Ninja's Healing Dash, seconds of sprint left (Ninja.cs)
        public byte Sprint;   // replicated: the Dodge's held sprint, how much is left (255 fresh, 0 none; Dodge.cs); its owner's game sends it in Msg.Input
        public float SurgeT;  // replicated: Arcane Surge's faster firing, seconds left (the Elementalist's class card)
        public float GhostT;  // host: Ghost Step's damage cut after a dodge, seconds left (the Ninja's class card)
        public float DashHeal = 1f, DashRadius;  // host: the footprints it leaves heal this much more, and are this wide
        public V2 DashLast;   // host: where the last footprint went down
        public float AegisT, FrenzyT;  // Aeroaegis, Alpha Call
        public float LegionT;          // replicated: seconds of Legion's Blood Moon left (Ultimates.cs, Game/LegionLook.cs)
        public float ScentT;           // host: Alpha Call's Blood Scent, which makes the frenzy build with each bite (Legion's frenzy stays flat)
        public float UnbreakT;  // Knight: Unbreakable (replicated)
        public float CaltropT;  // Caltrop Run (CaltropRun.cs), replicated: seconds of the run left (the speed and the look)
        public int CaltropLeft; public float CaltropAcc; public V2 CaltropLast;  // host: caltrops still to drop, px moved toward the next, where you were last tick
        public bool GauntletDown;  // The Gauntlet (Gauntlet.cs), replicated: it lies on the floor at GauntletRest until it comes back
        public V2 GauntletRest;
        public const int WallPlates = 7;  // Shield Wall: plates across the arc, slot 3 is dead ahead
        public readonly byte[] WallPlate = new byte[WallPlates];  // Shield Wall (replicated): each plate's health, 0 = broken; the wall is up while any plate is left
        public bool WallUp { get { for (int i = 0; i < WallPlates; i++) if (WallPlate[i] > 0) return true; return false; } }
        public float VanishT;                         // Ninja: Vanish (replicated)
        public int PlateN;                            // Ninja: Ceremonial Armor's plates up (replicated; CeremonialArmor.cs)
        public float PlateClock, PlateNewT, PlateMax; // ...their turning clock, the newest one's drop-in left, a full plate's health (replicated)
        public readonly float[] PlateHp = new float[Game.CerePlateMax];  // ...each plate's health (replicated as a byte of PlateMax)
        public bool PlateBurst;                       // host: mastered, a breaking plate bursts
        public int SbN;                               // Ninja: Senbon Seekers' needles held (replicated; SenbonSeekers.cs)
        public float SbClock, SbNewT;                 // ...their schooling clock, the last cast's flourish left (replicated)
        public bool SbCap;                            // ...mastered: holds 12 (replicated, for the key's cap)
        public float SbGapT, SbPw = 1f;               // host: until the next launch; the cast's power
        public V2 VanishFrom;                         // where enemies last saw you before you vanished (replicated since 158: the smoke bank is drawn there)
        public float OrbitT;                          // Ninja: Blade Fan's kunai still orbiting you, seconds left (replicated)
        public int OrbitN;                            // ...and how many there are (replicated)
        public float OrbitClock;                      // host: counts up while they orbit, for each enemy's 0.5 s between cuts
        public readonly Dictionary<int, float> OrbitCut = new Dictionary<int, float>();  // host: enemy id -> OrbitClock of its last cut
        public float SpinUp;                          // Burst SMG: how spun up it is, 0 to 1 (replicated)
        public int PreyId;                            // Wolf Whistle: the enemy the pack is hunting (replicated)
        public byte PackKills;                        // Wolf Whistle: kills towards the next wolf (replicated; WolfPack.cs)
        public bool PackStarted;                      // host: the Wolf Whistle's starting wolves came
        public int Orbiters;                          // Ice Shards: shards circling you, waiting for an enemy (replicated)
        public float LaunchCd;                        // host: until the next one launches
        public int NeedleN;                           // Acupuncture Needles in you, each healing over time (replicated: drawn in your body)
        public float NeedleT;                         // until they fall out (replicated); each new one renews it
        public float NeedleHps;                       // host: how much each needle heals a second
        public int NeedleBy;                          // host: who placed the last one
        public float BloodletCd;                      // host: until Bloodletting can go again (AcupunctureNeedles.cs)
        public V2 KunaiMark;                          // Kunai: where the last shadow kunai landed, to blink to (replicated)
        public float KunaiMarkT;                      //   for this much longer (replicated)
        public int KunaiCount;                        // host: kunai thrown (every KunaiShadowEvery-th is a shadow kunai)
        public int Light;                             // Lightbringer: its light gauge, hits landed toward a Radiant Noon or a Sunlance (replicated; Lightbringer.cs)
        public int Combo;                             // Twin Blades and Censer: the chain step last thrown, 1 to 5 (0: the next is the first), in the low 3 bits, and a count of steps thrown above them (replicated; your own game steps you forward when the count changes); ComboT: until the chain starts over (host)
        public float ComboT;
        public byte KnuckleDashes;                    // Arc Knuckles: dashes and lunges started (the Rocket Dash, the Brawl stance's Pull Punch, Slip Dashes and the step in to a punch; replicated: your own game moves you when it changes)
        public V2 KnuckleDashTo;                      //   where the last one ends (replicated: the host picked it)
        public byte KnuckleStance;                    // Arc Knuckles: 0 Brawl, 1 Rocket (replicated; the weapon-action key swaps; ArcKnucklesBrawl.cs)
        public byte KnuckleMove;                      //   Brawl: the next move, 0 the Pull Punch to 7 the Quake Punch (replicated: the pips, and your own game's click wheel shows from 1)
        public byte KnuckleSteer;                     //   the click wheel's direction with this input (0 none, else an angle; sent in Msg.Input; host)
        public bool KnuckleRooted;                    //   the Barrage holds you still (replicated: your own game roots you)
        public byte KnuckleCharge;                    //   the Quake Punch's charge, 0-255 (replicated: the ring on you fills)
        public float KnuckleChargeT, KnuckleRootT, KnuckleIdleT, KnuckleBufT;  //   host: the charge held, the root left, since the last Brawl move, a click waiting for the move in progress
        public bool KnuckleCharging, KnuckleFireWas;  //   host: Fire held through the Quake Punch's charge; Fire down last tick (a click is its edge)
        public byte KnuckleChain;                     // Arc Knuckles: the dash chain's stage (replicated; ArcKnucklesChain.cs): 0 none, odd a Fire window open, 2 Flurry, 4 Grab & Throw, 6 Rocket Punch
        public float KnuckleChainT;                   //   seconds in that stage (host)
        public int KnuckleTarget;                     //   the enemy the dash locked onto, which the chain's moves turn to (replicated: your own screen marks it)
        public bool KnuckleArmed;                     // Arc Knuckles: a perfect parry armed the Counter Hammer, the next press fires it (replicated: the pips glow gold; ArcKnuckles.cs)
        public float KnuckleArmedT;                   //   until it fades unused (host)
        public float FangCd;                          // Twin Blades: until Twin Fang can be thrown again, counted from the catch (host)
        public byte FangOut;                          // Twin Blades: Twin Fang's sabres (replicated; TwinBlades.cs): 0 in hand, 1 winding up, 2 following the cursor, 3 flying back
        public V2 FangAt;                             //   where the spinning pair is (replicated; Game/TwinBladesLook.cs draws them there)
        public float LanceHookCd;                     // Lance: until Hook & Haul can be thrown again (host)
        public bool LancePrimed;                      // Lance: a hook landed, so the next press is the Breaching Thrust (replicated: the fourth pip glows; Lance.cs)
        public float LanceCd;                         // Lightbringer: until the Sunlance can be thrown again (host)
        public float MercyT;                          // a Lightbringer's ward (Mercy Cut, Radiant Noon) is on you for this long (replicated; Game/LightbringerLook.cs draws it)
        public byte Embers;                           // Censer: Embers lit by hits, spent by Censer Cast (replicated; Censer.cs)
        public float CenserCd;                        // Censer: until Censer Cast can be thrown again (host)
        // The Katana's two stances (Katana.cs; Docs/KatanaCombo.md): the stance (0 Hayate, 1 Senbon); where each stance's combo is waiting
        // (its next step: Hayate 0-3, Senbon 0-4; the stance you leave keeps its place); the last step thrown (stance * 8 + step) and the
        // steps that move you (wraps; your own game moves you along each when it changes).
        public byte KatanaStance, KatanaHay, KatanaSen, KatanaStep, KatanaDashes;  // (all replicated)
        public byte BoStep;                           // Bo Staff: which of slash, jab, spin comes next (0, 1, 2); there is no timer, a click always carries on (replicated)
        public float ShoveCd;                         // host: the Bo Staff's shove cools down for this long
        public float KatanaIdle, KatanaSwitchCd;      // host (Katana): time since the last step (the stance you're in starts over after a while); until the stance can switch again
        public float PreyT, PackT;                    // host: until the mark fades; the window for every wolf to bite
        public int HookId;                            // Kusarigama: the enemy the weight was thrown at, 0 for a miss (replicated; Kusarigama.cs)
        public float HookT;                           //   the throw plays for this much longer, KusariThrowTime down to 0 (replicated)
        public byte KusariStep;                       // Kusarigama: which piece of the combo landed last, 0 none, 1 the sickle cut, 2 the weight throw (replicated, drawn as pips behind you)
        public float KusariAim;                       // Kusarigama: the angle the weight was thrown along (replicated)
        public bool KusariWhirl;                      // Kusarigama: the weapon-action key is held, the weight circles you (replicated; you move slowly and can't swing)
        public float KusariHeld, KusariAng;           //   how long it has whirled, and where the weight is on its circle (replicated)
        public float KusariGap, KusariLock;           // host: seconds since the last piece; seconds before the whirl can start again
        public float KusariDmg, KusariD0;             // host: the throw's damage; how far its target was from you when it landed
        public bool KusariHit;                        // host: the throw landed and is dragging its target in
        // The Fuma Shuriken's Orbit stance (FumaOrbit.cs; Docs/FumaRework.md). Replicated: the stance (0 Throw, 1 Orbit); the move under
        // way (0 circling, 1/2 a Lash either way, 3 the Cyclone) and the next one (0-2); stars circling; stolen stars; the stance's clock,
        // and the orbit's anchor (angle FumaAng0 at clock FumaClk0, turning FumaSpin radians a second) so every screen works out the same
        // circle from the clock alone; where it came out from (FumaR0); when the move started and along which aim; when the last shot was stolen.
        public byte FumaStance, FumaMove, FumaStep, FumaStars, FumaMinis;
        public float FumaClock, FumaAng0, FumaClk0, FumaSpin, FumaR0, FumaMoveAt, FumaLashAim, FumaMiniAt;
        public float FumaFastT, FumaRevCd, FumaSwitchCd, FumaMoveDmg;  // host: Reversal's speed-up left, its cooldown, the switch's, the move's damage
        public readonly Dictionary<int, float> FumaCut = new Dictionary<int, float>();  // host: enemy id -> FumaClock of the star's last cut
        public readonly List<int> FumaMiniFrom = new List<int>();  // host: who fired each stolen shot
        public byte StarStance;                       // the Shuriken's stance: 0 the fan, 1 the Great Star (GreatStar.cs; replicated)
        public float StarSwitchCd, StarCatchCd;       // host: the stance switch's cooldown; the wait after the Great Star is caught
        public float AmbushT;                         // host: Vanish's double-damage hit is ready for this long
        public float DebtT;                           // Hemomancer: Blood Debt's window, paid-in-full bonus included (replicated; BloodDebt.cs)
        public bool BloodWallOn;                      // Blood Wall is being laid (replicated; BloodWall.cs)
        public float BloodWallClock;                  // seconds since it opened, which sets the drain (replicated)
        public V2 WallLast;                           // host: where the last node went down
        public int WallPrev = -1, WallCount;          // host: that node's zone id (-1: the next node starts a new wall); nodes in this wall so far
        public float WallPower = 1f;                  // host: the cast's mastery power
        public bool WallMastered;                     // host
        public float Debt, DebtOwed;                  // Blood Debt: health still owed, and what the cast took (replicated, to draw the ledger)
        public float DebtRepay = 1f;                  // host: Blood Debt's repayment multiplier (mastery, Usury, power)
        public GadgetId Gadget = GadgetId.None;       // the gadget in your free hand (replicated; Gadgets.cs)
        public float GadgetLeft;                      // its seconds of use left (replicated)
        public bool GadgetOn;                         // switched on: using up GadgetLeft (replicated)
        public readonly GadgetId[] BagKind = { GadgetId.None, GadgetId.None, GadgetId.None, GadgetId.None };  // gadgets carried besides the one in hand (replicated; Gadgets.cs)
        public readonly float[] BagLeft = new float[4];  // each one's seconds of use left
        public byte GadgetUses;                       // the gadget in hand's uses left (the Lodestone, the Medkit, the throwables; replicated)
        public readonly byte[] BagUses = new byte[4];  // each bagged gadget's uses left
        public float GadgetCd;                        // host: until the next throwable charge can go
        public bool SprayOn;                          // Arterial Spray is gushing (replicated; ArterialSpray.cs)
        public float SprayT, SprayTick, SprayPower = 1f;  // host: how long it has run, until the next damage tick, mastery power
        public bool SprayMastered;                    // host: a mastered spray washes away enemy bullets
        public float BroodCd;                         // host: Brood Mother's next turret while hitting a boss (Combat.cs)
        public int BroodDue;                          // host: Brood Mother turrets to place behind you this tick
        public float BoltStore, BoltCd;               // host: Ricochet on hits that can't bounce, stored toward the next bounce bolt (Combat.cs)
        public float BloodReserve;                    // Sanguine Orb: heal reserve charged by coating enemies (replicated, whole points)
        public float CoatT;                           // coated in a Sanguine Orb's healing blood: heals while it lasts (replicated)
        public int CoatBy;                            // host: who coated them
        public int Clot, ClotMax = 5;                 // Blood Lash: Clot stacks and the most it can hold (replicated)
        public float ClotT;                           // host: until the next Clot stack fades
        public readonly List<int> Chains = new List<int>();      // Blood Lash: the enemies chained (replicated; BloodLash.cs)
        public readonly List<byte> ChainLinks = new List<byte>(); // and the links on each (replicated)
        public readonly List<float> ChainT = new List<float>();  // host: until each chain's next link fades
        public bool LeashOn;                          // Martyr's Leash is held (replicated); LeashT: how long (replicated), LeashCd: host
        public float LeashT, LeashCd;
        public float GrazeGap;                        // host: until the next graze can count (Graze.cs)
        public int Grazes;                            // grazes this run: the host's count for everyone, a client's for its own player (Graze.cs; the HUD flashes the ultimate on a new one)
        public int BsTarget;                          // Blood Brawler: the marked enemy (replicated; BloodBrawler.cs); BsT: the thread's time left (replicated)
        public float BsT;
        public int BsHits;                            // host: hits landed in this string
        public float SympT, SympCd;                   // Sympathy: time left (replicated), cooldown (host)
        public int SympSrc;                           // the marked enemy the bound ones echo (replicated)
        public readonly List<int> SympBound = new List<int>();  // the enemies bound to it (replicated)
        public float HarvestClock, HarvestBlood, HarvestHold;  // Crimson Harvest (CrimsonHarvest.cs), all replicated: seconds since the cast, blood in the ball, and the hold's length (0 = no ball)
        public float GaleT, GaleAim;    // Gale Wall (GaleWall.cs), replicated: seconds the lens has left (0 = none), and the way it faces
        public V2 GalePos;            // ...where it was cast (replicated)
        public bool GaleWide;         // ...mastered: wider, holds more (replicated)
        public readonly List<int> GaleDots = new List<int>();  // ...each caught shot's place in it, two bytes (replicated; Game.GaleDot)
        public float GalePerShot;     // host: damage each caught shot adds (Momentum Bank)
        public float GlassT, GlassAim;  // Sanctum Glass (SanctumGlass.cs), replicated: seconds the pane has left (0 = none), and the way it faces
        public V2 GlassPos;           // ...its middle (replicated)
        public bool GlassWide;        // ...mastered: wider (replicated)
        public float GlassHealed, GlassCap;  // ...healing it has sent, and its cap (replicated: the cracks and the bar)
        public float WwAng;         // Water Ward (WaterWard.cs), replicated: how far the ring of drops has turned
        public readonly List<float> WwOff = new List<float>();   // ...each drop's place on the ring (replicated as a byte)
        public readonly List<float> WwFall = new List<float>();  // ...and its seconds still falling, 0 = landed (replicated as a byte)
        public bool WwWide;         // host: mastered, splashes and bursts reach farther
        public float RainClock;  // Sanguine Rain (SanguineRain.cs), replicated: seconds since the cast (0 = not raining)
        public float RainAcc;    // host: until the next heal tick
        public float JuggT, JuggArmor, JuggMax;  // the Juggernaut Suit (Juggernaut.cs), replicated: time left (0 = off), armour left, armour it went on with
        public bool JuggMastered;                // host: worn with the capstone (bigger shards)
        public int JuggShards;                   // host: flak shards thrown this run (tests, stats)
        public float TankT, TankArmor, TankMax;  // A Literal Tank (LiteralTank.cs), replicated: time left (0 = off), armour left, armour it came with
        public float TankCd;                     // host: until the main gun can fire again
        public float RocketT;                    // Rocket Drop (RocketDrop.cs), replicated: the launcher's time left (0 = off)
        public float StdShieldHp, StdShieldMax;  // the Aegis Standard's shield and its most (KnightBanners.cs), replicated (0 = none)
        public float StdShieldHitT; public bool StdShieldIn;  // host: seconds since a hit (its regen waits), inside an Aegis Standard last tick
        public float RocketCd;                   // host: until the launcher fires again
        public bool RocketMastered;              // host: taken with the capstone (longer, bomblets)
        public int RocketShots;                  // host: rockets fired (tests)
        public bool TankMastered;                // host: driven with the capstone (wider blasts)
        public int TankShells, TankCrushes, TankWrecks;  // host: shells fired, enemies run over, tanks blown up this run (tests, stats)
        public float TitheClock;  // Vein Tithe (VeinTithe.cs), replicated: seconds since the cast (0 = not holding anyone)
        public readonly List<int> Tithed = new List<int>();  // Vein Tithe: the enemies held right now, in the order the tendrils reach them (replicated)
        public bool HarvestMastered;  // (host)
        public readonly List<int> Grasp = new List<int>();  // Leech Tendrils: the enemies held right now (replicated)
        public readonly List<float> GraspCharge = new List<float>();  // Leech Tendrils: how far each held enemy has built up toward its blast, 0..1, parallel to Grasp (replicated)
        public float GraspCd, GraspDmg;  // host: Leech Tendrils' cooldown left after a blast or a release, and the damage a full blast does per tendril
        public float CaltropPower = 1f; public bool CaltropMastered;  // host: Caltrop Run's cast
        public readonly float[] WallPlateHp = new float[WallPlates];  // host: Shield Wall plate health
        public float WallPlateMax;                                    // host: what each plate started with
        public bool WallReflect;                         // host: mastered Shield Wall
        public int SwingCount;  // host: Knight weapon state
        public byte StaffStacks;     // Quarterstaff: Jab stacks stored, 0 to 3 plus one per extra projectile (replicated, drawn on the staff and as pips behind you)
        public float StaffJabCd;     // host: the Quarterstaff's jab cools down for this long
        public byte TwinNades;       // Twin Rifle: grenades loaded in the tubes, 0 to 2 (replicated; TwinGrenades.cs)
        public byte TwinNext;        // Twin Rifle: the tube the weapon-action key fires next, 0 left, 1 right (replicated)
        public byte TwinCharge;      // Twin Rifle: hits toward the next grenade, 0 to TwinNadeHits (replicated)
        public float TwinNadeCd;     // host: the tubes can't fire again for this long
        public float TwinAimL, TwinAimR;  // Twin Rifle Auto: each gun's heading relative to your facing, radians, + toward your right (replicated; TwinAuto.cs)
        public int TwinLock;         // Twin Rifle Auto: the enemy id both guns are locked on, 0 none (replicated)
        public float BotCd;          // host: Engineer: seconds until a broken robot can be summoned again (EngineerBots.cs)
        public bool BotFireWas;      // host: Engineer robot weapons: Fire was held last tick (a fresh click on an enemy sends the robot at it)
        public float BotSummonT;     // host: a robot was just summoned: no attack for this long (or until Fire is let go)
        public float DraftCarry;     // host: Engineer: Draftsman's part-vectors not yet paid
        // The Arcanist (Arcanist.cs). Replicated (SyncArcanist): the enemy Kinetic Grasp holds (0: none), the Orbit Shards' guard
        // (Alt held), Runic Array's time left. Host: how long the grasp has held.
        public int KgHeld;
        public bool ShardGuard;
        public float ArrayT, KgT;
        public float TwinAutoCd;     // host: Alt can't lock or let go again for this long
        public byte AmmoMove, AmmoSeq;   // Out of Ammo: the rifle arms' move running (1-4: left bash, right bash, left bash, rifle clap; 0 none) and a count that rises with every move started (replicated; OutOfAmmo.cs)
        public byte KnifeMove, KnifeSeq; // Out of Ammo: the knife's move running (1-3: cross cut, reverse cut, lunge stab; 0 none) and its count (replicated)
        public int KnifeCombo;           // host: the next knife move, 0-2
        public float KnifeT, KnifeIdle;  // host: seconds into the knife move, and since the last one
        public bool KnifeHit;            // host: this knife move has landed
        public byte SwatSeqL, SwatSeqR;  // Out of Ammo: counts that rise with each rifle's swat (replicated)
        public float SwatAngL, SwatAngR; // Out of Ammo: the world angle each rifle last swatted at (replicated)
        public float AmmoSpin;           // Out of Ammo: seconds of Alt's spin left, 0 none (replicated)
        public int AmmoCombo;            // host: the next move of the chain, 0-4
        public float AmmoT, AmmoIdle, AmmoCd, AmmoSwatCdL, AmmoSwatCdR, AmmoTick;  // host: seconds into the move, since the last one, Alt's cooldown, each rifle's swat recovery, the spin's tick
        public bool AmmoHit;             // host: this move has landed
        public byte ScatShells;      // Scattergun: Shock Grenades loaded, 0 to 3, refilling over time (replicated; TankAlts.cs)
        public float ScatAcc;        // host: ScatCharge's exact value (the byte would drop each tick's fraction at the slow reload)
        public byte ScatCharge;      // Scattergun: percent toward the next grenade (replicated)
        public float ScatCd, MineCd, GasCd;  // host: the Tank Soldier's Alt fires cool down (TankAlts.cs)
        public float ChalkIgniteCd;  // host: the Spirit Chalk's Alt (ignite your loops) cooling down (Chalk.cs)
        public float NimbusStormCd, NimbusStormT, NimbusZapT;  // host: the Nimbus's Eye of the Storm cooling down, blowing, until its next bolt (Nimbus.cs)
        public float BoneWallCd, BoneLinkT;  // host: the Bone Guard's Ossuary Wall cooling down; time left to link your next dart to the last (BoneGuard.cs)
        public int BoneLastHit = -1;         // host: the enemy your last Bone Guard dart hit
                public byte RepelMeter;      // Repulsion Cannon: the Undertow meter, 0 to 100, filled by blasts that hit (replicated; Repulsion.cs)
        public float RepelMeterF;    // host: the exact meter
        public byte RailHeat;        // Railgun: the heat meter, 0 to 100, filled by shots, emptied by Heat Dump (replicated; RailBeam.cs)
        public float RailHeatF;      // host: the exact heat
        public float DumpCd;         // host: Heat Dump's cooldown
        public bool RepelPull;       // Repulsion Cannon: the weapon-action key is held and the Undertow is pulling (replicated; you move at 75% and can't fire)
        public float RepelTickT;     // host: till the pull's next sting
        public float RepelHumT;      // host: till the pull's hum plays again
        public byte GreatStep;       // Greatsword: which cut of the chain landed last, 0 to 3 (replicated, drawn as pips behind you)
        public float BlinkBoost = 1f; // host: Shadow Mark's extra on the hit after a Kunai blink
        public float LightCarry;     // host: Lightbringer light toward the next pip (Daybreak fills it in fractions)
        public float GreatGap;       // host: seconds since the last cut
        public int RapierTempo;      // Rapier: Tempo stacks, 0 to Game.RapierTempoMax: one per enemy a thrust hits; each makes the thrusts quicker (replicated, drawn as diamonds behind you; Rapier.cs)
        public float RapierTempoF;   // host: the exact Tempo (it drains a little at a time)
        public float RapierHitAgo = 99f;  // host: seconds since a Rapier thrust last hit an enemy (Tempo holds for RapierGrace, then drains)
        public byte RapierGraceLeft; // Rapier: how much of the grace period is left, 0 to 255 (replicated: the arc under the diamonds; 0 = draining)
        public bool RapierFireWas;   // host: fire was held last tick (a click is the press)
        public bool RapierGuard;     // Rapier: the weapon-action key is held, the Riposte stance is up (replicated; you move slowly and reflect bullets)
        public byte RapierCharge;    // Rapier: the stance's charge, 0 to 255 (replicated: the lunge's reach; drawn on the blade and as an arc)
        public float RapierChargeF, RapierLungeEl, RapierLungeDur, RapierLungeLen, RapierLungeDmg;  // host: exact charge; the lunge's clock, length, damage
        public V2 RapierLungeFrom, RapierLungeDir;  // host: where the lunge started and which way
        public readonly HashSet<int> RapierLungeHit = new HashSet<int>();  // host: enemies this lunge already hit
        public int RapierFlourishLeft, RapierFlourishCharge;  // host: Flourish swings still to come, and the Tempo it spent
        public float RapierFlourishT, RapierFlourishAim;      // host: until the next Flourish swing; where it is aimed
        public float MeleeGraceT;   // host: a melee swing just landed: bodies you're touching can't hurt you for a moment (Balance.MeleeContactGrace)
        public float BossContactT;  // host: a boss's body can only hurt you once a second
        public float StunT;         // stunned by the Warden's stun shot: can't move (replicated; your own game stops you)
        public float BashGraceT;    // host: until a Riot Shield can bash this player again (WardenFaction.cs)
        public float BleedT, BleedDps;  // host: a Rioter's bottle cut them: bleeds this much a second for this long (WardenFaction.cs)
        public float WarpT;         // beamed into the hub before a boss wave: can't move or shoot (replicated; BossBeam.cs)
        public float PvpStunImmuneT;  // host: PvP: no new stun from another player until this runs out (Pvp.cs)
        public float PvpSlowT, PvpSlowMult = 1f;  // PvP: slowed by another player's attack, this long, to this speed (replicated; Pvp.cs)
        public float SwallowT;      // inside the Devourer: hidden, can't fire or be hit from outside (replicated; your own game holds you in it)
        public float UltT;                     // ultimate time left (Fortress: its dome's time, too)
        public float RodT;                     // Lightning Rod: seconds of charge left (replicated; LightningRod.cs)
        public float VortexT, VortexAge, VortexR;  // Vortex (Vortex.cs), replicated: time left (0 = off), seconds since the cast, its radius
        public float VortexTickT, VortexHumT;  // host: the damage tick's clock, the hum's
        public bool VortexUp, VortexMastered;  // host (VortexUp: it blows, so it flings when it ends)
        public float ArmT, ArmAge;    // Iron Armadillo (IronArmadillo.cs), replicated: time left (the curl included; 0 = off), seconds since the cast
        public float ArmSpd, ArmBashCd;  // host: its rolling speed (from how you move), until the next wall bash counts
        public V2 ArmLast;            // host: where you were last tick
        public bool ArmMastered;      // host
        public int ArmHits, ArmBashes, ArmRicochets, ArmSlams;  // host: counts (tests)
        public float BoltT, BoltAge;  // Living Bolt (LivingBolt.cs), replicated: time left (the channel included; 0 = off), seconds since the cast
        public float BoltLockT;       // host: until the next dash is taken
        public bool BoltUp, BoltMastered;  // host (BoltUp: electrified, so it ends with the discharge)
        public float RvT, RvLen;  // Blood Ravager (BloodRavager.cs), replicated: seconds since the cast (0 = off), its length
        public float RvPw, RvLashCd;  // host: the ultimate's power at the cast, until the next lash is taken
        public RavagerGait RvGait;  // host: the legs' walk (where a foot lands, it stomps)
        public readonly List<Game.RavagerLash> RvLashes = new List<Game.RavagerLash>();  // host: the legs lashing now
        public int HorseId;                    // Trojan Horse (TrojanHorse.cs), replicated: the horse zone you ride in (0 = none)
        public float SallyT;                   // replicated: Sally Forth's damage boost left
        public float HorseCd;                  // host: time to your next arrow from the horse
        public int HorseBoards, HorseArrows;   // host: times you climbed in, arrows shot (tests, stats)
        public float BoilT, BoilLen;  // Blood Boil (BloodBoil.cs), replicated: seconds since the cast (0 = off), its length
        public int RvLashCount, RvLashHits, RvStomps, RvTrampled, RvRams;  // host: lashes, lash hits, steps landed, enemies stomped, rams (tests)
        public float RmT, RmAng, RmLen;  // Rodent Minigun (RodentMinigun.cs), replicated: seconds since the cast (0 = off), the portal's heading, its full-fire time
        public float RmAcc, RmPw;        // host: rodents owed to the next tick, the cast's mastery power
        public float FpT, FpAge, FpStageT;  // Forbidden Path (ForbiddenPath.cs), replicated: beast time left (0 = off), seconds since the cast, seconds into the stage
        public byte FpStage;                // ...the combo stage running (0: none, 1 Pounce .. 5 Gate; replicated)
        public int FpTarget = -1;           // ...the locked enemy's id (-1: none; replicated)
        public V2 FpAt, FpFrom;             // ...the stage's anchor (the target where it started) and where you stood (replicated)
        public byte FpNext = 1;             // host: the stage the next Fire starts
        public float FpIdle;                // host: seconds since the last stage ended (the combo starts over after FpReset)
        public bool FpUp, FpMastered;       // host
        public readonly int[] FpShades = new int[3];  // host: Shadow Rend's three targets (ids; -1: none)
        public int FpHits, FpKills, FpStages;  // host: combo hits, kills and stages this run (tests)
        public int RodentsFired;         // host: rodents fired this run (tests, stats)
        public int MammothTramples;      // host: enemies a Mammoth Fall trampled this run (tests)
        public int BoltDashes, BoltShocks, BoltResidueHits, BoltDischarged;  // host: dashes, enemies shocked, residue ticks landed, enemies discharged into (tests)
        public int VortexHits, VortexSwept, VortexFlung;  // host: damage ticks landed, shots swept away, enemies flung (tests)
        public float ColT, ColAge, ColWind;    // Stone Colossus (StoneColossus.cs), replicated: time left (0 = off), seconds since the cast, the slam (0..1)
        public float ColSlamT = -1f, ColSlamCd, ColSlamAng, ColWalk;  // host: the slam's clock (-1 = none), its cooldown and heading; px walked toward the next step
        public V2 ColLast;                     // host: where you stood last tick (the steps)
        public int ColSide, ColStomps, ColSlams;  // host: the foot that came down last; stomps and slams this run (tests)
        public bool ColSlamHit, ColMastered, ColUp;  // host (ColUp: it stands, so it crumbles when ColT runs out)
        public float GolT, GolAge, GolCoats, GolPunch;  // Blood Golem (BloodGolem.cs), replicated: time left (0 = off), since the cast, armour in coats (the gather's count before it forms), the punch number + its swing 0..1
        public float GolArmor, GolPer, GolPunchCd, GolSwingT = -1f, GolClotCd, GolPaid;  // host: the armour, a coat's worth, the swing's cooldown and clock, Hurl Clot's cooldown, coats paid for
        public int GolStep, GolNeed = -1, GolShown, GolBurst, GolRipped, GolPunches, GolSlams, GolKills;  // host: punches thrown, coats to pay, coats shown (the shedding), the last collapse's coats; counts for tests
        public bool GolUp, GolMastered;  // host (GolUp: on, so it collapses when it ends)
        public float HbT;                       // Heartbeat (Heartbeat.cs), replicated: seconds since the cast (0 = off)
        public bool HbMastered;                 // host
        public int HbBeatCount, HbHits, HbOrbs; // host: beats, wave hits and blood orbs from its kills this run (tests)
        public float HbPaid;                    // host: health the beats have cost this run (tests)
        public float BubT, BubR, BubCharge;      // Static Bubble (StaticBubble.cs), replicated: time left (0 = off), its size, its charge
        public float RushT, RushLen;           // ...Static Rush from its burst, replicated: time left, how long it was
        public float BubZapT;                   // host: until the next feed crackle
        public bool BubUp, BubMastered;          // host
        public int BubEaten, BubFed, BubBursts, BubFizzles, BubRushed;  // host: shots eaten, enemy touches, full bursts, smaller pops, Static Rushes given this run (tests)
        public float ApT, ApLinkT;            // Astral Projection (AstralProjection.cs), replicated: time left (0 = off); on a host, held up while a tether is on them
        public int ApHost;                     // ...the player the spirit rides (your own id: your body; replicated)
        public V2 ApPos;                       // ...the spirit (replicated)
        public float ApFireCd, ApSwapCd;       // host: until the next bolt, until the next switch
        public bool ApUp, ApMastered, ApParryWas;  // host (ApParryWas: the parry key was held last tick)
        public int ApBolts, ApSwaps;           // host: bolts and switches this run (tests)
        public float ApHealed;                 // host: HP healed this run (tests)
        public float SpT, SpAge, SpWind, SpAng;  // Spirit Avatar (SpiritAvatar.cs), replicated: time left (0 = off), seconds since the cast, the cleave (0 none, 0..1 wind-up, 1..2 sweep), its heading
        public float SpCastT = -1f, SpCd;      // host: the cleave's clock (-1 = none), its cooldown
        public int SpCleaves, SpSwallowed;     // host: cleaves and shots swallowed this run (tests)
        public bool SpMastered, SpUp;          // host (SpUp: it's out, so it bursts when SpT runs out)
        public readonly HashSet<int> SpHit = new HashSet<int>();  // host: enemies this cleave has struck
        public int RodArcs;                    // host: how many enemies each bolt arcs on to (2, 3 mastered)
        public byte HeliGuns;                  // Attack Helicopter: its guns (1, 2 mastered; 0 = not out), replicated (AttackHelicopter.cs)
        public int HeliTgt, HeliTgt2;          // Attack Helicopter: each gun's target's enemy id (0 = none), replicated
        public float HeliLock, HeliLock2, HeliCd, HeliCd2, HeliSpin;  // host: each gun's lock-on and next round, the barrels' spin-up
        public int HeliRounds;                 // host: rounds that hit (tests)
        public bool DogmaOn;                   // Dragon's Dogma: the dragon is out (replicated; DragonsDogma.cs)
        public V2 DogmaHead;                   // Dragon's Dogma: its head (replicated while it's out)
        public int DogmaTgt;                   // Dragon's Dogma: the enemy it's after (0 = none; replicated while it's out)
        public float DogmaAng, DogmaAge;       // host: its heading, seconds out
        public bool DogmaHealOn;               // host: mastered (each enemy devoured heals)
        public int DogmaEaten, DogmaBites;     // host: enemies devoured, big ones bitten (tests)
        public readonly System.Collections.Generic.List<V2> DogmaTrail = new System.Collections.Generic.List<V2>();  // every game: the points the head has passed (UpdateDogmaView)
        public V2 DogmaDisp;                   // every game: the head on screen
        public float DogmaSince, DogmaSink;    // every game: seconds since it came out; how far the body has drained back in
        public float DomeT;                    // Fortress: the dome's time left (replicated; Dome.cs)
        public V2 DomePos;                     // Fortress: where the dome stands (replicated)
        public int EchoLeft;                   // Riot Repeater: rounds still to fire in this burst
        public float EchoT, EchoGap;
        // The Revolver (Weapons.cs, PistolWhip.cs). Replicated: the stance (0 Gunslinger, 1 Fan), the hammer (cocked, or the pull's
        // time left), the Fan stance's step (RevWhip... RevFan) and its clock, and the Fan the Hammer's reload left.
        public byte RevStance, RevStep;
        public bool RevCocked = true;
        public float RevCockT, RevT, RevFanReload;
        public bool RevGuardWas, RevFanByParry;  // host: the parry was up last tick; the fan under way was fired with the parry up
        public int RevFanLeft;                   // host: fan rounds still to go
        public float RevFanNext, RevFanAim, RevBotClick;  // host: until the next fan round; the fan's aim; a bot's next click
        public int DbUsed, DbBarrel, DbShot;   // Double Barrel: shells spent, the barrel that fires next, volleys fired (replicated; DoubleBarrel.cs)
        public float DbReload;                 // Double Barrel: reload time left (replicated)
        public byte TpSide;                    // Twin Pistols: the gun that fires next, 0 left, 1 right (replicated; TwinPistols.cs)
        public float TpLaserT, TpCd;           // Twin Pistols: Laser Sights time left, and until the key works again (replicated)
        public int GlMode, GlUsed, GlSmokeUsed, GlShot;  // Grenade Launcher: drum (0 HE, 1 smoke), HE rounds spent, smoke canisters spent, rounds fired (replicated; GrenadeLauncher.cs)
        public float GlReload, GlSwap, GlCharge;  // Grenade Launcher: reload and drum swap time left, progress to the next canister 0-1 (replicated)
        public int SsMask, SsSide = Game.SsPerSide;  // Stonespear: the spears up (a bit a slot) and how many a side (replicated; Stonespear.cs)
        public float Cinder = Game.CinderMax;  // Wyvern Staff: the Cinder meter (replicated; WyvernStaff.cs)
        public float WyvSlamCd, WyvTick, WyvFoes;  // host: the Wyrm Slam cooling down, the breath's next tick, enemies in the gale (WyvernStaff.cs)
        public float BumbleCd;  // host: the Bumble Remote's Swarm Burst cooling down (BumbleRemote.cs)
        public float HoneyFill = Game.HoneyMax;  // Honeybee Staff: the honey meter (replicated; HoneybeeStaff.cs)
        public bool HoneyOn;                   // Honeybee Staff: spraying (replicated)
        public float HoneyWait, HoneyGlobT;    // host only: until the meter refills; until the next glob
        public bool HoneyHeld;                 // host only: Alt is held (a dry staff refills once it's let go)
        public bool BbBrace;                   // Bulwark Barrier: braced, the weapon-action key held (replicated: the slow, the wide guard, the look; BulwarkBarrier.cs)
        public float BbRam;                    // Bulwark Barrier: Ram soaked into the shield, 0-100 (replicated: the pips)
        public byte BbBashes;                  // Bulwark Barrier: Shield Bashes started (replicated; your own game dashes when it changes)
        public V2 BbBashTo;                    //   where the last one ends (replicated)
        public bool HbSet;                     // Halberd: the pike is set, the weapon-action key held (replicated: the slow, the look; Halberd.cs)
        public float HbPikeA;                  // Halberd: where the set pike points, turning on its own to the nearest enemy (replicated)
        public bool HbHeld;                    // host: the Halberd's weapon-action key is down (the pike is set again after a stun)
        public float HbPokeCd;                 // host: until the set pike can poke again
        public int HbPokes, HbLunges;          // host: pokes and Pike Lunges so far (tests)
        public float HbLungeCd;                // host: until letting go of the pike lunges again
        public bool BbHeld;                    // host: the weapon-action key is down (the Brace comes back up after a bash or a stun)
        public float BbMeleeT;                 // host: until a body leaning on the braced plate counts again (BulwarkMelee)
        public float BbLock, BbIdle, BbBlockT; // host: until the Brace can rise again, since it dropped (Ram drains after BbRamHold), until a block can sound again
        public float BbSwingT = 5f, BbSwingDur;  // host: since the last chain step and its length (the guard narrows while it plays)
        public int BbStored;                   // host: shots soaked since the last bash (the shards a full bash throws)
        public int FlStep;                     // Frost Lance: the chain's next step, 0-4 (host; FrostLance.cs)
        public float FlIdle;                   // Frost Lance: since the last step (host: FlComboReset starts the chain over)
        public bool FlRime;                    // Frost Lance: a perfect parry primed the Rime Counter (replicated: the glow)
        public float FlZeroT, FlZeroCd;        // Frost Lance: Absolute Zero playing (counts down from ZeroDur), its cooldown (replicated)
        public V2 FlZeroAt;                    // host: where Absolute Zero was cast
        public int FlZeroStage;                // host: Absolute Zero's waves gone (0, 1, 2)
        public float BlastTideCd;              // Blood Blast: Crimson Tide's cooldown (host; BloodBlast.cs)
        public byte BlastStep;                 // Blood Blast: the chain's last step, 1-6 (0: none yet; host; BloodBlast.cs)
        public int WcStep;                     // Wind Clash: the combo's next step, 0-3 (host; WindClash.cs)
        public float WcIdle;                   // Wind Clash: since the last step (host: WcComboReset starts the combo over)
        public float WcTempestT, WcTempestCd;  // Wind Clash: the Tempest blowing, its cooldown (replicated: the slow, the storm, the HUD ring)
        public float WcTempestTick;            // host: until the Tempest's next damage tick
        public int SsLock;                     // Stonespear: the enemy a volley is going at, 0 none (replicated: the lock ring)
        public float SsAge, SsCdT, SsWindT, SsVolleyT;  // host only: since the slam landed, until the next slam, the wind-up left, until the next volley spear
        public bool SsHad;                     // host only: spears were up last tick
        public V2 SsLockAt;                    // host only: where the volley was aimed
        public bool NunGuard;                  // Nunchaku: the Twin Windmill guard is up (replicated; you move slower, the discs swat shots back)
        public int NunSwats;                   // Nunchaku: shots swatted back since the last Twin Counter, up to NunSwatCap (replicated: the pips)
        public float NunSwitchCd;              // host only: until the weapon-action key switches stance again
        public readonly Dictionary<int, float> NunDiscHit = new Dictionary<int, float>();  // host only: each enemy a disc hit, and how long until it can again
        public float NunAttackT;               // host only: a Twin Counter is playing (the discs are down: nothing is swatted)
        public byte NodStance;                 // Nodachi: 0 Arashi (Storm), 1 Mamori (Guard) (replicated; Nodachi.cs)
        public byte NodStorm, NodGuard;        // Nodachi: each stance's place in its combo, the step the next press plays (replicated)
        public byte NodCode, NodCount;         // Nodachi: the last step's code (stance * 8 + step) and a count of steps (replicated: your game moves you)
        public float NodT = 99f, NodSwitchCd;  // host only: since the last step, until the key switches again
        public byte VgStance;                  // Vowguard: 0 Valor, 1 Sanctuary (replicated; Vowguard.cs)
        public byte VgAtV, VgAtS;              // Vowguard: each stance's place in its chain, the step the next press plays (replicated)
        public byte VgMoveId = (byte)VgMove.None, VgCount;  // Vowguard: the last step's move and a count of steps (replicated: your game moves you)
        public byte VgFaith;                   // Vowguard: Faith, 0-6, spent by finishers (replicated: the pips)
        public V2 VgDashTo;                    // Vowguard: where the last dash or hop ends (replicated)
        public byte[] VgChain = VowguardMotion.DefaultPack;  // Vowguard: their chains (VowguardMotion.Pack), from the run's start on every machine
        public float VgT = 99f, VgDur, VgIdle, VgSwitchCd, VgBlockT;  // host only: the step's clock and length, since it ended, the key's and the block sound's cooldowns
        public byte VgSoak;                    // host only: shots Bulwark Absorb soaked this step
        public int VgCoverId;                  // host only: the ally the last Cover Ally went to
        public float VgCoverT;                 // host only: Cover Ally's guard on this player
        public readonly int[] EsSt = new int[2];          // Embolus Spears: each hand's spear (Game.EsInHand, EsFlying, EsStuck, EsReturning, EsHardening; replicated; EmbolusSpears.cs)
        public readonly V2[] EsPos = new V2[2], EsDir = new V2[2];  // Embolus Spears: each spear's middle and the way its point faces (replicated)
        public readonly float[] EsPaid = new float[2], EsT = new float[2];  // host only: the health each spear cost; its distance flown, or time stuck or hardening
        public readonly HashSet<int>[] EsHits = { new HashSet<int>(), new HashSet<int>() };  // host only: the enemies each spear has hit this pass
        public bool EsWasFiring;               // host only: Fire was held last tick (a fresh click recalls a spear still flying)
        public int NsCount;                    // Needle Swarm: needles in the swarm (replicated; NeedleSwarm.cs)
        public readonly int[] NsTen = new int[3];         // Needle Swarm: the enemy each tendril reaches for or wraps, 0 none (replicated)
        public readonly float[] NsLen = new float[3];     // Needle Swarm: how far each tendril has grown, px (replicated)
        public readonly int[] NsBound = new int[3];       // Needle Swarm: needles wrapped round each tendril's enemy (replicated)
        public float NsBurstCd;                // Needle Swarm: Alt's cooldown (replicated)
        public bool NsReady;                   // host only: the swarm was filled when the weapon came out
        public float NsAcc, NsPaid, NsPaidT;   // host only: the siphon's part-needle, the health it cost lately and that spurt's clock
        public readonly float[] NsWrapAcc = new float[3], NsHurtT = new float[3], NsBleedT = new float[3];  // host only: each wrap's needles arriving, cuts and bleed clocks
        public readonly List<int> SsQueue = new List<int>();  // host only: the volley's slots still to go, in order
        public readonly List<int> ArcTags = new List<int>();  // Arc Lightning: the enemies tagged this draw, in order (replicated; ArcLock.cs)
        public float ArcTagT, ArcIdleT, ForkCdT;  // host only: until the next tag, how long tags have sat with no draw, Forked Volley's cooldown
        public V2 ArcFrom;                     // host only: where the chain's next hop leaves from
        public readonly List<Game.ArcHit> ArcHits = new List<Game.ArcHit>();  // host only: chain and fork hits still to land
        public bool WasFiring, TapQueued;      // host: Fire last tick (a click is a new press); a click kept until it can be used (Revolver, Katana, ...)
        public float DrawT;                    // Mender's Bow: how long the current draw has been held (replicated; your own is local)
        public float BowRelease;               // Mender's Bow (host): a release waiting to fire, as its draw time; the shooter measures it
        public float BowShotDraw;              // Mender's Bow (host): the draw of the arrow being fired
        public bool BowFocus;                  // Mender's Bow (host): the release waiting to fire was timed in the Focus bar's zone (BowFocus.cs)
        public bool BowShotFocus;              // Mender's Bow (host): the arrow being fired is a Focus shot
        public float ScatterCd;                // host: the Mender's Bow's Scatter cools down for this long (BowFocus.cs)
        public bool Parrying;                  // holding the parry shield up (replicated)
        public float ParryHp = Game.ParryCapacity;  // damage the shield can still take (replicated)
        public float CounterT;                 // host: Counterstance fire-rate boost time left
        public float ParryBroken;              // seconds until a broken shield can be raised again (replicated)
        public float ParryHeld, ParryDown = 10f;  // host: how long it's been up / down
        public bool ParryPerfect;              // host: raised fresh, so the first moment reflects
        public float ParryBumpT;               // host: a melee hit on the shield counts once per bump
        public bool ParryActive => Parrying && ParryBroken <= 0f && !Downed && ApT <= 0f;  // (Astral Projection: the parry key switches teammate instead)
        public float BarrierHp, BarrierMax, AegisHp;  // damage the Barrier / Aegis can still soak (host)
        public bool BarrierReflect;                   // host: a mastered Barrier sends bullets back
        public float AegisReflectMult = 1f;           // host: a mastered Aegis reflects twice as hard
        public Dictionary<AbilityId, float> MasteryXp = new Dictionary<AbilityId, float>();  // ability mastery (Mastery.cs)
        public float BarrierFrac;                     // replicated BarrierHp / BarrierMax, for drawing
        public bool ShellOn;                   // Blood Shell is up (replicated; its wear is your health)
        public bool ShellMastered;  // host: a mastered shell erases bullets as it bursts
        public float ShellT, ShellPower = 1f;  // host: how long it has stood; mastery power
        public bool StandOn;                   // Last Stand: planted behind the barrier (replicated; its wear is your health)
        public float StandAim;                 // Last Stand (replicated): where the barrier faces; it turns slowly toward your aim
        public float StandT;                   // host: how long you have been planted
        public bool StandMastered;             // host: a mastered Last Stand takes 5% instead of 10%
        public float ShieldHp, ShieldT;        // Energy Ward
        public float GustT;                      // a Gust Gate's speed boost left (GustGate.cs); replicated, for the speed and the look
        public float GustCd;                     // host: time before another Gust Gate pass can boost
        public V2 GustLast; public bool GustSeen; // host: where this player stood at the last zone tick (and whether that was set), for Gust Gate's crossing test
        public float WardT, WardFrac, WardInvuln;  // Martyr's ward: going down while it lasts gets you straight back up (WardT replicated)
        public float CompanionCd;              // host: summoner companion timer

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public int Gold;                       // the Bazaar's gold (Shop.cs, replicated)
        public RelicId Relic = RelicId.None;   // the relic slot (Relics.cs, replicated)
        public float OverHp;                     // health past max from a health shrine: soaked by damage after shields, never decays, lost when downed (Shrines.cs, replicated)
        public ulong ShrinesUsed;                // the health shrines this player has used, a bit per shrine (Shrines.Bit; replicated and saved)
        public readonly byte[] Stash = new byte[Market.Count];  // Black Market buys not yet placed, by BuildKind (Market.cs, replicated)
        public readonly int[] Hand = new int[Expansion.HandMax];  // room cards held, Piece values, empty slots 0 at the end (Pieces.cs, replicated)
        public bool CardsDealt;  // host: this player has had the starting hand (Pieces.cs, not replicated)
        public int Vectors = Game.VectorStart, VectorCap = Game.VectorCapBase;  // the Expanding arena's drawing budget and the most you hold (ExpansionGame.cs, replicated)
        public int MapSpent;  // host: vectors this player spent on the map in play; half come back when it resets (MapReset.cs, saved, not on the wire)
        public readonly int[] Shelf = NewShelf();  // this player's shop: upgrade ids, the first Game.ShelfLen of them are live (replicated)
        public int ShelfSold;                  // bit per shelf slot: bought (replicated)
        public int ShelfLen;                   // cards on the shelf now (replicated; Game.MaxShelf at most)
        public int Restocks;                   // Add stock bought this visit (replicated; each costs more, no limit)
        static int[] NewShelf() { var a = new int[Game.MaxShelf]; for (int i = 0; i < a.Length; i++) a[i] = -1; return a; }
        public bool ShopDone;                  // clicked Done shopping (replicated)
        public bool ReviveToken;               // bought at the Bazaar: get back up once by yourself (replicated)
        public bool SecondWind;                // the Second Wind modifier: get back up once by yourself this boss cycle (replicated)
        public float TokenT;                   // host: how long you've been down with a token
        public float Xp;
        public int[] Offer;  // three upgrade ids while choosing, otherwise null
        public bool OfferLegendary;  // the current offer is a boss reward
        public int PendingLegendary; // boss rewards still to pick (host keeps the queue below)
        public readonly List<EnemyType> LegendaryQueue = new List<EnemyType>();
        public int PendingMods;      // weapon mods still to pick (one per boss, after its legendary)
        public bool OfferMod;        // the current offer is the weapon mod list
        public int PendingCards;     // boss card picks still to make (Expanding arena, room cards on: one per boss, after its legendary; BossCards.cs)
        public bool OfferCards;      // the current offer is a card pick (boss or wave): Offer holds BossCards codes, not upgrade ids
        public int WavePicks;        // how many of PendingCards are wave card picks (2 cards after each wave, taken after the boss's; Hand.cs)
        public bool OfferWave;       // the current card offer is a wave card pick (no reroll)
        public int PendingMilestones;  // milestone picks still to take: one every 10 levels, and lucky ones from the Milestone Compass
        public bool OfferMilestone;  // the current offer is from the milestone pool
        public int PrizePicks;       // challenge room prizes still to pick: rare or better (Challenges.cs)
        public bool OfferPrize;      // the current offer is a challenge room's prize
        public int StunShots;        // Stun Gun: bolts left while it's out (replicated; Stun.cs)
        public float GuardCd;        // host: until the next Warden's Guard arrives
        public readonly EnemyType[] PactType = new EnemyType[2];  // host: Force Pact: the enemy type each pact slot holds (slot 1 is the mastered second thrall; ForcePact.cs)
        public readonly bool[] PactSet = new bool[2];
        public readonly float[] PactCd = new float[2];    // host: until the slot's thrall arrives
        public readonly V2[] PactAt = new V2[2];          // host: where a freshly bound thrall appears (it is bound where the enemy stood)
        public readonly bool[] PactBinding = new bool[2]; // host: the pact is still binding (PactAt), not respawning at your side
        public byte PactRez;         // replicated: how far along the soonest respawn is, 1..255 (0: none waiting; Game/ForcePactLook.cs)
        public float DroneCd, DroneBossCd, BomberCd;  // host: Hive Drones (next one back; next sting while hitting a boss) and Bomber Escort (QueenLoot.cs)
        public V2 LightPos, LightDisp;  // Searchlight: its pool (replicated), and where this screen draws it (Stun.cs)
        public bool RadiantOn;              // Radiant Orb: held out (replicated; RadiantOrb.cs)
        public bool StaticOn;               // Static Shock: the cone is out (replicated; StaticShock.cs)
        public float OverchargeT;           // Overcharge from a Static Shock: time left (replicated)
        public bool TtCharging;             // Twin Tessen: the Kamikaze Gust held back (replicated; TwinTessen.cs)
        public float TtCharge, TtGustCd;    // Twin Tessen: how long it has been held, until the gust can go again (replicated)
        public float TtTailT;               // Tailwind from a Twin Tessen: time left (replicated; faster moving)
        public float CrSurgeT, CrSurgeMax;  // a Chakra Surge from Chakra Reserves: time left, and how long it was (replicated; ChakraReserves.cs)
        public byte TtBreeze;               // Twin Tessen: Breeze, 0-100 (replicated: the meter, a full bar makes the next gust a Tempest)
        public float TtBreezeF;             // host: Breeze unrounded
        public byte HfMode, HfBpm = 60, HfBeats;  // Heart Flail: rest/swing/spin/thrown/rooted/reeling, beats a minute, beats so far (replicated; HeartFlail.cs)
        public float HfT, HfAng, HfCd;      // Heart Flail: time in the mode, the heart's angle round you, Open Heart's cooldown (replicated)
        public int HfSide;                  // Heart Flail: the swing's side, +1 or -1 (replicated)
        public float HfAim0;                // Heart Flail: the aim the swing started on (replicated)
        public V2 HfPos;                    // Heart Flail: where the heart is (replicated)
        public float Armor;                 // ballistic armor from an Armor Drop's vest (0-ArmorCap): soaks damage, never regenerates, kept between waves (replicated; ArmorDrop.cs)
        public float ChitinHp;              // Hive Censer chitin: a barrier that wears off and takes hits first (replicated; HiveCenser.cs)
        public bool HiveOn;                 // Hive Censer: pouring (replicated)
        public V2 HivePour, HivePourDisp;   // ...where the insects come out (replicated), and where this screen draws it
        public float HivePourT, HiveCallT, HiveCallCd;  // host: the pour's stat clock; Hive Call's rush left and cooldown
        public V2 HiveCallAt;               // host: where Hive Call sends the swarms
        public float SupplyT;               // standing in an open Supply Drop: faster cooldowns, shooting and moving while > 0 (replicated; SupplyDrop.cs)
        public float ArcCd;                 // host: until the Arc Cannon can go again
        public float OfudaCd;               // host: until the Ofuda's Blast Ofuda can go again (Ofuda.cs)
        public V2 RadiantPos, RadiantDisp;      // ...where it is (replicated), and where this screen draws it
        public RunStats Run = new RunStats();
        public int ReviveBy;         // host: who is reviving this downed player (credited when it completes)
        public bool Ready;           // pressed Ready for the next wave (replicated by Msg.WaveReady; WaveReady.cs)
        public BreakStatus Break;    // what they're doing in the break (host-worked out, replicated by Msg.WaveReady; WaveReady.cs)
        public byte BreakUi;         // host: what their screen shows in the break (BreakUiBox, BreakUiBuild, BreakUiShop, BreakUiOut)
        public bool AutoReady = true;  // 'Auto-ready from now on': counts as ready as soon as their picks are done (replicated; the app sets it from the player's saved choice)
        public float LifestealPool;  // host: Royal Jelly heals at most 10 HP per second
        public readonly List<int> Taken = new List<int>();
        public Stats Stats = new Stats();
        // Which ability sits on the 2nd, 3rd and 4th slots (the player picks; host-authoritative, sent with the upgrades).
        public AbilityId[] CardAbilities = { AbilityId.None, AbilityId.None, AbilityId.None };
        public Dictionary<int, float> StatOverrides = new Dictionary<int, float>();  // dev menu: hand-edited stats (index into Game.StatFields)
        public bool DevGod;  // dev menu: takes no damage
        public PerkId[] Perks = Arena.Perks.Empty;  // the perks brought into the run (host; RiotGear.cs)
        public float RiotT;  // Riot Gear: seconds until the shield is back, 0 = ready (RiotGear.cs)
        public readonly AbilitySlot[] Slots = { new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot() };  // Upgrades.SlotCount

        public float Speed => Def.Speed * Stats.SpeedMult * Game.MomentumMult(this) * Defs.KitSpeedMult(Kit) * (AdrenalineT > 0f ? Game.AdrenalineSpeed : 1f) * (OverchargeT > 0f ? Game.OverchargeMove : 1f) * (Game.Supplied(this) ? Game.SupplyMove : 1f) * Game.GustMult(GustT) * (DashT > 0f ? Game.HealDashSpeed : 1f) * (SwiftT > 0f ? Game.HolySwiftMult : 1f) * (CaltropT > 0f ? Game.CaltropSpeed : 1f) * (HarvestHold > 0f ? Game.HarvestSlow : 1f) * (RepelPull ? Game.RepelPullSelfSlow : 1f) * (RapierGuard ? Game.RapierStanceSlow : 1f) * (KusariWhirl ? Game.KusariWhirlSlow : 1f) * (NunGuard ? Game.NunGuardSlow : 1f) * (NodStance == 1 && Weapon.Id == WeaponId.Nodachi ? Game.NodGuardSlow : 1f) * (VgStance == 1 && Weapon.Id == WeaponId.Vowguard ? Game.VgSanctSlow : 1f) * (JuggT > 0f ? Game.JuggSlow : 1f) * Game.ColSpeed(this) * Game.GolSpeed(this) * Game.SpSpeed(this) * Game.BoltSpeed(this) * Game.RodentSpeed(this) * Game.RavagerSpeed(this) * Game.BoilSpeedMult(this) * (TankT > 0f ? Game.TankSlow : 1f) * (Game.RocketOn(this) ? Game.RocketSlow : 1f) * (WcTempestT > 0f ? Game.TempestSlow : 1f) * (FlZeroT > Game.ZeroDur - Game.ZeroCharge ? Game.ZeroSlow : 1f) * (TitheClock > 0f ? Game.TitheSlow : 1f) * (BbBrace ? Game.BraceSlow : 1f) * (HbSet ? Game.PikeSlow : 1f) * (ShardGuard ? Game.ShardGuardSlow : 1f) * (TpLaserT > 0f ? Game.TpLaserSlow : 1f) * (Game.EnergyDrinkOn(this) ? Game.EnergySpeed : 1f) * (PvpSlowT > 0f ? PvpSlowMult : 1f) * Game.FpSpeed(this) * Game.ApSpeed(this) * Game.StaticRushSpeed(this) * (TtCharging ? Game.TtChargeSlow : 1f) * (TtTailT > 0f ? Game.TtTailSpeed : 1f) * Game.CrMoveMult(this);
        public float BarrierRadius => Game.BarrierRadius * Stats.BarrierRadiusMult;
        public float AuraRange => Game.AuraRange * Stats.AuraRangeMult;
    }

    public partial class Enemy
    {
        public int Id;
        public V2 KnockLeft;    // host only: a push still to travel, and how fast (Knock.cs)
        public float KnockSpeed, KnockFxAt = -10f;
        public float BubFedAt = -10f;  // host: when it last fed a Static Bubble (its T; StaticBubble.cs)
        public int KnuckleSlamBy;  // host only: a Drive Punch's owner id + 1 while it slides (0: none); a wall stops it hard: Wall Slam (ArcKnucklesBrawl.cs)
        public int ImpactBy;    // host only: a Lance shove's owner id + 1 while it slides (0: none); fast enough, it hits what it meets (Lance.cs)
        public int ImpactLast = -1;  // host only: the enemy it last hit that way (each pair once)
        public V2 RoomAt;       // host only: its last spot inside a room of the map (KeepEnemiesInRooms, EnemyAI.cs)
        public bool HasRoomAt;
        public float LitT;      // host: seconds held in a Searchlight (below 0: just locked, resting) (Stun.cs)
        public EnemyType Type;
        public EnemyDef Def;
        public V2 Pos, Disp, Waypoint;
        public int DevSpawner;  // host only: the dev spawner that brought it in (Core/DevSpawners.cs), 0 none
        public int Home = -1;   // host only: the walled world's area it belongs to (Rooms.cs): a Gatekeeper room's mini-boss
        public float FarT;         // host only: how long it has been far from every player (Recycling.cs)
        public float PortalT;      // host only: just came out of a portal (it can't take one again until this runs out)
        public float Hp, MaxHp, Radius, Speed;
        public int Score;
        public float T, SpawnT, FireT, Ang, Strafe = 1f, RetargetT, Flash, T2;
        public float FlashHeat;  // every peer, drawing only: rises while it keeps getting hit, so a hit's white flash softens under sustained fire (Juice.cs HitFlashMix)
        public float Facing;    // direction it faces (shields, charges)
        public byte Phase;      // per-type state, e.g. charger wind-up / dash, boss shield
        public float PhaseT;
        public byte SeenPhase;  // every peer, drawing only: the Phase the swing look last saw, and how long ago it changed (Game.cs; Swordsman.cs)
        public float SeenT;
        public bool Elite;
        public EnemyVariant Variant;  // a variant of its type that spawned in its place (EnemyVariants.cs; replicated)
        public byte Traits;           // an elite's traits (EliteTrait flags; Variety.cs; replicated)
        public int SquadId, SquadSlot;  // host: the squad it arrived with, and its place in it (Variety.cs)
        public float BubbleHp, BubbleMax, BubbleT;  // a bubble that soaks damage first: a Shielded elite's (host; drawn from BubbleFrac)
        public float BubbleFrac;      // replicated
        public float[] CellHp;        // host: a bubble's twelve cells, each holding a twelfth of its health; a hit wears down the cells on the side it came from (Variety.cs)
        public int Cells;             // replicated: bits 0-11 are the cells still up, bits 12-23 the ones worn down (drawn from Variety's look)
        public int CellsSeen;         // drawing only: the Cells this peer last saw, to flash the cells that changed
        public float[] CellHitT;      // drawing only: when each cell last took a hit
        public float[] CellDieT;      // drawing only: when each cell last broke
        public float BubbleBornT = -99f;  // drawing only: when this peer first saw the bubble go up (-98: seen without one, so the next bubble is new; -99: never seen without, so no growing in)
        public int LinkId;            // a Bound Wraith's partner (replicated, for the tether; ReaperFaction.cs)
        public int MirrorCount;       // host: player shots that have hit it (Mirrored bounces every fourth)
        public float MarkT, MarkMult = 1f;  // Mark for Death (host); Marked is the replicated flag
        public float ChallengeT;            // replicated: seconds left of a Gauntlet's Challenge (Gauntlet.cs)
        public int ChallengeBy;             // replicated: who threw it
        public float PowerScale = 1f;       // host: a boss's extra health for a strong party (BuildPower.cs)
        public float ShootT, ShootRest, ShootWait;  // host: its turn to shoot, resting after one, and how long it's waited (Readability.cs)
        public bool Winding;                // about to fire a volley: it glows (replicated; Readability.cs)
        public V2 GasAt;                    // host: where a Gas Grenadier's winding-up lob will land, once locked (WardenFaction.cs)
        public bool GasLocked;              // host: GasAt is set for this wind-up
        public float SlowT, SlowMult = 1f;  // the strongest slow and its time left (host; set with Game.Slow)
        public float SlowT2, SlowMult2 = 1f;  // the next-strongest slow, taken over when the strongest ends (Game.Slow)
        public float WadeT, WadeMult = 1f;  // host: wading in a Bloodbath, for Heavy enemies and bosses, which the usual slow skips (Bloodbath.cs)
        public float BurnT, BurnDps;        // the Flamethrower's fire (host)
        public float BurnPool, PoolT;       // Incendiary Rounds: damage still to burn, and the time left to burn it in (host)
        public int BurnBy;
        public float BleedT, BleedDps;      // the shared bleed: claws, Sanguine Orb, the Needle Swarm, Vein Rush, Arterial Spray (host)
        public int BleedHits;               // the claw hits stacked on it, up to five (replicated, for the claw scars: Game/BearLooks.cs)
        public int BleedBy;
        public float CauterT;               // host: seconds before fire can cauterize it again (Cauterize.cs)
        public bool Burning, Chilled, Frozen, Bleeding;  // replicated, for drawing (Chilled = slowed but not frozen)
        public float WetT, WetArcCd;        // Tsunami: wet (slowed, and lightning arcs to it), and the pause before it can be arced again (host)
        public bool Wet;                    // replicated, for the drips (Game/TsunamiLook.cs)
        public float ChargedT, ChargePulseT;  // host: charged by lightning, and until a wet, charged one's next arc (Charged.cs)
        public int ChargedBy;
        public bool Charged;                // replicated, for the bolt icon and the arcs on its body
        public float BrittleT;              // host: Brittle (earth) seconds left: takes more damage; frozen, it shatters (Brittle.cs)
        public int BrittleBy;               // host: who made it brittle (the shatter is theirs)
        public bool Brittle;                // replicated, for the cracked-stone icon and the cracks on its body
        public float DazzleT;               // host: Dazzled (light) seconds left: its shots stray (Dazzled.cs)
        public bool Dazzled;                // replicated, for the sun icon and the glints round its head
        public float SteamCd;               // host: until it can turn to steam again (Steam.cs)
        public float IceSlowT, ChillGateT;  // host: slowed by ice (shown chilled, bosses too), and until steady ice can add a stack again (Chill.cs)
        public bool Slowed;                 // replicated: slowed by anything (Chill.cs ShowsSlowed; Chilled is ice only)
        public float WindCd, WindChillImmuneT;  // host: until wind can react with it again, and until wind can chill it again after a wind freeze (Wind.cs)
        public bool WasIced;                // host: frozen in real ice last tick (a freeze ending leaves it wet: Steam.cs)
        public float WetLeft, WetPeak;      // wet seconds left (replicated, in tenths) and the most seen since it began: the effect bar's fill (Game/EffectBar.cs)
        public byte FrostChill;             // Frost Lance: chill on it, 0-2 (3 freezes; replicated for the pips; FrostLance.cs)
        public float FrostChillT;           // host: until the chill wears off
        public float FrozenT;               // Glacial Tomb: can't move or shoot (host)
        public float IceT, IceMult = 1f;    // Blood Freeze: seconds left of its ice, and the damage multiplier while it lasts (host; BloodFreeze.cs)
        public int IceBy;                   // host: who froze it (its death while frozen heals them, FreezeKill)
        public float GraspT;                // Leech Tendrils: held, can't move or shoot (host)
        public int ReelBy;                  // host: Exsanguinate: the Hemomancer reeling it in (0: nobody; Exsanguinate.cs)
        public float ReelT, ReelHeal, ReelDmg;  // host: seconds into its reel (negative while staggered), and what it drains when it lands
        public V2 ReelFrom;                 // host: where it was hooked
        public bool ReelCap;                // host: the cast was mastered (it lands slowed)
        public int BoneDarts;               // host: Bone Guard darts stuck in it (the third cages it; Minions.cs)
        public float BoneDartT, CageT, CageImmuneT;  // host: until the darts fall out; caged (can't move or shoot); until darts count again
        public bool Caged;                  // replicated, for drawing (and the Skeleton Guards' aim)
        public float WallDartT;             // host: until an Ossuary Wall it's pressed against darts it again (BoneGuard.cs)
        public float SmokeT;                 // host: jammed in a Grenade Launcher smoke cloud (no attacking; GrenadeLauncher.cs)
        public float DazeT, DazeImmuneT;    // host: dazed by a close-in hit (no attacking), and until it can be dazed again (Daze.cs)
        public float BsLiftT;               // host: thrown in the air by the Blood Brawler's Heart Lift (BloodBrawler.cs)
        public float StunT;                 // host: stunned (Daze.cs Stun): held like FrozenT, but shown as stars over its head, not ice
        public bool Dazed;                  // replicated, for the stars over its head
        public int LastHitBy;
        public bool HeartKill;              // host: a Heartbeat wave killed it, so it drops a blood orb (Heartbeat.cs)
        public bool Dormant;                // replicated: asleep in an unseen room until it is seen or hit (DormantGroups.cs): no moving, shooting or counting for the wave
        public bool Penned;                 // replicated: held in by an ink loop (Chalk.cs), for drawing
        public int PenId;                   // host: the ink loop (Chalk.cs InkPen) holding it in, 0 if none
        public V2 PenPos;                   // host: where it was last inside that loop
        public bool Marked;
        public bool Risen;
        public float FrenzyT;   // host: a Shrieker's scream: 30% faster and set on you (Zombies.cs)
        public int GrabId;      // host: a Crawler holding this player (0: nobody)
        public float GrabHp;    //   and its health when it grabbed (25 damage since then frees you)      // host: a Zombie Infection zombie that got back up (no XP; won't rise again; Zombies.cs)
        public int TargetId, ParentId;
        public int CueSig = int.MinValue;  // host: the boss attack last announced with a sound
        public bool Spawning;  // replicated SpawnT > 0 on clients
        public BossFight Boss;      // a boss's fight (BossFight.cs; replicated by each boss's Sync), or null; one of these:
        public WardenState Warden { get { return Boss as WardenState; } set { Boss = value; } }        // Warden.cs
        public QueenState Queen { get { return Boss as QueenState; } set { Boss = value; } }           // the Hive Queen (Queen.cs)
        public SingState Sing { get { return Boss as SingState; } set { Boss = value; } }              // the Singularity (Singularity.cs)
        public HeraldState Herald { get { return Boss as HeraldState; } set { Boss = value; } }        // the Storm Herald (Herald.cs)
        public EclipseState Eclipse { get { return Boss as EclipseState; } set { Boss = value; } }     // Eclipse.cs
        public DevourState Devour { get { return Boss as DevourState; } set { Boss = value; } }        // the Devourer (Devourer.cs)
        public DuelState Duel { get { return Boss as DuelState; } set { Boss = value; } }              // the Duelist and the Court (Duelist.cs)
        public LichState Lich { get { return Boss as LichState; } set { Boss = value; } }              // Lich.cs
        public ColossusState Colossus { get { return Boss as ColossusState; } set { Boss = value; } }  // Colossus.cs
        public float ShedT;           // host: until the Repulsion Cannon can knock another health orb out of it
        public int SweepMark;         // host: the last Halberd sweep that hit it (once a sweep)
        public float KatanaShockT;    // host: marked by a Katana's Momentum Burst for this long (caltrops thrown then seek it; Katana.cs)
        public bool KatanaShocked;    // marked (replicated)
        public float ShieldHp, ShieldMax;  // host: a Shielder's shield, worn down by blocked hits and time (its Phase shows the cracks)
        public byte FpBrands;                // Forbidden Path's Blood Brands on it, up to Game.FpBrandMax (replicated; ForbiddenPath.cs)
        public float FpBrandT;               // host: the brands fade when this runs out
        public byte BlastStacks;             // Blood Blast stacks on it, up to Game.BlastStackMax: its blood heals BlastOwner when it dies (replicated, with the owner; BloodBlast.cs)
        public int BlastOwner, BlastPress;   // the player whose stacks they are (replicated); host: the last press that stacked it
        public int BleedStacks;              // bleed stacks on it from any source, up to five (KitWeapons.cs); a Needle Swarm cut that fills them bursts it (Hemomancer.cs NeedleBleed); replicated, drawn as drops
        public float KusariCd;               // host: the Kusarigama's whirl can hit it again after this long
        public float Suppress;               // host: the Riot Repeater's suppression, 0 to SuppressMax (slows its shooting)
        public bool Suppressed;              // replicated, for drawing
        public float PoisonT, PoisonDps;     // host: Senbon Seekers' poison (stacking)
        public int NeedlesIn;                // Bloodletting needles stuck in it (replicated, drawn; AcupunctureNeedles.cs)
        public float NeedleT;                // host: until they fall out
        public int NeedleBy;                 // host: whose they are
        public int PoisonBy;
        public bool Poisoned;                // replicated, for drawing
        public float BeeLastT; public int BeeChain;  // host: the Honeybee Staff's Swarm: the last bee sting's time (hostClock) and the stings in a row (HoneybeeStaff.cs)
        public float VenomT, VenomDps, VenomReach, VenomFalloff;  // host: Poison Dart's venom, and how it spreads when this dies (PoisonDart.cs)
        public int VenomBy, VenomJumps;
        public bool Venomed;                 // replicated, for drawing
        public int StarsStuck;               // Shuriken stars stuck in it (replicated, for drawing)
        public int SeededBy;                 // host: a Sprite Staff seed is stuck in it (killing it frees a sprite)
        public float SeededT;
    }

    public class Bullet
    {
        public int Id;
        public byte Kind;
        public V2 Pos, Vel;
        public V2 Prev;           // host only: where it was before this tick's move (Combat.Enters), once HasPrev
        public bool HasPrev;
        public float AngVel, Life, Radius, Dmg;
        public float Life0;       // its lifetime when fired (for drawing it aging: the Flamethrower's flames)
        public int Owner, Pierce, LastHit;
        public int PrevHit;       // host: the enemy hit before LastHit, so a piercing shot inside two overlapping enemies doesn't hit them by turns
        public int Shooter;       // host only: the enemy that fired it, when known (0: not known); the Juggernaut Suit's flak goes back at it
        public bool FromWeapon;   // host only: fired by the player's gun (for accuracy)
        public bool WeaponDmg;    // host only: its hits are the weapon's in the session recording (FromWeapon, or launched later by it: Ice Shards, hammer throws)
        public float Splash;      // host only: explosion radius on impact
        public V2 FallOrigin;     // host only: a Double Barrel pellet: where it left the gun, for its damage falling off with distance (DoubleBarrel.cs)
        public bool PointBlank;   // host only: a Scattergun pellet (double damage and a shove up close)
        public ShotLook Look;     // its shape: a dart (aimed), a crescent (curving) or round (sent with its spawn; Readability.cs)
        public Bullet Sent;       // host only: its entry in the spawn message still to go out (so its Look can be set after)
        public float Sheds;       // host only: a Twin Pistols round's chance to knock a tiny health orb out of what it hits (TwinPistols.cs)
        public bool Grazed;       // host only: it already skimmed past a player (each shot grazes once; Graze.cs)
        public bool Suppressive;  // host only: a Riot Repeater round, which slows how fast what it hits shoots
        public bool Medicine;     // host only: an Acupuncture Needle: flies through enemies, sticks in a hurt teammate (AcupunctureNeedles.cs)
        public int ReturnTo;      // host only: a needle flying back from a dead enemy to this player, healing HealAlly on arrival
        public bool ShadowKunai;  // host only: a shadow kunai, which leaves a mark where it lands to blink to
        public bool Sticks;       // host only: a Shuriken star, which sticks in what it hits (to be recalled)
        public bool Talisman;     // host only: an Ofuda Healing Talisman (Ofuda.cs)
        public bool Pins;         // host only: a mastered Ballista bolt pins what it passes (Ballista.cs)
        public bool Bolts;        // host only: a piercing shot whose hits fling Ricochet's bounce bolts instead of it bouncing (Combat.cs)
        public bool FanRound;     // host only: a Revolver's Fan the Hammer round (harder on stunned enemies: PistolWhip.cs)
        public bool Crossfire;    // host only: a Twin Rifle round, double near Cross (where the streams meet)
        public V2 Cross;
        public float HealAlly;    // host only: heals an ally it passes through
        public float WardAlly;    // host only: wards an ally it passes through instead (Mercy Cut's crescent; Lightbringer.cs)
        public float HealBurst;   // host only: heals allies near where it bursts (the Ninja's Ofuda)
        public float Pool, PoolTime;  // host only: leaves a Blood Pool this big, for this long, where it lands (the Gore Mortar)
        public int Burst;         // host only: mortar bombs burst into this many bullets when they land
        public int Hops;          // host only: a Sprite Staff firefly hops to this many more unseeded enemies when it lands (KitWeapons.cs)
        public int Chain;         // host only: lightning jumps to this many more enemies
        public int Shock;         // host only: electrifies the enemy it hits, which arcs to this many others round it (the Storm Hammer)
        public float SlowAmt;     // host only: frost slows enemies hit
        public float Ignite;      // host only: sets enemies hit burning for this much a second (Flamethrower)
        public float IgniteBonus; // host only: and this much longer than FlameBurnTime (Wildfire)
        public bool Pierced;      // host only: a Flamethrower flame that has hit its first enemy (it does less after)
        public bool FromBoss;     // fired by a boss (only these wear blockers down; drawn with a dark core)
        public float Homing;      // Homing Rounds: turn rate toward the nearest enemy (host corrects clients)
        public int Bounces;       // host only: Ricochet bounces left
        public byte WallHops;     // host only: times it has bounced off a wall (the Wall Bounce relic, Relics.cs)
        public int Frags;         // host only: on hit, throws this many fragments (Frost Lance, Railgun, Flak, Fireball)
        public byte FragKind;
        public float FragDmg, FragSpeed, FragLife, FragSplash;
        public bool FragSideways; // fragments fly out to the sides (shatter, shrapnel) rather than all around (bomblets)
        public bool GlNade;       // host only: a Grenade Launcher HE grenade (it bursts with GlBlast: GrenadeLauncher.cs)
        public bool Incendiary;   // host only: an Incendiary gadget's bottle: it lands as burning ground (IncendiaryBurst: Gadgets.cs; Dmg is its damage multiplier)
        public bool Bomblet;      // host only: a Flak Cannon bomblet; it pops where it lands (Burst marks it as landing)
        public bool Pulled;       // host only: bent by a gravity well (so the host sends its course)
        public bool StunBack;     // host only: a Warden's stun shot sent back by a perfect parry (stuns the Warden)
        public bool Tossed;       // host only: a Katana caltrop still in the air: it flies over enemies and lies where it lands (Katana.cs)
        public EnemyType? Source; // host only: the enemy that fired it (for the session record)
        public int ShooterId;     // host only: the id of the enemy that fired it (0: not known); Lightning Rod calls its bolt on it
        public V2 ShooterPos;     // host only: where that enemy stood when it fired (the bolt still lands there if it has died)
        public HashSet<int> Gusted; // host only: a Focus arrow: the enemies its wind has shoved (BowFocus.cs)
        public bool Senbon, SbHome;  // host only: a Senbon Seekers needle (SteerSenbon, poisons on a hit), flying back into the ring
        public float SbAge;       // host only: how long the needle has steered (it turns harder)
        public int HomeOn;        // host only: a Stonespear volley spear: the enemy it curves in on (SteerSpear; Pulled sends its course)
    }

    /// A health pickup dropped by a dead enemy.
    public class Orb
    {
        public int Id;
        public int Owner;       // the player this copy is for (an enemy's or a crate's orb, Combat.cs SpawnOrbEach); 0: anyone's
        public bool Big, Tiny;  // Tiny: knocked loose by the Repulsion Cannon
        public bool Blood;      // a blood orb: a Hemomancer's kill dropped it, for that Hemomancer only (Owner; Combat.cs DropBloodOrb)
        public bool Med;        // a Deployable Medkit's orb (Medkit.cs), or a Field Hospital's
        public int Supply;      // host: the Supply Drop or Armor Drop zone that threw it (SupplyDrop.cs, ArmorDrop.cs; 0: none)
        public bool Vest;       // an Armor Drop's ballistic vest (ArmorDrop.cs): heals nothing, lies still, gives armor
        public int Hosp;        // host: the Field Hospital zone it lies in (FieldHospital.cs; 0: none): it only drifts to players inside
        public float MedHeal;   // host: what it heals
        public V2 Pos, Disp;
        public float Life;

        public float Radius => Vest ? Game.ArmorVestR : Big ? 11f : Med ? 8f : Blood ? 7f : Tiny ? 4f : 6f;
        public float Boost = 1f; // host: heals this much more (Undertow Pump's tiny orbs)
        public float Heal => Vest ? 0f : (Med ? MedHeal : Blood ? Game.BloodOrbHeal : Big ? Game.BigOrbHeal : Tiny ? Game.TinyOrbHeal : Game.OrbHeal) * Boost;
    }

    /// An area on the ground: Flame Wall, Blizzard, smoke, pools, wards, a meteor's warning and the like (ZoneKind).
    public class Zone
    {
        public int Id, Owner;
        public bool Landed;         // host: a Proximity Shell or Tear Gas canister has come down (its landing sound sent; TankAlts.cs)
        public ZoneKind Kind;
        public V2 Pos;
        public float Radius, T, Duration;
        public float DmgMult = 1f;  // host: owner's damage multiplier when cast
        public float HealMult = 1f; // host: the casting ability's mastery
        public float ExtraSlow;     // host: mastered Flame Wall also slows
        public float ExtraRevive;   // host: mastered Healing Rain revives downed allies inside this much faster (1 = twice as fast)
        public bool Follow;         // host: mastered Soul Lantern moves with its owner
        public float DamageCut;     // host: allies inside take less damage (the Ninja's smoke)
        public bool Embers;         // host: a mastered Meteor leaves burning ground
        public float Aim;           // replicated: a Lumen Ward's cone points this way (follows its owner's aim) (radians; Pos is its apex, Radius its length); a Vein Trail: 1 when it will burst
        public bool Freezes;        // host: a mastered Lumen Ward freezes enemies outside its light (not bosses)
        public bool Consecrates;    // host: Hallowed Blade blesses the ground where it lands
        public float BlockHp;       // host: damage it can still soak (blocking zones)
        public float GuardFrac;     // replicated BlockHp fraction, for drawing (a Spirit Ward's soak)
        public bool Congealed;      // host: a Blood Pool that has set (Hemomancer.cs)
        public bool Bursts;         // host: a mastered Vein Rush trail that hasn't burst yet (VeinRush.cs)
        public float Soak, SoakMax, Soaked;  // host: a Spirit Ward's damage left to take for allies, its most, and what it has taken (Ninja.cs)
        public float TickT, HealBack;        // host: a Bloodbath's time toward its next bleed tick, and the share of it that heals its owner (Bloodbath.cs)
        public byte[] Bones;        // replicated, a Bone Phalanx: each skeleton's health as a fraction of 255 (0 = broken); its length is how many (BonePhalanx.cs)
        public float Marched;       // replicated, a Bone Phalanx or a Stampede: px the line has walked from the gate
        public float[] BoneHp, SpearT;  // host, a Bone Phalanx: each skeleton's health and time to its next spear
        public float BoneMax;       // host, a Bone Phalanx: a skeleton's full health
        public V2 Origin;           // host, a Stampede: where its gate opened; a Blast Ofuda: where on its enemy it rides (Ofuda.cs)
        public float Heading;       // host, a Stampede: the way it charges out (Aim flips to face back on the return)
        public int Leg = -1;        // host, a Stampede: which charge it is on (0 out, 1 back)
        public float Blast;         // host, a Decoy Dummy: how far its confetti blast reaches when it pops (DecoyDummy.cs)
        public HashSet<int> Trampled, Slammed;  // host, a Stampede: enemy ids already hurt, and already slammed, on this charge
        public Dictionary<int, float> HitAt;  // host, a Mammoth Fall: when (its age) it last trampled each enemy (MammothFall.cs)
        public float Spool, GunCd; public int Side; public bool Quiet, Cap;  // host, a Deployable Turret: Quiet folded away with no blast, Cap mastered; how far the barrels have spun up (0-1), time to its next round, which barrel fires next (DeployableTurret.cs)
        public V2 Origin2;          // replicated, a Tidal Tusker: where its second (mastered) stream ends; Origin the first's (TidalTusker.cs)
        public V2 OwnerAt;          // host, a Tidal Tusker: where its owner was last tick (the way they walk)
        public int Rider, BoltsLeft;  // replicated, a Ballista: who rides it (0 = nobody), bolts left (Ballista.cs)
        public float BaReloadT;      // host, a Ballista: seconds between bolts (Windlass shortens it)
        public ZoneDef Def => Kits.Get(Kind);
    }

    /// A friendly unit: turrets, summoner companions and summoned creatures.
    public class Minion
    {
        public int Id, Owner;
        public MinionKind Kind;
        public V2 Pos, Disp;
        public float Aim;
        public float T = -1f, Duration;  // seconds left; -1 = a companion that stays
        public float Cd, Ang, HealRate;
        public float BlockHp, StaggerT;  // host: damage it can still soak; a staggered companion doesn't block
        public float GuardFrac = 1f;     // replicated: 0 while staggered or broken
        public bool Companion;
        public float Scale = 1f;         // replicated: Treant Rod's treant grows with extra projectiles
        public float Power = 1f;         // host: mastery of the ability that summoned it
        public float Hp, MaxHp, HurtT;   // host: a Warden's Guard's or thrall's health, and its contact-damage cooldown
        public bool Mastered;            // host: summoned by a mastered ability (Phoenix)
        public byte Pact = 255;          // replicated: the EnemyType a Thrall was; PactSlot (host) is its Force Pact slot
        public int PactSlot;
        public float Hold;               // host: Legion's pack circles its owner this many seconds before it is let loose
        public int Stacks;               // replicated: Alpha Call's Blood Scent stacks, 0 when not in it (2..5)
        public bool NoFrenzy;            // host: Legion's pack, which doesn't frenzy (the frenzy is for your other minions)
        public MinionDef Def => Kits.Get(Kind);
        public bool Sent;                // host: a Wisp Lantern wisp or Imp Caller imp sent out at something
        public bool Returning;           // host: a wisp flying back to you
        public int GoalEnemy;            // host: what it was sent at (0: just the point Goal)
        public V2 Goal;
        public float SentT;              // host: how long it has been out
        public int Stings;               // host: a Hive Drone's stings left on this sortie (QueenLoot.cs)
        public int LinkTo;               // replicated: the player a Soul Lantern is linked to (SoulLantern.cs)
        public float Launch;             // host: a Bat Swarm bat circles its owner this long before it dives (BatSwarm in Minions.cs)
        public int BiteN;                // host: a Bat Swarm bat's bites so far (every other one shows a slash)
        public int Head;                 // host: a Hydra's next head to spit (Hydra.cs)
        public V2 Vel;                   // host: a World Tree butterfly's velocity (WorldTree.cs)
        public float Wind;               // replicated: a Titan's slam, 0 at rest and 0..1 over its wind-up and follow-through (Titan.cs)
        public float SlamT = -1f;        // host: a Titan's time into its slam, -1 when not slamming
        public bool Stood, SlamHit;      // host: a Titan has finished rising; its slam has landed
        public System.Collections.Generic.HashSet<int> Burrowed;  // host: the enemies a Burrower has already bitten (Burrower.cs)
        public bool Capstone;            // host: a Burrower from a mastered Earthquake (freezes what it bites)
        public float SeedCd;             // host: a Treant Rod Treant's time to its next seed (KitWeapons.cs)
        public int Alpha;                // host: a Wolf Whistle alpha's wolves merged into it, 0 a normal wolf (WolfPack.cs)
        public float Ring;               // host: a Twin Scythe's circle round its owner, signed by which way it spins (ReaperLoot.cs)
    }

    public class Effect
    {
        public FxKind Kind;
        public V2 Pos, Pos2;  // Pos2: the far end of a lightning bolt
        public Rgba Color;
        public float Radius, T, Dur;
        public bool Juiced;  // the screen's visual effects have seen it (Game/Juice.cs); never sent
        public bool Heard;   // its sound has played on this machine (Game/ArcLockSoundBank.cs); never sent
    }

    public struct PlayerInput
    {
        public V2 Move;       // desired direction, length <= 1
        public V2 AimAt;      // point in arena coordinates
        public bool Fire;
        public bool Ability;  // held states; Game detects the presses
        public bool AbilityQ;
        public bool AbilityE;
        public bool AbilityShift;
        public bool Ultimate;
        public bool Parry;     // held: raise the parry shield toward the cursor
        public bool Dodge;         // pressed: dodge (Ctrl or a double tap of a movement key; Dodge.cs); this machine only
        public bool DodgeHeld;     // held: the key that started the dodge is still down (keep holding to sprint; Dodge.cs); this machine only
        public byte Steer;         // the Arc Knuckles' click wheel: the way the cursor points from its centre (0 none, else an angle code; ArcKnucklesBrawl.cs); sent in Msg.Input
        public bool WeaponAction;  // held: your weapon's secondary (Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama whirl); this machine only
    }
}

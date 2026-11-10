using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    // The match's data types: states, effects and sounds, and the Player / Enemy / Bullet / Orb / Zone / Minion /
    // Effect classes the Game holds.
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver, Victory }

    public enum FxKind : byte { Boom, Spark, Ring, Bolt, Target, Slash, Jab, Shatter, Claw, Implode, Repulse, Icicle, Splinter, LightCone, BloodCoat, ArcField, ChainThrow, IonTrail, SteelWall, Debris, Callout, Bleed, BloodShell, Drain, DebtCoin, WallSplash, Link, Rewind, Knock, RootLash, Strike, Tsunami, Meteor, Lifeline, RiotBlock, DeathLotus, ShadowStep, LeapSlam, ShellHit, ShellPop, HolyCross, KatanaStrike, StandHit, CavalryCall, KatanaCut, HarvestThrow, StaffShove , TitanSlam, RainRelease, TitheSnap, PactBind, PactSnap, PactDissolve, PactRise, BurrowSnap, KatanaHitCue, GreatRend, MartyrCast, BloodFreeze, TwinStep, TwinFang, TwinHit, HammerThrow, HammerSlam, LightStep, Sunlance, DebtSettle, VeinRush, VeinCut, CenserStep, CenserCast, Exsanguinate, LightMark, LanceStep, LanceHook, LanceHit, LanceSpark, LanceImpact, Airstrike, SteelCatch, SteelRelease, SteelHit, SteelDrop, GauntletThrow, GauntletCharge, GauntletBowl, GauntletImpact, GauntletBack, MercyLeap, MercyKill, MercyEnd, BridgeSpark, JuggShard, BathHeal , RodBolt, RodArc, RodGround, PhalanxJab, PhalanxSpark, PhalanxBreak, VenomDart, VenomSpread, PerfectParry, KatanaStanceFx, KatanaHop, KatanaDash, KatanaSweep, KatanaSlash, KatanaBurst, TaserHit, BossBeam }  // (the DPS Knight's Caught Steel, Gauntlet, Misericorde and Drawbridge kinds are described in Core/CaughtSteel.cs, Gauntlet.cs, Misericorde.cs, Drawbridge.cs); Target: a marker where something is about to land; Slash: a melee swing (Pos2 = tip, Radius = half the arc); Jab: the Lance's box (Pos2 = far end, Radius = half its width); Shatter: a parry shield breaking (Pos = its owner, Pos2 = the shield's middle, Radius = half its arc); Claw: a bear-claw swipe (Pos2 = reach, Radius = half the arc, signed by the way it rakes); Implode: blood collapsing into an enemy (Implosion); Icicle: the Frost Lance's icicle (Pos2 = its point, Radius = half its width); Splinter: shards off its sides (Pos2 = its point, Radius = how far out they fly); LightCone: Chain Light (Pos2 = the middle of its far edge, Radius = half its angle); BloodCoat: a Sanguine Orb circling in on an enemy (Pos2.X = the enemy's id, Radius = its radius); ArcField: Arc Lightning going off (Pos2 = the far end of its strip, Radius = half its width); ChainThrow: the Kusarigama's chain flying out (Pos2 = where it ends, Radius = 1 when it hooked something); IonTrail: a Railgun shot's charged path (Pos2 = its far end, Radius = the round's speed); SteelWall: a Greatsword's lingering crescent (Pos = the swing's centre, Pos2 = the middle of its edge, Radius = half its arc); Debris: a crate or pot smashed (Props.cs); Callout: an ability's name over whoever cast it (Pos2 = (player id, AbilityId), drawn on the HUD); Bleed: a Hemomancer paying health for an ability (Pos2.X = their id, Radius = health paid); BloodShell: Blood Shell forming (Pos2.X = the caster's id); Drain: blood flowing from Pos to Pos2 (Exsanguinate, Blood Gift, Martyr; Radius = how thick); DebtCoin: a Blood Debt repayment, blood coins flying from a kill into the debtor's health bar (Pos = the kill, Pos2.X = the debtor's id, Radius = health repaid; Game/BloodDebtLook.cs); WallSplash: an enemy shot stopping on a Blood Wall (Pos = where, Pos2 = back along the shot, Radius = the shot's size; BloodWall.cs); Link: a beam of light from a caster (Pos) to whoever their ability reached (Pos2); Rewind: Time Rewind turning a player's clock back (Pos2.X = their id); RootLash: the Treant Rod's root whipping out (Pos2 = its tip, Radius = half its width); Strike: the Nimbus's lightning landing (Radius = its blast); Tsunami: a wave rolling from Pos to Pos2 (Radius = half its width; Tsunami.cs); Meteor: one landing (Radius = its blast; drawn by Game/ElementalistLooks.cs); Lifeline: one per player the ultimate reached (Pos = where the caster stood, Pos2 = (that player's id, the caster's id), Radius = how far they were; Game/LifelineLook.cs); RiotBlock: Riot Gear taking a hit (Pos = its owner, Pos2 = where the hit came from, Radius = the plate's reach; Game/RiotGearLook.cs); DeathLotus: the Ninja's Death Lotus (Pos2.X = -1 for the cast itself, Pos = the Ninja, Pos2.Y = their id, Radius = targets struck; otherwise one per enemy struck, Pos = the enemy, Pos2.X = seconds until its cuts land, Radius = its size; Game/DeathLotusLook.cs); ShadowStep: the smoke look of a Ninja's step (Pos = where they stood, Pos2 = where they landed, Radius = their id * 1000 + the radius of the enemy they struck, or just their id * 1000 when it was a short step with no enemy; Game/ShadowStepLook.cs); LeapSlam: the Knight's Leap Slam, sent once at the cast and lasting its leap, landing and stun (Pos = where they leapt from, Pos2 = where they land, Radius = their id * 1000 + the slam's radius, Color.A = 1 when mastered and 0.5 otherwise, Dur = LeapTime + LeapFxTail + the stun; Game/LeapSlamLook.cs); TitanSlam: the Titan (Titan.cs; Radius = its reach, Pos2.X = 0 for a slam, 1 for standing up, 2 for the rubble when it falls, Dur = how long the cracks or rubble stay; Game/TitanLook.cs); HolyCross: a Holy Cross flying from Pos to Pos2 (Radius = its flight, Dur = flight + fade; Game/HolyCrossLook.cs); KatanaStrike: the Katana's Alt strike from the stance to its fading line, Pos to Pos2 (Radius = the striker's id, Color.A = 1 when empowered, 0.5 otherwise, Dur = the whole show; Game/KatanaLook.cs); KatanaCut: one enemy the strike cut (Pos = the enemy, Pos2 = the line's direction, Radius = its size, Color.A = 1 when it died, 0.5 when it lived); StandHit: a shot stopped by a planted Last Stand (Pos = where, Pos2 = the knight, Radius = its damage; Game/LastStandLook.cs); CavalryCall: Cavalry Call (Pos = where the knight stood, Pos2 = the lane's far end, Radius = lancers (2 or 4), colour alpha 1 = mastered; Game/CavalryCallLook.cs); HarvestThrow: a Crimson Harvest ball thrown from Pos (the Hemomancer's feet) to Pos2, flight then blast then the stain, drawn from its age (Radius = the blood in it, Dur = flight + tail; Game/CrimsonHarvestLook.cs); StaffShove: the Bo Staff's shove (Pos = the Ninja, Pos2 = the far end of the bar's push, Radius = half the bar's width; Game/BoStaffLook.cs); RainRelease: a Sanguine Rain ring splashing away when the rain ends (Pos = where the ring was, Radius = its size; Game/SanguineRainLook.cs); TitheSnap: a Vein Tithe tendril letting go of an enemy (Pos = the Hemomancer, Pos2 = the enemy, Radius = its size, Color.A = 1 when it was a boss; Game/VeinTitheLook.cs); PactBind: Force Pact's chain cuffing an enemy (Pos = the caster, Pos2 = the enemy, Radius = the enemy's EnemyType, Color = its colour, Dur = ForcePactBind; Game/ForcePactLook.cs); PactSnap: a thrall's chain snapping as it falls (Pos = the thrall, Pos2 = its owner, Radius = its EnemyType); PactDissolve: an old thrall dissolving into the new one (Pos = the old one, Pos2 = the new one's spot, Radius = the old EnemyType); PactRise: a fallen thrall thrown back out on its chain (Pos = its owner, Pos2 = where it lands, Radius = its EnemyType); BurrowSnap: the Burrower snapping up under an enemy it hurt (Pos = the enemy, Pos2 = the Burrower, Radius = the enemy's size, Dur = 0.7; Game/BurrowerLook.cs); KatanaHitCue: a Katana swipe or lunge that cut something, drawn as nothing: it carries the cut's sound, and your own lunge's hit-pause (Pos = the swordsman, Pos2.X = their id, Radius = enemies cut, Color.A = 1 for the lunge, 0.5 for a swipe; Game/Juice.cs); MartyrCast: Martyr (Pos = the Hemomancer, Pos2 = the ally, Radius = the ally's id * 10 + 1 for a revive, + 0 for a ward put on, + 2 for a ward spent (Pos2 = Pos), Dur = 1.4, or the invulnerability when longer (the shimmer); Game/MartyrLook.cs); BloodFreeze: the Hemomancer's Blood Freeze, sent once at the cast and lasting its charge, wave, freeze and thaw (Pos = where it was cast, Pos2 = (caster's id, seconds a regular enemy stays frozen), Radius = how far the wave goes, Color.A = 1 when mastered and 0.5 otherwise; Game/BloodFreezeLook.cs); TwinStep: one Twin Blades chain step, drawn from its age (Pos = the swordsman, Pos2 = along the aim at reach, Radius = their id * 10 + the step, Dur = the step + its recovery; Game/TwinBladesLook.cs); TwinFang: Twin Fang thrown (Pos = where from, Pos2 = where the sabres cross, Radius = the thrower's id, Dur = TwinBladesMotion.FangTotal); TwinHit: a Twin Blades step or thrown sabre that cut something, drawn as nothing: it carries the cut's sound, and the swordsman's own hit-pause and shake (Pos = the swordsman, Pos2 = (their id, the step 1-5, 6 a sabre passing, 7 Twin Fang's burst), Radius = enemies cut; Game/Juice.cs); HammerThrow: the Storm Hammer in its owner's hand (Radius = their id, Pos2.X = 0 for a throw, 1 for a slam's wind-up, Dur = how long the hand is busy; the held model reads its pose from it, Game/StormHammerLook.cs); HammerSlam: the Storm Hammer's shockwave (Pos = the crater, Pos2 = the player, Radius = how far the wave runs, Dur = how long the cracks last; Game/StormHammerLook.cs); LightStep: one Lightbringer chain step, drawn from its age (Pos = the knight, Pos2 = along the aim at the cut's reach or the Sunburst's radius, Radius = their id * 10 + the step 1-3, 4 a Radiant Noon, Dur = the step + its recovery; Game/LightbringerLook.cs); Sunlance: the thrust and the lance of light (Pos = the knight, Pos2 = where the ally stood, Radius = the knight's id * 1000 + the ally's id, Dur = the thrust + its recovery); DebtSettle: a Blood Debt's moments (Pos = the debtor, Pos2 = (their id, 2 borrowed at the cast / 1 paid in full / 0 forfeited), Radius = the health borrowed, paid or lost; Game/BloodDebtLook.cs); VeinRush: a Vein Rush streak from Pos to Pos2 (Radius = the rusher's id, Dur = the rush plus its fade; Game/VeinRushLook.cs); VeinCut: an enemy the rush cut through (Pos = the enemy, Pos2 = the rush's direction, Radius = the rusher's id * 1000 + the enemy's size; its blood flies back to the rusher); CenserStep: one Censer rite step, drawn from its age (Pos = the knight, Pos2 = along the aim at reach, Radius = their id * 10 + the step 1-3, Dur = the step + its recovery; Game/CenserLook.cs); CenserCast: Censer Cast thrown (Pos = where from, Pos2 = where the head lands, Radius = the thrower's id * 10 + Embers spent, Dur = CenserMotion.CastTotal); Exsanguinate: the Barbed Lash (Pos2.X = -1 the cast, -2 the impact, otherwise the hooked enemy's id; Pos2.Y = the caster's id; Core/Exsanguinate.cs, Game/ExsanguinateLook.cs); LightMark: a Lightbringer's mark on a player (Radius = their id; Pos2.X = 1 a heal burst, 2 a ward snapping shut, 3 a shot striking the ward; Pos2.Y = seconds before a burst or snap shows (the Sunburst's ring reaching them), or the angle the shot came from; Color.A = 1 for a Radiant Noon's bigger burst; Game/LightbringerLook.cs); LanceStep: one Lance chain step, drawn from its age (Pos = the knight, Pos2 = along the aim at the step's reach, Radius = their id * 10 + the step 1-4, Color.A = the Breaching Thrust's half width / 100, Dur = the step + its recovery; Game/LanceLook.cs); LanceHook: Hook & Haul (Pos = the knight, Pos2 = where the hooked enemy stood (or the aim point on a miss), Radius = their id * 10 + 1 when it hooked something, Dur = the move + its recovery); LanceHit: a Lance step that hit something, drawn as nothing: it carries the hit's sound, and the knight's own hit-pause and shake (Pos = the knight, Pos2 = (their id, the step 1-4, 5 the hook), Radius = enemies hit; Game/Juice.cs); LanceSpark: one enemy a Lance step hit (Pos = the enemy, Pos2 = Pos + the push's direction, Radius = 1 for the Breaching Thrust, 0.5 otherwise); LanceImpact: a shoved enemy hitting another or a wall (Pos = where, Pos2 = (the enemy's id, the other's id or -1 for a wall), Radius = the damage each took; Core/Lance.cs); Airstrike: one wall of the Soldier DPS's Airstrike (Pos = where the jet comes in, Pos2 = the lane's far end, Radius = thrower id * 10000 + the flare's distance along the lane, colour alpha 1 = with the flare, 0.5 = the mastered second wall; Core/Airstrike.cs; JuggShard: a Juggernaut Suit flak shard flying back at a shooter (Pos = where the shot struck the armour, Pos2 = the shooter, Radius = the wearer's id; Core/Juggernaut.cs, Game/JuggernautLook.cs)); RodBolt: Lightning Rod's bolt from the sky (Pos = where it lands, the shooter or where it stood; Pos2 = (the rod's player id, the bolt's damage); Radius = the struck enemy's size, 0 when it was already dead); RodArc: one arc on from a bolt (Pos = from, Pos2 = to, Radius = its damage); RodGround: an enemy hit grounded through a Lightning Rod (Pos = the player, Pos2 = (their id, the damage they took), Radius = 1 for a shot, 2 for a melee or contact hit; Core/LightningRod.cs, Game/LightningRodLook.cs); TaserHit: a Fork Bolt landing on a player (Pos = the player, Pos2 = a point one px on along the bolt's flight, Dur = 0.7; Game/TaserLook.cs); BossBeam: one player beamed into the hub before a boss wave (Pos = where they stood, Pos2 = where they land, Radius = their id, Dur = BeamTotal; Core/BossBeam.cs, Game/BossBeamLook.cs)

    /// Sound cues raised by the game; the Unity layer plays them.
    public enum Sfx : byte
    {
        None, EnemyShot, Hit, Hurt, Explode, BigExplode, Ability, Shockwave, Blink,
        LevelUp, Pick, WaveStart, WaveClear, GameOver, Downed, Revive,
        // One gunshot per weapon, plus the turret and Nova.
        ShotShotgun, ShotCannon, ShotRifle, ShotSuppressed, ShotPistol, ShotBow, ShotAssault, ShotSniper, ShotSmg,
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
        // The pause menu's sounds (WindowAnim.cs; the tear and the typing were its old glitch opening's): your own screen only, never sent.
        PauseTear, PauseType, PauseSettle, PauseClose,
        // The Repulsion Cannon's blast, and Chain Light's cone and its first arc (SoundBank.cs recipes; named Shot/Spell so they use the Weapons volume).
        ShotRepulse, SpellChainCone, SpellChainArc,
        // The Radiant Orb (RadiantOrb.cs): cast, a twinkle each tick that touches something, and fading out. Sent, so quieter for allies.
        RadiantCast, RadiantTouch, RadiantFade,
        // Zombie Infection (Zombies.cs; SoundBank.cs recipes): a groan now and then, getting back up, spit, a grab, a scream, a burst, a mend.
        ZombieGroan, ZombieRise, ZombieSpit, ZombieGrab, ZombieShriek, ZombieBurst, ZombieMend,
        // The profile picker and the menu trading places (ProfileTransition.cs): your own screen only, never sent.
        ProfSelect, ProfPing, ProfZip, ProfSweep, ProfSweepBack, ProfScratch, ProfBar, ProfChord,
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
        // Quieter the further they are from you. SawWhirr is a loop while a Buzzsaw touches someone (played by SoundBank.SetSawLoop, not as a cue).
        SwarmerChomp, SwarmerDie, SpikerFlip, SpikerStab, SpikerDie, BruteStep, BruteGrunt, BruteBash, BruteDie,
        LungerTell, LungerLeap, LungerLand, LungerDie, SawWhirr, SawDie,
        SpikeBlockRock, SpikeBlockShake, SpikeBlockLunge, SpikeBlockRest, SpikeBlockDie,
        SwarmerPatter, SpikerSkitter, LungerPad,  // their footsteps
        // The Ninja DPS (NinjaSoundBank.cs, the Shinobi set of Docs/Mockups/ninja-dps-sounds.html): the Kunai's whoosh (named Shot so it uses the Weapons
        // volume), its blink, Shadow Step's smoke and its strike, Vanish, Blade Fan opening and flying out, and Death Lotus darkening the arena. Sent, so quieter for allies.
        ShotKunai, NinjaBlink, NinjaStepCast, NinjaStepStrike, NinjaVanish, NinjaFanOpen, NinjaFanBurst, NinjaLotusCast,
        // The Katana (KatanaSoundBank.cs, the Steel set of Docs/Mockups/katana-sounds.html; Katana.cs). Sent by the host: Senbon's swipes, the old wind-up
        // (unused), Hayate's dash (KatanaLunge), a cut landing (the first five use the Weapons volume); KatanaStance as a Kage Chain or Zanshin begins, the old
        // strike cut (unused) and Zanshin's cut, the sheathe, the blow, a chime per hop; the old empowered/lost cues are unused. Your own screen only: a meter full
        // (KatanaReady, KatanaEmpReady for Zanshin's); KatanaEmpty, KatanaAim, KatanaSwitch are unused.
        KatanaSwipe1, KatanaSwipe2, KatanaWindup, KatanaLunge, KatanaHit, KatanaStance, KatanaStrikeCut, KatanaEmpStrikeCut, KatanaSheathe, KatanaBlow,
        KatanaChain1, KatanaChain2, KatanaChain3, KatanaChain4, KatanaChain5, KatanaEmpReady, KatanaLost, KatanaReady, KatanaEmpty, KatanaAim, KatanaSwitch,
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
        public WeaponDef Weapon;
        public AbilityId RoleAbility;
        public AbilityId StartBonus = AbilityId.None;  // a Wildcard's second starting ability: it sits on a bonus key (Q, E or Shift) from the start (Game.Recalc)
        public Kit WildKit;                            // a Wildcard's ultimate comes from the class of its first ability
        public Kit UltKit => Role == Role.Wildcard ? WildKit : Kit;
        public byte UltPick;  // the key picked on the lobby's Ultimate row (LobbyEntry.Ult; 0 the default ultimate), sent in Msg.Start
        public float BaseHp;
        public string Name;
        public V2 Pos, Disp;
        public V2 Motion, MotionFrom;  // host only: how fast and which way they're moving, smoothed (the Leader aims ahead with it; Variety.cs)
        public float Aim;
        public V2 AimAt;         // the point they're aiming at (Implosion picks the enemy under it; sent with inputs)
        public bool Firing;
        public bool FiringSeen;  // holding fire, as everyone's screen knows it (for drawing beams; synced in snapshots)
        public float Hp, MaxHp;
        public float PortalT;   // your own game: just came out of a Rooms portal (Rooms.cs)
        public float Wound;     // health a hit took that can't be healed back until the wave is cleared (WoundShare of each hit; synced)
        /// The most healing can bring you to: max health less the wound.
        public float HealCap => Math.Max(1f, MaxHp - Wound);
        public bool Downed;
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, AdrenalineT, SwiftT, Flash, LevelFlash;  // AdrenalineT: the Healer's Adrenaline Shot (Adrenaline.cs)
        public float TauntT;  // Roar: enemies target this player
        public float DashT;   // replicated: the Ninja's Healing Dash, seconds of sprint left (Ninja.cs)
        public float DashHeal = 1f, DashRadius;  // host: the footprints it leaves heal this much more, and are this wide
        public V2 DashLast;   // host: where the last footprint went down
        public float AegisT, FrenzyT;  // Arcane Aegis, Alpha Call
        public float LegionT;          // replicated: seconds of Legion's Blood Moon left (Ultimates.cs, Game/LegionLook.cs)
        public float ScentT;           // host: Alpha Call's Blood Scent, which makes the frenzy build with each bite (Legion's frenzy stays flat)
        public float UnbreakT;  // Knight: Unbreakable (replicated)
        public float SteelT;    // Caught Steel (CaughtSteel.cs), replicated: seconds left to catch bullets
        public byte Steel;      // Caught Steel, replicated: shards hanging round you
        public bool GauntletDown;  // The Gauntlet (Gauntlet.cs), replicated: it lies on the floor at GauntletRest until it comes back
        public V2 GauntletRest;
        public const int WallPlates = 7;  // Shield Wall: plates across the arc, slot 3 is dead ahead
        public readonly byte[] WallPlate = new byte[WallPlates];  // Shield Wall (replicated): each plate's health, 0 = broken; the wall is up while any plate is left
        public bool WallUp { get { for (int i = 0; i < WallPlates; i++) if (WallPlate[i] > 0) return true; return false; } }
        public float VanishT, IronT;                  // Ninja: Vanish and Iron Body (replicated)
        public V2 VanishFrom;                         // where enemies last saw you before you vanished (replicated since 158: the smoke bank is drawn there)
        public float OrbitT;                          // Ninja: Blade Fan's kunai still orbiting you, seconds left (replicated)
        public int OrbitN;                            // ...and how many there are (replicated)
        public float OrbitClock;                      // host: counts up while they orbit, for each enemy's 0.5 s between cuts
        public readonly Dictionary<int, float> OrbitCut = new Dictionary<int, float>();  // host: enemy id -> OrbitClock of its last cut
        public float SpinUp;                          // Burst SMG: how spun up it is, 0 to 1 (replicated)
        public int PreyId;                            // Wolf Whistle: the enemy the pack is hunting (replicated)
        public int Orbiters;                          // Arcane Missiles: missiles circling you, waiting for an enemy (replicated)
        public float LaunchCd;                        // host: until the next one launches
        public float DoseT;                           // dosed by Medicine Darts: healing over time while it lasts (replicated)
        public float DoseHps;                         // host: how much a second
        public int DoseBy;
        public V2 KunaiMark;                          // Kunai: where the last shadow kunai landed, to blink to (replicated)
        public float KunaiMarkT;                      //   for this much longer (replicated)
        public int KunaiCount;                        // host: kunai thrown (every KunaiShadowEvery-th is a shadow kunai)
        public int Light;                             // Lightbringer: its light gauge, hits landed toward a Radiant Noon or a Sunlance (replicated; Lightbringer.cs)
        public int Combo;                             // Twin Blades and Censer: the chain step last thrown, 1 to 5 (0: the next is the first), in the low 3 bits, and a count of steps thrown above them (replicated; your own game steps you forward when the count changes); ComboT: until the chain starts over (host)
        public float ComboT;
        public float FangCd;                          // Twin Blades: until Twin Fang can be thrown again (host)
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
        public float AmbushT;                         // host: Vanish's double-damage hit is ready for this long
        public float IronPower = 1f;                  // host: Iron Body's mastery, for its shockwave
        public bool IronMastered;
        public float DebtT;                           // Blood Mage: Blood Debt's window, paid-in-full bonus included (replicated; BloodDebt.cs)
        public bool BloodWallOn;                      // Blood Wall is being laid (replicated; BloodWall.cs)
        public float BloodWallClock;                  // seconds since it opened, which sets the drain (replicated)
        public V2 WallLast;                           // host: where the last node went down
        public int WallPrev = -1, WallCount;          // host: that node's zone id (-1: the next node starts a new wall); nodes in this wall so far
        public float WallPower = 1f;                  // host: the cast's mastery power
        public bool WallMastered;                     // host
        public float Debt, DebtOwed;                  // Blood Debt: health still owed, and what the cast took (replicated, to draw the ledger)
        public float DebtRepay = 1f;                  // host: Blood Debt's repayment multiplier (mastery, Usury, power)
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
        public float HarvestClock, HarvestBlood, HarvestHold;  // Crimson Harvest (CrimsonHarvest.cs), all replicated: seconds since the cast, blood in the ball, and the hold's length (0 = no ball)
        public float RainClock;  // Sanguine Rain (SanguineRain.cs), replicated: seconds since the cast (0 = not raining)
        public float RainAcc;    // host: until the next heal tick
        public float JuggT, JuggArmor, JuggMax;  // the Juggernaut Suit (Juggernaut.cs), replicated: time left (0 = off), armour left, armour it went on with
        public bool JuggMastered;                // host: worn with the capstone (bigger shards)
        public int JuggShards;                   // host: flak shards thrown this match (tests, stats)
        public float TitheClock;  // Vein Tithe (VeinTithe.cs), replicated: seconds since the cast (0 = not holding anyone)
        public readonly List<int> Tithed = new List<int>();  // Vein Tithe: the enemies held right now, in the order the tendrils reach them (replicated)
        public bool HarvestMastered;  // (host)
        public readonly List<int> Grasp = new List<int>();  // Leech Tendrils: the enemies held right now (replicated)
        public float SteelPower = 1f; public bool SteelMastered;  // host: Caught Steel's cast
        public readonly float[] WallPlateHp = new float[WallPlates];  // host: Shield Wall plate health
        public float WallPlateMax;                                    // host: what each plate started with
        public bool WallReflect;                         // host: mastered Shield Wall
        public int SwingCount;  // host: Knight weapon state
        public byte StaffStacks;     // Quarterstaff: Jab stacks stored, 0 to 3 plus one per extra projectile (replicated, drawn on the staff and as pips behind you)
        public float StaffJabCd;     // host: the Quarterstaff's jab cools down for this long
        public byte TwinNades;       // Twin Rifle: grenades loaded in the tubes, 0 to 2 (replicated; TwinGrenades.cs)
        public byte TwinNext;        // Twin Rifle: the tube the weapon-action key fires next, 0 left, 1 right (replicated)
        public byte TwinCharge;      // Twin Rifle: hits toward the next grenade, 0 to TwinNadeHits (replicated)
        public float TwinNadeCd;
        public byte RepelMeter;      // Repulsion Cannon: the Undertow meter, 0 to 100, filled by blasts that hit (replicated; Repulsion.cs)
        public float RepelMeterF;    // host: the exact meter
        public bool RepelPull;       // Repulsion Cannon: the weapon-action key is held and the Undertow is pulling (replicated; you move at 75% and can't fire)
        public float RepelTickT;     // host: till the pull's next sting     // host: the tubes can't fire again for this long
        public byte GreatStep;       // Greatsword: which cut of the chain landed last, 0 to 3 (replicated, drawn as pips behind you)
        public byte GreatTemper;     // Greatsword: Temper stored while guarding, 0 to 6 (replicated, drawn as pips and on the blade)
        public bool GreatGuard;      // Greatsword: the weapon-action key is held, the guard arc is up (replicated; you move at half speed and can't swing)
        public bool GreatPrimed;     // Greatsword: a full-Temper Rend was fired, so the next chain starts on the overhead rend (replicated)
        public float GreatGap, GreatLock, GreatBlockT;  // host: seconds since the last cut; seconds the guard can't go up again after a Rend; a guard clang just sounded (not another for a moment)
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
        public float WarpT;         // beamed into the hub before a boss wave: can't move or shoot (replicated; BossBeam.cs)
        public float SwallowT;      // inside the Devourer: hidden, can't fire or be hit from outside (replicated; your own game holds you in it)
        public float UltT;                     // ultimate time left (Fortress: its dome's time, too)
        public float RodT;                     // Lightning Rod: seconds of charge left (replicated; LightningRod.cs)
        public int RodArcs;                    // host: how many enemies each bolt arcs on to (2, 3 mastered)
        public float DomeT;                    // Fortress: the dome's time left (replicated; Dome.cs)
        public V2 DomePos;                     // Fortress: where the dome stands (replicated)
        public int EchoLeft;                   // Riot Repeater: rounds still to fire in this burst
        public float EchoT, EchoGap;
        public int AmmoUsed;                   // Revolver: rounds fired since the last reload (replicated)
        public float ReloadT, ReloadIdle;      // Revolver: reload time left (replicated); how long since you last fired
        public bool WasFiring, TapQueued, Fanning;  // Revolver (host): tap vs hold; a click that came too early; fanning the hammer
        public float HeldT;                    // Revolver (host): how long the trigger has been held
        public float DrawT;                    // Mender's Bow: how long the current draw has been held (replicated; your own is local)
        public float BowRelease;               // Mender's Bow (host): a release waiting to fire, as its draw time; the shooter measures it
        public float BowShotDraw;              // Mender's Bow (host): the draw of the arrow being fired
        public bool Parrying;                  // holding the parry shield up (replicated)
        public float ParryHp = Game.ParryCapacity;  // damage the shield can still take (replicated)
        public float CounterT;                 // host: Counterstance fire-rate boost time left
        public float ParryBroken;              // seconds until a broken shield can be raised again (replicated)
        public float ParryHeld, ParryDown = 10f;  // host: how long it's been up / down
        public bool ParryPerfect;              // host: raised fresh, so the first moment reflects
        public float ParryBumpT;               // host: a melee hit on the shield counts once per bump
        public bool ParryActive => Parrying && ParryBroken <= 0f && !Downed;
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
        public float EnergyMax;                  // the Energy Ward's full shield (0 = the shield isn't one); replicated, with ShieldT, so every screen can draw it shrinking and turning green (Game/EnergyWardLook.cs)
        public bool EnergyOn;                    // host: the shield is an Energy Ward's, storing what it soaks to pay back as healing
        public float EnergyStore;                // host: what it has soaked
        public int EnergyBy;                     // host: who cast it
        public float WardT, WardFrac, WardInvuln;  // Martyr's ward: going down while it lasts gets you straight back up (WardT replicated)
        public float CompanionCd;              // host: summoner companion timer
        public readonly float[] HpHistory = new float[16];   // host: unused since Time Rewind was replaced by Healing Rain (every 0.25 s)
        public readonly bool[] DownHistory = new bool[16];
        public int HistoryIdx;

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public int Gold;                       // the Bazaar's gold (Shop.cs, replicated)
        public int Vectors = Game.VectorStart, VectorCap = Game.VectorCapBase;  // the Expanding arena's drawing budget and the most you hold (ExpansionGame.cs, replicated)
        public readonly int[] Shelf = NewShelf();  // this player's shop: upgrade ids, Game.ShelfSize per stock (Game.ShelfCount of them are live, replicated)
        public int ShelfSold;                  // bit per shelf slot: bought (replicated)
        public int Restocks;                   // Add stock bought this visit (replicated; each costs more, Game.StockMax at most)
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
        public V2 RadiantPos, RadiantDisp;      // ...where it is (replicated), and where this screen draws it
        public RunStats Run = new RunStats();
        public int ReviveBy;         // host: who is reviving this downed player (credited when it completes)
        public bool Ready;           // pressed Ready for the next wave (replicated by Msg.WaveReady; WaveReady.cs)
        public bool AutoReady = true;  // 'Auto-ready from now on': counts as ready as soon as their picks are done (replicated; the app sets it from the player's saved choice)
        public float LifestealPool;  // host: Royal Jelly heals at most 10 HP per second
        public readonly List<int> Taken = new List<int>();
        public Stats Stats = new Stats();
        // Which bonus ability sits on Q, E and Shift (the player picks; host-authoritative, sent with the upgrades).
        public AbilityId[] BonusKeys = { AbilityId.None, AbilityId.None, AbilityId.None };
        public Dictionary<int, float> StatOverrides = new Dictionary<int, float>();  // dev menu: hand-edited stats (index into Game.StatFields)
        public bool DevGod;  // dev menu: takes no damage
        public PerkId[] Perks = Arena.Perks.Empty;  // the perks brought into the run (host; RiotGear.cs)
        public float RiotT;  // Riot Gear: seconds until the shield is back, 0 = ready (RiotGear.cs)
        public readonly AbilitySlot[] Slots = { new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot(), new AbilitySlot() };  // Upgrades.SlotCount

        public float Speed => Def.Speed * Stats.SpeedMult * Defs.KitSpeedMult(Kit) * (AdrenalineT > 0f ? Game.AdrenalineSpeed : 1f) * (DashT > 0f ? Game.HealDashSpeed : 1f) * (SwiftT > 0f ? Game.HolySwiftMult : 1f) * (HarvestHold > 0f ? Game.HarvestSlow : 1f) * (GreatGuard ? Game.GreatGuardSlow : 1f) * (RepelPull ? Game.RepelPullSelfSlow : 1f) * (RapierGuard ? Game.RapierStanceSlow : 1f) * (KusariWhirl ? Game.KusariWhirlSlow : 1f) * (JuggT > 0f ? Game.JuggSlow : 1f);
        public float BarrierRadius => Game.BarrierRadius * Stats.BarrierRadiusMult;
        public float AuraRange => Game.AuraRange * Stats.AuraRangeMult;
    }

    public class Enemy
    {
        public int Id;
        public V2 KnockLeft;    // host only: a push still to travel, and how fast (Knock.cs)
        public float KnockSpeed, KnockFxAt = -10f;
        public int ImpactBy;    // host only: a Lance shove's owner id + 1 while it slides (0: none); fast enough, it hits what it meets (Lance.cs)
        public int ImpactLast = -1;  // host only: the enemy it last hit that way (each pair once)
        public float LitT;      // host: seconds held in a Searchlight (below 0: just locked, resting) (Stun.cs)
        public EnemyType Type;
        public EnemyDef Def;
        public V2 Pos, Disp, Waypoint;
        public int Home = -1;   // host only: the Rooms arena's area it belongs to (Rooms.cs): a room it guards, or the vault
        public bool Spilled;    // host only: sent out of its room in a spill, or overflowed out of it (it hunts instead of guarding)
        public float HomeT;     // host only: how long it has guarded its room (Rooms.GuardTime, then it overflows)
        public int ExitDoor = -1;  // host only: the door an overflowing enemy is leaving its room by (Rooms.Doors index)
        public float PortalT;      // host only: just came out of a Rooms portal (it can't take one again until this runs out)
        public float Hp, MaxHp, Radius, Speed;
        public int Score;
        public float T, SpawnT, FireT, Ang, Strafe = 1f, RetargetT, Flash, T2;
        public float FlashHeat;  // every peer, drawing only: rises while it keeps getting hit, so a hit's white flash softens under sustained fire (Juice.cs HitFlashMix)
        public float Facing;    // direction it faces (shields, charges)
        public V2 Vel;          // host only: a Buzzsaw's steering velocity (it swoops instead of turning on the spot)
        public byte Phase;      // per-type state, e.g. charger wind-up / dash, boss shield
        public float PhaseT;
        public byte SeenPhase;  // every peer, drawing only: the Phase the swing look last saw, and how long ago it changed (Game.cs; Swordsman.cs)
        public float SeenT;
        public bool Elite;
        public byte Traits;           // an elite's traits (EliteTrait flags; Variety.cs; replicated)
        public int SquadId, SquadSlot;  // host: the squad it arrived with, and its place in it (Variety.cs)
        public float BubbleHp, BubbleMax, BubbleT;  // a bubble that soaks damage first: Shielded, or a Warder's (host; drawn from BubbleFrac)
        public float BubbleFrac;      // replicated
        public float[] CellHp;        // host: a bubble's twelve cells, each holding a twelfth of its health; a hit wears down the cells on the side it came from (Variety.cs)
        public int Cells;             // replicated: bits 0-11 are the cells still up, bits 12-23 the ones worn down (drawn from Variety's look)
        public int CellsSeen;         // drawing only: the Cells this peer last saw, to flash the cells that changed
        public float[] CellHitT;      // drawing only: when each cell last took a hit
        public float[] CellDieT;      // drawing only: when each cell last broke
        public float BubbleBornT = -99f;  // drawing only: when this peer first saw the bubble go up (-98: seen without one, so the next bubble is new; -99: never seen without, so no growing in)
        public int WardId;            // the Warder holding up its bubble (replicated, for the tether)
        public int MirrorCount;       // host: player shots that have hit it (Mirrored bounces every fourth)
        public float WardT;           // host: a Warder's time until it wards again
        public float MarkT, MarkMult = 1f;  // Mark for Death (host); Marked is the replicated flag
        public float ChallengeT;            // replicated: seconds left of a Gauntlet's Challenge (Gauntlet.cs)
        public int ChallengeBy;             // replicated: who threw it
        public float PowerScale = 1f;       // host: a boss's extra health for a strong party (BuildPower.cs)
        public float ShootT, ShootRest, ShootWait;  // host: its turn to shoot, resting after one, and how long it's waited (Readability.cs)
        public bool Winding;                // about to fire a volley: it glows (replicated; Readability.cs)
        public float SlowT, SlowMult = 1f;  // Graviton Rounds (host)
        public float WadeT, WadeMult = 1f;  // host: wading in a Bloodbath, for Heavy enemies and bosses, which the usual slow skips (Bloodbath.cs)
        public float BurnT, BurnDps;        // the Flamethrower's fire (host)
        public float BurnPool, PoolT;       // Incendiary Rounds: damage still to burn, and the time left to burn it in (host)
        public int BurnBy;
        public float BleedT, BleedDps;      // Bear Totem's claws (host)
        public int BleedHits;               // the claw hits stacked on it, up to five (replicated, for the claw scars: Game/BearLooks.cs)
        public int BleedBy;
        public bool Burning, Chilled, Frozen, Bleeding;  // replicated, for drawing (Chilled = slowed but not frozen)
        public float WetT, WetArcCd;        // Tsunami: wet (slowed, and lightning arcs to it), and the pause before it can be arced again (host)
        public bool Wet;                    // replicated, for the drips (Game/TsunamiLook.cs)
        public float FrozenT;               // Glacial Tomb: can't move or shoot (host)
        public float IceT, IceMult = 1f;    // Blood Freeze: seconds left of its ice, and the damage multiplier while it lasts (host; BloodFreeze.cs)
        public float GraspT;                // Leech Tendrils: held, can't move or shoot (host)
        public int ReelBy;                  // host: Exsanguinate: the Hemomancer reeling it in (0: nobody; Exsanguinate.cs)
        public float ReelT, ReelHeal, ReelDmg;  // host: seconds into its reel (negative while staggered), and what it drains when it lands
        public V2 ReelFrom;                 // host: where it was hooked
        public bool ReelCap;                // host: the cast was mastered (it lands slowed)
        public int BoneDarts;               // host: Bone Guard darts stuck in it (the third cages it; Minions.cs)
        public float BoneDartT, CageT, CageImmuneT;  // host: until the darts fall out; caged (can't move or shoot); until darts count again
        public bool Caged;                  // replicated, for drawing (and the Skeleton Guards' aim)
        public int LastHitBy;
        public bool Penned;                 // replicated: held in by an ink loop (Chalk.cs), for drawing
        public int PenId;                   // host: the ink loop (Chalk.cs InkPen) holding it in, 0 if none
        public V2 PenPos;                   // host: where it was last inside that loop
        public bool Dummy;                  // host: a target dummy for balancing (DpsTools.cs): never dies, shoots or hurts
        public float DummyTaken;            // host: damage a dummy has taken since the meter was reset
        public V2 DummyHome;                // host: where a dummy stands (or patrols about)
        public bool Marked;
        public bool Risen;
        public float FrenzyT;   // host: a Shrieker's scream: 30% faster and set on you (Zombies.cs)
        public int GrabId;      // host: a Crawler holding this player (0: nobody)
        public float GrabHp;    //   and its health when it grabbed (25 damage since then frees you)      // host: a Zombie Infection zombie that got back up (no XP; won't rise again; Zombies.cs)
        public int TargetId, ParentId;
        public int CueSig = int.MinValue;  // host: the boss attack last announced with a sound
        public bool Spawning;  // replicated SpawnT > 0 on clients
        public WardenState Warden;  // the Warden's fight (Warden.cs; replicated)
        public QueenState Queen;    // the Hive Queen's fight (Queen.cs; replicated)
        public SingState Sing;      // the Singularity's fight (Singularity.cs; replicated)
        public HeraldState Herald;  // the Storm Herald's fight (Herald.cs; replicated)
        public EclipseState Eclipse;  // the Eclipse's fight (Eclipse.cs; replicated)
        public DevourState Devour;  // the Devourer's fight (Devourer.cs; replicated)
        public DuelState Duel;      // the Duelist's fight (Duelist.cs; replicated)
        public LichState Lich;      // the Lich's fight (Lich.cs; replicated)
        public ColossusState Colossus;  // the Colossus's fight (Colossus.cs; replicated)
        public float ShedT;           // host: until the Repulsion Cannon can knock another health orb out of it
        public int SweepMark;         // host: the last Halberd sweep that hit it (once a sweep)
        public float KatanaShockT;    // host: marked by a Katana's Momentum Burst for this long (caltrops thrown then seek it; Katana.cs)
        public bool KatanaShocked;    // marked (replicated)
        public float ShieldHp, ShieldMax;  // host: a Shielder's shield, worn down by blocked hits and time (its Phase shows the cracks)
        public int Hemorrhage;               // Blood Bolts stuck in it (the fifth bursts; BloodMage.cs); replicated, for drawing
        public float HemorrhageT;            // host: until they fade
        public float KusariCd;               // host: the Kusarigama's whirl can hit it again after this long
        public float Suppress;               // host: the Riot Repeater's suppression, 0 to SuppressMax (slows its shooting)
        public bool Suppressed;              // replicated, for drawing
        public float PoisonT, PoisonDps;     // host: Medicine Darts' poison (stacking)
        public int PoisonBy;
        public bool Poisoned;                // replicated, for drawing
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
        public float AngVel, Life, Radius, Dmg;
        public float Stasis;      // 0..1: how far a Lumen Ward has slowed it to a stop (worked out the same on every peer, not sent; LumenWard.cs)
        public float Life0;       // its lifetime when fired (for drawing it aging: the Flamethrower's flames)
        public int Owner, Pierce, LastHit;
        public int Shooter;       // host only: the enemy that fired it, when known (0: not known); the Juggernaut Suit's flak goes back at it
        public bool FromWeapon;   // host only: fired by the player's gun (for accuracy)
        public float Splash;      // host only: explosion radius on impact
        public bool PointBlank;   // host only: a Scattergun pellet (double damage and a shove up close)
        public ShotLook Look;     // its shape: a dart (aimed), a crescent (curving) or round (sent with its spawn; Readability.cs)
        public Bullet Sent;       // host only: its entry in the spawn message still to go out (so its Look can be set after)
        public bool Rush;         // host only: a Pauser's shot; when it runs out (after hanging still) one rushes at a player from where it hung
        public bool Suppressive;  // host only: a Riot Repeater round, which slows how fast what it hits shoots
        public bool Medicine;     // host only: a Medicine Dart (poisons enemies, doses allies with healing over time)
        public bool ShadowKunai;  // host only: a shadow kunai, which leaves a mark where it lands to blink to
        public bool Sticks;       // host only: a Shuriken star, which sticks in what it hits (to be recalled)
        public bool DropsSpear;   // host only: a Bone Spear, which stays stuck where it stops (a pickup)
        public bool Ward;         // host only: an Ofuda charm flying to where it will lie as a ward
        public bool Bolts;        // host only: a piercing shot whose hits fling Ricochet's bounce bolts instead of it bouncing (Combat.cs)
        public bool Crossfire;    // host only: a Twin Rifle round, double near Cross (where the streams meet)
        public V2 Cross;
        public float HealAlly;    // host only: heals an ally it passes through
        public float WardAlly;    // host only: wards an ally it passes through instead (Mercy Cut's crescent; Lightbringer.cs)
        public float HealBurst;   // host only: heals allies near where it bursts (the Ninja's Ofuda)
        public float Pool, PoolTime;  // host only: leaves a Blood Pool this big, for this long, where it lands (the Clot Cannon)
        public int Burst;         // host only: mortar bombs burst into this many bullets when they land
        public int Hops;          // host only: a Sprite Staff firefly hops to this many more unseeded enemies when it lands (KitWeapons.cs)
        public int Chain;         // host only: lightning jumps to this many more enemies
        public int Shock;         // host only: electrifies the enemy it hits, which arcs to this many others round it (the Storm Hammer)
        public float SlowAmt;     // host only: frost slows enemies hit
        public float Ignite;      // host only: sets enemies hit burning for this much a second (Flamethrower)
        public bool Pierced;      // host only: a Flamethrower flame that has hit its first enemy (it does less after)
        public bool FromBoss;     // fired by a boss (only these wear blockers down; drawn with a dark core)
        public float Homing;      // Homing Rounds: turn rate toward the nearest enemy (host corrects clients)
        public int Bounces;       // host only: Ricochet bounces left
        public int Frags;         // host only: on hit, throws this many fragments (Frost Lance, Railgun, Flak, Fireball)
        public byte FragKind;
        public float FragDmg, FragSpeed, FragLife, FragSplash;
        public bool FragSideways; // fragments fly out to the sides (shatter, shrapnel) rather than all around (bomblets)
        public bool Bomblet;      // host only: a Flak Cannon bomblet; it pops where it lands (Burst marks it as landing)
        public bool Pulled;       // host only: bent by a gravity well (so the host sends its course)
        public bool StunBack;     // host only: a Warden's stun shot sent back by a perfect parry (stuns the Warden)
        public bool Tossed;       // host only: a Katana caltrop still in the air: it flies over enemies and lies where it lands (Katana.cs)
        public EnemyType? Source; // host only: the enemy that fired it (for the session record)
        public int ShooterId;     // host only: the id of the enemy that fired it (0: not known); Lightning Rod calls its bolt on it
        public V2 ShooterPos;     // host only: where that enemy stood when it fired (the bolt still lands there if it has died)
    }

    /// A health pickup dropped by a dead enemy.
    public class Orb
    {
        public int Id;
        public bool Big, Tiny;  // Tiny: knocked loose by the Repulsion Cannon
        public bool Med;        // a Deployable Medkit's orb (Medkit.cs)
        public float MedHeal;   // host: what it heals
        public V2 Pos, Disp;
        public float Life;

        public float Radius => Big ? 11f : Med ? 8f : Tiny ? 4f : 6f;
        public float Heal => Med ? MedHeal : Big ? Game.BigOrbHeal : Tiny ? Game.TinyOrbHeal : Game.OrbHeal;
    }

    /// An area on the ground: Flame Wall, Blizzard, smoke, pools, wards, a meteor's warning and the like (ZoneKind).
    public class Zone
    {
        public int Id, Owner;
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
        public float Aim;           // replicated: a Lumen Ward's cone points this way (radians; Pos is its apex, Radius its length); a Vein Trail: 1 when it will burst
        public bool Freezes;        // host: a mastered Lumen Ward freezes enemies outside its light (not bosses)
        public bool Consecrates;    // host: Hallowed Blade blesses the ground where it lands
        public float BlockHp;       // host: damage it can still soak (blocking zones)
        public float GuardFrac;     // replicated BlockHp fraction, for drawing (a Spirit Ward's soak)
        public bool Congealed;      // host: a Blood Pool that has set (BloodMage.cs)
        public bool Bursts;         // host: a mastered Vein Rush trail that hasn't burst yet (VeinRush.cs)
        public float Soak, SoakMax, Soaked;  // host: a Spirit Ward's damage left to take for allies, its most, and what it has taken (Ninja.cs)
        public float TickT, HealBack;        // host: a Bloodbath's time toward its next bleed tick, and the share of it that heals its owner (Bloodbath.cs)
        public byte[] Bones;        // replicated, a Bone Phalanx: each skeleton's health as a fraction of 255 (0 = broken); its length is how many (BonePhalanx.cs)
        public float Marched;       // replicated, a Bone Phalanx: px the line has walked from the gate
        public float[] BoneHp, SpearT;  // host, a Bone Phalanx: each skeleton's health and time to its next spear
        public float BoneMax;       // host, a Bone Phalanx: a skeleton's full health
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
        public float Ring;               // host: a Twin Scythe's circle round its owner, signed by which way it spins (ReaperLoot.cs)
    }

    public class Effect
    {
        public FxKind Kind;
        public V2 Pos, Pos2;  // Pos2: the far end of a lightning bolt
        public Rgba Color;
        public float Radius, T, Dur;
        public bool Juiced;  // the screen's visual effects have seen it (Game/Juice.cs); never sent
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
        public bool WeaponAction;  // held: your weapon's secondary (Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama whirl); this machine only
    }
}

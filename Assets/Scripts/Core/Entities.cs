using System;
using System.Collections.Generic;
using System.IO;

namespace Arena
{
    // The match's data types: states, effects and sounds, and the Player / Enemy / Bullet / Orb / Zone / Minion /
    // Effect classes the Game holds.
    public enum MatchState : byte { Idle, Fighting, Intermission, GameOver, Victory }

    public enum FxKind : byte { Boom, Spark, Ring, Bolt, Target, Slash, Jab, Shatter, Claw, Implode, Repulse, Icicle, Splinter, LightCone, BloodCoat, ArcField, ChainThrow, IonTrail, SteelWall, Debris, Callout, Bleed, BloodShell, Drain, Boil, PactBurst, Link, Rewind, Knock, RootLash, Strike, Tsunami, Meteor, Lifeline, RiotBlock, DeathLotus, ShadowStep, LeapSlam, ShellHit, ShellPop, HolyCross, KatanaStrike, StandHit, CavalryCall, KatanaCut, HarvestThrow, StaffShove, RainRelease }  // Target: a marker where something is about to land; Slash: a melee swing (Pos2 = tip, Radius = half the arc); Jab: the Lance's box (Pos2 = far end, Radius = half its width); Shatter: a parry shield breaking (Pos = its owner, Pos2 = the shield's middle, Radius = half its arc); Claw: a bear-claw swipe (Pos2 = reach, Radius = half the arc, signed by the way it rakes); Implode: blood collapsing into an enemy (Implosion); Icicle: the Frost Lance's icicle (Pos2 = its point, Radius = half its width); Splinter: shards off its sides (Pos2 = its point, Radius = how far out they fly); LightCone: Chain Light (Pos2 = the middle of its far edge, Radius = half its angle); BloodCoat: a Sanguine Orb circling in on an enemy (Pos2.X = the enemy's id, Radius = its radius); ArcField: Arc Lightning going off (Pos2 = the far end of its strip, Radius = half its width); ChainThrow: the Kusarigama's chain flying out (Pos2 = where it ends, Radius = 1 when it hooked something); IonTrail: a Railgun shot's charged path (Pos2 = its far end, Radius = the round's speed); SteelWall: a Greatsword's lingering crescent (Pos = the swing's centre, Pos2 = the middle of its edge, Radius = half its arc); Debris: a crate or pot smashed (Props.cs); Callout: an ability's name over whoever cast it (Pos2 = (player id, AbilityId), drawn on the HUD); Bleed: a Hemomancer paying health for an ability (Pos2.X = their id, Radius = health paid); BloodShell: Blood Shell forming (Pos2.X = the caster's id); Drain: blood flowing from Pos to Pos2 (Exsanguinate, Blood Gift, Martyr; Radius = how thick); Boil: an enemy boiling before its Blood Boil bursts (Pos2.X = the enemy's id); PactBurst: Crimson Pact's stored damage bursting (Radius = its reach); Link: a beam of light from a caster (Pos) to whoever their ability reached (Pos2); Rewind: Time Rewind turning a player's clock back (Pos2.X = their id); RootLash: the Treant Rod's root whipping out (Pos2 = its tip, Radius = half its width); Strike: the Nimbus's lightning landing (Radius = its blast); Tsunami: a wave rolling from Pos to Pos2 (Radius = half its width; Tsunami.cs); Meteor: one landing (Radius = its blast; drawn by Game/ElementalistLooks.cs); Lifeline: one per player the ultimate reached (Pos = where the caster stood, Pos2 = (that player's id, the caster's id), Radius = how far they were; Game/LifelineLook.cs); RiotBlock: Riot Gear taking a hit (Pos = its owner, Pos2 = where the hit came from, Radius = the plate's reach; Game/RiotGearLook.cs); DeathLotus: the Ninja's Death Lotus (Pos2.X = -1 for the cast itself, Pos = the Ninja, Pos2.Y = their id, Radius = targets struck; otherwise one per enemy struck, Pos = the enemy, Pos2.X = seconds until its cuts land, Radius = its size; Game/DeathLotusLook.cs); ShadowStep: the smoke look of a Ninja's step (Pos = where they stood, Pos2 = where they landed, Radius = their id * 1000 + the radius of the enemy they struck, or just their id * 1000 when it was a short step with no enemy; Game/ShadowStepLook.cs); LeapSlam: the Knight's Leap Slam, sent once at the cast and lasting its leap, landing and stun (Pos = where they leapt from, Pos2 = where they land, Radius = their id * 1000 + the slam's radius, Color.A = 1 when mastered and 0.5 otherwise, Dur = LeapTime + LeapFxTail + the stun; Game/LeapSlamLook.cs); HolyCross: a Holy Cross flying from Pos to Pos2 (Radius = its flight, Dur = flight + fade; Game/HolyCrossLook.cs); KatanaStrike: the Katana's Alt strike from the stance to its fading line, Pos to Pos2 (Radius = the striker's id, Color.A = 1 when empowered, 0.5 otherwise, Dur = the whole show; Game/KatanaLook.cs); KatanaCut: one enemy the strike cut (Pos = the enemy, Pos2 = the line's direction, Radius = its size, Color.A = 1 when it died, 0.5 when it lived); StandHit: a shot stopped by a planted Last Stand (Pos = where, Pos2 = the knight, Radius = its damage; Game/LastStandLook.cs); CavalryCall: Cavalry Call (Pos = where the knight stood, Pos2 = the lane's far end, Radius = lancers (2 or 4), colour alpha 1 = mastered; Game/CavalryCallLook.cs); HarvestThrow: a Crimson Harvest ball thrown from Pos (the Hemomancer's feet) to Pos2, flight then blast then the stain, drawn from its age (Radius = the blood in it, Dur = flight + tail; Game/CrimsonHarvestLook.cs); StaffShove: the Bo Staff's shove (Pos = the Ninja, Pos2 = the far end of the bar's push, Radius = half the bar's width; Game/BoStaffLook.cs); RainRelease: a Sanguine Rain ring splashing away when the rain ends (Pos = where the ring was, Radius = its size; Game/SanguineRainLook.cs)

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
    }

    public struct SoundCue
    {
        public Sfx Sfx;
        public bool Ally;  // your friend's shot: played quieter

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
        public float BaseHp;
        public string Name;
        public V2 Pos, Disp;
        public V2 Motion, MotionFrom;  // host only: how fast and which way they're moving, smoothed (the Leader aims ahead with it; Variety.cs)
        public float Aim;
        public V2 AimAt;         // the point they're aiming at (Implosion picks the enemy under it; sent with inputs)
        public bool Firing;
        public bool FiringSeen;  // holding fire, as everyone's screen knows it (for drawing beams; synced in snapshots)
        public float Hp, MaxHp;
        public bool Downed;
        public float Revive, FireCd, BarrierT, InvulnT, OverdriveT, AdrenalineT, SwiftT, Flash, LevelFlash;  // AdrenalineT: the Healer's Adrenaline Shot (Adrenaline.cs)
        public float TauntT;  // Roar: enemies target this player
        public float DashT;   // replicated: the Ninja's Healing Dash, seconds of sprint left (Ninja.cs)
        public float DashHeal = 1f, DashRadius;  // host: the footprints it leaves heal this much more, and are this wide
        public V2 DashLast;   // host: where the last footprint went down
        public float AegisT, FrenzyT;  // Arcane Aegis, Alpha Call
        public float LegionT;          // replicated: seconds of Legion's Blood Moon left (Ultimates.cs, Game/LegionLook.cs)
        public float ScentT;           // host: Alpha Call's Blood Scent, which makes the frenzy build with each bite (Legion's frenzy stays flat)
        public float UnbreakT, FuryT, WhirlT;  // Knight: Unbreakable, Battle Fury, Whirlwind (replicated)
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
        public int Light;                             // Lightbringer: its light gauge, hits landed toward a radiant swing (replicated)
        public V2 LastIncense;                        // host: where the Censer last left incense
        public int Combo;                             // Twin Blades: slashes landed in a row (replicated); ComboT: until it lapses (host)
        public float ComboT;
        public byte KatanaChain;                      // Katana: kills in a row with the Alt strike (replicated); the meter doesn't drain while it is above 0, and at KatanaChainNeed the next strike is empowered
        public byte KatanaStep, KatanaLunges;         // Katana: which of swipe, swipe, lunge comes next (0, 1, 2); how many lunges they have thrown (wraps; the player's own game steps them forward when it changes) (both replicated)
        public float KatanaMeter;                     // Katana: the strike meter, 0 to KatanaMeterMax (replicated); built by hits, drained when you stop hitting, spent by the Alt strike
        public byte BoStep;                           // Bo Staff: which of slash, jab, spin comes next (0, 1, 2); there is no timer, a click always carries on (replicated)
        public float ShoveCd;                         // host: the Bo Staff's shove cools down for this long
        public float KatanaGap, KatanaIdle;           // host: time since the last step began (negative while it plays); time since the last hit
        public float PreyT, PackT;                    // host: until the mark fades; the window for every wolf to bite
        public int HookId;                            // Kusarigama: the enemy its chain is hooked in (replicated)
        public float HookT;                           //   for this much longer (replicated)
        public float AmbushT;                         // host: Vanish's double-damage hit is ready for this long
        public float IronPower = 1f;                  // host: Iron Body's mastery, for its shockwave
        public bool IronMastered;
        public float PactT, RushT;                    // Blood Mage: Crimson Pact and Frenzy Pact (replicated)
        public float BroodCd;                         // host: Brood Mother's next turret while hitting a boss (Combat.cs)
        public int BroodDue;                          // host: Brood Mother turrets to place behind you this tick
        public float BoltStore, BoltCd;               // host: Ricochet on hits that can't bounce, stored toward the next bounce bolt (Combat.cs)
        public float BloodReserve;                    // Sanguine Orb: heal reserve charged by coating enemies (replicated, whole points)
        public float CoatT;                           // coated in a Sanguine Orb's healing blood: heals while it lasts (replicated)
        public int CoatBy;                            // host: who coated them
        public int Clot, ClotMax = 5;                 // Blood Lash: Clot stacks and the most it can hold (replicated)
        public float ClotT;                           // host: until the next Clot stack fades
        public float PactStore, PactPower = 1f, PactRadius;  // damage stored by the Pact and its burst's reach (replicated, to draw them)
        public float HarvestClock, HarvestBlood, HarvestHold;  // Crimson Harvest (CrimsonHarvest.cs), all replicated: seconds since the cast, blood in the ball, and the hold's length (0 = no ball)
        public float RainClock;  // Sanguine Rain (SanguineRain.cs), replicated: seconds since the cast (0 = not raining)
        public float RainAcc;    // host: until the next heal tick
        public bool PactMastered, RushMastered, HarvestMastered;  // (host)
        public readonly List<int> Grasp = new List<int>();  // Leech Tendrils: the enemies held right now (replicated)
        public float WhirlTick, WhirlPower = 1f;  // host
        public readonly float[] WallPlateHp = new float[WallPlates];  // host: Shield Wall plate health
        public float WallPlateMax;                                    // host: what each plate started with
        public bool WallReflect, WhirlAll;               // host: mastered Shield Wall / Whirlwind
        public int SwingCount, RapierTarget, RapierStreak;  // host: Knight weapon state
        public float BossContactT;  // host: a boss's body can only hurt you once a second
        public float StunT;         // stunned by the Warden's stun shot: can't move (replicated; your own game stops you)
        public float BashGraceT;    // host: until a Riot Shield can bash this player again (WardenFaction.cs)
        public float SwallowT;      // inside the Devourer: hidden, can't fire or be hit from outside (replicated; your own game holds you in it)
        public float UltT;                     // ultimate time left (Fortress: its dome's time, too)
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
        public bool ShellHold, ShellMastered;  // host: the ability key is held; a mastered shell erases bullets as it bursts
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
        public float LinkT;   // Soul Link: protecting LinkTo
        public int LinkTo;

        public int Level = 1, PendingPicks, Rerolls = Game.StartingRerolls;
        public int Gold;                       // the Bazaar's gold (Shop.cs, replicated)
        public readonly int[] Shelf = { -1, -1, -1, -1, -1, -1 };  // this player's shop: upgrade ids (Game.ShelfSize, replicated)
        public byte ShelfSold;                 // bit per shelf slot: bought (replicated)
        public int Restocks;                   // shelf restocks this visit (replicated; each costs more)
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

        public float Speed => Def.Speed * Stats.SpeedMult * Defs.KitSpeedMult(Kit) * (AdrenalineT > 0f ? Game.AdrenalineSpeed : 1f) * (DashT > 0f ? Game.HealDashSpeed : 1f) * (SwiftT > 0f ? Game.HolySwiftMult : 1f) * (HarvestHold > 0f ? Game.HarvestSlow : 1f);
        public float BarrierRadius => Game.BarrierRadius * Stats.BarrierRadiusMult;
        public float AuraRange => Game.AuraRange * Stats.AuraRangeMult;
    }

    public class Enemy
    {
        public int Id;
        public V2 KnockLeft;    // host only: a push still to travel, and how fast (Knock.cs)
        public float KnockSpeed, KnockFxAt = -10f;
        public float LitT;      // host: seconds held in a Searchlight (below 0: just locked, resting) (Stun.cs)
        public EnemyType Type;
        public EnemyDef Def;
        public V2 Pos, Disp, Waypoint;
        public float Hp, MaxHp, Radius, Speed;
        public int Score;
        public float T, SpawnT, FireT, Ang, Strafe = 1f, RetargetT, Flash, T2;
        public float Facing;    // direction it faces (shields, charges)
        public V2 Vel;          // host only: a Buzzsaw's steering velocity (it swoops instead of turning on the spot)
        public byte Phase;      // per-type state, e.g. charger wind-up / dash, boss shield
        public float PhaseT;
        public bool Elite;
        public byte Traits;           // an elite's traits (EliteTrait flags; Variety.cs; replicated)
        public int SquadId, SquadSlot;  // host: the squad it arrived with, and its place in it (Variety.cs)
        public float BubbleHp, BubbleMax, BubbleT;  // a bubble that soaks damage first: Shielded, or a Warder's (host; drawn from BubbleFrac)
        public float BubbleFrac;      // replicated
        public int WardId;            // the Warder holding up its bubble (replicated, for the tether)
        public int MirrorCount;       // host: player shots that have hit it (Mirrored bounces every fourth)
        public float WardT;           // host: a Warder's time until it wards again
        public float MarkT, MarkMult = 1f;  // Mark for Death (host); Marked is the replicated flag
        public float PowerScale = 1f;       // host: a boss's extra health for a strong party (BuildPower.cs)
        public float ShootT, ShootRest, ShootWait;  // host: its turn to shoot, resting after one, and how long it's waited (Readability.cs)
        public bool Winding;                // about to fire a volley: it glows (replicated; Readability.cs)
        public float SlowT, SlowMult = 1f;  // Graviton Rounds (host)
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
        public float GraspT;                // Leech Tendrils: held, can't move or shoot (host)
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
        public float ShieldHp, ShieldMax;  // host: a Shielder's shield, worn down by blocked hits and time (its Phase shows the cracks)
        public int Hemorrhage;               // Blood Bolts stuck in it (the fifth bursts; BloodMage.cs); replicated, for drawing
        public float HemorrhageT;            // host: until they fade
        public float Suppress;               // host: the Riot Repeater's suppression, 0 to SuppressMax (slows its shooting)
        public bool Suppressed;              // replicated, for drawing
        public float PoisonT, PoisonDps;     // host: Medicine Darts' poison (stacking)
        public int PoisonBy;
        public bool Poisoned;                // replicated, for drawing
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
        public float HealBurst;   // host only: heals allies near where it bursts (the Ninja's Ofuda)
        public float HealOwner;   // host only: heals whoever fired it this much when it hits (Hemo Nova)
        public float Pool, PoolTime;  // host only: leaves a Blood Pool this big, for this long, where it hits (Clot Bolt)
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
        public EnemyType? Source; // host only: the enemy that fired it (for the session record)
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

    /// An area on the ground: Flame Wall, Blizzard, Soul Lantern, healing totem or a meteor's warning.
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
        public float Aim;           // replicated: a Lumen Ward's cone points this way (radians; Pos is its apex, Radius its length)
        public bool Freezes;        // host: a mastered Lumen Ward freezes enemies outside its light (not bosses)
        public bool Consecrates;    // host: Hallowed Blade blesses the ground where it lands
        public float BlockHp;       // host: damage it can still soak (blocking zones)
        public float GuardFrac;     // replicated BlockHp fraction, for drawing (a Spirit Ward's soak)
        public bool Congealed;      // host: a Blood Pool that has set (BloodMage.cs)
        public float Soak, SoakMax, Soaked;  // host: a Spirit Ward's damage left to take for allies, its most, and what it has taken (Ninja.cs)
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
        public float Hp, MaxHp, HurtT;   // host: a Warden's Guard's or Soul Ghost's health, and its contact-damage cooldown
        public float InvulnT;            // host: a Soul Ghost from a mastered Soul Link can't be hurt at first
        public bool Mastered;            // host: summoned by a mastered ability (Phoenix, Guardian Spirit)
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
        public bool WeaponAction;  // held: your weapon's secondary (Kunai blink, Twin Blades lunge, Shuriken recall, Bo Staff vault, Kusarigama reel); this machine only
    }
}

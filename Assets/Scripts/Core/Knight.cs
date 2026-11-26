using System;
using System.Collections.Generic;

namespace Arena
{
    /// One melee strike, waiting to land (host only). Swings are resolved inside Collide so a hit goes
    /// through the same on-hit pipeline as a bullet: weapon mods, legendaries, crits, lifesteal, kills.
    public class Swing
    {
        public int Owner;
        public float Aim, Reach, HalfArc, Dmg, Knock, Delay;  // Knock 0 = the standard melee shove (MeleeHit.cs)
        public bool NoKnock;          // dazes but doesn't shove (the Flurry's jabs)
        public int MaxTargets;        // 0 = everything in the arc; otherwise the nearest few (Rapier)
        public bool CutAll;           // also cuts big orbs and mines (it was the mastered Whirlwind's; nothing sets it today)
        public float HealPerHit, HealRange;
        public int HealHits;          // most enemies a swing's healing counts (Quarterstaff)
        public float HealSpin;        // heals allies near you once per swing (Censer)
        public bool Rapier, Heavy, Silent;
        public bool FromWeapon;       // queued by the player's weapon (QueueSwing): the session recording credits its hits to the weapon
        public bool Rend;             // a long box drawn as a lance of light (the Greatsword's Eclipse), Power = Temper / GreatTemperMax (1: gold)
        public byte Great;            // a Greatsword chain hit (Game.GreatMove; GreatswordChain.cs): the parry takes back one still winding up, a ring draws as one
        public int Waves;             // sword waves launched as it lands (the Greatsword's Eclipse with extra projectiles)
        public float WaveDmg;
        public bool Staff;            // each enemy it hits adds a Jab stack (the Quarterstaff's swipes; Quarterstaff.cs)
        public bool Thrust;           // a forward jab (Lance, Halberd, Rapier): drawn as a thrust, not a sweep
        public float Stun;            // freezes what it hits for this long (not bosses)
        public bool StarStun;         // the Stun is a plain stun (Daze.cs Stun: stars, not ice; the Arc Knuckles)
        public float Circle, CircleAt, Power;  // Circle > 0: hits everything within this radius of the point CircleAt px along the aim (the Warhammer's slam); Power 0..1 is how charged it was (its look)
        public float HalfWidth;       // > 0: hits a rectangle this wide either side of the aim line, out to Reach (Lance), not an arc
        public bool NoCut;            // doesn't cut bullets (the Ninja's Kusarigama: a chain, not a blade)
        public bool Deflect;          // bats enemy bullets in the arc back as your own (the Quarterstaff: it parries as it swings)
        public float Bleed;           // each hit bleeds for this much of its damage per second (Bear Totem)
        public float ClawSide;        // != 0: a bear-claw swipe, drawn as claw marks raking this way (+1 / -1)
        public float Tip;             // > 0: a whip (the Hemomancer's Blood Lash): hits beyond this distance crack for double
        public int Links;             // > 0: a Blood Lash step: hooks a chain on what it hits, this many links (BloodLash.cs)
        public float Taunt;           // taunts what it hits for this long (the Blood Lash's Snap)
        public int Sweep;             // > 0: one step of a sweep (Halberd): each enemy is hit once per sweep, however many steps reach it
        public bool NoFx;             // draws nothing itself (a Halberd step after the first: the first draws the whole sweep)
        public float FxAim, FxHalf;   // FxHalf > 0: its effect is this arc instead of its own (the Halberd's whole sweep)
        public float Hollow;          // a box that skips enemies centred within this distance of the aim line (the splinters miss the icicle's own line)
        public float Slow;            // slows what it hits by this much (as frost shots do)
        public bool DrawCut;          // the Katana's dash-cut: a box (HalfWidth) drawn as a long thin slash, not a jab
        public byte KatanaMove;       // a Katana step (Katana.cs): 1 a dash (a kill brings the next step at once), 2 any other cut
        public bool FromSet;          // lands from From, not from where the swinger stands by then (the Katana's dash-cut, along the line you dashed)
        public V2 From;
        public Sfx Sound;             // played in place of the usual swing sound (the Katana's own: KatanaSoundBank.cs)
        public bool Root;             // the Treant Rod's root lash: a box drawn as a thorny root (FxKind.RootLash) that heals allies inside it
        public float RootHeal;        // what an ally touched by the lash is healed
        public bool Twin;             // a Twin Blades step (TwinBlades.cs): Twin Fang and the parry take back one still winding up
        public byte TwinKind;         // which step (1-5): its hit cue's sound and your hit-pause (TwinHitCue)
        public byte LightKind;        // a Lightbringer step (1 Dawn, 2 Mercy, 3 Noon; Lightbringer.cs): fills the gauge, throws its crescents or heals its ring
        public bool LightRadiant;     // a Radiant Noon: three times the healing, a ward on everyone in the ring
        public int LightWaves;        // Dawn's and Mercy's crescents of light
        public float LightWaveDmg;
        public byte CenserStep;       // a Censer step (1-3; Censer.cs): its hits light Embers, and it leaves incense as it lands
        public byte LanceKind;        // a Lance step (1-4; Lance.cs): its hits are cued and spark, and what it shoves can Impact; Hook & Haul takes back one still winding up
        public float LanceAcross;     // the Lance's Wide Sweep: the way its swing bats what it hits (LanceKnockDir)
        public byte Halberd;          // a Reaper's Wheel step (1-4) or the Pike Lunge (5; Halberd.cs): the parry takes back one still winding up
        public byte Bulwark;          // a Bulwark Barrier hit (1-3 the chain, 4 the Shield Bash; BulwarkBarrier.cs): the parry takes back a chain step still winding up
        public byte Knuckle;          // an Arc Knuckles hit (its KnuckleHitCue kind; ArcKnuckles.cs): the parry takes back one still winding up
        public V2 KnuckleKnock;       // a Drive Punch: what it hits is sent this far this way (ArcKnucklesBrawl.cs)
        public int OnlyEnemy;         // != 0: hits only this enemy (the Arc Knuckles' locked-on Rocket Dash)
        public bool Nun;              // a Nunchaku hit (Nunchaku.cs): a stick landing sounds Sfx.NunHit
        public byte Tessen;           // a Twin Tessen step (1-5, 6 the Petal Spin's second turn; TwinTessen.cs): Breeze, its healing and petals
        public byte Vg;               // a Vowguard hit (Vowguard.cs): 1 a Valor link (lights Faith), 2 any other; the parry and the switch take back one still winding up
        public bool Nod;              // a Nodachi hit (Nodachi.cs): the parry and the stance switch take back one still winding up
        public int BlastPress;        // != 0: a Blood Blast cut, this press's number: what it hits gets one blood stack per press (BloodBlast.cs)
    }

    /// The Knight: a melee class for every role. Its weapons swing instead of shooting, cutting small
    /// bullets out of the air; its abilities close the distance and keep it standing.
    public partial class Game
    {
        readonly List<Swing> pendingSwings = new List<Swing>();

        const float LeapRange = 320f, SlamRadius = 130f, SlamDamage = 50f;
        public const float LeapTime = 0.45f, LeapFxTail = 0.5f;  // the leap's length, and how long its look outlasts the stun (Game/LeapSlamLook.cs)
        public static readonly Rgba LeapBoomColor = new Rgba(0.6f, 0.78f, 1f);  // the landing's Boom: the Bulwark look draws its own impact
        const float WallCapacity = 400f, WallHalfArc = 1.05f;  // 120 degrees across; the capacity is shared out between the plates
        public const float RootHalfWidth = 22f, RootHeal = 10f, RootFxTime = 0.5f;  // the Treant Rod's root lash (KitWeapons.cs)
        // The Warhammer: a slam on a fixed circle in front of you. Holding charges its damage and stun, never its area.
        public const float HammerRadius = 80f, HammerCentreBase = 16f;
        /// How far in front of you the Warhammer's circle is centred, for a weapon reach (Reach upgrades push it out and, through the radius, widen it).
        public static float HammerCentre(float reach) => HammerCentreBase + 0.65f * reach;
        /// The Warhammer's blast colour: it also marks the blast as the hammer's, so the screen's melee look (MeleeLooks.cs),
        /// which draws the whole slam itself, can leave it out.
        public static readonly Rgba HammerBoomColor = new Rgba(0.85f, 0.8f, 0.7f);
        /// The Warhammer's slam radius for this player: Reach upgrades widen it, and Quake Hammer.
        public static float HammerR(Player p) => HammerRadius * MeleeReach(p, p.Weapon.Reach) / p.Weapon.Reach * p.Stats.HammerAreaMult;

        const float HammerKnockMin = 30f, HammerKnockMax = 55f;  // the slam's shockwave push, px, by charge (it radiates from the slam's centre)
        const float HammerTap = 0.25f, HammerStunMin = 0.25f, HammerStunMax = 0.9f;
        // The Halberd's sweeps (Reaper's Wheel steps 1 and 2; Halberd.cs): a thin 18-degree cone swept across 80 degrees in six steps.
        public const int HalberdSteps = 6;
        const float HalberdSweep = 0.7f, HalberdConeDeg = 18f;
        public const float HalberdStepTime = 0.035f;
        int sweepCount;

        // --- Weapons ------------------------------------------------------------------

        /// Piercing Rounds on weapons that don't fire bullets that run out of pierce (melee, beams, cones, sprays, loops,
        /// sent companions, wards): each stack makes the weapon reach 15% farther.
        public const float PierceReachPerStack = 0.075f;  // per point of pierce: Piercing Rounds give 2 a stack (15%), Dark Matter 1
        public static float PierceReach(Player p) => 1f + PierceReachPerStack * p.Stats.Pierce;

        /// How far a melee weapon reaches (Piercing Rounds lengthen it 15% a stack).
        public static float MeleeReach(Player p, float baseReach) => baseReach * PierceReach(p);

        /// A melee weapon's base swing reach (Defs; bots and tests use it).
        public static float MeleeRange(WeaponId w) => Defs.WeaponOf(w).Reach;

        Swing NewSwing(Player p, float aim, float reach, float arcDeg, float dmg)
        {
            return new Swing { Owner = p.Id, Aim = aim, Reach = MeleeReach(p, reach), HalfArc = arcDeg * MathX.Pi / 360f, Dmg = dmg };
        }

        /// A Knight weapon's attack. `extra` is the number of extra projectiles; each weapon uses them its own way.
        void FireKnightWeapon(Player p, float aim, float dmg, int extra)
        {
            p.SwingCount++;
            var w = p.Weapon;  // its Reach, Damage and Grow (Defs)
            switch (w.Id)
            {
                case WeaponId.Warhammer:
                {
                    // Hold to charge, let go to slam a fixed circle in front of you. The charge sets the damage (a quarter
                    // for a tap, all of it at full) and how long what it hits is stunned.
                    float c = BowCharge(p, p.BowShotDraw);
                    float stun = HammerStunMin + (HammerStunMax - HammerStunMin) * c + p.Stats.HammerStunBonus, power = HammerTap + (1f - HammerTap) * c;  // (Quake Hammer: longer)
                    float radius = HammerR(p);  // Reach upgrades widen the circle too (and Quake Hammer)
                    for (int k = 0; k <= extra; k++)  // each extra projectile: an aftershock slam, further out
                    {
                        var s = NewSwing(p, aim, w.Reach + w.Grow * k, 0f, w.Damage * dmg * power * (k == 0 ? 1f : 0.6f));
                        s.Circle = radius;
                        s.CircleAt = HammerCentre(s.Reach);
                        s.Power = c;
                        s.Stun = stun;
                        s.Knock = HammerKnockMin + (HammerKnockMax - HammerKnockMin) * c;  // the shockwave: a charged slam shoves further
                        s.Heavy = true;
                        s.Thrust = true;
                        s.Delay = 0.2f * k;
                        QueueSwing(s);
                    }
                    // The blast's old ring (drawn when the screen's melee look is off; with it on, the slam draws its own impact).
                    BroadcastFx(FxKind.Boom, p.Pos + V2.FromAngle(aim) * HammerCentre(MeleeReach(p, w.Reach)), HammerBoomColor, radius * (0.5f + 0.5f * c), 0.35f, c > 0.95f ? Sfx.BigExplode : Sfx.Explode);
                    break;
                }
                case WeaponId.Lance:
                    FireLance(p, aim, dmg, extra);  // the Breach's next step (Lance.cs)
                    break;
                case WeaponId.BulwarkBarrier:
                    FireBulwark(p, aim, dmg, extra);  // the Shield Chain's next step, or the Shield Bash while braced (BulwarkBarrier.cs)
                    break;
                case WeaponId.Halberd:
                    FireHalberd(p, aim, dmg, extra);  // Reaper's Wheel's next step (Halberd.cs)
                    break;
                case WeaponId.Quarterstaff:
                {
                    // The Quarterstaff: a wide swipe that knocks enemies back and parries bullets as it goes. Every enemy it
                    // hits adds a Jab stack (Quarterstaff.cs); the screen's look alternates a left and a right swipe.
                    var s = NewSwing(p, aim, w.Reach, StaffSwipeArc, w.Damage * dmg);
                    s.Knock = StaffKnock;
                    s.Deflect = true;
                    s.Staff = true;
                    s.HealPerHit = 2f + p.Stats.StaffHealBonus;  // each enemy hit heals you and allies near you (as the Holy Mace did; Pilgrim's Staff: more)
                    s.HealRange = 220f;
                    s.HealHits = 4;
                    QueueSwing(s);
                    break;
                }
                case WeaponId.Lightbringer:
                    FireLightbringer(p, aim, dmg, extra);  // Dawn, Mercy, Noon (Lightbringer.cs)
                    break;
                case WeaponId.Censer:
                    FireCenser(p, aim, dmg, extra);  // the Thurible Rite's next step (Censer.cs)
                    break;
                case WeaponId.TwinBlades:
                    FireTwinBlades(p, aim, dmg, extra);  // the Crescent Waltz's next step (TwinBlades.cs)
                    break;
                case WeaponId.Greatsword:
                    FireGreatsword(p, aim, dmg, extra);  // the ten-step chain's next step (GreatswordChain.cs)
                    break;
                case WeaponId.Vowguard:
                    FireVowguard(p, aim, dmg, extra);  // the next step of the stance's chain (Vowguard.cs)
                    break;
                case WeaponId.ArcKnuckles:
                    FireArcKnuckles(p, aim, dmg, extra);  // the next punch, or the Counter Hammer (ArcKnuckles.cs)
                    break;
                case WeaponId.Rapier:
                {
                    // Tempo sets how soon the next thrust comes: slow at 0, quick at full (Rapier.cs).
                    p.FireCd = RapierCooldown(p);
                    var s = NewSwing(p, aim, w.Reach, 36f, w.Damage * dmg);
                    s.Rapier = true;
                    s.Thrust = true;
                    s.MaxTargets = 1 + extra;
                    QueueSwing(s);
                    break;
                }
            }

        }

        /// Barrage on a Knight weapon: a sword wave per stack, fanned out to the sides.
        void FireKnightWaves(Player p, float aim)
        {
            for (int v = 1; v <= p.Stats.Fan; v++)
            {
                float a = aim + (v % 2 == 1 ? 1f : -1f) * 0.14f * ((v + 1) / 2);
                SpawnBullet(Defs.KnightWave, p.Pos + V2.FromAngle(a) * p.Def.BodyR, V2.FromAngle(a) * 650f, 0f, 0.5f, 9f * p.Stats.DmgMult, p.Id);
            }
        }

        /// Greatsword: each sweep leaves its edge hanging in the air for SteelWallTime, a crescent that stops enemy
        /// bullets crossing it (all but red aimed shots).
        public const float SteelWallTime = 0.5f, SteelWallBand = 16f;

        class Crescent
        {
            public V2 Center;
            public float Aim, Reach, HalfArc, T, Wait, Life;
            public int Owner;
        }

        readonly List<Crescent> steelWalls = new List<Crescent>();

        /// A crescent that hangs for `life` seconds, starting `wait` seconds from now (the overhead rend's blade goes up first).
        void SteelWall(Player p, float aim, float reach, float halfArc, float life = SteelWallTime, float wait = 0f)
        {
            var c = new Crescent { Owner = p.Id, Center = p.Pos, Aim = aim, Reach = reach, HalfArc = halfArc, T = life, Wait = wait, Life = life };
            steelWalls.Add(c);
            if (wait <= 0f)
                SendSteelWall(c);
        }

        void SendSteelWall(Crescent c) =>
            BroadcastFx(FxKind.SteelWall, c.Center, c.Center + V2.FromAngle(c.Aim) * c.Reach, Defs.BulletColor(Defs.KnightSlash), c.HalfArc, c.Life, Sfx.None);

        readonly List<int> steelStopped = new List<int>();  // (scratch)

        void TickSteelWalls(float dt)
        {
            if (steelWalls.Count == 0)
                return;
            var stopped = steelStopped;
            stopped.Clear();
            for (int i = steelWalls.Count - 1; i >= 0; i--)
            {
                var c = steelWalls[i];
                if (c.Wait > 0f)
                {
                    if ((c.Wait -= dt) <= 0f)
                        SendSteelWall(c);
                    continue;
                }
                if ((c.T -= dt) <= 0f)
                {
                    steelWalls.RemoveAt(i);
                    continue;
                }
                CollectShotsInArc(c.Center, c.Aim, c.Reach, SteelWallBand, c.HalfArc, stopped);
            }
            foreach (int id in stopped)
                if (Bullets.ContainsKey(id))
                {
                    AddFx(FxKind.Spark, Bullets[id].Pos, Defs.BulletColor(Defs.KnightSlash), 8f, 0.2f);
                    KillBullet(id);
                }
        }

        /// Lightbringer: each enemy its swings hit fills the light gauge; full, the next Noon Sunburst is Radiant (Lightbringer.cs).
        public const int LightMax = 12;
        public const float LightRadiant = 3f;

        /// Queue a swing to land after its Delay; one the weapon throws (as it fires, or in a Katana step) is marked as the weapon's.
        void QueueSwing(Swing s)
        {
            s.FromWeapon = (firingWeapon || dmgSrc == "weapon") && !casting;
            pendingSwings.Add(s);
        }

        /// Called from Collide (after it clears its queues): land every swing that's due.
        void ResolveSwings()
        {
            for (int i = pendingSwings.Count - 1; i >= 0; i--)
            {
                var s = pendingSwings[i];
                s.Delay -= tickDt;
                if (s.Delay > 0f)
                    continue;
                pendingSwings.RemoveAt(i);
                Player p;
                if (Players.TryGetValue(s.Owner, out p) && !p.Downed)
                {
                    string was = CreditTo(s.FromWeapon ? "weapon" : null);
                    LandSwing(p, s);
                    dmgSrc = was;
                }
            }
        }

        static float Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

        /// How much wider a cone reaches for a body of radius r at distance d (its edge counts, not just its centre);
        /// right on top of it (d up to 1 px), any direction does.
        static float BodySlack(float d, float r) => d > 1f ? (float)Math.Asin(Math.Min(1f, r / d)) : MathX.Pi;

        /// The enemy shots (blockable ones) on an arc: within band of radius from center (plus the shot's size), and
        /// within halfArc of aim. Added to into (not cleared first), for a wall or guard to stop them.
        void CollectShotsInArc(V2 center, float aim, float radius, float band, float halfArc, List<int> into)
        {
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (Math.Abs(b.Pos.DistanceTo(center) - radius) > band + b.Radius || AngleTo(center, b.Pos, aim) > halfArc)
                    continue;
                into.Add(b.Id);
            }
        }

        static float AngleTo(V2 from, V2 to, float aim) => Math.Abs(MathX.WrapPi((to - from).Angle - aim));

        // Reused by every swing (swings land one at a time, from ResolveSwings).
        readonly List<Enemy> swingHits = new List<Enemy>();
        readonly List<Bullet> swingDeflected = new List<Bullet>();
        readonly List<int> swingCut = new List<int>();

        void LandSwing(Player p, Swing s)
        {
            V2 origin = s.FromSet ? s.From : p.Pos, dir = V2.FromAngle(s.Aim);
            bool full = s.HalfArc >= MathX.Pi - 0.01f, box = s.HalfWidth > 0f, circle = s.Circle > 0f;
            V2 circleAt = origin + dir * s.CircleAt;
            for (int i = 0; i < s.Waves; i++)  // the overhead rend's sword waves (Greatsword.cs)
            {
                float f = s.Waves == 1 ? 0f : i * 2f / (s.Waves - 1) - 1f;
                var wave = SpawnBullet(Defs.KnightWave, origin + dir * p.Def.BodyR, V2.FromAngle(s.Aim + f * 0.25f) * 700f, 0f, 0.6f, s.WaveDmg, p.Id);
                wave.Pierce += 99;
            }
            if (s.NoFx)
            {
                if (!s.Silent && !((s.Twin || s.LanceKind > 0 || s.Knuckle > 0 || s.Nod || s.Vg > 0) && s.Sound == Sfx.None))  // (a Twin Blades or Lance hit with no sound of its own: the step's is already playing)
                    BroadcastSound(s.Sound != Sfx.None ? s.Sound : Sfx.SwingHeavy);  // a Halberd sweep's or Twin Blades step's whoosh (its effect was sent at the click)
            }
            else if (s.FxHalf > 0f)  // the Halberd's whole sweep, from its first step (MeleeLooks.cs plays it over HalberdSweepTime)
                BroadcastFx(FxKind.Slash, origin, origin + V2.FromAngle(s.FxAim) * s.Reach, Defs.BulletColor(Defs.KnightSlash), s.FxHalf, 0.3f,
                    s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else if (s.ClawSide != 0f)
                BroadcastFx(FxKind.Claw, origin, origin + dir * s.Reach, BearClawColor, s.ClawSide * s.HalfArc, 0.2f,
                    s.Silent ? Sfx.None : Sfx.SwingLight);
            else if (s.Root)
                BroadcastFx(FxKind.RootLash, origin, origin + dir * s.Reach, new Rgba(0.55f, 0.36f, 0.17f), s.HalfWidth, RootFxTime, Sfx.None);
            else if (s.DrawCut)
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, new Rgba(1f, 1f, 1f), -0.1f, 0.3f, s.Sound != Sfx.None ? s.Sound : Sfx.SwingHeavy);
            else if (circle && s.Great != 0)  // a Greatsword ring (Comet Leap's landing, Eclipse's strike)
                BroadcastFx(FxKind.Ring, circleAt, s.Great == (byte)GreatMove.Eclipse ? new Rgba(1f, 0.84f, 0.51f) : Defs.BulletColor(Defs.KnightSlash), s.Circle, 0.4f, s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else if (circle)  // the Warhammer's slam: its power rides in the colour's alpha, its radius in the effect's
                BroadcastFx(FxKind.Jab, origin, circleAt, HammerBoomColor.WithAlpha(0.2f + 0.8f * s.Power), s.Circle, 0.26f, s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else if (s.Rend)  // the Greatsword's Rend: a lance of light (Game/GreatswordLook.cs); Radius is its half width, the colour's alpha is its Temper
                BroadcastFx(FxKind.GreatRend, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.KnightSlash).WithAlpha(0.2f + 0.8f * s.Power), s.HalfWidth, GreatRendFxTime, Sfx.GreatRend);
            else if (box && p.Weapon.Id == WeaponId.BoStaff)  // the Bo Staff's jab: a thrust, drawn like the Rapier's (MeleeLooks.cs)
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.KnightSlash), -0.3f, 0.24f, s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else if (box)
                BroadcastFx(FxKind.Jab, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.KnightSlash), s.HalfWidth, 0.26f,
                    s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, s.Tip > 0f || s.Links > 0 || s.BlastPress != 0 ? LashRed : Defs.BulletColor(Defs.KnightSlash), s.Thrust ? -s.HalfArc : s.HalfArc, s.Heavy ? 0.24f : 0.16f,
                    s.Silent ? Sfx.None : s.Sound != Sfx.None ? s.Sound : s.Heavy ? Sfx.SwingHeavy : Sfx.SwingLight);

            // Who's in the arc (the Rapier only takes the nearest few).
            var hits = swingHits;
            hits.Clear();
            foreach (var e in AllFoes(by: p.Id))  // (with PvP on, the other players too: Pvp.cs)
            {
                float d = e.Pos.DistanceTo(origin);
                if (circle)
                {
                    if (e.Pos.FartherThan(circleAt, s.Circle + e.Radius))
                        continue;
                    if (!BlocksShot(e, origin))
                        hits.Add(e);
                    else
                        WearShield(e, s.Dmg);
                    continue;
                }
                if (box)
                {
                    // The Lance's box: from your body out to its reach, HalfWidth either side of the aim line.
                    V2 rel = e.Pos - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    if (along < -e.Radius - p.Def.BodyR || along > s.Reach + e.Radius || across > s.HalfWidth + e.Radius || across < s.Hollow)
                        continue;
                    // A Katana dash-cut that runs past an enemy's centre cuts it from behind: a shield facing you can't stop it.
                    V2 blockFrom = s.KatanaMove == 1 && along < s.Reach ? origin + dir * s.Reach : origin;
                    if (!BlocksShot(e, blockFrom))
                        hits.Add(e);
                    else
                        WearShield(e, s.Dmg);
                    continue;
                }
                if (d - e.Radius > s.Reach)
                    continue;
                float slack = BodySlack(d, e.Radius);
                if (!full && AngleTo(origin, e.Pos, s.Aim) > s.HalfArc + slack && d > e.Radius + p.Def.BodyR)
                    continue;
                if (BlocksShot(e, origin))
                {
                    WearShield(e, s.Dmg);
                    continue;  // shields stop swords too (and wear)
                }
                hits.Add(e);
            }
            GateLockStrike(q =>  // a Warden gate's lock in reach takes the swing as a target LockW wide (Warden.cs)
            {
                float lr = LockW / 2f;
                if (circle)
                    return q.WithinReach(circleAt, s.Circle + lr);
                if (box)
                {
                    V2 rel = q - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    return along >= -lr - p.Def.BodyR && along <= s.Reach + lr && across <= s.HalfWidth + lr && across >= s.Hollow;
                }
                float d = q.DistanceTo(origin);
                if (d - lr > s.Reach)
                    return false;
                float slack = BodySlack(d, lr);
                return full || AngleTo(origin, q, s.Aim) <= s.HalfArc + slack || d <= lr + p.Def.BodyR;
            });
            if (s.Dmg > 0f)  // a room turret in reach takes the cut too (RoomTraps.cs)
                MeleeHitRoomTurrets(s.Dmg, (q, lr) =>
                {
                    if (circle)
                        return q.WithinReach(circleAt, s.Circle + lr);
                    if (box)
                    {
                        V2 rel = q - origin;
                        float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                        return along >= -lr - p.Def.BodyR && along <= s.Reach + lr && across <= s.HalfWidth + lr && across >= s.Hollow;
                    }
                    float d = q.DistanceTo(origin);
                    if (d - lr > s.Reach)
                        return false;
                    return full || AngleTo(origin, q, s.Aim) <= s.HalfArc + BodySlack(d, lr) || d <= lr + p.Def.BodyR;
                });
            if (s.Sweep > 0)
            {
                hits.RemoveAll(e => e.SweepMark == s.Sweep);  // already hit by an earlier step of this sweep
                foreach (var e in hits)
                    e.SweepMark = s.Sweep;
            }
            if (s.OnlyEnemy != 0)
                hits.RemoveAll(e => e.Id != s.OnlyEnemy);  // a locked-on hit (the Arc Knuckles' Rocket Dash)
            if (s.MaxTargets > 0 && hits.Count > s.MaxTargets)
            {
                hits.Sort((a, b) => a.Pos.DistanceSq(origin).CompareTo(b.Pos.DistanceSq(origin)));
                hits.RemoveRange(s.MaxTargets, hits.Count - s.MaxTargets);
            }

            p.Run.ShotsFired++;
            if (hits.Count > 0)
                p.Run.ShotsHit++;
            if (s.LightKind > 0)  // a Lightbringer step: the gauge, its crescents or its ring's healing (Lightbringer.cs)
                LightLanded(p, s, origin, circleAt, hits.Count);
            if (hits.Count > 0 && s.Staff)  // a Quarterstaff swipe landed: a Jab stack for every enemy hit, up to its limit
                p.StaffStacks = (byte)Math.Min(StaffCap(p), p.StaffStacks + hits.Count);
            if (hits.Count > 0 && p.Weapon.Melee && !s.Silent)
                p.MeleeGraceT = MeleeContactGrace;  // you're in the thick of it: touching bodies don't hurt for a moment
            if (hits.Count > 0 && s.TwinKind > 0)
                TwinHitCue(p, s.TwinKind, hits.Count);  // a Twin Blades step cut something: its sound, your hit-pause and shake (TwinBlades.cs)
            if (hits.Count > 0 && s.LanceKind > 0)
                LanceHitCue(p, s.LanceKind, hits.Count);  // a Lance step hit something: its sound, your hit-pause and shake (Lance.cs)
            if (s.Nod && hits.Count > 0)
                BroadcastSound(Sfx.NodHit);  // the Nodachi bit something (the step's own sound was sent at the press)
            if (s.Vg > 0 && hits.Count > 0)
            {
                VgLanded(p, s, hits.Count);  // a Vowguard link lights Faith (Vowguard.cs)
                BroadcastSound(Sfx.TwinHit);
            }
            if (s.Nun && hits.Count > 0)
                BroadcastSound(Sfx.NunHit);  // a Nunchaku stick landed (the step's own sound was sent at the press)
            if (s.Rapier && hits.Count > 0)
                RapierGainTempo(p, hits.Count);
            if (s.Knuckle == 7)
                KnuckleDashLanded(p, hits);  // the Arc Knuckles' Rocket Dash (ArcKnuckles.cs)
            else if (s.Knuckle > 0 && hits.Count > 0)
                KnuckleHitCue(p, s.Knuckle, hits.Count, hits[0].Pos);  // an Arc Knuckles punch connected: its crack, your hit-pause and shake  // a stack of Tempo for every enemy the thrust hits (Rapier.cs)
            int healed = 0;
            foreach (var e in hits)
            {
                float dmg = s.Dmg;
                bool crack = s.Tip > 0f && e.Pos.DistanceTo(origin) + e.Radius >= s.Tip;
                if (crack)
                    dmg *= LashCrack;
                dmg *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                HurtEnemy(e, dmg, p.Id);
                if (s.Knuckle > 0)
                {
                    Charge(e, p.Id);  // an Arc Knuckles punch or Rocket Dash charges what it hits (Charged.cs)
                    KnuckleHitExtra(p, s, e);  // a Drive Punch's steered shove, the Pin Punch's stop, the Barrage's held stun (ArcKnucklesBrawl.cs)
                }
                Daze(e);  // a melee hit: it can't hit back for a split second (Daze.cs)
                float knock = MeleeKnockOf(s);  // the standard melee shove unless the move has its own (MeleeHit.cs)
                ModHit(e, p.Id, dmg, Defs.KnightSlash, dir, s.Slow);
                if (s.Bleed > 0f)
                    ApplyBleed(e, dmg * s.Bleed, p.Id, true);  // KitWeapons.cs
                if (s.Stun > 0f && !e.Def.IsBoss && s.StarStun)
                    Stun(e, s.Stun);
                else if (s.Stun > 0f && !e.Def.IsBoss)
                {
                    Hold(e, s.Stun);
                }
                if (s.LanceKind > 0)  // a Lance step: drives straight ahead (the sweep bats along its swing), sparks, and arms Impact (Lance.cs)
                {
                    V2 away = (e.Pos - origin).Normalized(), kd = LanceKnockDir(s, away, dir);
                    if (knock > 0f && !Heavy(e))
                    {
                        KnockEnemy(e, kd * knock);
                        e.ImpactBy = p.Id + 1;
                        e.ImpactLast = -1;
                    }
                    BroadcastFx(FxKind.LanceSpark, e.Pos, e.Pos + kd, LanceGlow, s.LanceKind == 4 ? 1f : 0.5f, 0.4f, Sfx.None);
                }
                else if (knock > 0f && !Heavy(e))
                {
                    ShoveFrom(e, circle && p.Weapon.Id == WeaponId.Warhammer ? circleAt : origin, knock, dir);  // the Warhammer's shockwave spreads from the slam's centre  // slides (Knock.cs)
                }
                else if (knock < 0f && !Heavy(e))  // pulled in (Kusarigama), but never into you
                {
                    float d = Math.Max(e.Pos.DistanceTo(origin) + knock, e.Radius + p.Def.BodyR + 4f);
                    e.Pos = ClampToArena(origin + AwayFrom(e.Pos, origin, dir) * d, e.Radius);
                }
                if (s.HealPerHit > 0f && healed < s.HealHits)
                {
                    healed++;
                    foreach (var q in Players.Values)
                        if (q.Pos.CloserThan(origin, s.HealRange))
                            Heal(q, s.HealPerHit, p);
                }
                if (s.BlastPress != 0)
                    BlastLanded(p, e, s);  // a blood stack (BloodBlast.cs)
                if (s.Links > 0)
                    LashLanded(p, e, s, crack);  // the tip's taunt and the chains (BloodLash.cs)
                else if (s.Taunt > 0f)
                    TauntEnemy(p, e, s.Taunt);
            }
            if (s.Tessen > 0)
                TessenLanded(p, s, origin, hits.Count);  // Breeze, the healing, Tailwind and petals (TwinTessen.cs)
            if (s.KatanaMove > 0)
                KatanaLanded(p, s, hits);  // each cut's look, a killing dash bringing the next step at once, the hit cue (Katana.cs)
            if (s.CenserStep > 0)
                CenserLanded(p, s, hits.Count);  // Embers, the Benediction's bonus heal and the step's incense (Censer.cs)
            if (s.HealSpin > 0f)
                foreach (var q in Players.Values)
                    if (q.Pos.CloserThan(origin, s.HealRange))
                        Heal(q, s.HealSpin, p);
            if (s.RootHeal > 0f)  // the root lash mends the allies it touches (not you)
                foreach (var q in Players.Values)
                {
                    if (q.Id == p.Id || q.Downed)
                        continue;
                    V2 rel = q.Pos - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    if (along > -q.Def.BodyR && along < s.Reach + q.Def.BodyR && across < s.HalfWidth + q.Def.BodyR)
                        Heal(q, s.RootHeal, p);
                }

            if (s.Deflect)
                DeflectBullets(p, origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, s.HalfArc);
            else if (s.Rapier)
            {
                // The Rapier skewers enemy bullets along its line (big ones too, never red aimed shots). They add no Tempo.
                int skewered = CutBullets(origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, s.HalfArc, true, RapierSkewerWidth, RapierSkewerMax + p.Stats.SkewerBonus);  // Duelist's Edge: one more
                if (skewered > 0)
                    BroadcastFx(FxKind.Spark, origin + dir * (s.Reach * 0.6f), new Rgba(1f, 1f, 1f), 14f, 0.2f, Sfx.None);
            }
            else if (!s.NoCut && circle)
                CutBulletsInCircle(circleAt, s.Circle * p.Stats.CutReachMult, s.CutAll);  // the slam clears small bullets over its whole circle (Plate Armour: wider)
            else if (!s.NoCut)
                CutBullets(origin, s.Aim, (s.Reach + p.Def.BodyR * 0.5f) * p.Stats.CutReachMult, full ? MathX.Pi : s.HalfArc, s.CutAll, s.HalfWidth);  // Plate Armour: farther out
        }

        const float StaffKnock = 70f, DeflectSpeed = 520f, DeflectDamage = 12f;

        /// The Quarterstaff parries as it swings: enemy bullets in the arc (big orbs too, but never red aimed shots) are
        /// batted back out the way the staff was swinging, as the swinger's own holy shots.
        int DeflectBullets(Player p, V2 origin, float aim, float reach, float halfArc)
        {
            var hit = swingDeflected;
            hit.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (b.Pos.FartherThan(origin, reach + b.Radius) || AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                hit.Add(b);
            }
            foreach (var b in hit)
            {
                KillBullet(b.Id);
                V2 away = (b.Pos - origin).Normalized();
                if (away.IsZero)
                    away = V2.FromAngle(aim);
                SpawnBullet(Defs.HolyShot, b.Pos, away * DeflectSpeed, 0f, 1f, DeflectDamage * p.Stats.DmgMult, p.Id);
            }
            if (hit.Count > 0)
                BroadcastFx(FxKind.Spark, origin + V2.FromAngle(aim) * reach * 0.6f, new Rgba(1f, 0.92f, 0.6f), 26f, 0.25f, Sfx.Clang);
            return hit.Count;
        }

        /// Swings slice small enemy bullets out of the air; a swing with CutAll would stop big orbs too.
        /// Bosses' red aimed shots always get through.
        public const float RapierSkewerWidth = 14f;
        public const int RapierSkewerMax = 3;

        void CutBulletsInCircle(V2 at, float radius, bool all)
        {
            var cut = swingCut;
            cut.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind) || !all && b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                    continue;
                if (b.Pos.WithinReach(at, radius + b.Radius))
                    cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                KillBullet(id);
            }
        }

        int CutBullets(V2 origin, float aim, float reach, float halfArc, bool all, float halfWidth = 0f, int max = int.MaxValue)
        {
            var cut = swingCut;
            cut.Clear();
            foreach (var b in Bullets.Values)
            {
                if (cut.Count >= max)
                    break;
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (!all && b.Kind != Defs.SmallShot && b.Kind != Defs.FastShot)
                    continue;
                if (halfWidth > 0f)
                {
                    V2 dir = V2.FromAngle(aim), rel = b.Pos - origin;
                    float along = Dot(rel, dir);
                    if (along < -b.Radius || along > reach + b.Radius || Math.Abs(Dot(rel, dir.Orthogonal())) > halfWidth + b.Radius)
                        continue;
                    cut.Add(b.Id);
                    continue;
                }
                if (b.Pos.FartherThan(origin, reach + b.Radius))
                    continue;
                if (halfArc < MathX.Pi && AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                KillBullet(id);
            }
            return cut.Count;
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Knight: swings in FireKnightWeapon, abilities in KnightAbility. See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef KnightKit = new KitDef
        {
            Id = Kit.Knight, Name = "Knight", Icon = "class_knight", HpMult = 1.15f,
            Blurb = "Melee. Holding fire swings instead of shooting, hitting every enemy in the arc and cutting small bullets out of the air in front of you (big orbs, mines and red aimed shots still get through). Armoured for the thick of it: 115% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKnightWeapon(p, aim, dmg, extra),
            Ability = (g, p, id, target) => g.KnightAbility(p, id, target),
            OnKill = (g, e) => g.GauntletKill(e),  // a Challenged enemy's death sends the gauntlet back (Gauntlet.cs)
            OnShotHit = (g, b, e, hit) => g.SteelShardHit(b, e),  // the Bulwark Barrier's steel shards striking (BulwarkBarrier.cs)
        };

        /// The Knight's weapons (swung in FireKnightWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] KnightWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Warhammer, Charge = true, Range = (g, p, w, x) => HammerCentre(MeleeReach(p, w.Reach + w.Grow * x)), Kit = Kit.Knight, Role = Role.Tank, Choice = 0, Damage = 80f, Reach = 130f, Grow = 40f, Melee = true, ExtraDesc = "+1 aftershock: another slam 0.2 s later, 40 px further out, at 60% damage", Name = "Warhammer", FireInterval = 0.75f,
                Desc = "Primary Fire: Hold to charge, let go to slam the ground: longer charges hit harder and stun longer." },
            new WeaponDef { Id = WeaponId.Lance, Range = (g, p, w, x) => MeleeReach(p, p.LancePrimed || TwinBladesMotion.StepOf(p.Combo) == 3 ? w.Reach + BreachExtraReach + w.Grow * x : w.Reach), BotAlt = LanceBotAlt, Kit = Kit.Knight, Role = Role.Tank, Choice = 1, Damage = 14f, Reach = 178f, Grow = 30f, Melee = true, ExtraDesc = "The Breaching Thrust: +30 px reach, 24 px wider and +60 px knockback", Name = "Lance", FireInterval = 0.24f,
                Desc = "Primary Fire: A four-step lance combo that ends in a thrust that shoves and stuns.\n\nAlternative Fire: Hook the nearest enemy and haul it in." },
            new WeaponDef { Id = WeaponId.Halberd, BotAlt = HalberdBotAlt, BotAltHold = true, Kit = Kit.Knight, Role = Role.Tank, Choice = 2, Damage = 30f, Reach = 200f, Grow = 30f, Melee = true, ExtraDesc = "+30 px reach and +10% damage on every step and poke", Name = "Halberd", FireInterval = 0.6f,
                Desc = "Primary Fire: A long, thin sweep that hits each enemy once." },
            new WeaponDef { Id = WeaponId.BulwarkBarrier, BotAlt = BulwarkBarrierBotAlt, BotAltHold = true, Kit = Kit.Knight, Role = Role.Tank, Choice = 3, Damage = 12f, Reach = 80f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach and +10% damage on the chain, and +20% Shield Bash damage", Name = "Bulwark Barrier", FireInterval = 0.3f,
                Desc = "Primary Fire: A shield that stops every shot at your front, plus a three-hit combo that ends in a stun.\n\nAlternative Fire: Hold to brace, then bash with a dash." },
            // Healer
            new WeaponDef { Id = WeaponId.Quarterstaff, BotAlt = QuarterstaffBotAlt, BotAltHold = true, Kit = Kit.Knight, Role = Role.Healer, Choice = 0, Damage = 18f, Reach = 120f, Melee = true, ExtraDesc = "+1 Jab stack limit (3 to start)", Name = "Quarterstaff", FireInterval = 0.5f,
                Desc = "Primary Fire: Staff swipes that knock enemies and bullets back, and heal you and allies.\n\nAlternative Fire: Spend a Jab stack on a cone jab." },
            new WeaponDef { Id = WeaponId.Lightbringer, Range = (g, p, w, x) => Math.Max(MeleeReach(p, w.Reach), p.Def.BodyR + 600f * 0.45f * p.Stats.ShotSpeedMult), BotAlt = LightbringerBotAlt, Kit = Kit.Knight, Role = Role.Healer, Choice = 1, Damage = 17f, Reach = 80f, Melee = true, ExtraDesc = "+1 crescent of light on Dawn and Mercy, fanned out, and +30 px on the Sunburst", Name = "Lightbringer", FireInterval = 0.5f,
                Desc = "Primary Fire: A three-step sword combo that heals allies it passes and ends in a healing ring.\n\nAlternative Fire: Fire a Sunlance that heals the ally nearest your cursor." },
            new WeaponDef { Id = WeaponId.Censer, BotAlt = CenserBotAlt, Kit = Kit.Knight, Role = Role.Healer, Choice = 2, Damage = 13f, Reach = 85f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach, +1 HP on every heal and +1 Ember", Name = "Censer", FireInterval = 0.26f,
                Desc = "Primary Fire: A three-step censer swing that heals allies and leaves incense that makes enemy shots stray.\n\nAlternative Fire: Throw the censer to your cursor for a burst and a big healing cloud." },
            new WeaponDef { Id = WeaponId.Vowguard, Kit = Kit.Knight, Role = Role.Healer, Choice = 3, Damage = 16f, Reach = 115f, Melee = true, BotRange = 100f, ExtraDesc = "+15% damage and healing on every step", Name = "Vowguard", FireInterval = 0.3f,
                Desc = "Primary Fire: A sword and shield with two stances and chains you build in the lobby.\n\nAlternative Fire: Switch stance." },
            // DPS
            new WeaponDef { Id = WeaponId.TwinBlades, BotAlt = TwinFangBotAlt, Kit = Kit.Knight, Role = Role.Dps, Choice = 0, Damage = 16f, Reach = 90f, Melee = true, ExtraDesc = "+1 crescent: the Pirouette and the Crescent Shear each launch one more wave, fanned out", Name = "Twin Blades", FireInterval = 0.18f,
                Desc = "Primary Fire: A five-step sabre combo that carries you forward and ends in a crescent wave.\n\nAlternative Fire: Hurl both sabres to cross at your cursor and fly back." },
            new WeaponDef { Id = WeaponId.Greatsword, Range = (g, p, w, x) => GreatNextReach(p), BotAlt = TwinFangBotAlt, Kit = Kit.Knight, Role = Role.Dps, Choice = 1, Damage = 40f, Reach = 125f, Melee = true, ExtraDesc = "+1 sword wave: Eclipse launches a piercing crescent", Name = "Greatsword", FireInterval = 0.5f,
                Desc = "Primary Fire: A ten-step sword combo that drags in, stuns, leaps and ends in a golden lance.\n\nAlternative Fire: Throw the sword spinning to your cursor and back." },
            new WeaponDef { Id = WeaponId.Rapier, BotAlt = RapierBotAlt, Kit = Kit.Knight, Role = Role.Dps, Choice = 2, Damage = 18f, Reach = 130f, Melee = true, ExtraDesc = "Skewers 1 more enemy behind the first", Name = "Rapier", FireInterval = 0.48f,  // the first thrust; Tempo makes them quicker (Rapier.cs)
                Desc = "Primary Fire: Fast thrusts at the nearest enemy that skewer bullets and build Tempo.\n\nAlternative Fire: Spend Tempo on a Flourish: a whipping blade that throws crescent waves." },
            new WeaponDef { Id = WeaponId.ArcKnuckles, BotAlt = ArcKnucklesBotAlt, Kit = Kit.Knight, Role = Role.Dps, Choice = 3, Damage = 22f, Reach = 80f, Melee = true, ExtraDesc = "+1 aftershock: the Quake Punch and the Counter Hammer slam again 0.2 s later, 40 px wider (60% damage)", Name = "Arc Knuckles", FireInterval = 0.24f,
                Desc = "Primary Fire: Brawl stance: a Pull Punch opens a click wheel; click its sides to send punches and dashes that way, then pin, barrage and quake.\n\nAlternative Fire: Swap to the Rocket stance: Rocket Dash, Flurry, Grab & Throw, Rocket Punch, one click each." },
        };

        /// The Knight's own upgrade cards: Rare upgrades of its 1st abilities (RequiresAbility), and cards for its weapons
        /// (Weapons) or the whole class (Kit). Upgrades.All gathers every class's list (Docs/UpgradePools.md).
        internal static readonly UpgradeDef[] KnightCards =
        {
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.ShieldWall, MaxStacks = 2, Name = "Tower Shield", Desc = "Shield Wall covers 160° and its plates take 50% more before they break",
                Apply = s => { s.WallArcMult += 0.33f; s.WallCapMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LeapSlam, MaxStacks = 2, Name = "Earthshaker", Desc = "Leap Slam hits 40% wider and stuns for 1 s longer",
                Apply = s => { s.SlamRadiusMult += 0.4f; s.SlamStunBonus += 1f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.CavalryCall, MaxStacks = 2, Name = "Iron Will", Desc = "Cavalry Call blocks 10% more damage while the lancers ride and every lance hits for 10 more",
                Apply = s => { s.UnbreakBonus += 0.1f; s.CavalryDmgBonus += 10f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SiegeBell, MaxStacks = 2, Name = "Great Bell", Desc = "Siege Bell's shockwave reaches 25% farther and stuns for 0.5 s longer",
                Apply = s => { s.BellRadiusMult += 0.25f; s.BellStunBonus += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Consecrate, MaxStacks = 2, Name = "Sacred Ground", Desc = "Consecrate flies 40% farther and its Swiftness lasts 2 s longer",
                Apply = s => { s.HolyRangeMult += 0.4f; s.HolySwiftBonus += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.GuardiansOath, MaxStacks = 2, Name = "Bulwark", Desc = "Guardian's Oath ward cuts 15% more damage", Apply = s => s.OathCut += 0.15f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.LayOnHands, MaxStacks = 2, Name = "Miracle", Desc = "Lay on Hands heals 60% more and reaches 30% further", Apply = s => s.LayMult += 0.6f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.SanctumGlass, MaxStacks = 2, Name = "Rose Window", Desc = "Sanctum Glass can heal 40% more before it shatters",
                Apply = s => s.GlassCapMult += 0.4f },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.BladedDive, MaxStacks = 2, Name = "Coup de Grace", Desc = "Bladed Dive's cuts deal 25% more damage and it rushes 40 px further",
                Apply = s => { s.BladedDamage += 0.25f; s.BladedReach += 40f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Gauntlet, MaxStacks = 2, Name = "Iron Grip", Desc = "The Gauntlet flies 40% further and its charge hits 50% harder",
                Apply = s => { s.GauntletRange += 0.4f; s.GauntletDamage += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.CaltropRun, MaxStacks = 2, Name = "Barbed Iron", Desc = "Caltrop Run drops 4 more caltrops, and each hits 4 harder",
                Apply = s => { s.CaltropMore += 4; s.CaltropDamage += 4f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.CannonCall, MaxStacks = 2, Name = "Siege Powder", Desc = "Cannon Call hits 25% harder and its blast is 15% wider",
                Apply = s => { s.CannonDamage += 0.25f; s.CannonWide += 0.15f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.WarhornVolley, MaxStacks = 2, Name = "Bodkin Points", Desc = "Warhorn Volley's arrows hit 25% harder and the volley is 20% wider",
                Apply = s => { s.VolleyDamage += 0.25f; s.VolleyWide += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, RequiresAbility = AbilityId.Ballista, MaxStacks = 2, Name = "Windlass", Desc = "The Ballista holds 2 more bolts and reloads 25% faster",
                Apply = s => { s.BaWindlass++; } },
            new UpgradeDef { Rarity = Rarity.Rare, Kit = Arena.Kit.Knight, MaxStacks = 2, Name = "Plate Armour", Desc = "Take 8% less damage, and your swings cut bullets out of the air 20% farther out",
                Apply = s => { s.DamageTaken *= 0.92f; s.CutReachMult += 0.2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Warhammer }, MaxStacks = 2, Name = "Quake Hammer", Desc = "The Warhammer's slam is 20% wider and stuns 0.3 s longer",
                Apply = s => { s.HammerAreaMult += 0.2f; s.HammerStunBonus += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Lance }, MaxStacks = 2, Name = "Breacher", Desc = "Breaching Thrust shoves 30% farther, and Impact damage is 50% higher",
                Apply = s => { s.BreachKnockMult += 0.3f; s.ImpactMult += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Halberd }, MaxStacks = 2, Name = "Long Haft", Desc = "The Halberd's blade reaches 15% farther and sweeps 20° wider",
                Apply = s => { s.HalberdReachMult += 0.15f; s.HalberdSweepBonus += 10f * MathX.Pi / 180f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.BulwarkBarrier }, MaxStacks = 2, Name = "Iron Boss", Desc = "Bulwark Barrier: Ram fills 30% faster, and the Shield Bash and its shards hit 30% harder",
                Apply = s => { s.BbRamMult += 0.3f; s.BbBashMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Quarterstaff }, MaxStacks = 2, Name = "Pilgrim's Staff", Desc = "Each Quarterstaff hit heals 1 more, and you can store 1 more Jab stack",
                Apply = s => { s.StaffHealBonus += 1f; s.StaffExtraStacks += 1; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Lightbringer }, MaxStacks = 2, Name = "Daybreak", Desc = "The light gauge fills 30% faster and the Sunlance heals 30% more",
                Apply = s => { s.LightGainMult += 0.3f; s.SunlanceHealMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Censer }, MaxStacks = 2, Name = "Thick Incense", Desc = "The Censer's incense clouds are 30% bigger and last 2 s longer",
                Apply = s => { s.IncenseSizeMult += 0.3f; s.IncenseBonusTime += 2f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.TwinBlades }, MaxStacks = 2, Name = "Whirling Sabres", Desc = "The thrown sabres follow your cursor 25% longer and hit 30% harder",
                Apply = s => { s.FangTimeMult += 0.25f; s.FangDmgMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Greatsword }, MaxStacks = 2, Name = "Lunar Arc", Desc = "The thrown sword flies 25% farther and hits 30% harder",
                Apply = s => { s.WheelRangeMult += 0.25f; s.WheelDmgMult += 0.3f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.Rapier }, MaxStacks = 2, Name = "Duelist's Edge", Desc = "Each thrust skewers 1 more bullet, and Tempo holds 0.5 s longer",
                Apply = s => { s.SkewerBonus += 1; s.TempoHoldBonus += 0.5f; } },
            new UpgradeDef { Rarity = Rarity.Rare, Weapons = new[] { WeaponId.ArcKnuckles }, MaxStacks = 2, Name = "Live Wire", Desc = "The Rocket Dash and Pull Punch reach 25% farther, and the Pin Punch and Counter Hammer stun 0.3 s longer",
                Apply = s => { s.KnuckleDashMult += 0.25f; s.KnuckleStunBonus += 0.3f; } },
        };

        /// The Knight's abilities and ultimates. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] KnightAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ShieldWall, Kit = Kit.Knight, Role = Role.Tank, Choice = 0,
                Name = "Shield Wall", Icon = "towershield", Cooldown = 12f, Desc = "Raise a wall of steel plates that blocks bullets at your front.",
                Capstone = "Bullets that hit the shield are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LeapSlam, Kit = Kit.Knight, Role = Role.Tank, Choice = 1,
                Name = "Leap Slam", Icon = "earthshaker", Cooldown = 9f, Desc = "Leap to your cursor untouchable, then slam and stun.",
                Capstone = "The landing also erases enemy bullets within 160 px.",
                Bot = b => b.HaveTarget && b.TargetD > 140f && b.TargetD < 340f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.CavalryCall, Kit = Kit.Knight, Role = Role.Tank, Choice = 2,
                Name = "Cavalry Call", Icon = "ironwill", Cooldown = 18f, Desc = "Two lancers charge across the map, shoving and slowing enemies.",
                Capstone = "Two more lancers ride outside the first pair, and the guard lasts 5 s.",
                Bot = b => b.HaveTarget && b.TargetD < 700f,
            },
            new AbilityDef
            {
                Id = AbilityId.SiegeBell, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (SiegeBell.cs)
                Name = "Siege Bell", Icon = "siegebell", Cooldown = BellCooldown, Desc = "Ring a ghostly bell three times; the last toll's shockwave stuns enemies around it.",
                Capstone = "The stun lasts 3.5 s and the shockwave erases enemy bullets.",
                Bot = b => b.NearEnemies >= 4 && b.TargetD < 220f,
            },
            new AbilityDef
            {
                Id = AbilityId.Consecrate, Kit = Kit.Knight, Role = Role.Healer, Choice = 0,
                Name = "Consecrate", Icon = "sacredground", Cooldown = 14f, Desc = "Throw a Holy Cross that heals and speeds allies and hurts enemies.",
                Capstone = "Two more crosses fly out at 22 degrees either side (each target is hit once).",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.GuardiansOath, Kit = Kit.Knight, Role = Role.Healer, Choice = 1,
                Name = "Guardian's Oath", Icon = "bulwark", Cooldown = 15f, Desc = "Haul an ally to safety and protect everyone in a ward of light.",
                Capstone = "Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.LayOnHands, Kit = Kit.Knight, Role = Role.Healer, Choice = 2,
                Name = "Lay on Hands", Icon = "miracle", Cooldown = 18f, Desc = "Heal the most hurt ally, or revive a downed one.",
                Capstone = "Also heals everyone else near them for half as much.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.SanctumGlass, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (SanctumGlass.cs)
                Name = "Sanctum Glass", Icon = "sanctumglass", Cooldown = 16f, Desc = "Raise a pane of stained glass ahead that turns enemy shots passing through into healing.",
                Capstone = "The pane is 40% wider and its motes heal 50% more.",
                Bot = b => b.Danger || b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.BladedDive, Kit = Kit.Knight, Role = Role.Dps, Choice = 0, Instant = true,  // Bladed Dive (BladedDive.cs) so the numbers stay put
                Name = "Bladed Dive", Icon = "bladeddive", Cooldown = 10f, Desc = "Rush forward untouchable with a spinning blade; a kill resets the cooldown.",
                Capstone = "Each kill heals you 5% of your health.",
                Bot = b => b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.Gauntlet, Kit = Kit.Knight, Role = Role.Dps, Choice = 1, Instant = true,  // The Gauntlet (Gauntlet.cs); the id is Lunge's
                Name = "The Gauntlet", Icon = "gauntlet", Cooldown = 9f, Desc = "Challenge an enemy and charge it; kill it in time to reset the cooldown.",
                Capstone = "The Challenge lasts 8 s, and the charge's impact stuns for 1 s.",
                Bot = b => b.HaveTarget && b.TargetD > 90f && b.TargetD < 380f,
            },
            new AbilityDef
            {
                Id = AbilityId.CaltropRun, Kit = Kit.Knight, Role = Role.Dps, Choice = 2,  // Caltrop Run (CaltropRun.cs; took Caught Steel's place and number)
                Name = "Caltrop Run", Icon = "caltroprun", Cooldown = CaltropCooldown, Desc = "Sprint and scatter caltrops behind you that cut, bleed and slow enemies.",
                Capstone = "Caltrops last 9 s and each catches 3 enemies.",
                Bot = b => b.HaveTarget && b.TargetD < 300f,
            },
            new AbilityDef
            {
                Id = AbilityId.CannonCall, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Fourth,  // the lobby's 4th choice (CannonCall.cs)
                Name = "Cannon Call", Icon = "cannonball", Cooldown = 12f, Desc = "Flare a spot and a cannonball slams into it, blasting and stunning the pack; 3 kills give back half the cooldown.",
                Capstone = "Two smaller balls land either side of the mark along your aim.",
                Bot = b => b.HaveTarget && b.TargetD > 120f && b.TargetD < 420f,
            },
            new AbilityDef
            {
                Id = AbilityId.LastStand, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Last Stand", Cooldown = 70f, Desc = "Plant yourself behind a wall of steel plates that blocks every bullet in front.",
                Capstone = "You take 5% of the damage instead of 10%.",
                Bot = b => b.Danger || b.AllyDown && b.DownedD < 70f,  // planted over a downed ally, the reviver stands in the lee
            },
            new AbilityDef
            {
                Id = AbilityId.HallowedBlade, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Hallowed Blade", Cooldown = 80f, Desc = "A giant holy sword lands, healing allies and burning enemies for a while.",
                Capstone = "Three swords fall: one at your cursor and one either side.",
            },
            new AbilityDef
            {
                Id = AbilityId.Drawbridge, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 2,  // the Drawbridge (Drawbridge.cs): key 2 of the Tank Knight's Ultimate row
                Name = "Drawbridge", Cooldown = 75f, Desc = "A drawbridge crushes the lane ahead, then blocks enemies and shots.",
                Capstone = "The bridge stands for 8 s.",
                Bot = b => b.HaveTarget && b.TargetD < 450f,
            },
            new AbilityDef
            {
                Id = AbilityId.WarhornVolley, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult,  // the DPS Knight's default ultimate (WarhornVolley.cs)
                Name = "Warhorn Volley", Icon = "firearrows", Cooldown = 70f,
                Desc = "A volley of fire arrows sweeps the field, burning and pinning enemies.",
                Capstone = "The volley is 40% wider and the fire burns for 8 s.",
                Bot = b => b.HaveTarget && b.TargetD < 900f,
            },
        };

        /// Knight abilities and ultimates. Returns false if `id` isn't one.
        bool KnightAbility(Player p, AbilityId id, V2 target)
        {
            var s = p.Stats;
            bool cap = castMastered;
            float pw = castPower;
            switch (id)
            {
                case AbilityId.ShieldWall:
                    p.WallPlateMax = WallCapacity * s.WallCapMult * pw / Player.WallPlates;
                    for (int k = 0; k < Player.WallPlates; k++)
                    {
                        p.WallPlateHp[k] = p.WallPlateMax;
                        p.WallPlate[k] = 255;
                    }
                    p.WallReflect = cap;
                    BroadcastFx(FxKind.Ring, p.Pos, new Rgba(0.75f, 0.85f, 1f), 50f, 0.35f, Sfx.Clang);
                    return true;
                case AbilityId.LeapSlam:
                    // The player's own game moves them (LocalControl); the slam lands where they come down.
                    p.InvulnT = Math.Max(p.InvulnT, LeapTime + 0.1f);
                    slams.Add(new Slam { Owner = p.Id, Pos = target, T = LeapTime, Power = pw, Mastered = cap,
                        Radius = SlamRadius * s.SlamRadiusMult, Stun = 1f + s.SlamStunBonus });
                    // The Bulwark look (Game/LeapSlamLook.cs) covers the whole leap, landing and stun from this one effect.
                    BroadcastFx(FxKind.LeapSlam, p.Pos, target, LeapBoomColor.WithAlpha(cap ? 1f : 0.5f), p.Id * 1000f + SlamRadius * s.SlamRadiusMult, LeapTime + LeapFxTail + 1f + s.SlamStunBonus, Sfx.None);
                    return true;
                case AbilityId.CavalryCall:
                    CastCavalryCall(p, target, cap, pw);  // CavalryCall.cs
                    return true;
                case AbilityId.SiegeBell:  // Siege Bell (SiegeBell.cs)
                    CastSiegeBell(p, pw, cap);
                    return true;
                case AbilityId.Consecrate:
                {
                    CastHolyCross(p, target, cap, pw);  // HolyCross.cs
                    return true;
                }
                case AbilityId.GuardiansOath:
                    return OathCast(p, target, cap);  // GuardiansOath.cs
                case AbilityId.LayOnHands:
                {
                    float range = 300f * (1f + 0.5f * (s.LayMult - 1f));
                    Player best = null;
                    foreach (var q in Players.Values)
                    {
                        if (q.Pos.FartherThan(p.Pos, range))
                            continue;
                        if (best == null || q.Downed && !best.Downed || q.Downed == best.Downed && q.Hp / q.MaxHp < best.Hp / best.MaxHp)
                            best = q;
                    }
                    if (best != null)
                    {
                        if (best.Downed)
                            RevivePlayer(best, 0.5f, p);
                        else
                            Heal(best, 70f * s.LayMult, p);
                        castHits++;
                        if (best != p)
                            BroadcastFx(FxKind.Link, p.Pos, best.Pos, new Rgba(1f, 0.95f, 0.6f), 0f, 0.5f, Sfx.None);
                        BroadcastFx(FxKind.Ring, best.Pos, new Rgba(1f, 0.95f, 0.6f), 70f, 0.5f, Sfx.SpellHoly);
                        if (cap)
                            foreach (var q in Players.Values)
                                if (q != best && !q.Downed && q.Pos.CloserThan(best.Pos, 200f))
                                    Heal(q, 35f * s.LayMult, p);
                    }
                    return true;
                }
                case AbilityId.SanctumGlass:
                    CastSanctumGlass(p, cap);  // SanctumGlass.cs
                    return true;
                case AbilityId.BladedDive:
                    CastBladedDive(p, target, pw, cap);  // BladedDive.cs
                    return true;
                case AbilityId.Gauntlet:
                    CastGauntlet(p, target, pw, cap);  // Gauntlet.cs
                    return true;
                case AbilityId.CaltropRun:
                    CastCaltropRun(p, pw, cap);  // CaltropRun.cs
                    return true;
                case AbilityId.CannonCall:
                    CastCannonCall(p, target, cap, pw);  // CannonCall.cs (the 4th choice)
                    return true;

                // Ultimates
                case AbilityId.LastStand:
                    PlantStand(p, cap);  // LastStand.cs
                    return true;
                case AbilityId.HallowedBlade:
                {
                    var spots = new List<V2> { target };
                    if (cap)
                    {
                        V2 side = (target - p.Pos).Normalized().Orthogonal();
                        if (side.IsZero)
                            side = new V2(1f, 0f);
                        spots.Add(ClampToArena(target + side * 220f, 10f));
                        spots.Add(ClampToArena(target - side * 220f, 10f));
                    }
                    foreach (var at in spots)
                        AddZone(ZoneKind.HolySword, p, at, 200f, 0.6f).Consecrates = true;
                    Sounds.Add(Sfx.SpellHoly);
                    return true;
                }
                case AbilityId.Drawbridge:
                    CastDrawbridge(p, target, cap);  // Drawbridge.cs (the Tank Knight's key 2)
                    return true;
                case AbilityId.WarhornVolley:
                    CastWarhornVolley(p, target, cap, pw);  // WarhornVolley.cs
                    return true;
                case AbilityId.Ballista:
                    CastBallista(p, target, cap);  // Ballista.cs (key 1)
                    return true;
            }
            return false;
        }

        // --- Timed effects ---------------------------------------------------------------

        class Slam
        {
            public int Owner;
            public V2 Pos;
            public float T, Power, Radius, Stun;
            public bool Mastered;
        }

        readonly List<Slam> slams = new List<Slam>();

        /// Host, every tick: Knight buffs run down, the DPS Knight's abilities run, Leap Slams land.
        void TickKnights(float dt)
        {
            TickOath(dt);  // GuardiansOath.cs
            TickCaltropRuns(dt);  // CaltropRun.cs: the run dropping caltrops
            TickGauntlets(dt);  // Gauntlet.cs
            TickBladed(dt);      // BladedDive.cs
            TickBells(dt);       // SiegeBell.cs: the tolls and the shockwave
            foreach (var p in Players.Values)
            {
                p.UnbreakT = Math.Max(0f, p.UnbreakT - dt);
                p.BossContactT = Math.Max(0f, p.BossContactT - dt);
                p.MeleeGraceT = Math.Max(0f, p.MeleeGraceT - dt);
                TickStand(p, dt);  // LastStand.cs
                if (p.Downed)
                {
                    Array.Clear(p.WallPlate, 0, Player.WallPlates);
                    p.UnbreakT = 0f;
                }
            }
            for (int i = slams.Count - 1; i >= 0; i--)
            {
                var sl = slams[i];
                sl.T -= dt;
                if (sl.T > 0f)
                    continue;
                slams.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(sl.Owner, out p) || p.Downed)
                    continue;  // (downed mid-leap: no landing)
                foreach (var e in FoesInCircle(sl.Pos, sl.Radius, by: p.Id))
                {
                    HurtEnemy(e, SlamDamage * p.Stats.DmgMult * sl.Power, p.Id);
                    if (!e.Def.IsBoss)
                    {
                        Hold(e, sl.Stun);
                    }
                }
                if (sl.Mastered)
                    EraseEnemyBullets(sl.Pos, 160f);
                BroadcastFx(FxKind.Boom, sl.Pos, LeapBoomColor, sl.Radius, 0.4f, Sfx.Explode);
            }
        }

        /// Does a raised Shield Wall cover this point (in front of the knight)? `plate` is the one it would hit; a
        /// broken plate leaves a gap, so only a plate that is still standing covers.
        bool WallCovers(Player p, V2 pos, float radius, out int plate)
        {
            plate = -1;
            V2 d = pos - p.Pos;
            float reach = p.Def.BodyR + 34f + radius;
            if (d.LengthSq > reach * reach)
                return false;
            float half = WallHalfArc * p.Stats.WallArcMult;
            float off = MathX.WrapPi(d.Angle - p.Aim);  // -pi..pi, signed
            if (Math.Abs(off) >= half)
                return false;
            plate = Math.Min(Player.WallPlates - 1, (int)((off + half) / (2f * half) * Player.WallPlates));
            return p.WallPlate[plate] > 0;
        }

        /// A blocked shot wears its plate down; at zero the plate breaks and the wall has a hole there.
        void WearWall(Player p, int plate, float wear, V2 at)
        {
            p.WallPlateHp[plate] -= wear;
            if (p.WallPlateHp[plate] <= 0f)
            {
                p.WallPlate[plate] = 0;
                BroadcastFx(FxKind.Boom, at, new Rgba(0.75f, 0.85f, 1f), 26f, 0.3f, Sfx.Clang);
            }
            else
                p.WallPlate[plate] = (byte)Math.Max(1, (int)Math.Ceiling(255f * p.WallPlateHp[plate] / p.WallPlateMax));
        }

        // --- Network: the player's Knight fields (SyncPlayer) ---

        void SyncKnight(NetSync s, Player p)
        {
            for (int k = 0; k < Player.WallPlates; k++)
                s.Byte(ref p.WallPlate[k]);
            s.Float(ref p.UnbreakT);
        }
    }
}

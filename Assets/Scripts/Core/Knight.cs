using System;
using System.Collections.Generic;

namespace Arena
{
    /// One melee strike, waiting to land (host only). Swings are resolved inside Collide so a hit goes
    /// through the same on-hit pipeline as a bullet: weapon mods, legendaries, crits, lifesteal, kills.
    public class Swing
    {
        public int Owner;
        public float Aim, Reach, HalfArc, Dmg, Knock, Delay;
        public int MaxTargets;        // 0 = everything in the arc; otherwise the nearest few (Rapier)
        public bool CutAll;           // also cuts big orbs and mines (it was the mastered Whirlwind's; nothing sets it today)
        public float HealPerHit, HealRange;
        public int HealHits;          // most enemies a swing's healing counts (Holy Mace)
        public float HealSpin;        // heals allies near you once per swing (Censer)
        public bool Rapier, Heavy, Silent;
        public bool Rend;             // the Greatsword's Rend (Greatsword.cs): a long box drawn as a lance of light, Power = Temper / GreatTemperMax
        public int Waves;             // sword waves launched as it lands (the Greatsword's overhead rend with extra projectiles)
        public float WaveDmg;
        public bool Staff;            // each enemy it hits adds a Jab stack (the Quarterstaff's swipes; Quarterstaff.cs)
        public bool Thrust;           // a forward jab (Lance, Halberd, Rapier): drawn as a thrust, not a sweep
        public float Stun;            // freezes what it hits for this long (not bosses)
        public float Circle, CircleAt, Power;  // Circle > 0: hits everything within this radius of the point CircleAt px along the aim (the Warhammer's slam); Power 0..1 is how charged it was (its look)
        public float HalfWidth;       // > 0: hits a rectangle this wide either side of the aim line, out to Reach (Lance), not an arc
        public bool NoCut;            // doesn't cut bullets (the Ninja's Kusarigama: a chain, not a blade)
        public bool Deflect;          // bats enemy bullets in the arc back as your own (the Quarterstaff: it parries as it swings)
        public float Bleed;           // each hit bleeds for this much of its damage per second (Bear Totem)
        public float ClawSide;        // != 0: a bear-claw swipe, drawn as claw marks raking this way (+1 / -1)
        public float Tip;             // > 0: a whip (the Blood Mage's Blood Lash): hits beyond this distance crack, closer ones are flung out to it
        public bool Clots;            // hits build the swinger's Clot (Blood Lash; BloodMage.cs)
        public float Taunt;           // taunts what it hits for this long (a Clot Burst)
        public int Sweep;             // > 0: one step of a sweep (Halberd): each enemy is hit once per sweep, however many steps reach it
        public bool NoFx;             // draws nothing itself (a Halberd step after the first: the first draws the whole sweep)
        public float FxAim, FxHalf;   // FxHalf > 0: its effect is this arc instead of its own (the Halberd's whole sweep)
        public byte Ice;              // the Frost Lance (Minions.cs): 1 = its icicle, 2 = the splinters off its sides (drawn as ice, not a jab)
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
        public byte Knuckle;          // an Arc Knuckles punch (1-6; ArcKnuckles.cs): the parry takes back one still winding up
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
        public const float IcicleHalfWidth = 14f, IcicleSlow = 0.3f, IcicleFxTime = 0.4f, SplinterFxTime = 0.4f;  // the Frost Lance's icicle (its length is the weapon's Reach)
        public const float SplinterReach = 60f, SplinterGrow = 15f, SplinterMult = 0.5f, SplinterDelay = 0.15f;  // and the splinters off its sides  // the Lance hits an 88 px wide box straight ahead
        // The Warhammer: a slam on a fixed circle in front of you. Holding charges its damage and stun, never its area.
        public const float HammerRadius = 80f, HammerCentreBase = 16f;
        /// How far in front of you the Warhammer's circle is centred, for a weapon reach (Reach upgrades push it out and, through the radius, widen it).
        public static float HammerCentre(float reach) => HammerCentreBase + 0.65f * reach;
        /// The Warhammer's blast colour: it also marks the blast as the hammer's, so the screen's melee look (MeleeLooks.cs),
        /// which draws the whole slam itself, can leave it out.
        public static readonly Rgba HammerBoomColor = new Rgba(0.85f, 0.8f, 0.7f);
        const float HammerTap = 0.25f, HammerStunMin = 0.25f, HammerStunMax = 0.9f;
        // The Halberd: a thin 18-degree cone swept across 80 degrees in six steps.
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
                    float stun = HammerStunMin + (HammerStunMax - HammerStunMin) * c, power = HammerTap + (1f - HammerTap) * c;
                    float reachMult = MeleeReach(p, w.Reach) / w.Reach;  // Reach upgrades widen the circle too
                    for (int k = 0; k <= extra; k++)  // each extra projectile: an aftershock slam, further out
                    {
                        var s = NewSwing(p, aim, w.Reach + w.Grow * k, 0f, w.Damage * dmg * power * (k == 0 ? 1f : 0.6f));
                        s.Circle = HammerRadius * reachMult;
                        s.CircleAt = HammerCentre(s.Reach);
                        s.Power = c;
                        s.Stun = stun;
                        s.Heavy = true;
                        s.Thrust = true;
                        s.Delay = 0.2f * k;
                        pendingSwings.Add(s);
                    }
                    // The blast's old ring (drawn when the screen's melee look is off; with it on, the slam draws its own impact).
                    BroadcastFx(FxKind.Boom, p.Pos + V2.FromAngle(aim) * HammerCentre(MeleeReach(p, w.Reach)), HammerBoomColor, HammerRadius * reachMult * (0.5f + 0.5f * c), 0.35f, c > 0.95f ? Sfx.BigExplode : Sfx.Explode);
                    break;
                }
                case WeaponId.Lance:
                    FireLance(p, aim, dmg, extra);  // the Breach's next step (Lance.cs)
                    break;
                case WeaponId.Halberd:
                {
                    // A long, thin cone that sweeps across in front of you; each enemy is hit once a sweep.
                    // An extra projectile sweeps it back again (60% damage). The swing is one motion timed from the click
                    // (HalberdMotion.cs: a 0.05 s pull back, then the sweep): its effect is sent now and plays the whole
                    // thing, and each step lands when the edge reaches it.
                    float reach = w.Reach + w.Grow * extra;
                    bool back = extra > 0;
                    BroadcastFx(FxKind.Slash, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, reach), Defs.BulletColor(Defs.KnightSlash),
                        HalberdSweep + HalberdConeDeg * MathX.Pi / 360f, HalberdMotion.Total(back), Sfx.None);
                    for (int pass = 0; pass <= Math.Min(1, extra); pass++)
                    {
                        int group = ++sweepCount;
                        for (int i = 0; i < HalberdSteps; i++)
                        {
                            float f = (float)i / (HalberdSteps - 1) * 2f - 1f;
                            var h = NewSwing(p, aim + (pass == 0 ? f : -f) * HalberdSweep, reach, HalberdConeDeg, w.Damage * dmg * (pass == 0 ? 1f : 0.6f));
                            h.Sweep = group;
                            h.Delay = HalberdMotion.StepTime(pass + 1, i, HalberdSteps);
                            h.Silent = i > 0;  // one whoosh a sweep, as the first step lands
                            h.NoFx = true;     // the click's effect draws it all
                            pendingSwings.Add(h);
                        }
                    }
                    break;
                }
                case WeaponId.HolyMace:
                {
                    // The Quarterstaff: a wide swipe that knocks enemies back and parries bullets as it goes. Every enemy it
                    // hits adds a Jab stack (Quarterstaff.cs); the screen's look alternates a left and a right swipe.
                    var s = NewSwing(p, aim, w.Reach, StaffSwipeArc, w.Damage * dmg);
                    s.Knock = StaffKnock;
                    s.Deflect = true;
                    s.Staff = true;
                    s.HealPerHit = 2f;  // each enemy hit heals you and allies near you (as the Holy Mace did)
                    s.HealRange = 220f;
                    s.HealHits = 4;
                    pendingSwings.Add(s);
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
                {
                    // Cut, return cut, overhead rend (Greatsword.cs). A full-Temper Rend makes the next chain start on the rend.
                    int step = p.GreatStep == 1 || p.GreatStep == 2 ? p.GreatStep + 1 : (p.GreatPrimed ? 3 : 1);
                    if (step == 3 && p.GreatStep != 2)
                        p.GreatPrimed = false;
                    p.GreatStep = (byte)step;
                    p.GreatGap = 0f;
                    p.FireCd = GreatInterval[step - 1] / FireRate(p);
                    var s = NewSwing(p, aim, w.Reach, step == 3 ? GreatRendArc : GreatCutArc, w.Damage * dmg * GreatMult[step - 1]);
                    s.Heavy = true;
                    if (step == 3)
                    {
                        s.Delay = GreatRendWind;  // the blade goes up first
                        BroadcastSound(Sfx.GreatWindup);
                        s.Knock = GreatRendKnock;
                        s.Waves = extra;
                        s.WaveDmg = GreatWaveDmg * dmg;
                    }
                    pendingSwings.Add(s);
                    SteelWall(p, aim, s.Reach, s.HalfArc, GreatWall[step - 1], s.Delay);  // the sweep's edge lingers, stopping bullets
                    break;
                }
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
                    pendingSwings.Add(s);
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

        void TickSteelWalls(float dt)
        {
            if (steelWalls.Count == 0)
                return;
            var stopped = new List<int>();
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
                foreach (var b in Bullets.Values)
                {
                    if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                        continue;
                    float d = b.Pos.DistanceTo(c.Center);
                    if (Math.Abs(d - c.Reach) > SteelWallBand + b.Radius || AngleTo(c.Center, b.Pos, c.Aim) > c.HalfArc)
                        continue;
                    stopped.Add(b.Id);
                }
            }
            foreach (int id in stopped)
                if (Bullets.ContainsKey(id))
                {
                    AddFx(FxKind.Spark, Bullets[id].Pos, Defs.BulletColor(Defs.KnightSlash), 8f, 0.2f);
                    Bullets.Remove(id);
                    pendingRemoved.Add(id);
                }
        }

        /// Lightbringer: each enemy its swings hit fills the light gauge; full, the next Noon Sunburst is Radiant (Lightbringer.cs).
        public const int LightMax = 12;
        public const float LightRadiant = 3f;

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
                    LandSwing(p, s);
            }
        }

        static float Dot(V2 a, V2 b) => a.X * b.X + a.Y * b.Y;

        static float AngleTo(V2 from, V2 to, float aim) => Math.Abs(MathX.Fmod((to - from).Angle - aim + MathX.Pi, MathX.Tau) - MathX.Pi);

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
                if (!s.Silent && !((s.Twin || s.LanceKind > 0 || s.Knuckle > 0) && s.Sound == Sfx.None))  // (a Twin Blades or Lance hit with no sound of its own: the step's is already playing)
                    BroadcastSound(s.Sound != Sfx.None ? s.Sound : Sfx.SwingHeavy);  // a Halberd sweep's or Twin Blades step's whoosh (its effect was sent at the click)
            }
            else if (s.FxHalf > 0f)  // the Halberd's whole sweep, from its first step (MeleeLooks.cs plays it over HalberdSweepTime)
                BroadcastFx(FxKind.Slash, origin, origin + V2.FromAngle(s.FxAim) * s.Reach, Defs.BulletColor(Defs.KnightSlash), s.FxHalf, 0.3f,
                    s.Silent ? Sfx.None : Sfx.SwingHeavy);
            else if (s.ClawSide != 0f)
                BroadcastFx(FxKind.Claw, origin, origin + dir * s.Reach, BearClawColor, s.ClawSide * s.HalfArc, 0.2f,
                    s.Silent ? Sfx.None : Sfx.SwingLight);
            else if (s.Ice != 0)
                BroadcastFx(s.Ice == 1 ? FxKind.Icicle : FxKind.Splinter, origin, origin + dir * s.Reach, Defs.BulletColor(Defs.FrostShot), s.HalfWidth,
                    s.Ice == 1 ? IcicleFxTime : SplinterFxTime, Sfx.None);
            else if (s.Root)
                BroadcastFx(FxKind.RootLash, origin, origin + dir * s.Reach, new Rgba(0.55f, 0.36f, 0.17f), s.HalfWidth, RootFxTime, Sfx.None);
            else if (s.DrawCut)
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, new Rgba(1f, 1f, 1f), -0.1f, 0.3f, s.Sound != Sfx.None ? s.Sound : Sfx.SwingHeavy);
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
                BroadcastFx(FxKind.Slash, origin, origin + dir * s.Reach, s.Tip > 0f || s.Clots ? LashRed : Defs.BulletColor(Defs.KnightSlash), s.Thrust ? -s.HalfArc : s.HalfArc, s.Heavy ? 0.24f : 0.16f,
                    s.Silent ? Sfx.None : s.Sound != Sfx.None ? s.Sound : s.Heavy ? Sfx.SwingHeavy : Sfx.SwingLight);

            // Who's in the arc (the Rapier only takes the nearest few).
            var hits = swingHits;
            hits.Clear();
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(origin);
                if (circle)
                {
                    if (e.Pos.DistanceTo(circleAt) > s.Circle + e.Radius)
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
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, e.Radius / d)) : MathX.Pi;
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
                    return q.DistanceTo(circleAt) <= s.Circle + lr;
                if (box)
                {
                    V2 rel = q - origin;
                    float along = Dot(rel, dir), across = Math.Abs(Dot(rel, dir.Orthogonal()));
                    return along >= -lr - p.Def.BodyR && along <= s.Reach + lr && across <= s.HalfWidth + lr && across >= s.Hollow;
                }
                float d = q.DistanceTo(origin);
                if (d - lr > s.Reach)
                    return false;
                float slack = d > 1f ? (float)Math.Asin(Math.Min(1f, lr / d)) : MathX.Pi;
                return full || AngleTo(origin, q, s.Aim) <= s.HalfArc + slack || d <= lr + p.Def.BodyR;
            });
            if (s.Sweep > 0)
            {
                hits.RemoveAll(e => e.SweepMark == s.Sweep);  // already hit by an earlier step of this sweep
                foreach (var e in hits)
                    e.SweepMark = s.Sweep;
            }
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
            if (s.Rapier && hits.Count > 0)
                RapierGainTempo(p, hits.Count);
            if (s.Knuckle == 7)
                KnuckleDashLanded(p, hits);  // the Arc Knuckles' Rocket Dash (ArcKnuckles.cs)  // a stack of Tempo for every enemy the thrust hits (Rapier.cs)
            int healed = 0;
            foreach (var e in hits)
            {
                float dmg = s.Dmg;
                bool crack = s.Tip > 0f && e.Pos.DistanceTo(origin) + e.Radius >= s.Tip;
                if (crack)
                    dmg *= LashCrack;
                dmg *= NinjaHitMult(p.Id, Defs.KnightSlash, e);  // Vanish's ambush (Ninja.cs)
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash, SlowAmt = s.Slow }, e);
                if (s.Bleed > 0f)
                    ApplyBleed(e, dmg * s.Bleed, p.Id, true);  // KitWeapons.cs
                if (s.Stun > 0f && !e.Def.IsBoss)
                {
                    e.FrozenT = Math.Max(e.FrozenT, s.Stun);
                    e.SlowT = Math.Max(e.SlowT, s.Stun);
                }
                if (s.LanceKind > 0)  // a Lance step: drives straight ahead (the sweep bats along its swing), sparks, and arms Impact (Lance.cs)
                {
                    V2 away = (e.Pos - origin).Normalized(), kd = LanceKnockDir(s, away, dir);
                    if (s.Knock > 0f && !Heavy(e))
                    {
                        KnockEnemy(e, kd * s.Knock);
                        e.ImpactBy = p.Id + 1;
                        e.ImpactLast = -1;
                    }
                    BroadcastFx(FxKind.LanceSpark, e.Pos, e.Pos + kd, LanceGlow, s.LanceKind == 4 ? 1f : 0.5f, 0.4f, Sfx.None);
                }
                else if (s.Knock > 0f && !Heavy(e))
                {
                    V2 away = (e.Pos - origin).Normalized();
                    KnockEnemy(e, (away.IsZero ? dir : away) * s.Knock);  // slides (Knock.cs)
                }
                else if (s.Knock < 0f && !Heavy(e))  // pulled in (Kusarigama), but never into you
                {
                    V2 away = (e.Pos - origin).Normalized();
                    float d = Math.Max(e.Pos.DistanceTo(origin) + s.Knock, e.Radius + p.Def.BodyR + 4f);
                    e.Pos = ClampToArena(origin + (away.IsZero ? dir : away) * d, e.Radius);
                }
                if (s.HealPerHit > 0f && healed < s.HealHits)
                {
                    healed++;
                    foreach (var q in Players.Values)
                        if (q.Pos.DistanceTo(origin) < s.HealRange)
                            Heal(q, s.HealPerHit, p);
                }
                if (s.Tip > 0f)
                    LashHit(p, e, s, crack);  // BloodMage.cs
                else if (s.Taunt > 0f)
                    TauntEnemy(p, e, s.Taunt);
                if (s.Clots)
                    AddClot(p, crack ? 2 : 1);
            }
            if (s.KatanaMove > 0)
                KatanaLanded(p, s, hits);  // each cut's look, a killing dash bringing the next step at once, the hit cue (Katana.cs)
            if (s.CenserStep > 0)
                CenserLanded(p, s, hits.Count);  // Embers, the Benediction's bonus heal and the step's incense (Censer.cs)
            if (s.HealSpin > 0f)
                foreach (var q in Players.Values)
                    if (q.Pos.DistanceTo(origin) < s.HealRange)
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
                int skewered = CutBullets(origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, s.HalfArc, true, RapierSkewerWidth, RapierSkewerMax);
                if (skewered > 0)
                    BroadcastFx(FxKind.Spark, origin + dir * (s.Reach * 0.6f), new Rgba(1f, 1f, 1f), 14f, 0.2f, Sfx.None);
            }
            else if (!s.NoCut && circle)
                CutBulletsInCircle(circleAt, s.Circle, s.CutAll);  // the slam clears small bullets over its whole circle
            else if (!s.NoCut)
                CutBullets(origin, s.Aim, s.Reach + p.Def.BodyR * 0.5f, full ? MathX.Pi : s.HalfArc, s.CutAll, s.HalfWidth);
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
                if (b.Pos.DistanceTo(origin) > reach + b.Radius || AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                hit.Add(b);
            }
            foreach (var b in hit)
            {
                Bullets.Remove(b.Id);
                pendingRemoved.Add(b.Id);
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
                if (b.Pos.DistanceTo(at) <= radius + b.Radius)
                    cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
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
                if (b.Pos.DistanceTo(origin) > reach + b.Radius)
                    continue;
                if (halfArc < MathX.Pi && AngleTo(origin, b.Pos, aim) > halfArc)
                    continue;
                cut.Add(b.Id);
            }
            foreach (int id in cut)
            {
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
            return cut.Count;
        }

        // --- Abilities ------------------------------------------------------------------

        /// The Knight: swings in FireKnightWeapon, abilities in KnightAbility. See KitDef (Classes.cs) for what each hook does.
        internal static readonly KitDef KnightKit = new KitDef
        {
            Id = Kit.Knight, Name = "Knight", Icon = "class_knight", HpMult = 1.15f,
            Blurb = "Melee: swings cut small bullets out of the air. Armoured: 115% health.",
            Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FireKnightWeapon(p, aim, dmg, extra),
            Ability = (g, p, id, target) => g.KnightAbility(p, id, target),
            OnKill = (g, e) => g.GauntletKill(e),  // a Challenged enemy's death sends the gauntlet back (Gauntlet.cs)
            OnShotHit = (g, b, e, hit) => g.SteelShardHit(b, e),  // Caught Steel's shards striking (CaughtSteel.cs)
        };

        /// The Knight's weapons (swung in FireKnightWeapon).
        /// Each entry's Kit, Role and Choice place it in the lobby (Defs.Weapons gathers every class's list).
        internal static readonly WeaponDef[] KnightWeapons =
        {
            // Tank
            new WeaponDef { Id = WeaponId.Warhammer, Kit = Kit.Knight, Role = Role.Tank, Choice = 0, Damage = 80f, Reach = 130f, Grow = 40f, Melee = true, ExtraDesc = "+1 aftershock: another slam 0.2 s later, 40 px further out, at 60% damage", Name = "Warhammer", FireInterval = 0.75f,
                Desc = "Hold to charge, let go to slam the ground in front of you. The longer the charge, the harder it hits and the longer it stuns." },
            new WeaponDef { Id = WeaponId.Lance, Kit = Kit.Knight, Role = Role.Tank, Choice = 1, Damage = 14f, Reach = 178f, Grow = 30f, Melee = true, ExtraDesc = "The Breaching Thrust: +30 px reach, 24 px wider and +60 px knockback", Name = "Lance", FireInterval = 0.24f,
                Desc = "A four-step chain with a lance and shield: two quick jabs, a wide sweep that bats enemies aside, then a Breaching Thrust that shoves everything in front far back and stuns it. Shoved enemies that hit another enemy or a wall take Impact damage. The weapon-action key hooks the enemy nearest your cursor, hauls it in and makes your next attack the Breaching Thrust." },
            new WeaponDef { Id = WeaponId.Halberd, Kit = Kit.Knight, Role = Role.Tank, Choice = 2, Damage = 30f, Reach = 200f, Grow = 30f, Melee = true, ExtraDesc = "+30 px reach and a return sweep (60% damage)", Name = "Halberd", FireInterval = 0.6f,
                Desc = "Sweeps a long, thin blade across in front of you, hitting each enemy once per sweep." },
            // Healer
            new WeaponDef { Id = WeaponId.HolyMace, Kit = Kit.Knight, Role = Role.Healer, Choice = 0, Damage = 18f, Reach = 120f, Melee = true, ExtraDesc = "+1 Jab stack limit (3 to start)", Name = "Quarterstaff", FireInterval = 0.5f,
                Desc = "Left and right staff swipes that knock enemies back and bat enemy bullets back at them. Every enemy hit heals you and nearby allies and stores a Jab stack (up to 3); the weapon-action key spends one on a cone jab." },
            new WeaponDef { Id = WeaponId.Lightbringer, Kit = Kit.Knight, Role = Role.Healer, Choice = 1, Damage = 17f, Reach = 80f, Melee = true, ExtraDesc = "+1 crescent of light on Dawn and Mercy, fanned out, and +30 px on the Sunburst", Name = "Lightbringer", FireInterval = 0.5f,
                Desc = "A three-step chain. Dawn Cut's crescent heals the allies it passes, Mercy Cut's wards them, and Noon Sunburst plants the sword in a ring that heals everyone near you. Hits fill a light gauge: full, the next Noon is Radiant. The weapon-action key spends the light on a Sunlance that heals the ally nearest your cursor." },
            new WeaponDef { Id = WeaponId.Censer, Kit = Kit.Knight, Role = Role.Healer, Choice = 2, Damage = 13f, Reach = 85f, Grow = 15f, Melee = true, ExtraDesc = "+15 px reach, +1 HP on every heal and +1 Ember", Name = "Censer", FireInterval = 0.26f,
                Desc = "A censer on a chain, swung in a three-step rite: a sweep, a return sweep and a Benediction that goes all the way round. Every step heals allies near you and leaves incense that heals allies in it and makes enemy shots fired from inside it stray. Each enemy hit lights an Ember; the weapon-action key throws the censer to your cursor, spending them on a burst and a big healing cloud." },
            // DPS
            new WeaponDef { Id = WeaponId.TwinBlades, Kit = Kit.Knight, Role = Role.Dps, Choice = 0, Damage = 16f, Reach = 75f, Melee = true, ExtraDesc = "+1 crescent: the Pirouette and the Crescent Shear each launch one more wave, fanned out", Name = "Twin Blades", FireInterval = 0.18f,
                Desc = "Two sabres in a five-step chain: rising cut, falling cut, double jab, pirouette, and a crossing shear that throws a crescent wave. Each step carries you forward. The weapon-action key hurls both sabres to cross at your cursor and fly back." },
            new WeaponDef { Id = WeaponId.Greatsword, Kit = Kit.Knight, Role = Role.Dps, Choice = 1, Damage = 40f, Reach = 125f, Melee = true, ExtraDesc = "+1 sword wave: the overhead rend launches a piercing crescent (50% damage)", Name = "Greatsword", FireInterval = 0.7f,
                Desc = "A three-hit chain: two quick cuts, then a slow overhead rend. The edge of each sweep hangs in the air as a crescent of steel that stops enemy bullets. Hold the weapon-action key to guard: the arc stops bullets and builds Temper, and letting go fires a beam that hits harder for every point of Temper." },
            new WeaponDef { Id = WeaponId.Rapier, Kit = Kit.Knight, Role = Role.Dps, Choice = 2, Damage = 18f, Reach = 130f, Melee = true, ExtraDesc = "Skewers 1 more enemy behind the first", Name = "Rapier", FireInterval = 0.48f,  // the first thrust; Tempo makes them quicker (Rapier.cs)
                Desc = "Precise thrusts at the nearest enemy in front, and they skewer enemy bullets along their line. The first thrust is slow, but every enemy you hit adds Tempo, and Tempo makes the thrusts quicker. Stop hitting and it holds a moment, then drains. The weapon-action key spends it on a Flourish: the blade whips back and forth, each swing throwing a crescent wave." },
            new WeaponDef { Id = WeaponId.ArcKnuckles, Kit = Kit.Knight, Role = Role.Dps, Choice = 3, Damage = 22f, Reach = 70f, Melee = true, ExtraDesc = "+1 aftershock: the Twin Hammer slams again 0.2 s later, 40 px further out (60% damage)", Name = "Arc Knuckles", FireInterval = 0.24f,
                Desc = "Coiled blue fists in a five-punch chain: jab, cross, a hook that sweeps a crowd, an uppercut that launches, and the Twin Hammer, a ground slam whose shockwave stuns. Raising the parry cancels a punch and starts the chain over; a perfect parry arms the gold Counter Hammer for your next press." },
        };

        /// The Knight's abilities and ultimates. (Upgrades.Abilities gathers every class's list.)
        internal static readonly AbilityDef[] KnightAbilities =
        {
            new AbilityDef
            {
                Id = AbilityId.ShieldWall, Kit = Kit.Knight, Role = Role.Tank, Choice = 0,
                Name = "Shield Wall", Cooldown = 12f, Desc = "Raise a wall of 7 steel plates: it stops every enemy bullet coming at your front (120°), even red aimed shots. Every shot wears down the plate it hits (400 damage in all, shared between the plates) and a broken plate leaves a gap. The wall stays up until every plate is gone.",
                Capstone = "Bullets that hit the shield are reflected back as your own.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LeapSlam, Kit = Kit.Knight, Role = Role.Tank, Choice = 1,
                Name = "Leap Slam", Cooldown = 9f, Desc = "Leap to your cursor (up to 320 px), untouchable in the air. Landing deals 50 damage around you and stuns enemies for 1 s.",
                Capstone = "The landing also erases enemy bullets within 160 px.",
                Bot = b => b.HaveTarget && b.TargetD > 140f && b.TargetD < 340f || b.BotDanger > 2.5f,
            },
            new AbilityDef
            {
                Id = AbilityId.CavalryCall, Kit = Kit.Knight, Role = Role.Tank, Choice = 2,
                Name = "Cavalry Call", Cooldown = 18f, Desc = "Two lancers ride in at your sides and charge across the map along your aim. Each enemy they meet takes 35, is shoved out of the lane and slowed 45% for 3 s (Heavy enemies and bosses barely move and are slowed less). You take 20% less damage for 3 s.",
                Capstone = "Two more lancers ride outside the first pair, and the guard lasts 5 s.",
                Bot = b => b.HaveTarget && b.TargetD < 700f,
            },
            new AbilityDef
            {
                Id = AbilityId.Consecrate, Kit = Kit.Knight, Role = Role.Healer, Choice = 0,
                Name = "Consecrate", Cooldown = 14f, Desc = "Throw a Holy Cross along your aim. It grows as it flies: allies it crosses heal 35 and run 30% faster for 4 s; enemies take 25, are slowed 40% for 3 s and are knocked back.",
                Capstone = "Two more crosses fly out at 22 degrees either side (each target is hit once).",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.GuardiansOath, Kit = Kit.Knight, Role = Role.Healer, Choice = 1,
                Name = "Guardian's Oath", Cooldown = 15f, Desc = "Charge to the player nearest your cursor (up to 600 px), downed or not, and drag them clear. A Ward of Light then cuts damage by half for everyone inside for 5 s, and downed allies in it revive twice as fast.",
                Capstone = "Charges up to 800 px. The ward is wider, follows you, and stands a downed ally back up at 25% health.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.LayOnHands, Kit = Kit.Knight, Role = Role.Healer, Choice = 2,
                Name = "Lay on Hands", Cooldown = 18f, Desc = "Heal the most hurt ally within 300 px (or yourself) for 70, or instantly revive them at half health if they're down.",
                Capstone = "Also heals everyone else near them for half as much.",
                Bot = b => b.AllyHurt,
            },
            new AbilityDef
            {
                Id = AbilityId.Whirlwind, Kit = Kit.Knight, Role = Role.Dps, Choice = 0, Instant = true,  // Misericorde (Misericorde.cs); the id is Whirlwind's, kept so the numbers stay put
                Name = "Misericorde", Cooldown = 12f, Desc = "Leap onto the wounded enemy (under 25% health, from anyone's damage) nearest your cursor within 200 px and execute it with the mercy dagger, then leap on to the next wounded enemy within 200 px, and the next, until none are left in reach. You can't be hurt while it runs. A wounded boss takes a heavy stab (120) instead, and the chain ends there.",
                Capstone = "Each execution heals you 5% of your health.",
                Bot = b => b.HaveTarget && b.TargetD < 260f,
            },
            new AbilityDef
            {
                Id = AbilityId.Lunge, Kit = Kit.Knight, Role = Role.Dps, Choice = 1, Instant = true,  // The Gauntlet (Gauntlet.cs); the id is Lunge's
                Name = "The Gauntlet", Cooldown = 9f, Desc = "Throw down the gauntlet at the enemy nearest your cursor along your aim (up to 380 px; it flies over anything in between): it takes 12 and is Challenged for 5 s. You shoulder-charge to it, untouchable, bowling everything in the way aside (18 and a shove), and hit it for 30. Kill the Challenged enemy before the Challenge runs out and the gauntlet flies back: the cooldown resets.",
                Capstone = "The Challenge lasts 8 s, and the charge's impact stuns for 1 s.",
                Bot = b => b.HaveTarget && b.TargetD > 90f && b.TargetD < 380f,
            },
            new AbilityDef
            {
                Id = AbilityId.BattleFury, Kit = Kit.Knight, Role = Role.Dps, Choice = 2,  // Caught Steel (CaughtSteel.cs); the id is Battle Fury's
                Name = "Caught Steel", Cooldown = 16f, Desc = "For 6 s, enemy bullets that skim past you without touching you are caught and hang round you as steel (up to 12). Press again (or let the time run out) and a sweeping cut hurls them out along your aim: each shard cuts the bullets in its way and hits the first enemy for 22. A hit knocks a third of them loose. The cooldown starts when you throw them.",
                Capstone = "Holds up to 18 shards.",
                Bot = b => b.Danger,
            },
            new AbilityDef
            {
                Id = AbilityId.LastStand, Kit = Kit.Knight, Role = Role.Tank, Choice = AbilityDef.Ult,
                Name = "Last Stand", Cooldown = 70f, Desc = "You plant yourself and brace a wall of steel plates 120 px out, 178° wide, facing your cursor (it turns slowly). It stops every enemy bullet from the front; your teammates' shots pass out through it. You take 10% of the damage it stops. You can't move while it stands, and it has no timer: it stays until you go down or press the ability again, and the cooldown starts when it comes down.",
                Capstone = "You take 5% of the damage instead of 10%.",
                Bot = b => b.Danger || b.AllyDown && b.DownedD < 70f,  // planted over a downed ally, the reviver stands in the lee
            },
            new AbilityDef
            {
                Id = AbilityId.HallowedBlade, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult,
                Name = "Hallowed Blade", Cooldown = 80f, Desc = "A giant holy sword falls at your cursor: 200 damage around it, then it stays planted for 8 s (allies in the ring heal 15 HP/s, enemies burn), and every 2 s a pulse crosses the whole arena healing allies and hurting enemies for a third as much.",
                Capstone = "Three swords fall: one at your cursor and one either side.",
            },
            new AbilityDef
            {
                Id = AbilityId.BladeTempest, Kit = Kit.Knight, Role = Role.Dps, Choice = AbilityDef.Ult,  // the Drawbridge (Drawbridge.cs); the id is Blade Tempest's
                Name = "Drawbridge", Cooldown = 75f, Desc = "A gatehouse rises behind you and its drawbridge falls along your aim: 500 px long and 100 wide, it crushes everything in the lane for 260 and throws enemies at its edges aside (45). Then it lies there for 5 s as a walkway: enemies can't step onto it and the rails grind any pressed against them (80 a second), and their shots stop at its rails, red aimed shots too.",
                Capstone = "The bridge stands for 8 s.",
                Bot = b => b.HaveTarget && b.TargetD < 450f,
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
                        if (q.Pos.DistanceTo(p.Pos) > range)
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
                                if (q != best && !q.Downed && q.Pos.DistanceTo(best.Pos) < 200f)
                                    Heal(q, 35f * s.LayMult, p);
                    }
                    return true;
                }
                case AbilityId.Whirlwind:
                    CastMisericorde(p, target, pw, cap);  // Misericorde.cs
                    return true;
                case AbilityId.Lunge:
                    CastGauntlet(p, target, pw, cap);  // Gauntlet.cs
                    return true;
                case AbilityId.BattleFury:
                    CastCaughtSteel(p, pw, cap);  // CaughtSteel.cs
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
                case AbilityId.BladeTempest:
                    CastDrawbridge(p, target, cap);  // Drawbridge.cs
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
            TickSteel(dt);      // CaughtSteel.cs
            TickGauntlets(dt);  // Gauntlet.cs
            TickMercy(dt);      // Misericorde.cs
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
                if (!Players.TryGetValue(sl.Owner, out p))
                    continue;
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Pos.DistanceTo(sl.Pos) > sl.Radius + e.Radius)
                        continue;
                    HurtEnemy(e, SlamDamage * p.Stats.DmgMult * sl.Power, p.Id);
                    if (!e.Def.IsBoss)
                    {
                        e.FrozenT = Math.Max(e.FrozenT, sl.Stun);
                        e.SlowT = Math.Max(e.SlowT, sl.Stun);
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
            float off = MathX.Fmod(d.Angle - p.Aim + MathX.Pi, MathX.Tau) - MathX.Pi;  // -pi..pi, signed
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
    }
}

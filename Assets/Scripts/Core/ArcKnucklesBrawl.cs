using System;

namespace Arena
{
    /// The Arc Knuckles' Brawl stance (Docs/ArcKnuckles.md "Stance rework", Docs/Mockups/arc-knuckles-stances.html, wheel steering).
    /// Fire is a click per move (held, it repeats); Player.KnuckleMove is the next one:
    ///   0 Pull Punch: click an enemy within KnucklePullRange: you're pulled in and punch it, a short stun on the group round it. It
    ///     opens the click wheel (your own game: Game/ArcKnucklesWheel.cs), fixed on the screen where you clicked.
    ///   From there each click steers by the side of the wheel it lands on (PlayerInput.Steer, an angle; none in its centre):
    ///   1 Drive Punch, 3 Drive Punch: punch the enemy nearest you; it and everything round it fly KnuckleDriveKnock the steered way
    ///     (with no steer: on along you → it). An enemy driven into the arena's wall takes a Wall Slam.
    ///   2 Slip Dash, 4 Slip Dash: dash KnuckleSlipLen the steered way (with no steer: at the cursor), untouchable.
    ///   5 Pin Punch: a hard punch that stuns the group and stops it where it is.
    ///   6 Barrage: eight jabs and a straight round the enemy nearest you; you're rooted; a stunned enemy stays stunned through it.
    ///   7 Quake Punch: hold to charge, let go to punch the ground: hurts and throws back everything round you, more the longer held.
    /// The punches need an enemy in reach: a click with none swings at the air and keeps your place. KnuckleBrawlIdle without a
    /// move and it starts over at the Pull Punch. Each punch steps you in to its enemy first if it's out of reach of your fist.
    public partial class Game
    {
        public const float KnuckleClickBuffer = 0.3f, KnuckleBrawlIdle = 1.6f;
        /// The Pull Punch: how far you can be pulled, how close to the cursor its enemy must be, its blast, damage and stun.
        public const float KnucklePullRange = 230f, KnucklePullLock = 70f, KnucklePullR = 50f, KnucklePullMult = 0.8f, KnucklePullStun = 0.5f;
        /// The Drive Punch: how far its enemy can be, its blast, damage (first and second), and how far it sends them.
        public const float KnuckleDriveReach = 150f, KnuckleDriveR = 60f, KnuckleDriveMult = 1f, KnuckleDrive2Mult = 1.15f, KnuckleDriveKnock = 190f;
        /// The Slip Dash's length; the Pin Punch's blast, damage and stun.
        public const float KnuckleSlipLen = 130f, KnucklePinR = 85f, KnucklePinMult = 1.6f, KnucklePinStun = 1.8f;
        /// The Barrage: its blast round the enemy, a jab's and the straight's damage, how long a stun it keeps up, the root.
        public const float KnuckleBarrageR = 75f, KnuckleBarrageMult = 0.35f, KnuckleBarrageStraight = 0.7f, KnuckleBarrageHold = 0.45f, KnuckleBarrageRoot = 0.95f;
        /// The Quake Punch: the full charge, how long a full charge can be held before it lets go on its own, its radius, damage
        /// and shove at no charge and at full.
        public const float KnuckleQuakeCharge = 1.2f, KnuckleQuakeHoldMax = 0.6f, KnuckleQuakeR0 = 90f, KnuckleQuakeR1 = 180f;
        public const float KnuckleQuakeMult0 = 1.4f, KnuckleQuakeMult1 = 3f, KnuckleQuakeKnock0 = 60f, KnuckleQuakeKnock1 = 160f;
        /// A Wall Slam: an enemy a Drive Punch sent into the arena's wall takes this share of the weapon's damage and a stun.
        public const float KnuckleSlamMult = 0.5f, KnuckleSlamStun = 0.6f;
        /// The punches step you in to their enemy when it's farther than this past your reach, and stop this short of it.
        public const float KnuckleStepIn = 10f, KnuckleStepStop = 6f;
        public const int KnuckleBrawlMoves = 8;

        /// The Brawl moves' names, in order (the pips and the wheel's label).
        public static readonly string[] KnuckleBrawlNames = { "Pull Punch", "Drive Punch", "Slip Dash", "Drive Punch", "Slip Dash", "Pin Punch", "Barrage", "Quake Punch" };
        /// The Rocket stance's moves.
        public static readonly string[] KnuckleRocketNames = { "Rocket Dash", "Flurry", "Grab & Throw", "Rocket Punch" };

        /// The click wheel's steer as a byte: 0 none, else 1-255 round the circle.
        public static byte KnuckleSteerCode(float angle)
        {
            float k = MathX.WrapPi(angle) / MathX.Tau;
            if (k < 0f)
                k += 1f;
            return (byte)(1 + Math.Min(254, (int)Math.Round(k * 255f) % 255));
        }

        /// The way a steer byte points, or false for none.
        public static bool KnuckleSteerDir(byte code, out V2 dir)
        {
            dir = code == 0 ? V2.Zero : V2.FromAngle((code - 1) / 255f * MathX.Tau);
            return code != 0;
        }

        /// The enemy a Pull Punch from `from` takes: the one nearest the cursor, if it's within KnucklePullLock of it and within
        /// KnucklePullRange of you (Live Wire: farther). Your own game asks too, to mark it.
        public Enemy KnucklePullTarget(V2 from, V2 cursor, float reachMult = 1f)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(from, KnucklePullRange * reachMult + e.Radius))
                    continue;
                float d = e.Pos.DistanceTo(cursor) - e.Radius;
                if (d < KnucklePullLock && d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// The enemy a Drive Punch, Pin Punch or Barrage from `from` takes: the nearest within `reach`, or null.
        public Enemy KnuckleNearTarget(V2 from, float reach)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                float d = e.Pos.DistanceTo(from) - e.Radius;
                if (d <= reach && d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host, every tick with the Arc Knuckles out (from the fire loop, Game.cs): a click is one move (one clicked during a move
        /// waits KnuckleClickBuffer for it), held Fire repeats; on the Quake Punch, held Fire charges and letting go throws it.
        void TickKnuckleFire(Player p, float dt)
        {
            bool click = p.Firing && !p.KnuckleFireWas;
            p.KnuckleFireWas = p.Firing;
            p.KnuckleBufT = click ? KnuckleClickBuffer : Math.Max(0f, p.KnuckleBufT - dt);
            if (p.KnuckleRootT > 0f)
                p.KnuckleRootT = Math.Max(0f, p.KnuckleRootT - dt);
            p.KnuckleRooted = p.KnuckleRootT > 0f;
            if (p.KnuckleMove != 0 && !p.KnuckleCharging && p.FireCd <= 0f && (p.KnuckleIdleT += dt) > KnuckleBrawlIdle)
                p.KnuckleMove = 0;  // too long without a move: the combo starts over (the wheel closes)
            if (p.ParryActive || p.StunT > 0f || State == MatchState.GameOver || InGreatStar(p) || p.RepelPull)
            {
                if (p.KnuckleCharging)
                {
                    p.KnuckleCharging = false;
                    p.KnuckleChargeT = 0f;
                    p.KnuckleCharge = 0;
                }
                return;
            }
            if (p.KnuckleCharging)
            {
                if (p.Firing && p.KnuckleChargeT < KnuckleQuakeCharge + KnuckleQuakeHoldMax)
                {
                    p.KnuckleChargeT += dt;
                    p.KnuckleCharge = (byte)Math.Round(255f * Math.Min(1f, p.KnuckleChargeT / KnuckleQuakeCharge));
                    return;
                }
                p.KnuckleCharging = false;
                FirePlayer(p);  // let go: the Quake Punch (FireBrawl reads the charge)
                p.KnuckleChargeT = 0f;
                p.KnuckleCharge = 0;
                return;
            }
            if (p.FireCd > 0f || !(p.Firing || p.KnuckleBufT > 0f) || !ImplodeReady(p))
                return;
            p.KnuckleBufT = 0f;
            if (p.KnuckleStance == 0 && p.KnuckleMove == 7 && !p.KnuckleArmed)
            {
                p.KnuckleCharging = true;  // the Quake Punch: charging while Fire is held
                p.KnuckleChargeT = 0f;
                p.KnuckleIdleT = 0f;
                return;
            }
            FirePlayer(p);
        }

        /// Host: the Brawl stance's next move (from FireArcKnuckles).
        void FireBrawl(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            float rm = KnuckleReachMult(p), hit = w.Damage * dmg;
            int move = p.KnuckleMove;
            p.KnuckleIdleT = 0f;
            V2 steer;
            bool steered = KnuckleSteerDir(p.KnuckleSteer, out steer);
            switch (move)
            {
                case 0:  // the Pull Punch
                {
                    var e = KnucklePullTarget(p.Pos, p.AimAt, p.Stats.KnuckleDashMult);
                    if (e == null)
                    {
                        KnuckleWhiff(p, aim);
                        return;
                    }
                    float t = KnuckleStepTo(p, e, 0f);
                    var s = KnuckleBlast(p, e.Pos, KnucklePullR * rm, hit * KnucklePullMult, t + 0.05f, 12);
                    s.Stun = KnucklePullStun;
                    s.NoKnock = true;
                    KnuckleLook(p, e.Pos, 2, t > 0f ? Sfx.KnuckleRocket : Sfx.KnuckleJab);
                    p.FireCd = t + 0.2f;
                    break;
                }
                case 1:
                case 3:  // a Drive Punch
                {
                    var e = KnuckleNearTarget(p.Pos, KnuckleDriveReach * rm);
                    if (e == null)
                    {
                        KnuckleWhiff(p, aim);
                        return;
                    }
                    V2 dir = steered ? steer : AwayFrom(e.Pos, p.Pos, V2.FromAngle(aim));
                    float t = KnuckleStepTo(p, e, MeleeReach(p, w.Reach));
                    var s = KnuckleBlast(p, e.Pos, KnuckleDriveR * rm, hit * (move == 1 ? KnuckleDriveMult : KnuckleDrive2Mult), t + KnuckleLand[0], 13);
                    s.NoKnock = true;
                    s.KnuckleKnock = dir * KnuckleDriveKnock;
                    KnuckleLook(p, e.Pos, move == 1 ? 1 : 2, Sfx.KnuckleJab);
                    p.FireCd = t + 0.28f;
                    break;
                }
                case 2:
                case 4:  // a Slip Dash
                {
                    V2 dir = steered ? steer : AwayFrom(p.AimAt, p.Pos, V2.FromAngle(aim));
                    V2 from = p.Pos, to = ClampToArena(from + dir * KnuckleSlipLen, p.Def.BodyR);
                    float t = KnuckleMoveTo(p, to);
                    p.InvulnT = Math.Max(p.InvulnT, t + KnuckleDashGrace);
                    BroadcastFx(FxKind.KnuckleDash, from, to, ArcBlue, p.Id, t + KnuckleDashFxAfter, Sfx.KnuckleRocket);
                    p.FireCd = t + 0.05f;
                    break;
                }
                case 5:  // the Pin Punch
                {
                    var e = KnuckleNearTarget(p.Pos, KnuckleDriveReach * rm);
                    if (e == null)
                    {
                        KnuckleWhiff(p, aim);
                        return;
                    }
                    float t = KnuckleStepTo(p, e, MeleeReach(p, w.Reach));
                    var s = KnuckleBlast(p, e.Pos, KnucklePinR * rm, hit * KnucklePinMult, t + KnuckleLand[2], 14);
                    s.NoKnock = true;
                    s.Stun = KnucklePinStun + p.Stats.KnuckleStunBonus;  // (Live Wire: longer)
                    s.Heavy = true;
                    KnuckleLook(p, e.Pos, 3, Sfx.KnuckleUpper);
                    p.FireCd = t + 0.42f;
                    break;
                }
                case 6:  // the Barrage
                {
                    var e = KnuckleNearTarget(p.Pos, KnuckleDriveReach * rm);
                    if (e == null)
                    {
                        KnuckleWhiff(p, aim);
                        return;
                    }
                    float t = KnuckleStepTo(p, e, MeleeReach(p, w.Reach));
                    V2 at = e.Pos;
                    for (int i = 0; i <= FlurryJabs; i++)
                    {
                        bool straight = i == FlurryJabs;
                        var s = KnuckleBlast(p, at, KnuckleBarrageR * rm, hit * (straight ? KnuckleBarrageStraight : KnuckleBarrageMult),
                            t + (straight ? FlurryStraightAt : FlurryStart + i * FlurryGap), straight ? 16 : 15);
                        s.NoKnock = true;
                        s.Heavy = straight;
                    }
                    p.KnuckleRootT = t + KnuckleBarrageRoot;
                    p.KnuckleRooted = true;
                    BroadcastFx(FxKind.KnuckleChain, p.Pos, at, ArcBlue, p.Id * 10 + 1, FlurryDur, Sfx.None);  // (the Flurry's look)
                    p.FireCd = t + FlurryStraightAt + 0.1f;
                    break;
                }
                default:  // the Quake Punch, let go
                {
                    float c = Math.Min(1f, p.KnuckleChargeT / KnuckleQuakeCharge), r = (KnuckleQuakeR0 + (KnuckleQuakeR1 - KnuckleQuakeR0) * c) * rm;
                    float mult = KnuckleQuakeMult0 + (KnuckleQuakeMult1 - KnuckleQuakeMult0) * c;
                    for (int k = 0; k <= extra; k++)  // each extra projectile: an aftershock, wider and later
                    {
                        var s = KnuckleSwing(p, aim, 0f, 0f, hit * mult * (k == 0 ? 1f : KnuckleAftershockMult), KnuckleLand[3] + KnuckleAftershock * k, 17);
                        s.Circle = r + KnuckleAftershockOut * k;
                        s.Knock = KnuckleQuakeKnock0 + (KnuckleQuakeKnock1 - KnuckleQuakeKnock0) * c;
                        s.Power = c;
                        s.Heavy = true;
                        s.Sound = Sfx.KnuckleSlam;
                    }
                    BroadcastFx(FxKind.KnuckleStep, p.Pos, p.Pos + V2.FromAngle(aim) * r, c >= 1f ? ArcGold : ArcBlue, p.Id * 10 + 4, KnuckleLand[3] + 0.7f + KnuckleAftershock * extra, Sfx.None);
                    p.FireCd = 0.45f;
                    p.KnuckleMove = 0;
                    return;
                }
            }
            p.KnuckleMove = (byte)(move + 1);
        }

        /// A punch's blast: everything within `radius` of `at`, landing `delay` after the click.
        Swing KnuckleBlast(Player p, V2 at, float radius, float dmg, float delay, int kind)
        {
            var s = KnuckleSwing(p, (at - p.Pos).Angle, 0f, 0f, dmg, delay, kind);
            s.FromSet = true;
            s.From = at;
            s.Circle = radius;
            return s;
        }

        /// Steps `p` in to `e` when it's out of reach (a lunge, moved like a dash) and returns how long that takes (0: in reach).
        float KnuckleStepTo(Player p, Enemy e, float reach)
        {
            float gap = e.Pos.DistanceTo(p.Pos) - e.Radius - p.Def.BodyR;
            if (gap <= reach * 0.5f + KnuckleStepIn)
                return 0f;
            V2 dir = AwayFrom(e.Pos, p.Pos, V2.FromAngle(p.Aim));
            return KnuckleMoveTo(p, ClampToArena(e.Pos - dir * (e.Radius + p.Def.BodyR + KnuckleStepStop), p.Def.BodyR));
        }

        /// A punch's look and its sound (look 1 a jab, 2 a cross, 3 the Pin Punch).
        void KnuckleLook(Player p, V2 at, int look, Sfx sound) =>
            BroadcastFx(FxKind.KnuckleStep, p.Pos, at, ArcBlue, p.Id * 10 + look, KnuckleDur[look - 1], sound);

        /// A punch with nothing to hit: a jab at the air; the combo keeps its place.
        void KnuckleWhiff(Player p, float aim)
        {
            BroadcastFx(FxKind.KnuckleStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, p.Weapon.Reach), ArcBlue, p.Id * 10 + 1, KnuckleDur[0], Sfx.KnuckleJab);
            p.FireCd = 0.18f;
        }

        /// Host: what a Brawl punch does to each enemy it hits beyond the damage (from LandSwing): a Drive Punch sends it the steered
        /// way (and arms its Wall Slam), the Pin Punch stops it dead, a Barrage jab keeps a stun going.
        void KnuckleHitExtra(Player p, Swing s, Enemy e)
        {
            switch (s.Knuckle)
            {
                case 13:
                    if (!Heavy(e) && !s.KnuckleKnock.IsZero)
                    {
                        KnockEnemy(e, s.KnuckleKnock);
                        e.KnuckleSlamBy = p.Id + 1;
                    }
                    break;
                case 14:
                    e.KnockLeft = V2.Zero;  // pinned where it stands
                    e.KnockSpeed = 0f;
                    e.KnuckleSlamBy = 0;
                    break;
                case 15:
                case 16:
                    if (e.StunT > 0f && !e.Def.IsBoss)
                        Stun(e, KnuckleBarrageHold);  // the stun holds through the Barrage
                    break;
            }
        }

        /// Host: an enemy a Drive Punch sent hit the arena's wall still sliding (from TickKnock): a Wall Slam.
        void KnuckleWallSlam(Enemy e)
        {
            Player p;
            int by = e.KnuckleSlamBy - 1;
            e.KnuckleSlamBy = 0;
            if (!Players.TryGetValue(by, out p) || p.Weapon.Id != WeaponId.ArcKnuckles)
                return;
            FangHurt(p, e, p.Weapon.Damage * p.Stats.DmgMult * KnuckleSlamMult, e.Pos);  // (TwinBlades.cs: the hit through the on-hit pipeline)
            Charge(e, p.Id);
            if (!e.Def.IsBoss)
                Stun(e, KnuckleSlamStun);
            BroadcastFx(FxKind.Ring, e.Pos, ArcGold, e.Radius + 20f, 0.3f, Sfx.None);
            KnuckleHitCue(p, 18, 1, e.Pos);
        }

        /// Host: the weapon-action key with the Arc Knuckles: Brawl to Rocket and back. Each starts at its first move.
        void SwapKnuckleStance(Player p)
        {
            if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed || State == MatchState.GameOver)
                return;
            p.KnuckleStance = (byte)(p.KnuckleStance == 0 ? 1 : 0);
            ResetKnuckleChain(p);
            p.FireCd = 0.15f;
            BroadcastFx(FxKind.Ring, p.Pos, p.KnuckleStance == 0 ? ArcBlue : ArcEmber, p.Def.BodyR + 18f, 0.35f, Sfx.KnuckleArm);
        }
    }
}

using System;

namespace Arena
{
    /// The Arc Knuckles (DPS Knight, the fourth weapon: Choice 3; Docs/ArcKnuckles.md, Docs/Mockups/arc-knuckles-stances.html, the Arc Coil look).
    /// Two stances, swapped on the weapon-action key (SwapKnuckleStance; Player.KnuckleStance):
    ///   - Brawl (ArcKnucklesBrawl.cs): a Pull Punch opens the click wheel, then seven moves steered by where you click on it.
    ///   - Rocket: the Rocket Dash and its chain (Flurry, Grab & Throw, Rocket Punch; ArcKnucklesChain.cs) on Fire, one move a click,
    ///     with no windows to hit: KnuckleRocketIdle without a click and it's back to the dash.
    ///   - Parry: raising it cancels a punch still winding up and sends the stance back to its first move (ResetAttackChains). A
    ///     perfect parry also arms the Counter Hammer (Player.KnuckleArmed): the next click, in either stance, is a gold ground slam.
    ///     It fades after KnuckleArmTime unused.
    /// Each punch is one effect (FxKind.KnuckleStep, its Radius = id * 10 + its look: 1 a jab, 2 a cross, 3 the Pin Punch, 4 the Quake
    /// Punch, 5 the Counter Hammer), each dash and lunge one (FxKind.KnuckleDash), drawn from their age (Game/ArcKnucklesLook.cs).
    public partial class Game
    {
        public const float KnuckleArmTime = 3f;
        /// Each look (1 jab, 2 cross, 3 Pin Punch, 4 Quake Punch, 5 Counter Hammer): when its fist lands after the press, and how long it plays.
        public static readonly float[] KnuckleLand = { 0.09f, 0.11f, 0.14f, 0.26f, 0.26f };
        public static readonly float[] KnuckleDur = { 0.28f, 0.32f, 0.42f, 0.62f, 0.62f };
        /// The Counter Hammer: its share of the weapon's damage, where it slams ahead of you, its shockwave, shove and stun.
        public const float KnuckleCounterMult = 38f / 12f, KnuckleCounterNext = 0.62f;
        public const float KnuckleHammerAt = 44f, KnuckleCounterR = 165f, KnuckleCounterKnock = 48f, KnuckleCounterStun = 0.8f;
        public const float KnuckleAftershock = 0.2f, KnuckleAftershockOut = 40f, KnuckleAftershockMult = 0.6f;
        public static readonly Rgba ArcBlue = new Rgba(0.48f, 0.83f, 1f), ArcGold = new Rgba(1f, 0.82f, 0.29f), ArcEmber = new Rgba(1f, 0.6f, 0.29f);
        /// The Rocket Dash: how far it goes with nothing locked, how long, its damage, the lane it hits in, the short shove; a dash
        /// that hits nothing can't be thrown again for KnuckleDashMiss.
        public const float KnuckleDashLen = 130f, KnuckleDashTime = 0.2f, KnuckleDashMult = 2f, KnuckleDashWidth = 28f, KnuckleDashMiss = 0.6f;
        public const float KnuckleDashFist = 30f, KnuckleDashKnock = 24f, KnuckleDashGrace = 0.08f;
        /// The lock-on: how far from you an enemy can be locked (the one nearest the cursor), the most a locked dash flies, where it
        /// stops short of the enemy, its speed (px/s; every dash and lunge moves at it), the shortest flight, the longest your own
        /// game takes to cover it (a far-off client catches up), when in the flight the fist lands, and how long its look lasts after.
        public const float KnuckleLockRange = 300f, KnuckleDashMax = 280f, KnuckleDashStop = 8f, KnuckleDashSpeed = 650f, KnuckleDashMinTime = 0.08f;
        public const float KnuckleDashMaxTime = 0.5f, KnuckleDashLandAt = 0.8f, KnuckleDashFxAfter = 0.35f;
        /// The Rocket stance: this long without a click and the chain is back to the dash.
        public const float KnuckleRocketIdle = 4f;

        /// Host: one click of the primary with the Arc Knuckles (from FireKnightWeapon, through TickKnuckleFire): the Counter Hammer
        /// if it's armed, else the stance's next move.
        void FireArcKnuckles(Player p, float aim, float dmg, int extra)
        {
            if (p.KnuckleArmed)
            {
                FireCounterHammer(p, aim, dmg, extra);
                return;
            }
            if (p.KnuckleStance == 0)
            {
                FireBrawl(p, aim, dmg, extra);  // ArcKnucklesBrawl.cs
                return;
            }
            if (p.KnuckleChain == 2 || p.KnuckleChain == 4 || p.KnuckleChain == 6)
                return;  // a chain move is running (ArcKnucklesChain.cs)
            if ((p.KnuckleChain & 1) == 1 && FireKnuckleChain(p, aim, dmg))
                return;  // the chain's next move
            HostRocketDash(p, p.AimAt);  // else it starts with the dash
        }

        /// The Counter Hammer: both fists slam the ground ahead of you, a gold shockwave that shoves and stuns. Each extra projectile
        /// adds an aftershock further out.
        void FireCounterHammer(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            p.KnuckleArmed = false;
            p.KnuckleArmedT = 0f;
            p.FireCd = KnuckleCounterNext / FireRate(p);
            float hit = w.Damage * dmg * KnuckleCounterMult, land = KnuckleLand[4], reachMult = KnuckleReachMult(p);
            for (int k = 0; k <= extra; k++)
            {
                var s = KnuckleSwing(p, aim, KnuckleHammerAt + KnuckleAftershockOut * k, 0f, hit * (k == 0 ? 1f : KnuckleAftershockMult), land + KnuckleAftershock * k, 6);
                s.Circle = KnuckleCounterR * reachMult;
                s.CircleAt = KnuckleHammerAt + KnuckleAftershockOut * k;
                s.Knock = KnuckleCounterKnock;
                s.Stun = KnuckleCounterStun + p.Stats.KnuckleStunBonus;  // (Live Wire: longer)
                s.Power = 1f;
                s.Heavy = true;
                s.Sound = k == 0 ? Sfx.KnuckleCounter : Sfx.KnuckleSlam;
            }
            BroadcastFx(FxKind.KnuckleStep, p.Pos, p.Pos + V2.FromAngle(aim) * KnuckleHammerAt, ArcGold, p.Id * 10 + 5, land + 0.7f + KnuckleAftershock * extra, Sfx.None);
        }

        /// How much Reach upgrades widen the punches' blasts and the slams.
        float KnuckleReachMult(Player p) => MeleeReach(p, p.Weapon.Reach) / Math.Max(1f, p.Weapon.Reach);

        /// One punch landing `delay` after the press.
        Swing KnuckleSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, int kind)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the move's own effect draws it (and the press played its sound; a slam has its own as it lands)
            s.Knuckle = (byte)kind;
            s.StarStun = true;  // its stuns are stuns, not freezes (Daze.cs)
            QueueSwing(s);
            return s;
        }

        /// Host: a perfect parry with the Arc Knuckles out arms the Counter Hammer (from ParryHit, Parry.cs).
        void ArmKnuckleCounter(Player p)
        {
            if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed)
                return;
            if (!p.KnuckleArmed)
                BroadcastSound(Sfx.KnuckleArm);
            p.KnuckleArmed = true;
            p.KnuckleArmedT = KnuckleArmTime;
        }

        /// Host, every tick: the chains, the Barrage's root, an unused Counter Hammer fading.
        void TickArcKnuckles(float dt)
        {
            foreach (var p in Players.Values)
            {
                TickKnuckleChain(p, dt);  // the Rocket stance's chain (ArcKnucklesChain.cs)
                if (p.Weapon.Id != WeaponId.ArcKnuckles)
                {
                    if (p.KnuckleMove != 0 || p.KnuckleRooted || p.KnuckleCharging)
                        ResetKnuckleChain(p);  // (the weapon was put away mid-combo)
                }
                if (!p.KnuckleArmed)
                    continue;
                if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed || (p.KnuckleArmedT -= dt) <= 0f)
                {
                    p.KnuckleArmed = false;
                    p.KnuckleArmedT = 0f;
                }
            }
            TickGrabRuns(dt);  // grabbed enemies carried and thrown (ArcKnucklesChain.cs)
        }

        /// Raising the parry, swapping stance or putting the weapon away: back to each stance's first move, a punch still winding up
        /// never lands, a charge is dropped, the root lets go. An armed Counter Hammer stays armed.
        void ResetKnuckleChain(Player p)
        {
            p.KnuckleMove = 0;
            p.KnuckleIdleT = 0f;
            p.KnuckleCharging = false;
            p.KnuckleChargeT = 0f;
            p.KnuckleCharge = 0;
            p.KnuckleRootT = 0f;
            p.KnuckleRooted = false;
            p.KnuckleBufT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Knuckle != 0 && s.Delay > 0f);
            EndKnuckleChain(p);  // (and the Rocket chain ends; ArcKnucklesChain.cs)
        }

        // --- The Rocket Dash (the Rocket stance's first move) --------------------------------------------------------------

        int knuckleDashesSeen = -1;
        float knuckleSwapLocalCd;  // your own game: a moment before the key swaps again

        /// The enemy a Rocket Dash from `from` locks onto: of those within KnuckleLockRange of you, the one closest to the cursor
        /// (`cursor`), or null (the dash then goes straight along your aim). Your own game asks too, to show the reticle.
        public Enemy KnuckleLockTarget(V2 from, V2 cursor, float reachMult = 1f)
        {
            Enemy best = null;
            float bestD = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(from, KnuckleLockRange * reachMult + e.Radius))
                    continue;
                float d = e.Pos.DistanceTo(cursor);
                if (d < bestD)
                {
                    bestD = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host: the Rocket Dash toward `aimAt`. It locks onto the enemy nearest the cursor (KnuckleLockTarget) and flies you up to it,
        /// fist first and untouchable; the chain's moves then turn to it too.
        void HostRocketDash(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.ArcKnuckles || p.Downed || p.StunT > 0f || p.ParryActive || p.SwallowT > 0f || State == MatchState.GameOver)
                return;
            V2 from = p.Pos, toward = aimAt - from;
            float far = p.Stats.KnuckleDashMult;  // Live Wire: the dash reaches farther
            var lockOn = KnuckleLockTarget(from, aimAt, far);
            float aim = lockOn != null ? (lockOn.Pos - from).Angle : toward.Length > 1f ? toward.Angle : p.Aim;
            V2 dir = V2.FromAngle(aim);
            float len = lockOn != null ? Math.Max(0f, Math.Min(KnuckleDashMax * far, lockOn.Pos.DistanceTo(from) - lockOn.Radius - p.Def.BodyR - KnuckleDashStop)) : KnuckleDashLen * far;
            V2 to = ClampToArena(from + dir * len, p.Def.BodyR);
            float time = KnuckleMoveTo(p, to);
            p.KnuckleTarget = lockOn != null ? lockOn.Id : 0;
            p.InvulnT = Math.Max(p.InvulnT, time + KnuckleDashGrace);
            p.FireCd = Math.Max(p.FireCd, time);  // (no punches mid-flight)
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Knuckle != 0 && s.Delay > 0f);  // a punch still winding up never lands
            var hit = NewSwing(p, aim, 0f, 0f, p.Weapon.Damage * KnuckleDashMult * p.Stats.DmgMult);
            hit.Reach = to.DistanceTo(from) + MeleeReach(p, KnuckleDashFist) + (lockOn != null ? lockOn.Radius + 20f : 0f);  // the lane, out to the fist where you stop
            hit.HalfWidth = KnuckleDashWidth;
            hit.FromSet = true;
            hit.From = from;
            hit.MaxTargets = 1;  // the first enemy in the way...
            hit.OnlyEnemy = lockOn != null ? lockOn.Id : 0;  // ...or the one it locked onto
            hit.Knock = KnuckleDashKnock;
            hit.Thrust = true;
            hit.Heavy = true;
            hit.NoFx = true;
            hit.Knuckle = 7;
            hit.Delay = time * KnuckleDashLandAt;
            QueueSwing(hit);
            BroadcastFx(FxKind.KnuckleDash, from, to, ArcEmber, p.Id, time + KnuckleDashFxAfter, Sfx.KnuckleRocket);
        }

        /// Host: moves `p` to `to` the way every Arc Knuckles dash and lunge does (your own game flies you there when the count
        /// changes; LocalKnuckleDash). Returns how long the flight takes.
        float KnuckleMoveTo(Player p, V2 to)
        {
            p.KnuckleDashes++;
            p.KnuckleDashTo = to;
            return Math.Max(KnuckleDashMinTime, to.DistanceTo(p.Pos) / KnuckleDashSpeed);
        }

        /// Host: the dash's hit landed (from LandSwing): the fist's crack and burst on what it hit, and the Flurry is next. A dash
        /// that hit nothing can't be thrown again for a moment.
        void KnuckleDashLanded(Player p, System.Collections.Generic.List<Enemy> hits)
        {
            if (hits.Count == 0)
            {
                p.FireCd = Math.Max(p.FireCd, KnuckleDashMiss);
                return;
            }
            var e = hits[0];
            BroadcastFx(FxKind.Ring, e.Pos, ArcEmber, e.Radius + 22f, 0.3f, Sfx.None);
            KnuckleHitCue(p, 7, hits.Count, e.Pos);  // the fist lands: its crack, your hit-pause and shake
            OpenKnuckleChain(p, e);  // the next click is the Flurry (ArcKnucklesChain.cs)
        }

        /// Host: a punch of `kind` connected with `hits` enemies, the first at `at`. Sent as an effect (FxKind.KnuckleHit): every
        /// screen throws its sparks; the puncher's own game holds the picture and shakes, sized to the punch (Game/Juice.cs,
        /// KnuckleHitFeel). It carries the contact sound (the slams have their own). Kinds: 6 the Counter Hammer; the Rocket stance's
        /// 7 Rocket Dash, 8 Flurry jab, 9 Rocket Punch, 10 the Flurry's straight, 11 a thrown enemy landing; the Brawl stance's
        /// 12 Pull Punch, 13 Drive Punch, 14 Pin Punch, 15 a Barrage jab, 16 the Barrage's straight, 17 the Quake Punch, 18 a Wall Slam.
        void KnuckleHitCue(Player p, int kind, int hits, V2 at)
        {
            bool heavy = KnuckleHeavyHit(kind);
            Sfx sound = kind == 6 || kind == 17 ? Sfx.None : heavy ? Sfx.KnuckleImpactHeavy : Sfx.KnuckleImpact;
            BroadcastFx(FxKind.KnuckleHit, at, new V2(p.Id, kind), kind == 6 || kind == 18 ? ArcGold : kind >= 7 && kind <= 11 ? ArcEmber : ArcBlue, hits, 0.25f, sound);
        }

        /// The punches that hit hard (a heavy knock, more sparks, a longer hold).
        public static bool KnuckleHeavyHit(int kind) => kind == 6 || kind == 7 || kind == 9 || kind == 10 || kind == 11 || kind == 14 || kind == 16 || kind == 17 || kind == 18;

        /// Your own game, every tick: a dash or lunge the host started moves you (when the count changes); the key swaps stance.
        /// Returns true while the Arc Knuckles are out (nothing else uses the key then).
        bool LocalKnuckleDash(Player me, V2 aimAt, bool pressed, float dt)
        {
            knuckleSwapLocalCd = Math.Max(0f, knuckleSwapLocalCd - dt);
            bool knuckles = me.Weapon.Id == WeaponId.ArcKnuckles;
            if (!knuckles || knuckleDashesSeen < 0)
                knuckleDashesSeen = me.KnuckleDashes;  // (picking the weapon up never replays an old count)
            if (me.KnuckleDashes != knuckleDashesSeen)
            {
                knuckleDashesSeen = me.KnuckleDashes;
                if (!me.Downed && me.StunT <= 0f)
                {
                    V2 d = me.KnuckleDashTo - me.Pos;  // where the host's dash ends
                    float len = d.Length;
                    if (len > 1f)
                    {
                        dashSpeed = Math.Max(KnuckleDashSpeed, len / KnuckleDashMaxTime);
                        DashDir = d / len;
                        DashT = len / dashSpeed;
                    }
                }
            }
            if (!knuckles)
                return false;
            if (pressed && knuckleSwapLocalCd <= 0f && !me.Downed)
            {
                knuckleSwapLocalCd = 0.25f;
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.KnuckleStance, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.KnuckleStance);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        // --- Network: the player's Arc Knuckles fields (SyncPlayer) ---

        void SyncArcKnuckles(NetSync s, Player p)
        {
            s.Bool(ref p.KnuckleArmed);
            s.Byte(ref p.KnuckleDashes);
            s.Byte(ref p.KnuckleChain);
            s.Vec(ref p.KnuckleDashTo);   // where the Rocket Dash ends, and what it locked onto (the chain turns to it)
            s.Int(ref p.KnuckleTarget);
            s.Byte(ref p.KnuckleStance);  // the Arc Knuckles' stance, the Brawl move next, the Barrage's root and the Quake Punch's charge (ArcKnucklesBrawl.cs)
            s.Byte(ref p.KnuckleMove);
            s.Bool(ref p.KnuckleRooted);
            s.Byte(ref p.KnuckleCharge);  // the dash chain's stage: your own game roots you and shows the Fire prompt  // Rocket Dashes started: your own game dashes when it changes  // the Arc Knuckles' Counter Hammer is armed
        }

        /// WeaponDef.BotAlt (BotWeaponAlt.cs): Alt swaps stance: between combos it takes turns, Rocket to dash in on a target a way off, Brawl again once the chain is done
        static bool ArcKnucklesBotAlt(Game g, AltView v, float dt, bool late, ref bool firing, ref V2 aimAt)
        {
            var me = v.Me;
            return !late && !me.Parrying && v.ShootAt != null && g.Tap(me.KnuckleStance == 0 ? me.KnuckleMove == 0 && v.ShootD > 90f : me.KnuckleChain == 0, 5f);
        }
    }
}

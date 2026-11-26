using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Nunchaku (DPS Ninja, the fourth weapon: Choice 3; Docs/Nunchaku.md, Docs/Mockups/ninja-nunchaku.html, Twin Storm). A chuck
    /// in each hand; the motion is NunchakuMotion.cs, which the hits below are timed from.
    ///   - Fire: Snap (the right chuck whips straight ahead), Backhand (the left sweeps back across), Double Figure-8 (both cross in
    ///     front twice: four hits), Twin Crush (both spin overhead and slam: a shockwave that stuns), then it starts over. The host
    ///     counts the steps in Player.Combo's low bits, as the Twin Blades do; NunChainWindow without a step and it starts over.
    ///   - Alt (the weapon-action key, Msg.WeaponDash WeaponAlt.NunGuard): the Twin Windmill guard, and again to leave it (Player.NunGuard).
    ///     Both chucks spin a disc at each side in front of you; small enemy shots that touch one are swatted back at the nearest
    ///     enemy as yours (each counts towards the counter, up to NunSwatCap: Player.NunSwats). You move slower while it's up.
    ///     Fire in the guard is the Twin Counter: both chucks lash straight ahead, harder for every shot swatted, which it uses up.
    ///   - Raising the parry sends the combo back to the Snap (ResetAttackChains) and takes back a step still winding up.
    /// Each step is one effect (FxKind.NunStep, Radius = id * 10 + step, 5 for the counter), drawn from its age; the counter's lash is
    /// FxKind.NunCounter (Game/NunchakuLook.cs).
    public partial class Game
    {
        public const float NunChainWindow = 1f;
        /// Each step's damage against the weapon's Damage (20): Snap 20, Backhand 22, each of the Figure-8's four hits 12, the Crush 68.
        public static readonly float[] NunMult = { 1f, 1.1f, 0.6f, 3.4f };
        /// When each step's hits land, as a share of its Dur (the swinging stick trails the handle by NunchakuMotion.Lag).
        public const float NunSnapAt = 0.55f, NunBackAt = 0.45f, NunFigAt1 = 0.26f, NunFigAt2 = 0.5f, NunCrushAt = 0.5f, NunCounterAt = 0.45f;
        public const float NunSnapReach = 95f, NunSnapWidth = 22f, NunBackReach = 90f, NunBackArc = 140f, NunFigReach = 85f, NunFigArc = 110f;
        public const float NunCrushAt0 = 40f, NunCrushR = 100f, NunCrushKnock = 38f, NunCrushStun = 0.7f, NunAftershockOut = 40f, NunAftershockMult = 0.6f, NunAftershockDelay = 0.2f;
        /// The guard: how much slower you move, each disc's reach, a swatted shot's damage and speed, how far it looks for an enemy.
        public const float NunGuardSlow = 0.75f, NunDiscR = 46f, NunSwatDmg = 6f, NunSwatSpeed = 560f, NunSwatSeek = 420f, NunSwitchCd = 0.3f;
        public const int NunSwatCap = 8;
        /// The discs' contact: each enemy touching one takes this much of the weapon's damage (20 x 0.3 = 6) once every NunDiscGap s and is shoved this far out.
        public const float NunDiscMult = 0.3f, NunDiscGap = 0.3f, NunDiscKnock = 36f;
        /// The Twin Counter: its line (reach and half width), damage (base and per swat, against the weapon's 20), and the wait after.
        public const float NunCounterReach = 175f, NunCounterWidth = 22f, NunCounterBase = 20f, NunCounterPer = 6f, NunCounterCd = 0.55f;
        static readonly Sfx[] NunStepSounds = { Sfx.NunSnap, Sfx.NunBack, Sfx.NunFig8, Sfx.NunCrush };
        public static readonly Rgba NunCrimson = new Rgba(1f, 0.3f, 0.37f);

        float nunSwitchLocalCd;  // your own game: until the key switches again (the host checks its own)

        /// The step a Combo value holds (0: the next press is the Snap).
        public static int NunStepOf(int combo) => combo & 7;

        /// Where a guard disc spins (`right`: the right hand's), for a player aiming `aim` from `pos`.
        public static V2 NunDiscAt(V2 pos, float aim, bool right) =>
            pos + V2.FromAngle(aim) * NunchakuMotion.GuardX + V2.FromAngle(aim + MathX.Pi / 2f) * (right ? NunchakuMotion.GuardY : -NunchakuMotion.GuardY);

        /// A disc's reach for this player (Iron Links: wider).
        public static float NunDisc(Player p) => NunDiscR * p.Stats.NunDiscMult;

        /// What the Twin Counter deals now, before damage upgrades: Damage * (20 + 6 a swat) / 20 (Iron Links: +2 a swat).
        public static float NunCounterDamage(Player p) =>
            p.Weapon.Damage * (NunCounterBase + (NunCounterPer + p.Stats.NunSwatBonus) * Math.Min(NunSwatCap, p.NunSwats)) / NunCounterBase;

        /// Host: one press of the primary with the Nunchaku (from FireNinjaWeapon): the next step, or the Twin Counter from the guard.
        void FireNunchaku(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            if (p.NunGuard)
            {
                var c = NunchakuMotion.Counter;
                var s = NunSwing(p, aim, NunCounterReach, 0f, NunCounterDamage(p) * dmg, c.Dur * NunCounterAt, Sfx.None);
                s.HalfWidth = NunCounterWidth;
                s.Thrust = true;
                s.Heavy = true;
                p.FireCd = NunCounterCd / FireRate(p);
                p.NunAttackT = c.Dur;  // the discs are down while the chucks lash out (TickNunchaku)
                BroadcastFx(FxKind.NunStep, p.Pos, p.Pos + V2.FromAngle(aim) * NunCounterReach, NunCrimson, p.Id * 10 + 5, c.Dur + NunchakuMotion.Recover, Sfx.NunCounter);
                BroadcastFx(FxKind.NunCounter, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, NunCounterReach), NunCrimson, Math.Min(NunSwatCap, p.NunSwats), c.Dur * NunCounterAt + 0.35f, Sfx.None);
                p.NunSwats = 0;
                return;
            }
            int last = NunStepOf(p.Combo);
            int step = last >= 1 && last < 4 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            var d = NunchakuMotion.Steps[step - 1];
            p.ComboT = d.Dur + NunChainWindow;
            p.FireCd = d.Dur * d.Next / FireRate(p);
            float hit = w.Damage * dmg * NunMult[step - 1];
            switch (step)
            {
                case 1:
                {
                    var s = NunSwing(p, aim, NunSnapReach, 0f, hit, d.Dur * NunSnapAt, Sfx.None);
                    s.HalfWidth = NunSnapWidth;
                    s.Thrust = true;
                    break;
                }
                case 2:
                    NunSwing(p, aim, NunBackReach, NunBackArc, hit, d.Dur * NunBackAt, Sfx.None);
                    break;
                case 3:
                    for (int k = 0; k < 4; k++)  // each crossing: both chucks
                        NunSwing(p, aim, NunFigReach, NunFigArc, hit, d.Dur * (k < 2 ? NunFigAt1 : NunFigAt2) + 0.01f * (k & 1), Sfx.None);
                    break;
                default:
                {
                    float reachMult = MeleeReach(p, NunSnapReach) / NunSnapReach;  // Reach upgrades widen the shockwave too
                    for (int k = 0; k <= extra; k++)  // each extra projectile: an aftershock ring, further out
                    {
                        float at = NunCrushAt0 + NunAftershockOut * k;
                        var s = NunSwing(p, aim, at, 0f, hit * (k == 0 ? 1f : NunAftershockMult), d.Dur * NunCrushAt + NunAftershockDelay * k, Sfx.None);
                        s.Circle = NunCrushR * reachMult;
                        s.CircleAt = at;
                        s.Knock = NunCrushKnock;
                        s.Stun = NunCrushStun;
                        s.StarStun = true;  // a stun shows stars, not ice (Daze.cs)
                        s.Power = 0.8f;
                        s.Heavy = true;
                    }
                    break;
                }
            }
            // The whole step, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.NunStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, w.Reach), NunCrimson, p.Id * 10 + step,
                d.Dur + NunchakuMotion.Recover + (step == 4 ? NunAftershockDelay * extra : 0f), NunStepSounds[step - 1]);  // (timed so its crack lands with the hit)
        }

        /// One hit of a step, landing `delay` after the press; `sound` plays as it lands.
        Swing NunSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, Sfx sound)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it
            s.Sound = sound;
            s.Twin = true;  // (the parry takes back one still winding up: ResetNunChain)
            s.Nun = true;  // its hits sound Sfx.NunHit
            QueueSwing(s);
            return s;
        }

        /// Raising the parry cancels the combo (ResetAttackChains): back to the Snap, a step still winding up never lands. The guard stays.
        void ResetNunChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Twin && s.Delay > 0f);
        }

        /// Host: the weapon-action key with the Nunchaku: the combo to the Twin Windmill guard, or back.
        void HostNunSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.Nunchaku || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.NunSwitchCd > 0f || State == MatchState.GameOver)
                return;
            p.NunSwitchCd = NunSwitchCd - 0.05f;  // (slack for a guest's trip, which keeps the real cooldown)
            p.NunGuard = !p.NunGuard;
            p.Combo &= ~7;  // either way the combo starts over at the Snap
            p.ComboT = 0f;
            BroadcastFx(FxKind.Ring, p.Pos, NunCrimson, 34f, 0.3f, p.NunGuard ? Sfx.NunGuard : Sfx.SwingLight);
        }

        readonly List<int> nunSwatted = new List<int>();
        readonly List<Enemy> nunTouched = new List<Enemy>();
        readonly List<int> nunCooled = new List<int>();

        /// Host: the spinning discs hit what touches them (NunDiscMult of the weapon's damage, each enemy once every NunDiscGap s) and shove
        /// it straight out from the disc's centre (not bosses or heavies).
        void DiscContact(Player p, V2 dR, V2 dL, float r, float dt)
        {
            var hitAt = p.NunDiscHit;
            nunCooled.Clear();
            foreach (var kv in hitAt)
                if (kv.Value - dt <= 0f)
                    nunCooled.Add(kv.Key);
            foreach (int id in nunCooled)
                hitAt.Remove(id);
            foreach (int id in new List<int>(hitAt.Keys))
                hitAt[id] -= dt;
            nunTouched.Clear();
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                if (e.Pos.CloserThan(dR, r + e.Radius) || e.Pos.CloserThan(dL, r + e.Radius))
                {
                    if (!hitAt.ContainsKey(e.Id))
                        nunTouched.Add(e);
                }
            }
            foreach (var e in nunTouched)
            {
                hitAt[e.Id] = NunDiscGap;
                V2 c = e.Pos.DistanceSq(dR) < e.Pos.DistanceSq(dL) ? dR : dL;
                V2 away = AwayFrom(e.Pos, c, V2.FromAngle(p.Aim));
                WeaponHurt(e, p.Weapon.Damage * NunDiscMult * p.Stats.DmgMult, p.Id);
                Daze(e);  // a melee hit: it can't hit back for a split second (Daze.cs)
                if (!Heavy(e))
                    KnockEnemy(e, away * NunDiscKnock);
                AddFx(FxKind.Spark, e.Pos - away * e.Radius, NunCrimson, 10f, 0.22f);
            }
            if (nunTouched.Count > 0)
                BroadcastSound(Sfx.NunHit);
        }

        /// Host, every tick for every player: the guard drops with what held it, and its discs swat shots back.
        void TickNunchaku(Player p, float dt)
        {
            if (p.Weapon.Id != WeaponId.Nunchaku)
            {
                p.NunGuard = false;
                p.NunSwats = 0;
                return;
            }
            p.NunSwitchCd = Math.Max(0f, p.NunSwitchCd - dt);
            if (!p.NunGuard)
            {
                p.NunDiscHit.Clear();
                return;
            }
            if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || State == MatchState.GameOver)
            {
                p.NunGuard = false;  // (the guard drops with what held it)
                return;
            }
            if (p.NunAttackT > 0f)
            {
                if ((p.NunAttackT -= dt) <= 0f)
                    BroadcastSound(Sfx.NunGuard);  // the counter is done: the windmill spins back up
                return;
            }  // mid-counter: the chucks are lashing out, not spinning, so nothing is swatted
            float r = NunDisc(p);
            V2 dR = NunDiscAt(p.Pos, p.Aim, true), dL = NunDiscAt(p.Pos, p.Aim, false);
            DiscContact(p, dR, dL, r, dt);
            nunSwatted.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                if (b.Pos.CloserThan(dR, r + b.Radius) || b.Pos.CloserThan(dL, r + b.Radius))
                    nunSwatted.Add(b.Id);
            }
            foreach (int id in nunSwatted)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                Enemy target = NearestEnemy(b.Pos, NunSwatSeek, p.Id);
                V2 dir = target != null ? (target.Pos - b.Pos).Normalized() : (b.Vel.IsZero ? V2.FromAngle(p.Aim) : -b.Vel.Normalized());
                if (dir.IsZero)
                    dir = V2.FromAngle(p.Aim);
                KillBullet(id);
                SpawnBullet(Defs.ArcaneShot, b.Pos, dir * NunSwatSpeed, 0f, 1f, NunSwatDmg * p.Stats.DmgMult, p.Id);
                AddFx(FxKind.Spark, b.Pos, NunCrimson, 9f, 0.22f);
                p.NunSwats = Math.Min(NunSwatCap, p.NunSwats + 1);
            }
            if (nunSwatted.Count > 0)
                BroadcastSound(Sfx.NunSwat);
        }

        /// Your own game, every tick: the weapon-action key switches the guard (the host checks the rest). Returns true while the
        /// Nunchaku is out (nothing else uses the key then).
        bool LocalNunGuard(Player me, V2 aimAt, bool pressed, float dt)
        {
            nunSwitchLocalCd = Math.Max(0f, nunSwitchLocalCd - dt);
            if (me.Weapon.Id != WeaponId.Nunchaku)
                return false;
            if (!pressed || nunSwitchLocalCd > 0f || me.Downed || me.StunT > 0f)
                return true;
            nunSwitchLocalCd = NunSwitchCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.NunGuard, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.NunGuard);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        // --- Network: the player's Nunchaku fields (SyncPlayer) ---

        void SyncNunchaku(NetSync s, Player p)
        {
            s.Bool(ref p.NunGuard);  // the Nunchaku
            s.Byte(ref p.NunSwats);
        }
    }
}

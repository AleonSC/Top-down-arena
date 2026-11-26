using System;

namespace Arena
{
    /// The Revolver's Fan stance (Healer Soldier; Docs/Mockups/revolver-rework.html). The weapon-action key switches between the
    /// Gunslinger stance (Weapons.cs RevolverTrigger: click to fire, click to pull the hammer) and the Fan stance, where Fire is
    /// a two-step combo:
    ///   - a click is the Pistol Whip: the gun swung across a 110° arc in front of you (120 px) that stuns everything it hits
    ///     for WhipStun (not bosses) and cuts small enemy shots (a Knight swing, Knight.cs, drawn by Game/PistolWhipLook.cs);
    ///   - a click within RevLink of the swing is Fan the Hammer: RevFanRounds rounds in a 30° cone, RevFanGap apart, hitting
    ///     stunned enemies RevFanStunMult harder; then the hammer needs RevFanReload before it fans again.
    /// Holding the parry and clicking fans at any time (the one shot you can take with the shield up). Raising the parry
    /// after a whip starts the combo over (the next click whips again), and cuts a fan you started with a click.
    /// Your game sends Msg.WeaponDash (WeaponAlt.RevStance) for the switch; the host keeps the step (replicated).
    public partial class Game
    {
        public const byte RevIdle = 0, RevWhip = 1, RevLinked = 2, RevFan = 3;  // Player.RevStep
        public const float WhipReach = 120f, WhipArc = 110f;  // the arc's radius (px) and width (degrees)
        public const float WhipMult = 0.5f;                   // damage against a Gunslinger shot's
        public const float WhipKnock = 30f;                   // a short shove: the pack stays together for the fan
        public const float WhipStun = 1f;
        public const float WhipWind = 0.07f;                  // from the click to the hit (the gun is halfway across)
        public const float WhipSweep = 0.16f, WhipStep = 0.18f, WhipFxTime = 0.5f;  // the gun's sweep, the whole step, and its look with the fade
        public const float RevLink = 0.5f;                    // after the whip, a click this soon fans
        public const int RevFanRounds = 6;                       // + 2 per extra projectile
        public const float RevFanGap = 0.07f, RevFanCone = 30f, RevFanRange = 280f;
        public const float RevFanMult = 0.32f;                   // each round, against a Gunslinger shot
        public const float RevFanStunMult = 1.25f;               // against a stunned enemy
        public const float RevFanReload = 1.2f;                  // after a full fan (half after a cut one)
        public const float RevSwitchCd = 0.25f;
        public static readonly Rgba WhipColor = new Rgba(1f, 0.86f, 0.55f);

        float revSwitchLocalCd;

        public int FanRoundsOf(Player p) => RevFanRounds + 2 * ExtraShots(p);

        /// Can `p` shoot with the parry up? Only the Revolver's Fan stance (Fan the Hammer).
        public static bool FiresBehindShield(Player p) => p.Weapon != null && p.Weapon.Id == WeaponId.Revolver && p.RevStance == 1;

        /// Your game: the weapon-action key with the Revolver switches stance. True when the key belongs to it (pressed or not).
        bool LocalPistolWhip(Player me, bool pressed, V2 aimAt, float dt)
        {
            revSwitchLocalCd -= dt;
            if (me.Weapon.Id != WeaponId.Revolver)
                return false;
            if (pressed && revSwitchLocalCd <= 0f && !me.Downed && me.StunT <= 0f)
            {
                revSwitchLocalCd = RevSwitchCd;
                if (IsHost)
                    HostWeaponDash(MyId, WeaponAlt.RevStance, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(WeaponAlt.RevStance);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }

        /// Host: a player switched the Revolver's stance.
        void HostRevStance(Player p)
        {
            if (p.Weapon.Id != WeaponId.Revolver || p.Downed || p.StunT > 0f || State == MatchState.GameOver)
                return;
            p.RevStance = (byte)(p.RevStance == 0 ? 1 : 0);
            p.RevStep = RevIdle;
            p.RevT = 0f;
            p.RevFanLeft = 0;
            p.TapQueued = false;
            p.FireCd = Math.Max(p.FireCd, 0.12f);
            BroadcastSound(Sfx.SwingLight);
        }

        /// Host, every tick with the Fan stance: the combo's clock, the parry's reset, the fan's rounds, and the click kept in TapQueued.
        void RevFanStance(Player p, float dt)
        {
            p.RevT += dt;
            p.RevFanReload = Math.Max(0f, p.RevFanReload - dt);
            bool guard = p.ParryActive, raised = guard && !p.RevGuardWas;
            p.RevGuardWas = guard;
            if (raised)
            {
                if (p.RevStep == RevLinked || p.RevStep == RevWhip && p.RevT >= WhipWind)
                    p.RevStep = RevIdle;  // the combo starts over: the next click whips
                else if (p.RevStep == RevFan && !p.RevFanByParry)
                {
                    p.RevStep = RevIdle;  // a clicked fan, cut short
                    p.RevFanLeft = 0;
                    p.RevFanReload = RevFanReload * 0.5f;
                }
            }
            if (p.RevStep == RevWhip && p.RevT >= WhipStep)
            {
                p.RevStep = RevLinked;
                p.RevT = 0f;
            }
            else if (p.RevStep == RevLinked && p.RevT >= RevLink)
                p.RevStep = RevIdle;
            else if (p.RevStep == RevFan)
            {
                for (p.RevFanNext -= dt; p.RevFanLeft > 0 && p.RevFanNext <= 0f; p.RevFanNext += RevFanGap / FireRate(p))
                    FanRound(p);
                if (p.RevFanLeft <= 0)
                {
                    p.RevStep = RevIdle;
                    p.RevFanReload = RevFanReload / FireRate(p) * p.Stats.RevolverCockMult;
                }
            }
            if (!p.TapQueued)
                return;
            if (p.Downed || p.StunT > 0f || p.SwallowT > 0f || State == MatchState.GameOver || p.RevStep == RevFan)
            {
                p.TapQueued = false;
                return;
            }
            if (p.RevStep == RevWhip && p.RevT < WhipStep)
                return;  // kept: it fans as the swing ends (or whips again when the fan isn't ready)
            if (p.FireCd > 0f)
                return;  // kept (just switched stance)
            p.TapQueued = false;
            if (guard)
            {
                if (p.RevFanReload <= 0f)
                    StartFan(p, true);
            }
            else if (p.RevStep == RevLinked && p.RevFanReload <= 0f)
                StartFan(p, false);
            else
                PistolWhip(p);
        }

        /// The Pistol Whip: a Knight swing with its own look, landing WhipWind after the click.
        void PistolWhip(Player p)
        {
            p.RevStep = RevWhip;
            p.RevT = 0f;
            p.SwingCount++;
            float aim = p.Aim;
            var s = NewSwing(p, aim, WhipReach, WhipArc, p.Weapon.Damage * WhipMult * p.Stats.DmgMult);
            s.Knock = WhipKnock;
            s.Stun = WhipStun;
            s.StarStun = true;        // stars, not ice (Daze.cs)
            s.NoFx = true;            // its look is FxKind.PistolWhip, sent now
            s.Sound = Sfx.SwingHeavy;
            s.Delay = WhipWind;
            QueueSwing(s);
            BroadcastFx(FxKind.PistolWhip, p.Pos, p.Pos + V2.FromAngle(aim) * s.Reach, WhipColor, p.Id, WhipFxTime, Sfx.None);  // (Radius = their id)
        }

        void StartFan(Player p, bool byParry)
        {
            p.RevStep = RevFan;
            p.RevT = 0f;
            p.RevFanByParry = byParry;
            p.RevFanLeft = FanRoundsOf(p);
            p.RevFanNext = 0f;
            p.RevFanAim = p.Aim;
        }

        /// One round of Fan the Hammer, spread evenly across the cone (left to right) with a little jitter.
        void FanRound(Player p)
        {
            int n = FanRoundsOf(p), i = n - p.RevFanLeft;
            p.RevFanLeft--;
            float cone = RevFanCone * MathX.Pi / 180f;
            float aim = p.RevFanAim + (n <= 1 ? 0f : (i / (n - 1f) - 0.5f) * cone) + Rand(-0.03f, 0.03f);
            var w = p.Weapon;
            V2 dir = V2.FromAngle(aim);
            firingWeapon = true;
            var b = SpawnBullet(Defs.HealerShot, p.Pos + dir * p.Def.BodyR, dir * w.Speed, 0f, RevFanRange / w.Speed, w.Damage * RevFanMult * p.Stats.DmgMult, p.Id);
            b.Radius = 4.5f * p.Stats.ShotSizeMult;
            b.FanRound = true;
            firingWeapon = false;
        }

        /// A Fan the Hammer round against a stunned enemy hits RevFanStunMult harder.
        float FanStunBonus(Bullet b, Enemy e) => b.FanRound && e.StunT > 0f ? RevFanStunMult : 1f;
    }
}

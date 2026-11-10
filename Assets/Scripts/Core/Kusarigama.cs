using System;

namespace Arena
{
    /// The Kusarigama (Tank Ninja; Docs/Mockups/kusarigama-combo.html).
    ///   - Combo: the primary button cuts with the sickle (a 144 degree arc, KusariCutReach out), then, if you press again within
    ///     KusariWindow, throws the iron weight. The weight spins up, flies out to the first enemy in its lane, bites it (stun) and
    ///     hauls it to your feet; the next cut lands on it. Player.KusariStep says which piece landed last.
    ///   - Whirl (the weapon-action key, Left Alt, held): the weight circles you, its orbit growing to KusariOrbitMax. Everything within
    ///     KusariPull is dragged toward you (never into you; bosses and heavies don't budge) and the chain cuts what it sweeps through.
    ///     You move at KusariWhirlSlow and can't swing. Let go, or run out of KusariWhirlMax, and the chain snaps in: a shockwave
    ///     that knocks back and stuns. Your game sends Msg.WeaponDash (DashWhirlOn / DashWhirlOff); the host does the rest.
    ///   - The throw is state, not an effect: HookId, HookT and KusariAim are replicated and the screen draws the weight from them
    ///     (Game/KusarigamaLook.cs); the host's timing is KusariThrowTime with the weight landing at KusariImpactAt.
    public partial class Game
    {
        public const float KusariCutReach = 96f, KusariCutArc = 144f, KusariCutKnock = 18f, KusariCutStun = 0.12f;
        public const float KusariCutMult = 16f / 26f, KusariThrowMult = 34f / 26f, KusariExtraDmg = 0.2f;  // damage against the weapon's own (26): 16 and 34
        public const float KusariCutTime = 0.30f, KusariThrowTime = 0.65f, KusariWindow = 1f;  // each piece holds the next press off this long; a cut's chance to be followed
        public const float KusariWindUp = 0.1f, KusariImpactAt = 0.26f, KusariDragFrom = 0.31f, KusariDragTo = 0.55f;  // seconds into the throw: the spin ends, the weight lands, the drag starts and ends
        public const float KusariHalfWidth = 16f, KusariThrowStun = 0.4f, KusariDragStop = 24f;  // the lane's half width; the stun; the gap left between you and what it dragged in

        public const float KusariWhirlDmg = 12f / 26f, KusariWhirlGap = 0.12f;  // a hit's damage against the weapon's own; how soon the chain can hit the same enemy again
        public const float KusariSpinUp = 0.35f, KusariOmega = 14f, KusariOrbitMin = 70f, KusariOrbitMax = 150f;  // seconds to full speed, radians a second, the orbit's radius at the start and at full speed
        public const float KusariPull = 200f, KusariPullSpeed = 240f, KusariSwirl = 80f, KusariWhirlSlow = 0.55f;  // the pull's reach and speed, how fast it swirls what it holds, your speed
        public const float KusariWhirlMax = 3f, KusariWhirlCd = 3f, KusariSnapMin = 0.3f;  // the longest whirl, the wait before the next, the shortest whirl that snaps
        public const float KusariSnapRadius = 110f, KusariSnapDmg = 30f, KusariSnapStun = 0.6f, KusariSnapKnock = 60f;
        public static readonly Rgba KusariColor = new Rgba(0.7f, 0.64f, 1f);

        /// The weight's orbit while whirling, for a spin-up of 0 to 1.
        public static float KusariOrbit(float spin)
        {
            float k = 1f - Math.Min(1f, Math.Max(0f, spin));
            return KusariOrbitMin + (KusariOrbitMax - KusariOrbitMin) * (1f - k * k * k);
        }

        /// How far the weight is thrown (the weapon's reach with its upgrades).
        public float KusariReachOf(Player p) => MeleeReach(p, p.Weapon.Reach + p.Weapon.Grow * ExtraShots(p));

        /// The primary button: the sickle cut, then the weight throw.
        void FireKusarigama(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            dmg *= 1f + KusariExtraDmg * extra;
            bool second = p.KusariStep == 1 && p.KusariGap <= KusariWindow;
            p.KusariGap = 0f;
            if (!second)
            {
                p.KusariStep = 1;
                p.FireCd = KusariCutTime / FireRate(p);
                var s = NewSwing(p, aim, KusariCutReach, KusariCutArc, w.Damage * dmg * KusariCutMult);
                s.Knock = KusariCutKnock;
                s.Stun = KusariCutStun;
                s.NoCut = true;  // a chain, not a blade: it doesn't cut bullets
                pendingSwings.Add(s);
                return;
            }
            p.KusariStep = 2;
            p.FireCd = KusariThrowTime / FireRate(p);
            StartKusariThrow(p, aim, w.Reach + w.Grow * extra, w.Damage * dmg * KusariThrowMult);
        }

        /// The throw leaves: its target is the first enemy in the lane (it can still be blocked or die before the weight lands).
        void StartKusariThrow(Player p, float aim, float reach, float dmg)
        {
            reach = MeleeReach(p, reach);
            V2 dir = V2.FromAngle(aim), side = dir.Orthogonal();
            Enemy first = null;
            float firstAlong = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                V2 rel = e.Pos - p.Pos;
                float along = Dot(rel, dir), across = Math.Abs(Dot(rel, side));
                if (along < 0f || along > reach + e.Radius || across > KusariHalfWidth + e.Radius || along >= firstAlong)
                    continue;
                first = e;
                firstAlong = along;
            }
            p.Run.ShotsFired++;
            p.KusariAim = aim;
            p.KusariDmg = dmg;
            p.KusariHit = false;
            p.HookId = first != null ? first.Id : 0;
            p.HookT = KusariThrowTime;
            BroadcastSound(Sfx.SwingLight);
        }

        /// Host: a player pressed (on = true) or let go of the weapon-action key with the Kusarigama.
        void HostKusariWhirl(Player p, bool on)
        {
            if (p.Weapon.Id != WeaponId.Kusarigama || State == MatchState.GameOver)
                return;
            if (!on)
            {
                if (p.KusariWhirl)
                    EndKusariWhirl(p);
                return;
            }
            if (p.KusariWhirl || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || p.KusariLock > 0f)
                return;
            EndKusariThrow(p);  // (the weight comes back to start spinning)
            p.KusariWhirl = true;
            p.KusariHeld = 0f;
            p.KusariAng = p.Aim;
            p.KusariStep = 0;
            BroadcastSound(Sfx.SwingLight);
        }

        void EndKusariThrow(Player p)
        {
            p.HookT = 0f;
            p.HookId = 0;
            p.KusariHit = false;
        }

        /// The whirl ends: the chain snaps in (when it ran long enough to count and you weren't taken out of it).
        void EndKusariWhirl(Player p)
        {
            bool snap = p.KusariHeld >= KusariSnapMin && !p.Downed && p.StunT <= 0f && p.SwallowT <= 0f;
            p.KusariWhirl = false;
            p.KusariHeld = 0f;
            p.KusariLock = KusariWhirlCd;
            p.KusariGap = 5f;
            if (!snap)
                return;
            p.FireCd = Math.Max(p.FireCd, 0.3f / FireRate(p));
            var s = NewSwing(p, p.Aim, KusariSnapRadius, 360f, p.Weapon.Damage * p.Stats.DmgMult * KusariSnapDmg / 26f);
            s.Circle = KusariSnapRadius;
            s.CircleAt = 0f;
            s.Stun = KusariSnapStun;
            s.Knock = KusariSnapKnock;
            s.Heavy = true;
            s.NoCut = true;
            s.NoFx = true;  // drawn here, not as a slash
            pendingSwings.Add(s);
            BroadcastFx(FxKind.Ring, p.Pos, new Rgba(1f, 1f, 1f), KusariSnapRadius, 0.3f, Sfx.None);
            BroadcastFx(FxKind.Ring, p.Pos, KusariColor, KusariSnapRadius * 0.8f, 0.4f, Sfx.None);
        }

        /// Host, every tick: the combo forgets its place, the throw plays out, the whirl pulls and cuts.
        void TickKusari(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.KusariGap = Math.Min(p.KusariGap + dt, 5f);
                p.KusariLock = Math.Max(0f, p.KusariLock - dt);
                if (p.Weapon.Id != WeaponId.Kusarigama || p.Downed)
                {
                    p.KusariStep = 0;
                    p.KusariWhirl = false;
                    p.KusariHeld = 0f;
                    EndKusariThrow(p);
                    continue;
                }
                if (p.KusariStep != 0 && p.KusariGap > KusariWindow)
                    p.KusariStep = 0;
                if (p.HookT > 0f)
                    TickKusariThrow(p, dt);
                if (p.KusariWhirl)
                    TickKusariWhirl(p, dt);
            }
        }

        void TickKusariThrow(Player p, float dt)
        {
            float before = KusariThrowTime - p.HookT;
            p.HookT -= dt;
            float t = KusariThrowTime - p.HookT;
            if (p.HookT <= 0f)
            {
                EndKusariThrow(p);
                return;
            }
            if (before < KusariImpactAt && t >= KusariImpactAt)
                KusariImpact(p);
            Enemy e;
            if (!p.KusariHit || t < KusariDragFrom || t >= KusariDragTo || !Enemies.TryGetValue(p.HookId, out e) || e.Hp <= 0f)
                return;
            if (Heavy(e))
                return;  // a boss takes the bite but doesn't come
            float k = Math.Min(1f, (t - KusariDragFrom) / (KusariDragTo - KusariDragFrom));
            k = k * k * (3f - 2f * k);
            V2 rel = e.Pos - p.Pos;
            float cur = rel.Length, stop = e.Radius + p.Def.BodyR + KusariDragStop;
            float want = p.KusariD0 + (stop - p.KusariD0) * k;
            if (cur > 1f && want < cur)
                e.Pos = ClampToArena(p.Pos + rel / cur * Math.Max(want, stop), e.Radius);
            e.FrozenT = Math.Max(e.FrozenT, 0.12f);
        }

        /// The weight lands: its target is hurt and stunned, and the drag begins. A shield stops it; a miss just thuds.
        void KusariImpact(Player p)
        {
            Enemy e;
            V2 dir = V2.FromAngle(p.KusariAim);
            if (p.HookId == 0 || !Enemies.TryGetValue(p.HookId, out e) || e.Hp <= 0f)
            {
                p.HookId = 0;
                BroadcastFx(FxKind.Spark, p.Pos + dir * KusariReachOf(p), Steel, 10f, 0.2f, Sfx.None);
                return;
            }
            if (BlocksShot(e, p.Pos))
            {
                WearShield(e, p.KusariDmg);
                p.HookId = 0;
                return;
            }
            float dmg = p.KusariDmg * NinjaHitMult(p.Id, Defs.KnightSlash, e);
            p.Run.ShotsHit++;
            p.KusariHit = true;
            p.KusariD0 = e.Pos.DistanceTo(p.Pos);
            HurtEnemy(e, dmg, p.Id);
            OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
            if (e.Hp > 0f && !e.Def.IsBoss)
            {
                e.FrozenT = Math.Max(e.FrozenT, KusariThrowStun);
                e.SlowT = Math.Max(e.SlowT, KusariThrowStun);
            }
            p.MeleeGraceT = MeleeContactGrace;
            BroadcastFx(FxKind.Ring, e.Pos, KusariColor, e.Radius + 34f, 0.3f, Sfx.SwingHeavy);
        }

        void TickKusariWhirl(Player p, float dt)
        {
            if (p.StunT > 0f || p.SwallowT > 0f || p.ParryActive || State == MatchState.GameOver)
            {
                EndKusariWhirl(p);
                return;
            }
            p.KusariHeld += dt;
            if (p.KusariHeld >= KusariWhirlMax)
            {
                EndKusariWhirl(p);
                return;
            }
            float spin = Math.Min(1f, p.KusariHeld / KusariSpinUp);
            float sweep = KusariOmega * spin * dt, prev = MathX.Fmod(p.KusariAng, MathX.Tau), orbit = KusariOrbit(spin);
            p.KusariAng = MathX.Fmod(prev + sweep, MathX.Tau);
            bool hitAny = false;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e))
                    continue;
                e.KusariCd -= dt;
                V2 rel = e.Pos - p.Pos;
                float d = rel.Length;
                if (d < 1f)
                    continue;
                if (d < KusariPull && !Heavy(e))
                {
                    float stop = e.Radius + p.Def.BodyR + 14f;
                    V2 inward = rel / d * -1f, swirl = rel.Orthogonal() / d;
                    V2 at = e.Pos + swirl * (KusariSwirl * spin * dt);
                    if (d > stop)
                        at += inward * Math.Min(d - stop, KusariPullSpeed * spin * dt);
                    e.Pos = ClampToArena(at, e.Radius);
                }
                if (sweep <= 0f || spin < 0.5f || e.KusariCd > 0f || d > orbit + e.Radius)
                    continue;
                float margin = (float)Math.Asin(Math.Min(0.99f, e.Radius / d));
                float ahead = MathX.Fmod(rel.Angle - prev, MathX.Tau);
                if (ahead > sweep + margin && ahead < MathX.Tau - margin)
                    continue;
                e.KusariCd = KusariWhirlGap;
                if (BlocksShot(e, p.Pos))
                {
                    WearShield(e, p.Weapon.Damage * p.Stats.DmgMult * KusariWhirlDmg);
                    continue;
                }
                float dmg = p.Weapon.Damage * p.Stats.DmgMult * (1f + KusariExtraDmg * ExtraShots(p)) * KusariWhirlDmg * NinjaHitMult(p.Id, Defs.KnightSlash, e);
                hitAny = true;
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = rel / d, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                BroadcastFx(FxKind.Spark, p.Pos + rel / d * Math.Min(d, orbit), new Rgba(1f, 1f, 1f), 8f, 0.15f, Sfx.None);
            }
            if (hitAny)
                p.MeleeGraceT = MeleeContactGrace;  // you're in the thick of it: the bodies you've pulled in don't hurt
        }
    }
}

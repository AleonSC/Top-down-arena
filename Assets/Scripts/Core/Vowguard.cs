using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Vowguard (Healer Knight, the fourth weapon: Choice 3; Docs/Vowguard.md, mockup Docs/Mockups/vowguard.html): an
    /// arming sword and a heater shield with two stances, each playing a chain of up to ten steps the player builds from that
    /// stance's nine moves (VowguardMotion.cs; the lobby's chain editor, Game/VowguardUi.cs).
    ///   - Valor (stance 0): the sword forward, full speed; the shield only guards during shield moves. Links light Faith, one
    ///     per enemy they hit.
    ///   - Sanctuary (stance 1): the shield up front, VgSanctSlow speed; small enemy shots in a VgSanctHalf * 2 arc in front are
    ///     always stopped (not red aimed shots), each lighting Faith. Raise Guard, Deflect Sweep, Cover Ally and Bulwark Absorb
    ///     widen it while they play.
    ///   - Fire plays the next step of the stance you're in; a press counts once VgNext of a step has played; the chain starts
    ///     over VgChainWindow after a step ends without one, and after its last step. Hold Fire and it plays on.
    ///   - A finisher spends all Faith (up to VgFaithMax): +VgFaithDmg damage on its last hit and +VgFaithHeal on each heal, a Faith.
    ///   - Alt (the weapon-action key, Msg.WeaponDash WeaponAlt.VgStance) switches stance: each keeps its own place (Player.VgAtV,
    ///     VgAtS); the step playing stops. Into Sanctuary blocks at once; into Valor shoves with the rim.
    ///   - Raising the parry starts both chains over (ResetVgChain). A perfect parry fills Faith (ArmVgFaith).
    ///   - Shield Charge, Sidestep and Cover Ally move you: your own game does when Player.VgCount changes (LocalVowguard), to
    ///     Player.VgDashTo.
    ///   - Extra projectiles: every step hits harder and heals more (VgExtraBonus each).
    /// Each step is one effect (FxKind.VgStep: Pos the knight, Pos2 along the aim, Radius = id * 100 + the move, Dur = the move
    /// + VgFxTail), drawn from its age; an Aegis Dome is FxKind.VgDome for its life (Game/VowguardLook.cs).
    public partial class Game
    {
        public const float VgSanctSlow = 0.8f, VgSanctHalf = 50f * MathX.Pi / 180f, VgGuardGap = 12f, VgSwitchCd = 0.25f;
        public const float VgNext = 0.75f, VgChainWindow = 0.8f, VgFxTail = 0.25f, VgExtraBonus = 0.15f;
        public const int VgFaithMax = 6;
        public const float VgFaithDmg = 3f, VgFaithHeal = 2f, VgShoveDmg = 6f, VgShoveReach = 50f, VgShoveKnock = 40f, VgBlockSoundGap = 0.08f;
        public const float VgBaseDamage = 16f;  // the moves' damage is written against this (the weapon's Damage)
        public static readonly Rgba VgGold = new Rgba(1f, 0.81f, 0.35f), VgJade = new Rgba(0.5f, 0.89f, 0.75f), VgWardBlue = new Rgba(0.75f, 0.89f, 1f);
        public static readonly string[] VgStanceNames = { "Valor", "Sanctuary" };

        float vgSwitchLocalCd;  // your own game: until the key switches again
        int vgCountSeen = -1;   // your own game: the last step it moved you for
        float vgMoveWait = -1f; // your own game: a dash or hop waiting for its moment
        V2 vgMoveTo;
        float vgMoveTime;

        /// The step a stance's chain plays next.
        public static int VgAt(Player p, int stance) => stance == 0 ? p.VgAtV : p.VgAtS;

        /// `p`'s chain in a stance (their lobby pick, sent with the run's start; the default if none).
        public static VgMove[] VgChainOf(Player p, int stance) => VowguardMotion.Chain(p.VgChain, stance);

        /// The move playing for `p` and how far through it is (0-1), from the host's clock; null when none is.
        public static VowguardMotion.Move VgPlaying(Player p, out float u)
        {
            u = 0f;
            var m = VowguardMotion.Of(p.VgMoveId);
            if (m == null || p.VgDur <= 0f || p.VgT >= p.VgDur)
                return null;
            u = p.VgT / p.VgDur;
            return m;
        }

        class VgEvent
        {
            public int Owner;
            public float Wait;
            public VowguardMotion.Heal Heal;  // a heal; or with none:
            public bool Dome, Ward;
            public float Bonus;               // the Faith spent (and the last step's bonus) added to each heal
            public float Mult = 1f;
            public float Aim;
        }

        class VgDome
        {
            public int Owner;
            public V2 Pos;
            public float Life;
        }

        readonly List<VgEvent> vgEvents = new List<VgEvent>();
        readonly List<VgDome> vgDomes = new List<VgDome>();

        /// Host: one press of the primary with the Vowguard (from FireKnightWeapon): the next step of the stance you're in.
        void FireVowguard(Player p, float aim, float dmg, int extra)
        {
            int st = p.VgStance;
            var chain = VgChainOf(p, st);
            int idx = VgAt(p, st) % chain.Length;
            var m = VowguardMotion.Of(chain[idx]);
            if (st == 0) p.VgAtV = (byte)((idx + 1) % chain.Length);
            else p.VgAtS = (byte)((idx + 1) % chain.Length);
            float rate = Math.Max(0.2f, FireRate(p));
            float dur = m.Dur / rate;
            p.VgMoveId = (byte)m.Id;
            p.VgCount++;
            p.VgT = 0f;
            p.VgDur = dur;
            p.VgSoak = 0;
            p.VgIdle = 0f;
            p.FireCd = dur * VgNext;
            float lastMult = 1f + VgExtraBonus * extra;  // (each extra projectile: every step harder)
            float faith = m.Fin ? p.VgFaith : 0f;
            if (m.Fin)
                p.VgFaith = 0;
            V2 dir = V2.FromAngle(aim), right = dir.Orthogonal() * -1f;
            float scale = p.Weapon.Damage * dmg / VgBaseDamage;
            for (int i = 0; i < m.Hits.Length; i++)
            {
                var h = m.Hits[i];
                float d = h.Dmg * scale * lastMult + (i == m.Hits.Length - 1 ? VgFaithDmg * faith * scale : 0f);
                Swing s;
                switch (h.K)
                {
                    case VowguardMotion.Shape.Lane:
                        s = NewSwing(p, aim, h.Reach, 0f, d);
                        s.HalfWidth = h.Width;
                        s.Thrust = true;
                        if (m.Keep)
                        {
                            s.FromSet = true;  // (the Shield Charge: along the line you dash, from where you started)
                            s.From = p.Pos;
                        }
                        break;
                    case VowguardMotion.Shape.Ring:
                        s = NewSwing(p, aim, h.Reach, 360f, d);
                        s.Circle = MeleeReach(p, h.Reach);
                        s.CircleAt = 0f;
                        break;
                    default:
                        s = NewSwing(p, aim, h.Reach, h.Half * 2f, d);
                        break;
                }
                s.Delay = h.T * dur;
                s.NoFx = true;  // the step's own effect draws it
                s.Vg = (byte)(st == 0 && !m.Fin ? 1 : 2);
                s.Heavy = m.Fin || h.Stun > 0f;
                if (h.Stun > 0f)
                {
                    s.Stun = h.Stun;
                    s.StarStun = true;
                }
                if (h.Knock > 0f)
                    s.Knock = h.Pull ? -h.Knock : h.Knock;
                s.Deflect = h.Deflect;
                QueueSwing(s);
            }
            foreach (var h in m.Heals)
                vgEvents.Add(new VgEvent { Owner = p.Id, Wait = h.T * dur, Heal = h, Bonus = VgFaithHeal * faith, Mult = lastMult, Aim = aim });
            if (m.DomeT >= 0f)
                vgEvents.Add(new VgEvent { Owner = p.Id, Wait = m.DomeT * dur, Dome = true });
            if (m.WardT >= 0f)
                vgEvents.Add(new VgEvent { Owner = p.Id, Wait = m.WardT * dur, Ward = true });
            if (m.InvulT0 >= 0f)
                p.InvulnT = Math.Max(p.InvulnT, m.InvulT1 * dur + 0.05f);
            if (m.Keep)
            {
                V2 to = p.Pos + dir * m.MoveFwd + right * m.MoveSide;
                p.VgCoverId = 0;
                if (m.CoverDash)
                {
                    var q = VgCoverTarget(p, aim);
                    if (q != null)
                    {
                        p.VgCoverId = q.Id;
                        to = q.Pos + dir * (q.Def.BodyR + p.Def.BodyR + 6f);
                    }
                }
                p.VgDashTo = ClampToArena(to, p.Def.BodyR);
            }
            BroadcastFx(FxKind.VgStep, p.Pos, p.Pos + dir * MeleeReach(p, p.Weapon.Reach), st == 0 ? VgGold : VgJade, p.Id * 100 + (int)m.Id, dur + VgFxTail, m.Sound);
        }

        /// Cover Ally's ally: the one nearest your aim within CoverRange (downed ones too), or null.
        Player VgCoverTarget(Player p, float aim)
        {
            Player best = null;
            float bestA = 1.1f;
            foreach (var q in Players.Values)
            {
                if (q.Id == p.Id || q.Pos.FartherThan(p.Pos, VowguardMotion.CoverRange))
                    continue;
                float a = AngleTo(p.Pos, q.Pos, aim);
                if (a < bestA)
                {
                    bestA = a;
                    best = q;
                }
            }
            return best;
        }

        /// From LandSwing: a Valor link that hit lights a Faith per enemy.
        void VgLanded(Player p, Swing s, int hits)
        {
            if (s.Vg == 1)
                p.VgFaith = (byte)Math.Min(VgFaithMax, p.VgFaith + hits);
        }

        /// A perfect parry fills Faith (from ParryHit).
        void ArmVgFaith(Player p)
        {
            if (p.Weapon.Id == WeaponId.Vowguard)
                p.VgFaith = VgFaithMax;
        }

        /// Half the guard's width for `p` now (radians; 0: no guard): a move's own while it plays, Sanctuary's otherwise.
        public static float VgGuardHalf(Player p)
        {
            float half = p.VgStance == 1 ? VgSanctHalf : 0f;
            var m = VgPlaying(p, out float u);
            if (m != null && m.GuardT0 >= 0f && u >= m.GuardT0 && u <= m.GuardT1)
                half = Math.Max(half, m.GuardHalf * MathX.Pi / 180f);
            return half;
        }

        public static float VgGuardRadius(Player p) => p.Def.BodyR + VgGuardGap;

        /// Host, from the bullet loop: an enemy shot meeting the Vowguard's shield from the front stops there (small shots only).
        bool VgBlocks(Player p, Bullet b)
        {
            if (p.Weapon.Id != WeaponId.Vowguard || p.Downed || p.SwallowT > 0f || Defs.Unblockable(b.Kind))
                return false;
            float half = VgGuardHalf(p);
            if (half <= 0f)
                return false;
            float r = VgGuardRadius(p) + b.Radius;
            V2 rel = b.Pos - p.Pos;
            if (rel.LengthSq >= r * r || b.Vel.X * rel.X + b.Vel.Y * rel.Y >= 0f)
                return false;
            return AngleTo(p.Pos, b.Pos, p.Aim) < half;
        }

        /// A shot stopped on the shield: a spark, Faith in Sanctuary, Raise Guard's heal, Bulwark Absorb's count.
        void VgBlockHit(Player p, Bullet b)
        {
            V2 rel = b.Pos - p.Pos;
            V2 at = p.Pos + (rel.IsZero ? V2.FromAngle(p.Aim) : rel.Normalized()) * VgGuardRadius(p);
            bool sound = p.VgBlockT <= 0f;
            if (sound)
                p.VgBlockT = VgBlockSoundGap;
            BroadcastFx(FxKind.Spark, at, VgJade, 10f, 0.22f, sound ? Sfx.Clang : Sfx.None);
            if (p.VgStance == 1)
                p.VgFaith = (byte)Math.Min(VgFaithMax, p.VgFaith + 1);
            var m = VgPlaying(p, out float u);
            if (m == null || m.GuardT0 < 0f || u < m.GuardT0 || u > m.GuardT1)
                return;
            if (m.Soak)
                p.VgSoak = (byte)Math.Min(255, p.VgSoak + 1);
            if (m.BlockHeal > 0f)
            {
                var q = MostHurt(p);
                if (q != null)
                    Heal(q, m.BlockHeal, p);
            }
        }

        /// The ally (or you, alone) missing the largest share of their health; null when nobody is hurt.
        Player MostHurt(Player p)
        {
            Player best = null;
            float bestF = 0.999f;
            foreach (var q in Players.Values)
            {
                if (q.Downed || q.MaxHp <= 0f)
                    continue;
                float f = q.Hp / q.MaxHp;
                if (f < bestF || (best == p && f <= bestF))
                {
                    bestF = f;
                    best = q;
                }
            }
            return best;
        }

        /// Cover Ally's guard on a player: CoverCut less damage while it lasts (Combat.cs Damage).
        public static float VgCover(Player p) => p.VgCoverT > 0f ? VowguardMotion.CoverCut : 0f;

        /// Host, from the enemy-bullet loop: an Aegis Dome stops every enemy shot that reaches it.
        bool VgDomeStops(Bullet b)
        {
            foreach (var d in vgDomes)
            {
                if (b.Pos.FartherThan(d.Pos, VowguardMotion.DomeR + b.Radius) || b.Pos.FartherThan(d.Pos, VowguardMotion.DomeR - 14f) == false)
                    continue;  // (only at its rim: a shot fired inside it isn't stopped)
                AddFx(FxKind.Spark, b.Pos, VgJade, 10f, 0.22f);
                return true;
            }
            return false;
        }

        /// Raising the parry starts both chains over; a step still winding up never lands.
        void ResetVgChain(Player p)
        {
            p.VgAtV = p.VgAtS = 0;
            p.FireCd = 0f;
            p.TapQueued = false;
            p.VgT = 99f;
            CancelVgStep(p);
        }

        void CancelVgStep(Player p)
        {
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Vg > 0 && s.Delay > 0f);
            vgEvents.RemoveAll(e => e.Owner == p.Id && !e.Dome);
        }

        /// Host: the weapon-action key with the Vowguard: Valor to Sanctuary and back. Both chains keep their place.
        void HostVgSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.Vowguard || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.VgSwitchCd > 0f || State == MatchState.GameOver)
                return;
            p.VgSwitchCd = VgSwitchCd - 0.05f;  // (slack for a guest's trip, which keeps the real cooldown)
            p.VgStance = (byte)(p.VgStance ^ 1);
            p.FireCd = 0f;
            p.TapQueued = false;
            p.VgT = 99f;
            CancelVgStep(p);
            if (p.VgStance == 0)
            {
                // Into Valor: the rim shoves whatever is on you.
                var s = NewSwing(p, p.Aim, VgShoveReach, 360f, VgShoveDmg * p.Stats.DmgMult);
                s.Circle = VgShoveReach;
                s.CircleAt = 0f;
                s.Knock = VgShoveKnock;
                s.NoFx = true;
                s.Silent = true;
                s.Vg = 2;
                QueueSwing(s);
            }
            BroadcastFx(FxKind.Ring, p.Pos, p.VgStance == 0 ? VgGold : VgJade, 40f, 0.3f, p.VgStance == 0 ? Sfx.TwinCut2 : Sfx.GreatRaise);
        }

        /// Host, every tick: steps' clocks, the chain's window, Cover Ally fading, the timed heals, domes and wards.
        void TickVowguard(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.VgCoverT = Math.Max(0f, p.VgCoverT - dt);
                if (p.Weapon.Id != WeaponId.Vowguard)
                {
                    if (p.VgStance != 0 || p.VgAtV != 0 || p.VgAtS != 0 || p.VgFaith != 0)
                    {
                        p.VgStance = 0;
                        p.VgAtV = p.VgAtS = 0;
                        p.VgFaith = 0;
                    }
                    continue;
                }
                p.VgSwitchCd = Math.Max(0f, p.VgSwitchCd - dt);
                p.VgBlockT = Math.Max(0f, p.VgBlockT - dt);
                bool was = p.VgT < p.VgDur;
                p.VgT = Math.Min(p.VgT + dt, 99f);
                if (p.VgT >= p.VgDur)
                {
                    if (was)
                        p.VgIdle = 0f;
                    p.VgIdle += dt;
                    if (p.VgIdle > VgChainWindow)
                    {
                        if (p.VgStance == 0) p.VgAtV = 0;
                        else p.VgAtS = 0;
                    }
                }
                if (p.Downed)
                {
                    p.VgT = 99f;
                    CancelVgStep(p);
                }
            }
            for (int i = vgEvents.Count - 1; i >= 0; i--)
            {
                var e = vgEvents[i];
                if ((e.Wait -= dt) > 0f)
                    continue;
                vgEvents.RemoveAt(i);
                Player p;
                if (!Players.TryGetValue(e.Owner, out p) || p.Downed || p.Weapon.Id != WeaponId.Vowguard)
                    continue;
                if (e.Dome)
                {
                    vgDomes.Add(new VgDome { Owner = p.Id, Pos = p.Pos, Life = VowguardMotion.DomeLife });
                    BroadcastFx(FxKind.VgDome, p.Pos, p.Pos, VgJade, VowguardMotion.DomeR, VowguardMotion.DomeLife, Sfx.None);
                }
                else if (e.Ward)
                {
                    foreach (var q in Players.Values)
                        if (!q.Downed && !q.Pos.FartherThan(p.Pos, VowguardMotion.WardR + q.Def.BodyR))
                        {
                            Ward(q, VowguardMotion.WardAmt);
                            LightMark(q, 2, 0f, false);
                        }
                }
                else
                    VgHeal(p, e);
            }
            for (int i = vgDomes.Count - 1; i >= 0; i--)
            {
                var d = vgDomes[i];
                Player owner;
                Players.TryGetValue(d.Owner, out owner);
                if ((d.Life -= dt) <= 0f)
                {
                    vgDomes.RemoveAt(i);
                    continue;
                }
                foreach (var q in Players.Values)
                    if (!q.Downed && !q.Pos.FartherThan(d.Pos, VowguardMotion.DomeR))
                        Heal(q, VowguardMotion.DomeHeal * dt, owner);
            }
        }

        /// One of a step's heals landing.
        void VgHeal(Player p, VgEvent e)
        {
            var h = e.Heal;
            float amt = h.Amt * e.Mult + e.Bonus;
            if (h.K == VowguardMotion.HealKind.Near && p.VgMoveId == (byte)VgMove.BulwarkAbsorb)
                amt += VowguardMotion.SoakHeal * Math.Min(VowguardMotion.SoakCap, p.VgSoak);
            switch (h.K)
            {
                case VowguardMotion.HealKind.Self:
                    Heal(p, amt, p);
                    break;
                case VowguardMotion.HealKind.Cover:
                {
                    Player q;
                    if (p.VgCoverId != 0 && Players.TryGetValue(p.VgCoverId, out q))
                    {
                        q.VgCoverT = VowguardMotion.CoverTime;
                        Heal(q, amt, p);
                        LightMark(q, 1, 0f, false);
                    }
                    break;
                }
                default:
                    foreach (var q in Players.Values)
                    {
                        if (q.Downed || q.Pos.FartherThan(p.Pos, h.R + q.Def.BodyR))
                            continue;
                        if (h.K == VowguardMotion.HealKind.Ahead && (q.Id == p.Id || AngleTo(p.Pos, q.Pos, e.Aim) > 1.05f))
                            continue;
                        Heal(q, amt, p);
                        if (q.Id != p.Id)
                            LightMark(q, 1, 0f, false);
                    }
                    break;
            }
        }

        /// Your own game, every tick: Shield Charge, Sidestep and Cover Ally move you (when VgCount changes), and the stance
        /// switch on the weapon-action key. Returns true while the Vowguard is out (nothing else uses the key then).
        bool LocalVowguard(Player me, V2 aimAt, bool pressed, float dt)
        {
            vgSwitchLocalCd = Math.Max(0f, vgSwitchLocalCd - dt);
            bool vg = me.Weapon.Id == WeaponId.Vowguard;
            if (!vg || vgCountSeen < 0)
                vgCountSeen = me.VgCount;
            else if (me.VgCount != vgCountSeen)
            {
                vgCountSeen = me.VgCount;
                vgMoveWait = -1f;
                var m = VowguardMotion.Of(me.VgMoveId);
                if (m != null && m.Keep && !me.Downed && me.StunT <= 0f)
                {
                    vgMoveWait = m.MoveT0 * m.Dur;
                    vgMoveTime = Math.Max(0.05f, (m.MoveT1 - m.MoveT0) * m.Dur);
                    vgMoveTo = me.VgDashTo;
                }
            }
            if (vgMoveWait >= 0f && (vgMoveWait -= dt) < 0f)
            {
                vgMoveWait = -1f;
                V2 d = vgMoveTo - me.Pos;
                float len = d.Length;
                if (len > 1f && !me.Downed && me.StunT <= 0f)
                {
                    dashSpeed = len / vgMoveTime;
                    DashDir = d / len;
                    DashT = vgMoveTime;
                }
            }
            if (!vg)
                return false;
            if (!pressed || vgSwitchLocalCd > 0f || me.Downed || me.StunT > 0f)
                return true;
            vgSwitchLocalCd = VgSwitchCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.VgStance, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.VgStance);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }
    }
}

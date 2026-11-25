using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Nodachi (Tank Ninja, the fourth weapon: Choice 3; Docs/Nodachi.md): a katana as tall as you, with two stances and a
    /// five-step combo in each. The moves are NodachiMotion.cs, which the hits below are timed from and every screen draws from.
    ///   - Arashi (Storm, stance 0): long sweeping hits round you. Every wind-up is armoured: NodArmorCut less damage taken.
    ///   - Mamori (Guard, stance 1): quick jabs. While one plays, small enemy shots in a NodConeDeg cone in front of you are blocked
    ///     (not red aimed shots), and you walk at NodGuardSlow the whole time you're in the stance.
    ///   - Alt (the weapon-action key, Msg.WeaponDash WeaponAlt.NodStance) switches stance. Each stance keeps its own place in its combo
    ///     (Player.NodStorm, NodGuard): there is no timer, so a combo never goes back down by waiting or switching. The step
    ///     playing is cut short by the switch (what hasn't landed of it never does). Raising the parry sends both combos back to
    ///     their first step (ResetNodChain).
    ///   - Nagi walks you forward and Hiki-zuki hops you back: your own game moves you when Player.NodCount changes (LocalNodachi).
    /// Each step is one effect (FxKind.NodStep: Pos the Ninja, Pos2 along the aim, Radius = id * 100 + code, code = stance * 8 +
    /// step), drawn from its age.
    public partial class Game
    {
        public const float NodArmorCut = 0.4f, NodGuardSlow = 0.85f, NodConeDeg = 90f, NodConeIn = 30f, NodConeOut = 110f, NodSwitchCd = 0.3f;
        /// Each extra projectile: another ring after Tenchi's slam, this much farther out, for this share of its damage.
        public const float NodAftershockOut = 45f, NodAftershockMult = 0.6f, NodAftershockDelay = 0.18f;
        public const float NodHopGrace = 0.06f;  // Hiki-zuki's hop is untouchable, and this long after
        public static readonly Rgba NodSteel = new Rgba(0.86f, 0.92f, 1f), NodJade = new Rgba(0.5f, 0.88f, 0.77f), NodVermilion = new Rgba(1f, 0.42f, 0.27f);
        /// Each step's own sound, sent at the press and timed to its strikes (Game/NodachiSoundBank.cs): Arashi's five, then Mamori's.
        static readonly Sfx[] NodStepSounds = { Sfx.NodNuki, Sfx.NodKesa, Sfx.NodMoon, Sfx.NodTenchi, Sfx.NodNagi, Sfx.NodKasumi, Sfx.NodNidan, Sfx.NodSenbon, Sfx.NodTsuka, Sfx.NodHiki };

        float nodSwitchLocalCd;  // your own game: until the key switches again (the host checks its own)
        byte nodCountSeen;       // your own game: the last step you moved along

        /// The place a stance's combo is at: the index of the step the next press plays.
        public static int NodAt(Player p, int stance) => stance == 0 ? p.NodStorm : p.NodGuard;

        /// The step playing for a player now (its age in `c`), from the host's own clock (NodT); null when none is.
        public static NodachiMotion.Step NodPlaying(Player p, out float c)
        {
            c = p.NodT;
            var s = NodachiMotion.StepOf(p.NodCode);
            return s != null && c < s.Dur ? s : null;
        }

        /// The Storm's armour (Combat.cs Damage): NodArmorCut while a Storm step winds up.
        float NodArmor(Player p)
        {
            if (p.Weapon.Id != WeaponId.Nodachi || p.Downed)
                return 0f;
            var s = NodPlaying(p, out float c);
            return s != null && NodachiMotion.StanceOf(p.NodCode) == 0 && NodachiMotion.Armoured(s, c) ? NodArmorCut : 0f;
        }

        /// Host: one press of the primary with the Nodachi (from FireNinjaWeapon): the next step of the stance you're in.
        void FireNodachi(Player p, float aim, float dmg, int extra)
        {
            int stance = p.NodStance, idx = NodAt(p, stance), code = NodachiMotion.Code(stance, idx);
            var s = NodachiMotion.StepOf(code);
            if (stance == 0)
                p.NodStorm = (byte)((idx + 1) % NodachiMotion.Steps);
            else
                p.NodGuard = (byte)((idx + 1) % NodachiMotion.Steps);
            p.NodCode = (byte)code;
            p.NodCount++;
            p.NodT = 0f;
            p.FireCd = s.Next / FireRate(p);
            float w = p.Weapon.Damage * dmg;
            int group = 0;
            foreach (var g in s.Segs)
            {
                switch (g.K)
                {
                    case NodachiMotion.Kind.Arc:
                    {
                        // The sweep in slices, each landing as the edge reaches it; an enemy is cut once a sweep.
                        group = ++sweepCount;
                        float span = Math.Abs(g.A1 - g.A0);
                        int n = Math.Max(1, (int)Math.Ceiling(span / 50f));
                        for (int i = 0; i < n; i++)
                        {
                            float deg = g.A0 + (g.A1 - g.A0) * (i + 0.5f) / n;
                            var h = NodSwing(p, aim + deg * MathX.Pi / 180f, g.Reach * p.Stats.NodReachMult, span / n + 12f, w * g.Mult, NodachiMotion.ArcTimeAt(g, deg));
                            h.Sweep = group;
                            h.Heavy = stance == 0;
                            if (g.Knock > 0f)
                                h.Knock = g.Knock;
                        }
                        break;
                    }
                    case NodachiMotion.Kind.Jab:
                    {
                        var h = NodSwing(p, aim + g.A0 * MathX.Pi / 180f, g.Reach, 0f, w * g.Mult, NodachiMotion.HitAt(g));
                        h.HalfWidth = g.Half;
                        h.Thrust = true;
                        break;
                    }
                    case NodachiMotion.Kind.Pommel:
                    {
                        var h = NodSwing(p, aim, g.Reach, 0f, w * g.Mult, NodachiMotion.HitAt(g));
                        h.HalfWidth = g.Half;
                        h.Thrust = true;
                        h.Stun = g.Stun;
                        h.StarStun = true;  // stars, not ice (Daze.cs)
                        h.Knock = g.Knock;
                        h.Heavy = true;
                        break;
                    }
                    case NodachiMotion.Kind.Slam:
                    {
                        float reachMult = MeleeReach(p, g.Reach) / g.Reach * p.Stats.NodReachMult;  // Reach upgrades widen the ring too
                        for (int k = 0; k <= extra; k++)  // each extra projectile: another ring, further out
                        {
                            var h = NodSwing(p, aim, 0f, 0f, w * g.Mult * (k == 0 ? 1f : NodAftershockMult), g.T0 + NodAftershockDelay * k);
                            h.Circle = (g.Reach + NodAftershockOut * k) * reachMult;
                            h.CircleAt = 0f;
                            h.Stun = g.Stun;
                            h.StarStun = true;
                            h.Knock = g.Knock;
                            h.Power = 1f;
                            h.Heavy = true;
                        }
                        break;
                    }
                    case NodachiMotion.Kind.Hop:
                        p.InvulnT = Math.Max(p.InvulnT, g.T1 + NodHopGrace);
                        break;
                }
            }
            BroadcastFx(FxKind.NodStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, p.Weapon.Reach), stance == 0 ? NodSteel : NodJade, p.Id * 100 + code,
                s.Dur + (idx == 3 && stance == 0 ? 0.6f + NodAftershockDelay * extra : 0.25f), NodStepSounds[stance * NodachiMotion.Steps + idx]);
        }

        /// One hit of a step, landing `delay` after the press.
        Swing NodSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it
            s.Nod = true;   // (the parry and the switch take back one still winding up)
            QueueSwing(s);
            return s;
        }

        /// Raising the parry cancels both combos (ResetAttackChains): each stance back to its first step, a step still winding up
        /// never lands.
        void ResetNodChain(Player p)
        {
            p.NodStorm = 0;
            p.NodGuard = 0;
            p.FireCd = 0f;
            p.TapQueued = false;
            p.NodT = 99f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Nod && s.Delay > 0f);
        }

        /// Host: the weapon-action key with the Nodachi: Storm to Guard and back. Both combos keep their place; the step playing stops.
        void HostNodSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.Nodachi || p.Downed || p.StunT > 0f || p.SwallowT > 0f || p.NodSwitchCd > 0f || State == MatchState.GameOver)
                return;
            p.NodSwitchCd = NodSwitchCd - 0.05f;  // (slack for a guest's trip, which keeps the real cooldown)
            p.NodStance = (byte)(p.NodStance ^ 1);
            p.FireCd = 0f;
            p.TapQueued = false;
            p.NodT = 99f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Nod && s.Delay > 0f);
            BroadcastFx(FxKind.Ring, p.Pos, p.NodStance == 0 ? NodSteel : NodJade, 40f, 0.3f, p.NodStance == 0 ? Sfx.NodToStorm : Sfx.NodToGuard);
        }

        readonly List<int> nodBlocked = new List<int>();

        /// Host, every tick for every player: the step's clock, the switch's cooldown, and the Guard's cone blocking shots.
        void TickNodachi(Player p, float dt)
        {
            if (p.Weapon.Id != WeaponId.Nodachi)
            {
                p.NodStance = 0;
                p.NodStorm = p.NodGuard = 0;
                return;
            }
            p.NodSwitchCd = Math.Max(0f, p.NodSwitchCd - dt);
            p.NodT = Math.Min(p.NodT + dt, 99f);
            var s = NodPlaying(p, out float c);
            if (s == null || NodachiMotion.StanceOf(p.NodCode) != 1 || !NodachiMotion.Guarding(s, c) || p.Downed)
                return;
            float half = (NodConeDeg + p.Stats.NodConeBonus) * MathX.Pi / 360f;
            nodBlocked.Clear();
            foreach (var b in Bullets.Values)
            {
                if (!Defs.IsEnemyShot(b.Kind) || Defs.Unblockable(b.Kind))
                    continue;
                float d = b.Pos.DistanceTo(p.Pos);
                if (d < NodConeIn || d > NodConeOut + b.Radius || AngleTo(p.Pos, b.Pos, p.Aim) > half)
                    continue;
                nodBlocked.Add(b.Id);
            }
            foreach (int id in nodBlocked)
            {
                Bullet b;
                if (!Bullets.TryGetValue(id, out b))
                    continue;
                KillBullet(id);
                AddFx(FxKind.Spark, b.Pos, NodJade, 10f, 0.22f);
            }
            if (nodBlocked.Count > 0)
                BroadcastSound(Sfx.NodBlock);
        }

        /// Your own game, every tick: Nagi's walk and Hiki-zuki's hop (when NodCount changes), and the stance switch on the
        /// weapon-action key. Returns true while the Nodachi is out (nothing else uses the key then).
        bool LocalNodachi(Player me, V2 aimAt, bool pressed, float dt)
        {
            nodSwitchLocalCd = Math.Max(0f, nodSwitchLocalCd - dt);
            bool nod = me.Weapon.Id == WeaponId.Nodachi;
            if (!nod || me.NodCount == nodCountSeen)
                nodCountSeen = me.NodCount;
            else
            {
                nodCountSeen = me.NodCount;
                var s = NodachiMotion.StepOf(me.NodCode);
                if (s != null && !me.Downed && me.StunT <= 0f)
                    foreach (var g in s.Segs)
                    {
                        if (g.K != NodachiMotion.Kind.Walk && g.K != NodachiMotion.Kind.Hop)
                            continue;
                        V2 dir = V2.FromAngle(me.Aim) * Math.Sign(g.Reach);
                        V2 to = ClampToArena(me.Pos + dir * Math.Abs(g.Reach), me.Def.BodyR);
                        float time = g.K == NodachiMotion.Kind.Walk ? g.T1 : g.T1 - g.T0;
                        dashSpeed = Math.Abs(g.Reach) / time;
                        DashDir = dir;
                        DashT = to.DistanceTo(me.Pos) / dashSpeed;
                    }
            }
            if (!nod)
                return false;
            if (!pressed || nodSwitchLocalCd > 0f || me.Downed || me.StunT > 0f)
                return true;
            nodSwitchLocalCd = NodSwitchCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.NodStance, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.NodStance);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }
    }
}

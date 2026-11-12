using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Lance (Tank Knight): the Breach (Docs/Mockups/lance-rework.html; the motion is LanceMotion.cs, the look Game/LanceLook.cs).
    ///   - Chain: Jab, Second Jab (quick thrusts, a small shove), Wide Sweep (the shaft swung flat across 150 degrees, batting
    ///     enemies to your left), Breaching Thrust (a wind-up, a step in, a 205 x 88 px box that shoves 160 px and stuns 1 s).
    ///     The next press counts once a step is Next of the way through; LanceChainWindow after a step ends without one, the chain
    ///     starts over. The step and a count of steps share Player.Combo with the Twin Blades (StepOf), and each step moves you
    ///     forward a little on your own game (LocalLanceStep).
    ///   - Impact: an enemy a Lance swing shoved that is still sliding fast (ImpactArmed px or more of the push left) and meets
    ///     another enemy: both take ImpactDmg and the one it hits slides on with ImpactCarry of the push (armed too, so a good
    ///     shove bowls a line). Into the arena wall: WallImpactDmg and a WallImpactStun. Heavy enemies and bosses aren't
    ///     shoved, so they never take it (Knock.cs calls in from TickKnock).
    ///   - Alt (the weapon-action key): Hook & Haul. The lance shoots out to the enemy nearest your cursor (up to HookRange),
    ///     hooks it and hauls it to your tip, stunned; your next press is the Breaching Thrust (Player.LancePrimed).
    public partial class Game
    {
        public const float LanceChainWindow = 0.7f;
        public const float LanceJabHalfWidth = 16f, LanceJabKnock = 30f, LanceSweepKnock = 85f;
        public const int LanceSweepSteps = 8;
        public const float BreachExtraReach = 27f, BreachHalfWidth = 44f, BreachKnock = 160f, BreachStun = 1f;
        public static readonly float[] LanceMult = { 1f, 1f, 16f / 14f, 40f / 14f };  // each step against the weapon's Damage (14, 14, 16, 40)
        public const float HookRange = 260f, HookMult = 10f / 14f, HookStun = 0.8f, HookHold = 62f, HookCooldown = 4f, HookMiss = 200f;
        public const float ImpactArmed = 34f, ImpactDmg = 10f, ImpactCarry = 0.7f, WallImpactDmg = 18f, WallImpactStun = 0.8f, ImpactFxTime = 1.1f;
        public static readonly Rgba LanceGlow = new Rgba(0.59f, 0.83f, 1f), LanceImpactCol = new Rgba(1f, 0.65f, 0.28f);

        class HookRun
        {
            public int Owner, Target;
            public V2 Dir;
            public float T;
        }

        readonly List<HookRun> hookRuns = new List<HookRun>();
        float hookLocalCd;  // your own game: until the key throws again (the host checks its own)
        public float LanceHookCd => hookLocalCd;

        /// Host: one press of the primary with the Lance (from FireKnightWeapon): the next step of the chain.
        void FireLance(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = TwinBladesMotion.StepOf(p.Combo);
            int step = p.LancePrimed ? 4 : last >= 1 && last < 4 && p.ComboT > 0f ? last + 1 : 1;
            p.LancePrimed = false;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            var d = LanceMotion.Steps[step - 1];
            p.ComboT = d.Dur + LanceChainWindow;
            p.FireCd = d.Dur * d.Next / FireRate(p);
            float hit = w.Damage * dmg * LanceMult[step - 1], reach = w.Reach, half = 0f;
            switch (step)
            {
                case 1:
                case 2:
                {
                    var s = LanceSwing(p, aim, w.Reach, 0f, hit, d.Dur * LanceMotion.JabHit, step, Sfx.LanceJab);
                    s.HalfWidth = LanceJabHalfWidth;
                    s.Knock = LanceJabKnock;
                    s.Thrust = true;
                    break;
                }
                case 3:
                    reach = LanceSweep(p, aim, hit, d);
                    break;
                default:
                {
                    reach = w.Reach + BreachExtraReach + w.Grow * extra;
                    half = BreachHalfWidth + 12f * extra;
                    var s = LanceSwing(p, aim, reach, 0f, hit, d.Dur * LanceMotion.BreachHit, step, Sfx.None);  // (its sound plays from the click, wind-up and all)
                    s.HalfWidth = half;
                    s.Knock = BreachKnock + 60f * extra;
                    s.Stun = BreachStun;
                    s.Heavy = true;
                    s.Thrust = true;
                    break;
                }
            }
            // The whole move, drawn from its age: Radius carries who and which step, the colour's alpha the Breaching Thrust's half width.
            BroadcastFx(FxKind.LanceStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, reach), LanceGlow.WithAlpha(half / 100f), p.Id * 10 + step,
                d.Dur + LanceMotion.Recover, step == 4 ? Sfx.LanceBreach : Sfx.None);
        }

        /// One hit of a step. `sound` plays as it lands (Sfx.None: nothing).
        Swing LanceSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, int step, Sfx sound)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it
            s.LanceKind = (byte)step;
            s.Sound = sound;
            QueueSwing(s);
            return s;
        }

        /// The Wide Sweep: LanceSweepSteps cones that follow the lance across, each landing when the motion has the lance there
        /// (LanceMotion's Wide Sweep), each enemy hit once. What they hit is batted along the sweep (LanceAcross). Returns the
        /// lance's reach from your centre.
        float LanceSweep(Player p, float aim, float hit, TwinBladesMotion.Step d)
        {
            int group = ++sweepCount;
            float a0 = LanceMotion.At(d, d.Dur * LanceMotion.SweepFrom).Rb, a1 = LanceMotion.At(d, d.Dur * LanceMotion.SweepTo).Rb;
            float gap = Math.Abs(a1 - a0) / (LanceSweepSteps - 1), u = LanceMotion.SweepFrom, reach = 0f;
            for (int i = 0; i < LanceSweepSteps; i++)
            {
                float target = a0 + (a1 - a0) * i / (LanceSweepSteps - 1);
                while (u < LanceMotion.SweepTo && (a1 < a0 ? LanceMotion.At(d, d.Dur * u).Rb > target : LanceMotion.At(d, d.Dur * u).Rb < target))
                    u += 0.004f;
                var m = LanceMotion.At(d, d.Dur * u);
                V2 tip = V2.FromAngle(m.Ra) * m.Rr + V2.FromAngle(m.Rb) * LanceMotion.Len;  // off the aim, from your centre
                reach = Math.Max(reach, tip.Length);
                var s = LanceSwing(p, aim + tip.Angle, tip.Length, gap * 1.15f * 180f / MathX.Pi, hit, d.Dur * u, 3, i == 0 ? Sfx.LanceSweep : Sfx.None);
                s.Sweep = group;
                s.Knock = LanceSweepKnock;
                s.LanceAcross = aim + m.Rb + (a1 < a0 ? -1f : 1f) * MathX.Pi / 2f;
            }
            return reach;
        }

        /// Host, from LandSwing: which way a Lance swing pushes what it hit. The thrusts drive straight ahead; the Wide Sweep bats
        /// along its swing, a little outward.
        static V2 LanceKnockDir(Swing s, V2 away, V2 dir)
        {
            if (s.LanceKind == 3)
            {
                V2 across = V2.FromAngle(s.LanceAcross) * 0.7f + (away.IsZero ? dir : away) * 0.45f;
                return across.Normalized();
            }
            return dir;
        }

        /// Host: a Lance step (1-4) or the hook (5) hit `hits` enemies. Sent as an effect that draws nothing: it carries the hit's
        /// sound, and the knight's own game holds the picture and shakes (Game/Juice.cs, LanceHitFeel).
        void LanceHitCue(Player p, int kind, int hits)
        {
            BroadcastFx(FxKind.LanceHit, p.Pos, new V2(p.Id, kind), LanceGlow, hits, 0.2f, kind == 5 ? Sfx.LanceHaul : kind == 4 ? Sfx.LanceHitHeavy : Sfx.LanceHitSfx);
        }

        /// Host, every tick: the hook's cooldown, hooks landing.
        void TickLance(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.LanceHookCd = Math.Max(0f, p.LanceHookCd - dt);
                if (p.LancePrimed && (p.Downed || p.Weapon.Id != WeaponId.Lance))
                    p.LancePrimed = false;
            }
            for (int i = hookRuns.Count - 1; i >= 0; i--)
            {
                var r = hookRuns[i];
                r.T += dt;
                if (r.T < LanceMotion.Hook.Dur * LanceMotion.HookHit)
                    continue;
                hookRuns.RemoveAt(i);
                Player p;
                Enemy e;
                if (!Players.TryGetValue(r.Owner, out p) || p.Downed || r.Target < 0 || !Enemies.TryGetValue(r.Target, out e) || e.Hp <= 0f)
                    continue;
                LandHook(p, e, r.Dir);
            }
        }

        /// The hook catches: the enemy is hurt, stunned (not bosses) and hauled to your tip (not heavies or bosses), and your next
        /// press is the Breaching Thrust.
        void LandHook(Player p, Enemy e, V2 dir)
        {
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * HookMult;
            WeaponHurt(e, dmg, p.Id);
            OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir * -1f, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
            if (!e.Def.IsBoss)
            {
                e.FrozenT = Math.Max(e.FrozenT, HookStun);
                e.SlowT = Math.Max(e.SlowT, HookStun);
            }
            if (!Heavy(e))
            {
                V2 to = ClampToArena(p.Pos + dir * HookHold, e.Radius);
                e.KnockLeft = V2.Zero;
                e.ImpactBy = 0;
                KnockEnemy(e, to - e.Pos);
            }
            p.LancePrimed = true;
            LanceHitCue(p, 5, 1);
        }

        /// The enemy Hook & Haul from `p` aimed at `aimAt` goes for: the one nearest the aim point within HookRange of `p`, or null.
        Enemy HookTarget(Player p, V2 aimAt)
        {
            Enemy best = null;
            float bd = float.MaxValue;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.DistanceTo(p.Pos) > HookRange)
                    continue;
                float d = e.Pos.DistanceSq(aimAt);
                if (d < bd)
                {
                    bd = d;
                    best = e;
                }
            }
            return best;
        }

        /// Host: the weapon-action key with the Lance, aimed at `aimAt`: Hook & Haul, if it is ready.
        void HostLanceHook(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Lance || p.LanceHookCd > 0f || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            var e = HookTarget(p, aimAt);
            V2 to = (e != null ? e.Pos : aimAt) - p.Pos;
            V2 dir = to.Length > 1f ? to.Normalized() : V2.FromAngle(p.Aim);
            p.LanceHookCd = HookCooldown;
            p.FireCd = Math.Max(p.FireCd, LanceMotion.Hook.Dur * LanceMotion.Hook.Next);
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.LanceKind > 0 && s.Delay > 0f);  // a step still winding up never lands
            hookRuns.Add(new HookRun { Owner = p.Id, Target = e != null ? e.Id : -1, Dir = dir });
            // The throw and the haul, drawn from its age: Pos2 is where the enemy stood (or the aim point on a miss).
            BroadcastFx(FxKind.LanceHook, p.Pos, e != null ? e.Pos : p.Pos + dir * HookMiss, LanceGlow, p.Id * 10 + (e != null ? 1 : 0),
                LanceMotion.Hook.Dur + LanceMotion.Recover, Sfx.LanceHookOut);
        }

        /// Raising the parry cancels the chain: back to the Jab, a step still winding up never lands (a primed thrust is kept).
        void ResetLanceChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.LanceKind > 0 && s.Delay > 0f);
        }

        // --- Impact (from TickKnock) ------------------------------------------------------------------------------

        /// A Lance-shoved enemy still sliding fast meets another: both take ImpactDmg (a heavy one or a boss doesn't, and isn't
        /// moved), and the one it hits slides on with ImpactCarry of the push, armed in turn. Each pair meets once.
        void LanceBump(Enemy e)
        {
            foreach (var g in Enemies.Values)
            {
                if (g == e || !Targetable(g) || g.Id == e.ImpactLast || g.Pos.DistanceTo(e.Pos) > e.Radius + g.Radius + 1f)
                    continue;
                Player owner;
                Players.TryGetValue(e.ImpactBy - 1, out owner);
                float dmg = ImpactDmg * (owner != null ? owner.Stats.DmgMult : 1f);
                e.ImpactLast = g.Id;
                g.ImpactLast = e.Id;
                V2 left = e.KnockLeft;
                bool heavy = Heavy(g);
                if (!heavy)
                {
                    KnockEnemy(g, left * ImpactCarry);
                    g.ImpactBy = e.ImpactBy;
                }
                e.KnockLeft = left * (1f - ImpactCarry);
                e.KnockSpeed *= 1f - ImpactCarry;
                WeaponHurt(e, dmg, e.ImpactBy - 1);
                if (!heavy)
                    WeaponHurt(g, dmg, e.ImpactBy - 1);
                BroadcastFx(FxKind.LanceImpact, (e.Pos + g.Pos) * 0.5f, new V2(e.Id, heavy ? -2 : g.Id), LanceImpactCol, dmg, ImpactFxTime, Sfx.LanceImpactSfx);
                return;
            }
        }

        /// A Lance-shoved enemy still sliding fast hits the arena wall at `at`: WallImpactDmg and a WallImpactStun (in place of the
        /// ordinary wall slam), and the slide stops.
        void LanceWallImpact(Enemy e, V2 at)
        {
            Player owner;
            Players.TryGetValue(e.ImpactBy - 1, out owner);
            float dmg = WallImpactDmg * (owner != null ? owner.Stats.DmgMult : 1f);
            e.KnockLeft = V2.Zero;
            e.KnockSpeed = 0f;
            e.FrozenT = Math.Max(e.FrozenT, WallImpactStun);
            e.SlowT = Math.Max(e.SlowT, WallImpactStun);
            WeaponHurt(e, dmg, e.ImpactBy - 1);
            BroadcastFx(FxKind.LanceImpact, at, new V2(e.Id, -1), LanceImpactCol, dmg, ImpactFxTime, Sfx.LanceWall);
            e.ImpactBy = 0;
            e.ImpactLast = -1;
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        int lanceCountSeen = -1;
        float lanceStepWait = -1f, lanceStepFwd, lanceStepTime;
        V2 lanceStepDir;

        /// Each step moves you forward a little while it plays (the host counts the steps; a change in the count is a new one):
        /// from LanceMotion.MoveFrom to MoveTo of the step, as the mockup's.
        void LocalLanceStep(Player me, float dt)
        {
            hookLocalCd = Math.Max(0f, hookLocalCd - dt);
            if (lanceStepWait >= 0f && (lanceStepWait -= dt) < 0f)
                StartLanceStep(me);
            int count = me.Combo >> 3;
            if (lanceCountSeen < 0 || me.Weapon.Id != WeaponId.Lance)
            {
                lanceCountSeen = count;
                lanceStepWait = -1f;
                return;
            }
            if (count == lanceCountSeen)
                return;
            lanceCountSeen = count;
            int step = TwinBladesMotion.StepOf(me.Combo);
            if (step < 1 || step > 4 || me.Downed || me.StunT > 0f || me.Parrying || KatanaStriking)
                return;
            var d = LanceMotion.Steps[step - 1];
            lanceStepDir = V2.FromAngle(me.Aim);
            lanceStepFwd = d.Fwd;
            lanceStepTime = d.Dur * (LanceMotion.MoveTo[step - 1] - LanceMotion.MoveFrom[step - 1]);
            lanceStepWait = d.Dur * LanceMotion.MoveFrom[step - 1];
        }

        void StartLanceStep(Player me)
        {
            lanceStepWait = -1f;
            if (me.Downed || me.StunT > 0f || DashT > 0f || me.Parrying || me.Weapon.Id != WeaponId.Lance)
                return;
            V2 to = ClampToArena(me.Pos + lanceStepDir * lanceStepFwd, me.Def.BodyR);
            DashDir = lanceStepDir;
            dashSpeed = lanceStepFwd / lanceStepTime;
            DashT = to.DistanceTo(me.Pos) / dashSpeed;
        }

        /// Your own game: the weapon-action key with the Lance. True when it threw (and the key is used up).
        bool LocalLanceHook(Player me, V2 aimAt, bool pressed)
        {
            if (me.Weapon.Id != WeaponId.Lance || !pressed || hookLocalCd > 0f || me.Downed || me.StunT > 0f)
                return false;
            hookLocalCd = HookCooldown;
            if (IsHost)
                HostWeaponDash(MyId, DashHook, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(DashHook);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }
    }
}

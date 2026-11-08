using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Twin Blades (DPS Knight): the Crescent Waltz (Docs/Mockups/twin-blades-rework.html; the motion is TwinBladesMotion.cs).
    ///   - Chain: Rising Cut, Falling Cut, Double Jab, Pirouette, Crescent Shear, then it starts over. The next press counts once a
    ///     step is Next of the way through; TwinChainWindow after a step ends without one, the chain starts over too. Each step
    ///     moves you forward a little: the host counts the swings in Player.Combo's high bits, and your own game steps you
    ///     forward when the count changes (LocalTwinStep), as the Katana's lunge does. The Shear launches a piercing crescent.
    ///   - Alt (the weapon-action key): Twin Fang. Both sabres are thrown out on mirrored curves to your cursor (up to
    ///     FangReach), cross there in a burst and fly back the other way, cutting what they pass both ways. You can't swing
    ///     until you catch them. Your game sends Msg.WeaponDash (DashStrike, aimed at the cursor); the host does the rest.
    /// Each step is one effect (FxKind.TwinStep) and each throw one (FxKind.TwinFang), drawn from their age (Game/TwinBladesLook.cs).
    public partial class Game
    {
        public const float TwinChainWindow = 0.7f;
        public const float TwinCutArc = 110f, TwinSpinReach = 80f, TwinJabReach = 115f, TwinJabHalfWidth = 10f, TwinShearArc = 200f, TwinShearKnock = 18f;
        public static readonly float[] TwinMult = { 1f, 1f, 0.75f, 0.8f, 2.75f };  // damage of each hit of each step against the weapon's Damage (16, 16, 12 x2, 13 x2, 44)
        public const float TwinWaveMult = 1.875f;                                    // the Shear's crescent: 30
        public const float FangReach = 210f, FangHitR = 20f, FangMult = 1.125f, FangBurstMult = 1.5625f, FangBurstR = 55f, FangCooldown = 4f;
        public static readonly Rgba TwinMoon = new Rgba(0.67f, 0.84f, 1f);

        class FangRun
        {
            public int Owner;
            public V2 From, To;
            public float T;          // seconds since the press
            public bool Burst;
            public readonly HashSet<int> Hit = new HashSet<int>();  // enemy id * 4 + blade * 2 + leg
        }

        readonly List<FangRun> fangRuns = new List<FangRun>();
        float fangLocalCd;   // your own game: until the key throws again (the host checks its own)
        public float TwinFangCd => fangLocalCd;

        /// Host: one press of the primary with the Twin Blades (from FireKnightWeapon): the next step of the chain.
        void FireTwinBlades(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int last = TwinBladesMotion.StepOf(p.Combo);
            int step = last >= 1 && last < 5 && p.ComboT > 0f ? last + 1 : 1;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            var d = TwinBladesMotion.Steps[step - 1];
            p.ComboT = d.Dur + TwinChainWindow;
            p.FireCd = d.Dur * d.Next / FireRate(p);
            float hit = w.Damage * dmg * TwinMult[step - 1];
            switch (step)
            {
                case 1:
                case 2:
                    TwinSwing(p, aim, w.Reach, TwinCutArc, hit, d.Dur * 0.5f, step == 1 ? Sfx.TwinCut1 : Sfx.TwinCut2, step);
                    break;
                case 3:
                    for (int k = 0; k < 2; k++)
                    {
                        var s = TwinSwing(p, aim, TwinJabReach, 0f, hit, d.Dur * (k == 0 ? 0.27f : 0.59f), Sfx.TwinJab, step);
                        s.HalfWidth = TwinJabHalfWidth;
                        s.Thrust = true;
                    }
                    break;
                case 4:
                    TwinSwing(p, aim, TwinSpinReach, 360f, hit, d.Dur * 0.35f, Sfx.TwinSpin, step);
                    var spin = TwinSwing(p, aim, TwinSpinReach, 360f, hit, d.Dur * 0.7f, Sfx.None, step);  // (the first's whirl covers both turns)
                    spin.Waves = extra;  // the +1 upgrade: the Pirouette throws crescents too
                    spin.WaveDmg = w.Damage * dmg * TwinWaveMult;
                    break;
                default:
                {
                    var s = TwinSwing(p, aim, w.Reach, TwinShearArc, hit, d.Dur * 0.53f, Sfx.None, step);
                    s.Heavy = true;
                    s.Knock = TwinShearKnock;
                    s.Waves = 1 + extra;
                    s.WaveDmg = w.Damage * dmg * TwinWaveMult;
                    break;
                }
            }
            // The whole move, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.TwinStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, w.Reach), TwinMoon, p.Id * 10 + step,
                d.Dur + TwinBladesMotion.Recover, step == 5 ? Sfx.TwinShear : Sfx.None);  // (the Shear's sound starts with its wind-up)
        }

        /// One hit of a step. `sound` plays as it lands (Sfx.None: nothing, as the Shear's and the second turn's are already playing).
        Swing TwinSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, Sfx sound, int step)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.NoFx = true;  // the step's own effect draws it
            s.Twin = true;
            s.TwinKind = (byte)step;
            s.Sound = sound;
            pendingSwings.Add(s);
            return s;
        }

        /// Host, every tick: the chain forgets its place, thrown sabres fly.
        void TickTwinBlades(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.FangCd = Math.Max(0f, p.FangCd - dt);
                if (p.Weapon.Id == WeaponId.Censer)
                    continue;  // the Censer keeps its rite in Combo too (Censer.cs)
                if (TwinBladesMotion.StepOf(p.Combo) != 0 && ((p.ComboT -= dt) <= 0f || p.Downed || p.Weapon.Id != WeaponId.TwinBlades))
                    p.Combo &= ~7;  // back to the first step (the swing count stays)
            }
            for (int i = fangRuns.Count - 1; i >= 0; i--)
            {
                var r = fangRuns[i];
                Player p;
                r.T += dt;
                if (!Players.TryGetValue(r.Owner, out p) || r.T >= TwinBladesMotion.FangRelease + TwinBladesMotion.FangFlight)
                {
                    fangRuns.RemoveAt(i);
                    if (p != null && !p.Downed)
                        BroadcastSound(Sfx.TwinCatch);  // both sabres back in hand
                    continue;
                }
                float q = (r.T - TwinBladesMotion.FangRelease) / TwinBladesMotion.FangFlight;
                if (q < 0f)
                    continue;
                FlyFang(p, r, q);
            }
        }

        void FlyFang(Player p, FangRun r, float q)
        {
            float dmg = p.Weapon.Damage * p.Stats.DmgMult;
            int leg = q < 0.5f ? 0 : 1, cut = 0;
            for (int blade = 0; blade < 2; blade++)
            {
                V2 at = TwinBladesMotion.FangPos(r.From, r.To, p.Pos, q, blade == 0 ? 1 : -1);
                foreach (var e in Enemies.Values)
                {
                    if (e.SpawnT > 0f || e.Hp <= 0f || e.Pos.DistanceTo(at) > e.Radius + FangHitR)
                        continue;
                    if (!r.Hit.Add(e.Id * 4 + blade * 2 + leg))
                        continue;
                    FangHurt(p, e, dmg * FangMult, at);
                    cut++;
                }
                CutShotsNear(at, FangHitR);
            }
            if (cut > 0)
                TwinHitCue(p, 6, cut);
            if (!r.Burst && q >= 0.5f)
            {
                r.Burst = true;
                int burst = 0;
                foreach (var e in Enemies.Values)
                    if (e.SpawnT <= 0f && e.Hp > 0f && e.Pos.DistanceTo(r.To) <= e.Radius + FangBurstR)
                    {
                        FangHurt(p, e, dmg * FangBurstMult, r.To);
                        burst++;
                    }
                BroadcastFx(FxKind.Ring, r.To, TwinMoon, FangBurstR, 0.3f, Sfx.TwinCross);
                if (burst > 0)
                    TwinHitCue(p, 7, burst);
            }
        }

        void FangHurt(Player p, Enemy e, float dmg, V2 from)
        {
            V2 dir = e.Pos - from;
            HurtEnemy(e, dmg, p.Id);
            OnBulletHit(new Bullet { Pos = e.Pos, Vel = dir.IsZero ? new V2(1f, 0f) : dir.Normalized(), Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
        }

        readonly List<int> fangCut = new List<int>();

        /// Small enemy shots within `r` of a thrown sabre are cut out of the air (as swings cut them).
        void CutShotsNear(V2 at, float r)
        {
            fangCut.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Radius <= 8f && b.Pos.DistanceTo(at) <= r + b.Radius)
                    fangCut.Add(b.Id);
            foreach (int id in fangCut)
            {
                AddFx(FxKind.Spark, Bullets[id].Pos, TwinMoon, 8f, 0.2f);
                Bullets.Remove(id);
                pendingRemoved.Add(id);
            }
        }

        /// Host: the weapon-action key with the Twin Blades, aimed at `aimAt`: Twin Fang, if it is ready.
        void HostTwinFang(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.TwinBlades || p.FangCd > 0f || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            V2 to = aimAt - p.Pos;
            float len = to.Length;
            V2 dir = len > 1f ? to / len : V2.FromAngle(p.Aim);
            V2 target = ClampToArena(p.Pos + dir * Math.Min(FangReach, Math.Max(60f, len)), 10f);
            p.FangCd = FangCooldown;
            p.FireCd = Math.Max(p.FireCd, TwinBladesMotion.FangTotal);  // no swings until the sabres are caught
            p.Combo &= ~7;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Twin && s.Delay > 0f);  // a step still winding up never lands
            fangRuns.Add(new FangRun { Owner = p.Id, From = p.Pos, To = target });
            BroadcastFx(FxKind.TwinFang, p.Pos, target, TwinMoon, p.Id, TwinBladesMotion.FangTotal, Sfx.TwinThrow);
        }

        /// Host: a step (1-5) or a thrown sabre (6 passing, 7 the burst) cut `hits` enemies. Sent as an effect that draws nothing: it
        /// carries the cut's sound, and the swordsman's own game holds the picture and shakes (Game/Juice.cs, TwinHitFeel).
        void TwinHitCue(Player p, int kind, int hits)
        {
            BroadcastFx(FxKind.TwinHit, p.Pos, new V2(p.Id, kind), TwinMoon, hits, 0.2f, kind == 5 || kind == 7 ? Sfx.TwinHitHeavy : Sfx.TwinHit);
        }

        /// Raising the parry cancels the chain (ResetAttackChains): back to the first step, a step still winding up never lands.
        void ResetTwinChain(Player p)
        {
            p.Combo &= ~7;
            p.ComboT = 0f;
            p.FireCd = 0f;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Twin && s.Delay > 0f);
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        int twinCountSeen = -1;

        /// Each step moves you forward a little as it plays (the host counts the steps; a change in the count is a new one).
        void LocalTwinStep(Player me, float dt)
        {
            fangLocalCd = Math.Max(0f, fangLocalCd - dt);
            int count = me.Combo >> 3;
            if (twinCountSeen < 0 || me.Weapon.Id != WeaponId.TwinBlades)
            {
                twinCountSeen = count;
                return;
            }
            if (count == twinCountSeen)
                return;
            twinCountSeen = count;
            int step = TwinBladesMotion.StepOf(me.Combo);
            if (step < 1 || me.Downed || me.StunT > 0f || DashT > 0f || me.Parrying || KatanaStriking)
                return;
            var d = TwinBladesMotion.Steps[step - 1];
            V2 dir = V2.FromAngle(me.Aim);
            V2 to = ClampToArena(me.Pos + dir * d.Fwd, me.Def.BodyR);
            float time = d.Dur * 0.55f;
            DashDir = dir;
            dashSpeed = d.Fwd / time;
            DashT = to.DistanceTo(me.Pos) / dashSpeed;
        }
    }
}

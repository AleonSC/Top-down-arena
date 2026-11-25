using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Twin Blades (DPS Knight): the Crescent Waltz (Docs/Mockups/twin-blades-rework.html; the motion is TwinBladesMotion.cs).
    ///   - Chain: Rising Cut, Falling Cut, Double Jab, Pirouette, Crescent Shear, then it starts over. The next press counts once a
    ///     step is Next of the way through; TwinChainWindow after a step ends without one, the chain starts over too. Each step
    ///     moves you forward a little: the host counts the swings in Player.Combo's high bits, and your own game steps you
    ///     forward when the count changes (LocalTwinStep), as the Katana's dash does. The Shear launches a piercing crescent.
    ///   - Alt (the weapon-action key): Twin Fang. Both sabres are thrown as one spinning pair that flies to your cursor and
    ///     keeps following it for FangFollow seconds, cutting every enemy it spins through (each again every FangRehit) and
    ///     small shots too. Enemies don't stop it; a wall or pillar (or the arena's edge) does, and then it flies back to your
    ///     hand. You can't swing until you catch it; the cooldown counts from the catch. Your game sends Msg.WeaponDash
    ///     (DashStrike); the host flies the pair after Player.AimAt and replicates it (Player.FangOut, FangAt).
    /// Each step is one effect (FxKind.TwinStep) and each throw one (FxKind.TwinFang), drawn from their age (Game/TwinBladesLook.cs).
    public partial class Game
    {
        public const float TwinChainWindow = 0.7f;
        public const float TwinCutArc = 110f, TwinSpinReach = 92f, TwinJabReach = 130f, TwinJabHalfWidth = 10f, TwinShearArc = 200f;
        public static readonly float[] TwinMult = { 1f, 1f, 0.75f, 0.8f, 2.75f };  // damage of each hit of each step against the weapon's Damage (16, 16, 12 x2, 13 x2, 44)
        public const float TwinWaveMult = 1.875f;                                    // the Shear's crescent: 30
        public const float FangSpeed = 620f, FangSteer = 8f, FangFollow = 2.2f, FangHitR = 44f, FangRehit = 0.2f, FangMult = 0.75f;
        public const float FangReturnSpeed = 950f, FangCatchR = 24f, FangWallPad = 16f, FangCooldown = 3.5f, FangSpinRate = 18f;
        public static readonly Rgba TwinMoon = new Rgba(0.67f, 0.84f, 1f);

        class FangRun
        {
            public int Owner;
            public V2 Pos, Vel;
            public float T;          // seconds since the press
            public float Follow;     // seconds it follows the cursor (Whirling Sabres: longer)
            public bool Back;        // flying back to the hand (a wall stopped it, or its time ran out)
            public bool Great;       // a Greatsword's Moonwheel (GreatswordChain.cs FlyWheel): out to Target and straight back
            public V2 Target;
            public float CueT;       // until the next hit cue (so a long cut doesn't shake every tick)
            public readonly Dictionary<int, float> Hit = new Dictionary<int, float>();  // enemy id -> when it can be cut again
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
                    TwinSwing(p, aim, w.Reach, TwinCutArc, hit, d.Dur * 0.5f, TwinSfx(p, step == 1 ? Sfx.TwinCut1 : Sfx.TwinCut2), step);
                    break;
                case 3:
                    for (int k = 0; k < 2; k++)
                    {
                        var s = TwinSwing(p, aim, TwinJabReach, 0f, hit, d.Dur * (k == 0 ? 0.27f : 0.59f), TwinSfx(p, Sfx.TwinJab), step);
                        s.HalfWidth = TwinJabHalfWidth;
                        s.Thrust = true;
                    }
                    break;
                case 4:
                    TwinSwing(p, aim, TwinSpinReach, 360f, hit, d.Dur * 0.35f, TwinSfx(p, Sfx.TwinSpin), step);
                    var spin = TwinSwing(p, aim, TwinSpinReach, 360f, hit, d.Dur * 0.7f, Sfx.None, step);  // (the first's whirl covers both turns)
                    spin.Waves = extra;  // the +1 upgrade: the Pirouette throws crescents too
                    spin.WaveDmg = w.Damage * dmg * TwinWaveMult;
                    break;
                default:
                {
                    var s = TwinSwing(p, aim, w.Reach, TwinShearArc, hit, d.Dur * 0.53f, Sfx.None, step);
                    s.Heavy = true;
                    s.Waves = 1 + extra;
                    s.WaveDmg = w.Damage * dmg * TwinWaveMult;
                    break;
                }
            }
            // The whole move, drawn from its age: Radius carries who and which step.
            BroadcastFx(FxKind.TwinStep, p.Pos, p.Pos + V2.FromAngle(aim) * MeleeReach(p, w.Reach), TwinMoon, p.Id * 10 + step,
                d.Dur + TwinBladesMotion.Recover, step == 5 ? TwinSfx(p, Sfx.TwinShear) : Sfx.None);  // (the Shear's sound starts with its wind-up)
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
            QueueSwing(s);
            return s;
        }

        /// Host, every tick: the chain forgets its place, thrown sabres fly.
        void TickTwinBlades(float dt)
        {
            foreach (var p in Players.Values)
            {
                p.FangCd = Math.Max(0f, p.FangCd - dt);
                if (p.FangOut != 0 && !fangRuns.Exists(r => r.Owner == p.Id))
                    p.FangOut = 0;  // (a run reset dropped the pair)
                if (p.Weapon.Id == WeaponId.Censer || p.Weapon.Id == WeaponId.BloodLash || p.Weapon.Id == WeaponId.BloodBrawler || p.Weapon.Id == WeaponId.BloodBlast)
                    continue;  // the Censer, the Blood Lash, the Blood Brawler and the Blood Blast keep their own step in Combo (Censer.cs, BloodLash.cs, BloodBrawler.cs, BloodBlast.cs)
                if (TwinBladesMotion.StepOf(p.Combo) != 0 && ((p.ComboT -= dt) <= 0f || p.Downed || (p.Weapon.Id != WeaponId.TwinBlades && p.Weapon.Id != WeaponId.Lightbringer && p.Weapon.Id != WeaponId.Lance && p.Weapon.Id != WeaponId.BulwarkBarrier && p.Weapon.Id != WeaponId.Halberd && p.Weapon.Id != WeaponId.ArcKnuckles && p.Weapon.Id != WeaponId.Nunchaku && p.Weapon.Id != WeaponId.TwinTessen)))  // (the Lightbringer's, the Lance's, the Halberd's and the Arc Knuckles' chains share Combo)
                    p.Combo &= ~7;  // back to the first step (the swing count stays)
            }
            for (int i = fangRuns.Count - 1; i >= 0; i--)
            {
                var r = fangRuns[i];
                Player p;
                if (!Players.TryGetValue(r.Owner, out p) || p.Weapon.Id != (r.Great ? WeaponId.Greatsword : WeaponId.TwinBlades) || r.T > TwinBladesMotion.FangRelease + r.Follow + 6f)
                {
                    fangRuns.RemoveAt(i);
                    if (p != null)
                        p.FangOut = 0;
                    continue;
                }
                if (!FlyFang(p, r, dt))
                {
                    fangRuns.RemoveAt(i);
                    p.FangOut = 0;
                    p.FangCd = r.Great ? WheelCooldown : FangCooldown;
                    if (!p.Downed)
                        BroadcastSound(TwinSfx(p, Sfx.TwinCatch));  // both sabres (or the greatsword) back in hand
                }
            }
        }

        /// One tick of a thrown pair: out of the hand, after the cursor, back on a wall or when time runs out. False once caught.
        bool FlyFang(Player p, FangRun r, float dt)
        {
            r.T += dt;
            r.CueT -= dt;
            p.FireCd = Math.Max(p.FireCd, 0.1f);  // no swings until they're caught
            if (r.Great)
                return FlyWheel(p, r, dt);
            if (r.T < TwinBladesMotion.FangRelease)
            {
                r.Pos = p.Pos;
                p.FangOut = 1;
                p.FangAt = p.Pos;
                return true;
            }
            if (p.FangOut == 1)
            {
                V2 aim = p.AimAt - p.Pos;  // released: thrown hard at the cursor
                r.Vel = (aim.LengthSq > 1f ? aim.Normalized() : V2.FromAngle(p.Aim)) * FangSpeed;
                r.Pos = p.Pos + r.Vel.Normalized() * 18f;
                p.FangOut = 2;
            }
            if (!r.Back)
            {
                V2 to = p.AimAt - r.Pos;
                float d = to.Length;
                V2 want = d > 1f ? to / d * Math.Min(FangSpeed, d * 5f) : V2.Zero;  // eases in over the cursor and hovers there
                r.Vel += (want - r.Vel) * Math.Min(1f, FangSteer * dt);
                r.Pos += r.Vel * dt;
                if (FangHitsWall(r.Pos))
                {
                    r.Back = true;
                    BroadcastFx(FxKind.Ring, r.Pos, TwinMoon, 26f, 0.25f, TwinSfx(p, Sfx.TwinCross));  // the clang on the wall
                    TwinHitCue(p, 7, 0);
                }
                else if (r.T >= TwinBladesMotion.FangRelease + r.Follow)
                    r.Back = true;
            }
            else
            {
                V2 to = p.Pos - r.Pos;
                float d = to.Length, step = FangReturnSpeed * dt;
                if (d <= FangCatchR || d <= step)
                    return false;
                r.Vel = to / d * FangReturnSpeed;
                r.Pos += r.Vel * dt;
            }
            p.FangOut = (byte)(r.Back ? 3 : 2);
            p.FangAt = r.Pos;
            // the spinning pair cuts what it passes through: each enemy again every FangRehit, hits don't stop it
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * FangMult * p.Stats.FangDmgMult;  // (Whirling Sabres: harder)
            int cut = 0;
            foreach (var e in FoesInCircle(r.Pos, FangHitR, by: p.Id))
            {
                float next;
                if (r.Hit.TryGetValue(e.Id, out next) && r.T < next)
                    continue;
                r.Hit[e.Id] = r.T + FangRehit;
                FangHurt(p, e, dmg, r.Pos);
                cut++;
            }
            CutShotsNear(r.Pos, FangHitR);
            if (cut > 0 && r.CueT <= 0f)
            {
                r.CueT = 0.12f;
                TwinHitCue(p, 6, cut);
            }
            return true;
        }

        /// A wall or pillar, or the arena's edge, where the spinning pair is.
        bool FangHitsWall(V2 at)
        {
            V2 lo = ArenaMin, hi = ArenaMax;
            if (at.X < lo.X + FangWallPad || at.Y < lo.Y + FangWallPad || at.X > hi.X - FangWallPad || at.Y > hi.Y - FangWallPad)
                return true;
            return LayoutOn && NearSolid(at, FangWallPad);
        }

        void FangHurt(Player p, Enemy e, float dmg, V2 from)
        {
            V2 dir = e.Pos - from;
            WeaponHurt(e, dmg, p.Id);
            ModHit(e, p.Id, dmg, Defs.KnightSlash, dir.IsZero ? new V2(1f, 0f) : dir.Normalized());
        }

        readonly List<int> fangCut = new List<int>();

        /// Small enemy shots within `r` of a thrown sabre are cut out of the air (as swings cut them).
        void CutShotsNear(V2 at, float r)
        {
            fangCut.Clear();
            foreach (var b in Bullets.Values)
                if (Defs.IsEnemyShot(b.Kind) && !Defs.Unblockable(b.Kind) && b.Radius <= 8f && b.Pos.WithinReach(at, r + b.Radius))
                    fangCut.Add(b.Id);
            foreach (int id in fangCut)
            {
                AddFx(FxKind.Spark, Bullets[id].Pos, TwinMoon, 8f, 0.2f);
                KillBullet(id);
            }
        }

        /// Host: the weapon-action key with the Twin Blades: Twin Fang, if it is ready and the sabres are in hand.
        void HostTwinFang(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.TwinBlades || p.FangCd > 0f || p.FangOut != 0 || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            p.AimAt = aimAt;
            p.FangOut = 1;
            p.FangAt = p.Pos;
            p.FireCd = Math.Max(p.FireCd, TwinBladesMotion.FangRelease);
            p.Combo &= ~7;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.Twin && s.Delay > 0f);  // a step still winding up never lands
            fangRuns.Add(new FangRun { Owner = p.Id, Pos = p.Pos, Follow = FangFollow * p.Stats.FangTimeMult });
            BroadcastFx(FxKind.TwinFang, p.Pos, aimAt, TwinMoon, p.Id, TwinBladesMotion.FangRelease + 0.3f, TwinSfx(p, Sfx.TwinThrow));
        }

        /// Host: a step (1-5) or the thrown sabres (6 cutting, 7 striking a wall) cut `hits` enemies. Sent as an effect that draws nothing: it
        /// carries the cut's sound, and the swordsman's own game holds the picture and shakes (Game/Juice.cs, TwinHitFeel).
        void TwinHitCue(Player p, int kind, int hits)
        {
            BroadcastFx(FxKind.TwinHit, p.Pos, new V2(p.Id, kind), TwinMoon, hits, 0.2f, TwinSfx(p, kind == 5 || kind == 7 ? Sfx.TwinHitHeavy : Sfx.TwinHit));
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
            if (me.FangOut != 0)
                fangLocalCd = me.Weapon.Id == WeaponId.Greatsword ? WheelCooldown : FangCooldown;  // the cooldown counts from the catch
            else
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

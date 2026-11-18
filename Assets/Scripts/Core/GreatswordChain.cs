using System;

namespace Arena
{
    /// The Greatsword's ten-step chain and its Moonwheel (Docs/Mockups/greatsword-chain-forge.html, the owner's chain from it).
    ///   - Chain (FireGreatsword): Undertow, Pommel Bash, Cross Cut, Pommel Bash, Backstep Reap, Comet Leap, Pommel Bash, Undertow,
    ///     Full Moon, Eclipse, then round again. A press counts once the step before has played (GreatMoves[..].Dur, quicker with fire
    ///     rate); GreatChainWindow after a step ends without one, the chain starts over. Player.GreatStep (replicated) is the step
    ///     that played last, 1 to 10; Player.Combo counts the swings above its low three bits (as the Twin Blades' do), so your own
    ///     game sees every new step and moves you for it (LocalGreatStep: Backstep Reap hops back, Comet Leap jumps forward).
    ///   - Each hit lands when the blade gets there (its Swing's Delay). Sweeps are Slash effects (MeleeLooks.cs draws the sword through
    ///     them; GreatSweepSign says which way). Backstep Reap, Full Moon and Eclipse leave crescents that stop bullets (SteelWall).
    ///   - Moonwheel (the weapon-action key; HostMoonwheel): the sword is thrown spinning to the cursor (WheelMin to WheelMax px out),
    ///     cutting every enemy it passes once on the way out and once on the way back, and small shots; a wall turns it back. You
    ///     can't swing until you catch it; the cooldown counts from the catch. It flies as a Twin Fang run (TwinBlades.cs FlyFang,
    ///     FangRun.Great) and replicates through Player.FangOut and FangAt. The old guard and Rend (Greatsword.cs) are kept, unused.
    /// The look is Game/GreatswordLook.cs.
    public partial class Game
    {
        public enum GreatMove : byte { None, Undertow, Pommel, Cross, Backstep, Leap, FullMoon, Eclipse }

        public struct GreatMoveDef
        {
            public string Name;
            public float Dur;     // seconds before the next press counts (at fire rate 1)
            public float Wind;    // seconds from the press to its (first) hit
            public float Mult;    // damage of each hit against the weapon's Damage
            public float Fwd;     // px your own game moves you along the aim with it (negative: back)
            public float MoveTime;
        }

        /// The chain, in the order it plays (the owner's pick).
        public static readonly GreatMove[] GreatChain =
        {
            GreatMove.Undertow, GreatMove.Pommel, GreatMove.Cross, GreatMove.Pommel, GreatMove.Backstep,
            GreatMove.Leap, GreatMove.Pommel, GreatMove.Undertow, GreatMove.FullMoon, GreatMove.Eclipse,
        };

        public static readonly GreatMoveDef[] GreatMoves =
        {
            default(GreatMoveDef),
            new GreatMoveDef { Name = "Undertow", Dur = 0.55f, Wind = 0.23f, Mult = 0.55f },
            new GreatMoveDef { Name = "Pommel Bash", Dur = 0.22f, Wind = 0.07f, Mult = 0.325f, Fwd = 14f, MoveTime = 0.07f },
            new GreatMoveDef { Name = "Cross Cut", Dur = 0.48f, Wind = 0.1f, Mult = 0.65f, Fwd = 10f, MoveTime = 0.2f },
            new GreatMoveDef { Name = "Backstep Reap", Dur = 0.41f, Wind = 0.14f, Mult = 0.775f, Fwd = -70f, MoveTime = 0.2f },
            new GreatMoveDef { Name = "Comet Leap", Dur = 0.68f, Wind = 0.38f, Mult = 1.55f, Fwd = 150f, MoveTime = 0.38f },
            new GreatMoveDef { Name = "Full Moon", Dur = 0.66f, Wind = 0.3f, Mult = 1.2f },
            new GreatMoveDef { Name = "Eclipse", Dur = 1.08f, Wind = 0.45f, Mult = 4.25f },
        };

        public const float GreatChainWindow = 0.7f;
        public const float UndertowReach = 155f, UndertowArc = 210f, UndertowPull = -55f;
        public const float PommelReach = 58f, PommelArc = 70f, PommelStun = 0.8f;
        public const float CrossReach = 112f, CrossArc = 110f, CrossSecond = 0.29f;
        public const float BackstepReach = 126f, BackstepArc = 190f, BackstepWall = 0.7f;
        public const float LeapRadius = 88f, LeapKnock = 60f;
        public const float MoonReach = 108f, MoonWall = 0.5f;
        public const float EclipseReach = 290f, EclipseHalfWidth = 34f, EclipseRing = 120f, EclipseRingAt = 130f, EclipseRingMult = 0.65f, EclipseWall = 1.2f, EclipseKnock = 60f;
        public const float GreatWaveDmg = 45f;  // an Eclipse sword wave (an extra projectile each)

        public const float WheelMin = 140f, WheelMax = 290f, WheelSpeed = 700f, WheelReturnSpeed = 820f, WheelHitR = 44f, WheelMult = 0.875f;
        public const float WheelRelease = 0.12f, WheelCooldown = 4f, WheelSpinRate = 22f;

        /// The move a chain step (1 to 10) plays.
        public static GreatMove GreatMoveOf(int step) => step >= 1 && step <= GreatChain.Length ? GreatChain[step - 1] : GreatMove.None;

        /// Which way a step's sweep goes on screen (+1 the aim's clockwise way; Backstep Reap and Cross Cut's second cut come back).
        public static float GreatSweepSign(GreatMove m, int hit) => m == GreatMove.Backstep || (m == GreatMove.Cross && hit == 1) ? -1f : 1f;

        /// Host: one press of the primary with the Greatsword (from FireKnightWeapon): the next step of the chain.
        void FireGreatsword(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int step = p.GreatStep >= 1 && p.GreatStep < GreatChain.Length ? p.GreatStep + 1 : 1;
            var m = GreatChain[step - 1];
            var d = GreatMoves[(int)m];
            p.GreatStep = (byte)step;
            p.GreatGap = 0f;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = count << 3;  // (low bits 0: the Twin Blades' chain step stays clear)
            p.FireCd = d.Dur / FireRate(p);
            float hit = w.Damage * dmg * d.Mult;
            V2 dir = V2.FromAngle(aim);
            switch (m)
            {
                case GreatMove.Undertow:
                {
                    var s = GreatSwing(p, aim, UndertowReach, UndertowArc, hit, d.Wind, m);
                    s.Knock = UndertowPull;  // a negative shove drags them in
                    break;
                }
                case GreatMove.Pommel:
                {
                    var s = GreatSwing(p, aim, PommelReach, PommelArc, hit, d.Wind, m);
                    s.Stun = PommelStun;
                    s.StarStun = true;
                    s.NoFx = true;  // the look draws the hilt driving out (GreatswordLook.cs)
                    s.Sound = Sfx.Clang;
                    break;
                }
                case GreatMove.Cross:
                    GreatSwing(p, aim - 0.35f, CrossReach, CrossArc, hit, d.Wind, m);
                    GreatSwing(p, aim + 0.35f, CrossReach, CrossArc, hit, CrossSecond, m);
                    break;
                case GreatMove.Backstep:
                {
                    var s = GreatSwing(p, aim, BackstepReach, BackstepArc, hit, d.Wind, m);
                    SteelWall(p, aim, s.Reach, s.HalfArc, BackstepWall, d.Wind);
                    break;
                }
                case GreatMove.Leap:
                {
                    // The landing: a ring where your own game lands you (clamped to the arena as it clamps you).
                    V2 land = ClampToArena(p.Pos + dir * d.Fwd, p.Def.BodyR);
                    var s = GreatSwing(p, aim, 0f, 0f, hit, d.Wind, m);
                    s.FromSet = true;
                    s.From = p.Pos;
                    s.CircleAt = land.DistanceTo(p.Pos);
                    s.Circle = MeleeReach(p, LeapRadius);
                    s.Knock = LeapKnock;
                    s.Heavy = true;
                    p.InvulnT = Math.Max(p.InvulnT, d.Wind);  // in the air
                    break;
                }
                case GreatMove.FullMoon:
                {
                    var s = GreatSwing(p, aim, MoonReach, 360f, hit, d.Wind, m);
                    s.Heavy = true;
                    SteelWall(p, aim, s.Reach, MathX.Pi, MoonWall, d.Wind);
                    break;
                }
                default:  // Eclipse: the blade goes up, then a golden lance of light and a ring where it strikes
                {
                    var s = GreatSwing(p, aim, EclipseReach, 0f, hit, d.Wind, m);
                    s.HalfWidth = EclipseHalfWidth;
                    s.Rend = true;  // drawn as the old Rend's lance at full Temper: gold (FxKind.GreatRend)
                    s.Power = 1f;
                    s.Heavy = true;
                    s.Knock = EclipseKnock;
                    s.Waves = extra;
                    s.WaveDmg = GreatWaveDmg * dmg;
                    var r = GreatSwing(p, aim, 0f, 0f, w.Damage * dmg * EclipseRingMult, d.Wind + 0.03f, m);
                    r.Circle = MeleeReach(p, EclipseRing);
                    r.CircleAt = MeleeReach(p, EclipseRingAt);
                    r.Knock = EclipseKnock;
                    r.Silent = true;
                    float reach = MeleeReach(p, EclipseReach);
                    SteelWall(p, aim, reach, (float)Math.Asin(Math.Min(1f, 44f / reach)), EclipseWall, d.Wind);
                    BroadcastSound(Sfx.GreatWindup);
                    break;
                }
            }
        }

        /// One hit of a step, landing `delay` seconds from the press.
        Swing GreatSwing(Player p, float aim, float reach, float arcDeg, float dmg, float delay, GreatMove m)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Delay = delay;
            s.Great = (byte)m;
            QueueSwing(s);
            return s;
        }

        /// Host: the weapon-action key with the Greatsword: Moonwheel, if it is ready and the sword is in hand.
        void HostMoonwheel(Player p, V2 aimAt)
        {
            if (p.Weapon.Id != WeaponId.Greatsword || p.FangCd > 0f || p.FangOut != 0 || p.Downed || p.StunT > 0f || p.ParryActive || State == MatchState.GameOver)
                return;
            V2 to = aimAt - p.Pos;
            float aim = to.LengthSq > 1f ? to.Angle : p.Aim;
            float dist = Math.Max(WheelMin, Math.Min(WheelMax * p.Stats.WheelRangeMult, to.Length));
            p.AimAt = aimAt;
            p.FangOut = 1;
            p.FangAt = p.Pos;
            ResetGreatChain(p);
            p.FireCd = Math.Max(p.FireCd, WheelRelease);
            fangRuns.Add(new FangRun { Owner = p.Id, Pos = p.Pos, Great = true, Target = ClampToArena(p.Pos + V2.FromAngle(aim) * dist, 20f) });
            BroadcastSound(Sfx.TwinThrow);
        }

        /// One tick of a thrown greatsword: out to its point, then back to the hand. False once caught.
        bool FlyWheel(Player p, FangRun r, float dt)
        {
            if (r.T < WheelRelease)
            {
                r.Pos = p.Pos;
                p.FangOut = 1;
                p.FangAt = p.Pos;
                return true;
            }
            if (!r.Back)
            {
                V2 to = r.Target - r.Pos;
                float d = to.Length, step = WheelSpeed * dt;
                if (d <= step)
                {
                    r.Pos = r.Target;
                    r.Back = true;
                    r.Hit.Clear();  // everything can be cut again on the way back
                }
                else
                    r.Pos += to / d * step;
                if (!r.Back && FangHitsWall(r.Pos))
                {
                    r.Back = true;
                    r.Hit.Clear();
                    BroadcastFx(FxKind.Ring, r.Pos, TwinMoon, 26f, 0.25f, Sfx.TwinCross);  // the clang on the wall
                }
            }
            else
            {
                V2 to = p.Pos - r.Pos;
                float d = to.Length, step = WheelReturnSpeed * dt;
                if (d <= FangCatchR || d <= step)
                    return false;
                r.Pos += to / d * step;
            }
            p.FangOut = (byte)(r.Back ? 3 : 2);
            p.FangAt = r.Pos;
            float dmg = p.Weapon.Damage * p.Stats.DmgMult * WheelMult * p.Stats.WheelDmgMult;  // (Lunar Arc: harder)
            int cut = 0;
            foreach (var e in Enemies.Values)
            {
                if (!Targetable(e) || e.Pos.FartherThan(r.Pos, e.Radius + WheelHitR) || r.Hit.ContainsKey(e.Id))
                    continue;
                r.Hit[e.Id] = r.T;  // once a leg
                FangHurt(p, e, dmg, r.Pos);
                cut++;
            }
            CutShotsNear(r.Pos, WheelHitR);
            if (cut > 0 && r.CueT <= 0f)
            {
                r.CueT = 0.12f;
                TwinHitCue(p, 6, cut);
            }
            return true;
        }

        // --- Your own game ------------------------------------------------------------------------------------------

        int greatCountSeen = -1;

        /// Each step that moves you does it as it plays (the host counts the steps in Combo; a change in the count is a new one).
        void LocalGreatStep(Player me)
        {
            int count = me.Combo >> 3;
            if (greatCountSeen < 0 || me.Weapon.Id != WeaponId.Greatsword)
            {
                greatCountSeen = count;
                return;
            }
            if (count == greatCountSeen)
                return;
            greatCountSeen = count;
            var d = GreatMoves[(int)GreatMoveOf(me.GreatStep)];
            if (d.Fwd == 0f || me.Downed || me.StunT > 0f || DashT > 0f || me.Parrying || KatanaStriking)
                return;
            V2 dir = V2.FromAngle(me.Aim) * Math.Sign(d.Fwd);
            V2 to = ClampToArena(me.Pos + dir * Math.Abs(d.Fwd), me.Def.BodyR);
            DashDir = dir;
            dashSpeed = Math.Abs(d.Fwd) / d.MoveTime;
            DashT = to.DistanceTo(me.Pos) / dashSpeed;
        }
    }
}

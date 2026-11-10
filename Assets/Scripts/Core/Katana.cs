using System;
using System.Collections.Generic;

namespace Arena
{
    /// The Katana's two stance combos (Docs/KatanaCombo.md; mockup Docs/Mockups/katana-stance-cancels.html). The weapon-action key
    /// switches stance at once; the stance you leave keeps its place in its combo for as long as you're away, so the skill is
    /// storing a combo one step from its finisher and calling it when it counts. The stance you're in starts over after
    /// KatanaIdleReset without a step.
    ///
    /// Hayate (0) dashes and slices: Kiri and Kaeshi (dash-cuts, the second angled to the other side), Tsumuji (a short dash into
    /// a spin), then the Momentum Burst: the blade stabbed into the ground, a shockwave that shoves enemies away, cuts small
    /// bullets and marks what it hits. Senbon (1) is the fancy one: Agari and Kaeshi (sweeps), Engetsu (a step into a full-moon
    /// spin), Senbon (six crossing slashes in a cone), then Caltrops: a storm of them thrown round you, lying for a while,
    /// hurting and slowing what steps on them. The two finishers feed each other: a Burst flings the caltrops lying on the
    /// ground at the nearest enemies, and caltrops thrown after a Burst seek the enemies it marked.
    ///
    /// As with other weapon moves, your own game moves you (each dash and step, when KatanaDashes changes: LocalKatana); the host
    /// lands the cuts on time (pendingSwings and TickKatanaLater) and sends the look (FxKind.KatanaDash, KatanaSweep, KatanaSlash,
    /// KatanaBurst, KatanaCut; Game/KatanaLook.cs). Caltrops are bullets (Defs.Caltrop) the host steers.
    public partial class Game
    {
        public static readonly Rgba KatanaRose = new Rgba(1f, 0.56f, 0.66f);   // Senbon
        public static readonly Rgba KatanaSteel = new Rgba(0.745f, 0.87f, 1f);  // Hayate
        public static readonly string[] KatanaHayNames = { "Kiri", "Kaeshi", "Tsumuji", "Momentum Burst" }, KatanaSenNames = { "Agari", "Kaeshi", "Engetsu", "Senbon", "Caltrops" };
        public static int KatanaSteps(int stance) => stance == 0 ? 4 : 5;

        /// A step that moves you: how far, how long, and how far off your aim (Kaeshi zig-zags by the count's parity). code = stance * 8 + step.
        public static float KatanaStepMove(int code) => code == 0 || code == 1 ? KatanaDashLen : code == 2 ? KatanaSpinDash : code == 10 ? KatanaMoonStep : 0f;
        public static float KatanaStepTime(int code) => code == 2 ? KatanaSpinDashTime : code == 10 ? KatanaMoonStepTime : KatanaDashTime;
        public static float KatanaStepTurn(int code, int count) => code == 1 ? KatanaZig * ((count & 1) == 0 ? 1f : -1f) : 0f;
        static bool KatanaMoves(int code) => code == 0 || code == 1 || code == 2 || code == 3 || code == 10;  // (the Burst plants you)

        class KatanaLater
        {
            public int Owner;
            public float T;
            public Action Run;
        }

        class Caltrop
        {
            public Bullet B;
            public int Owner, Target;
            public byte Mode;  // 0 thrown (in the air), 1 lying, 2 flying at an enemy
            public float T;
        }

        readonly List<KatanaLater> katanaLater = new List<KatanaLater>();
        readonly List<Caltrop> caltrops = new List<Caltrop>();

        // Local (your own screen): the steps you've moved along, the switch's cooldown, the Burst's plant.
        byte katanaDashesSeen;
        float katanaSwitchLocalCd, katanaPlantT;
        public bool KatanaStriking => katanaPlantT > 0f;  // planted for the Momentum Burst: you can't move or swing

        void Later(Player p, float t, Action run) => katanaLater.Add(new KatanaLater { Owner = p.Id, T = t, Run = run });

        // --- The steps (host) -------------------------------------------------------------------------------------------

        /// The next step of the stance you're in.
        void FireKatana(Player p, float aim, float dmg, int extra)
        {
            int stance = p.KatanaStance, idx = stance == 0 ? p.KatanaHay : p.KatanaSen, code = stance * 8 + idx;
            if (stance == 0)
                p.KatanaHay = (byte)((idx + 1) % 4);
            else
                p.KatanaSen = (byte)((idx + 1) % 5);
            p.KatanaStep = (byte)code;
            p.KatanaIdle = 0f;
            p.FireCd = (stance == 0 ? KatanaHayNext : KatanaSenNext)[idx] / FireRate(p);
            float w = p.Weapon.Damage * dmg, ang = aim;
            V2 from = p.Pos, to = from;
            if (KatanaMoves(code))
            {
                p.KatanaDashes++;
                ang = aim + KatanaStepTurn(code, p.KatanaDashes);
                float move = KatanaStepMove(code);
                if (move > 0f)
                    to = ClampToArena(from + V2.FromAngle(ang) * MeleeReach(p, move), p.Def.BodyR);
            }
            var col = stance == 0 ? KatanaSteel : KatanaRose;
            switch (code)
            {
                case 0:
                case 1:  // Kiri, Kaeshi: a dash-cut along the line
                {
                    p.InvulnT = Math.Max(p.InvulnT, KatanaDashTime + KatanaDashGrace);
                    KatanaDashFx(p, from, to, col);
                    for (int k = 0; k <= extra; k++)
                        KatanaLine(p, from, to, k == 0 ? w * KatanaDashMult : w * KatanaDashMult * KatanaExtraMult, KatanaDashTime * 0.5f + 0.05f * k, 1, k > 0, Sfx.KatanaLunge);
                    break;
                }
                case 2:  // Tsumuji: a short dash, then a spin where it ends
                {
                    p.InvulnT = Math.Max(p.InvulnT, KatanaSpinDashTime + KatanaDashGrace);
                    KatanaDashFx(p, from, to, col);
                    float reach = MeleeReach(p, KatanaSpinReach);
                    Later(p, KatanaSpinDashTime + 0.04f, () => KatanaSweepFx(p, to, ang, MathX.Tau, reach, 0.14f, col));
                    KatanaArc(p, to, ang, 360f, reach, w * KatanaSpinMult, KatanaSpinDashTime + 0.1f, Sfx.KatanaSwipe1);
                    break;
                }
                case 3:  // the Momentum Burst: the blade goes into the ground
                    Later(p, KatanaBurstAt, () => MomentumBurst(p, w, extra));
                    break;
                case 8:
                case 9:  // Agari, Kaeshi: sweeps, the second back the other way
                {
                    float arc = KatanaSwipeArc * MathX.Pi / 180f, side = idx == 0 ? 1f : -1f, reach = MeleeReach(p, p.Weapon.Reach);
                    Later(p, 0.05f, () => KatanaSweepFx(p, p.Pos, ang, side * arc, reach, KatanaSweepTime, col));
                    KatanaArc(p, null, ang, KatanaSwipeArc, reach, w * KatanaSwipeMult, 0.05f + KatanaSweepTime * 0.6f, idx == 0 ? Sfx.KatanaSwipe1 : Sfx.KatanaSwipe2);
                    break;
                }
                case 10:  // Engetsu: a step into a full-moon spin
                {
                    float reach = MeleeReach(p, KatanaMoonReach);
                    p.InvulnT = Math.Max(p.InvulnT, KatanaMoonStepTime + KatanaDashGrace);
                    Later(p, 0.04f, () => KatanaSweepFx(p, to, ang, MathX.Tau, reach, KatanaMoonTime, col));
                    KatanaArc(p, to, ang, 360f, reach, w * KatanaMoonMult, 0.04f + KatanaMoonTime * 0.6f, Sfx.KatanaSwipe1);
                    break;
                }
                case 11:  // Senbon: six crossing slashes in a cone ahead
                    BroadcastSound(Sfx.KatanaStrikeCut);
                    for (int k = 0; k < KatanaSlashes; k++)
                        Later(p, 0.02f + KatanaSlashGap * k, () => KatanaSlash(p, aim, w * KatanaSlashMult));
                    break;
                case 12:  // Caltrops
                    Later(p, KatanaCaltropAt, () => ThrowCaltrops(p, w, extra));
                    break;
            }
        }

        /// A dash-cut's line (a box from where it began), landing `delay` from now.
        void KatanaLine(Player p, V2 from, V2 to, float dmg, float delay, byte move, bool silent, Sfx sound)
        {
            V2 d = to - from;
            float len = Math.Max(20f, d.Length);
            var s = NewSwing(p, d.IsZero ? p.Aim : d.Angle, len, 20f, dmg);
            s.Reach = len;
            s.HalfWidth = KatanaDashWidth;
            s.FromSet = true;
            s.From = from;
            s.Heavy = true;
            s.NoFx = true;  // (the look is the step's own effect: KatanaLook.cs)
            s.KatanaMove = move;
            s.Delay = delay;
            s.Silent = silent;
            s.Sound = silent ? Sfx.None : sound;
            pendingSwings.Add(s);
        }

        /// A sweep or spin (`at` null: round where you stand when it lands).
        void KatanaArc(Player p, V2? at, float aim, float arcDeg, float reach, float dmg, float delay, Sfx sound)
        {
            var s = NewSwing(p, aim, reach, arcDeg, dmg);
            s.Reach = reach;
            if (at.HasValue)
            {
                s.FromSet = true;
                s.From = at.Value;
            }
            s.NoFx = true;
            s.KatanaMove = 2;
            s.Delay = delay;
            s.Sound = sound;
            pendingSwings.Add(s);
        }

        /// One of Senbon's slashes: a line across a point in the cone ahead.
        void KatanaSlash(Player p, float aim, float dmg)
        {
            float a = aim + Rand(-KatanaSlashCone, KatanaSlashCone), dist = Rand(60f, 125f), la = a + MathX.Pi / 2f + Rand(-0.9f, 0.9f);
            V2 c = p.Pos + V2.FromAngle(a) * dist, half = V2.FromAngle(la) * (KatanaSlashLen / 2f);
            V2 x0 = c - half, x1 = c + half;
            BroadcastFx(FxKind.KatanaSlash, x0, x1, KatanaRose, p.Id, 0.3f, Sfx.None);
            var s = NewSwing(p, la, KatanaSlashLen, 20f, dmg);
            s.Reach = KatanaSlashLen;
            s.HalfWidth = 12f;
            s.FromSet = true;
            s.From = x0;
            s.NoFx = true;
            s.Silent = true;
            s.KatanaMove = 2;
            LandSwing(p, s);
        }

        void KatanaDashFx(Player p, V2 from, V2 to, Rgba col) => BroadcastFx(FxKind.KatanaDash, from, to, col, p.Id, 0.42f, Sfx.None);

        /// A sweep's crescent: Pos where it's centred, Pos2 its start angle and signed arc, Radius the owner * 1000 + its reach.
        void KatanaSweepFx(Player p, V2 at, float aim, float arc, float reach, float time, Rgba col)
        {
            float a0 = arc >= MathX.Tau - 0.01f ? aim : aim - arc / 2f;
            BroadcastFx(FxKind.KatanaSweep, at, new V2(a0, arc), col, p.Id * 1000 + reach, time, Sfx.None);
        }

        /// A step's cut landed (LandSwing): every enemy hit gets its cut drawn (and splits if it died); a dash that killed brings
        /// the next step at once; the hit cue holds your picture a moment.
        void KatanaLanded(Player p, Swing s, List<Enemy> hits)
        {
            if (hits.Count == 0)
                return;
            V2 o = s.FromSet ? s.From : p.Pos;
            bool killed = false;
            foreach (var e in hits)
            {
                bool dead = e.Hp <= 0f;
                killed |= dead;
                V2 dir = s.HalfWidth > 0f ? V2.FromAngle(s.Aim) : (e.Pos - o).Normalized().Orthogonal();
                if (dir.IsZero)
                    dir = V2.FromAngle(s.Aim);
                KatanaCutFx(e, dir, dead, p.KatanaStance == 0 ? KatanaSteel : KatanaRose);
            }
            if (killed && s.KatanaMove == 1)
            {
                p.FireCd = 0f;  // a dash that kills brings the next step at once
            }
            if (!s.Silent)
                KatanaHitCue(p, false, hits.Count);
        }

        /// The look of one enemy cut: a line across it and, if it died, its two halves sliding apart in its own colour and shape.
        /// Pos2 is the cut's direction times the enemy's number of sides.
        void KatanaCutFx(Enemy e, V2 dir, bool dead, Rgba col)
        {
            BroadcastFx(FxKind.KatanaCut, e.Pos, dir * Math.Max(1, e.Def.Sides), e.Def.Color.WithAlpha(dead ? 1f : 0.5f), e.Radius, dead ? 0.9f : 0.3f, Sfx.None);
        }

        /// Host: a cut landed. Sent as an effect that draws nothing: it carries the cut's sound, and the striker's own game holds
        /// the picture a moment (Game/Juice.cs; harder for the Burst).
        void KatanaHitCue(Player p, bool heavy, int hits)
        {
            BroadcastFx(FxKind.KatanaHitCue, p.Pos, new V2(p.Id, 0f), KatanaRose.WithAlpha(heavy ? 1f : 0.5f), hits, 0.2f, Sfx.KatanaHit);
        }

        // --- The finishers --------------------------------------------------------------------------------------------------

        /// The Momentum Burst: shoves everything round you away, cuts it a little, cuts the small bullets in the ring, and marks
        /// what it hit. Caltrops lying on the ground fly at the nearest enemy.
        void MomentumBurst(Player p, float w, int extra)
        {
            if (p.Downed)
                return;
            float r = MeleeReach(p, KatanaBurstRadius) * (1f + 0.25f * extra);
            int hit = 0;
            foreach (var e in new List<Enemy>(Enemies.Values))
            {
                if (IsSpawning(e) || e.Hp <= 0f)
                    continue;
                V2 d = e.Pos - p.Pos;
                float dist = d.Length;
                if (dist > r + e.Radius)
                    continue;
                V2 away = dist > 1f ? d / dist : V2.FromAngle(p.Aim);
                if (!Heavy(e))
                    KnockEnemy(e, away * KatanaBurstPush);
                e.KatanaShockT = KatanaBurstMark;
                float dmg = w * KatanaBurstMult * NinjaHitMult(p.Id, Defs.KnightSlash, e);
                HurtEnemy(e, dmg, p.Id);
                OnBulletHit(new Bullet { Pos = e.Pos, Vel = away, Owner = p.Id, Dmg = dmg, Kind = Defs.KnightSlash }, e);
                KatanaCutFx(e, away.Orthogonal(), e.Hp <= 0f, KatanaSteel);
                hit++;
            }
            CutBulletsInCircle(p.Pos, r, false);  // the small bullets in the ring
            BroadcastFx(FxKind.KatanaBurst, p.Pos, new V2(p.Id, p.Aim), KatanaSteel, r, 0.9f, Sfx.KatanaBurstSfx);
            if (hit > 0)
                KatanaHitCue(p, true, hit);
            int flung = 0;
            foreach (var c in caltrops)
            {
                if (c.Owner != p.Id || c.Mode != 1)
                    continue;
                var t = NearestEnemy(c.B.Pos);
                if (t == null)
                    break;
                c.Mode = 2;
                c.Target = t.Id;
                c.T = 0f;
                c.B.Dmg = w * KatanaCaltropFlyMult;
                c.B.SlowAmt = 0f;
                c.B.Life = 3f;
                flung++;
            }
            if (flung > 0)
                BroadcastSound(Sfx.KatanaFling);
        }

        /// Caltrops: thrown out round you to lie on the ground; after a Burst, they seek the enemies it marked instead.
        void ThrowCaltrops(Player p, float w, int extra)
        {
            if (p.Downed)
                return;
            var marked = new List<Enemy>();
            foreach (var e in Enemies.Values)
                if (e.KatanaShockT > 0f && e.Hp > 0f && !IsSpawning(e))
                    marked.Add(e);
            int n = KatanaCaltropCount + 2 * extra;
            for (int i = 0; i < n; i++)
            {
                float a = MathX.Tau * i / n + Rand(-0.15f, 0.15f), r = Rand(60f, 150f);
                V2 land = ClampToArena(p.Pos + V2.FromAngle(a) * r, 6f);
                var c = new Caltrop { Owner = p.Id };
                if (marked.Count > 0)
                {
                    c.B = SpawnBullet(Defs.Caltrop, p.Pos, V2.FromAngle(a) * 240f, 0f, 3f, w * KatanaCaltropFlyMult, p.Id);
                    c.Mode = 2;
                    c.Target = marked[i % marked.Count].Id;
                }
                else
                {
                    c.B = SpawnBullet(Defs.Caltrop, p.Pos, (land - p.Pos) / KatanaToss, 0f, KatanaToss + KatanaCaltropLife, w * KatanaCaltropMult, p.Id);
                    c.B.Tossed = true;
                    c.B.SlowAmt = KatanaCaltropSlow;
                }
                c.B.Pulled = true;  // (the host steers it: its course is sent to every game)
                caltrops.Add(c);
            }
            BroadcastSound(Sfx.KatanaCaltrops);
            if (marked.Count > 0)
                BroadcastSound(Sfx.KatanaFling);
        }

        Enemy NearestEnemy(V2 at)
        {
            Enemy best = null;
            foreach (var e in Enemies.Values)
                if (!IsSpawning(e) && e.Hp > 0f && (best == null || e.Pos.DistanceSq(at) < best.Pos.DistanceSq(at)))
                    best = e;
            return best;
        }

        /// Host, every tick: the steps' timed cuts, the marks fading, and the caltrops landing and flying.
        void TickKatanaLater(float dt)
        {
            for (int i = 0; i < katanaLater.Count; i++)
            {
                var l = katanaLater[i];
                if ((l.T -= dt) > 0f)
                    continue;
                katanaLater.RemoveAt(i--);
                Player p;
                if (Players.TryGetValue(l.Owner, out p) && !p.Downed)
                    l.Run();
            }
            foreach (var e in Enemies.Values)
                if (e.KatanaShockT > 0f)
                    e.KatanaShockT = Math.Max(0f, e.KatanaShockT - dt);
            for (int i = caltrops.Count - 1; i >= 0; i--)
            {
                var c = caltrops[i];
                if (!Bullets.ContainsKey(c.B.Id))
                {
                    caltrops.RemoveAt(i);
                    continue;
                }
                c.T += dt;
                if (c.Mode == 0 && c.T >= KatanaToss)
                {
                    c.Mode = 1;  // landed: it lies still and hurts what steps on it
                    c.B.Vel = V2.Zero;
                    c.B.Tossed = false;
                }
                else if (c.Mode == 2)
                {
                    Enemy t;
                    if (!Enemies.TryGetValue(c.Target, out t) || t.Hp <= 0f)
                    {
                        t = NearestEnemy(c.B.Pos);
                        if (t == null)
                        {
                            c.B.Life = 0f;
                            continue;
                        }
                        c.Target = t.Id;
                    }
                    V2 d = t.Pos - c.B.Pos;
                    float k = (float)Math.Pow(0.02, dt);
                    c.B.Vel = c.B.Vel * k + (d.IsZero ? V2.Zero : d.Normalized() * KatanaCaltropSpeed) * (1f - k);
                }
            }
        }

        // --- The switch and the combos' memory ------------------------------------------------------------------------------

        /// Every tick, for every player: the switch's cooldown, and the stance you're in starting over after a while.
        void TickKatana(Player p, float dt)
        {
            p.KatanaSwitchCd = Math.Max(0f, p.KatanaSwitchCd - dt);
            p.KatanaIdle = Math.Min(p.KatanaIdle + dt, 10f);
            if (p.KatanaIdle > KatanaIdleReset)
            {
                if (p.KatanaStance == 0)
                    p.KatanaHay = 0;
                else
                    p.KatanaSen = 0;  // (only the stance you're in: the other keeps its place)
            }
        }

        /// The switch: the stance turns over at once and the step you're in stops (what hasn't landed of it never does).
        void HostKatanaSwitch(Player p)
        {
            if (p.Weapon.Id != WeaponId.Katana || p.Downed || p.StunT > 0f || p.KatanaSwitchCd > 0f)
                return;
            p.KatanaStance = (byte)(p.KatanaStance ^ 1);
            p.KatanaSwitchCd = KatanaSwitchCd - 0.05f;  // (a little slack for the trip from a guest's game, which keeps the real cooldown)
            p.KatanaIdle = 0f;
            p.FireCd = 0f;
            p.TapQueued = false;
            pendingSwings.RemoveAll(s => s.Owner == p.Id && s.KatanaMove > 0);
            katanaLater.RemoveAll(l => l.Owner == p.Id);
            BroadcastFx(FxKind.KatanaStanceFx, p.Pos, new V2(p.Id, p.KatanaStance), p.KatanaStance == 0 ? KatanaSteel : KatanaRose, 42f, 0.3f, Sfx.KatanaStanceSwap);
        }

        /// The Katana and Bo Staff fire on their own timing: a press during a step is remembered and starts the next step the
        /// moment this one allows it.
        void TickKatanaFire(Player p)
        {
            bool pressed = p.Firing && !p.WasFiring;
            p.WasFiring = p.Firing;
            if (pressed)
                p.TapQueued = true;
            if (p.ParryActive || State == MatchState.GameOver)
            {
                p.TapQueued = false;
                return;
            }
            if ((p.Firing || p.TapQueued) && p.FireCd <= 0f)
            {
                p.TapQueued = false;
                p.FireCd = p.Weapon.FireInterval / FireRate(p);  // (the step sets its own, in FireNinjaWeapon)
                FirePlayer(p);
            }
        }

        /// Raising the parry is an animation cancel for weapons that attack in a fixed order: the wait for the next step is
        /// cleared and an attack that hasn't landed yet never does. Builds you have earned (the Rapier's streak) are left alone.
        /// The Katana loses both combos' buildups (back to step 1 in each stance). Called on the host.
        void ResetAttackChains(Player p)
        {
            if (p.Weapon.Id == WeaponId.BoStaff)
            {
                p.BoStep = 0;  // back to the slash
                p.FireCd = 0f;
                p.TapQueued = false;
                if (boShoves.RemoveAll(sh => sh.Owner == p.Id) > 0)  // a shove still drawing back never lands (and costs no cooldown)
                    p.ShoveCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.FumaShuriken)
            {
                FumaParry(p);  // a star in flight caught into orbit, or Reversal (FumaOrbit.cs)
                return;
            }
            if (p.Weapon.Id == WeaponId.Kusarigama)
            {
                p.KusariStep = 0;  // back to the sickle cut (a throw already away still lands)
                p.FireCd = 0f;
                return;
            }
            if (p.Weapon.Id == WeaponId.Greatsword)
            {
                ResetGreatChain(p);  // Greatsword.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.TwinBlades)
            {
                ResetTwinChain(p);  // TwinBlades.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Lightbringer)
            {
                ResetLightChain(p);  // Lightbringer.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Censer)
            {
                ResetCenserChain(p);  // Censer.cs
                return;
            }
            if (p.Weapon.Id == WeaponId.Lance)
            {
                ResetLanceChain(p);  // Lance.cs
                return;
            }
            if (p.Weapon.Id != WeaponId.Katana)
                return;
            p.FireCd = 0f;
            p.TapQueued = false;
            p.KatanaHay = 0;  // the parry cancels both stances' buildups
            p.KatanaSen = 0;
            katanaLater.RemoveAll(l => l.Owner == p.Id);
        }

        // --- Your own screen ---------------------------------------------------------------------------------------------

        /// Your own game, every tick: each step that moves you (a dash, Tsumuji's, Engetsu's step), the Burst's plant, and the
        /// switch on the weapon-action key. Returns true while the Katana is out (nothing else uses the key then).
        bool LocalKatana(Player me, V2 aimAt, bool pressed, float dt)
        {
            katanaSwitchLocalCd = Math.Max(0f, katanaSwitchLocalCd - dt);
            katanaPlantT = Math.Max(0f, katanaPlantT - dt);
            bool katana = me.Weapon.Id == WeaponId.Katana;
            if (!katana)
                katanaDashesSeen = me.KatanaDashes;  // (picking the Katana up never replays an old count)
            if (me.KatanaDashes != katanaDashesSeen)
            {
                katanaDashesSeen = me.KatanaDashes;
                int code = me.KatanaStep;
                if (katana && !me.Downed && me.StunT <= 0f)
                {
                    float move = KatanaStepMove(code);
                    if (code == 3)
                    {
                        katanaPlantT = KatanaPlant;  // the Burst: planted a moment
                        DashT = 0f;
                    }
                    else if (move > 0f)
                    {
                        V2 dir = V2.FromAngle(me.Aim + KatanaStepTurn(code, me.KatanaDashes));
                        float len = MeleeReach(me, move), time = KatanaStepTime(code);
                        V2 to = ClampToArena(me.Pos + dir * len, me.Def.BodyR);
                        dashSpeed = len / time;
                        DashDir = dir;
                        DashT = to.DistanceTo(me.Pos) / dashSpeed;
                    }
                }
            }
            if (me.Downed || me.StunT > 0f)
            {
                katanaPlantT = 0f;
                return katana;
            }
            if (!katana)
                return false;
            if (pressed && katanaSwitchLocalCd <= 0f && DashT <= 0f && katanaPlantT <= 0f)
            {
                katanaSwitchLocalCd = KatanaSwitchCd;
                if (IsHost)
                    HostWeaponDash(MyId, DashKatanaStance, aimAt);
                else
                {
                    var w = Proto.Begin(Msg.WeaponDash);
                    w.Write(DashKatanaStance);
                    w.Write(aimAt);
                    session.SendToHost(Proto.End(w));
                }
            }
            return true;
        }
    }
}

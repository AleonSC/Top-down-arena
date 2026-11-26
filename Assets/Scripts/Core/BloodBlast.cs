using System;
using System.Collections.Generic;

namespace Arena
{
    /// The tank Hemomancer's Blood Blast (the 4th weapon key; Docs/BloodBlast.md, mockup Docs/Mockups/blood-blast.html):
    /// blood shaped into a blade, a six-step chain that never times out:
    ///   1 Crimson Slash, 2 Backcut (sweeps), 3 Thorn Flurry (three quick cuts), 4 Vein Whirl (a full spin),
    ///   5 Sanguine Cleave (a slam on a circle ahead), 6 Crescent Flight (a crescent flung out to BlastFlightReach), then 1 again.
    /// Every press costs BlastCostFrac[step] of your max health (PayHealth). Every press adds one blood stack (Enemy.BlastStacks, up to
    /// BlastStackMax) to each enemy it hits, however many of its cuts land (Enemy.BlastPress). The stacks are the last Blood Blast
    /// user's to hit it (Enemy.BlastOwner); when the enemy dies (BlastKill, the Hemomancer's KitDef.OnKill) its blood flies back
    /// to them as a Blood Vortex and heals BlastHealPerStack a stack as it arrives, BlastReturnTime later (TickBloodBlast).
    /// The step is the host's (Player.BlastStep) and is mirrored in Player.Combo (count above, step in the low 3 bits) for the look.
    /// Alt (the weapon-action key, Msg.WeaponDash WeaponAlt.Tide), Crimson Tide: TideCost of max health and a TideCd cooldown; the blood
    /// gathers for TideCharge, then a crescent (TideHalf either side of the aim) rolls out from TideR0 to TideR1 over TideTravel,
    /// hitting every enemy it reaches once for TideMult x the weapon's damage, shoving it TideKnock and adding a stack (TickTide).
    /// Extra projectiles: +15% damage and reach each (BlastGrow), the Tide's too.
    public partial class Game
    {
        public const int BlastSteps = 6, BlastStackMax = 10;
        public const float BlastHealPerStack = 2f, BlastReturnTime = 0.97f, BlastGrow = 0.15f;
        /// Each step: how long it takes (at the base fire rate), its damage x the weapon's, its health cost (of max health),
        /// its reach, its arc in degrees (360: a spin), and when it lands after the press.
        public static readonly float[] BlastTime = { 0.42f, 0.40f, 0.50f, 0.58f, 0.78f, 0.62f };
        public static readonly float[] BlastMult = { 1f, 1f, 9f / 22f, 18f / 22f, 40f / 22f, 26f / 22f };
        public static readonly float[] BlastCostFrac = { 0.04f / 3f, 0.04f / 3f, 0.05f / 3f, 0.02f, 0.03f, 0.07f / 3f };  // a third off the mockup's 2, 2, 2.5, 3, 4.5, 3.5%
        public static readonly float[] BlastReach = { 115f, 108f, 85f, 95f, 105f, 260f };
        public static readonly float[] BlastArc = { 120f, 130f, 100f, 360f, 0f, 80f };
        public static readonly float[] BlastDelay = { 0.08f, 0.08f, 0.05f, 0.2f, 0.38f, 0.3f };
        public const float BlastFlurryGap = 0.15f, BlastCleaveRadius = 70f, BlastCleaveKnock = 130f, BlastFlightReach = 260f;
        public const float BlastFxTail = 0.45f, BlastReturnFx = 1.15f;  // a cut's look outlasts it (drops landing); the Blood Vortex's whole show (it lands at BlastReturnTime)
        static readonly Sfx[] BlastSfx = { Sfx.BlastSlash, Sfx.BlastSlash, Sfx.BlastFlurry, Sfx.BlastWhirl, Sfx.BlastCleave, Sfx.BlastFlight };
        static readonly Rgba BlastRed = new Rgba(0.88f, 0.12f, 0.24f);

        class BlastReturn
        {
            public int Owner, Stacks;
            public float T;
        }

        readonly List<BlastReturn> blastReturns = new List<BlastReturn>();
        int blastPresses;  // host: every Blood Blast press gets its own number (Swing.BlastPress), so one press stacks an enemy once

        /// The Blood Blast step in `combo` (1-6; 0 none yet).
        public static int BlastStepOf(int combo) => combo & 7;

        /// The step the next press plays (from the replicated Combo, so every game agrees).
        public static int BlastNextStep(Player p) => BlastStepOf(p.Combo) % BlastSteps + 1;

        /// How far the next press reaches (the aim line): the cut's reach, the Cleave's circle centre, the Flight's end.
        public float BlastNextReach(Player p) => MeleeReach(p, BlastReach[BlastNextStep(p) - 1] * (1f + BlastGrow * ExtraShots(p)));

        /// What a step costs `p` (step 1-6).
        public static float BlastCost(Player p, int step) => p.MaxHp * BlastCostFrac[step - 1];

        /// Host: one press of fire with the Blood Blast (from FireBloodWeapon): the next step of the chain.
        void FireBloodBlast(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int step = p.BlastStep == 0 ? 1 : BlastNextStep(p);  // never times out: always the next step (fresh: the first)
            p.BlastStep = (byte)step;
            int count = ((p.Combo >> 3) + 1) & 31;
            p.Combo = (count << 3) | step;
            p.FireCd = BlastTime[step - 1] / FireRate(p);
            if (!casting)  // the blade is your own blood
            {
                float paid = PayHealth(p, BlastCost(p, step), w.Name);
                if (paid >= 1f)
                    BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), BlastRed, paid, 0.55f, Sfx.None);
            }
            float k = 1f + BlastGrow * extra, hit = w.Damage * BlastMult[step - 1] * dmg * k, reach = BlastReach[step - 1] * k;
            int press = ++blastPresses;
            Func<float, float, float, Swing> swing = (a, r, arc) =>
            {
                var s = NewSwing(p, a, r, arc, hit);
                s.BlastPress = press;
                s.NoFx = true;    // drawn from the press's FxKind.BlastCut (Game/BloodBlastLook.cs)
                s.Silent = true;  // and heard from it
                s.Delay = BlastDelay[step - 1];
                return s;
            };
            float fxReach = MeleeReach(p, reach);
            BroadcastFx(FxKind.BlastCut, p.Pos, p.Pos + V2.FromAngle(aim) * fxReach, BlastRed, p.Id * 10 + step, BlastTime[step - 1] / FireRate(p) + BlastFxTail,
                BlastSfx[step - 1]);
            switch (step)
            {
                case 3:  // Thorn Flurry: three short cuts, the middle one tilted the other way
                    for (int i = 0; i < 3; i++)
                    {
                        var s = swing(aim + (i == 0 ? -0.2f : i == 1 ? 0.18f : 0f), reach, BlastArc[2]);
                        s.Delay += BlastFlurryGap * i;
                        QueueSwing(s);
                    }
                    break;
                case 5:  // Sanguine Cleave: a slam on a circle ahead
                {
                    var s = swing(aim, reach, 0f);
                    s.Circle = BlastCleaveRadius * k;
                    s.CircleAt = s.Reach;
                    s.Heavy = true;
                    s.Thrust = true;
                    s.Knock = BlastCleaveKnock;
                    QueueSwing(s);
                    break;
                }
                case 6:  // Crescent Flight: the crescent flies out and cuts all it passes
                {
                    var s = swing(aim, reach, BlastArc[5]);
                    s.NoCut = true;  // it flies over the shots
                    QueueSwing(s);
                    break;
                }
                default:  // Crimson Slash, Backcut, Vein Whirl
                    QueueSwing(swing(aim, reach, BlastArc[step - 1]));
                    break;
            }
        }

        /// Host: a Blood Blast cut hit `e` (from LandSwing): one stack per press, up to BlastStackMax; the stacks go to `p`.
        void BlastLanded(Player p, Enemy e, Swing s)
        {
            if (s.BlastPress != blastHitSfx)
            {
                blastHitSfx = s.BlastPress;  // one wet splat a press that lands
                BroadcastSound(Sfx.BlastHit);
            }
            StackBlast(p, e, s.BlastPress);
        }

        int blastHitSfx;

        /// The most stacks one enemy can hold for `p` (Blood Harvest: +5 a card).
        public static int BlastStackCap(Player p) => BlastStackMax + p.Stats.BlastCapBonus;

        /// What each stack heals `p` (Blood Harvest: +1 a card).
        public static float BlastHealOf(Player p) => BlastHealPerStack + p.Stats.BlastHealBonus;

        /// One blood stack on `e` for press number `press` (once a press), the stacks now `p`'s.
        void StackBlast(Player p, Enemy e, int press)
        {
            if (e.BlastPress == press || e.Hp <= 0f)
                return;  // this press already stacked it (Thorn Flurry's later cuts)
            e.BlastPress = press;
            e.BlastOwner = p.Id;
            if (e.BlastStacks < BlastStackCap(p))
                e.BlastStacks++;
        }

        /// Host, an enemy died (HemomancerKit.OnKill): its stacks' blood flies back to their owner (a Blood Vortex) and heals on arrival.
        void BlastKill(Enemy e)
        {
            int n = e.BlastStacks;
            e.BlastStacks = 0;
            var p = PlayerOrNull(e.BlastOwner);
            if (n <= 0 || p == null || p.Downed)
                return;
            blastReturns.Add(new BlastReturn { Owner = p.Id, Stacks = n, T = BlastReturnTime });
            BroadcastFx(FxKind.BlastReturn, e.Pos, new V2(p.Id, n), BlastRed, e.Radius, BlastReturnFx, Sfx.BlastReturn);  // the Blood Vortex (Game/BloodBlastLook.cs)
        }

        /// Host, every tick: returning blood lands.
        void TickBloodBlast(float dt)
        {
            for (int i = blastReturns.Count - 1; i >= 0; i--)
            {
                var r = blastReturns[i];
                r.T -= dt;
                if (r.T > 0f)
                    continue;
                blastReturns.RemoveAt(i);
                var p = PlayerOrNull(r.Owner);
                if (p != null && !p.Downed)
                    Heal(p, r.Stacks * BlastHealOf(p), p, "Blood Blast");
            }
        }

        /// The weapon is put away (or you're down): the chain starts over.
        void ClearBloodBlast(Player p)
        {
            p.BlastStep = 0;
            p.Combo &= ~7;
        }

        // ---- Crimson Tide (Alt) ----

        public const float TideCost = 0.12f, TideCd = 8f, TideCharge = 0.65f, TideTravel = 0.65f, TideR0 = 40f, TideR1 = 400f;
        public const float TideHalf = 50f * MathX.Pi / 180f, TideMult = 60f / 22f, TideKnock = 150f;

        class TideRun
        {
            public int Owner, Press;
            public float Aim, T, Dmg, Reach;
            public V2 From;
            public bool Out;  // launched (else still charging)
            public readonly HashSet<int> Hit = new HashSet<int>();
        }

        readonly List<TideRun> tideRuns = new List<TideRun>();
        float tideLocalCd;  // your own game: until the Alt can go again (the ring under your feet; Game/TankAltLook.cs)
        public float TideLocalCd => tideLocalCd;

        /// The Tide's radius `age` seconds after it's let go (eases out to its full reach).
        public static float TideRadius(float age, float reach) => TideR0 + (reach - TideR0) * WcEase(age / TideTravel);

        /// Your own game: Alt with the Blood Blast. True when it sent something (LocalWeaponDash stops there).
        bool LocalTide(Player me, V2 aimAt, bool pressed, float dt)
        {
            tideLocalCd = Math.Max(0f, tideLocalCd - dt);
            if (!pressed || me.Weapon.Id != WeaponId.BloodBlast || tideLocalCd > 0f)
                return false;
            tideLocalCd = TideCd;
            if (IsHost)
                HostWeaponDash(MyId, WeaponAlt.Tide, aimAt);
            else
            {
                var w = Proto.Begin(Msg.WeaponDash);
                w.Write(WeaponAlt.Tide);
                w.Write(aimAt);
                session.SendToHost(Proto.End(w));
            }
            return true;
        }

        /// Host: Alt pressed with the Blood Blast: pay, then gather the Tide (TickTide lets it go after TideCharge).
        void HostTide(Player p, V2 target)
        {
            if (p.Weapon.Id != WeaponId.BloodBlast || p.Downed || p.StunT > 0f || p.BlastTideCd > 0f || State == MatchState.GameOver)
                return;
            p.BlastTideCd = TideCd;
            float paid = PayHealth(p, p.MaxHp * TideCost, "Crimson Tide");
            if (paid >= 1f)
                BroadcastFx(FxKind.Bleed, p.Pos, new V2(p.Id, 0f), BlastRed, paid, 0.55f, Sfx.None);
            float k = 1f + BlastGrow * ExtraShots(p);
            float aim = (target - p.Pos).IsZero ? p.Aim : (target - p.Pos).Angle;
            tideRuns.Add(new TideRun { Owner = p.Id, Press = ++blastPresses, Aim = aim, T = TideCharge, Dmg = p.Weapon.Damage * p.Stats.DmgMult * k * TideMult, Reach = TideR1 * k });
            p.FireCd = Math.Max(p.FireCd, TideCharge);  // no cuts while it gathers
            BroadcastFx(FxKind.BlastTide, p.Pos, p.Pos + V2.FromAngle(aim), BlastRed, p.Id * 10 + 1, TideCharge, Sfx.BlastTideGather);  // the blood gathering (Game/BloodBlastLook.cs)
        }

        /// Host, every tick: Tides gather, then roll out, cutting, shoving and stacking every enemy they reach once.
        void TickTide(float dt)
        {
            foreach (var p in Players.Values)
                p.BlastTideCd = Math.Max(0f, p.BlastTideCd - dt);
            for (int i = tideRuns.Count - 1; i >= 0; i--)
            {
                var r = tideRuns[i];
                var p = PlayerOrNull(r.Owner);
                if (p == null || p.Downed)
                {
                    tideRuns.RemoveAt(i);
                    continue;
                }
                if (!r.Out)
                {
                    r.T -= dt;
                    if (r.T > 0f)
                        continue;
                    r.Out = true;
                    r.T = 0f;
                    r.From = p.Pos;
                    BroadcastFx(FxKind.BlastTide, r.From, r.From + V2.FromAngle(r.Aim) * r.Reach, BlastRed, p.Id * 10 + 2, TideTravel + BlastFxTail, Sfx.BlastTideWave);  // the wave (Game/BloodBlastLook.cs)
                }
                r.T += dt;
                float R = TideRadius(r.T, r.Reach);
                string was = CreditTo("weapon");
                foreach (var e in AllFoes(by: p.Id))
                {
                    if (r.Hit.Contains(e.Id) || e.Pos.DistanceTo(r.From) - e.Radius > R)
                        continue;
                    if (AngleTo(r.From, e.Pos, r.Aim) > TideHalf + BodySlack(e.Pos.DistanceTo(r.From), e.Radius))
                        continue;
                    r.Hit.Add(e.Id);
                    HurtEnemy(e, r.Dmg, p.Id);
                    if (!Heavy(e))
                    {
                        ShoveFrom(e, r.From, TideKnock, V2.FromAngle(r.Aim));
                    }
                    StackBlast(p, e, r.Press);
                }
                dmgSrc = was;
                if (r.T >= TideTravel)
                    tideRuns.RemoveAt(i);
            }
        }
    }
}

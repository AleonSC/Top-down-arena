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
    /// Extra projectiles: +15% damage and reach each (BlastGrow).
    public partial class Game
    {
        public const int BlastSteps = 6, BlastStackMax = 10;
        public const float BlastHealPerStack = 2f, BlastReturnTime = 0.97f, BlastGrow = 0.15f;
        /// Each step: how long it takes (at the base fire rate), its damage x the weapon's, its health cost (of max health),
        /// its reach, its arc in degrees (360: a spin), and when it lands after the press.
        public static readonly float[] BlastTime = { 0.42f, 0.40f, 0.50f, 0.58f, 0.78f, 0.62f };
        public static readonly float[] BlastMult = { 1f, 1f, 9f / 22f, 18f / 22f, 40f / 22f, 26f / 22f };
        public static readonly float[] BlastCostFrac = { 0.02f, 0.02f, 0.025f, 0.03f, 0.045f, 0.035f };
        public static readonly float[] BlastReach = { 115f, 108f, 85f, 95f, 105f, 260f };
        public static readonly float[] BlastArc = { 120f, 130f, 100f, 360f, 0f, 80f };
        public static readonly float[] BlastDelay = { 0.08f, 0.08f, 0.05f, 0.2f, 0.38f, 0.3f };
        public const float BlastFlurryGap = 0.15f, BlastCleaveRadius = 70f, BlastCleaveKnock = 130f, BlastFlightReach = 260f;
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

        /// What a step costs `p` (step 1-6).
        public static float BlastCost(Player p, int step) => p.MaxHp * BlastCostFrac[step - 1];

        /// Host: one press of fire with the Blood Blast (from FireBloodWeapon): the next step of the chain.
        void FireBloodBlast(Player p, float aim, float dmg, int extra)
        {
            var w = p.Weapon;
            int step = p.BlastStep % BlastSteps + 1;  // never times out: always the next step
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
                s.Delay = BlastDelay[step - 1];
                return s;
            };
            switch (step)
            {
                case 3:  // Thorn Flurry: three short cuts, the middle one tilted the other way
                    for (int i = 0; i < 3; i++)
                    {
                        var s = swing(aim + (i == 0 ? -0.2f : i == 1 ? 0.18f : 0f), reach, BlastArc[2]);
                        s.Delay += BlastFlurryGap * i;
                        s.Silent = i > 0;
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
            if (e.BlastPress == s.BlastPress)
                return;  // this press already stacked it (Thorn Flurry's later cuts)
            e.BlastPress = s.BlastPress;
            e.BlastOwner = p.Id;
            if (e.BlastStacks < BlastStackMax)
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
            BroadcastFx(FxKind.Drain, e.Pos, p.Pos, BlastRed, 2f + n, BlastReturnTime, Sfx.None);  // (step 3 draws the Blood Vortex)
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
                    Heal(p, r.Stacks * BlastHealPerStack, p, "Blood Blast");
            }
        }

        /// The weapon is put away (or you're down): the chain starts over.
        void ClearBloodBlast(Player p)
        {
            p.BlastStep = 0;
        }
    }
}

using System;

namespace Arena
{
    /// The Vowguard's moves (Healer Knight, the fourth weapon; Docs/Vowguard.md, mockup Docs/Mockups/vowguard.html): eighteen
    /// moves, nine a stance, that a player strings into a chain of up to ten steps a stance (VgChain). Each move is data: its
    /// length, its key poses (sword and shield round the knight, timed 0-1 through the move), its hits, heals, guard, and what
    /// it does to you (a dash, a hop). The host times every hit and heal from these (Vowguard.cs), and every screen draws the
    /// arms, sword, shield and the move's light from the same poses (Game/VowguardLook.cs).
    public enum VgMove : byte
    {
        // Valor (stance 0): six links, then three finishers
        EdgeCut, RimBash, LungeThrust, ShieldHook, TwinStrike, MercySpin, JudgmentCleave, ShieldCharge, HallowedSlam,
        // Sanctuary (stance 1)
        RaiseGuard, GuardedJab, DeflectSweep, Sidestep, CoverAlly, RallyTap, BulwarkAbsorb, AegisDome, SanctifiedWard,
        None,
    }

    /// A pose: the sword's angle from the aim (degrees, + to the right), its reach (1 = at rest), the shield's angle and its
    /// distance from the knight's centre, the knight's lean (px forward and to the right) and the shield's size.
    public struct VgPose
    {
        public float Sword, Reach, Shield, ShieldD, Dx, Dy, ShieldS;

        public static VgPose Of(float[] k, int at) => new VgPose { Sword = k[at], Reach = k[at + 1], Shield = k[at + 2], ShieldD = k[at + 3], Dx = k[at + 4], Dy = k[at + 5], ShieldS = k[at + 6] };

        public static VgPose Lerp(VgPose a, VgPose b, float u) => new VgPose
        {
            Sword = a.Sword + (b.Sword - a.Sword) * u, Reach = a.Reach + (b.Reach - a.Reach) * u, Shield = a.Shield + (b.Shield - a.Shield) * u,
            ShieldD = a.ShieldD + (b.ShieldD - a.ShieldD) * u, Dx = a.Dx + (b.Dx - a.Dx) * u, Dy = a.Dy + (b.Dy - a.Dy) * u, ShieldS = a.ShieldS + (b.ShieldS - a.ShieldS) * u,
        };
    }

    public static class VowguardMotion
    {
        public enum Shape : byte { Arc, Lane, Ring }
        public enum HealKind : byte { Near, Self, Ahead, Cover }

        /// One hit, `T` (0-1) through the move: an arc `Half` degrees either side of the aim, a lane `Width` across, or a ring;
        /// out to `Reach`. Dmg is against the weapon's Damage (16).
        public class Hit
        {
            public float T, Half, Reach, Width, Dmg, Stun, Knock;
            public Shape K;
            public bool Deflect, Pull;  // bats small shots back as yours; drags what it hits toward you
        }

        /// One heal, `T` through the move: allies within `R` (Near), you (Self), allies ahead of you within `R` (Ahead: the
        /// Cleave's crescent), or the ally you cover (Cover).
        public class Heal
        {
            public float T, R, Amt;
            public HealKind K;
        }

        public class Move
        {
            public VgMove Id;
            public string Name, Blurb;
            public float Dur;
            public bool Fin;
            public float[][] Keys;          // { t, sword, reach, shield, shieldD, dx, dy, shieldS }
            public bool Keep;               // you end where the move took you (a dash or hop: your own game moves you)
            public float Wrap;              // a full turn: the rest pose at the end is this many degrees round
            public Hit[] Hits = NoHits;
            public Heal[] Heals = NoHeals;
            public float GuardT0 = -1f, GuardT1, GuardHalf;  // a guard while it plays (Valor's shield moves; Sanctuary's wider ones)
            public float BlockHeal;         // each shot the guard stops heals the most hurt ally this much (Raise Guard)
            public bool Soak;               // shots the guard stops are counted for the pulse (Bulwark Absorb)
            public float DomeT = -1f;       // plants an Aegis Dome here
            public float WardT = -1f;       // wards everyone in WardR here
            public float InvulT0 = -1f, InvulT1;  // untouchable between
            public float MoveT0, MoveT1, MoveFwd, MoveSide;  // the dash or hop (Keep): px along the aim and to its right
            public bool CoverDash;          // the dash goes to the ally nearest your aim (Cover Ally)
            public Sfx Sound = Sfx.SwingLight;
            public int Stance => Id < VgMove.RaiseGuard ? 0 : 1;
        }

        static readonly Hit[] NoHits = new Hit[0];
        static readonly Heal[] NoHeals = new Heal[0];

        public const int ChainMax = 10;           // steps a stance's chain can hold
        public const int MovesPerStance = 9;
        public static readonly float[] RestV = { 35f, 1f, -60f, 15f, 0f, 0f, 1f }, RestS = { 60f, 0.75f, 0f, 24f, 0f, 0f, 1.1f };

        static Hit Arc(float t, float half, float reach, float dmg) => new Hit { T = t, K = Shape.Arc, Half = half, Reach = reach, Dmg = dmg };
        static Hit Lane(float t, float reach, float width, float dmg) => new Hit { T = t, K = Shape.Lane, Reach = reach, Width = width, Dmg = dmg };
        static Hit Ring(float t, float reach, float dmg) => new Hit { T = t, K = Shape.Ring, Reach = reach, Dmg = dmg };
        static Heal Near(float t, float r, float amt) => new Heal { T = t, K = HealKind.Near, R = r, Amt = amt };

        public static readonly Move[] Moves =
        {
            // --- Valor ---
            new Move { Id = VgMove.EdgeCut, Name = "Edge Cut", Dur = 0.30f, Sound = Sfx.TwinCut1, Blurb = "A forehand cut, 110° and 115 px: 16. Cuts small shots in its arc.",
                Keys = new[] { new[] { 0.28f, 100f, 0.95f, -70f, 13f, 0f, 0f, 1f }, new[] { 0.62f, -65f, 1.1f, -45f, 15f, 8f, 0f, 1f } },
                Hits = new[] { Arc(0.5f, 55f, 115f, 16f) } },
            new Move { Id = VgMove.RimBash, Name = "Rim Bash", Dur = 0.32f, Sound = Sfx.SwingHeavy, Blurb = "The shield rim punched forward, 75 px: 12 and a 0.4 s stun. Blocks while it's out.",
                Keys = new[] { new[] { 0.3f, 50f, 0.9f, -25f, 10f, -6f, 0f, 1f }, new[] { 0.5f, 45f, 0.9f, 0f, 44f, 10f, 0f, 1.1f }, new[] { 0.68f, 40f, 0.9f, -10f, 30f, 8f, 0f, 1f } },
                Hits = new[] { new Hit { T = 0.5f, K = Shape.Arc, Half = 35f, Reach = 80f, Dmg = 12f, Stun = 0.4f } }, GuardT0 = 0.35f, GuardT1 = 0.7f, GuardHalf = 40f },
            new Move { Id = VgMove.LungeThrust, Name = "Lunge Thrust", Dur = 0.34f, Sound = Sfx.TwinCut2, Blurb = "A step in and the point run down a 165 px line: 20 to everything on it.",
                Keys = new[] { new[] { 0.3f, 20f, 0.6f, -55f, 13f, -8f, 0f, 1f }, new[] { 0.55f, 2f, 1.7f, -50f, 14f, 30f, 0f, 1f }, new[] { 0.75f, 4f, 1.5f, -50f, 14f, 26f, 0f, 1f } },
                Hits = new[] { Lane(0.55f, 165f, 14f, 20f) } },
            new Move { Id = VgMove.ShieldHook, Name = "Shield Hook", Dur = 0.36f, Sound = Sfx.SwingHeavy, Blurb = "Hooks with the rim, 95 px: 10, and drags what it hits 50 px toward you.",
                Keys = new[] { new[] { 0.3f, 45f, 0.9f, -70f, 20f, 0f, 0f, 1f }, new[] { 0.48f, 45f, 0.9f, -15f, 60f, 6f, 0f, 1f }, new[] { 0.75f, 40f, 0.9f, -45f, 18f, -6f, 0f, 1f } },
                Hits = new[] { new Hit { T = 0.48f, K = Shape.Arc, Half = 35f, Reach = 100f, Dmg = 10f, Knock = 50f, Pull = true } } },
            new Move { Id = VgMove.TwinStrike, Name = "Twin Strike", Dur = 0.42f, Sound = Sfx.TwinCut1, Blurb = "A cut (10), then the shield follows through as a bash (10, 0.3 s stun).",
                Keys = new[] { new[] { 0.2f, 100f, 1f, -60f, 13f, 0f, 0f, 1f }, new[] { 0.42f, -60f, 1.1f, -40f, 14f, 6f, 0f, 1f }, new[] { 0.55f, -50f, 1f, -10f, 20f, 6f, 0f, 1f }, new[] { 0.72f, -30f, 1f, 10f, 42f, 12f, 0f, 1.1f } },
                Hits = new[] { Arc(0.35f, 60f, 105f, 10f), new Hit { T = 0.72f, K = Shape.Arc, Half = 35f, Reach = 85f, Dmg = 10f, Stun = 0.3f } }, GuardT0 = 0.55f, GuardT1 = 0.85f, GuardHalf = 40f },
            new Move { Id = VgMove.MercySpin, Name = "Mercy Spin", Dur = 0.40f, Sound = Sfx.TwinSpin, Blurb = "A full turn, 95 px round you: 13, cuts every small shot around you and heals you 3.",
                Keys = new[] { new[] { 0.15f, 80f, 1f, -70f, 14f, 0f, 0f, 1f }, new[] { 0.5f, -100f, 1.1f, -250f, 14f, 0f, 0f, 1f }, new[] { 0.85f, -325f, 1.1f, -425f, 15f, 0f, 0f, 1f } }, Wrap = -360f,
                Hits = new[] { Ring(0.5f, 95f, 13f) }, Heals = new[] { new Heal { T = 0.6f, K = HealKind.Self, Amt = 3f } } },
            new Move { Id = VgMove.JudgmentCleave, Name = "Judgment Cleave", Fin = true, Dur = 0.58f, Sound = Sfx.GreatRend, Blurb = "Overhead down the aim: 34 on a 360 px line, and every ally ahead of you within 300 px heals 8.",
                Keys = new[] { new[] { 0.35f, 170f, 0.55f, -30f, 12f, -10f, 0f, 1f }, new[] { 0.58f, 0f, 1.45f, -50f, 14f, 20f, 0f, 1f }, new[] { 0.8f, 5f, 1.4f, -50f, 14f, 18f, 0f, 1f } },
                Hits = new[] { Lane(0.58f, 360f, 35f, 34f) }, Heals = new[] { new Heal { T = 0.7f, K = HealKind.Ahead, R = 300f, Amt = 8f } } },
            new Move { Id = VgMove.ShieldCharge, Name = "Shield Charge", Fin = true, Dur = 0.60f, Sound = Sfx.KnuckleRocket, Blurb = "A 140 px dash shield first, untouchable: 30 to everything in the way, shoved 80 px.",
                Keys = new[] { new[] { 0.2f, 60f, 0.8f, 0f, 20f, -10f, 0f, 1.15f }, new[] { 0.55f, 60f, 0.8f, 0f, 24f, 140f, 0f, 1.15f }, new[] { 0.8f, 55f, 0.8f, -20f, 18f, 136f, 0f, 1.05f } }, Keep = true,
                Hits = new[] { new Hit { T = 0.5f, K = Shape.Lane, Reach = 170f, Width = 30f, Dmg = 30f, Knock = 80f } }, GuardT0 = 0.15f, GuardT1 = 0.65f, GuardHalf = 50f,
                InvulT0 = 0.2f, InvulT1 = 0.6f, MoveT0 = 0.2f, MoveT1 = 0.55f, MoveFwd = 140f },
            new Move { Id = VgMove.HallowedSlam, Name = "Hallowed Slam", Fin = true, Dur = 0.62f, Sound = Sfx.LanceBreach, Blurb = "The shield slammed down: 28 within 120 px and a 0.6 s stun; allies within 160 px heal 10.",
                Keys = new[] { new[] { 0.35f, 40f, 0.8f, 0f, 6f, 0f, 0f, 1.2f }, new[] { 0.55f, 40f, 0.8f, 0f, 30f, 4f, 0f, 1.25f }, new[] { 0.8f, 40f, 0.8f, 0f, 28f, 4f, 0f, 1.2f } },
                Hits = new[] { new Hit { T = 0.56f, K = Shape.Ring, Reach = 120f, Dmg = 28f, Stun = 0.6f } }, Heals = new[] { Near(0.62f, 160f, 10f) } },
            // --- Sanctuary ---
            new Move { Id = VgMove.RaiseGuard, Name = "Raise Guard", Dur = 0.50f, Sound = Sfx.GreatRaise, Blurb = "Shield high and wide (160°) for 0.4 s. Each shot it stops heals the most hurt ally 2.",
                Keys = new[] { new[] { 0.15f, 70f, 0.7f, 0f, 28f, 0f, 0f, 1.35f }, new[] { 0.85f, 70f, 0.7f, 0f, 28f, 0f, 0f, 1.35f } },
                GuardT0 = 0.1f, GuardT1 = 0.9f, GuardHalf = 80f, BlockHeal = 2f },
            new Move { Id = VgMove.GuardedJab, Name = "Guarded Jab", Dur = 0.26f, Sound = Sfx.TwinCut2, Blurb = "The point darts out over the rim, 140 px: 10. The guard stays up.",
                Keys = new[] { new[] { 0.3f, 30f, 0.6f, 0f, 24f, 0f, 0f, 1.1f }, new[] { 0.55f, 8f, 1.6f, 0f, 24f, 6f, 0f, 1.1f } },
                Hits = new[] { Lane(0.55f, 145f, 10f, 10f) } },
            new Move { Id = VgMove.DeflectSweep, Name = "Deflect Sweep", Dur = 0.34f, Sound = Sfx.SwingHeavy, Blurb = "The shield swept 140° across, 80 px: 8, and small shots in the arc fly back as yours.",
                Keys = new[] { new[] { 0.25f, 60f, 0.75f, -70f, 30f, 0f, 0f, 1.15f }, new[] { 0.6f, 60f, 0.75f, 70f, 30f, 0f, 0f, 1.15f } },
                Hits = new[] { new Hit { T = 0.45f, K = Shape.Arc, Half = 70f, Reach = 85f, Dmg = 8f, Deflect = true } }, GuardT0 = 0.2f, GuardT1 = 0.7f, GuardHalf = 75f },
            new Move { Id = VgMove.Sidestep, Name = "Sidestep", Dur = 0.24f, Sound = Sfx.KatanaLunge, Blurb = "A 60 px hop to the side behind the shield, untouchable for 0.2 s.",
                Keys = new[] { new[] { 0.5f, 60f, 0.75f, 0f, 24f, 0f, -60f, 1.1f } }, Keep = true,
                InvulT0 = 0f, InvulT1 = 0.85f, MoveT0 = 0.05f, MoveT1 = 0.5f, MoveSide = -60f },
            new Move { Id = VgMove.CoverAlly, Name = "Cover Ally", Dur = 0.44f, Sound = Sfx.KatanaLunge, Blurb = "Dash in front of the ally nearest your aim (220 px): they heal 4 and take 30% less for 2 s.",
                Keys = new[] { new[] { 0.6f, 60f, 0.75f, -20f, 26f, 100f, -70f, 1.2f }, new[] { 0.85f, 60f, 0.75f, -15f, 26f, 100f, -70f, 1.2f } }, Keep = true,
                Heals = new[] { new Heal { T = 0.62f, K = HealKind.Cover, Amt = 4f } }, GuardT0 = 0.3f, GuardT1 = 1f, GuardHalf = 60f,
                MoveT0 = 0.1f, MoveT1 = 0.6f, MoveFwd = 100f, CoverDash = true },
            new Move { Id = VgMove.RallyTap, Name = "Rally Tap", Dur = 0.32f, Sound = Sfx.CenserBell, Blurb = "The flat of the blade rung on the shield: allies within 160 px heal 4.",
                Keys = new[] { new[] { 0.35f, -20f, 0.6f, 0f, 22f, 0f, 0f, 1.1f }, new[] { 0.5f, -5f, 0.55f, 0f, 20f, 0f, 0f, 1.1f } },
                Heals = new[] { Near(0.55f, 160f, 4f) } },
            new Move { Id = VgMove.BulwarkAbsorb, Name = "Bulwark Absorb", Fin = true, Dur = 0.90f, Sound = Sfx.GreatRaise, Blurb = "Soak every shot in front for 0.7 s, then a 170 px pulse: 12 to enemies, 6 + 2 a shot soaked healing to allies.",
                Keys = new[] { new[] { 0.12f, 60f, 0.7f, 0f, 26f, -4f, 0f, 1.4f }, new[] { 0.75f, 60f, 0.7f, 0f, 24f, -10f, 0f, 1.45f }, new[] { 0.85f, 60f, 0.7f, 0f, 34f, 4f, 0f, 1.3f } },
                GuardT0 = 0.1f, GuardT1 = 0.82f, GuardHalf = 90f, Soak = true,
                Hits = new[] { Ring(0.86f, 170f, 12f) }, Heals = new[] { Near(0.88f, 170f, 6f) } },
            new Move { Id = VgMove.AegisDome, Name = "Aegis Dome", Fin = true, Dur = 1.0f, Sound = Sfx.GreatFull, Blurb = "Plant the shield: a 110 px dome for 2 s that stops enemy shots; allies inside heal 3 a second.",
                Keys = new[] { new[] { 0.3f, 60f, 0.8f, 0f, 30f, 0f, 0f, 1.2f }, new[] { 0.45f, 60f, 0.8f, 0f, 38f, 0f, 0f, 1.3f }, new[] { 0.95f, 60f, 0.8f, 0f, 38f, 0f, 0f, 1.3f } },
                DomeT = 0.45f },
            new Move { Id = VgMove.SanctifiedWard, Name = "Sanctified Ward", Fin = true, Dur = 0.52f, Sound = Sfx.LightWard, Blurb = "The shield flares: every ally within 180 px gets a 15 HP ward for 3 s and heals 4.",
                Keys = new[] { new[] { 0.3f, 60f, 0.7f, 0f, 16f, 0f, 0f, 1.2f }, new[] { 0.5f, 60f, 0.7f, 0f, 26f, 0f, 0f, 1.35f } },
                Heals = new[] { Near(0.55f, 180f, 4f) }, WardT = 0.55f },
        };

        public const float DomeR = 110f, DomeLife = 2f, DomeHeal = 3f, WardR = 180f, WardAmt = 15f;
        public const float CoverRange = 220f, CoverCut = 0.3f, CoverTime = 2f, SoakHeal = 2f, SoakCap = 8;

        public static Move Of(VgMove id) => id < VgMove.None ? Moves[(int)id] : null;
        public static Move Of(int id) => id >= 0 && id < Moves.Length ? Moves[id] : null;
        public static int StanceOf(VgMove id) => id < VgMove.RaiseGuard ? 0 : 1;

        /// The pose at rest in a stance.
        public static VgPose Rest(int stance) => VgPose.Of(stance == 0 ? RestV : RestS, 0);

        static float Ease(float x) => x < 0.5f ? 2f * x * x : 1f - (-2f * x + 2f) * (-2f * x + 2f) / 2f;

        /// The pose `t` (0-1) through a move: eased between its keys, from and back to its stance's rest pose.
        public static VgPose PoseAt(Move m, float t) => Walk(m, t, false);

        /// The pose as drawn (Radiant motion, Docs/Mockups/vowguard-models.html): like PoseAt, but a strike (a segment that
        /// swings the sword over 50°, punches the shield out or thrusts) first pulls back a little, then lets go fast.
        /// It ends on the same keys, so the hits the host times from Move.Hits are unchanged.
        public static VgPose ShownPoseAt(Move m, float t) => Walk(m, t, true);

        public const float StopHit = 0.06f, StopFin = 0.09f;  // the drawn hit-stop on each hit (s)

        /// The time (s) to draw a move at, `tau` s into it: it holds on each hit for StopHit (StopFin on a finisher), then
        /// plays the rest a little faster so it still ends at Dur. `stopped`: in a hold.
        public static float ShownT(Move m, float tau, out bool stopped)
        {
            stopped = false;
            float t = Math.Max(0f, Math.Min(m.Dur, tau)), s = m.Fin ? StopFin : StopHit;
            for (int i = 0; i < m.Hits.Length; i++)
            {
                float h = m.Hits[i].T * m.Dur, rest = m.Dur - h - s;
                if (rest < 0.04f || t < h)
                    continue;
                if (t < h + s)
                {
                    stopped = true;
                    t = h;
                }
                else
                    t = h + (t - h - s) * (m.Dur - h) / rest;
            }
            return t;
        }

        static float EaseCubic(float x) => x < 0.5f ? 4f * x * x * x : 1f - (-2f * x + 2f) * (-2f * x + 2f) * (-2f * x + 2f) / 2f;

        static VgPose Walk(Move m, float t, bool sharp)
        {
            var rest = Rest(m.Stance);
            var end = rest;
            var last = m.Keys[m.Keys.Length - 1];
            if (m.Keep)
            {
                end.Dx = last[5];
                end.Dy = last[6];
            }
            end.Sword += m.Wrap;
            end.Shield += m.Wrap;
            float prevT = 0f;
            var prev = rest;
            for (int i = 0; i <= m.Keys.Length; i++)
            {
                float kt = i < m.Keys.Length ? m.Keys[i][0] : 1f;
                var k = i < m.Keys.Length ? VgPose.Of(m.Keys[i], 1) : end;
                if (t <= kt)
                {
                    float u = Math.Max(0f, Math.Min(1f, kt > prevT ? (t - prevT) / (kt - prevT) : 1f));
                    return sharp ? Sharp(prev, k, u) : VgPose.Lerp(prev, k, Ease(u));
                }
                prev = k;
                prevT = kt;
            }
            return end;
        }

        /// One segment from a to b, `u` through it, with the pull back before a strike.
        static VgPose Sharp(VgPose a, VgPose b, float u)
        {
            float dS = b.Sword - a.Sword;
            bool bash = b.ShieldD - a.ShieldD > 14f, thrust = b.Reach - a.Reach > 0.5f;
            if (Math.Abs(dS) <= 50f && !bash && !thrust)
                return VgPose.Lerp(a, b, EaseCubic(u));
            float e = Math.Max(0f, Math.Min(1f, (u - 0.28f) / 0.72f));
            var o = VgPose.Lerp(a, b, 1f - (1f - e) * (1f - e) * (1f - e));
            float w = u < 0.28f ? (float)Math.Sin(Math.PI * u / 0.28f * 0.5f) : Math.Max(0f, 1f - (u - 0.28f) / 0.2f);  // wind up, then let go
            o.Sword -= Math.Sign(dS) * Math.Min(20f, Math.Abs(dS) * 0.16f) * w;
            if (bash)
                o.ShieldD -= 5f * w;
            if (thrust)
                o.Reach -= 0.25f * w;
            return o;
        }

        // --- Chains -------------------------------------------------------------------------------------------------

        /// The presets (the editor's keys; bots and anyone who never edited play Crusader).
        public static readonly string[] PresetNames = { "Crusader", "Shepherd", "Bastion", "Marathon" };
        public static readonly VgMove[][][] Presets =
        {
            new[] { new[] { VgMove.EdgeCut, VgMove.LungeThrust, VgMove.EdgeCut, VgMove.MercySpin, VgMove.LungeThrust, VgMove.JudgmentCleave },
                    new[] { VgMove.GuardedJab, VgMove.DeflectSweep, VgMove.GuardedJab, VgMove.Sidestep, VgMove.SanctifiedWard } },
            new[] { new[] { VgMove.RimBash, VgMove.EdgeCut, VgMove.TwinStrike, VgMove.HallowedSlam, VgMove.EdgeCut, VgMove.MercySpin, VgMove.JudgmentCleave },
                    new[] { VgMove.RallyTap, VgMove.RaiseGuard, VgMove.CoverAlly, VgMove.RallyTap, VgMove.AegisDome } },
            new[] { new[] { VgMove.ShieldHook, VgMove.RimBash, VgMove.EdgeCut, VgMove.TwinStrike, VgMove.ShieldCharge },
                    new[] { VgMove.RaiseGuard, VgMove.DeflectSweep, VgMove.GuardedJab, VgMove.RaiseGuard, VgMove.DeflectSweep, VgMove.BulwarkAbsorb } },
            new[] { new[] { VgMove.EdgeCut, VgMove.RimBash, VgMove.LungeThrust, VgMove.EdgeCut, VgMove.ShieldHook, VgMove.TwinStrike, VgMove.MercySpin, VgMove.LungeThrust, VgMove.EdgeCut, VgMove.HallowedSlam },
                    new[] { VgMove.GuardedJab, VgMove.RaiseGuard, VgMove.GuardedJab, VgMove.DeflectSweep, VgMove.RallyTap, VgMove.Sidestep, VgMove.GuardedJab, VgMove.DeflectSweep, VgMove.RaiseGuard, VgMove.BulwarkAbsorb } },
        };

        /// A chain pair on the wire and in the profile: the Valor chain's length, its moves, then the Sanctuary chain's.
        public static byte[] Pack(VgMove[] valor, VgMove[] sanct)
        {
            var b = new byte[2 + valor.Length + sanct.Length];
            b[0] = (byte)valor.Length;
            for (int i = 0; i < valor.Length; i++)
                b[1 + i] = (byte)valor[i];
            b[1 + valor.Length] = (byte)sanct.Length;
            for (int i = 0; i < sanct.Length; i++)
                b[2 + valor.Length + i] = (byte)sanct[i];
            return b;
        }

        public static readonly byte[] DefaultPack = Pack(Presets[0][0], Presets[0][1]);

        /// One stance's chain out of a pack; a missing or broken pack gives the default.
        public static VgMove[] Chain(byte[] pack, int stance)
        {
            if (!Valid(pack))
                pack = DefaultPack;
            int at = stance == 0 ? 0 : 1 + pack[0];
            var c = new VgMove[pack[at]];
            for (int i = 0; i < c.Length; i++)
                c[i] = (VgMove)pack[at + 1 + i];
            return c;
        }

        /// A pack that holds two chains of 1 to ChainMax moves, each of its own stance.
        public static bool Valid(byte[] pack)
        {
            if (pack == null || pack.Length < 4)
                return false;
            int at = 0;
            for (int st = 0; st < 2; st++)
            {
                if (at >= pack.Length)
                    return false;
                int n = pack[at];
                if (n < 1 || n > ChainMax || at + 1 + n > pack.Length)
                    return false;
                for (int i = 0; i < n; i++)
                {
                    int m = pack[at + 1 + i];
                    if (m >= (int)VgMove.None || StanceOf((VgMove)m) != st)
                        return false;
                }
                at += 1 + n;
            }
            return at == pack.Length;
        }

        public static bool SamePack(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return false;
            for (int i = 0; i < a.Length; i++)
                if (a[i] != b[i])
                    return false;
            return true;
        }

        /// The pack read off the wire: kept when valid, else the default.
        public static byte[] Clean(byte[] pack) => Valid(pack) ? pack : DefaultPack;

        /// The profile's line: "EdgeCut,LungeThrust;GuardedJab,Sidestep" (names, so a reordered enum keeps a save).
        public static string ToLine(byte[] pack)
        {
            var v = Chain(pack, 0);
            var s = Chain(pack, 1);
            return string.Join(",", Array.ConvertAll(v, m => m.ToString())) + ";" + string.Join(",", Array.ConvertAll(s, m => m.ToString()));
        }

        public static byte[] FromLine(string line)
        {
            if (string.IsNullOrEmpty(line))
                return DefaultPack;
            var halves = line.Split(';');
            if (halves.Length != 2)
                return DefaultPack;
            var chains = new VgMove[2][];
            for (int st = 0; st < 2; st++)
            {
                var names = halves[st].Split(',');
                chains[st] = new VgMove[names.Length];
                for (int i = 0; i < names.Length; i++)
                {
                    VgMove m;
                    if (!Enum.TryParse(names[i], out m) || m >= VgMove.None)
                        return DefaultPack;
                    chains[st][i] = m;
                }
            }
            return Clean(Pack(chains[0], chains[1]));
        }
    }
}

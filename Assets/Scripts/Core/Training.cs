using System;

namespace Arena
{
    /// One Training drill: a short exercise that teaches one mechanic (Docs/Training.md). Built drills can be started from the
    /// Game Mode screen's Training page (TrainingUi.cs); the rest show there as SOON.
    public sealed class TrainingDrill
    {
        public string Key, Name, Group, Goal, Copy;  // Key: the profile's id for it
        public int Steps = 1;        // steps to finish it (beacons, walls...)
        public string StepNoun = "STEPS";  // what they are, for the HUD's count ("BEACONS LEFT")
        public string[] Keys;        // the keys it teaches, as shown on its card (the default binds)
        public float Gold, Silver;   // medal times in seconds (bronze: finish it)
        public bool Built;           // playable yet
    }

    /// Training (`GameMode.Training`): the drills, in the page's order, grouped (Docs/Training.md). A lobby in Training never
    /// starts a run from the lobby's START: a drill starts from its own key on the page.
    public static class Training
    {
        /// The Training Pistol: every recruit's one weapon in a drill (no lobby place; Defs.WeaponOf finds it here).
        public static readonly WeaponDef Pistol = new WeaponDef { Id = WeaponId.TrainingPistol, Fire = (g, p, aim, dir, muzzle, dmg, extra) => g.FirePlainRound(p, dir, muzzle, dmg), Kit = Kit.Soldier, Role = Role.Dps, Choice = -1,
            Name = "Training Pistol", Desc = "Primary Fire: one plain round a shot.", Speed = 760f, Life = 0.85f, Damage = 30f, FireInterval = 0.24f,
            Sound = Sfx.ShotPistol, BotRange = 300f };

        public static readonly string[] Groups = { "MOVEMENT", "COMBAT" };

        public static readonly TrainingDrill[] Drills =
        {
            new TrainingDrill { Group = "MOVEMENT", Key = "move", Name = "Move and Aim", Keys = new[] { "WASD", "MOUSE" }, Gold = 15f, Silver = 25f, Built = true, Steps = MoveSteps, StepNoun = "BEACONS",
                Goal = "Reach the five beacons", Copy = "Walk onto the first three. A target stands on the last two: aim and bring it down." },
            new TrainingDrill { Group = "MOVEMENT", Key = "dodge", Name = "Dodge", Keys = new[] { "CTRL" }, Gold = 20f, Silver = 30f, Built = true, Steps = DodgeSteps, StepNoun = "WALLS",
                Goal = "Dodge through six bullet walls", Copy = "A dodge is a short dash with a blink of invulnerability. Dodge just as the wall reaches you." },
            new TrainingDrill { Group = "MOVEMENT", Key = "wallkick", Name = "Wall Kick", Keys = new[] { "CTRL", "HOLD CTRL" }, Gold = 15f, Silver = 25f, Built = true, Steps = KickSteps, StepNoun = "STEPS",
                Goal = "Kick off three walls, reach the flag", Copy = "Dodge into a lit wall to bounce off it, with a free dodge back. Hold the key after a dodge to sprint." },
            new TrainingDrill { Group = "COMBAT", Key = "shoot", Name = "Shoot", Keys = new[] { "LMB" }, Gold = 25f, Silver = 40f, Built = true, Steps = ShootSteps, StepNoun = "TARGETS",
                Goal = "Break twelve targets", Copy = "Hold left click to fire. The first stand still; then they slide, faster each time." },
            new TrainingDrill { Group = "COMBAT", Key = "parry", Name = "Parry", Keys = new[] { "RMB" }, Gold = 25f, Silver = 35f, Built = true, Steps = ParrySteps, StepNoun = "SHOTS",
                Goal = "Block ten shots", Copy = "Hold right click to raise the shield toward your cursor. Turn it to face each shot." },
            new TrainingDrill { Group = "COMBAT", Key = "tells", Name = "Read the Tells", Keys = new[] { "WASD", "CTRL" }, Gold = 16f, Silver = 24f, Built = true, Steps = TellSteps, StepNoun = "TELLS",
                Goal = "Slip eight wind-ups", Copy = "A Sniper's line, a Lunger's lane, a slam's circle: every big hit is shown first. Get out of it." },
        };

        // Move and Aim (drill 1): five beacons in a zig-zag across the arena, one lit at a time. The first three are reached by
        // standing on them; on the last two stands a target dummy you bring down (your hits only).
        public const int MoveSteps = 5;
        public const float BeaconRadius = 34f, BeaconReach = 46f, TargetHp = 260f;

        static readonly float[,] MoveSpots = { { 0.22f, 0.72f }, { 0.36f, 0.28f }, { 0.55f, 0.66f }, { 0.72f, 0.3f }, { 0.84f, 0.7f } };

        /// Where beacon `step` stands in an arena w x h.
        public static V2 MoveBeacon(int step, float w, float h)
        {
            int i = Math.Max(0, Math.Min(MoveSteps - 1, step));
            return new V2(MoveSpots[i, 0] * w, MoveSpots[i, 1] * h);
        }

        /// A beacon you shoot open (a dummy stands on it) rather than walk onto.
        public static bool MoveShootStep(int step) => step >= 3;

        // Dodge (drill 2): six walls of shots sweep across the arena, top to bottom, one at a time, from alternate sides and a
        // little faster each time. Dodge as one reaches you and you're through; get hit and it starts again from its side.
        public const int DodgeSteps = 6;
        public const float WallSpeed = 250f, WallSpeedStep = 30f, WallGap = 0.8f, WallSlack = 0.25f;  // WallSlack: past the dodge's invulnerability (the dodge reaches the host late)

        /// Wall `step`'s speed (px/s) and which way it runs (+1: from the left).
        public static float WallSpeedAt(int step) => WallSpeed + WallSpeedStep * step;
        public static int WallDir(int step) => step % 2 == 0 ? 1 : -1;

        // Wall Kick (drill 3): three wall panels stand in the open arena (the drill's floor plan, `KickLayout`); dodge into the lit
        // one to kick off it (your own game sees the kick and tells the host), then sprint to the flag.
        public const int KickWalls = 3, KickSteps = KickWalls + 1;
        public const float KickNear = 90f, FlagReach = 46f;

        static readonly float[,] KickPanels = { { 0.3f, 0.52f, 25f, 125f }, { 0.55f, 0.24f, 125f, 25f }, { 0.76f, 0.62f, 25f, 125f } };  // centre x, y (of w, h), half width, half height

        /// Kick panel i as a box (centre, half sizes) in an arena w x h, moved onto the 50 px floor grid (RoomTraps.SnapCentre).
        public static Solid KickPanel(int i, float w, float h) =>
            new Solid { Pos = new V2(RoomTraps.SnapCentre(KickPanels[i, 0] * w, KickPanels[i, 2]), RoomTraps.SnapCentre(KickPanels[i, 1] * h, KickPanels[i, 3])), HW = KickPanels[i, 2], HH = KickPanels[i, 3] };

        /// The flag at the end.
        public static V2 KickFlag(float w, float h) => new V2(0.9f * w, 0.16f * h);

        static LayoutDef kickLayout;

        /// The Wall Kick drill's floor plan: the three panels, nothing else (the edges stay open and wrap).
        public static LayoutDef KickLayout(float w, float h)
        {
            if (kickLayout == null)
            {
                var solids = new Solid[KickWalls];
                for (int i = 0; i < KickWalls; i++)
                    solids[i] = KickPanel(i, w, h);
                kickLayout = new LayoutDef { Id = LayoutId.Open, Name = "Wall Kick", Blurb = "", Edge = EdgeGates.Open, Solids = solids,
                    Crates = new LayoutCrate[0], PlayerGates = new EdgeSpan[0], EnemyGates = new EdgeSpan[0], Waypoints = new V2[0] };
            }
            return kickLayout;
        }

        /// How far p is from the edge of a box (0 inside).
        public static float BoxDistance(Solid s, V2 p)
        {
            float dx = Math.Max(0f, Math.Abs(p.X - s.Pos.X) - s.HW), dy = Math.Max(0f, Math.Abs(p.Y - s.Pos.Y) - s.HH);
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        // Shoot (drill 4): twelve targets, one at a time, each your own target dummy (only you hit or see it). The first three
        // stand still; after that each slides back and forth across its lane, a little faster than the last.
        public const int ShootSteps = 12, ShootStill = 3;
        public const float ShootGap = 0.35f, ShootHp = 140f, ShootSlide = 220f, ShootSpeed = 90f, ShootSpeedStep = 30f;

        static readonly float[,] ShootSpots = { { 0.5f, 0.3f }, { 0.25f, 0.6f }, { 0.75f, 0.6f }, { 0.5f, 0.22f }, { 0.3f, 0.78f }, { 0.7f, 0.3f },
            { 0.5f, 0.75f }, { 0.28f, 0.28f }, { 0.72f, 0.75f }, { 0.5f, 0.45f }, { 0.3f, 0.5f }, { 0.7f, 0.5f } };

        /// Target `step`'s lane centre in an arena w x h.
        public static V2 ShootSpot(int step, float w, float h)
        {
            int i = Math.Max(0, Math.Min(ShootSteps - 1, step));
            return new V2(ShootSpots[i, 0] * w, ShootSpots[i, 1] * h);
        }

        /// How fast target `step` slides (px/s; 0: it stands still).
        public static float ShootSpeedAt(int step) => step < ShootStill ? 0f : ShootSpeed + ShootSpeedStep * (step - ShootStill);

        /// Where target `step` is `t` seconds after it came up: sliding along its lane (a triangle wave, so the speed is even).
        public static V2 ShootAt(int step, float t, float w, float h)
        {
            var c = ShootSpot(step, w, h);
            float v = ShootSpeedAt(step);
            if (v <= 0f)
                return c;
            float period = 4f * ShootSlide / v, k = (t % period) / period;  // 0..1 over one trip out and back each way
            float off = k < 0.25f ? k * 4f : k < 0.75f ? 2f - k * 4f : k * 4f - 4f;  // 0 -> 1 -> -1 -> 0
            return new V2(c.X + off * ShootSlide, c.Y);
        }

        // Parry (drill 5): ten shots, one at a time, each from a new side (a ring of eight round you, never the same side twice
        // running). It homes on you, so you can't step out of it: meet it with the shield raised and facing it. Only yours.
        public const int ParrySteps = 10;
        public const float ParryFrom = 430f, ParrySpeed = 280f, ParrySpeedStep = 15f, ParryGap = 0.6f, ParryHitR = 24f;
        static readonly int[] ParrySides = { 0, 4, 2, 6, 1, 5, 3, 7, 0, 4 };  // eighths of a turn, from the right, clockwise on screen

        /// Where shot `step` comes from, round `at`.
        public static V2 ParryStart(int step, V2 at) => at + V2.FromAngle(ParrySides[Math.Max(0, Math.Min(ParrySteps - 1, step))] * (float)Math.PI / 4f) * ParryFrom;

        public static float ParrySpeedAt(int step) => ParrySpeed + ParrySpeedStep * step;

        // Read the Tells (drill 6): eight wind-ups, one at a time, each aimed where you stand as it shows: a Sniper's thin line,
        // a Lunger's wide lane, or a slam's circle. It fires when the wind-up ends: still in it is a hit (it comes again),
        // out of it and you've slipped it. The wind-ups get shorter.
        public const int TellSteps = 8;
        public const byte TellLine = 0, TellLane = 1, TellCircle = 2;
        public const float TellFrom = 420f, TellGap = 0.7f, TellLineHalf = 20f, TellLaneHalf = 46f, TellLaneLen = 300f, TellCircleR = 105f, TellFlash = 0.35f;
        static readonly byte[] TellKinds = { TellLine, TellLane, TellCircle, TellLine, TellLane, TellCircle, TellLine, TellLane };
        static readonly int[] TellSides = { 2, 6, 0, 5, 1, 0, 3, 7 };

        public static byte TellKind(int step) => TellKinds[Math.Max(0, Math.Min(TellSteps - 1, step))];
        public static float TellWindup(int step) => 1.15f - 0.06f * step;
        public static string TellName(byte kind) => kind == TellLine ? "SNIPER" : kind == TellLane ? "LUNGER" : "SLAM";

        /// Where tell `step` comes from, aimed at `at` (a slam comes down on the spot itself).
        public static V2 TellStart(int step, V2 at) =>
            TellKind(step) == TellCircle ? at : at + V2.FromAngle(TellSides[Math.Max(0, Math.Min(TellSteps - 1, step))] * (float)Math.PI / 4f) * TellFrom;

        /// Is `p` caught when the tell fires? A line runs from its start through the spot and on across the arena; a lane runs
        /// from its start to TellLaneLen past the spot; a circle is round the spot.
        public static bool TellCatches(byte kind, V2 from, V2 at, V2 p)
        {
            if (kind == TellCircle)
                return p.CloserThan(at, TellCircleR);
            V2 dir = (at - from).Normalized();
            float along = (p - from).Dot(dir), side = Math.Abs((p - from).Dot(new V2(-dir.Y, dir.X)));
            float end = kind == TellLine ? 3000f : (at - from).Length + TellLaneLen;
            return along >= 0f && along <= end && side <= (kind == TellLine ? TellLineHalf : TellLaneHalf);
        }

        /// The medal a best time earns: 3 gold, 2 silver, 1 bronze (finished), 0 none (best 0: never finished).
        public static int Medal(TrainingDrill d, float best) => best <= 0f ? 0 : best <= d.Gold ? 3 : best <= d.Silver ? 2 : 1;

        public static int BuiltCount
        {
            get
            {
                int n = 0;
                foreach (var d in Drills)
                    if (d.Built)
                        n++;
                return n;
            }
        }
    }
}

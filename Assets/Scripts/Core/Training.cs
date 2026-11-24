using System;

namespace Arena
{
    /// One Training drill: a short exercise that teaches one mechanic (Docs/Training.md). Built drills can be started from the
    /// Game Mode screen's Training page (TrainingUi.cs); the rest show there as SOON.
    public sealed class TrainingDrill
    {
        public string Key, Name, Group, Goal, Copy;  // Key: the profile's id for it
        public string[] Keys;        // the keys it teaches, as shown on its card (the default binds)
        public float Gold, Silver;   // medal times in seconds (bronze: finish it)
        public bool Built;           // playable yet
    }

    /// Training (`GameMode.Training`): the drills, in the page's order, grouped (Docs/Training.md). A lobby in Training never
    /// starts a run from the lobby's START: a drill starts from its own key on the page.
    public static class Training
    {
        public static readonly string[] Groups = { "MOVEMENT", "COMBAT" };

        public static readonly TrainingDrill[] Drills =
        {
            new TrainingDrill { Group = "MOVEMENT", Key = "move", Name = "Move and Aim", Keys = new[] { "WASD", "MOUSE" }, Gold = 15f, Silver = 25f, Built = true,
                Goal = "Reach the five beacons", Copy = "Walk onto the first three. A target stands on the last two: aim and bring it down." },
            new TrainingDrill { Group = "MOVEMENT", Key = "dodge", Name = "Dodge", Keys = new[] { "CTRL" }, Gold = 20f, Silver = 35f,
                Goal = "Dash through six bullet walls", Copy = "A dodge is a short dash with a blink of invulnerability. Time it into the wall." },
            new TrainingDrill { Group = "MOVEMENT", Key = "wallkick", Name = "Wall Kick", Keys = new[] { "HOLD CTRL" }, Gold = 20f, Silver = 35f,
                Goal = "Kick off three walls, reach the flag", Copy = "Hold after a dodge to sprint. Dodge into a wall to bounce off it with a free dodge." },
            new TrainingDrill { Group = "COMBAT", Key = "shoot", Name = "Shoot", Keys = new[] { "LMB" }, Gold = 20f, Silver = 35f,
                Goal = "Break twelve targets", Copy = "Hold left click to fire. Targets slide faster as the drill goes on." },
            new TrainingDrill { Group = "COMBAT", Key = "parry", Name = "Parry", Keys = new[] { "RMB" }, Gold = 25f, Silver = 40f,
                Goal = "Block ten shots", Copy = "Hold right click to raise the parry shield. It blocks shots from the front." },
            new TrainingDrill { Group = "COMBAT", Key = "tells", Name = "Read the Tells", Keys = new[] { "CTRL" }, Gold = 30f, Silver = 45f,
                Goal = "Slip eight wind-ups", Copy = "A Sniper's line, a Lunger's crouch: every big hit is shown first. Move when you see it." },
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

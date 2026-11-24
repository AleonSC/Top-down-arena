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
            new TrainingDrill { Group = "MOVEMENT", Key = "move", Name = "Move and Aim", Keys = new[] { "WASD", "MOUSE" }, Gold = 15f, Silver = 25f,
                Goal = "Touch the five beacons", Copy = "Move with WASD, aim with the mouse. Beacons light up one at a time." },
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

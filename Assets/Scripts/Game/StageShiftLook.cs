using UnityEngine;

namespace Arena
{
    /// The 2 s stage changes of the Warden and the Hive Queen: on the floor, sirens (Warden) or wax cells
    /// spiralling in (Queen) and a pulsing tint; on the boss, energy streaming in, rings closing round it and
    /// shockwaves going out, then a flash as the new stage starts; on the HUD, a banner naming the new stage.
    public partial class ArenaApp
    {
        struct StageShift
        {
            public Enemy Boss;
            public float Progress;     // 0 -> 1 over the change
            public int Number;         // the stage it's changing to (2, 3)
            public string Name, Line;
            public Rgba Color, Second;
            public bool Warden, Reapers;
        }

        static bool CurrentShift(Game g, out StageShift s)
        {
            s = default(StageShift);
            var R = g.Reapers;
            if (R != null && R.Trans != 0)
            {
                Enemy boss = null;
                foreach (var e in g.Enemies.Values)
                    if (e.Type == EnemyType.DualReaper || (boss == null && e.Type == EnemyType.TwinA))
                        boss = e;
                if (boss != null)
                {
                    bool fuse = R.Trans == 1;
                    s = new StageShift
                    {
                        Boss = boss, Progress = Mathf.Clamp01(R.TransT / (fuse ? Game.FuseTime : Game.SplitTime)), Number = fuse ? 2 : 3, Reapers = true,
                        Name = fuse ? "THE DUAL REAPER" : "SUNDERED",
                        Line = fuse ? "Dusk and Dawn become one" : "The tether snaps",
                        Color = fuse ? new Rgba(1f, 0.8f, 0.35f) : new Rgba(1f, 0.35f, 0.35f),
                        Second = fuse ? new Rgba(1f, 0.95f, 0.75f) : new Rgba(1f, 0.6f, 0.75f),
                    };
                    return true;
                }
            }
            foreach (var e in g.Enemies.Values)
            {
                if (e.Type == EnemyType.Boss && e.Warden != null && e.Warden.TransT > 0f)
                {
                    var to = e.Warden.TransTo;
                    s = new StageShift
                    {
                        Boss = e, Progress = Mathf.Clamp01(1f - e.Warden.TransT / 2f), Number = (int)to + 1, Warden = true,
                        Name = to == WardenStage.Lockdown ? "LOCKDOWN" : "RIOT",
                        Line = to == WardenStage.Lockdown ? "The bars come down" : "The prisoners are loose",
                        Color = to == WardenStage.Lockdown ? new Rgba(0.35f, 0.65f, 1f) : new Rgba(1f, 0.5f, 0.2f),
                        Second = to == WardenStage.Lockdown ? new Rgba(1f, 0.25f, 0.3f) : new Rgba(1f, 0.85f, 0.3f),
                    };
                    return true;
                }
                if (e.Type == EnemyType.Singularity && e.Sing != null && e.Sing.TransT > 0f)
                {
                    bool acc = e.Sing.TransTo == SingStage.Accretion;
                    s = new StageShift
                    {
                        Boss = e, Progress = Mathf.Clamp01(1f - e.Sing.TransT / 2f), Number = (int)e.Sing.TransTo + 1,
                        Name = acc ? "ACCRETION" : "SPAGHETTIFY",
                        Line = acc ? "The wells fall in" : "Nothing escapes",
                        Color = acc ? new Rgba(0.75f, 0.45f, 1f) : new Rgba(1f, 0.45f, 0.85f),
                        Second = acc ? new Rgba(0.9f, 0.75f, 1f) : new Rgba(0.5f, 0.84f, 1f),
                    };
                    return true;
                }
                if (e.Type == EnemyType.Lich && e.Lich != null && e.Lich.TransT > 0f)
                {
                    bool harvest = e.Lich.TransTo == LichStage.Harvest;
                    s = new StageShift
                    {
                        Boss = e, Progress = Mathf.Clamp01(1f - e.Lich.TransT / 2f), Number = (int)e.Lich.TransTo + 1,
                        Name = harvest ? "SOUL HARVEST" : "UNDEATH",
                        Line = harvest ? "Three jars hold its soul" : "Its body falls away",
                        Color = harvest ? new Rgba(0.37f, 0.85f, 1f) : new Rgba(0.7f, 0.55f, 1f),
                        Second = harvest ? new Rgba(0.43f, 1f, 0.69f) : new Rgba(0.91f, 0.89f, 0.8f),
                    };
                    return true;
                }
                if (e.Type == EnemyType.HiveQueen && e.Queen != null && e.Queen.TransT > 0f)
                {
                    var to = e.Queen.TransTo;
                    s = new StageShift
                    {
                        Boss = e, Progress = Mathf.Clamp01(1f - e.Queen.TransT / 2f), Number = (int)to + 1,
                        Name = to == QueenStage.Honeycomb ? "HONEYCOMB" : "FRENZY",
                        Line = to == QueenStage.Honeycomb ? "The hive takes shape" : "The hive is angry",
                        Color = to == QueenStage.Honeycomb ? new Rgba(1f, 0.67f, 0.15f) : new Rgba(1f, 0.38f, 0.22f),
                        Second = to == QueenStage.Honeycomb ? new Rgba(1f, 0.9f, 0.5f) : new Rgba(1f, 0.8f, 0.3f),
                    };
                    return true;
                }
            }
            return false;
        }

        static float Ease(float x) => 1f - (1f - x) * (1f - x) * (1f - x);

        /// On the floor, under everything: sirens or wax, and the arena pulsing with the new stage's color.
        void DrawStageShiftFloor(float t)
        {
            StageShift s;
            if (!CurrentShift(game, out s))
                return;
            V2 c = s.Boss.Disp;
            float p = s.Progress;
            // The whole arena pulses, faster as the change builds.
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (6f + 14f * p));
            FillRect(0f, 0f, Game.Width, Game.Height, s.Color.WithAlpha(0.04f + 0.05f * pulse * p));
            if (s.Warden)
            {
                // Siren beams: two lights spinning from the Warden, sweeping the whole arena.
                for (int k = 0; k < 2; k++)
                {
                    float a = t * 4.5f + k * MathX.Pi;
                    var col = (k == 0 ? s.Color : s.Second).WithAlpha(0.13f);
                    V2 d0 = V2.FromAngle(a - 0.22f), d1 = V2.FromAngle(a + 0.22f);
                    SetColor(col);
                    Tri(c, c + d0 * 1900f, c + d1 * 1900f);
                    SetColor(col.WithAlpha(0.1f));
                    Tri(c, c + V2.FromAngle(a - 0.08f) * 1900f, c + V2.FromAngle(a + 0.08f) * 1900f);
                }
            }
            else if (s.Reapers)
            {
                // Dusk's pink and Dawn's blue streaming in from the edges.
                for (int k = 0; k < 24; k++)
                {
                    float f = MathX.Fmod(t * 0.9f + k / 24f, 1f), a = k * 2.399f;
                    V2 at = c + V2.FromAngle(a) * (900f * (1f - Ease(f)) + 60f);
                    Line(at, at + V2.FromAngle(a) * 40f, 3f, (k % 2 == 0 ? new Rgba(1f, 0.35f, 0.6f) : new Rgba(0.45f, 0.55f, 1f)).WithAlpha(0.6f * Mathf.Sin(f * MathX.Pi)));
                }
            }
            else
            {
                // Wax cells spiralling in from the edges toward her.
                for (int k = 0; k < 18; k++)
                {
                    float f = MathX.Fmod(p * 1.6f + k / 18f, 1f), a = k * 2.4f + f * 2.2f;
                    V2 at = c + V2.FromAngle(a) * (900f * (1f - Ease(f)) + 50f);
                    float size = 26f * (1f - f) + 6f;
                    HexOutline(at, size, 2f, s.Color.WithAlpha(0.55f * Mathf.Sin(f * MathX.Pi)));
                }
            }
        }

        /// On the boss: energy streaming in, segmented rings closing, shockwaves out, then a flash at the end.
        void DrawStageShiftBoss(Enemy e, V2 pos, float r, float t)
        {
            StageShift s;
            if (!CurrentShift(game, out s) || s.Boss != e)
                return;
            float p = s.Progress;
            // Streaks of light pulled in from all round.
            for (int k = 0; k < 28; k++)
            {
                float f = MathX.Fmod(t * 1.3f + k * 0.137f, 1f), a = k * 2.399f;
                float d0 = r + 30f + 380f * (1f - f), d1 = d0 + 40f + 50f * (1f - f);
                Line(pos + V2.FromAngle(a) * d0, pos + V2.FromAngle(a) * d1, 2f, (k % 2 == 0 ? s.Color : s.Second).WithAlpha(0.6f * f));
            }
            // Three segmented rings closing in and spinning opposite ways.
            for (int k = 0; k < 3; k++)
            {
                float rr = r + 20f + (140f - 40f * k) * (1f - Ease(p)), spin = t * (k % 2 == 0 ? 2.2f : -2.8f) + k;
                for (int seg = 0; seg < 6; seg++)
                    Arc(pos, rr, 3f, s.Color.Mix(Rgba.White, 0.3f * k).WithAlpha(0.75f), 8, spin + seg * MathX.Tau / 6f, 0.7f);
            }
            // Shockwaves going out, three of them across the change.
            for (int k = 0; k < 3; k++)
            {
                float f = MathX.Fmod(p * 1.5f + k / 3f, 1f);
                Arc(pos, r + 20f + 700f * Ease(f), 6f * (1f - f) + 1f, s.Color.WithAlpha(0.5f * (1f - f)), 64);
            }
            // The core glows hotter, and whites out right at the end.
            Circle(pos, r + 10f + 6f * Mathf.Sin(t * 20f), s.Second.WithAlpha(0.15f + 0.25f * p), 32);
            if (p > 0.85f)
            {
                float f = (p - 0.85f) / 0.15f;
                Circle(pos, r + 40f + 260f * f, Rgba.White.WithAlpha(0.35f * f), 40);
            }
        }

        /// The banner across the screen: the stage's number, its name and a line about it.
        void DrawStageBanner()
        {
            StageShift s;
            if (!CurrentShift(game, out s))
                return;
            float p = s.Progress, tIn = Mathf.Clamp01(p / 0.15f), tOut = Mathf.Clamp01((1f - p) / 0.12f), a = Ease(tIn) * tOut;
            float y = H * 0.27f, h = 110f * Ease(tIn);
            var band = ToColor(s.Color);
            Ui.Fill(new Rect(0f, y + 55f - h / 2f, W, h), new Color(0.03f, 0.03f, 0.06f, 0.72f * a));
            Ui.Fill(new Rect(0f, y + 55f - h / 2f, W * Ease(tIn), 3f), new Color(band.r, band.g, band.b, 0.9f * a));
            Ui.Fill(new Rect(W * (1f - Ease(tIn)), y + 55f + h / 2f - 3f, W * Ease(tIn), 3f), new Color(band.r, band.g, band.b, 0.9f * a));
            // The name slides in from the right and settles, the small lines slide in from the left.
            float slide = (1f - Ease(tIn)) * 260f;
            Ui.Label(new Rect(-slide, y + 8f, W, 20f), "STAGE " + s.Number + " / 3", 13, new Color(1f, 1f, 1f, 0.75f * a), TextAnchor.MiddleCenter, true);
            var top = ToColor(s.Second.Mix(Rgba.White, 0.5f));
            Ui.Fancy(new Rect(slide, y + 26f, W, 56f), s.Name, 46, new Color(top.r, top.g, top.b, a), new Color(band.r, band.g, band.b, a), TextAnchor.MiddleCenter,
                new Color(band.r, band.g, band.b, a));
            Ui.Label(new Rect(-slide, y + 82f, W, 20f), s.Line, 14, new Color(0.9f, 0.92f, 1f, 0.85f * a), TextAnchor.MiddleCenter);
        }
    }
}

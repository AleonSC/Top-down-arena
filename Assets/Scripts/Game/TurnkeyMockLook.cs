using UnityEngine;

namespace Arena
{
    /// MOCKUP (preview only, TKMOCK=A|B|C): the Turnkey rework's Cell Doors set into the arena's outer wall, facing in
    /// (pos on the wall line, Facing into the arena), each with red warning lights that speed up as its next Rioter nears
    /// (T2 counts down to it) and stay lit while the door is open (PhaseT); jammed: dark, chained and padlocked.
    public partial class ArenaApp
    {
        internal static char TkMock;  // 0: the current look; 'A' steel door, 'B' barred gate, 'C' blast door
        internal static bool TkTrapMock;  // the refreshed traps
        internal static V2 TkBuildAt; internal static float TkBuildFacing;  // where the building Turnkey's door goes (on the wall)

        static readonly Rgba WallConcrete = new Rgba(0.2f, 0.215f, 0.25f), WallEdge = new Rgba(0.33f, 0.35f, 0.4f),
            AlarmRed = new Rgba(1f, 0.16f, 0.12f), AlarmHot = new Rgba(1f, 0.75f, 0.65f), HazardBlack = new Rgba(0.08f, 0.08f, 0.1f),
            CellSteel = new Rgba(0.42f, 0.46f, 0.52f), CellSteelLit = new Rgba(0.66f, 0.71f, 0.78f);

        /// 0 calm, 1 a Rioter is about to come out (its last 1.5 s).
        static float DoorWarn(Enemy e) => e.Phase == 1 ? 1f - Mathf.Clamp01(e.T2 / 1.5f) : 0f;

        /// A red alarm lamp: off is a dark red lens; on a hot centre, a red lens and a soft halo.
        void AlarmLamp(V2 at, float r, float on)
        {
            if (on > 0.02f)
            {
                Circle(at, r * 9f, AlarmRed.WithAlpha(0.05f * on), 24);
                Circle(at, r * 5f, AlarmRed.WithAlpha(0.1f * on), 22);
                Circle(at, r * 2.6f, AlarmRed.WithAlpha(0.25f * on), 18);
            }
            Circle(at, r + 1.6f + OffInk, GuardInk, 16);
            Circle(at, r + 1.6f, new Rgba(0.3f, 0.31f, 0.35f), 16);  // its housing
            Circle(at, r, new Rgba(0.32f, 0.07f, 0.07f).Mix(AlarmRed, on), 16);
            Line(at - new V2(r, 0f), at + new V2(r, 0f), 0.9f, new Rgba(0.2f, 0.2f, 0.24f, 0.8f));  // the cage over the lens
            Line(at - new V2(0f, r), at + new V2(0f, r), 0.9f, new Rgba(0.2f, 0.2f, 0.24f, 0.8f));
            if (on > 0.02f)
                Circle(at - new V2(r * 0.3f, r * 0.3f), r * 0.4f, AlarmHot.WithAlpha(on), 10);
        }

        /// The lamps' state: alternating left/right, slow when calm and fast when a Rioter is due; both on while open.
        static float LampOn(Enemy e, float t, int side)
        {
            if (e.Phase != 1)
                return e.Phase == 2 ? (side < 0 && Mathf.Sin(t * 1.3f) > 0.97f ? 0.35f : 0f) : 0f;  // jammed: a dying flicker
            if (e.PhaseT > 0f)
                return 1f;
            float warn = DoorWarn(e), rate = Mathf.Lerp(1.4f, 9f, warn);
            float wave = Mathf.Sin(t * rate * MathX.Tau + (side > 0 ? MathX.Pi : 0f));
            return wave > 0f ? Mathf.Lerp(0.55f, 1f, warn) : 0.08f;
        }

        /// The red wash on the floor in front: a fan spilling into the arena, stronger as the Rioter nears, and chevrons.
        void DoorSpill(Enemy e, V2 pos, V2 f, V2 s, float half, float t)
        {
            if (e.Phase != 1)
                return;
            float warn = DoorWarn(e), open = e.PhaseT > 0f ? 1f : 0f;
            float a = Mathf.Max(open, warn) * (0.55f + 0.45f * Mathf.Abs(Mathf.Sin(t * Mathf.Lerp(4f, 18f, warn))));
            float calm = 0.05f;
            float len = 70f + 60f * Mathf.Max(warn, open);
            V2 m0 = pos + f * 50f, m1 = pos + f * (18f + len);
            Quad(m0 - s * half, m0 + s * half, m1 + s * (half + len * 0.45f), m1 - s * (half + len * 0.45f), AlarmRed.WithAlpha(calm + 0.12f * a));
            Quad(m0 - s * half * 0.6f, m0 + s * half * 0.6f, m1 + s * (half * 0.6f + len * 0.25f), m1 - s * (half * 0.6f + len * 0.25f), AlarmRed.WithAlpha(0.03f + 0.1f * a));
            for (int k = 0; k < 3; k++)  // chevrons marching out: the way it comes
            {
                float u = MathX.Fmod(t * (0.6f + 1.4f * warn) + k / 3f, 1f);
                V2 c = pos + f * (60f + u * (len - 20f));
                float al = (0.18f + 0.6f * Mathf.Max(warn, open)) * Mathf.Sin(u * MathX.Pi);
                Cap(c - s * 12f, c + f * 7f, 2.4f, AlarmRed.WithAlpha(al));
                Cap(c + s * 12f, c + f * 7f, 2.4f, AlarmRed.WithAlpha(al));
            }
        }

        /// The concrete frame round the doorway, set into the wall: posts, lintel with hazard stripes, and the lamps.
        void DoorFrame(V2 pos, V2 f, V2 s, float half, float depth)
        {
            V2 b = pos - f * 14f, n = pos + f * depth;
            float w = half + 12f;
            Quad(b - s * (w + OffInk), b + s * (w + OffInk), n + s * (w + OffInk) + f * OffInk, n - s * (w + OffInk) + f * OffInk, GuardInk);
            Quad(b - s * w, b + s * w, n + s * w, n - s * w, WallConcrete);
            Cap(n - s * w, n + s * w, 2f, WallEdge);  // its lip
            // hazard stripes on the lip
            for (int k = 0; k < 10; k++)
            {
                float u0 = -w + 2f * w * k / 10f, u1 = u0 + w / 10f;
                if (k % 2 == 0)
                    Quad(n - f * 5f + s * u0, n - f * 5f + s * u1, n - f * 1f + s * (u1 + 3f), n - f * 1f + s * (u0 + 3f), SignYellow.Darkened(0.15f));
                else
                    Quad(n - f * 5f + s * u0, n - f * 5f + s * u1, n - f * 1f + s * (u1 + 3f), n - f * 1f + s * (u0 + 3f), HazardBlack);
            }
            // the posts: rivets
            for (int side = -1; side <= 1; side += 2)
                for (int k = 0; k < 2; k++)
                    Circle(pos + s * (side * (half + 6f)) + f * (k * 8f - 4f), 1.2f, WallEdge, 6);
        }

        /// The cell itself, beyond the wall (off the map): a dark room with a bunk and a prisoner pacing, lit orange
        /// when the door opens, a red glow inside as one is due.
        void CellBehind(Enemy e, V2 pos, V2 f, V2 s, float half, float swing, float t)
        {
            float d = 64f, w = half + 12f;
            V2 b0 = pos - f * 14f, b1 = pos - f * (14f + d);
            Quad(b0 - s * (w + OffInk), b0 + s * (w + OffInk), b1 + s * (w + OffInk) - f * OffInk, b1 - s * (w + OffInk) - f * OffInk, GuardInk);
            Quad(b0 - s * w, b0 + s * w, b1 + s * w, b1 - s * w, WallConcrete.Darkened(0.25f));
            V2 c = pos - f * (14f + d * 0.5f);
            Quad(c - s * (w - 5f) - f * (d * 0.5f - 5f), c + s * (w - 5f) - f * (d * 0.5f - 5f), c + s * (w - 5f) + f * (d * 0.5f - 5f), c - s * (w - 5f) + f * (d * 0.5f - 5f), new Rgba(0.07f, 0.075f, 0.09f));
            float warn = DoorWarn(e);
            if (e.Phase == 1)
            {
                var gl = (swing > 0f ? CellGlow : AlarmRed).WithAlpha(swing > 0f ? 0.3f * swing + 0.08f : 0.05f + 0.12f * warn * (0.6f + 0.4f * Mathf.Sin(t * 12f)));
                for (int k = 0; k < 3; k++)  // the light from the doorway, strongest at it
                {
                    float u = (k + 1) / 3f;
                    Quad(b0 - s * (w - 5f) * u, b0 + s * (w - 5f) * u, b0 + s * (w - 5f) * u - f * (d - 5f) * u, b0 - s * (w - 5f) * u - f * (d - 5f) * u, gl);
                }
            }
            // the bunk along the back wall and the toilet in the corner
            V2 bk = c - f * (d * 0.5f - 12f) - s * (w * 0.35f);
            Quad(bk - s * 18f - f * 7f, bk + s * 18f - f * 7f, bk + s * 18f + f * 7f, bk - s * 18f + f * 7f, new Rgba(0.28f, 0.3f, 0.34f));
            Quad(bk - s * 16f - f * 5f, bk - s * 8f - f * 5f, bk - s * 8f + f * 5f, bk - s * 16f + f * 5f, new Rgba(0.6f, 0.6f, 0.62f));  // the pillow
            Circle(c - f * (d * 0.5f - 11f) + s * (w - 14f), 5f, new Rgba(0.55f, 0.58f, 0.62f), 12);
            Circle(c - f * (d * 0.5f - 11f) + s * (w - 14f), 3f, new Rgba(0.15f, 0.17f, 0.2f), 10);
            if (e.Phase == 1 && swing <= 0f)
            {
                // the next Rioter, pacing (at the door, ready, as it's due)
                float pace = Mathf.Sin(t * 0.8f + e.Id * 1.7f);
                V2 who = Vector2Lerp(c + s * (pace * w * 0.45f) + f * 4f, pos - f * 16f, warn);
                Circle(who, 9f + OffInk, GuardInk, 16);
                Circle(who, 9f, new Rgba(0.95f, 0.55f, 0.15f).Darkened(0.35f - 0.25f * warn), 16);
                Circle(who + f * (2f + 2f * warn), 5f, new Rgba(0.75f, 0.58f, 0.45f).Darkened(0.35f - 0.25f * warn), 12);
            }
        }


        /// In the body pass (under the walls): only the red wash on the floor in front.
        void DrawWallDoor(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            DoorSpill(e, pos, f, s, 34f, t);
            BeaconSweep(e, pos, f, s, t);
        }

        /// After the walls: every wall door's cell, frame and door, and a building Turnkey's blueprint.
        void DrawTurnkeyMockDoors(float t)
        {
            if (TkMock == 0 || game == null)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type == EnemyType.Turnkey && e.Phase == 3)
                    DrawDoorBlueprint(TkBuildAt, TkBuildFacing, 1f - Mathf.Clamp01(e.PhaseT / Game.TurnkeyBuild), t);
                if (e.Type != EnemyType.CellDoor)
                    continue;
                V2 pos = e.Disp;
                V2.Axes(e.Facing, out V2 f, out V2 s);
                float half = 34f;
                bool jammed = e.Phase == 2;
                float swing = jammed ? 0f : Mathf.Sin(Mathf.Clamp01(e.PhaseT / Game.DoorSwing) * MathX.Pi);
                switch (TkMock)
                {
                    case 'A': DoorSteel_A(e, pos, f, s, half, swing, jammed, t); break;
                    case 'B': DoorBars_B(e, pos, f, s, half, swing, jammed, t); break;
                    default: DoorBlast_C(e, pos, f, s, half, swing, jammed, t); break;
                }
                if (jammed)
                {
                    V2 c = pos + f * 18f;
                    for (int k = 0; k <= 8; k++)  // a chain across the doorway
                    {
                        float u = -1f + 2f * k / 8f;
                        Circle(c + s * (u * (half + 4f)) + f * (3f * (1f - u * u)), 2.3f, new Rgba(0.75f, 0.77f, 0.82f), 10);
                    }
                    V2 lk = c + f * 6f;
                    Arc(lk - f * 3f, 3.2f, 1.4f, new Rgba(0.75f, 0.77f, 0.82f), 12, e.Facing + MathX.Pi / 2f, MathX.Pi);
                    Quad(lk - s * 4.5f - f * 1f, lk + s * 4.5f - f * 1f, lk + s * 4.5f + f * 6f, lk - s * 4.5f + f * 6f, GuardInk);
                    Quad(lk - s * 3.5f, lk + s * 3.5f, lk + s * 3.5f + f * 5f, lk - s * 3.5f + f * 5f, new Rgba(0.88f, 0.26f, 0.2f));
                }
            }
        }

        /// The front-view frame shared by the three looks: a shadow on the floor, concrete posts and a hazard-striped
        /// lintel; d0..d1 is the doorway (lintel to floor), dark inside, lit orange as the door opens.
        void FrontFrame(V2 pos, V2 f, V2 s, float half, float swing, out V2 d0, out V2 d1)
        {
            float H = 46f, w = half + 11f;
            V2 top = pos - f * 13f, foot = pos + f * H;
            Quad(foot - s * (w + 4f), foot + s * (w + 4f), foot + s * (w + 2f) + f * 7f, foot - s * (w + 2f) + f * 7f, new Rgba(0f, 0f, 0f, 0.35f));
            Quad(top - s * (w + OffInk), top + s * (w + OffInk), foot + s * (w + OffInk) + f * OffInk, foot - s * (w + OffInk) + f * OffInk, GuardInk);
            Quad(top - s * w, top + s * w, foot + s * w, foot - s * w, WallConcrete);
            Quad(top - s * w, top + s * w, top + s * w + f * 9f, top - s * w + f * 9f, WallEdge.Darkened(0.2f));  // the lintel
            for (int k = 0; k < 8; k++)  // hazard stripes on it
            {
                float u0 = -w + 4f + (2f * w - 8f) * k / 8f, u1 = u0 + (w - 4f) / 8f;
                Quad(top + s * u0 + f * 2f, top + s * u1 + f * 2f, top + s * (u1 + 3f) + f * 7f, top + s * (u0 + 3f) + f * 7f, k % 2 == 0 ? SignYellow.Darkened(0.15f) : HazardBlack);
            }
            for (int side = -1; side <= 1; side += 2)  // bolts down the posts
                for (int k = 0; k < 3; k++)
                    Circle(top + f * (18f + k * 12f) + s * (side * (w - 5f)), 1.1f, WallEdge, 6);
            d0 = top + f * 9f; d1 = foot;
            Quad(d0 - s * half, d0 + s * half, d1 + s * half, d1 - s * half, new Rgba(0.04f, 0.04f, 0.06f));
            if (swing > 0f)
            {
                Quad(d0 - s * half, d0 + s * half, d1 + s * half, d1 - s * half, CellGlow.WithAlpha(0.45f * swing));
                Quad(d1 - s * half, d1 + s * half, d1 + s * (half + 12f) + f * 30f, d1 - s * (half + 12f) + f * 30f, CellGlow.WithAlpha(0.18f * swing));  // light spilling out
            }
        }

        /// A: a solitary-confinement door seen from the front, like a door in an old top-down dungeon: a riveted steel
        /// face with a barred window (the cell's light, and a face behind it as a Rioter's due) and a food slot; it slides
        /// up into the wall to open. A caged red lamp on each post, flashing in turn.
        void DoorSteel_A(Enemy e, V2 pos, V2 f, V2 s, float half, float swing, bool jammed, float t)
        {
            float warn = DoorWarn(e), w = half + 11f;
            FrontFrame(pos, f, s, half, swing, out V2 d0, out V2 d1);
            float faceLen = (d1 - d0).Length * (1f - swing * 0.92f);
            V2 fa = d0, fb = d0 + f * faceLen;
            Quad(fa - s * half, fa + s * half, fb + s * half, fb - s * half, CellSteel);
            Quad(fa - s * (half - 4f) + f * 3f, fa + s * (half - 4f) + f * 3f, fb + s * (half - 4f) - f * Mathf.Min(3f, faceLen), fb - s * (half - 4f) - f * Mathf.Min(3f, faceLen), CellSteel.Darkened(0.12f));  // its panel
            Cap(fb - s * half, fb + s * half, 1.8f, CellSteelLit);  // its bottom edge, catching the light
            for (int k = 0; k < 5; k++)  // rivets down each side
            {
                float u = (k + 0.5f) / 5f * faceLen;
                Circle(fa + f * u - s * (half - 2f), 1f, CellSteelLit, 6);
                Circle(fa + f * u + s * (half - 2f), 1f, CellSteelLit, 6);
            }
            V2 wc = fa + f * (faceLen - (d1 - d0).Length * 0.66f);
            if (faceLen > 30f)
            {
                V2 w0 = wc - s * 15f - f * 7f, w1 = wc + s * 15f - f * 7f, w2 = wc + s * 15f + f * 7f, w3 = wc - s * 15f + f * 7f;
                Quad(w0 - (s + f) * 1.5f, w1 + (s - f) * 1.5f, w2 + (s + f) * 1.5f, w3 + (f - s) * 1.5f, GuardInk);
                Quad(w0, w1, w2, w3, new Rgba(0.08f, 0.05f, 0.05f));
                if (e.Phase == 1)
                    Quad(w0, w1, w2, w3, CellGlow.WithAlpha(0.25f + 0.45f * warn));
                if (e.Phase == 1 && warn > 0.05f)
                {
                    V2 head = wc + s * (3f * Mathf.Sin(t * 2f)) + f * 2f;
                    Circle(head, 6.5f, new Rgba(0.05f, 0.03f, 0.03f, warn), 14);  // a head against the light
                    Circle(head - s * 2.4f - f * 1f, 1f, AlarmRed.WithAlpha(warn), 6);  // its eyes
                    Circle(head + s * 2.4f - f * 1f, 1f, AlarmRed.WithAlpha(warn), 6);
                }
                for (int k = 0; k < 4; k++)
                {
                    V2 bx = wc + s * (-11.25f + 7.5f * k);
                    Cap(bx - f * 7f, bx + f * 7f, 1.6f, CellSteelLit);
                }
                V2 sl = fa + f * (faceLen - 8f);  // the food slot and the lock
                Quad(sl - s * 9f - f * 2f, sl + s * 9f - f * 2f, sl + s * 9f + f * 2f, sl - s * 9f + f * 2f, new Rgba(0.12f, 0.13f, 0.16f));
                Circle(wc + s * (half - 8f) + f * 10f, 2.4f, KeyBrass, 10);
            }
            V2 top = pos - f * 13f;
            AlarmLamp(top + f * 5f - s * (w - 5f), 4.4f, LampOn(e, t, -1));
            AlarmLamp(top + f * 5f + s * (w - 5f), 4.4f, LampOn(e, t, 1));
        }

        /// B: a barred cell front: a prisoner in orange pacing behind the bars (gripping them as it's due), the gate sliding
        /// sideways into the wall to open; a red beacon on the lintel spinning, its beam sweeping the floor.
        void DoorBars_B(Enemy e, V2 pos, V2 f, V2 s, float half, float swing, bool jammed, float t)
        {
            float warn = DoorWarn(e);
            FrontFrame(pos, f, s, half, swing, out V2 d0, out V2 d1);
            float L = (d1 - d0).Length;
            if (e.Phase == 1)
                Quad(d1 - s * half - f * (L * 0.5f), d1 + s * half - f * (L * 0.5f), d1 + s * half, d1 - s * half, CellGlow.WithAlpha(0.12f + 0.18f * warn));  // the cell's light, low
            if (!jammed && swing <= 0f)
            {
                // the prisoner, front view: orange jumpsuit shoulders, head; at the bars as it's due
                float x = Mathf.Lerp(half * 0.45f * Mathf.Sin(t * 0.9f + e.Id), 0f, warn);
                V2 body = d0 + f * (L * 0.72f) + s * x;
                var suit = new Rgba(0.95f, 0.5f, 0.12f).Darkened(0.45f - 0.3f * warn);
                Quad(body - s * 11f, body + s * 11f, body + s * 9f + f * (L * 0.3f), body - s * 9f + f * (L * 0.3f), suit);
                Circle(body - f * 2f, 11f, suit, 16);
                Circle(body - f * 13f, 7f, new Rgba(0.78f, 0.6f, 0.47f).Darkened(0.45f - 0.3f * warn), 14);
                if (warn > 0.3f)  // its hands on the bars
                {
                    Circle(body - s * 8f - f * 6f, 2.6f, new Rgba(0.78f, 0.6f, 0.47f), 8);
                    Circle(body + s * 8f - f * 6f, 2.6f, new Rgba(0.78f, 0.6f, 0.47f), 8);
                }
            }
            // the gate: vertical bars and two cross bars, slid sideways
            float slide = swing * half * 2f;
            V2 g0 = pos - s * half + s * slide;
            for (int k = 0; k <= 7; k++)
            {
                float off = half * 2f * k / 7f + slide;
                if (off > half * 2f + 0.5f)
                    continue;  // in the wall
                V2 b = d0 - s * half + s * off;
                Cap(b, b + f * L, 2.4f + OffInk, GuardInk);
                Cap(b, b + f * L, 2.4f, CellSteelLit);
                Line(b - s * 0.8f, b - s * 0.8f + f * L, 0.8f, Rgba.White.WithAlpha(0.45f));
            }
            for (int k = 0; k < 2; k++)
            {
                V2 c0 = d0 + f * (L * (0.18f + 0.6f * k)) - s * half + s * slide, c1 = d0 + f * (L * (0.18f + 0.6f * k)) + s * half;
                if ((c1 - c0).Length > 1f)
                {
                    Cap(c0, c1, 3f + OffInk, GuardInk);
                    Cap(c0, c1, 3f, CellSteel);
                }
            }
            if (!jammed)
                Circle(d0 + f * (L * 0.5f) - s * half + s * slide + s * 4f, 2.6f, KeyBrass, 10);  // the lock box
            // the beacon on the lintel
            V2 bc = pos - f * 8f;
            bool live = e.Phase == 1;
            Quad(bc - s * 9f - f * 2f, bc + s * 9f - f * 2f, bc + s * 9f + f * 5f, bc - s * 9f + f * 5f, GuardInk);
            Quad(bc - s * 8f - f * 1f, bc + s * 8f - f * 1f, bc + s * 8f + f * 4f, bc - s * 8f + f * 4f, new Rgba(0.3f, 0.31f, 0.35f));
            float on = live ? (e.PhaseT > 0f || warn > 0f ? 1f : 0.55f + 0.45f * Mathf.Sin(t * 6f)) : 0f;
            AlarmLamp(bc - f * 2f, 6f, on);
            if (live)
            {
                float spin = t * Mathf.Lerp(3f, 12f, Mathf.Max(warn, swing > 0f ? 1f : 0f));
                V2 rd = V2.FromAngle(spin);  // the reflector going round inside the dome
                Cap(bc - f * 2f - rd * 4f, bc - f * 2f + rd * 4f, 2f, AlarmHot.WithAlpha(0.9f));
            }
        }

        /// The beacon's beam sweeping the floor in front (body pass, under the door).
        void BeaconSweep(Enemy e, V2 pos, V2 f, V2 s, float t)
        {
            if (TkMock != 'B' || e.Phase != 1)
                return;
            float warn = DoorWarn(e), spin = t * Mathf.Lerp(3f, 12f, Mathf.Max(warn, e.PhaseT > 0f ? 1f : 0f));
            V2 bc = pos + f * 40f;
            for (int k = 0; k < 2; k++)
            {
                float ang = e.Facing + Mathf.Sin(spin + k * MathX.Pi) * 1.1f;
                V2 d = V2.FromAngle(ang), o = d.Orthogonal();
                float Lb = 150f, a = 0.08f + 0.14f * warn;
                Quad(bc + o * 3f, bc - o * 3f, bc + d * Lb - o * 30f, bc + d * Lb + o * 30f, AlarmRed.WithAlpha(a));
                Quad(bc + o * 1f, bc - o * 1f, bc + d * Lb - o * 13f, bc + d * Lb + o * 13f, AlarmRed.WithAlpha(a));
            }
        }

        /// C: a blast door, two halves striped with hazard chevrons that part sideways; a strip of five red lamps along the
        /// lintel chasing in toward the middle, faster as the Rioter nears.
        void DoorBlast_C(Enemy e, V2 pos, V2 f, V2 s, float half, float swing, bool jammed, float t)
        {
            float warn = DoorWarn(e), w = half + 11f;
            FrontFrame(pos, f, s, half, swing, out V2 d0, out V2 d1);
            float L = (d1 - d0).Length, gap = swing * half;
            for (int side = -1; side <= 1; side += 2)
            {
                float wid = half - gap;
                if (wid < 0.5f)
                    continue;
                V2 inA = d0 + s * (side * gap), outA = d0 + s * (side * half);  // (the half's inner and outer edges at the lintel)
                Quad(inA, outA, outA + f * L, inA + f * L, CellSteel.Darkened(0.05f));
                for (int k = 0; k < 4; k++)  // chevrons pointing in at the seam
                {
                    V2 c = inA + f * (L * (0.15f + 0.22f * k));
                    V2 tip = c + s * (side * 3f), back = c + s * (side * Mathf.Min(wid - 2f, 18f));
                    Quad(tip, tip + f * 5f, back + f * 5f + f * 4f, back + f * 4f, SignYellow.Darkened(0.12f));
                }
                Cap(inA, inA + f * L, 1.6f, GuardInk);  // the seam
                Cap(outA - s * (side * 2f), outA - s * (side * 2f) + f * L, 1.2f, CellSteelLit);
                Circle(inA + s * (side * 6f) + f * (L * 0.5f), 2f, CellSteelLit, 8);  // the bolt
            }
            for (int k = 0; k < 5; k++)
            {
                float u = -1f + 2f * k / 4f;
                V2 at = pos - f * 8f + s * (u * (w - 8f));
                float on;
                if (e.Phase != 1)
                    on = jammed && k == 0 && Mathf.Sin(t * 1.3f) > 0.97f ? 0.35f : 0f;
                else if (e.PhaseT > 0f)
                    on = 1f;
                else
                {
                    float rate = Mathf.Lerp(1.2f, 7f, warn), dd = Mathf.Abs(u);
                    float ph = MathX.Fmod(t * rate - (1f - dd) * 0.5f, 1f);
                    on = ph < 0.3f ? Mathf.Lerp(0.5f, 1f, warn) : 0.06f;
                }
                AlarmLamp(at, 3.6f, on);
            }
        }

        // --- Traps, refreshed -----------------------------------------------------------------------------------------

        /// The mine: a squat olive disc with a raised pressure plate and a red LED ring that ticks round when armed; its
        /// blast circle drawn faint and dashed under it, so you see the zone before you step in.
        void DrawFloorMineMock(Enemy e, V2 pos, float r, float t)
        {
            float arm = Mathf.Clamp01(e.T2 / Game.FloorMineArm);
            bool armed = e.Phase >= 1, beep = e.Phase == 2, blink = Mathf.Sin(t * 60f) > 0f;
            if (armed && !beep)
                for (int k = 0; k < 16; k++)  // the trigger zone, dashed
                    Arc(pos, Game.FloorMineTrigger, 1.2f, AlarmRed.WithAlpha(0.28f), 4, k * MathX.Tau / 16f + t * 0.3f, MathX.Tau / 32f);
            Circle(pos + new V2(1.5f, 2f), r + 1.5f, new Rgba(0f, 0f, 0f, 0.35f), 20);  // its shadow
            Circle(pos, r + OffInk, GuardInk, 20);
            Circle(pos, r, new Rgba(0.33f, 0.36f, 0.27f), 20);
            Arc(pos, r * 0.8f, 1.2f, new Rgba(0.22f, 0.24f, 0.18f), 20);
            Circle(pos, r * 0.5f, new Rgba(0.42f, 0.45f, 0.36f), 14);  // the plate
            Circle(pos - new V2(r * 0.15f, r * 0.15f), r * 0.25f, new Rgba(0.55f, 0.58f, 0.48f), 10);
            for (int k = 0; k < 8; k++)  // its LED ring
            {
                float ang = k * MathX.Tau / 8f;
                bool lit = armed && (beep ? blink : (int)MathX.Fmod(t * 6f, 8f) == k);
                var c = lit ? (beep ? Rgba.White : AlarmRed) : armed ? new Rgba(0.45f, 0.08f, 0.06f) : new Rgba(0.2f, 0.1f, 0.08f);
                Circle(pos + V2.FromAngle(ang) * (r * 0.74f), 1.1f, c, 6);
                if (lit)
                    Circle(pos + V2.FromAngle(ang) * (r * 0.74f), 3.2f, AlarmRed.WithAlpha(0.3f), 8);
            }
            if (!armed)
                Arc(pos, r + 3f, 1.6f, AlarmRed.WithAlpha(0.6f), 20, -MathX.Pi / 2f, MathX.Tau * arm);  // arming
            if (beep)
            {
                Circle(pos, Game.FloorMineBlast, AlarmRed.WithAlpha(blink ? 0.16f : 0.06f), 40);
                Arc(pos, Game.FloorMineBlast, 2f, AlarmRed.WithAlpha(0.8f), 40);
            }
        }

        /// The shackle: a bear trap seen from above, two serrated jaws open on a round frame round a red plate, staked by
        /// a chain; snapped, the jaws meet with a white crack.
        void DrawShackleMock(Enemy e, V2 pos, float r, float t)
        {
            bool armed = e.Phase >= 1, shut = e.Phase == 2;
            float arm = Mathf.Clamp01(e.T2 / Game.SnareArm);
            V2 stake = pos + new V2(r * 1.6f, r * 1.1f);
            for (int k = 1; k <= 4; k++)  // the chain: alternating links
            {
                V2 c = pos + (stake - pos) * (k / 5f);
                float ang = (stake - pos).Angle + (k % 2 == 0 ? MathX.Pi / 2f : 0f);
                Arc(c, 2f, 1f, TrapSteel.WithAlpha(0.85f), 8);
                Cap(c - V2.FromAngle(ang) * 1.5f, c + V2.FromAngle(ang) * 1.5f, 1f, TrapSteel.Darkened(0.3f));
            }
            Circle(stake, 3.2f + OffInk, GuardInk, 10);
            Circle(stake, 3.2f, new Rgba(0.4f, 0.34f, 0.26f), 10);
            Circle(stake, 1.3f, TrapSteel, 6);
            Circle(pos + new V2(1.5f, 2f), r + 2f, new Rgba(0f, 0f, 0f, 0.35f), 20);
            var steel = armed ? TrapSteel : TrapSteel.Darkened(0.4f);
            Arc(pos, r * 0.95f, 2.2f + OffInk, GuardInk, 24);
            Arc(pos, r * 0.95f, 2.2f, steel.Darkened(0.25f), 24);  // the frame
            float open = shut ? 0.12f : 1f;
            for (int k = -1; k <= 1; k += 2)
            {
                // each jaw: an arc from hinge to hinge (left and right), laid open outward or closed to the middle
                V2 c = pos + new V2(0f, k * r * 0.45f * open);
                float start = k < 0 ? MathX.Pi : 0f;
                Arc(c, r * 0.85f, 2.6f + OffInk, GuardInk, 16, start, MathX.Pi);
                Arc(c, r * 0.85f, 2.6f, steel, 16, start, MathX.Pi);
                for (int j = 1; j <= 5; j++)  // serrated teeth
                {
                    V2 d = V2.FromAngle(start + MathX.Pi * j / 6f);
                    Tri3(c + d * (r * 0.85f) + d.Orthogonal() * 1.6f, c + d * (r * 0.85f) - d.Orthogonal() * 1.6f, c + d * (r * 0.5f), steel);
                }
            }
            Circle(pos - new V2(r, 0f), 2f, steel.Darkened(0.3f), 8);  // the hinges
            Circle(pos + new V2(r, 0f), 2f, steel.Darkened(0.3f), 8);
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 4f + e.Id);
            Circle(pos, 3.4f, armed ? AlarmRed : new Rgba(0.35f, 0.2f, 0.2f), 10);  // the plate you step on
            if (armed && !shut)
                Circle(pos, 6f, AlarmRed.WithAlpha(0.2f * pulse), 12);
            if (!armed)
                Arc(pos, r + 4f, 1.6f, TrapSteel.WithAlpha(0.6f), 20, -MathX.Pi / 2f, MathX.Tau * arm);
            if (shut)
                Arc(pos, r * 1.5f, 2f, Rgba.White.WithAlpha(Mathf.Clamp01(e.PhaseT / Game.SnareLinger)), 20);
        }

        /// While the Turnkey builds a door: a blueprint of the frame on the wall, filling in, with sparks off the wall.
        void DrawDoorBlueprint(V2 at, float facing, float work, float t)
        {
            V2.Axes(facing, out V2 f, out V2 s);
            float half = 30f, w = half + 12f;
            var line = new Rgba(1f, 0.82f, 0.36f, 0.6f + 0.35f * work);
            V2 b = at - f * 13f, n = at + f * 46f;
            V2[] c = { b - s * w, b + s * w, n + s * w, n - s * w };
            float total = work * 4f;
            for (int k = 0; k < 4; k++)
            {
                float u = Mathf.Clamp01(total - k);
                if (u <= 0f)
                    break;
                V2 p = c[k], q = c[(k + 1) % 4];
                for (int j = 0; j < 8; j++)  // dashed
                {
                    float a0 = j / 8f, a1 = a0 + 0.08f;
                    if (a0 > u)
                        break;
                    Cap(p + (q - p) * a0, p + (q - p) * Mathf.Min(a1, u), 2f, line);
                }
            }
            Quad(b - s * w, b + s * w, b + s * w + (n - b) * work, b - s * w + (n - b) * work, WallConcrete.WithAlpha(0.6f));  // the concrete going up
            for (int k = 0; k < 5; k++)  // sparks off the hammer
            {
                float u = MathX.Fmod(t * 3f + k * 0.21f, 1f);
                V2 d = V2.FromAngle(facing + (Hash(k * 13 + (int)(t * 3f)) - 0.5f) * 2.2f);
                V2 p = at + f * 48f + d * (4f + 22f * u);
                Cap(p, p + d * 4f, 1.2f, KeyBrass.WithAlpha(1f - u));
            }
        }
    }
}

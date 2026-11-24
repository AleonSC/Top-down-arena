using UnityEngine;

namespace Arena
{
    /// The Turnkey's Cell Door (Core/Turnkey.cs), built into the arena's outer wall and facing in (Facing): a barred cell
    /// front seen like a door in an old top-down dungeon, a prisoner in orange pacing behind the bars and gripping them as
    /// its Rioter is due (T2 counts down to it), the gate sliding into the wall as one comes out (PhaseT); a red beacon on
    /// the lintel spins faster as one nears, its beams sweeping the floor with a red wash and chevrons where it'll come
    /// out. Jammed (its Turnkey dead): dark, chained and padlocked. Being built: a gold outline of it filling in on the
    /// wall, sparks, and a dotted line to the Turnkey hammering at it. The floor wash goes under bodies (DrawCellDoorFloor,
    /// from the body pass); the door itself after the walls (DrawCellDoorsOver, WorldRenderer.cs), so it cuts into them.
    public partial class ArenaApp
    {

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

        /// The middle of the wall a door stands in (its body is DoorIn in from the wall's face).
        static V2 DoorWall(Enemy e, V2 pos, V2 f) => pos - f * (Game.DoorIn + Rooms.WallT * 0.5f);

        /// In the body pass (under the bodies): the red wash and the beacon's beams on the floor in front.
        void DrawCellDoorFloor(Enemy e, V2 pos, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            V2 w = DoorWall(e, pos, f);
            DoorSpill(e, w, f, s, DoorHalf, t);
            BeaconSweep(e, w, f, s, t);
        }

        const float DoorHalf = 34f;

        /// After the walls: every Cell Door, and the line from a Turnkey to the door it's building.
        void DrawCellDoorsOver(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type != EnemyType.CellDoor || e.Hp <= 0f)
                    continue;
                V2 pos = e.Disp;
                V2.Axes(e.Facing, out V2 f, out V2 s);
                V2 w = DoorWall(e, pos, f);
                if (e.Phase == 3)
                {
                    float work = 1f - Mathf.Clamp01(e.PhaseT / Game.TurnkeyBuild);
                    DrawDoorBlueprint(w, e.Facing, work, t);
                    Enemy tk;
                    if (game.Enemies.TryGetValue(e.ParentId, out tk))  // a dotted line back to the Turnkey at work
                        for (int k = 1; k < 14; k++)
                        {
                            float u = k / 14f, a = 0.5f * Mathf.Sin(u * MathX.Pi);
                            Circle(tk.Disp + (pos + f * 30f - tk.Disp) * u, 1.6f, KeyBrass.WithAlpha(a * (0.6f + 0.4f * Mathf.Sin(t * 8f - k))), 6);
                        }
                    if (e.Flash > 0f || JuiceFlash(e))
                        Circle(pos, e.Radius, Rgba.White.WithAlpha(0.5f * HitFlashMix(e)), 14);
                    continue;
                }
                bool jammed = e.Phase == 2;
                float swing = jammed ? 0f : Mathf.Sin(Mathf.Clamp01(e.PhaseT / Game.DoorSwing) * MathX.Pi);
                DrawCellGate(e, w, f, s, DoorHalf, swing, jammed, t);
                if (jammed)
                {
                    V2 c = w + f * 18f;
                    for (int k = 0; k <= 8; k++)  // a chain across the doorway
                    {
                        float u = -1f + 2f * k / 8f;
                        Circle(c + s * (u * (DoorHalf + 4f)) + f * (3f * (1f - u * u)), 2.3f, new Rgba(0.75f, 0.77f, 0.82f), 10);
                    }
                    V2 lk = c + f * 6f;
                    Arc(lk - f * 3f, 3.2f, 1.4f, new Rgba(0.75f, 0.77f, 0.82f), 12, e.Facing + MathX.Pi / 2f, MathX.Pi);
                    Quad(lk - s * 4.5f - f * 1f, lk + s * 4.5f - f * 1f, lk + s * 4.5f + f * 6f, lk - s * 4.5f + f * 6f, GuardInk);
                    Quad(lk - s * 3.5f, lk + s * 3.5f, lk + s * 3.5f + f * 5f, lk - s * 3.5f + f * 5f, new Rgba(0.88f, 0.26f, 0.2f));
                }
                if (e.Flash > 0f || JuiceFlash(e))
                {
                    V2 d0 = w - f * 4f, d1 = w + f * 46f;
                    Quad(d0 - s * DoorHalf, d0 + s * DoorHalf, d1 + s * DoorHalf, d1 - s * DoorHalf, Rgba.White.WithAlpha(0.6f * HitFlashMix(e)));
                }
            }
        }

        /// The frame: a shadow on the floor, concrete posts and a hazard-striped
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

        /// The barred cell front: a prisoner in orange pacing behind the bars (gripping them as it's due), the gate sliding
        /// sideways into the wall to open; a red beacon on the lintel spinning, its beam sweeping the floor.
        void DrawCellGate(Enemy e, V2 pos, V2 f, V2 s, float half, float swing, bool jammed, float t)
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
            if (e.Phase != 1)
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

        /// While the Turnkey builds a door: a blueprint of the frame on the wall, filling in, with sparks off the wall.
        void DrawDoorBlueprint(V2 at, float facing, float work, float t)  // (at: the wall's middle)
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

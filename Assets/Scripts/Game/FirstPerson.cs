using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// First-person view (F8, remembered): purely local drawing over the same game, network and rules, like the zoom.
    ///   eye      the camera sits in your body FpEye arena units up, turned by the mouse (fpYaw, fpPitch, at Settings > Gameplay >
    ///            First-person mouse: SetFpSens; the cursor is locked
    ///            while nothing wants the pointer). W walks where you look (FpMove) and you aim at the crosshair: MouseArena is
    ///            the floor point under it (FpAimPoint), or far ahead when you look up.
    ///   floor    Px (WorldRenderer.cs) sends every arena point through FpPx: the whole top-down picture becomes the floor.
    ///   stand    FpStand round a body (an enemy, minion, player or shot) stands its top-down drawing up as a card facing the
    ///            camera (its far side up; past its body, a trail or a lane lies flat at its height), lifted off the floor with a
///            shadow under it, fading out right in front of you; FpFloor lays the drawing flat again.
    ///            Your own body isn't drawn: you look out of it.
    ///   walls    the floor plan's walls and pillars (and a walled arena's edges) stand FpWallH high (FpWalls).
    ///   order    Vtx (Juice.cs) collects the triangles instead of drawing them; FpEndWorld draws the floor first, then the
    ///            cards and wall pieces far to near (painter's order), each cut at the near plane, the glow pass's triangles
    ///            with the additive material. A held frame (hit-pause) isn't replayed in this view.
    ///   HUD      ToUi (CameraLean.cs) projects too, so damage numbers and labels follow; the aim line and the weapon pips
    ///            round you are left out; the crosshair stays in the middle of the screen.
    public partial class ArenaApp
    {
        const float FpEye = 26f, FpFov = 74f, FpNear = 3f, FpWallH = 72f, FpSeg = 48f;
        const float FpMinAim = 70f, FpMaxAim = 900f, FpShotLift = 18f, FpSens = 0.035f;
        static readonly Rgba FpFog = new Rgba(0.03f, 0.035f, 0.055f, 1f);

        bool fpWant, fpLoaded;  // F8 toggled it (PlayerPrefs "first_person")
        float fpSensV = 0.5f;   // Settings > Gameplay > First-person mouse, 0.05..1 (PlayerPrefs "fp_sens"): 0.5 is the default speed, each end 4x slower / faster
        bool fpView;            // this frame: on, in a run, with you on the field
        bool fpProject;         // while the arena is drawn through the eye (FpBeginWorld .. FpEndWorld)
        bool fpCamReady;        // the camera below was set by a frame drawn in this view
        float fpYaw, fpPitch = -0.12f;

        // The camera of the frame being drawn (arena x, y; z up).
        Vector3 fpCam, fpFwd, fpRight, fpUp;
        float fpCx, fpCy, fpFocal;

        // What Px is drawing now: 0 the floor, 1 a standing card round fpAnchor, 2 your own body (left out), 3 a prop's top
        // (flat at fpLift, moved down by fpTopShift: the drop-in's lift).
        int fpMode;
        V2 fpAnchor;
        float fpLift, fpReach2, fpBend;  // fpBend: how far from its middle a card stands; past that it lies flat at its height
        V2 fpTopShift;
        bool fpBad;  // a corner of the triangle being drawn lies far from its card (a body's copy across a wrap): dropped

        // The collected frame: three corners (camera space) and colours per triangle, and runs of triangles by group.
        struct FpRun { public int Group, Seq, From, To; public float Depth; public bool Card; }
        readonly List<Vector3> fpV = new List<Vector3>(4096);
        readonly List<Color> fpC = new List<Color>(4096);
        readonly List<bool> fpGlowOf = new List<bool>(2048);
        readonly List<FpRun> fpRuns = new List<FpRun>(512);
        int fpGroup;
        float fpDepth;
        bool fpGlowNow;
        float fpFade = 1f;  // the run being drawn: its opacity
        readonly Vector3[] fpPolyV = new Vector3[5], fpClipV = new Vector3[5];
        readonly Color[] fpPolyC = new Color[5], fpClipC = new Color[5];

        /// The First-person mouse setting (0.05..1), loaded the first time it's asked for.
        float FpSensSetting
        {
            get
            {
                FpLoad();
                return fpSensV;
            }
        }

        void SetFpSens(float v)
        {
            FpLoad();
            fpSensV = Mathf.Clamp(v, 0.05f, 1f);
            PlayerPrefs.SetFloat("fp_sens", fpSensV);
            PlayerPrefs.Save();
        }

        /// The drawer's compact First-person mouse bar (MenuDrawer.cs): − and + keys, twenty cells, the speed as a multiplier.
        float FpSensSlider(Rect r, float v)
        {
            float cy = r.center.y;
            var minus = new Rect(r.x, cy - 13f, 26f, 26f);
            var plus = new Rect(r.xMax - 26f, cy - 13f, 26f, 26f);
            var val = new Rect(plus.x - 50f, r.y, 46f, r.height);
            var cells = new Rect(minus.xMax + 8f, cy - 9f, val.x - 6f - (minus.xMax + 8f), 18f);
            if (DkKey(minus, "-", false, FxCalm, size: 18, accent: null))
                v = Mathf.Round(v * 20f - 1f) / 20f;
            if (DkKey(plus, "+", false, FxCalm, size: 18, accent: null))
                v = Mathf.Round(v * 20f + 1f) / 20f;
            var e = Event.current;
            var hit = new Rect(cells.x - 2f, cells.y - 8f, cells.width + 4f, cells.height + 16f);
            Ui.NoteHover(hit);
            if (GUI.enabled && e.button == 0 && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && hit.Contains(e.mousePosition))
            {
                v = Mathf.Round(Mathf.Clamp01((e.mousePosition.x - cells.x) / cells.width) * 20f) / 20f;
                e.Use();
            }
            v = Mathf.Clamp(v, 0.05f, 1f);
            int lit = Mathf.RoundToInt(v * 20f);
            float cw = cells.width / 20f;
            for (int i = 0; i < 20; i++)
                Ui.Fill(new Rect(cells.x + i * cw, cells.y, cw - 2f, cells.height), i < lit ? CrtStyle.Ink : CrtStyle.Ink.WithAlpha(0.14f));
            float mult = Mathf.Pow(4f, v * 2f - 1f);
            Ui.Label(val, mult.ToString(mult < 0.995f ? "0.00" : "0.0", System.Globalization.CultureInfo.InvariantCulture) + "x", 16, CrtStyle.Ink, TextAnchor.MiddleRight, true);
            return v;
        }

        void FpLoad()
        {
            if (fpLoaded)
                return;
            fpLoaded = true;
            fpWant = PlayerPrefs.GetInt("first_person", 0) == 1;
            fpSensV = Mathf.Clamp(PlayerPrefs.GetFloat("fp_sens", 0.5f), 0.05f, 1f);
        }

        void SetFirstPerson(bool on)
        {
            fpWant = on;
            PlayerPrefs.SetInt("first_person", on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// Every frame (end of Update): F8, the mouse look, the cursor lock.
        void UpdateFirstPerson()
        {
            FpLoad();
            if (Input.GetKeyDown(KeyCode.F8) && !Keybinds.Busy && GUIUtility.keyboardControl == 0)
                SetFirstPerson(!fpWant);
            var me = game.Me;
            bool was = fpView;
            fpView = fpWant && session.Screen == AppScreen.Game && game.Running && me != null;
            if (fpView && !was)
            {
                fpYaw = me.Aim;  // start looking where you were aiming
                fpPitch = -0.12f;
            }
            if (!fpView)
                fpCamReady = false;
            bool look = fpView && !cursorWantPointer && !game.Paused && !chatOpen;
            Cursor.lockState = look ? CursorLockMode.Locked : CursorLockMode.None;
            if (!look)
                return;
            float mx = 0f, my = 0f, k = FpSens * Mathf.Pow(4f, fpSensV * 2f - 1f);  // (the setting: 4x slower to 4x faster)
            try
            {
                mx = Input.GetAxisRaw("Mouse X");
                my = Input.GetAxisRaw("Mouse Y");
            }
            catch (ArgumentException) { }  // (no mouse axes in the Input Manager: no turning)
            fpYaw = MathX.Fmod(fpYaw + mx * k + MathX.Pi, MathX.Tau) - MathX.Pi;
            fpPitch = Mathf.Clamp(fpPitch + my * k, -1.25f, 0.6f);
        }

        /// The move keys (screen directions, W up) as an arena direction: turned with the camera, or with your look.
        V2 FpMove(V2 keys)
        {
            if (!fpView)
                return UnrotateView(keys);
            V2 fwd = V2.FromAngle(fpYaw), right = V2.FromAngle(fpYaw + MathX.Pi / 2f);
            return fwd * -keys.Y + right * keys.X;
        }

        /// Where the crosshair points: the floor under it, held between FpMinAim and FpMaxAim ahead of you.
        V2 FpAimPoint()
        {
            var me = game.Me;
            if (me == null)
                return new V2(AW / 2f, AH / 2f);
            float sp = Mathf.Sin(fpPitch), cp = Mathf.Cos(fpPitch);
            float dist = sp < -0.01f ? FpEye / -sp * cp : FpMaxAim;
            return me.Pos + V2.FromAngle(fpYaw) * Mathf.Clamp(dist, FpMinAim, FpMaxAim);
        }

        /// The arena point the flat drawing puts in the middle of the screen (where the crosshair is drawn).
        V2 FpScreenMid() => ScreenToArena(new V2(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f));

        /// ToUi in this view: an arena point a little off the floor, on the HUD canvas (far off the screen when behind you).
        V2 FpToUi(V2 p)
        {
            var c = FpToCam(p.X, p.Y, 20f);
            if (c.z < FpNear)
                return new V2(-9999f, -9999f);
            float sx = fpCx + c.x / c.z * fpFocal, sy = fpCy + c.y / c.z * fpFocal;
            return new V2((sx - offX) / scale, (UnityEngine.Screen.height - sy - offY) / scale);
        }

        // --- The frame ---------------------------------------------------------------------------------------------------

        /// DrawWorld, before the arena: from here Px draws through your eyes.
        void FpBeginWorld()
        {
            fpProject = false;
            var me = game.Me;
            if (!fpView || me == null)
                return;
            fpProject = true;
            fpMode = 0;
            fpGroup = 0;
            fpDepth = float.MaxValue;
            fpGlowNow = false;
            fpBad = false;
            recording = false;  // (no hit-pause replays: the collected frame isn't drawn through Vtx)
            recVerts.Clear(); recCols.Clear(); recGlowFrom.Clear(); recGlowTo.Clear();
            float cy = Mathf.Cos(fpYaw), sy = Mathf.Sin(fpYaw), cp = Mathf.Cos(fpPitch), sp = Mathf.Sin(fpPitch);
            fpCam = new Vector3(me.Disp.X, me.Disp.Y, FpEye);
            fpFwd = new Vector3(cy * cp, sy * cp, sp);
            fpRight = new Vector3(-sy, cy, 0f);  // (arena y runs down: a quarter turn clockwise is your right)
            fpUp = new Vector3(-cy * sp, -sy * sp, cp);
            fpCx = UnityEngine.Screen.width / 2f;
            fpCy = UnityEngine.Screen.height / 2f;
            fpFocal = fpCy / Mathf.Tan(FpFov * 0.5f * Mathf.Deg2Rad);
            fpCamReady = true;
        }

        /// DrawWorld, after the arena: raise the walls, then draw everything collected, far to near.
        void FpEndWorld()
        {
            if (!fpProject)
                return;
            fpMode = 0;
            FpWalls();
            fpProject = false;
            FpFlush();
        }

        /// Stand what's drawn next up round `at` (a body of radius r), its centre `lift` off the floor (default: on the floor).
        void FpStand(V2 at, float r, float lift = -1f)
        {
            if (!fpProject)
                return;
            fpMode = 0;
            Circle(at, r * 0.9f, new Rgba(0f, 0f, 0f, 0.32f), 12);  // its shadow
            fpMode = 1;
            fpAnchor = at;
            fpLift = lift >= 0f ? lift : r + 2f;
            fpBend = r * 1.25f + 2f;
            float reach = Mathf.Max(150f, r * 5f);
            fpReach2 = reach * reach;
            FpGroupAt((at.X - fpCam.x) * fpFwd.x + (at.Y - fpCam.y) * fpFwd.y);
        }

        /// A shot stands at chest height.
        void FpStandShot(Bullet b) => FpStand(b.Pos, Mathf.Max(3f, b.Radius), FpShotLift);

        /// A player stands up; your own body isn't drawn (you look out of it).
        void FpStandPlayer(Player p)
        {
            if (!fpProject)
                return;
            if (p.Id != game.MyId)
            {
                FpStand(p.Disp, p.Def.BodyR);
                return;
            }
            fpMode = 2;
            FpGroupAt(float.MaxValue);
        }

        /// Your own body's floor drawing (auras, rings) in this view: left out, it would fill the screen from your feet.
        bool FpHidesMe(Player p) => fpProject && p.Id == game.MyId;

        /// A crate, barrel or chest (PropsLook.cs, drawn at `at`, lifted while it drops in): its sides raised as a solid box
        /// or drum, then its top-down drawing laid on its top.
        void FpProp(Prop p, V2 at)
        {
            if (!fpProject)
                return;
            float hw = Game.HalfW(p), hh = Game.HalfH(p), lift = p.Pos.Y - at.Y;
            float h = p.Kind == PropKind.Chest ? 16f : p.Kind == PropKind.Crate ? 20f : 22f;
            var ink = p.Kind == PropKind.Crate ? new Rgba(1f, 0.82f, 0.3f) : p.Kind == PropKind.HeavyCrate ? new Rgba(1f, 0.55f, 0.2f)
                : p.Kind == PropKind.Barrel ? new Rgba(1f, 0.35f, 0.25f) : p.Kind == PropKind.HealthBarrel ? new Rgba(0.4f, 1f, 0.5f) : new Rgba(1f, 0.85f, 0.35f);
            FpGroupAt((p.Pos.X - fpCam.x) * fpFwd.x + (p.Pos.Y - fpCam.y) * fpFwd.y);  // one group: a prop is small
            if (Game.Round(p))
            {
                const int n = 14;
                for (int i = 0; i < n; i++)
                {
                    V2 d0 = V2.FromAngle(MathX.Tau * i / n), d1 = V2.FromAngle(MathX.Tau * (i + 1) / n);
                    FpPropSide(p.Pos + d0 * hw, p.Pos + d1 * hw, V2.FromAngle(MathX.Tau * (i + 0.5f) / n), lift, h, ink, i % 2 == 0);
                }
            }
            else
            {
                float x0 = p.Pos.X - hw, y0 = p.Pos.Y - hh, x1 = p.Pos.X + hw, y1 = p.Pos.Y + hh;
                FpPropSide(new V2(x0, y0), new V2(x1, y0), new V2(0f, -1f), lift, h, ink, true);
                FpPropSide(new V2(x1, y1), new V2(x0, y1), new V2(0f, 1f), lift, h, ink, true);
                FpPropSide(new V2(x0, y1), new V2(x0, y0), new V2(-1f, 0f), lift, h, ink, true);
                FpPropSide(new V2(x1, y0), new V2(x1, y1), new V2(1f, 0f), lift, h, ink, true);
            }
            fpMode = 3;
            fpLift = h + lift;
            fpTopShift = new V2(0f, lift);
            FillRect(at.X - hw, at.Y - hh, hw * 2f, hh * 2f, new Rgba(0.06f, 0.07f, 0.1f, 0.92f));  // the lid under the drawing
        }

        /// One side of a prop, `lift` off the floor: a dark face with a lit rim top and bottom (`edges`: corner lines too).
        void FpPropSide(V2 a, V2 b, V2 outward, float lift, float h, Rgba ink, bool edges)
        {
            if ((fpCam.x - a.X) * outward.X + (fpCam.y - a.Y) * outward.Y <= 0f)
                return;  // its back
            float shade = 0.65f + 0.35f * Mathf.Abs(outward.X);
            var fill = new Rgba(0.08f, 0.08f, 0.1f, 1f).Mix(ink, 0.18f);
            var face = new Color(fill.R * shade, fill.G * shade, fill.B * shade, 1f);
            var rim = new Color(ink.R, ink.G, ink.B, 1f);
            FpWallQuad(a, b, lift, lift + h, face);
            FpWallQuad(a, b, lift + h - 2f, lift + h, rim);
            FpWallQuad(a, b, lift, lift + 1.5f, new Color(rim.r, rim.g, rim.b, 0.6f));
            if (edges)
                FpWallQuad(a, a + (b - a).Normalized() * 1.5f, lift, lift + h, new Color(rim.r, rim.g, rim.b, 0.7f));
        }

        /// Back to drawing on the floor.
        void FpFloor()
        {
            if (!fpProject)
                return;
            fpMode = 0;
            FpGroupAt(float.MaxValue);
        }

        void FpGroupAt(float depth)
        {
            fpGroup++;
            fpDepth = depth;
        }

        Vector3 FpToCam(float x, float y, float z)
        {
            float dx = x - fpCam.x, dy = y - fpCam.y, dz = z - fpCam.z;
            return new Vector3(dx * fpRight.x + dy * fpRight.y, dx * fpUp.x + dy * fpUp.y + dz * fpUp.z, dx * fpFwd.x + dy * fpFwd.y + dz * fpFwd.z);
        }

        /// Px while fpProject: an arena point in camera space (x right, y up, z ahead).
        Vector3 FpPx(V2 p)
        {
            if (fpMode == 0)
                return FpToCam(p.X, p.Y, 0f);
            if (fpMode == 3)
                return FpToCam(p.X + fpTopShift.X, p.Y + fpTopShift.Y, fpLift);
            float dx = p.X - fpAnchor.X, dy = p.Y - fpAnchor.Y;
            if (fpMode == 2 || dx * dx + dy * dy > fpReach2)
                fpBad = true;  // (your own body, or a corner far from its card: the triangle is dropped)
            // The card faces the camera: across stays across, the far side goes up; past fpBend (a shot's trail, an aim
            // lane) the rest lies flat at the card's height, so it still points the way it really does.
            float hx = fpFwd.x, hy = fpFwd.y, hl = Mathf.Sqrt(hx * hx + hy * hy);
            if (hl > 1e-4f) { hx /= hl; hy /= hl; }
            float side = dx * fpRight.x + dy * fpRight.y, along = dx * hx + dy * hy;
            float up = Mathf.Clamp(along, -fpBend, fpBend), flat = along - up;
            return FpToCam(fpAnchor.X + fpRight.x * side + hx * flat, fpAnchor.Y + fpRight.y * side + hy * flat, fpLift + up);
        }

        /// Vtx while fpProject: keep the corner; a finished triangle joins the current group's run.
        void FpVtx(Vector3 v)
        {
            fpV.Add(v);
            fpC.Add(glCur);
            if (fpV.Count % 3 != 0)
                return;
            if (fpBad)
            {
                fpV.RemoveRange(fpV.Count - 3, 3);
                fpC.RemoveRange(fpC.Count - 3, 3);
                fpBad = false;
                return;
            }
            int tri = fpV.Count / 3 - 1;
            fpGlowOf.Add(fpGlowNow);
            int n = fpRuns.Count;
            if (n > 0 && fpRuns[n - 1].Group == fpGroup && fpRuns[n - 1].To == tri)
            {
                var run = fpRuns[n - 1];
                run.To = tri + 1;
                fpRuns[n - 1] = run;
            }
            else
                fpRuns.Add(new FpRun { Group = fpGroup, Seq = n, From = tri, To = tri + 1, Depth = fpDepth, Card = fpMode == 1 });
        }

        // --- Walls -------------------------------------------------------------------------------------------------------

        /// The floor plan's walls and pillars, and a walled arena's edges, raised as solid pieces.
        void FpWalls()
        {
            var d = game.ActiveLayout;
            if (d != null)
                foreach (var s in d.Solids)
                {
                    if (s.Edge && game.Walled)
                        continue;  // (the walled arena's own edges are below)
                    if (s.Round)
                        FpPillar(s.Pos, s.R);
                    else
                        FpBox(s.Pos.X - s.HW, s.Pos.Y - s.HH, s.Pos.X + s.HW, s.Pos.Y + s.HH);
                }
            if (game.Walled && !game.RoomsOn)
            {
                V2 lo = game.ArenaMin, hi = game.ArenaMax;
                const float T = 40f;
                FpBox(lo.X - T, lo.Y - T, hi.X + T, lo.Y);
                FpBox(lo.X - T, hi.Y, hi.X + T, hi.Y + T);
                FpBox(lo.X - T, lo.Y, lo.X, hi.Y);
                FpBox(hi.X, lo.Y, hi.X + T, hi.Y);
            }
        }

        void FpBox(float x0, float y0, float x1, float y1)
        {
            FpFace(new V2(x0, y0), new V2(x1, y0), new V2(0f, -1f));
            FpFace(new V2(x1, y1), new V2(x0, y1), new V2(0f, 1f));
            FpFace(new V2(x0, y1), new V2(x0, y0), new V2(-1f, 0f));
            FpFace(new V2(x1, y0), new V2(x1, y1), new V2(1f, 0f));
        }

        void FpPillar(V2 c, float r)
        {
            int n = Mathf.Clamp(Mathf.CeilToInt(MathX.Tau * r / FpSeg), 10, 32);
            for (int i = 0; i < n; i++)
            {
                V2 a = c + V2.FromAngle(MathX.Tau * i / n) * r, b = c + V2.FromAngle(MathX.Tau * (i + 1) / n) * r;
                FpFace(a, b, V2.FromAngle(MathX.Tau * (i + 0.5f) / n));
            }
        }

        /// One side of a wall, from a to b, facing `outward`: cut into pieces so each sorts on its own against the cards.
        void FpFace(V2 a, V2 b, V2 outward)
        {
            if ((fpCam.x - a.X) * outward.X + (fpCam.y - a.Y) * outward.Y <= 0f)
                return;  // its back
            float len = (b - a).Length;
            int n = Mathf.Max(1, Mathf.CeilToInt(len / FpSeg));
            float shade = 0.7f + 0.3f * Mathf.Abs(outward.X);
            var fill = new Rgba(0.07f, 0.08f, 0.12f, 1f).Mix(WallFill, 0.22f);
            var face = new Color(fill.R * shade, fill.G * shade, fill.B * shade, 1f);
            var ink = new Color(Ink.R, Ink.G, Ink.B, 1f);
            var core = new Color(NeonCore.R, NeonCore.G, NeonCore.B, 1f);
            var seam = new Color(Ink.R, Ink.G, Ink.B, 0.22f);
            for (int i = 0; i < n; i++)
            {
                V2 p = V2.Lerp(a, b, (float)i / n), q = V2.Lerp(a, b, (float)(i + 1) / n), m = (p + q) * 0.5f;
                FpGroupAt((m.X - fpCam.x) * fpFwd.x + (m.Y - fpCam.y) * fpFwd.y);
                FpWallQuad(p, q, 0f, FpWallH, face);
                FpWallQuad(p, q, FpWallH - 4f, FpWallH, ink);
                FpWallQuad(p, q, FpWallH - 2.5f, FpWallH - 1.5f, core);
                FpWallQuad(p, q, 0f, 2.5f, new Color(ink.r, ink.g, ink.b, 0.7f));
                FpWallQuad(p, p + (q - p).Normalized() * 1.5f, 0f, FpWallH, seam);
            }
            FpGroupAt(float.MaxValue);
        }

        void FpWallQuad(V2 p, V2 q, float z0, float z1, Color c)
        {
            glCur = c;
            Vector3 a = FpToCam(p.X, p.Y, z0), b = FpToCam(q.X, q.Y, z0), d = FpToCam(q.X, q.Y, z1), e = FpToCam(p.X, p.Y, z1);
            FpVtx(a); FpVtx(b); FpVtx(d);
            FpVtx(a); FpVtx(d); FpVtx(e);
        }

        // --- Drawing the collected frame ---------------------------------------------------------------------------------

        void FpFlush()
        {
            fpRuns.Sort((x, y) => x.Depth != y.Depth ? y.Depth.CompareTo(x.Depth) : x.Seq.CompareTo(y.Seq));
            bool glow = false;
            foreach (var run in fpRuns)
            {
                fpFade = run.Card ? Mathf.Clamp01((run.Depth - 12f) / 40f) : 1f;  // a card right in your face (your own shot leaving) fades out
                if (fpFade <= 0f)
                    continue;
                for (int i = run.From; i < run.To; i++)
                {
                    if (fpGlowOf[i] != glow)
                    {
                        glow = fpGlowOf[i];
                        GL.End();
                        (glow ? GlowMaterial() : glMaterial)?.SetPass(0);
                        GL.Begin(GL.TRIANGLES);
                    }
                    FpEmit(i * 3, glow);
                }
            }
            if (glow)
            {
                GL.End();
                glMaterial?.SetPass(0);
                GL.Begin(GL.TRIANGLES);
            }
            fpV.Clear();
            fpC.Clear();
            fpGlowOf.Clear();
            fpRuns.Clear();
        }

        /// One collected triangle: cut at the near plane, then onto the screen as a fan.
        void FpEmit(int k, bool glow)
        {
            Vector3 a = fpV[k], b = fpV[k + 1], c = fpV[k + 2];
            if (a.z < FpNear && b.z < FpNear && c.z < FpNear)
                return;
            int n = 3;
            fpPolyV[0] = a; fpPolyV[1] = b; fpPolyV[2] = c;
            fpPolyC[0] = fpC[k]; fpPolyC[1] = fpC[k + 1]; fpPolyC[2] = fpC[k + 2];
            if (a.z < FpNear || b.z < FpNear || c.z < FpNear)
            {
                int m = 0;
                for (int i = 0; i < n; i++)
                {
                    int j = (i + 1) % n;
                    Vector3 p = fpPolyV[i], q = fpPolyV[j];
                    bool pin = p.z >= FpNear, qin = q.z >= FpNear;
                    if (pin)
                    {
                        fpClipV[m] = p; fpClipC[m] = fpPolyC[i]; m++;
                    }
                    if (pin != qin)
                    {
                        float f = (FpNear - p.z) / (q.z - p.z);
                        fpClipV[m] = new Vector3(p.x + (q.x - p.x) * f, p.y + (q.y - p.y) * f, p.z + (q.z - p.z) * f);
                        fpClipC[m] = Color.Lerp(fpPolyC[i], fpPolyC[j], f);
                        m++;
                    }
                }
                n = m;
                for (int i = 0; i < n; i++)
                {
                    fpPolyV[i] = fpClipV[i];
                    fpPolyC[i] = fpClipC[i];
                }
            }
            if (n < 3)
                return;
            // Off one side of the screen: skip it.
            float w = UnityEngine.Screen.width, h = UnityEngine.Screen.height;
            bool left = true, right = true, below = true, above = true;
            for (int i = 0; i < n; i++)
            {
                var v = fpPolyV[i];
                float sx = fpCx + v.x / v.z * fpFocal, sy = fpCy + v.y / v.z * fpFocal;
                fpPolyV[i] = new Vector3(sx, sy, v.z);
                left &= sx < 0f; right &= sx > w; below &= sy < 0f; above &= sy > h;
            }
            if (left || right || below || above)
                return;
            for (int i = 1; i + 1 < n; i++)
            {
                FpOut(0, glow);
                FpOut(i, glow);
                FpOut(i + 1, glow);
            }
        }

        /// A projected corner, faded into the dark with distance.
        void FpOut(int i, bool glow)
        {
            var v = fpPolyV[i];
            var c = fpPolyC[i];
            c.a *= fpFade;
            float f = Mathf.Clamp01((v.z - 600f) / 1600f) * 0.65f;
            if (f > 0f)
            {
                if (glow)
                    c.a *= 1f - f;
                else
                    c = new Color(c.r + (FpFog.R - c.r) * f, c.g + (FpFog.G - c.g) * f, c.b + (FpFog.B - c.b) * f, c.a);
            }
            GL.Color(c);
            GL.Vertex(new Vector3(v.x, v.y, 0f));
        }
    }
}

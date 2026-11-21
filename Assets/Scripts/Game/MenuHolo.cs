using UnityEngine;

namespace Arena
{
    /// The main menu's "holo floor" look (Docs/Mockups/menu-holo-floor.html): neon tube edges on the title and the menu box, the drive bay
    /// ledge hanging under the title (it houses the disk drive and the eject key, which has a green lamp), flat 2D rooms that draw
    /// themselves round the title and the box now and then, and a honeycomb band along the bottom of the screen with a notched rail
    /// carrying data packets. The floor itself is the Holo Floor menu background (BgHoloFloor, below).
    public partial class ArenaApp
    {
        // --- Neon ---------------------------------------------------------------------------------------------------------

        static readonly Rgba HoloHalo = new Rgba(0.43f, 0.6f, 1f), HoloCore = new Rgba(0.66f, 0.8f, 1f), HoloHot = new Rgba(0.9f, 0.95f, 1f);

        /// A neon tube's brightness at time t: 1, with two quick dips near the end of each `period` seconds (a tube flickering).
        float NeonFlicker(float t, float period, float offset)
        {
            if (fxReduced || !fxOn)
                return 1f;
            float u = MathX.Fmod(t + offset, period) / period;
            return (u >= 0.91f && u < 0.92f) || (u >= 0.935f && u < 0.945f) ? 0.45f : 1f;
        }

        /// GL: a neon tube along a path (closed or open): a wide soft halo, a bright core and a white-hot centre line.
        /// Each piece runs half its width past both ends, so the corners meet without notches.
        void NeonPath(V2[] pts, bool closed, float a)
        {
            if (a <= 0f)
                return;
            int n = closed ? pts.Length : pts.Length - 1;
            for (int layer = 0; layer < 5; layer++)
            {
                float w = layer == 0 ? 16f : layer == 1 ? 9f : layer == 2 ? 5f : layer == 3 ? 2.2f : 1f;
                var c = layer == 0 ? HoloHalo.WithAlpha(0.06f * a) : layer == 1 ? HoloHalo.WithAlpha(0.12f * a) : layer == 2 ? HoloHalo.WithAlpha(0.25f * a)
                    : layer == 3 ? HoloCore.WithAlpha(0.95f * a) : HoloHot.WithAlpha(0.9f * a);
                for (int i = 0; i < n; i++)
                {
                    V2 p = pts[i], q = pts[(i + 1) % pts.Length], d = (q - p).Normalized() * (layer >= 3 ? w / 2f : 0f);
                    Line(p - d, q + d, w, c);
                }
            }
        }

        void NeonRect(Rect r, float a) =>
            NeonPath(new[] { new V2(r.x, r.y), new V2(r.xMax, r.y), new V2(r.xMax, r.yMax), new V2(r.x, r.yMax) }, true, a);

        /// The title screen's outer edge (its bezel), following it as it drops in during the intro (CrtBox).
        static Rect TitleNeonRect { get { var b = CrtBox; return new Rect(b.x - 8f, b.y - 8f, b.width + 16f, b.height + 16f); } }

        /// IMGUI: the neon round the title, drawn over its bezel (called by DrawMenuTitleText).
        void DrawTitleNeon(float a)
        {
            if (a > 0f && BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                NeonRect(TitleNeonRect, a * NeonFlicker(Time.unscaledTime, 9f, 3f));
                EndUiGl();
            }
        }

        /// GL: the neon round the menu box.
        void DrawBoxNeon(Rect r, float a)
        {
            UiModelAt(V2.Zero, 1f);
            NeonRect(r, a * NeonFlicker(Time.unscaledTime, 7f, 1.4f));
        }

        // --- The drive bay ledge ------------------------------------------------------------------------------------------

        /// The ledge under the title: it runs out to halfway between the title's ends and the box's sides, from the title's
        /// bottom edge to just under the drive's readout, with its bottom corners cut.
        static Rect DriveBayRect => new Rect((112f + MenuCard.x) / 2f, 186f, W - (112f + MenuCard.x), 64f);
        const float BayCut = 10f;

        /// IMGUI: the ledge (drawn after the title, before the drive and its eject key). Its body covers the title's neon where they
        /// join, so the two read as one piece.
        void DrawDriveBay(float a)
        {
            if (a <= 0f || !BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            var r = DriveBayRect;
            float x0 = r.x, x1 = r.xMax, y0 = r.y - 3f, y1 = r.yMax, c = BayCut;
            // the body: dark at the top (under the title) to a deep blue, with the bottom corners cut
            var top = new Rgba(0.04f, 0.05f, 0.09f, a);
            var bot = new Rgba(0.07f, 0.1f, 0.19f, a);
            GradQuad(new V2(x0, y0), new V2(x1, y0), new V2(x1, y1 - c), new V2(x0, y1 - c), top, top.Mix(bot, 0.85f));
            SetColor(top.Mix(bot, 0.85f));
            Quad(new V2(x0, y1 - c), new V2(x1, y1 - c), new V2(x1 - c, y1), new V2(x0 + c, y1));
            // vents either side of the drive
            var vent = new Rgba(0.23f, 0.31f, 0.53f, 0.55f * a);
            float vy = r.y + 22f;
            for (int side = 0; side < 2; side++)
            {
                float vx0 = side == 0 ? x0 + 26f : W / 2f + DriveW / 2f + 70f, vx1 = side == 0 ? W / 2f - DriveW / 2f - 26f : x1 - 26f;
                for (float x = vx0; x + 6f <= vx1; x += 10f)
                    FillRect(x, vy, 6f, 18f, vent);
            }
            // the neon down the sides and along the bottom (the top joins the title, so it has none)
            float n = a * NeonFlicker(Time.unscaledTime, 9f, 3f);
            NeonPath(new[] { new V2(x0, r.y), new V2(x0, y1 - c), new V2(x0 + c, y1), new V2(x1 - c, y1), new V2(x1, y1 - c), new V2(x1, r.y) }, false, n);
            EndUiGl();
        }

        /// IMGUI: the eject key's lamp, a small green light that breathes (dim while there's nothing to eject).
        void DrawEjectLamp(float a)
        {
            if (a <= 0f)
                return;
            var r = EjectKeyRect;
            bool on = EjectAvailable && !profTr && !profEj;
            float t = Time.unscaledTime, pulse = fxReduced ? 1f : 0.72f + 0.28f * Mathf.Sin(t * 2.6f);
            var green = new Color(0.49f, 1f, 0.69f);
            var c = new Vector2(r.xMax - 8f, r.y + 8f);
            WithAlpha(a, () =>
            {
                if (on)
                {
                    Ui.Dot(new Rect(c.x - 9f, c.y - 9f, 18f, 18f), green.WithAlpha(0.18f * pulse));
                    Ui.Dot(new Rect(c.x - 5.5f, c.y - 5.5f, 11f, 11f), green.WithAlpha(0.35f * pulse));
                }
                Ui.Dot(new Rect(c.x - 3f, c.y - 3f, 6f, 6f), on ? green.WithAlpha(0.6f + 0.4f * pulse) : new Color(0.2f, 0.32f, 0.26f));
            });
        }

        // --- Rooms ----------------------------------------------------------------------------------------------------------

        struct MenuRoom
        {
            public Rect r;
            public char door;   // the wall facing the menu ('t', 'r', 'b', 'l'), drawn with a doorway gap
            public float delay; // seconds after the set starts
        }

        static MenuRoom Rm(float x, float y, float w, float h, char door) => new MenuRoom { r = new Rect(x, y, w, h), door = door };

        // Rooms round the title (it spans 112-1168 by 66-186) and round the menu box (300-980 by 268-588, its corner brackets 7 px out).
        static readonly MenuRoom[][] TitleRoomSets =
        {
            new[] { Rm(34, 80, 74, 92, 'r'), Rm(1172, 80, 74, 92, 'l') },
            new[] { Rm(330, 14, 190, 44, 'b'), Rm(545, 14, 190, 44, 'b'), Rm(760, 14, 190, 44, 'b') },
            new[] { Rm(34, 74, 74, 52, 'r'), Rm(34, 130, 74, 50, 'r'), Rm(1172, 74, 74, 52, 'l'), Rm(1172, 130, 74, 50, 'l') },
            new[] { Rm(34, 80, 74, 92, 'r'), Rm(860, 14, 200, 44, 'b') },
        };
        static readonly MenuRoom[][] BoxRoomSets =
        {
            new[] { Rm(214, 372, 72, 96, 'r'), Rm(994, 372, 72, 96, 'l') },
            new[] { Rm(214, 282, 72, 92, 'r'), Rm(214, 380, 72, 92, 'r'), Rm(994, 282, 72, 92, 'l'), Rm(994, 380, 72, 92, 'l') },
            new[] { Rm(214, 300, 72, 150, 'r'), Rm(994, 420, 72, 150, 'l') },
            new[] { Rm(214, 470, 72, 110, 'r'), Rm(994, 470, 72, 110, 'l'), Rm(994, 350, 72, 112, 'l') },
        };

        const float RoomEvery = 9f, RoomBuild = 1.3f, RoomHold = 3.4f, RoomFold = 0.9f, RoomBoxAfter = 0.5f, RoomStagger = 0.15f;

        /// GL: every RoomEvery seconds a set of rooms draws itself round the title, then round the box: each outline traced by a
        /// bright pen point, then a faint grid and a scan line fill it; it holds, then un-draws. Off with Reduced effects.
        void DrawMenuRooms(float t, float a)
        {
            if (a <= 0f || fxReduced || !fxOn)
                return;
            int cycle = Mathf.FloorToInt(t / RoomEvery);
            float age = t - cycle * RoomEvery;
            var titleSet = TitleRoomSets[(int)(Hash(cycle * 7 + 1) * TitleRoomSets.Length) % TitleRoomSets.Length];
            var boxSet = BoxRoomSets[(int)(Hash(cycle * 7 + 4) * BoxRoomSets.Length) % BoxRoomSets.Length];
            for (int i = 0; i < titleSet.Length; i++)
                DrawRoom(titleSet[i], age - i * RoomStagger, t, a);
            for (int i = 0; i < boxSet.Length; i++)
                DrawRoom(boxSet[i], age - RoomBoxAfter - i * RoomStagger, t, a);
        }

        static readonly V2[] roomPts = new V2[16];

        void DrawRoom(MenuRoom m, float age, float t, float a)
        {
            if (age <= 0f)
                return;
            float p = age < RoomBuild ? age / RoomBuild : age < RoomBuild + RoomHold ? 1f : 1f - (age - RoomBuild - RoomHold) / RoomFold;
            if (p <= 0f)
                return;
            var r = m.r;
            // The outline as one pen path, clockwise from the top-left, with a doorway in the wall facing the menu; big rooms get a divider.
            int n = 0;
            V2[] c = { new V2(r.x, r.y), new V2(r.xMax, r.y), new V2(r.xMax, r.yMax), new V2(r.x, r.yMax) };
            const string walls = "trbl";
            for (int i = 0; i < 4; i++)
            {
                V2 s = c[i], e = c[(i + 1) % 4];
                if (walls[i] == m.door)
                {
                    roomPts[n++] = s; roomPts[n++] = V2.Lerp(s, e, 0.32f);
                    roomPts[n++] = V2.Lerp(s, e, 0.68f); roomPts[n++] = e;
                }
                else
                {
                    roomPts[n++] = s; roomPts[n++] = e;
                }
            }
            if (r.height > 100f) { roomPts[n++] = new V2(r.x, r.y + r.height / 2f); roomPts[n++] = new V2(r.x + r.width * 0.55f, r.y + r.height / 2f); }
            if (r.width > 150f) { roomPts[n++] = new V2(r.x + r.width / 2f, r.y); roomPts[n++] = new V2(r.x + r.width / 2f, r.y + r.height * 0.6f); }
            float total = 0f;
            for (int i = 0; i < n; i += 2)
                total += (roomPts[i + 1] - roomPts[i]).Length;
            // the fill, once the outline is done
            float fill = Mathf.Clamp01((p - 0.85f) / 0.15f) * a;
            if (fill > 0f)
            {
                FillRect(r.x, r.y, r.width, r.height, new Rgba(0.16f, 0.27f, 0.59f, 0.16f * fill));
                var grid = new Rgba(0.43f, 0.59f, 0.94f, 0.1f * fill);
                for (float gx = r.x + 12f; gx < r.xMax; gx += 12f)
                    FillRect(gx, r.y, 1f, r.height, grid);
                for (float gy = r.y + 12f; gy < r.yMax; gy += 12f)
                    FillRect(r.x, gy, r.width, 1f, grid);
                FillRect(r.x, r.y + MathX.Fmod(t * 28f, r.height), r.width, 2f, new Rgba(0.59f, 0.75f, 1f, 0.14f * fill));
            }
            // the outline, as far as the pen has got
            float left = total * Mathf.Clamp01(p);
            V2 tip = default;
            bool drawing = false;
            for (int i = 0; i < n && left > 0f; i += 2)
            {
                V2 s = roomPts[i], e = roomPts[i + 1];
                float l = (e - s).Length, k = Mathf.Min(1f, left / l);
                V2 to = V2.Lerp(s, e, k);
                Line(s, to, 6f, new Rgba(0.36f, 0.52f, 0.91f, 0.15f * a));
                Line(s, to, 2f, new Rgba(0.59f, 0.73f, 1f, 0.9f * a));
                drawing = k < 1f;
                tip = to;
                left -= l;
            }
            if (drawing)
            {
                Circle(tip, 7f, new Rgba(0.62f, 0.75f, 1f, 0.3f * a), 12);
                FillRect(tip.X - 2.5f, tip.Y - 2.5f, 5f, 5f, new Rgba(1f, 1f, 1f, a));
            }
            if (p >= 1f)
                foreach (var q in c)
                    FillRect(q.X - 2f, q.Y - 2f, 4f, 4f, new Rgba(0.82f, 0.88f, 1f, 0.9f * a));
        }

        // --- The bottom band ------------------------------------------------------------------------------------------------

        const float HexR = 13f, BandRail = H - 74f;
        const int HexRows = 5;

        /// GL: along the bottom of the screen, a honeycomb band (fading out upward) lit by two waves crossing it and the odd cell
        /// flashing, blue on the left to violet on the right; over it a notched rail with tick marks, and packets of light sliding along it.
        void DrawMenuHexBand(float t, float a)
        {
            if (a <= 0f)
                return;
            UiModelAt(V2.Zero, 1f);
            bool still = fxReduced || !fxOn;
            if (still)
                t = 20f;
            GradQuad(new V2(0f, BandRail - 10f), new V2(W, BandRail - 10f), new V2(W, H), new V2(0f, H), new Rgba(0.16f, 0.2f, 0.55f, 0f), new Rgba(0.16f, 0.2f, 0.55f, 0.45f * a));
            float hw = 1.7320508f * HexR;
            var blue = new Rgba(0.36f, 0.52f, 0.91f);
            var violet = new Rgba(0.64f, 0.5f, 1f);
            int cell = 0;
            for (int row = 0; row < HexRows; row++)
            {
                float fade = 1f - row / (float)HexRows, cy = H + 6f - row * HexR * 1.5f;
                for (float x = -hw; x < W + hw; x += hw, cell++)
                {
                    float cx = x + (row % 2 == 1 ? hw / 2f : 0f);
                    float wave = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(cx * 0.012f - t * 2.2f + row * 0.6f)), 6f)
                        + 0.7f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(-cx * 0.008f - t * 1.3f + 3f)), 8f);
                    float slot = t * 0.6f + Hash(cell) * 50f;
                    int s = Mathf.FloorToInt(slot);
                    float flash = Hash(cell * 7 + s * 131) < 0.05f ? 1f - (slot - s) : 0f;
                    float b = Mathf.Min(1f, (wave + flash) * fade);
                    var col = blue.Mix(violet, Mathf.Clamp01(cx / W));
                    var at = new V2(cx, cy);
                    Arc(at, HexR - 1.5f, 1f, col.WithAlpha((0.1f * fade + 0.6f * b) * a), 6, MathX.Pi / 6f);
                    if (b > 0.05f)
                        PointyHexFill(at, HexR - 4.5f, col.WithAlpha(0.45f * b * a));
                }
            }
            // the rail: a line with a raised notch every 160 px, ticks under it
            var rail = new Rgba(0.59f, 0.73f, 1f, 0.75f * a);
            V2 prev = new V2(0f, BandRail);
            for (float x = 80f; x < W; x += 160f)
            {
                V2 a0 = new V2(x, BandRail), a1 = new V2(x + 10f, BandRail - 8f), a2 = new V2(x + 50f, BandRail - 8f), a3 = new V2(x + 60f, BandRail);
                RailLine(prev, a0, rail); RailLine(a0, a1, rail); RailLine(a1, a2, rail); RailLine(a2, a3, rail);
                prev = a3;
            }
            RailLine(prev, new V2(W, BandRail), rail);
            for (float x = 0f; x < W; x += 16f)
            {
                bool major = MathX.Fmod(x, 80f) < 1f;
                FillRect(x, BandRail + 4f, major ? 2f : 1f, major ? 6f : 3f, new Rgba(0.59f, 0.73f, 1f, 0.35f * a));
            }
            // packets of light sliding along it, each with a short tail
            for (int k = 0; k < 10; k++)
            {
                float v = 120f + Hash(k * 5 + 1) * 220f, period = (W + 60f) / v + 0.5f + Hash(k * 5 + 2) * 2.5f;
                float x = MathX.Fmod(t + Hash(k * 5 + 3) * period, period) * v - 30f;
                if (x > W + 20f)
                    continue;
                bool gold = Hash(k * 5 + 4) < 0.2f, big = Hash(k * 5 + 9) < 0.25f;
                var pc = gold ? new Rgba(0.95f, 0.76f, 0.31f) : new Rgba(0.78f, 0.88f, 1f);
                float y = RailY(x), sz = big ? 6f : 4f;
                FillRect(x - 24f - sz / 2f, y - 1f, 24f, 2f, pc.WithAlpha(0.25f * a));
                Circle(new V2(x, y), 6f, pc.WithAlpha(0.25f * a), 10);
                FillRect(x - sz / 2f, y - sz / 2f, sz, sz, pc.WithAlpha(0.95f * a));
            }
        }

        void RailLine(V2 p, V2 q, Rgba c)
        {
            Line(p, q, 6f, c.WithAlpha(c.A * 0.2f));
            Line(p, q, 1.5f, c);
        }

        /// The rail's height at x (up on the notches).
        static float RailY(float x)
        {
            float ph = MathX.Fmod(x - 80f + 1600f, 160f);
            float up = ph < 10f ? ph / 10f : ph < 50f ? 1f : ph < 60f ? (60f - ph) / 10f : 0f;
            return BandRail - 8f * up;
        }

        /// A filled pointy-topped hexagon.
        void PointyHexFill(V2 at, float r, Rgba c)
        {
            SetColor(c);
            for (int i = 0; i < 6; i++)
                Tri(at, at + V2.FromAngle(MathX.Pi / 6f + MathX.Tau * i / 6f) * r, at + V2.FromAngle(MathX.Pi / 6f + MathX.Tau * (i + 1) / 6f) * r);
        }

        // --- The Holo Floor background ------------------------------------------------------------------------------------

        const float HoloHorizon = 372f;

        /// A starry sky over a glowing horizon and a perspective grid floor scrolling toward you, with light running down its lanes
        /// (now and then a gold one) and motes drifting up.
        void BgHoloFloor(float t)
        {
            float hz = HoloHorizon;
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, hz), new V2(0, hz), new Rgba(0.016f, 0.02f, 0.043f), new Rgba(0.047f, 0.067f, 0.19f));
            Stars(110, t, 0.55f, 4100);
            GradQuad(new V2(0, hz), new V2(W, hz), new V2(W, H), new V2(0, H), new Rgba(0.09f, 0.075f, 0.25f), new Rgba(0.016f, 0.02f, 0.043f));
            SoftGlow(new V2(W / 2f, hz), 560f, new Rgba(0.37f, 0.53f, 1f, 0.3f + 0.08f * Mathf.Sin(t * 0.9f)));
            var grid = new Rgba(0.36f, 0.52f, 0.91f, 0.5f);
            for (int i = -24; i <= 24; i++)  // lanes to the vanishing point
                Line(new V2(W / 2f + i * 8f, hz), new V2(W / 2f + i * 150f, H), 1.2f, grid);
            float off = MathX.Fmod(t * 0.75f, 1f);
            for (int k = 0; k < 16; k++)  // cross lines coming toward you
            {
                float z = k + 1 - off, y = hz + (H - hz) * 0.9f / z;
                if (y > hz + 1f && y < H)
                    Line(new V2(0, y), new V2(W, y), 1.2f, grid);
            }
            for (int s = 0; s < 9; s++)  // light running down a lane, a new lane each pass
            {
                float run = t * 0.55f + Hash(s * 3 + 1) * 9f;
                int pass = Mathf.FloorToInt(run);
                float z = (run - pass) * 1.05f;
                int lane = Mathf.FloorToInt(Hash(s * 31 + pass * 17) * 36f) - 18;
                var pc = Hash(s * 13 + pass * 7) < 0.2f ? new Rgba(0.95f, 0.76f, 0.31f) : new Rgba(0.59f, 0.75f, 1f);
                for (int j = 0; j < 6; j++)
                {
                    float z0 = Mathf.Max(0f, z - 0.12f * (6 - j) / 6f), z1 = Mathf.Max(0f, z - 0.12f * (5 - j) / 6f);
                    if (z1 <= 0f)
                        continue;
                    float e0 = Mathf.Pow(z0, 2.2f), e1 = Mathf.Pow(z1, 2.2f);
                    V2 p0 = LaneAt(lane, e0, hz), p1 = LaneAt(lane, e1, hz);
                    Line(p0, p1, 1f + e1 * 3f, pc.WithAlpha(0.95f * (j + 1) / 6f));
                }
            }
            FillRect(0, hz - 1f, W, 2f, new Rgba(0.55f, 0.69f, 1f, 0.75f));
            GradQuad(new V2(0, hz), new V2(W, hz), new V2(W, hz + 60f), new V2(0, hz + 60f), new Rgba(0.09f, 0.075f, 0.25f, 0.8f), new Rgba(0.09f, 0.075f, 0.25f, 0f));
            for (int i = 0; i < 40; i++)  // motes drifting up off the floor
            {
                float sp = 0.3f + Hash(i * 5 + 2), span = H - hz + 80f;
                float y = H - MathX.Fmod(t * 12f * sp + Hash(i * 5 + 3) * span, span);
                float x = Hash(i * 5 + 4) * W + Mathf.Sin(t + i) * 4f;
                FillRect(x, y, 2f, 2f, new Rgba(0.59f, 0.73f, 1f, 0.25f + 0.25f * Mathf.Sin(t * 2f + i)));
            }
        }

        static V2 LaneAt(int lane, float e, float hz) =>
            new V2(Mathf.Lerp(W / 2f + lane * 8f, W / 2f + lane * 150f, e), hz + (H - hz) * e);
    }
}

using UnityEngine;

namespace Arena
{
    /// The main menu's "holo floor" look (Docs/Mockups/menu-holo-floor.html): neon tube edges on the title and the menu box, the drive bay
    /// ledge hanging under the title (it houses the disk drive and the eject key, which has a green lamp), flat 2D rooms that draw
    /// themselves round the title and the box now and then. The floor itself is the Holo Floor menu background (BgHoloFloor, below).
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
                NeonSparkSound(9f, 3f, a);
                HoloCorners(TitleNeonRect, a);
                EndUiGl();
            }
        }

        /// GL: glowing corner brackets just outside a panel (the settings windows' look, Docs/Mockups/settings-holo.html): a soft halo
        /// under a bright 3 px stroke, 22 px along each side, 9 px out from the panel's neon edge.
        void HoloCorners(Rect r, float a, float gap = 9f, float len = 22f)
        {
            if (a <= 0f)
                return;
            UiModelAt(V2.Zero, 1f);
            float x0 = r.x - gap, x1 = r.xMax + gap, y0 = r.y - gap, y1 = r.yMax + gap;
            foreach (var corner in new[] { new V2(x0, y0), new V2(x1, y0), new V2(x0, y1), new V2(x1, y1) })
            {
                float sx = corner.X < r.center.x ? 1f : -1f, sy = corner.Y < r.center.y ? 1f : -1f;
                V2 h = corner + new V2(sx * len, 0f), v = corner + new V2(0f, sy * len), back = new V2(-sx * 1.5f, 0f), up = new V2(0f, -sy * 1.5f);
                Line(corner + back, h, 9f, HoloHalo.WithAlpha(0.14f * a));
                Line(corner + up, v, 9f, HoloHalo.WithAlpha(0.14f * a));
                Line(corner + back, h, 3f, HoloHot.WithAlpha(0.95f * a));
                Line(corner + up, v, 3f, HoloHot.WithAlpha(0.95f * a));
            }
        }

        /// GL: the neon round the menu box.
        void DrawBoxNeon(Rect r, float a)
        {
            UiModelAt(V2.Zero, 1f);
            NeonRect(r, a * NeonFlicker(Time.unscaledTime, 7f, 1.4f));
        }

        // --- The Who's playing? screen ----------------------------------------------------------------------------------------

        const string WhoTitleText = "WHO'S PLAYING?";

        // --- Sounds (MenuSoundBank.cs) -----------------------------------------------------------------------------------------

        int roomsHeard = -1, neonHeard = -1;

        /// A menu key's own sound, in place of the generic click (Ui.TakeClick takes this event's click so it doesn't play too).
        void MenuSound(Sfx s, float vol = SoundBank.MenuSoundVolume)
        {
            if (sounds != null)
                sounds.Play(s, vol, Time.unscaledTime);
            Ui.TakeClick();
        }

        /// The split-flap clacks of the title screen's line changing as a move's clock goes from a to b: typing `text` on over
        /// [t0, t1] (a clack per letter, the space softer, Enter's thock on the last), or erasing it (softer clacks), letter by letter
        /// at the moments DrawTitleRetype shows them.
        void TitleFlapCues(string text, float a, float b, float t0, float t1, bool typing)
        {
            if (sounds == null || b <= a)
                return;
            int n = text.Length;
            float now = Time.unscaledTime;
            for (int i = 0; i < n; i++)
            {
                float at = typing ? t0 + (t1 - t0) * (i + 1f) / n : t0 + (t1 - t0) * (i + 0.5f) / n;
                if (a >= at || at > b)
                    continue;
                if (!typing)
                    sounds.Play(Sfx.TitleKey, 0.35f, now);
                else
                    sounds.Play(i == n - 1 ? Sfx.TitleEnter : text[i] == ' ' ? Sfx.TitleSpace : Sfx.TitleKey, 0.6f, now);
            }
        }

        float quitAt = -1f;

        /// Quit: the tube switching off, then the game closes once it has played (TickMenuSounds).
        void QuitWithSound()
        {
            MenuSound(Sfx.MenuQuit, 0.8f);
            if (quitAt < 0f)
                quitAt = Time.unscaledTime + 0.45f;
        }

        /// The neon's two tiny sparks as it flickers (the same clock as NeonFlicker), once per flicker.
        void NeonSparkSound(float period, float offset, float a)
        {
            if (sounds == null || a <= 0f || fxReduced || !fxOn || Event.current.type != EventType.Repaint)
                return;
            float t = Time.unscaledTime + offset;
            int cycle = Mathf.FloorToInt(t / period);
            if (MathX.Fmod(t, period) / period >= 0.91f && cycle != neonHeard)
            {
                neonHeard = cycle;
                sounds.Play(Sfx.NeonSpark, 0.4f * a, Time.unscaledTime);
            }
        }

        string runHoverId;
        int runHoverFrame = -10;

        /// A saved run's row on the Play screen: the menu keys' phosphor tick as the mouse comes onto it.
        void RunRowHover(Rect r, string id)
        {
            if (Event.current.type != EventType.Repaint)
                return;
            if (runHoverFrame < Time.frameCount - 1)
                runHoverId = null;  // (no row was under the mouse last frame)
            if (!r.Contains(Event.current.mousePosition) || !GUI.enabled)
                return;
            if (runHoverId != id && sounds != null)
                sounds.Play(Sfx.MenuHover, 0.5f, Time.unscaledTime);
            runHoverId = id;
            runHoverFrame = Time.frameCount;
        }

        // The menu's ambient hum: a low drone (55 Hz with a fifth and an octave over it) breathing slowly, a seamless 4 s loop, very quiet
        // (on the Music volume), fading in on the main menu and the Who's playing? screen and out everywhere else.
        AudioSource humSource;
        float humLevel;
        const float HumVolume = 0.05f;

        AudioClip MakeHumClip()
        {
            const int rate = 22050;
            const float len = 4f;  // every partial and the breath fit a whole number of cycles in it, so the loop has no seam
            int n = (int)(rate * len);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / rate, breath = 0.75f + 0.25f * Mathf.Sin(2f * Mathf.PI * 0.25f * t);
                float x = 0.5f * Mathf.Sin(2f * Mathf.PI * 55f * t) + 0.18f * Mathf.Sin(2f * Mathf.PI * 110f * t) + 0.08f * Mathf.Sin(2f * Mathf.PI * 165f * t)
                    + 0.22f * breath * Mathf.Sin(2f * Mathf.PI * 82.5f * t) + 0.12f * breath * Mathf.Sin(2f * Mathf.PI * 110.25f * t);
                data[i] = x * 0.6f;
            }
            var clip = AudioClip.Create("menu_hum", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// Update: the hum's fade, and a pending quit once its sound has played.
        void TickMenuSounds(float dt)
        {
            if (quitAt >= 0f && Time.unscaledTime >= quitAt)
            {
                quitAt = -1f;
                Application.Quit();
            }
            if (sounds == null)
                return;
            bool want = session.Screen == AppScreen.Menu;
            humLevel = Mathf.MoveTowards(humLevel, want ? 1f : 0f, dt / 1.2f);
            if (humLevel <= 0f)
            {
                if (humSource != null && humSource.isPlaying)
                    humSource.Stop();
                return;
            }
            if (humSource == null)
            {
                humSource = gameObject.AddComponent<AudioSource>();
                humSource.playOnAwake = false;
                humSource.loop = true;
                humSource.spatialBlend = 0f;
                humSource.priority = 64;
                humSource.clip = MakeHumClip();
            }
            humSource.volume = HumVolume * humLevel * sounds.Master * sounds.Music;
            if (!humSource.isPlaying)
                humSource.Play();
        }

        // --- Settings ---------------------------------------------------------------------------------------------------------

        bool cabHolo;  // drawing Settings from the main menu or the lobby: its cabinet and window get the holo look
        bool cabNeon;  // drawing the Profile from the main menu or the lobby: the same neon edges and corners (look only)
        bool CabNeon => cabHolo || cabNeon;

        /// IMGUI: a settings panel's neon edge and glowing corners, coming on as its soft sweep finishes (e: seconds into it).
        void DrawPanelNeon(Rect r, float e, float period, float offset)
        {
            float a = Mathf.Clamp01(e / HoloBoot.SoftLen) * Ui.Alpha;
            if (a <= 0f || !BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            NeonRect(r, a * NeonFlicker(Time.unscaledTime, period, offset));
            HoloCorners(r, a);
            EndUiGl();
        }

        /// IMGUI: the link from the open section's row (y on screen) across to its window: a glowing line in the section's colour
        /// with three packets of light running along it.
        void DrawSettingsLink(float y, Color col)
        {
            if (!cabWinOn || cabWinOutAt >= 0f || !BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            float x0 = cabBootRect.xMax, x1 = cabWinRect.x, a = Mathf.Clamp01(cabWinBootE / HoloBoot.SoftLen) * Ui.Alpha;
            if (x1 > x0 + 4f && a > 0f)
            {
                var c = new Rgba(col.r, col.g, col.b);
                Line(new V2(x0, y), new V2(x1, y), 8f, c.WithAlpha(0.18f * a));
                Line(new V2(x0, y), new V2(x1, y), 2f, c.Mix(Rgba.White, 0.4f).WithAlpha(0.95f * a));
                if (!fxReduced && fxOn)
                    for (int i = 0; i < 3; i++)
                    {
                        float u = MathX.Fmod(Time.unscaledTime / 1.1f + i / 3f, 1f), x = Mathf.Lerp(x0 - 3f, x1 - 3f, u);
                        FillRect(x, y - 3f, 6f, 6f, new Rgba(1f, 1f, 1f, a));
                    }
            }
            EndUiGl();
        }

        // --- The Play key's chevrons ------------------------------------------------------------------------------------------

        const float PlayChevW = 46f;

        /// The main menu's Play key (also its split halves in the bay-doors move, MenuServo.cs): three chevrons before PLAY.
        bool PlayKey(Rect r) => KeyUi.Key(r, "PLAY", null, 34, drawIcon: DrawPlayChevrons, iconW: PlayChevW);

        /// Three chevrons in r, lighting one after another (a 1.2 s wave, each 0.15 s behind the one before); steady with Reduced effects.
        void DrawPlayChevrons(Rect r, float a)
        {
            const float cw = 12f, gap = 5f, thick = 5f;
            float h = Mathf.Min(26f, r.height), y0 = r.center.y - h / 2f, t = Time.unscaledTime;
            for (int i = 0; i < 3; i++)
            {
                float glow = 1f;
                if (!fxReduced && fxOn)
                {
                    float u = MathX.Fmod(t - i * 0.15f, 1.2f) / 1.2f;
                    glow = u < 0.4f ? Mathf.Lerp(0.25f, 1f, u / 0.4f) : Mathf.Lerp(1f, 0.25f, (u - 0.4f) / 0.6f);
                }
                float x0 = r.x + i * (cw + gap);
                var c = new Color(1f, 1f, 1f, a * glow);
                var shade = new Color(0f, 0f, 0f, 0.4f * a * glow);
                for (int y = 0; y < (int)h; y++)
                {
                    float d = Mathf.Abs(y + 0.5f - h / 2f) / (h / 2f), x = x0 + (1f - d) * (cw - thick);
                    Ui.Fill(new Rect(x, y0 + y + 1f, thick, 1f), shade);  // engraved, like the legend
                    Ui.Fill(new Rect(x, y0 + y, thick, 1f), c);
                }
            }
        }

        // --- The drive bay ledge ------------------------------------------------------------------------------------------

        const float BayCut = 10f;

        // --- Rooms ----------------------------------------------------------------------------------------------------------

        struct MenuRoom
        {
            public Rect r;
            public char door;   // the wall facing the menu ('t', 'r', 'b', 'l'), drawn with a doorway gap
        }

        static MenuRoom Rm(float x, float y, float w, float h, char door) => new MenuRoom { r = new Rect(x, y, w, h), door = door };

        // Rooms round the menu box (300-980 by 268-588, its corner brackets 9 px out).
        static readonly MenuRoom[][] BoxRoomSets =
        {
            new[] { Rm(214, 372, 72, 96, 'r'), Rm(994, 372, 72, 96, 'l') },
            new[] { Rm(214, 282, 72, 92, 'r'), Rm(214, 380, 72, 92, 'r'), Rm(994, 282, 72, 92, 'l'), Rm(994, 380, 72, 92, 'l') },
            new[] { Rm(214, 300, 72, 150, 'r'), Rm(994, 420, 72, 150, 'l') },
            new[] { Rm(214, 470, 72, 110, 'r'), Rm(994, 470, 72, 110, 'l'), Rm(994, 350, 72, 112, 'l') },
        };


        // The rooms come one at a time: the first RoomDelay seconds after the screen shows (or the menu's intro ends), then one every
        // RoomGap seconds. Each draws in over RoomBuild; once RoomMax are up, each new one starts as the oldest un-draws (RoomFold).
        const float RoomDelay = 4f, RoomGap = 3.5f, RoomBuild = 1.3f, RoomFold = 0.9f;
        const int RoomMax = 3;

        float roomsStart;
        int roomsFrame = -10;
        MenuRoom[][] roomsLower;
        readonly System.Collections.Generic.List<MenuRoom> roomPool = new System.Collections.Generic.List<MenuRoom>();
        readonly System.Collections.Generic.List<int> roomSeq = new System.Collections.Generic.List<int>();

        /// GL: the main menu's rooms (round the box). Off with Reduced effects.
        void DrawMenuRooms(float t, float a) => DrawRooms(t, a, BoxRoomSets);

        /// GL: rooms drawing themselves round `lower` only (the menu box; not the title),
        /// one at a time: each outline traced by a bright pen point, then a faint grid and a scan line fill it; it stays until RoomMax
        /// newer ones are up, then un-draws as the next one starts. Each new room picks a spot clear of the ones still showing.
        void DrawRooms(float t, float a, MenuRoom[][] lower)
        {
            if (a <= 0f || fxReduced || !fxOn)
                return;
            if (Time.frameCount - roomsFrame > 2 || !ReferenceEquals(lower, roomsLower))
            {
                // the screen has just come up (or changed): start again, after a pause
                roomsStart = t + RoomDelay;
                roomsHeard = -1;
                roomSeq.Clear();
                if (!ReferenceEquals(lower, roomsLower))
                {
                    roomsLower = lower;
                    roomPool.Clear();
                    foreach (var set in lower)  // (only round the box, never the title)
                        foreach (var m in set)
                            AddRoomSpot(m);
                }
            }
            roomsFrame = Time.frameCount;
            float e = t - roomsStart;
            if (e < 0f || roomPool.Count == 0)
                return;
            int now = Mathf.FloorToInt(e / RoomGap);
            if (now != roomsHeard && e - now * RoomGap < RoomBuild)
            {
                roomsHeard = now;
                if (sounds != null)
                    sounds.Play(Sfx.MenuRooms, 0.4f * a, Time.unscaledTime);  // quiet bleeps while its walls draw (MenuSoundBank.cs)
            }
            for (int k = Mathf.Max(0, now - RoomMax); k <= now; k++)
                DrawRoom(roomPool[RoomAt(k)], e - k * RoomGap, RoomMax * RoomGap - RoomBuild, t, a);
        }

        void AddRoomSpot(MenuRoom m)
        {
            foreach (var o in roomPool)
                if (o.r.x == m.r.x && o.r.y == m.r.y && o.r.width == m.r.width && o.r.height == m.r.height)
                    return;
            roomPool.Add(m);
        }

        /// Which spot the k-th room takes: a hashed pick, moved on past any spot that overlaps the rooms before it still on screen.
        int RoomAt(int k)
        {
            while (roomSeq.Count <= k)
            {
                int i = roomSeq.Count, n = roomPool.Count, pick = (int)(Hash(i * 13 + 5) * n) % n;
                for (int j = 0; j < n; j++)
                {
                    int c = (pick + j) % n;
                    bool clear = true;
                    for (int back = 1; back <= RoomMax && back <= i && clear; back++)
                    {
                        var o = roomPool[roomSeq[i - back]].r;
                        var cr = roomPool[c].r;
                        if (c == roomSeq[i - back] || (cr.x < o.xMax + 6f && cr.xMax > o.x - 6f && cr.y < o.yMax + 6f && cr.yMax > o.y - 6f))
                            clear = false;
                    }
                    if (clear)
                    {
                        pick = c;
                        break;
                    }
                }
                roomSeq.Add(pick);
            }
            return roomSeq[k];
        }

        static readonly V2[] roomPts = new V2[16];

        void DrawRoom(MenuRoom m, float age, float hold, float t, float a)
        {
            if (age <= 0f)
                return;
            float p = age < RoomBuild ? age / RoomBuild : age < RoomBuild + hold ? 1f : 1f - (age - RoomBuild - hold) / RoomFold;
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

        // --- The Holo Floor background ------------------------------------------------------------------------------------

        const float HoloHorizon = 372f;

        /// Red: a starry sky over a glowing horizon and a perspective grid floor scrolling toward you, with light running down its lanes
        /// (now and then a gold one) and motes drifting up.
        void BgHoloFloor(float t)
        {
            float hz = HoloHorizon;
            GradQuad(new V2(0, 0), new V2(W, 0), new V2(W, hz), new V2(0, hz), HoloHue(new Rgba(0.043f, 0.016f, 0.02f)), HoloHue(new Rgba(0.19f, 0.047f, 0.067f)));
            Stars(110, t, 0.55f, 4100);
            GradQuad(new V2(0, hz), new V2(W, hz), new V2(W, H), new V2(0, H), HoloHue(new Rgba(0.25f, 0.075f, 0.09f)), HoloHue(new Rgba(0.043f, 0.016f, 0.02f)));
            SoftGlow(new V2(W / 2f, hz), 560f, HoloHue(new Rgba(1f, 0.37f, 0.45f, 0.3f + 0.08f * Mathf.Sin(t * 0.9f))));
            var grid = HoloHue(new Rgba(0.91f, 0.36f, 0.42f, 0.5f));
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
                var pc = Hash(s * 13 + pass * 7) < 0.2f ? HoloHue(new Rgba(0.95f, 0.76f, 0.31f)) : HoloHue(new Rgba(1f, 0.59f, 0.62f));
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
            FillRect(0, hz - 1f, W, 2f, HoloHue(new Rgba(1f, 0.55f, 0.6f, 0.75f)));
            GradQuad(new V2(0, hz), new V2(W, hz), new V2(W, hz + 60f), new V2(0, hz + 60f), HoloHue(new Rgba(0.25f, 0.075f, 0.09f, 0.8f)), HoloHue(new Rgba(0.25f, 0.075f, 0.09f, 0f)));
            var mote = HoloHue(new Rgba(1f, 0.59f, 0.63f));  // (every colour turned to the picked hue, MenuBgHue.cs)
            for (int i = 0; i < 40; i++)  // motes drifting up off the floor
            {
                float sp = 0.3f + Hash(i * 5 + 2), span = H - hz + 80f;
                float y = H - MathX.Fmod(t * 12f * sp + Hash(i * 5 + 3) * span, span);
                float x = Hash(i * 5 + 4) * W + Mathf.Sin(t + i) * 4f;
                FillRect(x, y, 2f, 2f, mote.WithAlpha(0.25f + 0.25f * Mathf.Sin(t * 2f + i)));
            }
        }

        static V2 LaneAt(int lane, float e, float hz) =>
            new V2(Mathf.Lerp(W / 2f + lane * 8f, W / 2f + lane * 150f, e), hz + (H - hz) * e);
    }
}

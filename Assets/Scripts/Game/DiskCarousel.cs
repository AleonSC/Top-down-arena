using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Mockups/whos-playing-carousel.html, Docs/Profiles.md): the monitor with its drive at the top, and one big
    /// floppy disk per profile on an endless side-scrolling carousel: the middle disk large, two either side smaller and fainter (fading out at the edges), wrapping
    /// round (up to ProfileStore.MaxProfiles, 50). Scroll with the ‹ › keys, the arrow keys, the mouse wheel or a drag; click a side disk to
    /// bring it to the middle; click the middle one (or press Enter) to load it, which plays the disk boot sequence (ProfileTransition.cs).
    /// Under it: PROFILE n OF m (dots while there are 12 or fewer), then Manage profiles (Rename and Delete under the middle disk; Delete
    /// asks twice) and New profile, which adds a blank disk at the end and asks for its name; the name is written on the disk as you type.
    public partial class ArenaApp
    {
        const float CarCx = W / 2f, CarCy = 392f, CarSize = 260f;   // the middle disk's centre and size
        static readonly float[] CarX = { 0f, 265f, 440f, 560f, 640f }, CarS = { 1f, 0.6f, 0.4f, 0.28f, 0.2f }, CarA = { 1f, 0.9f, 0.7f, 0.3f, 0f };
        const float CarReach = 3.2f;                                  // disks farther from the middle than this aren't drawn
        const float CarFadeFrom = 400f, CarFadeTo = 600f, CarFadeStrip = 6f;  // the edge fade: px from the centre where it starts and ends
        static readonly Rect CarLeftKey = new Rect(440f, 548f, 54f, 54f), CarRightKey = new Rect(786f, 548f, 54f, 54f);
        static readonly Rect WhoManageKey = new Rect(380f, 628f, 250f, 58f), WhoNewKey = new Rect(650f, 628f, 250f, 58f);

        float whoCarPos, whoCarTarget;       // the carousel's place (in disks; whole numbers are a disk in the middle), and where it's easing to
        float carDragX = -1f, carDragFrom; // a drag across the disks: where it started, and the carousel's place then
        bool carDragged;               // the mouse moved enough that the press is a drag, not a click
        int carBeforeNew;              // the disk in the middle when New profile was pressed (Cancel goes back to it)

        /// The disks on the carousel: every profile, then the blank disk while a new one is being named.
        int CarCount => whoList.Count + (whoEdit == "new" ? 1 : 0);

        /// The disk in the middle (its index on the carousel).
        int CarMiddle => CarCount == 0 ? 0 : Mod(Mathf.RoundToInt(whoCarPos), CarCount);

        static int Mod(int a, int n) => ((a % n) + n) % n;

        /// How far disk i is from the middle, the short way round (negative: to the left).
        float CarOffset(int i)
        {
            int n = CarCount;
            float d = i - whoCarPos;
            if (n <= 1)
                return d;
            d = ((d % n) + n) % n;
            return d > n / 2f ? d - n : d;
        }

        static float CarCurve(float a, float[] v)
        {
            int i = Mathf.Min(Mathf.FloorToInt(a), v.Length - 2);
            return Mathf.Lerp(v[i], v[i + 1], Mathf.Clamp01(a - i));
        }

        /// Where a disk off the middle by `off` is drawn, and how strongly.
        static Rect CarRect(float off, out float alpha)
        {
            float a = Mathf.Abs(off), s = CarCurve(a, CarS);
            alpha = CarCurve(a, CarA);
            float size = CarSize * s, cx = CarCx + Mathf.Sign(off) * CarCurve(a, CarX), cy = CarCy + (1f - s) * 60f;
            return new Rect(cx - size / 2f, cy - size / 2f, size, size);
        }

        ProfileInfo WhoAt(int i) => i >= 0 && i < whoList.Count ? whoList[i] : null;
        bool WhoFull => whoList.Count >= ProfileStore.MaxProfiles;

        /// Puts the carousel on profile id (or the first), at once.
        void CarCenterOn(string id)
        {
            int i = Mathf.Max(0, whoList.FindIndex(p => p.Id == id));
            whoCarPos = whoCarTarget = i;
        }

        /// Moves the carousel by n disks (eased).
        void CarStep(int n)
        {
            if (CarCount < 2)
                return;
            whoCarTarget = Mathf.Round(whoCarTarget) + n;
            MenuSound(Sfx.MenuKey, 0.45f);
        }

        void DrawWho()
        {
            float t = Time.unscaledTime;
            if (whoList.Count == 0 && ProfileStore.Root != null && !whoAutoNew && whoEdit == null)
            {
                WhoRefresh();
                if (whoList.Count == 0)
                {
                    whoAutoNew = true;
                    StartNewProfile();
                }
            }
            bool editing = whoEdit != null;
            var e = Event.current;
            if (editing && e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape && whoList.Count > 0)
            {
                CancelWhoEdit();
                e.Use();
                editing = false;
            }
            if (e.type == EventType.Repaint)
            {
                float dt = Mathf.Min(0.05f, Time.unscaledDeltaTime);
                whoCarPos = fxOn && !fxReduced ? Mathf.Lerp(whoCarPos, whoCarTarget, 1f - Mathf.Exp(-12f * dt)) : whoCarTarget;
                if (Mathf.Abs(whoCarTarget - whoCarPos) < 0.001f)
                    whoCarPos = whoCarTarget;
            }
            bool live = !editing;
            if (live)
                WhoInput();
            DrawMonitor(TitleAll(WhoTitleText), WhoDrive(), false, false);
            DrawCarousel(live && !whoManage, t);
            if (editing)
            {
                DrawWhoEditor(new Rect(W / 2f - 250f, 556f, 500f, 118f));
                return;
            }
            if (RoundKey(CarLeftKey, "‹", true))
                CarStep(-1);
            if (RoundKey(CarRightKey, "›", true))
                CarStep(1);
            DrawCarCount();
            if (whoError.Length > 0)
                Ui.Label(new Rect(W / 2f - 250f, 604f, 500f, 20f), whoError, 13, Ui.Warn, TextAnchor.MiddleCenter);
            if (MachKey(WhoManageKey, whoManage ? "DONE" : "MANAGE PROFILES", 15, true, -1, PixelArt.Get(whoManage ? "ui_check" : "ui_gear")))
            {
                MenuSound(Sfx.MenuKey);
                whoManage = !whoManage;
                whoArmed = null;
                whoError = "";
            }
            GUI.enabled = !WhoFull;
            if (MachKey(WhoNewKey, "+  NEW PROFILE", 15, !WhoFull, -1, null, false, WhoFull ? 0.45f : 1f))
            {
                MenuSound(Sfx.MenuKey);
                StartNewProfile();
            }
            GUI.enabled = true;
        }

        /// The drive's readout on this screen: which disk would go in.
        DriveState WhoDrive()
        {
            if (whoEdit == "new")
                return DriveEmpty("FORMAT NEW DISK");
            if (whoManage)
                return DriveEmpty("MANAGE PROFILES");
            var p = WhoAt(CarMiddle);
            return p == null ? DriveEmpty() : DriveEmpty("INSERT: " + p.Name.ToUpperInvariant());
        }

        /// The arrow keys, Enter, the mouse wheel and drags across the disks.
        void WhoInput()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && GUIUtility.keyboardControl == 0)
            {
                if (e.keyCode == KeyCode.LeftArrow || e.keyCode == KeyCode.A)
                {
                    CarStep(-1);
                    e.Use();
                }
                else if (e.keyCode == KeyCode.RightArrow || e.keyCode == KeyCode.D)
                {
                    CarStep(1);
                    e.Use();
                }
                else if ((e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter) && !whoManage && WhoAt(CarMiddle) != null)
                {
                    e.Use();
                    ChooseProfile(WhoAt(CarMiddle).Id);
                }
            }
            var band = new Rect(0f, 250f, W, 290f);
            float wheel = WheelOver(band, true);
            if (wheel > 0.01f || wheel < -0.01f)
                CarStep(wheel > 0f ? 1 : -1);
            if (e.type == EventType.MouseDown && e.button == 0 && band.Contains(e.mousePosition) && CarCount > 1)
            {
                carDragX = e.mousePosition.x;
                carDragFrom = whoCarTarget;
                carDragged = false;
            }
            else if (e.type == EventType.MouseDrag && carDragX >= 0f)
            {
                float dx = e.mousePosition.x - carDragX;
                if (Mathf.Abs(dx) > 6f)
                    carDragged = true;
                if (carDragged)
                {
                    whoCarTarget = carDragFrom - dx / CarX[1];
                    whoCarPos = whoCarTarget;
                    e.Use();
                }
            }
            else if (e.type == EventType.MouseUp && carDragX >= 0f)
            {
                carDragX = -1f;
                if (carDragged)
                {
                    whoCarTarget = Mathf.Round(whoCarTarget);
                    Ui.ForgetPress();  // the release ends the drag; it isn't a click on the disk under it
                }
            }
        }

        /// The disks, farthest first so the middle one is on top; clicks on them; Rename and Delete under the middle one while managing.
        void DrawCarousel(bool live, float t)
        {
            int n = CarCount;
            if (n == 0)
                return;
            // The reel repeats endlessly, so each disk is drawn at every place it holds within reach (with 2 or 3 profiles a disk can be
            // on both sides at once): one rolling out of the middle keeps going the way it went instead of jumping across.
            var order = new System.Collections.Generic.List<Vector2>();  // (disk, its offset from the middle)
            for (int i = 0; i < n; i++)
            {
                float d = CarOffset(i);
                for (int k = -3; k <= 3; k++)
                    if ((k == 0 || n > 1) && Mathf.Abs(d + k * n) < CarReach)
                        order.Add(new Vector2(i, d + k * n));
            }
            order.Sort((a, b) => Mathf.Abs(b.y).CompareTo(Mathf.Abs(a.y)));
            int mid = CarMiddle;
            foreach (var o in order)
            {
                int i = (int)o.x;
                float off = o.y;
                var r = CarRect(off, out float a);
                if (a <= 0.01f)
                    continue;
                DrawCarFaded(r, a, rr => DrawCarDisk(rr, i));
                if (Mathf.Abs(off) > 0.5f && Mathf.Abs(off) < 2.5f && live && whoEdit == null && Ui.HitArea(r) && !carDragged)
                {
                    whoCarTarget = Mathf.Round(whoCarTarget) + Mathf.Round(off);
                    MenuSound(Sfx.MenuKey, 0.45f);
                }
            }
            var mr = CarRect(CarOffset(mid), out _);
            var p = WhoAt(mid);
            if (p == null || Mathf.Abs(CarOffset(mid)) > 0.3f)
                return;
            if (whoManage && whoEdit == null)
                DrawManageChips(mr, p);
            else if (live && whoEdit == null && Ui.HitArea(mr) && !carDragged)
                ChooseProfile(p.Id);
        }

        /// Draws a disk at alpha a, fading out toward the carousel's left and right edges: a disk inside the fade band is drawn in thin
        /// vertical strips (each clipped by a GUI group), each strip as faint as its distance from the middle says.
        static void DrawCarFaded(Rect r, float a, System.Action<Rect> draw)
        {
            float near = Mathf.Min(Mathf.Abs(r.x - CarCx), Mathf.Abs(r.xMax - CarCx));
            if (r.x < CarCx && r.xMax > CarCx)
                near = 0f;
            float far = Mathf.Max(Mathf.Abs(r.x - CarCx), Mathf.Abs(r.xMax - CarCx));
            if (far <= CarFadeFrom)
            {
                WithAlphaS(a, () => draw(r));
                return;
            }
            if (near >= CarFadeTo)
                return;
            for (float x = r.x; x < r.xMax; x += CarFadeStrip)
            {
                float w = Mathf.Min(CarFadeStrip, r.xMax - x), f = CarEdgeFade(Mathf.Abs(x + w / 2f - CarCx));
                if (f * a <= 0.01f)
                    continue;
                GUI.BeginGroup(new Rect(x, r.y - 20f, w, r.height + 40f));  // (20 px spare above and below for the drop shadow)
                WithAlphaS(a * f, () => draw(new Rect(r.x - x, 20f, r.width, r.height)));
                GUI.EndGroup();
            }
        }

        /// 1 near the middle, easing to 0 at the carousel's edges.
        static float CarEdgeFade(float dist) => 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((dist - CarFadeFrom) / (CarFadeTo - CarFadeFrom)));

        static void WithAlphaS(float a, System.Action draw)
        {
            var old = GUI.color;
            float oldA = Ui.Alpha;
            Ui.Alpha = oldA * a;
            GUI.color = old.ScaleAlpha(a);
            draw();
            Ui.Alpha = oldA;
            GUI.color = old;
        }

        /// Disk i: a profile's disk, or the blank one being named.
        void DrawCarDisk(Rect r, int i)
        {
            var p = WhoAt(i);
            if (p != null)
                DrawWhoDisk(r, p, p.Id == ProfileStore.ActiveId);
            else
                DrawBlankDisk(r, whoName);
        }

        /// A new, unlabelled disk: light grey, its label blank but for the name being typed (in marker, as on the others).
        void DrawBlankDisk(Rect d, string name)
        {
            float k = d.width / 200f, ch = 24f * k;
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            Color c1 = new Color(0.85f, 0.86f, 0.89f), c2 = new Color(0.6f, 0.62f, 0.67f);
            for (float y = 0f; y < d.height; y += 2f)
                Ui.Fill(new Rect(d.x, d.y + y, y < ch ? d.width - ch + y : d.width, 2.5f), Color.Lerp(c1, c2, y / d.height));
            var sh = new Rect(d.x + 46f * k, d.y, 108f * k, 60f * k);
            Ui.Fill(sh, new Color(0.78f, 0.8f, 0.86f));
            Ui.Fill(new Rect(sh.x + sh.width * 0.2f, sh.y, sh.width * 0.35f, sh.height), new Color(0.93f, 0.94f, 0.97f));
            Ui.Pill(new Rect(sh.xMax - 38f * k, sh.y + 9f * k, 24f * k, 38f * k), new Color(0.04f, 0.055f, 0.1f));
            var lb = new Rect(d.x + 16f * k, d.y + 70f * k, 168f * k, 110f * k);
            Ui.Fill(lb, new Color(0.96f, 0.94f, 0.88f));
            for (int j = 1; j <= 4; j++)
                Ui.Fill(new Rect(lb.x, lb.y + 17f * k + j * 19f * k, lb.width, 1f), new Color(0.31f, 0.43f, 0.75f, 0.2f));
            Ui.Fill(new Rect(lb.x, lb.y, lb.width, 17f * k), new Color(0.72f, 0.74f, 0.8f));
            Ui.Label(new Rect(lb.x + 8f * k, lb.y, 120f * k, 17f * k), "NEW DISK", Mathf.Max(8, Mathf.RoundToInt(10f * k)), new Color(0.04f, 0.055f, 0.12f, 0.85f), TextAnchor.MiddleLeft, true);
            if (name.Length > 0)
            {
                int size = Mathf.RoundToInt(23f * k);
                Ui.HandFont = true;
                while (size > 11 && Ui.TextWidth(name, size, true) + 8f > lb.width - 12f * k)
                    size--;
                Ui.Label(new Rect(lb.x + 6f * k, lb.y + 19f * k, lb.width - 8f * k, 30f * k), name, size, new Color(0.1f, 0.14f, 0.28f), TextAnchor.MiddleLeft, true);
                Ui.HandFont = false;
            }
            else
                Ui.Label(new Rect(lb.x + 6f * k, lb.y + 22f * k, lb.width - 8f * k, 24f * k), "your name here", Mathf.RoundToInt(12f * k), new Color(0.35f, 0.4f, 0.56f, 0.6f), TextAnchor.MiddleLeft);
        }

        /// RENAME and DELETE (then SURE?) under the middle disk while managing.
        void DrawManageChips(Rect disk, ProfileInfo p)
        {
            bool armed = whoArmed == p.Id, canDelete = whoList.Count > 1;
            string del = armed ? "SURE?" : "DELETE";
            float w1 = Ui.TextWidth("RENAME", 13, true) + 22f, w2 = Ui.TextWidth(del, 13, true) + 22f, gap = 10f;
            float x = disk.center.x - (w1 + gap + w2) / 2f, y = disk.yMax + 10f;
            if (ManageChip(new Rect(x, y, w1, 30f), "RENAME", false, true))
            {
                whoEdit = p.Id;
                whoName = p.Name;
                whoError = "";
                whoFocus = true;
            }
            if (ManageChip(new Rect(x + w1 + gap, y, w2, 30f), del, true, canDelete))
            {
                if (!armed)
                {
                    whoArmed = p.Id;
                    MenuSound(Sfx.RunDelArm);
                }
                else
                {
                    MenuSound(Sfx.RunDelete);
                    ProfileStore.Delete(p.Id);
                    whoArmed = null;
                    savesCountT = 0f;
                    WhoRefresh();
                    whoCarPos = whoCarTarget = Mathf.Clamp(Mathf.Round(whoCarTarget), 0f, Mathf.Max(0, whoList.Count - 1));
                }
            }
        }

        bool ManageChip(Rect r, string text, bool danger, bool live)
        {
            bool hover = live && r.Contains(Event.current.mousePosition);
            var line = danger ? CrtStyle.Danger : CrtStyle.Ink;
            var ink = danger ? new Color(1f, 0.54f, 0.54f) : CrtStyle.T(new Color(0.81f, 0.878f, 1f));
            float a = live ? 1f : 0.4f;
            Ui.RoundBox(r, CrtStyle.T(new Color(0.04f, 0.08f, 0.16f, 0.85f)), 4);
            if (hover)
                Ui.RoundBox(r, line.WithAlpha(0.18f), 4);
            EdgeRing(r, 2f, line.WithAlpha(a));
            Ui.Label(r, text, 13, ink.WithAlpha(a), TextAnchor.MiddleCenter, true);
            return live && Ui.HitArea(r);
        }

        /// PROFILE n OF m under the carousel, and a dot per profile while there are 12 or fewer.
        void DrawCarCount()
        {
            int n = whoList.Count, mid = CarMiddle;
            string text = n == 0 ? "NO PROFILES YET" : "PROFILE " + (mid + 1) + " OF " + n + (WhoFull ? "  ·  FULL" : "");
            Ui.Label(new Rect(0f, 567f, W, 18f), text, 12, new Color(0f, 0f, 0f, 0.6f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(0f, 566f, W, 18f), text, 12, new Color(0.788f, 0.839f, 0.961f), TextAnchor.MiddleCenter, true);
            if (n < 2 || n > 12)
                return;
            float w = n * 6f + (n - 1) * 5f, x = W / 2f - w / 2f;
            for (int i = 0; i < n; i++)
            {
                var d = new Rect(x + i * 11f, 590f, 6f, 6f);
                if (i == mid)
                    Ui.RadialGlow(new Rect(d.x - 6f, d.y - 6f, 18f, 18f), MachNeon.WithAlpha(0.7f));
                Ui.Dot(d, i == mid ? MachNeon : new Color(1f, 1f, 1f, 0.25f));
            }
        }

        /// New profile: a blank disk joins the carousel's end, comes to the middle, and the name box opens.
        void StartNewProfile()
        {
            if (WhoFull)
                return;
            whoManage = false;
            whoArmed = null;
            whoEdit = "new";
            whoName = "";
            whoError = "";
            whoFocus = true;
            carBeforeNew = CarMiddle;
            int blank = whoList.Count;
            whoCarTarget = Mathf.Round(whoCarPos) + CarOffsetFrom(Mathf.Round(whoCarPos), blank);
            if (!(fxOn && !fxReduced) || whoList.Count == 0)
                whoCarPos = whoCarTarget;
        }

        /// The short way round from carousel place `from` to disk i (with the blank disk counted).
        float CarOffsetFrom(float from, int i)
        {
            int n = CarCount;
            float d = ((i - from) % n + n) % n;
            return d > n / 2f ? d - n : d;
        }

        /// Ends naming or renaming; a cancelled new profile's blank disk leaves and the carousel slides back to the disk it came from.
        void CancelWhoEdit()
        {
            bool wasNew = whoEdit == "new";
            whoEdit = null;
            whoError = "";
            int n = CarCount;
            if (n == 0)
                return;
            int back = wasNew ? Mathf.Clamp(carBeforeNew, 0, n - 1) : Mod(Mathf.RoundToInt(whoCarTarget), n);
            float from = Mathf.Round(whoCarTarget);
            whoCarTarget = from + CarOffsetFrom(from, back);
            if (n == 1 || !(fxOn && !fxReduced))
                whoCarPos = whoCarTarget = back;
        }
    }
}

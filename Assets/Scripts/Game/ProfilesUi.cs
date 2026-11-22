using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Profiles.md, holo look: Docs/Mockups/whos-playing-holo.html, "Shelf 4" of Docs/Mockups/whos-playing-shelves.html, mechanical clamp cradles and a marker-pen name from Docs/Mockups/whos-playing-mechanical.html): a screen of its own before the main
    /// menu on every launch, a floppy disk per profile standing on a wall shelf under the disk drive. Click one to play as it; Manage shows Rename and
    /// Delete (Delete asks twice); the + card makes a new one. The main menu's "Switch profile" opens it again.
    /// There are at most ProfileStore.MaxProfiles (3) profiles, and always four boxes: three slots (an empty one says
    /// "Empty") and the New profile box, which is grayed out while all three slots are full. That keeps the move to and
    /// from the main menu (ProfileTransition.cs) the same every time.
    /// Choosing a profile makes it the active one (ProfileStore.cs): its records, rank, saved runs and name.
    public partial class ArenaApp
    {
        bool whoOpen;                 // the screen is up (set at launch; the menu's intro waits for it to close)
        bool whoManage, whoFocus, whoAutoNew;
        string whoEdit;               // "new", a profile's id (renaming it), or null
        string whoName = "", whoError = "", whoArmed;
        List<ProfileInfo> whoList = new List<ProfileInfo>();
        bool profileChosen;           // a profile was picked on this screen: the name field now edits its name

        const float WhoCardW = 184f, WhoCardH = 184f, WhoCardGap = 60f;  // a disk's box: square, standing on the shelf (DrawWhoShelf)
        static float WhoCardTop => WhoLayout == 0 ? 280f : 222f;

        /// The screen's graphite look (Ui.Graphite: the blues drawn as greys; the disks and the neon keep their colours).
        public static bool WhoGraphite = true;

        /// Layout mockups (preview WHOLAYOUT=): 0 the drive under the title; with the drive and Manage profiles in a console under the shelf:
        /// 1 hanging, side by side; 2 hanging, stacked; 3 a full-width drawer flush under the shelf.
        public static int WhoLayout;

        /// The console under the shelf (layouts 1 to 3).
        /// Layouts 3 and 4: a drawer flush under the shelf (3: the drive in the middle, the key at the right; 4: the drive over the key, both
        /// in the middle). Their title, rack, shelf and drawer all share the title's left and right edges (WhoEdge).
        static bool WhoDrawer => WhoLayout >= 3;
        const float WhoEdge = 112f;  // the title's neon line (CrtBox is 1040 wide, its bezel 8 more each side)

        static Rect WhoConsole => WhoDrawer ? new Rect(WhoEdge, WhoShelfTop + 54f, W - 2f * WhoEdge, WhoLayout == 4 ? 180f : 116f)
            : WhoLayout == 1 ? new Rect(W / 2f - 390f, WhoShelfTop + 54f + 24f, 780f, 112f)
            : WhoLayout == 2 ? new Rect(W / 2f - 215f, WhoShelfTop + 54f + 20f, 430f, 190f)
            : new Rect(W / 2f - 215f, WhoShelfTop + 54f + 20f, 430f, 190f);
        const int WhoSlots = ProfileStore.MaxProfiles;  // profile slots; the New profile box is the box after them

        /// Where box i sits (0 to 2: the slots; 3: New profile). Always four, so the layout never changes.
        static Rect WhoBox(int i)
        {
            float rowW = (WhoSlots + 1) * WhoCardW + WhoSlots * WhoCardGap;
            return new Rect(W / 2f - rowW / 2f + i * (WhoCardW + WhoCardGap), WhoCardTop, WhoCardW, WhoCardH);
        }

        bool WhoFull => whoList.Count >= WhoSlots;

        /// The box under the mouse that can be clicked (a profile, or New profile while there's room), or -1.
        int WhoHotSlot()
        {
            var m = Event.current.mousePosition;
            for (int i = 0; i <= WhoSlots; i++)
                if (WhoBox(i).Contains(m) && (i == WhoSlots ? !WhoFull : WhoInSlot(i) != null))
                    return i;
            return -1;
        }
        ProfileInfo WhoInSlot(int i) => i < whoList.Count && i < WhoSlots ? whoList[i] : null;
        static Color WhoColor(ProfileInfo p) => p == null ? Ui.Muted : RankLook.TierColor(p.Rank);

        void OpenWho()
        {
            whoOpen = true;
            whoManage = false;
            whoEdit = null;
            whoError = "";
            whoArmed = null;
            whoAutoNew = false;
            WhoRefresh();
        }

        void WhoRefresh() => whoList = ProfileStore.List();

        /// Plays as this profile: its records load, its name becomes yours in the lobby, the screen closes.
        void ChooseProfile(string id)
        {
            if (!ProfileStore.SetActive(id))
            {
                whoError = "Couldn't open that profile.";
                return;
            }
            profileChosen = true;
            nameText = Profile.Current.Name.Length > 0 ? Profile.Current.Name : nameText;
            SaveMenuFields();
            savesCountT = 0f;  // the Continue count is this profile's (SavesUi.cs)
            whoEdit = null;
            StartProfTrIn(id);  // closes the screen: the move to the menu, or at once (ProfileTransition.cs)
        }

        /// Called at start (after the command line): shows this profile's name as yours, unless the command line named you.
        void SyncNameFromProfile()
        {
            if (ProfileStore.ActiveId.Length > 0 && Profile.Current.Name.Length > 0)
                nameText = Profile.Current.Name;
        }

        /// Typing in the menu's name field renames the profile you picked (the name is part of the profile).
        void SyncProfileName()
        {
            if (!profileChosen || ProfileStore.ActiveId.Length == 0)
                return;
            string n = ProfileStore.Clean(nameText);
            if (n.Length > 0 && n != Profile.Current.Name)
            {
                Profile.Current.Name = n;
                Profile.Current.Save();
            }
        }

        void WhoCommitEdit()
        {
            if (whoEdit == "new")
            {
                string problem = ProfileStore.NameProblem(whoName);
                string id = problem.Length == 0 ? ProfileStore.Create(whoName) : null;
                if (id == null)
                {
                    whoError = problem.Length > 0 ? problem : "Couldn't make the profile.";
                    return;
                }
                ChooseProfile(id);  // a new profile goes straight in
                return;
            }
            string bad = ProfileStore.NameProblem(whoName, whoEdit);
            if (bad.Length > 0 || !ProfileStore.Rename(whoEdit, whoName))
            {
                whoError = bad.Length > 0 ? bad : "Couldn't rename it.";
                return;
            }
            if (whoEdit == ProfileStore.ActiveId && profileChosen)
                nameText = Profile.Current.Name;
            whoEdit = null;
            whoError = "";
            WhoRefresh();
        }

        void DrawWho()
        {
            Ui.Graphite = WhoGraphite;
            if (WhoLayout != 0)
            {
                var c = WhoConsole;
                DriveCx = WhoLayout == 1 ? c.x + 34f + DriveW / 2f : W / 2f;
                DriveY = WhoLayout == 1 ? c.y + 42f : WhoLayout == 3 ? c.y + 46f : c.y + 38f;
            }
            try { DrawWhoBody(); }
            finally { Ui.Graphite = false; DriveCx = W / 2f; DriveY = 206f; }
        }

        const float WhoSideKeyW = 270f;  // layout 3's key, smaller so the drive can sit in the middle

        /// The key's rect: under the shelf in the middle, or in the console (layouts 1 to 3).
        Rect WhoManageRect(float kw)
        {
            if (WhoLayout == 0)
                return new Rect(W / 2f - kw / 2f, H - 118f, kw, 74f);
            var c = WhoConsole;
            if (WhoLayout == 2 || WhoLayout == 4)
                return new Rect(W / 2f - kw / 2f, c.y + 92f, kw, 74f);
            if (WhoLayout == 1)
                return new Rect(c.xMax - 34f - kw, c.y + 20f, kw, 74f);
            return new Rect(c.xMax - 30f - WhoSideKeyW, c.y + 22f, WhoSideKeyW, 74f);  // layout 3: at the right, past the drive's bay
        }

        /// The console under the shelf that holds the drive and Manage profiles (layouts 1 to 3): hung from two struts, or a drawer
        /// flush under the shelf; a dark body with faint scanlines and neon round its open sides.
        void DrawWhoConsole()
        {
            var c = WhoConsole;
            float shelfBottom = WhoShelfTop + 54f, cut = 14f;
            if (!WhoDrawer)
                foreach (float sx in new[] { c.x + 60f, c.xMax - 74f })
                {
                    var strut = new Rect(sx, shelfBottom - 2f, 14f, c.y - shelfBottom + 6f);
                    Ui.Fill(strut, new Color(0.08f, 0.1f, 0.2f));
                    WhoBand(new Rect(strut.x + 2f, strut.y, strut.width - 4f, strut.height), new Color(0.35f, 0.42f, 0.66f), new Color(0.16f, 0.2f, 0.38f), 2f);
                    KeyUi.Bolt(new Vector2(strut.center.x, strut.y + 8f), 6f);
                }
            if (WhoDrawer)
            {
                // A drawer built into the shelf: its face carries on from the shelf's front face (same metal, a shade darker), a
                // panel seam where they meet, vents under the shelf's vents, and a recessed bay holding the drive and the key.
                Ui.Pill(new Rect(c.x + 4f, c.y + 8f, c.width - 8f, c.height), new Color(0f, 0f, 0f, 0.5f));
                WhoBand(new Rect(c.x, c.y - 4f, c.width, c.height - cut + 4f), new Color(0.1f, 0.14f, 0.32f), new Color(0.055f, 0.075f, 0.17f), 3f);
                if (BeginUiGl())  // the chamfered bottom corners, the same as the neon's
                {
                    UiModelAt(V2.Zero, 1f);
                    var bot = new Rgba(0.055f, 0.075f, 0.17f);
                    SetColor(bot);
                    Quad(new V2(c.x, c.yMax - cut), new V2(c.xMax, c.yMax - cut), new V2(c.xMax - cut, c.yMax), new V2(c.x + cut, c.yMax));
                    EndUiGl();
                }
                Ui.Fill(new Rect(c.x, c.y, c.width, 2f), new Color(0.02f, 0.025f, 0.05f, 0.9f));       // the seam: a dark groove
                Ui.Fill(new Rect(c.x + 2f, c.y + 2f, c.width - 4f, 1f), new Color(1f, 1f, 1f, 0.12f));  // and the drawer's lit lip under it
                // the bay round the drive (layout 4: round the drive and the key), in the middle
                var bay = WhoLayout == 4 ? new Rect(W / 2f - 205f, c.y + 12f, 410f, c.height - 20f) : new Rect(W / 2f - 200f, c.y + 12f, 400f, c.height - 30f);
                if (WhoLayout == 4)
                {
                    WhoVent(new Rect(c.x + 20f, c.y + 70f, 100f, 24f));
                    WhoVent(new Rect(c.xMax - 120f, c.y + 70f, 100f, 24f));
                }
                else
                {
                    // the left balances the key on the right: a recessed readout plate of the same size
                    var plate = new Rect(c.x + 30f, c.y + 22f, WhoSideKeyW, 64f);
                    Ui.Pill(new Rect(plate.x - 1f, plate.y - 1f, plate.width + 2f, plate.height + 2f), new Color(1f, 1f, 1f, 0.07f));
                    Ui.Pill(plate, new Color(0.025f, 0.03f, 0.06f));
                    Ui.Fill(new Rect(plate.x + 6f, plate.y + 1f, plate.width - 12f, 3f), new Color(0f, 0f, 0f, 0.5f));
                    WhoVent(new Rect(plate.x + 14f, plate.y + 20f, 100f, 24f));
                    Ui.Label(new Rect(plate.x + 124f, plate.y + 12f, plate.width - 134f, 20f), "PROFILES", 11, new Color(0.55f, 0.62f, 0.8f), TextAnchor.MiddleLeft, true);
                    Ui.Label(new Rect(plate.x + 124f, plate.y + 30f, plate.width - 134f, 22f), whoList.Count + " / " + WhoSlots, 18, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleLeft, true);
                }
                Ui.Pill(new Rect(bay.x - 1f, bay.y - 1f, bay.width + 2f, bay.height + 2f), new Color(1f, 1f, 1f, 0.07f));
                Ui.Pill(bay, new Color(0.025f, 0.03f, 0.06f));
                Ui.Fill(new Rect(bay.x + 6f, bay.y + 1f, bay.width - 12f, 3f), new Color(0f, 0f, 0f, 0.5f));      // its inner shadow at the top
                Ui.Fill(new Rect(bay.x + 8f, bay.yMax - 2f, bay.width - 16f, 1f), new Color(1f, 1f, 1f, 0.06f));  // and a faint lit lip at the bottom
                foreach (var b in new[] { new Vector2(bay.x - 14f, c.y + 18f), new Vector2(bay.xMax + 14f, c.y + 18f), new Vector2(bay.x - 14f, c.yMax - 26f), new Vector2(bay.xMax + 14f, c.yMax - 26f) })
                    KeyUi.Bolt(b, 6f);
                return;
            }
            Ui.Pill(new Rect(c.x, c.y + 6f, c.width, c.height), new Color(0f, 0f, 0f, 0.45f));
            WhoBand(c, new Color(0.06f, 0.08f, 0.16f, 0.96f), new Color(0.035f, 0.05f, 0.1f, 0.96f), 4f);
            for (float y = c.y + 3f; y < c.yMax - 2f; y += 4f)
                Ui.Fill(new Rect(c.x + 2f, y, c.width - 4f, 1f), new Color(0.36f, 0.52f, 0.91f, 0.035f));
            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                NeonPath(new[] { new V2(c.x, c.y), new V2(c.xMax, c.y), new V2(c.xMax, c.yMax - cut), new V2(c.xMax - cut, c.yMax), new V2(c.x + cut, c.yMax), new V2(c.x, c.yMax - cut) }, true, NeonFlicker(Time.unscaledTime, 11f, 2f));
                EndUiGl();
            }
        }

        /// Layout 3: one neon line from the shelf's top edge down both sides and round the drawer's chamfered bottom, so the shelf and
        /// the drawer read as one unit (drawn after the shelf, over its ends).
        void DrawWhoDrawerNeon()
        {
            var c = WhoConsole;
            float cut = 14f, top = WhoShelfTop;
            if (!BeginUiGl())
                return;
            UiModelAt(V2.Zero, 1f);
            NeonPath(new[] { new V2(c.x, top), new V2(c.x, c.yMax - cut), new V2(c.x + cut, c.yMax), new V2(c.xMax - cut, c.yMax), new V2(c.xMax, c.yMax - cut), new V2(c.xMax, top) }, false, NeonFlicker(Time.unscaledTime, 11f, 2f));
            EndUiGl();
        }

        void DrawWhoBody()
        {
            float t = Time.unscaledTime;
            if (whoList.Count == 0 && ProfileStore.Root != null && !whoAutoNew && whoEdit == null)
            {
                WhoRefresh();
                if (whoList.Count == 0)
                {
                    whoAutoNew = true;
                    whoEdit = "new";
                    whoName = "";
                    whoFocus = true;
                }
            }
            bool editing = whoEdit != null;
            if (editing && Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape && whoList.Count > 0)
            {
                whoEdit = null;
                whoError = "";
                Event.current.Use();
                editing = false;
            }
            DrawWhoChrome(1f);  // the menu's title screen, band, rooms and drive bay (MenuHolo.cs)
            // The drive your disk goes into (DriveSlot.cs); its readout follows the slot under the mouse.
            var drive = DriveEmpty();
            int hot = editing || whoManage ? -1 : WhoHotSlot();
            if (hot >= 0 && hot < WhoSlots && WhoInSlot(hot) != null)
                drive.text = "INSERT: " + WhoInSlot(hot).Name.ToUpperInvariant();
            else if (hot == WhoSlots)
                drive.text = "FORMAT NEW DISK";
            if (Event.current.type == EventType.Repaint)
                for (int i = 0; i <= WhoSlots; i++)
                    whoTagLit[i] = Mathf.MoveTowards(whoTagLit[i], i == hot ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            if (WhoLayout != 0)
                DrawWhoConsole();
            DrawDriveSlot(1f, drive);
            DrawWhoShelf();
            if (WhoDrawer)
                DrawWhoDrawerNeon();
            for (int i = 0; i <= WhoSlots; i++)
            {
                var r = WhoBox(i);
                if (i == WhoSlots)
                    DrawWhoNewCard(r, !editing && !whoManage && !WhoFull);
                else if (WhoInSlot(i) != null)
                    DrawWhoCard(r, WhoInSlot(i), t, !editing);
                else
                    DrawWhoEmptyCard(r);
            }

            float y = WhoShelfTop + 54f + 40f;  // (under the shelf)
            if (editing)
                DrawWhoEditor(new Rect(W / 2f - 250f, Mathf.Min(y, H - 160f), 500f, 118f));
            else if (whoError.Length > 0)
                Ui.Label(new Rect(W / 2f - 250f, y, 500f, 24f), whoError, 14, Ui.Warn, TextAnchor.MiddleCenter);
            if (!editing && whoList.Count > 0)
            {
                // Manage profiles is a keycap like the main menu's Play (KeyUi.cs), with the same VHS hover.
                float kw = KeyUi.WhoStyle == 2 ? 380f : KeyUi.WhoStyle == 1 ? 330f : 300f;
                if (WhoLayout == 3)
                    kw = WhoSideKeyW;
                var kr = WhoManageRect(kw);
                if (MenuVhs.Draw(kr, "TRK 03", true, fxReduced, () => KeyUi.WhoStyle > 0
                    ? KeyUi.Graphite(kr, whoManage ? "Done" : "Manage profiles", PixelArt.Get("ui_gear"), WhoLayout == 3 ? 22 : 26, whoManage, KeyUi.WhoStyle == 2)
                    : KeyUi.Key(kr, whoManage ? "Done" : "Manage profiles", PixelArt.Get("ui_gear"), 26, whoManage)))
                {
                    whoManage = !whoManage;
                    whoArmed = null;
                    whoError = "";
                }
            }
        }

        // ---- the shelf and the disks (look of "Shelf 4, Display shelf" of Docs/Mockups/whos-playing-shelves.html) ----

        readonly float[] whoLift = new float[WhoSlots + 1];  // how far each disk is lifted by the mouse (0..1, eased; opens its clamps)
        readonly float[] whoTagLit = new float[WhoSlots + 1];  // how lit each slot's tag on the shelf is (0..1, eased; on under the mouse)

        /// Where the shelf's top edge is: the disks stand on their pedestals just above it.
        static float WhoShelfTop => WhoCardTop + WhoCardH + 16f;

        /// A vertical gradient drawn as thin strips (the game's boxes are flat, so depth is built from strips).
        static void WhoBand(Rect r, Color top, Color bottom, float step = 3f)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(r.height / step));
            float h = r.height / n;
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(r.x, r.y + i * h, r.width, h + 0.5f), Color.Lerp(top, bottom, (i + 0.5f) / n));
        }

        /// A louvred vent: a dark recess with slits.
        static void WhoVent(Rect r)
        {
            Ui.Pill(r, new Color(0.1f, 0.13f, 0.31f));
            Ui.Pill(new Rect(r.x + 1f, r.y + 2f, r.width - 2f, r.height - 3f), new Color(0.02f, 0.03f, 0.09f));
            for (float x = r.x + 5f; x < r.xMax - 9f; x += 12f)
                Ui.Fill(new Rect(x, r.y + 4f, 6f, r.height - 8f), new Color(0.1f, 0.14f, 0.32f));
        }

        /// One cradle's clamp post: a metal post with a lamp at its top and a bolt at its foot. open (0..1) slides it outwards.
        static void WhoClamp(Rect r, bool lit, bool left)
        {
            // A slim satin post: a dark rim, a smooth face lit on its outer edge, a slit LED near the top, and a rubber grip down the
            // side that holds the disk.
            Ui.Pill(new Rect(r.x + 1f, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.35f));
            Ui.Pill(r, new Color(0.07f, 0.075f, 0.085f));
            var face = new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f);
            Ui.Pill(face, new Color(0.3f, 0.31f, 0.35f));
            WhoBand(new Rect(face.x + 1f, face.y + 3f, face.width - 2f, face.height - 6f), new Color(0.52f, 0.54f, 0.59f), new Color(0.25f, 0.26f, 0.3f), 1f);
            float edge = left ? face.x + 1f : face.xMax - 2f;
            Ui.Fill(new Rect(edge, face.y + 4f, 1f, face.height - 8f), new Color(1f, 1f, 1f, 0.22f));        // the lit outer edge
            float grip = left ? face.xMax - 3f : face.x;
            Ui.Fill(new Rect(grip, face.y + 22f, 3f, face.height - 30f), new Color(0.04f, 0.042f, 0.05f));  // the rubber grip on the disk side
            var slit = new Rect(r.center.x - 1.5f, r.y + 7f, 3f, 12f);
            if (lit)
            {
                Ui.Pill(new Rect(slit.x - 3f, slit.y - 3f, slit.width + 6f, slit.height + 6f), new Color(0.42f, 1f, 0.6f, 0.25f));
                Ui.Fill(slit, new Color(0.55f, 1f, 0.7f));
            }
            else
                Ui.Fill(slit, new Color(0.05f, 0.06f, 0.06f));
        }

        /// The wall, the shelf, and under every disk its cradle: a pedestal with a clamp post either side, which open as the
        /// disk lifts (look "Mechanical 2, Clamp cradles" of Docs/Mockups/whos-playing-mechanical.html). Drawn under the disks.
        void DrawWhoShelf()
        {
            float top = WhoShelfTop, left = WhoDrawer ? WhoEdge : 70f, w = W - 2f * left;
            float inset = WhoDrawer ? 0f : 20f;  // (the drawer layouts: rack, shelf and drawer share the title's edges)
            var wall = new Rect(left + inset, WhoCardTop - 8f, w - 2f * inset, top - WhoCardTop + 8f + (WhoDrawer ? 30f : 54f));  // (layout 3: its bottom edge hides behind the shelf, off the drawer's seam)
            // the wall: a dark see-through rack with faint scanlines and a neon edge (Docs/Mockups/whos-playing-holo.html); the drawer
            // layouts have none: the disks and their clamps stand on the shelf in the open, over the floor
            if (!WhoDrawer)
            {
                Ui.Pill(new Rect(wall.x, wall.y + 6f, wall.width, wall.height), new Color(0f, 0f, 0f, 0.4f));
                WhoBand(wall, new Color(0.047f, 0.075f, 0.16f, 0.93f), new Color(0.04f, 0.06f, 0.13f, 0.93f), 6f);
                for (float y = wall.y + 3f; y < wall.yMax; y += 4f)
                    Ui.Fill(new Rect(wall.x + 2f, y, wall.width - 4f, 1f), new Color(0.36f, 0.52f, 0.91f, 0.04f));
            }
            if (BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                if (!WhoDrawer)
                    NeonRect(wall, NeonFlicker(Time.unscaledTime, 7f, 1.4f));
                HoloCorners(new Rect(left, wall.y, w, WhoDrawer ? WhoConsole.yMax - wall.y : wall.height), 1f, top: !WhoDrawer);  // (the drawer layouts: one frame from the title to the drawer, its top pair the title's)  // round the rack and its shelf (the shelf is the wider)
                EndUiGl();
            }

            // the shelf: a lit top edge, then the front face with a vent at each end and a plate for each slot
            if (!WhoDrawer)  // (layout 3: the drawer carries on under the shelf, so no shadow and no rim along the bottom)
                Ui.Pill(new Rect(left, top + 12f, w, 48f), new Color(0f, 0f, 0f, 0.35f));
            WhoBand(new Rect(left, top, w, 14f), new Color(0.53f, 0.63f, 0.93f), new Color(0.27f, 0.35f, 0.6f), 2f);
            Ui.Fill(new Rect(left + 3f, top, w - 6f, 2f), new Color(0.9f, 0.95f, 1f, 0.9f));  // its lit lip, glowing
            WhoBand(new Rect(left, top + 14f, w, 40f), new Color(0.24f, 0.3f, 0.55f), new Color(0.1f, 0.14f, 0.32f), 3f);
            if (!WhoDrawer)
                Ui.Rim(new Rect(left, top, w, 54f), new Color(0.42f, 0.52f, 0.84f), 0.6f);
            float vw = WhoDrawer ? 80f : 100f;  // (narrower on the drawer layouts' narrower shelf, clear of the slot tags)
            WhoVent(new Rect(left + 20f, top + 22f, vw, 24f));
            WhoVent(new Rect(left + w - 20f - vw, top + 22f, vw, 24f));

            for (int i = 0; i <= WhoSlots; i++)
            {
                var r = WhoBox(i);
                var info = i < WhoSlots ? WhoInSlot(i) : null;
                bool lit = info != null && info.Id == ProfileStore.ActiveId;
                float open = whoManage ? 0f : whoLift[i] * 9f, k = r.width / 200f;
                // the pedestal under the disk: a smooth satin plate, lit along its top, with one groove along it
                var ped = new Rect(r.x - 16f * k, r.yMax - 3f * k, r.width + 32f * k, 14f * k);
                Ui.Pill(new Rect(ped.x + 3f, ped.y + 5f, ped.width - 6f, ped.height), new Color(0f, 0f, 0f, 0.4f));
                Ui.Pill(ped, new Color(0.07f, 0.075f, 0.085f));
                WhoBand(new Rect(ped.x + 1.5f, ped.y + 1.5f, ped.width - 3f, ped.height - 3f), new Color(0.36f, 0.38f, 0.43f), new Color(0.15f, 0.16f, 0.19f), 1f);
                Ui.Fill(new Rect(ped.x + 6f, ped.y + 1.5f, ped.width - 12f, 1f), new Color(1f, 1f, 1f, 0.35f));
                Ui.Fill(new Rect(ped.x + 10f, ped.center.y + 1f, ped.width - 20f, 1f), new Color(0f, 0f, 0f, 0.35f));
                Ui.Fill(new Rect(ped.x + 10f, ped.center.y + 2f, ped.width - 20f, 1f), new Color(1f, 1f, 1f, 0.07f));
                // the clamps, either side of the disk (their grips face it)
                float ch = 80f * k, cw = 16f * k;
                WhoClamp(new Rect(r.x - 22f * k - open, r.yMax - ch + 4f * k, cw, ch), lit, true);
                WhoClamp(new Rect(r.xMax + 6f * k + open, r.yMax - ch + 4f * k, cw, ch), lit, false);
                // the slot's plate on the shelf face
                var plate = new Rect(r.center.x - 50f, top + 19f, 100f, 30f);
                if (whoManage)
                    Ui.Pill(plate, new Color(0.012f, 0.024f, 0.07f));
                else
                    DrawWhoTag(plate, i == WhoSlots ? "NEW" : "SLOT " + (i + 1), whoTagLit[i]);  // MenuHolo.cs
            }
        }

        /// A dashed disk outline (the chamfered corner is at the top right): for the empty slot and New profile.
        static void DrawWhoGhost(Rect r, Color c)
        {
            const float ch = 24f, dash = 7f, gap = 6f;
            for (float x = r.x + 2f; x < r.xMax - ch - dash; x += dash + gap)
                Ui.Fill(new Rect(x, r.y, dash, 2f), c);
            for (float y = r.y + ch + 4f; y < r.yMax - dash; y += dash + gap)
                Ui.Fill(new Rect(r.xMax - 2f, y, 2f, dash), c);
            for (float x = r.x + 2f; x < r.xMax - dash; x += dash + gap)
                Ui.Fill(new Rect(x, r.yMax - 2f, dash, 2f), c);
            for (float y = r.y + 2f; y < r.yMax - dash; y += dash + gap)
                Ui.Fill(new Rect(r.x, y, 2f, dash), c);
            for (int i = 0; i < 4; i++)
                Ui.Fill(new Rect(r.xMax - ch + i * 6f - 1f, r.y + i * 6f + 1f, 3f, 3f), c);
        }

        /// The Mission: Impossible disk: a pale magneto-optical disk, blood-spattered, with a small generic "230 MB" label.
        void DrawMissionDisk(Rect d)
        {
            float k = d.width / 200f;
            Color cream = new Color(0.9f, 0.87f, 0.83f), blood = new Color(0.55f, 0.1f, 0.1f, 0.75f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 22f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(cream, new Color(0.72f, 0.68f, 0.64f), y / d.height));
            }
            Ui.Fill(new Rect(d.x + 2f, d.y, d.width - ch - 2f, 2f), new Color(1f, 1f, 1f, 0.5f));
            Ui.Fill(new Rect(d.x + 44f * k, d.y, 104f * k, 44f * k), new Color(0.74f, 0.75f, 0.78f));      // the shutter
            Ui.Fill(new Rect(d.x + 110f * k, d.y, 38f * k, 44f * k), new Color(0.84f, 0.85f, 0.88f));
            Ui.Fill(new Rect(d.x + 56f * k, d.y + 8f * k, 24f * k, 30f * k), new Color(0.2f, 0.2f, 0.22f));
            // the spatter: a few big pools, each throwing a spray of droplets (smaller the further they fly), plus drips running down
            float[] bx = { 0.16f, 0.82f, 0.7f, 0.3f, 0.52f, 0.9f };
            float[] by = { 0.24f, 0.3f, 0.78f, 0.74f, 0.5f, 0.9f };
            float[] bs = { 30f, 26f, 32f, 20f, 16f, 18f };
            Color deep = new Color(0.4f, 0.05f, 0.06f, 0.85f);
            uint seed = 7u;
            for (int i = 0; i < bx.Length; i++)
            {
                float cx = d.x + bx[i] * d.width, cy = d.y + by[i] * d.height;
                Ui.Dot(new Rect(cx - bs[i] * k * 0.5f, cy - bs[i] * k * 0.5f, bs[i] * k, bs[i] * k), blood);
                Ui.Dot(new Rect(cx - bs[i] * k * 0.3f, cy - bs[i] * k * 0.3f, bs[i] * k * 0.55f, bs[i] * k * 0.55f), deep);
                for (int j = 0; j < 9; j++)   // the spray
                {
                    seed = seed * 1664525u + 1013904223u; float ang = (seed >> 8 & 0xFFFF) / 65536f * 6.2832f;
                    seed = seed * 1664525u + 1013904223u; float dist = (0.55f + (seed >> 8 & 0xFFFF) / 65536f * 1.6f) * bs[i] * k;
                    float ds = Mathf.Max(2f, bs[i] * 0.34f * (1.9f - dist / (bs[i] * k)) * k * 0.6f);
                    Ui.Dot(new Rect(cx + Mathf.Cos(ang) * dist - ds / 2f, cy + Mathf.Sin(ang) * dist - ds / 2f, ds, ds), blood);
                    if (j % 3 == 0)   // a short streak behind the bigger droplets, pointing back at the pool
                        Ui.Fill(new Rect(cx + Mathf.Cos(ang) * dist * 0.7f - 1f, cy + Mathf.Sin(ang) * dist * 0.7f - 1f, 2f, 2f), blood);
                }
                float dl = (10f + (i * 7 % 5) * 5f) * k;   // a drip running down from the pool
                Ui.Fill(new Rect(cx - 2f * k, cy, 4f * k, dl), blood);
                Ui.Dot(new Rect(cx - 3f * k, cy + dl - 2f * k, 6f * k, 6f * k), deep);
            }
            var lb = new Rect(d.x + 24f * k, d.y + 66f * k, 76f * k, 60f * k);                                 // the Verbatim label
            Ui.Fill(lb, new Color(0.97f, 0.96f, 0.94f));
            Ui.Fill(new Rect(lb.x, lb.yMax - 1f, lb.width, 1f), new Color(0f, 0f, 0f, 0.2f));
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 2f * k, lb.width - 8f * k, 16f * k), "DataDisk", Mathf.RoundToInt(13f * k), new Color(0.12f, 0.12f, 0.16f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 18f * k, lb.width - 8f * k, 10f * k), "FORMATTED", Mathf.Max(6, Mathf.RoundToInt(6.5f * k)), new Color(0.3f, 0.3f, 0.34f), TextAnchor.MiddleLeft);
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 28f * k, lb.width - 8f * k, 18f * k), "230 MB", Mathf.RoundToInt(15f * k), new Color(0.12f, 0.12f, 0.16f), TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 46f * k, lb.width - 8f * k, 10f * k), "Optical Disk", Mathf.Max(6, Mathf.RoundToInt(6.5f * k)), new Color(0.3f, 0.3f, 0.34f), TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(d.x + 10f * k, d.yMax - 22f * k, 10f * k, 10f * k), new Color(0.3f, 0.28f, 0.27f));
        }

        /// The hologram disk: a see-through cyan floppy of light with scanlines, a sweeping glint, a faint flicker and a chromatic ghost; the label
        /// shows the profile's name as a floating readout.
        void DrawHoloDisk(Rect d, ProfileInfo p)
        {
            float k = d.width / 200f, t = Time.unscaledTime;
            float flick = 0.9f + 0.1f * Mathf.Sin(t * 23f) * Mathf.Sin(t * 7.3f);
            Color cy = new Color(0.25f, 0.95f, 1f), mg = new Color(1f, 0.3f, 0.85f);
            float ch = 22f * k;
            // soft base glow and a faint body
            Ui.Pill(new Rect(d.x - 4f, d.y - 2f, d.width + 8f, d.height + 6f), cy.WithAlpha(0.07f * flick));
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                float a = (0.14f + 0.1f * (1f - y / d.height)) * flick;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), new Color(0.05f, 0.4f, 0.5f, a));
            }
            for (float y = 1f; y < d.height; y += 4f)   // scanlines
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 1f), cy.WithAlpha(0.16f * flick));
            }
            // the outline, with a magenta ghost one pixel off
            for (int pass = 0; pass < 2; pass++)
            {
                float o = pass == 0 ? 2f : 0f;
                Color c = pass == 0 ? mg.WithAlpha(0.35f * flick) : cy.WithAlpha(0.95f * flick);
                Ui.Fill(new Rect(d.x + o, d.y, d.width - ch - o, 2f), c);
                Ui.Fill(new Rect(d.x + o, d.yMax - 2f, d.width, 2f), c);
                Ui.Fill(new Rect(d.x + o, d.y, 2f, d.height), c);
                Ui.Fill(new Rect(d.xMax - 2f + o, d.y + ch, 2f, d.height - ch), c);
                for (int i = 0; i < 4; i++)
                    Ui.Fill(new Rect(d.xMax - ch + i * ch / 4f + o, d.y + i * ch / 4f, ch / 4f + 1f, 2f), c);
            }
            // the shutter and hub holes as wireframes
            var sh = new Rect(d.x + 46f * k, d.y + 4f * k, 106f * k, 52f * k);
            Color wf = cy.WithAlpha(0.7f * flick);
            Ui.Fill(new Rect(sh.x, sh.y, sh.width, 1.5f), wf); Ui.Fill(new Rect(sh.x, sh.yMax, sh.width, 1.5f), wf);
            Ui.Fill(new Rect(sh.x, sh.y, 1.5f, sh.height), wf); Ui.Fill(new Rect(sh.xMax, sh.y, 1.5f, sh.height + 1.5f), wf);
            Ui.Fill(new Rect(sh.x + 14f * k, sh.y + 8f * k, 22f * k, 34f * k), cy.WithAlpha(0.35f * flick));
            // the readout panel with the name
            var lb = new Rect(d.x + 16f * k, d.y + 74f * k, 168f * k, 98f * k);
            Ui.Fill(lb, cy.WithAlpha(0.1f * flick));
            Ui.Fill(new Rect(lb.x, lb.y, lb.width, 1.5f), wf); Ui.Fill(new Rect(lb.x, lb.yMax, lb.width, 1.5f), wf);
            Ui.Fill(new Rect(lb.x, lb.y, 1.5f, lb.height), wf); Ui.Fill(new Rect(lb.xMax, lb.y, 1.5f, lb.height + 1.5f), wf);
            string nm = p.Name.ToUpperInvariant();
            int size = Mathf.RoundToInt(22f * k);
            while (size > 10 && Ui.TextWidth(nm, size, true) + 12f > lb.width)
                size--;
            Ui.Label(new Rect(lb.x + 2f, lb.y + 12f * k, lb.width, 34f * k), nm, size, mg.WithAlpha(0.4f * flick), TextAnchor.MiddleCenter, true);   // ghost
            Ui.Label(new Rect(lb.x, lb.y + 12f * k, lb.width, 34f * k), nm, size, new Color(0.8f, 1f, 1f, flick), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(lb.x, lb.y + 52f * k, lb.width, 16f * k), "HOLOGRAPHIC PROFILE", Mathf.Max(6, Mathf.RoundToInt(8.5f * k)), cy.WithAlpha(0.85f * flick), TextAnchor.MiddleCenter);
            Ui.Label(new Rect(lb.x, lb.y + 70f * k, lb.width, 16f * k), "RANK " + p.Rank, Mathf.Max(6, Mathf.RoundToInt(9f * k)), cy.WithAlpha(0.6f * flick), TextAnchor.MiddleCenter);
            // the glint: a bright band that sweeps down the disk every few seconds
            float sweep = Mathf.Repeat(t * 0.35f, 1.6f) - 0.3f;
            float gy = d.y + sweep * d.height;
            for (int i = 0; i < 6; i++)
            {
                float yy = gy + i * 2.5f * k - 7f * k;
                if (yy < d.y || yy > d.yMax - 2f) continue;
                Ui.Fill(new Rect(d.x + 2f, yy, d.width - 4f, 2.5f * k), new Color(1f, 1f, 1f, 0.12f * (1f - Mathf.Abs(i - 2.5f) / 3f) * flick));
            }
        }

        /// The Windows 95 disk: a dark grey floppy with a white label, a four-pane logo of its own and "Desktop System 95 / Setup Disk n".
        void DrawWin95Disk(Rect d, int slot)
        {
            float k = d.width / 200f;
            Color plastic = new Color(0.2f, 0.21f, 0.25f), ink = new Color(0.08f, 0.08f, 0.1f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 18f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(plastic, new Color(0.1f, 0.11f, 0.14f), y / d.height));
            }
            Ui.Fill(new Rect(d.x + 2f, d.y, d.width - ch - 2f, 2f), new Color(1f, 1f, 1f, 0.16f));
            Ui.Fill(new Rect(d.x + 44f * k, d.y, 108f * k, 52f * k), new Color(0.74f, 0.75f, 0.78f));       // the shutter
            Ui.Fill(new Rect(d.x + 116f * k, d.y, 36f * k, 52f * k), new Color(0.86f, 0.87f, 0.9f));
            Ui.Fill(new Rect(d.x + 60f * k, d.y + 8f * k, 24f * k, 36f * k), new Color(0.06f, 0.06f, 0.08f));
            var lb = new Rect(d.x + 14f * k, d.y + 64f * k, 172f * k, 114f * k);                              // the label
            Ui.Fill(lb, new Color(0.97f, 0.97f, 0.95f));
            Ui.Fill(new Rect(lb.x, lb.y, lb.width, 5f * k), new Color(0f, 0.5f, 0.5f));                        // a teal band, the desktop colour
            float fx = lb.x + 12f * k, fy = lb.y + 16f * k, fs = 20f * k;                                     // the flag: four panes
            Ui.Fill(new Rect(fx, fy, fs, fs), new Color(0.95f, 0.45f, 0.2f));
            Ui.Fill(new Rect(fx + fs + 3f * k, fy, fs, fs), new Color(0.2f, 0.65f, 0.6f));
            Ui.Fill(new Rect(fx, fy + fs + 3f * k, fs, fs), new Color(0.5f, 0.3f, 0.8f));
            Ui.Fill(new Rect(fx + fs + 3f * k, fy + fs + 3f * k, fs, fs), new Color(0.9f, 0.3f, 0.5f));
            Ui.Label(new Rect(lb.x + 62f * k, lb.y + 14f * k, lb.width - 66f * k, 14f * k), "Desktop", Mathf.RoundToInt(11f * k), ink, TextAnchor.MiddleLeft);
            Ui.Label(new Rect(lb.x + 62f * k, lb.y + 28f * k, lb.width - 66f * k, 30f * k), "System 95", Mathf.RoundToInt(17f * k), ink, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(lb.x + 62f * k, lb.y + 58f * k, lb.width - 66f * k, 14f * k), "Setup Disk " + (slot + 1), Mathf.RoundToInt(11f * k), new Color(0.25f, 0.25f, 0.3f), TextAnchor.MiddleLeft);
            Ui.Fill(new Rect(lb.x + 8f * k, lb.y + 86f * k, lb.width - 16f * k, 1f), new Color(0f, 0f, 0f, 0.2f));
            Ui.Label(new Rect(lb.x + 8f * k, lb.y + 90f * k, lb.width - 16f * k, 16f * k), "Insert this disk in drive A:", Mathf.Max(6, Mathf.RoundToInt(8f * k)), new Color(0.3f, 0.3f, 0.34f), TextAnchor.MiddleLeft);
        }

        /// The Doom disk: black plastic, a cream label with the red lettering ("REGISTERED DISK n", n = the slot), a silver shutter with its window.
        void DrawDoomDisk(Rect d, int slot)
        {
            float k = d.width / 200f;
            Color black = new Color(0.14f, 0.14f, 0.16f), red = new Color(0.78f, 0.14f, 0.18f), ink = new Color(0.1f, 0.1f, 0.1f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 10f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y > d.height - ch ? d.width - (y - (d.height - ch)) : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(black, new Color(0.08f, 0.08f, 0.1f), y / d.height));
            }
            Ui.Fill(new Rect(d.x + 2f, d.y, d.width - 4f, 2f), new Color(1f, 1f, 1f, 0.14f));
            Ui.Fill(new Rect(d.x + 8f * k, d.y + 8f * k, 9f * k, 9f * k), new Color(0.04f, 0.04f, 0.06f));      // the two notches at the top
            Ui.Fill(new Rect(d.xMax - 17f * k, d.y + 8f * k, 9f * k, 9f * k), new Color(0.04f, 0.04f, 0.06f));
            var lb = new Rect(d.x + 14f * k, d.y + 12f * k, 172f * k, 104f * k);                                   // the label
            Ui.Fill(lb, new Color(0.93f, 0.9f, 0.82f));
            Ui.Fill(new Rect(lb.x, lb.yMax - 1f, lb.width, 1f), new Color(0f, 0f, 0f, 0.15f));
            Ui.Label(new Rect(lb.x, lb.y + 2f * k, lb.width, 12f * k), "To install, type INSTALL and press ENTER", Mathf.Max(6, Mathf.RoundToInt(6.6f * k)), red, TextAnchor.MiddleCenter);
            int ls = Mathf.RoundToInt(37f * k);                                                                    // the logo: a dark edge under red letters
            Ui.Label(new Rect(lb.x + 2f * k, lb.y + 16f * k, lb.width, 44f * k), "DEMON", ls, new Color(0.45f, 0.06f, 0.08f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(lb.x, lb.y + 14f * k, lb.width, 44f * k), "DEMON", ls, red, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(lb.x, lb.y + 60f * k, lb.width, 14f * k), "REGISTERED", Mathf.RoundToInt(11f * k), ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(lb.x, lb.y + 73f * k, lb.width, 14f * k), "DISK " + (slot + 1), Mathf.RoundToInt(11f * k), ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 88f * k, lb.width - 8f * k, 11f * k), "Shareware Edition      V 1.2", Mathf.Max(6, Mathf.RoundToInt(7f * k)), red, TextAnchor.MiddleCenter);
            var sh = new Rect(d.x + 44f * k, d.y + 128f * k, 82f * k, 66f * k);                                    // the silver shutter and its window
            Ui.Fill(sh, new Color(0.72f, 0.73f, 0.76f));
            Ui.Fill(new Rect(sh.x + 42f * k, sh.y, 40f * k, sh.height), new Color(0.82f, 0.83f, 0.86f));
            Ui.Fill(new Rect(sh.x + 12f * k, sh.y + 4f * k, 26f * k, 48f * k), black);
            Ui.Fill(new Rect(d.xMax - 26f * k, d.y + 150f * k, 10f * k, 20f * k), new Color(0.3f, 0.3f, 0.33f));  // the little arrow
        }

        /// The San Andreas disk: a plain blue floppy with a big paper shutter, a round metal hub and the black slot at the bottom.
        void DrawSanAndreasDisk(Rect d)
        {
            float k = d.width / 200f;
            Color blue = new Color(0.12f, 0.1f, 0.62f), blueHi = new Color(0.2f, 0.18f, 0.74f), blueLo = new Color(0.08f, 0.06f, 0.46f);
            Color black = new Color(0.03f, 0.03f, 0.06f), paper = new Color(0.78f, 0.74f, 0.68f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 12f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(blue, blueLo, y / d.height * 0.5f));
            }
            Ui.Fill(new Rect(d.x, d.y + ch, 6f * k, d.height - ch), blueHi);                     // lit left edge
            Ui.Fill(new Rect(d.xMax - 12f * k, d.y, 12f * k, d.height), blueHi);                  // right-hand rail
            var sh = new Rect(d.x + 36f * k, d.y + 2f * k, 108f * k, 70f * k);                    // the paper shutter and its window
            Ui.Fill(sh, paper);
            Ui.Fill(new Rect(sh.x, sh.yMax - 3f * k, sh.width, 3f * k), new Color(0.55f, 0.52f, 0.48f));
            Ui.Fill(new Rect(sh.x + 22f * k, sh.y + 10f * k, 38f * k, 50f * k), blue);
            var hub = new Rect(d.x + 62f * k, d.y + 82f * k, 76f * k, 76f * k);                   // the metal hub: ring, blue glint, black hole
            Ui.Dot(new Rect(hub.x - 2f * k, hub.y - 2f * k, hub.width + 4f * k, hub.height + 4f * k), new Color(0.08f, 0.07f, 0.4f));
            Ui.Dot(hub, new Color(0.72f, 0.73f, 0.76f));
            Ui.Dot(new Rect(hub.x + 5f * k, hub.y + 5f * k, hub.width - 10f * k, hub.height - 10f * k), new Color(0.62f, 0.63f, 0.67f));
            Ui.Fill(new Rect(hub.x + 42f * k, hub.y + 12f * k, 14f * k, 11f * k), new Color(0.2f, 0.45f, 0.95f));
            Ui.Fill(new Rect(hub.x + 34f * k, hub.y + 38f * k, 10f * k, 10f * k), black);
            Ui.Fill(new Rect(d.x + 28f * k, d.yMax - 42f * k, 134f * k, 36f * k), black);        // the slot at the bottom
            Ui.Fill(new Rect(d.x + 10f * k, d.yMax - 22f * k, 10f * k, 10f * k), black);         // the small hole at the bottom left
        }

        /// A disk: plastic tinted by the rank tier with its chamfered corner, the metal shutter, and a paper label.
        void DrawWhoDisk(Rect d, ProfileInfo p, bool current)
        {
            if (p.Disk == "pac")
            {
                DrawPacDisk(d);
                return;
            }
            if (p.Disk == "holo")
            {
                DrawHoloDisk(d, p);
                return;
            }
            if (p.Disk == "win95")
            {
                DrawWin95Disk(d, Mathf.Max(0, whoList.IndexOf(p)));
                return;
            }
            if (p.Disk == "mi")
            {
                DrawMissionDisk(d);
                return;
            }
            if (p.Disk == "doom")
            {
                DrawDoomDisk(d, Mathf.Max(0, whoList.IndexOf(p)));
                return;
            }
            if (p.Disk == "sa")
            {
                DrawSanAndreasDisk(d);
                return;
            }
            float k = d.width / 200f;
            var tier = RankLook.TierColor(p.Rank);
            Color c1 = Color.Lerp(tier, new Color(0f, 0f, 0f), 0.17f), c2 = Color.Lerp(tier, new Color(0f, 0f, 0f), 0.55f);
            Ui.Pill(new Rect(d.x + 3f, d.y + 12f * k, d.width - 6f, d.height - 8f * k), new Color(0f, 0f, 0f, 0.4f));
            float ch = 24f * k;
            for (float y = 0f; y < d.height; y += 2f)
            {
                float w = y < ch ? d.width - ch + y : d.width;
                Ui.Fill(new Rect(d.x, d.y + y, w, 2.5f), Color.Lerp(c1, c2, y / d.height));
            }
            Ui.Fill(new Rect(d.x + 2f, d.y, d.width - ch - 2f, 2f), new Color(1f, 1f, 1f, 0.24f));
            Ui.Fill(new Rect(d.x, d.y + ch, 2f, d.height - ch), new Color(1f, 1f, 1f, 0.12f));
            Ui.Fill(new Rect(d.x + 10f * k, d.yMax - 24f * k, 14f * k, 14f * k), new Color(0.04f, 0.05f, 0.09f));   // hub hole
            Ui.Fill(new Rect(d.xMax - 26f * k, d.yMax - 26f * k, 16f * k, 16f * k), new Color(0.04f, 0.05f, 0.09f)); // write-protect hole
            Ui.Fill(new Rect(d.xMax - 24f * k, d.yMax - 24f * k, 6f * k, 12f * k), new Color(0.91f, 0.93f, 0.97f));

            var sh = new Rect(d.x + 46f * k, d.y, 108f * k, 60f * k);   // the shutter: four lit columns, the slot, two grooves
            float[] cw = { 0.2f, 0.35f, 0.3f, 0.15f };
            Color[] cc = { new Color(0.56f, 0.6f, 0.71f), new Color(0.89f, 0.91f, 0.96f), new Color(0.7f, 0.74f, 0.84f), new Color(0.5f, 0.54f, 0.65f) };
            float sx = sh.x;
            for (int i = 0; i < 4; i++)
            {
                Ui.Fill(new Rect(sx, sh.y, sh.width * cw[i] + 0.5f, sh.height), cc[i]);
                sx += sh.width * cw[i];
            }
            Ui.Fill(new Rect(sh.x, sh.yMax - 2f, sh.width, 2f), new Color(0f, 0f, 0f, 0.3f));
            Ui.Fill(new Rect(sh.x + 12f * k, sh.y + 22f * k, 36f * k, 3f * k), new Color(0.16f, 0.2f, 0.31f, 0.3f));
            Ui.Fill(new Rect(sh.x + 12f * k, sh.y + 30f * k, 36f * k, 3f * k), new Color(0.16f, 0.2f, 0.31f, 0.3f));
            Ui.Pill(new Rect(sh.xMax - 38f * k, sh.y + 9f * k, 24f * k, 38f * k), new Color(0.04f, 0.055f, 0.1f));

            var lb = new Rect(d.x + 16f * k, d.y + 70f * k, 168f * k, 110f * k);   // the label
            Ui.Fill(lb, new Color(0.96f, 0.94f, 0.88f));
            for (int j = 1; j <= 4; j++)
                Ui.Fill(new Rect(lb.x, lb.y + 17f * k + j * 19f * k, lb.width, 1f), new Color(0.31f, 0.43f, 0.75f, 0.2f));
            Ui.Fill(new Rect(lb.x, lb.y, lb.width, 17f * k), tier);
            int slot = Mathf.Max(0, whoList.IndexOf(p));
            Ui.Label(new Rect(lb.x + 8f * k, lb.y, 80f * k, 17f * k), "SLOT " + (slot + 1), Mathf.Max(8, Mathf.RoundToInt(10f * k)), new Color(0.04f, 0.055f, 0.12f, 0.85f), TextAnchor.MiddleLeft, true);
            var ink = new Color(0.1f, 0.14f, 0.28f);
            string name = p.Name;
            int size = Mathf.RoundToInt(23f * k);
            float nameW = lb.width - 52f * k;
            Ui.HandFont = true;   // Permanent Marker, as if written on the label
            while (size > 11 && Ui.TextWidth(name, size, true) + 8f > nameW)
                size--;
            Ui.Label(new Rect(lb.x + 6f * k, lb.y + 19f * k, nameW + 4f, 30f * k), name, size, ink, TextAnchor.MiddleLeft, true);
            Ui.HandFont = false;
            RankLook.Draw(new Rect(lb.xMax - 46f * k, lb.y + 20f * k, 42f * k, 42f * k), p.Rank);
            Ui.Label(new Rect(lb.x + 6f * k, lb.y + 58f * k, lb.width - 8f * k, 20f * k), Rank.Title(p.Rank) + "  ·  Rank " + p.Rank, Mathf.RoundToInt(13f * k), new Color(0.16f, 0.21f, 0.4f), TextAnchor.MiddleLeft, true);
            if (!whoManage)
                Ui.Label(new Rect(lb.x + 6f * k, lb.y + 77f * k, lb.width - 8f * k, 18f * k), p.Wins + (p.Wins == 1 ? " win" : " wins") + "  ·  " + p.Runs + (p.Runs == 1 ? " run" : " runs"), Mathf.RoundToInt(11.5f * k), new Color(0.35f, 0.4f, 0.56f), TextAnchor.MiddleLeft);
            if (current)   // the profile you used last gets a red stamp
            {
                var st = new Rect(lb.xMax - 66f * k, lb.yMax - 21f * k, 60f * k, 15f * k);
                var red = new Color(0.7f, 0.15f, 0.12f);
                Ui.Fill(new Rect(st.x, st.y, st.width, 1.5f), red);
                Ui.Fill(new Rect(st.x, st.yMax - 1.5f, st.width, 1.5f), red);
                Ui.Fill(new Rect(st.x, st.y, 1.5f, st.height), red);
                Ui.Fill(new Rect(st.xMax - 1.5f, st.y, 1.5f, st.height), red);
                Ui.Label(st, "LAST USED", Mathf.Max(8, Mathf.RoundToInt(9f * k)), red, TextAnchor.MiddleCenter, true);
            }
        }

        void DrawWhoCard(Rect r, ProfileInfo p, float t, bool live)
        {
            int slot = Mathf.Clamp(whoList.IndexOf(p), 0, WhoSlots);
            bool hover = live && !whoManage && r.Contains(Event.current.mousePosition);
            bool current = p.Id == ProfileStore.ActiveId;
            if (Event.current.type == EventType.Repaint)
                whoLift[slot] = Mathf.MoveTowards(whoLift[slot], hover ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float lift = live && !whoManage ? whoLift[slot] : 0f;
            var d = new Rect(r.x, r.y - 12f * lift, r.width, r.height);
            bool graphite = Ui.Graphite;
            Ui.Graphite = false;  // the disks keep their own colours
            DrawWhoDisk(d, p, current);
            Ui.Graphite = graphite;
            float sel = whoManage ? 0f : whoLift[slot];  // selected (under the mouse, or picked and lifting): a neon border round the disk (MenuHolo.cs)
            if (sel > 0.01f && BeginUiGl())
            {
                UiModelAt(V2.Zero, 1f);
                NeonRect(new Rect(d.x - 6f, d.y - 6f, d.width + 12f, d.height + 12f), sel);
                EndUiGl();
            }
            if (whoManage && live)
            {
                float by = WhoShelfTop + 54f + 22f, bx = r.center.x - 78f;
                if (DkKey(new Rect(bx, by, 74f, 26f), "Rename", Ui.Neutral, false, 13))
                {
                    whoEdit = p.Id;
                    whoName = p.Name;
                    whoError = "";
                    whoFocus = true;
                }
                bool armed = whoArmed == p.Id;
                GUI.enabled = whoList.Count > 1;
                if (DkKey(new Rect(bx + 82f, by, 74f, 26f), armed ? "Sure?" : "Delete", armed ? CrtStyle.Danger : Ui.Neutral, false, 13))
                {
                    if (!armed)
                        whoArmed = p.Id;
                    else
                    {
                        ProfileStore.Delete(p.Id);
                        whoArmed = null;
                        savesCountT = 0f;
                        WhoRefresh();
                    }
                }
                GUI.enabled = true;
            }
            else if (live && Ui.HitArea(r))
                ChooseProfile(p.Id);
        }

        /// An unused slot: a dashed disk outline that says so. It can't be clicked; New profile fills the first one.
        void DrawWhoEmptyCard(Rect r)
        {
            DrawWhoHolo(r, 0.8f, 0f);  // a hologram disk (MenuHolo.cs)
            Ui.Label(new Rect(r.x, r.y + r.height / 2f - 14f, r.width, 28f), "Empty", 18, new Color(0.56f, 0.66f, 0.88f, 0.85f), TextAnchor.MiddleCenter, true);
        }

        /// The fourth box: a dashed disk. With all slots full it is grayed out and does nothing.
        void DrawWhoNewCard(Rect r, bool live)
        {
            bool full = WhoFull;
            bool hover = live && !full && r.Contains(Event.current.mousePosition);
            var dim = new Color(1f, 1f, 1f, full ? 0.25f : 1f);
            DrawWhoHolo(r, full ? 0.35f : 1f, hover ? 1f : 0f);  // a hologram disk (MenuHolo.cs)
            // the + pulses (a glow behind it swelling), unless the slots are full
            float pulse = full || fxReduced || !fxOn ? 0f : 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.9f);
            var plusR = new Rect(r.x, r.y + r.height / 2f - 50f, r.width, 60f);
            Ui.Label(plusR, "+", 44 + Mathf.RoundToInt(5f * pulse), full ? dim : hover ? Color.white : new Color(0.84f, 0.89f, 1f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + r.height / 2f + 8f, r.width, 24f), "New profile", 17, full ? dim : hover ? Color.white : new Color(0.66f, 0.75f, 0.94f), TextAnchor.MiddleCenter, true);
            if (full)
                Ui.Label(new Rect(r.x, r.y + r.height / 2f + 32f, r.width, 18f), "All " + WhoSlots + " slots used", 11, dim, TextAnchor.MiddleCenter);
            if (live && !full && Ui.HitArea(r))
            {
                whoEdit = "new";
                whoName = "";
                whoError = "";
                whoFocus = true;
            }
        }

        void DrawWhoEditor(Rect r)
        {
            DkPanel(r);
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 24f), whoEdit == "new" ? "New profile name" : "New name", 14, Ui.Muted, TextAnchor.MiddleLeft, true);
            var field = new Rect(r.x + 16f, r.y + 34f, r.width - 32f, 36f);
            GUI.SetNextControlName("who_name");
            whoName = DkField(field, whoName, ProfileStore.MaxNameLength);
            if (whoFocus && Event.current.type == EventType.Repaint)
            {
                GUI.FocusControl("who_name");
                whoFocus = false;
            }
            bool enter = Event.current.type == EventType.KeyDown && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter) && GUI.GetNameOfFocusedControl() == "who_name";
            if (enter)
                Event.current.Use();
            bool canCancel = whoList.Count > 0;
            float bw = 100f, bx = r.xMax - 16f - bw;
            if ((DkKey(new Rect(bx, r.y + 76f, bw, 32f), "Save", Ui.Primary, true, 15) || enter))
                WhoCommitEdit();
            if (canCancel && DkKey(new Rect(bx - bw - 8f, r.y + 76f, bw, 32f), "Cancel", Ui.Primary, false, 15))
            {
                whoEdit = null;
                whoError = "";
            }
            if (whoError.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 76f, bx - bw - r.x - 40f, 32f), whoError, 13, Ui.Warn, TextAnchor.MiddleLeft, false, true);
        }
    }
}

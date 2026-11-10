using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Profiles.md, "Shelf 4" of Docs/Mockups/whos-playing-shelves.html, mechanical clamp cradles and a marker-pen name from Docs/Mockups/whos-playing-mechanical.html): a screen of its own before the main
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

        const float WhoCardW = 184f, WhoCardH = 184f, WhoCardGap = 60f, WhoCardTop = 252f;  // a disk's box: square, standing on the shelf (DrawWhoShelf)
        const int WhoSlots = ProfileStore.MaxProfiles;  // profile slots; the New profile box is the box after them

        /// Where box i sits (0 to 2: the slots; 3: New profile). Always four, so the layout never changes.
        static Rect WhoBox(int i)
        {
            float rowW = (WhoSlots + 1) * WhoCardW + WhoSlots * WhoCardGap;
            return new Rect(W / 2f - rowW / 2f + i * (WhoCardW + WhoCardGap), WhoCardTop, WhoCardW, WhoCardH);
        }

        bool WhoFull => whoList.Count >= WhoSlots;
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
            Ui.Glitch(new Rect(0f, MenuTitleY - 56f, W, 80f), "WHO'S PLAYING?", 58, Color.white, TextAnchor.MiddleCenter, Ui.Accent);
            DrawDriveSlot(1f, DriveEmpty());  // the drive your disk goes into (DriveSlot.cs)

            DrawWhoShelf();
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
                var kr = new Rect(W / 2f - 150f, H - 118f, 300f, 74f);
                if (MenuVhs.Draw(kr, "TRK 03", true, fxReduced, () => KeyUi.Key(kr, whoManage ? "Done" : "Manage profiles", PixelArt.Get("ui_gear"), 26, whoManage)))
                {
                    whoManage = !whoManage;
                    whoArmed = null;
                    whoError = "";
                }
            }
        }

        // ---- the shelf and the disks (look of "Shelf 4, Display shelf" of Docs/Mockups/whos-playing-shelves.html) ----

        readonly float[] whoLift = new float[WhoSlots + 1];  // how far each disk is lifted by the mouse (0..1, eased; opens its clamps)

        /// Where the shelf's top edge is: the disks stand on their pedestals just above it.
        const float WhoShelfTop = WhoCardTop + WhoCardH + 16f;

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
        static void WhoClamp(Rect r, bool lit)
        {
            Ui.Pill(new Rect(r.x + 1f, r.y + 4f, r.width, r.height), new Color(0f, 0f, 0f, 0.4f));
            Ui.Pill(r, new Color(0.47f, 0.58f, 0.88f));
            var inner = new Rect(r.x + 1.5f, r.y + 1.5f, r.width - 3f, r.height - 3f);
            Ui.Pill(inner, new Color(0.15f, 0.2f, 0.44f));
            Ui.Fill(new Rect(inner.x + inner.width * 0.28f, inner.y + 3f, inner.width * 0.34f, inner.height - 6f), new Color(0.45f, 0.55f, 0.82f, 0.75f));
            var lamp = new Rect(r.center.x - 5f, r.y + 9f, 10f, 10f);
            if (lit)
            {
                Ui.Dot(new Rect(lamp.x - 4f, lamp.y - 4f, 18f, 18f), new Color(0.42f, 1f, 0.6f, 0.22f));
                Ui.Dot(lamp, new Color(0.42f, 1f, 0.6f));
            }
            else
                Ui.Dot(lamp, new Color(0.07f, 0.1f, 0.25f));
            KeyUi.Bolt(new Vector2(r.center.x, r.yMax - 12f), 8f);
        }

        /// The wall, the shelf, and under every disk its cradle: a pedestal with a clamp post either side, which open as the
        /// disk lifts (look "Mechanical 2, Clamp cradles" of Docs/Mockups/whos-playing-mechanical.html). Drawn under the disks.
        void DrawWhoShelf()
        {
            float top = WhoShelfTop, left = 70f, w = W - 140f;
            var wall = new Rect(left + 20f, WhoCardTop - 8f, w - 40f, top - WhoCardTop + 8f + 54f);
            Ui.Pill(new Rect(wall.x, wall.y + 4f, wall.width, wall.height), new Color(0f, 0f, 0f, 0.35f));
            Ui.Pill(wall, new Color(0.17f, 0.23f, 0.5f));
            WhoBand(new Rect(wall.x + 1f, wall.y + 1f, wall.width - 2f, wall.height - 2f), new Color(0.06f, 0.08f, 0.2f), new Color(0.03f, 0.04f, 0.1f), 6f);
            Ui.Rim(new Rect(wall.x + 5f, wall.y + 5f, wall.width - 10f, wall.height - 10f), new Color(0.11f, 0.16f, 0.35f), 0.7f);
            foreach (var c in new[] { new Vector2(wall.x + 12f, wall.y + 12f), new Vector2(wall.xMax - 12f, wall.y + 12f), new Vector2(wall.x + 12f, wall.yMax - 12f), new Vector2(wall.xMax - 12f, wall.yMax - 12f) })
                KeyUi.Bolt(c, 9f);

            // the shelf: a lit top edge, then the front face with a vent at each end and a plate for each slot
            Ui.Pill(new Rect(left, top + 12f, w, 48f), new Color(0f, 0f, 0f, 0.35f));
            WhoBand(new Rect(left, top, w, 14f), new Color(0.53f, 0.63f, 0.93f), new Color(0.27f, 0.35f, 0.6f), 2f);
            Ui.Fill(new Rect(left + 3f, top, w - 6f, 1f), new Color(1f, 1f, 1f, 0.45f));
            WhoBand(new Rect(left, top + 14f, w, 40f), new Color(0.24f, 0.3f, 0.55f), new Color(0.1f, 0.14f, 0.32f), 3f);
            Ui.Rim(new Rect(left, top, w, 54f), new Color(0.42f, 0.52f, 0.84f), 0.6f);
            WhoVent(new Rect(left + 20f, top + 22f, 100f, 24f));
            WhoVent(new Rect(left + w - 120f, top + 22f, 100f, 24f));

            for (int i = 0; i <= WhoSlots; i++)
            {
                var r = WhoBox(i);
                var info = i < WhoSlots ? WhoInSlot(i) : null;
                bool lit = info != null && info.Id == ProfileStore.ActiveId;
                float open = whoManage ? 0f : whoLift[i] * 9f, k = r.width / 200f;
                // the pedestal under the disk
                var ped = new Rect(r.x - 18f * k, r.yMax - 4f * k, r.width + 36f * k, 20f * k);
                Ui.Pill(new Rect(ped.x + 2f, ped.y + 6f, ped.width - 4f, ped.height), new Color(0f, 0f, 0f, 0.45f));
                Ui.Pill(ped, new Color(0.47f, 0.58f, 0.88f));
                WhoBand(new Rect(ped.x + 1.5f, ped.y + 1.5f, ped.width - 3f, ped.height - 3f), new Color(0.32f, 0.41f, 0.71f), new Color(0.1f, 0.14f, 0.31f), 2f);
                Ui.Fill(new Rect(ped.x + 4f, ped.y + 1f, ped.width - 8f, 1f), new Color(1f, 1f, 1f, 0.3f));
                for (float gx = ped.x + 14f * k; gx < ped.xMax - 24f * k; gx += 16f * k)
                    Ui.Fill(new Rect(gx, ped.y + 8f * k, 10f * k, 3f), new Color(0.04f, 0.055f, 0.13f));
                // the clamps, either side of the disk
                float ch = 84f * k, cw = 22f * k;
                WhoClamp(new Rect(r.x - 28f * k - open, r.yMax - ch + 6f * k, cw, ch), lit);
                WhoClamp(new Rect(r.xMax + 6f * k + open, r.yMax - ch + 6f * k, cw, ch), lit);
                // the slot's plate on the shelf face
                var plate = new Rect(r.center.x - 46f, top + 20f, 92f, 28f);
                Ui.Pill(plate, new Color(0.03f, 0.045f, 0.12f));
                if (!whoManage)
                    Ui.Label(plate, i == WhoSlots ? "NEW" : "SLOT " + (i + 1), 14, new Color(0.66f, 0.73f, 0.94f), TextAnchor.MiddleCenter, true);
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

        /// Easter egg: a name like "San Andreas", "SanAndreas", "san_andreas", "GTA:SA", "GTASA" or "SA" (case, spaces and punctuation ignored).
        public static bool IsSanAndreasName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            string n = sb.ToString();
            return n == "sa" || n == "gtasa" || n.Contains("sanandreas");
        }

        /// Easter egg: any name with "doom" in it (Doom, DOOM, doom 2, Doomguy, ...): the retail Doom install disk.
        public static bool IsDoomName(string name)
        {
            if (string.IsNullOrEmpty(name))
                return false;
            var sb = new System.Text.StringBuilder();
            foreach (char ch in name)
                if (char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString().Contains("doom");
        }

        /// The Doom disk: black plastic, a cream label with the red logo ("REGISTERED DISK n", n = the slot), a silver shutter with its window.
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
            Ui.Label(new Rect(lb.x, lb.y + 2f * k, lb.width, 12f * k), "To install DOOM, type INSTALL and press ENTER", Mathf.Max(6, Mathf.RoundToInt(6.6f * k)), red, TextAnchor.MiddleCenter);
            int ls = Mathf.RoundToInt(46f * k);                                                                    // the logo: a dark edge under red letters
            Ui.Label(new Rect(lb.x + 2f * k, lb.y + 16f * k, lb.width, 44f * k), "DOOM", ls, new Color(0.45f, 0.06f, 0.08f), TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(lb.x, lb.y + 14f * k, lb.width, 44f * k), "DOOM", ls, red, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(lb.x, lb.y + 60f * k, lb.width, 14f * k), "REGISTERED", Mathf.RoundToInt(11f * k), ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(lb.x, lb.y + 73f * k, lb.width, 14f * k), "DISK " + (slot + 1), Mathf.RoundToInt(11f * k), ink, TextAnchor.MiddleCenter);
            Ui.Label(new Rect(lb.x + 4f * k, lb.y + 88f * k, lb.width - 8f * k, 11f * k), "Created by id Software      V 1.2", Mathf.Max(6, Mathf.RoundToInt(7f * k)), red, TextAnchor.MiddleCenter);
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
            if (IsDoomName(p.Name))
            {
                DrawDoomDisk(d, Mathf.Max(0, whoList.IndexOf(p)));
                return;
            }
            if (IsSanAndreasName(p.Name))
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
            var tier = RankLook.TierColor(p.Rank);
            int slot = Mathf.Clamp(whoList.IndexOf(p), 0, WhoSlots);
            bool hover = live && !whoManage && r.Contains(Event.current.mousePosition);
            bool current = p.Id == ProfileStore.ActiveId;
            if (Event.current.type == EventType.Repaint)
                whoLift[slot] = Mathf.MoveTowards(whoLift[slot], hover ? 1f : 0f, Time.unscaledDeltaTime * 7f);
            float lift = live && !whoManage ? whoLift[slot] : 0f;
            var d = new Rect(r.x, r.y - 12f * lift, r.width, r.height);
            DrawWhoDisk(d, p, current);
            if (current || hover)  // a soft glow of the tier colour round the disk
                Ui.Rim(new Rect(d.x - 4f, d.y - 4f, d.width + 8f, d.height + 8f), tier, hover ? 0.28f : 0.14f);
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
                if (DkKey(new Rect(bx + 82f, by, 74f, 26f), armed ? "Sure?" : "Delete", armed ? Ui.Danger : Ui.Neutral, false, 13))
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
            DrawWhoGhost(r, new Color(0.6f, 0.68f, 0.9f, 0.2f));
            Ui.Label(new Rect(r.x, r.y + r.height / 2f - 14f, r.width, 28f), "Empty", 17, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, true);
        }

        /// The fourth box: a dashed disk. With all slots full it is grayed out and does nothing.
        void DrawWhoNewCard(Rect r, bool live)
        {
            bool full = WhoFull;
            bool hover = live && !full && r.Contains(Event.current.mousePosition);
            var dim = new Color(1f, 1f, 1f, full ? 0.25f : 1f);
            DrawWhoGhost(r, hover ? new Color(0.54f, 0.64f, 1f, 0.95f) : new Color(0.6f, 0.68f, 0.9f, full ? 0.2f : 0.4f));
            if (hover)
                Ui.Fill(new Rect(r.x + 4f, r.y + 26f, r.width - 8f, r.height - 30f), new Color(1f, 1f, 1f, 0.04f));
            Ui.Label(new Rect(r.x, r.y + r.height / 2f - 50f, r.width, 60f), "+", 44, full ? dim : hover ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + r.height / 2f + 8f, r.width, 24f), "New profile", 15, full ? dim : hover ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
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

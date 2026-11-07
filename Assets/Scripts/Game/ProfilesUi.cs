using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Profiles.md, "Shelf 4" of Docs/Mockups/whos-playing-shelves.html): a screen of its own before the main
    /// menu on every launch, a floppy disk per profile standing on a wall shelf under the menu's title axis. Click one to play as it; Manage shows Rename and
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

        const float WhoCardW = 184f, WhoCardH = 184f, WhoCardGap = 40f, WhoCardTop = 262f;  // a disk's box: square, standing on the shelf (DrawWhoShelf)
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
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, MenuAxisY), MenuAxisHalf, t);
                EndUiGl();
            }

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

            float y = WhoCardTop + WhoCardH + 70f;  // (under the shelf)
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

        static Texture2D whoPeg;
        readonly float[] whoLift = new float[WhoSlots + 1];  // how far each disk is lifted by the mouse (0..1, eased)

        /// One tile of the pegboard: a dim hole in a dark field (tiled over the wall behind the shelf).
        static Texture2D WhoPegTex()
        {
            if (whoPeg != null)
                return whoPeg;
            const int n = 26;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave };
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float d = Mathf.Sqrt((x - n / 2f + 0.5f) * (x - n / 2f + 0.5f) + (y - n / 2f + 0.5f) * (y - n / 2f + 0.5f));
                    px[y * n + x] = new Color32(41, 56, 107, (byte)(255f * Mathf.Clamp01(1.9f - d)));
                }
            tex.SetPixels32(px);
            tex.Apply();
            whoPeg = tex;
            return tex;
        }

        /// A vertical gradient drawn as thin strips (the game's boxes are flat, so depth is built from strips).
        static void WhoBand(Rect r, Color top, Color bottom, float step = 3f)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(r.height / step));
            float h = r.height / n;
            for (int i = 0; i < n; i++)
                Ui.Fill(new Rect(r.x, r.y + i * h, r.width, h + 0.5f), Color.Lerp(top, bottom, (i + 0.5f) / n));
        }

        /// The wall, the shelf and its slot plates, drawn under the disks. The disks stand on the shelf's top.
        void DrawWhoShelf()
        {
            float top = WhoCardTop + WhoCardH, left = 80f, w = W - 160f;
            var peg = new Rect(left + 20f, WhoCardTop - 42f, w - 40f, WhoCardH + 92f);
            Ui.Pill(peg, new Color(0.03f, 0.045f, 0.12f, 0.9f));
            var old = GUI.color;
            GUI.color = Ui.Faded(Color.white);
            var inner = new Rect(peg.x + 4f, peg.y + 4f, peg.width - 8f, peg.height - 8f);
            GUI.DrawTextureWithTexCoords(inner, WhoPegTex(), new Rect(0f, 0f, inner.width / 26f, inner.height / 26f));
            GUI.color = old;
            Ui.Rim(peg, new Color(0.14f, 0.2f, 0.45f), 0.7f);
            // brackets under the shelf
            foreach (float bx in new[] { left + 70f, left + w - 98f })
                for (int i = 0; i < 16; i++)
                    Ui.Fill(new Rect(bx, top + 46f + i * 3f, 28f, 3.5f), Color.Lerp(new Color(0.17f, 0.22f, 0.43f), new Color(0.08f, 0.1f, 0.25f), i / 15f));
            // the shelf: a lit top edge, then the front face
            Ui.Pill(new Rect(left, top + 14f, w, 40f), new Color(0f, 0f, 0f, 0.35f));
            WhoBand(new Rect(left, top - 1f, w, 12f), new Color(0.5f, 0.59f, 0.89f), new Color(0.26f, 0.35f, 0.6f), 2f);
            WhoBand(new Rect(left, top + 11f, w, 34f), new Color(0.23f, 0.29f, 0.53f), new Color(0.1f, 0.14f, 0.31f), 3f);
            Ui.Fill(new Rect(left, top + 11f, w, 1f), new Color(1f, 1f, 1f, 0.22f));
            Ui.Rim(new Rect(left, top - 1f, w, 46f), new Color(0.36f, 0.47f, 0.78f), 0.6f);
            for (int i = 0; i <= WhoSlots; i++)
            {
                var r = WhoBox(i);
                var plate = new Rect(r.center.x - 35f, top + 17f, 70f, 22f);
                Ui.Pill(plate, new Color(0.03f, 0.045f, 0.12f));
                if (!whoManage)
                    Ui.Label(plate, i == WhoSlots ? "NEW" : "SLOT " + (i + 1), 12, new Color(0.62f, 0.7f, 0.92f), TextAnchor.MiddleCenter, true);
            }
        }

        /// A spotlight on a disk: a cone from above that brightens when the mouse is on it.
        static void DrawWhoCone(Rect disk, Color tier, float a)
        {
            float y0 = disk.y - 40f, y1 = disk.yMax;
            for (float y = y0; y < y1; y += 4f)
            {
                float f = (y - y0) / (y1 - y0), half = Mathf.Lerp(18f, disk.width * 0.68f, f);
                Ui.Fill(new Rect(disk.center.x - half, y, half * 2f, 4.5f), new Color(tier.r, tier.g, tier.b, a * (1f - f * 0.8f)));
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

        /// A disk: plastic tinted by the rank tier with its chamfered corner, the metal shutter, and a paper label.
        void DrawWhoDisk(Rect d, ProfileInfo p, bool current)
        {
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
            int size = Mathf.RoundToInt(22f * k);
            float nameW = lb.width - 52f * k;
            while (size > 11 && Ui.TextWidth(name, size, true) + 8f > nameW)
                size--;
            Ui.Label(new Rect(lb.x + 6f * k, lb.y + 19f * k, nameW + 4f, 30f * k), name, size, ink, TextAnchor.MiddleLeft, true);
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
            DrawWhoCone(r, tier, 0.05f + (current ? 0.06f : 0f) + 0.2f * lift);
            var d = new Rect(r.x, r.y - 12f * lift, r.width, r.height);
            DrawWhoDisk(d, p, current);
            if (current || hover)  // a soft glow of the tier colour round the disk
                Ui.Rim(new Rect(d.x - 4f, d.y - 4f, d.width + 8f, d.height + 8f), tier, hover ? 0.28f : 0.14f);
            if (whoManage && live)
            {
                float by = r.yMax + 46f + 14f, bx = r.center.x - 78f;
                if (Ui.SmallButton(new Rect(bx, by, 74f, 26f), "Rename", Ui.Neutral, 13))
                {
                    whoEdit = p.Id;
                    whoName = p.Name;
                    whoError = "";
                    whoFocus = true;
                }
                bool armed = whoArmed == p.Id;
                GUI.enabled = whoList.Count > 1;
                if (Ui.SmallButton(new Rect(bx + 82f, by, 74f, 26f), armed ? "Sure?" : "Delete", armed ? Ui.Danger : Ui.Neutral, 13))
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
            Ui.Panel(r, true);
            Ui.Label(new Rect(r.x + 16f, r.y + 8f, r.width - 32f, 24f), whoEdit == "new" ? "New profile name" : "New name", 14, Ui.Muted, TextAnchor.MiddleLeft, true);
            var field = new Rect(r.x + 16f, r.y + 34f, r.width - 32f, 36f);
            GUI.SetNextControlName("who_name");
            whoName = Ui.Field(field, whoName, ProfileStore.MaxNameLength);
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
            if ((Ui.ActionButton(new Rect(bx, r.y + 76f, bw, 32f), "Save", true, Ui.Primary, null, null, 15) || enter))
                WhoCommitEdit();
            if (canCancel && Ui.ActionButton(new Rect(bx - bw - 8f, r.y + 76f, bw, 32f), "Cancel", false, Ui.Primary, null, null, 15))
            {
                whoEdit = null;
                whoError = "";
            }
            if (whoError.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 76f, bx - bw - r.x - 40f, 32f), whoError, 13, Ui.Warn, TextAnchor.MiddleLeft, false, true);
        }
    }
}

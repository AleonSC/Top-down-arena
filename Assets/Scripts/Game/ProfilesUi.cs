using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Profiles on "Who's playing?" (Docs/Profiles.md): the screen's state, choosing, naming and renaming a profile, and the disks'
    /// art (each profile's floppy disk, with its easter-egg looks). The screen itself is the disk carousel (DiskCarousel.cs); choosing
    /// a disk plays the boot sequence to the main menu (ProfileTransition.cs). Up to ProfileStore.MaxProfiles (50) profiles.
    /// Choosing a profile makes it the active one (ProfileStore.cs): its records, rank, saved runs and name.
    public partial class ArenaApp
    {
        bool whoOpen;                 // the screen is up (set at launch; the menu's intro waits for it to close)
        bool whoManage, whoFocus, whoAutoNew;
        string whoEdit;               // "new", a profile's id (renaming it), or null
        string whoName = "", whoError = "", whoArmed;
        List<ProfileInfo> whoList = new List<ProfileInfo>();
        bool profileChosen;           // a profile was picked on this screen: the name field now edits its name

        void OpenWho()
        {
            whoOpen = true;
            whoManage = false;
            whoEdit = null;
            whoError = "";
            whoArmed = null;
            whoAutoNew = false;
            WhoRefresh();
            CarCenterOn(ProfileStore.ActiveId);
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
                CrtKit.Frame(st, 1.5f, red);
                Ui.Label(st, "LAST USED", Mathf.Max(8, Mathf.RoundToInt(9f * k)), red, TextAnchor.MiddleCenter, true);
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
                CancelWhoEdit();
            if (whoError.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 76f, bx - bw - r.x - 40f, 32f), whoError, 13, Ui.Warn, TextAnchor.MiddleLeft, false, true);
        }
    }
}

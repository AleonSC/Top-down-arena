using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Profiles.md, look C of Docs/Mockups/whos-playing.html): a screen of its own before the main
    /// menu on every launch, a card per profile on the menu's title axis. Click one to play as it; Manage shows Rename and
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

        const float WhoCardW = 172f, WhoCardH = 206f, WhoCardGap = 18f, WhoCardTop = 250f;
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
            Ui.Fancy(new Rect(0f, MenuTitleY - 56f, W, 80f), "WHO'S PLAYING?", 58, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleCenter, Ui.Accent);
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, MenuAxisY), MenuAxisHalf, t);
                EndUiGl();
            }

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

            float y = WhoCardTop + WhoCardH + WhoCardGap;
            if (editing)
                DrawWhoEditor(new Rect(W / 2f - 250f, Mathf.Min(y, H - 160f), 500f, 118f));
            else if (whoError.Length > 0)
                Ui.Label(new Rect(W / 2f - 250f, y, 500f, 24f), whoError, 14, Ui.Warn, TextAnchor.MiddleCenter);
            if (!editing && whoList.Count > 0 && Ui.ActionButton(new Rect(W / 2f - 80f, H - 74f, 160f, 36f), whoManage ? "Done" : "Manage profiles", whoManage, Ui.Primary, null, null, 15))
            {
                whoManage = !whoManage;
                whoArmed = null;
                whoError = "";
            }
        }

        void DrawWhoCard(Rect r, ProfileInfo p, float t, bool live)
        {
            var tier = RankLook.TierColor(p.Rank);
            bool hover = live && !whoManage && r.Contains(Event.current.mousePosition);
            bool current = p.Id == ProfileStore.ActiveId;
            Ui.GlowPanel(r, tier, hover ? 0.34f : current ? 0.2f : 0.12f, hover ? 1f : current ? 0.9f : 0.6f);
            if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.05f));
            DrawWhoCardBody(r, p);
            if (whoManage && live)
            {
                float by = r.yMax - 34f;
                if (Ui.SmallButton(new Rect(r.x + 8f, by, 74f, 26f), "Rename", Ui.Neutral, 13))
                {
                    whoEdit = p.Id;
                    whoName = p.Name;
                    whoError = "";
                    whoFocus = true;
                }
                bool armed = whoArmed == p.Id;
                GUI.enabled = whoList.Count > 1;
                if (Ui.SmallButton(new Rect(r.xMax - 82f, by, 74f, 26f), armed ? "Sure?" : "Delete", armed ? Ui.Danger : Ui.Neutral, 13))
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

        /// What is on a profile's box: its rank badge, name, title and rank, and its wins and runs (Manage hides the last).
        void DrawWhoCardBody(Rect r, ProfileInfo p)
        {
            var tier = RankLook.TierColor(p.Rank);
            RankLook.Draw(new Rect(r.center.x - 40f, r.y + 16f, 80f, 80f), p.Rank);
            string name = p.Name;
            int size = 19;
            while (size > 11 && Ui.TextWidth(name, size, true) + 10f > r.width)
                size--;
            Ui.Label(new Rect(r.x + 4f, r.y + 104f, r.width - 8f, 26f), name, size, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 130f, r.width, 18f), Rank.Title(p.Rank) + "  ·  Rank " + p.Rank, 12, tier, TextAnchor.MiddleCenter, true);
            if (!whoManage)  // (Manage shows Rename and Delete in its place)
                Ui.Label(new Rect(r.x, r.y + 148f, r.width, 16f), p.Wins + (p.Wins == 1 ? " win" : " wins") + "  ·  " + p.Runs + (p.Runs == 1 ? " run" : " runs"), 11, Ui.Muted, TextAnchor.MiddleCenter);
        }

        /// An unused slot: a dim box that says so. It can't be clicked; New profile fills the first one.
        void DrawWhoEmptyCard(Rect r)
        {
            Ui.GlowPanel(r, Ui.Muted, 0.04f, 0.25f);
            Ui.Label(new Rect(r.x, r.y + r.height / 2f - 14f, r.width, 28f), "Empty", 17, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleCenter, true);
        }

        /// The fourth box. With all slots full it is grayed out and does nothing.
        void DrawWhoNewCard(Rect r, bool live)
        {
            bool full = WhoFull;
            bool hover = live && !full && r.Contains(Event.current.mousePosition);
            var dim = new Color(1f, 1f, 1f, full ? 0.25f : 1f);
            Ui.GlowPanel(r, Ui.Muted, full ? 0.03f : hover ? 0.24f : 0.08f, full ? 0.2f : hover ? 0.95f : 0.4f);
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

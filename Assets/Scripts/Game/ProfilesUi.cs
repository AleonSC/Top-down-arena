using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// "Who's playing?" (Docs/Profiles.md, look C of Docs/Mockups/whos-playing.html): a screen of its own before the main
    /// menu on every launch, a card per profile on the menu's title axis. Click one to play as it; Manage shows Rename and
    /// Delete (Delete asks twice); the + card makes a new one. The main menu's "Switch profile" opens it again.
    /// Choosing a profile makes it the active one (ProfileStore.cs): its records, rank, saved runs and name.
    public partial class ArenaApp
    {
        bool whoOpen;                 // the screen is up (set at launch; the menu's intro waits for it to close)
        bool whoManage, whoFocus, whoAutoNew;
        string whoEdit;               // "new", a profile's id (renaming it), or null
        string whoName = "", whoError = "", whoArmed;
        List<ProfileInfo> whoList = new List<ProfileInfo>();
        bool profileChosen;           // a profile was picked on this screen: the name field now edits its name

        const float WhoCardW = 150f;

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
            whoOpen = false;
            whoEdit = null;
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

            int n = whoList.Count + (whoList.Count < ProfileStore.MaxProfiles ? 1 : 0);
            int perRow = Mathf.Min(n, 5), rows = (n + 4) / 5;
            float ch = rows == 1 ? 206f : 184f, gap = 18f, top = rows == 1 ? 250f : 232f;
            for (int i = 0; i < n; i++)
            {
                int row = i / 5, inRow = Mathf.Min(5, n - row * 5);
                float rowW = inRow * WhoCardW + (inRow - 1) * gap;
                var r = new Rect(W / 2f - rowW / 2f + (i % 5) * (WhoCardW + gap), top + row * (ch + gap), WhoCardW, ch);
                if (i < whoList.Count)
                    DrawWhoCard(r, whoList[i], t, !editing);
                else
                    DrawWhoNewCard(r, !editing && !whoManage);
            }

            float y = top + rows * (ch + gap);
            if (editing)
                DrawWhoEditor(new Rect(W / 2f - 250f, Mathf.Min(y, H - 160f), 500f, 118f));
            else if (whoError.Length > 0)
                Ui.Label(new Rect(W / 2f - 250f, y, 500f, 24f), whoError, 14, Ui.Warn, TextAnchor.MiddleCenter);
            if (!editing && whoList.Count > 0 && Ui.Button(new Rect(W / 2f - 80f, H - 74f, 160f, 36f), whoManage ? "Done" : "Manage profiles", whoManage ? Ui.Primary : Ui.Neutral))
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
            Ui.Card(r, tier, hover ? 1f : current ? 0.85f : 0.55f);
            if (hover)
                Ui.Pill(r, new Color(1f, 1f, 1f, 0.05f));
            RankLook.Draw(new Rect(r.center.x - 40f, r.y + 16f, 80f, 80f), p.Rank);
            string name = p.Name;
            int size = 19;
            while (size > 11 && Ui.TextWidth(name, size, true) + 10f > r.width)
                size--;
            Ui.Label(new Rect(r.x + 4f, r.y + 104f, r.width - 8f, 26f), name, size, Color.white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 130f, r.width, 18f), Rank.Title(p.Rank) + "  ·  Rank " + p.Rank, 12, tier, TextAnchor.MiddleCenter, true);
            if (!whoManage)  // (Manage shows Rename and Delete in its place)
                Ui.Label(new Rect(r.x, r.y + 148f, r.width, 16f), p.Wins + (p.Wins == 1 ? " win" : " wins") + "  ·  " + p.Runs + (p.Runs == 1 ? " run" : " runs"), 11, Ui.Muted, TextAnchor.MiddleCenter);
            if (whoManage && live)
            {
                float by = r.yMax - 34f;
                if (Ui.Button(new Rect(r.x + 8f, by, 64f, 26f), "Rename", Ui.Neutral))
                {
                    whoEdit = p.Id;
                    whoName = p.Name;
                    whoError = "";
                    whoFocus = true;
                }
                bool armed = whoArmed == p.Id;
                GUI.enabled = whoList.Count > 1;
                if (Ui.Button(new Rect(r.xMax - 72f, by, 64f, 26f), armed ? "Sure?" : "Delete", armed ? Ui.Danger : Ui.Neutral))
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

        void DrawWhoNewCard(Rect r, bool live)
        {
            bool hover = live && r.Contains(Event.current.mousePosition);
            Ui.Card(r, Ui.Muted, hover ? 0.9f : 0.35f);
            Ui.Label(new Rect(r.x, r.y + r.height / 2f - 50f, r.width, 60f), "+", 44, hover ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + r.height / 2f + 8f, r.width, 24f), "New profile", 15, hover ? Color.white : Ui.Muted, TextAnchor.MiddleCenter, true);
            if (live && Ui.HitArea(r))
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
            if ((Ui.Button(new Rect(bx, r.y + 76f, bw, 32f), "Save", Ui.Primary) || enter))
                WhoCommitEdit();
            if (canCancel && Ui.Button(new Rect(bx - bw - 8f, r.y + 76f, bw, 32f), "Cancel", Ui.Neutral))
            {
                whoEdit = null;
                whoError = "";
            }
            if (whoError.Length > 0)
                Ui.Label(new Rect(r.x + 16f, r.y + 76f, bx - bw - r.x - 40f, 32f), whoError, 13, Ui.Warn, TextAnchor.MiddleLeft, false, true);
        }
    }
}

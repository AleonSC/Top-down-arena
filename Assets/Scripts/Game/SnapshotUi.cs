using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Run snapshots (Core/RunSnapshot.cs): F6 saves the whole match as it is now, F7 loads the newest back; the F1 dock's
    /// SNAP key opens the Snapshots sheet (take one with a name, load or delete any). Host only, with no guests connected.
    public partial class ArenaApp
    {
        List<RunSnapshot.Info> snapList;
        float snapListAt = -99f, snapScroll;
        string snapName = "", snapDeletePath = "", snapNote = "", snapRenamePath = "", snapRenameText = "";
        bool snapNoteBad;
        string snapToast = "";
        float snapToastT;
        bool snapToastBad;

        void RefreshSnapshots()
        {
            snapList = RunSnapshot.List();
            snapListAt = Time.unscaledTime;
        }

        /// F6 / F7, anywhere in a run (not while typing).
        void SnapshotKeys()
        {
            if (GUIUtility.keyboardControl != 0 || session.Screen != AppScreen.Game)
                return;
            if (Input.GetKeyDown(KeyCode.F6))
                TakeSnapshot("");
            else if (Input.GetKeyDown(KeyCode.F7))
            {
                var list = RunSnapshot.List();
                if (list.Count == 0)
                    SnapSay("No snapshots yet: F6 takes one", true);
                else
                    LoadSnapshot(list[0]);
            }
        }

        void TakeSnapshot(string name)
        {
            if (!session.IsHost || !game.Running)
            {
                SnapSay("Only the host can take a snapshot", true);
                return;
            }
            name = name.Trim();
            if (name.Length == 0)
                name = "Wave " + Mathf.Max(1, game.ShownWave) + ", " + StateWord(game.State.ToString());
            string path = null;
            try { path = RunSnapshot.SaveFile(game, name); }
            catch (System.Exception e) { Debug.LogException(e); }
            if (path == null)
                SnapSay("Couldn't save the snapshot", true);
            else
                SnapSay("Snapshot saved: " + name + "  (F7 loads it)", false);
            RefreshSnapshots();
        }

        void LoadSnapshot(RunSnapshot.Info info)
        {
            string why = RunSnapshot.CantLoad(game, session);
            if (why != null)
            {
                SnapSay(why, true);
                return;
            }
            string notes, err;
            try { err = RunSnapshot.LoadFile(game, info.Path, out notes); }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                err = e.Message;
                notes = "";
            }
            if (err != null)
                SnapSay(err, true);
            else
                SnapSay("Loaded: " + info.Name + (notes.Length > 0 ? "  (" + notes + ")" : ""), false);
        }

        void SnapSay(string text, bool bad)
        {
            snapToast = text;
            snapToastT = 2.6f;
            snapToastBad = bad;
            snapNote = text;
            snapNoteBad = bad;
        }

        static string StateWord(string state) => state == "Fighting" ? "mid-wave" : state == "Intermission" ? "between waves" : state.ToLowerInvariant();

        /// The line under the top of the screen after F6 / F7.
        void DrawSnapshotToast()
        {
            if (snapToastT <= 0f)
                return;
            snapToastT -= Time.unscaledDeltaTime;
            float a = Mathf.Clamp01(snapToastT / 0.4f);
            var r = new Rect(W / 2f - 300f, 76f, 600f, 34f);
            var bg = CrtUi.Bg;
            Ui.Fill(r, new Color(bg.r, bg.g, bg.b, 0.9f * a));
            var ink = snapToastBad ? Ui.Warn : CrtStyle.Head;
            CrtKit.Frame(r, 1f, new Color(ink.r, ink.g, ink.b, 0.7f * a));
            Ui.Label(r, snapToast, 13, new Color(ink.r, ink.g, ink.b, a), TextAnchor.MiddleCenter, true);
        }

        /// The dock's Snapshots sheet.
        void DrawDevSnapshots(Rect body, bool inMatch)
        {
            if (snapList == null || Time.unscaledTime - snapListAt > 2f)
                RefreshSnapshots();
            bool host = session.IsHost && inMatch;
            string cant = inMatch ? RunSnapshot.CantLoad(game, session) : "Start a run to take or load a snapshot.";

            var card = new Rect(body.x, body.y, body.width, 112f);
            float y = DevCard(card, "Take a snapshot", "Everything in the run right now: enemies, shots, timers, random rolls. Load it to play on from exactly there.", CrtStyle.Head);
            bool was = GUI.enabled;
            GUI.enabled = was && host;
            snapName = DkField(new Rect(card.x + 16f, y, card.width - 32f - 196f, 28f), snapName, 48);
            if (snapName.Length == 0)
                Ui.Label(new Rect(card.x + 26f, y, 300f, 28f), "Name (optional)", 12, new Color(1f, 1f, 1f, 0.3f), TextAnchor.MiddleLeft);
            if (DkKey(new Rect(card.xMax - 16f - 186f, y - 1f, 186f, 30f), "TAKE SNAPSHOT  F6", CrtStyle.Gold, false, 12))
            {
                TakeSnapshot(snapName);
                snapName = "";
                GUIUtility.keyboardControl = 0;
            }
            GUI.enabled = was;

            float fy = card.yMax + 10f;
            if (DkKey(new Rect(body.x, fy, 150f, 30f), "Open folder") && RunSnapshot.Folder != null)
                OpenFolder(RunSnapshot.Folder);
            string note = snapNote.Length > 0 ? snapNote : cant ?? "Keeps the newest " + RunSnapshot.Kept + ". Loading one marks the run as dev-touched (no records).";
            Ui.Label(new Rect(body.x + 162f, fy, body.width - 162f, 30f), note, 12, snapNote.Length > 0 && snapNoteBad || snapNote.Length == 0 && cant != null ? Ui.Warn : CrtStyle.Desc, TextAnchor.MiddleLeft);

            var list = new Rect(body.x, fy + 40f, body.width, body.yMax - fy - 40f);
            DkSub(list);
            if (snapList.Count == 0)
            {
                Ui.Label(new Rect(list.x, list.y + 30f, list.width, 24f), "No snapshots yet", 16, Color.white, TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(list.x, list.y + 58f, list.width, 40f), "Press F6 in a run (or Take snapshot above).", 12, CrtStyle.Desc, TextAnchor.UpperCenter);
                return;
            }
            const float row = 62f;
            float maxScroll = Mathf.Max(0f, snapList.Count * row - list.height + 8f);
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && list.Contains(e.mousePosition))
            {
                snapScroll = Mathf.Clamp(snapScroll + e.delta.y * 24f, 0f, maxScroll);
                e.Use();
            }
            snapScroll = Mathf.Clamp(snapScroll, 0f, maxScroll);
            RunSnapshot.Info load = null, delete = null, rename = null;
            GUI.BeginGroup(list);
            for (int i = 0; i < snapList.Count; i++)
            {
                float ry = 4f + i * row - snapScroll;
                if (ry < -row || ry > list.height)
                    continue;
                var s = snapList[i];
                var rr = new Rect(6f, ry, list.width - 12f - (maxScroll > 0f ? 6f : 0f), row - 6f);
                DkSub(rr, new Color(1f, 1f, 1f, i == 0 ? 0.06f : 0.03f));
                bool renaming = snapRenamePath == s.Path;
                if (renaming)
                {
                    snapRenameText = DkField(new Rect(rr.x + 10f, rr.y + 4f, rr.width - 190f, 26f), snapRenameText, 48);
                    if (e.type == EventType.KeyDown && (e.keyCode == KeyCode.Return || e.keyCode == KeyCode.KeypadEnter))
                    {
                        rename = s;
                        e.Use();
                    }
                }
                else
                    Ui.Label(new Rect(rr.x + 14f, rr.y + 5f, rr.width - 330f, 24f), s.Name + (i == 0 ? "   ·  F7" : ""), 14, Color.white, TextAnchor.MiddleLeft, true);
                string meta = s.Created.ToLocalTime().ToString("MMM d, HH:mm:ss") + "  ·  " + s.Mode + "  ·  wave " + s.Wave + " " + StateWord(s.State) + "  ·  "
                    + s.Enemies + " enemies  ·  " + ReplayClock(s.RunTime) + "  ·  " + (s.Bytes / 1024) + " KB  ·  " + s.Party;
                Ui.Label(new Rect(rr.x + 14f, rr.y + 30f, rr.width - 28f, 20f), meta, 11, CrtStyle.Desc);
                float bx = rr.xMax - 12f;
                if (renaming)
                {
                    bx -= 70f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "Cancel", Ui.Neutral, false, 11)) snapRenamePath = "";
                    bx -= 76f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "Save", Ui.Primary, false, 11)) rename = s;
                }
                else if (snapDeletePath == s.Path)
                {
                    bx -= 70f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "Keep", Ui.Neutral, false, 11)) snapDeletePath = "";
                    bx -= 76f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "SURE?", Ui.Danger, false, 11)) delete = s;
                }
                else
                {
                    bx -= 70f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "Delete", Ui.Danger, false, 11)) { snapDeletePath = s.Path; snapRenamePath = ""; }
                    bx -= 76f;
                    if (DkKey(new Rect(bx, rr.y + 6f, 70f, 26f), "Rename", Ui.Neutral, false, 11))
                    {
                        snapRenamePath = s.Path;
                        snapRenameText = s.Name;
                        snapDeletePath = "";
                    }
                    bx -= 82f;
                    GUI.enabled = was && cant == null;
                    if (DkKey(new Rect(bx, rr.y + 6f, 76f, 26f), "LOAD", Ui.Primary, false, 12)) load = s;
                    GUI.enabled = was;
                }
            }
            GUI.EndGroup();
            if (maxScroll > 0f)
            {
                float frac = list.height / (snapList.Count * row + 8f);
                float th = list.height * frac, ty = list.y + (list.height - th) * (snapScroll / maxScroll);
                Ui.Fill(new Rect(list.xMax - 4f, ty, 3f, th), new Color(1f, 1f, 1f, 0.3f));
            }

            if (rename != null)
            {
                snapRenamePath = "";
                GUIUtility.keyboardControl = 0;
                if (snapRenameText.Trim().Length > 0 && !RunSnapshot.Rename(rename.Path, snapRenameText))
                    SnapSay("Couldn't rename it", true);
                RefreshSnapshots();
            }
            if (delete != null)
            {
                snapDeletePath = "";
                try { System.IO.File.Delete(delete.Path); snapNote = ""; }
                catch (System.Exception) { SnapSay("Couldn't delete it", true); }
                RefreshSnapshots();
            }
            if (load != null)
                LoadSnapshot(load);
        }
    }
}

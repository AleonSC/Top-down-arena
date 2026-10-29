using System;
using System.IO;
using UnityEngine;

namespace Arena
{
    /// The dev text editor: F4 (or F1 → Tools) turns on text-edit mode. The text under the mouse is outlined;
    /// click it to edit it in a box. Edits show at once and are saved to TextOverrides.txt (at the top of the
    /// project in the Unity editor, so they can be committed; in a build, in the game's data folder). Numbers
    /// stay with the game: an edit has to keep them. Tools/TextEdits/fold.py writes the edits into the code.
    public partial class ArenaApp
    {
        bool textEditMode, textEditOpen, textEditFocus;
        string textEditOriginal = "", textEditBuffer = "", textEditNote = "";

        static string TextOverridePath => Application.isEditor
            ? Path.GetFullPath(Path.Combine(Application.dataPath, "..", TextOverrides.FileName))
            : Path.Combine(Application.persistentDataPath, TextOverrides.FileName);

        void LoadTextOverrides()
        {
            try { TextOverrides.Load(TextOverridePath); }
            catch (Exception e) { Debug.Log("Couldn't read " + TextOverridePath + ": " + e.Message); }
        }

        /// Update: F4 turns the mode on and off; Esc closes the box, then leaves the mode.
        void TextEditKeys()
        {
            if (Input.GetKeyDown(KeyCode.F4))
                SetTextEditMode(!textEditMode);
            else if (Input.GetKeyDown(KeyCode.Escape) && textEditMode)
            {
                if (textEditOpen)
                    textEditOpen = false;
                else
                    SetTextEditMode(false);
            }
        }

        void SetTextEditMode(bool on)
        {
            textEditMode = on;
            textEditOpen = false;
            GUIUtility.keyboardControl = 0;
        }

        /// Start of OnGUI: a click on outlined text opens the box (and goes no further).
        void TextEditBeginGui()
        {
            Ui.PickText = textEditMode && !textEditOpen;
            Ui.BeginPick();
            var ev = Event.current;
            if (Ui.PickText && ev.type == EventType.MouseDown && ev.button == 0 && Ui.Hovered != null && !MouseOnTextBanner())
            {
                textEditOriginal = Ui.Hovered;
                textEditBuffer = TextOverrides.Apply(textEditOriginal);
                textEditNote = "";
                textEditOpen = true;
                textEditFocus = true;
                Ui.PickText = false;
                ev.Use();
            }
        }

        /// End of OnGUI: the mode's banner, and the edit box when one is open.
        void TextEditEndGui()
        {
            Ui.EndPick();
            if (!textEditMode)
                return;
            Ui.NoPick = true;
            DrawTextBanner();
            if (textEditOpen)
                DrawTextEditBox();
            Ui.NoPick = false;
        }

        bool bannerLow;  // the banner keeps to the edge away from the mouse, so it never covers what you want to click

        Rect TextBanner => new Rect(W / 2f - 250f, bannerLow ? H - 34f : 4f, 500f, 30f);

        bool MouseOnTextBanner() => TextBanner.Contains(Event.current.mousePosition);

        void DrawTextBanner()
        {
            if (!TextBanner.Contains(Event.current.mousePosition))
                bannerLow = Event.current.mousePosition.y < H * 0.35f;
            var r = TextBanner;
            Ui.Pill(r, new Color(0.35f, 0.25f, 0.05f, 0.92f));
            Ui.Label(new Rect(r.x + 14f, r.y, r.width - 110f, r.height), "TEXT EDIT  ·  click any text to change it  ·  F4 or Esc to stop"
                + (TextOverrides.Count > 0 ? "  ·  " + TextOverrides.Count + " edited" : ""), 11, Ui.Gold, TextAnchor.MiddleLeft, true);
            if (Ui.SmallButton(new Rect(r.xMax - 78f, r.y + 4f, 70f, r.height - 8f), "Done", Ui.GoldButton, 11))
                SetTextEditMode(false);
        }

        void DrawTextEditBox()
        {
            var p = new Rect(W / 2f - 330f, H / 2f - 190f, 660f, 380f);
            Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f, 0.45f));
            Ui.Panel(p, true);
            float x = p.x + 24f, w = p.width - 48f, y = p.y + 16f;
            Ui.Fancy(new Rect(x, y, w, 30f), "EDIT TEXT", 22, Color.white, new Color(1f, 0.85f, 0.5f), TextAnchor.MiddleLeft, Ui.Gold);
            y += 38f;
            Ui.Label(new Rect(x, y, w, 16f), "ORIGINAL (IN THE CODE)", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 18f;
            Ui.Label(new Rect(x, y, w, 40f), textEditOriginal, 13, new Color(0.8f, 0.84f, 0.95f), TextAnchor.UpperLeft, false, true);
            y += 44f;
            int nums = TextOverrides.Numbers(textEditOriginal);
            Ui.Label(new Rect(x, y, w, 16f), "SHOWN AS", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 18f;
            GUI.SetNextControlName("textedit");
            textEditBuffer = Ui.Area(new Rect(x, y, w, 96f), textEditBuffer);
            if (textEditFocus)
            {
                GUI.FocusControl("textedit");
                textEditFocus = false;
            }
            y += 102f;
            string problem = TextOverrides.Problem(textEditOriginal, textEditBuffer);
            string note = problem ?? (textEditNote.Length > 0 ? textEditNote
                : nums > 0 ? "Numbers come from the game: edit the words around them (it changes this text with any numbers)."
                : "The same text anywhere else in the game changes too.");
            Ui.Label(new Rect(x, y, w, 32f), note, 11, problem != null ? Ui.Warn : Ui.Muted, TextAnchor.UpperLeft, false, true);
            y += 36f;
            if (Ui.Button(new Rect(x, y, 140f, 36f), "Save", problem == null ? Ui.Primary : Ui.Neutral) && problem == null)
            {
                TextOverrides.Set(textEditOriginal, textEditBuffer);
                try
                {
                    TextOverrides.Save(TextOverridePath);
                    textEditOpen = false;
                }
                catch (Exception e)
                {
                    textEditNote = "Couldn't save: " + e.Message;
                }
            }
            if (TextOverrides.Has(textEditOriginal) && Ui.Button(new Rect(x + 150f, y, 170f, 36f), "Back to original", Ui.Danger))
            {
                TextOverrides.Remove(textEditOriginal);
                try { TextOverrides.Save(TextOverridePath); } catch (Exception) { }
                textEditOpen = false;
            }
            if (Ui.Button(new Rect(p.xMax - 24f - 120f, y, 120f, 36f), "Cancel"))
                textEditOpen = false;
            Ui.Label(new Rect(x, p.yMax - 30f, w, 18f), "Saved to " + TextOverridePath + "  ·  into the code: python3 Tools/TextEdits/fold.py", 10, Ui.Muted);
        }
    }
}

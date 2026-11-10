using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// One full-screen tool or window drawn over whatever is underneath (the F1 menu, the wave editor, the Boss Lab setup): its id, how
    /// to draw it and what Esc does. Screens live in a stack: the one opened last is on top, draws, and gets Esc first. To add one:
    /// declare it (`static readonly UiScreen ScreenFoo = new UiScreen("foo", ...)` is not needed: make an instance field in
    /// `ArenaApp`), open it with `OpenScreen(ScreenFoo)`, close it with `CloseScreen(ScreenFoo)`; ArenaApp blocks the game's input while
    /// a modal one is open, draws the top one and sends Esc to it. See HANDOFF.md "Adding a screen".
    public sealed class UiScreen
    {
        public readonly string Id;
        public readonly Action Draw;
        /// Blocks the game's input and the screens under it (true for every tool window).
        public bool Modal = true;
        /// Esc is ignored while a text field has focus (it just lets go of the field) instead of closing the screen.
        public bool EscNeedsFreeFocus;
        /// Called instead of closing on Esc; return true to say Esc was used (the screen stays open), false to let it close.
        public Func<bool> OnEsc;
        /// Called once when the screen closes, however it closed.
        public Action OnClose;
        public bool IsOpen { get; internal set; }

        public UiScreen(string id, Action draw)
        {
            Id = id;
            Draw = draw;
        }
    }

    public partial class ArenaApp
    {
        // The tool screens (the old devMenuOpen / labSetupOpen / waveEdOpen bools are properties over these, so existing code
        // reads and sets them as before).
        UiScreen screenDev, screenLab, screenWaveEd;
        readonly List<UiScreen> screenStack = new List<UiScreen>();

        void InitScreens()
        {
            if (screenDev != null)
                return;
            screenDev = new UiScreen("dev", DrawDevMenu);
            screenLab = new UiScreen("lab", DrawLabSetup);
            screenWaveEd = new UiScreen("waveeditor", DrawWaveEditor) { EscNeedsFreeFocus = true };
        }

        bool devMenuOpen { get { InitScreens(); return screenDev.IsOpen; } set { InitScreens(); SetScreen(screenDev, value); } }
        bool labSetupOpen { get { InitScreens(); return screenLab.IsOpen; } set { InitScreens(); SetScreen(screenLab, value); } }
        bool waveEdOpen { get { InitScreens(); return screenWaveEd.IsOpen; } set { InitScreens(); SetScreen(screenWaveEd, value); } }

        void SetScreen(UiScreen s, bool open)
        {
            if (open)
                OpenScreen(s);
            else
                CloseScreen(s);
        }

        /// Open a screen on top of the stack (an open one moves to the top).
        void OpenScreen(UiScreen s)
        {
            screenStack.Remove(s);
            screenStack.Add(s);
            s.IsOpen = true;
        }

        void CloseScreen(UiScreen s)
        {
            if (!s.IsOpen)
                return;
            s.IsOpen = false;
            screenStack.Remove(s);
            if (s.OnClose != null)
                s.OnClose();
        }

        /// The screen on top, or null.
        UiScreen TopScreen => screenStack.Count > 0 ? screenStack[screenStack.Count - 1] : null;

        /// Is any screen that blocks the game open?
        bool ModalScreenOpen
        {
            get
            {
                InitScreens();
                foreach (var s in screenStack)
                    if (s.Modal)
                        return true;
                return false;
            }
        }

        /// Draw the top screen (the ones under it stay hidden, as the tools always were exclusive).
        void DrawScreens()
        {
            var top = TopScreen;
            if (top != null)
                top.Draw();
        }

        /// Esc: the top screen takes it. True when a screen was open (the press is used up, whatever it did).
        bool HandleScreenEsc()
        {
            var top = TopScreen;
            if (top == null)
                return false;
            if (top.EscNeedsFreeFocus && GUIUtility.keyboardControl != 0)
            {
                GUIUtility.keyboardControl = 0;  // the first Esc lets go of the text field
                return true;
            }
            if (top.OnEsc == null || !top.OnEsc())
                CloseScreen(top);
            return true;
        }
    }
}

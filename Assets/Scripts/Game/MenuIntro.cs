using UnityEngine;

namespace Arena
{
    /// The main menu's own entrance, the end of the disk boot sequence (Docs/Mockups/disk-boot-sequence.html; ProfileTransition.cs plays
    /// the whole of it from Who's playing?). The menu reads one machine clock, MachT (MenuMachine.cs), from 2.6 s on, so the intro's
    /// second 0 is the boot's 2.6 s: the title types on the monitor (0 to 0.7 s), the case drops out from under it and lands (0.3 to
    /// 1.1 s), then powers on (to 2.2 s). Any key or click skips to the end, under a quick channel flip (SkipStatic.cs). Nothing in the
    /// menu takes a click until it's over (MenuIntroPlaying). Coming back from the lobby or a run doesn't play it again. Reduced
    /// effects or Visual effects off: a 0.45 s fade instead. Each piece of the menu asks MenuIntroK(a, b) how far in it is (1 when no
    /// intro is playing).
    public partial class ArenaApp
    {
        const float MenuIntroLenFull = 2.2f, MenuIntroLenFade = 0.45f;
        float menuIntroT = -1f;            // seconds into the intro; below 0 when none has started
        bool menuIntroDone;                // it has started once this launch
        AppScreen menuLastScreen = AppScreen.Lobby;

        const float MenuIntroBuiltAt = 1.9f;  // the case has powered on by now: a skip after it has nothing left to flip over
        bool MenuIntroBuilt => menuIntroT >= MenuIntroBuiltAt;

        bool MenuIntroFull => fxOn && !fxReduced;
        float MenuIntroLen => MenuIntroFull ? MenuIntroLenFull : MenuIntroLenFade;
        bool MenuIntroPlaying => menuIntroT >= 0f && menuIntroT < MenuIntroLen;

        /// How far a piece of the menu is into its part of the intro (0 to 1), or 1 when the full intro isn't playing.
        float MenuIntroK(float a, float b) => MenuIntroPlaying && MenuIntroFull ? Seg(menuIntroT, a, b) : 1f;

        /// Update: start the intro the first time the main menu shows, run its clock, and skip it on any key or click.
        void TickMenuIntro(float dt)
        {
            bool onMenu = session.Screen == AppScreen.Menu;
            if (onMenu && menuLastScreen != AppScreen.Menu)
            {
                menuPlayOpen = menuJoinOpen = menuSetOpen = menuWikiOpen = false;  // the case's shutter is down (MenuMachine.cs)
                menuPlayK = 0f;
            }
            menuLastScreen = session.Screen;
            if (profTr)
            {
                TickProfTr(dt);  // the move to or from the picker runs the menu's intro itself (ProfileTransition.cs)
                TickTitleTyping();  // which types the title too
                return;
            }
            if (onMenu && !menuIntroDone && !whoOpen && !settingsOpen && !profileOpen)
            {
                menuIntroDone = true;
                menuIntroT = 0f;
                menuTypedSnd = 0;
                return;
            }
            if (!MenuIntroPlaying)
                return;
            if (!onMenu || Input.anyKeyDown)
            {
                if (onMenu && MenuIntroFull && !MenuIntroBuilt)
                    StartSkipStatic();  // the channel flip (SkipStatic.cs)
                menuIntroT = MenuIntroLen;  // skipped (or the menu went away)
            }
            else
            {
                float before = menuIntroT;
                menuIntroT += dt;
                if (MenuIntroFull)
                    PlayMachineCues(2.6f + before, 2.6f + menuIntroT);  // the case's drop and power-on (MenuMachine.cs)
                TickTitleTyping();
            }
        }

        const string CrtTitleText = "VECTOR SECTOR: ARENA";
        int menuTypedSnd;  // letters whose typing sound has played

        /// Plays a key sound for each new letter of the title as it types (look B of Docs/Mockups/menu-typing-sounds.html): the space bar for
        /// the gap, Enter for the last letter. Skipping the intro stops it (this isn't called then). Own screen only, nothing sent.
        void TickTitleTyping()
        {
            if (!MenuIntroFull)
                return;
            float typeK = Seg(menuIntroT, CrtTypeAt, CrtTypeAt + CrtTypeLen);
            int n = typeK >= 1f ? CrtTitleText.Length : Mathf.FloorToInt(typeK * CrtTitleText.Length);
            if (n < menuTypedSnd || n - menuTypedSnd > 3)
            {
                menuTypedSnd = n;  // the clock went backwards or jumped (skipped, or the transition runs in reverse): catch up silently
                return;
            }
            while (menuTypedSnd < n)
            {
                menuTypedSnd++;
                var s = menuTypedSnd == CrtTitleText.Length ? Sfx.TitleEnter : CrtTitleText[menuTypedSnd - 1] == ' ' ? Sfx.TitleSpace : Sfx.TitleKey;
                sounds.Play(s, 0.6f, Time.unscaledTime);
            }
        }

        /// Under the menu, as the full intro starts: the background is dark, then fades up.
        void DrawMenuIntroBackdrop()
        {
            if (MenuIntroPlaying && MenuIntroFull && !profTr)
                Ui.Fill(new Rect(0f, 0f, W, H), new Color(0.01f, 0.01f, 0.03f, 0.9f * (1f - Seg(menuIntroT, 0f, 0.5f))));
        }

        /// Over the menu, for the short version: everything fades up from dark.
        void DrawMenuIntroFade()
        {
            if (MenuIntroPlaying && !MenuIntroFull)
                Ui.Fill(new Rect(0f, 0f, W, H), new Color(0.01f, 0.01f, 0.03f, 1f - Seg(menuIntroT, 0f, 0.4f)));
        }


        static readonly Color CrtGreen = new Color(0.62f, 0.78f, 1f), CrtGlow = new Color(0.25f, 0.5f, 1f);  // (blue now; the names are from when it was green)
        const float CrtTypeAt = 0f, CrtTypeLen = 0.7f;  // intro seconds: the title types on the monitor (boot 2.6 to 3.3 s)
        static int crtTyped = -1;      // letters typed so far; -1 = all of them
        static string crtFull = CrtTitleText;  // the line on the screen (WHO'S PLAYING? while the moves to and from the picker retype it)
        static bool crtFlat;           // its letters are shown flat (no flip-in): a line being erased or retyped by DrawTitleRetype
        static Rect CrtBox => new Rect(W / 2f - 470f, 32f, 940f, 128f);  // the monitor's glass (PcMonitor.cs)

        /// The monitor's glass (Docs/Mockups/disk-boot-sequence.html): a near-black blue screen in a thin dark border, a faint blue
        /// rim inside it and a soft glow towards the middle.
        void DrawCrtBack()
        {
            var box = CrtBox;
            Ui.RoundBox(new Rect(box.x - 3f, box.y - 3f, box.width + 6f, box.height + 6f), new Color(0.039f, 0.043f, 0.055f), 8);
            Ui.RoundBox(box, CrtUi.T(new Color(0.016f, 0.039f, 0.094f)), 6);
            Ui.RadialGlow(new Rect(box.x + 40f, box.y + 4f, box.width - 80f, box.height - 8f), CrtUi.T(new Color(0.235f, 0.43f, 0.86f)).WithAlpha(0.2f));
            EdgeRing(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f), 1f, CrtUi.T(new Color(0.102f, 0.141f, 0.251f)));
            DrawHoloGlass(new Rect(box.x + 1f, box.y + 1f, box.width - 2f, box.height - 2f), Time.unscaledTime);  // the Tab screen's hologram glass (TabHolo.cs)
        }

        /// Over the text: rolling scanlines, a glow along the screen's edge, a glare, and a blinking cursor in the bottom corner.
        void DrawCrtFront(string text = null)
        {
            text = text ?? crtFull;
            var box = CrtBox;
            float t = Time.unscaledTime;
            GUI.BeginGroup(box);
            float roll = (t * 18f) % 4f;
            for (float y = -4f + roll; y < box.height; y += 4f)
                Ui.Fill(new Rect(0f, y, box.width, 2f), new Color(0f, 0f, 0f, 0.28f));
            Ui.Fill(new Rect(24f, 5f, box.width * 0.35f, 2f), new Color(1f, 1f, 1f, 0.07f));  // glare
            GUI.EndGroup();
            if (crtTyped >= 0)
            {
                // Typing: the cursor sits after the last letter typed (blinking), the way a terminal does.
                string full = text;
                float fw = Ui.TextWidth(full, (int)MenuTitleSize, true), cx = W / 2f - fw / 2f + Ui.TextWidth(full.Substring(0, crtTyped), (int)MenuTitleSize, true);
                // The cursor is as wide as the letter it sits under (the next one to type; an M once the line is done).
                string under = crtTyped < full.Length && full[crtTyped] != ' ' ? full[crtTyped].ToString() : "M";
                float cw = Ui.TextWidth(under, (int)MenuTitleSize, true);
                if ((int)(t * 8f) % 2 == 0)
                    Ui.Fill(new Rect(cx, MenuTitleY + 14f, cw, 8f), CrtGreen.WithAlpha(0.9f));
            }
            else if ((int)(t * 1.2f) % 2 == 0)
            {
                // Typed out: the cursor waits just after the last letter, on the line it was typed on.
                float fw = Ui.TextWidth(text, (int)MenuTitleSize, true);
                Ui.Fill(new Rect(W / 2f + fw / 2f + 10f, MenuTitleY + 15f, 30f, 7f), CrtGreen.WithAlpha(0.85f));
            }
        }

        /// The title text: phosphor blue with a glow, and a brief dip in brightness now and then (the screen flickering).
        void DrawCrtTitle(Rect r)
        {
            float t = Time.unscaledTime;
            float flick = (t % 3f) > 2.9f ? 0.7f : (t % 7f) > 6.95f ? 0.85f : 1f;
            var c = CrtGreen.WithAlpha(flick);
            if (crtTyped < 0)
            {
                Ui.Glitch(r, crtFull, (int)MenuTitleSize, c, TextAnchor.MiddleCenter, CrtGlow);
                return;
            }
            string full = crtFull;
            float fw = Ui.TextWidth(full, (int)MenuTitleSize, true), x0 = W / 2f - fw / 2f;
            // Each letter flips in as it's typed (Docs/Mockups/menu-holo-floor.html): it drops a little and unfolds from a sliver
            // about its middle line, overshooting a touch, over CrtFlipLen.
            for (int i = 0; i < crtTyped; i++)
            {
                if (full[i] == ' ')
                    continue;
                float typedAt = CrtTypeAt + CrtTypeLen * i / full.Length;
                float k = crtFlat ? 1f : Mathf.Clamp01((menuIntroT - typedAt) / CrtFlipLen);
                float sy = Mathf.Max(0.05f, EaseBack(k)), dy = -18f * (1f - EaseOut(k));
                float lx = x0 + Ui.TextWidth(full.Substring(0, i), (int)MenuTitleSize, true);
                var lr = new Rect(lx, r.y + dy, Ui.TextWidth(full[i].ToString(), (int)MenuTitleSize, true) + 30f, r.height);
                var old = GUI.matrix;
                if (sy != 1f)
                    GUIUtility.ScaleAroundPivot(new Vector2(1f, sy), new Vector2(lr.x, lr.center.y));
                Ui.Glitch(lr, full[i].ToString(), (int)MenuTitleSize, c.WithAlpha(c.a * Mathf.Clamp01(k * 3f)), TextAnchor.MiddleLeft, CrtGlow);
                GUI.matrix = old;
            }
        }

        const float CrtFlipLen = 0.3f;  // seconds for a typed letter to flip into place
    }
}

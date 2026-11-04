using UnityEngine;

namespace Arena
{
    /// The main menu drawing itself in, the first time it shows after the game starts ("pen plot", option B of
    /// Docs/Mockups/menu-d.html), 2.75 s:
    ///   0.0 s   the background fades up; the x-axis draws out from the origin
    ///   0.35 s  a pen writes the title in ink, left to right; at 1.35 s the letters fill in
    ///   1.4 s   the pen traces the shot's arc over the title; at 1.9 s the shot sets off along it
    ///   1.65 s  the box opens out of its middle and its plot corners snap on
    ///   1.75 s  your name card, then PLAY rise into it, then Join game, Records and Settings; Quit slides up; the line under the title types out
    /// Any key or click skips to the end. Nothing in the menu takes a click until it's over (MenuIntroPlaying). Coming back
    /// from the lobby or a run doesn't play it again. Reduced effects or Visual effects off: a 0.45 s fade instead.
    /// Each piece of the menu asks MenuIntroK(a, b) how far in it is (1 when no intro is playing).
    public partial class ArenaApp
    {
        const float MenuIntroLenFull = 2.75f, MenuIntroLenFade = 0.45f;
        const float MenuTitleHalf = 390f;  // about half the title's width at its size (where the pen starts and ends)
        float menuIntroT = -1f;            // seconds into the intro; below 0 when none has started
        bool menuIntroDone;                // it has started once this launch
        AppScreen menuLastScreen = AppScreen.Lobby;

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
                menuTipSeed = Random.Range(0, 1000);  // a new first tip each time the menu opens
                menuPlayOpen = false;                 // and the box is back on Play (SavesUi.cs)
                menuPlayK = 0f;
            }
            menuLastScreen = session.Screen;
            if (profTr)
            {
                TickProfTr(dt);  // the move to or from the picker runs the menu's intro itself (ProfileTransition.cs)
                return;
            }
            if (onMenu && !menuIntroDone && !whoOpen && !settingsOpen && !recordsOpen && !cosmeticsOpen && !continueOpen)
            {
                menuIntroDone = true;
                menuIntroT = 0f;
                return;
            }
            if (!MenuIntroPlaying)
                return;
            if (!onMenu || Input.anyKeyDown)
                menuIntroT = MenuIntroLen;  // skipped (or the menu went away)
            else
                menuIntroT += dt;
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

        float MenuPenX(float k) => W / 2f - MenuTitleHalf - 6f + (2f * MenuTitleHalf + 12f) * k;
        static float MenuPenY(float x) => MenuTitleY - 26f + 52f * (0.5f + 0.5f * Mathf.Sin(x * 0.19f));

        /// The title: during the intro, written in ink up to the pen and then filled in; otherwise as usual.
        void DrawMenuTitleText(Rect r)
        {
            if (!(MenuIntroPlaying && MenuIntroFull))
            {
                Ui.Glitch(r, "CARTESIAN COLOSSEUM", (int)MenuTitleSize, Color.white, TextAnchor.MiddleCenter, Ui.Accent);
                DrawVhsOsd(r);
                return;
            }
            float pen = Seg(menuIntroT, 0.35f, 1.35f), fill = Seg(menuIntroT, 1.35f, 1.55f);
            if (pen > 0f && fill < 1f)
            {
                var ink = new Color(0.42f, 0.6f, 1f, 0.9f * (1f - fill));  // blue ink, filling in white
                GUI.BeginGroup(new Rect(0f, r.y, MenuPenX(pen), r.height));
                Ui.Fancy(new Rect(0f, 0f, W, r.height), "CARTESIAN COLOSSEUM", (int)MenuTitleSize, ink, ink, TextAnchor.MiddleCenter, Ui.Accent);
                GUI.EndGroup();
            }
            if (fill > 0f)
                WithAlpha(fill, () => Ui.Glitch(r, "CARTESIAN COLOSSEUM", (int)MenuTitleSize, Color.white, TextAnchor.MiddleCenter, Ui.Accent));
        }

        /// The title as a VCR's on-screen display (Docs/Mockups/title-styles.html, look 6): "PLAY" top left, a running
        /// tape counter bottom right, and a tracking band that drifts down through the title every few seconds.
        void DrawVhsOsd(Rect r)
        {
            float t = Time.unscaledTime, left = W / 2f - MenuTitleHalf, right = W / 2f + MenuTitleHalf;
            float band = (t % 6f) / 1.6f;  // 0..1 while it drifts down, then it rests
            if (band < 1f)
            {
                GUI.BeginGroup(new Rect(left - 10f, r.y, right - left + 20f, r.height));
                float by = -24f + (r.height + 24f) * band;
                Ui.Fill(new Rect(0f, by, right - left + 20f, 22f), new Color(1f, 1f, 1f, 0.07f));
                Ui.Fill(new Rect(0f, by + 22f, right - left + 20f, 1f), new Color(1f, 1f, 1f, 0.12f));
                GUI.EndGroup();
            }
            Ui.Icon(new Rect(left - 4f, r.y - 14f, 14f, 14f), PixelArt.Get("ui_play"));
            Ui.Shadowed(new Rect(left + 14f, r.y - 16f, 80f, 18f), "PLAY", 13, Color.white, TextAnchor.MiddleLeft);
            int secs = Mathf.FloorToInt(t);
            string clock = "SP  " + (secs / 3600) + ":" + (secs / 60 % 60).ToString("00") + ":" + (secs % 60).ToString("00");
            Ui.Shadowed(new Rect(right - 140f, r.yMax - 8f, 144f, 18f), clock, 13, new Color(1f, 1f, 1f, 0.9f), TextAnchor.MiddleRight);
        }

        /// GL (inside BeginUiGl): the pen's glowing tip, writing the title and then tracing the arc.
        void DrawMenuIntroPen(float t)
        {
            if (!(MenuIntroPlaying && MenuIntroFull))
                return;
            UiModelAt(V2.Zero, 1f);
            float pen = Seg(menuIntroT, 0.35f, 1.35f), arc = Seg(menuIntroT, 1.4f, 1.9f);
            if (pen > 0f && pen < 1f)
            {
                float x = MenuPenX(pen);
                PenTip(new V2(x, MenuPenY(x)));
            }
            if (arc > 0f && arc < 1f)
                PenTip(ArcAt(arc));
        }

        void PenTip(V2 p)
        {
            Circle(p, 16f, new Rgba(0.59f, 0.78f, 1f, 0.12f), 18);
            Circle(p, 9f, new Rgba(0.59f, 0.78f, 1f, 0.3f), 14);
            Circle(p, 3.5f, new Rgba(1f, 1f, 1f, 0.95f), 10);
        }
    }
}

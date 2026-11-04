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
                DrawCrtBack();
                DrawCrtTitle(r);
                DrawCrtFront();
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
                WithAlpha(fill, () => { DrawCrtBack(); DrawCrtTitle(r); DrawCrtFront(); });
        }

        static readonly Color CrtGreen = new Color(0.49f, 1f, 0.66f), CrtGlow = new Color(0.18f, 1f, 0.4f);
        static Rect CrtBox => new Rect(W / 2f - 520f, MenuTitleY - 52f, 1040f, 104f);

        /// The title as text on a CRT (Docs/Mockups/title-styles.html, look 4): a bezel round a dark green glass screen.
        void DrawCrtBack()
        {
            var box = CrtBox;
            Ui.Pill(new Rect(box.x - 8f, box.y - 8f, box.width + 16f, box.height + 16f), new Color(0.23f, 0.26f, 0.3f));
            Ui.Pill(new Rect(box.x - 5f, box.y - 5f, box.width + 10f, box.height + 10f), new Color(0.125f, 0.14f, 0.16f));
            Ui.Pill(new Rect(box.x - 1f, box.y - 1f, box.width + 2f, box.height + 2f), new Color(0.03f, 0.035f, 0.04f));
            for (int i = 0; i < 6; i++)  // glass: dark at the edges, lighter towards the middle
            {
                float k = i / 5f;
                Ui.Pill(new Rect(box.x + i * 20f, box.y + i * 6f, box.width - i * 40f, box.height - i * 12f), Color.Lerp(new Color(0.004f, 0.02f, 0.01f), new Color(0.05f, 0.17f, 0.1f), k));
            }
        }

        /// Over the text: rolling scanlines, a glow along the screen's edge, a glare, and a blinking cursor in the bottom corner.
        void DrawCrtFront()
        {
            var box = CrtBox;
            float t = Time.unscaledTime;
            GUI.BeginGroup(box);
            float roll = (t * 18f) % 4f;
            for (float y = -4f + roll; y < box.height; y += 4f)
                Ui.Fill(new Rect(0f, y, box.width, 2f), new Color(0f, 0f, 0f, 0.28f));
            Ui.Fill(new Rect(24f, 5f, box.width * 0.35f, 2f), new Color(1f, 1f, 1f, 0.07f));  // glare
            GUI.EndGroup();
            Ui.Rim(box, CrtGlow, 0.3f);
            if ((int)(t * 1.2f) % 2 == 0)
            {
                Ui.Fill(new Rect(box.xMax - 58f, box.yMax - 18f, 30f, 7f), new Color(CrtGreen.r, CrtGreen.g, CrtGreen.b, 0.85f));
            }
        }

        /// The title text: phosphor green with a glow, and a brief dip in brightness now and then (the screen flickering).
        void DrawCrtTitle(Rect r)
        {
            float t = Time.unscaledTime;
            float flick = (t % 3f) > 2.9f ? 0.7f : (t % 7f) > 6.95f ? 0.85f : 1f;
            var c = new Color(CrtGreen.r, CrtGreen.g, CrtGreen.b, flick);
            Ui.Glitch(r, "CARTESIAN COLOSSEUM", (int)MenuTitleSize, c, TextAnchor.MiddleCenter, CrtGlow);
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

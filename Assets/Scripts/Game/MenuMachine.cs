using UnityEngine;

namespace Arena
{
    /// The main menu's case (design B of Docs/Mockups/main-menu-machines.html, its entrance from Docs/Mockups/disk-boot-sequence.html): a
    /// graphite case in a neon outline under the monitor, with Play, Join game and Wiki as keycaps down its left, a bay in the
    /// middle behind a ribbed roller shutter, your profile on a small screen at the right (click it for your profile) over a grille and the
    /// POWER and DISK lights. Play rolls the shutter up onto your saved runs (Continue, delete, New run); Join game onto the host address
    /// and Connect; Settings onto the settings (BaySettings.cs); pressing the key again (or Esc) rolls it back down.
    /// Its entrance reads the machine clock MachT (seconds of the disk boot sequence): the case falls out from under the monitor (2.9 s),
    /// lands with a squash, a bounce, a neon flare, the monitor jolting and dust puffing off its feet (3.35 s), then powers on: POWER,
    /// DISK, the key lamps one by one, the profile screen switching on like a tube (3.75 to 4.45 s), a rattle of the shutter (4.4 s).
    public partial class ArenaApp
    {
        static readonly Rect CaseRect = new Rect(W / 2f - 490f, 256f, 980f, 440f);
        const float CaseClipTop = 238f;   // the case shows only below the monitor's bottom edge, so it falls out from under it
        static readonly Rect PlayKeyRect = new Rect(184f, 290f, 210f, 70f), JoinKeyRect = new Rect(184f, 380f, 210f, 52f);
        static readonly Rect WikiKeyRect = new Rect(184f, 450f, 210f, 52f);
        // Under the profile screen, a recessed control strip: Settings (a cog), the POWER / DISK lights, Quit (a power button); then the
        // grille, level with the left one.
        static readonly Rect StripRect = new Rect(886f, 456f, 214f, 40f);
        static readonly Rect SettingsKeyRect = new Rect(892f, 462f, 28f, 28f), QuitKeyRect = new Rect(1066f, 462f, 28f, 28f);
        static readonly Rect BayRect = new Rect(424f, 288f, 432f, 376f);
        static readonly Rect ProfScreenRect = new Rect(886f, 290f, 214f, 150f);
        const float MachEnd = 4.8f;       // the boot sequence's length; past it the menu is settled
        // The boot sequence's moments on the machine clock (MachT, seconds):
        const float BootEraseAt = 1.95f, BootEraseEnd = 2.45f;  // WHO'S PLAYING? erased letter by letter (ProfileTransition.cs)
        const float BootMenuAt = 2.6f;      // the menu's own entrance starts: MenuIntro.cs's second 0
        const float BootDiskReadAt = 2.85f; // the disk is read: the drive's light, the eject key comes up (DriveSlot.cs)
        const float BootFallAt = 2.9f;      // the case starts falling out from under the monitor
        const float BootLandAt = 3.35f;     // it lands: squash, bounce, neon flare, the monitor's jolt, dust
        const float BootPowerAt = 3.75f, BootDiskAt = 3.85f;  // the POWER and DISK lights
        const float BootLampsAt = 3.95f, BootLampGap = 0.1f;  // the key lamps light one by one (Play, Join game, Wiki)...
        const float BootLampsOutAt = 4.25f; // ...then Join game's and Wiki's go out again
        const float BootProfAt = 4.0f;      // the profile screen's tube comes on (0.3 s), its contents fading in from 0.25 s
        const float BootRattleAt = 4.4f;    // the shutter rattles (0.2 s)
        const float CaseHiddenY = -470f;    // the case's drop before it falls: up behind the monitor

        /// The machine clock: seconds into the disk boot sequence (ProfileTransition.cs), the menu's own entrance from 2.6 s on, or
        /// past the end once it has settled.
        float MachT => profTr && !profEj ? profTrT : MenuIntroPlaying && MenuIntroFull ? BootMenuAt + menuIntroT : 99f;

        /// How far the case is from home (negative: up behind the monitor) and how squashed it is, at boot time T.
        static float CaseDrop(float T, out float squash)
        {
            squash = 1f;
            const float SquashEnd = BootLandAt + 0.1f, BounceEnd = BootLandAt + 0.35f;
            if (T < BootFallAt)
                return CaseHiddenY;
            float fall = Seg(T, BootFallAt, BootLandAt);
            if (T < BootLandAt)
                return Mathf.Lerp(CaseHiddenY, 0f, fall * fall * fall);
            if (T < SquashEnd)
            {
                squash = 1f - 0.035f * Mathf.Sin(Seg(T, BootLandAt, SquashEnd) * Mathf.PI);
                return 0f;
            }
            if (T < BounceEnd)
                return -11.2f * Mathf.Sin(Seg(T, SquashEnd, BounceEnd) * Mathf.PI);
            return 0f;
        }

        /// The monitor's jolt as the case lands (boot time T).
        static void MonitorShakeAt(float T)
        {
            float sh = Seg(T, BootLandAt, BootLandAt + 0.25f), amp = sh > 0f && sh < 1f ? 4f * (1f - sh) : 0f;
            monShakeX = Mathf.Sin(T * 90f) * amp * 0.5f;
            monShakeY = Mathf.Cos(T * 77f) * amp;
        }

        /// The case at boot time T (power: how far its lights are on, 0 to 1, for the eject's power-down). live: its keys take clicks.
        void DrawMachineCase(float T, float drop, float squash, float power, bool live, float t)
        {
            if (drop <= CaseHiddenY + 1f)
                return;
            // Two groups: the outer clips to below the monitor; the inner puts the coordinates back, moved by the drop.
            GUI.BeginGroup(new Rect(0f, CaseClipTop, W, H - CaseClipTop));
            GUI.BeginGroup(new Rect(0f, -CaseClipTop + drop, W, H - drop));
            var oldM = GUI.matrix;
            if (squash != 1f)
                GUIUtility.ScaleAroundPivot(new Vector2(1f, squash), new Vector2(W / 2f, CaseRect.yMax));
            float hit = Seg(T, BootLandAt, BootLandAt + 0.03f) * (1f - Seg(T, BootLandAt + 0.03f, BootLandAt + 0.55f));  // the neon flaring as it lands
            Metal(CaseRect, 12);
            NeonBox(CaseRect, 1f, hit, 12);
            DrawCaseKeys(T, power, live);
            HVent(new Rect(184f, 524f, 210f, 138f));
            DrawBay(T, live, t);
            DrawProfScreen(T, power, live, t);
            HVent(new Rect(886f, 524f, 214f, 138f));
            bool pw = T >= BootPowerAt && power > 0.5f, dk = T >= BootDiskAt && power > 0.5f;
            DrawStrip(pw, dk);
            DrawRoundKeys(T, power, live);
            GUI.matrix = oldM;
            GUI.EndGroup();
            GUI.EndGroup();
        }

        /// Key i's lamp (0 Play, 1 Join game, 2 Wiki) during the power-on: each lights in turn, then Join game's and Wiki's go out
        /// again; settled is its lamp once the menu has settled.
        static int BootLamp(float T, int i, int settled)
        {
            if (T >= MachEnd)
                return settled;
            if (T < BootLampsAt + i * BootLampGap)
                return 0;
            return i > 0 && T >= BootLampsOutAt ? settled : 1;
        }

        /// Play, Join game and Wiki down the case's left.
        void DrawCaseKeys(float T, float power, bool live)
        {
            bool on = power > 0.5f;
            int playLamp = on ? BootLamp(T, 0, 1) : 0;
            int joinLamp = on ? BootLamp(T, 1, menuJoinOpen ? 1 : 0) : 0;
            if (MachKey(PlayKeyRect, "PLAY", 20, live, playLamp, null, true))
            {
                MenuSound(Sfx.MenuPlay);
                if (PacSkin)
                    OpenPacman();  // a Pac profile's Play is the Pac-Man game (PacmanUi.cs)
                else if (menuPlayOpen)
                    PlayPickerBack();
                else
                    OpenPlayPicker();
            }
            if (MachKey(JoinKeyRect, "JOIN GAME", 15, live, joinLamp))
            {
                MenuSound(Sfx.MenuKey);
                SetBay(menuJoinOpen ? BayPage.None : BayPage.Join);
            }
            int wikiLamp = on ? BootLamp(T, 2, menuWikiOpen ? 1 : 0) : 0;
            if (MachKey(WikiKeyRect, "WIKI", 15, live, wikiLamp))
            {
                MenuSound(Sfx.MenuKey);
                ToggleMenuWiki();  // BayWiki.cs: the wiki on the bay's screen
            }
        }

        /// The control strip under the profile screen: a dark recessed plate with a divider in from each end, and between them the POWER
        /// and DISK lights, each over its small label (the keys are drawn over it by DrawRoundKeys).
        static void DrawStrip(bool pw, bool dk)
        {
            var r = StripRect;
            Ui.RoundBox(new Rect(r.x, r.y + 1f, r.width, r.height), new Color(1f, 1f, 1f, 0.07f), 9);  // the lit lip under the recess
            Ui.RoundBox(r, new Color(0.035f, 0.038f, 0.047f), 9);
            Ui.RoundBox(new Rect(r.x + 1f, r.y + 2f, r.width - 2f, r.height - 3f), new Color(0.071f, 0.075f, 0.09f), 8);
            foreach (float x in StripDividers)
            {
                Ui.Fill(new Rect(x, r.y + 9f, 1f, r.height - 18f), new Color(0f, 0f, 0f, 0.55f));
                Ui.Fill(new Rect(x + 1f, r.y + 9f, 1f, r.height - 18f), new Color(1f, 1f, 1f, 0.06f));
            }
            StripLight(966f, "POWER", LampGreen, pw);
            StripLight(1020f, "DISK", LampAmber, dk);
        }

        static readonly float[] StripDividers = { 928f, 1057f };

        static void StripLight(float cx, string label, Color c, bool on)
        {
            Led(cx - 3.5f, StripRect.y + 9f, c, on, 7f);
            Ui.Label(new Rect(cx - 30f, StripRect.y + 20f, 60f, 12f), label, 8, ChinInk.WithAlpha(0.85f), TextAnchor.MiddleCenter, true);
        }

        /// Settings (a cog) and Quit (a power button) at the control strip's ends: small, flat and unlit.
        void DrawRoundKeys(float T, float power, bool live)
        {
            if (FlatRoundKey(SettingsKeyRect, PixelArt.Get("ui_gear"), live))
            {
                MenuSound(Sfx.MenuKey);
                ToggleMenuSettings();  // BaySettings.cs: Settings on the bay's screen
            }
            if (FlatRoundKey(QuitKeyRect, PixelArt.Get("ui_power"), live))
                QuitWithSound();  // MenuHolo.cs: the tube switching off, then quit
        }

        /// What the bay's screen behind the shutter is for.
        enum BayPage { None, Play, Join, Settings, Wiki }
        BayPage bayPage;   // what a key rolled the shutter up onto (None: it is down, or rolling down)
        BayPage bayShown;  // what the screen shows: bayPage, kept while the shutter rolls back down over it
        float bayK;        // how far the shutter is up: 0 shut, 1 open
        const float BayRollLen = 0.9f;  // seconds for the shutter to roll up or down
        bool menuPlayOpen => bayPage == BayPage.Play;
        bool menuJoinOpen => bayPage == BayPage.Join;
        bool menuSetOpen => bayPage == BayPage.Settings;
        bool menuWikiOpen => bayPage == BayPage.Wiki;

        /// Rolls the shutter up onto page p (None: back down; the screen keeps the last page until it's shut).
        void SetBay(BayPage p)
        {
            bayPage = p;
            if (p != BayPage.None)
                bayShown = p;
        }

        /// The shutter down at once, nothing on the screen (a fresh menu: the intro, the boot, the move from the lobby).
        void ShutBay()
        {
            bayPage = bayShown = BayPage.None;
            bayK = 0f;
        }

        /// How far the shutter is rolled up (0 shut, 1 open), with its ease (slow start, quick middle, soft stop).
        float ShutterOpen => EaseInOut(Mathf.Clamp01(bayK));

        /// The bay's screen: the page it shows (the saved runs when none has been picked).
        void DrawBayScreen(float t, bool live)
        {
            switch (bayShown)
            {
                case BayPage.Settings: DrawBaySettings(MenuCard, live); break;
                case BayPage.Wiki: DrawBayWiki(MenuCard, live); break;
                case BayPage.Join: DrawBayJoin(MenuCard, live); break;
                default: DrawPlayPicker(t, live); break;
            }
        }

        /// The bay: its recess, the screen inside (runs or join), the roller shutter over it, and the shadow at its top.
        void DrawBay(float T, bool live, float t)
        {
            var b = BayRect;
            Ui.RoundBox(b, new Color(0.039f, 0.043f, 0.055f), 6);
            if (live && Event.current.type == EventType.Repaint)
            {
                float target = bayPage != BayPage.None ? 1f : 0f;
                float before = bayK;
                bayK = fxOn && !fxReduced ? Mathf.MoveTowards(bayK, target, Time.unscaledDeltaTime / BayRollLen) : target;
                if (before == 0f && bayK > 0f)
                    MenuSound(Sfx.BayServo, 0.55f);
                else if (before == 1f && bayK < 1f)
                    MenuSound(Sfx.BayServoBack, 0.55f);
                else if (before < 1f && bayK >= 1f || before > 0f && bayK <= 0f)
                    MenuSound(Sfx.BayClack, 0.5f);
                if (bayK <= 0f && bayPage == BayPage.None)
                    bayShown = BayPage.None;
            }
            float open = ShutterOpen;
            if (open > 0f)
                DrawBayScreen(t, live && bayK >= 1f && bayPage != BayPage.None);
            // The shutter: ribbed slats rolling up into the top of the bay, a handle at its foot.
            float jig = T > BootRattleAt && T < BootRattleAt + 0.2f ? Mathf.Sin(Seg(T, BootRattleAt, BootRattleAt + 0.2f) * Mathf.PI * 3f) * 2f : 0f;
            GUI.BeginGroup(b);
            float top = -open * b.height + jig;
            if (open < 1f)
            {
                for (float y = 0f; y < b.height; y += 18f)
                {
                    float yy = top + y;
                    Ui.Fill(new Rect(0f, yy, b.width, 9f), new Color(0.353f, 0.365f, 0.396f));
                    Ui.Fill(new Rect(0f, yy + 4.5f, b.width, 4.5f), new Color(0.31f, 0.322f, 0.35f));
                    Ui.Fill(new Rect(0f, yy + 9f, b.width, 7f), new Color(0.263f, 0.275f, 0.302f));
                    Ui.Fill(new Rect(0f, yy + 12f, b.width, 4f), new Color(0.2f, 0.208f, 0.231f));
                    Ui.Fill(new Rect(0f, yy + 16f, b.width, 2f), new Color(0.086f, 0.09f, 0.102f));
                }
                var handle = new Rect(b.width / 2f - 45f, top + b.height - 28f, 90f, 12f);
                Ui.RoundBox(handle, new Color(0.071f, 0.075f, 0.086f), 6);
                Ui.Fill(new Rect(handle.x + 4f, handle.y + 1f, handle.width - 8f, 2f), new Color(0f, 0f, 0f, 0.6f));
                for (int i = 0; i < 8; i++)  // its shadow on whatever is under it
                    Ui.Fill(new Rect(0f, top + b.height + i, b.width, 1f), new Color(0f, 0f, 0f, 0.5f * (1f - i / 8f)));
            }
            for (int i = 0; i < 10; i++)  // the dark under the bay's top edge
                Ui.Fill(new Rect(0f, i, b.width, 1f), new Color(0f, 0f, 0f, 0.9f * (1f - i / 10f)));
            GUI.EndGroup();
        }

        /// Join game, on the bay's screen: the host address to type, Connect, and how joining is going.
        void DrawBayJoin(Rect scr, bool live)
        {
            bool calm = fxReduced;
            CrtUi.Begin(scr);
            bool cursor = !calm && Mathf.FloorToInt(Time.unscaledTime * 2f) % 2 == 0;
            CrtUi.Text(new Rect(scr.x + 18f, scr.y + 12f, scr.width - 36f, 32f), "JOIN GAME" + (cursor ? "_" : " "), 22, CrtStyle.Ink, TextAnchor.MiddleLeft, true, calm);
            Ui.Fill(new Rect(scr.x + 18f, scr.y + 50f, scr.width - 36f, 1f), CrtStyle.Ink.WithAlpha(0.25f));
            if (session.Connecting)
            {
                CrtUi.Text(new Rect(scr.x + 18f, scr.y + 70f, scr.width - 36f, 60f), session.Status, 15, CrtStyle.Ink, TextAnchor.UpperLeft, false, calm);
                if (DkKey(new Rect(scr.x + 18f, scr.yMax - 62f, scr.width - 36f, 44f), "CANCEL", false, calm, size: 16, rawSize: true) && live)
                    session.Leave("");
            }
            else
            {
                CrtUi.Text(new Rect(scr.x + 18f, scr.y + 66f, 200f, 14f), "HOST ADDRESS", 10, CrtStyle.Dim, TextAnchor.MiddleLeft, false, true);
                var field = new Rect(scr.x + 18f, scr.y + 84f, scr.width - 36f, 40f);
                Ui.Fill(field, CrtStyle.Ink.WithAlpha(0.06f));
                EdgeRing(field, 1f, CrtStyle.Ink.WithAlpha(0.45f));
                var oldTint = GUI.contentColor;
                GUI.contentColor = CrtStyle.Ink;
                if (live)
                    addressText = Ui.BareField(new Rect(field.x + 10f, field.y + 6f, field.width - 20f, 28f), addressText, 64, 19);
                else
                    CrtUi.Text(new Rect(field.x + 12f, field.y, field.width - 20f, field.height), addressText, 19, CrtStyle.Ink, TextAnchor.MiddleLeft, false, calm);
                GUI.contentColor = oldTint;
                CrtUi.Text(new Rect(scr.x + 18f, scr.y + 132f, scr.width - 36f, 40f), "The host shares this from the lobby. Same network, or a forwarded port.", 12, CrtStyle.Dim, TextAnchor.UpperLeft, false, calm);
                if (session.Status.Length > 0)
                    CrtUi.Text(new Rect(scr.x + 18f, scr.y + 180f, scr.width - 36f, 60f), session.Status, 13, CrtStyle.Danger, TextAnchor.UpperLeft, false, calm);
                if (DkKey(new Rect(scr.x + 18f, scr.yMax - 62f, scr.width - 36f, 44f), "CONNECT", true, calm, icon: PixelArt.Get("ui_arrow"), size: 16, rawSize: true) && live)
                    JoinGame();
            }
            CrtUi.End(scr, Time.unscaledTime, calm);
        }

        /// Your profile on the case's small screen: a face, your name, rank and the XP to the next one. It switches on like a tube in
        /// the boot (a bright line that opens out), and off again on eject. Click it for your profile.
        void DrawProfScreen(float T, float power, bool live, float t)
        {
            var r = ProfScreenRect;
            DrawGlassScreen(r, 5, 10f, 6f, 0.18f);  // MenuIntro.cs
            float crt = Seg(T, BootProfAt, BootProfAt + 0.3f), shown = Seg(T, BootProfAt + 0.25f, BootProfAt + 0.45f) * power;
            if (crt > 0f && crt < 1f)  // the tube coming on: a line across, then it opens top and bottom
            {
                float w = r.width * EaseOut(crt), h = 2f + (crt > 0.7f ? (crt - 0.7f) / 0.3f * (r.height - 2f) : 0f);
                Ui.RadialGlow(new Rect(r.center.x - w / 2f - 10f, r.center.y - h / 2f - 10f, w + 20f, h + 20f), new Color(0.61f, 0.75f, 1f, 0.6f));
                Ui.Fill(new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h), new Color(0.81f, 0.878f, 1f, crt > 0.7f ? 1f - (crt - 0.7f) / 0.3f : 1f));
            }
            if (power < 1f && power > 0f)  // switching off on eject: it folds to a line
            {
                float k = 1f - power, h = Mathf.Lerp(r.height, 2f, Seg(k, 0f, 0.6f)), w = Mathf.Lerp(r.width, 0f, Seg(k, 0.6f, 1f));
                Ui.Fill(new Rect(r.center.x - w / 2f, r.center.y - h / 2f, w, h), new Color(0.81f, 0.878f, 1f, 0.9f));
            }
            if (shown <= 0f)
                return;
            var p = Profile.Current;
            bool hover = live && r.Contains(Event.current.mousePosition);
            WithAlpha(shown, () =>
            {
                if (hover)
                    Ui.RoundBox(r, CrtStyle.Ink.WithAlpha(0.06f), 5);
                var face = new Rect(r.x + 16f, r.y + 22f, 42f, 42f);
                Ui.Dot(face, new Color(0.345f, 0.447f, 0.722f));
                Ui.Dot(new Rect(face.x + 6f, face.y + 4f, 26f, 24f), new Color(0.79f, 0.84f, 1f, 0.55f));
                if (BeginUiGl())
                {
                    UiModelAt(new V2(face.center.x, face.center.y), 0.62f);
                    DrawGenericPlayer(t);  // RankUi.cs
                    EndUiGl();
                }
                string name = nameText.Length > 0 ? nameText : p.Name;
                int size = 19;
                while (size > 11 && Ui.TextWidth(name, size, true) + 6f > r.width - 86f)
                    size--;
                Ui.Label(new Rect(r.x + 70f, r.y + 22f, r.width - 80f, 24f), name, size, new Color(0.918f, 0.941f, 1f), TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(r.x + 70f, r.y + 46f, r.width - 80f, 16f), Rank.Title(p.RankNow).ToUpperInvariant() + " · RANK " + p.RankNow, 10, new Color(0.494f, 0.584f, 0.788f), TextAnchor.MiddleLeft, true);
                var bar = new Rect(r.x + 16f, r.y + 84f, r.width - 32f, 4f);
                Ui.Fill(bar, new Color(0.078f, 0.125f, 0.231f));
                Ui.Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(Rank.Of(p.Xp).Fraction), bar.height), new Color(0.4f, 0.639f, 1f));
                Ui.Label(new Rect(r.x + 16f, r.yMax - 34f, r.width - 32f, 18f), "YOUR PROFILE  ›", 10, new Color(0.494f, 0.584f, 0.788f, hover ? 1f : 0.6f), TextAnchor.MiddleRight, true);
            });
            if (live && T >= MachEnd)
            {
                DrawProfileHint(r, t, true);  // ProfileUi.cs: the YOUR PROFILE nudge, until it has been opened
                if (Ui.HitArea(r))
                    OpenProfile();
            }
        }

        /// Dust off the case's feet as it lands: soft puffs blowing out along the floor either side and fading (boot time T).
        static void DrawLandingDust(float T)
        {
            float dk = Seg(T, BootLandAt, BootLandAt + 0.6f);
            if (dk <= 0f || dk >= 1f)
                return;
            for (int i = 0; i < 10; i++)
            {
                float side = i < 5 ? -1f : 1f, k = (i % 5) / 5f;
                float x = W / 2f + side * (470f + dk * (60f + 80f * k)), y = CaseRect.yMax - 6f - dk * (10f + 24f * k);
                float sz = 14f * (1f + dk * 2.5f) + 8f;
                Ui.RadialGlow(new Rect(x - sz, y - sz, sz * 2f, sz * 2f), new Color(1f, 0.78f, 0.82f, 0.55f * (1f - dk)));
            }
        }

        static readonly (float At, Sfx Sfx, float Vol)[] MachCues =
        {
            (BootFallAt, Sfx.BossWhoosh, 0.35f), (BootLandAt, Sfx.StoneSlam, 0.6f), (BootLandAt + 0.02f, Sfx.LaunchLand, 0.45f),
            (BootPowerAt, Sfx.BayClack, 0.55f), (BootDiskAt, Sfx.BayTick, 0.5f),
            (BootLampsAt, Sfx.BayTick, 0.45f), (BootLampsAt + BootLampGap, Sfx.BayTick, 0.45f), (BootLampsAt + 2f * BootLampGap, Sfx.BayTick, 0.45f),
            (BootProfAt, Sfx.CrtPower, 0.5f), (BootRattleAt, Sfx.BayClamp, 0.35f),
        };

        /// The machine's sounds for boot time a to b (going forward): the case's drop and landing, POWER, DISK, the key lamps, the
        /// profile screen coming on, the shutter's rattle.
        void PlayMachineCues(float a, float b)
        {
            if (b <= a || sounds == null)
                return;
            float now = Time.unscaledTime;
            foreach (var c in MachCues)
                if (a < c.At && c.At <= b)
                    sounds.Play(c.Sfx, c.Vol, now);
        }
    }
}

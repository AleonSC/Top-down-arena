using UnityEngine;

namespace Arena
{
    /// The main menu's case (design B of Docs/Mockups/main-menu-machines.html, its entrance from Docs/Mockups/disk-boot-sequence.html): a
    /// graphite case in a neon outline under the monitor, with Play, Join game, Settings and Quit as keycaps down its left, a bay in the
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
        static readonly Rect SettingsKeyRect = new Rect(184f, 450f, 210f, 52f), QuitKeyRect = new Rect(184f, 622f, 210f, 40f);
        static readonly Rect BayRect = new Rect(424f, 288f, 432f, 376f);
        static readonly Rect ProfScreenRect = new Rect(886f, 290f, 214f, 150f);
        const float MachEnd = 4.8f;       // the boot sequence's length; past it the menu is settled

        /// The machine clock: seconds into the disk boot sequence (ProfileTransition.cs), the menu's own entrance from 2.6 s on, or
        /// past the end once it has settled.
        float MachT => profTr && !profEj ? profTrT : MenuIntroPlaying && MenuIntroFull ? 2.6f + menuIntroT : 99f;

        /// How far the case is from home (negative: up behind the monitor) and how squashed it is, at boot time T.
        static float CaseDrop(float T, out float squash)
        {
            squash = 1f;
            if (T < 2.9f)
                return -470f;
            float fall = Seg(T, 2.9f, 3.35f);
            if (T < 3.35f)
                return Mathf.Lerp(-470f, 0f, fall * fall * fall);
            if (T < 3.45f)
            {
                squash = 1f - 0.035f * Mathf.Sin(Seg(T, 3.35f, 3.45f) * Mathf.PI);
                return 0f;
            }
            if (T < 3.7f)
                return -11.2f * Mathf.Sin(Seg(T, 3.45f, 3.7f) * Mathf.PI);
            return 0f;
        }

        /// The monitor's jolt as the case lands (boot time T).
        static void MonitorShakeAt(float T)
        {
            float sh = Seg(T, 3.35f, 3.6f), amp = sh > 0f && sh < 1f ? 4f * (1f - sh) : 0f;
            monShakeX = Mathf.Sin(T * 90f) * amp * 0.5f;
            monShakeY = Mathf.Cos(T * 77f) * amp;
        }

        /// The case at boot time T (power: how far its lights are on, 0 to 1, for the eject's power-down). live: its keys take clicks.
        void DrawMachineCase(float T, float drop, float squash, float power, bool live, float t)
        {
            if (drop <= -469f)
                return;
            // Two groups: the outer clips to below the monitor; the inner puts the coordinates back, moved by the drop.
            GUI.BeginGroup(new Rect(0f, CaseClipTop, W, H - CaseClipTop));
            GUI.BeginGroup(new Rect(0f, -CaseClipTop + drop, W, H - drop));
            var oldM = GUI.matrix;
            if (squash != 1f)
                GUIUtility.ScaleAroundPivot(new Vector2(1f, squash), new Vector2(W / 2f, CaseRect.yMax));
            float hit = Seg(T, 3.35f, 3.38f) * (1f - Seg(T, 3.38f, 3.9f));
            Metal(CaseRect, 12);
            NeonBox(CaseRect, 1f, hit, 12);
            DrawCaseKeys(T, power, live);
            HVent(new Rect(184f, 524f, 210f, 76f));
            DrawBay(T, live, t);
            DrawProfScreen(T, power, live, t);
            HVent(new Rect(886f, 466f, 214f, 120f));
            bool pw = T >= 3.75f && power > 0.5f, dk = T >= 3.85f && power > 0.5f;
            Led(892f, 632f, LampGreen, pw);
            ChinText(new Rect(906f, 627f, 60f, 16f), "POWER", 10, ChinInk);
            Led(972f, 632f, LampAmber, dk);
            ChinText(new Rect(986f, 627f, 60f, 16f), "DISK", 10, ChinInk);
            GUI.matrix = oldM;
            GUI.EndGroup();
            GUI.EndGroup();
        }

        /// The key lamps during the power-on: each lights in turn, then Join and Settings go out again.
        static int BootLamp(float T, float on, int settled)
        {
            if (T >= MachEnd)
                return settled;
            if (T < on)
                return 0;
            return on > 4f && T >= 4.25f ? settled : 1;
        }

        /// Play, Join game, Settings and Quit down the case's left.
        void DrawCaseKeys(float T, float power, bool live)
        {
            bool on = power > 0.5f;
            int playLamp = on ? BootLamp(T, 3.95f, 1) : 0;
            int joinLamp = on ? BootLamp(T, 4.05f, menuJoinOpen ? 1 : 0) : 0;
            int setLamp = on ? BootLamp(T, 4.15f, menuSetOpen ? 1 : 0) : 0;
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
                menuJoinOpen = !menuJoinOpen;
                if (menuJoinOpen)
                    menuPlayOpen = menuSetOpen = false;
            }
            if (MachKey(SettingsKeyRect, "SETTINGS", 15, live, setLamp))
            {
                MenuSound(Sfx.MenuKey);
                ToggleMenuSettings();  // BaySettings.cs: Settings on the bay's screen
            }
            if (MachKey(QuitKeyRect, "QUIT", 13, live))
                QuitWithSound();  // MenuHolo.cs: the tube switching off, then quit
        }

        /// How far the shutter is rolled up (0 shut, 1 open), with its ease (slow start, quick middle, soft stop).
        float ShutterOpen => EaseInOut(Mathf.Clamp01(menuPlayK));

        /// The bay: its recess, the screen inside (runs or join), the roller shutter over it, and the shadow at its top.
        void DrawBay(float T, bool live, float t)
        {
            var b = BayRect;
            Ui.RoundBox(b, new Color(0.039f, 0.043f, 0.055f), 6);
            if (live && Event.current.type == EventType.Repaint)
            {
                float target = menuPlayOpen || menuJoinOpen || menuSetOpen ? 1f : 0f;
                float before = menuPlayK;
                menuPlayK = fxOn && !fxReduced ? Mathf.MoveTowards(menuPlayK, target, Time.unscaledDeltaTime / MenuPlayLen) : target;
                if (before == 0f && menuPlayK > 0f)
                    MenuSound(Sfx.BayServo, 0.55f);
                else if (before == 1f && menuPlayK < 1f)
                    MenuSound(Sfx.BayServoBack, 0.55f);
                else if (before < 1f && menuPlayK >= 1f || before > 0f && menuPlayK <= 0f)
                    MenuSound(Sfx.BayClack, 0.5f);
            }
            float open = ShutterOpen;
            if (open > 0f)
            {
                bool screenLive = live && menuPlayK >= 1f;
                if (menuSetOpen)
                    DrawBaySettings(MenuCard, screenLive);
                else if (menuJoinOpen)
                    DrawBayJoin(MenuCard, screenLive);
                else
                    DrawPlayPicker(t, screenLive);
            }
            // The shutter: ribbed slats rolling up into the top of the bay, a handle at its foot.
            float jig = T > 4.4f && T < 4.6f ? Mathf.Sin(Seg(T, 4.4f, 4.6f) * Mathf.PI * 3f) * 2f : 0f;
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
            Ui.RoundBox(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), new Color(0.039f, 0.043f, 0.055f), 7);
            Ui.RoundBox(r, CrtUi.T(new Color(0.016f, 0.039f, 0.094f)), 5);
            Ui.RadialGlow(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, r.height - 12f), CrtUi.T(new Color(0.235f, 0.43f, 0.86f)).WithAlpha(0.18f));
            EdgeRing(new Rect(r.x + 1f, r.y + 1f, r.width - 2f, r.height - 2f), 1f, CrtUi.T(new Color(0.102f, 0.141f, 0.251f)));
            float crt = Seg(T, 4.0f, 4.3f), shown = Seg(T, 4.25f, 4.45f) * power;
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
            float dk = Seg(T, 3.35f, 3.95f);
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

        static readonly Sfx[] MachCueSfx = { Sfx.BossWhoosh, Sfx.StoneSlam, Sfx.LaunchLand, Sfx.BayClack, Sfx.BayTick, Sfx.BayTick, Sfx.BayTick, Sfx.BayTick, Sfx.CrtPower, Sfx.BayClamp };
        static readonly float[] MachCueAt = { 2.9f, 3.35f, 3.37f, 3.75f, 3.85f, 3.95f, 4.05f, 4.15f, 4.0f, 4.4f };
        static readonly float[] MachCueVol = { 0.35f, 0.6f, 0.45f, 0.55f, 0.5f, 0.45f, 0.45f, 0.45f, 0.5f, 0.35f };

        /// The machine's sounds for boot time a to b (going forward): the case's drop and landing, POWER, DISK, the key lamps, the
        /// profile screen coming on, the shutter's rattle.
        void PlayMachineCues(float a, float b)
        {
            if (b <= a || sounds == null)
                return;
            float now = Time.unscaledTime;
            for (int i = 0; i < MachCueAt.Length; i++)
                if (a < MachCueAt[i] && MachCueAt[i] <= b)
                    sounds.Play(MachCueSfx[i], MachCueVol[i], now);
        }
    }
}

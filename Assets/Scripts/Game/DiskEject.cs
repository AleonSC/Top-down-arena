using UnityEngine;

namespace Arena
{
    /// The eject key beside the drive in the monitor's chin, and the way back to Who's playing? it plays (the disk boot sequence in
    /// reverse, about 2.7 s). Its clock `profEjT` runs in seconds from the click:
    ///   0.0 s   the key sinks; the case powers down (its lights, lamps and profile screen go out, 0.1 to 0.35 s)
    ///   0.2 s   the drive's light turns amber and the cells drain (EJECTING <name>)
    ///   0.4 s   the case lifts back up under the monitor (to 0.85 s)
    ///   0.5 s   latch, slit flash: the disk kicks out of the slit about 40 % and bounces on a spring (TAKE DISK blinking), then comes
    ///           the rest of the way out, tipping upright (1.4 to 1.7 s)
    ///   0.9 s   VECTOR SECTOR: ARENA backspaces away, WHO'S PLAYING? types on (1.5 to 2.1 s)
    ///   1.7 s   the disk drops back to the middle of the carousel (2.4 s), the rest of the carousel fading in round it (1.9 to 2.6 s)
    /// It rides on the profile move's flags (`profTr` with `profEj`), so the menu takes no clicks and any key skips it.
    /// Reduced effects or Visual effects off: no animation, the picker just opens. Drawing and sound only, no protocol change.
    public partial class ArenaApp
    {
        const float EjLen = 2.7f, EjPopAt = 0.5f, EjOutAt = 1.4f, EjFlyAt = 1.7f, EjLandAt = 2.4f;

        bool profEj;               // the eject move is playing (profTr is set too)
        float profEjT, profEjPrev;
        float ejHover;             // 0 to 1: the key's hover glow

        static Rect EjectKeyRect => new Rect(DriveX0 + DriveW + 12f, DriveY - 14f, 34f, 28f);

        bool EjectAvailable => ProfileStore.Root != null && ProfileStore.ActiveId.Length > 0 && Profile.Current.Name.Length > 0;

        /// The eject key, right of the drive (on the menu's monitor). live: it takes clicks.
        void DrawDriveEject(float alpha, bool live)
        {
            if (alpha <= 0f)
                return;
            var r = EjectKeyRect;
            bool usable = live && !profTr && EjectAvailable;
            bool hot = usable && r.Contains(Event.current.mousePosition);
            if (Event.current.type == EventType.Repaint)
                ejHover = Mathf.MoveTowards(ejHover, hot ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            float press = profEj ? Seg(profEjT, 0f, 0.08f) * (1f - Seg(profEjT, 0.2f, 0.3f)) : 0f;
            WithAlpha(alpha, () =>
            {
                float dy = 3f * press, h = ejHover;
                Ui.RoundBox(new Rect(r.x, r.y + 4f, r.width, r.height), new Color(0.086f, 0.09f, 0.102f), 5);
                var cap = new Rect(r.x, r.y + dy, r.width, r.height);
                Ui.RoundBox(cap, new Color(0.043f, 0.047f, 0.059f), 5);
                Ui.RoundGrad(new Rect(cap.x + 1.5f, cap.y + 1.5f, cap.width - 3f, cap.height - 3f), 4f, new[] { 0f, 0.6f, 1f },
                    new[] { Color.Lerp(new Color(0.302f, 0.314f, 0.345f), Color.white, 0.12f * h), new Color(0.173f, 0.18f, 0.204f), new Color(0.129f, 0.133f, 0.153f) });
                Ui.Fill(new Rect(cap.x + 5f, cap.y + 1.5f, cap.width - 10f, 1f), new Color(1f, 1f, 1f, 0.35f));
                DrawEjectGlyph(cap.center.x, cap.center.y + 1f, Color.Lerp(new Color(0.81f, 0.878f, 1f, usable || profEj ? 0.85f : 0.35f), Color.white, h));
                if (h > 0.3f && !profEj)  // the tooltip, under the key
                {
                    var tip = new Rect(r.center.x - 43f, r.yMax + 8f, 86f, 22f);
                    WithAlpha(h, () =>
                    {
                        Ui.RoundBox(tip, new Color(0.07f, 0.075f, 0.09f, 0.95f), 4);
                        EdgeRing(tip, 1f, MachNeon.WithAlpha(0.5f));
                        Ui.Label(tip, "EJECT DISK", 11, MachNeon, TextAnchor.MiddleCenter, true);
                    });
                }
            });
            if (usable && Ui.HitArea(r))
                StartProfEject();
        }

        /// The eject symbol: a triangle over a bar, 14 px wide.
        static void DrawEjectGlyph(float cx, float cy, Color c)
        {
            for (int k = 0; k < 8; k++)
                Ui.Fill(new Rect(cx - k - 0.5f, cy - 8f + k, 2f * k + 1f, 1f), c);
            Ui.Fill(new Rect(cx - 7f, cy + 3f, 14f, 3f), c);
        }

        /// The eject key was clicked: play the way back, or just show the picker.
        void StartProfEject()
        {
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == ProfileStore.ActiveId);
            if (!ProfTrFull || slot < 0)
            {
                OpenWho();
                return;
            }
            whoManage = false;
            whoEdit = null;
            whoError = "";
            whoArmed = null;
            profTr = true;
            profEj = true;
            profEjT = 0f;
            profEjPrev = -0.0001f;
            profTrAge = 0f;
            profTrSlot = slot;
            whoCarPos = whoCarTarget = slot;
            menuPlayOpen = menuJoinOpen = menuSetOpen = menuWikiOpen = false;
            menuIntroT = MenuIntroLen - 0.001f;  // the menu takes no clicks
        }

        /// Update (from TickProfTr): run the clock and its sounds.
        void TickProfEj(float dt)
        {
            profTrAge += dt;
            if (Input.anyKeyDown && profTrAge > 0.25f)
            {
                profEjT = EjLen;  // skipped: straight to the end, silently
                StartSkipStatic();  // under the channel flip (SkipStatic.cs)
            }
            else
            {
                profEjT = Mathf.Min(EjLen, profEjT + dt);
                PlayProfEjCues(profEjPrev, profEjT);
            }
            profEjPrev = profEjT;
            menuIntroT = MenuIntroLen - 0.001f;
            if (profEjT >= EjLen)
            {
                profTr = false;
                profEj = false;
                menuIntroT = MenuIntroLen;
                OpenWho();
            }
        }

        static readonly Sfx[] EjCueSfx = { Sfx.MenuEject, Sfx.BayClack, Sfx.BossWhoosh, Sfx.DiskClick, Sfx.DiskEject, Sfx.DiskFly, Sfx.DiskLift };
        static readonly float[] EjCueAt = { 0f, 0.15f, 0.4f, 0.45f, 0.5f, 1.7f, 2.4f };
        static readonly float[] EjCueVol = { 0.7f, 0.45f, 0.3f, 0.8f, 0.7f, 0.6f, 0.5f };

        void PlayProfEjCues(float a, float b)
        {
            if (b <= a || sounds == null)
                return;
            float now = Time.unscaledTime;
            for (int i = 0; i < EjCueAt.Length; i++)
                if (a < EjCueAt[i] && EjCueAt[i] <= b)
                    sounds.Play(EjCueSfx[i], EjCueVol[i], now);
            TitleFlapCues(CrtTitleText, a, b, 0.9f, 1.4f, false);
            TitleFlapCues(WhoTitleText, a, b, 1.5f, 2.1f, true);
        }

        /// The drive for the eject clock E: the cells drain with an amber light, a flash as the disk kicks out, TAKE DISK blinking, then empty.
        static DriveState DriveEjectAt(float E, string name)
        {
            if (E < 0.2f)
                return DriveLoaded(name);
            if (E >= EjFlyAt)
                return DriveEmpty("INSERT: " + name.ToUpperInvariant());
            var s = DriveEmpty();
            s.ledOn = true;
            if (E < EjPopAt)
            {
                s.ledOn = Mathf.FloorToInt(E * 14f) % 2 == 0;
                s.prog = 1f - Seg(E, 0.2f, EjPopAt);
                s.text = "EJECTING " + name.ToUpperInvariant();
            }
            else
            {
                s.flash = E < 0.8f ? 1f - Seg(E, EjPopAt, 0.8f) : 0f;
                s.text = Mathf.FloorToInt(E * 4f) % 2 == 1 ? "TAKE DISK" : "TAKE DISK  >";
            }
            return s;
        }

        /// How far out of the slit the disk is (0 to 1): a spring kick to about 0.4 with a bounce, a hang, then the rest.
        static float EjOut(float E)
        {
            if (E < EjOutAt)
            {
                float tau = Mathf.Max(0f, E - EjPopAt);
                return 0.4f * (1f - Mathf.Exp(-9f * tau) * Mathf.Cos(16f * tau));
            }
            return Mathf.Lerp(0.4f, 1f, EaseInOut(Seg(E, EjOutAt, EjFlyAt)));
        }

        /// The monitor's title during the eject: VECTOR SECTOR: ARENA backspacing away, then WHO'S PLAYING? typed.
        static MonTitle EjectTitleAt(float E)
        {
            if (E < 0.9f)
                return TitleAll(CrtTitleText);
            if (E < 1.4f)
                return new MonTitle { text = CrtTitleText, typed = Mathf.RoundToInt(CrtTitleText.Length * (1f - Seg(E, 0.9f, 1.4f))), flat = true };
            if (E < 1.5f)
                return new MonTitle { text = WhoTitleText, typed = 0, flat = true };
            if (E < 2.1f)
                return new MonTitle { text = WhoTitleText, typed = Mathf.FloorToInt(WhoTitleText.Length * Seg(E, 1.5f, 2.1f)), flat = true };
            return TitleAll(WhoTitleText);
        }

        /// The whole screen during the eject: the case powering down and lifting away, the monitor retyping, and the disk popping out
        /// of the slit and dropping back onto the carousel.
        void DrawProfEj()
        {
            float E = profEjT, t = Time.unscaledTime;
            var info = WhoAt(profTrSlot);
            float power = 1f - Seg(E, 0.1f, 0.35f), lift = Seg(E, 0.4f, 0.85f);
            DrawMachineCase(99f, -470f * lift * lift, 1f, power, false, t);
            var title = EjectTitleAt(E);
            var S = DriveForMenu();
            DrawMonitor(title, S, E < 0.85f, false);  // (the eject key goes with the case)
            DrawWhoStill(Seg(E, 1.9f, 2.6f));
            if (info == null || E < EjPopAt)
            {
                DrawDriveLip(1f, S);
                return;
            }
            float size = CarSize * 0.77f;
            if (E < EjFlyAt)
            {
                float k = 1f - EjOut(E);  // 1: in, 0: out and upright below the slit
                float top = Mathf.Lerp(232f, DriveY - size * 0.6f, k * k * k), tilt = 62f * k;
                DrawTiltedDisk(info, DriveSlitCx, top, size, tilt, 0f);
                DrawMonitorAbove(DriveY - 4f, title, S);
            }
            else
            {
                float c = EaseInOut(Seg(E, EjFlyAt, EjLandAt));
                DrawTiltedDisk(info, Mathf.Lerp(DriveSlitCx, CarCx, c), Mathf.Lerp(232f, CarCy - CarSize / 2f, c), Mathf.Lerp(size, CarSize, c), 0f, 0f);
            }
            DrawDriveLip(1f, S);
        }
    }
}

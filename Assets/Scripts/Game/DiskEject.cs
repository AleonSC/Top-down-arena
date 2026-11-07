using UnityEngine;

namespace Arena
{
    /// The eject key on the disk drive (look A, "Keycap", of Docs/Mockups/disk-eject.html) and the spring pop it plays (animation B): about 2.9 s,
    /// the way back to the "Who's playing?" picker (it replaced the menu's "Switch profile" tab). Its clock `profEjT` runs in seconds from the click:
    ///   0.0 s   the key sinks; 0.2 s the drive's light turns amber and the four cells drain (EJECTING <name>)
    ///   0.5 s   latch click, slit flash; the disk kicks out of the slit about 40 % and bounces on a spring; the readout blinks TAKE DISK
    ///   1.4 s   it slides the rest of the way out (1.7 s), the menu fades
    ///   1.7 s   it flies to its cradle on the shelf (2.6 s); the picker fades in from 1.9 s; the clamps close (2.6 to 2.9 s)
    /// It rides on the profile move's flags (`profTr` with `profEj`) so the menu counts as still in its intro (nothing clicks) and any key skips it.
    /// Reduced effects or Visual effects off: no animation, the picker just opens. Drawing and sound only, no protocol change.
    public partial class ArenaApp
    {
        const float EjLen = 2.9f;
        const float EjPopAt = 0.5f, EjOutAt = 1.4f, EjFlyAt = 1.7f, EjLandAt = 2.6f;

        bool profEj;               // the eject move is playing (profTr is set too)
        float profEjT, profEjPrev;
        float ejHover;             // 0 to 1: the key's hover glow

        static Rect EjectKeyRect => new Rect(W / 2f + DriveW / 2f + 12f, DriveY - 15f, 38f, 30f);

        bool EjectAvailable => ProfileStore.Root != null && ProfileStore.ActiveId.Length > 0 && Profile.Current.Name.Length > 0;

        /// The eject key, right of the drive on the menu (called from DrawMenu). live: it takes clicks (the menu isn't in its intro).
        void DrawDriveEject(float alpha, bool live)
        {
            if (alpha <= 0f || !EjectAvailable || (profTr && !profEj))
                return;
            var r = EjectKeyRect;
            var mouse = Event.current.mousePosition;
            bool hot = live && r.Contains(mouse);
            if (Event.current.type == EventType.Repaint)
                ejHover = Mathf.MoveTowards(ejHover, hot ? 1f : 0f, Time.unscaledDeltaTime * 8f);
            float press = profEj ? Seg(profEjT, 0f, 0.08f) * (1f - Seg(profEjT, 0.2f, 0.3f)) : 0f;
            bool spent = profEj && profEjT > 0.3f;  // the key has been used: dimmed while the disk comes out
            WithAlpha(alpha, () =>
            {
                float dy = 2f * press, h = ejHover;
                Ui.Pill(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0f, 0f, 0f, 0.4f));
                Ui.Pill(new Rect(r.x, r.y + dy, r.width, r.height), Color.Lerp(new Color(0.106f, 0.149f, 0.337f), new Color(0.165f, 0.227f, 0.47f), h));
                Ui.Rim(new Rect(r.x, r.y + dy, r.width, r.height), Color.Lerp(new Color(0.45f, 0.65f, 1f), ScanColor, h), 0.55f + 0.45f * h);
                if (press < 0.5f)
                    Ui.Fill(new Rect(r.x + 8f, r.y + 2f, r.width - 16f, 1f), new Color(0.75f, 0.83f, 1f, 0.3f));
                DrawEjectGlyph(r.center.x, r.center.y + dy, Color.Lerp(new Color(0.75f, 0.83f, 1f, spent ? 0.35f : 0.85f), Color.white, h));
                if (h > 0.3f && !profEj)  // the tooltip, under the key
                {
                    var tip = new Rect(r.center.x - 43f, r.yMax + 8f, 86f, 22f);
                    WithAlpha(h, () =>
                    {
                        Ui.Pill(tip, new Color(0.043f, 0.067f, 0.19f));
                        Ui.Rim(tip, new Color(0.45f, 0.65f, 1f), 0.5f);
                        Ui.Label(tip, "EJECT DISK", 11, new Color(0.81f, 0.88f, 1f), TextAnchor.MiddleCenter, true);
                    });
                }
            });
            if (live && !profTr && Ui.HitArea(r))
                StartProfEject();
        }

        /// The eject symbol: a triangle over a bar, 14 px wide.
        static void DrawEjectGlyph(float cx, float cy, Color c)
        {
            for (int k = 0; k < 8; k++)
                Ui.Fill(new Rect(cx - k - 0.5f, cy - 8f + k, 2f * k + 1f, 1f), c);
            Ui.Fill(new Rect(cx - 7f, cy + 3f, 14f, 3f), c);
        }

        /// The eject key was clicked: play the spring pop, or just show the picker.
        void StartProfEject()
        {
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == ProfileStore.ActiveId);
            if (!ProfTrFull || slot < 0 || slot >= WhoSlots)
            {
                OpenWho();
                return;
            }
            whoManage = false;
            whoEdit = null;
            whoError = "";
            whoArmed = null;
            profTr = true;
            profTrFwd = false;
            profEj = true;
            profEjT = 0f;
            profEjPrev = -0.0001f;
            profTrAge = 0f;
            profTrSlot = slot;
            menuIntroT = MenuIntroLen - 0.001f;  // the menu stays fully drawn, and takes no clicks
        }

        /// Update (from TickProfTr): run the clock and its sounds.
        void TickProfEj(float dt)
        {
            profTrAge += dt;
            if (Input.anyKeyDown && profTrAge > 0.25f)
                profEjT = EjLen;  // skipped: straight to the end, silently
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
                whoLift[profTrSlot] = 0f;
            }
        }

        static readonly (float t, Sfx sfx, float vol)[] EjCues =
        {
            (0.00f, Sfx.DiskClick, 0.6f),   // the key
            (0.45f, Sfx.DiskClick, 0.8f),   // the latch lets go
            (0.50f, Sfx.DiskEject, 0.7f),   // the spring kicks the disk out
            (1.70f, Sfx.DiskFly, 0.6f),     // it flies to the shelf
            (2.60f, Sfx.DiskLift, 0.5f),    // the clamps close on it
        };

        void PlayProfEjCues(float a, float b)
        {
            float now = Time.unscaledTime;
            foreach (var c in EjCues)
                if (a < c.t && c.t <= b)
                    sounds.Play(c.sfx, c.vol, now);
        }

        /// The drive for the eject clock E: the cells drain with an amber light, a flash as the disk kicks out, TAKE DISK blinking, then empty.
        static DriveState DriveEjectAt(float E, string name, Color col)
        {
            if (E < 0.2f)
                return DriveLoaded(name, col);
            if (E >= EjFlyAt)
                return DriveEmpty();
            var s = DriveEmpty();
            s.glowCol = col;
            s.led = DriveAmber;
            s.ledA = 1f;
            if (E < EjPopAt)
            {
                s.ledA = 0.65f + 0.35f * Mathf.Sin(E * 16f);
                s.prog = 1f - Seg(E, 0.2f, EjPopAt);
                s.glow = 0.4f * s.prog;
                s.text = "EJECTING " + name.ToUpperInvariant();
            }
            else
            {
                s.glow = 0.7f * (1f - Seg(E, EjPopAt, 1.0f));
                s.flash = E < 0.8f ? 1f - Seg(E, EjPopAt, 0.8f) : 0f;
                s.text = Mathf.FloorToInt(E * 4f) % 2 == 1 ? "TAKE DISK" : "TAKE DISK  >";
            }
            return s;
        }

        /// How far out of the slit the disk is, as a share of its height (0 to 1): a spring kick to about 0.4 with a bounce, a hang, then the rest.
        static float EjOut(float E)
        {
            if (E < EjOutAt)
            {
                float tau = Mathf.Max(0f, E - EjPopAt);
                return 0.4f * (1f - Mathf.Exp(-9f * tau) * Mathf.Cos(16f * tau));
            }
            return Mathf.Lerp(0.4f, 1f, EaseInOut(Seg(E, EjOutAt, EjFlyAt)));
        }

        /// The whole screen during the eject: the menu fading out, the picker fading in, the drive staying put, and the disk
        /// popping out of the slit and flying to its cradle.
        void DrawProfEj()
        {
            float E = profEjT, t = Time.unscaledTime;
            var info = WhoInSlot(profTrSlot);
            var R = WhoBox(profTrSlot);
            float menuA = 1f - Seg(E, EjOutAt, 2.1f), whoA = Seg(E, 1.9f, 2.6f);
            whoLift[profTrSlot] = 1f - Seg(E, EjLandAt, EjLen);  // its clamps stay open until it lands
            if (menuA > 0f)
                WithAlpha(menuA, DrawMenu);
            if (whoA > 0f)
                WithAlpha(whoA, () =>
                {
                    Ui.Glitch(new Rect(0f, MenuTitleY - 56f, W, 80f), "WHO'S PLAYING?", 58, Color.white, TextAnchor.MiddleCenter, Ui.Accent);
                    DrawWhoShelf();
                    for (int i = 0; i <= WhoSlots; i++)
                        if (i != profTrSlot)
                            DrawWhoBoxStatic(i, WhoBox(i), t);
                });
            var S = DriveForMenu();
            DrawDriveSlot(1f, S);  // the drive stays at full strength while the menu fades behind it
            if (info == null || E < EjPopAt)
            {
                DrawDriveLip(1f, S);
                return;
            }
            float D = DriveDiskSize, cx = W / 2f - D / 2f, slitY = DriveY + 4f;
            if (E < EjFlyAt)
            {
                float y = slitY - D + EjOut(E) * D;
                // Behind the drive's face: only the part below the slit shows, so it comes out of it.
                GUI.BeginGroup(new Rect(0f, slitY, W, H - slitY));
                DrawWhoDisk(new Rect(cx, y - slitY, D, D), info, true);
                GUI.EndGroup();
            }
            else
            {
                float f = EaseInOut(Seg(E, EjFlyAt, EjLandAt));
                float s = Mathf.Lerp(D, R.width, f);
                DrawWhoDisk(new Rect(Mathf.Lerp(cx, R.x, f), Mathf.Lerp(slitY, R.y, f), s, s), info, true);
                if (E >= EjLandAt)  // landed: its box (the name under it) fades in over the disk
                    WithAlpha(Seg(E, EjLandAt, EjLen), () => DrawWhoBoxStatic(profTrSlot, R, t));
            }
            DrawDriveLip(1f, S);
        }
    }
}

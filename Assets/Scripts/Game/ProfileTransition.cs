using UnityEngine;

namespace Arena
{
    /// The move between "Who's playing?" and the main menu (Docs/Mockups/floppy-slot.html: the drive bay, B): about 5.4 s. Picking a profile plays
    /// it forward; the menu's eject key (DiskEject.cs) has its own move back. Its clock `profTrT` runs in seconds:
    ///   0.1 s   your disk lifts and the clamps open; 0.5 s a ring on click
    ///   0.8 s   WHO'S PLAYING?, the shelf and the other disks fade; yours flies up to the drive under the title (0.9 to 2.0 s)
    ///   2.0 s   it tips flat from its top edge onto the slit, then slides in (2.5 s); the lip flashes and the latch clicks (2.95 s)
    ///   2.95 s  the drive reads it (four cells fill, readout) until 3.9 s: READY
    ///   3.0 s   the menu's CRT screen drops in and the title types onto it (the menu's own intro, MenuIntro.cs, run from a remapped clock: ProfTrToIntro)
    ///   4.05 s  the pen draws the arc over the title; 4.25 s the box opens; the buttons drop in; the bar slides up
    /// There are always four boxes (three slots and New profile, ProfilesUi.cs), so every move is the same. Nothing can be
    /// clicked meanwhile (the menu counts as still in its intro), and any key skips to the end. Reduced effects or Visual
    /// effects off: no move, the screens just change (and the menu's short fade plays).
    public partial class ArenaApp
    {
        const float ProfTrLen = 5.4f, ProfTrSpeed = 1f;
        const float ProfTrPenAt = 3.0f, ProfTrPenRate = 1f;  // menu intro second 0.35 happens at 3.0 s; each intro second takes 1 s of the clock

        bool profTr;               // the move is playing
        bool profTrFwd;            // true: picker to menu; false: menu to picker
        float profTrT, profTrPrev, profTrAge;
        int profTrSlot;            // the box that becomes (or came from) the point at the origin

        bool ProfTrFull => fxOn && !fxReduced;

        /// The menu's intro clock for the move's clock: nothing before the pen starts, then the intro's own timings, squeezed.
        static float ProfTrToIntro(float t) => 0.35f + Mathf.Max(0f, t - ProfTrPenAt) / ProfTrPenRate;

        /// The axis is already full during the move.
        float MenuAxisK => profTr ? 1f : MenuIntroK(0f, 0.45f);

        /// A profile was picked (ChooseProfile): play the move in, or just show the menu.
        void StartProfTrIn(string id)
        {
            whoOpen = false;
            WhoRefresh();
            int slot = whoList.FindIndex(p => p.Id == id);
            if (!ProfTrFull || slot < 0 || slot >= WhoSlots)
                return;
            profTr = true;
            profTrFwd = true;
            profTrT = 0f;
            profTrPrev = -0.0001f;
            profTrAge = 0f;
            profTrSlot = slot;
            menuIntroDone = true;  // this is the menu's intro now
            menuIntroT = ProfTrToIntro(0f);
        }

        /// Update (from TickMenuIntro): run the clock and its sounds, and hand the menu its place in its intro.
        void TickProfTr(float dt)
        {
            if (profEj)
            {
                TickProfEj(dt);  // the eject key's move (DiskEject.cs)
                return;
            }
            profTrAge += dt;
            if (Input.anyKeyDown && profTrAge > 0.25f)
                profTrT = profTrFwd ? ProfTrLen : 0f;  // skipped: straight to the end, silently
            else
            {
                profTrT = Mathf.Clamp(profTrT + (profTrFwd ? 1f : -1f) * dt * ProfTrSpeed, 0f, ProfTrLen);
                PlayProfTrCues(profTrPrev, profTrT);
            }
            profTrPrev = profTrT;
            menuIntroT = Mathf.Min(MenuIntroLen - 0.001f, ProfTrToIntro(profTrT));
            if (profTrFwd && profTrT >= ProfTrLen)
            {
                profTr = false;
                menuIntroT = MenuIntroLen;
            }
            else if (!profTrFwd && profTrT <= 0f)
            {
                profTr = false;
                menuIntroT = MenuIntroLen;
                OpenWho();
            }
        }

        // t: when it plays going forward; d: how long it lasts (going backwards it plays that much later in the clock);
        // back: what plays going backwards (None: nothing).
        struct ProfCue
        {
            public float t, d, vol;
            public Sfx fwd, back;
            public ProfCue(float t, float d, Sfx fwd, Sfx back, float vol) { this.t = t; this.d = d; this.fwd = fwd; this.back = back; this.vol = vol; }
        }

        static readonly ProfCue[] ProfCues =
        {
            new ProfCue(0.00f, 0.15f, Sfx.ProfSelect, Sfx.None, 0.7f),      // the click on your disk
            new ProfCue(0.55f, 0.30f, Sfx.DiskLift, Sfx.DiskLift, 0.7f),    // the clamps let go
            new ProfCue(0.90f, 1.10f, Sfx.DiskFly, Sfx.DiskFly, 0.6f),      // it flies up to the drive
            new ProfCue(2.00f, 0.15f, Sfx.DiskTip, Sfx.DiskTip, 0.7f),      // it tips flat onto the slit
            new ProfCue(2.50f, 0.45f, Sfx.DiskSlide, Sfx.DiskEject, 0.7f),  // it slides in (backwards: it pops out)
            new ProfCue(2.95f, 0.10f, Sfx.DiskClick, Sfx.DiskClick, 0.8f),  // the latch
            new ProfCue(3.00f, 0.90f, Sfx.DiskRead, Sfx.None, 0.6f),        // the drive reads it
            new ProfCue(3.90f, 0.40f, Sfx.DiskReady, Sfx.None, 0.6f),       // ready
            new ProfCue(4.05f, 0.60f, Sfx.ProfScratch, Sfx.ProfScratch, 0.5f),  // the pen draws the arc
            new ProfCue(4.25f, 0.45f, Sfx.ProfSweep, Sfx.ProfSweepBack, 0.7f),  // the box opens
            new ProfCue(4.48f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),  // Host, Join and Continue drop in
            new ProfCue(4.54f, 0.30f, Sfx.PauseSettle, Sfx.PauseClose, 0.5f),  // the box settles
            new ProfCue(4.66f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),
            new ProfCue(4.75f, 0.26f, Sfx.ProfBar, Sfx.ProfBar, 0.8f),      // the bar slides up
            new ProfCue(4.78f, 0.05f, Sfx.PauseType, Sfx.PauseType, 0.5f),
            new ProfCue(4.92f, 0.20f, Sfx.ProfChord, Sfx.ProfChord, 0.6f),  // a low chord as the menu settles
        };

        /// Plays each cue the clock passed between a and b (forward: its start; backwards: its end).
        void PlayProfTrCues(float a, float b)
        {
            float now = Time.unscaledTime;
            foreach (var c in ProfCues)
            {
                if (b > a && a < c.t && c.t <= b)
                    sounds.Play(c.fwd, c.vol, now);
                else if (b < a && b <= c.t + c.d && c.t + c.d < a && c.back != Sfx.None)
                    sounds.Play(c.back, c.vol, now);
            }
        }

        /// The whole screen during the move: the menu at its place in its intro (its title, axis, box and bar), with the
        /// picker's title and boxes on top.
        void DrawProfTr()
        {
            if (profEj)
            {
                DrawProfEj();  // DiskEject.cs
                return;
            }
            DrawMenu();
            DrawProfTrWho(profTrT);
        }

        /// A box as it sits on the picker (nothing in it can be clicked).
        void DrawWhoBoxStatic(int i, Rect r, float t)
        {
            if (i == WhoSlots)
                DrawWhoNewCard(r, false);
            else if (WhoInSlot(i) != null)
                DrawWhoCard(r, WhoInSlot(i), t, false);
            else
                DrawWhoEmptyCard(r);
        }

        /// The picker's side of the move, drawn over the menu: the picker fades out while your disk lifts, flies to the drive, tips in and slides into it.
        void DrawProfTrWho(float T)
        {
            float t = Time.unscaledTime;
            var info = WhoInSlot(profTrSlot);
            var R = WhoBox(profTrSlot);
            float hover = EaseOut(Seg(T, 0.1f, 0.5f)), flight = EaseInOut(Seg(T, 0.9f, 2.0f)), fade = 1f - Seg(T, 0.8f, 1.5f);
            whoLift[profTrSlot] = hover * (1f - flight);  // the clamps open as it lifts
            if (fade > 0f)
                WithAlpha(fade, () =>
                {
                    Ui.Glitch(new Rect(0f, MenuTitleY - 56f, W, 80f), "WHO'S PLAYING?", 58, Color.white, TextAnchor.MiddleCenter, Ui.Accent);
                    DrawWhoShelf();
                    for (int i = 0; i <= WhoSlots; i++)
                        if (i != profTrSlot)
                            DrawWhoBoxStatic(i, WhoBox(i), t);
                });
            if (info == null || T >= 2.95f)
            {
                DrawDriveLip(1f, DriveForMenu());
                return;
            }
            float s = Mathf.Lerp(R.width, DriveDiskSize, flight);
            float x = Mathf.Lerp(R.x, W / 2f - DriveDiskSize / 2f, flight), y = Mathf.Lerp(R.y - 12f * hover, DriveY + 8f, flight);
            float sx = 1f, sy = 1f, a = 1f;
            if (T >= 2.0f)  // it tips flat from its top edge, which sits on the slit
            {
                float k = EaseInOut(Seg(T, 2.0f, 2.5f));
                sy = Mathf.Lerp(1f, 0.06f, k);
                y = Mathf.Lerp(DriveY + 8f, DriveY - 4f, k);
            }
            if (T >= 2.5f)  // then slides in: narrower, and fading into the slit's dark
            {
                float k = EaseInOut(Seg(T, 2.5f, 2.95f));
                sx = Mathf.Lerp(1f, 0.9f, k);
                y = DriveY - 4f;
                a = 1f - k;
            }
            if (T >= 0.5f && T < 0.9f)  // the ring on the click
            {
                float c = Seg(T, 0.5f, 0.9f);
                Ui.Rim(new Rect(R.x - 4f - 14f * c, R.y - 16f - 14f * c, R.width + 8f + 28f * c, R.height + 8f + 28f * c), WhoColor(info), 0.8f * (1f - c));
            }
            var oldMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(new Vector3(offX + (x + s * (1f - sx) / 2f) * scale, offY + y * scale, 0f), Quaternion.identity,
                new Vector3(scale * sx * s / 200f, scale * sy * s / 200f, 1f));
            WithAlpha(a, () => DrawWhoDisk(new Rect(0f, 0f, 200f, 200f), info, info.Id == ProfileStore.ActiveId));
            GUI.matrix = oldMatrix;
            DrawDriveLip(1f, DriveForMenu());
        }
    }
}

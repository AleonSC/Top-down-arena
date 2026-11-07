using UnityEngine;

namespace Arena
{
    /// The disk drive under the title (look B, "Drive bay", of Docs/Mockups/floppy-slot.html): it replaces the glowing x-axis line on the
    /// picker and the main menu. A housing with a status light, a slit, four read cells and a small readout underneath (DRIVE A: EMPTY while
    /// you choose; DISK DETECTED, READING <name>, then <name> READY as your disk goes in, ProfileTransition.cs). On the menu it stays, lit in your
    /// rank colour, with your name. The slit's lower lip is drawn again over the disk (DrawDriveLip) so the disk reads as going in through it.
    public partial class ArenaApp
    {
        const float DriveY = 206f;                 // the slit's centre line (the CRT title's bezel ends at 186)
        const float DriveDiskSize = 166f;          // a disk's width when it reaches the slot
        const float DriveW = 330f, DriveH = 32f, DriveSlitW = 190f;
        static readonly Color DriveAmber = new Color(1f, 0.71f, 0.28f), DriveDim = new Color(0.27f, 0.35f, 0.59f);

        struct DriveState
        {
            public Color led, glowCol;
            public float ledA, glow, flash, prog;
            public string text;
            public bool done;
        }

        static DriveState DriveEmpty() => new DriveState { led = DriveDim, glowCol = DriveDim, ledA = 0.6f, text = "DRIVE A: EMPTY" };

        static DriveState DriveLoaded(string name, Color col) =>
            new DriveState { led = col, glowCol = col, ledA = 1f, glow = 0.4f, prog = 1f, done = true, text = name.ToUpperInvariant() + "  READY" };

        /// The drive for the clock T of the profile move (ProfileTransition.cs): empty, a pulsing light as the disk comes, the slide in, the read, ready.
        static DriveState DriveAt(float T, string name, Color col)
        {
            var s = DriveEmpty();
            s.glowCol = col;
            if (T >= 3.9f)
                return DriveLoaded(name, col);
            if (T >= 1.2f && T < 2.5f)
            {
                s.led = DriveAmber;
                s.ledA = 0.6f + 0.4f * Mathf.Sin(T * 14f);
                s.text = "DISK INCOMING";
            }
            else if (T >= 2.5f && T < 2.95f)
            {
                s.led = DriveAmber;
                s.ledA = 1f;
                s.text = "DISK DETECTED";
            }
            else if (T >= 2.95f)
            {
                s.led = col;
                s.ledA = Mathf.FloorToInt(T * 10f) % 2 == 1 ? 1f : 0.35f;
                s.text = "READING " + name.ToUpperInvariant();
            }
            s.glow = Seg(T, 2.5f, 3.0f);
            s.flash = T >= 2.9f && T < 3.25f ? 1f - Seg(T, 2.9f, 3.25f) : 0f;
            s.prog = Seg(T, 2.95f, 3.9f);
            return s;
        }

        /// What the drive shows when nothing is moving: your profile loaded, or empty if no profile has been chosen (a command-line start).
        DriveState DriveIdle()
        {
            if (ProfileStore.ActiveId.Length == 0 || Profile.Current.Name.Length == 0)
                return DriveEmpty();
            return DriveLoaded(Profile.Current.Name, RankLook.TierColor(Profile.Current.RankNow));
        }

        /// The drive for the menu: following the move's clock while it plays (also backwards), else idle.
        DriveState DriveForMenu()
        {
            if (!profTr)
                return DriveIdle();
            var info = WhoInSlot(profTrSlot);
            if (profEj)
                return DriveEjectAt(profEjT, info != null ? info.Name : Profile.Current.Name, WhoColor(info));
            return DriveAt(profTrT, info != null ? info.Name : Profile.Current.Name, WhoColor(info));
        }

        static Color Al(Color c, float a) => new Color(c.r, c.g, c.b, c.a * a);

        void DrawDriveSlot(float alpha, DriveState S)
        {
            if (alpha <= 0f)
                return;
            WithAlpha(alpha, () =>
            {
                float cx = W / 2f, x0 = cx - DriveW / 2f, y0 = DriveY - DriveH / 2f;
                var edge = new Color(0.45f, 0.65f, 1f, 0.5f);
                Ui.Pill(new Rect(x0, y0 + 4f, DriveW, DriveH), new Color(0f, 0f, 0f, 0.35f));
                Ui.Pill(new Rect(x0, y0, DriveW, DriveH), edge);
                Ui.Pill(new Rect(x0 + 1f, y0 + 1f, DriveW - 2f, DriveH - 2f), new Color(0.07f, 0.09f, 0.2f));
                Ui.Fill(new Rect(x0 + 12f, y0 + 2f, DriveW - 24f, 1f), new Color(0.6f, 0.7f, 1f, 0.25f));
                var slit = new Rect(cx - DriveSlitW / 2f, DriveY - 4f, DriveSlitW, 8f);
                if (S.glow > 0f)
                    Ui.Pill(new Rect(slit.x - 3f, slit.y - 3f, slit.width + 6f, slit.height + 6f), Al(S.glowCol, 0.3f * S.glow));
                Ui.Pill(slit, new Color(0.004f, 0.008f, 0.04f));
                if (S.glow > 0f)
                    Ui.Pill(slit, Al(S.glowCol, 0.55f * S.glow));
                DrawDriveLipLine(S);
                // the status light and the drive's letter
                var lc = new Vector2(cx - 143f, DriveY);
                Ui.Dot(new Rect(lc.x - 9.5f, lc.y - 9.5f, 19f, 19f), Al(S.led, 0.25f * S.ledA));
                Ui.Dot(new Rect(lc.x - 4.5f, lc.y - 4.5f, 9f, 9f), Al(S.led, S.ledA));
                Ui.Dot(new Rect(lc.x - 3f, lc.y - 3f, 3f, 3f), new Color(1f, 1f, 1f, 0.55f));
                Ui.Label(new Rect(cx - 130f, DriveY - 9f, 24f, 18f), "A:", 11, new Color(0.63f, 0.75f, 1f, 0.7f), TextAnchor.MiddleLeft, true);
                // the read cells fill while the disk is read
                for (int i = 0; i < 4; i++)
                {
                    bool on = S.done || S.prog > (i + 0.5f) / 4f;
                    Ui.Pill(new Rect(cx + 112f + i * 12f, DriveY - 4f, 8f, 8f), on ? Al(S.glowCol, 0.95f) : new Color(0.06f, 0.09f, 0.23f));
                }
                Ui.Label(new Rect(cx - 150f, DriveY + 22f, 300f, 16f), S.text, 11, new Color(0.63f, 0.75f, 1f, S.done ? 0.9f : 0.6f), TextAnchor.MiddleCenter, true);
            });
        }

        void DrawDriveLipLine(DriveState S)
        {
            float cx = W / 2f;
            Ui.Fill(new Rect(cx - DriveSlitW / 2f + 2f, DriveY + 4f, DriveSlitW - 4f, 1f), new Color(0.75f, 0.83f, 1f, 0.5f));
            if (S.flash > 0f)
                Ui.Fill(new Rect(cx - DriveSlitW / 2f, DriveY - 3f, DriveSlitW, 2f), new Color(1f, 1f, 1f, S.flash));
        }

        /// Drawn after the disk, over it: the slit's lower lip and the flash as the disk seats.
        void DrawDriveLip(float alpha, DriveState S)
        {
            if (alpha > 0f)
                WithAlpha(alpha, () => DrawDriveLipLine(S));
        }
    }
}

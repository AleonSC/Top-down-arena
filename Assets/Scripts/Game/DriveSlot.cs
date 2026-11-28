using UnityEngine;

namespace Arena
{
    /// The floppy drive in the monitor's chin (Docs/Mockups/disk-boot-sequence.html): a dark housing with the drive's letter, a slit, a
    /// light and four read cells, and a readout under it. On Who's playing? it reads INSERT: <name> for the disk in the middle of the
    /// carousel; as a disk goes in: DISK DETECTED, READING <name>… (the light blinking amber, the cells filling), then <name> READY in
    /// green, which it keeps on the main menu. The slit's lower lip is drawn again over a disk (DrawDriveLip) so it goes in through it.
    public partial class ArenaApp
    {
        const float DriveY = 194f;                 // the slit's centre line (in the monitor's chin, PcMonitor.cs)
        const float DriveCx = W / 2f;              // the housing's centre
        const float DriveW = 330f, DriveH = 34f, DriveSlitW = 200f;
        static float DriveX0 => DriveCx - DriveW / 2f;
        static float DriveSlitX => DriveX0 + 53f;           // the slit's left end (it sits left of centre, the light and cells to its right)
        static float DriveSlitCx => DriveSlitX + DriveSlitW / 2f;
        static readonly Color DriveAmber = new Color(1f, 0.71f, 0.278f);
        static readonly Color DriveLabel = new Color(0.545f, 0.576f, 0.651f), DriveReadyLabel = new Color(0.718f, 0.961f, 0.8f);
        static readonly Color CellOff = new Color(0.106f, 0.133f, 0.22f), CellDone = new Color(0.173f, 0.478f, 0.29f);

        struct DriveState
        {
            public Color led;
            public bool ledOn;
            public float flash, prog;   // prog: how many of the four cells are lit (0 to 1), amber while reading
            public string text;
            public bool done;           // read: the light and cells green, the label green
        }

        static DriveState DriveEmpty(string text = "DRIVE A: EMPTY") => new DriveState { led = DriveAmber, text = text };

        static DriveState DriveLoaded(string name) =>
            new DriveState { led = LampGreen, ledOn = true, prog = 1f, done = true, text = name.ToUpperInvariant() + " READY" };

        /// The drive for the clock T of the disk going in (ProfileTransition.cs).
        static DriveState DriveAt(float T, string name)
        {
            var s = DriveEmpty("INSERT: " + name.ToUpperInvariant());
            if (T >= BootDiskReadAt)
                return DriveLoaded(name);
            if (T >= 1.8f)
            {
                s.text = "DISK DETECTED";
                s.ledOn = true;
            }
            if (T >= 2.0f)
            {
                s.text = "READING " + name.ToUpperInvariant() + "…";
                s.ledOn = Mathf.FloorToInt(T * 14f) % 2 == 0;
                s.prog = Seg(T, 2.0f, 2.8f);
            }
            s.flash = Seg(T, 1.72f, 1.82f) * (1f - Seg(T, 1.82f, 2.05f));
            return s;
        }

        /// What the drive shows when nothing is moving: your profile loaded, or empty if none has been chosen (a command-line start).
        DriveState DriveIdle()
        {
            if (ProfileStore.ActiveId.Length == 0 || Profile.Current.Name.Length == 0)
                return DriveEmpty();
            return DriveLoaded(Profile.Current.Name);
        }

        /// The drive for the menu: following the moves' clocks while they play, else idle.
        DriveState DriveForMenu()
        {
            if (!profTr)
                return DriveIdle();
            var info = WhoAt(profTrSlot);
            string name = info != null ? info.Name : Profile.Current.Name;
            return profEj ? DriveEjectAt(profEjT, name) : DriveAt(profTrT, name);
        }

        /// The housing, the slit, the light, the cells and the readout.
        void DrawDriveSlot(float alpha, DriveState S)
        {
            if (alpha <= 0f)
                return;
            WithAlpha(alpha, () =>
            {
                float x0 = DriveX0, y0 = DriveY - 18f;
                Ui.RoundBox(new Rect(x0, y0, DriveW, DriveH), new Color(0.165f, 0.176f, 0.208f), 5);
                Ui.RoundBox(new Rect(x0 + 1f, y0 + 1f, DriveW - 2f, DriveH - 2f), new Color(0.043f, 0.047f, 0.063f), 4);
                Ui.Fill(new Rect(x0 + 4f, y0 + 1f, DriveW - 8f, 2f), new Color(0f, 0f, 0f, 0.6f));  // its inner shadow at the top
                Ui.Label(new Rect(x0 + 14f, y0 + 9f, 24f, 16f), "A:", 11, DriveLabel, TextAnchor.MiddleLeft, true);
                Ui.RoundBox(new Rect(DriveSlitX, DriveY - 4f, DriveSlitW, 8f), Color.black, 3);
                DrawDriveLipLine(S);
                Led(x0 + 264f, DriveY - 5f, S.done ? LampGreen : S.led, S.ledOn);
                for (int i = 0; i < 4; i++)
                {
                    bool on = S.done || S.prog * 4f > i;
                    var r = new Rect(x0 + 277f + i * 11f, DriveY - 5f, 7f, 10f);
                    if (on && !S.done)
                        Ui.RadialGlow(new Rect(r.x - 5f, r.y - 4f, 17f, 18f), DriveAmber.WithAlpha(0.5f));
                    Ui.Fill(r, S.done ? CellDone : on ? DriveAmber : CellOff);
                }
                Ui.Label(new Rect(x0, y0 + 38f, DriveW, 16f), S.text, 10, S.done ? DriveReadyLabel : DriveLabel, TextAnchor.MiddleCenter, true);
            });
        }

        void DrawDriveLipLine(DriveState S)
        {
            Ui.Fill(new Rect(DriveSlitX + 2f, DriveY + 3f, DriveSlitW - 4f, 2f), new Color(1f, 1f, 1f, 0.22f));
            if (S.flash > 0f)
            {
                Ui.RadialGlow(new Rect(DriveSlitX - 20f, DriveY - 22f, DriveSlitW + 40f, 44f), new Color(0.78f, 0.88f, 1f, 0.8f * S.flash));
                Ui.RoundBox(new Rect(DriveSlitX, DriveY - 4f, DriveSlitW, 8f), new Color(1f, 1f, 1f, S.flash), 3);
            }
        }

        /// Drawn after a disk, over it: the slit's lower lip and the flash as the disk seats.
        void DrawDriveLip(float alpha, DriveState S)
        {
            if (alpha > 0f)
                WithAlpha(alpha, () => DrawDriveLipLine(S));
        }
    }
}

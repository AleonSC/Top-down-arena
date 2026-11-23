using UnityEngine;

namespace Arena
{
    /// The monitor at the top of Who's playing? and the main menu (Docs/Mockups/disk-boot-sequence.html): a graphite case with a lit top
    /// edge, the title's CRT glass in a neon outline (MenuIntro.cs draws the glass and the typed title, CrtBox), and a chin under it:
    /// ARENA CRT-14, two grilles, a power light, the floppy drive (DriveSlot.cs) and, on the menu, the eject key (DiskEject.cs).
    public partial class ArenaApp
    {
        static readonly Rect MonRect = new Rect(W / 2f - 490f, 18f, 980f, 222f);
        static float monShakeX, monShakeY;  // the monitor jolts when the menu's case lands under it (MenuMachine.cs)

        /// The monitor's title line: the text on the glass, how many letters are typed (-1: all, with the cursor after it),
        /// and whether the letters show flat (being erased or retyped) instead of flipping in.
        struct MonTitle
        {
            public string text;
            public int typed;
            public bool flat;
        }

        static MonTitle TitleAll(string text) => new MonTitle { text = text, typed = -1, flat = true };

        /// The whole monitor: case, glass and title, chin, drive and (eject: the menu) the eject key.
        void DrawMonitor(MonTitle title, DriveState drive, bool eject, bool ejectLive)
        {
            bool shake = monShakeX != 0f || monShakeY != 0f;
            if (shake)
                GUI.BeginGroup(new Rect(monShakeX, monShakeY, W, H));
            Metal(MonRect, 14);
            DrawMonitorGlass(title);
            DrawMonitorChin();
            DrawDriveSlot(1f, drive);
            if (eject)
                DrawDriveEject(1f, ejectLive);
            if (shake)
                GUI.EndGroup();
        }

        /// The glass with the title on it, and its neon outline.
        void DrawMonitorGlass(MonTitle title)
        {
            crtFull = title.text;
            crtFlat = title.flat;
            crtTyped = title.typed >= title.text.Length ? -1 : title.typed;
            DrawCrtBack();
            if (crtTyped != 0)
                DrawCrtTitle(new Rect(0f, MenuTitleY - MenuTitleSize * 0.6f, W, MenuTitleSize * 1.2f));
            DrawCrtFront();
            NeonBox(new Rect(CrtBox.x - 3f, CrtBox.y - 3f, CrtBox.width + 6f, CrtBox.height + 6f), 1f, 0f, 8);
            crtFull = CrtTitleText;
            crtFlat = false;
            crtTyped = -1;
        }

        /// The chin under the glass: the model name, two grilles and the power light (the drive sits between them).
        void DrawMonitorChin()
        {
            float x = MonRect.x, y = MonRect.y;
            ChinText(new Rect(x + 38f, y + 162f, 200f, 14f), "ARENA CRT-14", 10, ChinInk);
            Vent(new Rect(x + 38f, y + 186f, 220f, 16f), 0.8f);
            Vent(new Rect(x + 722f, y + 186f, 200f, 16f), 0.8f);
            Led(x + 936f, y + 169f, LampGreen, true);
        }

        /// Draws the monitor again, clipped to above y = bottom: for a disk going into (or coming out of) the slit, which must vanish
        /// behind the chin above the slit.
        void DrawMonitorAbove(float bottom, MonTitle title, DriveState drive)
        {
            GUI.BeginGroup(new Rect(0f, 0f, W, bottom));
            DrawMonitor(title, drive, false, false);
            GUI.EndGroup();
        }
    }
}

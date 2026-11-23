using UnityEngine;

namespace Arena
{
    /// The main menu as the computer (a mockup, preview MENUPC=1; the Who's playing? screen's layout 5 is the same machine): the
    /// title is the monitor, standing on the case; the case's top row holds the floppy drive with your disk in it and its eject key,
    /// with the power button and lights on the left; the menu box below is the case's small screen and keys, so Play still opens the
    /// saved runs in it. Drawn in graphite (Ui.Graphite).
    public partial class ArenaApp
    {
        public static bool MenuComputer;

        const float MenuPcDriveY = 248f;  // the drive's slit, in the case's top row

        /// The case: from under the monitor to just under the menu box, as wide as the monitor's screen.
        static Rect MenuPcCase => new Rect(WhoEdge, TitleNeonRect.yMax + 34f, W - 2f * WhoEdge, MenuCard.yMax + 16f - (TitleNeonRect.yMax + 34f));

        /// IMGUI: the case: graphite, lit along its top where the monitor stands, a drive row with the power button, lights, vents and
        /// the drive's bay, then the recess the menu box sits in, vents down both sides, and the neon round it all.
        void DrawMenuPcCase(float a)
        {
            if (a <= 0f)
                return;
            WithAlpha(a, () =>
            {
                var c = MenuPcCase;
                float cut = 14f;
                Ui.Pill(new Rect(c.x + 4f, c.y + 8f, c.width - 8f, c.height), new Color(0f, 0f, 0f, 0.5f));
                WhoBand(new Rect(c.x, c.y, c.width, c.height - cut), new Color(0.24f, 0.25f, 0.28f), new Color(0.1f, 0.105f, 0.12f), 3f);
                if (BeginUiGl())
                {
                    UiModelAt(V2.Zero, 1f);
                    SetColor(new Rgba(0.1f, 0.105f, 0.12f, Ui.Alpha));
                    Quad(new V2(c.x, c.yMax - cut), new V2(c.xMax, c.yMax - cut), new V2(c.xMax - cut, c.yMax), new V2(c.x + cut, c.yMax));
                    EndUiGl();
                }
                Ui.Fill(new Rect(c.x + 2f, c.y + 1f, c.width - 4f, 1f), new Color(1f, 1f, 1f, 0.35f));  // the lit top edge
                for (int i = 0; i < 10; i++)                                                             // the monitor's shadow on it
                    Ui.Fill(new Rect(c.x + 8f, c.y + 2f + i, c.width - 16f, 1f), new Color(0f, 0f, 0f, 0.5f * (1f - i / 10f) * (1f - i / 10f)));
                // the drive row: its bay in the middle, the power button and lights on the left, vents on the right
                var bay = new Rect(W / 2f - DriveW / 2f - 18f, MenuPcDriveY - 26f, DriveW + 18f + 18f + 60f, 54f);
                Ui.Pill(new Rect(bay.x - 1f, bay.y - 1f, bay.width + 2f, bay.height + 2f), new Color(1f, 1f, 1f, 0.07f));
                Ui.Pill(bay, new Color(0.03f, 0.032f, 0.038f));
                Ui.Fill(new Rect(bay.x + 6f, bay.y + 1f, bay.width - 12f, 3f), new Color(0f, 0f, 0f, 0.5f));
                var pb = new Rect(c.x + 30f, MenuPcDriveY - 17f, 34f, 34f);
                Ui.Dot(new Rect(pb.x - 3f, pb.y - 3f, pb.width + 6f, pb.height + 6f), new Color(0.04f, 0.042f, 0.05f));
                Ui.Dot(pb, new Color(0.34f, 0.35f, 0.39f));
                Ui.Dot(new Rect(pb.x + 4f, pb.y + 3f, pb.width - 8f, pb.height - 10f), new Color(0.45f, 0.47f, 0.52f));
                var ink = new Color(0.14f, 0.15f, 0.17f);
                Ui.Dot(new Rect(pb.center.x - 7f, pb.center.y - 6f, 14f, 14f), ink, true);
                Ui.Fill(new Rect(pb.center.x - 3f, pb.center.y - 10f, 6f, 7f), new Color(0.45f, 0.47f, 0.52f));
                Ui.Fill(new Rect(pb.center.x - 1f, pb.center.y - 10f, 2f, 8f), ink);
                string[] names = { "POWER", "DISK" };
                for (int i = 0; i < 2; i++)
                {
                    float ly = MenuPcDriveY - 15f + i * 18f;
                    var led = new Rect(c.x + 84f, ly + 3f, 14f, 5f);
                    var col = i == 0 ? new Color(0.42f, 1f, 0.6f) : new Color(1f, 0.71f, 0.28f);
                    Ui.Pill(new Rect(led.x - 3f, led.y - 3f, led.width + 6f, led.height + 6f), col.WithAlpha(0.22f));
                    Ui.Fill(led, col);
                    Ui.Label(new Rect(led.xMax + 8f, ly - 3f, 80f, 16f), names[i], 10, new Color(0.62f, 0.64f, 0.7f), TextAnchor.MiddleLeft, true);
                }
                for (float x = c.x + 186f; x < bay.x - 24f; x += 10f)
                    Ui.Fill(new Rect(x, MenuPcDriveY - 14f, 5f, 28f), new Color(0.04f, 0.042f, 0.05f, 0.85f));
                for (float x = bay.xMax + 24f; x < c.xMax - 30f; x += 10f)
                    Ui.Fill(new Rect(x, MenuPcDriveY - 14f, 5f, 28f), new Color(0.04f, 0.042f, 0.05f, 0.85f));
                // a panel line under the drive row, then the recess the menu box sits in, with vents down both sides of it
                float ry = MenuCard.y - 14f;
                Ui.Fill(new Rect(c.x + 14f, ry, c.width - 28f, 1f), new Color(0f, 0f, 0f, 0.4f));
                Ui.Fill(new Rect(c.x + 14f, ry + 1f, c.width - 28f, 1f), new Color(1f, 1f, 1f, 0.06f));
                var rec = new Rect(MenuCard.x - 10f, MenuCard.y - 8f, MenuCard.width + 20f, MenuCard.height + 16f);
                Ui.Pill(new Rect(rec.x - 1f, rec.y - 1f, rec.width + 2f, rec.height + 2f), new Color(1f, 1f, 1f, 0.08f));
                Ui.Pill(rec, new Color(0.03f, 0.032f, 0.038f));
                foreach (float vx in new[] { c.x + 40f, rec.xMax + 40f })
                    for (float y = MenuCard.y + 20f; y < MenuCard.yMax - 20f; y += 10f)
                        Ui.Fill(new Rect(vx, y, rec.x - c.x - 80f, 5f), new Color(0.04f, 0.042f, 0.05f, 0.85f));
                if (BeginUiGl())
                {
                    UiModelAt(V2.Zero, 1f);
                    NeonPath(new[] { new V2(c.x, c.y), new V2(c.xMax, c.y), new V2(c.xMax, c.yMax - cut), new V2(c.xMax - cut, c.yMax), new V2(c.x + cut, c.yMax), new V2(c.x, c.yMax - cut) }, true, a * NeonFlicker(Time.unscaledTime, 11f, 2f));
                    EndUiGl();
                }
            });
        }
    }
}

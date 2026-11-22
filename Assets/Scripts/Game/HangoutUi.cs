using UnityEngine;

namespace Arena
{
    /// The Hangout's HUD (HangoutGame.cs): in place of the GET READY / WAVE CLEARED band, a small chip at the top that says where you are.
    public partial class ArenaApp
    {
        const float HangoutChipW = 360f, HangoutChipH = 40f;

        void DrawHangoutTag()
        {
            var r = new Rect(W / 2f - HangoutChipW / 2f, 14f, HangoutChipW, HangoutChipH);
            Ui.Pill(r, new Color(0.02f, 0.03f, 0.05f, 0.7f));
            Ui.Label(new Rect(r.x, r.y + 3f, r.width, 20f), "HANGOUT", 16, CrtStyle.Gold, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(r.x, r.y + 21f, r.width, 16f), "your real loadout  ·  no waves  ·  menu: back to lobby", 11, CrtStyle.Dim, TextAnchor.MiddleCenter);
        }
    }
}

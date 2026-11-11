using UnityEngine;

namespace Arena
{
    /// The look of surprise rooms (Docs/SurpriseRooms.md step 2): a gold flash and a scan line sweeping the new room as it builds in
    /// (1.5 s), a thin gold rim for the rest of the wave, gold on the minimap (RoomsLook.cs) and a SURPRISE ROOM chip with its shape's name.
    public partial class ArenaApp
    {
        const float SurpriseBuild = 1.5f, SurpriseChipT = 4.5f;

        /// DrawRoomsFloor: one box of a surprise room (area i), over its floor tint.
        void DrawSurpriseRoom(World wd, int i, RoomArea a)
        {
            float k = Mathf.Clamp01(game.SurpriseAge / SurpriseBuild);
            bool fresh = game.SurpriseFresh;
            float flash = fresh ? (1f - k) * (1f - k) : 0f;
            if (flash > 0f)
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, DoorGold.WithAlpha(0.22f * flash));
            if (fresh && k < 1f)  // the scan line sweeping down the box
            {
                float y = a.Min.Y + a.Size.Y * k;
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 6f, DoorGold.WithAlpha(0.25f * (1f - k)));
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 2.5f, DoorGold.WithAlpha(0.95f * (1f - k)));
            }
            var rim = DoorGold.WithAlpha(fresh ? 0.5f + 0.5f * flash : 0.2f);
            foreach (var seg in wd.Rim(i, 14f))  // (round the whole room, not across its boxes' seams)
                Line(seg[0], seg[1], 2.5f, rim);
        }

        /// DrawHud: the SURPRISE ROOM chip, top middle, for a few seconds after one appears.
        void DrawSurpriseBanner()
        {
            if (game.SurpriseKind == 0 || game.SurpriseAge > SurpriseChipT || !game.Running)
                return;
            float age = game.SurpriseAge;
            float al = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01((SurpriseChipT - age) / 0.6f);
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, al);
            var r = new Rect(W / 2f - 190f, 96f, 380f, 58f);
            DkPanel(r);
            var gold = new Color(1f, 0.82f, 0.4f);
            DkTitle(new Rect(r.x, r.y + 5f, r.width, 28f), "SURPRISE ROOM", 22, gold, TextAnchor.MiddleCenter);
            CrtUi.Text(new Rect(r.x, r.y + 32f, r.width, 20f), Expansion.SurpriseName((SurpriseShape)(game.SurpriseKind - 1)).ToUpperInvariant() + "  ·  FREE  ·  OPEN TO ALL", 12, new Color(gold.r, gold.g, gold.b, 0.8f), TextAnchor.MiddleCenter, true, CabCalm);
            GUI.color = old;
        }
    }
}

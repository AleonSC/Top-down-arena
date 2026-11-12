using UnityEngine;

namespace Arena
{
    /// The look of surprise rooms (Docs/SurpriseRooms.md step 2): a gold flash and a scan line sweeping the new room as it builds in
    /// (1.5 s), a thin gold rim for the rest of the wave, gold on the minimap (RoomsLook.cs) and a SURPRISE ROOM chip with its shape's name.
    public partial class ArenaApp
    {
        const float SurpriseBuild = 1.5f, SurpriseChipT = 4.5f;

        /// A surprise room's ink: gold, or teal for a breach Stash (Docs/BreachRooms.md).
        Rgba SurpriseInk(byte kind) => kind == 1 + (int)SurpriseShape.Stash ? DoorTeal : DoorGold;

        /// DrawRoomsFloor: one box of a surprise room (area i), over its floor tint.
        void DrawSurpriseRoom(World wd, int i, RoomArea a)
        {
            var ink = SurpriseInk(a.Surprise);
            float k = Mathf.Clamp01(game.SurpriseAge / SurpriseBuild);
            bool fresh = game.SurpriseFresh;
            float flash = fresh ? (1f - k) * (1f - k) : 0f;
            if (flash > 0f)
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, ink.WithAlpha(0.22f * flash));
            if (fresh && k < 1f)  // the scan line sweeping down the box
            {
                float y = a.Min.Y + a.Size.Y * k;
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 6f, ink.WithAlpha(0.25f * (1f - k)));
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 2.5f, ink.WithAlpha(0.95f * (1f - k)));
            }
            bool cache = a.Surprise == 1 + (int)SurpriseShape.Stash && i - 1 < game.Expanded.Count && !game.Expanded[i - 1].Claimed;
            if (cache)  // the vector cache: a breathing diamond to walk into until someone takes it
            {
                float br = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3.2f), r = 26f + 5f * br;
                V2 c = a.Mid, up = c + new V2(0f, -r), rt = c + new V2(r, 0f), dn = c + new V2(0f, r), lf = c + new V2(-r, 0f);
                FillRect(c.X - r * 0.5f, c.Y - r * 0.5f, r, r, ink.WithAlpha(0.18f + 0.12f * br));
                Line(up, rt, 2.5f, ink); Line(rt, dn, 2.5f, ink); Line(dn, lf, 2.5f, ink); Line(lf, up, 2.5f, ink);
                Line(c + new V2(-r * 1.9f, 0f), c + new V2(r * 1.9f, 0f), 1.5f, ink.WithAlpha(0.35f * br));
                Line(c + new V2(0f, -r * 1.9f), c + new V2(0f, r * 1.9f), 1.5f, ink.WithAlpha(0.35f * br));
            }
            var rim = ink.WithAlpha(fresh ? 0.5f + 0.5f * flash : 0.2f);
            foreach (var seg in wd.Rim(i, 14f))  // (round the whole room, not across its boxes' seams)
                Line(seg[0], seg[1], 2.5f, rim);
        }

        /// DrawHud: "+10" over each Stash's vector cache that nobody has taken yet (the diamond itself is drawn in DrawSurpriseRoom).
        void DrawStashLabels()
        {
            byte kind = (byte)(1 + (int)SurpriseShape.Stash);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 3.2f);
            for (int i = 0; i < game.Expanded.Count; i++)
            {
                var r = game.Expanded[i];
                if (r.Surprise != kind || r.Claimed)
                    continue;
                V2 c = ToUi(new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f - 52f));
                if (c.X < -60f || c.X > W + 60f || c.Y < -30f || c.Y > H + 30f)
                    continue;
                Ui.Label(new Rect(c.X - 60f, c.Y - 14f, 120f, 28f), "+" + Game.StashVectors, 22, new Color(0.25f, 0.9f, 0.7f, pulse), TextAnchor.MiddleCenter, true);
            }
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
            bool stash = game.SurpriseKind == 1 + (int)SurpriseShape.Stash;
            var gold = stash ? new Color(0.25f, 0.9f, 0.7f) : new Color(1f, 0.82f, 0.4f);
            DkTitle(new Rect(r.x, r.y + 5f, r.width, 28f), stash ? "STASH" : "SURPRISE ROOM", 22, gold, TextAnchor.MiddleCenter);
            CrtUi.Text(new Rect(r.x, r.y + 32f, r.width, 20f), (stash ? "WALK IN FOR +" + Game.StashVectors + " VECTORS" : Expansion.SurpriseName((SurpriseShape)(game.SurpriseKind - 1)).ToUpperInvariant() + "  ·  FREE  ·  OPEN TO ALL"), 12, new Color(gold.r, gold.g, gold.b, 0.8f), TextAnchor.MiddleCenter, true, CabCalm);
            GUI.color = old;
        }
    }
}

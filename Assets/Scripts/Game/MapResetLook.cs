using UnityEngine;

namespace Arena
{
    /// The map reset's look (Docs/MapReset.md step 3, Core/MapReset.cs): the old map's rooms fold into the hub as phosphor outlines,
    /// the kept room builds in gold (a flash and a scan line, then a thin gold rim for the rest of the map), and a MAP n/10 banner
    /// says what came back. Preview `MAPRESET=<s since the reset> EXPANDMAP=0 EXPANDROOMS=... shot.sh x hud`.
    public partial class ArenaApp
    {
        const float FoldTime = 1.6f, KeptBuild = 1.8f, MapBannerT = 5f;
        static readonly Rgba FoldInk = new Rgba(0.4f, 0.64f, 1f);

        /// DrawWorld, over the floor: each of the old map's boxes shrinks into the hub's middle and fades, a scan of its outline left behind.
        void DrawMapFold()
        {
            float age = game.MapResetAge;
            if (age >= FoldTime || game.FoldRooms.Count == 0)
                return;
            float k = Mathf.Clamp01(age / FoldTime);
            float pull = k * k * (3f - 2f * k);  // (eased: slow off, quick in)
            float fade = 1f - k;
            var c = Game.ArenaMid;
            foreach (var r in game.FoldRooms)
            {
                V2 a = V2.Lerp(new V2(r.X, r.Y), c, pull), b = V2.Lerp(new V2(r.X1, r.Y1), c, pull);
                FillRect(a.X, a.Y, b.X - a.X, b.Y - a.Y, FoldInk.WithAlpha(0.08f * fade));
                var ink = FoldInk.WithAlpha(0.85f * fade);
                Line(a, new V2(b.X, a.Y), 3.5f, ink);
                Line(new V2(b.X, a.Y), b, 3.5f, ink);
                Line(b, new V2(a.X, b.Y), 3.5f, ink);
                Line(new V2(a.X, b.Y), a, 3.5f, ink);
                // The ghost of where it stood, dimmer and gone sooner.
                var ghost = FoldInk.WithAlpha(0.3f * Mathf.Clamp01(1f - age / (FoldTime * 0.6f)));
                if (ghost.A > 0f)
                {
                    Line(new V2(r.X, r.Y), new V2(r.X1, r.Y), 1.5f, ghost);
                    Line(new V2(r.X1, r.Y), new V2(r.X1, r.Y1), 1.5f, ghost);
                    Line(new V2(r.X1, r.Y1), new V2(r.X, r.Y1), 1.5f, ghost);
                    Line(new V2(r.X, r.Y1), new V2(r.X, r.Y), 1.5f, ghost);
                }
            }
            // The hub's walls flash as the last of it arrives.
            float hit = Mathf.Clamp01((k - 0.7f) / 0.3f);
            var rim = FoldInk.WithAlpha(0.7f * hit * fade * 3f);
            if (rim.A > 0f)
            {
                float w = Game.Width, h = Game.Height;
                Line(new V2(0f, 0f), new V2(w, 0f), 4f, rim);
                Line(new V2(w, 0f), new V2(w, h), 4f, rim);
                Line(new V2(w, h), new V2(0f, h), 4f, rim);
                Line(new V2(0f, h), new V2(0f, 0f), 4f, rim);
            }
        }


        /// The kept room's box (area i) is part of the room the team kept.
        bool IsKept(int area)
        {
            int head = game.KeptHead;
            return head >= 0 && head < game.Expanded.Count && area - 1 >= head && Expansion.RoomGroup(game.Expanded, head).Contains(area - 1);
        }

        /// DrawRoomsFloor: one box of the kept room: a gold flash and scan line as it builds in after the reset, then a thin gold rim.
        void DrawKeptRoom(World wd, int i, RoomArea a)
        {
            var ink = DoorGold;
            float k = Mathf.Clamp01((game.MapResetAge - FoldTime * 0.6f) / KeptBuild);  // (it builds in as the old map arrives)
            if (game.MapResetAge < FoldTime * 0.6f)
                return;  // (not yet)
            float flash = (1f - k) * (1f - k);
            if (flash > 0f)
                FillRect(a.Min.X, a.Min.Y, a.Size.X, a.Size.Y, ink.WithAlpha(0.24f * flash));
            if (k < 1f)
            {
                float y = a.Min.Y + a.Size.Y * k;
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 6f, ink.WithAlpha(0.25f * (1f - k)));
                Line(new V2(a.Min.X, y), new V2(a.Max.X, y), 2.5f, ink.WithAlpha(0.95f * (1f - k)));
            }
            var rim = ink.WithAlpha(0.4f + 0.5f * flash);
            foreach (var seg in wd.Rim(i, 10f))
                Line(seg[0], seg[1], 2f, rim);
        }

        /// DrawHud: the MAP n/10 banner, top middle, for a few seconds after a reset: your vectors back, the room kept, kits kept.
        void DrawMapResetBanner()
        {
            float age = game.MapResetAge;
            if (age > MapBannerT || !game.Running || game.MapNumber < 2)
                return;
            float al = Mathf.Clamp01((age - 0.3f) / 0.25f) * Mathf.Clamp01((MapBannerT - age) / 0.6f);
            if (al <= 0f)
                return;
            var old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, al);
            var r = new Rect(W / 2f - 220f, 96f, 440f, 58f);
            DkPanel(r);
            int of = game.MapsInRun;
            var blue = FoldInk.ToColor();
            DkTitle(new Rect(r.x, r.y + 5f, r.width, 28f), "MAP " + game.MapNumber + (of >= game.MapNumber ? " / " + of : ""), 22, blue, TextAnchor.MiddleCenter);
            int back = game.MapRefunds.TryGetValue(game.MyId, out int b) ? b : 0;
            string kept = game.KeptHead >= 0 && game.KeptHead < game.Expanded.Count ? "ROOM KEPT" : "FRESH MAP";
            string line = "+" + back + " VECTORS BACK  ·  " + kept + "  ·  KITS KEPT";
            CrtUi.Text(new Rect(r.x, r.y + 32f, r.width, 20f), line, 12, CrtStyle.Gold.WithAlpha(0.85f), TextAnchor.MiddleCenter, true, FxCalm);
            GUI.color = old;
        }
    }
}

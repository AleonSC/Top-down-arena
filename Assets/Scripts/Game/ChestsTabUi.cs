using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The wave box's Chests tab (Docs/RoundFlow.md step 5; Core/RemoteChests.cs): every unopened chest on the map as a row (a little
    /// chest in its kind's ink, its kind, which way and how far, and a COLLECT key with the fee), beside a map of the rooms with the
    /// chests on it (the row under the mouse lit there) and your gold. Collect opens the chest where it stands for everyone; the payer
    /// is whoever pressed it, and the key is dim when you can't afford it. The tab shows while any chest is unopened.
    public partial class ArenaApp
    {
        readonly List<ChestSpot> wcChests = new List<ChestSpot>();
        int wcChestHot = -1;  // the row under the mouse (lit on the map)
        const int WcChestRows = 4;

        bool WcChestsAvail => wcChests.Count > 0 && game.CanCollectChests;

        void RefreshWcChests() => game.UncollectedChests(wcChests);

        static string ChestKindName(ChestKind k) => k == ChestKind.Upgrade ? "UPGRADE CHEST" : k == ChestKind.Gold ? "GOLD CHEST" : k == ChestKind.Vector ? "VECTOR CHEST"
            : k == ChestKind.Relic ? "RELIC CHEST" : k == ChestKind.Piece ? "PIECE CHEST" : "BUILDABLES CHEST";

        /// What it gives each player, one line.
        string ChestGives(ChestKind k) => k == ChestKind.Gold ? (Game.ChestGold + Game.ChestGoldPerWave * Mathf.Max(1, game.Wave)) + " GOLD EACH"
            : k == ChestKind.Vector ? (Game.VectorChestPickups * Game.VectorDropSmall) + " VECTORS EACH" : k == ChestKind.Piece ? "PIECE CARDS"
            : k == ChestKind.Buildable ? "A BUILD EACH" : "A CARD EACH";

        static readonly string[] Compass = { "EAST", "SOUTH-EAST", "SOUTH", "SOUTH-WEST", "WEST", "NORTH-WEST", "NORTH", "NORTH-EAST" };

        /// "NORTH-EAST · 9 S WALK" from you (a straight line at your speed: a guide, not a route).
        string ChestWay(Player me, V2 at)
        {
            V2 d = at - me.Pos;
            float ang = Mathf.Atan2(d.Y, d.X) * Mathf.Rad2Deg;  // (y down: south is +90)
            int i = Mathf.RoundToInt(((ang % 360f) + 360f) % 360f / 45f) % 8;
            int secs = Mathf.Max(1, Mathf.RoundToInt(d.Length / Mathf.Max(60f, me.Speed)));
            return Compass[i] + "  ·  " + secs + " S WALK";
        }

        void DrawWcChests(Player me, float x, float y, bool calm)
        {
            var g = CrtStyle.Gold;
            DrawWcHead(x, y, "CHESTS", g, calm);
            int fee = game.ChestCollectFee;
            WcTile(new Rect(x + 646f, y + 16f, 130f, 50f), fee + " G", "FEE TO COLLECT", calm);
            WcTile(new Rect(x + 786f, y + 16f, 132f, 50f), me.Gold.ToString(), "YOUR GOLD", calm);
            HudCrt(new Rect(x + WcPad + 2f, y + 62f, 600f, 18f), "COLLECT ONE FROM HERE, OR WALK THERE FOR FREE", 16, CrtUi.Dim);
            float rx = x + WcPad, ry = y + 84f;
            wcChestHot = -1;
            Vector2 mouse = Event.current.mousePosition;
            int shown = Mathf.Min(wcChests.Count, wcChests.Count > WcChestRows ? WcChestRows - 1 : WcChestRows);
            for (int i = 0; i < shown; i++)
            {
                var c = wcChests[i];
                var row = new Rect(rx, ry, WcTallyW, WcRowH);
                bool hot = row.Contains(mouse);
                if (hot)
                    wcChestHot = i;
                CrtKit.SoftBox(row, CrtUi.Ink, hot, false, false);
                DrawChestIcon(new Rect(row.x + 12f, row.y + 12f, 36f, 28f), c.Kind);
                Ui.Label(new Rect(row.x + 62f, row.y + 7f, 300f, 20f), ChestKindName(c.Kind) + "  ·  " + ChestGives(c.Kind), 14, Color.white, TextAnchor.MiddleLeft, true);
                HudCrt(new Rect(row.x + 62f, row.y + 28f, 320f, 16f), ChestWay(me, c.Pos), 15, CrtUi.Dim);
                bool afford = me.Gold >= fee;
                if (LobbyKit.Key(new Rect(row.xMax - 186f, row.y + 8f, 176f, 36f), afford ? "COLLECT · " + fee + " G" : "NEED " + fee + " G", false, calm, size: 18, minSize: 12,
                        enabled: afford, accent: afford ? g : (Color?)null))
                    game.CollectChest(c);
                ry += WcRowStep;
            }
            if (shown < wcChests.Count)
                HudCrt(new Rect(rx + 10f, ry + 4f, WcTallyW, 20f), "+" + (wcChests.Count - shown) + " MORE ON THE MAP  ·  THEY MOVE UP AS YOU COLLECT", 18, CrtUi.Dim);
            DrawChestMap(me, new Rect(x + 640f, y + 84f, 278f, 222f));
        }

        /// A little chest: a body and a lid in the kind's ink.
        void DrawChestIcon(Rect r, ChestKind k)
        {
            var ink = ToColor(ChestInkOf(k));
            float lid = r.height * 0.36f;
            Ui.Fill(new Rect(r.x, r.y + lid, r.width, r.height - lid), new Color(ink.r, ink.g, ink.b, 0.18f));
            CrtKit.Frame(new Rect(r.x, r.y + lid, r.width, r.height - lid), 2f, ink);
            CrtKit.Frame(new Rect(r.x + 2f, r.y, r.width - 4f, lid + 1f), 2f, ink);
            Ui.Fill(new Rect(r.center.x - 3f, r.y + lid - 2f, 6f, 8f), ink);  // the lock
        }

        /// The map beside the list: every built room as a faint box, the chests as squares in their ink (the hot row's ringed), you as a dot.
        void DrawChestMap(Player me, Rect r)
        {
            var ink = CrtUi.Ink;
            CrtKit.Dashed(r, 1f, new Color(ink.r, ink.g, ink.b, 0.45f));
            HudCrt(new Rect(r.x + 10f, r.y + 6f, r.width - 20f, 16f), "MAP", 15, CrtUi.Dim);
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            foreach (var rm in game.Expanded)
            {
                x0 = Mathf.Min(x0, rm.X); y0 = Mathf.Min(y0, rm.Y); x1 = Mathf.Max(x1, rm.X + rm.W); y1 = Mathf.Max(y1, rm.Y + rm.H);
            }
            foreach (var c in wcChests)
            {
                x0 = Mathf.Min(x0, c.Pos.X - 40f); y0 = Mathf.Min(y0, c.Pos.Y - 40f); x1 = Mathf.Max(x1, c.Pos.X + 40f); y1 = Mathf.Max(y1, c.Pos.Y + 40f);
            }
            x0 = Mathf.Min(x0, me.Pos.X - 40f); y0 = Mathf.Min(y0, me.Pos.Y - 40f); x1 = Mathf.Max(x1, me.Pos.X + 40f); y1 = Mathf.Max(y1, me.Pos.Y + 40f);
            var area = new Rect(r.x + 12f, r.y + 26f, r.width - 24f, r.height - 38f);
            float k = Mathf.Min(area.width / Mathf.Max(1f, x1 - x0), area.height / Mathf.Max(1f, y1 - y0));
            float ox = area.x + (area.width - (x1 - x0) * k) / 2f, oy = area.y + (area.height - (y1 - y0) * k) / 2f;
            System.Func<float, float, Vector2> M = (wx, wy) => new Vector2(ox + (wx - x0) * k, oy + (wy - y0) * k);
            foreach (var rm in game.Expanded)
            {
                var a = M(rm.X, rm.Y);
                var box = new Rect(a.x, a.y, rm.W * k, rm.H * k);
                Ui.Fill(box, new Color(ink.r, ink.g, ink.b, 0.07f));
                CrtKit.Frame(box, 1f, new Color(ink.r, ink.g, ink.b, 0.35f));
            }
            for (int i = 0; i < wcChests.Count; i++)
            {
                var c = wcChests[i];
                var p = M(c.Pos.X, c.Pos.Y);
                var col = ToColor(ChestInkOf(c.Kind));
                if (i == wcChestHot)
                    CrtKit.Frame(new Rect(p.x - 8f, p.y - 8f, 16f, 16f), 2f, CrtStyle.Gold);
                Ui.Fill(new Rect(p.x - 5f, p.y - 5f, 10f, 10f), new Color(0.02f, 0.03f, 0.06f, 0.9f));
                Ui.Fill(new Rect(p.x - 4f, p.y - 4f, 8f, 8f), col);
            }
            var mp = M(me.Pos.X, me.Pos.Y);
            Ui.Dot(new Rect(mp.x - 6f, mp.y - 6f, 12f, 12f), ToColor(me.Def.Color));
        }
    }
}

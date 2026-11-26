using UnityEngine;

namespace Arena
{
    /// The Map key (M by default, Keybinds.cs) cycles the Rooms and Expanding arenas' map: the minimap (top right) → the full
    /// map (the whole world fitted into a big panel mid-screen, a legend under it) → no map → the minimap again. Remembered in
    /// PlayerPrefs. Each switch animates over MapSwitchTime: minimap ↔ full map the window grows (or shrinks) and the view
    /// zooms out to the whole world at the same time; to no map the window switches off like a CRT (its height folds to a
    /// bright line, then the line shrinks to a dot), and comes back from no map the same way in reverse. The drawing is
    /// DrawRoomsMinimap (RoomsLook.cs) in the window MapGeoNow gives it.
    public partial class ArenaApp
    {
        public const int MapMini = 0, MapFull = 1, MapOff = 2;
        const float MapSwitchTime = 0.42f;

        /// Where the map is heading (MapMini/MapFull/MapOff) and how far through the switch (1 = there).
        int mapView, mapFromView;
        float mapT = 1f;
        MapGeo mapFromGeo, mapLastGeo;
        bool mapHasLast;
        float mapOffAt = -10f;  // when the map was switched off (the "Map off" note fades)

        /// A map window: its screen rect, the scale (screen px per world px), the world point at its middle, how much the
        /// screen behind it dims (the full map) and how folded it is (1 = a line, the CRT switching off).
        struct MapGeo
        {
            public Rect R;
            public float K, Dim, Fold;
            public V2 C;
        }

        void LoadMapView()
        {
            mapView = mapFromView = Mathf.Clamp(PlayerPrefs.GetInt("map_view", MapMini), MapMini, MapOff);
            mapT = 1f;
        }

        /// ArenaApp.Update: the Map key, and the switch moving on.
        void TickMapView(float dt)
        {
            if (Keybinds.Down(KeyAction.Map) && GUIUtility.keyboardControl == 0 && session.Screen == AppScreen.Game && game.RoomsOn && game.Running)
                SetMapView((mapView + 1) % 3);
            mapT = Mathf.Min(1f, mapT + dt / MapSwitchTime);
        }

        /// Switch to `v`, animating from wherever the window is now (a switch half way through starts from there).
        void SetMapView(int v, bool save = true)
        {
            if (v == mapView)
                return;
            mapFromView = mapView;
            mapFromGeo = mapLastGeo;
            mapT = mapHasLast ? 0f : 1f;  // (nothing drawn yet: straight there)
            mapView = v;
            if (v == MapOff)
                mapOffAt = Time.unscaledTime;
            if (save)
            {
                PlayerPrefs.SetInt("map_view", v);
                PlayerPrefs.Save();
            }
        }

        /// Tools/Preview (MAPVIEW=from,to,t): the switch from view `from` to `to`, `t` of the way through.
        void PreviewMapView(int from, int to, float t)
        {
            V2 lo, hi;
            MapBounds(out lo, out hi);
            mapFromGeo = MapGeoFor(from, lo, hi);
            mapFromView = from;
            mapView = to;
            mapT = t;
            mapHasLast = true;
            mapOffAt = Time.unscaledTime - t * MapSwitchTime;
        }

        /// The full map's panel: mid-screen, clear of the top banner and the bottom HUD.
        static readonly Rect MapFullRect = new Rect(150f, 66f, W - 300f, H - 66f - 172f);
        const float MapFullTop = 30f, MapFullFoot = 30f;  // the title strip and the legend inside the panel

        static float MapEase(float x) => x < 0.5f ? 4f * x * x * x : 1f - Mathf.Pow(-2f * x + 2f, 3f) / 2f;

        /// The window for view `v` this frame (the minimap follows you; the full map fits the world bounds lo-hi).
        MapGeo MapGeoFor(int v, V2 lo, V2 hi)
        {
            if (v == MapFull)
            {
                var r = MapFullRect;
                float k = Mathf.Min((r.width - 48f) / Mathf.Max(1f, hi.X - lo.X), (r.height - MapFullTop - MapFullFoot - 24f) / Mathf.Max(1f, hi.Y - lo.Y));
                k = Mathf.Min(k, 0.6f);
                // (the title strip and legend are the same height, so the world's middle sits at the panel's middle)
                return new MapGeo { R = r, K = k, C = (lo + hi) * 0.5f, Dim = 1f };
            }
            float hwx = RoomMapW / RoomMapScale / 2f, hwy = RoomMapH / RoomMapScale / 2f;
            V2 at = game.Me != null ? game.Me.Disp : camFocus;
            var c = new V2(hi.X - lo.X <= 2f * hwx ? (lo.X + hi.X) / 2f : Mathf.Clamp(at.X, lo.X + hwx, hi.X - hwx),
                           hi.Y - lo.Y <= 2f * hwy ? (lo.Y + hi.Y) / 2f : Mathf.Clamp(at.Y, lo.Y + hwy, hi.Y - hwy));
            return new MapGeo { R = new Rect(W - RoomMapRight - RoomMapW, RoomMapTop, RoomMapW, RoomMapH), K = RoomMapScale, C = c, Fold = v == MapOff ? 1f : 0f };
        }

        /// The window this frame, part way through a switch if one is on. Null: nothing to draw (the map is off).
        MapGeo? MapGeoNow(V2 lo, V2 hi)
        {
            if (mapT >= 1f)
                return mapView == MapOff ? (MapGeo?)null : MapGeoFor(mapView, lo, hi);
            if (mapView == MapOff)  // switching off: the window it was in folds away
                return Folded(mapFromGeo, mapT);
            if (mapFromView == MapOff)  // switching on: the new window unfolds
                return Folded(MapGeoFor(mapView, lo, hi), 1f - mapT);
            var a = mapFromGeo;
            var b = MapGeoFor(mapView, lo, hi);
            float e = MapEase(mapT);
            return new MapGeo
            {
                R = new Rect(Mathf.Lerp(a.R.x, b.R.x, e), Mathf.Lerp(a.R.y, b.R.y, e), Mathf.Lerp(a.R.width, b.R.width, e), Mathf.Lerp(a.R.height, b.R.height, e)),
                K = Mathf.Pow(a.K, 1f - e) * Mathf.Pow(b.K, e),  // (a steady zoom: even steps in log scale)
                C = a.C + (b.C - a.C) * e,
                Dim = Mathf.Lerp(a.Dim, b.Dim, e),
            };
        }

        /// `g` switching off `f` of the way (0 = on, 1 = gone): the first 60% folds its height to a 2 px line, the rest
        /// shrinks the line to nothing about its middle.
        static MapGeo Folded(MapGeo g, float f)
        {
            float h = Mathf.Clamp01(f / 0.6f), w = Mathf.Clamp01((f - 0.6f) / 0.4f);
            h = h * h;  // (slow at first, then a snap shut)
            float cx = g.R.center.x, cy = g.R.center.y;
            float nh = Mathf.Lerp(g.R.height, 2f, h), nw = Mathf.Lerp(g.R.width, 0f, w * (2f - w));
            return new MapGeo { R = new Rect(cx - nw / 2f, cy - nh / 2f, nw, nh), K = g.K, C = g.C, Dim = g.Dim * (1f - h), Fold = h };
        }

        /// DrawRoomsMinimap, after the GL pass: the full map's title, key hint and legend, and the "Map off" note.
        void DrawMapLabels(MapGeo? geo)
        {
            float off = mapView == MapOff ? Time.unscaledTime - mapOffAt : -1f;
            if (off >= 0f && off < 2.2f && session.Screen == AppScreen.Game)
            {
                float a = Mathf.Clamp01((off - MapSwitchTime * 0.8f) / 0.2f) * Mathf.Clamp01((2.2f - off) / 0.6f);
                if (a > 0f)
                {
                    string key = Keybinds.NameOrNull(KeyAction.Map);
                    var r = new Rect(W - RoomMapRight - RoomMapW, RoomMapTop + RoomMapH / 2f - 11f, RoomMapW, 22f);
                    Ui.Pill(r, new Color(0f, 0f, 0f, 0.45f * a));
                    Ui.Label(r, key != null ? "MAP OFF  ·  " + key.ToUpperInvariant() + " to bring it back" : "MAP OFF", 11, new Color(0.6f, 0.78f, 1f, 0.8f * a), TextAnchor.MiddleCenter, true);
                }
            }
            if (!geo.HasValue)
                return;
            var g = geo.Value;
            float full = Mathf.Clamp01((g.R.width - RoomMapW) / (MapFullRect.width - RoomMapW));  // how far grown into the full map
            if (full < 0.7f || g.Fold > 0.05f)
                return;
            float la = Mathf.Clamp01((full - 0.7f) / 0.3f) * (1f - g.Fold);
            var ink = new Color(0.55f, 0.75f, 1f, 0.9f * la);
            Ui.Label(new Rect(g.R.x + 14f, g.R.y + 6f, 200f, 20f), "MAP", 13, ink, TextAnchor.MiddleLeft, true);
            string mk = Keybinds.NameOrNull(KeyAction.Map);
            if (mk != null)
                Ui.Label(new Rect(g.R.xMax - 314f, g.R.y + 6f, 300f, 20f), mk.ToUpperInvariant() + "  ·  hide the map", 11, new Color(1f, 1f, 1f, 0.5f * la), TextAnchor.MiddleRight, true);
            float lx = g.R.x + 14f, ly = g.R.yMax - MapFullFoot + 4f;
            for (int i = 0; i < MapLegend.Length; i++)
            {
                Ui.Label(new Rect(lx + 14f, ly, 120f, 20f), MapLegend[i], 11, new Color(1f, 1f, 1f, 0.6f * la), TextAnchor.MiddleLeft, true);
                lx += LegendStep(i);
            }
        }

        static readonly string[] MapLegend = { "You", "Team", "Enemies", "Bosses", "Chests, coins", "Doors" };
        static readonly Rgba[] MapLegendInk =  // (Team is drawn in each teammate's colour: its swatch is split)
        {
            new Rgba(1f, 1f, 1f, 0.9f), new Rgba(0.4f, 1f, 0.55f), new Rgba(1f, 0.32f, 0.36f), new Rgba(1f, 0.62f, 0.3f),
            Rgba.Gold, new Rgba(0.373f, 0.941f, 1f),
        };
        static float LegendStep(int i) => 14f + MapLegend[i].Length * 6.2f + 18f;

        /// The legend's swatches and the full map's corner brackets (GL, inside DrawRoomsMinimap).
        void DrawMapChrome(MapGeo g)
        {
            float full = Mathf.Clamp01((g.R.width - RoomMapW) / (MapFullRect.width - RoomMapW));
            if (full <= 0f || g.Fold > 0.05f)
                return;
            var acc = new Rgba(0.4f, 0.64f, 1f, 0.85f * full);
            float L = 18f * full, t = 2f, x0 = g.R.x - 6f, y0 = g.R.y - 6f, x1 = g.R.xMax + 6f, y1 = g.R.yMax + 6f;
            RoomMapRect(x0, y0, L, t, acc); RoomMapRect(x0, y0, t, L, acc);
            RoomMapRect(x1 - L, y0, L, t, acc); RoomMapRect(x1 - t, y0, t, L, acc);
            RoomMapRect(x0, y1 - t, L, t, acc); RoomMapRect(x0, y1 - L, t, L, acc);
            RoomMapRect(x1 - L, y1 - t, L, t, acc); RoomMapRect(x1 - t, y1 - L, t, L, acc);
            if (full < 0.7f)
                return;
            float la = Mathf.Clamp01((full - 0.7f) / 0.3f);
            RoomMapRect(g.R.x + 10f, g.R.y + MapFullTop, g.R.width - 20f, 1f, new Rgba(0.4f, 0.64f, 1f, 0.25f * la));  // under the title
            RoomMapRect(g.R.x + 10f, g.R.yMax - MapFullFoot, g.R.width - 20f, 1f, new Rgba(0.4f, 0.64f, 1f, 0.25f * la));  // over the legend
            float lx = g.R.x + 14f, ly = g.R.yMax - MapFullFoot + 14f;
            for (int i = 0; i < MapLegend.Length; i++)
            {
                RoomMapRect(lx + 2f, ly - 3f, 7f, 7f, MapLegendInk[i].WithAlpha(MapLegendInk[i].A * la));
                if (i == 1)
                    RoomMapRect(lx + 5.5f, ly - 3f, 3.5f, 7f, new Rgba(1f, 0.55f, 0.24f, la));
                lx += LegendStep(i);
            }
        }
    }
}

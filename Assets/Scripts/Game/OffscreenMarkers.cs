using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Off-screen markers while the scroll-wheel zoom (CameraZoom.cs) hides part of the arena: an arrow at the edge of the
    /// view pointing at each enemy and teammate you can't see, on the HUD canvas, under the rest of the HUD.
    ///   teammates  an arrow in their colour with a dark rim and their name just inside it; a downed teammate's arrow
    ///              pulses red and reads "NAME · DOWNED".
    ///   enemies    a small red arrow each (elites orange, a little bigger); bosses a big crimson arrow with a ring.
    ///              The nearest MarkEnemyCap show; the farther one is, the smaller and fainter its arrow.
    ///   area       arrows sit inside the view, below the boss bars and clear of the bottom console (MarkArea); they fade in with the zoom, so
    ///              there are none at 1x.
    /// Local drawing only; no setting of its own (it follows Settings → Gameplay → Scroll-wheel zoom).
    public partial class ArenaApp
    {
        const int MarkEnemyCap = 40;
        const float MarkFar = 700f;  // arena units past the view's edge where an arrow reaches its smallest
        static readonly Rect MarkArea = new Rect(20f, 80f, W - 40f, 590f - 80f);  // (below the boss bars and the top hints, above the console)
        static readonly Rgba MarkEnemy = new Rgba(1f, 0.32f, 0.3f), MarkElite = new Rgba(1f, 0.62f, 0.25f), MarkBoss = new Rgba(0.95f, 0.15f, 0.22f);
        static readonly Rgba MarkChest = new Rgba(1f, 0.82f, 0.3f);
        static readonly Rgba MarkCache = new Rgba(0.25f, 0.9f, 0.7f);  // an unclaimed Stash's vector cache (Docs/BreachRooms.md)
        static readonly Rgba MarkRim= new Rgba(0.02f, 0.03f, 0.06f, 0.85f);

        readonly List<Enemy> markEnemies = new List<Enemy>();
        readonly List<float> markDist = new List<float>();

        /// DrawHud: the markers (GL arrows first, then the teammates' names).
        void DrawOffscreenMarkers()
        {
            if (!CamZoomed || !game.Running)
                return;
            float fade = camFollow ? 1f : Mathf.Clamp01((camZoom - 1f) / 0.12f);  // (the rooms' camera hides things at any zoom: Rooms.cs)
            if (fade <= 0.01f)
                return;
            V2 mid = new V2(MarkArea.center.x, MarkArea.center.y);

            // The off-screen enemies, nearest first.
            markEnemies.Clear();
            markDist.Clear();
            foreach (var e in game.Enemies.Values)
            {
                if (e.Hp <= 0f)
                    continue;
                float d = OffBy(ToUi(e.Disp), e.Radius * UiWorldScale);
                if (d <= 0f)
                    continue;
                int i = markDist.Count;
                while (i > 0 && markDist[i - 1] > d)
                    i--;
                markDist.Insert(i, d);
                markEnemies.Insert(i, e);
            }

            if (BeginUiGl())
            {
                int shown = 0;
                for (int k = 0; k < markEnemies.Count; k++)
                {
                    var e = markEnemies[k];
                    bool boss = e.Def.IsBoss;
                    if (!boss && shown >= MarkEnemyCap)
                        continue;  // (bosses always show)
                    shown++;
                    float near = 1f - Mathf.Clamp01(markDist[k] / (MarkFar * UiWorldScale));
                    float size = boss ? 15f : e.Elite ? 9f : 7f;
                    size *= 0.7f + 0.3f * near;
                    var col = boss ? MarkBoss : e.Elite ? MarkElite : MarkEnemy;
                    V2 at = ToUi(e.Disp);
                    V2 tip = EdgePoint(mid, at);
                    MarkArrow(tip, (at - mid).Normalized(), size, col.WithAlpha(fade * (0.45f + 0.5f * near)));
                    if (boss)
                        Arc(tip - (at - mid).Normalized() * (size * 0.4f), size * 1.35f, 2f, col.WithAlpha(fade * 0.6f), 24);
                }
                foreach (var c in game.Props.Values)  // unopened treasure chests: a gold arrow with a ring
                {
                    if (c.Kind != PropKind.Chest || c.Open || c.Hp <= 0f || OffBy(ToUi(c.Pos), Game.ChestW * 0.5f * UiWorldScale) <= 0f)
                        continue;
                    V2 at = ToUi(c.Pos), dir = (at - mid).Normalized(), tip = EdgePoint(mid, at);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                    MarkArrow(tip, dir, 12f, MarkChest.WithAlpha(fade * pulse));
                    Arc(tip - dir * 5f, 16f, 2f, MarkChest.WithAlpha(fade * 0.55f * pulse), 24);
                }
                DrawGateMarker(mid, fade);  // the boss gate (BossGateLook.cs)
                byte stashKind = (byte)(1 + (int)SurpriseShape.Stash);
                foreach (var r in game.Expanded)  // an unclaimed Stash cache: a teal arrow with a ring
                {
                    if (r.Surprise != stashKind || r.Claimed)
                        continue;
                    V2 at = ToUi(new V2((r.X + r.X1) / 2f, (r.Y + r.Y1) / 2f));
                    if (OffBy(at, 30f * UiWorldScale) <= 0f)
                        continue;
                    V2 dir = (at - mid).Normalized(), tip = EdgePoint(mid, at);
                    float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
                    MarkArrow(tip, dir, 11f, MarkCache.WithAlpha(fade * pulse));
                    Arc(tip - dir * 5f, 15f, 2f, MarkCache.WithAlpha(fade * 0.55f * pulse), 24);
                }
                foreach (var p in game.Players.Values)
                {
                    if (p.Id == game.MyId || OffBy(ToUi(p.Disp), p.Def.BodyR * UiWorldScale) <= 0f)
                        continue;
                    V2 at = ToUi(p.Disp);
                    var col = p.Downed ? new Rgba(1f, 0.3f, 0.3f) : p.Def.Color;
                    float pulse = p.Downed ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
                    MarkArrow(EdgePoint(mid, at), (at - mid).Normalized(), 13f, col.WithAlpha(fade * pulse));
                }
                EndUiGl();
            }

            // Their names, just inside each teammate's arrow.
            foreach (var p in game.Players.Values)
            {
                if (p.Id == game.MyId || OffBy(ToUi(p.Disp), p.Def.BodyR * UiWorldScale) <= 0f)
                    continue;
                V2 at = ToUi(p.Disp);
                V2 dir = (at - mid).Normalized();
                V2 e = EdgePoint(mid, at);
                string name = p.Downed ? p.Name + " · DOWNED" : p.Name;
                var col = p.Downed ? new Color(1f, 0.55f, 0.55f, fade) : ToColor(p.Def.Color.WithAlpha(fade));
                bool side = Mathf.Abs(e.X - MarkArea.x) < 1f || Mathf.Abs(e.X - MarkArea.xMax) < 1f;  // on the left or right edge
                if (side)  // beside the arrow, toward the middle
                {
                    bool right = dir.X > 0f;
                    var r = right ? new Rect(e.X - 32f - 180f, e.Y - 9f, 180f, 18f) : new Rect(e.X + 32f, e.Y - 9f, 180f, 18f);
                    Ui.Label(r, name, 11, col, right ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft, true);
                }
                else  // under an arrow at the top, over one at the bottom
                    Ui.Label(new Rect(e.X - 90f, dir.Y < 0f ? e.Y + 26f : e.Y - 44f, 180f, 18f), name, 11, col, TextAnchor.MiddleCenter, true);
            }
        }

        /// How far a HUD-canvas point (with radius r) lies outside the view, in canvas units (0 or less: on screen).
        static float OffBy(V2 at, float r)
        {
            float dx = Mathf.Max(-r - at.X, at.X - W - r), dy = Mathf.Max(-r - at.Y, at.Y - H - r);
            return Mathf.Max(dx, dy);
        }

        /// Where the line from the view's middle toward `at` meets MarkArea's edge.
        static V2 EdgePoint(V2 mid, V2 at)
        {
            V2 d = at - mid;
            float hx = MarkArea.width / 2f, hy = MarkArea.height / 2f;
            float k = Mathf.Min(Mathf.Abs(d.X) > 0.001f ? hx / Mathf.Abs(d.X) : 1e6f, Mathf.Abs(d.Y) > 0.001f ? hy / Mathf.Abs(d.Y) : 1e6f);
            return mid + d * Mathf.Min(1f, k);
        }

        /// A chevron arrowhead with its point at `tip`, pointing along `dir`, on a dark rim.
        void MarkArrow(V2 tip, V2 dir, float size, Rgba col)
        {
            SetColor(MarkRim.WithAlpha(MarkRim.A * col.A));
            Chevron(tip + dir * 2.5f, dir, size + 2.5f);
            SetColor(col);
            Chevron(tip, dir, size);
        }

        void Chevron(V2 tip, V2 dir, float size)
        {
            V2 side = dir.Orthogonal(), back = tip - dir * (size * 1.6f), notch = tip - dir * (size * 1.1f);
            Tri(tip, back + side * size, notch);
            Tri(tip, notch, back - side * size);
        }
    }
}

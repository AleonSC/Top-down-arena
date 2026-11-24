using UnityEngine;

namespace Arena
{
    /// The Field Hospital's look (Core/FieldHospital.cs, Docs/Mockups/field-hospital.html):
    /// - the pick (your own game, while it waits for a click): every free door spot in reach is a green hologram door with a white
    ///   "+" (the mystery door's look in medic green) and a dashed ghost of the cell behind it; the one a click would take is bright,
    ///   its cell washed and named; a door spot whose cell is taken shows a small red cross;
    /// - the hospital: a green wash and a big faint cross on its floor, a health shrine in the middle (ShrineLook.cs; grey once you
    ///   have used it), a green laser gate across each of its doors (the shutter gate's beams in green) with a bar that empties
    ///   over its minute; outside a Grid run, a tent: a ring of the same green beams round the shrine;
    /// - the HUD: the pick's hint over you, the time left over the door, and EJECT 3, 2, 1 over the cell at the end.
    /// Preview: HOSP=pick|open|<s into its minute> [HOSPTENT=1] (Tools/Preview/ShotHospital.cs).
    public partial class ArenaApp
    {
        static readonly Rgba HospInk = new Rgba(0.4f, 1f, 0.6f), HospHot = new Rgba(0.85f, 1f, 0.9f), HospDark = new Rgba(0.02f, 0.08f, 0.05f);
        static readonly Rgba HospTaken = new Rgba(1f, 0.3f, 0.35f), HospGold = new Rgba(1f, 0.82f, 0.32f);

        static bool HospUsedByMe(Zone z, int me) => (z.Side & (1 << (me & 31))) != 0;

        /// In the zone pass (under the walls): the floor wash and the shrine.
        void DrawHospitalZone(Zone z, float t)
        {
            float fade = Mathf.Clamp01(z.T / 0.4f) * Mathf.Clamp01((z.Duration - z.T) / 0.4f + 0.2f);
            bool room = Game.HospIsRoom(z);
            if (room)
            {
                float h = z.Radius;
                FillRect(z.Pos.X - h, z.Pos.Y - h, 2f * h, 2f * h, HospInk.WithAlpha(0.06f * fade));
                float arm = h * 0.55f, w = h * 0.16f;  // a big faint cross painted on the floor
                FillRect(z.Pos.X - arm, z.Pos.Y - w, 2f * arm, 2f * w, HospInk.WithAlpha(0.05f * fade));
                FillRect(z.Pos.X - w, z.Pos.Y - arm, 2f * w, 2f * arm, HospInk.WithAlpha(0.05f * fade));
            }
            else
            {
                Circle(z.Pos, z.Radius, HospInk.WithAlpha(0.07f * fade), 48);
            }
            DrawShrine(z.Pos, HospUsedByMe(z, game.MyId), t);
        }

        /// Over the walls (after the mystery doors): each hospital's gate, and the pick.
        void DrawHospitals(float t)
        {
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.FieldHospital)
                    DrawHospitalGate(z, t);
            var me = game.Me;
            if (me != null && game.HospPickSlot >= 0)
                DrawHospPick(me, t);
        }

        void DrawHospitalGate(Zone z, float t)
        {
            float fade = Mathf.Clamp01(z.T / 0.3f);
            float hum = 0.85f + 0.15f * Mathf.Sin(t * 9f + z.Id);
            if (!Game.HospIsRoom(z))
            {
                const int posts = 16;  // a tent: posts round the ring with beams between them
                for (int k = 0; k < posts; k++)
                {
                    float a0 = MathX.Tau * k / posts + t * 0.1f, a1 = a0 + MathX.Tau / posts;
                    V2 p0 = z.Pos + V2.FromAngle(a0) * z.Radius, p1 = z.Pos + V2.FromAngle(a1) * z.Radius;
                    Line(p0, p1, 6f, HospInk.WithAlpha(0.2f * hum * fade));
                    Line(p0, p1, 2f, HospHot.WithAlpha(0.85f * hum * fade));
                    FillRect(p0.X - 4f, p0.Y - 4f, 8f, 8f, HospHot.WithAlpha(fade));
                }
                Arc(z.Pos, z.Radius + 10f, 3f, HospInk.WithAlpha(0.8f * fade), 64, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(z.T / z.Duration));
                return;
            }
            int area = Expansion.HospitalAt(game.Expanded, z.Pos) + 1;
            var world = game.CurrentWorld;
            if (area <= 0 || world == null || world.Doors == null)
                return;
            foreach (var d in world.Doors)
            {
                if (!d.Joins(area))
                    continue;
                V2 c = d.Pos, along = d.Along, across = d.Across;
                float half = d.W / 2f, wt = Rooms.WallT / 2f + 3f;
                SetColor(HospInk.WithAlpha(0.12f * hum * fade));
                Quad(c - along * half - across * (wt + 6f), c + along * half - across * (wt + 6f), c + along * half + across * (wt + 6f), c - along * half + across * (wt + 6f));
                for (int k = 0; k < 3; k++)  // three beams along the gap
                {
                    float u = (k - 1) * 6f;
                    V2 a = c - along * half + across * u, b = c + along * half + across * u;
                    float flick = z.T < Game.HospEject && Mathf.Repeat(t * 14f + k * 0.37f, 1f) < 0.3f ? 0.35f : 1f;
                    Line(a, b, 6f, HospInk.WithAlpha(0.22f * hum * flick * fade));
                    Line(a, b, 2f, HospHot.WithAlpha(0.95f * hum * flick * fade));
                }
                for (int s = -1; s <= 1; s += 2)  // the emitter posts
                {
                    V2 p = c + along * ((half + 3f) * s);
                    Circle(p, 11f, HospInk.WithAlpha(0.25f * fade), 14);
                    FillRect(p.X - 5f, p.Y - 5f, 10f, 10f, HospHot.WithAlpha(fade));
                    FillRect(p.X - 2.5f, p.Y - 2.5f, 5f, 5f, HospInk.WithAlpha(fade));
                }
                // the time left: a bar along the gap, outside the room
                V2 outSide = (c - z.Pos).Dot(across) > 0f ? across : -across;
                V2 bar0 = c - along * half + outSide * (wt + 12f);
                float left = Mathf.Clamp01(z.T / z.Duration);
                Line(bar0, bar0 + along * (2f * half), 4f, new Rgba(0f, 0f, 0f, 0.55f * fade));
                Line(bar0, bar0 + along * (2f * half * left), 4f, (z.T < Game.HospEject ? HospGold : HospInk).WithAlpha(0.95f * fade));
            }
        }

        /// The pick: lit doors, ghost cells, red crosses where a cell is taken.
        void DrawHospPick(Player me, float t)
        {
            var pick = game.HospPickTarget(me, me.AimAt);
            Arc(me.Disp, Game.HospReach, 1.5f, HospInk.WithAlpha(0.12f + 0.05f * Mathf.Sin(t * 3f)), 96);
            float c = Expansion.GridCell;
            int i = 0;
            foreach (var s in game.HospSpots(me))
            {
                i++;
                bool vertical = s.Side >= 2;
                if (!s.Free)
                {
                    float k = 8f;
                    Line(s.Door + new V2(-k, -k), s.Door + new V2(k, k), 3f, HospTaken.WithAlpha(0.85f));
                    Line(s.Door + new V2(k, -k), s.Door + new V2(-k, k), 3f, HospTaken.WithAlpha(0.85f));
                    continue;
                }
                bool hot = pick.HasValue && pick.Value.X == s.X && pick.Value.Y == s.Y;
                DrawHospGhost(s.X, s.Y, c, t, hot);
                DrawHospDoor(s.Door, vertical, t, i * 1.3f, hot);
            }
        }

        /// The dashed outline of the cell a door would open (hot: washed, brighter, its cross showing).
        void DrawHospGhost(float x, float y, float c, float t, bool hot)
        {
            float m = 10f;
            if (hot)
            {
                FillRect(x + m, y + m, c - 2f * m, c - 2f * m, HospInk.WithAlpha(0.1f));
                ShrineCross(new V2(x + c / 2f, y + c / 2f), 1.6f, HospInk.WithAlpha(0.45f));
            }
            var col = HospInk.WithAlpha(hot ? 0.9f : 0.35f);
            float dash = 18f, gap = 12f, off = Mathf.Repeat(t * 30f, dash + gap);
            V2[] corners = { new V2(x + m, y + m), new V2(x + c - m, y + m), new V2(x + c - m, y + c - m), new V2(x + m, y + c - m) };
            for (int e = 0; e < 4; e++)
            {
                V2 a = corners[e], b = corners[(e + 1) % 4], d = (b - a).Normalized();
                float len = (b - a).Length;
                for (float u = -off; u < len; u += dash + gap)
                {
                    float u0 = Mathf.Max(0f, u), u1 = Mathf.Min(len, u + dash);
                    if (u1 > u0)
                        Line(a + d * u0, a + d * u1, hot ? 2.5f : 1.5f, col);
                }
            }
        }

        /// A lit door: the mystery door's hologram in medic green, a white "+" instead of its "?".
        void DrawHospDoor(V2 c, bool vertical, float t, float seed, bool hot)
        {
            V2 along = vertical ? new V2(0f, 1f) : new V2(1f, 0f), across = vertical ? new V2(1f, 0f) : new V2(0f, 1f);
            float half = Rooms.DoorW / 2f, wt = Rooms.WallT / 2f + 4f;
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 3f + seed);
            float flick = 0.88f + 0.12f * Mathf.Sin(t * 23f + seed * 3f);
            float g = hot ? 1f : 0.7f;
            SetColor(HospInk.WithAlpha((hot ? 0.26f : 0.14f) * pulse));  // the halo
            Quad(c - along * (half + 18f) - across * (wt + 18f), c + along * (half + 18f) - across * (wt + 18f), c + along * (half + 18f) + across * (wt + 18f), c - along * (half + 18f) + across * (wt + 18f));
            SetColor(HospDark.WithAlpha(0.92f));
            Quad(c - along * half - across * wt, c + along * half - across * wt, c + along * half + across * wt, c - along * half + across * wt);
            SetColor(HospInk.WithAlpha(0.32f * flick * g));
            Quad(c - along * half - across * wt, c + along * half - across * wt, c + along * half + across * wt, c - along * half + across * wt);
            for (int k = 0; k < 5; k++)  // scan lines drifting along it
            {
                float at = -half + 2f * half * Mathf.Repeat(t * 0.35f + k / 5f, 1f);
                Line(c + along * at - across * wt, c + along * at + across * wt, 1.5f, HospHot.WithAlpha(0.45f * flick * g));
            }
            Line(c - along * half - across * wt, c + along * half - across * wt, 2.5f, HospInk.WithAlpha(0.95f * g));
            Line(c - along * half + across * wt, c + along * half + across * wt, 2.5f, HospInk.WithAlpha(0.95f * g));
            float b = 14f, ob = wt + 7f;  // corner brackets past the wall
            foreach (int sa in new[] { -1, 1 })
                foreach (int sc in new[] { -1, 1 })
                {
                    V2 corner = c + along * (sa * (half + 4f)) + across * (sc * ob);
                    Line(corner, corner - along * (sa * b), 3f, HospInk.WithAlpha(0.95f * g));
                    Line(corner, corner - across * (sc * b), 3f, HospInk.WithAlpha(0.95f * g));
                }
            float arm = hot ? 11f : 9f;  // the "+"
            Line(c - new V2(arm, 0f), c + new V2(arm, 0f), 6f, HospInk.WithAlpha(0.6f));
            Line(c - new V2(0f, arm), c + new V2(0f, arm), 6f, HospInk.WithAlpha(0.6f));
            Line(c - new V2(arm, 0f), c + new V2(arm, 0f), 3f, new Rgba(1f, 1f, 1f, 0.95f));
            Line(c - new V2(0f, arm), c + new V2(0f, arm), 3f, new Rgba(1f, 1f, 1f, 0.95f));
        }

        /// DrawHud: the pick's hint over you; each hospital's time left over its door and EJECT n over it at the end.
        void DrawHospitalHint()
        {
            var me = game.Me;
            if (me != null && game.HospPickSlot >= 0)
            {
                string key = Keybinds.KeyName(Keybinds.Get(KeyAction.Ultimate)).ToUpperInvariant();
                bool any = game.HospPickTarget(me, me.AimAt).HasValue;
                V2 over = ToUi(me.Disp);
                var r = new Rect(over.X - 140f, over.Y + 40f, 280f, 22f);  // (under you: the doors are usually above)
                Ui.Pill(r, any ? new Color(0.4f, 1f, 0.6f, 0.92f) : new Color(0f, 0f, 0f, 0.65f));
                Ui.Label(r, any ? "CLICK  OPEN FIELD HOSPITAL   ·   " + key + "  CANCEL" : "PICK A LIT DOOR   ·   " + key + "  CANCEL", 11,
                    any ? new Color(0.03f, 0.12f, 0.06f) : new Color(0.6f, 1f, 0.72f), TextAnchor.MiddleCenter, true);
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.FieldHospital)
                    continue;
                if (z.T < Game.HospEject)
                {
                    int n = Mathf.Max(1, Mathf.CeilToInt(z.T));
                    V2 top = ToUi(z.Pos - new V2(0f, z.Radius * 0.6f));
                    var big = new Rect(top.X - 100f, top.Y - 22f, 200f, 44f);
                    Ui.Label(big, "EJECT " + n, 34, new Color(1f, 0.82f, 0.32f), TextAnchor.MiddleCenter, true);
                    continue;
                }
                V2 tag = Game.HospIsRoom(z) ? ToUi(z.Pos + V2.FromAngle(z.Aim) * (z.Radius + 46f)) : ToUi(z.Pos + new V2(0f, z.Radius + 26f));
                var r = new Rect(tag.X - 70f, tag.Y - 9f, 140f, 18f);
                Ui.Pill(r, new Color(0f, 0f, 0f, 0.6f));
                Ui.Label(r, "FIELD HOSPITAL  " + Mathf.CeilToInt(z.T) + " s", 10, new Color(0.55f, 1f, 0.7f), TextAnchor.MiddleCenter, true);
            }
        }
    }
}

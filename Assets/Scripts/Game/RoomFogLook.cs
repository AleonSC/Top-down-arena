using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The look of room fog (Docs/FogOfWar.md, Core/RoomFog.cs): an unseen room under a dark veil with slow diagonal hatching
    /// (its walls, doors and breaches stay bright), its enemies and pickups hidden unless a player is within peeking reach; as
    /// it's seen the veil lifts behind a gold scan line. On the maps: dark with a "?" (RoomsLook.cs, ExpansionUi.cs).
    public partial class ArenaApp
    {
        static readonly Rgba FogVeil = new Rgba(0.01f, 0.015f, 0.035f, 0.72f);
        static readonly Rgba FogHatch = new Rgba(0.45f, 0.55f, 0.75f, 0.05f);
        static readonly Rgba FogMapFill = new Rgba(0.02f, 0.03f, 0.06f, 0.92f);
        static readonly Color RoomFogInk = new Color(0.62f, 0.7f, 0.86f, 0.75f);

        readonly HashSet<int> fogDark = new HashSet<int>();                 // areas drawn unseen last frame
        readonly Dictionary<int, float> fogLift = new Dictionary<int, float>();  // area -> when it was seen (the sweep)
        int fogMap = -1;  // the map number the areas belong to (a map reset renumbers them)

        /// DrawArenaContents, over the floor and under the walls: the veil on each unseen room, the lift on a room just seen.
        void DrawRoomFogVeil(float t)
        {
            if (!game.ExpandingOn || !game.RoomsOn)
                return;
            if (fogMap != game.MapNumber)
            {
                fogMap = game.MapNumber;
                fogDark.Clear();
                fogLift.Clear();
            }
            var wd = game.CurrentWorld;
            float now = Time.unscaledTime;
            for (int i = 1; i < wd.Areas.Length; i++)
            {
                var a = wd.Areas[i];
                if (game.AreaUnseen(i))
                {
                    fogDark.Add(i);
                    DrawFogBox(a, 1f, t);
                    continue;
                }
                if (fogDark.Remove(i))
                    fogLift[i] = now;
                float at;
                if (!fogLift.TryGetValue(i, out at))
                    continue;
                float k = (now - at) / RoomFog.RevealTime;
                if (k >= 1f)
                {
                    fogLift.Remove(i);
                    continue;
                }
                // The lift: the veil pulls back left to right behind a gold scan line, a soft gold flash over what's uncovered.
                float x = Mathf.Lerp(a.Min.X, a.Max.X, k);
                var rest = new RoomArea { Min = new V2(x, a.Min.Y), Max = a.Max };
                DrawFogBox(rest, 1f - k * 0.5f, t);
                FillRect(a.Min.X, a.Min.Y, x - a.Min.X, a.Size.Y, DoorGold.WithAlpha(0.12f * (1f - k)));
                Line(new V2(x, a.Min.Y), new V2(x, a.Max.Y), 4f, DoorGold.WithAlpha(0.9f * (1f - k * k)));
            }
        }

        /// One box of veil: the dark fill and diagonal hatching drifting slowly.
        void DrawFogBox(RoomArea a, float alpha, float t)
        {
            var sz = a.Size;
            if (sz.X <= 1f || sz.Y <= 1f)
                return;
            FillRect(a.Min.X, a.Min.Y, sz.X, sz.Y, FogVeil.WithAlpha(FogVeil.A * alpha));
            const float step = 48f;
            float drift = (t * 10f) % step;
            for (float s = -sz.Y + drift; s < sz.X; s += step)  // lines x - y = s across the box, clipped to it
            {
                float x0 = Mathf.Max(s, 0f), x1 = Mathf.Min(s + sz.Y, sz.X);
                if (x1 <= x0)
                    continue;
                Line(new V2(a.Min.X + x0, a.Min.Y + x0 - s), new V2(a.Min.X + x1, a.Min.Y + x1 - s), 2f, FogHatch.WithAlpha(FogHatch.A * alpha));
            }
        }

        /// The maps: an unseen room's box drawn dark (over its fill), for the "?" afterwards.
        void DrawFogMapBox(V2 p0, V2 p1)
        {
            RoomMapRect(p0.X, p0.Y, p1.X - p0.X, p1.Y - p0.Y, FogMapFill);
        }

        /// The maps, after the GL pass: a "?" in the middle of each unseen room (its head box), sized to the room.
        void DrawFogMapMarks(System.Func<V2, V2> M, Rect clip)
        {
            if (!game.ExpandingOn)
                return;
            var rooms = game.Expanded;
            for (int i = 0; i < rooms.Count; i++)
            {
                var r = rooms[i];
                if (!r.Unseen || Expansion.HeadOf(rooms, i) != i)
                    continue;
                V2 p0 = M(new V2(r.X, r.Y)), p1 = M(new V2(r.X1, r.Y1));
                float h = Mathf.Min(p1.X - p0.X, p1.Y - p0.Y);
                if (h < 7f)
                    continue;
                var c = new V2((p0.X + p1.X) / 2f, (p0.Y + p1.Y) / 2f);
                if (!clip.Contains(new Vector2(c.X, c.Y)))
                    continue;
                int size = Mathf.Clamp(Mathf.RoundToInt(h * 0.6f), 8, 22);
                Ui.Label(new Rect(c.X - 20f, c.Y - size, 40f, 2f * size), "?", size, RoomFogInk, TextAnchor.MiddleCenter, true);
            }
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// A One-way piece's door (Core/OneWayDoors.cs): amber jambs and a row of chevrons across the doorway that flow the way it opens, with a
    /// faint wash on the side you come in from. Drawn instead of the plain door's dashes (DrawRoomsFloor, RoomsLook.cs).
    public partial class ArenaApp
    {
        static readonly Rgba OneWayInk = new Rgba(1f, 0.72f, 0.28f);

        void DrawOneWayDoor(World wd, RoomDoor d, float a)
        {
            int into = d.Pass > 0 ? d.B : d.A;
            V2 dir = (wd.Areas[into].Mid - d.Pos).Dot(d.Across) >= 0f ? d.Across : -d.Across;  // the way through
            float half = d.W / 2f, t = game.AnimT;
            V2 j0 = d.Pos - d.Along * half, j1 = d.Pos + d.Along * half, wt = d.Across * (Rooms.WallT * 0.9f);
            SetColor(OneWayInk.WithAlpha(0.06f * a));  // the wash on the way in
            Quad(j0 - dir * 46f, j1 - dir * 46f, j1, j0);
            foreach (var j in new[] { j0, j1 })
            {
                Line(j - wt, j + wt, 6f, OneWayInk.WithAlpha(0.22f * a));
                Line(j - wt, j + wt, 2.5f, OneWayInk.WithAlpha(a));
            }
            // three lanes of chevrons, each moving through the gap and fading at its ends
            const int lanes = 3, per = 3;
            const float run = 56f, size = 22f;
            for (int l = 0; l < lanes; l++)
            {
                V2 lane = d.Pos + d.Along * ((l - (lanes - 1) / 2f) * (d.W / lanes));
                for (int k = 0; k < per; k++)
                {
                    float u = Mathf.Repeat(t * 0.9f + k / (float)per, 1f);  // 0..1 along the run
                    float fade = Mathf.Sin(u * Mathf.PI);
                    V2 c = lane + dir * (-run / 2f + u * run);
                    Chevron(c, dir, size, 5f, OneWayInk.WithAlpha(0.18f * fade * a));
                    Chevron(c, dir, size, 2f, OneWayInk.WithAlpha(0.9f * fade * a));
                }
            }
        }

        /// The build map (BuildMapLook.cs): each one-way door as an amber bar across the doorway with an arrow through it the way it opens, gently pulsing along.
        void DrawHoloOneWays(World wd, System.Func<V2, V2> M, float k)
        {
            var ow = wd.OneWays;
            for (int i = 0; i < ow.Length; i++)
            {
                var d = wd.Doors[ow[i]];
                int into = d.Pass > 0 ? d.B : d.A;
                V2 dir = (wd.Areas[into].Mid - d.Pos).Dot(d.Across) >= 0f ? d.Across : -d.Across;
                V2 c = M(d.Pos);
                float half = Mathf.Max(6f, d.W / 2f * k), sz = Mathf.Clamp(half * 1.4f, 9f, 16f);
                Line(c - d.Along * half, c + d.Along * half, 2.5f, OneWayInk.WithAlpha(0.9f));
                float nudge = Mathf.Repeat(game.AnimT * 0.8f + i * 0.37f, 1f) * sz * 0.35f;
                V2 tail = c - dir * sz * 0.8f, tip = c + dir * (sz + nudge), wing = d.Along * sz * 0.65f, back = dir * sz * 0.75f;
                foreach (var pass in new[] { 0, 1 })  // a dark outline under the amber, so it reads over walls and floor
                {
                    float w = pass == 0 ? 5f : 2.5f;
                    var col = pass == 0 ? new Rgba(0f, 0f, 0f, 0.7f) : OneWayInk;
                    Line(tail, tip, w, col);
                    Line(tip - back + wing, tip, w, col);
                    Line(tip - back - wing, tip, w, col);
                }
            }
        }
    }
}

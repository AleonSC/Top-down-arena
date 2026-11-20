using System.Collections.Generic;
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
                DrawHoloOneWay(wd, ow[i], M, k, i * 0.37f);
        }

        void DrawHoloOneWay(World wd, int door, System.Func<V2, V2> M, float k, float seed)
        {
            var d = wd.Doors[door];
            int into = d.Pass > 0 ? d.B : d.A;
            V2 dir = (wd.Areas[into].Mid - d.Pos).Dot(d.Across) >= 0f ? d.Across : -d.Across;
            V2 c = M(d.Pos);
            float half = Mathf.Max(6f, d.W / 2f * k), sz = Mathf.Clamp(half * 1.4f, 9f, 16f);
            Line(c - d.Along * half, c + d.Along * half, 2.5f, OneWayInk.WithAlpha(0.9f));
            float nudge = Mathf.Repeat(game.AnimT * 0.8f + seed, 1f) * sz * 0.35f;
            HoloArrow(c - dir * sz * 0.8f, c + dir * (sz + nudge), d.Along, sz, OneWayInk, false);
        }

        /// An arrow from tail to tip (wings `sz` wide along `across`), over a dark outline so it reads on walls and floor; both: a head at each end.
        void HoloArrow(V2 tail, V2 tip, V2 across, float sz, Rgba ink, bool both)
        {
            V2 dir = (tip - tail).Normalized(), wing = across * sz * 0.65f, back = dir * sz * 0.75f;
            foreach (var pass in new[] { 0, 1 })
            {
                float w = pass == 0 ? 5f : 2.5f;
                var col = pass == 0 ? new Rgba(0f, 0f, 0f, 0.7f) : ink;
                Line(tail, tip, w, col);
                Line(tip - back + wing, tip, w, col);
                Line(tip - back - wing, tip, w, col);
                if (both)
                {
                    Line(tail + back + wing, tail, w, col);
                    Line(tail + back - wing, tail, w, col);
                }
            }
        }

        World ghostWays;      // the map as it would be with the ghost built (for a One-way card's arrows), and what it was built for
        string ghostWaysKey;

        /// The build screen's ghost of a One-way card (GridRoomsUi.cs): the arrow each of its doors would get, and a two-headed one on the door that stays
        /// both ways, worked out on the map as it would be with the piece built (so they match what building it gives).
        void DrawGhostOneWays(System.Func<V2, V2> G, Placement pick, PieceCard card)
        {
            if (!Expansion.IsShapePieceCard(card) || PieceMods.OneWay.Get(Expansion.PieceCardMods(card)) == 0)
                return;
            string key = (int)card + ":" + pick.Orient + ":" + pick.X + "," + pick.Y + ":" + game.Expanded.Count + ":" + game.Openings.Count;
            if (key != ghostWaysKey)
            {
                var l = new List<ExpansionRoom>(game.Expanded);
                foreach (var q in pick.Rooms)
                {
                    var q2 = q;
                    q2.OneWay = true;
                    l.Add(q2);
                }
                ghostWays = Expansion.Build(l, game.Openings);
                ghostWaysKey = key;
            }
            var wd = ghostWays;
            int first = game.Expanded.Count + 1;  // (the ghost's areas)
            float k = (G(new V2(1f, 0f)) - G(V2.Zero)).X;
            bool freeShown = false;
            for (int i = 0; i < wd.Doors.Length; i++)
            {
                var d = wd.Doors[i];
                if ((d.A >= first) == (d.B >= first))
                    continue;  // (not one of the ghost's doors)
                if (d.Pass != 0)
                    DrawHoloOneWay(wd, i, G, k, i * 0.37f);
                else if (!freeShown)
                {
                    freeShown = true;
                    float sz = Mathf.Clamp(d.W / 2f * k * 1.4f, 9f, 16f);
                    int inside = d.A >= first ? d.A : d.B;
                    V2 inward = (wd.Areas[inside].Mid - d.Pos).Dot(d.Across) >= 0f ? d.Across : -d.Across;
                    V2 c = G(d.Pos) + inward * (sz * 1.9f);  // (just inside the ghost, clear of the door spot's mark on the wall)
                    HoloArrow(c - d.Across * sz, c + d.Across * sz, d.Along, sz, GridOk, true);
                }
            }
        }
    }
}

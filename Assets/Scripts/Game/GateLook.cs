using UnityEngine;

namespace Arena
{
    /// The Expanding arena's shutter gates (Core/Gates.cs, Docs/RoomBuilding2.md step 3), the Laser grid look the owner picked from
    /// Docs/Mockups/room-fittings.html (B): two emitter posts at the door's ends; when shut, red beams fill the doorway one by one
    /// across the gap (the same red as the breach barrier), a faint red field, and a timer bar; while it recharges the posts show a
    /// filling arc. Standing by a ready gate shows the key to shut it.
    public partial class ArenaApp
    {
        static readonly Rgba GateRed = new Rgba(1f, 0.25f, 0.28f), GateHot = new Rgba(1f, 0.6f, 0.55f), GatePost = new Rgba(1f, 0.82f, 0.78f), GateIdle = new Rgba(0.55f, 0.65f, 0.85f);

        /// Every gate on the map, over the walls (after DrawLayout).
        void DrawGates()
        {
            if (!game.ExpandingOn || !game.RoomsOn)
                return;
            for (int i = 0; i < game.GateFits.Count; i++)
                if (game.GateLive(i))
                    DrawGate(game.GateFits[i], game.GateShut(i), game.GateCool(i), i * 0.71f);
        }

        void DrawGate(GateFit g, float shut, float cool, float seed)
        {
            float t = game.AnimT;
            V2 c = g.Pos, along = g.Vertical ? new V2(0f, 1f) : new V2(1f, 0f), across = g.Vertical ? new V2(1f, 0f) : new V2(0f, 1f);
            float half = Rooms.DoorW / 2f, wt = Rooms.WallT / 2f + 3f;
            float since = Gates.CloseTime - shut;  // seconds since it shut
            float fill = shut > 0f ? Mathf.Clamp01(since / Gates.Grow) * Mathf.Clamp01(shut / 0.35f) : 0f;  // the beams come in, then go at the end
            if (fill > 0f)
            {
                float hum = 0.85f + 0.15f * Mathf.Sin(t * 9f + seed);
                SetColor(GateRed.WithAlpha(0.14f * fill * hum));
                Quad(c - along * half - across * (wt + 6f), c + along * half - across * (wt + 6f), c + along * half + across * (wt + 6f), c - along * half + across * (wt + 6f));
                const int n = 7;
                int lit = Mathf.CeilToInt(fill * n);
                for (int k = 0; k < n; k++)
                {
                    if (k >= lit)
                        break;
                    float u = -half + (k + 0.5f) * (2f * half / n);
                    float flick = shut < 1.5f && Mathf.Repeat(t * 14f + k * 0.37f, 1f) < 0.3f ? 0.35f : 1f;  // flickers in its last moments
                    V2 a = c + along * u - across * wt, b = c + along * u + across * wt;
                    Line(a, b, 6f, GateRed.WithAlpha(0.22f * hum * flick));
                    Line(a, b, 2f, GateHot.WithAlpha(0.95f * hum * flick));
                }
                // the timer: a thin bar along the gap that empties
                float left = Mathf.Clamp01(shut / Gates.CloseTime);
                V2 bar0 = c - along * half - across * (wt + 12f);
                Line(bar0, bar0 + along * (2f * half), 3f, new Rgba(0f, 0f, 0f, 0.5f * fill));
                Line(bar0, bar0 + along * (2f * half * left), 3f, GateHot.WithAlpha(0.9f * fill));
            }
            // the emitter posts at the gap's ends
            for (int s = -1; s <= 1; s += 2)
            {
                V2 p = c + along * ((half + 3f) * s);
                bool hot = shut > 0f;
                Circle(p, hot ? 11f : 8f, (hot ? GateRed : GateIdle).WithAlpha(hot ? 0.25f : 0.12f), 14);
                FillRect(p.X - 5f, p.Y - 5f, 10f, 10f, hot ? GatePost : GateIdle.WithAlpha(0.85f));
                FillRect(p.X - 2.5f, p.Y - 2.5f, 5f, 5f, hot ? GateRed : new Rgba(0.1f, 0.14f, 0.24f));
                if (!hot && cool > 0f)  // recharging: an arc filling round the post
                    Arc(p, 10f, 2f, GateHot.WithAlpha(0.8f), 16, -Mathf.PI / 2f, MathX.Tau * (1f - cool / Gates.Cooldown));
                else if (!hot)
                    Arc(p, 10f, 1.5f, GateIdle.WithAlpha(0.45f + 0.2f * Mathf.Sin(t * 2f + seed)), 16);
            }
        }

        /// DrawHud: by a gate, the key to shut it (or how long until it can be shut again).
        void DrawGateHint()
        {
            var me = game.Me;
            if (me == null || me.Downed || !game.ExpandingOn || !game.RoomsOn)
                return;
            int i = game.GateNear(me.Pos);
            if (i < 0)
                return;
            float shut = game.GateShut(i), cool = game.GateCool(i);
            V2 at = ToUi(game.GateFits[i].Pos);
            string key = Keybinds.KeyName(Keybinds.Get(KeyAction.TakeItem)).ToUpperInvariant();
            string text = shut > 0f ? "GATE SHUT  ·  " + Mathf.CeilToInt(shut) + " s" : cool > 0f ? "GATE READY IN " + Mathf.CeilToInt(cool) + " s" : key + "   SHUT GATE";
            var r = new Rect(at.X - 80f, at.Y - 54f, 160f, 22f);
            bool ready = shut <= 0f && cool <= 0f;
            Ui.Pill(r, ready ? new Color(1f, 0.25f, 0.28f, 0.85f) : new Color(0f, 0f, 0f, 0.6f));
            Ui.Label(r, text, 11, ready ? new Color(0.06f, 0.04f, 0.06f) : new Color(1f, 0.7f, 0.68f), TextAnchor.MiddleCenter, true);
        }
    }
}

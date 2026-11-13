using UnityEngine;

namespace Arena
{
    /// The boss gate (Core/BossGate.cs), a summoning circle on the hub floor: two rune rings turning opposite ways, a
    /// seven-point star, a skull in the middle. It grows open over 0.9 s (timed from when this screen saw it), glows
    /// brighter and spins faster as the team's fill rises, and has a socket per standing player round its bottom, lit as
    /// each steps in (a chime on your screen), with a thread from each player inside. Reduced effects stops the turning.
    /// Its colour leans toward the coming boss's (BossGateInk). It also shows on the minimap (DrawGateOnMap, RoomsLook.cs) and as
    /// an arrow at the edge of a zoomed view (DrawGateMarker, OffscreenMarkers.cs).
    public partial class ArenaApp
    {
        static readonly Rgba BgRed = new Rgba(1f, 0.22f, 0.25f), BgHot = new Rgba(1f, 0.72f, 0.45f), BgDark = new Rgba(0.04f, 0.0f, 0.02f);

        float gateSeenAt = -1f;  // Time.time when this screen saw the gate open (-1: it's shut)
        internal float gateAgeOverride = -1f;  // Tools/Preview: a fixed age
        int gateInsideWas;       // players in it last frame (the chime as one more steps in)
        float gateHumAt;         // Time.unscaledTime of the next low pulse while it's open (the hum)
        public const float GateToastTime = 4f;  // seconds the "boss gate is open" toast stays up
        Rgba gateRed = BgRed, gateHot = BgHot;  // this frame's colours (BossGateInk)

        /// The coming boss's colour, leaned on the gate's red so it still reads as the boss gate.
        Rgba BossGateInk()
        {
            var boss = Defs.Get(game.BossAt(game.Hunt != null ? game.Wave + 1 : game.Wave)).Color;
            return BgRed.Mix(boss.WithAlpha(1f), 0.6f);
        }

        void DrawBossGate(float t)
        {
            if (!game.GateUp)
            {
                gateSeenAt = -1f;
                return;
            }
            if (gateSeenAt < 0f)
            {
                gateSeenAt = Time.time;
                gateInsideWas = 0;
            }
            gateRed = BossGateInk();
            gateHot = gateRed.Mix(new Rgba(1f, 0.92f, 0.75f), 0.6f);
            float age = gateAgeOverride >= 0f ? gateAgeOverride : Time.time - gateSeenAt;
            float open = Mathf.Clamp01(age / 0.9f), e = 1f - (1f - open) * (1f - open) * (1f - open);
            float fill = Mathf.Clamp01(game.GateFill), tt = fxReduced ? 0f : t * (1f + 2.5f * fill);
            float pulse = fxReduced ? 0.6f : 0.5f + 0.5f * Mathf.Sin(t * 3f);
            DrawGateSigil(game.GatePos, e, pulse, fill, tt);
            int alive, inside;
            game.GateCount(out alive, out inside);
            if (inside > gateInsideWas && gateAgeOverride < 0f)
                sounds.Play(Sfx.BossChime, 0.4f + 0.2f * inside / Mathf.Max(1, alive), Time.unscaledTime);  // one more in
            gateInsideWas = inside;
            if (gateAgeOverride < 0f && fill < 1f && Time.unscaledTime >= gateHumAt)  // the hum: a soft low pulse every 2.4 s while it waits
            {
                gateHumAt = Time.unscaledTime + 2.4f;
                sounds.Play(Sfx.BossRumble, 0.14f, Time.unscaledTime);
            }
            DrawGateSockets(game.GatePos, alive, inside, e, t);
        }

        void DrawGateSigil(V2 c, float e, float pulse, float fill, float t)
        {
            float R = Game.GateR * e;
            if (R < 1f)
                return;
            var red = gateRed.Mix(gateHot, fill * 0.6f);
            GlowDisc(c, R * 2.2f, gateRed, 0.25f + 0.12f * pulse + 0.35f * fill);
            Circle(c, R * 1.18f, BgDark.WithAlpha(0.75f), 56);
            Arc(c, R * 1.18f, 3f, red, 64);
            Arc(c, R * 1.05f, 1.5f, red.WithAlpha(0.7f), 64);
            for (int k = 0; k < 28; k++)  // rune marks between the outer rings, turning
            {
                float a = k * MathX.Tau / 28f + t * 0.25f;
                V2 d = V2.FromAngle(a), s = d.Orthogonal();
                V2 m = c + d * R * 1.115f;
                int kind = (int)(GateHash(k, 7, 2) * 3f);
                if (kind == 0)
                    Line(m - d * 4f, m + d * 4f, 1.8f, red);
                else if (kind == 1)
                {
                    Line(m - d * 4f + s * 3f, m + d * 4f, 1.8f, red);
                    Line(m - d * 4f - s * 3f, m + d * 4f, 1.8f, red);
                }
                else
                    Arc(m, 3f, 1.6f, red, 8);
            }
            for (int k = 0; k < 40; k++)  // the inner dashed ring, turning the other way
                Arc(c, R * 0.86f, 2f, red.WithAlpha(0.8f), 3, k * MathX.Tau / 40f - t * 0.4f, MathX.Tau / 40f * 0.5f);
            if (fill > 0f)  // the team's fill: the inner ring burns round from the top
                Arc(c, R * 0.95f, 5f, gateHot.WithAlpha(0.9f), 48, -MathX.Pi / 2f, MathX.Tau * fill);
            float sp = -t * 0.15f;  // a seven-point star, drawn as one stroke
            for (int k = 0; k < 7; k++)
            {
                V2 a = c + V2.FromAngle(sp + k * MathX.Tau / 7f - MathX.Pi / 2f) * R * 0.84f;
                V2 b = c + V2.FromAngle(sp + (k + 3) * MathX.Tau / 7f - MathX.Pi / 2f) * R * 0.84f;
                Line(a, b, 2.2f, red.WithAlpha(0.65f + 0.35f * pulse));
            }
            Circle(c, R * 0.36f, BgDark.WithAlpha(0.9f), 32);
            Arc(c, R * 0.36f, 2.5f, gateHot.WithAlpha(0.7f + 0.3f * pulse), 32);
            DrawGateSkull(c, R * 0.24f, gateHot.WithAlpha(0.85f + 0.15f * pulse));
        }

        /// A small skull: cranium, eye holes and teeth.
        void DrawGateSkull(V2 c, float s, Rgba col)
        {
            Circle(c + new V2(0f, -s * 0.15f), s, col, 20);
            FillRect(c.X - s * 0.55f, c.Y + s * 0.5f, s * 1.1f, s * 0.5f, col);
            Circle(c + new V2(-s * 0.4f, -s * 0.1f), s * 0.28f, BgDark, 12);
            Circle(c + new V2(s * 0.4f, -s * 0.1f), s * 0.28f, BgDark, 12);
            for (int k = -1; k <= 1; k++)
                FillRect(c.X + k * s * 0.3f - s * 0.04f, c.Y + s * 0.55f, s * 0.08f, s * 0.42f, BgDark);
        }

        /// A socket per standing player round the gate's bottom, lit as they step in; a thread from each player inside.
        void DrawGateSockets(V2 c, int alive, int inside, float e, float t)
        {
            float ring = Game.GateR * 1.6f;
            for (int k = 0; k < alive; k++)
            {
                float a = MathX.Pi / 2f + (k - (alive - 1) / 2f) * 0.45f;
                V2 at = c + V2.FromAngle(a) * ring;
                bool lit = k < inside;
                GlowDisc(at, 30f, lit ? gateHot : gateRed, lit ? 0.5f : 0.15f);
                Polygon(at, 13f * e, 4, 0.785f, lit ? gateHot : BgDark.WithAlpha(0.9f));
                Arc(at, 13f * e, 2.5f, lit ? Rgba.White : gateRed.WithAlpha(0.8f), 4, 0.785f, MathX.Tau);
            }
            foreach (var p in game.Players.Values)
                if (!p.Downed && p.Disp.DistanceTo(c) < Game.GateR)
                    Line(c, p.Disp, 2f, gateHot.WithAlpha(0.45f + 0.2f * Mathf.Sin(t * 6f)));
        }

        /// The HUD (Hud.cs): for GateToastTime after the gate opens, a toast at the top saying so, unless the wave box is
        /// open on screen (its Gate tab already says it). Fades out over its last second.
        void DrawGateToast(Player me)
        {
            if (!game.GateUp || gateSeenAt < 0f || WaveBoxShown(me))
                return;
            float age = gateAgeOverride >= 0f ? gateAgeOverride : Time.time - gateSeenAt;
            if (age > GateToastTime)
                return;
            float a = Mathf.Clamp01((GateToastTime - age) / 1f) * Mathf.Clamp01(age / 0.25f);
            var ink = BossGateInk();
            var col = new Color(ink.R, ink.G, ink.B, a);
            float y = ExpandChipUp ? 160f : DoorChipUp ? 144f : 110f;  // (under the minimized strip)
            var r = new Rect(W / 2f - 260f, y, 520f, 54f);
            var was = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, a);
            DkHud(r, col);
            GUI.color = was;
            string boss = Defs.Get(game.BossAt(game.Hunt != null ? game.Wave + 1 : game.Wave)).Name.ToUpperInvariant();
            HudCrt(new Rect(r.x, r.y + 5f, r.width, 24f), "THE BOSS GATE IS OPEN", 22, col, TextAnchor.MiddleCenter);
            int alive, inside;
            game.GateCount(out alive, out inside);
            HudCrt(new Rect(r.x, r.y + 29f, r.width, 18f), "STAND IN IT TOGETHER TO FACE " + boss + "  ·  " + inside + " / " + alive + " IN", 14,
                new Color(1f, 1f, 1f, 0.8f * a), TextAnchor.MiddleCenter);
        }

        /// The minimap (RoomsLook.cs): a small ring in the gate's colour where it stands, pulsing.
        void DrawGateOnMap(System.Func<V2, V2> M)
        {
            if (!game.GateUp)
                return;
            V2 c = M(game.GatePos);
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 4f);
            var ink = BossGateInk();
            Arc(c, 6f, 2f, ink.WithAlpha(0.6f + 0.4f * pulse), 16);
            MapRect(c.X - 2f, c.Y - 2f, 4f, 4f, ink);
        }

        /// A zoomed view (OffscreenMarkers.cs): an arrow at the edge pointing at the gate when it's off screen.
        void DrawGateMarker(V2 mid, float fade)
        {
            if (!game.GateUp)
                return;
            V2 at = ToUi(game.GatePos);
            if (OffBy(at, Game.GateR * UiWorldScale) <= 0f)
                return;
            V2 dir = (at - mid).Normalized(), tip = EdgePoint(mid, at);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            var ink = BossGateInk();
            MarkArrow(tip, dir, 19f, ink.WithAlpha(fade * pulse));
            Arc(tip - dir * 8f, 25f, 3f, ink.WithAlpha(fade * 0.6f * pulse), 24);
        }
    }
}

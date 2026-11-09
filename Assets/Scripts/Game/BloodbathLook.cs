using UnityEngine;

namespace Arena
{
    /// Bloodbath (the Tank Hemomancer's second ultimate, Core/Bloodbath.cs), from Docs/Mockups/bloodbath.html
    /// and the first Tank Hemomancer card of Docs/Mockups/ultimate-ideas.html. Everything is drawn from the replicated zone
    /// (`ZoneKind.Bloodbath`: Pos, Radius, T, Duration) and the enemies' positions, so it costs nothing extra on the network:
    ///   the pool: a dark red flood with a wavy, uneven edge that spreads out over Game.BathGrow s (Game.BathReach), glossy
    ///     towards the middle, with a bright wet rim and a slow wobble;
    ///   ripples: rings rolling out from where it was cast;
    ///   enemies wading in it: a red tint over them, a ring of blood at their feet and drops falling off them;
    ///   allies standing in it: a thin red guard arc (they take 20% less);
    ///   heal pops: a small "+n" rising off the Hemomancer for each tick (`FxKind.BathHeal`, on the HUD);
    ///   the channel: while it drains you, drops of your blood run off you and down into the pool's middle;
    ///   drying up: once the channel ends (`Zone.Aim` 1) it darkens to brown over Game.BathDry s, pulls back unevenly and cracks.
    /// Visual effects off: a plain disc at its reach, a rim, and the zone's time ring. Reduced effects: no wobble, fewer ripples and drops.
    public partial class ArenaApp
    {
        static readonly Rgba BathDeep = new Rgba(0.30f, 0.01f, 0.05f), BathFlood = new Rgba(0.47f, 0.03f, 0.08f), BathWet = new Rgba(0.84f, 0.18f, 0.25f);
        static readonly Rgba BathDried = new Rgba(0.24f, 0.08f, 0.05f), BathDrop = new Rgba(0.86f, 0.12f, 0.2f), BathHealPink = new Rgba(1f, 0.6f, 0.66f);
        const int BathEdge = 64;  // points round the edge

        static Rgba BathMix(Rgba a, Rgba b, float f) => new Rgba(a.R + (b.R - a.R) * f, a.G + (b.G - a.G) * f, a.B + (b.B - a.B) * f, a.A + (b.A - a.A) * f);

        /// How dry the pool is, 0 (wet) to 1 (gone), over its last BathDry seconds.
        static float BathDryness(Zone z) => z.Aim > 0f ? Mathf.Clamp01(1f - z.T / Game.BathDry) : 0f;

        /// The edge's distance from the middle at angle a: the reach, made wavy and uneven, and pulled back as it dries.
        float BathEdgeAt(Zone z, float reach, float a, float t, float dry)
        {
            float s = reach / Game.BathRadius;  // the waves scale with the pool
            float wob = fxReduced ? 0f : t * 0.6f;
            float r = reach + (Mathf.Sin(a * 6f + 1f + z.Id + wob) * 7f + Mathf.Sin(a * 11f - wob * 0.7f) * 4f + Mathf.Sin(a * 3f + z.Id * 2.3f) * 5f) * s;
            if (dry > 0f)
                r *= 1f - dry * (0.35f + 0.3f * (0.5f + 0.5f * Mathf.Sin(a * 3f + z.Id * 1.7f)));
            return Mathf.Max(0f, r);
        }

        /// The pool, on the floor under everyone (DrawZone).
        void DrawBloodbath(Zone z, float t)
        {
            float reach = Game.BathReach(z), age = z.Duration - z.T, dry = BathDryness(z);
            if (!fxOn)
            {
                Circle(z.Pos, reach, BathFlood.WithAlpha(0.35f), 40);
                Arc(z.Pos, reach, 2f, BathWet.WithAlpha(0.6f), 48);
                Arc(z.Pos, reach + 4f, 2.5f, BathFlood.WithAlpha(0.8f), 48, -MathX.Pi / 2f, MathX.Tau * (z.Aim > 0f ? 0f : MathX.Clamp01(1f - age / Game.BathTime)));
                return;
            }
            float fade = 1f - dry * dry;
            Rgba deep = BathMix(BathDeep, BathDried, dry).WithAlpha(0.72f * fade), flood = BathMix(BathFlood, BathDried, dry).WithAlpha(0.5f * fade);
            // The flood: a fan from the glossy middle out to the wavy edge.
            V2 prev = z.Pos + V2.FromAngle(0f) * BathEdgeAt(z, reach, 0f, t, dry);
            for (int k = 1; k <= BathEdge; k++)
            {
                float a = MathX.Tau * k / BathEdge;
                V2 at = z.Pos + V2.FromAngle(a) * BathEdgeAt(z, reach, a, t, dry);
                VTri(prev, flood, at, flood, z.Pos, deep);
                prev = at;
            }
            // The wet rim, broken up a little where the edge bulges.
            var rim = BathMix(BathWet, BathDried, dry).WithAlpha(0.65f * fade);
            prev = z.Pos + V2.FromAngle(0f) * BathEdgeAt(z, reach, 0f, t, dry);
            for (int k = 1; k <= BathEdge; k++)
            {
                float a = MathX.Tau * k / BathEdge;
                V2 at = z.Pos + V2.FromAngle(a) * BathEdgeAt(z, reach, a, t, dry);
                Line(prev, at, 2f, rim);
                prev = at;
            }
            // The splash as it floods out, then ripples rolling out from the middle.
            if (age < 0.6f)
                Arc(z.Pos, reach + 10f * age, 4f * (1f - age / 0.6f), BathWet.WithAlpha(0.8f * (1f - age / 0.6f)), 48);
            int rings = fxReduced ? 2 : 4;
            for (int q = 0; q < rings; q++)
            {
                float p = Mathf.Repeat(t * 0.35f + q / (float)rings, 1f);
                Arc(z.Pos, reach * p, 1.5f, new Rgba(0.9f, 0.31f, 0.35f, 0.25f * (1f - p) * fade * (1f - dry)), 40);
            }
            // A glint of wet light, gone once it dries.
            if (dry < 1f)
                Ellipse(z.Pos + new V2(-reach * 0.32f, -reach * 0.34f), reach * 0.16f, reach * 0.06f, -0.6f, new Rgba(1f, 0.55f, 0.58f, 0.16f * (1f - dry)), 16);
            // Drying: dark cracks spreading through the crust.
            if (dry > 0.25f)
            {
                var crack = new Rgba(0.13f, 0.03f, 0.02f, 0.7f * Mathf.Clamp01((dry - 0.25f) / 0.3f) * fade);
                for (int k = 0; k < 7; k++)
                {
                    V2 d0 = V2.FromAngle(k * 0.9f + z.Id);
                    float len = reach * (0.35f + 0.35f * Hash01(z.Id + k)) * Mathf.Clamp01((dry - 0.25f) / 0.5f);
                    V2 mid = z.Pos + d0.Rotated(0.25f) * (len * 0.55f);
                    Line(z.Pos + d0 * (reach * 0.12f), mid, 1.5f, crack);
                    Line(mid, z.Pos + d0.Rotated(-0.15f) * len, 1.2f, crack);
                }
            }
            // The channel: your blood running off you into the pool's middle while it drains you.
            Player owner;
            if (dry <= 0f && game.Players.TryGetValue(z.Owner, out owner) && !owner.Downed)
            {
                V2 from = owner.Disp, to = z.Pos;
                float len = from.DistanceTo(to);
                if (len < 30f)
                    to = from + new V2(0f, 26f);  // standing in the middle: the drops just fall at your feet
                int n = fxReduced ? 2 : 4;
                for (int k = 0; k < n; k++)
                {
                    float ph = Mathf.Repeat(t * 0.9f + k / (float)n, 1f);
                    V2 at = from + (to - from) * ph + new V2(0f, -10f * Mathf.Sin(ph * MathX.Pi));
                    Circle(at, 2.4f * (1f - 0.5f * ph), BathDrop.WithAlpha(0.85f * (1f - ph * 0.6f)), 8);
                }
                Arc(owner.Disp, owner.Def.BodyR + 3f, 1.5f, BathWet.WithAlpha(0.5f + 0.3f * Mathf.Sin(t * 6f)), 20);  // a pulse round you: you're paying for it
            }
            // Allies standing in it: a thin guard arc under them (they take Game.BathGuard less).
            foreach (var p in game.Players.Values)
                if (!p.Downed && Game.InBath(z, p.Pos, 0f))
                    Arc(p.Disp, p.Def.BodyR + 7f, 2f, BathWet.WithAlpha(0.55f * fade), 24, t * 1.5f, MathX.Pi * 1.3f);
        }

        /// Over the enemies (WorldRenderer): a red tint over each one wading in a Bloodbath, a ring at its feet and drops falling off it.
        void DrawBloodbathOver(float t)
        {
            if (!fxOn)
                return;
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Bloodbath)
                    continue;
                float fade = 1f - BathDryness(z);
                if (fade <= 0f)
                    continue;
                foreach (var e in game.Enemies.Values)
                {
                    if (game.IsSpawning(e) || e.Hp <= 0f || !Game.InBath(z, e.Pos, e.Radius))
                        continue;
                    float r = e.Radius;
                    Circle(e.Disp, r, new Rgba(0.75f, 0.05f, 0.12f, 0.32f * fade), 20);  // the tint
                    Arc(e.Disp + new V2(0f, r * 0.35f), r + 4f, 1.5f, BathWet.WithAlpha(0.45f * fade), 20, 0.15f, MathX.Pi - 0.3f);  // wading: the blood at its feet
                    int drops = fxReduced ? 1 : 3;
                    for (int k = 0; k < drops; k++)
                    {
                        float ph = Mathf.Repeat(t * 1.4f + e.Id * 0.37f + k / (float)drops, 1f);
                        float x = (Hash01(e.Id * 3.1f + k) - 0.5f) * r * 1.2f;
                        Ellipse(e.Disp + new V2(x, r * 0.6f + ph * 10f), 1.8f * (1f - ph * 0.6f), 2.8f * (1f - ph * 0.6f), 0f, BathDrop.WithAlpha(0.9f * (1f - ph) * fade), 8);
                    }
                }
            }
        }

        /// On the HUD: the small "+n" off the Hemomancer for each tick that healed them.
        void DrawBathPops()
        {
            if (game == null)
                return;
            foreach (var fx in game.Effects)
            {
                if (fx.Kind != FxKind.BathHeal || fx.T > Game.BathHealFxTime)
                    continue;
                Player p;
                V2 at = game.Players.TryGetValue((int)fx.Pos2.X, out p) ? p.Disp : fx.Pos;
                float side = (Hash01(fx.Pos.X * 0.37f + fx.Pos.Y) - 0.5f) * 20f;  // a little apart, so ticks in a row don't sit on top of each other
                float body = p != null ? p.Def.BodyR : 14f;  // to your right, clear of your name and health bar
                RisingPop(at + new V2(body + 26f + side * 0.4f, -4f + side * 0.3f), fx.T, "+" + Mathf.Max(1, Mathf.RoundToInt(fx.Radius)), BathHealPink, 12);
            }
        }
    }
}

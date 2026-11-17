using UnityEngine;

namespace Arena
{
    /// Flood (the Tank Elementalist's 4th Space ability, Core/Flood.cs), look B "Breaker" of Docs/Mockups/flood.html. Everything is drawn
    /// from the replicated zone (`ZoneKind.Flood`: Pos, Radius, T, Duration) and the enemies' positions, so it costs nothing on the network:
    ///   the surge: as the water spreads (Game.FloodGrow s) a white foam crest crashes outward with a cyan band behind it and spray;
    ///   the pool: translucent water, deeper in the middle, with an uneven, churning edge;
    ///   swells: soft bright rings rolling out from where it was cast;
    ///   the foam rim: broken white blobs riding the edge;
    ///   waders: a foam ring at each wading enemy's feet and wake ripples spreading behind it (the wet drips are Tsunami's, TsunamiLook.cs);
    ///   draining: the water pulls back in (Game.FloodReach) and fades over Game.FloodDrain s.
    /// Visual effects off: a plain disc at its reach, a rim, and a time ring. Reduced effects: no churn, fewer swells and foam.
    public partial class ArenaApp
    {
        static readonly Rgba FloodDeep = new Rgba(0.05f, 0.30f, 0.40f), FloodMid = new Rgba(0.16f, 0.56f, 0.68f), FloodEdge = new Rgba(0.31f, 0.82f, 0.91f);
        static readonly Rgba FloodFoam = new Rgba(0.92f, 0.99f, 1f), FloodCrest = new Rgba(0.31f, 0.82f, 0.91f);
        const int FloodSegs = 72;

        // Sounds: when each Flood's next lap is due (by zone id); the stamp plays on first sight (if it's young), the gurgle once as it starts draining.
        readonly System.Collections.Generic.Dictionary<int, float> floodNext = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.HashSet<int> floodDrained = new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.List<int> floodGone = new System.Collections.Generic.List<int>();
        const float FloodLapEvery = 1.15f;

        void UpdateFloodSound()
        {
            float now = Time.unscaledTime;
            floodGone.Clear();
            foreach (var kv in floodNext)
                if (!game.Zones.ContainsKey(kv.Key))
                    floodGone.Add(kv.Key);
            foreach (int id in floodGone)
            {
                floodNext.Remove(id);
                floodDrained.Remove(id);
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Flood)
                    continue;
                bool mine = z.Owner == game.MyId;
                float quiet = mine ? 0f : game.QuietAt(z.Pos);
                float next;
                if (!floodNext.TryGetValue(z.Id, out next))
                {
                    if (Game.FloodAge(z) < 0.5f)  // (a pool already there when you arrive just laps)
                        sounds?.Play(new SoundCue { Sfx = Sfx.FloodCast, Ally = !mine, Quiet = quiet }, now);
                    floodNext[z.Id] = now + FloodLapEvery;
                    continue;
                }
                if (!Game.FloodOn(z))
                {
                    if (floodDrained.Add(z.Id))
                        sounds?.Play(new SoundCue { Sfx = Sfx.FloodDrain, Ally = !mine, Quiet = quiet }, now);
                }
                else if (now >= next)
                {
                    sounds?.Play(new SoundCue { Sfx = Sfx.FloodLap, Ally = !mine, Quiet = quiet }, now);
                    floodNext[z.Id] = now + FloodLapEvery;
                }
            }
        }

        /// How much of the pool is left as it drains: 1 while it floods, down to 0.
        static float FloodFade(Zone z)
        {
            float over = Game.FloodAge(z) - Game.FloodLife(z);
            return over <= 0f ? 1f : Mathf.Clamp01(1f - over / Game.FloodDrain);
        }

        /// The edge's distance from the middle at angle a: the reach, churned by a few travelling waves.
        float FloodEdgeAt(Zone z, float reach, float a, float t)
        {
            if (reach <= 0f)
                return 0f;
            float s = Mathf.Min(1f, reach / Game.FloodRadius), ph = fxReduced ? 0f : t * 2.4f;
            return Mathf.Max(0f, reach + (Mathf.Sin(a * 7f + ph + z.Id) * 3f + Mathf.Sin(a * 13f - ph * 1.7f) * 2f) * s);
        }

        /// The pool, on the floor under everyone (DrawZone).
        void DrawFlood(Zone z, float t)
        {
            float reach = Game.FloodReach(z), age = Game.FloodAge(z), fade = FloodFade(z);
            if (!fxOn)
            {
                Circle(z.Pos, reach, FloodMid.WithAlpha(0.3f), 40);
                Arc(z.Pos, reach, 2f, FloodEdge.WithAlpha(0.7f), 48);
                float life = Game.FloodLife(z);
                Arc(z.Pos, reach + 4f, 2.5f, FloodEdge.WithAlpha(0.8f), 48, -MathX.Pi / 2f, MathX.Tau * MathX.Clamp01(1f - age / Mathf.Max(0.01f, life)));
                return;
            }
            if (reach > 1f)
            {
                // The water: a fan from the deep middle out to the churning edge.
                Rgba deep = FloodDeep.WithAlpha(0.42f * fade), rim = FloodEdge.WithAlpha(0.34f * fade);
                V2 prev = z.Pos + V2.FromAngle(0f) * FloodEdgeAt(z, reach, 0f, t);
                for (int k = 1; k <= FloodSegs; k++)
                {
                    float a = MathX.Tau * k / FloodSegs;
                    V2 at = z.Pos + V2.FromAngle(a) * FloodEdgeAt(z, reach, a, t);
                    VTri(prev, rim, at, rim, z.Pos, deep);
                    prev = at;
                }
                // Swells rolling out from the middle.
                int swells = fxReduced ? 2 : 3;
                for (int q = 0; q < swells; q++)
                {
                    float p = Mathf.Repeat(t * 0.24f + q / (float)swells, 1f);
                    Arc(z.Pos, reach * p, 5f, FloodFoam.WithAlpha(0.13f * (1f - p) * fade), 48);
                }
                // The edge line, then broken foam riding it.
                var line = FloodFoam.WithAlpha(0.55f * fade);
                prev = z.Pos + V2.FromAngle(0f) * FloodEdgeAt(z, reach, 0f, t);
                for (int k = 1; k <= FloodSegs; k++)
                {
                    float a = MathX.Tau * k / FloodSegs;
                    V2 at = z.Pos + V2.FromAngle(a) * FloodEdgeAt(z, reach, a, t);
                    Line(prev, at, 1.5f, line);
                    prev = at;
                }
                int blobs = fxReduced ? 36 : 70;
                for (int i = 0; i < blobs; i++)
                {
                    float a = MathX.Tau * i / blobs + Mathf.Sin(i * 3.1f) * 0.03f;
                    float r = FloodEdgeAt(z, reach, a, t) + (fxReduced ? 0f : Mathf.Sin(a * 9f + t * 3f + i) * 2.5f) - 2f;
                    float size = (1.6f + (i * 37 % 5) * 0.6f + (fxReduced ? 0f : Mathf.Sin(t * 4f + i) * 0.7f)) * Mathf.Min(1f, reach / 60f);
                    float alpha = (0.5f + 0.3f * Mathf.Sin(i + t * 5f)) * fade;
                    if (size > 0.4f)
                        Circle(z.Pos + V2.FromAngle(a) * r, size, FloodFoam.WithAlpha(alpha), 8);
                }
            }
            // The surge: a foam crest crashing outward while the water spreads, then gone.
            float crestT = Game.FloodGrow + 0.25f;
            if (age < crestT)
            {
                float k = Mathf.Clamp01(age / Game.FloodGrow), cr = z.Radius * (1f - (1f - k) * (1f - k) * (1f - k)) + 6f;
                float al = 1f - Mathf.Clamp01((age - Game.FloodGrow) / 0.25f);
                Arc(z.Pos, Mathf.Max(1f, cr - 10f), 10f, FloodCrest.WithAlpha(0.55f * al), 56);
                Arc(z.Pos, cr, 6f, FloodFoam.WithAlpha(0.85f * al), 56);
                int spray = fxReduced ? 16 : 36;
                for (int i = 0; i < spray; i++)
                {
                    float a = i * 2.39996f + z.Id;
                    Circle(z.Pos + V2.FromAngle(a) * (cr + 4f + (i * 13 % 9)), 1.5f, FloodFoam.WithAlpha(al), 6);
                }
            }
            // Waders: a foam ring at their feet and wake ripples spreading behind them.
            if (fade < 1f)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e) || e.Hp <= 0f || !Game.InFlood(z, e.Pos, e.Radius))
                    continue;
                float r = e.Radius;
                Ellipse(e.Disp + new V2(0f, r * 0.55f), r * 1.15f, r * 0.42f, 0f, FloodFoam.WithAlpha(0.18f), 16);
                Arc(e.Disp + new V2(0f, r * 0.2f), r + 3f, 1.5f, FloodFoam.WithAlpha(0.5f), 20, 0.2f, MathX.Pi - 0.4f);
                int rings = fxReduced ? 1 : 2;
                for (int q = 0; q < rings; q++)
                {
                    float p = Mathf.Repeat(t * 0.9f + e.Id * 0.29f + q / (float)rings, 1f);
                    float rr = r * 0.6f + p * 26f;
                    EllipseLine(e.Disp + new V2(0f, r * 0.55f), rr, rr * 0.45f, 1.2f, FloodFoam.WithAlpha(0.35f * (1f - p)), 18);
                }
            }
        }
    }
}

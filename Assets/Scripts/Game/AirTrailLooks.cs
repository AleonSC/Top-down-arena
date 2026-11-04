using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The air trail with Visual effects on (look A, Ribbons, of Docs/Mockups/air-trail.html): anything that moves fast enough
    /// (players, enemies, minions) leaves two thin streamlines off its shoulders that wobble and fade. Slow movers get nothing;
    /// the strength is ((speed - AirStart) / (AirFull - AirStart)) squared, so mid speeds stay light and only the fast ones get
    /// the full trail. Speed is how far the drawn body moved this frame (smoothed), so dashes and knockback count too. Each client
    /// works it out itself: nothing is sent. Drawn in the normal pass under the bodies (DrawAirTrails, from WorldRenderer).
    public partial class ArenaApp
    {
        const float AirStart = 110f, AirFull = 330f;  // px/s: no trail at or below the first, the full trail at the second
        const float AirKeep = 0.55f;                  // seconds of path kept; a full-strength trail shows 0.55 s of it
        const int AirPlayerKey = 0, AirEnemyKey = 1 << 20, AirMinionKey = 1 << 21;

        struct AirPt { public V2 Pos; public float T; }

        class AirTrail
        {
            public V2 Last; public bool Has, Seen; public float Speed, R;
            public Rgba Col;
            public readonly List<AirPt> Pts = new List<AirPt>();
        }

        readonly Dictionary<int, AirTrail> airTrails = new Dictionary<int, AirTrail>();
        readonly List<int> airGone = new List<int>();

        /// 0 at or below AirStart, 1 at or above AirFull, squared in between.
        static float AirStrength(float speed)
        {
            float t = Mathf.Clamp01((speed - AirStart) / (AirFull - AirStart));
            return t * t;
        }

        void ClearAirTrails() { airTrails.Clear(); }

        /// Each frame (UpdateJuice): follow every body's drawn position and keep its recent path.
        void UpdateAirTrails(float dt)
        {
            if (dt <= 0f)
                return;
            foreach (var tr in airTrails.Values)
                tr.Seen = false;
            if (Juicy)
            {
                foreach (var p in game.Players.Values)
                    if (!p.Downed)
                        FeedAir(AirPlayerKey + p.Id, p.Disp, p.Def.BodyR, p.Def.Color, dt);
                foreach (var e in game.Enemies.Values)
                    if (e.Hp > 0f && !game.IsSpawning(e))
                        FeedAir(AirEnemyKey + e.Id, e.Disp, e.Radius, e.Def.Color, dt);
                foreach (var m in game.Minions.Values)
                    FeedAir(AirMinionKey + m.Id, m.Disp, m.Def.Radius * m.Scale, m.Def.Color, dt);
            }
            airGone.Clear();
            foreach (var kv in airTrails)
            {
                var tr = kv.Value;
                if (!tr.Seen)
                    tr.Has = false;  // gone or hidden: what's left fades out, then the entry goes
                while (tr.Pts.Count > 0 && juiceClock - tr.Pts[0].T > AirKeep)
                    tr.Pts.RemoveAt(0);
                if (!tr.Seen && tr.Pts.Count == 0)
                    airGone.Add(kv.Key);
            }
            foreach (var k in airGone)
                airTrails.Remove(k);
        }

        void FeedAir(int key, V2 at, float r, Rgba col, float dt)
        {
            AirTrail tr;
            if (!airTrails.TryGetValue(key, out tr))
                airTrails[key] = tr = new AirTrail();
            tr.Seen = true;
            tr.R = r;
            tr.Col = col;
            float dist = tr.Has ? at.DistanceTo(tr.Last) : 0f;
            if (dist > 220f)  // a jump (a respawn, wrapping round the arena): no streak across it
            {
                tr.Pts.Clear();
                tr.Speed = 0f;
                dist = 0f;
            }
            tr.Speed += (dist / dt - tr.Speed) * (1f - Mathf.Exp(-dt * 12f));
            if (AirStrength(tr.Speed) > 0.01f || tr.Pts.Count > 0)
                tr.Pts.Add(new AirPt { Pos = at, T = juiceClock });
            tr.Last = at;
            tr.Has = true;
        }

        /// The normal pass, before the minions, enemies and players: each moving body's two ribbons.
        void DrawAirTrails()
        {
            if (!Juicy)
                return;
            foreach (var tr in airTrails.Values)
            {
                var pts = tr.Pts;
                if (pts.Count < 2)
                    continue;
                float k = AirStrength(tr.Speed);
                if (k <= 0.01f && juiceClock - pts[pts.Count - 1].T > 0.2f)
                    continue;
                float life = 0.15f + 0.4f * k;
                Rgba tint = tr.Col.Mix(Rgba.White, 0.65f);
                for (int side = -1; side <= 1; side += 2)
                {
                    bool have = false;
                    V2 prev = default(V2);
                    float prevW = 0f, prevA = 0f;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        float age = juiceClock - pts[i].T;
                        if (age > life)
                            continue;
                        V2 a = pts[Mathf.Max(0, i - 1)].Pos, b = pts[Mathf.Min(pts.Count - 1, i + 1)].Pos, d = b - a;
                        if (d.Length < 0.01f)
                            continue;
                        V2 n = new V2(-d.Y, d.X) / d.Length;
                        float f = 1f - age / life;
                        float off = side * tr.R * 0.7f + Mathf.Sin(age * 14f + (side > 0 ? 0f : 2f)) * tr.R * 0.3f * k * (age / life);
                        V2 at = pts[i].Pos + n * off;
                        float w = 0.6f + tr.R * 0.16f * f * k, al = 0.65f * k * f;
                        if (have && al > 0.01f)
                            Line(prev, at, (w + prevW) * 0.5f, tint.WithAlpha((al + prevA) * 0.5f));
                        prev = at; prevW = w; prevA = al; have = true;
                    }
                }
            }
        }
    }
}

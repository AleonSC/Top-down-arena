using UnityEngine;

namespace Arena
{
    /// Bullet Rain (the Elementalist DPS ultimate): "Rain Barrage", look B of Docs/Mockups/bullet-rain.html (the Soldier's
    /// mockup), recoloured from its reds to rain blues. A square target zone (520 px across, `Zone.Radius` = its half-side) locks on (shrinking in from 1.6x over 0.7 s) with a
    /// dashed tether from the caster, a dashed rim, corner ticks and a crosshair; rounds fall into it (220 a second),
    /// each casting a growing dot first, and land with a ring, two sparks and a wet mark. The caster glows and launches
    /// rounds upward. Everything is drawn from the zone's clock alone, so it costs nothing on the network. The ground
    /// half draws with the zone (`DrawBulletRain`), the rounds in the air over the enemies (`DrawBulletRainAir`).
    /// Reduced effects: no vignette or flash, fewer rounds.
    public partial class ArenaApp
    {
        // Look B's colours moved to blue: accent (255,110,80), rim (255,130,90), fill (255,90,60), ticks (255,170,130),
        // crosshair (255,200,170), dots (255,150,100), trail glow and marks (255,120,80), core (255,225,185),
        // landing (255,140,90), launch (255,180,110).
        static readonly Rgba RainAcc = new Rgba(0.47f, 0.69f, 0.94f), RainRim = new Rgba(0.51f, 0.73f, 0.98f),
            RainFill = new Rgba(0.36f, 0.6f, 0.92f), RainTick = new Rgba(0.67f, 0.84f, 1f), RainCross = new Rgba(0.8f, 0.9f, 1f),
            RainDot = new Rgba(0.59f, 0.78f, 1f), RainTrail = new Rgba(0.47f, 0.69f, 0.98f), RainCore = new Rgba(0.88f, 0.95f, 1f),
            RainLand = new Rgba(0.55f, 0.75f, 0.98f), RainLaunch = new Rgba(0.7f, 0.86f, 1f);
        const float RainLock = 0.7f, RainRate = 220f, RainLife = 1.4f + 0.4f;  // lock-on time, rounds a second, a round's fall + mark

        float RainAge(Zone z) { return z.Duration - z.T; }
        float RainZa(Zone z)  // the zone's fade: in over the lock-on, out over the last 0.8 s
        {
            float age = RainAge(z);
            return Mathf.Clamp01(age / RainLock) * Mathf.Clamp01(z.T / 0.8f);
        }
        float RainGlow(Zone z) { return Mathf.Clamp01(RainAge(z) / 0.3f) * Mathf.Clamp01(z.T / 0.6f) * 0.8f; }

        /// One falling round: where it lands, its start height and speed, and the time since it was dropped. False when
        /// this slot has no round in this cycle (before the cast, or too late to land before the zone ends).
        bool RainRound(Zone z, int k, int n, out V2 land, out float z0, out float vz, out float tau, out float seed)
        {
            float period = n / RainRate, age = RainAge(z);
            float u = (age + Hash01(k + 0.37f) * period) / period, cyc = (float)System.Math.Floor(u);
            tau = (u - cyc) * period;
            seed = k * 13.7f + cyc * 7.1f + 300f;
            land = z.Pos + new V2(Hash01(seed) * 2f - 1f, Hash01(seed + 1.3f) * 2f - 1f) * z.Radius;  // anywhere in the square
            z0 = 300f + 220f * Hash01(seed + 2.1f);
            vz = 1300f + 400f * Hash01(seed + 3.7f);
            float spawn = age - tau;
            return spawn >= 0f && spawn <= z.Duration - z0 / vz;
        }

        // Sounds: when each Bullet Rain zone's next patter is due (by zone id); the cast plays on first sight, the end near its last 0.5 s.
        readonly System.Collections.Generic.Dictionary<int, float> rainNext = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.List<int> rainGone = new System.Collections.Generic.List<int>();
        readonly System.Collections.Generic.HashSet<int> rainEnded = new System.Collections.Generic.HashSet<int>();

        void UpdateBulletRainSound(float t)
        {
            float now = Time.unscaledTime;
            rainGone.Clear();
            foreach (var kv in rainNext)
                if (!game.Zones.ContainsKey(kv.Key))
                    rainGone.Add(kv.Key);
            foreach (int id in rainGone)
            {
                rainNext.Remove(id);
                rainEnded.Remove(id);
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.BulletRain)
                    continue;
                bool mine = z.Owner == game.MyId;
                float quiet = mine ? 0f : game.QuietAt(z.Pos);
                float next;
                if (!rainNext.TryGetValue(z.Id, out next))
                {
                    sounds?.PlayRainCue(SoundBank.RainCue.RainCast, !mine, quiet, now);
                    rainNext[z.Id] = now + 0.5f;
                    continue;
                }
                if (z.T < 0.5f)
                {
                    if (rainEnded.Add(z.Id))
                        sounds?.PlayRainCue(SoundBank.RainCue.RainEnd, !mine, quiet, now);
                }
                else if (now >= next)
                {
                    sounds?.PlayRainCue(SoundBank.RainCue.RainPatter, !mine, quiet, now);
                    rainNext[z.Id] = now + 0.45f;
                }
            }
        }

        int RainRounds { get { return fxReduced ? 120 : (int)(RainRate * RainLife); } }

        /// Under the enemies: the caster's glow and launch, the tether, the zone, wet marks and each round's dot.
        void DrawBulletRain(Zone z, float t)
        {
            float age = RainAge(z), za = RainZa(z);
            if (za <= 0f)
                return;
            float R = z.Radius * (age < RainLock ? 1.6f - 0.6f * age / RainLock : 1f);
            Player owner;
            bool hasOwner = game.Players.TryGetValue(z.Owner, out owner) && !owner.Downed;
            // Wet marks where rounds landed (they last 1.4 s).
            int n = RainRounds;
            for (int k = 0; k < n; k++)
            {
                V2 land; float z0, vz, tau, seed;
                if (!RainRound(z, k, n, out land, out z0, out vz, out tau, out seed))
                    continue;
                float a = tau - z0 / vz;
                if (a >= 0f && a < 1.4f)
                    Circle(land, 2.2f, RainTrail.WithAlpha((1.4f - a) / 1.4f * 0.45f * za), 6);
            }
            if (hasOwner)
            {
                // The dashed tether from the caster to the zone's middle.
                V2 from = owner.Disp, to = z.Pos;
                float len = from.DistanceTo(to);
                V2 dir = (to - from) / Mathf.Max(1f, len);
                for (float d = 0f; d < len; d += 14f)
                    Line(from + dir * d, from + dir * Mathf.Min(len, d + 5f), 1.5f, RainRim.WithAlpha(0.22f * za));
            }
            // The zone: a square darkening to its edge, a turning dashed rim, a thin outer square, corner ticks and a crosshair.
            V2 c0 = z.Pos + new V2(-R, -R), c1 = z.Pos + new V2(R, -R), c2 = z.Pos + new V2(R, R), c3 = z.Pos + new V2(-R, R);
            Rgba fi = RainFill.WithAlpha(0.03f * za), fo = RainFill.WithAlpha(0.2f * za);
            VTri(z.Pos, fi, c0, fo, c1, fo);
            VTri(z.Pos, fi, c1, fo, c2, fo);
            VTri(z.Pos, fi, c2, fo, c3, fo);
            VTri(z.Pos, fi, c3, fo, c0, fo);
            V2[] cs = { c0, c1, c2, c3 };
            float off = t * 50f;
            for (int side = 0; side < 4; side++)
            {
                V2 a0 = cs[side], a1 = cs[(side + 1) % 4], dirS = (a1 - a0) / (2f * R);
                for (float d = -(off % 26f); d < 2f * R; d += 26f)
                {
                    float d0 = Mathf.Max(0f, d), d1 = Mathf.Min(2f * R, d + 16f);
                    if (d1 > d0)
                        Line(a0 + dirS * d0, a0 + dirS * d1, 2.5f, RainRim.WithAlpha(0.85f * za));
                }
                Line(a0 + (a0 - z.Pos) * (12f / R), a1 + (a1 - z.Pos) * (12f / R), 1f, RainRim.WithAlpha(0.25f * za));
                V2 outv = (a0 - z.Pos) / (R * 1.4142f);  // corner tick, pointing out
                Line(a0 + outv * 4f, a0 + outv * 24f, 3f, RainTick.WithAlpha(0.8f * za));
            }
            Rgba cr = RainCross.WithAlpha(0.7f * za);
            Line(z.Pos + new V2(-14f, 0f), z.Pos + new V2(-5f, 0f), 1.5f, cr);
            Line(z.Pos + new V2(5f, 0f), z.Pos + new V2(14f, 0f), 1.5f, cr);
            Line(z.Pos + new V2(0f, -14f), z.Pos + new V2(0f, -5f), 1.5f, cr);
            Line(z.Pos + new V2(0f, 5f), z.Pos + new V2(0f, 14f), 1.5f, cr);
            // Each falling round's dot on the ground, growing as it comes down.
            for (int k = 0; k < n; k++)
            {
                V2 land; float z0, vz, tau, seed;
                if (!RainRound(z, k, n, out land, out z0, out vz, out tau, out seed) || tau >= z0 / vz)
                    continue;
                float kk = 1f - Mathf.Clamp01((z0 - vz * tau) / 520f);
                Circle(land, 1.2f + 2.4f * kk, RainDot.WithAlpha(0.12f + 0.5f * kk), 8);
            }
            if (hasOwner)
                DrawRainCaster(z, owner.Disp, t);
        }

        /// The caster: a glow and a pulsing ring, the cast's two rings, and rounds launched straight up.
        void DrawRainCaster(Zone z, V2 at, float t)
        {
            float age = RainAge(z), glow = RainGlow(z);
            if (glow > 0f)
            {
                FanGradient(at, 64f, RainAcc.WithAlpha(0.38f * glow), RainAcc.WithAlpha(0f), 28);
                Arc(at, 30f + Mathf.Sin(t * 18f) * 2.5f * glow, 2f, RainAcc.WithAlpha(0.55f * glow), 32);
            }
            if (age < 0.6f)  // the cast: an accent ring out to 280 px and a white one to 170 px
            {
                float lf = 1f - age / 0.6f;
                Arc(at, 10f + 270f * (1f - lf), 6f * lf + 0.5f, RainAcc.WithAlpha(lf * 0.9f), 64);
            }
            if (age < 0.45f)
            {
                float lf = 1f - age / 0.45f;
                Arc(at, 6f + 164f * (1f - lf), 3f * lf + 0.5f, Rgba.White.WithAlpha(lf * 0.9f), 48);
            }
            // Rounds launched upward, one every 0.07 s from alternate hands, 0.55 s each.
            float aim = -1.45f;
            V2 fwd = V2.FromAngle(aim), side = new V2(-fwd.Y, fwd.X);
            for (int k = 0; k < 8; k++)
            {
                float u = (age - k * 0.07f) / 0.56f, cyc = (float)System.Math.Floor(u), lt = (u - cyc) * 0.56f;
                float spawn = age - lt;
                if (spawn < 0f || spawn > z.Duration - 0.55f || lt > 0.55f)
                    continue;
                float seed = k * 5.3f + cyc * 11.9f + 900f, lf = 1f - lt / 0.55f;
                V2 o = at + fwd * 40f + side * (Hash01(seed) < 0.5f ? -7f : 7f);
                V2 v = new V2(-30f + 60f * Hash01(seed + 1f), -1100f);
                V2 p = o + v * lt;
                Line(p, p - v * 0.035f, 2.5f, RainLaunch.WithAlpha(lf));
            }
        }

        /// Over the enemies: each round falling (a trail, a bright core and a white head), the landing rings and sparks,
        /// then the rim vignette and the cast's flash over the arena.
        void DrawBulletRainAir(float t)
        {
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.BulletRain)
                    continue;
                float za = RainZa(z), age = RainAge(z);
                if (za <= 0f)
                    continue;
                int n = RainRounds;
                for (int k = 0; k < n; k++)
                {
                    V2 land; float z0, vz, tau, seed;
                    if (!RainRound(z, k, n, out land, out z0, out vz, out tau, out seed))
                        continue;
                    float fall = z0 / vz;
                    if (tau < fall)
                    {
                        V2 head = land - new V2(0f, z0 - vz * tau), tail = head - new V2(0f, 48f);
                        Line(tail, head, 5f, RainTrail.WithAlpha(0.2f));
                        Line(tail, head, 1.8f, RainCore.WithAlpha(0.95f));
                        Circle(head, 1.5f, Rgba.White.WithAlpha(0.95f), 6);
                        continue;
                    }
                    float a = tau - fall;
                    if (a < 0.35f)  // the landing ring
                    {
                        float lf = 1f - a / 0.35f;
                        Arc(land, 2f + 20f * (1f - lf), 2.5f * lf + 0.5f, RainLand.WithAlpha(lf * 0.9f), 16);
                    }
                    if (a < 0.22f)  // two sparks
                    {
                        float lf = 1f - a / 0.22f;
                        for (int s = 0; s < 2; s++)
                        {
                            V2 v = V2.FromAngle(Hash01(seed + 5f + s) * MathX.Tau) * (80f + 120f * Hash01(seed + 7f + s));
                            V2 p = land + v * a;
                            Line(p, p - v * 0.035f, 1.6f, RainLand.WithAlpha(lf));
                        }
                    }
                }
                if (fxReduced)
                    continue;
                // The rim vignette (alpha 0.16 at the edges) and the cast's flash.
                float vig = 0.16f * Mathf.Clamp01(age / 0.3f) * Mathf.Clamp01(z.T / 0.5f);
                V2 c = new V2(Game.Width * 0.5f, Game.Height * 0.5f);
                float r0 = Game.Height * 0.45f, r1 = Mathf.Max(Game.Width * 0.62f, c.Length + 2f);
                if (vig > 0f)
                    for (int i = 0; i < 48; i++)
                    {
                        V2 d0 = V2.FromAngle(MathX.Tau * i / 48f), d1 = V2.FromAngle(MathX.Tau * (i + 1) / 48f);
                        Rgba ci = RainAcc.WithAlpha(0f), co = RainAcc.WithAlpha(vig);
                        VTri(c + d0 * r1, co, c + d1 * r1, co, c + d0 * r0, ci);
                        VTri(c + d1 * r1, co, c + d1 * r0, ci, c + d0 * r0, ci);
                    }
                float flash = 0.5f - 2.4f * age;
                if (flash > 0f)
                    FillRect(0f, 0f, Game.Width, Game.Height, RainAcc.WithAlpha(flash * 0.35f));
            }
        }
    }
}

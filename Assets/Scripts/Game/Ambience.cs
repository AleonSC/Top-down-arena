using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// More visual effects (Juice.cs), on this screen only:
    ///   orbs     a health orb flying to you leaves a trail and a faint link to you; picking one up bursts into data bits and a hex ring on you (OrbLook.cs)
    ///   dust     a puff at your feet when you set off, turn hard or dash
    ///   weather  each path has its own: embers rising on Burning Grounds, snow on Frozen Lake, petals on Sanctuary,
    ///            red ash under the Blood Moon, mist motes in Fogbound, grit on Narrow Pass, pollen on Swarm Fields,
    ///            dust on Sniper Ridge, gold motes on the Elite Gauntlet, lantern sparks at the Bazaar
    public partial class ArenaApp
    {
        // --- Orbs --------------------------------------------------------------------------------------------------------
        class OrbSeen
        {
            public V2 Pos;
            public int Frame, N;
            public bool Moving, Big;
            public readonly V2[] Trail = new V2[7];
        }

        readonly Dictionary<int, OrbSeen> seenOrbs = new Dictionary<int, OrbSeen>();
        readonly Stack<OrbSeen> orbPool = new Stack<OrbSeen>();
        static readonly Rgba OrbGreen = new Rgba(0.35f, 1f, 0.5f);

        void UpdateOrbs()
        {
            foreach (var o in game.Orbs.Values)
            {
                OrbSeen s;
                if (!seenOrbs.TryGetValue(o.Id, out s))
                {
                    s = orbPool.Count > 0 ? orbPool.Pop() : new OrbSeen();
                    s.N = 0;
                    s.Pos = o.Disp;
                    seenOrbs[o.Id] = s;
                }
                s.Moving = o.Disp.DistanceTo(s.Pos) > 0.6f;
                if (s.Moving)
                {
                    for (int k = System.Math.Min(s.N, s.Trail.Length - 1); k > 0; k--)
                        s.Trail[k] = s.Trail[k - 1];
                    s.Trail[0] = s.Pos;
                    s.N = System.Math.Min(s.N + 1, s.Trail.Length);
                }
                else if (s.N > 0)
                    s.N--;
                s.Pos = o.Disp;
                s.Big = o.Big;
                s.Frame = juiceFrame;
            }
            goneIds.Clear();
            foreach (var kv in seenOrbs)
                if (kv.Value.Frame != juiceFrame)
                    goneIds.Add(kv.Key);
            foreach (int id in goneIds)
            {
                var s = seenOrbs[id];
                seenOrbs.Remove(id);
                // Picked up (gone right by a player, not faded out): a data burst on them.
                foreach (var p in game.Players.Values)
                    if (!p.Downed && p.Disp.DistanceTo(s.Pos) < p.Def.BodyR + 30f)
                    {
                        DataBurst(s.Pos, p.Disp, p.Def.BodyR, s.Big);  // OrbLook.cs
                        break;
                    }
                orbPool.Push(s);
            }
        }

        /// Under the arena's contents: the trail behind a flying orb, and a faint link to the player it's flying to.
        void DrawOrbTrails()
        {
            foreach (var s in seenOrbs.Values)
            {
                if (s.N < 2)
                    continue;
                V2 from = s.Pos;
                for (int i = 0; i < s.N; i++)
                {
                    float f = 1f - (float)i / s.N;
                    Line(from, s.Trail[i], (s.Big ? 7f : 5f) * f, OrbGreen.WithAlpha(0.35f * f));
                    from = s.Trail[i];
                }
                if (!s.Moving)
                    continue;
                Player near = null;
                foreach (var p in game.Players.Values)
                    if (!p.Downed && (near == null || p.Disp.DistanceSq(s.Pos) < near.Disp.DistanceSq(s.Pos)))
                        near = p;
                if (near != null && near.Disp.DistanceTo(s.Pos) < 160f)
                    Line(s.Pos, near.Disp, 1.5f, OrbGreen.WithAlpha(0.18f));
            }
        }

        // --- Dust ----------------------------------------------------------------------------------------------------------
        class Stride { public V2 Pos, Dir; public float Cool; public bool Moving; }
        readonly Dictionary<int, Stride> strides = new Dictionary<int, Stride>();
        float myDash;

        void UpdateDust(float dt)
        {
            foreach (var p in game.Players.Values)
            {
                Stride s;
                if (!strides.TryGetValue(p.Id, out s))
                {
                    strides[p.Id] = s = new Stride { Pos = p.Disp };
                    continue;
                }
                s.Cool -= dt;
                V2 step = p.Disp - s.Pos;
                s.Pos = p.Disp;
                if (dt <= 0f || p.Downed)
                    continue;
                float speed = step.Length / dt;
                bool moving = speed > 90f && speed < 1500f;  // (a wrap round the arena's edge isn't a step)
                V2 dir = moving ? step / step.Length : s.Dir;
                bool setOff = moving && !s.Moving, turn = moving && s.Moving && dir.Dot(s.Dir) < 0.2f;
                bool dash = p.Id == game.MyId && game.DashT > 0f && myDash <= 0f;
                if ((setOff || turn || dash) && s.Cool <= 0f)
                {
                    Puff(p.Disp + new V2(0f, p.Def.BodyR * 0.6f) - dir * (p.Def.BodyR * 0.6f), dir, dash ? 8 : 4);
                    s.Cool = 0.18f;
                }
                s.Moving = moving;
                if (moving)
                    s.Dir = dir;
            }
            myDash = game.DashT;
        }

        static readonly Rgba DustColor = new Rgba(0.62f, 0.6f, 0.68f);

        void Puff(V2 at, V2 dir, int n)
        {
            for (int i = 0; i < n; i++)
            {
                V2 v = -dir * Rnd(20f, 70f) + V2.FromAngle(Rnd(0f, MathX.Tau)) * Rnd(5f, 35f);
                AddPart(new Part { Kind = PDust, Pos = at + V2.FromAngle(Rnd(0f, MathX.Tau)) * Rnd(0f, 6f), Vel = v, Life = Rnd(0.35f, 0.55f), Max = 0.55f, Size = Rnd(3f, 5f), Col = DustColor });
            }
        }

        // --- Weather -------------------------------------------------------------------------------------------------------
        class WeatherDef
        {
            public int Count;
            public Rgba Col;
            public V2 Drift;            // px/s
            public float Size, Sway, Twinkle;
            public bool Streak, Glow;   // drawn as short streaks along their drift; glows in the additive pass
        }

        static WeatherDef WeatherFor(PathId p)
        {
            switch (p)
            {
                case PathId.BurningGrounds: return new WeatherDef { Count = 70, Col = new Rgba(1f, 0.55f, 0.2f, 0.85f), Drift = new V2(8f, -55f), Size = 2f, Sway = 18f, Twinkle = 6f, Glow = true };
                case PathId.FrozenLake: return new WeatherDef { Count = 110, Col = new Rgba(0.9f, 0.95f, 1f, 0.7f), Drift = new V2(-20f, 45f), Size = 2.2f, Sway = 14f };
                case PathId.Sanctuary: return new WeatherDef { Count = 40, Col = new Rgba(0.75f, 1f, 0.75f, 0.55f), Drift = new V2(12f, 22f), Size = 3f, Sway = 30f, Twinkle = 1.5f };
                case PathId.BloodMoon: return new WeatherDef { Count = 80, Col = new Rgba(0.75f, 0.12f, 0.15f, 0.6f), Drift = new V2(-6f, 38f), Size = 1.8f, Sway = 10f };
                case PathId.Fogbound: return new WeatherDef { Count = 50, Col = new Rgba(0.8f, 0.85f, 0.9f, 0.25f), Drift = new V2(14f, 4f), Size = 4f, Sway = 20f, Twinkle = 0.8f };
                case PathId.NarrowPass: return new WeatherDef { Count = 45, Col = new Rgba(0.7f, 0.6f, 0.5f, 0.5f), Drift = new V2(4f, 70f), Size = 1.6f, Sway = 4f };
                case PathId.SwarmFields: return new WeatherDef { Count = 55, Col = new Rgba(1f, 0.95f, 0.55f, 0.45f), Drift = new V2(18f, -6f), Size = 1.8f, Sway = 22f, Twinkle = 3f };
                case PathId.SniperRidge: return new WeatherDef { Count = 50, Col = new Rgba(0.75f, 0.68f, 0.55f, 0.35f), Drift = new V2(160f, 10f), Size = 1.5f, Sway = 6f, Streak = true };
                case PathId.EliteGauntlet: return new WeatherDef { Count = 40, Col = new Rgba(1f, 0.85f, 0.35f, 0.6f), Drift = new V2(0f, -18f), Size = 1.8f, Sway = 12f, Twinkle = 4f, Glow = true };
                case PathId.Bazaar: return new WeatherDef { Count = 35, Col = new Rgba(1f, 0.75f, 0.4f, 0.55f), Drift = new V2(0f, -25f), Size = 2f, Sway = 16f, Twinkle = 5f, Glow = true };
            }
            return null;
        }

        struct Flake { public V2 Pos; public float Phase, Depth; }
        readonly Flake[] flakes = new Flake[120];
        PathId weatherPath = PathId.None;
        float weatherFade;  // 0 to 1: weather fades in as a path starts, and out as it ends

        PathId WeatherPath() => game.OnPath && game.Wave % 5 != 0 && game.State != MatchState.GameOver ? game.CurrentPath : PathId.None;

        void UpdateWeather(float dt)
        {
            var want = WeatherPath();
            if (want != weatherPath)
            {
                weatherFade = Mathf.Max(0f, weatherFade - dt * 1.5f);
                if (weatherFade <= 0f)
                {
                    weatherPath = want;
                    for (int i = 0; i < flakes.Length; i++)
                        flakes[i] = new Flake { Pos = new V2(Rnd(0f, Game.Width), Rnd(0f, Game.Height)), Phase = Rnd(0f, MathX.Tau), Depth = Rnd(0.6f, 1.4f) };
                }
            }
            else
                weatherFade = Mathf.Min(1f, weatherFade + dt * 0.7f);
            var w = WeatherFor(weatherPath);
            if (w == null)
                return;
            for (int i = 0; i < w.Count && i < flakes.Length; i++)
            {
                var f = flakes[i];
                f.Pos += w.Drift * (f.Depth * dt);
                if (f.Pos.X < -20f) f.Pos.X += Game.Width + 40f;
                if (f.Pos.X > Game.Width + 20f) f.Pos.X -= Game.Width + 40f;
                if (f.Pos.Y < -20f) f.Pos.Y += Game.Height + 40f;
                if (f.Pos.Y > Game.Height + 20f) f.Pos.Y -= Game.Height + 40f;
                flakes[i] = f;
            }
        }

        V2 FlakeAt(Flake f, WeatherDef w, float t) => f.Pos + new V2(Mathf.Sin(t * 0.9f + f.Phase) * w.Sway, 0f);

        float FlakeAlpha(Flake f, WeatherDef w, float t) =>
            weatherFade * (w.Twinkle > 0f ? 0.55f + 0.45f * Mathf.Sin(t * w.Twinkle + f.Phase * 3f) : 1f);

        /// Over the arena's contents (under the mood tint): this path's weather.
        void DrawWeather()
        {
            var w = WeatherFor(weatherPath);
            if (w == null || weatherFade <= 0f)
                return;
            float t = juiceClock;
            for (int i = 0; i < w.Count && i < flakes.Length; i++)
            {
                var f = flakes[i];
                V2 at = FlakeAt(f, w, t);
                var c = w.Col.WithAlpha(w.Col.A * FlakeAlpha(f, w, t));
                if (w.Streak)
                    Line(at, at - w.Drift.Normalized() * (14f * f.Depth), w.Size, c);
                else
                    Circle(at, w.Size * f.Depth, c, 6);
            }
        }

        /// The glow pass: embers, gold motes and lantern sparks glow.
        void DrawWeatherGlow()
        {
            var w = WeatherFor(weatherPath);
            if (w == null || !w.Glow || weatherFade <= 0f)
                return;
            float t = juiceClock;
            for (int i = 0; i < w.Count && i < flakes.Length; i++)
            {
                var f = flakes[i];
                GlowDisc(FlakeAt(f, w, t), w.Size * f.Depth * 6f, w.Col, 0.35f * FlakeAlpha(f, w, t));
            }
        }

        void ClearAmbience()
        {
            foreach (var s in seenOrbs.Values) orbPool.Push(s);
            seenOrbs.Clear();
            strides.Clear();
            weatherPath = PathId.None;
            weatherFade = 0f;
        }
    }
}

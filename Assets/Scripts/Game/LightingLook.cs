using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Dynamic lighting (Settings > Visual effects > Dynamic lighting, with Visual effects on): shots, blasts, lightning
    /// and fire light the floor round them, and walls, pillars, crates, chests, turrets, players and enemies cast shadows
    /// from them (Core/LightCast.cs works out each light's fan). Two layers: the light on the floor, under the arena's
    /// contents (`DrawLighting`, from DrawWorld), and a faint haze of the same fans over them in the glow pass
    /// (`DrawLightHaze`), so light shafts show in the air between the shadows. Shots close together share one light, and
    /// only the brightest `MaxLights` on screen cast shadows (the rest keep their own glow).
    public partial class ArenaApp
    {
        bool lightsOn = true;  // Settings: Dynamic lighting ("lights_on")
        bool LightsLive => Juicy && lightsOn && game != null;

        public const int MaxLights = 24;
        const float LightCell = 56f;        // shots in the same cell share a light
        const float ShotLightA = 0.22f, ShotLightReach = 90f, ShotReachPerR = 8f, ShotReachMax = 210f;
        const float BoomLightA = 0.62f, BoomReachK = 2.4f;
        const float FlashLightA = 0.55f;    // lightning and meteor strikes
        const float FireLightA = 0.34f;
        const float LightHazeK = 0.32f;     // the haze over the contents, against the floor light
        static readonly float[] FloorRings = { 0f, 0.16f, 0.42f, 1f }, HazeRings = { 0f, 0.4f, 1f };
        static readonly Rgba FireLight = new Rgba(1f, 0.55f, 0.18f), StrikeLight = new Rgba(0.7f, 0.85f, 1f);

        struct LightSrc
        {
            public V2 Pos;
            public float Reach, A, Score;
            public Rgba Col;
            public int Count;
        }

        LightSrc[] lightSrc = new LightSrc[256];
        int lightSrcN;
        readonly Dictionary<int, int> lightCells = new Dictionary<int, int>();
        readonly ShadowScene shadowScene = new ShadowScene();
        readonly LightFan[] lightFans = MakeFans();
        readonly Rgba[] lightFanCol = new Rgba[MaxLights];
        readonly float[] lightFanA = new float[MaxLights];
        int lightFanN;
        SolidUnion lightWalls, lightEdges;

        static LightFan[] MakeFans()
        {
            var f = new LightFan[MaxLights];
            for (int i = 0; i < f.Length; i++)
                f[i] = new LightFan();
            return f;
        }

        /// DrawWorld, on the floor and under the arena's contents: this frame's lights, each clipped by its shadows.
        void DrawLighting(float t)
        {
            lightFanN = 0;
            if (!LightsLive)
                return;
            GatherLights(t);
            if (lightSrcN == 0)
                return;
            // the brightest on screen cast shadows (a partial selection sort: MaxLights is small)
            int take = Mathf.Min(MaxLights, lightSrcN);
            for (int i = 0; i < take; i++)
            {
                int best = i;
                for (int j = i + 1; j < lightSrcN; j++)
                    if (lightSrc[j].Score > lightSrc[best].Score)
                        best = j;
                var keep = lightSrc[i]; lightSrc[i] = lightSrc[best]; lightSrc[best] = keep;
            }
            BuildShadowScene();
            for (int i = 0; i < take; i++)
            {
                var s = lightSrc[i];
                if (s.Score <= 0f)
                    break;
                LightCast.Cast(shadowScene, s.Pos, s.Reach, lightFans[lightFanN], s.Reach < 110f ? 14 : LightCast.RimRays);
                lightFanCol[lightFanN] = s.Col;
                lightFanA[lightFanN] = s.A;
                lightFanN++;
            }
            if (lightFanN == 0)
                return;
            if (recording) recGlowFrom.Add(recVerts.Count);  // (a held frame replays it with the glow material: Juice.cs)
            SwitchToGlow(true);
            for (int i = 0; i < lightFanN; i++)
                DrawLightFan(lightFans[i], lightFanCol[i], lightFanA[i], FloorRings);
            SwitchToGlow(false);
            if (recording) recGlowTo.Add(recVerts.Count);
        }

        /// DrawGlowPass: the same fans, faint, over the bodies: the light hanging in the air.
        void DrawLightHaze()
        {
            for (int i = 0; i < lightFanN; i++)
                DrawLightFan(lightFans[i], lightFanCol[i], lightFanA[i] * LightHazeK, HazeRings);
        }

        // --- The lights ------------------------------------------------------------------------------------------------

        void GatherLights(float t)
        {
            lightSrcN = 0;
            lightCells.Clear();
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind == Defs.Caltrop)
                    continue;
                bool foe = Defs.IsEnemyShot(b.Kind);
                int cx = Mathf.FloorToInt(b.Pos.X / LightCell), cy = Mathf.FloorToInt(b.Pos.Y / LightCell);
                int key = ((cx * 7919) ^ (cy * 104729)) * 2 + (foe ? 1 : 0);
                float reach = Mathf.Min(ShotReachMax, ShotLightReach + b.Radius * ShotReachPerR);
                var col = Defs.BulletColor(b.Kind);
                int at;
                if (lightCells.TryGetValue(key, out at))
                {
                    var s = lightSrc[at];  // another shot in the cell: the light moves to their middle and grows
                    s.Count++;
                    s.Pos = s.Pos + (b.Pos - s.Pos) * (1f / s.Count);
                    s.Reach = Mathf.Max(s.Reach, reach);
                    s.Col = s.Col.Mix(col, 1f / s.Count);
                    lightSrc[at] = s;
                    continue;
                }
                lightCells[key] = lightSrcN;
                AddLight(b.Pos, reach, ShotLightA, col);
            }
            for (int i = 0; i < lightSrcN; i++)  // a cell of shots: brighter, a little wider, never blinding
                if (lightSrc[i].Count > 1)
                {
                    float k = Mathf.Sqrt(lightSrc[i].Count);
                    lightSrc[i].A = Mathf.Min(0.42f, ShotLightA * (0.6f + 0.4f * k));
                    lightSrc[i].Reach = Mathf.Min(ShotReachMax * 1.3f, lightSrc[i].Reach * (0.9f + 0.1f * k));
                }
            foreach (var fx in game.Effects)
            {
                float f = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f;
                switch (fx.Kind)
                {
                    case FxKind.Boom:
                    {
                        if (HammerBoomDrawnByMelee(fx) || IsLeapBoom(fx))
                            break;
                        float k = (1f - f) * (1f - f);
                        var col = IsGrenadeBoom(fx) || IsFlakBoom(fx) || IsGlBoom(fx) ? FragOrange : fx.Color;
                        AddLight(fx.Pos, fx.Radius * BoomReachK + 60f, BoomLightA * k, col);
                        break;
                    }
                    case FxKind.Strike:
                    case FxKind.RodBolt:
                        AddLight(fx.Pos, Mathf.Max(fx.Radius, 40f) * 2.6f + 60f, FlashLightA * (1f - f), StrikeLight);
                        break;
                    case FxKind.Meteor:
                        AddLight(fx.Pos, Mathf.Max(fx.Radius, 40f) * 2.4f + 60f, FlashLightA * (1f - f), FireLight);
                        break;
                }
            }
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.FlameWall || (z.Kind == ZoneKind.Meteor && z.Embers && z.T < z.Duration * 0.5f))
                {
                    float flick = 0.85f + 0.1f * Mathf.Sin(t * 17f + z.Id) + 0.05f * Mathf.Sin(t * 31f + z.Id * 2.3f);
                    float fade = Mathf.Clamp01(z.T / 0.6f);  // the flames sink in its last moments
                    AddLight(z.Pos, z.Radius * 1.5f + 90f, FireLightA * flick * fade, FireLight);
                }
            for (int i = 0; i < lightSrcN; i++)  // off screen: no shadows to show
            {
                var s = lightSrc[i];
                V2 lo = s.Pos - new V2(s.Reach, s.Reach), hi = s.Pos + new V2(s.Reach, s.Reach);
                lightSrc[i].Score = s.A < 0.02f || OffScreen(Px(lo), Px(hi), Px(new V2(lo.X, hi.Y))) ? 0f : s.A * s.Reach;
            }
        }

        void AddLight(V2 at, float reach, float a, Rgba col)
        {
            if (a < 0.02f || reach < 8f)
                return;
            if (lightSrcN == lightSrc.Length)
                System.Array.Resize(ref lightSrc, lightSrcN * 2);
            lightSrc[lightSrcN++] = new LightSrc { Pos = at, Reach = reach, A = a, Col = col, Count = 1 };
        }

        // --- The shadow casters ----------------------------------------------------------------------------------------

        void BuildShadowScene()
        {
            var s = shadowScene;
            s.Clear();
            var d = game.ActiveLayout;
            if (d != null && game.LayoutAge > 0.3f)  // (rising walls cast once they're up)
            {
                AddUnionEdges(SolidUnion.For(d.Solids, false, ref lightWalls));
                if (!game.Walled)  // (as LayoutLook.cs draws them)
                    AddUnionEdges(SolidUnion.For(d.Solids, true, ref lightEdges));
                foreach (var w in d.Solids)
                    if (w.Round && (!w.Edge || !game.Walled))
                        s.AddDisc(w.Pos, w.R);
            }
            foreach (var p in game.Props.Values)
            {
                if (p.Kind == PropKind.Crate || p.Kind == PropKind.HeavyCrate)
                    s.AddBox(p.Pos, Game.HalfW(p.Kind), Game.HalfW(p.Kind));
                else if (p.Kind == PropKind.Chest)
                    s.AddBox(p.Pos, Game.ChestW / 2f, Game.ChestH / 2f);
                else
                    s.AddDisc(p.Pos, Game.BarrelR);
            }
            foreach (var p in game.Players.Values)
                if (!LaunchHides(p) && p.VanishT <= 0f)
                    s.AddDisc(p.Disp, p.Downed ? p.Def.BodyR * 0.7f : p.Def.BodyR);
            foreach (var e in game.Enemies.Values)
                if (!game.IsSpawning(e))
                    s.AddDisc(e.Disp, e.Radius);
            foreach (var b in game.Placed)
                if (b.Kind == BuildKind.Turret)
                    s.AddDisc(b.Pos, TurretShadowR);
            if (game.RoomsOn)
                foreach (var tr in game.RoomTrapList)
                    if (tr.Kind == TrapKind.Turret && tr.Face.X == 0f && tr.Face.Y == 0f)  // (one set into a wall is in the wall's shadow)
                        s.AddDisc(tr.Pos, TurretShadowR);
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.DeployTurret)
                    s.AddDisc(z.Pos, TurretShadowR + 4f);
        }

        const float TurretShadowR = 16f;

        void AddUnionEdges(SolidUnion u)
        {
            if (u == null)
                return;
            var g = u.Edges;
            for (int i = 0; i + 3 < g.Length; i += 4)
                shadowScene.AddSeg(new V2(g[i], g[i + 1]), new V2(g[i + 2], g[i + 3]));
        }

        // --- Drawing ---------------------------------------------------------------------------------------------------

        /// One light's fan, its brightness falling off with the square of the distance: bands at `rings` (fractions of
        /// its reach) out each ray, each band stopped where the ray is.
        void DrawLightFan(LightFan f, Rgba col, float a, float[] rings)
        {
            int n = f.N;
            float reach = f.Reach;
            for (int i = 0; i < n; i++)
            {
                int j = i + 1 == n ? 0 : i + 1;
                V2 di = V2.FromAngle(f.Ang[i]), dj = V2.FromAngle(f.Ang[j]);
                float ri = f.Dist[i], rj = f.Dist[j], pi = 0f, pj = 0f;
                for (int k = 1; k < rings.Length; k++)
                {
                    float ring = rings[k] * reach;
                    float ci = Mathf.Min(ring, ri), cj = Mathf.Min(ring, rj);
                    if (ci <= pi && cj <= pj)
                        break;
                    V2 i1 = f.At + di * ci, j1 = f.At + dj * cj;
                    Rgba ai1 = col.WithAlpha(LightFall(a, ci, reach)), aj1 = col.WithAlpha(LightFall(a, cj, reach));
                    if (k == 1)
                        VTri(f.At, col.WithAlpha(a), i1, ai1, j1, aj1);
                    else
                    {
                        V2 i0 = f.At + di * pi, j0 = f.At + dj * pj;
                        Rgba ai0 = col.WithAlpha(LightFall(a, pi, reach)), aj0 = col.WithAlpha(LightFall(a, pj, reach));
                        if (ci > pi)
                            VTri(i0, ai0, i1, ai1, j1, aj1);
                        if (cj > pj)
                            VTri(i0, ai0, j1, aj1, j0, aj0);
                    }
                    pi = ci;
                    pj = cj;
                }
            }
        }

        static float LightFall(float a, float d, float reach)
        {
            float u = 1f - d / reach;
            return u <= 0f ? 0f : a * u * u;
        }
    }
}

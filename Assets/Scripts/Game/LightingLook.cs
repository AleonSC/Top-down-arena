using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Dynamic lighting (Settings > Visual effects > Lighting, with Visual effects on): shots, muzzle flashes, blasts,
    /// lightning, Railgun beams, bursts and fire light the floor round them, and walls, pillars, crates, chests, turrets,
    /// players and enemies cast shadows from them (Core/LightCast.cs works out each light's fan). Two layers: the light on
    /// the floor, under the arena's contents (`DrawLighting`, from DrawWorld), and a faint haze of the same fans over them
    /// in the glow pass (`DrawLightHaze`), so light shafts show in the air between the shadows.
    /// Options: Hard or Soft shadows (Soft: the brightest few lights are cast three times from a small source, so their
    /// shadows' edges blur), and the floor dimmed Off / Low / High under the lights (`DimFloor`).
    /// Cost stays bounded in a crowd: shots close together share one light, only the `MaxLights` brightest on screen cast
    /// shadows, each looks at its `LightCasters` nearest casters at most, and once the frame's rays pass `LightRayBudget`
    /// the rest light the floor with no shadows (and soft ones fall back to hard). In a big crowd (`LightCrowdFrom` to
    /// `LightCrowdFull`) the lights, the ray budget and the bands step down, and soft edges turn hard.
    public partial class ArenaApp
    {
        bool lightsOn = true;  // Settings: Lighting on/off ("lights_on")
        bool lightsSoft = true;  // Settings: Soft or Hard shadow edges ("lights_soft")
        int floorDim;            // Settings: Floor dim 0 Off, 1 Low, 2 High ("floor_dim")
        public static readonly string[] FloorDimNames = { "Off", "Low", "High" };
        static readonly float[] FloorDimA = { 0f, 0.32f, 0.58f };
        bool LightsLive => Juicy && lightsOn && game != null;

        public const int MaxLights = 24;
        public const int SoftLights = 8;       // Soft: the brightest this many get soft edges
        public const int LightCasters = 28;    // the most casters one light looks at
        public const int LightRayBudget = 2600;  // a frame's rays (all lights, all samples); past it: no more shadows
        const float LightCrowdFrom = 80f, LightCrowdFull = 320f;  // enemies (+ a quarter of the shots): the lighting steps back
        const int SoftSamples = 3;
        const float LightCell = 56f;        // shots in the same cell share a light
        const float ShotLightA = 0.22f, ShotLightReach = 90f, ShotReachPerR = 8f, ShotReachMax = 210f;
        const float BoomLightA = 0.62f, BoomReachK = 2.4f;
        const float FlashLightA = 0.55f;    // lightning and meteor strikes
        const float FireLightA = 0.34f;
        const float MuzzleLightA = 0.4f, MuzzleLightReach = 95f, MuzzleLife = 0.07f;
        const float LightHazeK = 0.32f;     // the haze over the contents, against the floor light
        static readonly float[] FloorRings = { 0f, 0.16f, 0.42f, 1f }, HazeRings = { 0f, 0.4f, 1f }, CheapRings = { 0f, 1f };
        static readonly Rgba FireLight = new Rgba(1f, 0.55f, 0.18f), StrikeLight = new Rgba(0.7f, 0.85f, 1f),
            FrostLight = new Rgba(0.6f, 0.9f, 1f), BloodLight = new Rgba(1f, 0.22f, 0.2f), ParryLight = new Rgba(1f, 0.95f, 0.7f),
            MuzzleLight = new Rgba(1f, 0.9f, 0.65f);

        struct LightSrc
        {
            public V2 Pos;
            public float Reach, A, Score;
            public Rgba Col;
            public int Count;
        }

        struct MuzzleFlash { public V2 Pos; public float At; public Rgba Col; }

        LightSrc[] lightSrc = new LightSrc[256];
        int lightSrcN;
        readonly Dictionary<int, int> lightCells = new Dictionary<int, int>();
        readonly ShadowScene shadowScene = new ShadowScene();
        readonly LightFan[] lightFans = MakeFans();
        readonly Rgba[] lightFanCol = new Rgba[MaxLights * SoftSamples];
        readonly float[] lightFanA = new float[MaxLights * SoftSamples];
        readonly bool[] lightFanHaze = new bool[MaxLights * SoftSamples];  // one sample of each light draws the haze
        int lightFanN, lightRays, rayBudget = LightRayBudget;
        readonly MuzzleFlash[] muzzleFlashes = new MuzzleFlash[32];
        int muzzleNext;

        static LightFan[] MakeFans()
        {
            var f = new LightFan[MaxLights * SoftSamples];
            for (int i = 0; i < f.Length; i++)
                f[i] = new LightFan();
            return f;
        }

        /// Juice.cs, a gun's shot leaving the muzzle: a short flash of light there.
        void NoteMuzzleLight(V2 at, Rgba col)
        {
            muzzleFlashes[muzzleNext] = new MuzzleFlash { Pos = at, At = juiceClock, Col = col.Mix(MuzzleLight, 0.6f) };
            muzzleNext = (muzzleNext + 1) % muzzleFlashes.Length;
        }

        /// DrawWorld, on the floor and under the arena's contents: the floor dimmed, then this frame's lights, each
        /// clipped by its shadows.
        void DrawLighting(float t)
        {
            lightFanN = 0;
            lightRays = 0;
            if (!LightsLive)
                return;
            DimFloor();
            GatherLights(t);
            if (lightSrcN == 0)
                return;
            // A big crowd already makes a heavy frame: the lighting steps back as it grows (fewer lights and rays, and hard
            // edges past the middle), from LightCrowdFrom to LightCrowdFull enemies (a shot counts a quarter)
            float crowd = Mathf.Clamp01((game.Enemies.Count + game.Bullets.Count * 0.25f - LightCrowdFrom) / (LightCrowdFull - LightCrowdFrom));
            int maxLights = Mathf.RoundToInt(Mathf.Lerp(MaxLights, MaxLights * 0.55f, crowd));
            rayBudget = Mathf.RoundToInt(Mathf.Lerp(LightRayBudget, LightRayBudget * 0.5f, crowd));
            bool softOk = lightsSoft && crowd < 0.5f;
            // the brightest on screen cast shadows (a partial selection sort: MaxLights is small)
            int take = Mathf.Min(maxLights, lightSrcN);
            for (int i = 0; i < take; i++)
            {
                int best = i;
                for (int j = i + 1; j < lightSrcN; j++)
                    if (lightSrc[j].Score > lightSrc[best].Score)
                        best = j;
                var keep = lightSrc[i]; lightSrc[i] = lightSrc[best]; lightSrc[best] = keep;
                if (lightSrc[i].Score <= 0f)
                {
                    take = i;
                    break;
                }
            }
            if (take == 0)
                return;
            BuildShadowScene(take);
            for (int i = 0; i < take; i++)
            {
                var s = lightSrc[i];
                int rim = s.Reach < 110f ? 14 : LightCast.RimRays;
                bool budget = lightRays < rayBudget;
                int casters = budget ? LightCasters : 0;  // over budget: light, no shadows
                bool soft = softOk && budget && i < SoftLights && lightRays < rayBudget * 2 / 3;
                if (!soft)
                {
                    AddFan(s.Pos, s.Reach, rim, casters, s.Col, s.A, true);
                    continue;
                }
                // Soft: three casts from the corners of a small source, each a third as bright; their shadows overlap
                // in the middle (umbra) and part at the edges (penumbra)
                float src = Mathf.Clamp(s.Reach * 0.1f, 6f, 22f), turn = i * 1.3f;
                for (int k = 0; k < SoftSamples; k++)
                    AddFan(s.Pos + V2.FromAngle(turn + MathX.Tau * k / SoftSamples) * src, s.Reach, rim, casters, s.Col, s.A / SoftSamples, k == 0);
            }
            if (lightFanN == 0)
                return;
            if (recording) recGlowFrom.Add(recVerts.Count);  // (a held frame replays it with the glow material: Juice.cs)
            SwitchToGlow(true);
            var rings = lightRays > rayBudget || crowd > 0.5f ? HazeRings : FloorRings;  // (a busy frame: fewer bands)
            for (int i = 0; i < lightFanN; i++)
                DrawLightFan(lightFans[i], lightFanCol[i], lightFanA[i], rings);
            SwitchToGlow(false);
            if (recording) recGlowTo.Add(recVerts.Count);
        }

        void AddFan(V2 at, float reach, int rim, int casters, Rgba col, float a, bool haze)
        {
            var f = lightFans[lightFanN];
            LightCast.Cast(shadowScene, at, reach, f, rim, casters);
            lightRays += f.N;
            lightFanCol[lightFanN] = col;
            lightFanA[lightFanN] = a;
            lightFanHaze[lightFanN] = haze;
            lightFanN++;
        }

        /// DrawGlowPass: the same fans, faint, over the bodies: the light hanging in the air (a soft light's first sample
        /// only, at the light's full strength).
        void DrawLightHaze()
        {
            var rings = lightRays > rayBudget / 2 ? CheapRings : HazeRings;
            for (int i = 0; i < lightFanN; i++)
                if (lightFanHaze[i])
                {
                    float a = lightFanA[i] * LightHazeK;
                    if (i + 1 < lightFanN && !lightFanHaze[i + 1])
                        a *= SoftSamples;  // (a soft light: its samples share its strength)
                    DrawLightFan(lightFans[i], lightFanCol[i], a, rings);
                }
        }

        /// Floor dim: the floor under the lights darkened, so they stand out (walls, props and bodies draw over it).
        void DimFloor()
        {
            float a = FloorDimA[Mathf.Clamp(floorDim, 0, FloorDimA.Length - 1)];
            if (a <= 0f)
                return;
            const float m = 6000f;  // past any map: the screen clips it
            FillRect(-m, -m, AW + 2f * m, AH + 2f * m, new Rgba(0f, 0f, 0.01f, a));
        }

        // --- The lights ------------------------------------------------------------------------------------------------

        void GatherLights(float t)
        {
            lightSrcN = 0;
            lightCells.Clear();
            foreach (var b in game.Bullets.Values)
            {
                if (b.Kind == Defs.Caltrop || b.Kind == Defs.RodentShot || b.Kind == Defs.PaperCrane)
                    continue;  // (a Rodent Minigun rodent gives no light)
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
            foreach (var m in muzzleFlashes)
            {
                float age = juiceClock - m.At;
                if (m.Col.A > 0f && age >= 0f && age < MuzzleLife)  // (an unused slot's colour is clear)
                {
                    float k = 1f - age / MuzzleLife;
                    AddLight(m.Pos, MuzzleLightReach, MuzzleLightA * k, m.Col);
                }
            }
            foreach (var fx in game.Effects)
                EffectLight(fx);
            foreach (var z in game.Zones.Values)
            {
                float fade = Mathf.Clamp01(z.T / 0.6f);  // gone in its last moments
                if (z.Kind == ZoneKind.FlameWall || (z.Kind == ZoneKind.Meteor && z.Embers && z.T < z.Duration * 0.5f))
                {
                    float flick = 0.85f + 0.1f * Mathf.Sin(t * 17f + z.Id) + 0.05f * Mathf.Sin(t * 31f + z.Id * 2.3f);
                    AddLight(z.Pos, z.Radius * 1.5f + 90f, FireLightA * flick * fade, FireLight);
                }
                else if (z.Kind == ZoneKind.ShockGrenade || z.Kind == ZoneKind.ArcCrush)
                {
                    float crackle = 0.7f + 0.3f * Mathf.Abs(Mathf.Sin(t * 23f + z.Id * 1.7f));
                    AddLight(z.Pos, z.Radius * 1.3f + 70f, 0.26f * crackle * fade, StrikeLight);
                }
            }
            for (int i = 0; i < lightSrcN; i++)  // off screen: no shadows to show
            {
                var s = lightSrc[i];
                V2 lo = s.Pos - new V2(s.Reach, s.Reach), hi = s.Pos + new V2(s.Reach, s.Reach);
                lightSrc[i].Score = s.A < 0.02f || OffScreen(Px(lo), Px(hi), Px(new V2(lo.X, hi.Y))) ? 0f : s.A * s.Reach;
            }
        }

        /// The light an effect gives off while it plays, if any (its strength falls with its age).
        void EffectLight(Effect fx)
        {
            float f = fx.Dur > 0f ? Mathf.Clamp01(fx.T / fx.Dur) : 1f, k = 1f - f, k2 = k * k;
            var rgb = fx.Color.WithAlpha(1f);
            switch (fx.Kind)
            {
                case FxKind.Boom:
                    if (!HammerBoomDrawnByMelee(fx) && !IsLeapBoom(fx))
                        AddLight(fx.Pos, fx.Radius * BoomReachK + 60f, BoomLightA * k2, IsGrenadeBoom(fx) || IsFlakBoom(fx) || IsGlBoom(fx) ? FragOrange : rgb);
                    break;
                case FxKind.Strike:
                case FxKind.RodBolt:
                case FxKind.NimbusStrike:
                    AddLight(fx.Pos, Mathf.Max(fx.Kind == FxKind.RodBolt ? 30f : fx.Radius, 40f) * 2.6f + 60f, FlashLightA * k, StrikeLight);
                    break;
                case FxKind.Meteor:
                    AddLight(fx.Pos, Mathf.Max(fx.Radius, 40f) * 2.4f + 60f, FlashLightA * k, FireLight);
                    break;
                case FxKind.Bolt:      // lightning from one point to another: lit at its middle
                case FxKind.ArcLink:
                case FxKind.ArcFork:
                case FxKind.RodArc:
                {
                    float len = fx.Pos.DistanceTo(fx.Pos2);
                    AddLight((fx.Pos + fx.Pos2) * 0.5f, Mathf.Min(260f, len * 0.5f + 70f), 0.38f * k, StrikeLight);
                    break;
                }
                case FxKind.RailBeam:  // a beam: lights along it, one every 150 px (up to 5)
                {
                    float len = fx.Pos.DistanceTo(fx.Pos2);
                    int n = Mathf.Clamp(Mathf.RoundToInt(len / 150f), 1, 5);
                    for (int i = 0; i < n; i++)
                        AddLight(fx.Pos + (fx.Pos2 - fx.Pos) * ((i + 0.5f) / n), 140f, 0.42f * k, rgb);
                    break;
                }
                case FxKind.ArcBall:  // the Arc Cannon's ball, rolling from Pos to Pos2
                    AddLight(fx.Pos + (fx.Pos2 - fx.Pos) * f, 140f, 0.34f, StrikeLight);
                    break;
                case FxKind.ArcBurst:
                case FxKind.HammerSlam:
                case FxKind.Overcharge:
                    AddLight(fx.Pos, Mathf.Clamp(fx.Kind == FxKind.Overcharge ? 30f : fx.Radius, 40f, 220f) * 1.4f + 60f, 0.5f * k2, StrikeLight);
                    break;
                case FxKind.Nova:
                    AddLight(fx.Pos, 230f, 0.5f * k2, rgb);
                    break;
                case FxKind.Shockwave:  // the light rides out with the wave
                    AddLight(fx.Pos, Mathf.Max(80f, fx.Radius * f) + 80f, 0.4f * k2, new Rgba(0.8f, 0.9f, 1f));
                    break;
                case FxKind.FrostBurst:
                case FxKind.AbsoluteZero:
                    AddLight(fx.Pos, fx.Kind == FxKind.AbsoluteZero ? 320f : 180f, 0.45f * k2, FrostLight);
                    break;
                case FxKind.EmbolusBlast:
                    AddLight(fx.Pos, 200f, 0.45f * k2, BloodLight);
                    break;
                case FxKind.PortalSeal:
                case FxKind.DecoyPop:
                    AddLight(fx.Pos, Mathf.Clamp(fx.Radius, 30f, 160f) * 1.6f + 60f, 0.45f * k2, fx.Kind == FxKind.DecoyPop ? FragOrange : rgb);
                    break;
                case FxKind.PerfectParry:
                    AddLight(fx.Pos, 170f, 0.5f * k2, ParryLight);
                    break;
            }
        }

        void AddLight(V2 at, float reach, float a, Rgba col)
        {
            if (a < 0.02f || reach < 8f)
                return;
            if (col.R + col.G + col.B < 0.3f)
                col = StrikeLight;  // (an effect whose colour carries data, not a colour)
            if (lightSrcN == lightSrc.Length)
                System.Array.Resize(ref lightSrc, lightSrcN * 2);
            lightSrc[lightSrcN++] = new LightSrc { Pos = at, Reach = reach, A = a, Col = col, Count = 1 };
        }

        // --- The shadow casters ----------------------------------------------------------------------------------------

        /// Everything that can cast a shadow from the first `lights` lights: only what's inside their reach is kept, so a
        /// big map or a crowd off screen costs nothing.
        void BuildShadowScene(int lights)
        {
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = float.MinValue, y1 = float.MinValue;
            for (int i = 0; i < lights; i++)
            {
                var l = lightSrc[i];
                float r = l.Reach + 20f;
                x0 = Mathf.Min(x0, l.Pos.X - r); y0 = Mathf.Min(y0, l.Pos.Y - r);
                x1 = Mathf.Max(x1, l.Pos.X + r); y1 = Mathf.Max(y1, l.Pos.Y + r);
            }
            var s = shadowScene;
            s.Clear();
            var d = game.ActiveLayout;
            if (d != null && game.LayoutAge > 0.3f)  // (rising walls cast once they're up)
            {
                if (!game.Walled)  // (as LayoutLook.cs draws them; its unions, cached)
                    AddUnionEdges(SolidUnion.For(d.Solids, true, ref edgeUnion), x0, y0, x1, y1);
                AddUnionEdges(SolidUnion.For(d.Solids, false, ref wallUnion), x0, y0, x1, y1);
                foreach (var w in d.Solids)
                    if (w.Round && (!w.Edge || !game.Walled) && Near(w.Pos, w.R, x0, y0, x1, y1))
                        s.AddDisc(w.Pos, w.R);
            }
            foreach (var p in game.Props.Values)
            {
                if (!Near(p.Pos, 40f, x0, y0, x1, y1))
                    continue;
                if (p.Kind == PropKind.Crate || p.Kind == PropKind.HeavyCrate)
                    s.AddBox(p.Pos, Game.HalfW(p.Kind), Game.HalfW(p.Kind));
                else if (p.Kind == PropKind.Chest)
                    s.AddBox(p.Pos, Game.ChestW / 2f, Game.ChestH / 2f);
                else
                    s.AddDisc(p.Pos, Game.BarrelR);
            }
            foreach (var p in game.Players.Values)
                if (!LaunchHides(p) && p.VanishT <= 0f && Near(p.Disp, p.Def.BodyR, x0, y0, x1, y1))
                    s.AddDisc(p.Disp, p.Downed ? p.Def.BodyR * 0.7f : p.Def.BodyR);
            foreach (var e in game.Enemies.Values)
                if (Near(e.Disp, e.Radius, x0, y0, x1, y1) && !game.IsSpawning(e))
                    s.AddDisc(e.Disp, e.Radius);
            foreach (var b in game.Placed)
                if (b.Kind == BuildKind.Turret && Near(b.Pos, TurretShadowR, x0, y0, x1, y1))
                    s.AddDisc(b.Pos, TurretShadowR);
            if (game.RoomsOn)
                foreach (var tr in game.RoomTrapList)
                    if (tr.Kind == TrapKind.Turret && tr.Face.X == 0f && tr.Face.Y == 0f && Near(tr.Pos, TurretShadowR, x0, y0, x1, y1))  // (one set into a wall is in the wall's shadow)
                        s.AddDisc(tr.Pos, TurretShadowR);
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.DeployTurret && Near(z.Pos, TurretShadowR + 4f, x0, y0, x1, y1))
                    s.AddDisc(z.Pos, TurretShadowR + 4f);
        }

        const float TurretShadowR = 16f;

        static bool Near(V2 p, float r, float x0, float y0, float x1, float y1) => p.X + r > x0 && p.X - r < x1 && p.Y + r > y0 && p.Y - r < y1;

        void AddUnionEdges(SolidUnion u, float x0, float y0, float x1, float y1)
        {
            if (u == null)
                return;
            var g = u.Edges;
            for (int i = 0; i + 3 < g.Length; i += 4)
                if (g[i + 2] > x0 && g[i] < x1 && g[i + 3] > y0 && g[i + 1] < y1)  // (edges run x0 <= x1, y0 <= y1)
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

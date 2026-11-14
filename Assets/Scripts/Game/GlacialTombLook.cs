using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Glacial Tomb in look C, "Crystal Reliquary" (Docs/Mockups/glacial-tomb.html), with Visual effects on. The cast's Ring
    /// effect (900 px, lasting `Game.TombFxTime`: the freeze plus a second) carries the whole thing, and every part runs off its clock:
    ///   floor   a hex lattice of frost grows out from the caster cell by cell at 1100 px/s, with a bright growing edge, and a
    ///           turning mandala sits under the caster; when the freeze ends the lattice drains back out the same way
    ///   enemies each frozen enemy is caged in a cluster of seven spires that rise as the wave reaches it (`DrawTombSpires`, in place
    ///           of Frost Nova's ice block); they pale and pulse faster over the last second, then crack and fall as shards and mist
    ///           (`DrawTombShatter`) the moment the host stops the freeze
    ///   shots   each enemy shot the cast erases is replaced by a ripple and a tiny crystal or star that blooms and fades; the
    ///           positions come from the last frame before the cast (`UpdateTombLook`), since the host erases them at once
    ///   bosses  a ring of eight spears jabs in and out around them (they take damage instead of freezing)
    /// Drawing only: nothing is synced and the effect is told by its radius and lifetime. With effects off, the old plain ring.
    public partial class ArenaApp
    {
        const float TombSpd = 1100f, TombHex = 30f, TombFlat = 0.55f;
        static readonly Rgba TombDeep = new Rgba(24f / 255f, 78f / 255f, 160f / 255f), TombPale = new Rgba(196f / 255f, 238f / 255f, 1f);
        static readonly Rgba TombMint = new Rgba(0.49f, 1f, 0.88f);
        static readonly int[] TombPyramidOrder = { 3, 4, 2, 5, 0, 1 };  // back faces first

        /// One cage: when its spires start to rise (renderer clock), when the freeze should end, and when it broke (0 = still held).
        class TombState { public float Start, End, Thaw; public float Seed; }
        readonly Dictionary<int, TombState> tombStates = new Dictionary<int, TombState>();
        readonly Dictionary<Effect, List<V2>> tombSnaps = new Dictionary<Effect, List<V2>>();  // enemy shots a cast erased, by cast
        readonly List<V2> tombPrev = new List<V2>();  // enemy shots as of the last frame
        readonly List<Effect> tombGone = new List<Effect>();
        readonly List<int> tombDead = new List<int>();

        static bool SameTombTime(float a, float b) => Mathf.Abs(a - b) < 0.01f;
        bool IsGlacialTomb(Effect fx) => fx.Kind == FxKind.Ring && Mathf.Abs(fx.Radius - 900f) < 1f &&
            (SameTombTime(fx.Dur, Game.TombFxTime(false)) || SameTombTime(fx.Dur, Game.TombFxTime(true)));
        static float TombFreeze(Effect fx) => fx.Dur - Game.TombFxTail;

        void ClearTombLook()
        {
            tombStates.Clear();
            tombSnaps.Clear();
            tombPrev.Clear();
        }

        /// Each frame: keeps the enemy shot positions, takes a copy for each new cast, and forgets cages that are over.
        void UpdateTombLook(float t)
        {
            bool any = false;
            foreach (var fx in game.Effects)
                if (IsGlacialTomb(fx))
                {
                    any = true;
                    if (!tombSnaps.ContainsKey(fx))
                        tombSnaps[fx] = new List<V2>(tombPrev);
                }
            if (tombSnaps.Count > 0)
            {
                tombGone.Clear();
                foreach (var k in tombSnaps.Keys)
                    if (!game.Effects.Contains(k))
                        tombGone.Add(k);
                foreach (var k in tombGone)
                    tombSnaps.Remove(k);
            }
            tombPrev.Clear();
            if (!any)  // (while one is going the shots are erased: nothing new to remember)
                foreach (var b in game.Bullets.Values)
                    if (Defs.IsEnemyShot(b.Kind) && tombPrev.Count < 160)
                        tombPrev.Add(b.Pos);
            if (tombStates.Count > 0)
            {
                tombDead.Clear();
                foreach (var kv in tombStates)
                    if (!game.Enemies.ContainsKey(kv.Key) || (kv.Value.Thaw > 0f && t - kv.Value.Thaw > 1.2f) || t - kv.Value.Start > 20f)
                        tombDead.Add(kv.Key);
                foreach (int id in tombDead)
                    tombStates.Remove(id);
            }
        }

        // --- Shared pieces -------------------------------------------------------------------------------------------

        static float TombBack(float x)
        {
            x = Mathf.Clamp01(x);
            const float c = 1.9f;
            return 1f + (c + 1f) * Mathf.Pow(x - 1f, 3f) + c * Mathf.Pow(x - 1f, 2f);
        }

        static Rgba TombCol(float s, float a) => TombDeep.Mix(TombPale, Mathf.Clamp01(s)).WithAlpha(a);

        /// A hexagonal crystal standing on `b`: six shaded sides, a six-sided pyramid on top, tilted by `lean`. The ground is drawn
        /// flattened (TombFlat) so it reads as standing up out of the floor.
        void TombCrystal(V2 b, float r, float h, float lean, float tip, float tone, float al)
        {
            if (h < 1f || al <= 0.01f)
                return;
            V2[] v = new V2[6], tp = new V2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * MathX.Pi / 3f + MathX.Pi / 6f, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                v[i] = new V2(b.X + ca * r, b.Y + sa * r * TombFlat);
                tp[i] = new V2(b.X + lean + ca * r * 0.78f, b.Y - h + sa * r * TombFlat * 0.78f);
            }
            V2 apex = new V2(b.X + lean, b.Y - h - tip);
            const float light = -0.9f;
            for (int i = 0; i < 6; i++)
            {
                int j = (i + 1) % 6;
                float ma = i * MathX.Pi / 3f + MathX.Pi / 3f;
                if (Mathf.Sin(ma) < -0.25f)
                    continue;
                SetColor(TombCol((0.3f + 0.35f * (0.5f + 0.5f * Mathf.Cos(ma - light))) * tone, al));
                Quad(v[i], v[j], tp[j], tp[i]);
                Line(v[i], tp[i], 1.1f, new Rgba(0.88f, 0.97f, 1f, 0.5f * al));
            }
            foreach (int i in TombPyramidOrder)
            {
                int j = (i + 1) % 6;
                float ma = i * MathX.Pi / 3f + MathX.Pi / 3f;
                SetColor(TombCol((0.5f + 0.5f * (0.5f + 0.5f * Mathf.Cos(ma - light))) * tone, al));
                Tri(tp[i], tp[j], apex);
                Line(tp[i], apex, 1.1f, new Rgba(0.92f, 0.98f, 1f, 0.65f * al));
            }
        }

        void TombStar(V2 c, float r, float rot, Rgba col)
        {
            SetColor(col);
            V2 prev = c + V2.FromAngle(rot) * r;
            for (int i = 1; i <= 8; i++)
            {
                V2 next = c + V2.FromAngle(rot + i * MathX.Pi / 4f) * (i % 2 == 1 ? r * 0.28f : r);
                Tri(c, prev, next);
                prev = next;
            }
        }

        /// A hexagon outline on the floor, squashed like the rest of the ground.
        void TombFlatHex(V2 c, float r, float rot, float width, Rgba col)
        {
            for (int i = 0; i < 6; i++)
            {
                float a0 = rot + i * MathX.Tau / 6f, a1 = rot + (i + 1) * MathX.Tau / 6f;
                Line(c + new V2(Mathf.Cos(a0) * r, Mathf.Sin(a0) * r * TombFlat), c + new V2(Mathf.Cos(a1) * r, Mathf.Sin(a1) * r * TombFlat), width, col);
            }
        }

        // --- Floor: the lattice and the mandala ----------------------------------------------------------------------

        void DrawTombFloor(float t)
        {
            if (!fxOn)
                return;
            foreach (var fx in game.Effects)
                if (IsGlacialTomb(fx))
                    DrawTombLattice(fx);
        }

        readonly V2[] tombHexPts = new V2[6];
        static readonly V2[] TombHexDirs = { V2.FromAngle(0f), V2.FromAngle(MathX.Tau / 6f), V2.FromAngle(MathX.Tau * 2f / 6f), V2.FromAngle(MathX.Tau * 3f / 6f), V2.FromAngle(MathX.Tau * 4f / 6f), V2.FromAngle(MathX.Tau * 5f / 6f) };

        void DrawTombLattice(Effect fx)
        {
            float age = fx.T, freeze = TombFreeze(fx), R = Mathf.Clamp(age, 0f, 1.6f) * TombSpd;
            if (R <= 0f)
                return;
            float inner = Mathf.Max(0f, (age - freeze) * TombSpd);
            V2 c = fx.Pos;
            float dx = TombHex * 1.5f, dy = TombHex * 1.7320508f;
            int i0 = Mathf.Max(-1, Mathf.FloorToInt((c.X - R) / dx)), i1 = Mathf.Min(Mathf.CeilToInt(Game.Width / dx) + 1, Mathf.CeilToInt((c.X + R) / dx));
            for (int i = i0; i <= i1; i++)
            {
                int j0 = Mathf.Max(-1, Mathf.FloorToInt((c.Y - R) / dy) - 1), j1 = Mathf.Min(Mathf.CeilToInt(Game.Height / dy) + 1, Mathf.CeilToInt((c.Y + R) / dy) + 1);
                for (int j = j0; j <= j1; j++)
                {
                    V2 q = new V2(i * dx, j * dy + ((i & 1) != 0 ? dy / 2f : 0f));
                    float d = q.DistanceTo(c);
                    if (d > R || d < inner - 10f)
                        continue;
                    float rnd = Hash01(i * 17f + j * 3.1f);
                    float grow = Mathf.Clamp01((R - d) / 140f), edge = Mathf.Clamp01(1f - (R - d) / 70f), rec = inner > 0f ? Mathf.Clamp01((d - inner) / 60f) : 1f;
                    float fillA = Mathf.Clamp01(((0.05f + 0.1f * rnd) * grow + 0.28f * edge) * rec);
                    Rgba fill = edge > 0.2f ? new Rgba(0.86f, 0.98f, 1f, fillA) : new Rgba(0.47f, 0.82f, 1f, fillA);
                    SetColor(fill);
                    float hr = TombHex - 2f;
                    for (int k = 0; k < 6; k++)
                        tombHexPts[k] = q + TombHexDirs[k] * hr;
                    Quad(tombHexPts[0], tombHexPts[1], tombHexPts[2], tombHexPts[3]);  // the hexagon in 4 triangles, not 6 (a lattice is hundreds of cells)
                    Quad(tombHexPts[0], tombHexPts[3], tombHexPts[4], tombHexPts[5]);
                    float la = (0.18f + 0.45f * edge) * grow * rec;
                    if (la > 0.03f)
                        for (int k = 0; k < 6; k++)
                            Line(tombHexPts[k], tombHexPts[(k + 1) % 6], 1f, new Rgba(0.75f, 0.94f, 1f, la));
                }
            }
            // the mandala under the caster
            float fade = Mathf.Clamp01(1f - (age - 2.4f) / 2f) * 0.7f + 0.3f, k0 = Mathf.Clamp01(age / 0.25f);
            float spin = age * (age < 0.9f ? 4f : 0.35f) + 2f;
            float alpha = 0.85f * fade * k0 * Mathf.Clamp01((freeze + 0.5f - age) / 0.5f);
            if (alpha > 0.02f)
            {
                Rgba m = TombMint.WithAlpha(alpha);
                TombFlatHex(c, 60f, spin, 1.8f, m);
                TombFlatHex(c, 60f, -spin * 0.8f + 0.5f, 1.8f, m);
                TombFlatHex(c, 44f, spin * 1.3f, 1.8f, m);
                for (int i = 0; i < 40; i++)
                {
                    float a0 = i * MathX.Tau / 40f, a1 = (i + 1) * MathX.Tau / 40f;
                    Line(c + new V2(Mathf.Cos(a0) * 72f, Mathf.Sin(a0) * 72f * TombFlat), c + new V2(Mathf.Cos(a1) * 72f, Mathf.Sin(a1) * 72f * TombFlat), 1.6f, m);
                }
            }
        }

        // --- The effect itself: wave, flash, shot sprouts, boss spears -------------------------------------------------

        void DrawGlacialTomb(Effect fx)
        {
            float age = fx.T;
            if (!fxOn)  // the old ring: out and gone in under a second
            {
                float f = age / 0.8f;
                if (f < 1f)
                    Arc(fx.Pos, fx.Radius * (0.2f + 0.8f * f), 4f, fx.Color.WithAlpha(1f - f), 64);
                return;
            }
            float R = Mathf.Clamp(age, 0f, 1.6f) * TombSpd;
            if (!fxReduced && age < 0.16f)
                GlowDisc(fx.Pos, 420f, new Rgba(0.82f, 1f, 0.96f), 0.5f * (1f - age / 0.16f));
            if (R > 0f && R < 1150f)
            {
                float a = Mathf.Clamp01(1f - R / 1150f);
                Arc(fx.Pos, R - 8f, 22f, new Rgba(0.4f, 0.78f, 1f, 0.3f * a), 72);
                Arc(fx.Pos, R, 4f, new Rgba(0.78f, 1f, 0.96f, a), 72);
            }
            // each shot the cast erased: a ripple, then a small crystal or a star that blooms and fades
            if (tombSnaps.TryGetValue(fx, out var snap))
                for (int i = 0; i < snap.Count; i++)
                {
                    V2 p = snap[i];
                    float local = age - p.DistanceTo(fx.Pos) / TombSpd;
                    if (local < 0f || local > 0.7f)
                        continue;
                    if (local < 0.4f)
                        Arc(p, 16f * EaseOut3(local / 0.4f) + 2f, 2f * (1f - local / 0.4f), new Rgba(0.78f, 0.96f, 1f, 0.9f), 14);
                    float g = local < 0.25f ? TombBack(local / 0.25f) : Mathf.Clamp01(1f - (local - 0.25f) / 0.45f);
                    if (i % 2 == 0)
                        TombCrystal(p + new V2(0f, 4f), 3.5f, 13f * g, 0f, 5f * g, 1f, 0.95f);
                    else
                        TombStar(p, 8f * g, local * 3f, new Rgba(0.75f, 0.98f, 0.94f, 0.9f * (1f - local / 0.7f)));
                }
            // bosses take the damage instead of freezing: a ring of spears jabs in and out
            foreach (var e in game.Enemies.Values)
            {
                if (!e.Def.IsBoss || e.SpawnT > 0f)
                    continue;
                float local = age - e.Disp.DistanceTo(fx.Pos) / TombSpd;
                if (local < 0f || local > 1.2f)
                    continue;
                float k = local < 0.25f ? TombBack(local / 0.25f) : Mathf.Clamp01(1f - (local - 0.5f) / 0.7f);
                float br = e.Def.Radius, sc = Mathf.Clamp(br / 46f, 0.6f, 2.2f);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * MathX.Tau / 8f + 0.2f, rad = 78f * sc * (1f - 0.3f * (1f - Mathf.Clamp01(k)));
                    V2 at = e.Disp + new V2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad * 0.6f + 8f);
                    TombCrystal(at, 9f * sc, 64f * sc * k, -Mathf.Cos(a) * 10f * k, 18f * sc * k, 1f, 0.92f);
                }
            }
        }

        // --- Enemies: the spire cluster and its breaking -------------------------------------------------------------

        /// Finds (or starts) this enemy's cage if a Glacial Tomb froze it; null when something else did.
        TombState TombCage(Enemy e, float t)
        {
            if (tombStates.TryGetValue(e.Id, out var s))
                return s;
            if (!e.Frozen || e.Def.IsBoss)
                return null;
            foreach (var fx in game.Effects)
            {
                if (!IsGlacialTomb(fx) || fx.T > TombFreeze(fx))
                    continue;
                float d = fx.Pos.DistanceTo(e.Disp);
                if (d > fx.Radius + e.Def.Radius)
                    continue;
                float fxStart = t - fx.T;
                s = new TombState { Start = fxStart + d / TombSpd, End = fxStart + TombFreeze(fx), Seed = e.Id * 1.37f };
                tombStates[e.Id] = s;
                return s;
            }
            return null;
        }

        /// A frozen enemy's spires. False when this freeze isn't a Glacial Tomb's (the caller draws Frost Nova's block).
        bool DrawTombSpires(Enemy e, V2 pos, float r, float t)
        {
            var s = TombCage(e, t);
            if (s == null)
                return false;
            s.Thaw = 0f;  // frozen again, or still
            float age = t - s.Start;
            if (age < 0f)
                return true;  // the wave hasn't reached it yet
            float left = s.End - t, scale = Mathf.Clamp(r, 8f, 40f) / 15f;
            float last = Mathf.Clamp01(1f - left / 1f), tone = 1f + last * 0.5f;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (4f + Mathf.Clamp01(1f - left / 1.2f) * 18f));
            if (age < 0.5f)  // the ring where the floor cracks open
                Arc(pos + new V2(0f, r * 0.4f), r * 3.6f * EaseOut3(age / 0.5f), 5f * (1f - age / 0.5f), new Rgba(0.75f, 1f, 0.94f, 0.9f * (1f - age / 0.5f)), 28);
            GlowDisc(pos + new V2(0f, 6f), r * 3.2f, TombMint, 0.22f + 0.12f * pulse);
            Circle(pos, r * 1.1f, new Rgba(0.55f, 0.78f, 1f, 0.3f), 20);  // the enemy, seen through glass
            float shake = left < 0.9f ? Mathf.Sin(t * 80f) * (1f - left / 0.9f) * 1.6f : 0f;
            // painter order: back to front by each crystal's ground row
            const int n = 7;
            int[] order = new int[n];
            float[] row = new float[n];
            for (int k = 0; k < n; k++)
            {
                order[k] = k;
                row[k] = k == 0 ? 0f : Mathf.Sin((k - 1) * MathX.Tau / 6f + 0.4f) * 0.95f;
            }
            for (int a = 1; a < n; a++)
                for (int b = a; b > 0 && row[order[b - 1]] > row[order[b]]; b--)
                {
                    int tmp = order[b]; order[b] = order[b - 1]; order[b - 1] = tmp;
                }
            float hBase = Mathf.Min(r * 3.8f, 150f);
            for (int oi = 0; oi < n; oi++)
            {
                int k = order[oi];
                float dx, dy, rs, hs, lean, tip, dl;
                if (k == 0) { dx = 0f; dy = 0f; rs = 0.5f; hs = 1.25f; lean = 0f; tip = 18f; dl = 0f; }
                else
                {
                    float a = (k - 1) * MathX.Tau / 6f + 0.4f, hv = Hash01(s.Seed + k);
                    dx = Mathf.Cos(a) * 0.95f; dy = Mathf.Sin(a) * 0.95f;
                    rs = 0.3f + hv * 0.08f; hs = 0.45f + Hash01(s.Seed + k + 9f) * 0.3f;
                    lean = Mathf.Cos(a) * (6f + hv * 6f) * scale; tip = (10f + hv * 6f); dl = 0.05f + hv * 0.1f;
                }
                float g = TombBack((age - dl) / 0.3f);
                if (g <= 0f)
                    continue;
                V2 b = new V2(pos.X + dx * r * 0.95f + shake, pos.Y + dy * r * 0.75f + r * 0.45f);
                TombCrystal(b, r * rs * 1.2f, hBase * hs * g, lean * g, tip * scale * g, tone, k == 0 ? 0.74f : 0.88f);
            }
            return true;
        }

        /// The cage breaking: shards thrown up and out, mist, and a ring on the floor, for 0.9 s once the freeze has ended.
        void DrawTombShatter(Enemy e, V2 pos, float r, float t)
        {
            if (!tombStates.TryGetValue(e.Id, out var s) || e.Frozen)
                return;
            if (t - s.Start < 0f)
                return;
            if (s.Thaw <= 0f)
                s.Thaw = t;
            float u = t - s.Thaw;
            if (u > 0.9f)
                return;
            float k = u / 0.9f, scale = Mathf.Clamp(r, 8f, 40f) / 15f;
            V2 o = pos + new V2(0f, -20f * scale);
            if (u < 0.5f)
                Arc(pos + new V2(0f, 8f), r * 4f * EaseOut3(u / 0.5f), 4f * (1f - u / 0.5f), new Rgba(0.86f, 0.96f, 1f, 0.9f * (1f - u / 0.5f)), 28);
            for (int i = 0; i < 26; i++)
            {
                float a = Hash01(s.Seed + i * 1.7f) * MathX.Tau, sp = 300f * (0.4f + 0.8f * Hash01(s.Seed + i * 2.3f)) * scale;
                float life = 0.7f + 0.5f * Hash01(s.Seed + i * 3.1f);
                float uu = u / life;
                if (uu >= 1f)
                    continue;
                V2 p = o + new V2(Mathf.Cos(a) * sp * u, Mathf.Sin(a) * sp * u - 140f * u + 130f * u * u);
                float size = (5f + 6f * Hash01(s.Seed + i * 4.9f)) * Mathf.Min(scale, 1.6f), rot = Hash01(s.Seed + i) * MathX.Tau + u * (Hash01(s.Seed + i * 6f) - 0.5f) * 14f;
                V2 d0 = V2.FromAngle(rot) * size, d1 = V2.FromAngle(rot + 2.5f) * size * 0.55f, d2 = V2.FromAngle(rot - 2.4f) * size * 0.5f;
                float al = 1f - uu;
                SetColor(new Rgba(0.75f, 0.92f, 1f, al));
                Tri(p + d0, p + d1, p + d2);
                Line(p + d0, p + d1, 1f, new Rgba(0.93f, 0.98f, 1f, 0.7f * al));
            }
            for (int i = 0; i < 6; i++)
            {
                V2 p = pos + new V2((Hash01(s.Seed + i * 5.3f) - 0.5f) * 44f * scale + (Hash01(s.Seed + i) - 0.5f) * 40f * u, 6f - (30f + 20f * Hash01(s.Seed + i * 7f)) * u);
                GlowDisc(p, 38f * scale * (0.6f + k), new Rgba(0.75f, 0.88f, 1f), 0.3f * (1f - k));
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Deployable Cover (the Tank Soldier's 4th choice, Core/DeployableCover.cs), look A "Stake Drop" of
    /// Docs/Mockups/deployable-cover.html. Drawn from the replicated zone (`ZoneKind.Cover`: Pos, Aim, T, Duration, GuardFrac):
    ///   the throw: a canister arcs out from the caster over its shadow, its blue light blinking (0 - CoverLand s);
    ///   the landing: a thump (a small kick for covers near you), a dust ring and a pale shockwave; the back wall springs up from
    ///     the middle with an overshoot, then the two arms swing back from lying along it and four bolts stake it down with sparks;
    ///     the blue light strip on its inside flickers on and the health bar fades in;
    ///   standing: steel plates with height (a ground shadow and a dark side face), a hazard band on the front face, rivets, panel
    ///     seams; it darkens, dents and cracks as GuardFrac falls (a crack every sixth), and the bar turns amber then red;
    ///   FxKind.CoverHit: a white flash and a small jolt on the plate, a spark burst back toward the shooter;
    ///   FxKind.CoverBreak: a flash, plates tumbling away from the shots with dust and sparks, then scraps, bolt stubs and a dark
    ///     patch left on the floor for a few seconds; FxKind.CoverFold: the arms fold in and the wall sinks away.
    /// Visual effects off: the plates and the bar only, no throw, flashes or debris.
    public partial class ArenaApp
    {
        static readonly Rgba CoverTop = new Rgba(0.557f, 0.596f, 0.639f), CoverWorn = new Rgba(0.345f, 0.369f, 0.396f);
        static readonly Rgba CoverSide = new Rgba(0.227f, 0.255f, 0.29f), CoverInk = new Rgba(0.149f, 0.169f, 0.192f);
        static readonly Rgba CoverHazard = new Rgba(0.85f, 0.643f, 0.227f), CoverLed = new Rgba(0.67f, 0.84f, 1f), CoverSpark = new Rgba(1f, 0.86f, 0.6f);
        static readonly Rgba CoverDust = new Rgba(0.59f, 0.57f, 0.53f);
        const float CoverLand = 0.22f;

        readonly HashSet<int> coverLanded = new HashSet<int>();  // covers whose landing has kicked the camera
        readonly Dictionary<int, float> coverGhost = new Dictionary<int, float>();  // the health bar's pale drain behind the real one

        static float CoverHash(float n) => Mathf.Repeat(Mathf.Sin(n * 127.1f + 311.7f) * 43758.5453f, 1f);
        static float CoverEo(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }
        static float CoverBack(float x) { x = Mathf.Clamp01(x); const float c = 2.2f; float u = x - 1f; return 1f + (c + 1f) * u * u * u + c * u * u; }
        static float CoverPh(float t, float a, float b) => Mathf.Clamp01((t - a) / (b - a));

        // The frame being drawn: its middle, forward (out along its facing) and across.
        V2 cvO, cvF, cvS;
        void CoverFrame(V2 pos, float aim) { cvO = pos; cvF = V2.FromAngle(aim); cvS = new V2(-cvF.Y, cvF.X); }
        V2 CP(float lx, float ly) => cvO + cvF * lx + cvS * ly;
        V2 CPv(V2 l) => CP(l.X, l.Y);

        /// The four corners of piece k (0 the wall, 1 and 2 the arms), with the wall grown to `wall` and the arms swung out by `arm`.
        void CoverCorners(int k, float wall, float arm, V2 off, out V2 a, out V2 b, out V2 c, out V2 d)
        {
            float th = Game.CoverThick, half = Game.CoverHalf * wall;
            if (k == 0)
            {
                a = CP(-th, -half) + off; b = CP(th, -half) + off; c = CP(th, half) + off; d = CP(-th, half) + off;
                return;
            }
            float side = k == 1 ? -1f : 1f, hw = Game.CoverArmW * 0.5f;
            float ang = side * (MathX.Pi * 0.5f + MathX.Pi * 0.5f * arm);  // lying along the wall (folded) to reaching back (arm 1)
            V2 hinge = new V2(0f, side * (Game.CoverHalf - hw) * wall);
            V2 dir = V2.FromAngle(ang), n = new V2(-dir.Y, dir.X);
            a = CPv(hinge + dir * th + n * hw) + off; b = CPv(hinge + dir * Game.CoverArm + n * hw) + off;
            c = CPv(hinge + dir * Game.CoverArm - n * hw) + off; d = CPv(hinge + dir * th - n * hw) + off;
        }

        void CoverPieces(float wall, float arm, V2 off, Rgba col, bool outline)
        {
            for (int k = 2; k >= 0; k--)
            {
                if (k > 0 && wall < 0.98f && arm <= 0.01f)
                    continue;  // the arms only show once the wall is up
                CoverCorners(k, wall, arm, off, out V2 a, out V2 b, out V2 c, out V2 d);
                Quad(a, b, c, d, col);
                if (outline)
                {
                    Line(a, b, 1.4f, CoverInk.WithAlpha(col.A)); Line(b, c, 1.4f, CoverInk.WithAlpha(col.A));
                    Line(c, d, 1.4f, CoverInk.WithAlpha(col.A)); Line(d, a, 1.4f, CoverInk.WithAlpha(col.A));
                }
            }
        }

        /// The bracket itself. dmg 0-1 (health lost), flash 0-1, led 0-1, jit a jolt back along its facing.
        void DrawCoverBody(V2 pos, float aim, float wall, float arm, float alpha, float dmg, float flash, float led, int seed, float jit)
        {
            if (alpha <= 0.01f || wall <= 0.01f)
                return;
            CoverFrame(pos, aim);
            cvO = cvO + cvF * jit;
            float th = Game.CoverThick, half = Game.CoverHalf * wall;
            CoverPieces(wall, arm, new V2(5f, 8f), new Rgba(0f, 0f, 0f, 0.42f * alpha), false);  // ground shadow
            CoverPieces(wall, arm, new V2(0f, 4.4f), CoverSide.WithAlpha(alpha), false);   // the side face (height)
            CoverPieces(wall, arm, V2.Zero, CoverTop.Mix(CoverWorn, dmg).WithAlpha(alpha), true);
            if (wall > 0.6f)  // the hazard band on the front face
            {
                float x0 = th - 3.4f;
                Quad(CP(x0, -half), CP(th, -half), CP(th, half), CP(x0, half), new Rgba(0.11f, 0.12f, 0.13f, alpha));
                var hz = (dmg > 0.5f ? CoverHazard.Mix(CoverWorn, 0.45f) : CoverHazard).WithAlpha(alpha);
                for (float y = -half; y < half - 2f; y += 9.4f)
                    Line(CP(x0, y), CP(th, Mathf.Min(half, y + 6.8f)), 2.6f, hz);
            }
            Line(CP(-th + 1.4f, -half + 2f), CP(-th + 1.4f, half - 2f), 1.3f, Rgba.White.WithAlpha(0.3f * alpha));  // top edge catching the light
            if (wall > 0.9f)
                for (int s = -1; s <= 1; s++)  // panel seams
                    Line(CP(-th, s * 20.4f + 10.2f), CP(th - 3.4f, s * 20.4f + 10.2f), 1.2f, new Rgba(0f, 0f, 0f, 0.35f * alpha));
            if (arm > 0.95f)
            {
                float hy = Game.CoverHalf - Game.CoverArmW * 0.5f;
                foreach (var p in new[] { CP(-Game.CoverArm + 4f, -hy), CP(-Game.CoverArm + 4f, hy), CP(0f, -hy), CP(0f, hy) })
                    Circle(p, 1.8f, new Rgba(0.165f, 0.188f, 0.212f, alpha), 6);  // rivets
            }
            if (led > 0f && fxOn)  // the light strip inside (tank blue)
            {
                Line(CP(-th - 2f, -27f), CP(-th - 2f, 27f), 5f, new Rgba(0.44f, 0.69f, 0.94f, 0.35f * led * alpha));
                Line(CP(-th - 2f, -27f), CP(-th - 2f, 27f), 1.8f, CoverLed.WithAlpha(0.95f * led * alpha));
            }
            int cracks = Mathf.FloorToInt(dmg * 7f);  // a crack every sixth (and a seventh as it gives)
            for (int k = 0; k < cracks; k++)
            {
                float y0 = -34f + CoverHash(seed * 40 + k) * 68f;
                V2 prev = CP(th, y0);
                for (int q = 1; q < 4; q++)
                {
                    V2 next = CP(th - q * 3.4f, y0 + (CoverHash(seed * 40 + k * 7 + q) - 0.5f) * 15f);
                    Line(prev, next, 1.3f, new Rgba(0.08f, 0.086f, 0.1f, 0.9f * alpha));
                    prev = next;
                }
            }
            int dents = Mathf.RoundToInt(dmg * 16f);  // dents along the front face where the shots landed
            for (int k = 0; k < dents; k++)
            {
                float y = -37f + CoverHash(seed * 13 + k * 3.1f) * 74f;
                bool big = CoverHash(seed * 5 + k) > 0.75f;
                Circle(CP(th - 1.2f, y), big ? 2.7f : 1.9f, new Rgba(0.1f, 0.11f, 0.125f, 0.75f * alpha), 6);
                Circle(CP(th - 1.9f, y - 0.7f), 0.9f, Rgba.White.WithAlpha(0.25f * alpha), 4);
            }
            if (flash > 0f)
                CoverPieces(wall, arm, V2.Zero, Rgba.White.WithAlpha(0.8f * flash * alpha), false);
        }

        readonly HashSet<int> coverHeard = new HashSet<int>();  // covers whose build sound has played (by zone id)
        readonly List<int> coverHeardGone = new List<int>();

        /// Every frame: a cover's build sound the first time it's seen young (one already standing when you arrive stays quiet),
        /// and a crash, fold or ting for each FxKind.CoverBreak, CoverFold and CoverHit (CoverSoundBank.cs).
        void UpdateCoverSound()
        {
            float now = Time.unscaledTime;
            coverHeardGone.Clear();
            foreach (int id in coverHeard)
                if (!game.Zones.ContainsKey(id))
                    coverHeardGone.Add(id);
            foreach (int id in coverHeardGone)
            {
                coverHeard.Remove(id);
                coverLanded.Remove(id);
                coverGhost.Remove(id);
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.Cover || !coverHeard.Add(z.Id) || Game.CoverAge(z) > 0.3f)
                    continue;
                bool mine = z.Owner == game.MyId;
                sounds?.Play(new SoundCue { Sfx = Sfx.CoverDeploy, Ally = !mine, Quiet = mine ? 0f : game.QuietAt(z.Pos) }, now);
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Heard || fx.Kind != FxKind.CoverBreak && fx.Kind != FxKind.CoverFold && fx.Kind != FxKind.CoverHit)
                    continue;
                fx.Heard = true;
                if (fx.T >= 0.3f)
                    continue;  // (an old one, seen late)
                var sfx = fx.Kind == FxKind.CoverBreak ? Sfx.CoverBreak : fx.Kind == FxKind.CoverFold ? Sfx.CoverFold : Sfx.CoverTing;
                sounds?.Play(new SoundCue { Sfx = sfx, Quiet = game.QuietAt(fx.Pos) }, now);
            }
        }

        /// A standing (or landing) cover, from the zone layer (DrawZone).
        void DrawCover(Zone z, float t)
        {
            if (z.T <= 0f)
                return;
            float u = Game.CoverAge(z), frac = Mathf.Clamp01(game.CoverFrac(z)), dmg = 1f - frac;
            V2 fwd = V2.FromAngle(z.Aim);
            // the last shot to ring off it: a flash and a jolt
            float hitAge = 9f;
            V2 key = z.Pos + fwd;
            if (fxOn)
                foreach (var fx in FxOf(FxKind.CoverHit))
                    if (fx.Kind == FxKind.CoverHit && fx.T < hitAge && fx.Pos2.DistanceSq(key) < 1f)
                        hitAge = fx.T;
            float flash = hitAge < 0.07f ? 0.45f * (1f - hitAge / 0.07f) : 0f, jit = hitAge < 0.07f ? -1.4f * (1f - hitAge / 0.07f) : 0f;

            if (!fxOn)
            {
                DrawCoverBody(z.Pos, z.Aim, 1f, 1f, u < CoverLand ? 0f : 1f, dmg, 0f, 1f, z.Id, 0f);
                DrawCoverBar(z, u, frac);
                return;
            }
            float wall = CoverBack(CoverPh(u, CoverLand, 0.40f)), arm = CoverBack(CoverPh(u, 0.34f, 0.54f));
            float led = u < 0.56f ? 0f : u < 0.7f ? (Mathf.Sin(u * 120f) > 0f ? 1f : 0.2f) : 1f;
            if (u > 0.5f && u < 0.66f)
                flash = Mathf.Max(flash, (1f - CoverPh(u, 0.5f, 0.66f)) * 0.5f);

            if (u < CoverLand)  // the canister in flight
            {
                float f = u / CoverLand;
                V2 from = z.Pos - fwd * (Game.CoverAhead - 26f), g = V2.Lerp(from, z.Pos, f);
                float h = Mathf.Sin(Mathf.PI * f) * 40f;
                Ellipse(g + new V2(0f, 3f), 8f, 4f, new Rgba(0f, 0f, 0f, 0.4f));
                CoverFrame(g - new V2(0f, h), f * 9f);
                Quad(CP(-8.5f, -5f), CP(8.5f, -5f), CP(8.5f, 5f), CP(-8.5f, 5f), CoverTop);
                Quad(CP(-1.7f, -5f), CP(1.7f, -5f), CP(1.7f, 5f), CP(-1.7f, 5f), CoverHazard);
                GlowDisc(g - new V2(0f, h), 10f, new Rgba(0.47f, 0.75f, 1f), Mathf.Sin(u * 60f) > 0f ? 0.9f : 0.3f);
            }
            else if (coverLanded.Add(z.Id) && u < CoverLand + 0.2f)
            {
                Player me;
                if (game.Players.TryGetValue(game.MyId, out me) && me.Pos.DistanceTo(z.Pos) < 450f)
                    Shake(0.12f);  // the thump
            }

            float v = u - CoverLand;
            if (v >= 0f && v < 0.45f)  // dust ring and shockwave
            {
                float k = v / 0.45f, e = CoverEo(k);
                Arc(z.Pos, 14f + 54f * e, 6f * (1f - k) + 1.5f, CoverDust.WithAlpha(0.55f * (1f - k)), 32);
                if (!fxReduced)
                    for (int q = 0; q < 10; q++)
                    {
                        float a = CoverHash(z.Id * 20 + q) * MathX.Tau, r = 17f + 44f * e * (0.5f + CoverHash(q + 3) * 0.5f);
                        Circle(z.Pos + V2.FromAngle(a) * r, 5f + 7f * e, CoverDust.WithAlpha(0.22f * (1f - k)), 10);
                    }
                if (v < 0.2f)
                    Arc(z.Pos, 75f * CoverEo(v / 0.2f), 1.8f, new Rgba(1f, 0.94f, 0.82f, 0.8f * (1f - v / 0.2f)), 36);
            }

            DrawCoverBody(z.Pos, z.Aim, wall, arm, 1f, dmg, flash, led, z.Id, jit);

            float w = u - 0.52f;
            if (w >= 0f && w < 0.2f)  // the bolts staking it down
            {
                float f2 = 1f - w / 0.2f, hy = Game.CoverHalf - Game.CoverArmW * 0.5f;
                CoverFrame(z.Pos, z.Aim);
                V2[] bolts = { CP(-Game.CoverArm + 4f, -hy), CP(-Game.CoverArm + 4f, hy), CP(0f, -hy), CP(0f, hy) };
                for (int k = 0; k < 4; k++)
                {
                    GlowDisc(bolts[k], 12f * f2 + 2f, CoverSpark, 0.95f);
                    for (int s = 0; s < 3; s++)
                    {
                        float a2 = CoverHash(k * 9 + s + z.Id) * MathX.Tau, r2 = 5f + 15f * (w / 0.2f);
                        Circle(bolts[k] + V2.FromAngle(a2) * r2, 1.7f * f2, CoverSpark.WithAlpha(0.9f), 5);
                    }
                }
            }
            DrawCoverBar(z, u, frac);
        }

        /// The health bar over a cover: four segments, blue, amber under half, red under a quarter, with a pale drain behind it.
        void DrawCoverBar(Zone z, float u, float frac)
        {
            float a = fxOn ? CoverPh(u, 0.55f, 0.8f) : (u < CoverLand ? 0f : 1f);
            if (a <= 0f)
                return;
            float ghost;
            if (!coverGhost.TryGetValue(z.Id, out ghost) || ghost < frac)
                ghost = frac;
            ghost = Mathf.Max(frac, ghost - Time.unscaledDeltaTime * 0.6f);
            coverGhost[z.Id] = ghost;
            const float w = 51f, h = 5f;
            V2 at = z.Pos + new V2(-w * 0.5f, -Game.CoverHalf - 16f);
            FillRect(at.X - 1.5f, at.Y - 1.5f, w + 3f, h + 3f, new Rgba(0f, 0f, 0f, 0.65f * a));
            FillRect(at.X, at.Y, w * ghost, h, new Rgba(1f, 0.93f, 0.78f, 0.5f * a));
            var col = frac > 0.5f ? new Rgba(0.44f, 0.69f, 0.94f) : frac > 0.25f ? new Rgba(0.88f, 0.65f, 0.25f) : new Rgba(0.88f, 0.33f, 0.25f);
            FillRect(at.X, at.Y, w * frac, h, col.WithAlpha(a));
            FillRect(at.X, at.Y, w * frac, 1.5f, Rgba.White.WithAlpha(0.4f * a));
            for (int n = 1; n < 4; n++)
                FillRect(at.X + w * n / 4f - 0.75f, at.Y, 1.5f, h, new Rgba(0f, 0f, 0f, 0.6f * a));
        }

        /// FxKind.CoverHit: a spark burst back toward the shooter where the shot rang off.
        void DrawCoverHitFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            Zone src = null;
            V2 facing = V2.Zero;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.Cover && (z.Pos + V2.FromAngle(z.Aim)).DistanceSq(fx.Pos2) < 1f)
                    src = z;
            if (src != null)
                facing = V2.FromAngle(src.Aim);
            float back = facing.IsZero ? (fx.Pos - fx.Pos2).Angle : facing.Angle;
            bool big = fx.Radius > 6f;
            GlowDisc(fx.Pos, (big ? 17f : 12f) * (1f - f), CoverSpark, 0.9f);
            int n = big ? 6 : 4;
            for (int s = 0; s < n; s++)
            {
                float a = back + (CoverHash(fx.Pos.X * 0.37f + fx.Pos.Y + s) - 0.5f) * 2.2f, r = 5f + 24f * CoverEo(f) * (0.5f + CoverHash(s + fx.Pos.Y) * 0.5f);
                Line(fx.Pos + V2.FromAngle(a) * (r * 0.5f), fx.Pos + V2.FromAngle(a) * r, 1.7f, CoverSpark.WithAlpha(1f - f));
            }
        }

        /// FxKind.CoverBreak: the burst (0.9 s), then the scraps on the floor for the rest of the effect.
        void DrawCoverBreakFx(Effect fx)
        {
            float v = fx.T, aim = (fx.Pos2 - fx.Pos).Angle;
            int seed = (int)fx.Radius;
            V2 c = fx.Pos;
            CoverFrame(c, aim);
            float e = 1f - Mathf.Exp(-3.2f * Mathf.Min(v, 0.9f));
            float[,] spots = { { 0, -32 }, { 0, -15 }, { 0, 2 }, { 0, 19 }, { 0, 34 }, { -17, -36 }, { -17, 36 }, { -26, -36 }, { -26, 36 } };
            if (v >= 0.9f)  // what's left on the floor
            {
                float al = Mathf.Clamp01(1f - (v - 0.9f) / Mathf.Max(0.1f, fx.Dur - 0.9f));
                Ellipse(c - cvF * 10f, 42f, 30f, new Rgba(0f, 0f, 0f, 0.28f * al));
                for (int k = 0; k < spots.GetLength(0); k++)
                {
                    float h = CoverHash(seed * 90 + k), h2 = CoverHash(seed * 90 + k + 33);
                    float lx = spots[k, 0] - (85f + h * 153f) * e / 3.2f, ly = spots[k, 1] * (1f + 2.4f * e / 3.2f) + (h2 - 0.5f) * 100f * e / 3.2f;
                    V2 q = CP(lx, ly);
                    float sz = 3.4f + h2 * 3.4f;
                    Quad(q + new V2(-sz, -sz * 0.6f), q + new V2(sz, -sz * 0.6f), q + new V2(sz, sz * 0.6f), q + new V2(-sz, sz * 0.6f), new Rgba(0.357f, 0.388f, 0.424f, al));
                }
                float hy = Game.CoverHalf - Game.CoverArmW * 0.5f;
                foreach (var p in new[] { CP(0f, -hy), CP(0f, hy), CP(-Game.CoverArm + 4f, -hy), CP(-Game.CoverArm + 4f, hy) })
                {
                    Circle(p, 2.7f, new Rgba(0.48f, 0.52f, 0.56f, al), 6);
                    Circle(p, 1.8f, new Rgba(0.165f, 0.188f, 0.212f, al), 6);
                }
                return;
            }
            float f = v / 0.9f;
            if (v < 0.12f)
                GlowDisc(c, 85f * (1f - v / 0.12f) + 17f, new Rgba(1f, 0.9f, 0.75f), 0.7f);
            if (!fxReduced)
                for (int k = 0; k < 12; k++)  // dust
                {
                    float a = CoverHash(seed * 11 + k) * MathX.Tau, r = 14f + 58f * CoverEo(f) * (0.4f + CoverHash(k + 2) * 0.6f);
                    Circle(c + V2.FromAngle(a) * r - cvF * (14f * f), 7f + 14f * CoverEo(f), CoverDust.WithAlpha(0.35f * (1f - f) * 0.6f), 10);
                }
            for (int k = 0; k < spots.GetLength(0); k++)  // the plates, flung back away from the shots, tumbling
            {
                float h = CoverHash(seed * 90 + k), h2 = CoverHash(seed * 90 + k + 33);
                float lx = spots[k, 0] - (85f + h * 153f) * e / 3.2f, ly = spots[k, 1] * (1f + 2.4f * e / 3.2f) + (h2 - 0.5f) * 100f * e / 3.2f;
                V2 q = CP(lx, ly);
                float lift = Mathf.Sin(Mathf.PI * Mathf.Min(1f, v / 0.6f)) * 20f * ((6.8f + h2 * 6.8f) / 10f);
                float pw = 6.8f + h2 * 6.8f, ph = 5f + h * 5f, rot = aim + (h - 0.5f) * 14f * e / 3.2f;
                Ellipse(q + new V2(1.7f, 3.4f), pw * 0.5f, ph * 0.5f, new Rgba(0f, 0f, 0f, 0.35f));
                V2 m = q - new V2(0f, lift), ax = V2.FromAngle(rot) * (pw * 0.5f), ay = V2.FromAngle(rot + MathX.Pi * 0.5f) * (ph * 0.5f);
                Quad(m - ax - ay, m + ax - ay, m + ax + ay, m - ax + ay, CoverTop);
                Line(m - ax - ay, m + ax - ay, 1.2f, CoverInk); Line(m + ax + ay, m - ax + ay, 1.2f, CoverInk);
                if (spots[k, 1] < -25f || spots[k, 1] > 25f)
                    Line(m - ax - ay, m - ax + ay, 2f, CoverHazard);
            }
            if (v < 0.5f)  // sparks
                for (int s = 0; s < 14; s++)
                {
                    float a2 = aim + MathX.Pi + (CoverHash(s * 3 + seed) - 0.5f) * 2.6f, r2 = 10f + 100f * CoverEo(v / 0.5f) * (0.4f + CoverHash(s) * 0.6f);
                    Circle(c + V2.FromAngle(a2) * r2, 2.4f * (1f - v / 0.5f), CoverSpark.WithAlpha(0.95f), 5);
                }
        }

        /// FxKind.CoverFold: the arms fold in along the wall, then the wall sinks away.
        void DrawCoverFoldFx(Effect fx)
        {
            float v = fx.T, aim = (fx.Pos2 - fx.Pos).Angle;
            float arm = 1f - CoverEo(CoverPh(v, 0f, 0.22f)), wall = 1f - CoverEo(CoverPh(v, 0.18f, 0.42f));
            DrawCoverBody(fx.Pos, aim, Mathf.Max(0.02f, wall), arm, 1f - CoverPh(v, 0.3f, 0.45f), 1f - Mathf.Clamp01(fx.Radius), 0f, 1f - CoverPh(v, 0f, 0.1f), 0, 0f);
            if (fxOn && v > 0.2f)
                Arc(fx.Pos, 20f + 30f * CoverEo(CoverPh(v, 0.2f, 0.45f)), 2f, CoverDust.WithAlpha(0.4f * (1f - CoverPh(v, 0.2f, 0.45f))), 28);
        }
    }
}

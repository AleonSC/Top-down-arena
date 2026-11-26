using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Decoy Dummy (the Healer Soldier's 4th choice, Core/DecoyDummy.cs): an inflatable soldier, drawn from the replicated zone
    /// (`ZoneKind.DecoyDummy`: Pos, Aim and Marched its throw, T and Duration its age, GuardFrac its health left):
    ///   the throw: a rolled-up canister tumbles out from where it was thrown over its shadow (0 - DecoyFly s);
    ///   the puff-up: it lands with a dust ring and puffs up with an overshoot and a few wisps of air (DecoyInflate s);
    ///   standing: a beige vinyl soldier seen from above at a slant (shadow, stubby arms, round body with a seam, a red cross,
    ///     a round head with a painted grin and a green helmet band, a valve and a shine), bobbing gently; a thin dashed green
    ///     ring shows how far it pulls, brighter for your own; under half health it sags and sways; a health bar over it;
    ///   FxKind.DecoyHit: it wobbles and squashes for a moment, and a small white puff where the shot landed;
    ///   FxKind.DecoyPop: a flash, a gold ring out to the blast's reach, confetti flung out and fluttering down, and the empty
    ///     skin zipping off in a squiggle before it lands flat and fades;
    ///   FxKind.DecoyFold: it sags flat into a puddle of vinyl with a hiss of air, then fades.
    /// Visual effects off: the dummy and its bar only (no throw, puffs, wobble or confetti).
    public partial class ArenaApp
    {
        static readonly Rgba DecoyVinyl = new Rgba(0.945f, 0.85f, 0.71f), DecoyVinylDark = new Rgba(0.79f, 0.66f, 0.51f), DecoyShine = new Rgba(1f, 0.965f, 0.9f);
        static readonly Rgba DecoyCross = new Rgba(0.88f, 0.29f, 0.29f), DecoyHelmet = new Rgba(0.36f, 0.48f, 0.29f), DecoyFace = new Rgba(0.16f, 0.12f, 0.09f);
        static readonly Rgba DecoyPull = new Rgba(0.35f, 0.95f, 0.5f), DecoyGold = Rgba.Gold;
        static readonly Rgba[] DecoyConfetti =
        {
            new Rgba(0.95f, 0.33f, 0.36f), Rgba.Gold, new Rgba(0.35f, 0.95f, 0.5f), new Rgba(0.4f, 0.64f, 1f), new Rgba(1f, 0.55f, 0.85f),
        };

        readonly HashSet<int> decoyHeard = new HashSet<int>();  // dummies whose throw sound has played (by zone id)
        readonly List<int> decoyHeardGone = new List<int>();
        readonly Dictionary<int, float> decoyGhost = new Dictionary<int, float>();  // the health bar's pale drain behind the real one

        static float DecoyHash(float n) => Mathf.Repeat(Mathf.Sin(n * 91.7f + 47.3f) * 43758.5453f, 1f);
        static float DecoyEo(float x) { x = Mathf.Clamp01(x); return 1f - (1f - x) * (1f - x) * (1f - x); }

        /// The dummy itself at `c` (its middle). s its size, sx/sy a squash, sway its head leaning sideways, alpha, flat 0-1 how
        /// far it has sagged flat (deflating).
        void DrawDecoyBody(V2 c, float s, float sx, float sy, float sway, float alpha, float flat)
        {
            if (alpha <= 0.01f || s <= 0.01f)
                return;
            float up = 1f - flat;
            Ellipse(c + new V2(3f, 16f * s), 16f * s * (1f + flat * 0.5f), 5f * s, new Rgba(0f, 0f, 0f, 0.4f * alpha));  // shadow
            // stubby arms
            Ellipse(c + new V2(-21f * s * sx, 0f), 9f * s, 7f * s * up + 2f, 0.2f, DecoyVinylDark.WithAlpha(alpha), 12);
            Ellipse(c + new V2(21f * s * sx, 0f), 9f * s, 7f * s * up + 2f, -0.2f, DecoyVinylDark.WithAlpha(alpha), 12);
            // body: a rim, the vinyl, a lit side
            V2 body = c + new V2(0f, 2f * s + 10f * s * flat);
            float brx = 15f * s * sx * (1f + flat * 0.7f), bry = 17f * s * sy * (up * 0.85f + 0.15f);
            Ellipse(body, brx + 1.2f, bry + 1.2f, 0f, DecoyVinylDark.WithAlpha(alpha), 20);
            Ellipse(body, brx, bry, 0f, DecoyVinyl.WithAlpha(alpha), 20);
            Ellipse(body + new V2(-brx * 0.25f, -bry * 0.2f), brx * 0.6f, bry * 0.65f, 0f, DecoyShine.WithAlpha(0.45f * alpha), 16);
            Line(body + new V2(0f, -bry + 2f), body + new V2(0f, bry - 1f), 1f, DecoyVinylDark.WithAlpha(0.8f * alpha));  // the seam
            // the red cross on its chest
            V2 x = body + new V2(0f, bry * 0.45f);
            float cw = 2.5f * s * sx, ch = 6f * s * sy * up + 1f;
            Quad(x + new V2(-cw, -ch), x + new V2(cw, -ch), x + new V2(cw, ch), x + new V2(-cw, ch), DecoyCross.WithAlpha(alpha));
            Quad(x + new V2(-ch, -cw), x + new V2(ch, -cw), x + new V2(ch, cw), x + new V2(-ch, cw), DecoyCross.WithAlpha(alpha));
            Circle(body + new V2(brx * 0.8f, bry * 0.3f), 1.6f * s, DecoyVinylDark.WithAlpha(alpha), 6);  // the valve
            if (flat > 0.85f)
                return;  // (flat: no head left to see)
            // head, helmet band and face
            V2 head = c + new V2(sway * 4f, -13f * s * sy * up + 6f * flat);
            float hr = 9f * s;
            Circle(head, hr + 1f, DecoyVinylDark.WithAlpha(alpha), 16);
            Circle(head, hr, DecoyVinyl.WithAlpha(alpha), 16);
            Circle(head + new V2(-2.5f * s, -3f * s), hr * 0.5f, DecoyShine.WithAlpha(0.5f * alpha), 10);
            Arc(head + new V2(0f, -1.5f * s), hr - 1.5f * s, 3.4f * s, DecoyHelmet.WithAlpha(alpha), 12, MathX.Pi * 1.05f, MathX.Pi * 0.9f);
            Circle(head + new V2(-3.5f * s, 0.5f * s), 1.1f * s, DecoyFace.WithAlpha(alpha), 5);
            Circle(head + new V2(3.5f * s, 0.5f * s), 1.1f * s, DecoyFace.WithAlpha(alpha), 5);
            Arc(head + new V2(0f, 2f * s), 3.4f * s, 1.1f, DecoyFace.WithAlpha(alpha), 8, 0.3f, MathX.Pi - 0.6f);  // the painted grin
        }

        /// Every frame: a throw sound the first time a young dummy is seen, and a squeak, pop or deflate for each FxKind.DecoyHit,
        /// DecoyPop and DecoyFold (DecoyDummySoundBank.cs); a pop near you kicks the camera a little.
        void UpdateDecoySound()
        {
            float now = Time.unscaledTime;
            decoyHeardGone.Clear();
            foreach (int id in decoyHeard)
                if (!game.Zones.ContainsKey(id))
                    decoyHeardGone.Add(id);
            foreach (int id in decoyHeardGone)
            {
                decoyHeard.Remove(id);
                decoyGhost.Remove(id);
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.DecoyDummy || !decoyHeard.Add(z.Id) || Game.DecoyAge(z) > 0.3f)
                    continue;
                bool mine = z.Owner == game.MyId;
                sounds?.Play(new SoundCue { Sfx = Sfx.DecoyThrow, Ally = !mine, Quiet = mine ? 0f : game.QuietAt(z.Pos) }, now);
            }
            foreach (var fx in game.Effects)
            {
                if (fx.Heard || fx.Kind != FxKind.DecoyHit && fx.Kind != FxKind.DecoyPop && fx.Kind != FxKind.DecoyFold)
                    continue;
                fx.Heard = true;
                if (fx.T >= 0.3f)
                    continue;  // (an old one, seen late)
                var sfx = fx.Kind == FxKind.DecoyPop ? Sfx.DecoyPop : fx.Kind == FxKind.DecoyFold ? Sfx.DecoyFold : Sfx.DecoySqueak;
                sounds?.Play(new SoundCue { Sfx = sfx, Quiet = game.QuietAt(fx.Pos) }, now);
                Player me;
                if (fx.Kind == FxKind.DecoyPop && fxOn && game.Players.TryGetValue(game.MyId, out me) && me.Pos.DistanceTo(fx.Pos) < 400f)
                    Shake(0.1f);
            }
        }

        /// A standing (or flying, or puffing up) dummy, from the zone layer (DrawZone).
        void DrawDecoy(Zone z, float t)
        {
            if (z.T <= 0f)
                return;
            float u = Game.DecoyAge(z), frac = Mathf.Clamp01(game.DecoyFrac(z));
            V2 fwd = V2.FromAngle(z.Aim);
            if (!fxOn)
            {
                if (u >= Game.DecoyFly)
                {
                    DrawDecoyBody(z.Pos, 1f, 1f, frac < 0.5f ? 0.9f : 1f, 0f, 1f, 0f);
                    DrawDecoyBar(z, 1f, frac);
                }
                return;
            }
            if (u < Game.DecoyFly)  // the canister in flight
            {
                float f = u / Game.DecoyFly;
                V2 from = z.Pos - fwd * z.Marched, g = V2.Lerp(from, z.Pos, f);
                float h = Mathf.Sin(Mathf.PI * f) * Mathf.Min(50f, 18f + z.Marched * 0.15f);
                Ellipse(g + new V2(0f, 4f), 7f, 3f, new Rgba(0f, 0f, 0f, 0.4f));
                V2 m = g - new V2(0f, h), ax = V2.FromAngle(f * 11f) * 7f, ay = V2.FromAngle(f * 11f + MathX.Pi * 0.5f) * 4f;
                Quad(m - ax - ay, m + ax - ay, m + ax + ay, m - ax + ay, DecoyVinylDark);
                Quad(m - ax * 0.2f - ay, m + ax * 0.2f - ay, m + ax * 0.2f + ay, m - ax * 0.2f + ay, DecoyCross);
                return;
            }
            float v = u - Game.DecoyFly, inf = Mathf.Clamp01(v / Game.DecoyInflate);
            if (v < 0.4f)  // the landing: a dust ring
            {
                float k = v / 0.4f;
                Arc(z.Pos + new V2(0f, 10f), 10f + 26f * DecoyEo(k), 3f * (1f - k) + 1f, new Rgba(0.75f, 0.72f, 0.66f, 0.5f * (1f - k)), 24);
            }
            if (inf < 1f && !fxReduced)  // wisps of air as it puffs up
                for (int q = 0; q < 5; q++)
                {
                    float a = DecoyHash(z.Id * 7 + q) * MathX.Tau, r = 10f + 26f * inf;
                    Circle(z.Pos + V2.FromAngle(a) * r, 2.5f + 3f * inf, Rgba.White.WithAlpha(0.35f * (1f - inf)), 8);
                }
            // the last hit: a wobble and a squash
            float hitAge = 9f;
            V2 key = z.Pos + fwd;
            foreach (var fx in FxOf(FxKind.DecoyHit))
                if (fx.Kind == FxKind.DecoyHit && fx.T < hitAge && fx.Pos2.DistanceSq(key) < 1f)
                    hitAge = fx.T;
            float wob = hitAge < 0.3f ? 1f - hitAge / 0.3f : 0f, wave = Mathf.Sin(hitAge * 45f) * wob;
            float pulse = inf < 1f ? 1f + Mathf.Sin(inf * MathX.Pi * 3f) * 0.12f * (1f - inf) : 1f;
            float sag = frac < 0.5f ? 0.88f + Mathf.Sin(t * 9f + z.Id) * 0.03f : 1f;
            float s = (0.3f + 0.7f * DecoyEo(inf)) * pulse;
            float bob = Mathf.Sin(t * 2.4f + z.Id) * 0.025f;
            float sway = (frac < 0.5f ? Mathf.Sin(t * 3f + z.Id) * 0.5f : 0f) + wave * 0.8f;
            if (inf >= 1f)  // the pull ring: dashes turning slowly, brighter for your own
            {
                float a = z.Owner == game.MyId ? 0.22f : 0.11f;
                const int dashes = 36;
                for (int d = 0; d < dashes; d++)
                    Arc(z.Pos, Game.DecoyPull, 1.5f, DecoyPull.WithAlpha(a), 3, t * 0.08f + d * MathX.Tau / dashes, MathX.Tau / dashes * 0.45f);
            }
            DrawDecoyBody(z.Pos, s, 1f + wave * 0.1f + bob, sag * (1f - wave * 0.08f - bob), sway, 1f, 0f);
            DrawDecoyBar(z, inf, frac);
        }

        /// The health bar over a dummy: green, amber under half, red under a quarter, with a pale drain behind it.
        void DrawDecoyBar(Zone z, float a, float frac)
        {
            if (a <= 0f)
                return;
            float ghost;
            if (!decoyGhost.TryGetValue(z.Id, out ghost) || ghost < frac)
                ghost = frac;
            ghost = Mathf.Max(frac, ghost - Time.unscaledDeltaTime * 0.8f);
            decoyGhost[z.Id] = ghost;
            const float w = 30f, h = 4f;
            V2 at = z.Pos + new V2(-w * 0.5f, -36f);
            FillRect(at.X - 1.5f, at.Y - 1.5f, w + 3f, h + 3f, new Rgba(0f, 0f, 0f, 0.65f * a));
            FillRect(at.X, at.Y, w * ghost, h, new Rgba(1f, 0.93f, 0.78f, 0.5f * a));
            var col = frac > 0.5f ? DecoyPull : frac > 0.25f ? new Rgba(0.88f, 0.65f, 0.25f) : new Rgba(0.88f, 0.33f, 0.25f);
            FillRect(at.X, at.Y, w * frac, h, col.WithAlpha(a));
            FillRect(at.X, at.Y, w * frac, 1.2f, Rgba.White.WithAlpha(0.4f * a));
        }

        /// FxKind.DecoyHit: a small white puff where the shot or bite landed (the wobble is drawn with the dummy).
        void DrawDecoyHitFx(Effect fx)
        {
            if (!fxOn)
                return;
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            Circle(fx.Pos, 3f + 7f * DecoyEo(f), Rgba.White.WithAlpha(0.7f * (1f - f)), 10);
        }

        /// FxKind.DecoyPop: the flash, the blast ring, confetti, and the empty skin zipping off and landing flat.
        void DrawDecoyPopFx(Effect fx)
        {
            float v = fx.T, aim = (fx.Pos2 - fx.Pos).Angle;
            V2 c = fx.Pos;
            int seed = Mathf.RoundToInt(c.X * 3.1f + c.Y * 1.7f);
            // the skin: zips off in a squiggle (0 - 0.7 s), then lies flat and fades
            float zipA = aim + MathX.Pi + (DecoyHash(seed) - 0.5f) * 2f, zip = Mathf.Min(1f, v / 0.7f);
            if (v < 0.7f && fxOn)
            {
                V2 prev = DecoySkinAt(c, zipA, seed, 0f);
                for (int i = 1; i <= 16; i++)
                {
                    V2 next = DecoySkinAt(c, zipA, seed, zip * i / 16f);
                    Line(prev, next, 2f, DecoyVinyl.WithAlpha(0.6f * i / 16f));
                    prev = next;
                }
                Ellipse(prev, 9f * (1f - zip * 0.4f), 3.5f, v * 30f, DecoyVinyl, 10);
            }
            else
            {
                float al = 1f - Mathf.Clamp01((v - 0.7f) / Mathf.Max(0.1f, fx.Dur - 0.7f));
                V2 at = fxOn ? DecoySkinAt(c, zipA, seed, 1f) : c;
                Ellipse(at, 11f, 4.5f, zipA, DecoyVinylDark.WithAlpha(al), 12);
                Ellipse(at, 9f, 3.2f, zipA, DecoyVinyl.WithAlpha(al), 12);
                Circle(at + V2.FromAngle(zipA) * 5f, 1.6f, DecoyCross.WithAlpha(al), 5);
            }
            if (!fxOn)
                return;
            if (v < 0.12f)
                GlowDisc(c, 60f * (1f - v / 0.12f) + 12f, new Rgba(1f, 0.95f, 0.8f), 0.75f);
            if (v < 0.35f)  // the blast's reach
            {
                float k = v / 0.35f;
                Arc(c, fx.Radius * DecoyEo(k), 3f * (1f - k) + 1f, DecoyGold.WithAlpha(0.85f * (1f - k)), 40);
            }
            int pieces = fxReduced ? 14 : 28;
            for (int i = 0; i < pieces; i++)  // confetti: flung out, then fluttering down
            {
                float h = DecoyHash(seed * 13 + i), h2 = DecoyHash(seed * 7 + i + 50);
                float a = h * MathX.Tau, reach = fx.Radius * (0.4f + 0.7f * h2);
                float out_ = 1f - Mathf.Exp(-6f * v);
                V2 p = c + V2.FromAngle(a) * (reach * out_) + new V2(Mathf.Sin(v * 9f + i) * 4f, 34f * v * v - 18f * out_);
                float al = 1f - Mathf.Clamp01((v - 0.6f) / 0.5f);
                Ellipse(p, 3.4f, 1.6f * Mathf.Abs(Mathf.Cos(v * 12f + i)) + 0.4f, v * 8f + i, DecoyConfetti[i % DecoyConfetti.Length].WithAlpha(al), 6);
            }
        }

        /// Where the popped skin is along its zip (k 0-1): out from c along zipA, squiggling side to side.
        static V2 DecoySkinAt(V2 c, float zipA, int seed, float k) =>
            c + V2.FromAngle(zipA) * (110f * DecoyEo(k)) + V2.FromAngle(zipA + MathX.Pi * 0.5f) * (Mathf.Sin(k * 17f + seed) * 16f * (1f - k));

        /// FxKind.DecoyFold: it sags flat into a puddle with a hiss of air, then fades.
        void DrawDecoyFoldFx(Effect fx)
        {
            float v = fx.T, k = DecoyEo(Mathf.Clamp01(v / 0.4f));
            float al = 1f - Mathf.Clamp01((v - 0.35f) / Mathf.Max(0.05f, fx.Dur - 0.35f));
            DrawDecoyBody(fx.Pos, 1f, 1f, 1f, fxOn ? Mathf.Sin(v * 30f) * 0.5f * (1f - k) : 0f, al, k);
            if (fxOn && !fxReduced && v < 0.45f)
                for (int q = 0; q < 4; q++)  // air hissing out
                {
                    float a = -MathX.Pi * 0.5f + (DecoyHash(q + fx.Pos.X) - 0.5f) * 1.6f;
                    Circle(fx.Pos + V2.FromAngle(a) * (8f + 34f * v / 0.45f), 2f + 4f * v, Rgba.White.WithAlpha(0.3f * (1f - v / 0.45f)), 8);
                }
        }
    }
}

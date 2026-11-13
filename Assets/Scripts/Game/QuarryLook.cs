using UnityEngine;

namespace Arena
{
    /// The Colossus's faction, The Quarry (Core/QuarryFaction.cs), drawn as in `Docs/Mockups/colossus-faction.html`:
    ///   Rubble (Rioter): a jagged chunk of stone that rolls as it moves, a magma vein, two gold eyes.
    ///   Quarryman (Guard): dusty overalls with hi-vis straps, an ochre hard hat, a headlamp beam ahead, a pickaxe that
    ///     swings across its front when it's close.
    ///   Stomper (Thug): a big slab-shouldered rock with an ember slit; as its foot lifts (StomperLift) it rises, the foot
    ///     swells and its veins glow; its ring is the Colossus's (DrawStompRing, ColossusLook.cs), from DrawQuarryFloor.
    public partial class ArenaApp
    {
        static readonly Rgba QuarryInk = new Rgba(0.11f, 0.085f, 0.063f, 0.95f), QuarryVein = new Rgba(1f, 0.48f, 0.23f),
            QuarryGold = new Rgba(1f, 0.8f, 0.35f), QuarryHat = new Rgba(0.93f, 0.66f, 0.2f), QuarryHatDark = new Rgba(0.45f, 0.29f, 0.05f),
            QuarryVest = new Rgba(0.94f, 0.54f, 0.17f), QuarrySteel = new Rgba(0.73f, 0.75f, 0.77f), QuarryHaft = new Rgba(0.42f, 0.29f, 0.17f);

        /// A rock's outline: `n` points round `c`, each pushed out by a fixed jitter from `seed`, turned by `rot`.
        V2 RockPt(V2 c, float r, int n, int i, float rot, int seed)
        {
            float j = 0.74f + 0.36f * OrbHash(seed * 13 + i * 7);
            return c + V2.FromAngle(rot + MathX.Tau * i / n) * (r * j);
        }

        /// A filled rock: an outline in ink, the fill a shade inside it, a lighter cap toward the light (up-left).
        void Rock(V2 c, float r, int n, float rot, int seed, Rgba fill, float edge)
        {
            SetColor(QuarryInk);
            for (int i = 0; i < n; i++)
                Tri(c, RockPt(c, r + edge, n, i, rot, seed), RockPt(c, r + edge, n, i + 1, rot, seed));
            SetColor(fill);
            for (int i = 0; i < n; i++)
                Tri(c, RockPt(c, r, n, i, rot, seed), RockPt(c, r, n, i + 1, rot, seed));
            Circle(c + new V2(-r * 0.3f, -r * 0.32f), r * 0.3f, fill.Mix(Rgba.White, 0.18f).WithAlpha(fill.A * 0.55f), 10);
        }

        void DrawQuarryBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            switch (e.Type)
            {
                case EnemyType.Rubble: DrawRubble(e, pos, r, col, t); break;
                case EnemyType.Quarryman: DrawQuarryman(e, pos, r, col, t); break;
                case EnemyType.Stomper: DrawStomper(e, pos, r, col, t); break;
                case EnemyType.SlabBearer: DrawSlabBearer(e, pos, r, col, t); break;
                case EnemyType.RockHurler: DrawRockHurler(e, pos, r, col, t); break;
                case EnemyType.Driller: DrawDriller(e, pos, r, col, t); break;
                case EnemyType.Geomancer: DrawGeomancer(e, pos, r, col, t); break;
                case EnemyType.CinderPriest: DrawCinderPriest(e, pos, r, col, t); break;
            }
        }

        /// A crew body: dusty overalls with two hi-vis straps and a hard hat (the Quarryman's, without its lamp).
        void QuarryCrew(V2 pos, float r, float face, Rgba col, bool lamp)
        {
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            Ellipse(pos, r * 0.95f + 1.8f, r * 1.2f + 1.8f, face, QuarryInk, 16);
            Ellipse(pos, r * 0.95f, r * 1.2f, face, col, 16);
            for (int k = -1; k <= 1; k += 2)
                Line(pos - f * r * 0.85f + s * k * r * 0.62f, pos + f * r * 0.85f + s * k * r * 0.62f, 2.6f, QuarryVest);
            Circle(pos + f * r * 0.1f, r * 0.55f, QuarryHatDark, 14);
            Circle(pos + f * r * 0.1f, r * 0.47f, QuarryHat, 14);
            Line(pos - f * r * 0.3f, pos + f * r * 0.5f, 1.2f, QuarryHatDark.WithAlpha(0.6f));
            if (lamp)
                Circle(pos + f * r * 0.62f, 2.2f, new Rgba(1f, 0.97f, 0.78f), 8);
        }

        /// A stone slab standing across `f` at `c`: ink edge, a lit face, a highlight, `cracks` crack lines (0-3), a white flash.
        void QuarrySlab(V2 c, V2 f, float halfW, float halfL, int cracks, float flash, float a)
        {
            V2 s = f.Orthogonal();
            Quad(c - f * (halfW + 1.5f) - s * (halfL + 1.5f), c + f * (halfW + 1.5f) - s * (halfL + 1.5f), c + f * (halfW + 1.5f) + s * (halfL + 1.5f), c - f * (halfW + 1.5f) + s * (halfL + 1.5f), QuarryInk.WithAlpha(0.95f * a));
            Quad(c - f * halfW - s * halfL, c + f * halfW - s * halfL, c + f * halfW + s * halfL, c - f * halfW + s * halfL, new Rgba(0.6f, 0.51f, 0.42f, a));
            Line(c + f * (halfW - 1.5f) - s * (halfL - 1f), c + f * (halfW - 1.5f) + s * (halfL - 1f), 1.5f, new Rgba(1f, 1f, 1f, 0.14f * a));
            var ink = QuarryInk.WithAlpha(0.9f * a);
            if (cracks >= 1) { Line(c - s * halfL * 0.6f - f * halfW * 0.6f, c - s * halfL * 0.35f + f * halfW * 0.4f, 1.3f, ink); Line(c - s * halfL * 0.35f + f * halfW * 0.4f, c - s * halfL * 0.1f - f * halfW * 0.2f, 1.3f, ink); }
            if (cracks >= 2) { Line(c + s * halfL * 0.7f + f * halfW * 0.5f, c + s * halfL * 0.4f - f * halfW * 0.5f, 1.3f, ink); Line(c + s * halfL * 0.4f - f * halfW * 0.5f, c + s * halfL * 0.15f + f * halfW * 0.3f, 1.3f, ink); }
            if (cracks >= 3) { Line(c - f * halfW * 0.7f + s * halfL * 0.05f, c + f * halfW * 0.6f + s * halfL * 0.2f, 1.3f, ink); Line(c + f * halfW * 0.6f + s * halfL * 0.2f, c - f * halfW * 0.2f + s * halfL * 0.45f, 1.3f, ink); }
            if (flash > 0f)
                Quad(c - f * halfW - s * halfL, c + f * halfW - s * halfL, c + f * halfW + s * halfL, c - f * halfW + s * halfL, new Rgba(1f, 1f, 1f, 0.8f * flash * a));
        }

        /// The Slab Bearer (a Riot Shield): its arms reach to a slab held in front; it flashes before the bash and drops
        /// low after it. Broken, the slab is gone (it lies as cover, DrawQuarryPatch) and the bearer wobbles while it staggers.
        void DrawSlabBearer(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int crack = Game.RiotCrack(e);
            byte st = Game.RiotState(e);
            bool broken = crack >= Game.RiotCracked;
            float face = e.Facing + (broken && st == Game.RiotStaggered ? Mathf.Sin(t * 30f) * 0.18f : 0f);
            V2 f = V2.FromAngle(face), s = f.Orthogonal();
            if (!broken)
                for (int k = -1; k <= 1; k += 2)
                    Line(pos + f * r * 0.35f + s * k * r * 0.6f, pos + f * r * 0.95f + s * k * r * 0.75f, 3f, new Rgba(0.18f, 0.16f, 0.14f));
            QuarryCrew(pos, r * 0.9f, face, col, false);
            if (broken)
                return;
            float low = st == Game.RiotLow ? 0.7f : 1f;  // held low after a bash: front hits land
            float flash = st == Game.RiotWinding ? (Mathf.Sin(t * 50f) > 0f ? 1f : 0.3f) : 0f;
            QuarrySlab(pos + f * (r * 1.15f), f, r * 0.26f, r * 1.3f * low, crack, flash, low < 1f ? 0.75f : 1f);
        }

        /// The Rock Hurler (a Gas Grenadier): a rock body with an arm that hauls a boulder back over the wind-up.
        void DrawRockHurler(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float back = 0f;
            bool hold = true;
            if (GasLobOf(e, out float age, out float throwAt, out float flight))
            {
                back = age < throwAt ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : 0f;
                hold = age < throwAt || (age - throwAt) / Mathf.Max(0.01f, flight) > 0.55f;  // a new boulder once the last has landed
            }
            Rock(pos, r * 0.95f, 10, e.Facing, 3, col.Mix(new Rgba(0.29f, 0.24f, 0.2f), 0.3f), 2.2f);
            Line(pos - f * r * 0.4f - s * r * 0.3f, pos + s * r * 0.15f, 1.6f, QuarryVein.WithAlpha(0.8f));
            Line(pos + s * r * 0.15f, pos - f * r * 0.25f + s * r * 0.5f, 1.6f, QuarryVein.WithAlpha(0.8f));
            for (int k = -1; k <= 1; k += 2)
                Circle(pos + f * r * 0.6f + s * k * r * 0.22f, 1.8f, QuarryGold, 6);
            if (!hold)
                return;
            V2 shoulder = pos - s * r * 0.6f, hand = pos + f * Mathf.Lerp(r * 0.3f, -r * 1.1f, back) - s * Mathf.Lerp(r * 1f, r * 0.35f, back);
            Line(shoulder, hand, 4f, new Rgba(0.29f, 0.24f, 0.2f));
            Rock(hand, r * 0.6f, 8, t * 0.5f * back, 5, new Rgba(0.6f, 0.52f, 0.43f), 1.6f);
        }

        /// A Rock Hurler's lob (GasLobLook.cs): an orange ring on where you were heading that fills over the wind-up, the
        /// boulder on an arc with its shadow growing under it.
        void DrawHurlerLobFx(Enemy e, Effect fx, float age, float throwAt, float flight, V2 land, float R, float t)
        {
            var orange = Game.HurlerColor;
            float tellP = throwAt > 0f ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt)) : 1f;
            if (age < throwAt + flight)
            {
                Circle(land, R, orange.WithAlpha(0.07f), 32);
                Arc(land, R, 2f, orange.WithAlpha(0.85f), 32);
                Arc(land, R - 3f, 4f, orange, Mathf.Max(3, (int)(32 * tellP)), -MathX.Pi / 2f, MathX.Tau * tellP);
            }
            if (age >= throwAt && age < throwAt + flight)
            {
                float u = (age - throwAt) / flight;
                V2 from = e.Disp + V2.FromAngle(e.Facing) * e.Radius;
                V2 ground = V2.Lerp(from, land, u);
                float h = 90f * Mathf.Sin(Mathf.PI * u);
                Ellipse(ground + new V2(0f, 3f), 7f + 4f * u, 5f + 3f * u, 0f, new Rgba(0f, 0f, 0f, 0.35f), 12);
                Rock(ground - new V2(0f, h), 9f * (1f + 0.35f * Mathf.Sin(Mathf.PI * u)), 8, u * 8f, 6, new Rgba(0.6f, 0.52f, 0.43f), 1.6f);
            }
        }

        /// Quarry floor patches (PathsUi.cs DrawPatch): a rubble pile once the boulder has landed, a dropped slab; both fade out.
        void DrawQuarryPatch(PathPatch pt, float t)
        {
            float a = Mathf.Clamp01((pt.Life - pt.Age) / 0.4f);
            if (pt.Kind == PatchKind.Slab)
            {
                V2 f = V2.FromAngle(pt.R1);
                Quad(pt.Pos - f * 10f - f.Orthogonal() * 28f + new V2(3f, 5f), pt.Pos + f * 10f - f.Orthogonal() * 28f + new V2(3f, 5f), pt.Pos + f * 10f + f.Orthogonal() * 28f + new V2(3f, 5f), pt.Pos - f * 10f + f.Orthogonal() * 28f + new V2(3f, 5f), new Rgba(0f, 0f, 0f, 0.35f * a));
                QuarrySlab(pt.Pos, f, Game.SlabCoverHalfW, Game.SlabCoverHalfL, 3, 0f, a);
                return;
            }
            if (!pt.Active)
                return;  // (the boulder is still in the air: GasLob)
            float grow = Mathf.Clamp01((pt.Age - pt.Warn) / 0.12f), r = pt.R0 * grow;
            Circle(pt.Pos + new V2(2f, 4f), r, new Rgba(0f, 0f, 0f, 0.3f * a), 20);
            var stone = new Rgba(0.58f, 0.5f, 0.41f, a);
            Rock(pt.Pos + new V2(-r * 0.35f, r * 0.2f), r * 0.42f, 7, pt.Id, 1, stone, 1.5f);
            Rock(pt.Pos + new V2(r * 0.38f, r * 0.25f), r * 0.38f, 7, pt.Id + 1f, 2, stone, 1.5f);
            Rock(pt.Pos + new V2(r * 0.05f, -r * 0.3f), r * 0.36f, 7, pt.Id + 2f, 3, stone, 1.5f);
            Rock(pt.Pos, r * 0.5f, 8, pt.Id + 3f, 0, stone.Mix(Rgba.White, 0.06f), 1.8f);
        }

        void DrawRubble(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float roll = (pos.X + pos.Y * 0.7f) / r;  // it rolls as it moves
            Rock(pos, r, 9, roll, e.Id % 4, col.Mix(new Rgba(0.29f, 0.24f, 0.2f), 0.35f), 2f);
            V2 a = pos + V2.FromAngle(roll + 2.4f) * r * 0.5f, b = pos + V2.FromAngle(roll + 3.3f) * r * 0.1f, c = pos + V2.FromAngle(roll + 4.2f) * r * 0.45f;
            Line(a, b, 1.6f, QuarryVein.WithAlpha(0.85f));
            Line(b, c, 1.6f, QuarryVein.WithAlpha(0.85f));
            for (int k = -1; k <= 1; k += 2)  // eyes on its target (they don't roll)
                Circle(pos + V2.FromAngle(e.Facing + k * 0.5f) * r * 0.45f, 1.8f, QuarryGold, 6);
        }

        void DrawQuarryman(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            // the headlamp's beam, faint, ahead of it
            SetColor(new Rgba(1f, 0.94f, 0.67f, 0.09f));
            Tri(pos + f * r * 0.8f, pos + f * r * 7f - s * r * 2.2f, pos + f * r * 7f + s * r * 2.2f);
            // the pickaxe: over its shoulder, swinging across its front when you're close
            float near = HiveNearestPlayer(pos), swing = near < r * 3.2f ? 0.5f + 0.5f * Mathf.Sin(t * 9f) : 0f;
            float ha = e.Facing + Mathf.Lerp(0.15f, -1.9f, swing);
            V2 grip = pos + s * r * 0.7f, head = grip + V2.FromAngle(ha) * r * 1.45f, hs = V2.FromAngle(ha).Orthogonal();
            Line(grip, head, 3f, QuarryHaft);
            Line(head - hs * r * 0.6f - V2.FromAngle(ha) * 2f, head, 3f, QuarrySteel);
            Line(head, head + hs * r * 0.6f - V2.FromAngle(ha) * 2f, 3f, QuarrySteel);
            // body: dusty overalls with two hi-vis straps
            Ellipse(pos, r * 0.95f + 1.8f, r * 1.2f + 1.8f, e.Facing, QuarryInk, 16);
            Ellipse(pos, r * 0.95f, r * 1.2f, e.Facing, col, 16);
            for (int k = -1; k <= 1; k += 2)
                Line(pos - f * r * 0.85f + s * k * r * 0.62f, pos + f * r * 0.85f + s * k * r * 0.62f, 2.6f, QuarryVest);
            // hard hat with a brim line and the lamp
            Circle(pos + f * r * 0.1f, r * 0.55f, QuarryHatDark, 14);
            Circle(pos + f * r * 0.1f, r * 0.47f, QuarryHat, 14);
            Line(pos - f * r * 0.3f, pos + f * r * 0.5f, 1.2f, QuarryHatDark.WithAlpha(0.6f));
            Circle(pos + f * r * 0.62f, 2.2f, new Rgba(1f, 0.97f, 0.78f), 8);
        }

        void DrawStomper(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            float lift = Game.StomperLift(e);
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal(), at = pos - new V2(0f, lift * 7f);  // it rises as its foot goes up
            var dark = new Rgba(0.29f, 0.24f, 0.2f);
            // feet: the back one planted, the front one lifting (it swells as it rises)
            Ellipse(at - f * r * 0.15f + s * r * 0.78f, r * 0.42f, r * 0.3f, e.Facing, dark, 10);
            float fs = 1f + lift * 0.3f;
            Ellipse(at + f * (r * 0.55f + lift * 6f) - s * r * 0.72f, r * 0.42f * fs, r * 0.3f * fs, e.Facing, dark.Mix(col, 0.25f), 10);
            Rock(at, r * 0.92f, 9, e.Facing, 7, col.Mix(dark, 0.3f), 2.5f);
            // shoulder slabs
            for (int k = -1; k <= 1; k += 2)
            {
                V2 c = at - f * r * 0.05f + s * k * r * 0.82f;
                Quad(c - f * r * 0.36f - s * r * 0.18f, c + f * r * 0.3f - s * r * 0.18f, c + f * r * 0.3f + s * r * 0.18f, c - f * r * 0.36f + s * r * 0.18f, QuarryInk);
                Quad(c - f * r * 0.32f - s * r * 0.13f, c + f * r * 0.26f - s * r * 0.13f, c + f * r * 0.26f + s * r * 0.13f, c - f * r * 0.32f + s * r * 0.13f, col.Mix(Rgba.White, 0.08f));
            }
            // veins that glow as the foot rises
            var vein = QuarryVein.WithAlpha(0.45f + 0.55f * lift);
            Line(at - f * r * 0.5f - s * r * 0.25f, at - f * r * 0.15f, 1.6f + lift, vein);
            Line(at - f * r * 0.15f, at - f * r * 0.4f + s * r * 0.35f, 1.6f + lift, vein);
            Line(at + f * r * 0.1f - s * r * 0.45f, at + f * r * 0.28f - s * r * 0.15f, 1.6f + lift, vein);
            // head knob with an ember slit
            V2 head = at + f * r * 0.5f;
            Circle(head, r * 0.34f, QuarryInk, 12);
            Circle(head, r * 0.28f, col.Mix(dark, 0.45f), 12);
            Line(head + f * r * 0.12f - s * r * 0.15f, head + f * r * 0.12f + s * r * 0.15f, 2.2f, new Rgba(1f, 0.7f, 0.28f).Mix(Rgba.White, lift * 0.4f));
        }

        /// On the floor, under everyone: each Stomper's ring and each crack (the Colossus's looks, ColossusLook.cs).
        void DrawQuarryFloor(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (e.Stomp != null && e.Type == EnemyType.Stomper)
                    DrawStompRing(e.Stomp, t);
                if (e.Crack != null)
                    DrawCrack(e.Crack, t);
            }
        }

        /// The Driller (a Shiv): a crew body with goggles and a jackhammer held out front, its bit chattering while it
        /// winds up and drills.
        void DrawDriller(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            bool busy = e.Phase == 1 || e.Phase == 2;
            float jit = busy ? Mathf.Sin(t * 70f) * 1.2f : 0f;
            V2 a = pos + f * (r * 0.7f) + s * jit, b = pos + f * (r * 1.9f) + s * jit;
            Line(a, b, 7f, QuarryInk);
            Line(a, b, 5f, new Rgba(0.24f, 0.25f, 0.26f));
            Line(a + f * 3f - s * 4.5f, a + f * 3f + s * 4.5f, 3f, QuarryInk);  // the handles
            Line(a + f * 5f - s * 2.5f, a + f * 5f + s * 2.5f, 2.5f, QuarryVest);
            SetColor(QuarrySteel);
            Tri(b - s * 3f, b + s * 3f, b + f * (r * 0.7f));  // the bit
            QuarryCrew(pos, r, e.Facing, col, false);
            for (int k = -1; k <= 1; k += 2)  // goggles
                Circle(pos + f * r * 0.5f + s * k * r * 0.22f, 1.8f, new Rgba(0.62f, 0.9f, 1f), 6);
        }

        /// The Geomancer (moves like a Taser Guard): a hooded robe with a crystal staff that glows as it charges.
        void DrawGeomancer(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            float glow = e.Winding ? 0.6f + 0.4f * Mathf.Sin(t * 18f) : 0f;
            if (glow > 0f)
                Arc(pos, r + 9f, 1.4f, new Rgba(1f, 0.75f, 0.35f, 0.6f * glow), 24, t * 2f, MathX.Pi * 1.6f);
            Ellipse(pos, r * 0.95f + 1.8f, r * 1.15f + 1.8f, e.Facing, QuarryInk, 16);
            Ellipse(pos, r * 0.95f, r * 1.15f, e.Facing, col, 16);
            V2 grip = pos + s * r * 0.85f, tip = grip + f * r * 1.3f;
            Line(grip - f * r * 0.3f, tip, 2.6f, QuarryHaft);
            var crystal = glow > 0f ? new Rgba(1f, 0.88f, 0.63f) : new Rgba(0.83f, 0.6f, 0.29f);
            if (glow > 0f)
                Circle(tip, 6f + 4f * glow, new Rgba(1f, 0.75f, 0.35f, 0.3f * glow), 12);
            Quad(tip - s * 3.5f, tip + f * 4.5f, tip + s * 3.5f, tip - f * 3f, crystal);
            Circle(pos, r * 0.62f, new Rgba(0.11f, 0.09f, 0.06f), 14);  // the hood
            Circle(pos - f * r * 0.08f, r * 0.55f, col.Mix(Rgba.White, 0.08f), 14);
            Circle(pos + f * r * 0.22f, r * 0.3f, new Rgba(0.04f, 0.03f, 0.03f), 10);  // the dark under it
        }

        /// The Cinder Priest (a Taser Guard): a dark robe with an ember hem, cupped hands holding a molten core that swells as it charges.
        void DrawCinderPriest(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 f = V2.FromAngle(e.Facing), s = f.Orthogonal();
            Ellipse(pos, r * 0.95f + 1.8f, r * 1.15f + 1.8f, e.Facing, QuarryInk, 16);
            Ellipse(pos, r * 0.95f, r * 1.15f, e.Facing, col, 16);
            Arc(pos, r * 1.05f, 1.6f, QuarryVein.WithAlpha(0.8f), 14, e.Facing - 1.2f, 2.4f);  // the ember hem
            V2 core = pos + f * r * 1.15f;
            for (int k = -1; k <= 1; k += 2)
                Circle(pos + f * r * 0.9f + s * k * r * 0.4f, r * 0.24f, new Rgba(0.16f, 0.13f, 0.11f), 8);
            float ch = e.Winding ? 0.5f + 0.5f * Mathf.Sin(t * 14f) : 0f;
            float cr = 3.5f + 3f * ch;
            Circle(core, cr * 2.4f, new Rgba(1f, 0.45f, 0.15f, 0.18f + 0.25f * ch), 14);
            Circle(core, cr, ch > 0.85f ? new Rgba(1f, 0.95f, 0.8f) : new Rgba(1f, 0.6f, 0.29f), 12);
            Circle(pos, r * 0.62f, new Rgba(0.09f, 0.07f, 0.06f), 14);  // the hood, rimmed in ember
            Arc(pos, r * 0.62f, 1.4f, QuarryVein.WithAlpha(0.9f), 16);
            Circle(pos + f * r * 0.22f, r * 0.3f, new Rgba(0.03f, 0.02f, 0.02f), 10);
        }

        /// A Cinder Priest's magma bolt (WorldShots.cs): a molten ball with a glow and a dripping tail.
        void DrawMagmaBolt(Bullet b, float t)
        {
            V2 d = b.Vel.Normalized();
            for (int i = 1; i < 6; i++)
                Circle(b.Pos - d * (i * 7f) + d.Orthogonal() * (Mathf.Sin(t * 30f + i) * 2f), 4f - i * 0.6f, new Rgba(1f, 0.55f - i * 0.06f, 0.24f, 0.7f - i * 0.11f), 8);
            Circle(b.Pos, 12f, new Rgba(1f, 0.35f, 0.12f, 0.25f), 14);
            Circle(b.Pos, 7f, new Rgba(1f, 0.6f, 0.29f), 12);
            Circle(b.Pos, 3.5f, new Rgba(1f, 0.95f, 0.8f), 10);
        }
    }
}

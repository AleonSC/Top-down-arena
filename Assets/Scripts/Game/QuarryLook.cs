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
            }
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

        /// On the floor, under everyone: each Stomper's ring (the Colossus's look, ColossusLook.cs).
        void DrawQuarryFloor(float t)
        {
            foreach (var e in game.Enemies.Values)
                if (e.Stomp != null && e.Type == EnemyType.Stomper)
                    DrawStompRing(e.Stomp, t);
        }
    }
}

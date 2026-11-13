using UnityEngine;

namespace Arena
{
    /// The Twin Reapers' cult (Core/ReaperFaction.cs, Docs/Mockups/reaper-faction.html). Every member is drawn in its sworn
    /// side's colour (Dusk pink or Dawn blue, by its id), so a pack reads as the Reapers'. The Bound Wraiths' tether and a
    /// revive's ghost, the Acolyte's sickle, the Scythe Wraith's scythe, the Lamplighter's lantern and gold guide lines,
    /// the Mourner's two veils, the Gravedigger's shovel, the Shade's smoke and a Wisp's flame.
    public partial class ArenaApp
    {
        static readonly Rgba CultDusk = new Rgba(1f, 0.35f, 0.6f), CultDawn = new Rgba(0.45f, 0.55f, 1f), CultGold = new Rgba(1f, 0.8f, 0.45f),
            CultBone = new Rgba(0.91f, 0.89f, 0.95f), CultEarth = new Rgba(0.81f, 0.78f, 0.71f);

        /// Its sworn side's colour (a Bound pair always has one of each: their ids are consecutive).
        static Rgba CultTint(Enemy e) => (e.Id & 1) == 0 ? CultDusk : CultDawn;

        /// DrawEnemyUnderlay: the tether (drawn once per pair, from the lower id), a revive's ghost, the lantern's guide lines.
        void DrawCultUnder(Enemy e, V2 pos, float r, Rgba col, bool spawning, float t)
        {
            if (spawning)
                return;
            if (e.Type == EnemyType.BoundWraith)
            {
                Enemy o;
                if (e.Phase == Game.BoundChanneling)
                {
                    // Channelling its fallen partner back: a ghost ring with the time left filling round it, a thin thread to it.
                    V2 g = e.Waypoint;
                    float k = 1f - Mathf.Clamp01(e.PhaseT / Game.BoundRevive);
                    Rgba other = (e.Id & 1) == 0 ? CultDawn : CultDusk;  // the fallen one was sworn to the other side
                    Arc(g, r + 2f, 2f, other.WithAlpha(0.5f + 0.2f * Mathf.Sin(t * 10f)), 20);
                    Arc(g, r + 8f, 3f, col.WithAlpha(0.9f), 24, -MathX.Pi / 2f, MathX.Tau * k);
                    DashedLine(pos, g, 1.5f, col.WithAlpha(0.35f + 0.25f * Mathf.Sin(t * 12f)));
                    Circle(pos, r + 7f + 2f * Mathf.Sin(t * 9f), col.WithAlpha(0.18f), 20);
                }
                else if (e.LinkId != 0 && e.Id < e.LinkId && game.Enemies.TryGetValue(e.LinkId, out o) && o.Phase != Game.BoundChanneling && !game.IsSpawning(o))
                {
                    // The tether: Dusk at one end, Dawn at the other, a crackle along it.
                    V2 a = pos, b = o.Disp, d = b - a;
                    float len = Mathf.Max(1f, d.Length);
                    V2 n = (d / len).Orthogonal();
                    Rgba ca = CultTint(e), cb = CultTint(o);
                    Line(a, b, Game.BoundTetherWidth + 8f, new Rgba(0.8f, 0.65f, 1f, 0.12f));
                    const int segs = 8;
                    V2 prev = a;
                    for (int k = 1; k <= segs; k++)
                    {
                        float u = k / (float)segs, j = k < segs ? Mathf.Sin(t * 40f + k * 2f + e.Id) * 3f : 0f;
                        V2 at = V2.Lerp(a, b, u) + n * j;
                        Line(prev, at, Game.BoundTetherWidth * 0.6f, ca.Mix(cb, u - 0.5f / segs).WithAlpha(0.9f));
                        prev = at;
                    }
                }
            }
            else if (e.Type == EnemyType.Lamplighter && e.Winding)
            {
                // The next volley's lanes in gold, dashed: three lines at you, or the cross.
                bool cross = e.Phase == 1;
                int lanes = cross ? 4 : Game.LampLines;
                for (int i = 0; i < lanes; i++)
                {
                    float a = cross ? e.Facing + i * MathX.Pi / 2f : e.Facing + (i - (Game.LampLines - 1) / 2f) * Game.LampSpread;
                    DashedLine(pos + V2.FromAngle(a) * (r + 6f), pos + V2.FromAngle(a) * 420f, 1.5f, CultGold.WithAlpha(0.45f));
                }
            }
        }

        /// A Wisp: a flame-like triangle pointing where it goes, a short trail of embers behind.
        void DrawWispBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 back = -V2.FromAngle(e.Facing);
            for (int k = 1; k <= 3; k++)
                Circle(pos + back * (k * r * 0.75f) + back.Orthogonal() * (Mathf.Sin(t * 14f + k + e.Id) * 2f), r * (0.7f - k * 0.15f), col.WithAlpha(0.4f / k), 10);
            Circle(pos, r + 3f, col.WithAlpha(0.18f), 14);
            Polygon(pos, r, 3, e.Facing, col.Darkened(0.4f));
            Polygon(pos, r - 2f, 3, e.Facing, col);
            Polygon(pos, r * 0.42f, 3, e.Facing, CultBone);
        }

        /// The Acolyte: a hooded cultist, a sickle on a short haft that swings across the cone (`rel` is the swing's angle).
        void DrawAcolyteBody(Enemy e, V2 pos, float r, Rgba col, float rel, float t)
        {
            float f = e.Facing;
            V2 fwd = V2.FromAngle(f), side = fwd.Orthogonal();
            Circle(pos, r, col.Darkened(0.45f), 20);
            Circle(pos, r - 1.8f, col, 20);
            // the hood: a dark peak pointing ahead, a pale face slit under it
            SetColor(col.Darkened(0.6f));
            Tri(pos + fwd * (r * 0.9f), pos - fwd * (r * 0.4f) + side * (r * 0.75f), pos - fwd * (r * 0.4f) - side * (r * 0.75f));
            Line(pos + fwd * (r * 0.35f) - side * (r * 0.28f), pos + fwd * (r * 0.35f) + side * (r * 0.28f), 2f, CultBone.WithAlpha(0.8f));
            // the sickle: the haft from the hand, the blade a crescent at its end, its edge on the cone's rim
            float reach = Game.SwordReachOf(e.Type), inner = Game.SwordInnerOf(e.Type), a = f + rel;
            V2 dir = V2.FromAngle(a), hand = pos + dir * inner, head = pos + dir * (reach - 16f);
            Line(pos + side * (r * 0.7f), hand, 5f, col.Darkened(0.4f));
            Line(hand, head, 3f, new Rgba(0.36f, 0.24f, 0.16f));
            if (e.Phase == 2)
                Arc(pos, reach - 6f, 10f, CultBone.WithAlpha(0.2f), 16, a - 0.35f, 0.35f);
            Arc(head + dir * 2f, 15f, 3.5f, CultBone, 12, a - 0.2f, 2.2f);
            Circle(hand, 3.5f, col.Darkened(0.2f), 8);
        }

        /// Details over the polygon body.
        void DrawCultDetails(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2 fwd = V2.FromAngle(e.Facing), side = fwd.Orthogonal();
            switch (e.Type)
            {
                case EnemyType.BoundWraith:  // a dashed ring: bound to its partner
                    for (int k = 0; k < 6; k++)
                        Arc(pos, r + 5f, 1.5f, col.WithAlpha(0.55f), 4, t * 1.5f + k * MathX.Tau / 6f, 0.5f);
                    break;
                case EnemyType.ScytheWraith:  // its scythe held across its front, raised while it marks, a pale sweep as it reaps
                {
                    float lift = e.Phase == 1 ? 0.6f : 0f;
                    Line(pos - side * (r * 0.9f), pos + side * (r * 0.9f) + fwd * (r * 0.4f), 3f, new Rgba(0.36f, 0.24f, 0.16f));
                    Arc(pos + fwd * 4f, r + 10f, e.Phase == 2 ? 5f : 3f, CultBone.WithAlpha(e.Phase == 2 ? 1f : 0.85f), 16, e.Facing - 1.3f - lift, 2.6f);
                    if (e.Phase == 2)
                    {
                        Arc(pos, r + Game.ScytheReap, 8f, CultBone.WithAlpha(0.25f), 20, e.Facing + MathX.Pi / 2f - 1f, 2f);
                        Arc(pos, r + Game.ScytheReap, 8f, CultBone.WithAlpha(0.25f), 20, e.Facing - MathX.Pi / 2f - 1f, 2f);
                    }
                    break;
                }
                case EnemyType.Lamplighter:  // a lantern out in front: it flares while it winds up
                {
                    V2 l = pos + fwd * (r + 5f);
                    float glow = e.Winding ? 1f : 0.3f + 0.08f * Mathf.Sin(t * 5f + e.Id);
                    Circle(l, 7f + 7f * glow, CultGold.WithAlpha(0.1f + 0.2f * glow), 16);
                    Line(pos + fwd * (r * 0.5f), l, 2f, CultEarth.Darkened(0.3f));
                    Polygon(l, 5f, 4, MathX.Pi / 4f, CultGold.Darkened(0.4f));
                    Polygon(l, 3.6f, 4, MathX.Pi / 4f, CultGold.Mix(Rgba.White, glow * 0.5f));
                    break;
                }
                case EnemyType.Mourner:  // two veils turning opposite ways, wider while it weeps
                {
                    float spread = e.Phase == 1 ? 1f : 0.4f;
                    Arc(pos, r + 6f + 4f * spread, 2.5f, CultDusk.WithAlpha(0.5f + 0.4f * spread), 16, t * 1.5f, 1.4f + spread);
                    Arc(pos, r + 6f + 4f * spread, 2.5f, CultDawn.WithAlpha(0.5f + 0.4f * spread), 16, -t * 1.5f + MathX.Pi, 1.4f + spread);
                    for (int k = -1; k <= 1; k += 2)  // tear streaks
                        Line(pos + fwd * (r * 0.3f) + side * (k * r * 0.3f), pos + fwd * (r * 0.3f) + side * (k * r * 0.3f) - fwd * (r * 0.5f), 1.5f, CultBone.WithAlpha(0.7f));
                    break;
                }
                case EnemyType.Gravedigger:  // a shovel over its shoulder, a dirt-dark face
                {
                    V2 hand = pos + side * (r * 0.8f);
                    V2 tip = hand + V2.FromAngle(e.Facing + 0.5f) * (r + 20f);
                    Line(hand - V2.FromAngle(e.Facing + 0.5f) * 8f, tip, 4f, new Rgba(0.42f, 0.3f, 0.2f));
                    Polygon(tip + V2.FromAngle(e.Facing + 0.5f) * 5f, 9f, 4, e.Facing + 0.5f + MathX.Pi / 4f, CultEarth.Darkened(0.5f));
                    Polygon(tip + V2.FromAngle(e.Facing + 0.5f) * 5f, 7.5f, 4, e.Facing + 0.5f + MathX.Pi / 4f, CultEarth);
                    Line(pos + fwd * (r * 0.35f) - side * (r * 0.4f), pos + fwd * (r * 0.35f) + side * (r * 0.4f), 3f, new Rgba(0.15f, 0.08f, 0.1f, 0.9f));
                    break;
                }
                case EnemyType.Shade:  // smoke rising off its shroud
                    for (int k = 0; k < 4; k++)
                    {
                        float u = MathX.Fmod(t * 0.8f + k * 0.25f + e.Id * 0.13f, 1f);
                        Circle(pos + new V2(Mathf.Sin(k * 2.1f + t) * r * 0.5f, -r * 0.4f - u * r * 1.4f), 2.5f + 2f * u, col.WithAlpha(0.35f * (1f - u)), 8);
                    }
                    break;
            }
        }
    }
}

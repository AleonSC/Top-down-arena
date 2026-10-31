using UnityEngine;

namespace Arena
{
    /// How the second half's new enemies and elite traits look (Variety.cs): the Beam Sentry's aim line and beam,
    /// the Reaver's cone and its opening, the Warder's tethers, bubbles, trait markers over elites, and the
    /// Scorcher's embers. Built from the mockup in Docs/Mockups/enemy-variety.html.
    public partial class ArenaApp
    {
        static readonly Rgba SentryCyan = new Rgba(0.44f, 0.89f, 1f);
        static readonly Rgba BubbleBlue = new Rgba(0.56f, 0.82f, 1f);
        static readonly Rgba WardViolet = new Rgba(0.71f, 0.61f, 1f);

        /// On the floor, under the enemies: beams, cones and tethers.
        void DrawVarietyFloor(float t)
        {
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e))
                    continue;
                V2 pos = e.Disp;
                if (e.Type == EnemyType.BeamSentry && (e.Phase == 2 || e.Phase == 3))
                {
                    V2 tip = pos + V2.FromAngle(e.Facing) * Game.SentryBeamLen;
                    if (e.Phase == 2)
                        DashedLine(pos, tip, 2f, SentryCyan.WithAlpha(0.35f + 0.3f * Mathf.Sin(t * 14f)));
                    else
                    {
                        Line(pos, tip, 32f, SentryCyan.WithAlpha(0.28f));
                        Line(pos, tip, Game.SentryBeamWidth * 0.6f, new Rgba(0.87f, 0.97f, 1f));
                    }
                }
                if (e.Type == EnemyType.Reaver && e.Phase == 1)  // the rake's cone, filling as it winds up
                {
                    float reach = Game.ReaverSwipeR + e.Radius;
                    for (int k = 0; k < 6; k++)
                        Arc(pos, reach * (k + 0.5f) / 6f, reach / 6f, e.Def.Color.WithAlpha(0.1f + 0.05f * k), 10, e.Facing - Game.ReaverCone, Game.ReaverCone * 2f);
                    Line(pos, pos + V2.FromAngle(e.Facing - Game.ReaverCone) * reach, 2f, e.Def.Color.WithAlpha(0.8f));
                    Line(pos, pos + V2.FromAngle(e.Facing + Game.ReaverCone) * reach, 2f, e.Def.Color.WithAlpha(0.8f));
                }
                if (e.WardId != 0 && e.BubbleFrac > 0f)  // a tether back to the Warder holding its bubble up
                {
                    Enemy w;
                    if (game.Enemies.TryGetValue(e.WardId, out w))
                        DashedLine(w.Disp, pos, 2f, WardViolet.WithAlpha(0.55f));
                }
                if (Game.HasTrait(e, EliteTrait.Commander))  // its reach, faint
                    Arc(pos, Game.CommandRange, 1.5f, new Rgba(1f, 0.42f, 0.6f, 0.18f), 48);
            }
        }

        /// Over an enemy's body: a bubble, the Reaver's opening, a planted Beam Sentry's legs, and trait markers.
        void DrawVarietyOnEnemy(Enemy e, V2 pos, float r, float t)
        {
            if (e.BubbleFrac > 0f)
            {
                var c = e.WardId != 0 ? WardViolet : BubbleBlue;
                Circle(pos, r + 11f, c.WithAlpha(0.07f + 0.1f * e.BubbleFrac), 28);
                Arc(pos, r + 11f, 2f, c.WithAlpha(0.35f + 0.5f * e.BubbleFrac), 28);
            }
            if (Game.ReaverIsOpen(e))
                Arc(pos, r + 8f, 3f, BubbleBlue.WithAlpha(0.6f + 0.4f * Mathf.Sin(t * 20f)), 24);
            if (e.Type == EnemyType.BeamSentry && e.Phase >= 1)
                for (int k = 0; k < 3; k++)
                {
                    V2 d = V2.FromAngle(MathX.Tau * k / 3f + 0.5f);
                    Line(pos + d * r, pos + d * (r + 12f), 2f, SentryCyan.WithAlpha(0.6f));
                }
            if (e.Traits == 0)
                return;
            // Hasted: streaks behind it.
            if (Game.HasTrait(e, EliteTrait.Hasted))
            {
                V2 back = -V2.FromAngle(e.Facing), side = back.Orthogonal();
                for (int k = -1; k <= 1; k++)
                    Line(pos + back * (r + 4f) + side * (k * r * 0.5f), pos + back * (r + 16f + 4f * Mathf.Sin(t * 20f + k)) + side * (k * r * 0.5f), 2f, new Rgba(1f, 0.88f, 0.4f, 0.6f));
            }
            // Volatile: a pulsing orange rim.
            if (Game.HasTrait(e, EliteTrait.Volatile))
                Arc(pos, r + 8f, 2f, new Rgba(1f, 0.55f, 0.23f, 0.45f + 0.35f * Mathf.Sin(t * 8f + e.Id)), 24);
            // A row of markers above it, one per trait.
            int n = 0;
            for (int bit = 0; bit < 6; bit++)
                if ((e.Traits & (1 << bit)) != 0)
                    n++;
            int i = 0;
            for (int bit = 0; bit < 6; bit++)
            {
                if ((e.Traits & (1 << bit)) == 0)
                    continue;
                V2 at = pos + new V2((i - (n - 1) / 2f) * 14f, -r - 20f);
                DrawTraitMarker((EliteTrait)(1 << bit), at, t);
                i++;
            }
        }

        void DrawTraitMarker(EliteTrait trait, V2 at, float t)
        {
            switch (trait)
            {
                case EliteTrait.Hasted:  // a chevron
                    Line(at + new V2(-4f, -4f), at + new V2(1f, 0f), 2f, new Rgba(1f, 0.88f, 0.4f));
                    Line(at + new V2(-4f, 4f), at + new V2(1f, 0f), 2f, new Rgba(1f, 0.88f, 0.4f));
                    Line(at + new V2(0f, -4f), at + new V2(5f, 0f), 2f, new Rgba(1f, 0.88f, 0.4f));
                    Line(at + new V2(0f, 4f), at + new V2(5f, 0f), 2f, new Rgba(1f, 0.88f, 0.4f));
                    break;
                case EliteTrait.Shielded:  // a little ring
                    Arc(at, 4.5f, 2f, BubbleBlue, 12);
                    break;
                case EliteTrait.Volatile:  // a burst
                    for (int k = 0; k < 6; k++)
                        Line(at, at + V2.FromAngle(MathX.Tau * k / 6f) * 5f, 1.8f, new Rgba(1f, 0.55f, 0.23f));
                    break;
                case EliteTrait.Splitting:  // three dots
                    for (int k = 0; k < 3; k++)
                        Circle(at + V2.FromAngle(MathX.Tau * k / 3f - MathX.Pi / 2f) * 3.5f, 1.8f, new Rgba(0.77f, 1f, 0.48f), 6);
                    break;
                case EliteTrait.Mirrored:  // a diamond
                {
                    V2 a = at + new V2(0f, -5f), b = at + new V2(5f, 0f), c = at + new V2(0f, 5f), d = at + new V2(-5f, 0f);
                    var w = new Rgba(0.92f, 0.95f, 1f);
                    Line(a, b, 1.8f, w); Line(b, c, 1.8f, w); Line(c, d, 1.8f, w); Line(d, a, 1.8f, w);
                    break;
                }
                case EliteTrait.Commander:  // a star
                    for (int k = 0; k < 5; k++)
                        Line(at, at + V2.FromAngle(MathX.Tau * k / 5f - MathX.Pi / 2f) * 5.5f, 2f, new Rgba(1f, 0.42f, 0.6f));
                    break;
            }
        }

        /// A Scorcher's embers: a small smouldering patch that dims as it burns out.
        void DrawEmbers(PathPatch pt, float r, float t)
        {
            float fade = Mathf.Clamp01((pt.Life - pt.Age) / 0.8f), on = pt.Active ? 1f : 0.4f;
            Circle(pt.Pos, r, new Rgba(0.85f, 0.3f, 0.06f, 0.3f * fade * on), 16);
            Circle(pt.Pos, r * (0.55f + 0.08f * Mathf.Sin(t * 10f + pt.Id)), new Rgba(1f, 0.62f, 0.2f, 0.32f * fade * on), 14);
            for (int k = 0; k < 2; k++)
            {
                float f = MathX.Fmod(t * 1.1f + k * 0.5f + pt.Id * 0.21f, 1f);
                Circle(pt.Pos + new V2((k - 0.5f) * r * 0.7f, -f * r * 1.2f), 1.8f * (1f - f) + 0.5f, new Rgba(1f, 0.8f, 0.35f, 0.8f * (1f - f) * fade), 6);
            }
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// How elite traits look (Variety.cs): bubbles, trait markers over elites, a Commander's reach, and the floor pass
    /// that calls the factions' floor drawing. Built from the mockup in Docs/Mockups/enemy-variety.html.
    public partial class ArenaApp
    {
        static readonly Rgba BubbleBlue = new Rgba(0.56f, 0.82f, 1f);

        /// On the floor, under the enemies: the factions' beams and lines, aim lines, a Commander's reach.
        void DrawVarietyFloor(float t)
        {
            DrawAccretionBands(t);  // the Singularity's Accretion rings (InfallLook.cs)
            foreach (var e in game.Enemies.Values)
            {
                if (game.IsSpawning(e))
                    continue;
                V2 pos = e.Disp;
                DrawInfallFloor(e, pos, t);  // a Pulsar's beams (InfallLook.cs)
                DrawPenumbralFloor(e, pos, t);   // a Heliograph's line (PenumbralLook.cs)
                DrawSquallFloor(e, pos, t);  // a Pylon pair's line, a Lineman's mark (SquallLook.cs)
                DrawShooterAim(e, pos, t);  // a regular shooter's aim line while it winds up (ShooterAimLook.cs)
                if (Game.HasTrait(e, EliteTrait.Commander))  // its reach, faint
                    Arc(pos, Game.CommandRange, 1.5f, new Rgba(1f, 0.42f, 0.6f, 0.18f), 48);
            }
        }

        /// Over an enemy's body: the wind-up glow, a bubble, a Core Carrier's halo, and trait markers.
        void DrawVarietyOnEnemy(Enemy e, V2 pos, float r, float t)
        {
            if (e.Winding && !Game.IsSecurity(e.Type) && Game.SharesWith(e.Type) != EnemyType.GasGrenadier)  // about to fire: a white-hot core and a ring closing in on it (Readability.cs; the Security Guard shows its red lane instead)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 30f);
                Circle(pos, r * 0.55f, new Rgba(1f, 1f, 1f, 0.55f + 0.3f * k), 14);
                Arc(pos, r + 5f + 5f * (1f - k), 2.5f, e.Def.Color.Mix(Rgba.White, 0.6f).WithAlpha(0.9f), 22);
            }
            if (e.BubbleFrac > 0f)
                DrawBubble(e, pos, r, t);  // twelve cells (BubbleLook.cs)
            else if (e.CellsSeen != 0 || e.BubbleBornT != -98f)
                NoBubble(e);
            if (Game.HasTrait(e, EliteTrait.CoreCarrier))  // a Core Carrier: a pulsing gold halo with a few sparks (Docs/Variants.md)
            {
                Circle(pos, r + 11f + 2.5f * Mathf.Sin(t * 6f + e.Id), new Rgba(1f, 0.82f, 0.25f, 0.3f), 20);
                Arc(pos, r + 7f, 2.5f, new Rgba(1f, 0.9f, 0.4f, 0.95f), 24);
                for (int k = 0; k < 3; k++)
                {
                    float f = MathX.Fmod(t * 0.9f + k / 3f + e.Id * 0.13f, 1f);
                    Circle(pos + V2.FromAngle(MathX.Tau * k / 3f + t) * (r + 4f) + new V2(0f, -f * 14f), 1.6f * (1f - f) + 0.5f, new Rgba(1f, 0.9f, 0.4f, 0.9f * (1f - f)), 6);
                }
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

        /// Enemy shots by what they'll do (Readability.cs): an aimed shot is a dart pointing where it flies, a curving
        /// one a crescent bowed toward the side it turns. Rings and sprays stay round.
        void DrawShapedShot(Bullet b, Rgba c)
        {
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            if (b.Look == ShotLook.Dart)
            {
                V2 tip = b.Pos + dir * (r * 1.7f), back = b.Pos - dir * (r * 1.5f);
                Line(back - dir * (r * 1.4f), back, r * 0.9f, c.WithAlpha(0.22f));  // a short streak behind
                SetColor(c.WithAlpha(0.35f));
                Tri(tip + dir * 3f, back + side * (r + 3f), back - side * (r + 3f));
                SetColor(c);
                Tri(tip, back + side * r, back - side * r);
                SetColor(new Rgba(1f, 1f, 1f, 0.9f));
                Tri(b.Pos + dir * (r * 0.9f), b.Pos - dir * (r * 0.4f) + side * (r * 0.35f), b.Pos - dir * (r * 0.4f) - side * (r * 0.35f));
                return;
            }
            // A crescent: its horns point back and toward the side it curves.
            float turn = b.AngVel >= 0f ? 1f : -1f;
            float a = b.Vel.Angle + MathX.Pi - turn * 0.35f;
            Circle(b.Pos, r + 2.5f, c.WithAlpha(0.22f), 12);
            Arc(b.Pos, r * 0.85f, r * 0.9f, c, 12, a - 1.25f, 2.5f);
            Arc(b.Pos, r * 0.85f, 1.5f, new Rgba(1f, 1f, 1f, 0.85f), 10, a - 0.9f, 1.8f);
        }
    }
}

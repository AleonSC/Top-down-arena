using UnityEngine;

namespace Arena
{
    /// How the Devourer's fight looks (Devourer.cs): a reddening gullet as it starves, the lunge lines, the inhale cone
    /// with air rushing in, the burrow ripple and the eruption mark, the Maw's cone, the Jaws' teeth before they fly,
    /// the last stand's ring of teeth, morsels, bile pools and shots, and its toothed mouth (with whoever it swallowed).
    /// Built from the mockup in Docs/Mockups/devourer-3-stage.html.
    public partial class ArenaApp
    {
        static readonly Rgba MawRed = new Rgba(1f, 0.35f, 0.48f);
        static readonly Rgba BileGreen = new Rgba(0.78f, 0.88f, 0.35f);
        static readonly Rgba Enamel = new Rgba(0.96f, 0.9f, 0.82f);
        static readonly Rgba DirtBrown = new Rgba(0.85f, 0.64f, 0.35f);

        static Enemy FindDevourer(Game g)
        {
            foreach (var e in g.Enemies.Values)
                if (e.Type == EnemyType.Devourer && e.Devour != null && !g.IsSpawning(e))
                    return e;
            return null;
        }

        /// A filled slice of a circle (a cone), drawn as a fan of triangles.
        void Wedge(V2 center, float r, float ang, float half, Rgba col, int segs = 16)
        {
            SetColor(col);
            V2 prev = center + V2.FromAngle(ang - half) * r;
            for (int k = 1; k <= segs; k++)
            {
                V2 next = center + V2.FromAngle(ang - half + 2f * half * k / segs) * r;
                Tri(center, prev, next);
                prev = next;
            }
        }

        /// A tooth pointing along `ang`: an enamel triangle with a dark outline.
        void Tooth(V2 at, float ang, float size, float alpha = 1f)
        {
            V2 tip = at + V2.FromAngle(ang) * size, b1 = at + V2.FromAngle(ang + 2.5f) * size * 0.6f, b2 = at + V2.FromAngle(ang - 2.5f) * size * 0.6f;
            SetColor(new Rgba(0.47f, 0.08f, 0.16f, 0.9f * alpha));
            Tri(tip + V2.FromAngle(ang) * 2f, b1 + V2.FromAngle(ang + 2.5f) * 2f, b2 + V2.FromAngle(ang - 2.5f) * 2f);
            SetColor(Enamel.WithAlpha(alpha));
            Tri(tip, b1, b2);
        }

        /// Over the floor, under the enemies: the gullet's tint, the cones, lines, ripple, jaws and teeth.
        void DrawDevourerFloor(float t)
        {
            var e = FindDevourer(game);
            if (e == null)
                return;
            var s = e.Devour;
            V2 pos = e.Disp;
            if (s.Stage >= DevourStage.Ravenous && s.TransT <= 0f)
            {
                float a = 0.1f + 0.03f * Mathf.Sin(t * 2f);
                FillRect(0f, 0f, Game.Width, Game.Height, new Rgba(0.35f, 0.02f, 0.08f, a));
            }
            if (s.TransT > 0f)
                return;
            // The inhale: a cone with air rushing into its mouth.
            if (s.InhaleT > 0f)
            {
                Wedge(pos, Game.InhaleReach, e.Facing, Game.InhaleHalf, BileGreen.WithAlpha(0.06f + 0.04f * Mathf.Sin(t * 12f)), 20);
                Arc(pos, Game.InhaleReach, 2f, BileGreen.WithAlpha(0.35f), 20, e.Facing - Game.InhaleHalf, 2f * Game.InhaleHalf);
                for (int k = 0; k < 18; k++)
                {
                    float a = e.Facing + (k / 17f - 0.5f) * 2f * Game.InhaleHalf * 0.9f, d = Game.InhaleReach - Fmod(t * 420f + k * 97f, Game.InhaleReach - 40f);
                    Line(pos + V2.FromAngle(a) * d, pos + V2.FromAngle(a) * (d - 40f), 2f, new Rgba(0.86f, 0.94f, 0.55f, 0.35f));
                }
            }
            // The Maw: the side its mouth heals it from.
            if (s.Stage == DevourStage.Ravenous && s.BurrowT <= 0f)
            {
                Wedge(pos, 260f, e.Facing, Game.MawHalf, MawRed.WithAlpha(0.09f), 14);
                for (int side = -1; side <= 1; side += 2)
                    DashedLine(pos, pos + V2.FromAngle(e.Facing + side * Game.MawHalf) * 260f, 2f, MawRed.WithAlpha(0.45f));
            }
            // A lunge's warning line, as wide as its body.
            if (s.Lunging && s.LungeWarn > 0f)
            {
                V2 end = s.LungeFrom + V2.FromAngle(s.LungeAng) * s.LungeLen;
                float blink = Fmod(t, 0.16f) < 0.08f ? 1f : 0.6f;
                Line(s.LungeFrom, end, e.Radius * 2f, new Rgba(1f, 0.27f, 0.4f, 0.12f));
                Line(s.LungeFrom, end, 3f, new Rgba(1f, 0.43f, 0.55f, 0.9f * blink));
                Arc(end, Game.BiteReach * 0.6f, 2.5f, new Rgba(1f, 0.43f, 0.55f, 0.7f * blink), 16, s.LungeAng - MathX.Pi / 2f, MathX.Pi);
            }
            // Underground: a ripple of earth that stops, then marks where it will burst out.
            if (s.BurrowT > 0f)
            {
                for (int j = 0; j < 3; j++)
                {
                    float f = Fmod(t * 60f + j * 25f, 75f);
                    Arc(pos, 30f + f, 3f, DirtBrown.WithAlpha(0.6f - f / 140f), 24);
                }
                for (int j = 0; j < 6; j++)
                    Circle(pos + new V2(Mathf.Cos(j * 1.1f + t * 4f) * 30f, Mathf.Sin(j * 1.7f + t * 5f) * 22f), 4f, new Rgba(0.54f, 0.42f, 0.25f), 8);
                if (s.BurrowT >= Game.BurrowChase)
                {
                    float k = Mathf.Clamp01((s.BurrowT - Game.BurrowChase) / (Game.BurrowTime - Game.BurrowChase));
                    Circle(pos, Game.EruptRadius, new Rgba(1f, 0.27f, 0.4f, 0.08f + 0.15f * k), 32);
                    Arc(pos, Game.EruptRadius, 2.5f, new Rgba(1f, 0.43f, 0.55f, 0.9f), 32);
                    Arc(pos, Game.EruptRadius * k, 3f, new Rgba(1f, 0.43f, 0.55f, 0.9f), 32);
                }
            }
            // The Jaws: the teeth show where they'll fly from, blinking, until they close.
            if (s.JawsT > 0f)
            {
                float blink = 0.7f + 0.3f * Mathf.Sin(t * 20f);
                for (int side = -1; side <= 1; side += 2)
                    for (int i = 0; i < 7; i++)
                    {
                        V2 at = Game.JawsTooth(s, side, i);
                        Circle(at, 15f, MawRed.WithAlpha(0.3f * blink), 12);
                        Tooth(at, (s.JawsAt - at).Angle, 16f);
                    }
                Arc(s.JawsAt, 24f, 2f, MawRed.WithAlpha(0.6f), 16);
            }
            // The last stand: ten teeth turning around it, with two gaps.
            if (s.Stage == DevourStage.LastStand)
                for (int k = 0; k < 12; k++)
                    if (k != 0 && k != 6)
                    {
                        V2 at = Game.ToothPos(pos, s.Spin, k);
                        Circle(at, Game.ToothHit + 6f, MawRed.WithAlpha(0.12f), 12);
                        Tooth(at, (pos - at).Angle, 22f);
                    }
        }

        /// Under its body: a glow, and a pulsing ring while it's hungry.
        void DrawDevourerUnderlay(Enemy e, V2 pos, float r, float t)
        {
            Circle(pos, r + 34f, MawRed.WithAlpha(0.1f + 0.05f * Mathf.Sin(t * 5f)), 28);
            if (e.Phase == 1)  // hungry: a pulsing warning glow, as it always had
            {
                Circle(pos, r + 18f + 6f * Mathf.Sin(t * 10f), new Rgba(1f, 0.2f, 0.6f, 0.25f), 40);
                Arc(pos, r + 18f + 6f * Mathf.Sin(t * 10f), 4f, new Rgba(1f, 0.2f, 0.6f, 0.45f), 40);
            }
        }

        /// Its mouth, on the side it faces: dark, ringed with teeth, wider when it feeds, inhales, lunges or starves;
        /// whoever it swallowed shows faintly inside.
        void DrawDevourerMouth(Enemy e, V2 pos, float r, float t)
        {
            var s = e.Devour;
            bool wide = e.Phase == 1 || s != null && (s.InhaleT > 0f || s.Stage == DevourStage.Ravenous || s.Turn == DevourTurn.Vomit || s.Lunging && s.LungeWarn <= 0f);
            float open = s != null && s.SwallowId != 0 ? 0.35f : wide ? 0.6f : 0.38f;
            // veins
            for (int k = 0; k < 5; k++)
            {
                float a = k * 1.3f + 0.4f;
                Line(pos + V2.FromAngle(a) * r * 0.45f, pos + V2.FromAngle(a + 0.3f) * r * 0.9f, 2f, new Rgba(1f, 0.55f, 0.63f, 0.25f));
            }
            V2 mouth = pos + V2.FromAngle(e.Facing) * r * 0.32f;
            float mr = r * open;
            Circle(mouth, mr, new Rgba(0.1f, 0f, 0.03f, 1f), 24);
            for (int k = 0; k < 10; k++)
            {
                float a = e.Facing + MathX.Tau * k / 10f + t * 0.3f;
                Tooth(mouth + V2.FromAngle(a) * mr, a + MathX.Pi, Mathf.Max(6f, mr * 0.32f));
            }
            Player q;
            if (s != null && s.SwallowId != 0 && game.Players.TryGetValue(s.SwallowId, out q))
                Circle(mouth, mr * 0.5f, q.Def.Color.WithAlpha(0.4f + 0.3f * Mathf.Sin(t * 8f)), 16);
        }

        /// A morsel: a lump of meat that bobs where it landed.
        void DrawMorsel(Enemy e, V2 pos, float r, float t)
        {
            Circle(pos + new V2(0f, 8f), r, new Rgba(0f, 0f, 0f, 0.3f), 14);
            Circle(pos + new V2(-r * 0.3f, -r * 0.3f), r * 0.4f, new Rgba(1f, 0.76f, 0.6f, 0.9f), 10);
            Arc(pos, r + 4f + 2f * Mathf.Sin(t * 5f + e.Id), 1.5f, new Rgba(1f, 0.69f, 0.44f, 0.5f), 16);
        }

        /// A bile pool: a glob in the air with its shadow growing, then a pool of bubbling acid.
        void DrawBilePatch(PathPatch pt, float t)
        {
            float r = pt.Radius;
            if (!pt.Active)
            {
                float k = Mathf.Clamp01(pt.Age / Mathf.Max(0.01f, pt.Warn));
                Circle(pt.Pos, r * (0.4f + 0.6f * k), new Rgba(0f, 0f, 0f, 0.2f + 0.25f * k), 24);
                for (int i = 0; i < 16; i += 2)  // a dashed rim
                    Arc(pt.Pos, r, 2f, BileGreen.WithAlpha(0.7f), 3, MathX.Tau * i / 16f, MathX.Tau / 16f);
                V2 glob = pt.Pos - new V2(0f, (1f - k) * 260f);
                Circle(glob, 13f, BileGreen, 12);
                Circle(glob - new V2(3f, 3f), 5f, new Rgba(0.94f, 1f, 0.7f), 8);
                return;
            }
            float a = Mathf.Clamp01((pt.Age - pt.Warn) / 0.15f) * Mathf.Clamp01((pt.Life - pt.Age) / 0.5f);
            Circle(pt.Pos, r, new Rgba(0.59f, 0.78f, 0.24f, 0.24f * a), 28);
            Arc(pt.Pos, r, 2f, new Rgba(0.78f, 0.9f, 0.35f, 0.6f * a), 28);
            for (int j = 0; j < 4; j++)  // bubbles
            {
                V2 at = pt.Pos + new V2(Mathf.Cos(t * 0.7f + j * 1.7f + pt.Id) * r * 0.55f, Mathf.Sin(t * 0.9f + j * 2.3f + pt.Id) * r * 0.45f);
                Circle(at, 5f + 3f * Mathf.Sin(t * 3f + j), new Rgba(0.86f, 0.98f, 0.47f, 0.35f * a), 8);
            }
        }

        /// The Devourer's shots: green globs of bile, and the Jaws' teeth flying point-first.
        void DrawDevourerShot(Bullet b, float t)
        {
            if (b.Kind == Defs.ToothShot)
            {
                Tooth(b.Pos, b.Vel.Angle, 16f);
                return;
            }
            var c = Defs.BulletColor(b.Kind);
            Circle(b.Pos, b.Radius * 1.7f, c.WithAlpha(0.25f), 10);
            Circle(b.Pos, b.Radius, c, 10);
            Circle(b.Pos - new V2(1.5f, 1.5f), b.Radius * 0.4f, new Rgba(0.94f, 1f, 0.7f, 0.9f), 6);
        }
    }
}

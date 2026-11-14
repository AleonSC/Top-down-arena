using UnityEngine;

namespace Arena
{
    /// Zombie Infection's look (Docs/Mockups/zombie-enemies.html): seven green zombies with their own shapes (wide and
    /// narrow triangles, a pentagon, a trapezoid, a swelling circle, a diamond and a cross), a spore haze, dead eyes,
    /// green drips as they move, frenzy streaks after a Shrieker's scream, the Crawler's tendrils round whoever it holds,
    /// the Spitter's acid and the Bloater's spores. The arena turns dark green while the modifier is on (WorldRenderer.cs).
    public partial class ArenaApp
    {
        static readonly Rgba ZombieFloor = new Rgba(0.043f, 0.063f, 0.047f, 0.9f), ZombieGrid = new Rgba(0.55f, 1f, 0.42f, 0.045f), ZombieBorder = new Rgba(0.32f, 0.6f, 0.34f, 0.6f);

        bool ZombieArena => game.Has(RunMod.ZombieInfection) && game.Practice == null;

        // Each shape's corners, pointing along +X (scaled by the radius, turned to face where it's going).
        static readonly float[] ShamblerPts = { 1.05f, 0f, -0.75f, 1.05f, -0.75f, -1.05f };
        static readonly float[] RunnerPts = { 1.55f, 0f, -0.95f, 0.52f, -0.95f, -0.52f };
        static readonly float[] CrawlerPts = { 0.75f, 0.5f, -0.8f, 1f, -0.8f, -1f, 0.75f, -0.5f };
        static readonly float[] ShriekerPts = { 1.2f, 0f, 0f, 0.72f, -1f, 0f, 0f, -0.72f };
        // The Grave Caller (the Lich's faction): a tall cross, longer to the front, under a hood.
        static readonly float[] CallerPts = { 1.25f, -0.28f, 1.25f, 0.28f, 0.28f, 0.28f, 0.28f, 0.8f, -0.28f, 0.8f, -0.28f, 0.28f, -0.9f, 0.28f, -0.9f, -0.28f, -0.28f, -0.28f, -0.28f, -0.8f, 0.28f, -0.8f, 0.28f, -0.28f };
        static readonly Rgba SoulEye = new Rgba(0.84f, 0.96f, 1f), GraveBone = new Rgba(0.91f, 0.89f, 0.78f);
        static readonly float[] MenderPts = { 1f, -0.36f, 1f, 0.36f, 0.36f, 0.36f, 0.36f, 1f, -0.36f, 1f, -0.36f, 0.36f, -1f, 0.36f, -1f, -0.36f, -0.36f, -0.36f, -0.36f, -1f, 0.36f, -1f, 0.36f, -0.36f };
        readonly V2[] zShape = new V2[16];

        int ZombieShape(Enemy e, V2 pos, float r, float t)
        {
            float bob = e.Type == EnemyType.Shambler ? 0.22f * Mathf.Sin(t * 5f + e.Id) : e.Type == EnemyType.Runner ? 0.08f * Mathf.Sin(t * 16f + e.Id) : e.Type == EnemyType.Crawler ? 0.12f * Mathf.Sin(t * 12f + e.Id) : 0f;
            float a = e.Facing + bob;
            bool acting = (e.Phase & Zombies.PhAct) != 0;
            if (e.Type == EnemyType.Bloater)
            {
                float k = r * (1f + 0.09f * Mathf.Sin(t * 2.6f + e.Id));
                for (int i = 0; i < 16; i++)
                    zShape[i] = pos + V2.FromAngle(a + MathX.Tau * i / 16f) * k;
                return 16;
            }
            if (e.Type == EnemyType.Spitter)
            {
                float k = r * (acting ? 1.2f : 1f);
                for (int i = 0; i < 5; i++)
                    zShape[i] = pos + V2.FromAngle(a + MathX.Tau * i / 5f) * k;
                return 5;
            }
            var pts = e.Type == EnemyType.Shambler ? ShamblerPts : e.Type == EnemyType.Runner ? RunnerPts : e.Type == EnemyType.Crawler ? CrawlerPts : e.Type == EnemyType.Shrieker ? ShriekerPts : e.Type == EnemyType.GraveCaller ? CallerPts : MenderPts;
            float s = r * (e.Type == EnemyType.Shrieker && acting ? 1.3f : 1f);
            V2 fx = V2.FromAngle(a), fy = fx.Orthogonal();
            int n = pts.Length / 2;
            for (int i = 0; i < n; i++)
                zShape[i] = pos + fx * (pts[i * 2] * s) + fy * (pts[i * 2 + 1] * s);
            return n;
        }

        /// The body: a dark green fill, its outline in its colour, a darker core and two pale dead eyes.
        void DrawZombieBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            int n = ZombieShape(e, pos, r, t);
            SetColor(col.Darkened(0.5f));
            for (int i = 0; i < n; i++)
                Tri(pos, zShape[i], zShape[(i + 1) % n]);
            for (int i = 0; i < n; i++)
                Line(zShape[i], zShape[(i + 1) % n], 2.5f, col);
            SetColor(new Rgba(0.04f, 0.12f, 0.05f, 0.55f));
            for (int i = 0; i < n; i++)
                Tri(pos, pos + (zShape[i] - pos) * 0.48f, pos + (zShape[(i + 1) % n] - pos) * 0.48f);
            if (e.Type == EnemyType.Mender)
                Circle(pos, r * ((e.Phase & Zombies.PhAct) != 0 ? 0.34f : 0.22f), new Rgba(0.86f, 1f, 0.9f, 0.55f + 0.35f * Mathf.Sin(t * 4f + e.Id)), 12);
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            bool soul = e.Type == EnemyType.GraveCaller || (e.Phase & Zombies.PhCalled) != 0;
            if (e.Type == EnemyType.GraveCaller)
            {
                // its hood over the head end, and while it calls, the soul light gathering in its raised hands
                Arc(pos + f * (r * 0.6f), r * 0.55f, 2f, SoulEye.WithAlpha(0.85f), 14, e.Facing + 1.9f, MathX.Tau - 3.8f);
                if ((e.Phase & Zombies.PhAct) != 0)
                    for (int s = -1; s <= 1; s += 2)
                        Circle(pos + side * (s * r * 0.95f) + f * (r * 0.2f), r * 0.3f, Game.CallerSoul.WithAlpha(0.45f + 0.35f * Mathf.Sin(t * 14f)), 10);
            }
            float ef = e.Type == EnemyType.Runner ? 0.55f : e.Type == EnemyType.GraveCaller ? 0.75f : 0.35f, er = Mathf.Max(1.8f, r * 0.09f);
            for (int s = -1; s <= 1; s += 2)
                Circle(pos + f * (r * ef) + side * (s * r * 0.24f), er, soul ? SoulEye : new Rgba(0.92f, 1f, 0.62f), 8);
            if (e.Type == EnemyType.Spitter && (e.Phase & Zombies.PhAct) != 0)
                Circle(pos + f * r, r * 0.45f, new Rgba(0.72f, 1f, 0.48f, 0.5f), 12);  // the glob leaving its mouth
        }

        /// Under the body: the spore haze, frenzy streaks, the Crawler's hold, and (with effects on) green drips.
        const float ZombieDripCrowd = 30f;

        void DrawZombieUnder(Enemy e, V2 pos, float r, float t)
        {
            var col = e.Def.Color;
            if (fxOn && !fxReduced)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2f + e.Id);
                Circle(pos, r * 1.7f, col.WithAlpha(0.05f + 0.03f * pulse), 18);
                Circle(pos, r * 1.3f, col.WithAlpha(0.07f + 0.03f * pulse), 16);
                float rate = (e.Type == EnemyType.Runner ? 22f : 6f) * Mathf.Min(1f, ZombieDripCrowd / Mathf.Max(1f, game.Enemies.Count));  // (a breach flood: about as many drips in all, fewer each, so the particle pool stays in budget)
                if (!game.IsSpawning(e) && Rnd(0f, 1f) < ViewDt * rate)
                    Sparks(pos - V2.FromAngle(e.Facing) * (r * 0.6f), col, 1, 16f, e.Type == EnemyType.Runner ? 0.5f : 0.9f, e.Type == EnemyType.Runner ? 2.2f : 2f);
            }
            if ((e.Phase & Zombies.PhCalled) != 0)
            {
                // a Grave Caller's Shambler: a faint soul thread back to the Caller that raised it (kill that and it crumbles)
                Enemy best = null;
                float bd = 900f;
                foreach (var o in game.Enemies.Values)
                    if (o.Type == EnemyType.GraveCaller && o.Hp > 0f && o.Disp.DistanceTo(pos) < bd)
                    {
                        best = o;
                        bd = o.Disp.DistanceTo(pos);
                    }
                if (best != null)
                {
                    V2 to = best.Disp, mid = (pos + to) * 0.5f + (to - pos).Normalized().Orthogonal() * (10f * Mathf.Sin(t * 3f + e.Id));
                    var thread = Game.CallerSoul.WithAlpha(0.16f + 0.06f * Mathf.Sin(t * 9f + e.Id));
                    Line(pos, mid, 2f, thread);
                    Line(mid, to, 2f, thread);
                }
            }
            if ((e.Phase & Zombies.PhFrenzy) != 0)
            {
                V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal() * (r * 0.6f);
                for (int k = 1; k <= 3; k++)
                {
                    V2 b = pos - f * (r + 6f * k);
                    var c = new Rgba(0.89f, 1f, 0.61f, 0.35f / k);
                    Line(b + side, b + side - f * 10f, 2f, c);
                    Line(b - side, b - side - f * 10f, 2f, c);
                }
            }
            if (e.Type == EnemyType.Crawler && (e.Phase & Zombies.PhGrab) != 0)
            {
                var p = HeldBy(e);
                if (p != null)
                {
                    V2 to = p.Disp, mid = (pos + to) * 0.5f, n = (to - pos).Normalized().Orthogonal();
                    var vine = new Rgba(0.25f, 0.67f, 0.33f, 0.95f);
                    for (int i = -1; i <= 1; i++)
                    {
                        V2 bend = mid + n * (i * 14f + 4f * Mathf.Sin(t * 9f + i)), foot = to + n * (i * 8f) + new V2(0f, 10f);
                        Line(pos, bend, 3f, vine);
                        Line(bend, foot, 3f, vine);
                    }
                    Arc(to, 30f, 3f, new Rgba(0.55f, 1f, 0.42f, 0.6f + 0.4f * Mathf.Sin(t * 20f)), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.StunT / Zombies.GrabTime));
                }
            }
        }

        /// The player a grabbing Crawler holds: the nearest one still rooted (the host knows; every game can see it).
        Player HeldBy(Enemy crawler)
        {
            Player best = null;
            float bd = crawler.Radius + 40f;
            foreach (var p in game.Players.Values)
                if (p.StunT > 0f && !p.Downed && p.Disp.DistanceTo(crawler.Disp) < bd)
                {
                    best = p;
                    bd = p.Disp.DistanceTo(crawler.Disp);
                }
            return best;
        }

        /// Is a Crawler holding this player? (Then its vines show instead of the Warden's stun crackle.)
        bool HeldByCrawler(Player p)
        {
            if (p.StunT <= 0f)
                return false;
            foreach (var e in game.Enemies.Values)
                if (e.Type == EnemyType.Crawler && (e.Phase & Zombies.PhGrab) != 0 && e.Disp.DistanceTo(p.Disp) < e.Radius + 40f)
                    return true;
            return false;
        }

        /// A Spitter's acid: a glowing green puddle with bubbles (the glob's landing ring comes with the throw).
        void DrawAcidPatch(PathPatch pt, float t)
        {
            if (!pt.Active)
                return;
            float a = Mathf.Clamp01((pt.Age - pt.Warn) / 0.25f) * Mathf.Clamp01((pt.Life - pt.Age) / 0.6f), r = pt.Radius;
            Circle(pt.Pos, r, new Rgba(0.31f, 0.88f, 0.65f, 0.22f * a), 28);
            Circle(pt.Pos, r * 0.7f, new Rgba(0.67f, 1f, 0.47f, 0.3f * a), 24);
            Arc(pt.Pos, r, 2f, new Rgba(0.67f, 1f, 0.47f, 0.45f * a), 28);
            for (int b = 0; b < 4; b++)
            {
                float q = t * 1.3f + b * 1.7f + pt.Id;
                Circle(pt.Pos + new V2(Mathf.Cos(q * 1.3f) * r * 0.5f, Mathf.Sin(q) * r * 0.38f), 2f + (b % 2), new Rgba(0.86f, 1f, 0.63f, 0.5f * a), 8);
            }
        }

        /// A Grave Caller's grave: cracks glowing soul cyan and spreading, pulsing faster as it opens; then a dark pit with
        /// two bony hands reaching out, fading. Gone at once if no Grave Caller is left (its graves close with it).
        void DrawGravePatch(PathPatch pt, float t)
        {
            bool caller = false;
            foreach (var o in game.Enemies.Values)
                if (o.Type == EnemyType.GraveCaller && o.Hp > 0f)
                {
                    caller = true;
                    break;
                }
            if (!caller && !pt.Active)
                return;
            float r = pt.Radius, k = Mathf.Clamp01(pt.Age / Mathf.Max(0.01f, pt.Warn)), pulse = 0.5f + 0.5f * Mathf.Sin(t * (8f + 12f * k));
            float fade = Mathf.Clamp01((pt.Life - pt.Age) / 0.6f);
            var soul = Game.CallerSoul;
            if (!pt.Active)
            {
                Circle(pt.Pos, r * (0.7f + 0.5f * k), soul.WithAlpha(0.1f + 0.08f * k), 20);
                Circle(pt.Pos, r * (0.4f + 0.4f * k), soul.WithAlpha(0.12f + 0.25f * k * pulse), 16);
                for (int i = 0; i < 6; i++)
                {
                    float q = MathX.Tau * i / 6f + pt.Id * 1.7f, len = r * (0.35f + 0.75f * k) * (i % 2 == 0 ? 1f : 0.7f);
                    V2 dir = new V2(Mathf.Cos(q), Mathf.Sin(q) * 0.65f), kink = pt.Pos + dir * (len * 0.55f) + dir.Orthogonal() * (len * 0.15f);
                    Line(pt.Pos, kink, 2.5f, soul.WithAlpha(0.55f + 0.45f * pulse));
                    Line(kink, pt.Pos + dir * len, 1.5f, soul.WithAlpha(0.35f + 0.4f * pulse));
                }
                Arc(pt.Pos, r, 1.5f, soul.WithAlpha(0.25f + 0.3f * k), 24);
                return;
            }
            // open: a dark pit, a soul glow at its lip, two hands of bone clawing out
            float h = Mathf.Clamp01((pt.Age - pt.Warn) / 0.3f);
            Circle(pt.Pos, r * 0.8f, new Rgba(0.02f, 0.04f, 0.05f, 0.75f * fade), 20);
            Arc(pt.Pos, r * 0.8f, 2f, soul.WithAlpha(0.5f * fade), 20);
            for (int s = -1; s <= 1; s += 2)
            {
                V2 b = pt.Pos + new V2(s * r * 0.35f, 0f), w = b + new V2(s * 2f, -12f * h), c = w + new V2(s * 4f, -5f * h);
                Line(b, w, 3f, GraveBone.WithAlpha(fade));
                Line(w, c, 2f, GraveBone.WithAlpha(fade));
                Line(w, w + new V2(-s * 3f, -6f * h), 2f, GraveBone.WithAlpha(fade));
            }
        }

        /// A Bloater's spores: a thick green fog swirling round, thinning out.
        void DrawSporePatch(PathPatch pt, float t)
        {
            float a = Mathf.Clamp01(pt.Age / 0.3f) * Mathf.Clamp01((pt.Life - pt.Age) / 1f), r = pt.Radius;
            for (int i = 0; i < 9; i++)
            {
                float q = MathX.Tau * i / 9f + t * 0.4f * (i % 2 == 0 ? 1f : -1f), rr = r * (0.35f + 0.4f * ((i * 37) % 7) / 7f);
                V2 c = pt.Pos + new V2(Mathf.Cos(q) * rr * 0.6f, Mathf.Sin(q) * rr * 0.5f);
                Circle(c, r * 0.6f, new Rgba(0.66f, 0.79f, 0.23f, 0.14f * a), 18);
                Circle(c, r * 0.35f, new Rgba(0.66f, 0.79f, 0.23f, 0.12f * a), 14);
            }
        }
    }
}

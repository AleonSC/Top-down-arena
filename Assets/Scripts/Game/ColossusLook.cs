using UnityEngine;

namespace Arena
{
    /// How the Colossus's fight looks (Colossus.cs): its stomp rings (push blocks like the Warden's gates, spreading
    /// out), the shadows of boulders, meteors and falling pillars, fissures, the charge line, fallen plates, pillars,
    /// and its body breaking down stage by stage.
    public partial class ArenaApp
    {
        static readonly Rgba Stone = new Rgba(0.78f, 0.62f, 0.45f), StoneDark = new Rgba(0.37f, 0.3f, 0.24f), Magma = new Rgba(1f, 0.48f, 0.23f);

        static ColossusState ColossusOf(Game g, out Enemy boss)
        {
            boss = null;
            foreach (var e in g.Enemies.Values)
                if (e.Type == EnemyType.Colossus && e.Colossus != null && !g.IsSpawning(e))
                {
                    boss = e;
                    return e.Colossus;
                }
            return null;
        }

        /// On the floor, under everyone: fissures, the charge line, landing shadows, plates, pillars and the rings.
        void DrawColossusFloor(float t)
        {
            Enemy boss;
            var c = ColossusOf(game, out boss);
            if (c == null)
                return;
            foreach (var k in c.Cracks)
                DrawCrack(k, t);
            if (c.ChargeWarn > 0f)  // where it's about to charge: a wide red lane and a dashed centre line
            {
                V2 dir = V2.FromAngle(c.ChargeDir), from = boss.Disp, to = from + dir * Game.ColChargeLength;
                Line(from, to, boss.Radius * 2f, new Rgba(1f, 0.27f, 0.27f, 0.12f + 0.1f * Mathf.Sin(t * 25f)));
                for (float d = 0f; d < Game.ColChargeLength; d += 24f)
                    Line(from + dir * d, from + dir * Mathf.Min(Game.ColChargeLength, d + 14f), 2f, new Rgba(1f, 0.35f, 0.35f, 0.9f));
            }
            foreach (var d in c.Drops)
            {
                float k = Mathf.Clamp01(d.T / d.Dur), blink = k > 0.7f ? 0.6f + 0.4f * Mathf.Sin(t * 30f) : 1f;
                Circle(d.Pos, d.Radius * (0.3f + 0.7f * k), new Rgba(0f, 0f, 0f, 0.25f + 0.35f * k), 28);
                Arc(d.Pos, d.Radius, 2.5f, (d.Hot ? Magma : Stone).WithAlpha(0.85f * blink), 32);
            }
            foreach (var s in c.Slabs)
                DrawSlab(s);
            foreach (var p in c.Pillars)
            {
                Circle(p.Pos + new V2(0f, 6f), Game.PillarR + 4f, new Rgba(0f, 0f, 0f, 0.4f), 20);
                Polygon(p.Pos, Game.PillarR, 7, p.Pos.X * 0.1f, new Rgba(0.43f, 0.35f, 0.27f));
                Arc(p.Pos, Game.PillarR, 3f, p.T < 0.6f ? new Rgba(1f, 0.86f, 0.7f, 0.5f + 0.5f * Mathf.Sin(t * 30f)) : Stone, 20);  // cracking: it's about to shatter
            }
            foreach (var r in c.Rings)
                DrawStompRing(r, t);
        }

        /// In the air, over everyone: what it threw, on an arc toward its shadow, and pillars dropping from above.
        void DrawColossusAir(float t)
        {
            Enemy boss;
            var c = ColossusOf(game, out boss);
            if (c == null)
                return;
            foreach (var d in c.Drops)
            {
                float k = Mathf.Clamp01(d.T / d.Dur);
                if (d.Kind == ColDrop.Pillar)
                {
                    Polygon(d.Pos - new V2(0f, (1f - k) * 260f), 22f, 7, 0.3f, new Rgba(0.55f, 0.45f, 0.34f, k));
                    continue;
                }
                V2 at = d.From + (d.Pos - d.From) * k - new V2(0f, Mathf.Sin(k * Mathf.PI) * 220f);
                float r = d.Hot ? 24f : 18f;
                Polygon(at, r, 6, t * 3f, d.Hot ? new Rgba(0.35f, 0.16f, 0.1f) : new Rgba(0.54f, 0.44f, 0.34f));
                Arc(at, r, 3f, d.Hot ? Magma : Stone, 16);
                if (d.Hot)
                    Circle(at, r * 0.45f, new Rgba(1f, 0.67f, 0.27f, 0.7f + 0.3f * Mathf.Sin(t * 20f)), 12);
            }
        }

        /// A stomp ring: two rows of push blocks round its centre, staggered like bricks, each chevron pointing out;
        /// its gaps glow green. Before it moves, a pulse at its feet shows where the gaps will be.
        void DrawStompRing(StompRing r, float t)
        {
            if (r.Warn > 0f)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(t * 22f);
                Arc(r.Center, r.Radius, 6f, PushCol.WithAlpha(0.35f + 0.35f * blink), 48);
                DrawRingGap(r, r.Gap1, r.Radius + 30f);
                if (r.Gaps == 2)
                    DrawRingGap(r, r.Gap2, r.Radius + 30f);
                return;
            }
            for (int row = 0; row < Game.GateRows; row++)
            {
                float rad = r.Radius - row * Game.GateRowGap;
                if (rad < 8f)
                    continue;
                int n = Mathf.Max(8, Mathf.FloorToInt(MathX.Tau * rad / 26f));
                float off = row % 2 == 0 ? 0f : MathX.Pi / n;
                for (int i = 0; i < n; i++)
                {
                    float a = off + MathX.Tau * i / n;
                    V2 at = r.Center + V2.FromAngle(a) * rad;
                    if (at.X < -20f || at.Y < -20f || at.X > Game.Width + 20f || at.Y > Game.Height + 20f)
                        continue;
                    if (Game.InRingGap(r, at, -Game.GateBulletR))
                        continue;
                    DrawRingBlock(at, V2.FromAngle(a), row == 0);
                }
            }
            DrawRingGap(r, r.Gap1, r.Radius + 4f);
            if (r.Gaps == 2)
                DrawRingGap(r, r.Gap2, r.Radius + 4f);
        }

        /// A gap's edges, lit green like the Warden's doors.
        void DrawRingGap(StompRing r, float gap, float rad)
        {
            var green = new Rgba(0.5f, 1f, 0.65f, 0.8f);
            Arc(r.Center, rad, 4f, green, 10, gap - Game.RingGapHalf, Game.RingGapHalf * 2f);
        }

        /// One push block, turned to face outward: the same look as the Warden's gate bullets (WardenLook.cs).
        void DrawRingBlock(V2 p, V2 dir, bool lead)
        {
            float r = Game.GateBulletR;
            V2 side = dir.Orthogonal();
            Circle(p, r * 1.6f, new Rgba(0.65f, 0.78f, 1f, 0.16f), 10);
            SetColor(PushEdge);
            Quad(p + (-dir - side) * (r + 1f), p + (dir - side) * (r + 1f), p + (dir + side) * (r + 1f), p + (-dir + side) * (r + 1f));
            SetColor(lead ? PushLead : PushCol);
            Quad(p + (-dir - side) * r, p + (dir - side) * r, p + (dir + side) * r, p + (-dir + side) * r);
            var dark = new Rgba(0.1f, 0.14f, 0.27f, 0.95f);
            Line(p - side * 4f - dir * 2f, p + dir * 2.5f, 2f, dark);
            Line(p + dir * 2.5f, p + side * 4f - dir * 2f, 2f, dark);
        }

        /// A fissure: a dark jagged crack with a hot line in it, glowing and flickering once it's about to erupt.
        void DrawCrack(ColCrack k, float t)
        {
            const int segs = 20;
            float seg = k.Max / segs;
            bool ready = k.Len >= k.Max;
            V2 dir = V2.FromAngle(k.Ang), side = dir.Orthogonal(), prev = k.From;
            var hot = ready ? new Rgba(1f, 0.55f + 0.3f * Mathf.Sin(t * 30f), 0.25f) : new Rgba(1f, 0.47f, 0.2f, 0.7f);
            for (int i = 1; i <= segs && (i - 1) * seg < k.Len; i++)
            {
                float d = Mathf.Min(i * seg, k.Len), jag = (Hash(k.Seed * 31 + i) - 0.5f) * 20f;
                V2 at = k.From + dir * d + side * (i == segs ? 0f : jag);
                Line(prev, at, 12f, new Rgba(0.08f, 0.04f, 0.02f, 0.9f));
                Line(prev, at, ready ? 5f : 3f, hot);
                prev = at;
            }
        }

        /// A fallen plate: a stone slab with cracks that spread as enemy shots wear it down.
        void DrawSlab(ColSlab s)
        {
            float x0 = s.Pos.X - s.W / 2f, y0 = s.Pos.Y - s.H / 2f;
            FillRect(x0, y0 + 5f, s.W, s.H, new Rgba(0f, 0f, 0f, 0.35f));
            FillRect(x0, y0, s.W, s.H, new Rgba(0.37f, 0.3f, 0.24f));
            FillRect(x0 + 3f, y0 + 3f, s.W - 6f, s.H - 6f, new Rgba(0.5f, 0.41f, 0.32f));
            V2 a = new V2(x0, y0), b = new V2(x0 + s.W, y0), c2 = new V2(x0 + s.W, y0 + s.H), d = new V2(x0, y0 + s.H);
            Line(a, b, 2f, Stone); Line(b, c2, 2f, Stone); Line(c2, d, 2f, Stone); Line(d, a, 2f, Stone);
            int cracks = (40 - s.Hp) / 8;
            bool wide = s.W > s.H;
            for (int k = 0; k < cracks; k++)
            {
                V2 from = wide ? new V2(x0 + (k + 1) * s.W / 6f, y0) : new V2(x0, y0 + (k + 1) * s.H / 6f);
                V2 to = from + (wide ? new V2(6f, s.H * 0.65f) : new V2(s.W * 0.65f, 6f));
                Line(from, to, 2f, new Rgba(0.1f, 0.07f, 0.05f, 0.9f));
            }
        }

        /// Under its body: the stage change, a molten glow from stage 3, and the yellow flash while dazed.
        void DrawColossusUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var c = e.Colossus;
            if (c == null)
                return;
            if (c.TransT > 0f)
                DrawStageShiftBoss(e, pos, r, t);  // changing stage (StageShiftLook.cs)
            if (c.Stage >= ColStage.MoltenCore)
                Circle(pos, r + 26f + 4f * Mathf.Sin(t * 5f), Magma.WithAlpha(0.18f + 0.08f * Mathf.Sin(t * 5f)), 32);
            if (c.DazeT > 0f)
                Arc(pos, r + 8f, 4f, new Rgba(1f, 0.86f, 0.35f, 0.6f + 0.4f * Mathf.Sin(t * 20f)), 32);
        }

        /// On its body: armour plates (Awakening), then cracks that glow molten from stage 3, a glowing core, the
        /// eyes on its target, and dizzy stars while it's dazed.
        void DrawColossusDetails(Enemy e, V2 pos, float r, float t)
        {
            var c = e.Colossus;
            var stage = c != null ? c.Stage : ColStage.Awakening;
            float rot = e.Facing + MathX.Pi / 6f;
            if (stage == ColStage.Awakening)
                for (int k = 0; k < 6; k++)  // its armour: an inner hexagon of plate seams
                {
                    V2 a = pos + V2.FromAngle(rot + MathX.Tau * k / 6f) * (r - 14f), b = pos + V2.FromAngle(rot + MathX.Tau * (k + 1) / 6f) * (r - 14f);
                    Line(a, b, 3f, new Rgba(0.24f, 0.17f, 0.12f, 0.9f));
                }
            else
            {
                bool molten = stage >= ColStage.MoltenCore;
                var crack = molten ? new Rgba(1f, 0.5f + 0.25f * Mathf.Sin(t * 6f), 0.2f, 0.95f) : new Rgba(0.12f, 0.08f, 0.05f, 0.9f);
                for (int k = 0; k < (molten ? 5 : 3); k++)
                {
                    float a = rot + k * 1.3f;
                    V2 p1 = pos + V2.FromAngle(a) * 8f, p2 = pos + V2.FromAngle(a + 0.25f) * r * 0.55f, p3 = pos + V2.FromAngle(a - 0.1f) * r * 0.95f;
                    Line(p1, p2, molten ? 4f : 3f, crack);
                    Line(p2, p3, molten ? 4f : 3f, crack);
                }
                if (molten)
                    Circle(pos, 13f + 2f * Mathf.Sin(t * 6f), new Rgba(1f, 0.75f, 0.35f, 0.9f), 16);
            }
            for (int k = -1; k <= 1; k += 2)  // eyes on its target
                Circle(pos + V2.FromAngle(e.Facing + k * 0.35f) * r * 0.55f, 5f, stage >= ColStage.MoltenCore ? new Rgba(1f, 0.82f, 0.48f) : new Rgba(1f, 0.92f, 0.72f), 8);
            if (c != null && c.DazeT > 0f)
                for (int k = 0; k < 3; k++)  // dizzy stars circling its head
                {
                    V2 at = pos + new V2(Mathf.Cos(t * 5f + k * 2.1f) * 30f, -r - 14f + Mathf.Sin(t * 5f + k * 2.1f) * 8f);
                    Polygon(at, 6f, 5, t * 4f, new Rgba(1f, 0.85f, 0.35f));
                }
        }
    }
}

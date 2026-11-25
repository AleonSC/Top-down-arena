using UnityEngine;

namespace Arena
{
    /// How the Colossus's fight looks (Colossus.cs): its stomp rings (orbs like the Singularity's disk, with a few
    /// small openings; a Stomper's are push blocks like the Warden's gates), where a held boulder will land and the
    /// shadows of boulders, meteors and falling pillars, fissures and the scars they leave, the charge line, fallen
    /// plates, pillars, and under its body the glow and the Break meter while it's staggered (the body itself:
    /// ColossusBodyLook.cs; its shell blasting off: ColossusShellLook.cs).
    public partial class ArenaApp
    {
        static readonly Rgba Stone = new Rgba(0.78f, 0.62f, 0.45f), Magma = new Rgba(1f, 0.48f, 0.23f);

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
            DrawColossusShell(boss, c, t, false);  // shards of its shell lying where they landed (ColossusShellLook.cs)
            foreach (var k in c.Cracks)
                DrawColCrack(k, t);
            if (c.ChargeWarn > 0f)  // where it's about to charge: a wide red lane and a dashed centre line
            {
                V2 dir = V2.FromAngle(c.ChargeDir), from = boss.Disp, to = from + dir * Game.ColChargeLength;
                Line(from, to, boss.Radius * 2f, new Rgba(1f, 0.27f, 0.27f, 0.12f + 0.1f * Mathf.Sin(t * 25f)));
                for (float d = 0f; d < Game.ColChargeLength; d += 24f)
                    Line(from + dir * d, from + dir * Mathf.Min(Game.ColChargeLength, d + 14f), 2f, new Rgba(1f, 0.35f, 0.35f, 0.9f));
            }
            if (c.HoldT > 0f)  // where the boulder it's holding up will land: a dashed red ring that follows its target
            {
                float k = Mathf.Clamp01((Game.ColHoldTime - c.HoldT) / Game.ColHoldTime);
                for (int i = 0; i < 16; i++)
                    Arc(c.HoldAim, Game.ColBoulderR, 2.5f, new Rgba(1f, 0.32f, 0.38f, 0.25f + 0.6f * k), 4, MathX.Tau * i / 16f + t * 0.6f, MathX.Tau / 32f);
            }
            foreach (var d in c.Drops)
            {
                float k = Mathf.Clamp01(d.T / d.Dur), blink = k > 0.7f ? 0.6f + 0.4f * Mathf.Sin(t * 30f) : 1f;
                if (d.Kind == ColDrop.Boulder)  // its shadow on the floor under it, all along the throw
                {
                    float up = Mathf.Sin(k * Mathf.PI);
                    Circle(V2.Lerp(d.From, d.Pos, k), ColBoulderSize * (1f - 0.35f * up), new Rgba(0f, 0f, 0f, 0.4f - 0.2f * up), 20);
                    Circle(d.Pos, d.Radius * k, new Rgba(1f, 0.25f, 0.3f, 0.08f + 0.14f * k), 28);
                    Arc(d.Pos, d.Radius, 2.5f, new Rgba(1f, 0.35f, 0.38f, 0.9f * blink), 32);
                    continue;
                }
                Circle(d.Pos, d.Radius * (0.3f + 0.7f * k), new Rgba(0f, 0f, 0f, 0.25f + 0.35f * k), 28);
                Arc(d.Pos, d.Radius, 2.5f, (d.Hot ? Magma : Stone).WithAlpha(0.85f * blink), 32);
            }
            foreach (var s in c.Slabs)
                DrawSlab(s);
            DrawColossusPlatesFlying(boss, c, t, false);  // shadows of its plates coming off (ColossusBodyLook.cs)
            foreach (var p in c.Pillars)
            {
                Circle(p.Pos + new V2(0f, 6f), Game.PillarR + 4f, new Rgba(0f, 0f, 0f, 0.4f), 20);
                Polygon(p.Pos, Game.PillarR, 7, p.Pos.X * 0.1f, new Rgba(0.43f, 0.35f, 0.27f));
                Arc(p.Pos, Game.PillarR, 3f, p.T < 0.6f ? new Rgba(1f, 0.86f, 0.7f, 0.5f + 0.5f * Mathf.Sin(t * 30f)) : Stone, 20);  // cracking: it's about to shatter
            }
            foreach (var r in c.Rings)
                if (r.Orbs)
                    DrawColOrbRing(r, t, c.Stage >= ColStage.MoltenCore);
                else
                    DrawStompRing(r, t);
        }

        /// In the air, over everyone: what it threw, on an arc toward its shadow, and pillars dropping from above.
        void DrawColossusAir(float t)
        {
            Enemy boss;
            var c = ColossusOf(game, out boss);
            if (c == null)
                return;
            DrawColossusPlatesFlying(boss, c, t, true);  // its plates flying off to become cover (ColossusBodyLook.cs)
            DrawColossusShell(boss, c, t, true);  // its shell blasting off (ColossusShellLook.cs)
            foreach (var d in c.Drops)
            {
                float k = Mathf.Clamp01(d.T / d.Dur);
                if (d.Kind == ColDrop.Pillar)
                {
                    Polygon(d.Pos - new V2(0f, (1f - k) * 260f), 22f, 7, 0.3f, new Rgba(0.55f, 0.45f, 0.34f, k));
                    continue;
                }
                if (d.Kind == ColDrop.Boulder)  // a huge boulder lobbed high: bigger at the top of the throw, shedding grit
                {
                    float up = Mathf.Sin(k * Mathf.PI);
                    V2 rock = V2.Lerp(d.From, d.Pos, k) - new V2(0f, up * 240f);
                    for (int q = 1; q <= 4; q++)
                    {
                        float kq = Mathf.Max(0f, k - q * 0.04f);
                        V2 back = V2.Lerp(d.From, d.Pos, kq) - new V2(0f, Mathf.Sin(kq * Mathf.PI) * 240f);
                        Circle(back + new V2(TH(q * 3.1f) - 0.5f, 0f) * 16f, 7f - q, Stone.WithAlpha(0.5f - q * 0.1f), 6);
                    }
                    ColBoulder(rock, ColBoulderSize * (1f + 0.5f * up), 0.85f);
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

        /// The Colossus's stomp ring: a ring of indestructible orbs like the Singularity's disk (DrawEmberOrb's look), in
        /// sandstone (molten from Molten Core), with a few small openings. Before it moves, a pulse at its feet.
        void DrawColOrbRing(StompRing r, float t, bool hot)
        {
            Rgba body = hot ? new Rgba(1f, 0.5f, 0.22f) : new Rgba(0.93f, 0.74f, 0.45f), rim = hot ? new Rgba(1f, 0.85f, 0.45f) : new Rgba(1f, 0.92f, 0.7f);
            if (r.Warn > 0f)
            {
                float blink = 0.5f + 0.5f * Mathf.Sin(t * 22f);
                Arc(r.Center, r.Radius, 6f, body.WithAlpha(0.3f + 0.35f * blink), 48);
                return;
            }
            float or = Game.ColOrbR;
            for (int i = 0; i < Game.ColRingOrbs; i++)
            {
                float a = r.Rot + MathX.Tau * i / Game.ColRingOrbs;
                if (Game.RingOpenAt(r, a))
                    continue;
                V2 at = Game.RingOrbAt(r, i);
                if (at.X < -20f || at.Y < -20f || at.X > Game.Width + 20f || at.Y > Game.Height + 20f)
                    continue;
                Circle(at, or * 1.9f, body.WithAlpha(0.16f), 10);
                Circle(at, or, body, 12);
                Arc(at, or * 0.72f, or * 0.3f, rim, 12);  // the hot rim
                Circle(at, or * 0.4f, new Rgba(0.12f, 0.06f, 0.02f, 0.9f), 8);  // a boss orb's dark core
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
            float half = Game.RingGapHalfAt(rad);
            Arc(r.Center, rad, 4f, green, 10, gap - half, half * 2f);
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

        /// One of the Colossus's fissures: racing out and about to erupt like a Stomper's crack (DrawCrack), then a
        /// burning trench while it erupts, then a dark scar with a faint ember line until the phase ends.
        void DrawColCrack(ColCrack k, float t)
        {
            if (k.State == ColCrack.Racing || k.State == ColCrack.Waiting)
            {
                DrawCrack(k, t);
                return;
            }
            const int segs = 20;
            float seg = k.Max / segs;
            V2 dir = V2.FromAngle(k.Ang), side = dir.Orthogonal(), prev = k.From;
            bool erupt = k.State == ColCrack.Erupting;
            float fade = erupt ? 1f - Mathf.Clamp01((k.Wait - (Game.CrackErupt - 0.3f)) / 0.3f) : 0f;
            for (int i = 1; i <= segs && (i - 1) * seg < k.Len; i++)
            {
                float d = Mathf.Min(i * seg, k.Len), jag = (Hash(k.Seed * 31 + i) - 0.5f) * 20f;
                V2 at = k.From + dir * d + side * (i == segs ? 0f : jag);
                Line(prev, at, 14f, new Rgba(0.07f, 0.04f, 0.02f, 0.92f));
                if (erupt)
                {
                    Line(prev, at, Game.CrackHurtR * 2f, Magma.WithAlpha(0.35f * fade));
                    Line(prev, at, 10f + 3f * Mathf.Sin(t * 40f + i), new Rgba(1f, 0.85f, 0.5f, fade));
                }
                else
                    Line(prev, at, 2.5f, new Rgba(0.65f, 0.25f, 0.08f, 0.45f + 0.15f * Mathf.Sin(t * 2f + i * 0.7f)));
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

        /// Under its body: the phase change, a molten glow from phase 3, and the yellow flash while dazed.
        void DrawColossusUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var c = e.Colossus;
            if (c == null)
                return;
            if (c.TransT > 0f)
                DrawStageShiftBoss(e, pos, r, t);  // changing phase (StageShiftLook.cs)
            if (c.Stage >= ColStage.MoltenCore)
                Circle(pos, r + 26f + 4f * Mathf.Sin(t * 5f), Magma.WithAlpha(0.18f + 0.08f * Mathf.Sin(t * 5f)), 32);
            if (c.DazeT > 0f)
                Arc(pos, r + 8f, 4f, new Rgba(1f, 0.86f, 0.35f, 0.6f + 0.4f * Mathf.Sin(t * 20f)), 32);
            if (c.Stagger)  // staggered: the Break meter as a gold ring round it, filling as it's hit
            {
                var gold = new Rgba(1f, 0.8f, 0.3f);
                Arc(pos, r + 18f, 7f, gold.WithAlpha(0.18f), 48);
                Arc(pos, r + 18f, 7f, gold.WithAlpha(0.9f), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(c.Break));
                Arc(pos, r + 18f, 16f, gold.WithAlpha(0.12f + 0.08f * Mathf.Sin(t * 8f)), 48, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(c.Break));
            }
        }
    }
}

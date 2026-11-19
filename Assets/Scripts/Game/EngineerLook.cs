using UnityEngine;

namespace Arena
{
    /// The Engineer's look (Docs/Engineer.md, mockup only: no class or game logic yet). Everything he builds is drawn in
    /// the Expanding arena's vector ink (cyan neon with a white core over a dark plate):
    ///   the Drag Pylon: a tripod with a blinking tip that projects a violet grid on the floor (clipped to its circle) with a
    ///     dashed rim and a slow pulse; enemies inside trail three violet drag rings; an old pylon folds up as the next lands;
    ///   Breach: an amber vector hammer draws itself, swings into the wall, a white flash and cyan shards burst, then the room
    ///     behind builds in (the game's surprise-room build-in) with its contents' icon.
    /// Tools/Preview draws it through `EngineerMock` (ShotEngineer.cs); nothing in a run sets it.
    public class EngineerMock
    {
        public bool Pylon; public V2 PylonPos; public float PylonAge; public V2 OldPylonPos; public float OldFold = -1f;
        public bool Breach; public V2 WallPt, Out; public float BreachT; public int Loot = -1; public V2 LootPos; public float LootAge;
        public bool Hat; public int Model = -1;  // Model: the Engineer look drawn in place of your own model (EngineerModel.cs)
    }

    public partial class ArenaApp
    {
        internal static EngineerMock engineerMock;  // Tools/Preview only (ShotEngineer.cs)

        static readonly Rgba EngInk = new Rgba(0.36f, 0.88f, 1f), EngInkHot = new Rgba(0.72f, 0.96f, 1f), EngPlate = new Rgba(0.05f, 0.1f, 0.16f);
        static readonly Rgba EngSlow = new Rgba(0.64f, 0.55f, 1f), EngAmber = new Rgba(1f, 0.71f, 0.28f), EngHostile = new Rgba(1f, 0.37f, 0.45f);
        static readonly Rgba EngGood = new Rgba(0.44f, 0.94f, 0.66f), EngWhite = new Rgba(1f, 1f, 1f);
        public const float PylonRadius = 130f;

        /// A neon stroke: a soft wide halo, the ink, a thin white core.
        void EngStroke(V2 a, V2 b, float w, Rgba c, float alpha = 1f)
        {
            Line(a, b, w * 3.2f, c.WithAlpha(0.16f * alpha));
            Line(a, b, w, c.WithAlpha(alpha));
            Line(a, b, w * 0.35f, EngWhite.WithAlpha(0.65f * alpha));
        }

        void DrawEngineerMock(float t)
        {
            var m = engineerMock;
            if (m == null)
                return;
            if (m.OldFold >= 0f && m.OldFold < 1f)
                DrawDragPylon(m.OldPylonPos, m.PylonAge + 3f, m.OldFold, t);
            if (m.Pylon)
                DrawDragPylon(m.PylonPos, m.PylonAge, 0f, t);
            if (m.Pylon)
                foreach (var e in game.Enemies.Values)
                    if (e.Hp > 0f && (e.Disp - m.PylonPos).Length < PylonRadius)
                        DrawDragTrail(e, t);
            if (m.Breach)
                DrawBreach(m, t);
            if (m.Loot >= 0)
                DrawBreachLoot(m.LootPos, m.Loot, m.LootAge, t);
            if (m.Hat && game.Me != null)
                DrawHardHat(game.Me.Disp, game.Me.Def.BodyR, game.Me.Aim);
        }

        // --- in a run: drawn from the replicated zone, minion and effect ---

        /// A Drag Pylon zone (ZoneKind.DragPylon): it unfolds as it lands and folds up in its last moment.
        void DrawDragPylonZone(Zone z, float t)
        {
            float age = z.Duration - z.T;
            float fold = z.T < 0.3f ? 1f - z.T / 0.3f : 0f;
            bool med = z.Kind == ZoneKind.MedPylon;
            DrawDragPylon(z.Pos, age, fold, t, z.Radius, med);
            if (med)  // a Med Pylon: plus marks rising off the floor instead of drag rings
            {
                for (int i = 0; i < 4; i++)
                {
                    float ph = Mathf.Repeat(t * 0.5f + i * 0.25f, 1f);
                    V2 c = z.Pos + V2.FromAngle(i * 1.9f + 0.6f) * (z.Radius * 0.55f) + new V2(0f, -20f * ph);
                    float a = Mathf.Sin(ph * MathX.Pi) * 0.8f;
                    Line(c - new V2(5f, 0f), c + new V2(5f, 0f), 2.4f, EngGood.WithAlpha(a));
                    Line(c - new V2(0f, 5f), c + new V2(0f, 5f), 2.4f, EngGood.WithAlpha(a));
                }
                return;
            }
            foreach (var e in game.Enemies.Values)
                if (e.Hp > 0f && e.Disp.CloserThan(z.Pos, z.Radius + e.Radius * 0.5f))
                    DrawDragTrail(e, t);
        }

        /// A Breach room's loot (NodeKind.BreachLoot, its kind in Rule): the icon bobbing in a ring to walk into, a label under it
        /// (an ambush shows only "?"); gone once taken.
        void DrawBreachLootNode(MapNode n)
        {
            if (n.Spent)
                return;
            float t = game.AnimT;
            int kind = Mathf.Clamp(n.Rule, 0, 4);
            Rgba c = kind == 4 ? EngAmber : LootInk[kind];
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
            Circle(n.Pos, 58f, c.WithAlpha(0.05f + 0.04f * pulse), 28);
            Arc(n.Pos, Game.BreachLootReach + 14f + 3f * pulse, 2f, c.WithAlpha(0.35f + 0.25f * pulse), 32);
            if (kind == 4)
            {
                Arc(n.Pos, 14f, 2.4f, c, 20, MathX.Pi, MathX.Pi * 1.5f);
                Circle(n.Pos + new V2(0f, 14f), 2.2f, c, 8);
            }
            else
                DrawBreachLoot(n.Pos + new V2(0f, Mathf.Sin(t * 2.6f) * 3f), kind, 1f, t);
        }

        /// One Vector Wall or Bastion tile: a glowing cyan block turned to its Aim, fading in its last second; GuardFrac its soak left.
        void DrawVectorBlock(Minion m, float t)
        {
            float a = m.T >= 0f && m.T < 1f ? m.T : 1f;
            float age = m.Duration - m.T;
            float grow = Mathf.Clamp01(age / 0.2f);
            V2 f = V2.FromAngle(m.Aim), s = new V2(-f.Y, f.X);
            float hw = 17f * grow, hd = 8f * grow;
            V2 p0 = m.Disp - f * hd - s * hw, p1 = m.Disp + f * hd - s * hw, p2 = m.Disp + f * hd + s * hw, p3 = m.Disp - f * hd + s * hw;
            SetColor(EngPlate.WithAlpha(0.85f * a)); Quad(p0, p1, p2, p3);
            SetColor(EngInk.WithAlpha(0.12f * a * (0.4f + 0.6f * m.GuardFrac))); Quad(p0, p1, p2, p3);
            EngStroke(p0, p1, 1.8f, EngInk, a); EngStroke(p1, p2, 1.8f, EngInk, a); EngStroke(p2, p3, 1.8f, EngInk, a); EngStroke(p3, p0, 1.8f, EngInk, a);
            Line(m.Disp - s * hw * 0.8f, m.Disp + s * hw * 0.8f, 1f, EngInk.WithAlpha(0.4f * a));
        }

        /// Safe Room: the room (or dome) washed green, a dashed cyan rim, plus marks rising; the cut shown as a faint shield hatch.
        void DrawSafeRoom(Zone z, float t)
        {
            float left = Mathf.Clamp01(z.T / Mathf.Max(0.01f, z.Duration)), a = z.T < 1f ? z.T : 1f;
            Circle(z.Pos, z.Radius, EngGood.WithAlpha(0.08f * a), 48);
            int dashes = 32;
            for (int i = 0; i < dashes; i++)
                Arc(z.Pos, z.Radius, 2.2f, EngGood.WithAlpha(0.8f * a), 4, MathX.Tau * i / dashes + t * 0.2f, MathX.Tau / dashes * 0.5f);
            Arc(z.Pos, z.Radius + 6f, 2.5f, EngInk.WithAlpha(0.6f * a), 48, -MathX.Pi / 2f, MathX.Tau * left);
            for (int i = 0; i < 6; i++)
            {
                float ph = Mathf.Repeat(t * 0.4f + i / 6f, 1f);
                V2 c = z.Pos + V2.FromAngle(i * 2.4f) * (z.Radius * 0.6f) + new V2(0f, -26f * ph);
                float al = Mathf.Sin(ph * MathX.Pi) * 0.8f * a;
                Line(c - new V2(6f, 0f), c + new V2(6f, 0f), 2.6f, EngGood.WithAlpha(al));
                Line(c - new V2(0f, 6f), c + new V2(0f, 6f), 2.6f, EngGood.WithAlpha(al));
            }
        }

        /// FxKind.BreachSmash: the hammer and the hit (Pos the wall, Pos2 one px on into it, T its age).
        void DrawBreachFx(Effect fx)
        {
            V2 o = fx.Pos2 - fx.Pos;
            var m = new EngineerMock { WallPt = fx.Pos, Out = o.Length > 0.01f ? o.Normalized() : new V2(0f, -1f), BreachT = fx.T };
            DrawBreach(m, Time.time);
        }

        void DrawDashed(V2 a, V2 b, Rgba c, float w, float dash, float gap, float offset)
        {
            float len = (b - a).Length;
            if (len < 1f)
                return;
            V2 d = (b - a) / len;
            float s = -Mathf.Repeat(offset, dash + gap);
            for (; s < len; s += dash + gap)
            {
                float s0 = Mathf.Max(0f, s), s1 = Mathf.Min(len, s + dash);
                if (s1 > s0)
                    Line(a + d * s0, a + d * s1, w, c);
            }
        }

        void DrawLockReticle(V2 at, float r, float t)
        {
            for (int i = 0; i < 4; i++)
            {
                float a = t * 1.5f + i * MathX.Pi * 0.5f;
                V2 d = V2.FromAngle(a), n = new V2(-d.Y, d.X);
                EngStroke(at + d * r - n * 6f, at + d * r + n * 6f, 2.2f, EngInk);
                EngStroke(at + d * (r - 4f), at + d * (r + 4f), 2.2f, EngInk);
            }
        }

        void DrawDragPylon(V2 at, float age, float fold, float t, float radius = PylonRadius, bool med = false)
        {
            Rgba ink = med ? EngGood : EngSlow;
            float r = radius * (1f - fold) * Mathf.Clamp01(age / 0.25f);
            if (r > 2f)
            {
                Circle(at, r, ink.WithAlpha(0.07f), 48);
                const float sp = 26f;
                float o = Mathf.Repeat(t * 12f, sp);
                for (float x = -r + o; x < r; x += sp)
                {
                    float h = Mathf.Sqrt(Mathf.Max(0f, r * r - x * x));
                    Line(at + new V2(x, -h), at + new V2(x, h), 1.2f, ink.WithAlpha(0.28f));
                    Line(at + new V2(-h, x), at + new V2(h, x), 1.2f, ink.WithAlpha(0.28f));
                }
                int dashes = 28;
                for (int i = 0; i < dashes; i++)
                {
                    float a0 = MathX.Tau * i / dashes - t * 0.25f;
                    Arc(at, r, 2.4f, ink, 4, a0, MathX.Tau / dashes * 0.55f);
                }
                Arc(at, r, 9f, ink.WithAlpha(0.12f), 48);
                float pu = Mathf.Repeat(t * 0.6f, 1f);
                Arc(at, r * pu, 1.4f, ink.WithAlpha(0.5f * (1f - pu)), 40);
            }
            float k = 1f - fold;
            if (k <= 0.02f)
                return;
            V2 tip = at + new V2(0f, -18f * k);
            Circle(at + new V2(0f, 4f), 13f * k, new Rgba(0f, 0f, 0f, 0.3f), 16);
            EngStroke(at + new V2(-11f * k, 9f * k), tip, 2.2f, ink);
            EngStroke(at + new V2(11f * k, 9f * k), tip, 2.2f, ink);
            EngStroke(at + new V2(0f, 9f * k), tip, 2.2f, ink);
            float blink = 0.6f + 0.4f * Mathf.Sin(t * 6f);
            Circle(tip, 7f * k, ink.WithAlpha(0.3f * blink), 12);
            Circle(tip, 3.2f * k, EngWhite, 10);
        }

        void DrawDragTrail(Enemy e, float t)
        {
            V2 back = V2.FromAngle(e.Facing + MathX.Pi);
            for (int i = 1; i <= 3; i++)
                Arc(e.Disp + back * (e.Radius + i * 8f), 3f + i * 0.6f, 1.6f, EngSlow.WithAlpha(0.85f - i * 0.2f), 12);
        }

        void DrawHardHat(V2 at, float r, float aim)
        {
            V2 f = V2.FromAngle(aim), s = new V2(-f.Y, f.X);
            // a hard hat seen from above: an amber shell over the back of the head and a brim jutting forward
            Arc(at - f * (r * 0.2f), r * 0.62f, 3.4f, EngAmber, 18, aim + MathX.Pi * 0.5f, MathX.Pi);
            Line(at - f * (r * 0.2f) + s * (r * 0.75f), at - f * (r * 0.2f) - s * (r * 0.75f), 3f, EngAmber);
            Circle(at - f * (r * 0.5f), 2.2f, EngWhite, 8);
        }

        void DrawBreach(EngineerMock m, float t)
        {
            float bt = m.BreachT;
            V2 o = m.Out, side = new V2(-o.Y, o.X);
            // the hammer: drawn in over 0.55 s behind you, swung into the wall by 0.75 s, faded by 1.3 s
            if (bt < 1.3f)
            {
                float draw = Mathf.Clamp01(bt / 0.55f);
                float swing = bt < 0.55f ? -1.15f + draw * 0.15f : Mathf.Clamp(-1f + (bt - 0.55f) / 0.2f * 1.0f, -1f, 0f);
                float alpha = bt > 0.9f ? Mathf.Clamp01(1f - (bt - 0.9f) / 0.4f) : 1f;
                V2 pivot = m.WallPt - o * 150f;
                V2 hd = V2.FromAngle(o.Angle + swing), hs = new V2(-hd.Y, hd.X);
                System.Func<float, float, V2> P = (along, across) => pivot + hd * along + hs * across;
                var segs = new[] {
                    P(0f, 0f), P(118f, 0f),
                    P(118f, -40f), P(118f, 40f), P(118f, 40f), P(150f, 40f), P(150f, 40f), P(150f, -40f), P(150f, -40f), P(118f, -40f),
                    P(118f, -24f), P(150f, -24f), P(118f, 24f), P(150f, 24f), P(14f, -7f), P(14f, 7f) };
                int n = segs.Length / 2;
                float dn = draw * n;
                for (int i = 0; i < n; i++)
                {
                    float u = Mathf.Clamp01(dn - i);
                    if (u <= 0f)
                        break;
                    V2 a = segs[i * 2], b = segs[i * 2 + 1];
                    EngStroke(a, a + (b - a) * u, 2.8f, EngAmber, alpha);
                }
            }
            // the hit: a white flash over the hole and cyan shards thrown back into the arena
            if (bt >= 0.75f && bt < 1.6f)
            {
                float u = (bt - 0.75f) / 0.85f;
                if (u < 0.35f)
                {
                    float fa = 1f - u / 0.35f;
                    V2 c = m.WallPt;
                    SetColor(EngWhite.WithAlpha(0.85f * fa));
                    Quad(c - side * 90f - o * 30f, c + side * 90f - o * 30f, c + side * 90f + o * 30f, c - side * 90f + o * 30f);
                    Circle(c, 140f * u / 0.35f, EngWhite.WithAlpha(0.15f * fa), 40);
                }
                Arc(m.WallPt, 30f + 120f * u, 5f * (1f - u) + 1f, EngInk.WithAlpha(1f - u), 48, o.Angle + MathX.Pi * 0.5f, MathX.Pi);
                for (int i = 0; i < 26; i++)
                {
                    float h1 = Mathf.Repeat(Mathf.Sin(i * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin(i * 78.233f) * 12543.1f, 1f);
                    float ang = o.Angle + MathX.Pi + (h1 - 0.5f) * 2.6f;
                    float dist = (60f + 240f * h2) * (1f - (1f - u) * (1f - u));
                    V2 p = m.WallPt + side * ((h2 - 0.5f) * 90f) + V2.FromAngle(ang) * dist;
                    V2 d = V2.FromAngle(i * 1.7f + u * 9f * (h1 - 0.5f)) * (5f + 8f * h1);
                    EngStroke(p - d, p + d, 2f, i % 3 == 0 ? EngWhite : EngInk, 1f - u);
                }
            }
        }

        static readonly Rgba[] LootInk = { EngInk, EngAmber, EngGood, EngSlow, EngHostile };

        /// 0 vector cache, 1 supply crate, 4 ambush (2 and 3 were the Strider's, retired).
        void DrawBreachLoot(V2 at, int kind, float age, float t)
        {
            float pop = Mathf.Clamp01(age / 0.3f);
            if (pop <= 0f)
                return;
            float s = 16f * (pop < 1f ? 0.6f + 0.6f * pop : 1f + Mathf.Sin(t * 4f) * 0.05f);
            Rgba c = LootInk[kind];
            Circle(at, s * 1.8f, c.WithAlpha(0.12f), 24);
            switch (kind)
            {
                case 0:
                {
                    V2 a = at + new V2(0f, -s), b = at + new V2(s, 0f), d = at + new V2(0f, s), e = at + new V2(-s, 0f);
                    SetColor(c.WithAlpha(0.25f)); Quad(a, b, d, e);
                    EngStroke(a, b, 2.4f, c); EngStroke(b, d, 2.4f, c); EngStroke(d, e, 2.4f, c); EngStroke(e, a, 2.4f, c);
                    break;
                }
                case 1:
                    FillRect(at.X - s * 0.7f, at.Y - s * 0.45f, s * 1.4f, s * 0.9f, c.WithAlpha(0.2f));
                    EngStroke(at + new V2(-s * .7f, -s * .45f), at + new V2(s * .7f, -s * .45f), 2.4f, c);
                    EngStroke(at + new V2(s * .7f, -s * .45f), at + new V2(s * .7f, s * .45f), 2.4f, c);
                    EngStroke(at + new V2(s * .7f, s * .45f), at + new V2(-s * .7f, s * .45f), 2.4f, c);
                    EngStroke(at + new V2(-s * .7f, s * .45f), at + new V2(-s * .7f, -s * .45f), 2.4f, c);
                    EngStroke(at + new V2(-s * .7f, -s * .1f), at + new V2(s * .7f, -s * .1f), 2f, c);
                    break;
                default:
                    EngStroke(at + new V2(0f, -s * .7f), at + new V2(s * .65f, s * .5f), 2.4f, c);
                    EngStroke(at + new V2(s * .65f, s * .5f), at + new V2(-s * .65f, s * .5f), 2.4f, c);
                    EngStroke(at + new V2(-s * .65f, s * .5f), at + new V2(0f, -s * .7f), 2.4f, c);
                    EngStroke(at + new V2(0f, -s * .25f), at + new V2(0f, s * .1f), 2.4f, c);
                    Circle(at + new V2(0f, s * .3f), 1.8f, c, 6);
                    break;
            }
        }
    }
}

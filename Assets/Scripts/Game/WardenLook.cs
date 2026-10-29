using UnityEngine;

namespace Arena
{
    /// How the Warden's fight looks (Warden.cs): the watchtower and its searchlight, the Lockdown's gates of push
    /// bullets, the riot's doors, and on the Warden itself the cross guides, the stage-change shell, the riot
    /// shield, the baton charge's line and the last stand. Also stunned players, stun shots and rioters.
    public partial class ArenaApp
    {
        static readonly V2 Tower = new V2(40f, 40f);  // top-left: the boss bar covers the top middle
        static readonly Rgba LightCol = new Rgba(1f, 0.95f, 0.78f), LockCol = new Rgba(1f, 0.3f, 0.25f), StunCol = new Rgba(0.43f, 0.88f, 1f);
        static readonly Rgba PushCol = new Rgba(0.6f, 0.69f, 0.88f), PushLead = new Rgba(0.8f, 0.87f, 1f), PushEdge = new Rgba(0.16f, 0.22f, 0.37f);

        /// On the floor, under everything else: the doors, the searchlight and the gates.
        void DrawWardenYard(float t)
        {
            var e = game.TheWarden();
            if (e == null || e.Warden == null)
                return;
            var w = e.Warden;
            bool riot = w.TransT > 0f ? w.TransTo >= WardenStage.Riot : w.Stage >= WardenStage.Riot;
            DrawRiotDoors(riot, t);
            if (w.Turn == WardenTurn.Light && w.TransT <= 0f)
                DrawSearchlight(w, t);
            DrawWatchtower(w, t);
            foreach (var g in w.Gates)
                DrawGate(g, t);
        }

        // --- The watchtower and its searchlight ---------------------------------------------------------

        void DrawWatchtower(WardenState w, float t)
        {
            bool lit = w.Turn == WardenTurn.Light && w.TransT <= 0f && (w.LightMode == WardenLight.Follow || w.LightMode == WardenLight.Lock);
            bool locked = lit && w.LightMode == WardenLight.Lock;
            var trim = new Rgba(1f, 0.9f, 0.62f, 0.5f);  // a corner tower, its lamp in the middle
            FillRect(0f, 0f, 80f, 80f, new Rgba(0.1f, 0.12f, 0.2f, 0.95f));
            Line(new V2(80f, 0f), new V2(80f, 80f), 2f, trim);
            Line(new V2(0f, 80f), new V2(80f, 80f), 2f, trim);
            Arc(Tower, 20f, 2f, trim, 24);
            if (lit)
            {
                var c = locked ? LockCol : LightCol;
                for (int k = 3; k >= 1; k--)
                    Circle(Tower, 12f + k * 11f, c.WithAlpha(0.09f * (4 - k)), 24);
                for (int k = 0; k < 6; k++)  // rays
                {
                    V2 d = V2.FromAngle(t * 0.4f + k * MathX.Pi / 3f);
                    Line(Tower + d * 12f, Tower + d * 30f, 1.5f, c.WithAlpha(0.45f));
                }
            }
            Circle(Tower, 9f, lit ? (locked ? new Rgba(1f, 0.6f, 0.5f) : new Rgba(1f, 0.97f, 0.86f)) : new Rgba(0.45f, 0.45f, 0.52f, 0.7f), 16);
        }

        /// A soft beam from the tower and a feathered pool of light where it lands, dust drifting in it. Red when
        /// it's locked on, dark while its shackle ring closes; it flickers on when it comes back.
        void DrawSearchlight(WardenState w, float t)
        {
            if (w.LightMode == WardenLight.Off || w.LightMode == WardenLight.Ring)
                return;
            bool locked = w.LightMode == WardenLight.Lock;
            float on = w.LightMode == WardenLight.Follow && w.LightT < 0.35f && ((int)(w.LightT * 20f) & 1) == 1 ? 0.35f : 1f;
            var c = locked ? LockCol : LightCol;
            V2 pool = w.LightDisp, from = Tower, d = pool - from;
            float dist = Mathf.Max(1f, d.Length), r = Game.LightRadius;
            V2 side = d.Normalized().Orthogonal();
            // The beam: widening from the lamp to the pool, a soft outer layer and a brighter core.
            SetColor(c.WithAlpha(0.07f * on));
            Quad(from - side * 6f, from + side * 6f, pool + side * r * 0.95f, pool - side * r * 0.95f);
            SetColor(c.WithAlpha(0.06f * on));
            Quad(from - side * 3f, from + side * 3f, pool + side * r * 0.5f, pool - side * r * 0.5f);
            // Dust drifting through it.
            for (int k = 0; k < 16; k++)
            {
                float f = MathX.Fmod(k * 0.137f + t * 0.06f, 1f), s = Mathf.Sin(k * 7.3f + t * 0.7f) * 0.8f;
                V2 p = from + d * f + side * (s * r * f * 0.85f);
                Circle(p, 1.6f + (k % 3) * 0.5f, c.WithAlpha(0.35f * on * (0.5f + 0.5f * Mathf.Sin(t * 3f + k))), 6);
            }
            // The pool: brighter towards the middle, a soft edge and a thin rim.
            for (int k = 0; k < 5; k++)
                Circle(pool, r * (1.2f - k * 0.18f), c.WithAlpha((0.035f + k * 0.018f) * on), 40);
            Arc(pool, r, 1.5f, c.WithAlpha(0.4f * on), 48);
            if (locked)  // brackets closing in on whoever it caught
            {
                float k = 1f - Mathf.Clamp01(w.LightT / 0.6f), br = 30f + k * 60f;
                for (int q = 0; q < 4; q++)
                    Arc(pool, br, 3f, LockCol.WithAlpha(0.7f + 0.3f * Mathf.Sin(t * 30f)), 6, w.LightT * 3f + q * MathX.Pi / 2f - 0.3f, 0.6f);
            }
        }

        // --- The gates ------------------------------------------------------------------------------------

        /// A gate of push bullets: steel-blue blocks, staggered like bricks, each with a chevron pointing the way
        /// it moves, so it reads as "this shoves you" rather than "this is a bullet". Its door gaps light up first.
        void DrawGate(WardenGate g, float t)
        {
            if (g.Warn > 0f)
            {
                float edge = g.Dir > 0f ? 0f : Game.Height - 14f, blink = 0.4f + 0.4f * Mathf.Sin(t * 20f);
                FillRect(0f, edge, Game.Width, 14f, new Rgba(0.45f, 0.65f, 1f, 0.22f * blink));
                FillRect(g.Gap1, g.Dir > 0f ? 0f : Game.Height - 18f, Game.GateDoor, 18f, new Rgba(0.5f, 1f, 0.65f, 0.8f));
                FillRect(g.Gap2, g.Dir > 0f ? 0f : Game.Height - 18f, Game.GateDoor, 18f, new Rgba(0.5f, 1f, 0.65f, 0.8f));
                return;
            }
            for (int row = 0; row < Game.GateRows; row++)
            {
                float y = g.Front - g.Dir * row * Game.GateRowGap;
                if (y < -20f || y > Game.Height + 20f)
                    continue;
                for (float x = 13f + (row % 2) * 13f; x < Game.Width; x += 26f)
                {
                    if ((x > g.Gap1 - 6f && x < g.Gap1 + Game.GateDoor + 6f) || (x > g.Gap2 - 6f && x < g.Gap2 + Game.GateDoor + 6f))
                        continue;
                    DrawPushBullet(new V2(x, y), g.Dir, row == 0);
                }
            }
        }

        void DrawPushBullet(V2 p, float dir, bool lead)
        {
            float r = Game.GateBulletR;
            Circle(p, r * 1.6f, new Rgba(0.65f, 0.78f, 1f, 0.16f), 10);
            SetColor(PushEdge);
            Quad(p + new V2(-r - 1f, -r - 1f), p + new V2(r + 1f, -r - 1f), p + new V2(r + 1f, r + 1f), p + new V2(-r - 1f, r + 1f));
            SetColor(lead ? PushLead : PushCol);
            Quad(p + new V2(-r, -r), p + new V2(r, -r), p + new V2(r, r), p + new V2(-r, r));
            var dark = new Rgba(0.1f, 0.14f, 0.27f, 0.95f);
            Line(p + new V2(-4f, -dir * 2f), p + new V2(0f, dir * 2.5f), 2f, dark);
            Line(p + new V2(0f, dir * 2.5f), p + new V2(4f, -dir * 2f), 2f, dark);
        }

        // --- The riot's doors -----------------------------------------------------------------------------

        void DrawRiotDoors(bool open, float t)
        {
            foreach (var d in Game.RiotDoors)
            {
                bool bottom = d.Y >= Game.Height - 1f;
                float x = bottom ? d.X - 55f : d.X <= 0f ? 0f : Game.Width - 16f, y = bottom ? Game.Height - 16f : d.Y - 55f;
                float w = bottom ? 110f : 16f, h = bottom ? 16f : 110f;
                FillRect(x, y, w, h, open ? new Rgba(1f, 0.5f, 0.15f, 0.3f + 0.08f * Mathf.Sin(t * 4f)) : new Rgba(0.6f, 0.65f, 0.78f, 0.22f));
                var bar = open ? new Rgba(1f, 0.6f, 0.3f, 0.8f) : new Rgba(0.75f, 0.8f, 0.9f, 0.5f);
                for (int i = 1; i < 5; i++)
                {
                    if (bottom)
                        Line(new V2(x + i * 22f, y), new V2(x + i * 22f, y + h), 2f, bar);
                    else
                        Line(new V2(x, y + i * 22f), new V2(x + w, y + i * 22f), 2f, bar);
                }
            }
        }

        // --- On the Warden itself -------------------------------------------------------------------------

        void DrawWardenUnderlay(Enemy e, V2 pos, float r, float t)
        {
            var w = e.Warden;
            if (w == null)
                return;
            if (w.TransT > 0f)  // changing stage (can't be hurt): StageShiftLook.cs
            {
                DrawStageShiftBoss(e, pos, r, t);
                return;
            }
            if (w.Stage >= WardenStage.Riot)  // red-hot
                Circle(pos, r + 18f, new Rgba(1f, 0.47f, 0.16f, 0.12f + 0.06f * Mathf.Sin(t * 8f)), 32);
            switch (w.Turn)
            {
                case WardenTurn.Cross:
                    if (!w.Dashing)
                        DrawWardenCrossGuides(e, pos, r);
                    break;
                case WardenTurn.Rioters:  // the riot shield, facing its target
                    Arc(pos, r + 16f, 18f, new Rgba(0.78f, 0.86f, 1f, 0.2f), 20, w.Aux - 1.05f, 2.1f);
                    Arc(pos, r + 16f, 7f, new Rgba(0.8f, 0.88f, 1f, 0.9f), 20, w.Aux - 1.05f, 2.1f);
                    break;
                case WardenTurn.Charge:
                    if (w.TurnT < 0.9f)  // the line it's about to charge down
                    {
                        float a = 0.35f + 0.35f * Mathf.Sin(t * 24f);
                        V2 d = V2.FromAngle(w.Aux);
                        Line(pos, pos + d * 620f, 92f, new Rgba(1f, 0.48f, 0.2f, a * 0.5f));
                        Line(pos, pos + d * 620f, 3f, new Rgba(1f, 0.6f, 0.3f, a));
                    }
                    break;
                case WardenTurn.LastStand:
                    Arc(pos, r + 10f + 3f * Mathf.Sin(t * 14f), 3f, new Rgba(1f, 0.4f, 0.2f, 0.8f), 32);
                    break;
            }
        }

        // --- Stunned players, stun shots and rioters --------------------------------------------------

        /// Stunned: crackling sparks round you and a ring that runs down with the stun.
        void DrawStunned(Player p, float t)
        {
            if (p.StunT <= 0f || p.Downed)
                return;
            V2 c = p.Disp;
            for (int i = 0; i < 5; i++)
            {
                float a = t * 12f + i * MathX.Tau / 5f;
                V2 p1 = c + V2.FromAngle(a) * 15f, p2 = c + V2.FromAngle(a + 0.4f) * 21f, p3 = c + V2.FromAngle(a + 0.15f) * 27f;
                Line(p1, p2, 2f, new Rgba(0.7f, 0.93f, 1f, 0.9f));
                Line(p2, p3, 2f, new Rgba(0.7f, 0.93f, 1f, 0.9f));
            }
            Arc(c, 32f, 3f, StunCol.WithAlpha(0.9f), 32, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.StunT / Game.StunTime));
        }

        /// The Warden's stun shot: an electric orb with a crackle of little arcs.
        void DrawStunShot(Bullet b, float t)
        {
            float r = b.Radius;
            Circle(b.Pos, r * 2.2f, StunCol.WithAlpha(0.22f), 14);
            Circle(b.Pos, r, StunCol, 12);
            Circle(b.Pos, r * 0.45f, new Rgba(0.92f, 0.98f, 1f), 8);
            for (int i = 0; i < 3; i++)
            {
                float a = t * 9f + i * 2.1f + b.Id;
                V2 p1 = b.Pos + V2.FromAngle(a) * r, p2 = b.Pos + V2.FromAngle(a + 0.35f) * r * 1.6f, p3 = b.Pos + V2.FromAngle(a + 0.1f) * r * 2.1f;
                Line(p1, p2, 1.5f, new Rgba(0.8f, 0.96f, 1f, 0.85f));
                Line(p2, p3, 1.5f, new Rgba(0.8f, 0.96f, 1f, 0.85f));
            }
        }

        /// A guard: a riot helmet's visor and a baton held out the way it's facing.
        void DrawGuardDetails(Enemy e, V2 pos, float r, float t)
        {
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            Line(pos + f * (r * 0.25f) - side * (r * 0.55f), pos + f * (r * 0.25f) + side * (r * 0.55f), 3.5f, new Rgba(0.75f, 0.9f, 1f, 0.9f));  // visor
            V2 grip = pos + side * (r * 0.8f), tip = grip + V2.FromAngle(e.Facing + 0.5f * Mathf.Sin(t * 9f + e.Id)) * (r + 12f);
            Line(grip, tip, 4f, new Rgba(0.12f, 0.14f, 0.22f));  // the baton
            Circle(tip, 2.5f, new Rgba(0.6f, 0.65f, 0.75f), 6);
        }

        /// A rioter: an orange jumpsuit with a dark band across it.
        void DrawRioterDetails(Enemy e, V2 pos, float r, Rgba col)
        {
            Line(pos + new V2(-r * 0.75f, r * 0.15f), pos + new V2(r * 0.75f, r * 0.15f), 3f, new Rgba(0.15f, 0.12f, 0.16f, 0.85f));
            Circle(pos + new V2(0f, -r * 0.3f), r * 0.28f, col.Mix(Rgba.White, 0.35f), 10);
        }
    }
}

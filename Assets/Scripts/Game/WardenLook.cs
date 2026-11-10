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
            if ((w.Turn == WardenTurn.Light || w.Turn == WardenTurn.LastStand) && w.TransT <= 0f)
                DrawSearchlight(w, t);
            DrawWatchtower(w, t);
            foreach (var g in w.Gates)
                DrawGate(g, t);
        }

        // --- The watchtower and its searchlight ---------------------------------------------------------

        void DrawWatchtower(WardenState w, float t)
        {
            bool lit = (w.Turn == WardenTurn.Light || w.Turn == WardenTurn.LastStand) && w.TransT <= 0f && (w.LightMode == WardenLight.Follow || w.LightMode == WardenLight.Lock);
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
            if (w.Stage == WardenStage.LastStand && w.TransT <= 0f)
            {
                // The alarm beacon on the tower: a steady red lamp (no flashing or sweeping beam).
                Circle(Tower, 11f, new Rgba(1f, 0.2f, 0.15f, 0.25f), 14);
                Circle(Tower, 6f, new Rgba(1f, 0.25f, 0.2f, 0.9f), 12);
            }
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

        /// Players' Searchlights (Stun.cs): a beam from the player to a pool of light on their cursor, rimmed in their
        /// colour, with every enemy caught in it outlined (they take extra damage while lit).
        void DrawPlayerSearchlights(float t)
        {
            foreach (var p in game.Players.Values)
            {
                if (p.Stats.Searchlight <= 0 || p.Downed)
                    continue;
                V2 pool = p.LightDisp, d = pool - p.Disp;
                float r = Game.SearchlightRadius(p);
                if (d.Length > r)
                {
                    V2 side = d.Normalized().Orthogonal();
                    SetColor(LightCol.WithAlpha(0.05f));
                    Quad(p.Disp - side * 4f, p.Disp + side * 4f, pool + side * r * 0.9f, pool - side * r * 0.9f);
                }
                for (int k = 0; k < 4; k++)
                    Circle(pool, r * (1.1f - k * 0.2f), LightCol.WithAlpha(0.03f + k * 0.015f), 40);
                Arc(pool, r, 1.5f, p.Def.Color.Mix(LightCol, 0.5f).WithAlpha(0.55f), 48);
                foreach (var e in game.Enemies.Values)
                    if (!game.IsSpawning(e) && e.Disp.DistanceTo(pool) < r + e.Radius * 0.5f)
                        Arc(e.Disp, e.Radius + 5f, 2f, LightCol.WithAlpha(0.6f + 0.3f * Mathf.Sin(t * 10f + e.Id)), 24);
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
                FillRect(g.LockX - Game.LockW / 2f, g.Dir > 0f ? 0f : Game.Height - 18f, Game.LockW, 18f, new Rgba(1f, 0.8f, 0.3f, 0.85f));  // where its lock will be
                return;
            }
            if (Game.GateHolding(g))  // stopped just inside: the doors light up for a moment
            {
                float top = Mathf.Min(g.Front, Game.GateBack(g)) - Game.GateBulletR - 4f, h = Game.GateRowGap + Game.GateBulletR * 2f + 8f;
                float glow = 0.35f + 0.25f * Mathf.Sin(t * 30f);
                FillRect(g.Gap1, top, Game.GateDoor, h, new Rgba(0.5f, 1f, 0.65f, glow));
                FillRect(g.Gap2, top, Game.GateDoor, h, new Rgba(0.5f, 1f, 0.65f, glow));
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
                    if (Mathf.Abs(x - g.LockX) < (g.LockOpen ? Game.LockDoor / 2f : Game.LockW / 2f) + 6f)
                        continue;  // the lock, or the door it left
                    DrawPushBullet(new V2(x, y), g.Dir, row == 0);
                }
            }
            if (!g.LockOpen)
                DrawGateLock(g, t);
        }

        /// A gate's gold lock: a padlock block across both rows, cracking as it's shot. Shot out, it leaves a door.
        void DrawGateLock(WardenGate g, float t)
        {
            float back = Game.GateBack(g), top = Mathf.Min(g.Front, back) - Game.GateBulletR - 3f, bottom = Mathf.Max(g.Front, back) + Game.GateBulletR + 3f;
            float x = g.LockX - Game.LockW / 2f, w = Game.LockW, h = bottom - top;
            float glow = 0.5f + 0.5f * Mathf.Sin(t * 5f);
            FillRect(x - 4f, top - 4f, w + 8f, h + 8f, new Rgba(1f, 0.8f, 0.3f, 0.15f + 0.1f * glow));
            FillRect(x, top, w, h, new Rgba(0.6f, 0.42f, 0.1f));
            FillRect(x + 2f, top + 2f, w - 4f, h - 4f, new Rgba(1f, 0.8f, 0.3f));
            Arc(new V2(g.LockX, top + 1f), 6f, 2.5f, new Rgba(0.75f, 0.55f, 0.15f), 10, MathX.Pi, MathX.Pi);  // the shackle
            V2 c = new V2(g.LockX, (top + bottom) / 2f);
            Circle(c - new V2(0f, 2f), 2.6f, new Rgba(0.2f, 0.12f, 0.03f), 8);  // the keyhole
            FillRect(c.X - 1f, c.Y - 1f, 2f, 6f, new Rgba(0.2f, 0.12f, 0.03f));
            int lost = Game.LockHits - g.LockHp;  // cracks as it's shot
            var crack = new Rgba(0.35f, 0.2f, 0.05f);
            for (int i = 0; i < lost && i < 5; i++)
            {
                float sx = x + 4f + (i * 7 % 16), sy = top + 4f + (i * 11 % (int)Mathf.Max(6f, h - 8f));
                Line(new V2(sx, sy), new V2(sx + 5f, sy + 4f), 1.2f, crack);
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
            DrawWardenArmor(w, pos, r, t);  // the last stand's armor, or plates locking on as it armors up
            if (w.ExposedT > 0f)  // caught in its own light: a white glare round it
                for (int k = 3; k >= 1; k--)
                    Circle(pos, r + 12f + k * 14f, new Rgba(1f, 0.97f, 0.85f, 0.08f * (4 - k)), 32);
            if (w.Turn == WardenTurn.Fan && w.FanWarn > 0f && w.StunT <= 0f && w.ExposedT <= 0f)
            {
                // Where the next volley goes: nine red lanes, brightening until it fires.
                float k = 1f - w.FanWarn / Game.FanWarnTime;
                for (int i = 0; i < 9; i++)
                {
                    V2 d = V2.FromAngle(w.FanAim + (i - 4) * 0.12f);
                    Line(pos + d * (r + 4f), pos + d * (r + 300f), 2f + 2f * k, new Rgba(1f, 0.15f, 0.12f, 0.15f + 0.45f * k));
                }
                Arc(pos, r + 6f, 4f, new Rgba(1f, 0.2f, 0.15f, 0.4f + 0.5f * k), 32, w.FanAim - 0.6f, 1.2f);
            }
            switch (w.Turn)
            {
                case WardenTurn.Cross:
                    if (!w.Dashing)
                        DrawWardenCrossGuides(e, pos, r);
                    break;
                case WardenTurn.Rioters:  // the riot shield, facing its target: cracking as it wears, gone once broken
                {
                    int crack = Game.WardenShieldCrack(w);
                    if (crack >= 4)
                    {
                        // Broken: a red-hot ring of exposure, and a few shards still on its arm.
                        Arc(pos, r + 8f, 3f, new Rgba(1f, 0.45f, 0.3f, 0.55f + 0.25f * Mathf.Sin(t * 10f)), 28);
                        for (int k = 0; k < 3; k++)
                            Arc(pos, r + 16f, 5f, new Rgba(0.6f, 0.66f, 0.78f, 0.6f), 4, w.Aux - 0.9f + k * 0.7f, 0.25f);
                        break;
                    }
                    float dim = 1f - 0.15f * crack;
                    Arc(pos, r + 16f, 18f, new Rgba(0.78f, 0.86f, 1f, 0.2f * dim), 20, w.Aux - 1.05f, 2.1f);
                    Arc(pos, r + 16f, 7f, new Rgba(0.8f, 0.88f, 1f, 0.9f * dim), 20, w.Aux - 1.05f, 2.1f);
                    for (int k = 0; k < crack * 2; k++)  // cracks across its face, more as it wears
                    {
                        float a = w.Aux - 0.9f + (k * 0.53f) % 1.8f;
                        V2 o = pos + V2.FromAngle(a) * (r + 8f), m = pos + V2.FromAngle(a + 0.08f) * (r + 16f), q = pos + V2.FromAngle(a - 0.05f) * (r + 25f);
                        Line(o, m, 1.6f, new Rgba(0.15f, 0.18f, 0.28f, 0.9f));
                        Line(m, q, 1.6f, new Rgba(0.15f, 0.18f, 0.28f, 0.9f));
                    }
                    break;
                }
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
                {
                    Arc(pos, r + 10f + 3f * Mathf.Sin(t * 14f), 3f, new Rgba(1f, 0.4f, 0.2f, 0.8f), 32);
                    // The pause before each burst: four red lanes where its streams will go, brightening until they fire.
                    float into = MathX.Fmod(w.TurnT, Game.LastStandCycle), facing = Game.LastStandFacing(w.TurnT);
                    if (into < Game.LastStandGap && w.StunT <= 0f && w.ExposedT <= 0f)
                    {
                        float k = into / Game.LastStandGap;
                        for (int i = 0; i < 4; i++)
                        {
                            V2 d = V2.FromAngle(facing + MathX.Pi / 2f * i);
                            Line(pos + d * (r + 4f), pos + d * (r + 520f), 14f, new Rgba(1f, 0.15f, 0.12f, 0.06f + 0.12f * k));
                            Line(pos + d * (r + 4f), pos + d * (r + 520f), 2f + 2f * k, new Rgba(1f, 0.2f, 0.15f, 0.2f + 0.5f * k));
                        }
                    }
                    break;
                }
            }
        }

        // --- Stunned players, stun shots and rioters --------------------------------------------------

        /// Stunned: crackling sparks round you and a ring that runs down with the stun.
        void DrawStunned(Player p, float t)
        {
            if (p.StunT <= 0f || p.Downed || HeldByCrawler(p))
                return;  // (a Crawler's hold shows its vines instead: ZombieLook.cs)
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

        /// The Stun Gun drawn (Stun.cs): a stubby gun out in front with crackling prongs, and a pip per shot left.
        void DrawHeldStunGun(Player p, float t)
        {
            if (p.StunShots <= 0 || p.Downed)
                return;
            V2 dir = V2.FromAngle(p.Aim), side = dir.Orthogonal(), grip = p.Disp + dir * (p.Def.BodyR + 2f);
            Line(grip, grip + dir * 11f, 7f, new Rgba(0.2f, 0.24f, 0.32f));
            Line(grip + dir * 2f, grip + dir * 9f, 3f, new Rgba(0.98f, 0.8f, 0.25f));  // the warning stripe
            for (int k = -1; k <= 1; k += 2)
                Line(grip + dir * 11f + side * (2.5f * k), grip + dir * 15f + side * (2.5f * k), 1.5f, StunCol);
            if (Mathf.Sin(t * 30f + p.Id) > 0f)
                Line(grip + dir * 15f + side * 2.5f, grip + dir * 15f - side * 2.5f, 1.5f, new Rgba(0.9f, 0.98f, 1f, 0.9f));
            int max = Mathf.Max(Game.StunGunShots, p.StunShots);
            float r = p.Def.BodyR + 13f, span = Mathf.Min(MathX.Pi * 0.9f, max * 0.2f);
            for (int i = 0; i < max; i++)
            {
                float a = MathX.Pi / 2f + span / 2f - span * (max == 1 ? 0.5f : (float)i / (max - 1));
                Circle(p.Disp + V2.FromAngle(a) * r, 2.8f, StunCol.WithAlpha(i < p.StunShots ? 0.95f : 0.15f), 8);
            }
        }

        /// A Warden's Guard (Stun.cs): one of the riot's guards (same body, visor and baton), on your side: a ring in its
        /// owner's colour under it, and a health bar once it's been hurt.
        void DrawWardenGuard(Minion m, float t)
        {
            var c = m.Def.Color;
            float r = m.Def.Radius;
            Player owner;
            var mark = game.Players.TryGetValue(m.Owner, out owner) ? owner.Def.Color : Rgba.White;
            Circle(m.Disp, r + 7f, mark.WithAlpha(0.18f), 20);
            Arc(m.Disp, r + 6f, 2f, mark.WithAlpha(0.8f), 24);
            Polygon(m.Disp, r, m.Def.Sides, m.Aim, c);
            V2 f = V2.FromAngle(m.Aim), side = f.Orthogonal();
            Line(m.Disp + f * (r * 0.25f) - side * (r * 0.55f), m.Disp + f * (r * 0.25f) + side * (r * 0.55f), 3.5f, new Rgba(0.75f, 0.9f, 1f, 0.9f));  // visor
            V2 grip = m.Disp + side * (r * 0.8f), tip = grip + V2.FromAngle(m.Aim + 0.5f * Mathf.Sin(t * 9f + m.Id)) * (r + 12f);
            Line(grip, tip, 4f, new Rgba(0.12f, 0.14f, 0.22f));  // the baton
            Circle(tip, 2.5f, new Rgba(0.6f, 0.65f, 0.75f), 6);
            if (m.GuardFrac < 0.999f)
                Bar(m.Disp + new V2(-15f, -r - 10f), 30f, 3.5f, Mathf.Clamp01(m.GuardFrac), mark);
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

        static readonly Rgba RiotSteel = new Rgba(0.62f, 0.74f, 0.98f);

        /// A Riot Shield: a guard's visor behind a tall slab of a shield. The shield cracks as it wears (Phase's low bits),
        /// flashes as it winds up a bash, glows through the lunge and is held low after it; once broken, a flailing baton.
        void DrawRiotShield(Enemy e, V2 pos, float r, float t)
        {
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            byte st = Game.RiotState(e), crack = Game.RiotCrack(e);
            Line(pos + f * (r * 0.1f) - side * (r * 0.5f), pos + f * (r * 0.1f) + side * (r * 0.5f), 3f, new Rgba(0.75f, 0.9f, 1f, 0.9f));  // visor
            if (crack >= Game.RiotCracked)
            {
                // Shield gone: a baton flailing fast.
                V2 grip = pos + side * (r * 0.8f), tip = grip + V2.FromAngle(e.Facing + 0.9f * Mathf.Sin(t * 18f + e.Id)) * (r + 12f);
                Line(grip, tip, 4f, new Rgba(0.12f, 0.14f, 0.22f));
                Circle(tip, 2.5f, new Rgba(0.6f, 0.65f, 0.75f), 6);
                if (st == Game.RiotStaggered)  // stars round its head
                    for (int k = 0; k < 3; k++)
                        Circle(pos + V2.FromAngle(t * 6f + k * MathX.Tau / 3f) * (r * 0.7f) + new V2(0f, -r * 0.4f), 2f, new Rgba(1f, 0.95f, 0.6f, 0.9f), 6);
                return;
            }
            bool low = st == Game.RiotLow;
            // The shield: a tall slab across its front (shorter and off to the side when held low after a bash).
            float half = low ? r * 0.8f : r * 1.6f, depth = 10f;
            V2 c = pos + f * (r + (st == Game.RiotLunging ? 11f : 7f)) + (low ? side * (r * 0.7f) : V2.Zero);
            V2 a = c - side * half, b = c + side * half, a2 = a + f * depth, b2 = b + f * depth;
            float wear = crack / 4f;
            Rgba face = RiotSteel.WithAlpha(low ? 0.5f : 0.92f - 0.18f * wear);
            if (st == Game.RiotLunging)  // (the wind-up's warning is the shared lane under it: LungeLaneLook.cs)
                face = face.Mix(Rgba.White, 0.65f);
            var edge = new Rgba(0.1f, 0.13f, 0.24f, 0.95f);
            Line(a + f * (depth / 2f), b + f * (depth / 2f), depth, face);
            Line(a, b, 1.6f, edge);
            Line(a2, b2, 2f, new Rgba(0.92f, 0.96f, 1f, 0.95f));  // the bright front rim
            Line(a, a2, 1.6f, edge);
            Line(b, b2, 1.6f, edge);
            // A window to see through, and the stripe across it.
            Line(c + f * (depth * 0.45f) - side * (half * 0.3f), c + f * (depth * 0.45f) - side * (half * 0.05f), 3f, new Rgba(0.18f, 0.26f, 0.42f, 0.9f));
            Line(c + f * (depth * 0.45f) + side * (half * 0.15f), c + f * (depth * 0.45f) + side * (half * 0.55f), 2f, new Rgba(1f, 1f, 1f, 0.55f));
            // Cracks: fixed for each shield (from its id), more at each stage.
            for (int k = 0; k < crack * 2; k++)
            {
                float u = -0.85f + 1.7f * (((e.Id * 37 + k * 61) % 97) / 96f);
                V2 p0 = c + side * (half * u) + f * depth, p1 = p0 - f * (depth * 0.55f) + side * (k % 2 == 0 ? 4f : -4f), p2 = p1 - f * (depth * 0.45f) + side * (k % 2 == 0 ? -2f : 2f);
                Line(p0, p1, 1.5f, edge);
                Line(p1, p2, 1.5f, edge);
            }
        }

        /// A Gas Grenadier: a gas mask (two round lenses and a filter) and a canister in hand.
        void DrawGrenadier(Enemy e, V2 pos, float r, float t)
        {
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            var lens = new Rgba(0.15f, 0.2f, 0.12f, 0.95f);
            Circle(pos + f * (r * 0.25f) - side * (r * 0.32f), r * 0.22f, lens, 10);
            Circle(pos + f * (r * 0.25f) + side * (r * 0.32f), r * 0.22f, lens, 10);
            Circle(pos + f * (r * 0.62f), r * 0.2f, new Rgba(0.35f, 0.4f, 0.3f), 8);  // the filter
            V2 can = pos + side * (r * 0.95f) + f * (r * 0.3f);
            Circle(can, 4.5f, new Rgba(0.7f, 0.95f, 0.45f), 10);
            float puff = MathX.Fmod(t * 0.8f + e.Id * 0.37f, 1f);
            Circle(can + new V2(0f, -6f - puff * 10f), 2f + puff * 3f, new Rgba(0.65f, 0.95f, 0.45f, 0.5f * (1f - puff)), 8);
        }

        /// A rioter: an orange jumpsuit with a dark band across it.
        void DrawRioterDetails(Enemy e, V2 pos, float r, Rgba col)
        {
            Line(pos + new V2(-r * 0.75f, r * 0.15f), pos + new V2(r * 0.75f, r * 0.15f), 3f, new Rgba(0.15f, 0.12f, 0.16f, 0.85f));
            Circle(pos + new V2(0f, -r * 0.3f), r * 0.28f, col.Mix(Rgba.White, 0.35f), 10);
        }

        // --- Over the Warden: blinded, stunned; the last stand's alarm; the HUD tags ---------------------

        /// Drawn over the enemies: the glare while it's blinded by its own light, the stars while it's stunned.
        void DrawWardenOver(float t)
        {
            var e = game.TheWarden();
            if (e == null || e.Warden == null || e.Warden.TransT > 0f)
                return;
            var w = e.Warden;
            V2 pos = e.Disp;
            float r = e.Radius;
            if (w.ExposedT > 0f)
            {
                float flick = 0.55f + 0.25f * Mathf.Sin(t * 22f);
                Circle(pos, r, new Rgba(1f, 0.98f, 0.9f, 0.35f * flick), 28);
                for (int k = 0; k < 10; k++)  // the glare's rays
                {
                    V2 d = V2.FromAngle(t * 0.7f + k * MathX.Tau / 10f);
                    Line(pos + d * (r + 4f), pos + d * (r + 22f + 8f * Mathf.Sin(t * 9f + k)), 2f, new Rgba(1f, 0.97f, 0.8f, 0.7f));
                }
                Arc(pos, r + 30f, 3f, new Rgba(1f, 0.85f, 0.4f, 0.9f), 40, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(w.ExposedT / Game.ExposeTime));
            }
            if (w.StunT > 0f)
            {
                for (int i = 0; i < 5; i++)  // stars circling over its head
                {
                    V2 o = V2.FromAngle(t * 5f + i * MathX.Tau / 5f);
                    V2 s = pos + new V2(o.X * r * 0.8f, -r - 10f + o.Y * 9f);
                    var col = new Rgba(1f, 0.9f, 0.35f, 0.95f);
                    Line(s - new V2(5f, 0f), s + new V2(5f, 0f), 2f, col);
                    Line(s - new V2(0f, 5f), s + new V2(0f, 5f), 2f, col);
                }
                for (int i = 0; i < 6; i++)  // and the stun crackling round it
                {
                    float a = t * 10f + i * MathX.Tau / 6f;
                    Line(pos + V2.FromAngle(a) * (r + 2f), pos + V2.FromAngle(a + 0.3f) * (r + 12f), 2f, StunCol.WithAlpha(0.9f));
                }
                Arc(pos, r + 18f, 3f, StunCol.WithAlpha(0.9f), 40, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(w.StunT / Game.WardenStunTime));
            }
        }

        const float AlarmSight = 190f, AlarmEdge = 80f, AlarmCell = 30f;
        float[] alarmAlpha;

        static readonly Rgba ArmorSteel = new Rgba(0.55f, 0.68f, 0.88f), ArmorDark = new Rgba(0.18f, 0.22f, 0.32f);

        /// The Warden's last-stand armor: five steel plates round it with a bright rim, falling away one by one as the
        /// armor wears down. While it armors up (the 2 s stage change), each plate flies in from outside and locks on at
        /// Game.ArmorPlateAt, with a small white flash (and a clang, from the host).
        void DrawWardenArmor(WardenState w, V2 pos, float r, float t)
        {
            const int plates = 5;
            float seg = MathX.Tau / plates, gapA = 0.1f, rr = r + 7f;
            bool arming = w.TransT > 0f && w.TransTo == WardenStage.LastStand;
            if (!arming && w.Armor <= 0f)
                return;
            float elapsed = 2f - w.TransT;
            int whole = arming ? plates : Mathf.CeilToInt(plates * Mathf.Clamp01(w.Armor / Mathf.Max(1f, w.ArmorMax)));
            for (int k = 0; k < plates; k++)
            {
                float from = -MathX.Pi / 2f + seg * k + gapA / 2f, span = seg - gapA, mid = from + span / 2f;
                V2 at = pos;
                float alpha = 1f;
                if (arming)
                {
                    float land = Game.ArmorPlateAt[k], f = Mathf.Clamp01((elapsed - (land - 0.4f)) / 0.4f);
                    if (f <= 0f)
                        continue;
                    at = pos + V2.FromAngle(mid) * (220f * (1f - f) * (1f - f));  // flying in, slowing as it arrives
                    alpha = Mathf.Min(1f, f * 3f);
                    float flash = Mathf.Clamp01(1f - (elapsed - land) / 0.25f);
                    if (elapsed >= land && flash > 0f)
                        Arc(pos, rr + 2f, 9f, new Rgba(1f, 1f, 1f, 0.6f * flash), 8, from, span);  // it locks on
                }
                else if (k >= whole)
                    continue;  // that plate has broken off
                Arc(at, rr, 9f, ArmorDark.WithAlpha(0.95f * alpha), 8, from, span);
                Arc(at, rr + 2.5f, 3.5f, ArmorSteel.WithAlpha(alpha), 8, from, span);
                Arc(at, rr + 4f, 1.2f, new Rgba(0.9f, 0.95f, 1f, 0.8f * alpha), 6, from + span * 0.15f, span * 0.4f);  // a sheen
                V2 rivet = at + V2.FromAngle(mid) * (rr - 1f);
                Circle(rivet, 1.8f, new Rgba(0.85f, 0.9f, 1f, alpha), 6);
            }
        }

        /// The last stand's alarm: the lights go out (lit round each player, the Warden and its searchlight's pool) and
        /// the dark has a steady red tint. Nothing flashes or pulses (the owner asked for no flashing lights here).
        void DrawWardenAlarm(float t)
        {
            var e = game.TheWarden();
            if (e == null || e.Warden == null || e.Warden.Stage != WardenStage.LastStand || e.Warden.TransT > 0f)
                return;
            var w = e.Warden;
            int nx = Mathf.CeilToInt(AW / AlarmCell), ny = Mathf.CeilToInt(AH / AlarmCell);
            if (alarmAlpha == null || alarmAlpha.Length < (nx + 1) * (ny + 1))
                alarmAlpha = new float[(nx + 1) * (ny + 1)];
            bool light = w.LightMode == WardenLight.Follow || w.LightMode == WardenLight.Lock;
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    V2 p = new V2(Mathf.Min(AW, i * AlarmCell), Mathf.Min(AH, j * AlarmCell));
                    float dark = Smooth((p.DistanceTo(e.Disp) - e.Radius - 40f) / AlarmEdge);
                    foreach (var pl in game.Players.Values)
                        dark = Mathf.Min(dark, Smooth((p.DistanceTo(pl.Disp) - AlarmSight) / AlarmEdge));
                    if (light)
                        dark = Mathf.Min(dark, Smooth((p.DistanceTo(w.LightDisp) - Game.LightRadius) / 40f));
                    alarmAlpha[j * (nx + 1) + i] = 0.82f * dark;
                }
            alarmCol = new Color(0.09f, 0.005f, 0.01f, 1f);  // a steady red-black
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    float a00 = alarmAlpha[k], a10 = alarmAlpha[k + 1], a01 = alarmAlpha[k + nx + 1], a11 = alarmAlpha[k + nx + 2];
                    if (a00 + a10 + a01 + a11 <= 0.001f)
                        continue;
                    V2 p00 = new V2(i * AlarmCell, j * AlarmCell), p11 = new V2(Mathf.Min(AW, p00.X + AlarmCell), Mathf.Min(AH, p00.Y + AlarmCell));
                    V2 p10 = new V2(p11.X, p00.Y), p01 = new V2(p00.X, p11.Y);
                    AlarmVertex(p00, a00); AlarmVertex(p10, a10); AlarmVertex(p11, a11);
                    AlarmVertex(p00, a00); AlarmVertex(p11, a11); AlarmVertex(p01, a01);
                }
            // A faint, steady red along the walls.
            var wash = new Rgba(1f, 0.1f, 0.08f, 0.08f);
            FillRect(0f, 0f, AW, 10f, wash);
            FillRect(0f, AH - 10f, AW, 10f, wash);
            FillRect(0f, 0f, 10f, AH, wash);
            FillRect(AW - 10f, 0f, 10f, AH, wash);
        }

        Color alarmCol;

        void AlarmVertex(V2 p, float a)
        {
            glCur = new Color(alarmCol.r, alarmCol.g, alarmCol.b, a);
            GL.Color(glCur);
            Vtx(Px(p));  // through Vtx, so a held frame replays it (Juice.cs)
        }

        /// HUD: "EXPOSED" or "STUNNED" over the Warden while it lasts.
        void DrawWardenTags()
        {
            var e = game.TheWarden();
            if (e == null || e.Warden == null || e.Warden.TransT > 0f)
                return;
            var w = e.Warden;
            string tag = w.StunT > 0f ? "STUNNED" : w.ExposedT > 0f ? "EXPOSED  ·  +" + Mathf.RoundToInt((Game.ExposedMult - 1f) * 100f) + "% DAMAGE" : null;
            if (tag == null)
                return;
            V2 at = ToUi(e.Disp);
            float r = e.Radius * UiWorldScale;
            var col = w.StunT > 0f ? new Color(0.55f, 0.9f, 1f) : new Color(1f, 0.88f, 0.45f);
            Ui.Shadowed(new Rect(at.X - 160f, at.Y - r - 52f, 320f, 22f), tag, 16, new Color(col.r, col.g, col.b, 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 8f)));
        }
    }
}

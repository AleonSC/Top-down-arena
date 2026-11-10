using System;
using System.Collections.Generic;

namespace Arena
{
    /// The four ghosts are the roles (Docs/Pacman.md): DPS red chases you, Tank cyan cuts you off, Healer pink ambushes ahead, Wildcard orange
    /// chases and then wanders off when close. Same order as the classic Blinky, Inky, Pinky, Clyde: red, cyan, pink, orange.
    public enum GhostRole : byte { Dps, Tank, Healer, Wildcard }

    public enum GhostMode : byte { House, Leaving, Scatter, Chase, Frightened, Eaten, Entering }

    public enum PacState : byte { Ready, Playing, LevelClear, Over }

    public enum PacEvent : byte { Start, Dot, Pellet, EatGhost, Death, ExtraLife, LevelClear, GameOver }

    /// Anything that moves on the maze: a position in tile units (a tile's centre is a whole number), a direction and a speed.
    public class PacMover
    {
        public float X, Y;
        public int Dx, Dy;
    }

    public class PacChomper : PacMover
    {
        public int Id;
        public int WantX, WantY;        // the turn the player is holding: it happens at the next junction it fits
        public float DeadT;              // > 0: dying (counts down); then it respawns, or stays Out when there are no lives left
        public float InvulnT;            // just respawned: ghosts pass through
        public bool Out;                 // out of lives
        public int Score;
        public int StartX, StartY;
        public bool Alive => DeadT <= 0f && !Out;
    }

    public class PacGhost : PacMover
    {
        public GhostRole Role;
        public GhostMode Mode;
        public bool Reverse;             // turn round at the next tile centre (a mode change)
        public int HomeX, HomeY;
        public int TargetX, TargetY;     // the tile it is heading for (for readouts and tests)
        public float StuckT;             // time in the house (the release timer)
    }

    /// The classic Pac-Man maze game, without Unity (Docs/Pacman.md, step 2): a 28 x 31 maze, chompers steered corridor to corridor
    /// with a queued turn, four ghosts with their own chase rules, scatter / chase waves, power pellets, lives, levels. 1-3 chompers.
    /// Host-authoritative: the host calls Step, everyone else will get snapshots (step 4).
    public class PacmanGame
    {
        public const int W = 28, H = 31;
        public const float BaseSpeed = 9.47f;   // tiles a second at "100%" (the original's 75.75 pixels a second over 8-pixel tiles)
        public const int DoorX = 13, DoorY = 12, ExitY = 11, HouseY = 14;
        public const int TunnelRow = 14;

        // Left half of the maze (columns 0-13); the right half is its mirror. # wall, x outside, . dot, o power pellet, ' ' empty, = ghost door.
        static readonly string[] LeftHalf =
        {
            "##############",
            "#............#",
            "#.####.#####.#",
            "#o####.#####.#",
            "#.####.#####.#",
            "#.............",
            "#.####.##.####",
            "#.####.##.####",
            "#......##....#",
            "######.##### #",
            "xxxxx#.##### #",
            "xxxxx#.##     ",
            "xxxxx#.## ###=",
            "######.## #   ",
            "      .   #   ",
            "######.## #   ",
            "xxxxx#.## ####",
            "xxxxx#.##     ",
            "xxxxx#.## ####",
            "######.## ####",
            "#............#",
            "#.####.#####.#",
            "#.####.#####.#",
            "#o..##....... ",
            "###.##.##.####",
            "###.##.##.####",
            "#......##....#",
            "#.##########.#",
            "#.##########.#",
            "#.............",
            "##############",
        };

        public static readonly string[] Layout = BuildLayout();

        static string[] BuildLayout()
        {
            var rows = new string[H];
            for (int y = 0; y < H; y++)
            {
                string l = LeftHalf[y];
                var r = new char[W];
                for (int x = 0; x < 14; x++)
                {
                    r[x] = l[x];
                    r[W - 1 - x] = l[x];
                }
                rows[y] = new string(r);
            }
            return rows;
        }

        // ---- state ----
        public int Level = 1, Score, Lives = 3, DotsLeft, DotsEaten, DotsTotal;
        public PacState State = PacState.Ready;
        public float StateT = ReadyTime;
        public float FrightT, ModeT;
        public int ModeIdx;
        public int Combo;                // ghosts eaten on this power pellet
        public bool ExtraGiven;
        public readonly char[,] Cell = new char[W, H];   // '#' wall (x counts as wall), '.' dot, 'o' pellet, ' ' empty, '=' door
        public readonly List<PacChomper> Chompers = new List<PacChomper>();
        public readonly PacGhost[] Ghosts = new PacGhost[4];
        public readonly List<PacEvent> Events = new List<PacEvent>();   // for the front end's sounds; clear it after reading
        public float NoDotT;             // time since a dot was eaten (a ghost waits no longer than this in the house)
        uint rng;

        public const float ReadyTime = 2.5f, ClearTime = 2.4f, DieTime = 1.6f, InvulnTime = 2f;
        static readonly float[] WaveTimes = { 7f, 20f, 7f, 20f, 5f, 20f, 5f, -1f };   // scatter, chase, ... ; -1 = for good
        static readonly int[] ReleaseDots = { 0, 30, 0, 60 };                         // by GhostRole: Dps (starts outside), Tank, Healer, Wildcard

        public PacmanGame(int players = 1, int seed = 1)
        {
            rng = (uint)(seed * 2654435761u + 12345u);
            if (rng == 0) rng = 1;
            int n = Math.Max(1, Math.Min(3, players));
            int[] sx = { 13, 14, 12 };
            for (int i = 0; i < n; i++)
                Chompers.Add(new PacChomper { Id = i, StartX = sx[i], StartY = 23 });
            for (int i = 0; i < 4; i++)
                Ghosts[i] = new PacGhost { Role = (GhostRole)i };
            LoadLevel();
        }

        // ---- the maze ----

        public static bool IsWallTile(char c) => c == '#' || c == 'x';

        /// Whether a tile can be walked: walls never; the door only for ghosts going in or out.
        public bool Open(int x, int y, bool door = false)
        {
            if (y < 0 || y >= H)
                return false;
            x = ((x % W) + W) % W;
            char c = Cell[x, y];
            return c == '.' || c == 'o' || c == ' ' || (door && c == '=');
        }

        void LoadLevel()
        {
            DotsLeft = DotsTotal = 0;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    char c = Layout[y][x];
                    if (c == 'x') c = '#';
                    // the tunnel row has no dots
                    if (c == '.' && NoDotZone(x, y)) c = ' ';
                    Cell[x, y] = c;
                    if (c == '.' || c == 'o') { DotsLeft++; DotsTotal++; }
                }
            DotsEaten = 0;
            ResetActors();
            ModeIdx = 0; ModeT = 0f; FrightT = 0f; Combo = 0; NoDotT = 0f;
            State = PacState.Ready;
            StateT = ReadyTime;
            Events.Add(PacEvent.Start);
        }

        static bool NoDotZone(int x, int y)
        {
            return y == TunnelRow;   // the tunnel row has no dots
        }

        void ResetActors()
        {
            foreach (var c in Chompers)
            {
                c.X = c.StartX; c.Y = c.StartY; c.Dx = 0; c.Dy = 0; c.WantX = 0; c.WantY = 0;
                if (!c.Out) { c.DeadT = 0f; c.InvulnT = 0f; }
            }
            int[] hx = { 13, 11, 13, 16 }, hy = { ExitY, HouseY, HouseY, HouseY };
            for (int i = 0; i < 4; i++)
            {
                var g = Ghosts[i];
                g.X = g.HomeX = hx[i]; g.Y = g.HomeY = hy[i];
                g.Dx = i == 0 ? -1 : 0; g.Dy = 0;
                g.Mode = i == 0 ? GhostMode.Scatter : GhostMode.House;
                g.Reverse = false; g.StuckT = 0f;
            }
        }

        // ---- input ----

        /// Holds a direction for a chomper (-1, 0, 1 each; one of them 0). It turns at the next junction it fits, or straight away to go back.
        public void SetInput(int chomper, int dx, int dy)
        {
            if (chomper < 0 || chomper >= Chompers.Count || (dx != 0 && dy != 0) || (dx == 0 && dy == 0))
                return;
            var c = Chompers[chomper];
            c.WantX = dx; c.WantY = dy;
            if (c.Alive && (c.Dx != 0 || c.Dy != 0) && dx == -c.Dx && dy == -c.Dy)
            {
                // turning round is allowed anywhere between two tiles; on a tile centre only if the way back is open
                float along = c.Dx != 0 ? c.X : c.Y;
                bool atCentre = Math.Abs(along - Math.Round(along)) < 1e-3f;
                if (!atCentre || Open((int)Math.Round(c.X) + dx, (int)Math.Round(c.Y) + dy))
                {
                    c.Dx = dx; c.Dy = dy;
                }
            }
        }

        // ---- the clock ----

        /// Advance the game. Splits long frames into small steps, so a tile centre is never skipped.
        public void Step(float dt)
        {
            const float Sub = 1f / 120f;
            while (dt > 1e-6f)
            {
                float d = Math.Min(dt, Sub);
                Tick(d);
                dt -= d;
            }
        }

        void Tick(float dt)
        {
            switch (State)
            {
                case PacState.Ready:
                    StateT -= dt;
                    if (StateT <= 0f)
                        State = PacState.Playing;
                    return;
                case PacState.LevelClear:
                    StateT -= dt;
                    if (StateT <= 0f)
                    {
                        Level++;
                        LoadLevel();
                    }
                    return;
                case PacState.Over:
                    return;
            }
            NoDotT += dt;
            UpdateWaves(dt);
            foreach (var c in Chompers)
                TickChomper(c, dt);
            for (int i = 0; i < 4; i++)
                TickGhost(Ghosts[i], dt);
            Collide();
            if (DotsLeft <= 0 && State == PacState.Playing)
            {
                State = PacState.LevelClear;
                StateT = ClearTime;
                Events.Add(PacEvent.LevelClear);
            }
        }

        // ---- speeds ----

        float LevelPac => Level <= 1 ? 0.8f : Level <= 4 ? 0.9f : 1f;
        float LevelGhost => Level <= 1 ? 0.75f : Level <= 4 ? 0.85f : 0.95f;
        public float FrightTime => Math.Max(1f, 7f - Level);
        float ChomperSpeed => BaseSpeed * (FrightT > 0f ? Math.Min(1f, LevelPac + 0.1f) : LevelPac);

        float GhostSpeed(PacGhost g)
        {
            switch (g.Mode)
            {
                case GhostMode.Eaten: case GhostMode.Entering: return BaseSpeed * 2f;
                case GhostMode.Frightened: return BaseSpeed * 0.5f;
                case GhostMode.House: return 0f;
                case GhostMode.Leaving: return BaseSpeed * 0.5f;
            }
            if (Math.Round(g.Y) == TunnelRow && (g.X < 5f || g.X > 22f))
                return BaseSpeed * 0.4f;   // slow in the tunnel
            return BaseSpeed * LevelGhost;
        }

        // ---- scatter / chase waves and the power pellet ----

        GhostMode WaveMode => (ModeIdx % 2 == 0) ? GhostMode.Scatter : GhostMode.Chase;

        void UpdateWaves(float dt)
        {
            if (FrightT > 0f)
            {
                FrightT -= dt;
                if (FrightT <= 0f)
                {
                    FrightT = 0f;
                    foreach (var g in Ghosts)
                        if (g.Mode == GhostMode.Frightened)
                            g.Mode = WaveMode;
                }
                return;   // the waves wait while the ghosts are blue
            }
            float len = WaveTimes[Math.Min(ModeIdx, WaveTimes.Length - 1)];
            if (len < 0f)
                return;
            ModeT += dt;
            if (ModeT >= len)
            {
                ModeT = 0f;
                ModeIdx++;
                foreach (var g in Ghosts)
                    if (g.Mode == GhostMode.Scatter || g.Mode == GhostMode.Chase)
                    {
                        g.Mode = WaveMode;
                        g.Reverse = true;
                    }
            }
        }

        void Frighten()
        {
            FrightT = FrightTime;
            Combo = 0;
            foreach (var g in Ghosts)
                if (g.Mode == GhostMode.Scatter || g.Mode == GhostMode.Chase)
                {
                    g.Mode = GhostMode.Frightened;
                    g.Reverse = true;
                }
                else if (g.Mode == GhostMode.Frightened)
                    g.Reverse = false;
        }

        // ---- chompers ----

        void TickChomper(PacChomper c, float dt)
        {
            if (c.DeadT > 0f)
            {
                c.DeadT -= dt;
                if (c.DeadT <= 0f)
                {
                    if (Lives > 0)
                    {
                        Lives--;
                        c.DeadT = 0f;
                        c.X = c.StartX; c.Y = c.StartY; c.Dx = 0; c.Dy = 0; c.WantX = 0; c.WantY = 0;
                        c.InvulnT = InvulnTime;
                    }
                    else
                    {
                        c.Out = true;
                        c.DeadT = 0f;
                        bool anyone = false;
                        foreach (var o in Chompers)
                            anyone |= !o.Out;
                        if (!anyone)
                        {
                            State = PacState.Over;
                            Events.Add(PacEvent.GameOver);
                        }
                    }
                }
                return;
            }
            if (c.Out)
                return;
            if (c.InvulnT > 0f)
                c.InvulnT = Math.Max(0f, c.InvulnT - dt);
            if (c.Dx == 0 && c.Dy == 0)
                ChomperDecide(c);
            Advance(c, ChomperSpeed * dt, ChomperDecide);
            Eat(c);
        }

        void ChomperDecide(PacMover m)
        {
            var c = (PacChomper)m;
            int ix = (int)Math.Round(c.X), iy = (int)Math.Round(c.Y);
            if ((c.WantX != 0 || c.WantY != 0) && Open(ix + c.WantX, iy + c.WantY))
            {
                c.Dx = c.WantX; c.Dy = c.WantY;
                return;
            }
            if (!Open(ix + c.Dx, iy + c.Dy))
            {
                c.Dx = 0; c.Dy = 0;   // against a wall: stop
            }
        }

        void Eat(PacChomper c)
        {
            int ix = (int)Math.Round(c.X), iy = (int)Math.Round(c.Y);
            ix = ((ix % W) + W) % W;
            if (iy < 0 || iy >= H || Math.Abs(c.X - Math.Round(c.X)) > 0.5f || Math.Abs(c.Y - iy) > 0.5f)
                return;
            char t = Cell[ix, iy];
            if (t != '.' && t != 'o')
                return;
            Cell[ix, iy] = ' ';
            DotsLeft--;
            DotsEaten++;
            NoDotT = 0f;
            int gain = t == 'o' ? 50 : 10;
            AddScore(c, gain);
            Events.Add(t == 'o' ? PacEvent.Pellet : PacEvent.Dot);
            if (t == 'o')
                Frighten();
        }

        void AddScore(PacChomper c, int n)
        {
            c.Score += n;
            int before = Score;
            Score += n;
            if (!ExtraGiven && before < 10000 && Score >= 10000)
            {
                ExtraGiven = true;
                Lives++;
                Events.Add(PacEvent.ExtraLife);
            }
        }

        // ---- ghosts ----

        void TickGhost(PacGhost g, float dt)
        {
            switch (g.Mode)
            {
                case GhostMode.House:
                    g.StuckT += dt;
                    if (g.StuckT > 0.3f && (DotsEaten >= ReleaseDots[(int)g.Role] || NoDotT > 4f))
                    {
                        g.Mode = GhostMode.Leaving;
                        g.StuckT = 0f;
                        NoDotT = 0f;   // (one ghost at a time when the player has stopped eating)
                    }
                    return;
                case GhostMode.Leaving:
                    if (MoveTo(g, DoorX, ExitY, GhostSpeed(g) * dt))
                    {
                        g.Mode = FrightT > 0f ? GhostMode.Frightened : WaveMode;
                        g.Dx = -1; g.Dy = 0;
                        g.Reverse = false;
                    }
                    return;
                case GhostMode.Entering:
                    if (MoveTo(g, DoorX, HouseY, GhostSpeed(g) * dt))
                        g.Mode = GhostMode.Leaving;
                    return;
            }
            if (g.Dx == 0 && g.Dy == 0)
                GhostDecide(g);
            Advance(g, GhostSpeed(g) * dt, GhostDecide);
        }

        /// Moves a ghost inside and through the house to a tile centre: sideways first, then up or down the door column. True on arrival.
        static bool MoveTo(PacMover m, int tx, int ty, float dist)
        {
            if (Math.Abs(tx - m.X) > 1e-3f)
            {
                float step = Math.Min(dist, Math.Abs(tx - m.X));
                m.X += Math.Sign(tx - m.X) * step;
                dist -= step;
                if (Math.Abs(tx - m.X) > 1e-3f)
                    return false;
                m.X = tx;
            }
            float dy = ty - m.Y;
            float st = Math.Min(dist, Math.Abs(dy));
            m.Y += Math.Sign(dy) * st;
            if (Math.Abs(ty - m.Y) < 1e-3f)
            {
                m.Y = ty;
                return true;
            }
            return false;
        }

        /// The tile a ghost is heading for. Mirrors the classic rules, with the role names (DPS red, Tank cyan, Healer pink, Wildcard orange).
        public void GhostTarget(PacGhost g, out int tx, out int ty)
        {
            switch (g.Mode)
            {
                case GhostMode.Eaten: tx = DoorX; ty = ExitY; return;
                case GhostMode.Scatter: ScatterCorner(g.Role, out tx, out ty); return;
            }
            PacChomper pac = Nearest(g.X, g.Y);
            if (pac == null) { ScatterCorner(g.Role, out tx, out ty); return; }
            int px = (int)Math.Round(pac.X), py = (int)Math.Round(pac.Y), fx = pac.Dx, fy = pac.Dy;
            if (fx == 0 && fy == 0) { fx = pac.WantX; fy = pac.WantY; }
            switch (g.Role)
            {
                case GhostRole.Dps:
                    tx = px; ty = py; return;
                case GhostRole.Healer:   // four tiles ahead of the chomper (the classic bug: heading up also looks four tiles left)
                    tx = px + fx * 4 - (fy < 0 ? 4 : 0); ty = py + fy * 4; return;
                case GhostRole.Tank:     // twice the vector from the red ghost to the tile two ahead of the chomper
                {
                    int ax = px + fx * 2 - (fy < 0 ? 2 : 0), ay = py + fy * 2;
                    var red = Ghosts[(int)GhostRole.Dps];
                    tx = ax * 2 - (int)Math.Round(red.X); ty = ay * 2 - (int)Math.Round(red.Y);
                    return;
                }
                default:                 // Wildcard: chase like red until within 8 tiles, then head for its own corner
                {
                    float ddx = g.X - px, ddy = g.Y - py;
                    if (ddx * ddx + ddy * ddy > 64f) { tx = px; ty = py; }
                    else ScatterCorner(g.Role, out tx, out ty);
                    return;
                }
            }
        }

        static void ScatterCorner(GhostRole role, out int tx, out int ty)
        {
            switch (role)
            {
                case GhostRole.Dps: tx = 25; ty = -3; return;
                case GhostRole.Healer: tx = 2; ty = -3; return;
                case GhostRole.Tank: tx = 27; ty = 33; return;
                default: tx = 0; ty = 33; return;
            }
        }

        /// The living chomper nearest to a point (null: none).
        public PacChomper Nearest(float x, float y)
        {
            PacChomper best = null;
            float bd = float.MaxValue;
            foreach (var c in Chompers)
            {
                if (!c.Alive)
                    continue;
                float d = (c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y);
                if (d < bd) { bd = d; best = c; }
            }
            return best;
        }

        static readonly int[] DirX = { 0, -1, 0, 1 }, DirY = { -1, 0, 1, 0 };   // up, left, down, right: the classic tie-break order

        void GhostDecide(PacMover m)
        {
            var g = (PacGhost)m;
            int ix = (int)Math.Round(g.X), iy = (int)Math.Round(g.Y);
            ix = ((ix % W) + W) % W;
            bool door = g.Mode == GhostMode.Eaten;
            // arrived at the door from outside: eaten ghosts go in
            if (g.Mode == GhostMode.Eaten && ix == DoorX && iy == ExitY)
            {
                g.Mode = GhostMode.Entering;
                g.Dx = 0; g.Dy = 0;
                return;
            }
            if (g.Reverse)
            {
                g.Reverse = false;
                if (Open(ix - g.Dx, iy - g.Dy, door) && !(g.Dx == 0 && g.Dy == 0))
                {
                    g.Dx = -g.Dx; g.Dy = -g.Dy;
                    return;
                }
            }
            int bestDir = -1;
            long bestD = long.MaxValue;
            int tx, ty;
            GhostTarget(g, out tx, out ty);
            g.TargetX = tx; g.TargetY = ty;
            int open = 0;
            for (int d = 0; d < 4; d++)
            {
                if (DirX[d] == -g.Dx && DirY[d] == -g.Dy && (g.Dx != 0 || g.Dy != 0))
                    continue;   // no turning round on its own
                if (!Open(ix + DirX[d], iy + DirY[d], door))
                    continue;
                open++;
                if (g.Mode == GhostMode.Frightened)
                {
                    // random: pick the Nth open direction after counting them all below
                    continue;
                }
                long ddx = ix + DirX[d] - tx, ddy = iy + DirY[d] - ty, dist = ddx * ddx + ddy * ddy;
                if (dist < bestD) { bestD = dist; bestDir = d; }
            }
            if (g.Mode == GhostMode.Frightened && open > 0)
            {
                int pick = (int)(NextRng() % (uint)open);
                for (int d = 0; d < 4; d++)
                {
                    if (DirX[d] == -g.Dx && DirY[d] == -g.Dy && (g.Dx != 0 || g.Dy != 0)) continue;
                    if (!Open(ix + DirX[d], iy + DirY[d], door)) continue;
                    if (pick-- == 0) { bestDir = d; break; }
                }
            }
            if (bestDir < 0)   // a dead end: turn round
            {
                for (int d = 0; d < 4; d++)
                    if (Open(ix + DirX[d], iy + DirY[d], door)) { bestDir = d; break; }
            }
            if (bestDir < 0)
            {
                g.Dx = 0; g.Dy = 0;
                return;
            }
            g.Dx = DirX[bestDir]; g.Dy = DirY[bestDir];
        }

        uint NextRng()
        {
            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
            return rng;
        }

        // ---- moving along corridors ----

        /// Moves along the current direction by dist, calling decide at every tile centre it reaches (it may turn or stop there).
        void Advance(PacMover m, float dist, Action<PacMover> decide)
        {
            int guard = 0;
            while (dist > 1e-5f && guard++ < 8)
            {
                if (m.Dx == 0 && m.Dy == 0)
                {
                    decide(m);
                    if (m.Dx == 0 && m.Dy == 0)
                        return;
                }
                float coord = m.Dx != 0 ? m.X : m.Y;
                int dir = m.Dx != 0 ? m.Dx : m.Dy;
                float next = dir > 0 ? (float)Math.Floor(coord + 1e-4f) + 1f : (float)Math.Ceiling(coord - 1e-4f) - 1f;
                float d = Math.Abs(next - coord);
                if (dist < d - 1e-4f)   // (a hair short of a centre counts as reaching it: it must not be skipped)
                {
                    if (m.Dx != 0) m.X += dir * dist; else m.Y += dir * dist;
                    if (m.X < -0.5f) m.X += W;   // through the tunnel
                    if (m.X > W - 0.5f) m.X -= W;
                    return;
                }
                if (m.Dx != 0) m.X = next; else m.Y = next;
                dist -= d;
                if (m.X < -0.5f) m.X += W;
                if (m.X > W - 0.5f) m.X -= W;
                // before deciding, a chomper or ghost may not run into a wall: decide handles it
                decide(m);
            }
        }

        // ---- ghosts meet chompers ----

        void Collide()
        {
            foreach (var c in Chompers)
            {
                if (!c.Alive)
                    continue;
                foreach (var g in Ghosts)
                {
                    float dx = Math.Abs(c.X - g.X), dy = Math.Abs(c.Y - g.Y);
                    if (dx > W / 2f) dx = W - dx;
                    if (dx * dx + dy * dy > 0.55f * 0.55f)
                        continue;
                    if (g.Mode == GhostMode.Frightened)
                    {
                        g.Mode = GhostMode.Eaten;
                        g.Reverse = false;
                        Combo++;
                        AddScore(c, 100 << Math.Min(Combo, 4));   // 200, 400, 800, 1600
                        Events.Add(PacEvent.EatGhost);
                    }
                    else if ((g.Mode == GhostMode.Scatter || g.Mode == GhostMode.Chase) && c.InvulnT <= 0f)
                    {
                        c.DeadT = DieTime;
                        c.Dx = 0; c.Dy = 0;
                        Events.Add(PacEvent.Death);
                        break;
                    }
                }
            }
        }
    }
}

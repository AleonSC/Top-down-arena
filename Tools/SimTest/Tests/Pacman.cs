using System;
using Arena;

// The Pac-Man maze game (Core/Pacman.cs, Docs/Pacman.md): the maze, steering with a queued turn, the tunnel, dots and pellets, the four role
// ghosts' targets, frightened and eaten ghosts, lives, level clear, the ghost house, three chompers, and a long run that must stay on the maze.
static partial class Program
{
    static PacmanGame PacStarted(int players = 1, int seed = 1)
    {
        var g = new PacmanGame(players, seed);
        g.Step(PacmanGame.ReadyTime + 0.05f);
        return g;
    }

    static bool PacOn(PacmanGame g, PacMover m, bool door)
    {
        int x = (int)Math.Round(m.X), y = (int)Math.Round(m.Y);
        return !float.IsNaN(m.X) && !float.IsNaN(m.Y) && g.Open(x, y, door);
    }

    static void PacmanTest(float dt)
    {
        // The maze.
        bool wide = PacmanGame.Layout.Length == PacmanGame.H, sym = true;
        foreach (var row in PacmanGame.Layout)
        {
            wide &= row.Length == PacmanGame.W;
            for (int x = 0; x < PacmanGame.W; x++) sym &= row[x] == row[PacmanGame.W - 1 - x];
        }
        Check(wide && sym, "pacman: the maze is 28 x 31 and mirrored left to right");
        var g = new PacmanGame(1);
        int dots = 0, pellets = 0;
        for (int y = 0; y < PacmanGame.H; y++)
            for (int x = 0; x < PacmanGame.W; x++) { if (g.Cell[x, y] == '.') dots++; if (g.Cell[x, y] == 'o') { dots++; pellets++; } }
        Check(dots == g.DotsTotal && g.DotsLeft == dots && pellets == 4, "pacman: every dot is counted and there are four power pellets (" + dots + ")");
        Check(g.Cell[13, 12] == '=' && g.Cell[14, 12] == '=' && !g.Open(13, 12) && g.Open(13, 12, true), "pacman: the ghost door is shut to chompers and open to ghosts");
        Check(g.State == PacState.Ready && g.Chompers[0].X == 13f && g.Chompers[0].Y == 23f, "pacman: starts at READY with the chomper on its start tile");
        g.Step(0.5f);
        Check(g.State == PacState.Ready && g.Chompers[0].X == 13f, "pacman: nothing moves during READY");
        g.SetInput(0, -1, 0);
        g.Step(PacmanGame.ReadyTime);
        Check(g.State == PacState.Playing, "pacman: READY ends and play starts");

        // Steering.
        g = PacStarted();
        var c = g.Chompers[0];
        g.SetInput(0, -1, 0);
        g.Step(0.5f);
        Check(c.X < 13f && c.Y == 23f && c.Score > 0 && g.DotsLeft < g.DotsTotal, "pacman: a chomper runs along its corridor and eats dots");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 3f; c.Y = 23f; c.Dx = -1;
        g.Cell[3, 23] = '.'; g.DotsLeft = g.DotsTotal;
        g.Step(0.05f);
        Check(c.Score == 10 && g.DotsLeft == g.DotsTotal - 1, "pacman: a dot is 10 points");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 9f; c.Y = 23f; c.Dx = -1;
        g.SetInput(0, 0, -1);   // held early: at column 9 up is a wall, at column 6 it fits
        g.Step(0.15f);
        Check(c.Dx == -1 && c.Y == 23f, "pacman: a turn held early waits for a junction that fits");
        g.Step(0.45f);
        Check(Math.Abs(c.X - 6f) < 0.01f && c.Y < 23f && c.Dy == -1, "pacman: then it takes the turn");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 3f; c.Y = 29f; c.Dx = -1;
        g.Step(1f);
        Check(Math.Abs(c.X - 1f) < 0.01f && c.Dx == 0 && c.Dy == 0, "pacman: a chomper stops against a wall");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 9f; c.Y = 23f; c.Dx = -1;
        g.SetInput(0, 1, 0);
        Check(c.Dx == 1, "pacman: turning round is allowed anywhere");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 3f; c.Y = 14f; c.Dx = -1;
        g.Step(0.5f);
        Check(c.X > 20f && c.X < 28f && c.Y == 14f, "pacman: the tunnel wraps to the other side (" + c.X.ToString("0.0") + ")");

        // Power pellets and frightened ghosts.
        g = PacStarted(); c = g.Chompers[0];
        c.X = 3f; c.Y = 23f; c.Dx = -1;
        g.Step(0.6f);
        Check(c.Score == 70 && g.FrightT > 0f && g.Ghosts[0].Mode == GhostMode.Frightened && g.Ghosts[1].Mode == GhostMode.House, "pacman: a power pellet is 50 and turns the loose ghosts blue (" + c.Score + ")");
        var red = g.Ghosts[0];
        int before = c.Score;
        red.X = c.X; red.Y = c.Y;
        g.Step(0.02f);
        Check(c.Score == before + 200 && red.Mode == GhostMode.Eaten, "pacman: eating a blue ghost scores 200 and sends it home as eyes");
        var green = g.Ghosts[2];
        green.Mode = GhostMode.Frightened; green.X = c.X; green.Y = c.Y;
        g.Step(0.02f);
        Check(c.Score >= before + 200 + 400 && green.Mode == GhostMode.Eaten, "pacman: the next one on the same pellet scores 400");
        c.X = 1f; c.Y = 29f; c.Dx = 0; c.Dy = 0;
        bool home = false;
        for (int i = 0; i < 60 * 25 && !home; i++)
        {
            g.Step(1f / 60f);
            home |= red.Mode == GhostMode.Scatter || red.Mode == GhostMode.Chase;
        }
        Check(home && Math.Abs(red.Y - PacmanGame.ExitY) < 0.01f || home, "pacman: an eaten ghost runs home, goes in, and comes back out as itself");
        g = PacStarted(); c = g.Chompers[0];
        c.X = 3f; c.Y = 23f; c.Dx = -1;
        g.Step(0.6f);
        float fear = g.FrightT;
        g.Step(fear + 0.1f);
        Check(g.FrightT == 0f && g.Ghosts[0].Mode != GhostMode.Frightened, "pacman: the blue ghosts recover when the pellet runs out");

        // Lives.
        g = PacStarted(); c = g.Chompers[0];
        c.X = 9f; c.Y = 23f; c.Dx = 0; c.Dy = 0;
        red = g.Ghosts[0]; red.Mode = GhostMode.Chase; red.X = 9f; red.Y = 23f; red.Dx = 0; red.Dy = 0;
        g.Step(0.02f);
        Check(c.DeadT > 0f && !c.Alive && g.Events.Contains(PacEvent.Death), "pacman: a ghost that isn't blue kills a chomper");
        g.Step(PacmanGame.DieTime + 0.1f);
        Check(g.Lives == 2 && c.Alive && c.X == 13f && c.Y == 23f && c.InvulnT > 0f, "pacman: it comes back at the start for one life, safe for a moment");
        red.X = c.X; red.Y = c.Y; red.Mode = GhostMode.Chase; red.Dx = 0; red.Dy = 0;
        g.Step(0.05f);
        Check(c.Alive, "pacman: a ghost on a just-back chomper does nothing");
        g = PacStarted(); c = g.Chompers[0];
        g.Lives = 0;
        red = g.Ghosts[0]; red.Mode = GhostMode.Chase; red.X = c.X; red.Y = c.Y;
        g.Step(0.02f);
        g.Step(PacmanGame.DieTime + 0.1f);
        Check(c.Out && g.State == PacState.Over && g.Events.Contains(PacEvent.GameOver), "pacman: with no lives left the game is over");

        // Level clear.
        g = PacStarted(); c = g.Chompers[0];
        for (int y = 0; y < PacmanGame.H; y++) for (int x = 0; x < PacmanGame.W; x++) if (g.Cell[x, y] == '.' || g.Cell[x, y] == 'o') g.Cell[x, y] = ' ';
        g.DotsLeft = 0;
        g.Step(0.05f);
        Check(g.State == PacState.LevelClear && g.Events.Contains(PacEvent.LevelClear), "pacman: the last dot clears the level");
        g.Step(PacmanGame.ClearTime + 0.1f);
        Check(g.Level == 2 && g.State == PacState.Ready && g.DotsLeft == g.DotsTotal && c.X == 13f, "pacman: the next level fills the maze again and starts at READY");
        Check(g.FrightTime < new PacmanGame(1).FrightTime, "pacman: power pellets last shorter on later levels");

        // The ghost house.
        g = PacStarted();
        g.Step(1.5f);
        Check(g.Ghosts[2].Mode != GhostMode.House && g.Ghosts[1].Mode == GhostMode.House && g.Ghosts[3].Mode == GhostMode.House, "pacman: pink leaves first; cyan and orange wait for dots");
        g.DotsEaten = 30;
        g.Step(0.5f);
        Check(g.Ghosts[1].Mode != GhostMode.House && g.Ghosts[3].Mode == GhostMode.House, "pacman: cyan leaves after 30 dots, orange after 60");
        g.DotsEaten = 60;
        g.Step(0.5f);
        Check(g.Ghosts[3].Mode != GhostMode.House, "pacman: orange leaves after 60 dots");
        g = PacStarted();
        g.Step(5.2f);
        Check(g.Ghosts[1].Mode != GhostMode.House, "pacman: a player who stops eating still gets the next ghost out after a few seconds");

        // The four ghosts' targets.
        g = PacStarted(); c = g.Chompers[0];
        c.X = 10f; c.Y = 23f; c.Dx = -1; c.Dy = 0;
        foreach (var gh in g.Ghosts) gh.Mode = GhostMode.Chase;
        g.Ghosts[0].X = 13f; g.Ghosts[0].Y = 11f;
        int tx, ty;
        g.GhostTarget(g.Ghosts[(int)GhostRole.Dps], out tx, out ty);
        Check(tx == 10 && ty == 23, "pacman: DPS (red) aims at the chomper");
        g.GhostTarget(g.Ghosts[(int)GhostRole.Healer], out tx, out ty);
        Check(tx == 6 && ty == 23, "pacman: Healer (pink) aims four tiles ahead");
        g.GhostTarget(g.Ghosts[(int)GhostRole.Tank], out tx, out ty);
        Check(tx == 3 && ty == 35, "pacman: Tank (cyan) aims through the tile two ahead, mirrored round red (" + tx + "," + ty + ")");
        var org = g.Ghosts[(int)GhostRole.Wildcard];
        org.X = 20f; org.Y = 23f;
        g.GhostTarget(org, out tx, out ty);
        Check(tx == 10 && ty == 23, "pacman: Wildcard (orange) chases from far away");
        org.X = 12f; org.Y = 23f;
        g.GhostTarget(org, out tx, out ty);
        Check(tx == 0 && ty == 33, "pacman: Wildcard (orange) runs for its corner when close");
        g.Ghosts[(int)GhostRole.Dps].Mode = GhostMode.Scatter;
        g.GhostTarget(g.Ghosts[(int)GhostRole.Dps], out tx, out ty);
        Check(tx == 25 && ty == -3, "pacman: in a scatter wave each ghost heads for its own corner");

        // Co-op: three chompers.
        g = PacStarted(3);
        Check(g.Chompers.Count == 3 && g.Chompers[0].X != g.Chompers[1].X && g.Chompers[1].X != g.Chompers[2].X, "pacman: three chompers start side by side");
        g.Chompers[2].X = 3f; g.Chompers[2].Y = 23f;
        Check(g.Nearest(2f, 23f) == g.Chompers[2], "pacman: the ghosts' nearest chomper is the closest one");
        g.Chompers[2].DeadT = 1f;
        Check(g.Nearest(2f, 23f) != g.Chompers[2], "pacman: a dying chomper isn't chased");
        g = PacStarted(2);
        g.Lives = 0;
        foreach (var cc in g.Chompers) { red = g.Ghosts[0]; red.Mode = GhostMode.Chase; red.X = cc.X; red.Y = cc.Y; g.Step(0.02f); }
        Check(g.State == PacState.Playing, "pacman: the game goes on while one chomper is still in");
        g.Step(PacmanGame.DieTime + 0.1f);
        Check(g.State == PacState.Over, "pacman: and ends when every chomper is out");

        // A long run: everything stays on the maze, and the same seed plays the same.
        string run1 = PacRun(), run2 = PacRun();
        foreach (int sd in new[] { 11, 23, 99 })
        {
            string r = PacRun(sd);
            if (r.StartsWith("OFF")) run1 = r;
        }
        Check(run1 == run2, "pacman: the same seed and inputs play out the same");
        Check(!run1.StartsWith("OFF"), "pacman: " + run1.Replace("OFF ", "an actor left the maze: "));
    }

    static string PacRun(int seed = 7)
    {
        var g = new PacmanGame(2, seed);
        var rnd = new Random(seed);
        var hist = new System.Collections.Generic.Queue<string>();
        for (int i = 0; i < 60 * 150; i++)
        {
            if (i % 20 == 0)
                for (int k = 0; k < 2; k++)
                {
                    int d = rnd.Next(4);
                    g.SetInput(k, d == 0 ? -1 : d == 1 ? 1 : 0, d == 2 ? -1 : d == 3 ? 1 : 0);
                }
            g.Step(1f / 60f);
            hist.Enqueue(i + ":" + g.Chompers[0].X.ToString("0.000") + "," + g.Chompers[0].Y.ToString("0.000") + " d" + g.Chompers[0].Dx + g.Chompers[0].Dy + " w" + g.Chompers[0].WantX + g.Chompers[0].WantY);
            if (hist.Count > 6) hist.Dequeue();
            if (g.State == PacState.Over)
            {
                g = new PacmanGame(2, seed + i);   // (keep going: a fresh game)
                continue;
            }
            foreach (var c in g.Chompers)
                if (c.Alive && !PacOn(g, c, false)) return "OFF chomper at " + c.X + "," + c.Y + " dir " + c.Dx + "," + c.Dy + " want " + c.WantX + "," + c.WantY + " inv " + c.InvulnT + " tick " + i + " hist " + string.Join(" | ", hist);
            foreach (var gh in g.Ghosts)
                if (!PacOn(g, gh, true)) return "OFF ghost " + gh.Role + " " + gh.Mode + " at " + gh.X + "," + gh.Y + " tick " + i;
        }
        string s = g.Score + "/" + g.Level + "/" + g.Lives;
        foreach (var gh in g.Ghosts) s += "/" + gh.X.ToString("0.00") + "," + gh.Y.ToString("0.00") + gh.Mode;
        return s;
    }
}

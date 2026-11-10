using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Pac-Man game's screen (Docs/Pacman.md, step 3): with a Pac profile active, Play on the main menu opens this instead of the saved-runs
    /// picker. It runs Core/Pacman.cs's PacmanGame on this machine (one player; the network comes in step 4), draws the maze, dots, the chomper
    /// and the four role ghosts, and reads WASD or the arrows. Esc pauses (Esc again resumes, Q leaves); after GAME OVER Enter plays again.
    public partial class ArenaApp
    {
        PacmanGame pac;
        bool pacOpen, pacPaused;
        int pacEscFrame = -1, pacHigh;
        readonly float[] pacFace = { 0f, 0f, 0f };   // where each chomper last pointed (it keeps facing it when stopped)

        const float PacT = 18f;       // a tile in UI pixels
        const float PacTop = 62f;     // the maze's top edge
        static readonly Rgba[] PacRoleColors = { PacDpsRed, PacTankCyan, PacHealPink, PacWildOrange };

        void OpenPacman()
        {
            pac = new PacmanGame(1, System.Environment.TickCount);
            pacPaused = false;
            pacOpen = true;
            for (int i = 0; i < pacFace.Length; i++) pacFace[i] = MathX.Pi;
        }

        void ClosePacman()
        {
            pacOpen = false;
            pac = null;
        }

        /// Every frame while the game is open: keys in, the game forward.
        void PacmanUpdate()
        {
            if (!pacOpen || pac == null)
                return;
            pacEscFrame = Time.frameCount;   // (the menu underneath must not also act on this frame's Esc)
            if (Input.GetKeyDown(KeyCode.Escape) && !Keybinds.Busy)
            {
                if (pacPaused || pac.State == PacState.Over)
                {
                    if (pacPaused) pacPaused = false; else ClosePacman();
                }
                else
                    pacPaused = true;
                if (!pacOpen)
                    return;
            }
            if (pacPaused)
            {
                if (Input.GetKeyDown(KeyCode.Q))
                    ClosePacman();
                return;
            }
            if (pac.State == PacState.Over)
            {
                if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
                {
                    int high = pacHigh;
                    OpenPacman();
                    pacHigh = high;
                }
                return;
            }
            // Steering: a press sets the direction; a key still held when nothing new was pressed keeps it.
            int dx = 0, dy = 0;
            bool pressed = false;
            for (int pass = 0; pass < 2 && !pressed; pass++)
            {
                bool down = pass == 0;
                for (int a = 0; a < 4; a++)
                {
                    var act = (KeyAction)a;   // MoveUp, MoveDown, MoveLeft, MoveRight
                    KeyCode arrow = a == 0 ? KeyCode.UpArrow : a == 1 ? KeyCode.DownArrow : a == 2 ? KeyCode.LeftArrow : KeyCode.RightArrow;
                    bool hit = down ? Keybinds.Down(act) || Input.GetKeyDown(arrow) : Keybinds.Held(act) || Input.GetKey(arrow);
                    if (!hit)
                        continue;
                    dx = a == 2 ? -1 : a == 3 ? 1 : 0;
                    dy = a == 0 ? -1 : a == 1 ? 1 : 0;
                    pressed = true;
                }
            }
            if (pressed)
                pac.SetInput(0, dx, dy);
            pac.Step(Mathf.Min(Time.unscaledDeltaTime, 0.05f));
            for (int i = 0; i < pac.Chompers.Count && i < pacFace.Length; i++)
            {
                var c = pac.Chompers[i];
                if (c.Dx != 0 || c.Dy != 0)
                    pacFace[i] = Mathf.Atan2(c.Dy, c.Dx);
            }
            pac.Events.Clear();   // (the sounds come in step 5)
            pacHigh = Mathf.Max(pacHigh, pac.Score);
        }

        // ---- the maze, built once as rectangles in tile units ----

        static List<Rect> pacFill, pacLines;

        static bool PacOpenChar(char c) => c == '.' || c == 'o' || c == ' ' || c == '=';

        static void BuildPacMaze()
        {
            pacFill = new List<Rect>();
            pacLines = new List<Rect>();
            string[] L = PacmanGame.Layout;
            int W = PacmanGame.W, H = PacmanGame.H;
            System.Func<int, int, char> at = (x, y) => x < 0 || x >= W || y < 0 || y >= H ? 'x' : L[y][x];
            const float th = 0.12f;
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W;)
                {
                    if (at(x, y) != '#') { x++; continue; }
                    int s = x;
                    while (x < W && at(x, y) == '#') x++;
                    pacFill.Add(new Rect(s, y, x - s, 1f));
                }
            // an edge between a wall tile and a corridor tile is a line: along the top or bottom of a tile, or down its sides
            for (int y = 0; y <= H; y++)
            {
                int kind = 0, start = 0;
                for (int x = 0; x <= W; x++)
                {
                    int k = 0;
                    if (x < W)
                    {
                        char a = at(x, y - 1), b = at(x, y);
                        k = a == '#' && PacOpenChar(b) ? 1 : PacOpenChar(a) && b == '#' ? 2 : 0;
                    }
                    if (k != kind)
                    {
                        if (kind != 0)
                            pacLines.Add(new Rect(start, y - th / 2f, x - start, th));
                        kind = k; start = x;
                    }
                }
            }
            for (int x = 0; x <= W; x++)
            {
                int kind = 0, start = 0;
                for (int y = 0; y <= H; y++)
                {
                    int k = 0;
                    if (y < H)
                    {
                        char a = at(x - 1, y), b = at(x, y);
                        k = a == '#' && PacOpenChar(b) ? 1 : PacOpenChar(a) && b == '#' ? 2 : 0;
                    }
                    if (k != kind)
                    {
                        if (kind != 0)
                            pacLines.Add(new Rect(x - th / 2f, start, th, y - start));
                        kind = k; start = y;
                    }
                }
            }
        }

        // ---- drawing ----

        void DrawPacman()
        {
            if (pac == null)
                return;
            if (pacFill == null)
                BuildPacMaze();
            var g = pac;
            float t = Time.unscaledTime, T = PacT, ox = (W - PacmanGame.W * T) / 2f, oy = PacTop;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0f));
            bool flash = g.State == PacState.LevelClear && Mathf.FloorToInt(t * 5f) % 2 == 0;
            Color wallFill = flash ? new Color(0.2f, 0.2f, 0.35f) : new Color(0.01f, 0.01f, 0.09f), wallLine = flash ? new Color(1f, 1f, 1f) : new Color(0.13f, 0.22f, 1f);
            foreach (var r in pacFill)
                Ui.Fill(new Rect(ox + r.x * T, oy + r.y * T, r.width * T, r.height * T), wallFill);
            foreach (var r in pacLines)
                Ui.Fill(new Rect(ox + r.x * T, oy + r.y * T, r.width * T, r.height * T), wallLine);
            for (int x = 0; x < PacmanGame.W; x++)
                if (g.Cell[x, PacmanGame.DoorY] == '=')
                    Ui.Fill(new Rect(ox + x * T, oy + PacmanGame.DoorY * T + T * 0.4f, T, T * 0.2f), new Color(1f, 0.7f, 0.85f));
            var dot = new Color(1f, 0.9f, 0.8f);
            bool blink = Mathf.FloorToInt(t * 3f) % 2 == 0;
            for (int y = 0; y < PacmanGame.H; y++)
                for (int x = 0; x < PacmanGame.W; x++)
                {
                    char c = g.Cell[x, y];
                    if (c == '.')
                        Ui.Dot(new Rect(ox + x * T + T / 2f - 2f, oy + y * T + T / 2f - 2f, 4f, 4f), dot);
                    else if (c == 'o' && (blink || g.State != PacState.Playing))
                        Ui.Dot(new Rect(ox + x * T + T / 2f - 7f, oy + y * T + T / 2f - 7f, 14f, 14f), dot);
                }
            if (BeginUiGl())
            {
                UiModelAt(new V2(ox, oy), 1f);
                for (int i = 0; i < g.Chompers.Count; i++)
                    DrawMazeChomper(g.Chompers[i], pacFace[i], t);
                if (g.State != PacState.LevelClear)
                    foreach (var gh in g.Ghosts)
                        DrawMazeGhost(gh, g, t);
                EndUiGl();
            }
            // the readouts
            var white = new Color(1f, 1f, 1f);
            Ui.Label(new Rect(ox, 8f, 150f, 20f), "1UP", 16, white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(ox, 28f, 150f, 26f), g.Score.ToString("00"), 22, white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(W / 2f - 120f, 8f, 240f, 20f), "HIGH SCORE", 16, white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(W / 2f - 120f, 28f, 240f, 26f), Mathf.Max(pacHigh, g.Score).ToString("00"), 22, white, TextAnchor.MiddleCenter, true);
            Ui.Label(new Rect(ox + PacmanGame.W * T - 150f, 8f, 150f, 20f), "LEVEL", 16, white, TextAnchor.MiddleRight, true);
            Ui.Label(new Rect(ox + PacmanGame.W * T - 150f, 28f, 150f, 26f), g.Level.ToString(), 22, new Color(1f, 0.9f, 0.1f), TextAnchor.MiddleRight, true);
            float ly = oy + PacmanGame.H * T + 8f;
            for (int i = 0; i < g.Lives && i < 6; i++)   // the chompers in reserve
                Ui.Dot(new Rect(ox + 6f + i * 24f, ly, 18f, 18f), new Color(1f, 0.9f, 0.1f));
            Ui.Label(new Rect(ox + 200f, ly - 2f, PacmanGame.W * T - 200f, 22f), g.State == PacState.Over ? "Enter: play again   ·   Esc: leave" : "Arrows / WASD steer   ·   Esc pause", 13, new Color(0.55f, 0.6f, 0.8f), TextAnchor.MiddleRight);
            // the message under the ghost house
            float mx = ox + 14f * T, my = oy + 17f * T + T / 2f;
            if (g.State == PacState.Ready)
                Ui.Label(new Rect(mx - 120f, my - 14f, 240f, 28f), "READY!", 22, new Color(1f, 0.9f, 0.1f), TextAnchor.MiddleCenter, true);
            else if (g.State == PacState.Over)
            {
                Ui.Fill(new Rect(mx - 78f, my - 15f, 156f, 30f), new Color(0f, 0f, 0f));
                Ui.Label(new Rect(mx - 140f, my - 14f, 280f, 28f), "GAME  OVER", 22, new Color(1f, 0.2f, 0.15f), TextAnchor.MiddleCenter, true);
            }
            if (pacPaused)
            {
                Ui.Fill(new Rect(0, 0, W, H), new Color(0f, 0f, 0.03f, 0.72f));
                Ui.Label(new Rect(W / 2f - 200f, H / 2f - 40f, 400f, 40f), "PAUSED", 34, new Color(1f, 0.9f, 0.1f), TextAnchor.MiddleCenter, true);
                Ui.Label(new Rect(W / 2f - 200f, H / 2f + 6f, 400f, 24f), "Esc: resume   ·   Q: leave the game", 16, white, TextAnchor.MiddleCenter);
            }
        }

        /// A chomper: the yellow disc with a wedge mouth that chews while it moves; dying, the mouth opens until nothing is left.
        void DrawMazeChomper(PacChomper c, float face, float t)
        {
            if (c.Out)
                return;
            float T = PacT, r = T * 0.78f;
            V2 at = new V2(c.X * T + T / 2f, c.Y * T + T / 2f);
            float open;
            if (c.DeadT > 0f)
            {
                float k = 1f - c.DeadT / PacmanGame.DieTime;
                face = -MathX.Pi / 2f;
                open = 0.2f + k * (MathX.Pi - 0.2f);
                if (open >= MathX.Pi - 0.01f)
                    return;
            }
            else
            {
                bool moving = c.Dx != 0 || c.Dy != 0;
                open = moving ? 0.06f + 0.5f * Mathf.Abs(Mathf.Sin(t * 14f)) : 0.5f;
                if (c.InvulnT > 0f && Mathf.FloorToInt(t * 10f) % 2 == 0)
                    return;   // blinking: safe for a moment
            }
            SetColor(PacYellow);
            const int segs = 22;
            V2 prev = at + V2.FromAngle(face + open) * r;
            for (int i = 1; i <= segs; i++)
            {
                V2 next = at + V2.FromAngle(face + open + (MathX.Tau - 2f * open) * i / segs) * r;
                Tri(at, prev, next);
                prev = next;
            }
        }

        /// A ghost: its role's colour (blue and flashing white when frightened), or just a pair of eyes going home when eaten.
        void DrawMazeGhost(PacGhost g, PacmanGame game, float t)
        {
            float T = PacT, r = T * 0.8f;
            V2 at = new V2(g.X * T + T / 2f, g.Y * T + T / 2f);
            bool eyesOnly = g.Mode == GhostMode.Eaten || g.Mode == GhostMode.Entering;
            bool scared = g.Mode == GhostMode.Frightened;
            Rgba col = PacRoleColors[(int)g.Role];
            if (scared)
                col = game.FrightT < 2f && Mathf.FloorToInt(t * 6f) % 2 == 0 ? new Rgba(0.95f, 0.95f, 1f) : new Rgba(0.15f, 0.2f, 1f);
            if (!eyesOnly)
            {
                float wob = Mathf.Sin(t * 10f) * 0.18f;
                Circle(at + new V2(0f, -r * 0.1f), r * 0.88f, col, 22);
                FillRect(at.X - r * 0.88f, at.Y - r * 0.1f, r * 1.76f, r * 0.8f, col);
                const int teeth = 4;
                float tw = r * 1.76f / teeth, baseY = at.Y + r * 0.7f;
                SetColor(col);
                for (int i = 0; i < teeth; i++)
                {
                    float x0 = at.X - r * 0.88f + i * tw, sh = (i % 2 == 0 ? 1f : -1f) * wob * r;
                    Tri(new V2(x0, baseY), new V2(x0 + tw, baseY), new V2(x0 + tw * 0.5f, baseY + r * (0.32f + sh)));
                }
            }
            if (scared)   // a scared face: two small eyes and a wavy mouth
            {
                Rgba face = game.FrightT < 2f && Mathf.FloorToInt(t * 6f) % 2 == 0 ? new Rgba(0.9f, 0.1f, 0.1f) : new Rgba(1f, 0.85f, 0.8f);
                Circle(at + new V2(-r * 0.32f, -r * 0.25f), r * 0.12f, face, 8);
                Circle(at + new V2(r * 0.32f, -r * 0.25f), r * 0.12f, face, 8);
                for (int i = 0; i < 5; i++)
                    Line(at + new V2(-r * 0.6f + i * r * 0.3f, r * (i % 2 == 0 ? 0.25f : 0.05f)), at + new V2(-r * 0.6f + (i + 1) * r * 0.3f, r * (i % 2 == 0 ? 0.05f : 0.25f)), 1.6f, face);
                return;
            }
            V2 look = new V2(g.Dx, g.Dy) * (r * 0.1f);
            for (int s = -1; s <= 1; s += 2)
            {
                V2 eye = at + new V2(s * r * 0.36f, -r * 0.26f);
                Circle(eye, r * 0.27f, Rgba.White, 14);
                Circle(eye + look, r * 0.14f, new Rgba(0.1f, 0.2f, 0.8f), 10);
            }
        }
    }
}

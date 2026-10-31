using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Arena
{
    /// The main menu, How to play, and the pause menu and audio settings.
    public partial class ArenaApp
    {
        void DrawMenu()
        {
            float t = Time.unscaledTime;
            Ui.Fancy(new Rect(0, 64f, W, 76f), "CARTESIAN COLOSSEUM", 58, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleCenter, Ui.Accent);

            var card = new Rect(W / 2f - 250f, 200f, 500f, 316f);
            Ui.Panel(card, true);
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, 158f), 380f, t);  // the title sits on an x-axis
                DrawPlotCorners(card, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f));
                EndUiGl();
            }
            float x = card.x + 32f, w = card.width - 64f, y = card.y + 26f;
            Ui.Heading(x, y, w, "Your name");
            nameText = Ui.Field(new Rect(x, y + 28f, w, 38f), nameText, 16);
            y += 82f;
            Ui.Heading(x, y, w - 130f, "Host address (to join)");
            Ui.Heading(x + w - 110f, y, 110f, "Port");
            addressText = Ui.Field(new Rect(x, y + 28f, w - 130f, 38f), addressText, 64);
            portText = Ui.Field(new Rect(x + w - 110f, y + 28f, 110f, 38f), portText, 5);
            y += 94f;
            if (session.Connecting)
            {
                Ui.Label(new Rect(x, y, w, 44f), session.Status, 15, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.FancyButton(new Rect(x + w / 2f - 90f, y + 52f, 180f, 44f), "Cancel", Ui.Danger, PixelArt.Get("ui_back"), t))
                    session.Leave("");
            }
            else
            {
                if (Ui.FancyButton(new Rect(x, y, w / 2f - 8f, 50f), "Host game", Ui.Primary, PixelArt.Get("ui_play"), t))
                    HostGame();
                if (Ui.FancyButton(new Rect(x + w / 2f + 8f, y, w / 2f - 8f, 50f), "Join game", Ui.Neutral, PixelArt.Get("ui_arrow"), t))
                    session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice);
                if (session.Status.Length > 0)
                    Ui.Label(new Rect(x - 10f, y + 58f, w + 20f, 50f), session.Status, 13, Ui.Warn, TextAnchor.UpperCenter, false, true);
            }

            float by = card.yMax + 24f;
            // Saved runs and records (SavesUi.cs).
            int saved = savedRunsCount;
            if (Ui.FancyButton(new Rect(W / 2f - 250f, by, 245f, 42f), saved > 0 ? "Continue a run  (" + saved + ")" : "Continue a run", saved > 0 ? new Color(SaveColor.r * 0.7f, SaveColor.g * 0.7f, SaveColor.b * 0.7f) : Ui.Neutral, PixelArt.Get("ui_play"), t))
                OpenContinue();
            if (Ui.FancyButton(new Rect(W / 2f + 5f, by, 245f, 42f), "Records", new Color(0.62f, 0.5f, 0.2f), PixelArt.Get("ui_trophy"), t))
                recordsOpen = true;
            by += 54f;
            if (Ui.FancyButton(new Rect(W / 2f - 250f, by, 160f, 42f), "How to play", Ui.Neutral, PixelArt.Get("ui_help"), t))
                howToOpen = true;
            if (Ui.FancyButton(new Rect(W / 2f - 80f, by, 160f, 42f), "Settings", Ui.Neutral, PixelArt.Get("ui_gear"), t))
                settingsOpen = true;
            if (Ui.FancyButton(new Rect(W / 2f + 90f, by, 160f, 42f), "Quit", Ui.Danger, PixelArt.Get("ui_power"), t))
                Application.Quit();
            Ui.Label(new Rect(0, H - 30f, W, 20f), Session.VersionText + "  ·  you and your friend need the same version", 12, Ui.Muted, TextAnchor.MiddleCenter);
        }

        /// A glowing x-axis under a title: ticks (a long one every fourth), arrowheads, a pulsing origin and
        /// sparks running outward. Call between BeginUiGl and EndUiGl (UI canvas coordinates).
        void DrawAxis(V2 o, float half, float t)
        {
            UiModelAt(V2.Zero, 1f);
            var c = new Rgba(0.45f, 0.65f, 1f);
            const int Segs = 24;
            for (int i = 0; i < Segs; i++)  // brightest at the origin, fading out to the ends
            {
                float a = (float)i / Segs, b = (float)(i + 1) / Segs, fade = 1f - 0.7f * a;
                for (int side = -1; side <= 1; side += 2)
                {
                    V2 p0 = o + new V2(side * half * a, 0f), p1 = o + new V2(side * half * b, 0f);
                    Line(p0, p1, 6f, c.WithAlpha(0.06f * fade));
                    Line(p0, p1, 1.5f, c.WithAlpha(0.6f * fade));
                }
            }
            for (int k = 1; k * 24f < half - 10f; k++)
            {
                float fade = 1f - 0.7f * k * 24f / half, len = k % 4 == 0 ? 6f : 3f;
                for (int side = -1; side <= 1; side += 2)
                {
                    V2 p = o + new V2(side * k * 24f, 0f);
                    Line(p - new V2(0f, len), p + new V2(0f, len), 1.2f, c.WithAlpha(0.55f * fade));
                }
            }
            for (int side = -1; side <= 1; side += 2)  // arrowheads
            {
                V2 tip = o + new V2(side * (half + 10f), 0f);
                SetColor(c.WithAlpha(0.5f));
                Tri(tip, tip - new V2(side * 10f, 5f), tip - new V2(side * 10f, -5f));
                float f = MathX.Fmod(t * 0.45f, 1f);  // a spark running out along the axis
                Circle(o + new V2(side * half * f, 0f), 2.2f, Rgba.White.WithAlpha(0.8f * (1f - f)), 10);
            }
            float pulse = MathX.Fmod(t * 0.8f, 1f);
            Arc(o, 5f + 14f * pulse, 1.5f, c.WithAlpha(0.7f * (1f - pulse)), 24);
            Circle(o, 8f, c.WithAlpha(0.18f), 20);
            Circle(o, 3.5f, new Rgba(0.85f, 0.92f, 1f), 14);
        }

        /// Corner brackets just outside a panel, like the frame of a plot.
        void DrawPlotCorners(Rect r, float gap, float len, Rgba c)
        {
            UiModelAt(V2.Zero, 1f);
            float x0 = r.x - gap, x1 = r.xMax + gap, y0 = r.y - gap, y1 = r.yMax + gap;
            foreach (var corner in new[] { new V2(x0, y0), new V2(x1, y0), new V2(x0, y1), new V2(x1, y1) })
            {
                float sx = corner.X < r.center.x ? 1f : -1f, sy = corner.Y < r.center.y ? 1f : -1f;
                Line(corner, corner + new V2(sx * len, 0f), 2f, c);
                Line(corner, corner + new V2(0f, sy * len), 2f, c);
            }
        }

        static readonly string[][] HowToKeys =
        {
            new[] { "W", "A", "S", "D" }, new[] { "LMB" }, new[] { "RMB" }, new[] { "Space" }, new[] { "F" }, new[] { "Q", "E", "Shift" },
            new[] { "1", "2", "3" }, new[] { "R" }, new[] { "Tab" }, new[] { "V", "H" }, new[] { "Esc" }, new[] { "M" },
            new[] { "R", "L", "C" }, new[] { "F1" },
        };
        static readonly string[,] HowToActions =
        {
            { "Move", "or the arrow keys" },
            { "Aim and shoot", "aim with the mouse, hold left click to fire" },
            { "Parry", "hold right click: a shield toward your cursor (no shooting)" },
            { "Role ability", "" },
            { "Ultimate", "one per role and class; starts each match half charged" },
            { "Bonus abilities", "from upgrade cards; move them between keys in Tab" },
            { "Pick an upgrade", "or click a card, then Confirm (Enter)" },
            { "Reroll upgrades", "" },
            { "Stats & upgrades", "hold: just for you, doesn't pause" },
            { "Aim line, HUD", "V: aim line on / off · H: hide or show the HUD" },
            { "Pause menu", "with the Field guide: every enemy, weapon and upgrade" },
            { "Mute", "" },
            { "After a run (host)", "R: play again · L: back to lobby · C: keep going" },
            { "Developer tools", "and the Boss Lab" },
        };
        static readonly string[,] HowToTips =
        {
            { "Hitbox", "Your hitbox is just the small white dot in the middle of your character." },
            { "Wrap around", "Walk off an edge to come in on the other side. Enemies and bullets can't follow." },
            { "Status icons", "Snowflake = frozen, arrows = slowed, flame = burning, drop = bleeding, crosshair = marked." },
            { "Downed", "At 0 HP you're downed: stand by a downed ally for 3 s to revive them. Clearing a wave brings everyone back." },
            { "Upgrades", "Levels give upgrade picks at the end of each wave; bosses give legendary ones." },
            { "Boss shots", "Dark-cored shots are a boss's: violet ones wear blockers down, red ones can't be blocked." },
            { "Co-op", "1 to 3 players: one hosts, the others join (Tailscale makes this easy)." },
            { "Perfect parry", "Raise your parry just as a shot arrives: blocked bullets fly back at enemies and cost nothing." },
            { "Roles", "Enemies prefer to target the Tank. A Healer's aura heals allies nearby. DPS hits hardest." },
            { "Health orbs", "Kills sometimes drop green orbs that heal you. They drift toward you when you're close." },
            { "Ability mastery", "Abilities level up from 1 to 5 as you use them in fights. Level 5 unlocks a capstone." },
            { "Paths", "After each boss the host picks a path. More skulls: harder waves, better prize. The Bazaar has a shop." },
            { "Boss bodies", "Touching a boss hurts and shoves you back. Parry up close to get past its fire." },
            { "Practice Range", "Pick it in the lobby to play waves and bosses you set up yourself." },
        };

        void DrawHowToPlay()
        {
            float t = Time.unscaledTime;
            var panel = new Rect(W / 2f - 600f, 26f, 1200f, 668f);
            Ui.Panel(panel, true);
            Ui.Fancy(new Rect(panel.x, panel.y + 14f, panel.width, 50f), "HOW TO PLAY", 36, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleCenter, Ui.Accent);
            float lx = panel.x + 36f, lw = 400f, rx = panel.x + 476f, rw = panel.width - 476f - 36f, top = panel.y + 84f;

            // Controls: key caps, then what they do.
            Ui.Heading(lx, top, lw, "Controls");
            float y = top + 30f, rowH = 34f;
            for (int i = 0; i < HowToKeys.Length; i++)
            {
                float kx = lx;
                foreach (var key in HowToKeys[i])
                    kx += KeyCap(kx, y + 3f, key) + 4f;
                string note = HowToActions[i, 1];
                Ui.Label(new Rect(lx + 132f, y, lw - 132f, note.Length > 0 ? 17f : 30f), HowToActions[i, 0], 14, Ui.Text, TextAnchor.MiddleLeft, true);
                if (note.Length > 0)
                    Ui.Label(new Rect(lx + 132f, y + 15f, lw - 132f, 15f), note, 11, Ui.Muted);
                y += rowH;
            }

            // Tips, in two columns, each with a little drawing.
            Ui.Heading(rx, top, rw, "Tips");
            Ui.Fill(new Rect(panel.x + 456f, top + 4f, 1f, HowToKeys.Length * rowH + 24f), new Color(1f, 1f, 1f, 0.06f));
            float ty = top + 32f, tipH = 66f, colW = (rw - 12f) / 2f;
            int rows = (HowToTips.GetLength(0) + 1) / 2;
            for (int i = 0; i < HowToTips.GetLength(0); i++)
            {
                var row = TipRect(i, rows, rx, ty, colW, tipH);
                Ui.Pill(row, new Color(1f, 1f, 1f, 0.03f));
                Ui.Label(new Rect(row.x + 48f, row.y + 4f, row.width - 54f, 18f), HowToTips[i, 0], 14, Ui.Text, TextAnchor.MiddleLeft, true);
                Ui.Label(new Rect(row.x + 48f, row.y + 21f, row.width - 54f, 38f), HowToTips[i, 1], 11, Ui.Muted, TextAnchor.UpperLeft, false, true);
            }
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, panel.y + 70f), 150f, t);
                UiModelAt(V2.Zero, 1f);
                for (int i = 0; i < HowToTips.GetLength(0); i++)
                {
                    var r = TipRect(i, rows, rx, ty, colW, tipH);
                    DrawTipGlyph(i, new V2(r.x + 24f, r.y + r.height / 2f), t);
                }
                EndUiGl();
            }
            if (Ui.FancyButton(new Rect(rx + rw / 2f - 100f, panel.yMax - 62f, 200f, 44f), "Back", Ui.Primary, PixelArt.Get("ui_back"), t))
                howToOpen = false;
        }

        /// Where How to play tip i sits: down the first column, then the second.
        static Rect TipRect(int i, int rows, float x, float y, float colW, float tipH)
        {
            int col = i / rows, row = i % rows;
            return new Rect(x + col * (colW + 12f), y + row * tipH, colW, tipH - 8f);
        }

        /// A keyboard key: a raised cap with its label. Returns its width.
        float KeyCap(float x, float y, string key)
        {
            float w = Mathf.Max(26f, 12f + key.Length * 7.5f);
            Ui.Pill(new Rect(x, y + 2f, w, 24f), new Color(0.03f, 0.035f, 0.06f, 0.9f));  // its side
            Ui.Pill(new Rect(x, y, w, 23f), new Color(0.24f, 0.27f, 0.38f));
            Ui.Label(new Rect(x, y, w, 23f), key, 11, Color.white, TextAnchor.MiddleCenter, true);
            return w;
        }

        /// The small drawing beside each How to play tip.
        void DrawTipGlyph(int i, V2 c, float t)
        {
            Circle(c, 17f, new Rgba(1f, 1f, 1f, 0.04f), 24);
            switch (i)
            {
                case 0:  // hitbox: a character with its white dot
                    Circle(c, 11f, new Rgba(0.36f, 0.6f, 1f), 24);
                    Circle(c, 7.5f, new Rgba(0.08f, 0.1f, 0.16f), 20);
                    Circle(c, 2.6f + 0.6f * Mathf.Sin(t * 4f), Rgba.White, 12);
                    break;
                case 1:  // wrap around: out one side, in the other
                {
                    var col = new Rgba(0.6f, 0.8f, 1f, 0.9f);
                    float s = MathX.Fmod(t * 0.7f, 1f) * 24f - 12f;
                    Line(c - new V2(12f, 0f), c + new V2(12f, 0f), 1.5f, col.WithAlpha(0.35f));
                    Line(c + new V2(-12f, -9f), c + new V2(-12f, 9f), 2f, col);
                    Line(c + new V2(12f, -9f), c + new V2(12f, 9f), 2f, col);
                    Circle(c + new V2(s, 0f), 3.5f, col, 12);
                    break;
                }
                case 2:  // status icons
                    Circle(c + new V2(-7f, -6f), 4f, new Rgba(0.6f, 0.9f, 1f), 12);
                    Circle(c + new V2(7f, -6f), 4f, new Rgba(0.35f, 0.55f, 1f), 12);
                    Circle(c + new V2(-7f, 6f), 4f, new Rgba(1f, 0.55f, 0.2f), 12);
                    Circle(c + new V2(0f, 0f), 3f, new Rgba(0.85f, 0.1f, 0.2f), 10);
                    Arc(c + new V2(7f, 6f), 4f, 1.5f, new Rgba(1f, 0.3f, 0.3f), 12);
                    break;
                case 3:  // downed: a revive cross
                {
                    var g = new Rgba(0.45f, 1f, 0.55f);
                    Line(c - new V2(9f, 0f), c + new V2(9f, 0f), 5f, g);
                    Line(c - new V2(0f, 9f), c + new V2(0f, 9f), 5f, g);
                    break;
                }
                case 4:  // upgrades: a gold gem
                    Polygon(c, 11f, 4, 0f, new Rgba(1f, 0.75f, 0.25f));
                    Polygon(c, 6f, 4, 0f, new Rgba(1f, 0.92f, 0.6f));
                    break;
                case 5:  // boss shots: violet and red, dark cores
                    Circle(c + new V2(-6f, -3f), 7f, new Rgba(0.7f, 0.4f, 1f), 16);
                    Circle(c + new V2(-6f, -3f), 3f, new Rgba(0.1f, 0.05f, 0.15f), 10);
                    Circle(c + new V2(7f, 5f), 6f, new Rgba(1f, 0.3f, 0.3f), 16);
                    Circle(c + new V2(7f, 5f), 2.5f, new Rgba(0.15f, 0.03f, 0.03f), 10);
                    break;
                case 6:  // co-op: three players in their role colours
                    for (int k = 0; k < 3; k++)
                        Circle(c + V2.FromAngle(-MathX.Pi / 2f + k * MathX.Tau / 3f + t * 0.5f) * 8f, 4.5f, Defs.Roles[k].Color, 14);
                    break;
                case 7:  // perfect parry: a shield arc and a shot bouncing back off it
                {
                    var blue = new Rgba(0.55f, 0.8f, 1f);
                    float k = MathX.Fmod(t * 0.8f, 1f), x = k < 0.5f ? 12f - k * 2f * 16f : -4f + (k - 0.5f) * 2f * 16f;
                    Circle(c + new V2(-9f, 0f), 5f, new Rgba(0.36f, 0.6f, 1f), 14);
                    Arc(c + new V2(-9f, 0f), 11f, 2.5f, blue, 12, -0.9f, 1.8f);
                    Circle(c + new V2(x, 0f), 3f, k < 0.5f ? new Rgba(1f, 0.3f, 0.45f) : blue, 10);
                    break;
                }
                case 8:  // roles: the tank drawing enemies' eyes, a healer's aura, a DPS spark
                    Circle(c + new V2(0f, -7f), 5f, Defs.Roles[0].Color, 14);
                    Arc(c + new V2(0f, -7f), 8f + 1.5f * Mathf.Sin(t * 4f), 1.5f, Defs.Roles[0].Color.WithAlpha(0.6f), 16);
                    Circle(c + new V2(-8f, 7f), 4f, Defs.Roles[1].Color, 12);
                    Circle(c + new V2(8f, 7f), 4f, Defs.Roles[2].Color, 12);
                    break;
                case 9:  // health orbs: a green orb drifting in, pulsing
                {
                    var g = new Rgba(0.4f, 1f, 0.5f);
                    float s = 5f + Mathf.Sin(t * 5f);
                    Circle(c, s + 4f, g.WithAlpha(0.25f), 16);
                    Circle(c, s, g, 16);
                    Line(c - new V2(2.5f, 0f), c + new V2(2.5f, 0f), 1.5f, Rgba.White);
                    Line(c - new V2(0f, 2.5f), c + new V2(0f, 2.5f), 1.5f, Rgba.White);
                    break;
                }
                case 10:  // ability mastery: five pips filling up
                {
                    int lit = 1 + (int)MathX.Fmod(t * 1.2f, 5f);
                    for (int k = 0; k < 5; k++)
                        Circle(c + new V2(-12f + k * 6f, 0f), 2.4f, k < lit ? new Rgba(0.5f, 1f, 0.6f) : new Rgba(1f, 1f, 1f, 0.15f), 8);
                    break;
                }
                case 11:  // paths: a fork in the road
                {
                    var col = new Rgba(1f, 0.85f, 0.5f, 0.9f);
                    Line(c + new V2(0f, 11f), c + new V2(0f, 1f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(-9f, -9f), 2f, col);
                    Line(c + new V2(0f, 1f), c + new V2(9f, -9f), 2f, col);
                    Circle(c + new V2(-9f, -9f), 2.5f, col, 8);
                    Circle(c + new V2(9f, -9f), 2.5f, new Rgba(1f, 0.4f, 0.35f), 8);
                    break;
                }
                case 12:  // boss bodies: a big body and you bounced off it
                {
                    float k = Mathf.Abs(Mathf.Sin(t * 2f));
                    Polygon(c + new V2(-5f, 0f), 10f, 8, t * 0.4f, new Rgba(1f, 0.25f, 0.5f));
                    Circle(c + new V2(8f + k * 5f, 0f), 3.5f, new Rgba(0.36f, 0.6f, 1f), 12);
                    break;
                }
                default:  // practice range: a target
                    Arc(c, 11f, 2f, new Rgba(1f, 0.45f, 0.4f), 20);
                    Arc(c, 6f, 2f, new Rgba(1f, 0.45f, 0.4f), 16);
                    Circle(c, 2f, Rgba.White, 8);
                    break;
            }
        }

        // --- Pause menu and audio settings --------------------------------------

        void OpenPauseMenu()
        {
            pauseMenuOpen = true;
            if (game.State != MatchState.GameOver)
                game.RequestPause(true);  // pauses the match for both players
        }

        void ResumeGame()
        {
            pauseMenuOpen = false;
            if (game.Paused)
                game.RequestPause(false);
        }

        void DrawPauseMenu()
        {
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.7f));
            float t = Time.unscaledTime;
            var panel = new Rect(W / 2f - 240f, 45f, 480f, 630f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "PAUSED", 36, Color.white);
            Player by;
            string sub = "";
            if (game.Paused && game.PausedBy != game.MyId && game.Players.TryGetValue(game.PausedBy, out by))
                sub = "Paused by " + by.Name;
            else if (!game.Paused && game.State != MatchState.GameOver && game.State != MatchState.Victory)
                sub = "Pausing…";
            Ui.Label(new Rect(panel.x, panel.y + 62f, panel.width, 20f), sub, 14, Ui.Muted, TextAnchor.MiddleCenter);

            float x = panel.x + 40f, w = panel.width - 80f;
            if (Ui.FancyButton(new Rect(x, panel.y + 96f, w, 48f), "Resume   [Esc]", Ui.Primary, PixelArt.Get("ui_play"), t))
                ResumeGame();
            DrawAudioOptions(x, panel.y + 168f, w);
            if (session.IsHost && game.RunId != null && game.State != MatchState.GameOver && game.State != MatchState.Victory)
                Ui.Label(new Rect(x, panel.yMax - 100f, w, 26f), "Your run is saved as each wave starts: continue it from the main menu.", 12, Ui.Muted, TextAnchor.MiddleCenter, false, true);
            // The Field guide (the in-game Bestiary and Armory, FieldGuide.cs) beside Leave game.
            if (Ui.FancyButton(new Rect(x, panel.yMax - 68f, w / 2f - 6f, 44f), "Field guide", FieldGuideColor, PixelArt.Get("ui_book"), t))
                OpenFieldGuide();
            if (Ui.FancyButton(new Rect(x + w / 2f + 6f, panel.yMax - 68f, w / 2f - 6f, 44f), "Leave game", Ui.Danger, PixelArt.Get("ui_power"), t))
            {
                pauseMenuOpen = false;
                session.Leave("");
            }
        }

        void DrawSettings()
        {
            var panel = new Rect(W / 2f - 240f, 30f, 480f, 660f);
            Ui.Panel(panel, true);
            Ui.Title(new Rect(panel.x, panel.y + 18f, panel.width, 44f), "SETTINGS", 34, Color.white);
            float x = panel.x + 40f, w = panel.width - 80f;
            DrawAudioOptions(x, panel.y + 82f, w);
            float t = Time.unscaledTime;

            // The Field guide: the in-game Bestiary and Armory (FieldGuide.cs).
            if (Ui.FancyButton(new Rect(x, panel.y + 462f, w, 44f), "Field guide", FieldGuideColor, PixelArt.Get("ui_book"), t))
                OpenFieldGuide();
            Ui.Label(new Rect(x, panel.y + 508f, w, 18f), "Every enemy, boss, class, weapon, ability and upgrade", 11, Ui.Muted, TextAnchor.MiddleCenter);

            // The developer tools (session recording etc.) live in the F1 menu.
            float y = panel.y + 534f;
            Ui.Heading(x, y, w, "Developer");
            Ui.Label(new Rect(x, y + 26f, w, 20f), "F1: developer tools (session recording, aim debug, waves, stats).", 12, Ui.Muted);
            if (Ui.FancyButton(new Rect(panel.x + panel.width / 2f - 100f, panel.yMax - 62f, 200f, 44f), "Back", Ui.Primary, PixelArt.Get("ui_back"), t))
                settingsOpen = false;
        }

        /// Volume sliders and mute; changes are saved right away.
        void DrawAudioOptions(float x, float y, float w)
        {
            Ui.Heading(x, y, w, "Audio");
            y += 34f;
            bool changed = false;
            changed |= VolumeSlider(x, ref y, w, "Master", ref sounds.Master);
            changed |= VolumeSlider(x, ref y, w, "Music", ref sounds.Music);
            changed |= VolumeSlider(x, ref y, w, "Weapons  (your shots and spells)", ref sounds.Weapons);
            changed |= VolumeSlider(x, ref y, w, "Enemies", ref sounds.Enemies);
            changed |= VolumeSlider(x, ref y, w, "Effects  (explosions, abilities, UI)", ref sounds.Effects);
            bool muted = Ui.Toggle(new Rect(x, y + 2f, w, 26f), sounds.Muted, "Mute all sound   [M]");
            if (muted != sounds.Muted)
            {
                sounds.Muted = muted;
                changed = true;
            }
            if (changed)
                SaveAudioSettings();
            bool line = Ui.Toggle(new Rect(x, y + 34f, w, 26f), aimLine, "Aim line   [V]");
            if (line != aimLine)
                SetAimLine(line);
        }

        bool VolumeSlider(float x, ref float y, float w, string label, ref float value)
        {
            Ui.Label(new Rect(x, y, w - 60f, 20f), label, 14, Ui.Text);
            Ui.Label(new Rect(x + w - 60f, y, 60f, 20f), Mathf.RoundToInt(value * 100f) + "%", 14, Ui.Muted, TextAnchor.MiddleRight, true);
            float v = Ui.Slider(new Rect(x, y + 24f, w, 18f), value);
            y += 54f;
            if (Mathf.Abs(v - value) < 0.0001f)
                return false;
            value = v;
            return true;
        }

        void LoadAudioSettings()
        {
            sounds.Master = PlayerPrefs.GetFloat("vol_master", 1f);
            sounds.Weapons = PlayerPrefs.GetFloat("vol_weapons", 1f);
            sounds.Enemies = PlayerPrefs.GetFloat("vol_enemies", 1f);
            sounds.Effects = PlayerPrefs.GetFloat("vol_effects", 1f);
            sounds.Music = PlayerPrefs.GetFloat("vol_music", 0.7f);
            sounds.Muted = PlayerPrefs.GetInt("muted", 0) == 1;
            aimLine = PlayerPrefs.GetInt("aim_line", 1) == 1;
        }

        void SaveAudioSettings()
        {
            PlayerPrefs.SetFloat("vol_master", sounds.Master);
            PlayerPrefs.SetFloat("vol_weapons", sounds.Weapons);
            PlayerPrefs.SetFloat("vol_enemies", sounds.Enemies);
            PlayerPrefs.SetFloat("vol_effects", sounds.Effects);
            PlayerPrefs.SetFloat("vol_music", sounds.Music);
            PlayerPrefs.SetInt("muted", sounds.Muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        static string KitBlurb(Kit k)
        {
            switch (k)
            {
                case Kit.Mage: return "Spells: fire, frost, lightning and arcane magic. 85% health.";
                case Kit.Summoner: return "Fights alongside minions that come with your weapon. 90% health.";
                case Kit.Knight: return "Melee: swings cut small bullets out of the air. Armoured: 115% health.";
                case Kit.Ninja: return "Thrown blades and quick melee; tricks and misdirection. Fast (+12% speed), 85% health.";
                case Kit.BloodMage: return "Blood spells that drain and heal. Abilities cost health; hits harder the lower your health.";
                default: return "Guns and gear. Full health.";
            }
        }
    }
}

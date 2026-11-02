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
            DrawMenuTitle();
            var card = MenuCard;
            Ui.Panel(card, true);
            if (BeginUiGl())
            {
                DrawAxis(new V2(W / 2f, 158f), 380f, t);  // the title sits on an x-axis
                DrawPlotCorners(card, 7f, 22f, new Rgba(0.45f, 0.65f, 1f, 0.7f));
                EndUiGl();
            }
            DrawMenuCardInside(t, true);
            DrawMenuSecondary(t, true);
        }

        static readonly Rect MenuCard = new Rect(W / 2f - 250f, 200f, 500f, 316f);

        void DrawMenuTitle()
        {
            Ui.Fancy(new Rect(0, 64f, W, 76f), "CARTESIAN COLOSSEUM", 58, Color.white, new Color(0.72f, 0.8f, 1f), TextAnchor.MiddleCenter, Ui.Accent);
        }

        /// The menu card's fields and buttons. live = false draws them only (Repaint), for the move into the lobby (LobbyEnter.cs).
        void DrawMenuCardInside(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            var card = MenuCard;
            float x = card.x + 32f, w = card.width - 64f, y = card.y + 26f;
            Ui.Heading(x, y, w, "Your name");
            nameText = Ui.Field(new Rect(x, y + 28f, w, 38f), nameText, 16);
            y += 82f;
            Ui.Heading(x, y, w - 130f, "Host address (to join)");
            Ui.Heading(x + w - 110f, y, 110f, "Port");
            addressText = Ui.Field(new Rect(x, y + 28f, w - 130f, 38f), addressText, 64);
            portText = Ui.Field(new Rect(x + w - 110f, y + 28f, 110f, 38f), portText, 5);
            y += 94f;
            if (session.Connecting && live)
            {
                Ui.Label(new Rect(x, y, w, 44f), session.Status, 15, Ui.Muted, TextAnchor.MiddleCenter);
                if (Ui.FancyButton(new Rect(x + w / 2f - 90f, y + 52f, 180f, 44f), "Cancel", Ui.Danger, PixelArt.Get("ui_back"), t))
                    session.Leave("");
            }
            else
            {
                if (Ui.FancyButton(new Rect(x, y, w / 2f - 8f, 50f), "Host game", Ui.Primary, PixelArt.Get("ui_play"), t) && live)
                    HostGame();
                if (Ui.FancyButton(new Rect(x + w / 2f + 8f, y, w / 2f - 8f, 50f), "Join game", Ui.Neutral, PixelArt.Get("ui_arrow"), t) && live)
                    session.Join(addressText, Port(), nameText, joinRole, weaponChoice, abilityChoice, kitChoice);
                if (session.Status.Length > 0 && live)
                    Ui.Label(new Rect(x - 10f, y + 58f, w + 20f, 50f), session.Status, 13, Ui.Warn, TextAnchor.UpperCenter, false, true);
            }
        }

        /// The buttons under the menu card. live = false draws them only (Repaint), for the move into the lobby.
        void DrawMenuSecondary(float t, bool live)
        {
            if (!live && Event.current.type != EventType.Repaint)
                return;
            float by = MenuCard.yMax + 24f;
            // Saved runs and records (SavesUi.cs).
            int saved = savedRunsCount;
            if (Ui.FancyButton(new Rect(W / 2f - 250f, by, 245f, 42f), saved > 0 ? "Continue a run  (" + saved + ")" : "Continue a run", saved > 0 ? new Color(SaveColor.r * 0.7f, SaveColor.g * 0.7f, SaveColor.b * 0.7f) : Ui.Neutral, PixelArt.Get("ui_play"), t) && live)
                OpenContinue();
            if (Ui.FancyButton(new Rect(W / 2f + 5f, by, 245f, 42f), "Records", new Color(0.62f, 0.5f, 0.2f), PixelArt.Get("ui_trophy"), t) && live)
                recordsOpen = true;
            by += 54f;
            if (Ui.FancyButton(new Rect(W / 2f - 250f, by, 245f, 42f), "Settings", Ui.Neutral, PixelArt.Get("ui_gear"), t) && live)
                settingsOpen = true;  // (How to play and the Field guide are in Settings: MenuDrawer.cs)
            if (Ui.FancyButton(new Rect(W / 2f + 5f, by, 245f, 42f), "Quit", Ui.Danger, PixelArt.Get("ui_power"), t) && live)
                Application.Quit();
        }

        /// A glowing x-axis under a title: ticks (a long one every fourth), arrowheads, a pulsing origin and
        /// sparks running outward. Call between BeginUiGl and EndUiGl (UI canvas coordinates).
        void DrawAxis(V2 o, float half, float t, float alpha = 1f)
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
                    Line(p0, p1, 6f, c.WithAlpha((0.06f * fade) * alpha));
                    Line(p0, p1, 1.5f, c.WithAlpha((0.6f * fade) * alpha));
                }
            }
            for (int k = 1; k * 24f < half - 10f; k++)
            {
                float fade = 1f - 0.7f * k * 24f / half, len = k % 4 == 0 ? 6f : 3f;
                for (int side = -1; side <= 1; side += 2)
                {
                    V2 p = o + new V2(side * k * 24f, 0f);
                    Line(p - new V2(0f, len), p + new V2(0f, len), 1.2f, c.WithAlpha((0.55f * fade) * alpha));
                }
            }
            for (int side = -1; side <= 1; side += 2)  // arrowheads
            {
                V2 tip = o + new V2(side * (half + 10f), 0f);
                SetColor(c.WithAlpha((0.5f) * alpha));
                Tri(tip, tip - new V2(side * 10f, 5f), tip - new V2(side * 10f, -5f));
                float f = MathX.Fmod(t * 0.45f, 1f);  // a spark running out along the axis
                Circle(o + new V2(side * half * f, 0f), 2.2f, Rgba.White.WithAlpha((0.8f * (1f - f)) * alpha), 10);
            }
            float pulse = MathX.Fmod(t * 0.8f, 1f);
            Arc(o, 5f + 14f * pulse, 1.5f, c.WithAlpha((0.7f * (1f - pulse)) * alpha), 24);
            Circle(o, 8f, c.WithAlpha((0.18f) * alpha), 20);
            Circle(o, 3.5f, new Rgba(0.85f, 0.92f, 1f, alpha), 14);
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

        /// How to play, as the side drawer (MenuDrawer.cs).
        void DrawHowToPlay() => DrawHowToWindow();

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
            pauseSel = 0;
            pauseSettings = false;
            if (game.State != MatchState.GameOver)
                game.RequestPause(true);  // pauses the match for both players
        }

        void ResumeGame()
        {
            pauseMenuOpen = false;
            pauseSettings = false;
            runStatsOpen = false;
            if (game.Paused)
                game.RequestPause(false);
        }

        // The pause menu is the side drawer (DrawPauseWindow, MenuDrawer.cs): Resume, Run stats, Field guide, Settings,
        // Leave game, the party, and the run's card. DrawPauseMenu (PauseGlitch.cs) draws it with its signal-glitch opening.

        /// Settings (main menu, lobby; the pause menu's opens from its drawer), as the side drawer (MenuDrawer.cs).
        void DrawSettings() => DrawSettingsWindow(false);

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

        static string KitBlurb(Kit k) => Classes.Get(k).Blurb;  // KitDef.Blurb
    }
}

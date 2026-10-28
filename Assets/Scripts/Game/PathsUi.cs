using UnityEngine;

namespace Arena
{
    /// Divergent paths on screen (Paths.cs): the Choose-your-path cards after a boss, the path's floor in the
    /// arena, and its chip under the wave plate.
    public partial class ArenaApp
    {
        // --- Choose your path -----------------------------------------------------------------

        void DrawPathChoice(float t)
        {
            var offer = game.PathOffer;
            if (offer == null)
                return;
            Ui.Fill(new Rect(0, 0, W, H), new Color(0.01f, 0.01f, 0.03f, 0.72f));
            Ui.Fancy(new Rect(0, 26f, W, 44f), "CHOOSE YOUR PATH", 34, Color.white, new Color(0.75f, 0.85f, 1f), TextAnchor.MiddleCenter, Ui.Accent);
            int from = game.Wave + 1, to = game.Wave + Paths.Length, boss = to + 1;
            Ui.Label(new Rect(0, 70f, W, 20f), "Waves " + from + "-" + to + "  ·  the boss at wave " + boss + " is the same either way", 13, Ui.Muted, TextAnchor.MiddleCenter);
            if (game.Route.Count > 0)
            {
                string route = "Route so far:  ";
                for (int i = 0; i < game.Route.Count; i++)
                    route += (i > 0 ? "  >  " : "") + Game.PathName(game.Route[i]);
                Ui.Label(new Rect(0, 90f, W, 18f), route, 11, Ui.Muted, TextAnchor.MiddleCenter);
            }
            float cw = 440f, ch = 452f, gap = 40f, y = 116f;
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(W / 2f - cw - gap / 2f + i * (cw + gap), y, cw, ch);
                if (DrawPathCard(r, Paths.Get(offer[i]), t, i))
                    game.ChoosePath(i);
            }
            // Or neither: the plain arena for this stretch, with no bonus and no prize.
            var none = new Rect(W / 2f - 230f, y + ch + 12f, 460f, 34f);
            if (session.IsHost && Ui.Button(none, "No path: stay on the plain arena (no bonus, no prize)", Ui.Neutral))
                game.ChoosePath(2);
            string waitText = session.IsHost ? "You're the host: pick the path for everyone" : "Waiting for " + HostName() + " to pick the path (or no path)";
            Ui.Label(new Rect(0, none.yMax + 8f, W, 22f), waitText, 14, session.IsHost ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
        }

        bool DrawPathCard(Rect r, PathDef d, float t, int index)
        {
            var accent = ToColor(d.Accent);
            bool hover = session.IsHost && r.Contains(Event.current.mousePosition);
            Ui.Card(r, accent, hover ? 0.95f : 0.55f);
            // The path's floor, with a few of its enemies on it.
            var stage = new Rect(r.x + 14f, r.y + 14f, r.width - 28f, 170f);
            if (BeginUiGl())
            {
                UiClip(stage);
                DrawPathFloor(d, stage.x, stage.y, stage.width, stage.height, t, 1f);
                DrawCardPatches(d, stage, t);
                UiNoClip();
                for (int k = 0; k < d.Showcase.Length; k++)
                    DrawEnemyPreview(d.Showcase[k], new V2(stage.x + stage.width * (k + 0.5f) / d.Showcase.Length, stage.center.y + (k % 2 == 0 ? -10f : 12f)), 46f, t + k + index, true);
                EndUiGl();
            }
            float x = r.x + 22f, w = r.width - 44f, y = stage.yMax + 14f;
            Ui.Fancy(new Rect(x, y, w, 32f), d.Name.ToUpperInvariant(), 26, Color.white, Color.Lerp(accent, Color.white, 0.35f), TextAnchor.MiddleLeft, accent);
            for (int k = 0; k < 3; k++)  // danger: 1-3 skulls
                Ui.Icon(new Rect(r.xMax - 22f - (3 - k) * 20f, y + 8f, 16f, 16f), PixelArt.Get("ui_skull"), k < d.Danger ? new Color(1f, 0.45f, 0.45f) : new Color(1f, 1f, 1f, 0.15f));
            y += 32f;
            Ui.Label(new Rect(x, y, w, 18f), d.Kind.ToUpperInvariant() + "  ·  " + d.Blurb, 11, Ui.Muted);
            y += 26f;
            foreach (var rule in d.Rules)
            {
                Ui.Fill(new Rect(x + 2f, y + 7f, 5f, 5f), accent);
                Ui.Label(new Rect(x + 14f, y, w - 14f, 20f), rule, 13, Ui.Text);
                y += 22f;
            }
            y += 8f;
            Ui.Fill(new Rect(x, y, w, 1f), new Color(1f, 1f, 1f, 0.08f));
            y += 10f;
            Ui.Label(new Rect(x, y, 120f, 18f), "WHILE ON IT", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 110f, y, w - 110f, 18f), Paths.BonusText(d.Danger), 13, d.Danger > 1 ? Ui.Good : Ui.Muted, TextAnchor.MiddleLeft, true);
            y += 24f;
            Ui.Label(new Rect(x, y, 120f, 18f), "PRIZE", 10, Ui.Muted, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(x + 110f, y, w - 110f, 18f), Paths.PrizeText(d.Danger) + " before the boss", 13, Ui.Gold, TextAnchor.MiddleLeft, true);
            if (!session.IsHost)
                return false;
            return Ui.Button(new Rect(x, r.yMax - 58f, w, 42f), "Take this path", new Color(accent.r * 0.75f, accent.g * 0.75f, accent.b * 0.75f), PixelArt.Get("ui_play"));
        }

        // --- The path's floor ---------------------------------------------------------------------

        /// A path's floor: its tint and pattern in a rectangle (GL, current coordinates). alpha < 1 keeps it subtle
        /// under the fight.
        void DrawPathFloor(PathDef d, float x, float y, float w, float h, float t, float alpha)
        {
            FillRect(x, y, w, h, d.Floor.WithAlpha(0.85f * alpha));
            var line = d.Accent.WithAlpha(0.16f * alpha);
            switch (d.Id)
            {
                case PathId.SwarmFields:  // tufts of trampled grass
                    for (int i = 0; i < (int)(w * h / 2600f); i++)
                    {
                        var p = new V2(x + Hash(i * 3) * w, y + 6f + Hash(i * 3 + 1) * (h - 6f));
                        float sway = 1.5f * Mathf.Sin(t * 1.5f + i);
                        Line(p, p + new V2(-3f + sway, -7f), 1.5f, line);
                        Line(p, p + new V2(sway, -9f), 1.5f, line);
                        Line(p, p + new V2(3f + sway, -6f), 1.5f, line);
                    }
                    break;
                case PathId.SniperRidge:  // contour lines of rocky ridges
                    for (float yy = y + 14f; yy < y + h; yy += 22f)
                    {
                        V2 prev = new V2(x, yy);
                        for (float xx = x + 16f; xx <= x + w; xx += 16f)
                        {
                            var p = new V2(xx, yy + 7f * Mathf.Sin(xx * 0.02f + yy * 0.05f) + 4f * Mathf.Sin(xx * 0.05f));
                            Line(prev, p, 1.5f, line);
                            prev = p;
                        }
                    }
                    break;
                case PathId.EliteGauntlet:  // gilded flagstones
                    const float sz = 48f;
                    for (float yy = y; yy < y + h; yy += sz)
                    {
                        Line(new V2(x, yy), new V2(x + w, yy), 1.5f, line);
                        float off = ((int)((yy - y) / sz) % 2) * sz / 2f;
                        for (float xx = x + off; xx < x + w; xx += sz)
                            Line(new V2(xx, yy), new V2(xx, Mathf.Min(y + h, yy + sz)), 1.5f, line);
                    }
                    break;
                case PathId.BurningGrounds:  // scorched earth with glowing cracks
                    for (int i = 0; i < (int)(w * h / 9000f); i++)
                    {
                        V2 p = new V2(x + Hash(i * 5) * w, y + Hash(i * 5 + 1) * h);
                        float glow = 0.5f + 0.5f * Mathf.Sin(t * 2f + i);
                        var crack = new Rgba(1f, 0.45f, 0.15f, (0.1f + 0.12f * glow) * alpha);
                        for (int k = 0; k < 3; k++)
                        {
                            V2 q = p + V2.FromAngle(Hash(i * 5 + 2 + k) * MathX.Tau) * (10f + 14f * Hash(i * 7 + k));
                            if (q.X > x && q.X < x + w && q.Y > y && q.Y < y + h)
                                Line(p, q, 1.5f, crack);
                        }
                    }
                    break;
                case PathId.FrozenLake:  // cracked ice with slow sheens of light sliding across
                {
                    var crack = new Rgba(0.85f, 0.97f, 1f, 0.18f * alpha);
                    for (int i = 0; i < (int)(w * h / 26000f) + 2; i++)
                    {
                        V2 p = new V2(x + Hash(i * 7) * w, y + Hash(i * 7 + 1) * h);
                        float a = Hash(i * 7 + 2) * MathX.Tau;
                        for (int k = 0; k < 4; k++)
                        {
                            a += (Hash(i * 7 + 3 + k) - 0.5f) * 1.4f;
                            V2 q = p + V2.FromAngle(a) * (18f + 26f * Hash(i * 9 + k));
                            if (q.X < x || q.X > x + w || q.Y < y || q.Y > y + h)
                                break;
                            Line(p, q, 1.2f, crack);
                            p = q;
                        }
                    }
                    for (int k = 0; k < 3; k++)  // sheens
                    {
                        float f = MathX.Fmod(t * 0.06f + k / 3f, 1f), sx = x - w * 0.3f + f * w * 1.6f;
                        V2 a0 = new V2(Mathf.Clamp(sx, x, x + w), y), a1 = new V2(Mathf.Clamp(sx + 40f, x, x + w), y);
                        V2 b0 = new V2(Mathf.Clamp(sx - h * 0.4f, x, x + w), y + h), b1 = new V2(Mathf.Clamp(sx - h * 0.4f + 40f, x, x + w), y + h);
                        SetColor(new Rgba(1f, 1f, 1f, 0.05f * alpha));
                        Tri(a0, a1, b1);
                        Tri(a0, b1, b0);
                    }
                    break;
                }
                case PathId.BloodMoon:  // a red moon in the corner and drifting red mist
                {
                    float mr = Mathf.Min(w, h) * 0.13f;
                    V2 moon = new V2(x + w - mr * 1.6f, y + mr * 1.5f);
                    Circle(moon, mr * 2.2f, new Rgba(1f, 0.2f, 0.25f, 0.06f * alpha), 32);
                    Circle(moon, mr * 1.4f, new Rgba(1f, 0.25f, 0.3f, 0.1f * alpha), 32);
                    Circle(moon, mr, new Rgba(0.85f, 0.2f, 0.22f, 0.55f * alpha), 32);
                    Circle(moon + new V2(-mr * 0.3f, -mr * 0.2f), mr * 0.25f, new Rgba(0.6f, 0.1f, 0.12f, 0.4f * alpha), 16);
                    for (int i = 0; i < 6; i++)
                    {
                        float mx = x + MathX.Fmod(Hash(i * 3) * w + t * (8f + 6f * Hash(i * 3 + 1)), w);
                        Circle(new V2(mx, y + Hash(i * 3 + 2) * h), Mathf.Min(w, h) * (0.12f + 0.1f * Hash(i)), new Rgba(0.6f, 0.05f, 0.1f, 0.08f * alpha), 24);
                    }
                    break;
                }
                case PathId.Sanctuary:  // soft motes of light drifting up
                    for (int i = 0; i < (int)(w * h / 5000f); i++)
                    {
                        float fy = MathX.Fmod(Hash(i * 3 + 1) + t * (0.02f + 0.03f * Hash(i * 3 + 2)), 1f);
                        V2 p = new V2(x + Hash(i * 3) * w, y + h - fy * h);
                        Circle(p, 1.5f + Hash(i) * 1.5f, new Rgba(0.6f, 0.9f, 1f, 0.25f * alpha * Mathf.Sin(fy * MathX.Pi)), 8);
                    }
                    break;
            }
        }

        // --- Fire patches and healing pools ------------------------------------------------------------

        void DrawPathPatches(float t)
        {
            foreach (var pt in game.Patches.Values)
                DrawPatch(pt, t);
        }

        /// One patch (current GL coordinates). Fire: a warning ring that counts down to ignition, then burning
        /// ground with flickering flame tongues and rising embers. Heal: a soft pool with rising plus signs.
        void DrawPatch(PathPatch pt, float t)
        {
            float r = pt.Radius, fade = Mathf.Clamp01((pt.Life - pt.Age) / 1f);
            if (pt.Kind == PatchKind.Fire)
            {
                if (!pt.Active)
                {
                    float f = pt.Age / pt.Warn, blink = MathX.Fmod(t, f > 0.7f ? 0.12f : 0.25f) < (f > 0.7f ? 0.06f : 0.125f) ? 1f : 0.5f;
                    Circle(pt.Pos, r, new Rgba(1f, 0.35f, 0.1f, 0.06f + 0.14f * f), 32);
                    Arc(pt.Pos, r, 2.5f, new Rgba(1f, 0.45f, 0.15f, 0.7f * blink), 32);
                    Arc(pt.Pos, r * (1f - f), 2f, new Rgba(1f, 0.75f, 0.3f, 0.6f), 32);  // closes in until it lights
                    return;
                }
                Circle(pt.Pos, r, new Rgba(0.9f, 0.25f, 0.08f, 0.32f * fade), 36);
                Circle(pt.Pos, r * (0.62f + 0.05f * Mathf.Sin(t * 9f + pt.Id)), new Rgba(1f, 0.6f, 0.15f, 0.28f * fade), 28);
                Circle(pt.Pos, r * 0.3f, new Rgba(1f, 0.9f, 0.45f, 0.22f * fade), 20);
                for (int k = 0; k < 12; k++)  // flame tongues licking out of the edge
                {
                    float a = MathX.Tau * k / 12f + pt.Id, len = 10f + 7f * Mathf.Sin(t * 11f + k * 1.7f + pt.Id);
                    V2 d = V2.FromAngle(a), s = d.Orthogonal();
                    SetColor(new Rgba(1f, 0.5f + 0.2f * Mathf.Sin(t * 7f + k), 0.15f, 0.6f * fade));
                    Tri(pt.Pos + d * (r + len), pt.Pos + d * (r - 6f) + s * 6f, pt.Pos + d * (r - 6f) - s * 6f);
                }
                for (int k = 0; k < 6; k++)  // embers rising
                {
                    float f = MathX.Fmod(t * 0.8f + k / 6f + pt.Id * 0.13f, 1f);
                    V2 at = pt.Pos + new V2((Hash(pt.Id * 11 + k) - 0.5f) * r * 1.4f, r * 0.3f - f * r * 1.2f);
                    Circle(at, 2f * (1f - f) + 0.6f, new Rgba(1f, 0.75f, 0.3f, 0.8f * (1f - f) * fade), 6);
                }
            }
            else
            {
                float a = Mathf.Clamp01(pt.Age / pt.Warn) * fade, pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f + pt.Id);
                Circle(pt.Pos, r, new Rgba(0.4f, 0.85f, 1f, 0.16f * a), 36);
                Circle(pt.Pos, r * 0.55f, new Rgba(0.7f, 0.95f, 1f, 0.12f * a), 28);
                Arc(pt.Pos, r, 2.5f, new Rgba(0.6f, 0.95f, 1f, (0.45f + 0.3f * pulse) * a), 40);
                for (int k = 0; k < 4; k++)  // plus signs drifting up
                {
                    float f = MathX.Fmod(t * 0.5f + k / 4f + pt.Id * 0.21f, 1f);
                    V2 at = pt.Pos + new V2((Hash(pt.Id * 13 + k) - 0.5f) * r * 1.2f, r * 0.4f - f * r);
                    var c = new Rgba(0.75f, 1f, 0.85f, 0.8f * Mathf.Sin(f * MathX.Pi) * a);
                    Line(at - new V2(4f, 0f), at + new V2(4f, 0f), 2f, c);
                    Line(at - new V2(0f, 4f), at + new V2(0f, 4f), 2f, c);
                }
            }
        }

        /// The path card's sample patches (fire cycling through warning and burning; a pool).
        readonly PathPatch[] cardPatches = { new PathPatch { Id = 901 }, new PathPatch { Id = 902 } };

        void DrawCardPatches(PathDef d, Rect stage, float t)
        {
            if (d.Id != PathId.BurningGrounds && d.Id != PathId.Sanctuary)
                return;
            for (int i = 0; i < 2; i++)
            {
                var pt = cardPatches[i];
                bool fire = d.Id == PathId.BurningGrounds;
                if (!fire && i == 1)
                    break;
                pt.Kind = fire ? PatchKind.Fire : PatchKind.Heal;
                pt.Warn = fire ? Paths.FireWarn : Paths.PoolWarn;
                pt.Life = fire ? 5f : 9f;
                pt.R0 = fire ? 26f : 44f;
                pt.R1 = fire ? 40f : 44f;
                pt.Pos = new V2(stage.x + stage.width * (fire ? (i == 0 ? 0.22f : 0.78f) : 0.78f), stage.y + stage.height * (i == 0 ? 0.62f : 0.4f));
                pt.Age = MathX.Fmod(t + i * 2.3f, pt.Life - 0.2f);
                DrawPatch(pt, t);
            }
        }

        /// In the arena while a path is being played: its floor, faint so the fight stays readable.
        void DrawPathArena(float t)
        {
            if (!game.OnPath || game.Wave % 5 == 0)
                return;
            DrawPathFloor(Paths.Get(game.CurrentPath), 0f, 0f, AW, AH, t, 0.45f);
        }

        // --- HUD chip -------------------------------------------------------------------------------

        /// Under the wave plate: the path's name and how many waves are left on it. Returns the height used.
        float DrawPathChip(float x, float y)
        {
            var d = Paths.Get(game.CurrentPath);
            if (d == null)
                return 0f;
            int left = game.PathWavesLeft;
            string text = d.Name.ToUpperInvariant() + "  ·  " + (game.OnPath ? left + " wave" + (left == 1 ? "" : "s") + " left" : "starts next wave");
            float w = 30f + text.Length * 6.4f + 16f * d.Danger;
            var chip = new Rect(x, y, w, 20f);
            var accent = ToColor(d.Accent);
            Ui.Pill(chip, new Color(0f, 0f, 0f, 0.55f));
            Ui.Fill(new Rect(chip.x + 3f, chip.y + 4f, 3f, chip.height - 8f), accent);
            Ui.Label(new Rect(chip.x + 12f, chip.y, w, chip.height), text, 10, accent, TextAnchor.MiddleLeft, true);
            for (int k = 0; k < d.Danger; k++)
                Ui.Icon(new Rect(chip.xMax - 8f - (d.Danger - k) * 15f, chip.y + 4f, 12f, 12f), PixelArt.Get("ui_skull"), new Color(1f, 0.5f, 0.5f));
            return 24f;
        }
    }
}

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
                if (d.Id == PathId.NarrowPass)
                    DrawNarrowWalls(stage.x, stage.y, stage.xMax, stage.yMax, new V2(stage.x + stage.width * 0.17f, stage.y + stage.height * 0.13f),
                        new V2(stage.xMax - stage.width * 0.17f, stage.yMax - stage.height * 0.13f), 1f, d.Accent, t);
                UiNoClip();
                for (int k = 0; k < d.Showcase.Length; k++)
                {
                    // In the pass, the enemies crowd together between the walls.
                    float fx = d.Id == PathId.NarrowPass ? 0.3f + 0.2f * k : (k + 0.5f) / d.Showcase.Length;
                    DrawEnemyPreview(d.Showcase[k], new V2(stage.x + stage.width * fx, stage.center.y + (k % 2 == 0 ? -10f : 12f)), 46f, t + k + index, true);
                }
                if (d.Id == PathId.Fogbound)
                {
                    // A lantern in the middle: the enemy next to it shows, the others are shapes in the mist.
                    var lamp = new V2(stage.center.x + 34f * Mathf.Sin(t * 0.7f), stage.center.y + 30f);
                    cardLights.Clear();
                    cardLights.Add(lamp);
                    DrawFog(stage.x, stage.y, stage.width, stage.height, 24f, cardLights, 70f, 50f, 1f, t);
                    Circle(lamp, 14f, new Rgba(1f, 0.9f, 0.6f, 0.15f), 20);
                    Circle(lamp, 5f, new Rgba(1f, 0.92f, 0.65f, 0.95f), 12);
                }
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
            Ui.Label(new Rect(x + 110f, y, w - 110f, 18f), Paths.PrizeText(d) + " before the boss", 13, Ui.Gold, TextAnchor.MiddleLeft, true);
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
                case PathId.Bazaar:  // market awnings: warm diagonal stripes, with a few coins
                    for (float xx = x - h; xx < x + w; xx += 36f)
                    {
                        float x1 = Mathf.Max(x, xx), x2 = Mathf.Min(x + w, xx + h);
                        if (x2 > x1)
                            Line(new V2(x1, y + (x1 - xx)), new V2(x2, y + (x2 - xx)), 6f, d.Accent.WithAlpha(0.07f * alpha));
                    }
                    for (int i = 0; i < (int)(w * h / 9000f); i++)
                    {
                        var c = new V2(x + 8f + Hash(i * 5) * (w - 16f), y + 8f + Hash(i * 5 + 1) * (h - 16f));
                        Circle(c, 4f, d.Accent.WithAlpha(0.22f * alpha), 10);
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
                case PathId.Fogbound:  // pale wisps of mist drifting across
                    for (int i = 0; i < (int)(w * h / 30000f) + 3; i++)
                    {
                        float mx = x + MathX.Fmod(Hash(i * 5) * w + t * (10f + 8f * Hash(i * 5 + 1)), w);
                        V2 at = new V2(mx, y + Hash(i * 5 + 2) * h);
                        float rr = Mathf.Min(w, h) * (0.08f + 0.08f * Hash(i * 5 + 3));
                        Circle(at, rr * 1.8f, new Rgba(0.8f, 0.85f, 0.95f, 0.035f * alpha), 24);
                        Circle(at + new V2(rr * 0.8f, rr * 0.2f), rr, new Rgba(0.85f, 0.9f, 1f, 0.05f * alpha), 20);
                    }
                    break;
                case PathId.NarrowPass:  // canyon stone: layers of sediment and scattered pebbles
                    for (float yy = y + 10f; yy < y + h; yy += 26f)
                    {
                        V2 prev = new V2(x, yy);
                        for (float xx = x + 20f; xx <= x + w; xx += 20f)
                        {
                            var p = new V2(xx, yy + 5f * Mathf.Sin(xx * 0.012f + yy * 0.3f) + 3f * Mathf.Sin(xx * 0.04f + yy));
                            Line(prev, p, 1.2f, line);
                            prev = p;
                        }
                    }
                    for (int i = 0; i < (int)(w * h / 7000f); i++)
                        Circle(new V2(x + Hash(i * 3) * w, y + Hash(i * 3 + 1) * h), 1.5f + 2f * Hash(i * 3 + 2), d.Accent.WithAlpha(0.12f * alpha), 6);
                    break;
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
            if (pt.Kind == PatchKind.Gas)
            {
                DrawGasPatch(pt, r, fade, t);
                return;
            }
            if (pt.Kind == PatchKind.Lava)
            {
                // The Colossus's lava (Colossus.cs): a dull glow while it settles, then bubbling molten rock.
                float a = Mathf.Clamp01((pt.Life - pt.Age) / 0.8f), warm = pt.Active ? 1f : pt.Age / Mathf.Max(0.01f, pt.Warn);
                Circle(pt.Pos, r, new Rgba(0.75f, 0.18f, 0.06f, (pt.Active ? 0.5f : 0.15f * warm) * a), 32);
                Circle(pt.Pos, r * 0.7f, new Rgba(1f, 0.45f, 0.12f, (pt.Active ? 0.45f : 0.1f * warm) * a), 28);
                Arc(pt.Pos, r, 2.5f, new Rgba(1f, 0.55f, 0.2f, (pt.Active ? 0.7f : 0.4f + 0.3f * Mathf.Sin(t * 20f)) * a), 32);
                if (pt.Active)
                    for (int k = 0; k < 4; k++)  // bubbles
                    {
                        float f = MathX.Fmod(t * 0.9f + k * 0.25f + pt.Id * 0.17f, 1f);
                        V2 at = pt.Pos + new V2((Hash(pt.Id * 7 + k) - 0.5f) * r * 1.1f, (Hash(pt.Id * 5 + k) - 0.5f) * r * 1.1f);
                        Circle(at, 2f + 4f * f, new Rgba(1f, 0.85f, 0.45f, 0.7f * (1f - f) * a), 10);
                    }
                return;
            }
            if (pt.Kind == PatchKind.Honey)
            {
                if (pt.Active)  // a sticky amber puddle (the glob is still in the air until then)
                {
                    float a = Mathf.Clamp01((pt.Age - pt.Warn) / 0.15f) * Mathf.Clamp01((pt.Life - pt.Age) / 0.5f);
                    Circle(pt.Pos, r, new Rgba(0.86f, 0.55f, 0.08f, 0.45f * a), 28);
                    Circle(pt.Pos + new V2(-r * 0.25f, -r * 0.2f), r * 0.3f, new Rgba(1f, 0.86f, 0.47f, 0.35f * a), 14);
                    Arc(pt.Pos, r, 2f, new Rgba(1f, 0.75f, 0.3f, 0.5f * a), 28);
                }
                return;
            }
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

        // --- Arena rules: Narrow Pass's walls and Fogbound's mist -----------------------------------------

        readonly System.Collections.Generic.List<V2> fogLights = new System.Collections.Generic.List<V2>(), cardLights = new System.Collections.Generic.List<V2>();

        /// Over the arena's contents: the walls closing in, and the mist hiding what's far from every player.
        void DrawArenaRules(float t)
        {
            DrawEclipseSky(t);  // the Umbra's dark and Last Light's burning edge (EclipseLook.cs)
            if (game.NarrowF > 0f)
                DrawNarrowWalls(0f, 0f, AW, AH, game.ArenaMin, game.ArenaMax, game.NarrowF, Paths.Get(PathId.NarrowPass).Accent, t);
            else if (game.TinyInset.X > 0.5f)
            {
                // Tiny Arena: the same rock, glowing brighter while it moves.
                float f = Mathf.Clamp01(game.TinyInset.X / 40f) * (game.TinyMoving ? 0.999f : 1f);
                DrawNarrowWalls(0f, 0f, AW, AH, game.ArenaMin, game.ArenaMax, f, TinyAccent, t);
            }
            if (game.NightF > 0f)
                DrawNight(t);
            if (game.FogF > 0f)
            {
                fogLights.Clear();
                foreach (var p in game.Players.Values)
                    fogLights.Add(p.Disp);
                DrawFog(0f, 0f, AW, AH, 40f, fogLights, Paths.FogSight, Paths.FogEdge, game.FogF, t);
            }
        }

        /// Canyon rock outside the pass (x0..x1, y0..y1 is the whole floor; lo..hi the pass), with layered strata,
        /// boulders along the foot of the walls and a glowing edge with runes that pulse along it.
        void DrawNarrowWalls(float x0, float y0, float x1, float y1, V2 lo, V2 hi, float f, Rgba accent, float t)
        {
            float a = Mathf.Clamp01(f * 3f);
            var rock = new Rgba(0.12f, 0.085f, 0.07f, 0.97f * a);
            FillRect(x0, y0, x1 - x0, lo.Y - y0, rock);
            FillRect(x0, hi.Y, x1 - x0, y1 - hi.Y, rock);
            FillRect(x0, lo.Y, lo.X - x0, hi.Y - lo.Y, rock);
            FillRect(hi.X, lo.Y, x1 - hi.X, hi.Y - lo.Y, rock);
            // Strata: wavy layers following each wall, further apart the deeper into the rock.
            var strata = new Rgba(0.3f, 0.2f, 0.14f, 0.55f * a);
            for (float d = 10f, k = 0; ; d += 12f + 6f * k, k++)
            {
                bool any = false;
                if (lo.Y - y0 > d) { WallLayer(new V2(x0, lo.Y - d), new V2(x1, lo.Y - d), k, strata); any = true; }
                if (y1 - hi.Y > d) { WallLayer(new V2(x0, hi.Y + d), new V2(x1, hi.Y + d), k + 7, strata); any = true; }
                if (lo.X - x0 > d) { WallLayer(new V2(lo.X - d, lo.Y), new V2(lo.X - d, hi.Y), k + 13, strata); any = true; }
                if (x1 - hi.X > d) { WallLayer(new V2(hi.X + d, lo.Y), new V2(hi.X + d, hi.Y), k + 19, strata); any = true; }
                if (!any)
                    break;
            }
            // Boulders at the foot of each wall.
            V2[] corners = { lo, new V2(hi.X, lo.Y), hi, new V2(lo.X, hi.Y) };
            for (int side = 0; side < 4; side++)
            {
                V2 s0 = corners[side], s1 = corners[(side + 1) % 4], along = (s1 - s0).Normalized(), outward = along.Orthogonal();
                V2 probe = (s0 + s1) / 2f + outward * 10f;
                if (probe.X > lo.X && probe.X < hi.X && probe.Y > lo.Y && probe.Y < hi.Y)
                    outward = -outward;  // into the rock
                float len = s0.DistanceTo(s1);
                for (int i = 0; i < (int)(len / 38f); i++)
                {
                    float u = (i + Hash(side * 97 + i)) * 38f;
                    float r = 5f + 8f * Hash(side * 53 + i * 3);
                    V2 at = s0 + along * u + outward * (r * 0.6f + 2f);
                    Circle(at, r, new Rgba(0.2f + 0.08f * Hash(i * 7 + side), 0.14f, 0.1f, 0.95f * a), 10);
                    Circle(at - outward * r * 0.3f, r * 0.45f, new Rgba(0.36f, 0.26f, 0.19f, 0.6f * a), 8);
                }
            }
            // The glowing edge, brightest while the walls are still closing in.
            float pulse = 0.75f + 0.25f * Mathf.Sin(t * 2.4f), closing = f < 1f ? 1.6f : 1f;
            for (int side = 0; side < 4; side++)
            {
                V2 s0 = corners[side], s1 = corners[(side + 1) % 4];
                Line(s0, s1, 16f, accent.WithAlpha(0.07f * a * closing));
                Line(s0, s1, 7f, accent.WithAlpha(0.18f * a * pulse * closing));
                Line(s0, s1, 2.5f, accent.WithAlpha(0.85f * a));
                float len = s0.DistanceTo(s1);
                V2 along = (s1 - s0) / Mathf.Max(1f, len);
                for (float u = 60f; u < len - 30f; u += 120f)
                {
                    // Runes: a light runs round the walls, so they read as alive (and dangerous to hug).
                    float wave = 0.5f + 0.5f * Mathf.Sin(t * 3f - (u + side * 1000f) * 0.01f);
                    V2 c = s0 + along * u, n = along.Orthogonal() * 5f;
                    SetColor(accent.WithAlpha((0.35f + 0.6f * wave) * a));
                    Quad(c - along * 5f, c + n, c + along * 5f, c - n);
                }
            }
        }

        static readonly Rgba TinyAccent = new Rgba(1f, 0.45f, 0.35f);

        // Nightfall: dark everywhere but round each player and in each player's lantern beam.
        const float NightSight = 210f, NightEdge = 90f, NightBeam = 560f, NightBeamHalf = 0.42f, NightCell = 30f;
        float[] nightAlpha;

        void DrawNight(float t)
        {
            int nx = Mathf.CeilToInt(AW / NightCell), ny = Mathf.CeilToInt(AH / NightCell);
            if (nightAlpha == null || nightAlpha.Length < (nx + 1) * (ny + 1))
                nightAlpha = new float[(nx + 1) * (ny + 1)];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    V2 p = new V2(Mathf.Min(AW, i * NightCell), Mathf.Min(AH, j * NightCell));
                    float dark = 1f;
                    foreach (var pl in game.Players.Values)
                    {
                        V2 rel = p - pl.Disp;
                        float d = rel.Length;
                        float round = Smooth((d - NightSight) / NightEdge);
                        // The lantern: a cone toward where they aim, fading toward its far end.
                        float off = Mathf.Abs(MathX.Fmod(rel.Angle - pl.Aim + MathX.Pi, MathX.Tau) - MathX.Pi);
                        float beam = d < 1f ? 0f : Mathf.Max(Smooth((off - NightBeamHalf) / 0.18f), Smooth((d - NightBeam) / 160f) * 0.85f + 0.15f * d / NightBeam);
                        if (!pl.Downed)
                            round = Mathf.Min(round, beam);
                        dark = Mathf.Min(dark, round);
                    }
                    nightAlpha[j * (nx + 1) + i] = 0.93f * game.NightF * dark;
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    float a00 = nightAlpha[k], a10 = nightAlpha[k + 1], a01 = nightAlpha[k + nx + 1], a11 = nightAlpha[k + nx + 2];
                    if (a00 + a10 + a01 + a11 <= 0.001f)
                        continue;
                    V2 p00 = new V2(i * NightCell, j * NightCell), p11 = new V2(Mathf.Min(AW, p00.X + NightCell), Mathf.Min(AH, p00.Y + NightCell));
                    V2 p10 = new V2(p11.X, p00.Y), p01 = new V2(p00.X, p11.Y);
                    NightVertex(p00, a00); NightVertex(p10, a10); NightVertex(p11, a11);
                    NightVertex(p00, a00); NightVertex(p11, a11); NightVertex(p01, a01);
                }
        }

        static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        void NightVertex(V2 p, float a)
        {
            GL.Color(new Color(0.015f, 0.02f, 0.06f, a));
            GL.Vertex(Px(p));
        }

        /// One layer of strata between two points: a wavy line seeded by k.
        void WallLayer(V2 a, V2 b, float k, Rgba c)
        {
            float len = a.DistanceTo(b);
            V2 along = (b - a) / Mathf.Max(1f, len), n = along.Orthogonal();
            V2 prev = a;
            for (float u = 24f; u <= len; u += 24f)
            {
                V2 p = a + along * u + n * (3f * Mathf.Sin(u * 0.03f + k * 1.7f) + 2f * Mathf.Sin(u * 0.11f + k));
                Line(prev, p, 1.5f, c);
                prev = p;
            }
        }

        /// The mist over a rectangle (current GL coordinates; no clipping): thick everywhere except within sight of
        /// a light, with a soft edge and a slow drift so it reads as fog rather than a vignette.
        void DrawFog(float x, float y, float w, float h, float cell, System.Collections.Generic.List<V2> lights, float sight, float edge, float density, float t)
        {
            int nx = Mathf.CeilToInt(w / cell), ny = Mathf.CeilToInt(h / cell);
            if (fogAlpha == null || fogAlpha.Length < (nx + 1) * (ny + 1))
                fogAlpha = new float[(nx + 1) * (ny + 1)];
            for (int j = 0; j <= ny; j++)
                for (int i = 0; i <= nx; i++)
                {
                    V2 p = new V2(x + Mathf.Min(w, i * cell), y + Mathf.Min(h, j * cell));
                    float near = float.MaxValue;
                    foreach (var l in lights)
                        near = Mathf.Min(near, (p - l).Length);
                    float f = Mathf.Clamp01((near - sight) / edge);
                    f = f * f * (3f - 2f * f);
                    float drift = 0.88f + 0.12f * Mathf.Sin(p.X * 0.011f + t * 0.35f) * Mathf.Sin(p.Y * 0.013f - t * 0.27f);
                    fogAlpha[j * (nx + 1) + i] = 0.94f * density * f * drift;
                }
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    int k = j * (nx + 1) + i;
                    float a00 = fogAlpha[k], a10 = fogAlpha[k + 1], a01 = fogAlpha[k + nx + 1], a11 = fogAlpha[k + nx + 2];
                    if (a00 + a10 + a01 + a11 <= 0.001f)
                        continue;  // clear around a player
                    V2 p00 = new V2(x + i * cell, y + j * cell), p11 = new V2(Mathf.Min(x + w, p00.X + cell), Mathf.Min(y + h, p00.Y + cell));
                    V2 p10 = new V2(p11.X, p00.Y), p01 = new V2(p00.X, p11.Y);
                    FogVertex(p00, a00); FogVertex(p10, a10); FogVertex(p11, a11);
                    FogVertex(p00, a00); FogVertex(p11, a11); FogVertex(p01, a01);
                }
        }

        float[] fogAlpha;

        void FogVertex(V2 p, float a)
        {
            GL.Color(new Color(0.12f, 0.13f, 0.16f, a));
            GL.Vertex(Px(p));
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

        /// The Warden's tear gas: a target ring while the canister's in the air, then a churning green cloud.
        void DrawGasPatch(PathPatch pt, float r, float fade, float t)
        {
            if (!pt.Active)
            {
                float f = pt.Age / pt.Warn;
                Arc(pt.Pos, r, 2.5f, new Rgba(0.6f, 1f, 0.35f, 0.4f + 0.4f * f), 32);
                Arc(pt.Pos, r * (1f - f), 2f, new Rgba(0.75f, 1f, 0.5f, 0.6f), 32);
                Line(pt.Pos - new V2(10f, 0f), pt.Pos + new V2(10f, 0f), 2f, new Rgba(0.7f, 1f, 0.45f, 0.7f));
                Line(pt.Pos - new V2(0f, 10f), pt.Pos + new V2(0f, 10f), 2f, new Rgba(0.7f, 1f, 0.45f, 0.7f));
                return;
            }
            float grow = Mathf.Clamp01((pt.Age - pt.Warn) / 0.4f);
            Circle(pt.Pos, r * grow, new Rgba(0.45f, 0.8f, 0.25f, 0.18f * fade), 36);
            for (int k = 0; k < 7; k++)  // puffs churning round inside it
            {
                float a = t * (0.5f + 0.1f * k) * (k % 2 == 0 ? 1f : -1f) + k * 0.9f + pt.Id, d = r * (0.25f + 0.08f * k);
                V2 at = pt.Pos + V2.FromAngle(a) * Mathf.Min(d, r * 0.62f) * grow;
                Circle(at, r * (0.34f + 0.04f * Mathf.Sin(t * 2f + k)) * grow, new Rgba(0.6f, 0.9f, 0.35f, 0.14f * fade), 20);
            }
            Arc(pt.Pos, r * grow, 2f, new Rgba(0.7f, 1f, 0.4f, 0.35f * fade), 40);
        }
    }
}

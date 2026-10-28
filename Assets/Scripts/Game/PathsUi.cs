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
                    route += (i > 0 ? "  >  " : "") + Paths.Get(game.Route[i]).Name;
                Ui.Label(new Rect(0, 90f, W, 18f), route, 11, Ui.Muted, TextAnchor.MiddleCenter);
            }
            float cw = 440f, ch = 470f, gap = 40f, y = 118f;
            for (int i = 0; i < 2; i++)
            {
                var r = new Rect(W / 2f - cw - gap / 2f + i * (cw + gap), y, cw, ch);
                if (DrawPathCard(r, Paths.Get(offer[i]), t, i))
                    game.ChoosePath(i);
            }
            string waitText = session.IsHost ? "You're the host: pick the path for everyone" : "Waiting for " + HostName() + " to pick the path";
            Ui.Label(new Rect(0, y + ch + 14f, W, 22f), waitText, 14, session.IsHost ? Ui.Gold : Ui.Muted, TextAnchor.MiddleCenter, true);
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
                DrawPathFloor(d, stage.x, stage.y, stage.width, stage.height, t, 1f);
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

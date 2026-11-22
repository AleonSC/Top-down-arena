using UnityEngine;

namespace Arena
{
    /// The Boss Hunt tab's look (HuntUi.cs), shared by every game-mode tab (Quick Play, Campaign, Survival): the spaced header, the
    /// list rows on the left (soft keys with a number, a portrait or "?", a name and a status chip), the phosphor scope on the
    /// right, the pip ladder and the outlined rule tags.
    public partial class ArenaApp
    {
        const float ModeListW = 268f, ModeGap = 14f;

        /// The header (the Dossier look): a progress ring (when `pct` is 0-1; `ringText` in its middle), the mode's name big and
        /// letter-spaced in `acc` with a rule, the mode's facts as outlined chips (`tag`, split on "·") and a short line under
        /// them. Returns where the body starts.
        static float ModeHeader(Rect r, string title, string tag, string sub, Color subCol, Color acc = default, float pct = -1f, string ringText = null)
        {
            var ink = CrtStyle.Ink;
            if (acc.a <= 0f) acc = ink;
            float x = r.x + 18f, w = r.width - 36f, y = r.y + 12f;
            float tx = x;
            if (pct >= 0f)
            {
                ModeRing(new Vector2(x + 32f, y + 32f), 29f, pct, acc, ringText);
                tx = x + 78f;
            }
            LobbyKit.Spaced(new Rect(tx, y, w * 0.5f, 26f), title, 20, acc, TextAnchor.MiddleLeft, 4.4f);
            float l0 = tx + LobbyKit.SpacedWidth(title, 20, 4.4f) + 20f;
            Ui.Fill(new Rect(l0, y + 13f, x + w - l0, 1f), acc.WithAlpha(0.35f));
            ModeChips(tx, y + 32f, tag, acc);
            if (!string.IsNullOrEmpty(sub))
                Ui.Label(new Rect(tx, y + 56f, x + w - tx, 18f), sub, 12, subCol);
            return y + 82f;
        }

        /// A progress ring: 40 dots round `c` (radius `rad`), the first `pct` of them lit in `acc`, `text` in the middle.
        static void ModeRing(Vector2 c, float rad, float pct, Color acc, string text)
        {
            const int n = 40;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(pct) * n);
            for (int k = 0; k < n; k++)
            {
                float a = -Mathf.PI / 2f + k * Mathf.PI * 2f / n;
                Ui.Fill(new Rect(c.x + Mathf.Cos(a) * rad - 1.5f, c.y + Mathf.Sin(a) * rad - 1.5f, 3f, 3f), k < lit ? acc : CrtStyle.Dim.WithAlpha(0.45f));
            }
            if (!string.IsNullOrEmpty(text))
                CrtUi.Text(new Rect(c.x - rad + 5f, c.y - 11f, rad * 2f - 10f, 22f), text, 13, Color.white, TextAnchor.MiddleCenter, true, false);
        }

        /// The mode's facts as outlined chips in `c`, left to right from x (`tags` split on "·"). Returns the x after the last one.
        static float ModeChips(float x, float y, string tags, Color c)
        {
            foreach (var part in tags.Split('·'))
            {
                string f = part.Trim();
                if (f.Length == 0) continue;
                float fw = LobbyKit.SpacedWidth(f, 10, 1f) + 12f;
                var fr = new Rect(x, y, fw, 18f);
                Ui.Fill(fr, c.WithAlpha(0.06f));
                HuntBox(fr, c.WithAlpha(0.4f));
                LobbyKit.Spaced(fr, f, 10, c.WithAlpha(0.85f), TextAnchor.MiddleCenter, 1f);
                x += fw + 6f;
            }
            return x;
        }

        /// A list row: a soft key (glowing in `acc` on `top`), or dashed and dim when not `lit`. Number, a "?" when not `known`
        /// (the portrait goes at x + 30..66), the name and a chip on the right.
        void ModeRow(Rect c, string num, string name, bool known, bool lit, bool top, bool hover, Color acc, string chip, Color chipCol)
        {
            if (lit || top)
                CrtKit.SoftBox(c, top ? acc : CrtStyle.Ink, top, hover, false);
            else
            {
                var d = CrtStyle.Dim;
                Ui.Fill(c, d.WithAlpha(0.015f));
                CrtKit.Dashed(c, 1f, d.WithAlpha(0.3f));
            }
            Color tc = lit || top ? CrtKit.SoftText(top ? acc : CrtStyle.Ink, hover || top) : CrtStyle.Dim.WithAlpha(0.6f);
            Ui.Label(new Rect(c.x + 8f, c.y, 24f, c.height), num, 10, tc.WithAlpha(0.6f), TextAnchor.MiddleLeft);
            float chipW = string.IsNullOrEmpty(chip) ? 0f : LobbyKit.SpacedWidth(chip, 10, 1f) + 18f;
            CrtUi.Text(new Rect(c.x + 70f, c.y, c.width - 70f - Mathf.Max(78f, chipW), c.height), known ? name : "???", 14, tc, TextAnchor.MiddleLeft, true, false);
            if (!known)
                CrtUi.Text(new Rect(c.x + 30f, c.y, 36f, c.height), "?", 26, CrtStyle.Dim.WithAlpha(top ? 0.9f : 0.6f), TextAnchor.MiddleCenter, true, FxCalm);
            if (chipW > 0f)
                HuntChip(c.xMax - 8f, c.center.y, chip, chipCol);
        }

        /// A row's portrait: the model in the square left of its name (inside BeginUiGl).
        void ModeRowModel(Rect c, EnemyType e, float t, bool top)
        {
            UiClip(new Rect(c.x + 30f, c.y + 2f, 36f, c.height - 4f));
            DrawEnemyPreview(e == EnemyType.ReaperOfDawn ? EnemyType.ReaperOfDusk : e, new V2(c.x + 48f, c.center.y), Mathf.Min(34f, c.height - 6f), t, top);
        }

        /// The phosphor-green scope: the screen, its labels and (with `reticle`) the crosshair and range rings.
        static void ModeScope(Rect scope, string left, string right, bool reticle = true)
        {
            Ui.Fill(scope, new Color(0.012f, 0.067f, 0.04f, 1f));  // phosphor green screen (the mockup's #03110a)
            Ui.Fill(new Rect(scope.x + scope.width * 0.15f, scope.y + scope.height * 0.15f, scope.width * 0.7f, scope.height * 0.7f), new Color(0.04f, 0.16f, 0.1f, 0.55f));
            HuntBox(scope, new Color(0.11f, 0.23f, 0.16f, 1f));
            if (reticle)
            {
                Ui.Fill(new Rect(scope.x, scope.center.y, scope.width, 1f), PhosGreen.WithAlpha(0.22f));
                Ui.Fill(new Rect(scope.center.x, scope.y, 1f, scope.height), PhosGreen.WithAlpha(0.22f));
                foreach (float rad in new[] { 0.62f, 0.42f })
                {
                    float rr = scope.height * rad * 0.5f;
                    for (int k = 0; k < 48; k++)
                    {
                        float a = k * Mathf.PI * 2f / 48f;
                        Ui.Fill(new Rect(scope.center.x + Mathf.Cos(a) * rr * 1.6f - 1f, scope.center.y + Mathf.Sin(a) * rr - 1f, 2f, 2f), PhosGreen.WithAlpha(0.28f));
                    }
                }
            }
            LobbyKit.Spaced(new Rect(scope.x + 10f, scope.y + 6f, scope.width * 0.5f, 16f), left, 11, PhosGreen, TextAnchor.MiddleLeft, 2f);
            LobbyKit.Spaced(new Rect(scope.center.x, scope.y + 6f, scope.width * 0.5f - 10f, 16f), right, 11, CrtStyle.Gold, TextAnchor.MiddleRight, 2f);
        }

        /// The scope's model, centred (inside BeginUiGl).
        void ModeScopeModel(Rect scope, EnemyType e, float t)
        {
            UiClip(new Rect(scope.x + 2f, scope.y + 22f, scope.width - 4f, scope.height - 44f));
            DrawEnemyPreview(e == EnemyType.ReaperOfDawn ? EnemyType.ReaperOfDusk : e, new V2(scope.center.x, scope.center.y), scope.height - 56f, t, true);
        }

        /// CRT finish over the scope (and the model): scan lines, then a vignette toward the edges.
        static void ModeScopeFinish(Rect scope)
        {
            for (float sy = scope.y + 1f; sy < scope.yMax - 1f; sy += 3f)
                Ui.Fill(new Rect(scope.x + 1f, sy, scope.width - 2f, 1f), new Color(0f, 0f, 0f, 0.25f));
            for (int k = 0; k < 8; k++)
            {
                float a = 0.30f * (1f - k / 8f) * (1f - k / 8f);
                float d = 1f + k * 4f;
                var vc = new Color(0f, 0f, 0f, a);
                Ui.Fill(new Rect(scope.x + d, scope.y + d, scope.width - 2f * d, 4f), vc);
                Ui.Fill(new Rect(scope.x + d, scope.yMax - d - 4f, scope.width - 2f * d, 4f), vc);
                Ui.Fill(new Rect(scope.x + d, scope.y + d + 4f, 4f, scope.height - 2f * d - 8f), vc);
                Ui.Fill(new Rect(scope.xMax - d - 4f, scope.y + d + 4f, 4f, scope.height - 2f * d - 8f), vc);
            }
        }

        /// One pip of a wave ladder: a tinted box, outlined, its label centred (white on a `strong` one).
        static void ModePip(Rect pr, string label, Color pc, bool strong)
        {
            Ui.Fill(pr, pc.WithAlpha(strong ? 0.3f : 0.15f));
            HuntBox(pr, pc.WithAlpha(0.7f));
            Ui.Label(pr, label, 10, strong ? Color.white : Ui.Muted, TextAnchor.MiddleCenter);
        }

        /// The rules as a short list, a marker before each, in two columns when wide. Returns the y of the last row.
        static float ModeRules(float x, float y, float w, string[] rules)
        {
            var ink = CrtStyle.Ink.WithAlpha(0.75f);
            bool two = w > 520f && rules.Length > 3;
            int per = two ? (rules.Length + 1) / 2 : rules.Length;
            float cw = two ? w / 2f : w, last = y;
            for (int i = 0; i < rules.Length; i++)
            {
                float rx = x + (i / per) * cw, ry = y + (i % per) * 18f;
                Ui.Label(new Rect(rx, ry, 14f, 16f), ">", 11, CrtStyle.Gold.WithAlpha(0.8f), TextAnchor.MiddleLeft);
                Ui.Label(new Rect(rx + 14f, ry, cw - 14f, 16f), rules[i].Substring(0, 1) + rules[i].Substring(1).ToLowerInvariant(), 12, ink, TextAnchor.MiddleLeft);
                last = Mathf.Max(last, ry);
            }
            return last;
        }

        /// A row of record tiles across w.
        static void ModeTiles(float x, float y, float w, float h, string[] vals, string[] labels)
        {
            int n = vals.Length;
            float tw = (w - 8f * (n - 1)) / n;
            for (int i = 0; i < n; i++)
                LobbyKit.Tile(new Rect(x + i * (tw + 8f), y, tw, h), vals[i], labels[i], false);
        }

        static string ModeClock(float s) => s > 0f ? (int)(s / 60f) + ":" + ((int)s % 60).ToString("00") : "-";
    }
}

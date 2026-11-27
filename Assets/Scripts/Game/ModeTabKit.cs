using UnityEngine;

namespace Arena
{
    /// The pieces of the Game Mode page (ModePageUi.cs): the progress ring, the fact chips, the list
    /// rows (soft keys with a number, a portrait or "?", a name and a status chip) and the phosphor scope.
    public partial class ArenaApp
    {
        
        /// A progress ring: 40 dots round `c` (radius `rad`), the first `pct` of them lit in `acc`, `text` in the middle.
        static void ModeRing(Vector2 c, float rad, float pct, Color acc, string text, int textSize = 13)
        {
            const int n = 40;
            int lit = Mathf.RoundToInt(Mathf.Clamp01(pct) * n);
            for (int k = 0; k < n; k++)
            {
                float a = -Mathf.PI / 2f + k * Mathf.PI * 2f / n;
                Ui.Fill(new Rect(c.x + Mathf.Cos(a) * rad - 1.5f, c.y + Mathf.Sin(a) * rad - 1.5f, 3f, 3f), k < lit ? acc : CrtStyle.Dim.WithAlpha(0.45f));
            }
            if (!string.IsNullOrEmpty(text))
                CrtUi.Text(new Rect(c.x - rad + 5f, c.y - 11f, rad * 2f - 10f, 22f), text, textSize, Color.white, TextAnchor.MiddleCenter, true, false);
        }

        /// A list row: a soft key (glowing in `acc` on `top`), or dashed and dim when not `lit`. Number, a "?" when not `known`
        /// (the portrait goes at x + 30..66; with `portrait` it shows even then), the name and a chip on the right.
        void ModeRow(Rect c, string num, string name, bool known, bool lit, bool top, bool hover, Color acc, string chip, Color chipCol, bool portrait = false)
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
            if (!known && !portrait)
                CrtUi.Text(new Rect(c.x + 30f, c.y, 36f, c.height), "?", 26, CrtStyle.Dim.WithAlpha(top ? 0.9f : 0.6f), TextAnchor.MiddleCenter, true, FxCalm);
            if (chipW > 0f)
                HuntChip(c.xMax - 8f, c.center.y, chip, chipCol);
        }

        /// A row's portrait: the model in the square left of its name (inside BeginUiGl).
        void ModeRowModel(Rect c, EnemyType e, float t, bool top)
        {
            UiClip(new Rect(c.x + 30f, c.y + 2f, 36f, c.height - 4f));
            DrawEnemyPreview(e == EnemyType.ReaperOfDawn ? EnemyType.ReaperOfDusk : e, new V2(c.x + 48f, c.center.y), Mathf.Min(34f, c.height - 6f), t, top, ModeFacing0);
        }

        /// The scope: the loadout preview's spotlight (LobbyUi.cs DrawShowcaseFloor): a dark floor and a soft pool of light in the
        /// preview's colour (your role's, or the hue picked on its colour wheel: ShowcaseGlow), a thin frame in it, and its labels.
        void ModeScope(Rect scope, string left, string right)
        {
            var glow = ScopeGlow();
            Ui.Fill(scope, new Color(0.025f, 0.03f, 0.05f, 1f));
            GUI.BeginGroup(scope);  // (clips the light to the screen)
            float rr = scope.height * 0.62f, rw = rr * 1.15f;
            var c = new Vector2(scope.width / 2f, scope.height / 2f + 6f);
            Ui.RadialGlow(new Rect(c.x - rw, c.y - rr, rw * 2f, rr * 2f), glow.WithAlpha(0.24f));
            Ui.RadialGlow(new Rect(c.x - rr * 0.43f, c.y - rr * 0.43f, rr * 0.86f, rr * 0.86f), new Color(1f, 1f, 1f, 0.05f));
            GUI.EndGroup();
            HuntBox(scope, glow.WithAlpha(0.45f));
            LobbyKit.Spaced(new Rect(scope.x + 10f, scope.y + 6f, scope.width * 0.5f, 16f), left, 11, glow, TextAnchor.MiddleLeft, 2f);
            LobbyKit.Spaced(new Rect(scope.center.x, scope.y + 6f, scope.width * 0.5f - 10f, 16f), right, 11, CrtStyle.Gold, TextAnchor.MiddleRight, 2f);
        }

        /// The scope's light: the same colour as your loadout preview's.
        Color ScopeGlow()
        {
            LobbyEntry me;
            var role = session.Lobby.TryGetValue(session.MyId, out me) ? me.Role : Role.Dps;
            return ShowcaseGlow(Defs.Get(role).Color.ToColor());
        }

        /// The scope's model, centred (inside BeginUiGl), held at `modeFacing` (turned by dragging in the scope).
        void ModeScopeModel(Rect scope, EnemyType e, float t)
        {
            UiClip(new Rect(scope.x + 2f, scope.y + 22f, scope.width - 4f, scope.height - 44f));
            DrawEnemyPreview(e == EnemyType.ReaperOfDawn ? EnemyType.ReaperOfDusk : e, new V2(scope.center.x, scope.center.y), scope.height - 56f, t, true, modeFacing);
        }

        /// The models' resting heading (radians): facing down the screen, toward the player.
        const float ModeFacing0 = Mathf.PI * 0.5f;
        float modeFacing = ModeFacing0;
        float modeDragX = -1f;

        /// Click and drag across the scope to turn its model; it holds still otherwise. Returns true while dragging.
        bool ModeScopeDrag(Rect scope)
        {
            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && scope.Contains(e.mousePosition))
            {
                modeDragX = e.mousePosition.x;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag && modeDragX >= 0f)
            {
                modeFacing += (e.mousePosition.x - modeDragX) * 0.012f;
                modeDragX = e.mousePosition.x;
                e.Use();
            }
            else if (e.type == EventType.MouseUp && modeDragX >= 0f)
            {
                modeDragX = -1f;
                Ui.ForgetPress();
                e.Use();
            }
            return modeDragX >= 0f;
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

        static string ModeClock(float s) => s > 0f ? (int)(s / 60f) + ":" + ((int)s % 60).ToString("00") : "-";
    }
}

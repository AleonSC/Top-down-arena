using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Hangout's spawners (Core/HangoutSpawners.cs): the pad on the floor with a ghost of its enemy (DrawHangoutSpawners, world pass;
    /// the dev spawner's look, DevSpawnerLook.cs: grey and still when off, red with a turning rim when on), and over the one you stand
    /// on its enemy's name and the keys (DrawHangoutSpawnerTags, HUD). The keys: Interact turns it on or off, the discard key or the
    /// mouse wheel changes its enemy (UpdateWorldKeys, CameraZoom.cs).
    public partial class ArenaApp
    {
        static readonly Rgba HangoutPadOff = new Rgba(0.6f, 0.66f, 0.78f);
        readonly Dictionary<EnemyType, Enemy> hangoutGhosts = new Dictionary<EnemyType, Enemy>();

        void DrawHangoutSpawners(float t)
        {
            if (!game.HangoutOn)
                return;
            int near = game.Me != null ? game.SpawnerNear(game.Me.Pos) : -1;
            for (int i = 0; i < game.Spawners.Length; i++)
            {
                var s = game.Spawners[i];
                V2 c = s.Pos;
                const float R = 34f;
                Rgba ink = s.On ? SpawnerRed : HangoutPadOff;
                Circle(c, R + 4f, new Rgba(0f, 0f, 0f, 0.5f), 28);
                Circle(c, R - 2f, ink.WithAlpha(s.On ? 0.12f : 0.05f), 28);
                const int dashes = 12;
                float spin = s.On ? t * 1.2f : i * 0.4f;
                for (int k = 0; k < dashes; k++)
                {
                    float a0 = spin + k * MathX.Tau / dashes;
                    Arc(c, R + 2f, 2f, ink.WithAlpha(s.On ? 0.8f : 0.4f), 4, a0, MathX.Tau / dashes * 0.55f);
                }
                if (i == near)  // the one you stand on: a bright ring
                    Arc(c, R + 9f, 1.5f, ink.WithAlpha(0.55f + 0.25f * Mathf.Sin(t * 5f)), 36);
                // two chevrons pointing into the room, the way its enemies come
                V2 side = new V2(-s.Inward.Y, s.Inward.X);
                for (int k = 0; k < 2; k++)
                {
                    V2 tip = c + s.Inward * (R + 14f + k * 9f);
                    float a = s.On ? 0.35f + 0.5f * Mathf.Repeat(t * 1.5f - k * 0.3f, 1f) : 0.25f;
                    Line(tip - s.Inward * 6f - side * 7f, tip, 2f, ink.WithAlpha(a));
                    Line(tip - s.Inward * 6f + side * 7f, tip, 2f, ink.WithAlpha(a));
                }
                Enemy e;
                if (!hangoutGhosts.TryGetValue(s.Type, out e))
                {
                    var d = Defs.Get(s.Type);
                    hangoutGhosts[s.Type] = e = new Enemy { Id = 700 + (int)s.Type, Type = s.Type, Def = d, Radius = d.Radius, Hp = 1f, MaxHp = 1f };
                }
                e.Disp = c + new V2(0f, Mathf.Sin(t * 2f + i) * 2f);
                e.Facing = s.Inward.Angle + (s.On ? Mathf.Sin(t * 1.3f + i) * 0.4f : 0f);
                float keep = glAlpha;
                glAlpha = keep * (s.On ? 0.7f : 0.4f);
                menuPreview = true;
                DrawEnemy(e, s.On ? t : 0f);
                menuPreview = false;
                glAlpha = keep;
            }
        }

        /// HUD: over the spawner you stand on, its enemy and the keys.
        void DrawHangoutSpawnerTags()
        {
            var me = game.Me;
            if (!game.HangoutOn || me == null)
                return;
            int i = game.SpawnerNear(me.Pos);
            if (i < 0)
                return;
            var s = game.Spawners[i];
            V2 at = ToUi(s.Pos + s.Inward * 78f);  // (on the room side of the pad)
            var ink = (s.On ? SpawnerRed : HangoutPadOff).ToColor();
            var box = new Rect(at.X - 110f, at.Y - 18f, 220f, 36f);
            Ui.Pill(box, new Color(0f, 0f, 0f, 0.7f));
            Ui.Label(new Rect(box.x, box.y + 3f, box.width, 15f), PracticeEnemyName(s.Type).ToUpperInvariant() + (s.On ? "  ·  ON" : "  ·  OFF"), 11, ink, TextAnchor.MiddleCenter, true);
            string use = Keybinds.KeyName(Keybinds.Get(KeyAction.Interact)).ToUpperInvariant();
            string swap = Keybinds.KeyName(Keybinds.Get(KeyAction.DiscardItem)).ToUpperInvariant();
            Ui.Label(new Rect(box.x, box.y + 18f, box.width, 14f), use + (s.On ? "  TURN OFF" : "  TURN ON") + "   ·   " + swap + " / WHEEL  CHANGE", 9, CrtStyle.Desc, TextAnchor.MiddleCenter, true);
        }
    }
}

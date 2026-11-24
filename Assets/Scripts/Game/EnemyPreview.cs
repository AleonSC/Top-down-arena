using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// One enemy drawn live in a UI box (the dev tools' enemy list, the pause menu, path cards, the Game Mode page).
    public partial class ArenaApp
    {
        readonly Dictionary<EnemyType, Enemy> previewEnemies = new Dictionary<EnemyType, Enemy>();

        /// One enemy, drawn as in a run and fitted into a box `size` across (call between BeginUiGl and EndUiGl).
        void DrawEnemyPreview(EnemyType type, V2 center, float size, float t, bool lively)
        {
            Enemy e;
            if (!previewEnemies.TryGetValue(type, out e))
            {
                var def = Defs.Get(type);
                e = new Enemy { Id = 900 + (int)type, Type = type, Def = def, Radius = def.Radius, Hp = 1f, MaxHp = 1f };
                previewEnemies[type] = e;
            }
            float r = e.Radius;
            float extent = type == EnemyType.Singularity ? r * 1.65f : e.Def.IsBoss ? r + 16f : r + (e.Def.Melee ? 7f : 4f);
            float zoom = Mathf.Min(1.25f, size * 0.5f / extent);
            float speed = lively ? 1f : 0.35f;
            e.Facing = t * 0.9f * speed + e.Id;
            e.Phase = (byte)(type == EnemyType.Spinner && lively && t % 3.3f < 1.3f ? 1 : 0);
            V2 bob = new V2(0f, (lively ? 2.5f : 1f) * Mathf.Sin(t * 2.2f + e.Id));
            if (type == EnemyType.ReaperOfDusk)  // the pair, side by side
            {
                Enemy b;
                if (!previewEnemies.TryGetValue(EnemyType.ReaperOfDawn, out b))
                {
                    var def = Defs.Get(EnemyType.ReaperOfDawn);
                    b = new Enemy { Id = 950, Type = EnemyType.ReaperOfDawn, Def = def, Radius = def.Radius, Hp = 1f, MaxHp = 1f };
                    previewEnemies[EnemyType.ReaperOfDawn] = b;
                }
                zoom *= 0.62f;
                b.Facing = -e.Facing;
                UiModelAt(center, zoom);
                e.Disp = new V2(-r * 1.05f, 0f) + bob;
                b.Disp = new V2(r * 1.05f, 0f) - bob;
                Line(e.Disp, b.Disp, 2f, new Rgba(0.9f, 0.85f, 1f, 0.35f + 0.2f * Mathf.Sin(t * 5f)));  // their tether
                DrawEnemy(e, t * speed);
                DrawEnemy(b, t * speed);
                return;
            }
            UiModelAt(center, zoom);
            e.Disp = bob;
            menuPreview = true;  // e.g. a Shade doesn't flicker out of sight in its tile
            DrawEnemy(e, t * speed);
            menuPreview = false;
        }
    }
}

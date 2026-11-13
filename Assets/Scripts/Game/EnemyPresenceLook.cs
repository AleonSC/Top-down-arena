using UnityEngine;

namespace Arena
{
    /// Regular enemies stand out from the floor with Visual effects on (look D of Docs/Mockups/enemy-prominence.html): a soft dark
    /// shadow under each body (DrawEnemy) and a soft glow of its own colour round it (the glow pass), much quieter than a
    /// player's halo. The wind-up (VarietyLook.cs) is unchanged. Bosses (they have their own glow), spawning enemies and
    /// zombies are left alone. Front end only.
    public partial class ArenaApp
    {
        const float GlowAlpha = 0.12f, GlowRadius = 2.6f;  // a player's halo is 0.3 at 5.2x: this is far quieter

        /// Does this enemy get the shadow and the soft glow?
        bool HasPresence(Enemy e)
        {
            return Juicy && !e.Def.IsBoss && !game.IsSpawning(e) && !Zombies.Is(e.Type) && e.Hp > 0f;
        }

        /// A shadow's sides at most: a 12-sided one is within half a pixel of a circle at an enemy's size.
        const int ShadowSides = 12;

        /// DrawEnemy, behind the body: a dark contact shadow, offset down and right.
        void DrawEnemyShadow(Enemy e, V2 pos, float r)
        {
            if (!HasPresence(e))
                return;
            int sides = Mathf.Min(e.Def.Sides, ShadowSides);  // a round body's 24 sides would cost 72 triangles a shadow
            Polygon(pos + new V2(2.5f, 4f), r * 1.05f, sides, EnemyRotation(e, game.AnimT), new Rgba(0f, 0f, 0f, 0.38f));
        }

        /// DrawGlowPass: a soft glow of its own colour round every regular enemy.
        void DrawEnemyPresenceGlow()
        {
            foreach (var e in game.Enemies.Values)
            {
                if (!HasPresence(e))
                    continue;
                GlowDisc(e.Disp, e.Radius * JuiceScale(e) * GlowRadius, e.Def.Color, GlowAlpha);
            }
        }
    }
}

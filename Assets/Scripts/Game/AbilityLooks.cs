using UnityEngine;

namespace Arena
{
    /// Telling what an ability just did: its name pops up over whoever cast it (FxKind.Callout, sent by HostAbility),
    /// in their role's colour, gold for an ultimate. On by default; Settings > Visual effects > Ability names.
    public partial class ArenaApp
    {
        bool fxCallouts = true;

        /// On the HUD (IMGUI), over the arena: each fresh cast's name rising off its caster and fading out. Two casts by
        /// the same player at once stack rather than overlap.
        void DrawAbilityCallouts()
        {
            if (!fxCallouts || game == null)
                return;
            for (int i = 0; i < game.Effects.Count; i++)
            {
                var fx = game.Effects[i];
                if (fx.Kind != FxKind.Callout)
                    continue;
                Player p;
                if (!game.Players.TryGetValue((int)fx.Pos2.X, out p) || LaunchHides(p))
                    continue;
                int id = (int)fx.Pos2.Y;
                if (id <= 0 || id >= Upgrades.Abilities.Length)
                    continue;
                var def = Upgrades.Ability((AbilityId)id);
                bool ult = def.Choice == AbilityDef.Ult;
                float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
                float pop = Mathf.Min(1f, fx.T * 10f), fade = f < 0.65f ? 1f : 1f - (f - 0.65f) / 0.35f;
                int stack = 0;  // a newer cast by the same player sits above an older one
                for (int k = i + 1; k < game.Effects.Count; k++)
                {
                    var o = game.Effects[k];
                    if (o.Kind == FxKind.Callout && (int)o.Pos2.X == p.Id)
                        stack++;
                }
                V2 at = p.Disp * WorldToUi;  // HUD coordinates; it starts just above their name tag (Hud.cs) and rises
                at.Y -= p.Def.BodyR * WorldToUi + 42f + 16f * f + 16f * stack;
                Color c = ult ? new Color(1f, 0.84f, 0.35f) : Color.Lerp(ToColor(p.Def.Color), Color.white, 0.45f);
                c.a = fade;
                int size = Mathf.RoundToInt((ult ? 15f : 12f) * (0.7f + 0.3f * pop));
                Ui.Shadowed(new Rect(at.X - 90f, at.Y - 10f, 180f, 20f), ult ? def.Name.ToUpperInvariant() : def.Name, size, c);
            }
        }
    }
}

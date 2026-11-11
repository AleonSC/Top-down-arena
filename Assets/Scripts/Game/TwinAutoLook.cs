using UnityEngine;

namespace Arena
{
    /// The Twin Rifle Auto's lock (Core/TwinAuto.cs): while Alt has both guns locked on an enemy, a red reticle turns round it and a thin
    /// tether runs from each muzzle to it. The guns, rounds and muzzle effects are the Twin Rifle's (DpsGunLooks.cs, TwinRifleFx.cs).
    public partial class ArenaApp
    {
        static readonly Rgba AutoLockCol = new Rgba(1f, 0.36f, 0.36f);

        void DrawTwinAutoLock(Player p, float t)
        {
            Enemy e;
            if (p.Weapon.Id != WeaponId.TwinRifleAuto || p.Downed || p.TwinLock == 0 || !game.Enemies.TryGetValue(p.TwinLock, out e))
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * 7f);
            for (int g = 0; g < 2; g++)
                Line(game.AutoMuzzle(p, p.Disp, g), e.Disp, 1.2f, AutoLockCol.WithAlpha(0.22f + 0.1f * pulse));
            float r = e.Radius + 12f + 2f * pulse, spin = t * 1.6f;
            Arc(e.Disp, r, 1.2f, AutoLockCol.WithAlpha(0.35f), 28);
            for (int i = 0; i < 4; i++)  // four brackets turning slowly round it
                Arc(e.Disp, r + 3f, 2.4f, AutoLockCol.WithAlpha(0.9f), 8, spin + i * MathX.Pi / 2f, MathX.Pi / 6f);
            Line(e.Disp + new V2(-4f, 0f), e.Disp + new V2(4f, 0f), 1.4f, AutoLockCol.WithAlpha(0.8f));
            Line(e.Disp + new V2(0f, -4f), e.Disp + new V2(0f, 4f), 1.4f, AutoLockCol.WithAlpha(0.8f));
        }
    }
}

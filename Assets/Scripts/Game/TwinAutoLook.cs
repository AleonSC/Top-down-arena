using UnityEngine;

namespace Arena
{
    /// The Twin Rifle Auto's lock (Core/TwinAuto.cs): while Alt has both guns locked on an enemy, a red reticle turns round it and a thin
    /// tether runs from each muzzle to it. The guns, rounds and muzzle effects are the Twin Rifle's (DpsGunLooks.cs, TwinRifleFx.cs).
    public partial class ArenaApp
    {
        static readonly Rgba AutoLockCol = new Rgba(1f, 0.36f, 0.36f);

        static readonly Rgba AutoLaserCol = new Rgba(0.35f, 0.9f, 1f), AutoLaserHit = new Rgba(1f, 0.62f, 0.3f);

        /// A thin laser out of each muzzle along its gun, as long as the gun reaches: it ends in a small cap where the range ends, or in a
        /// dot on the first enemy in the way (orange; red while that is the locked one).
        void DrawTwinAutoLasers(Player p, float t)
        {
            if (p.Weapon.Id != WeaponId.TwinRifleAuto || p.Downed)
                return;
            float range = game.AutoRange(p);
            for (int g = 0; g < 2; g++)
            {
                V2 o = game.AutoMuzzle(p, p.Disp, g), d = V2.FromAngle(game.AutoHeading(p, g));
                float len = range, best = range + 1f;
                Enemy hit = null;
                foreach (var e in game.Enemies.Values)
                {
                    if (game.IsSpawning(e))
                        continue;
                    V2 m = e.Disp - o;
                    float along = m.Dot(d), perp2 = m.Dot(m) - along * along, r = e.Radius;
                    if (along < 0f || along > range + r || perp2 > r * r)
                        continue;
                    float at = along - Mathf.Sqrt(Mathf.Max(0f, r * r - perp2));
                    if (at < best) { best = at; hit = e; }
                }
                bool on = hit != null && best <= range;
                if (on)
                    len = Mathf.Max(0f, best);
                var col = on ? (hit.Id == p.TwinLock ? AutoLockCol : AutoLaserHit) : AutoLaserCol;
                V2 end = o + d * len;
                Line(o, end, 1f, col.WithAlpha(on ? 0.5f : 0.3f));
                if (on)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 12f);
                    Circle(end, 2.4f + pulse, col.WithAlpha(0.95f), 8);
                    Circle(end, 5f, col.WithAlpha(0.25f), 10);
                }
                else
                {
                    V2 n = d.Orthogonal() * 4f;
                    Line(end - n, end + n, 1.4f, col.WithAlpha(0.55f));  // the range ends here
                }
            }
        }

        void DrawTwinAutoLock(Player p, float t)
        {
            Enemy e;
            DrawTwinAutoLasers(p, t);
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

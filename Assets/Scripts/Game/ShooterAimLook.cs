using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Aim lines for the regular enemies that shoot straight at you (Docs/EnemyReadability.md section 2). While a shooter
    /// winds up (Enemy.Winding, the 0.3 s before a volley) a thin dashed line shows where its shot will go, the same dashed
    /// line the Leader and the Beam Sentry use: Sniper, Shard, Medic, Splitter and Phantom one line along the shot, the
    /// Grunt its three-shot fan. The line firms up over the wind-up. Rings, spirals and lobs have their own marks
    /// (Burster, Spinner, Mortar), so they get none. A shooter's Facing is the aim. Front end only.
    public partial class ArenaApp
    {
        readonly Dictionary<int, float> aimSince = new Dictionary<int, float>();

        /// How far the line reaches, and the half-angle of its fan (0 = a single line); false if this type has no aim line.
        static bool AimLineInfo(EnemyType type, out float reach, out float fan)
        {
            fan = 0f;
            switch (type)
            {
                case EnemyType.Sniper: reach = 420f; return true;
                case EnemyType.Shard: reach = 300f; return true;
                case EnemyType.Medic: reach = 240f; return true;
                case EnemyType.Splitter: reach = 260f; return true;
                case EnemyType.Phantom: reach = 300f; return true;
                case EnemyType.Grunt: reach = 230f; fan = 0.22f; return true;
            }
            reach = 0f;
            return false;
        }

        /// DrawVarietyUnder (the foreach over enemies): the aim line, while the enemy winds up to fire.
        void DrawShooterAim(Enemy e, V2 pos, float t)
        {
            if (!e.Winding || !AimLineInfo(e.Type, out float reach, out float fan))
            {
                aimSince.Remove(e.Id);
                return;
            }
            if (!aimSince.TryGetValue(e.Id, out float since))
            {
                if (aimSince.Count > 128)
                    aimSince.Clear();  // dead enemies leave entries behind
                since = aimSince[e.Id] = t;
            }
            float p = MathX.Clamp01((t - since) / Game.WindUp);
            Rgba c = e.Def.Color.Mix(Rgba.White, 0.5f);
            float a = 0.25f + 0.45f * p;
            V2 from = pos + V2.FromAngle(e.Facing) * e.Radius;
            if (fan <= 0f)
                DashedLine(from, pos + V2.FromAngle(e.Facing) * reach, 1.5f, c.WithAlpha(a));
            else
                for (int k = -1; k <= 1; k++)
                    DashedLine(from, pos + V2.FromAngle(e.Facing + k * fan) * reach, 1.5f, c.WithAlpha(k == 0 ? a : a * 0.6f));
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The shooting line every enemy that shoots straight at you shares (Docs/EnemyReadability.md section 2). While a shooter
    /// winds up (Enemy.Winding, the beat before a volley) a slim lane in its colour fills from the muzzle out to where the
    /// shot will go, brighter toward the far end, and closes with a cap when the shot is due (DrawAimLane): Sniper, Shard,
    /// Medic, Splitter, Phantom, Taser Guard and Security Guard one lane along the shot, the Grunt its three-shot fan. The
    /// Leader and the Beam Sentry keep their own blinking marks, and rings, spirals and lobs have their own (Burster,
    /// Spinner, Mortar). A shooter's Facing is the aim. Front end only.
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
                case EnemyType.Phantom: case EnemyType.Shade: reach = 300f; return true;
                case EnemyType.Grunt: reach = 230f; fan = 0.22f; return true;
                case EnemyType.TaserGuard: reach = 420f; return true;
            }
            reach = 0f;
            return false;
        }

        /// The lane's colour: the enemy's own, lifted toward white so it reads on the dark floor.
        static Rgba AimTint(Enemy e) => e.Def.Color.Mix(Rgba.White, 0.45f);

        /// The shared shooting line: a lane 6 px wide from `from` along `dir` for `reach`, its faint floor and edges, the
        /// fill reaching out with the wind-up `p` (0-1, brighter toward its far end), and the end cap once it's full.
        /// `alpha` dims it (the Grunt's outer shots).
        void DrawAimLane(V2 from, V2 dir, float reach, float p, Rgba c, float alpha)
        {
            const float hw = 3f;
            V2 n = new V2(-dir.Y, dir.X) * hw, tip = from + dir * reach;
            float edgeA = (0.45f + 0.5f * p) * alpha;
            Quad(from + n, tip + n, tip - n, from - n, c.WithAlpha(0.1f * alpha));  // the floor
            Line(from, tip, 9f, c.WithAlpha(0.06f * alpha));  // a soft glow round it
            const int steps = 8;
            float fill = reach * SecEase(p);
            for (int i = 0; i < steps; i++)  // the fill, stronger toward its head
            {
                float a0 = (float)i / steps, a1 = (float)(i + 1) / steps;
                V2 q0 = from + dir * (fill * a0), q1 = from + dir * (fill * a1);
                Quad(q0 + n, q1 + n, q1 - n, q0 - n, c.WithAlpha((0.12f + 0.55f * a1) * (0.6f + 0.4f * p) * alpha));
            }
            Line(from + n, tip + n, 1.3f, c.WithAlpha(edgeA * 0.7f));  // the edges
            Line(from - n, tip - n, 1.3f, c.WithAlpha(edgeA * 0.7f));
            V2 head = from + dir * fill;
            Line(head + n * 1.6f, head - n * 1.6f, 1.6f, c.WithAlpha(edgeA));  // the fill's front
            if (p >= 0.98f)
                Line(tip + n * 1.6f, tip - n * 1.6f, 2f, c.WithAlpha(edgeA));  // full: the cap
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
            float p = MathX.Clamp01((t - since) / Game.WindUpOf(e.Type));
            Rgba c = AimTint(e);
            V2 from = pos + V2.FromAngle(e.Facing) * e.Radius;
            if (fan <= 0f)
                DrawAimLane(from, V2.FromAngle(e.Facing), reach - e.Radius, p, c, 1f);
            else
                for (int k = -1; k <= 1; k++)
                    DrawAimLane(from, V2.FromAngle(e.Facing + k * fan), reach - e.Radius, p, c, k == 0 ? 1f : 0.6f);
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Spawn portals (Core/SpawnPortals.cs): a vector vortex on the floor.
    ///   opening   a rune ring at full size with a thin ring growing inside it, and chevrons pointing the way the burst
    ///             will come out; violet, or red for a flank (it opens slower).
    ///   emitting  a dark core, three counter-turning arc rings, a pulsing rim and the chevrons.
    ///   closing   the whole thing shrinks shut.
    /// Flank portals also put a red arrow round your own player, pointing at the portal (DrawFlankArrows).
    /// Clients time the opening and closing from when they saw the phase change (the cargo and timers stay on the host).
    public partial class ArenaApp
    {
        static readonly Rgba PortalViolet = new Rgba(0.78f, 0.35f, 1f), PortalFlank = new Rgba(1f, 0.25f, 0.3f), PortalCore = new Rgba(0.05f, 0.02f, 0.09f);

        struct PortalSeen { public byte Phase; public float Since; }
        readonly Dictionary<int, PortalSeen> portalSeen = new Dictionary<int, PortalSeen>();

        /// Seconds since this portal entered its current phase, on this machine.
        float PortalAge(Enemy e)
        {
            float now = Time.time;
            PortalSeen s;
            if (!portalSeen.TryGetValue(e.Id, out s) || s.Phase != e.Phase)
            {
                if (portalSeen.Count > 64)
                    portalSeen.Clear();
                s = new PortalSeen { Phase = e.Phase, Since = now };
                portalSeen[e.Id] = s;
            }
            return now - s.Since;
        }

        void DrawSpawnPortal(Enemy e, V2 pos, float r, bool flash, bool spawning, float t)
        {
            float age = PortalAge(e);
            bool flank = SpawnPortals.IsFlank(e.Phase);
            var col = flank ? PortalFlank : PortalViolet;
            float open = SpawnPortals.IsOpening(e.Phase) ? Mathf.Clamp01(age / (flank ? SpawnPortals.FlankOpenTime : SpawnPortals.OpenTime)) : 1f;
            float size = e.Phase == SpawnPortals.Closing ? 1f - Mathf.Clamp01(age / SpawnPortals.CloseTime) : 1f;
            if (spawning)
                size *= 0.4f;
            float R = r * size;
            if (R < 0.5f)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(t * (flank ? 10f : 6f) + e.Id);
            V2 fwd = V2.FromAngle(e.Facing);

            // The floor rune ring at full size and its tick marks: where it will be.
            Arc(pos, R + 10f, 1.5f, col.WithAlpha(0.35f + 0.25f * pulse), 48);
            for (int i = 0; i < 12; i++)
            {
                float a = MathX.Tau * i / 12f - t * 0.4f;
                Line(pos + V2.FromAngle(a) * (R + 6f), pos + V2.FromAngle(a) * (R + 14f), 2f, col.WithAlpha(0.5f));
            }
            GlowDisc(pos, R * 2.4f, col, (0.12f + 0.12f * pulse) * (0.4f + 0.6f * open));

            // The core, growing as it opens.
            float core = R * (0.25f + 0.75f * open);
            Circle(pos, core * 1.08f, col.WithAlpha(0.35f), 32);
            Circle(pos, core, PortalCore.WithAlpha(0.92f), 32);
            Circle(pos, core * 0.35f, col.WithAlpha(0.25f + 0.2f * pulse), 20);  // the eye of the vortex
            var ring = flash ? Rgba.White : col;
            for (int k = 0; k < 3; k++)
            {
                float rr = core * (1f - k * 0.28f), spin = (k % 2 == 0 ? 1f : -1.4f) * t * (2f + k);
                for (int j = 0; j < 3; j++)
                    Arc(pos, rr, 2.5f - k * 0.5f, ring.WithAlpha(0.9f - k * 0.2f), 10, spin + MathX.Tau * j / 3f, MathX.Tau / 5f);
            }
            Arc(pos, core, 2f, ring.WithAlpha(0.6f + 0.4f * pulse), 40);

            // Chevrons out the front: the way the burst comes in.
            if (e.Phase != SpawnPortals.Closing)
                for (int k = 0; k < 3; k++)
                {
                    float d = R + 16f + k * 12f + 6f * ((t * 2f) % 1f);
                    V2 tip = pos + fwd * (d + 7f), side = fwd.Orthogonal() * 8f, back = pos + fwd * d;
                    var c = col.WithAlpha((0.75f - k * 0.22f) * (0.5f + 0.5f * open));
                    Line(back + side, tip, 2.5f, c);
                    Line(back - side, tip, 2.5f, c);
                }

            if (e.Hp < e.MaxHp && !spawning && e.Phase != SpawnPortals.Closing)
            {
                float w = Mathf.Max(30f, r * 2f);
                GhostBar(pos + new V2(-w / 2f, -r - 18f), w, 4f, e.Hp / e.MaxHp, new Rgba(1f, 0.3f, 0.3f), EnemyKey(e.Id));  // HealthBars.cs
            }
        }

        /// A red arrow round your player for each flank portal, pointing at it (drawn after the players).
        void DrawFlankArrows(float t)
        {
            Player me;
            if (!game.Players.TryGetValue(game.MyId, out me) || me.Downed)
                return;
            foreach (var e in game.Enemies.Values)
            {
                if (e.Type != EnemyType.Portal || !SpawnPortals.IsFlank(e.Phase) || e.Hp <= 0f)
                    continue;
                V2 to = e.Disp - me.Disp;
                float dist = to.Length;
                if (dist < 1f)
                    continue;
                V2 dir = to / dist, side = dir.Orthogonal();
                float pulse = 0.55f + 0.45f * Mathf.Sin(t * 12f);
                float near = me.Def.BodyR + 40f + 6f * pulse;
                V2 tip = me.Disp + dir * (near + 22f), b = me.Disp + dir * near;
                Arc(me.Disp, near - 6f, 3f, PortalFlank.WithAlpha(0.5f * pulse), 12, dir.Angle - 0.5f, 1f);  // a red arc on that side
                SetColor(PortalFlank.WithAlpha(0.75f * pulse + 0.25f));
                Tri(tip, b + side * 14f, b - side * 14f);
                Line(b + side * 14f, tip, 2f, Rgba.White.WithAlpha(0.7f * pulse));
                Line(b - side * 14f, tip, 2f, Rgba.White.WithAlpha(0.7f * pulse));
            }
        }
    }
}

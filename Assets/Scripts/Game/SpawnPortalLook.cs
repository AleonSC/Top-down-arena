using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Spawn portals (Core/SpawnPortals.cs): a digital "loading" gate on the floor.
    ///   opening   a 24-segment ring fills like a progress bar (the open timer), the core square fills from the bottom,
    ///             bits stream in along 8 spokes over a round floor grid; violet, or red for a flank (it opens slower).
    ///   emitting  the lit segments chase round the ring, the core blinks like a cursor, chevrons show the burst's way.
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

            var hot = col.Mix(Rgba.White, 0.55f);
            var edge = flash ? Rgba.White : col;

            // LOADING: a segmented ring that fills like a progress bar while it opens, then cycles; bits fly in
            // along the ring's spokes; a materialise grid on the floor.
            float B = R * 1.25f;
            GlowDisc(pos, R * 2f, col, 0.08f + 0.08f * pulse);
            // floor grid inside
            for (int k = -4; k <= 4; k++)
            {
                float o = k * R * 0.25f, w = Mathf.Sqrt(Mathf.Max(0f, 1f - (k / 4.5f) * (k / 4.5f))) * R;
                Line(pos + new V2(-w, o), pos + new V2(w, o), 1f, col.WithAlpha(0.22f));
                Line(pos + new V2(o, -w), pos + new V2(o, w), 1f, col.WithAlpha(0.22f));
            }
            const int S = 24;
            bool opening = open < 1f;
            for (int i = 0; i < S; i++)
            {
                float a0 = -MathX.Tau / 4f + MathX.Tau * i / S, f = i / (float)S;
                bool lit = opening ? f < open : ((i - (int)(t * 18f)) % S + S) % S < 8;
                float al = opening ? 1f : 1f - (((i - (int)(t * 18f)) % S + S) % S) / 8f;
                Arc(pos, B, lit ? 5f : 3f, (lit ? edge : col).WithAlpha(lit ? 0.35f + 0.65f * al : 0.18f), 3, a0 + 0.03f, MathX.Tau / S - 0.07f);
            }
            Arc(pos, B + 8f, 1f, col.WithAlpha(0.5f), 48);
            Arc(pos, B - 7f, 1f, col.WithAlpha(0.35f), 48, -t, MathX.Tau * 0.75f);
            // bits streaming in
            for (int k = 0; k < 12; k++)
            {
                float ph = (t * 1.2f + PHash(e.Id, k)) % 1f;
                float a = MathX.Tau * Mathf.Floor(PHash(e.Id, k + 50) * 8f) / 8f;
                V2 p = pos + V2.FromAngle(a) * Mathf.Lerp(B * 1.6f, R * 0.25f, ph);
                Box(p, 2f, hot.WithAlpha(Mathf.Sin(ph * Mathf.PI) * 0.9f));
            }
            // the core: a square that fills, then a blinking cursor block
            float core = R * 0.45f;
            Box(pos, core, PortalCore.WithAlpha(0.92f));
            BoxEdge(pos, core, 2f, edge);
            if (opening) Quad(pos + new V2(-core, core), pos + new V2(core, core), pos + new V2(core, core - 2f * core * open), pos + new V2(-core, core - 2f * core * open), col.WithAlpha(0.6f));
            else if ((int)(t * 4f) % 2 == 0) Box(pos, core * 0.45f, hot);

            // Chevrons out the front: the way the burst comes in.
            if (e.Phase != SpawnPortals.Closing)
                for (int k = 0; k < 3; k++)
                {
                    float d = B + 14f + k * 12f + 6f * ((t * 2f) % 1f);
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

        static float PHash(int id, int k) { uint h = (uint)(id * 73856093 ^ k * 19349663); h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15; return (h & 0xffff) / 65535f; }

        void Box(V2 c, float h, Rgba col) { Quad(c + new V2(-h, -h), c + new V2(h, -h), c + new V2(h, h), c + new V2(-h, h), col); }

        void BoxEdge(V2 c, float h, float w, Rgba col)
        {
            V2 a = c + new V2(-h, -h), b = c + new V2(h, -h), d = c + new V2(h, h), e = c + new V2(-h, h);
            Line(a, b, w, col); Line(b, d, w, col); Line(d, e, w, col); Line(e, a, w, col);
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

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Movement feel: what moving looks like, on this screen only (nothing sent, the game untouched). From each player's
    /// drawn position every frame (TickMoveFeel, from UpdateJuice, so Visual effects off turns it all off):
    ///   stretch  the body stretches along where it's heading (up to FeelStretchWalk at walking speed, FeelStretchDash in a
    ///            dash) and leans FeelLeanPx ahead of its footprint; stopping or landing a dash squashes it, and a spring
    ///            lets it wobble back (FeelSpringK, FeelSpringDamp). Applied to the model's vertices in Px (WorldRenderer.cs).
    ///   dust     a puff off each foot as you walk (the footsteps' stride, Footsteps.cs), a burst behind a dash as it starts,
    ///            and a skid puff when you turn hard at speed.
    ///   ghosts   a dash leaves FeelGhostMax fading copies of the model behind it (DrawMoveGhosts).
    ///   camera   your own dash kicks the camera lean (CameraLean.cs) FeelCamKick ahead, and it eases back.
    /// A dash is any burst over FeelDashSpeed (the Dodge, the Soldier's Dash, a Leap...), so teammates' dashes show too.
    /// Reduced effects keeps the dust and ghosts but leaves the body and the camera still.
    public partial class ArenaApp
    {
        const float FeelStretchWalk = 0.14f, FeelStretchDash = 0.32f, FeelSquashStop = -3.2f, FeelSquashLand = -4.6f;
        const float FeelSpringK = 520f, FeelSpringDamp = 19f, FeelLeanPx = 3.5f, FeelWalkRef = 280f;
        const float FeelDashSpeed = 620f, FeelJump = 120f, FeelGhostGap = 0.03f, FeelGhostLife = 0.24f, FeelCamKick = 9f;
        const int FeelGhostMax = 5;

        struct FeelGhost { public V2 Pos; public float T; public float Aim; }
        class FeelState
        {
            public V2 Last, Dir = new V2(1f, 0f);
            public bool Seen, Dashing;
            public float Speed, S, SV, StepPhase = 0.75f, SkidCd, GhostCd;
            public int Foot = 1;
            public readonly FeelGhost[] Ghosts = new FeelGhost[FeelGhostMax];
            public int GhostN;
        }
        readonly Dictionary<int, FeelState> feel = new Dictionary<int, FeelState>();

        // The body transform Px applies while a model is drawn (DrawPlayer): about BodyC, scaled by BodyAlong along BodyAx and
        // BodyAcross across it, then moved by BodyOff.
        bool bodyXfOn;
        V2 bodyC, bodyAx, bodyOff;
        float bodyAlong = 1f, bodyAcross = 1f;

        void ClearMoveFeel() => feel.Clear();

        /// Each frame from UpdateJuice (dt already 0 while paused or held).
        void TickMoveFeel(float dt)
        {
            if (dt <= 0f)
                return;
            var me = game.Me;
            foreach (var p in game.Players.Values)
            {
                FeelState s;
                if (!feel.TryGetValue(p.Id, out s))
                    feel[p.Id] = s = new FeelState();
                V2 d = p.Disp - s.Last;
                bool jump = !s.Seen || d.Length > FeelJump;  // a wrap round the edge, a blink or a respawn: not movement
                s.Last = p.Disp;
                s.Seen = true;
                float raw = jump || p.Downed ? 0f : d.Length / dt;
                V2 rawDir = raw > 1f ? d / d.Length : V2.Zero;
                bool dashing = raw > FeelDashSpeed;
                float before = s.Speed;
                s.Speed += (raw - s.Speed) * (1f - Mathf.Exp(-dt * (dashing ? 40f : 18f)));  // (smooths the snapshots' uneven steps)
                s.SkidCd -= dt;
                s.GhostCd -= dt;

                // Turning hard at speed: a skid puff where your feet were.
                if (!dashing && raw > 150f && before > 150f && rawDir.Dot(s.Dir) < -0.2f && s.SkidCd <= 0f)
                {
                    s.SkidCd = 0.18f;
                    FeelDust(p, s.Dir, 3, 7f, 70f, 0.4f);
                }
                if (raw > 30f)
                    s.Dir += (rawDir - s.Dir) * Mathf.Min(1f, dt * (dashing ? 40f : 14f));
                if (s.Dir.LengthSq < 0.0001f)
                    s.Dir = rawDir.IsZero ? new V2(1f, 0f) : rawDir;
                s.Dir = s.Dir.Normalized();

                // A dash: a burst of dust as it starts, copies left behind, a squash as it lands, your camera kicked ahead.
                if (dashing && !s.Dashing)
                {
                    FeelDust(p, rawDir, 6, 8f, 130f, 0.45f);
                    s.GhostCd = 0f;
                    if (p == me && camLean && !FxCalm)
                        lean += LeanTurn(rawDir * FeelCamKick);
                }
                else if (!dashing && s.Dashing)
                    s.SV += FeelSquashLand;
                s.Dashing = dashing;
                if (dashing && s.GhostCd <= 0f)
                {
                    s.GhostCd = FeelGhostGap;
                    for (int i = FeelGhostMax - 1; i > 0; i--)
                        s.Ghosts[i] = s.Ghosts[i - 1];
                    s.Ghosts[0] = new FeelGhost { Pos = p.Disp, T = juiceClock, Aim = p.Aim };
                    s.GhostN = Mathf.Min(FeelGhostMax, s.GhostN + 1);
                }

                // Stopping from a run squashes the body.
                if (!dashing && before > 160f && s.Speed < 60f && raw < 20f)
                    s.SV += FeelSquashStop * Mathf.Clamp01(before / FeelWalkRef);

                // Footfalls: a little dust off alternate feet, at the footsteps' stride.
                if (raw > 110f && !dashing)
                {
                    s.StepPhase += dt * s.Speed / (40f + s.Speed * 0.08f);
                    if (s.StepPhase >= 1f)
                    {
                        s.StepPhase -= 1f;
                        s.Foot = -s.Foot;
                        V2 side = s.Dir.Orthogonal() * (s.Foot * p.Def.BodyR * 0.4f);
                        AddPart(new Part { Kind = PDust, Pos = p.Disp - s.Dir * (p.Def.BodyR * 0.55f) + side, Vel = -s.Dir * 22f + side * 0.6f,
                            Life = 0.38f, Max = 0.38f, Size = 3.6f + s.Speed * 0.006f, Col = DustColor });
                    }
                }
                else if (raw < 30f)
                    s.StepPhase = 0.75f;  // (the first puff comes quickly when you set off)

                // The spring: the stretch eases toward what the speed asks for, kicked by stops and landings.
                float want = s.Dashing ? FeelStretchDash : FeelStretchWalk * Mathf.Clamp01(s.Speed / FeelWalkRef);
                float st = Mathf.Min(dt, 1f / 60f), left = dt;
                while (left > 0f)  // (small steps: the spring is stiff)
                {
                    float h = Mathf.Min(st, left);
                    s.SV += (-FeelSpringK * (s.S - want) - FeelSpringDamp * s.SV) * h;
                    s.S = Mathf.Clamp(s.S + s.SV * h, -0.22f, 0.36f);
                    left -= h;
                }
            }
        }

        /// A few puffs behind you, fanned out: `dir` is where you're heading.
        void FeelDust(Player p, V2 dir, int n, float size, float speed, float life)
        {
            V2 back = -dir, side = dir.Orthogonal();
            for (int i = 0; i < n; i++)
            {
                float u = n == 1 ? 0f : (i / (n - 1f)) * 2f - 1f;
                V2 v = (back + side * (u * 0.9f)).Normalized() * (speed * Rnd(0.6f, 1f));
                AddPart(new Part { Kind = PDust, Pos = p.Disp + back * (p.Def.BodyR * 0.5f) + side * (u * p.Def.BodyR * 0.5f), Vel = v,
                    Life = life * Rnd(0.8f, 1.1f), Max = life, Size = size * Rnd(0.75f, 1.15f), Col = DustColor });
            }
        }

        /// DrawPlayer, round the model: stretch and lean it (no-op with effects calm, in a menu preview or downed).
        void BeginBodyFeel(Player p, bool preview)
        {
            FeelState s;
            if (preview || FxCalm || p.Downed || !feel.TryGetValue(p.Id, out s) || Mathf.Abs(s.S) < 0.004f && s.Speed < 20f)
                return;
            bodyXfOn = true;
            bodyC = p.Disp;
            bodyAx = s.Dir;
            bodyAlong = 1f + s.S;
            bodyAcross = 1f - s.S * 0.6f;
            bodyOff = s.Dir * (FeelLeanPx * Mathf.Clamp01(s.Speed / FeelWalkRef));
        }

        void EndBodyFeel() => bodyXfOn = false;

        /// Px: a point of the model, stretched and leaned.
        V2 BodyFeelPoint(V2 q)
        {
            V2 d = q - bodyC;
            float along = d.Dot(bodyAx);
            V2 across = d - bodyAx * along;
            return bodyC + bodyAx * (along * bodyAlong) + across * bodyAcross + bodyOff;
        }

        /// Under a player: the fading copies a dash left behind.
        void DrawMoveGhosts(Player p, float t)
        {
            FeelState s;
            if (!fxOn || p.Downed || !feel.TryGetValue(p.Id, out s) || s.GhostN == 0)
                return;
            V2 keepAt = p.Disp;
            float keepAim = p.Aim;
            Rgba col = p.Def.Color;
            for (int i = s.GhostN - 1; i >= 0; i--)
            {
                var g = s.Ghosts[i];
                float f = 1f - (juiceClock - g.T) / FeelGhostLife;
                if (f <= 0f)
                {
                    s.GhostN = i;  // (older ones are gone too)
                    continue;
                }
                if (g.Pos.DistanceTo(keepAt) < p.Def.BodyR * 0.6f)
                    continue;  // (still under you)
                p.Disp = g.Pos;
                p.Aim = g.Aim;
                DrawPlayerModel(p, p.Def.BodyR * (0.92f + 0.08f * f), t, col.WithAlpha(0.42f * f * (1f - i * 0.12f)));
            }
            p.Disp = keepAt;
            p.Aim = keepAim;
        }
    }
}

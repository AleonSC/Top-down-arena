using System;
using System.Collections.Generic;

namespace Arena
{
    /// Shadow Clone as a wall (Docs/Mockups/shadow-clone-wall.html, look C): nine afterimages in a line 80 px ahead of the
    /// caster. They never fight. Enemies aim at them and their bodies eat enemy bullets (each soaks CloneWall HP, set by
    /// MinionDef.BlockCapacity); players shove them by walking into them, and the row keeps its shape, so one push moves it all.
    /// The look is Game/CloneWallLook.cs.
    public partial class Game
    {
        public const int CloneWallCount = 9;              // 4 clones either side of the middle one
        public const float CloneWallGap = 32f, CloneWallAhead = 80f;
        const float CloneWallSpring = 16f, CloneWallFriction = 6f, CloneWallMaxSlide = 170f;

        /// Raise the wall in front of the caster, across their aim. hpMult: mastery, Mirror Image and the capstone.
        void SpawnCloneWall(Player p, float life, float hpMult)
        {
            V2 fwd = V2.FromAngle(p.Aim), side = fwd.Orthogonal();
            int mid = CloneWallCount / 2;
            for (int i = 0; i < CloneWallCount; i++)
            {
                V2 offset = side * ((i - mid) * CloneWallGap);
                var m = SpawnMinion(MinionKind.CloneWall, p, p.Pos + fwd * CloneWallAhead + offset, life);
                m.Goal = offset;                  // where it sits in the row, relative to the row's middle
                m.BlockHp *= hpMult;
                m.MaxHp = m.BlockHp;
                m.Ang = Math.Abs(i - mid);        // how far from the middle (the look raises them centre-out)
            }
        }

        readonly List<Minion> wallBuf = new List<Minion>();

        /// Host, every tick: players shove the wall, the clones push each other and the row springs back into shape.
        void TickCloneWalls(float dt)
        {
            if (!IsHost || dt <= 0f)
                return;
            wallBuf.Clear();
            foreach (var m in clones)
                if (m.Kind == MinionKind.CloneWall && m.T != 0f)
                    wallBuf.Add(m);
            if (wallBuf.Count == 0)
                return;
            float damp = (float)Math.Exp(-CloneWallFriction * dt);
            foreach (var m in wallBuf)
            {
                V2 sum = V2.Zero;
                int n = 0;
                foreach (var o in wallBuf)
                    if (o.Owner == m.Owner)
                    {
                        sum += o.Pos - o.Goal;
                        n++;
                    }
                V2 anchor = sum / n;
                m.Vel += (anchor + m.Goal - m.Pos) * (CloneWallSpring * dt);
                m.Pos += m.Vel * dt;
                m.Vel = (m.Vel * damp).LimitLength(CloneWallMaxSlide);
            }
            float r = Kits.Get(MinionKind.CloneWall).Radius;
            for (int it = 0; it < 3; it++)
            {
                foreach (var m in wallBuf)
                    foreach (var p in Players.Values)
                    {
                        if (p.Downed)
                            continue;
                        V2 d = m.Pos - p.Pos;
                        float dist = d.Length, min = r + p.Def.BodyR;
                        if (dist >= min)
                            continue;
                        V2 nrm = dist > 0.01f ? d / dist : V2.FromAngle(p.Aim);
                        float pen = min - dist;
                        m.Pos += nrm * pen;
                        if (it == 0)
                            m.Vel = (m.Vel + nrm * (pen / dt * 0.25f)).LimitLength(CloneWallMaxSlide);
                    }
                for (int i = 0; i < wallBuf.Count; i++)
                    for (int j = i + 1; j < wallBuf.Count; j++)
                    {
                        var a = wallBuf[i];
                        var b = wallBuf[j];
                        V2 d = b.Pos - a.Pos;
                        float dist = d.Length;
                        if (dist >= r * 2f)
                            continue;
                        V2 nrm = dist > 0.01f ? d / dist : new V2(1f, 0f);
                        V2 half = nrm * ((r * 2f - dist) * 0.5f);
                        a.Pos -= half;
                        b.Pos += half;
                    }
                foreach (var m in wallBuf)
                    m.Pos = ClampToArena(m.Pos, r);
            }
        }
    }
}

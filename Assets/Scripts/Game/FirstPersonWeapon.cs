using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The first-person view's weapon (FirstPerson.cs): a small 3D model in camera space, low on the right, built from
    /// shaded boxes and drawn over the arena (FpEndWorld). Purely a look: the shots still come from the game.
    ///   shape    from your weapon (FpVmShapeOf): a gun (Soldier and Engineer weapons and anything that shoots rounds),
    ///            a sword (every melee weapon), or a staff with a glowing crystal (spells, bows, summons, thrown things)
    ///   colour   your role's colour on the accent strips, the guard and the crystal
    ///   motion   a walk bob from how far you moved this frame, a slow idle sway; holding Fire plays a kick every fire
    ///            interval (at least FpVmMinBeat): the gun recoils with a muzzle flash, the sword swings (right to left,
    ///            then back), the staff thrusts and its crystal flares
    ///   boxes    FpVmBox puts a box (local centre and half sizes) through the pose (roll about z, pitch about x, then
    ///            moved to fpVmAt); every visible face is shaded by its normal and drawn far to near
    public partial class ArenaApp
    {
        const float FpVmMinBeat = 0.12f;
        enum FpVmShape { Gun, Sword, Staff }

        float fpVmKick, fpVmKickLen = 0.2f, fpVmBeat, fpVmBob;
        int fpVmSwings;  // swings so far (the sword alternates its side)
        V2 fpVmLast;
        bool fpVmSeen;

        // The pose of the box being built.
        float fpVmRoll, fpVmPitch, fpVmYaw, fpVmSize = 1f;
        Vector3 fpVmAt;

        struct FpVmFace { public Vector3 A, B, C, D; public Color Col; public float Z; }
        readonly List<FpVmFace> fpVmFaces = new List<FpVmFace>(64);

        static FpVmShape FpVmShapeOf(WeaponDef w)
        {
            if (w == null)
                return FpVmShape.Gun;
            if (w.Melee)
                return FpVmShape.Sword;
            if (w.Kit == Kit.Soldier || w.Kit == Kit.Engineer)
                return FpVmShape.Gun;
            string n = w.Id.ToString();
            foreach (var g in new[] { "Pistol", "Gun", "Rifle", "Cannon", "Smg", "Repeater", "Launcher", "Railgun", "Revolver", "Barrel", "Darts", "Ammo" })
                if (n.Contains(g))
                    return FpVmShape.Gun;
            return FpVmShape.Staff;
        }

        /// FpEndWorld, after the arena: your weapon in front of the camera.
        void FpDrawWeapon()
        {
            var me = game.Me;
            if (me == null || me.Downed || me.Weapon == null)
            {
                fpVmSeen = false;
                return;
            }
            var shape = FpVmShapeOf(me.Weapon);
            float dt = Mathf.Min(0.05f, Time.deltaTime), t = Time.time;
            // The walk bob: how far you went this frame.
            float moved = fpVmSeen ? (me.Disp - fpVmLast).Length : 0f;
            fpVmSeen = true;
            fpVmLast = me.Disp;
            float speed = dt > 0f ? Mathf.Clamp01(moved / dt / 220f) : 0f;
            fpVmBob += dt * (6f + 6f * speed);
            // Fire held: a kick every fire interval.
            fpVmBeat -= dt;
            bool firing = Keybinds.Held(KeyAction.Fire) && !fireBlocked && !game.Paused;
            if (firing && fpVmBeat <= 0f)
            {
                float beat = Mathf.Max(FpVmMinBeat, me.Weapon.FireInterval);
                fpVmBeat = beat;
                fpVmKickLen = shape == FpVmShape.Sword ? Mathf.Clamp(beat * 0.9f, 0.18f, 0.4f) : Mathf.Clamp(beat * 0.8f, 0.08f, 0.22f);
                fpVmKick = 1f;
                fpVmSwings++;
            }
            fpVmKick = Mathf.Max(0f, fpVmKick - dt / fpVmKickLen);
            float k = fpVmKick, s = 1f - k;  // k: 1 at the kick, easing to 0; s: the swing's progress 0..1
            float bobX = Mathf.Cos(fpVmBob) * 0.35f * speed, bobY = Mathf.Abs(Mathf.Sin(fpVmBob)) * -0.5f * speed + Mathf.Sin(t * 1.7f) * 0.15f;
            var accent = me.Def.Color;
            var acc = new Color(accent.R, accent.G, accent.B, 1f);
            var hot = new Color(Mathf.Lerp(accent.R, 1f, 0.55f), Mathf.Lerp(accent.G, 1f, 0.55f), Mathf.Lerp(accent.B, 1f, 0.55f), 1f);
            fpVmFaces.Clear();
            Vector3 muzzle = new Vector3(0f, 0f, 0f);
            bool flash = false;
            if (shape == FpVmShape.Gun)
            {
                float rk = k * k;
                fpVmSize = 0.8f;
                fpVmAt = new Vector3(10.5f + bobX, -9.6f + bobY + rk * 0.6f, 17f - rk * 2.2f);
                fpVmRoll = 0.04f; fpVmPitch = -0.06f - rk * 0.22f; fpVmYaw = -0.1f;
                var body = new Color(0.15f, 0.16f, 0.21f, 1f);
                var metal = new Color(0.24f, 0.26f, 0.32f, 1f);
                FpVmBox(0f, 1.2f, 4f, 1.5f, 1.7f, 6f, body, true);       // the receiver
                FpVmBox(0f, 1.6f, 12.4f, 0.75f, 0.75f, 3.2f, metal, true);  // the barrel
                FpVmBox(0f, 3.0f, 4f, 0.55f, 0.16f, 5f, acc, false);    // the lit strip on top
                FpVmBox(1.55f, 1.0f, 5f, 0.08f, 0.4f, 4.5f, acc, false);  // and down its side
                FpVmBox(0f, -2.2f, 0.5f, 1.1f, 2.2f, 1.3f, body, true);  // the grip
                FpVmBox(0f, -1.4f, 5.2f, 0.9f, 1.6f, 1.1f, metal, true);  // the magazine
                FpVmBox(0f, 1.6f, 15.75f, 0.95f, 0.95f, 0.2f, k > 0.5f ? hot : acc, false);  // the muzzle ring
                muzzle = FpVmPoint(0f, 1.6f, 16.6f);
                flash = k > 0.55f;
            }
            else if (shape == FpVmShape.Sword)
            {
                // At rest: leaning in, point up and ahead. A swing goes from the right to the left (every other one back).
                bool back = fpVmSwings % 2 == 0;
                float e = k > 0f ? s * s * (3f - 2f * s) : 1f;  // eased progress (1: done, at rest)
                float from = back ? 1.6f : -0.7f, to = back ? -0.7f : 1.6f;
                float swing = k > 0f ? Mathf.Sin(e * Mathf.PI) : 0f;
                fpVmRoll = k > 0f ? Mathf.Lerp(from, to, e) : 0.45f;
                fpVmPitch = 0.5f + swing * 0.7f;
                fpVmYaw = 0f;
                float x = k > 0f ? Mathf.Lerp(back ? 3f : 10f, back ? 10f : 3f, e) : 9f;
                fpVmSize = 0.85f;
                fpVmAt = new Vector3(x + 1.5f + bobX, -10.5f + bobY + swing * 2f, 18f + swing * 3f);
                var grip = new Color(0.2f, 0.16f, 0.14f, 1f);
                var steel = new Color(0.62f, 0.66f, 0.74f, 1f);
                FpVmBox(0f, -2.2f, 0f, 0.65f, 2.2f, 0.65f, grip, true);   // the grip
                FpVmBox(0f, -4.6f, 0f, 0.9f, 0.4f, 0.9f, acc, true);      // the pommel
                FpVmBox(0f, 0.4f, 0f, 3.2f, 0.5f, 0.9f, acc, true);       // the guard
                FpVmBox(0f, 8.9f, 0f, 1.15f, 8f, 0.22f, steel, true);     // the blade
                FpVmBox(0f, 17.6f, 0f, 0.6f, 0.8f, 0.18f, steel, true);   // its point
                FpVmBox(1.2f, 8.9f, 0f, 0.1f, 7.6f, 0.26f, k > 0f ? hot : acc, false);  // the lit edge
            }
            else
            {
                float th = Mathf.Sin(Mathf.Min(1f, s * 1.4f) * Mathf.PI) * (k > 0f ? 1f : 0f);
                fpVmSize = 0.95f;
                fpVmAt = new Vector3(10.5f + bobX, -12.5f + bobY, 18f + th * 3f);
                fpVmRoll = 0.25f; fpVmPitch = 0.35f + th * 0.35f; fpVmYaw = 0f;
                var wood = Color.Lerp(new Color(0.22f, 0.18f, 0.2f, 1f), acc, 0.15f);
                FpVmBox(0f, 4f, 0f, 0.55f, 9f, 0.55f, wood, true);        // the shaft
                FpVmBox(0f, 12.2f, 0f, 1.2f, 0.4f, 1.2f, acc, true);      // the collar
                FpVmBox(0f, -4.8f, 0f, 0.8f, 0.4f, 0.8f, acc, true);      // the butt cap
                float glow = 0.6f + 0.4f * Mathf.Sin(t * 3f) * 0.5f + k * 0.5f;
                var crystal = Color.Lerp(acc, Color.white, Mathf.Clamp01(glow - 0.5f));
                fpVmRoll += Mathf.Sin(t * 1.3f) * 0.02f;
                FpVmBox(0f, 14.7f, 0f, 1.5f, 2.1f, 1.5f, crystal, false); // the crystal
                muzzle = FpVmPoint(0f, 14.7f, 0f);
                flash = k > 0.3f;
            }
            // Far to near, then the flash over it all.
            fpVmFaces.Sort((a, b) => b.Z.CompareTo(a.Z));
            foreach (var f in fpVmFaces)
            {
                FpVmOut(f.A, f.Col); FpVmOut(f.B, f.Col); FpVmOut(f.C, f.Col);
                FpVmOut(f.A, f.Col); FpVmOut(f.C, f.Col); FpVmOut(f.D, f.Col);
            }
            if (flash && muzzle.z > FpNear)
            {
                GL.End();
                GlowMaterial()?.SetPass(0);
                GL.Begin(GL.TRIANGLES);
                float cx = fpCx + muzzle.x / muzzle.z * fpFocal, cy = fpCy + muzzle.y / muzzle.z * fpFocal;
                float r = fpFocal / muzzle.z * (shape == FpVmShape.Gun ? 3.2f : 3.8f) * (0.6f + k * 0.6f);
                var core = new Color(1f, 1f, 1f, 0.9f * k);
                var rim = new Color(hot.r, hot.g, hot.b, 0f);
                const int n = 10;
                for (int i = 0; i < n; i++)
                {
                    float a0 = MathX.Tau * i / n + fpVmSwings, a1 = MathX.Tau * (i + 1) / n + fpVmSwings;
                    float r0 = r * (i % 2 == 0 ? 1f : 0.55f), r1 = r * ((i + 1) % 2 == 0 ? 1f : 0.55f);
                    GL.Color(core); GL.Vertex(new Vector3(cx, cy, 0f));
                    GL.Color(rim); GL.Vertex(new Vector3(cx + Mathf.Cos(a0) * r0, cy + Mathf.Sin(a0) * r0, 0f));
                    GL.Color(rim); GL.Vertex(new Vector3(cx + Mathf.Cos(a1) * r1, cy + Mathf.Sin(a1) * r1, 0f));
                }
                GL.End();
                glMaterial?.SetPass(0);
                GL.Begin(GL.TRIANGLES);
            }
        }

        /// A model point through the pose, into camera space.
        Vector3 FpVmPoint(float x, float y, float z)
        {
            x *= fpVmSize; y *= fpVmSize; z *= fpVmSize;
            float cr = Mathf.Cos(fpVmRoll), sr = Mathf.Sin(fpVmRoll);
            float x1 = x * cr - y * sr, y1 = x * sr + y * cr;
            float cp = Mathf.Cos(fpVmPitch), sp = Mathf.Sin(fpVmPitch);
            float y2 = y1 * cp - z * sp, z2 = y1 * sp + z * cp;
            float cy = Mathf.Cos(fpVmYaw), sy = Mathf.Sin(fpVmYaw);
            float x3 = x1 * cy + z2 * sy, z3 = -x1 * sy + z2 * cy;
            return new Vector3(fpVmAt.x + x3, fpVmAt.y + y2, fpVmAt.z + z3);
        }

        /// A box (model centre and half sizes) through the pose: its faces that face the camera, shaded (`lit`) or flat.
        void FpVmBox(float cx, float cy, float cz, float hx, float hy, float hz, Color col, bool lit)
        {
            var p = new Vector3[8];
            for (int i = 0; i < 8; i++)
                p[i] = FpVmPoint(cx + ((i & 1) != 0 ? hx : -hx), cy + ((i & 2) != 0 ? hy : -hy), cz + ((i & 4) != 0 ? hz : -hz));
            FpVmQuad(p[0], p[2], p[3], p[1], col, lit);  // the back (-z)
            FpVmQuad(p[4], p[5], p[7], p[6], col, lit);  // the front (+z)
            FpVmQuad(p[0], p[1], p[5], p[4], col, lit);  // -y
            FpVmQuad(p[2], p[6], p[7], p[3], col, lit);  // +y
            FpVmQuad(p[0], p[4], p[6], p[2], col, lit);  // -x
            FpVmQuad(p[1], p[3], p[7], p[5], col, lit);  // +x
        }

        void FpVmQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col, bool lit)
        {
            // Its normal from two edges, turned to face the camera (at the origin): every face is drawn, far to near, so
            // the near ones cover the far ones (a box is small; no culling needed).
            float ux = b.x - a.x, uy = b.y - a.y, uz = b.z - a.z, vx = d.x - a.x, vy = d.y - a.y, vz = d.z - a.z;
            float nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            float mx = (a.x + c.x) * 0.5f, my = (a.y + c.y) * 0.5f, mz = (a.z + c.z) * 0.5f;
            float facing = nx * mx + ny * my + nz * mz;
            if (Mathf.Abs(facing) < 1e-6f)
                return;
            float len = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
            if (facing > 0f)
            {
                nx = -nx; ny = -ny; nz = -nz;
            }
            if (lit && len > 1e-6f)
            {
                // Light from the upper left, behind you; a little ambient.
                float l = (nx * -0.45f + ny * 0.75f + nz * -0.48f) / len;
                float shade = 0.45f + 0.55f * Mathf.Clamp01(l * 0.5f + 0.5f);
                col = new Color(col.r * shade, col.g * shade, col.b * shade, col.a);
            }
            fpVmFaces.Add(new FpVmFace { A = a, B = b, C = c, D = d, Col = col, Z = mz });
        }

        void FpVmOut(Vector3 v, Color c)
        {
            GL.Color(c);
            GL.Vertex(new Vector3(fpCx + v.x / v.z * fpFocal, fpCy + v.y / v.z * fpFocal, 0f));
        }
    }
}

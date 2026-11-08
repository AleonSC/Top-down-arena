using System;
using UnityEngine;

namespace Arena
{
    /// The Quarterstaff, held and swung (Docs/Mockups/quarterstaff-rework.html, the Beacon look). Drawing only, with Visual effects on.
    ///   - the model: both gloved hands on an iron shaft with steel bands and a spike behind, a lantern head with a crystal that
    ///     lights up as Jab stacks build, and one gold pip circling the head for each stack (DrawQuarterstaffModel, from
    ///     DrawMeleeModel; the held pose is in CharacterModels.cs);
    ///   - stacks also show as diamonds on an arc behind you, gold when filled (DrawStaffPips, from WorldRenderer.cs);
    ///   - the jab (a thrust effect whose Radius is the cone's negative half angle) is a pale gold cone with a bright rim.
    public partial class ArenaApp
    {
        static readonly Rgba StaffIron = new Rgba(0.29f, 0.32f, 0.38f), StaffIronDark = new Rgba(0.07f, 0.08f, 0.11f), StaffSteel = new Rgba(0.6f, 0.64f, 0.72f),
            StaffGold = new Rgba(1f, 0.83f, 0.42f), StaffGlove = new Rgba(0.49f, 0.35f, 0.18f), StaffGloveEdge = new Rgba(0.24f, 0.16f, 0.07f);
        public const float StaffLength = 158f;  // the shaft's length, rear tip to the lantern

        /// Jab stacks and their limit for a player (0 / the base limit when they aren't found).
        int StaffStacksOf(int owner, out int cap)
        {
            Player p;
            cap = Game.StaffBaseStacks;
            if (!game.Players.TryGetValue(owner, out p))
                return 0;
            cap = game.StaffCap(p);
            return Mathf.Min(cap, p.StaffStacks);
        }

        /// The staff from your body out along `ang` to its head at `tip`: the shaft slides through your hands for a jab.
        void DrawQuarterstaffModel(V2 o, float ang, float tip, float alpha, Rgba col, bool bright, float bend, Rgba arm, int owner)
        {
            V2 dir = V2.FromAngle(ang), side = dir.Orthogonal();
            int cap, n = StaffStacksOf(owner, out cap);
            float inten = cap > 0 ? (float)n / cap : 0f, t = Time.unscaledTime;
            Rgba armc = arm.A > 0f ? arm : new Rgba(0.8f, 0.72f, 0.4f);
            V2 head = o + dir * tip, rear = head - dir * StaffLength;
            V2 rearHand = o + dir * 14f, leadHand = o + dir * 46f, shL = o + side * 13f, shR = o - side * 13f;
            Line(shL, rearHand, 7.4f, StaffIronDark.WithAlpha(alpha));
            Line(shL, rearHand, 5.2f, armc.WithAlpha(alpha));
            Line(shR, leadHand, 7.4f, StaffIronDark.WithAlpha(alpha));
            Line(shR, leadHand, 5.2f, armc.WithAlpha(alpha));
            // the shaft, its bands and the spike behind
            Line(rear, head - dir * 8f, 7.4f, StaffIronDark.WithAlpha(alpha));
            Line(rear, head - dir * 8f, 5.4f, StaffIron.WithAlpha(alpha));
            Line(rear + side * 1.2f, head - dir * 14f + side * 1.2f, 1.2f, StaffSteel.WithAlpha(0.8f * alpha));
            for (int i = 0; i < 3; i++)
            {
                V2 b = head - dir * (26f + 46f * i);
                Line(b - dir * 1.6f, b + dir * 1.6f, 9f, StaffSteel.WithAlpha(alpha));
            }
            SetColor(new Rgba(0.73f, 0.76f, 0.83f, alpha));
            Tri(rear - dir * 10f, rear + side * 3.5f, rear - side * 3.5f);
            // the lantern head: a hexagon of steel round a crystal that glows with the stacks
            Polygon(head, 10.5f, 6, ang + 0.52f, StaffSteel.WithAlpha(alpha));
            Polygon(head, 8.3f, 6, ang + 0.52f, new Rgba(0.06f, 0.07f, 0.1f, alpha));
            Rgba glow = bright ? Rgba.White : StaffGold;
            float lit = Mathf.Max(inten, bright ? 1f : 0f);
            if (lit > 0f)
                Circle(head, 9f + 11f * lit, glow.WithAlpha((0.1f + 0.25f * lit) * alpha), 16);
            Polygon(head, 6.2f, 4, ang, (lit > 0f ? glow : new Rgba(0.42f, 0.46f, 0.55f)).WithAlpha((0.55f + 0.45f * lit) * alpha));
            // a pip circling the head for each stack, dim ones for the room left
            for (int i = 0; i < cap; i++)
            {
                float a = t * 1.4f + i * MathX.Tau / Mathf.Max(1, cap);
                V2 pc = head + V2.FromAngle(a) * 17f;
                if (i < n)
                {
                    Circle(pc, 6f, StaffGold.WithAlpha(0.25f * alpha), 10);
                    Circle(pc, 3f, StaffGold.WithAlpha(alpha), 8);
                }
                else
                    Circle(pc, 2.4f, new Rgba(1f, 1f, 1f, 0.16f * alpha), 8);
            }
            // gloves over the shaft
            Circle(rearHand, 5.8f, StaffGloveEdge.WithAlpha(alpha), 10);
            Circle(rearHand, 4.6f, StaffGlove.WithAlpha(alpha), 10);
            Circle(leadHand, 5.8f, StaffGloveEdge.WithAlpha(alpha), 10);
            Circle(leadHand, 4.6f, StaffGlove.WithAlpha(alpha), 10);
        }

        /// The Quarterstaff held at the ready in both gloved hands, drawn over the body in place of the model's own staff. It phases
        /// out while a swipe or jab draws the staff, and back in after (HeldPhase).
        void DrawQuarterstaffIdle(Player p, float t)
        {
            if (!fxOn || p.Weapon.Id != WeaponId.HolyMace || p.Downed)
                return;
            float phase = HeldPhase(p.Id);
            if (phase <= 0f)
                return;
            DrawQuarterstaffModel(p.Disp, p.Aim - 0.3f + 0.02f * Mathf.Sin(t * 2.6f), Game.MeleeReach(p, p.Weapon.Reach) * 0.85f, phase, StaffGold, false, 0f, ArmColor(p.Id), p.Id);
        }

        /// One diamond behind you for each stack the staff can store, gold when held.
        void DrawStaffPips(Player p)
        {
            if (!fxOn || p.Downed || p.StaffStacks == 0)
                return;
            int cap = game.StaffCap(p), n = Mathf.Min(cap, (int)p.StaffStacks);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            for (int d = 0; d < cap; d++)
            {
                float da = p.Aim + MathX.Pi + (d - (cap - 1) * 0.5f) * 0.34f;
                V2 c = p.Disp + V2.FromAngle(da) * 30f;
                bool on = d < n;
                if (on)
                    Circle(c, 7f, StaffGold.WithAlpha(0.12f + 0.08f * pulse), 10);
                Polygon(c, 4.6f, 4, 0f, on ? StaffGold : new Rgba(1f, 1f, 1f, 0.14f));
            }
        }

        /// The jab's cone: from your body out to its reach and half an angle either side, fading out as it ends. `light` is the additive pass.
        void StaffJabCone(V2 o, float aim, float half, float reach, float T, float thrustTime, float fade, Rgba col)
        {
            float grow = EaseOut3(Mathf.Clamp01(T / (thrustTime * 0.6f))), r = reach * (0.35f + 0.65f * grow);
            const int N = 10;
            for (int i = 0; i < N; i++)
            {
                float a0 = aim - half + 2f * half * i / N, a1 = aim - half + 2f * half * (i + 1) / N;
                V2 p0 = o + V2.FromAngle(a0) * r, p1 = o + V2.FromAngle(a1) * r;
                VTri(o + V2.FromAngle(aim) * 18f, col.WithAlpha(0.05f * fade), p0, col.WithAlpha(0.42f * fade), p1, col.WithAlpha(0.42f * fade));
            }
            Arc(o, r, 2.4f, Rgba.White.WithAlpha(0.85f * fade), 16, aim - half, 2f * half);
            Line(o + V2.FromAngle(aim - half) * 26f, o + V2.FromAngle(aim - half) * r, 1.6f, col.WithAlpha(0.6f * fade));
            Line(o + V2.FromAngle(aim + half) * 26f, o + V2.FromAngle(aim + half) * r, 1.6f, col.WithAlpha(0.6f * fade));
            Circle(o + V2.FromAngle(aim) * r, 8f + 10f * (1f - grow), Rgba.White.WithAlpha(0.5f * fade), 12);
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// Gadgets (Core/Gadgets.cs): the Magnet on the floor, in the air when thrown, in a player's hand and pulling; the Gadget
    /// key (tap: on / off, hold and let go: throw) and its HUD pill.
    public partial class ArenaApp
    {
        static readonly Rgba MagRed = new Rgba(0.92f, 0.2f, 0.22f), MagDark = new Rgba(0.45f, 0.06f, 0.08f), MagSteel = new Rgba(0.86f, 0.9f, 0.96f);
        /// Seconds the Gadget key must be held for a throw instead of a switch.
        const float GadgetHoldToThrow = 0.22f;
        float gadgetHold = -1f;  // how long the Gadget key has been held (-1 = not held)
        V2 gadgetAimAt;          // the arena spot under the cursor when the key went down (where a throw from the wheel lands)

        /// ArenaApp's input: tap the Gadget key to switch the gadget in hand on or off; hold it and let go to throw it where you aimed. With
        /// gadgets in the bag the hold opens the wheel instead (GadgetWheel.cs): a slice swaps it into your hand, the middle throws.
        void TickGadgetKey(Player me, bool free)
        {
            if (!free || me == null || me.Downed || (me.Gadget == GadgetId.None && BagCount(me) == 0))
            {
                gadgetHold = -1f;
                gadgetWheelOpen = false;
                gadgetWheelK = Mathf.MoveTowards(gadgetWheelK, 0f, Time.unscaledDeltaTime * 8f);
                return;
            }
            bool held = Keybinds.Held(KeyAction.Gadget);
            if (gadgetHold < 0f)
            {
                if (Keybinds.Down(KeyAction.Gadget))
                {
                    gadgetHold = 0f;
                    gadgetAimAt = MouseArena();
                }
                gadgetWheelK = Mathf.MoveTowards(gadgetWheelK, 0f, Time.unscaledDeltaTime * 8f);
                return;
            }
            if (held)
            {
                gadgetHold += Time.unscaledDeltaTime;
                if (!gadgetWheelOpen && gadgetHold >= GadgetHoldToThrow && BagCount(me) > 0)
                    OpenGadgetWheel(me);
                if (gadgetWheelOpen)
                    TickGadgetWheelHover();
                gadgetWheelK = Mathf.MoveTowards(gadgetWheelK, gadgetWheelOpen ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                return;
            }
            if (gadgetWheelOpen)
            {
                int slot = gadgetHover >= 0 ? gadgetWheelSlots[gadgetHover] : -2;
                if (slot >= 0)
                    game.EquipGadget(slot);
                else if (slot == -2 && me.Gadget != GadgetId.None)
                    game.ThrowGadget(gadgetAimAt);  // let go in the middle: throw what's in hand
            }
            else if (gadgetHold < GadgetHoldToThrow)
                game.UseGadget();
            else
                game.ThrowGadget(MouseArena());
            gadgetHold = -1f;
            gadgetWheelOpen = false;
        }

        bool AimingGadget => gadgetHold >= GadgetHoldToThrow && game.Me != null && game.Me.Gadget != GadgetId.None && (!gadgetWheelOpen || gadgetHover < 0);

        /// A horseshoe magnet: the red bend round c, its legs running toward ang, steel tips. s = the bend's radius.
        void DrawMagnetShape(V2 c, float s, float ang, float alpha, bool glow)
        {
            V2.Axes(ang, out V2 d, out V2 n);
            float w = s * 0.62f, leg = s * 1.15f, tip = s * 0.5f;
            if (glow)
                Circle(c + d * leg * 0.5f, s * 2.6f, MagRed.WithAlpha(0.12f * alpha), 16);
            Arc(c, s, w + 1.6f, MagDark.WithAlpha(alpha), 12, ang + Mathf.PI / 2f, Mathf.PI);  // outline
            for (int k = -1; k <= 1; k += 2)
                Line(c + n * (s * k) - d * 0.5f, c + n * (s * k) + d * (leg + 0.8f), w + 1.6f, MagDark.WithAlpha(alpha));
            Arc(c, s, w, MagRed.WithAlpha(alpha), 12, ang + Mathf.PI / 2f, Mathf.PI);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 a = c + n * (s * k);
                Line(a - d * 0.5f, a + d * (leg - tip), w, MagRed.WithAlpha(alpha));
                Line(a + d * (leg - tip), a + d * leg, w, MagSteel.WithAlpha(alpha));
            }
            Arc(c, s - w * 0.18f, w * 0.22f, new Rgba(1f, 0.6f, 0.6f, 0.55f * alpha), 10, ang + Mathf.PI * 0.75f, Mathf.PI * 0.4f);  // shine
        }

        static readonly Rgba GadgetCyan = new Rgba(0.45f, 0.9f, 1f), GadgetCyanDark = new Rgba(0.05f, 0.25f, 0.34f);

        /// A recall beacon: a ring round a house-like chevron pointing home. s = its radius.
        void DrawRecallShape(V2 c, float s, float alpha, float spin)
        {
            Circle(c, s * 1.9f, GadgetCyan.WithAlpha(0.12f * alpha), 16);
            Arc(c, s, 3.4f, GadgetCyanDark.WithAlpha(alpha), 20);
            Arc(c, s, 1.8f, GadgetCyan.WithAlpha(alpha), 20, spin, MathX.Tau * 0.8f);
            V2 top = c + new V2(0f, -s * 0.55f), l = c + new V2(-s * 0.5f, s * 0.05f), r = c + new V2(s * 0.5f, s * 0.05f);
            Line(l, top, 2.6f, GadgetCyanDark.WithAlpha(alpha));
            Line(top, r, 2.6f, GadgetCyanDark.WithAlpha(alpha));
            Line(l, top, 1.4f, GadgetCyan.WithAlpha(alpha));
            Line(top, r, 1.4f, GadgetCyan.WithAlpha(alpha));
            Line(c + new V2(-s * 0.3f, s * 0.05f), c + new V2(-s * 0.3f, s * 0.5f), 1.4f, GadgetCyan.WithAlpha(alpha));
            Line(c + new V2(s * 0.3f, s * 0.05f), c + new V2(s * 0.3f, s * 0.5f), 1.4f, GadgetCyan.WithAlpha(alpha));
            Line(c + new V2(-s * 0.3f, s * 0.5f), c + new V2(s * 0.3f, s * 0.5f), 1.4f, GadgetCyan.WithAlpha(alpha));
        }

        /// The gadgets on the floor (bobbing, a slow turn, a red ring) and in the air (an arc over its shadow).
        void DrawGadgets(float t)
        {
            foreach (var g in game.Gadgets.Values)
            {
                if (!game.MinePickup(g.Owner))
                    continue;
                bool recall = g.Kind == GadgetId.Recall;
                Rgba ring = recall ? GadgetCyan : MagRed;
                float lift = 0f, spin = t * 0.8f + g.Id;
                V2 ground = g.Disp;
                if (g.Flying)
                {
                    float f = g.FlyT / Game.GadgetFlyTime;
                    lift = Mathf.Sin(f * Mathf.PI) * Mathf.Min(60f, 14f + g.From.DistanceTo(g.To) * 0.18f);
                    spin = t * 0.8f + g.Id + f * Mathf.PI * 3f;
                    Circle(g.To, 14f, ring.WithAlpha(0.18f + 0.2f * f), 18);  // where it'll land
                }
                else
                {
                    lift = 3f + Mathf.Sin(t * 3f + g.Id) * 2f;
                    float pulse = Mathf.Repeat(t * 0.7f + g.Id * 0.3f, 1f);
                    Arc(ground, 18f + pulse * 18f, 1.6f, ring.WithAlpha(0.5f * (1f - pulse)), 20);
                }
                Circle(ground + new V2(0f, 7f), 10f - lift * 0.05f, new Rgba(0f, 0f, 0f, 0.3f), 10);  // shadow
                if (recall)
                    DrawRecallShape(ground - new V2(0f, lift + 3f), 11f, 1f, spin);
                else
                    DrawMagnetShape(ground - new V2(0f, lift + 3f), 8.5f, -Mathf.PI / 2f + Mathf.Sin(spin) * 0.5f, 1f, true);
                if (!recall && !g.Flying && g.Left < Game.GadgetTime(g.Kind) - 0.05f)  // part used: what's left as a thin arc
                    Arc(ground, 17f, 2f, MagSteel.WithAlpha(0.7f), 18, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(g.Left / Game.GadgetTime(g.Kind)));
            }
        }

        /// A player's held gadget beside them; switched on, field lines drawing in and a timer ring.
        void DrawHeldGadget(Player p, float t)
        {
            if (p.Gadget == GadgetId.None || p.Downed)
                return;
            float r = p.Def.BodyR;
            if (p.Gadget == GadgetId.Recall)
            {
                if (p.GadgetOn)  // the channel: rings rising off the feet and a progress arc
                {
                    float prog = 1f - Mathf.Clamp01(p.GadgetLeft / Game.RecallChannel);
                    for (int i = 0; i < 3; i++)
                    {
                        float f = Mathf.Repeat(t * 1.1f + i / 3f, 1f);
                        Arc(p.Disp + new V2(0f, -f * 34f), r + 4f + f * 6f, 1.8f, GadgetCyan.WithAlpha(0.5f * (1f - f)), 28);
                    }
                    Arc(p.Disp, r + 4f, 2.6f, GadgetCyan.WithAlpha(0.9f), 30, -Mathf.PI / 2f, MathX.Tau * prog);
                }
                DrawRecallShape(p.Disp + V2.FromAngle(p.Aim + 2.1f) * (r + 9f), 5.5f, 1f, t * 2f);
                return;
            }
            if (p.Gadget != GadgetId.Magnet)
                return;
            if (p.GadgetOn)
            {
                for (int i = 0; i < 3; i++)  // field rings closing in
                {
                    float f = Mathf.Repeat(t * 1.3f + i / 3f, 1f);
                    Arc(p.Disp, r + 6f + (1f - f) * 60f, 1.8f, MagRed.WithAlpha(0.45f * f), 28);
                }
                for (int i = 0; i < 6; i++)  // dashes streaming inward
                {
                    float a = i * MathX.Tau / 6f + t * 0.6f, f = Mathf.Repeat(t * 1.8f + i * 0.37f, 1f);
                    V2 dir = V2.FromAngle(a);
                    float d0 = r + 10f + (1f - f) * 70f;
                    Line(p.Disp + dir * d0, p.Disp + dir * (d0 - 9f), 2f, MagSteel.WithAlpha(0.5f * f));
                }
                Arc(p.Disp, r + 4f, 2.4f, MagRed.WithAlpha(0.85f), 30, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(p.GadgetLeft / Game.MagnetTime));
            }
            float side = p.Aim + 2.1f;
            V2 at = p.Disp + V2.FromAngle(side) * (r + 9f);
            DrawMagnetShape(at, 5.5f, p.GadgetOn ? p.Aim : -Mathf.PI / 2f, 1f, p.GadgetOn);
        }

        /// While the Gadget key is held long enough: the throw's arc and where it'll land.
        void DrawGadgetAim(float t)
        {
            if (!AimingGadget)
                return;
            var me = game.Me;
            V2 to = me.Disp + ((gadgetWheelOpen ? gadgetAimAt : MouseArena()) - me.Disp).LimitLength(Game.GadgetThrowRange);
            float len = me.Disp.DistanceTo(to), h = Mathf.Min(60f, 14f + len * 0.18f);
            int dots = Mathf.Max(4, (int)(len / 14f));
            for (int i = 1; i < dots; i++)
            {
                float f = i / (float)dots;
                V2 q = V2.Lerp(me.Disp, to, f) - new V2(0f, Mathf.Sin(f * Mathf.PI) * h);
                Circle(q, 1.8f, MagSteel.WithAlpha(0.55f + 0.3f * Mathf.Sin(t * 10f - i)), 6);
            }
            Arc(to, 14f, 2f, MagRed.WithAlpha(0.85f), 20);
            Arc(to, 14f + 4f * Mathf.Sin(t * 6f), 1f, MagRed.WithAlpha(0.4f), 20);
        }
    }
}

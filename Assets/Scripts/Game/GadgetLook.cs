using UnityEngine;

namespace Arena
{
    /// Gadgets (Core/Gadgets.cs): every gadget on the floor (shapes: GadgetShapes.cs), in the air when thrown, in a player's hand
    /// and switched on; the Gadget key (tap: on / off, hold and let go: throw), the Gadget wheel key, and the HUD pill.
    public partial class ArenaApp
    {
        static readonly Rgba MagRed = new Rgba(0.92f, 0.2f, 0.22f), MagDark = new Rgba(0.45f, 0.06f, 0.08f), MagSteel = new Rgba(0.86f, 0.9f, 0.96f);
        /// Seconds the Gadget key must be held for a throw instead of a switch.
        const float GadgetHoldToThrow = 0.22f;
        float gadgetHold = -1f;  // how long the Gadget key has been held (-1 = not held)
        V2 gadgetAimAt;          // the arena spot under the cursor when the key went down (where a throw from the wheel lands)
        bool gadgetWheelByKey;   // the wheel is open on the Gadget wheel key (its middle cancels instead of throwing)

        /// ArenaApp's input: tap the Gadget key to switch the gadget in hand on or off; hold it and let go to throw it where you aimed. The
        /// gadget wheel (GadgetWheel.cs: a slice swaps it into your hand) opens on its own key while that's held; with no Gadget wheel key,
        /// holding the Gadget key with gadgets in the bag opens it instead (its middle then throws).
        void TickGadgetKey(Player me, bool free)
        {
            if (!free || me == null || me.Downed || (me.Gadget == GadgetId.None && BagCount(me) == 0))
            {
                gadgetHold = -1f;
                gadgetWheelOpen = false;
                gadgetWheelByKey = false;
                gadgetWheelK = Mathf.MoveTowards(gadgetWheelK, 0f, Time.unscaledDeltaTime * 8f);
                return;
            }
            bool wheelKey = Keybinds.Get(KeyAction.GadgetWheel) != KeyCode.None || Keybinds.Get2(KeyAction.GadgetWheel) != KeyCode.None;
            if (wheelKey && gadgetHold < 0f && TickGadgetWheelKey(me))
                return;
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
                if (!wheelKey && !gadgetWheelOpen && gadgetHold >= GadgetHoldToThrow && BagCount(me) > 0)
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
                game.UseGadget(MouseArena());
            else
                game.ThrowGadget(MouseArena());
            gadgetHold = -1f;
            gadgetWheelOpen = false;
        }

        /// Right click while the Gadget key is held (aiming a throw, or the wheel it opened): nothing is thrown or used, and the key
        /// waits to be let go before it counts again. True on the cancelling frame.
        bool CancelGadgetThrow()
        {
            if (gadgetHold < 0f || !Input.GetMouseButtonDown(1))
                return false;
            gadgetHold = -1f;
            gadgetWheelOpen = false;
            return true;
        }

        /// The Gadget wheel key: held, the wheel is open; let go on a slice to swap that gadget into your hand (the middle cancels). True
        /// while it has the input (the Gadget key waits).
        bool TickGadgetWheelKey(Player me)
        {
            if (!gadgetWheelByKey)
            {
                if (!Keybinds.Down(KeyAction.GadgetWheel))
                    return false;
                gadgetWheelByKey = true;
                OpenGadgetWheel(me);
            }
            if (Keybinds.Held(KeyAction.GadgetWheel))
            {
                TickGadgetWheelHover();
                gadgetWheelK = Mathf.MoveTowards(gadgetWheelK, 1f, Time.unscaledDeltaTime * 8f);
                return true;
            }
            int slot = gadgetHover >= 0 ? gadgetWheelSlots[gadgetHover] : -1;
            if (slot >= 0)
                game.EquipGadget(slot);
            gadgetWheelByKey = false;
            gadgetWheelOpen = false;
            return true;
        }

        bool AimingGadget => gadgetHold >= GadgetHoldToThrow && game.Me != null && game.Me.Gadget != GadgetId.None && (!gadgetWheelOpen || gadgetHover < 0) && !gadgetWheelByKey;

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
        static readonly Rgba EnergyLime = new Rgba(0.6f, 1f, 0.25f), EnergyDark = new Rgba(0.1f, 0.26f, 0.05f);

        /// An energy drink can: a lime can with a steel rim and a dark lightning bolt, tilted by `tilt`. s = half its height.
        void DrawEnergyShape(V2 c, float s, float alpha, float tilt, bool glow)
        {
            V2.Axes(tilt - Mathf.PI / 2f, out V2 up, out V2 side);
            float w = s * 1.15f;
            if (glow)
                Circle(c, s * 2.4f, EnergyLime.WithAlpha(0.13f * alpha), 16);
            Line(c - up * (s + 0.8f), c + up * (s + 0.8f), w + 1.8f, EnergyDark.WithAlpha(alpha));  // outline
            Line(c - up * s, c + up * s, w, EnergyLime.WithAlpha(alpha));
            Line(c + up * (s - s * 0.22f), c + up * s, w, MagSteel.WithAlpha(alpha));  // the top rim
            Line(c - up * s, c - up * (s - s * 0.14f), w, MagSteel.WithAlpha(0.8f * alpha));  // the base
            float b = s * 0.42f, bw = Mathf.Max(1.2f, s * 0.22f);  // the bolt
            V2 p0 = c + up * (b * 1.1f) + side * (b * 0.25f), p1 = c - side * (b * 0.3f), p2 = c + side * (b * 0.3f), p3 = c - up * (b * 1.1f) - side * (b * 0.25f);
            Line(p0, p1, bw, EnergyDark.WithAlpha(alpha));
            Line(p1, p2, bw, EnergyDark.WithAlpha(alpha));
            Line(p2, p3, bw, EnergyDark.WithAlpha(alpha));
            Line(c - side * (w * 0.32f) - up * (s * 0.6f), c - side * (w * 0.32f) + up * (s * 0.5f), Mathf.Max(1f, w * 0.14f), new Rgba(1f, 1f, 1f, 0.35f * alpha));  // shine
        }

        readonly System.Collections.Generic.Dictionary<int, V2> energyLast = new System.Collections.Generic.Dictionary<int, V2>(), energyVel = new System.Collections.Generic.Dictionary<int, V2>();

        /// A drinking player's speed lines: streaks trailing behind the way they're moving, and a lime ring with the time left.
        void DrawEnergyTrail(Player p, float t)
        {
            float dt = Mathf.Max(1e-3f, Time.deltaTime), r = p.Def.BodyR;
            V2 last, vel;
            if (!energyLast.TryGetValue(p.Id, out last))
                last = p.Disp;
            energyVel.TryGetValue(p.Id, out vel);
            V2 now = (p.Disp - last) * (1f / dt);
            if (now.Length > 2000f)
                now = default(V2);  // (a teleport)
            vel = V2.Lerp(vel, now, Mathf.Clamp01(dt * 12f));
            energyLast[p.Id] = p.Disp;
            energyVel[p.Id] = vel;
            float speed = vel.Length;
            if (speed > 40f)
            {
                V2 back = vel * (-1f / speed), n = new V2(-back.Y, back.X);
                float k = Mathf.Clamp01(speed / 400f);
                for (int i = 0; i < 5; i++)
                {
                    float f = Mathf.Repeat(t * 3.2f + i * 0.41f, 1f), off = (i - 2f) * r * 0.42f;
                    V2 a = p.Disp + n * off + back * (r * 0.6f + f * 26f);
                    Line(a, a + back * (10f + 18f * k), 2f, EnergyLime.WithAlpha(0.55f * k * (1f - f)));
                }
            }
            Arc(p.Disp, r + 4f, 2.4f, EnergyLime.WithAlpha(0.85f), 30, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(p.GadgetLeft / Game.EnergyTime));
            float pulse = Mathf.Repeat(t * 2.2f, 1f);
            Arc(p.Disp, r + 5f + pulse * 10f, 1.4f, EnergyLime.WithAlpha(0.4f * (1f - pulse)), 26);
        }

        /// A recall beacon: a ring round a house-like chevron pointing home. s = its radius.
        void DrawLodestoneShape(V2 c, float s, float alpha, float spin)
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

        /// The gadgets on the floor (bobbing, a slow turn, a ring in its colour) and in the air (an arc over its shadow).
        void DrawGadgets(float t)
        {
            foreach (var g in game.Gadgets.Values)
            {
                if (!game.MinePickup(g.Owner))
                    continue;
                Rgba ring = GadgetTone(g.Kind);
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
                    if (g.Fizzle > 0f)  // a Tactical Vest drop: its time on the floor drains as a gold arc, and it blinks before it fizzles (TacticalVest.cs)
                    {
                        if (Game.VestBlinkAlpha(g, t) < 1f)
                            continue;
                        Arc(ground, 22f, 1.6f, Game.VestInk.WithAlpha(0.6f), 24, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(g.Fizzle / Game.VestFizzle));
                    }
                    lift = 3f + Mathf.Sin(t * 3f + g.Id) * 2f;
                    float pulse = Mathf.Repeat(t * 0.7f + g.Id * 0.3f, 1f);
                    Arc(ground, 18f + pulse * 18f, 1.6f, ring.WithAlpha(0.5f * (1f - pulse)), 20);
                    // pickup glow: soft halo plus a steady outline, both breathing together
                    float beat = 0.5f + 0.5f * Mathf.Sin(t * 3.2f + g.Id);
                    Circle(ground - new V2(0f, lift), 17f + beat * 3f, ring.WithAlpha(0.07f + 0.08f * beat), 20);
                    Circle(ground - new V2(0f, lift), 12f + beat * 2f, ring.WithAlpha(0.1f + 0.1f * beat), 18);
                    Arc(ground - new V2(0f, lift), 14f + beat * 1.5f, 1.8f, ring.WithAlpha(0.55f + 0.4f * beat), 24);
                }
                Circle(ground + new V2(0f, 7f), 10f - lift * 0.05f, new Rgba(0f, 0f, 0f, 0.3f), 10);  // shadow
                DrawGadgetShape(g.Kind, ground - new V2(0f, lift + 3f), 8.5f, spin, t, true);
                if (Game.GadgetTimed(g.Kind) && !g.Flying && g.Left < Game.GadgetTime(g.Kind) - 0.05f)  // part used: what's left as a thin arc
                    Arc(ground, 17f, 2f, ring.WithAlpha(0.7f), 18, -Mathf.PI / 2f, MathX.Tau * Mathf.Clamp01(g.Left / Game.GadgetTime(g.Kind)));
                else if (!g.Flying && g.Uses > 0 && g.Uses < Game.GadgetUses(g.Kind))  // uses spent: a pip for each left
                    for (int i = 0; i < g.Uses; i++)
                        Circle(ground + new V2((i - (g.Uses - 1) * 0.5f) * 6f, 15f), 2f, ring, 6);
            }
        }

        /// A player's held gadget beside them; switched on, field lines drawing in and a timer ring.
        void DrawHeldGadget(Player p, float t)
        {
            if (p.Gadget == GadgetId.None || p.Downed)
                return;
            float r = p.Def.BodyR;
            if (p.Gadget == GadgetId.Lodestone)
            {
                if (p.GadgetOn)  // the channel: rings rising off the feet and a progress arc
                {
                    float prog = 1f - Mathf.Clamp01(p.GadgetLeft / Game.LodestoneChannel);
                    for (int i = 0; i < 3; i++)
                    {
                        float f = Mathf.Repeat(t * 1.1f + i / 3f, 1f);
                        Arc(p.Disp + new V2(0f, -f * 34f), r + 4f + f * 6f, 1.8f, GadgetCyan.WithAlpha(0.5f * (1f - f)), 28);
                    }
                    Arc(p.Disp, r + 4f, 2.6f, GadgetCyan.WithAlpha(0.9f), 30, -Mathf.PI / 2f, MathX.Tau * prog);
                }
                DrawLodestoneShape(p.Disp + V2.FromAngle(p.Aim + 2.1f) * (r + 9f), 5.5f, 1f, t * 2f);
                return;
            }
            if (p.Gadget == GadgetId.EnergyDrink)
            {
                if (p.GadgetOn)
                    DrawEnergyTrail(p, t);
                DrawEnergyShape(p.Disp + V2.FromAngle(p.Aim + 2.1f) * (r + 9f), 5f, 1f, p.GadgetOn ? Mathf.Sin(t * 9f) * 0.25f : 0f, p.GadgetOn);
                return;
            }
            if (p.Gadget != GadgetId.Magnet)
            {
                if (p.Gadget == GadgetId.ShieldGenerator && p.GadgetOn)
                    DrawShieldDome(p, t);  // GadgetShapes.cs
                else if (p.Gadget == GadgetId.Cloak && p.GadgetOn)
                    DrawCloakAura(p, t);
                else if (p.Gadget == GadgetId.OverclockChip && p.GadgetOn)
                    DrawOverclockAura(p, t);
                DrawGadgetShape(p.Gadget, p.Disp + V2.FromAngle(p.Aim + 2.1f) * (r + 9f), 5f, 0f, t, p.GadgetOn);
                return;
            }
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

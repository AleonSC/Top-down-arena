using System;

namespace Arena
{
    /// Tactical Vest, the DPS Soldier's 4th choice (`AbilityId.TacticalVest`, `AbilityDef.Fourth`: the lobby's 4th ability key;
    /// mockup in session chat). A press pops VestDrops random gadgets off your vest (distinct kinds from VestGadgets): each flies
    /// out over GadgetFlyTime and lands VestDropMin-VestDropMax px round you as a real floor gadget (Gadgets.cs) with its full uses,
    /// yours alone to pick up (`Gadget.Owner`) and then used like any other gadget (tap or hold the Gadget key, the bag, the wheel).
    /// One left on the floor fizzles out after VestFizzle s (`Gadget.Fizzle`, replicated, so every screen blinks it over its last
    /// VestBlink s); once carried it's an ordinary gadget and never fizzles. Mastered: VestDropsMastered gadgets, and they stay on
    /// the floor until picked up.
    public partial class Game
    {
        public const float VestCooldown = 20f, VestFizzle = 12f, VestBlink = 3f, VestDropMin = 50f, VestDropMax = 90f;
        public const int VestDrops = 3, VestDropsMastered = 4;
        public static readonly Rgba VestInk = new Rgba(1f, 0.8f, 0.35f);  // the pop's ring (CrtStyle.Gold)

        /// What the vest can drop: the Soldier's throwables and the EMP.
        public static readonly GadgetId[] VestGadgets = { GadgetId.Grenade, GadgetId.SmokeBomb, GadgetId.GasGrenade, GadgetId.Incendiary,
            GadgetId.LandMine, GadgetId.Emp };
        readonly GadgetId[] vestPool = (GadgetId[])VestGadgets.Clone();

        /// Host: pop the vest. The gadgets fly from you to a ring round you (spread evenly, a little jitter), into the arena and out
        /// of walls.
        void CastTacticalVest(Player p, bool cap)
        {
            int n = Math.Min(vestPool.Length, cap ? VestDropsMastered : VestDrops);
            for (int i = vestPool.Length - 1; i > 0; i--)  // shuffle, then take the first n
            {
                int j = GadgetRng.Next(i + 1);
                var t = vestPool[i];
                vestPool[i] = vestPool[j];
                vestPool[j] = t;
            }
            float a0 = (float)GadgetRng.NextDouble() * MathX.Tau;
            for (int i = 0; i < n; i++)
            {
                float a = a0 + i * MathX.Tau / n + ((float)GadgetRng.NextDouble() - 0.5f) * 0.6f;
                float d = VestDropMin + (float)GadgetRng.NextDouble() * (VestDropMax - VestDropMin);
                V2 to = p.Pos + V2.FromAngle(a) * d;
                to = ClampToArena(LayoutOn ? PushOutOfSolids(to, 12f) : to, 14f);
                GadgetId kind = vestPool[i];
                var g = SpawnGadget(kind, p.Pos, GadgetTime(kind));
                g.Owner = p.Id;
                g.Thrower = p.Id;  // (not caught leaving the vest; free to take once you've walked off where it landed)
                g.From = p.Pos;
                g.To = to;
                g.FlyT = 0f;
                g.Fizzle = cap ? 0f : VestFizzle;
            }
            BroadcastFx(FxKind.Ring, p.Pos, VestInk, 46f, 0.3f, Sfx.PickupBig);
        }

        readonly System.Collections.Generic.List<int> fizzled = new System.Collections.Generic.List<int>();

        /// Host, from TickGadgets: vest gadgets on the floor run down and fizzle out.
        void TickVestFizzle(float dt)
        {
            fizzled.Clear();
            foreach (var g in Gadgets.Values)
            {
                if (g.Fizzle <= 0f || g.Flying)
                    continue;
                g.Fizzle -= dt;
                if (g.Fizzle <= 0f)
                    fizzled.Add(g.Id);
            }
            foreach (int id in fizzled)
            {
                BroadcastFx(FxKind.Ring, Gadgets[id].Pos, new Rgba(0.6f, 0.62f, 0.7f), 20f, 0.3f, Sfx.None);
                Gadgets.Remove(id);
            }
        }

        /// 0-1: is a vest gadget about to fizzle (blinking over its last VestBlink s)? Its draw alpha this frame.
        public static float VestBlinkAlpha(Gadget g, float t) =>
            g.Fizzle > 0f && g.Fizzle < VestBlink && !g.Flying ? (((int)(t * (g.Fizzle < 1f ? 16f : 8f)) & 1) == 0 ? 1f : 0.25f) : 1f;
    }
}

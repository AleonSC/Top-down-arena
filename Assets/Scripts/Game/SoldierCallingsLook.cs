using System;
using UnityEngine;

namespace Arena
{
    /// The look of the Soldier's Callings (Core/SoldierCallings.cs): the six Calling weapons in the hand, their shots, the
    /// three shoulder mounts (Flak Mount, Lifeline Tether, Shoulder Minigun), the Nano Sprayer's mist, the tether's beam,
    /// the Defib Cannon's charge pips and the mounts' tracers (FxKind.MountTracer).
    public partial class ArenaApp
    {
        /// A colour with its alpha scaled.
        static Rgba Fa(Rgba c, float a) => c.WithAlpha(c.A * a);

        static readonly Rgba SiegeOrange = new Rgba(1f, 0.66f, 0.3f), DefibCyan = new Rgba(0.45f, 1f, 0.95f), NanoGreen = new Rgba(0.55f, 1f, 0.8f);
        static readonly Rgba SpikeSteel = new Rgba(0.85f, 0.92f, 1f), PodFlame = new Rgba(1f, 0.55f, 0.2f), TetherGreen = new Rgba(0.4f, 1f, 0.6f);

        // --- Held (the hand's frame, MFrame: +x along the aim) -----------------------------------------------------------

        void DrawHeldSiegeCannon(Player p, float t, int hold)
        {
            MRect(-10f, -6f, 14f, 12f, MDark);                                       // the breech
            MPoly(Hx(0x5b6470), 4f, -7f, 30f, -8f, 38f, -10f, 38f, 10f, 30f, 8f, 4f, 7f);  // the long barrel, flared at the muzzle
            MRect(12f, -9f, 5f, 18f, Hx(0x3b4350)); MRect(24f, -9.5f, 4f, 19f, Hx(0x3b4350));  // reinforcing rings
            MLine(38f, -9f, 38f, 9f, 2.4f, Hx(0xff9d3a));                            // a hot orange lip
            MCirc(-4f, -8f, 4f, Hx(0x2a2f36));
        }

        void DrawHeldSlugBreacher(Player p, float t, int hold)
        {
            MRect(-8f, -3f, 9f, 6f, MWood);
            MRect(0f, -4.5f, 24f, 9f, MDark); MRect(4f, 2f, 14f, 4f, Hx(0x4c5a68));   // the pump under the barrel
            MLine(24f, 0f, 32f, 0f, 5f, MSteel);
            MLine(10f, -4.5f, 10f, 4.5f, 1.5f, Hx(0x8fb7ff)); MLine(16f, -4.5f, 16f, 4.5f, 1.5f, Hx(0x8fb7ff));
        }

        void DrawHeldDefibCannon(Player p, float t, int hold)
        {
            float lit = Mathf.Clamp01((float)p.CallingN / Mathf.Max(1, Game.DefibEveryOf(p) - 1));
            MRect(-6f, -4f, 10f, 8f, MDark);
            MPoly(Hx(0x3f5f66), 4f, -6f, 24f, -6f, 30f, -3f, 30f, 3f, 24f, 6f, 4f, 6f);
            for (int i = 0; i < 3; i++) MLine(8f + i * 5f, -6f, 8f + i * 5f, 6f, 1.6f, Fa(DefibCyan, 0.35f + 0.65f * lit));  // the coils fill as it charges
            MCirc(31f, -4f, 2.2f, MSteel); MCirc(31f, 4f, 2.2f, MSteel);                       // the paddles' prongs
            if (lit >= 0.99f) MLine(32f, -4f, 32f, 4f, 1.2f, Fa(Rgba.White, 0.6f + 0.4f * Mathf.Sin(t * 40f)));
        }

        void DrawHeldNanoSprayer(Player p, float t, int hold)
        {
            MRect(-6f, -3f, 8f, 6f, MDark);
            MCirc(4f, -7f, 6f, Hx(0x2f5a4a)); MCirc(4f, -7f, 3.5f, Fa(NanoGreen, 0.8f));  // the nanite canister
            MRect(2f, -2.5f, 20f, 5f, Hx(0x46564f));
            MPoly(Hx(0x5f7a6e), 22f, -2.5f, 30f, -6f, 30f, 6f, 22f, 2.5f);                      // the nozzle bell
        }

        void DrawHeldRailspike(Player p, float t, int hold)
        {
            float c = p.Id == game.MyId ? game.LiveDraw(p) : 0f;
            MRect(-10f, -3f, 12f, 6f, MDark);
            MRect(0f, -5f, 38f, 3f, Hx(0x5b6470)); MRect(0f, 2f, 38f, 3f, Hx(0x5b6470));   // two rails
            MLine(4f, 0f, 30f + 6f * c, 0f, 2f, SpikeSteel);                                // the spike between them, sliding forward as it charges
            if (c > 0.05f) MLine(0f, 0f, 38f, 0f, 5f * c, new Rgba(0.6f, 0.8f, 1f, 0.35f * c));
        }

        void DrawHeldRocketPod(Player p, float t, int hold)
        {
            MRect(-6f, -8f, 26f, 16f, Hx(0x5a5f3f));                                       // the olive pod
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2; j++)
                    MCirc(20f, -5f + i * 5f, 2f, j == 0 ? Hx(0x22262b) : Hx(0x22262b));
            MLine(-6f, -8f, 20f, -8f, 1.4f, Hx(0xffa64d)); MLine(-6f, 8f, 20f, 8f, 1.4f, Hx(0xffa64d));
        }

        // --- Shots ------------------------------------------------------------------------------------------------------

        /// A Soldier Calling shot (WorldShots); false: not one.
        bool DrawCallingShot(Bullet b, float t)
        {
            V2 dir = b.Vel.IsZero ? V2.FromAngle(0f) : b.Vel.Normalized();
            V2 side = new V2(-dir.Y, dir.X);
            switch (b.Kind)
            {
                case Defs.SiegeShell:
                    if (fxOn) { TaperTrail(b, 10, 9f, new Rgba(0.75f, 0.72f, 0.68f), 0.5f); GlowDisc(b.Pos, 22f, SiegeOrange, 0.5f); }
                    Circle(b.Pos, 9f, new Rgba(0.32f, 0.34f, 0.38f), 16);
                    Circle(b.Pos + dir * 3f, 5.5f, SiegeOrange, 12);
                    Circle(b.Pos + dir * 4f, 2.5f, new Rgba(1f, 0.93f, 0.75f), 8);
                    return true;
                case Defs.SlugRound:
                    if (fxOn) TaperTrail(b, 6, 5f, new Rgba(0.55f, 0.75f, 1f), 0.6f);
                    Line(b.Pos - dir * 6f, b.Pos + dir * 4f, 7f, new Rgba(0.75f, 0.82f, 0.9f));
                    Circle(b.Pos + dir * 4f, 3.5f, new Rgba(0.9f, 0.95f, 1f), 8);
                    return true;
                case Defs.DefibBolt:
                {
                    float r = b.Radius;
                    if (fxOn) { TaperTrail(b, 6, r * 1.4f, DefibCyan, 0.55f); GlowDisc(b.Pos, r * 3f, DefibCyan, 0.5f); }
                    for (int i = 0; i < 3; i++)  // crackling arcs round the core
                    {
                        float a = t * 18f + i * 2.1f + b.Id;
                        Line(b.Pos, b.Pos + V2.FromAngle(a) * r * 1.8f, 1.2f, Fa(Rgba.White, 0.7f));
                    }
                    Circle(b.Pos, r, DefibCyan, 12);
                    Circle(b.Pos, r * 0.5f, Rgba.White, 8);
                    return true;
                }
                case Defs.RailSpikeShot:
                    if (fxOn) { TaperTrail(b, 8, 4f, new Rgba(0.6f, 0.8f, 1f), 0.7f); GlowDisc(b.Pos, 14f, SpikeSteel, 0.35f); }
                    Line(b.Pos - dir * 16f, b.Pos + dir * 8f, 3f, SpikeSteel);
                    Line(b.Pos + dir * 8f, b.Pos + dir * 14f, 1.5f, Rgba.White);
                    return true;
                case Defs.PodRocket:
                    if (fxOn) { TaperTrail(b, 9, 4f, new Rgba(0.7f, 0.7f, 0.7f), 0.45f); GlowDisc(b.Pos - dir * 6f, 8f, PodFlame, 0.6f); }
                    Line(b.Pos - dir * 6f, b.Pos + dir * 5f, 4.5f, new Rgba(0.42f, 0.45f, 0.3f));
                    Circle(b.Pos + dir * 5f, 2.4f, PodFlame, 8);
                    Line(b.Pos - dir * 6f + side * 3f, b.Pos - dir * 6f - side * 3f, 1.6f, new Rgba(0.3f, 0.32f, 0.22f));
                    return true;
                case Defs.MinigunRound:
                    Line(b.Pos - dir * 7f, b.Pos + dir * 2f, 2f, new Rgba(1f, 0.85f, 0.45f));
                    return true;
            }
            return false;
        }

        // --- Under the player: the mist, the beam, the charge pips --------------------------------------------------------

        void DrawSoldierCallingUnder(Player p, float t)
        {
            var c = Game.CallingNow(p);
            if (!Game.SoldierCalling(c))
                return;
            if (p.Weapon.Id == WeaponId.NanoSprayer && (p.Firing || p.FiringSeen))
            {
                float reach = p.Def.BodyR + Game.NanoReachOf(p), half = Game.NanoHalfArc;
                if (fxOn)
                    for (int i = 0; i < 18; i++)  // drifting nanite motes
                    {
                        float f = MathX.Fmod(t * 1.6f + i * 0.137f, 1f), a = p.Aim + (Mathf.Sin(i * 7.3f + t * 3f) * 0.9f) * half;
                        Circle(p.Disp + V2.FromAngle(a) * (p.Def.BodyR + f * (reach - p.Def.BodyR)), 1.6f + 2.5f * f, Fa(NanoGreen, 0.7f * (1f - f)), 6);
                    }
                Wedge(p.Disp, reach, p.Aim, half, Fa(NanoGreen, 0.13f), 18);
                Arc(p.Disp, reach, 1.4f, Fa(NanoGreen, 0.5f), 12, p.Aim - half, half * 2f);
            }
            if (c == CallingId.LifelineTether && p.TetherTo != 0)
            {
                Player q;
                if (game.Players.TryGetValue(p.TetherTo, out q))
                {
                    V2 from = MountAt(p), to = q.Disp;
                    float pulse = 0.5f + 0.5f * Mathf.Sin(t * 6f);
                    var col = q.Downed ? Rgba.Gold : TetherGreen;
                    if (fxOn) Line(from, to, 7f, Fa(col, 0.18f + 0.1f * pulse));
                    Line(from, to, 2f, Fa(col, 0.85f));
                    for (int i = 0; i < 3; i++)  // beads running down the line to the ally
                    {
                        float f = MathX.Fmod(t * 1.2f + i / 3f, 1f);
                        Circle(V2.Lerp(from, to, f), 2.6f, Fa(Rgba.White, 0.8f), 8);
                    }
                    Arc(to, q.Def.BodyR + 6f, 1.5f, Fa(col, 0.6f), 16);
                }
            }
            if (c == CallingId.DefibCannon && p.Id == game.MyId)
            {
                int every = Game.DefibEveryOf(p);
                for (int i = 0; i < every; i++)  // a pip per shock: the last lights gold, the next full charge
                {
                    float a = MathX.Pi * 0.5f + (i - (every - 1) * 0.5f) * 0.22f;
                    V2 at = p.Disp + V2.FromAngle(a) * (p.Def.BodyR + 12f);
                    bool lit = i < p.CallingN;
                    Circle(at, 2.6f, i == every - 1 ? Fa(Rgba.Gold, lit ? 1f : 0.35f) : Fa(DefibCyan, lit ? 1f : 0.3f), 8);
                }
            }
        }

        // --- The shoulder mounts (over the player) -----------------------------------------------------------------------

        static V2 MountAt(Player p) => p.Disp + V2.FromAngle(p.Aim - MathX.Pi * 0.5f) * (p.Def.BodyR * 0.8f) + new V2(0f, -6f);

        void DrawSoldierMount(Player p, float t)
        {
            var c = Game.CallingNow(p);
            if (Game.WayOf(c) != Game.CallingWay.AutoFire || p.Downed)
                return;
            V2 at = MountAt(p);
            float aim = c == CallingId.LifelineTether && p.TetherTo == 0 ? p.Aim : p.MountAim;
            V2 fw = V2.FromAngle(aim), sd = new V2(-fw.Y, fw.X);
            Circle(at, 5.5f, new Rgba(0.2f, 0.22f, 0.26f), 12);                     // the swivel
            switch (c)
            {
                case CallingId.FlakMount:
                    Line(at, at + fw * 13f, 4f, new Rgba(0.45f, 0.5f, 0.58f));
                    Line(at + fw * 13f + sd * 2.5f, at + fw * 13f - sd * 2.5f, 2f, Rgba.Gold);
                    break;
                case CallingId.LifelineTether:
                    Line(at, at + fw * 8f, 3.5f, new Rgba(0.35f, 0.45f, 0.4f));
                    Circle(at + fw * 9f, 3.5f, Fa(TetherGreen, 0.6f + 0.4f * Mathf.Sin(t * 5f)), 10);
                    break;
                case CallingId.ShoulderMinigun:
                {
                    float spin = p.MountSpin / 255f, rot = t * 30f * spin;
                    for (int i = 0; i < 3; i++)  // three barrels, turning
                    {
                        float o = Mathf.Sin(rot + i * 2.1f) * 2.4f;
                        Line(at + sd * o, at + sd * o + fw * 15f, 1.6f, new Rgba(0.55f, 0.58f, 0.64f));
                    }
                    Line(at - fw * 3f, at + fw * 4f, 6f, new Rgba(0.28f, 0.3f, 0.34f));
                    if (spin > 0.95f && fxOn && MathX.Fmod(t * 11f, 1f) < 0.5f)
                        GlowDisc(at + fw * 17f, 6f, new Rgba(1f, 0.85f, 0.45f), 0.7f);
                    break;
                }
            }
            Circle(at, 2.2f, new Rgba(0.6f, 0.64f, 0.7f), 8);
        }

        /// The Flak Mount's tracers (FxKind.MountTracer: Pos the mount, Pos2 the bullet it shot down).
        void DrawMountTracers()
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.MountTracer)
                {
                    float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
                    Line(fx.Pos, fx.Pos2, fx.Radius * (1f + f), Fa(fx.Color, 1f - f));
                }
        }
    }
}

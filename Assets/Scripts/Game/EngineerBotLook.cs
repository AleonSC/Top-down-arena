using UnityEngine;

namespace Arena
{
    /// The Engineer's robot weapons (Core/EngineerBots.cs):
    ///   the robots (MinionKind.TankBot / MedicBot / AssaultBot): the Engineer's own model in look 3 (steel plating, a power cell,
    ///     a steel hard hat with a cyan visor, its role's stripe and an antenna), holding a rivet gun; Wind 2-3 builds it in (a cyan
    ///     scan ring), Wind 0-1 is its recoil; a sent robot shows a dashed line and the lock reticle on its target; the Medic Bot a
    ///     soft green ring where it heals;
    ///   the held tools: the Iron Fist (a steel gauntlet that punches out), the Stasis Gun (an emitter that glows while held), the
    ///     Vector Blade (a cyan-edged blade that sweeps across);
    ///   the stasis field (ZoneKind.Stasis): frost-blue rings and a hex lattice at the cursor, ticking hands on the enemies held;
    ///   the fist's shove (FxKind.FistShove): an amber arc of force rolling out with the fist at its middle.
    public partial class ArenaApp
    {
        static readonly Rgba StasisInk = new Rgba(0.55f, 0.85f, 1f), FistAmber = new Rgba(1f, 0.71f, 0.28f);

        static int BotRole(MinionKind k) => k == MinionKind.TankBot ? 0 : k == MinionKind.MedicBot ? 1 : 2;

        /// A robot Engineer.
        void DrawEngineerBot(Minion m, float t)
        {
            var d = m.Def;
            Enemy target = null;
            if (m.Stacks == 1 && m.LinkTo != 0)
                game.Enemies.TryGetValue(m.LinkTo, out target);
            if (target != null)
            {
                DrawDashed(m.Disp, target.Disp, d.Color.WithAlpha(0.5f), 2f, 12f, 8f, t * 60f);
                DrawLockReticle(target.Disp, target.Radius + 10f, t);
            }
            float build = m.Wind >= 2f ? MathX.Clamp01(m.Wind - 2f) : 1f;
            if (m.Kind == MinionKind.MedicBot && build >= 1f)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 3f);
                Circle(m.Disp, Game.MedicBotHealR, d.Color.WithAlpha(0.05f + 0.03f * pulse), 40);
                Arc(m.Disp, Game.MedicBotHealR, 1.5f, d.Color.WithAlpha(0.3f), 48, t * 0.6f, MathX.Tau * 0.8f);
            }
            float recoil = m.Wind < 2f ? m.Wind : 0f;
            float r = d.Radius * 0.95f;
            DrawEngineerModel(m.Disp, m.Aim, r, 3, t, recoil, build, 0f, BotRole(m.Kind));
            if (build < 1f)  // building: a cyan scan ring sweeps over it as it appears
            {
                Arc(m.Disp, r * 1.6f, 2f, EngInk.WithAlpha(0.8f), 32, t * 5f, MathX.Tau * build);
                float y = (build * 2f - 1f) * r * 1.3f;
                V2 side = V2.FromAngle(m.Aim + MathX.Pi * 0.5f);
                Line(m.Disp - side * r * 1.4f + V2.FromAngle(m.Aim) * y, m.Disp + side * r * 1.4f + V2.FromAngle(m.Aim) * y, 2f, EngInkHot.WithAlpha(0.9f));
                return;
            }
            if (m.GuardFrac < 0.999f)  // its health, over its head
            {
                V2 at = m.Disp + new V2(-r * 1.3f, -r * 1.9f);
                float w = r * 2.6f;
                SetColor(new Rgba(0f, 0f, 0f, 0.6f)); Quad(at, at + new V2(w, 0f), at + new V2(w, 4f), at + new V2(0f, 4f));
                SetColor(d.Color); Quad(at, at + new V2(w * m.GuardFrac, 0f), at + new V2(w * m.GuardFrac, 4f), at + new V2(0f, 4f));
            }
        }

        /// Which robot weapon a player holds, as DrawEngineerModel's tool: 0 none.
        static int EngineerToolOf(Player p) =>
            p.Weapon.Id == WeaponId.IronFist ? 1 : p.Weapon.Id == WeaponId.StasisGun ? 2 : p.Weapon.Id == WeaponId.VectorBlade ? 3 : p.Weapon.Id == WeaponId.BumbleRemote ? 4 : 0;

        /// The tool's motion 0-1: the punch or swipe just after an attack, the stasis field while it is out.
        float EngineerToolFire(Player p, int tool)
        {
            if (tool == 4)
                return p.Firing ? 1f : 0f;  // the Bumble Remote: Fire held
            if (tool == 2)
            {
                foreach (var z in game.Zones.Values)  // the field is out only while Fire is held
                    if (z.Owner == p.Id && z.Kind == ZoneKind.Stasis)
                        return 1f;
                return 0f;
            }
            if (p.FireCd <= 0f)
                return 0f;
            return MathX.Clamp01(p.FireCd / Mathf.Max(0.01f, p.Weapon.FireInterval));
        }

        /// The held tool in the model frame (x along the aim): `fire` is the punch (1 = just thrown), the stasis beam, or the swipe.
        void DrawEngineerTool(ModelPal K, int tool, float ry, float t, float fire)
        {
            if (tool == 1)  // Iron Fist: a steel gauntlet on the right arm that punches out; the left hand braced
            {
                float punch = fire > 0.6f ? 1f : fire / 0.6f;
                float fx = 13f + 12f * punch, fy = 4f - 3f * punch;
                ModelArm(K, 1f, -ry * 0.72f, 9f, -ry * 0.55f);
                MCirc(9f, -ry * 0.55f, 3.3f, K.Glove);
                ModelArm(K, 1f, ry * 0.72f, fx - 4f, fy);
                MRect(fx - 9f, fy - 4.5f, 7f, 9f, EngIron);                // the cuff
                MLine(fx - 8f, fy - 4.5f, fx - 8f, fy + 4.5f, 1f, FistAmber);
                MCirc(fx, fy, 6.2f, Hx(0x2a313c), 16); MCirc(fx, fy, 5.2f, EngSteel, 16);
                for (int i = -1; i <= 1; i++) MCirc(fx + 3.6f, fy + i * 2.6f, 1.5f, Hx(0xcfd8e6), 8);  // knuckles
                if (punch > 0.5f)
                    MArc(fx, fy, 9f, -0.9f, 0.9f, 2f, FistAmber.WithAlpha(punch - 0.3f));
                return;
            }
            if (tool == 2)  // Stasis Gun: a two-handed emitter, its rings glowing frost-blue while held
            {
                float rhx = 11f, rhy = 4.5f, lhx = 19f, lhy = 1.5f;
                ModelArm(K, 1f, ry * 0.72f, rhx, rhy);
                ModelArm(K, 1f, -ry * 0.72f, lhx, lhy);
                MRect(6f, -3.5f, 20f, 7f, Hx(0x1c2129)); MRect(7f, -2.5f, 18f, 5f, Hx(0x5d6878));
                MRect(9f, 3f, 4f, 5f, Hx(0x1c2129));
                float glow = 0.45f + 0.2f * Mathf.Sin(t * 6f) + 0.5f * fire;
                for (int i = 0; i < 3; i++)
                {
                    float x = 26f + i * 3.2f, rr = 3.5f + i * 1.3f;
                    MArc(x, 0f, rr, -1.2f, 1.2f, 1.4f, StasisInk.WithAlpha(Mathf.Min(1f, glow)));
                }
                MCirc(27f, 0f, 4.5f, StasisInk.WithAlpha(0.2f * glow), 12);
                MCirc(27f, 0f, 2f, MWhite.WithAlpha(0.6f + 0.4f * fire), 8);
                MCirc(rhx, rhy, 3.3f, K.Glove); MCirc(lhx, lhy, 3.3f, K.Glove);
                return;
            }
            if (tool == 4)
            {
                DrawBumbleRemoteTool(K, ry, t, fire);  // BumbleRemoteLook.cs
                return;
            }
            // Vector Blade: held in the right hand; it sweeps from one side to the other on a swipe
            float swing = fire > 0f ? Mathf.Lerp(1.15f, -1.15f, MathX.Clamp01((1f - fire) * 4f)) : 0.75f;
            float hx = 12f, hy = 6f;
            ModelArm(K, 1f, -ry * 0.72f, 8f, -ry * 0.6f);
            MCirc(8f, -ry * 0.6f, 3.3f, K.Glove);
            ModelArm(K, 1f, ry * 0.72f, hx, hy);
            MFrame(hx, hy, swing, 1f, false);
            MRect(-2f, -1.6f, 5f, 3.2f, Hx(0x2a313c));                  // grip
            MRect(3f, -4f, 2.2f, 8f, EngSteel);                         // guard
            MPoly(Hx(0xc9d4e2), 5f, -2.2f, 30f, -1.2f, 34f, 0f, 30f, 1.2f, 5f, 2.2f);
            MLine(6f, -2f, 31f, -0.9f, 1.1f, EngCyan);                  // the cyan edge
            if (fire > 0.4f)
                MLine(6f, -2f, 31f, -0.9f, 4f, EngCyan.WithAlpha(0.25f));
            MFrame(0f, 0f, 0f, 1f, false);
            MCirc(hx, hy, 3.3f, K.Glove);
        }

        /// The Stasis Gun's field: frost-blue rings, a slowly turning hex lattice and a soft fill.
        void DrawStasisZone(Zone z, float t)
        {
            V2 c = z.Pos;
            float r = z.Radius;
            Circle(c, r, StasisInk.WithAlpha(0.12f + 0.04f * Mathf.Sin(t * 8f)), 40);
            Arc(c, r, 2.5f, StasisInk.WithAlpha(0.85f), 48);
            Arc(c, r * 0.62f, 1.2f, StasisInk.WithAlpha(0.45f), 36, -t * 1.5f, MathX.Tau * 0.75f);
            for (int i = 0; i < 6; i++)  // the hex lattice's spokes
            {
                float a = t * 0.8f + i * MathX.Pi / 3f;
                Line(c + V2.FromAngle(a) * r * 0.2f, c + V2.FromAngle(a) * r * 0.92f, 1.2f, StasisInk.WithAlpha(0.4f));
                V2 p0 = c + V2.FromAngle(a) * r * 0.62f, p1 = c + V2.FromAngle(a + MathX.Pi / 3f) * r * 0.62f;
                Line(p0, p1, 1f, StasisInk.WithAlpha(0.3f));
            }
            for (int i = 0; i < 4; i++)  // brackets that pulse inward
            {
                float a = i * MathX.Pi * 0.5f + MathX.Pi * 0.25f, k = 1.08f + 0.06f * Mathf.Sin(t * 10f);
                Arc(c, r * k, 2.5f, MWhite.WithAlpha(0.7f), 6, a - 0.25f, 0.5f);
            }
            foreach (var e in game.Enemies.Values)  // the enemies held: a frost glaze on each
                if (e.Disp.CloserThan(c, r + e.Radius * 0.5f))
                {
                    Arc(e.Disp, e.Radius + 4f, 2f, StasisInk.WithAlpha(0.8f), 24, t * 3f, MathX.Tau * 0.7f);
                    Circle(e.Disp, e.Radius + 2f, StasisInk.WithAlpha(0.18f), 20);
                }
        }

        /// FxKind.FistShove: Pos the player, Pos2 the arc's far middle, Radius half the arc. An amber force arc rolls out to the
        /// reach and fades, with a ghost of the fist at its middle.
        void DrawFistShoveFx(Effect fx, float f)
        {
            V2 to = fx.Pos2 - fx.Pos;
            float reach = to.Length, aim = to.Angle, half = fx.Radius;
            if (reach < 1f)
                return;
            float grow = Mathf.Min(1f, f * 3f), fade = 1f - f;
            for (int k = 0; k < 3; k++)
            {
                float rr = reach * (0.45f + 0.55f * grow) - k * 9f;
                if (rr > 4f)
                    Arc(fx.Pos, rr, (6f - k * 1.5f) * fade + 1f, fx.Color.WithAlpha((0.8f - k * 0.22f) * fade), 20, aim - half, half * 2f);
            }
            V2 dir = V2.FromAngle(aim), side = new V2(-dir.Y, dir.X);
            for (int k = -2; k <= 2; k++)  // push streaks
            {
                V2 a0 = fx.Pos + V2.FromAngle(aim + k * half * 0.4f) * reach * (0.3f + 0.5f * grow);
                Line(a0, a0 + V2.FromAngle(aim + k * half * 0.4f) * 16f * fade, 1.6f, Rgba.White.WithAlpha(0.6f * fade));
            }
            V2 fist = fx.Pos + dir * reach * (0.35f + 0.4f * grow);
            Circle(fist, 9f, fx.Color.WithAlpha(0.35f * fade), 16);
            Circle(fist, 6f, EngSteel.WithAlpha(0.9f * fade), 14);
            for (int i = -1; i <= 1; i++)
                Circle(fist + dir * 4f + side * (i * 3f), 1.6f, Rgba.White.WithAlpha(0.8f * fade), 6);
        }
    }
}

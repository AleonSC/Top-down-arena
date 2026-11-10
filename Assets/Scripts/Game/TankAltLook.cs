using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Alt fires (Core/TankAlts.cs): Double Barrel's shells behind you and its blast, the Proximity Shell lobbed in and
    /// lying armed (ZoneKind.FlakMine), the Tear Gas canister and its cloud (ZoneKind.TearGas), and a ring under your own feet while the
    /// shell or canister comes back.
    public partial class ArenaApp
    {
        static readonly Rgba DbShellRed = new Rgba(0.82f, 0.2f, 0.16f), ShellBrass = new Rgba(0.95f, 0.75f, 0.35f);
        static readonly Rgba MineSteel = new Rgba(0.3f, 0.32f, 0.36f), MineAmber = new Rgba(1f, 0.72f, 0.3f);
        static readonly Rgba GasPale = new Rgba(0.92f, 0.9f, 0.7f), CanisterGrey = new Rgba(0.55f, 0.58f, 0.6f);

        /// Behind you: the Scattergun's two shells (red with brass caps, lit when loaded, the next one filling ringed by its progress).
        /// Under your own feet: the Proximity Shell's or Tear Gas's cooldown filling.
        void DrawTankAltPips(Player p)
        {
            if (p.Downed)
                return;
            if (p.Id == game.MyId)
            {
                float cd = p.Weapon.Id == WeaponId.FlakCannon ? game.MineLocalCd / Game.MineCd : p.Weapon.Id == WeaponId.RiotRepeater ? game.GasLocalCd / Game.TearGasCd : 0f;
                if (cd > 0f)
                {
                    Rgba col = p.Weapon.Id == WeaponId.FlakCannon ? MineAmber : GasPale;
                    Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                    Arc(p.Disp, p.Def.BodyR + 7f, 2f, col.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * (1f - cd));
                }
            }
            if (p.Weapon.Id != WeaponId.Scattergun || p.ScatShells == 0 && p.ScatCharge == 0)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            V2 back = V2.FromAngle(p.Aim + MathX.Pi), side = back.Orthogonal();
            for (int i = 0; i < Game.ScatShellMax; i++)
            {
                V2 c = p.Disp + back * 30f + side * (i == 0 ? -8f : 8f);
                bool on = i < p.ScatShells;
                if (on && fxOn)
                    Circle(c, 10f, DbShellRed.WithAlpha(0.14f + 0.1f * pulse), 12);
                Rgba body = on ? DbShellRed : new Rgba(1f, 1f, 1f, 0.14f), cap = on ? ShellBrass : new Rgba(1f, 1f, 1f, 0.2f);
                Line(c + new V2(0f, -7f), c + new V2(0f, 4f), 6.5f, body);       // the hull, standing upright
                Line(c + new V2(0f, 4f), c + new V2(0f, 8f), 7.4f, cap);        // the brass head
                if (i == p.ScatShells && p.ScatCharge > 0)  // a ring closing round the next one as your point-blank hits load it
                    Arc(c, 11f, 1.8f, ShellBrass.WithAlpha(0.75f), 16, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.ScatCharge / (float)Game.ScatShellHits));
            }
        }

        /// Double Barrel going off: a fat cone of flame along the aim, both barrels' smoke rolling back, a shell or two kicked out.
        void DrawDoubleBarrelFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            int shells = Mathf.Max(1, (int)fx.Radius % 10);
            V2 dir = (fx.Pos2 - fx.Pos).Normalized(), side = dir.Orthogonal();
            float flash = Mathf.Clamp01(1f - f / 0.35f), len = (60f + 22f * shells) * (0.6f + 0.4f * flash);
            if (flash > 0f)
            {
                for (int k = 0; k < 3; k++)  // the cone of flame, white-hot in the middle
                {
                    float w = (0.42f - k * 0.12f) * len;
                    Rgba col = (k == 2 ? Rgba.White : k == 1 ? ShellBrass : new Rgba(1f, 0.45f, 0.15f)).WithAlpha(0.75f * flash);
                    V2 tip = fx.Pos + dir * len * (1f - k * 0.2f);
                    Quad(fx.Pos + side * 4f, fx.Pos - side * 4f, tip - side * w, tip + side * w, col);
                }
                Circle(fx.Pos, 10f + 6f * shells * flash, Rgba.White.WithAlpha(0.6f * flash), 14);
            }
            if (!fxOn)
                return;
            for (int k = 0; k < 6; k++)  // smoke rolling out and up
            {
                float a = (k - 2.5f) * 0.25f, d = 18f + 70f * f * (0.6f + 0.1f * k);
                V2 at = fx.Pos + V2.FromAngle(dir.Angle + a) * d + new V2(0f, -14f * f);
                Circle(at, 7f + 14f * f, new Rgba(0.6f, 0.6f, 0.62f, 0.28f * (1f - f)), 12);
            }
            for (int s = 0; s < shells; s++)  // the spent shells spinning out behind
            {
                V2 at = fx.Pos - dir * (14f + 40f * f) + side * ((s == 0 ? 1f : -1f) * (10f + 30f * f)) + new V2(0f, -30f * f * (1f - f) * 4f);
                V2 u = V2.FromAngle(f * 14f + s * 2f) * 4f;
                Line(at - u, at + u, 3.6f, DbShellRed.WithAlpha(1f - f));
            }
        }

        /// Where a lobbed shell or canister is on its way in (an arc from 140 px back along its flight, over its shadow).
        V2 LobbedAt(Zone z, float k, out float lift)
        {
            V2 from = z.Pos - V2.FromAngle(z.Aim) * 140f * (1f - k);
            lift = 50f * 4f * k * (1f - k);
            return from;
        }

        /// The Proximity Shell: lobbed in (with a ring where it lands), then a squat finned shell lying armed: an amber light blinking
        /// faster as its time runs out, and a faint ring showing how far its burst reaches.
        void DrawFlakMine(Zone z, float t)
        {
            float age = z.Duration - z.T;
            if (age < Game.MineArm)
            {
                float k = age / Game.MineArm, lift;
                V2 at = LobbedAt(z, k, out lift);
                Arc(z.Pos, Game.MineTrigger * (1.6f - 0.6f * k), 2f, MineAmber.WithAlpha(0.5f), 24);
                Circle(at, 5f * (1f - 0.4f * k), new Rgba(0f, 0f, 0f, 0.25f), 10);
                Circle(at + new V2(0f, -lift), 8f, MineSteel, 10);
                Circle(at + new V2(0f, -lift), 3.5f, MineAmber, 8);
                return;
            }
            float settle = Mathf.Clamp01((age - Game.MineArm) / 0.2f);
            float left = z.T / Mathf.Max(0.01f, z.Duration);
            float rate = left < 0.2f ? 10f : 3.5f;
            bool blink = Mathf.Sin(t * rate * MathX.Tau + z.Id) > 0.3f;
            Arc(z.Pos, z.Radius, 1.5f, MineAmber.WithAlpha(0.16f * settle), 40);
            Arc(z.Pos, Game.MineTrigger, 1.5f, MineAmber.WithAlpha((blink ? 0.35f : 0.15f) * settle), 24);
            if (fxOn && blink)
                Circle(z.Pos, 20f, MineAmber.WithAlpha(0.18f), 14);
            Circle(z.Pos + new V2(2f, 3f), 12f, new Rgba(0f, 0f, 0f, 0.3f), 12);  // shadow
            for (int i = 0; i < 3; i++)  // fins splayed out from the shell's tail
            {
                V2 u = V2.FromAngle(z.Aim + MathX.Pi + (i - 1) * 0.7f);
                Line(z.Pos + u * 6f, z.Pos + u * 16f, 3.5f, MineSteel.Mix(Rgba.White, 0.25f));
            }
            Circle(z.Pos, 10f, MineSteel, 14);
            Arc(z.Pos, 10f, 1.5f, MineAmber.WithAlpha(0.5f), 14);
            Circle(z.Pos, 4f, blink ? MineAmber : MineAmber.Mix(MineSteel, 0.6f), 8);
        }

        /// Tear Gas: the canister tumbling in, then a pale churning cloud, the canister still hissing in its middle.
        void DrawTearGas(Zone z, float t)
        {
            float age = z.Duration - z.T;
            if (age < Game.TearGasFlight)
            {
                float k = age / Game.TearGasFlight, lift;
                V2 at = LobbedAt(z, k, out lift);
                Arc(z.Pos, z.Radius, 2f, GasPale.WithAlpha(0.25f + 0.35f * k), 32);
                Arc(z.Pos, z.Radius * (1f - k), 1.5f, GasPale.WithAlpha(0.5f), 32);
                Circle(at, 4f, new Rgba(0f, 0f, 0f, 0.25f), 8);
                V2 u = V2.FromAngle(t * 16f) * 5f;
                Line(at + new V2(0f, -lift) - u, at + new V2(0f, -lift) + u, 5f, CanisterGrey);
                return;
            }
            float grow = Mathf.Clamp01((age - Game.TearGasFlight) / 0.45f), fade = Mathf.Clamp01(z.T / 0.8f);
            float r = z.Radius * (0.4f + 0.6f * grow);
            Circle(z.Pos, r, GasPale.WithAlpha(0.13f * fade), 36);
            int puffs = fxOn ? 9 : 5;
            for (int k = 0; k < puffs; k++)  // puffs churning round, drifting up
            {
                float a = t * (0.4f + 0.07f * k) * (k % 2 == 0 ? 1f : -1f) + k * 0.7f + z.Id, d = r * (0.2f + 0.07f * k);
                V2 at = z.Pos + V2.FromAngle(a) * Mathf.Min(d, r * 0.62f) + new V2(0f, -3f * Mathf.Sin(t * 1.3f + k));
                Circle(at, r * (0.32f + 0.05f * Mathf.Sin(t * 2f + k)), GasPale.WithAlpha(0.11f * fade), 18);
            }
            Arc(z.Pos, r, 1.5f, GasPale.WithAlpha(0.3f * fade), 40);
            Line(z.Pos - new V2(5f, 0f), z.Pos + new V2(5f, 0f), 4.5f, CanisterGrey.WithAlpha(fade));  // the canister, hissing
            if (fxOn)
                for (int k = 0; k < 3; k++)
                {
                    float f = Mathf.Repeat(t * 1.6f + k / 3f, 1f);
                    Circle(z.Pos + new V2(6f + 10f * f, -8f * f), 2f + 5f * f, Rgba.White.WithAlpha(0.35f * (1f - f) * fade), 8);
                }
        }
    }
}

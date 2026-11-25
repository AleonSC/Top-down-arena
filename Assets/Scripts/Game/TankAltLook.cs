using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Alt fires (Core/TankAlts.cs): Double Barrel's shells behind you and its blast, the Proximity Shell lobbed in and
    /// lying armed (ZoneKind.FlakMine), the Tear Gas canister and its cloud (ZoneKind.TearGas), and a ring under your own feet while the
    /// shell or canister comes back.
    public partial class ArenaApp
    {
        static readonly Rgba GrenadeBody = new Rgba(0.3f, 0.34f, 0.4f), ShockBlue = new Rgba(0.45f, 0.85f, 1f);
        static readonly Rgba MineSteel = new Rgba(0.3f, 0.32f, 0.36f), MineAmber = new Rgba(1f, 0.72f, 0.3f);
        static readonly Rgba GasPale = new Rgba(0.92f, 0.9f, 0.7f), CanisterGrey = new Rgba(0.55f, 0.58f, 0.6f);

        /// Behind you: the Scattergun's three shock grenades (grey with a glowing band, lit when loaded, the next one filling ringed by its progress).
        /// Under your own feet: the Proximity Shell's or Tear Gas's cooldown filling.
        void DrawTankAltPips(Player p)
        {
            if (p.Downed)
                return;
            if (p.Id == game.MyId)
            {
                float cd = p.Weapon.Id == WeaponId.FlakCannon ? game.MineLocalCd / Game.MineCd : p.Weapon.Id == WeaponId.RiotRepeater ? game.GasLocalCd / Game.TearGasCd
                    : p.Weapon.Id == WeaponId.BoneGuard ? game.BoneWallLocalCd / Game.BoneWallCd
                    : p.Weapon.Id == WeaponId.Nimbus ? game.StormLocalCd / Game.NimbusStormCd
                    : p.Weapon.Id == WeaponId.WindClash ? game.TempestLocalCd / Game.TempestCd
                    : p.Weapon.Id == WeaponId.TwinTessen ? p.TtGustCd / Game.TtGustCd  // (the Twin Tessen's Kamikaze Gust: TwinTessen.cs)
                    : p.Weapon.Id == WeaponId.Ofuda ? game.OfudaLocalCd / Game.BlastCd  // (the Ofuda's Blast Ofuda: Ofuda.cs)
                    : p.Weapon.Id == WeaponId.HiveCenser ? game.HiveLocalCd / Game.HiveCallCd  // (the Hive Censer's Hive Call: HiveCenser.cs)
                    : p.Weapon.Id == WeaponId.StaticShock ? game.ArcLocalCd / Game.CannonCd  // (Static Shock's Arc Cannon: StaticShock.cs)
                    : p.Weapon.Id == WeaponId.WyvernStaff ? game.WyvernSlamLocalCd / Game.WyvernSlamCd
                    : p.Weapon.Id == WeaponId.BumbleRemote ? game.BumbleBurstLocalCd / Game.BumbleBurstCd  // (the Bumble Remote's Swarm Burst: BumbleRemote.cs)
                    : p.Weapon.Id == WeaponId.BloodBlast ? game.TideLocalCd / Game.TideCd
                    : p.Weapon.Id == WeaponId.FrostLance ? game.ZeroLocalCd / Game.ZeroCd
                    : p.Weapon.Id == WeaponId.SpiritChalk ? game.IgniteLocalCd / Game.ChalkIgniteCd : 0f;  // (the Frost Lance's Absolute Zero: FrostLance.cs)  // (Wind Clash's Tempest: WindClash.cs)  // (the Nimbus's Eye of the Storm: Nimbus.cs)  // (the Bone Guard's Ossuary Wall: BoneGuard.cs)
                if (cd > 0f)
                {
                    Rgba col = p.Weapon.Id == WeaponId.WyvernStaff ? Game.WyvColor : p.Weapon.Id == WeaponId.BumbleRemote ? Game.BumbleGold : p.Weapon.Id == WeaponId.FlakCannon ? MineAmber : p.Weapon.Id == WeaponId.BoneGuard ? Defs.BulletColor(Defs.BoneShot) : p.Weapon.Id == WeaponId.Nimbus ? Game.NimbusBoltColor : p.Weapon.Id == WeaponId.WindClash ? Game.WcColor : p.Weapon.Id == WeaponId.TwinTessen ? Game.TtWind : p.Weapon.Id == WeaponId.StaticShock ? Game.CannonViolet : p.Weapon.Id == WeaponId.HiveCenser ? Game.HiveGold : p.Weapon.Id == WeaponId.Ofuda ? Game.OfudaRed : p.Weapon.Id == WeaponId.BloodBlast ? new Rgba(0.88f, 0.12f, 0.24f) : p.Weapon.Id == WeaponId.FrostLance ? Game.FlColor : p.Weapon.Id == WeaponId.SpiritChalk ? new Rgba(1f, 0.55f, 0.15f) : GasPale;
                    Arc(p.Disp, p.Def.BodyR + 7f, 2f, new Rgba(1f, 1f, 1f, 0.1f), 28);
                    Arc(p.Disp, p.Def.BodyR + 7f, 2f, col.WithAlpha(0.6f), 28, -MathX.Pi / 2f, MathX.Tau * (1f - cd));
                }
            }
            if (p.Weapon.Id != WeaponId.Scattergun || p.ScatShells >= Game.ScatShellMax && p.Id != game.MyId)
                return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
            float popAge = fxOn ? ShellLoadAge(p) : 99f;
            for (int i = 0; i < Game.ScatShellMax; i++)
            {
                V2 c = ShellPipAt(p, i);
                bool on = i < p.ScatShells;
                // The shell that just loaded drops in from above and bounces (a squash, then an overshoot).
                float sc = 1f, drop = 0f;
                if (on && i == p.ScatShells - 1 && popAge < ShellPop)
                {
                    float k = popAge / ShellPop;
                    drop = k < 0.35f ? -14f * Sq(1f - k / 0.35f) : 0f;
                    sc = k < 0.35f ? 1f : 1f + 0.35f * Mathf.Sin((k - 0.35f) / 0.65f * MathX.Pi) * (1f - k);
                }
                c += new V2(0f, drop);
                if (on && fxOn)
                    Circle(c, 10f * sc, ShockBlue.WithAlpha(0.14f + 0.1f * pulse), 12);
                Rgba body = on ? GrenadeBody : new Rgba(1f, 1f, 1f, 0.14f), cap = on ? ShockBlue : new Rgba(1f, 1f, 1f, 0.2f);
                Circle(c, 6.5f * sc, body, 12);                                   // the round body
                Arc(c, 6.5f * sc, 1.6f, cap, 12);                                 // its glowing band
                Line(c + new V2(0f, -6f * sc), c + new V2(0f, -9f * sc), 3f * sc, cap);  // the fuse cap
                if (i == p.ScatShells && p.ScatCharge > 0)  // a ring closing round the next one as your point-blank hits load it
                    Arc(c, 11f, 1.8f, ShockBlue.WithAlpha(0.75f), 16, -MathX.Pi / 2f, MathX.Tau * Mathf.Clamp01(p.ScatCharge / 100f));
            }
        }

        /// Where shell pip `i` sits: behind you, side by side.
        V2 ShellPipAt(Player p, int i)
        {
            V2.Axes(p.Aim + MathX.Pi, out V2 back, out V2 side);
            return p.Disp + back * 30f + side * ((i - (Game.ScatShellMax - 1) / 2f) * 10f);
        }

        /// A shock grenade popped from the launcher: a blue flash and a small ring at the muzzle, a puff of smoke.
        void DrawDoubleBarrelFx(Effect fx)
        {
            float f = Mathf.Clamp01(fx.T / Mathf.Max(0.01f, fx.Dur));
            V2 dir = (fx.Pos2 - fx.Pos).Normalized();
            float flash = Mathf.Clamp01(1f - f / 0.4f);
            if (flash > 0f)
                Circle(fx.Pos + dir * 8f, 8f + 10f * flash, ShockBlue.WithAlpha(0.6f * flash), 14);
            if (!fxOn)
                return;
            for (int k = 0; k < 4; k++)
            {
                float a = (k - 1.5f) * 0.3f, d = 14f + 40f * f * (0.6f + 0.15f * k);
                Circle(fx.Pos + V2.FromAngle(dir.Angle + a) * d + new V2(0f, -10f * f), 5f + 9f * f, new Rgba(0.6f, 0.6f, 0.62f, 0.25f * (1f - f)), 12);
            }
        }

        /// A Shock Grenade: tumbling in with a ring where it lands, then a crackling blue flash that fades (ScatShockLinger).
        void DrawShockGrenade(Zone z, float t)
        {
            float age = z.Duration - z.T;
            if (age < Game.ScatFlight)
            {
                float k = age / Game.ScatFlight, lift;
                V2 at = LobbedAt(z, k, out lift);
                Arc(z.Pos, z.Radius, 2f, ShockBlue.WithAlpha(0.2f + 0.3f * k), 32);
                Arc(z.Pos, z.Radius * (1f - k), 1.5f, ShockBlue.WithAlpha(0.5f), 32);
                Circle(at, 4f, new Rgba(0f, 0f, 0f, 0.25f), 8);
                V2 g = at + new V2(0f, -lift);
                Circle(g, 5f, GrenadeBody, 10);
                Arc(g, 5f, 1.5f, ShockBlue, 10, t * 14f, MathX.Pi);
                return;
            }
            float since = age - Game.ScatFlight, f = Mathf.Clamp01(since / Game.ScatShockLinger), fade = 1f - f;
            Circle(z.Pos, z.Radius * (0.5f + 0.5f * Mathf.Sqrt(f)), ShockBlue.WithAlpha(0.18f * fade), 36);
            Arc(z.Pos, z.Radius * (0.5f + 0.5f * Mathf.Sqrt(f)), 2f, ShockBlue.WithAlpha(0.8f * fade), 40);
            int arcs = fxOn ? 9 : 5;
            for (int k = 0; k < arcs; k++)  // crackling forks from the middle, jumping about each frame
            {
                float a = k * MathX.Tau / arcs + Mathf.Floor(t * 30f) * 0.9f, r = z.Radius * (0.35f + 0.65f * ((k * 37 + (int)(t * 30f)) % 10) / 10f) * (0.6f + 0.4f * f);
                V2 mid = z.Pos + V2.FromAngle(a + 0.25f) * r * 0.55f, end = z.Pos + V2.FromAngle(a) * r;
                Line(z.Pos, mid, 1.8f, ShockBlue.WithAlpha(0.9f * fade));
                Line(mid, end, 1.4f, Rgba.White.WithAlpha(0.8f * fade));
            }
            Circle(z.Pos, 9f * fade + 3f, Rgba.White.WithAlpha(0.8f * fade), 14);
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
                V2 up = at + new V2(0f, -lift), nose = V2.FromAngle(z.Aim + t * 0f) * 7f;
                Line(up - nose, up + nose, 8f, MineSteel);  // the shell, nose first, fins folded
                Circle(up + nose, 4f, MineSteel.Mix(Rgba.White, 0.2f), 8);
                Circle(up, 2.5f, MineAmber, 8);
                return;
            }
            float since = age - Game.MineArm, settle = Mathf.Clamp01(since / 0.2f);
            float unfold = fxOn ? Mathf.Clamp01((since - 0.05f) / 0.25f) : 1f;               // the fins spring out after the thump
            float squash = fxOn && since < 0.25f ? 0.3f * Mathf.Sin(since / 0.25f * MathX.Pi) * (1f - since / 0.25f) : 0f;
            float left = z.T / Mathf.Max(0.01f, z.Duration);
            float rate = left < 0.2f ? 10f : 3.5f;
            bool blink = Mathf.Sin(t * rate * MathX.Tau + z.Id) > 0.3f;
            Arc(z.Pos, z.Radius, 1.5f, MineAmber.WithAlpha(0.16f * settle), 40);
            Arc(z.Pos, Game.MineTrigger, 1.5f, MineAmber.WithAlpha((blink ? 0.35f : 0.15f) * settle), 24);
            if (fxOn && blink)
                Circle(z.Pos, 20f, MineAmber.WithAlpha(0.18f), 14);
            Circle(z.Pos + new V2(2f, 3f), 12f, new Rgba(0f, 0f, 0f, 0.3f), 12);  // shadow
            if (fxOn && settle >= 1f)  // the proximity sensor sweeping round, a wedge of amber light
            {
                float sw = t * 3.2f + z.Id;
                for (int k = 0; k < 4; k++)
                    Arc(z.Pos, Game.MineTrigger * (0.45f + 0.18f * k), Game.MineTrigger * 0.18f, MineAmber.WithAlpha(0.1f), 10, sw - 0.6f, 0.6f);
                Line(z.Pos, z.Pos + V2.FromAngle(sw) * Game.MineTrigger, 1.5f, MineAmber.WithAlpha(0.45f));
            }
            for (int i = 0; i < 3; i++)  // fins splayed out from the shell's tail, opening after it lands
            {
                V2 u = V2.FromAngle(z.Aim + MathX.Pi + (i - 1) * 0.7f * unfold);
                Line(z.Pos + u * 6f, z.Pos + u * (8f + 8f * unfold), 3.5f, MineSteel.Mix(Rgba.White, 0.25f));
            }
            Circle(z.Pos, 10f * (1f + squash), MineSteel, 14);
            Arc(z.Pos, 10f, 1.5f, MineAmber.WithAlpha(0.5f), 14);
            Circle(z.Pos, 4f, blink ? MineAmber : MineAmber.Mix(MineSteel, 0.6f), 8);
        }

        /// Tear Gas: the canister tumbling in, then a pale churning cloud, the canister still hissing in its middle.
        void DrawTearGas(Zone z, float t, bool toxic = false)
        {
            Rgba GasPale = toxic ? ToxicPale : ArenaApp.GasPale;  // (a Gas Grenade's cloud: purple; Gadgets.cs)
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
            // The canister: a small bounce as it lands, then spinning slowly on the ground as it vents from both ends.
            float since = age - Game.TearGasFlight, hop = fxOn && since < 0.35f ? 10f * Mathf.Sin(since / 0.35f * MathX.Pi) : 0f;
            float spin = fxOn ? z.Aim + since * 2.2f / (1f + since * 0.4f) : 0f;
            V2 cn = z.Pos + new V2(0f, -hop), ax = V2.FromAngle(spin) * 6f;
            Circle(z.Pos + new V2(1f, 2f), 5f, new Rgba(0f, 0f, 0f, 0.25f * fade), 8);
            Line(cn - ax, cn + ax, 5f, CanisterGrey.WithAlpha(fade));
            Line(cn + ax * 0.6f, cn + ax, 5.4f, (toxic ? new Rgba(0.72f, 0.42f, 1f, fade) : new Rgba(0.85f, 0.75f, 0.3f, fade)));  // the yellow band (a Gas Grenade's: purple)
            if (fxOn)
                for (int end = -1; end <= 1; end += 2)  // a jet of gas out of each end
                    for (int k = 0; k < 4; k++)
                    {
                        float f = Mathf.Repeat(t * 2.2f + k / 4f + (end > 0 ? 0.125f : 0f), 1f);
                        V2 jet = cn + ax * end * (1f + 3.5f * f) + ax.Orthogonal() * (0.6f * f * Mathf.Sin(t * 9f + k));
                        Circle(jet, 2f + 6f * f, (toxic ? ToxicPale : Rgba.White).WithAlpha(0.4f * (1f - f) * fade), 8);
                    }
        }

        static readonly Rgba ToxicPale = new Rgba(0.74f, 0.5f, 1f);
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// The Tank Soldier's Alt fires, animated with Visual effects on (Juice.cs runs this; TankAltLook.cs draws the shapes):
    ///   Double Barrel   a twin muzzle blast with a shockwave, sparks and rolling smoke, both spent shells spinning out, the gun
    ///                   kicked hard and broken open (TankAltPose), a shake on your own screen; a shell loading pops into its pip.
    ///   Proximity Shell a mortar puff at the cannon, a smoke trail on the way in, a thump of dust and sparks on landing, shrapnel
    ///                   and a kick when it goes off.
    ///   Tear Gas        the canister trails gas as it tumbles in, bursts with a ring of puffs on landing, the cloud sheds wisps
    ///                   from its edge, and enemies inside drip tears and cough little puffs.
    public partial class ArenaApp
    {
        class AltSeen { public float DumpT = -99f, LoadT = -99f; public int Shells; }
        class AltZone { public ZoneKind Kind; public V2 Pos; public float T, Duration, Aim, Radius; public bool Landed; public int Owner; public float NextPuff; }

        readonly Dictionary<int, AltSeen> altSeen = new Dictionary<int, AltSeen>();
        readonly Dictionary<int, AltZone> altZones = new Dictionary<int, AltZone>();
        readonly List<int> altGone = new List<int>();
        float tearDripT;

        public const float DumpKick = 0.2f, DumpBreak = 0.55f, ShellPop = 0.35f;
        static readonly Rgba GasWisp = new Rgba(0.9f, 0.9f, 0.72f), TearBlue = new Rgba(0.7f, 0.88f, 1f);

        void ClearTankAltLooks()
        {
            altSeen.Clear();
            altZones.Clear();
        }

        AltSeen AltOf(int id)
        {
            AltSeen s;
            if (!altSeen.TryGetValue(id, out s))
                altSeen[id] = s = new AltSeen();
            return s;
        }

        /// Seconds since this player's Double Barrel went off and since a shell last loaded (large when never).
        float DumpAge(Player p) { AltSeen s; return altSeen.TryGetValue(p.Id, out s) ? juiceClock - s.DumpT : 99f; }
        float ShellLoadAge(Player p) { AltSeen s; return altSeen.TryGetValue(p.Id, out s) ? juiceClock - s.LoadT : 99f; }

        /// The Scattergun after a Double Barrel: kicked well back, then broken open and snapped shut (`pump` slides the fore-end).
        void TankAltPose(Player p, ref float kick, ref float pump)
        {
            if (p.Weapon.Id != WeaponId.Scattergun)
                return;
            float age = DumpAge(p);
            if (age < DumpKick)
                kick = Mathf.Max(kick, 15f * Sq(1f - age / DumpKick));
            float tb = (age - DumpKick) / (DumpBreak - DumpKick);
            if (tb > 0f && tb < 1f)
                pump = Mathf.Max(pump, 14f * Mathf.Sin(MathX.Pi * tb));
        }

        /// Juice's effects pass: a Double Barrel blast (FxKind.DoubleBarrel).
        void DoubleBarrelFeel(Effect fx)
        {
            int owner = (int)(fx.Radius / 10f), shells = Mathf.Max(1, (int)fx.Radius % 10);
            AltOf(owner).DumpT = juiceClock;
            V2 dir = (fx.Pos2 - fx.Pos).Normalized(), side = dir.Orthogonal(), at = fx.Pos;
            float aim = dir.Angle;
            for (int b = -1; b <= 1; b += 2)  // both barrels, side by side
            {
                V2 m = at + side * (3.5f * b);
                AddPart(new Part { Kind = PMuzzle, Pos = m, Rot = aim + b * 0.08f, Life = 0.1f, Max = 0.1f, Size = 46f + 12f * shells, Col = new Rgba(1f, 0.9f, 0.6f) });
            }
            for (int k = -2; k <= 2; k++)
                AddPart(new Part { Kind = PMuzzle, Pos = at, Rot = aim + k * 0.28f, Life = 0.08f, Max = 0.08f, Size = 26f, Col = new Rgba(1f, 0.55f, 0.2f) });
            AddPart(new Part { Kind = PFlash, Pos = at + dir * 10f, Life = 0.12f, Max = 0.12f, Size = 30f + 8f * shells, Col = Rgba.White });
            Ring(at + dir * 18f, 70f + 30f * shells, 0.3f, new Rgba(1f, 0.85f, 0.6f, 0.8f));
            for (int i = 0; i < 18 + 8 * shells; i++)  // sparks thrown forward in the cone
            {
                float a = aim + Rnd(-0.5f, 0.5f), sp = Rnd(250f, 620f);
                AddPart(new Part { Kind = PSpark, Pos = at, Vel = V2.FromAngle(a) * sp, Life = Rnd(0.12f, 0.26f), Max = 0.26f, Size = Rnd(1.4f, 2.4f), Col = new Rgba(1f, Rnd(0.6f, 0.9f), 0.4f) });
            }
            for (int i = 0; i < 10 + 4 * shells; i++)  // smoke rolling forward, then lifting
            {
                float life = Rnd(0.6f, 1.1f);
                AddPart(new Part { Kind = PDust, Pos = at + dir * Rnd(0f, 20f), Vel = dir * Rnd(60f, 220f) + side * Rnd(-90f, 90f) + new V2(0f, -Rnd(10f, 40f)), Life = life, Max = life, Size = Rnd(7f, 13f), Col = SmokeCol });
            }
            for (int s = 0; s < shells; s++)  // the spent shells kicked out of the breech
                AddPart(new Part { Kind = PCasing, Pos = at - dir * 14f, Vel = side * ((s == 0 ? 1f : -1f) * Rnd(110f, 150f)) - dir * Rnd(40f, 80f), Rot = aim + MathX.Pi / 2f, Spin = (s == 0 ? 1f : -1f) * Rnd(14f, 20f), Life = 1f, Max = 1f, Size = 5f, Col = ShellRed });
            if (owner == game.MyId)
            {
                Shake(0.22f + 0.08f * shells);
                Hold(0.035f, 0.2f);
            }
        }

        /// Each frame: shells popping into their pips, and the mines' and canisters' flight, landing, bursting and gas.
        void UpdateTankAltLooks(float dt)
        {
            foreach (var p in game.Players.Values)
            {
                var s = AltOf(p.Id);
                if (p.ScatShells > s.Shells && p.Weapon.Id == WeaponId.Scattergun)
                {
                    s.LoadT = juiceClock;
                    V2 c = ShellPipAt(p, p.ScatShells - 1);
                    Sparks(c, ShellBrass, 6, 90f, 0.25f, 1.4f);
                    Ring(c, 14f, 0.25f, ShellBrass.WithAlpha(0.8f));
                }
                s.Shells = p.ScatShells;
            }
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.FlakMine && z.Kind != ZoneKind.TearGas)
                    continue;
                AltZone a;
                if (!altZones.TryGetValue(z.Id, out a))
                {
                    float seenAge = z.Duration - z.T, flight = z.Kind == ZoneKind.FlakMine ? Game.MineArm : Game.TearGasFlight;
                    altZones[z.Id] = a = new AltZone { Kind = z.Kind, Owner = z.Owner, Landed = seenAge > flight + 0.15f };  // (already down when first seen: no thump)
                    Player owner;
                    if (seenAge < 0.15f && game.Players.TryGetValue(z.Owner, out owner))  // the launch: a puff and a flash at the gun
                    {
                        V2 m = owner.Disp + V2.FromAngle(z.Aim) * (owner.Def.BodyR + 16f);
                        AddPart(new Part { Kind = PMuzzle, Pos = m, Rot = z.Aim, Life = 0.07f, Max = 0.07f, Size = z.Kind == ZoneKind.FlakMine ? 30f : 18f, Col = z.Kind == ZoneKind.FlakMine ? MineAmber : GasWisp });
                        for (int i = 0; i < 5; i++)
                        {
                            float life = Rnd(0.4f, 0.7f);
                            AddPart(new Part { Kind = PDust, Pos = m, Vel = V2.FromAngle(z.Aim + Rnd(-0.6f, 0.6f)) * Rnd(30f, 100f), Life = life, Max = life, Size = Rnd(5f, 8f), Col = SmokeCol });
                        }
                    }
                }
                a.Pos = z.Pos; a.T = z.T; a.Duration = z.Duration; a.Aim = z.Aim; a.Radius = z.Radius;
                float age = z.Duration - z.T, fly = z.Kind == ZoneKind.FlakMine ? Game.MineArm : Game.TearGasFlight;
                if (age < fly)
                {
                    float lift;
                    V2 at = LobbedAt(z, age / fly, out lift) + new V2(0f, -lift);
                    if (Rnd(0f, 1f) < 0.7f)  // the trail: smoke behind the shell, gas behind the canister
                    {
                        float life = Rnd(0.3f, 0.55f);
                        AddPart(new Part { Kind = PDust, Pos = at, Vel = new V2(Rnd(-12f, 12f), Rnd(-20f, 0f)), Life = life, Max = life, Size = Rnd(3f, 5.5f), Col = z.Kind == ZoneKind.FlakMine ? SmokeCol : GasWisp.WithAlpha(0.7f) });
                    }
                    continue;
                }
                if (!a.Landed)
                {
                    a.Landed = true;
                    if (z.Kind == ZoneKind.FlakMine)
                    {
                        Ring(z.Pos, 30f, 0.3f, new Rgba(0.75f, 0.7f, 0.6f, 0.6f));
                        Sparks(z.Pos, MineAmber, 8, 140f, 0.2f, 1.5f);
                        for (int i = 0; i < 8; i++)
                        {
                            float ang = MathX.Tau * i / 8f, life = Rnd(0.35f, 0.6f);
                            AddPart(new Part { Kind = PDust, Pos = z.Pos, Vel = V2.FromAngle(ang) * Rnd(50f, 90f), Life = life, Max = life, Size = Rnd(4f, 7f), Col = DustColor });
                        }
                    }
                    else
                    {
                        AddPart(new Part { Kind = PFlash, Pos = z.Pos, Life = 0.1f, Max = 0.1f, Size = 18f, Col = GasWisp });
                        Ring(z.Pos, z.Radius, 0.45f, GasWisp.WithAlpha(0.7f));
                        for (int i = 0; i < 16; i++)  // the burst: puffs racing out to the cloud's edge
                        {
                            float ang = MathX.Tau * i / 16f + Rnd(-0.1f, 0.1f), life = Rnd(0.5f, 0.8f);
                            AddPart(new Part { Kind = PDust, Pos = z.Pos, Vel = V2.FromAngle(ang) * z.Radius * Rnd(1.6f, 2.2f), Life = life, Max = life, Size = Rnd(10f, 16f), Col = GasWisp.WithAlpha(0.6f) });
                        }
                    }
                }
                if (z.Kind == ZoneKind.TearGas && z.T > 0.6f && Rnd(0f, 1f) < dt * 10f)  // wisps curling off the cloud's edge
                {
                    float ang = Rnd(0f, MathX.Tau), life = Rnd(0.8f, 1.4f);
                    V2 edge = z.Pos + V2.FromAngle(ang) * z.Radius * Rnd(0.75f, 0.95f);
                    AddPart(new Part { Kind = PDust, Pos = edge, Vel = V2.FromAngle(ang) * Rnd(8f, 22f) + new V2(0f, -Rnd(6f, 16f)), Life = life, Max = life, Size = Rnd(8f, 13f), Col = GasWisp.WithAlpha(0.35f) });
                }
            }
            // Gone: an armed mine that went before its time burst under an enemy.
            altGone.Clear();
            foreach (var kv in altZones)
                if (!game.Zones.ContainsKey(kv.Key))
                    altGone.Add(kv.Key);
            foreach (int id in altGone)
            {
                var a = altZones[id];
                altZones.Remove(id);
                if (a.Kind != ZoneKind.FlakMine || !a.Landed || a.T < 0.2f)
                    continue;
                for (int i = 0; i < 14; i++)  // shrapnel: the fins and casing torn apart
                {
                    float ang = Rnd(0f, MathX.Tau), sp = Rnd(200f, 420f);
                    AddPart(new Part { Kind = PSpark, Pos = a.Pos, Vel = V2.FromAngle(ang) * sp, Life = Rnd(0.18f, 0.32f), Max = 0.32f, Size = Rnd(1.6f, 2.6f), Col = i % 3 == 0 ? Rgba.White : MineAmber });
                }
                for (int i = 0; i < 3; i++)
                    AddPart(new Part { Kind = PCasing, Pos = a.Pos, Vel = V2.FromAngle(Rnd(0f, MathX.Tau)) * Rnd(120f, 200f), Rot = Rnd(0f, MathX.Tau), Spin = Rnd(-20f, 20f), Life = 0.7f, Max = 0.7f, Size = 4f, Col = MineSteel.Mix(Rgba.White, 0.3f) });
                Ring(a.Pos, a.Radius * 1.1f, 0.3f, MineAmber.WithAlpha(0.9f));
                Player mine;
                if (game.Players.TryGetValue(game.MyId, out mine) && mine.Pos.DistanceTo(a.Pos) < 420f)
                    Shake(a.Owner == game.MyId ? 0.16f : 0.08f);
            }
            // Gassed enemies: tears dripping and a cough now and then.
            tearDripT -= dt;
            if (tearDripT > 0f)
                return;
            tearDripT = 0.12f;
            foreach (var z in game.Zones.Values)
            {
                if (z.Kind != ZoneKind.TearGas || Game.TankAltInFlight(z))
                    continue;
                foreach (var e in game.Enemies.Values)
                {
                    if (e.Pos.DistanceTo(z.Pos) > z.Radius + e.Radius || Rnd(0f, 1f) > 0.35f)
                        continue;
                    float side = Rnd(0f, 1f) < 0.5f ? -1f : 1f;
                    V2 eye = e.Disp + new V2(side * e.Radius * 0.4f, -e.Radius * 0.3f);
                    AddPart(new Part { Kind = PSpark, Pos = eye, Vel = new V2(side * Rnd(10f, 30f), Rnd(40f, 80f)), Life = 0.35f, Max = 0.35f, Size = 1.8f, Col = TearBlue });
                    if (Rnd(0f, 1f) < 0.3f)  // a cough
                        AddPart(new Part { Kind = PDust, Pos = e.Disp + new V2(0f, -e.Radius), Vel = new V2(Rnd(-30f, 30f), -Rnd(30f, 60f)), Life = 0.4f, Max = 0.4f, Size = 4f, Col = GasWisp.WithAlpha(0.6f) });
                }
            }
        }
    }
}

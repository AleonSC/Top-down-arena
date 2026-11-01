using UnityEngine;

namespace Arena
{
    /// New looks for player weapons' shots (Docs/Mockups/projectiles.html), with Visual effects on (Juice.cs); off, every
    /// shot draws as before. Drawing only: the shots fly, hit and hurt exactly as they did.
    ///   Twin Rifle, Burst SMG   tracers: a bright head and a long thin streak
    ///   Railgun                 a heavy slug pushing rings of air ahead of it
    ///   Pistol                  a heavier round with one ring of air
    ///   Scattergun              flechettes: hair-thin needles
    ///   Prism Beam              prism shards that split red, green and blue when they hit (ShotSplitsPrism)
    ///   Arcane Missiles         little missiles with a smoke trail (EmitFromShot)
    ///   lightning               flickering, re-forking bolts with a white-hot core (DrawJuicyBolt)
    ///   Shuriken                motion-blur arcs while it spins
    ///   Fireball, boulder, Blood Bolts   embers and smoke, dust, drips (EmitFromShot)
    public partial class ArenaApp
    {
        /// The weapon that fired a player's shot, if the shot is that weapon's own (not a turret's, an ability's...).
        WeaponId? ShotWeapon(Bullet b)
        {
            Player p;
            if (b.Owner <= 0 || !game.Players.TryGetValue(b.Owner, out p))
                return null;
            var w = p.Weapon.Id;
            switch (b.Kind)
            {
                case Defs.DpsShot: return w == WeaponId.TwinRifle || w == WeaponId.BurstSmg ? w : (WeaponId?)null;
                case Defs.RailShot: return w == WeaponId.Railgun ? w : (WeaponId?)null;
                case Defs.HealerShot: return w == WeaponId.Pistol ? w : (WeaponId?)null;
                case Defs.TankShot: return w == WeaponId.Scattergun ? w : (WeaponId?)null;
                case Defs.ArcaneShot: return w == WeaponId.PrismBeam || w == WeaponId.ArcaneMissiles ? w : (WeaponId?)null;
            }
            return null;
        }

        /// DrawBigShot / DrawSmallShot: draws the shot its new way and says so (false: draw it as before).
        bool DrawWeaponShot(Bullet b, float t)
        {
            if (!Juicy)
                return false;
            var w = ShotWeapon(b);
            if (w == null)
                return false;
            var c = Defs.BulletColor(b.Kind);
            V2 dir = b.Vel.IsZero ? new V2(1f, 0f) : b.Vel.Normalized(), side = dir.Orthogonal();
            float r = b.Radius;
            switch (w.Value)
            {
                case WeaponId.TwinRifle:
                case WeaponId.BurstSmg:
                {
                    float len = Mathf.Min(46f, b.Vel.Length * 0.03f);
                    Line(b.Pos - dir * len, b.Pos, r * 1.1f, c.WithAlpha(0.35f));
                    Line(b.Pos - dir * (len * 0.6f), b.Pos, r * 0.6f, c.WithAlpha(0.85f));
                    Circle(b.Pos, r * 0.75f, Rgba.White, 8);
                    return true;
                }
                case WeaponId.Railgun:
                case WeaponId.Pistol:
                {
                    // A blunt round: a stretched body, a hot nose, and rings of air pushed ahead of it.
                    bool rail = w.Value == WeaponId.Railgun;
                    int rings = rail ? 3 : 1;
                    for (int k = 0; k < rings; k++)
                    {
                        float f = MathX.Fmod(t * 6f + k / (float)rings + b.Id * 0.13f, 1f);
                        Arc(b.Pos - dir * (f * 26f), r + 5f + f * 9f, 1.8f, c.WithAlpha(0.5f * (1f - f)), 10, dir.Angle + MathX.Pi - 1f, 2f);
                    }
                    float l = r * (rail ? 2.4f : 1.8f), h = r * 0.8f;
                    SetColor(c);
                    Quad(b.Pos + dir * l + side * (h * 0.6f), b.Pos + dir * l - side * (h * 0.6f), b.Pos - dir * l - side * h, b.Pos - dir * l + side * h);
                    Circle(b.Pos + dir * (l * 0.7f), h * 0.7f, Rgba.White.WithAlpha(0.9f), 8);
                    if (rail)
                        Line(b.Pos - dir * (l + 30f), b.Pos - dir * l, h * 0.8f, c.WithAlpha(0.3f));
                    return true;
                }
                case WeaponId.Scattergun:
                    // A flechette: a thin needle with a bright point.
                    Line(b.Pos - dir * 11f, b.Pos + dir * 3f, 1.6f, c);
                    Line(b.Pos - dir * 11f, b.Pos - dir * 7f, 3f, c.WithAlpha(0.5f));
                    Circle(b.Pos + dir * 3f, 1.5f, Rgba.White, 6);
                    return true;
                case WeaponId.PrismBeam:
                {
                    // A clear shard with a rainbow edge.
                    V2 tip = b.Pos + dir * (r + 6f), back = b.Pos - dir * (r + 2f);
                    SetColor(new Rgba(0.95f, 0.97f, 1f, 0.9f));
                    Tri(tip, back + side * (r + 1f), back - side * (r + 1f));
                    Line(tip, back + side * (r + 1f), 1.2f, new Rgba(1f, 0.35f, 0.35f, 0.9f));
                    Line(tip, back - side * (r + 1f), 1.2f, new Rgba(0.35f, 0.55f, 1f, 0.9f));
                    Line(back + side * (r + 1f), back - side * (r + 1f), 1.2f, new Rgba(0.35f, 1f, 0.45f, 0.9f));
                    return true;
                }
                case WeaponId.ArcaneMissiles:
                {
                    // A little missile: a pale body, a coloured nose, a flame at the back.
                    float l = r + 4f;
                    Line(b.Pos - dir * l, b.Pos + dir * (l * 0.4f), r * 1.1f, new Rgba(0.82f, 0.85f, 0.92f));
                    SetColor(c);
                    Tri(b.Pos + dir * (l + 3f), b.Pos + dir * (l * 0.4f) + side * (r * 0.6f), b.Pos + dir * (l * 0.4f) - side * (r * 0.6f));
                    Circle(b.Pos - dir * (l + 2f), r * (0.6f + 0.2f * Mathf.Sin(t * 40f + b.Id)), new Rgba(1f, 0.75f, 0.35f, 0.9f), 8);
                    return true;
                }
            }
            return false;
        }

        /// UpdateJuice, for each shot still flying: what it sheds as it goes (smoke, embers, dust, drips).
        void EmitFromShot(Bullet b, float dt)
        {
            if (dt <= 0f)
                return;
            V2 back = b.Vel.IsZero ? V2.Zero : -b.Vel.Normalized();
            if (b.Kind == Defs.ArcaneShot && ShotWeapon(b) == WeaponId.ArcaneMissiles)
            {
                if (Rnd(0f, 1f) < dt * 40f)
                    AddPart(new Part { Kind = PDust, Pos = b.Pos + back * (b.Radius + 5f), Vel = back * 20f + new V2(Rnd(-8f, 8f), Rnd(-8f, 8f)), Life = 0.6f, Max = 0.6f, Size = 2.4f, Col = new Rgba(0.7f, 0.68f, 0.78f) });
            }
            else if (b.Kind == Defs.FireballShot)
            {
                if (Rnd(0f, 1f) < dt * 30f)
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + new V2(Rnd(-4f, 4f), Rnd(-4f, 4f)), Vel = back * 60f + new V2(Rnd(-20f, 20f), Rnd(-35f, 0f)), Life = 0.35f, Max = 0.35f, Size = 2.4f, Col = new Rgba(1f, Rnd(0.5f, 0.9f), 0.25f) });
                if (Rnd(0f, 1f) < dt * 12f)
                    AddPart(new Part { Kind = PDust, Pos = b.Pos, Vel = new V2(Rnd(-10f, 10f), Rnd(-25f, -5f)), Life = 0.7f, Max = 0.7f, Size = 3.5f, Col = new Rgba(0.45f, 0.42f, 0.46f) });
            }
            else if (b.Kind == Defs.BoulderShot)
            {
                if (Rnd(0f, 1f) < dt * 14f)
                    AddPart(new Part { Kind = PDust, Pos = b.Pos + back * 8f, Vel = back * 25f + new V2(Rnd(-12f, 12f), Rnd(-12f, 12f)), Life = 0.6f, Max = 0.6f, Size = 3.5f, Col = new Rgba(0.6f, 0.52f, 0.45f) });
            }
            else if (b.Kind == Defs.BloodShot && b.Owner > 0)
            {
                if (Rnd(0f, 1f) < dt * 10f)
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + back * 4f, Vel = back * 20f + new V2(0f, 50f), Life = 0.35f, Max = 0.35f, Size = 1.6f, Col = new Rgba(0.8f, 0.08f, 0.15f) });
            }
        }

        /// BulletEnding: a Prism Beam shard that hits splits into red, green and blue.
        bool ShotSplitsPrism(int owner, byte kind)
        {
            Player p;
            return kind == Defs.ArcaneShot && owner > 0 && game.Players.TryGetValue(owner, out p) && p.Weapon.Id == WeaponId.PrismBeam;
        }

        /// DrawBoltFx with Visual effects on: the bolt re-forks every few frames, with a white-hot core and a side branch.
        bool DrawJuicyBolt(Effect fx, float f)
        {
            if (!Juicy)
                return false;
            V2 a = fx.Pos, b = fx.Pos2, n = (b - a).Normalized().Orthogonal();
            int seed = (int)(juiceClock * 24f) * 31 + (int)fx.Pos.X;
            V2 prev = a, fork = a;
            for (int k = 1; k <= 7; k++)
            {
                float jit = ((seed * 7 + k * 53) % 19) - 9.5f;
                V2 pt = k == 7 ? b : V2.Lerp(a, b, k / 7f) + n * jit;
                Line(prev, pt, 4f, fx.Color.WithAlpha(0.55f * (1f - f)));
                Line(prev, pt, 1.6f, Rgba.White.WithAlpha(1f - f));
                if (k == 3)
                    fork = pt;
                prev = pt;
            }
            V2 branch = fork + ((b - a).Normalized() * 0.5f + n * (((seed % 2) == 0) ? 1f : -1f)).Normalized() * Mathf.Min(40f, (b - a).Length * 0.3f);
            Line(fork, branch, 1.5f, fx.Color.WithAlpha(0.7f * (1f - f)));
            return true;
        }
    }
}

using UnityEngine;

namespace Arena
{
    /// New looks for player weapons' shots (Docs/Mockups/projectiles.html), with Visual effects on (Juice.cs); off, every
    /// shot draws as before. Drawing only: the shots fly, hit and hurt exactly as they did.
    ///   Twin Rifle, Burst SMG   tracers: a bright head and a long thin streak
    ///   Railgun                 a heavy slug pushing rings of air ahead of it
    ///   Pistol                  a heavier round with one ring of air
    ///   Scattergun              flechettes: hair-thin needles
    ///   Ice Shards (was Arcane Missiles)   faceted ice kites with a trail of glints (EmitFromShot)
    ///   lightning               flickering, re-forking bolts with a white-hot core (DrawJuicyBolt)
    ///   Shuriken                motion-blur arcs while it spins
    ///   Blood Bolts             the mockup's blood drop: a teardrop with a dotted trail
    ///   Fireball, boulder, Blood Bolts   embers and smoke, dust, drips (EmitFromShot)
    /// Each one's glow in the glow pass is the mockup's too (ShotGlow): small for flechettes, at the exhaust for missiles,
    /// none for the boulder and blood.
    public partial class ArenaApp
    {
        /// The weapon that fired a player's shot, if the shot is that weapon's own (not a turret's, an ability's...).
        WeaponId? ShotWeapon(Bullet b) => ShotWeapon(b.Kind, b.Owner);

        WeaponId? ShotWeapon(byte kind, int owner)
        {
            Player p;
            if (owner <= 0 || !game.Players.TryGetValue(owner, out p))
                return null;
            var w = p.Weapon.Id;
            switch (kind)
            {
                case Defs.DpsShot: return w == WeaponId.TwinRifle || w == WeaponId.BurstSmg ? w : (WeaponId?)null;
                case Defs.RailShot: return w == WeaponId.Railgun ? w : (WeaponId?)null;
                case Defs.HealerShot: return w == WeaponId.Pistol ? w : (WeaponId?)null;
                case Defs.TankShot: return w == WeaponId.Scattergun || w == WeaponId.RiotRepeater ? w : (WeaponId?)null;
                case Defs.FlakShell: return w == WeaponId.FlakCannon ? w : (WeaponId?)null;
                case Defs.ArcaneShot: return w == WeaponId.ArcaneMissiles ? w : (WeaponId?)null;
                case Defs.BloodShot: return w == WeaponId.BloodBolts ? w : (WeaponId?)null;
                case Defs.BoulderShot: return w == WeaponId.GolemStaff ? w : (WeaponId?)null;
                case Defs.FireflyShot: return w == WeaponId.SpriteStaff ? w : (WeaponId?)null;
            }
            return null;
        }

        /// Ice Shards (WeaponId.ArcaneMissiles, Docs/Mockups/ice-shards.html look A): frost blue, with a pale and a deep half.
        /// Defs.BulletColor(ArcaneShot) stays violet for the other arcane shots (Aeroaegis reflections and rings).
        static readonly Rgba IceCol = new Rgba(0.56f, 0.89f, 1f), IceLight = new Rgba(0.94f, 0.99f, 1f), IceDark = new Rgba(0.29f, 0.64f, 0.88f);

        /// A shot's colour: Ice Shards' frost blue, or the kind's own.
        Rgba ShotColor(byte kind, int owner) => kind == Defs.ArcaneShot && ShotWeapon(kind, owner) == WeaponId.ArcaneMissiles ? IceCol : Defs.BulletColor(kind);

        /// One ice shard (a kite: pale half, deep half) pointing along `dir`, `len` long and `wid` across.
        void IceShard(V2 at, V2 dir, float len, float wid, float alpha)
        {
            V2 side = dir.Orthogonal();
            SetColor(IceLight.WithAlpha(alpha));
            Tri(at + dir * len, at + side * wid, at - dir * (len * 0.45f));
            SetColor(IceDark.WithAlpha(alpha));
            Tri(at + dir * len, at - side * wid, at - dir * (len * 0.45f));
        }

        /// The mockup's taper(): a trail as long as the shot's last `frames` frames (never longer than it has flown), from
        /// `width` at the shot to 0.6 px and from `alpha` to nothing, colour per vertex so it's smooth.
        void TaperTrail(Bullet b, int frames, float width, Rgba c, float alpha)
        {
            float speed = b.Vel.Length, flown = Mathf.Max(0f, (b.Life0 - b.Life) * speed);
            float len = Mathf.Min(speed / 60f * frames, flown);
            if (len <= 1f)
                return;
            V2 dir = b.Vel.Normalized(), side = dir.Orthogonal(), tail = b.Pos - dir * len;
            Rgba head = c.WithAlpha(alpha), clear = c.WithAlpha(0f);
            VTri(b.Pos + side * (width / 2f), head, tail + side * 0.3f, clear, tail - side * 0.3f, clear);
            VTri(b.Pos + side * (width / 2f), head, tail - side * 0.3f, clear, b.Pos - side * (width / 2f), head);
        }

        /// The glow pass's glow for a shot: the tracers' is the mockup's (14 px at 0.8), the rest the usual soft glow.
        void ShotGlow(Bullet b)
        {
            if (IsLightCrescent(b))
            {
                DrawSunriseCrescent(b);  // all light (LightbringerFxLook.cs)
                return;
            }
            var w = ShotWeapon(b);
            var c = Defs.BulletColor(b.Kind);
            switch (w)
            {
                case WeaponId.TwinRifle:
                case WeaponId.BurstSmg:
                    GlowDisc(b.Pos, 14f, c, 0.8f);  // the mockup's tracer
                    return;
                case WeaponId.Scattergun:
                    GlowDisc(b.Pos, 7f, c, 0.6f);  // flechettes: a pin of light each, not a blob
                    return;
                case WeaponId.FlakCannon:
                {
                    float sc = 1f + FlakLift(b) / 70f;  // the shell's glow rides up the arc with it (TankGunLooks.cs)
                    GlowDisc(b.Pos + new V2(0f, -FlakLift(b)), Mathf.Max(14f, b.Radius * 4.5f) * sc, c, 0.5f);
                    return;
                }
                case WeaponId.ArcaneMissiles:
                    GlowDisc(b.Pos, 16f, IceCol, 0.7f);  // a cold glow round the shard
                    return;
                case WeaponId.GolemStaff:
                case WeaponId.BloodBolts:
                    return;  // a rock and a drop of blood don't glow (as in the mockup)
            }
            GlowDisc(b.Pos, Mathf.Max(14f, b.Radius * 4.5f), Defs.BulletColor(b.Kind), b.Owner != 0 ? 0.47f : 0.5f);
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
                    // The mockup's tracer: a streak as long as its last 10 frames (never longer than it has flown),
                    // tapering from 3 px to 0.6 and fading from 0.8 to nothing, colour per vertex so it's smooth; a white
                    // head; its glow (TracerGlow) is drawn in the glow pass.
                    TaperTrail(b, 10, 3f, c, 0.8f);
                    Circle(b.Pos, 3f, Rgba.White, 10);
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
                case WeaponId.FlakCannon:
                    DrawFlakShell(b, c);  // TankGunLooks.cs
                    return true;
                case WeaponId.RiotRepeater:
                    DrawRiotRound(b, c);
                    return true;
                case WeaponId.BloodBolts:
                {
                    // The mockup's blood drop: a dotted trail (every other frame of the last 8, shrinking and fading),
                    // a teardrop pointing back, and a pale highlight.
                    float speed = b.Vel.Length, flown = Mathf.Max(0f, (b.Life0 - b.Life) * speed);
                    for (int i = 2; i < 8; i += 2)
                    {
                        float back = speed / 60f * i, f = 1f - i / 8f;
                        if (back > flown)
                            break;
                        Circle(b.Pos - dir * back, 2.2f * f, c.WithAlpha(0.6f * f), 8);
                    }
                    SetColor(c);
                    V2 o = b.Pos + dir * 2f;
                    for (int k = 0; k < 8; k++)  // the round front: a half circle of radius 5
                    {
                        float a0 = -MathX.Pi / 2f + MathX.Pi * k / 8f, a1 = -MathX.Pi / 2f + MathX.Pi * (k + 1) / 8f;
                        Tri(o, o + (dir * Mathf.Cos(a0) + side * Mathf.Sin(a0)) * 5f, o + (dir * Mathf.Cos(a1) + side * Mathf.Sin(a1)) * 5f);
                    }
                    Tri(b.Pos - dir * 9f, o + side * 5f, o - side * 5f);  // the point, trailing behind
                    Circle(b.Pos + new V2(1f, -2f), 1.5f, new Rgba(1f, 0.6f, 0.6f), 6);
                    return true;
                }
                case WeaponId.SpriteStaff:
                {
                    // A firefly: a pale pollen trail, two flickering wings and a white-hot core.
                    TaperTrail(b, 14, 4f, c, 0.55f);
                    float flap = 0.5f + 0.5f * Mathf.Sin(t * 60f + b.Id);
                    Rgba wing = new Rgba(0.75f, 1f, 0.9f, 0.55f);
                    SetColor(wing);
                    Tri(b.Pos - dir * 1f, b.Pos - dir * 8f + side * (4f + 5f * flap), b.Pos - dir * 3f + side * 2f);
                    Tri(b.Pos - dir * 1f, b.Pos - dir * 8f - side * (4f + 5f * flap), b.Pos - dir * 3f - side * 2f);
                    Circle(b.Pos, r * 0.9f, c, 10);
                    Circle(b.Pos, 2.4f, Rgba.White, 8);
                    return true;
                }
                case WeaponId.GolemStaff:
                    return false;  // drawn by DrawBoulder (WorldShots.cs); only its glow is set here (ShotGlow: none, it's a rock)
                case WeaponId.ArcaneMissiles:
                {
                    // An ice shard: a faceted kite, pale above and deep blue below, with a white ridge down the middle.
                    IceShard(b.Pos, dir, 11f, 4f, 1f);
                    Line(b.Pos - dir * 3f, b.Pos + dir * 10f, 1f, Rgba.White.WithAlpha(0.9f));
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
                if (Rnd(0f, 1f) < dt * 40f)  // glints shed behind it, fading
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + back * (b.Radius + 4f) + new V2(Rnd(-3f, 3f), Rnd(-3f, 3f)), Vel = back * 12f + new V2(Rnd(-8f, 8f), Rnd(-8f, 8f)), Life = 0.45f, Max = 0.45f, Size = 2.2f, Col = new Rgba(0.82f, 0.96f, 1f) });
            }
            else if (b.Kind == Defs.FireballShot)
            {
                if (Rnd(0f, 1f) < dt * 30f)
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + new V2(Rnd(-4f, 4f), Rnd(-4f, 4f)), Vel = back * 60f + new V2(Rnd(-20f, 20f), Rnd(-35f, 0f)), Life = 0.35f, Max = 0.35f, Size = 2.4f, Col = new Rgba(1f, Rnd(0.5f, 0.9f), 0.25f) });
                if (Rnd(0f, 1f) < dt * 12f)
                    AddPart(new Part { Kind = PDust, Pos = b.Pos, Vel = new V2(Rnd(-10f, 10f), Rnd(-25f, -5f)), Life = 0.7f, Max = 0.7f, Size = 3.5f, Col = new Rgba(0.45f, 0.42f, 0.46f) });
            }
            else if (b.Kind == Defs.FireflyShot)
            {
                if (Rnd(0f, 1f) < dt * 45f)  // pollen glitter shed behind it
                    AddPart(new Part { Kind = PSpark, Pos = b.Pos + back * 4f + new V2(Rnd(-3f, 3f), Rnd(-3f, 3f)), Vel = back * 25f + new V2(Rnd(-14f, 14f), Rnd(-14f, 14f)), Life = 0.4f, Max = 0.4f, Size = 1.8f, Col = new Rgba(0.92f, 1f, 0.65f) });
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

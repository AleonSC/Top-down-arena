using UnityEngine;

namespace Arena
{
    /// The Gas Grenadier's overhand lob (FxKind.GasLob, WardenFaction.cs; the "Overhand Lob" mockup, option A). One effect lasts
    /// from the start of the wind-up to the landing: the ring marks where the canister will fall and fills as the wind-up goes, a
    /// dotted arc shows the lob, then the throw, then the canister flies (drawn here, with a shadow on the ground and a height off
    /// it). The grenadier reads the same clock (`GasLobOf`) to pose its arm: it reaches back and lifts the canister, the lenses glow
    /// amber, it leans back then into the swing, and the arm snaps forward. A second GasLob with Pos2.Y = -1 is the release.
    public partial class ArenaApp
    {
        /// A lob in progress for this grenadier, as (s into the wind-up, the time of the throw, the flight).
        bool GasLobOf(Enemy e, out float age, out float throwAt, out float flight)
        {
            foreach (var fx in game.Effects)
                if (fx.Kind == FxKind.GasLob && fx.Pos2.Y > 0f && (int)fx.Pos2.X == e.Id)
                {
                    age = fx.T; flight = fx.Pos2.Y; throwAt = fx.Dur - flight;
                    return true;
                }
            age = throwAt = flight = 0f;
            return false;
        }

        static readonly Rgba GasLobGreen = new Rgba(0.62f, 0.92f, 0.42f);
        static readonly Rgba GasLobAmber = new Rgba(1f, 0.71f, 0.28f);
        const float GasLobArc = 80f, GasLobLift = 9f;

        static float GasEase(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// The hand in body radii (forward, sideways) and its canister's height, `age` s into the wind-up.
        static void GasHand(float age, float throwAt, float flight, out float u, out float v, out float z, out bool has)
        {
            const float restU = 0.3f, restV = 0.95f, holdU = -0.9f, holdV = 1.15f, extU = 1.9f, extV = 0.5f;
            float swing = Mathf.Min(Game.GrenadierSwing, throwAt), tellEnd = throwAt - swing;
            has = true;
            z = 0f;
            if (age <= 0f || throwAt <= 0f && age < 0f)
            {
                u = restU; v = restV;
            }
            else if (age < tellEnd)
            {
                float e = GasEase(age / tellEnd);
                u = Mathf.Lerp(restU, holdU, e); v = Mathf.Lerp(restV, holdV, e); z = GasLobLift * e;
            }
            else if (age < throwAt)
            {
                float p = (age - tellEnd) / swing, e = p * p;
                u = Mathf.Lerp(holdU, extU, e); v = Mathf.Lerp(holdV, extV, e); z = GasLobLift * (1f - GasEase((p - 0.35f) / 0.65f));
            }
            else
            {
                float e = GasEase((age - throwAt) / Mathf.Max(0.01f, flight) * 2.2f);
                u = Mathf.Lerp(extU, restU, e); v = Mathf.Lerp(extV, restV, e);
                has = (age - throwAt) / Mathf.Max(0.01f, flight) > 0.55f;  // a new canister comes out once the old one has landed
            }
        }

        /// Where the hand is on the screen (before its height), for an enemy standing at `pos` facing `f`.
        static V2 GasHandAt(V2 pos, V2 f, float r, float u, float v) => pos + f * (u * r) + f.Orthogonal() * (v * r);

        void DrawGasLobFx(Effect fx, float t)
        {
            if (!game.Enemies.TryGetValue((int)fx.Pos2.X, out Enemy e))
                e = null;
            if (fx.Pos2.Y < 0f)  // the release: a puff off the hand
            {
                if (e == null)
                    return;
                V2 fr = V2.FromAngle(e.Facing);
                float k = fx.T / fx.Dur;
                V2 hp = GasHandAt(e.Disp, fr, e.Radius, 1.9f, 0.5f);
                if (e.Type == EnemyType.RockHurler)
                    Arc(e.Disp + fr * e.Radius, 6f + 14f * k, 2.5f, new Rgba(0.78f, 0.62f, 0.45f, 1f - k), 14);  // a puff of grit as the boulder leaves
                else if (e.Type == EnemyType.HoneyLobber)
                    Arc(e.Disp + fr * (e.Radius * 0.6f), 6f + 14f * k, 2.5f, new Rgba(1f, 0.8f, 0.3f, 1f - k), 14);  // a puff off its swollen abdomen
                else if (e.Type == EnemyType.DuskLobber)
                    Arc(e.Disp + fr * (e.Radius * 1.2f), 6f + 14f * k, 2.5f, new Rgba(0.61f, 0.48f, 1f, 1f - k), 14);  // a wisp of dusk
                else
                    Arc(hp, 6f + 14f * k, 2.5f, new Rgba(0.86f, 1f, 0.75f, 1f - k), 14);
                return;
            }
            float flight = Mathf.Max(0.01f, fx.Pos2.Y), age = fx.T, throwAt = fx.Dur - flight, R = fx.Radius;
            V2 land = fx.Pos;
            V2 face = e != null ? V2.FromAngle(e.Facing) : (land - fx.Pos).Normalized();
            float r = e != null ? e.Radius : 14f;
            V2 body = e != null ? e.Disp : land;
            if (e != null && e.Type == EnemyType.RockHurler)
            {
                DrawHurlerLobFx(e, fx, age, throwAt, flight, land, R, t);  // a boulder (QuarryLook.cs)
                return;
            }
            if (e != null && e.Type == EnemyType.DuskLobber)
            {
                DrawDuskLobFx(e, age, throwAt, flight, land, R, t);  // a ball of night (PenumbralLook.cs)
                return;
            }
            if (e != null && e.Type == EnemyType.BileSpitter)
            {
                DrawBileLobFx(e, fx, age, throwAt, flight, land, R);  // a glob of bile (LarderLook.cs)
                return;
            }
            if (e != null && e.Type == EnemyType.HoneyLobber)
            {
                DrawHoneyLobFx(e, fx, age, throwAt, flight, land, R);  // the mockup's red ring and honey glob (HiveLook.cs)
                return;
            }

            // The ring: amber creeping in as the throw gets close, filling from the middle outwards.
            if (age < throwAt + flight)
            {
                float tellP = throwAt > 0f ? Mathf.Clamp01(age / Mathf.Max(0.01f, throwAt - Mathf.Min(Game.GrenadierSwing, throwAt))) : 1f;
                float a = throwAt > 0f && age < throwAt ? GasEase(age / 0.17f) : 1f;
                Rgba col = GasLobGreen.Mix(GasLobAmber, GasEase(tellP * 1.2f));
                Circle(land, R, col.WithAlpha(0.1f * a), 32);
                Circle(land, R * GasEase(tellP), col.WithAlpha(0.22f * a), 32);
                Arc(land, R, 2.5f, col.WithAlpha(0.9f * a), 32);
                Line(land - new V2(5f, 0f), land + new V2(5f, 0f), 2f, col.WithAlpha(0.9f * a));
                Line(land - new V2(0f, 5f), land + new V2(0f, 5f), 2f, col.WithAlpha(0.9f * a));
                // The dotted arc of the lob, drawn out as the wind-up goes, fading as the arm swings.
                if (e != null && throwAt > 0f && age < throwAt)
                {
                    V2 rp = GasHandAt(body, face, r, 1.9f, 0.5f);
                    float drawn = GasEase(tellP * 1.3f), fade = a * Mathf.Clamp01((throwAt - age) / Mathf.Min(Game.GrenadierSwing, throwAt) * 1.5f);
                    for (float u = 0.05f; u < drawn; u += 0.06f)
                        Circle(V2.Lerp(rp, land, u) - new V2(0f, GasLobArc * 4f * u * (1f - u) * 0.9f), 1.6f, GasLobAmber.WithAlpha(0.8f * fade), 6);
                }
            }

            // The canister in the air: a ground shadow, a trail, and the canister up off the ground, tumbling.
            if (e != null && age >= throwAt && age < throwAt + flight)
            {
                float u = (age - throwAt) / flight;
                V2 rp = GasHandAt(body, face, r, 1.9f, 0.5f);
                V2 ground = V2.Lerp(rp, land, u);
                float z = Mathf.Lerp(GasLobLift, 0f, u) + GasLobArc * 4f * u * (1f - u);
                for (int k = 5; k >= 1; k--)
                {
                    float ut = Mathf.Max(0f, u - k * 0.04f), zt = Mathf.Lerp(GasLobLift, 0f, ut) + GasLobArc * 4f * ut * (1f - ut);
                    Circle(V2.Lerp(rp, land, ut) - new V2(0f, zt), 2.2f + k * 0.45f, GasLobGreen.WithAlpha(0.22f * (1f - k / 6f)), 8);
                }
                Circle(ground + new V2(0f, 1f), 3.6f * (1f - z / 300f), new Rgba(0f, 0f, 0f, 0.35f - z / 500f), 10);
                float sc = 1f + z / 90f;
                V2 c = ground - new V2(0f, z);
                Circle(c, 3.6f * sc, new Rgba(0.7f, 0.95f, 0.45f), 10);
                V2 tum = V2.FromAngle(u * 14f);
                Line(c + tum * (1.4f * sc), c + tum * (4.4f * sc), 2f * sc, new Rgba(0.12f, 0.16f, 0.08f, 0.9f));
            }
        }

        /// The Gas Grenadier's arm and canister while a lob is under way (read from its effect), else nothing: the caller draws it
        /// at rest. Returns true when it drew the arm.
        bool DrawGrenadierLob(Enemy e, V2 pos, float r, float t)
        {
            if (!GasLobOf(e, out float gAge, out float gThrowAt, out float gFlight))
                return false;
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            GasHand(gAge, gThrowAt, gFlight, out float u, out float v, out float z, out bool has);
            float swing = Mathf.Min(Game.GrenadierSwing, gThrowAt), tellEnd = gThrowAt - swing;
            float lean = gAge < tellEnd ? -2f * GasEase(gAge / Mathf.Max(0.01f, tellEnd))
                : gAge < gThrowAt ? Mathf.Lerp(-2f, 3f, GasEase((gAge - tellEnd) / swing))
                : Mathf.Lerp(3f, 0f, GasEase((gAge - gThrowAt) / Mathf.Max(0.01f, gFlight) * 2f));
            V2 b = pos + f * lean;
            float glow = gAge < tellEnd ? GasEase(gAge / Mathf.Max(0.01f, tellEnd)) : gAge < gThrowAt ? 1f : 0f;
            Rgba lens = new Rgba(0.15f, 0.2f, 0.12f, 0.95f).Mix(GasLobAmber, glow);
            V2 l1 = b + f * (r * 0.25f) - side * (r * 0.32f), l2 = b + f * (r * 0.25f) + side * (r * 0.32f);
            Circle(l1, r * 0.22f, lens, 10);
            Circle(l2, r * 0.22f, lens, 10);
            if (glow > 0.3f)
            {
                Circle(l1, r * 0.5f, GasLobAmber.WithAlpha(0.25f * glow), 10);
                Circle(l2, r * 0.5f, GasLobAmber.WithAlpha(0.25f * glow), 10);
            }
            Circle(b + f * (r * 0.62f), r * 0.2f, new Rgba(0.35f, 0.4f, 0.3f), 8);  // the filter
            V2 shoulder = b + side * (r * 0.8f), hand = GasHandAt(b, f, r, u, v) - new V2(0f, z);
            Line(shoulder, hand, 4f, new Rgba(0.35f, 0.55f, 0.23f));
            Circle(hand, 2.6f, new Rgba(0.47f, 0.7f, 0.35f), 8);
            if (has)
            {
                float sc = 1f + z / 90f;
                Circle(hand + new V2(0f, -1f), 3.6f * sc, new Rgba(0.7f, 0.95f, 0.45f).Mix(GasLobAmber, 0.6f * glow), 10);
                Line(hand + new V2(-1f, -2f - 3f * sc), hand + new V2(1f, -2f - 3f * sc), 2f, new Rgba(0.12f, 0.16f, 0.08f, 0.9f));
            }
            return true;
        }
    }
}

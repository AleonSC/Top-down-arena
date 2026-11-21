using UnityEngine;

namespace Arena
{
    /// The Warden's officers glowing (Visual effects on; looks 1 "Colour halo" and 2 "Gear lights" of the glow mockups),
    /// all in the additive glow pass (DrawOfficerGlow, from Juice.cs DrawGlowPass):
    ///   - a breathing halo in the officer's own colour, in place of the quiet glow other enemies get (EnemyPresenceLook.cs);
    ///   - its kit lit: the Guard's baton tip and cap badge, the Riot Shield's face, the Gas Grenadier's mask lenses and
    ///     canister, the Taser Guard's prongs crackling, the Security Guard's pistol light; and a blinking radio LED.
    /// The prisoners (Rioter, Thug, Shiv) and the Turnkey get the halo too, no kit; the Thug's is a faint red that grows
    /// bigger, brighter and quicker as it enrages (Game.BruteRage: the health it has lost).
    /// The kit's spots follow the bodies' own drawing (OfficerLook.cs, SecurityLook.cs, WardenLook.cs).
    public partial class ArenaApp
    {
        const float OfficerHaloR = 4f, OfficerHaloA = 0.34f, OfficerCoreR = 1.9f, OfficerCoreA = 0.3f;  // x the body's radius

        static readonly Rgba BatonLight = new Rgba(1f, 0.7f, 0.25f), BadgeLight = new Rgba(1f, 0.85f, 0.35f),
            ShieldLight = new Rgba(0.55f, 0.75f, 1f), LensLight = new Rgba(0.45f, 1f, 0.3f), LensHot = new Rgba(0.85f, 1f, 0.75f),
            ProngLight = new Rgba(0.45f, 0.8f, 1f), PistolLight = new Rgba(1f, 0.95f, 0.8f), RadioLed = new Rgba(1f, 0.2f, 0.2f);

        /// The five officers with a kit that lights up.
        static bool IsOfficer(EnemyType ty) =>
            ty == EnemyType.Guard || ty == EnemyType.RiotShield || ty == EnemyType.GasGrenadier || ty == EnemyType.TaserGuard ||
            ty == EnemyType.SecurityGuard;

        /// The Warden's faction members with a halo: the officers, the prisoners and the Turnkey.
        static bool HasWardenHalo(EnemyType ty) =>
            IsOfficer(ty) || ty == EnemyType.Rioter || ty == EnemyType.Thug || ty == EnemyType.Shiv || ty == EnemyType.Turnkey;

        static readonly Rgba ThugRage = new Rgba(1f, 0.16f, 0.12f);

        /// DrawGlowPass: every officer's halo and kit, the prisoners' and Turnkey's halos.
        void DrawOfficerGlow()
        {
            float t = game.AnimT;
            foreach (var e in game.Enemies.Values)
            {
                if (!HasWardenHalo(e.Type) || !HasPresence(e))
                    continue;
                V2 pos = e.Disp - new V2(0f, BsLiftOf(e));
                float r = e.Radius * JuiceScale(e);
                if (e.Type == EnemyType.Thug)
                {
                    float rage = Game.BruteRage(e);  // 0 fresh, 1 near death
                    float beat = 0.85f + 0.15f * Mathf.Sin(t * (2.4f + 6f * rage) + e.Id * 1.3f);
                    GlowDisc(pos, r * (3f + 2.6f * rage), ThugRage, (0.14f + 0.36f * rage) * beat);
                    GlowDisc(pos, r * (1.6f + 0.6f * rage), ThugRage, 0.12f + 0.25f * rage);
                    continue;
                }
                float breath = 0.85f + 0.15f * Mathf.Sin(t * 2.4f + e.Id * 1.3f);
                GlowDisc(pos, r * OfficerHaloR, e.Def.Color, OfficerHaloA * breath);
                GlowDisc(pos, r * OfficerCoreR, e.Def.Color, OfficerCoreA);
                if (IsOfficer(e.Type))
                    OfficerKitGlow(e, pos, r, t);
            }
        }

        void OfficerKitGlow(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            float pulse = 0.8f + 0.2f * Mathf.Sin(t * 5f + e.Id);
            switch (e.Type)
            {
                case EnemyType.Guard:
                {
                    float wind, swing, ba = GuardBatonAngle(e, t, out wind, out swing);  // where DrawGuardBody holds it
                    V2 bd = V2.FromAngle(ba);
                    V2 hand = pos + bd * (r * 1.05f);
                    if (e.LarderStage == Game.LarderIdle)
                        hand = V2.Lerp(pos + side * (r * 1.0f) + f * (r * 0.95f), hand, GuardReady(e));
                    V2 tip = hand + bd * (r * 1.6f);
                    GlowDisc(V2.Lerp(hand, tip, 0.5f), 18f, BatonLight, 0.22f);  // along the stick
                    GlowDisc(tip, 26f, BatonLight, 0.55f * pulse);
                    GlowDisc(tip, 6f, Rgba.White, 0.8f);
                    GlowDisc(pos + f * (r * 0.55f), 14f, BadgeLight, 0.5f);
                    break;
                }
                case EnemyType.RiotShield:
                {
                    byte st = Game.RiotState(e);
                    if (Game.RiotCrack(e) >= Game.RiotCracked)
                        break;  // the shield's gone
                    bool low = st == Game.RiotLow;
                    float half = low ? r * 0.8f : r * 1.6f, k = low ? 0.5f : 1f;
                    V2 c = pos + f * (r + (st == Game.RiotLunging ? 11f : 7f)) + (low ? side * (r * 0.7f) : V2.Zero);  // as DrawRiotShield
                    SpillLine(c + f * 9f - side * half, c + f * 9f + side * half, 30f, ShieldLight, 0.4f * k);
                    SpillLine(c + f * 9f - side * half, c + f * 9f + side * half, 6f, Rgba.White, 0.45f * k);
                    break;
                }
                case EnemyType.GasGrenadier:
                {
                    for (int s = -1; s <= 1; s += 2)
                    {
                        V2 l = pos + f * (r * 0.25f) + side * (s * r * 0.32f);  // as DrawGrenadier's lenses
                        GlowDisc(l, 16f, LensLight, 0.6f * pulse);
                        GlowDisc(l, 4f, LensHot, 0.9f);
                    }
                    if (!GasLobOf(e, out _, out _, out _))  // the canister in hand (mid-lob, GasLobLook.cs moves it)
                        GlowDisc(pos + side * (r * 1.05f) + f * (r * 0.75f), 30f, LensLight, 0.35f * pulse);
                    GlowDisc(pos, r * 2.6f, LensLight, 0.12f);
                    break;
                }
                case EnemyType.TaserGuard:
                {
                    V2 muzzle = pos + f * 29f;  // as DrawTaserGuardDetails
                    GlowDisc(muzzle, 40f, ProngLight, 0.45f * pulse);
                    GlowDisc(muzzle, 12f, Rgba.White, 0.85f);
                    for (int i = 0; i < 3; i++)  // a crackle round the prongs
                    {
                        float a0 = t * 9f + i * 2.1f + e.Id;
                        Line(muzzle + V2.FromAngle(a0) * 6f, muzzle + V2.FromAngle(a0 + 0.7f) * 14f, 1.4f, ProngLight.WithAlpha(0.8f));
                    }
                    break;
                }
                case EnemyType.SecurityGuard:
                {
                    if (e.Phase == 0)
                        break;  // reloading: the light's down with the pistol
                    float k = r / 15f, gx = e.Winding ? 20f : 16.75f;  // the muzzle, as DrawSecurityBody's G(15, 0)
                    V2 gun = pos + f * ((gx + 15f) * k);
                    SpillCone(gun, e.Facing, 0.2f, 190f, PistolLight, 0.2f);
                    GlowDisc(gun, 10f, Rgba.White, 0.9f);
                    break;
                }
            }
            bool led = Mathf.Repeat(t + e.Id * 0.3f, 1f) < 0.3f;
            GlowDisc(pos - side * (r * 0.75f) - f * (r * 0.2f), 7f, RadioLed, led ? 0.7f : 0.21f);  // the radio on its shoulder
        }

        /// A band of light along a line, brightest on it and gone `w` either side.
        void SpillLine(V2 a, V2 b, float w, Rgba col, float alpha)
        {
            V2 n = (b - a).Normalized().Orthogonal() * w;
            Rgba on = col.WithAlpha(alpha), off = col.WithAlpha(0f);
            VTri(a + n, off, b + n, off, b, on);
            VTri(a + n, off, b, on, a, on);
            VTri(a - n, off, b - n, off, b, on);
            VTri(a - n, off, b, on, a, on);
        }

        /// A cone of light from `from` along `ang`, `half` radians each side, fading out over `len`.
        void SpillCone(V2 from, float ang, float half, float len, Rgba col, float alpha)
        {
            const int segs = 8;
            Rgba z = col.WithAlpha(0f), apex = col.WithAlpha(alpha);
            for (int i = 0; i < segs; i++)
            {
                float a0 = ang - half + 2f * half * i / segs, a1 = ang - half + 2f * half * (i + 1) / segs;
                float e0 = 1f - Mathf.Abs(i / (float)segs * 2f - 1f), e1 = 1f - Mathf.Abs((i + 1) / (float)segs * 2f - 1f);
                V2 m0 = from + V2.FromAngle(a0) * (len * 0.5f), m1 = from + V2.FromAngle(a1) * (len * 0.5f);
                V2 p0 = from + V2.FromAngle(a0) * len, p1 = from + V2.FromAngle(a1) * len;
                Rgba c0 = col.WithAlpha(alpha * 0.55f * (0.3f + 0.7f * e0)), c1 = col.WithAlpha(alpha * 0.55f * (0.3f + 0.7f * e1));
                VTri(m0, c0, m1, c1, from, apex);
                VTri(p0, z, p1, z, m1, c1);
                VTri(p0, z, m1, c1, m0, c0);
            }
        }
    }
}

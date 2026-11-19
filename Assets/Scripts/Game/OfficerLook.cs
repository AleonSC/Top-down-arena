using UnityEngine;

namespace Arena
{
    /// The Warden's officers in the smooth Rioter style (WardenLook.cs), arms holding each weapon: the shared torso, peaked
    /// cap, arm and stick, then the Guard (side-handle baton, swung by GuardSwing.cs), the Riot Shield (both hands on the
    /// shield's grips; a baton once it breaks), the Gas Grenadier (mask, bandolier, a canister held up; its lob is
    /// GasLobLook.cs) and the Taser Guard (the taser in both hands; its bolt is TaserLook.cs). The Security Guard's body
    /// and pistol are in SecurityLook.cs.
    public partial class ArenaApp
    {
        static readonly Rgba OfficerGlove = new Rgba(0.12f, 0.13f, 0.17f), OfficerSkin = new Rgba(0.9f, 0.72f, 0.56f);
        const float OffInk = 1.3f;

        /// An arm from the shoulder to the hand: an inked sleeve and a fist (bare or gloved), all round-ended.
        void OfficerArm(V2 shoulder, V2 hand, float w, Rgba sleeve, Rgba fist)
        {
            Cap(shoulder, hand, w + OffInk * 2f, GuardInk);
            Cap(shoulder, shoulder + (hand - shoulder) * 0.7f, w, sleeve.Mix(GuardInk, 0.22f));  // a shade darker than the shirt, so it reads
            Circle(hand, w * 0.62f + OffInk, GuardInk, 20);
            Circle(hand, w * 0.62f, fist, 20);
        }

        void OfficerTorso(V2 pos, float r, float face, Rgba shirt)
        {
            V2.Axes(face, out V2 f, out V2 s);
            Oval(pos, r * 1.0f + OffInk, r * 1.12f + OffInk, face, GuardInk);
            Oval(pos, r * 1.0f, r * 1.12f, face, shirt);
            Cap(pos - s * r * 0.92f, pos + s * r * 0.92f, 2.6f, new Rgba(0.28f, 0.2f, 0.12f));  // the duty belt
            Circle(pos - s * r * 0.72f - f * r * 0.1f, 2.6f, new Rgba(0.1f, 0.1f, 0.12f), 16);  // the radio
            Cap(pos - s * r * 0.72f - f * r * 0.1f, pos - s * r * 0.72f - f * (r * 0.1f + 5f), 1.2f, new Rgba(0.1f, 0.1f, 0.12f));
        }

        /// The peaked cap: a rounded peak out front under the crown, the badge on the crown's front.
        void OfficerCap(V2 pos, float r, float face, Rgba crown, Rgba badge)
        {
            V2 f = V2.FromAngle(face);
            Oval(pos + f * r * 0.32f, r * 0.5f + OffInk, r * 0.52f + OffInk, face, GuardInk);
            Oval(pos + f * r * 0.32f, r * 0.5f, r * 0.52f, face, new Rgba(0.06f, 0.07f, 0.1f));  // the peak
            Circle(pos, r * 0.6f + OffInk, GuardInk, 28);
            Circle(pos, r * 0.6f, crown, 28);
            Circle(pos - f * r * 0.12f, r * 0.4f, crown.Mix(Rgba.White, 0.08f), 24);  // the crown's top
            Circle(pos + f * r * 0.38f, 2.3f, badge, 12);
        }

        void GuardStick(V2 a, V2 b, float w, Rgba shaft)
        {
            V2 n = (b - a).Normalized().Orthogonal();
            Cap(a, b, w + 2.4f, GuardInk);
            Cap(a, b, w, shaft);
            Line(a + n * (w * 0.22f), b + n * (w * 0.22f), 1f, new Rgba(0.75f, 0.8f, 0.92f, 0.7f));
        }

        void OfficerFlash(Enemy e, V2 pos, float r)
        {
            if (e.Flash > 0f || JuiceFlash(e))
                Oval(pos, r * 1.0f, r * 1.12f, e.Facing, Rgba.White.WithAlpha(0.8f * HitFlashMix(e)));
        }

        /// The Guard, a corrections officer: khaki shirt, peaked cap with a gold badge, the nightstick in its right hand.
        // --- Guard -------------------------------------------------------------------------------------------------

        void DrawGuardBody(Enemy e, V2 pos, float r, Rgba col, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 s);
            float wind, swing, ba = GuardBatonAngle(e, t, out wind, out swing);
            V2 bd = V2.FromAngle(ba);
            V2 hand = e.LarderStage != Game.LarderIdle ? pos + bd * (r * 1.05f) : pos + s * (r * 1.0f) + f * (r * 0.95f);
            V2 tip = hand + bd * (r * 1.6f);
            var shirt = GuardShirt.Mix(col, 0.12f);
            OfficerTorso(pos, r, e.Facing, shirt);
            OfficerArm(pos - s * r * 0.9f, pos - s * r * 0.85f + f * r * 1.0f, 4.4f, shirt, OfficerSkin);  // the free arm
            OfficerArm(pos + s * r * 0.9f, hand, 4.4f, shirt, OfficerSkin);
            OfficerCap(pos, r, e.Facing, GuardCap, GuardBadge);
            GuardStick(hand - bd * 4f, tip, 3.6f, GuardBaton);
            GuardStick(hand + bd * 1.5f, hand + bd * 1.5f - bd.Orthogonal() * 6f, 3f, GuardBaton);  // the side handle
            Circle(hand, 3.6f * 0.62f, OfficerSkin, 20);  // the fist round the stick
            if (wind > 0.6f)
                Circle(tip, 3.2f, Rgba.White.WithAlpha(0.85f), 12);
        }

        // --- Riot Shield ---------------------------------------------------------------------------------------------

        static readonly Rgba RiotFatigues = new Rgba(0.3f, 0.4f, 0.62f);

        void DrawRiotShieldBody(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            float rr = r * 0.85f;
            OfficerTorso(pos, rr, e.Facing, RiotFatigues);
            V2 back = pos - f * (rr * 0.66f);
            Cap(back - side * (rr * 0.5f), back + side * (rr * 0.5f), 3f, new Rgba(0.95f, 0.95f, 0.88f, 0.9f));
            Circle(pos, r * 0.55f + OffInk, GuardInk, 28);
            Circle(pos, r * 0.55f, new Rgba(0.12f, 0.14f, 0.2f), 28);  // the helmet
            Circle(pos - f * r * 0.12f - side * r * 0.12f, r * 0.2f, new Rgba(1f, 1f, 1f, 0.1f), 16);
            Arc(pos, r * 0.45f, 3f, new Rgba(0.65f, 0.85f, 1f, 0.85f), 24, e.Facing - 1.0f, 2.0f);   // its visor
            Arc(pos, r * 0.45f, 1.1f, new Rgba(1f, 1f, 1f, 0.8f), 12, e.Facing - 0.75f, 0.55f);
            if (e.Flash > 0f || JuiceFlash(e))
                Oval(pos, rr * 1.0f, rr * 1.12f, e.Facing, Rgba.White.WithAlpha(0.8f * HitFlashMix(e)));
        }

        void DrawRiotShield(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            byte st = Game.RiotState(e), crack = Game.RiotCrack(e);
            float rr = r * 0.85f;
            if (crack >= Game.RiotCracked)
            {
                V2 grip = pos + side * (r * 1.0f) + f * (r * 0.3f), tip = grip + V2.FromAngle(e.Facing + 0.9f * Mathf.Sin(t * 18f + e.Id)) * (r + 12f);
                OfficerArm(pos + side * rr * 0.9f, grip, 4.4f, RiotFatigues, OfficerGlove);
                OfficerArm(pos - side * rr * 0.9f, pos - side * rr * 0.7f + f * rr * 0.8f, 4.4f, RiotFatigues, OfficerGlove);
                GuardStick(grip, tip, 3.6f, GuardBaton);
                Circle(grip, 3.6f * 0.62f, OfficerGlove, 20);
                if (st == Game.RiotStaggered)
                    for (int k = 0; k < 3; k++)
                        Circle(pos + V2.FromAngle(t * 6f + k * MathX.Tau / 3f) * (r * 0.7f) + new V2(0f, -r * 0.4f), 2f, new Rgba(1f, 0.95f, 0.6f, 0.9f), 10);
                return;
            }
            bool low = st == Game.RiotLow;
            float half = low ? r * 0.8f : r * 1.6f, depth = 9f;
            V2 c = pos + f * (r + (st == Game.RiotLunging ? 11f : 7f)) + (low ? side * (r * 0.7f) : V2.Zero);
            // the arms reach to the grips on the shield's back
            for (int k = -1; k <= 1; k += 2)
                OfficerArm(pos + side * (k * rr * 0.9f), c + side * (k * half * 0.38f) - f * 1f, 4.4f, RiotFatigues, OfficerGlove);
            float wear = crack / 4f;
            Rgba face = RiotSteel.WithAlpha(low ? 0.55f : 0.92f - 0.18f * wear);
            if (st == Game.RiotLunging)
                face = face.Mix(Rgba.White, 0.65f);
            var edge = new Rgba(0.1f, 0.13f, 0.24f, 0.95f);
            V2 mid = c + f * (depth / 2f);
            Cap(mid - side * half, mid + side * half, depth + 3f, edge);  // a slab with rounded ends
            Cap(mid - side * half, mid + side * half, depth, face);
            Cap(mid + f * (depth * 0.3f) - side * half, mid + f * (depth * 0.3f) + side * half, 2f, new Rgba(0.92f, 0.96f, 1f, 0.95f));  // the bright front rim
            Cap(mid - side * (half * 0.3f), mid - side * (half * 0.05f), 3f, new Rgba(0.18f, 0.26f, 0.42f, 0.9f));  // the window
            Cap(mid + side * (half * 0.15f), mid + side * (half * 0.55f), 2f, new Rgba(1f, 1f, 1f, 0.55f));
            for (int k = 0; k < crack * 2; k++)
            {
                float u = -0.85f + 1.7f * (((e.Id * 37 + k * 61) % 97) / 96f);
                V2 p0 = c + side * (half * u) + f * depth, p1 = p0 - f * (depth * 0.55f) + side * (k % 2 == 0 ? 4f : -4f), p2 = p1 - f * (depth * 0.45f) + side * (k % 2 == 0 ? -2f : 2f);
                Cap(p0, p1, 1.4f, edge);
                Cap(p1, p2, 1.4f, edge);
            }
        }

        // --- Gas Grenadier -------------------------------------------------------------------------------------------

        static readonly Rgba GrenOlive = new Rgba(0.52f, 0.58f, 0.36f), GasGreen = new Rgba(0.7f, 0.95f, 0.45f);

        void DrawGrenadierBody(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            float rr = r * 0.92f;
            OfficerTorso(pos, rr, e.Facing, GrenOlive);
            V2 b0 = pos - side * (rr * 0.85f) - f * (rr * 0.55f), b1 = pos + side * (rr * 0.85f) + f * (rr * 0.55f);
            Cap(b0, b1, 3.6f, new Rgba(0.45f, 0.33f, 0.18f));  // the bandolier
            for (int k = -1; k <= 1; k += 2)
            {
                V2 c = pos + (b1 - pos) * (0.62f * k);
                Circle(c, 2.1f + OffInk, GuardInk, 14);
                Circle(c, 2.1f, GasGreen, 14);
            }
            Circle(pos, r * 0.6f + OffInk, GuardInk, 28);
            Circle(pos, r * 0.6f, new Rgba(0.4f, 0.43f, 0.4f), 28);  // the rubber hood
            if (e.Flash > 0f || JuiceFlash(e))
                Oval(pos, rr, rr * 1.12f, e.Facing, Rgba.White.WithAlpha(0.8f * HitFlashMix(e)));
        }

        void DrawGrenadier(Enemy e, V2 pos, float r, float t)
        {
            if (DrawGrenadierLob(e, pos, r, t))
                return;
            V2.Axes(e.Facing, out V2 f, out V2 side);
            float rr = r * 0.92f;
            V2 can = pos + side * (r * 1.05f) + f * (r * 0.75f);
            OfficerArm(pos - side * rr * 0.9f, pos - side * rr * 0.75f + f * rr * 0.75f, 4.2f, GrenOlive, OfficerGlove);
            OfficerArm(pos + side * rr * 0.9f, can - f * 2f, 4.2f, GrenOlive, OfficerGlove);
            // the canister held up: a green can with a dark cap and the pin's ring
            Cap(can - side * 3.2f, can + side * 3.2f, 7.4f + 2.4f, GuardInk);
            Cap(can - side * 3.2f, can + side * 3.2f, 7.4f, GasGreen);
            Cap(can - side * 3.2f, can - side * 1.6f, 7.4f, new Rgba(0.25f, 0.3f, 0.2f));
            Arc(can - side * 5.6f + f * 2.5f, 2.2f, 1.1f, new Rgba(0.85f, 0.85f, 0.8f), 14);
            var lens = new Rgba(0.15f, 0.2f, 0.12f, 0.95f);
            for (int k = -1; k <= 1; k += 2)
            {
                V2 l = pos + f * (r * 0.25f) + side * (k * r * 0.32f);
                Circle(l, r * 0.22f + 1f, GuardInk, 20);
                Circle(l, r * 0.22f, lens, 20);
                Circle(l - f * 1f - side * 1f, r * 0.07f, new Rgba(1f, 1f, 1f, 0.35f), 10);
            }
            Circle(pos + f * (r * 0.62f), r * 0.2f + 1f, GuardInk, 20);
            Circle(pos + f * (r * 0.62f), r * 0.2f, new Rgba(0.35f, 0.4f, 0.3f), 20);  // the filter
            float puff = MathX.Fmod(t * 0.8f + e.Id * 0.37f, 1f);
            Circle(can + new V2(0f, -6f - puff * 10f), 2f + puff * 3f, new Rgba(0.65f, 0.95f, 0.45f, 0.5f * (1f - puff)), 16);
        }

        // --- Taser Guard ---------------------------------------------------------------------------------------------

        static readonly Rgba TaserShirt = new Rgba(0.33f, 0.37f, 0.46f);

        void DrawTaserGuardBody(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            OfficerTorso(pos, r, e.Facing, TaserShirt);
            V2 back = pos - f * (r * 0.72f);
            Cap(back - side * (r * 0.55f), back + side * (r * 0.55f), 3f, new Rgba(1f, 0.84f, 0.2f, 0.95f));  // the hi-vis band
            OfficerFlash(e, pos, r);
        }

        void DrawTaserGuardDetails(Enemy e, V2 pos, float r, float t)
        {
            V2.Axes(e.Facing, out V2 f, out V2 side);
            V2 grip = pos + f * 15f;
            // both hands on the taser, held out front
            OfficerArm(pos + side * r * 0.9f, grip + side * 2.2f, 4.2f, TaserShirt, OfficerGlove);
            OfficerArm(pos - side * r * 0.9f, grip - side * 2.2f, 4.2f, TaserShirt, OfficerGlove);
            OfficerCap(pos, r, e.Facing, new Rgba(0.11f, 0.13f, 0.19f), TaserCharge);
            Cap(grip, pos + f * 25f, 6.4f + 2.4f, GuardInk);
            Cap(grip, pos + f * 25f, 6.4f, new Rgba(0.81f, 0.85f, 0.93f));    // the taser
            Cap(pos + f * 21f - side * 3.2f, pos + f * 21f + side * 3.2f, 1.6f, new Rgba(1f, 0.84f, 0.2f));  // its yellow band
            Circle(grip, 3.6f * 0.62f, OfficerGlove, 20);
            for (int k = -1; k <= 1; k += 2)
            {
                Cap(pos + f * 26f + side * (k * 2.4f), pos + f * 29f + side * (k * 2.8f), 1.5f, ForkWhite);  // its prongs
                Circle(pos + f * 29f + side * (k * 2.8f), 1.1f, ForkWhite, 8);
            }
            if (!e.Winding)
                return;
            float k2 = 1f;
            if (aimSince.TryGetValue(e.Id, out float since))
                k2 = MathX.Clamp01((t - since) / Game.WindUpOf(e.Type));
            V2 muzzle = pos + f * 29f, gap = side * 4f;
            TaserHalo(muzzle, 8f + 16f * k2, TaserCharge, 0.55f * k2);
            TaserBoltLine(muzzle + gap, muzzle - gap, 1f + 4f * k2, Mathf.Floor(t * 30f), 3, TaserCharge, 1.3f, 0.5f + 0.5f * k2);
        }
    }
}

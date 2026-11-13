using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        static readonly Rgba CaptainNavy = new Rgba(0.13f, 0.17f, 0.32f), CaptainSteel = new Rgba(0.62f, 0.74f, 0.98f), CaptainGold = new Rgba(1f, 0.8f, 0.35f);

        /// The Riot Captain (RiotCaptain.cs): a heavy officer in navy with gold epaulettes and a peaked cap, behind a curved tower
        /// shield. Winding up a charge: its lane on the floor and the shield flashing; charging: after-images; dazed: the shield
        /// dropped beside it and stars round its cap; held low: the shield swung aside. A gas canister in hand for the volley, a
        /// whistle and rings for the call, a muzzle flash over the rim for the buckshot. Red glow below half health.
        void DrawRiotCaptain(Enemy e, V2 pos, float r, bool flash, bool spawning, float t)
        {
            byte st = e.Phase;
            V2 f = V2.FromAngle(e.Facing), side = f.Orthogonal();
            bool enraged = e.MaxHp > 0f && e.Hp < e.MaxHp * 0.5f;
            float a = spawning ? 0.4f : 1f;
            if (st == Game.CapWinding)  // the charge's lane, pulsing
            {
                float w = 0.5f + 0.5f * Mathf.Sin(t * 30f), len = Game.CaptainDash * Game.CaptainDashTime + r;
                Line(pos, pos + f * len, r * 2f, new Rgba(1f, 0.3f, 0.2f, 0.1f + 0.1f * w));
                Line(pos + side * r, pos + side * r + f * len, 2f, new Rgba(1f, 0.4f, 0.3f, 0.75f));
                Line(pos - side * r, pos - side * r + f * len, 2f, new Rgba(1f, 0.4f, 0.3f, 0.75f));
                for (int k = 1; k <= 4; k++)  // chevrons along it
                {
                    V2 c = pos + f * (len * k / 5f);
                    Line(c - side * 10f - f * 8f, c, 3f, new Rgba(1f, 0.5f, 0.35f, 0.6f * w));
                    Line(c + side * 10f - f * 8f, c, 3f, new Rgba(1f, 0.5f, 0.35f, 0.6f * w));
                }
            }
            if (st == Game.CapCharging)
                for (int k = 1; k <= 3; k++)
                    Circle(pos - f * (k * 20f), r * (1f - k * 0.15f), CaptainSteel.WithAlpha(0.22f / k), 20);
            GlowDisc(pos, r * 2f, enraged ? new Rgba(1f, 0.3f, 0.2f) : CaptainSteel, enraged ? 0.32f : 0.14f);

            // The body: a navy uniform, gold epaulettes, a belt with a canister on it.
            var navy = (flash ? Rgba.White : CaptainNavy).WithAlpha(a);
            Polygon(pos, r, 8, e.Facing + MathX.Pi / 8f, navy);
            Polygon(pos, r * 0.8f, 8, e.Facing + MathX.Pi / 8f, (flash ? Rgba.White : new Rgba(0.2f, 0.26f, 0.45f)).WithAlpha(a));
            for (int s = -1; s <= 1; s += 2)
            {
                V2 ep = pos + side * (s * r * 0.78f) - f * (r * 0.1f);
                Circle(ep, r * 0.26f, CaptainGold.WithAlpha(a), 12);
                for (int k = -1; k <= 1; k++)  // fringe
                    Line(ep + f * (k * r * 0.12f), ep + f * (k * r * 0.12f) + side * (s * r * 0.2f), 2f, CaptainGold.Darkened(0.25f).WithAlpha(a));
            }
            Line(pos - f * (r * 0.45f) - side * (r * 0.62f), pos - f * (r * 0.45f) + side * (r * 0.62f), 3f, new Rgba(0.08f, 0.08f, 0.12f, a));  // belt

            // The peaked cap, looking where it faces, a gold badge on the front.
            V2 head = pos - f * (r * 0.05f);
            Circle(head, r * 0.42f, new Rgba(0.09f, 0.12f, 0.24f, a), 18);
            Arc(head, r * 0.42f, 2f, CaptainSteel.Darkened(0.3f).WithAlpha(a), 18);
            Arc(head + f * (r * 0.12f), r * 0.44f, 4f, new Rgba(0.05f, 0.06f, 0.1f, a), 12, e.Facing - 0.9f, 1.8f);  // the peak
            Polygon(head + f * (r * 0.22f), r * 0.12f, 5, e.Facing, (enraged ? new Rgba(1f, 0.45f, 0.3f) : CaptainGold).WithAlpha(a));

            // What's in its off hand.
            V2 hand = pos - side * (r * 0.95f) + f * (r * 0.35f);
            if (st == Game.CapGas)
            {
                V2 up = hand - f * (r * 0.5f) + new V2(0f, -r * 0.3f * Mathf.Sin(t * 8f));
                Circle(up, 6f, new Rgba(0.6f, 0.95f, 0.4f, a), 10);
                Circle(up, 3f, new Rgba(0.25f, 0.4f, 0.2f, a), 8);
            }
            else if (st == Game.CapWhistle)
            {
                Circle(head + f * (r * 0.45f), 3.5f, new Rgba(0.85f, 0.9f, 1f, a), 8);
                for (int k = 0; k < 3; k++)
                {
                    float u = (t * 2.2f + k / 3f) % 1f;
                    Arc(head + f * (r * 0.5f), 10f + u * 50f, 2f, new Rgba(0.75f, 0.91f, 1f, 0.7f * (1f - u)), 16, e.Facing - 0.7f, 1.4f);
                }
            }
            else if (st == Game.CapFans)
            {
                V2 muzzle = pos + side * (r * 0.9f) + f * (r * 1.4f);
                Line(pos + side * (r * 0.9f) + f * (r * 0.3f), muzzle, 5f, new Rgba(0.1f, 0.1f, 0.14f, a));
                if (e.PhaseT > 0.25f)
                    Circle(muzzle + f * 5f, 7f, new Rgba(1f, 0.85f, 0.45f, 0.85f), 10);
            }
            else
            {
                V2 tip = hand + f * (r * 0.55f);  // a baton held ready
                Line(hand, tip, 4f, new Rgba(0.08f, 0.09f, 0.13f, a));
                Circle(tip, 2.5f, new Rgba(0.6f, 0.65f, 0.75f, a), 6);
            }

            // The tower shield: a thick curved slab round its front.
            if (st == Game.CapDazed)
            {
                // Dropped beside it, flat; stars round the cap.
                V2 c = pos + side * (r * 1.3f) + f * (r * 0.4f);
                Line(c - f * (r * 0.9f), c + f * (r * 0.9f), 12f, CaptainSteel.WithAlpha(0.45f));
                Line(c - f * (r * 0.9f) + side * 6f, c + f * (r * 0.9f) + side * 6f, 2f, new Rgba(0.92f, 0.96f, 1f, 0.6f));
                for (int k = 0; k < 4; k++)
                    Circle(head + V2.FromAngle(t * 6f + k * MathX.Tau / 4f) * (r * 0.75f), 3f, new Rgba(1f, 0.95f, 0.6f, 0.95f), 6);
                return;
            }
            bool low = st == Game.CapLow;
            float rad = r + (st == Game.CapCharging ? 14f : 10f), sweep = Game.CaptainShieldArc * 1.6f;
            float mid = e.Facing + (low ? 1.1f : 0f);
            Rgba face = CaptainSteel.WithAlpha(low ? 0.45f : 0.9f);
            if (st == Game.CapWinding)
                face = face.Mix(Rgba.White, 0.35f + 0.35f * Mathf.Sin(t * 30f));
            if (st == Game.CapCharging)
                face = face.Mix(Rgba.White, 0.6f);
            if (flash)
                face = Rgba.White;
            Arc(pos, rad + 5f, 12f, face, 20, mid - sweep / 2f, sweep);
            Arc(pos, rad + 11f, 2.5f, new Rgba(0.92f, 0.96f, 1f, low ? 0.4f : 0.95f), 20, mid - sweep / 2f, sweep);  // the bright rim
            Arc(pos, rad - 1f, 1.6f, new Rgba(0.1f, 0.13f, 0.24f, 0.95f), 20, mid - sweep / 2f, sweep);
            // Its window and the gold stripe of rank.
            Arc(pos, rad + 5f, 4f, new Rgba(0.18f, 0.26f, 0.42f, 0.9f), 8, mid - 0.18f, 0.36f);
            Arc(pos, rad + 5f, 3f, CaptainGold.WithAlpha(low ? 0.4f : 0.9f), 6, mid + 0.3f, 0.2f);
            Arc(pos, rad + 5f, 3f, CaptainGold.WithAlpha(low ? 0.4f : 0.9f), 6, mid - 0.5f, 0.2f);
        }

        /// The Riot Captain's boss bar note.
        string CaptainNote(Enemy e, ref Color col)
        {
            switch (e.Phase)
            {
                case Game.CapWinding:
                    col = new Color(1f, 0.45f, 0.35f);
                    return "SHIELD CHARGE — sidestep it into a wall";
                case Game.CapDazed:
                    col = new Color(1f, 0.9f, 0.45f);
                    return "DAZED — shield down, hit it now";
                case Game.CapWhistle:
                    col = new Color(0.75f, 0.91f, 1f);
                    return "WHISTLE — Taser Guards coming";
            }
            if (e.Hp < e.MaxHp * 0.5f)
            {
                col = new Color(1f, 0.45f, 0.35f);
                return "ENRAGED — faster, and it charges twice";
            }
            return "WARDEN MINI-BOSS · its shield blocks the front: get round it";
        }
    }
}

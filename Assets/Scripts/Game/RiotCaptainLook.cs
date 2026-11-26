using UnityEngine;

namespace Arena
{
    public partial class ArenaApp
    {
        static readonly Rgba CaptainNavy = new Rgba(0.13f, 0.17f, 0.32f), CaptainSteel = new Rgba(0.62f, 0.74f, 0.98f), CaptainGold = Rgba.Gold;

        /// The Riot Captain (RiotCaptain.cs): a heavy officer in navy with gold epaulettes and a peaked cap, behind a curved tower
        /// shield. Winding up a charge: its lane on the floor and the shield flashing; charging: after-images; dazed: the shield
        /// dropped beside it and stars round its cap; held low: the shield swung aside. A gas canister in hand for the volley, a
        /// whistle and rings for the call, a muzzle flash over the rim for the buckshot. Red glow below half health.
        void DrawRiotCaptain(Enemy e, V2 pos, float r, bool flash, bool spawning, float t)
        {
            byte st = e.Phase;
            V2.Axes(e.Facing, out V2 f, out V2 side);
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
            DrawRiotCaptainBody(e, pos, r, flash, a, enraged, t);
        }

        /// The Captain itself, in the officers' smooth style (OfficerLook.cs): an inked navy coat, gold epaulettes, the peaked
        /// cap, one gloved hand on the tower shield's grip and the other holding the baton, the canister, the whistle or the
        /// shotgun (that one over the shield's rim, the shield held in the other hand).
        void DrawRiotCaptainBody(Enemy e, V2 pos, float r, bool flash, float a, bool enraged, float t)
        {
            byte st = e.Phase;
            V2.Axes(e.Facing, out V2 f, out V2 side);
            float k = r / 15f, aw = 3.4f * k;
            var navy = flash ? Rgba.White : CaptainNavy.Mix(new Rgba(0.2f, 0.26f, 0.45f), 0.5f);
            bool dazed = st == Game.CapDazed, low = st == Game.CapLow, fans = st == Game.CapFans;
            float rad = r + (st == Game.CapCharging ? 14f : 10f), sweep = Game.CaptainShieldArc * 1.6f;
            float mid = e.Facing + (low ? 1.1f : 0f);
            // the torso: an inked navy coat, the belt
            Oval(pos, r * 0.78f + OffInk, r * 1.0f + OffInk, e.Facing, GuardInk.WithAlpha(a));
            Oval(pos, r * 0.78f, r * 1.0f, e.Facing, navy.WithAlpha(a));
            Oval(pos - f * r * 0.12f, r * 0.55f, r * 0.82f, e.Facing, navy.Mix(GuardInk, 0.18f).WithAlpha(a));
            Cap(pos - f * (r * 0.45f) - side * (r * 0.62f), pos - f * (r * 0.45f) + side * (r * 0.62f), 3f * k * 0.8f, new Rgba(0.08f, 0.08f, 0.12f, a));
            // the arms: one on the shield's grip, one with what's in hand
            V2 lShoulder = pos - side * (r * 0.82f), rShoulder = pos + side * (r * 0.82f);
            V2 shieldGrip = pos + V2.FromAngle(mid + (fans ? -0.35f : 0.35f)) * (rad - 1f);
            V2 itemHand = fans ? pos + side * (r * 0.9f) + f * (r * 0.75f) : pos - side * (r * 0.95f) + f * (r * 0.45f);
            if (dazed)
            {
                shieldGrip = pos + side * (r * 1.05f) + f * (r * 0.2f);
                itemHand = pos - side * (r * 1.0f) + f * (r * 0.2f);
            }
            Rgba sleeve = navy, glove = OfficerGlove;
            OfficerArm(fans ? lShoulder : rShoulder, shieldGrip, aw, sleeve, glove);
            if (st == Game.CapGas)
                itemHand = itemHand - f * (r * 0.4f) + new V2(0f, -r * 0.3f * Mathf.Sin(t * 8f));
            if (st != Game.CapWhistle)
                OfficerArm(fans ? rShoulder : lShoulder, itemHand, aw, sleeve, glove);
            // the epaulettes
            for (int s = -1; s <= 1; s += 2)
            {
                V2 ep = pos + side * (s * r * 0.72f) - f * (r * 0.1f);
                for (int j = -1; j <= 1; j++)  // fringe
                    Cap(ep + f * (j * r * 0.12f), ep + f * (j * r * 0.12f) + side * (s * r * 0.24f), 1.8f, CaptainGold.Darkened(0.25f).WithAlpha(a));
                Circle(ep, r * 0.24f + OffInk, GuardInk.WithAlpha(a), 24);
                Circle(ep, r * 0.24f, CaptainGold.WithAlpha(a), 24);
                Circle(ep - f * r * 0.05f - side * r * 0.05f, r * 0.1f, new Rgba(1f, 0.95f, 0.75f, 0.6f * a), 12);
            }
            // the peaked cap
            V2 head = pos - f * (r * 0.05f);
            OfficerCap(head, r * 0.7f, e.Facing, new Rgba(0.09f, 0.12f, 0.24f), enraged ? new Rgba(1f, 0.45f, 0.3f) : CaptainGold);
            // what's in hand
            if (st == Game.CapGas)
            {
                Cap(itemHand - side * 3f, itemHand + side * 3f, 8f + 2.4f, GuardInk);
                Cap(itemHand - side * 3f, itemHand + side * 3f, 8f, new Rgba(0.6f, 0.95f, 0.4f, a));
                Cap(itemHand - side * 3f, itemHand - side * 1.4f, 8f, new Rgba(0.25f, 0.4f, 0.2f, a));
            }
            else if (st == Game.CapWhistle)
            {
                OfficerArm(lShoulder, head + f * (r * 0.42f) - side * 2f, aw, sleeve, glove);  // the whistle to its mouth
                Circle(head + f * (r * 0.5f), 3.5f + 1f, GuardInk, 16);
                Circle(head + f * (r * 0.5f), 3.5f, new Rgba(0.85f, 0.9f, 1f, a), 16);
                for (int j = 0; j < 3; j++)
                {
                    float u = (t * 2.2f + j / 3f) % 1f;
                    Arc(head + f * (r * 0.5f), 10f + u * 50f, 2f, new Rgba(0.75f, 0.91f, 1f, 0.7f * (1f - u)), 24, e.Facing - 0.7f, 1.4f);
                }
            }
            else if (fans) { }  // the shotgun: drawn over the shield, below
            else
            {
                V2 tip = itemHand + f * (r * 0.7f);  // a baton held ready
                GuardStick(itemHand - f * 3f, tip, 3.4f, new Rgba(0.08f, 0.09f, 0.13f, a));
                Circle(itemHand, aw * 0.62f, glove, 20);
            }
            // the tower shield
            if (dazed)
            {
                V2 c = pos + side * (r * 1.35f) + f * (r * 0.4f);
                Cap(c - f * (r * 0.9f), c + f * (r * 0.9f), 12f + 2.4f, new Rgba(0.1f, 0.13f, 0.24f, 0.6f));
                Cap(c - f * (r * 0.9f), c + f * (r * 0.9f), 12f, CaptainSteel.WithAlpha(0.5f));
                Cap(c - f * (r * 0.9f) + side * 4f, c + f * (r * 0.9f) + side * 4f, 2f, new Rgba(0.92f, 0.96f, 1f, 0.6f));
                for (int j = 0; j < 4; j++)
                    Circle(head + V2.FromAngle(t * 6f + j * MathX.Tau / 4f) * (r * 0.75f), 3f, new Rgba(1f, 0.95f, 0.6f, 0.95f), 12);
                return;
            }
            Rgba face = CaptainSteel.WithAlpha(low ? 0.5f : 0.92f);
            if (st == Game.CapWinding)
                face = face.Mix(Rgba.White, 0.35f + 0.35f * Mathf.Sin(t * 30f));
            if (st == Game.CapCharging)
                face = face.Mix(Rgba.White, 0.6f);
            if (flash)
                face = Rgba.White;
            float a0 = mid - sweep / 2f, R = rad + 5f;
            var edge = new Rgba(0.1f, 0.13f, 0.24f, 0.95f);
            Arc(pos, R, 14.5f, edge, 48, a0, sweep);  // the outline, then the face, with round ends
            for (int s = 0; s < 2; s++)
                Circle(pos + V2.FromAngle(a0 + sweep * s) * R, 7.25f, edge, 20);
            Arc(pos, R, 12f, face, 48, a0, sweep);
            for (int s = 0; s < 2; s++)
                Circle(pos + V2.FromAngle(a0 + sweep * s) * R, 6f, face, 20);
            Arc(pos, R + 4f, 2.2f, new Rgba(0.92f, 0.96f, 1f, low ? 0.4f : 0.95f), 48, a0, sweep);  // the bright rim
            Arc(pos, R, 4f, new Rgba(0.18f, 0.26f, 0.42f, 0.9f), 16, mid - 0.18f, 0.36f);            // its window
            Arc(pos, R, 3f, CaptainGold.WithAlpha(low ? 0.4f : 0.9f), 12, mid + 0.3f, 0.2f);          // the gold stripes of rank
            Arc(pos, R, 3f, CaptainGold.WithAlpha(low ? 0.4f : 0.9f), 12, mid - 0.5f, 0.2f);
            Circle(shieldGrip, aw * 0.62f + OffInk, GuardInk, 20);  // the glove on the grip, over the shield's back edge
            Circle(shieldGrip, aw * 0.62f, glove, 20);
            if (fans)  // over the shield's rim
            {
                V2 muzzle = pos + side * (r * 0.9f) + f * (r * 2.0f), stock = pos + side * (r * 0.9f) + f * (r * 0.15f);
                Cap(stock, muzzle, 5.5f + 2.4f, GuardInk);
                Cap(stock, muzzle, 5.5f, new Rgba(0.18f, 0.18f, 0.22f, a));                                  // the shotgun
                Cap(stock, stock + f * (r * 0.3f), 6f, new Rgba(0.45f, 0.3f, 0.18f, a));                    // its wooden stock
                Cap(muzzle - f * (r * 0.95f), muzzle - f * (r * 0.65f), 6.5f, new Rgba(0.4f, 0.28f, 0.17f, a)); // the pump
                Circle(itemHand, aw * 0.62f, glove, 20);
                if (e.PhaseT > 0.25f)
                    Circle(muzzle + f * 6f, 7f, new Rgba(1f, 0.85f, 0.45f, 0.85f), 20);
            }
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

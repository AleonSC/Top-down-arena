using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Riot Gear's look, the Plate (Docs/Mockups/riot-gear-effects.html, look 2): a riot-shield plate that hovers beside you and swings
    /// toward the nearest incoming enemy shot (else where you aim). Recharging, a ghost plate fills along its length; when the shield
    /// returns the plate snaps back with a small pop. A block (FxKind.RiotBlock) flashes the plate white, throws sparks off the impact
    /// point and breaks the plate into three pieces. Visual effects reduced or off: the plate and the HUD chip only.
    /// Drawing only: everything comes from the replicated RiotT, the player's perks and the shots this screen already has.
    public partial class ArenaApp
    {
        const float PlateArc = 0.75f;  // the plate covers this much either side of its middle (radians)
        static readonly Rgba PlateSteel = new Rgba(0.56f, 0.71f, 0.85f), PlateDark = new Rgba(0.14f, 0.23f, 0.35f), PlateLight = new Rgba(0.89f, 0.95f, 1f);

        readonly Dictionary<int, float> riotAngle = new Dictionary<int, float>();   // per player: where the plate is facing
        readonly Dictionary<int, float> riotPrevT = new Dictionary<int, float>();   // per player: RiotT last frame
        readonly Dictionary<int, float> riotBackAt = new Dictionary<int, float>();  // per player: when the shield last came back

        static float AngleDiff(float from, float to)
        {
            float d = to - from;
            while (d > Mathf.PI) d -= MathX.Tau;
            while (d < -Mathf.PI) d += MathX.Tau;
            return d;
        }

        void DrawRiotGear(Player p, float t)
        {
            if (!Game.HasPerk(p, PerkId.RiotGear) || p.Downed)
                return;
            float R = p.Def.BodyR + 11f;
            bool ready = p.RiotT <= 0f;
            float prev;
            if (riotPrevT.TryGetValue(p.Id, out prev) && prev > 0f && ready)
                riotBackAt[p.Id] = t;  // it just came back: the plate pops in
            riotPrevT[p.Id] = p.RiotT;

            // The plate swings toward the nearest enemy shot, else it rests to the side and behind you (clear of the gun and the aim line).
            float idle = p.Aim + 2.4f, want = idle, best = 240f * 240f;
            foreach (var b in game.Bullets.Values)
            {
                float d = (b.Pos - p.Pos).LengthSq;
                if (Defs.IsEnemyShot(b.Kind) && d < best)
                {
                    best = d;
                    want = Mathf.Atan2(b.Pos.Y - p.Pos.Y, b.Pos.X - p.Pos.X);
                }
            }
            float cur;
            if (!riotAngle.TryGetValue(p.Id, out cur))
                cur = want;
            if (ready)
                cur += AngleDiff(cur, want) * Mathf.Min(1f, Time.deltaTime * 9f);
            else
                cur += AngleDiff(cur, idle) * Mathf.Min(1f, Time.deltaTime * 4f);  // resting
            riotAngle[p.Id] = cur;
            V2 c = p.Disp;

            if (ready)
            {
                float since, pop = 0f;
                if (riotBackAt.TryGetValue(p.Id, out since))
                    pop = Mathf.Max(0f, 0.3f - (t - since)) * 1.2f;
                float rr = R * (1f + pop * 0.4f);
                Arc(c, rr, 9f, PlateDark, 14, cur - PlateArc, PlateArc * 2f);
                Arc(c, rr, 5.5f, PlateSteel, 14, cur - PlateArc, PlateArc * 2f);
                Arc(c, rr + 2.5f, 1.5f, PlateLight.WithAlpha(0.9f), 14, cur - PlateArc + 0.05f, PlateArc * 2f - 0.1f);
            }
            else
            {
                float fill = 1f - p.RiotT / Perks.RiotRecharge;
                for (int k = 0; k < 6; k++)  // the ghost plate, dashed
                    Arc(c, R, 3f, PlateSteel.WithAlpha(0.2f), 3, cur - PlateArc + k * PlateArc / 3f, PlateArc / 6f);
                if (fill > 0.01f)
                    Arc(c, R, 5f, PlateSteel.WithAlpha(0.35f + 0.5f * fill), 12, cur - PlateArc, PlateArc * 2f * fill);
            }
        }

        /// FxKind.RiotBlock (Pos = the owner, Pos2 = where the hit came from, Radius = the plate's reach). Shown for Game.RiotBlockFxTime.
        void DrawRiotBlockFx(Effect fx)
        {
            float ti = fx.T, R = fx.Radius;
            V2 c = fx.Pos;
            float dir = Mathf.Atan2(fx.Pos2.Y - c.Y, fx.Pos2.X - c.X);
            if (ti < 0.2f)  // the plate flashes white where the hit landed
                Arc(c, R, 9f, Rgba.White.WithAlpha(1f - ti / 0.2f), 14, dir - PlateArc, PlateArc * 2f);
            if (!fxOn || fxReduced)
                return;
            if (ti < 0.4f)  // a ring
            {
                float k = ti / 0.4f;
                Arc(c, R + 40f * k, 2f, Rgba.White.WithAlpha(0.8f * (1f - k)), 28);
            }
            if (ti < 0.4f)  // sparks off the impact point
                for (int k = 0; k < 16; k++)
                {
                    float a = dir + Mathf.PI + (Hash01(k) - 0.5f) * 2.4f, v = 90f + Hash01(k + 5f) * 210f;
                    V2 at = fx.Pos2 + V2.FromAngle(a) * v * ti * (1f - ti);
                    Circle(at, 2.6f * (1f - ti / 0.4f), (k % 3 == 0 ? Rgba.White : new Rgba(1f, 0.82f, 0.47f)).WithAlpha(1f - ti / 0.4f), 6);
                }
            if (ti < Game.RiotBlockFxTime)  // the plate breaks into three pieces that fly off
            {
                float al = 1f - ti / Game.RiotBlockFxTime;
                for (int k = 0; k < 3; k++)
                {
                    float a0 = dir - PlateArc + k * PlateArc * 2f / 3f, mid = a0 + PlateArc / 3f;
                    V2 at = c + V2.FromAngle(mid) * (70f * ti);
                    Arc(at, R, 9f, PlateDark.WithAlpha(al), 6, a0, PlateArc * 2f / 3f);
                    Arc(at, R, 5.5f, PlateSteel.WithAlpha(al), 6, a0, PlateArc * 2f / 3f);
                }
            }
        }

        /// The HUD chip beside the ability tiles: a tile that fills from the bottom while the shield recharges, then "READY" (the Plate
        /// chip of Docs/Mockups/riot-gear-effects.html).
        void DrawRiotChip(Rect r, Player me)
        {
            bool ready = me.RiotT <= 0f;
            float fill = ready ? 1f : 1f - me.RiotT / Perks.RiotRecharge;
            var steel = new Color(PlateSteel.R, PlateSteel.G, PlateSteel.B);
            DkHud(r, ready ? steel : (Color?)null);
            var tile = new Rect(r.x + 6f, r.y + 6f, 26f, 26f);
            Ui.Fill(tile, new Color(0.04f, 0.05f, 0.1f, 0.9f));
            Ui.Fill(new Rect(tile.x, tile.y + tile.height * (1f - fill), tile.width, tile.height * fill), new Color(steel.r, steel.g, steel.b, 0.35f));
            Ui.Rim(tile, steel, ready ? 0.9f : 0.3f);
            Ui.Icon(new Rect(tile.x + 4f, tile.y + 4f, 18f, 18f), PixelArt.Get("perk_riot"), ready ? 1f : 0.55f);
            Ui.Label(new Rect(r.x + 38f, r.y + 4f, r.width - 42f, 15f), "Riot Gear", 11, Color.white, TextAnchor.MiddleLeft, true);
            Ui.Label(new Rect(r.x + 38f, r.y + 19f, r.width - 42f, 15f), ready ? "READY" : Mathf.CeilToInt(me.RiotT) + " s", 11, ready ? steel : Ui.Muted, TextAnchor.MiddleLeft, true);
        }
    }
}

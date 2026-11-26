using System.Collections.Generic;
using UnityEngine;

namespace Arena
{
    /// Hazard Foam (Core/HazardFoam.cs; mockup Docs/Mockups/hazard-foam.html), all worked out from replicated state:
    ///   the foam    (ZoneKind.HazardFoam) every lump drawn together so touching lumps read as one mass: a soft shadow, a dark
    ///               olive rim, a cream body that yellows as it cures, a highlight, bubbles turned by the lump's Aim, darker
    ///               dents as it's worn (GuardFrac), puffing up when it lands and fizzing away over its last seconds.
    ///   the soldier (Player.FoamT) a striped hazard canister on the back with a hose to a yellow spray wand held in both hands;
    ///               under them a gold time ring and the tank's striped bar; for your own, where the foam lands round the cursor.
    ///   a glob      (Defs.FoamGlob) a cream blob on a little lob, with a shadow under it.
    public partial class ArenaApp
    {
        static readonly Rgba FmCream = Game.FoamCream, FmYellow = Game.FoamYellow;
        static readonly Rgba FmCured = new Rgba(0.89f, 0.82f, 0.5f), FmRim = new Rgba(0.36f, 0.29f, 0.06f), FmShade = new Rgba(0.79f, 0.7f, 0.36f);
        static readonly Rgba FmInk = new Rgba(0.1f, 0.1f, 0.1f), FmHose = new Rgba(0.16f, 0.16f, 0.17f);
        readonly Dictionary<int, float> fmShown = new Dictionary<int, float>();  // each lump's drawn radius, eased toward its real one (the puff as foam lands)
        readonly List<int> fmGone = new List<int>();
        float fmLastT;

        // ---------------- the foam ----------------

        /// After the zones: every lump in four passes (shadow, rim, body, details) so overlapping lumps merge.
        void DrawHazardFoam(float t)
        {
            float dt = Mathf.Clamp(t - fmLastT, 0f, 0.1f);
            fmLastT = t;
            bool any = false;
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.HazardFoam)
                {
                    any = true;
                    float shown;
                    if (!fmShown.TryGetValue(z.Id, out shown))
                        shown = z.Radius * 0.3f;
                    fmShown[z.Id] = shown + (z.Radius - shown) * Mathf.Min(1f, dt * 9f);
                }
            fmGone.Clear();
            foreach (var id in fmShown.Keys)
                if (!game.Zones.ContainsKey(id))
                    fmGone.Add(id);
            foreach (var id in fmGone)
                fmShown.Remove(id);
            if (!any)
                return;

            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.HazardFoam)
                    Ellipse(z.Pos + new V2(3f, 5f), FoamDrawR(z) + 2f, FoamDrawR(z) + 1f, new Rgba(0f, 0f, 0f, 0.3f * FoamFade(z)));
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.HazardFoam)
                    Circle(z.Pos, FoamDrawR(z) + 2.5f, FmRim.WithAlpha(FoamFade(z)), 24);
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.HazardFoam)
                {
                    float r = FoamDrawR(z), age = z.Duration - z.T, cure = Mathf.Clamp01(age / 20f), a = FoamFade(z);
                    Rgba body = FmCream.Mix(FmCured, cure);
                    Circle(z.Pos, r, FmShade.WithAlpha(a), 24);
                    Circle(z.Pos + new V2(-r * 0.12f, -r * 0.14f), r * 0.86f, body.WithAlpha(a), 24);
                }
            foreach (var z in game.Zones.Values)
                if (z.Kind == ZoneKind.HazardFoam)
                    DrawFoamDetails(z, t);
        }

        /// A lump's drawn radius: eased in as it puffs up, shrinking with the solid radius as it dissolves.
        float FoamDrawR(Zone z)
        {
            float shown;
            float r = fmShown.TryGetValue(z.Id, out shown) ? shown : z.Radius;
            return r * (Game.FoamR(z) / Mathf.Max(1f, z.Radius));
        }

        static float FoamFade(Zone z) => 0.35f + 0.65f * Mathf.Clamp01(z.T / Game.FoamFizz);

        void DrawFoamDetails(Zone z, float t)
        {
            float r = FoamDrawR(z), a = FoamFade(z), worn = 1f - game.FoamFrac(z);
            Circle(z.Pos + new V2(-r * 0.38f, -r * 0.42f), r * 0.22f, new Rgba(1f, 1f, 1f, 0.55f * a), 10);  // the shine
            for (int i = 0; i < 6; i++)  // bubbles
            {
                float h1 = Mathf.Repeat(Mathf.Sin((z.Id * 7 + i) * 12.9898f) * 43758.55f, 1f), h2 = Mathf.Repeat(Mathf.Sin((z.Id * 7 + i) * 78.233f) * 12543.7f, 1f);
                V2 at = z.Pos + V2.FromAngle(z.Aim + i * 1.9f) * (r * (0.2f + 0.55f * h1));
                float br = r * (0.08f + 0.1f * h2);
                Arc(at, br, 1f, FmRim.WithAlpha(0.35f * a), 10);
                Circle(at + new V2(-br * 0.35f, -br * 0.35f), br * 0.3f, new Rgba(1f, 1f, 1f, 0.6f * a), 6);
            }
            if (worn > 0.35f)  // dents where it's been chewed and shot
            {
                float k = (worn - 0.35f) / 0.65f;
                Circle(z.Pos + V2.FromAngle(z.Aim + 2.4f) * (r * 0.3f), r * (0.25f + 0.2f * k), FmRim.WithAlpha(0.45f * k * a), 12);
                Circle(z.Pos + V2.FromAngle(z.Aim - 0.8f) * (r * 0.45f), r * 0.18f * k, FmRim.WithAlpha(0.35f * k * a), 10);
            }
            if (z.T < Game.FoamFizz && !fxReduced)  // fizzing away: bubbles rising off it
            {
                float k = 1f - z.T / Game.FoamFizz;
                for (int i = 0; i < 5; i++)
                {
                    float ph = Mathf.Repeat(t * (0.9f + 0.2f * i) + i * 0.37f + z.Id * 0.13f, 1f);
                    V2 at = z.Pos + new V2(Mathf.Sin(i * 2.1f + z.Id) * r * 0.7f, -ph * (14f + r));
                    Circle(at, 1.2f + 1.6f * (1f - ph), FmCream.WithAlpha(0.8f * k * (1f - ph)), 6);
                }
            }
        }

        // ---------------- a glob in flight ----------------

        void DrawFoamGlob(Bullet b)
        {
            float k = b.Life0 > 0f ? Mathf.Clamp01(1f - b.Life / b.Life0) : 1f, lift = Mathf.Sin(k * Mathf.PI) * 14f;
            Circle(b.Pos, 3f, new Rgba(0f, 0f, 0f, 0.25f), 8);  // its shadow on the floor
            V2 at = b.Pos + new V2(0f, -lift);
            Circle(at, 4.6f, FmRim, 10);
            Circle(at, 3.6f, FmCream, 10);
            Circle(at + new V2(-1.2f, -1.2f), 1.2f, Rgba.White, 6);
        }

        // ---------------- the soldier ----------------

        /// From DrawPlayerModel, in place of the weapon: both hands on the spray wand (the canister and wand are drawn over the head).
        void DrawFoamArms(Player p, ModelPal K, float ry)
        {
            MFrame(0f, 0f, 0f, 1f, false);
            ModelArm(K, 1f, -ry * 0.72f, 16f, 4f);   // the front hand on the wand
            ModelArm(K, 1f, ry * 0.72f, 5f, 12f);    // the grip
            MCirc(16f, 4f, 2.8f, K.Sleeve, 8);
            MCirc(5f, 12f, 2.8f, K.Sleeve, 8);
        }

        /// The striped canister on the back, its hose, and the wand held out front; a puff at the nozzle while spraying.
        void DrawFoamSprayer(Player p, float t)
        {
            MFrame(0f, 0f, 0f, 1f, false);
            float x0 = -24f, w = 12f, h = 9f;
            MRect(x0 - 1f, -h - 1f, w + 2f, h * 2f + 2f, FmInk);  // the canister
            MRect(x0, -h, w, h * 2f, FmYellow);
            for (int i = 0; i < 4; i++)  // its hazard stripes
            {
                float y = -h + 1f + i * 4.6f;
                MPoly(FmInk, x0, y, x0 + w, y + 2.4f, x0 + w, y + 4.2f, x0, y + 1.8f);
            }
            MRect(x0 + 3f, -h - 3f, w - 6f, 2.5f, FmHose);  // the valve
            MPoly(FmHose, x0 + w - 1f, -2f, 2f, 6f, 6f, 10f, 4f, 11.5f, 0f, 8f, x0 + w - 1f, 1f);  // the hose round to the grip
            MRect(4f, 6.5f, 26f, 4.5f, FmInk);  // the wand
            MRect(5f, 7.3f, 24f, 2.9f, FmYellow);
            MRect(28f, 5.5f, 5f, 6.5f, FmHose);  // the nozzle
            if (p.Firing && p.FoamTank > 0f)
                for (int i = 0; i < 3; i++)
                {
                    float ph = Mathf.Repeat(t * 9f + i * 0.33f, 1f);
                    MCirc(34f + ph * 10f, 8.7f + Mathf.Sin(i * 2.3f + t * 20f) * 2f * ph, 2.2f + ph * 1.8f, FmCream.WithAlpha(1f - ph), 8);
                }
        }

        /// Under the soldier: the time ring and the tank's striped bar; for your own, where the foam lands.
        void DrawFoamAura(Player p, float t)
        {
            if (!Game.FoamOn(p))
                return;
            float r = p.Def.BodyR + 11f, left = Mathf.Clamp01(p.FoamT / Game.FoamTime);
            bool warn = p.FoamT < 2.5f && Mathf.Repeat(t * 6f, 1f) < 0.5f;
            Arc(p.Disp, r, 3f, Rgba.Gold.WithAlpha(0.18f), 32);
            Arc(p.Disp, r, 3f, (warn ? new Rgba(1f, 0.3f, 0.25f) : Rgba.Gold).WithAlpha(0.9f), 32, -MathX.Pi / 2f, MathX.Tau * left);
            float full = p.FoamTank > Game.FoamTankMax ? Game.FoamTankMastered : Mathf.Max(Game.FoamTankMax, p.FoamTank);
            float fill = Mathf.Clamp01(p.FoamTank / full), bw = 44f, bx = p.Disp.X - bw / 2f, by = p.Disp.Y + p.Def.BodyR + 18f;
            Quad(new V2(bx - 1f, by - 1f), new V2(bx + bw + 1f, by - 1f), new V2(bx + bw + 1f, by + 6f), new V2(bx - 1f, by + 6f), new Rgba(0f, 0f, 0f, 0.65f));
            float fw = bw * fill;
            if (fw > 0.5f)
            {
                Quad(new V2(bx, by), new V2(bx + fw, by), new V2(bx + fw, by + 5f), new V2(bx, by + 5f), FmYellow);
                for (float s = 3f; s < fw - 1f; s += 7f)  // the stripes
                    Quad(new V2(bx + s, by + 5f), new V2(bx + Mathf.Min(fw, s + 2.5f), by + 5f), new V2(bx + Mathf.Min(fw, s + 5f), by), new V2(bx + Mathf.Min(fw, s + 2.5f), by), FmInk);
            }
            if (game == null || p.Id != game.MyId || p.AimAt.IsZero)
                return;
            V2 from = p.Disp + V2.FromAngle(p.Aim) * Game.FoamMuzzle;
            float d = Mathf.Clamp(p.AimAt.DistanceTo(p.Pos) - Game.FoamMuzzle, Game.FoamMinRange, Game.FoamRange);
            V2 at = from + V2.FromAngle(p.Aim) * d;
            for (int i = 0; i < 12; i++)  // dashed: where the foam lands
                Arc(at, 16f, 1.5f, FmYellow.WithAlpha(0.5f), 3, i * MathX.Tau / 12f + t * 0.6f, MathX.Tau / 24f);
        }
    }
}

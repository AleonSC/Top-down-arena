using System;
using System.Collections.Generic;

namespace Arena
{
    /// Holy Lance Rain, the Healer Knight's third ultimate (key 2 on the lobby's Ultimate row; Docs/Mockups/holy-lance-rain.html,
    /// the Scatter pattern). For LrRain s, LrCount spears of light fall at random spots within LrRadius px of the knight (the circle
    /// follows them as they walk; each spot is the best of LrTries, the one farthest from the spears still glowing, so the ground
    /// fills evenly). Each spear shows a gold ring for LrWarn s, then lands: every enemy within LrHit px takes LrDamage (times
    /// your damage), is stunned LrStun s (not bosses) and shoved LrShove px (not Heavy enemies or bosses). It stays as a small
    /// healing glow for LrGlowT s: each player standing in one or more glows heals LrGlowHeal HP/s (glows don't stack).
    /// Mastered: LrCountMastered spears, and the glows last LrGlowMastered s.
    /// Host logic; clients draw it from two effects (Game/HolyLanceRainLook.cs):
    ///   FxKind.HolyLanceRain: one at the cast; Radius = the caster's id, Color.A = LrRadius / 1000, Dur = LrRainFxDur. The circle
    ///     is drawn round wherever the caster is.
    ///   FxKind.HolyLanceSpear: one per spear, sent as its warning starts; Pos = where it lands, Radius = the caster's id,
    ///     Color.A = the glow's time / 10, Dur = LrWarn + the glow + LrFade. Its landing sends Sfx.LanceImpactSfx.
    public partial class Game
    {
        public const float LrRain = 3f, LrWarn = 0.35f, LrRadius = 340f, LrHit = 55f, LrDamage = 30f, LrStun = 0.3f, LrShove = 30f;
        public const float LrGlowR = 40f, LrGlowHeal = 5f, LrGlowT = 4f, LrGlowMastered = 6f, LrHealTick = 0.25f, LrFade = 0.6f;
        public const int LrCount = 24, LrCountMastered = 32, LrTries = 6;
        public const float LrRainFxDur = LrRain + LrWarn + 0.4f;

        internal static readonly AbilityDef[] LanceRainUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.HolyLanceRain, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Holy Lance Rain", Cooldown = 80f,
                Desc = "Spears of light rain down all around you for 3 s: each hurts the enemies where it lands and leaves a small healing glow.",
                Capstone = "32 spears instead of 24, and the glows last 6 s.",
                Bot = b => b.NearEnemies >= 3 || (b.HaveTarget && b.BotDanger > 2f),
            },
        };

        class LanceRain
        {
            public int Owner, Left;
            public float Gap, Every, Glow;
        }

        class LanceSpear
        {
            public int Owner;
            public V2 At;
            public float T, Glow;  // T: seconds since its warning started; Glow: how long it glows once landed
            public bool Landed;
        }

        readonly List<LanceRain> lanceRains = new List<LanceRain>();
        readonly List<LanceSpear> lanceSpears = new List<LanceSpear>();
        float lanceHealGap;

        void CastLanceRain(Player p, bool cap)
        {
            int n = cap ? LrCountMastered : LrCount;
            lanceRains.Add(new LanceRain { Owner = p.Id, Left = n, Gap = 0f, Every = LrRain / n, Glow = cap ? LrGlowMastered : LrGlowT });
            BroadcastFx(FxKind.HolyLanceRain, p.Pos, Rgba.Gold.WithAlpha(LrRadius / 1000f), p.Id, LrRainFxDur, Sfx.SpellHoly);
        }

        /// Where the next spear around `c` lands: the best of LrTries random spots in the circle, the one farthest from the
        /// spears still warning or glowing.
        V2 LanceSpot(V2 c)
        {
            V2 best = c;
            float bestD = -1f;
            for (int k = 0; k < LrTries; k++)
            {
                V2 at = ClampToArena(c + V2.FromAngle(RandAngle()) * (LrRadius * (float)Math.Sqrt(Rand(0f, 1f))), 20f);
                float d = float.MaxValue;
                foreach (var s in lanceSpears)
                    d = Math.Min(d, s.At.DistanceTo(at));
                if (d > bestD)
                {
                    bestD = d;
                    best = at;
                }
            }
            return best;
        }

        /// Host, every tick: drop the next spears, land them, heal those standing in the glows.
        void TickLanceRain(float dt)
        {
            for (int i = lanceRains.Count - 1; i >= 0; i--)
            {
                var r = lanceRains[i];
                Player p;
                if (!Players.TryGetValue(r.Owner, out p) || r.Left <= 0)
                {
                    lanceRains.RemoveAt(i);
                    continue;
                }
                r.Gap -= dt;
                while (r.Gap <= 0f && r.Left > 0)
                {
                    r.Gap += r.Every;
                    r.Left--;
                    var s = new LanceSpear { Owner = r.Owner, At = LanceSpot(p.Pos), Glow = r.Glow };
                    lanceSpears.Add(s);
                    BroadcastFx(FxKind.HolyLanceSpear, s.At, Rgba.Gold.WithAlpha(s.Glow / 10f), r.Owner, LrWarn + s.Glow + LrFade, Sfx.None);
                }
            }

            for (int i = lanceSpears.Count - 1; i >= 0; i--)
            {
                var s = lanceSpears[i];
                s.T += dt;
                if (!s.Landed && s.T >= LrWarn)
                {
                    s.Landed = true;
                    LandLance(s);
                }
                if (s.T >= LrWarn + s.Glow)
                    lanceSpears.RemoveAt(i);
            }

            if ((lanceHealGap -= dt) > 0f)
                return;
            lanceHealGap += LrHealTick;
            if (lanceHealGap < 0f)
                lanceHealGap = LrHealTick;
            foreach (var q in Players.Values)
            {
                if (q.Downed)
                    continue;
                LanceSpear glow = null;
                foreach (var s in lanceSpears)
                    if (s.Landed && s.At.DistanceTo(q.Pos) <= LrGlowR + 10f)  // (a player's feet: 10 px past the glow's rim)
                    {
                        glow = s;
                        break;
                    }
                Player o;
                if (glow != null && Players.TryGetValue(glow.Owner, out o))
                    Heal(q, LrGlowHeal * LrHealTick, o, "Holy Lance Rain");
            }
        }

        void LandLance(LanceSpear s)
        {
            Player p;
            float mult = Players.TryGetValue(s.Owner, out p) ? p.Stats.DmgMult : 1f;
            foreach (var e in FoesInCircle(s.At, LrHit, by: s.Owner))
            {
                HurtEnemy(e, LrDamage * mult, s.Owner);
                if (e.Def.IsBoss)
                    continue;
                Stun(e, LrStun);
                if (!e.Def.Heavy)
                    ShoveFrom(e, s.At, LrShove);
            }
            BroadcastSound(Sfx.LanceImpactSfx);
        }
    }
}

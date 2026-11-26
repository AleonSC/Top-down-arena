using System;
using System.Collections.Generic;

namespace Arena
{
    /// Sanctum Glass, the Healer Knight's 4th choice (`AbilityId.SanctumGlass`, `AbilityDef.Fourth`: the lobby's 4th ability key).
    /// The 1st ability raises a flat pane of stained glass GlassFront px ahead of you (GlassHalfWidth px each side, GlassBand px
    /// thick), facing your aim; it stays where it was cast for GlassTime s. Every enemy shot that touches it (red aimed shots too)
    /// turns into light: it is gone, and a healing mote flies from that spot to the most hurt player within GlassReach px of the
    /// pane, healing GlassSmallHeal (GlassBigHeal for a big shot) when it lands, GlassMoteT s later. The healing it has sent
    /// counts toward its cap (GlassCap, Rose Window +40% a stack); at the cap, or when its time runs out, it shatters. A shot
    /// that finds nobody hurt is still turned to light, but it heals nobody and doesn't count. Mastered: 40% wider, motes heal
    /// 50% more. Players' own shots pass through it untouched. Going down takes it away.
    /// Replicated on the Player: GlassT (seconds left, 0 = none), GlassPos/GlassAim (its middle and the way it faces), GlassWide
    /// (mastered), GlassHealed/GlassCap (healing sent and the cap: the cracks and the bar). FxKind.GlassMote is a mote (Pos = where
    /// the shot was turned, Pos2.X = the player it flies to, Pos2.Y = the caster), FxKind.GlassShatter the pane breaking
    /// (Pos = its middle, Pos2.X = its aim, Radius = its half width). The look: Game/SanctumGlassLook.cs.
    public partial class Game
    {
        public const float GlassTime = 6f, GlassFront = 110f, GlassHalfWidth = 110f, GlassHalfWidthMastered = 155f, GlassBand = 9f;
        public const float GlassCap = 120f, GlassSmallHeal = 4f, GlassBigHeal = 10f, GlassMasteredHeal = 1.5f, GlassReach = 400f, GlassMoteT = 0.45f;
        public const int GlassPanels = 7;
        public static readonly Rgba GlassGold = new Rgba(1f, 0.82f, 0.4f), GlassHealColor = new Rgba(0.49f, 1f, 0.69f);

        /// The panes' colours, left to right across the glass (as the caster faces it): ruby, cobalt, amber, emerald, violet, ...
        public static readonly Rgba[] GlassColors =
        {
            new Rgba(0.78f, 0.13f, 0.23f), new Rgba(0.12f, 0.37f, 0.84f), new Rgba(0.91f, 0.64f, 0.09f), new Rgba(0.12f, 0.62f, 0.35f),
            new Rgba(0.48f, 0.18f, 0.76f), new Rgba(0.91f, 0.64f, 0.09f), new Rgba(0.78f, 0.13f, 0.23f),
        };

        public static float GlassHalf(Player p) => p.GlassWide ? GlassHalfWidthMastered : GlassHalfWidth;

        /// Along the glass, the way its panes run (the caster's left to right).
        public static V2 GlassAcross(Player p) => V2.FromAngle(p.GlassAim + MathX.Pi * 0.5f);

        /// Which pane (0..GlassPanels-1) a point lies in front of or behind.
        public static int GlassPanelAt(Player p, V2 at)
        {
            float v = (at - p.GlassPos).Dot(GlassAcross(p)), half = GlassHalf(p);
            return Math.Max(0, Math.Min(GlassPanels - 1, (int)((v + half) / (2f * half) * GlassPanels)));
        }

        class GlassMote
        {
            public int Caster, Target;
            public float Amount, T;
        }

        readonly List<GlassMote> glassMotes = new List<GlassMote>();

        void CastSanctumGlass(Player p, bool cap)
        {
            p.GlassAim = p.Aim;
            p.GlassPos = ClampToArena(p.Pos + V2.FromAngle(p.Aim) * GlassFront, 10f);
            p.GlassWide = cap;
            p.GlassT = GlassTime;
            p.GlassHealed = 0f;
            p.GlassCap = GlassCap * p.Stats.GlassCapMult;
            BroadcastFx(FxKind.Ring, p.GlassPos, GlassGold, 60f, 0.35f, Sfx.SpellHoly);
        }

        /// Host, from the enemy-bullet loop in Combat.cs: does a Sanctum Glass turn this shot into light? Every enemy shot that
        /// touches a pane does (red aimed shots too).
        bool GlassStops(Bullet b)
        {
            foreach (var p in Players.Values)
            {
                if (p.GlassT <= 0f || p.Downed)
                    continue;
                V2 rel = b.Pos - p.GlassPos;
                float u = rel.Dot(V2.FromAngle(p.GlassAim)), v = rel.Dot(GlassAcross(p));
                if (Math.Abs(u) > GlassBand + b.Radius || Math.Abs(v) > GlassHalf(p) + b.Radius * 0.5f)
                    continue;
                float amount = (b.Kind == Defs.BigShot || b.Radius >= 9f ? GlassBigHeal : GlassSmallHeal) * (p.GlassWide ? GlassMasteredHeal : 1f);
                Player to = null;
                foreach (var q in Players.Values)
                    if (!q.Downed && q.SwallowT <= 0f && q.Hp < q.MaxHp && !q.Pos.FartherThan(p.GlassPos, GlassReach)
                        && (to == null || q.Hp / q.MaxHp < to.Hp / to.MaxHp))
                        to = q;
                if (to != null)
                {
                    amount = Math.Min(amount, p.GlassCap - p.GlassHealed);
                    p.GlassHealed += amount;
                    glassMotes.Add(new GlassMote { Caster = p.Id, Target = to.Id, Amount = amount, T = GlassMoteT });
                    BroadcastFx(FxKind.GlassMote, b.Pos, new V2(to.Id, p.Id), GlassColors[GlassPanelAt(p, b.Pos)], amount, GlassMoteT, Sfx.None);
                    if (p.GlassHealed >= p.GlassCap - 0.01f)
                        ShatterGlass(p);
                }
                else
                    BroadcastFx(FxKind.Spark, b.Pos, GlassColors[GlassPanelAt(p, b.Pos)], 10f, 0.25f, Sfx.None);
                return true;
            }
            return false;
        }

        void ShatterGlass(Player p)
        {
            if (p.GlassT <= 0f)
                return;
            BroadcastFx(FxKind.GlassShatter, p.GlassPos, new V2(p.GlassAim, 0f), GlassGold, GlassHalf(p), 0.9f, Sfx.FrostShatter);
            p.GlassT = 0f;
        }

        /// Host, every tick: panes run down and shatter; motes land and heal.
        void TickSanctumGlass(float dt)
        {
            foreach (var p in Players.Values)
            {
                if (p.GlassT <= 0f)
                    continue;
                if (p.Downed)
                {
                    p.GlassT = 0f;
                    continue;
                }
                p.GlassT -= dt;
                if (p.GlassT <= 0f)
                {
                    p.GlassT = 0.001f;
                    ShatterGlass(p);
                }
            }
            for (int i = glassMotes.Count - 1; i >= 0; i--)
            {
                var m = glassMotes[i];
                m.T -= dt;
                if (m.T > 0f)
                    continue;
                glassMotes.RemoveAt(i);
                Player to, from;
                if (Players.TryGetValue(m.Target, out to) && !to.Downed)
                    Heal(to, m.Amount, Players.TryGetValue(m.Caster, out from) ? from : null);
            }
        }
    }
}

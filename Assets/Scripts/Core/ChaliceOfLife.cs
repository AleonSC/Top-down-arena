using System;
using System.Collections.Generic;

namespace Arena
{
    /// Chalice of Life, the Healer Knight's fourth ultimate (key 3 on the lobby's Ultimate row; mockup Docs/Mockups/chalice-of-life.html).
    /// A big chalice drops in up to ChReach px toward your cursor and stands ChLife s. Every player can drink from it once by
    /// walking within ChDrink px: it clears their wounds, fills their health, adds ChOver HP past max (Player.OverHp, as a health
    /// shrine's) and puts up a ChShield HP shield for ChShieldT s (Player.ShieldHp). It is gone once everyone has drunk.
    /// Mastered: a ChShieldMastered HP shield for ChShieldTMastered s.
    /// Host logic; clients draw it from two effects (Game/ChaliceOfLifeLook.cs):
    ///   FxKind.ChaliceOfLife: one at the cast; Pos = the chalice, Pos2 = the caster, Radius = the caster's id, Dur = ChLife + ChFade.
    ///   FxKind.ChaliceSip: one per drink; Pos = the chalice, Pos2 = the drinker, Radius = the drinker's id, Dur = the chalice's
    ///     time left + ChFade. Counting them gives the liquid level; once every player has one, the chalice fades early.
    public partial class Game
    {
        public const float ChReach = 140f, ChLife = 12f, ChDrink = 50f, ChOver = 20f, ChShield = 40f, ChShieldT = 4f;
        public const float ChShieldMastered = 60f, ChShieldTMastered = 6f, ChFade = 0.6f, ChDropT = 0.35f;

        internal static readonly AbilityDef[] ChaliceUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.ChaliceOfLife, Kit = Kit.Knight, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Chalice of Life", Cooldown = 90f,
                Desc = "Summon a chalice for 12 s. Each player drinks from it once: a full heal that clears wounds, 20 health past max and a 40 shield for 4 s.",
                Capstone = "The shield is 60 and lasts 6 s.",
                Bot = b => b.AllyHurt && (b.Danger || b.AllyDown || b.HaveTarget),
            },
        };

        class Chalice
        {
            public int Owner;
            public V2 At;
            public float T;      // seconds since it was cast
            public bool Mastered;
            public readonly HashSet<int> Drank = new HashSet<int>();
        }

        readonly List<Chalice> chalices = new List<Chalice>();

        void CastChalice(Player p, V2 target, bool cap)
        {
            V2 d = target - p.Pos;
            float len = d.Length;
            V2 at = ClampToArena(len > ChReach ? p.Pos + d * (ChReach / len) : target, 30f);
            chalices.Add(new Chalice { Owner = p.Id, At = at, Mastered = cap });
            BroadcastFx(FxKind.ChaliceOfLife, at, p.Pos, Rgba.Gold, p.Id, ChLife + ChFade, Sfx.SpellHoly);
        }

        /// Host, every tick: players who walk up to a chalice they haven't drunk from drink; a chalice everyone drank from, or one
        /// past its time, is gone.
        void TickChalices(float dt)
        {
            for (int i = chalices.Count - 1; i >= 0; i--)
            {
                var c = chalices[i];
                c.T += dt;
                if (c.T >= ChDropT)  // (it has landed)
                    foreach (var q in Players.Values)
                        if (!q.Downed && !c.Drank.Contains(q.Id) && q.Pos.CloserThan(c.At, ChDrink + q.Def.BodyR))
                            DrinkChalice(c, q);
                bool empty = true;
                foreach (var q in Players.Values)
                    if (!c.Drank.Contains(q.Id))
                        empty = false;
                if (empty || c.T >= ChLife)
                    chalices.RemoveAt(i);
            }
        }

        void DrinkChalice(Chalice c, Player q)
        {
            c.Drank.Add(q.Id);
            float before = q.Hp;
            q.Wound = 0f;  // the chalice clears wounds
            q.Hp = Math.Max(q.Hp, q.HealCap);
            q.OverHp = Math.Max(q.OverHp, ChOver);
            q.ShieldHp = Math.Max(q.ShieldHp, c.Mastered ? ChShieldMastered : ChShield);
            q.ShieldT = Math.Max(q.ShieldT, c.Mastered ? ChShieldTMastered : ChShieldT);
            Player o;
            if (Players.TryGetValue(c.Owner, out o))
                o.Run.HealingDone += Math.Max(0f, q.Hp - before);
            BroadcastFx(FxKind.ChaliceSip, c.At, q.Pos, new Rgba(0.49f, 1f, 0.69f), q.Id, Math.Max(ChFade, ChLife - c.T) + ChFade, Sfx.Revive);
            DoLog("player " + q.Id + " drank from the Chalice of Life");
        }
    }
}

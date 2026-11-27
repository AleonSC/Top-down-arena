using System;
using System.Collections.Generic;

namespace Arena
{
    /// Pressure Point Press: the Healer Ninja's third ultimate (lobby key 2 of the Ultimate row; Blossom Storm stays the default).
    /// From its mockup (claude.ai artifact "Pressure Point Press"), on the cast's clock:
    ///   - 0 s, the Mark: a jade ring PppRange px round you tags the PppMax enemies nearest you (and stuns them where they stand
    ///     until they're pressed). They're visited nearest-next, starting from you.
    ///   - PppMark: you blink to each in turn, PppHop s apart, untouchable the whole cast. Each Press: PppDmg times your damage,
    ///     a PppStun s stun (bosses and heavies are slowed to PppHeavySlow instead), and poison, PppPoisonDps for PppPoisonTime s.
    ///     Each Press also sends a jade wisp to the most hurt teammate (you included): PppHeal.
    ///   - the Seal (PppSealAt): you blink back to where you cast, and every pressed enemy still standing is stunned PppSealStun s more.
    /// It stops if you go down or the wave ends. Mastered: PppMaxMastered targets.
    /// The Rare card Hundred Meridians: PppMeridianMore more targets and PppMeridianDmg harder presses, a stack.
    /// Host only: the runs (PppRuns) aren't replicated; your moves go out as teleports (MovePlayer). Every game draws it from
    /// FxKind.PressurePoint, whose Radius says which moment (PppFx*); drawn by Game/PressurePointPressLook.cs.
    public partial class Game
    {
        public const int PppMax = 8, PppMaxMastered = 12, PppMeridianMore = 2;
        public const float PppRange = 360f, PppMark = 0.45f, PppHop = 0.15f, PppSealHold = 0.3f;
        public const float PppDmg = 30f, PppStun = 2f, PppSealStun = 1f, PppHeavySlow = 0.5f;
        public const float PppPoisonDps = 8f, PppPoisonTime = 5f, PppHeal = 10f, PppMeridianDmg = 0.2f;
        // FxKind.PressurePoint's Radius: which moment
        public const int PppFxMark = 0, PppFxTarget = 1, PppFxDash = 2, PppFxPress = 3, PppFxWisp = 4, PppFxSeal = 5, PppFxSealLine = 6;
        public const float PppFxDashT = 0.35f, PppFxPressT = 0.5f, PppFxWispT = 0.5f, PppFxSealT = 0.7f;

        public static readonly Rgba PppJade = new Rgba(0.31f, 0.88f, 0.66f), PppGold = new Rgba(1f, 0.8f, 0.35f), PppPoison = new Rgba(0.61f, 0.89f, 0.29f);

        /// Pressure Point Press's entry: key 2 of the Healer Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] PressUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.PressurePointPress, Kit = Kit.Ninja, Role = Role.Healer, Choice = AbilityDef.Ult, UltKey = 2,
                Name = "Pressure Point Press", Cooldown = 70f,
                Desc = "Mark up to 8 enemies near you, then blink to each: it's stunned 2 s and poisoned, and each press heals your most hurt teammate.",
                Capstone = "Marks up to 12 enemies.",
                Bot = b => b.NearEnemies >= 3,
            },
        };

        /// One cast, on the host.
        public class PppRun
        {
            public int Owner;
            public float T;
            public V2 From, At;
            public int Done;
            public bool Sealed;
            public readonly List<int> Targets = new List<int>();
            public readonly List<int> Pressed = new List<int>();
        }

        public readonly List<PppRun> PppRuns = new List<PppRun>();

        /// The cast's clock for n targets: the Seal (back where you cast) and the end.
        public static float PppSealAt(int n) => PppMark + n * PppHop;  // (a hop after the last press)
        public static float PppEndAt(int n) => PppSealAt(n) + PppSealHold;
        /// How many enemies a cast marks.
        public static int PppTargets(Player p, bool mastered) => (mastered ? PppMaxMastered : PppMax) + p.Stats.PppMore;

        readonly List<Enemy> pppPool = new List<Enemy>();

        void CastPressurePoint(Player p, bool mastered)
        {
            var r = new PppRun { Owner = p.Id, From = p.Pos, At = p.Pos };
            pppPool.Clear();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(p.Pos, PppRange + e.Radius))
                    pppPool.Add(e);
            pppPool.Sort((a, b) => a.Pos.DistanceSq(p.Pos).CompareTo(b.Pos.DistanceSq(p.Pos)));
            if (pppPool.Count > PppTargets(p, mastered))
                pppPool.RemoveRange(PppTargets(p, mastered), pppPool.Count - PppTargets(p, mastered));
            V2 cur = p.Pos;
            while (pppPool.Count > 0)  // nearest-next, from you
            {
                int best = 0;
                for (int k = 1; k < pppPool.Count; k++)
                    if (pppPool[k].Pos.DistanceSq(cur) < pppPool[best].Pos.DistanceSq(cur))
                        best = k;
                var e = pppPool[best];
                pppPool.RemoveAt(best);
                r.Targets.Add(e.Id);
                cur = e.Pos;
            }
            int n = r.Targets.Count;
            for (int i = 0; i < n; i++)
            {
                var e = Enemies[r.Targets[i]];
                float until = PppMark + (i + 1) * PppHop;
                if (!Heavy(e))
                    Stun(e, until);  // marked: it stands still until its press (a Stun, so no ice look)
                BroadcastFx(FxKind.PressurePoint, e.Pos, e.Pos, PppJade, PppFxTarget, until, Sfx.None);
            }
            PppRuns.Add(r);
            p.UltT = PppEndAt(n);
            p.InvulnT = Math.Max(p.InvulnT, PppEndAt(n));
            BroadcastFx(FxKind.PressurePoint, p.Pos, p.Pos, PppJade, PppFxMark, PppMark + 0.3f, Sfx.NinjaVanish);
        }

        /// Host, every tick: each cast's presses and Seal on its clock.
        void TickPressurePoint(float dt)
        {
            for (int i = PppRuns.Count - 1; i >= 0; i--)
            {
                var r = PppRuns[i];
                Player p;
                if (!Players.TryGetValue(r.Owner, out p) || !Battle || p.Downed)
                {
                    PppRuns.RemoveAt(i);
                    continue;
                }
                r.T += dt;
                while (r.Done < r.Targets.Count && r.T >= PppMark + r.Done * PppHop)
                    PppPress(r, p, r.Targets[r.Done++]);
                if (!r.Sealed && r.T >= PppSealAt(r.Targets.Count))
                    PppSeal(r, p);
                if (r.T >= PppEndAt(r.Targets.Count))
                    PppRuns.RemoveAt(i);
            }
        }

        /// One press: blink beside the enemy, strike its pressure point, send a wisp to the most hurt teammate.
        void PppPress(PppRun r, Player p, int id)
        {
            Enemy e;
            if (!Enemies.TryGetValue(id, out e) || !Targetable(e))
                return;  // (it died on the way: the next one is pressed on time)
            V2 d = e.Pos - r.At;
            d = d.Length < 1f ? V2.FromAngle(p.Aim) : d.Normalized();
            V2 land = TeleportLanding(r.At, e.Pos - d * (e.Radius + p.Def.BodyR + 4f), p.Def.BodyR);
            BroadcastFx(FxKind.PressurePoint, r.At, land, PppJade, PppFxDash, PppFxDashT, Sfx.None);
            MovePlayer(p, land);
            r.At = land;
            BroadcastFx(FxKind.PressurePoint, e.Pos, land, PppGold, PppFxPress, PppFxPressT, Sfx.NinjaStepStrike);
            HurtEnemy(e, PppDmg * p.Stats.DmgMult * p.Stats.PppDmgMult, p.Id);
            p.PppPresses++;
            if (e.Hp > 0f)
            {
                if (Heavy(e))
                    Slow(e, PppStun, PppHeavySlow);
                else
                    Stun(e, PppStun);
                e.PoisonDps = Math.Max(e.PoisonDps, PppPoisonDps * p.Stats.DmgMult);
                e.PoisonT = Math.Max(e.PoisonT, PppPoisonTime);
                e.PoisonBy = p.Id;
                r.Pressed.Add(id);
            }
            Player hurt = null;
            foreach (var q in Players.Values)
                if (!q.Downed && q.Hp < q.MaxHp && (hurt == null || q.Hp / q.MaxHp < hurt.Hp / hurt.MaxHp))
                    hurt = q;
            if (hurt != null)
            {
                BroadcastFx(FxKind.PressurePoint, e.Pos, hurt.Pos, PppJade, PppFxWisp, PppFxWispT, Sfx.None);
                Heal(hurt, PppHeal, p);
            }
        }

        /// The Seal: back where you cast; every pressed enemy still standing is stunned a while longer.
        void PppSeal(PppRun r, Player p)
        {
            r.Sealed = true;
            V2 home = TeleportLanding(r.At, r.From, p.Def.BodyR);
            if (!(home - r.At).IsZero)
                BroadcastFx(FxKind.PressurePoint, r.At, home, PppJade, PppFxDash, PppFxDashT, Sfx.None);
            MovePlayer(p, home);
            r.At = home;
            BroadcastFx(FxKind.PressurePoint, home, home, PppJade, PppFxSeal, PppFxSealT, Sfx.NinjaVanish);
            foreach (int id in r.Pressed)
            {
                Enemy e;
                if (!Enemies.TryGetValue(id, out e) || e.Hp <= 0f)
                    continue;
                if (!Heavy(e))
                    Stun(e, Math.Max(e.StunT, 0f) + PppSealStun);
                BroadcastFx(FxKind.PressurePoint, home, e.Pos, PppJade, PppFxSealLine, PppFxSealT, Sfx.None);
            }
        }
    }
}

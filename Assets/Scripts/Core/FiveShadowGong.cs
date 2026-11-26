using System;
using System.Collections.Generic;

namespace Arena
{
    /// Five Shadow Gong: the Tank Ninja's fourth ultimate (lobby key 3 of the Ultimate row; Thousand Clones stays the default).
    /// From its mockup (claude.ai artifact "Five Shadow Gong"), on the cast's clock:
    ///   - 0 s: a bronze gong drops in FsgGongOff px ahead of you (it lands at FsgLand).
    ///   - FsgStrike: you strike it. A gold shockwave; every enemy within FsgRange that isn't a boss or heavy is stunned until the
    ///     sheathe. The FsgShadows * FsgPer enemies nearest you are picked (with fewer, the spare cuts go round them again),
    ///     sorted by their angle round you and split into FsgShadows slices.
    ///   - FsgSpawn: a shadow ninja appears FsgRing px out toward each slice.
    ///   - FsgChain: every shadow cuts its slice, nearest first, FsgHop s a cut (FsgStag s between the shadows), landing FsgOver px
    ///     past each enemy.
    ///   - the sheathe (FsgSheatheAt): every cut lands at once, FsgCut each (FsgHeavyCut on bosses and heavies), times your damage.
    ///   - FsgDepartAt: each shadow drops a smoke grenade and vanishes as it pops (FsgPop later). The cloud (ZoneKind.GongSmoke,
    ///     FsgSmokeR round, FsgSmokeLife s) slows enemies in it FsgSmokeSlow, moving and shooting (Enemy.FsgSmokeT).
    /// It plays out even if you go down; it stops when the wave ends. Mastered: each shadow cuts FsgPerMastered.
    /// The Rare card Temple Bronze: cuts FsgBronzeDmg harder and the smoke FsgBronzeSmoke wider, a stack.
    /// Host only: the runs (FsgRuns) aren't replicated. Every game draws it from three effects (Game/FiveShadowGongLook.cs):
    /// FxKind.GongShow (the gong and the strike), GongCut (a shadow appearing or cutting) and GongSplit (an enemy cut at the sheathe).
    public partial class Game
    {
        public const int FsgShadows = 5, FsgPer = 3, FsgPerMastered = 4;
        public const float FsgLand = 0.55f, FsgStrike = 0.95f, FsgSpawn = 1.05f, FsgChain = 1.3f, FsgHop = 0.13f, FsgStag = 0.025f;
        public const float FsgSheatheGap = 0.4f, FsgDepartGap = 0.4f, FsgPop = 0.15f, FsgGongFade = 0.6f;
        public const float FsgGongOff = 95f, FsgRange = 900f, FsgRing = 100f, FsgOver = 30f, FsgShockR = 520f;
        public const float FsgCut = 120f, FsgHeavyCut = 60f;
        public const float FsgSmokeR = 90f, FsgSmokeLife = 4f, FsgSmokeSlow = 0.4f, FsgSmokeHold = 0.3f;
        public const float FsgBronzeDmg = 0.2f, FsgBronzeSmoke = 0.25f;  // the Rare card Temple Bronze, a stack
        public const int FsgBotCrowd = 5;

        public static readonly Rgba FsgGold = new Rgba(0.91f, 0.71f, 0.29f), FsgLilac = new Rgba(0.72f, 0.61f, 1f), FsgInk = new Rgba(0.08f, 0.07f, 0.12f);

        /// Five Shadow Gong's entry: key 3 of the Tank Ninja's Ultimate row. Upgrades.Abilities gathers it.
        internal static readonly AbilityDef[] GongUltimates =
        {
            new AbilityDef
            {
                Id = AbilityId.FiveShadowGong, Kit = Kit.Ninja, Role = Role.Tank, Choice = AbilityDef.Ult, UltKey = 3,
                Name = "Five Shadow Gong", Cooldown = 70f,
                Desc = "Strike a summoned gong: enemies near you freeze, five shadow ninjas each cut 3 of them, and the cuts land at once. The shadows leave slowing smoke.",
                Capstone = "Each shadow cuts 4 enemies.",
                Bot = b => b.NearEnemies >= FsgBotCrowd,
            },
        };

        /// One cast, on the host.
        public class FsgRun
        {
            public int Owner, Per;
            public float T;
            public V2 Gong, From;
            public bool Planned, Spawned, Sheathed, Departed;
            public readonly V2[] At = new V2[FsgShadows];
            public readonly List<int>[] Targets = new List<int>[FsgShadows];
            public readonly int[] Done = new int[FsgShadows];
            public readonly Dictionary<int, int> Hits = new Dictionary<int, int>();  // enemy id -> cuts on it
            public readonly Dictionary<int, V2> Dir = new Dictionary<int, V2>();     // enemy id -> the way its last cut went
            public readonly Dictionary<int, V2> Seen = new Dictionary<int, V2>();    // enemy id -> where it stood at the strike
        }

        public readonly List<FsgRun> FsgRuns = new List<FsgRun>();

        /// The cast's clock: the sheathe, the grenades, the shadows gone.
        public static float FsgSheatheAt(int per) => FsgChain + (per - 1) * FsgHop + (FsgShadows - 1) * FsgStag + FsgSheatheGap;
        public static float FsgDepartAt(int per) => FsgSheatheAt(per) + FsgDepartGap;
        public static float FsgEndAt(int per) => FsgDepartAt(per) + FsgPop;

        void CastShadowGong(Player p, bool mastered)
        {
            int per = mastered ? FsgPerMastered : FsgPer;
            var r = new FsgRun { Owner = p.Id, Per = per, Gong = ClampToArena(p.Pos + V2.FromAngle(p.Aim) * FsgGongOff, 40f) };
            for (int s = 0; s < FsgShadows; s++)
                r.Targets[s] = new List<int>();
            FsgRuns.Add(r);
            p.UltT = FsgEndAt(per);
            BroadcastFx(FxKind.GongShow, r.Gong, p.Pos, FsgGold, p.Id, FsgEndAt(per) + FsgGongFade, Sfx.NinjaVanish);
        }

        /// Host, every tick: each cast's strike, shadows, cuts, sheathe and smoke on its clock.
        void TickShadowGong(float dt)
        {
            for (int i = FsgRuns.Count - 1; i >= 0; i--)
            {
                var r = FsgRuns[i];
                Player p;
                if (!Players.TryGetValue(r.Owner, out p) || !Battle)
                {
                    FsgRuns.RemoveAt(i);
                    continue;
                }
                r.T += dt;
                if (!r.Planned && r.T >= FsgStrike)
                    FsgPlan(r, p);
                if (!r.Spawned && r.T >= FsgSpawn)
                {
                    r.Spawned = true;
                    for (int s = 0; s < FsgShadows; s++)
                        BroadcastFx(FxKind.GongCut, r.At[s], r.At[s], FsgLilac, p.Id * 10 + s, FsgEndAt(r.Per) - r.T, s == 0 ? Sfx.NinjaVanish : Sfx.None);
                }
                for (int s = 0; s < FsgShadows; s++)
                    while (r.Done[s] < r.Targets[s].Count && r.T >= FsgChain + r.Done[s] * FsgHop + s * FsgStag)
                        FsgCutOne(r, p, s);
                if (!r.Sheathed && r.T >= FsgSheatheAt(r.Per))
                    FsgSheathe(r, p);
                if (!r.Departed && r.T >= FsgDepartAt(r.Per))
                {
                    r.Departed = true;
                    for (int s = 0; s < FsgShadows; s++)
                        AddZone(ZoneKind.GongSmoke, p, r.At[s], FsgSmokeR * p.Stats.FsgSmokeMult, FsgPop + FsgSmokeLife);
                    BroadcastFx(FxKind.Ring, r.From, r.From, FsgLilac, 10f, 0.05f, Sfx.NinjaVanish);
                }
                if (r.T >= FsgEndAt(r.Per))
                    FsgRuns.RemoveAt(i);
            }
        }

        readonly List<Enemy> fsgPool = new List<Enemy>();
        readonly List<Enemy> fsgList = new List<Enemy>();

        /// The strike: stun the room, pick the cuts and place the shadows.
        void FsgPlan(FsgRun r, Player p)
        {
            r.Planned = true;
            r.From = p.Pos;
            V2 from = p.Pos;
            fsgPool.Clear();
            foreach (var e in Enemies.Values)
                if (Targetable(e) && e.Pos.CloserThan(from, FsgRange))
                {
                    fsgPool.Add(e);
                    if (!Heavy(e))
                        Stun(e, FsgSheatheAt(r.Per) - r.T + 0.05f);
                }
            fsgPool.Sort((a, b) => a.Pos.DistanceSq(from).CompareTo(b.Pos.DistanceSq(from)));
            int total = FsgShadows * r.Per;
            fsgList.Clear();
            for (int k = 0; k < total && fsgPool.Count > 0; k++)
                fsgList.Add(fsgPool[k % fsgPool.Count]);  // fewer enemies: the spare cuts go round them again, nearest first
            fsgList.Sort((a, b) =>
            {
                int c = (a.Pos - from).Angle.CompareTo((b.Pos - from).Angle);
                return c != 0 ? c : a.Id.CompareTo(b.Id);
            });
            int n = fsgList.Count;
            for (int s = 0; s < FsgShadows; s++)
            {
                V2 sum = V2.Zero;
                var slice = new List<Enemy>();
                for (int k = 0; k < n; k++)
                    if (k * FsgShadows / n == s)
                    {
                        slice.Add(fsgList[k]);
                        sum += (fsgList[k].Pos - from).Normalized();
                    }
                float ang = slice.Count > 0 && !sum.IsZero ? sum.Angle : p.Aim + s * MathX.Tau / FsgShadows;
                r.At[s] = ClampToArena(from + V2.FromAngle(ang) * FsgRing, 20f);
                V2 cur = r.At[s];
                while (slice.Count > 0)  // nearest first
                {
                    int best = 0;
                    for (int k = 1; k < slice.Count; k++)
                        if (slice[k].Pos.DistanceSq(cur) < slice[best].Pos.DistanceSq(cur))
                            best = k;
                    var e = slice[best];
                    slice.RemoveAt(best);
                    r.Targets[s].Add(e.Id);
                    r.Seen[e.Id] = e.Pos;
                    cur = e.Pos;
                }
            }
            BroadcastFx(FxKind.Ring, r.Gong, r.Gong, FsgGold, FsgShockR, 0.6f, Sfx.BossGong);
        }

        /// One shadow's next cut: it flashes through the enemy and lands just past it.
        void FsgCutOne(FsgRun r, Player p, int s)
        {
            int id = r.Targets[s][r.Done[s]++];
            Enemy e;
            V2 at = Enemies.TryGetValue(id, out e) && e.Hp > 0f ? e.Pos : r.Seen[id];
            V2 d = at - r.At[s];
            d = d.Length < 1f ? V2.FromAngle(p.Aim + s) : d.Normalized();
            V2 land = ClampToArena(at + d * FsgOver, 20f);
            BroadcastFx(FxKind.GongCut, r.At[s], land, FsgGold, p.Id * 10 + s, FsgEndAt(r.Per) - r.T, s == 0 ? Sfx.NinjaStepStrike : Sfx.None);
            r.At[s] = land;
            int hits;
            r.Hits.TryGetValue(id, out hits);
            r.Hits[id] = hits + 1;
            r.Dir[id] = d;
            p.FsgCuts++;
        }

        readonly List<int> fsgIds = new List<int>();

        /// The sheathe: every cut lands at once.
        void FsgSheathe(FsgRun r, Player p)
        {
            r.Sheathed = true;
            fsgIds.Clear();
            fsgIds.AddRange(r.Hits.Keys);
            bool first = true;
            foreach (int id in fsgIds)
            {
                Enemy e;
                if (!Enemies.TryGetValue(id, out e) || e.Hp <= 0f)
                    continue;
                float dmg = r.Hits[id] * (Heavy(e) ? FsgHeavyCut : FsgCut) * p.Stats.DmgMult * p.Stats.FsgDmgMult;
                V2 pos = e.Pos;
                BroadcastFx(FxKind.GongSplit, pos, pos + r.Dir[id], e.Def.Color, e.Radius, 0.9f, first ? Sfx.KatanaStrikeCut : Sfx.None);
                first = false;
                HurtEnemy(e, dmg, p.Id);
                if (e.Hp <= 0f)
                    p.FsgKills++;
            }
        }
    }
}

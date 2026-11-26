using System;
using System.Linq;
using Arena;

// Boss stage tables (Core/BossStages.cs, Docs/DifficultyTiers.md step 2): every boss's stages on each mechanics tier, and a
// fight following the table of its run's tier.
static partial class Program
{
    static void BossStagesTest(float dt)
    {
        // --- The tables --------------------------------------------------------------------------------------------------
        var bosses = new[] { EnemyType.Warden, EnemyType.HiveQueen, EnemyType.ReaperOfDusk, EnemyType.Singularity, EnemyType.Colossus,
            EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse };
        bool ordered = true, hardSame = true, easyShort = true;
        foreach (var b in bosses)
        {
            var normal = Game.StagesFor(b, MechTier.Normal);
            ordered &= normal.Length >= 3 && normal[0].At == 1f && normal[0].Stage == 0;
            for (int i = 1; i < normal.Length; i++)
                ordered &= normal[i].At < normal[i - 1].At && normal[i].Stage == i;  // (today the stages run in their enum's order)
            foreach (var t in new[] { MechTier.Hard, MechTier.Nightmare })
                hardSame &= Game.StagesFor(b, t).SequenceEqual(normal);
            var easy = Game.StagesFor(b, MechTier.Easy);
            easyShort &= b == EnemyType.ReaperOfDusk ? easy.SequenceEqual(normal) : easy.SequenceEqual(normal.Take(normal.Length - 1));
        }
        Check(ordered, "every boss has a stage table: it starts at full health and each stage starts lower than the last");
        Check(hardSame, "Hard and Nightmare use Normal's stages (no extra stages yet)");
        Check(easyShort, "Easy has every stage but the last stand (the Twin Reapers have none, so theirs is the same)");
        Check(Game.StagesFor(EnemyType.ReaperOfDawn, MechTier.Normal) == Game.StagesFor(EnemyType.ReaperOfDusk, MechTier.Normal)
              && Game.StagesFor(EnemyType.Grunt, MechTier.Normal).Length == 0,
            "both Reapers share one table; an enemy that isn't a boss has none");

        // Normal is the fight as balanced: the thresholds it had before the tables.
        Func<EnemyType, byte, float> at = (b, st) => Game.StageAt(b, st, MechTier.Normal);
        Check(at(EnemyType.Warden, (byte)WardenStage.Lockdown) == Game.LockdownAt && at(EnemyType.Warden, (byte)WardenStage.Riot) == Game.RiotAt
              && at(EnemyType.Warden, (byte)WardenStage.LastStand) == Game.LastStandAt
              && at(EnemyType.HiveQueen, (byte)QueenStage.Honeycomb) == 0.65f && at(EnemyType.HiveQueen, (byte)QueenStage.LastStand) == 0.1f
              && at(EnemyType.Eclipse, (byte)EclipseStage.Penumbra) == 0.8f && at(EnemyType.Eclipse, (byte)EclipseStage.Totality) == 0.05f,
            "Normal keeps today's thresholds (Warden 65/30/10%, Queen 65/30/10%, Eclipse 80% ... Totality 5%)");
        var queenN = Game.StagesFor(EnemyType.HiveQueen, MechTier.Normal);
        Check(Game.StageAt(EnemyType.Warden, (byte)WardenStage.LastStand, MechTier.Easy) == -1f
              && queenN[3].How == StageEntry.Instant && queenN[1].How == StageEntry.Timed
              && Game.StagesFor(EnemyType.Colossus, MechTier.Normal).Skip(1).All(r => r.How == StageEntry.Stagger),
            "a stage a tier doesn't have reads -1; each row says how the boss goes in (timed change, straight away, a stagger)");

        // --- A fight on each tier: the Hive Queen ------------------------------------------------------------------------
        foreach (var tier in new[] { MechTier.Normal, MechTier.Easy })
        {
            var s = StartBuffed(dt, 10, 0, h => h.Diff.Set(DiffKnob.Mechanics, (float)tier));
            var g = s[0].Game; var cg = s[1].Game;
            g.Bot = cg.Bot = false;
            g.DevSetGod(1, true);
            g.DevSetGod(2, true);
            Run(s, dt, 12, () => { var b = Find(s[0], EnemyType.HiveQueen); return b != null && b.SpawnT <= 0f; });
            var q = Find(s[0], EnemyType.HiveQueen);
            Check(q != null && g.MechTier == tier && cg.MechTier == tier, tier + ": wave 10 brings the Hive Queen, and both games know the tier");
            if (q == null) { s[0].Leave(""); s[1].Leave(""); continue; }
            q.Hp = q.MaxHp * 0.64f;
            Run(s, dt, 2.6f);
            bool honey = q.Queen.Stage == QueenStage.Honeycomb;
            q.Hp = q.MaxHp * 0.29f;
            Run(s, dt, 2.6f);
            bool frenzy = q.Queen.Stage == QueenStage.Frenzy;
            q.Hp = q.MaxHp * 0.09f;
            Run(s, dt, 6f, () => q.Queen.Stage == QueenStage.LastStand, () => { if (q.Hp > q.MaxHp * 0.09f) q.Hp = q.MaxHp * 0.09f; });
            bool last = q.Queen.Stage == QueenStage.LastStand;
            Run(s, dt, 0.5f, null, () => { if (q.Hp > q.MaxHp * 0.09f) q.Hp = q.MaxHp * 0.09f; });  // (the next snapshots reach the client)
            var cq = cg.Enemies.Values.FirstOrDefault(e => e.Type == EnemyType.HiveQueen);
            Console.WriteLine("  {0}: honeycomb {1}, frenzy {2}, last stand {3}", tier, honey, frenzy, last);
            if (tier == MechTier.Normal)
                Check(honey && frenzy && last && cq != null && cq.Queen != null && cq.Queen.Stage == QueenStage.LastStand,
                    "Normal: the Queen goes Swarm, Honeycomb, Frenzy, then her Last Stand below 10% (the client sees it)");
            else
            {
                // Hit her until she dies (a hit can land while she can't be hurt: a dive, her drones' guard).
                var hurt = typeof(Game).GetMethod("HurtEnemy", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                Run(s, dt, 8f, () => !g.Enemies.ContainsKey(q.Id) || q.Hp <= 0f, () =>
                {
                    if (!g.Enemies.ContainsKey(q.Id) || q.Hp <= 0f)
                        return;
                    q.Hp = Math.Min(q.Hp, 1f);
                    hurt.Invoke(g, new object[] { q, 50f, 1 });
                });
                Console.WriteLine("  Easy: stage at the end {0}, health {1:0.0}", q.Queen.Stage, q.Hp);
                Check(honey && frenzy && !last && cq != null && cq.Queen != null && cq.Queen.Stage == QueenStage.Frenzy,
                    "Easy: the same stages but no Last Stand: she stays in Frenzy below 10% (the client too)");
                Check(!g.Enemies.ContainsKey(q.Id) || q.Hp <= 0f, "Easy: she still dies without her Last Stand");
            }
            s[0].Leave(""); s[1].Leave("");
        }
    }
}

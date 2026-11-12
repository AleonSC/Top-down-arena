using System;
using Arena;

// The boss schedules (Core/BossSchedule.cs, Docs/QuickPlay.md step 0): the Campaign's and a hunt's give the same
// answers as the every-fifth-wave formulas they replaced.
static partial class Program
{
    static readonly EnemyType[] OldCycle =
    {
        EnemyType.Boss, EnemyType.HiveQueen, EnemyType.TwinA, EnemyType.Singularity, EnemyType.Colossus,
        EnemyType.Duelist, EnemyType.Lich, EnemyType.StormHerald, EnemyType.Devourer, EnemyType.Eclipse,
    };

    static void BossScheduleTest()
    {
        var c = BossSchedule.Campaign;
        bool same = c.Slotted && c.FirstBossWave == 5;
        for (int w = 0; w <= 120; w++)
        {
            same &= c.IsBossWave(w) == (w > 0 && w % 5 == 0);
            same &= c.BossAt(w) == OldCycle[(Math.Max(5, w) / 5 - 1) % OldCycle.Length];
            same &= c.NextBossWaveFrom(w) == Math.Max(5, (w + 4) / 5 * 5);
            same &= c.ApproachStep(w) == (w <= 0 ? 0 : Math.Min(4, (w - 1) % 5 + 1));
            same &= c.FinaleWave(w) == ((w - 1) % 5 == 4);
            same &= c.LastBossWave(w) == w - w % 5;
        }
        Check(same, "the Campaign's boss schedule matches the old every-fifth-wave formulas (waves 0-120)");

        var g = new Session().Game;
        Check(g.Bosses == BossSchedule.Campaign, "a run without a hunt uses the Campaign's schedule");
        g.Hunt = Hunts.Get(EnemyType.Boss);
        var h = g.Bosses;
        bool hunt = !h.Slotted && h.FirstBossWave == Hunts.Length;
        for (int w = 0; w <= Hunts.Length; w++)
        {
            hunt &= h.IsBossWave(w) == (w >= Hunts.Length) && h.BossAt(w) == EnemyType.Boss && h.NextBossWaveFrom(w) == Hunts.Length;
            hunt &= h.ApproachStep(w) == (w >= Hunts.Length ? 0 : Hunts.Step(w)) && h.FinaleWave(w) == Hunts.FinaleWave(w);
        }
        Check(hunt && g.Bosses == h, "a Boss Hunt's schedule: only its boss, on its last wave (and the same object each time)");
    }
}

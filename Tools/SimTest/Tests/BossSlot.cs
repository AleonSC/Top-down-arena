using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using Arena;

// The boss between waves (Hunts.cs: BossSlot): a boss follows every fifth regular wave without using up a wave number.
static partial class Program
{
    static void BossSlotTest(float dt)
    {
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 5 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        g.DevSetGod(1, true); g.DevSetGod(2, true);
        Action NoPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Action KillAll = () => { foreach (var e in g.Enemies.Values) e.Hp = 0f; };
        Func<bool> bossUp = () => g.Enemies.Values.Any(e => e.Def.IsBoss);

        // Wave 5 is a regular wave: no boss yet.
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Enemies.Count > 0);
        Check(g.Wave == 5 && !g.BossSlot && !g.BossNow && !bossUp() && !client.Game.BossNow, "wave 5 is a regular wave now: the boss isn't in it");
        Run(s, dt, 40f, () => g.State == MatchState.Intermission, KillAll);
        NoPicks();
        Run(s, dt, 1f, null, NoPicks);
        Check(g.Wave == 5 && g.BossNext && !g.BossSlot && client.Game.BossNext, "clearing wave 5 puts the boss next, on both screens (the wave number holds)");
        Check(g.NextBossWave(g.Wave, false) == 5 && g.PathOffer == null, "the next boss is wave 5's, and no paths are offered yet");

        // The boss comes as its own fight and keeps wave 5.
        Run(s, dt, 30f, () => g.State == MatchState.Fighting && Find(client, EnemyType.Boss) != null, NoPicks);
        Check(g.Wave == 5 && g.BossSlot && g.BossNow && !g.BossNext && client.Game.BossSlot && Find(client, EnemyType.Boss) != null, "then the Warden: still wave 5, a boss fight on both screens");
        Check(g.ApproachShare(5) == 0f && !g.Props.Values.Any(), "the boss fight has no approach and no props");
        Run(s, dt, 60f, () => g.State == MatchState.Intermission, KillAll);
        NoPicks();
        Run(s, dt, 1f, null, NoPicks);
        Check(g.BossNow && !g.BossNext && g.PathOffer != null, "beating the boss offers the paths");
        Check(g.NextBossWave(g.Wave, false) == 10, "and the next boss is wave 10's");
        g.ChoosePath(0);
        Check(g.PathFrom == 6 && g.PathTo == 10, "a path covers the five regular waves 6-10");
        g.ChoosePath(2);
        g.CurrentPath = PathId.None;
        Run(s, dt, 30f, () => g.State == MatchState.Fighting && g.Wave == 6, NoPicks);
        Run(s, dt, 0.5f, null, NoPicks);  // (the snapshot catches up)
        Check(g.Wave == 6 && !g.BossSlot && !bossUp() && !client.Game.BossSlot, "wave 6 follows the boss as the next regular wave");
        host.Leave(""); client.Leave("");

        // The save: a run saved after the boss resumes on the next regular wave, one saved before it resumes at the boss; old saves guess from the wave.
        var save = new RunSave { Wave = 5, BossSlot = true };
        save.Seats.Add(new SeatSave());
        var back = RunSave.Parse(save.ToText());
        Check(back != null && back.Wave == 5 && back.BossSlot, "a save after the boss remembers it");
        save.BossSlot = false;
        back = RunSave.Parse(save.ToText());
        Check(back != null && back.Wave == 5 && !back.BossSlot && back.Summary.StartsWith("Boss 5"), "a save before the boss remembers that, and says so");
        string old = save.ToText().Replace("slot 0\n", "");
        back = RunSave.Parse(old);
        Check(back != null && back.BossSlot, "a save from before the boss slot at a multiple of 5 counts its boss as beaten");
        save.Wave = 7;
        back = RunSave.Parse(save.ToText().Replace("slot 0\n", ""));
        Check(back != null && !back.BossSlot, "and one at any other wave doesn't");

        // The Boss Lab starts at the boss with a negative start wave.
        port = nextPort++;
        var lab = new Session { AutoStart = 1, StartWave = -10 };
        lab.Host(port, "Lab", Role.Dps);
        lab.StartMatch();
        for (int f = 0; f < 60 * 20 && !(lab.Game.State == MatchState.Fighting && lab.Game.Enemies.Values.Any(e => e.Def.IsBoss)); f++)
            Step(lab, dt);
        Check(lab.Game.Wave == 10 && lab.Game.BossSlot && lab.Game.Enemies.Values.Any(e => e.Def.IsBoss), "a negative start wave starts at the boss after that wave");
        lab.Leave("");
    }
}

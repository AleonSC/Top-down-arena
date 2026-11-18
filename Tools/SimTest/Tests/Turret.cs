using System;
using System.Linq;
using Arena;

static partial class Program
{
    // The Black Market's Turret (Turret.cs): placed from the stash in the break, it shoots the nearest enemy in a wave with its owner's
    // shots, fights Market.TurretWaves waves and goes; each player has Market.TurretsEach up at most; a client places through the host.
    static void TurretTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        Check(Market.Names[(int)BuildKind.Turret] == "Turret" && Market.Prices[(int)BuildKind.Turret] == 120, "the Black Market sells the Turret for 120 in the Spike trap's old slot");
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 6 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting && g.Wave == 6, noPicks);
        foreach (var gg in new[] { g, cg }) { gg.Diff.Set(DiffKnob.ArenaExpanding, 1f); gg.Expanded.Clear(); gg.Nodes.Clear(); gg.ExpandOpen = true; }
        Run(s, dt, 0.6f, () => cg.Expanded.Count == 4, noPicks);  // (the Grid core)
        foreach (var e in g.Enemies.Values.ToList()) e.Hp = 0f;
        Run(s, dt, 0.3f, null, noPicks);

        // The friend places one from their stash: it is theirs, with 5 waves, on both screens.
        var friend = g.Players[2];
        friend.Stash[(int)BuildKind.Turret] = 2;
        V2 spot = V2.Zero; string why = null; bool ok = false;
        var spots = new System.Collections.Generic.List<V2>();
        foreach (var ar in g.ExpansionWorld.Areas)
            for (float ty = ar.Min.Y; ty <= ar.Max.Y; ty += 100f)
                for (float tx = ar.Min.X; tx <= ar.Max.X; tx += 100f)
                    if (Expansion.PlaceSpot(g.ExpansionWorld, g.Placed, new V2(tx, ty), out why) && spots.All(q => !q.CloserThan(new V2(tx, ty), 250f)))
                        spots.Add(new V2(tx, ty));
        ok = spots.Count >= 4;
        spot = spots[0];
        PollBoth(host, client);
        cg.PlaceBuild(BuildKind.Turret, (int)spot.X, (int)spot.Y);
        Run(s, dt, 0.6f, () => cg.Placed.Count == 1, noPicks);
        Check(ok && g.Placed.Count == 1 && cg.Placed.Count == 1 && g.Placed[0].Kind == BuildKind.Turret && g.Placed[0].Owner == 2 && cg.Placed[0].Waves == Market.TurretWaves
              && friend.Stash[(int)BuildKind.Turret] == 1, "the friend's turret stands on both screens, theirs, with " + Market.TurretWaves + " waves");

        // At most 3 each.
        g.Placed.Add(new PlacedBuild { Kind = BuildKind.Turret, X = (short)spots[1].X, Y = (short)spots[1].Y, Owner = 2, Waves = 5 });
        g.Placed.Add(new PlacedBuild { Kind = BuildKind.Turret, X = (short)spots[2].X, Y = (short)spots[2].Y, Owner = 2, Waves = 5 });
        var placeAt = typeof(Game).GetMethod("HostPlaceBuild", flags);
        placeAt.Invoke(g, new object[] { 2, BuildKind.Turret, (int)spots[3].X, (int)spots[3].Y });
        Check(g.TurretsOf(2) == Market.TurretsEach && friend.Stash[(int)BuildKind.Turret] == 1, "a fourth turret of the same player is refused");
        g.Players[1].Stash[(int)BuildKind.Turret] = 1;
        placeAt.Invoke(g, new object[] { 1, BuildKind.Turret, (int)spots[3].X, (int)spots[3].Y });
        Check(g.TurretsOf(1) == 1, "another player can still place theirs");
        g.Placed.RemoveRange(1, g.Placed.Count - 1);

        // In a wave it shoots an enemy in range, as its owner's shots; out of range it holds fire.
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var far = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, spot + new V2(Game.TurretRange + 400f, 0f), 0f, 0, false });
        far.Pos = spot + new V2(Game.TurretRange + 400f, 0f);
        Run(s, dt, 0.4f, null, () => { far.Pos = spot + new V2(Game.TurretRange + 400f, 0f); far.Hp = far.MaxHp; noPicks(); });
        Check(!g.Bullets.Values.Any(b => b.Kind == Defs.TurretShot && b.Owner == 2), "with no enemy in range it holds fire");
        var near = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, spot + new V2(250f, 0f), 0f, 0, false });
        float hp0 = near.Hp;
        bool shot = false;
        Run(s, dt, 1.5f, null, () => { near.Pos = spot + new V2(250f, 0f); far.Hp = far.MaxHp; shot |= g.Bullets.Values.Any(b => b.Kind == Defs.TurretShot && b.Owner == 2); noPicks(); });
        Check(shot && near.Hp < hp0, "in a wave it shoots the enemy in range, as the friend's shots (" + hp0.ToString("0") + " -> " + near.Hp.ToString("0") + " hp)");
        Check(Game.TurretDamage(20) > Game.TurretDamage(6), "its shots hit harder in later waves");
        near.Hp = 0f; far.Hp = 0f;

        // It fights 5 waves, a pip less each, and goes after the last.
        var age = typeof(Game).GetMethod("AgeTurrets", flags);
        for (int k = 0; k < Market.TurretWaves - 1; k++) age.Invoke(g, null);
        Run(s, dt, 0.4f, null, noPicks);
        Check(g.Placed.Count == 1 && g.Placed[0].Waves == 1 && cg.Placed[0].Waves == 1, "each cleared wave takes one off (1 left, on both screens)");
        age.Invoke(g, null);
        Run(s, dt, 0.4f, () => cg.Placed.Count == 0, noPicks);
        Check(g.Placed.Count == 0 && cg.Placed.Count == 0, "after its 5th wave it goes");

        // Saves keep a turret's owner and waves; an old save's Spike trap is dropped.
        var save = new RunSave { HasExpansion = true };
        save.Seats.Add(new SeatSave());
        save.Builds.Add(new PlacedBuild { Kind = BuildKind.Turret, X = 300, Y = 400, Owner = 2, Waves = 3 });
        var back = RunSave.Parse(save.ToText());
        Check(back != null && back.Builds.Count == 1 && back.Builds[0].Owner == 2 && back.Builds[0].Waves == 3, "a save keeps a turret's owner and waves left");
        var old = RunSave.Parse(save.ToText().Replace("build Turret 300 400 2 3", "build SpikeTrap 300 400"));
        Check(old != null && old.Builds.Count == 0, "an old save's Spike trap is dropped");
    }
}

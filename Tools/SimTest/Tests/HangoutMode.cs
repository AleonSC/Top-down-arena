using System;
using System.Linq;
using System.Threading;
using Arena;

// The Hangout as a game mode (HangoutGame.cs, Docs/Hangout.md): a real match held between waves. Opened by a client's ask, every screen
// agrees, no wave starts, the dummies stand and come back, firing works with the real loadout, nobody levels up or dies, and Back to lobby works.
static partial class Program
{

    static void HangoutModeTest(float dt)
    {
        int port = nextPort++;
        var host = new Session();
        host.Mode = GameMode.Quick;  // (the lobby's pick must not leak into the Hangout)
        host.Host(port, "Host", Role.Tank);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Dps);
        WaitForLobby(host, client);
        PollBoth(host, client, 80);
        Check(!host.Game.Running && ModeDef.For(GameMode.Hangout).Name == "Hangout" && ModeDef.FromKey("hangout") == GameMode.Hangout, "the Hangout is a mode with its own row");
        var picked = host.Mode;
        host.SetMode(GameMode.Hangout);
        Check(host.Mode == picked, "the carousel can't pick the Hangout");

        client.StartHangout();  // a client asks the host
        WaitForGame(host, client, dt);
        Check(host.Game.Running && client.Game.Running && host.Game.HangoutOn && client.Game.HangoutOn, "a client's ask opens it on every screen");
        Check(host.Game.Mode == GameMode.Hangout && client.Game.Mode == GameMode.Hangout, "both screens play the Hangout mode");
        Check(!host.Game.Quick && !client.Game.Quick && host.Game.Practice == null && host.Game.Hunt == null && !host.Game.SurvivalOn, "the lobby's own pick doesn't carry in");

        var both = new[] { host, client };
        Run(both, dt, 3f);
        Check(host.Game.Enemies.Count == 3 && client.Game.Enemies.Count == 3, "three target dummies stand, seen by the client (" + host.Game.Enemies.Count + ")");

        // Fire the host's real weapon at a dummy until it falls: it comes back, nothing is gained.
        float xp0 = host.Game.Me.Xp;
        int lvl0 = host.Game.Me.Level;
        var target = host.Game.Enemies.Values.First();
        int tid = target.Id;
        bool fell = false;
        for (int i = 0; i < 60 * 12 && !fell; i++)
        {
            Enemy e;
            if (!host.Game.Enemies.TryGetValue(tid, out e))
            {
                fell = true;
                break;
            }
            host.Poll();
            if (host.Game.Me.Pos.FartherThan(e.Pos, 90f))
                host.Game.Me.Pos = host.Game.Me.Disp = e.Pos + new V2(0f, 80f);  // (a close fight: any weapon reaches)
            host.Game.FixedStep(dt, new PlayerInput { AimAt = e.Pos, Fire = true });
            host.Game.Update(dt);
            Step(client, dt);
        }
        Check(fell, "the loadout's own weapon takes a dummy down");
        Run(both, dt, 2.5f);
        Check(host.Game.Enemies.Count == 3, "...and it comes back");
        Check(host.Game.Me.Level == lvl0 && host.Game.Me.Xp == xp0 && host.Game.Me.Offer == null && host.Game.Me.PendingPicks == 0, "no XP, no level-ups, no cards");
        Check(host.Game.Coins.Count == 0 && host.Game.Orbs.Count == 0, "nothing drops");

        // Held for good between waves.
        Run(both, dt, 70f);
        Check(host.Game.State == MatchState.Intermission && client.Game.State == MatchState.Intermission && host.Game.Wave == 1 && host.Game.StateTimer > 30f, "no wave ever starts: the state is held");
        Check(host.Game.Players.Values.All(p => p.Hp >= p.MaxHp - 0.01f && !p.Downed) && host.Game.Players.Values.All(p => !p.DevGod), "everyone is up at full health, and nobody is in god mode");

        // At 0 health nobody goes down: a beam takes them up and sets them down, full, somewhere else.
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hurt = typeof(Game).GetMethod("Damage", flags);
        var hc = host.Game.Players[client.Game.MyId];
        V2 fellAt = hc.Pos;
        hc.InvulnT = 0f;
        hurt.Invoke(host.Game, new object[] { hc, hc.MaxHp * 5f, "test" });
        Check(!hc.Downed && hc.Hp >= hc.MaxHp - 0.01f && hc.WarpT > 0f, "at 0 health in the Hangout: no going down, back to full, the beam starts");
        Run(both, dt, Game.BeamTotal + 0.3f);
        Check(!hc.Downed && hc.WarpT <= 0f && client.Game.Me.Pos.FartherThan(fellAt, 40f) && client.Game.Me.Pos.DistanceTo(hc.Pos) < 30f, "...and it sets them down somewhere else in the room, on both screens");
        Check(host.Game.State == MatchState.Intermission && host.Game.Running, "nothing ends the Hangout");

        // The spawners: twelve round the edges, all off. The client turns one on: its enemies come in and fight.
        Check(host.Game.Spawners.Length == Game.SpawnerCount && host.Game.Spawners.All(sp => !sp.On) && Game.SpawnerChoices.Length > 40 && !Game.SpawnerChoices.Any(t => Defs.Get(t).IsBoss), "twelve spawners, all off; every regular enemy can be picked, no bosses");
        Check(host.Game.Spawners.All(sp => sp.Pos.X < 100f || sp.Pos.Y < 100f || sp.Pos.X > Game.Width - 100f || sp.Pos.Y > Game.Height - 100f), "...standing round the edges");
        var pad = host.Game.Spawners[0];
        client.Game.UseSpawner(0, 1);  // (from away from it: the host ignores the press)
        Run(both, dt, 0.3f);
        Check(pad.Pick == Array.IndexOf(Game.SpawnerChoices, EnemyType.Grunt) || pad.Pick == 0, "a press from across the room does nothing");
        client.Game.Me.Pos = client.Game.Me.Disp = pad.Pos;
        Run(both, dt, 0.3f);
        Check(client.Game.SpawnerNear(client.Game.Me.Pos) == 0, "standing on it, the client is by it");
        int pick0 = pad.Pick;
        client.Game.UseSpawner(0, 1);
        Run(both, dt, 0.3f);
        Check(pad.Pick == (pick0 + 1) % Game.SpawnerChoices.Length && client.Game.Spawners[0].Pick == pad.Pick, "the discard key / wheel steps its enemy, on both screens");
        client.Game.UseSpawner(0, -1);
        Run(both, dt, 0.3f);
        Check(pad.Pick == pick0, "...and back");
        client.Game.UseSpawner(0, 0);
        Run(both, dt, 0.3f);
        Check(pad.On && client.Game.Spawners[0].On, "Interact turns it on, on both screens");
        int before = host.Game.Enemies.Count;
        Run(both, dt, Game.SpawnerEvery * 2f + 1f);
        Check(host.Game.Enemies.Count > before && host.Game.Enemies.Values.Count(en => en.Type == pad.Type) >= 2, "it trickles its enemy in (" + (host.Game.Enemies.Count - before) + ")");
        Run(both, dt, Game.SpawnerEvery * (Game.SpawnerMax + 3));
        Check(host.Game.Enemies.Values.Count(en => en.Type == pad.Type && !host.Game.Spawners.Skip(1).Any(sp => sp.Alive.Contains(en.Id))) <= Game.SpawnerMax + 3, "...up to its cap (dummies aside)");
        // Its enemies fight: someone standing among them gets hurt (and beamed out at 0, never downed).
        var hm = host.Game.Me;
        float hurtBefore = hm.Run.DamageTaken;
        for (int i = 0; i < 600 && hm.Run.DamageTaken <= hurtBefore; i++)
        {
            var foe = host.Game.Enemies.Values.FirstOrDefault(en => pad.Alive.Contains(en.Id));
            if (foe != null && hm.WarpT <= 0f)
                hm.Pos = hm.Disp = foe.Pos + new V2(0f, foe.Radius + 20f);
            host.Game.FixedStep(dt, new PlayerInput { AimAt = hm.Pos + new V2(0f, 200f) });
            host.Game.Update(dt);
            Step(client, dt);
        }
        Check(hm.Run.DamageTaken > hurtBefore && !hm.Downed, "its enemies attack (the dummies never do)");
        var made = pad.Alive.ToList();
        client.Game.Me.Pos = client.Game.Me.Disp = pad.Pos;
        Run(both, dt, 0.2f);
        client.Game.UseSpawner(0, 0);
        Run(both, dt, 0.3f);
        Check(made.Count > 0 && !pad.On && !client.Game.Spawners[0].On && pad.Alive.Count == 0 && made.All(id => !host.Game.Enemies.ContainsKey(id) && !client.Game.Enemies.ContainsKey(id)), "turning it off takes its enemies away, on both screens");

        // No cooldowns: a spent ability or ultimate is back on the next tick, on the host and on a client.
        foreach (var g in new[] { host.Game, client.Game })
            foreach (var sl in g.Me.Slots)
                if (sl.Id != AbilityId.None)
                {
                    sl.Charges = 0;
                    sl.Timer = 60f;
                }
        Run(both, dt, 0.2f);
        Check(new[] { host.Game, client.Game }.All(g => g.Me.Slots.All(sl => sl.Id == AbilityId.None || sl.Charges == sl.MaxCharges && sl.Timer <= 0f))
            && host.Game.Me.Slots[Upgrades.UltSlot].Id != AbilityId.None, "no cooldowns: every ability and the ultimate come straight back");

        // Back to the lobby.
        host.ReturnToLobby();
        PollBoth(host, client, 80);
        Check(host.Screen == AppScreen.Lobby && client.Screen == AppScreen.Lobby && !host.Game.HangoutOn && !host.Game.Running, "Back to lobby ends it, and the lobby plays a normal mode again");

        // PLAY in the loadout's preview window (Showcase.Control): your input drives the real game on the dummies.
        var sc = new Showcase();
        sc.Show(Role.Dps, Kit.Soldier, 0, Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0), false);
        for (int i = 0; i < 60; i++) sc.Step(dt);
        V2 home = sc.Me.Pos;
        sc.SetControl(true);
        sc.Show(Role.Dps, Kit.Soldier, 0, Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0), false);
        Check(sc.Game.Running && sc.Control && sc.Me.Pos.DistanceTo(Showcase.Home) < 1f, "PLAY starts the scene over");
        for (int i = 0; i < 60; i++) { sc.Play = new PlayerInput { Move = new V2(1f, 0f), AimAt = sc.Me.Pos + new V2(0f, -200f) }; sc.Step(dt); }
        Check(sc.Me.Pos.X > Showcase.Home.X + 50f, "WASD moves you in the window (and nothing walks you home)");
        float hp0 = sc.Game.Enemies.Values.Sum(e => e.Hp);
        for (int i = 0; i < 180; i++) { var d = sc.Game.Enemies.Values.OrderBy(e => e.Pos.DistanceTo(sc.Me.Pos)).First(); sc.Play = new PlayerInput { AimAt = d.Pos, Fire = true }; sc.Step(dt); }
        Check(sc.Game.Enemies.Values.Sum(e => e.Hp) < hp0 || sc.Game.Enemies.Count != 3, "holding fire with your real weapon hurts the dummies");
        foreach (var sl in sc.Me.Slots)
            if (sl.Id != AbilityId.None)
            {
                sl.Charges = 0;
                sl.Timer = 60f;
            }
        for (int i = 0; i < 3; i++) { sc.Play = new PlayerInput { AimAt = sc.Me.Pos + new V2(0f, -200f) }; sc.Step(dt); }
        Check(sc.Me.Slots.All(sl => sl.Id == AbilityId.None || sl.Charges == sl.MaxCharges && sl.Timer <= 0f) && sc.Me.Slots[Upgrades.UltSlot].Id != AbilityId.None,
            "PLAY has no cooldowns: every ability and the ultimate come straight back");
        sc.SetControl(false);
        sc.Show(Role.Dps, Kit.Soldier, 0, Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0), false);
        for (int i = 0; i < 3; i++) sc.Step(dt);
        Check(!sc.Game.ShowcasePlay, "...and the plain preview keeps its cooldowns");
        sc.Stop();
    }
}

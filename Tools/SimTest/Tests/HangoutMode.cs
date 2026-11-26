using System;
using System.Linq;
using System.Threading;
using Arena;

// Practice, the Hangout mode (HangoutGame.cs, Docs/Hangout.md): a real match held between waves behind the lobby. Opened by a client's ask,
// every screen agrees and stays the lobby, control at once, no dummies, Ready and loadout swaps work, a newcomer joins, Start replaces it.
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
        WaitForPractice(host, client, dt);
        Check(host.Game.Running && client.Game.Running && host.Game.HangoutOn && client.Game.HangoutOn && host.Game.Practicing, "a client's ask opens it on every screen");
        Check(host.Screen == AppScreen.Lobby && client.Screen == AppScreen.Lobby && !host.Game.InRun, "Practice plays inside the lobby: every screen stays the lobby");
        Check(host.Game.Mode == GameMode.Hangout && client.Game.Mode == GameMode.Hangout, "both screens play the Hangout mode");
        Check(!host.Game.Quick && !client.Game.Quick && host.Game.Hunt == null && !host.Game.SurvivalOn, "the lobby's own pick doesn't carry in");

        // Control from the first tick: no countdown holds you.
        var cm = client.Game.Me;
        V2 start0 = cm.Pos;
        for (int i = 0; i < 20; i++) { host.Poll(); host.Game.FixedStep(dt, new PlayerInput()); host.Game.Update(dt); client.Poll(); client.Game.FixedStep(dt, new PlayerInput { Move = new V2(1f, 0f), AimAt = cm.Pos }); client.Game.Update(dt); }
        Check(!client.Game.HoldControls && cm.Pos.X > start0.X + 20f, "you move at once (" + (cm.Pos.X - start0.X).ToString("0") + " px)");

        var both = new[] { host, client };
        Run(both, dt, 3f);
        Check(host.Game.Enemies.Count == 0 && client.Game.Enemies.Count == 0, "no target dummies (the loadout preview keeps its own)");
        float xp0 = host.Game.Me.Xp;
        int lvl0 = host.Game.Me.Level;

        // The lobby goes on around it: Ready works, and a changed loadout swaps just that player in place.
        client.RequestReady(true);
        Run(both, dt, 0.3f);
        Check(host.Lobby[client.Game.MyId].Ready && host.Game.Practicing, "Ready works while Practice is up, and leaves it running");
        client.RequestReady(false);
        var hostMe = host.Game.Me;
        hostMe.Pos = hostMe.Disp = new V2(300f, 300f);
        var was = host.Game.Players[client.Game.MyId];
        V2 standAt = was.Pos;
        var oldWeapon = was.Weapon.Id;
        client.RequestWeapon(client.Lobby[client.Game.MyId].Weapon == 0 ? 1 : 0);
        Run(both, dt, 0.4f);
        var after = host.Game.Players[client.Game.MyId];
        Check(after != was && after.Weapon.Id != oldWeapon && client.Game.Me.Weapon.Id == after.Weapon.Id, "a new weapon swaps that player's character, on both screens (" + oldWeapon + " -> " + after.Weapon.Id + ")");
        Check(after.Pos.DistanceTo(standAt) < 60f && host.Game.Me == hostMe && hostMe.Pos.DistanceTo(new V2(300f, 300f)) < 1f, "...where they stood, and nobody else is touched");
        client.RequestKit(Kit.Knight);
        Run(both, dt, 0.4f);
        Check(host.Game.Players[client.Game.MyId].Kit == Kit.Knight && client.Game.Me.Kit == Kit.Knight && host.Game.Practicing, "a new class swaps them too");

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
        Check(host.Game.Spawners.All(sp => sp.Pos.X < 100f || sp.Pos.Y < 200f || sp.Pos.X > Game.Width - 100f || sp.Pos.Y > Game.Height - 200f), "...standing round the edges");
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
        Check(host.Game.Enemies.Values.Count(en => en.Type == pad.Type && !host.Game.Spawners.Skip(1).Any(sp => sp.Alive.Contains(en.Id))) <= Game.SpawnerMax + 3, "...up to its cap");
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
        Check(hm.Run.DamageTaken > hurtBefore && !hm.Downed, "its enemies attack");
        Check(host.Game.Me.Level == lvl0 && host.Game.Me.Xp == xp0 && host.Game.Me.Offer == null && host.Game.Me.PendingPicks == 0 && host.Game.Orbs.Count == 0, "no XP, no level-ups, no cards");
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

        // Someone joining while it's up: the room starts over with them in it.
        var third = new Session();
        third.Join("127.0.0.1", port, "Third", Role.Healer);
        for (int i = 0; i < 400 && !(third.Game.Practicing && host.Game.Players.Count == 3); i++) { Step(host, dt); Step(client, dt); Step(third, dt); Nap(1); }
        Check(third.Screen == AppScreen.Lobby && third.Game.Practicing && host.Game.Players.Count == 3 && client.Game.Players.Count == 3, "a newcomer joins the room (it starts over with them)");
        var three = new[] { host, client, third };

        // Everyone ready: the real run replaces the room.
        host.RequestReady(true); client.RequestReady(true); third.RequestReady(true);
        Run(three, dt, 0.3f);
        host.StartMatch();
        for (int i = 0; i < 300 && (client.Screen != AppScreen.Game || third.Screen != AppScreen.Game); i++) { Step(host, dt); Step(client, dt); Step(third, dt); Nap(1); }
        Check(host.Screen == AppScreen.Game && client.Screen == AppScreen.Game && third.Screen == AppScreen.Game && host.Game.InRun && !host.Game.HangoutOn && !client.Game.PvpOn, "Start from the lobby replaces Practice with the real run");
        third.Leave("");

        // Back to the lobby.
        host.ReturnToLobby();
        PollBoth(host, client, 80);
        Check(host.Screen == AppScreen.Lobby && client.Screen == AppScreen.Lobby && !host.Game.HangoutOn && !host.Game.Running, "Back to lobby ends it, and the lobby plays a normal mode again");

    }
}

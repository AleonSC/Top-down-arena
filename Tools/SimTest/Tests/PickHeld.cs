using System;
using System.Linq;
using System.Reflection;
using Arena;

// Level-up cards held mid-fight (Game.PickHeld): between waves a run can't pause (co-op), so a level-up won while
// enemies are about (a woken dormant group, a duel, an extra room) keeps its cards hidden until no awake enemy is near.
static partial class Program
{
    static void PickHeldTest(float dt)
    {
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", BindingFlags.NonPublic | BindingFlags.Instance);
        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Host", Role.Tank);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action calm = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = p.PendingMilestones = p.PrizePicks = 0; p.LegendaryQueue.Clear(); p.Offer = null; p.InvulnT = 1f; } };
        Run(s, dt, 30f, () => g.State == MatchState.Intermission, () => { calm(); foreach (var e in g.Enemies.Values) e.Hp = 0f; g.Enemies.Clear(); });
        Check(g.State == MatchState.Intermission, "the break starts");
        g.StateTimer = 60f;
        var friend = g.Players[2];
        var e1 = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Brute, friend.Pos + new V2(300f, 0f), 0f, 0, false });
        g.DevLevelUp(2);
        Run(s, dt, 0.5f, null, () => { friend.InvulnT = 1f; e1.Pos = friend.Pos + new V2(300f, 0f); });
        Check(friend.Offer != null && g.PickHeld(friend), "host: an awake enemy near you holds the cards back");
        Check(cg.Me != null && cg.Me.Offer != null && cg.PickHeld(cg.Me), "the friend's own game holds them too (no message)");
        Check(!g.PickHeld(g.Players[1]), "a player with no cards isn't held");
        e1.Pos = friend.Pos + new V2(Game.PickHoldReach + 400f, 0f);
        Check(!g.PickHeld(friend), "a far enemy doesn't hold them");
        e1.Pos = friend.Pos + new V2(200f, 0f); e1.Dormant = true;
        Check(!g.PickHeld(friend), "a sleeping group doesn't hold them");
        e1.Dormant = false;
        Check(g.PickHeld(friend), "it wakes: held again");
        e1.Hp = 0f; g.Enemies.Remove(e1.Id);
        Run(s, dt, 0.5f, null, () => friend.InvulnT = 1f);
        Check(!g.PickHeld(friend) && !cg.PickHeld(cg.Me) && cg.Me.Offer != null, "the fight over: the cards come up, the pick still waiting");
        Check(g.AnyonePicking(), "the countdown still waits on the held pick");
    }
}

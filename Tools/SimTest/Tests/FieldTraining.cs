using System;
using System.Collections.Generic;
using System.Linq;
using Arena;

// Field Training (Soldier.cs), the Soldier's class passive: one more card in every level-up offer (5 at most with Wide
// Choice), a free reroll as each wave starts while holding fewer than 3, on both screens; other classes and a Wildcard
// get neither.
static partial class Program
{
    static void FieldTrainingTest(float dt)
    {
        int port = nextPort++;
        var hostS = new Session { AutoStart = 2, StartWave = 3 };
        hostS.Host(port, "Soldier", Role.Dps, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Healer, 0, 0, Kit.Knight);
        var s = new[] { hostS, client };
        WaitForGame(hostS, client, dt);
        var g = hostS.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var knight = g.Players[2];

        Check(Game.IsSoldier(me) && !Game.IsSoldier(knight), "a Soldier has Field Training, a Knight doesn't");
        int mine = Upgrades.RollOffer(new Random(1), g.OfferFor(me), 8).Length;
        int theirs = Upgrades.RollOffer(new Random(1), g.OfferFor(knight), 8).Length;
        Console.WriteLine("  field training: a Soldier's offer has {0} cards, a Knight's {1}", mine, theirs);
        Check(mine == 4 && theirs == 3, "a Soldier's level-up offer shows 4 cards, another class's 3");
        int wide = Array.FindIndex(Upgrades.All, u => u.Name == "Wide Choice");
        me.Taken.Add(wide); me.Taken.Add(wide);
        Check(Upgrades.RollOffer(new Random(2), g.OfferFor(me), 8).Length == 5, "with Wide Choice x2 a Soldier still sees 5 cards at most");
        me.Taken.Remove(wide); me.Taken.Remove(wide);
        me.Role = Role.Wildcard;
        Check(!Game.IsSoldier(me) && Upgrades.RollOffer(new Random(1), g.OfferFor(me), 8).Length == 3, "a Wildcard (Soldier gear or none) gets no Field Training");
        me.Role = Role.Dps;

        // The wave reroll: given as each wave starts, up to 3 held.
        var reroll = typeof(Game).GetMethod("FieldTrainingRerolls", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        me.Rerolls = 0; knight.Rerolls = 0;
        for (int w = 0; w < 5; w++) reroll.Invoke(g, null);
        Check(me.Rerolls == Game.FieldTrainingRerollCap && knight.Rerolls == 0, "a free reroll each wave, up to 3 held (" + me.Rerolls + "); none for a Knight");
        me.Rerolls = 1;
        Run(s, dt, 2, () => false);
        Check(client.Game.Players[1].Rerolls == 1, "the friend's screen shows the Soldier's rerolls");

        hostS.Leave(""); client.Leave("");
    }
}

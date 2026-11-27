using System.Linq;
using System;
using System.Collections.Generic;
using Arena;

// Tactical Vest (TacticalVest.cs), the DPS Soldier's 4th choice: the table (20 s), the pick reaching the match, a press popping
// 3 distinct gadgets from the vest's pool onto a 50-90 px ring round you (flying out first, yours alone, not caught leaving you),
// walking over one taking it as a real gadget (hand, then bag), a full hand and bag leaving them on the floor, the rest fizzling
// after 12 s (replicated, client included), and mastery (4 gadgets that never fizzle).
static partial class Program
{
    static void TacticalVestTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);

        var def = Upgrades.Ability(AbilityId.TacticalVest);
        Check(def.Name == "Tactical Vest" && def.Choice == AbilityDef.Fourth && def.Kit == Kit.Soldier && def.Role == Role.Dps && def.Cooldown == 20f,
            "Tactical Vest: a 20 s DPS Soldier 1st ability on the 4th key");
        Check(Upgrades.AbilityLimit(Role.Dps, Kit.Soldier) == 4 && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 3) == AbilityId.TacticalVest
            && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0) == AbilityId.PoisonDart && Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 2) == AbilityId.Claymore,
            "the DPS Soldier's 4th key is Tactical Vest; its first three are unchanged");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 2, StartWave = 3 }, port, false, "Gunner", Role.Dps, 0, 3, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Tank, 0, 0, Kit.Knight);
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game;
        Run(s, dt, 6, () => g.State == MatchState.Fighting);
        var me = g.Players[1];
        var friend = g.Players[2];
        Check(me.Slots[0].Id == AbilityId.TacticalVest && client.Game.Players[1].Slots[0].Id == AbilityId.TacticalVest,
            "the 4th choice reaches the run as the 1st ability");
        g.DevSetGod(1, true); g.DevSetGod(2, true);

        V2 home = new V2(500f, 450f), friendAt = new V2(1400f, 900f);
        V2 mePos = home;
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var e in g.Enemies.Values) { e.FireT = 99f; e.Pos = new V2(1700f, 150f); }
                me.Pos = mePos; friend.Pos = friendAt;
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = home + new V2(200f, 0f) });
            }
        };
        Action clearHands = () =>
        {
            me.Gadget = GadgetId.None; me.GadgetOn = false;
            for (int i = 0; i < me.BagKind.Length; i++) me.BagKind[i] = GadgetId.None;
        };
        Func<List<Gadget>> cast = () =>
        {
            int top = g.Gadgets.Count == 0 ? 0 : g.Gadgets.Keys.Max();
            me.Slots[0].Charges = Math.Max(1, me.Slots[0].Charges); me.Slots[0].Timer = 0f;
            hostAbility.Invoke(g, new object[] { 1, 0, home + new V2(200f, 0f), false });
            return g.Gadgets.Values.Where(x => x.Id > top).OrderBy(x => x.Id).ToList();
        };

        // The pop: three distinct kinds from the pool, flying out from you to a 50-90 px ring, yours alone.
        g.Gadgets.Clear(); clearHands(); mePos = home; step(dt);
        var drops = cast();
        Check(drops.Count == 3 && drops.Select(x => x.Kind).Distinct().Count() == 3 && drops.All(x => Game.VestGadgets.Contains(x.Kind)),
            "a press pops 3 different gadgets from the vest's pool (" + string.Join(", ", drops.Select(x => x.Kind)) + ")");
        Check(drops.All(x => x.Flying && x.Owner == 1 && x.Fizzle == Game.VestFizzle && x.Uses == Game.GadgetUses(x.Kind)),
            "they fly out with their full uses, yours alone, on a 12 s fizzle");
        Check(drops.All(x => { float d = x.To.DistanceTo(home); return d >= Game.VestDropMin - 0.5f && d <= Game.VestDropMax + 0.5f; }),
            "they land 50-90 px round you");
        step(Game.GadgetFlyTime + 0.1f);
        Check(me.Gadget == GadgetId.None && drops.All(x => g.Gadgets.ContainsKey(x.Id) && !x.Flying), "they land on the floor; you don't catch them leaving the vest");
        Check(client.Game.Gadgets.Count == 3 && client.Game.Gadgets.Values.All(x => x.Fizzle > 0f && x.Fizzle < Game.VestFizzle),
            "the client sees all three and their fizzle timers");

        // A teammate can't take yours.
        friendAt = drops[2].Pos; step(0.3f);
        Check(g.Gadgets.ContainsKey(drops[2].Id) && friend.Gadget == GadgetId.None, "a teammate walking over one leaves it (it's yours)");
        friendAt = new V2(1400f, 900f);

        // Walking over one takes it as a real gadget: into the hand, then the bag; it never fizzles once carried.
        mePos = drops[0].Pos; step(0.2f);
        Check(me.Gadget == drops[0].Kind && me.GadgetUses == Game.GadgetUses(drops[0].Kind) && !g.Gadgets.ContainsKey(drops[0].Id),
            "walking over one puts it in your hand with its full uses");
        mePos = drops[1].Pos; step(0.2f);
        Check(me.BagKind.Contains(drops[1].Kind) && !g.Gadgets.ContainsKey(drops[1].Id), "a second goes in the bag");

        // The third fizzles after 12 s on the floor, on the client too.
        mePos = home;
        step(Game.VestFizzle - 1f);
        Check(g.Gadgets.ContainsKey(drops[2].Id), "it's still there just before 12 s");
        step(1.5f);
        Check(!g.Gadgets.ContainsKey(drops[2].Id) && client.Game.Gadgets.Count == 0, "left on the floor, it fizzles out after 12 s (the client's goes too)");
        Check(me.Gadget == drops[0].Kind && me.BagKind.Contains(drops[1].Kind), "the ones you carry stay");

        // A full hand and bag leaves them on the floor.
        me.Gadget = GadgetId.Medkit; me.GadgetUses = 3;
        for (int i = 0; i < me.BagKind.Length; i++) { me.BagKind[i] = GadgetId.Magnet; me.BagLeft[i] = 10f; }
        g.Gadgets.Clear();
        drops = cast(); step(Game.GadgetFlyTime + 0.1f);
        mePos = drops[0].Pos; step(0.3f);
        Check(g.Gadgets.ContainsKey(drops[0].Id), "with a full hand and bag, walking over one leaves it");

        // Mastered: four gadgets that stay until grabbed.
        clearHands(); g.Gadgets.Clear(); mePos = home; step(dt);
        me.MasteryXp[AbilityId.TacticalVest] = 1e9f;
        drops = cast();
        Check(drops.Count == 4 && drops.Select(x => x.Kind).Distinct().Count() == 4 && drops.All(x => x.Fizzle == 0f), "mastered: 4 different gadgets with no fizzle");
        step(Game.VestFizzle + 2f);
        Check(drops.All(x => g.Gadgets.ContainsKey(x.Id)), "mastered: they're all still on the floor after 14 s");
        me.MasteryXp.Remove(AbilityId.TacticalVest);

        // Random: every kind in the pool turns up over a few presses.
        var seen = new HashSet<GadgetId>();
        for (int i = 0; i < 12; i++) { g.Gadgets.Clear(); foreach (var x in cast()) seen.Add(x.Kind); }
        Check(seen.Count == Game.VestGadgets.Length, "over 12 presses every kind in the pool turns up (" + seen.Count + " of " + Game.VestGadgets.Length + ")");

        host.Leave(""); client.Leave("");

    }
}

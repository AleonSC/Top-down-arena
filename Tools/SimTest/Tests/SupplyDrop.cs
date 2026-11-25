using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arena;

// Supply Drop (SupplyDrop.cs): the Healer Soldier's key 2 on the lobby's Ultimate row. A flare at the cursor (up to 520 px away); nothing
// for 1.5 s, then the crate lands: damage and a stagger to enemies within 90 px, nobody farther. It opens into a 170 px zone for 12 s:
// players inside (on every screen) move 20% and fire 30% faster and their cooldowns recharge 40% faster, the ultimate's not; outside
// nothing; health packs (medkit orbs) come out every 2 s, 4 at most on the floor; it ends on time and the buff wears off.
static partial class Program
{
    static void SupplyDropTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var fireRate = typeof(Game).GetMethod("FireRate", flags);

        var def = Upgrades.Ability(AbilityId.SupplyDrop);
        Check(def.Name == "Supply Drop" && def.Cooldown == 110f && def.Choice == AbilityDef.Ult && def.UltKey == 2 && def.Role == Role.Healer && def.Kit == Kit.Soldier && def.Capstone != null,
            "Supply Drop is the Healer Soldier's ultimate on key 2, 110 s");
        Check(Upgrades.Ultimate(Role.Healer, Kit.Soldier) == AbilityId.Lifeline && Upgrades.Ultimate(Role.Healer, Kit.Soldier, 1) == AbilityId.FieldHospital
            && Upgrades.Ultimate(Role.Healer, Kit.Soldier, 2) == AbilityId.SupplyDrop && Upgrades.ClampUlt(Role.Healer, Kit.Soldier, 2) == 2,
            "Lifeline stays key 0 and the Field Hospital key 1");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 6 }, port, false, "Medic", Role.Healer, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Thread.Sleep(1); } };
        host.RequestUlt(2);
        settle();
        Check(host.Lobby[1].UltId == AbilityId.SupplyDrop && client.Lobby[1].Ult == 2, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, noPicks);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.SupplyDrop, "in the match the host's ultimate is the Supply Drop");
        g.DevSetGod(client.MyId, true);

        V2 drop = new V2(900f, 500f), meAt = new V2(700f, 500f), allyAt = new V2(300f, 300f);
        var mine = new Dictionary<int, V2>();
        Action<float> step = secs =>
        {
            for (int f = 0; f < secs / dt; f++)
            {
                foreach (var id in g.Enemies.Keys.Where(k => !mine.ContainsKey(k)).ToList()) g.Enemies.Remove(id);
                foreach (var e in g.Enemies.Values) { e.Pos = mine[e.Id]; e.FireT = 99f; }
                g.State = MatchState.Fighting;
                me.Pos = meAt; me.InvulnT = 1f; ally.Pos = allyAt;
                noPicks();
                host.Poll(); client.Poll();
                g.FixedStep(dt, new PlayerInput { AimAt = drop });
            }
        };
        step(0.2f);
        Func<V2, Enemy> enemy = at =>
        {
            var e = (Enemy)spawn.Invoke(g, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.SpawnT = 0f; e.Hp = e.MaxHp = 1000f; mine[e.Id] = at;
            return e;
        };
        var near = enemy(drop + new V2(40f, 0f));
        var far = enemy(drop + new V2(200f, 0f));

        me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, drop, false });
        var z = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.SupplyDrop);
        Check(z != null && z.Pos.CloserThan(drop, 1f) && z.Radius == Game.SupplyRadius, "the flare marks the cursor");
        step(1.2f);
        Check(near.Hp == 1000f && !Game.SupplyLanded(z), "nothing lands for the first 1.5 s");
        meAt = drop + new V2(-60f, 0f);
        step(0.4f);
        Check(near.Hp < 1000f - 40f && near.StunT > 0f, "the crate lands on the enemy beside it: " + (1000f - near.Hp).ToString("0") + " damage and a stagger");
        Check(far.Hp == 1000f, "an enemy 200 px off isn't touched");

        settle();
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.SupplyDrop), "the other screen has the drop");
        step(0.6f);
        Check(Game.SupplyZoneOn(z) && me.SupplyT > 0f && ally.SupplyT == 0f, "it opens: the caster inside is supplied, the ally outside isn't");
        float sp = me.Speed, fr = (float)fireRate.Invoke(g, new object[] { me });
        me.SupplyT = 0f;
        float sp0 = me.Speed, fr0 = (float)fireRate.Invoke(g, new object[] { me });
        Check(Math.Abs(sp / sp0 - Game.SupplyMove) < 0.001f, "20% faster movement inside");
        Check(Math.Abs(fr / fr0 - Game.SupplyFire) < 0.001f, "30% faster fire inside");

        allyAt = drop + new V2(50f, 50f);
        step(0.3f);
        settle();
        Check(ally.SupplyT > 0f && cg.Players[client.MyId].SupplyT > 0f, "the ally walks in and is supplied, on their own screen too");
        Check(cg.Players[client.MyId].Speed > g.Players[client.MyId].Speed / Game.SupplyMove * 1.1f, "their own screen moves them faster");

        var slot = me.Slots[0];
        var ult = me.Slots[Upgrades.UltSlot];
        slot.Charges = 0; slot.Timer = 50f; slot.MaxCharges = Math.Max(1, slot.MaxCharges);
        float ultBefore = ult.Timer;
        step(1f);
        Check(Math.Abs((50f - slot.Timer) - Game.SupplyCdMult) < 0.05f, "a 1st ability recharges 40% faster inside (" + (50f - slot.Timer).ToString("0.00") + " s in 1 s)");
        Check(Math.Abs((ultBefore - ult.Timer) - 1f) < 0.05f, "the ultimate doesn't");

        step(5f);
        var packs = g.Orbs.Values.Where(o => o.Supply == z.Id).ToList();
        Check(packs.Count >= 2 && packs.Count <= Game.SupplyPackCap && packs.All(o => o.Med && o.MedHeal == Game.SupplyPackHeal), "health packs come out of the crate (" + packs.Count + ")");
        Check(packs.All(o => o.Pos.CloserThan(drop, Game.SupplyPackMax + 2f) && o.Pos.FartherThan(drop, Game.SupplyPackMin - 2f)), "they land 60-150 px out");
        settle();
        Check(cg.Orbs.Values.Count(o => o.Med) >= packs.Count, "the other screen sees them");
        meAt = drop + new V2(400f, 300f);  // (out of reach of the packs, so they pile up)
        allyAt = drop + new V2(-400f, 300f);
        step(8f);
        Check(g.Orbs.Values.Count(o => o.Supply == z.Id) <= Game.SupplyPackCap, "never more than 4 on the floor");
        Check(me.SupplyT == 0f, "stepping out ends the buff");
        step(1f);
        Check(!g.Zones.ContainsKey(z.Id), "it ends after its 12 s");

        // the reach: a flare thrown too far lands 520 px out
        me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
        meAt = new V2(500f, 500f);
        me.Pos = meAt;
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, meAt + new V2(1500f, 0f), false });
        var z2 = g.Zones.Values.First(q => q.Kind == ZoneKind.SupplyDrop);
        Check(z2.Pos.DistanceTo(meAt) <= Game.SupplyReach + 1f, "a flare thrown too far lands 520 px out");

        host.Leave(""); client.Leave("");
    }
}

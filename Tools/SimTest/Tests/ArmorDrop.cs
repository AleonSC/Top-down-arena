using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Arena;

// Armor Drop (ArmorDrop.cs): the Tank Soldier's key 3 on the lobby's Ultimate row. A flare at the cursor (up to 520 px away); nothing
// for 1.6 s, then the crate lands: damage, a stagger and a shove to enemies within 110 px, nobody farther. It opens and kicks out 3
// ballistic vests (mastered 4) 70-110 px out, on every screen. Walking over one sets your armor to 50 (on every screen); one you
// can't use stays put. Armor takes damage before health, after the Juggernaut Suit's, never regenerates, and a vest refills it.
// The crate folds away once every vest is taken.
static partial class Program
{
    static void ArmorDropTest(float dt)
    {
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var hostAbility = typeof(Game).GetMethod("HostAbility", flags, null, new[] { typeof(int), typeof(int), typeof(V2), typeof(bool) }, null);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        var damage = typeof(Game).GetMethod("Damage", flags, null, new[] { typeof(Player), typeof(float), typeof(string) }, null);

        var def = Upgrades.Ability(AbilityId.ArmorDrop);
        Check(def.Name == "Armor Drop" && def.Cooldown == 80f && def.Choice == AbilityDef.Ult && def.UltKey == 3 && def.Role == Role.Tank && def.Kit == Kit.Soldier && def.Capstone != null,
            "Armor Drop is the Tank Soldier's ultimate on key 3, 80 s");
        Check(Upgrades.Ultimate(Role.Tank, Kit.Soldier) == AbilityId.Fortress && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 1) == AbilityId.JuggernautSuit
            && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 2) == AbilityId.LiteralTank && Upgrades.Ultimate(Role.Tank, Kit.Soldier, 3) == AbilityId.ArmorDrop
            && Upgrades.ClampUlt(Role.Tank, Kit.Soldier, 3) == 3 && Upgrades.ClampUlt(Role.Dps, Kit.Soldier, 3) == 0,
            "Fortress stays the default, the Juggernaut Suit key 1, A Literal Tank key 2; key 3 is the Armor Drop, only for the Tank Soldier");

        int port = nextPort++;
        var host = HostAs(new Session { AutoStart = 9, StartWave = 6 }, port, false, "Quartermaster", Role.Tank, 0, 0, Kit.Soldier);
        var client = JoinAs(port, false, "Friend", Role.Dps);
        WaitForLobby(host, client);
        Action settle = () => { for (int i = 0; i < 40; i++) { host.Poll(); client.Poll(); Nap(1); } };
        host.RequestUlt(3);
        settle();
        Check(host.Lobby[1].UltId == AbilityId.ArmorDrop && client.Lobby[1].Ult == 3, "the pick reaches everyone");
        host.StartMatch();
        var s = new[] { host, client };
        WaitForGame(host, client, dt);
        var g = host.Game; var cg = client.Game;
        Action noPicks = () => { foreach (var p in g.Players.Values) { p.PendingPicks = p.PendingLegendary = p.PendingMods = p.PendingCards = 0; p.LegendaryQueue.Clear(); p.Offer = null; } };
        Run(s, dt, 20f, () => g.State == MatchState.Fighting, noPicks);
        var me = g.Players[1];
        var ally = g.Players[client.MyId];
        Check(me.Slots[Upgrades.UltSlot].Id == AbilityId.ArmorDrop, "in the match the host's ultimate is the Armor Drop");
        Check(me.Armor == 0f && ally.Armor == 0f, "nobody starts with armor");
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
        var far = enemy(drop + new V2(260f, 0f));

        me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, drop, false });
        var z = g.Zones.Values.FirstOrDefault(q => q.Kind == ZoneKind.ArmorDrop);
        Check(z != null && z.Pos.CloserThan(drop, 1f), "the flare marks the cursor");
        step(1.3f);
        Check(near.Hp == 1000f && !Game.ArmorLanded(z), "nothing lands for the first 1.6 s");
        step(0.4f);
        Check(near.Hp < 1000f - 40f && near.StunT > 0f, "the crate lands on the enemy beside it: " + (1000f - near.Hp).ToString("0") + " damage and a stagger");
        Check(far.Hp == 1000f, "an enemy 260 px off isn't touched");
        foreach (var e in g.Enemies.Values.ToList()) g.Enemies.Remove(e.Id);
        mine.Clear();

        step(0.4f);
        var vests = g.Orbs.Values.Where(o => o.Vest).ToList();
        Check(vests.Count == Game.ArmorVests && vests.All(o => o.Supply == z.Id && o.Heal == 0f), "the crate opens and kicks out 3 vests (" + vests.Count + ")");
        Check(vests.All(o => o.Pos.CloserThan(drop, Game.ArmorVestMax + 2f) && o.Pos.FartherThan(drop, Game.ArmorVestMin - 2f)), "they land 70-110 px out");
        settle();
        Check(cg.Zones.Values.Any(q => q.Kind == ZoneKind.ArmorDrop) && cg.Orbs.Values.Count(o => o.Vest) == Game.ArmorVests, "the other screen has the crate and the vests");
        step(2f);
        Check(g.Orbs.Values.Count(o => o.Vest) == Game.ArmorVests, "the vests lie still and wait: nobody is near");

        // pick one up
        meAt = vests[0].Pos;
        step(0.1f);
        settle();
        Check(me.Armor == Game.ArmorCap && !g.Orbs.ContainsKey(vests[0].Id), "walking over a vest gives 50 armor and takes it");
        Check(cg.Players[me.Id].Armor == Game.ArmorCap && !cg.Orbs.ContainsKey(vests[0].Id), "on the other screen too");
        meAt = vests[1].Pos;
        step(0.2f);
        Check(me.Armor == Game.ArmorCap && g.Orbs.ContainsKey(vests[1].Id), "at full armor another vest stays on the floor (no stacking past 50)");

        // armor soaks first and never comes back
        meAt = drop + new V2(-300f, 200f);
        step(0.1f);
        float hp = me.Hp;
        damage.Invoke(g, new object[] { me, 10f, "Test" });
        Check(me.Armor < Game.ArmorCap && me.Armor > 0f && me.Hp == hp, "a hit comes off the armor, not health (" + me.Armor.ToString("0.#") + " left)");
        float left = me.Armor;
        step(3f);
        Check(me.Armor == left, "armor doesn't regenerate");
        me.JuggT = 5f; me.JuggArmor = me.JuggMax = 1000f;
        damage.Invoke(g, new object[] { me, 10f, "Test" });
        Check(me.Armor == left && me.JuggArmor < 1000f, "the Juggernaut Suit's armor takes it first");
        me.JuggT = 0f; me.JuggArmor = 0f;
        damage.Invoke(g, new object[] { me, 50f, "Test" });
        Check(me.Armor == 0f && me.Hp < hp && !me.Downed, "a big hit breaks it and the rest reaches health");
        step(0.1f);
        settle();
        Check(cg.Players[me.Id].Armor == 0f, "the other screen sees it gone");
        me.Hp = me.MaxHp;

        // a vest refills it; the crate folds once all are taken
        meAt = vests[1].Pos;
        allyAt = vests[2].Pos;
        step(0.2f);
        Check(me.Armor == Game.ArmorCap && ally.Armor == Game.ArmorCap, "another vest refills it, and the ally takes the last one");
        step(Game.ArmorFold + 0.3f);
        Check(!g.Zones.ContainsKey(z.Id), "every vest taken: the crate folds away");

        // mastered: a fourth vest; a flare thrown too far lands 520 px out
        me.MasteryXp[AbilityId.ArmorDrop] = 1e6f;
        me.Slots[Upgrades.UltSlot].Charges = 1; me.Slots[Upgrades.UltSlot].Timer = 0f;
        meAt = new V2(500f, 500f);
        me.Pos = meAt;
        hostAbility.Invoke(g, new object[] { me.Id, Upgrades.UltSlot, meAt + new V2(1500f, 0f), false });
        var z2 = g.Zones.Values.First(q => q.Kind == ZoneKind.ArmorDrop);
        Check(z2.Pos.DistanceTo(meAt) <= Game.ArmorReach + 1f, "a flare thrown too far lands 520 px out");
        allyAt = new V2(200f, 200f);
        step(Game.ArmorCallIn + Game.ArmorOpen + 0.2f);
        Check(g.Orbs.Values.Count(o => o.Vest && o.Supply == z2.Id) == Game.ArmorVestsMastered, "mastered, the crate holds 4 vests");
        step(Game.ArmorVestLife + Game.ArmorFold);
        Check(!g.Orbs.Values.Any(o => o.Vest) && !g.Zones.ContainsKey(z2.Id), "unclaimed vests are gone after 20 s, and the crate with them");

        host.Leave(""); client.Leave("");
    }
}

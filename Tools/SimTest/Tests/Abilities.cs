using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Abilities: every class ability and ultimate over the network, and the lobby's loadout preview.
static partial class Program
{
    // Every class ability choice works over the network.
    static void AbilitySweep(float dt)
    {
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            for (int choice = 0; choice < Upgrades.RoleAbilityChoices; choice++)
            {
                int port = nextPort++;
                var host = new Session { AutoStart = 2, StartWave = 3 };
                host.Game.Bot = true;
                host.Host(port, "Host", roles[0], 0, choice);
                var client = new Session();
                client.Game.Bot = true;
                client.Join("127.0.0.1", port, "Friend", roles[1], 0, choice);
                var s = new[] { host, client };
                for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
                bool taunt = false, zone = false, link = false, grenade = false, marked = false;
                int ghosts = 0, frame = 0;
                // Mark needs an enemy on screen when the bot presses it, so give it longer.
                float seconds = roles[0] == Role.Dps && choice == 2 ? 26f : 14f;
                Run(s, dt, seconds, null, () =>
                {
                    foreach (var p in client.Game.Players.Values)
                    {
                        taunt |= p.TauntT > 0f;
                        link |= p.LinkT > 0f;
                    }
                    zone |= client.Game.Zones.Count > 0;
                    foreach (var b in client.Game.Bullets.Values)
                        grenade |= b.Kind == Defs.GrenadeShot;
                    foreach (var e in client.Game.Enemies.Values)
                        marked |= e.Marked;
                    if (++frame % 60 == 0)
                    {
                        for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                        ghosts += Ghosts(s);
                    }
                });
                foreach (int id in new[] { 1, 2 })
                {
                    var ab = client.Game.Players[id].Slots[0].Id;
                    bool seen = true;
                    switch (ab)
                    {
                        case AbilityId.Roar: seen = taunt; break;
                        case AbilityId.Haven: seen = zone; break;
                        case AbilityId.SoulLink: seen = link; break;
                        case AbilityId.Grenade: seen = grenade; break;
                        case AbilityId.Mark: seen = marked; break;
                    }
                    if (Environment.GetEnvironmentVariable("DEBUG") != null)
                        Console.WriteLine("  {0}: seen {1} ghosts {2} wave {3} enemies {4}", ab, seen, ghosts, host.Game.Wave, host.Game.Enemies.Count);
                    Check(ab == Upgrades.RoleAbility(client.Game.Players[id].Role, Kit.Soldier, choice) && seen && ghosts == 0,
                        "class ability works over the network: " + Upgrades.Ability(ab).Name);
                }
                host.Leave("");
                client.Leave("");
            }
        }
    }

    static bool anyChain;

    // Every mage and summoner weapon and ability, over the network.
    static void KitSweep(float dt)
    {
        foreach (var kit in new[] { Kit.Mage, Kit.Summoner })
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        for (int choice = 0; choice < 3; choice++)
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 3 };
            host.Game.Bot = true;
            host.Host(port, "Host", roles[0], choice, choice, kit);
            var client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Friend", roles[1], choice, choice, kit);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            var minions = new HashSet<MinionKind>();
            var zones = new HashSet<ZoneKind>();
            bool aegis = false, shield = false, surge = false, frenzy = false, bolt = false, boltShot = false;
            int ghosts = 0, frame = 0;
            Action watch = () =>
            {
                foreach (var m in client.Game.Minions.Values)
                    minions.Add(m.Kind);
                foreach (var z in client.Game.Zones.Values)
                    zones.Add(z.Kind);
                foreach (var p in client.Game.Players.Values)
                {
                    aegis |= p.AegisT > 0f;
                    shield |= p.ShieldHp > 0f;
                    surge |= p.SurgeT > 0f;
                    frenzy |= p.FrenzyT > 0f;
                }
                foreach (var fx in client.Game.Effects)
                    bolt |= fx.Kind == FxKind.Bolt;
                foreach (var b in client.Game.Bullets.Values)
                    boltShot |= b.Kind == Defs.BoltShot || b.Kind == Defs.HammerShot;
                if (++frame % 60 == 0)
                {
                    for (int k = 0; k < 5; k++) { Thread.Sleep(2); client.Poll(); }
                    ghosts += Ghosts(s);
                }
            };
            Run(s, dt, 13, null, watch);
            // A chain needs two enemies close together when a lightning shot lands; keep going until one does.
            bool lightning = false;
            foreach (var p in client.Game.Players.Values)
                lightning |= p.Weapon.Id == WeaponId.StormHammer || p.Weapon.Id == WeaponId.ChainLight || p.Weapon.Id == WeaponId.ArcLightning;  // Chain Light's arcs are bolts too
            if (lightning && !bolt)
                Run(s, dt, 15, () => bolt, watch);
            foreach (int id in new[] { 1, 2 })
            {
                var p = client.Game.Players[id];
                var w = p.Weapon;
                bool weaponOk = w.Companion == MinionKind.None || minions.Contains(w.Companion);
                if (w.Id == WeaponId.TotemStaff) weaponOk = zones.Contains(ZoneKind.Totem);
                // Chains need a second enemy nearby, so per run just require the shots; a chain is checked below.
                if (w.Id == WeaponId.StormHammer) weaponOk = boltShot;
                if (w.Id == WeaponId.ChainLight || w.Id == WeaponId.ArcLightning) weaponOk = bolt;  // a cone of light or a strip of lightning, no shots: bolts arc to whatever is in it
                bool abilityOk = true;
                switch (p.Slots[0].Id)
                {
                    case AbilityId.ArcaneAegis: abilityOk = aegis; break;
                    case AbilityId.FlameWall: abilityOk = zones.Contains(ZoneKind.FlameWall); break;
                    case AbilityId.ArcaneWard: abilityOk = shield; break;
                    case AbilityId.Meteor: abilityOk = zones.Contains(ZoneKind.Meteor); break;
                    case AbilityId.Blizzard: abilityOk = zones.Contains(ZoneKind.Blizzard); break;
                    case AbilityId.ArcaneSurge: abilityOk = surge; break;
                    case AbilityId.GolemWall: abilityOk = minions.Contains(MinionKind.Wall); break;
                    case AbilityId.GuardianSpirit: abilityOk = minions.Contains(MinionKind.Guardian); break;
                    case AbilityId.SpiritSwarm: abilityOk = minions.Contains(MinionKind.Sprite); break;
                    case AbilityId.Phoenix: abilityOk = minions.Contains(MinionKind.Phoenix); break;
                    case AbilityId.SoulLantern: abilityOk = zones.Contains(ZoneKind.Lantern); break;
                    case AbilityId.BatSwarm: abilityOk = minions.Contains(MinionKind.Bat); break;
                    case AbilityId.AlphaCall: abilityOk = frenzy; break;
                }
                bool kitOk = p.Kit == kit && Math.Abs(host.Game.Players[id].MaxHp - p.Def.Hp * Defs.KitHpMult(kit)) < 0.5f
                    && p.Slots[0].Id == Upgrades.RoleAbility(p.Role, kit, choice) && w == Defs.Weapon(p.Role, kit, choice);
                Check(kitOk && weaponOk && abilityOk && ghosts == 0, string.Format("{0} {1}: {2} + {3} work over the network",
                    kit, p.Def.Name, w.Name, Upgrades.Ability(p.Slots[0].Id).Name));
            }
            anyChain |= bolt;
            host.Leave("");
            client.Leave("");
        }
        Check(anyChain, "lightning chains between enemies (seen on the client)");
    }

    // Every role and class has its own ultimate on right click, and each one works over the network.
    static void UltimateSweep(float dt)
    {
        var seenUlts = new HashSet<AbilityId>();
        foreach (var kit in new[] { Kit.Soldier, Kit.Mage, Kit.Summoner })
        foreach (var roles in new[] { new[] { Role.Tank, Role.Healer }, new[] { Role.Dps, Role.Dps } })
        {
            int port = nextPort++;
            var host = new Session { AutoStart = 2, StartWave = 9 };  // enemies tough enough to survive Glacial Tomb's hit
            host.Game.Bot = true;
            host.Host(port, "Host", roles[0], 0, 0, kit);
            var client = new Session();
            client.Game.Bot = true;
            client.Join("127.0.0.1", port, "Friend", roles[1], 0, 0, kit);
            var s = new[] { host, client };
            for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
            Run(s, dt, 6, () => host.Game.Enemies.Count >= 4 && host.Game.State == MatchState.Fighting);
            Func<int, Player> mine = id => (id == 1 ? host : client).Game.Players[id];
            foreach (int id in new[] { 1, 2 })
            {
                foreach (var g in new[] { host.Game.Players[id], mine(id) })  // fill the ultimate on both sides
                {
                    g.Slots[Upgrades.UltSlot].Charges = 1;
                    g.Slots[Upgrades.UltSlot].Timer = 0f;
                }
                if (kit == Kit.Soldier && roles[0] == Role.Tank)
                    host.Game.Players[id].Hp = 10f;  // for Lifeline to heal
                host.Game.Players[id].InvulnT = 6f;  // don't get downed before casting
            }
            var minions = new Dictionary<MinionKind, int>();
            var zones = new Dictionary<ZoneKind, int>();
            var ultT = new HashSet<int>();
            bool frozen = false;
            var fullHp = new HashSet<int>();
            Run(s, dt, 5, null, () =>
            {
                var mc = new Dictionary<MinionKind, int>();
                foreach (var m in client.Game.Minions.Values)
                    mc[m.Kind] = (mc.ContainsKey(m.Kind) ? mc[m.Kind] : 0) + 1;
                foreach (var kv in mc)
                    minions[kv.Key] = Math.Max(minions.ContainsKey(kv.Key) ? minions[kv.Key] : 0, kv.Value);
                var zc = new Dictionary<ZoneKind, int>();
                foreach (var z in client.Game.Zones.Values)
                    zc[z.Kind] = (zc.ContainsKey(z.Kind) ? zc[z.Kind] : 0) + 1;
                foreach (var kv in zc)
                    zones[kv.Key] = Math.Max(zones.ContainsKey(kv.Key) ? zones[kv.Key] : 0, kv.Value);
                foreach (var p in client.Game.Players.Values)
                    if (p.UltT > 0f)
                        ultT.Add(p.Id);
                foreach (var p in host.Game.Players.Values)
                    if (p.Hp >= p.MaxHp - 0.5f)
                        fullHp.Add(p.Id);
                foreach (var e in client.Game.Enemies.Values)
                    frozen |= e.Frozen;  // the client sees them frozen (drawn in ice with a snowflake)
            });
            foreach (int id in new[] { 1, 2 })
            {
                var hp = host.Game.Players[id];
                var ult = hp.Slots[Upgrades.UltSlot].Id;
                seenUlts.Add(ult);
                bool used = hp.Slots[Upgrades.UltSlot].Charges == 0 && hp.Slots[Upgrades.UltSlot].Timer > 0f;
                Func<MinionKind, int> M = k => minions.ContainsKey(k) ? minions[k] : 0;
                Func<ZoneKind, int> Z = k => zones.ContainsKey(k) ? zones[k] : 0;
                bool effect = false;
                switch (ult)
                {
                    case AbilityId.Fortress: case AbilityId.BulletStorm: effect = ultT.Contains(id); break;
                    case AbilityId.GlacialTomb: effect = frozen; break;
                    case AbilityId.Titan: effect = M(MinionKind.Titan) > 0; break;
                    case AbilityId.Lifeline: effect = fullHp.Count == 2; break;
                    case AbilityId.Sanctum: effect = Z(ZoneKind.Sanctum) > 0; break;
                    case AbilityId.WorldTree: effect = Z(ZoneKind.Grove) > 0 && M(MinionKind.Sprite) >= 4; break;
                    case AbilityId.MeteorStorm: effect = Z(ZoneKind.Meteor) >= 10; break;
                    case AbilityId.Legion: effect = M(MinionKind.Wolf) >= 6 && M(MinionKind.Bat) >= 6; break;
                }
                bool synced = client.Game.Players[id].Slots[Upgrades.UltSlot].Id == ult && ult == Upgrades.Ultimate(hp.Role, kit);
                Check(used && effect && synced, string.Format("{0} {1}: ultimate {2} works over the network", kit, hp.Def.Name, Upgrades.Ability(ult).Name));
            }
            host.Leave("");
            client.Leave("");
        }
        Check(seenUlts.Count == 9, "all 9 role and class combinations have their own ultimate");
    }

    // The lobby's loadout preview (Showcase): an offline copy of the game where every weapon hits the
    // target dummies and every Space ability gets cast, with no levelling or mastery building up.
    static void ShowcaseTest(float dt)
    {
        var quiet = new List<string>();
        var uncast = new List<string>();
        bool grew = false;
        int weapons = 0, abilities = 0;
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var kit = (Kit)k;
                for (int w = 0; w < Defs.WeaponsPerRole; w++)
                {
                    var sc = new Showcase();
                    sc.Show(role, kit, w, Upgrades.RoleAbility(role, kit, 0), false);
                    for (int f = 0; f < 360; f++)
                        sc.Step(dt);
                    var g = sc.Game;
                    bool hit = g.Score > 0 || g.Enemies.Values.Any(e => e.Hp < e.MaxHp - 0.5f);
                    if (hit) weapons++; else quiet.Add(sc.Me.Weapon.Name);
                    sc.Stop();
                }
                for (int a = 0; a < Upgrades.RoleAbilityChoices; a++)
                {
                    var sc = new Showcase();
                    var id = Upgrades.RoleAbility(role, kit, a);
                    sc.Show(role, kit, 0, id, true);
                    for (int f = 0; f < 300; f++)
                        sc.Step(dt);
                    var me = sc.Me;
                    if (me.Slots[0].Id == id && me.Run.AbilitiesUsed >= 2) abilities++; else uncast.Add(Upgrades.Ability(id).Name + " (" + me.Run.AbilitiesUsed + ")");
                    grew |= me.Level > 1 || me.MasteryXp.Values.Any(x => x > 0f);
                    sc.Stop();
                }
            }
        Console.WriteLine("  showcase: {0} weapons hit the dummies{1}; {2} abilities cast on a loop{3}", weapons,
            quiet.Count > 0 ? " (silent: " + string.Join(", ", quiet) + ")" : "", abilities, uncast.Count > 0 ? " (not cast: " + string.Join(", ", uncast) + ")" : "");
        Check(quiet.Count == 0, "the loadout preview: every weapon hits the target dummies");
        Check(uncast.Count == 0, "the loadout preview: every Space ability is cast on a loop");
        Check(!grew, "the loadout preview gives no XP or mastery");

        // Switching to another ability restarts the preview: nothing from the last one plays on.
        {
            var sc = new Showcase();
            sc.Show(Role.Healer, Kit.Soldier, 0, AbilityId.Haven, true);
            for (int f = 0; f < 120; f++) sc.Step(dt);
            bool zoneUp = sc.Game.Zones.Count > 0;
            sc.Show(Role.Healer, Kit.Soldier, 0, AbilityId.SoulLink, true);
            sc.Step(dt);
            Console.WriteLine("  showcase switch: Haven's zone up {0}; after switching to Soul Link: {1} zones, {2} casts", zoneUp, sc.Game.Zones.Count, sc.Me.Run.AbilitiesUsed);
            Check(zoneUp && sc.Game.Zones.Count == 0 && sc.Me.Run.AbilitiesUsed == 0, "switching the previewed ability restarts the preview (the last one's effects are gone)");
            sc.Stop();
        }

        // The ultimate can be previewed too: every role and class's ultimate is cast on a loop.
        var ultsUncast = new List<string>();
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                var kit = (Kit)k;
                var sc = new Showcase();
                sc.Show(role, kit, 0, Upgrades.RoleAbility(role, kit, 0), true, true);
                for (int f = 0; f < 120; f++) sc.Step(dt);
                var me = sc.Me;
                var ult = Upgrades.Ultimate(role, kit);
                if (me.Slots[Upgrades.UltSlot].Id != ult || me.Run.AbilitiesUsed < 1 || me.Slots[Upgrades.UltSlot].Charges >= me.Slots[Upgrades.UltSlot].MaxCharges)
                    ultsUncast.Add(Upgrades.Ability(ult).Name);
                sc.Stop();
            }
        Console.WriteLine("  showcase ultimates: {0} of {1} cast{2}", 3 * Defs.KitCount - ultsUncast.Count, 3 * Defs.KitCount, ultsUncast.Count > 0 ? " (not cast: " + string.Join(", ", ultsUncast) + ")" : "");
        Check(ultsUncast.Count == 0, "the loadout preview casts every ultimate when you click it");
    }
}

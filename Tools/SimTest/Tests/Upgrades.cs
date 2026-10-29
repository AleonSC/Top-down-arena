using System.Linq;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using Arena;

// Upgrade cards: offers, the stat sheet, rerolls, legendaries, weapon mods, class ability cards, mastery.
static partial class Program
{
    static void CheckStatSheet()
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var p = new Player { Role = Role.Dps, Def = Defs.Get(Role.Dps), BaseHp = 95f, RoleAbility = AbilityId.Dash };
        var taken = new List<int> { U("Sharpened Rounds"), U("Hollow Points"), U("Fortitude"), U("Multishot"), U("Blink"), U("Executioner") };
        var lines = StatSheet.Describe(p, taken);
        var map = new Dictionary<string, string>();
        foreach (var l in lines)
            if (!l.Header)
                map[l.Label] = l.Value;
        Console.WriteLine("  stat sheet: " + string.Join(" | ", Array.ConvertAll(lines.ToArray(), l => l.Header ? "[" + l.Label + "]" : l.Label + "=" + l.Value)));
        Check(map["Damage"] == "128%" && map["Max HP"] == "135" && map["Extra projectiles"] == "+1" && map["Q"] == "Blink"
            && map.ContainsKey("Executioner") && map["Regeneration"] == "0.5 HP/s", "stat sheet shows the right numbers");
        var withCard = new List<int>(taken) { U("Quick Trigger") };
        Check(StatSheet.Describe(p, withCard).Find(l => l.Label == "Fire rate").Value == "108%", "stat preview reflects the selected card");
    }

    // Every ability has one complete entry, and every lobby place (Space choice, ultimate) holds exactly one.
    static void CheckAbilityRegistry()
    {
        var ids = Enum.GetValues(typeof(AbilityId)).Cast<AbilityId>().Where(a => a != AbilityId.None).ToList();
        bool complete = ids.All(a => Upgrades.Ability(a).Id == a && !string.IsNullOrEmpty(Upgrades.Ability(a).Name)
            && !string.IsNullOrEmpty(Upgrades.Ability(a).Desc) && !string.IsNullOrEmpty(Upgrades.Ability(a).Capstone));
        var placed = new List<AbilityId>();
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                placed.Add(Upgrades.Ultimate(role, (Kit)k));
                for (int c = 0; c < Upgrades.RoleAbilityChoices; c++)
                    placed.Add(Upgrades.RoleAbility(role, (Kit)k, c));
            }
        bool lobby = placed.Distinct().Count() == placed.Count && placed.All(a => a != AbilityId.None)
            && placed.All(a => Upgrades.Ability(a).Choice != AbilityDef.CardOnly)
            && ids.Count(a => Upgrades.Ability(a).Choice != AbilityDef.CardOnly) == placed.Count;
        Check(complete && lobby, "every ability has a complete entry, and each lobby place holds exactly one");
    }

    static void CheckOffers()
    {
        var rng = new Random(1);
        bool ok = true;
        var rarityCount = new int[4];
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
        for (int choice = 0; choice < Upgrades.RoleAbilityChoices; choice++)
        {
            var ab = Upgrades.RoleAbility(role, Kit.Soldier, choice);
            var taken = new List<int>();
            for (int level = 1; level < 60; level++)
            {
                var offer = Upgrades.RollOffer(rng, role, ab, taken, level);
                var seen = new HashSet<int>();
                foreach (int id in offer)
                {
                    ok &= id >= 0 && seen.Add(id) && Upgrades.Available(id, role, ab, taken);
                    ok &= Upgrades.All[id].Rarity != Rarity.Legendary;  // legendaries only come from bosses
                    ok &= Upgrades.All[id].RequiresAbility == AbilityId.None || Upgrades.HasAbility(Upgrades.All[id].RequiresAbility, ab, taken);  // on any key
                    if (id >= 0)
                        rarityCount[(int)Upgrades.All[id].Rarity]++;
                    if (id >= 0 && Upgrades.All[id].Grants != AbilityId.None)
                        ok &= !taken.Contains(id);
                }
                taken.Add(offer[rng.Next(3)]);
            }
            int grants = 0;
            foreach (int t in taken)
                if (Upgrades.All[t].Grants != AbilityId.None)
                    grants++;
            ok &= grants <= Upgrades.BonusSlots;
        }
        Console.WriteLine("offer rarities seen: common {0}, uncommon {1}, rare {2}, very rare {3}", rarityCount[0], rarityCount[1], rarityCount[2], rarityCount[3]);
        Check(ok, "offers are 3 distinct, eligible upgrades; at most 3 new abilities");
    }

    // Start at the boss wave with boosted damage so the bots kill the boss,
    // then check everyone got the bonus rerolls.
    static void BossRerollScenario(float dt)
    {
        var host = new Session { AutoStart = 2, StartWave = 5 };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        Check(host.Host(7791, "Host", Role.Dps, 1), "boss scenario: host listens");
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", 7791, "Friend", Role.Dps, 2);
        for (int i = 0; i < 300 && client.Screen != AppScreen.Game; i++) { Step(host, dt); Step(client, dt); Thread.Sleep(2); }
        int hollow = Array.FindIndex(Upgrades.All, u => u.Name == "Hollow Points");
        for (int k = 0; k < 25; k++)
        {
            host.Game.GrantUpgrade(1, hollow);
            host.Game.GrantUpgrade(2, hollow);
        }
        bool bossSeen = false, bossDead = false, bigOrb = false;
        for (int i = 0; i < 60 * 60 && !bossDead; i++)
        {
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Thread.Sleep(1);
            bool bossAlive = false;
            foreach (var e in host.Game.Enemies.Values)
                bossAlive |= e.Def.IsBoss;
            bossSeen |= bossAlive;
            bossDead = bossSeen && !bossAlive;
            foreach (var o in client.Game.Orbs.Values)
                bigOrb |= o.Big;
            if (host.Game.State == MatchState.GameOver)
                break;
        }
        for (int i = 0; i < 20; i++)
        {
            Step(host, dt);
            Step(client, dt);
            Thread.Sleep(2);
            foreach (var o in client.Game.Orbs.Values)
                bigOrb |= o.Big;
        }
        Console.WriteLine("  boss dead: {0}, rerolls host {1}/{2}, client sees {3}/{4}", bossDead, host.Game.Players[1].Rerolls, host.Game.Players[2].Rerolls,
            client.Game.Players[1].Rerolls, client.Game.Players[2].Rerolls);
        Check(bossDead && host.Game.Players[1].Rerolls == 4 && client.Game.Players[2].Rerolls == 4, "boss kill gives everyone +2 rerolls");
        Check(bigOrb, "boss drops a large health orb (seen on the client)");
        // After the wave, each player picks one of The Warden's legendary weapon upgrades.
        Run(new[] { host, client }, dt, 20, () => host.Game.State == MatchState.Fighting && host.Game.Wave > 5);
        int legendaries = 0;
        foreach (var p in client.Game.Players.Values)
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Rarity == Rarity.Legendary && Upgrades.All[t].Boss == EnemyType.Boss)
                {
                    legendaries++;
                    Console.WriteLine("  {0} took legendary: {1}", p.Role, Upgrades.All[t].Name);
                }
        Check(legendaries == 2, "boss kill gives each player one legendary weapon upgrade from that boss (seen on client)");
        int mods = 0;
        foreach (var p in client.Game.Players.Values)
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Rarity == Rarity.Mod)
                {
                    mods++;
                    Console.WriteLine("  {0} took weapon mod: {1}", p.Role, Upgrades.All[t].Name);
                }
        Check(mods == 2, "after the legendary, each player also picks one weapon mod (seen on client)");
        host.Leave("");
        client.Leave("");
    }

    // Every legendary active at once: their extra shots, splits and turrets stay in sync.
    static void LegendarySync(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        for (int id = 0; id < Upgrades.All.Length; id++)
            if (Upgrades.All[id].Rarity == Rarity.Legendary)
            {
                s[0].Game.GrantUpgrade(1, id);
                s[0].Game.GrantUpgrade(2, id);
            }
        bool swarm = false;
        int ghosts = 0, frame = 0;
        Run(s, dt, 15, null, () =>
        {
            foreach (var b in s[1].Game.Bullets.Values)
                swarm |= b.Kind == Defs.SwarmShot;
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
            }
        });
        Check(swarm && ghosts == 0, "all legendary weapon effects at once stay in sync");
        s[0].Leave(""); s[1].Leave("");
    }

    // Every weapon mod at full stacks: homing shots curve and stay in sync, ricochets
    // happen, and burning / chilled enemies show up on the client.
    static void WeaponModTest(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        var offer = Upgrades.ModOffer(Role.Dps, Upgrades.RoleAbility(Role.Dps, Kit.Soldier, 0), new List<int>());
        Check(offer != null && offer.Length == 8, "the weapon mod offer lists all 8 mods");
        foreach (int id in offer)
            for (int k = 0; k < 3; k++)
            {
                s[0].Game.GrantUpgrade(1, id);
                s[0].Game.GrantUpgrade(2, id);
            }
        Check(Upgrades.ModOffer(Role.Dps, AbilityId.Dash, s[0].Game.Players[2].Taken) == null, "maxed-out mods are no longer offered");
        var spawnAngle = new Dictionary<int, float>();
        int curved = 0, ricochets = 0, ghosts = 0, frame = 0, compared = 0;
        float worst = 0f;
        bool burning = false, chilled = false;
        Run(s, dt, 15, null, () =>
        {
            foreach (var b in s[0].Game.Bullets.Values)
            {
                if (b.Owner == 0 || b.Homing <= 0f)
                    continue;
                float a0;
                if (!spawnAngle.TryGetValue(b.Id, out a0))
                    spawnAngle[b.Id] = b.Vel.Angle;
                else if (Math.Abs(MathX.Fmod(b.Vel.Angle - a0 + MathX.Pi, MathX.Tau) - MathX.Pi) > 0.3f)
                {
                    curved++;
                    spawnAngle[b.Id] = 99f;  // count each bullet once
                }
                if (b.Bounces < 3 && b.Bounces >= 0 && b.LastHit != 0)
                    ricochets++;
            }
            foreach (var e in s[1].Game.Enemies.Values)
            {
                burning |= e.Burning;
                chilled |= e.Chilled;
            }
            if (++frame % 60 == 0)
            {
                for (int k = 0; k < 5; k++) { Thread.Sleep(2); s[1].Poll(); }
                ghosts += Ghosts(s);
                foreach (var b in s[1].Game.Bullets.Values)
                {
                    Bullet hb;
                    if (b.Homing > 0f && s[0].Game.Bullets.TryGetValue(b.Id, out hb))
                    {
                        worst = Math.Max(worst, hb.Pos.DistanceTo(b.Pos));
                        compared++;
                    }
                }
            }
        });
        Console.WriteLine("  {0} homing shots curved, {1} ricochet sightings, homing drift {2:0.0}px over {3} samples, burning {4}, chilled {5}",
            curved, ricochets, worst, compared, burning, chilled);
        Check(curved > 20, "Homing Rounds curve toward enemies");
        Check(ricochets > 0, "Ricochet shots bounce to another enemy");
        Check(compared > 0 && worst < 40f && ghosts == 0, "homing shots stay in sync on the client");
        Check(burning && chilled, "burning and chilled enemies show on the client");
        s[0].Leave(""); s[1].Leave("");
    }

    // Class ability cards: very rare cards for your class's two other Space abilities, which go on Q, E or Shift.
    static void ClassAbilityTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var cards = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].ClassAbility).ToList();
        int right = 0, combos = 0;
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
                for (int c = 0; c < Upgrades.RoleAbilityChoices; c++)
                {
                    var ab = Upgrades.RoleAbility(role, (Kit)k, c);
                    var offered = cards.Where(i => Upgrades.Available(i, role, ab, new List<int>())).Select(i => Upgrades.All[i].Grants).OrderBy(x => x).ToList();
                    var expect = Enumerable.Range(0, Upgrades.RoleAbilityChoices).Where(o => o != c).Select(o => Upgrades.RoleAbility(role, (Kit)k, o)).OrderBy(x => x).ToList();
                    combos++;
                    if (offered.SequenceEqual(expect))
                        right++;
                }
        Console.WriteLine("  class ability cards: {0} cards; the right two offered for {1}/{2} role/class/ability picks", cards.Count, right, combos);
        Check(cards.Count == 3 * Defs.KitCount * Upgrades.RoleAbilityChoices && right == combos, "each class is offered exactly its two other Space abilities as very rare cards");

        var rng = new Random(7);
        int veryRare = 0, cls = 0;
        for (int n = 0; n < 4000; n++)
            foreach (int id in Upgrades.RollOffer(rng, Role.Tank, AbilityId.Roar, new List<int>(), 20))
                if (Upgrades.All[id].Rarity == Rarity.VeryRare)
                {
                    veryRare++;
                    if (Upgrades.All[id].ClassAbility)
                        cls++;
                }
        float frac = cls / (float)Math.Max(1, veryRare);
        Console.WriteLine("  very rare cards rolled: {0}, class abilities {1:0%} (6 in 11 = 55%)", veryRare, frac);
        Check(frac > 0.49f && frac < 0.61f, "class ability cards count triple among very rare cards");

        var taken = new List<int> { U("Barrier") };  // a Tank Soldier who picked Roar, then found Barrier
        Check(!Upgrades.Available(U("Barrier"), Role.Tank, AbilityId.Roar, taken) && Upgrades.Available(U("Bastion"), Role.Tank, AbilityId.Roar, taken)
            && !Upgrades.Available(U("Bastion"), Role.Tank, AbilityId.Roar, new List<int>()),
            "a class ability card isn't offered twice, and that ability's own upgrades open up");

        // In a match: the DPS Soldier (Space: Dash) gets Cluster Grenade on Q, and the bot throws it.
        var s = StartBuffed(dt, 1, 0);
        Run(s, dt, 1);
        s[0].Game.GrantUpgrade(2, U("Cluster Grenade"));
        bool thrown = false;
        Run(s, dt, 9, null, () => thrown |= s[0].Game.Bullets.Values.Any(b => b.Kind == Defs.GrenadeShot && b.Owner == 2));
        var hp = s[0].Game.Players[2];
        var cp = s[1].Game.Players[2];
        Console.WriteLine("  DPS Soldier: Space {0}, Q {1} (client sees {2}); grenade thrown {3}", hp.Slots[0].Id, hp.Slots[1].Id, cp.Slots[1].Id, thrown);
        Check(hp.Slots[0].Id == AbilityId.Dash && hp.Slots[1].Id == AbilityId.Grenade && cp.Slots[1].Id == AbilityId.Grenade && thrown,
            "a class ability card puts that ability on Q, E or Shift, and it works there");
        s[0].Leave(""); s[1].Leave("");
    }

    // Ability mastery: XP from use, levels give power and shorter cooldowns, level 5 unlocks a capstone.
    static void MasteryTest(float dt)
    {
        var s = StartBuffed(dt, 3, 0);
        s[0].Game.Bot = s[1].Game.Bot = false;
        Run(s, dt, 6, () => s[0].Game.State == MatchState.Fighting);
        var host = s[0].Game;
        var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
        var cast = typeof(Game).GetMethod("HostAbility", flags);
        var spawn = typeof(Game).GetMethod("SpawnEnemyAt", flags);
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var hp = host.Players[1];
        host.DevSetGod(1, true);
        host.DevSetGod(2, true);
        hp.Pos = s[1].Game.Players[1].Pos = new V2(600f, 450f);
        // A frozen, unkillable dummy right next to the host.
        Func<V2, Enemy> Dummy = at =>
        {
            var e = (Enemy)spawn.Invoke(host, new object[] { EnemyType.Grunt, at, 0f, 0, false });
            e.Hp = e.MaxHp = 1e6f;
            return e;
        };
        foreach (var e in host.Enemies.Values) e.Hp = 0f;
        Run(s, dt, 0.2f);
        var dummy = Dummy(new V2(680f, 450f));
        Action Hold = () => { foreach (var e in host.Enemies.Values) { e.FrozenT = 1f; if (e != dummy && e.Hp < 1e5f) e.Hp = 0f; } };
        Run(s, dt, 0.3f, null, Hold);

        host.GrantUpgrade(1, U("Shockwave"));
        Run(s, dt, 0.2f);
        int slot = Array.FindIndex(hp.Slots, sl => sl.Id == AbilityId.Shockwave);
        Func<float> Xp = () => { float x; hp.MasteryXp.TryGetValue(AbilityId.Shockwave, out x); return x; };
        float cd1 = host.Cooldown(hp, slot);
        float before = dummy.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float dmg1 = before - dummy.Hp, xpHit = Xp();
        Run(s, dt, 0.1f, null, Hold);
        // Cast with nothing in range: it still earns a little (instant abilities earn by what they hit).
        hp.Pos = s[1].Game.Players[1].Pos = new V2(1400f, 800f);
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float xpMiss = Xp() - xpHit;
        Console.WriteLine("  shockwave XP: {0:0.0} for a hit, {1:0.0} for a miss (cooldown {2:0.0}s)", xpHit, xpMiss, cd1);
        Check(xpHit > 0f && xpMiss > 0f && xpMiss < xpHit * 0.8f, "abilities earn mastery XP, more when they land");

        // Level 3: +16% damage, -8% cooldown.
        host.GainMastery(hp, AbilityId.Shockwave, Game.MasteryXpFor[2] - Xp());
        hp.Pos = s[1].Game.Players[1].Pos = new V2(600f, 450f);
        Run(s, dt, 0.3f, null, Hold);
        before = dummy.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float dmg3 = before - dummy.Hp, cd3 = host.Cooldown(hp, slot);
        var cp = s[1].Game.Players[1];
        Console.WriteLine("  shockwave level {0}: damage {1:0.0} -> {2:0.0}, cooldown {3:0.00}s -> {4:0.00}s; client sees level {5}",
            Game.MasteryLevel(hp, AbilityId.Shockwave), dmg1, dmg3, cd1, cd3, Game.MasteryLevel(cp, AbilityId.Shockwave));
        Check(Game.MasteryLevel(hp, AbilityId.Shockwave) == 3 && Math.Abs(dmg3 / dmg1 - 1.16f) < 0.02f && Math.Abs(cd3 / cd1 - 0.92f) < 0.01f,
            "each mastery level adds 8% potency and takes 4% off the cooldown");


        // Capstones: Shockwave knocks back, Nova fires 48, Dash gets a charge, Mark hits three.
        host.GainMastery(hp, AbilityId.Shockwave, 1e4f);
        var pushed = Dummy(new V2(660f, 470f));
        Run(s, dt, 0.3f, null, Hold);
        V2 was = pushed.Pos;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float shove = pushed.Pos.DistanceTo(was);
        host.GrantUpgrade(1, U("Nova"));
        Run(s, dt, 0.2f, null, Hold);
        int nova = Array.FindIndex(hp.Slots, sl => sl.Id == AbilityId.Nova);
        Func<int> NovaShots = () => host.Bullets.Values.Count(b => b.Kind == Defs.NovaShot);
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaBase = NovaShots();
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        host.GainMastery(hp, AbilityId.Nova, 1e4f);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaMax = NovaShots();
        var dp = host.Players[2];
        int dash = Array.FindIndex(dp.Slots, sl => sl.Id == AbilityId.Dash);
        int dashBefore = dash >= 0 ? dp.Slots[dash].MaxCharges : -1;
        host.DevMasterAll(2);
        Run(s, dt, 0.3f, null, Hold);
        int dashAfter = dash >= 0 ? dp.Slots[dash].MaxCharges : -1, dashClient = dash >= 0 ? s[1].Game.Players[2].Slots[dash].MaxCharges : -1;
        Console.WriteLine("  capstones: shockwave shoves {0:0} px, nova {1} -> {2} shots, dash charges {3} -> {4} (client {5}), {6} gold",
            shove, novaBase, novaMax, dashBefore, dashAfter, dashClient, string.Join("/", dp.Slots.Where(sl => sl.Id != AbilityId.None).Select(sl => Game.MasteryLevel(dp, sl.Id))));
        Check(shove > 100f && novaBase == 24 && novaMax == 48, "level 5 unlocks each ability's capstone");
        Check(Game.MasteryLevel(cp, AbilityId.Shockwave) == 5 && host.MasteryToasts.Any(t => t.Ability == AbilityId.Shockwave && t.Level == 3)
            && s[1].Game.MasteryToasts.Any(t => t.Ability == AbilityId.Dash && t.Level == 5),
            "the player's own game knows the level (and shows a level-up popup)");
        Check(dash < 0 || dashAfter == dashBefore + 1 && dashClient == dashAfter, "a mastered Dash gets an extra charge on both games");
        Check(dp.Slots.Where(sl => sl.Id != AbilityId.None).All(sl => Game.Mastered(dp, sl.Id)), "the dev menu can master every ability");
        Check(Enum.GetValues(typeof(AbilityId)).Cast<AbilityId>().Where(a => a != AbilityId.None).All(a => Game.Capstone(a).Length > 0), "every ability has a capstone");

        // No XP between waves.
        float xpNow = Xp();
        host.State = MatchState.Intermission;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        Check(Xp() == xpNow, "no mastery XP outside a fight");
        s[0].Leave(""); s[1].Leave("");
    }
}

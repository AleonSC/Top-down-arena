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
        var p = new Player { Role = Role.Dps, Def = Defs.Get(Role.Dps), BaseHp = 95f, FirstAbility = AbilityId.PoisonDart };
        var taken = new List<int> { U("Sharpened Rounds"), U("Hollow Points"), U("Fortitude"), U("Multishot"), U("Blink"), U("Executioner") };
        var lines = StatSheet.Describe(p, taken);
        var map = new Dictionary<string, string>();
        foreach (var l in lines)
            if (!l.Header)
                map[l.Label] = l.Value;
        Console.WriteLine("  stat sheet: " + string.Join(" | ", Array.ConvertAll(lines.ToArray(), l => l.Header ? "[" + l.Label + "]" : l.Label + "=" + l.Value)));
        Check(map["Damage"] == "130%" && map["Max HP"] == "135" && map["Extra projectiles"] == "+1" && map["Q"] == "Blink"
            && map.ContainsKey("Executioner") && map["Regeneration"] == "0.0 HP/s", "stat sheet shows the right numbers");
        var withCard = new List<int>(taken) { U("Quick Trigger") };
        Check(StatSheet.Describe(p, withCard).Find(l => l.Label == "Fire rate").Value == "110%", "stat preview reflects the selected card");
    }

    // Every ability has one complete entry, and every lobby place (1st ability choice, ultimate) holds exactly one.
    static void CheckAbilityRegistry()
    {
        var ids = Enum.GetValues(typeof(AbilityId)).Cast<AbilityId>().Where(a => a != AbilityId.None).ToList();
        bool complete = ids.All(a => Upgrades.Ability(a).Id == a && !string.IsNullOrEmpty(Upgrades.Ability(a).Name)
            && !string.IsNullOrEmpty(Upgrades.Ability(a).Desc) && !string.IsNullOrEmpty(Upgrades.Ability(a).Capstone));
        var placed = new List<AbilityId>();
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
            {
                if (!Upgrades.UltGap(role, (Kit)k))  // (a class still being built: its stand-ins aren't places of their own)
                    placed.Add(Upgrades.Ultimate(role, (Kit)k));
                for (int u = 1; u < Upgrades.UltKeys; u++)  // the other keys of the lobby's Ultimate row (the Juggernaut Suit)
                    if (Upgrades.AltUltimate(role, (Kit)k, u) != AbilityId.None)
                        placed.Add(Upgrades.AltUltimate(role, (Kit)k, u));
                for (int c = 0; c < Upgrades.AbilityLimit(role, (Kit)k); c++)  // (a 4th where the class has one: Flood)
                    if (!Upgrades.AbilityGap(role, (Kit)k, c))
                        placed.Add(Upgrades.FirstAbility(role, (Kit)k, c));
            }
        bool lobby = placed.Distinct().Count() == placed.Count && placed.All(a => a != AbilityId.None)
            && placed.All(a => Upgrades.Ability(a).Choice != AbilityDef.CardOnly)
            && ids.Count(a => Upgrades.Ability(a).Choice != AbilityDef.CardOnly) == placed.Count;
        Check(complete && lobby, "every ability has a complete entry, and each lobby place holds exactly one");
        // Kits.Zones and Kits.Minions are read by kind number: an enum value added or removed without its entry shifts
        // every later kind's numbers (it compiles, and only some zone tests notice).
        int zoneKinds = Enum.GetValues(typeof(ZoneKind)).Length;
        int minionKinds = Enum.GetValues(typeof(MinionKind)).Cast<MinionKind>().Count(k => k != MinionKind.None);
        Check(Kits.Zones.Length == zoneKinds && Kits.Minions.Length == minionKinds,
            "every zone and minion kind has exactly one definition (" + Kits.Zones.Length + "/" + zoneKinds + " zones, " + Kits.Minions.Length + "/" + minionKinds + " minions)");
    }

    static void CheckOffers()
    {
        var rng = new Random(1);
        bool ok = true;
        var rarityCount = new int[4];
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
        for (int choice = 0; choice < Upgrades.FirstAbilityChoices; choice++)
        {
            var ab = Upgrades.FirstAbility(role, Kit.Soldier, choice);
            var taken = new List<int>();
            for (int level = 1; level < 60; level++)
            {
                var offer = Upgrades.RollOffer(rng, new OfferCtx(role, ab, taken), level);
                var seen = new HashSet<int>();
                foreach (int id in offer)
                {
                    ok &= id >= 0 && seen.Add(id) && Upgrades.Available(id, new OfferCtx(role, ab, taken));
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
            ok &= grants <= Upgrades.CardAbilitySlots;
        }
        Console.WriteLine("offer rarities seen: common {0}, uncommon {1}, rare {2}, very rare {3}", rarityCount[0], rarityCount[1], rarityCount[2], rarityCount[3]);
        Check(ok, "offers are 3 distinct, eligible upgrades; at most 3 new abilities");
    }

    // Start at the boss wave with boosted damage so the bots kill the boss,
    // then check everyone got the bonus rerolls.
    static void BossRerollScenario(float dt)
    {
        var host = new Session { AutoStart = 2, StartWave = -5 };
        host.Game.Bot = true;
        host.Game.BotRerolls = false;
        Check(host.Host(7791, "Host", Role.Dps, 1), "boss scenario: host listens");
        var client = new Session();
        client.Game.Bot = true;
        client.Game.BotRerolls = false;
        client.Join("127.0.0.1", 7791, "Friend", Role.Dps, 2);
        WaitForGame(host, client, dt);
        int hollow = Array.FindIndex(Upgrades.All, u => u.Name == "Hollow Points");
        for (int k = 0; k < 25; k++)
        {
            host.Game.GrantUpgrade(1, hollow);
            host.Game.GrantUpgrade(2, hollow);
        }
        bool bossSeen = false, bossDead = false, bigOrb = false;
        for (int i = 0; i < 60 * 240 && !bossDead; i++)  // bosses are tough (BossHpMult): allow a few minutes
        {
            foreach (var p in host.Game.Players.Values)
                p.InvulnT = 1f;  // this is about the rewards, so the bots mustn't lose first
            Step(host, dt);
            Step(client, dt);
            if (i % 2 == 0)
                Nap(1);
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
            Nap(2);
            foreach (var o in client.Game.Orbs.Values)
                bigOrb |= o.Big;
        }
        Console.WriteLine("  boss dead: {0} ({5}), rerolls host {1}/{2}, client sees {3}/{4}", bossDead, host.Game.Players[1].Rerolls, host.Game.Players[2].Rerolls,
            client.Game.Players[1].Rerolls, client.Game.Players[2].Rerolls, host.Game.State);
        Check(bossDead && host.Game.Players[1].Rerolls == 4 && client.Game.Players[2].Rerolls == 4, "boss kill gives everyone +2 rerolls");
        Check(bigOrb, "boss drops a large health orb (seen on the client)");
        // After the wave, each player picks one of The Warden's legendary weapon upgrades.
        Run(new[] { host, client }, dt, 20, () => host.Game.State == MatchState.Fighting && host.Game.Wave > 5);
        int legendaries = 0;
        foreach (var p in client.Game.Players.Values)
            foreach (int t in p.Taken)
                if (Upgrades.All[t].Rarity == Rarity.Legendary && Upgrades.All[t].Boss == EnemyType.Warden)
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
                for (int k = 0; k < 5; k++) { Nap(2); s[1].Poll(); }
                ghosts += Ghosts(s);
            }
        });
        Check(swarm && ghosts == 0, "all legendary weapon effects at once stay in sync");
        s[0].Leave(""); s[1].Leave("");
    }

    // Every offer reads the player's weapon (Docs/UpgradePools.md step 2): a weapon-only weapon mod, legendary or
    // milestone card goes to that weapon and no other, and Game.OfferFor carries the player's weapon, class and arena.
    static void OfferWeaponTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var none = new List<int>();
        var chalk = new OfferCtx(Role.Healer, AbilityId.MendPulse, none, WeaponId.SpiritChalk);
        var other = new OfferCtx(Role.Healer, AbilityId.MendPulse, none, WeaponId.Revolver);
        var bare = new OfferCtx(Role.Healer, AbilityId.MendPulse, none);
        int mod = U("Ricochet"), leg = U("Searchlight"), mile = U("Scholar");
        foreach (int id in new[] { mod, leg, mile })
            Upgrades.All[id].Weapons = new[] { WeaponId.SpiritChalk };  // (test only: put back below)
        try
        {
            Func<OfferCtx, int[]> legs = c => { var all = new List<int>(); var rng = new Random(2); for (int i = 0; i < 40; i++) all.AddRange(Upgrades.LegendaryOffer(rng, EnemyType.Warden, c)); return all.ToArray(); };
            Func<OfferCtx, int[]> miles = c => { var all = new List<int>(); var rng = new Random(2); for (int i = 0; i < 40; i++) all.AddRange(Upgrades.MilestoneOffer(rng, c)); return all.ToArray(); };
            Check(Upgrades.ModOffer(chalk).Contains(mod) && !Upgrades.ModOffer(other).Contains(mod) && !Upgrades.ModOffer(bare).Contains(mod),
                "a weapon-only weapon mod is offered to that weapon only");
            Check(legs(chalk).Contains(leg) && !legs(other).Contains(leg), "a weapon-only legendary is offered to that weapon only");
            Check(miles(chalk).Contains(mile) && !miles(other).Contains(mile), "a weapon-only milestone card is offered to that weapon only");
        }
        finally
        {
            foreach (int id in new[] { mod, leg, mile })
                Upgrades.All[id].Weapons = null;
        }
        var s = StartBuffed(dt, 6, 0);
        var p = s[0].Game.Players[2];
        var c2 = s[0].Game.OfferFor(p);
        Check(c2.Role == p.Role && c2.HasKit(p.Kit) && c2.FirstAbility == p.FirstAbility && c2.Taken == p.Taken && c2.Weapon == p.Weapon.Id
            && c2.Expanding == s[0].Game.ExpandingOn, "Game.OfferFor carries the player's role, class, 1st ability, picks, weapon and arena");
        s[0].Leave(""); s[1].Leave("");
    }

    // Class and weapon-list cards (Docs/UpgradePools.md step 3): a Kit card goes to that class only, for every role
    // and 1st ability; a Wildcard counts as the class of its 1st ability, weapon and Wildcard ability; a Weapons card
    // goes to any weapon on its list and no other.
    static void CardFilterTest()
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var none = new List<int>();
        int card = U("Barrage");
        var u0 = Upgrades.All[card];
        try
        {
            u0.Kit = Kit.Knight;  // (test only: put back below)
            bool kitOk = true;
            foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
                for (int k = 0; k < Defs.KitCount; k++)
                    for (int c = 0; c < Upgrades.FirstAbilityChoices; c++)
                    {
                        var ab = Upgrades.FirstAbility(role, (Kit)k, c);
                        kitOk &= Upgrades.Available(card, new OfferCtx(role, ab, none)) == ((Kit)k == Kit.Knight);
                        var pl = new Player { Role = role, Kit = (Kit)k, FirstAbility = ab, Weapon = Defs.Weapon(role, (Kit)k, 0) };
                        kitOk &= Upgrades.Available(card, OfferCtx.Of(pl, false)) == ((Kit)k == Kit.Knight);
                    }
            Check(kitOk, "a Knight card is offered to every Knight (each role and 1st ability) and to no other class");

            int seenKnight = 0, seenSoldier = 0;
            var rng = new Random(5);
            for (int i = 0; i < 1500; i++)
            {
                seenKnight += Upgrades.RollOffer(rng, new OfferCtx(Role.Tank, Upgrades.FirstAbility(Role.Tank, Kit.Knight, 0), none), 20).Count(x => x == card);
                seenSoldier += Upgrades.RollOffer(rng, new OfferCtx(Role.Tank, Upgrades.FirstAbility(Role.Tank, Kit.Soldier, 0), none), 20).Count(x => x == card);
            }
            Check(seenKnight > 0 && seenSoldier == 0, "level-up offers show a Knight card to a Knight (" + seenKnight + "x) and never to a Soldier");

            // A Wildcard with a Ninja 1st ability, a Knight weapon and a Hemomancer Wildcard ability.
            var wild = new Player
            {
                Role = Role.Wildcard, Kit = Kit.Soldier,
                FirstAbility = Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, (int)Kit.Ninja * Upgrades.FirstAbilityChoices),
                Weapon = Defs.Weapon(Role.Wildcard, Kit.Soldier, Defs.WildWeaponPick(Kit.Knight, 0)),
                WildcardAbility = Upgrades.FirstAbility(Role.Wildcard, Kit.Soldier, (int)Kit.Hemomancer * Upgrades.FirstAbilityChoices),
            };
            var wc = OfferCtx.Of(wild, false);
            Check(wc.HasKit(Kit.Ninja) && wc.HasKit(Kit.Knight) && wc.HasKit(Kit.Hemomancer) && !wc.HasKit(Kit.Soldier) && !wc.HasKit(Kit.Elementalist)
                && Upgrades.Available(card, wc), "a Wildcard counts as the classes of its 1st ability, weapon and Wildcard ability, and no others");
            u0.Kit = null;

            u0.Weapons = new[] { WeaponId.Scattergun, WeaponId.Railgun };
            Check(Upgrades.Available(card, new OfferCtx(Role.Dps, AbilityId.PoisonDart, none, WeaponId.Scattergun))
                && Upgrades.Available(card, new OfferCtx(Role.Dps, AbilityId.PoisonDart, none, WeaponId.Railgun))
                && !Upgrades.Available(card, new OfferCtx(Role.Dps, AbilityId.PoisonDart, none, WeaponId.Revolver))
                && !Upgrades.Available(card, new OfferCtx(Role.Dps, AbilityId.PoisonDart, none)),
                "a weapon-list card is offered to every weapon on its list and to no other (nor to no weapon)");
            u0.Kit = Kit.Soldier;
            Check(Upgrades.FilterNote(u0) == "Soldier · 2 weapons" && Upgrades.FilterNote(Upgrades.All[U("Warding Ink")]) == "Spirit Chalk"
                && Upgrades.FilterNote(Upgrades.All[U("Hollow Points")]) == "", "the dev tools' filter note names the class and weapons");
        }
        finally
        {
            u0.Kit = null;
            u0.Weapons = null;
        }
    }

    // Each class keeps its own cards beside its abilities (Docs/UpgradePools.md step 4): every card in a class's list
    // belongs to that class (by its ability, weapons or Kit), all of them are in Upgrades.All, and card names are unique
    // (saves find cards by name).
    static void ClassCardListsTest()
    {
        var lists = new[] { Game.SoldierCards, Game.ElementalistCards, Game.SummonerCards, Game.KnightCards, Game.NinjaCards, Game.HemomancerCards, Game.EngineerCards, new UpgradeDef[0] /* the Arcanist has no cards of its own yet */ };
        bool own = lists.Length == Defs.KitCount, inAll = true;
        var bad = new List<string>();
        for (int k = 0; k < lists.Length; k++)
            foreach (var u in lists[k])
            {
                inAll &= Array.IndexOf(Upgrades.All, u) >= 0;
                bool mine = (u.RequiresAbility == AbilityId.None || Upgrades.Ability(u.RequiresAbility).Kit == (Kit)k)
                    && (u.Weapons == null || u.Weapons.All(w => Defs.WeaponOf(w).Kit == (Kit)k))
                    && (!u.Kit.HasValue || u.Kit.Value == (Kit)k)
                    && (u.RequiresAbility != AbilityId.None || u.Weapons != null || u.Kit.HasValue);
                if (!mine)
                    bad.Add((Kit)k + ": " + u.Name);
            }
        Check(own && inAll && bad.Count == 0, "each class's card list holds only its own cards, and all are offered" + (bad.Count > 0 ? " (wrong: " + string.Join(", ", bad) + ")" : ""));
        var general = Upgrades.All.Where(u => lists.All(l => Array.IndexOf(l, u) < 0)).ToList();
        Check(general.All(u => u.RequiresAbility == AbilityId.None && u.Weapons == null && !u.Kit.HasValue),
            "no class card is left in the general list");
        Check(Upgrades.All.Select(u => u.Name).Distinct().Count() == Upgrades.All.Length, "every card name is unique (" + Upgrades.All.Length + " cards)");
    }

    // Card ids cross the wire as 2 bytes (Docs/UpgradePools.md step 1): an offer with ids past 255 and an empty
    // slot (-1) reaches the client as sent, and so does the taken list.
    static void CardIdWireTest(float dt)
    {
        Check(Upgrades.All.Length < short.MaxValue, "every card id fits in 2 bytes (" + Upgrades.All.Length + " cards)");
        var s = StartBuffed(dt, 6, 0);
        var host = s[0].Game;
        var sent = new[] { 300, -1, 4000 };  // (ids past the 1-byte cap; the client only stores them)
        s[1].Game.Bot = false;  // (its bot would pick from the offer, and these cards don't exist)
        host.Players[2].Offer = sent;
        bool offer = false;
        Run(s, dt, 1f, () => offer = s[1].Game.Players[2].Offer != null && s[1].Game.Players[2].Offer.SequenceEqual(sent));
        Check(offer, "an offer with card ids past 255 and an empty slot reaches the client as sent");
        host.Players[2].Offer = null;
        int last = Upgrades.All.Length - 1;
        host.GrantUpgrade(2, last);
        bool taken = false;
        Run(s, dt, 1f, () => taken = s[1].Game.Players[2].Taken.SequenceEqual(host.Players[2].Taken));
        Check(taken && s[1].Game.Players[2].Taken.Contains(last), "the taken list reaches the client, the last card id included");
        s[0].Leave(""); s[1].Leave("");
    }

    // Every weapon mod at full stacks: homing shots curve and stay in sync, ricochets
    // happen, and burning / chilled enemies show up on the client.
    static void WeaponModTest(float dt)
    {
        var s = StartBuffed(dt, 6, 0);
        var offer = Upgrades.ModOffer(new OfferCtx(Role.Dps, Upgrades.FirstAbility(Role.Dps, Kit.Soldier, 0), new List<int>()));
        int mods = Upgrades.All.Count(u => u.Rarity == Rarity.Mod);
        Check(offer != null && offer.Length == mods, "the weapon mod offer lists all " + mods + " mods");
        foreach (int id in offer)
            for (int k = 0; k < 3; k++)
            {
                s[0].Game.GrantUpgrade(1, id);
                s[0].Game.GrantUpgrade(2, id);
            }
        Check(Upgrades.ModOffer(new OfferCtx(Role.Dps, AbilityId.PoisonDart, s[0].Game.Players[2].Taken)) == null, "maxed-out mods are no longer offered");
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
                for (int k = 0; k < 5; k++) { Nap(2); s[1].Poll(); }
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

    // Class ability cards: very rare cards for your class's two other 1st abilities, which go on 2nd, 3rd or 4th.
    static void ClassAbilityTest(float dt)
    {
        Func<string, int> U = name => Array.FindIndex(Upgrades.All, u => u.Name == name);
        var cards = Enumerable.Range(0, Upgrades.All.Length).Where(i => Upgrades.All[i].ClassAbility).ToList();
        int right = 0, combos = 0, gaps = 0;
        foreach (Role role in new[] { Role.Tank, Role.Healer, Role.Dps })
            for (int k = 0; k < Defs.KitCount; k++)
                for (int c = 0; c < Upgrades.AbilityLimit(role, (Kit)k); c++)
                {
                    if (Upgrades.AbilityGap(role, (Kit)k, c))
                    {
                        gaps++;
                        continue;  // a class still being built: a stand-in, locked in the lobby
                    }
                    var ab = Upgrades.FirstAbility(role, (Kit)k, c);
                    var offered = cards.Where(i => Upgrades.Available(i, new OfferCtx(role, ab, new List<int>()))).Select(i => Upgrades.All[i].Grants).OrderBy(x => x).ToList();
                    var expect = Enumerable.Range(0, Upgrades.AbilityLimit(role, (Kit)k)).Where(o => o != c && !Upgrades.AbilityGap(role, (Kit)k, o)).Select(o => Upgrades.FirstAbility(role, (Kit)k, o)).OrderBy(x => x).ToList();
                    combos++;
                    if (offered.SequenceEqual(expect))
                        right++;
                }
        Console.WriteLine("  class ability cards: {0} cards; the right two offered for {1}/{2} role/class/ability picks", cards.Count, right, combos);
        int fourths = new[] { Role.Tank, Role.Healer, Role.Dps }.Sum(r => Enumerable.Range(0, Defs.KitCount).Count(k => Upgrades.AbilityLimit(r, (Kit)k) > Upgrades.FirstAbilityChoices));  // (Flood, Gale Wall)
        Check(cards.Count == 3 * Defs.KitCount * Upgrades.FirstAbilityChoices + fourths - gaps && right == combos, "each class is offered exactly its other 1st abilities (two, or three with a 4th) as very rare cards");

        var rng = new Random(7);
        int veryRare = 0, cls = 0;
        Role triRole = Role.Healer; Kit triKit = Kit.Soldier;  // a class with exactly three 1st abilities and no gaps: two cards (a 4th, like the Decoy Dummy, makes three)
        foreach (var r in new[] { Role.Healer, Role.Tank, Role.Dps })
            for (int k = Defs.KitCount - 1; k >= 0; k--)
                if (Upgrades.AbilityLimit(r, (Kit)k) == Upgrades.FirstAbilityChoices && !Enumerable.Range(0, Upgrades.FirstAbilityChoices).Any(o => Upgrades.AbilityGap(r, (Kit)k, o)))
                { triRole = r; triKit = (Kit)k; }
        var triAbility = Upgrades.FirstAbility(triRole, triKit, 0);
        Console.WriteLine("  odds measured on " + triRole + " " + triKit + " (" + triAbility + ")");
        for (int n = 0; n < 4000; n++)
            foreach (int id in Upgrades.RollOffer(rng, new OfferCtx(triRole, triAbility, new List<int>()), 20))
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
        Check(!Upgrades.Available(U("Barrier"), new OfferCtx(Role.Tank, AbilityId.Roar, taken)) && Upgrades.Available(U("Bastion"), new OfferCtx(Role.Tank, AbilityId.Roar, taken))
            && !Upgrades.Available(U("Bastion"), new OfferCtx(Role.Tank, AbilityId.Roar, new List<int>())),
            "a class ability card isn't offered twice, and that ability's own upgrades open up");

        // In a run: the DPS Soldier (1st ability: Poison Dart) gets Airstrike as the 2nd, and the bot calls it in.
        var s = StartBuffed(dt, 1, 0);
        Run(s, dt, 1);
        s[0].Game.GrantUpgrade(2, U("Airstrike"));
        bool thrown = false;
        Run(s, dt, 9, null, () => thrown |= s[0].Game.Effects.Any(e => e.Kind == FxKind.Airstrike && (int)(e.Radius / 10000f) == 2));
        var hp = s[0].Game.Players[2];
        var cp = s[1].Game.Players[2];
        Console.WriteLine("  DPS Soldier: 1st {0}, 2nd {1} (client sees {2}); airstrike called {3}", hp.Slots[0].Id, hp.Slots[1].Id, cp.Slots[1].Id, thrown);
        Check(hp.Slots[0].Id == AbilityId.PoisonDart && hp.Slots[1].Id == AbilityId.Airstrike && cp.Slots[1].Id == AbilityId.Airstrike && thrown,
            "a class ability card puts that ability on 2nd, 3rd or 4th, and it works there");
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
        float xpHit = Xp(), hpCast = dummy.Hp;
        V2 dummyAt = dummy.Pos;
        Run(s, dt, 0.5f, null, Hold);  // the wave runs out (Shockwave.cs)
        SettleKnocks(host);
        float dmg1 = before - dummy.Hp, push1 = dummy.Pos.DistanceTo(dummyAt);
        Console.WriteLine("  shockwave: {0:0} damage on the cast frame, {1:0.0} once the wave reached it, shoved {2:0} px", before - hpCast, dmg1, push1);
        Check(before - hpCast < 0.01f && dmg1 > 50f && push1 > 100f, "the shockwave's wave travels: it hits and shoves the enemy when it gets there, not on the cast");
        dummy.Pos = new V2(680f, 450f);
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
        dummy.Pos = new V2(680f, 450f);
        var far = Dummy(new V2(870f, 450f));  // 270 px out: past the 230 px wave, inside the mastered 300
        before = dummy.Hp;
        float farHp = far.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        float cd3 = host.Cooldown(hp, slot);
        Run(s, dt, 0.5f, null, Hold);
        float dmg3 = before - dummy.Hp, farHit3 = farHp - far.Hp;
        var cp = s[1].Game.Players[1];
        Console.WriteLine("  shockwave level {0}: damage {1:0.0} -> {2:0.0}, cooldown {3:0.00}s -> {4:0.00}s; client sees level {5}",
            Game.MasteryLevel(hp, AbilityId.Shockwave), dmg1, dmg3, cd1, cd3, Game.MasteryLevel(cp, AbilityId.Shockwave));
        Check(Game.MasteryLevel(hp, AbilityId.Shockwave) == 3 && Math.Abs(dmg3 / dmg1 - 1.16f) < 0.02f && Math.Abs(cd3 / cd1 - 0.92f) < 0.01f,
            "each mastery level adds 8% potency and takes 4% off the cooldown");


        // Capstones: Shockwave knocks back, Nova fires 48, Mark hits three.
        host.GainMastery(hp, AbilityId.Shockwave, 1e4f);
        Run(s, dt, 0.3f, null, Hold);
        far.Pos = new V2(870f, 450f);
        farHp = far.Hp;
        cast.Invoke(host, new object[] { 1, slot, dummy.Pos, true });
        Run(s, dt, 0.5f, null, Hold);
        float farHit5 = farHp - far.Hp;
        Console.WriteLine("  shockwave reach: an enemy 270 px out takes {0:0} before mastery, {1:0} mastered", farHit3, farHit5);
        Check(farHit3 < 0.01f && farHit5 > 50f, "the mastered Shockwave runs out 300 px");
        host.GrantUpgrade(1, U("Starburst"));
        Run(s, dt, 0.2f, null, Hold);
        int nova = Array.FindIndex(hp.Slots, sl => sl.Id == AbilityId.Starburst);
        Func<int> NovaShots = () => host.Bullets.Values.Count(b => b.Kind == Defs.NovaShot);
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaBase = NovaShots();
        foreach (var b in host.Bullets.Values.Where(b => b.Kind == Defs.NovaShot).ToList()) host.Bullets.Remove(b.Id);
        host.GainMastery(hp, AbilityId.Starburst, 1e4f);
        cast.Invoke(host, new object[] { 1, nova, dummy.Pos, true });
        int novaMax = NovaShots();
        var dp = host.Players[2];
        host.DevMasterAll(2);
        Run(s, dt, 0.3f, null, Hold);
        Console.WriteLine("  capstones: nova {0} -> {1} shots, {2} gold",
            novaBase, novaMax, string.Join("/", dp.Slots.Where(sl => sl.Id != AbilityId.None).Select(sl => Game.MasteryLevel(dp, sl.Id))));
        Check(novaBase == 24 && novaMax == 48, "level 5 unlocks each ability's capstone");
        Check(Game.MasteryLevel(cp, AbilityId.Shockwave) == 5 && host.MasteryToasts.Any(t => t.Ability == AbilityId.Shockwave && t.Level == 3)
            && s[1].Game.MasteryToasts.Any(t => t.Ability == AbilityId.PoisonDart && t.Level == 5),
            "the player's own game knows the level (and shows a level-up popup)");
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

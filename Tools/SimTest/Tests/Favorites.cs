using System;
using System.Linq;
using Arena;

// Saved favourite loadouts (Core/Favorites.cs, the lobby's Saved loadouts drawer): saving, the slot cap, names,
// the profile's "fav" lines, and a guest loading one over the network.
static partial class Program
{
    static void FavoritesTest(float dt)
    {
        var p = new Profile();
        var knight = new LobbyEntry { Role = Role.Tank, Kit = Kit.Knight, Weapon = 1, Ability = 2, Ult = 0 };
        Check(Favorites.Save(p, "  Lance|Wall  ", knight) && p.Favorites[0].Name == "LanceWall" && p.Favorites[0].Matches(knight), "a loadout saves under a clean name (no '|', trimmed) and matches its seat");
        Check(Favorites.Save(p, "", knight) && p.Favorites[1].Name == "Knight Tank", "an empty name becomes the class and role");
        Check(Favorites.Save(p, new string('x', 40), knight) && p.Favorites[2].Name.Length == Favorites.NameMax, "a long name is cut to " + Favorites.NameMax + " letters");
        var wild = new LobbyEntry { Role = Role.Wildcard, Kit = Kit.Soldier, Weapon = 9, Ability = 12, Wild = Defs.MakeWild(14, 2), Ult = (byte)Upgrades.WildUlt(Kit.Ninja, 0) };
        p.PerkNames.Add("RiotGear");
        Favorites.Save(p, "Wild one", wild);
        while (Favorites.Save(p, "fill", knight)) { }
        Check(p.Favorites.Count == Favorites.Slots, "there are " + Favorites.Slots + " slots and no more");
        Check(!p.Favorites[3].Matches(new LobbyEntry { Role = Role.Wildcard, Kit = Kit.Soldier, Weapon = 9, Ability = 12, Wild = Defs.MakeWild(13, 2), Ult = wild.Ult }), "a Wildcard's second ability counts towards a match");

        var back = Profile.Parse(p.ToText());
        Check(back.Valid && back.Favorites.Count == Favorites.Slots && back.Favorites.Select(Favorites.ToLine).SequenceEqual(p.Favorites.Select(Favorites.ToLine))
            && back.Favorites[3].Perks.SequenceEqual(new[] { "RiotGear" }) && back.Favorites[3].Name == "Wild one", "the fav lines read back the same (names with spaces, perks, a Wildcard)");
        Check(Profile.Parse("arena-profile 7\nfav 9 0 0 0 0 0 -|Bad\nfav 0 3 1 2 0 0 -|Ok\nend 1\n").Favorites.Count == 1, "a damaged fav line is skipped; an older file loads");
        Check(Favorites.Rename(p, 1, " Tank|Day ") && p.Favorites[1].Name == "TankDay" && Favorites.Rename(p, 1, "") && p.Favorites[1].Name == "Knight Tank"
            && Favorites.Rename(p, 3, new string('y', 30)) && p.Favorites[3].Name.Length == Favorites.NameMax && !Favorites.Rename(p, 99, "x"), "a favourite renames with the same cleaning (empty: class and role; cut to the limit; no such slot: false)");
        Check(Profile.Parse(p.ToText()).Favorites[3].Name == p.Favorites[3].Name && p.Favorites[3].Matches(wild), "a new name saves and keeps the picks");
        Favorites.Rename(p, 1, "Knight Tank");
        Check(Favorites.Delete(p, 0) && p.Favorites.Count == Favorites.Slots - 1 && p.Favorites[0].Name == "Knight Tank" && !Favorites.Delete(p, 99), "a favourite deletes; the rest move up");

        // A guest loads a saved Wildcard, then a Knight: the host ends up with exactly those picks.
        int port = nextPort++;
        var host = new Session { AutoStart = 3 };
        host.Host(port, "Host", Role.Tank, 0, 0, Kit.Soldier);
        var client = new Session();
        client.Join("127.0.0.1", port, "Friend", Role.Healer, 0, 0, Kit.Summoner);
        var s = new[] { host, client };
        Run(s, dt, 3, () => host.Lobby.Count == 2 && client.Lobby.Count == 2);
        int fid = host.Lobby.Keys.First(k => k != 1);
        var cp = new Profile();
        Favorites.Save(cp, "Wild", wild);
        Favorites.Save(cp, "Elem rod", new LobbyEntry { Role = Role.Tank, Kit = Kit.Elementalist, Weapon = 2, Ability = 1, Ult = 1 });
        Favorites.Load(client, cp, cp.Favorites[0]);
        Run(s, dt, 2, () => cp.Favorites[0].Matches(host.Lobby[fid]) && cp.Favorites[0].Matches(client.Lobby[fid]));
        Check(cp.Favorites[0].Matches(host.Lobby[fid]) && cp.Favorites[0].Matches(client.Lobby[fid]), "loading a saved Wildcard sets every pick on the host and back on the guest");
        Favorites.Load(client, cp, cp.Favorites[1]);
        Run(s, dt, 2, () => cp.Favorites[1].Matches(host.Lobby[fid]));
        var e = host.Lobby[fid];
        Check(cp.Favorites[1].Matches(e) && e.Kit == Kit.Elementalist && e.Weapon == 2 && e.Ability == 1 && e.UltId == AbilityId.LightningRod, "then a tank Elementalist with Lightning Rod: the role, class and picks land in order");
        Favorites.Load(host, null, cp.Favorites[1]);
        Run(s, dt, 1, () => cp.Favorites[1].Matches(host.Lobby[1]));
        Check(cp.Favorites[1].Matches(host.Lobby[1]), "the host loads one too");

        // Perks come back if they're still unlocked; a locked one is left off.
        var pp = new Profile();
        pp.BossBeaten(EnemyType.Boss, 5); pp.HuntEnded(EnemyType.Boss, true, 300f);
        var withPerk = new FavLoadout { Name = "x", Role = Role.Tank, Kit = Kit.Knight }; withPerk.Perks.Add("RiotGear"); withPerk.Perks.Add("Contraband");
        Check(Favorites.Load(client, pp, withPerk) && pp.PerkNames.SequenceEqual(new[] { "RiotGear" }), "loading equips its perks that are unlocked (Contraband isn't yet)");
        Check(!Favorites.Load(client, pp, withPerk), "loading the same perks again changes nothing");
        client.Leave(""); host.Leave("");
    }
}

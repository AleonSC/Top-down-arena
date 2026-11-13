using System;
using System.Collections.Generic;
using System.Linq;

namespace Arena
{
    /// One saved loadout (the lobby's Saved loadouts drawer, Game/FavoritesUi.cs): a name you typed, the role, class,
    /// weapon, ability, a Wildcard's second ability, the Ultimate row's pick and the perks equipped when it was saved.
    public class FavLoadout
    {
        public string Name = "";
        public Role Role;
        public Kit Kit;
        public byte Weapon, Ability, Wild, Ult;
        public readonly List<string> Perks = new List<string>();

        /// The same picks as this seat (the drawer marks it EQUIPPED). Perks aren't compared: they live on the profile.
        public bool Matches(LobbyEntry e) => e != null && e.Role == Role && e.Kit == Kit && e.Weapon == Weapon && e.Ability == Ability
            && e.Ult == Ult && (Role != Role.Wildcard || e.Wild == Wild);
    }

    /// Saved favourite loadouts, kept on the profile ("fav" lines, Profile format 8).
    public static class Favorites
    {
        public const int Slots = 6;
        public const int NameMax = 20;

        /// Saves this seat's loadout (and the profile's equipped perks) under a name. False: every slot is full.
        public static bool Save(Profile p, string name, LobbyEntry e)
        {
            if (p == null || e == null || p.Favorites.Count >= Slots)
                return false;
            var f = new FavLoadout { Name = CleanName(name, e), Role = e.Role, Kit = e.Kit, Weapon = e.Weapon, Ability = e.Ability, Wild = e.Wild, Ult = e.Ult };
            f.Perks.AddRange(p.PerkNames);
            p.Favorites.Add(f);
            return true;
        }

        public static bool Delete(Profile p, int index)
        {
            if (p == null || index < 0 || index >= p.Favorites.Count)
                return false;
            p.Favorites.RemoveAt(index);
            return true;
        }

        /// The default name for a new favourite: "Knight Tank" (a Wildcard: "Wildcard").
        public static string DefaultName(LobbyEntry e) => e.Role == Role.Wildcard ? Defs.Get(e.Role).Name : Defs.KitName(e.Kit) + " " + Defs.Get(e.Role).Name;

        /// A name kept to one line of NameMax letters, with no '|' (the file's separator); empty: the default.
        public static string CleanName(string name, LobbyEntry e)
        {
            var s = new string((name ?? "").Where(ch => ch != '|' && !char.IsControl(ch)).ToArray()).Trim();
            if (s.Length > NameMax)
                s = s.Substring(0, NameMax).TrimEnd();
            return s.Length > 0 ? s : DefaultName(e);
        }

        /// Asks for the favourite's picks (the host applies them in order: role, class, then the rest), and equips its
        /// perks if they're still unlocked. Returns true when the perks changed (the caller saves and shares them).
        public static bool Load(Session s, Profile p, FavLoadout f)
        {
            s.RequestRole(f.Role);
            if (f.Role == Role.Wildcard)
                s.RequestWildcard(f.Weapon, f.Ability, Defs.WildSecond(f.Wild), Defs.WildKey(f.Wild));
            else
            {
                s.RequestKit(f.Kit);
                s.RequestWeapon(f.Weapon);
                s.RequestAbility(f.Ability);
            }
            s.RequestUlt(f.Ult);
            if (p == null)
                return false;
            var before = string.Join(",", p.PerkNames);
            p.PerkNames.Clear();
            foreach (var n in f.Perks)
            {
                var d = Arena.Perks.Get(n);
                if (d != null)
                    Arena.Perks.Equip(p, d.Id);  // (skips one that's locked now, or past the slots)
            }
            return string.Join(",", p.PerkNames) != before;
        }

        /// "fav" line: role kit weapon ability wild ult perks|name (perks comma-separated, "-" for none).
        public static string ToLine(FavLoadout f) =>
            (int)f.Role + " " + (int)f.Kit + " " + f.Weapon + " " + f.Ability + " " + f.Wild + " " + f.Ult + " " + (f.Perks.Count > 0 ? string.Join(",", f.Perks) : "-") + "|" + f.Name;

        public static FavLoadout FromLine(string val)
        {
            int bar = val.IndexOf('|');
            if (bar < 0)
                throw new FormatException("no name");
            var q = val.Substring(0, bar).Trim().Split(' ');
            int role = RunSave.I(q[0]), kit = RunSave.I(q[1]);
            if (role < 0 || role >= Defs.Roles.Length || kit < 0 || kit >= Defs.KitCount)
                throw new FormatException("role or class out of range");
            var f = new FavLoadout
            {
                Role = (Role)role, Kit = (Kit)kit,
                Weapon = (byte)RunSave.I(q[2]), Ability = (byte)RunSave.I(q[3]), Wild = (byte)RunSave.I(q[4]), Ult = (byte)RunSave.I(q[5]),
            };
            if (q[6] != "-")
                foreach (var n in q[6].Split(','))
                    if (n.Length > 0) f.Perks.Add(n);
            f.Name = val.Substring(bar + 1).Trim();
            if (f.Name.Length == 0)
                f.Name = Defs.KitName(f.Kit);
            return f;
        }
    }
}

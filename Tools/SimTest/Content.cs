using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Arena;

// Content queries for tests: which role and class has how many weapons, 1st abilities and ultimates, read from the real
// definitions (Classes.cs's lists, Defs.Weapons, Upgrades.Abilities). A test that needs "a class without a key-1
// ultimate" or "a class with three 1st abilities" asks here instead of naming one: a class named as an example breaks
// the day it gets new content. Every pick is deterministic (the first match in Content.All's fixed order: Tank, Healer,
// DPS, each in Kit order) and fails the test with a clear message when nothing matches.
// Content.Fuzz (the contentfuzz suite, or CONTENTFUZZ=1 before any suite) adds fake content to every class and role to
// prove the tests don't depend on today's content.
static partial class Program
{
    /// A role and class, as the content queries hand them out.
    struct RoleKit
    {
        public Role Role;
        public Kit Kit;
        public RoleKit(Role role, Kit kit) { Role = role; Kit = kit; }
        public override string ToString() => "the " + (Role == Role.Dps ? "DPS" : Role.ToString()) + " " + Kit;
    }

    static class Content
    {
        /// The roles with their own lobby lists (the Wildcard picks from the DPS ones).
        public static readonly Role[] Roles = { Role.Tank, Role.Healer, Role.Dps };

        /// Every role and class, in the fixed order the picks search: Tank, Healer, DPS, each in Kit order.
        public static IEnumerable<RoleKit> All()
        {
            foreach (var r in Roles)
                for (int k = 0; k < Defs.KitCount; k++)
                    yield return new RoleKit(r, (Kit)k);
        }

        // --- Counts, read from the definitions ---
        public static int Classes => Defs.KitCount;
        public static int WeaponCount(Role r, Kit k) => Defs.WeaponCount(r, k);
        public static int AbilityCount(Role r, Kit k) => Upgrades.AbilityLimit(r, k);
        /// The ultimates on a role and class's Ultimate row: its default one and every key 1-3 with one.
        public static int UltCount(Role r, Kit k) => 1 + Enumerable.Range(1, Upgrades.UltKeys - 1).Count(key => HasUlt(r, k, key));
        public static bool HasUlt(Role r, Kit k, int key) => key == 0 || Upgrades.AltUltimate(r, k, key) != AbilityId.None;
        /// The lowest key 1-3 with no ultimate on it, or -1 when the row is full.
        public static int FreeUltKey(Role r, Kit k)
        {
            for (int key = 1; key < Upgrades.UltKeys; key++)
                if (!HasUlt(r, k, key))
                    return key;
            return -1;
        }
        /// The highest key with an ultimate on it (0: only the default one).
        public static int LastUltKey(Role r, Kit k)
        {
            int last = 0;
            for (int key = 1; key < Upgrades.UltKeys; key++)
                if (HasUlt(r, k, key))
                    last = key;
            return last;
        }
        /// No lobby place filled by a stand-in (a class still being built, FillsGaps).
        public static bool Complete(Role r, Kit k) =>
            !Upgrades.UltGap(r, k) && !Enumerable.Range(0, Defs.WeaponsPerRole).Any(i => Defs.WeaponGap(r, k, i))
            && !Enumerable.Range(0, Upgrades.FirstAbilityChoices).Any(i => Upgrades.AbilityGap(r, k, i));

        /// Every role and class's own weapons (no Wildcard picks), 4th keys included: the count the lobby shows over all of them.
        public static int TotalWeaponKeys => All().Sum(rk => WeaponCount(rk.Role, rk.Kit));
        public static int TotalAbilityChoices => All().Sum(rk => AbilityCount(rk.Role, rk.Kit));
        public static int TotalUlts => All().Sum(rk => UltCount(rk.Role, rk.Kit));
        /// How many role-and-class lists have a 4th weapon key, a 4th 1st ability, or an ultimate on `key`.
        public static int FourthWeapons => All().Count(rk => WeaponCount(rk.Role, rk.Kit) > Defs.WeaponsPerRole);
        public static int FourthAbilities => All().Count(rk => AbilityCount(rk.Role, rk.Kit) > Upgrades.FirstAbilityChoices);
        public static int UltsOnKey(int key) => All().Count(rk => HasUlt(rk.Role, rk.Kit, key));
        /// The classes with a 4th DPS weapon key (the Wildcard's picks after Defs.WildcardChoices).
        public static int DpsFourthWeapons => Enumerable.Range(0, Defs.KitCount).Count(k => WeaponCount(Role.Dps, (Kit)k) > Defs.WeaponsPerRole);

        // --- Picks: the first role and class that matches, else a failed check naming what was needed ---

        /// The first role and class (Content.All order, only `role`'s when given) that matches; a failed Check naming what
        /// was needed when none does. `complete`: only classes with no stand-in places (most tests want a finished class).
        public static RoleKit Pick(string what, Func<Role, Kit, bool> match, Role? role = null, bool complete = true)
        {
            var rk = Find(match, role, complete);
            if (rk != null)
                return rk.Value;
            Check(false, "Content: the test needs " + (role == null ? "a role and class " : "a " + role + " class ") + what
                + ", and none has that now (ask Tools/SimTest/Content.cs for another subject, or let the test skip that part)");
            return new RoleKit(role ?? Role.Tank, Kit.Soldier);
        }

        /// Like Pick, but null instead of a failure when none matches (for a test that can skip that part).
        public static RoleKit? Find(Func<Role, Kit, bool> match, Role? role = null, bool complete = true)
        {
            foreach (var rk in All())
                if ((role == null || rk.Role == role) && (!complete || Complete(rk.Role, rk.Kit)) && match(rk.Role, rk.Kit))
                    return rk;
            return null;
        }

        public static RoleKit RoleWithWeapons(int n, Role? role = null, bool complete = true) =>
            Pick("with exactly " + n + " weapons", (r, k) => WeaponCount(r, k) == n, role, complete);
        public static RoleKit RoleWithMoreWeaponsThan(int n, Role? role = null, bool complete = true) =>
            Pick("with more than " + n + " weapons", (r, k) => WeaponCount(r, k) > n, role, complete);
        public static RoleKit RoleWithAbilityChoices(int n, Role? role = null, bool complete = true) =>
            Pick("with exactly " + n + " 1st ability choices", (r, k) => AbilityCount(r, k) == n, role, complete);
        public static RoleKit RoleWithMoreAbilitiesThan(int n, Role? role = null, bool complete = true) =>
            Pick("with more than " + n + " 1st ability choices", (r, k) => AbilityCount(r, k) > n, role, complete);
        public static RoleKit RoleWithUlt(int key, Role? role = null, bool complete = true) =>
            Pick("with an ultimate on key " + key, (r, k) => HasUlt(r, k, key), role, complete);
        public static RoleKit RoleWithoutUlt(int key, Role? role = null, bool complete = true) =>
            Pick("with no ultimate on key " + key, (r, k) => !HasUlt(r, k, key), role, complete);
        /// A role and class with an empty key on its Ultimate row, and that key (the lowest 1-3 any class leaves free);
        /// null (key 0) when every class fills every key, so a test can skip its clamp check (NoFreeUltKey).
        public static RoleKit? FindFreeUltKey(out int key)
        {
            for (key = 1; key < Upgrades.UltKeys; key++)
            {
                int k = key;
                var rk = Find((r, kt) => !HasUlt(r, kt, k));
                if (rk != null)
                    return rk;
            }
            key = 0;
            return null;
        }
        /// The skip note (true) for a clamp check with no subject: no class leaves `key` (0: any key) empty.
        public static bool NoFreeUltKey(int key = 0)
        {
            Console.WriteLine("  (Content: every class has an ultimate on " + (key > 0 ? "key " + key : "keys 1-" + (Upgrades.UltKeys - 1)) + ", so that clamp has nothing to check)");
            return true;
        }

        // --- The fall-backs, checked on whichever role and class lacks the thing (true, with a note, when none does) ---

        /// A role and class with no ultimate on `key` has that pick clamped to 0 and gets its default ultimate.
        public static bool UltFallsBack(int key, Role? role = null)
        {
            var rk = Find((r, k) => !HasUlt(r, k, key), role);
            if (rk == null)
                return Skip("no " + (role?.ToString() ?? "role and") + " class lacks an ultimate on key " + key);
            var x = rk.Value;
            return Upgrades.ClampUlt(x.Role, x.Kit, key) == 0 && Upgrades.Ultimate(x.Role, x.Kit, key) == Upgrades.Ultimate(x.Role, x.Kit);
        }

        /// A Wildcard picking key `key` of a class whose DPS row has nothing there falls back to that class's key 0.
        public static bool WildUltFallsBack(int key)
        {
            var rk = Find((r, k) => !HasUlt(r, k, key), Role.Dps, complete: false);
            if (rk == null)
                return Skip("no DPS class lacks an ultimate on key " + key);
            var kit = rk.Value.Kit;
            return Upgrades.ClampUlt(Role.Wildcard, Kit.Soldier, Upgrades.WildUlt(kit, key)) == Upgrades.WildUlt(kit, 0)
                && Upgrades.Ultimate(Role.Wildcard, kit, Upgrades.WildUlt(kit, key)) == Upgrades.Ultimate(Role.Dps, kit);
        }

        /// A role and class with three 1st abilities: a 4th pick falls back to its first.
        public static bool ThreeAbilitiesFallBack()
        {
            var rk = Find((r, k) => AbilityCount(r, k) == Upgrades.FirstAbilityChoices);
            if (rk == null)
                return Skip("every class has a 4th 1st ability");
            var x = rk.Value;
            return Upgrades.FirstAbility(x.Role, x.Kit, Upgrades.FirstAbilityChoices) == Upgrades.FirstAbility(x.Role, x.Kit, 0);
        }

        static bool Skip(string why)
        {
            Console.WriteLine("  (Content: " + why + ", so that fall-back has nothing to check)");
            return true;
        }

        // --- The content fuzz: fake content for every class and role ---

        public static bool Fuzzed { get; private set; }
        public static readonly List<string> FuzzAdded = new List<string>();

        /// Adds fake content in every free lobby place the game's model has: a 4th weapon key and a 4th 1st ability
        /// wherever a role and class has none, and an ultimate on the lowest free key 1-3 of every Ultimate row. One place
        /// of each kind stays free (the last complete one in Content.All order, and per key for the ultimates), so a test that needs
        /// "a class without X" still has one, but a different one than without the fuzz: a test that names its subject
        /// breaks, a test that asks Content passes. The fakes clone a real definition under a new id past the enum's
        /// values (the lobby, counts and saves read them; casting a fake does nothing). Run before anything reads the tables
        /// (Main does it first): it replaces Defs.Weapons and Upgrades.Abilities.
        public static void Fuzz()
        {
            if (Fuzzed)
                return;
            Fuzzed = true;
            const BindingFlags S = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

            // Weapons: a 4th key (Choice 3) wherever the slot is free.
            var fourth = (WeaponDef[])typeof(Defs).GetField("fourth", S).GetValue(null);
            int nextWeapon = Enum.GetValues(typeof(WeaponId)).Length;
            var newWeapons = new List<WeaponDef>(Defs.Weapons);
            var freeW = All().Where(rk => fourth[(int)rk.Role * Defs.KitCount + (int)rk.Kit] == null).ToList();
            foreach (var rk in AllButOne(freeW))
            {
                var w = Clone(Defs.Weapon(rk.Role, rk.Kit, 0));
                w.Id = (WeaponId)nextWeapon++;
                w.Role = rk.Role;
                w.Kit = rk.Kit;
                w.Choice = Defs.WeaponsPerRole;
                w.Variant = false;
                w.FillsGaps = false;
                w.Name = "Fuzz " + w.Name;
                fourth[(int)rk.Role * Defs.KitCount + (int)rk.Kit] = w;
                newWeapons.Add(w);
                FuzzAdded.Add(rk + ": 4th weapon " + w.Name);
            }
            typeof(Defs).GetField("Weapons", S).SetValue(null, newWeapons.ToArray());
            var byId = new WeaponDef[nextWeapon];
            foreach (var w in newWeapons)
                byId[(int)w.Id] = w;
            typeof(Defs).GetField("byId", S).SetValue(null, byId);
            typeof(Defs).GetField("wildFourth", S).SetValue(null, null);  // (rebuilt from the new 4th keys)

            // 1st abilities: a 4th choice wherever there is none; ultimates: the lowest free key.
            var fourthA = (AbilityId[])typeof(Upgrades).GetField("FourthAbilities", S).GetValue(null);
            int nextAbility = Enum.GetValues(typeof(AbilityId)).Length - 1;  // (every id but None, which is 255)
            var abilities = new List<AbilityDef>(Upgrades.Abilities);
            int realAbilities = abilities.Count;
            var freeA = All().Where(rk => fourthA[(int)rk.Role * Defs.KitCount + (int)rk.Kit] == AbilityId.None).ToList();
            foreach (var rk in AllButOne(freeA))
            {
                var a = Clone(Upgrades.Ability(Upgrades.FirstAbility(rk.Role, rk.Kit, 0)));
                a.Id = (AbilityId)nextAbility++;
                a.Role = rk.Role;
                a.Kit = rk.Kit;
                a.Choice = AbilityDef.Fourth;
                a.FillsGaps = false;
                a.Name = "Fuzz " + a.Name;
                fourthA[(int)rk.Role * Defs.KitCount + (int)rk.Kit] = a.Id;
                abilities.Add(a);
                FuzzAdded.Add(rk + ": 4th ability " + a.Name);
            }
            for (int key = 1; key < Upgrades.UltKeys; key++)
            {
                var free = All().Where(rk => FreeUltKey(rk.Role, rk.Kit) == key).ToList();  // (the tables aren't replaced yet: each row gets one fake)
                foreach (var rk in AllButOne(free))
                {
                    var a = Clone(Upgrades.Ability(Upgrades.Ultimate(rk.Role, rk.Kit)));
                    a.Id = (AbilityId)nextAbility++;
                    a.Role = rk.Role;
                    a.Kit = rk.Kit;
                    a.Choice = AbilityDef.Ult;
                    a.UltKey = key;
                    a.FillsGaps = false;
                    a.Name = "Fuzz " + a.Name;
                    abilities.Add(a);
                    FuzzAdded.Add(rk + ": ultimate on key " + key + " " + a.Name);
                }
            }
            if (nextAbility >= (int)AbilityId.None)
                throw new InvalidOperationException("Content.Fuzz: out of free AbilityId numbers");
            typeof(Upgrades).GetField("Abilities", S).SetValue(null, abilities.ToArray());
            // A 4th ability's very rare class card, as Upgrades.ClassAbilityCards makes for a real one (at the end: no card id moves).
            var cards = new List<UpgradeDef>(Upgrades.All);
            foreach (var a in abilities.Skip(realAbilities).Where(x => x.Choice == AbilityDef.Fourth))
                cards.Add(new UpgradeDef { Rarity = Rarity.VeryRare, Grants = a.Id, ClassAbility = true, Weight = 3, Name = a.Name, Desc = a.Desc });
            typeof(Upgrades).GetField("All", S).SetValue(null, cards.ToArray());
        }

        /// The free places the fuzz fills: all but one, the last complete one (Content.Pick's default subjects) or else the last.
        static List<RoleKit> AllButOne(List<RoleKit> free)
        {
            int keep = free.FindLastIndex(rk => Complete(rk.Role, rk.Kit));
            if (keep < 0)
                keep = free.Count - 1;
            return free.Where((rk, i) => i != keep).ToList();
        }

        static T Clone<T>(T x) => (T)typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(x, null);
    }

    /// Suite contentfuzz: fake content for every class and role (Content.Fuzz, installed by Main before anything reads the
    /// tables), then the tests that read content counts or pick classes. A test failing here names a class or count that
    /// new content will change: make it ask Content instead.
    static void ContentFuzzCheck()
    {
        Check(Content.Fuzzed && Content.FuzzAdded.Count > 0, "contentfuzz: " + Content.FuzzAdded.Count + " fakes added (" + string.Join("; ", Content.FuzzAdded) + ")");
        var w = Defs.Weapons.LastOrDefault(x => x.Name.StartsWith("Fuzz "));
        Check(w != null && Defs.Weapon(w.Role, w.Kit, Defs.WeaponsPerRole) == w && Defs.WeaponOf(w.Id) == w && Defs.WeaponCount(w.Role, w.Kit) == Defs.WeaponsPerRole + 1,
            "contentfuzz: the lobby sees the fake weapons");
        var a = Upgrades.Abilities.LastOrDefault(x => x.Name.StartsWith("Fuzz ") && x.Choice == AbilityDef.Ult);
        Check(a != null && Upgrades.AltUltimate(a.Role, a.Kit, a.UltKey) == a.Id && Upgrades.Ability(a.Id) == a, "contentfuzz: the lobby sees the fake ultimates");
        var f = Upgrades.Abilities.FirstOrDefault(x => x.Name.StartsWith("Fuzz ") && x.Choice == AbilityDef.Fourth);
        Check(f != null && Upgrades.FirstAbility(f.Role, f.Kit, Upgrades.FirstAbilityChoices) == f.Id && Upgrades.AbilityLimit(f.Role, f.Kit) == Upgrades.FirstAbilityChoices + 1,
            "contentfuzz: the lobby sees the fake 4th abilities");
    }
}

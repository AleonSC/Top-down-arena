#!/usr/bin/env python3
"""Reads the game's numbers straight from the C# code into game-data.json, for the reference pages
(the Wave Almanac and the Build Planner) so they don't have to be copied by hand.

What it reads:
  Defs.cs      roles, class health/speed, weapons, enemies
  Upgrades.cs  upgrade cards (and what each does to the Stats sheet)
  *.cs         every AbilityDef (Space abilities, ultimates, card-only abilities)
  EnemyAI.cs   the boss cycle, the wave enemy pools (when each enemy joins), wave sizes
  Variety.cs   squads, elite traits
  Balance.cs   bosses' extra health
  Paths.cs / Modifiers.cs  path and modifier names and blurbs
Formulas that are code rather than data (wave size, health scaling, the bullet budget) are written into the
pages' JavaScript; check_formulas() below fails the build if the lines they mirror change.

Usage: python3 Docs/Data/extract.py   (writes Docs/Data/game-data.json)
"""
import json, os, re, sys

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
CORE = os.path.join(ROOT, "Assets", "Scripts", "Core")


def src(name):
    with open(os.path.join(CORE, name), encoding="utf-8") as f:
        return f.read()


STR = r'"((?:[^"\\]|\\.)*)"'


def unescape(s):
    return s.replace('\\"', '"').replace("\\n", "\n").replace("\\\\", "\\")


def blocks(text, opener):
    """The bodies of every `opener { ... }` (brace matched, strings skipped)."""
    out = []
    for m in re.finditer(re.escape(opener) + r"\s*\{", text):
        i, depth, start = m.end(), 1, m.end()
        while depth and i < len(text):
            c = text[i]
            if c == '"':
                i += 1
                while text[i] != '"':
                    i += 2 if text[i] == "\\" else 1
            elif c == "{":
                depth += 1
            elif c == "}":
                depth -= 1
            i += 1
        out.append(text[start:i - 1])
    return out


def field_str(body, name):
    """A string field, joining `"a" + "b"` concatenations."""
    m = re.search(r"\b" + name + r"\s*=\s*(" + STR + r"(?:\s*\+\s*" + STR + r")*)", body)
    if not m:
        return None
    return unescape("".join(re.findall(STR, m.group(1))))


def field_num(body, name, default=None):
    m = re.search(r"\b" + name + r"\s*=\s*(-?[0-9.]+(?:e-?[0-9]+)?)f?\b", body)
    return float(m.group(1)) if m else default


def field_enum(body, name, enum):
    m = re.search(r"\b" + name + r"\s*=\s*" + enum + r"\.(\w+)", body)
    return m.group(1) if m else None


def field_bool(body, name):
    return re.search(r"\b" + name + r"\s*=\s*true", body) is not None


def rgba(body):
    m = re.search(r"new Rgba\(([0-9.]+)f?,\s*([0-9.]+)f?,\s*([0-9.]+)f?", body)
    return [float(m.group(i)) for i in (1, 2, 3)] if m else [0.8, 0.8, 0.8]


def enum_members(text, name):
    m = re.search(r"enum " + name + r"\s*(?::\s*\w+)?\s*\{(.*?)\}", text, re.S)
    body = re.sub(r"//[^\n]*", "", m.group(1))
    names = []
    for part in body.split(","):
        part = part.strip()
        if part:
            names.append(part.split("=")[0].strip())
    return names


def main():
    defs, ups, ai, variety, balance, mods, paths = (src(n) for n in
        ("Defs.cs", "Upgrades.cs", "EnemyAI.cs", "Variety.cs", "Balance.cs", "Modifiers.cs", "Paths.cs"))
    data = {}

    # --- Roles and classes -------------------------------------------------------------------------------------
    roles = []
    for b in blocks(defs, "new RoleDef"):
        roles.append({k: field_num(b, k) for k in ("Hp", "Speed", "FireInterval", "AbilityCd")} |
                     {"name": field_str(b, "Name"), "desc": field_str(b, "Desc"), "color": rgba(b)})
    data["roles"] = roles
    kits = enum_members(defs, "Kit")
    kit_names = {"Mage": "Elementalist", "BloodMage": "Hemomancer"}
    hp = dict(re.findall(r"k == Kit\.(\w+) \? ([0-9.]+)f", re.search(r"KitHpMult.*", defs).group(0)))
    sp = dict(re.findall(r"k == Kit\.(\w+) \? ([0-9.]+)f", re.search(r"KitSpeedMult.*", defs).group(0)))
    data["kits"] = [{"id": k, "name": kit_names.get(k, k), "hp": float(hp.get(k, 1)), "speed": float(sp.get(k, 1))} for k in kits]
    xp = re.search(r"XpToNext\(int level\) => ([0-9.]+)f \+ ([0-9.]+)f \* \(level - 1\)", defs)
    data["xpToNext"] = [float(xp.group(1)), float(xp.group(2))]

    # --- Weapons (ordered Role, then Kit, three each) ----------------------------------------------------------
    wsec = defs[defs.index("public static readonly WeaponDef[] Weapons"):]
    weapons = []
    for i, b in enumerate(blocks(wsec, "new WeaponDef")):
        if i >= 3 * len(kits) * 3:
            break
        weapons.append({
            "id": field_enum(b, "Id", "WeaponId"), "name": field_str(b, "Name"), "desc": field_str(b, "Desc"),
            "extra": field_str(b, "ExtraDesc"), "interval": field_num(b, "FireInterval"), "damage": field_num(b, "Damage", 0),
            "speed": field_num(b, "Speed", 0), "life": field_num(b, "Life", 0), "reach": field_num(b, "Reach", 0),
            "melee": field_bool(b, "Melee"), "companion": field_enum(b, "Companion", "MinionKind"),
            "companions": field_num(b, "CompanionCount", 0),
            "role": i // (len(kits) * 3), "kit": (i // 3) % len(kits),
        })
    data["weapons"] = weapons

    # --- Enemies (indexed by EnemyType) ------------------------------------------------------------------------
    types = enum_members(defs, "EnemyType")
    esec = defs[defs.index("public static readonly EnemyDef[] Enemies"):defs.index("public static readonly WeaponDef[] Weapons")]
    enemies = {}
    for t, b in zip(types, blocks(esec, "new EnemyDef")):
        enemies[t] = {"name": field_str(b, "Name"), "hp": field_num(b, "Hp"), "radius": field_num(b, "Radius"),
                      "speed": field_num(b, "Speed"), "xp": field_num(b, "Xp", 0), "score": field_num(b, "Score", 0),
                      "sides": field_num(b, "Sides", 3), "color": rgba(b), "boss": field_bool(b, "IsBoss"),
                      "melee": field_bool(b, "Melee"), "heavy": field_bool(b, "Heavy"), "contact": field_num(b, "Contact", 0),
                      "blurb": field_str(b, "Blurb")}
    data["enemies"] = enemies
    mult = re.search(r"EnemyBulletMult\s*=\s*([0-9.]+)f", defs)
    data["enemyBulletMult"] = float(mult.group(1)) if mult else 1.0

    # --- Waves -------------------------------------------------------------------------------------------------
    cyc = re.search(r"BossCycle\s*=\s*\{(.*?)\};", ai, re.S).group(1)
    data["bossCycle"] = re.findall(r"EnemyType\.(\w+)", cyc)
    data["pool"] = [{"type": "Grunt", "from": 1}] + [{"type": t, "from": int(n)} for n, t in
                    re.findall(r"if \(n >= (\d+)\) pool\.Add\(EnemyType\.(\w+)\)", ai)]
    data["meleePool"] = [{"type": "Swarmer", "from": 1}] + [{"type": t, "from": int(n)} for n, t in
                         re.findall(r"if \(n >= (\d+)\) melee\.Add\(EnemyType\.(\w+)\)", ai)]
    bw = ai[ai.index("void BuildWave"):ai.index("void SpawnEnemy")]
    adds = {}
    for case in re.split(r"case EnemyType\.", bw)[1:]:
        boss = case.split(":")[0]
        m = re.search(r"i < n / (\d+); i\+\+\)\s*spawnQueue\.Add\(([^;]*)\);", case)
        if m:
            adds[boss] = {"per": int(m.group(1)), "types": re.findall(r"EnemyType\.(\w+)", m.group(2))}
    data["bossAdds"] = adds
    bm = balance[balance.index("static float BossHpMult"):]
    bm = bm[:bm.index("public const float EliteSize")]
    bhm = {}
    for cases, val in re.findall(r"((?:case EnemyType\.\w+:\s*)+)return ([0-9.]+)f", bm):
        for t in re.findall(r"EnemyType\.(\w+)", cases):
            bhm[t] = float(val)
    bhm["default"] = float(re.search(r"default: return ([0-9.]+)f", bm).group(1))
    data["bossHpMult"] = bhm
    for name in ("LateBossWave", "MaxWave"):
        data[name] = int(re.search(name + r"\s*=\s*(\d+)", balance).group(1))
    data["LateBossScaling"] = float(re.search(r"LateBossScaling\s*=\s*([0-9.]+)f", balance).group(1))

    # --- Squads and elites -------------------------------------------------------------------------------------
    squads = []
    for b in re.findall(r"new SquadDef \{(.*?)\},\n", variety):
        squads.append({"name": field_str(b, "Name"), "from": int(field_num(b, "From")),
                       "members": re.findall(r"EnemyType\.(\w+)", b), "form": field_enum(b, "Form", "SquadForm") or "Line"})
    data["squads"] = squads
    data["traits"] = [t for t in enum_members(variety, "EliteTrait") if t != "None"]
    data["squadShare"] = float(re.search(r"SquadShare\s*=\s*([0-9.]+)f", variety).group(1))

    # --- Abilities ---------------------------------------------------------------------------------------------
    abilities = {}
    for fn in sorted(os.listdir(CORE)):
        if not fn.endswith(".cs") or fn == "Upgrades.cs":
            continue
        for b in blocks(src(fn), "new AbilityDef"):
            aid = field_enum(b, "Id", "AbilityId")
            if not aid:
                continue
            ch = re.search(r"\bChoice\s*=\s*(AbilityDef\.Ult|\d+)", b)
            abilities[aid] = {"name": field_str(b, "Name"), "desc": field_str(b, "Desc"), "capstone": field_str(b, "Capstone"),
                              "cd": field_num(b, "Cooldown", 0), "kit": field_enum(b, "Kit", "Kit"), "role": field_enum(b, "Role", "Role"),
                              "choice": ("ult" if ch and ch.group(1) == "AbilityDef.Ult" else int(ch.group(1)) if ch else None),
                              "charges": 1 + int(field_num(b, "ExtraCharges", 0)), "healthCost": field_num(b, "HealthCost", 0),
                              "capstoneCharge": field_bool(b, "CapstoneCharge")}
    data["abilities"] = abilities

    # --- Upgrade cards -----------------------------------------------------------------------------------------
    allsec = ups[ups.index("public static readonly UpgradeDef[] All"):ups.index("public static int[] ModOffer")]
    cards = []
    for b in blocks(allsec, "new UpgradeDef"):
        apply = re.search(r"Apply\s*=\s*s\s*=>\s*(.*)$", b.strip(), re.S)  # Apply is always the last field
        effects = []
        if apply:
            for stmt in re.split(r";", apply.group(1).strip("{} ")):
                stmt = stmt.strip()
                m = re.match(r"s\.(\w+)\s*(\+=|\*=|=)\s*(.+)$", stmt)
                if not m:
                    continue
                f, op, rhs = m.groups()
                num = re.fullmatch(r"(-?[0-9.]+)f?", rhs.strip())
                if num:
                    effects.append([f, op, float(num.group(1))])
                elif rhs.strip() == "true":
                    effects.append([f, "=", 1])
                else:  # Math.Min(cap, s.X + v), or Explosive Rounds' first-stack-is-bigger
                    mm = re.match(r"Math\.Min\(([0-9.]+)f,\s*s\.\w+\s*\+\s*([0-9.]+)f\)", rhs.strip())
                    if mm:
                        effects.append([f, "+cap", float(mm.group(2)), float(mm.group(1))])
                    else:
                        mm = re.match(r"s\.\w+ == 0f \? ([0-9.]+)f : ([0-9.]+)f", rhs.strip())
                        if mm:
                            effects.append([f, "+first", float(mm.group(1)), float(mm.group(2))])
        cards.append({"name": field_str(b, "Name"), "desc": field_str(b, "Desc"), "rarity": field_enum(b, "Rarity", "Rarity"),
                      "max": int(field_num(b, "MaxStacks", 99)), "role": field_enum(b, "Role", "Role"),
                      "requires": field_enum(b, "RequiresAbility", "AbilityId"), "grants": field_enum(b, "Grants", "AbilityId"),
                      "needsExtra": field_bool(b, "NeedsExtraAbility"), "boss": field_enum(b, "Boss", "EnemyType"), "effects": effects})
    data["upgrades"] = cards
    kd = re.search(r"KnightDesc = new Dictionary<string, string>\s*\{(.*?)\};", ups, re.S).group(1)
    data["meleeCardText"] = {unescape(a): unescape(b) for a, b in re.findall(r"\{\s*" + STR + r",\s*" + STR + r"\s*\}", kd)}

    # --- Modifiers and paths (names and blurbs) ----------------------------------------------------------------
    data["modifiers"] = [{"name": field_str(b, "Name"), "desc": field_str(b, "Desc"), "lean": field_enum(b, "Lean", "ModLean")}
                         for b in blocks(mods, "new ModDef")]
    data["paths"] = [{"name": field_str(b, "Name"), "danger": field_num(b, "Danger", 0)} for b in blocks(paths, "new PathDef")
                     if field_str(b, "Name")]

    # --- Single numbers the planner uses ----------------------------------------------------------------------
    def const(fn, name):
        return float(re.search(name + r"\s*=\s*([0-9.]+)f", src(fn)).group(1))
    data["consts"] = {"HomingDamageCost": const("Combat.cs", "HomingDamageCost"), "ParryCapacity": const("Parry.cs", "ParryCapacity"),
                      "PassiveRegen": const("Balance.cs", "PassiveRegen"), "AuraHeal": const("Balance.cs", "AuraHeal"),
                      "AuraRange": const("Balance.cs", "AuraRange"), "AuraSelfHeal": const("Balance.cs", "AuraSelfHeal")}

    check_formulas(ai, variety, src("Waves.cs"))
    out = os.path.join(os.path.dirname(__file__), "game-data.json")
    with open(out, "w", encoding="utf-8") as f:
        json.dump(data, f, indent=1, ensure_ascii=False)
    print(out, "-", len(weapons), "weapons,", len(abilities), "abilities,", len(cards), "cards,", len(enemies), "enemies,", len(squads), "squads")


# The lines the pages' JavaScript copies by hand. If one changes, update the page (see Docs/README.md) and this list.
FORMULAS = [
    ("EnemyAI.cs", "int count = Math.Min(4 + n * 2, n < 20 ? 32 : Math.Min(40, 32 + (n - 20) / 2));"),
    ("EnemyAI.cs", "count += 2;  // the first waves get a couple more"),
    ("EnemyAI.cs", "count = (int)(count * 1.25f);  // a third player"),
    ("EnemyAI.cs", "public static float MeleeShare(int wave) => Math.Max(0f, Math.Min(1f, (13f - wave) / 12f));"),
    ("EnemyAI.cs", "e.MaxHp = d.Hp * (1f + 0.15f * (Wave - 1)) * PartyHpScale(d.IsBoss);"),
    ("EnemyAI.cs", "if (n <= 1) return 0.7f;"),
    ("EnemyAI.cs", "return boss ? 1.5f : 1.2f;"),
    ("EnemyAI.cs", "public int ShotBudget => (int)(Math.Min(120f, 50f + 4f * Wave) * (1f + 0.3f * (Players.Count - 1)));"),
    ("EnemyAI.cs", "return (int)(ShotBudget * 0.4f);"),
    ("EnemyAI.cs", "float rate = Math.Min(2f, 1f + 0.05f * (Wave - 1));"),
    ("EnemyAI.cs", "Wave >= 12 && rng.NextDouble() < Math.Min(0.35, 0.04 * (Wave - 11));"),
    ("EnemyAI.cs", "e.MaxHp *= 2.5f;"),
    ("Variety.cs", "int want = Wave >= 35 ? 2 : 1;"),
    ("Mastery.cs", "MasteryCooldown(Player p, AbilityId id) => 1f - 0.04f * (MasteryLevel(p, id) - 1);"),
    ("Game.cs", "return Upgrades.Ability(id).Cooldown * MasteryCooldown(p, id);  // ultimates ignore cooldown upgrades"),
    ("StatSheet.cs", "Pct(s.DmgMult * (1f - Game.HomingDamageCost * s.Homing))"),
    ("Waves.cs", "spawnTimer = Math.Max(0.3f, 1.1f - Wave * 0.05f);"),
]


def check_formulas(*_):
    bad = [f + ": " + line for f, line in FORMULAS if line not in src(f)]
    if bad:
        sys.exit("These lines changed; update the Wave Almanac's formulas and extract.py's FORMULAS:\n  " + "\n  ".join(bad))


if __name__ == "__main__":
    main()

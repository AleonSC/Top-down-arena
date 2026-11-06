# Wildcard (4th role): design

Status: **built** (Nov 2026, protocol 186); see `Docs/AfterReset.md`. Final choices: second ability on a bonus key (Q/E/Shift) chosen in the lobby, same 5 slots; Hemomancer abilities keep their health cost; Wildcard is locked out of the lobby and Practice Range until unlocked; bots only get it by hand. The lobby already has a locked 4th role slot (`RoleSlots = 4`, `LobbyUi.cs`);
`History.md` notes a Wildcard "pick any class's weapon/ability" was discussed for it.

## Decisions (owner, Nov 2026)

| Question | Decision |
|---|---|
| Pool | **Any DPS weapon and any DPS ability, from any class** (Soldier, Elementalist, Summoner, Knight, Ninja, Hemomancer). No Tank or Healer gear. |
| Identity | Middle health (~130 HP), **no role passive** (no taunt, no healing aura), plus a "+1 bonus slot" (see open question 1). |
| Unlock | **Win a run once with each role** (Tank, Healer, DPS). Until then the slot stays the padlock "Coming soon" card. |
| Cost | **Lesser stats: less damage and slower cooldowns** than a native role. |
| Upgrades | Generic cards work as normal. A weapon's own cards (e.g. Warding Ink for Spirit Chalk) still appear, since they key off the weapon. **Class passives do not carry over** (Hemomancer blood price and low-health bonus, Ninja speed, Knight armour). |

## First-guess numbers (constants to tune)

- Health 130.
- Damage x0.90 on all Wildcard damage.
- Ability cooldowns x1.25 (25% slower).
- Both as named constants so they're one-line changes.

## Answers to the first round (owner, Nov 2026)

1. **Bonus slot:** the Wildcard starts with a **second ability** (two Space abilities, both from the DPS pool).
   Open detail: a second key, or Space cycles / both fire? Needs a pick.
2. **Win:** co-op wins count toward the "win once with each role" unlock.
3. **Hemomancer abilities:** they deal their damage but get **no low-health bonus**. (Whether the health cost stays
   wasn't said; assumed it stays, since it is part of the ability. Confirm.)
4. **Mastery:** borrowed gear earns mastery under its original class.
5. **Bots:** they keep their current three role choices by default; the player can assign a bot Wildcard by hand.

## Still open

- How the second ability is triggered (second key vs. both on Space).
- Does the Hemomancer health cost stay?
- Practice Range: Wildcard unlocked regardless? (assumed yes)

## Implementation sketch (when approved)

- `Role` gets `Wildcard` at the **end** (`Core/Defs.cs`, currently Tank, Healer, Dps). **Bump `Session.ProtocolVersion`**,
  since the role is in lobby messages and snapshots.
- `Kit` stays as picked (it chooses the class and its stats); the weapon and ability lists for Wildcard are the union
  of every kit's DPS rows, so `Defs` needs a lookup `WildcardWeapons/Abilities` rather than a (role, kit) key.
- Health multiplier and the two penalties live where role health and `Player.Speed` are computed. No aura or taunt code
  runs for the role.
- Lobby: unlock check on the role card, then 4 role cards fit the existing grid; the weapon and ability rows show
  every class's DPS options (grouped by class).
- Tests: a Wildcard suite (damage and cooldown multipliers, kit-locked cards still offered, class passives absent,
  unlock gating), then core + features. README Roles section and `CODEMAP.md` updated. Armory/Bestiary left alone.

# Wildcard (4th role): design

Status: **decided, not built** (Nov 2026). The lobby already has a locked 4th role slot (`RoleSlots = 4`, `LobbyUi.cs`);
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

## Open questions

1. **"+1 bonus slot"** was in the option text I offered, but I never said what it is. Candidates: a third loadout
   slot (a second ability), an extra upgrade-card choice per level-up, or nothing (drop it, the freedom of mixing is
   the bonus). Which?
2. **What counts as a win?** Surviving the final wave of a run, or any "victory" end screen? Does it have to be solo,
   or does a co-op win count? Needs a per-role win record in the profile (check what `Records` already stores).
3. **Hemomancer abilities without the Hemomancer class.** Every Hemomancer Space ability costs health and hits harder at
   low health. With no class passive, either they keep their health cost (the cost is part of the ability) and lose the
   low-health bonus, or they become free and flat. Same question for Knight/Ninja gear that reads class state.
4. **Mastery and XP.** Does borrowed gear earn ability mastery under its original class? Suggest yes.
5. **Bots and Practice.** Bots fill roles by name; simplest is that bots never pick Wildcard. Practice Range: unlocked
   regardless?

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

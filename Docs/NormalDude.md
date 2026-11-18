# Normal Dude: design plan

Status: **design only, not built.** Decisions made with the owner (November 2026).

## The idea

A joke class: a regular guy with no gimmick. The baseline the other classes are measured against.

## Decisions

- **Kit**: pure vanilla. Average stats (the defaults every class is tuned from), no active ability, no ultimate, no passive.
- **Weapon**: a random existing weapon, rolled once per run at spawn ("grabbed whatever was lying around"). Rolled by the host, so every peer agrees.
- **Look**: plain player model (no accessory), a name like "NORMAL DUDE", tagline "Just a guy."
- **Roles**: fills any role; it has no role bonus.

## Open questions (for the build step)

- Weapon pool: every `WeaponId`, or only starter-tier weapons? Reroll on death in Survival?
- Upgrade cards: general cards only (no class ability cards exist for him).
- Bots: use the generic bot brain, no special handling.

## Build steps

1. `Kit.NormalDude` at the end of the enum (before `None`), class registry entry, weapon roll, picker entry; protocol bump + `Docs/ProtocolHistory.md` line.
2. Player model + preview shot (`Tools/Preview`).
3. Test (`run.sh 1 1 normaldude`), README section, CODEMAP line.

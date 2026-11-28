# Arcanist: telekinesis, runes and enchantments

Status: **step 1 built** (Nov 21, 2026; protocol 462): one weapon, 1st ability and ultimate per role, the Kinetic Field passive,
looks and icons. Next: more weapons and abilities per role (each role has two locked weapon keys and two locked ability keys),
class cards, bot Alt rules. Numbers are first guesses. Previews: `ARCANIST=tank|healer|dps Tools/Preview/shot.sh x hud`,
`KIT=7 Tools/Preview/shot.sh x kitgrid`.

## The class

A mage who moves things with their mind and writes runes in the air: violet force, gold glyphs. Indigo robes, a deep hood with
a gold circlet. Health x0.9, speed x1.0. Passive **Kinetic Field**: enemy bullets within 90 px fly at 75% speed.

## Kit (decided Nov 21)

| Role | Weapon | 1st ability | Ultimate |
|---|---|---|---|
| Tank | **Orbit Shards**: 4 shards circle you cutting small shots; Fire flings one out and back; Alt held: Shard Guard (stops every blockable shot at 46 px, you at 55% speed) | **Kinetic Grasp** (10 s): hold the biggest enemy near the cursor as a shield; press again (or 4 s) to throw it | **Grand Sigil** (75 s): 200 px circle, binds then slows and taunts; enemy shots freeze on its rim and fly back out when it ends |
| Healer | **Glyph Stylus**: rune bolts through two enemies, heal 7 to an ally they pass | **Arcane Tether** (12 s): pull the ally nearest the cursor to you, untouchable on the way, +30 health | **Sanctum Sigil** (80 s): 170 px circle following you, 25 HP/s, six rim runes that burst on enemies |
| DPS | **Dancing Blades**: 3 swords; Fire sends one to dance 1 s on the cursor's enemy (4 slashes); Alt recalls them all | **Telekinetic Crush** (toggle, force meter): a 70 px grip follows the cursor carrying up to 6 enemies (6/s) and catching shots; let go still to slam (30 +10 per other), on the move to fling them into walls (25); caught shots fly back as blades (TelekineticCrush.cs) | **Runic Array** (65 s): 4 sigils behind you for 6 s; each Fire also sends 4 spectral blades |

Names: the Healer's 1st ability was drafted as "Lifeline", already the Healer Soldier's ultimate, so it is **Arcane Tether**.

## How it's built

- `Core/Arcanist.cs`: everything above. The shards and blades are weapon companions (`MinionKind.ArcShard` / `ArcBlade`),
  moved by `MoveArcane`; their Alt goes through `Msg.WeaponDash` (52-54). Kinetic Field and the sigil's rim are a speed share
  on enemy shots in `MoveBullets` (`ArcShotSpeed`, on every game from the replicated players and zones).
- `Game/ArcanistLook.cs`: the looks; the model's palette, mantle and hood are cases in `CharacterModels.cs`.
- Test: `Tools/SimTest/run.sh 1 1 arcanist`.

## Open questions

- Shard Guard has no limit beyond the slow: add a meter if it proves too safe.
- Class cards (e.g. a wider Kinetic Field, more shards) and the bots' Alt rules (they never use Shard Guard or the recall yet).

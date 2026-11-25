# Spirit Avatar (design)

Status: **built** (Nov 2026, protocol 500). The Tank Ninja's third ultimate (lobby key 2 of the Ultimate row; Thousand Clones stays the
default, Dragon's Dogma is key 1). The owner chose look **C, Sumi Giant**, and the rules as shown in the mockup.
Mockup: [`Docs/Mockups/spirit-avatar.html`](Mockups/spirit-avatar.html) (https://claude.ai/artifact/7HCYDiWg8GvaPzgP3heeZj).
Looks A (Oni Warden) and B (Kitsune Mantle) are in the same mockup and were not picked.

## How it plays
- **Cast**: 70 s cooldown, lasts **12 s**. A giant ink spirit with a red seal on its back wraps your Ninja.
- **Rise** (0.8 s): ink pulls in round you; you can't be hurt; **30** and a shove to enemies within **95 px**.
- **Avatar**: **60%** less damage, walk at **85%**, every enemy (bosses too) goes for you. Enemy shots that hit the
  spirit's body are swallowed.
- **Moon Cleave**: Fire is the cleave (your weapon is put away). **0.9 s** wind-up: the giant draws its brush back and a red
  arc fills in on the floor (you turn slowly). Then one **200°** sweep out to **170 px**: **80** damage, **0.5 s** stun, enemies
  pushed back **~150 px** (heavies half, bosses not). The stroke stays on the floor about 2 s. One every **1.6 s** while Fire is held.
- **Release**: at the end the spirit shudders, then bursts: **40** and a shove within **130 px**. Also ends at once if you go
  down or the wave ends.
- All damage scales by your damage. **Mastered**: 15 s, the cleave reaches 20% further. Rare card **Kami Vessel** (up to 2):
  +2 s and the wind-up 0.2 s faster, a stack.

## How it's built
- `Core/SpiritAvatar.cs`: the cast, rise, cut, taunt, shot swallowing, the Moon Cleave (`SpiritTrigger`, `TickSpirit`, `SpSweepHits`)
  and the release; replicated `Player.SpT`, `SpAge`, `SpWind` (0 none, 0..1 wind-up, 1..2 sweep), `SpAng`.
- `Game/SpiritAvatarLook.cs`: the Sumi Giant and the floor marks (`FxKind.SpiritInk`).
- Test suite `spiritavatar`; preview `SPIRIT=<s> NINJA=0 Tools/Preview/shot.sh x hud`.

## Not done
- Its own sounds: it uses the shared `Shockwave` (rise, each cleave) and `BigExplode` (release) sounds for now.
- Other games always show the base reach on the floor arc (mastery isn't sent).

## The plan it was built from
1. **Core**: `Core/SpiritAvatar.cs` on the Stone Colossus pattern (`Core/StoneColossus.cs`): `AbilityId.SpiritAvatar` (at the
   end of the enum), the `AbilityDef` with `UltKey = 2`, cast, rise, damage cut and taunt, Fire as the cleave (wind-up, sweep
   hit by angle, push), shot swallowing, release; replicated `Player` fields (time left, age, wind-up 0..1, aim lock); protocol
   bump. Test suite `spiritavatar`.
2. **Look**: `Game/SpiritAvatarLook.cs`: the ink body, seal, mask, arms and brush; the floor arc during the wind-up; the stroke
   left on the floor (an `FxKind`); rise and release. Preview `SPIRIT=<s>` through `Tools/Preview`.
3. **Sounds, card, bot, README**: brush whoosh and ink slap, the Kami Vessel card, a bot cast rule, README rows.

# Docs

Design docs (`Paths.md`, `BossHuntsAndPerks.md`, `ZombieInfection.md`, ...); each starts with its status.
`History.md` holds finished work and the old changelog; `Mockups/` the visual options.

The Bestiary and Armory web pages were removed. Their replacement is the in-game Field guide
(`Assets/Scripts/Game/FieldGuide.cs`): the Armory reads the game's definitions directly, and the Bestiary's text is
in `Assets/Scripts/Core/BestiaryData.Entries.cs` (edit by hand; the `fieldguide` sim test checks every `EnemyType`
has an entry).

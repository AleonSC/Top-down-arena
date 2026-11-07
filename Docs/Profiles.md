# Multiple profiles

Status: **built** (protocol unchanged). The picker is a "Who's playing?" screen on every launch (profiles as floppy disks in clamp cradles on a mechanical shelf, names in Permanent Marker (`Resources/Fonts`, Apache 2.0): "Shelf 4" and `whos-playing-mechanical.html`, of
`Docs/Mockups/whos-playing-shelves.html`, first round `whos-playing-floppy.html`; `ProfilesUi.cs`), plus a "Switch profile" button above the main menu's name card.
Code: `Core/ProfileStore.cs`, `Profile.Name` (format 3), `Game/ProfilesUi.cs`; tests in the `saves` suite
(`ProfileStoreTest`); preview `Tools/Preview/shot.sh who who` (`TWO=1 MANAGE=1 EMPTY=1`; the move: `shot.sh whotr whotr` with `PROFTR=<0..2.26>,<slot>[,back]`). At most **3 profiles** and always **four boxes** (three slots, an empty one says "Empty", and New profile, grayed out when all three are used), so the move to and from the menu is the same every time; a
command-line start (`--host`, `--join`, `--autostart`) skips the screen. Profiles are listed by name.

## Decisions (owner)

1. The lobby name is **per profile**.
2. Saved runs are **split per profile**.
3. Keybinds, volume, aim line, port and last address **stay global** (`PlayerPrefs`).
4. Look: mockup first (`Docs/Mockups/profiles.html`).

## Layout on disk

```
<persistentDataPath>/
  profiles/
    active.txt                 id of the selected profile (one line)
    <id>/
      profile.txt              records, rank, cosmetics, plus a new "name" line
      profile.txt.bak          (already built) the save before last
      profile.corrupt-*.txt    (already built) set-aside files
      saves/*.run              this profile's saved runs
```

`<id>` is a slug of the name plus a short suffix if taken (never the raw name: no slashes, dots or reserved names).
`Profile.Folder` and `SaveStore.Folder` simply point into the active profile's folder, so `Profile.Current`, `Save()`,
`Load()` and the recovery code are unchanged. Switching = set both folders, `Profile.Reload()`, reload the name.

## Core: `ProfileStore` (new, `Core/ProfileStore.cs`, no Unity)

`Root` (null: off, tests set a temp folder), `List()` (id, name, rank, runs, wins, play time, read cheaply),
`Create(name)`, `Rename(id, name)`, `Delete(id)`, `Active` / `SetActive(id)`, `EnsureMigrated()`.
Rules: names 1-16 chars, trimmed, unique ignoring case; the last profile can't be deleted; deleting the active one
switches to the first left; a folder without a readable profile.txt is still listed (by id) so nothing is hidden.

## Profile data

`Profile` gets `Name` (a `name` line; `FormatVersion` 3 with a migration step 2 -> 3 that leaves it empty, the
store fills it from the folder). The lobby name field edits the active profile's name and saves it. The old
`PlayerPrefs "player_name"` is only read once, to name the migrated profile.

## Migration (first launch with the new code)

Copy, don't move: `profile.txt` (and `.bak`) -> `profiles/default-<n>/`, `saves/*.run` -> its `saves/`; name from
`player_name`. Leave the originals in place (marked by a `migrated.txt`) so a downgrade or a bug loses nothing.
Run once, only when `profiles/` doesn't exist.

## Co-op and split saves

Only the host writes a run save, in the host's active profile. Seats are matched by name (`RunSaves.SeatFor`), and a
client's lobby name is now their profile's name, so continuing works as before. Two people on one computer who both
host keep separate Continue lists. One consequence to accept: a run hosted under profile A isn't visible from B.
(Moving a run between profiles is a possible later "Copy to..." action.)

## Tests (SimTest, "saves" suite)

Migration from the single-file layout (originals left alone, runs only once); create/rename/delete rules; switching
doesn't mix records or saved runs; hostile names ("..", "con", unicode, 200 chars) make safe ids; a damaged
profile.txt in one profile doesn't affect another; the name round-trips.

## Front end

`ProfilesUi.cs` (new) for the chosen look; `Menus.cs` name field reads and writes the active profile's name;
`SavesUi.InitSaveFolders` calls `ProfileStore.EnsureMigrated()` and points the folders. Delete asks twice, like the
Continue list (`deleteArmed`). Add the new files to `CODEMAP.md` and `HANDOFF.md`.

## Not in this change

No network message changes, so **no `Session.ProtocolVersion` bump**. Cloud sync and a PIN/lock per profile are out
of scope.

## The move to and from the menu (built, Nov 2026)

Look C ("Plot points") of `Docs/Mockups/profile-transitions.html`, at half speed (~4.5 s), `Game/ProfileTransition.cs`.
Picking a profile plays it forward; the menu's "Switch profile" plays it backwards. Each box shrinks into a point on the axis
(a ping each), the points you didn't pick shoot off the ends, yours slides to the origin and opens into the menu box; the
menu's own intro (`MenuIntro.cs`) runs from a remapped clock. Sounds are `Sfx.Prof*` (SoundBank.cs) plus the pause menu's
thunk and key ticks. Any key skips it; nothing can be clicked meanwhile. Reduced effects or Visual effects off: no move.
Audio and drawing only, no protocol bump. Preview: `PROFTR=<0..2.26>,<slot>[,back] Tools/Preview/shot.sh x whotr`.
Profiles made before the limit (more than 3) stay on disk; the picker shows the first three until some are deleted.

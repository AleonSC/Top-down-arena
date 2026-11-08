# Enemy readability: telling what an enemy does at a glance

Feedback from two recorded runs (v191 before / v194 after the wave recipe): "it kinda feels hard to differentiate what
enemies even do sometimes". The recipe (`EnemyRoles.md`) gave every enemy a job, but the player can't see the job.
Four changes, in the order they should land. Mockup: `Docs/Mockups/enemy-readability.html`.

## 1. Role language (color + shape marker)
Every enemy carries its job (`Game.JobOf`) as a small marker under or over the body, one color and one shape per job:
Pressure = orange dot, Hunter = red chevron, Zoner = yellow ring, Disruptor = violet diamond, Anchor = cyan square.
Body art stays; the marker is the second read. Three looks in the mockup (Pip, Underline, Rim). Colorblind-safe
because shape carries the job, color only helps.

## 2. Stronger telegraphs
One wind-up per attack, one look per attack kind: line (shots, beams), cone (melee swings), ring (blasts, zones),
tether (grabs). Wind-up length and the fill color follow the job color. Audit every enemy against that table; the
ones that already have a good telegraph (Beam Sentry, Riot Shield, Spike Block, Reaver) are the reference.

Done for lunges (option A, `Docs/Mockups/lunge-telegraph.html`): the Lunger, Spike Block, Charger and Riot Shield share one lane
(`Game/LungeLaneLook.cs`): a strip as wide as the body and as long as the lunge, Hunter red, filling over the wind-up, white for
the last 0.1 s. It replaced their four older looks. The fill is timed on each client from when it first sees the wind-up
(`PhaseT` isn't sent), so there is no protocol change. Preview: `LUNGE=0.5 Tools/Preview/shot.sh lunge hud 12 5 1`.
Also done: the Phantom marks the spot it will blink to (a `FxKind.Target` ring for `Game.PhantomWarn` = 0.5 s, Phase 1) before it
blinks and fires its ring, and the Mortar's bomb comes with a landing ring (`MortarMarkRadius`). Test: `landmarks`.
Aim lines (`Game/ShooterAimLook.cs`): while an aimed shooter winds up (`Enemy.Winding`, 0.3 s) a thin dashed line, the same dash
the Leader and Beam Sentry use, shows its shot: Sniper, Shard, Medic, Splitter and Phantom one line, the Grunt its three-shot fan. The
rings, spirals and lobs (Burster, Spinner, Mortar) have their own marks. Preview: `AIM=0.8 Tools/Preview/shot.sh aim hud 12 5 1`.
Charger: a `FxKind.Target` ring marks where the dash ends (and so where its ring of 8 will burst) from the start of the wind-up
(`ChargerDashSpeed` x `ChargerDashTime` = 252 px along its facing, clamped to the arena; walls can still cut a dash short).
Section 2 (telegraphs) is done; section 4 (name tags) is next.

## 3. Fewer types per wave
Waves 16-24 spawn 10-14 distinct types (see the run logs). Cap a recipe wave at 6 distinct types (8 from wave 25):
the 2-3 headliners, plus the singles needed to fill each job's share. Squads count as one type. Fewer kinds on screen
means each one gets learned. Change lives in `WaveRecipe.cs` (headliner pick and the fill step) with a test in the
features suite that counts distinct types per wave.

Done: `Game.KindCap` (6, 8 from wave 25) bounds a recipe wave. Headliners count first, then `BuildSquads` only takes a squad
whose types still fit, then the singles repeat the types already on the bill (`KindOk`). Test: `wavekinds`. Children a type
spawns later (Splitter's Shards) and boss adds are not counted.

## 4. In-game tags
First 3 times a type is seen (per profile) and for elites: a short name + job icon above it for 2 s on spawn, plus a
one-line "what it does" in the wave banner for a newly unlocked type. Stored in the profile, no network change.

Tags done (world part): `Game/EnemyTagLook.cs`, `Profile.TagSeen` ("tag" lines, profile format 6, `Profile.TagShows` = 3). The first 3
times a type comes into view it carries its name and job (job colours) for 2.6 s, fading out; after that, never. Counted per profile,
client side only, nothing sent. Elites are not tagged on their own (a Gauntlet wave has 40-60 of them); they follow their type's
count. Preview: `TAGS=1 Tools/Preview/shot.sh tags hud 12 5 1`. New-enemy chips (option B of `Docs/Mockups/new-enemy-banner.html`): under the between-waves box (and under the GET READY band) one
CRT chip per type the next wave brings that this profile hasn't met: job marker, name, job, NEW tag, one line from `Game.EnemyNote`.
`Game.NewTypesAt(n)` (`Core/NewEnemies.cs`) lists what a wave brings (its `WavePool` against the last wave's, plus the rushers'
unlock waves); a type counts as met once it has carried its name tag (`Profile.TagSeen`). Up to three chips; none on boss waves or in
the Practice Range. Code: `Game/NewEnemyChips.cs`. Test: `newenemies`. Preview: `WAVECLEARED=1 WCWAVE=17 Tools/Preview/shot.sh chips hud 18 5 1`.
The one-line notes are drafts for the owner to reword. Section 4 is done.

## Order and cost
1 then 3 (small, mostly code), then 2 (art audit), then 4 (profile data + UI). Only 4 touches the profile; none change
the snapshot format unless the job marker is sent rather than derived (derive it: `JobOf` is deterministic).

## Not touched
Bestiary / Armory reference pages are not updated (owner's rule); they'll be out of date once these land.

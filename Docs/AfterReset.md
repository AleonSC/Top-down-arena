# To do after the usage reset

A running list of things to pick up next session. Tick off / delete as they're done.

## Bugs / polish
- [x] **Soldier DPS gun animations (Twin Rifle, Railgun, Burst SMG).** Built (Nov 2026, no protocol change, 156): all three of [`Mockups/soldier-dps-gun-animations.html`](Mockups/soldier-dps-gun-animations.html) (https://claude.ai/artifact/75sL7drSpCfqpWaaUaTxjH), glowing shots kept, lobby preview matches (`DpsGunLooks.cs`; spray cone in `WorldRenderer.cs`). Needs a try in Unity. Left out: the mockup's gold crossfire ring and per-enemy pierce rings (`Bullet.Crossfire` is host-only, so clients can't see it), and the cone doesn't widen with the extra-projectile upgrade. Armory pages for these three are now out of date.
- [ ] **Your look: Reward ladder first, two-line ladder rows.** Built (Nov 2026, protocol unchanged): the Reward ladder is the first row of the drawer (`SectionOrder`, `CosmeticsUi.cs`), and each ladder row is two lines, name on top with the kind under it, status on the right (look A of [`Mockups/reward-ladder-text.html`](Mockups/reward-ladder-text.html), https://claude.ai/artifact/8DzFHSAzyt9YBpRTFkyJhS), so names are no longer cut off. Rows are 52 px (were 42). Not compiled (no mono in the cloud container) and not tried in Unity.
- [ ] **Sanctum: healing pulses, Bubble look.** Built (Nov 2026, protocol unchanged, 156): look B of [`Mockups/sanctum.html`](Mockups/sanctum.html) (https://claude.ai/artifact/TT6twY4kADa161ohhGM9aj).
      The dome (200 px, 300 with the capstone, 7 s, still stops bullets and prevents going down) no longer heals 20 HP/s: it sends 7 pulses from its centre, one every 0.9 s starting at 0.9 s,
      20 HP each and 30 for the last (150 total, was 140). A ring takes 0.55 s to reach the rim and each ally is healed as it passes them (`Core/Sanctum.cs`; `Game/SanctumLook.cs`).
      Left out of the mockup: ripples where bullets hit the shell. Needs a try in Unity. Armory page for Sanctum is out of date.
- [ ] **Lumen Ward (Sanctify, reworked).** Built (Nov 2026, protocol 156): look A (Cathedral) of [`Mockups/sanctify-cone.html`](Mockups/sanctify-cone.html) (https://claude.ai/artifact/V38EdAFeoRynvKNiGrvooq), named Lumen Ward (`AbilityId.Sanctify` kept). A 740 px cone along your aim for 6 s, locked at the cast: heals 9 HP/s inside, burns enemy bullets in it; enemy bullets elsewhere slow to a stop (about 0.7 s, spreading at 1100 px/s) and fly again when it ends. Cooldown 20 s, no longer Instant for mastery XP. Capstone: enemies outside the light freeze too (not bosses). Numbers are my proposals. Left out of the mockup's look: the dark vignette outside the cone. Needs a try in Unity. README is updated; the Armory page for it is out of date.
- [ ] **Melee grunts: Buzzsaw, zig-zag Swarmer, Spike Block.** Built (Nov 2026, protocol 157) from [`Mockups/melee-grunts.html`](Mockups/melee-grunts.html) (https://claude.ai/artifact/6eBLdvmwXbaWiwDVRPKpo4). The Buzzsaw (new, `EnemyType.Buzzsaw`) and Spike Block (new, `EnemyType.SpikeBlock`) join the melee mix from wave 5 (and Swarm Fields); the Swarmer stays, now zig-zagging with a thin spike on each face (the Buzzsaw is added beside it, not instead of it: say if you want the Swarmer gone). Numbers are first guesses (`MeleeGrunts.cs`). A Buzzsaw cuts your hit grace to 0.12 s so its touches come fast. The Swarmer's zig-zag makes it slower to arrive (about 0.6 of its speed toward you). Needs a try in Unity. Bestiary and Armory pages are out of date (no entries for the two new enemies).
- [ ] **Artillery Assault and Bullet Rain.** Built (Nov 2026, protocol 155). **Artillery Assault** is the DPS Soldier's Bullet Storm renamed, with look A of
      [`Mockups/bullet-rain.html`](Mockups/bullet-rain.html) (heat aura, streaks, muzzle flash; `ArtilleryLook.cs`); mechanics unchanged. **Bullet Rain** replaces the
      Elementalist DPS's Meteor Storm: look D (Downpour) of [`Mockups/bullet-rain-elementalist.html`](Mockups/bullet-rain-elementalist.html). A `ZoneKind.BulletRain`
      zone (165 px radius, fixed where you cast it, 6 s or 9 s mastered) that does 45 damage a second to every enemy in it (`BulletRainLook.cs` draws it). Needs a try in
      Unity; the 45 a second is a first guess and the zone does not follow the cursor as the mockup did. Enemies in the rain are wet (3 s after they leave, with the 30% wet slow).
      `AbilityId.MeteorStorm` became `BulletRain`. Armory and Bestiary pages are out of date for both.
- [x] **Your look: side-menu icons + Reward ladder.** Built (Nov 2026, protocol unchanged, 152): the icons (`ui_frame`, `ui_trail`, `ui_scene`, `ui_card`, `ui_name`, `ui_aim`, `ui_emote` in `PixelArt.cs`) and look 1 of [`Mockups/reward-ladder-next.html`](Mockups/reward-ladder-next.html) (https://claude.ai/artifact/XySVp6TrCVv4ruoSvgm8oh): the next reward pinned in a box on the left (live preview, XP to go), the ladder as a rail on the right (shields on a line, tier banners, a gold "you" marker; it opens at your rank). Compile check, core and feature suites pass. Needs a try in Unity.
- [x] **Lifeline look.** Built (Nov 2026, protocol 153): look E (Heartbeat Halo) of [`Mockups/lifeline.html`](Mockups/lifeline.html) (https://claude.ai/artifact/KTCFvJN7mTMWBGzBJ1sbu7), under Visual effects (`LifelineLook.cs`, new `FxKind.Lifeline`). Needs a try in Unity, with an ally down, to see the halo and column on everyone. The downed ally is revived at the cast (as before), so the mockup's raise-on-landing is just the halo dropping in. The Armory and Bestiary are unaffected.
- [x] **Menu title typing sounds.** Built (Nov 2026, protocol unchanged, 151): look B (Mechanical keyboard) of [`Mockups/menu-typing-sounds.html`](Mockups/menu-typing-sounds.html) (https://claude.ai/artifact/TQwbAXCgovn2mq9gH7V5mK). A click-and-thock per letter while the intro types the title (four takes), a deeper one for the space, and a bigger Enter thock on the last letter (`TickTitleTyping`, `MenuIntro.cs`; `Sfx.TitleKey/TitleSpace/TitleEnter`). Plays in the profile picker to menu move too (it types the title as well). Own screen only; silent when the intro is skipped or with reduced effects. Needs a try in Unity.
- [x] **Cursor cosmetics.** Built (Nov 2026, protocol unchanged, 153): the ten looks of [`Mockups/cursors.html`](Mockups/cursors.html) (https://claude.ai/artifact/1RkWbWDHf2rd3vmaSydA4e), under **Cursor** in Your look. Classic (the old crosshair) is free; the others unlock at ranks 3, 4, 11, 15, 17, 21, 26, 31, 38 and 46. Only your own crosshair is drawn, so nothing goes on the wire. Left out: additive glow; a still fallback with Visual effects off is not built (the mockup note says so, remove it or build it). Needs a try in Unity. The Armory and Bestiary are unaffected.
- [x] **Aim line cosmetics.** Built (Nov 2026, protocol unchanged, 147): the ten looks of [`Mockups/aim-lines.html`](Mockups/aim-lines.html) (https://claude.ai/artifact/DqF7ZeYmB5eJkJizFEkubX), under **Aim line** in Your look. Classic is free; the other nine unlock at ranks 5, 11, 14, 17, 21, 26, 29, 34 and 46. Only your own line is drawn, so nothing goes on the wire. Left out: the Ruler's numbers (the arena draws shapes only, no text) and additive glow on Ember, Storm and Prism. Needs a try in Unity. The Armory and Bestiary are unaffected.
- [x] **Main menu VHS hover.** Built (Nov 2026, protocol unchanged, 147): look C (Rewind) of [`Mockups/menu-vhs-hover.html`](Mockups/menu-vhs-hover.html) (https://claude.ai/artifact/PsNUAeijJ2QycuxskywvLi). Hover Play, Join game, Records or Settings for the tape look; click snaps it into focus (0.3 s double image and white flash) until you leave and re-enter (`MenuVhs.cs`). Left out of the mockup: the blur of the focus pull (IMGUI has no blur, the double image stands in). Needs a try in Unity. The Play picker (Back, Continue, New run, Older runs, Delete, and the older-runs list's Continue) has it too (Nov 2026); say if the pause menu should. Also fixed a sometimes-delayed click: the snap's double image drew the button three times on repaint, which shifted IMGUI control ids, so a click right after hover could miss (`Ui.NoHit`).
- [ ] **Spirit Chalk: no bullet block by default, slow fade, Warding Ink card.** Built (Nov 2026, protocol 150 after merging; a card was added and card ids shifted). Ink now lasts 10 s (`ChalkLife`)
      and lets bullets through; the new Rare card **Warding Ink** (Spirit Chalk only; `Upgrades.RequiresWeapon`) makes it destroy bullets, each taking 1.5 s (`ChalkWear`) off the dot it hit.
      Look C (dust) built: the ribbon dims to a ghost and grains lift off it, a spark puff marks each Warding Ink block (`ChalkLook.cs`). From [`Mockups/chalk-fade.html`](Mockups/chalk-fade.html)
      (look C picked). Needs a try in Unity. Armory page for Spirit Chalk is out of date.
- [x] **Run replays with a timeline (F1 → Replays).** Built (Nov 2026, protocol unchanged, 150): timeline look C of [`Mockups/dev-timeline.html`](Mockups/dev-timeline.html) (https://claude.ai/artifact/LFGSc7YWDfNEu29cSi9aPu) and the tab of [`Mockups/dev-recording.html`](Mockups/dev-recording.html) (https://claude.ai/artifact/4jXeaJ9ddAMPZTf4RcNpgV). Record is a saved switch (off by default); replays save to `replays/` as `.replay`; Open / Rename / Delete in the tab; the bar has pause, frame steps, wave jumps, scrubbing and speeds 0.1x to 10x either way. Watch and scrub only: no taking over from a point (the game's random numbers aren't seeded and the host state isn't saved; that would be a second pass). Opens from the main menu only. Needs a try in Unity (the bar covers the bottom HUD; say if you want it smaller or hideable).
- [x] **Play to Continue was too abrupt.** Built (Nov 2026, protocol unchanged): look D (Scan wipe) of [`Mockups/play-transition.html`](Mockups/play-transition.html) (https://claude.ai/artifact/KoZjXL3aurKkt4Dhx6GTaB), slowed to 0.85 s (`MenuPlayLen` in `Menus.cs`). A teal line sweeps left to right from the menu buttons to the picker, and right to left on Back or Esc. No clicks during the sweep; it snaps with reduced effects. Needs a try in Unity.
- [x] **Menu tip box (no more record line).** Built (Nov 2026, protocol unchanged, 147): look C with D's corner outlines of [`Mockups/menu-tip-box.html`](Mockups/menu-tip-box.html)
      (https://claude.ai/artifact/1fYyGEDjRqhw9XfqR2nMxB). The chip under the title is gone, record line and all; a separate box sits under the menu card with the tip's drawing,
      the text, a count and Prev / Next. It changes every 8 s (or at once on a click) and fades out while the Join address row is open (`DrawMenuTipBox`, `Menus.cs`).
      The gap under the title axis is now empty; say if you want the card or title moved up. Needs a try in Unity.
- [x] **Tank gun animations (Scattergun, Flak Cannon, Riot Repeater).** Built (Nov 2026, no protocol change): glowing shots kept, lobby preview matches. Mockup:
      [`Mockups/tank-gun-animations.html`](Mockups/tank-gun-animations.html) (https://claude.ai/artifact/QeTMr1Gr6n1KoJKymyutE3). Needs a try in Unity. Armory pages are now out of date for these three.
- [x] **Pause menu: models show up before the rest of the menu.** Done (Nov 2026): models fade with their panel.

- [x] **HUD panels get the upgrade-card gradient.** Built (Nov 2026, protocol unchanged, 147): `Ui.HudPanel` now shades every HUD panel like `CardArt.Body` (slate when plain, tinted by the accent otherwise). Needs a try in Unity.
- [x] **HUD animations: rim runner.** Built (Nov 2026, protocol unchanged, 147): look D of [`Mockups/hud-animations.html`](Mockups/hud-animations.html) (https://claude.ai/artifact/FtY5EZaHeJZgckp88Pb9SN). Recharging draws the tile's border as a loading ring, a ready tile has a spark circling it, using it snuffs the rim with a burst at the key cap (`DrawTileRim`, `HudTiles.cs`). Reduced effects: the ring only. Needs a try in Unity.

## Balance / design
- [x] **Hangout: scrolling map, PvP and shooting.** Built (Nov 2026, protocol 148): layout B of [`Mockups/hangout-pvp.html`](Mockups/hangout-pvp.html) (https://claude.ai/artifact/Xn5oUe7SyRfa7pNCxk6WLT)
      without the background slider (you dropped it), the PvP checkbox under the minimap, and look A (Grow) of [`Mockups/hangout-transition.html`](Mockups/hangout-transition.html)
      (https://claude.ai/artifact/3FWZesgUXiUERrtzBmmikm). 2400 x 1600 map. Shots use your weapon's rate, speed and reach only: no abilities, pellets, beams or swings. Needs a try in Unity.
      Differences from the mockup: nobody is drawn faded when PvP is off (everyone starts off, so everything would be ghosted); instead health bars show on fighters. Say if you want the faded look.
      Abilities (protocol 149): only the first build's three, on Space or right click with PvP on: DPS Dash, Tank Barrier (4 s, deletes other players' shots, since the Hangout has no enemies), Healer Burst heal (+60 in 230 px, revives at once). Needs a try in Unity.
- [x] **Elementalist DPS abilities: Meteor and Blizzard visuals, Arcane Surge becomes Tsunami.** Built (Nov 2026, protocol 147): look A for all three, from
      [`Mockups/elementalist-dps-abilities.html`](Mockups/elementalist-dps-abilities.html) (https://claude.ai/artifact/R7eis4QntDJvmvq8mpvhb9). Meteor gets a falling
      fireball and a flash/ring/sparks/scorch impact (`FxKind.Meteor`), Blizzard a whiteout of wind-driven snow. **Tsunami** (`Core/Tsunami.cs`): a wave 340 px wide
      along your aim, 12 damage, **wet** for 8 s (14 mastered), slowed 30% (15% on bosses and heavy enemies); every lightning hit (Arc Lightning, Storm Hammer pulses,
      Chain Light, Nimbus) arcs to every wet enemy on the map for 40% of that hit, at most once per 0.4 s per enemy. Mastered: the wave rolls back and wets what it
      missed. Storm Hammer pulses count as half a hammer hit (their own arcs are only an eighth, which would have made wet arcs about 2 damage). Needs a try in Unity.
      Armory and Bestiary pages are out of date for Arcane Surge, Meteor and Blizzard. Say if you want fire (Fireball, Meteor) to dry enemies off.
- [x] **Elementalist tank reworks: Aeroaegis, Frost Nova, Flame Wall.** Built (Nov 2026, protocol unchanged), picks B, A and B of
      [`Mockups/aeroaegis.html`](Mockups/aeroaegis.html) (https://claude.ai/artifact/N3c3iaVGPCbmaTwdkmMbFT),
      [`Mockups/frost-nova-rework.html`](Mockups/frost-nova-rework.html) (https://claude.ai/artifact/EmEnaEkh7bnKo6Y8twYRRS) and
      [`Mockups/flame-wall-rework.html`](Mockups/flame-wall-rework.html) (https://claude.ai/artifact/F2KPGcHLYYcnByVSCiuFEL). The lobby preview
      runs the real game, so it matches. Needs a try in Unity. Armory pages are now out of date for all three.
      - **Aeroaegis** (was Arcane Aegis, `Core/Aegis.cs`, `Game/AeroaegisLook.cs`): the vortex (56 px) goes to the player your cursor is nearest
        (you if no one is closer; downed allies skipped). Enemies are pressed out to its rim and slid round it. I kept the bullet reflect and its
        mastery (4.5 s, double damage), now at the vortex's radius. `AbilityId.ArcaneAegis` is unchanged. Say if bullets should pass instead.
      - **Frost Nova** (`Game/FrostNovaLook.cs`): its Ring effect now lasts 3.3 s so the frosted ground stays; the old ring draws with effects off.
        Left out: the mockup's 0.35 s wind-up (the cast is instant), the ice bursting into shards when it thaws, and a freeze ring that tracks
        other freezes (the ring counts 3 s from when the enemy is first seen frozen).
      - **Flame Wall** (`Game/FlameWallLook.cs`): magma ring; the mockup's fireball arc from the staff and spark bursts on blocked shots are left out
        (the cast is instant; blocked shots flare the rock just before they hit).
- [x] **Ping system.** Built (Nov 2026, protocol 145; `Msg.PingMark`, since `Msg.Ping` was already the latency check). Mockup:
      [`Mockups/ping-system.html`](Mockups/ping-system.html) (https://claude.ai/artifact/UHdFiTLxtxuzmZLQZQbH3Z). I picked the wheel (tap = last pick, hold = wheel),
      the player's slot colour, and a separate key (middle mouse, rebindable). The arena is one fixed screen, so the mockup's off-screen arrows and
      minimap weren't built. Left out for now: muting a player's pings, ping sounds, a Loot mark on the item itself. Say if you want any. Needs a try in Unity.
      Bestiary/Armory pages are unaffected.
- [x] **"Wave cleared" box with awards and a ready system.** Built (Nov 2026, protocol 146): look 1 of [`Mockups/wave-cleared-ready.html`](Mockups/wave-cleared-ready.html)
      (https://claude.ai/artifact/U4U1dESwvCGWizAUC9ivBk): squad tally with an Awards column, the next wave's enemy types and the road to the boss,
      Ready (Y) and an Auto-ready switch (off by default). Everyone ready = a 3 s countdown; everyone on auto-ready = the usual 4 s; otherwise it waits.
      The next-wave panel lists enemy *types* (the counts are random per wave), so no counts like the mockup.
- [x] **In-run text chat box.** Built (Nov 2026, protocol 143), look C (Bubbles) of [`Mockups/chat-box.html`](Mockups/chat-box.html) (https://claude.ai/artifact/KL9YxpHUagLVPpcBNU1tB8).
      Match only; no system lines (joins, revives) yet. Say if you want them or the Hangout to have chat too.
- [x] **Air trail behind fast movers.** Built (Nov 2026, look A Ribbons, protocol unchanged): `AirTrailLooks.cs`, from [`Mockups/air-trail.html`](Mockups/air-trail.html)
      (https://claude.ai/artifact/7n5yGrXprCX62ViuzaxePi). Players, enemies and minions; none under 110 px/s, full at 330. Tune `AirStart`/`AirFull` if it's too shy or too loud.
- [x] **Defend the Crystal should use mostly melee enemies.** Done (Nov 2026): melee, a gun 15% of the time.
- [x] **Revolver and Mender's Bow animations.** Built: look A for both (Revolver "Smoke & brass", Bow "Flex & snap"), Nov 2026
      (`Game/GunBowLooks.cs`). Mockup: [`Mockups/revolver-bow-animations.html`](Mockups/revolver-bow-animations.html)
      (https://claude.ai/artifact/WRUe5JpFtw1zhdMPZtiVY4). Kept from today's look: the cylinder pips and the bow's draw ring and aim cone.
- [ ] **Healer Summoner left-click attacks.** Picks (Nov 2026), mockup [`Mockups/healer-summoner-attacks.html`](Mockups/healer-summoner-attacks.html)
      (https://claude.ai/artifact/V7omfhCLTvzB8i3riqqfA8):
      - [x] **Sprite Staff: Firefly Hop.** Built (protocol 130): a firefly seeds what it hits, then hops (up to 2, 300 px) to the
            nearest enemy that isn't seeded. 7 damage every 0.5 s: ~14 a second on one enemy, ~42 across three.
      - [x] **Treant Rod: Root Lash**, and the tether to the Treant is now a root too. Built: 250 px line, 15 damage every 0.55 s
            (~27 a second), heals allies it touches 10. Homing Rounds widen it. Code names (`Vine*`, `TickVines`) are unchanged.
      - [x] **Totem Staff replaced by Spirit Chalk**, and **Prism Beam replaced by Nimbus** (Healer Elementalist). Built (Nov 2026, protocol 131; smoothed and old code removed at 133)
            from [`Mockups/totem-staff-rework.html`](Mockups/totem-staff-rework.html) (https://claude.ai/artifact/3cp8ynYASdq6fySbRoaeJZ).
            Spirit Chalk also traps: **draw a loop round non-boss enemies and they can't leave it for 3.5 s.** The old weapons'
            leftover code (the Prism Beam's split beams, the totem zone, charge and timer) was removed on request (Nov 2026).

- [x] **Healer Soldier: Haven replaced by the Adrenaline Shot.** Built (Nov 2026, protocol 144): look C (Heartbeat), capstone C (half-length dose to the nearest
      other ally within 250 px), from [`Mockups/adrenaline-shot.html`](Mockups/adrenaline-shot.html) (https://claude.ai/artifact/UmTvWZEiBvZ95Qt1a6pQJL).
      Instant, 14 s cooldown, goes to the player closest to your cursor (you, alone): 8 s of +30% move speed, +40% fire rate, 30% less damage.
      Hallowed Ground became **Long Dose** (+3 s, 2 stacks). Haven's leftovers are still in the code, waiting for your OK to delete: `ZoneKind.Haven` and its
      `ZoneDef` (they keep the zone numbers in place), the revive-boost zone code in `Minions.cs` and `SoloZoneHeal`.

- [x] **Healer rework: Soul Lantern (Summoner) and Deployable Medkit (Soldier).** Built (Nov 2026, protocol 139): Soul Lantern look B
      (Wisp Cage) and Deployable Medkit look A (Field Kit), from [`Mockups/soul-lantern.html`](Mockups/soul-lantern.html)
      (https://claude.ai/artifact/1LBQNmz4VkR48go2Un1xSk) and [`Mockups/deployable-medkit.html`](Mockups/deployable-medkit.html)
      (https://claude.ai/artifact/6mZaE1GtNnM1PsK4YhDRwa). Soul Link left the Soldier; the Medkit took its slot (and enum number).
      Kindred Spirits now speeds the lantern's heal. Medkit orbs use the normal orb magnet (110 px, hurt players only).
      Leftover for you to decide: the Soul Ghost code (`SoulGhost.cs`, `MinionKind.SoulGhost`, `Player.LinkT`/`LinkTo`, the old tether
      drawing) is dormant: nothing casts it. Say the word and I'll remove it. The old zone lantern (`ZoneKind.Lantern`) is dormant too.
      Bestiary/Armory pages still show the old abilities until you ask.

- [x] **Milestone upgrades.** Built (Nov 2026, protocol 141): every 10 levels an extra pick from their own gold pool (Scholar, Second Helping,
      Wide Choice, Reroll Cache, Fortune, Iron Constitution, Quickening), plus the very rare **Milestone Compass** (5% a stack, max 3, a lucky
      milestone pick on any level-up). Mockup: [`Mockups/milestone-upgrades.html`](Mockups/milestone-upgrades.html)
      (https://claude.ai/artifact/G9mB2sSNavM6WPVZvzWXQL). Numbers are placeholders to tune. Milestone picks can't be rerolled, and a lucky one
      uses the same "MILESTONE!" title. Bestiary/Armory pages don't list these yet.
- [x] **Level-up cards redrawn** like the milestone cards (rarity chip on top, cutout icon tile, stack circles, rarity glow and gradient body, slow bob,
      the pick preview on the card), two-click Reroll, uppercase REROLL / CHOOSE UPGRADE. Mockup: [`Mockups/upgrade-card-layout.html`](Mockups/upgrade-card-layout.html).

## Visuals
- [x] **Tank Soldier Fortress dome.** Built (Nov 2026, protocol 146): look B, Bulwark wall (`Game/DomeLook.cs`, `Core/Dome.cs`), 10 s (14 s mastered), no taunt or damage cut. Only players and player bullets cross; enemies (bosses too) and their shots are held out, and nothing hostile damages a player inside. Mockup: [`Mockups/fortress-dome.html`](Mockups/fortress-dome.html) (https://claude.ai/artifact/VapgPykPS23u6D1UnMjHvv). Bestiary/Armory pages still show the old Fortress until you ask.
- [x] **Profile picker <-> main menu transition, with sound.** Built (Nov 2026, no protocol bump): look C, Plot points, at half speed
      (`Game/ProfileTransition.cs`), from [`Mockups/profile-transitions.html`](Mockups/profile-transitions.html)
      (https://claude.ai/artifact/3pyKAd8sSKGrPhRcXE7tND). Max 3 profiles; the picker always shows four boxes (empty slots say "Empty",
      New profile grays out when full). It replaces the menu's plotted intro after a pick (the intro now runs inside the move).
- [x] **Healer Soldier Mend Pulse animations.** Built (Nov 2026): look B, Heartbeat (`Game/MendPulseLook.cs`, no protocol bump), from
      [`Mockups/mend-pulse.html`](Mockups/mend-pulse.html) (https://claude.ai/artifact/4RHDSKaLNNGErr8MhprTWx). Left out: the capstone's bullet
      pops (bullets are erased host-side with no event the client sees).
- [x] **Tank Soldier Roar and Bulldoze animations.** Built (Nov 2026): the "Proposed" side of [`Mockups/tank-abilities.html`](Mockups/tank-abilities.html)
      (https://claude.ai/artifact/NosUu8ukpJzDsBes8FiAYM), `Game/TankLooks.cs`, no protocol bump (protocol 142). Timed from each cast's `FxKind.Callout`,
      so nothing is synced; the old plain rings are skipped with Visual effects on. Differences from the mockup: no windup before the cast (the
      ability fires instantly, so Roar's pull-in is 0.08 s, and Bulldoze's lane shows as it starts), enemies don't turn to face you, no hit-stop
      on other players' casts, and an enemy a Bulldoze kills is gone before the charge reaches it, so it shows no contact sparks.
      Preview: `CAST=Roar CASTT=0.25 Tools/Preview/shot.sh x hud 6 9 2`, `CAST=Bulldoze CASTT=0.2 ...`, or `LOADOUT=0,0,0,1 ABILITY=1 SHOWT=1.55 shot.sh x lobby`.
- [x] **Tank Soldier Barrier animations.** Built (Nov 2026): look C, Bulwark Plates (`Game/BarrierLook.cs`, no protocol bump). Reflect tint left out (host-only flag). Mockup: [`Mockups/barrier-animations.html`](Mockups/barrier-animations.html)
      (https://claude.ai/artifact/CpWoeYAWoSirm4mkf7XHx4): A today, B Force Field (hex dome), C Bulwark Plates (8 plates,
      one knocked off per boss hit), D Soap Bubble. Drawing only; a reflect-specific tint would need `BarrierReflect`
      replicated (protocol bump).
- [x] **Arcane Missiles reworked as Ice Shards.** Built (Nov 2026), look A from [`Mockups/ice-shards.html`](Mockups/ice-shards.html)
      (https://claude.ai/artifact/6D2aFuh1WxeedCRGpd7iSx). Same mechanics (orbit, launch, homing); renamed, frost-blue kites that
      shatter on a hit. Internal ids (`WeaponId.ArcaneMissiles`, `Defs.ArcaneShot`) unchanged; no protocol change. The Armory
      page still says Arcane Missiles until you ask for it to be updated.
- [x] **Radiant Orb trail.** Built (Nov 2026): style B, Stardust (`Game/RadiantOrbLooks.cs`), from
      [`Mockups/radiant-orb-trails.html`](Mockups/radiant-orb-trails.html). Light Orbs get it at half size. Drawing only.
- [x] **Arc Lightning looks.** Built (Nov 2026): style A, Thunderhead (`Game/ArcLightningLooks.cs`), in the arena and the
      lobby preview, from [`Mockups/arc-lightning.html`](Mockups/arc-lightning.html).
- [x] **Chain Light looks.** Built (Nov 2026): style A, Radiance (`Game/ChainLightLooks.cs`), in the arena and the
      lobby preview, from [`Mockups/chain-light.html`](Mockups/chain-light.html).
- [x] **Soldier DPS ability visuals: Dash, Cluster Grenade, Mark for Death.** Built (Nov 2026, no protocol change,
      `Game/SoldierLooks.cs`) from [`Mockups/soldier-dps-abilities.html`](Mockups/soldier-dps-abilities.html)
      (https://claude.ai/artifact/5ZoTSdqs2X2kqCzhZwdRRo): Dash B (Comet Streak), Grenade A (Frag Burst), Mark A (Reticle Lock)
      with B's skull over it. Left out for now: the dash's squashed body, and other players' dash streaks (only your own dash
      has ever been replicated); the shards are for looks (the real shrapnel's angles are the host's).
- [x] **Character models for every role and class (18), with weapon models.** Built (Nov 2026) from
      [`Mockups/character-models.html`](Mockups/character-models.html), colour-by-class (`Game/CharacterModels.cs`),
      plus a bigger role-coloured glow on the floor round every player (`Juice.cs`, `GlowPlayer`). The weapon models are
      a first pass: redraw any of them in `DrawHeld`.
- [x] **Melee attack animations.** Built (Nov 2026): style E, the weapon models with the energy arc
      (`Game/MeleeLooks.cs`), from [`Mockups/melee-attacks.html`](Mockups/melee-attacks.html).
- [x] **Repulsion Cannon visuals.** Built: look D, the cannon blast (`RepulseLook.cs`), Nov 2026. Mockup: [`Mockups/repulsion-cannon.html`](Mockups/repulsion-cannon.html)
      (https://claude.ai/artifact/QutT2bQZmuigHQpx9cX5h3): today's look and four options, A Shockwave, B Light
      burst, C Force ripples (bends the floor grid), D Cannon blast (smoke, sparks, small kick), at the real 70° / 240 px
      cone, with optional hit feedback (flash, slide streak, orb). Drawing only (`DrawRepulseFx`), no protocol change.
      Not built from D: the dust puffs behind hit enemies (needs hit positions in the effect).
- [x] **Frost Lance visuals.** Built: look A, the crystal spear (`FrostLook.cs`), Nov 2026. Mockup: [`Mockups/frost-lance.html`](Mockups/frost-lance.html)
      (https://claude.ai/artifact/3iseS4yjtmVDriXUxqJoxF): today's look and four options, A Crystal spear, B Frost
      growth, C Glacier, D Aurora lance, at the real 230 px reach and 0.15 s splinter delay, with optional frost on
      hit enemies. Drawing only (`DrawIcicleFx` / `DrawSplinterFx`), no protocol change. Not built from A: the facet
      sparks on hit enemies (needs a per-enemy hit timer).
- [x] **Redesign the main menu buttons.** Built: design D with the pen plot intro (Nov 2026).
      Mockups done, waiting for a pick: four layouts in [`Mockups/menu-redesign.html`](Mockups/menu-redesign.html)
      (https://claude.ai/artifact/6T2r1GZSBsajVK8pxaAo8s), and three plotted intros for the menu in
      [`Mockups/menu-intro.html`](Mockups/menu-intro.html) (https://claude.ai/artifact/TdwEQNxJ8STpVk2MRPqLuk).
      Owner likes **D (Big play)**: its intros and the Host game → lobby move are in
      [`Mockups/menu-d.html`](Mockups/menu-d.html) (https://claude.ai/artifact/VskdxmJhgTzmpeUnjjM2R5). With D the
      port moves to Settings (Network, 7777 by default) and the name is edited on the bottom-left chip.
- [x] **Knockback visual effect.** Done (Nov 2026): pushes slide (0.18 s) with a streak, dust and an impact puff.
- [x] **Load-in animations for the How to Play and Field guide windows.** Done (Nov 2026): How to play slides in;
      the Field guide rises in.

## Rank (cosmetic account rank)
Design doc: [`Rank.md`](Rank.md); mockups: [`Mockups/rank-screens.html`](Mockups/rank-screens.html)
(https://claude.ai/artifact/6jPzqJeT4sFbfWhm1Ewkf9). **Badge style B (shield) picked.** XP from every run with a
bonus for harder modifiers; ranks unlock cosmetics only (titles, trails, frames, menu backgrounds).
- [x] **Built** (Nov 2026, protocol 135): `Rank.cs` + profile XP + `rank` tests, the results strip, the lobby badge and
      frame, the menu chip, Records > Rank, the Ember and Gold stardust trails, the Steel and Gold frames, and the Dusk
      Grid background. See "As built" in the doc.
- [x] **Light cycle (Tron) trail.** Built (Nov 2026, protocol 139): Grid wake with Derezz's breakup, cyan only, rank 45 (`Game/LightCycleTrail.cs`); the orange and magenta equip options were not built. Mockup (Nov 2026): [`Mockups/tron-trail.html`](Mockups/tron-trail.html)
      (https://claude.ai/artifact/Ctzk9h9coiwQf4NvEY48hN). A wall of light that snaps to right-angle turns, in four styles
      (A Light wall, B Derezz, C Grid wake, D Twin rail) and three colours (cyan, orange, magenta), proposed at rank 45. Pick a
      style (my pick: B) and whether the colours are equip options.
- [x] **More rank frames (borders).** Built (Nov 2026, protocol 138): all five, drawn by `Game/FrameLooks.cs` on the party card, the menu chip and the cosmetics look card. Mockup (Nov 2026): [`Mockups/rank-frames.html`](Mockups/rank-frames.html)
      (https://claude.ai/artifact/CRkVFKgeCQEWGGFahRY3c3). Today's Steel (12) and Gold (30) plus five new animated ones:
      Circuit 16, Ember Edge 20, Void Pulse 24, Legend Foil 40, Prism 50. Frames are numbered in rank order in the lobby (Gold 2 to 5, hence the bump); with Visual effects off they fall back to a static rim.
- [x] **Lobby Hangout.** Built (Nov 2026, protocol 137): a Hangout tab in the lobby where every player's character walks around
      with WASD, wearing their trail, frame and rank (`Game/LobbyRoom.cs`, `Core/LobbyAvatars.cs`, `Docs/Hangout.md`). No mockup
      first, at the owner's request. Left for later: emotes, a ready pad, a weapon preview spot.
- [x] **More rank trails.** Built (Nov 2026, protocol 136): Frost 14, Storm 18, Void 22 and Prism 35, from
      [`Mockups/rank-trails.html`](Mockups/rank-trails.html) (https://claude.ai/artifact/L5DppgBwZDXzgAaPo2cf2o). Drawing only
      (`TrailPiece` in `Game/RankTrails.cs`, shared with the cosmetics window samples). Trails are numbered in rank order in the
      lobby, so Gold went from 2 to 5, hence the bump. The reward ladder panel goes to three columns. Armory and Bestiary untouched. Left for later: tuning the XP numbers from real play.

## Unlocks & ascensions plan
Two layers that don't compete: **unlocks give new items, ascensions deepen the ones you have.** Both use the unused
unlock system in `Profile.cs` (`Unlocks.All`) and the stats Records already keep.

**Unlock rules (weapons and abilities)**
- Earned by playing that class, ideally hinting at what the item is good at. Takes 2-5 runs, not a grind.
- Can't need the item itself. Per player in co-op (like Records). Today's weapons/abilities stay unlocked.
- Built from a few **standard condition pieces**, themed per item: play class/role · reach wave N · beat boss X ·
  win hunt X (later) · with item Y equipped · simple totals. Build the pieces once; each new unlock is just data.
- Truly unique conditions only for a few showcase/secret unlocks.
- Locked items show their condition and progress in the lobby and Records.
- First one: **Missile Pod**, a 4th DPS Soldier weapon. Hold fire and sweep to lock up to 4 targets (~0.3 s each),
  release to fire homing missiles with small blasts; a tap with no locks fires one unguided rocket. Upgrade: +1 lock
  or bomblets. Weak up close. Unlock: **beat the Hive Queen as a DPS Soldier**. Mockup first.

**Ascensions (per weapon, 3 tiers; the lobby strip is already there as a placeholder, `LobbyUi.DrawAscensionStrip`)**
- Earned only by using that weapon: I = reach wave 10 with it · II = beat 3 different bosses with it · III = win a
  full run or 3 different hunts with it.
- Rewards are **twists, not raw power**; each tier is a variant you can switch on, the base stays available.
  Example (Twin Rifle): I = new tracer/muzzle look · II = streams cross closer, crossfire hits harder ·
  III = crossfire hits mark enemies for Mark for Death.
- Links between systems, kept few: Boss Hunts count toward Ascension III; at most **one unlock per class** comes from
  an ascension (e.g. Twin Rifle II unlocks the Missile Pod); perks stay separate (they come from bosses).
- Open: each weapon's actual ascension variants need designing (a design doc or mockups before building).

**Modifier unlocks** (same standard condition pieces, no new system)
- **Open from the start:** the Easier ones (Lucky, Head Start, Practiced, Second Wind) plus a couple of simple
  Harder ones (Tough Crowd, Swift Foes). Easier ones help struggling players, who have unlocked the least.
- **Unlocked by playing:** the rest of the Harder/Risky ones, each tied to something that fits its theme. Examples:
  - Nightfall: reach wave 20.
  - Death Throes: beat the Hive Queen.
  - Hunted: win a co-op run.
  - Zombie Infection: beat the Lich.
- In co-op the **host's** unlocks decide what's available.
- Locked modifiers show their condition in the Modifiers tab.
- **Badges:** a Records badge for each modifier you've won a run with, so the open ones also get tried.

- [x] **Sort modifiers by tier.** Done (Nov 2026): Easier / Risky / Harder headings in one grid.

## Difficulty with twists
Today the presets only change numbers (README "Difficulty"), so Nightmare plays like Normal, just harder. Keep the
numbers, but each step up also **adds named rules**, shown on the difficulty card.

- **Hard** adds:
  - **Smarter enemies:** snipers lead their shots, chargers fake out, melee enemies flank instead of beelining.
  - **Elite affixes:** each elite gets one trait (shielded, splits on death, leaves a hazard trail...).
- **Nightmare** adds Hard's twists, plus:
  - **Boss upgrades:** every boss gets one extra move per stage (e.g. the Warden's searchlight splits in the last
    stand, the Hive Queen's eggs hatch faster). Most work (10 bosses), most memorable.
  - Longer last stands; health orbs fade after a few seconds.
- **Ladder past Nightmare** (name it **Threat** or **Heat**, not "ascension", which is for weapons): beat a Nightmare
  run to unlock 10 rungs, each adding one stacking rule (enemies drop a hazard on death · slower revives and the
  reviver can't shoot · mimic chests · a forced Harder modifier · bosses get an extra stage · ...). Each rung won is
  a record.
- **Ties in:**
  - "Win on Hard" / "win at Threat N" become standard unlock pieces.
  - A hunt's "Hard" (perk #2) uses these twists.
  - The twists become switches in the Custom panel.
- **Build first:** the Hard twists (smarter AI + elite affixes). They're in every wave, so they change the feel
  fastest, and need no per-boss work.

## Online play without Tailscale: relay with room codes (not urgent)
So friends can play without Tailscale or port forwarding.
- A small **relay** program on a cheap server (~$4-6/month, e.g. Hetzner or DigitalOcean) that just passes messages
  between the host and the joiners. The host's game still runs the match.
- In the menu: **Host online** gives a short room code (e.g. `KJ4P`); **Join online** takes the code. Nobody
  installs anything extra.
- Fits the current setup: networking is plain TCP (`Assets/Scripts/Core/Net.cs`, `Session.cs`) and the core game
  doesn't depend on Unity, so the relay can be plain C#. Test it like the rest of multiplayer (SimTest drives real
  sessions over localhost); add a relay check.
- Deliverables: the relay program, the two menu buttons and code entry, a version check through the relay, and
  step-by-step server setup instructions (rent, install, run as a service) in the README.
- Until then, a no-code stopgap: the host runs a free tunnel app like playit.gg and friends join its address + port.

## Bigger features (design docs ready)
- [x] **Zombie enemies for Zombie Infection** (built, with the dark green arena). Mockup: `Docs/Mockups/zombie-enemies.html` (published:
      https://claude.ai/artifact/9Dvd5zEz49owzvonTP3hnb). Four green zombies with green particle effects: Shambler
      (wide triangle, slow melee), Runner (narrow long triangle, fast melee, packs of 3), Spitter (pentagon, lobs globs
      that leave acid puddles), Crawler (trapezoid, grabs and roots the player), Bloater (swelling circle, bursts into a slowing cloud), Shrieker
      (diamond, screams to frenzy the horde), Mender (cross, heals and raises the dead faster). Waiting for the owner's go-ahead or
      changes; then build them as new EnemyTypes replacing the stand-ins.
- [x] **Wave editor** (option B, timeline) in F1 → Tools. Generation script done (`Tools/WaveGen/build.sh`, `Data/Waves`). Next: let
      hunts/approaches/paths use designed waves (the Warden Hunt's nine first).
- [x] Zombie Infection modifier, basic version (hordes, cap, the dead rise). Next: zombie looks/types, infected
      co-op players: [`ZombieInfection.md`](ZombieInfection.md)
- [ ] **Riot Gear, the first perk (and the perk framework).** Built (Nov 2026, protocol 154; plan in [`RiotGear.md`](RiotGear.md)): the lobby's Perks tab (look A of
      [`Mockups/riot-gear-perk-menu.html`](Mockups/riot-gear-perk-menu.html), https://claude.ai/artifact/WrbA5TsYMgcn6tfe7Rd8ad), the Plate look and HUD chip (look 2 of
      [`Mockups/riot-gear-effects.html`](Mockups/riot-gear-effects.html), https://claude.ai/artifact/241kgcxDtwMbJVEkRijuuG), perks synced through the lobby, the match start,
      the snapshot and saved runs. Blocks one hit, then a 30 s recharge, 0.5 s of invulnerability after a block; burn, gas, acid and lava ticks go through. Needs a try in Unity:
      the Perks tab (clicks, hover tips, seat icons), the plate's look and timing, the HUD chip's place above the XP strip, and how a block feels. The Armory and Bestiary are not
      updated. Replay files are now format 2 (older replays no longer open). Left for later: Contraband and the other bosses' perks, the second/third perk unlocks per boss, the
      no-perks record variant, and a "perks used" line on the run records. Say if reviving should refill the shield (it doesn't now: the timer runs on).
- [ ] **Contraband, the second Warden perk.** Built (Nov 2026, protocol unchanged, 154; [`Contraband.md`](Contraband.md)): win the Warden Hunt on Hard or Nightmare to unlock it. The first wave after each boss
      (6, 11, 16...) always has a treasure chest when anyone in the party has it, and your own item from any chest is rare or better. Needs a try in Unity (the chest turning up, the Perks tab card).
      Left out: a marker or call-out for the cycle chest (say if you want one). The Armory and Bestiary are not updated.
- [x] Boss Hunt for the Warden (10 waves, its own enemies). Next: factions + starting builds for later bosses, then Perks:
      [`BossHuntsAndPerks.md`](BossHuntsAndPerks.md)

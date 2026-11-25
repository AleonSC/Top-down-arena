# Needle Swarm: the DPS Hemomancer's 1st weapon

**Status: built** (protocol 509). It replaced Blood Bolts (`WeaponId.BloodBolts` renamed `NeedleSwarm`, same slot). Logic in
`Assets/Scripts/Core/NeedleSwarm.cs`, look in `Assets/Scripts/Game/NeedleSwarmLook.cs`, test `Tools/SimTest/run.sh 1 30 needleswarm`,
preview `NEEDLESWARM=<s held> [NSALT=<s>] ZOOM=2 CAMAT=800,470 Tools/Preview/shot.sh x hud 1 6 1`. Mockup `Docs/Mockups/needle-swarm.html`
(published: https://claude.ai/artifact/JpcTsJuQBbbfV7MhXEW3EL).

## Owner's decisions

- A LOT of blood needles in a thick swarm that circles you fluidly; it should look and move exactly like the mockup.
- Lost needles are siphoned back out of you, and that costs health.
- Needles block enemy bullets and break doing it.
- Near an enemy, the swarm slowly grows a tendril to it, wraps it and keeps cutting until it dies.
- A weapon, not an ability: Fire (held) aims the tendril at the enemy nearest the cursor; the ring and the siphon are always on.
- Alt: a fan of needles thrown out of the swarm (Needle burst).

## How it's built

- The host keeps only numbers: the needle count, each tendril's enemy, length and wrapped needles (`Player.NsCount`, `NsTen`, `NsLen`,
  `NsBound`, replicated). Every screen flies the needles itself with the mockup's springs from those numbers, so 160+ needles cost
  nothing on the wire. A broken needle or the Alt's fan is an `FxKind.NeedleSwarm`, so the look takes the right needles away.
- Blocking: an enemy shot coming in through the ring (72 px) stops; under 35% of a full swarm only some do (`NsBlocks`).
- One tendril grows at a time; the next starts once the last has wrapped its enemy, up to three.
- Its wraps keep the Hemomancer's bleed burst (Blood Bolts' Hemophilia card now belongs to the Needle Swarm).

## Numbers

160 needles (+20 per extra projectile), siphoned at 32/s for 0.14% max health each, none under 15% health; tendril 140 px/s to 260 px,
pulled back 3x as fast when let go; up to 30 wrapped needles each (45% of the swarm between all three), 4 damage/s each, 50% slow, a
bleed stack every 0.5 s; Alt 24 needles (+4 per extra projectile), 14 each, 2.5 s cooldown. Lobby DPS 140 (Blood Bolts was 121).

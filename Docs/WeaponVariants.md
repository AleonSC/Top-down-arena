# Weapon variants: design plan

Status: **plan, nothing built yet.** Decisions below were made with the owner (November 2026); the variants and
numbers are first guesses to tune in play-testing, the Boss Lab and the `weapondps` report.

## The idea

Each weapon gets **three variants**: different ways to play it, each with a trade-off, unlocked by going far in a
run **with that weapon**. In the lobby you pick a weapon and then which version of it to take: the original or any
variant you've unlocked. They give you new things to try and a reason to come back, without making the base weapons
obsolete.

## Decisions (owner)

| Question | Decision |
|---|---|
| Upgrades or sidegrades? | **Sidegrades.** A variant plays differently and gives something up; it isn't a stronger version. Boss tuning, co-op balance and the base weapons all stay meaningful. |
| How are they unlocked? | **Milestones inside one run** with the weapon (not one win per variant: 3 variants × 54 weapons × ~45 min a run would be far too long). |
| How many per weapon? | **Three.** |
| Start with all 54 weapons? | **No: a pilot of 6** (one per class, two per role), then expand what plays well. |

## Unlocking

Play a run with the weapon and pass its milestones; each one unlocks the next variant:

| Milestone (with this weapon) | Unlocks |
|---|---|
| Beat the **wave-15 boss** (the Twin Reapers) | Variant 1 |
| Beat the **wave-30 boss** (the Duelist) | Variant 2 |
| **Win the run** (beat the Eclipse, wave 50) | Variant 3 |

- One great run unlocks all three; a run lost at wave 34 still unlocks two. Progress is your **best wave with each
  weapon**, kept in your profile, so a later run picks up where the best one left off.
- **Resumed runs count** (saves are deleted when a run is lost, so a milestone still has to be earned).
- **Each player unlocks for their own weapon** in co-op, on their own computer (like Records).
- Practice Range, dev tools and the bot don't count (the profile's existing rules).
- Variants of a weapon count as that weapon: winning with Explosive Arrows still counts for the Mender's Bow.
- A toast when it happens: "Unlocked: Explosive Arrows (Mender's Bow)", and the Records page lists your unlocks.

## In the lobby

Under the three weapon buttons, the picked weapon shows **four chips**: the original and its three variants.
Unlocked ones can be picked; locked ones show a lock and their milestone, with your best so far
("Beat the Duelist with the Mender's Bow · best: wave 24"). The spec sheet, hero card stats (Damage / s), live
preview and Field guide all follow the picked variant.

## The pilot: six weapons

Two per role, one per class.

### Mender's Bow (Soldier Healer)
Today: hold to draw; a full draw hits for 40, pierces 2 and heals an ally it passes for 20; a perfect release +25%.

1. **Explosive Arrows:** a full draw bursts on the first enemy it hits (30 to everything within 60 px) instead of
   piercing; it no longer heals as it passes, but allies inside the burst heal 12.
2. **Volley Bow:** no draw: each shot is 3 arrows in a 12° fan (14 each, heal 6 each), every 0.55 s. Steadier,
   lower peak, no perfect shots.
3. **Lifeline Bow:** an arrow that hits an ally links you for 5 s: 30% of the damage you deal heals them. Arrows
   hit for 30 and pierce 1.

### Flamethrower (Elementalist Tank)
Today: a short stream: 3 to the first enemy, 1 to the rest, burning 5 a second for 3 s.

1. **Napalm:** shorter reach; flames leave patches of burning ground (2 s, 6 a second) instead of passing through.
2. **Blue Flame:** a narrow jet with 60% more reach that hits for 6 and doesn't burn: strong on one target.
3. **Backdraft:** hold to build heat (up to 1.5 s), let go for a cone blast (up to 60) that also burns away enemy
   bullets in the cone.

### Wolf Whistle (Summoner DPS)
Today: darts mark prey and two wolves hunt it; all wolves biting within a second is a takedown (20 each, 0.6 s stun).

1. **Alpha:** one big wolf (twice the health) instead of two; its takedown hits for 50 and stuns for 1 s.
2. **Hunting Horn:** marks up to 3 prey; the wolves split up; no takedowns, but bites do 1.5× to marked enemies.
3. **Pack Leader:** +10% move speed for each wolf alive, and each takedown heals you 10; the wolves bite 20% softer.

### Lance (Knight Tank)
Today: a heavy jab that shoves enemies far back and stuns for 1 s.

1. **Jousting Lance:** hold to charge forward (up to 220 px); the jab on arrival hits twice as hard.
2. **Pike Wall:** hold to plant it: a 180 px line in front of you stops small bullets and pokes what walks in
   (12 every 0.3 s), but you move at half speed.
3. **Thunder Lance:** each jab also throws a bolt 300 px (24) that chains to 2 more enemies; much less knockback.

### Katana (Ninja DPS)
Today: fast 110° slashes; sheathed (0.6 s), the next cut is a 220 px quick-draw through everything for 2.5×.

1. **Nodachi:** slower (0.45 s) but wider (140°) and longer; the quick-draw does 3× but takes 1 s to sheathe.
2. **Twin Katana:** alternating 13-damage slashes every 0.2 s, no quick-draw; every 5th hit dashes you 60 px.
3. **Moonlit Blade:** the quick-draw also sends a crescent 300 px (60% damage); it takes 0.8 s to sheathe.

### Sanguine Orb (Hemomancer Healer)
Today: homing orbs (16 plus a bleed stack) charge a heal reserve; with 25 reserve an orb coats a hurt ally (30 over 4 s).

1. **Blood Moon:** one big, slow orb that pierces (40, two bleed stacks); the reserve fills faster and a coat heals 50.
2. **Leech Swarm:** three small orbs (6 each) that circle you for 2 s before they seek; a coat heals you as well.
3. **Blood Mirror:** a coat is a 25-health shield instead of healing.

## Building it, step by step

Each step: code, sim tests, previews, `weapondps` re-run, README / Armory / Field guide, then push and a short report.

1. **Framework.** Variants as weapon entries that name their base weapon (they share its lobby place and its
   Space abilities); the lobby's variant chips with locks and milestones; `LobbyEntry.Variant` (a protocol bump);
   per-weapon best wave and unlocks in `Profile` (and the Records page); the unlock toast; an F1 "unlock all
   variants" switch for testing. No variants yet: everything still plays as today.
2. **Mender's Bow** variants.
3. **Flamethrower** variants.
4. **Wolf Whistle** variants.
5. **Lance** variants.
6. **Katana** variants.
7. **Sanguine Orb** variants.
8. Play-test, retune, then decide which weapons get variants next.

## Open questions

- Should the milestones be different bosses (e.g. the wave-10 and wave-25 bosses, for earlier unlocks)?
- Do variants need their own look (a tinted weapon, different shots), or only their own behaviour?
- Should abilities get variants the same way later?

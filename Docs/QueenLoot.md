# The Hive Queen's legendary cards

Status: **plan**. Two cards agreed (Hive Drones, Royal Dive); the third is open (see the end).

## Why

The Warden's three legendaries each hand you one of **his** fight's mechanics, with its look: his riot guard
(Warden's Guard, a companion), his stun gun (Stun Gun, a new ability), his searchlight (Searchlight). The Queen's
are generic stats with a bee name: Swarm Rounds (shots split), Royal Jelly (heal per hit), Brood Mother (gun
turrets). Her fight has plenty to give instead: drones that shield her and fly home to heal her, bombers, eggs,
sealing honeycomb cells, a nectar spray leaving honey, the swarm cloud and the royal dive. Her look code
(`QueenLook.cs`), honey patches (`PatchKind.Honey`) and the dive already exist to build on.

The owner ruled out another cursor-following effect (Searchlight is the Warden's).

## Hive Drones (companion; replaces Brood Mother)

Two of her drones switch sides and circle you.

- **Guard:** they circle you 45 px out and each soaks enemy bullets that reach it (like Warden's Guard and the
  Guardian Spirit: a small block, 60 damage; boss bullets wear it down; a worn-out drone is dazed for 4 s).
- **Sting:** when you kill an enemy, an idle drone flies to the nearest enemy within 300 px of the kill and stings
  it three times (15 damage each, 0.3 s apart, your damage upgrades count), then flies back.
- **Home:** a drone that gets back to you heals you 4 health (her drones fly home and heal her; Royal Jelly's
  healing lives on here).
- **Stacks (up to 3):** +1 drone each (2, 3, 4).
- **Bosses:** hitting a boss also sends one idle drone every 4 s, so it isn't kills-only.
- Built as a new `MinionKind.HiveDrone` (her drone's look, small), moving like a sent Wisp Lantern wisp
  (`MoveSent`) and guarding like a Warden's Guard.

## Royal Dive (new ability; replaces Royal Jelly)

Her royal dive, yours: a bonus ability on Q/E/Shift, like the Stun Gun. Cooldown 12 s.

- **Leap:** you rise for 0.6 s (can't be hit, can't shoot), your shadow racing to your cursor (up to 380 px).
- **Crash:** you land there: 120 damage (your damage upgrades count) to everything within 110 px, enemies
  knocked 100 px out (not bosses), and enemy bullets in the ring erased.
- **Honey:** the landing leaves a 100 px honey pool for 4 s: enemies in it are slowed 40%; allies in it heal
  6 health a second (Royal Jelly's healing, as a place to stand).
- **Mastery capstone (level 5):** two pools' worth: a 150 px pool that lasts 6 s.
- Built on the Queen's dive (`Queen.cs`: shadow, rise, land) and `PatchKind.Honey` puddles (they already slow;
  healing allies is new). Movement goes through the same path as Shadow Step / Substitution (`Msg.Teleport`) so
  clients land where the host does.

## Both

- Icons in `PixelArt.cs` (a drone; a crown over a landing ring), card text, README, Armory, Bestiary note.
- Balance: measured with `run.sh 1 1 balance` (add both) against the legendary target (+40-60% in their best
  fight, at least +15% on a boss); Royal Dive also by its damage per cooldown next to the Stun Gun.
- Tests: drones guard, sting after a kill and heal on return; the dive can't be hit in the air, lands at the
  cursor (also on the client), hurts and knocks back, and the pool slows enemies and heals allies.
- Old saves that took Brood Mother or Royal Jelly load them as Hive Drones / Royal Dive.
- Protocol bump (new minion kind, ability, upgrades).

## Open: the third card

Options, all from her fight and none following the cursor:

1. **Keep Swarm Rounds** (recommended): it's already bee-themed ("the swarm bites") and was just balanced.
2. **Egg Clutch:** enemies you kill can leave one of her eggs; it hatches after 2 s into two little bees that hunt
   for 6 s.
3. **Bomber Escort:** every 6 s one of her bombers flies from you to the toughest enemy within 400 px and
   bursts on it.
4. **Honeycomb Shell:** every 12 s a honeycomb shell closes round you and soaks 40 damage; when it breaks, it
   bursts into a honey pool that slows the enemies around you.

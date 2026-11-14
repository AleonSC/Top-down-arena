# The Hive Queen's faction

**Status: built** (protocol 318). Code: `Core/HiveKin.cs`, `Core/Approaches.cs` (the Hive `FactionDef`), `Core/Defs.cs`, `Core/WardenFaction.cs`
(the Lobber's honey, the Stinger's dart), `Game/HiveLook.cs`, `Game/GasLobLook.cs`; mockup `Docs/Mockups/hive-faction.html`; test suite `hive`;
preview `HIVE=0.4 [HIVELOB=1.8] Tools/Preview/shot.sh x hud 1`.

Eight enemies, one for each of the Warden's roles, so the Hive's ten-wave cycle (waves 11-20 of the 100-wave Campaign) teaches in the chart's order
(`Docs/Approaches.md`).

| Hive | Plays like | Difference |
|---|---|---|
| Worker | Rioter | +10% speed per Worker within 110 px (max +50%) |
| Soldier | Guard | inside 260 px it charges: 30% faster, half damage (`Enemy.Phase` 1) |
| Comb Shield | Riot Shield | seven wax cells instead of a slab; two break per wear step; 15% of a shot gets through, +15% per crack |
| Honey Lobber | Gas Grenadier | honey puddle (slows 45%, no damage) instead of gas |
| Stinger | Taser Guard | 105 health (x1.5); its own bullet kind (`StingBolt`), a lime barbed dart; same stun and parry |
| Needler | Security Guard | 70 health (x1.5); strafes harder (1.2 vs 0.7 sideways pull, x1.4 while locking on) |
| Bumble | Thug (Brute) | none |
| Hornet | Shiv (Lunger) | none |

## How

- A Hive enemy is its own `EnemyType` with a `Def` of its counterpart's numbers. Its behaviour is its brain, built from the same parts as its
  counterpart's (Docs/EnemyBehaviours.md; the old kin table is gone); sounds, thrall attacks and lanes follow its role (`Game.SharesWith`).
  Looks and the Bestiary are the Hive's own.
- The faction (`Approaches.All`, boss `HiveQueen`): members with the Warden's weights and fade order, squads Swarm, Nursery, Picket, Honeycomb, the finale
  Royal guard, lesson-only squads Worker pack and Soldier pair, and ten lessons in the Warden's order (Soldier 2, Bumble 3, Needler 4, recap 5, Comb Shield 6,
  Honey Lobber 7, Stinger 8, every squad 9, exam 10: the Royal guard and a big Swarm).
- The faction also themes waves 6-9 of the 50-wave track, Quick Play (when the Queen is the boss) and fades after her like the Warden's.
- The look is a direct port of the mockup's insect renderer (`HiveLook.cs`): the mockup's sizes and colours as they are, scaled so a bee is about 1.45
  radii from the middle to the head. The glow is a wider, fainter stroke under each line.

## Notes

- A comeback squad (`Approaches.Comeback`) is now picked first in a wave, so its types count against the wave's budget of distinct types before the
  factions pick theirs (the Hive's squads would have broken the cap on wave 14).
- Open: the Hive Queen's own Drones, Bombers and Eggs stay hers (they come from her fight, not from waves); balance numbers are the Warden's, untested.

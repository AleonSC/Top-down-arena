# Room modifiers (room kits)

Status: **built** (protocol 254). Owner's idea: change an existing room into a path floor or a shop.

- A **room kit** (`RoomToken`: Shop, or Floor + a path) is a team-held one-use token (max 6, `Expansion.MaxTokens`), synced in `Msg.ExpandState`.
- **Sources** (decided): a cleared boss wave gives a Floor kit (`Expansion.FloorToken`, seeded, prefers floors not on the map), a won challenge
  room gives a Shop kit, the vendor sells a Floor kit (`ShopAction.RoomKit`, 200 gold).
- **Spending**: only in an open building round, from the build window's Room kits screen (`Msg.ExpandApply`: kit index, room index; the host
  checks `Expansion.CanRetype`). One change per room, permanent, Combat rooms only.
- The dormant Shop/Floor rules (`FloorAt`, `FloorBonusAt`, shop-room gold) start working in a room once its kind changes.
- Not done: a flash when a room changes, a Gatekeeper-room source, balance numbers (kit rates and price are first guesses).

# Vowguard (Healer Knight, 4th weapon)

Status: built (protocol 453). Mockup: https://claude.ai/artifact/Ca5cn1c4Hd88ZnakEtqT49 (`Docs/Mockups/vowguard.html`).

## The owner's decisions

- A sword and shield for the Healer Knight with **two stances**: **Valor** uses the blade and shield offensively, **Sanctuary**
  mainly blocks and uses the shield defensively.
- Each stance plays a **combo the player customises** from a pool of moves.
- A chain holds **up to 10 steps**, and steps are **easy to add and remove**.

## How it works

- Alt (the weapon-action key) switches stance; each stance keeps its own place in its chain (like the Nodachi).
- Fire plays the next step. The chain starts over 0.8 s after a step ends without a press, and after its last step.
- Each stance has nine moves: six links and three finishers. Any move can go anywhere in its own stance's chain, repeats
  allowed; finishers don't end the chain.
- **Faith** (0-6): Valor links light one per enemy hit, Sanctuary one per shot the shield stops, a perfect parry fills it.
  A finisher spends it all: +3 damage on its last hit and +2 on each of its heals per Faith.
- Sanctuary: 80% speed, and small shots in a 100° front arc always stop on the shield. Valor's shield only guards during
  shield moves.
- Raising the parry starts both chains over.
- Extra projectiles: +15% damage and healing on every step.

Every number is in README.md (Knight weapons table) and `Core/VowguardMotion.cs`.

## The chain editor

The weapon's detail box in the lobby has **EDIT CHAINS** while the Vowguard is picked. It opens over the loadout's right
column (as Saved loadouts does) while the preview on the left plays the chains:

- Each stance: its steps as up to ten keys, then its nine moves. Click a move to add it to the end; click a step to select
  it, and the next move clicked replaces it. `<` `>` move the selected step, `X` removes it (the last step when nothing is
  selected), `CLEAR` leaves one step.
- Presets: Crusader (damage, the default and what bots play), Shepherd (healing), Bastion (control), Marathon (10 + 10).
- The chains are saved in the profile (`vgchain` line), shared with the lobby (`Msg.Cosmetics`, `Msg.Lobby`) and sent with
  the run's start (`Msg.Start`), so every screen draws everyone's pips.

## Ideas not built

- Its own sound bank (the moves use the Twin Blades', Lance's, Greatsword's and Censer's sounds for now).
- Bots don't switch stance (they play the Valor chain).
- A Rare upgrade card for it.
- Balance: the default Crusader chain measures 74 damage a second (the Lightbringer is 58).

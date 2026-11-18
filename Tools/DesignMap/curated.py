# Hand-written design content (modes, mechanics, goals and how they connect). Executed by seed.py.
MODES = {
 "Quick Play": "Ten waves and a boss, then endless. The lobby's default mode.",
 "Campaign": "100 waves, a boss after every 10th, then endless. Waves are built from the boss factions.",
 "Boss Hunt": "One boss and ten waves of its faction. Winning unlocks that boss's perk.",
 "Survival": "No waves or boss: one clock, pressure climbs, ends when everyone is down. First two minutes melee only.",
 "Practice Range": "Practice (build your own waves) and Training (mechanic drills) tabs, with upgrade picks between waves.",
 "Lobby Hangout": "Social room where everyone's character walks around with their trail, rank badge and name plate.",
}
MECH = {
 "Timed waves": "A wave's enemies arrive over about 45 s: ramp-up, three surges, lulls, taper. Wave ends when the last one dies.",
 "Spawn portals": "Single enemies come in bursts (3 on wave 1, +1 every 8 waves, up to 8) through a violet portal that fills while it opens.",
 "Flank portals": "From wave 5, one burst in 4 opens behind the party; red portal, 1.6 s to open, an arrow points at it.",
 "Party size scaling": "Solo enemies have 85% health, duo 100%, trio 120% and 25% more of them, bosses 150%.",
 "Enemy damage growth": "About +1.5% enemy damage per wave (Normal: 1.3x at wave 22, 2.5x at wave 100).",
 "Boss power scaling": "Boss health rises up to x3 against builds far above typical for that wave; +10% per two waves after wave 40.",
 "Boss stage orbs": "A boss knocks loose small health orbs (2 + 1 per player, 15 HP) at each stage change.",
 "XP and level-up cards": "Every kill gives XP to everyone; after the wave each player picks one of 3 upgrade cards per level gained.",
 "Ability mastery": "Abilities level 1-5 by casting them in fights (XP = cooldown in seconds); level 5 gives a mastery bonus.",
 "Ultimates": "F key; each role+class has a default and some have alternates. Start half charged; upgrades don't shorten the cooldown.",
 "Parry": "Hold right click for a small shield on the cursor side: blocks bullets (red aimed shots too) and melee, slides melee back 45 px.",
 "Momentum": "DPS role passive: each kill adds a stack (max 5), +3% move speed and +3% fire rate per stack.",
 "Taunt and armour": "Tank role: enemies prefer you; 200 HP; Barrier and Roar abilities.",
 "Healing aura": "Healer role: heals allies within 170 px for 9 HP/s; overheal becomes a shield (up to 20% max HP).",
 "Wildcard role": "Fourth role: any class's DPS weapon, two DPS abilities and ultimate; 125 HP, -10% damage, +25% cooldowns. Locked until won as Tank, Healer and DPS.",
 "Multishot per weapon": "Extra projectiles upgrade each weapon in its own way instead of adding bullets in a line.",
 "Weapon variants": "Alternate versions of weapons chosen in the loadout.",
 "Campaign buildup": "The last boss's faction fades out over the next waves, older bosses stay as a pool, the next boss's faction builds to 100%.",
 "Paths": "After each boss the host picks one of two paths for the next ten waves (or none): changes the enemy mix and arena floor, pays off by danger.",
 "Bazaar shop": "Shop reached through paths where you spend earnings on items.",
 "Data Gates": "Once per ten waves a gate opens in the break: everyone alive stands in it to enter a challenge.",
 "Run modifiers": "19 lobby switches grouped Easier / Risky / Harder that change the whole run; host-controlled.",
 "Difficulty": "Easy, Normal, Hard, Nightmare or Custom; applies to the whole run and is kept in saved runs.",
 "Perks": "One rule per boss, unlocked by winning that boss's Boss Hunt; 1 slot to start, 2nd after a full-run win, 3rd after five different hunts.",
 "Expanding arena": "Campaign and Quick Play arena: starts as the plain Classic box and grows as you play (Simple Arena modifier keeps the open arena).",
 "Rooms arena": "Map made of connected rooms with nests and gates.",
 "Room cards": "Cards played on the build screen to add rooms to the expanding arena.",
 "Rank and cosmetics": "Cosmetic account rank: every finished run earns XP; ranks unlock frames, backgrounds, name effects and trails.",
 "Co-op networking": "1-3 players over the network; host-authoritative with snapshots.",
 "Saved runs and records": "Runs can be saved and continued; session JSON feeds balancing reports.",
}
GOALS = {
 "Survive and beat the boss": "Clear every wave and kill the boss without the whole party going down.",
 "Win a full run": "Finish a Campaign or Quick Play run; unlocks a second perk slot and counts toward Wildcard.",
 "Win as Tank, Healer and DPS": "Unlocks the Wildcard role (a co-op win counts).",
 "Beat every Boss Hunt": "Each win unlocks that boss's perk; five different wins unlock the third perk slot.",
 "Reach the Campaign's end": "Wave 100 and beyond into endless.",
 "Outlast the clock": "Survival: stay up as long as possible while pressure climbs.",
 "Climb rank": "XP from every finished run raises cosmetic Rank.",
 "Master abilities": "Level abilities to 5 for their mastery bonuses.",
 "Set records": "Best wave, kills and bosses tracked per profile.",
}
for k, v in MODES.items(): add("Mode", k, v)
for k, v in MECH.items(): add("Mechanic", k, v)
for k, v in GOALS.items(): add("Goal", k, v)
def L(a, *bs):
    for b in bs: edges.append([a, b])
M = lambda n: "Mechanic:" + n
for m in ["Quick Play", "Campaign", "Boss Hunt", "Survival", "Practice Range"]:
    L("Mode:" + m, M("Difficulty"), M("Run modifiers"), M("Party size scaling"), M("Co-op networking"))
for m in ["Quick Play", "Campaign"]:
    L("Mode:" + m, M("Expanding arena"), M("Timed waves"), M("Spawn portals"), M("Flank portals"), M("Room cards"), M("XP and level-up cards"))
L("Mode:Campaign", M("Campaign buildup"), M("Paths"), M("Data Gates"), M("Bazaar shop"), M("Enemy damage growth"))
L("Mode:Boss Hunt", M("Perks"), M("Boss power scaling"))
L("Mode:Practice Range", M("Rooms arena"))
L("Mode:Lobby Hangout", M("Rank and cosmetics"))
for b in [n["id"] for n in nodes if n["type"] == "Boss"]: L(b, M("Boss stage orbs"), M("Boss power scaling"))
L("Role:Tank", M("Taunt and armour")); L("Role:DPS", M("Momentum")); L("Role:Healer", M("Healing aura"))
L("Role:Wildcard", M("Wildcard role"))
L(M("Ultimates"), M("Ability mastery")); L(M("Parry"), "Class:Knight")
L(M("Multishot per weapon"), M("Weapon variants"))
L("Goal:Survive and beat the boss", "Mode:Quick Play", "Mode:Campaign", "Mode:Boss Hunt")
L("Goal:Win a full run", "Mode:Quick Play", "Mode:Campaign", M("Perks"))
L("Goal:Win as Tank, Healer and DPS", M("Wildcard role"), "Role:Tank", "Role:Healer", "Role:DPS")
L("Goal:Beat every Boss Hunt", "Mode:Boss Hunt", M("Perks"))
L("Goal:Reach the Campaign's end", "Mode:Campaign")
L("Goal:Outlast the clock", "Mode:Survival")
L("Goal:Climb rank", M("Rank and cosmetics")); L("Goal:Master abilities", M("Ability mastery"))
L("Goal:Set records", M("Saved runs and records"))

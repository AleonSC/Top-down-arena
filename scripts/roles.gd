extends RefCounted
## Stats and descriptions for the three playable roles.

const ORDER := ["tank", "healer", "dps"]

const DATA := {
	"tank": {
		"name": "Tank",
		"color": Color(0.35, 0.6, 1.0),
		"hp": 240.0,
		"speed": 185.0,
		"hit_r": 9.0,
		"body_r": 18.0,
		"fire_interval": 0.45,
		"ability": "Barrier",
		"ability_cd": 12.0,
		"desc": "Big HP pool and a short-range shotgun. Enemies prefer to target you.\nAbility: a 4 s barrier bubble that deletes enemy bullets. Keep your ally inside it.",
	},
	"healer": {
		"name": "Healer",
		"color": Color(0.35, 1.0, 0.55),
		"hp": 120.0,
		"speed": 215.0,
		"hit_r": 7.0,
		"body_r": 14.0,
		"fire_interval": 0.25,
		"ability": "Mend Pulse",
		"ability_cd": 10.0,
		"desc": "Your aura slowly heals an ally standing near you.\nAbility: heal everyone nearby for 60 HP and instantly revive downed allies in range.",
	},
	"dps": {
		"name": "DPS",
		"color": Color(1.0, 0.55, 0.25),
		"hp": 95.0,
		"speed": 240.0,
		"hit_r": 6.0,
		"body_r": 12.0,
		"fire_interval": 0.1,
		"ability": "Dash",
		"ability_cd": 3.5,
		"desc": "Fragile, but shreds enemies with a twin-stream rifle.\nAbility: a quick dash that makes you briefly invulnerable.",
	},
}

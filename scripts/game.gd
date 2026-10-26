extends Node2D
## The match. The host simulates enemies, bullets, damage and waves; every
## peer moves its own player locally and renders the shared state.
##
## Network traffic:
##   client -> host  net_input (60 Hz, position/aim/firing), net_ability
##   host -> all     net_spawn_bullets / net_remove_bullets (bullets fly
##                   deterministically, so only spawns and hits are sent),
##                   net_snapshot (20 Hz, players, enemies and wave state)

const Roles = preload("res://scripts/roles.gd")

const ARENA := Rect2(0, 0, 1280, 720)

enum { IDLE, FIGHTING, INTERMISSION, GAMEOVER }

const KIND_TANK := 0
const KIND_HEALER := 1
const KIND_DPS := 2
const KIND_SMALL := 10  # Bullets of this kind and above belong to enemies.
const KIND_BIG := 11
const KIND_FAST := 12

const BULLET_RADIUS := {0: 5.0, 1: 4.5, 2: 3.5, 10: 6.0, 11: 10.0, 12: 4.5}
const BULLET_DAMAGE := {10: 12.0, 11: 20.0, 12: 15.0}
const BULLET_COLOR := {
	0: Color(0.5, 0.75, 1.0),
	1: Color(0.5, 1.0, 0.65),
	2: Color(1.0, 0.7, 0.35),
	10: Color(1.0, 0.3, 0.45),
	11: Color(0.85, 0.35, 1.0),
	12: Color(1.0, 0.95, 0.4),
}

const ENEMY := {
	"grunt": {"hp": 60.0, "r": 14.0, "speed": 85.0, "score": 10, "color": Color(1.0, 0.35, 0.35), "sides": 3},
	"burster": {"hp": 110.0, "r": 17.0, "speed": 55.0, "score": 20, "color": Color(0.8, 0.4, 1.0), "sides": 6},
	"sniper": {"hp": 50.0, "r": 12.0, "speed": 95.0, "score": 15, "color": Color(1.0, 0.9, 0.3), "sides": 4},
	"spinner": {"hp": 150.0, "r": 18.0, "speed": 35.0, "score": 25, "color": Color(1.0, 0.6, 0.2), "sides": 5},
	"boss": {"hp": 2400.0, "r": 46.0, "speed": 45.0, "score": 300, "color": Color(1.0, 0.25, 0.5), "sides": 8},
}

const SNAPSHOT_EVERY := 3  # physics ticks
const START_DELAY := 3.0
const INTERMISSION_TIME := 5.0
const CONTACT_DAMAGE := 15.0
const HIT_IFRAMES := 0.35
const REVIVE_RANGE := 70.0
const REVIVE_TIME := 3.0
const AURA_RANGE := 170.0
const AURA_HEAL := 9.0
const BARRIER_TIME := 4.0
const BARRIER_RADIUS := 105.0
const PULSE_RANGE := 260.0
const PULSE_HEAL := 60.0
const DASH_TIME := 0.18
const DASH_SPEED := 1200.0


class Player:
	var id: int
	var role: String
	var pname: String
	var stats: Dictionary
	var color: Color
	var pos := Vector2.ZERO
	var disp := Vector2.ZERO
	var aim := 0.0
	var firing := false
	var hp := 0.0
	var max_hp := 0.0
	var downed := false
	var revive := 0.0
	var fire_cd := 0.0
	var ability_cd := 0.0
	var barrier_t := 0.0
	var invuln_t := 0.0
	var flash := 0.0

	func _init(p_id: int, p_role: String, p_name: String, p_stats: Dictionary) -> void:
		id = p_id
		role = p_role
		pname = p_name
		stats = p_stats
		color = stats["color"]
		max_hp = stats["hp"]
		hp = max_hp


class Enemy:
	var id: int
	var type: String
	var pos := Vector2.ZERO
	var disp := Vector2.ZERO
	var hp := 0.0
	var max_hp := 0.0
	var radius := 10.0
	var speed := 0.0
	var score := 0
	var t := 0.0
	var spawn_t := 0.0
	var fire_t := 0.0
	var ang := 0.0
	var strafe := 1.0
	var target_id := 0
	var retarget_t := 0.0
	var waypoint := Vector2.ZERO
	var spawning := false
	var flash := 0.0


class Bullet:
	var id: int
	var kind: int
	var pos: Vector2
	var vel: Vector2
	var angvel: float
	var life: float
	var radius: float
	var dmg := 0.0
	var owner := 0


var main: Node
var running := false
var state := IDLE
var wave := 0
var state_timer := 0.0
var remaining := 0
var score := 0

var players := {}  # peer id -> Player
var enemies := {}  # enemy id -> Enemy
var bullets := {}  # bullet id -> Bullet
var effects := []

var my_id := 1
var my_ability_cd := 0.0
var dash_t := 0.0
var dash_dir := Vector2.RIGHT
var ability_was_down := false
var anim_t := 0.0
var font: Font

# Host-only simulation state.
var next_bullet_id := 1
var next_enemy_id := 1
var spawn_queue := []
var spawn_timer := 0.0
var tick := 0
var pending_ids := PackedInt32Array()
var pending_data := PackedFloat32Array()
var pending_removed := PackedInt32Array()
var log_stats := false
var bot := false  # --bot: drive the local player automatically (for testing)


func _ready() -> void:
	visible = false
	font = ThemeDB.fallback_font
	log_stats = "--log" in OS.get_cmdline_user_args()
	bot = "--bot" in OS.get_cmdline_user_args()


# --- Lifecycle -------------------------------------------------------------

func host_start(lobby: Dictionary) -> void:
	net_start.rpc(lobby)


@rpc("authority", "call_local", "reliable")
func net_start(lobby: Dictionary) -> void:
	stop()
	my_id = multiplayer.get_unique_id()
	var ids := lobby.keys()
	ids.sort()
	for i in ids.size():
		var info: Dictionary = lobby[ids[i]]
		var p := Player.new(ids[i], info["role"], info["name"], Roles.DATA[info["role"]])
		p.pos = ARENA.get_center() + Vector2((i - (ids.size() - 1) / 2.0) * 90.0, 140.0)
		p.disp = p.pos
		p.aim = -PI / 2
		players[p.id] = p
	state = INTERMISSION
	state_timer = START_DELAY
	for arg in OS.get_cmdline_user_args():
		if arg.begins_with("--start-wave="):
			wave = int(arg.get_slice("=", 1)) - 1
	running = true
	visible = true
	main.on_game_started()
	_log("game started with %d player(s)" % players.size())


@rpc("authority", "call_local", "reliable")
func net_to_lobby() -> void:
	stop()
	main.show_lobby_screen()


func stop() -> void:
	running = false
	visible = false
	state = IDLE
	wave = 0
	score = 0
	remaining = 0
	players.clear()
	enemies.clear()
	bullets.clear()
	effects.clear()
	spawn_queue.clear()
	pending_ids.clear()
	pending_data.clear()
	pending_removed.clear()
	next_bullet_id = 1
	next_enemy_id = 1
	my_ability_cd = 0.0
	dash_t = 0.0
	tick = 0


func on_peer_left(id: int) -> void:
	players.erase(id)


func _unhandled_input(event: InputEvent) -> void:
	if not running or not (event is InputEventKey) or not event.pressed or event.echo:
		return
	match event.keycode:
		KEY_ESCAPE:
			main.leave_session()
		KEY_R:
			if multiplayer.is_server() and state == GAMEOVER:
				host_start(main.lobby_players)
		KEY_L:
			if multiplayer.is_server() and state == GAMEOVER:
				net_to_lobby.rpc()


# --- Frame loop ------------------------------------------------------------

func _physics_process(dt: float) -> void:
	if not running:
		return
	_local_control(dt)
	if multiplayer.is_server():
		_host_tick(dt)
	else:
		_move_bullets(dt)
		tick += 1
		if log_stats and tick % 120 == 0:
			_log("wave %d state %d enemies %d bullets %d hp %s" % [wave, state, enemies.size(), bullets.size(), _hp_summary()])
	for e in enemies.values():
		e.flash = maxf(0.0, e.flash - dt)
	for p in players.values():
		p.flash = maxf(0.0, p.flash - dt)


func _process(dt: float) -> void:
	if not running:
		return
	anim_t += dt
	var k := 1.0 - exp(-18.0 * dt)
	for p in players.values():
		p.disp = p.pos if p.id == my_id else p.disp.lerp(p.pos, k)
	for e in enemies.values():
		e.disp = e.disp.lerp(e.pos, k)
	for fx in effects:
		fx["t"] += dt
	effects = effects.filter(func(fx): return fx["t"] < fx["dur"])
	queue_redraw()


func _local_control(dt: float) -> void:
	var me: Player = players.get(my_id)
	if me == null:
		return
	my_ability_cd = maxf(0.0, my_ability_cd - dt)
	me.aim = (get_global_mouse_position() - me.pos).angle()
	var firing := Input.is_mouse_button_pressed(MOUSE_BUTTON_LEFT) and not me.downed
	var ability_down := Input.is_key_pressed(KEY_SPACE) or Input.is_mouse_button_pressed(MOUSE_BUTTON_RIGHT)
	var ability_pressed := ability_down and not ability_was_down
	ability_was_down = ability_down

	var move := Vector2(
		int(Input.is_key_pressed(KEY_D) or Input.is_key_pressed(KEY_RIGHT)) - int(Input.is_key_pressed(KEY_A) or Input.is_key_pressed(KEY_LEFT)),
		int(Input.is_key_pressed(KEY_S) or Input.is_key_pressed(KEY_DOWN)) - int(Input.is_key_pressed(KEY_W) or Input.is_key_pressed(KEY_UP)))
	move = move.normalized()
	if bot:
		move = Vector2.from_angle(anim_t * 0.9 + my_id)
		var nearest: Enemy = null
		for e in enemies.values():
			if nearest == null or e.pos.distance_to(me.pos) < nearest.pos.distance_to(me.pos):
				nearest = e
		if nearest:
			me.aim = (nearest.pos - me.pos).angle()
		firing = not me.downed
		ability_pressed = fmod(anim_t, 4.0) < dt

	if me.downed:
		dash_t = 0.0
	elif dash_t > 0.0:
		dash_t -= dt
		me.pos += dash_dir * DASH_SPEED * dt
	else:
		me.pos += move * me.stats["speed"] * dt

	if ability_pressed and not me.downed and my_ability_cd <= 0.0 and state != GAMEOVER:
		my_ability_cd = me.stats["ability_cd"]
		if me.role == "dps":
			dash_dir = move if move != Vector2.ZERO else Vector2.from_angle(me.aim)
			dash_t = DASH_TIME
		if multiplayer.is_server():
			_host_ability(my_id)
		else:
			net_ability.rpc_id(1)

	var r: float = me.stats["body_r"]
	me.pos = me.pos.clamp(ARENA.position + Vector2(r, r), ARENA.end - Vector2(r, r))
	if multiplayer.is_server():
		me.firing = firing
	else:
		net_input.rpc_id(1, me.pos, me.aim, firing)


@rpc("any_peer", "call_remote", "unreliable_ordered")
func net_input(pos: Vector2, aim: float, firing: bool) -> void:
	if not multiplayer.is_server():
		return
	var p: Player = players.get(multiplayer.get_remote_sender_id())
	if p == null:
		return
	if not p.downed:
		var r: float = p.stats["body_r"]
		p.pos = pos.clamp(ARENA.position + Vector2(r, r), ARENA.end - Vector2(r, r))
	p.aim = aim
	p.firing = firing


@rpc("any_peer", "call_remote", "reliable")
func net_ability() -> void:
	if multiplayer.is_server():
		_host_ability(multiplayer.get_remote_sender_id())


# --- Host simulation -------------------------------------------------------

func _host_tick(dt: float) -> void:
	_tick_players(dt)
	_tick_waves(dt)
	_tick_enemies(dt)
	_move_bullets(dt)
	_collide()
	_check_gameover()

	if not pending_ids.is_empty():
		net_spawn_bullets.rpc(pending_ids, pending_data)
		pending_ids = PackedInt32Array()
		pending_data = PackedFloat32Array()
	if not pending_removed.is_empty():
		net_remove_bullets.rpc(pending_removed)
		pending_removed = PackedInt32Array()
	tick += 1
	if tick % SNAPSHOT_EVERY == 0:
		net_snapshot.rpc(_snapshot())
	if log_stats and tick % 120 == 0:
		_log("wave %d state %d enemies %d bullets %d score %d hp %s" % [wave, state, enemies.size(), bullets.size(), score, _hp_summary()])


func _tick_players(dt: float) -> void:
	for p in players.values():
		p.ability_cd = maxf(0.0, p.ability_cd - dt)
		p.barrier_t = maxf(0.0, p.barrier_t - dt)
		p.invuln_t = maxf(0.0, p.invuln_t - dt)
		p.fire_cd -= dt
		if p.downed:
			continue
		if p.firing and p.fire_cd <= 0.0 and state != GAMEOVER:
			p.fire_cd = p.stats["fire_interval"]
			_fire_player(p)
		if p.role == "healer":
			p.hp = minf(p.max_hp, p.hp + 3.0 * dt)
			for q in players.values():
				if q != p and not q.downed and q.pos.distance_to(p.pos) < AURA_RANGE:
					q.hp = minf(q.max_hp, q.hp + AURA_HEAL * dt)

	# Standing next to a downed ally slowly brings them back.
	for p in players.values():
		if not p.downed:
			continue
		var helped := false
		for q in players.values():
			if q != p and not q.downed and q.pos.distance_to(p.pos) < REVIVE_RANGE:
				helped = true
		if helped:
			p.revive += dt / REVIVE_TIME
			if p.revive >= 1.0:
				_revive(p, 0.35)
		else:
			p.revive = maxf(0.0, p.revive - dt * 0.25)


func _fire_player(p: Player) -> void:
	var dir := Vector2.from_angle(p.aim)
	var muzzle: Vector2 = p.pos + dir * p.stats["body_r"]
	match p.role:
		"tank":
			for i in 5:
				_spawn_bullet(KIND_TANK, muzzle, Vector2.from_angle(p.aim + (i - 2) * 0.13) * 520.0, 0.0, 0.5, 7.0, p.id)
		"healer":
			_spawn_bullet(KIND_HEALER, muzzle, dir * 650.0, 0.0, 1.3, 11.0, p.id)
		"dps":
			var side := dir.orthogonal() * 5.0
			_spawn_bullet(KIND_DPS, muzzle + side, dir * 820.0, 0.0, 1.0, 6.5, p.id)
			_spawn_bullet(KIND_DPS, muzzle - side, dir * 820.0, 0.0, 1.0, 6.5, p.id)


func _host_ability(id: int) -> void:
	var p: Player = players.get(id)
	if p == null or p.downed or p.ability_cd > 0.5:
		return
	p.ability_cd = p.stats["ability_cd"]
	match p.role:
		"tank":
			p.barrier_t = BARRIER_TIME
			net_fx.rpc("ring", p.pos, Color(0.5, 0.8, 1.0), BARRIER_RADIUS, 0.4)
		"dps":
			p.invuln_t = maxf(p.invuln_t, DASH_TIME + 0.12)
		"healer":
			for q in players.values():
				if q.pos.distance_to(p.pos) < PULSE_RANGE:
					if q.downed:
						_revive(q, 0.5)
					else:
						q.hp = minf(q.max_hp, q.hp + PULSE_HEAL)
			net_fx.rpc("ring", p.pos, Color(0.4, 1.0, 0.55), PULSE_RANGE, 0.6)


func _damage(p: Player, amount: float) -> void:
	p.hp -= amount
	p.invuln_t = HIT_IFRAMES
	p.flash = 0.1
	if p.hp <= 0.0:
		p.hp = 0.0
		p.downed = true
		p.revive = 0.0
		p.barrier_t = 0.0
		p.firing = false


func _revive(p: Player, fraction: float) -> void:
	p.downed = false
	p.revive = 0.0
	p.hp = maxf(p.hp, p.max_hp * fraction)
	p.invuln_t = 1.5


func _check_gameover() -> void:
	if state == GAMEOVER or state == IDLE or players.is_empty():
		return
	for p in players.values():
		if not p.downed:
			return
	state = GAMEOVER
	spawn_queue.clear()
	_log("game over at wave %d, score %d" % [wave, score])


func _tick_waves(dt: float) -> void:
	match state:
		INTERMISSION:
			state_timer -= dt
			if state_timer <= 0.0:
				wave += 1
				state = FIGHTING
				spawn_timer = 0.5
				spawn_queue = _build_wave(wave)
				_log("wave %d: %s" % [wave, spawn_queue])
		FIGHTING:
			if not spawn_queue.is_empty():
				spawn_timer -= dt
				if spawn_timer <= 0.0:
					_spawn_enemy(spawn_queue.pop_front())
					spawn_timer = maxf(0.3, 1.1 - wave * 0.05)
			elif enemies.is_empty():
				_wave_cleared()
	remaining = spawn_queue.size() + enemies.size()


func _build_wave(n: int) -> Array:
	var q := []
	if n % 5 == 0:
		q.append("boss")
		for i in int(n / 2.0):
			q.append("grunt")
		return q
	var pool := ["grunt"]
	if n >= 2:
		pool.append("burster")
	if n >= 3:
		pool.append("sniper")
	if n >= 4:
		pool.append("spinner")
	for i in mini(4 + n * 2, 32):
		q.append(pool[randi() % pool.size()])
	return q


func _wave_cleared() -> void:
	state = INTERMISSION
	state_timer = INTERMISSION_TIME
	for p in players.values():
		if p.downed:
			_revive(p, 0.4)
		else:
			p.hp = minf(p.max_hp, p.hp + p.max_hp * 0.3)
	net_clear_enemy_bullets.rpc()
	_log("wave %d cleared" % wave)


func _spawn_enemy(type: String) -> void:
	var d: Dictionary = ENEMY[type]
	var e := Enemy.new()
	e.id = next_enemy_id
	next_enemy_id += 1
	e.type = type
	e.max_hp = d["hp"] * (1.0 + 0.15 * (wave - 1)) * (1.0 if players.size() > 1 else 0.7)
	e.hp = e.max_hp
	e.radius = d["r"]
	e.speed = d["speed"]
	e.score = d["score"]
	var m := e.radius + 30.0
	match randi() % 4:
		0: e.pos = Vector2(randf_range(m, ARENA.end.x - m), m)
		1: e.pos = Vector2(randf_range(m, ARENA.end.x - m), ARENA.end.y - m)
		2: e.pos = Vector2(m, randf_range(m, ARENA.end.y - m))
		_: e.pos = Vector2(ARENA.end.x - m, randf_range(m, ARENA.end.y - m))
	if type == "boss":
		e.pos = Vector2(ARENA.get_center().x, 140.0)
	e.disp = e.pos
	e.spawn_t = 0.9
	e.fire_t = randf_range(0.3, 1.2)
	e.strafe = 1.0 if randf() < 0.5 else -1.0
	e.ang = randf() * TAU
	e.waypoint = e.pos
	enemies[e.id] = e


func _pick_target() -> int:
	var alive := []
	var tanks := []
	for p in players.values():
		if not p.downed:
			alive.append(p.id)
			if p.role == "tank":
				tanks.append(p.id)
	if alive.is_empty():
		return 0
	if not tanks.is_empty() and randf() < 0.7:
		return tanks.pick_random()
	return alive.pick_random()


func _tick_enemies(dt: float) -> void:
	var rate := minf(2.0, 1.0 + 0.05 * (wave - 1))
	for e in enemies.values():
		e.t += dt
		if e.spawn_t > 0.0:
			e.spawn_t -= dt
			continue
		e.retarget_t -= dt
		var target: Player = players.get(e.target_id)
		if e.retarget_t <= 0.0 or target == null or target.downed:
			e.target_id = _pick_target()
			e.retarget_t = 3.0
			target = players.get(e.target_id)
		var tpos: Vector2 = target.pos if target else ARENA.get_center()
		_move_enemy(e, tpos, dt)
		if state == FIGHTING:
			e.fire_t -= dt * rate
			if e.fire_t <= 0.0:
				_enemy_fire(e, tpos)
		for p in players.values():
			if not p.downed and p.invuln_t <= 0.0 and p.pos.distance_to(e.pos) < e.radius + p.stats["hit_r"]:
				_damage(p, CONTACT_DAMAGE)

	# Keep enemies from stacking on top of each other.
	var list := enemies.values()
	for i in list.size():
		for j in range(i + 1, list.size()):
			var a: Enemy = list[i]
			var b: Enemy = list[j]
			var d := b.pos - a.pos
			var l := d.length()
			var min_d := a.radius + b.radius
			if l < min_d and l > 0.001:
				var push := d / l * (min_d - l) * 0.5
				a.pos -= push
				b.pos += push


func _keep_distance(dir: Vector2, dist: float, want: float) -> Vector2:
	if dist > want + 40.0:
		return dir
	if dist < want - 40.0:
		return -dir
	return Vector2.ZERO


func _move_enemy(e: Enemy, tpos: Vector2, dt: float) -> void:
	var to := tpos - e.pos
	var dist := to.length()
	var dir := to / dist if dist > 0.01 else Vector2.ZERO
	var v := Vector2.ZERO
	match e.type:
		"grunt":
			v = _keep_distance(dir, dist, 230.0) + dir.orthogonal() * e.strafe * 0.6
		"sniper":
			v = _keep_distance(dir, dist, 420.0) + dir.orthogonal() * e.strafe * 0.8
		"burster":
			v = _keep_distance(dir, dist, 160.0)
		"spinner":
			if e.pos.distance_to(e.waypoint) < 20.0:
				e.waypoint = Vector2(randf_range(150, ARENA.end.x - 150), randf_range(150, ARENA.end.y - 150))
			v = (e.waypoint - e.pos).normalized()
		"boss":
			var goal := Vector2(ARENA.get_center().x + sin(e.t * 0.5) * 380.0, 150.0 + sin(e.t * 0.8) * 40.0)
			v = (goal - e.pos) / 60.0
	if randf() < dt * 0.3:
		e.strafe = -e.strafe
	e.pos += v.limit_length(1.0) * e.speed * dt
	e.pos = e.pos.clamp(ARENA.position + Vector2(e.radius, e.radius), ARENA.end - Vector2(e.radius, e.radius))


func _enemy_fire(e: Enemy, tpos: Vector2) -> void:
	var aim := (tpos - e.pos).angle()
	match e.type:
		"grunt":
			e.fire_t = 1.8
			for i in 3:
				_enemy_shot(e, aim + (i - 1) * 0.22, 175.0, KIND_SMALL)
		"burster":
			e.fire_t = 2.4
			var n := 12 + mini(wave, 10)
			e.ang += PI / n
			for i in n:
				_enemy_shot(e, e.ang + TAU * i / n, 135.0, KIND_SMALL)
		"sniper":
			e.fire_t = 2.6
			_enemy_shot(e, aim, 400.0, KIND_FAST)
		"spinner":
			e.fire_t = 0.11
			e.ang += 0.27
			for k in 3:
				_enemy_shot(e, e.ang + TAU * k / 3.0, 125.0, KIND_SMALL, 0.25)
		"boss":
			match int(e.t / 6.0) % 3:
				0:  # four-armed spiral
					e.fire_t = 0.07
					e.ang += 0.19
					for k in 4:
						_enemy_shot(e, e.ang + TAU * k / 4.0, 150.0, KIND_SMALL)
				1:  # slow rings of big orbs
					e.fire_t = 1.0
					e.ang += 0.12
					for i in 26:
						_enemy_shot(e, e.ang + TAU * i / 26.0, 110.0, KIND_BIG)
				2:  # aimed fan
					e.fire_t = 0.45
					for i in 9:
						_enemy_shot(e, aim + (i - 4) * 0.12, 210.0, KIND_SMALL)


func _enemy_shot(e: Enemy, ang: float, speed: float, kind: int, angvel := 0.0) -> void:
	var dir := Vector2.from_angle(ang)
	_spawn_bullet(kind, e.pos + dir * e.radius, dir * speed, angvel, 8.0, BULLET_DAMAGE[kind], 0)


func _spawn_bullet(kind: int, pos: Vector2, vel: Vector2, angvel: float, life: float, dmg: float, owner: int) -> void:
	var b := _make_bullet(next_bullet_id, kind, pos, vel, angvel, life)
	next_bullet_id += 1
	b.dmg = dmg
	b.owner = owner
	pending_ids.append(b.id)
	for v in [kind, pos.x, pos.y, vel.x, vel.y, angvel, life]:
		pending_data.append(v)


func _make_bullet(id: int, kind: int, pos: Vector2, vel: Vector2, angvel: float, life: float) -> Bullet:
	var b := Bullet.new()
	b.id = id
	b.kind = kind
	b.pos = pos
	b.vel = vel
	b.angvel = angvel
	b.life = life
	b.radius = BULLET_RADIUS[kind]
	bullets[id] = b
	return b


func _move_bullets(dt: float) -> void:
	var bounds := ARENA.grow(40.0)
	var gone := []
	for b in bullets.values():
		if b.angvel != 0.0:
			b.vel = b.vel.rotated(b.angvel * dt)
		b.pos += b.vel * dt
		b.life -= dt
		if b.life <= 0.0 or not bounds.has_point(b.pos):
			gone.append(b.id)
	for id in gone:
		bullets.erase(id)


func _collide() -> void:
	var removed := []
	for b in bullets.values():
		if b.kind < KIND_SMALL:
			for e in enemies.values():
				if e.spawn_t > 0.0 or e.hp <= 0.0:
					continue
				if b.pos.distance_squared_to(e.pos) < pow(e.radius + b.radius, 2):
					e.hp -= b.dmg
					e.flash = 0.06
					removed.append(b.id)
					break
		else:
			for p in players.values():
				if p.downed:
					continue
				if p.barrier_t > 0.0 and b.pos.distance_to(p.pos) < BARRIER_RADIUS:
					removed.append(b.id)
					break
				if b.pos.distance_squared_to(p.pos) < pow(p.stats["hit_r"] + b.radius, 2):
					# Invulnerable players (dash, recent hit) let bullets pass through.
					if p.invuln_t <= 0.0:
						_damage(p, b.dmg)
						removed.append(b.id)
					break
	for id in removed:
		var b: Bullet = bullets.get(id)
		if b:
			_add_fx("spark", b.pos, BULLET_COLOR[b.kind], 10.0, 0.15)
			bullets.erase(id)
			pending_removed.append(id)

	for e in enemies.values().duplicate():
		if e.hp <= 0.0:
			score += e.score
			_add_fx("boom", e.pos, ENEMY[e.type]["color"], e.radius * 2.2, 0.4)
			enemies.erase(e.id)


func _snapshot() -> Array:
	var ps := []
	for p in players.values():
		ps.append([p.id, p.pos, p.aim, p.hp, p.downed, p.revive, p.ability_cd, p.barrier_t, p.invuln_t])
	var es := []
	for e in enemies.values():
		es.append([e.id, e.type, e.pos, e.hp, e.max_hp, e.spawn_t > 0.0])
	return [state, wave, state_timer, remaining, score, ps, es]


# --- Client-side replication ----------------------------------------------

@rpc("authority", "call_remote", "reliable")
func net_spawn_bullets(ids: PackedInt32Array, data: PackedFloat32Array) -> void:
	if not running:
		return
	for i in ids.size():
		var o := i * 7
		_make_bullet(ids[i], int(data[o]), Vector2(data[o + 1], data[o + 2]), Vector2(data[o + 3], data[o + 4]), data[o + 5], data[o + 6])


@rpc("authority", "call_remote", "reliable")
func net_remove_bullets(ids: PackedInt32Array) -> void:
	for id in ids:
		var b: Bullet = bullets.get(id)
		if b:
			_add_fx("spark", b.pos, BULLET_COLOR[b.kind], 10.0, 0.15)
			bullets.erase(id)


@rpc("authority", "call_local", "reliable")
func net_clear_enemy_bullets() -> void:
	for b in bullets.values():
		if b.kind >= KIND_SMALL:
			bullets.erase(b.id)


@rpc("authority", "call_local", "reliable")
func net_fx(kind: String, pos: Vector2, color: Color, radius: float, dur: float) -> void:
	_add_fx(kind, pos, color, radius, dur)


@rpc("authority", "call_remote", "unreliable_ordered")
func net_snapshot(s: Array) -> void:
	if not running:
		return
	state = s[0]
	wave = s[1]
	state_timer = s[2]
	remaining = s[3]
	score = s[4]
	for pa in s[5]:
		var p: Player = players.get(pa[0])
		if p == null:
			continue
		if p.id != my_id:
			p.pos = pa[1]
			p.aim = pa[2]
		if pa[3] < p.hp:
			p.flash = 0.1
		p.hp = pa[3]
		p.downed = pa[4]
		p.revive = pa[5]
		p.ability_cd = pa[6]
		p.barrier_t = pa[7]
		p.invuln_t = pa[8]

	var seen := {}
	for ea in s[6]:
		var e: Enemy = enemies.get(ea[0])
		if e == null:
			e = Enemy.new()
			e.id = ea[0]
			e.type = ea[1]
			e.radius = ENEMY[e.type]["r"]
			e.pos = ea[2]
			e.disp = e.pos
			e.hp = ea[3]
			enemies[e.id] = e
		if ea[3] < e.hp:
			e.flash = 0.06
		e.pos = ea[2]
		e.hp = ea[3]
		e.max_hp = ea[4]
		e.spawning = ea[5]
		seen[e.id] = true
	for e in enemies.values().duplicate():
		if not seen.has(e.id):
			_add_fx("boom", e.disp, ENEMY[e.type]["color"], e.radius * 2.2, 0.4)
			enemies.erase(e.id)


# --- Rendering -------------------------------------------------------------

func _add_fx(kind: String, pos: Vector2, color: Color, radius: float, dur: float) -> void:
	effects.append({"kind": kind, "pos": pos, "color": color, "radius": radius, "t": 0.0, "dur": dur})


func _draw() -> void:
	draw_rect(ARENA, Color(0.05, 0.06, 0.09))
	for x in range(0, int(ARENA.end.x) + 1, 64):
		draw_line(Vector2(x, 0), Vector2(x, ARENA.end.y), Color(1, 1, 1, 0.035))
	for y in range(0, int(ARENA.end.y) + 1, 64):
		draw_line(Vector2(0, y), Vector2(ARENA.end.x, y), Color(1, 1, 1, 0.035))
	draw_rect(ARENA.grow(-2.0), Color(0.3, 0.35, 0.5, 0.6), false, 3.0)

	for p in players.values():
		if p.downed:
			continue
		if p.role == "healer":
			draw_circle(p.disp, AURA_RANGE, Color(0.3, 1.0, 0.5, 0.03))
			draw_arc(p.disp, AURA_RANGE, 0.0, TAU, 64, Color(0.3, 1.0, 0.5, 0.18), 2.0)
		if p.barrier_t > 0.0:
			var a := 0.7 if p.barrier_t > 1.0 or fmod(anim_t, 0.2) < 0.1 else 0.25
			draw_circle(p.disp, BARRIER_RADIUS, Color(0.4, 0.7, 1.0, 0.12))
			draw_arc(p.disp, BARRIER_RADIUS, 0.0, TAU, 64, Color(0.5, 0.8, 1.0, a), 3.0)

	for b in bullets.values():
		if b.kind < KIND_SMALL:
			draw_circle(b.pos, b.radius, BULLET_COLOR[b.kind] * Color(1, 1, 1, 0.8))

	for e in enemies.values():
		_draw_enemy(e)
	for p in players.values():
		_draw_player(p)

	for b in bullets.values():
		if b.kind >= KIND_SMALL:
			var c: Color = BULLET_COLOR[b.kind]
			draw_circle(b.pos, b.radius + 2.5, Color(c, 0.3))
			draw_circle(b.pos, b.radius, c)
			draw_circle(b.pos, b.radius * 0.45, Color(1, 1, 1, 0.9))

	for fx in effects:
		var f: float = fx["t"] / fx["dur"]
		var c: Color = fx["color"]
		match fx["kind"]:
			"boom":
				draw_circle(fx["pos"], fx["radius"] * (0.4 + 0.6 * f), Color(c, 0.35 * (1.0 - f)))
				draw_arc(fx["pos"], fx["radius"] * (0.5 + f), 0.0, TAU, 32, Color(c, 1.0 - f), 3.0)
			"spark":
				draw_arc(fx["pos"], fx["radius"] * f, 0.0, TAU, 12, Color(c, 1.0 - f), 2.0)
			"ring":
				draw_arc(fx["pos"], fx["radius"] * (0.2 + 0.8 * f), 0.0, TAU, 64, Color(c, 1.0 - f), 4.0)

	_draw_crosshair()
	_draw_hud()


func _draw_poly(center: Vector2, r: float, sides: int, rot: float, color: Color) -> void:
	var pts := PackedVector2Array()
	for i in sides:
		pts.append(center + Vector2.from_angle(rot + TAU * i / sides) * r)
	draw_colored_polygon(pts, color.darkened(0.45))
	pts.append(pts[0])
	draw_polyline(pts, color, 2.5)


func _draw_bar(pos: Vector2, size: Vector2, frac: float, color: Color) -> void:
	draw_rect(Rect2(pos, size), Color(0, 0, 0, 0.6))
	draw_rect(Rect2(pos, Vector2(size.x * clampf(frac, 0.0, 1.0), size.y)), color)


func _draw_enemy(e: Enemy) -> void:
	var d: Dictionary = ENEMY[e.type]
	var col: Color = d["color"]
	var spawning: bool = e.spawn_t > 0.0 if multiplayer.is_server() else e.spawning
	if spawning:
		draw_arc(e.disp, e.radius + 6.0 + 4.0 * sin(anim_t * 12.0), 0.0, TAU, 32, Color(col, 0.6), 2.0)
		col = Color(col, 0.35)
	if e.flash > 0.0:
		col = Color.WHITE
	var spin := 2.5 if e.type == "spinner" else 0.4
	_draw_poly(e.disp, e.radius, d["sides"], anim_t * spin + e.id, col)
	if e.hp < e.max_hp:
		var w := maxf(30.0, e.radius * 2.0)
		_draw_bar(e.disp + Vector2(-w / 2.0, -e.radius - 10.0), Vector2(w, 4.0), e.hp / e.max_hp, Color(1, 0.3, 0.3))


func _draw_player(p: Player) -> void:
	var r: float = p.stats["body_r"]
	var col := p.color
	if p.downed:
		col = Color(0.45, 0.45, 0.5)
	elif p.invuln_t > 0.0 and fmod(anim_t, 0.12) < 0.06:
		col = Color(col, 0.35)
	if p.flash > 0.0:
		col = Color.WHITE

	if p.id == my_id and dash_t > 0.0:
		draw_circle(p.disp - dash_dir * 18.0, r * 0.8, Color(col, 0.25))
	draw_circle(p.disp, r, col.darkened(0.55))
	draw_arc(p.disp, r, 0.0, TAU, 32, col, 3.0)
	var dir := Vector2.from_angle(p.aim)
	draw_line(p.disp + dir * r * 0.5, p.disp + dir * (r + 10.0), col, 5.0)
	# Role glyph, then the real hitbox drawn as a bright core.
	match p.role:
		"tank":
			draw_rect(Rect2(p.disp - Vector2(r, r) * 0.55, Vector2(r, r) * 1.1), Color(col, 0.5), false, 2.0)
		"healer":
			draw_line(p.disp - Vector2(r * 0.6, 0), p.disp + Vector2(r * 0.6, 0), Color(col, 0.6), 3.0)
			draw_line(p.disp - Vector2(0, r * 0.6), p.disp + Vector2(0, r * 0.6), Color(col, 0.6), 3.0)
	draw_circle(p.disp, p.stats["hit_r"], Color(1, 1, 1, 0.85))
	draw_circle(p.disp, p.stats["hit_r"] * 0.5, col)

	var label: String = p.pname + (" (you)" if p.id == my_id else "")
	draw_string(font, p.disp + Vector2(-80, -r - 18.0), label, HORIZONTAL_ALIGNMENT_CENTER, 160, 13, Color(col, 0.9))
	_draw_bar(p.disp + Vector2(-20, -r - 12.0), Vector2(40, 4), p.hp / p.max_hp, Color(0.3, 1.0, 0.4))
	if p.downed:
		draw_arc(p.disp, r + 8.0, -PI / 2, -PI / 2 + TAU * maxf(p.revive, 0.01), 32, Color(0.4, 1.0, 0.5), 4.0)
		draw_string(font, p.disp + Vector2(-130, r + 24.0), "DOWNED — stand close to revive", HORIZONTAL_ALIGNMENT_CENTER, 260, 11, Color(1, 0.6, 0.6))


func _draw_crosshair() -> void:
	var m := get_global_mouse_position()
	var c := Color(1, 1, 1, 0.7)
	draw_arc(m, 8.0, 0.0, TAU, 16, c, 1.5)
	draw_line(m - Vector2(13, 0), m - Vector2(5, 0), c, 1.5)
	draw_line(m + Vector2(5, 0), m + Vector2(13, 0), c, 1.5)
	draw_line(m - Vector2(0, 13), m - Vector2(0, 5), c, 1.5)
	draw_line(m + Vector2(0, 5), m + Vector2(0, 13), c, 1.5)


func _draw_hud() -> void:
	var white := Color(1, 1, 1, 0.9)
	draw_string(font, Vector2(20, 36), "WAVE %d" % wave, HORIZONTAL_ALIGNMENT_LEFT, -1, 26, white)
	draw_string(font, Vector2(20, 58), "Enemies left: %d    Score: %d" % [remaining, score], HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(white, 0.7))

	var center := ARENA.get_center()
	match state:
		INTERMISSION:
			var msg := "Get ready!" if wave == 0 else "Wave %d cleared!" % wave
			draw_string(font, center + Vector2(-400, -40), msg, HORIZONTAL_ALIGNMENT_CENTER, 800, 34, white)
			draw_string(font, center + Vector2(-400, -8), "Wave %d starts in %d" % [wave + 1, ceili(state_timer)], HORIZONTAL_ALIGNMENT_CENTER, 800, 18, Color(white, 0.7))
		GAMEOVER:
			draw_rect(ARENA, Color(0, 0, 0, 0.45))
			draw_string(font, center + Vector2(-400, -40), "GAME OVER", HORIZONTAL_ALIGNMENT_CENTER, 800, 48, Color(1, 0.4, 0.4))
			draw_string(font, center + Vector2(-400, 0), "You survived to wave %d with %d points" % [wave, score], HORIZONTAL_ALIGNMENT_CENTER, 800, 20, white)
			var hint := "R: play again    L: back to lobby    Esc: quit" if multiplayer.is_server() else "Waiting for the host…    Esc: quit"
			draw_string(font, center + Vector2(-400, 32), hint, HORIZONTAL_ALIGNMENT_CENTER, 800, 16, Color(white, 0.7))

	# Player cards in the top-right corner, yours first.
	var ids := players.keys()
	ids.sort_custom(func(a, b): return a == my_id or (b != my_id and a < b))
	var x := ARENA.end.x - 262.0 * ids.size() - 8.0
	for id in ids:
		var p: Player = players[id]
		var y := 12.0
		draw_rect(Rect2(x, y, 250, 50), Color(0, 0, 0, 0.45))
		draw_string(font, Vector2(x + 8, y + 16), "%s — %s" % [p.pname, p.stats["name"]], HORIZONTAL_ALIGNMENT_LEFT, 234, 13, p.color)
		_draw_bar(Vector2(x + 8, y + 22), Vector2(234, 8), p.hp / p.max_hp, Color(0.3, 1.0, 0.4) if not p.downed else Color(0.5, 0.5, 0.5))
		var cd := my_ability_cd if id == my_id else p.ability_cd
		var max_cd: float = p.stats["ability_cd"]
		var ready := cd <= 0.0
		_draw_bar(Vector2(x + 8, y + 36), Vector2(120, 6), 1.0 - cd / max_cd, Color(0.5, 0.8, 1.0) if ready else Color(0.35, 0.45, 0.6))
		draw_string(font, Vector2(x + 134, y + 43), "%s %s" % [p.stats["ability"], "READY" if ready else "%.1fs" % cd], HORIZONTAL_ALIGNMENT_LEFT, 110, 11, Color(white, 0.8))
		x += 262.0


func _hp_summary() -> String:
	var parts := []
	for p in players.values():
		parts.append("%s:%d%s" % [p.role, int(p.hp), "(down)" if p.downed else ""])
	return " ".join(parts)


func _log(msg: String) -> void:
	if log_stats:
		print("[%s] %s" % ["host" if multiplayer.is_server() else "client", msg])

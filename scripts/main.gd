extends Node
## Menus, lobby and connection handling. The match itself lives in game.gd.

const Roles = preload("res://scripts/roles.gd")
const GameScript = preload("res://scripts/game.gd")

const DEFAULT_PORT := 7777
const MAX_PLAYERS := 2

var game: Node2D
var lobby_players := {}  # peer id -> {"name": String, "role": String}

var ui: CanvasLayer
var menu_root: Control
var lobby_root: Control
var name_edit: LineEdit
var ip_edit: LineEdit
var port_edit: LineEdit
var menu_status: Label
var lobby_info: Label
var lobby_list: Label
var role_desc: Label
var start_btn: Button
var role_btns := {}

# Command-line options for quick testing, e.g.
#   godot -- --host --role=tank --autostart=2
#   godot -- --join=127.0.0.1 --role=dps
var cli := {}


func _ready() -> void:
	game = GameScript.new()
	game.name = "Game"
	game.main = self
	add_child(game)

	ui = CanvasLayer.new()
	ui.name = "UI"
	add_child(ui)
	_build_menu()
	_build_lobby()
	_show_menu("")

	multiplayer.peer_disconnected.connect(_on_peer_disconnected)
	multiplayer.connected_to_server.connect(_on_connected_to_server)
	multiplayer.connection_failed.connect(_on_connection_failed)
	multiplayer.server_disconnected.connect(_on_server_disconnected)

	_parse_cli()
	if cli.has("name"):
		name_edit.text = cli["name"]
	if cli.has("host"):
		_on_host_pressed()
	elif cli.has("join"):
		ip_edit.text = cli["join"]
		_on_join_pressed()


func _parse_cli() -> void:
	for arg in OS.get_cmdline_user_args():
		var parts: PackedStringArray = arg.trim_prefix("--").split("=", true, 1)
		cli[parts[0]] = parts[1] if parts.size() > 1 else ""


# --- UI construction -------------------------------------------------------

func _make_screen() -> Control:
	var root := Control.new()
	root.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	ui.add_child(root)
	var bg := ColorRect.new()
	bg.color = Color(0.06, 0.07, 0.1)
	bg.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(bg)
	return root


func _make_panel(root: Control) -> VBoxContainer:
	var center := CenterContainer.new()
	center.set_anchors_and_offsets_preset(Control.PRESET_FULL_RECT)
	root.add_child(center)
	var panel := PanelContainer.new()
	center.add_child(panel)
	var margin := MarginContainer.new()
	for side in ["left", "right", "top", "bottom"]:
		margin.add_theme_constant_override("margin_" + side, 24)
	panel.add_child(margin)
	var box := VBoxContainer.new()
	box.custom_minimum_size = Vector2(520, 0)
	box.add_theme_constant_override("separation", 10)
	margin.add_child(box)
	return box


func _make_label(parent: Control, text: String, size := 16) -> Label:
	var label := Label.new()
	label.text = text
	label.add_theme_font_size_override("font_size", size)
	label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	parent.add_child(label)
	return label


func _make_field(parent: Control, caption: String, value: String) -> LineEdit:
	_make_label(parent, caption, 14)
	var edit := LineEdit.new()
	edit.text = value
	parent.add_child(edit)
	return edit


func _make_button(parent: Control, text: String, callback: Callable) -> Button:
	var btn := Button.new()
	btn.text = text
	btn.custom_minimum_size = Vector2(0, 36)
	btn.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	btn.pressed.connect(callback)
	parent.add_child(btn)
	return btn


func _build_menu() -> void:
	menu_root = _make_screen()
	var box := _make_panel(menu_root)
	_make_label(box, "TOP-DOWN ARENA", 32).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_make_label(box, "Co-op bullet hell for two. Survive the waves.", 14).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	name_edit = _make_field(box, "Your name", "Player")
	ip_edit = _make_field(box, "Host address (only needed to join)", "127.0.0.1")
	port_edit = _make_field(box, "Port", str(DEFAULT_PORT))
	var row := HBoxContainer.new()
	box.add_child(row)
	_make_button(row, "Host game", _on_host_pressed)
	_make_button(row, "Join game", _on_join_pressed)
	menu_status = _make_label(box, "", 14)
	menu_status.add_theme_color_override("font_color", Color(1, 0.75, 0.4))
	_make_label(box, "Controls: WASD move · Mouse aim · Left click shoot · Space / Right click ability · Esc leave", 13)


func _build_lobby() -> void:
	lobby_root = _make_screen()
	var box := _make_panel(lobby_root)
	_make_label(box, "LOBBY", 28).horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	lobby_info = _make_label(box, "", 14)
	lobby_info.add_theme_color_override("font_color", Color(0.7, 0.8, 1))
	lobby_list = _make_label(box, "", 18)
	_make_label(box, "Pick your role:", 14)
	var row := HBoxContainer.new()
	box.add_child(row)
	for role in Roles.ORDER:
		var btn := _make_button(row, Roles.DATA[role]["name"], _on_role_pressed.bind(role))
		btn.toggle_mode = true
		btn.add_theme_color_override("font_color", Roles.DATA[role]["color"])
		role_btns[role] = btn
	role_desc = _make_label(box, "", 14)
	role_desc.custom_minimum_size = Vector2(0, 60)
	var row2 := HBoxContainer.new()
	box.add_child(row2)
	start_btn = _make_button(row2, "Start game", _on_start_pressed)
	_make_button(row2, "Leave", leave_session)


func _show_menu(status: String) -> void:
	menu_status.text = status
	menu_root.visible = true
	lobby_root.visible = false


func show_lobby_screen() -> void:
	menu_root.visible = false
	lobby_root.visible = true
	_refresh_lobby_ui()


func on_game_started() -> void:
	menu_root.visible = false
	lobby_root.visible = false


func _refresh_lobby_ui() -> void:
	var is_host := multiplayer.is_server()
	var my_id := multiplayer.get_unique_id()
	var lines := []
	var ids := lobby_players.keys()
	ids.sort()
	for id in ids:
		var info: Dictionary = lobby_players[id]
		var tags := []
		if id == 1:
			tags.append("host")
		if id == my_id:
			tags.append("you")
		lines.append("• %s — %s  (%s)" % [info["name"], Roles.DATA[info["role"]]["name"], ", ".join(tags)])
	if lobby_players.size() < MAX_PLAYERS:
		lines.append("• waiting for a friend to join…")
	lobby_list.text = "\n".join(lines)

	var my_role: String = lobby_players.get(my_id, {}).get("role", "")
	for role in role_btns:
		role_btns[role].set_pressed_no_signal(role == my_role)
	role_desc.text = Roles.DATA[my_role]["desc"] if my_role != "" else ""

	start_btn.visible = is_host
	if is_host:
		var port := _port()
		lobby_info.text = "Hosting on port %d. Your friend should join one of these addresses:\n%s\n(Use a 100.x.x.x address if you both run Tailscale.)" % [port, ", ".join(_local_ipv4s())]
	else:
		lobby_info.text = "Connected. Waiting for the host to start the game."


func _local_ipv4s() -> PackedStringArray:
	var out := PackedStringArray()
	for addr in IP.get_local_addresses():
		if addr.count(".") == 3 and not addr.begins_with("127.") and not addr.begins_with("169.254."):
			out.append(addr)
	if out.is_empty():
		out.append("127.0.0.1")
	return out


func _port() -> int:
	var port := int(port_edit.text)
	return port if port > 0 and port < 65536 else DEFAULT_PORT


func _player_name() -> String:
	var n := name_edit.text.strip_edges().substr(0, 16)
	return n if n != "" else "Player"


# --- Connection handling ---------------------------------------------------

func _on_host_pressed() -> void:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_server(_port(), MAX_PLAYERS - 1)
	if err != OK:
		_show_menu("Could not host on port %d (error %d). Is another copy already hosting?" % [_port(), err])
		return
	multiplayer.multiplayer_peer = peer
	lobby_players = {1: {"name": _player_name(), "role": cli.get("role", "tank")}}
	show_lobby_screen()


func _on_join_pressed() -> void:
	var peer := ENetMultiplayerPeer.new()
	var err := peer.create_client(ip_edit.text.strip_edges(), _port())
	if err != OK:
		_show_menu("Could not start connecting (error %d)." % err)
		return
	multiplayer.multiplayer_peer = peer
	menu_status.text = "Connecting to %s:%d…" % [ip_edit.text.strip_edges(), _port()]


func _on_connected_to_server() -> void:
	register_player.rpc_id(1, _player_name(), cli.get("role", ""))


func _on_connection_failed() -> void:
	_reset_network()
	_show_menu("Could not connect. Check the address and port, and that the host is in the lobby.")


func _on_server_disconnected() -> void:
	game.stop()
	_reset_network()
	_show_menu("Disconnected from the host (the game may already have been in progress).")


func _on_peer_disconnected(id: int) -> void:
	if not multiplayer.is_server():
		return
	lobby_players.erase(id)
	game.on_peer_left(id)
	_broadcast_lobby()


func _reset_network() -> void:
	if multiplayer.multiplayer_peer:
		multiplayer.multiplayer_peer.close()
	multiplayer.multiplayer_peer = OfflineMultiplayerPeer.new()
	lobby_players.clear()


func leave_session() -> void:
	game.stop()
	_reset_network()
	_show_menu("")


@rpc("any_peer", "call_remote", "reliable")
func register_player(pname: String, wanted_role: String) -> void:
	if not multiplayer.is_server():
		return
	var id := multiplayer.get_remote_sender_id()
	if game.running or lobby_players.size() >= MAX_PLAYERS:
		multiplayer.multiplayer_peer.disconnect_peer(id)
		return
	var role := wanted_role
	if not Roles.DATA.has(role):
		var taken := []
		for info in lobby_players.values():
			taken.append(info["role"])
		role = "dps"
		for r in Roles.ORDER:
			if r not in taken:
				role = r
				break
	var clean := pname.strip_edges().substr(0, 16)
	lobby_players[id] = {"name": clean if clean != "" else "Player", "role": role}
	_broadcast_lobby()
	if cli.has("autostart") and lobby_players.size() >= int(cli["autostart"]):
		_on_start_pressed()


func _broadcast_lobby() -> void:
	sync_lobby.rpc(lobby_players)
	if lobby_root.visible:
		_refresh_lobby_ui()


@rpc("authority", "call_remote", "reliable")
func sync_lobby(players: Dictionary) -> void:
	lobby_players = players
	if not game.running:
		show_lobby_screen()


func _on_role_pressed(role: String) -> void:
	if multiplayer.is_server():
		_set_role(1, role)
	else:
		request_role.rpc_id(1, role)
		# Keep the button showing the confirmed role until the host replies.
		_refresh_lobby_ui()


@rpc("any_peer", "call_remote", "reliable")
func request_role(role: String) -> void:
	if multiplayer.is_server():
		_set_role(multiplayer.get_remote_sender_id(), role)


func _set_role(id: int, role: String) -> void:
	if lobby_players.has(id) and Roles.DATA.has(role):
		lobby_players[id]["role"] = role
		_broadcast_lobby()


func _on_start_pressed() -> void:
	if multiplayer.is_server():
		game.host_start(lobby_players)

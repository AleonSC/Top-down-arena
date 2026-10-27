#!/usr/bin/env python3
"""Summarize recorded sessions for balancing.

Usage:
    python report.py <session.json | folder> [more files or folders...]

Turn recording on in the game under Settings > Developer (or launch with --record); the host saves
one JSON file per match. Pass one file to look at a single run, or a folder to see trends across
many runs (boss kill times, what downs people, which upgrades get picked, ...).
"""
import json
import os
import sys
from collections import defaultdict


def load(paths):
    runs = []
    for path in paths:
        files = [os.path.join(path, f) for f in sorted(os.listdir(path)) if f.endswith(".json")] if os.path.isdir(path) else [path]
        for f in files:
            try:
                with open(f, encoding="utf-8") as fh:
                    run = json.load(fh)
                run["_file"] = os.path.basename(f)
                runs.append(run)
            except (OSError, ValueError) as e:
                print(f"skipping {f}: {e}", file=sys.stderr)
    return runs


def table(headers, rows, title=None):
    if title:
        print(f"\n== {title} " + "=" * max(0, 70 - len(title)))
    if not rows:
        print("  (nothing recorded)")
        return
    cells = [[str(h) for h in headers]] + [[fmt(c) for c in r] for r in rows]
    widths = [max(len(r[i]) for r in cells) for i in range(len(headers))]
    for n, r in enumerate(cells):
        print("  " + "  ".join(c.ljust(widths[i]) if i == 0 else c.rjust(widths[i]) for i, c in enumerate(r)))
        if n == 0:
            print("  " + "  ".join("-" * w for w in widths))


def fmt(v):
    if isinstance(v, float):
        return f"{v:,.1f}"
    if isinstance(v, int):
        return f"{v:,}"
    return str(v)


def mean(xs):
    xs = list(xs)
    return sum(xs) / len(xs) if xs else 0.0


def main():
    if len(sys.argv) < 2:
        print(__doc__)
        sys.exit(1)
    runs = load(sys.argv[1:])
    if not runs:
        print("No sessions found.")
        sys.exit(1)

    # --- Runs -------------------------------------------------------------------
    rows = []
    for r in runs:
        team = ", ".join(f"{p['class']} {p['role']} ({p['weapon']})" for p in r["players"])
        end = r["end"]
        rows.append([r["_file"], r["playerCount"], team, end["outcome"], end["wave"], f"{end['time'] / 60:.1f} min"])
    table(["session", "players", "team", "outcome", "wave", "length"], rows, f"Runs ({len(runs)})")

    # --- Bosses -----------------------------------------------------------------
    bosses = defaultdict(list)
    for r in runs:
        for b in r["bosses"]:
            bosses[b["name"]].append((r, b))
    rows = []
    for name, fights in sorted(bosses.items(), key=lambda kv: min(b["wave"] for _, b in kv[1])):
        killed = [b for _, b in fights if b["killed"]]
        rows.append([
            name, len(fights), f"{100 * len(killed) / len(fights):.0f}%",
            mean(b["timeToKill"] for b in killed), mean(b["maxHp"] for _, b in fights),
            mean(b["playerDamageTaken"] for _, b in fights), mean(b["downs"] for _, b in fights),
        ])
    table(["boss", "fights", "killed", "avg time to kill (s)", "avg max HP", "avg dmg to players", "avg downs"], rows, "Boss fights")

    # --- Waves ------------------------------------------------------------------
    by_wave = defaultdict(list)
    for r in runs:
        for w in r["waves"]:
            by_wave[w["wave"]].append(w)
    rows = []
    for n in sorted(by_wave):
        ws = by_wave[n]
        per_player = [p for w in ws for p in w["players"].values()]
        rows.append([
            n, len(ws), mean(w["duration"] for w in ws if w["duration"] >= 0),
            mean(sum(w["spawned"].values()) for w in ws),
            mean(p["damageTaken"] for p in per_player), mean(100 * p["damageTaken"] / max(1, p["maxHp"]) for p in per_player),
            sum(p["downs"] for p in per_player), mean(p["lowHpSeconds"] for p in per_player),
            ws[0]["boss"] or "",
        ])
    table(["wave", "runs", "avg length (s)", "avg enemies", "dmg taken / player", "% of max HP", "downs", "low-HP s / player", "boss"],
          rows, "Difficulty by wave (watch for spikes)")

    # --- What downs people --------------------------------------------------------
    downs = defaultdict(int)
    for r in runs:
        for d in r["downs"]:
            downs[d["source"]] += 1
    table(["source", "downs"], sorted(downs.items(), key=lambda kv: -kv[1]), "What downs players")

    taken = defaultdict(float)
    for r in runs:
        for w in r["waves"]:
            for p in w["players"].values():
                for src, v in p["damageTakenFrom"].items():
                    taken[src] += v
    total = sum(taken.values()) or 1
    table(["source", "damage", "share"], [[s, v, f"{100 * v / total:.1f}%"] for s, v in sorted(taken.items(), key=lambda kv: -kv[1])[:20]],
          "Where damage to players comes from (top 20)")

    # --- Enemies ----------------------------------------------------------------
    enemies = defaultdict(lambda: defaultdict(float))
    for r in runs:
        for name, e in r["enemyTypes"].items():
            for k in ("spawned", "killed", "damageToPlayers"):
                enemies[name][k] += e[k]
            enemies[name]["lifetime"] += e["avgLifetime"] * e["killed"]
    rows = []
    for name, e in enemies.items():
        rows.append([name, int(e["spawned"]), int(e["killed"]), e["lifetime"] / max(1, e["killed"]),
                     e["damageToPlayers"], e["damageToPlayers"] / max(1, e["spawned"])])
    table(["enemy", "spawned", "killed", "avg lifetime (s)", "dmg to players", "dmg per spawn"],
          sorted(rows, key=lambda r: -r[5]), "Enemy lethality (most dangerous per spawn first)")

    # --- Upgrades ----------------------------------------------------------------
    offered, picked = defaultdict(int), defaultdict(int)
    kinds = {}
    for r in runs:
        for p in r["picks"]:
            for o in p["offered"]:
                offered[o] += 1
                kinds[o] = p["kind"]
            picked[p["picked"]] += 1
    rows = [[u, kinds[u], offered[u], picked[u], f"{100 * picked[u] / offered[u]:.0f}%"] for u in offered]
    rows.sort(key=lambda r: (-picked[r[0]] / offered[r[0]], -offered[r[0]]))
    table(["upgrade", "kind", "offered", "picked", "pick rate"], rows,
          "Upgrade pick rates (high = maybe too strong, low = maybe too weak; mods are all offered at once)")
    rerolls = [p["rerolls"] for r in runs for p in r["players"]]
    print(f"\n  rerolls used per player per run: {mean(rerolls):.1f}")

    # --- Abilities and parry -------------------------------------------------------
    uses = defaultdict(int)
    parry = defaultdict(float)
    for r in runs:
        for w in r["waves"]:
            for p in w["players"].values():
                for a, n in p["abilitiesUsed"].items():
                    uses[a] += n
                parry["blocked"] += p["parryBlocked"]
                parry["perfect"] += p["perfectParries"]
                parry["breaks"] += p["parryBreaks"]
    table(["ability", "uses"], sorted(uses.items(), key=lambda kv: -kv[1]), "Ability uses")
    print(f"\n  parry: {parry['blocked']:,.0f} damage blocked, {int(parry['perfect'])} perfect parries, {int(parry['breaks'])} breaks")

    # --- Roles -------------------------------------------------------------------
    roles = defaultdict(lambda: defaultdict(float))
    for r in runs:
        info = {str(p["id"]): f"{p['class']} {p['role']}" for p in r["players"]}
        team_dmg = defaultdict(float)
        for w in r["waves"]:
            for pid, p in w["players"].items():
                team_dmg[pid] += p["damageDealt"]
        total_dmg = sum(team_dmg.values()) or 1
        for pid, d in team_dmg.items():
            key = info.get(pid, "?")
            roles[key]["runs"] += 1
            roles[key]["share"] += d / total_dmg
            for w in r["waves"]:
                p = w["players"].get(pid)
                if p:
                    for k in ("damageDealt", "damageTaken", "healingDone", "downs"):
                        roles[key][k] += p[k]
    rows = [[k, int(v["runs"]), f"{100 * v['share'] / v['runs']:.0f}%", v["damageDealt"] / v["runs"], v["damageTaken"] / v["runs"],
             v["healingDone"] / v["runs"], v["downs"] / v["runs"]] for k, v in roles.items()]
    table(["class + role", "runs", "team dmg share", "dmg dealt / run", "dmg taken / run", "healing / run", "downs / run"],
          sorted(rows, key=lambda r: r[0]), "Classes and roles")


if __name__ == "__main__":
    main()

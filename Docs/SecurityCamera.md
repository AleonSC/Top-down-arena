# Security camera

**Status: planned** (no code yet). Mockup: `Docs/Mockups/security-camera.html` (approximate HTML; the owner picked design A, Dome).

A room hazard: a camera in a room's inner corner sweeps a search light. If it catches a player it calls enemies in near itself. Shooting it down still calls some, but fewer.

## The owner's picks

1. **Look: Dome.** A black dome on a triangular corner plate, a glowing lens that points along the light, and a soft amber light cone (gold idle, red on alarm).
2. **Rules as proposed:**
   - Spawns in an inner room corner in about 1 in 3 rooms, from wave 4.
   - **Lock-on:** when the light touches a player it stops sweeping and follows them; 0.7 s in the light raises the alarm. Stepping out drains the lock (gold ring round the camera).
   - **Alarm:** 4 of the faction's basic enemy come in through red spawn rings near the camera (0.9 s telegraph), then a 3 s cooldown before it sweeps again.
   - **Shot down** (6 hits): it calls 2, then stays a smoking wreck. Coins on kill.
   - It never blocks bullets, and enemies ignore it.

## Steps

1. **Core:** a new `EnemyType` at the end (before `None`); corner placement; sweep, lock, alarm, cooldown and wreck state; the spawn rings (reusing the spawn portal's "loading" ring); snapshot sync and a protocol bump; test suite.
2. **Look:** `Game/SecurityCameraLook.cs`: the dome, cone, lock ring, alarm pulse and wreck; a preview switch; README section.
3. **Bots and tuning:** bots step out of the light or shoot the camera; session report counters.

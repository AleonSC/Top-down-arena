# Pixel sprites: design plan

Status: **proposal, nothing built.** Mockup: [`Mockups/pixel-sprites.html`](Mockups/pixel-sprites.html) (live
effects, three resolutions, a 1:1 game-size view). Nothing here changes the game until the owner picks a look and an
approach (see *Decisions needed*).

## The idea

Players, enemies and ability bodies are drawn as a **grid of square pixels** instead of (or on top of) the vector
shapes they use now. A grid gives every pixel its own life, so the effects the vector looks can't do come cheap:

| Effect | What each pixel does |
|---|---|
| Death: disintegrate | breaks off in a wave from the side the killing hit came from, flies away, fades through white to ember |
| Spawn / teleport / Vanish | gathers from scatter in a random order, or is revealed row by row under a scan line |
| Shockwave passing | pushed outward and lit as a ring crosses it |
| Hit | white flash, then a few chips fly off the side that was hit |
| Burn / wet / stun | colour shift, plus embers rising, drips falling, jitter and orbiting sparks |

## Scope: render only

The simulation does **not** become a grid. `Core/` keeps positions, radii, `BodyR` and the network snapshot as they
are, so hit detection, `SimTest`, `RenderCheck` and the `Session.ProtocolVersion` all stay put (no new message, no
bump). The pixel grid is a way to *draw* a thing, and every pixel effect is a **stateless function of
(pixel, age, aim)**, the same way the existing looks draw from an effect's age. Nothing is stored or sent per pixel.

## Decisions needed (owner)

1. **Is the look wanted at all?** The arena is "phosphor vector / CRT" now. Pixels can sit inside that (lit like
   phosphor dots, with scanlines and bloom over them), but it's a change in style. See the mockup's chunky/fine
   toggle and judge it.
2. **How fine?** Grid resolution is the main look choice (below).
3. **Which approach to getting the pixels** (A or B below).
4. **Rotation:** does the figure's grid rotate with the aim (cells re-sampled each frame, as in the mockup) or turn
   in 8/16 fixed steps? Re-sampling is smooth but shimmers; stepped looks most like pixel art.
5. **Scope of the pilot:** one player model and one enemy (recommended) before touching anything else.

## Resolution

A Tank is `BodyR = 18` px, so a figure is about 36-44 px across at 1:1. A grid cell must be a whole number of screen
pixels to stay crisp:

| Grid | Cell | Reads as | Notes |
|---|---|---|---|
| 16 x 16 | ~2.5 px | chunky, retro | best disintegrate; loses the weapon detail and the role trim |
| 24 x 24 | ~1.7 px | fine pixel art | shows trim, hands, held weapon |
| 32 x 32 | ~1.3 px | barely pixels at 1:1 | the dust effect is nicer than the sprite |

At 1:1 the cell is 2 px or less, so the pixel look reads mostly **when things move**: the dissolve, the burst and the
scatter, not the resting body. That is good news for the plan (the animation is the point) and a warning against
spending effort on resting-pose detail. Bosses and big enemies get more cells, not bigger cells, so the cell size
stays one number across the game (the pixel look stays consistent).

## Getting the pixels: two approaches

**A. Hand-authored grids.** Each model is a small palette-indexed grid, stored as data (`Assets/Data/Sprites/*.px`,
built into C# like the waves are, `Tools/WaveGen`-style). Held weapons are separate grids drawn in their own frame as
`CharacterModels.cs` does today. Total control of the look, but every class x weapon x enemy needs art, and a
rework of a model means redrawing. About 60 enemies and bosses, 15+ classes, 30+ weapons.

**B. Rasterise the existing vector models (recommended to prototype).** Keep `CharacterModels.cs` and the enemy
looks as the source of truth. Draw the model into a small offscreen buffer at one cell per pixel (a point-filtered
`RenderTexture`, or a CPU buffer with the `FillRect` calls redirected), read the lit cells back into a list, and
feed the list to the pixel effects. The costume, palette and weapon changes already in the game carry across for
free, and a model rework updates the pixel look automatically. Costs: a per-entity raster step (cached while the
entity doesn't turn or change, which for a given aim step is most frames) and a rule for when to re-raster.

Either way the output is the same: a list of `(x, y, colour)` cells, and the effect code doesn't care which.

## Build outline

1. `Game/PixelFx.cs`: the stateless effects from the mockup, `ApplyFx(cells, fx, age, aim) -> draw ops`. Pure math,
   no Unity beyond `FillRect`, so it is covered by `CompileCheck` and a `Preview` shot.
2. `Game/PixelBody.cs`: produces the cell list for an entity (approach A: lookup and rotate; approach B: raster and cache).
3. Hook into the existing paths without removing them: a Visual effects setting (as the other looks have: on,
   Reduced, off). **Off keeps today's vector model**; Reduced uses the pixel body with no scatter, only a fade.
4. Pilot: the Soldier and one swarmer enemy, death + spawn + hit. Then decide whether to go on.
5. Later, only if the pilot works: burn / wet / stun overlays, Vanish and Shadow Step, shockwave ripple for
   Tsunami and boss slams, then more models.

## Cost and risks

- **Draw cost.** Drawing is immediate-mode `GL.Begin(GL.TRIANGLES)` (`WorldRenderer.cs`), a cell is one `FillRect`
  (two triangles). A 24 x 24 body is about 300 lit cells, 600 triangles; twenty enemies dying in one frame is
  around 12k triangles, fine. Cell lists are cached per entity and aim step to avoid re-sampling.
- **Network and sim:** none (see *Scope*). Effects are drawn on every peer from replicated age and aim.
- **Determinism:** the per-pixel noise comes from a hash of the cell position, not `Random`, so peers and
  replays agree and `RenderCheck` can diff frames.
- **Style clash** with the CRT look: the main risk, and decision 1.
- **Hit readability.** The "bright hit core" must stay visible and the cell grid must not drift from `BodyR`,
  or players will feel unfair hits. Keep the core as a separate vector dot on top of the pixels.
- **Accessibility:** Reduced effects drops scatter, flashes and jitter (fade only).
- **Reference pages** (Bestiary, Armory) stay as they are; they will be out of date if this ships, which is the
  owner's call to refresh.

## Open questions

- Do **ability** bodies (zones, domes, shields, clones) become pixels too, or only things with a figure? Zones and
  big areas are already effect-heavy; pixels would help dissolves but hurt smooth gradients.
- Do **bullets** become single cells? That fits the pixel look and is the cheapest thing to try.
- Do **players and enemies share one cell size** with the backdrop and UI, or does the world get a low-res pass
  (renders everything at half or third resolution and upscales)? A whole-world pass is the most consistent and the
  biggest change; not recommended for a pilot.

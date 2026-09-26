# Unit sprite workflow

The first reference is **A (pixel art)** in [the knight style sheet](art/sprites/knight-style-options.png), selected by Nick. The game uses the existing Knight definition; the art does not define stats or outcomes.

## Generate

Use the built-in image-generation tool in the coding conversation. No API key or runtime image-generation service is needed. Supply the [approved knight sprite](../client/src/assets/sprites/knight/knight.png) as the primary style reference for every new unit. If also supplying the original style sheet, identify A explicitly and ignore B and C. Record each unit's complete prompt with its assets. Get a human visual pick before implementing a new art direction.

The exact historical knight prompt is saved in [the authoring record](art/sprites/knight-prompt.md). New units use the [shared art specification](art/sprites/unit-art-spec.md). Keep its camera, pixel treatment, lighting and proportion rules; choose the new unit's silhouette, equipment and accent ramp explicitly. Blue cloth is the knight's accent, not a requirement for every unit.

Reusable prompt:

Fill in every bracket below and paste the selected palette rows from the shared art specification into the prompt. Attach the knight image; a repository path alone is not an image reference for the generator.

> Create a production 2D pixel-art attack sprite sheet on a genuinely transparent RGBA background. Use the attached approved knight as the visual standard for chunky proportions, apparent pixel size, dark outlines, shading detail and camera. The new character is [unit description, body shape, clothing and exact equipment]. Its distinguishing silhouette is [silhouette]. Its standing body height is [1.0 times the knight for an ordinary humanoid; explicit ratio for other bodies], excluding raised weapons, horns and hats. Ordinary humanoids are approximately three heads tall, matching the reference. Preserve the reference's coarse square pixel clusters and approximately one logical pixel dark contour; do not increase detail for a larger source image. Light comes from the upper screen-left, with hard-edged shadow, midtone and highlight clusters and small bright metal highlights. Use these material ramps in shadow-to-highlight order: [paste selected material ramps and one accent ramp with hex values]. Keep large surfaces in these ramps, reserve the accent for [cloth/body markings], and use the shared darkest colour for outlines. Show the SAME character in four distinct coherent sequential poses in reading order: top-left idle [description], top-right wind-up [description], bottom-left strike toward screen RIGHT [description], bottom-right recovery [description]. Keep anatomy, clothing, equipment, colours, camera and body scale identical across all poses. Face screen RIGHT with the reference's slightly elevated three-quarter camera. Arrange a square sheet in a regular 2-by-2 grid of equal square cells. Place the foot anchor at 50 percent across and 82 percent down each cell, with the complete character and weapon sweep inside the middle 84 percent of cell width. Do not shrink individual poses to fit. Use crisp square pixels, no smoothing, gradients, painterly textures, scenery, ground or cast shadows, labels, borders or grid lines. Exactly one character per cell; no clipped limbs or weapons, no painted checkerboard, no redesigns between poses.

Use attack descriptions appropriate to the unit: a bow draw and release for an archer, or a bite for a creature. These poses illustrate server events and do not define combat mechanics.

Generated grids and pixel placement are approximate. Inspect the actual output, including real alpha transparency, before setting the manifest. If a pose changes the equipment or character, edit with the chosen sheet as a reference and request only that correction. Preserve previous versions.

## Prepare

For new biped walk cycles, start with the [reusable walk template](art/sprites/biped-walk-template.md). The [knight authoring record](art/sprites/walk-cycle-workflow.md) preserves the exact prompts, reference images, corrections, and accepted coloured loop that informed it.

The selected metal walk is stored losslessly as four individual `knight-walk-metal-1.png` through `knight-walk-metal-4.png` files under `client/src/assets/sprites/knight/`. Playback order is near-leg passing, near-leg contact, far-leg passing, far-leg contact, at 160 ms per frame. Each RGBA canvas is 1254 × 1254 with a full-canvas region and a common foot anchor of (627, 1080); display scale is 0.1. The historical metadata calls the near/sword leg `left` and the far/shield leg `right`; these identities do not swap with screen position. `/sprites` uses these metal frames. `/walk-review.html?play=1` retains the large loop and coloured comparisons. The selected individual frames supersede the original two-candidate 2×2 sheet plan; battle movement remains separate.

- Put the chosen original transparent PNG under `client/src/assets/sprites/<unit>/`.
- Define frame regions, foot anchors, display scale and attack durations in a typed manifest under `client/src/sprites/`. Register the unit id in `spriteCatalog.ts`.
- Regions are source-image pixels. Anchors are relative to each region, measured at the feet. All frames use the same scale. The renderer places each anchor at the same board point.
- The knight source is 1254 by 1254, with measured frame regions rather than an assumed exact 2-by-2 grid. The wider left regions preserve the extended sword. Lower-row anchors compensate for vertical drift in the generated layout.
- Left-facing art is mirrored around the foot anchor. This also mirrors sword/shield handedness. Separate left-facing art is needed if anatomical handedness becomes a requirement.
- Keep the PNG alpha channel. A checkerboard painted into an image is not transparency. Keep the original and source prompt; do not rely on an expiring artifact link.
- AI-styled pixels are not guaranteed to follow a strict pixel grid. Future production cleanup can snap/repaint the source; do not smooth it when rendering.

## Preview

From `client/`:

```powershell
npm ci
npm run dev -- --host 0.0.0.0
```

Open `/sprites` for the preview inside the app, or `/sprite-preview.html` for the isolated preview entry. Neither preview needs the API. The preview starts idle, offers left/right and pose controls, and can repeat the attack. The smaller display uses the board's SVG scale. Actual apparent size on the responsive board depends on viewport width.

Nick reviews the running preview. Check the foot stability, complete weapon sweep, palette/equipment consistency, direction, and readability at board size on both dark and light backgrounds. Do not use browser automation to self-verify the visuals.

For phone review, export the same Vue preview with its assets embedded:

```powershell
npm run export:sprites
orca artifacts share ./dist/sprite-preview.html --json
```

This outputs a standalone interactive HTML artifact. The exporter losslessly recompresses PNG data and packs it more compactly than base64 in the JavaScript bundle to fit Orca's CLI transport. Tests compare the decompressed pixel bytes, ancillary metadata and checksums against the original PNG; the committed source is untouched. Use the returned share URL on the phone. Orca must be signed in and public artifact publishing enabled. The artifact contains only the preview and embedded art; no game API or credentials. Artifacts expire, so the repository is the durable source.

## Battle playback

`UnitSprite.vue` is shared by the preview and SVG battle board. Non-sprite units keep their existing rendering. Labels, HP, team circles and placement interaction remain available.

Server `unitAttacked` events trigger the visual sequence during play or a one-tick forward step. The target's post-tick position determines left/right; a lethal attack uses the recorded death position before falling back to the pre-tick position. Hex row offsets count when deciding screen direction.

The knight sequence is wind-up 100 ms, strike 80 ms, recovery 140 ms, then idle. These are presentation durations only: damage is applied exactly as supplied by the server log. Like projectiles, a started attack finishes visually during pause; it does not advance the battle. Seeking clears transient animation, except a genuine single forward step can trigger that tick's attacks. Restart/new battle clears old attacks.

Server `unitMoved` events drive the walk loop the same way: leftPassing, leftContact, rightPassing, rightContact at 160 ms per frame, looping for as long as consecutive ticks keep moving that unit, then decaying back to idle. Attack always wins over walking for the same unit. Seeking, pause, restart and reset clear transient walk presentation exactly like attacks - the loop's cadence is a presentation detail, not a simulation timing rule.

## Verify

```powershell
npm run type-check
npm run test -- --run
npm run lint
npm run build
npm run export:sprites
```

Tests cover frame boundaries, PNG dimensions/format, manifest regions, facing, lethal attacks, event filtering, overlapping attacks, and seek/reset decisions. Human visual review remains required.

# Unit sprite workflow

The first reference is **A (pixel art)** in [the knight style sheet](art/sprites/knight-style-options.png), selected by Nick. The game uses the existing Knight definition; the art does not define stats or outcomes.

## Generate

Use the built-in image-generation tool in the coding conversation. No API key or runtime image-generation service is needed. Show the reference image to the tool and identify A explicitly. Generate other units against this same reference and record their prompts with their assets. Get a human visual pick before implementing a new art direction.

The exact knight prompt is saved in [the authoring record](art/sprites/knight-prompt.md). For another unit, retain its camera, coarse pixel style, silhouette, blue cloth and restrained palette conventions, then change the unit description and equipment as appropriate.

Reusable prompt:

> Create a transparent 2D pixel-art sprite sheet using the selected reference for style. Show the SAME [unit description] in four distinct sequential poses: idle ready stance, wind-up, strike toward screen right, and recovery. Face right with a slightly elevated three-quarter camera in every pose. Keep proportions, palette, clothing and equipment identical. Use crisp square pixels and no antialiasing, scenery, labels, frame borders or cast shadows. Arrange a 2-by-2 grid with equal cells, consistent foot anchors and ample room for the weapon sweep. Do not clip any weapon or limb.

Generated grids and pixel placement are approximate. Inspect the actual output, including real alpha transparency, before setting the manifest. If a pose changes the equipment or character, edit with the chosen sheet as a reference and request only that correction. Preserve previous versions.

## Prepare

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

## Verify

```powershell
npm run type-check
npm run test -- --run
npm run lint
npm run build
npm run export:sprites
```

Tests cover frame boundaries, PNG dimensions/format, manifest regions, facing, lethal attacks, event filtering, overlapping attacks, and seek/reset decisions. Human visual review remains required.

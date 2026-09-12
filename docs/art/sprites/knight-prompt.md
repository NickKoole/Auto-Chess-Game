# Knight authoring record

Tool: built-in image generation. Selected visual reference: A in [knight-style-options.png](knight-style-options.png).
Output: [knight.png](../../../client/src/assets/sprites/knight/knight.png).
No API model id was exposed by the tool.

## Exact prompt

Use case: stylized-concept. Asset type: production 2D pixel-art knight attack spritesheet. Reference image is a STYLE AND CHARACTER REFERENCE: use ONLY knight A, the pixel art knight at the LEFT. Ignore B and C entirely. Create one image on a genuinely transparent background with FOUR frames in a perfectly regular 2-column by 2-row grid of equal square cells. No grid lines, no labels, no letters, no text, no background, no ground shadows, no cast shadows, no frame borders. The four frames show exactly the same compact steel-armored knight with blue tabard, enclosed helmet, brown belt, small blue shield with brown rim, short straight sword, matching A's chunky old-school pixel-art aesthetic and modest detail. Every frame faces screen RIGHT with the same slightly elevated three-quarter camera. Sharp square pixels, restricted consistent palette, no smoothing, no painterly or vector shading. Frames in reading order: top-left idle ready stance, sword relaxed diagonally behind; top-right WIND-UP sword lifted back above shoulder ready to slash right; bottom-left STRIKE sword extended horizontally to screen RIGHT beyond shield, clear active attacking pose and slight forward lean; bottom-right RECOVERY sword lowered after slash, returning to ready. Identical helmet, armor, sword length, shield, palette, camera, body scale, and feet baseline across all four frames. Foot anchor is centered horizontally and 82 percent down WITHIN EACH CELL. Character including sword stays within each cell's middle 84 percent width with generous transparent margins for weapon sweep. No duplicate knights within any cell. Make four actual distinct coherent sequential poses of one knight, not four redesigns. Overall square image.

## Preparation

The generated source has actual RGBA transparency and is preserved without raster edits. It did not follow the requested uniform grid exactly. Rendering uses explicit frame rectangles and individual foot anchors in the knight manifest; it never resizes each pose independently. The strike reads as a forward thrust in the generated output. Nick reviews the final pose motion in the live preview.

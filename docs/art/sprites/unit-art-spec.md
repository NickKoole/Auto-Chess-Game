# Shared unit art specification

The [approved knight](../../../client/src/assets/sprites/knight/knight.png) is the visual reference. The rules below formalise its style for future units. Palette ramps and numerical proportion targets are proposed authoring guidance, pending Nick's review of new samples; they do not recolour the existing knight or establish a new approved art direction.

## Palette

Use a shared set of material ramps, plus one dominant accent ramp per unit. Values run from shadow to highlight. Steel and blue include sampled knight colours; the other values are curated approximations or proposed extensions, not an exhaustive extraction of the source's many colour variations.

| Role | Shadow | Midtone | Highlight | Use |
| --- | --- | --- | --- | --- |
| Outline | `#06070D` | — | — | Shared dark contour and deepest crevices |
| Steel | `#616576` | `#B7B5BD` | `#F3F1E8` | Armour, blades and small bright details |
| Leather | `#38251F` | `#76503A` | `#AD7B54` | Belts, boots and grips |
| Wood | `#513522` | `#92613A` | `#C99659` | Bows, shafts and wooden equipment |
| Gold | `#93602D` | `#CE9148` | `#F9BD62` | Small fittings and trim |
| Blue accent | `#052A70` | `#043891` | `#1362E7` | Knight cloth; available to other units |
| Green accent | `#203C2D` | `#3E7045` | `#79A65B` | Optional cloth or creature markings |
| Red accent | `#592632` | `#A34446` | `#D97862` | Optional cloth or creature markings |
| Purple accent | `#352846` | `#69477E` | `#AA79B2` | Optional cloth or creature markings |

Select only the materials the character needs. Keep gold and the brightest steel to small areas. Use three main shades per material, without gradients or noisy texture. Accent colours identify the art, not gameplay teams; existing team indicators remain authoritative.

For skin, fur, bone or another material not covered here, specify a three-colour ramp in the unit's authoring record before generation and present it with the sample for review. Different skin tones can use different ramps. Do not force organic bodies into a metal ramp.

Hex codes communicate targets; generation is not guaranteed to reproduce them exactly. Review hue, contrast and material consistency against the knight at board size. Exact palette enforcement would require a separate cleanup step; retain the original generated image and record any later edits.

## Drawing and scale

The default board direction is warm stone, selected by Nick from [the arena mockup](../../../client/board-mockup.html). Keep sprites readable against the muted stone tiles and dark grout, using clear midtone and highlight clusters around dark materials. Team circles carry allegiance; sprite colours are independent of team. Future travel biomes may change the environment. Board materials use `--arena-*` theme variables, separate from team colours; biome selection itself is future work.

- Match the knight's slightly elevated three-quarter camera and screen-right facing. Light comes from upper screen-left in the source art; the renderer's left-facing mirror also mirrors that lighting.
- Match its coarse square pixel clusters and approximately one logical pixel dark outline. A logical pixel means a visible art block, not one pixel in the large generated PNG. More source resolution must not add finer detail.
- Use hard-edged shading with clear light/dark separation. Metal may have small bright glints; cloth, leather and wood should have broader, simpler shading. No soft gradients, texture noise or cast shadows.
- Ordinary humanoids should be approximately three heads tall and match the knight's standing body height at board scale. Keep heads, hands and equipment chunky enough to read at that size.
- Define non-humanoid proportions and an explicit height ratio against the knight before generation. Exclude weapons, horns and hats when comparing body height. Preserve that body scale across poses; allow a wider silhouette for larger bodies instead of shrinking individual frames.
- Give each unit a recognisable silhouette through its body and equipment. Use the knight as a style reference without copying its helmet, shield or costume onto unrelated units.

## Per-unit authoring record

Record the attached references, full expanded prompt, chosen material/accent ramps, body-height ratio, equipment, silhouette and four pose descriptions. Record the output path and any deviations discovered during preparation. Keep camera, drawing and palette instructions unchanged between units unless Nick selects a revised direction.

## First comparison samples

Validate the reusable prompt with these two proposed samples before treating it as proven across the roster:

| Sample | Proposed design | What it checks |
| --- | --- | --- |
| Archer | Ordinary humanoid, 1.0 knight body height, green cloth, leather, wooden bow; ready, draw, release, recover | Consistent proportions and materials with less armour and a different attack |
| Creature | Compact quadruped, 0.65 knight body height, proposed purple body ramp and small steel-ramp claws; ready, pull back, bite, recover | Shared pixel treatment and lighting across different anatomy |

These are art tests, not new game unit definitions. Present samples for Nick's explicit visual pick before adding them to the client. Review beside the knight in the running preview at board size: apparent pixel size, outlines, body scale, palette, readable silhouette, stable feet and coherent equipment across all poses. Do not self-verify visuals with browser tools.

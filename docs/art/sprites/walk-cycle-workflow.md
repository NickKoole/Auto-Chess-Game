# Walk-cycle authoring with coloured leg references

For another unit, start with the [reusable biped template](biped-walk-template.md). This file is the knight's historical authoring record, including earlier poses and the corrections that produced the accepted study.

## What improved, and what remains

The knight's first generated walk sheets repeatedly kept the same foot forward. Longer prompts, explicit left/right instructions, and separate frame generation did not reliably fix this. Naming the legs by their equipment helped describe intent but did not make their identity easy to follow in the generated images.

The useful change was to temporarily colour the near leg yellow and the far leg purple, generate one contact pose at a time, then edit those images to make the passing poses. The outputs now visibly alternate the coloured feet at contact and alternate the airborne foot during passing. A targeted correction folded the yellow shin back after its first passing pose looked like a kick.

Nick accepted the shorter-stride coloured loop after reviewing playback on 2026-09-18. Steel colours were restored in a separate set of frames, and Nick subsequently requested shipping the produced metal images. These four frames are now selected in the app preview as documented in [the sprite workflow](../../SPRITES.md). The technique does not guarantee identical results for another unit: review hip attachments, leg lengths, frame alignment, and flow again.

## Coordinate and identity rules

- Forward/backward mean screen-right/screen-left for this right-facing knight.
- Near/far mean drawing depth, not horizontal position.
- Leg A is the original sword-side leg. It is yellow and always drawn over the other leg at overlaps.
- Leg B is the original shield-side leg. It is purple and always drawn behind the other leg at overlaps.
- These identities and hip attachments persist when the feet exchange horizontal positions. Swapping colours on an unchanged pose is not a valid step.
- In an in-place cycle, the supporting foot moves backward relative to the fixed body while remaining on the baseline. The returning foot lifts, bends at the knee, and travels forward.
- This study uses grounded walking motion. A true run requires an airborne phase and a separate motion brief.

## Repeatable process

1. Choose one idle pose as the identity reference. If the source is an attack sheet, explicitly select its idle cell; do not use the attack poses as gait references.
2. Generate one contact pose with coloured legs. Check both foot positions, hip attachments, and overlap order.
3. Edit that output into the opposite contact pose. Compare the two before proceeding.
4. Generate a passing pose from each contact image, keeping the same colours and depth order. Use the actual contact image as the edit target, not just its filename in the text.
5. Make a narrowly scoped correction when a specific joint or foot is wrong. For example, change only the swinging shin and boot while keeping the supporting leg fixed.
6. Review the four images in sequence and in a loop. Keep source canvases intact; measure any alignment adjustment instead of individually resizing figures by eye. Check at a slow cadence and at intended playback size/speed.
7. After the human visually approves the coloured motion, ask whether to restore the original colours and materials, following the [template's final recolouring step](biped-walk-template.md#final-step-request-recolouring-after-visual-inspection). Wait for that request unless already given. Then restore the materials while preserving the approved leg geometry, and present the restored loop for visual review because recolouring can also alter shapes.

Use the built-in image-generation edit workflow. Inspect local input images before editing them, attach the actual image, retain alpha, and preserve previous outputs. Include a direct image link in every response that delivers a new generated image. Keep approved study images and their prompts in the repository rather than relying on machine-specific generated-image paths.

Requests to preserve every pixel are instructions, not an exact-preservation guarantee. Compare the outputs. Adding more restrictive prose alone did not solve the original failure.

## Saved study and playback

The current four images are preserved with the [review page](../../../client/public/walk-review.html). Run the client dev server and open `/walk-review.html`; this page needs no backend. It provides a row of frames, looping playback, speed selection, and previous/next controls.

The accepted coloured sequence uses `frame-1.png`, `frame-2-compact.png`, `frame-3.png`, and `frame-4-compact.png`, at 160 ms per frame. Select **Coloured · shorter stride** to inspect it. The default is now **Metal · shorter stride**; open `/walk-review.html?play=1` for automatic playback. The table below records the original generation inputs; the contact corrections and metal restoration are preserved below.

| Playback order | Image | Generation input |
| --- | --- | --- |
| 1. Yellow passing | [Frame 1](../../../client/public/walk-review/frame-1.png) | Opposite contact, then the lower-leg correction below |
| 2. Yellow forward contact | [Frame 2](../../../client/public/walk-review/frame-2.png) | Original knight's top-left idle pose |
| 3. Purple passing | [Frame 3](../../../client/public/walk-review/frame-3.png) | Yellow forward contact |
| 4. Purple forward contact | [Frame 4](../../../client/public/walk-review/frame-4.png) | Yellow forward contact |

Generation order differs from playback order: create the two contacts first, then the passing poses. Frame 4 loops into frame 1.

The prompts below record the actual successful study steps, including the correction. For another unit, explicitly replace equipment-side references and choose distinguishable temporary leg colours.

## Prompt 1: yellow forward contact

Input: the [original knight sheet](../../../client/src/assets/sprites/knight/knight.png).

```text
Use only the top-left knight as the reference. Produce one knight in one pose.

Temporarily colour the entire sword-side leg yellow and the entire shield-side leg purple, including their boots. These colours identify the legs for animation planning.

Change the stance so the yellow leg reaches diagonally across the body, with its boot planted at the rightmost end of the stride. The purple leg extends backward, with its boot planted at the leftmost end.

The yellow leg must visibly overlap and obscure the purple leg where they cross. Both soles touch the same ground line.

Keep the upper body and equipment as in the reference. Transparent background.
```

## Prompt 2: purple forward contact

Input: Prompt 1's output (playback frame 2). Output: playback frame 4.

```text
Edit this coloured knight study into the OPPOSITE CONTACT POSE of the same walking cycle. Output one isolated knight in one pose.

Change only the two legs and feet. Keep the head, torso, pelvis, arms, sword, shield, pixel-art style, body scale and image position identical to the supplied image.

Leg identities persist: the yellow leg is the near/sword-side leg attached to the same near hip. The purple leg is the far/shield-side leg attached to the same far hip. Do not exchange their hip attachments or merely repaint the legs.

Move the YELLOW leg backward: extend it diagonally toward SCREEN-LEFT, with its yellow boot planted at the leftmost end of the stride.
Move the PURPLE leg forward: extend it diagonally toward SCREEN-RIGHT, with its purple boot planted at the rightmost end of the stride.

Both boot soles touch the same ground baseline as the reference. Use the same stride width. Keep yellow in the foreground and purple in the background: wherever the thighs, knees or shins overlap, the yellow leg covers the purple leg. The purple forward leg emerges from behind the yellow near leg.

This is the opposite of the reference's yellow-forward/purple-back contact. Required visible result: yellow boot at screen-left, purple boot at screen-right, same fixed hip attachments and yellow-over-purple depth order.

Transparent background. No labels, borders, ground line, checkerboard or extra objects.
```

## Prompt 3: yellow passing

Input: Prompt 2's output (playback frame 4). This first result needed Prompt 5's correction before becoming playback frame 1.

```text
Edit this exact sprite into the next passing pose of a rightward walk in place. One knight only, transparent background, identical canvas, framing, scale, equipment, and upper body. Only the coloured legs and feet move.

In this source the YELLOW leg is the near leg trailing left; the PURPLE leg is the far leg leading right. Preserve their hip attachment points, their colours and their lengths. Yellow is always drawn over purple at overlaps.

Advance the motion halfway toward the opposite contact:
- PURPLE is the supporting leg. Move its boot backward from screen-right to directly beneath the hips, sole flat on the original ground baseline. Its knee is gently bent to support the fixed pelvis.
- YELLOW is the swinging leg. Lift its boot off the ground, bend its knee, and bring the yellow knee forward toward screen-right. Its airborne boot passes close to the supporting ankle, clearly above the ground, with the shin folding backward from the forward knee. Draw the swinging yellow leg IN FRONT OF the purple support leg.
The pose must have a narrow passing silhouette: the feet are close horizontally, not at the wide stride extremes. The yellow sole has visible air beneath it; only the purple sole touches the ground. Keep the pelvis at its existing location. Do not recolour or exchange the leg identities. No labels, ground line, shadow, checkerboard or extra objects.
```

## Prompt 4: purple passing

Input: Prompt 1's output (playback frame 2). Output: playback frame 3.

```text
Edit this exact sprite into the next passing pose of a rightward walk in place. One knight only, transparent background, identical canvas, framing, scale, equipment, and upper body. Only the coloured legs and feet move.

In this source the YELLOW leg is the near leg leading right; the PURPLE leg is the far leg trailing left. Preserve their hip attachment points, colours and lengths. Yellow is always drawn over purple wherever they overlap.

Advance the motion halfway toward the opposite contact:
- YELLOW is the supporting leg. Move its boot backward from screen-right to directly beneath the hips, sole flat on the original ground baseline. Its knee is gently bent to support the fixed pelvis. Show a continuous yellow supporting leg from its unchanged near hip down to the grounded yellow boot.
- PURPLE is the swinging leg. Lift its boot off the ground, bend its knee, and bring the purple knee forward toward screen-right. Its airborne boot passes close to the yellow supporting ankle, clearly above the ground, with the shin folding backward from the forward knee.
The purple swinging leg passes BEHIND the yellow supporting leg. At every overlap, yellow covers purple. Portions of the purple thigh and shin may be hidden behind yellow; do not move its hip attachment to make it fully visible.

The pose must have a narrow passing silhouette: feet close horizontally, not at wide stride extremes. The purple sole has visible air beneath it; only the yellow sole touches the ground. Keep the pelvis at its existing location. Do not recolour or exchange leg identities. No labels, ground line, shadow, checkerboard or extra objects.
```

## Prompt 5: correct the yellow passing shin

Input: Prompt 3's first output. Output: playback frame 1. Its first version extended the shin forward; this correction folded it back. The discarded intermediate is not required to reuse the workflow: apply this correction if a new passing pose shows the same problem.

```text
Make one tightly scoped correction to this exact knight sprite. Keep the upper body, canvas, position, equipment, pelvis and entire PURPLE supporting leg exactly unchanged. Keep the YELLOW hip and thigh unchanged. Only bend the yellow knee more and reposition its lower leg and boot.

The current yellow shin extends down-and-right and looks like a kick. Instead fold the yellow shin DOWN-AND-LEFT from its existing forward knee. Bring the yellow boot back close to the purple supporting ankle, suspended above the baseline by about one boot height. The knee stays ahead at screen-right, the ankle is behind the knee toward screen-left. The toes still point screen-right. This must read as the bent-leg passing phase of walking.

Yellow is the NEAR leg, so the yellow shin and boot cover the purple leg wherever they overlap. Purple remains the far leg with its boot planted on the original baseline. Maintain the original leg lengths and pixel-art shading. Output one isolated sprite with true transparent background. No labels or extra objects.
```

## Restoring the steel palette

After the coloured shorter-stride loop was accepted, each of its four images was edited separately with built-in image generation. Image 1 was the accepted coloured frame; image 2 was the original knight sheet used only for materials. The restored frames still need human review in motion; the colour edit does not guarantee pixel-identical geometry.

The review page now defaults to **Metal · shorter stride**. The accepted coloured study remains available under **Coloured · shorter stride**, and the older wide stance remains available for comparison.

| Playback position | Coloured edit target | Metal output |
| --- | --- | --- |
| 1 | [Near passing](../../../client/public/walk-review/frame-1.png) | [Metal 1](../../../client/public/walk-review/frame-1-metal.png) |
| 2 | [Near contact](../../../client/public/walk-review/frame-2-compact.png) | [Metal 2](../../../client/public/walk-review/frame-2-metal.png) |
| 3 | [Far passing](../../../client/public/walk-review/frame-3.png) | [Metal 3](../../../client/public/walk-review/frame-3-metal.png) |
| 4 | [Far contact](../../../client/public/walk-review/frame-4-compact.png) | [Metal 4](../../../client/public/walk-review/frame-4-metal.png) |

The same exact prompt was used for all four:

```text
Recolour IMAGE 1 only. It is an approved walking pose; its leg geometry and silhouette must be preserved. IMAGE 2 is only a MATERIAL AND COLOUR reference: use the top-left metal knight's steel leg plates and brown leather joints. Do not copy any stance or pose from image 2.

Remove ALL temporary yellow and purple from the two legs and feet in image 1. Restore neutral silver/grey steel armour on thighs, knees, shins and armoured boot surfaces, with small dark-brown leather joint/strap areas matching the reference. Use the same cool grey shadows and silver highlights as the existing torso. The far leg may be slightly darker for depth, but neither leg is brightly coloured.

This is a colour/material edit, not a pose edit. Preserve every leg outline, hip attachment, knee angle, foot position, overlap, pixel cluster boundary, baseline and image alignment from IMAGE 1. Preserve its head, torso, pelvis, arms, sword, gold equipment trim, shield, size and canvas exactly. Only change the coloured leg/foot pixels below the hip armour. Do not change the gold on the shield, belt or sword. Keep the original lifted/planted feet exactly as shown. Preserve transparency; do not include a checkerboard or ground shadow. One isolated knight only.
```

## Human review before finishing

Check the colour of the leading foot alternates, the airborne foot alternates, and yellow remains in front of purple at overlaps. Follow each leg back to the same hip through all frames. Check knee bending, limb length, stable body scale, baseline, and the frame 4-to-1 transition. Then inspect at board size and restore the production colours only after the motion is accepted.

## Contact revision: shorter stride

The initial contact frames read as a rigid triangle in playback. A later pass shortened their horizontal spread and requested slight knee flexion plus leading-heel contact and trailing-toe push-off. Nick accepted the resulting coloured loop after watching it play. The passing poses are unchanged.

The review page retains both coloured stride variants for comparison. Revised assets: [yellow contact](../../../client/public/walk-review/frame-2-compact.png), [purple contact](../../../client/public/walk-review/frame-4-compact.png).

Yellow-forward edit, applied to original playback frame 2:

```text
Edit only the coloured legs and feet of this knight into a more natural, compact walking contact pose. Preserve the exact upper body, pelvis position, equipment, canvas, apparent pixel size, body scale, transparent background and ground level.

Yellow is the near leg, attached to its existing near hip; purple is the far leg, attached to its existing far hip. Yellow always overlaps purple where they cross. Keep YELLOW leading toward screen-right, PURPLE trailing toward screen-left.

Correct the wide triangular stance: shorten the stride to about HALF its present horizontal heel-to-heel separation. Keep the thighs hanging mostly downward from their hips, knees closer together beneath the pelvis, not splayed out sideways. The legs swing fore-and-aft in the walking direction. Preserve their lengths; use slight natural knee flexion rather than shortening limbs.

Place the yellow leading heel a modest distance ahead of the hips, just making ground contact, with its toe slightly raised. The purple trailing forefoot/toes touch the same ground level a modest distance behind the hips, with its heel slightly lifted as it finishes pushing off. Both boots point toward screen-right. The asymmetry of heel strike and toe-off is essential: avoid two identical flat boots and two rigid straight diagonal legs.

One isolated knight. Only redraw the legs below the hip armour. No ground line, shadows, labels, borders, checkerboard, or additional figures.
```

Purple-forward edit, applied to original playback frame 4:

```text
Edit only the coloured legs and feet of this knight into a more natural, compact walking contact pose. Preserve the exact upper body, pelvis position, equipment, canvas, apparent pixel size, body scale, transparent background and ground level.

Yellow is the near leg, attached to its existing near hip; purple is the far leg, attached to its existing far hip. Yellow always overlaps purple where they cross. Keep PURPLE leading toward screen-right, YELLOW trailing toward screen-left.

Correct the wide triangular stance: shorten the stride to about HALF its present horizontal heel-to-heel separation. Keep the thighs hanging mostly downward from their hips, knees closer together beneath the pelvis, not splayed out sideways. The legs swing fore-and-aft in the walking direction. Preserve their lengths; use slight natural knee flexion rather than shortening limbs.

Place the purple leading heel a modest distance ahead of the hips, just making ground contact, with its toe slightly raised. The yellow trailing forefoot/toes touch the same ground level a modest distance behind the hips, with its heel slightly lifted as it finishes pushing off. Both boots point toward screen-right. The asymmetry of heel strike and toe-off is essential: avoid two identical flat boots and two rigid straight diagonal legs.

One isolated knight. Only redraw the legs below the hip armour. No ground line, shadows, labels, borders, checkerboard, or additional figures.
```

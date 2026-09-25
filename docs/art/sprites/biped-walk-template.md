# Reusable biped walk authoring template

Use this for a two-legged unit with visible legs, a fixed side or three-quarter camera, and a four-frame grounded walk in place. It does not depend on a knight, weapon, armour, or particular production palette. Quadrupeds, flight, true running with airborne phases, and cameras that change leg visibility need a different gait brief.

This template is derived from the [accepted knight motion study](walk-cycle-workflow.md). The exact knight prompts and images are preserved there; this parameterized version has not yet been validated on another unit.

## Why the workflow helped

Generating a complete sheet repeatedly left the same foot forward. Instead, use temporary contrasting colours to keep each leg identifiable, separate drawing depth from travel direction, and make each pose as an edit of a named reference image. Establish opposite contacts first, then passing poses. Short strides, soft knees, heel contact, and trailing-toe push-off avoid the rigid triangular contact stance. A folded returning shin avoids a kicking motion.

## Set these parameters first

| Parameter | Meaning |
| --- | --- |
| `[UNIT]` | Unit name or description |
| `[IDLE]` | One selected idle pose, identified explicitly if inside a sheet |
| `[NEAR_HIP]` | Unambiguous description of the hip nearest the camera in that pose |
| `[FAR_HIP]` | Unambiguous description of the other hip |
| `[NEAR_COLOUR]` | Temporary colour for the entire near leg, including its foot |
| `[FAR_COLOUR]` | A clearly contrasting temporary colour for the entire far leg |
| `[FORWARD]` | Screen-right or screen-left |
| `[BACKWARD]` | The opposite screen direction |
| `[STRIDE]` | Modest heel-to-heel separation suited to this unit, specified relative to its feet/body or measured in source pixels |

Choose guide colours distinct from each other and easy to distinguish from the body. They are authoring aids and are removed later. Keep the same parameter values throughout. Do not rename a leg based on its current foot position.

## Shared prompt for every edit

Attach the actual input image specified in the next section. Replace all brackets with the chosen values.

```text
Create one pose of [UNIT] walking in place toward [FORWARD]. Use the attached image as the direct edit target. Preserve its upper body, equipment, pelvis position, camera, scale, canvas dimensions, ground baseline and pixel-art treatment. Change only the legs and feet. Output one isolated character on a transparent background.

Leg A is attached at [NEAR_HIP] and coloured [NEAR_COLOUR]. Leg B is attached at [FAR_HIP] and coloured [FAR_COLOUR]. These identities and hip attachments persist as the feet move. At every overlap, A is drawn over B. Keep the original limb lengths. Forward means [FORWARD]; backward means [BACKWARD]. Drawing depth is independent of horizontal position. Do not exchange hip attachments or merely swap leg colours.
```

For the first edit only, also say: "Use only [IDLE] as the character reference. Temporarily colour all of leg A, including its foot, [NEAR_COLOUR], and all of leg B, including its foot, [FAR_COLOUR]."

## Generate contacts, then passing poses

Use the shared prompt plus the appropriate pose prompt. Save each result separately and retain its input image and fully expanded prompt. Generate one image per edit.

| Generation step | Direct image input | Pose | Playback position |
| --- | --- | --- | --- |
| 1 | Selected idle | A forward contact | 2 |
| 2 | A forward contact | B forward contact | 4 |
| 3 | B forward contact | A passing | 1 |
| 4 | A forward contact | B passing | 3 |

### Contact prompt

For A contact, substitute A as `[LEADING]` and B as `[TRAILING]`. Reverse those roles for B contact, without changing their hip attachments or drawing depth.

```text
[LEADING] steps forward toward [FORWARD]; [TRAILING] finishes its step behind toward [BACKWARD]. Use a compact stride with heel-to-heel separation [STRIDE]. Keep thighs mostly downward and knees close beneath the pelvis, with gentle flexion rather than two rigid diagonals spreading into a triangle. Preserve limb lengths.

The leading heel meets the ground a modest distance ahead of the body with toes slightly raised. The trailing forefoot touches the same ground baseline behind the body with its heel lifted for push-off. Both feet point toward [FORWARD]. Keep the asymmetric heel-contact/toe-off shape clear.
```

For the second contact edit, add: "This is the opposite contact: the previously trailing leg now leads and the previously leading leg now trails. A still overlaps B."

### Passing prompt

For A passing, substitute A as `[SWINGING]` and B as `[SUPPORTING]`. Reverse those roles for B passing.

```text
Advance the supplied contact pose halfway toward the opposite contact. [SUPPORTING] moves its foot backward to beneath the hips, flat on the baseline, with a softly bent supporting knee. Keep the body in place.

[SWINGING] lifts its rear foot and brings its knee forward. Fold its shin down and backward from that knee so its airborne foot passes close to the supporting ankle, about one foot height above the ground. Keep the toes pointing forward. The feet are close horizontally during passing. Only the supporting foot touches the ground.

A remains in front of B at overlaps. When B swings, allow parts of it to disappear behind supporting leg A instead of moving its hip to expose it.
```

## Correct individual failures

- **Same leg leads twice:** return to the opposite contact edit. Identify the two colours and trace each leg to its hip before proceeding.
- **Contact resembles a triangle:** shorten the current heel-to-heel separation, soften knees, and specify leading heel versus trailing toes. About half the previous separation helped the knight; do not assume that factor fits every unit.
- **Returning leg looks like a kick:** edit only that shin and foot. Keep its knee forward, fold the shin backward, and bring the foot close to the supporting ankle. Preserve the supporting leg and upper body.
- **Scale or baseline drifts:** inspect original canvas positions and measure alignment. Do not independently enlarge or shrink each frame to fill a preview tile.

Make one targeted correction at a time. An instruction to preserve every pixel does not guarantee exact preservation; inspect the actual output.

## Review and preserve

Play **A passing → A contact → B passing → B contact → repeat**. Start around 160 ms per frame for comparison with the knight, then choose the cadence for the unit. Review slowly and at board size. Check alternating lead feet, alternating airborne feet, stable hip attachments, A-over-B depth, baseline, limb lengths and the loop boundary.

Provide a page with a row of images, loop playback, previous/next controls and speed adjustment, following the [knight review page](../../../client/public/walk-review.html). The human reviews motion in the running page. Include a direct link for every newly generated image.

Keep the guide colours during motion review and any further gait corrections. Once the human has visually accepted the coloured loop, follow the final recolouring step below.

Save the following with each unit so another session can reproduce and inspect the work:

- Source idle image and parameter values.
- Fully expanded prompts, direct input image for each edit, and any correction prompts.
- Accepted coloured frames, production frames when ready, and their explicit playback order.
- Canvas sizes, measured anchors/offsets, playback timing and preview entry point.
- Human review outcome and what remains unfinished.

Keep files in the repository and link them from the unit's authoring record. Do not rely on local generated-image cache paths or this conversation as the only record.

## Final step: request recolouring after visual inspection

After the human has inspected and accepted the coloured loop in playback, ask:

> Shall I restore [UNIT]'s original colours and materials on all four frames, preserving the accepted gait?

Wait for their answer before recolouring. Acceptance of the motion alone does not request colour restoration. If the human has already explicitly requested recolouring, proceed without asking again.

Once requested, edit each accepted frame separately. Attach that coloured frame as image 1 and the original unit sprite as image 2. Use image 2 only for its palette and materials. Preserve the coloured originals for comparison.

```text
Recolour image 1 only. It is an accepted walking pose. Image 2 supplies only the original colours and materials of [UNIT]; do not copy its pose.

Replace [NEAR_COLOUR] and [FAR_COLOUR] on the legs and feet with the original materials and shading from image 2. Preserve the accepted silhouettes, hip attachments, limb lengths, knee angles, foot positions, overlap order, pixel boundaries, ground baseline, canvas and alignment of image 1. Keep its upper body and equipment unchanged. Preserve transparency. Output one recoloured frame in the same pose.
```

Save the recoloured frames and the expanded prompt, update the preview, and provide direct image links plus a link to the restored loop. Ask for visual inspection of that loop before treating the recoloured asset as accepted; generation can alter geometry despite preservation instructions.

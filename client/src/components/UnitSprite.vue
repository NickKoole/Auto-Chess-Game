<script setup lang="ts">
import { computed } from 'vue'
import { scaleFor, type SpriteDefinition, type SpriteFacing, type SpritePresentation } from '@/sprites/spriteAnimation'

const props = withDefaults(
  defineProps<{
    sprite: SpriteDefinition
    presentation?: SpritePresentation
    facing?: SpriteFacing
    x?: number
    y?: number
  }>(),
  { presentation: () => ({ kind: 'attack', pose: 'idle' }), facing: 'right', x: 0, y: 0 },
)

const frame = computed(() =>
  props.presentation.kind === 'walk'
    ? props.sprite.walk.frames[props.presentation.phase]
    : props.sprite.frames[props.presentation.pose],
)
const sheet = computed(() =>
  props.presentation.kind === 'walk' ? props.sprite.walkSheets[props.presentation.phase] : props.sprite.sheet,
)
const scale = computed(() => scaleFor(props.sprite, props.presentation))
const transform = computed(() =>
  `translate(${props.x}, ${props.y}) scale(${props.facing === 'left' ? -1 : 1}, 1)`,
)
</script>

<template>
  <g :transform="transform" aria-hidden="true" style="pointer-events: none">
    <svg
      :x="-frame.anchorX * scale"
      :y="-frame.anchorY * scale"
      :width="frame.width * scale"
      :height="frame.height * scale"
      :viewBox="`${frame.x} ${frame.y} ${frame.width} ${frame.height}`"
      overflow="hidden"
    >
      <image
        :href="sheet"
        :width="sprite.width"
        :height="sprite.height"
        style="image-rendering: pixelated"
      />
    </svg>
  </g>
</template>

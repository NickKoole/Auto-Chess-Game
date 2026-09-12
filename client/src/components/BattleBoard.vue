<script setup lang="ts">
import { computed, onUnmounted, ref, watch } from 'vue'
import type { BattleState, BattleUnitState, HexPosition } from '@/battle/battleState'
import { currentTargetOf, SIDE_PLAYER, unitLabel } from '@/battle/battleState'
import { isInPlayerBand } from '@/battle/composition'
import { gridDimensions, hexPolygonPoints, hexToPixel } from '@/battle/hexCoords'
import { PROJECTILE_VISIBLE_MS, type TimedProjectile } from '@/battle/projectiles'
import { facingTarget, type SpriteAttack } from '@/battle/spriteAttacks'
import { attackDuration, poseAtElapsed } from '@/sprites/spriteAnimation'
import { spriteForUnit } from '@/sprites/spriteCatalog'
import UnitSprite from './UnitSprite.vue'

const props = withDefaults(
  defineProps<{
    state: BattleState
    interactive?: boolean
    projectiles?: TimedProjectile[]
    attacks?: SpriteAttack[]
    selectedUnitId?: string | null
  }>(),
  { projectiles: () => [], attacks: () => [], selectedUnitId: null },
)

const emit = defineEmits<{
  'tile-drop': [payload: { row: number; col: number; unitId: string }]
  'unit-delete': [payload: { row: number; col: number }]
}>()

// Mirrors GameSim's Battlefield: an 8x8 board, rows 3-4 neutral, rows 0-2 the
// opponent's deployment band (the far side), rows 5-7 the player's (the near
// side). Row 0 renders at the top because y grows with row - no flip needed.
const ROWS = 8
const COLS = 8
const NEUTRAL_FIRST_ROW = 3
const NEUTRAL_LAST_ROW = 4
const HEX_SIZE = 40

const dims = computed(() => gridDimensions(ROWS, COLS, HEX_SIZE))

interface HexTile {
  row: number
  col: number
  points: string
}

const hexes = computed<HexTile[]>(() => {
  const tiles: HexTile[] = []
  for (let row = 0; row < ROWS; row++) {
    for (let col = 0; col < COLS; col++) {
      const { x, y } = hexToPixel(row, col, HEX_SIZE)
      tiles.push({
        row,
        col,
        points: hexPolygonPoints(x + dims.value.originX, y + dims.value.originY, HEX_SIZE),
      })
    }
  }
  return tiles
})

function bandClass(row: number): string {
  const band =
    row >= NEUTRAL_FIRST_ROW && row <= NEUTRAL_LAST_ROW
      ? 'neutral-band'
      : row < NEUTRAL_FIRST_ROW
        ? 'opponent-band'
        : 'player-band'
  const droppable = props.interactive && isInPlayerBand(row) ? ' droppable' : ''
  return `hex ${band}${droppable}`
}

function onDrop(event: DragEvent, row: number, col: number): void {
  if (!props.interactive) {
    return
  }
  const unitId = event.dataTransfer?.getData('text/plain')
  if (!unitId) {
    return
  }
  emit('tile-drop', { row, col, unitId })
}

function onTileClick(row: number, col: number): void {
  if (!props.interactive) {
    return
  }
  if (props.selectedUnitId) {
    emit('tile-drop', { row, col, unitId: props.selectedUnitId })
    return
  }
  const occupant = props.state.units.find(
    (unit) => unit.side === SIDE_PLAYER && unit.position.row === row && unit.position.col === col,
  )
  if (occupant) {
    emit('unit-delete', { row, col })
  }
}

function positionCenter(position: HexPosition) {
  const { x, y } = hexToPixel(position.row, position.col, HEX_SIZE)
  return { x: x + dims.value.originX, y: y + dims.value.originY }
}

// Drives projectile motion directly rather than via SMIL/CSS, whose timing
// is unreliable for elements created dynamically well after page load.
const now = ref(performance.now())
let animationFrame: number | undefined

function tick(): void {
  now.value = performance.now()
  animationFrame = requestAnimationFrame(tick)
}

const hasAnimatingSprites = computed(() => props.attacks.some((attack) => {
  const unit = props.state.units.find((candidate) => candidate.id === attack.unitId)
  const sprite = unit && spriteForUnit(unit.definitionId)
  return sprite && now.value - attack.startedAt < attackDuration(sprite)
}))

const unitVisuals = computed(() => props.state.units.map((unit) => {
  const sprite = spriteForUnit(unit.definitionId)
  const attack = props.attacks.find((candidate) => candidate.unitId === unit.id)
  const target = currentTargetOf(props.state, unit)
  const fallback = unit.side === SIDE_PLAYER ? 'right' : 'left'
  const facing = attack?.facing ?? (target ? facingTarget(unit.position, target.position, fallback) : fallback)
  const pose = sprite ? poseAtElapsed(sprite, attack ? now.value - attack.startedAt : null) : 'idle'
  return { unit, sprite, pose, facing }
}))

watch(
  () => props.projectiles.length > 0 || hasAnimatingSprites.value,
  (hasAnimation) => {
    if (hasAnimation && animationFrame === undefined) {
      animationFrame = requestAnimationFrame(tick)
    } else if (!hasAnimation && animationFrame !== undefined) {
      cancelAnimationFrame(animationFrame)
      animationFrame = undefined
    }
  },
  { immediate: true },
)

onUnmounted(() => {
  if (animationFrame !== undefined) {
    cancelAnimationFrame(animationFrame)
  }
})

function projectilePosition(projectile: TimedProjectile) {
  const progress = Math.min(1, Math.max(0, (now.value - projectile.firedAt) / PROJECTILE_VISIBLE_MS))
  const from = positionCenter(projectile.from)
  const to = positionCenter(projectile.to)
  return { x: from.x + (to.x - from.x) * progress, y: from.y + (to.y - from.y) * progress }
}

function projectileAngleDegrees(projectile: TimedProjectile): number {
  const from = positionCenter(projectile.from)
  const to = positionCenter(projectile.to)
  return (Math.atan2(to.y - from.y, to.x - from.x) * 180) / Math.PI
}

function projectileTransform(projectile: TimedProjectile): string {
  const { x, y } = projectilePosition(projectile)
  return `translate(${x}, ${y}) rotate(${projectileAngleDegrees(projectile)})`
}

function unitCenter(unit: BattleUnitState) {
  return positionCenter(unit.position)
}

function sideClass(unit: BattleUnitState): string {
  return unit.side === SIDE_PLAYER ? 'unit player' : 'unit opponent'
}

function label(unit: BattleUnitState): string {
  if (unit.isPreview) {
    return props.state.unitDefinitions[unit.definitionId]?.displayName ?? unit.definitionId
  }
  return unitLabel(unit, props.state.unitDefinitions)
}

function targetLabel(unit: BattleUnitState): string | null {
  const target = currentTargetOf(props.state, unit)
  return target ? `→#${target.id}` : null
}

interface TargetMarker {
  attackerId: number
  x: number
  y: number
}

const targetMarkers = computed<TargetMarker[]>(() =>
  props.state.units.flatMap((unit): TargetMarker[] => {
    const target = currentTargetOf(props.state, unit)
    return target ? [{ attackerId: unit.id, ...unitCenter(target) }] : []
  }),
)
</script>

<template>
  <svg class="battle-board" :viewBox="`0 -32 ${dims.width} ${dims.height + 32}`" role="img" aria-label="Battle grid">
    <polygon
      v-for="hex in hexes"
      :key="`${hex.row}-${hex.col}`"
      :points="hex.points"
      :class="bandClass(hex.row)"
      @dragover.prevent
      @drop="onDrop($event, hex.row, hex.col)"
      @click="onTileClick(hex.row, hex.col)"
    />

    <circle
      v-for="marker in targetMarkers"
      :key="marker.attackerId"
      :cx="marker.x"
      :cy="marker.y"
      :r="HEX_SIZE * 0.78"
      class="target-marker"
    />

    <g v-for="{ unit, sprite, pose, facing } in unitVisuals" :key="unit.id" :data-unit-id="unit.id" class="unit-group">
      <circle :cx="unitCenter(unit).x" :cy="unitCenter(unit).y" :r="HEX_SIZE * 0.62" :class="sideClass(unit)" />
      <UnitSprite v-if="sprite" :sprite="sprite" :pose="pose" :facing="facing" :x="unitCenter(unit).x" :y="unitCenter(unit).y + 8" />
      <text :x="unitCenter(unit).x" :y="unitCenter(unit).y + (sprite ? -52 : -9)" text-anchor="middle" class="unit-label">
        {{ label(unit) }}
      </text>
      <text :x="unitCenter(unit).x" :y="unitCenter(unit).y + (sprite ? 20 : 3)" text-anchor="middle" class="unit-hp">
        {{ unit.hp }}
      </text>
      <text
        v-if="targetLabel(unit)"
        :x="unitCenter(unit).x"
        :y="unitCenter(unit).y + (sprite ? 30 : 14)"
        text-anchor="middle"
        class="unit-target-label"
      >
        {{ targetLabel(unit) }}
      </text>
    </g>

    <g v-for="p in props.projectiles" :key="p.id" :transform="projectileTransform(p)">
      <line x1="-10" y1="0" x2="4" y2="0" class="projectile-shaft" />
      <polygon points="9,0 1,-3.5 1,3.5" class="projectile-head" />
    </g>
  </svg>
</template>

<style scoped>
.battle-board {
  display: block;
  width: 100%;
  max-width: 640px;
  height: auto;
  margin: 0 auto;
  padding: 16px;
  background: var(--panel);
  border: 1px solid var(--panel-border);
  box-shadow: 0 14px 30px oklch(0 0 0 / 0.5);
  clip-path: polygon(0 10px, 10px 0, 100% 0, 100% calc(100% - 10px), calc(100% - 10px) 100%, 0 100%);
}

.hex {
  stroke-width: 1.4;
}

.opponent-band {
  fill: var(--opp-fill);
  stroke: var(--opp-stroke);
}

.neutral-band {
  fill: var(--neu-fill);
  stroke: var(--neu-stroke);
}

.player-band {
  fill: var(--ply-fill);
  stroke: var(--ply-stroke);
}

.droppable {
  cursor: copy;
}

.unit-group {
  /* A unit sits visually on top of its hex; without this it swallows the
     hex's dragover/drop instead of letting a drop on the unit reach it. */
  pointer-events: none;
}

.unit.player {
  fill: var(--unit-player);
  stroke: oklch(0.1 0 0);
  stroke-width: 1.5;
}

.unit.opponent {
  fill: var(--unit-enemy);
  stroke: oklch(0.1 0 0);
  stroke-width: 1.5;
}

.target-marker {
  fill: none;
  stroke: var(--accent);
  stroke-width: 2;
  pointer-events: none;
}

.projectile-shaft {
  stroke: var(--panel-border);
  stroke-width: 1.5;
  pointer-events: none;
}

.projectile-head {
  fill: var(--accent);
  stroke: var(--panel-border);
  stroke-width: 1;
  pointer-events: none;
}

.unit-label,
.unit-hp {
  font-family: var(--font-body);
  font-size: 8px;
  fill: var(--text);
  pointer-events: none;
  user-select: none;
}

.unit-target-label {
  font-family: var(--font-body);
  font-size: 8px;
  fill: var(--text);
  pointer-events: none;
  user-select: none;
}
</style>

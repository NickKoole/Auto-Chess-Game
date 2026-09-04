<script setup lang="ts">
import { computed } from 'vue'
import type { BattleState, BattleUnitState } from '@/battle/battleState'
import { currentTargetOf, SIDE_PLAYER, unitLabel } from '@/battle/battleState'
import { gridDimensions, hexPolygonPoints, hexToPixel } from '@/battle/hexCoords'

const props = defineProps<{ state: BattleState }>()

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
  if (row >= NEUTRAL_FIRST_ROW && row <= NEUTRAL_LAST_ROW) {
    return 'hex neutral-band'
  }
  return row < NEUTRAL_FIRST_ROW ? 'hex opponent-band' : 'hex player-band'
}

function unitCenter(unit: BattleUnitState) {
  const { x, y } = hexToPixel(unit.position.row, unit.position.col, HEX_SIZE)
  return { x: x + dims.value.originX, y: y + dims.value.originY }
}

function sideClass(side: number): string {
  return side === SIDE_PLAYER ? 'unit player' : 'unit opponent'
}

function label(unit: BattleUnitState): string {
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
  <svg class="battle-board" :viewBox="`0 0 ${dims.width} ${dims.height}`" role="img" aria-label="Battle grid">
    <polygon v-for="hex in hexes" :key="`${hex.row}-${hex.col}`" :points="hex.points" :class="bandClass(hex.row)" />

    <circle
      v-for="marker in targetMarkers"
      :key="marker.attackerId"
      :cx="marker.x"
      :cy="marker.y"
      :r="HEX_SIZE * 0.78"
      class="target-marker"
    />

    <g v-for="unit in props.state.units" :key="unit.id" :data-unit-id="unit.id" class="unit-group">
      <circle :cx="unitCenter(unit).x" :cy="unitCenter(unit).y" :r="HEX_SIZE * 0.62" :class="sideClass(unit.side)" />
      <text :x="unitCenter(unit).x" :y="unitCenter(unit).y - 9" text-anchor="middle" class="unit-label">
        {{ label(unit) }}
      </text>
      <text :x="unitCenter(unit).x" :y="unitCenter(unit).y + 3" text-anchor="middle" class="unit-hp">
        {{ unit.hp }}
      </text>
      <text
        v-if="targetLabel(unit)"
        :x="unitCenter(unit).x"
        :y="unitCenter(unit).y + 14"
        text-anchor="middle"
        class="unit-target-label"
      >
        {{ targetLabel(unit) }}
      </text>
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
}

.hex {
  stroke: #333;
  stroke-width: 1;
}

.opponent-band {
  fill: #f8d7da;
}

.neutral-band {
  fill: #e2e2e2;
}

.player-band {
  fill: #d1e7dd;
}

.unit.player {
  fill: #2e7d32;
  stroke: #1b1b1b;
  stroke-width: 1;
}

.unit.opponent {
  fill: #c62828;
  stroke: #1b1b1b;
  stroke-width: 1;
}

.target-marker {
  fill: none;
  stroke: #f9a825;
  stroke-width: 2;
  pointer-events: none;
}

.unit-label,
.unit-hp {
  font-size: 8px;
  fill: #fff;
  pointer-events: none;
  user-select: none;
}

.unit-target-label {
  font-size: 7px;
  fill: #ff4081;
  pointer-events: none;
  user-select: none;
}
</style>

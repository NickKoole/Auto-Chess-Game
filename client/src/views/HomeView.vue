<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue'
import {
  listEnemySetups,
  listMatchups,
  listUnits,
  simulateBattle,
  type EnemySetupDto,
  type MatchupDto,
  type SimulateBattleRequest,
  type SimulateBattleResponse,
  type UnitDefinitionDto,
} from '@/api/battles'
import { toNumber } from '@/api/numeric'
import { applyTick, type BattleEvent } from '@/battle/applyTick'
import { cloneBattleState, createBattleState, type BattleState } from '@/battle/battleState'
import {
  canResetArmy,
  compositionToBattleState,
  isDeploymentEditable,
  placeUnit,
  removePlacementAt,
  toggleRosterSelection,
  type CompositionPlacement,
  type EnemyPreviewPlacement,
} from '@/battle/composition'
import { endingLabel, survivorsBySide } from '@/battle/outcome'
import { seekTo, terminatingTickOf } from '@/battle/playback'
import { projectilesForTick, PROJECTILE_VISIBLE_MS, type Projectile, type TimedProjectile } from '@/battle/projectiles'
import BattleBoard from '@/components/BattleBoard.vue'

// A 150-350 tick battle at 30ms/tick is a 5-10 second playback - watchable,
// not instant, not a slog.
const TICK_INTERVAL_MS = 30

const SIDE_LABELS: Record<number, string> = {
  0: 'Player',
  1: 'Opponent',
}

type BattleMode = 'matchup' | 'composition'
const mode = ref<BattleMode>('composition')

const matchups = ref<MatchupDto[]>([])
const matchupsError = ref<string | null>(null)
const selectedMatchupId = ref('')

const units = ref<UnitDefinitionDto[]>([])
const unitsError = ref<string | null>(null)

const enemySetups = ref<EnemySetupDto[]>([])
const enemySetupsError = ref<string | null>(null)
const selectedEnemySetupId = ref('')

const playerPlacements = ref<CompositionPlacement[]>([])
const placementError = ref<string | null>(null)
const selectedUnitId = ref<string | null>(null)

const seedInput = ref('')
const isRequesting = ref(false)

const outcome = ref<SimulateBattleResponse | null>(null)
const finalState = ref<BattleState | null>(null)
const error = ref<string | null>(null)
const battleState = ref<BattleState | null>(null)
const currentTick = ref(0)
const terminatingTick = ref(0)
const isPlaying = ref(false)
const hasFinished = ref(false)
const activeProjectiles = ref<TimedProjectile[]>([])
const projectileTimers = new Map<string, ReturnType<typeof setTimeout>>()

let timer: ReturnType<typeof setInterval> | undefined

function clearProjectiles(): void {
  for (const pending of projectileTimers.values()) {
    clearTimeout(pending)
  }
  projectileTimers.clear()
  activeProjectiles.value = []
}

// Appends rather than replacing, so projectiles fired on consecutive ticks
// can be in flight at once instead of each wiping out the last.
function showProjectiles(fired: Projectile[]): void {
  if (fired.length === 0) {
    return
  }
  const firedAt = performance.now()
  const timed = fired.map((projectile) => ({ ...projectile, firedAt }))
  activeProjectiles.value = [...activeProjectiles.value, ...timed]
  for (const projectile of timed) {
    const pending = setTimeout(() => {
      activeProjectiles.value = activeProjectiles.value.filter((p) => p.id !== projectile.id)
      projectileTimers.delete(projectile.id)
    }, PROJECTILE_VISIBLE_MS)
    projectileTimers.set(projectile.id, pending)
  }
}

function sideLabel(side: number): string {
  return SIDE_LABELS[side] ?? `Unknown (${side})`
}

const survivorGroups = computed(() => (finalState.value ? survivorsBySide(finalState.value) : []))

const unitDefinitionsById = computed<Record<string, UnitDefinitionDto>>(() => {
  const map: Record<string, UnitDefinitionDto> = {}
  for (const unit of units.value) {
    map[unit.id] = unit
  }
  return map
})

const selectedEnemySetup = computed(() => enemySetups.value.find((s) => s.id === selectedEnemySetupId.value))

const enemyPreviewPlacements = computed<EnemyPreviewPlacement[]>(
  () =>
    selectedEnemySetup.value?.placements.map((p) => ({
      definitionId: p.definitionId,
      position: { row: toNumber(p.position.row), col: toNumber(p.position.col) },
    })) ?? [],
)

const compositionPreviewState = computed(() =>
  compositionToBattleState(playerPlacements.value, unitDefinitionsById.value, enemyPreviewPlacements.value),
)

// Once a battle has actually run, the same board switches from the static
// preview to rendering that live result in place - never a second board.
const compositionBoardState = computed(() => battleState.value ?? compositionPreviewState.value)

// Drives both the Reset army button and the board's `interactive` prop.
const canEditDeployment = computed(() => isDeploymentEditable(battleState.value, currentTick.value, isPlaying.value))

function stopPlayback(): void {
  if (timer !== undefined) {
    clearInterval(timer)
    timer = undefined
  }
  isPlaying.value = false
}

// A leftover result from the other mode would otherwise show through as
// stale, and - for composition - leave the board's interactive prop wired to
// a battleState that will never clear, permanently blocking placement.
watch(mode, () => {
  clearBattleResult()
})

// Every control but Play routes through here: one code path for step,
// jump and scrub, all built on the same rebuild-from-tick-0 primitive. A
// projectile only ever shows for a genuine one-tick forward move - jumping
// or scrubbing to any other tick, including backward, clears it instead.
function seek(tick: number): void {
  const response = outcome.value
  if (!response) {
    return
  }

  const isForwardStep = tick === currentTick.value + 1
  // seekTo builds a fresh state rather than mutating this one, so it is a
  // genuine pre-tick snapshot - no cloning needed, unlike play()'s loop.
  const preTickState = battleState.value

  stopPlayback()
  const result = seekTo(response, tick)
  battleState.value = result.state
  currentTick.value = result.tick
  hasFinished.value = result.tick >= terminatingTick.value
  clearProjectiles()
  if (isForwardStep && result.tick === tick && preTickState) {
    showProjectiles(projectilesForTick(preTickState, result.state, tick, response.events as BattleEvent[]))
  }
}

function stepForward(): void {
  seek(currentTick.value + 1)
}

function stepBack(): void {
  seek(currentTick.value - 1)
}

function jumpToStart(): void {
  seek(0)
}

function jumpToEnd(): void {
  seek(terminatingTick.value)
}

function onScrub(event: Event): void {
  seek(Number((event.target as HTMLInputElement).value))
}

function play(): void {
  const response = outcome.value
  if (!response || isPlaying.value) {
    return
  }

  if (currentTick.value >= terminatingTick.value) {
    if (terminatingTick.value === 0) {
      return
    }
    // Finished (or never started): "Play"/"Play again" restarts from tick 0.
    // Otherwise this resumes a pause from wherever it was left.
    const result = seekTo(response, 0)
    battleState.value = result.state
    currentTick.value = result.tick
    clearProjectiles()
  }
  hasFinished.value = false

  // The generated event type is technically optional (see applyTick.ts);
  // the server always sends it, so this is the one place that trust is spent.
  const log = response.events as BattleEvent[]

  isPlaying.value = true
  timer = setInterval(() => {
    currentTick.value += 1
    // applyTick mutates battleState.value in place, so unlike seek()'s
    // rebuild-based path this needs an explicit snapshot to keep a real
    // pre-tick state to fall back on for a target this tick kills.
    const preTickState = cloneBattleState(battleState.value!)
    applyTick(battleState.value!, currentTick.value, log)
    showProjectiles(projectilesForTick(preTickState, battleState.value!, currentTick.value, log))
    if (currentTick.value >= terminatingTick.value) {
      stopPlayback()
      hasFinished.value = true
    }
  }, TICK_INTERVAL_MS)
}

function pause(): void {
  stopPlayback()
}

function resolvedSeed(): string | undefined {
  const trimmed = seedInput.value.trim()
  return trimmed === '' ? undefined : trimmed
}

async function runBattleWithRequest(request: SimulateBattleRequest): Promise<void> {
  // isRequesting stays true for the rest of this call, disabling the button
  // that is this function's only entry point — so no second call can start
  // until this one finishes, and a stale response can never land.
  isRequesting.value = true
  clearBattleResult()

  const result = await simulateBattle(request)

  isRequesting.value = false

  if (result.ok) {
    outcome.value = result.data
    battleState.value = createBattleState(result.data)
    terminatingTick.value = terminatingTickOf(result.data)
    finalState.value = seekTo(result.data, terminatingTick.value).state
    play()
  } else {
    error.value = result.error
  }
}

async function runMatchupBattle(): Promise<void> {
  if (!selectedMatchupId.value || isRequesting.value) {
    return
  }
  await runBattleWithRequest({ matchupId: selectedMatchupId.value, seed: resolvedSeed() })
}

async function runCompositionBattle(): Promise<void> {
  if (playerPlacements.value.length === 0 || !selectedEnemySetupId.value || isRequesting.value) {
    return
  }
  await runBattleWithRequest({
    enemySetupId: selectedEnemySetupId.value,
    playerPlacements: playerPlacements.value.map((p) => ({ unitId: p.unitId, position: p.position })),
    seed: resolvedSeed(),
  })
}

function onDragStart(event: DragEvent, unitId: string): void {
  event.dataTransfer?.setData('text/plain', unitId)
}

function onRosterClick(unitId: string): void {
  selectedUnitId.value = toggleRosterSelection(selectedUnitId.value, unitId)
}

// Discards a previous run's playback without touching playerPlacements.
function clearBattleResult(): void {
  stopPlayback()
  outcome.value = null
  finalState.value = null
  error.value = null
  battleState.value = null
  currentTick.value = 0
  terminatingTick.value = 0
  hasFinished.value = false
  clearProjectiles()
}

// Any edit to the placements invalidates a battle result still on screen.
watch(playerPlacements, () => {
  if (battleState.value) {
    clearBattleResult()
  }
})

// selectedUnitId is left as-is on a successful placement, so a roster
// selection is a toggle: once turned on, clicking the board keeps placing
// that unit type until the roster item is clicked again to turn it off.
function onTileDrop({ row, col, unitId }: { row: number; col: number; unitId: string }): void {
  const result = placeUnit(playerPlacements.value, unitId, { row, col })
  if (result.ok) {
    playerPlacements.value = result.placements
    placementError.value = null
  } else {
    placementError.value = result.reason
  }
}

function onUnitDelete({ row, col }: { row: number; col: number }): void {
  playerPlacements.value = removePlacementAt(playerPlacements.value, { row, col })
}

function resetArmy(): void {
  clearBattleResult()
  playerPlacements.value = []
  placementError.value = null
  selectedUnitId.value = null
}

async function loadMatchups(): Promise<void> {
  const result = await listMatchups()
  if (result.ok) {
    matchups.value = result.data
    selectedMatchupId.value = result.data[0]?.id ?? ''
  } else {
    matchupsError.value = result.error
  }
}

async function loadUnits(): Promise<void> {
  const result = await listUnits()
  if (result.ok) {
    units.value = result.data
  } else {
    unitsError.value = result.error
  }
}

async function loadEnemySetups(): Promise<void> {
  const result = await listEnemySetups()
  if (result.ok) {
    enemySetups.value = result.data
    selectedEnemySetupId.value = result.data[0]?.id ?? ''
  } else {
    enemySetupsError.value = result.error
  }
}

onMounted(() => {
  loadMatchups()
  loadUnits()
  loadEnemySetups()
})

onUnmounted(() => {
  stopPlayback()
  clearProjectiles()
})
</script>

<template>
  <main>
    <h1>Battle playback</h1>

    <p>
      <label>
        <input type="radio" v-model="mode" value="composition" :disabled="isRequesting" />
        Build your own army
      </label>
      <label>
        <input type="radio" v-model="mode" value="matchup" :disabled="isRequesting" />
        Fixed matchup
      </label>
    </p>

    <template v-if="mode === 'matchup'">
      <p v-if="matchupsError" role="alert">Could not load matchups: {{ matchupsError }}</p>

      <p v-else>
        <label>
          Matchup:
          <select v-model="selectedMatchupId" :disabled="isRequesting">
            <option v-for="matchup in matchups" :key="matchup.id" :value="matchup.id">
              {{ matchup.displayName }}
            </option>
          </select>
        </label>
        <label>
          Seed:
          <input
            v-model="seedInput"
            type="text"
            placeholder="random"
            aria-label="Seed (blank for random)"
            :disabled="isRequesting"
          />
        </label>
        <button type="button" :disabled="!selectedMatchupId || isRequesting" @click="runMatchupBattle">
          {{ isRequesting ? 'Running…' : 'Run battle' }}
        </button>
      </p>
    </template>

    <template v-else>
      <p v-if="unitsError" role="alert">Could not load units: {{ unitsError }}</p>
      <p v-if="enemySetupsError" role="alert">Could not load enemy setups: {{ enemySetupsError }}</p>

      <section aria-label="Roster">
        <span
          v-for="unit in units"
          :key="unit.id"
          draggable="true"
          :class="{ selected: unit.id === selectedUnitId }"
          @dragstart="onDragStart($event, unit.id)"
          @click="onRosterClick(unit.id)"
        >
          {{ unit.displayName }}
        </span>
      </section>

      <p>
        <button
          type="button"
          :disabled="!canResetArmy(playerPlacements.length, hasFinished, canEditDeployment)"
          @click="resetArmy"
        >
          Reset army
        </button>
      </p>

      <BattleBoard
        :state="compositionBoardState"
        :interactive="canEditDeployment"
        :projectiles="activeProjectiles"
        :selected-unit-id="selectedUnitId"
        @tile-drop="onTileDrop"
        @unit-delete="onUnitDelete"
      />

      <p v-if="placementError" role="alert">{{ placementError }}</p>

      <p>
        <label>
          Enemy setup:
          <select v-model="selectedEnemySetupId" :disabled="isRequesting">
            <option v-for="setup in enemySetups" :key="setup.id" :value="setup.id">
              {{ setup.displayName }}
            </option>
          </select>
        </label>
        <label>
          Seed:
          <input
            v-model="seedInput"
            type="text"
            placeholder="random"
            aria-label="Seed (blank for random)"
            :disabled="isRequesting"
          />
        </label>
        <button
          type="button"
          :disabled="playerPlacements.length === 0 || !selectedEnemySetupId || isRequesting"
          @click="runCompositionBattle"
        >
          {{ isRequesting ? 'Running…' : 'Run battle' }}
        </button>
      </p>
    </template>

    <p v-if="error" role="alert">Could not run the battle: {{ error }}</p>

    <template v-if="outcome && battleState">
      <p>Seed: {{ outcome.seed }}</p>

      <p>
        <button type="button" :disabled="isPlaying" @click="play">
          {{ hasFinished ? 'Play again' : currentTick > 0 ? 'Resume' : 'Play' }}
        </button>
        <button type="button" :disabled="!isPlaying" @click="pause">Pause</button>
        <button type="button" :disabled="currentTick <= 0" @click="jumpToStart">|&lt; Start</button>
        <button type="button" :disabled="currentTick <= 0" @click="stepBack">&lt; Step</button>
        <button type="button" :disabled="currentTick >= terminatingTick" @click="stepForward">Step &gt;</button>
        <button type="button" :disabled="currentTick >= terminatingTick" @click="jumpToEnd">End &gt;|</button>
      </p>

      <p>
        <input
          type="range"
          min="0"
          :max="terminatingTick"
          :value="currentTick"
          aria-label="Seek to tick"
          @input="onScrub"
        />
        · Tick: {{ currentTick }} / {{ terminatingTick }}
      </p>

      <BattleBoard v-if="mode === 'matchup'" :state="battleState" :projectiles="activeProjectiles" />

      <section v-if="hasFinished" aria-label="Outcome">
        <p role="status">
          {{ endingLabel(outcome.outcome.terminationReason) }} · Ended on tick {{ outcome.outcome.terminatingTick }}
        </p>

        <div v-for="group in survivorGroups" :key="group.side">
          <h3>{{ sideLabel(group.side) }}</h3>
          <p v-if="group.units.length === 0">No survivors</p>
          <ul v-else>
            <li v-for="unit in group.units" :key="unit.id">
              {{ battleState.unitDefinitions[unit.definitionId]?.displayName ?? unit.definitionId }}:
              {{ unit.hp }}/{{ unit.maxHp }} hp
            </li>
          </ul>
        </div>
      </section>
    </template>

    <p v-else-if="!error">Pick a matchup, or build your own army, and press Run battle.</p>
  </main>
</template>

<style scoped>
section[aria-label='Roster'] span {
  display: inline-block;
  cursor: pointer;
  padding: 2px 6px;
  margin: 2px;
  border: 1px solid transparent;
  user-select: none;
}

section[aria-label='Roster'] span.selected {
  border-color: #2e7d32;
  background: #d1e7dd;
  color: #1b1b1b;
}
</style>

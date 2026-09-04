<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue'
import { listMatchups, simulateBattle, type MatchupDto, type SimulateBattleResponse } from '@/api/battles'
import { applyTick, type BattleEvent } from '@/battle/applyTick'
import { createBattleState, type BattleState } from '@/battle/battleState'
import { endingLabel, survivorsBySide } from '@/battle/outcome'
import { seekTo, terminatingTickOf } from '@/battle/playback'
import BattleBoard from '@/components/BattleBoard.vue'

// A 150-350 tick battle at 30ms/tick is a 5-10 second playback - watchable,
// not instant, not a slog.
const TICK_INTERVAL_MS = 30

const SIDE_LABELS: Record<number, string> = {
  0: 'Player',
  1: 'Opponent',
}

const matchups = ref<MatchupDto[]>([])
const matchupsError = ref<string | null>(null)
const selectedMatchupId = ref('')
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

let timer: ReturnType<typeof setInterval> | undefined

function sideLabel(side: number): string {
  return SIDE_LABELS[side] ?? `Unknown (${side})`
}

const survivorGroups = computed(() => (finalState.value ? survivorsBySide(finalState.value) : []))

function stopPlayback(): void {
  if (timer !== undefined) {
    clearInterval(timer)
    timer = undefined
  }
  isPlaying.value = false
}

// Every control but Play routes through here: one code path for step,
// jump and scrub, all built on the same rebuild-from-tick-0 primitive.
function seek(tick: number): void {
  const response = outcome.value
  if (!response) {
    return
  }

  stopPlayback()
  const result = seekTo(response, tick)
  battleState.value = result.state
  currentTick.value = result.tick
  hasFinished.value = result.tick >= terminatingTick.value
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
  }
  hasFinished.value = false

  // The generated event type is technically optional (see applyTick.ts);
  // the server always sends it, so this is the one place that trust is spent.
  const log = response.events as BattleEvent[]

  isPlaying.value = true
  timer = setInterval(() => {
    currentTick.value += 1
    applyTick(battleState.value!, currentTick.value, log)
    if (currentTick.value >= terminatingTick.value) {
      stopPlayback()
      hasFinished.value = true
    }
  }, TICK_INTERVAL_MS)
}

function pause(): void {
  stopPlayback()
}

/** Requests a battle for the selected matchup and seed, then plays it back from tick 0. */
async function runBattle(): Promise<void> {
  if (!selectedMatchupId.value || isRequesting.value) {
    return
  }

  // Cleanly cancel whatever is currently playing before anything else, so a
  // stray interval can never go on applying the old log once this starts.
  // isRequesting stays true for the rest of this call, disabling the button
  // that is this function's only entry point — so no second call can start
  // until this one finishes, and a stale response can never land.
  stopPlayback()

  isRequesting.value = true
  outcome.value = null
  finalState.value = null
  error.value = null
  battleState.value = null
  currentTick.value = 0
  terminatingTick.value = 0
  hasFinished.value = false

  const trimmedSeed = seedInput.value.trim()
  const result = await simulateBattle({
    matchupId: selectedMatchupId.value,
    seed: trimmedSeed === '' ? undefined : trimmedSeed,
  })

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

async function loadMatchups(): Promise<void> {
  const result = await listMatchups()
  if (result.ok) {
    matchups.value = result.data
    selectedMatchupId.value = result.data[0]?.id ?? ''
  } else {
    matchupsError.value = result.error
  }
}

onMounted(loadMatchups)

onUnmounted(() => {
  stopPlayback()
})
</script>

<template>
  <main>
    <h1>Battle playback</h1>

    <p v-if="matchupsError" role="alert">Could not load matchups: {{ matchupsError }}</p>

    <template v-else>
      <p>
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
        <button type="button" :disabled="!selectedMatchupId || isRequesting" @click="runBattle">
          {{ isRequesting ? 'Running…' : 'Run battle' }}
        </button>
      </p>

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

        <BattleBoard :state="battleState" />

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

      <p v-else-if="!error">Pick a matchup and press Run battle.</p>
    </template>
  </main>
</template>

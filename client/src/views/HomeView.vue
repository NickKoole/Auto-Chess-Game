<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { simulateBattle, type SimulateBattleResponse } from '@/api/battles'
import { applyTick, type BattleEvent } from '@/battle/applyTick'
import { createBattleState, type BattleState } from '@/battle/battleState'
import { seekTo, terminatingTickOf } from '@/battle/playback'
import BattleBoard from '@/components/BattleBoard.vue'

const MATCHUP_ID = 'melee-mirror'

// The two hardcoded matchups run 150-350 ticks; at 30ms/tick that's a
// 5-10 second playback - watchable, not instant, not a slog.
const TICK_INTERVAL_MS = 30

const WINNER_LABELS: Record<number, string> = {
  0: 'Draw',
  1: 'Player',
  2: 'Opponent',
}

const outcome = ref<SimulateBattleResponse | null>(null)
const error = ref<string | null>(null)
const battleState = ref<BattleState | null>(null)
const currentTick = ref(0)
const terminatingTick = ref(0)
const isPlaying = ref(false)
const hasFinished = ref(false)

let timer: ReturnType<typeof setInterval> | undefined

function winnerLabel(winner: number): string {
  return WINNER_LABELS[winner] ?? `Unknown (${winner})`
}

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

async function loadBattle(): Promise<void> {
  stopPlayback()
  outcome.value = null
  error.value = null
  battleState.value = null
  currentTick.value = 0
  terminatingTick.value = 0
  hasFinished.value = false

  const result = await simulateBattle({ matchupId: MATCHUP_ID })
  if (result.ok) {
    outcome.value = result.data
    battleState.value = createBattleState(result.data)
    terminatingTick.value = terminatingTickOf(result.data)
  } else {
    error.value = result.error
  }
}

onMounted(loadBattle)

onUnmounted(() => {
  stopPlayback()
})
</script>

<template>
  <main>
    <h1>Battle playback</h1>
    <p>Matchup: {{ MATCHUP_ID }}</p>

    <p v-if="error" role="alert">Could not run the battle: {{ error }}</p>

    <template v-else-if="outcome && battleState">
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

      <p v-if="hasFinished" role="status">
        Winner: {{ winnerLabel(outcome.outcome.winner) }} · Ended on tick {{ outcome.outcome.terminatingTick }}
      </p>
    </template>

    <p v-else>Running battle&hellip;</p>
  </main>
</template>

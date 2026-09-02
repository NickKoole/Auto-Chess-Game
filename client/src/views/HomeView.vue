<script setup lang="ts">
import { onMounted, onUnmounted, ref } from 'vue'
import { simulateBattle, type SimulateBattleResponse } from '@/api/battles'
import { toNumber } from '@/api/numeric'
import { applyTick, type BattleEvent } from '@/battle/applyTick'
import { createBattleState, type BattleState } from '@/battle/battleState'
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

function play(): void {
  const response = outcome.value
  if (!response || isPlaying.value) {
    return
  }

  stopPlayback()
  hasFinished.value = false
  currentTick.value = 0
  battleState.value = createBattleState(response)

  // The generated event type is technically optional (see applyTick.ts);
  // the server always sends it, so this is the one place that trust is spent.
  const log = response.events as BattleEvent[]
  const terminatingTick = toNumber(response.outcome.terminatingTick)

  isPlaying.value = true
  timer = setInterval(() => {
    currentTick.value += 1
    applyTick(battleState.value!, currentTick.value, log)
    if (currentTick.value >= terminatingTick) {
      stopPlayback()
      hasFinished.value = true
    }
  }, TICK_INTERVAL_MS)
}

onMounted(async () => {
  const result = await simulateBattle({ matchupId: MATCHUP_ID })
  if (result.ok) {
    outcome.value = result.data
    battleState.value = createBattleState(result.data)
  } else {
    error.value = result.error
  }
})

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
        <button type="button" :disabled="isPlaying" @click="play">{{ hasFinished ? 'Play again' : 'Play' }}</button>
        · Tick: {{ currentTick }}
      </p>

      <BattleBoard :state="battleState" />

      <p v-if="hasFinished" role="status">
        Winner: {{ winnerLabel(outcome.outcome.winner) }} · Ended on tick {{ outcome.outcome.terminatingTick }}
      </p>
    </template>

    <p v-else>Running battle&hellip;</p>
  </main>
</template>

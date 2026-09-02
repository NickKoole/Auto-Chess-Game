<script setup lang="ts">
import { computed, onMounted, ref } from 'vue'
import { simulateBattle, type SimulateBattleResponse } from '@/api/battles'
import { createBattleState } from '@/battle/battleState'
import BattleBoard from '@/components/BattleBoard.vue'

const MATCHUP_ID = 'melee-mirror'

const WINNER_LABELS: Record<number, string> = {
  0: 'Draw',
  1: 'Player',
  2: 'Opponent',
}

const outcome = ref<SimulateBattleResponse | null>(null)
const error = ref<string | null>(null)

const battleState = computed(() => (outcome.value ? createBattleState(outcome.value) : null))

function winnerLabel(winner: number): string {
  return WINNER_LABELS[winner] ?? `Unknown (${winner})`
}

onMounted(async () => {
  const result = await simulateBattle({ matchupId: MATCHUP_ID })
  if (result.ok) {
    outcome.value = result.data
  } else {
    error.value = result.error
  }
})
</script>

<template>
  <main>
    <h1>Battle smoke test</h1>
    <p>Matchup: {{ MATCHUP_ID }}</p>

    <p v-if="error" role="alert">Could not run the battle: {{ error }}</p>
    <p v-else-if="outcome">
      Winner: {{ winnerLabel(outcome.outcome.winner) }} · Ticks: {{ outcome.outcome.terminatingTick }} · Events:
      {{ outcome.events.length }}
    </p>
    <p v-else>Running battle&hellip;</p>

    <BattleBoard v-if="battleState" :state="battleState" />
  </main>
</template>

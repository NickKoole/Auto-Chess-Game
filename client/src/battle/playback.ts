import type { SimulateBattleResponse } from '@/api/battles'
import { toNumber } from '@/api/numeric'
import { applyTick, type BattleEvent } from './applyTick'
import { createBattleState, type BattleState } from './battleState'

export interface SeekResult {
  state: BattleState
  tick: number
}

/** The last tick that occurred in this battle - `outcome.terminatingTick` coerced to a number. */
export function terminatingTickOf(response: SimulateBattleResponse): number {
  return toNumber(response.outcome.terminatingTick)
}

/** Clamps `tick` into the playable range for a battle that ends at `terminatingTick`. */
export function clampTick(tick: number, terminatingTick: number): number {
  return Math.min(Math.max(tick, 0), terminatingTick)
}

/**
 * Rebuilds the battle state at `tick` from tick 0, applying every intervening
 * tick's events in order. `applyTick` only moves forward and `unitDied`
 * removes a unit from state outright, so replaying from zero is the only
 * honest way to move backward - there is nothing to reverse-apply.
 *
 * `tick` is clamped to [0, terminatingTick]; the clamped value is returned
 * alongside the state so a caller never has to re-derive it.
 */
export function seekTo(response: SimulateBattleResponse, tick: number): SeekResult {
  const terminatingTick = terminatingTickOf(response)
  const clamped = clampTick(tick, terminatingTick)
  const state = createBattleState(response)
  const log = response.events as BattleEvent[]

  for (let t = 1; t <= clamped; t++) {
    applyTick(state, t, log)
  }

  return { state, tick: clamped }
}

import type { SimulateBattleResponse } from '@/api/battles'
import { toNumber } from '@/api/numeric'
import { applyTick, type BattleEvent } from './applyTick'
import { createBattleState, type BattleState } from './battleState'

export interface SeekResult {
  state: BattleState
  tick: number
}

export function terminatingTickOf(response: SimulateBattleResponse): number {
  return toNumber(response.outcome.terminatingTick)
}

export function clampTick(tick: number, terminatingTick: number): number {
  return Math.min(Math.max(tick, 0), terminatingTick)
}

/**
 * `applyTick` only moves forward and `unitDied` removes a unit outright, so
 * replaying from zero is the only honest way to move backward. Returns the
 * clamped tick alongside the state so a caller never has to re-derive it.
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

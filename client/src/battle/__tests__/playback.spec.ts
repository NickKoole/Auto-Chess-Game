import { describe, expect, it } from 'vitest'
import type { components } from '@/api/schema.d.ts'
import { applyTick, type BattleEvent } from '../applyTick'
import { createBattleState, SIDE_OPPONENT, SIDE_PLAYER } from '../battleState'
import { clampTick, seekTo, terminatingTickOf } from '../playback'

type SimulateBattleResponse = components['schemas']['SimulateBattleResponse']

const TERMINATING_TICK = 8

const EVENTS: BattleEvent[] = [
  { type: 'unitMoved', unitId: 0, from: { row: 5, col: 2 }, to: { row: 4, col: 2 }, tick: 2 },
  { type: 'unitAttacked', attackerId: 1, targetId: 0, tick: 4 },
  { type: 'damageDealt', attackerId: 1, targetId: 0, amount: 10, resultingHp: 20, tick: 4 },
  { type: 'unitMoved', unitId: 1, from: { row: 2, col: 2 }, to: { row: 3, col: 2 }, tick: 6 },
  { type: 'damageDealt', attackerId: 0, targetId: 1, amount: 30, resultingHp: 0, tick: 8 },
  { type: 'unitDied', unitId: 1, position: { row: 3, col: 2 }, tick: 8 },
]

function response(): SimulateBattleResponse {
  return {
    seed: '1',
    initialSetup: [
      { unitId: 0, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 5, col: 2 } },
      { unitId: 1, side: SIDE_OPPONENT, definitionId: 'knight', position: { row: 2, col: 2 } },
    ],
    unitDefinitions: [
      {
        id: 'knight',
        displayName: 'Knight',
        maxHp: 30,
        attackDamage: 10,
        attackIntervalTicks: 4,
        movementTicksPerHex: 2,
        attackRangeHexes: 1,
        damageVariance: 0,
      },
    ],
    events: EVENTS,
    outcome: { winner: SIDE_PLAYER, terminatingTick: TERMINATING_TICK, terminationReason: 0 },
  }
}

/** Builds the expected state by stepping tick by tick, independently of seekTo's own loop. */
function stepTo(res: SimulateBattleResponse, tick: number) {
  const state = createBattleState(res)
  for (let t = 1; t <= tick; t++) {
    applyTick(state, t, res.events as BattleEvent[])
  }
  return state
}

describe('terminatingTickOf', () => {
  it('coerces the outcome terminating tick to a number', () => {
    expect(terminatingTickOf(response())).toBe(TERMINATING_TICK)
  })
})

describe('clampTick', () => {
  it('clamps below zero up to zero', () => {
    expect(clampTick(-5, TERMINATING_TICK)).toBe(0)
  })

  it('clamps above the terminating tick down to it', () => {
    expect(clampTick(100, TERMINATING_TICK)).toBe(TERMINATING_TICK)
  })

  it('leaves an in-range tick untouched', () => {
    expect(clampTick(4, TERMINATING_TICK)).toBe(4)
  })
})

describe('seekTo', () => {
  it('at tick 0 matches the tick-0 board exactly', () => {
    const res = response()
    const result = seekTo(res, 0)
    expect(result.tick).toBe(0)
    expect(result.state).toEqual(createBattleState(res))
  })

  it('at the terminating tick matches the final outcome', () => {
    const res = response()
    const result = seekTo(res, TERMINATING_TICK)
    expect(result.tick).toBe(TERMINATING_TICK)
    expect(result.state.units.map((unit) => unit.id)).toEqual([0])
    expect(result.state.units[0]).toMatchObject({ hp: 20, position: { row: 4, col: 2 } })
  })

  it('clamps a negative tick to 0', () => {
    const result = seekTo(response(), -3)
    expect(result.tick).toBe(0)
  })

  it('clamps a tick past the end to the terminating tick', () => {
    const result = seekTo(response(), 1000)
    expect(result.tick).toBe(TERMINATING_TICK)
  })

  it('stepping back brings a dead unit back to life at its earlier position', () => {
    const res = response()
    const atEnd = seekTo(res, TERMINATING_TICK)
    expect(atEnd.state.units.map((unit) => unit.id)).toEqual([0])

    const backBeforeDeath = seekTo(res, 5)
    const opponent = backBeforeDeath.state.units.find((unit) => unit.id === 1)
    expect(opponent).toMatchObject({ hp: 30, position: { row: 2, col: 2 } })
  })

  it('stepping back undoes a move', () => {
    const res = response()
    const afterMove = seekTo(res, 3)
    expect(afterMove.state.units[0]!.position).toEqual({ row: 4, col: 2 })

    const beforeMove = seekTo(res, 1)
    expect(beforeMove.state.units[0]!.position).toEqual({ row: 5, col: 2 })
  })

  it('matches stepping there one tick at a time, for several N including 0 and the final tick', () => {
    const res = response()
    for (const n of [0, 1, 2, 4, 5, 6, 7, TERMINATING_TICK]) {
      expect(seekTo(res, n).state).toEqual(stepTo(res, n))
    }
  })

  it('reconstructs currentTargetId when scrubbed to a tick after a unitAttacked event', () => {
    const res = response()
    const result = seekTo(res, 4)

    const attacker = result.state.units.find((unit) => unit.id === 1)
    expect(attacker?.currentTargetId).toBe(0)
    expect(result.state).toEqual(stepTo(res, 4))
  })
})

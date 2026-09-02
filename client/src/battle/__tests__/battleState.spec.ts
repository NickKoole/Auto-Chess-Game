import { describe, expect, it } from 'vitest'
import type { components } from '@/api/schema.d.ts'
import { createBattleState, SIDE_OPPONENT, SIDE_PLAYER } from '../battleState'

type SimulateBattleResponse = components['schemas']['SimulateBattleResponse']

function response(overrides: Partial<SimulateBattleResponse> = {}): SimulateBattleResponse {
  return {
    seed: '1',
    initialSetup: [
      { unitId: 0, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 5, col: 2 } },
      { unitId: '3', side: SIDE_OPPONENT, definitionId: 'archer', position: { row: '2', col: 4 } },
    ],
    unitDefinitions: [
      {
        id: 'knight',
        displayName: 'Knight',
        maxHp: 30,
        attackDamage: 5,
        attackIntervalTicks: 10,
        movementTicksPerHex: 5,
        attackRangeHexes: 1,
        damageVariance: 0,
      },
      {
        id: 'archer',
        displayName: 'Archer',
        maxHp: '18',
        attackDamage: 3,
        attackIntervalTicks: 12,
        movementTicksPerHex: 5,
        attackRangeHexes: 3,
        damageVariance: 0,
      },
    ],
    events: [],
    outcome: { winner: SIDE_PLAYER, terminatingTick: 0, terminationReason: 0 },
    ...overrides,
  }
}

describe('createBattleState', () => {
  it('builds one unit per starting placement, seeded at full HP', () => {
    const state = createBattleState(response())

    expect(state.units).toEqual([
      { id: 0, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 5, col: 2 }, hp: 30, maxHp: 30 },
      { id: 3, side: SIDE_OPPONENT, definitionId: 'archer', position: { row: 2, col: 4 }, hp: 18, maxHp: 18 },
    ])
  })

  it('coerces string-encoded numeric fields to numbers', () => {
    const state = createBattleState(response())
    const archer = state.units[1]!
    expect(archer.id).toBe(3)
    expect(archer.position.row).toBe(2)
    expect(typeof archer.hp).toBe('number')
  })

  it('indexes unit definitions by id', () => {
    const state = createBattleState(response())
    expect(state.unitDefinitions.knight?.displayName).toBe('Knight')
    expect(state.unitDefinitions.archer?.displayName).toBe('Archer')
  })

  it('throws if a placement references a definition that was not sent', () => {
    const broken = response({
      initialSetup: [{ unitId: 0, side: SIDE_PLAYER, definitionId: 'ghost', position: { row: 5, col: 2 } }],
    })
    expect(() => createBattleState(broken)).toThrow(/ghost/)
  })
})

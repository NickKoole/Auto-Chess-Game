import { describe, expect, it } from 'vitest'
import type { components } from '@/api/schema.d.ts'
import { createBattleState, currentTargetOf, SIDE_OPPONENT, SIDE_PLAYER, unitLabel } from '../battleState'

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
      {
        id: 0,
        side: SIDE_PLAYER,
        definitionId: 'knight',
        position: { row: 5, col: 2 },
        hp: 30,
        maxHp: 30,
        currentTargetId: null,
      },
      {
        id: 3,
        side: SIDE_OPPONENT,
        definitionId: 'archer',
        position: { row: 2, col: 4 },
        hp: 18,
        maxHp: 18,
        currentTargetId: null,
      },
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

describe('unitLabel', () => {
  it('pairs the display name with the unit id, so two of the same unit are distinguishable', () => {
    const state = createBattleState(response())
    expect(unitLabel(state.units[0]!, state.unitDefinitions)).toBe('Knight #0')
    expect(unitLabel(state.units[1]!, state.unitDefinitions)).toBe('Archer #3')
  })

  it('falls back to the raw definition id if no definition was sent for it', () => {
    const state = createBattleState(response())
    const unit = { ...state.units[0]!, definitionId: 'ghost' }
    expect(unitLabel(unit, state.unitDefinitions)).toBe('ghost #0')
  })
})

describe('currentTargetOf', () => {
  it('is undefined when the unit has no current target', () => {
    const state = createBattleState(response())
    expect(currentTargetOf(state, state.units[0]!)).toBeUndefined()
  })

  it('finds the unit named by currentTargetId', () => {
    const state = createBattleState(response())
    state.units[0]!.currentTargetId = state.units[1]!.id
    expect(currentTargetOf(state, state.units[0]!)).toBe(state.units[1])
  })

  it('is undefined once the target has died and left state.units', () => {
    const state = createBattleState(response())
    const target = state.units[1]!
    state.units[0]!.currentTargetId = target.id
    state.units = state.units.filter((unit) => unit.id !== target.id)
    expect(currentTargetOf(state, state.units[0]!)).toBeUndefined()
  })
})

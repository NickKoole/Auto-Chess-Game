import { describe, expect, it } from 'vitest'
import type { components } from '@/api/schema.d.ts'
import type { BattleEvent } from '../applyTick'
import { SIDE_OPPONENT, SIDE_PLAYER, type BattleState } from '../battleState'
import { projectilesForTick } from '../projectiles'

type UnitDefinitionDto = components['schemas']['UnitDefinitionDto']

function archerDefinition(): UnitDefinitionDto {
  return {
    id: 'archer',
    displayName: 'Archer',
    maxHp: 60,
    attackDamage: 8,
    attackIntervalTicks: 12,
    movementTicksPerHex: 5,
    attackRangeHexes: 3,
    damageVariance: 0,
  }
}

function knightDefinition(): UnitDefinitionDto {
  return {
    id: 'knight',
    displayName: 'Knight',
    maxHp: 180,
    attackDamage: 16,
    attackIntervalTicks: 16,
    movementTicksPerHex: 8,
    attackRangeHexes: 1,
    damageVariance: 0,
  }
}

function state(): BattleState {
  return {
    units: [
      {
        id: 0,
        side: SIDE_PLAYER,
        definitionId: 'archer',
        position: { row: 5, col: 2 },
        hp: 60,
        maxHp: 60,
        currentTargetId: null,
      },
      {
        id: 1,
        side: SIDE_OPPONENT,
        definitionId: 'archer',
        position: { row: 2, col: 2 },
        hp: 60,
        maxHp: 60,
        currentTargetId: null,
      },
      {
        id: 2,
        side: SIDE_PLAYER,
        definitionId: 'knight',
        position: { row: 5, col: 4 },
        hp: 180,
        maxHp: 180,
        currentTargetId: null,
      },
      {
        id: 3,
        side: SIDE_OPPONENT,
        definitionId: 'knight',
        position: { row: 2, col: 4 },
        hp: 180,
        maxHp: 180,
        currentTargetId: null,
      },
    ],
    unitDefinitions: { archer: archerDefinition(), knight: knightDefinition() },
  }
}

describe('projectilesForTick', () => {
  it('emits a projectile for a ranged attacker at the given tick', () => {
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 4 }]

    const projectiles = projectilesForTick(state(), state(), 4, log)

    expect(projectiles).toEqual([{ id: '0-1-4', from: { row: 5, col: 2 }, to: { row: 2, col: 2 } }])
  })

  it('emits nothing for a melee attacker (attackRangeHexes 1)', () => {
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 2, targetId: 3, tick: 4 }]

    expect(projectilesForTick(state(), state(), 4, log)).toEqual([])
  })

  it('prefers the target\'s post-tick position, since it may move the same tick it is attacked', () => {
    const preTickState = state()
    const postTickState = state()
    const movedTarget = postTickState.units.find((unit) => unit.id === 1)!
    movedTarget.position = { row: 3, col: 2 }
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 4 }]

    const projectiles = projectilesForTick(preTickState, postTickState, 4, log)

    expect(projectiles).toEqual([{ id: '0-1-4', from: { row: 5, col: 2 }, to: { row: 3, col: 2 } }])
  })

  it('falls back to the pre-tick target position for a killing blow, whose unitDied shares this tick', () => {
    const preTickState = state()
    const postTickState = state()
    postTickState.units = postTickState.units.filter((unit) => unit.id !== 1)
    const log: BattleEvent[] = [
      { type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 4 },
      { type: 'damageDealt', attackerId: 0, targetId: 1, amount: 60, resultingHp: 0, tick: 4 },
      { type: 'unitDied', unitId: 1, position: { row: 2, col: 2 }, tick: 4 },
    ]

    expect(projectilesForTick(preTickState, postTickState, 4, log)).toEqual([
      { id: '0-1-4', from: { row: 5, col: 2 }, to: { row: 2, col: 2 } },
    ])
  })

  it('ignores unitAttacked events for a different tick', () => {
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 5 }]

    expect(projectilesForTick(state(), state(), 4, log)).toEqual([])
  })

  it('ignores non-unitAttacked events entirely', () => {
    const log: BattleEvent[] = [
      { type: 'damageDealt', attackerId: 0, targetId: 1, amount: 8, resultingHp: 52, tick: 4 },
    ]

    expect(projectilesForTick(state(), state(), 4, log)).toEqual([])
  })

  it('skips an event whose attacker or target is not present in either state', () => {
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 0, targetId: 99, tick: 4 }]

    expect(projectilesForTick(state(), state(), 4, log)).toEqual([])
  })

  it('skips an event whose attacker is not present pre-tick, even if somehow present post-tick', () => {
    const log: BattleEvent[] = [{ type: 'unitAttacked', attackerId: 99, targetId: 1, tick: 4 }]

    expect(projectilesForTick(state(), state(), 4, log)).toEqual([])
  })

  it('emits one projectile per qualifying ranged attack on the same tick', () => {
    const extraArcher = {
      id: 4,
      side: SIDE_PLAYER,
      definitionId: 'archer',
      position: { row: 6, col: 3 },
      hp: 60,
      maxHp: 60,
      currentTargetId: null,
    }
    const preTickState = state()
    preTickState.units.push(extraArcher)
    const postTickState = state()
    postTickState.units.push(extraArcher)
    const log: BattleEvent[] = [
      { type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 4 },
      { type: 'unitAttacked', attackerId: 4, targetId: 1, tick: 4 },
    ]

    expect(projectilesForTick(preTickState, postTickState, 4, log)).toEqual([
      { id: '0-1-4', from: { row: 5, col: 2 }, to: { row: 2, col: 2 } },
      { id: '4-1-4', from: { row: 6, col: 3 }, to: { row: 2, col: 2 } },
    ])
  })
})

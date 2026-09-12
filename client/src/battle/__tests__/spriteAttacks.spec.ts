import { describe, expect, it } from 'vitest'
import type { BattleEvent } from '../applyTick'
import { applyTick } from '../applyTick'
import { cloneBattleState, type BattleState } from '../battleState'
import { facingTarget, mergeSpriteAttacks, spriteAttacksAfterSeek, spriteAttacksForTick } from '../spriteAttacks'

function state(): BattleState {
  return {
    unitDefinitions: {},
    units: [
      { id: 1, definitionId: 'knight', side: 0, hp: 180, maxHp: 180, position: { row: 4, col: 3 }, currentTargetId: null },
      { id: 2, definitionId: 'knight', side: 1, hp: 180, maxHp: 180, position: { row: 4, col: 4 }, currentTargetId: null },
    ],
  }
}
const attack: BattleEvent = { type: 'unitAttacked', tick: 5, attackerId: 1, targetId: 2 }

describe('sprite facing', () => {
  it('uses screen position including staggered hex rows', () => {
    expect(facingTarget({ row: 4, col: 3 }, { row: 5, col: 3 }, 'left')).toBe('right')
    expect(facingTarget({ row: 5, col: 3 }, { row: 4, col: 3 }, 'right')).toBe('left')
    expect(facingTarget({ row: 4, col: 3 }, { row: 2, col: 3 }, 'left')).toBe('left')
  })
})

describe('server-driven sprite attacks', () => {
  it('extracts only attack events at the displayed tick without changing game state', () => {
    const before = state()
    const snapshot = cloneBattleState(before)
    const log: BattleEvent[] = [attack, { ...attack, tick: 6 }, { type: 'damageDealt', tick: 5, attackerId: 1, targetId: 2, amount: 16, resultingHp: 164 }]
    expect(spriteAttacksForTick(before, state(), 5, log, 1000)).toEqual([
      { unitId: 1, facing: 'right', startedAt: 1000 },
    ])
    expect(before).toEqual(snapshot)
    expect(spriteAttacksForTick(before, state(), 4, log, 1000)).toEqual([])
  })

  it('uses the target position after movement on the same tick', () => {
    const after = state()
    after.units[1]!.position = { row: 4, col: 2 }
    expect(spriteAttacksForTick(state(), after, 5, [attack], 0)[0]?.facing).toBe('left')
  })

  it('faces the recorded death position for a target that moved before a lethal hit', () => {
    const log: BattleEvent[] = [
      { type: 'unitMoved', tick: 5, unitId: 2, from: { row: 4, col: 4 }, to: { row: 4, col: 2 } },
      attack,
      { type: 'damageDealt', tick: 5, attackerId: 1, targetId: 2, amount: 180, resultingHp: 0 },
      { type: 'unitDied', tick: 5, unitId: 2, position: { row: 4, col: 2 } },
    ]
    const before = state()
    const after = cloneBattleState(before)
    applyTick(after, 5, log)
    expect(after.units).toHaveLength(1)
    expect(spriteAttacksForTick(before, after, 5, log, 200)).toEqual([
      { unitId: 1, facing: 'left', startedAt: 200 },
    ])
    expect(after.units[0]?.hp).toBe(180)
  })

  it('falls back to the pre-tick target position when the target is absent', () => {
    const after = state()
    after.units.pop()
    expect(spriteAttacksForTick(state(), after, 5, [attack], 0)[0]?.facing).toBe('right')
  })

  it('ignores missing attackers and targets and deaths from other ticks', () => {
    expect(spriteAttacksForTick(state(), state(), 5, [{ ...attack, attackerId: 99 }], 0)).toEqual([])
    expect(spriteAttacksForTick(state(), state(), 5, [
      { ...attack, targetId: 99 },
      { type: 'unitDied', tick: 4, unitId: 99, position: { row: 4, col: 2 } },
    ], 0)).toEqual([])
  })

  it('starts a fresh animation on a single forward step', () => {
    expect(spriteAttacksAfterSeek(state(), state(), 4, 5, 5, [attack], 900)).toEqual([
      { unitId: 1, facing: 'right', startedAt: 900 },
    ])
  })

  it.each([[6, 5, 5], [0, 5, 5], [5, 5, 5], [4, 5, 4], [5, 0, 0]])(
    'clears transient attacks when seeking from %s to %s (resolved %s)',
    (previous, requested, resolved) => {
      expect(spriteAttacksAfterSeek(state(), state(), previous, requested, resolved, [attack], 0)).toEqual([])
    },
  )

  it('clears attacks when there is no prior battle', () => {
    expect(spriteAttacksAfterSeek(null, state(), 4, 5, 5, [attack], 0)).toEqual([])
  })

  it('preserves overlapping attacks, expires old ones and restarts repeated attackers', () => {
    const active = [
      { unitId: 1, facing: 'left' as const, startedAt: 0 },
      { unitId: 2, facing: 'right' as const, startedAt: 200 },
      { unitId: 3, facing: 'right' as const, startedAt: 100 },
    ]
    const incoming = [{ unitId: 2, facing: 'left' as const, startedAt: 320 }]
    expect(mergeSpriteAttacks(active, incoming, 320, 320)).toEqual([
      { unitId: 3, facing: 'right', startedAt: 100 },
      { unitId: 2, facing: 'left', startedAt: 320 },
    ])
    expect(mergeSpriteAttacks(active, [], 520, 320)).toEqual([])
  })
})

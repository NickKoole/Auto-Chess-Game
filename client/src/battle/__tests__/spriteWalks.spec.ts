import { describe, expect, it } from 'vitest'
import type { BattleEvent } from '../applyTick'
import type { BattleState } from '../battleState'
import { mergeSpriteWalks, spriteWalksAfterSeek, spriteWalksForTick } from '../spriteWalks'

function state(): BattleState {
  return {
    unitDefinitions: {},
    units: [
      { id: 1, definitionId: 'knight', side: 0, hp: 180, maxHp: 180, position: { row: 4, col: 3 }, currentTargetId: null },
      { id: 2, definitionId: 'knight', side: 1, hp: 180, maxHp: 180, position: { row: 4, col: 4 }, currentTargetId: null },
    ],
  }
}

const moved: BattleEvent = { type: 'unitMoved', tick: 5, unitId: 1, from: { row: 4, col: 3 }, to: { row: 4, col: 4 } }

describe('server-driven sprite walks', () => {
  it('extracts only unitMoved events at the displayed tick', () => {
    const log: BattleEvent[] = [moved, { ...moved, tick: 6 }, { type: 'unitAttacked', tick: 5, attackerId: 1, targetId: 2 }]
    expect(spriteWalksForTick(state(), 5, log)).toEqual([{ unitId: 1, facing: 'right' }])
    expect(spriteWalksForTick(state(), 4, log)).toEqual([])
  })

  it('faces the direction of the move, falling back to side when the column does not change', () => {
    expect(spriteWalksForTick(state(), 5, [{ ...moved, from: { row: 2, col: 3 }, to: { row: 1, col: 3 } }])).toEqual([
      { unitId: 1, facing: 'right' },
    ])
    expect(
      spriteWalksForTick(state(), 5, [{ ...moved, unitId: 2, from: { row: 2, col: 4 }, to: { row: 1, col: 4 } }]),
    ).toEqual([{ unitId: 2, facing: 'left' }])
    expect(spriteWalksForTick(state(), 5, [{ ...moved, from: { row: 4, col: 4 }, to: { row: 4, col: 3 } }])).toEqual([
      { unitId: 1, facing: 'left' },
    ])
  })

  it('ignores a move for a unit no longer in the state', () => {
    expect(spriteWalksForTick(state(), 5, [{ ...moved, unitId: 99 }])).toEqual([])
  })

  it('starts a fresh walk on a single forward step', () => {
    expect(spriteWalksAfterSeek(state(), 4, 5, 5, [moved], 900, 640)).toEqual([
      { unitId: 1, facing: 'right', startedAt: 900, activeUntil: 1540 },
    ])
  })

  it.each([[6, 5, 5], [0, 5, 5], [5, 5, 5], [4, 5, 4], [5, 0, 0]])(
    'clears transient walks when seeking from %s to %s (resolved %s)',
    (previous, requested, resolved) => {
      expect(spriteWalksAfterSeek(state(), previous, requested, resolved, [moved], 0, 640)).toEqual([])
    },
  )

  it('clears walks when there is no prior battle', () => {
    expect(spriteWalksAfterSeek(null, 4, 5, 5, [moved], 0, 640)).toEqual([])
  })

  it('keeps the original startedAt across consecutive hex-steps so the loop phase keeps advancing', () => {
    const active = [{ unitId: 1, facing: 'right' as const, startedAt: 0, activeUntil: 640 }]
    const incoming = [{ unitId: 1, facing: 'right' as const }]
    expect(mergeSpriteWalks(active, incoming, 240, 640)).toEqual([
      { unitId: 1, facing: 'right', startedAt: 0, activeUntil: 880 },
    ])
  })

  it('updates facing on a refreshed walk without resetting its phase', () => {
    const active = [{ unitId: 1, facing: 'right' as const, startedAt: 0, activeUntil: 640 }]
    const incoming = [{ unitId: 1, facing: 'left' as const }]
    expect(mergeSpriteWalks(active, incoming, 240, 640)[0]).toEqual({
      unitId: 1, facing: 'left', startedAt: 0, activeUntil: 880,
    })
  })

  it('starts a brand-new walker with its own startedAt', () => {
    const active = [{ unitId: 1, facing: 'right' as const, startedAt: 0, activeUntil: 640 }]
    const incoming = [{ unitId: 2, facing: 'left' as const }]
    expect(mergeSpriteWalks(active, incoming, 100, 640)).toEqual([
      { unitId: 1, facing: 'right', startedAt: 0, activeUntil: 640 },
      { unitId: 2, facing: 'left', startedAt: 100, activeUntil: 740 },
    ])
  })

  it('drops a walk once no move has refreshed it within one loop duration', () => {
    const active = [{ unitId: 1, facing: 'right' as const, startedAt: 0, activeUntil: 640 }]
    expect(mergeSpriteWalks(active, [], 640, 640)).toEqual([])
    expect(mergeSpriteWalks(active, [], 639, 640)).toEqual(active)
  })
})

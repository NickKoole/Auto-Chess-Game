import { describe, expect, it } from 'vitest'
import type { components } from '@/api/schema.d.ts'
import { SIDE_PLAYER } from '../battleState'
import {
  compositionToBattleState,
  isInPlayerBand,
  placeUnit,
  PLAYER_BAND_FIRST_ROW,
  PLAYER_BAND_LAST_ROW,
  type CompositionPlacement,
} from '../composition'

type UnitDefinitionDto = components['schemas']['UnitDefinitionDto']

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

describe('isInPlayerBand', () => {
  it('is true for every row in the player deployment band', () => {
    for (let row = PLAYER_BAND_FIRST_ROW; row <= PLAYER_BAND_LAST_ROW; row++) {
      expect(isInPlayerBand(row)).toBe(true)
    }
  })

  it('is false for the neutral band, the opponent band, and off the board', () => {
    expect(isInPlayerBand(3)).toBe(false)
    expect(isInPlayerBand(4)).toBe(false)
    expect(isInPlayerBand(0)).toBe(false)
    expect(isInPlayerBand(2)).toBe(false)
    expect(isInPlayerBand(-1)).toBe(false)
    expect(isInPlayerBand(99)).toBe(false)
  })
})

describe('placeUnit', () => {
  it('places a unit on an empty tile within the player band', () => {
    const result = placeUnit([], 'knight', { row: 5, col: 2 })
    expect(result).toEqual({ ok: true, placements: [{ unitId: 'knight', position: { row: 5, col: 2 } }] })
  })

  it('rejects a tile outside the player band, naming the row', () => {
    const result = placeUnit([], 'knight', { row: 3, col: 2 })
    expect(result).toMatchObject({
      ok: false,
      reason: expect.stringContaining('outside your deployment band'),
    })
  })

  it('rejects a tile in the opponent band', () => {
    const result = placeUnit([], 'knight', { row: 0, col: 2 })
    expect(result.ok).toBe(false)
  })

  it('replaces whatever already occupies the same tile instead of rejecting', () => {
    const withKnight: CompositionPlacement[] = [{ unitId: 'knight', position: { row: 5, col: 2 } }]
    const result = placeUnit(withKnight, 'archer', { row: 5, col: 2 })
    expect(result).toEqual({ ok: true, placements: [{ unitId: 'archer', position: { row: 5, col: 2 } }] })
  })

  it('leaves other placements untouched when placing on a different tile', () => {
    const existing: CompositionPlacement[] = [{ unitId: 'knight', position: { row: 5, col: 2 } }]
    const result = placeUnit(existing, 'archer', { row: 6, col: 3 })
    expect(result).toEqual({
      ok: true,
      placements: [
        { unitId: 'knight', position: { row: 5, col: 2 } },
        { unitId: 'archer', position: { row: 6, col: 3 } },
      ],
    })
  })

  it('does not mutate the array passed in', () => {
    const existing: CompositionPlacement[] = [{ unitId: 'knight', position: { row: 5, col: 2 } }]
    placeUnit(existing, 'archer', { row: 6, col: 3 })
    expect(existing).toEqual([{ unitId: 'knight', position: { row: 5, col: 2 } }])
  })
})

describe('compositionToBattleState', () => {
  it('builds one full-hp player unit per placement, with no current target', () => {
    const placements: CompositionPlacement[] = [
      { unitId: 'knight', position: { row: 5, col: 2 } },
      { unitId: 'knight', position: { row: 6, col: 3 } },
    ]
    const definitions = { knight: knightDefinition() }

    const state = compositionToBattleState(placements, definitions)

    expect(state.units).toEqual([
      { id: 0, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 5, col: 2 }, hp: 180, maxHp: 180, currentTargetId: null },
      { id: 1, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 6, col: 3 }, hp: 180, maxHp: 180, currentTargetId: null },
    ])
    expect(state.unitDefinitions).toBe(definitions)
  })

  it('gives an unknown definition id zero HP rather than throwing', () => {
    const state = compositionToBattleState([{ unitId: 'ghost', position: { row: 5, col: 2 } }], {})
    expect(state.units[0]).toMatchObject({ hp: 0, maxHp: 0 })
  })
})

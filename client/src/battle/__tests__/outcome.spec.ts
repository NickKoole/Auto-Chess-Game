import { describe, expect, it } from 'vitest'
import type { BattleState } from '../battleState'
import { SIDE_OPPONENT, SIDE_PLAYER } from '../battleState'
import {
  ENDING_MUTUAL_ELIMINATION,
  ENDING_OPPONENT_ELIMINATED_PLAYER,
  ENDING_PLAYER_ELIMINATED_OPPONENT,
  ENDING_TICK_CAP_DRAW,
  endingLabel,
  survivorsBySide,
} from '../outcome'

function state(): BattleState {
  return {
    units: [
      {
        id: 2,
        side: SIDE_PLAYER,
        definitionId: 'knight',
        position: { row: 4, col: 2 },
        hp: 20,
        maxHp: 180,
        currentTargetId: null,
      },
      {
        id: 0,
        side: SIDE_PLAYER,
        definitionId: 'knight',
        position: { row: 5, col: 2 },
        hp: 180,
        maxHp: 180,
        currentTargetId: null,
      },
      {
        id: 3,
        side: SIDE_OPPONENT,
        definitionId: 'archer',
        position: { row: 2, col: 4 },
        hp: 5,
        maxHp: 70,
        currentTargetId: null,
      },
    ],
    unitDefinitions: {},
  }
}

describe('survivorsBySide', () => {
  it('groups units by side, each sorted by id', () => {
    const groups = survivorsBySide(state())

    expect(groups).toEqual([
      {
        side: SIDE_PLAYER,
        units: [
          {
            id: 0,
            side: SIDE_PLAYER,
            definitionId: 'knight',
            position: { row: 5, col: 2 },
            hp: 180,
            maxHp: 180,
            currentTargetId: null,
          },
          {
            id: 2,
            side: SIDE_PLAYER,
            definitionId: 'knight',
            position: { row: 4, col: 2 },
            hp: 20,
            maxHp: 180,
            currentTargetId: null,
          },
        ],
      },
      {
        side: SIDE_OPPONENT,
        units: [
          {
            id: 3,
            side: SIDE_OPPONENT,
            definitionId: 'archer',
            position: { row: 2, col: 4 },
            hp: 5,
            maxHp: 70,
            currentTargetId: null,
          },
        ],
      },
    ])
  })

  it('reports an empty list for a side with no survivors, not an absent entry', () => {
    const wiped: BattleState = {
      units: [
        {
          id: 0,
          side: SIDE_PLAYER,
          definitionId: 'knight',
          position: { row: 5, col: 2 },
          hp: 30,
          maxHp: 30,
          currentTargetId: null,
        },
      ],
      unitDefinitions: {},
    }

    const groups = survivorsBySide(wiped)

    expect(groups.find((g) => g.side === SIDE_OPPONENT)).toEqual({ side: SIDE_OPPONENT, units: [] })
  })
})

describe('endingLabel', () => {
  it('gives every ending its own text', () => {
    const labels = [
      ENDING_PLAYER_ELIMINATED_OPPONENT,
      ENDING_OPPONENT_ELIMINATED_PLAYER,
      ENDING_MUTUAL_ELIMINATION,
      ENDING_TICK_CAP_DRAW,
    ].map(endingLabel)

    expect(new Set(labels).size).toBe(4)
  })

  it('distinguishes the two draw endings from each other', () => {
    expect(endingLabel(ENDING_MUTUAL_ELIMINATION)).not.toBe(endingLabel(ENDING_TICK_CAP_DRAW))
    expect(endingLabel(ENDING_MUTUAL_ELIMINATION)).toMatch(/draw/i)
    expect(endingLabel(ENDING_TICK_CAP_DRAW)).toMatch(/draw/i)
  })

  it('falls back to a labelled unknown value rather than throwing', () => {
    expect(endingLabel(99)).toBe('Unknown ending (99)')
  })
})

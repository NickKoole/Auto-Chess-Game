import { describe, expect, it } from 'vitest'
import type { BattleState } from '../battleState'
import { SIDE_OPPONENT, SIDE_PLAYER } from '../battleState'
import { applyTick, type BattleEvent } from '../applyTick'

function state(): BattleState {
  return {
    units: [
      { id: 0, side: SIDE_PLAYER, definitionId: 'knight', position: { row: 5, col: 2 }, hp: 30, maxHp: 30 },
      { id: 1, side: SIDE_OPPONENT, definitionId: 'knight', position: { row: 2, col: 2 }, hp: 30, maxHp: 30 },
    ],
    unitDefinitions: {},
  }
}

describe('applyTick', () => {
  it('ignores events for a different tick', () => {
    const s = state()
    const log: BattleEvent[] = [
      { type: 'unitMoved', unitId: 0, from: { row: 5, col: 2 }, to: { row: 4, col: 2 }, tick: 5 },
    ]

    applyTick(s, 1, log)

    expect(s.units[0]!.position).toEqual({ row: 5, col: 2 })
  })

  it('moves a unit to the event target hex', () => {
    const s = state()
    const log: BattleEvent[] = [
      { type: 'unitMoved', unitId: 0, from: { row: 5, col: 2 }, to: { row: 4, col: 2 }, tick: 3 },
    ]

    applyTick(s, 3, log)

    expect(s.units[0]!.position).toEqual({ row: 4, col: 2 })
  })

  it('sets HP to the event resultingHp rather than computing a subtraction', () => {
    const s = state()
    const log: BattleEvent[] = [
      { type: 'damageDealt', attackerId: 1, targetId: 0, amount: 12, resultingHp: 18, tick: 10 },
    ]

    applyTick(s, 10, log)

    expect(s.units[0]!.hp).toBe(18)
  })

  it('removes a unit from state on death', () => {
    const s = state()
    const log: BattleEvent[] = [{ type: 'unitDied', unitId: 1, position: { row: 2, col: 2 }, tick: 20 }]

    applyTick(s, 20, log)

    expect(s.units.map((unit) => unit.id)).toEqual([0])
  })

  it('applies every event for a tick, in log order', () => {
    const s = state()
    const log: BattleEvent[] = [
      { type: 'damageDealt', attackerId: 1, targetId: 0, amount: 5, resultingHp: 25, tick: 7 },
      { type: 'unitMoved', unitId: 0, from: { row: 5, col: 2 }, to: { row: 4, col: 2 }, tick: 7 },
    ]

    applyTick(s, 7, log)

    expect(s.units[0]).toMatchObject({ hp: 25, position: { row: 4, col: 2 } })
  })

  it('ignores unitSpawned and unitAttacked - neither changes visible state', () => {
    const s = state()
    const before = JSON.parse(JSON.stringify(s))
    const log: BattleEvent[] = [
      {
        type: 'unitSpawned',
        unitId: 2,
        side: SIDE_PLAYER,
        definitionId: 'archer',
        position: { row: 5, col: 3 },
        tick: 0,
      },
      { type: 'unitAttacked', attackerId: 0, targetId: 1, tick: 0 },
    ]

    applyTick(s, 0, log)

    expect(s).toEqual(before)
  })

  it('fails loudly on an unhandled event type instead of skipping it', () => {
    const s = state()
    const log = [{ type: 'somethingNew', tick: 1 }] as unknown as BattleEvent[]

    expect(() => applyTick(s, 1, log)).toThrow(/somethingNew/)
  })

  it('throws if an event targets a unit id not present in state', () => {
    const s = state()
    const log: BattleEvent[] = [
      { type: 'damageDealt', attackerId: 0, targetId: 99, amount: 5, resultingHp: 10, tick: 1 },
    ]

    expect(() => applyTick(s, 1, log)).toThrow(/99/)
  })
})

import type { components } from '@/api/schema.d.ts'
import { toNumber } from '@/api/numeric'
import type { BattleState } from './battleState'

type UnitSpawnedEventDto = components['schemas']['BattleEventDtoUnitSpawnedEventDto']
type UnitMovedEventDto = components['schemas']['BattleEventDtoUnitMovedEventDto']
type UnitAttackedEventDto = components['schemas']['BattleEventDtoUnitAttackedEventDto']
type DamageDealtEventDto = components['schemas']['BattleEventDtoDamageDealtEventDto']
type UnitDiedEventDto = components['schemas']['BattleEventDtoUnitDiedEventDto']

/**
 * The wire event union with `type` narrowed from optional to required. The
 * generated schema marks it optional because ASP.NET Core's OpenAPI writer
 * doesn't flag a JsonPolymorphic discriminator as required, but the server
 * always sends it - narrowing it here is what makes the switch in
 * `applyEvent` a real compile-time exhaustiveness check, not just a runtime
 * one.
 */
export type BattleEvent =
  | (Omit<UnitSpawnedEventDto, 'type'> & { type: 'unitSpawned' })
  | (Omit<UnitMovedEventDto, 'type'> & { type: 'unitMoved' })
  | (Omit<UnitAttackedEventDto, 'type'> & { type: 'unitAttacked' })
  | (Omit<DamageDealtEventDto, 'type'> & { type: 'damageDealt' })
  | (Omit<UnitDiedEventDto, 'type'> & { type: 'unitDied' })

/**
 * Applies every event at `tick` to `state`, in log order. Pure with respect
 * to `log` - it reads events and mutates only `state`. No combat rules live
 * here: damage is copied verbatim from `resultingHp`, death removes exactly
 * the unit the log names, and nothing is computed from stats or targeting.
 */
export function applyTick(state: BattleState, tick: number, log: readonly BattleEvent[]): void {
  for (const event of log) {
    if (toNumber(event.tick) === tick) {
      applyEvent(state, event)
    }
  }
}

function applyEvent(state: BattleState, event: BattleEvent): void {
  switch (event.type) {
    case 'unitSpawned':
    case 'unitAttacked':
      // Spawning is already reflected in the tick-0 battle state (story
      // 2.3). An attack alone changes nothing visible - the damageDealt
      // event that follows it is what moves HP.
      return
    case 'unitMoved':
      findUnit(state, toNumber(event.unitId)).position = {
        row: toNumber(event.to.row),
        col: toNumber(event.to.col),
      }
      return
    case 'damageDealt':
      findUnit(state, toNumber(event.targetId)).hp = toNumber(event.resultingHp)
      return
    case 'unitDied': {
      const id = toNumber(event.unitId)
      state.units = state.units.filter((unit) => unit.id !== id)
      return
    }
    default:
      return assertExhaustive(event)
  }
}

function findUnit(state: BattleState, id: number) {
  const unit = state.units.find((candidate) => candidate.id === id)
  if (!unit) {
    throw new Error(`applyTick: no unit with id ${id} in battle state.`)
  }
  return unit
}

function assertExhaustive(event: never): never {
  const type = (event as { type?: unknown }).type
  throw new Error(`Unhandled battle event type '${String(type)}'.`)
}

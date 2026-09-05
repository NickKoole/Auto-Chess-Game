import { toNumber } from '@/api/numeric'
import type { BattleEvent } from './applyTick'
import type { BattleState, HexPosition } from './battleState'

const MELEE_RANGE_HEXES = 1

export interface Projectile {
  id: string
  from: HexPosition
  to: HexPosition
}

/**
 * `state` must already reflect `tick` applied - attacker/target positions
 * are read live from it, not reconstructed, so a unit that died this same
 * tick is skipped rather than guessed at.
 */
export function projectilesForTick(state: BattleState, tick: number, log: readonly BattleEvent[]): Projectile[] {
  const projectiles: Projectile[] = []

  for (const event of log) {
    if (event.type !== 'unitAttacked' || toNumber(event.tick) !== tick) {
      continue
    }

    const attackerId = toNumber(event.attackerId)
    const targetId = toNumber(event.targetId)
    const attacker = state.units.find((unit) => unit.id === attackerId)
    const target = state.units.find((unit) => unit.id === targetId)
    if (!attacker || !target) {
      continue
    }

    const range = toNumber(state.unitDefinitions[attacker.definitionId]?.attackRangeHexes ?? MELEE_RANGE_HEXES)
    if (range <= MELEE_RANGE_HEXES) {
      continue
    }

    projectiles.push({ id: `${attackerId}-${targetId}-${tick}`, from: attacker.position, to: target.position })
  }

  return projectiles
}

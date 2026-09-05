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
 * `preTickState` is the state as it stood before `tick` was applied,
 * `postTickState` after. Both are needed because a unit decides its own
 * action independently each tick: the attacker never also moves this tick
 * (so its pre-tick position is authoritative), but the target might - its
 * post-tick position is preferred, falling back to pre-tick only when a
 * same-tick `unitDied` (sharing the killing blow's `unitAttacked` tick) has
 * already removed it from `postTickState`.
 */
export function projectilesForTick(
  preTickState: BattleState,
  postTickState: BattleState,
  tick: number,
  log: readonly BattleEvent[],
): Projectile[] {
  const projectiles: Projectile[] = []

  for (const event of log) {
    if (event.type !== 'unitAttacked' || toNumber(event.tick) !== tick) {
      continue
    }

    const attackerId = toNumber(event.attackerId)
    const targetId = toNumber(event.targetId)
    const attacker = preTickState.units.find((unit) => unit.id === attackerId)
    if (!attacker) {
      continue
    }

    const range = toNumber(preTickState.unitDefinitions[attacker.definitionId]?.attackRangeHexes ?? MELEE_RANGE_HEXES)
    if (range <= MELEE_RANGE_HEXES) {
      continue
    }

    const target =
      postTickState.units.find((unit) => unit.id === targetId) ??
      preTickState.units.find((unit) => unit.id === targetId)
    if (!target) {
      continue
    }

    projectiles.push({ id: `${attackerId}-${targetId}-${tick}`, from: attacker.position, to: target.position })
  }

  return projectiles
}

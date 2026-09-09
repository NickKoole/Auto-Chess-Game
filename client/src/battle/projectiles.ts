import { toNumber } from '@/api/numeric'
import type { BattleEvent } from './applyTick'
import type { BattleState, HexPosition } from './battleState'

const MELEE_RANGE_HEXES = 1

// A shot's own visible lifetime, independent of the simulation's tick rate.
export const PROJECTILE_VISIBLE_MS = 150

export interface Projectile {
  id: string
  from: HexPosition
  to: HexPosition
}

// Same as Projectile, plus the wall-clock moment it was fired, so a renderer
// can interpolate its position without relying on SMIL/CSS animation timing.
export interface TimedProjectile extends Projectile {
  firedAt: number
}

/**
 * `preTickState` is the state as it stood before `tick` was applied,
 * `postTickState` after. Both are needed because a unit decides its own
 * action independently each tick: the attacker never also moves this tick
 * (so its pre-tick position is authoritative), but the target might - its
 * post-tick position is preferred. If a same-tick `unitDied` (sharing the
 * killing blow's `unitAttacked` tick) has already removed it from
 * `postTickState`, that event's own `position` is used instead of
 * `preTickState`'s, since the server records where a unit died *after* any
 * movement that tick, not where it started.
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

    const targetPosition =
      postTickState.units.find((unit) => unit.id === targetId)?.position ??
      deathPositionOf(log, targetId, tick) ??
      preTickState.units.find((unit) => unit.id === targetId)?.position
    if (!targetPosition) {
      continue
    }

    projectiles.push({ id: `${attackerId}-${targetId}-${tick}`, from: attacker.position, to: targetPosition })
  }

  return projectiles
}

function deathPositionOf(log: readonly BattleEvent[], unitId: number, tick: number): HexPosition | undefined {
  for (const event of log) {
    if (event.type === 'unitDied' && toNumber(event.unitId) === unitId && toNumber(event.tick) === tick) {
      return { row: toNumber(event.position.row), col: toNumber(event.position.col) }
    }
  }
  return undefined
}

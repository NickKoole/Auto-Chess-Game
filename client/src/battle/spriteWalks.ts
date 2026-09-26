import { toNumber } from '@/api/numeric'
import type { SpriteFacing } from '@/sprites/spriteAnimation'
import type { BattleEvent } from './applyTick'
import { facingTarget } from './spriteAttacks'
import { SIDE_PLAYER, type BattleState } from './battleState'

export interface SpriteWalk {
  unitId: number
  facing: SpriteFacing
  startedAt: number
  activeUntil: number
}

export interface MovedUnit {
  unitId: number
  facing: SpriteFacing
}

export function spriteWalksForTick(before: BattleState, tick: number, log: readonly BattleEvent[]): MovedUnit[] {
  const moved: MovedUnit[] = []
  for (const event of log) {
    if (event.type !== 'unitMoved' || toNumber(event.tick) !== tick) {
      continue
    }
    const unitId = toNumber(event.unitId)
    const unit = before.units.find((candidate) => candidate.id === unitId)
    if (!unit) {
      continue
    }
    const from = { row: toNumber(event.from.row), col: toNumber(event.from.col) }
    const to = { row: toNumber(event.to.row), col: toNumber(event.to.col) }
    moved.push({ unitId, facing: facingTarget(from, to, unit.side === SIDE_PLAYER ? 'right' : 'left') })
  }
  return moved
}

export function spriteWalksAfterSeek(
  before: BattleState | null,
  previousTick: number,
  requestedTick: number,
  resolvedTick: number,
  log: readonly BattleEvent[],
  startedAt: number,
  loopDurationMs: number,
): SpriteWalk[] {
  if (!before || requestedTick !== previousTick + 1 || resolvedTick !== requestedTick) {
    return []
  }
  return spriteWalksForTick(before, resolvedTick, log).map((walk) => ({
    ...walk,
    startedAt,
    activeUntil: startedAt + loopDurationMs,
  }))
}

/**
 * A unit already walking keeps its original `startedAt` so the four-frame
 * loop keeps advancing across consecutive hex-steps instead of restarting
 * each time - it only stops once no `unitMoved` event has refreshed it
 * within one loop duration.
 */
export function mergeSpriteWalks(
  active: readonly SpriteWalk[],
  incoming: readonly MovedUnit[],
  now: number,
  loopDurationMs: number,
): SpriteWalk[] {
  const incomingByUnit = new Map(incoming.map((walk) => [walk.unitId, walk]))
  const continued = active
    .filter((walk) => now < walk.activeUntil)
    .map((walk) => {
      const refresh = incomingByUnit.get(walk.unitId)
      return refresh ? { ...walk, facing: refresh.facing, activeUntil: now + loopDurationMs } : walk
    })
  const continuedIds = new Set(continued.map((walk) => walk.unitId))
  const started = incoming
    .filter((walk) => !continuedIds.has(walk.unitId))
    .map((walk) => ({ ...walk, startedAt: now, activeUntil: now + loopDurationMs }))
  return [...continued, ...started]
}

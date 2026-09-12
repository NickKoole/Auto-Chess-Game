import { toNumber } from '@/api/numeric'
import type { SpriteFacing } from '@/sprites/spriteAnimation'
import type { BattleEvent } from './applyTick'
import { SIDE_PLAYER, type BattleState, type HexPosition } from './battleState'
import { hexToPixel } from './hexCoords'

export interface SpriteAttack {
  unitId: number
  facing: SpriteFacing
  startedAt: number
}

export function facingTarget(from: HexPosition, to: HexPosition, fallback: SpriteFacing): SpriteFacing {
  const fromX = hexToPixel(from.row, from.col, 1).x
  const toX = hexToPixel(to.row, to.col, 1).x
  return toX < fromX ? 'left' : toX > fromX ? 'right' : fallback
}

export function spriteAttacksForTick(
  before: BattleState,
  after: BattleState,
  tick: number,
  log: readonly BattleEvent[],
  startedAt: number,
): SpriteAttack[] {
  const attacks: SpriteAttack[] = []
  for (const event of log) {
    if (event.type !== 'unitAttacked' || toNumber(event.tick) !== tick) {
      continue
    }
    const unitId = toNumber(event.attackerId)
    const targetId = toNumber(event.targetId)
    const attacker = before.units.find((unit) => unit.id === unitId)
    if (!attacker) {
      continue
    }
    const death = log.find((candidate) =>
      candidate.type === 'unitDied' &&
      toNumber(candidate.tick) === tick &&
      toNumber(candidate.unitId) === targetId,
    )
    const targetPosition =
      after.units.find((unit) => unit.id === targetId)?.position ??
      (death?.type === 'unitDied' ? { row: toNumber(death.position.row), col: toNumber(death.position.col) } : undefined) ??
      before.units.find((unit) => unit.id === targetId)?.position
    if (!targetPosition) {
      continue
    }
    attacks.push({
      unitId,
      facing: facingTarget(attacker.position, targetPosition, attacker.side === SIDE_PLAYER ? 'right' : 'left'),
      startedAt,
    })
  }
  return attacks
}

export function spriteAttacksAfterSeek(
  before: BattleState | null,
  after: BattleState,
  previousTick: number,
  requestedTick: number,
  resolvedTick: number,
  log: readonly BattleEvent[],
  startedAt: number,
): SpriteAttack[] {
  if (!before || requestedTick !== previousTick + 1 || resolvedTick !== requestedTick) {
    return []
  }
  return spriteAttacksForTick(before, after, resolvedTick, log, startedAt)
}

export function mergeSpriteAttacks(
  active: readonly SpriteAttack[],
  incoming: readonly SpriteAttack[],
  now: number,
  durationMs: number,
): SpriteAttack[] {
  const replaced = new Set(incoming.map((attack) => attack.unitId))
  return [
    ...active.filter((attack) => now - attack.startedAt < durationMs && !replaced.has(attack.unitId)),
    ...incoming,
  ]
}

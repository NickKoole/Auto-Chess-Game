import { poseAtElapsed, walkPhaseAtElapsed, type SpriteData, type SpriteFacing, type SpritePresentation } from '@/sprites/spriteAnimation'
import type { SpriteAttack } from './spriteAttacks'
import type { SpriteWalk } from './spriteWalks'

export interface UnitVisual {
  presentation: SpritePresentation
  facing: SpriteFacing
}

/**
 * Attack always wins over walking for the same unit, and an unmoving,
 * non-attacking unit stays idle - the two other cases the board renders.
 */
export function resolveUnitPresentation(
  sprite: SpriteData,
  attack: SpriteAttack | undefined,
  walk: SpriteWalk | undefined,
  now: number,
  fallbackFacing: SpriteFacing,
): UnitVisual {
  if (attack) {
    return {
      presentation: { kind: 'attack', pose: poseAtElapsed(sprite, now - attack.startedAt) },
      facing: attack.facing,
    }
  }
  if (walk) {
    return {
      presentation: { kind: 'walk', phase: walkPhaseAtElapsed(sprite, now - walk.startedAt) },
      facing: walk.facing,
    }
  }
  return { presentation: { kind: 'attack', pose: 'idle' }, facing: fallbackFacing }
}

import { describe, expect, it } from 'vitest'
import { knightSpriteData } from '@/sprites/knight'
import type { SpriteAttack } from '../spriteAttacks'
import type { SpriteWalk } from '../spriteWalks'
import { resolveUnitPresentation } from '../unitPresentation'

const attack: SpriteAttack = { unitId: 1, facing: 'right', startedAt: 100 }
const walk: SpriteWalk = { unitId: 1, facing: 'left', startedAt: 100, activeUntil: 740 }

describe('resolveUnitPresentation', () => {
  it('stays idle, facing the fallback, when neither attacking nor walking', () => {
    expect(resolveUnitPresentation(knightSpriteData, undefined, undefined, 500, 'right')).toEqual({
      presentation: { kind: 'attack', pose: 'idle' },
      facing: 'right',
    })
  })

  it('plays the walk loop and faces the movement direction while walking', () => {
    expect(resolveUnitPresentation(knightSpriteData, undefined, walk, 260, 'right')).toEqual({
      presentation: { kind: 'walk', phase: 'leftContact' },
      facing: 'left',
    })
  })

  it('gives the attack animation and facing priority over an in-progress walk', () => {
    expect(resolveUnitPresentation(knightSpriteData, attack, walk, 200, 'right')).toEqual({
      presentation: { kind: 'attack', pose: 'strike' },
      facing: 'right',
    })
  })

  it('selects the attack pose at the elapsed time since the attack started', () => {
    expect(resolveUnitPresentation(knightSpriteData, { ...attack, startedAt: 0 }, undefined, 5000, 'left')).toEqual({
      presentation: { kind: 'attack', pose: 'idle' },
      facing: 'right',
    })
  })
})

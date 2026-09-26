import knightSheet from '@/assets/sprites/knight/knight.png'
import knightWalkLeftPassing from '@/assets/sprites/knight/knight-walk-metal-1.png'
import knightWalkLeftContact from '@/assets/sprites/knight/knight-walk-metal-2.png'
import knightWalkRightPassing from '@/assets/sprites/knight/knight-walk-metal-3.png'
import knightWalkRightContact from '@/assets/sprites/knight/knight-walk-metal-4.png'
import { knightSpriteData } from './knight'
import type { SpriteDefinition } from './spriteAnimation'

const sprites: Readonly<Record<string, SpriteDefinition>> = {
  knight: {
    ...knightSpriteData,
    sheet: knightSheet,
    walkSheets: {
      leftPassing: knightWalkLeftPassing,
      leftContact: knightWalkLeftContact,
      rightPassing: knightWalkRightPassing,
      rightContact: knightWalkRightContact,
    },
  },
}

export function spriteForUnit(definitionId: string): SpriteDefinition | undefined {
  return Object.prototype.hasOwnProperty.call(sprites, definitionId) ? sprites[definitionId] : undefined
}

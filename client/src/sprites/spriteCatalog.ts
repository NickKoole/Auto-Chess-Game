import knightSheet from '@/assets/sprites/knight/knight.png'
import { knightSpriteData } from './knight'
import type { SpriteDefinition } from './spriteAnimation'

const sprites: Readonly<Record<string, SpriteDefinition>> = {
  knight: { ...knightSpriteData, sheet: knightSheet },
}

export function spriteForUnit(definitionId: string): SpriteDefinition | undefined {
  return Object.prototype.hasOwnProperty.call(sprites, definitionId) ? sprites[definitionId] : undefined
}

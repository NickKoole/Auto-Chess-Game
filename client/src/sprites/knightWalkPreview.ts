import knightWalkIndividualA from '@/assets/sprites/knight/knight-walk-metal-1.png'
import knightWalkIndividualB from '@/assets/sprites/knight/knight-walk-metal-2.png'
import knightWalkIndividualC from '@/assets/sprites/knight/knight-walk-metal-3.png'
import knightWalkIndividualD from '@/assets/sprites/knight/knight-walk-metal-4.png'
import type { SpriteFrame } from './spriteAnimation'

export const walkPhases = ['leftPassing', 'leftContact', 'rightPassing', 'rightContact'] as const

export type WalkPhase = (typeof walkPhases)[number]

export interface WalkPreviewSprite {
  name: string
  sheets: Record<WalkPhase, string>
  width: number
  height: number
  scale: number
  frames: Record<WalkPhase, SpriteFrame>
}

export const knightWalkCandidates: readonly WalkPreviewSprite[] = [
  {
    name: 'Metal knight',
    sheets: {
      leftPassing: knightWalkIndividualA,
      leftContact: knightWalkIndividualB,
      rightPassing: knightWalkIndividualC,
      rightContact: knightWalkIndividualD,
    },
    width: 1254,
    height: 1254,
    scale: 0.1,
    frames: {
      leftPassing: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      leftContact: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      rightPassing: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      rightContact: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
    },
  },
]

export const walkFrameDurationMs = 160

export function walkPhaseAtElapsed(elapsedMs: number | null): WalkPhase {
  if (elapsedMs === null || !Number.isFinite(elapsedMs) || elapsedMs < 0) {
    return 'leftPassing'
  }
  return walkPhases[Math.floor(elapsedMs / walkFrameDurationMs) % walkPhases.length]!
}

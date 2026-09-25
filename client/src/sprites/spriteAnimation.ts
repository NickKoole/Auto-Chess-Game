export type SpritePose = 'idle' | 'windUp' | 'strike' | 'recovery'
export type SpriteFacing = 'left' | 'right'
export type WalkPhase = 'leftPassing' | 'leftContact' | 'rightPassing' | 'rightContact'

export interface SpriteFrame {
  x: number
  y: number
  width: number
  height: number
  anchorX: number
  anchorY: number
}

export interface WalkSpriteData {
  order: readonly WalkPhase[]
  frames: Record<WalkPhase, SpriteFrame>
  frameDurationMs: number
}

export interface SpriteData {
  width: number
  height: number
  scale: number
  frames: Record<SpritePose, SpriteFrame>
  attack: readonly { pose: Exclude<SpritePose, 'idle'>; durationMs: number }[]
  walk: WalkSpriteData
}

export interface SpriteDefinition extends SpriteData {
  sheet: string
  walkSheets: Record<WalkPhase, string>
}

export type SpritePresentation = { kind: 'attack'; pose: SpritePose } | { kind: 'walk'; phase: WalkPhase }

export function attackDuration(sprite: SpriteData): number {
  return sprite.attack.reduce((duration, frame) => duration + frame.durationMs, 0)
}

export function poseAtElapsed(sprite: SpriteData, elapsedMs: number | null): SpritePose {
  if (elapsedMs === null || !Number.isFinite(elapsedMs) || elapsedMs < 0) {
    return 'idle'
  }
  let end = 0
  for (const frame of sprite.attack) {
    end += frame.durationMs
    if (elapsedMs < end) {
      return frame.pose
    }
  }
  return 'idle'
}

export function walkLoopDuration(sprite: SpriteData): number {
  return sprite.walk.frameDurationMs * sprite.walk.order.length
}

export function walkPhaseAtElapsed(sprite: SpriteData, elapsedMs: number | null): WalkPhase {
  const { order, frameDurationMs } = sprite.walk
  if (elapsedMs === null || !Number.isFinite(elapsedMs) || elapsedMs < 0) {
    return order[0]!
  }
  return order[Math.floor(elapsedMs / frameDurationMs) % order.length]!
}

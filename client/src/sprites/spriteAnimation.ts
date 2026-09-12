export type SpritePose = 'idle' | 'windUp' | 'strike' | 'recovery'
export type SpriteFacing = 'left' | 'right'

export interface SpriteFrame {
  x: number
  y: number
  width: number
  height: number
  anchorX: number
  anchorY: number
}

export interface SpriteData {
  width: number
  height: number
  scale: number
  frames: Record<SpritePose, SpriteFrame>
  attack: readonly { pose: Exclude<SpritePose, 'idle'>; durationMs: number }[]
}

export interface SpriteDefinition extends SpriteData {
  sheet: string
}

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

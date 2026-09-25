import type { SpriteData } from './spriteAnimation'

export const knightSpriteData: SpriteData = {
  width: 1254,
  height: 1254,
  scale: 0.14,
  frames: {
    idle: { x: 0, y: 0, width: 680, height: 627, anchorX: 320, anchorY: 598 },
    windUp: { x: 680, y: 0, width: 574, height: 627, anchorX: 270, anchorY: 598 },
    strike: { x: 0, y: 627, width: 680, height: 627, anchorX: 330, anchorY: 521 },
    recovery: { x: 680, y: 627, width: 574, height: 627, anchorX: 275, anchorY: 521 },
  },
  attack: [
    { pose: 'windUp', durationMs: 100 },
    { pose: 'strike', durationMs: 80 },
    { pose: 'recovery', durationMs: 140 },
  ],
  walk: {
    order: ['leftPassing', 'leftContact', 'rightPassing', 'rightContact'],
    frameDurationMs: 160,
    frames: {
      leftPassing: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      leftContact: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      rightPassing: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
      rightContact: { x: 0, y: 0, width: 1254, height: 1254, anchorX: 627, anchorY: 1080 },
    },
  },
}

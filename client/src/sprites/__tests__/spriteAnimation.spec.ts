import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { knightSpriteData } from '../knight'
import { attackDuration, poseAtElapsed } from '../spriteAnimation'
import { spriteForUnit } from '../spriteCatalog'

describe('knight animation', () => {
  it.each([
    [null, 'idle'], [-1, 'idle'], [NaN, 'idle'], [Infinity, 'idle'],
    [0, 'windUp'], [99, 'windUp'], [100, 'strike'], [179, 'strike'],
    [180, 'recovery'], [319, 'recovery'], [320, 'idle'], [5000, 'idle'],
  ] as const)('selects %s ms as %s', (elapsed, pose) => {
    expect(poseAtElapsed(knightSpriteData, elapsed)).toBe(pose)
  })

  it('finishes the attack in 320 ms', () => {
    expect(attackDuration(knightSpriteData)).toBe(320)
  })

  it('only maps registered unit definitions to artwork', () => {
    expect(spriteForUnit('knight')?.sheet).toContain('knight.png')
    expect(spriteForUnit('archer')).toBeUndefined()
    expect(spriteForUnit('toString')).toBeUndefined()
  })

  it('matches the committed RGBA PNG dimensions and keeps every region inside it', () => {
    const png = readFileSync('src/assets/sprites/knight/knight.png')
    expect(png.subarray(0, 8)).toEqual(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
    expect(png.readUInt32BE(16)).toBe(knightSpriteData.width)
    expect(png.readUInt32BE(20)).toBe(knightSpriteData.height)
    expect(png[25]).toBe(6)
    for (const frame of Object.values(knightSpriteData.frames)) {
      expect(frame.x).toBeGreaterThanOrEqual(0)
      expect(frame.y).toBeGreaterThanOrEqual(0)
      expect(frame.x + frame.width).toBeLessThanOrEqual(knightSpriteData.width)
      expect(frame.y + frame.height).toBeLessThanOrEqual(knightSpriteData.height)
      expect(frame.anchorX).toBeGreaterThan(0)
      expect(frame.anchorX).toBeLessThan(frame.width)
      expect(frame.anchorY).toBeGreaterThan(0)
      expect(frame.anchorY).toBeLessThan(frame.height)
    }
  })
})

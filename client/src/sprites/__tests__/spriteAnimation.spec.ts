import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { createHash } from 'node:crypto'
import { alphaBoundingBox, decodeRgbaPng } from '../../../scripts/png-alpha-bbox.mjs'
import { knightSpriteData } from '../knight'
import { attackDuration, poseAtElapsed, scaleFor, walkLoopDuration, walkPhaseAtElapsed } from '../spriteAnimation'
import { spriteForUnit } from '../spriteCatalog'

const walkPhaseFileNumber: Record<(typeof knightSpriteData.walk.order)[number], number> = {
  leftPassing: 1,
  leftContact: 2,
  rightPassing: 3,
  rightContact: 4,
}

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

  it.each([
    [null, 'leftPassing'], [-1, 'leftPassing'], [NaN, 'leftPassing'], [Infinity, 'leftPassing'],
    [0, 'leftPassing'], [159, 'leftPassing'], [160, 'leftContact'], [319, 'leftContact'],
    [320, 'rightPassing'], [479, 'rightPassing'], [480, 'rightContact'], [639, 'rightContact'], [640, 'leftPassing'],
  ] as const)('cycles the walk loop at %s ms as %s', (elapsed, phase) => {
    expect(walkPhaseAtElapsed(knightSpriteData, elapsed)).toBe(phase)
  })

  it('completes one walk loop in 640 ms', () => {
    expect(walkLoopDuration(knightSpriteData)).toBe(640)
  })

  it('keeps every walk frame region inside the shared knight canvas', () => {
    for (const frame of Object.values(knightSpriteData.walk.frames)) {
      expect(frame.x + frame.width).toBeLessThanOrEqual(knightSpriteData.width)
      expect(frame.y + frame.height).toBeLessThanOrEqual(knightSpriteData.height)
      expect(frame.anchorX).toBeGreaterThan(0)
      expect(frame.anchorX).toBeLessThan(frame.width)
      expect(frame.anchorY).toBeGreaterThan(0)
      expect(frame.anchorY).toBeLessThan(frame.height)
    }
  })

  it('renders every walk phase at the same on-screen knight height as the idle attack pose', () => {
    const idlePng = decodeRgbaPng(readFileSync('src/assets/sprites/knight/knight.png'))
    const idleFrame = knightSpriteData.frames.idle
    const idleContentHeight = alphaBoundingBox(idlePng, idleFrame).height
    const idleRenderedHeight = idleContentHeight * scaleFor(knightSpriteData, { kind: 'attack', pose: 'idle' })

    for (const phase of knightSpriteData.walk.order) {
      const walkPng = decodeRgbaPng(readFileSync(`src/assets/sprites/knight/knight-walk-metal-${walkPhaseFileNumber[phase]}.png`))
      const walkContentHeight = alphaBoundingBox(walkPng).height
      const walkRenderedHeight = walkContentHeight * scaleFor(knightSpriteData, { kind: 'walk', phase })
      expect(walkRenderedHeight).toBeGreaterThan(idleRenderedHeight * 0.95)
      expect(walkRenderedHeight).toBeLessThan(idleRenderedHeight * 1.05)
    }
  })

  it('maps every walk phase to a distinct committed sprite sheet', () => {
    const sprite = spriteForUnit('knight')!
    const sheets = new Set(Object.values(sprite.walkSheets))
    expect(sheets.size).toBe(4)
    for (const sheet of sheets) {
      expect(sheet).toContain('knight-walk-metal-')
    }
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

  it('retains the original blue knight as a matching alternate sheet', () => {
    const metal = readFileSync('src/assets/sprites/knight/knight.png')
    const blue = readFileSync('src/assets/sprites/knight/knight-blue.png')
    expect(blue.subarray(0, 8)).toEqual(metal.subarray(0, 8))
    expect(blue.readUInt32BE(16)).toBe(knightSpriteData.width)
    expect(blue.readUInt32BE(20)).toBe(knightSpriteData.height)
    expect(blue[25]).toBe(6)
    expect(createHash('sha256').update(blue).digest('hex')).toBe('36f60438072aaa2dedbd3ff3fa10dac6eee5366fce4c01a4dfaf9b4bc38ca900')
    expect(blue.equals(metal)).toBe(false)
  })
})

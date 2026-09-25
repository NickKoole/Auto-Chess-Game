import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { knightWalkCandidates, walkFrameDurationMs, walkPhaseAtElapsed, walkPhases } from '../knightWalkPreview'

describe('knight walk preview', () => {
  it.each([
    [null, 'leftPassing'], [-1, 'leftPassing'], [NaN, 'leftPassing'], [Infinity, 'leftPassing'],
    [0, 'leftPassing'], [159, 'leftPassing'], [160, 'leftContact'], [319, 'leftContact'],
    [320, 'rightPassing'], [479, 'rightPassing'], [480, 'rightContact'], [639, 'rightContact'], [640, 'leftPassing'],
  ] as const)('selects %s ms as %s', (elapsed, phase) => {
    expect(walkPhaseAtElapsed(elapsed)).toBe(phase)
  })

  it('keeps the individual frames as in-bounds RGBA sprites', () => {
    const candidate = knightWalkCandidates[0]!
    for (const phase of [1, 2, 3, 4]) {
      const png = readFileSync(`src/assets/sprites/knight/knight-walk-metal-${phase}.png`)
      expect(png).toEqual(readFileSync(`public/walk-review/frame-${phase}-metal.png`))
      expect(png.subarray(0, 8)).toEqual(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
      expect(png.readUInt32BE(16)).toBe(candidate.width)
      expect(png.readUInt32BE(20)).toBe(candidate.height)
      expect(png[25]).toBe(6)
    }
    for (const phase of walkPhases) {
      const frame = candidate.frames[phase]
      expect(frame.x + frame.width).toBeLessThanOrEqual(candidate.width)
      expect(frame.y + frame.height).toBeLessThanOrEqual(candidate.height)
      expect(frame.anchorX).toBeGreaterThan(0)
      expect(frame.anchorX).toBeLessThan(frame.width)
      expect(frame.anchorY).toBeGreaterThan(0)
      expect(frame.anchorY).toBeLessThan(frame.height)
    }
  })

  it('holds every gait phase for 160 ms', () => {
    expect(walkFrameDurationMs).toBe(160)
  })

})

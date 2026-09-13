import { describe, expect, it } from 'vitest'
import { gridDimensions, hexPolygonPoints, hexToPixel } from '../hexCoords'

describe('hexToPixel', () => {
  it('places an even column with no vertical offset', () => {
    expect(hexToPixel(0, 0, 10)).toEqual({ x: 0, y: 0 })

    const { x, y } = hexToPixel(3, 2, 10)
    expect(x).toBeCloseTo(30)
    expect(y).toBeCloseTo(51.9615)
  })

  it('shifts an odd column south by half a hex height', () => {
    const first = hexToPixel(1, 1, 10)
    expect(first.x).toBeCloseTo(15)
    expect(first.y).toBeCloseTo(25.9808)

    const second = hexToPixel(3, 3, 10)
    expect(second.x).toBeCloseTo(45)
    expect(second.y).toBeCloseTo(60.6218)
  })

  it('advances columns by 1.5x the hex radius', () => {
    expect(hexToPixel(2, 5, 10).x).toBeCloseTo(75)
    expect(hexToPixel(4, 0, 10).x).toBe(0)
  })
})

describe('gridDimensions', () => {
  it('sizes an 8x8 board with room for every hex edge', () => {
    const dims = gridDimensions(8, 8, 10)
    expect(dims.width).toBeCloseTo(125)
    expect(dims.height).toBeCloseTo(147.2243)

    // No hex center, once shifted by the origin, should ever go negative.
    for (let row = 0; row < 8; row++) {
      for (let col = 0; col < 8; col++) {
        const { x, y } = hexToPixel(row, col, 10)
        expect(x + dims.originX).toBeGreaterThanOrEqual(0)
        expect(y + dims.originY).toBeGreaterThanOrEqual(0)
      }
    }
  })
})

describe('hexPolygonPoints', () => {
  it('returns six vertices with points at the left and right', () => {
    const points = hexPolygonPoints(0, 0, 10)
      .split(' ')
      .map((pair) => pair.split(',').map(Number))
    expect(points).toHaveLength(6)
    expect(points.some(([x, y]) => Math.abs(x! - 10) < 1e-9 && Math.abs(y!) < 1e-9)).toBe(true)
    expect(points.some(([x, y]) => Math.abs(x! + 10) < 1e-9 && Math.abs(y!) < 1e-9)).toBe(true)
  })
})

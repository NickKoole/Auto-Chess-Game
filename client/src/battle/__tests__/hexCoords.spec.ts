import { describe, expect, it } from 'vitest'
import { gridDimensions, hexPolygonPoints, hexToPixel } from '../hexCoords'

describe('hexToPixel', () => {
  it('places an even row with no horizontal offset', () => {
    expect(hexToPixel(0, 0, 10)).toEqual({ x: 0, y: 0 })

    const { x, y } = hexToPixel(0, 3, 10)
    expect(x).toBeCloseTo(51.9615) // 3 hexes across at sqrt(3) * 10 apart, no row shift
    expect(y).toBe(0)
  })

  it('shifts an odd row right by half a hex width', () => {
    const first = hexToPixel(1, 0, 10)
    expect(first.x).toBeCloseTo(8.6603) // half of sqrt(3) * 10
    expect(first.y).toBeCloseTo(15)

    const second = hexToPixel(3, 2, 10)
    expect(second.x).toBeCloseTo(43.3013) // 2 hexes across, plus the odd-row half-width shift
    expect(second.y).toBeCloseTo(45)
  })

  it('stacks rows 1.5x the hex size apart vertically, regardless of column', () => {
    expect(hexToPixel(2, 5, 10).y).toBeCloseTo(30)
    expect(hexToPixel(4, 0, 10).y).toBeCloseTo(60)
  })
})

describe('gridDimensions', () => {
  it('sizes an 8x8 board with room for every hex edge', () => {
    const dims = gridDimensions(8, 8, 10)
    expect(dims.width).toBeCloseTo(147.2243) // 8.5 hex-widths, for the widest odd-row shift
    expect(dims.height).toBeCloseTo(125) // 7 row-spacings plus a full hex height top and bottom

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
  it('returns six vertices with a point at the top, not a flat edge', () => {
    const points = hexPolygonPoints(0, 0, 10)
      .split(' ')
      .map((pair) => pair.split(',').map(Number))
    expect(points).toHaveLength(6)
    expect(points.some(([x, y]) => Math.abs(x!) < 1e-9 && y! < 0)).toBe(true)
  })
})

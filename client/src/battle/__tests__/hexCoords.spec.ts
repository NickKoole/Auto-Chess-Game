import { describe, expect, it } from 'vitest'
import { gridDimensions, hexPolygonPoints, hexToPixel, hexRowSpacing, hexWidth } from '../hexCoords'

describe('hexToPixel', () => {
  it('places an even row with no horizontal offset', () => {
    expect(hexToPixel(0, 0, 10)).toEqual({ x: 0, y: 0 })
    expect(hexToPixel(0, 3, 10)).toEqual({ x: 3 * hexWidth(10), y: 0 })
  })

  it('shifts an odd row right by half a hex width', () => {
    expect(hexToPixel(1, 0, 10)).toEqual({ x: hexWidth(10) / 2, y: hexRowSpacing(10) })
    expect(hexToPixel(3, 2, 10)).toEqual({ x: 2 * hexWidth(10) + hexWidth(10) / 2, y: 3 * hexRowSpacing(10) })
  })

  it('stacks rows 1.5x the hex size apart vertically, regardless of column', () => {
    expect(hexToPixel(2, 5, 10).y).toBeCloseTo(30)
    expect(hexToPixel(4, 0, 10).y).toBeCloseTo(60)
  })
})

describe('gridDimensions', () => {
  it('sizes an 8x8 board with room for every hex edge', () => {
    const dims = gridDimensions(8, 8, 10)
    expect(dims.width).toBeCloseTo(8 * hexWidth(10) + hexWidth(10) / 2)
    expect(dims.height).toBeCloseTo(7 * hexRowSpacing(10) + 20)

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

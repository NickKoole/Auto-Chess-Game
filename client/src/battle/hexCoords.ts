export interface PixelPosition {
  x: number
  y: number
}

export interface GridDimensions {
  /** Total SVG viewBox width needed to fit every hex without clipping. */
  width: number
  /** Total SVG viewBox height needed to fit every hex without clipping. */
  height: number
  /** Added to every hexToPixel() x so the leftmost hex isn't clipped. */
  originX: number
  /** Added to every hexToPixel() y so the topmost hex isn't clipped. */
  originY: number
}

/** Distance between adjacent flat-top hex centers in the same row. */
export function hexWidth(hexSize: number): number {
  return hexSize * 2
}

/** Distance between adjacent flat-top hex centers in the same column. */
export function hexRowSpacing(hexSize: number): number {
  return Math.sqrt(3) * hexSize
}

/** Mirrors GameSim's flat-top odd-column coordinates: odd columns shift south by half a row spacing. */
export function hexToPixel(row: number, col: number, hexSize: number): PixelPosition {
  const height = hexRowSpacing(hexSize)
  const columnShift = col % 2 !== 0 ? height / 2 : 0
  return {
    x: col * hexSize * 1.5,
    y: row * height + columnShift,
  }
}

export function gridDimensions(rows: number, cols: number, hexSize: number): GridDimensions {
  const height = hexRowSpacing(hexSize)
  const originX = hexSize
  const originY = height / 2
  return {
    width: hexSize * 1.5 * (cols - 1) + hexWidth(hexSize),
    height: height * (rows - 1) + (cols > 1 ? height / 2 : 0) + height,
    originX,
    originY,
  }
}

export function hexPolygonPoints(cx: number, cy: number, hexSize: number): string {
  const points: string[] = []
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i)
    const x = cx + hexSize * Math.cos(angle)
    const y = cy + hexSize * Math.sin(angle)
    points.push(`${x},${y}`)
  }
  return points.join(' ')
}

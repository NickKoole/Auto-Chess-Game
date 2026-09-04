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

/** Distance between adjacent hex centers in the same row - not one hex's own corner-to-corner width. */
export function hexWidth(hexSize: number): number {
  return Math.sqrt(3) * hexSize
}

export function hexRowSpacing(hexSize: number): number {
  return hexSize * 1.5
}

/** Odd rows are shifted right by half a hex width, so a row stays a clean horizontal band instead of a staircase - matches GameSim's HexCoord odd-r layout. */
export function hexToPixel(row: number, col: number, hexSize: number): PixelPosition {
  const width = hexWidth(hexSize)
  const rowShift = row % 2 !== 0 ? width / 2 : 0
  return {
    x: col * width + rowShift,
    y: row * hexRowSpacing(hexSize),
  }
}

export function gridDimensions(rows: number, cols: number, hexSize: number): GridDimensions {
  const width = hexWidth(hexSize)
  const originX = width / 2
  const originY = hexSize
  return {
    width: width * cols + originX,
    height: hexRowSpacing(hexSize) * (rows - 1) + hexSize * 2,
    originX,
    originY,
  }
}

export function hexPolygonPoints(cx: number, cy: number, hexSize: number): string {
  const points: string[] = []
  for (let i = 0; i < 6; i++) {
    const angle = (Math.PI / 180) * (60 * i - 30)
    const x = cx + hexSize * Math.cos(angle)
    const y = cy + hexSize * Math.sin(angle)
    points.push(`${x},${y}`)
  }
  return points.join(' ')
}

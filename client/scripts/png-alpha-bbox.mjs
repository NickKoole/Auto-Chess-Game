import { inflateSync } from 'node:zlib'

function paeth(a, b, c) {
  const p = a + b - c
  const pa = Math.abs(p - a)
  const pb = Math.abs(p - b)
  const pc = Math.abs(p - c)
  if (pa <= pb && pa <= pc) return a
  if (pb <= pc) return b
  return c
}

export function decodeRgbaPng(source) {
  if (!source.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) {
    throw new Error('Expected a PNG sprite.')
  }
  const width = source.readUInt32BE(16)
  const height = source.readUInt32BE(20)
  const bitDepth = source[24]
  const colorType = source[25]
  if (bitDepth !== 8 || colorType !== 6) {
    throw new Error('Expected an 8-bit RGBA PNG.')
  }
  const idat = []
  for (let offset = 8; offset < source.length;) {
    const length = source.readUInt32BE(offset)
    const type = source.toString('ascii', offset + 4, offset + 8)
    if (type === 'IDAT') {
      idat.push(source.subarray(offset + 8, offset + 8 + length))
    }
    offset += length + 12
  }
  const filtered = inflateSync(Buffer.concat(idat))
  const bytesPerPixel = 4
  const stride = width * bytesPerPixel
  const pixels = Buffer.alloc(height * stride)
  let filteredOffset = 0
  for (let y = 0; y < height; y++) {
    const filterType = filtered[filteredOffset]
    filteredOffset += 1
    const rowStart = y * stride
    const previousRowStart = (y - 1) * stride
    for (let x = 0; x < stride; x++) {
      const raw = filtered[filteredOffset + x]
      const a = x >= bytesPerPixel ? pixels[rowStart + x - bytesPerPixel] : 0
      const b = y > 0 ? pixels[previousRowStart + x] : 0
      const c = y > 0 && x >= bytesPerPixel ? pixels[previousRowStart + x - bytesPerPixel] : 0
      let value
      switch (filterType) {
        case 0: value = raw; break
        case 1: value = raw + a; break
        case 2: value = raw + b; break
        case 3: value = raw + Math.floor((a + b) / 2); break
        case 4: value = raw + paeth(a, b, c); break
        default: throw new Error(`Unsupported PNG filter type ${filterType}.`)
      }
      pixels[rowStart + x] = value & 0xff
    }
    filteredOffset += stride
  }
  return { width, height, pixels }
}

export function alphaBoundingBox({ width, height, pixels }, region, threshold = 8) {
  const x0 = region?.x ?? 0
  const y0 = region?.y ?? 0
  const x1 = region ? region.x + region.width : width
  const y1 = region ? region.y + region.height : height
  let minX = x1
  let minY = y1
  let maxX = x0 - 1
  let maxY = y0 - 1
  for (let y = y0; y < y1; y++) {
    for (let x = x0; x < x1; x++) {
      const alpha = pixels[(y * width + x) * 4 + 3]
      if (alpha >= threshold) {
        if (x < minX) minX = x
        if (x > maxX) maxX = x
        if (y < minY) minY = y
        if (y > maxY) maxY = y
      }
    }
  }
  return { x: minX, y: minY, width: maxX - minX + 1, height: maxY - minY + 1 }
}

import { constants, crc32, deflateSync, inflateSync } from 'node:zlib'

export const spriteAlphabet = Array.from({ length: 94 }, (_, index) => String.fromCharCode(index + 33))
  .filter((char) => !['"', "'", '\\', '<', '>', '\x60'].includes(char)).slice(0, 85).join('')

export function compressedPng(source) {
  if (!source.subarray(0, 8).equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))) {
    throw new Error('Expected a PNG sprite.')
  }
  const chunks = []
  for (let offset = 8; offset < source.length;) {
    const length = source.readUInt32BE(offset)
    const bytes = source.subarray(offset, offset + length + 12)
    if (bytes.length !== length + 12) throw new Error('Truncated PNG chunk.')
    chunks.push({ type: bytes.toString('ascii', 4, 8), bytes })
    offset += length + 12
  }
  const original = Buffer.concat(chunks.filter((chunk) => chunk.type === 'IDAT').map((chunk) => chunk.bytes.subarray(8, -4)))
  const filteredPixels = inflateSync(original)
  const candidates = [original, ...[constants.Z_DEFAULT_STRATEGY, constants.Z_FILTERED, constants.Z_RLE]
    .map((strategy) => deflateSync(filteredPixels, { level: 9, strategy }))]
  const compressed = candidates.reduce((smallest, candidate) => candidate.length < smallest.length ? candidate : smallest)
  const idat = Buffer.alloc(compressed.length + 12)
  idat.writeUInt32BE(compressed.length)
  idat.write('IDAT', 4)
  compressed.copy(idat, 8)
  idat.writeUInt32BE(crc32(idat.subarray(4, -4)), idat.length - 4)
  let replaced = false
  return Buffer.concat([source.subarray(0, 8), ...chunks.flatMap((chunk) => {
    if (chunk.type !== 'IDAT') return [chunk.bytes]
    if (replaced) return []
    replaced = true
    return [idat]
  })])
}

export function encodeSprite(bytes) {
  let encoded = ''
  for (let offset = 0; offset < bytes.length; offset += 4) {
    let value = 0
    for (let index = 0; index < 4; index++) value = value * 256 + (bytes[offset + index] ?? 0)
    let block = ''
    for (let index = 0; index < 5; index++) {
      block = spriteAlphabet[value % 85] + block
      value = Math.floor(value / 85)
    }
    encoded += block
  }
  return encoded
}

export function decodeSprite(encoded, length) {
  const bytes = new Uint8Array(length)
  for (let offset = 0; offset < encoded.length; offset += 5) {
    let value = 0
    for (let index = 0; index < 5; index++) value = value * 85 + spriteAlphabet.indexOf(encoded[offset + index])
    for (let index = 3; index >= 0; index--) {
      const destination = offset / 5 * 4 + index
      if (destination < length) bytes[destination] = value % 256
      value = Math.floor(value / 256)
    }
  }
  return bytes
}

export function packedSpriteModule(source) {
  const png = compressedPng(source)
  return [
    'const spriteAlphabet = ' + JSON.stringify(spriteAlphabet) + ';',
    decodeSprite.toString(),
    'const bytes = decodeSprite(' + JSON.stringify(encodeSprite(png)) + ', ' + png.length + ');',
    "export default 'data:image/png;base64,' + btoa(Array.from(bytes, byte => String.fromCharCode(byte)).join(''));",
  ].join('\n')
}

import { describe, expect, it } from 'vitest'
import { readFileSync } from 'node:fs'
import { execFileSync } from 'node:child_process'
import { crc32, inflateSync } from 'node:zlib'
import { compressedPng, decodeSprite, encodeSprite, spriteAlphabet } from '../../../scripts/pack-sprite.mjs'

function pngChunks(png: Buffer) {
  const chunks: { type: string; payload: Buffer; crc: number }[] = []
  for (let offset = 8; offset < png.length;) {
    const length = png.readUInt32BE(offset)
    const type = png.toString('ascii', offset + 4, offset + 8)
    const payload = png.subarray(offset + 8, offset + 8 + length)
    chunks.push({ type, payload, crc: png.readUInt32BE(offset + 8 + length) })
    offset += length + 12
  }
  return chunks
}

describe('standalone sprite packing', () => {
  it('round-trips every byte value and partial final blocks', () => {
    expect(new Set(spriteAlphabet).size).toBe(85)
    for (const length of [0, 1, 2, 3, 4, 255, 256, 257]) {
      const bytes = Buffer.from(Array.from({ length }, (_, index) => index % 256))
      expect(Buffer.from(decodeSprite(encodeSprite(bytes), bytes.length))).toEqual(bytes)
    }
  })

  it('preserves exact filtered pixel bytes and ancillary metadata with valid PNG checksums', () => {
    const source = readFileSync('src/assets/sprites/knight/knight.png')
    const compressed = compressedPng(source)
    const originalChunks = pngChunks(source)
    const packedChunks = pngChunks(compressed)
    const pixels = (chunks: ReturnType<typeof pngChunks>) =>
      inflateSync(Buffer.concat(chunks.filter((chunk) => chunk.type === 'IDAT').map((chunk) => chunk.payload)))
    expect(pixels(packedChunks).equals(pixels(originalChunks))).toBe(true)
    expect(packedChunks.filter((chunk) => chunk.type !== 'IDAT')).toEqual(originalChunks.filter((chunk) => chunk.type !== 'IDAT'))
    for (const chunk of packedChunks) {
      expect(crc32(Buffer.concat([Buffer.from(chunk.type), chunk.payload]))).toBe(chunk.crc)
    }
    expect(Buffer.from(decodeSprite(encodeSprite(compressed), compressed.length)).equals(compressed)).toBe(true)
    expect(compressed.length).toBeLessThanOrEqual(source.length)
  })

  it('rejects a non-PNG input', () => {
    expect(() => compressedPng(Buffer.from('not an image'))).toThrow('Expected a PNG sprite')
  })

  it('exports a standalone preview without external assets', () => {
    execFileSync(process.execPath, ['scripts/export-sprite-preview.mjs'], { cwd: process.cwd() })
    const html = readFileSync('dist/sprite-preview.html', 'utf8')

    expect(html).toContain('<script type="module">')
    expect(html).not.toMatch(/<script[^>]+src=/i)
    expect(html).not.toMatch(/<link[^>]+href=/i)
    expect(html).not.toMatch(/(?:src|href)=["'](?:\.\/|https?:\/\/)/i)
  })
})

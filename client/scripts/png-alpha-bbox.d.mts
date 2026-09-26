import type { Buffer } from 'node:buffer'

export interface DecodedPng {
  width: number
  height: number
  pixels: Buffer
}

export interface PixelRegion {
  x: number
  y: number
  width: number
  height: number
}

export function decodeRgbaPng(source: Buffer): DecodedPng
export function alphaBoundingBox(decoded: DecodedPng, region?: PixelRegion, threshold?: number): PixelRegion

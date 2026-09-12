import type { Buffer } from 'node:buffer'

export const spriteAlphabet: string
export function compressedPng(source: Buffer): Buffer
export function encodeSprite(bytes: Uint8Array): string
export function decodeSprite(encoded: string, length: number): Uint8Array
export function packedSpriteModule(source: Buffer): string

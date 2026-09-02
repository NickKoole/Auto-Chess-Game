/** DTO int32 fields are typed `number | string`; the server always sends plain numbers. */
export function toNumber(value: number | string): number {
  return typeof value === 'number' ? value : Number(value)
}

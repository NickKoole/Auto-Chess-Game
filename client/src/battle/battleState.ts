import type { components } from '@/api/schema.d.ts'
import { toNumber } from '@/api/numeric'

type SimulateBattleResponse = components['schemas']['SimulateBattleResponse']
type UnitDefinitionDto = components['schemas']['UnitDefinitionDto']

/** Mirrors Api/Battles/Dtos/SideDto.cs - Player = 0, Opponent = 1 on the wire. */
export const SIDE_PLAYER = 0
export const SIDE_OPPONENT = 1

export interface HexPosition {
  row: number
  col: number
}

export interface BattleUnitState {
  id: number
  side: number
  definitionId: string
  position: HexPosition
  hp: number
  maxHp: number
}

/** Rendering reads only this, never the raw API response - so replaying events onto it is a mutation, not a rebuild. */
export interface BattleState {
  units: BattleUnitState[]
  unitDefinitions: Record<string, UnitDefinitionDto>
}

export function createBattleState(response: SimulateBattleResponse): BattleState {
  const unitDefinitions: Record<string, UnitDefinitionDto> = {}
  for (const definition of response.unitDefinitions) {
    unitDefinitions[definition.id] = definition
  }

  const units = response.initialSetup.map((unit): BattleUnitState => {
    const definition = unitDefinitions[unit.definitionId]
    if (!definition) {
      throw new Error(`No unit definition for '${unit.definitionId}'.`)
    }
    const maxHp = toNumber(definition.maxHp)
    return {
      id: toNumber(unit.unitId),
      side: toNumber(unit.side),
      definitionId: unit.definitionId,
      position: { row: toNumber(unit.position.row), col: toNumber(unit.position.col) },
      hp: maxHp,
      maxHp,
    }
  })

  return { units, unitDefinitions }
}

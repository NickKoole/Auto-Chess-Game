import type { components } from '@/api/schema.d.ts'
import { toNumber } from '@/api/numeric'
import { SIDE_OPPONENT, SIDE_PLAYER, type BattleState, type BattleUnitState, type HexPosition } from './battleState'

type UnitDefinitionDto = components['schemas']['UnitDefinitionDto']

/** Mirrors Battlefield.DeploymentBand(Player) - rows 5-7 of the 8x8 board. */
export const PLAYER_BAND_FIRST_ROW = 5
export const PLAYER_BAND_LAST_ROW = 7

export function isInPlayerBand(row: number): boolean {
  return row >= PLAYER_BAND_FIRST_ROW && row <= PLAYER_BAND_LAST_ROW
}

export interface CompositionPlacement {
  unitId: string
  position: HexPosition
}

export type PlaceUnitResult =
  | { ok: true; placements: CompositionPlacement[] }
  | { ok: false; reason: string }

/** Replaces whatever already occupies the tile, if anything - dropping a new unit there is how a placement is rearranged. */
export function placeUnit(
  existing: readonly CompositionPlacement[],
  unitId: string,
  position: HexPosition,
): PlaceUnitResult {
  if (!isInPlayerBand(position.row)) {
    return {
      ok: false,
      reason: `Row ${position.row} is outside your deployment band (rows ${PLAYER_BAND_FIRST_ROW}-${PLAYER_BAND_LAST_ROW}).`,
    }
  }

  return {
    ok: true,
    placements: [
      ...existing.filter((p) => !(p.position.row === position.row && p.position.col === position.col)),
      { unitId, position },
    ],
  }
}

export function removePlacementAt(
  existing: readonly CompositionPlacement[],
  position: HexPosition,
): CompositionPlacement[] {
  return existing.filter((p) => !(p.position.row === position.row && p.position.col === position.col))
}

export function canResetArmy(placementCount: number, battleState: BattleState | null, hasFinished: boolean): boolean {
  return placementCount > 0 && (battleState === null || hasFinished)
}

export function toggleRosterSelection(current: string | null, tapped: string): string | null {
  return current === tapped ? null : tapped
}

export interface EnemyPreviewPlacement {
  definitionId: string
  position: HexPosition
}

/** Renders a composition through the same board `BattleBoard.vue` already knows how to draw - full HP, no target, player side. */
export function compositionToBattleState(
  placements: readonly CompositionPlacement[],
  unitDefinitions: Record<string, UnitDefinitionDto>,
  enemyPreview: readonly EnemyPreviewPlacement[] = [],
): BattleState {
  const playerUnits = placements.map((placement, index): BattleUnitState => {
    const maxHp = toNumber(unitDefinitions[placement.unitId]?.maxHp ?? 0)
    return {
      id: index,
      side: SIDE_PLAYER,
      definitionId: placement.unitId,
      position: placement.position,
      hp: maxHp,
      maxHp,
      currentTargetId: null,
    }
  })

  const enemyUnits = enemyPreview.map((placement, index): BattleUnitState => {
    const maxHp = toNumber(unitDefinitions[placement.definitionId]?.maxHp ?? 0)
    return {
      // Negative so a preview unit's id never collides with a player placement's index-based id.
      id: -(index + 1),
      side: SIDE_OPPONENT,
      definitionId: placement.definitionId,
      position: placement.position,
      hp: maxHp,
      maxHp,
      currentTargetId: null,
      isPreview: true,
    }
  })

  return { units: [...playerUnits, ...enemyUnits], unitDefinitions }
}

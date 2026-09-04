import { SIDE_OPPONENT, SIDE_PLAYER, type BattleState, type BattleUnitState } from './battleState'

export interface SideSurvivors {
  side: number
  units: BattleUnitState[]
}

/**
 * Never derived by counting or comparing HP - that would be a client-side
 * re-implementation of a rule the server already answered. Pass the state
 * at the battle's terminating tick (`seekTo`'s result), not a mid-battle one.
 */
export function survivorsBySide(finalState: BattleState): SideSurvivors[] {
  const bySide = (side: number): BattleUnitState[] =>
    finalState.units.filter((unit) => unit.side === side).sort((a, b) => a.id - b.id)

  return [
    { side: SIDE_PLAYER, units: bySide(SIDE_PLAYER) },
    { side: SIDE_OPPONENT, units: bySide(SIDE_OPPONENT) },
  ]
}

/** Mirrors Api.Battles.Dtos.BattleEndingDto - the exact reason a battle ended. */
export const ENDING_PLAYER_ELIMINATED_OPPONENT = 0
export const ENDING_OPPONENT_ELIMINATED_PLAYER = 1
export const ENDING_MUTUAL_ELIMINATION = 2
export const ENDING_TICK_CAP_DRAW = 3

const ENDING_LABELS: Record<number, string> = {
  [ENDING_PLAYER_ELIMINATED_OPPONENT]: 'Player wins — opponent eliminated',
  [ENDING_OPPONENT_ELIMINATED_PLAYER]: 'Opponent wins — player eliminated',
  [ENDING_MUTUAL_ELIMINATION]: 'Draw — mutual elimination',
  [ENDING_TICK_CAP_DRAW]: 'Draw — tick cap reached',
}

/** The two draw endings get distinct text - a mutual kill and a stalemate at the tick cap read differently, not the same event wearing one label. */
export function endingLabel(terminationReason: number): string {
  return ENDING_LABELS[terminationReason] ?? `Unknown ending (${terminationReason})`
}

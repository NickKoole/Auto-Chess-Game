import type { components } from './schema.d.ts'

export type SimulateBattleRequest = components['schemas']['SimulateBattleRequest']
export type SimulateBattleResponse = components['schemas']['SimulateBattleResponse']
export type MatchupDto = components['schemas']['MatchupDto']
export type UnitDefinitionDto = components['schemas']['UnitDefinitionDto']
export type EnemySetupDto = components['schemas']['EnemySetupDto']

export type SimulateBattleResult =
  | { ok: true; data: SimulateBattleResponse }
  | { ok: false; error: string }

export type ListMatchupsResult =
  | { ok: true; data: MatchupDto[] }
  | { ok: false; error: string }

export type ListUnitsResult =
  | { ok: true; data: UnitDefinitionDto[] }
  | { ok: false; error: string }

export type ListUnitDefinitionsResult =
  | { ok: true; data: UnitDefinitionDto[] }
  | { ok: false; error: string }

export type ListEnemySetupsResult =
  | { ok: true; data: EnemySetupDto[] }
  | { ok: false; error: string }

export async function simulateBattle(request: SimulateBattleRequest): Promise<SimulateBattleResult> {
  let response: Response
  try {
    response = await fetch('/api/battles/simulate', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(request),
    })
  } catch {
    return { ok: false, error: 'Could not reach the server.' }
  }

  if (!response.ok) {
    return { ok: false, error: await describeError(response) }
  }

  return { ok: true, data: (await response.json()) as SimulateBattleResponse }
}

export async function listMatchups(): Promise<ListMatchupsResult> {
  let response: Response
  try {
    response = await fetch('/api/battles/matchups')
  } catch {
    return { ok: false, error: 'Could not reach the server.' }
  }

  if (!response.ok) {
    return { ok: false, error: await describeError(response) }
  }

  return { ok: true, data: (await response.json()) as MatchupDto[] }
}

export async function listUnits(): Promise<ListUnitsResult> {
  let response: Response
  try {
    response = await fetch('/api/battles/units')
  } catch {
    return { ok: false, error: 'Could not reach the server.' }
  }

  if (!response.ok) {
    return { ok: false, error: await describeError(response) }
  }

  return { ok: true, data: (await response.json()) as UnitDefinitionDto[] }
}

export async function listUnitDefinitions(): Promise<ListUnitDefinitionsResult> {
  let response: Response
  try {
    response = await fetch('/api/battles/unit-definitions')
  } catch {
    return { ok: false, error: 'Could not reach the server.' }
  }

  if (!response.ok) {
    return { ok: false, error: await describeError(response) }
  }

  return { ok: true, data: (await response.json()) as UnitDefinitionDto[] }
}

export async function listEnemySetups(): Promise<ListEnemySetupsResult> {
  let response: Response
  try {
    response = await fetch('/api/battles/enemy-setups')
  } catch {
    return { ok: false, error: 'Could not reach the server.' }
  }

  if (!response.ok) {
    return { ok: false, error: await describeError(response) }
  }

  return { ok: true, data: (await response.json()) as EnemySetupDto[] }
}

async function describeError(response: Response): Promise<string> {
  try {
    const body = (await response.json()) as components['schemas']['ErrorResponse']
    if (body.message) {
      return body.message
    }
  } catch {
    // Body wasn't the JSON error shape the API contracts to send — fall through.
  }
  return `Request failed with status ${response.status}.`
}

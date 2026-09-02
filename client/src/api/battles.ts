import type { components } from './schema.d.ts'

export type SimulateBattleRequest = components['schemas']['SimulateBattleRequest']
export type SimulateBattleResponse = components['schemas']['SimulateBattleResponse']

export type SimulateBattleResult =
  | { ok: true; data: SimulateBattleResponse }
  | { ok: false; error: string }

/** Runs one hardcoded matchup on the server and returns its outcome. */
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

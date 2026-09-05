import { afterEach, describe, expect, it, vi } from 'vitest'
import {
  listEnemySetups,
  listMatchups,
  listUnits,
  simulateBattle,
  type EnemySetupDto,
  type MatchupDto,
  type SimulateBattleResponse,
  type UnitDefinitionDto,
} from '../battles'

function jsonResponse(body: unknown, init?: ResponseInit): Response {
  return new Response(JSON.stringify(body), {
    headers: { 'Content-Type': 'application/json' },
    ...init,
  })
}

describe('simulateBattle', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('posts the request and returns the parsed response on success', async () => {
    const responseBody: SimulateBattleResponse = {
      seed: '123',
      initialSetup: [],
      unitDefinitions: [],
      events: [],
      outcome: { winner: 1, terminatingTick: 42, terminationReason: 0 },
    }
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(responseBody))
    vi.stubGlobal('fetch', fetchMock)

    const result = await simulateBattle({ matchupId: 'melee-mirror' })

    expect(fetchMock).toHaveBeenCalledWith(
      '/api/battles/simulate',
      expect.objectContaining({
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ matchupId: 'melee-mirror' }),
      }),
    )
    expect(result).toEqual({ ok: true, data: responseBody })
  })

  it('returns the server-provided message when the API responds with an error', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse({ message: "Unknown matchup id 'nope'." }, { status: 400 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await simulateBattle({ matchupId: 'nope' })

    expect(result).toEqual({ ok: false, error: "Unknown matchup id 'nope'." })
  })

  it('falls back to a status-based message when the error body is not JSON', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(new Response('oops', { status: 500 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await simulateBattle({ matchupId: 'melee-mirror' })

    expect(result).toEqual({ ok: false, error: 'Request failed with status 500.' })
  })

  it('returns an error when the network request itself fails', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await simulateBattle({ matchupId: 'melee-mirror' })

    expect(result).toEqual({ ok: false, error: 'Could not reach the server.' })
  })
})

describe('listMatchups', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches the matchup list and returns it parsed', async () => {
    const responseBody: MatchupDto[] = [
      { id: 'melee-mirror', displayName: 'Melee Mirror' },
      { id: 'archers-vs-melee', displayName: 'Archers vs Melee' },
    ]
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(responseBody))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listMatchups()

    expect(fetchMock).toHaveBeenCalledWith('/api/battles/matchups')
    expect(result).toEqual({ ok: true, data: responseBody })
  })

  it('returns the server-provided message when the API responds with an error', async () => {
    const fetchMock = vi
      .fn<typeof fetch>()
      .mockResolvedValue(jsonResponse({ message: 'Something went wrong.' }, { status: 500 }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listMatchups()

    expect(result).toEqual({ ok: false, error: 'Something went wrong.' })
  })

  it('returns an error when the network request itself fails', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listMatchups()

    expect(result).toEqual({ ok: false, error: 'Could not reach the server.' })
  })
})

describe('listUnits', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches the unit roster and returns it parsed', async () => {
    const responseBody: UnitDefinitionDto[] = [
      {
        id: 'knight',
        displayName: 'Knight',
        maxHp: 180,
        attackDamage: 16,
        attackIntervalTicks: 16,
        movementTicksPerHex: 8,
        attackRangeHexes: 1,
        damageVariance: 0,
      },
    ]
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(responseBody))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listUnits()

    expect(fetchMock).toHaveBeenCalledWith('/api/battles/units')
    expect(result).toEqual({ ok: true, data: responseBody })
  })

  it('returns an error when the network request itself fails', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listUnits()

    expect(result).toEqual({ ok: false, error: 'Could not reach the server.' })
  })
})

describe('listEnemySetups', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('fetches the enemy setup list and returns it parsed', async () => {
    const responseBody: EnemySetupDto[] = [
      { id: 'elite-line', displayName: 'Elite Line' },
      { id: 'mixed-line', displayName: 'Mixed Line' },
      { id: 'swarm', displayName: 'Swarm' },
    ]
    const fetchMock = vi.fn<typeof fetch>().mockResolvedValue(jsonResponse(responseBody))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listEnemySetups()

    expect(fetchMock).toHaveBeenCalledWith('/api/battles/enemy-setups')
    expect(result).toEqual({ ok: true, data: responseBody })
  })

  it('returns an error when the network request itself fails', async () => {
    const fetchMock = vi.fn<typeof fetch>().mockRejectedValue(new TypeError('Failed to fetch'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await listEnemySetups()

    expect(result).toEqual({ ok: false, error: 'Could not reach the server.' })
  })
})

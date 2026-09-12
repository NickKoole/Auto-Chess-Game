# Auto Chess — repo conventions

Read this before touching code. It binds human and agent contributors equally.

## Stack

- **Server**: .NET 10 (LTS) / C#, ASP.NET Core minimal API, EF Core + SQLite, xUnit.
  Target `net10.0`. .NET 8 and 9 both reach end of support on 10 Nov 2026, so
  neither is a sane floor for a project starting now. CI pins the same SDK as
  your machine — local and CI must match, because a determinism bug that shows
  up on only one runtime is the worst kind to chase.
- **EF tooling** is pinned per-repo via `.config/dotnet-tools.json`
  (`dotnet tool restore`), not a global install, so the agent and CI can never
  drift onto a different `dotnet-ef` than yours.
- **Client**: Vue 3 (`<script setup lang="ts">`), TypeScript, Vite, Vitest,
  Vue Router, Pinia. **Requires Node >= 22.18 or >= 24.12** — the toolchain
  declares it in `engines`, and CI runs Node 24.
- **The game is server-authoritative.** The client is a view.

## Architecture

```
server/
  AutoChess.slnx
  Directory.Build.props        shared compiler settings, enforced not advised
  .config/dotnet-tools.json    dotnet-ef pinned per-repo, not installed globally
  src/GameSim/                 headless, deterministic, zero I/O  <-- the real game
  src/Api/                     ASP.NET Core + EF Core (SQLite)
    openapi/Api.json           the contract, emitted at build, committed
    Persistence/Migrations/
  tests/GameSim.Tests/         includes ArchitectureTests, which enforce this file
  tests/Api.Tests/
client/
  src/api/schema.d.ts          generated from Api.json, committed
  src/{components,views,router,stores}/
```

Project reference direction is **`Api → GameSim`, never the reverse.** `GameSim`
references nothing but the BCL: no ASP.NET, no EF Core, no file or network I/O,
no logging framework. If a change to `GameSim` needs any of those, the design is
wrong — the simulation has to run in a test, in a background job, and in an
offline-progress catch-up pass with no host around it.

**The client never simulates outcomes.** It sends intents (buy, place, sell,
reroll) and renders the battle log the server returns. Any combat maths in
`client/` is animation timing, not truth.

## Determinism in `GameSim` — the rules that matter

Same seed and same inputs must produce a byte-identical battle log, on any
machine, on any runtime. C# makes this easy to break by accident:

- **Randomness**: thread a seeded PRNG explicitly through the sim. Never
  `new Random()`, never `Random.Shared`, never `Guid.NewGuid()`.
- **Time**: no `DateTime.Now`, `DateTime.UtcNow`, `Stopwatch`, or
  `Environment.TickCount` inside the sim. Time enters only as an integer tick
  count, supplied by the caller.
- **Collection ordering**: never let the iteration order of a `Dictionary<,>`
  or `HashSet<>` affect an outcome. .NET guarantees no order and it has changed
  between runtimes. Use `List<>`, or sort with an explicit total order (a tie
  break on unit id, not just on a stat).
- **Floating point**: prefer integer or fixed-point maths for anything that
  affects an outcome. Never accumulate state in `float`/`double`, and avoid
  `Math` transcendentals in the sim — they are not guaranteed identical across
  platforms.
- **Sorting**: LINQ `OrderBy` is stable, so it preserves source order for equal
  keys — which means the source order must itself be deterministic. Sorting
  does not rescue you from the ordering rule above.
- **No parallelism**: no `Parallel.*`, no PLINQ, no `async` racing inside the
  sim. It is a pure function of (state, inputs, seed).

**Time is authoritative and server-side.** Offline progress is computed from the
server's own clock as "run N ticks", never from a client-supplied timestamp or a
wall-clock delta. This is the whole reason the backend exists.

## State and persistence

- Save state is plain serializable data: no behaviour, no polymorphic graphs.
- EF Core owns the schema. Every schema change ships with a migration.
- SQLite runs in-process, so integration tests need no external service — keep
  it that way. Nothing in the pipeline can start a database for you — the review
  agent runs unattended, and CI has no service container.

## Testing

- Every `GameSim` change ships with xUnit tests. Non-negotiable: it is the only
  layer fully verifiable without a human looking at pixels.
- A test must fail if the logic is wrong. Asserting whatever the implementation
  currently returns proves nothing.
- Determinism gets its own test: run a battle twice from one seed and assert the
  logs are identical.
- Balance changes test the *property* that matters ("a tier-3 unit beats two
  tier-1 units of the same trait in at least 80% of seeds"), not exact damage.
- `Api` changes: integration tests against SQLite, covering the failure paths
  and not just the happy one.
- `client/` changes: test the logic you extracted out of the component with
  Vitest. Don't chase coverage on rendered output.

## Conventions

**Comments**
- Default is no comments. Code should read from names and structure alone;
  don't add a comment to explain what a line already says.
- Write a comment only when a reader genuinely can't get the fact from the
  code itself — e.g. a workaround for an external bug, a non-obvious ordering
  constraint, a regex, a value picked to satisfy an external system. Never a
  comment restating the code, and never to justify a design choice. Default
  to one `//` (or `///` for C#) line at the statement; go past one line only
  when the fact genuinely doesn't fit in one — that's an exception for a
  single fact that needs the room, not a general allowance to spread out, and
  never a second paragraph.
- Never reference a ticket in code — no issue numbers in comments, test
  names, or identifiers. The ID belongs in the commit message, the branch and
  the PR; in a file it's a dead reference for the next reader.
- If you're about to write a comment, ask: would a better variable/function
  name make this comment unnecessary? If yes, rename instead of commenting.

**C#**
- File-scoped namespaces, nullable reference types enabled, `var` only when the
  type is obvious from the right-hand side.
- `record` for sim state and DTOs; `class` only when identity or mutation is
  genuinely needed.
- Errors inside the sim are programmer errors: throw. Errors at the API boundary
  are results: return a typed failure, don't throw across the wire.
- One public type per file, named after the file.

**TypeScript / Vue**
- SFCs with `<script setup lang="ts">` and the Composition API. Props and emits
  are typed — no bare `defineProps()` without a type argument.
- Named exports from `.ts` modules. (`.vue` files are exempt: a single-file
  component is a default export by design.)
- No barrel `index.ts` files re-exporting a whole directory — they wreck
  tree-shaking and hide import cycles.
- Server DTOs are not hand-copied into the client. Generate them from the API's
  OpenAPI document so drift is a build error rather than a runtime surprise.
- Discriminated unions over optional-field soup.

## Commands

```bash
# server
cd server && dotnet build && dotnet test
dotnet ef migrations add <Name> --project src/Api/Api.csproj --output-dir Persistence/Migrations

# client
cd client && npm run typecheck && npm run test -- --run && npm run lint

# after ANY change to an API endpoint or DTO
cd server && dotnet build src/Api/Api.csproj   # re-emits openapi/Api.json
cd client && npm run generate:api              # regenerates schema.d.ts
```

Both generated files are committed. CI rebuilds them and fails on a diff, so a
stale contract is caught in review rather than at runtime.

## Commits and branches

- Branch: `agent/<issue>-<slug>` for agent work, `nick/<slug>` for hand work.
- Commit subject: `<type>(<area>): <what changed>` — e.g. `feat(sim): add trait stacking`.
- Every PR body ends with `Closes #<issue>`.

## If you are an agent reading this during a ticket run

Your scope is the issue's acceptance criteria and nothing else. Don't refactor
adjacent code, don't add NuGet or npm packages, and don't touch `.github/` or
`automation/` unless the ticket carries `area:infra`.

Full pipeline: [docs/PIPELINE.md](docs/PIPELINE.md).

## Working with Nick

- Use a ticket with concrete acceptance criteria for nontrivial work unless Nick explicitly authorizes an exception.
- Only the human chooses `state:ready` and `state:approved`. Never promote either autonomously.
- For UI work, present mockup options and get an explicit pick before implementation. Nick reviews visuals in the running dev server; do not use browser tools to self-verify visual changes.
- Read `docs/PIPELINE.md` for ticket work. In an existing Codex conversation, use the claim/finish path; do not open another interactive CLI inside this session.
- Claude's private design artifacts are historical references; exported files are needed to reuse them here.

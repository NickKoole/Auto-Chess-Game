# Local Codex pipeline

Read [the pipeline guide](../docs/PIPELINE.md) for commands, permissions, review behavior, session migration, and recovery.

```powershell
node automation/dev.mjs 42
node automation/review.mjs 42
node automation/approve.mjs 42 --dry-run
node automation/ship.mjs 42 --dry-run
node --test automation/tests/*.test.mjs
```

| Path | Role |
| --- | --- |
| `claim.mjs` | Prepare a ticket worktree, task, and push guard. |
| `session.mjs` | Initialize or resume the ticket's recorded Codex session. |
| `dev.mjs` | Claim, interactive session, then publish on successful exit. |
| `finish.mjs` | Commit sweep, push, draft PR, and independent review. |
| `review.mjs` | Validate the published worktree and run the read-only reviewer. |
| `approve.mjs`, `ship.mjs` | Separate human approval and shipping actions. |
| `lib/codex.mjs` | Shell-free Codex launching and JSONL event parsing. |
| `lib/validation.mjs`, `lib/verdict.mjs` | Independent checks and strict verdict validation. |
| `settings/codex.json` | Shared model and reasoning effort. |
| `settings/review.schema.json` | Structured reviewer output contract. |
| `prompts/` | Standing rules and per-ticket task templates. |

No game dependencies are needed for the automation tests; they use Node's built-in test runner and temporary fixtures.

# Local agents

Two scripts. You start the first; it starts the second.

```bash
node automation/dev.mjs 42     # build ticket 42, then review it automatically
node automation/review.mjs 42  # re-review by hand
```

Full workflow: [../docs/PIPELINE.md](../docs/PIPELINE.md).

| Path | Role |
| --- | --- |
| `dev.mjs` | Claims a ticket → worktree → headless `claude -p` → commit → push → draft PR → spawns the reviewer. |
| `review.mjs` | Fresh read-only audit of the PR diff → severity-ranked findings → one PR comment. |
| `lib/core.mjs` | gh CLI, git worktrees, headless Claude spawn, session store. |
| `prompts/dev-system.md` | Standing rules for the dev agent, including how to behave when nobody is watching. |
| `prompts/review-system.md` | Standing rules for the reviewer, including what counts as a real finding. |
| `prompts/dev.md`, `review.md` | Per-run task templates. `{{VAR}}` placeholders. |
| `settings/dev.json` | Dev permissions. **Denies `git push`, `git remote`, `gh`, branch switching.** |
| `settings/review.json` | Reviewer permissions. **Denies `Write`, `Edit`, `gh`.** |

## The two structural guarantees

**The dev agent cannot publish.** Only `dev.mjs` pushes, and only after the run
exits cleanly. An unattended agent that goes off the rails has no route to the
remote, and everything lands on a draft PR on its own branch.

**The reviewer cannot edit or approve.** It starts in a fresh session with no
memory of writing the code, and it's read-only. Both properties are enforced by
the permission layer rather than by asking politely, because an approval only
means something if the reviewer couldn't have quietly fixed things first.

## Troubleshooting

**`gh is not authenticated`** — `gh auth login`.

**"produced no changes"** — the acceptance criteria weren't concrete enough to
act on. The issue comment quotes what the agent reported. Sharpen the ticket,
then re-run with `--fresh`.

**"no machine-readable verdict"** — the reviewer didn't end with the JSON fence.
Its raw notes are posted anyway, so nothing is lost. Re-run `review.mjs 42`.

**Agent stuck on a bad approach** — `node automation/dev.mjs 42 --fresh` wipes
its memory but keeps the branch, so it re-reads the code with fresh eyes.

**A run died halfway** — the ticket is left in `state:building` or
`state:blocked`. Nothing is watching, so nothing will retry it; just re-run
`dev.mjs 42` when you're ready.

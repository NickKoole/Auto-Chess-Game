# Local agents

Two scripts. You start the first; it starts the second.

```bash
node automation/dev.mjs 42     # build ticket 42 with Claude, then review it automatically
node automation/review.mjs 42  # re-review by hand
```

`dev.mjs` is a session, not a job: it sets up the worktree, opens Claude in your
terminal, and waits. You steer the build and answer its questions. When you exit,
it commits, pushes, opens the draft PR, and hands off to the reviewer — which
*is* headless, and deliberately so.

Full workflow: [../docs/PIPELINE.md](../docs/PIPELINE.md).

| Path | Role |
| --- | --- |
| `dev.mjs` | Claims a ticket → worktree → interactive `claude` → commit sweep → push → draft PR → spawns the reviewer. |
| `review.mjs` | Fresh read-only audit of the PR diff → severity-ranked findings → one PR comment. |
| `lib/core.mjs` | gh CLI, git worktrees, both Claude spawns, session store. |
| `prompts/dev-system.md` | Standing rules for the dev agent: scope, when to ask, plan-first method, the report. |
| `prompts/review-system.md` | Standing rules for the reviewer, including what counts as a real finding. |
| `prompts/dev.md`, `review.md` | Per-run task templates. `{{VAR}}` placeholders. |
| `settings/dev.json` | Dev permissions. **Denies `git push`, `git remote`, `gh`, branch switching, `rm`, `git clean`.** |
| `settings/review.json` | Reviewer permissions. **Denies `Write`, `Edit`, `gh`.** |

## The two structural guarantees

**The dev agent cannot publish.** Only `dev.mjs` pushes, and only after you exit
the session. You being in the room is not what makes this safe — the permission
layer is — so an agent that goes off the rails still has no route to the remote,
and everything lands on a draft PR on its own branch.

**The reviewer cannot edit or approve.** It starts in a fresh session with no
memory of writing the code, and it's read-only. Both properties are enforced by
the permission layer rather than by asking politely, because an approval only
means something if the reviewer couldn't have quietly fixed things first.

## What the permission files do and don't do

The allow list *is* the unprompted surface: anything missing from it stops and
asks you. That's why `node`, `npx` and `npm install` are absent rather than
denied — you can still approve one when a ticket genuinely calls for it, and
`npm install` prompting is what makes "no new dependencies" a rule the tooling
enforces rather than one the prompt merely requests.

Deletion is denied outright rather than by flag pattern. `Bash(rm -rf:*)` matches
a command prefix, so `rm -fr`, `rm -r -f` and a plain `rm` all slipped past it;
`Bash(rm:*)` doesn't. `git rm` is deliberately in neither list, so a legitimate
file removal prompts you.

None of this contains a *determined* agent, and it can't: anything that may write
files and run `dotnet test` or `npm run` has arbitrary code execution, since it
controls what those commands execute. These rules stop accidents. The real
backstop is that the work is pushed — recovery beats prevention here.

## How the session talks to the runner

Inside the worktree, gitignored:

- `.agent/task.md` — the ticket, plus every comment since the last session. The
  runner writes it; Claude is pointed at it on startup.
- `.agent/report.md` — Claude writes it before finishing. The runner turns it
  into the PR body. No report means a placeholder PR body and a warning.

## Troubleshooting

**`gh is not authenticated`** — `gh auth login`.

**"ended with no changes"** — you exited before anything was committed. Nothing
is pushed and nothing is lost; the branch and the worktree are still there.

**"no `.agent/report.md` was written"** — the session ended before Claude wrote
its report. The PR gets a placeholder body; re-run `dev.mjs 42` and ask it to
write the report, or edit the PR body yourself.

**"no machine-readable verdict"** — the reviewer didn't end with the JSON fence.
Its raw notes are posted anyway, so nothing is lost. Re-run `review.mjs 42`.

**Agent stuck on a bad approach** — `node automation/dev.mjs 42 --fresh` wipes
its memory but keeps the branch, so it re-reads the code with fresh eyes.

**A session died halfway** — the ticket is left in `state:building` or
`state:blocked`. Nothing is watching, so nothing will retry it; just re-run
`dev.mjs 42` when you're ready.

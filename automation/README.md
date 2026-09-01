# Local agents

```bash
node automation/dev.mjs 42     # build ticket 42 with Claude, then review it automatically
node automation/review.mjs 42  # re-review by hand
node automation/approve.mjs 42 # set state:approved from here, after reading the findings
node automation/ship.mjs 42    # merge it once you have approved it, then clean up
```

`dev.mjs` is a session, not a job: it sets up the worktree, opens Claude in your
terminal, and waits. You steer the build and answer its questions. When you exit,
it commits, pushes, opens the draft PR, and hands off to the reviewer — which
*is* headless, and deliberately so.

## Running the session somewhere else

`dev.mjs` holds no logic of its own. It is the one-terminal path through two
halves you can also run yourself:

```bash
node automation/claim.mjs 42   # worktree, branch, task file, push guard
#   ...run an agent in the worktree, however you like...
node automation/finish.mjs 42  # sweep → push → draft PR → reviewer
```

Use those directly when something else owns the session: an Orca terminal, a
second machine, or several tickets at once. `dev.mjs` is a single blocking
terminal by construction, so it cannot run two tickets in parallel; the split
can. What it costs you is session resume — `--fresh` and `--resume` need the
Claude session id, which only the attached path gets back.

`claim.mjs` prints the exact `claude` line to run, built from the same argv
`dev.mjs` uses — permission mode, the settings file, `dev-system.md`, the
session id, and the pointer at `.agent/task.md`. Paste it rather than
reconstructing it: an agent launched without `--append-system-prompt-file` has
none of its standing rules about scope, asking, or writing the report.

Full workflow: [../docs/PIPELINE.md](../docs/PIPELINE.md).

| Path | Role |
| --- | --- |
| `dev.mjs` | `claim` → interactive `claude` → `finish`. One terminal, one ticket. |
| `claim.mjs` | Ticket → worktree → branch → `.agent/task.md` → push guard. Stops before the agent. |
| `finish.mjs` | Commit sweep → push → draft PR → spawns the reviewer. The only thing that may publish. |
| `review.mjs` | Fresh read-only audit of the PR diff → severity-ranked findings → one PR comment. |
| `approve.mjs` | Prints the PR and the reviewer's findings, then sets `state:approved`. Only ever runs when you type it. |
| `ship.mjs` | Push what is left → ready → wait for CI → squash-merge → remove the worktree. Refuses without `state:approved`. |
| `lib/core.mjs` | gh CLI, git worktrees, both Claude spawns, session store, push guard. |
| `prompts/dev-system.md` | Standing rules for the dev agent: scope, when to ask, plan-first method, the report. |
| `prompts/review-system.md` | Standing rules for the reviewer, including what counts as a real finding. |
| `prompts/dev.md`, `review.md` | Per-run task templates. `{{VAR}}` placeholders. |
| `settings/dev.json` | Dev permissions. **Denies `git push`, `git remote`, `gh`, branch switching, `rm`, `git clean`.** |
| `settings/review.json` | Reviewer permissions. **Denies `Write`, `Edit`, `gh`.** |

## The two structural guarantees

**The dev agent cannot publish.** Only `finish.mjs` pushes, and only after the
session ends. You being in the room is not what makes this safe — the permission
layer is — so an agent that goes off the rails still has no route to the remote,
and everything lands on a draft PR on its own branch.

This is enforced twice, because the two mechanisms fail differently. The
permission file denies `git push`, `git remote` and `gh`, but that only binds a
session someone launched with `--settings dev.json` — it is advisory the moment
another launcher starts the agent. So `claim.mjs` also installs a `pre-push`
hook in the worktree that refuses any push without `AGENT_PUSH_OK`, which only
`finish.mjs` sets. That one binds the checkout rather than the session, so it
holds whatever opened it. Worktrees share `.git/hooks` with the main checkout,
so the hook is scoped with `extensions.worktreeConfig` and a per-worktree
`core.hooksPath`; your own pushes from the main tree are untouched.

**The reviewer cannot edit or approve.** It starts in a fresh session with no
memory of writing the code, and it's read-only. Both properties are enforced by
the permission layer rather than by asking politely, because an approval only
means something if the reviewer couldn't have quietly fixed things first.

**Nothing merges without you.** `state:approved` is written by no *unattended*
script in this directory — not `claim.mjs`, not `finish.mjs`, not `review.mjs`,
all of which run without you watching — and `ship.mjs` refuses to run without
it. `approve.mjs` sets the label, but only ever when you type the command
yourself, same as clicking the button on GitHub; it never runs as part of a
pipeline step, and no script here chains it into a merge. That label is the one
point in the pipeline where a human says yes, so nothing here sets it *and*
merges in one breath — `ship.mjs` still requires the label to already be there
when it starts. `state:done` is the mirror image: only `pr-closed.yml` writes
it, from the `Closes #N` trailer, so nothing here can mark work finished that
GitHub does not agree is merged.

`ship.mjs` is also the only thing that cleans up. Worktrees, branches and
`.agent-state` files for merged tickets used to accumulate until someone noticed;
running it on an already-merged ticket skips the approval requirement, because
the merge consumed that label and what is left is only litter.

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
  into the PR body, and refreshes that body on every later revision run, so the
  description never describes an earlier round. No report means a placeholder
  body on a new PR, no change to an existing one, and a warning either way.

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

**`refusing push: this worktree publishes through automation/finish.mjs`** —
working as intended: something tried to push from an agent worktree directly.
Run `node automation/finish.mjs 42` instead. If you genuinely need to push by
hand from there, `AGENT_PUSH_OK=1 git push` is the deliberate override.

**`#42 is state:needs-you, not state:approved`** — `ship.mjs` will not merge a
ticket you have not approved. Read the PR and the reviewer's findings, then set
the label — `node automation/approve.mjs 42` does that from here, or use the
GitHub UI. There is no flag on `ship.mjs` itself to skip this.

**`CI is not green on PR #N`** — the merge did not happen and nothing was
deleted. Fix the branch, re-run `dev.mjs 42` to push the fix, then `ship.mjs 42`
again. `ship.mjs` is the first thing in the pipeline that looks at CI at all: the
reviewer reads the diff, not the build.

**`could not remove the worktree`** — the merge succeeded; only the tidy-up
stopped. Something in that checkout is modified or untracked. Look at it before
running the `--force` command it printed, because that is the one thing here that
destroys work rather than filing it somewhere recoverable.

**`No worktree recorded for #42`** — `finish.mjs` reads the worktree path from
`.agent-state/issue-42.json`, and that ticket was never claimed (or the state
file was deleted). Run `node automation/claim.mjs 42` first.

# The pipeline

Tickets live on GitHub. Agents run on your machine. **Nothing polls, and nothing
runs in the background** — you start every run yourself, and the reviewer is
spawned directly by the dev session as it exits.

You build **with** the dev agent, not by dispatching it: `dev.mjs` opens a Claude
session in your terminal and gets out of the way until you exit. Everything
around that session is automated — worktree, commit sweep, push, draft PR, and
the review that follows.

## The loop

```
  you file a ticket ──────────────► GitHub  (forms + readiness gate)
                                       │
              you pick one, state:ready │
                                       ▼
  $ node automation/dev.mjs 42     ┌─────────────┐
                                   │ dev session │  Claude in your terminal
                                   │  (worktree) │  you steer it, it asks you
                                   └──────┬──────┘
                                          │ commits locally; you exit
                                          │ runner pushes + opens DRAFT PR
                                          ▼
                                   ┌─────────────┐
                                   │  reviewer   │  spawned directly — this is
                                   │ (read-only) │  the whole "auto review"
                                   └──────┬──────┘
                                          │ posts findings to the PR
                                          ▼
                              ╔═══════════════════════╗
                              ║      state:needs-you  ║ ◄── you read the diff
                              ║  draft PR + findings  ║     and the findings
                              ╚═══════════╦═══════════╝
                                          │
                    ┌─────────────────────┴──────────────────┐
                    │                                        │
        comment what you want changed              happy with it
                    │                                        │
        $ node automation/dev.mjs 42                  label state:approved,
        (resumes the same session,                    then ship.mjs 42
         re-reviews automatically)                          │
                    │                                        ▼
                    └────────────► back to needs-you      state:done
```

## Why there's no watcher

Polling exists to answer *"is there work for me?"* — a question that only
matters when a process is already running, idle, and can't see where work comes
from. Starting the dev agent by hand removes the first condition, and that
removes the second: the only remaining hand-off is dev → review, and both are in
the same process tree. `finish.mjs` just spawns `review.mjs`.

So GitHub is the ticket store, the diff host, and the record. It never tells
your machine to do anything.

## Commands

```bash
node automation/dev.mjs           # list the ready pool, pick one
node automation/dev.mjs 42        # build ticket 42 (or continue it)
node automation/dev.mjs 42 --fresh      # discard the agent's memory, start cold
node automation/dev.mjs 42 --no-review  # stop after the PR

node automation/review.mjs 42     # re-review by hand

node automation/ship.mjs 42       # merge an approved ticket and clean up after it
node automation/ship.mjs 42 --dry-run   # check the preconditions, change nothing
```

Re-running `dev.mjs 42` on a ticket that already has a PR is a **revision run**:
it collects every comment you have left since the last session, resumes that same
session so the agent still has its context, and puts you back in front of it.
Exiting pushes and re-reviews as before.

## Reading the result

The PR body opens with **Summary** then **Approach** — the shape the agent
settled on before writing code, and where implementing it changed its mind. Read
those two before the diff. A design you disagree with is much cheaper to catch
there than three hundred lines later, and the reviewer checks the diff against
that stated approach, so an unexplained mismatch comes back as a finding.

## The dev session

`dev.mjs` does the setup — worktree, branch, the ticket and any new comments
written to `.agent/task.md` — and then hands you a Claude session attached to
your terminal, in that worktree. You are in the loop for the whole build: it
states its approach before writing code, asks you when a guess would be
expensive to undo, and you steer it.

Exit when you are happy. The runner takes over again from there: it sweeps any
uncommitted work into a commit, pushes, opens the draft PR, and starts the
reviewer.

The PR body comes from `.agent/report.md`, which the agent writes before
finishing, and a revision run refreshes it — so the description always describes
the commits actually on the branch rather than the first round. Both files live
in a gitignored `.agent/` directory inside the worktree, so nothing about the
plumbing reaches a commit. If the session ends without a report — you quit
early, or it forgot — a new PR gets a placeholder body and an existing one keeps
the description it already has, rather than losing a good one to the
placeholder. Either way the runner says so on the way out.

## Giving feedback

Just comment on the PR normally. There are no slash commands — nothing is
listening for them, and a plain comment is what you'd write anyway. The next
`dev.mjs` session picks up everything newer than the last one.

## States

One `state:*` label per issue, enforced by `ticket-guard.yml`.

| Label | Meaning |
| --- | --- |
| `state:backlog` | Captured, not groomed. |
| `state:ready` | Your shortlist when you sit down to work. |
| `state:building` | A dev session is open. |
| `state:reviewing` | A review run is in progress. |
| `state:needs-you` | Machine's done. Your turn. |
| `state:approved` | You approved. `ship.mjs` refuses to merge without it. |
| `state:done` | Merged. Set by the `pr-closed` workflow, never by a script. |
| `state:blocked` | Needs a decision or something external. Set automatically if a run crashes. |

Nothing on your machine watches these. They're for *you* — a filter for "what
should I start?" and "what's waiting on me?"

## What GitHub Actions does

Coordination only. It dispatches nothing.

- **`ticket-guard`** — one state label at a time, and refuses `state:ready` for
  a ticket with no checkable acceptance criteria. The highest-value automation
  here. The agent can ask you what you meant now, but a ticket too vague to have
  acceptance criteria is one you have not finished thinking about — and the
  session is a bad place to discover that.
- **`epic-split`** — label an epic `split` and each Breakdown checkbox becomes a
  real child ticket. Deterministic parsing, not a model inventing work.
- **`bootstrap-labels`** — reconciles labels from `.github/labels.yml`.
- **`pr-closed`** — settles the state label when a PR merges or is abandoned.
- **`ci`** — two independent jobs. **server**: `dotnet restore/build/test`,
  skipped until a `.csproj` exists under `server/`. **client**: typecheck, lint,
  Vitest, build, skipped until `client/package.json` exists. Each half can go
  green on its own while the project is still being scaffolded.

## Safety

The dev agent is denied `git push`, `git remote`, `gh`, branch switching, and
every form of `rm` and `git clean` (`automation/settings/dev.json`). It writes
code and commits; **only `finish.mjs`
publishes.** Watching the session is not what keeps that true, which is the
point: even with you at the keyboard, work can only land on a draft PR on the
ticket's own branch — never on `main`, and never on a remote you did not intend.

The permission list binds a session launched with `--settings dev.json`, which
is not every session that could open in that worktree. So `claim.mjs` also
installs a `pre-push` hook that refuses any push without `AGENT_PUSH_OK` — set
only by `finish.mjs`. That one binds the checkout instead of the session, so it
survives an agent started by something else. It is scoped to the worktree with
`extensions.worktreeConfig` and a per-worktree `core.hooksPath`, so pushes from
your main checkout are unaffected.

The reviewer is denied `Write`, `Edit`, and `gh`. A reviewer that could quietly
fix what it finds would produce a clean report hiding its own work, and its
approval would stop distinguishing "this was correct" from "this was made
correct". It also runs in a **fresh session**, so it has no memory of writing the
code and no investment in defending it.

Neither agent can approve a PR. GitHub review state stays yours, so an approval
always means a human approved.

Both agents are pinned to `claude-opus-5` in their settings files, alongside the
permissions. Left unset, the model follows the CLI default, which moves on
upgrade — and for the attached dev run it would also inherit whatever `/model`
you last picked, so two runs of the same ticket a week apart would not be the
same experiment. The reviewer additionally carries a `--fallback-model`: it runs
unattended, and an overloaded model should degrade the review, not skip it.

## Worktrees

Each ticket builds in `../Auto-Chess-Game-work/issue-<n>/` on branch
`agent/<n>-<slug>`, so your main checkout stays free while a session is open.

```bash
git worktree list
git worktree remove ../Auto-Chess-Game-work/issue-42
git worktree prune
```

`dev.mjs` is one blocking terminal, so it builds one ticket at a time. To run
several at once, use its two halves directly and let anything you like own the
sessions in between:

```bash
node automation/claim.mjs 42   # worktree, branch, task file, push guard
node automation/finish.mjs 42  # sweep → push → draft PR → reviewer
```

Session state is keyed by issue number, and so are the state labels, so parallel
tickets do not collide. What the split gives up is session resume: `--fresh` and
`--resume` need the Claude session id, which only the attached path gets back.

## Session state

`.agent-state/issue-<n>.json` (gitignored) holds the Claude session id, PR
number, branch, and when feedback was last swept. Deleting one, or passing
`--fresh`, makes the next session start cold against the same branch — useful
when an agent has talked itself into a bad approach.
